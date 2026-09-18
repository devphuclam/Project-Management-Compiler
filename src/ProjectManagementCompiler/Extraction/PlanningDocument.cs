using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Extraction;

public enum PlanningDocumentKind
{
    Readme,
    Doc07,
    AppendixA,
    Gantt,
    Kanban
}

public sealed record PlanningDocument
{
    private PlanningDocument(SourceDocument source, PlanningDocumentKind kind)
    {
        Source = source;
        Kind = kind;
    }

    public SourceDocument Source { get; }
    public PlanningDocumentKind Kind { get; }
    public int AuthorityRank => Kind switch
    {
        PlanningDocumentKind.Doc07 => 1,
        PlanningDocumentKind.AppendixA => 2,
        PlanningDocumentKind.Gantt => 3,
        PlanningDocumentKind.Kanban => 4,
        PlanningDocumentKind.Readme => 5,
        _ => int.MaxValue
    };

    public IReadOnlyList<string> RequiredHeadings => Kind switch
    {
        PlanningDocumentKind.Doc07 => ["Source identity", "Phases"],
        PlanningDocumentKind.AppendixA => ["Appendix A"],
        PlanningDocumentKind.Kanban => ["Planning policy", "Delivery cards"],
        _ => Array.Empty<string>()
    };

    public static PlanningDocument Create(SourceDocument source)
    {
        if (!TryCreate(source, out var document))
        {
            throw new ArgumentException($"The source path is not a recognized planning document: {source.RelativeFile}", nameof(source));
        }

        return document;
    }

    public static bool TryCreate(SourceDocument source, out PlanningDocument document)
    {
        var path = source.RelativeFile.Replace('\\', '/');
        var kind = path switch
        {
            "README.md" => PlanningDocumentKind.Readme,
            "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md" => PlanningDocumentKind.Doc07,
            "docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md" => PlanningDocumentKind.AppendixA,
            "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html" => PlanningDocumentKind.Gantt,
            "docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md" => PlanningDocumentKind.Kanban,
            _ => (PlanningDocumentKind?)null
        };

        if (kind is null)
        {
            document = null!;
            return false;
        }

        document = new PlanningDocument(source, kind.Value);
        return true;
    }
}

