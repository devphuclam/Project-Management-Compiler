using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class GanttTests
{
    public static void GanttKeepsPlanAndAddsActualAndAlertLanes()
    {
        var project = CaptureCanonicalProject();
        var update = new ExecutionOverlayUpdater().Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 25),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)
        });
        var analyzed = new ManagementAnalysisOrchestrator().Analyze(update.Project, new DateOnly(2026, 9, 28));
        var gantt = new GanttProjector().Build(update.Project, analyzed, new DateOnly(2026, 9, 28));
        var item = gantt.Items.Single(item => item.WorkItemId == "P04-A");

        var plan = item.Lanes.Single(lane => lane.Lane == GanttLane.Plan);
        var actual = item.Lanes.Single(lane => lane.Lane == GanttLane.Actual);
        var alert = item.Lanes.Single(lane => lane.Lane == GanttLane.Alert && lane.AlertCode == "OVERDUE");

        TestAssert.Equal(new DateOnly(2026, 9, 25), plan.Start, "PLAN lane must keep the baseline start.");
        TestAssert.Equal(new DateOnly(2026, 9, 25), plan.Finish, "PLAN lane must keep the baseline finish.");
        TestAssert.Equal(new DateOnly(2026, 9, 25), actual.Start, "ACTUAL lane must use the recorded actual start.");
        TestAssert.True(actual.Finish is null, "In-progress ACTUAL lane must preserve the missing actual finish.");
        TestAssert.True(actual.IsOpenEnded, "In-progress ACTUAL lane must be marked open-ended for presentation.");
        TestAssert.Equal("P04-A", alert.WorkItemId, "ALERT lane must retain the canonical work-item ID.");
        TestAssert.Equal(
            string.Join(",", project.Assignments.Where(assignment => assignment.WorkItemId == "P04-A").Select(assignment => assignment.LogicalRoleCode).OrderBy(role => role, StringComparer.Ordinal)),
            string.Join(",", item.LogicalRoles),
            "Gantt must expose available logical responsibility roles without fabricating concrete identities.");
        TestAssert.True(item.IsCritical, "Gantt must expose dependency-critical highlighting from shared analysis.");
        TestAssert.True(item.HasExecutionEvidence, "Gantt must distinguish an explicit execution overlay from planning-only state.");
        TestAssert.True(item.SourceReferences.Count > 0, "Gantt delivery-card rows must retain safe item-level source references.");
    }

    public static void GanttLeavesActualLaneUnknownWithoutExecutionEvidence()
    {
        var project = CaptureCanonicalProject();
        var asOfDate = new DateOnly(2026, 9, 28);
        var analysis = new ManagementAnalysisOrchestrator().Analyze(project, asOfDate);
        var item = new GanttProjector().Build(project, analysis, asOfDate).Items.Single(item => item.WorkItemId == "P01-A");

        TestAssert.True(item.Lanes.Any(lane => lane.Lane == GanttLane.Plan), "Every card must have a PLAN lane.");
        TestAssert.False(item.Lanes.Any(lane => lane.Lane == GanttLane.Actual && lane.Start is not null), "Planning-only input must not fabricate an ACTUAL bar.");
        TestAssert.False(item.Lanes.Any(lane => lane.Lane == GanttLane.Alert && lane.AlertCode == "OVERDUE"), "A card without execution evidence must not fabricate active overdue actuals.");
    }

    public static void GanttKeepsInProgressActualLaneOpenForPresentation()
    {
        var project = CaptureCanonicalProject();
        var asOfDate = new DateOnly(2026, 9, 28);
        var update = new ExecutionOverlayUpdater().Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 25),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)
        });
        var analysis = new ManagementAnalysisOrchestrator().Analyze(update.Project, asOfDate);
        var actual = new GanttProjector().Build(update.Project, analysis, asOfDate)
            .Items.Single(item => item.WorkItemId == "P04-A")
            .Lanes.Single(lane => lane.Lane == GanttLane.Actual);

        TestAssert.True(actual.Finish is null, "An in-progress ACTUAL lane must preserve the missing actual finish instead of storing as-of as completion.");
    }

    public static void GanttRetainsFinishOnlyExecutionEvidenceForInspector()
    {
        var project = CaptureCanonicalProject();
        var asOfDate = new DateOnly(2026, 9, 28);
        var update = new ExecutionOverlayUpdater().Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P05-A",
            ExecutionState = ExecutionState.Completed,
            ActualFinish = asOfDate,
            LastUpdatedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 30, TimeSpan.Zero)
        });
        var analysis = new ManagementAnalysisOrchestrator().Analyze(update.Project, asOfDate);
        var item = new GanttProjector().Build(update.Project, analysis, asOfDate)
            .Items.Single(item => item.WorkItemId == "P05-A");
        var actual = item.Lanes.Single(lane => lane.Lane == GanttLane.Actual);

        TestAssert.True(item.HasExecutionEvidence, "Finish-only completion must remain explicit execution evidence.");
        TestAssert.True(actual.Start is null, "Finish-only execution evidence must not fabricate an actual start.");
        TestAssert.Equal(asOfDate, actual.Finish, "Finish-only execution evidence must retain its authored actual finish.");
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
