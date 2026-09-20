using System.Collections;
using System.Reflection;
using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class ExecutiveProgressProjectionTests
{
    public static void ProjectionUsesOfficialSourceDateAndApprovedTimelineSemantics()
    {
        var result = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult(new DateOnly(2026, 9, 19));
        var report = BuildReport(result);

        TestAssert.Equal(new DateOnly(2026, 9, 19), ReadDate(report, "SourceReportingDate"), "The report must disclose the official manifest reporting date.");
        TestAssert.Equal(new DateOnly(2026, 9, 18), ReadDate(report, "PlanningStart"), "The report must preserve the official planning start.");
        TestAssert.Equal(new DateOnly(2026, 12, 31), ReadDate(report, "PlanningFinish"), "The report must preserve the official planning finish.");
        TestAssert.Equal("Planning package PH0", Read(report, "CurrentPhase"), "The current phase must be selected from the source reporting date.");

        var nextMilestone = Read(report, "NextMilestone");
        TestAssert.True(nextMilestone is not null, "The report must expose a next milestone summary.");
        TestAssert.False(string.Equals(Read(nextMilestone!, "Kind")?.ToString(), "Decision", StringComparison.Ordinal), "A decision-kind record must never be selected as the next milestone.");

        var timeline = Items(report, "OverviewTimeline");
        TestAssert.True(timeline.Count > 0, "The overview timeline must contain phase/milestone rows.");
        TestAssert.False(timeline.Any(row => string.Equals(Read(row, "Kind")?.ToString(), "Decision", StringComparison.Ordinal)), "Decision-kind records must be excluded from the overview timeline.");
        TestAssert.True(timeline.Any(row => string.Equals(Read(row, "Kind")?.ToString(), "Phase", StringComparison.Ordinal)), "The overview timeline must contain phase rows.");

        var before = BuildReport(WithSourceDate(result, new DateOnly(2026, 9, 1)));
        var after = BuildReport(WithSourceDate(result, new DateOnly(2027, 1, 1)));
        TestAssert.Equal("Chưa xác định giai đoạn hiện tại", Read(before, "CurrentPhase"), "A reporting date before the planning window must use the exact missing-phase text.");
        TestAssert.Equal("Chưa xác định giai đoạn hiện tại", Read(after, "CurrentPhase"), "A reporting date after the planning window must use the exact missing-phase text.");
        TestAssert.Equal("Chưa xác định mốc sắp tới", Read(after, "NextMilestone") is null ? null : Read(Read(after, "NextMilestone")!, "DisplayName"), "A reporting date after all milestones must use the exact missing-milestone text.");
    }

    public static void ProjectionKeepsScheduleAndReadinessConditionsIndependent()
    {
        var result = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult(new DateOnly(2026, 9, 19));
        result = result with
        {
            Analysis = result.Analysis with
            {
                Alerts = Array.Empty<Alert>()
            }
        };
        var baseline = BuildReport(result);
        var schedule = Read(baseline, "ScheduleCondition");
        var readiness = Read(baseline, "ReadinessCondition");

        TestAssert.Equal("Chưa ghi nhận lệch kế hoạch", Read(schedule!, "Label"), "Assessable schedule data without alerts must use the plan condition.");
        TestAssert.Equal("Chưa đánh giá", Read(readiness!, "Label"), "Missing readiness evidence must remain explicitly unevaluated.");
        TestAssert.False(string.Equals(Read(schedule!, "Tone")?.ToString(), Read(readiness!, "Tone")?.ToString(), StringComparison.Ordinal), "Schedule and readiness must remain separate conditions.");

        var blockedAnalysis = result.Analysis with
        {
            Alerts =
            [
                new Alert
                {
                    WorkItemId = "P01-A",
                    AlertCode = "BLOCKED",
                    Severity = WarningSeverity.Error,
                    Message = "blocked",
                    DerivedAt = new DateOnly(2026, 9, 19)
                }
            ]
        };
        var blocked = BuildReport(result with { Analysis = blockedAnalysis });
        TestAssert.Equal("Bị chặn", Read(Read(blocked, "ScheduleCondition")!, "Label"), "Blocked schedule alerts must have highest schedule precedence.");
        TestAssert.Contains("Có 1 hạng mục lịch trình đang bị chặn.", Read(Read(blocked, "ScheduleCondition")!, "Detail")?.ToString() ?? string.Empty, "Blocked schedule detail must use the normative sentence.");
        TestAssert.Equal("Chưa đánh giá", Read(Read(blocked, "ReadinessCondition")!, "Label"), "A schedule alert must not change readiness condition.");
    }

    public static void ProjectionUsesEvidenceSafeProgressAndSupportedStateCounts()
    {
        var result = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult(new DateOnly(2026, 9, 19));
        var report = BuildReport(result);
        var progress = Read(report, "Progress")!;

        TestAssert.Equal(50, Read(progress, "RecordedPercent"), "Recorded percentage must use actual/(actual+remaining) and round to a whole percent.");
        TestAssert.Equal("Đã ghi nhận 50% theo nỗ lực thực tế và còn lại.", Read(progress, "Statement"), "Valid progress must use the normative sentence.");
        TestAssert.Equal(0, Read(progress, "CompletedCount"), "Completed count must remain evidence-backed.");
        TestAssert.Equal(1, Read(progress, "InProgressCount"), "In-progress count must remain evidence-backed.");
        TestAssert.Equal(52, Read(progress, "NotStartedCount"), "Not-started count must remain evidence-backed.");
        TestAssert.Equal(0, Read(progress, "UnknownCount"), "Unknown count must be the non-negative remainder.");

        foreach (var effort in new[]
        {
            new ExecutionEffortSummary { ActualEffortHours = null, RemainingEffortHours = 8m },
            new ExecutionEffortSummary { ActualEffortHours = -1m, RemainingEffortHours = 8m },
            new ExecutionEffortSummary { ActualEffortHours = 0m, RemainingEffortHours = 0m }
        })
        {
            var invalid = BuildReport(result with
            {
                Analysis = result.Analysis with { ExecutionEffort = effort }
            });
            var invalidProgress = Read(invalid, "Progress")!;
            TestAssert.Equal(null, Read(invalidProgress, "RecordedPercent"), "Invalid or zero-sum effort must not produce a percentage.");
            TestAssert.Equal("Chưa đủ dữ liệu để tính % hoàn thành", Read(invalidProgress, "Statement"), "Invalid effort must use the exact insufficient-data sentence.");
        }
    }

    public static void ProjectionBuildsConservativeWorkPackageAndDeliveryCardRows()
    {
        var report = new ProjectManagementCompiler.Management.ExecutiveProgressReportProjector()
            .Build(ExecutiveProgressTestFixtures.BuildOfficialFixtureResult(new DateOnly(2026, 9, 19)));

        TestAssert.Equal(35, report.WorkPackageSchedule.Count, "The executive schedule must contain one row per work package.");
        TestAssert.Equal(53, report.DeliveryCardDetails.Count, "The executive detail sheet must contain one row per delivery card.");

        var workPackage = report.WorkPackageSchedule.Single(row => row.DisplayName == "Planning package F05");
        TestAssert.Equal("Chưa bắt đầu", workPackage.StateLabel, "A work package whose evidenced children are all not started must be not started.");
        TestAssert.Equal("Phụ trách quy trình", workPackage.OwnerLabel, "A work package without direct readiness ownership must use the unanimous child accountable owner.");
        TestAssert.Equal("Planning package PH1", workPackage.PhaseDisplayName, "Work-package rows must retain the cleaned parent phase name.");

        var card = report.DeliveryCardDetails.Single(detail => detail.ReferenceCode == "P01-A");
        TestAssert.Equal("Planning card", card.Description, "Delivery-card descriptions must preserve source meaning without technical decoration.");
        TestAssert.Equal("Đang thực hiện", card.StateLabel, "Delivery-card state must come from official effective source execution.");
        TestAssert.Equal("Đầu mối dự án", card.OwnerLabel, "Delivery-card ownership must use the single CARIO-A accountable role.");
        TestAssert.Equal("P01-A", card.ReferenceCode, "The short raw card identity must remain available only as the final detail field.");
    }

    public static void ProjectionRanksActionableAttentionWithSourceBackedConsequences()
    {
        var result = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult(new DateOnly(2026, 9, 19));
        var milestone = result.Project.Milestones
            .Where(candidate => candidate.Kind == MilestoneKind.Milestone && candidate.PlannedDate >= new DateOnly(2026, 9, 19))
            .OrderBy(candidate => candidate.PlannedDate)
            .First();
        var evidence = new ManagementEvidence
        {
            DiscoveryState = ManagementEvidenceDiscoveryState.Known,
            Observations =
            [
                new ManagementEvidenceObservation
                {
                    Id = "decision:scope",
                    EvidenceKind = ManagementEvidenceKind.DecisionRecord,
                    SourceRecordId = milestone.Id,
                    StateCode = "OPEN",
                    ActionSummary = "Chốt phạm vi trước mốc",
                    GateEffectSummary = "AUTHORIZATION",
                    RequiredAuthorityRole = "PDA",
                    ValidationState = ValidationState.Known
                },
                new ManagementEvidenceObservation
                {
                    Id = "action:verify",
                    EvidenceKind = ManagementEvidenceKind.HumanAction,
                    SourceRecordId = "ACT-1",
                    StateCode = "OPEN",
                    ActionSummary = "Hoàn tất kiểm tra",
                    CompletionCondition = "Có kết quả kiểm tra",
                    WaitingForRole = "QA",
                    ValidationState = ValidationState.Known
                }
            ]
        };
        var alerts = new Alert[]
        {
            new Alert { WorkItemId = "P01-A", AlertCode = "BLOCKED", Severity = WarningSeverity.Error, Message = "blocked", DerivedAt = new DateOnly(2026, 9, 19) },
            new Alert { WorkItemId = "P04-A", AlertCode = "OVERDUE", Severity = WarningSeverity.Warning, Message = "late", DerivedAt = new DateOnly(2026, 9, 19) },
            new Alert { WorkItemId = "F01-B", AlertCode = "AT_RISK", Severity = WarningSeverity.Warning, Message = "risk", DerivedAt = new DateOnly(2026, 9, 19) }
        };
        var report = new ProjectManagementCompiler.Management.ExecutiveProgressReportProjector().Build(result with
        {
            Project = result.Project with { ManagementEvidence = evidence },
            Analysis = result.Analysis with { Alerts = alerts }
        });

        TestAssert.Equal("Blocked|Overdue|DecisionBeforeNextMilestone|AtRisk|PendingAction", string.Join('|', report.AllAttention.Select(item => item.Category)), "Attention must use the normative category priority.");
        TestAssert.Equal(4, report.OverviewAttention.Count, "The overview must retain up to five eligible attention items.");
        TestAssert.Equal(string.Join('|', report.AllAttention.Where(item => item.OverviewEligible).Take(5).Select(item => item.DeduplicationKey)), string.Join('|', report.OverviewAttention.Select(item => item.DeduplicationKey)), "Overview attention must be the first five eligible full-list items.");

        var blocked = report.AllAttention[0];
        TestAssert.Equal("Xử lý hạng mục “Planning card”.", blocked.Action, "Analysis alerts must use the fixed reader-facing action template.");
        TestAssert.Equal("Hạng mục đang bị chặn.", blocked.Impact, "Blocked alerts must use the fixed factual impact template.");
        var decision = report.AllAttention.Single(item => item.Category == ExecutiveAttentionCategory.DecisionBeforeNextMilestone);
        TestAssert.Equal("Thẩm quyền quyết định sản phẩm", decision.OwnerLabel, "Decision attention must use the required authority role.");
        TestAssert.Equal("Cần phê duyệt trước khi tiếp tục.", decision.Impact, "Known consequence tokens must use their approved Vietnamese translation.");
        TestAssert.Equal(milestone.PlannedDate!.Value, decision.DueDate, "A decision linked to the next milestone must carry that milestone date.");
    }

    private static CompilationResult WithSourceDate(CompilationResult result, DateOnly sourceDate) =>
        result with
        {
            Project = result.Project with
            {
                ImportMetadata = result.Project.ImportMetadata! with { RegisterStatusDate = sourceDate }
            }
        };

    private static object BuildReport(CompilationResult result)
    {
        var projectorType = Type.GetType("ProjectManagementCompiler.Management.ExecutiveProgressReportProjector, ProjectManagementCompiler", throwOnError: false);
        TestAssert.True(projectorType is not null, "ExecutiveProgressReportProjector must exist before projection regressions can pass.");
        var projector = Activator.CreateInstance(projectorType!);
        var build = projectorType!.GetMethod("Build", BindingFlags.Public | BindingFlags.Instance, [typeof(CompilationResult)]);
        TestAssert.True(build is not null, "ExecutiveProgressReportProjector must expose Build(CompilationResult).");
        return build!.Invoke(projector, [result]) ?? throw new InvalidOperationException("The executive projector returned no report.");
    }

    private static object? Read(object value, string propertyName) =>
        value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(value);

    private static DateOnly? ReadDate(object value, string propertyName) =>
        Read(value, propertyName) is DateOnly date ? date : null;

    private static IReadOnlyList<object> Items(object value, string propertyName) =>
        ((IEnumerable)(Read(value, propertyName) ?? Array.Empty<object>())).Cast<object>().ToArray();
}
