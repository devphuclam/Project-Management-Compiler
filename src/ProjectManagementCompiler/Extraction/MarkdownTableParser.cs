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
        var inFence = false;
        var fenceMarker = '\0';
        var fenceLength = 0;
        var fenceStartLine = 0;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            if (inFence)
            {
                if (IsClosingFence(lines[lineIndex], fenceMarker, fenceLength))
                {
                    inFence = false;
                }

                continue;
            }

            if (TryGetFence(lines[lineIndex], out fenceMarker, out fenceLength))
            {
                inFence = true;
                fenceStartLine = lineIndex + 1;
                continue;
            }

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
            diagnostics.AddRange(PlanningParserSupport.ValidateTableShape(document, tableIndex, 0, lineIndex + 1, headers, headers));
            var rowIndex = 0;
            lineIndex += 2;
            for (; lineIndex < lines.Length && lines[lineIndex].Contains('|'); lineIndex++)
            {
                if (lineIndex + 1 < lines.Length && PlanningParserSupport.IsSeparator(lines[lineIndex + 1]))
                {
                    break;
                }

                if (PlanningParserSupport.IsSeparator(lines[lineIndex]))
                {
                    continue;
                }

                rowIndex++;
                var values = PlanningParserSupport.SplitPipeRow(lines[lineIndex]);
                diagnostics.AddRange(PlanningParserSupport.ValidateTableShape(document, tableIndex, rowIndex, lineIndex + 1, headers, values));
                if (headers.Count != values.Count || headers.GroupBy(PlanningParserSupport.Normalize, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                {
                    continue;
                }

                var row = PlanningParserSupport.CreateRow(
                    document,
                    section,
                    tableIndex,
                    rowIndex,
                    lineIndex + 1,
                    headers,
                    values);
                rows.Add(row);
                diagnostics.AddRange(PlanningParserSupport.Validate(document, row));
            }

            lineIndex--;
        }

        if (inFence)
        {
            diagnostics.Add(new ImportWarning
            {
                Id = $"UNTERMINATED_MARKDOWN_FENCE:{document.Source.RelativeFile}:{fenceStartLine}",
                Severity = WarningSeverity.Error,
                Code = "UNTERMINATED_MARKDOWN_FENCE",
                Message = $"Markdown fence opened at line {fenceStartLine} in '{document.Source.RelativeFile}' is not closed; fenced content was ignored.",
                SourceReferences = [document.Source.SourceReference with
                {
                    RelativeFile = document.Source.RelativeFile,
                    Item = $"line-{fenceStartLine:D4}",
                    ExtractionRule = "idea-planning-markdown-fence"
                }]
            });
        }

        diagnostics.InsertRange(0, PlanningParserSupport.MissingHeadings(document, headings));
        return new PlanningParseResult { Rows = rows, Diagnostics = diagnostics };
    }

    internal static string ContentOutsideFences(string content)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var visibleLines = new List<string>();
        var inFence = false;
        var fenceMarker = '\0';
        var fenceLength = 0;

        foreach (var line in lines)
        {
            if (inFence)
            {
                if (IsClosingFence(line, fenceMarker, fenceLength))
                {
                    inFence = false;
                }

                continue;
            }

            if (TryGetFence(line, out fenceMarker, out fenceLength))
            {
                inFence = true;
                continue;
            }

            visibleLines.Add(line);
        }

        return string.Join('\n', visibleLines);
    }

    private static bool TryGetFence(string line, out char marker, out int length)
    {
        marker = '\0';
        length = 0;
        var leadingSpaces = line.Length - line.TrimStart(' ').Length;
        if (leadingSpaces > 3)
        {
            return false;
        }

        var candidate = line[leadingSpaces..];
        if (candidate.Length == 0 || candidate[0] is not ('`' or '~'))
        {
            return false;
        }

        marker = candidate[0];
        while (length < candidate.Length && candidate[length] == marker)
        {
            length++;
        }

        return length >= 3;
    }

    private static bool IsClosingFence(string line, char marker, int openingLength)
    {
        if (!TryGetFence(line, out var closingMarker, out var closingLength)
            || closingMarker != marker
            || closingLength < openingLength)
        {
            return false;
        }

        var leadingSpaces = line.Length - line.TrimStart(' ').Length;
        var remainder = line[(leadingSpaces + closingLength)..];
        return string.IsNullOrWhiteSpace(remainder);
    }
}
