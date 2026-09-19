namespace ProjectManagementCompiler.Domain;

public sealed record CanonicalProject
{
    public string SchemaVersion { get; init; } = "1.0";
    public Project Project { get; init; } = new();
    public IReadOnlyList<ProjectSource> Sources { get; init; } = Array.Empty<ProjectSource>();
    public ProjectBaseline Baseline { get; init; } = new();
    public IReadOnlyList<Phase> Phases { get; init; } = Array.Empty<Phase>();
    public IReadOnlyList<WorkPackage> WorkPackages { get; init; } = Array.Empty<WorkPackage>();
    public IReadOnlyList<DeliveryCard> DeliveryCards { get; init; } = Array.Empty<DeliveryCard>();
    public IReadOnlyList<MilestoneDecision> Milestones { get; init; } = Array.Empty<MilestoneDecision>();
    public IReadOnlyList<Dependency> Dependencies { get; init; } = Array.Empty<Dependency>();
    public IReadOnlyList<ResponsibilityRole> ResponsibilityRoles { get; init; } = Array.Empty<ResponsibilityRole>();
    public IReadOnlyList<Assignment> Assignments { get; init; } = Array.Empty<Assignment>();
    public CapacityPlan Capacity { get; init; } = new();
    public Reserve Reserve { get; init; } = new();
    public PolicySet Policies { get; init; } = new();
    public ExecutionOverlay ExecutionOverlay { get; init; } = new();
    public ManifestSnapshotMetadata? ImportMetadata { get; init; }
    public SourceExecutionSnapshot SourceExecution { get; init; } = new();
    public IReadOnlyList<ExecutionProposal> ExecutionProposals { get; init; } = Array.Empty<ExecutionProposal>();
    public ManagementEvidence ManagementEvidence { get; init; } = new();
    public IReadOnlyList<SourceReference> Provenance { get; init; } = Array.Empty<SourceReference>();
    public IReadOnlyList<ImportWarning> Warnings { get; init; } = Array.Empty<ImportWarning>();
    public ManagementAnalysis? Analysis { get; init; }
}
