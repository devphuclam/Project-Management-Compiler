using System.Net;
using System.Text.RegularExpressions;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Extraction;

public static class HtmlTableParser
{
    private static readonly Regex HeadingPattern = new(@"<h[1-6][^>]*>(?<text>.*?)</h[1-6]>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex TablePattern = new(@"<table\b[^>]*>(?<body>(?:(?!<table\b).)*?)</table>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex TableTagPattern = new(@"</?table\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex RowTagPattern = new(@"</?tr\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex RowPattern = new(@"<tr\b(?<attributes>[^>]*)>(?<body>.*?)</tr>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex CellPattern = new(@"<(?<kind>th|td)\b[^>]*>(?<text>.*?)</\k<kind>>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex AnyCellPattern = new(@"<(?<kind>th|td)\b[^>]*>(?<text>.*?)</(?<close>th|td)>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex CellTagPattern = new(@"<(?<closing>/)?(?<kind>th|td)\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex DataAttributePattern = new(@"(?<name>data-[A-Za-z0-9_-]+)\s*=\s*(?:(?<quote>[""'])(?<quotedValue>.*?)\k<quote>|(?<unquotedValue>[^\s""'`=<>]+))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
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

        diagnostics.AddRange(UnclosedTableDiagnostics(document, content));

        foreach (Match tableMatch in TablePattern.Matches(content))
        {
            tableIndex++;
            var section = headings.LastOrDefault(heading => heading.Position < tableMatch.Index).Text;
            if (string.IsNullOrEmpty(section))
            {
                section = null;
            }

            diagnostics.AddRange(UnclosedRowDiagnostics(document, tableIndex, tableMatch.Groups["body"], content));
            var tableRows = RowPattern.Matches(tableMatch.Groups["body"].Value).Cast<Match>().ToArray();
            if (tableRows.Length == 0)
            {
                continue;
            }

            var unbalancedCellRows = new HashSet<int>();
            for (var index = 0; index < tableRows.Length; index++)
            {
                var rowMatch = tableRows[index];
                if (!HasBalancedCellTags(rowMatch.Groups["body"].Value))
                {
                    unbalancedCellRows.Add(index);
                    diagnostics.Add(UnbalancedCellDiagnostic(
                        document,
                        tableIndex,
                        index));
                }
            }

            var headerPosition = Array.FindIndex(tableRows, row => CellPattern.Matches(row.Groups["body"].Value).Cast<Match>().Any(cell => cell.Groups["kind"].Value.Equals("th", StringComparison.OrdinalIgnoreCase)));
            if (headerPosition < 0)
            {
                diagnostics.Add(new ImportWarning
                {
                    Id = $"MISSING_TABLE_HEADER:{document.Source.RelativeFile}:{tableIndex}",
                    Severity = WarningSeverity.Error,
                    Code = "MISSING_TABLE_HEADER",
                    Message = $"HTML table {tableIndex} in '{document.Source.RelativeFile}' has no <th> header row; data rows were not guessed.",
                    SourceReferences = [document.Source.SourceReference with
                    {
                        RelativeFile = document.Source.RelativeFile,
                        Table = $"table-{tableIndex:D2}",
                        ExtractionRule = "idea-planning-html-table-header"
                    }]
                });
                continue;
            }

            if (unbalancedCellRows.Contains(headerPosition))
            {
                continue;
            }

            var headers = Cells(tableRows[headerPosition].Groups["body"].Value);
            var headerShapeDiagnostics = PlanningParserSupport.ValidateTableShape(document, tableIndex, 0, 0, headers, headers);
            diagnostics.AddRange(headerShapeDiagnostics);
            var rowIndex = 0;
            for (var index = headerPosition + 1; index < tableRows.Length; index++)
            {
                var rowMatch = tableRows[index];
                rowIndex++;
                var sourceLine = content[..(tableMatch.Groups["body"].Index + rowMatch.Index)].Count(character => character == '\n') + 1;
                var cellShape = AnyCellPattern.Matches(rowMatch.Groups["body"].Value)
                    .Cast<Match>()
                    .Where(match => !match.Groups["kind"].Value.Equals(match.Groups["close"].Value, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (cellShape.Length > 0)
                {
                    diagnostics.Add(new ImportWarning
                    {
                        Id = $"MISMATCHED_HTML_CELL_TAG:{document.Source.RelativeFile}:{tableIndex}:{rowIndex}",
                        Severity = WarningSeverity.Error,
                        Code = "MISMATCHED_HTML_CELL_TAG",
                        Message = $"HTML table {tableIndex} row {rowIndex} in '{document.Source.RelativeFile}' contains mismatched opening and closing cell tags; the row was skipped.",
                        SourceReferences = [document.Source.SourceReference with
                        {
                            RelativeFile = document.Source.RelativeFile,
                            Table = $"table-{tableIndex:D2}",
                            Item = $"row-{rowIndex:D3}",
                            ExtractionRule = "idea-planning-html-cell-shape"
                        }]
                    });
                    continue;
                }

                if (unbalancedCellRows.Contains(index))
                {
                    continue;
                }

                var values = Cells(rowMatch.Groups["body"].Value);
                diagnostics.AddRange(PlanningParserSupport.ValidateTableShape(document, tableIndex, rowIndex, sourceLine, headers, values));
                if (headers.Count != values.Count || headers.GroupBy(PlanningParserSupport.Normalize, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                {
                    continue;
                }

                var dataAttributes = DataAttributePattern.Matches(rowMatch.Groups["attributes"].Value)
                    .Cast<Match>()
                    .ToArray();
                var duplicateAttributes = dataAttributes
                    .GroupBy(match => match.Groups["name"].Value, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToArray();
                if (duplicateAttributes.Length > 0)
                {
                    diagnostics.Add(new ImportWarning
                    {
                        Id = $"DUPLICATE_DATA_ATTRIBUTE:{document.Source.RelativeFile}:{tableIndex}:{rowIndex}",
                        Severity = WarningSeverity.Error,
                        Code = "DUPLICATE_DATA_ATTRIBUTE",
                        Message = $"HTML table {tableIndex} row {rowIndex} in '{document.Source.RelativeFile}' contains duplicate data-* attributes: {string.Join(", ", duplicateAttributes)}; the row was skipped.",
                        SourceReferences = [document.Source.SourceReference with
                        {
                            RelativeFile = document.Source.RelativeFile,
                            Table = $"table-{tableIndex:D2}",
                            Item = $"row-{rowIndex:D3}",
                            ExtractionRule = "idea-planning-html-data-attributes"
                        }]
                    });
                    continue;
                }

                var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var attribute in dataAttributes)
                {
                    var value = attribute.Groups["quotedValue"].Success
                        ? attribute.Groups["quotedValue"].Value
                        : attribute.Groups["unquotedValue"].Value;
                    attributes.Add(attribute.Groups["name"].Value, WebUtility.HtmlDecode(value));
                }
                var row = PlanningParserSupport.CreateRow(document, section, tableIndex, rowIndex, sourceLine, headers, values, attributes);
                rows.Add(row);
                diagnostics.AddRange(PlanningParserSupport.Validate(document, row));
            }
        }

        diagnostics.InsertRange(0, PlanningParserSupport.MissingHeadings(document, headings.Select(heading => heading.Text)));
        return new PlanningParseResult { Rows = rows, Diagnostics = diagnostics };
    }

    private static ImportWarning UnbalancedCellDiagnostic(
        PlanningDocument document,
        int tableIndex,
        int rowIndex) => new()
        {
            Id = $"UNBALANCED_HTML_CELL_TAG:{document.Source.RelativeFile}:{tableIndex}:{rowIndex}",
            Severity = WarningSeverity.Error,
            Code = "UNBALANCED_HTML_CELL_TAG",
            Message = $"HTML table {tableIndex} row {rowIndex} in '{document.Source.RelativeFile}' contains unbalanced cell tags; the row was skipped.",
            SourceReferences = [document.Source.SourceReference with
            {
                RelativeFile = document.Source.RelativeFile,
                Table = $"table-{tableIndex:D2}",
                Item = $"row-{rowIndex:D3}",
                ExtractionRule = "idea-planning-html-cell-shape"
            }]
        };

    private static bool HasBalancedCellTags(string html)
    {
        var openCells = new Stack<string>();
        foreach (Match tag in CellTagPattern.Matches(html))
        {
            var kind = tag.Groups["kind"].Value;
            if (tag.Groups["closing"].Success)
            {
                if (openCells.Count == 0 || !openCells.Peek().Equals(kind, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                openCells.Pop();
                continue;
            }

            openCells.Push(kind);
        }

        return openCells.Count == 0;
    }

    private static IReadOnlyList<ImportWarning> UnclosedRowDiagnostics(
        PlanningDocument document,
        int tableIndex,
        Group tableBody,
        string content)
    {
        var openRows = new Stack<(int RowIndex, int SourceLine)>();
        var rowIndex = 0;
        foreach (Match tag in RowTagPattern.Matches(tableBody.Value))
        {
            if (tag.Value.StartsWith("</", StringComparison.Ordinal))
            {
                if (openRows.Count > 0)
                {
                    openRows.Pop();
                }

                continue;
            }

            rowIndex++;
            openRows.Push((rowIndex, content[..(tableBody.Index + tag.Index)].Count(character => character == '\n') + 1));
        }

        return openRows
            .Reverse()
            .Select(unclosed => new ImportWarning
            {
                Id = $"UNCLOSED_HTML_ROW:{document.Source.RelativeFile}:{tableIndex}:{unclosed.RowIndex}",
                Severity = WarningSeverity.Error,
                Code = "UNCLOSED_HTML_ROW",
                Message = $"HTML table {tableIndex} row {unclosed.RowIndex} in '{document.Source.RelativeFile}' is not closed; the row was not parsed.",
                SourceReferences = [document.Source.SourceReference with
                {
                    RelativeFile = document.Source.RelativeFile,
                    Table = $"table-{tableIndex:D2}",
                    Item = $"row-{unclosed.RowIndex:D3}",
                    ExtractionRule = "idea-planning-html-row-shape"
                }]
            })
            .ToArray();
    }

    private static IReadOnlyList<ImportWarning> UnclosedTableDiagnostics(PlanningDocument document, string content)
    {
        var openTables = new Stack<(int TableIndex, int SourceLine)>();
        var tableIndex = 0;
        foreach (Match tag in TableTagPattern.Matches(content))
        {
            if (tag.Value.StartsWith("</", StringComparison.Ordinal))
            {
                if (openTables.Count > 0)
                {
                    openTables.Pop();
                }

                continue;
            }

            tableIndex++;
            openTables.Push((tableIndex, content[..tag.Index].Count(character => character == '\n') + 1));
        }

        return openTables
            .Reverse()
            .Select(unclosed => new ImportWarning
            {
                Id = $"UNCLOSED_HTML_TABLE:{document.Source.RelativeFile}:{unclosed.TableIndex}",
                Severity = WarningSeverity.Error,
                Code = "UNCLOSED_HTML_TABLE",
                Message = $"HTML table {unclosed.TableIndex} in '{document.Source.RelativeFile}' is not closed; its rows were not parsed.",
                SourceReferences = [document.Source.SourceReference with
                {
                    RelativeFile = document.Source.RelativeFile,
                    Table = $"table-{unclosed.TableIndex:D2}",
                    Item = $"line-{unclosed.SourceLine:D4}",
                    ExtractionRule = "idea-planning-html-table-shape"
                }]
            })
            .ToArray();
    }

    private static IReadOnlyList<string> Cells(string html) =>
        CellPattern.Matches(html)
            .Cast<Match>()
            .Select(match => Text(match.Groups["text"].Value))
            .ToArray();

    private static string Text(string value) =>
        WebUtility.HtmlDecode(TagPattern.Replace(value, string.Empty)).Trim();
}
