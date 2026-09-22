using System.Reflection;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class ReaderFacingTextPolicyTests
{
    public static void CleanNameRemovesOnlyRecognizedIdentityAndFormattingNoise()
    {
        foreach (var (sourceText, exactId, expected) in ExecutiveProgressTestFixtures.ReaderFacingTitleCases)
        {
            TestAssert.Equal(
                expected,
                CleanName(sourceText, exactId),
                $"Reader-facing cleanup must preserve business meaning for '{sourceText}'.");
        }

        TestAssert.Equal(
            "Lập Gantt, lịch làm việc và milestone",
            CleanName("Work Package → `**Lập Gantt, lịch làm việc và milestone**`", "PLN02"),
            "Recognized kind, arrow, and Markdown decoration must be removed.");
    }

    public static void CleanNameHandlesExactIdsAtBothEdgesWithoutEatingMeaning()
    {
        TestAssert.Equal(
            "Chuẩn bị môi trường",
            CleanName("P04 — P04 — Chuẩn bị môi trường — P04", "P04"),
            "Repeated exact IDs at recognized separators must be removed.");
        TestAssert.Equal(
            "Thiết kế API P04 tương thích",
            CleanName("Thiết kế API P04 tương thích", "P04"),
            "An ID-like token inside meaningful text must remain.");
        TestAssert.Equal(
            "[Bắt buộc] Kiểm tra dữ liệu đầu vào",
            CleanName("[Bắt buộc] Kiểm tra dữ liệu đầu vào", "P04"),
            "Meaningful bracketed Vietnamese text must remain.");
    }

    public static void PolicyUsesApprovedMissingEvidenceAndOwnerLabels()
    {
        TestAssert.Equal(
            "Chưa ghi nhận",
            CleanName("  **P01**  ", "P01"),
            "An empty cleaned title must use the approved missing-evidence label.");
        TestAssert.Equal(
            "Chưa phân công",
            OwnerOrMissing(" "),
            "A missing owner must use the approved owner label.");
        TestAssert.Equal(
            "LEAD",
            OwnerOrMissing("  LEAD  "),
            "A recorded owner must be trimmed without being rewritten.");
    }

    public static void ExecutiveProjectorsUseTheSharedReaderFacingPolicy()
    {
        var result = ExecutiveProgressTestFixtures.BuildFeature007ReportFixtureResult();
        result = result with
        {
            Project = result.Project with
            {
                Assignments = result.Project.Assignments
                    .Where(assignment => !string.Equals(
                        assignment.WorkItemId,
                        ExecutiveProgressTestFixtures.CompletedEarlyCardId,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray()
            }
        };

        var report = new ExecutiveProgressReportProjector().Build(result);
        var detail = report.DeliveryCardDetails.Single(item =>
            item.ReferenceCode == ExecutiveProgressTestFixtures.CompletedEarlyCardId);
        var daily = report.DailyGantt.FullRows.Single(item =>
            item.Kind == ExecutiveDailyGanttRowKind.DeliveryCard
            && item.ReferenceCode == ExecutiveProgressTestFixtures.CompletedEarlyCardId);

        const string expectedName = "Xác nhận bộ tài liệu được phép dùng để bắt đầu";
        TestAssert.Equal(expectedName, detail.Description, "The report detail projector must use the shared name policy.");
        TestAssert.Equal(expectedName, daily.DisplayName, "The daily-Gantt projector must use the shared name policy.");
        TestAssert.Equal("Chưa phân công", detail.OwnerLabel, "The report detail projector must use the shared missing-owner label.");
        TestAssert.Equal("Chưa phân công", daily.OwnerLabel, "The daily-Gantt projector must use the shared missing-owner label.");
    }

    private static string CleanName(string? sourceText, string? exactId)
    {
        var method = PolicyType().GetMethod(
            "CleanName",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(string), typeof(string)],
            modifiers: null);
        TestAssert.True(method is not null, "ReaderFacingTextPolicy.CleanName must exist.");
        return (string)method!.Invoke(null, [sourceText, exactId])!;
    }

    private static string OwnerOrMissing(string? owner)
    {
        var method = PolicyType().GetMethod(
            "OwnerOrMissing",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(string)],
            modifiers: null);
        TestAssert.True(method is not null, "ReaderFacingTextPolicy.OwnerOrMissing must exist.");
        return (string)method!.Invoke(null, [owner])!;
    }

    private static Type PolicyType()
    {
        var type = Type.GetType(
            "ProjectManagementCompiler.Management.ReaderFacingTextPolicy, ProjectManagementCompiler",
            throwOnError: false);
        TestAssert.True(type is not null, "ReaderFacingTextPolicy must exist in the management layer.");
        return type!;
    }
}
