using System.Globalization;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Outputs;

internal sealed class ExecutiveProgressWorkbookComposer
{
    private const int DailyGanttFixedColumnCount = 8;
    private const int NearTermOverdueMarkerSpan = 6;

    public ExecutiveWorkbookDocument Build(ExecutiveProgressReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ExecutiveWorkbookDocument(
        [
            BuildOverview(report),
            BuildDailyGantt(report),
            BuildNearTerm(report),
            BuildAttention(report),
            BuildDetails(report)
        ],
        activeSheetIndex: 0);
    }

    private static ExecutiveWorkbookWorksheet BuildOverview(ExecutiveProgressReport report)
    {
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("Báo cáo điều hành tiến độ", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text(report.ProjectName, ExecutiveWorkbookStyleToken.Subtitle)),
            RowOf(Text($"Ngày báo cáo: {FormatDate(report.SourceReportingDate)}", ExecutiveWorkbookStyleToken.ReportingBoundary)),
            RowOf(Text(BuildPlanningContext(report), ExecutiveWorkbookStyleToken.Subtitle)),
            RowOf(Text(BuildProvenance(report), ExecutiveWorkbookStyleToken.Subtitle)),
            BlankRow(),
            RowOf(
                Text("Giai đoạn hiện tại", ExecutiveWorkbookStyleToken.Header),
                Text(string.Empty, ExecutiveWorkbookStyleToken.Header),
                Text("Mốc kế tiếp", ExecutiveWorkbookStyleToken.Header),
                Text(string.Empty, ExecutiveWorkbookStyleToken.Header),
                Text("Tiến độ thực tế", ExecutiveWorkbookStyleToken.Header),
                Text(string.Empty, ExecutiveWorkbookStyleToken.Header),
                Text("Cần quyết định", ExecutiveWorkbookStyleToken.Header),
                Text(string.Empty, ExecutiveWorkbookStyleToken.Header)),
            RowOf(
                Text(report.CurrentPhase, StyleFor(report.ScheduleCondition.Tone)),
                Text(string.Empty, StyleFor(report.ScheduleCondition.Tone)),
                Text(report.NextMilestone.DisplayName, report.NextMilestone.IsMissing ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Plan),
                Text(string.Empty, report.NextMilestone.IsMissing ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Plan),
                Text(report.Progress.Statement, report.Progress.RecordedPercent is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.ActualComplete),
                Text(string.Empty, report.Progress.RecordedPercent is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.ActualComplete),
                Text(report.ReadinessCondition.Label, StyleFor(report.ReadinessCondition.Tone)),
                Text(string.Empty, StyleFor(report.ReadinessCondition.Tone))),
            RowOf(
                Text(report.ScheduleCondition.Detail, StyleFor(report.ScheduleCondition.Tone)),
                Text(string.Empty, StyleFor(report.ScheduleCondition.Tone)),
                Text(FormatDate(report.NextMilestone.PlannedDate), report.NextMilestone.IsMissing ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Plan),
                Text(string.Empty, report.NextMilestone.IsMissing ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Plan),
                Text(BuildProgressEvidence(report.Progress), report.Progress.RecordedPercent is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.ActualComplete),
                Text(string.Empty, report.Progress.RecordedPercent is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.ActualComplete),
                Text(report.ReadinessCondition.Detail, StyleFor(report.ReadinessCondition.Tone)),
                Text(string.Empty, StyleFor(report.ReadinessCondition.Tone))),
            BlankRow(),
            RowOf(
                Text("Kế hoạch", ExecutiveWorkbookStyleToken.Plan),
                Text("Thực tế", ExecutiveWorkbookStyleToken.ActualComplete),
                Text("Dự báo", ExecutiveWorkbookStyleToken.Forecast),
                Text("Ngày báo cáo", ExecutiveWorkbookStyleToken.ReportingBoundary)),
            BlankRow()
        };

        var mergedRanges = new List<ExecutiveWorkbookRange>();
        AddReaderContextMerges(mergedRanges, DailyGanttFixedColumnCount, 2, 3, 4, 5);
        AddOverviewSummaryMerges(mergedRanges);
        var layout = AppendDailyGanttTable(
            rows,
            mergedRanges,
            report.DailyGantt.OverviewRows,
            report.DailyGantt.FullStart,
            report.DailyGantt.FullFinish,
            report.SourceReportingDate,
            report.AnalysisAsOfDate,
            useContinuationMarkers: false);
        rows.Add(BlankRow());
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

