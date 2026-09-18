using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class AnalysisTests
{
    public static void StatusAnalysisUsesWorkingCalendarForLateStart()
    {
        var project = Apply(project: CaptureCanonicalProject(), new ExecutionUpdate
        {
            WorkItemId = "P01-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 21),
            LastUpdatedAt = UpdatedAt()
        });

        var analysis = new ManagementAnalysisOrchestrator().Analyze(project, new DateOnly(2026, 9, 21));
        var variance = analysis.WorkItemVariances.Single(item => item.WorkItemId == "P01-A");

        TestAssert.Equal(480, variance.StartVarianceWorkingMinutes, "Friday-to-Monday start variance must count one working day, not weekend calendar minutes.");
        TestAssert.True(analysis.Alerts.Any(alert => alert.WorkItemId == "P01-A" && alert.AlertCode == "START_DELAY"), "A late actual start must produce a START_DELAY alert.");
    }

    public static void StatusAnalysisDerivesOverdueWithoutChangingExecutionState()
    {
        var project = Apply(CaptureCanonicalProject(), new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 25),
            LastUpdatedAt = UpdatedAt()
        });

        var analysis = new ManagementAnalysisOrchestrator().Analyze(project, new DateOnly(2026, 9, 28));

        TestAssert.True(analysis.Alerts.Any(alert => alert.WorkItemId == "P04-A" && alert.AlertCode == "OVERDUE" && alert.VarianceWorkingMinutes == 480), "Monday after a Friday finish must derive 480 working minutes of active overdue variance.");
        TestAssert.Equal(ExecutionState.NotStarted, project.DeliveryCards.Single(card => card.Id == "P04-A").State, "Derived overdue must not mutate the source execution state.");
        TestAssert.Equal(ExecutionState.InProgress, project.ExecutionOverlay.Records.Single().ExecutionState, "Derived overdue must leave the manual execution state as IN_PROGRESS.");
    }

    public static void StatusAnalysisDerivesCompletedOnTimeAndLateSeparately()
    {
        var onTime = Apply(CaptureCanonicalProject(), new ExecutionUpdate
        {
            WorkItemId = "P01-A",
            ExecutionState = ExecutionState.Completed,
            ActualStart = new DateOnly(2026, 9, 18),
            ActualFinish = new DateOnly(2026, 9, 18),
            LastUpdatedAt = UpdatedAt()
        });
        var late = Apply(CaptureCanonicalProject(), new ExecutionUpdate
        {
            WorkItemId = "P01-B",
            ExecutionState = ExecutionState.Completed,
            ActualStart = new DateOnly(2026, 9, 21),
            ActualFinish = new DateOnly(2026, 9, 22),
            LastUpdatedAt = UpdatedAt()
        });

        var onTimeAnalysis = new ManagementAnalysisOrchestrator().Analyze(onTime, new DateOnly(2026, 9, 22));
        var lateAnalysis = new ManagementAnalysisOrchestrator().Analyze(late, new DateOnly(2026, 9, 22));

        TestAssert.True(onTimeAnalysis.Alerts.Any(alert => alert.WorkItemId == "P01-A" && alert.AlertCode == "COMPLETED_ON_TIME"), "Completion on or before the plan must be explicit.");
        TestAssert.True(lateAnalysis.Alerts.Any(alert => alert.WorkItemId == "P01-B" && alert.AlertCode == "COMPLETED_LATE" && alert.VarianceWorkingMinutes == 480), "Completion after the plan must report working-calendar finish variance.");
        TestAssert.Equal(1, lateAnalysis.ExecutionStatus.CompletedLate, "Completed-late dashboard count must be explicit.");
    }

    public static void CancelledIsNotOverdueAndSuspendedIsExplicit()
    {
        var cancelled = Apply(CaptureCanonicalProject(), new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.Cancelled,
            LastUpdatedAt = UpdatedAt()
        });
        var suspended = Apply(CaptureCanonicalProject(), new ExecutionUpdate
        {
            WorkItemId = "P04-B",
            ExecutionState = ExecutionState.Suspended,
            ActualStart = new DateOnly(2026, 9, 28),
            LastUpdatedAt = UpdatedAt()
        });

        var cancelledAnalysis = new ManagementAnalysisOrchestrator().Analyze(cancelled, new DateOnly(2026, 10, 1));
        var suspendedAnalysis = new ManagementAnalysisOrchestrator().Analyze(suspended, new DateOnly(2026, 10, 1));

        TestAssert.False(cancelledAnalysis.Alerts.Any(alert => alert.WorkItemId == "P04-A" && alert.AlertCode == "OVERDUE"), "Cancelled work must not remain overdue.");
        TestAssert.True(cancelledAnalysis.Alerts.Any(alert => alert.WorkItemId == "P04-A" && alert.AlertCode == "CANCELLED"), "Cancellation must remain visible as an explicit condition.");
        TestAssert.True(suspendedAnalysis.Alerts.Any(alert => alert.WorkItemId == "P04-B" && alert.AlertCode == "SUSPENDED"), "Suspension must remain visible as an explicit condition.");
    }

    public static void UnstartedPastFinishIsLateToStartButNotActiveOverdue()
    {
        var analysis = new ManagementAnalysisOrchestrator().Analyze(CaptureCanonicalProject(), new DateOnly(2026, 9, 28));

        TestAssert.True(analysis.Alerts.Any(alert => alert.WorkItemId == "P04-A" && alert.AlertCode == "START_DELAY"), "An unstarted item after its planned finish must remain visibly late to start.");
        TestAssert.False(analysis.Alerts.Any(alert => alert.WorkItemId == "P04-A" && alert.AlertCode == "OVERDUE"), "MVP1 treats OVERDUE as active in-progress work beyond finish, not unstarted work.");
        TestAssert.Equal(0, analysis.ExecutionStatus.Overdue, "Unstarted work must not inflate the active-overdue count.");
    }

    public static void LatePredecessorMarksUnstartedSuccessorAtRiskConservatively()
    {
        var project = Apply(CaptureCanonicalProject(), new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 25),
            LastUpdatedAt = UpdatedAt()
        });

        var analysis = new ManagementAnalysisOrchestrator().Analyze(project, new DateOnly(2026, 9, 28));
        var risk = analysis.Alerts.Single(alert => alert.WorkItemId == "P04-B" && alert.AlertCode == "AT_RISK");

        TestAssert.True(risk.ReasonWorkItemIds.Contains("P04-A"), "At-risk evidence must name the late predecessor.");
        TestAssert.False(analysis.Alerts.Any(alert => alert.WorkItemId == "P04-B" && alert.AlertCode == "FORECAST_DELAY"), "Conservative risk must not fabricate a deterministic successor delay.");
        TestAssert.Equal(1, analysis.ExecutionStatus.AtRisk, "At-risk dashboard count must be explicit.");
    }

    public static void InvalidDependencyEvidenceCannotFabricateDownstreamRisk()
    {
        var project = CaptureCanonicalProject();
        var missing = project.Dependencies.Single(dependency => dependency.PredecessorId == "X99");
        var analysis = new ManagementAnalysisOrchestrator().Analyze(project, new DateOnly(2026, 12, 31));

        TestAssert.False(analysis.Alerts.Any(alert => alert.AlertCode == "AT_RISK" && alert.WorkItemId == missing.SubjectId), "Invalid source dependency evidence must not create fabricated downstream risk.");
    }

    public static void AtRiskAggregatesDelayedCardPredecessorsAndIgnoresWorkPackageEdges()
    {
        var captured = CaptureCanonicalProject("ideaengineering-real-shaped");
        var project = captured with
        {
            DeliveryCards = captured.DeliveryCards
                .Select(card => card with { State = ExecutionState.NotStarted })
                .ToArray()
        };
        var delayedP03 = Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P03",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 12, 1),
            LastUpdatedAt = UpdatedAt()
        });
        var delayedP05 = Apply(delayedP03, new ExecutionUpdate
        {
            WorkItemId = "P05",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 12, 1),
            LastUpdatedAt = UpdatedAt()
        });
        var delayedP06 = Apply(delayedP05, new ExecutionUpdate
        {
            WorkItemId = "P06",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 12, 1),
            LastUpdatedAt = UpdatedAt()
        });

        var analysis = new ManagementAnalysisOrchestrator().Analyze(delayedP06, new DateOnly(2026, 12, 2));
        var p04Risks = analysis.Alerts.Where(alert => alert.WorkItemId == "P04" && alert.AlertCode == "AT_RISK").ToArray();
        var p07Risks = analysis.Alerts.Where(alert => alert.WorkItemId == "P07" && alert.AlertCode == "AT_RISK").ToArray();

        TestAssert.Equal(1, p04Risks.Length, "A WorkPackage:P04 trace edge must not double-count a DeliveryCard:P04 risk.");
        TestAssert.Equal("P03", string.Join(",", p04Risks[0].ReasonWorkItemIds), "DeliveryCard P04 risk must use the card predecessor only.");
        TestAssert.Equal(1, p07Risks.Length, "Two delayed predecessors must create one AT_RISK alert for successor P07.");
        TestAssert.Equal("P05,P06", string.Join(",", p07Risks[0].ReasonWorkItemIds), "The aggregated risk must preserve both delayed card predecessors.");
        TestAssert.Equal(2, analysis.ExecutionStatus.AtRisk, "At-risk count must count distinct successors, not dependency edges.");
    }

    private static DateTimeOffset UpdatedAt() => new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    private static CanonicalProject Apply(CanonicalProject project, ExecutionUpdate update)
    {
        var result = new ExecutionOverlayUpdater().Apply(project, update);
        TestAssert.True(result.Accepted, "The analysis fixture update should be accepted.");
        return result.Project;
    }

    private static CanonicalProject CaptureCanonicalProject(string fixtureName = "ideaengineering")
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", fixtureName);
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(new SourceRequest { Location = root }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var resolution = AuthorityResolution.Resolve(snapshot);
        return new CanonicalProjectNormalizer().Normalize(new IdeaEngineeringExtractor().Extract(resolution));
    }
}
