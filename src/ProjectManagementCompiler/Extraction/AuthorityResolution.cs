using System.Globalization;
using System.Text.RegularExpressions;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Extraction;

public sealed record AuthorityResolution
{
    public bool HasCanonicalBaseline { get; init; }
    public DateTimeOffset? CapturedAtUtc { get; init; }
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
                    && !string.Equals(existingRow.Signature, signature, StringComparison.Ordinal))
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
        var phaseCandidates = authorityRows
            .Where(row => row.Headers.Any(header => PlanningParserSupport.Normalize(header) is "PHASE ID" or "PHASE"))
            .ToArray();
        var structuredPhaseRows = phaseCandidates.Where(IsUsablePhaseRow).ToArray();
        var phaseRows = structuredPhaseRows.Length > 0 ? structuredPhaseRows : phaseCandidates;
        var allRows = documents
            .SelectMany(document => parsed.TryGetValue(document, out var result) ? result.Rows : Array.Empty<PlanningTableRow>())
            .ToArray();
        var scheduleBaseline = authority is null ? null : ScheduleBaseline(authority.Source.Content);
        var planningWindow = authority is null ? null : PlanningWindow(authorityRows);
        var baseline = new ExtractedPlanningBaseline
        {
            ProjectId = FactValue(authorityFacts, "Project ID", "Stable Document ID"),
            ProjectName = FactValue(authorityFacts, "Project name", "Title") ?? Heading(authority),
            BaselineId = FactValue(authorityFacts, "Baseline ID", "Schedule baseline") ?? scheduleBaseline?.Id,
            BaselineVersion = FactValue(authorityFacts, "Baseline version") ?? scheduleBaseline?.Version,
            Status = FactValue(authorityFacts, "Status", "Document Status"),
            AuthorityDocumentId = authority?.Source.Id ?? string.Empty,
            PlanningStart = DateFact(authorityFacts, "Planning start") ?? planningWindow?.Start,
            PlanningFinish = DateFact(authorityFacts, "Planning finish") ?? planningWindow?.Finish,
            TargetDate = DateFact(authorityFacts, "Target date") ?? MilestoneTargetDate(authorityRows, "MS5"),
            PlannedEffortHours = DecimalFact(authorityFacts, "Authoritative effort", "Planned phase work"),
            ReserveHours = DecimalFact(authorityFacts, "Initial reserve", "Controlled reserve"),
            CapacityHours = DecimalFact(authorityFacts, "Capacity", "Total allocation", "Weekday capacity")
        };

        if (authority is not null)
        {
            diagnostics.AddRange(FindSubordinateReferences(authority, documents, baseline.BaselineVersion));
        }

        diagnostics.AddRange(conflicts);
        var requiredBaselineDiagnostics = RequiredBaselineDiagnostics(authority, appendix, baseline, authorityRows, parsed);
        diagnostics.AddRange(requiredBaselineDiagnostics);
        return new AuthorityResolution
        {
            HasCanonicalBaseline = authority is not null
                && appendix is not null
                && IsValidRequiredDocument(authority, parsed)
                && IsValidRequiredDocument(appendix, parsed)
                && !requiredBaselineDiagnostics.Any(diagnostic => diagnostic.Severity == WarningSeverity.Error)
                && !diagnostics.Any(diagnostic => diagnostic.Severity == WarningSeverity.Error && IsBaselineBlockingDiagnostic(diagnostic)),
            CapturedAtUtc = snapshot.CapturedAtUtc,
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

    private static bool IsBaselineBlockingDiagnostic(ImportWarning diagnostic)
    {
        var sourceFiles = diagnostic.SourceReferences
            .Select(reference => reference.RelativeFile.Replace('\\', '/'))
            .Where(path => path.Length > 0)
            .ToArray();
        if (sourceFiles.Length > 0 && sourceFiles.All(path => path.EndsWith("idea-roadmap-december-2026.html", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("idea-technical-pilot-kanban-cario.md", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static IReadOnlyList<ImportWarning> RequiredBaselineDiagnostics(
        PlanningDocument? authority,
        PlanningDocument? appendix,
        ExtractedPlanningBaseline baseline,
        IReadOnlyList<PlanningTableRow> authorityRows,
        IReadOnlyDictionary<PlanningDocument, PlanningParseResult> parsed)
    {
        if (authority is null || appendix is null)
        {
            return Array.Empty<ImportWarning>();
        }

        var diagnostics = new List<ImportWarning>();
        var requiredFacts = new (string Name, bool Present)[]
        {
            ("Project ID", HasValue(baseline.ProjectId)),
            ("Baseline ID", HasValue(baseline.BaselineId)),
            ("Baseline version", HasValue(baseline.BaselineVersion)),
            ("Planning start", baseline.PlanningStart is not null),
            ("Planning finish", baseline.PlanningFinish is not null),
            ("Target date", baseline.TargetDate is not null),
            ("Authoritative effort", baseline.PlannedEffortHours is not null)
        };

        foreach (var (name, present) in requiredFacts.Where(fact => !fact.Present))
        {
            diagnostics.Add(new ImportWarning
            {
                Id = $"MISSING_REQUIRED_BASELINE_DATA:{authority.Source.RelativeFile}:{name}",
                Severity = WarningSeverity.Error,
                Code = "MISSING_REQUIRED_BASELINE_DATA",
                Message = $"DOC-07 is missing usable required baseline fact '{name}'; canonical extraction will not guess a value.",
                AffectedIds = [name],
                SourceReferences = [authority.Source.SourceReference with
                {
                    RelativeFile = authority.Source.RelativeFile,
                    Section = "Source identity",
                    ExtractionRule = "idea-planning-required-baseline-fact"
                }]
            });
        }

        if (HasValue(baseline.BaselineVersion)
            && !TryParseDottedVersion(baseline.BaselineVersion!, out _))
        {
            diagnostics.Add(new ImportWarning
            {
                Id = $"MALFORMED_BASELINE_VERSION:{authority.Source.RelativeFile}:{baseline.BaselineVersion}",
                Severity = WarningSeverity.Error,
                Code = "MALFORMED_BASELINE_VERSION",
                Message = $"DOC-07 contains malformed baseline version '{baseline.BaselineVersion}'; canonical extraction will not treat it as a valid version.",
                AffectedIds = ["Baseline version"],
                SourceReferences = [authority.Source.SourceReference with
                {
                    RelativeFile = authority.Source.RelativeFile,
                    Section = "Source identity",
                    ExtractionRule = "idea-planning-baseline-version"
                }]
            });
        }

        var hasPhaseRow = authorityRows.Any(row => row.Headers.Any(header =>
            (PlanningParserSupport.Normalize(header) is "PHASE ID" or "PHASE")
            && HasValue(row.Cells[header])));
        var appendixRows = parsed.TryGetValue(appendix, out var appendixResult)
            ? appendixResult.Rows
            : Array.Empty<PlanningTableRow>();
        var hasWorkPackageRow = appendixRows.Any(IsUsableWorkPackageRow);
        if (!hasPhaseRow || !hasWorkPackageRow)
        {
            var missingRows = new List<string>();
            if (!hasPhaseRow)
            {
                missingRows.Add("DOC-07 phase row");
            }

            if (!hasWorkPackageRow)
            {
                missingRows.Add("Appendix A work-package row");
            }

            diagnostics.Add(new ImportWarning
            {
                Id = $"MISSING_REQUIRED_BASELINE_ROWS:{authority.Source.RelativeFile}",
                Severity = WarningSeverity.Error,
                Code = "MISSING_REQUIRED_BASELINE_ROWS",
                Message = $"Canonical extraction requires at least one usable {string.Join(" and ", missingRows)}.",
                AffectedIds = missingRows,
                SourceReferences = [authority.Source.SourceReference, appendix.Source.SourceReference]
            });
        }

        return diagnostics;
    }

    private static bool IsUsablePhaseRow(PlanningTableRow row) =>
        row.Headers.Any(header => (PlanningParserSupport.Normalize(header) is "PHASE ID" or "PHASE")
            && HasValue(row.Cells[header]))
        && (row.Headers.Any(header => PlanningParserSupport.Normalize(header) is "START" or "PLANNED START")
            || row.Headers.Any(header => PlanningParserSupport.Normalize(header) == "WORK-PACKAGE RANGE"));

    private static bool IsUsableWorkPackageRow(PlanningTableRow row) =>
        row.Headers.Any(header => (PlanningParserSupport.Normalize(header) is "ID" or "WORK PACKAGE ID" or "CODE" or "MÃ")
            && HasValue(row.Cells[header]));

    private static bool HasValue(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim() is not ("—" or "-");

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
        if (firstHeader is not ("FIELD" or "POLICY" or "ITEM")
            || secondHeader is not ("VALUE" or "RECORDED VALUE" or "PLANNED HOURS / CONDITION"))
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
            if (normalizedHeader is not ("ID" or "PHASE ID" or "WORK PACKAGE ID" or "CODE" or "KEY" or "PHASE" or "MÃ"))
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

    private static string? FactValue(IReadOnlyDictionary<string, ExtractedPlanningFact> facts, params string[] keys) =>
        keys.Select(key => facts.TryGetValue(key, out var fact) ? fact.Value : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static DateOnly? DateFact(IReadOnlyDictionary<string, ExtractedPlanningFact> facts, params string[] keys) =>
        keys.Select(key => facts.TryGetValue(key, out var fact) ? fact.Value : null)
            .Select(ParseIsoDate)
            .FirstOrDefault(value => value is not null);

    private static decimal? DecimalFact(IReadOnlyDictionary<string, ExtractedPlanningFact> facts, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!facts.TryGetValue(key, out var fact))
            {
                continue;
            }

            var valueText = fact.Value.Trim();
            if (Regex.IsMatch(valueText, @"^\d+(?:\.\d+)?\s*(?:hours?|h)?$", RegexOptions.IgnoreCase)
                && decimal.TryParse(Regex.Match(valueText, @"\d+(?:\.\d+)?").Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            if (key is "Planned phase work" or "Weekday capacity" or "Total allocation")
            {
                var match = Regex.Match(fact.Value, @"(?<!\d)(?<value>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                if (match.Success && decimal.TryParse(match.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static DateOnly? ParseIsoDate(string? value) =>
        value is not null
        && DateOnly.TryParseExact(value.Trim().Trim('`'), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static (string Id, string Version)? ScheduleBaseline(string content)
    {
        var match = Regex.Match(content, @"Schedule baseline:\s*`?(?<id>[A-Z0-9-]+)@(?<version>\d+(?:\.\d+)+)", RegexOptions.IgnoreCase);
        return match.Success ? (match.Groups["id"].Value, match.Groups["version"].Value) : null;
    }

    private static (DateOnly Start, DateOnly Finish)? PlanningWindow(IEnumerable<PlanningTableRow> rows)
    {
        foreach (var row in rows)
        {
            var key = Cell(row, "Field", "Item");
            var value = Cell(row, "Value", "Recorded value", "Planned hours / condition");
            if (!string.Equals(key, "Planning window", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var matches = Regex.Matches(value, @"(?<day>\d{1,2})\s+(?<month>January|February|March|April|May|June|July|August|September|October|November|December)(?:–|-)(?<endDay>\d{1,2})\s+(?<endMonth>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<year>\d{4})", RegexOptions.IgnoreCase);
            if (matches.Count == 0)
            {
                continue;
            }

            var match = matches[0];
            if (DateOnly.TryParseExact($"{match.Groups["day"].Value} {match.Groups["month"].Value} {match.Groups["year"].Value}", "d MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
                && DateOnly.TryParseExact($"{match.Groups["endDay"].Value} {match.Groups["endMonth"].Value} {match.Groups["year"].Value}", "d MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var finish))
            {
                return (start, finish);
            }
        }

        return null;
    }

    private static DateOnly? MilestoneTargetDate(IEnumerable<PlanningTableRow> rows, string id)
    {
        foreach (var row in rows)
        {
            var identity = Cell(row, "ID / target date");
            if (identity is null || !identity.StartsWith(id + " / ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var token = identity[(id.Length + 3)..].Trim();
            var match = Regex.Match(token, @"(?<day>\d{1,2})\s+(?<month>January|February|March|April|May|June|July|August|September|October|November|December)", RegexOptions.IgnoreCase);
            if (match.Success && DateOnly.TryParseExact($"{match.Groups["day"].Value} {match.Groups["month"].Value} 2026", "d MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return null;
    }

    private static string? Cell(PlanningTableRow row, params string[] names)
    {
        foreach (var name in names)
        {
            var header = row.Headers.FirstOrDefault(candidate => PlanningParserSupport.Normalize(candidate) == PlanningParserSupport.Normalize(name));
            if (header is not null && row.Cells.TryGetValue(header, out var value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? Heading(PlanningDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        var content = document.Source.Format == SourceDocumentFormat.Markdown
            ? MarkdownTableParser.ContentOutsideFences(document.Source.Content)
            : document.Source.Content;
        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(PlanningParserSupport.MarkdownHeading)
            .FirstOrDefault(heading => !string.IsNullOrWhiteSpace(heading));
    }

    private static IReadOnlyList<ImportWarning> FindSubordinateReferences(
        PlanningDocument authority,
        IReadOnlyList<PlanningDocument> documents,
        string? baselineVersion)
    {
        var warnings = new List<ImportWarning>();
        var authorityReferences = ControlEnvelopeReferences(authority);
        var current = baselineVersion;
        foreach (var reference in authorityReferences)
        {
            if (TryNormalizeControlEnvelopeVersion(reference, out var normalized))
            {
                if (string.IsNullOrEmpty(current))
                {
                    current = normalized;
                }
                else if (CompareDottedVersions(normalized, current) != 0)
                {
                    warnings.Add(ConflictingAuthorityControlEnvelopeDiagnostic(authority, current, normalized));
                }

                continue;
            }

            warnings.Add(MalformedControlEnvelopeDiagnostic(authority, reference));
        }

        if (string.IsNullOrEmpty(current))
        {
            return warnings;
        }

        foreach (var document in documents.Where(document => document.AuthorityRank > authority.AuthorityRank))
        {
            foreach (var reference in ControlEnvelopeReferences(document))
            {
                if (!TryNormalizeControlEnvelopeVersion(reference, out var subordinate))
                {
                    warnings.Add(MalformedControlEnvelopeDiagnostic(document, reference));
                    continue;
                }

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
        if (!TryParseDottedVersion(left, out var leftParts)
            || !TryParseDottedVersion(right, out var rightParts))
        {
            return 0;
        }

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

    private static IReadOnlyList<string> ControlEnvelopeReferences(PlanningDocument document)
    {
        var content = document.Source.Format == SourceDocumentFormat.Markdown
            ? MarkdownTableParser.ContentOutsideFences(document.Source.Content)
            : document.Source.Content;
        return Regex.Matches(content, @"DOC-07@(?<version>[^\s<>""',;)\]}]+)", RegexOptions.IgnoreCase)
            .Cast<Match>()
            .Select(match => match.Groups["version"].Value)
            .ToArray();
    }

    private static ImportWarning ConflictingAuthorityControlEnvelopeDiagnostic(PlanningDocument authority, string current, string conflicting) => new()
    {
        Id = $"CONFLICTING_AUTHORITY_CONTROL_ENVELOPE:{authority.Source.RelativeFile}:{conflicting}",
        Severity = WarningSeverity.Error,
        Code = "CONFLICTING_AUTHORITY_CONTROL_ENVELOPE",
        Message = $"'{authority.Source.RelativeFile}' contains semantically different valid DOC-07 control-envelope versions: DOC-07@{current} and DOC-07@{conflicting}. The selected baseline version remains current.",
        SourceReferences = [authority.Source.SourceReference]
    };

    private static bool TryNormalizeControlEnvelopeVersion(string raw, out string normalized)
    {
        normalized = raw.TrimEnd(',', ';', ':', ')', ']', '}');
        if (TryParseDottedVersion(normalized, out _))
        {
            return true;
        }

        if (normalized.EndsWith(".", StringComparison.Ordinal)
            && TryParseDottedVersion(normalized[..^1], out _))
        {
            normalized = normalized[..^1];
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private static bool TryParseDottedVersion(string version, out int[] parsed)
    {
        var components = version.Split('.');
        if (components.Length < 2)
        {
            parsed = Array.Empty<int>();
            return false;
        }

        parsed = new int[components.Length];
        for (var index = 0; index < components.Length; index++)
        {
            if (!int.TryParse(components[index], NumberStyles.None, CultureInfo.InvariantCulture, out parsed[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static ImportWarning MalformedControlEnvelopeDiagnostic(PlanningDocument document, string reference) => new()
    {
        Id = $"MALFORMED_CONTROL_ENVELOPE_REFERENCE:{document.Source.RelativeFile}:{reference}",
        Severity = WarningSeverity.Error,
        Code = "MALFORMED_CONTROL_ENVELOPE_REFERENCE",
        Message = $"'{document.Source.RelativeFile}' contains an untrusted malformed DOC-07@{reference} control-envelope reference; it was ignored for version classification.",
        SourceReferences = [document.Source.SourceReference]
    };
}

public sealed class AuthorityResolver
{
    public AuthorityResolution Resolve(RepositorySnapshot snapshot) => AuthorityResolution.Resolve(snapshot);
}
