namespace ProjectManagementCompiler.Domain;

public sealed record ImportWarning
{
    public string Id { get; init; } = string.Empty;
    public WarningSeverity Severity { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> AffectedIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
}

