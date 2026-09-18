using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public enum WbsNodeKind
{
    Project,
    Phase,
    WorkPackage,
    DeliveryCard,
    Milestone
}

public sealed record WbsNode
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public WbsNodeKind Kind { get; init; }
    public string? ParentId { get; init; }
    public string? PhaseId { get; init; }
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedFinish { get; init; }
    public decimal? PlannedEffortHours { get; init; }
    public int? PlannedDurationWorkingMinutes { get; init; }
    public ExecutionState? ExecutionState { get; init; }
    public IReadOnlyList<WbsNode> Children { get; init; } = Array.Empty<WbsNode>();
}

public sealed record WbsProjection
{
    public WbsNode Root { get; init; } = new();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed class WbsProjector
{
    public WbsProjection Build(CanonicalProject project)
    {
        var diagnostics = new List<ImportWarning>();
        var phaseIds = project.Phases.Select(phase => phase.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var workPackagesByPhase = project.WorkPackages
            .GroupBy(workPackage => workPackage.PhaseId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(workPackage => workPackage.Id, StringComparer.Ordinal).ToArray(), StringComparer.OrdinalIgnoreCase);
        var cardsByWorkPackage = project.DeliveryCards
            .GroupBy(card => card.WorkPackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(card => card.Id, StringComparer.Ordinal).ToArray(), StringComparer.OrdinalIgnoreCase);
        var milestonesByPhase = project.Milestones
            .Where(milestone => milestone.ParentId is not null)
            .GroupBy(milestone => milestone.ParentId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(milestone => milestone.PlannedDate).ThenBy(milestone => milestone.Id, StringComparer.Ordinal).ToArray(), StringComparer.OrdinalIgnoreCase);

        var phases = new List<WbsNode>();
        foreach (var phase in project.Phases.OrderBy(phase => phase.Id, StringComparer.Ordinal))
        {
            var workPackages = new List<WbsNode>();
            if (workPackagesByPhase.TryGetValue(phase.Id, out var phaseWorkPackages))
            {
                foreach (var workPackage in phaseWorkPackages)
                {
                    var cards = new List<WbsNode>();
                    if (cardsByWorkPackage.TryGetValue(workPackage.Id, out var packageCards))
                    {
                        cards.AddRange(packageCards.Select(CreateCardNode));
                    }

                    workPackages.Add(CreatePlannedNode(
                        workPackage.Id,
                        workPackage.Name,
                        WbsNodeKind.WorkPackage,
                        phase.Id,
                        workPackage.PhaseId,
                        workPackage.PlannedStart,
                        workPackage.PlannedFinish,
                        workPackage.PlannedEffortHours,
                        workPackage.PlannedDurationWorkingMinutes,
                        null,
                        cards));
                }
            }

            var milestones = new List<WbsNode>();
            if (milestonesByPhase.TryGetValue(phase.Id, out var phaseMilestones))
            {
                milestones.AddRange(phaseMilestones.Select(CreateMilestoneNode));
            }

            phases.Add(CreatePlannedNode(
                phase.Id,
                phase.Name,
                WbsNodeKind.Phase,
                project.Project.Id,
                phase.Id,
                phase.PlannedStart,
                phase.PlannedFinish,
                phase.PlannedEffortHours,
                phase.PlannedDurationWorkingMinutes,
                null,
                workPackages.Concat(milestones).OrderBy(node => node.Kind).ThenBy(node => node.Id, StringComparer.Ordinal).ToArray()));
        }

        foreach (var workPackage in project.WorkPackages.Where(workPackage => !phaseIds.Contains(workPackage.PhaseId)))
        {
            AddMissingParentDiagnostic(diagnostics, "MISSING_WBS_PHASE", workPackage.Id, workPackage.PhaseId, "work package");
        }

        var workPackageIds = project.WorkPackages.Select(workPackage => workPackage.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var card in project.DeliveryCards.Where(card => !workPackageIds.Contains(card.WorkPackageId)))
        {
            AddMissingParentDiagnostic(diagnostics, "MISSING_WBS_WORK_PACKAGE", card.Id, card.WorkPackageId, "delivery card");
        }

        foreach (var milestone in project.Milestones.Where(milestone => milestone.ParentId is null || !phaseIds.Contains(milestone.ParentId)))
        {
            AddMissingParentDiagnostic(diagnostics, "MISSING_WBS_PHASE", milestone.Id, milestone.ParentId ?? string.Empty, "milestone");
        }

        var root = new WbsNode
        {
            Id = project.Project.Id,
            Name = project.Project.Name,
            Kind = WbsNodeKind.Project,
            Children = phases
        };

        return new WbsProjection
        {
            Root = root,
            Diagnostics = diagnostics
        };
    }

    private static WbsNode CreateCardNode(DeliveryCard card) => CreatePlannedNode(
        card.Id,
        card.Name,
        WbsNodeKind.DeliveryCard,
        card.WorkPackageId,
        card.PhaseId,
        card.PlannedStart,
        card.PlannedFinish,
        card.PlannedEffortHours,
        card.PlannedDurationWorkingMinutes,
        card.State,
        Array.Empty<WbsNode>());

    private static WbsNode CreateMilestoneNode(MilestoneDecision milestone) => new()
    {
        Id = milestone.Id,
        Name = milestone.Name,
        Kind = WbsNodeKind.Milestone,
        ParentId = milestone.ParentId,
        PlannedStart = ValidDate(milestone.PlannedDate) ? milestone.PlannedDate : null,
        PlannedFinish = ValidDate(milestone.PlannedDate) ? milestone.PlannedDate : null,
        PlannedEffortHours = milestone.PlannedEffortHours,
        PlannedDurationWorkingMinutes = milestone.PlannedDurationWorkingMinutes,
        ExecutionState = milestone.State
    };

    private static WbsNode CreatePlannedNode(
        string id,
        string name,
        WbsNodeKind kind,
        string? parentId,
        string? phaseId,
        DateOnly? plannedStart,
        DateOnly? plannedFinish,
        decimal? plannedEffortHours,
        int? plannedDurationWorkingMinutes,
        ExecutionState? executionState,
        IReadOnlyList<WbsNode> children) => new()
    {
        Id = id,
        Name = name,
        Kind = kind,
        ParentId = parentId,
        PhaseId = phaseId,
        PlannedStart = ValidDate(plannedStart) ? plannedStart : null,
        PlannedFinish = ValidDate(plannedFinish) ? plannedFinish : null,
        PlannedEffortHours = plannedEffortHours,
        PlannedDurationWorkingMinutes = plannedDurationWorkingMinutes,
        ExecutionState = executionState,
        Children = children
    };

    private static void AddMissingParentDiagnostic(ICollection<ImportWarning> diagnostics, string code, string id, string parentId, string kind)
    {
        diagnostics.Add(new ImportWarning
        {
            Id = $"{code}:{id}:{parentId}",
            Severity = WarningSeverity.Error,
            Code = code,
            Message = $"The {kind} '{id}' cannot be projected because parent '{parentId}' is missing.",
            AffectedIds = [id, parentId]
        });
    }

    private static bool ValidDate(DateOnly? date) => date is not null && date != DateOnly.MinValue;
}
