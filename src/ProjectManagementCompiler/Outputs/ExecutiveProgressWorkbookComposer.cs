using System.Globalization;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Outputs;

internal sealed class ExecutiveProgressWorkbookComposer
{
    private const int DailyGanttFixedColumnCount = 8;
    private const int OverviewFixedColumnCount = 5;
    private const int MaxExecutiveOperatingItems = 7;
    private const int NearTermOverdueMarkerSpan = 6;

    public ExecutiveWorkbookDocument Build(ExecutiveProgressReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ExecutiveWorkbookDocument(
        [
            BuildOverview(report),
            BuildOperating(report),
            BuildDailyGantt(report),
            BuildWbs(report),
            BuildDetails(report),
            BuildMetadata(report)
        ],
        activeSheetIndex: 0);
    }

    private static ExecutiveWorkbookWorksheet BuildOverview(ExecutiveProgressReport report)
    {
        var buckets = BuildCompactOverviewAxis(report);
        var totalColumns = OverviewFixedColumnCount + buckets.Count;
        var mergedRanges = new List<ExecutiveWorkbookRange>();
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("Báo cáo điều hành tiến độ", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text(report.ProjectName, ExecutiveWorkbookStyleToken.Subtitle)),
            RowOf(Text($"Cập nhật đến {FormatDate(report.SourceReportingDate)}", ExecutiveWorkbookStyleToken.ReportingBoundary)),
            RowOf(Text(BuildPlanningContext(report), ExecutiveWorkbookStyleToken.Subtitle)),
            BlankRow()
        };

        foreach (var row in new[] { 1, 2, 3, 4 })
        {
            mergedRanges.Add(new ExecutiveWorkbookRange(row, 1, row, totalColumns));
        }

        var summarySpans = BuildBalancedSpans(totalColumns, 5);
        AddSpannedRow(
            rows,
            mergedRanges,
            totalColumns,
            summarySpans,
            [
                Text("Vị trí hiện tại", ExecutiveWorkbookStyleToken.Header),
                Text("Độ phủ ghi nhận", ExecutiveWorkbookStyleToken.Header),
                Text("Tiến độ lịch", ExecutiveWorkbookStyleToken.Header),
                Text("Mốc kế tiếp", ExecutiveWorkbookStyleToken.Header),
                Text("Điều kiện mở cổng", ExecutiveWorkbookStyleToken.Header)
            ]);
        AddSpannedRow(
            rows,
            mergedRanges,
            totalColumns,
            summarySpans,
            [
                Text(report.CurrentPhase, ExecutiveWorkbookStyleToken.Default),
                Text(report.Progress.Statement, report.Progress.RecordedPercent is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.ActualComplete),
                Text(report.ScheduleCondition.Label, StyleFor(report.ScheduleCondition.Tone)),
                Text(report.NextMilestone.DisplayName, report.NextMilestone.IsMissing ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Plan),
                Text(report.ReadinessCondition.Label, StyleFor(report.ReadinessCondition.Tone))
            ]);
        AddSpannedRow(
            rows,
            mergedRanges,
            totalColumns,
            summarySpans,
            [
                Text(BuildCounts(report.Progress), ExecutiveWorkbookStyleToken.Default),
                Text(BuildProgressSummaryDetail(report.Progress), report.Progress.RecordedPercent is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.ActualComplete),
                Text(report.ScheduleCondition.Detail, StyleFor(report.ScheduleCondition.Tone)),
                Text(FormatDate(report.NextMilestone.PlannedDate), report.NextMilestone.IsMissing ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Plan),
                Text(report.ReadinessCondition.Detail, StyleFor(report.ReadinessCondition.Tone))
            ]);

        rows.Add(BlankRow());
        var timelineTitleRow = rows.Count + 1;
        rows.Add(RowOf(Text("Tiến độ giai đoạn và mốc", ExecutiveWorkbookStyleToken.Header)));
        mergedRanges.Add(new ExecutiveWorkbookRange(timelineTitleRow, 1, timelineTitleRow, totalColumns));
        var timelineNoteRow = rows.Count + 1;
        rows.Add(RowOf(Text("Gantt tóm tắt theo giai đoạn; mỗi cột là một tuần. Kế hoạch và thực tế từng công việc nằm ở sheet Gantt.", ExecutiveWorkbookStyleToken.Subtitle)));
        mergedRanges.Add(new ExecutiveWorkbookRange(timelineNoteRow, 1, timelineNoteRow, totalColumns));

