using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Extraction;

public static class MarkdownTableParser
{
    public static PlanningParseResult Parse(PlanningDocument document)
    {
        if (document.Source.Format != SourceDocumentFormat.Markdown)
        {
            return new PlanningParseResult
            {
                Diagnostics =
                [new ImportWarning
                {
                    Id = $"UNSUPPORTED_DOCUMENT_FORMAT:{document.Source.RelativeFile}",
                    Severity = WarningSeverity.Error,
                    Code = "UNSUPPORTED_DOCUMENT_FORMAT",
                    Message = $"'{document.Source.RelativeFile}' is not a Markdown document.",
                    SourceReferences = [document.Source.SourceReference]
                }]
            };
        }

        var lines = document.Source.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var rows = new List<PlanningTableRow>();
        var diagnostics = new List<ImportWarning>();
        var headings = new List<string>();
        string? section = null;
        var tableIndex = 0;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var heading = PlanningParserSupport.MarkdownHeading(lines[lineIndex]);
            if (heading is not null)
            {
                section = heading;
                headings.Add(heading);
                continue;
            }

            if (!lines[lineIndex].Contains('|') || lineIndex + 1 >= lines.Length || !PlanningParserSupport.IsSeparator(lines[lineIndex + 1]))
            {
                continue;
            }

            tableIndex++;
            var headers = PlanningParserSupport.SplitPipeRow(lines[lineIndex]);
            var rowIndex = 0;
            lineIndex += 2;
            for (; lineIndex < lines.Length && lines[lineIndex].Contains('|'); lineIndex++)
            {
                if (PlanningParserSupport.IsSeparator(lines[lineIndex]))
                {
                    continue;
                }

                rowIndex++;
                var row = PlanningParserSupport.CreateRow(
                    document,
                    section,
                    tableIndex,
                    rowIndex,
                    lineIndex + 1,
                    headers,
                    PlanningParserSupport.SplitPipeRow(lines[lineIndex]));
                rows.Add(row);
                diagnostics.AddRange(PlanningParserSupport.Validate(document, row));
            }

            lineIndex--;
        }

        diagnostics.InsertRange(0, PlanningParserSupport.MissingHeadings(document, headings));
        return new PlanningParseResult { Rows = rows, Diagnostics = diagnostics };
    }
}
