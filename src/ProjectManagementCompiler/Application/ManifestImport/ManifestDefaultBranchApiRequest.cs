using ProjectManagementCompiler.Outputs;

namespace ProjectManagementCompiler.Application.ManifestImport;

public sealed record ManifestDefaultBranchApiRequest
{
    public string RepositoryRoot { get; init; } = string.Empty;
    public string ManifestPath { get; init; } = "planning/project-management-compiler-manifest.json";
    public DateOnly? AnalysisAsOfOverride { get; init; }
    public int MaxFileBytes { get; init; } = 4 * 1024 * 1024;
    public int MaxTotalBytes { get; init; } = 32 * 1024 * 1024;
    public CarioMappingConfiguration? Mapping { get; init; }
}
