using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed class ManagementAnalysisOrchestrator
{
    private readonly StatusAnalyzer statusAnalyzer;

    public ManagementAnalysisOrchestrator(StatusAnalyzer? statusAnalyzer = null)
    {
        this.statusAnalyzer = statusAnalyzer ?? new StatusAnalyzer();
    }

    public ManagementAnalysis Analyze(CanonicalProject project, DateOnly asOfDate) =>
        statusAnalyzer.Analyze(project, asOfDate);
}
