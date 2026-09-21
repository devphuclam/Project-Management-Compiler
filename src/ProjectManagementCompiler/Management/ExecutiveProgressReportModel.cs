using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public enum ExecutiveConditionTone
{
    Plan,
    Complete,
    Attention,
    Blocked,
    Unknown
}

public enum ExecutiveConditionCode
{
    ScheduleBlocked,
    ScheduleLate,
    ScheduleAtRisk,
    ScheduleAssessable,
    ScheduleUnknown,
    ReadinessBlocked,
    ReadinessDecision,
    ReadinessAction,
    ReadinessComplete,
    ReadinessUnknown
}

public enum ExecutiveScheduleRowKind
{
    Phase,
    Milestone,
    WorkPackage
}

public enum ExecutiveAttentionCategory
{
    Blocked,
    Overdue,
    DecisionBeforeNextMilestone,
    AtRisk,
    MissingOwner,
    OtherDecision,
    PendingAction
}

public sealed record ExecutiveProgressReport
{
    public string ProjectName { get; init; } = string.Empty;
    public DateOnly SourceReportingDate { get; init; }
    public DateOnly AnalysisAsOfDate { get; init; }
    public DateOnly? PlanningStart { get; init; }
    public DateOnly? PlanningFinish { get; init; }
    public string CurrentPhase { get; init; } = string.Empty;
    public ExecutiveCondition ScheduleCondition { get; init; } = new();
    public ExecutiveCondition ReadinessCondition { get; init; } = new();
    public ExecutiveMilestoneSummary NextMilestone { get; init; } = new();
    public ExecutiveProgressSummary Progress { get; init; } = new();
    public ExecutiveDailyGanttProjection DailyGantt { get; init; } = new();
    public IReadOnlyList<ExecutiveAttentionItem> OverviewAttention { get; init; } = Array.Empty<ExecutiveAttentionItem>();
    public IReadOnlyList<ExecutiveAttentionItem> AllAttention { get; init; } = Array.Empty<ExecutiveAttentionItem>();
    public IReadOnlyList<ExecutiveScheduleRow> OverviewTimeline { get; init; } = Array.Empty<ExecutiveScheduleRow>();
    public IReadOnlyList<ExecutiveScheduleRow> WorkPackageSchedule { get; init; } = Array.Empty<ExecutiveScheduleRow>();
    public IReadOnlyList<ExecutiveDeliveryCardDetail> DeliveryCardDetails { get; init; } = Array.Empty<ExecutiveDeliveryCardDetail>();
}

public sealed record ExecutiveCondition
{
    public ExecutiveConditionCode Code { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public ExecutiveConditionTone Tone { get; init; }
}

public sealed record ExecutiveMilestoneSummary
{
    public string DisplayName { get; init; } = string.Empty;
    public MilestoneKind? Kind { get; init; }
    public DateOnly? PlannedDate { get; init; }
    public bool IsMissing { get; init; }
}

public sealed record ExecutiveProgressSummary
{
    public int? RecordedPercent { get; init; }
    public string Statement { get; init; } = string.Empty;
    public decimal? ActualEffortHours { get; init; }
    public decimal? RemainingEffortHours { get; init; }
    public int CompletedCount { get; init; }
    public int InProgressCount { get; init; }
    public int NotStartedCount { get; init; }
    public int UnknownCount { get; init; }
    public int RecordedCardCount { get; init; }
    public int TotalCardCount { get; init; }
    public int ProgressEligibleCardCount { get; init; }
    public DateTimeOffset? LastOfficialUpdate { get; init; }
}

public sealed record ExecutiveScheduleRow
{
    public ExecutiveScheduleRowKind Kind { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? PhaseDisplayName { get; init; }
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedFinish { get; init; }
    public string StateLabel { get; init; } = string.Empty;
    public string OwnerLabel { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
    public bool IsNextMilestone { get; init; }
    public int SourceOrder { get; init; }
}

public sealed record ExecutiveDeliveryCardDetail
{
    public string Description { get; init; } = string.Empty;
    public string PhaseName { get; init; } = string.Empty;
    public string WorkPackageName { get; init; } = string.Empty;
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedFinish { get; init; }
    public DateOnly? ActualStart { get; init; }
    public DateOnly? ActualFinish { get; init; }
    public DateOnly? ForecastFinish { get; init; }
    public decimal? ActualEffortHours { get; init; }
    public decimal? RemainingEffortHours { get; init; }
    public int? ProgressPercent { get; init; }
    public string ProgressLabel { get; init; } = string.Empty;
    public string RecordingLabel { get; init; } = string.Empty;
    public string OwnerLabel { get; init; } = string.Empty;
    public string StateLabel { get; init; } = string.Empty;
    public DateTimeOffset? LastOfficialUpdate { get; init; }
    public string ReferenceCode { get; init; } = string.Empty;
    public int SourceOrder { get; init; }
}

public sealed record ExecutiveAttentionItem
{
    public ExecutiveAttentionCategory Category { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
    public string OwnerLabel { get; init; } = string.Empty;
    public DateOnly? DueDate { get; init; }
    public string DueLabel { get; init; } = string.Empty;
    public bool OverviewEligible { get; init; }
    public string DeduplicationKey { get; init; } = string.Empty;
    public int SourceOrder { get; init; }
}
