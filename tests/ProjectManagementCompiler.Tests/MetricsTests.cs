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
        TestAssert.True(analysis.ViewSummaries.Any(summary => summary.ViewId == "dashboard.planned-effort" && summary.Value == "512h"), "Dashboard summaries must expose authoritative planned effort.");
        TestAssert.True(analysis.ViewSummaries.Any(summary => summary.ViewId == "dashboard.capacity-hours" && summary.Value == "600h"), "Dashboard summaries must expose capacity hours.");
        TestAssert.True(analysis.ViewSummaries.Any(summary => summary.ViewId == "dashboard.actual-effort" && summary.Value == "UNKNOWN"), "Planning-only dashboard actual effort must remain UNKNOWN.");
        TestAssert.True(analysis.ViewSummaries.Any(summary => summary.ViewId == "dashboard.remaining-effort" && summary.Value == "UNKNOWN"), "Planning-only dashboard remaining effort must remain UNKNOWN.");
        TestAssert.True(analysis.ViewSummaries.Any(summary => summary.ViewId == "dashboard.reserve-initial" && summary.Value == "88h"), "Dashboard summaries must expose initial reserve.");
    }

    public static void PlanningOnlyActualForecastAndHealthRemainUnknown()
    {
        var analysis = Analyze(CaptureCanonicalProject());

        TestAssert.Equal(DataState.Unknown, analysis.ExecutionEffort.ActualState, "Planning-only actual effort must remain unknown.");
        TestAssert.Equal(DataState.Unknown, analysis.ExecutionEffort.RemainingState, "Planning-only remaining effort must remain unknown.");
        TestAssert.Equal(DataState.Unknown, analysis.ForecastState, "Planning-only forecast must remain unknown.");
        TestAssert.True(analysis.HealthIndicators.Any(indicator => indicator.Status == "UNKNOWN"), "Insufficient actual evidence must produce an explicit UNKNOWN health indicator.");
    }

    public static void ActualStartAloneDoesNotFabricateForecastFromCpmFinish()
    {
        var project = CaptureCanonicalProject();
        var updated = new ExecutionOverlayUpdater().Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P01-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 18),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero)
        });
        TestAssert.True(updated.Accepted, "An actual start should be accepted for forecast semantics testing.");

        var analysis = Analyze(updated.Project);

        TestAssert.Equal(DataState.Calculated, analysis.CpmState, "The fixture should still produce a calculated CPM finish.");
        TestAssert.True(analysis.CalculatedFinish is not null, "The fixture should expose an independent CPM calculated finish.");
        TestAssert.Equal(DataState.Unknown, analysis.ForecastState, "Actual start alone is insufficient evidence for a forecast.");
        TestAssert.Equal(null, analysis.ForecastFinish, "An unknown forecast must not reuse the CPM calculated finish.");
        TestAssert.True(
            analysis.ViewSummaries.Any(summary => summary.ViewId == "dashboard.cpm-finish")
            && analysis.ViewSummaries.Any(summary => summary.ViewId == "dashboard.forecast-finish"),
            "Dashboard summaries must expose CPM and forecast as separate concepts.");
    }

    public static void DashboardSummariesExposeKnownActualAndRemainingEffort()
    {
        var project = CaptureCanonicalProject();
        var updated = new ExecutionOverlayUpdater().Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P01-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 18),
            ActualEffortHours = 4m,
            RemainingEffortHours = 2m,
            LastUpdatedAt = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero)
        });
        TestAssert.True(updated.Accepted, "Actual and remaining effort evidence should be accepted.");

        var analysis = Analyze(updated.Project);
        TestAssert.True(analysis.ViewSummaries.Any(summary => summary.ViewId == "dashboard.actual-effort" && summary.Value == "4h" && summary.State == DataState.Calculated), "Dashboard must expose known actual effort.");
        TestAssert.True(analysis.ViewSummaries.Any(summary => summary.ViewId == "dashboard.remaining-effort" && summary.Value == "2h" && summary.State == DataState.Calculated), "Dashboard must expose known remaining effort.");
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
