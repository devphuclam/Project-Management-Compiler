using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Outputs;

namespace ProjectManagementCompiler.Application.ManifestImport;

public sealed class ManifestImportApplicationService
{
    private readonly IIdeaEngineeringManifestImporter importer;
    private readonly IProjectCompiler compiler;
    private readonly CompilerApplicationState state;

    public ManifestImportApplicationService(
        IIdeaEngineeringManifestImporter importer,
        IProjectCompiler compiler,
        CompilerApplicationState state)
    {
        this.importer = importer;
        this.compiler = compiler;
        this.state = state;
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

    public void ClearPreview() => state.ClearActivePreview();

    public void ClearXlsxPreview() => state.ClearXlsxPreview();
}
