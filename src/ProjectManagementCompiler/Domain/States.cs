namespace ProjectManagementCompiler.Domain;

public enum DataState
{
    Known,
    Unknown,
    NotRun,
    Invalid,
    Blocked
}

public enum ValidationState
{
    Known,
    Warning,
    Blocked,
    Unknown,
    InvalidSourceEvidence
}

public enum ExecutionState
{
    NotStarted,
    InProgress,
    Completed,
    Suspended,
    Cancelled
}

public enum CaptureState
{
    Known,
    Blocked,
    Unknown
}

public enum DependencyType
{
    FinishToStart,
    StartToStart,
    FinishToFinish,
    StartToFinish
}

public enum SourceDocumentFormat
{
    Markdown,
    Html,
    Text
}

public enum WarningSeverity
{
    Info,
    Warning,
    Error
}

public enum MilestoneKind
{
    Decision,
    Milestone
}

