using ProjectManagementCompiler.Application;

namespace ProjectManagementCompiler.Management;

public sealed class ExecutiveOperatingProjector
{
    public ExecutiveOperatingProjection Build(
        CompilationResult result,
        ExecutiveDailyGanttProjection dailyGantt,
        IReadOnlyList<ExecutiveAttentionItem> attention)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(dailyGantt);
        ArgumentNullException.ThrowIfNull(attention);

        var start = result.Project.ImportMetadata?.RegisterStatusDate
            ?? throw new InvalidOperationException("An official reporting date is required for the operating projection.");
        var finish = start.AddDays(29);
        var candidates = new List<ExecutiveOperatingItem>();

        foreach (var item in attention)
        {
            var category = item.Category switch
            {
                ExecutiveAttentionCategory.Overdue => ExecutiveOperatingCategory.OverdueUnfinished,
                ExecutiveAttentionCategory.Blocked
                    or ExecutiveAttentionCategory.DecisionBeforeNextMilestone
                    or ExecutiveAttentionCategory.OtherDecision
                    or ExecutiveAttentionCategory.PendingAction
                    or ExecutiveAttentionCategory.AtRisk
                    => ExecutiveOperatingCategory.DecisionOrBlocker,
                _ => (ExecutiveOperatingCategory?)null
            };
            if (category is null)
            {
                continue;
            }

            var target = ParseTarget(item.DeduplicationKey);
            candidates.Add(new ExecutiveOperatingItem
            {
                StableKey = item.DeduplicationKey,
                Category = category.Value,
                TargetKind = target.Kind,
                TargetId = target.Id,
                Action = item.Action,
                Consequence = item.Impact,
                OwnerLabel = item.OwnerLabel,
                RequiredDate = item.DueDate,
                RequiredDateLabel = item.DueLabel,
                StateLabel = category == ExecutiveOperatingCategory.OverdueUnfinished ? "Quá hạn" : "Cần xử lý",
                ScheduleContext = item.DueDate is null ? "Ngoài lịch cụ thể" : $"Cần xử lý trước {item.DueLabel}",
                SourceOrder = item.SourceOrder
            });
        }

        foreach (var row in dailyGantt.FullRows.Where(row => row.Kind is ExecutiveDailyGanttRowKind.DeliveryCard or ExecutiveDailyGanttRowKind.Milestone))
        {
            if (row.Kind == ExecutiveDailyGanttRowKind.DeliveryCard && row.IsOverdue && row.StateLabel is not "Hoàn thành" and not "Đã hủy")
            {
                candidates.Add(BuildScheduleItem(
                    row,
                    ExecutiveOperatingCategory.OverdueUnfinished,
                    $"Hoàn tất “{row.DisplayName}” và cập nhật vướng mắc.",
                    "Công việc đang trễ so với ngày kế hoạch."));
                continue;
            }

            if (row.Kind == ExecutiveDailyGanttRowKind.DeliveryCard
                && row.StateLabel == "Đang thực hiện"
                && Intersects(row, start, finish))
            {
                candidates.Add(BuildScheduleItem(
                    row,
                    ExecutiveOperatingCategory.Active,
                    $"Cập nhật tiến độ “{row.DisplayName}”.",
                    "Cần xác nhận tình trạng tại ngày báo cáo."));
                continue;
            }

            if (row.Kind == ExecutiveDailyGanttRowKind.DeliveryCard
                && row.StateLabel is "Hoàn thành" or "Đã hủy")
            {
                continue;
            }

            if (Intersects(row, start, finish))
            {
                candidates.Add(BuildScheduleItem(
                    row,
                    ExecutiveOperatingCategory.PlannedOrMilestone,
                    row.Kind == ExecutiveDailyGanttRowKind.Milestone
                        ? $"Chuẩn bị mốc “{row.DisplayName}”."
                        : $"{row.DisplayName}.",
                    row.Kind == ExecutiveDailyGanttRowKind.Milestone
                        ? "Mốc nằm trong 30 ngày tới."
                        : "Dự kiến hoàn tất trong 30 ngày tới."));
            }
        }

