using System.Globalization;
using System.Text.RegularExpressions;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Extraction;

public sealed record AuthorityResolution
{
    public bool HasCanonicalBaseline { get; init; }
    public PlanningDocument? AuthorityDocument { get; init; }
    public ExtractedPlanningBaseline Baseline { get; init; } = new();
    public string? ProjectId => Baseline.ProjectId;
    public string? ProjectName => Baseline.ProjectName;
    public string? BaselineId => Baseline.BaselineId;
    public string? BaselineVersion => Baseline.BaselineVersion;
    public IReadOnlyList<PlanningDocument> Documents { get; init; } = Array.Empty<PlanningDocument>();
    public IReadOnlyList<PlanningTableRow> Rows { get; init; } = Array.Empty<PlanningTableRow>();
    public IReadOnlyList<PlanningTableRow> PhaseRows { get; init; } = Array.Empty<PlanningTableRow>();
    public IReadOnlyDictionary<string, ExtractedPlanningFact> PolicyFacts { get; init; } = new Dictionary<string, ExtractedPlanningFact>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ImportWarning> Conflicts { get; init; } = Array.Empty<ImportWarning>();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();

    public static AuthorityResolution Resolve(RepositorySnapshot snapshot)
    {
        var discovery = new IdeaPlanningDiscovery().Discover(snapshot);
        var documents = discovery.Documents;
        var diagnostics = new List<ImportWarning>(discovery.Diagnostics);
        var parsed = new Dictionary<PlanningDocument, PlanningParseResult>();

        foreach (var document in documents)
        {
            var result = document.Source.Format == SourceDocumentFormat.Html
                ? HtmlTableParser.Parse(document)
                : MarkdownTableParser.Parse(document);
            parsed[document] = result;
            diagnostics.AddRange(result.Diagnostics);
        }

        var authority = documents.FirstOrDefault(document => document.Kind == PlanningDocumentKind.Doc07);
        var appendix = documents.FirstOrDefault(document => document.Kind == PlanningDocumentKind.AppendixA);
        if (authority is null)
        {
            diagnostics.Add(new ImportWarning
            {
                Id = "NO_PLANNING_AUTHORITY",
                Severity = WarningSeverity.Error,
                Code = "NO_PLANNING_AUTHORITY",
                Message = "DOC-07 was not captured, so no recognized planning authority is available."
            });
        }

        var facts = new Dictionary<string, ExtractedPlanningFact>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<ImportWarning>();
        var rowIdentities = new Dictionary<string, (PlanningTableRow Row, string Signature)>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents.OrderBy(document => document.AuthorityRank))
        {
            if (!parsed.TryGetValue(document, out var result))
            {
                continue;
            }

            foreach (var row in result.Rows)
            {
                if (TryGetKeyValue(row, out var key, out var value))
                {
                    var fact = new ExtractedPlanningFact
                    {
                        Key = key,
                        Value = value,
                        Row = row,
                        AuthorityRank = document.AuthorityRank
                    };
                    if (facts.TryGetValue(key, out var existing) && !string.Equals(existing.Value, value, StringComparison.Ordinal))
                    {
                        conflicts.Add(new ImportWarning
                        {
                            Id = $"CONFLICTING_BASELINE:{key}:{document.Source.RelativeFile}",
                            Severity = WarningSeverity.Warning,
                            Code = "CONFLICTING_BASELINE",
                            Message = $"Sources disagree on '{key}'; authority rank {existing.AuthorityRank} retains '{existing.Value}' over '{value}'.",
                            AffectedIds = [key],
                            SourceReferences = [existing.Row.SourceReference, row.SourceReference]
                        });
                    }
                    else if (!facts.ContainsKey(key))
                    {
                        facts[key] = fact;
                    }

                    continue;
                }

                if (!TryGetRowIdentity(row, out var identity))
                {
                    continue;
                }

                var signature = RowSignature(row);
                if (rowIdentities.TryGetValue(identity, out var existingRow)
                    && !string.Equals(existingRow.Signature, signature, StringComparison.Ordinal)
                    && !string.Equals(existingRow.Row.SourceRelativePath, row.SourceRelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    conflicts.Add(new ImportWarning
                    {
                        Id = $"CONFLICTING_ROW:{identity}:{document.Source.RelativeFile}",
                        Severity = WarningSeverity.Warning,
                        Code = "CONFLICTING_ROW",
                        Message = $"Sources disagree on ordinary logical row '{identity}'; both row values are retained.",
                        AffectedIds = [identity],
                        SourceReferences = [existingRow.Row.SourceReference, row.SourceReference]
                    });
                }
                else if (!rowIdentities.ContainsKey(identity))
                {
                    rowIdentities[identity] = (row, signature);
                }
            }
        }

        if (authority is not null)
        {
            diagnostics.AddRange(FindSubordinateReferences(authority, documents));
        }

        var authorityRows = authority is not null && parsed.TryGetValue(authority, out var authorityResult)
            ? authorityResult.Rows
            : Array.Empty<PlanningTableRow>();
        var authorityFacts = authorityRows
            .Where(row => TryGetKeyValue(row, out _, out _))
            .Select(row =>
            {
                TryGetKeyValue(row, out var key, out var value);
                return new ExtractedPlanningFact
                {
                    Key = key,
                    Value = value,
                    Row = row,
                    AuthorityRank = authority!.AuthorityRank
                };
            })
            .GroupBy(fact => fact.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var phaseRows = authorityRows
            .Where(row => row.Headers.Any(header => PlanningParserSupport.Normalize(header) is "PHASE ID" or "PHASE"))
            .ToArray();
        var allRows = documents
            .SelectMany(document => parsed.TryGetValue(document, out var result) ? result.Rows : Array.Empty<PlanningTableRow>())
            .ToArray();
        var baseline = new ExtractedPlanningBaseline
        {
            ProjectId = FactValue(authorityFacts, "Project ID"),
            ProjectName = FactValue(authorityFacts, "Project name") ?? Heading(authority),
            BaselineId = FactValue(authorityFacts, "Baseline ID"),
            BaselineVersion = FactValue(authorityFacts, "Baseline version"),
            Status = FactValue(authorityFacts, "Status"),
            AuthorityDocumentId = authority?.Source.Id ?? string.Empty,
            PlanningStart = DateFact(authorityFacts, "Planning start"),
            PlanningFinish = DateFact(authorityFacts, "Planning finish"),
            TargetDate = DateFact(authorityFacts, "Target date"),
            PlannedEffortHours = DecimalFact(authorityFacts, "Authoritative effort"),
            ReserveHours = DecimalFact(authorityFacts, "Initial reserve"),
            CapacityHours = DecimalFact(authorityFacts, "Capacity")
        };

        diagnostics.AddRange(conflicts);
        return new AuthorityResolution
        {
            HasCanonicalBaseline = authority is not null
                && appendix is not null
                && IsValidRequiredDocument(authority, parsed)
                && IsValidRequiredDocument(appendix, parsed),
            AuthorityDocument = authority,
            Baseline = baseline,
            Documents = documents,
            Rows = allRows,
            PhaseRows = phaseRows,
            PolicyFacts = facts,
            Conflicts = conflicts,
            Diagnostics = diagnostics
        };
    }

    private static bool IsValidRequiredDocument(PlanningDocument document, IReadOnlyDictionary<PlanningDocument, PlanningParseResult> parsed) =>
        parsed.TryGetValue(document, out var result)
        && result.Diagnostics.All(diagnostic => diagnostic.Severity != WarningSeverity.Error);

    private static bool TryGetKeyValue(PlanningTableRow row, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        if (row.Headers.Count < 2)
        {
            return false;
        }

        var firstHeader = PlanningParserSupport.Normalize(row.Headers[0]);
        var secondHeader = PlanningParserSupport.Normalize(row.Headers[1]);
        if (firstHeader is not ("FIELD" or "POLICY") || secondHeader != "VALUE")
        {
            return false;
        }

        key = row.Cells[row.Headers[0]].Trim();
        value = row.Cells[row.Headers[1]].Trim();
        return key.Length > 0;
    }

    private static bool TryGetRowIdentity(PlanningTableRow row, out string identity)
    {
        identity = string.Empty;
        foreach (var header in row.Headers)
        {
            var normalizedHeader = PlanningParserSupport.Normalize(header);
            if (normalizedHeader is not ("ID" or "PHASE ID" or "WORK PACKAGE ID" or "CODE" or "KEY" or "PHASE"))
            {
                continue;
            }

            var value = row.Cells[header].Trim();
            if (value.Length > 0)
            {
                identity = $"{normalizedHeader}={value}";
                return true;
            }
        }

        return false;
    }

    private static string RowSignature(PlanningTableRow row) =>
        string.Join("\u001f", row.Headers.Select(header => $"{PlanningParserSupport.Normalize(header)}={row.Cells[header].Trim()}"));

    private static string? FactValue(IReadOnlyDictionary<string, ExtractedPlanningFact> facts, string key) =>
        facts.TryGetValue(key, out var fact) ? fact.Value : null;

    private static DateOnly? DateFact(IReadOnlyDictionary<string, ExtractedPlanningFact> facts, string key) =>
        facts.TryGetValue(key, out var fact)
        && DateOnly.TryParseExact(fact.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : null;

    private static decimal? DecimalFact(IReadOnlyDictionary<string, ExtractedPlanningFact> facts, string key)
    {
        if (!facts.TryGetValue(key, out var fact))
        {
            return null;
        }

        var valueText = Regex.Replace(fact.Value.Trim(), @"\s+(?:hours?|h)$", string.Empty, RegexOptions.IgnoreCase);
        return decimal.TryParse(valueText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string? Heading(PlanningDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        return document.Source.Content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(PlanningParserSupport.MarkdownHeading)
            .FirstOrDefault(heading => !string.IsNullOrWhiteSpace(heading));
    }

    private static IReadOnlyList<ImportWarning> FindSubordinateReferences(PlanningDocument authority, IReadOnlyList<PlanningDocument> documents)
    {
        var current = Regex.Match(authority.Source.Content, @"DOC-07@(?<version>\d+(?:\.\d+)+)", RegexOptions.IgnoreCase).Groups["version"].Value;
        if (string.IsNullOrEmpty(current))
        {
            return Array.Empty<ImportWarning>();
        }

        var warnings = new List<ImportWarning>();
        foreach (var document in documents.Where(document => document.AuthorityRank > authority.AuthorityRank))
        {
            foreach (Match match in Regex.Matches(document.Source.Content, @"DOC-07@(?<version>\d+(?:\.\d+)+)", RegexOptions.IgnoreCase))
            {
                var subordinate = match.Groups["version"].Value;
                var comparison = CompareDottedVersions(subordinate, current);
                if (comparison == 0)
                {
                    continue;
                }

                var code = comparison < 0 ? "STALE_SUBORDINATE_REFERENCE" : "FUTURE_SUBORDINATE_REFERENCE";
                var age = comparison < 0 ? "older" : "newer";
                warnings.Add(new ImportWarning
                {
                    Id = $"{code}:{document.Source.RelativeFile}:{subordinate}",
                    Severity = WarningSeverity.Warning,
                    Code = code,
                    Message = $"'{document.Source.RelativeFile}' references {age} DOC-07@{subordinate}, while the current authority is DOC-07@{current}.",
                    SourceReferences = [document.Source.SourceReference, authority.Source.SourceReference]
                });
            }
        }

        return warnings;
    }

    private static int CompareDottedVersions(string left, string right)
    {
        var leftParts = left.Split('.').Select(int.Parse).ToArray();
        var rightParts = right.Split('.').Select(int.Parse).ToArray();
        var length = Math.Max(leftParts.Length, rightParts.Length);
        for (var index = 0; index < length; index++)
        {
            var leftPart = index < leftParts.Length ? leftParts[index] : 0;
            var rightPart = index < rightParts.Length ? rightParts[index] : 0;
            var comparison = leftPart.CompareTo(rightPart);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }
}

public sealed class AuthorityResolver
{
    public AuthorityResolution Resolve(RepositorySnapshot snapshot) => AuthorityResolution.Resolve(snapshot);
}