        mergedRanges.Add(new ExecutiveWorkbookRange(1, 1, 1, layout.TotalColumns));
        return DailyWorksheet(
            "Tổng quan",
            rows,
            layout.TotalColumns,
            mergedRanges,
            freezeRows: layout.HeaderLastRow,
            freezeColumns: DailyGanttFixedColumnCount,
            fitToWidth: 1);
    }

    private static ExecutiveWorkbookWorksheet BuildDailyGantt(ExecutiveProgressReport report)
    {
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("Gantt theo ngày", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text(report.ProjectName, ExecutiveWorkbookStyleToken.Subtitle)),
            RowOf(Text(BuildProvenance(report), ExecutiveWorkbookStyleToken.Subtitle)),
            RowOf(
                Text("Kế hoạch", ExecutiveWorkbookStyleToken.Plan),
                Text("Thực tế", ExecutiveWorkbookStyleToken.ActualComplete),
                Text("Dự báo", ExecutiveWorkbookStyleToken.Forecast),
                Text("Ngày báo cáo", ExecutiveWorkbookStyleToken.ReportingBoundary),
                Text(FormatDate(report.SourceReportingDate), ExecutiveWorkbookStyleToken.ReportingBoundary)),
            BlankRow()
        };
        var mergedRanges = new List<ExecutiveWorkbookRange>();
        AddReaderContextMerges(mergedRanges, DailyGanttFixedColumnCount, 2, 3);
        var layout = AppendDailyGanttTable(
            rows,
            mergedRanges,
            report.DailyGantt.FullRows,
            report.DailyGantt.FullStart,
            report.DailyGantt.FullFinish,
            report.SourceReportingDate,
            report.AnalysisAsOfDate,
            useContinuationMarkers: false);
        mergedRanges.Add(new ExecutiveWorkbookRange(1, 1, 1, layout.TotalColumns));

        return DailyWorksheet(
            "Gantt theo ngày",
            rows,
            layout.TotalColumns,
            mergedRanges,
            freezeRows: layout.HeaderLastRow,
            freezeColumns: DailyGanttFixedColumnCount,
            fitToWidth: 0);
    }

    private static ExecutiveWorkbookWorksheet BuildNearTerm(ExecutiveProgressReport report)
    {
        var nearTermCards = report.DailyGantt.NearTermRows
            .Where(row => row.Kind == ExecutiveDailyGanttRowKind.DeliveryCard)
            .ToArray();
        var nearTermMilestone = report.DailyGantt.NearTermRows
            .Where(row => row.Kind == ExecutiveDailyGanttRowKind.Milestone && row.IsNextMilestone)
            .OrderBy(row => row.PlannedStart)
            .ThenBy(row => row.SourceOrder)
            .FirstOrDefault();
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("30 ngày tới", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text(report.ProjectName, ExecutiveWorkbookStyleToken.Subtitle)),
            RowOf(Text($"Cửa sổ theo dõi: {FormatDate(report.DailyGantt.NearTermStart)} – {FormatDate(report.DailyGantt.NearTermFinish)}", ExecutiveWorkbookStyleToken.ReportingBoundary)),
            RowOf(Text(BuildProvenance(report), ExecutiveWorkbookStyleToken.Subtitle)),
            RowOf(
                Text($"Công việc đủ điều kiện: {nearTermCards.Length}", ExecutiveWorkbookStyleToken.Header),
                Text($"Quá hạn chưa xong: {nearTermCards.Count(row => row.IsOverdue)}", ExecutiveWorkbookStyleToken.Header),
                Text($"Đang thực hiện: {nearTermCards.Count(row => row.StateLabel == "Đang thực hiện")}", ExecutiveWorkbookStyleToken.Header),
                Text($"Hoàn thành: {nearTermCards.Count(row => row.StateLabel == "Hoàn thành")}", ExecutiveWorkbookStyleToken.Header),
                Text($"Chưa cập nhật: {nearTermCards.Count(row => row.RecordedChildCount == 0)}", ExecutiveWorkbookStyleToken.Header)),
            RowOf(Text(
                nearTermMilestone is null
                    ? "Mốc kế tiếp trong cửa sổ: Chưa xác định"
                    : $"Mốc kế tiếp trong cửa sổ: {nearTermMilestone.DisplayName} ({FormatDate(nearTermMilestone.PlannedStart)})",
                nearTermMilestone is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Plan)),
            RowOf(
                Text("Kế hoạch", ExecutiveWorkbookStyleToken.Plan),
                Text("Thực tế", ExecutiveWorkbookStyleToken.ActualComplete),
                Text("Dự báo", ExecutiveWorkbookStyleToken.Forecast),
                Text("◀ / ▶: tiếp tục ngoài cửa sổ", ExecutiveWorkbookStyleToken.Subtitle),
                Text("Ngày báo cáo", ExecutiveWorkbookStyleToken.ReportingBoundary)),
            BlankRow()
        };
        var mergedRanges = new List<ExecutiveWorkbookRange>();
        AddReaderContextMerges(mergedRanges, DailyGanttFixedColumnCount, 2, 3, 4, 6);
        var layout = AppendDailyGanttTable(
            rows,
            mergedRanges,
            report.DailyGantt.NearTermRows,
            report.DailyGantt.NearTermStart,
            report.DailyGantt.NearTermFinish,
            report.SourceReportingDate,
            report.AnalysisAsOfDate,
            useContinuationMarkers: true);
        if (report.DailyGantt.NearTermRows.Count == 0)
        {
            rows.Add(RowOf(Text("Không có công việc quá hạn hoặc giao với cửa sổ 30 ngày.", ExecutiveWorkbookStyleToken.Unknown)));
        }

        mergedRanges.Add(new ExecutiveWorkbookRange(1, 1, 1, layout.TotalColumns));
        return DailyWorksheet(
            "30 ngày tới",
            rows,
            layout.TotalColumns,
            mergedRanges,
            freezeRows: layout.HeaderLastRow,
            freezeColumns: DailyGanttFixedColumnCount,
            fitToWidth: 1);
    }

    private static DailyGanttTableLayout AppendDailyGanttTable(
        ICollection<ExecutiveWorkbookRow> rows,
        ICollection<ExecutiveWorkbookRange> mergedRanges,
        IReadOnlyList<ExecutiveDailyGanttRow> ganttRows,
        DateOnly axisStart,
        DateOnly axisFinish,
        DateOnly sourceReportingDate,
        DateOnly analysisAsOfDate,
        bool useContinuationMarkers)
    {
        var dates = BuildDailyAxis(axisStart, axisFinish);
        var monthHeaderRow = rows.Count + 1;
        rows.Add(RowOf(BuildMonthHeaderCells(dates)));
        AddMonthHeaderMerges(mergedRanges, monthHeaderRow, dates);
        rows.Add(RowOf(BuildDailyDateHeaderCells(dates, sourceReportingDate)));
        rows.Add(RowOf(BuildDailyWeekdayHeaderCells(dates, sourceReportingDate)));
        var headerLastRow = rows.Count;

        foreach (var row in ganttRows)
        {
            if (row.Kind == ExecutiveDailyGanttRowKind.Milestone)
            {
                rows.Add(RowOf(BuildMilestoneCells(row, dates, sourceReportingDate)));
                continue;
            }

            var planRow = rows.Count + 1;
            rows.Add(RowOf(BuildPlanCells(row, dates, sourceReportingDate, axisStart, axisFinish, useContinuationMarkers)));
            AddPreWindowOverdueMarkerMerge(mergedRanges, row, planRow, dates.Count, axisStart, useContinuationMarkers);
            rows.Add(RowOf(BuildActualCells(row, dates, sourceReportingDate, analysisAsOfDate, axisStart, axisFinish, useContinuationMarkers)));
            AddTaskBandMerges(mergedRanges, planRow);
        }

        return new DailyGanttTableLayout(headerLastRow, DailyGanttFixedColumnCount + dates.Count);
    }

    private static IReadOnlyList<ExecutiveWorkbookCell> BuildMonthHeaderCells(IReadOnlyList<DateOnly> dates)
    {
        var cells = new List<ExecutiveWorkbookCell>
        {
            Text("Trục thời gian", ExecutiveWorkbookStyleToken.Header)
        };
        cells.AddRange(Enumerable.Repeat(Text(string.Empty, ExecutiveWorkbookStyleToken.Header), DailyGanttFixedColumnCount - 1));
        cells.AddRange(dates.Select((date, index) =>
        {
            var previous = index == 0 ? (DateOnly?)null : dates[index - 1];
            var isFirstDateOfMonth = previous is null
                || previous.Value.Month != date.Month
                || previous.Value.Year != date.Year;
            return Text(
                isFirstDateOfMonth ? $"Tháng {date:MM/yyyy}" : string.Empty,
                ExecutiveWorkbookStyleToken.Header,
                isReportingBoundary: false);
        }));
        return cells;
    }

    private static IReadOnlyList<ExecutiveWorkbookCell> BuildDailyDateHeaderCells(
        IReadOnlyList<DateOnly> dates,
        DateOnly sourceReportingDate)
    {
        var cells = new List<ExecutiveWorkbookCell>
        {
            Text("Mã", ExecutiveWorkbookStyleToken.Header),
            Text("Hạng mục", ExecutiveWorkbookStyleToken.Header),
            Text("Trạng thái", ExecutiveWorkbookStyleToken.Header),
            Text("Đầu mối", ExecutiveWorkbookStyleToken.Header),
            Text("% thực tế", ExecutiveWorkbookStyleToken.Header),
            Text("Độ phủ", ExecutiveWorkbookStyleToken.Header),
            Text("Cập nhật cuối", ExecutiveWorkbookStyleToken.Header),
            Text("Làn", ExecutiveWorkbookStyleToken.Header)
        };
        cells.AddRange(dates.Select(date => Day(
            date,
            DailyHeaderStyle(date, sourceReportingDate),
            isReportingBoundary: date == sourceReportingDate)));
        return cells;
    }

    private static IReadOnlyList<ExecutiveWorkbookCell> BuildDailyWeekdayHeaderCells(
        IReadOnlyList<DateOnly> dates,
        DateOnly sourceReportingDate)
    {
        var cells = Enumerable.Repeat(Text(string.Empty, ExecutiveWorkbookStyleToken.Header), DailyGanttFixedColumnCount).ToList();
        cells.AddRange(dates.Select(date => Text(
            VietnameseWeekday(date),
            DailyHeaderStyle(date, sourceReportingDate),
            isReportingBoundary: date == sourceReportingDate)));
        return cells;
    }

    private static IReadOnlyList<ExecutiveWorkbookCell> BuildPlanCells(
        ExecutiveDailyGanttRow row,
        IReadOnlyList<DateOnly> dates,
        DateOnly sourceReportingDate,
        DateOnly axisStart,
        DateOnly axisFinish,
        bool useContinuationMarkers)
    {
        var cells = new List<ExecutiveWorkbookCell>
        {
            Text(row.ReferenceCode, HierarchyStyle(row)),
            Text(HierarchyDisplayName(row), HierarchyStyle(row)),
            Text(row.StateLabel, RowStateStyle(row)),
            Text(row.OwnerLabel, row.OwnerLabel == "Chưa xác định đầu mối" ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
            ProgressCell(row),
            Text(row.CoverageLabel, row.RecordedChildCount == row.TotalChildCount ? ExecutiveWorkbookStyleToken.Default : ExecutiveWorkbookStyleToken.Unknown),
            Text(FormatUpdate(row.LastOfficialUpdate), row.LastOfficialUpdate is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
            Text("Kế hoạch", ExecutiveWorkbookStyleToken.Plan)
        };
        cells.AddRange(dates.Select(date => TimelinePlanCell(row, date, sourceReportingDate, axisStart, axisFinish, useContinuationMarkers)));
        return cells;
    }

    private static IReadOnlyList<ExecutiveWorkbookCell> BuildActualCells(
        ExecutiveDailyGanttRow row,
        IReadOnlyList<DateOnly> dates,
        DateOnly sourceReportingDate,
        DateOnly analysisAsOfDate,
        DateOnly axisStart,
        DateOnly axisFinish,
        bool useContinuationMarkers)
    {
        var laneStyle = row.ActualDisplayThrough is not null
            ? ExecutiveWorkbookStyleToken.ActualComplete
            : row.ForecastFinish is not null && row.ForecastFinish > analysisAsOfDate
                ? ExecutiveWorkbookStyleToken.Forecast
                : ExecutiveWorkbookStyleToken.Unknown;
        var cells = new List<ExecutiveWorkbookCell>
        {
            Text(string.Empty),
            Text(string.Empty),
            Text(string.Empty),
            Text(string.Empty),
            Text(string.Empty),
            Text(string.Empty),
            Text(string.Empty),
            Text("Thực tế", laneStyle)
        };
        cells.AddRange(dates.Select(date => TimelineActualCell(row, date, sourceReportingDate, analysisAsOfDate, axisStart, axisFinish, useContinuationMarkers)));
        return cells;
    }

    private static IReadOnlyList<ExecutiveWorkbookCell> BuildMilestoneCells(
        ExecutiveDailyGanttRow row,
        IReadOnlyList<DateOnly> dates,
        DateOnly sourceReportingDate)
    {
        var cells = new List<ExecutiveWorkbookCell>
        {
            Text(row.ReferenceCode, ExecutiveWorkbookStyleToken.Milestone),
            Text(HierarchyDisplayName(row), ExecutiveWorkbookStyleToken.Milestone),
            Text(row.StateLabel, RowStateStyle(row)),
            Text(row.OwnerLabel, row.OwnerLabel == "Chưa xác định đầu mối" ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
            Text("Chưa đủ dữ liệu", ExecutiveWorkbookStyleToken.Unknown),
            Text(row.CoverageLabel, ExecutiveWorkbookStyleToken.Unknown),
            Text(FormatUpdate(row.LastOfficialUpdate), ExecutiveWorkbookStyleToken.Unknown),
            Text("Mốc", ExecutiveWorkbookStyleToken.Milestone)
        };
        cells.AddRange(dates.Select(date =>
            IsWithin(date, row.PlannedStart, row.PlannedFinish)
                ? Text("◆", ExecutiveWorkbookStyleToken.Milestone, isReportingBoundary: date == sourceReportingDate)
                : TimelineBackgroundCell(date, sourceReportingDate)));
        return cells;
    }

    private static ExecutiveWorkbookCell TimelinePlanCell(
        ExecutiveDailyGanttRow row,
        DateOnly date,
        DateOnly sourceReportingDate,
        DateOnly axisStart,
        DateOnly axisFinish,
        bool useContinuationMarkers)
    {
        if (IsWithin(date, row.PlannedStart, row.PlannedFinish))
        {
            return Text(
                useContinuationMarkers ? ContinuationMarker(date, row.PlannedStart, row.PlannedFinish, axisStart, axisFinish) : string.Empty,
                ExecutiveWorkbookStyleToken.Plan,
                isReportingBoundary: date == sourceReportingDate);
        }

        if (HasPreWindowOverdueMarker(row, axisStart, useContinuationMarkers)
            && date == axisStart)
        {
            return Text($"Quá hạn · {row.PlannedFinish:dd/MM}", ExecutiveWorkbookStyleToken.BlockedOrOverdue, isReportingBoundary: date == sourceReportingDate);
        }

        return TimelineBackgroundCell(date, sourceReportingDate);
    }

    private static ExecutiveWorkbookCell TimelineActualCell(
        ExecutiveDailyGanttRow row,
        DateOnly date,
        DateOnly sourceReportingDate,
        DateOnly analysisAsOfDate,
        DateOnly axisStart,
        DateOnly axisFinish,
        bool useContinuationMarkers)
    {
        if (IsWithin(date, row.ActualStart, row.ActualDisplayThrough))
        {
            return Text(
                useContinuationMarkers ? ContinuationMarker(date, row.ActualStart, row.ActualDisplayThrough, axisStart, axisFinish) : string.Empty,
                ExecutiveWorkbookStyleToken.ActualComplete,
                isReportingBoundary: date == sourceReportingDate);
        }

        var forecastStart = analysisAsOfDate.AddDays(1);
        if (row.ForecastFinish is not null
            && row.ForecastFinish > analysisAsOfDate
            && date >= forecastStart
            && date <= row.ForecastFinish)
        {
            var continuation = useContinuationMarkers
                ? ContinuationMarker(date, forecastStart, row.ForecastFinish, axisStart, axisFinish)
                : string.Empty;
            return Text(
                continuation.Length > 0 ? continuation : date == forecastStart || date == axisStart ? "Dự báo" : string.Empty,
                ExecutiveWorkbookStyleToken.Forecast,
                isReportingBoundary: date == sourceReportingDate);
        }

        return TimelineBackgroundCell(date, sourceReportingDate);
    }

    private static string ContinuationMarker(
        DateOnly date,
        DateOnly? start,
        DateOnly? finish,
        DateOnly axisStart,
        DateOnly axisFinish)
    {
        if (start is null || finish is null)
        {
            return string.Empty;
        }

        var continuesLeft = date == axisStart && start < axisStart;
        var continuesRight = date == axisFinish && finish > axisFinish;
        return continuesLeft && continuesRight ? "◀▶" : continuesLeft ? "◀" : continuesRight ? "▶" : string.Empty;
    }

    private static ExecutiveWorkbookCell TimelineBackgroundCell(DateOnly date, DateOnly sourceReportingDate) =>
        Text(
            string.Empty,
            IsWeekend(date) ? ExecutiveWorkbookStyleToken.Weekend : ExecutiveWorkbookStyleToken.Default,
            isReportingBoundary: date == sourceReportingDate);

    private static ExecutiveWorkbookCell ProgressCell(ExecutiveDailyGanttRow row) =>
        row.ProgressPercent is null
            ? Text("Chưa đủ dữ liệu", ExecutiveWorkbookStyleToken.Unknown)
            : Number(row.ProgressPercent.Value / 100m, ExecutiveWorkbookStyleToken.ActualComplete, ExecutiveWorkbookNumberFormat.Percentage);

    private static ExecutiveWorkbookStyleToken DailyHeaderStyle(DateOnly date, DateOnly sourceReportingDate) =>
        date == sourceReportingDate
            ? ExecutiveWorkbookStyleToken.ReportingBoundary
            : IsWeekend(date)
                ? ExecutiveWorkbookStyleToken.Weekend
                : ExecutiveWorkbookStyleToken.Header;

    private static ExecutiveWorkbookStyleToken RowStateStyle(ExecutiveDailyGanttRow row) =>
        row.IsBlocked || row.IsOverdue ? ExecutiveWorkbookStyleToken.BlockedOrOverdue : StateStyle(row.StateLabel);

    private static ExecutiveWorkbookStyleToken HierarchyStyle(ExecutiveDailyGanttRow row) => row.Kind switch
    {
        ExecutiveDailyGanttRowKind.Project => ExecutiveWorkbookStyleToken.ProjectHierarchy,
        ExecutiveDailyGanttRowKind.Phase => ExecutiveWorkbookStyleToken.PhaseHierarchy,
        ExecutiveDailyGanttRowKind.WorkPackage => ExecutiveWorkbookStyleToken.WorkPackageHierarchy,
        ExecutiveDailyGanttRowKind.DeliveryCard => ExecutiveWorkbookStyleToken.DeliveryCardHierarchy,
        ExecutiveDailyGanttRowKind.Milestone => ExecutiveWorkbookStyleToken.Milestone,
        _ => ExecutiveWorkbookStyleToken.Default
    };

    private static string HierarchyDisplayName(ExecutiveDailyGanttRow row)
    {
        var prefix = row.Kind switch
        {
            ExecutiveDailyGanttRowKind.Project => string.Empty,
            ExecutiveDailyGanttRowKind.Phase => "› ",
            ExecutiveDailyGanttRowKind.WorkPackage => "  › ",
            ExecutiveDailyGanttRowKind.DeliveryCard => "    · ",
            ExecutiveDailyGanttRowKind.Milestone => "  ◆ ",
            _ => string.Empty
        };
        return prefix + row.DisplayName;
    }

    private static IReadOnlyList<DateOnly> BuildDailyAxis(DateOnly start, DateOnly finish)
    {
        if (finish < start)
        {
            throw new InvalidOperationException("A daily Gantt axis must end on or after its start date.");
        }

        return Enumerable.Range(0, finish.DayNumber - start.DayNumber + 1)
            .Select(start.AddDays)
            .ToArray();
    }

    private static void AddMonthHeaderMerges(
        ICollection<ExecutiveWorkbookRange> mergedRanges,
        int monthHeaderRow,
        IReadOnlyList<DateOnly> dates)
    {
        mergedRanges.Add(new ExecutiveWorkbookRange(
            monthHeaderRow,
            1,
            monthHeaderRow,
            DailyGanttFixedColumnCount));

        var startIndex = 0;
        while (startIndex < dates.Count)
        {
            var endIndex = startIndex;
            while (endIndex + 1 < dates.Count
                && dates[endIndex + 1].Month == dates[startIndex].Month
                && dates[endIndex + 1].Year == dates[startIndex].Year)
            {
                endIndex++;
            }

            if (endIndex > startIndex)
            {
                mergedRanges.Add(new ExecutiveWorkbookRange(
                    monthHeaderRow,
                    DailyGanttFixedColumnCount + startIndex + 1,
                    monthHeaderRow,
                    DailyGanttFixedColumnCount + endIndex + 1));
            }

            startIndex = endIndex + 1;
        }
    }

    private static void AddTaskBandMerges(ICollection<ExecutiveWorkbookRange> mergedRanges, int planRow)
    {
        foreach (var column in new[] { 1, 2, 3, 4, 5, 6, 7 })
        {
            mergedRanges.Add(new ExecutiveWorkbookRange(planRow, column, planRow + 1, column));
        }
    }

    private static void AddPreWindowOverdueMarkerMerge(
        ICollection<ExecutiveWorkbookRange> mergedRanges,
        ExecutiveDailyGanttRow row,
        int planRow,
        int dateCount,
        DateOnly axisStart,
        bool useContinuationMarkers)
    {
        if (!HasPreWindowOverdueMarker(row, axisStart, useContinuationMarkers))
        {
            return;
        }

        var markerStartColumn = DailyGanttFixedColumnCount + 1;
        var markerEndColumn = markerStartColumn + Math.Min(NearTermOverdueMarkerSpan, dateCount) - 1;
        mergedRanges.Add(new ExecutiveWorkbookRange(planRow, markerStartColumn, planRow, markerEndColumn));
    }

    private static bool HasPreWindowOverdueMarker(
        ExecutiveDailyGanttRow row,
        DateOnly axisStart,
        bool useContinuationMarkers) =>
        useContinuationMarkers
        && row.IsOverdue
        && row.PlannedFinish is not null
        && row.PlannedFinish < axisStart;

    private static void AddReaderContextMerges(
        ICollection<ExecutiveWorkbookRange> mergedRanges,
        int lastColumn,
        params int[] rows)
    {
        foreach (var row in rows)
        {
            mergedRanges.Add(new ExecutiveWorkbookRange(row, 1, row, lastColumn));
        }
    }

    private static void AddOverviewSummaryMerges(ICollection<ExecutiveWorkbookRange> mergedRanges)
    {
        foreach (var row in new[] { 7, 8, 9 })
        {
            for (var startColumn = 1; startColumn <= 7; startColumn += 2)
            {
                mergedRanges.Add(new ExecutiveWorkbookRange(row, startColumn, row, startColumn + 1));
            }
        }
    }

    private static bool IsWithin(DateOnly date, DateOnly? start, DateOnly? finish)
    {
        if (start is null && finish is null)
        {
            return false;
        }

        var effectiveStart = start ?? finish!.Value;
        var effectiveFinish = finish ?? start!.Value;
        return effectiveStart <= effectiveFinish && date >= effectiveStart && date <= effectiveFinish;
    }

    private static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static string VietnameseWeekday(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        _ => "CN"
    };

    private static ExecutiveWorkbookWorksheet DailyWorksheet(
        string name,
        IReadOnlyList<ExecutiveWorkbookRow> rows,
        int totalColumns,
        IReadOnlyList<ExecutiveWorkbookRange> mergedRanges,
        int freezeRows,
        int freezeColumns,
        int fitToWidth) =>
        new(
            name,
            rows,
            new[] { 15d, 29d, 14d, 12d, 11d, 11d, 10d, 10d }
                .Concat(Enumerable.Repeat(3d, totalColumns - DailyGanttFixedColumnCount))
                .ToArray(),
            mergedRanges,
            new ExecutiveWorkbookPane(freezeRows, freezeColumns),
            new ExecutiveWorkbookPrintSettings(ExecutiveWorkbookPrintOrientation.Landscape, fitToWidth, fitToHeight: 0),
            showGridLines: false,
            zoomPercent: 100);

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

        return Worksheet(
            "Vấn đề cần xử lý",
            rows,
            new[] { 40d, 44d, 28d, 20d },
            freezeRows: 4,
            freezeColumns: 0,
            mergedRanges:
            [
                new ExecutiveWorkbookRange(1, 1, 1, 4),
                new ExecutiveWorkbookRange(2, 1, 2, 4)
            ]);
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
                Text("Bắt đầu thực tế", ExecutiveWorkbookStyleToken.Header),
                Text("Kết thúc thực tế", ExecutiveWorkbookStyleToken.Header),
                Text("Kết thúc dự báo", ExecutiveWorkbookStyleToken.Header),
                Text("Giờ thực tế", ExecutiveWorkbookStyleToken.Header),
                Text("Giờ còn lại", ExecutiveWorkbookStyleToken.Header),
                Text("% thực tế", ExecutiveWorkbookStyleToken.Header),
                Text("Trạng thái ghi nhận", ExecutiveWorkbookStyleToken.Header),
                Text("Tình trạng thực thi", ExecutiveWorkbookStyleToken.Header),
                Text("Đầu mối", ExecutiveWorkbookStyleToken.Header),
                Text("Cập nhật cuối", ExecutiveWorkbookStyleToken.Header),
                Text("Mã tham chiếu", ExecutiveWorkbookStyleToken.Header))
        };
        rows.AddRange(report.DeliveryCardDetails.Select(detail => RowOf(
            Text(detail.Description),
            Text(detail.PhaseName),
            Text(detail.WorkPackageName),
            DateCell(detail.PlannedStart, ExecutiveWorkbookStyleToken.Plan),
            DateCell(detail.PlannedFinish, ExecutiveWorkbookStyleToken.Plan),
            DateCell(detail.ActualStart, ExecutiveWorkbookStyleToken.ActualComplete),
            DateCell(detail.ActualFinish, ExecutiveWorkbookStyleToken.ActualComplete),
            DateCell(detail.ForecastFinish, ExecutiveWorkbookStyleToken.Forecast),
            HoursCell(detail.ActualEffortHours),
            HoursCell(detail.RemainingEffortHours),
            detail.ProgressPercent is null
                ? Text(detail.ProgressLabel, ExecutiveWorkbookStyleToken.Unknown)
                : Number(detail.ProgressPercent.Value / 100m, ExecutiveWorkbookStyleToken.ActualComplete, ExecutiveWorkbookNumberFormat.Percentage),
            Text(detail.RecordingLabel, detail.RecordingLabel == "Đã ghi nhận" ? ExecutiveWorkbookStyleToken.ActualComplete : ExecutiveWorkbookStyleToken.Unknown),
            Text(detail.StateLabel, StateStyle(detail.StateLabel)),
            Text(detail.OwnerLabel, detail.OwnerLabel == "Chưa xác định đầu mối" ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
            Text(FormatUpdate(detail.LastOfficialUpdate), detail.LastOfficialUpdate is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
            Text(detail.ReferenceCode))));
        if (report.DeliveryCardDetails.Count == 0)
        {
            rows.Add(RowOf(Text("Chưa có dữ liệu công việc", ExecutiveWorkbookStyleToken.Unknown)));
        }

        return Worksheet(
            "Chi tiết công việc",
            rows,
            new[] { 40d, 24d, 28d, 15d, 15d, 15d, 15d, 15d, 14d, 14d, 14d, 18d, 22d, 24d, 20d, 18d },
            freezeRows: 4,
            freezeColumns: 1,
            mergedRanges:
            [
                new ExecutiveWorkbookRange(1, 1, 1, 16),
                new ExecutiveWorkbookRange(2, 1, 2, 16)
            ]);
    }

    private static ExecutiveWorkbookWorksheet Worksheet(
        string name,
        IReadOnlyList<ExecutiveWorkbookRow> rows,
        IReadOnlyList<double> widths,
        int freezeRows,
        int freezeColumns,
        IReadOnlyList<ExecutiveWorkbookRange>? mergedRanges = null) =>
        new(
            name,
            rows,
            widths,
            mergedRanges ?? Array.Empty<ExecutiveWorkbookRange>(),
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

    private static ExecutiveWorkbookCell Text(
        string value,
        ExecutiveWorkbookStyleToken style = ExecutiveWorkbookStyleToken.Default,
        bool isReportingBoundary = false) =>
        new(value, style, ExecutiveWorkbookNumberFormat.Text, isReportingBoundary);

    private static ExecutiveWorkbookCell Day(
        DateOnly value,
        ExecutiveWorkbookStyleToken style,
        bool isReportingBoundary) =>
        new(value, style, ExecutiveWorkbookNumberFormat.DayOfMonth, isReportingBoundary);

    private static ExecutiveWorkbookCell Number(
        decimal value,
        ExecutiveWorkbookStyleToken style,
        ExecutiveWorkbookNumberFormat format) =>
        new(value, style, format);

    private static ExecutiveWorkbookCell DateCell(DateOnly? value, ExecutiveWorkbookStyleToken style) =>
        value is null
            ? Text(string.Empty, ExecutiveWorkbookStyleToken.Unknown)
            : new ExecutiveWorkbookCell(value.Value, style, ExecutiveWorkbookNumberFormat.Date);

    private static ExecutiveWorkbookCell HoursCell(decimal? value) =>
        value is null
            ? Text(string.Empty, ExecutiveWorkbookStyleToken.Unknown)
            : Number(value.Value, ExecutiveWorkbookStyleToken.Default, ExecutiveWorkbookNumberFormat.Hours);

    private static ExecutiveWorkbookRow RowOf(params ExecutiveWorkbookCell[] cells) => new(cells);
    private static ExecutiveWorkbookRow RowOf(IReadOnlyList<ExecutiveWorkbookCell> cells) => new(cells);
    private static ExecutiveWorkbookRow BlankRow() => new(Array.Empty<ExecutiveWorkbookCell>());

    private static ExecutiveWorkbookStyleToken TimelineStyle(ExecutiveScheduleRow row) =>
        row.Kind == ExecutiveScheduleRowKind.Milestone || row.IsCurrent ? ExecutiveWorkbookStyleToken.ReportingBoundary : ExecutiveWorkbookStyleToken.Plan;

    private static string BuildProvenance(ExecutiveProgressReport report) =>
        report.AnalysisAsOfDate == report.SourceReportingDate
            ? $"Nguồn chính thức IDEAEngineering · dữ liệu cập nhật đến {FormatDate(report.SourceReportingDate)}"
            : $"Nguồn chính thức IDEAEngineering · dữ liệu cập nhật đến {FormatDate(report.SourceReportingDate)} · phân tích đến {FormatDate(report.AnalysisAsOfDate)}";

    private static string BuildPlanningContext(ExecutiveProgressReport report) =>
        $"Khung kế hoạch: {FormatDate(report.PlanningStart)} – {FormatDate(report.PlanningFinish)}";

    private static string BuildProgressEvidence(ExecutiveProgressSummary progress)
    {
        var effort = progress.ActualEffortHours is not null && progress.RemainingEffortHours is not null
            ? $"Nỗ lực: {progress.ActualEffortHours:0.##} giờ thực tế / {progress.RemainingEffortHours:0.##} giờ còn lại"
            : "Nỗ lực: Chưa đủ dữ liệu";
        return $"{effort} · Độ phủ ghi nhận {progress.RecordedCardCount}/{progress.TotalCardCount} · Đủ effort {progress.ProgressEligibleCardCount}/{progress.TotalCardCount} · Cập nhật {FormatUpdate(progress.LastOfficialUpdate)}";
    }

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

    private static string FormatUpdate(DateTimeOffset? update) =>
        update?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "Chưa cập nhật";

    private sealed record TimelineAxis(IReadOnlyList<DateOnly> WeekStarts, DateOnly ReportingDate);
    private sealed record DailyGanttTableLayout(int HeaderLastRow, int TotalColumns);
}
