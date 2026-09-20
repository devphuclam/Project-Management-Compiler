namespace ProjectManagementCompiler.Outputs;

public sealed record XlsxPreviewModel
{
    public string FileName { get; init; } = string.Empty;
    public string ExportKind { get; init; } = string.Empty;
    public string ContractVersion { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string SourceIdentity { get; init; } = string.Empty;
    public string SnapshotId { get; init; } = string.Empty;
    public DateOnly? AsOfDate { get; init; }
    public IReadOnlyList<DateOnly> DateAxis { get; init; } = Array.Empty<DateOnly>();
    public IReadOnlyList<XlsxPreviewTaskRow> Tasks { get; init; } = Array.Empty<XlsxPreviewTaskRow>();
    public IReadOnlyList<XlsxPreviewGanttRow> GanttRows { get; init; } = Array.Empty<XlsxPreviewGanttRow>();
    public bool ReadOnly => true;
    public bool Authoritative => false;
}

public sealed record XlsxPreviewTaskRow
{
    public string WorkItemType { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public string Phase { get; init; } = string.Empty;
    public string WorkPackage { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string PlannedStart { get; init; } = string.Empty;
    public string Deadline { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string Team { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string InitialState { get; init; } = string.Empty;
    public decimal? PlannedEffortHours { get; init; }
    public string BaselineAnalysisState { get; init; } = string.Empty;
    public string SourceReference { get; init; } = string.Empty;
}

public sealed record XlsxPreviewGanttRow
{
    public int Level { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Lane { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string OwnerRole { get; init; } = string.Empty;
    public string PlanStart { get; init; } = string.Empty;
    public string PlanFinish { get; init; } = string.Empty;
    public string ActualStart { get; init; } = string.Empty;
    public string ActualFinish { get; init; } = string.Empty;
    public string RecordedPercent { get; init; } = string.Empty;
    public string Critical { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public string SourceReference { get; init; } = string.Empty;
    public IReadOnlyList<string> DailyCells { get; init; } = Array.Empty<string>();
}

public sealed record XlsxPreviewDiagnostic
{
    public string Code { get; init; } = string.Empty;
    public string Severity { get; init; } = "ERROR";
    public string Message { get; init; } = string.Empty;
}

public sealed record XlsxPreviewImportResult
{
    public XlsxPreviewModel? Preview { get; init; }
    public IReadOnlyList<XlsxPreviewDiagnostic> Diagnostics { get; init; } = Array.Empty<XlsxPreviewDiagnostic>();
    public bool IsValid => Preview is not null && Diagnostics.Count == 0;
}

public sealed record XlsxPreviewImportLimits
{
    public long MaxUploadBytes { get; init; } = 8 * 1024 * 1024;
    public long MaxPackageBytes { get; init; } = 8 * 1024 * 1024;
    public long MaxEntryBytes { get; init; } = 4 * 1024 * 1024;
    public int MaxEntryCount { get; init; } = 64;
    public int MaxRowsPerSheet { get; init; } = 5_000;
}
