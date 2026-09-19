using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Application.ManifestImport;

public interface IIdeaEngineeringManifestImporter
{
    Task<ManifestImportResult> ImportAsync(
        ManifestImportRequest request,
        CancellationToken cancellationToken = default);
}
