namespace ProjectManagementCompiler.Domain;

public enum ManifestImportMode
{
    GitCommit,
    UncommittedPreview
}

public enum ManifestImportClassification
{
    Failed,
    OfficialCommit,
    CandidatePreview,
    UncommittedPreview
}

public enum ManifestValidationResult
{
    Pass,
    PassWithWarnings,
    Fail
}

public enum SourceReadinessState
{
    Unknown,
    Pass,
    Fail
}

public enum SourceRecordingState
{
    NotRecorded,
    Recorded
}

public enum SourceResultState
{
    NotRun,
    Pass,
    Fail,
    Blocked,
    NotApplicable
}

public enum ProposalLifecycle
{
    Draft,
    ReadyForReview,
    StaleBase
}

public enum ManifestSourceRole
{
    RoadmapAuthority,
    WorkPackageAuthority,
    DeliveryCardAuthority,
    ExecutionAuthority,
    RenditionCrossCheck,
    ReadinessEvidence,
    NavigationOnly
}

public sealed record ManifestImportRequest
{
    public const long HardMaxFileBytes = 8 * 1024 * 1024;
    public const long HardMaxTotalBytes = 64 * 1024 * 1024;

    public string RepositoryRoot { get; init; } = string.Empty;
    public string ManifestPath { get; init; } = "planning/project-management-compiler-manifest.json";
    public ManifestImportMode Mode { get; init; } = ManifestImportMode.GitCommit;
    public string? RequestedCommit { get; init; }
    public DateOnly? AnalysisAsOfOverride { get; init; }
    public long MaxFileBytes { get; init; } = 4 * 1024 * 1024;
    public long MaxTotalBytes { get; init; } = 32 * 1024 * 1024;
}

public sealed record ManifestSourceFile
{
    public string RelativePath { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public SourceDocumentFormat Format { get; init; } = SourceDocumentFormat.Text;
}

public sealed record ManifestSourceCapture
{
    public string RepositoryIdentity { get; init; } = string.Empty;
    public string? ResolvedCommit { get; init; }
    public string PreviewIdentity { get; init; } = string.Empty;
    public ManifestImportMode Mode { get; init; }
    public bool IsStable { get; init; }
    public IReadOnlyDictionary<string, ManifestSourceFile> Files { get; init; } =
        new Dictionary<string, ManifestSourceFile>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ManifestDiagnostic> Diagnostics { get; init; } = Array.Empty<ManifestDiagnostic>();
}

public sealed record SourceExecutionEvidence
{
    public string EvidenceId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string RepositoryPath { get; init; } = string.Empty;
    public string? Commit { get; init; }
    public string? ExternalUri { get; init; }
    public string? Description { get; init; }
    public string? Result { get; init; }
    public DateTimeOffset? RecordedAt { get; init; }
    public string? RecordedBy { get; init; }
}

public sealed record SourceExecutionRecord
{
    public CanonicalWorkItemKey Entity { get; init; }
    public SourceRecordingState RecordingState { get; init; }
    public ExecutionState? ExecutionState { get; init; }
    public SourceResultState? ResultState { get; init; }
    public DateOnly? ActualStart { get; init; }
    public DateOnly? ActualFinish { get; init; }
    public decimal? ActualEffortHours { get; init; }
    public decimal? RemainingEffortHours { get; init; }
    public DateTimeOffset? ForecastFinish { get; init; }
    public string? Blocker { get; init; }
    public IReadOnlyList<SourceExecutionEvidence> Evidence { get; init; } = Array.Empty<SourceExecutionEvidence>();
    public DateTimeOffset? LastUpdatedAt { get; init; }
    public string? RecordedBy { get; init; }
    public string SourcePath { get; init; } = string.Empty;
}

public sealed record SourceExecutionSnapshot
{
    public string ProjectId { get; init; } = string.Empty;
    public string BaselineId { get; init; } = string.Empty;
    public string RegisterId { get; init; } = string.Empty;
    public int RegisterRevision { get; init; }
    public DateOnly? StatusDate { get; init; }
    public string TimeZone { get; init; } = string.Empty;
    public IReadOnlyList<SourceExecutionRecord> Records { get; init; } = Array.Empty<SourceExecutionRecord>();
    public string SourcePath { get; init; } = string.Empty;
}

public sealed record ProjectCalendarSet
{
    public string TimeZone { get; init; } = string.Empty;
    public string BaselineCalendarId { get; init; } = string.Empty;
    public string ForecastCalendarId { get; init; } = string.Empty;
    public string DifferenceTreatment { get; init; } = string.Empty;
}

public sealed record ManifestSnapshotMetadata
{
    public string RepositoryIdentity { get; init; } = string.Empty;
    public ManifestImportMode ImportMode { get; init; }
    public ManifestImportClassification Classification { get; init; }
    public string SourceIdentity { get; init; } = string.Empty;
    public string ManifestPath { get; init; } = string.Empty;
    public string ContractVersion { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string BaselineId { get; init; } = string.Empty;
    public string? BaselineVersion { get; init; }
    public int RegisterRevision { get; init; }
    public DateOnly? RegisterStatusDate { get; init; }
    public ManifestValidationResult ValidationResult { get; init; }
    public SourceReadinessState SourceReadiness { get; init; }
    public string SnapshotId { get; init; } = string.Empty;
    public DateTimeOffset ImportedAtUtc { get; init; }
    public int WarningCount { get; init; }
    public int ErrorCount { get; init; }
    public ProjectCalendarSet Calendars { get; init; } = new();
}

public sealed record IdeaEngineeringSnapshot
{
    public CanonicalProject Project { get; init; } = new();
    public ManifestSnapshotMetadata Metadata { get; init; } = new();
    public SourceExecutionSnapshot SourceExecution { get; init; } = new();
    public IReadOnlyList<ManifestDiagnostic> Diagnostics { get; init; } = Array.Empty<ManifestDiagnostic>();
}

public sealed record ManifestImportAttempt
{
    public ManifestImportClassification Classification { get; init; }
    public ManifestImportMode Mode { get; init; }
    public string RepositoryIdentity { get; init; } = string.Empty;
    public string SourceIdentity { get; init; } = string.Empty;
    public DateTimeOffset AttemptedAtUtc { get; init; }
    public ManifestValidationResult ValidationResult { get; init; }
    public SourceReadinessState SourceReadiness { get; init; }
    public IReadOnlyList<ManifestDiagnostic> Diagnostics { get; init; } = Array.Empty<ManifestDiagnostic>();
}

public sealed record ManifestImportResult
{
    public ManifestImportClassification Classification { get; init; }
    public IdeaEngineeringSnapshot? Snapshot { get; init; }
    public ManifestImportAttempt Attempt { get; init; } = new();
    public IReadOnlyList<ManifestDiagnostic> Diagnostics { get; init; } = Array.Empty<ManifestDiagnostic>();
}
