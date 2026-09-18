using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed record DependencyAnalysisResult
{
    public DataState State { get; init; } = DataState.Unknown;
    public IReadOnlyList<CpmNodeMetric> Nodes { get; init; } = Array.Empty<CpmNodeMetric>();
    public IReadOnlyList<string> CriticalPathIds { get; init; } = Array.Empty<string>();
    public DateOnly? CalculatedFinish { get; init; }
    public ScheduleVariance? ScheduleVariance { get; init; }
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed class DependencyNetworkAnalyzer
{
    public DependencyAnalysisResult Analyze(CanonicalProject project)
    {
        var diagnostics = new List<ImportWarning>();
        var nodeDefinitions = project.DeliveryCards
            .Select(card => new NodeDefinition(
                CanonicalWorkItemKey.DeliveryCard(card.Id),
                card.PlannedDurationWorkingMinutes,
                card.DurationState,
                card.PlannedStart,
                card.PlannedFinish))
            .Concat(project.Milestones.Select(milestone => new NodeDefinition(
                CanonicalWorkItemKey.Milestone(milestone.Id),
                milestone.PlannedDurationWorkingMinutes,
                milestone.PlannedDurationWorkingMinutes is null ? DataState.Unknown : DataState.Known,
                milestone.PlannedDate,
                milestone.PlannedDate)))
            .ToArray();

        var duplicateNodeIds = nodeDefinitions
            .GroupBy(node => node.Key)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key.Kind, StringComparer.Ordinal)
            .ThenBy(key => key.Id, StringComparer.Ordinal)
            .ToArray();
        foreach (var duplicateNodeId in duplicateNodeIds)
        {
            AddDiagnostic(diagnostics, "DUPLICATE_DEPENDENCY_NODE", WarningSeverity.Error, $"Dependency graph contains duplicate node '{duplicateNodeId}'.", duplicateNodeId.Id);
        }

        if (duplicateNodeIds.Length > 0)
        {
            return Unknown(diagnostics);
        }

        var nodes = nodeDefinitions.ToDictionary(node => node.Key);
        foreach (var node in nodeDefinitions)
        {
            if (node.Duration is null || node.Duration < 0 || node.DurationState != DataState.Known)
            {
                AddDiagnostic(diagnostics, "MISSING_DEPENDENCY_DURATION", WarningSeverity.Error, $"Dependency node '{node.Key}' does not have a safe normalized duration.", node.Key.Id);
            }
        }

        var edges = new List<GraphEdge>();
        foreach (var dependency in project.Dependencies
                     .OrderBy(dependency => dependency.SubjectKind, StringComparer.Ordinal)
                     .ThenBy(dependency => dependency.SubjectId, StringComparer.Ordinal)
                     .ThenBy(dependency => dependency.PredecessorKind, StringComparer.Ordinal)
                     .ThenBy(dependency => dependency.PredecessorId, StringComparer.Ordinal))
        {
            var subjectKey = new CanonicalWorkItemKey(dependency.SubjectKind, dependency.SubjectId);
            var predecessorKey = new CanonicalWorkItemKey(dependency.PredecessorKind, dependency.PredecessorId);

            if (!dependency.AnalysisEligible)
            {
                if (dependency.ValidationState == ValidationState.InvalidSourceEvidence)
                {
                    AddDiagnostic(
                        diagnostics,
                        "INVALID_SOURCE_DEPENDENCY",
                        WarningSeverity.Warning,
                        $"Dependency '{dependency.SubjectId}' -> '{dependency.PredecessorId}' is retained as invalid source evidence and excluded from CPM.",
                        dependency.SubjectId);
                }

                continue;
            }

            // Work-package dependencies remain traceability edges. CPM is only
            // calculated over executable cards and milestone/decision nodes.
            if (string.Equals(subjectKey.Kind, "WorkPackage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(predecessorKey.Kind, "WorkPackage", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (dependency.DependencyType != DependencyType.FinishToStart)
            {
                AddDiagnostic(
                    diagnostics,
                    "UNSUPPORTED_DEPENDENCY_TYPE",
                    WarningSeverity.Error,
                    $"Dependency '{dependency.SubjectId}' -> '{dependency.PredecessorId}' uses unsupported type '{dependency.DependencyType}'.",
                    dependency.SubjectId);
                continue;
            }

            if (subjectKey == predecessorKey)
            {
                AddDiagnostic(diagnostics, "SELF_DEPENDENCY", WarningSeverity.Error, $"Dependency node '{dependency.SubjectId}' cannot depend on itself.", dependency.SubjectId);
                continue;
            }

            if (!nodes.ContainsKey(subjectKey))
            {
                AddDiagnostic(diagnostics, "MISSING_DEPENDENCY_SUBJECT", WarningSeverity.Error, $"Dependency subject '{dependency.SubjectId}' is missing from the CPM graph.", dependency.SubjectId);
                continue;
            }

            if (!nodes.ContainsKey(predecessorKey))
            {
                AddDiagnostic(diagnostics, "MISSING_DEPENDENCY_PREDECESSOR", WarningSeverity.Error, $"Dependency predecessor '{dependency.PredecessorId}' is missing from the CPM graph.", dependency.PredecessorId);
                continue;
            }

            edges.Add(new GraphEdge(predecessorKey, subjectKey));
        }

        if (diagnostics.Any(diagnostic => diagnostic.Severity == WarningSeverity.Error))
        {
            return Unknown(diagnostics);
        }

        var predecessors = nodes.Keys.ToDictionary(key => key, _ => new List<CanonicalWorkItemKey>());
        var successors = nodes.Keys.ToDictionary(key => key, _ => new List<CanonicalWorkItemKey>());
        foreach (var edge in edges.Distinct())
        {
            predecessors[edge.Subject].Add(edge.Predecessor);
            successors[edge.Predecessor].Add(edge.Subject);
        }

        var indegree = nodes.Keys.ToDictionary(key => key, key => predecessors[key].Count);
        var ready = new SortedSet<CanonicalWorkItemKey>(
            indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key),
            WorkItemKeyComparer.Instance);
        var topologicalOrder = new List<CanonicalWorkItemKey>(nodes.Count);
        while (ready.Count > 0)
        {
            var current = ready.Min!;
            ready.Remove(current);
            topologicalOrder.Add(current);
            foreach (var successor in successors[current].OrderBy(key => key.Kind, StringComparer.Ordinal).ThenBy(key => key.Id, StringComparer.Ordinal))
            {
                indegree[successor]--;
                if (indegree[successor] == 0)
                {
                    ready.Add(successor);
                }
            }
        }

        if (topologicalOrder.Count != nodes.Count)
        {
            AddDiagnostic(diagnostics, "DEPENDENCY_CYCLE", WarningSeverity.Error, "Dependency graph contains a cycle; CPM cannot be calculated.", null);
            return Unknown(diagnostics);
        }

        var earliestStart = nodes.Keys.ToDictionary(key => key, _ => 0);
        var earliestFinish = nodes.Keys.ToDictionary(key => key, _ => 0);
        foreach (var nodeKey in topologicalOrder)
        {
            earliestStart[nodeKey] = predecessors[nodeKey].Count == 0
                ? 0
                : predecessors[nodeKey].Max(predecessorKey => earliestFinish[predecessorKey]);
            earliestFinish[nodeKey] = earliestStart[nodeKey] + nodes[nodeKey].Duration!.Value;
        }

        var projectDuration = earliestFinish.Values.DefaultIfEmpty(0).Max();
        var latestFinish = nodes.Keys.ToDictionary(key => key, _ => projectDuration);
        var latestStart = nodes.Keys.ToDictionary(key => key, _ => 0);
        foreach (var nodeKey in topologicalOrder.AsEnumerable().Reverse())
        {
            if (successors[nodeKey].Count > 0)
            {
                latestFinish[nodeKey] = successors[nodeKey].Min(successorKey => latestStart[successorKey]);
            }

            latestStart[nodeKey] = latestFinish[nodeKey] - nodes[nodeKey].Duration!.Value;
        }

        var criticalPathIds = nodes.Keys
            .Where(key => latestStart[key] - earliestStart[key] == 0)
            .OrderBy(key => key.Kind, StringComparer.Ordinal)
            .ThenBy(key => key.Id, StringComparer.Ordinal)
            .Select(key => key.Id)
            .ToArray();
        var calendar = new WorkingCalendar(project.Capacity.Calendar);
        var anchor = ValidDate(project.Baseline.PlanningStart)
            ? project.Baseline.PlanningStart
            : nodeDefinitions.Where(node => ValidDate(node.PlannedStart)).Select(node => node.PlannedStart).DefaultIfEmpty(DateOnly.MinValue).Min();
        var hasAnchor = ValidDate(anchor);
        var anchorDate = anchor.GetValueOrDefault();
        DateOnly? calculatedFinish = hasAnchor
            ? calendar.DateForFinishOffset(anchorDate, projectDuration)
            : null;
        var scheduleVariance = BuildScheduleVariance(project.Baseline.PlanningFinish, calculatedFinish, calendar);
        var metrics = nodeDefinitions
            .OrderBy(node => node.Key.Kind, StringComparer.Ordinal)
            .ThenBy(node => node.Key.Id, StringComparer.Ordinal)
            .Select(node => new CpmNodeMetric
            {
                NodeId = node.Key.Id,
                NodeKind = node.Key.Kind,
                EarliestStartWorkingMinutes = earliestStart[node.Key],
                EarliestFinishWorkingMinutes = earliestFinish[node.Key],
                LatestStartWorkingMinutes = latestStart[node.Key],
                LatestFinishWorkingMinutes = latestFinish[node.Key],
                FloatWorkingMinutes = latestStart[node.Key] - earliestStart[node.Key],
                IsCritical = latestStart[node.Key] - earliestStart[node.Key] == 0,
                CalculatedStart = hasAnchor ? calendar.DateForStartOffset(anchorDate, earliestStart[node.Key]) : null,
                CalculatedFinish = hasAnchor ? calendar.DateForFinishOffset(anchorDate, earliestFinish[node.Key]) : null
            })
            .ToArray();

        return new DependencyAnalysisResult
        {
            State = DataState.Calculated,
            Nodes = metrics,
            CriticalPathIds = criticalPathIds,
            CalculatedFinish = calculatedFinish,
            ScheduleVariance = scheduleVariance,
            Diagnostics = diagnostics
        };
    }

    private static DependencyAnalysisResult Unknown(IReadOnlyList<ImportWarning> diagnostics) => new()
    {
        State = DataState.Unknown,
        Diagnostics = diagnostics
    };

    private static ScheduleVariance BuildScheduleVariance(DateOnly? baselineFinish, DateOnly? calculatedFinish, WorkingCalendar calendar)
    {
        var workingMinutes = calendar.SignedWorkingMinutes(baselineFinish, calculatedFinish);
        return new ScheduleVariance
        {
            WorkingMinutes = workingMinutes,
            Hours = workingMinutes is null ? null : workingMinutes.Value / 60m,
            State = workingMinutes is null ? DataState.Unknown : DataState.Calculated
        };
    }

    private static void AddDiagnostic(ICollection<ImportWarning> diagnostics, string code, WarningSeverity severity, string message, string? affectedId)
    {
        diagnostics.Add(new ImportWarning
        {
            Id = $"{code}:{affectedId ?? "graph"}",
            Severity = severity,
            Code = code,
            Message = message,
            AffectedIds = affectedId is null ? Array.Empty<string>() : [affectedId]
        });
    }

    private static bool ValidDate(DateOnly? date) => date is not null && date != DateOnly.MinValue;

    private sealed record NodeDefinition(
        CanonicalWorkItemKey Key,
        int? Duration,
        DataState DurationState,
        DateOnly? PlannedStart,
        DateOnly? PlannedFinish);

    private readonly record struct GraphEdge(CanonicalWorkItemKey Predecessor, CanonicalWorkItemKey Subject);

    private sealed class WorkItemKeyComparer : IComparer<CanonicalWorkItemKey>
    {
        public static WorkItemKeyComparer Instance { get; } = new();

        public int Compare(CanonicalWorkItemKey left, CanonicalWorkItemKey right)
        {
            var kindComparison = StringComparer.Ordinal.Compare(left.Kind, right.Kind);
            return kindComparison != 0
                ? kindComparison
                : StringComparer.Ordinal.Compare(left.Id, right.Id);
        }
    }
}
