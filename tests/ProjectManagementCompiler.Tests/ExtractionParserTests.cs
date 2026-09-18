using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;

namespace ProjectManagementCompiler.Tests;

internal static class ExtractionParserTests
{
    public static void MarkdownParserPreservesVietnameseAndStableSourceMetadata()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
            "# Kế hoạch dự án\n\n## Bảng công việc\n\n| ID | Name | Effort | Start |\n|---|---|---:|---|\n| A01 | Kiểm thử tiếng Việt | 12 | 2026-09-18 |\n| A02 | Xác nhận | 8 | 2026-09-19 |");

        var result = MarkdownTableParser.Parse(document);

        TestAssert.Equal(2, result.Rows.Count, "Markdown rows should retain source order.");
        TestAssert.Equal("Kiểm thử tiếng Việt", result.Rows[0].Cells["Name"], "UTF-8 text should be preserved.");
        TestAssert.Equal(document.Source.RelativeFile, result.Rows[0].SourceRelativePath, "Row should retain relative source path.");
        TestAssert.Equal("Bảng công việc", result.Rows[0].Section, "Row should retain the active heading.");
        TestAssert.Equal(1, result.Rows[0].TableIndex, "The first table should have deterministic index one.");
        TestAssert.Equal(1, result.Rows[0].RowIndex, "The first data row should have deterministic index one.");
        TestAssert.True(result.Rows[0].SourceLine > 0, "Row should retain its source line.");
        TestAssert.Equal("A02", result.Rows[1].Cells["ID"], "Rows should be emitted in document order.");
    }

    public static void HtmlParserReadsTablesAndDataAttributesInStableOrder()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html",
            "<h1>Lộ trình</h1><table><tr><th>Phase</th><th>Name</th></tr>"
            + "<tr data-phase=\"PH0\"><td>PH0</td><td>Khởi động</td></tr>"
            + "<tr data-phase=\"PH1\"><td>PH1</td><td>Thực hiện</td></tr></table>");

        var result = HtmlTableParser.Parse(document);

        TestAssert.Equal(2, result.Rows.Count, "HTML parser should return data rows only.");
        TestAssert.Equal("PH0", result.Rows[0].DataAttributes["data-phase"], "HTML data attributes should be retained.");
        TestAssert.Equal("Khởi động", result.Rows[0].Cells["Name"], "HTML UTF-8 text should be decoded and preserved.");
        TestAssert.Equal(1, result.Rows[0].TableIndex, "HTML table index should be deterministic.");
        TestAssert.Equal(2, result.Rows[1].RowIndex, "HTML row order should be stable.");
    }

    public static void HtmlParserRejectsDuplicateDataAttributesWithoutThrowing()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html",
            "<h1>Gantt</h1><table><tr><th>ID</th><th>Name</th></tr>"
            + "<tr data-phase=\"PH0\" data-phase=\"duplicate\"><td>A01</td><td>Malformed</td></tr>"
            + "<tr data-z=\"last\" data-a=\"first\"><td>A02</td><td>Valid</td></tr></table>");

        var result = HtmlTableParser.Parse(document);

        TestAssert.Equal(1, result.Rows.Count, "Rows with duplicate data-* attributes must be skipped safely.");
        TestAssert.Equal("A02", result.Rows[0].Cells["ID"], "A valid row after a malformed row must still be emitted.");
        TestAssert.Equal("data-z,data-a", string.Join(",", result.Rows[0].DataAttributes.Keys), "Valid data attributes must preserve source order.");
        var diagnostic = result.Diagnostics.Single(d => d.Code == "DUPLICATE_DATA_ATTRIBUTE");
        TestAssert.Equal(WarningSeverity.Error, diagnostic.Severity, "Duplicate data-* attributes must be an Error diagnostic.");
        TestAssert.Equal(document.Source.RelativeFile, diagnostic.SourceReferences.Single().RelativeFile, "Duplicate attribute diagnostics must retain source metadata.");
        TestAssert.Equal("table-01", diagnostic.SourceReferences.Single().Table, "Duplicate attribute diagnostics must identify the source table.");
    }

    public static void HtmlParserRejectsMismatchedCellTagsWithoutGuessingRows()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html",
            "<h1>Gantt</h1><table><tr><th>ID</th><th>Name</th></tr>"
            + "<tr><td>A01</th><td>Malformed</td></tr>"
            + "<tr><td>A02</td><td>Valid</td></tr></table>");

        var result = HtmlTableParser.Parse(document);

        TestAssert.Equal(1, result.Rows.Count, "Mismatched HTML cell tags must not produce a guessed row.");
        TestAssert.Equal("A02", result.Rows[0].Cells["ID"], "Valid HTML rows after a malformed row must still be emitted.");
        var diagnostic = result.Diagnostics.Single(d => d.Code == "MISMATCHED_HTML_CELL_TAG");
        TestAssert.Equal(WarningSeverity.Error, diagnostic.Severity, "Mismatched HTML cell tags must be explicit errors.");
        TestAssert.Equal(document.Source.RelativeFile, diagnostic.SourceReferences.Single().RelativeFile, "Mismatched-cell diagnostics must retain source metadata.");
    }

    public static void ParsersDiagnoseMissingHeadingsAndMalformedCells()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
            "# Không đúng tiêu đề\n\n| Field | Capacity | Start |\n|---|---|---|\n| Capacity | not-a-number | not-a-date |");

        var result = MarkdownTableParser.Parse(document);

        TestAssert.True(result.Diagnostics.Any(d => d.Code == "MISSING_REQUIRED_HEADING"), "Missing required headings must be explicit.");
        TestAssert.True(result.Diagnostics.Any(d => d.Code == "MALFORMED_NUMERIC"), "Malformed numeric cells must be explicit.");
        TestAssert.True(result.Diagnostics.Any(d => d.Code == "MALFORMED_DATE"), "Malformed date cells must be explicit.");
    }

    public static void MarkdownParserRejectsDuplicateHeadersAndWrongCellCounts()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
            "# Source plan\n\n## Source identity\n\n| Field | Value | Value |\n|---|---|---|\n| Project ID | project | duplicate |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | only | extra |\n| PH1 |\n");

        var result = MarkdownTableParser.Parse(document);

        TestAssert.True(result.Diagnostics.Any(d => d.Code == "DUPLICATE_TABLE_HEADER" && d.Severity == WarningSeverity.Error), "Duplicate Markdown headers must be explicit errors.");
        TestAssert.True(result.Diagnostics.Count(d => d.Code == "TABLE_CELL_COUNT_MISMATCH" && d.Severity == WarningSeverity.Error) >= 2, "Too few and too many Markdown cells must be explicit errors.");
        TestAssert.Equal(0, result.Rows.Count, "Malformed Markdown rows must not be padded, truncated, or dictionary-overwritten.");
    }

    public static void MarkdownParserIgnoresBacktickAndTildeFencedContent()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
            "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n\n```markdown\n# Fake heading\n\n| Fake | Value |\n|---|---|\n| F01 | Do not parse |\n```\n\n~~~text\n## Fake phases\n\n| Phase ID | Name |\n|---|---|\n| FAKE | Do not parse |\n~~~\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Real phase |");

        var result = MarkdownTableParser.Parse(document);

        TestAssert.Equal(2, result.Rows.Count, "Markdown headings and tables inside fences must be ignored.");
        TestAssert.True(result.Rows.All(row => !row.Cells.Values.Contains("Do not parse")), "Fenced table rows must not be emitted.");
        TestAssert.False(result.Diagnostics.Any(d => d.Code == "MISSING_REQUIRED_HEADING"), "Fenced headings must not interfere with valid heading detection.");
    }

    public static void MarkdownParserPreservesEscapedPipesAsCellContent()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
            "# Source plan\n\n| ID | Name |\n|---|---|\n| A01 | Text with \\| pipe |");

        var result = MarkdownTableParser.Parse(document);

        TestAssert.Equal(1, result.Rows.Count, "An escaped pipe must not create an extra Markdown cell.");
        TestAssert.Equal("Text with | pipe", result.Rows[0].Cells["Name"], "Escaped pipes must be preserved as literal pipe content.");
        TestAssert.False(result.Diagnostics.Any(d => d.Code == "TABLE_CELL_COUNT_MISMATCH"), "Escaped pipes must not produce a cell-count diagnostic.");
    }

    public static void MarkdownParserStopsAdjacentTablesBeforeNextHeader()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
            "# Source plan\n\n## Source identity\n\n"
            + "| Field | Value |\n|---|---|\n| Project ID | project |\n| First | one |\n| Second | two |\n"
            + "| ID | Name |\n|---|---|\n| A01 | Next table |\n"
            + "## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Real phase |");

        var result = MarkdownTableParser.Parse(document);

        TestAssert.Equal(5, result.Rows.Count, "Adjacent Markdown tables must retain all rows without merging them.");
        var nextTableRow = result.Rows.Single(row => row.Cells.TryGetValue("ID", out var id) && id == "A01");
        TestAssert.Equal(2, nextTableRow.TableIndex, "The adjacent table must receive the next stable table index.");
        TestAssert.Equal(1, nextTableRow.RowIndex, "The adjacent table row order must restart at one.");
        var phaseRow = result.Rows.Single(row => row.Cells.TryGetValue("Phase ID", out var phase) && phase == "PH0");
        TestAssert.Equal(3, phaseRow.TableIndex, "A later table must retain its stable index after an adjacent table.");
        TestAssert.False(result.Diagnostics.Any(d => d.Code == "TABLE_CELL_COUNT_MISMATCH"), "A valid adjacent table must not create shape diagnostics.");
    }

    public static void MarkdownParserUsesBackslashParityForEscapedPipes()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
            "# Source plan\n\n| ID | Literal | Backslashes | Tail |\n|---|---|---|---|\n"
            + "| A01 | one \\| two | path \\\\|tail |");

        var result = MarkdownTableParser.Parse(document);

        TestAssert.Equal(1, result.Rows.Count, "Odd and even backslash runs must produce the intended four-cell row.");
        TestAssert.Equal("one | two", result.Rows[0].Cells["Literal"], "An odd backslash run must escape the pipe and remove only its escaping slash.");
        TestAssert.Equal("path \\\\", result.Rows[0].Cells["Backslashes"], "An even backslash run must remain content before the delimiter.");
        TestAssert.Equal("tail", result.Rows[0].Cells["Tail"], "The pipe after an even backslash run must remain a delimiter.");
    }

    public static void MarkdownParserDiagnosesEvenBackslashPipeShapeMismatch()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
            "# Source plan\n\n| ID | Name |\n|---|---|\n| A01 | path \\\\| next |");

        var result = MarkdownTableParser.Parse(document);

        TestAssert.Equal(0, result.Rows.Count, "An even backslash before a pipe must expose the delimiter-induced shape mismatch.");
        TestAssert.True(result.Diagnostics.Any(d => d.Code == "TABLE_CELL_COUNT_MISMATCH" && d.Severity == WarningSeverity.Error), "Delimiter-induced cell-count mismatches must remain explicit Error diagnostics.");
    }

    public static void HtmlParserRejectsDuplicateHeadersAndWrongCellCounts()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html",
            "<h1>Gantt</h1><table><tr><th>ID</th><th>Name</th><th>Name</th></tr><tr><td>A01</td><td>One</td><td>Duplicate</td></tr></table>"
            + "<table><tr><th>ID</th><th>Name</th></tr><tr><td>A02</td></tr><tr><td>A03</td><td>Three</td><td>Extra</td></tr></table>");

        var result = HtmlTableParser.Parse(document);

        TestAssert.True(result.Diagnostics.Any(d => d.Code == "DUPLICATE_TABLE_HEADER" && d.Severity == WarningSeverity.Error), "Duplicate HTML headers must be explicit errors.");
        TestAssert.True(result.Diagnostics.Count(d => d.Code == "TABLE_CELL_COUNT_MISMATCH" && d.Severity == WarningSeverity.Error) >= 2, "Too few and too many HTML cells must be explicit errors.");
        TestAssert.Equal(0, result.Rows.Count, "Malformed HTML rows must not be padded, truncated, or dictionary-overwritten.");
    }

    public static void HtmlParserRejectsTablesWithoutHeaderRows()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html",
            "<h1>Gantt</h1><table><tr><td>ID</td><td>Name</td></tr><tr><td>A01</td><td>First data row</td></tr></table>");

        var result = HtmlTableParser.Parse(document);

        TestAssert.True(result.Diagnostics.Any(d => d.Code == "MISSING_TABLE_HEADER" && d.Severity == WarningSeverity.Error), "HTML tables without a th header row must be explicit errors.");
        TestAssert.Equal(0, result.Rows.Count, "HTML data rows must not be guessed into headers or emitted without a th header row.");
    }

    public static void HtmlParserDiagnosesUnclosedTables()
    {
        var document = Document(
            "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html",
            "<h1>Gantt</h1><table><tr><th>ID</th><th>Name</th></tr><tr><td>A01</td><td>Missing close</td></tr>");

        var result = HtmlTableParser.Parse(document);

        TestAssert.Equal(0, result.Rows.Count, "An unclosed HTML table must not silently appear to have no parsed rows.");
        var diagnostic = result.Diagnostics.Single(d => d.Code == "UNCLOSED_HTML_TABLE");
        TestAssert.Equal(WarningSeverity.Error, diagnostic.Severity, "An unclosed HTML table must be an explicit Error diagnostic.");
    }

    public static void DiscoveryIgnoresUnrecognizedCapturedDocuments()
    {
        var snapshot = new ProjectManagementCompiler.Sources.RepositorySnapshot
        {
            RepositoryId = "test",
            Documents =
            [
                Document("README.md", "# Readme").Source,
                new SourceDocument
                {
                    Id = "notes/random.md",
                    RelativeFile = "notes/random.md",
                    Format = SourceDocumentFormat.Markdown,
                    Content = "| ID | Name |\n|---|---|\n| X | Do not scan |"
                }
            ]
        };

        var result = new IdeaPlanningDiscovery().Discover(snapshot);

        TestAssert.Equal(1, result.Documents.Count, "Discovery must use the explicit recognized path set.");
        TestAssert.False(result.Documents.Any(d => d.Source.RelativeFile == "notes/random.md"), "Arbitrary repository files must not be interpreted.");
    }

    private static PlanningDocument Document(string relativeFile, string content)
    {
        var source = new SourceDocument
        {
            Id = relativeFile,
            RelativeFile = relativeFile,
            Format = relativeFile.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                ? SourceDocumentFormat.Html
                : SourceDocumentFormat.Markdown,
            Content = content,
            SourceReference = new SourceReference
            {
                SourceId = "test",
                Repository = "test",
                RelativeFile = relativeFile,
                ExtractionRule = "test"
            }
        };

        return PlanningDocument.Create(source);
    }
}
