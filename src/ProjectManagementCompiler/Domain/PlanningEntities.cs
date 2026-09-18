namespace ProjectManagementCompiler.Domain;

public sealed record Project
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateOnly? TargetDate { get; init; }
    public IReadOnlyList<string> SourceIds { get; init; } = Array.Empty<string>();
}

public sealed record ProjectSource
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string? ResolvedRef { get; init; }
    public CaptureState CaptureState { get; init; } = CaptureState.Unknown;
    public DateTimeOffset? CapturedAtUtc { get; init; }
    public IReadOnlyList<SourceDocument> Documents { get; init; } = Array.Empty<SourceDocument>();
}

public sealed record ProjectBaseline
{
    public string Id { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string AuthorityDocumentId { get; init; } = string.Empty;
    public DateOnly? PlanningStart { get; init; }
    public DateOnly? PlanningFinish { get; init; }
    public DateOnly? TargetDate { get; init; }
    public decimal? PlannedEffortHours { get; init; }
    public decimal? ReserveHours { get; init; }
    public decimal? CapacityHours { get; init; }
    public ValidationState ValidationState { get; init; } = ValidationState.Unknown;
}

public abstract record PlannedEntity
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ParentId { get; init; }
    public string PhaseId { get; init; } = string.Empty;
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedFinish { get; init; }
    public decimal? PlannedEffortHours { get; init; }
    public DataState PlannedEffortState { get; init; } = DataState.Unknown;
    public int? PlannedDurationWorkingMinutes { get; init; }
    public DataState DurationState { get; init; } = DataState.Unknown;
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
}

public sealed record Phase : PlannedEntity
{
    public decimal? ReserveHours { get; init; }
    public IReadOnlyList<string> MilestoneIds { get; init; } = Array.Empty<string>();
}

public sealed record WorkPackage : PlannedEntity
{
    public IReadOnlyList<string> DependencyIds { get; init; } = Array.Empty<string>();
    public string CompletionCondition { get; init; } = string.Empty;
    public IReadOnlyList<string> DeliveryCardIds { get; init; } = Array.Empty<string>();
}

public sealed record DeliveryCard : PlannedEntity
{
    public string WorkPackageId { get; init; } = string.Empty;
    public ExecutionState? State { get; init; }
    public IReadOnlyList<string> RoleAssignmentIds { get; init; } = Array.Empty<string>();
}

public sealed record MilestoneDecision
{
    public string Id { get; init; } = string.Empty;
    public MilestoneKind Kind { get; init; }
    public string? ParentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateOnly? PlannedDate { get; init; }
    public ExecutionState? State { get; init; }
    public IReadOnlyList<string> DependencyIds { get; init; } = Array.Empty<string>();
    public decimal? PlannedEffortHours { get; init; }
    public int? PlannedDurationWorkingMinutes { get; init; }
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
}

public sealed record Dependency
{
    public string SubjectId { get; init; } = string.Empty;
    public string SubjectKind { get; init; } = string.Empty;
    public string PredecessorId { get; init; } = string.Empty;
    public string PredecessorKind { get; init; } = string.Empty;
    public DependencyType DependencyType { get; init; }
    public bool AnalysisEligible { get; init; }
    public ValidationState ValidationState { get; init; } = ValidationState.Unknown;
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
}

public sealed record Estimate
{
    public decimal? PlannedEffortHours { get; init; }
    public DataState PlannedEffortState { get; init; } = DataState.Unknown;
    public int? PlannedDurationWorkingMinutes { get; init; }
    public DataState PlannedDurationState { get; init; } = DataState.Unknown;
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedFinish { get; init; }
    public decimal? ActualHours { get; init; }
    public DataState ActualState { get; init; } = DataState.Unknown;
    public decimal? RemainingHours { get; init; }
    public DataState RemainingState { get; init; } = DataState.Unknown;
    public DataState CompletionEvidenceState { get; init; } = DataState.Unknown;
}

public sealed record ResponsibilityRole
{
    public string Code { get; init; } = string.Empty;
    public string? CarioRoleCode { get; init; }
    public string? LogicalRoleCode { get; init; }
    public string? SourceMeaning { get; init; }
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
}

public sealed record Assignment
{
    public string WorkItemId { get; init; } = string.Empty;
    public string LogicalRoleCode { get; init; } = string.Empty;
    public string? CarioRoleCode { get; init; }
    public string? ConcreteIdentity { get; init; }
    public string? MappingStatus { get; init; }
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
}

public sealed record CapacityPlan
{
    public decimal? CapacityHours { get; init; }
    public string? SourceResourcePolicy { get; init; }
    public string? ResourceLogicalRole { get; init; }
    public string EffortAccountingLevel { get; init; } = string.Empty;
    public CalendarDefinition Calendar { get; init; } = new();
}

public sealed record CalendarDefinition
{
    public IReadOnlyList<DayOfWeek> WorkingWeekdays { get; init; } = Array.Empty<DayOfWeek>();
    public decimal? HoursPerWorkingDay { get; init; }
}

public sealed record Reserve
{
    public decimal? InitialHours { get; init; }
    public decimal? ConsumedHours { get; init; }
    public decimal? RemainingHours { get; init; }
    public DataState ConsumptionState { get; init; } = DataState.Unknown;
    public DataState RemainingState { get; init; } = DataState.Unknown;
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
}

public sealed record PolicySet
{
    public int? WorkInProgressLimit { get; init; }
    public string? ResourceConstraint { get; init; }
    public IReadOnlyList<string> Rules { get; init; } = Array.Empty<string>();
}
