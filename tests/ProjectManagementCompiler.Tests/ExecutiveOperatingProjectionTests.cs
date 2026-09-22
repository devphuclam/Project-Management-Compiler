using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class ExecutiveOperatingProjectionTests
{
    public static void OperatingProjectionUsesFourUrgencyGroupsAndStableDeduplication()
    {
        var report = new ExecutiveProgressReportProjector().Build(ExecutiveProgressTestFixtures.BuildFeature007ReportFixtureResult());
        var items = report.OperatingItems;

        TestAssert.True(items.Count > 0, "The operating projection must contain actionable or scheduled items for the fixture.");
        TestAssert.Equal(items.Count, items.Select(item => item.StableKey).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Operating targets must be deduplicated.");
        TestAssert.True(items.Any(item => item.RequiredDate is not null), "Operating items must retain supported dates when available.");
        var categoryOrder = items.Select(item => item.Category).ToArray();
        TestAssert.Equal(
            string.Join('|', categoryOrder.OrderBy(CategoryOrder)),
            string.Join('|', categoryOrder),
            "Operating groups must be ordered by management urgency.");
        TestAssert.True(items.Any(item => item.Category == ExecutiveOperatingCategory.OverdueUnfinished), "Overdue unfinished work must enter the operating agenda.");
        TestAssert.True(items.Any(item => item.Category == ExecutiveOperatingCategory.Active), "Active work must enter the operating agenda.");
        TestAssert.False(items.Any(item => item.StateLabel is "Hoàn thành" or "Đã hủy"), "Completed or cancelled work must not consume the bounded executive action agenda.");
        TestAssert.False(items.Any(item => item.Action.StartsWith("Chuẩn bị công việc theo kế hoạch.", StringComparison.Ordinal)), "The executive agenda must use the work name directly instead of a repeated filler prefix.");
    }

    private static int CategoryOrder(ExecutiveOperatingCategory category) => category switch
    {
        ExecutiveOperatingCategory.DecisionOrBlocker => 0,
        ExecutiveOperatingCategory.OverdueUnfinished => 1,
        ExecutiveOperatingCategory.Active => 2,
        ExecutiveOperatingCategory.PlannedOrMilestone => 3,
        _ => int.MaxValue
    };
}
