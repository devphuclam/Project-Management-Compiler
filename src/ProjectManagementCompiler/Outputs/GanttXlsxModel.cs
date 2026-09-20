using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Outputs;

public sealed record GanttXlsxModel
{
    public string ProjectName { get; init; } = string.Empty;
    public string SnapshotScope { get; init; } = string.Empty;
    public string SourceIdentity { get; init; } = string.Empty;
    public string SnapshotId { get; init; } = string.Empty;
    public DateOnly AsOfDate { get; init; }
    public WbsProjection Wbs { get; init; } = new();
    public GanttProjection Gantt { get; init; } = new();
}
