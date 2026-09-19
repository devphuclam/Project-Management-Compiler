namespace ProjectManagementCompiler.Domain;

public enum ManagementEvidenceDiscoveryState
{
    NotRequested,
    Known,
    Unknown,
    Ambiguous,
    Unavailable
}

public enum ManagementEvidenceKind
{
    ControlEnvelope,
    ReadinessCheck,
    GateExecution,
    GateOutcome,
    DecisionRecord,
    HumanAction,
    ChecklistContext
}

public enum EvidenceReconciliationStatus
{
    Matched,
    Unmatched,
    Ambiguous,
    Invalid
}

public sealed record EvidenceTarget
{
    public string Kind { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;

    public override string ToString() => $"{Kind}:{Id}";
}

public sealed record ManagementEvidenceObservation
{
    public string Id { get; init; } = string.Empty;
    public ManagementEvidenceKind EvidenceKind { get; init; }
    public string SourceRecordId { get; init; } = string.Empty;
    public string? RawTargetKind { get; init; }
    public string? RawTargetId { get; init; }
    public EvidenceTarget? ExplicitTarget { get; init; }
    public string? StateCode { get; init; }
    public string? StateMeaning { get; init; }
    public string? ResultCode { get; init; }
    public string? ResultMeaning { get; init; }
    public DateOnly? EvidenceDate { get; init; }
    public DateTimeOffset? ObservedAtUtc { get; init; }
    public string? OwnerRole { get; init; }
    public string? WaitingForRole { get; init; }
    public string? DueCondition { get; init; }
    public string? GateEffect { get; init; }
    public string? BlockerOrDeviation { get; init; }
    public IReadOnlyList<string> EvidenceLinks { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
    public int? AuthorityRank { get; init; }
    public string? AuthorityKind { get; init; }
    public ValidationState ValidationState { get; init; } = ValidationState.Unknown;
}

public sealed record EvidenceReconciliation
{
    public string ObservationId { get; init; } = string.Empty;
    public EvidenceReconciliationStatus Status { get; init; }
    public EvidenceTarget? ResolvedTarget { get; init; }
    public IReadOnlyList<EvidenceTarget> CandidateTargets { get; init; } = Array.Empty<EvidenceTarget>();
    public string RuleId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
}

public sealed record ManagementEvidence
{
    public ManagementEvidenceDiscoveryState DiscoveryState { get; init; } = ManagementEvidenceDiscoveryState.NotRequested;
    public string? IncrementPath { get; init; }
    public string? IncrementId { get; init; }
    public string? IncrementPhaseId { get; init; }
    public string? IncrementName { get; init; }
    public string? IncrementStatus { get; init; }
    public DateTimeOffset? CapturedAtUtc { get; init; }
    public IReadOnlyList<ManagementEvidenceObservation> Observations { get; init; } = Array.Empty<ManagementEvidenceObservation>();
    public IReadOnlyList<EvidenceReconciliation> Reconciliations { get; init; } = Array.Empty<EvidenceReconciliation>();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}
