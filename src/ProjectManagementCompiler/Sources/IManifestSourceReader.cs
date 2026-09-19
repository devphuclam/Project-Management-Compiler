using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Sources;

public interface IManifestSourceReader
{
    Task<ManifestSourceCapture> CaptureAsync(
        ManifestImportRequest request,
        CancellationToken cancellationToken = default);
}
