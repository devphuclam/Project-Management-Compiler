using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class ProjectOverviewProjectionTests
{
    public static void ProgressCoverageWithholdsPartialProjectPercentAndLabelsPartialTotals()
    {
        var project = Project(
            Card("P01", 4m, 0m),
            Card("P02", null, null));
        var analysis = Analysis(actualEffortHours: 4m, remainingEffortHours: 0m, completed: 1);

        var summary = new ProgressCoverageProjector().Build(project, analysis);

        TestAssert.Equal(null, summary.RecordedPercent, "Partial effort coverage must not be represented as a project-wide percentage.");
        TestAssert.False(summary.CoverageComplete, "A project with an ineligible delivery card must have incomplete effort coverage.");
        TestAssert.Equal(1, summary.EligibleCardCount, "Coverage must report the number of eligible delivery cards.");
        TestAssert.Equal(2, summary.TotalCardCount, "Coverage must report all delivery cards in the project.");
        TestAssert.Equal(ProgressCoverageState.Partial, summary.CoverageState, "Subset effort totals must be explicitly labeled partial.");
        TestAssert.Equal(1, summary.CompletedCardCount, "Completed-item count must remain separate from effort progress.");
    }

    public static void ProgressCoverageCalculatesCompleteEffortAndRoundsMidpointsAwayFromZero()
    {
        var project = Project(Card("P01", 1m, 7m));
        var analysis = Analysis(actualEffortHours: 1m, remainingEffortHours: 7m, completed: 0);

        var summary = new ProgressCoverageProjector().Build(project, analysis);

        TestAssert.Equal(13, summary.RecordedPercent, "A complete 12.5% effort ratio must round to 13%.");
        TestAssert.True(summary.CoverageComplete, "A project whose every card has an eligible pair must have complete coverage.");
        TestAssert.Equal(ProgressCoverageState.Complete, summary.CoverageState, "Complete evidence must not be labeled partial.");
        TestAssert.Equal(0, summary.CompletedCardCount, "Effort-derived percentage must not manufacture completed cards.");
    }

    public static void ProgressCoverageRejectsMissingNegativeAndZeroTotalEffortPairs()
    {
        foreach (var invalidRecord in new[]
        {
            Card("P01", null, 4m),
            Card("P01", -1m, 3m),
            Card("P01", 0m, 0m)
        })
        {
            var project = Project(invalidRecord);
            var actual = invalidRecord.ActualEffortHours;
            var remaining = invalidRecord.RemainingEffortHours;
            var analysis = Analysis(actual, remaining, completed: 1);

            var summary = new ProgressCoverageProjector().Build(project, analysis);

            TestAssert.Equal(null, summary.RecordedPercent, "Missing, negative, and zero-total effort pairs must never produce a project percentage.");
            TestAssert.False(summary.CoverageComplete, "An invalid effort pair must make coverage incomplete.");
            TestAssert.Equal(0, summary.EligibleCardCount, "Invalid effort pairs must not count toward eligible coverage.");
        }
    }

    public static void OverviewUsesOfficialReportingDateAndRequiresOneCurrentPhase()
    {
        var sourceDate = new DateOnly(2026, 9, 15);
        var project = OfficialProject(sourceDate, [
            new Phase { Id = "PH0", Name = "[PH0] Chuẩn bị", PlannedStart = new DateOnly(2026, 9, 1), PlannedFinish = new DateOnly(2026, 9, 30) },
            new Phase { Id = "PH1", Name = "[PH1] Triển khai", PlannedStart = new DateOnly(2026, 10, 1), PlannedFinish = new DateOnly(2026, 10, 31) }
        ]);
        var analysis = new ManagementAnalysis { AsOfDate = new DateOnly(2026, 9, 29) };

        var overview = new ProjectOverviewProjector().Build(project, analysis);

        TestAssert.Equal(sourceDate, overview.Project.ReportingDate, "Overview position must use the official register status date, not the analysis override.");
        TestAssert.Equal("Chuẩn bị", overview.CurrentPhase.Name, "The phase name must use the shared reader-facing cleanup policy.");
        TestAssert.Equal(DataState.Known, overview.CurrentPhase.State, "A unique valid containing phase must be marked known.");
        TestAssert.Equal("0123456789abcdef0123456789abcdef01234567", overview.Project.SourceIdentity, "The overview must retain the exact imported source identity.");

        var ambiguous = OfficialProject(sourceDate, [
            new Phase { Id = "PH0", Name = "Preparation", PlannedStart = new DateOnly(2026, 9, 1), PlannedFinish = new DateOnly(2026, 9, 20) },
            new Phase { Id = "PH1", Name = "Delivery", PlannedStart = new DateOnly(2026, 9, 10), PlannedFinish = new DateOnly(2026, 9, 25) }
        ]);
        var unknown = new ProjectOverviewProjector().Build(ambiguous, analysis);
        TestAssert.Equal(DataState.Unknown, unknown.CurrentPhase.State, "Overlapping phase ranges must not be guessed by source order.");
        TestAssert.Equal(null, unknown.CurrentPhase.Name, "Ambiguous phase selection must not present one phase as current.");
    }

    public static void OverviewSelectsEarliestFutureIncompleteControlPointAndPreservesItsState()
    {
        var reportingDate = new DateOnly(2026, 9, 20);
        var project = OfficialProject(reportingDate, []) with
        {
            Milestones = [
                new MilestoneDecision { Id = "G0", Name = "Today", Kind = MilestoneKind.Milestone, PlannedDate = reportingDate },
                new MilestoneDecision { Id = "G1", Name = "Already complete", Kind = MilestoneKind.Milestone, PlannedDate = new DateOnly(2026, 9, 21), State = ExecutionState.Completed },
                new MilestoneDecision { Id = "G2", Name = "[G2] Review", Kind = MilestoneKind.Decision, PlannedDate = new DateOnly(2026, 9, 22) },
                new MilestoneDecision { Id = "G3", Name = "Later point", Kind = MilestoneKind.Milestone, PlannedDate = new DateOnly(2026, 9, 25), State = ExecutionState.NotStarted }
            ]
        };

        var overview = new ProjectOverviewProjector().Build(project, new ManagementAnalysis());

        TestAssert.Equal("Review", overview.NextControlPoint!.Name, "The earliest future incomplete milestone or decision must be selected.");
        TestAssert.Equal(new DateOnly(2026, 9, 22), overview.NextControlPoint.PlannedDate, "The selected control-point date must remain source-backed.");
        TestAssert.Equal(MilestoneKind.Decision, overview.NextControlPoint.Kind, "Decision points must be eligible control points.");
        TestAssert.Equal(null, overview.NextControlPoint.SourceState, "An absent source state must stay unknown rather than being inferred from its date.");
    }

    public static void OverviewUsesStableSourceOrderForTiedControlPointsAndReportsEmptyState()
    {
        var reportingDate = new DateOnly(2026, 9, 20);
        var tied = OfficialProject(reportingDate, []) with
        {
            Milestones = [
                new MilestoneDecision { Id = "G2", Name = "First source row", Kind = MilestoneKind.Milestone, PlannedDate = new DateOnly(2026, 9, 22) },
                new MilestoneDecision { Id = "G1", Name = "Second source row", Kind = MilestoneKind.Decision, PlannedDate = new DateOnly(2026, 9, 22) }
            ]
        };
        var overview = new ProjectOverviewProjector().Build(tied, new ManagementAnalysis());
        TestAssert.Equal("First source row", overview.NextControlPoint!.Name, "Equal dates must preserve source order before using identifier as a final stable tie-break.");

        var empty = new ProjectOverviewProjector().Build(OfficialProject(reportingDate, []), new ManagementAnalysis());
        TestAssert.Equal(null, empty.NextControlPoint, "An overview without a future dated milestone or decision must keep the field empty.");
        TestAssert.Equal(DataState.Unknown, empty.CurrentPhase.State, "An overview without a uniquely dated current phase must be explicitly unknown.");
    }

    public static void OverviewMapsSupportedAlertsToConciseConsequencesWithoutRawDiagnostics()
    {
        var consequences = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["START_DELAY"] = "Chưa bắt đầu đúng kế hoạch.",
            ["OVERDUE"] = "Đang kéo dài quá ngày dự kiến.",
            ["SUSPENDED"] = "Đang tạm dừng.",
            ["AT_RISK"] = "Có nguy cơ chậm do công việc trước bị trễ."
        };
        var reportingDate = new DateOnly(2026, 9, 20);

        foreach (var (code, consequence) in consequences)
        {
            var project = OfficialProject(reportingDate, []) with
            {
                DeliveryCards = [new DeliveryCard { Id = "P01", Name = "[P01] Chuẩn bị môi trường" }]
            };
            var analysis = new ManagementAnalysis
            {
                Alerts = [new Alert
                {
                    WorkItemId = "P01",
                    AlertCode = code,
                    Severity = WarningSeverity.Warning,
                    Message = "RAW SECRET DIAGNOSTIC TEXT"
                }]
            };

            var item = new ProjectOverviewProjector().Build(project, analysis).AttentionItems.Single();

            TestAssert.Equal("P01", item.WorkItemId, "An attention item must retain a secondary target identity for its Gantt destination.");
            TestAssert.Equal("Chuẩn bị môi trường", item.WorkItemName, "Attention names must be cleaned for readers.");
            TestAssert.Equal(consequence, item.Consequence, "Supported alert codes must map to concise deterministic Vietnamese copy.");
            TestAssert.Equal("gantt", item.Destination, "Attention items must direct users to the detailed schedule.");
            var serialized = System.Text.Json.JsonSerializer.Serialize(item);
            TestAssert.False(serialized.Contains("RAW SECRET", StringComparison.Ordinal), "Raw diagnostic messages must not enter the overview contract.");
            TestAssert.False(serialized.Contains(code, StringComparison.Ordinal), "Machine alert codes must not enter the overview contract.");
        }

        var unsupported = new ProjectOverviewProjector().Build(
            OfficialProject(reportingDate, []) with
            {
                DeliveryCards = [new DeliveryCard { Id = "P01", Name = "Visible work" }]
            },
            new ManagementAnalysis
            {
                Alerts = [new Alert { WorkItemId = "P01", AlertCode = "COMPLETED_LATE", Message = "Do not paraphrase" }]
            });
        TestAssert.Equal(0, unsupported.AttentionItems.Count, "Unsupported alert types must be omitted rather than guessed or shown raw.");
    }

    public static void OverviewOrdersAndCapsAttentionItemsDeterministically()
    {
        var project = OfficialProject(new DateOnly(2026, 9, 20), []) with
        {
            DeliveryCards = new[] { "P01", "P02", "P03", "P04", "P05" }
                .Select(id => new DeliveryCard { Id = id, Name = $"Work {id}" })
                .ToArray()
        };
        var analysis = new ManagementAnalysis
        {
            Alerts = [
                new Alert { WorkItemId = "P01", AlertCode = "START_DELAY", Severity = WarningSeverity.Warning, DerivedAt = new DateOnly(2026, 9, 19) },
                new Alert { WorkItemId = "P02", AlertCode = "OVERDUE", Severity = WarningSeverity.Warning, DerivedAt = new DateOnly(2026, 9, 18) },
                new Alert { WorkItemId = "P03", AlertCode = "SUSPENDED", Severity = WarningSeverity.Warning, DerivedAt = new DateOnly(2026, 9, 18) },
                new Alert { WorkItemId = "P04", AlertCode = "AT_RISK", Severity = WarningSeverity.Warning, DerivedAt = new DateOnly(2026, 9, 17) },
                new Alert { WorkItemId = "P05", AlertCode = "UNSUPPORTED_SECRET_CODE", Severity = WarningSeverity.Error, Message = "Raw error" }
            ]
        };

        var overview = new ProjectOverviewProjector().Build(project, analysis);

        TestAssert.True(
            overview.AttentionItems.Select(item => item.WorkItemId).SequenceEqual(new[] { "P04", "P02", "P03" }),
            "Attention items must filter unsupported types, order severity/date/identity deterministically, and stop at three.");
    }

    public static void OverviewIsPartOfAggregateAndNamedApiView()
    {
        var reportingDate = new DateOnly(2026, 9, 20);
        var project = OfficialProject(reportingDate, []);
        var views = new ManagementViewProjector().Build(project, new ManagementAnalysis(), reportingDate.AddDays(2));

        TestAssert.Equal("Dự án thử nghiệm", views.Overview.Project.Name, "The aggregate management view must include the read-only Overview projection.");
        TestAssert.Equal(reportingDate, views.Overview.Project.ReportingDate, "Aggregate Overview must keep official source reporting-date semantics.");

        var program = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "Program.cs"));
        TestAssert.Contains("app.MapGet(\"/api/views/{viewName}\"", program, "The existing named-view route must serve Overview without introducing a parallel endpoint.");
        TestAssert.Contains("\"overview\" => Results.Ok(current.Views.Overview)", program, "The API must expose GET /api/views/overview from the active view set.");
        TestAssert.Contains("Code = \"NO_PROJECT\"", program, "The named-view route must retain the existing no-project response contract.");
    }

    private static CanonicalProject Project(params ExecutionRecord[] records)
    {
        var cards = records.Select(record => new DeliveryCard { Id = record.WorkItemId, Name = record.WorkItemId }).ToArray();
        return new CanonicalProject
        {
            DeliveryCards = cards,
            ExecutionOverlay = new ExecutionOverlay { Records = records }
        };
    }

    private static ExecutionRecord Card(string id, decimal? actualEffortHours, decimal? remainingEffortHours) => new()
    {
        WorkItemId = id,
        ExecutionState = ExecutionState.Completed,
        ActualEffortHours = actualEffortHours,
        RemainingEffortHours = remainingEffortHours
    };

    private static ManagementAnalysis Analysis(decimal? actualEffortHours, decimal? remainingEffortHours, int completed) => new()
    {
        ExecutionEffort = new ExecutionEffortSummary
        {
            ActualEffortHours = actualEffortHours,
            RemainingEffortHours = remainingEffortHours
        },
        ExecutionStatus = new ExecutionStatusCounts { Completed = completed }
    };

    private static CanonicalProject OfficialProject(DateOnly reportingDate, IReadOnlyList<Phase> phases) => new()
    {
        Project = new Project { Id = "PRJ-01", Name = "[PRJ-01] Dự án thử nghiệm" },
        ImportMetadata = new ManifestSnapshotMetadata
        {
            Classification = ManifestImportClassification.OfficialCommit,
            SourceIdentity = "0123456789abcdef0123456789abcdef01234567",
            RegisterStatusDate = reportingDate
        },
        Phases = phases
    };
}
