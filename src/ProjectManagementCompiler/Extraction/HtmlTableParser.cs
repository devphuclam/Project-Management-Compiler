using System.Net;
using System.Text;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Extraction;

public static class HtmlTableParser
{
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
        var headings = ExtractHeadings(content);
        var diagnostics = new List<ImportWarning>();
        var tables = new List<HtmlTableState>();
        var stack = new Stack<HtmlOpenTag>();
        var tableIndex = 0;

        foreach (var token in Tokenize(content))
        {
            if (token.Kind == HtmlTokenKind.Text || token.Kind == HtmlTokenKind.GenericTag)
            {
                if (stack.TryPeek(out var open) && open.Kind == HtmlTagKind.Cell)
                {
                    open.Cell!.Content.Append(token.RawText);
                }

                continue;
            }

            var tag = token.Tag!;
            if (!tag.IsClosing)
            {
                switch (tag.Kind)
                {
                    case HtmlTagKind.Table:
                        StartTable(document, content, headings, tag, stack, tables, diagnostics, ref tableIndex);
                        break;
                    case HtmlTagKind.Row:
                        StartRow(document, content, tag, stack, diagnostics);
                        break;
                    case HtmlTagKind.Cell:
                        StartCell(document, tag, stack, diagnostics);
                        break;
                }

                continue;
            }

            switch (tag.Kind)
            {
                case HtmlTagKind.Table:
                    EndTable(document, tag, stack, diagnostics);
                    break;
                case HtmlTagKind.Row:
                    EndRow(document, tag, stack, diagnostics);
                    break;
                case HtmlTagKind.Cell:
                    EndCell(document, tag, stack, diagnostics);
                    break;
            }
        }

        while (stack.Count > 0)
        {
            var open = stack.Peek();
            if (open.Kind == HtmlTagKind.Row)
            {
                DiscardRow(document, open.Row!, stack, diagnostics, unclosed: true);
                continue;
            }

            if (open.Kind == HtmlTagKind.Cell)
            {
                var row = CurrentRow(stack);
                if (row is not null)
                {
                    row.Invalid = true;
                }

                stack.Pop();
                continue;
            }

            stack.Pop();
        }

        foreach (var table in tables.Where(table => !table.Closed))
        {
            diagnostics.Add(UnclosedTableDiagnostic(document, table));
        }

        var rows = new List<PlanningTableRow>();
        foreach (var table in tables.Where(table => table.Closed && !table.Invalid))
        {
            ParseTable(document, table, diagnostics, rows);
        }

