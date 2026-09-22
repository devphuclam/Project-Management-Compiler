using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

/// <summary>
/// Builds the management-only daily Gantt view from immutable planning and
/// official source-execution evidence. This projector never writes to the
/// canonical graph and never reads local proposal or overlay values when a
/// source-execution snapshot is available.
/// </summary>
public sealed class ExecutiveDailyGanttProjector
{
    public ExecutiveDailyGanttProjection Build(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var project = result.Project;
        var sourceReportingDate = project.ImportMetadata?.RegisterStatusDate;
        var analysisAsOfDate = result.Analysis.AsOfDate;
        if (sourceReportingDate is null || analysisAsOfDate is null)
        {
            throw IncompleteOfficial("A source reporting date and an analysis as-of date are required for the daily Gantt.");
        }

        var phaseContexts = project.Phases
            .Select((phase, index) => new PhaseContext(phase, index, ReaderFacingTextPolicy.CleanName(phase.Name, phase.Id)))
            .ToArray();
        var phasesById = phaseContexts.ToDictionary(context => context.Phase.Id, StringComparer.OrdinalIgnoreCase);
        var workPackageContexts = project.WorkPackages
            .Select((workPackage, index) => new WorkPackageContext(
                workPackage,
                index,
                ReaderFacingTextPolicy.CleanName(workPackage.Name, workPackage.Id),
                phasesById.TryGetValue(workPackage.PhaseId, out var phase) ? phase : null))
            .ToArray();
        var workPackagesById = workPackageContexts.ToDictionary(context => context.WorkPackage.Id, StringComparer.OrdinalIgnoreCase);
        var overdueIds = result.Analysis.Alerts
            .Where(alert => string.Equals(alert.AlertCode, "OVERDUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(alert.AlertCode, "START_DELAY", StringComparison.OrdinalIgnoreCase))
            .Select(alert => alert.WorkItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blockedIds = result.Analysis.Alerts
            .Where(alert => string.Equals(alert.AlertCode, "BLOCKED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(alert.AlertCode, "SUSPENDED", StringComparison.OrdinalIgnoreCase))
            .Select(alert => alert.WorkItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cardContexts = project.DeliveryCards
            .Select((card, index) => BuildCard(
                project,
                card,
                index,
                sourceReportingDate.Value,
                analysisAsOfDate.Value,
                phasesById,
                workPackagesById,
                overdueIds.Contains(card.Id),
                blockedIds.Contains(card.Id)))
            .ToArray();
        var milestones = project.Milestones
            .Select((milestone, index) => new MilestoneContext(milestone, index))
            .ToArray();
        var nextMilestone = milestones
            .Where(context => context.Milestone.Kind == MilestoneKind.Milestone
                && context.Milestone.PlannedDate is not null
                && context.Milestone.PlannedDate >= sourceReportingDate)
            .OrderBy(context => context.Milestone.PlannedDate)
            .ThenBy(context => context.Index)
            .FirstOrDefault();

        var fullRows = new List<ExecutiveDailyGanttRow>();
        var sourceOrder = 0;
        void Add(ExecutiveDailyGanttRow row) => fullRows.Add(row with { SourceOrder = sourceOrder++ });

        var projectStart = project.Baseline.PlanningStart ?? MinimumPlannedStart(project, cardContexts);
        var projectFinish = project.Baseline.PlanningFinish ?? MaximumPlannedFinish(project, cardContexts);
        Add(BuildRollup(
            ExecutiveDailyGanttRowKind.Project,
            project.Project.Id,
            ReaderFacingTextPolicy.CleanName(project.Project.Name, project.Project.Id),
            hierarchyLevel: 0,
            phaseDisplayName: null,
            workPackageDisplayName: null,
            projectStart,
            projectFinish,
            cardContexts,
            sourceReportingDate.Value,
            analysisAsOfDate.Value,
            isCurrent: IsWithin(sourceReportingDate.Value, projectStart, projectFinish)));

        var emittedCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emittedWorkPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emittedMilestones = new HashSet<int>();
        foreach (var phase in phaseContexts)
        {
            var phaseCards = cardContexts
                .Where(context => string.Equals(context.Card.PhaseId, phase.Phase.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Add(BuildRollup(
                ExecutiveDailyGanttRowKind.Phase,
                phase.Phase.Id,
                phase.DisplayName,
                hierarchyLevel: 1,
                phaseDisplayName: phase.DisplayName,
                workPackageDisplayName: null,
                phase.Phase.PlannedStart,
                phase.Phase.PlannedFinish,
                phaseCards,
                sourceReportingDate.Value,
                analysisAsOfDate.Value,
                isCurrent: IsWithin(sourceReportingDate.Value, phase.Phase.PlannedStart, phase.Phase.PlannedFinish)));

            foreach (var workPackage in workPackageContexts.Where(context => string.Equals(context.WorkPackage.PhaseId, phase.Phase.Id, StringComparison.OrdinalIgnoreCase)))
            {
                emittedWorkPackages.Add(workPackage.WorkPackage.Id);
                var children = cardContexts
                    .Where(context => string.Equals(context.Card.WorkPackageId, workPackage.WorkPackage.Id, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                Add(BuildRollup(
                    ExecutiveDailyGanttRowKind.WorkPackage,
                    workPackage.WorkPackage.Id,
                    workPackage.DisplayName,
                    hierarchyLevel: 2,
                    phaseDisplayName: phase.DisplayName,
                    workPackageDisplayName: workPackage.DisplayName,
                    workPackage.WorkPackage.PlannedStart,
                    workPackage.WorkPackage.PlannedFinish,
                    children,
                    sourceReportingDate.Value,
                    analysisAsOfDate.Value,
                    isCurrent: IsWithin(sourceReportingDate.Value, workPackage.WorkPackage.PlannedStart, workPackage.WorkPackage.PlannedFinish)));
                foreach (var child in children)
                {
                    emittedCards.Add(child.Card.Id);
                    Add(child.Row);
                }

                foreach (var milestone in MilestonesForParent(milestones, workPackage.WorkPackage.Id, emittedMilestones))
                {
                    emittedMilestones.Add(milestone.Index);
                    Add(BuildMilestoneRow(milestone, phase.DisplayName, workPackage.DisplayName, hierarchyLevel: 3, nextMilestone));
                }
            }

            foreach (var orphan in phaseCards.Where(context => !emittedCards.Contains(context.Card.Id)))
            {
                emittedCards.Add(orphan.Card.Id);
                Add(orphan.Row with { WorkPackageDisplayName = "Chưa xác định gói công việc" });
            }

            foreach (var milestone in MilestonesForParent(milestones, phase.Phase.Id, emittedMilestones))
            {
                emittedMilestones.Add(milestone.Index);
                Add(BuildMilestoneRow(milestone, phase.DisplayName, null, hierarchyLevel: 2, nextMilestone));
            }
        }

        foreach (var workPackage in workPackageContexts.Where(context => !emittedWorkPackages.Contains(context.WorkPackage.Id)))
        {
            emittedWorkPackages.Add(workPackage.WorkPackage.Id);
            var children = cardContexts
                .Where(context => string.Equals(context.Card.WorkPackageId, workPackage.WorkPackage.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Add(BuildRollup(
                ExecutiveDailyGanttRowKind.WorkPackage,
                workPackage.WorkPackage.Id,
                workPackage.DisplayName,
                hierarchyLevel: 2,
                phaseDisplayName: "Chưa xác định giai đoạn",
                workPackageDisplayName: workPackage.DisplayName,
                workPackage.WorkPackage.PlannedStart,
                workPackage.WorkPackage.PlannedFinish,
                children,
                sourceReportingDate.Value,
                analysisAsOfDate.Value,
                isCurrent: IsWithin(sourceReportingDate.Value, workPackage.WorkPackage.PlannedStart, workPackage.WorkPackage.PlannedFinish)));
            foreach (var child in children.Where(context => emittedCards.Add(context.Card.Id)))
            {
                Add(child.Row with { PhaseDisplayName = "Chưa xác định giai đoạn" });
            }
        }

        foreach (var orphan in cardContexts.Where(context => emittedCards.Add(context.Card.Id)))
        {
            Add(orphan.Row with
            {
                PhaseDisplayName = "Chưa xác định giai đoạn",
                WorkPackageDisplayName = "Chưa xác định gói công việc"
            });
        }

        foreach (var milestone in milestones.Where(context => !emittedMilestones.Contains(context.Index)).OrderBy(context => context.Milestone.PlannedDate).ThenBy(context => context.Index))
        {
            emittedMilestones.Add(milestone.Index);
            Add(BuildMilestoneRow(milestone, "Chưa xác định giai đoạn", null, hierarchyLevel: 1, nextMilestone));
        }

        var planningAxisDates = fullRows
            .SelectMany(row => new[]
            {
                row.PlannedStart,
                row.PlannedFinish
            })
            .Concat(new DateOnly?[]
            {
                project.Baseline.PlanningStart,
                project.Baseline.PlanningFinish
            })
            .Where(date => date is not null)
            .Select(date => date!.Value)
            .ToArray();
        if (planningAxisDates.Length == 0)
        {
            throw IncompleteOfficial("No attributable baseline planning date exists for the daily Gantt axis.");
        }

        var axisDates = planningAxisDates
            .Cast<DateOnly?>()
            .Concat(fullRows.SelectMany(row => new[]
            {
                row.ActualStart,
                row.ActualFinish,
                row.ActualDisplayThrough,
                row.ForecastFinish
            }))
            .Concat(new DateOnly?[]
            {
                sourceReportingDate,
                analysisAsOfDate
            })
            .Where(date => date is not null)
            .Select(date => date!.Value)
            .ToArray();
        if (axisDates.Length == 0)
        {
            throw IncompleteOfficial("No attributable planning, Actual, forecast, or reporting date exists for the daily Gantt axis.");
        }

        var nearTermStart = sourceReportingDate.Value;
        var nearTermFinish = sourceReportingDate.Value.AddDays(29);
        var nearTermRows = SelectNearTermRows(fullRows, nearTermStart, nearTermFinish);

        return new ExecutiveDailyGanttProjection
        {
            FullStart = axisDates.Min(),
            FullFinish = axisDates.Max(),
            NearTermStart = nearTermStart,
            NearTermFinish = nearTermFinish,
            FullRows = fullRows,
            OverviewRows = fullRows
                .Where(row => row.Kind is ExecutiveDailyGanttRowKind.Project or ExecutiveDailyGanttRowKind.Phase or ExecutiveDailyGanttRowKind.Milestone)
                .ToArray(),
            NearTermRows = nearTermRows
        };
    }

    private static IReadOnlyList<ExecutiveDailyGanttRow> SelectNearTermRows(
        IReadOnlyList<ExecutiveDailyGanttRow> fullRows,
        DateOnly nearTermStart,
        DateOnly nearTermFinish)
    {
        var qualifying = fullRows
            .Where(row => row.Kind is ExecutiveDailyGanttRowKind.DeliveryCard or ExecutiveDailyGanttRowKind.Milestone)
            .Where(row => QualifiesForNearTerm(row, nearTermStart, nearTermFinish))
            .ToArray();

        return qualifying
            .OrderBy(row => row.Kind == ExecutiveDailyGanttRowKind.DeliveryCard && row.IsOverdue ? 0 : 1)
            .ThenBy(row => row.PlannedFinish ?? DateOnly.MaxValue)
            .ThenBy(row => row.SourceOrder)
            .ToArray();
    }

    private static bool QualifiesForNearTerm(
        ExecutiveDailyGanttRow row,
        DateOnly nearTermStart,
        DateOnly nearTermFinish)
    {
        if (row.Kind == ExecutiveDailyGanttRowKind.Milestone)
        {
            return row.PlannedStart is not null
                && row.PlannedStart >= nearTermStart
                && row.PlannedStart <= nearTermFinish;
        }

        var missingPlannedBoundary = row.PlannedStart is null || row.PlannedFinish is null;
        var unfinishedOverdue = row.StateLabel is not "Hoàn thành" and not "Đã hủy"
            && ((row.PlannedFinish is not null && row.PlannedFinish < nearTermStart)
                || (missingPlannedBoundary && row.IsOverdue));
        var intersectsWindow = row.PlannedStart is not null
            && row.PlannedFinish is not null
            && row.PlannedStart <= nearTermFinish
            && row.PlannedFinish >= nearTermStart;
        return unfinishedOverdue || intersectsWindow;
    }

    private static CardContext BuildCard(
        CanonicalProject project,
        DeliveryCard card,
        int sourceOrder,
        DateOnly sourceReportingDate,
        DateOnly analysisAsOfDate,
        IReadOnlyDictionary<string, PhaseContext> phasesById,
        IReadOnlyDictionary<string, WorkPackageContext> workPackagesById,
        bool isOverdue,
        bool hasBlockedAlert)
    {
        var record = ExecutionTruthResolver.ForCard(project, card.Id);
        var isRecorded = record?.IsRecorded == true;
        var state = isRecorded ? record!.ExecutionState : null;
        var actualStart = isRecorded ? record!.ActualStart : null;
        var actualFinish = isRecorded ? record!.ActualFinish : null;
        var actualDisplayThrough = DirectActualDisplayThrough(state, actualStart, actualFinish, analysisAsOfDate);
        var actualPresentationKind = ClassifyActual(record, state, actualStart, actualFinish, actualDisplayThrough);
        var forecastFinish = isRecorded ? DateOnlyFromOffset(record!.ForecastFinish) : null;
        var progressEligible = isRecorded && IsProgressEligible(record!);
        int? progressPercent = progressEligible
            ? CalculatePercent(record!.ActualEffortHours!.Value, record.RemainingEffortHours!.Value)
            : null;
        var phaseName = phasesById.TryGetValue(card.PhaseId, out var phase)
            ? phase.DisplayName
            : "Chưa xác định giai đoạn";
        var workPackageName = workPackagesById.TryGetValue(card.WorkPackageId, out var workPackage)
            ? workPackage.DisplayName
            : "Chưa xác định gói công việc";
        var isBlocked = isRecorded && (hasBlockedAlert
            || state == ExecutionState.Suspended
            || record!.ResultState == SourceResultState.Blocked
            || !string.IsNullOrWhiteSpace(record.Blocker));
        var derivedOverdue = state is not ExecutionState.Completed and not ExecutionState.Cancelled
            && card.PlannedFinish is not null
            && card.PlannedFinish < sourceReportingDate;
        var row = new ExecutiveDailyGanttRow
        {
            Kind = ExecutiveDailyGanttRowKind.DeliveryCard,
            ReferenceCode = card.Id,
            DisplayName = ReaderFacingTextPolicy.CleanName(card.Name, card.Id),
            HierarchyLevel = 3,
            PhaseDisplayName = phaseName,
            WorkPackageDisplayName = workPackageName,
            PlannedStart = card.PlannedStart,
            PlannedFinish = card.PlannedFinish,
            ActualStart = actualStart,
            ActualFinish = actualFinish,
            ActualDisplayThrough = actualDisplayThrough,
            ActualPresentationKind = actualPresentationKind,
            ActualEvidenceLabel = isRecorded ? "Có ghi nhận" : ReaderFacingTextPolicy.MissingEvidenceLabel,
            ActualEffortHours = isRecorded ? record!.ActualEffortHours : null,
            RemainingEffortHours = isRecorded ? record!.RemainingEffortHours : null,
            ForecastFinish = forecastFinish,
            ProgressPercent = progressPercent,
            ProgressLabel = ProgressLabel(progressPercent),
            RecordedChildCount = isRecorded ? 1 : 0,
            ProgressEligibleChildCount = progressEligible ? 1 : 0,
            TotalChildCount = 1,
            CoverageLabel = CoverageLabel(isRecorded ? 1 : 0, 1),
            StateLabel = isRecorded ? ExecutionLabel(state) : "Chưa cập nhật",
            OwnerLabel = ResolveDeliveryCardOwner(project, card.Id),
            LastOfficialUpdate = isRecorded ? record!.LastUpdatedAt : null,
            IsBlocked = isBlocked,
            IsOverdue = isOverdue || derivedOverdue,
            IsCurrent = IsWithin(sourceReportingDate, card.PlannedStart, card.PlannedFinish),
            IsNextMilestone = false,
            SourceOrder = sourceOrder
        };
        return new CardContext(card, record, state, row);
    }

    private static ExecutiveDailyGanttRow BuildRollup(
        ExecutiveDailyGanttRowKind kind,
        string referenceCode,
        string displayName,
        int hierarchyLevel,
        string? phaseDisplayName,
        string? workPackageDisplayName,
        DateOnly? plannedStart,
        DateOnly? plannedFinish,
        IReadOnlyList<CardContext> children,
        DateOnly sourceReportingDate,
        DateOnly analysisAsOfDate,
        bool isCurrent)
    {
        var total = children.Count;
        var recorded = children.Count(child => child.IsRecorded);
        var eligible = children.Count(child => child.ProgressEligible);
        var percentage = total > 0 && eligible == total
            ? AggregatePercent(children)
            : null;
        var activityStart = children
            .Select(child => child.Row.ActualStart)
            .Where(date => date is not null)
            .Select(date => date!.Value)
            .DefaultIfEmpty()
            .Min();
        var hasActivityStart = children.Any(child => child.Row.ActualStart is not null);
        var hasOpenActual = children.Any(child => child.State == ExecutionState.InProgress && child.Row.ActualDisplayThrough is not null);
        var completedFinish = children
            .Where(child => child.State == ExecutionState.Completed
                && child.Row.ActualStart is not null
                && child.Row.ActualFinish is not null
                && child.Row.ActualFinish >= child.Row.ActualStart)
            .Select(child => child.Row.ActualFinish!.Value)
            .DefaultIfEmpty()
            .Max();
        var hasCompletedFinish = children.Any(child => child.State == ExecutionState.Completed
            && child.Row.ActualStart is not null
            && child.Row.ActualFinish is not null
            && child.Row.ActualFinish >= child.Row.ActualStart);
        var hasFinishOnly = children.Any(child => child.Row.ActualPresentationKind == ExecutiveActualPresentationKind.CompletionPoint);
        var hasEffortOnly = children.Any(child => child.Row.ActualPresentationKind == ExecutiveActualPresentationKind.EffortOnly);
        var actualPresentationKind = ClassifyRollupActual(
            children,
            hasActivityStart,
            hasOpenActual,
            hasCompletedFinish,
            hasFinishOnly,
            hasEffortOnly);
        var incomplete = children.Where(child => !child.IsRecorded || child.State != ExecutionState.Completed).ToArray();
        DateOnly? forecastFinish = incomplete.Length > 0 && incomplete.All(child => child.Row.ForecastFinish is not null)
            ? incomplete.Max(child => child.Row.ForecastFinish!.Value)
            : null;

        return new ExecutiveDailyGanttRow
        {
            Kind = kind,
            ReferenceCode = referenceCode,
            DisplayName = displayName,
            HierarchyLevel = hierarchyLevel,
            PhaseDisplayName = phaseDisplayName,
            WorkPackageDisplayName = workPackageDisplayName,
            PlannedStart = plannedStart,
            PlannedFinish = plannedFinish,
            ActualStart = hasActivityStart ? activityStart : null,
            ActualFinish = null,
            ActualDisplayThrough = hasOpenActual ? analysisAsOfDate : hasCompletedFinish ? completedFinish : null,
            ActualPresentationKind = actualPresentationKind,
            ActualEvidenceLabel = recorded > 0 ? "Có ghi nhận" : ReaderFacingTextPolicy.MissingEvidenceLabel,
            ActualEffortHours = SumKnown(children.Select(child => child.Row.ActualEffortHours)),
            RemainingEffortHours = SumKnown(children.Select(child => child.Row.RemainingEffortHours)),
            ForecastFinish = forecastFinish,
            ProgressPercent = percentage,
            ProgressLabel = ProgressLabel(percentage),
            RecordedChildCount = recorded,
            ProgressEligibleChildCount = eligible,
            TotalChildCount = total,
            CoverageLabel = CoverageLabel(recorded, total),
            StateLabel = RollupStateLabel(children),
            OwnerLabel = ResolveScopeOwner(children),
            LastOfficialUpdate = children
                .Where(child => child.IsRecorded && child.Row.LastOfficialUpdate is not null)
                .Select(child => child.Row.LastOfficialUpdate!.Value)
                .OrderByDescending(value => value)
                .Cast<DateTimeOffset?>()
                .FirstOrDefault(),
            IsBlocked = children.Any(child => child.Row.IsBlocked),
            IsOverdue = children.Any(child => child.Row.IsOverdue),
            IsCurrent = isCurrent,
            IsNextMilestone = false
        };
    }

    private static ExecutiveDailyGanttRow BuildMilestoneRow(
        MilestoneContext context,
        string? phaseDisplayName,
        string? workPackageDisplayName,
        int hierarchyLevel,
        MilestoneContext? nextMilestone) =>
        new()
        {
            Kind = ExecutiveDailyGanttRowKind.Milestone,
            ReferenceCode = context.Milestone.Id,
            DisplayName = ReaderFacingTextPolicy.CleanName(context.Milestone.Name, context.Milestone.Id),
            HierarchyLevel = hierarchyLevel,
            PhaseDisplayName = phaseDisplayName,
            WorkPackageDisplayName = workPackageDisplayName,
            PlannedStart = context.Milestone.PlannedDate,
            PlannedFinish = context.Milestone.PlannedDate,
            ProgressLabel = ReaderFacingTextPolicy.MissingEvidenceLabel,
            CoverageLabel = CoverageLabel(0, 0),
            StateLabel = context.Milestone.State is null ? "Chưa cập nhật" : ExecutionLabel(context.Milestone.State),
            OwnerLabel = ReaderFacingTextPolicy.MissingOwnerLabel,
            IsNextMilestone = nextMilestone is not null && nextMilestone.Index == context.Index
        };

    private static IEnumerable<MilestoneContext> MilestonesForParent(
        IEnumerable<MilestoneContext> milestones,
        string parentId,
        ISet<int> emitted) =>
        milestones
            .Where(context => !emitted.Contains(context.Index)
                && string.Equals(context.Milestone.ParentId, parentId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(context => context.Milestone.PlannedDate)
            .ThenBy(context => context.Index);

    private static int? AggregatePercent(IEnumerable<CardContext> children)
    {
        var actual = children.Sum(child => child.Record!.ActualEffortHours!.Value);
        var remaining = children.Sum(child => child.Record!.RemainingEffortHours!.Value);
        return actual + remaining > 0m ? CalculatePercent(actual, remaining) : null;
    }

    private static string RollupStateLabel(IReadOnlyList<CardContext> children)
    {
        if (children.Count == 0)
        {
            return "Chưa cập nhật";
        }

        if (children.Any(child => child.IsRecorded && child.State == ExecutionState.InProgress))
        {
            return "Đang thực hiện";
        }

        foreach (var candidate in new[]
        {
            ExecutionState.Completed,
            ExecutionState.NotStarted,
            ExecutionState.Suspended,
            ExecutionState.Cancelled
        })
        {
            if (children.All(child => child.IsRecorded && child.State == candidate))
            {
                return ExecutionLabel(candidate);
            }
        }

        return "Chưa cập nhật";
    }

    private static string ResolveScopeOwner(IReadOnlyList<CardContext> children)
    {
        var owners = children
            .Select(child => child.Row.OwnerLabel)
            .Where(owner => owner != ReaderFacingTextPolicy.MissingOwnerLabel)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return children.Count > 0 && owners.Length == 1 && children.All(child => child.Row.OwnerLabel == owners[0])
            ? owners[0]
            : ReaderFacingTextPolicy.MissingOwnerLabel;
    }

    private static string ResolveDeliveryCardOwner(CanonicalProject project, string cardId)
    {
        var assignments = project.Assignments
            .Where(assignment => string.Equals(assignment.WorkItemId, cardId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(assignment.CarioRoleCode, "A", StringComparison.OrdinalIgnoreCase))
            .Select(assignment => (Identity: assignment.ConcreteIdentity?.Trim(), Role: assignment.LogicalRoleCode?.Trim()))
            .Distinct()
            .ToArray();
        if (assignments.Length != 1)
        {
            return ReaderFacingTextPolicy.MissingOwnerLabel;
        }

        return !string.IsNullOrWhiteSpace(assignments[0].Identity)
            ? assignments[0].Identity!
            : ReaderFacingTextPolicy.OwnerOrMissing(RoleLabel(assignments[0].Role));
    }

    private static string? RoleLabel(string? role) => role?.Trim().ToUpperInvariant() switch
    {
        "PM" => "Quản lý dự án",
        "LEAD" => "Đầu mối dự án",
        "DEV" or "DEV2" => "Nhóm phát triển",
        "QA" => "Đảm bảo chất lượng",
        "PDA" or "PRODUCT_DECISION_AUTHORITY" => "Thẩm quyền quyết định sản phẩm",
        "PROC" => "Phụ trách quy trình",
        "QLHT" => "Quản lý hệ thống",
        "HTKT" => "Hỗ trợ kỹ thuật",
        "SPEC" => "Chuyên gia chuyên môn",
        "PILOT" => "Đầu mối thử nghiệm",
        _ => null
    };

    private static bool IsProgressEligible(EffectiveExecutionRecord record) =>
        record.ActualEffortHours is not null
        && record.RemainingEffortHours is not null
        && record.ActualEffortHours >= 0m
        && record.RemainingEffortHours >= 0m
        && record.ActualEffortHours + record.RemainingEffortHours > 0m;

    private static ExecutiveActualPresentationKind ClassifyActual(
        EffectiveExecutionRecord? record,
        ExecutionState? state,
        DateOnly? actualStart,
        DateOnly? actualFinish,
        DateOnly? actualDisplayThrough)
    {
        if (record?.IsRecorded != true)
        {
            return ExecutiveActualPresentationKind.None;
        }

        if (actualStart is not null && actualFinish is not null && actualFinish >= actualStart)
        {
            return ExecutiveActualPresentationKind.RecordedInterval;
        }

        if (state == ExecutionState.InProgress
            && actualStart is not null
            && actualFinish is null
            && actualDisplayThrough is not null)
        {
            return ExecutiveActualPresentationKind.OpenRecordedInterval;
        }

        if (actualStart is null && actualFinish is not null)
        {
            return ExecutiveActualPresentationKind.CompletionPoint;
        }

        if (actualStart is null
            && actualFinish is null
            && (record.ActualEffortHours is not null || record.RemainingEffortHours is not null))
        {
            return ExecutiveActualPresentationKind.EffortOnly;
        }

        return ExecutiveActualPresentationKind.None;
    }

    private static ExecutiveActualPresentationKind ClassifyRollupActual(
        IReadOnlyList<CardContext> children,
        bool hasActivityStart,
        bool hasOpenActual,
        bool hasCompletedFinish,
        bool hasFinishOnly,
        bool hasEffortOnly)
    {
        if (children.Count == 0 || children.All(child => !child.IsRecorded))
        {
            return ExecutiveActualPresentationKind.None;
        }

        if (children.All(child => child.Row.ActualPresentationKind == ExecutiveActualPresentationKind.RecordedInterval))
        {
            return ExecutiveActualPresentationKind.RecordedInterval;
        }

        if (hasOpenActual && hasActivityStart)
        {
            return ExecutiveActualPresentationKind.OpenRecordedInterval;
        }

        if (hasFinishOnly && !hasActivityStart && children.All(child => child.Row.ActualPresentationKind == ExecutiveActualPresentationKind.CompletionPoint))
        {
            return ExecutiveActualPresentationKind.CompletionPoint;
        }

        if (hasEffortOnly && !hasActivityStart && !hasCompletedFinish && !hasFinishOnly)
        {
            return ExecutiveActualPresentationKind.EffortOnly;
        }

        return ExecutiveActualPresentationKind.None;
    }

    private static decimal? SumKnown(IEnumerable<decimal?> values)
    {
        var known = values.Where(value => value is not null).Select(value => value!.Value).ToArray();
        return known.Length == 0 ? null : known.Sum();
    }

    private static int CalculatePercent(decimal actual, decimal remaining) =>
        decimal.ToInt32(decimal.Round(actual / (actual + remaining) * 100m, 0, MidpointRounding.AwayFromZero));

    private static DateOnly? DirectActualDisplayThrough(
        ExecutionState? state,
        DateOnly? actualStart,
        DateOnly? actualFinish,
        DateOnly analysisAsOfDate)
    {
        if (actualStart is null)
        {
            return null;
        }

        return state switch
        {
            ExecutionState.Completed when actualFinish is not null && actualFinish >= actualStart => actualFinish,
            ExecutionState.InProgress when actualStart <= analysisAsOfDate => analysisAsOfDate,
            ExecutionState.Suspended or ExecutionState.Cancelled when actualFinish is not null && actualFinish >= actualStart => actualFinish,
            ExecutionState.Suspended or ExecutionState.Cancelled => actualStart,
            _ => null
        };
    }

    private static DateOnly? DateOnlyFromOffset(DateTimeOffset? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value.Date);

    private static string ExecutionLabel(ExecutionState? state) => state switch
    {
        ExecutionState.NotStarted => "Chưa bắt đầu",
        ExecutionState.InProgress => "Đang thực hiện",
        ExecutionState.Completed => "Hoàn thành",
        ExecutionState.Suspended => "Tạm dừng",
        ExecutionState.Cancelled => "Đã hủy",
        _ => "Chưa cập nhật"
    };

    private static string ProgressLabel(int? percent) =>
        percent is null ? ReaderFacingTextPolicy.MissingEvidenceLabel : $"{percent}%";

    private static string CoverageLabel(int recorded, int total) => $"Độ phủ {recorded}/{total}";

    private static bool IsWithin(DateOnly date, DateOnly? start, DateOnly? finish) =>
        start is not null && finish is not null && start <= finish && date >= start && date <= finish;

    private static DateOnly? MinimumPlannedStart(CanonicalProject project, IEnumerable<CardContext> cards)
    {
        var values = project.Phases.Select(phase => phase.PlannedStart)
            .Concat(project.WorkPackages.Select(workPackage => workPackage.PlannedStart))
            .Concat(cards.Select(card => card.Row.PlannedStart))
            .Where(date => date is not null)
            .Select(date => date!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Min();
    }

    private static DateOnly? MaximumPlannedFinish(CanonicalProject project, IEnumerable<CardContext> cards)
    {
        var values = project.Phases.Select(phase => phase.PlannedFinish)
            .Concat(project.WorkPackages.Select(workPackage => workPackage.PlannedFinish))
            .Concat(cards.Select(card => card.Row.PlannedFinish))
            .Where(date => date is not null)
            .Select(date => date!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Max();
    }

    private static ProjectCompilationException IncompleteOfficial(string message) =>
        new(
            "executive-export",
            [new ImportWarning
            {
                Id = "EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL",
                Code = "EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL",
                Severity = WarningSeverity.Error,
                Message = message
            }]);

    private sealed record PhaseContext(Phase Phase, int Index, string DisplayName);

    private sealed record WorkPackageContext(
        WorkPackage WorkPackage,
        int Index,
        string DisplayName,
        PhaseContext? Phase);

    private sealed record MilestoneContext(MilestoneDecision Milestone, int Index);

    private sealed record CardContext(
        DeliveryCard Card,
        EffectiveExecutionRecord? Record,
        ExecutionState? State,
        ExecutiveDailyGanttRow Row)
    {
        public bool IsRecorded => Record?.IsRecorded == true;
        public bool ProgressEligible => Row.ProgressEligibleChildCount == 1;
    }
}
