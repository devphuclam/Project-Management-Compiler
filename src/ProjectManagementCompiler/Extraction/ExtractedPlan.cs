using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Extraction;

public sealed record ExtractedValueProvenance
{
    public string Key { get; init; } = string.Empty;
    public string? RawValue { get; init; }
    public SourceReference SourceReference { get; init; } = new();
}

public sealed record ExtractedCardEvidence
{
    public string CardId { get; init; } = string.Empty;
    public string LogicalRoleCode { get; init; } = string.Empty;
    public string CarioRoleCode { get; init; } = string.Empty;
    public string? PredecessorId { get; init; }
    public SourceReference SourceReference { get; init; } = new();
    public IReadOnlyList<ExtractedRoleAssignmentEvidence> RoleAssignments { get; init; } = Array.Empty<ExtractedRoleAssignmentEvidence>();
}

public sealed record ExtractedRoleAssignmentEvidence
{
    public string CardId { get; init; } = string.Empty;
    public string LogicalRoleCode { get; init; } = string.Empty;
    public string CarioRoleCode { get; init; } = string.Empty;
    public SourceReference SourceReference { get; init; } = new();
}

public sealed record ExtractedPlan
{
    public bool HasCanonicalBaseline { get; init; }
    public string SourceId { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string? ResolvedRef { get; init; }
    public DateTimeOffset? CapturedAtUtc { get; init; }
    public IReadOnlyList<SourceDocument> SourceDocuments { get; init; } = Array.Empty<SourceDocument>();
    public Project Project { get; init; } = new();
    public ProjectBaseline Baseline { get; init; } = new();
    public IReadOnlyList<Phase> Phases { get; init; } = Array.Empty<Phase>();
    public IReadOnlyList<WorkPackage> WorkPackages { get; init; } = Array.Empty<WorkPackage>();
    public IReadOnlyList<DeliveryCard> DeliveryCards { get; init; } = Array.Empty<DeliveryCard>();
    public IReadOnlyList<MilestoneDecision> Milestones { get; init; } = Array.Empty<MilestoneDecision>();
    public IReadOnlyList<Dependency> Dependencies { get; init; } = Array.Empty<Dependency>();
    public IReadOnlyList<ResponsibilityRole> ResponsibilityRoles { get; init; } = Array.Empty<ResponsibilityRole>();
    public IReadOnlyList<Assignment> Assignments { get; init; } = Array.Empty<Assignment>();
    public IReadOnlyList<ExtractedCardEvidence> CardEvidence { get; init; } = Array.Empty<ExtractedCardEvidence>();
    public CapacityPlan Capacity { get; init; } = new();
    public Reserve Reserve { get; init; } = new();
    public PolicySet Policies { get; init; } = new();
    public IReadOnlyList<ExtractedValueProvenance> ValueProvenance { get; init; } = Array.Empty<ExtractedValueProvenance>();
    public IReadOnlyList<ImportWarning> Warnings { get; init; } = Array.Empty<ImportWarning>();
}
