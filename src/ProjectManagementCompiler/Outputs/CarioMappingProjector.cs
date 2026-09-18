using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Outputs;

public sealed record CarioMappingConfiguration
{
    public IReadOnlyDictionary<string, CarioRoleMapping> LogicalRoles { get; init; } =
        new Dictionary<string, CarioRoleMapping>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, CarioTaskMapping> TaskMappings { get; init; } =
        new Dictionary<string, CarioTaskMapping>(StringComparer.OrdinalIgnoreCase);
}

public sealed record CarioRoleMapping
{
    public string LogicalRoleCode { get; init; } = string.Empty;
    public string? CarioRoleCode { get; init; }
    public string? ConcreteIdentity { get; init; }
}

public sealed record CarioTaskMapping
{
    public string? Priority { get; init; }
    public string? Department { get; init; }
    public string? Team { get; init; }
    public string? Notes { get; init; }
}

public sealed record CarioWorkbookModel
{
    public string ProjectName { get; init; } = string.Empty;
    public IReadOnlyList<CarioTaskRow> Tasks { get; init; } = Array.Empty<CarioTaskRow>();
    public IReadOnlyList<CarioAssignmentRow> Assignments { get; init; } = Array.Empty<CarioAssignmentRow>();
    public IReadOnlyList<CarioChildMilestoneRow> ChildrenMilestones { get; init; } = Array.Empty<CarioChildMilestoneRow>();
    public IReadOnlyList<CarioDependencyRow> Dependencies { get; init; } = Array.Empty<CarioDependencyRow>();
    public IReadOnlyList<CarioProjectInfoRow> ProjectInfo { get; init; } = Array.Empty<CarioProjectInfoRow>();
    public IReadOnlyList<ImportWarning> Warnings { get; init; } = Array.Empty<ImportWarning>();
}

public sealed record CarioTaskRow
{
    public string TaskId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedDeadline { get; init; }
    public decimal? PlannedEffortHours { get; init; }
    public string? Priority { get; init; }
    public string? Department { get; init; }
    public string? Team { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<string> MappingWarningIds { get; init; } = Array.Empty<string>();
}

public sealed record CarioAssignmentRow
{
    public string TaskId { get; init; } = string.Empty;
    public string LogicalRole { get; init; } = string.Empty;
    public string? CarioRoleCode { get; init; }
    public string? ConcreteIdentity { get; init; }
    public string MappingStatus { get; init; } = string.Empty;
}

public sealed record CarioChildMilestoneRow
{
    public string ParentWorkPackageId { get; init; } = string.Empty;
    public string ParentId { get; init; } = string.Empty;
    public string ParentKind { get; init; } = string.Empty;
    public string RecordType { get; init; } = string.Empty;
    public string RecordId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateOnly? PlannedDate { get; init; }
    public string Relationship { get; init; } = string.Empty;
}

public sealed record CarioDependencyRow
{
    public string SubjectId { get; init; } = string.Empty;
    public string PredecessorId { get; init; } = string.Empty;
    public DependencyType DependencyType { get; init; }
    public ValidationState ValidationState { get; init; }
    public bool AnalysisEligible { get; init; }
}

public sealed record CarioProjectInfoRow
{
    public string Key { get; init; } = string.Empty;
    public string? Value { get; init; }
}

public sealed class CarioMappingProjector
{
    private static readonly HashSet<string> SupportedCarioCodes =
        ["A", "R+", "R", "C", "I", "O"];