public sealed record PlanningTableRow
{
    public string SourceRelativePath { get; init; } = string.Empty;
    public string? Section { get; init; }
    public int TableIndex { get; init; }
    public int RowIndex { get; init; }
    public int SourceLine { get; init; }
    public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> Cells { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> DataAttributes { get; init; } = new Dictionary<string, string>();
    public SourceReference SourceReference { get; init; } = new();
}

public sealed record PlanningParseResult
{
    public IReadOnlyList<PlanningTableRow> Rows { get; init; } = Array.Empty<PlanningTableRow>();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed record ExtractedPlanningFact
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public PlanningTableRow Row { get; init; } = new();
    public int AuthorityRank { get; init; }
}

public sealed record ExtractedPlanningBaseline
{
    public string? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public string? BaselineId { get; init; }
    public string? BaselineVersion { get; init; }
    public string? Status { get; init; }
    public string AuthorityDocumentId { get; init; } = string.Empty;
    public DateOnly? PlanningStart { get; init; }
    public DateOnly? PlanningFinish { get; init; }
    public DateOnly? TargetDate { get; init; }
    public decimal? PlannedEffortHours { get; init; }
    public decimal? ReserveHours { get; init; }
    public decimal? CapacityHours { get; init; }
}

internal static class PlanningParserSupport
{
    private static readonly Regex HeadingPattern = new(@"^\s{0,3}(?<marks>#{1,6})\s+(?<text>.+?)\s*#*\s*$", RegexOptions.Compiled);
    private static readonly Regex DatePattern = new(@"^(?<date>\d{4}-\d{2}-\d{2})(?:\s+(?:AM|PM))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? MarkdownHeading(string line) =>
        HeadingPattern.Match(line) is { Success: true } match
            ? match.Groups["text"].Value.Trim()
            : null;

    public static bool IsSeparator(string line) =>
        SplitPipeRow(line).Count > 0 && SplitPipeRow(line).All(cell => Regex.IsMatch(cell, @"^:?-{3,}:?$"));

    public static IReadOnlyList<string> SplitPipeRow(string line)
    {
        var value = line.Trim();
        if (value.StartsWith('|'))
        {
            value = value[1..];
        }

        if (value.EndsWith('|') && !IsEscapedPipe(value, value.Length - 1))
        {
            value = value[..^1];
        }

        var cells = new List<string>();
        var cell = new StringBuilder();
        for (var index = 0; index < value.Length;)
        {
            if (value[index] == '\\')
            {
                var runStart = index;
                while (index < value.Length && value[index] == '\\')
                {
                    index++;
                }

                var backslashCount = index - runStart;
                if (index < value.Length && value[index] == '|')
                {
                    if (backslashCount % 2 == 1)
                    {
                        cell.Append('\\', backslashCount - 1);
                        cell.Append('|');
                        index++;
                        continue;
                    }

                    cell.Append('\\', backslashCount);
                    continue;
                }

                cell.Append('\\', backslashCount);
                continue;
            }

            if (value[index] == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                index++;
                continue;
            }

            cell.Append(value[index]);
            index++;
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }

    private static bool IsEscapedPipe(string value, int pipeIndex)
    {
        var backslashes = 0;
        for (var index = pipeIndex - 1; index >= 0 && value[index] == '\\'; index--)
        {
            backslashes++;
        }

        return backslashes % 2 == 1;
    }

    public static string Normalize(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();

    public static PlanningTableRow CreateRow(
        PlanningDocument document,
        string? section,
        int tableIndex,
        int rowIndex,
        int sourceLine,
        IReadOnlyList<string> headers,
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, string>? dataAttributes = null)
    {
        if (headers.Count != values.Count)
        {
            throw new ArgumentException("Table headers and values must have the same number of cells.", nameof(values));
        }

        if (headers.GroupBy(Normalize, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Table headers must be unique.", nameof(headers));
        }

        var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            cells.Add(headers[index], values[index]);
        }

        return new PlanningTableRow
        {
            SourceRelativePath = document.Source.RelativeFile,
            Section = section,
            TableIndex = tableIndex,
            RowIndex = rowIndex,
            SourceLine = sourceLine,
            Headers = headers.ToArray(),
            Cells = cells,
            DataAttributes = dataAttributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            SourceReference = document.Source.SourceReference with
            {
                RelativeFile = document.Source.RelativeFile,
                Section = section,
                Table = $"table-{tableIndex:D2}",
                Item = $"row-{rowIndex:D3}",
                ExtractionRule = document.Source.Format == SourceDocumentFormat.Html
                    ? "idea-planning-html-table"
                    : "idea-planning-markdown-table",
                AuthorityRank = document.AuthorityRank,
                ConfidenceState = DataState.Known,
                ValidationState = ValidationState.Known
            }
        };
    }

    public static IReadOnlyList<ImportWarning> ValidateTableShape(
        PlanningDocument document,
        int tableIndex,
        int rowIndex,
        int sourceLine,
        IReadOnlyList<string> headers,
        IReadOnlyList<string> values)
    {
        var diagnostics = new List<ImportWarning>();
        var duplicateHeaders = headers
            .GroupBy(Normalize, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" / ", group))
            .ToArray();

        if (duplicateHeaders.Length > 0)
        {
            diagnostics.Add(new ImportWarning
            {
                Id = $"DUPLICATE_TABLE_HEADER:{document.Source.RelativeFile}:{tableIndex}:{string.Join(",", duplicateHeaders)}",
                Severity = WarningSeverity.Error,
                Code = "DUPLICATE_TABLE_HEADER",
                Message = $"Table {tableIndex} in '{document.Source.RelativeFile}' has duplicate headers: {string.Join(", ", duplicateHeaders)}.",
                SourceReferences = [document.Source.SourceReference with
                {
                    RelativeFile = document.Source.RelativeFile,
                    Table = $"table-{tableIndex:D2}",
                    Item = $"row-{rowIndex:D3}",
                    ExtractionRule = "idea-planning-table-shape"
                }]
            });
        }

        if (headers.Count != values.Count)
        {
            diagnostics.Add(new ImportWarning
            {
                Id = $"TABLE_CELL_COUNT_MISMATCH:{document.Source.RelativeFile}:{sourceLine}:{rowIndex}",
                Severity = WarningSeverity.Error,
                Code = "TABLE_CELL_COUNT_MISMATCH",
                Message = $"Table {tableIndex} row {rowIndex} in '{document.Source.RelativeFile}' has {values.Count} cells but requires exactly {headers.Count}.",
                SourceReferences = [document.Source.SourceReference with
                {
                    RelativeFile = document.Source.RelativeFile,
                    Table = $"table-{tableIndex:D2}",
                    Item = $"row-{rowIndex:D3}",
                    ExtractionRule = "idea-planning-table-shape"
                }]
            });
        }

        return diagnostics;
    }

    public static IReadOnlyList<ImportWarning> Validate(PlanningDocument document, PlanningTableRow row)
    {
        var diagnostics = new List<ImportWarning>();
        foreach (var pair in row.Cells)
        {
            var header = Normalize(pair.Key);
            var value = pair.Value.Trim();
            if (string.IsNullOrEmpty(value) || value is "—" or "-")
            {
                continue;
            }

            if (IsNumericHeader(header) && !TryParseNumeric(value, allowHoursSuffix: true, out _))
            {
                diagnostics.Add(Diagnostic("MALFORMED_NUMERIC", document, row, $"The value '{value}' in '{pair.Key}' is not a valid invariant numeric value."));
            }
            else if (IsDateHeader(header) && !IsDate(value))
            {
                diagnostics.Add(Diagnostic("MALFORMED_DATE", document, row, $"The value '{value}' in '{pair.Key}' is not a supported ISO date."));
            }
        }

        if (TryGetKeyValue(row, out var key, out var semanticValue))
        {
            var normalizedKey = Normalize(key);
            if (normalizedKey is "PLANNING START" or "PLANNING FINISH" or "TARGET DATE")
            {
                if (!IsIsoDate(semanticValue))
                {
                    diagnostics.Add(Diagnostic("MALFORMED_DATE", document, row, $"The value '{semanticValue}' for '{key}' is not a supported ISO date."));
                }
            }
            else if (normalizedKey is "AUTHORITATIVE EFFORT" or "INITIAL RESERVE" or "CAPACITY" or "WIP POLICY")
            {
                if (!TryParseNumeric(semanticValue, allowHoursSuffix: normalizedKey is not "WIP POLICY", out _))
                {
                    diagnostics.Add(Diagnostic("MALFORMED_NUMERIC", document, row, $"The value '{semanticValue}' for '{key}' is not a valid invariant numeric value."));
                }
            }
        }

        return diagnostics;
    }

    public static IReadOnlyList<ImportWarning> MissingHeadings(PlanningDocument document, IEnumerable<string> headings)
    {
        var actual = headings.Select(Normalize).ToArray();
        return document.RequiredHeadings
            .Where(required => !actual.Any(candidate => candidate.Equals(Normalize(required), StringComparison.OrdinalIgnoreCase)
                || candidate.StartsWith(Normalize(required) + " ", StringComparison.OrdinalIgnoreCase)))
            .Select(required => new ImportWarning
            {
                Id = $"MISSING_REQUIRED_HEADING:{document.Source.RelativeFile}:{required}",
                Severity = WarningSeverity.Error,
                Code = "MISSING_REQUIRED_HEADING",
                Message = $"Required heading '{required}' is missing from '{document.Source.RelativeFile}'.",
                SourceReferences = [document.Source.SourceReference with
                {
                    RelativeFile = document.Source.RelativeFile,
                    Section = required,
                    ExtractionRule = "idea-planning-required-heading"
                }]
            })
            .ToArray();
    }

    public static ImportWarning Diagnostic(string code, PlanningDocument document, PlanningTableRow row, string message) => new()
    {
        Id = $"{code}:{document.Source.RelativeFile}:{row.SourceLine}:{row.RowIndex}",
        Severity = WarningSeverity.Error,
        Code = code,
        Message = message,
        SourceReferences = [row.SourceReference]
    };

    private static bool IsNumericHeader(string header) =>
        header.Contains("EFFORT", StringComparison.Ordinal)
        || header.Contains("DURATION", StringComparison.Ordinal)
        || header.Contains("CAPACITY", StringComparison.Ordinal)
        || header.Contains("RESERVE", StringComparison.Ordinal)
        || header.Contains("WIP", StringComparison.Ordinal)
        || header.EndsWith(" HOURS", StringComparison.Ordinal)
        || header.EndsWith(" MINUTES", StringComparison.Ordinal);

    private static bool IsDateHeader(string header) =>
        header.Contains("START", StringComparison.Ordinal)
        || header.Contains("FINISH", StringComparison.Ordinal)
        || header.EndsWith(" DATE", StringComparison.Ordinal)
        || header == "DATE";

    private static bool IsDate(string value) =>
        DatePattern.Match(value) is { Success: true } match
        && DateOnly.TryParseExact(match.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool IsIsoDate(string value) =>
        DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool TryParseNumeric(string value, bool allowHoursSuffix, out decimal result)
    {
        var normalized = value.Trim();
        if (allowHoursSuffix)
        {
            normalized = Regex.Replace(normalized, @"\s+(?:hours?|h)$", string.Empty, RegexOptions.IgnoreCase);
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryGetKeyValue(PlanningTableRow row, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        if (row.Headers.Count < 2)
        {
            return false;
        }

        var firstHeader = Normalize(row.Headers[0]);
        var secondHeader = Normalize(row.Headers[1]);
        if (firstHeader is not ("FIELD" or "POLICY") || secondHeader != "VALUE")
        {
            return false;
        }

        key = row.Cells[row.Headers[0]].Trim();
        value = row.Cells[row.Headers[1]].Trim();
        return key.Length > 0;
    }
}