        var items = candidates
            .GroupBy(item => item.StableKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(item => CategoryOrder(item.Category))
                .ThenBy(item => item.RequiredDate is null)
                .ThenBy(item => item.RequiredDate ?? DateOnly.MaxValue)
                .ThenBy(item => item.SourceOrder)
                .First())
            .OrderBy(item => CategoryOrder(item.Category))
            .ThenBy(item => item.RequiredDate is null)
            .ThenBy(item => item.RequiredDate ?? DateOnly.MaxValue)
            .ThenBy(item => item.SourceOrder)
            .ThenBy(item => item.StableKey, StringComparer.Ordinal)
            .ToArray();

        return new ExecutiveOperatingProjection
        {
            WindowStart = start,
            WindowFinish = finish,
            Items = items
        };
    }

    public ExecutiveOperatingProjection Build(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var daily = new ExecutiveDailyGanttProjector().Build(result);
        var report = new ExecutiveProgressReportProjector().Build(result);
        return Build(result, daily, report.AllAttention);
    }

    private static ExecutiveOperatingItem BuildScheduleItem(
        ExecutiveDailyGanttRow row,
        ExecutiveOperatingCategory category,
        string action,
        string consequence)
    {
        var targetKind = row.Kind == ExecutiveDailyGanttRowKind.Milestone ? "Milestone" : "DeliveryCard";
        var requiredDate = row.PlannedFinish ?? row.PlannedStart;
        var plan = FormatRange(row.PlannedStart, row.PlannedFinish);
        var actual = FormatRange(row.ActualStart, row.ActualFinish ?? row.ActualDisplayThrough);
        return new ExecutiveOperatingItem
        {
            StableKey = $"{targetKind}:{row.ReferenceCode}",
            Category = category,
            TargetKind = targetKind,
            TargetId = row.ReferenceCode,
            Action = action,
            Consequence = consequence,
            OwnerLabel = row.OwnerLabel,
            RequiredDate = requiredDate,
            RequiredDateLabel = FormatDate(requiredDate),
            StateLabel = row.StateLabel,
            ScheduleContext = $"Kế hoạch: {plan}; Thực tế: {actual}",
            SourceOrder = row.SourceOrder
        };
    }

    private static (string Kind, string Id) ParseTarget(string key)
    {
        var separator = key.IndexOf(':');
        return separator > 0
            ? (key[..separator], key[(separator + 1)..])
            : ("Nội dung", key);
    }

    private static bool Intersects(ExecutiveDailyGanttRow row, DateOnly start, DateOnly finish)
    {
        var rowStart = row.PlannedStart ?? row.PlannedFinish;
        var rowFinish = row.PlannedFinish ?? row.PlannedStart;
        return rowStart is not null
            && rowFinish is not null
            && rowStart <= finish
            && rowFinish >= start;
    }

    private static int CategoryOrder(ExecutiveOperatingCategory category) => category switch
    {
        ExecutiveOperatingCategory.DecisionOrBlocker => 0,
        ExecutiveOperatingCategory.OverdueUnfinished => 1,
        ExecutiveOperatingCategory.Active => 2,
        ExecutiveOperatingCategory.PlannedOrMilestone => 3,
        _ => int.MaxValue
    };

    private static string FormatRange(DateOnly? start, DateOnly? finish)
    {
        if (start is null && finish is null)
        {
            return "Chưa ghi nhận";
        }

        if (start is null)
        {
            return $"đến {FormatDate(finish)}";
        }

        if (finish is null)
        {
            return $"từ {FormatDate(start)}";
        }

        return start == finish ? FormatDate(start) : $"{FormatDate(start)} – {FormatDate(finish)}";
    }

    private static string FormatDate(DateOnly? value) => value is null ? "Chưa xác định" : value.Value.ToString("dd/MM/yyyy");
}