        diagnostics.InsertRange(0, PlanningParserSupport.MissingHeadings(document, headings.Select(heading => heading.Text)));
        return new PlanningParseResult { Rows = rows, Diagnostics = diagnostics };
    }

    private static void StartTable(
        PlanningDocument document,
        string content,
        IReadOnlyList<(int Position, string Text)> headings,
        HtmlTag tag,
        Stack<HtmlOpenTag> stack,
        ICollection<HtmlTableState> tables,
        ICollection<ImportWarning> diagnostics,
        ref int tableIndex)
    {
        tableIndex++;
        var section = headings.LastOrDefault(heading => heading.Position < tag.Position).Text;
        if (string.IsNullOrEmpty(section))
        {
            section = null;
        }

        var table = new HtmlTableState
        {
            Index = tableIndex,
            Section = section,
            SourceLine = Line(content, tag.Position)
        };
        if (stack.Count > 0)
        {
            table.Invalid = true;
            var outerTable = CurrentTable(stack);
            if (outerTable is not null)
            {
                outerTable.Invalid = true;
            }

            var row = CurrentRow(stack);
            if (row is not null)
            {
                row.Invalid = true;
            }

            diagnostics.Add(MisorderedDiagnostic(document, tag, table.Index, row?.Ordinal, "A table tag was nested inside another relevant HTML element."));
        }

        tables.Add(table);
        stack.Push(HtmlOpenTag.ForTable(table));
    }

    private static void StartRow(
        PlanningDocument document,
        string content,
        HtmlTag tag,
        Stack<HtmlOpenTag> stack,
        ICollection<ImportWarning> diagnostics)
    {
        var table = CurrentTable(stack);
        if (table is null)
        {
            diagnostics.Add(MisorderedDiagnostic(document, tag, 0, null, "A row tag appeared outside a table."));
            return;
        }

        var existingRow = CurrentRow(stack);
        if (existingRow is not null)
        {
            diagnostics.Add(MisorderedDiagnostic(document, tag, table.Index, existingRow.Ordinal, "A row tag was nested before the previous row was closed."));
            DiscardRow(document, existingRow, stack, diagnostics, unclosed: true);
        }

        if (!stack.TryPeek(out var parent) || parent.Kind != HtmlTagKind.Table)
        {
            diagnostics.Add(MisorderedDiagnostic(document, tag, table.Index, null, "A row tag must be a direct child of a table."));
            return;
        }

        var row = new HtmlRowState
        {
            Table = table,
            Ordinal = table.Rows.Count + 1,
            SourceLine = Line(content, tag.Position),
            Attributes = tag.Attributes,
            AttributeError = tag.AttributeError
        };
        if (tag.AttributeError)
        {
            row.Invalid = true;
            diagnostics.Add(RowDiagnostic(document, "MALFORMED_HTML_ATTRIBUTE", table.Index, row.Ordinal, row.SourceLine, "The HTML row contains a malformed attribute token stream; the row was skipped."));
        }

        foreach (var duplicate in tag.DuplicateDataAttributes)
        {
            row.Invalid = true;
            diagnostics.Add(RowDiagnostic(document, "DUPLICATE_DATA_ATTRIBUTE", table.Index, row.Ordinal, row.SourceLine, $"HTML table {table.Index} row {row.Ordinal} contains duplicate data-* attribute '{duplicate}'; the row was skipped."));
        }

        table.Rows.Add(row);
        stack.Push(HtmlOpenTag.ForRow(row));
    }

    private static void StartCell(
        PlanningDocument document,
        HtmlTag tag,
        Stack<HtmlOpenTag> stack,
        ICollection<ImportWarning> diagnostics)
    {
        var table = CurrentTable(stack);
        var row = CurrentRow(stack);
        if (table is null || row is null)
        {
            diagnostics.Add(MisorderedDiagnostic(document, tag, table?.Index ?? 0, row?.Ordinal, "A cell tag appeared outside a row inside a table."));
            return;
        }

        if (!stack.TryPeek(out var parent) || parent.Kind != HtmlTagKind.Row)
        {
            row.Invalid = true;
            diagnostics.Add(MisorderedDiagnostic(document, tag, table.Index, row.Ordinal, "A cell tag was nested inside another cell or was not directly inside a row."));
        }

        stack.Push(HtmlOpenTag.ForCell(new HtmlCellState
        {
            Kind = tag.CellKind,
            Position = tag.Position
        }));
    }

    private static void EndTable(
        PlanningDocument document,
        HtmlTag tag,
        Stack<HtmlOpenTag> stack,
        ICollection<ImportWarning> diagnostics)
    {
        var table = CurrentTable(stack);
        if (table is null)
        {
            diagnostics.Add(UnmatchedDiagnostic(document, tag));
            return;
        }

        var row = CurrentRow(stack);
        if (row is not null && row.Table == table)
        {
            diagnostics.Add(MisorderedDiagnostic(document, tag, table.Index, row.Ordinal, "A table closing tag appeared before the current row was closed."));
            DiscardRow(document, row, stack, diagnostics, unclosed: true);
        }

        if (stack.TryPeek(out var open) && open.Kind == HtmlTagKind.Table && open.Table == table)
        {
            stack.Pop();
            table.Closed = true;
            return;
        }

        table.Invalid = true;
        diagnostics.Add(MisorderedDiagnostic(document, tag, table.Index, null, "A table closing tag was not ordered after its rows and cells."));
        RemoveTable(stack, table);
    }

    private static void EndRow(
        PlanningDocument document,
        HtmlTag tag,
        Stack<HtmlOpenTag> stack,
        ICollection<ImportWarning> diagnostics)
    {
        var row = CurrentRow(stack);
        if (row is null)
        {
            diagnostics.Add(UnmatchedDiagnostic(document, tag));
            return;
        }

        if (!stack.TryPeek(out var open) || open.Kind != HtmlTagKind.Row || open.Row != row)
        {
            row.Invalid = true;
            diagnostics.Add(RowDiagnostic(document, "UNBALANCED_HTML_CELL_TAG", row.Table.Index, row.Ordinal, row.SourceLine, $"HTML table {row.Table.Index} row {row.Ordinal} contains an unclosed or misordered cell; the row was skipped."));
            diagnostics.Add(MisorderedDiagnostic(document, tag, row.Table.Index, row.Ordinal, "A row closing tag appeared before its cell tags were closed."));
            DiscardRow(document, row, stack, diagnostics, unclosed: false);
            return;
        }

        stack.Pop();
        row.Closed = true;
    }

    private static void EndCell(
        PlanningDocument document,
        HtmlTag tag,
        Stack<HtmlOpenTag> stack,
        ICollection<ImportWarning> diagnostics)
    {
        var row = CurrentRow(stack);
        var table = CurrentTable(stack);
        if (row is null || table is null)
        {
            diagnostics.Add(UnmatchedDiagnostic(document, tag));
            return;
        }

        if (!stack.TryPeek(out var open) || open.Kind != HtmlTagKind.Cell)
        {
            row.Invalid = true;
            diagnostics.Add(RowDiagnostic(document, "UNBALANCED_HTML_CELL_TAG", table.Index, row.Ordinal, row.SourceLine, $"HTML table {table.Index} row {row.Ordinal} contains an unmatched cell closing tag; the row was skipped."));
            return;
        }

        if (open.Cell!.Kind != tag.CellKind)
        {
            row.Invalid = true;
            diagnostics.Add(RowDiagnostic(document, "MISMATCHED_HTML_CELL_TAG", table.Index, row.Ordinal, row.SourceLine, $"HTML table {table.Index} row {row.Ordinal} contains mismatched opening and closing cell tags; the row was skipped."));
            return;
        }

        stack.Pop();
        open.Cell.Closed = true;
        row.Cells.Add(open.Cell);
    }

    private static void DiscardRow(
        PlanningDocument document,
        HtmlRowState row,
        Stack<HtmlOpenTag> stack,
        ICollection<ImportWarning> diagnostics,
        bool unclosed)
    {
        if (unclosed && !row.UnclosedReported)
        {
            row.UnclosedReported = true;
            diagnostics.Add(RowDiagnostic(document, "UNCLOSED_HTML_ROW", row.Table.Index, row.Ordinal, row.SourceLine, $"HTML table {row.Table.Index} row {row.Ordinal} is not closed; the row was not parsed."));
        }

        row.Invalid = true;
        while (stack.Count > 0 && (stack.Peek().Kind != HtmlTagKind.Row || stack.Peek().Row != row))
        {
            stack.Pop();
        }

        if (stack.Count > 0)
        {
            stack.Pop();
        }
    }

    private static void RemoveTable(Stack<HtmlOpenTag> stack, HtmlTableState table)
    {
        var remaining = stack.Where(open => open.Table != table).Reverse().ToArray();
        stack.Clear();
        foreach (var open in remaining)
        {
            stack.Push(open);
        }
    }

    private static void ParseTable(
        PlanningDocument document,
        HtmlTableState table,
        ICollection<ImportWarning> diagnostics,
        ICollection<PlanningTableRow> rows)
    {
        var headerPosition = table.Rows.FindIndex(row => !row.Invalid && row.Cells.Any(cell => cell.Kind == HtmlTagKind.HeaderCell));
        if (headerPosition < 0)
        {
            if (table.Rows.Count > 0)
            {
                diagnostics.Add(new ImportWarning
                {
                    Id = $"MISSING_TABLE_HEADER:{document.Source.RelativeFile}:{table.Index}",
                    Severity = WarningSeverity.Error,
                    Code = "MISSING_TABLE_HEADER",
                    Message = $"HTML table {table.Index} in '{document.Source.RelativeFile}' has no <th> header row; data rows were not guessed.",
                    SourceReferences = [document.Source.SourceReference with
                    {
                        RelativeFile = document.Source.RelativeFile,
                        Table = $"table-{table.Index:D2}",
                        ExtractionRule = "idea-planning-html-table-header"
                    }]
                });
            }

            return;
        }

        var header = table.Rows[headerPosition];
        var headers = header.Cells.Select(cell => Text(cell.Content.ToString())).ToArray();
        foreach (var diagnostic in PlanningParserSupport.ValidateTableShape(document, table.Index, 0, header.SourceLine, headers, headers))
        {
            diagnostics.Add(diagnostic);
        }
        for (var index = headerPosition + 1; index < table.Rows.Count; index++)
        {
            var row = table.Rows[index];
            var rowIndex = row.Ordinal - header.Ordinal;
            if (row.Invalid || !row.Closed)
            {
                continue;
            }

            var values = row.Cells.Select(cell => Text(cell.Content.ToString())).ToArray();
            foreach (var diagnostic in PlanningParserSupport.ValidateTableShape(document, table.Index, rowIndex, row.SourceLine, headers, values))
            {
                diagnostics.Add(diagnostic);
            }

            if (headers.Length != values.Length || headers.GroupBy(PlanningParserSupport.Normalize, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            {
                continue;
            }

            var attributes = row.Attributes
                .Where(pair => pair.Key.StartsWith("data-", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => WebUtility.HtmlDecode(pair.Value), StringComparer.OrdinalIgnoreCase);
            var planningRow = PlanningParserSupport.CreateRow(document, table.Section, table.Index, rowIndex, row.SourceLine, headers, values, attributes);
            rows.Add(planningRow);
            foreach (var diagnostic in PlanningParserSupport.Validate(document, planningRow))
            {
                diagnostics.Add(diagnostic);
            }
        }
    }

    private static HtmlTableState? CurrentTable(IEnumerable<HtmlOpenTag> stack) =>
        stack.FirstOrDefault(open => open.Kind == HtmlTagKind.Table)?.Table;

    private static HtmlRowState? CurrentRow(IEnumerable<HtmlOpenTag> stack) =>
        stack.FirstOrDefault(open => open.Kind == HtmlTagKind.Row)?.Row;

    private static ImportWarning RowDiagnostic(PlanningDocument document, string code, int tableIndex, int rowIndex, int sourceLine, string message) => new()
    {
        Id = $"{code}:{document.Source.RelativeFile}:{tableIndex}:{rowIndex}:{sourceLine}",
        Severity = WarningSeverity.Error,
        Code = code,
        Message = message,
        SourceReferences = [document.Source.SourceReference with
        {
            RelativeFile = document.Source.RelativeFile,
            Table = $"table-{tableIndex:D2}",
            Item = $"row-{rowIndex:D3}",
            ExtractionRule = "idea-planning-html-row-shape"
        }]
    };

    private static ImportWarning MisorderedDiagnostic(PlanningDocument document, HtmlTag tag, int tableIndex, int? rowIndex, string message) => new()
    {
        Id = $"MISORDERED_HTML_TAG:{document.Source.RelativeFile}:{tag.Position}:{tag.Name}",
        Severity = WarningSeverity.Error,
        Code = "MISORDERED_HTML_TAG",
        Message = message,
        SourceReferences = [document.Source.SourceReference with
        {
            RelativeFile = document.Source.RelativeFile,
            Table = $"table-{tableIndex:D2}",
            Item = rowIndex is null ? null : $"row-{rowIndex.Value:D3}",
            ExtractionRule = "idea-planning-html-structure"
        }]
    };

    private static ImportWarning UnmatchedDiagnostic(PlanningDocument document, HtmlTag tag) => new()
    {
        Id = $"UNMATCHED_HTML_CLOSING_TAG:{document.Source.RelativeFile}:{Line(document.Source.Content, tag.Position)}:{tag.Name}",
        Severity = WarningSeverity.Error,
        Code = "UNMATCHED_HTML_CLOSING_TAG",
        Message = $"HTML document '{document.Source.RelativeFile}' contains unmatched closing tag '{tag.Raw}'.",
        AffectedIds = [tag.Name],
        SourceReferences = [document.Source.SourceReference with
        {
            RelativeFile = document.Source.RelativeFile,
            Item = $"line-{Line(document.Source.Content, tag.Position):D4}",
            ExtractionRule = "idea-planning-html-structure"
        }]
    };

    private static ImportWarning UnclosedTableDiagnostic(PlanningDocument document, HtmlTableState table) => new()
    {
        Id = $"UNCLOSED_HTML_TABLE:{document.Source.RelativeFile}:{table.Index}",
        Severity = WarningSeverity.Error,
        Code = "UNCLOSED_HTML_TABLE",
        Message = $"HTML table {table.Index} in '{document.Source.RelativeFile}' is not closed; its rows were not parsed.",
        SourceReferences = [document.Source.SourceReference with
        {
            RelativeFile = document.Source.RelativeFile,
            Table = $"table-{table.Index:D2}",
            Item = $"line-{table.SourceLine:D4}",
            ExtractionRule = "idea-planning-html-table-shape"
        }]
    };

    private static int Line(string content, int position) => content[..position].Count(character => character == '\n') + 1;

    private static IReadOnlyList<(int Position, string Text)> ExtractHeadings(string content)
    {
        var headings = new List<(int Position, string Text)>();
        var activeLevel = 0;
        var activePosition = -1;
        var activeText = new StringBuilder();

        foreach (var token in Tokenize(content))
        {
            if (token.Kind == HtmlTokenKind.Text)
            {
                if (activeLevel != 0)
                {
                    activeText.Append(token.RawText);
                }

                continue;
            }

            if (token.Kind == HtmlTokenKind.GenericTag && TryParseHeadingTag(token.RawText, out var level, out var isClosing))
            {
                if (!isClosing)
                {
                    activeLevel = level;
                    activePosition = token.Position;
                    activeText.Clear();
                }
                else if (activeLevel == level)
                {
                    headings.Add((activePosition, Text(activeText.ToString())));
                    activeLevel = 0;
                    activePosition = -1;
                    activeText.Clear();
                }

                continue;
            }

            if (activeLevel != 0)
            {
                activeText.Append(token.RawText);
            }
        }

        return headings;
    }

    private static bool TryParseHeadingTag(string raw, out int level, out bool isClosing)
    {
        level = 0;
        isClosing = false;
        var index = 1;
        if (index < raw.Length && raw[index] == '/')
        {
            isClosing = true;
            index++;
        }

        while (index < raw.Length && char.IsWhiteSpace(raw[index]))
        {
            index++;
        }

        if (index + 1 >= raw.Length || (raw[index] is not ('h' or 'H')) || raw[index + 1] is < '1' or > '6')
        {
            return false;
        }

        level = raw[index + 1] - '0';
        return index + 2 >= raw.Length || char.IsWhiteSpace(raw[index + 2]) || raw[index + 2] is '/' or '>';
    }

    private static IEnumerable<HtmlToken> Tokenize(string content)
    {
        var position = 0;
        while (position < content.Length)
        {
            var open = content.IndexOf('<', position);
            if (open < 0)
            {
                if (position < content.Length)
                {
                    yield return HtmlToken.Text(content[position..], position);
                }

                yield break;
            }

            if (open > position)
            {
                yield return HtmlToken.Text(content[position..open], position);
            }

            if (content.AsSpan(open).StartsWith("<!--", StringComparison.Ordinal))
            {
                var commentEnd = content.IndexOf("-->", open + 4, StringComparison.Ordinal);
                position = commentEnd < 0 ? content.Length : commentEnd + 3;
                continue;
            }

            var close = FindTagEnd(content, open + 1);
            if (close < 0)
            {
                yield return HtmlToken.Text(content[open..], open);
                yield break;
            }

            var raw = content[open..(close + 1)];
            if (TryParseRelevantTag(raw, open, out var tag))
            {
                yield return HtmlToken.Relevant(tag);
            }
            else
            {
                yield return HtmlToken.Generic(raw, open);
            }

            position = close + 1;
        }
    }

    private static int FindTagEnd(string content, int position)
    {
        char quote = '\0';
        for (var index = position; index < content.Length; index++)
        {
            var character = content[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (character == '>')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryParseRelevantTag(string raw, int position, out HtmlTag tag)
    {
        tag = null!;
        var index = 1;
        var isClosing = false;
        if (index < raw.Length && raw[index] == '/')
        {
            isClosing = true;
            index++;
        }

        while (index < raw.Length && char.IsWhiteSpace(raw[index]))
        {
            index++;
        }

        var nameStart = index;
        while (index < raw.Length && (char.IsLetterOrDigit(raw[index]) || raw[index] is ':' or '-'))
        {
            index++;
        }

        if (nameStart == index)
        {
            return false;
        }

        var name = raw[nameStart..index];
        var kind = name.ToLowerInvariant() switch
        {
            "table" => HtmlTagKind.Table,
            "tr" => HtmlTagKind.Row,
            "th" => HtmlTagKind.HeaderCell,
            "td" => HtmlTagKind.DataCell,
            _ => (HtmlTagKind?)null
        };
        if (kind is null)
        {
            return false;
        }

        var attributeEnd = raw.Length - 1;
        while (attributeEnd > index && char.IsWhiteSpace(raw[attributeEnd - 1]))
        {
            attributeEnd--;
        }

        var selfClosing = attributeEnd > index && raw[attributeEnd - 1] == '/';
        if (selfClosing)
        {
            attributeEnd--;
        }

        var attributes = ParseAttributes(raw[index..attributeEnd], out var attributeError, out var duplicates);
        tag = new HtmlTag
        {
            Raw = raw,
            Name = name,
            Kind = kind.Value is HtmlTagKind.Table or HtmlTagKind.Row ? kind.Value : HtmlTagKind.Cell,
            CellKind = kind.Value,
            IsClosing = isClosing,
            Position = position,
            Attributes = attributes,
            AttributeError = attributeError || selfClosing,
            DuplicateDataAttributes = duplicates
        };
        return true;
    }

    private static IReadOnlyDictionary<string, string> ParseAttributes(string value, out bool malformed, out IReadOnlyList<string> duplicates)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var duplicateNames = new List<string>();
        malformed = false;
        var index = 0;
        while (index < value.Length)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            if (index >= value.Length)
            {
                break;
            }

            var nameStart = index;
            if (!IsAttributeNameStart(value[index]))
            {
                malformed = true;
                break;
            }

            index++;
            while (index < value.Length && IsAttributeNameCharacter(value[index]))
            {
                index++;
            }

            var name = value[nameStart..index];
            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            if (index >= value.Length || value[index] != '=')
            {
                malformed = true;
                continue;
            }

            index++;
            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            string attributeValue;
            if (index < value.Length && value[index] is '\'' or '"')
            {
                var quote = value[index++];
                var valueStart = index;
                while (index < value.Length && value[index] != quote)
                {
                    index++;
                }

                if (index >= value.Length)
                {
                    malformed = true;
                    break;
                }

                attributeValue = value[valueStart..index];
                index++;
            }
            else
            {
                var valueStart = index;
                while (index < value.Length && !char.IsWhiteSpace(value[index]))
                {
                    if (value[index] is '"' or '\'' or '`' or '=' or '<' or '>')
                    {
                        malformed = true;
                    }

                    index++;
                }

                if (valueStart == index)
                {
                    malformed = true;
                    continue;
                }

                attributeValue = value[valueStart..index];
            }

            if (name.StartsWith("data-", StringComparison.OrdinalIgnoreCase) && parsed.ContainsKey(name))
            {
                duplicateNames.Add(name);
            }

            parsed[name] = attributeValue;
        }

        duplicates = duplicateNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return parsed;
    }

    private static bool IsAttributeNameStart(char character) => char.IsLetter(character) || character is ':' or '_';

    private static bool IsAttributeNameCharacter(char character) => char.IsLetterOrDigit(character) || character is ':' or '_' or '-' or '.';

    private static string Text(string value)
    {
        var text = new StringBuilder();
        foreach (var token in Tokenize(value))
        {
            if (token.Kind == HtmlTokenKind.Text)
            {
                text.Append(token.RawText);
            }
        }

        return WebUtility.HtmlDecode(text.ToString()).Trim();
    }

    private enum HtmlTokenKind
    {
        Text,
        GenericTag,
        RelevantTag
    }

    private enum HtmlTagKind
    {
        Table,
        Row,
        Cell,
        HeaderCell,
        DataCell
    }

    private sealed record HtmlToken(HtmlTokenKind Kind, string RawText, HtmlTag? Tag, int Position)
    {
        public static HtmlToken Text(string text, int position) => new(HtmlTokenKind.Text, text, null, position);
        public static HtmlToken Generic(string text, int position) => new(HtmlTokenKind.GenericTag, text, null, position);
        public static HtmlToken Relevant(HtmlTag tag) => new(HtmlTokenKind.RelevantTag, tag.Raw, tag, tag.Position);
    }

    private sealed record HtmlTag
    {
        public string Raw { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public HtmlTagKind Kind { get; init; }
        public HtmlTagKind CellKind { get; init; }
        public bool IsClosing { get; init; }
        public bool AttributeError { get; init; }
        public int Position { get; init; }
        public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<string> DuplicateDataAttributes { get; init; } = Array.Empty<string>();
    }

    private sealed class HtmlTableState
    {
        public int Index { get; init; }
        public string? Section { get; init; }
        public int SourceLine { get; init; }
        public bool Closed { get; set; }
        public bool Invalid { get; set; }
        public List<HtmlRowState> Rows { get; } = [];
    }

    private sealed class HtmlRowState
    {
        public required HtmlTableState Table { get; init; }
        public int Ordinal { get; init; }
        public int SourceLine { get; init; }
        public bool Closed { get; set; }
        public bool Invalid { get; set; }
        public bool UnclosedReported { get; set; }
        public bool AttributeError { get; init; }
        public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<HtmlCellState> Cells { get; } = [];
    }

    private sealed class HtmlCellState
    {
        public HtmlTagKind Kind { get; init; }
        public int Position { get; init; }
        public bool Closed { get; set; }
        public StringBuilder Content { get; } = new();
    }

    private sealed record HtmlOpenTag(HtmlTagKind Kind, HtmlTableState? Table, HtmlRowState? Row, HtmlCellState? Cell)
    {
        public static HtmlOpenTag ForTable(HtmlTableState table) => new(HtmlTagKind.Table, table, null, null);
        public static HtmlOpenTag ForRow(HtmlRowState row) => new(HtmlTagKind.Row, row.Table, row, null);
        public static HtmlOpenTag ForCell(HtmlCellState cell) => new(HtmlTagKind.Cell, null, null, cell);
    }
}
