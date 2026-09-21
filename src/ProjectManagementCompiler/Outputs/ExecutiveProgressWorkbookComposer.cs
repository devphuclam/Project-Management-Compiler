using System.Globalization;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Outputs;

internal sealed class ExecutiveProgressWorkbookComposer
{
    public ExecutiveWorkbookDocument Build(ExecutiveProgressReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ExecutiveWorkbookDocument(
        [
            BuildOverview(report),
            BuildSchedule(report),
            BuildAttention(report),
            BuildDetails(report)
        ],
        activeSheetIndex: 0);
    }

    private static ExecutiveWorkbookWorksheet BuildOverview(ExecutiveProgressReport report)
    {
        var axis = BuildTimelineAxis(report.OverviewTimeline, report);
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("Báo cáo tiến độ", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text(report.ProjectName, ExecutiveWorkbookStyleToken.Subtitle)),
            RowOf(Text($"Ngày báo cáo: {FormatDate(report.SourceReportingDate)}", ExecutiveWorkbookStyleToken.ReportingBoundary)),
            RowOf(Text($"Khung kế hoạch: {FormatDate(report.PlanningStart)} – {FormatDate(report.PlanningFinish)}")),
            RowOf(Text(BuildProvenance(report), ExecutiveWorkbookStyleToken.Subtitle)),
            BlankRow(),
            RowOf(Text("Tình trạng lịch trình", ExecutiveWorkbookStyleToken.Header)),
            RowOf(Text($"Giai đoạn hiện tại: {report.CurrentPhase}", StyleFor(report.ScheduleCondition.Tone))),
            RowOf(Text(report.ScheduleCondition.Label, StyleFor(report.ScheduleCondition.Tone)), Text(report.ScheduleCondition.Detail, StyleFor(report.ScheduleCondition.Tone))),
            BlankRow(),
            RowOf(Text("Mốc sắp tới", ExecutiveWorkbookStyleToken.Header)),
            RowOf(Text(report.NextMilestone.DisplayName, report.NextMilestone.IsMissing ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Plan), Text(FormatDate(report.NextMilestone.PlannedDate), report.NextMilestone.IsMissing ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Plan)),
            BlankRow(),
            RowOf(Text("Tiến độ được ghi nhận", ExecutiveWorkbookStyleToken.Header)),
            RowOf(Text(report.Progress.Statement, report.Progress.RecordedPercent is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.ActualComplete)),
            RowOf(Text(BuildCounts(report.Progress))),
            BlankRow(),
            RowOf(Text("Việc cần quyết định", ExecutiveWorkbookStyleToken.Header)),
            RowOf(Text(report.ReadinessCondition.Label, StyleFor(report.ReadinessCondition.Tone)), Text(report.ReadinessCondition.Detail, StyleFor(report.ReadinessCondition.Tone))),
            BlankRow(),
            RowOf(Text("Dòng thời gian kế hoạch", ExecutiveWorkbookStyleToken.Header)),
            RowOf(
                Text("Giai đoạn / mốc", ExecutiveWorkbookStyleToken.Header),
                Text("Bắt đầu kế hoạch", ExecutiveWorkbookStyleToken.Header),
                Text("Kết thúc kế hoạch", ExecutiveWorkbookStyleToken.Header),
                Text("Tình trạng", ExecutiveWorkbookStyleToken.Header),
                Text("Đầu mối", ExecutiveWorkbookStyleToken.Header))
        };

        rows.AddRange(TimelineAxisRows(axis, fixedColumnCount: 5));
        rows.AddRange(report.OverviewTimeline.Select(row => RowOf(
            new[]
            {
                Text(row.DisplayName, row.IsCurrent ? ExecutiveWorkbookStyleToken.Plan : ExecutiveWorkbookStyleToken.Default),
                Text(FormatDate(row.PlannedStart), ExecutiveWorkbookStyleToken.Plan),
                Text(FormatDate(row.PlannedFinish), ExecutiveWorkbookStyleToken.Plan),
                Text(row.StateLabel, StateStyle(row.StateLabel)),
                Text(row.OwnerLabel)
            }
            .Concat(TimelineCells(row, axis))
            .ToArray())));

