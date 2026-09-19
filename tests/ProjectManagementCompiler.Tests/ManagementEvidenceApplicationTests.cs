using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class ManagementEvidenceApplicationTests
{
    public static void CompilerComposesReadinessEvidenceAndReopenPreservesIt()
    {
        var compiler = new ProjectCompiler();
        var result = compiler.CompileAsync(
            new CompilationRequest
            {
                SourcePath = RealShapedFixturePath(),
                AsOfDate = new DateOnly(2026, 9, 28),
                IncludeManagementEvidence = true,
                ManagementEvidenceIncrementPath = "specs/004-technical-pilot-readiness"
            },
            CancellationToken.None).GetAwaiter().GetResult();

        var baselineBeforeImport = result.Project.Baseline;

        TestAssert.Equal(ManagementEvidenceDiscoveryState.Known, result.Project.ManagementEvidence.DiscoveryState, "Compiler must attach the known readiness package.");
        TestAssert.Equal(7, result.Project.ManagementEvidence.Observations.Count(observation => observation.EvidenceKind == ManagementEvidenceKind.ReadinessCheck), "Compiler must attach P01-P07 observations.");
        TestAssert.Equal(EvidenceReconciliationStatus.Matched, result.Project.ManagementEvidence.Reconciliations.Single(item => item.ObservationId == "readiness:P04").Status, "P04 must reconcile to the canonical WorkPackage.");
        TestAssert.Equal(EvidenceReconciliationStatus.Unmatched, result.Project.ManagementEvidence.Reconciliations.Single(item => item.ObservationId == "decision:D0").Status, "D0 must remain a management-only record.");
        TestAssert.Equal(ManagementEvidenceDiscoveryState.Known, result.Views.ManagementControl.DiscoveryState, "The management control view must expose evidence scope.");
        TestAssert.Equal("PH0", result.Views.ManagementControl.IncrementPhaseId, "The management control view must expose an explicitly sourced readiness phase.");
        TestAssert.Equal("NOT-RUN", result.Views.ManagementControl.CurrentGate.ExecutionState, "Gate execution must remain separate in the management control view.");
        TestAssert.Equal("NOT-APPLICABLE", result.Views.ManagementControl.CurrentGate.Outcome, "Gate outcome must remain separate in the management control view.");
        TestAssert.Equal(baselineBeforeImport, result.Project.Baseline, "Readiness import must not mutate the immutable baseline.");

        var json = compiler.SaveJson(result);
        var reopened = compiler.Reopen(json, new DateOnly(2026, 9, 28));

        TestAssert.Equal(result.Project.ManagementEvidence.IncrementId, reopened.Project.ManagementEvidence.IncrementId, "Reopen must preserve the active increment.");
        TestAssert.Equal(result.Project.ManagementEvidence.Observations.Count, reopened.Project.ManagementEvidence.Observations.Count, "Reopen must preserve evidence observations.");
        TestAssert.Equal(result.SemanticDigest, reopened.SemanticDigest, "Evidence-bearing canonical JSON must preserve semantic digest.");

        var update = compiler.ApplyExecutionUpdate(
            result,
            new ExecutionUpdate
            {
                WorkItemId = "P04",
                ExecutionState = ExecutionState.InProgress,
                ActualStart = new DateOnly(2026, 9, 28),
                LastUpdatedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)
            },
            new DateOnly(2026, 9, 28));
        TestAssert.True(update.Accepted, "A valid execution update must remain accepted with management evidence attached.");
        TestAssert.Equal(result.Project.ManagementEvidence.IncrementId, update.Result.Project.ManagementEvidence.IncrementId, "Execution updates must preserve management evidence.");
        TestAssert.Equal(baselineBeforeImport, update.Result.Project.Baseline, "Manual execution updates must also preserve the immutable baseline.");
    }

    public static void CompilerKeepsMvp1BaselineOnlyEvidenceNotRequested()
    {
        var compiler = new ProjectCompiler();
        var result = compiler.CompileAsync(
            new CompilationRequest
            {
                SourcePath = RealShapedFixturePath(),
                AsOfDate = new DateOnly(2026, 9, 28)
            },
            CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(ManagementEvidenceDiscoveryState.NotRequested, result.Project.ManagementEvidence.DiscoveryState, "MVP1 compilation without the profile must remain baseline-only.");
        TestAssert.Equal(0, result.Project.ManagementEvidence.Observations.Count, "MVP1 baseline-only compilation must not fabricate evidence.");
    }

    private static string RealShapedFixturePath() =>
        Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering-real-shaped");
}
