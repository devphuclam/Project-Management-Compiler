namespace ProjectManagementCompiler.Domain;

using System.Text.Json.Serialization;

public sealed record SourceDocument
{
    public string Id { get; init; } = string.Empty;
    public string RelativeFile { get; init; } = string.Empty;
    public SourceDocumentFormat Format { get; init; }
    public long SizeBytes { get; init; }
    [JsonIgnore]
    public string Content { get; init; } = string.Empty;
    public SourceReference SourceReference { get; init; } = new();
}

public sealed record SourceReference
{
    public string SourceId { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string? ResolvedRef { get; init; }
    public string RelativeFile { get; init; } = string.Empty;
    public string? Section { get; init; }
    public string? Table { get; init; }
    public string? Item { get; init; }
    public string ExtractionRule { get; init; } = string.Empty;
    public int? AuthorityRank { get; init; }
    public DataState ConfidenceState { get; init; } = DataState.Unknown;
    public ValidationState ValidationState { get; init; } = ValidationState.Unknown;
}
