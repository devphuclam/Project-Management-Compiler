using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;

namespace ProjectManagementCompiler.Application;

public interface IProjectCompiler
{
    Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken = default);
    CompilationResult Reopen(string json, DateOnly? asOfDate = null, CarioMappingConfiguration? mapping = null);
    ExecutionApplicationResult ApplyExecutionUpdate(CompilationResult current, ExecutionUpdate update, DateOnly? asOfDate = null);
    string SaveJson(CompilationResult result);
    byte[] ExportCarioXlsx(CompilationResult result);
}

public sealed record CompilationRequest
{
    public string SourcePath { get; init; } = string.Empty;
    public string? Ref { get; init; }
    public DateOnly? AsOfDate { get; init; }
    public int MaxDocumentBytes { get; init; } = 2 * 1024 * 1024;
    public int MaxTotalDocumentBytes { get; init; } = 8 * 1024 * 1024;
    public CarioMappingConfiguration Mapping { get; init; } = new();
}

public sealed record CompilationResult
{
    public CanonicalProject Project { get; init; } = new();
    public ManagementAnalysis Analysis { get; init; } = new();
    public ManagementViewSet Views { get; init; } = new();
    public CarioWorkbookModel Cario { get; init; } = new();
    public CarioMappingConfiguration Mapping { get; init; } = new();
    public string SemanticDigest { get; init; } = string.Empty;
    public IReadOnlyList<ImportWarning> Warnings => Project.Warnings
        .Concat(Analysis.Diagnostics)
        .GroupBy(warning => warning.Id, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .OrderBy(warning => warning.Id, StringComparer.Ordinal)
        .ToArray();
}

public sealed record ExecutionApplicationResult
{
    public bool Accepted { get; init; }
    public CompilationResult Result { get; init; } = new();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed class ProjectCompilationException : Exception
{
    public ProjectCompilationException(string phase, IReadOnlyList<ImportWarning> diagnostics)
        : base($"{phase}: {string.Join(" ", diagnostics.Select(diagnostic => diagnostic.Message))}")
    {
        Phase = phase;
        Diagnostics = diagnostics;
    }

    public string Phase { get; }
    public IReadOnlyList<ImportWarning> Diagnostics { get; }
}
