using System.Collections;
using System.Reflection;
using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Tests;

internal static class ExecutiveDailyGanttProjectionTests
{
    public static void DailyGanttProjectsDirectActualForecastAndEffortTruthfully()
    {
        var analysisDate = new DateOnly(2026, 10, 2);
        var projection = BuildProjection(ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult(
            analysisAsOfDate: analysisDate));

        var completedEarly = FullRow(projection, ExecutiveProgressTestFixtures.CompletedEarlyCardId);
        TestAssert.Equal(new DateOnly(2026, 9, 18), ReadDate(completedEarly, "PlannedStart"), "Plan must remain the immutable baseline date even when Actual starts early.");
        TestAssert.Equal(new DateOnly(2026, 9, 16), ReadDate(completedEarly, "ActualStart"), "A completed card must retain its direct official Actual start.");
        TestAssert.Equal(new DateOnly(2026, 9, 17), ReadDate(completedEarly, "ActualFinish"), "A completed card must retain its direct official Actual finish.");
        TestAssert.Equal(new DateOnly(2026, 9, 17), ReadDate(completedEarly, "ActualDisplayThrough"), "A completed Actual lane must end on its recorded finish.");
        TestAssert.Equal(100, ReadInt(completedEarly, "ProgressPercent"), "Completed official effort must produce its direct percentage.");

        var openInProgress = FullRow(projection, ExecutiveProgressTestFixtures.OpenInProgressCardId);
        TestAssert.Equal(new DateOnly(2026, 9, 21), ReadDate(openInProgress, "ActualStart"), "An in-progress card must retain its official Actual start.");
        TestAssert.Equal(null, ReadDate(openInProgress, "ActualFinish"), "An in-progress card must not receive a fabricated Actual finish.");
        TestAssert.Equal(analysisDate, ReadDate(openInProgress, "ActualDisplayThrough"), "An open Actual lane must display through the analysis date, not the source reporting date.");
        TestAssert.Equal(new DateOnly(2026, 10, 6), ReadDate(openInProgress, "ForecastFinish"), "A direct forecast must come from the official source record.");
        TestAssert.Equal(13, ReadInt(openInProgress, "ProgressPercent"), "1/8 effort must round 12.5% away from zero to 13%.");
        TestAssert.Equal("Đang thực hiện", ReadText(openInProgress, "StateLabel"), "Direct execution state must use the approved reader-facing label.");
        TestAssert.True(ReadBoolean(openInProgress, "IsOverdue"), "An official in-progress card beyond its baseline finish must retain its derived overdue cue.");

        var suspended = FullRow(projection, ExecutiveProgressTestFixtures.SuspendedCardId);
        TestAssert.Equal("Tạm dừng", ReadText(suspended, "StateLabel"), "Suspended work must remain visibly suspended.");
        TestAssert.True(ReadBoolean(suspended, "IsBlocked"), "Recorded blocking evidence must produce the blocked cue.");
        TestAssert.Equal(new DateOnly(2026, 9, 26), ReadDate(suspended, "ActualDisplayThrough"), "Suspended work may show only its recorded past Actual interval.");

        var oneDay = FullRow(projection, ExecutiveProgressTestFixtures.OneDayCompletedCardId);
        TestAssert.Equal(ReadDate(oneDay, "ActualStart"), ReadDate(oneDay, "ActualFinish"), "A completed one-day card must remain a one-day Actual interval.");
    }

