using System.Reflection;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;

namespace ProjectManagementCompiler.Tests;

internal static class ExecutiveProgressXlsxTests
{
    public static void ExecutiveWorkbookComposerExposesNeutralDocumentLayoutContract()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildOfficialFixtureResult());
        var composerType = Type.GetType("ProjectManagementCompiler.Outputs.ExecutiveProgressWorkbookComposer, ProjectManagementCompiler", throwOnError: false);
        TestAssert.True(composerType is not null, "ExecutiveProgressWorkbookComposer must exist so composition stays separate from Open XML serialization.");

        var composer = Activator.CreateInstance(composerType!, nonPublic: true);
        TestAssert.True(composer is not null, "ExecutiveProgressWorkbookComposer must be constructible for direct workbook-composition tests.");
        var build = composerType!.GetMethod("Build", BindingFlags.Public | BindingFlags.Instance, [typeof(ExecutiveProgressReport)]);
        TestAssert.True(build is not null, "ExecutiveProgressWorkbookComposer must expose Build(ExecutiveProgressReport).");

        var document = build!.Invoke(composer, [report]);
        TestAssert.True(document is not null, "Workbook composition must return a neutral document rather than package bytes.");
        TestAssert.Equal("ExecutiveWorkbookDocument", document!.GetType().Name, "The composition boundary must return ExecutiveWorkbookDocument.");
        TestAssert.Equal(0, Convert.ToInt32(ReadRequiredProperty(document, "ActiveSheetIndex")), "The ordered document must open on its first sheet.");

        var sheets = ReadCollection(document, "Sheets");
        TestAssert.Equal(
            "Tổng quan|Lịch trình|Vấn đề cần xử lý|Chi tiết công việc",
            string.Join('|', sheets.Select(sheet => ReadRequiredProperty(sheet, "Name").ToString())),
            "The foundation composer must retain the pre-feature sheet order until the approved five-sheet replacement is introduced.");

        var overview = sheets[0];
        var titleCell = ReadCollection(overview, "Rows")
            .SelectMany(row => ReadCollection(row, "Cells"))
            .First(cell => string.Equals(ReadRequiredProperty(cell, "Value").ToString(), "Báo cáo tiến độ", StringComparison.Ordinal));
        TestAssert.Equal("Title", ReadRequiredProperty(titleCell, "StyleToken").ToString(), "Cells must use semantic style tokens rather than exporter-local numeric style IDs.");
        TestAssert.True(ReadRequiredProperty(titleCell, "NumberFormat") is not null, "Every neutral cell must carry an explicit number-format token.");

        var widths = ReadCollection(overview, "ColumnWidths").Select(Convert.ToDouble).ToArray();
        TestAssert.True(widths.Length > 0 && widths.All(width => width > 0d), "The neutral worksheet must preserve positive column widths.");
        var mergedRanges = overview.GetType().GetProperty("MergedRanges", BindingFlags.Public | BindingFlags.Instance);
        TestAssert.True(mergedRanges is not null, "The neutral worksheet must expose typed merged ranges for title, month, and task-band composition.");
        TestAssert.True(mergedRanges!.PropertyType.IsGenericType && mergedRanges.PropertyType.GetGenericArguments()[0].Name == "ExecutiveWorkbookRange", "Merged ranges must be represented by an ExecutiveWorkbookRange value type.");

        var freezePane = ReadRequiredProperty(overview, "FreezePane");
        TestAssert.True(Convert.ToInt32(ReadRequiredProperty(freezePane, "FrozenRows")) > 0, "The overview document must preserve a vertical freeze pane.");
        TestAssert.True(Convert.ToInt32(ReadRequiredProperty(freezePane, "FrozenColumns")) > 0, "The overview document must preserve a horizontal freeze pane.");
        TestAssert.False(Convert.ToBoolean(ReadRequiredProperty(overview, "ShowGridLines")), "The neutral worksheet must hide gridlines.");
        TestAssert.Equal(100, Convert.ToInt32(ReadRequiredProperty(overview, "ZoomPercent")), "The neutral worksheet must open at 100% zoom.");

        var printSettings = ReadRequiredProperty(overview, "PrintSettings");
        TestAssert.Equal("Landscape", ReadRequiredProperty(printSettings, "Orientation").ToString(), "The neutral worksheet must retain landscape print orientation.");
        TestAssert.Equal(1, Convert.ToInt32(ReadRequiredProperty(printSettings, "FitToWidth")), "The neutral worksheet must retain its fit-to-width setting.");
        TestAssert.Equal(0, Convert.ToInt32(ReadRequiredProperty(printSettings, "FitToHeight")), "The neutral worksheet must retain its fit-to-height setting.");
    }

    public static void ExecutiveWorkbookPackageRemainsDeterministicForTheSameReport()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildOfficialFixtureResult());
        var first = Export(report);
        var second = Export(report);

        TestAssert.True(first.SequenceEqual(second), "The same management report must produce identical XLSX package bytes.");
    }

    public static void ExecutiveWorkbookUsesApprovedSheetsAndOverviewRegions()
    {
        var result = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult() with
        {
            Analysis = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult().Analysis with
            {
                Alerts = Array.Empty<ProjectManagementCompiler.Domain.Alert>(),
                CpmNodes = Array.Empty<ProjectManagementCompiler.Domain.CpmNodeMetric>(),
                CriticalPathIds = Array.Empty<string>()
            }
        };
        var report = new ExecutiveProgressReportProjector().Build(result);
        var bytes = Export(report);

        ExecutiveProgressTestFixtures.AssertHasSheetNames(
            bytes,
            "Tổng quan",
            "Lịch trình",
            "Vấn đề cần xử lý",
            "Chi tiết công việc");
        TestAssert.Contains("activeTab=\"0\"", ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/workbook.xml"), "The executive workbook must open on Tổng quan.");
        var overview = ExecutiveProgressTestFixtures.WorksheetText(bytes, 1);
        TestAssert.Contains("Báo cáo tiến độ", overview, "The overview must contain a reader-facing report title.");
        TestAssert.Contains("Tình trạng lịch trình", overview, "The overview must contain the schedule summary block.");
        TestAssert.Contains("Mốc sắp tới", overview, "The overview must contain the next-milestone summary block.");
        TestAssert.Contains("Tiến độ được ghi nhận", overview, "The overview must contain the recorded-progress summary block.");
        TestAssert.Contains("Việc cần quyết định", overview, "The overview must contain the readiness/decision summary block.");
        TestAssert.Contains("Ngày báo cáo", overview, "The overview must text-label the reporting-date marker.");
    }

    public static void ExecutiveWorkbookHidesTechnicalProvenanceAndUsesPresentationSettings()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildOfficialFixtureResult());
        var bytes = Export(report);
        var packageText = string.Join("|", new[]
        {
            ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/workbook.xml"),
            ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/worksheets/sheet1.xml"),
            ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/worksheets/sheet2.xml"),
            ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/worksheets/sheet3.xml"),
            ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/worksheets/sheet4.xml")
        });

        foreach (var forbidden in new[]
        {
            "PMC_EXPORT_KIND",
            "PMC_EXPORT_CONTRACT_VERSION",
            "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4",
            "snapshot-test-execution",
            "project-management-compiler-manifest.json",
            "ExecutionOverlay",
            "SourceExecution",
            "ValidationState"
        })
        {
            TestAssert.False(packageText.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"Executive workbook must not expose technical value '{forbidden}'.");
        }

        foreach (var sheetNumber in Enumerable.Range(1, 4))
        {
            var sheet = ExecutiveProgressTestFixtures.ReadEntry(bytes, $"xl/worksheets/sheet{sheetNumber}.xml");
            TestAssert.Contains("showGridLines=\"0\"", sheet, "Every executive sheet must hide gridlines.");
            TestAssert.Contains("zoomScale=\"100\"", sheet, "Every executive sheet must open at 100% zoom.");
        }

        var overview = ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/worksheets/sheet1.xml");
        TestAssert.Contains("orientation=\"landscape\"", overview, "The overview must use landscape print orientation.");
        TestAssert.Contains("fitToWidth=\"1\"", overview, "The overview must fit to one page wide.");
        TestAssert.Contains("<pane", overview, "The overview must freeze its context while scrolling.");
        var styles = ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/styles.xml");
        TestAssert.Contains("wrapText=\"1\"", styles, "Primary executive text must wrap instead of being semantically clipped.");
        TestAssert.Contains("Aptos", styles, "The executive workbook must use a standard spreadsheet font.");
    }

    public static void ExecutiveWorkbookWrapsDefaultReaderText()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildOfficialFixtureResult());
        var bytes = new ExecutiveProgressXlsxExporter().Export(report);
        var styles = ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/styles.xml");
        var cellFormats = System.Xml.Linq.XDocument.Parse(styles)
            .Descendants("{http://schemas.openxmlformats.org/spreadsheetml/2006/main}cellXfs")
            .Single()
            .Elements("{http://schemas.openxmlformats.org/spreadsheetml/2006/main}xf")
            .ToArray();

        TestAssert.True(cellFormats.Length > 0, "The executive workbook must declare a default cell format.");
        TestAssert.True(
            cellFormats[0].Descendants("{http://schemas.openxmlformats.org/spreadsheetml/2006/main}alignment")
                .Any(alignment => (string?)alignment.Attribute("wrapText") == "1"),
            "Default reader-facing text must wrap so long work-package and delivery-card descriptions are not clipped.");
    }

    public static void ExecutiveWorkbookIdentifiesOfficialAuthority()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildOfficialFixtureResult());
        var overview = ExecutiveProgressTestFixtures.WorksheetText(new ExecutiveProgressXlsxExporter().Export(report), 1);

        TestAssert.Contains("Nguồn chính thức", overview, "Every executive export must identify its official authority mode in the reader-facing provenance line.");
    }

    public static void ExecutiveWorkbookHasExplicitEmptyAttentionStateAndBoundedOverview()
    {
        var result = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult() with
        {
            Analysis = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult().Analysis with
            {
                Alerts = Array.Empty<ProjectManagementCompiler.Domain.Alert>(),
                CpmNodes = Array.Empty<ProjectManagementCompiler.Domain.CpmNodeMetric>(),
                CriticalPathIds = Array.Empty<string>()
            }
        };
        var report = new ExecutiveProgressReportProjector().Build(result);
        var bytes = Export(report);
        var overview = ExecutiveProgressTestFixtures.WorksheetText(bytes, 1);
        var actionSheet = ExecutiveProgressTestFixtures.WorksheetText(bytes, 3);

        TestAssert.Contains("Hiện chưa có nội dung cần xin ý kiến", overview, "The overview must state the exact empty attention state.");
        TestAssert.Contains("Hiện chưa có nội dung cần xin ý kiến", actionSheet, "The action sheet must state the exact empty attention state.");
        TestAssert.False(CountRows(overview, "attention") > 5, "The overview must never show more than five attention items.");
    }

    public static void ExecutiveWorkbookUsesProgressiveDisclosureRowsAndWeeklyAxis()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildOfficialFixtureResult());
        var bytes = Export(report);
        var overview = ExecutiveProgressTestFixtures.WorksheetText(bytes, 1);
        var schedule = ExecutiveProgressTestFixtures.WorksheetText(bytes, 2);
        var actions = ExecutiveProgressTestFixtures.WorksheetText(bytes, 3);
        var details = ExecutiveProgressTestFixtures.WorksheetText(bytes, 4);

        TestAssert.Contains("Tháng", overview, "The overview timeline must group the axis by month.");
        TestAssert.Contains("W38", overview, "The overview timeline must show ISO-week detail.");
        TestAssert.False(overview.Contains("Decision", StringComparison.OrdinalIgnoreCase), "Decision-kind records must not appear in the overview timeline.");
        TestAssert.Contains("Planning package F05", schedule, "The schedule sheet must contain work-package rows.");
        TestAssert.False(schedule.Contains("Planning card", StringComparison.OrdinalIgnoreCase), "The schedule sheet must not expand delivery cards.");
        TestAssert.Equal(
            "Việc cần xử lý|Ảnh hưởng|Đầu mối|Cần xong trước",
            string.Join('|', ExecutiveProgressTestFixtures.WorksheetRow(bytes, 3, "Việc cần xử lý")),
            "The action sheet must expose exactly the four reader-facing columns.");
        TestAssert.Contains("Chi tiết công việc", details, "The detail sheet must contain its reader-facing title.");
        TestAssert.Contains("Planning card", details, "The detail sheet must contain delivery-card rows.");
        TestAssert.Equal(1, CountOccurrences(details, "P01-A"), "A raw delivery-card ID must appear only in the final reference field.");
        TestAssert.False(details.Contains("PMC_", StringComparison.Ordinal), "The detail sheet must not expose technical preview markers.");
    }

    public static void ExecutiveWorkbookConnectsTimelineRowsToWeeklyAxis()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildOfficialFixtureResult());
        var bytes = new ExecutiveProgressXlsxExporter().Export(report);
        var overview = ExecutiveProgressTestFixtures.WorksheetText(bytes, 1);
        var schedule = ExecutiveProgressTestFixtures.WorksheetText(bytes, 2);

        TestAssert.Contains("■", overview, "Overview phase rows must connect to the weekly axis with visible plan markers.");
        TestAssert.Contains("◆", overview, "Overview milestone rows must connect to the weekly axis with visible milestone markers.");
        TestAssert.Contains("■", schedule, "Work-package schedule rows must connect to the shared weekly axis with visible plan markers.");
    }

    public static void ExecutiveWorkbookKeepsReportingMarkerVisibleOutsidePlanningBounds()
    {
        var baseResult = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult();
        var before = new ExecutiveProgressReportProjector().Build(baseResult with
        {
            Project = baseResult.Project with
            {
                ImportMetadata = baseResult.Project.ImportMetadata! with { RegisterStatusDate = new DateOnly(2026, 9, 1) }
            }
        });
        var after = new ExecutiveProgressReportProjector().Build(baseResult with
        {
            Project = baseResult.Project with
            {
                ImportMetadata = baseResult.Project.ImportMetadata! with { RegisterStatusDate = new DateOnly(2027, 1, 10) }
            }
        });

        var beforeText = ExecutiveProgressTestFixtures.WorksheetText(Export(before), 1);
        var afterText = ExecutiveProgressTestFixtures.WorksheetText(Export(after), 1);
        TestAssert.Contains("Ngày báo cáo|01/09/2026", beforeText, "A pre-plan reporting date must remain visible on the executive axis.");
        TestAssert.Contains("W36", beforeText, "The weekly axis must extend before the planning window.");
        TestAssert.Contains("Ngày báo cáo|10/01/2027", afterText, "A post-plan reporting date must remain visible on the executive axis.");
        TestAssert.Contains("Tháng 01/2027", afterText, "The monthly axis must extend after the planning window.");
        TestAssert.False(afterText.Contains("Ngày 10/01/2027", StringComparison.Ordinal), "The executive axis must stay weekly, not one column per day.");
    }

    private static byte[] Export(ExecutiveProgressReport report)
    {
        var exporterType = Type.GetType("ProjectManagementCompiler.Outputs.ExecutiveProgressXlsxExporter, ProjectManagementCompiler", throwOnError: false);
        TestAssert.True(exporterType is not null, "ExecutiveProgressXlsxExporter must exist before workbook regressions can pass.");
        var exporter = Activator.CreateInstance(exporterType!);
        var export = exporterType!.GetMethod("Export", BindingFlags.Public | BindingFlags.Instance, [typeof(ExecutiveProgressReport)]);
        TestAssert.True(export is not null, "ExecutiveProgressXlsxExporter must expose Export(ExecutiveProgressReport).");
        return (byte[])export!.Invoke(exporter, [report])!;
    }

    private static object ReadRequiredProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        TestAssert.True(property is not null, $"{instance.GetType().Name} must expose {propertyName}.");
        var value = property!.GetValue(instance);
        TestAssert.True(value is not null, $"{instance.GetType().Name}.{propertyName} must not be null.");
        return value!;
    }

    private static object[] ReadCollection(object instance, string propertyName)
    {
        var value = ReadRequiredProperty(instance, propertyName);
        TestAssert.True(value is System.Collections.IEnumerable, $"{instance.GetType().Name}.{propertyName} must be enumerable.");
        return ((System.Collections.IEnumerable)value).Cast<object>().ToArray();
    }

    private static int CountRows(string worksheetText, string marker) =>
        worksheetText.Split("<row", StringSplitOptions.RemoveEmptyEntries)
            .Count(row => row.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static int CountOccurrences(string value, string expected)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(expected, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += expected.Length;
        }

        return count;
    }
}
