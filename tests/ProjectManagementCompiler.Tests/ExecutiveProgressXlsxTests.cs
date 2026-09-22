using System.Reflection;
using System.IO.Compression;
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
            "Tổng quan|Gantt theo ngày|30 ngày tới|Vấn đề cần xử lý|Chi tiết công việc",
            string.Join('|', sheets.Select(sheet => ReadRequiredProperty(sheet, "Name").ToString())),
            "The neutral workbook document must expose the approved five-sheet executive report in its reader-facing order.");

        var overview = sheets[0];
        var titleCell = ReadCollection(overview, "Rows")
            .SelectMany(row => ReadCollection(row, "Cells"))
            .First(cell => string.Equals(ReadRequiredProperty(cell, "Value").ToString(), "Báo cáo điều hành tiến độ", StringComparison.Ordinal));
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

    public static void ExecutiveWorkbookDocumentValidatesProgressiveDisclosureMetadata()
    {
        var rowType = InternalOutputType("ExecutiveWorkbookRow");
        var columnGroupType = InternalOutputType("ExecutiveWorkbookColumnGroup");
        var worksheetType = InternalOutputType("ExecutiveWorkbookWorksheet");

        TestAssert.True(rowType.GetProperty("OutlineLevel") is not null, "Workbook rows must expose an outline level.");
        TestAssert.True(rowType.GetProperty("Hidden") is not null, "Workbook rows must expose initial hidden state.");
        TestAssert.True(rowType.GetProperty("Collapsed") is not null, "Workbook rows must expose initial collapsed state.");
        TestAssert.True(worksheetType.GetProperty("ColumnGroups") is not null, "Worksheets must expose typed column groups.");
        TestAssert.True(worksheetType.GetProperty("AutoFilterRange") is not null, "Worksheets must expose an optional auto-filter range.");
        TestAssert.True(worksheetType.GetProperty("OutlineSummaryBelow") is not null, "Worksheets must expose row summary direction.");
        TestAssert.True(worksheetType.GetProperty("OutlineSummaryRight") is not null, "Worksheets must expose column summary direction.");

        var cell = CreateInternal(
            "ExecutiveWorkbookCell",
            "WBS",
            ParseInternalEnum("ExecutiveWorkbookStyleToken", "Header"),
            ParseInternalEnum("ExecutiveWorkbookNumberFormat", "Text"),
            false);
        var cells = TypedArray(cell.GetType(), cell);
        var parent = CreateInternal("ExecutiveWorkbookRow", cells, 2, false, true);
        var deliveryCard = CreateInternal("ExecutiveWorkbookRow", cells, 3, true, false);
        var rows = TypedArray(rowType, parent, deliveryCard);
        var widths = Enumerable.Repeat(12d, 12).ToArray();
        var mergedRanges = TypedArray(InternalOutputType("ExecutiveWorkbookRange"));
        var group = CreateInternal("ExecutiveWorkbookColumnGroup", 9, 10, 1, true, true);
        var groups = TypedArray(columnGroupType, group);
        var filter = CreateInternal("ExecutiveWorkbookRange", 1, 1, 2, 12);
        var pane = CreateInternal("ExecutiveWorkbookPane", 1, 3);
        var print = CreateInternal(
            "ExecutiveWorkbookPrintSettings",
            ParseInternalEnum("ExecutiveWorkbookPrintOrientation", "Landscape"),
            0,
            0);

        var worksheet = CreateInternal(
            "ExecutiveWorkbookWorksheet",
            "WBS",
            rows,
            widths,
            mergedRanges,
            pane,
            print,
            false,
            100,
            groups,
            filter,
            false,
            false);

        TestAssert.Equal(3, Convert.ToInt32(ReadRequiredProperty(deliveryCard, "OutlineLevel")), "Delivery Card rows must retain outline level 3.");
        TestAssert.True(Convert.ToBoolean(ReadRequiredProperty(deliveryCard, "Hidden")), "Delivery Card rows must retain their initial hidden state.");
        TestAssert.True(Convert.ToBoolean(ReadRequiredProperty(parent, "Collapsed")), "Visible parent rows must retain their collapsed state.");
        TestAssert.Equal(1, ReadCollection(worksheet, "ColumnGroups").Length, "A valid worksheet must retain its typed column groups.");
        TestAssert.False(Convert.ToBoolean(ReadRequiredProperty(worksheet, "OutlineSummaryBelow")), "WBS parent summaries must precede hidden children.");
        TestAssert.False(Convert.ToBoolean(ReadRequiredProperty(worksheet, "OutlineSummaryRight")), "WBS primary columns must precede optional groups.");

        AssertInvocationThrows<ArgumentOutOfRangeException>(
            () => CreateInternal("ExecutiveWorkbookRow", cells, 8, false, false),
            "Row outline levels above seven must be rejected.");

        var overlappingGroups = TypedArray(
            columnGroupType,
            group,
            CreateInternal("ExecutiveWorkbookColumnGroup", 10, 11, 1, true, false));
        AssertInvocationThrows<ArgumentException>(
            () => CreateInternal(
                "ExecutiveWorkbookWorksheet",
                "Invalid groups",
                rows,
                widths,
                mergedRanges,
                pane,
                print,
                false,
                100,
                overlappingGroups,
                filter,
                false,
                false),
            "Overlapping column groups must be rejected.");

        var outOfBoundsFilter = CreateInternal("ExecutiveWorkbookRange", 1, 1, 3, 12);
        AssertInvocationThrows<ArgumentOutOfRangeException>(
            () => CreateInternal(
                "ExecutiveWorkbookWorksheet",
                "Invalid filter",
                rows,
                widths,
                mergedRanges,
                pane,
                print,
                false,
                100,
                groups,
                outOfBoundsFilter,
                false,
                false),
            "Auto-filter ranges outside the used worksheet bounds must be rejected.");
    }

    public static void ExecutiveWorkbookPackageSerializesProgressiveDisclosureMetadata()
    {
        var document = CreateProgressiveDisclosureDocument();
        var exporterType = typeof(ExecutiveProgressXlsxExporter);
        var serialize = exporterType.GetMethod("Serialize", BindingFlags.NonPublic | BindingFlags.Static);
        TestAssert.True(serialize is not null, "The XLSX exporter must retain one deterministic neutral-document serialization seam.");

        var first = (byte[])serialize!.Invoke(null, [document])!;
        var second = (byte[])serialize.Invoke(null, [document])!;
        TestAssert.True(first.SequenceEqual(second), "Progressive-disclosure metadata must not make package bytes non-deterministic.");

        var worksheet = ExecutiveProgressTestFixtures.ReadXml(first, "xl/worksheets/sheet1.xml");
        var outline = worksheet.Descendants().SingleOrDefault(element => element.Name.LocalName == "outlinePr");
        TestAssert.True(outline is not null, "Worksheet XML must contain outlinePr.");
        TestAssert.Equal("0", outline!.Attribute("summaryBelow")?.Value, "WBS row summaries must be serialized above their children.");
        TestAssert.Equal("0", outline.Attribute("summaryRight")?.Value, "WBS column summaries must be serialized left of optional groups.");

        var rows = worksheet.Descendants().Where(element => element.Name.LocalName == "row").ToArray();
        TestAssert.Equal("2", rows[0].Attribute("outlineLevel")?.Value, "The parent row outline level must be serialized.");
        TestAssert.Equal("1", rows[0].Attribute("collapsed")?.Value, "The parent collapsed state must be serialized.");
        TestAssert.Equal("3", rows[1].Attribute("outlineLevel")?.Value, "The Delivery Card row outline level must be serialized.");
        TestAssert.Equal("1", rows[1].Attribute("hidden")?.Value, "The Delivery Card initial hidden state must be serialized.");

        var groupedColumns = worksheet.Descendants()
            .Where(element => element.Name.LocalName == "col"
                && element.Attribute("outlineLevel")?.Value == "1")
            .ToArray();
        TestAssert.Equal(2, groupedColumns.Length, "Every column in the approved test group must carry outline metadata.");
        TestAssert.True(groupedColumns.All(column => column.Attribute("hidden")?.Value == "1"), "Initially collapsed optional columns must be hidden.");
        TestAssert.Equal("1", groupedColumns[^1].Attribute("collapsed")?.Value, "The final grouped column must expose the collapsed control.");

        var autoFilter = worksheet.Descendants().SingleOrDefault(element => element.Name.LocalName == "autoFilter");
        TestAssert.Equal("A1:L2", autoFilter?.Attribute("ref")?.Value, "The complete typed auto-filter range must be serialized.");

        using var archive = new ZipArchive(new MemoryStream(first), ZipArchiveMode.Read);
        TestAssert.False(
            archive.Entries.Any(entry =>
                entry.FullName.Contains("vba", StringComparison.OrdinalIgnoreCase)
                || entry.FullName.Contains("connections", StringComparison.OrdinalIgnoreCase)
                || entry.FullName.Contains("externalLinks", StringComparison.OrdinalIgnoreCase)),
            "The report package must not gain macro, connection, or external-link parts.");
    }

    public static void ExecutiveWorkbookPackageRemainsDeterministicForTheSameReport()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildOfficialFixtureResult());
        var first = Export(report);
        var second = Export(report);

        TestAssert.True(first.SequenceEqual(second), "The same management report must produce identical XLSX package bytes.");
    }

    public static void ExecutiveWorkbookDailyGanttUsesPairedDailyLanesAndSemanticStyles()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult());
        var document = Compose(report);
        var sheets = ReadCollection(document, "Sheets");
        var gantt = sheets.SingleOrDefault(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "Gantt theo ngày", StringComparison.Ordinal));
        TestAssert.True(gantt is not null, "The executive workbook must contain the full daily Gantt sheet before its detailed reader view can be inspected.");

        var rows = ReadCollection(gantt!, "Rows");
        var cells = rows.SelectMany(row => ReadCollection(row, "Cells")).ToArray();
        var dateHeader = rows
            .Select(row => ReadCollection(row, "Cells")
                .Select(cell => ReadRequiredProperty(cell, "Value"))
                .OfType<DateOnly>()
                .ToArray())
            .OrderByDescending(dates => dates.Length)
            .First();
        var expectedDates = Enumerable.Range(0, 107).Select(offset => new DateOnly(2026, 9, 16).AddDays(offset)).ToArray();
        TestAssert.Equal(string.Join('|', expectedDates), string.Join('|', dateHeader), "The full Gantt must allocate one consecutive daily column from the earliest Actual date through the baseline finish.");

        foreach (var header in new[] { "Mã", "Hạng mục", "Trạng thái", "Đầu mối", "% thực tế", "Cập nhật cuối", "Làn" })
        {
            TestAssert.True(cells.Any(cell => string.Equals(ReadRequiredProperty(cell, "Value").ToString(), header, StringComparison.Ordinal)), $"The daily Gantt must retain fixed management column '{header}'.");
        }

        var p01PlanIndex = Array.FindIndex(rows, row =>
            RowContains(row, "P01-A") && RowContains(row, "Kế hoạch"));
        TestAssert.True(p01PlanIndex >= 0, "A delivery-card task band must show its identity once on the adjacent Plan lane.");
        TestAssert.True(p01PlanIndex + 1 < rows.Length && RowContains(rows[p01PlanIndex + 1], "Thực tế"), "Every non-milestone task band must place the Actual lane immediately after the Plan lane.");
        TestAssert.False(RowContains(rows[p01PlanIndex + 1], "P01-A"), "The Actual lane must not repeat the task identity already shown by the Plan lane.");

        AssertCellStyle(cells, "Kế hoạch", "Plan", "Plan labels must use the semantic Plan style.");
        AssertCellStyle(cells, "Thực tế", "ActualComplete", "Evidence-backed Actual labels must use the semantic Actual style.");
        AssertCellStyle(cells, "Dự báo", "Forecast", "Official forecast labels must use the semantic Forecast style.");
        AssertCellStyle(cells, "Tạm dừng", "BlockedOrOverdue", "Blocked or overdue text must use the semantic red-cue style.");
        AssertCellStyle(cells, "Chưa cập nhật", "Unknown", "Missing official execution must use the semantic unknown style.");
        AssertCellStyle(cells, "◆", "Milestone", "Milestones must use their distinct semantic symbol and style.");
        TestAssert.True(cells.Any(cell => ReadRequiredProperty(cell, "StyleToken").ToString() == "Weekend"), "Weekend daily columns must use a subdued semantic weekend style.");
        AssertCellStyle(cells, "Ngày báo cáo", "ReportingBoundary", "The reporting-date marker must have its own labelled semantic style.");

        var mergedRanges = ReadCollection(gantt!, "MergedRanges");
        TestAssert.True(mergedRanges.Length > 0, "The daily Gantt must merge month and task-band presentation ranges rather than duplicate identities.");
        var pane = ReadRequiredProperty(gantt!, "FreezePane");
        TestAssert.True(Convert.ToInt32(ReadRequiredProperty(pane, "FrozenRows")) >= 5, "The daily Gantt must freeze its timeline headers.");
        TestAssert.True(Convert.ToInt32(ReadRequiredProperty(pane, "FrozenColumns")) >= 7, "The daily Gantt must freeze its fixed management columns.");
        var print = ReadRequiredProperty(gantt!, "PrintSettings");
        TestAssert.Equal("Landscape", ReadRequiredProperty(print, "Orientation").ToString(), "The daily Gantt must use landscape printing.");
        TestAssert.Equal(0, Convert.ToInt32(ReadRequiredProperty(print, "FitToWidth")), "The full daily Gantt must paginate horizontally instead of shrinking daily columns below readability.");

        var bytes = Export(report);
        var sheetIndex = Array.IndexOf(ExecutiveProgressTestFixtures.SheetNames(bytes).ToArray(), "Gantt theo ngày") + 1;
        TestAssert.True(sheetIndex > 0, "The serialized workbook must retain the composed daily-Gantt sheet.");
        var serialized = ExecutiveProgressTestFixtures.WorksheetText(bytes, sheetIndex);
        foreach (var required in new[] { "Kế hoạch", "Thực tế", "Dự báo", "Ngày báo cáo", "◆" })
        {
            TestAssert.Contains(required, serialized, $"The serialized daily Gantt must retain reader-facing '{required}' evidence.");
        }
    }

    public static void ExecutiveWorkbookNearTermUsesExactThirtyDayAxisAndTruthfulContinuations()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult());
        var document = Compose(report);
        var nearTerm = ReadCollection(document, "Sheets")
            .SingleOrDefault(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "30 ngày tới", StringComparison.Ordinal));
        TestAssert.True(nearTerm is not null, "The approved workbook must contain a separate operating view for the next 30 days.");
        var nearTermSheet = nearTerm ?? throw new InvalidOperationException("The near-term worksheet was not composed.");

        var dates = ReadCollection(nearTermSheet, "Rows")
            .Select(row => ReadCollection(row, "Cells")
                .Select(cell => ReadRequiredProperty(cell, "Value"))
                .OfType<DateOnly>()
                .ToArray())
            .OrderByDescending(values => values.Length)
            .First();
        var expectedDates = Enumerable.Range(0, 30).Select(offset => new DateOnly(2026, 9, 30).AddDays(offset)).ToArray();
        TestAssert.Equal(string.Join('|', expectedDates), string.Join('|', dates), "The near-term Gantt must use exactly the source reporting date through plus 29 days as daily columns.");

        var rows = ReadCollection(nearTermSheet, "Rows");
        var cells = rows.SelectMany(row => ReadCollection(row, "Cells")).ToArray();
        var overdueMarkerRowIndex = Array.FindIndex(rows, row =>
            ReadCollection(row, "Cells").Any(cell => (Convert.ToString(ReadRequiredProperty(cell, "Value")) ?? string.Empty)
                .StartsWith("Quá hạn · ", StringComparison.Ordinal))) + 1;
        TestAssert.True(overdueMarkerRowIndex > 0, "Wholly pre-window overdue Plan must use a dated warning marker rather than a fabricated in-window Plan bar.");
        TestAssert.True(
            ReadCollection(nearTermSheet, "MergedRanges").Any(range =>
                Convert.ToInt32(ReadRequiredProperty(range, "StartRow")) == overdueMarkerRowIndex
                && Convert.ToInt32(ReadRequiredProperty(range, "StartColumn")) == 9
                && Convert.ToInt32(ReadRequiredProperty(range, "EndRow")) == overdueMarkerRowIndex
                && Convert.ToInt32(ReadRequiredProperty(range, "EndColumn")) >= 14),
            "A dated overdue marker must span several daily cells horizontally so it remains readable at 100% zoom instead of wrapping vertically in one date cell.");
        TestAssert.True(cells.Any(cell => string.Equals(ReadRequiredProperty(cell, "Value").ToString(), "◀", StringComparison.Ordinal)
            || string.Equals(ReadRequiredProperty(cell, "Value").ToString(), "▶", StringComparison.Ordinal)
            || string.Equals(ReadRequiredProperty(cell, "Value").ToString(), "◀▶", StringComparison.Ordinal)), "Intervals clipped by the 30-day view must disclose truthful continuation at the visible boundary.");
    }

    public static void ExecutiveWorkbookExplainsGanttLegendAndNearTermCounts()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult());
        var sheets = ReadCollection(Compose(report), "Sheets");
        var overview = sheets.Single(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "Tổng quan", StringComparison.Ordinal));
        var overviewHasCompleteLegend = ReadCollection(overview, "Rows")
            .Any(row => string.Equals(
                string.Join('|', ReadCollection(row, "Cells").Select(cell => Convert.ToString(ReadRequiredProperty(cell, "Value")))),
                "Kế hoạch|Thực tế|Dự báo|Ngày báo cáo",
                StringComparison.Ordinal));
        TestAssert.True(overviewHasCompleteLegend, "The overview Gantt must include one explicit legend explaining Plan, Actual, Forecast, and the reporting-date boundary.");

        var nearTerm = sheets.Single(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "30 ngày tới", StringComparison.Ordinal));
        var nearTermText = ReadCollection(nearTerm, "Rows")
            .SelectMany(row => ReadCollection(row, "Cells"))
            .Select(cell => Convert.ToString(ReadRequiredProperty(cell, "Value")) ?? string.Empty)
            .ToArray();
        TestAssert.True(nearTermText.Any(value => value.StartsWith("Hoàn thành:", StringComparison.Ordinal)), "The 30-day summary must explicitly count completed qualifying work when it is present.");
    }

    public static void ExecutiveWorkbookUsesCompactDailyColumnsAndMergedReaderContext()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult());
        var sheets = ReadCollection(Compose(report), "Sheets");
        var overview = sheets.Single(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "Tổng quan", StringComparison.Ordinal));
        var gantt = sheets.Single(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "Gantt theo ngày", StringComparison.Ordinal));
        var nearTerm = sheets.Single(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "30 ngày tới", StringComparison.Ordinal));
        var attention = sheets.Single(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "Vấn đề cần xử lý", StringComparison.Ordinal));
        var details = sheets.Single(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "Chi tiết công việc", StringComparison.Ordinal));

        foreach (var row in new[] { 2, 3, 4, 5 })
        {
            TestAssert.True(HasMergedRange(overview, row, 1, row, 8), "Overview context must span the fixed Gantt columns so project, date, planning, and source context remain readable at 100% zoom.");
        }

        foreach (var row in new[] { 7, 8, 9 })
        {
            foreach (var startColumn in new[] { 1, 3, 5, 7 })
            {
                TestAssert.True(HasMergedRange(overview, row, startColumn, row, startColumn + 1), "Overview summary must render as four readable two-column cards rather than four narrow one-column blocks.");
            }
        }

        foreach (var row in new[] { 2, 3 })
        {
            TestAssert.True(HasMergedRange(gantt, row, 1, row, 8), "Full daily Gantt context must span the fixed management columns rather than wrapping into the first narrow column.");
        }

        foreach (var row in new[] { 2, 3, 4, 6 })
        {
            TestAssert.True(HasMergedRange(nearTerm, row, 1, row, 8), "The 30-day reader context must span the fixed Gantt columns rather than wrapping into the first narrow column.");
        }

        TestAssert.True(HasMergedRange(attention, 1, 1, 1, 4) && HasMergedRange(attention, 2, 1, 2, 4), "Attention-sheet title context must use the report width rather than a single data column.");
        TestAssert.True(HasMergedRange(details, 1, 1, 1, 16) && HasMergedRange(details, 2, 1, 2, 16), "Detail-sheet title context must use the report width rather than a single data column.");

        var widths = ReadCollection(gantt, "ColumnWidths").Select(Convert.ToDouble).ToArray();
        TestAssert.True(widths.Take(8).Sum() <= 112d, "The eight fixed Gantt columns must leave a visible daily timeline at the required 100% opening zoom.");
        TestAssert.True(widths[0] >= 15d && widths[7] >= 10d, "The task identifier and lane columns must remain wide enough to avoid vertical letter-by-letter reader text.");
        TestAssert.True(widths.Skip(8).All(width => width >= 3d && width <= 3.25d), "Daily axis columns must be wide enough for two-digit typed dates to render without Excel hash marks while remaining compact for the 30-day view.");
    }

    public static void ExecutiveWorkbookKeepsTypedActualForecastAndEffortDetails()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult());
        var details = ReadCollection(Compose(report), "Sheets")
            .Single(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "Chi tiết công việc", StringComparison.Ordinal));

        var inProgress = ReadCollection(details, "Rows")
            .Single(row => RowContains(row, ExecutiveProgressTestFixtures.OpenInProgressCardId));
        var inProgressCells = ReadCollection(inProgress, "Cells");
        AssertTypedCell(inProgressCells[5], new DateOnly(2026, 9, 21), "Date", "The detail row must retain a recorded Actual start as a typed Excel date.");
        AssertEmptyUnknownCell(inProgressCells[6], "The detail row must leave an unrecorded Actual finish explicitly empty rather than inventing one.");
        AssertTypedCell(inProgressCells[7], new DateOnly(2026, 10, 6), "Date", "The detail row must retain an official forecast finish as a typed Excel date.");
        AssertTypedCell(inProgressCells[8], 1m, "Hours", "The detail row must retain actual effort as a typed Excel number with the hours format.");
        AssertTypedCell(inProgressCells[9], 7m, "Hours", "The detail row must retain remaining effort as a typed Excel number with the hours format.");
        AssertTypedCell(inProgressCells[10], 0.13m, "Percentage", "The detail row must retain the approved whole-percent, midpoint-away-from-zero progress calculation as a numeric Excel percentage.");
        TestAssert.Equal("Đã ghi nhận", ReadRequiredProperty(inProgressCells[11], "Value").ToString(), "The detail row must distinguish recorded execution evidence from a missing update.");
        TestAssert.Equal("Đang thực hiện", ReadRequiredProperty(inProgressCells[12], "Value").ToString(), "The detail row must retain the official execution state.");

        var unrecorded = ReadCollection(details, "Rows")
            .Single(row => RowContains(row, ExecutiveProgressTestFixtures.UnrecordedCardId));
        var unrecordedCells = ReadCollection(unrecorded, "Cells");
        foreach (var index in new[] { 5, 6, 7, 8, 9 })
        {
            AssertEmptyUnknownCell(unrecordedCells[index], "An unrecorded delivery card must not gain fabricated Actual, forecast, or effort detail.");
        }

        TestAssert.Equal("Chưa ghi nhận", ReadRequiredProperty(unrecordedCells[10], "Value").ToString(), "An unrecorded delivery card must show explicit unknown progress rather than a false zero percent.");
        TestAssert.Equal("Chưa ghi nhận", ReadRequiredProperty(unrecordedCells[11], "Value").ToString(), "An unrecorded delivery card must state that no official update is recorded.");
    }

    public static void ExecutiveWorkbookStatesWhenTheThirtyDayWindowIsEmpty()
    {
        var result = ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult();
        var emptyNearTerm = result with
        {
            Project = result.Project with
            {
                DeliveryCards = result.Project.DeliveryCards
                    .Select(card => card with { PlannedStart = new DateOnly(2026, 11, 1), PlannedFinish = new DateOnly(2026, 11, 2) })
                    .ToArray(),
                Milestones = result.Project.Milestones
                    .Select(milestone => milestone with { PlannedDate = new DateOnly(2026, 11, 3) })
                    .ToArray()
            },
            Analysis = result.Analysis with { Alerts = Array.Empty<ProjectManagementCompiler.Domain.Alert>() }
        };

        var report = new ExecutiveProgressReportProjector().Build(emptyNearTerm);
        TestAssert.Equal(0, report.DailyGantt.NearTermRows.Count, "The test fixture must have no eligible work before asserting the 30-day empty state.");

        var nearTermWorksheet = ExecutiveProgressTestFixtures.WorksheetText(Export(report), 3);
        TestAssert.Contains("Không có công việc quá hạn hoặc giao với cửa sổ 30 ngày.", nearTermWorksheet, "The 30-day workbook sheet must state its empty operational state explicitly.");
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
            "Gantt theo ngày",
            "30 ngày tới",
            "Vấn đề cần xử lý",
            "Chi tiết công việc");
        TestAssert.Contains("activeTab=\"0\"", ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/workbook.xml"), "The executive workbook must open on Tổng quan.");
        var overview = ExecutiveProgressTestFixtures.WorksheetText(bytes, 1);
        TestAssert.Contains("Báo cáo điều hành tiến độ", overview, "The overview must contain the approved executive report title.");
        TestAssert.Contains("Giai đoạn hiện tại", overview, "The overview must contain the current-phase summary block.");
        TestAssert.Contains("Mốc kế tiếp", overview, "The overview must contain the next-milestone summary block.");
        TestAssert.Contains("Tiến độ thực tế", overview, "The overview must contain the actual-progress summary block.");
        TestAssert.Contains("Cần quyết định", overview, "The overview must contain the readiness/decision summary block.");
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
            ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/worksheets/sheet4.xml"),
            ExecutiveProgressTestFixtures.ReadEntry(bytes, "xl/worksheets/sheet5.xml")
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

        foreach (var sheetNumber in Enumerable.Range(1, 5))
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
        var actionSheet = ExecutiveProgressTestFixtures.WorksheetText(bytes, 4);

        TestAssert.Contains("Hiện chưa có nội dung cần xin ý kiến", overview, "The overview must state the exact empty attention state.");
        TestAssert.Contains("Hiện chưa có nội dung cần xin ý kiến", actionSheet, "The action sheet must state the exact empty attention state.");
        TestAssert.False(CountRows(overview, "attention") > 5, "The overview must never show more than five attention items.");
    }

    public static void ExecutiveWorkbookContainsCompleteHierarchyAndTraceableDetails()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildOfficialFixtureResult());
        var bytes = Export(report);
        var overview = ExecutiveProgressTestFixtures.WorksheetText(bytes, 1);
        var gantt = ExecutiveProgressTestFixtures.WorksheetText(bytes, 2);
        var actions = ExecutiveProgressTestFixtures.WorksheetText(bytes, 4);
        var details = ExecutiveProgressTestFixtures.WorksheetText(bytes, 5);

        TestAssert.Contains("Kế hoạch", overview, "The overview must retain the Plan lane legend before its daily Gantt.");
        TestAssert.Contains("PH0", gantt, "The detailed Gantt must retain phase hierarchy rows.");
        TestAssert.Contains("P01", gantt, "The detailed Gantt must retain work-package hierarchy rows.");
        TestAssert.Equal(1, CountRowsWithFirstCell(bytes, 2, "P01-A"), "Each canonical delivery card must appear once as one logical task band, even when its source title repeats the identifier.");
        TestAssert.Contains("G-D0", gantt, "The detailed Gantt must retain milestone rows independently of delivery-card IDs.");
        TestAssert.Equal(
            "Mã|Hạng mục|Trạng thái|Đầu mối|% thực tế|Độ phủ|Cập nhật cuối|Làn",
            string.Join('|', ExecutiveProgressTestFixtures.WorksheetRow(bytes, 2, "Mã")),
            "The detailed daily Gantt must expose the approved fixed management columns in order.");
        TestAssert.Equal(
            "Việc cần xử lý|Ảnh hưởng|Đầu mối|Cần xong trước",
            string.Join('|', ExecutiveProgressTestFixtures.WorksheetRow(bytes, 4, "Việc cần xử lý")),
            "The action sheet must expose exactly the four reader-facing columns.");
        TestAssert.Contains("Chi tiết công việc", details, "The detail sheet must contain its reader-facing title.");
        TestAssert.Equal(
            "Công việc|Giai đoạn|Gói công việc|Bắt đầu kế hoạch|Kết thúc kế hoạch|Bắt đầu thực tế|Kết thúc thực tế|Kết thúc dự báo|Giờ thực tế|Giờ còn lại|% thực tế|Trạng thái ghi nhận|Tình trạng thực thi|Đầu mối|Cập nhật cuối|Mã tham chiếu",
            string.Join('|', ExecutiveProgressTestFixtures.WorksheetRow(bytes, 5, "Công việc")),
            "The detail sheet must expose the complete traceable delivery-card contract in order.");
        TestAssert.Equal(1, CountOccurrences(details, "P01-A"), "A raw delivery-card ID must appear only in the final reference field.");
        TestAssert.False(string.Join('|', Enumerable.Range(1, 4).Select(sheet => ExecutiveProgressTestFixtures.WorksheetText(bytes, sheet))).Contains("PMC_", StringComparison.Ordinal), "The first four sheets must not expose technical preview markers.");
    }

    public static void ExecutiveWorkbookUsesDailyLaneLabelsAndMilestoneSymbol()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildOfficialFixtureResult());
        var bytes = new ExecutiveProgressXlsxExporter().Export(report);
        var gantt = ExecutiveProgressTestFixtures.WorksheetText(bytes, 2);

        TestAssert.Contains("Kế hoạch", gantt, "The detailed Gantt must label its immutable baseline lane.");
        TestAssert.Contains("Thực tế", gantt, "The detailed Gantt must label its evidence-backed Actual lane.");
        TestAssert.Contains("◆", gantt, "The detailed Gantt must retain a distinct milestone symbol.");
        TestAssert.False(gantt.Contains("W38", StringComparison.Ordinal), "The detailed Gantt must use daily columns rather than the superseded weekly axis.");
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

        var beforeGantt = ReadCollection(Compose(before), "Sheets")
            .Single(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "Gantt theo ngày", StringComparison.Ordinal));
        var afterGantt = ReadCollection(Compose(after), "Sheets")
            .Single(sheet => string.Equals(ReadRequiredProperty(sheet, "Name").ToString(), "Gantt theo ngày", StringComparison.Ordinal));
        var beforeDates = ReadCollection(beforeGantt, "Rows")
            .SelectMany(row => ReadCollection(row, "Cells"))
            .Select(cell => ReadRequiredProperty(cell, "Value"))
            .OfType<DateOnly>()
            .ToArray();
        var afterDates = ReadCollection(afterGantt, "Rows")
            .SelectMany(row => ReadCollection(row, "Cells"))
            .Select(cell => ReadRequiredProperty(cell, "Value"))
            .OfType<DateOnly>()
            .ToArray();
        TestAssert.True(beforeDates.Contains(new DateOnly(2026, 9, 1)), "A pre-plan reporting date must remain visible as an actual daily header.");
        TestAssert.True(afterDates.Contains(new DateOnly(2027, 1, 10)), "A post-plan reporting date must remain visible as an actual daily header.");
        TestAssert.True(afterDates.Contains(new DateOnly(2027, 1, 1)), "The daily axis must include every date through an out-of-plan reporting boundary.");
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

    private static object Compose(ExecutiveProgressReport report)
    {
        var composerType = Type.GetType("ProjectManagementCompiler.Outputs.ExecutiveProgressWorkbookComposer, ProjectManagementCompiler", throwOnError: false);
        TestAssert.True(composerType is not null, "ExecutiveProgressWorkbookComposer must exist before daily-Gantt composition can be tested.");
        var composer = Activator.CreateInstance(composerType!, nonPublic: true);
        TestAssert.True(composer is not null, "ExecutiveProgressWorkbookComposer must be constructible for daily-Gantt composition tests.");
        var build = composerType!.GetMethod("Build", BindingFlags.Public | BindingFlags.Instance, [typeof(ExecutiveProgressReport)]);
        TestAssert.True(build is not null, "ExecutiveProgressWorkbookComposer must expose Build(ExecutiveProgressReport).");
        return build!.Invoke(composer, [report]) ?? throw new InvalidOperationException("The workbook composer returned no document.");
    }

    private static void AssertCellStyle(object[] cells, string value, string styleToken, string message) =>
        TestAssert.True(cells.Any(cell => string.Equals(ReadRequiredProperty(cell, "Value").ToString(), value, StringComparison.Ordinal)
            && string.Equals(ReadRequiredProperty(cell, "StyleToken").ToString(), styleToken, StringComparison.Ordinal)), message);

    private static bool RowContains(object row, string value) =>
        ReadCollection(row, "Cells")
            .Any(cell => string.Equals(ReadRequiredProperty(cell, "Value").ToString(), value, StringComparison.Ordinal));

    private static bool HasMergedRange(object worksheet, int startRow, int startColumn, int endRow, int endColumn) =>
        ReadCollection(worksheet, "MergedRanges")
            .Any(range => Convert.ToInt32(ReadRequiredProperty(range, "StartRow")) == startRow
                && Convert.ToInt32(ReadRequiredProperty(range, "StartColumn")) == startColumn
                && Convert.ToInt32(ReadRequiredProperty(range, "EndRow")) == endRow
                && Convert.ToInt32(ReadRequiredProperty(range, "EndColumn")) == endColumn);

    private static void AssertTypedCell(object cell, object expectedValue, string expectedNumberFormat, string message)
    {
        var value = ReadRequiredProperty(cell, "Value");
        TestAssert.Equal(expectedValue.GetType(), value.GetType(), message);
        TestAssert.Equal(expectedValue, value, message);
        TestAssert.Equal(expectedNumberFormat, ReadRequiredProperty(cell, "NumberFormat").ToString(), message);
    }

    private static void AssertEmptyUnknownCell(object cell, string message)
    {
        TestAssert.Equal(string.Empty, ReadRequiredProperty(cell, "Value").ToString(), message);
        TestAssert.Equal("Unknown", ReadRequiredProperty(cell, "StyleToken").ToString(), message);
        TestAssert.Equal("Text", ReadRequiredProperty(cell, "NumberFormat").ToString(), message);
    }

    private static object ReadRequiredProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        TestAssert.True(property is not null, $"{instance.GetType().Name} must expose {propertyName}.");
        var value = property!.GetValue(instance);
        TestAssert.True(value is not null, $"{instance.GetType().Name}.{propertyName} must not be null.");
        return value!;
    }

    private static object CreateProgressiveDisclosureDocument()
    {
        var cell = CreateInternal(
            "ExecutiveWorkbookCell",
            "WBS",
            ParseInternalEnum("ExecutiveWorkbookStyleToken", "Header"),
            ParseInternalEnum("ExecutiveWorkbookNumberFormat", "Text"),
            false);
        var cells = TypedArray(cell.GetType(), cell);
        var rowType = InternalOutputType("ExecutiveWorkbookRow");
        var rows = TypedArray(
            rowType,
            CreateInternal("ExecutiveWorkbookRow", cells, 2, false, true),
            CreateInternal("ExecutiveWorkbookRow", cells, 3, true, false));
        var widths = Enumerable.Repeat(12d, 12).ToArray();
        var rangeType = InternalOutputType("ExecutiveWorkbookRange");
        var worksheet = CreateInternal(
            "ExecutiveWorkbookWorksheet",
            "WBS",
            rows,
            widths,
            TypedArray(rangeType),
            CreateInternal("ExecutiveWorkbookPane", 1, 3),
            CreateInternal(
                "ExecutiveWorkbookPrintSettings",
                ParseInternalEnum("ExecutiveWorkbookPrintOrientation", "Landscape"),
                0,
                0),
            false,
            100,
            TypedArray(
                InternalOutputType("ExecutiveWorkbookColumnGroup"),
                CreateInternal("ExecutiveWorkbookColumnGroup", 9, 10, 1, true, true)),
            CreateInternal("ExecutiveWorkbookRange", 1, 1, 2, 12),
            false,
            false);

        return CreateInternal(
            "ExecutiveWorkbookDocument",
            TypedArray(InternalOutputType("ExecutiveWorkbookWorksheet"), worksheet),
            0);
    }

    private static Type InternalOutputType(string typeName) =>
        Type.GetType($"ProjectManagementCompiler.Outputs.{typeName}, ProjectManagementCompiler", throwOnError: false)
        ?? throw new InvalidOperationException($"Missing internal output type '{typeName}'.");

    private static object ParseInternalEnum(string typeName, string value) =>
        Enum.Parse(InternalOutputType(typeName), value);

    private static object CreateInternal(string typeName, params object?[] arguments) =>
        Activator.CreateInstance(
            InternalOutputType(typeName),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            args: arguments,
            culture: null)
        ?? throw new InvalidOperationException($"Could not create internal output type '{typeName}'.");

    private static Array TypedArray(Type itemType, params object[] items)
    {
        var values = Array.CreateInstance(itemType, items.Length);
        for (var index = 0; index < items.Length; index++)
        {
            values.SetValue(items[index], index);
        }

        return values;
    }

    private static void AssertInvocationThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TargetInvocationException exception) when (exception.InnerException is TException)
        {
            return;
        }

        throw new InvalidOperationException($"{message} Expected {typeof(TException).Name}.");
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

    private static int CountRowsWithFirstCell(byte[] bytes, int sheetNumber, string firstCellText) =>
        ExecutiveProgressTestFixtures.ReadXml(bytes, $"xl/worksheets/sheet{sheetNumber}.xml")
            .Descendants()
            .Where(element => element.Name.LocalName == "row")
            .Count(row => row.Descendants().FirstOrDefault(element => element.Name.LocalName == "t")?.Value == firstCellText);
}