    public static void DailyGanttKeepsMissingActualAndUnofficialProposalDataExplicit()
    {
        var result = ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult();
        var proposalOnly = result with
        {
            Project = result.Project with
            {
                ExecutionOverlay = new ExecutionOverlay
                {
                    Records =
                    [
                        new ExecutionRecord
                        {
                            WorkItemId = ExecutiveProgressTestFixtures.UnrecordedCardId,
                            ExecutionState = ExecutionState.Completed,
                            ActualStart = new DateOnly(2026, 9, 24),
                            ActualFinish = new DateOnly(2026, 9, 24),
                            ActualEffortHours = 4m,
                            RemainingEffortHours = 0m,
                            LastUpdatedAt = new DateTimeOffset(2026, 9, 24, 16, 0, 0, TimeSpan.Zero)
                        }
                    ]
                },
                ExecutionProposals =
                [
                    new ExecutionProposal
                    {
                        Id = "proposal-only-p03-b",
                        BaseSnapshotId = result.Project.ImportMetadata!.SnapshotId,
                        ExpectedRegisterRevision = result.Project.ImportMetadata.RegisterRevision,
                        TargetKind = "DeliveryCard",
                        TargetId = ExecutiveProgressTestFixtures.UnrecordedCardId,
                        Lifecycle = ProposalLifecycle.ReadyForReview,
                        ProposedChanges = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["actualStart"] = "2026-09-24",
                            ["actualFinish"] = "2026-09-24",
                            ["forecastFinish"] = "2026-10-31"
                        },
                        CreatedAtUtc = new DateTimeOffset(2026, 9, 24, 16, 0, 0, TimeSpan.Zero),
                        UpdatedAtUtc = new DateTimeOffset(2026, 9, 24, 16, 0, 0, TimeSpan.Zero)
                    }
                ]
            }
        };
        var projection = BuildProjection(proposalOnly);

        var unrecorded = FullRow(projection, ExecutiveProgressTestFixtures.UnrecordedCardId);
        TestAssert.Equal("Chưa cập nhật", ReadText(unrecorded, "StateLabel"), "A source record marked not recorded must not be relabelled as not started.");
        TestAssert.Equal(null, ReadDate(unrecorded, "ActualStart"), "Overlay or proposal Actual data must not become official Actual.");
        TestAssert.Equal(null, ReadDate(unrecorded, "ActualFinish"), "Overlay or proposal completion must not become official Actual.");
        TestAssert.Equal(null, ReadDate(unrecorded, "ForecastFinish"), "Only an official source forecast may appear on a daily Gantt row.");
        TestAssert.Equal(null, ReadRequired(unrecorded, "ProgressPercent"), "Unrecorded work must not receive a fabricated percentage.");
        TestAssert.Equal("Chưa ghi nhận", ReadText(unrecorded, "ProgressLabel"), "Unrecorded work must state its missing evidence explicitly.");
        TestAssert.Equal(null, ReadRequired(unrecorded, "LastOfficialUpdate"), "Unrecorded work must not inherit a local update timestamp.");

        var inProgressWithoutStart = FullRow(projection, ExecutiveProgressTestFixtures.InProgressWithoutStartCardId);
        TestAssert.Equal("Đang thực hiện", ReadText(inProgressWithoutStart, "StateLabel"), "Explicit in-progress state must remain visible even when Actual dates are incomplete.");
        TestAssert.Equal(null, ReadDate(inProgressWithoutStart, "ActualStart"), "An in-progress record without a start must not draw a green Actual lane.");
        TestAssert.Equal(null, ReadDate(inProgressWithoutStart, "ActualDisplayThrough"), "An in-progress record without a start must not receive a display-through date.");
        TestAssert.Equal(50, ReadInt(inProgressWithoutStart, "ProgressPercent"), "Valid effort evidence remains separately visible even without an Actual lane.");

        var oneSided = FullRow(projection, ExecutiveProgressTestFixtures.OneSidedEffortCardId);
        var zeroSum = FullRow(projection, ExecutiveProgressTestFixtures.ZeroSumEffortCardId);
        var missing = FullRow(projection, ExecutiveProgressTestFixtures.MissingEffortCardId);
        foreach (var row in new[] { oneSided, zeroSum, missing })
        {
            TestAssert.Equal(null, ReadRequired(row, "ProgressPercent"), "One-sided, zero-sum, or missing effort must not be coerced into zero percent.");
            TestAssert.Equal("Chưa ghi nhận", ReadText(row, "ProgressLabel"), "Insufficient effort must use the compact reader-facing label.");
        }

