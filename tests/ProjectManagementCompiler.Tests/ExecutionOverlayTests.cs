using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ExecutionOverlayTests
{
    public static void PlanningOnlyCanonicalProjectStartsWithEmptyExecutionOverlay()
    {
        var project = CaptureCanonicalProject();

        TestAssert.True(project.ExecutionOverlay.Records.Count == 0, "Planning-only extraction must not invent execution records.");
    }

    public static void ManualUpdateStoresActualEvidenceWithoutChangingPlan()
    {
        var project = CaptureCanonicalProject();
        var card = project.DeliveryCards.Single(candidate => candidate.Id == "P01-A");
        var update = new ExecutionUpdate
        {
            WorkItemId = card.Id,
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 24),
            ActualEffortHours = 8m,
            RemainingEffortHours = 6m,
            LastUpdatedAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero),
            Note = "Manual progress evidence"
        };

        var result = new ExecutionOverlayUpdater().Apply(project, update);

        TestAssert.True(result.Accepted, "A valid manual update should be accepted.");
        TestAssert.Equal(1, result.Project.ExecutionOverlay.Records.Count, "The update should create one overlay record.");
        var record = result.Project.ExecutionOverlay.Records.Single();
        TestAssert.Equal(card.Id, record.WorkItemId, "The overlay must use the canonical work-item ID.");
        TestAssert.Equal(ExecutionState.InProgress, record.ExecutionState, "The overlay must retain the authored execution state.");
        TestAssert.Equal(new DateOnly(2026, 9, 24), record.ActualStart, "Actual start must be stored independently.");
        TestAssert.Equal(DataState.Known, record.ActualStartState, "An entered actual start must be known.");
        TestAssert.Equal(8m, record.ActualEffortHours, "Actual effort must remain separate from planned effort.");
        TestAssert.Equal(6m, record.RemainingEffortHours, "Remaining effort must remain explicit.");
        TestAssert.True(record.ActualFinish is null, "An in-progress update must not invent an actual finish.");
        TestAssert.Equal(card.PlannedStart, result.Project.DeliveryCards.Single(candidate => candidate.Id == card.Id).PlannedStart, "Manual execution must not mutate planned start.");
        TestAssert.Equal(card.PlannedFinish, result.Project.DeliveryCards.Single(candidate => candidate.Id == card.Id).PlannedFinish, "Manual execution must not mutate planned finish.");
    }

    public static void InvalidActualFinishBeforeStartIsRejectedWithoutChangingOverlay()
    {
        var project = CaptureCanonicalProject();
        var update = new ExecutionUpdate
        {
            WorkItemId = "P01-A",
            ExecutionState = ExecutionState.Completed,
            ActualStart = new DateOnly(2026, 9, 24),
            ActualFinish = new DateOnly(2026, 9, 23),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero)
        };

        var result = new ExecutionOverlayUpdater().Apply(project, update);

        TestAssert.False(result.Accepted, "An actual finish before actual start must be rejected.");
        TestAssert.True(result.Diagnostics.Any(diagnostic => diagnostic.Code == "INVALID_EXECUTION_UPDATE"), "Invalid execution evidence must produce a structured diagnostic.");
        TestAssert.True(result.Project.ExecutionOverlay.Records.Count == 0, "Rejected execution evidence must not be persisted.");
    }

    public static void InvalidWorkItemAndNegativeEffortAreRejected()
    {
        var project = CaptureCanonicalProject();
        var result = new ExecutionOverlayUpdater().Apply(project, new ExecutionUpdate
        {
            WorkItemId = "UNKNOWN-CARD",
            ExecutionState = ExecutionState.InProgress,
            ActualEffortHours = -1m,
            LastUpdatedAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero)
        });

        TestAssert.False(result.Accepted, "An update for a missing work item must be rejected.");
        TestAssert.True(result.Diagnostics.Any(diagnostic => diagnostic.Code == "INVALID_EXECUTION_UPDATE"), "Invalid target/evidence must produce a structured diagnostic.");
        TestAssert.True(result.Project.ExecutionOverlay.Records.Count == 0, "Rejected target evidence must not create an overlay record.");
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
