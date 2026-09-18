using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class CpmTests
{
    public static void CpmUsesNormalizedDurationAndExcludesInvalidSourceEdges()
    {
        var project = CaptureCanonicalProject();
        var result = new DependencyNetworkAnalyzer().Analyze(project);
        var first = result.Nodes.Single(node => node.NodeId == "P01-A");
        var successor = result.Nodes.Single(node => node.NodeId == "P01-B");

        TestAssert.Equal(DataState.Calculated, result.State, "Acyclic fixture durations should produce calculated CPM.");
        TestAssert.Equal(0, first.EarliestStartWorkingMinutes, "A root dependency node must start at offset zero.");
        TestAssert.Equal(480, first.EarliestFinishWorkingMinutes, "CPM must consume normalized duration, not four hours of card effort.");
        TestAssert.Equal(480, successor.EarliestStartWorkingMinutes, "Finish-to-start successor must begin after predecessor duration.");
        TestAssert.True(result.CriticalPathIds.Contains("P01-A"), "Zero-float dependency nodes must be reported on the critical path.");
        TestAssert.True(result.Diagnostics.Any(diagnostic => diagnostic.Code == "INVALID_SOURCE_DEPENDENCY"), "Invalid source evidence must remain visible without blocking valid CPM.");
    }

    public static void CpmDetectsCyclesAndLeavesBaselineUntouched()
    {
        var project = CaptureCanonicalProject();
        var cycle = project with
        {
            Dependencies = project.Dependencies.Append(new Dependency
            {
                SubjectId = "P01-A",
                SubjectKind = "DeliveryCard",
                PredecessorId = "P01-B",
                PredecessorKind = "DeliveryCard",
                DependencyType = DependencyType.FinishToStart,
                AnalysisEligible = true,
                ValidationState = ValidationState.Known
            }).ToArray()
        };

        var result = new DependencyNetworkAnalyzer().Analyze(cycle);

        TestAssert.Equal(DataState.Unknown, result.State, "A dependency cycle must make CPM unknown.");
        TestAssert.True(result.Diagnostics.Any(diagnostic => diagnostic.Code == "DEPENDENCY_CYCLE"), "Cycle failure must be explicit.");
        TestAssert.Equal(project.Baseline.PlanningFinish, cycle.Baseline.PlanningFinish, "CPM must not mutate the source baseline.");
    }

    public static void CpmRejectsUnsupportedEligibleEdges()
    {
        var project = CaptureCanonicalProject() with
        {
            Dependencies =
            [
                new Dependency
                {
                    SubjectId = "P01-B",
                    SubjectKind = "DeliveryCard",
                    PredecessorId = "P01-A",
                    PredecessorKind = "DeliveryCard",
                    DependencyType = DependencyType.StartToStart,
                    AnalysisEligible = true,
                    ValidationState = ValidationState.Known
                }
            ]
        };

        var result = new DependencyNetworkAnalyzer().Analyze(project);

        TestAssert.Equal(DataState.Unknown, result.State, "Unsupported eligible dependency types must block CPM.");
        TestAssert.True(result.Diagnostics.Any(diagnostic => diagnostic.Code == "UNSUPPORTED_DEPENDENCY_TYPE"), "Unsupported dependency types must be diagnosed.");
    }

    public static void OrchestratorCombinesCpmAndExecutionAnalysis()
    {
        var project = CaptureCanonicalProject();
        var analysis = new ManagementAnalysisOrchestrator().Analyze(project, new DateOnly(2026, 9, 28));

        TestAssert.Equal(DataState.Calculated, analysis.CpmState, "Orchestrator must expose calculated dependency CPM.");
        TestAssert.True(analysis.CpmNodes.Count > 0, "Orchestrator must expose CPM node metrics.");
        TestAssert.True(analysis.CriticalPathIds.Count > 0, "Orchestrator must expose dependency critical-path IDs.");
        TestAssert.True(analysis.Alerts.Any(alert => alert.AlertCode == "START_DELAY"), "Orchestrator must retain execution status alerts alongside CPM.");
    }

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
