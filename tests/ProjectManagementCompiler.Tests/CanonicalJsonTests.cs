using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class CanonicalJsonTests
{
    public static void CanonicalJsonEmitsExecutionOverlayAndExplicitUnknownFields()
    {
        var project = CaptureCanonicalProject();
        var result = new ExecutionOverlayUpdater().Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P01-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 24),
            ActualEffortHours = 8m,
            LastUpdatedAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero)
        });
        var json = new CanonicalJsonSerializer().Serialize(result.Project);

        TestAssert.Contains("\"schemaVersion\":\"1.0\"", json, "Canonical JSON must declare schema version 1.0.");
        TestAssert.Contains("\"executionOverlay\":{\"records\":[", json, "Canonical JSON must emit the execution overlay.");
        TestAssert.Contains("\"actualStart\":\"2026-09-24\"", json, "Actual start must be persisted as an ISO date.");
        TestAssert.Contains("\"actualFinish\":null", json, "Missing actual finish must remain explicit.");
        TestAssert.Contains("\"plannedStart\":\"2026-09-18\"", json, "Baseline planned start must remain in the snapshot.");
    }

    public static void CanonicalJsonRoundTripsOverlayWithoutChangingBaseline()
    {
        var project = CaptureCanonicalProject();
        var originalCard = project.DeliveryCards.Single(card => card.Id == "P01-A");
        var updated = new ExecutionOverlayUpdater().Apply(project, new ExecutionUpdate
        {
            WorkItemId = originalCard.Id,
            ExecutionState = ExecutionState.Completed,
            ActualStart = new DateOnly(2026, 9, 24),
            ActualFinish = new DateOnly(2026, 9, 25),
            ActualEffortHours = 8m,
            LastUpdatedAt = new DateTimeOffset(2026, 9, 25, 17, 0, 0, TimeSpan.Zero)
        }).Project;

        var reopened = new CanonicalJsonSerializer().Deserialize(new CanonicalJsonSerializer().Serialize(updated));
        var reopenedCard = reopened.DeliveryCards.Single(card => card.Id == originalCard.Id);
        var record = reopened.ExecutionOverlay.Records.Single();

        TestAssert.Equal(originalCard.PlannedStart, reopenedCard.PlannedStart, "Reopen must retain the original planned start.");
        TestAssert.Equal(originalCard.PlannedFinish, reopenedCard.PlannedFinish, "Reopen must retain the original planned finish.");
        TestAssert.Equal(ExecutionState.Completed, record.ExecutionState, "Reopen must retain execution state.");
        TestAssert.Equal(new DateOnly(2026, 9, 25), record.ActualFinish, "Reopen must retain actual finish.");
        TestAssert.Equal(8m, record.ActualEffortHours, "Reopen must retain actual effort independently.");
    }

    public static void Schema10JsonWithoutOverlayUsesEmptyOverlay()
    {
        var project = CaptureCanonicalProject();
        var json = new CanonicalJsonSerializer().Serialize(project).Replace(",\"executionOverlay\":{\"records\":[]}", string.Empty, StringComparison.Ordinal);

        var reopened = new CanonicalJsonSerializer().Deserialize(json);

        TestAssert.True(reopened.ExecutionOverlay.Records.Count == 0, "An older schema-1.0 snapshot without executionOverlay must reopen with an empty overlay.");
    }

    public static void SemanticDigestIgnoresCaptureAndAnalysisButIncludesOverlay()
    {
        var project = CaptureCanonicalProject();
        var timestamped = project with
        {
            Sources = project.Sources.Select(source => source with
            {
                CapturedAtUtc = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero)
            }).ToArray(),
            Analysis = new ManagementAnalysis
            {
                CpmState = DataState.Calculated,
                BaselineFinish = project.Baseline.PlanningFinish
            }
        };

        TestAssert.Equal(
            CanonicalJsonDigest.Compute(project),
            CanonicalJsonDigest.Compute(timestamped),
            "Capture timestamps and replaceable analysis must not change semantic digest.");

        var updated = new ExecutionOverlayUpdater().Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P01-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 24),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero)
        }).Project;
        TestAssert.True(
            CanonicalJsonDigest.Compute(project) != CanonicalJsonDigest.Compute(updated),
            "User-owned execution evidence must participate in semantic digest.");
    }

    public static void CanonicalJsonRejectsOverlayForUnknownWorkItem()
    {
        var project = CaptureCanonicalProject() with
        {
            ExecutionOverlay = new ExecutionOverlay
            {
                Records =
                [
                    new ExecutionRecord
                    {
                        WorkItemId = "X99",
                        ExecutionState = ExecutionState.InProgress,
                        ActualStart = new DateOnly(2026, 9, 24),
                        ActualStartState = DataState.Known,
                        LastUpdatedAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero)
                    }
                ]
            }
        };

        var serializer = new CanonicalJsonSerializer();
        TestAssert.Throws<InvalidDataException>(
            () => serializer.Deserialize(serializer.Serialize(project)),
            "Canonical JSON must reject execution records that do not resolve to delivery cards.");
    }

    public static void CanonicalJsonRejectsInconsistentOverlayStateAndEffort()
    {
        var project = CaptureCanonicalProject() with
        {
            ExecutionOverlay = new ExecutionOverlay
            {
                Records =
                [
                    new ExecutionRecord
                    {
                        WorkItemId = "P01-A",
                        ExecutionState = ExecutionState.InProgress,
                        ActualEffortHours = -1m,
                        ActualEffortState = DataState.Known,
                        LastUpdatedAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero)
                    }
                ]
            }
        };

        var serializer = new CanonicalJsonSerializer();
        TestAssert.Throws<InvalidDataException>(
            () => serializer.Deserialize(serializer.Serialize(project)),
            "Canonical JSON must reject negative effort and state/date inconsistencies.");
    }

    public static void CanonicalJsonRetainsInvalidSourceDependencyEvidence()
    {
        var project = CaptureCanonicalProject();
        var reopened = new CanonicalJsonSerializer().Deserialize(new CanonicalJsonSerializer().Serialize(project));
        var invalidDependency = reopened.Dependencies.Single(dependency => dependency.PredecessorId == "X99");

        TestAssert.Equal(ValidationState.InvalidSourceEvidence, invalidDependency.ValidationState, "Reopen must retain the explicit invalid-source dependency state.");
        TestAssert.False(invalidDependency.AnalysisEligible, "Invalid source dependency evidence must remain excluded from analysis.");
        TestAssert.Equal("Q06-A", invalidDependency.SubjectId, "Reopen must retain the invalid dependency subject.");
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
