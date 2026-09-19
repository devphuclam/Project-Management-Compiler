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
        TestAssert.Equal(60, result.Cario.Tasks.Count, "Application CARIO model must preserve 53 cards plus 7 milestone/decision task rows.");
        TestAssert.Equal(53, result.Cario.Tasks.Count(task => task.WorkItemType == "DeliveryCard"), "Application CARIO model must preserve all executable delivery cards.");
        TestAssert.Equal(7, result.Cario.Tasks.Count(task => task.WorkItemType is "Decision" or "Milestone"), "Application CARIO model must preserve all milestone/decision rows.");
        TestAssert.Equal(53, result.Cario.Assignments.Count, "Application CARIO model must preserve every assignment row.");
        TestAssert.True(result.Warnings.Any(warning => warning.Code == "CARIO_MAPPING_UNRESOLVED"), "Unresolved CARIO mappings must be visible in the application warning stream.");
        TestAssert.True(result.SemanticDigest.Length == 64, "Application result must expose a semantic SHA-256 digest.");
    }

    public static void CompilerCompilesRealShapedFixtureThroughFullPipelineWithTypedP04Ids()
    {
        var compiler = new ProjectCompiler();
        var asOfDate = new DateOnly(2026, 9, 28);
        var result = compiler.CompileAsync(new CompilationRequest
        {
            SourcePath = RealShapedFixturePath(),
            AsOfDate = asOfDate
        }, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(6, result.Project.Phases.Count, "The full real-shaped compiler path must preserve six phases.");
        TestAssert.Equal(35, result.Project.WorkPackages.Count, "The full real-shaped compiler path must preserve 35 work packages.");
        TestAssert.Equal(53, result.Project.DeliveryCards.Count, "The full real-shaped compiler path must preserve 53 delivery cards.");
        TestAssert.Equal(7, result.Project.Milestones.Count, "The full real-shaped compiler path must preserve seven milestones.");
        TestAssert.Equal(512m, result.Project.Baseline.PlannedEffortHours, "The full real-shaped compiler path must preserve 512 authoritative hours.");
        TestAssert.Equal(88m, result.Project.Baseline.ReserveHours, "The full real-shaped compiler path must preserve 88 reserve hours.");
        TestAssert.Equal(600m, result.Project.Baseline.CapacityHours, "The full real-shaped compiler path must preserve 600 capacity hours.");

        var workPackage = result.Project.WorkPackages.Single(item => item.Id == "P04");
        var deliveryCard = result.Project.DeliveryCards.Single(item => item.Id == "P04");
        TestAssert.Equal("P04", workPackage.Id, "WorkPackage P04 must retain its source ID.");
        TestAssert.Equal("P04", deliveryCard.Id, "DeliveryCard P04 must retain its source ID.");
        TestAssert.True(result.Project.Dependencies.Any(dependency =>
            dependency.SubjectKind == "WorkPackage"
            && dependency.SubjectId == "P04"
            && dependency.PredecessorKind == "WorkPackage"
            && dependency.PredecessorId == "P03"), "WorkPackage P04 dependency must remain a typed work-package edge.");
        TestAssert.True(result.Project.Dependencies.Any(dependency =>
            dependency.SubjectKind == "DeliveryCard"
            && dependency.SubjectId == "P04"
            && dependency.PredecessorKind == "DeliveryCard"
            && dependency.PredecessorId == "P03"), "DeliveryCard P04 dependency must remain a typed card edge.");
        TestAssert.False(result.Warnings.Any(warning => warning.Code is "INVALID_DEPENDENCY_SUBJECT_KIND" or "INVALID_DEPENDENCY_PREDECESSOR_KIND" or "DUPLICATE_DEPENDENCY"), "Shared raw IDs must not create typed-ID validation errors.");

        var p04Nodes = result.Views.DependencyNetwork.Nodes.Where(node => node.Id == "P04").ToArray();
        TestAssert.Equal(2, p04Nodes.Length, "Dependency network must retain both typed P04 nodes.");
        TestAssert.True(p04Nodes.Any(node => node.Kind == "WorkPackage"), "Dependency network must expose WorkPackage:P04.");
        TestAssert.True(p04Nodes.Any(node => node.Kind == "DeliveryCard"), "Dependency network must expose DeliveryCard:P04.");

        var ganttP04 = result.Views.Gantt.Items.Single(item => item.WorkItemId == "P04");
        TestAssert.True(ganttP04.DependencyIds.SequenceEqual(new[] { "P03" }), "Gantt must expose the DeliveryCard P04 dependency without duplicate trace edges.");
        TestAssert.Equal(1, result.Cario.Tasks.Count(task => task.WorkItemType == "DeliveryCard" && task.TaskId == "P04"), "CARIO must export exactly one executable P04 card row.");
        TestAssert.Equal(60, result.Cario.Tasks.Count, "CARIO must export 53 cards plus seven milestone/decision rows, not work packages.");

        var json = compiler.SaveJson(result);
        var reopened = compiler.Reopen(json, asOfDate);
        TestAssert.Equal(35, reopened.Project.WorkPackages.Count, "Real-shaped canonical JSON reopen must preserve work packages.");
        TestAssert.Equal(53, reopened.Project.DeliveryCards.Count, "Real-shaped canonical JSON reopen must preserve delivery cards.");
        TestAssert.True(reopened.Project.WorkPackages.Any(item => item.Id == "P04"), "Reopened JSON must preserve WorkPackage P04.");
        TestAssert.True(reopened.Project.DeliveryCards.Any(item => item.Id == "P04"), "Reopened JSON must preserve DeliveryCard P04.");
        TestAssert.True(compiler.ExportCarioXlsx(result).Length > 0, "Real-shaped full compiler output must export a CARIO workbook.");
    }

    public static void CompilerRequiresExplicitAsOfDateForCompileAndReopen()
    {
        var compiler = new ProjectCompiler();
        try
        {
            compiler.CompileAsync(new CompilationRequest
            {
                SourcePath = FixturePath()
            }, CancellationToken.None).GetAwaiter().GetResult();
            throw new InvalidOperationException("Compile without an as-of date should fail.");
        }
        catch (ProjectCompilationException exception)
        {
            TestAssert.Equal("compile-request", exception.Phase, "Missing compile as-of date must be a structured request error.");
            TestAssert.True(exception.Diagnostics.Any(diagnostic => diagnostic.Code == "MISSING_AS_OF_DATE"), "Missing compile as-of date must expose a stable diagnostic code.");
        }

        var valid = compiler.CompileAsync(new CompilationRequest
        {
            SourcePath = FixturePath(),
            AsOfDate = new DateOnly(2026, 9, 28)
        }, CancellationToken.None).GetAwaiter().GetResult();
        var json = compiler.SaveJson(valid);

        try
        {
            compiler.Reopen(json);
            throw new InvalidOperationException("Reopen without an as-of date should fail.");
        }
        catch (ProjectCompilationException exception)
        {
            TestAssert.Equal("reopen", exception.Phase, "Missing reopen as-of date must be a structured request error.");
            TestAssert.True(exception.Diagnostics.Any(diagnostic => diagnostic.Code == "MISSING_AS_OF_DATE"), "Missing reopen as-of date must expose a stable diagnostic code.");
        }
    }

    public static void CompilerReopenUsesExplicitAsOfDateDeterministically()
    {
        var compiler = new ProjectCompiler();
        var initial = compiler.CompileAsync(new CompilationRequest
        {
            SourcePath = FixturePath(),
            AsOfDate = new DateOnly(2026, 9, 28)
        }, CancellationToken.None).GetAwaiter().GetResult();
        var updatedProject = SourceExecutionTestFixtures.Apply(initial.Project, new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 25),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)
        });
        var updated = SourceExecutionTestFixtures.BuildResult(updatedProject, new DateOnly(2026, 9, 28));

        var json = compiler.SaveJson(updated);
        var sameDateFirst = compiler.Reopen(json, new DateOnly(2026, 9, 28));
        var sameDateSecond = compiler.Reopen(json, new DateOnly(2026, 9, 28));
        var firstAlerts = string.Join("|", sameDateFirst.Analysis.Alerts.Select(alert => $"{alert.WorkItemId}:{alert.AlertCode}:{alert.VarianceWorkingMinutes}"));
        var secondAlerts = string.Join("|", sameDateSecond.Analysis.Alerts.Select(alert => $"{alert.WorkItemId}:{alert.AlertCode}:{alert.VarianceWorkingMinutes}"));
        TestAssert.Equal(firstAlerts, secondAlerts, "The same canonical JSON and explicit as-of date must derive deterministic alerts.");
        TestAssert.Equal(1, sameDateFirst.Analysis.ExecutionStatus.Overdue, "The later explicit as-of date must expose the overdue condition.");

        var earlierDate = compiler.Reopen(json, new DateOnly(2026, 9, 24));
        TestAssert.Equal(0, earlierDate.Analysis.ExecutionStatus.Overdue, "Changing the explicit as-of date must intentionally change overdue analysis.");
    }

    public static void CompilerExecutionUpdateSaveAndReopenPreserveBaselineAndRecalculateAlerts()
    {
        var compiler = new ProjectCompiler();
        var initial = compiler.CompileAsync(new CompilationRequest
        {
            SourcePath = FixturePath(),
            AsOfDate = new DateOnly(2026, 9, 28)
        }, CancellationToken.None).GetAwaiter().GetResult();

        var updatedProject = SourceExecutionTestFixtures.Apply(initial.Project, new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 25),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)
        });
        var updated = SourceExecutionTestFixtures.BuildResult(updatedProject, new DateOnly(2026, 9, 28));

        TestAssert.True(updated.Views.Dashboard.Overdue == 1, "Application source execution must recalculate overdue status.");
        var json = compiler.SaveJson(updated);
        var reopened = compiler.Reopen(json, new DateOnly(2026, 9, 28));

        TestAssert.Equal(initial.Project.Baseline, reopened.Project.Baseline, "Reopen must preserve the immutable baseline.");
        TestAssert.Equal(new DateOnly(2026, 9, 18), reopened.Project.DeliveryCards.Single(card => card.Id == "P01-A").PlannedStart, "Reopen must preserve planned dates.");
        TestAssert.Equal(ExecutionState.InProgress, reopened.Project.SourceExecution.Records.Single(record => record.Entity.Id == "P04-A").ExecutionState, "Reopen must preserve source execution state.");
        TestAssert.True(reopened.Project.Analysis?.Alerts.Any(alert => alert.WorkItemId == "P04-A" && alert.AlertCode == "OVERDUE") == true, "Reopen must recalculate execution-derived alerts.");
        TestAssert.Equal(CanonicalJsonDigest.Compute(updated.Project), CanonicalJsonDigest.Compute(reopened.Project), "Reopen must preserve the semantic project digest.");
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

    public static void CompilerRejectsMalformedCanonicalJsonWithStructuredReopenError()
    {
        var compiler = new ProjectCompiler();
        var threw = false;
        try
        {
            compiler.Reopen("{ not valid canonical json", new DateOnly(2026, 9, 28));
        }
        catch (ProjectCompilationException exception)
        {
            threw = true;
            TestAssert.Equal("reopen", exception.Phase, "Malformed canonical JSON must identify the reopen phase.");
            TestAssert.True(exception.Diagnostics.Any(diagnostic => diagnostic.Code == "INVALID_CANONICAL_JSON"), "Malformed canonical JSON must return a structured diagnostic.");
        }

        TestAssert.True(threw, "Malformed canonical JSON must not escape as an unstructured parser exception.");
    }

    private static string FixturePath() =>
        Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering");

    private static string RealShapedFixturePath() =>
        Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering-real-shaped");
}
