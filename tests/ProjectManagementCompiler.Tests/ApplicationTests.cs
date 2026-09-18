using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;

namespace ProjectManagementCompiler.Tests;

internal static class ApplicationTests
{
    public static void CompilerComposesCaptureCanonicalAnalysisViewsAndCarioExport()
    {
        var compiler = new ProjectCompiler();
        var result = compiler.CompileAsync(new CompilationRequest
        {
            SourcePath = FixturePath(),
            AsOfDate = new DateOnly(2026, 9, 28)
        }, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(6, result.Project.Phases.Count, "Application compilation must preserve all phases.");
        TestAssert.Equal(53, result.Project.DeliveryCards.Count, "Application compilation must preserve all delivery cards.");
        TestAssert.Equal(result.Project.Project.Id, result.Views.Wbs.Root.Id, "Application WBS must use the canonical project identity.");
        TestAssert.Equal(53, result.Views.Gantt.Items.Count, "Application Gantt must use the shared management projection.");
        TestAssert.Equal(5, result.Views.Kanban.Columns.Count, "Application Kanban must expose the five supported execution status columns.");
        TestAssert.True(result.Cario.Tasks.Count == 53 && result.Cario.Assignments.Count == 53, "Application CARIO model must preserve task and assignment counts.");
        TestAssert.True(result.SemanticDigest.Length == 64, "Application result must expose a semantic SHA-256 digest.");
    }

    public static void CompilerExecutionUpdateSaveAndReopenPreserveBaselineAndRecalculateAlerts()
    {
        var compiler = new ProjectCompiler();
        var initial = compiler.CompileAsync(new CompilationRequest
        {
            SourcePath = FixturePath(),
            AsOfDate = new DateOnly(2026, 9, 28)
        }, CancellationToken.None).GetAwaiter().GetResult();

        var updated = compiler.ApplyExecutionUpdate(initial, new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 25),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)
        }, new DateOnly(2026, 9, 28));

        TestAssert.True(updated.Accepted, "Application execution update must be accepted.");
        TestAssert.True(updated.Result.Views.Dashboard.Overdue == 1, "Application update must recalculate overdue status.");
        var json = compiler.SaveJson(updated.Result);
        var reopened = compiler.Reopen(json, new DateOnly(2026, 9, 28));

        TestAssert.Equal(initial.Project.Baseline, reopened.Project.Baseline, "Reopen must preserve the immutable baseline.");
        TestAssert.Equal(new DateOnly(2026, 9, 18), reopened.Project.DeliveryCards.Single(card => card.Id == "P01-A").PlannedStart, "Reopen must preserve planned dates.");
        TestAssert.Equal(ExecutionState.InProgress, reopened.Project.ExecutionOverlay.Records.Single().ExecutionState, "Reopen must preserve execution overlay state.");
        TestAssert.True(reopened.Project.Analysis?.Alerts.Any(alert => alert.WorkItemId == "P04-A" && alert.AlertCode == "OVERDUE") == true, "Reopen must recalculate execution-derived alerts.");
        TestAssert.Equal(CanonicalJsonDigest.Compute(updated.Result.Project), CanonicalJsonDigest.Compute(reopened.Project), "Reopen must preserve the semantic project digest.");
    }

    public static void CompilerRejectsInvalidExecutionUpdateWithoutMutatingResult()
    {
        var compiler = new ProjectCompiler();
        var initial = compiler.CompileAsync(new CompilationRequest
        {
            SourcePath = FixturePath(),
            AsOfDate = new DateOnly(2026, 9, 28)
        }, CancellationToken.None).GetAwaiter().GetResult();
        var result = compiler.ApplyExecutionUpdate(initial, new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.InProgress,
            LastUpdatedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)
        }, new DateOnly(2026, 9, 28));

        TestAssert.False(result.Accepted, "Application must reject an in-progress update without an actual start.");
        TestAssert.Equal(0, result.Result.Project.ExecutionOverlay.Records.Count, "Rejected execution updates must not mutate the result.");
        TestAssert.True(result.Diagnostics.Any(diagnostic => diagnostic.Code == "INVALID_EXECUTION_UPDATE"), "Rejected updates must return structured diagnostics.");
    }

    private static string FixturePath() =>
        Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering");
}
