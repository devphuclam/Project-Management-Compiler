using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed class ManagementAnalysisOrchestrator
{
    private readonly StatusAnalyzer statusAnalyzer;
    private readonly DependencyNetworkAnalyzer dependencyNetworkAnalyzer;

    public ManagementAnalysisOrchestrator(
        StatusAnalyzer? statusAnalyzer = null,
        DependencyNetworkAnalyzer? dependencyNetworkAnalyzer = null)
    {
        this.statusAnalyzer = statusAnalyzer ?? new StatusAnalyzer();
        this.dependencyNetworkAnalyzer = dependencyNetworkAnalyzer ?? new DependencyNetworkAnalyzer();
    }

    public ManagementAnalysis Analyze(CanonicalProject project, DateOnly asOfDate)
    {
        var status = statusAnalyzer.Analyze(project, asOfDate);
        var dependency = dependencyNetworkAnalyzer.Analyze(project);
        return status with
        {
            CpmState = dependency.State,
            CpmNodes = dependency.Nodes,
            CriticalPathIds = dependency.CriticalPathIds,
            CalculatedFinish = dependency.CalculatedFinish,
            ScheduleVariance = dependency.ScheduleVariance,
            ResourceBaselineScheduleConstraint = new ScheduleConstraintSummary
            {
                ResourceConstraint = project.Capacity.ResourceLogicalRole,
                BaselineConstraint = project.Policies.ResourceConstraint
            },
            Diagnostics = status.Diagnostics.Concat(dependency.Diagnostics).ToArray()
        };
    }
}
