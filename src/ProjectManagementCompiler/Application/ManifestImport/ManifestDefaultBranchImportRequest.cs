namespace ProjectManagementCompiler.Application.ManifestImport;

public sealed record ManifestDefaultBranchImportRequest
{
    public string RepositoryRoot { get; init; } = string.Empty;
    public string ManifestPath { get; init; } = "planning/project-management-compiler-manifest.json";
    public DateOnly? AnalysisAsOfOverride { get; init; }
    public long MaxFileBytes { get; init; } = 4 * 1024 * 1024;
    public long MaxTotalBytes { get; init; } = 32 * 1024 * 1024;
}
