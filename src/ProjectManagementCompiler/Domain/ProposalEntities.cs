namespace ProjectManagementCompiler.Domain;

public sealed record ExecutionProposal
{
    public string Id { get; init; } = string.Empty;
    public string BaseSnapshotId { get; init; } = string.Empty;
    public int ExpectedRegisterRevision { get; init; }
    public string TargetKind { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public ProposalLifecycle Lifecycle { get; init; } = ProposalLifecycle.Draft;
    public IReadOnlyDictionary<string, string?> ProposedChanges { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<SourceExecutionEvidence> Evidence { get; init; } = Array.Empty<SourceExecutionEvidence>();
    public IReadOnlyList<ManifestDiagnostic> Diagnostics { get; init; } = Array.Empty<ManifestDiagnostic>();
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record ProposalPreviewResult
{
    public bool IsAuthoritative { get; init; }
    public bool IsEstimated { get; init; }
    public string Label { get; init; } = "PROPOSAL_PREVIEW";
    public string BaseSnapshotId { get; init; } = string.Empty;
    public IReadOnlyList<ManifestDiagnostic> Diagnostics { get; init; } = Array.Empty<ManifestDiagnostic>();
    public SourceExecutionSnapshot EffectiveExecution { get; init; } = new();
}
