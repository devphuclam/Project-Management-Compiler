using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public enum GanttLane
{
    Plan,
    Actual,
    Alert
}

public sealed record GanttLaneEntry
{
    public string WorkItemId { get; init; } = string.Empty;
    public GanttLane Lane { get; init; }
    public DateOnly? Start { get; init; }
    public DateOnly? Finish { get; init; }
    public bool IsOpenEnded { get; init; }
    public decimal? ActualEffortHours { get; init; }
    public decimal? RemainingEffortHours { get; init; }
    public DateTimeOffset? LastUpdatedAt { get; init; }
    public DataState State { get; init; } = DataState.Unknown;
    public string? AlertCode { get; init; }
    public string? Label { get; init; }
    public IReadOnlyList<string> ReasonWorkItemIds { get; init; } = Array.Empty<string>();
}

public sealed record GanttItem
{
    public string WorkItemId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string PhaseId { get; init; } = string.Empty;
    public string WorkPackageId { get; init; } = string.Empty;
    public ExecutionState? ExecutionState { get; init; }
    public bool HasExecutionEvidence { get; init; }
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
    public IReadOnlyList<string> LogicalRoles { get; init; } = Array.Empty<string>();
    public bool IsCritical { get; init; }
    public IReadOnlyList<string> DependencyIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<GanttLaneEntry> Lanes { get; init; } = Array.Empty<GanttLaneEntry>();
}

public sealed record GanttMilestoneEntry
{
    public string MilestoneId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public MilestoneKind Kind { get; init; }
    public string? ParentId { get; init; }
    public DateOnly? PlannedDate { get; init; }
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
    public bool IsCritical { get; init; }
    public IReadOnlyList<string> DependencyIds { get; init; } = Array.Empty<string>();
    public DataState State { get; init; } = DataState.Unknown;
}

