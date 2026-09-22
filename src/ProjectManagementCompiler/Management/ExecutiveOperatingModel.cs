namespace ProjectManagementCompiler.Management;

public enum ExecutiveOperatingCategory
{
    DecisionOrBlocker,
    OverdueUnfinished,
    Active,
    PlannedOrMilestone
}

public sealed record ExecutiveOperatingItem
{
    public string StableKey { get; init; } = string.Empty;
    public ExecutiveOperatingCategory Category { get; init; }
    public string TargetKind { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Consequence { get; init; } = string.Empty;
    public string OwnerLabel { get; init; } = ReaderFacingTextPolicy.MissingOwnerLabel;
    public DateOnly? RequiredDate { get; init; }
    public string RequiredDateLabel { get; init; } = "Chưa xác định";
    public string StateLabel { get; init; } = ReaderFacingTextPolicy.MissingEvidenceLabel;
    public string ScheduleContext { get; init; } = ReaderFacingTextPolicy.MissingEvidenceLabel;
    public int SourceOrder { get; init; }
}

public sealed record ExecutiveOperatingProjection
{
    public DateOnly WindowStart { get; init; }
    public DateOnly WindowFinish { get; init; }
    public IReadOnlyList<ExecutiveOperatingItem> Items { get; init; } = Array.Empty<ExecutiveOperatingItem>();
}
