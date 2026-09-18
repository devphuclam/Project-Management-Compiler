namespace ProjectManagementCompiler.Domain;

public sealed record ManagementAnalysis
{
    public DateOnly? AsOfDate { get; init; }
    public DataState CpmState { get; init; } = DataState.Unknown;
    public IReadOnlyList<CpmNodeMetric> CpmNodes { get; init; } = Array.Empty<CpmNodeMetric>();
    public IReadOnlyList<string> CriticalPathIds { get; init; } = Array.Empty<string>();
    public DateOnly? BaselineFinish { get; init; }
    public DateOnly? CalculatedFinish { get; init; }
    public DataState ForecastState { get; init; } = DataState.Unknown;
    public DateOnly? ForecastFinish { get; init; }
    public ScheduleConstraintSummary? ResourceBaselineScheduleConstraint { get; init; }
    public EffortAccountingReconciliation? EffortAccounting { get; init; }
    public ScheduleVariance? ScheduleVariance { get; init; }
    public CapacityAnalysis? Capacity { get; init; }
    public ReserveAnalysis? Reserve { get; init; }
    public ExecutionStatusCounts ExecutionStatus { get; init; } = new();
    public IReadOnlyList<WorkItemVariance> WorkItemVariances { get; init; } = Array.Empty<WorkItemVariance>();
    public IReadOnlyList<Alert> Alerts { get; init; } = Array.Empty<Alert>();
    public IReadOnlyList<HealthIndicator> HealthIndicators { get; init; } = Array.Empty<HealthIndicator>();
    public IReadOnlyList<ViewSummary> ViewSummaries { get; init; } = Array.Empty<ViewSummary>();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed record ExecutionStatusCounts
{
    public int Total { get; init; }
    public int Completed { get; init; }
    public int InProgress { get; init; }
    public int NotStarted { get; init; }
    public int Suspended { get; init; }
    public int Cancelled { get; init; }
    public int LateToStart { get; init; }
    public int Overdue { get; init; }
    public int CompletedLate { get; init; }
    public int AtRisk { get; init; }
}

public sealed record WorkItemVariance
{
    public string WorkItemId { get; init; } = string.Empty;
    public ExecutionState ExecutionState { get; init; }
    public DateOnly? BaselineStart { get; init; }
    public DateOnly? BaselineFinish { get; init; }
    public DateOnly? ActualStart { get; init; }
    public DateOnly? ActualFinish { get; init; }
    public int? StartVarianceWorkingMinutes { get; init; }
    public int? FinishVarianceWorkingMinutes { get; init; }
    public DataState State { get; init; } = DataState.Unknown;
}

public sealed record Alert
{
    public string WorkItemId { get; init; } = string.Empty;
    public string AlertCode { get; init; } = string.Empty;
    public WarningSeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;
    public int? VarianceWorkingMinutes { get; init; }
    public IReadOnlyList<string> ReasonWorkItemIds { get; init; } = Array.Empty<string>();
    public DateOnly DerivedAt { get; init; }
}

public sealed record ScheduleConstraintSummary
{
    public string? ResourceConstraint { get; init; }
    public string? BaselineConstraint { get; init; }
}

public sealed record EffortAccountingReconciliation
{
    public string AccountingLevel { get; init; } = string.Empty;
    public decimal? AuthoritativeEffortHours { get; init; }
    public decimal? DetailedEffortHours { get; init; }
    public decimal? DifferenceHours { get; init; }
    public DataState State { get; init; } = DataState.Unknown;
    public IReadOnlyList<string> ReconciliationWarnings { get; init; } = Array.Empty<string>();
}

public sealed record CpmNodeMetric
{
    public string NodeId { get; init; } = string.Empty;
    public int? EarliestStartWorkingMinutes { get; init; }
    public int? EarliestFinishWorkingMinutes { get; init; }
    public int? LatestStartWorkingMinutes { get; init; }
    public int? LatestFinishWorkingMinutes { get; init; }
    public int? FloatWorkingMinutes { get; init; }
    public bool IsCritical { get; init; }
    public DateOnly? CalculatedStart { get; init; }
    public DateOnly? CalculatedFinish { get; init; }
}

public sealed record ScheduleVariance
{
    public int? WorkingMinutes { get; init; }
    public decimal? Hours { get; init; }
    public DataState State { get; init; } = DataState.Unknown;
}

public sealed record CapacityAnalysis
{
    public decimal? PlannedEffortHours { get; init; }
    public decimal? CapacityHours { get; init; }
    public decimal? LoadHours { get; init; }
    public DataState State { get; init; } = DataState.Unknown;
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed record ReserveAnalysis
{
    public decimal? InitialHours { get; init; }
    public decimal? ConsumedHours { get; init; }
    public decimal? RemainingHours { get; init; }
    public DataState ConsumptionState { get; init; } = DataState.Unknown;
    public DataState RemainingState { get; init; } = DataState.Unknown;
}

public sealed record HealthIndicator
{
    public string Id { get; init; } = string.Empty;
    public string RuleId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Detail { get; init; }
}

public sealed record ViewSummary
{
    public string ViewId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public DataState State { get; init; } = DataState.Unknown;
}