        rows.Add(BlankRow());
        rows.Add(RowOf(Text("Ngày báo cáo", ExecutiveWorkbookStyleToken.ReportingBoundary), Text(FormatDate(report.SourceReportingDate), ExecutiveWorkbookStyleToken.ReportingBoundary)));
        rows.Add(RowOf(Text("Nội dung cần xin ý kiến", ExecutiveWorkbookStyleToken.Header)));
        var attention = report.OverviewAttention.Take(5).ToArray();
        if (attention.Length == 0)
        {
            rows.Add(RowOf(Text("Hiện chưa có nội dung cần xin ý kiến", ExecutiveWorkbookStyleToken.Unknown)));
        }
        else
        {
            rows.Add(RowOf(
                Text("Việc cần xử lý", ExecutiveWorkbookStyleToken.Header),
                Text("Ảnh hưởng", ExecutiveWorkbookStyleToken.Header),
                Text("Đầu mối", ExecutiveWorkbookStyleToken.Header),
                Text("Cần xong trước", ExecutiveWorkbookStyleToken.Header)));
            rows.AddRange(attention.Select(item => RowOf(
                Text(item.Action, ExecutiveWorkbookStyleToken.Attention),
                Text(item.Impact, ExecutiveWorkbookStyleToken.Attention),
                Text(item.OwnerLabel, item.OwnerLabel == "Chưa xác định đầu mối" ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
                Text(item.DueLabel))));
        }

        return Worksheet(
            "Tổng quan",
            rows,
            new[] { 30d, 18d, 18d, 34d, 24d }.Concat(axis.WeekStarts.Select(_ => 8d)).ToArray(),
            freezeRows: 22 + (axis.WeekStarts.Count > 0 ? 2 : 0),
            freezeColumns: 1);
    }

    private static ExecutiveWorkbookWorksheet BuildSchedule(ExecutiveProgressReport report)
    {
        var axis = BuildTimelineAxis(report.WorkPackageSchedule, report);
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("Lịch trình", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text($"Ngày báo cáo: {FormatDate(report.SourceReportingDate)}", ExecutiveWorkbookStyleToken.ReportingBoundary)),
            BlankRow(),
            RowOf(
                Text("Giai đoạn", ExecutiveWorkbookStyleToken.Header),
                Text("Gói công việc", ExecutiveWorkbookStyleToken.Header),
                Text("Bắt đầu kế hoạch", ExecutiveWorkbookStyleToken.Header),
                Text("Kết thúc kế hoạch", ExecutiveWorkbookStyleToken.Header),
                Text("Tình trạng", ExecutiveWorkbookStyleToken.Header),
                Text("Đầu mối", ExecutiveWorkbookStyleToken.Header))
        };
        rows.AddRange(TimelineAxisRows(axis, fixedColumnCount: 6));
        rows.AddRange(report.WorkPackageSchedule.Select(row => RowOf(
            new[]
            {
                Text(row.PhaseDisplayName ?? "Chưa xác định"),
                Text(row.DisplayName),
                Text(FormatDate(row.PlannedStart), ExecutiveWorkbookStyleToken.Plan),
                Text(FormatDate(row.PlannedFinish), ExecutiveWorkbookStyleToken.Plan),
                Text(row.StateLabel, StateStyle(row.StateLabel)),
                Text(row.OwnerLabel, row.OwnerLabel == "Chưa xác định đầu mối" ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default)
            }
            .Concat(TimelineCells(row, axis))
            .ToArray())));
        if (report.WorkPackageSchedule.Count == 0)
        {
            rows.Add(RowOf(Text("Chưa có dữ liệu gói công việc", ExecutiveWorkbookStyleToken.Unknown)));
        }

