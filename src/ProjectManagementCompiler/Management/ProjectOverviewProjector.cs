using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed class ProjectOverviewProjector
{
    private const int MaximumAttentionItems = 3;

    private static readonly IReadOnlyDictionary<string, string> Consequences =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["START_DELAY"] = "Chưa bắt đầu đúng kế hoạch.",
            ["OVERDUE"] = "Đang kéo dài quá ngày dự kiến.",
            ["SUSPENDED"] = "Đang tạm dừng.",
            ["AT_RISK"] = "Có nguy cơ chậm do công việc trước bị trễ."
        };

    internal static bool TryGetSupportedAttentionConsequence(string code, out string consequence) =>
        Consequences.TryGetValue(code, out consequence!);

    public ProjectOverviewProjection Build(CanonicalProject project, ManagementAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(analysis);

        var reportingDate = project.ImportMetadata?.RegisterStatusDate;
        return new ProjectOverviewProjection
        {
            Project = new ProjectOverviewIdentity
            {
                Name = ReaderFacingTextPolicy.CleanName(project.Project.Name, project.Project.Id),
                ReportingDate = reportingDate,
                SourceIdentity = project.ImportMetadata?.SourceIdentity ?? string.Empty
            },
            CurrentPhase = BuildCurrentPhase(project.Phases, reportingDate),
            Progress = new ProgressCoverageProjector().Build(project, analysis),
            NextControlPoint = BuildNextControlPoint(project.Milestones, reportingDate),
            AttentionItems = BuildAttentionItems(project, analysis.Alerts)
        };
    }

    private static ProjectOverviewPhase BuildCurrentPhase(
        IReadOnlyList<Phase> phases,
        DateOnly? reportingDate)
    {
        if (reportingDate is null)
        {
            return new ProjectOverviewPhase();
        }

        var matches = phases
            .Where(phase => phase.PlannedStart is not null
                && phase.PlannedFinish is not null
                && phase.PlannedStart <= phase.PlannedFinish
                && phase.PlannedStart <= reportingDate
                && reportingDate <= phase.PlannedFinish)
            .ToArray();
        if (matches.Length != 1)
        {
            return new ProjectOverviewPhase();
        }

        var current = matches[0];
        return new ProjectOverviewPhase
        {
            Name = ReaderFacingTextPolicy.CleanName(current.Name, current.Id),
            PlannedStart = current.PlannedStart,
            PlannedFinish = current.PlannedFinish,
            State = DataState.Known
        };
    }

    private static ProjectOverviewControlPoint? BuildNextControlPoint(
        IReadOnlyList<MilestoneDecision> milestones,
        DateOnly? reportingDate)
    {
        if (reportingDate is null)
        {
            return null;
        }

        var candidate = milestones
            .Select((milestone, sourceIndex) => new { Milestone = milestone, SourceIndex = sourceIndex })
            .Where(item => item.Milestone.PlannedDate is not null
                && item.Milestone.PlannedDate > reportingDate
                && item.Milestone.State != ExecutionState.Completed)
            .OrderBy(item => item.Milestone.PlannedDate)
            .ThenBy(item => item.SourceIndex)
            .ThenBy(item => item.Milestone.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        return candidate is null
            ? null
            : new ProjectOverviewControlPoint
            {
                Name = ReaderFacingTextPolicy.CleanName(candidate.Milestone.Name, candidate.Milestone.Id),
                PlannedDate = candidate.Milestone.PlannedDate!.Value,
                Kind = candidate.Milestone.Kind,
                SourceState = candidate.Milestone.State
            };
    }

    private static IReadOnlyList<ProjectOverviewAttentionItem> BuildAttentionItems(
        CanonicalProject project,
        IReadOnlyList<Alert> alerts)
    {
        var cards = project.DeliveryCards.ToDictionary(card => card.Id, StringComparer.OrdinalIgnoreCase);
        return alerts
            .Where(alert => Consequences.ContainsKey(alert.AlertCode)
                && cards.ContainsKey(alert.WorkItemId))
            .OrderByDescending(alert => alert.Severity)
            .ThenBy(alert => alert.DerivedAt)
            .ThenBy(alert => alert.WorkItemId, StringComparer.Ordinal)
            .ThenBy(alert => alert.AlertCode, StringComparer.Ordinal)
            .Take(MaximumAttentionItems)
            .Select(alert => new ProjectOverviewAttentionItem
            {
                WorkItemId = alert.WorkItemId,
                WorkItemName = ReaderFacingTextPolicy.CleanName(cards[alert.WorkItemId].Name, alert.WorkItemId),
                Consequence = Consequences[alert.AlertCode],
                Severity = alert.Severity,
                Destination = "gantt"
            })
            .ToArray();
    }
}