        var axisTitleCells = Enumerable.Repeat(Text(string.Empty, ExecutiveWorkbookStyleToken.Header), totalColumns).ToArray();
        axisTitleCells[0] = Text("Hạng mục", ExecutiveWorkbookStyleToken.Header);
        axisTitleCells[3] = Text("Bắt đầu", ExecutiveWorkbookStyleToken.Header);
        axisTitleCells[4] = Text("Kết thúc", ExecutiveWorkbookStyleToken.Header);
        axisTitleCells[OverviewFixedColumnCount] = Text(buckets.Count > 0 && buckets[0].IsMonthly ? "Tháng" : "Tuần", ExecutiveWorkbookStyleToken.Header);
        var axisTitleRow = rows.Count + 1;
        rows.Add(RowOf(axisTitleCells));
        mergedRanges.Add(new ExecutiveWorkbookRange(axisTitleRow, 1, axisTitleRow, 3));
        if (buckets.Count > 1)
        {
            mergedRanges.Add(new ExecutiveWorkbookRange(axisTitleRow, OverviewFixedColumnCount + 1, axisTitleRow, totalColumns));
        }

        var axisDateCells = Enumerable.Repeat(Text(string.Empty, ExecutiveWorkbookStyleToken.Header), totalColumns).ToArray();
        for (var index = 0; index < buckets.Count; index++)
        {
            var bucket = buckets[index];
            axisDateCells[OverviewFixedColumnCount + index] = Text(
                bucket.Label,
                bucket.Contains(report.SourceReportingDate) ? ExecutiveWorkbookStyleToken.ReportingBoundary : ExecutiveWorkbookStyleToken.Header,
                isReportingBoundary: bucket.Contains(report.SourceReportingDate));
        }

        var axisDateRow = rows.Count + 1;
        rows.Add(RowOf(axisDateCells));
        mergedRanges.Add(new ExecutiveWorkbookRange(axisDateRow, 1, axisDateRow, 3));

        foreach (var item in report.OverviewTimeline)
        {
            var cells = Enumerable.Repeat(Text(string.Empty), totalColumns).ToArray();
            cells[0] = Text(item.DisplayName, item.IsCurrent ? ExecutiveWorkbookStyleToken.ReportingBoundary : TimelineStyle(item));
            cells[3] = DateCell(item.PlannedStart, ExecutiveWorkbookStyleToken.Plan);
            cells[4] = DateCell(item.PlannedFinish, ExecutiveWorkbookStyleToken.Plan);
            for (var index = 0; index < buckets.Count; index++)
            {
                var bucket = buckets[index];
                var isBoundary = bucket.Contains(report.SourceReportingDate);
                if (item.Kind == ExecutiveScheduleRowKind.Milestone
                    && item.PlannedStart is not null
                    && bucket.Contains(item.PlannedStart.Value))
                {
                    cells[OverviewFixedColumnCount + index] = Text("◆", ExecutiveWorkbookStyleToken.Milestone, isBoundary);
                }
                else if (item.Kind == ExecutiveScheduleRowKind.Phase
                    && bucket.Intersects(item.PlannedStart, item.PlannedFinish))
                {
                    cells[OverviewFixedColumnCount + index] = Text("■", ExecutiveWorkbookStyleToken.Plan, isBoundary);
                }
                else
                {
                    cells[OverviewFixedColumnCount + index] = Text(string.Empty, ExecutiveWorkbookStyleToken.Default, isBoundary);
                }
            }

            var scheduleRow = rows.Count + 1;
            rows.Add(RowOf(cells));
            mergedRanges.Add(new ExecutiveWorkbookRange(scheduleRow, 1, scheduleRow, 3));
        }

        rows.Add(RowOf(
            Text("Kế hoạch giai đoạn", ExecutiveWorkbookStyleToken.Plan),
            Text("◆ Mốc", ExecutiveWorkbookStyleToken.Milestone),
            Text("Tuần báo cáo", ExecutiveWorkbookStyleToken.ReportingBoundary),
            Text("Chi tiết kế hoạch / thực tế: xem sheet Gantt", ExecutiveWorkbookStyleToken.Subtitle)));
        rows.Add(BlankRow());
        var attentionTitleRow = rows.Count + 1;
        rows.Add(RowOf(Text("Nội dung cần xin ý kiến", ExecutiveWorkbookStyleToken.Header)));
        mergedRanges.Add(new ExecutiveWorkbookRange(attentionTitleRow, 1, attentionTitleRow, totalColumns));
        var attention = report.OverviewAttention.Take(5).ToArray();
        if (attention.Length == 0)
        {
            var emptyRow = rows.Count + 1;
            rows.Add(RowOf(Text("Hiện chưa có nội dung cần xin ý kiến", ExecutiveWorkbookStyleToken.Unknown)));
            mergedRanges.Add(new ExecutiveWorkbookRange(emptyRow, 1, emptyRow, totalColumns));
        }
        else
        {
            var attentionSpans = BuildAttentionSpans(totalColumns);
            AddSpannedRow(
                rows,
                mergedRanges,
                totalColumns,
                attentionSpans,
                [
                    Text("Việc cần xử lý", ExecutiveWorkbookStyleToken.Header),
                    Text("Ảnh hưởng", ExecutiveWorkbookStyleToken.Header),
                    Text("Đầu mối", ExecutiveWorkbookStyleToken.Header),
                    Text("Cần xong trước", ExecutiveWorkbookStyleToken.Header)
                ]);
            foreach (var item in attention)
            {
                AddSpannedRow(
                    rows,
                    mergedRanges,
                    totalColumns,
                    attentionSpans,
                    [
                        Text(item.Action, ExecutiveWorkbookStyleToken.Attention),
                        Text(item.Impact, ExecutiveWorkbookStyleToken.Attention),
                        Text(item.OwnerLabel, item.OwnerLabel == ReaderFacingTextPolicy.MissingOwnerLabel ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
                        Text(item.DueLabel)
                    ]);
            }
        }

        return new ExecutiveWorkbookWorksheet(
            "Tổng quan",
            rows,
            new[] { 18d, 18d, 18d, 12d, 12d }
                .Concat(Enumerable.Repeat(buckets.Count > 0 && buckets[0].IsMonthly ? 8d : 6d, buckets.Count))
                .ToArray(),
            mergedRanges,
            new ExecutiveWorkbookPane(axisDateRow, OverviewFixedColumnCount),
            new ExecutiveWorkbookPrintSettings(ExecutiveWorkbookPrintOrientation.Landscape, fitToWidth: 1, fitToHeight: 0),
            showGridLines: false,
            zoomPercent: 100);
    }

