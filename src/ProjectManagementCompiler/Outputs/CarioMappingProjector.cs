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
    public string WorkItemType { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? PhaseId { get; init; }
    public string? WorkPackageId { get; init; }
    public DateOnly? PlannedStart { get; init; }
    public DateOnly? PlannedDeadline { get; init; }
    public string? InitialState { get; init; }
    public decimal? PlannedEffortHours { get; init; }
    public string? BaselineAnalysisState { get; init; }
    public string? Priority { get; init; }
    public string? Department { get; init; }
    public string? Team { get; init; }
    public string? Notes { get; init; }
    public string? SourceReference { get; init; }
    public IReadOnlyList<string> MappingWarningIds { get; init; } = Array.Empty<string>();
}

public sealed record CarioAssignmentRow
{
    public string TaskId { get; init; } = string.Empty;
    public string LogicalRole { get; init; } = string.Empty;
    public string? CarioRoleCode { get; init; }
    public string? ConcreteIdentity { get; init; }
    public string MappingStatus { get; init; } = string.Empty;
    public string? SourceReference { get; init; }
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
    public string? SourceReference { get; init; }
}

public sealed record CarioDependencyRow
{
    public string SubjectId { get; init; } = string.Empty;
    public string PredecessorId { get; init; } = string.Empty;
    public DependencyType DependencyType { get; init; }
    public ValidationState ValidationState { get; init; }
    public bool AnalysisEligible { get; init; }
    public string? SourceReference { get; init; }
}

