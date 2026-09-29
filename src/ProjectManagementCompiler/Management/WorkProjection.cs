using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed record WorkProjection
{
    public string? CurrentPhaseId { get; init; }
    public IReadOnlyList<WorkPhaseProjection> Phases { get; init; } = Array.Empty<WorkPhaseProjection>();
    public IReadOnlyList<WorkPackageProjection> WorkPackages { get; init; } = Array.Empty<WorkPackageProjection>();
    public IReadOnlyList<WorkCardProjection> Cards { get; init; } = Array.Empty<WorkCardProjection>();
}

public sealed record WorkPhaseProjection
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Order { get; init; }
    public IReadOnlyList<string> WorkPackageIds { get; init; } = Array.Empty<string>();
}

public sealed record WorkPackageProjection
{
    public string Id { get; init; } = string.Empty;
    public string PhaseId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Order { get; init; }
    public IReadOnlyList<string> DeliveryCardIds { get; init; } = Array.Empty<string>();
}

public sealed record WorkCardProjection
{
    public CanonicalWorkItemKey Key { get; init; }
    public string Name { get; init; } = string.Empty;
    public string PhaseId { get; init; } = string.Empty;
    public string WorkPackageId { get; init; } = string.Empty;
    public WorkCardOrder Order { get; init; } = new();
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedFinish { get; init; }
    public decimal? PlannedEffortHours { get; init; }
    public DataState PlannedEffortState { get; init; } = DataState.Unknown;
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
}

public sealed record WorkCardOrder
{
    public int? Phase { get; init; }
    public int? WorkPackage { get; init; }
    public int Card { get; init; }
}