    private static ExecutiveWorkbookWorksheet BuildOperating(ExecutiveProgressReport report)
    {
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("Điều hành 30 ngày", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text(report.ProjectName, ExecutiveWorkbookStyleToken.Subtitle)),
            RowOf(Text($"Cửa sổ: {FormatDate(report.Metadata.SourceReportingDate)} – {FormatDate(report.OperatingItems.Count == 0 ? report.Metadata.SourceReportingDate.AddDays(29) : report.Metadata.SourceReportingDate.AddDays(29))}", ExecutiveWorkbookStyleToken.ReportingBoundary)),
            RowOf(Text("Ưu tiên theo thứ tự: quyết định / bị chặn → quá hạn → đang thực hiện → theo kế hoạch.", ExecutiveWorkbookStyleToken.Subtitle)),
            BlankRow(),
            RowOf(
                Text("Nhóm", ExecutiveWorkbookStyleToken.Header),
                Text("Việc cần làm", ExecutiveWorkbookStyleToken.Header),
                Text("Ảnh hưởng", ExecutiveWorkbookStyleToken.Header),
                Text("Đầu mối", ExecutiveWorkbookStyleToken.Header),
                Text("Cần xong trước", ExecutiveWorkbookStyleToken.Header),
                Text("Trạng thái", ExecutiveWorkbookStyleToken.Header),
                Text("Bối cảnh tiến độ", ExecutiveWorkbookStyleToken.Header))
        };

        var displayItems = report.OperatingItems.Take(MaxExecutiveOperatingItems).ToArray();
        rows.AddRange(displayItems.Select(item => RowOf(
            Text(OperatingCategoryLabel(item.Category), OperatingCategoryStyle(item.Category)),
            Text(item.Action, ExecutiveWorkbookStyleToken.Attention),
            Text(item.Consequence, ExecutiveWorkbookStyleToken.Attention),
            Text(item.OwnerLabel, item.OwnerLabel == ReaderFacingTextPolicy.MissingOwnerLabel ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
            Text(item.RequiredDateLabel, item.RequiredDate is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Plan),
            Text(item.StateLabel, StateStyle(item.StateLabel)),
            Text(item.ScheduleContext, ExecutiveWorkbookStyleToken.Default))));
        if (displayItems.Length == 0)
        {
            rows.Add(RowOf(Text("Không có việc cần theo dõi trong 30 ngày tới.", ExecutiveWorkbookStyleToken.Unknown)));
        }

        return new ExecutiveWorkbookWorksheet(
            "Điều hành 30 ngày",
            rows,
            new[] { 20d, 44d, 36d, 22d, 18d, 18d, 34d },
            [
                new ExecutiveWorkbookRange(1, 1, 1, 7),
                new ExecutiveWorkbookRange(2, 1, 2, 7),
                new ExecutiveWorkbookRange(3, 1, 3, 7),
                new ExecutiveWorkbookRange(4, 1, 4, 7)
            ],
            new ExecutiveWorkbookPane(6, 2),
            new ExecutiveWorkbookPrintSettings(ExecutiveWorkbookPrintOrientation.Landscape, fitToWidth: 1, fitToHeight: 0),
            showGridLines: false,
            zoomPercent: 100,
            autoFilterRange: new ExecutiveWorkbookRange(6, 1, rows.Count, 7));
    }

