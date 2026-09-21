namespace ProjectManagementCompiler.Management;

public enum ExecutiveDailyGanttRowKind
{
    Project,
    Phase,
    WorkPackage,
    DeliveryCard,
    Milestone
}

/// <summary>
/// One reader-facing row in a presentation-only daily Gantt projection.
/// Delivery cards carry direct official execution facts; aggregate rows carry
/// conservative child roll-ups and always disclose evidence coverage.
/// </summary>
public sealed record ExecutiveDailyGanttRow
{
    public ExecutiveDailyGanttRowKind Kind { get; init; }
    public string ReferenceCode { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int HierarchyLevel { get; init; }
    public string? PhaseDisplayName { get; init; }
    public string? WorkPackageDisplayName { get; init; }
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedFinish { get; init; }
    public DateOnly? ActualStart { get; init; }
    public DateOnly? ActualFinish { get; init; }
    public DateOnly? ActualDisplayThrough { get; init; }
    public DateOnly? ForecastFinish { get; init; }
    public int? ProgressPercent { get; init; }
    public string ProgressLabel { get; init; } = "Chưa đủ dữ liệu";
    public int RecordedChildCount { get; init; }
    public int ProgressEligibleChildCount { get; init; }
    public int TotalChildCount { get; init; }
    public string CoverageLabel { get; init; } = "Độ phủ 0/0";
    public string StateLabel { get; init; } = "Chưa cập nhật";
    public string OwnerLabel { get; init; } = "Chưa xác định đầu mối";
    public DateTimeOffset? LastOfficialUpdate { get; init; }
    public bool IsBlocked { get; init; }
    public bool IsOverdue { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsNextMilestone { get; init; }
    public int SourceOrder { get; init; }
}

/// <summary>
/// Immutable daily-Gantt presentation data. The near-term collection is
/// populated by the later near-term feature task; its dates are established
/// here so every consumer shares the official reporting-date boundary.
/// </summary>
public sealed record ExecutiveDailyGanttProjection
{
    public DateOnly FullStart { get; init; }
    public DateOnly FullFinish { get; init; }
    public DateOnly NearTermStart { get; init; }
    public DateOnly NearTermFinish { get; init; }
    public IReadOnlyList<ExecutiveDailyGanttRow> OverviewRows { get; init; } = Array.Empty<ExecutiveDailyGanttRow>();
    public IReadOnlyList<ExecutiveDailyGanttRow> FullRows { get; init; } = Array.Empty<ExecutiveDailyGanttRow>();
    public IReadOnlyList<ExecutiveDailyGanttRow> NearTermRows { get; init; } = Array.Empty<ExecutiveDailyGanttRow>();
}
