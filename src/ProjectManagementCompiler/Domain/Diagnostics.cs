namespace ProjectManagementCompiler.Domain;

public sealed record ImportWarning
{
    public string Id { get; init; } = string.Empty;
    public WarningSeverity Severity { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> AffectedIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
    public string? EntityKind { get; init; }
    public string? EntityId { get; init; }
    public string? SourcePath { get; init; }
    public string? Field { get; init; }
    public string? RecommendedAction { get; init; }
}

public sealed record ManifestDiagnostic
{
    public string Code { get; init; } = string.Empty;
    public WarningSeverity Severity { get; init; }
    public string? EntityKind { get; init; }
    public string? EntityId { get; init; }
    public string? SourcePath { get; init; }
    public string? Field { get; init; }
    public string Message { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;

    public ImportWarning ToImportWarning() => new()
    {
        Id = $"{Code}:{EntityKind}:{EntityId}:{SourcePath}:{Field}",
        Code = Code,
        Severity = Severity,
        Message = Message,
        AffectedIds = EntityId is null ? Array.Empty<string>() : [EntityId],
        EntityKind = EntityKind,
        EntityId = EntityId,
        SourcePath = SourcePath,
        Field = Field,
        RecommendedAction = RecommendedAction,
        SourceReferences = SourcePath is null
            ? Array.Empty<SourceReference>()
            : [new SourceReference
            {
                Repository = "IDEAEngineering",
                RelativeFile = SourcePath,
                ExtractionRule = "manifest-import-diagnostic",
                ConfidenceState = DataState.Known,
                ValidationState = Severity == WarningSeverity.Error ? ValidationState.InvalidSourceEvidence : ValidationState.Warning
            }]
    };
}