    private static ExecutiveWorkbookWorksheet BuildWbs(ExecutiveProgressReport report)
    {
        const int primaryColumnCount = 8;
        const int totalColumns = 19;
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("WBS dự án", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text($"{report.Wbs.ProjectCount} dự án · {report.Wbs.PhaseCount} giai đoạn · {report.Wbs.WorkPackageCount} gói công việc · {report.Wbs.DeliveryCardCount} thẻ công việc", ExecutiveWorkbookStyleToken.Subtitle)),
            BlankRow(),
            RowOf(
                Text("WBS", ExecutiveWorkbookStyleToken.Header),
                Text("Mã", ExecutiveWorkbookStyleToken.Header),
                Text("Hạng mục", ExecutiveWorkbookStyleToken.Header),
                Text("Loại", ExecutiveWorkbookStyleToken.Header),
                Text("Đầu mối", ExecutiveWorkbookStyleToken.Header),
                Text("Trạng thái", ExecutiveWorkbookStyleToken.Header),
                Text("% thực tế", ExecutiveWorkbookStyleToken.Header),
                Text("Cần chú ý", ExecutiveWorkbookStyleToken.Header),
                Text("Kế hoạch bắt đầu", ExecutiveWorkbookStyleToken.Header),
                Text("Kế hoạch kết thúc", ExecutiveWorkbookStyleToken.Header),
                Text("Thực tế bắt đầu", ExecutiveWorkbookStyleToken.Header),
                Text("Thực tế kết thúc", ExecutiveWorkbookStyleToken.Header),
                Text("Giờ thực tế", ExecutiveWorkbookStyleToken.Header),
                Text("Giờ còn lại", ExecutiveWorkbookStyleToken.Header),
                Text("Tiền nhiệm", ExecutiveWorkbookStyleToken.Header),
                Text("Phụ thuộc", ExecutiveWorkbookStyleToken.Header),
                Text("Tóm tắt ghi nhận", ExecutiveWorkbookStyleToken.Header),
                Text("Nguồn tham chiếu", ExecutiveWorkbookStyleToken.Header),
                Text("Cập nhật cuối", ExecutiveWorkbookStyleToken.Header))
        };

