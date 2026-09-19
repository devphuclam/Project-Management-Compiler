using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Extraction;

public sealed record PlanningDiscoveryResult
{
    public IReadOnlyList<PlanningDocument> Documents { get; init; } = Array.Empty<PlanningDocument>();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed class IdeaPlanningDiscovery
{
    public PlanningDiscoveryResult Discover(
        RepositorySnapshot snapshot,
        IReadOnlyDictionary<string, PlanningDocumentKind>? manifestDocumentKinds = null)
    {
        var documents = new List<PlanningDocument>();
        var diagnostics = new List<ImportWarning>(snapshot.Diagnostics);

        foreach (var source in snapshot.Documents)
        {
            var path = source.RelativeFile.Replace('\\', '/');
            var manifestKind = default(PlanningDocumentKind);
            var hasManifestKind = manifestDocumentKinds is not null
                && manifestDocumentKinds.TryGetValue(path, out manifestKind);
            if (!hasManifestKind && !SourcePathPolicy.RecognizedPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (hasManifestKind)
            {
                documents.Add(PlanningDocument.CreateForManifest(source, manifestKind));
            }
            else if (PlanningDocument.TryCreate(source, out var planningDocument))
            {
                documents.Add(planningDocument);
            }
        }

        foreach (var required in new[]
        {
            (Path: SourcePathPolicy.RecognizedPaths[1], Kind: PlanningDocumentKind.Doc07),
            (Path: SourcePathPolicy.RecognizedPaths[2], Kind: PlanningDocumentKind.AppendixA)
        })
        {
            if (documents.All(document => document.Kind != required.Kind))
            {
                diagnostics.Add(new ImportWarning
                {
                    Id = $"MISSING_REQUIRED_PLANNING_DOCUMENT:{required.Path}",
                    Severity = WarningSeverity.Error,
                    Code = "MISSING_REQUIRED_PLANNING_DOCUMENT",
                    Message = $"Required planning document '{required.Path}' was not captured.",
                    SourceReferences = [new SourceReference
                    {
                        SourceId = snapshot.RepositoryId,
                        Repository = snapshot.RepositoryLabel,
                        ResolvedRef = snapshot.ResolvedRef,
                        RelativeFile = manifestDocumentKinds?.FirstOrDefault(pair => pair.Value == required.Kind).Key ?? required.Path,
                        ExtractionRule = "idea-planning-required-document"
                    }]
                });
            }
        }

        if (documents.Count == 0)
        {
            diagnostics.Add(new ImportWarning
            {
                Id = "UNSUPPORTED_PLANNING_CONVENTION",
                Severity = WarningSeverity.Error,
                Code = "UNSUPPORTED_PLANNING_CONVENTION",
                Message = "No captured document matched the recognized IDEA planning convention."
            });
        }

        return new PlanningDiscoveryResult { Documents = documents, Diagnostics = diagnostics };
    }
}
