namespace ProjectManagementCompiler.Sources;

public sealed record SourceRequest
{
    public required string Location { get; init; }
    public string? Ref { get; init; }
    public bool AllowGitHttps { get; init; }
    public string? ManagementEvidenceIncrementPath { get; init; }
    public int MaxDocumentBytes { get; init; } = 2 * 1024 * 1024;
    public int MaxTotalDocumentBytes { get; init; } = 8 * 1024 * 1024;
}
