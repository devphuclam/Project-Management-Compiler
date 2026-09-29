using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed record ProjectOverviewProjection
{
    public ProjectOverviewIdentity Project { get; init; } = new();
    public ProjectOverviewPhase CurrentPhase { get; init; } = new();
    public ProgressCoverageSummary Progress { get; init; } = new();
    public ProjectOverviewControlPoint? NextControlPoint { get; init; }
    public IReadOnlyList<ProjectOverviewAttentionItem> AttentionItems { get; init; } = Array.Empty<ProjectOverviewAttentionItem>();
}

public sealed record ProjectOverviewIdentity
{
    public string Name { get; init; } = ReaderFacingTextPolicy.MissingEvidenceLabel;
    public DateOnly? ReportingDate { get; init; }
    public string SourceIdentity { get; init; } = string.Empty;
}

public sealed record ProjectOverviewPhase
{
    public string? Name { get; init; }
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedFinish { get; init; }
    public DataState State { get; init; } = DataState.Unknown;
}

public sealed record ProjectOverviewControlPoint
{
    public string Name { get; init; } = ReaderFacingTextPolicy.MissingEvidenceLabel;
    public DateOnly PlannedDate { get; init; }
    public MilestoneKind Kind { get; init; }
    public ExecutionState? SourceState { get; init; }
}

public sealed record ProjectOverviewAttentionItem
{
    public string WorkItemId { get; init; } = string.Empty;
    public string WorkItemName { get; init; } = ReaderFacingTextPolicy.MissingEvidenceLabel;
    public string Consequence { get; init; } = string.Empty;
    public WarningSeverity Severity { get; init; }
    public string Destination { get; init; } = "gantt";
}
