using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Application.ManifestImport;

public sealed class ManifestImportApplicationService
{
    private readonly IIdeaEngineeringManifestImporter importer;
    private readonly IProjectCompiler compiler;
    private readonly CompilerApplicationState state;
    private readonly IManifestDefaultBranchResolver defaultBranchResolver;

    public ManifestImportApplicationService(
        IIdeaEngineeringManifestImporter importer,
        IProjectCompiler compiler,
        CompilerApplicationState state,
        IManifestDefaultBranchResolver? defaultBranchResolver = null)
    {
        this.importer = importer;
        this.compiler = compiler;
        this.state = state;
        this.defaultBranchResolver = defaultBranchResolver ?? new ManifestDefaultBranchResolver();
    }

    public async Task<ManifestImportResult> ImportAsync(
        ManifestImportRequest request,
        CarioMappingConfiguration? mapping = null,
        CancellationToken cancellationToken = default)
    {
        var result = await importer.ImportAsync(request, cancellationToken);
        CompilationResult? compiled = null;
        if (result.Snapshot is not null)
        {
            compiled = compiler.BuildImportedResult(result.Snapshot, request.AnalysisAsOfOverride, mapping);
        }

        state.RecordManifestImport(result, compiled);
        return result;
    }

    public async Task<ManifestImportResult> ImportDefaultBranchAsync(
        ManifestDefaultBranchImportRequest request,
        CarioMappingConfiguration? mapping = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateDefaultBranchRequest(request);

        var resolution = await defaultBranchResolver.ResolveAsync(request.RepositoryRoot, cancellationToken);
        if (!resolution.Succeeded)
        {
            throw new ManifestDefaultBranchImportException(resolution);
        }

        return await ImportAsync(new ManifestImportRequest
        {
            RepositoryRoot = request.RepositoryRoot,
            ManifestPath = request.ManifestPath,
            Mode = ManifestImportMode.GitCommit,
            RequestedCommit = resolution.CommitSha,
            AnalysisAsOfOverride = request.AnalysisAsOfOverride,
            MaxFileBytes = request.MaxFileBytes,
            MaxTotalBytes = request.MaxTotalBytes
        }, mapping, cancellationToken);
    }

    public void ClearPreview() => state.ClearActivePreview();

    public void ClearXlsxPreview() => state.ClearXlsxPreview();

    private static void ValidateDefaultBranchRequest(ManifestDefaultBranchImportRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RepositoryRoot);
        if (!string.Equals(
            request.ManifestPath?.Replace('\\', '/'),
            ManifestCaptureSupport.SupportedManifestPath,
            StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The supported manifest path is the sole discovery entry point.",
                nameof(request.ManifestPath));
        }

        if (request.MaxFileBytes <= 0
            || request.MaxTotalBytes <= 0
            || request.MaxTotalBytes < request.MaxFileBytes)
        {
            throw new ArgumentException(
                "Source size limits must be positive and the total limit must cover one file.",
                nameof(request));
        }
    }
}