    public CarioWorkbookModel Build(CanonicalProject project, CarioMappingConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        configuration ??= new CarioMappingConfiguration();

        var warnings = project.Warnings.ToList();
        var warningIdsByTask = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var assignments = new List<CarioAssignmentRow>();
        foreach (var (assignment, index) in project.Assignments
                     .OrderBy(assignment => assignment.WorkItemId, StringComparer.Ordinal)
                     .ThenBy(assignment => assignment.LogicalRoleCode, StringComparer.Ordinal)
                     .Select((assignment, index) => (assignment, index)))
        {
            configuration.LogicalRoles.TryGetValue(assignment.LogicalRoleCode, out var roleMapping);
            var carioCode = roleMapping?.CarioRoleCode ?? assignment.CarioRoleCode;
            if (carioCode is not null && !SupportedCarioCodes.Contains(carioCode))
            {
                var invalidCodeWarning = NewWarning(
                    $"CARIO_MAPPING_INVALID_CODE:{assignment.WorkItemId}:{index + 1:D4}",
                    "CARIO_MAPPING_INVALID_CODE",
                    assignment.WorkItemId,
                    $"CARIO role code '{carioCode}' is not supported; the exported role cell is left blank.",
                    assignment.SourceReferences);
                warnings.Add(invalidCodeWarning);
                AddTaskWarning(warningIdsByTask, assignment.WorkItemId, invalidCodeWarning.Id);
                carioCode = null;
            }

            var concreteIdentity = string.IsNullOrWhiteSpace(roleMapping?.ConcreteIdentity)
                ? null
                : roleMapping!.ConcreteIdentity;
            var mappingStatus = concreteIdentity is null ? "UNRESOLVED_IDENTITY" : "MAPPED";
            if (concreteIdentity is null)
            {
                var unresolvedWarning = NewWarning(
                    $"CARIO_MAPPING_UNRESOLVED:{assignment.WorkItemId}:{index + 1:D4}",
                    "CARIO_MAPPING_UNRESOLVED",
                    assignment.WorkItemId,
                    $"No concrete CARIO identity is configured for logical role '{assignment.LogicalRoleCode}'; the identity cell remains blank.",
                    assignment.SourceReferences);
                warnings.Add(unresolvedWarning);
                AddTaskWarning(warningIdsByTask, assignment.WorkItemId, unresolvedWarning.Id);
            }

            assignments.Add(new CarioAssignmentRow
            {
                TaskId = assignment.WorkItemId,
                LogicalRole = assignment.LogicalRoleCode,
                CarioRoleCode = carioCode,
                ConcreteIdentity = concreteIdentity,
                MappingStatus = mappingStatus
            });
        }

        var tasks = project.DeliveryCards
            .OrderBy(card => card.Id, StringComparer.Ordinal)
            .Select(card =>
            {
                configuration.TaskMappings.TryGetValue(card.Id, out var taskMapping);
                return new CarioTaskRow
                {
                    TaskId = card.Id,
                    Title = card.Name,
                    // These are intentionally baseline-only. ExecutionOverlay is
                    // exported through the application state, never into CARIO
                    // planned-date cells.
                    PlannedStart = ValidDate(card.PlannedStart) ? card.PlannedStart : null,
                    PlannedDeadline = ValidDate(card.PlannedFinish) ? card.PlannedFinish : null,
                    PlannedEffortHours = card.PlannedEffortHours,
                    Priority = taskMapping?.Priority,
                    Department = taskMapping?.Department,
                    Team = taskMapping?.Team,
                    Notes = taskMapping?.Notes,
                    MappingWarningIds = warningIdsByTask.TryGetValue(card.Id, out var ids)
                        ? ids.OrderBy(id => id, StringComparer.Ordinal).ToArray()
                        : Array.Empty<string>()
                };
            })
            .ToArray();

        var childrenMilestones = project.WorkPackages
            .OrderBy(workPackage => workPackage.Id, StringComparer.Ordinal)
            .SelectMany(workPackage => project.DeliveryCards
                .Where(card => string.Equals(card.WorkPackageId, workPackage.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(card => card.Id, StringComparer.Ordinal)
                .Select(card => new CarioChildMilestoneRow
                {
                    ParentWorkPackageId = workPackage.Id,
                    ParentId = workPackage.Id,
                    ParentKind = "WorkPackage",
                    RecordType = "DeliveryCard",
                    RecordId = card.Id,
                    Name = card.Name,
                    PlannedDate = ValidDate(card.PlannedStart) ? card.PlannedStart : null,
                    Relationship = "CHILD"
                }))
            .Concat(project.Milestones
                .OrderBy(milestone => milestone.Id, StringComparer.Ordinal)
                .Select(milestone => new CarioChildMilestoneRow
                {
                    ParentWorkPackageId = project.WorkPackages.FirstOrDefault(workPackage => string.Equals(workPackage.Id, milestone.ParentId, StringComparison.OrdinalIgnoreCase))?.Id ?? string.Empty,
                    ParentId = milestone.ParentId ?? string.Empty,
                    ParentKind = project.Phases.Any(phase => string.Equals(phase.Id, milestone.ParentId, StringComparison.OrdinalIgnoreCase)) ? "Phase" : "WorkPackage",
                    RecordType = milestone.Kind == MilestoneKind.Decision ? "Decision" : "Milestone",
                    RecordId = milestone.Id,
                    Name = milestone.Name,
                    PlannedDate = ValidDate(milestone.PlannedDate) ? milestone.PlannedDate : null,
                    Relationship = "MILESTONE"
                }))
            .ToArray();

        var dependencies = project.Dependencies
            .OrderBy(dependency => dependency.SubjectId, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.PredecessorId, StringComparer.Ordinal)
            .Select(dependency => new CarioDependencyRow
            {
                SubjectId = dependency.SubjectId,
                PredecessorId = dependency.PredecessorId,
                DependencyType = dependency.DependencyType,
                ValidationState = dependency.ValidationState,
                AnalysisEligible = dependency.AnalysisEligible
            })
            .ToArray();

        var projectInfo = BuildProjectInfo(project);
        var distinctWarnings = warnings
            .GroupBy(warning => warning.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(warning => warning.Id, StringComparer.Ordinal)
            .ToArray();

        return new CarioWorkbookModel
        {
            ProjectName = project.Project.Name,
            Tasks = tasks,
            Assignments = assignments,
            ChildrenMilestones = childrenMilestones,
            Dependencies = dependencies,
            ProjectInfo = projectInfo,
            Warnings = distinctWarnings
        };
    }

    private static IReadOnlyList<CarioProjectInfoRow> BuildProjectInfo(CanonicalProject project) =>
    [
        new() { Key = "Project ID", Value = project.Project.Id },
        new() { Key = "Project name", Value = project.Project.Name },
        new() { Key = "Baseline ID", Value = project.Baseline.Id },
        new() { Key = "Baseline version", Value = project.Baseline.Version },
        new() { Key = "Baseline status", Value = project.Baseline.Status },
        new() { Key = "Planning start", Value = FormatDate(project.Baseline.PlanningStart) },
        new() { Key = "Planning finish", Value = FormatDate(project.Baseline.PlanningFinish) },
        new() { Key = "Target date", Value = FormatDate(project.Baseline.TargetDate) },
        new() { Key = "Capacity hours", Value = FormatDecimal(project.Baseline.CapacityHours ?? project.Capacity.CapacityHours) },
        new() { Key = "Reserve hours", Value = FormatDecimal(project.Baseline.ReserveHours ?? project.Reserve.InitialHours) },
        new() { Key = "WIP limit", Value = project.Policies.WorkInProgressLimit?.ToString() },
        new() { Key = "Source IDs", Value = string.Join(",", project.Project.SourceIds.OrderBy(id => id, StringComparer.Ordinal)) },
        new() { Key = "Source ref", Value = string.Join(",", project.Sources.Select(source => source.ResolvedRef).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal)) }
    ];

    private static void AddTaskWarning(IDictionary<string, List<string>> warningIdsByTask, string taskId, string warningId)
    {
        if (!warningIdsByTask.TryGetValue(taskId, out var warningIds))
        {
            warningIds = [];
            warningIdsByTask[taskId] = warningIds;
        }

        warningIds.Add(warningId);
    }

    private static ImportWarning NewWarning(
        string id,
        string code,
        string affectedId,
        string message,
        IReadOnlyList<SourceReference> sourceReferences) => new()
        {
            Id = id,
            Severity = WarningSeverity.Warning,
            Code = code,
            Message = message,
            AffectedIds = [affectedId],
            SourceReferences = sourceReferences
        };

    private static string? FormatDate(DateOnly? date) => ValidDate(date) ? date!.Value.ToString("yyyy-MM-dd") : null;
    private static string? FormatDecimal(decimal? value) => value?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    private static bool ValidDate(DateOnly? date) => date is not null && date != DateOnly.MinValue;
}
