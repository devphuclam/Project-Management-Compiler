using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class ExecutiveWbsProjectionTests
{
    public static void WbsReconcilesCanonicalHierarchyAndExcludesMilestones()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildFeature007ReportFixtureResult());
        var wbs = report.Wbs;

        TestAssert.Equal(1, wbs.ProjectCount, "The WBS must contain exactly one project row.");
        TestAssert.Equal(ExecutiveProgressTestFixtures.ExpectedPhaseCount, wbs.PhaseCount, "The WBS must reconcile all phases.");
        TestAssert.Equal(ExecutiveProgressTestFixtures.ExpectedWorkPackageCount, wbs.WorkPackageCount, "The WBS must reconcile all work packages.");
        TestAssert.Equal(ExecutiveProgressTestFixtures.ExpectedDeliveryCardCount, wbs.DeliveryCardCount, "The WBS must reconcile all delivery cards.");
        TestAssert.False(wbs.Rows.Any(row => row.Kind.ToString().Contains("Milestone", StringComparison.Ordinal)), "Milestones and gates must not become WBS rows.");
        TestAssert.Equal("1", wbs.Rows[0].WbsNumber, "The project row must start positional WBS numbering.");
        TestAssert.Equal(0, wbs.Rows[0].Depth, "The project row must be depth zero.");
        TestAssert.Equal("1.1", wbs.Rows.First(row => row.Kind == ExecutiveWbsRowKind.Phase).WbsNumber, "The first phase must use positional numbering.");
        TestAssert.True(wbs.Rows.Where(row => row.Kind == ExecutiveWbsRowKind.DeliveryCard).All(row => row.Depth == 3), "Delivery Cards must remain at WBS depth three.");
        TestAssert.True(wbs.Rows.All(row => !Path.IsPathFullyQualified(row.SourceReferenceLabel)), "WBS source references must never expose an absolute path.");
    }

    public static void WbsRetainsReaderFactsAndOfficialEvidenceLabels()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildFeature007ReportFixtureResult());
        var card = report.Wbs.Rows.Single(row => row.ReferenceCode == ExecutiveProgressTestFixtures.FinishOnlyCardId);
        var effortOnly = report.Wbs.Rows.Single(row => row.ReferenceCode == ExecutiveProgressTestFixtures.EffortOnlyCardId);
        var missing = report.Wbs.Rows.Single(row => row.ReferenceCode == ExecutiveProgressTestFixtures.NoExecutionEvidenceCardId);

        TestAssert.Equal("Chưa phân công", missing.OwnerLabel, "A missing WBS owner must use the approved reader label.");
        TestAssert.Equal(null, card.ActualStart, "A finish-only WBS row must not infer Actual start.");
        TestAssert.Equal(new DateOnly(2026, 9, 24), card.ActualFinish, "A finish-only WBS row must retain Actual finish.");
        TestAssert.True(effortOnly.EvidenceSummary.Contains("Có ghi nhận", StringComparison.Ordinal), "Effort-only WBS evidence must remain explicit.");
        TestAssert.True(report.Wbs.Rows.Any(row => row.PredecessorCodes.Count > 0), "The WBS must retain supported predecessor identity for traceability.");
    }
}
