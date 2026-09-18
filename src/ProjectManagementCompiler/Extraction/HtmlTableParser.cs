using System.Net;
using System.Text.RegularExpressions;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Extraction;

public static class HtmlTableParser
{
    private static readonly Regex HeadingPattern = new(@"<h[1-6][^>]*>(?<text>.*?)</h[1-6]>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex TablePattern = new(@"<table\b[^>]*>(?<body>.*?)</table>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex RowPattern = new(@"<tr\b(?<attributes>[^>]*)>(?<body>.*?)</tr>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex CellPattern = new(@"<(?<kind>th|td)\b[^>]*>(?<text>.*?)</(?<close>th|td)>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex DataAttributePattern = new(@"(?<name>data-[A-Za-z0-9_-]+)\s*=\s*[""'](?<value>.*?)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);

    public static PlanningParseResult Parse(PlanningDocument document)
    {
        if (document.Source.Format != SourceDocumentFormat.Html)
        {
            return new PlanningParseResult
            {
                Diagnostics =
                [new ImportWarning
                {
                    Id = $"UNSUPPORTED_DOCUMENT_FORMAT:{document.Source.RelativeFile}",
                    Severity = WarningSeverity.Error,
                    Code = "UNSUPPORTED_DOCUMENT_FORMAT",
                    Message = $"'{document.Source.RelativeFile}' is not an HTML document.",
                    SourceReferences = [document.Source.SourceReference]
                }]
            };
        }

        var content = document.Source.Content;
        var headings = HeadingPattern.Matches(content)
            .Cast<Match>()
            .Select(match => (Position: match.Index, Text: Text(match.Groups["text"].Value)))
            .ToArray();
        var rows = new List<PlanningTableRow>();
        var diagnostics = new List<ImportWarning>();
        var tableIndex = 0;

        foreach (Match tableMatch in TablePattern.Matches(content))
        {
            tableIndex++;
            var section = headings.LastOrDefault(heading => heading.Position < tableMatch.Index).Text;
            if (string.IsNullOrEmpty(section))
            {
                section = null;
            }

            var tableRows = RowPattern.Matches(tableMatch.Groups["body"].Value).Cast<Match>().ToArray();
            if (tableRows.Length == 0)
            {
                continue;
            }

            var headerPosition = Array.FindIndex(tableRows, row => CellPattern.Matches(row.Groups["body"].Value).Cast<Match>().Any(cell => cell.Groups["kind"].Value.Equals("th", StringComparison.OrdinalIgnoreCase)));
            if (headerPosition < 0)
            {
                headerPosition = 0;
            }

            var headers = Cells(tableRows[headerPosition].Groups["body"].Value);
            var headerShapeDiagnostics = PlanningParserSupport.ValidateTableShape(document, tableIndex, 0, 0, headers, headers);
            diagnostics.AddRange(headerShapeDiagnostics);
            var rowIndex = 0;
            for (var index = headerPosition + 1; index < tableRows.Length; index++)
            {
                var rowMatch = tableRows[index];
                var values = Cells(rowMatch.Groups["body"].Value);
                rowIndex++;
                var sourceLine = content[..(tableMatch.Groups["body"].Index + rowMatch.Index)].Count(character => character == '\n') + 1;
                diagnostics.AddRange(PlanningParserSupport.ValidateTableShape(document, tableIndex, rowIndex, sourceLine, headers, values));
                if (headers.Count != values.Count || headers.GroupBy(PlanningParserSupport.Normalize, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                {
                    continue;
                }

                var attributes = DataAttributePattern.Matches(rowMatch.Groups["attributes"].Value)
                    .Cast<Match>()
                    .ToDictionary(match => match.Groups["name"].Value, match => WebUtility.HtmlDecode(match.Groups["value"].Value), StringComparer.OrdinalIgnoreCase);
                var row = PlanningParserSupport.CreateRow(document, section, tableIndex, rowIndex, sourceLine, headers, values, attributes);
                rows.Add(row);
                diagnostics.AddRange(PlanningParserSupport.Validate(document, row));
            }
        }

        diagnostics.InsertRange(0, PlanningParserSupport.MissingHeadings(document, headings.Select(heading => heading.Text)));
        return new PlanningParseResult { Rows = rows, Diagnostics = diagnostics };
    }

    private static IReadOnlyList<string> Cells(string html) =>
        CellPattern.Matches(html)
            .Cast<Match>()
            .Select(match => Text(match.Groups["text"].Value))
            .ToArray();

    private static string Text(string value) =>
        WebUtility.HtmlDecode(TagPattern.Replace(value, string.Empty)).Trim();
}
