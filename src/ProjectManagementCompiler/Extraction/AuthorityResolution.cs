using System.Globalization;
using System.Text.RegularExpressions;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Extraction;

public sealed record AuthorityResolution
{
    public bool HasCanonicalBaseline { get; init; }
    public PlanningDocument AuthorityDocument { get; init; } = null!;
    public ExtractedPlanningBaseline Baseline { get; init; } = new();
    public string? ProjectId => Baseline.ProjectId;
    public string? ProjectName => Baseline.ProjectName;
    public string? BaselineId => Baseline.BaselineId;
    public string? BaselineVersion => Baseline.BaselineVersion;
    public IReadOnlyList<PlanningDocument> Documents { get; init; } = Array.Empty<PlanningDocument>();
    public IReadOnlyList<PlanningTableRow> PhaseRows { get; init; } = Array.Empty<PlanningTableRow>();
    public IReadOnlyDictionary<string, ExtractedPlanningFact> PolicyFacts { get; init; } = new Dictionary<string, ExtractedPlanningFact>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ImportWarning> Conflicts { get; init; } = Array.Empty<ImportWarning>();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();

    public static AuthorityResolution Resolve(RepositorySnapshot snapshot)
    {
        var discovery = new IdeaPlanningDiscovery().Discover(snapshot);
        var documents = discovery.Documents;
        var diagnostics = new List<ImportWarning>(discovery.Diagnostics);
        var allRows = new Dictionary<PlanningDocument, PlanningParseResult>();

        foreach (var document in documents)
        {
            var result = document.Source.Format == SourceDocumentFormat.Html
                ? HtmlTableParser.Parse(document)
                : MarkdownTableParser.Parse(document);
            allRows[document] = result;
            diagnostics.AddRange(result.Diagnostics);
        }

        var authority = documents.FirstOrDefault(document => document.Kind == PlanningDocumentKind.Doc07);
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
        foreach (var document in documents.OrderBy(document => document.AuthorityRank))
        {
            if (!allRows.TryGetValue(document, out var result))
            {
                continue;
            }

            foreach (var row in result.Rows)
            {
                if (!TryGetKeyValue(row, out var key, out var value))
                {
                    continue;
                }

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
            }
        }

        if (authority is not null)
        {
            diagnostics.AddRange(FindStaleSubordinateReferences(authority, documents));
        }

        var authorityRows = authority is not null && allRows.TryGetValue(authority, out var authorityResult)
            ? authorityResult.Rows
            : Array.Empty<PlanningTableRow>();
        var phaseRows = authorityRows
            .Where(row => row.Headers.Any(header => PlanningParserSupport.Normalize(header) is "PHASE ID" or "PHASE"))
            .ToArray();
        var baseline = new ExtractedPlanningBaseline
        {
            ProjectId = FactValue(facts, "Project ID"),
            ProjectName = FactValue(facts, "Project name") ?? Heading(authority),
            BaselineId = FactValue(facts, "Baseline ID"),
            BaselineVersion = FactValue(facts, "Baseline version"),
            Status = FactValue(facts, "Status"),
            AuthorityDocumentId = authority?.Source.Id ?? string.Empty,
            PlanningStart = DateFact(facts, "Planning start"),
            PlanningFinish = DateFact(facts, "Planning finish"),
            TargetDate = DateFact(facts, "Target date"),
            PlannedEffortHours = DecimalFact(facts, "Authoritative effort"),
            ReserveHours = DecimalFact(facts, "Initial reserve"),
            CapacityHours = DecimalFact(facts, "Capacity")
        };

        diagnostics.AddRange(conflicts);
        return new AuthorityResolution
        {
            HasCanonicalBaseline = documents.Any(document => document.Kind == PlanningDocumentKind.Doc07)
                && documents.Any(document => document.Kind == PlanningDocumentKind.AppendixA),
            AuthorityDocument = authority!,
            Baseline = baseline,
            Documents = documents,
            PhaseRows = phaseRows,
            PolicyFacts = facts,
            Conflicts = conflicts,
            Diagnostics = diagnostics
        };
    }

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

    private static IReadOnlyList<ImportWarning> FindStaleSubordinateReferences(PlanningDocument authority, IReadOnlyList<PlanningDocument> documents)
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
                if (subordinate == current)
                {
                    continue;
                }

                warnings.Add(new ImportWarning
                {
                    Id = $"STALE_SUBORDINATE_REFERENCE:{document.Source.RelativeFile}:{subordinate}",
                    Severity = WarningSeverity.Warning,
                    Code = "STALE_SUBORDINATE_REFERENCE",
                    Message = $"'{document.Source.RelativeFile}' references DOC-07@{subordinate}, while the current authority is DOC-07@{current}.",
                    SourceReferences = [document.Source.SourceReference, authority.Source.SourceReference]
                });
            }
        }

        return warnings;
    }
}

public sealed class AuthorityResolver
{
    public AuthorityResolution Resolve(RepositorySnapshot snapshot) => AuthorityResolution.Resolve(snapshot);
}