public sealed record GanttProjection
{
    public IReadOnlyList<GanttItem> Items { get; init; } = Array.Empty<GanttItem>();
    public IReadOnlyList<GanttMilestoneEntry> Milestones { get; init; } = Array.Empty<GanttMilestoneEntry>();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed class GanttProjector
{
    public GanttProjection Build(CanonicalProject project, ManagementAnalysis analysis, DateOnly asOfDate)
    {
        var records = project.ExecutionOverlay.Records
            .ToDictionary(record => record.WorkItemId, StringComparer.OrdinalIgnoreCase);
        var criticalCardIds = analysis.CpmNodes
            .Where(node => node.IsCritical && string.Equals(CanonicalWorkItemKey.NormalizeKind(node.NodeKind), "DeliveryCard", StringComparison.OrdinalIgnoreCase))
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var criticalMilestoneIds = analysis.CpmNodes
            .Where(node => node.IsCritical && string.Equals(CanonicalWorkItemKey.NormalizeKind(node.NodeKind), "Milestone", StringComparison.OrdinalIgnoreCase))
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var alertsByWorkItem = analysis.Alerts
            .GroupBy(alert => alert.WorkItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(alert => alert.AlertCode, StringComparer.Ordinal).ToArray(), StringComparer.OrdinalIgnoreCase);
        var cardDependenciesBySubject = project.Dependencies
            .Where(dependency => string.Equals(CanonicalWorkItemKey.NormalizeKind(dependency.SubjectKind), "DeliveryCard", StringComparison.OrdinalIgnoreCase))
            .GroupBy(dependency => dependency.SubjectId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(dependency => dependency.PredecessorId).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var milestoneDependenciesBySubject = project.Dependencies
            .Where(dependency => string.Equals(CanonicalWorkItemKey.NormalizeKind(dependency.SubjectKind), "Milestone", StringComparison.OrdinalIgnoreCase))
            .GroupBy(dependency => dependency.SubjectId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(dependency => dependency.PredecessorId).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var logicalRolesByWorkItem = project.Assignments
            .GroupBy(assignment => assignment.WorkItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(assignment => assignment.LogicalRoleCode).Where(role => !string.IsNullOrWhiteSpace(role)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(role => role, StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var items = new List<GanttItem>();
        foreach (var card in project.DeliveryCards.OrderBy(card => card.Id, StringComparer.Ordinal))
        {
            records.TryGetValue(card.Id, out var record);
            var state = record?.ExecutionState ?? card.State;
            var lanes = new List<GanttLaneEntry>
            {
                new()
                {
                    WorkItemId = card.Id,
                    Lane = GanttLane.Plan,
                    Start = ValidDate(card.PlannedStart) ? card.PlannedStart : null,
                    Finish = ValidDate(card.PlannedFinish) ? card.PlannedFinish : null,
                    State = ValidDate(card.PlannedStart) && ValidDate(card.PlannedFinish) ? DataState.Known : DataState.Unknown,
                    Label = "PLAN"
                }
            };

            if (record is not null && (record.ActualStart is not null
                || record.ActualFinish is not null
                || record.ActualEffortHours is not null
                || record.RemainingEffortHours is not null))
            {
                lanes.Add(new GanttLaneEntry
                {
                    WorkItemId = card.Id,
                    Lane = GanttLane.Actual,
                    Start = record.ActualStart,
                    Finish = record.ActualFinish,
                    IsOpenEnded = record.ActualFinish is null && state == ExecutionState.InProgress,
                    ActualEffortHours = record.ActualEffortHours,
                    RemainingEffortHours = record.RemainingEffortHours,
                    LastUpdatedAt = record.LastUpdatedAt,
                    State = record.ActualFinish is null ? DataState.Unknown : DataState.Known,
                    Label = "ACTUAL"
                });
            }

            if (alertsByWorkItem.TryGetValue(card.Id, out var alerts))
            {
                lanes.AddRange(alerts.Select(alert => new GanttLaneEntry
                {
                    WorkItemId = card.Id,
                    Lane = GanttLane.Alert,
                    State = DataState.Calculated,
                    AlertCode = alert.AlertCode,
                    Label = alert.Message,
                    ReasonWorkItemIds = alert.ReasonWorkItemIds
                }));
            }

            items.Add(new GanttItem
            {
                WorkItemId = card.Id,
                Name = card.Name,
                PhaseId = card.PhaseId,
                WorkPackageId = card.WorkPackageId,
                ExecutionState = state,
                HasExecutionEvidence = record is not null,
                SourceReferences = card.SourceReferences,
                LogicalRoles = logicalRolesByWorkItem.TryGetValue(card.Id, out var logicalRoles)
                    ? logicalRoles
                    : Array.Empty<string>(),
                IsCritical = criticalCardIds.Contains(card.Id),
                DependencyIds = cardDependenciesBySubject.TryGetValue(card.Id, out var dependencyIds)
                    ? dependencyIds
                    : Array.Empty<string>(),
                Lanes = lanes
            });
        }

        var milestoneEntries = project.Milestones
            .OrderBy(milestone => milestone.PlannedDate)
            .ThenBy(milestone => milestone.Id, StringComparer.Ordinal)
            .Select(milestone => new GanttMilestoneEntry
            {
                MilestoneId = milestone.Id,
                Name = milestone.Name,
                Kind = milestone.Kind,
                ParentId = milestone.ParentId,
                PlannedDate = ValidDate(milestone.PlannedDate) ? milestone.PlannedDate : null,
                SourceReferences = milestone.SourceReferences,
                IsCritical = criticalMilestoneIds.Contains(milestone.Id),
                DependencyIds = milestoneDependenciesBySubject.TryGetValue(milestone.Id, out var dependencyIds)
                    ? dependencyIds
                    : Array.Empty<string>(),
                State = ValidDate(milestone.PlannedDate) ? DataState.Known : DataState.Unknown
            })
            .ToArray();

        return new GanttProjection
        {
            Items = items,
            Milestones = milestoneEntries,
            Diagnostics = analysis.Diagnostics
        };
    }

    private static bool ValidDate(DateOnly? date) => date is not null && date != DateOnly.MinValue;
}
