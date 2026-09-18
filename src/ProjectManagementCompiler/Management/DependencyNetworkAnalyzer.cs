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
                card.Id,
                card.PlannedDurationWorkingMinutes,
                card.DurationState,
                card.PlannedStart,
                card.PlannedFinish))
            .Concat(project.Milestones.Select(milestone => new NodeDefinition(
                milestone.Id,
                milestone.PlannedDurationWorkingMinutes,
                milestone.PlannedDurationWorkingMinutes is null ? DataState.Unknown : DataState.Known,
                milestone.PlannedDate,
                milestone.PlannedDate)))
            .ToArray();

        var duplicateNodeIds = nodeDefinitions
            .GroupBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        foreach (var duplicateNodeId in duplicateNodeIds)
        {
            AddDiagnostic(diagnostics, "DUPLICATE_DEPENDENCY_NODE", WarningSeverity.Error, $"Dependency graph contains duplicate node '{duplicateNodeId}'.", duplicateNodeId);
        }

        if (duplicateNodeIds.Length > 0)
        {
            return Unknown(diagnostics);
        }

        var nodes = nodeDefinitions.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodeDefinitions)
        {
            if (node.Duration is null || node.Duration < 0 || node.DurationState != DataState.Known)
            {
                AddDiagnostic(diagnostics, "MISSING_DEPENDENCY_DURATION", WarningSeverity.Error, $"Dependency node '{node.Id}' does not have a safe normalized duration.", node.Id);
            }
        }

        var edges = new List<GraphEdge>();
        foreach (var dependency in project.Dependencies
                     .OrderBy(dependency => dependency.SubjectId, StringComparer.Ordinal)
                     .ThenBy(dependency => dependency.PredecessorId, StringComparer.Ordinal))
        {
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

            if (string.Equals(dependency.SubjectId, dependency.PredecessorId, StringComparison.OrdinalIgnoreCase))
            {
                AddDiagnostic(diagnostics, "SELF_DEPENDENCY", WarningSeverity.Error, $"Dependency node '{dependency.SubjectId}' cannot depend on itself.", dependency.SubjectId);
                continue;
            }

            if (!nodes.ContainsKey(dependency.SubjectId))
            {
                AddDiagnostic(diagnostics, "MISSING_DEPENDENCY_SUBJECT", WarningSeverity.Error, $"Dependency subject '{dependency.SubjectId}' is missing from the CPM graph.", dependency.SubjectId);
                continue;
            }

            if (!nodes.ContainsKey(dependency.PredecessorId))
            {
                AddDiagnostic(diagnostics, "MISSING_DEPENDENCY_PREDECESSOR", WarningSeverity.Error, $"Dependency predecessor '{dependency.PredecessorId}' is missing from the CPM graph.", dependency.PredecessorId);
                continue;
            }

            edges.Add(new GraphEdge(dependency.PredecessorId, dependency.SubjectId));
        }

        if (diagnostics.Any(diagnostic => diagnostic.Severity == WarningSeverity.Error))
        {
            return Unknown(diagnostics);
        }

        var predecessors = nodes.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        var successors = nodes.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges.Distinct())
        {
            predecessors[edge.SubjectId].Add(edge.PredecessorId);
            successors[edge.PredecessorId].Add(edge.SubjectId);
        }

        var indegree = nodes.Keys.ToDictionary(id => id, id => predecessors[id].Count, StringComparer.OrdinalIgnoreCase);
        var ready = new SortedSet<string>(
            indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key),
            StringComparer.Ordinal);
        var topologicalOrder = new List<string>(nodes.Count);
        while (ready.Count > 0)
        {
            var current = ready.Min!;
            ready.Remove(current);
            topologicalOrder.Add(current);
            foreach (var successor in successors[current].OrderBy(id => id, StringComparer.Ordinal))
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

        var earliestStart = nodes.Keys.ToDictionary(id => id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var earliestFinish = nodes.Keys.ToDictionary(id => id, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var nodeId in topologicalOrder)
        {
            earliestStart[nodeId] = predecessors[nodeId].Count == 0
                ? 0
                : predecessors[nodeId].Max(predecessorId => earliestFinish[predecessorId]);
            earliestFinish[nodeId] = earliestStart[nodeId] + nodes[nodeId].Duration!.Value;
        }

        var projectDuration = earliestFinish.Values.DefaultIfEmpty(0).Max();
        var latestFinish = nodes.Keys.ToDictionary(id => id, _ => projectDuration, StringComparer.OrdinalIgnoreCase);
        var latestStart = nodes.Keys.ToDictionary(id => id, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var nodeId in topologicalOrder.AsEnumerable().Reverse())
        {
            if (successors[nodeId].Count > 0)
            {
                latestFinish[nodeId] = successors[nodeId].Min(successorId => latestStart[successorId]);
            }

            latestStart[nodeId] = latestFinish[nodeId] - nodes[nodeId].Duration!.Value;
        }

        var criticalPathIds = nodes.Keys
            .Where(id => latestStart[id] - earliestStart[id] == 0)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var calendar = new WorkingCalendar(project.Capacity.Calendar);
        var anchor = ValidDate(project.Baseline.PlanningStart)
            ? project.Baseline.PlanningStart
            : nodeDefinitions.Where(node => ValidDate(node.PlannedStart)).Select(node => node.PlannedStart).DefaultIfEmpty(DateOnly.MinValue).Min();
        var hasAnchor = ValidDate(anchor);
        var anchorDate = anchor.GetValueOrDefault();
        DateOnly? calculatedFinish = hasAnchor
            ? calendar.AddWorkingMinutes(anchorDate, projectDuration)
            : null;
        var scheduleVariance = BuildScheduleVariance(project.Baseline.PlanningFinish, calculatedFinish, calendar);
        var metrics = nodeDefinitions
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .Select(node => new CpmNodeMetric
            {
                NodeId = node.Id,
                EarliestStartWorkingMinutes = earliestStart[node.Id],
                EarliestFinishWorkingMinutes = earliestFinish[node.Id],
                LatestStartWorkingMinutes = latestStart[node.Id],
                LatestFinishWorkingMinutes = latestFinish[node.Id],
                FloatWorkingMinutes = latestStart[node.Id] - earliestStart[node.Id],
                IsCritical = latestStart[node.Id] - earliestStart[node.Id] == 0,
                CalculatedStart = hasAnchor ? calendar.AddWorkingMinutes(anchorDate, earliestStart[node.Id]) : null,
                CalculatedFinish = hasAnchor ? calendar.AddWorkingMinutes(anchorDate, Math.Max(0, earliestFinish[node.Id] - 1)) : null
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
        string Id,
        int? Duration,
        DataState DurationState,
        DateOnly PlannedStart,
        DateOnly PlannedFinish);

    private readonly record struct GraphEdge(string PredecessorId, string SubjectId);
}