        var explicitNotStarted = FullRow(projection, ExecutiveProgressTestFixtures.ExplicitNotStartedCardId);
        TestAssert.Equal("Chưa bắt đầu", ReadText(explicitNotStarted, "StateLabel"), "An explicit recorded not-started state must remain distinct from unrecorded work.");
        TestAssert.Equal(null, ReadDate(explicitNotStarted, "ActualStart"), "Not-started work must not receive an Actual lane.");
    }

    public static void DailyGanttBuildsStableHierarchyOverviewAndMilestoneRows()
    {
        var projection = BuildProjection(ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult());
        var full = Items(projection, "FullRows");
        var kinds = full.Select(row => ReadText(row, "Kind")).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal);

        TestAssert.Equal("DeliveryCard|Milestone|Phase|Project|WorkPackage", string.Join('|', kinds), "The daily Gantt must expose exactly the approved row kinds.");
        TestAssert.Equal(new DateOnly(2026, 9, 16), ReadDate(projection, "FullStart"), "The full axis must retain attributable Actual dates outside the baseline window.");
        TestAssert.Equal(new DateOnly(2026, 12, 31), ReadDate(projection, "FullFinish"), "The full axis must retain the immutable baseline finish.");

        var project = full.Single(row => ReadText(row, "Kind") == "Project");
        var phase = full.Single(row => ReadText(row, "Kind") == "Phase" && ReadText(row, "ReferenceCode") == "PH0");
        var workPackage = FullRow(projection, ExecutiveProgressTestFixtures.RepeatedRawId, "WorkPackage");
        var card = FullRow(projection, ExecutiveProgressTestFixtures.CompletedEarlyCardId);
        var milestone = FullRow(projection, ExecutiveProgressTestFixtures.RepeatedRawId, "Milestone");
        TestAssert.Equal(0, ReadInt(project, "HierarchyLevel"), "Project must occupy hierarchy level 0.");
        TestAssert.Equal(1, ReadInt(phase, "HierarchyLevel"), "Phase must occupy hierarchy level 1.");
        TestAssert.Equal(2, ReadInt(workPackage, "HierarchyLevel"), "Work package must occupy hierarchy level 2.");
        TestAssert.Equal(3, ReadInt(card, "HierarchyLevel"), "Delivery card must occupy hierarchy level 3.");
        TestAssert.Equal(new DateOnly(2026, 9, 24), ReadDate(milestone, "PlannedStart"), "Milestones must retain their zero-duration plan date.");
        TestAssert.Equal(new DateOnly(2026, 9, 24), ReadDate(milestone, "PlannedFinish"), "Milestones must remain zero-duration on the daily axis.");
        TestAssert.Equal(null, ReadDate(milestone, "ActualStart"), "Milestones must not borrow Actual execution dates from delivery cards.");
        TestAssert.True(IndexOf(full, ExecutiveProgressTestFixtures.CompletedEarlyCardId, "DeliveryCard") < IndexOf(full, ExecutiveProgressTestFixtures.CompletedLateCardId, "DeliveryCard"), "Delivery cards must retain stable canonical source order.");

        var overview = Items(projection, "OverviewRows");
        TestAssert.True(overview.All(row => ReadText(row, "Kind") is "Project" or "Phase" or "Milestone"), "Overview rows must exclude work packages and delivery cards.");
        TestAssert.True(overview.Any(row => ReadText(row, "Kind") == "Milestone"), "Overview rows must retain milestone context.");

        var result = ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult();
        var orphanId = ExecutiveProgressTestFixtures.MissingEffortCardId;
        var orphanResult = result with
        {
            Project = result.Project with
            {
                DeliveryCards = result.Project.DeliveryCards
                    .Select(item => item.Id == orphanId
                        ? item with { WorkPackageId = "UNKNOWN-WORK-PACKAGE", ParentId = "UNKNOWN-WORK-PACKAGE" }
                        : item)
                    .ToArray()
            }
        };
        var orphan = FullRow(BuildProjection(orphanResult), orphanId);
        TestAssert.Equal("Chưa xác định gói công việc", ReadText(orphan, "WorkPackageDisplayName"), "An orphaned delivery card must remain visible with an explicit unknown-parent label.");
    }

    public static void DailyGanttRollsUpCoverageActualAndForecastConservatively()
    {
        var projection = BuildProjection(ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult());

        var fullyRecorded = FullRow(projection, "P01", "WorkPackage");
        TestAssert.Equal(2, ReadInt(fullyRecorded, "TotalChildCount"), "A work-package roll-up must count all descendant cards.");
        TestAssert.Equal(2, ReadInt(fullyRecorded, "RecordedChildCount"), "A fully recorded work package must disclose complete evidence coverage.");
        TestAssert.Equal(2, ReadInt(fullyRecorded, "ProgressEligibleChildCount"), "Every effort-eligible child must be counted separately.");
        TestAssert.Equal(100, ReadInt(fullyRecorded, "ProgressPercent"), "A complete work-package scope may show its aggregate percentage.");
        TestAssert.Equal("Độ phủ 2/2", ReadText(fullyRecorded, "CoverageLabel"), "Complete coverage must remain explicit beside a roll-up percentage.");
        TestAssert.Equal(new DateOnly(2026, 9, 16), ReadDate(fullyRecorded, "ActualStart"), "Roll-up Actual must start at the earliest valid child Actual start.");
        TestAssert.Equal(new DateOnly(2026, 9, 23), ReadDate(fullyRecorded, "ActualDisplayThrough"), "Completed roll-up Actual must end at the latest child completion.");

        var partial = FullRow(projection, "P03", "WorkPackage");
        TestAssert.Equal(2, ReadInt(partial, "TotalChildCount"), "Partial coverage must retain the full child denominator.");
        TestAssert.Equal(1, ReadInt(partial, "RecordedChildCount"), "Only official recorded children count toward coverage.");
        TestAssert.Equal(null, ReadRequired(partial, "ProgressPercent"), "A partial work-package scope must not present a percentage as complete.");
        TestAssert.Equal("Độ phủ 1/2", ReadText(partial, "CoverageLabel"), "Partial work must state its coverage fraction.");
        TestAssert.Equal("Chưa cập nhật", ReadText(partial, "StateLabel"), "Mixed recorded and unrecorded children must use the conservative unknown state.");

        var forecastPartial = FullRow(projection, "P02", "WorkPackage");
        TestAssert.Equal(25, ReadInt(forecastPartial, "ProgressPercent"), "A fully effort-eligible two-card scope must aggregate its actual and remaining effort.");
        TestAssert.Equal(null, ReadDate(forecastPartial, "ForecastFinish"), "A partial child forecast set must not become a roll-up forecast.");
        TestAssert.Equal(new DateOnly(2026, 9, 30), ReadDate(forecastPartial, "ActualDisplayThrough"), "An open child must extend a roll-up Actual display only through the analysis date.");

        var project = Items(projection, "FullRows").Single(row => ReadText(row, "Kind") == "Project");
        TestAssert.Equal(53, ReadInt(project, "TotalChildCount"), "The project roll-up must include every delivery card.");
        TestAssert.Equal(11, ReadInt(project, "RecordedChildCount"), "The project roll-up must not count an explicit not-recorded source record as evidence.");
        TestAssert.Equal("Độ phủ 11/53", ReadText(project, "CoverageLabel"), "Project coverage must disclose sparse official evidence.");
        TestAssert.Equal(null, ReadRequired(project, "ProgressPercent"), "Sparse project evidence must not be presented as a whole-project completion percentage.");
        TestAssert.Equal(null, ReadDate(project, "ForecastFinish"), "Incomplete non-completed forecasts must not become a project forecast.");
    }

    public static void DailyGanttSelectsExactNearTermWindowAndOrdersOverdueWork()
    {
        var result = ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult();
        var withNearTermMilestone = result with
        {
            Project = result.Project with
            {
                Milestones = result.Project.Milestones
                    .Concat(
                    [
                        new MilestoneDecision
                        {
                            Id = "NT-M1",
                            Kind = MilestoneKind.Milestone,
                            ParentId = "PH0",
                            Name = "Mốc nằm trong cửa sổ 30 ngày",
                            PlannedDate = new DateOnly(2026, 10, 2),
                            State = ExecutionState.NotStarted,
                            PlannedDurationWorkingMinutes = 0
                        }
                    ])
                    .ToArray()
            }
        };
        var projection = BuildProjection(withNearTermMilestone);
        var nearTerm = Items(projection, "NearTermRows");

        TestAssert.Equal(new DateOnly(2026, 9, 30), ReadDate(projection, "NearTermStart"), "The near-term window must start on the official source reporting date.");
        TestAssert.Equal(new DateOnly(2026, 10, 29), ReadDate(projection, "NearTermFinish"), "The near-term window must contain exactly thirty inclusive calendar dates.");
        TestAssert.True(nearTerm.Any(row => ReadText(row, "ReferenceCode") == ExecutiveProgressTestFixtures.OpenInProgressCardId), "Unfinished work planned before the source reporting date must remain visible as overdue work.");
        TestAssert.True(nearTerm.Any(row => ReadText(row, "ReferenceCode") == ExecutiveProgressTestFixtures.UnrecordedCardId), "Unrecorded work whose plan is overdue must remain visible rather than being silently treated as complete.");
        TestAssert.True(nearTerm.Any(row => ReadText(row, "ReferenceCode") == ExecutiveProgressTestFixtures.MissingEffortCardId), "Planned work intersecting the exact 30-day axis must remain visible even when effort is missing.");
        TestAssert.True(nearTerm.Any(row => ReadText(row, "ReferenceCode") == "NT-M1" && ReadText(row, "Kind") == "Milestone"), "A milestone dated inside the exact window must be retained as near-term context.");
        TestAssert.False(nearTerm.Any(row => ReadText(row, "ReferenceCode") == ExecutiveProgressTestFixtures.CompletedEarlyCardId), "Completed work outside the window must not be carried into the operating view.");

        var firstNonOverdue = Array.FindIndex(nearTerm.ToArray(), row => !ReadBoolean(row, "IsOverdue"));
        if (firstNonOverdue >= 0)
        {
            TestAssert.True(nearTerm.Take(firstNonOverdue).All(row => ReadBoolean(row, "IsOverdue")), "Every overdue item must precede non-overdue near-term work.");
        }
    }

    public static void DailyGanttRetainsAlertedDateIncompleteOverdueAndSupportsEmptyNearTerm()
    {
        var result = ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult();
        var dateIncompleteOverdue = result with
        {
            Project = result.Project with
            {
                DeliveryCards = result.Project.DeliveryCards
                    .Select(card => string.Equals(card.Id, ExecutiveProgressTestFixtures.MissingEffortCardId, StringComparison.OrdinalIgnoreCase)
                        ? card with { PlannedStart = null, PlannedFinish = null }
                        : card)
                    .ToArray()
            },
            Analysis = result.Analysis with
            {
                Alerts = result.Analysis.Alerts
                    .Concat(
                    [
                        new Alert
                        {
                            WorkItemId = ExecutiveProgressTestFixtures.MissingEffortCardId,
                            AlertCode = "OVERDUE",
                            Severity = WarningSeverity.Warning,
                            Message = "Official analysis identifies a date-incomplete overdue item.",
                            DerivedAt = result.Analysis.AsOfDate!.Value
                        }
                    ])
                    .ToArray()
            }
        };
        var dateIncompleteRows = Items(BuildProjection(dateIncompleteOverdue), "NearTermRows");
        var dateIncomplete = dateIncompleteRows.SingleOrDefault(row => string.Equals(ReadText(row, "ReferenceCode"), ExecutiveProgressTestFixtures.MissingEffortCardId, StringComparison.Ordinal));
        TestAssert.True(dateIncomplete is not null, "A date-incomplete delivery card with an attributable official overdue alert must remain visible in the near-term view.");
        TestAssert.Equal(null, ReadDate(dateIncomplete!, "PlannedStart"), "The near-term projection must retain the missing plan boundary instead of inventing a date.");
        TestAssert.True(ReadBoolean(dateIncomplete!, "IsOverdue"), "The date-incomplete card must retain its attributable overdue cue.");

        var emptyNearTerm = result with
        {
            Project = result.Project with
            {
                DeliveryCards = result.Project.DeliveryCards
                    .Select(card => card with { PlannedStart = new DateOnly(2026, 11, 1), PlannedFinish = new DateOnly(2026, 11, 2) })
                    .ToArray(),
                Milestones = result.Project.Milestones
                    .Select(milestone => milestone with { PlannedDate = new DateOnly(2026, 11, 3) })
                    .ToArray()
            },
            Analysis = result.Analysis with { Alerts = Array.Empty<Alert>() }
        };
        TestAssert.Equal(0, Items(BuildProjection(emptyNearTerm), "NearTermRows").Count, "The near-term collection must be empty when no overdue/open work or milestone intersects the exact window.");
    }

    private static object BuildProjection(CompilationResult result)
    {
        var projectorType = Type.GetType("ProjectManagementCompiler.Management.ExecutiveDailyGanttProjector, ProjectManagementCompiler", throwOnError: false);
        TestAssert.True(projectorType is not null, "ExecutiveDailyGanttProjector must exist to turn official planning and execution evidence into daily rows.");
        var projector = Activator.CreateInstance(projectorType!);
        TestAssert.True(projector is not null, "ExecutiveDailyGanttProjector must be constructible for direct projection tests.");
        var build = projectorType!.GetMethod("Build", BindingFlags.Public | BindingFlags.Instance, [typeof(CompilationResult)]);
        TestAssert.True(build is not null, "ExecutiveDailyGanttProjector must expose Build(CompilationResult).");
        return build!.Invoke(projector, [result]) ?? throw new InvalidOperationException("The daily-Gantt projector returned no projection.");
    }

    private static object FullRow(object projection, string referenceCode, string kind = "DeliveryCard") =>
        Items(projection, "FullRows").Single(row =>
            string.Equals(ReadText(row, "Kind"), kind, StringComparison.Ordinal)
            && string.Equals(ReadText(row, "ReferenceCode"), referenceCode, StringComparison.Ordinal));

    private static int IndexOf(IReadOnlyList<object> rows, string referenceCode, string kind) =>
        rows.Select((row, index) => new { row, index })
            .Single(item => string.Equals(ReadText(item.row, "ReferenceCode"), referenceCode, StringComparison.Ordinal)
                && string.Equals(ReadText(item.row, "Kind"), kind, StringComparison.Ordinal))
            .index;

    private static IReadOnlyList<object> Items(object value, string propertyName) =>
        ((IEnumerable)(ReadRequired(value, propertyName) ?? Array.Empty<object>())).Cast<object>().ToArray();

    private static DateOnly? ReadDate(object value, string propertyName) =>
        ReadRequired(value, propertyName) is DateOnly date ? date : null;

    private static int ReadInt(object value, string propertyName)
    {
        var raw = ReadRequired(value, propertyName);
        TestAssert.True(raw is not null, $"'{propertyName}' must be present when this daily-Gantt assertion requires a numeric value.");
        return Convert.ToInt32(raw);
    }

    private static bool ReadBoolean(object value, string propertyName)
    {
        var raw = ReadRequired(value, propertyName);
        TestAssert.True(raw is not null, $"'{propertyName}' must be present when this daily-Gantt assertion requires a Boolean value.");
        return Convert.ToBoolean(raw);
    }

    private static string ReadText(object value, string propertyName) =>
        ReadRequired(value, propertyName)?.ToString() ?? string.Empty;

    private static object? ReadRequired(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        TestAssert.True(property is not null, $"Daily-Gantt projection objects must expose '{propertyName}'.");
        return property!.GetValue(value);
    }
}
