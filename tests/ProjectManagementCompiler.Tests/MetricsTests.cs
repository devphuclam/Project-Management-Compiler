using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class MetricsTests
{
    public static void DashboardUsesAuthoritativeWorkPackageEffortAndReserveSemantics()
    {
        var analysis = Analyze(CaptureCanonicalProject());

        TestAssert.Equal(512m, analysis.Capacity.PlannedEffortHours, "Capacity analysis must use authoritative work-package effort.");
        TestAssert.Equal(512m, analysis.Capacity.LoadHours, "Capacity load must not double-count delivery-card detail effort.");
        TestAssert.Equal(600m, analysis.Capacity.CapacityHours, "Capacity must retain the source capacity value.");
        TestAssert.Equal(DataState.Calculated, analysis.Capacity.State, "Known effort and capacity must produce a calculated load summary.");
        TestAssert.Equal(88m, analysis.Reserve.InitialHours, "Reserve analysis must retain initial reserve.");
        TestAssert.Equal(DataState.NotRun, analysis.Reserve.ConsumptionState, "Planning-only reserve consumption must remain NOT_RUN.");
        TestAssert.Equal(DataState.Unknown, analysis.Reserve.RemainingState, "Planning-only reserve remaining must remain UNKNOWN.");
        TestAssert.Equal(512m, analysis.EffortAccounting!.AuthoritativeEffortHours, "Effort reconciliation must identify the authoritative total.");
        TestAssert.Equal(212m, analysis.EffortAccounting.DetailedEffortHours, "Effort reconciliation must retain card-level detail separately.");
    }

    public static void PlanningOnlyActualForecastAndHealthRemainUnknown()
    {
        var analysis = Analyze(CaptureCanonicalProject());

        TestAssert.Equal(DataState.Unknown, analysis.ExecutionEffort.ActualState, "Planning-only actual effort must remain unknown.");
        TestAssert.Equal(DataState.Unknown, analysis.ExecutionEffort.RemainingState, "Planning-only remaining effort must remain unknown.");
        TestAssert.Equal(DataState.Unknown, analysis.ForecastState, "Planning-only forecast must remain unknown.");
        TestAssert.True(analysis.HealthIndicators.Any(indicator => indicator.Status == "UNKNOWN"), "Insufficient actual evidence must produce an explicit UNKNOWN health indicator.");
    }

    private static ManagementAnalysis Analyze(CanonicalProject project) =>
        new ManagementAnalysisOrchestrator().Analyze(project, new DateOnly(2026, 9, 28));

    private static CanonicalProject CaptureCanonicalProject()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering");
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(new SourceRequest { Location = root }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var resolution = AuthorityResolution.Resolve(snapshot);
        return new CanonicalProjectNormalizer().Normalize(new IdeaEngineeringExtractor().Extract(resolution));
    }
}
