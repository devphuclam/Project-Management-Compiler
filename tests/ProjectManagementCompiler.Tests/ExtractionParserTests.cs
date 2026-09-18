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
