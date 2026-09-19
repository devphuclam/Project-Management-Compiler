namespace ProjectManagementCompiler.Domain;

public enum ManagementEvidenceDiscoveryState
{
    NotRequested,
    NotConfigured,
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
    Standalone,
    Unmatched,
    Ambiguous,
    Invalid
}

public enum EffectiveEvidenceSelectionStatus
{
    Missing,
    Resolved,
    Conflict
}

public sealed record EvidenceRoleReference
{
    public string Code { get; init; } = string.Empty;
    public string DisplayLabel { get; init; } = string.Empty;
    public string? SourceMeaning { get; init; }
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
    public string? OwnerRoleLabel { get; init; }
    public string? OwnerRoleSourceMeaning { get; init; }
    public EvidenceRoleReference? OwnerRoleReference { get; init; }
    public string? WaitingForRole { get; init; }
    public string? WaitingForRoleLabel { get; init; }
    public string? WaitingForRoleSourceMeaning { get; init; }
    public EvidenceRoleReference? WaitingForRoleReference { get; init; }
    public string? RequiredAuthorityRole { get; init; }
    public string? RequiredAuthorityRoleLabel { get; init; }
    public string? RequiredAuthorityRoleSourceMeaning { get; init; }
    public EvidenceRoleReference? RequiredAuthorityRoleReference { get; init; }
    public string? DueCondition { get; init; }
    public string? DueConditionCode { get; init; }
    public string? DueConditionSummary { get; init; }
    public string? GateEffect { get; init; }
    public string? GateEffectCode { get; init; }
    public string? GateEffectSummary { get; init; }
    public string? BlockerOrDeviation { get; init; }
    public string? BlockerSummary { get; init; }
    public string? PendingActionSummary { get; init; }
    public string? Summary { get; init; }
    public string? ActionSummary { get; init; }
    public string? AffectedTargetSummary { get; init; }
    public string? CompletionCondition { get; init; }
    public string? GateId { get; init; }
    public string? ProposedSuccessorIncrementId { get; init; }
    public string? ProposedSuccessorSummary { get; init; }
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
