using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Tests;

internal static class ExecutiveDailyGanttFixtureTests
{
    public static void DailyGanttFixtureBuildsOfficialSparseAndBoundaryEvidence()
    {
        var result = ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult();
        var project = result.Project;
        var records = project.SourceExecution.Records.ToDictionary(record => record.Entity.Id, StringComparer.OrdinalIgnoreCase);

        TestAssert.Equal(ManifestImportClassification.OfficialCommit, project.ImportMetadata!.Classification, "The daily-Gantt fixture must be an official snapshot.");
        TestAssert.Equal(ExecutiveProgressTestFixtures.DailyGanttDefaultReportingDate, project.ImportMetadata.RegisterStatusDate, "The fixture must expose its deterministic source reporting date.");
        TestAssert.Equal(new DateOnly(2026, 9, 16), records[ExecutiveProgressTestFixtures.CompletedEarlyCardId].ActualStart, "The fixture must retain Actual work before the baseline window.");
        TestAssert.Equal(new DateOnly(2026, 9, 23), records[ExecutiveProgressTestFixtures.CompletedLateCardId].ActualFinish, "The fixture must retain a late completed Actual finish.");
        TestAssert.Equal(new DateOnly(2026, 10, 6), DateOnly.FromDateTime(records[ExecutiveProgressTestFixtures.OpenInProgressCardId].ForecastFinish!.Value.DateTime), "The fixture must retain an official forecast finish.");
        TestAssert.Equal(null, records[ExecutiveProgressTestFixtures.InProgressWithoutStartCardId].ActualStart, "The fixture must retain an in-progress record without an Actual start.");
        TestAssert.Equal(SourceRecordingState.NotRecorded, records[ExecutiveProgressTestFixtures.UnrecordedCardId].RecordingState, "Unrecorded fixture work must remain explicitly unrecorded.");
        TestAssert.Equal(null, records[ExecutiveProgressTestFixtures.OneSidedEffortCardId].RemainingEffortHours, "The fixture must retain one-sided effort evidence.");
        TestAssert.Equal(0m, records[ExecutiveProgressTestFixtures.ZeroSumEffortCardId].ActualEffortHours, "The fixture must retain zero Actual effort.");
        TestAssert.Equal(0m, records[ExecutiveProgressTestFixtures.ZeroSumEffortCardId].RemainingEffortHours, "The fixture must retain zero remaining effort.");
        TestAssert.Equal(null, records[ExecutiveProgressTestFixtures.MissingEffortCardId].ActualEffortHours, "The fixture must retain missing effort without fabricating zero.");
        TestAssert.Equal(records[ExecutiveProgressTestFixtures.OneDayCompletedCardId].ActualStart, records[ExecutiveProgressTestFixtures.OneDayCompletedCardId].ActualFinish, "The fixture must retain a completed one-day Actual interval.");
        TestAssert.True(project.Milestones.Any(item => item.Id == ExecutiveProgressTestFixtures.RepeatedRawId), "The fixture must contain a milestone sharing a raw identifier with another canonical kind.");
        TestAssert.True(project.WorkPackages.Any(item => item.Id == ExecutiveProgressTestFixtures.RepeatedRawId), "The fixture must retain the work package sharing that raw identifier.");
        TestAssert.Contains("**Chuẩn bị dữ liệu", project.DeliveryCards.Single(item => item.Id == ExecutiveProgressTestFixtures.OneSidedEffortCardId).Name, "The fixture must retain long Vietnamese markdown text for presentation tests.");
    }

    public static void DailyGanttFixtureSeparatesContradictoryAndNegativeEvidenceForFailClosedTests()
    {
        var compiler = new ProjectCompiler();

        AssertRejected(compiler, ExecutiveProgressTestFixtures.BuildContradictoryActualFixtureProject(), "INVALID_SOURCE_EXECUTION");
        AssertRejected(compiler, ExecutiveProgressTestFixtures.BuildNegativeEffortFixtureProject(), "INVALID_SOURCE_EXECUTION");
    }

    private static void AssertRejected(ProjectCompiler compiler, CanonicalProject project, string expectedDiagnostic)
    {
        try
        {
            _ = compiler.BuildImportedResult(new IdeaEngineeringSnapshot
            {
                Project = project,
                Metadata = project.ImportMetadata!,
                SourceExecution = project.SourceExecution
            }, ExecutiveProgressTestFixtures.DailyGanttDefaultReportingDate);
        }
        catch (ProjectCompilationException exception)
        {
            TestAssert.Contains(expectedDiagnostic, string.Join('|', exception.Diagnostics.Select(item => item.Code)), "Contradictory source evidence must fail canonical validation before it can be exported.");
            return;
        }

        throw new InvalidOperationException("Contradictory source evidence must be rejected before it can be exported.");
    }
}
