namespace ProjectManagementCompiler.Domain;

public sealed record ExecutionOverlay
{
    public IReadOnlyList<ExecutionRecord> Records { get; init; } = Array.Empty<ExecutionRecord>();
}

public sealed record ExecutionRecord
{
    public string WorkItemId { get; init; } = string.Empty;
    public ExecutionState ExecutionState { get; init; } = ExecutionState.NotStarted;
    public DateOnly? ActualStart { get; init; }
    public DataState ActualStartState { get; init; } = DataState.Unknown;
    public DateOnly? ActualFinish { get; init; }
    public DataState ActualFinishState { get; init; } = DataState.Unknown;
    public decimal? ActualEffortHours { get; init; }
    public DataState ActualEffortState { get; init; } = DataState.Unknown;
    public decimal? RemainingEffortHours { get; init; }
    public DataState RemainingEffortState { get; init; } = DataState.Unknown;
    public DateTimeOffset? LastUpdatedAt { get; init; }
    public string? Note { get; init; }
    public SourceReference? EvidenceReference { get; init; }
}
