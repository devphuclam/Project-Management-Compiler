using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed class ManagementAnalysisOrchestrator
{
    private readonly StatusAnalyzer statusAnalyzer;
    private readonly DependencyNetworkAnalyzer dependencyNetworkAnalyzer;
    private readonly ManagementMetricsAnalyzer metricsAnalyzer;

    public ManagementAnalysisOrchestrator(
        StatusAnalyzer? statusAnalyzer = null,
        DependencyNetworkAnalyzer? dependencyNetworkAnalyzer = null,
        ManagementMetricsAnalyzer? metricsAnalyzer = null)
    {
        this.statusAnalyzer = statusAnalyzer ?? new StatusAnalyzer();
        this.dependencyNetworkAnalyzer = dependencyNetworkAnalyzer ?? new DependencyNetworkAnalyzer();
        this.metricsAnalyzer = metricsAnalyzer ?? new ManagementMetricsAnalyzer();
    }

    public ManagementAnalysis Analyze(CanonicalProject project, DateOnly asOfDate)
    {
        var status = statusAnalyzer.Analyze(project, asOfDate);
        var dependency = dependencyNetworkAnalyzer.Analyze(project);
        var metrics = metricsAnalyzer.Analyze(project, dependency);
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
            Capacity = metrics.Capacity,
            Reserve = metrics.Reserve,
            EffortAccounting = metrics.EffortAccounting,
            ExecutionEffort = metrics.ExecutionEffort,
            ForecastState = metrics.ForecastState,
            ForecastFinish = metrics.ForecastFinish,
            HealthIndicators = metrics.HealthIndicators,
            ViewSummaries = status.ViewSummaries.Concat(metrics.ViewSummaries).ToArray(),
            Diagnostics = status.Diagnostics.Concat(dependency.Diagnostics).Concat(metrics.Diagnostics).ToArray()
        };
    }
}
