using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class WorkAuthorityCharacterizationTests
{
    public static void SyntheticOfficialExecutionNeverFallsBackToLegacyOverlay()
    {
        var baseline = WorkTestFixtures.SyntheticProject();
        var officialWithSourceRecord = baseline with
        {
            ImportMetadata = WorkTestFixtures.OfficialMetadata(),
            SourceExecution = new SourceExecutionSnapshot
            {
                Records = [WorkTestFixtures.SourceRecord("P01", ExecutionState.InProgress, actualHours: 0m, remainingHours: 3m)]
            },
            ExecutionOverlay = new ExecutionOverlay
            {
                Records = [WorkTestFixtures.LegacyRecord("P01", ExecutionState.Completed, actualHours: 99m)]
            }
        };

        var sourceRecord = ExecutionTruthResolver.ForCard(officialWithSourceRecord, "P01");

        TestAssert.True(sourceRecord?.IsRecorded == true, "The official source record should be the execution authority.");
        TestAssert.Equal(ExecutionState.InProgress, sourceRecord!.ExecutionState, "Official source state must outrank a conflicting legacy overlay.");
        TestAssert.Equal(0m, sourceRecord.ActualEffortHours, "Known zero source effort must remain zero rather than falling back to the legacy value.");
        TestAssert.Equal(3m, sourceRecord.RemainingEffortHours, "Remaining source effort must come from the source execution record.");

        var officialWithoutSourceRecord = officialWithSourceRecord with
        {
            SourceExecution = new SourceExecutionSnapshot()
        };
        TestAssert.True(
            ExecutionTruthResolver.ForCard(officialWithoutSourceRecord, "P01") is null,
            "An official source snapshot with no card record must not fall back to the legacy execution overlay.");

        var legacyOnly = baseline with
        {
            ExecutionOverlay = new ExecutionOverlay
            {
                Records = [WorkTestFixtures.LegacyRecord("P01", ExecutionState.Completed, actualHours: 5m)]
            }
        };
        var legacyRecord = ExecutionTruthResolver.ForCard(legacyOnly, "P01");
        TestAssert.True(legacyRecord?.IsRecorded == true, "A project without source execution authority should retain its supported legacy overlay behavior.");
        TestAssert.Equal(ExecutionState.Completed, legacyRecord!.ExecutionState, "Legacy state should remain usable only on the legacy path.");
    }

    public static void SyntheticDependencyProjectionPreservesEligibilityAndTypedDirectEdges()
    {
        var project = WorkTestFixtures.SyntheticProject();
        var views = new ManagementViewProjector().Build(project, new ManagementAnalysis(), WorkTestFixtures.ReportingDate);
        var network = views.DependencyNetwork;

        var supportedDirect = network.Edges.Single(edge =>
            edge.SubjectKey == "DeliveryCard:P02" && edge.PredecessorKey == "DeliveryCard:P01");
        TestAssert.True(supportedDirect.IncludedInAnalysis, "A supported direct card dependency should be eligible for primary dependency detail.");

        var chainEdge = network.Edges.Single(edge =>
            edge.SubjectKey == "DeliveryCard:P03" && edge.PredecessorKey == "DeliveryCard:P02");
        TestAssert.True(chainEdge.IncludedInAnalysis, "The second supported direct edge should remain a separate direct relationship.");
        TestAssert.False(network.Edges.Any(edge =>
            edge.SubjectKey == "DeliveryCard:P03" && edge.PredecessorKey == "DeliveryCard:P01"),
            "Dependency projection must not synthesize the transitive P03 → P01 relationship.");

        var packageTraceability = network.Edges.Single(edge => edge.SubjectKey == "WorkPackage:WP-B");
        TestAssert.False(packageTraceability.IncludedInAnalysis, "Work Package traceability must not be treated as a supported card dependency.");
        TestAssert.Equal("WORK_PACKAGE_TRACEABILITY", packageTraceability.Reason, "Excluded Work Package edges must retain their authoritative exclusion reason.");

        var invalidEvidence = network.Edges.Single(edge => edge.PredecessorKey == "DeliveryCard:P99");
        TestAssert.False(invalidEvidence.IncludedInAnalysis, "An invalid or missing endpoint must not become a supported dependency claim.");
        TestAssert.Equal("INVALID_SOURCE_EVIDENCE", invalidEvidence.Reason, "Invalid source evidence must remain explicitly excluded.");

        var unsupportedType = network.Edges.Single(edge => edge.SubjectKey == "DeliveryCard:P04" && edge.PredecessorKey == "DeliveryCard:P02");
        TestAssert.False(unsupportedType.IncludedInAnalysis, "An unsupported relationship type must not become a supported dependency claim.");
        TestAssert.Equal("UNSUPPORTED_DEPENDENCY_TYPE", unsupportedType.Reason, "Unsupported relationship types must retain their existing exclusion reason.");

        TestAssert.True(
            network.Nodes.Any(node => node.Key == "DeliveryCard:P01")
                && network.Nodes.Any(node => node.Key == "Milestone:P01"),
            "The dependency graph must keep same-raw-ID items distinct by canonical kind.");
        TestAssert.False(
            CanonicalWorkItemKey.DeliveryCard("P01") == CanonicalWorkItemKey.Milestone("P01"),
            "Gantt cross-selection identity must remain kind-qualified when raw IDs collide.");

        var gantt = new GanttProjector().Build(project, new ManagementAnalysis(), WorkTestFixtures.ReportingDate);
        TestAssert.True(gantt.Items.Any(item => item.WorkItemId == "P01"), "The synthetic Gantt should expose the Delivery Card row.");
        TestAssert.True(gantt.Milestones.Any(item => item.MilestoneId == "P01"), "The synthetic Gantt should expose the same-raw-ID milestone separately.");
    }
}