public sealed record CarioProjectInfoRow
{
    public string Key { get; init; } = string.Empty;
    public string? Value { get; init; }
    public DataState DataState { get; init; } = DataState.Unknown;
    public string? SourceReference { get; init; }
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
                MappingStatus = mappingStatus,
                SourceReference = FormatSourceReferences(assignment.SourceReferences)
            });
        }

        var cardTasks = project.DeliveryCards
            .OrderBy(card => card.Id, StringComparer.Ordinal)
            .Select(card =>
            {
                configuration.TaskMappings.TryGetValue(card.Id, out var taskMapping);
                return new CarioTaskRow
                {
                    WorkItemType = "DeliveryCard",
                    TaskId = card.Id,
                    Title = card.Name,
                    PhaseId = card.PhaseId,
                    WorkPackageId = card.WorkPackageId,
                    // These are intentionally baseline-only. ExecutionOverlay is
                    // exported through the application state, never into CARIO
                    // planned-date cells.
                    PlannedStart = ValidDate(card.PlannedStart) ? card.PlannedStart : null,
                    PlannedDeadline = ValidDate(card.PlannedFinish) ? card.PlannedFinish : null,
                    InitialState = card.State?.ToString().ToUpperInvariant() ?? "UNKNOWN",
                    PlannedEffortHours = card.PlannedEffortHours,
                    BaselineAnalysisState = FormatBaselineAnalysisState(project, card.PlannedStart, card.PlannedFinish, card.PlannedEffortHours),
                    Priority = taskMapping?.Priority,
                    Department = taskMapping?.Department,
                    Team = taskMapping?.Team,
                    Notes = taskMapping?.Notes,
                    SourceReference = FormatSourceReferences(card.SourceReferences),
                    MappingWarningIds = warningIdsByTask.TryGetValue(card.Id, out var ids)
                        ? ids.OrderBy(id => id, StringComparer.Ordinal).ToArray()
                        : Array.Empty<string>()
                };
            })
            .ToArray();

        var milestoneTasks = project.Milestones
            .OrderBy(milestone => milestone.Id, StringComparer.Ordinal)
            .Select(milestone =>
            {
                configuration.TaskMappings.TryGetValue(milestone.Id, out var taskMapping);
                var phaseId = ResolveMilestonePhaseId(project, milestone.ParentId);
                return new CarioTaskRow
                {
                    WorkItemType = milestone.Kind.ToString(),
                    TaskId = milestone.Id,
                    Title = milestone.Name,
                    PhaseId = phaseId,
                    WorkPackageId = ResolveMilestoneWorkPackageId(project, milestone.ParentId),
                    PlannedStart = ValidDate(milestone.PlannedDate) ? milestone.PlannedDate : null,
                    PlannedDeadline = ValidDate(milestone.PlannedDate) ? milestone.PlannedDate : null,
                    InitialState = milestone.State?.ToString().ToUpperInvariant() ?? "UNKNOWN",
                    PlannedEffortHours = milestone.PlannedEffortHours,
                    BaselineAnalysisState = FormatBaselineAnalysisState(project, milestone.PlannedDate, milestone.PlannedDate, milestone.PlannedEffortHours),
                    Priority = taskMapping?.Priority,
                    Department = taskMapping?.Department,
                    Team = taskMapping?.Team,
                    Notes = taskMapping?.Notes,
                    SourceReference = FormatSourceReferences(milestone.SourceReferences),
                    MappingWarningIds = warningIdsByTask.TryGetValue(milestone.Id, out var ids)
                        ? ids.OrderBy(id => id, StringComparer.Ordinal).ToArray()
                        : Array.Empty<string>()
                };
            })
            .ToArray();

        var tasks = cardTasks.Concat(milestoneTasks).ToArray();

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
                    Relationship = "CHILD",
                    SourceReference = FormatSourceReferences(card.SourceReferences)
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
                    Relationship = "MILESTONE",
                    SourceReference = FormatSourceReferences(milestone.SourceReferences)
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
                AnalysisEligible = dependency.AnalysisEligible,
                SourceReference = FormatSourceReferences(dependency.SourceReferences)
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

    private static IReadOnlyList<CarioProjectInfoRow> BuildProjectInfo(CanonicalProject project)
    {
        var sourceReference = FormatSourceReferences(project.Provenance);
        var resolvedRefs = string.Join(",", project.Sources
            .Select(source => source.ResolvedRef)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal));
        var analysisReference = FormatSourceReferences(project.Provenance);
        var analysis = project.Analysis;
        return
        [
            Info("Project ID", project.Project.Id, DataState.Known, sourceReference),
            Info("Project name", project.Project.Name, DataState.Known, sourceReference),
            Info("Baseline ID", project.Baseline.Id, DataState.Known, sourceReference),
            Info("Baseline version", project.Baseline.Version, DataState.Known, sourceReference),
            Info("Baseline status", project.Baseline.Status, DataState.Known, sourceReference),
            Info("Planning start", FormatDate(project.Baseline.PlanningStart), project.Baseline.PlanningStart is null ? DataState.Unknown : DataState.Known, sourceReference),
            Info("Planning finish", FormatDate(project.Baseline.PlanningFinish), project.Baseline.PlanningFinish is null ? DataState.Unknown : DataState.Known, sourceReference),
            Info("Target date", FormatDate(project.Baseline.TargetDate), project.Baseline.TargetDate is null ? DataState.Unknown : DataState.Known, sourceReference),
            Info("Capacity hours", FormatDecimal(project.Baseline.CapacityHours ?? project.Capacity.CapacityHours), project.Baseline.CapacityHours is not null || project.Capacity.CapacityHours is not null ? DataState.Known : DataState.Unknown, sourceReference),
            Info("Reserve hours", FormatDecimal(project.Baseline.ReserveHours ?? project.Reserve.InitialHours), project.Baseline.ReserveHours is not null || project.Reserve.InitialHours is not null ? DataState.Known : DataState.Unknown, sourceReference),
            Info("WIP limit", project.Policies.WorkInProgressLimit?.ToString(), project.Policies.WorkInProgressLimit is null ? DataState.Unknown : DataState.Known, sourceReference),
            Info("Source IDs", string.Join(",", project.Project.SourceIds.OrderBy(id => id, StringComparer.Ordinal)), project.Project.SourceIds.Count == 0 ? DataState.Unknown : DataState.Known, sourceReference),
            Info("Source ref", string.IsNullOrWhiteSpace(resolvedRefs) ? null : resolvedRefs, string.IsNullOrWhiteSpace(resolvedRefs) ? DataState.Unknown : DataState.Known, sourceReference),
            Info("CPM state", analysis?.CpmState.ToString().ToUpperInvariant(), analysis is null ? DataState.Unknown : DataState.Calculated, analysisReference),
            Info("Forecast state", analysis?.ForecastState.ToString().ToUpperInvariant(), analysis is null ? DataState.Unknown : analysis.ForecastState, analysisReference),
            Info("Export note", "Human-assisted CARIO fill file; native CARIO import is not claimed.", DataState.Known, sourceReference)
        ];
    }

    private static CarioProjectInfoRow Info(string key, string? value, DataState state, string? sourceReference) => new()
    {
        Key = key,
        Value = value,
        DataState = state,
        SourceReference = sourceReference
    };

    private static string FormatBaselineAnalysisState(CanonicalProject project, DateOnly? start, DateOnly? finish, decimal? effort)
    {
        var baseline = ValidDate(start) && ValidDate(finish) && effort is not null ? "KNOWN" : "PARTIAL";
        var analysis = project.Analysis?.CpmState.ToString().ToUpperInvariant() ?? "UNKNOWN";
        return $"BASELINE_{baseline};CPM_{analysis}";
    }

    private static string? ResolveMilestonePhaseId(CanonicalProject project, string? parentId)
    {
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return null;
        }

        var phase = project.Phases.FirstOrDefault(candidate => string.Equals(candidate.Id, parentId, StringComparison.OrdinalIgnoreCase));
        if (phase is not null)
        {
            return phase.Id;
        }

        return project.WorkPackages
            .FirstOrDefault(workPackage => string.Equals(workPackage.Id, parentId, StringComparison.OrdinalIgnoreCase))
            ?.PhaseId;
    }

    private static string? ResolveMilestoneWorkPackageId(CanonicalProject project, string? parentId) =>
        project.WorkPackages.Any(workPackage => string.Equals(workPackage.Id, parentId, StringComparison.OrdinalIgnoreCase))
            ? parentId
            : null;

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
    private static string? FormatSourceReferences(IReadOnlyList<SourceReference> references) =>
        references.Count == 0
            ? null
            : string.Join(";", references.Select(FormatSourceReference).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal));

    private static string FormatSourceReference(SourceReference reference)
    {
        var suffix = string.Join("#", new[] { reference.Section, reference.Table, reference.Item }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(suffix)
            ? $"{reference.SourceId}:{reference.RelativeFile}"
            : $"{reference.SourceId}:{reference.RelativeFile}#{suffix}";
    }

    private static bool ValidDate(DateOnly? date) => date is not null && date != DateOnly.MinValue;
}
