using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed class StatusAnalyzer
{
    public ManagementAnalysis Analyze(CanonicalProject project, DateOnly asOfDate)
    {
        var calendar = new WorkingCalendar(project.Capacity.Calendar);
        var records = project.ExecutionOverlay.Records
            .ToDictionary(record => record.WorkItemId, StringComparer.OrdinalIgnoreCase);
        var cards = project.DeliveryCards.ToDictionary(card => card.Id, StringComparer.OrdinalIgnoreCase);
        var alerts = new List<Alert>();
        var variances = new List<WorkItemVariance>();
        var completed = 0;
        var inProgress = 0;
        var notStarted = 0;
        var suspended = 0;
        var cancelled = 0;
        var lateToStart = 0;
        var overdue = 0;
        var completedLate = 0;
        var dependencyDelayedPredecessors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in project.DeliveryCards.OrderBy(card => card.Id, StringComparer.Ordinal))
        {
            records.TryGetValue(card.Id, out var record);
            var state = record?.ExecutionState ?? card.State;
            var actualStart = record?.ActualStart;
            var actualFinish = record?.ActualFinish;
            var baselineStart = ValidDate(card.PlannedStart) ? card.PlannedStart : null;
            var baselineFinish = ValidDate(card.PlannedFinish) ? card.PlannedFinish : null;
            var startVariance = calendar.SignedWorkingMinutes(baselineStart, actualStart);
            var finishVariance = calendar.SignedWorkingMinutes(baselineFinish, actualFinish);
            variances.Add(new WorkItemVariance
            {
                WorkItemId = card.Id,
                ExecutionState = state,
                BaselineStart = baselineStart,
                BaselineFinish = baselineFinish,
                ActualStart = actualStart,
                ActualFinish = actualFinish,
                StartVarianceWorkingMinutes = startVariance,
                FinishVarianceWorkingMinutes = finishVariance,
                State = actualStart is null && actualFinish is null ? DataState.Unknown : DataState.Known
            });

            switch (state)
            {
                case ExecutionState.Completed:
                    completed++;
                    break;
                case ExecutionState.InProgress:
                    inProgress++;
                    break;
                case ExecutionState.NotStarted:
                    notStarted++;
                    break;
                case ExecutionState.Suspended:
                    suspended++;
                    break;
                case ExecutionState.Cancelled:
                    cancelled++;
                    break;
            }

            if (baselineStart is not null
                && ((state == ExecutionState.NotStarted && asOfDate > baselineStart)
                    || (actualStart is not null && actualStart > baselineStart))
                && state != ExecutionState.Cancelled)
            {
                lateToStart++;
                alerts.Add(new Alert
                {
                    WorkItemId = card.Id,
                    AlertCode = "START_DELAY",
                    Severity = WarningSeverity.Warning,
                    Message = $"Work item '{card.Id}' started late or remains unstarted after its planned start.",
                    VarianceWorkingMinutes = actualStart is null
                        ? calendar.SignedWorkingMinutes(baselineStart, asOfDate)
                        : startVariance,
                    DerivedAt = asOfDate
                });
            }

            if (record is not null
                && ((actualStart is not null && baselineStart is not null && actualStart > baselineStart)
                    || (state == ExecutionState.InProgress
                        && actualStart is not null
                        && actualFinish is null
                        && baselineFinish is not null
                        && asOfDate > baselineFinish)
                    || (state == ExecutionState.Completed
                        && actualFinish is not null
                        && baselineFinish is not null
                        && actualFinish > baselineFinish)))
            {
                dependencyDelayedPredecessors.Add(card.Id);
            }

            if (state == ExecutionState.InProgress
                && actualStart is not null
                && actualFinish is null
                && baselineFinish is not null
                && asOfDate > baselineFinish)
            {
                overdue++;
                alerts.Add(new Alert
                {
                    WorkItemId = card.Id,
                    AlertCode = "OVERDUE",
                    Severity = WarningSeverity.Warning,
                    Message = $"Work item '{card.Id}' is in progress beyond its planned finish.",
                    VarianceWorkingMinutes = calendar.SignedWorkingMinutes(baselineFinish, asOfDate),
                    DerivedAt = asOfDate
                });
            }

            if (state == ExecutionState.Completed && actualFinish is not null && baselineFinish is not null)
            {
                var code = actualFinish > baselineFinish ? "COMPLETED_LATE" : "COMPLETED_ON_TIME";
                if (code == "COMPLETED_LATE")
                {
                    completedLate++;
                }

                alerts.Add(new Alert
                {
                    WorkItemId = card.Id,
                    AlertCode = code,
                    Severity = code == "COMPLETED_LATE" ? WarningSeverity.Warning : WarningSeverity.Info,
                    Message = code == "COMPLETED_LATE"
                        ? $"Work item '{card.Id}' completed after its planned finish."
                        : $"Work item '{card.Id}' completed on or before its planned finish.",
                    VarianceWorkingMinutes = finishVariance,
                    DerivedAt = asOfDate
                });
            }

            if (state == ExecutionState.Suspended)
            {
                alerts.Add(new Alert
                {
                    WorkItemId = card.Id,
                    AlertCode = "SUSPENDED",
                    Severity = WarningSeverity.Warning,
                    Message = $"Work item '{card.Id}' is suspended.",
                    DerivedAt = asOfDate
                });
            }

            if (state == ExecutionState.Cancelled)
            {
                alerts.Add(new Alert
                {
                    WorkItemId = card.Id,
                    AlertCode = "CANCELLED",
                    Severity = WarningSeverity.Info,
                    Message = $"Work item '{card.Id}' is cancelled.",
                    DerivedAt = asOfDate
                });
            }
        }

        var atRisk = 0;
        var delayedPredecessorsBySuccessor = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in project.Dependencies
                     .Where(dependency => dependency.AnalysisEligible
                         && dependency.DependencyType == DependencyType.FinishToStart
                         && dependency.ValidationState != ValidationState.InvalidSourceEvidence
                         && string.Equals(CanonicalWorkItemKey.NormalizeKind(dependency.SubjectKind), "DeliveryCard", StringComparison.OrdinalIgnoreCase)
                         && string.Equals(CanonicalWorkItemKey.NormalizeKind(dependency.PredecessorKind), "DeliveryCard", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(dependency => dependency.SubjectKind, StringComparer.Ordinal)
                     .ThenBy(dependency => dependency.SubjectId, StringComparer.Ordinal)
                     .ThenBy(dependency => dependency.PredecessorKind, StringComparer.Ordinal)
                     .ThenBy(dependency => dependency.PredecessorId, StringComparer.Ordinal))
        {
            if (!cards.TryGetValue(dependency.SubjectId, out var successor))
            {
                continue;
            }

            if (!cards.ContainsKey(dependency.PredecessorId))
            {
                continue;
            }

            var successorState = records.TryGetValue(successor.Id, out var successorRecord)
                ? successorRecord.ExecutionState
                : successor.State;
            if (successorState != ExecutionState.NotStarted)
            {
                continue;
            }

            if (!dependencyDelayedPredecessors.Contains(dependency.PredecessorId))
            {
                continue;
            }

            if (!delayedPredecessorsBySuccessor.TryGetValue(successor.Id, out var delayedPredecessors))
            {
                delayedPredecessors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                delayedPredecessorsBySuccessor[successor.Id] = delayedPredecessors;
            }

            delayedPredecessors.Add(dependency.PredecessorId);
        }

        foreach (var (successorId, delayedPredecessors) in delayedPredecessorsBySuccessor.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var reasons = delayedPredecessors.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            atRisk++;
            alerts.Add(new Alert
            {
                WorkItemId = successorId,
                AlertCode = "AT_RISK",
                Severity = WarningSeverity.Warning,
                Message = $"Work item '{successorId}' may be at risk because predecessor(s) '{string.Join("', '", reasons)}' are late.",
                ReasonWorkItemIds = reasons,
                DerivedAt = asOfDate
            });
        }

        return new ManagementAnalysis
        {
            AsOfDate = asOfDate,
            BaselineFinish = project.Baseline.PlanningFinish,
            ExecutionStatus = new ExecutionStatusCounts
            {
                Total = project.DeliveryCards.Count,
                Completed = completed,
                InProgress = inProgress,
                NotStarted = notStarted,
                Suspended = suspended,
                Cancelled = cancelled,
                LateToStart = lateToStart,
                Overdue = overdue,
                CompletedLate = completedLate,
                AtRisk = atRisk
            },
            WorkItemVariances = variances,
            Alerts = alerts
                .OrderBy(alert => alert.WorkItemId, StringComparer.Ordinal)
                .ThenBy(alert => alert.AlertCode, StringComparer.Ordinal)
                .ToArray(),
            ViewSummaries =
            [
                new ViewSummary
                {
                    ViewId = "dashboard.completed",
                    Label = "Delivery cards completed",
                    Value = $"{completed}/{project.DeliveryCards.Count}",
                    State = DataState.Calculated
                }
            ]
        };
    }

    private static bool ValidDate(DateOnly? date) => date is not null && date != DateOnly.MinValue;
}
