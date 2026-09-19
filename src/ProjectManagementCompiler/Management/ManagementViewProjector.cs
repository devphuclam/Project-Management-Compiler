using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed record ManagementViewSet
{
    public WbsProjection Wbs { get; init; } = new();
    public GanttProjection Gantt { get; init; } = new();
    public KanbanProjection Kanban { get; init; } = new();
    public DependencyNetworkProjection DependencyNetwork { get; init; } = new();
    public CpmProjection Cpm { get; init; } = new();
    public DashboardProjection Dashboard { get; init; } = new();
    public ManagementControlView ManagementControl { get; init; } = new();
}

public sealed record KanbanProjection
{
    public IReadOnlyList<KanbanColumn> Columns { get; init; } = Array.Empty<KanbanColumn>();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed record KanbanColumn
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int? WipLimit { get; init; }
    public int WipUsed { get; init; }
    public IReadOnlyList<KanbanItem> Items { get; init; } = Array.Empty<KanbanItem>();
}

public sealed record KanbanItem
{
    public string WorkItemId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string PhaseId { get; init; } = string.Empty;
    public string WorkPackageId { get; init; } = string.Empty;
    public ExecutionState? ExecutionState { get; init; }
    public bool IsOverdue { get; init; }
    public bool IsAtRisk { get; init; }
    public bool IsLateToStart { get; init; }
}

