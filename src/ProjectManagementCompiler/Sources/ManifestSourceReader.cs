using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Sources;

public sealed class ManifestSourceReader : IManifestSourceReader
{
    private readonly ManifestGitObjectReader gitReader;
    private readonly ManifestWorkingTreeReader workingTreeReader;

    public ManifestSourceReader(
        ManifestGitObjectReader? gitReader = null,
        ManifestWorkingTreeReader? workingTreeReader = null)
    {
        this.gitReader = gitReader ?? new ManifestGitObjectReader();
        this.workingTreeReader = workingTreeReader ?? new ManifestWorkingTreeReader();
    }

    public Task<ManifestSourceCapture> CaptureAsync(
        ManifestImportRequest request,
        CancellationToken cancellationToken = default) =>
        request.Mode == ManifestImportMode.UncommittedPreview
            ? workingTreeReader.CaptureAsync(request, cancellationToken)
            : gitReader.CaptureAsync(request, cancellationToken);
}