        foreach (var item in report.Wbs.Rows)
        {
            var isCard = item.Kind == ExecutiveWbsRowKind.DeliveryCard;
            rows.Add(new ExecutiveWorkbookRow(
                [
                    Text(item.WbsNumber, WbsKindStyle(item.Kind)),
                    Text(item.ReferenceCode, WbsKindStyle(item.Kind)),
                    Text(item.DisplayName, WbsKindStyle(item.Kind)),
                    Text(WbsKindLabel(item.Kind), WbsKindStyle(item.Kind)),
                    Text(item.OwnerLabel, item.OwnerLabel == ReaderFacingTextPolicy.MissingOwnerLabel ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
                    Text(item.StateLabel, StateStyle(item.StateLabel)),
                    item.ProgressPercent is null ? Text(item.ProgressLabel, ExecutiveWorkbookStyleToken.Unknown) : Number(item.ProgressPercent.Value / 100m, ExecutiveWorkbookStyleToken.ActualComplete, ExecutiveWorkbookNumberFormat.Percentage),
                    Text(item.AttentionLabel, item.AttentionLabel == "—" ? ExecutiveWorkbookStyleToken.Default : ExecutiveWorkbookStyleToken.Attention),
                    DateCell(item.PlannedStart, ExecutiveWorkbookStyleToken.Plan),
                    DateCell(item.PlannedFinish, ExecutiveWorkbookStyleToken.Plan),
                    DateCell(item.ActualStart, ExecutiveWorkbookStyleToken.ActualComplete),
                    DateCell(item.ActualFinish, ExecutiveWorkbookStyleToken.ActualComplete),
                    HoursCell(item.ActualEffortHours),
                    HoursCell(item.RemainingEffortHours),
                    Text(item.PredecessorCodes.Count == 0 ? "—" : string.Join(", ", item.PredecessorCodes)),
                    Text(item.DependencyLabel),
                    Text(item.EvidenceSummary, item.EvidenceSummary == ReaderFacingTextPolicy.MissingEvidenceLabel ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
                    Text(item.SourceReferenceLabel),
                    Text(FormatUpdate(item.LastOfficialUpdate), item.LastOfficialUpdate is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default)
                ],
                outlineLevel: item.Depth,
                hidden: isCard,
                collapsed: item.Kind == ExecutiveWbsRowKind.WorkPackage));
        }

        return new ExecutiveWorkbookWorksheet(
            "WBS",
            rows,
            new[] { 10d, 16d, 42d, 18d, 22d, 18d, 12d, 18d, 15d, 15d, 15d, 15d, 13d, 13d, 18d, 18d, 30d, 30d, 20d },
            Array.Empty<ExecutiveWorkbookRange>(),
            new ExecutiveWorkbookPane(4, primaryColumnCount),
            new ExecutiveWorkbookPrintSettings(ExecutiveWorkbookPrintOrientation.Landscape, fitToWidth: 1, fitToHeight: 0),
            showGridLines: false,
            zoomPercent: 100,
            columnGroups:
            [
                new ExecutiveWorkbookColumnGroup(9, 10, 1, hidden: true, collapsed: true),
                new ExecutiveWorkbookColumnGroup(11, 14, 1, hidden: true, collapsed: true),
                new ExecutiveWorkbookColumnGroup(15, 16, 1, hidden: true, collapsed: true),
                new ExecutiveWorkbookColumnGroup(17, 19, 1, hidden: true, collapsed: true)
            ],
            autoFilterRange: new ExecutiveWorkbookRange(4, 1, rows.Count, totalColumns),
            outlineSummaryBelow: false,
            outlineSummaryRight: false);
    }

    private static ExecutiveWorkbookWorksheet BuildMetadata(ExecutiveProgressReport report)
    {
        var metadata = report.Metadata;
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("Thông tin báo cáo", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text("Authority, nguồn và giới hạn của bản báo cáo", ExecutiveWorkbookStyleToken.Subtitle)),
            BlankRow(),
            RowOf(Text("Quyền hạn và nguồn", ExecutiveWorkbookStyleToken.Header), Text(string.Empty, ExecutiveWorkbookStyleToken.Header)),
            MetadataRow("Phân loại nguồn", metadata.AuthorityLabel),
            MetadataRow("Source identity", metadata.SourceIdentity),
            MetadataRow("Snapshot", metadata.SnapshotId),
            MetadataRow("Project", metadata.ProjectId),
            MetadataRow("Baseline", metadata.BaselineId),
            MetadataRow("Baseline version", metadata.BaselineVersion ?? ReaderFacingTextPolicy.MissingEvidenceLabel),
            MetadataRow("Contract", metadata.ContractVersion),
            MetadataRow("Register revision", metadata.RegisterRevision.ToString(CultureInfo.InvariantCulture)),
            MetadataRow("Ngày báo cáo", FormatDate(metadata.SourceReportingDate)),
            MetadataRow("Ngày phân tích đến", FormatDate(metadata.AnalysisAsOfDate)),
            BlankRow(),
            RowOf(Text("Khoảng kế hoạch", ExecutiveWorkbookStyleToken.Header), Text(string.Empty, ExecutiveWorkbookStyleToken.Header)),
            MetadataRow("Bắt đầu kế hoạch", FormatDate(metadata.PlanningStart)),
            MetadataRow("Kết thúc kế hoạch", FormatDate(metadata.PlanningFinish)),
            BlankRow(),
            RowOf(Text("Giới hạn sử dụng", ExecutiveWorkbookStyleToken.Header), Text(string.Empty, ExecutiveWorkbookStyleToken.Header))
        };
        rows.AddRange(metadata.Limitations.Select(limit => RowOf(Text("•", ExecutiveWorkbookStyleToken.Unknown), Text(limit))));

        return new ExecutiveWorkbookWorksheet(
            "Thông tin báo cáo",
            rows,
            new[] { 28d, 80d },
            [
                new ExecutiveWorkbookRange(1, 1, 1, 2),
                new ExecutiveWorkbookRange(2, 1, 2, 2),
                new ExecutiveWorkbookRange(4, 1, 4, 2),
                new ExecutiveWorkbookRange(16, 1, 16, 2),
                new ExecutiveWorkbookRange(20, 1, 20, 2)
            ],
            new ExecutiveWorkbookPane(4, 1),
            new ExecutiveWorkbookPrintSettings(ExecutiveWorkbookPrintOrientation.Portrait, fitToWidth: 1, fitToHeight: 0),
            showGridLines: false,
            zoomPercent: 100);
    }

    private static ExecutiveWorkbookRow MetadataRow(string label, string value) =>
        RowOf(Text(label, ExecutiveWorkbookStyleToken.Header), Text(value));

    private static string WbsKindLabel(ExecutiveWbsRowKind kind) => kind switch
    {
        ExecutiveWbsRowKind.Project => "Dự án",
        ExecutiveWbsRowKind.Phase => "Giai đoạn",
        ExecutiveWbsRowKind.WorkPackage => "Gói công việc",
        ExecutiveWbsRowKind.DeliveryCard => "Thẻ công việc",
        _ => "Hạng mục"
    };

    private static ExecutiveWorkbookStyleToken WbsKindStyle(ExecutiveWbsRowKind kind) => kind switch
    {
        ExecutiveWbsRowKind.Project => ExecutiveWorkbookStyleToken.ProjectHierarchy,
        ExecutiveWbsRowKind.Phase => ExecutiveWorkbookStyleToken.PhaseHierarchy,
        ExecutiveWbsRowKind.WorkPackage => ExecutiveWorkbookStyleToken.WorkPackageHierarchy,
        ExecutiveWbsRowKind.DeliveryCard => ExecutiveWorkbookStyleToken.DeliveryCardHierarchy,
        _ => ExecutiveWorkbookStyleToken.Default
    };

