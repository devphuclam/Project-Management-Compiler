using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Sources;

public sealed record RepositorySnapshot
{
    public string RepositoryId { get; init; } = string.Empty;
    public string RepositoryLabel { get; init; } = string.Empty;
    public string LocationLabel { get; init; } = string.Empty;
    public string? ResolvedRef { get; init; }
    public DateTimeOffset? CapturedAtUtc { get; init; }
    public CaptureState CaptureState { get; init; } = CaptureState.Unknown;
    public IReadOnlyList<SourceDocument> Documents { get; init; } = Array.Empty<SourceDocument>();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}
