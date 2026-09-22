using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public enum ExecutiveWbsRowKind
{
    Project,
    Phase,
    WorkPackage,
    DeliveryCard
}

public sealed record ExecutiveWbsRow
{
    public string WbsNumber { get; init; } = string.Empty;
    public string ReferenceCode { get; init; } = string.Empty;
    public string DisplayName { get; init; } = ReaderFacingTextPolicy.MissingEvidenceLabel;
    public ExecutiveWbsRowKind Kind { get; init; }
    public string? ParentReferenceCode { get; init; }
    public int Depth { get; init; }
    public string OwnerLabel { get; init; } = ReaderFacingTextPolicy.MissingOwnerLabel;
    public string StateLabel { get; init; } = ReaderFacingTextPolicy.MissingEvidenceLabel;
    public int? ProgressPercent { get; init; }
    public string ProgressLabel { get; init; } = ReaderFacingTextPolicy.MissingEvidenceLabel;
    public string AttentionLabel { get; init; } = "—";
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedFinish { get; init; }
    public DateOnly? ActualStart { get; init; }
    public DateOnly? ActualFinish { get; init; }
    public decimal? ActualEffortHours { get; init; }
    public decimal? RemainingEffortHours { get; init; }
    public DateTimeOffset? LastOfficialUpdate { get; init; }
    public IReadOnlyList<string> PredecessorCodes { get; init; } = Array.Empty<string>();
    public string DependencyLabel { get; init; } = "Không có tiền nhiệm";
    public string EvidenceSummary { get; init; } = ReaderFacingTextPolicy.MissingEvidenceLabel;
    public string SourceReferenceLabel { get; init; } = ReaderFacingTextPolicy.MissingEvidenceLabel;
    public int SourceOrder { get; init; }
}

public sealed record ExecutiveWbsProjection
{
    public IReadOnlyList<ExecutiveWbsRow> Rows { get; init; } = Array.Empty<ExecutiveWbsRow>();
    public int ProjectCount { get; init; }
    public int PhaseCount { get; init; }
    public int WorkPackageCount { get; init; }
    public int DeliveryCardCount { get; init; }
}