        rows.Add(BlankRow());
        rows.Add(RowOf(Text("Ngày báo cáo", ExecutiveWorkbookStyleToken.ReportingBoundary), Text(FormatDate(report.SourceReportingDate), ExecutiveWorkbookStyleToken.ReportingBoundary)));
        return Worksheet(
            "Lịch trình",
            rows,
            new[] { 24d, 32d, 18d, 18d, 22d, 28d }.Concat(axis.WeekStarts.Select(_ => 8d)).ToArray(),
            freezeRows: 4 + (axis.WeekStarts.Count > 0 ? 2 : 0),
            freezeColumns: 2);
    }

    private static ExecutiveWorkbookWorksheet BuildAttention(ExecutiveProgressReport report)
    {
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("Vấn đề cần xử lý", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text("Tất cả nội dung có hành động hoặc quyết định cần theo dõi.", ExecutiveWorkbookStyleToken.Subtitle)),
            BlankRow(),
            RowOf(
                Text("Việc cần xử lý", ExecutiveWorkbookStyleToken.Header),
                Text("Ảnh hưởng", ExecutiveWorkbookStyleToken.Header),
                Text("Đầu mối", ExecutiveWorkbookStyleToken.Header),
                Text("Cần xong trước", ExecutiveWorkbookStyleToken.Header))
        };
        if (report.AllAttention.Count == 0)
        {
            rows.Add(RowOf(Text("Hiện chưa có nội dung cần xin ý kiến", ExecutiveWorkbookStyleToken.Unknown)));
        }
        else
        {
            rows.AddRange(report.AllAttention.Select(item => RowOf(
                Text(item.Action, ExecutiveWorkbookStyleToken.Attention),
                Text(item.Impact, ExecutiveWorkbookStyleToken.Attention),
                Text(item.OwnerLabel, item.OwnerLabel == "Chưa xác định đầu mối" ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
                Text(item.DueLabel))));
        }

        return Worksheet("Vấn đề cần xử lý", rows, new[] { 40d, 44d, 28d, 20d }, freezeRows: 4, freezeColumns: 0);
    }

    private static ExecutiveWorkbookWorksheet BuildDetails(ExecutiveProgressReport report)
    {
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("Chi tiết công việc", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text($"Ngày báo cáo: {FormatDate(report.SourceReportingDate)}", ExecutiveWorkbookStyleToken.ReportingBoundary)),
            BlankRow(),
            RowOf(
                Text("Công việc", ExecutiveWorkbookStyleToken.Header),
                Text("Giai đoạn", ExecutiveWorkbookStyleToken.Header),
                Text("Gói công việc", ExecutiveWorkbookStyleToken.Header),
                Text("Bắt đầu kế hoạch", ExecutiveWorkbookStyleToken.Header),
                Text("Kết thúc kế hoạch", ExecutiveWorkbookStyleToken.Header),
                Text("Đầu mối", ExecutiveWorkbookStyleToken.Header),
                Text("Tình trạng", ExecutiveWorkbookStyleToken.Header),
                Text("Mã tham chiếu", ExecutiveWorkbookStyleToken.Header))
        };
        rows.AddRange(report.DeliveryCardDetails.Select(detail => RowOf(
            Text(detail.Description),
            Text(detail.PhaseName),
            Text(detail.WorkPackageName),
            Text(FormatDate(detail.PlannedStart), ExecutiveWorkbookStyleToken.Plan),
            Text(FormatDate(detail.PlannedFinish), ExecutiveWorkbookStyleToken.Plan),
            Text(detail.OwnerLabel, detail.OwnerLabel == "Chưa xác định đầu mối" ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
            Text(detail.StateLabel, StateStyle(detail.StateLabel)),
            Text(detail.ReferenceCode))));
        if (report.DeliveryCardDetails.Count == 0)
        {
            rows.Add(RowOf(Text("Chưa có dữ liệu công việc", ExecutiveWorkbookStyleToken.Unknown)));
        }

        return Worksheet("Chi tiết công việc", rows, new[] { 40d, 24d, 28d, 18d, 18d, 28d, 22d, 18d }, freezeRows: 4, freezeColumns: 0);
    }

    private static ExecutiveWorkbookWorksheet Worksheet(
        string name,
        IReadOnlyList<ExecutiveWorkbookRow> rows,
        IReadOnlyList<double> widths,
        int freezeRows,
        int freezeColumns) =>
        new(
            name,
            rows,
            widths,
            Array.Empty<ExecutiveWorkbookRange>(),
            new ExecutiveWorkbookPane(freezeRows, freezeColumns),
            new ExecutiveWorkbookPrintSettings(ExecutiveWorkbookPrintOrientation.Landscape, fitToWidth: 1, fitToHeight: 0),
            showGridLines: false,
            zoomPercent: 100);

    private static IReadOnlyList<ExecutiveWorkbookRow> TimelineAxisRows(TimelineAxis axis, int fixedColumnCount)
    {
        if (axis.WeekStarts.Count == 0)
        {
            return Array.Empty<ExecutiveWorkbookRow>();
        }

        var monthCells = axis.WeekStarts
            .Select((week, index) =>
            {
                var previous = index == 0 ? (DateOnly?)null : axis.WeekStarts[index - 1];
                var isFirstWeekOfMonth = previous is null || previous.Value.Month != week.Month || previous.Value.Year != week.Year;
                return Text(isFirstWeekOfMonth ? $"Tháng {week:MM/yyyy}" : string.Empty, isFirstWeekOfMonth ? ExecutiveWorkbookStyleToken.Plan : ExecutiveWorkbookStyleToken.Default);
            })
            .ToArray();
        var weekCells = axis.WeekStarts
            .Select(week =>
            {
                var weekNumber = ISOWeek.GetWeekOfYear(week.ToDateTime(TimeOnly.MinValue));
                var marker = axis.ReportingDate >= week && axis.ReportingDate <= week.AddDays(6)
                    ? " · Ngày báo cáo"
                    : string.Empty;
                return Text($"W{weekNumber:00}{marker}", marker.Length > 0 ? ExecutiveWorkbookStyleToken.ReportingBoundary : ExecutiveWorkbookStyleToken.Default);
            })
            .ToArray();

        var monthPrefix = new[] { Text("Tháng", ExecutiveWorkbookStyleToken.Header) }
            .Concat(Enumerable.Repeat(Text(string.Empty), fixedColumnCount - 1));
        var weekPrefix = new[] { Text("Tuần ISO", ExecutiveWorkbookStyleToken.Header) }
            .Concat(Enumerable.Repeat(Text(string.Empty), fixedColumnCount - 1));
        return
        [
            RowOf(monthPrefix.Concat(monthCells).ToArray()),
            RowOf(weekPrefix.Concat(weekCells).ToArray())
        ];
    }

    private static IReadOnlyList<ExecutiveWorkbookCell> TimelineCells(ExecutiveScheduleRow row, TimelineAxis axis)
    {
        if (axis.WeekStarts.Count == 0 || row.PlannedStart is null && row.PlannedFinish is null)
        {
            return Array.Empty<ExecutiveWorkbookCell>();
        }

        var start = row.PlannedStart ?? row.PlannedFinish!.Value;
        var finish = row.PlannedFinish ?? row.PlannedStart!.Value;
        if (finish < start)
        {
            (start, finish) = (finish, start);
        }

        return axis.WeekStarts
            .Select(week =>
            {
                var weekFinish = week.AddDays(6);
                var visible = row.Kind == ExecutiveScheduleRowKind.Milestone
                    ? start >= week && start <= weekFinish
                    : start <= weekFinish && finish >= week;
                return Text(visible ? row.Kind == ExecutiveScheduleRowKind.Milestone ? "◆" : "■" : string.Empty, visible ? TimelineStyle(row) : ExecutiveWorkbookStyleToken.Default);
            })
            .ToArray();
    }

    private static TimelineAxis BuildTimelineAxis(IEnumerable<ExecutiveScheduleRow> rows, ExecutiveProgressReport report)
    {
        var dates = rows
            .SelectMany(row => new[] { row.PlannedStart, row.PlannedFinish })
            .Concat(new DateOnly?[] { report.PlanningStart, report.PlanningFinish, report.SourceReportingDate })
            .Where(date => date is not null)
            .Select(date => date!.Value)
            .ToArray();
        if (dates.Length == 0)
        {
            return new TimelineAxis(Array.Empty<DateOnly>(), report.SourceReportingDate);
        }

        var start = dates.Min();
        var finish = dates.Max();
        var weekStart = start.AddDays(-(int)start.DayOfWeek + (int)DayOfWeek.Monday);
        if (start.DayOfWeek == DayOfWeek.Sunday)
        {
            weekStart = start.AddDays(-6);
        }

        var lastWeek = finish.AddDays(-(int)finish.DayOfWeek + (int)DayOfWeek.Monday);
        if (finish.DayOfWeek == DayOfWeek.Sunday)
        {
            lastWeek = finish.AddDays(-6);
        }

        var weekStarts = new List<DateOnly>();
        for (var cursor = weekStart; cursor <= lastWeek; cursor = cursor.AddDays(7))
        {
            weekStarts.Add(cursor);
        }

        return new TimelineAxis(weekStarts, report.SourceReportingDate);
    }

    private static ExecutiveWorkbookCell Text(string value, ExecutiveWorkbookStyleToken style = ExecutiveWorkbookStyleToken.Default) =>
        new(value, style, ExecutiveWorkbookNumberFormat.Text);

    private static ExecutiveWorkbookRow RowOf(params ExecutiveWorkbookCell[] cells) => new(cells);
    private static ExecutiveWorkbookRow BlankRow() => new(Array.Empty<ExecutiveWorkbookCell>());

    private static ExecutiveWorkbookStyleToken TimelineStyle(ExecutiveScheduleRow row) =>
        row.Kind == ExecutiveScheduleRowKind.Milestone || row.IsCurrent ? ExecutiveWorkbookStyleToken.ReportingBoundary : ExecutiveWorkbookStyleToken.Plan;

    private static string BuildProvenance(ExecutiveProgressReport report) =>
        report.AnalysisAsOfDate == report.SourceReportingDate
            ? $"Nguồn chính thức IDEAEngineering · dữ liệu cập nhật đến {FormatDate(report.SourceReportingDate)}"
            : $"Nguồn chính thức IDEAEngineering · dữ liệu cập nhật đến {FormatDate(report.SourceReportingDate)} · phân tích đến {FormatDate(report.AnalysisAsOfDate)}";

    private static string BuildCounts(ExecutiveProgressSummary progress) =>
        $"Hoàn thành: {progress.CompletedCount} · Đang thực hiện: {progress.InProgressCount} · Chưa bắt đầu: {progress.NotStartedCount} · Chưa cập nhật: {progress.UnknownCount}";

    private static ExecutiveWorkbookStyleToken StateStyle(string stateLabel) => stateLabel switch
    {
        "Hoàn thành" => ExecutiveWorkbookStyleToken.ActualComplete,
        "Bị chặn" or "Trễ kế hoạch" => ExecutiveWorkbookStyleToken.BlockedOrOverdue,
        "Đang thực hiện" or "Cần xử lý" or "Cần quyết định" => ExecutiveWorkbookStyleToken.Attention,
        "Chưa cập nhật" or "Chưa đánh giá" => ExecutiveWorkbookStyleToken.Unknown,
        _ => ExecutiveWorkbookStyleToken.Default
    };

    private static ExecutiveWorkbookStyleToken StyleFor(ExecutiveConditionTone tone) => tone switch
    {
        ExecutiveConditionTone.Plan => ExecutiveWorkbookStyleToken.Plan,
        ExecutiveConditionTone.Complete => ExecutiveWorkbookStyleToken.ActualComplete,
        ExecutiveConditionTone.Attention => ExecutiveWorkbookStyleToken.Attention,
        ExecutiveConditionTone.Blocked => ExecutiveWorkbookStyleToken.BlockedOrOverdue,
        _ => ExecutiveWorkbookStyleToken.Unknown
    };

    private static string FormatDate(DateOnly? date) => date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Chưa xác định";

    private sealed record TimelineAxis(IReadOnlyList<DateOnly> WeekStarts, DateOnly ReportingDate);
}