    private static string OperatingCategoryLabel(ExecutiveOperatingCategory category) => category switch
    {
        ExecutiveOperatingCategory.DecisionOrBlocker => "Quyết định / bị chặn",
        ExecutiveOperatingCategory.OverdueUnfinished => "Quá hạn",
        ExecutiveOperatingCategory.Active => "Đang thực hiện",
        ExecutiveOperatingCategory.PlannedOrMilestone => "Theo kế hoạch",
        _ => "Theo dõi"
    };

    private static ExecutiveWorkbookStyleToken OperatingCategoryStyle(ExecutiveOperatingCategory category) => category switch
    {
        ExecutiveOperatingCategory.DecisionOrBlocker or ExecutiveOperatingCategory.OverdueUnfinished => ExecutiveWorkbookStyleToken.Attention,
        ExecutiveOperatingCategory.Active => ExecutiveWorkbookStyleToken.ActualComplete,
        _ => ExecutiveWorkbookStyleToken.Plan
    };

    private static ExecutiveWorkbookWorksheet BuildDailyGantt(ExecutiveProgressReport report)
    {
        var rows = new List<ExecutiveWorkbookRow>
        {
            RowOf(Text("Gantt", ExecutiveWorkbookStyleToken.Title)),
            RowOf(Text(report.ProjectName, ExecutiveWorkbookStyleToken.Subtitle)),
            RowOf(Text($"Cập nhật đến {FormatDate(report.SourceReportingDate)}", ExecutiveWorkbookStyleToken.ReportingBoundary)),
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
            "Gantt",
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
            : row.ActualPresentationKind == ExecutiveActualPresentationKind.EffortOnly
                ? ExecutiveWorkbookStyleToken.ActualComplete
            : row.ForecastFinish is not null && row.ForecastFinish > analysisAsOfDate
                ? ExecutiveWorkbookStyleToken.Forecast
                : ExecutiveWorkbookStyleToken.Unknown;
        var laneLabel = row.ActualPresentationKind == ExecutiveActualPresentationKind.EffortOnly
            ? "● Có ghi nhận"
            : row.ActualPresentationKind == ExecutiveActualPresentationKind.None
                ? ReaderFacingTextPolicy.MissingEvidenceLabel
                : "Thực tế";
        var cells = new List<ExecutiveWorkbookCell>
        {
            Text(string.Empty),
            Text(string.Empty),
            Text(string.Empty),
            Text(string.Empty),
            Text(string.Empty),
            Text(string.Empty),
            Text(string.Empty),
            Text(laneLabel, laneStyle)
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
        if (row.ActualPresentationKind is ExecutiveActualPresentationKind.RecordedInterval or ExecutiveActualPresentationKind.OpenRecordedInterval
            && IsWithin(date, row.ActualStart, row.ActualDisplayThrough))
        {
            return Text(
                useContinuationMarkers ? ContinuationMarker(date, row.ActualStart, row.ActualDisplayThrough, axisStart, axisFinish) : string.Empty,
                ExecutiveWorkbookStyleToken.ActualComplete,
                isReportingBoundary: date == sourceReportingDate);
        }

        if (row.ActualPresentationKind == ExecutiveActualPresentationKind.CompletionPoint
            && row.ActualFinish == date)
        {
            return Text("✓", ExecutiveWorkbookStyleToken.ActualComplete, isReportingBoundary: date == sourceReportingDate);
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
            RowOf(Text($"Cập nhật đến {FormatDate(report.SourceReportingDate)} · mỗi dòng là một thẻ công việc", ExecutiveWorkbookStyleToken.ReportingBoundary)),
            BlankRow(),
            RowOf(
                Text("Hạng mục", ExecutiveWorkbookStyleToken.Header),
                Text("Giai đoạn", ExecutiveWorkbookStyleToken.Header),
                Text("Gói công việc", ExecutiveWorkbookStyleToken.Header),
                Text("Đầu mối", ExecutiveWorkbookStyleToken.Header),
                Text("Trạng thái", ExecutiveWorkbookStyleToken.Header),
                Text("Ghi nhận", ExecutiveWorkbookStyleToken.Header),
                Text("Cần chú ý", ExecutiveWorkbookStyleToken.Header),
                Text("% thực tế", ExecutiveWorkbookStyleToken.Header),
                Text("Bắt đầu kế hoạch", ExecutiveWorkbookStyleToken.Header),
                Text("Kết thúc kế hoạch", ExecutiveWorkbookStyleToken.Header),
                Text("Bắt đầu thực tế", ExecutiveWorkbookStyleToken.Header),
                Text("Kết thúc thực tế", ExecutiveWorkbookStyleToken.Header),
                Text("Kết thúc dự báo", ExecutiveWorkbookStyleToken.Header),
                Text("Giờ thực tế", ExecutiveWorkbookStyleToken.Header),
                Text("Giờ còn lại", ExecutiveWorkbookStyleToken.Header),
                Text("Tiền nhiệm", ExecutiveWorkbookStyleToken.Header),
                Text("Phụ thuộc", ExecutiveWorkbookStyleToken.Header),
                Text("Cập nhật cuối", ExecutiveWorkbookStyleToken.Header),
                Text("Mã tham chiếu", ExecutiveWorkbookStyleToken.Header),
                Text("Nguồn tham chiếu", ExecutiveWorkbookStyleToken.Header))
        };
        rows.AddRange(report.DeliveryCardDetails.Select(detail => RowOf(
            Text(detail.Description),
            Text(detail.PhaseName),
            Text(detail.WorkPackageName),
            Text(detail.OwnerLabel, detail.OwnerLabel == ReaderFacingTextPolicy.MissingOwnerLabel ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
            Text(detail.StateLabel, StateStyle(detail.StateLabel)),
            Text(detail.RecordingLabel, detail.RecordingLabel == "Có ghi nhận" ? ExecutiveWorkbookStyleToken.ActualComplete : ExecutiveWorkbookStyleToken.Unknown),
            Text(detail.AttentionLabel, detail.AttentionLabel == "—" ? ExecutiveWorkbookStyleToken.Default : ExecutiveWorkbookStyleToken.Attention),
            detail.ProgressPercent is null
                ? Text(detail.ProgressLabel, ExecutiveWorkbookStyleToken.Unknown)
                : Number(detail.ProgressPercent.Value / 100m, ExecutiveWorkbookStyleToken.ActualComplete, ExecutiveWorkbookNumberFormat.Percentage),
            DateCell(detail.PlannedStart, ExecutiveWorkbookStyleToken.Plan),
            DateCell(detail.PlannedFinish, ExecutiveWorkbookStyleToken.Plan),
            DateCell(detail.ActualStart, ExecutiveWorkbookStyleToken.ActualComplete),
            DateCell(detail.ActualFinish, ExecutiveWorkbookStyleToken.ActualComplete),
            DateCell(detail.ForecastFinish, ExecutiveWorkbookStyleToken.Forecast),
            HoursCell(detail.ActualEffortHours),
            HoursCell(detail.RemainingEffortHours),
            Text(detail.PredecessorCodes.Count == 0 ? "—" : string.Join(", ", detail.PredecessorCodes)),
            Text(detail.DependencyLabel),
            Text(FormatUpdate(detail.LastOfficialUpdate), detail.LastOfficialUpdate is null ? ExecutiveWorkbookStyleToken.Unknown : ExecutiveWorkbookStyleToken.Default),
            Text(detail.ReferenceCode),
            Text(detail.SourceReferenceLabel))));
        if (report.DeliveryCardDetails.Count == 0)
        {
            rows.Add(RowOf(Text("Chưa có dữ liệu công việc", ExecutiveWorkbookStyleToken.Unknown)));
        }

        return Worksheet(
            "Chi tiết công việc",
            rows,
            new[] { 40d, 24d, 28d, 22d, 18d, 18d, 18d, 12d, 15d, 15d, 15d, 15d, 15d, 14d, 14d, 18d, 18d, 20d, 18d, 30d },
            freezeRows: 4,
            freezeColumns: 1,
            mergedRanges:
            [
                new ExecutiveWorkbookRange(1, 1, 1, 20),
                new ExecutiveWorkbookRange(2, 1, 2, 20)
            ],
            autoFilterRange: new ExecutiveWorkbookRange(4, 1, rows.Count, 20));
    }

    private static ExecutiveWorkbookWorksheet Worksheet(
        string name,
        IReadOnlyList<ExecutiveWorkbookRow> rows,
        IReadOnlyList<double> widths,
        int freezeRows,
        int freezeColumns,
        IReadOnlyList<ExecutiveWorkbookRange>? mergedRanges = null,
        ExecutiveWorkbookRange? autoFilterRange = null) =>
        new(
            name,
            rows,
            widths,
            mergedRanges ?? Array.Empty<ExecutiveWorkbookRange>(),
            new ExecutiveWorkbookPane(freezeRows, freezeColumns),
            new ExecutiveWorkbookPrintSettings(ExecutiveWorkbookPrintOrientation.Landscape, fitToWidth: 1, fitToHeight: 0),
            showGridLines: false,
            zoomPercent: 100,
            autoFilterRange: autoFilterRange);

    private static IReadOnlyList<OverviewTimeBucket> BuildCompactOverviewAxis(ExecutiveProgressReport report)
    {
        var dates = report.OverviewTimeline
            .SelectMany(row => new[] { row.PlannedStart, row.PlannedFinish })
            .Concat(new DateOnly?[] { report.PlanningStart, report.PlanningFinish, report.SourceReportingDate })
            .Where(date => date is not null)
            .Select(date => date!.Value)
            .ToArray();
        if (dates.Length == 0)
        {
            return Array.Empty<OverviewTimeBucket>();
        }

        var start = dates.Min();
        var finish = dates.Max();
        if (finish.DayNumber - start.DayNumber <= 196)
        {
            var dayOffset = ((int)start.DayOfWeek + 6) % 7;
            var firstMonday = start.AddDays(-dayOffset);
            var buckets = new List<OverviewTimeBucket>();
            for (var cursor = firstMonday; cursor <= finish; cursor = cursor.AddDays(7))
            {
                buckets.Add(new OverviewTimeBucket(cursor, cursor.AddDays(6), cursor.ToString("dd/MM", CultureInfo.InvariantCulture), IsMonthly: false));
            }

            return buckets;
        }

        var firstMonth = new DateOnly(start.Year, start.Month, 1);
        var monthlyBuckets = new List<OverviewTimeBucket>();
        for (var cursor = firstMonth; cursor <= finish; cursor = cursor.AddMonths(1))
        {
            var monthFinish = new DateOnly(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            monthlyBuckets.Add(new OverviewTimeBucket(cursor, monthFinish, cursor.ToString("MM/yyyy", CultureInfo.InvariantCulture), IsMonthly: true));
        }

        return monthlyBuckets;
    }

    private static IReadOnlyList<OverviewColumnSpan> BuildBalancedSpans(int totalColumns, int spanCount)
    {
        var spans = new List<OverviewColumnSpan>(spanCount);
        var baseWidth = totalColumns / spanCount;
        var remainder = totalColumns % spanCount;
        var start = 1;
        for (var index = 0; index < spanCount; index++)
        {
            var width = baseWidth + (index < remainder ? 1 : 0);
            spans.Add(new OverviewColumnSpan(start, start + width - 1));
            start += width;
        }

        return spans;
    }

    private static IReadOnlyList<OverviewColumnSpan> BuildAttentionSpans(int totalColumns)
    {
        var actionEnd = Math.Max(1, (int)Math.Round(totalColumns * 0.38m, MidpointRounding.AwayFromZero));
        var impactEnd = Math.Max(actionEnd + 1, (int)Math.Round(totalColumns * 0.72m, MidpointRounding.AwayFromZero));
        var ownerEnd = Math.Max(impactEnd + 1, (int)Math.Round(totalColumns * 0.87m, MidpointRounding.AwayFromZero));
        ownerEnd = Math.Min(ownerEnd, totalColumns - 1);
        return
        [
            new OverviewColumnSpan(1, actionEnd),
            new OverviewColumnSpan(actionEnd + 1, impactEnd),
            new OverviewColumnSpan(impactEnd + 1, ownerEnd),
            new OverviewColumnSpan(ownerEnd + 1, totalColumns)
        ];
    }

    private static void AddSpannedRow(
        ICollection<ExecutiveWorkbookRow> rows,
        ICollection<ExecutiveWorkbookRange> mergedRanges,
        int totalColumns,
        IReadOnlyList<OverviewColumnSpan> spans,
        IReadOnlyList<ExecutiveWorkbookCell> values)
    {
        if (spans.Count != values.Count)
        {
            throw new InvalidOperationException("A spanned overview row must provide one value per span.");
        }

        var cells = Enumerable.Repeat(Text(string.Empty), totalColumns).ToArray();
        for (var index = 0; index < spans.Count; index++)
        {
            cells[spans[index].StartColumn - 1] = values[index];
        }

        var rowNumber = rows.Count + 1;
        rows.Add(RowOf(cells));
        foreach (var span in spans.Where(span => span.EndColumn > span.StartColumn))
        {
            mergedRanges.Add(new ExecutiveWorkbookRange(rowNumber, span.StartColumn, rowNumber, span.EndColumn));
        }
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

    private static string BuildProgressSummaryDetail(ExecutiveProgressSummary progress)
    {
        var percentage = progress.RecordedPercent is null
            ? "Chưa đủ dữ liệu để tính % hoàn thành toàn dự án"
            : $"Tiến độ tổng thể {progress.RecordedPercent}%";
        return $"{progress.CompletedCount} công việc hoàn thành · {percentage}";
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

    private sealed record OverviewColumnSpan(int StartColumn, int EndColumn);

    private sealed record OverviewTimeBucket(DateOnly Start, DateOnly Finish, string Label, bool IsMonthly)
    {
        public bool Contains(DateOnly date) => date >= Start && date <= Finish;

        public bool Intersects(DateOnly? start, DateOnly? finish)
        {
            if (start is null && finish is null)
            {
                return false;
            }

            var effectiveStart = start ?? finish!.Value;
            var effectiveFinish = finish ?? start!.Value;
            if (effectiveFinish < effectiveStart)
            {
                (effectiveStart, effectiveFinish) = (effectiveFinish, effectiveStart);
            }

            return effectiveStart <= Finish && effectiveFinish >= Start;
        }
    }

    private sealed record DailyGanttTableLayout(int HeaderLastRow, int TotalColumns);
}