public sealed record DependencyNetworkProjection
{
    public IReadOnlyList<DependencyNetworkNode> Nodes { get; init; } = Array.Empty<DependencyNetworkNode>();
    public IReadOnlyList<DependencyNetworkEdge> Edges { get; init; } = Array.Empty<DependencyNetworkEdge>();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed record DependencyNetworkNode
{
    public string Key { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsMilestone { get; init; }
    public DateOnly? PlannedDate { get; init; }
}

public sealed record DependencyNetworkEdge
{
    public string SubjectKind { get; init; } = string.Empty;
    public string SubjectId { get; init; } = string.Empty;
    public string PredecessorKind { get; init; } = string.Empty;
    public string PredecessorId { get; init; } = string.Empty;
    public string SubjectKey { get; init; } = string.Empty;
    public string PredecessorKey { get; init; } = string.Empty;
    public DependencyType DependencyType { get; init; }
    public ValidationState ValidationState { get; init; }
    public bool IncludedInAnalysis { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record CpmProjection
{
    public DataState State { get; init; } = DataState.Unknown;
    public DateOnly? CalculatedFinish { get; init; }
    public IReadOnlyList<string> CriticalPathIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CpmProjectionRow> Rows { get; init; } = Array.Empty<CpmProjectionRow>();
}

public sealed record CpmProjectionRow
{
    public string NodeKind { get; init; } = string.Empty;
    public string NodeId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsMilestone { get; init; }
    public int? EarliestStartWorkingMinutes { get; init; }
    public int? EarliestFinishWorkingMinutes { get; init; }
    public int? LatestStartWorkingMinutes { get; init; }
    public int? LatestFinishWorkingMinutes { get; init; }
    public int? FloatWorkingMinutes { get; init; }
    public bool IsCritical { get; init; }
    public DateOnly? CalculatedStart { get; init; }
    public DateOnly? CalculatedFinish { get; init; }
}

public sealed record DashboardProjection
{
    public int TotalCards { get; init; }
    public int Completed { get; init; }
    public int InProgress { get; init; }
    public int NotStarted { get; init; }
    public int Suspended { get; init; }
    public int Cancelled { get; init; }
    public int LateToStart { get; init; }
    public int Overdue { get; init; }
    public int CompletedLate { get; init; }
    public int AtRisk { get; init; }
    public DateOnly? BaselineFinish { get; init; }
    public DateOnly? CpmFinish { get; init; }
    public DateOnly? ForecastFinish { get; init; }
    public DataState ForecastState { get; init; } = DataState.Unknown;
    public IReadOnlyList<ViewSummary> Summaries { get; init; } = Array.Empty<ViewSummary>();
}

public sealed class ManagementViewProjector
{
    private static readonly (string Id, string Label, ExecutionState? State)[] ColumnDefinitions =
    [
        ("NOT_STARTED", "Chưa bắt đầu", ExecutionState.NotStarted),
        ("IN_PROGRESS", "Đang thực hiện", ExecutionState.InProgress),
        ("COMPLETED", "Hoàn thành", ExecutionState.Completed),
        ("SUSPENDED", "Tạm ngưng", ExecutionState.Suspended),
        ("CANCELLED", "Hủy", ExecutionState.Cancelled),
        ("UNKNOWN", "Unknown", null)
    ];

    public ManagementViewSet Build(CanonicalProject project, ManagementAnalysis analysis, DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(analysis);

        return new ManagementViewSet
        {
            Wbs = new WbsProjector().Build(project),
            Gantt = new GanttProjector().Build(project, analysis, asOfDate),
            Kanban = BuildKanban(project, analysis),
            DependencyNetwork = BuildDependencyNetwork(project),
            Cpm = BuildCpm(project, analysis),
            Dashboard = BuildDashboard(project, analysis),
            ManagementControl = new ManagementControlViewProjector().Build(project)
        };
    }

    private static KanbanProjection BuildKanban(CanonicalProject project, ManagementAnalysis analysis)
    {
        var sourceExecution = ExecutionTruthResolver.UsesSourceExecution(project);
        var alerts = analysis.Alerts
            .GroupBy(alert => alert.WorkItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(alert => alert.AlertCode).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        var items = project.DeliveryCards
            .OrderBy(card => card.Id, StringComparer.Ordinal)
            .Select(card =>
            {
                var record = ExecutionTruthResolver.ForCard(project, card.Id);
                alerts.TryGetValue(card.Id, out var itemAlerts);
                return new
                {
                    Card = card,
                    State = record?.ExecutionState ?? (sourceExecution ? null : card.State),
                    Alerts = itemAlerts ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                };
            })
            .ToArray();

        var columns = ColumnDefinitions
            .Where(definition => definition.State is not null || items.Any(item => item.State is null))
            .Select(definition =>
            {
                var columnItems = items
                    .Where(item => definition.State is null ? item.State is null : item.State == definition.State)
                    .Select(item => new KanbanItem
                    {
                        WorkItemId = item.Card.Id,
                        Name = item.Card.Name,
                        PhaseId = item.Card.PhaseId,
                        WorkPackageId = item.Card.WorkPackageId,
                        ExecutionState = item.State,
                        IsOverdue = item.Alerts.Contains("OVERDUE"),
                        IsAtRisk = item.Alerts.Contains("AT_RISK"),
                        IsLateToStart = item.Alerts.Contains("START_DELAY")
                    })
                    .ToArray();

                return new KanbanColumn
                {
                    Id = definition.Id,
                    Label = definition.Label,
                    WipLimit = definition.State == ExecutionState.InProgress ? project.Policies.WorkInProgressLimit : null,
                    WipUsed = columnItems.Length,
                    Items = columnItems
                };
            })
            .ToArray();

        return new KanbanProjection
        {
            Columns = columns,
            Diagnostics = analysis.Diagnostics
        };
    }

    private static DependencyNetworkProjection BuildDependencyNetwork(CanonicalProject project)
    {
        var nodeMap = new Dictionary<CanonicalWorkItemKey, DependencyNetworkNode>();
        foreach (var workPackage in project.WorkPackages)
        {
            var key = CanonicalWorkItemKey.WorkPackage(workPackage.Id);
            nodeMap.TryAdd(key, new DependencyNetworkNode
            {
                Key = key.ToString(),
                Id = workPackage.Id,
                Kind = "WorkPackage",
                Name = workPackage.Name
            });
        }

        foreach (var card in project.DeliveryCards)
        {
            var key = CanonicalWorkItemKey.DeliveryCard(card.Id);
            nodeMap.TryAdd(key, new DependencyNetworkNode
            {
                Key = key.ToString(),
                Id = card.Id,
                Kind = "DeliveryCard",
                Name = card.Name,
                PlannedDate = card.PlannedStart
            });
        }

        foreach (var milestone in project.Milestones)
        {
            var key = CanonicalWorkItemKey.Milestone(milestone.Id);
            nodeMap.TryAdd(key, new DependencyNetworkNode
            {
                Key = key.ToString(),
                Id = milestone.Id,
                Kind = "Milestone",
                Name = milestone.Name,
                IsMilestone = true,
                PlannedDate = milestone.PlannedDate
            });
        }

        var edges = project.Dependencies
            .OrderBy(dependency => dependency.SubjectKind, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.SubjectId, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.PredecessorKind, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.PredecessorId, StringComparer.Ordinal)
            .Select(dependency =>
            {
                var subjectKey = new CanonicalWorkItemKey(dependency.SubjectKind, dependency.SubjectId);
                var predecessorKey = new CanonicalWorkItemKey(dependency.PredecessorKind, dependency.PredecessorId);
                var subjectExists = nodeMap.ContainsKey(subjectKey);
                var predecessorExists = nodeMap.ContainsKey(predecessorKey);
                var workPackageTraceability = string.Equals(subjectKey.Kind, "WorkPackage", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(predecessorKey.Kind, "WorkPackage", StringComparison.OrdinalIgnoreCase);
                var included = subjectExists
                    && predecessorExists
                    && !workPackageTraceability
                    && dependency.AnalysisEligible
                    && dependency.ValidationState != ValidationState.InvalidSourceEvidence
                    && dependency.DependencyType == DependencyType.FinishToStart;
                var reason = !subjectExists || !predecessorExists
                    ? dependency.ValidationState == ValidationState.InvalidSourceEvidence ? "INVALID_SOURCE_EVIDENCE" : "MISSING_NODE"
                    : dependency.ValidationState == ValidationState.InvalidSourceEvidence
                        ? "INVALID_SOURCE_EVIDENCE"
                        : workPackageTraceability
                            ? "WORK_PACKAGE_TRACEABILITY"
                        : !dependency.AnalysisEligible
                            ? "NOT_ANALYSIS_ELIGIBLE"
                            : dependency.DependencyType != DependencyType.FinishToStart
                                ? "UNSUPPORTED_DEPENDENCY_TYPE"
                                : "INCLUDED";
                return new DependencyNetworkEdge
                {
                    SubjectKind = dependency.SubjectKind,
                    SubjectId = dependency.SubjectId,
                    PredecessorKind = dependency.PredecessorKind,
                    PredecessorId = dependency.PredecessorId,
                    SubjectKey = subjectKey.ToString(),
                    PredecessorKey = predecessorKey.ToString(),
                    DependencyType = dependency.DependencyType,
                    ValidationState = dependency.ValidationState,
                    IncludedInAnalysis = included,
                    Reason = reason
                };
            })
            .ToArray();

        return new DependencyNetworkProjection
        {
            Nodes = nodeMap.Values
                .OrderBy(node => node.Kind, StringComparer.Ordinal)
                .ThenBy(node => node.Id, StringComparer.Ordinal)
                .ToArray(),
            Edges = edges
        };
    }

    private static CpmProjection BuildCpm(CanonicalProject project, ManagementAnalysis analysis)
    {
        var names = project.DeliveryCards.ToDictionary(
            card => CanonicalWorkItemKey.DeliveryCard(card.Id),
            card => (card.Name, IsMilestone: false));
        foreach (var milestone in project.Milestones)
        {
            names[CanonicalWorkItemKey.Milestone(milestone.Id)] = (milestone.Name, IsMilestone: true);
        }

        return new CpmProjection
        {
            State = analysis.CpmState,
            CalculatedFinish = analysis.CalculatedFinish,
            CriticalPathIds = analysis.CriticalPathIds,
            Rows = analysis.CpmNodes
                .OrderBy(node => node.NodeId, StringComparer.Ordinal)
                .Select(node => new CpmProjectionRow
                {
                    NodeKind = node.NodeKind,
                    NodeId = node.NodeId,
                    Name = names.TryGetValue(new CanonicalWorkItemKey(node.NodeKind, node.NodeId), out var name) ? name.Name : node.NodeId,
                    IsMilestone = names.TryGetValue(new CanonicalWorkItemKey(node.NodeKind, node.NodeId), out name) && name.IsMilestone,
                    EarliestStartWorkingMinutes = node.EarliestStartWorkingMinutes,
                    EarliestFinishWorkingMinutes = node.EarliestFinishWorkingMinutes,
                    LatestStartWorkingMinutes = node.LatestStartWorkingMinutes,
                    LatestFinishWorkingMinutes = node.LatestFinishWorkingMinutes,
                    FloatWorkingMinutes = node.FloatWorkingMinutes,
                    IsCritical = node.IsCritical,
                    CalculatedStart = node.CalculatedStart,
                    CalculatedFinish = node.CalculatedFinish
                })
                .ToArray()
        };
    }

    private static DashboardProjection BuildDashboard(CanonicalProject project, ManagementAnalysis analysis)
    {
        var counts = analysis.ExecutionStatus;

        return new DashboardProjection
        {
            TotalCards = counts.Total,
            Completed = counts.Completed,
            InProgress = counts.InProgress,
            NotStarted = counts.NotStarted,
            Suspended = counts.Suspended,
            Cancelled = counts.Cancelled,
            LateToStart = counts.LateToStart,
            Overdue = counts.Overdue,
            CompletedLate = counts.CompletedLate,
            AtRisk = counts.AtRisk,
            BaselineFinish = analysis.BaselineFinish,
            CpmFinish = analysis.CalculatedFinish,
            ForecastFinish = analysis.ForecastFinish,
            ForecastState = analysis.ForecastState,
            Summaries = analysis.ViewSummaries
        };
    }
}
