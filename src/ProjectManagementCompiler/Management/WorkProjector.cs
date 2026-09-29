using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed class WorkProjector
{
    private static readonly HashSet<string> SupportedAttentionCodes = new(StringComparer.Ordinal)
    {
        "START_DELAY",
        "OVERDUE",
        "SUSPENDED",
        "AT_RISK"
    };

    private static readonly IComparer<IReadOnlyList<string>> ReasonIdsComparer = new OrdinalStringSequenceComparer();

    public WorkProjection Build(CanonicalProject project, ManagementAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(analysis);

        var phasePositions = UniquePositions(project.Phases, phase => phase.Id);
        var packagePositions = UniquePositions(project.WorkPackages, package => package.Id);
        var cardGroups = project.DeliveryCards
            .Select((card, index) => new { Card = card, Index = index })
            .GroupBy(item => item.Card.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var cardsById = cardGroups
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().Card, StringComparer.OrdinalIgnoreCase);
        var packageCards = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var cardOrders = new Dictionary<string, WorkCardOrder>(StringComparer.OrdinalIgnoreCase);
        foreach (var (package, packageIndex) in project.WorkPackages.Select((package, index) => (package, index)))
        {
            var linkedCardIds = new List<string>();
            if (packagePositions.ContainsKey(package.Id) && HasValidPhaseParent(package, phasePositions))
            {
                foreach (var (cardId, cardIndex) in package.DeliveryCardIds.Select((id, index) => (id, index)))
                {
                    if (!cardsById.TryGetValue(cardId, out var card)
                        || !HasValidPackageParent(card, package)
                        || cardOrders.ContainsKey(card.Id))
                    {
                        continue;
                    }

                    linkedCardIds.Add(card.Id);
                    cardOrders[card.Id] = new WorkCardOrder
                    {
                        Phase = phasePositions[package.PhaseId],
                        WorkPackage = packageIndex,
                        Card = cardIndex
                    };
                }
            }

            packageCards[package.Id] = linkedCardIds;
        }

        var attentionByCard = BuildAttentionByCard(analysis);

        var cards = project.DeliveryCards
            .Select((card, sourceIndex) => ToCard(project, card, cardOrders, attentionByCard, sourceIndex))
            .OrderBy(card => card.Order.Phase ?? int.MaxValue)
            .ThenBy(card => card.Order.WorkPackage ?? int.MaxValue)
            .ThenBy(card => card.Order.Card)
            .ThenBy(card => card.Key.Id, StringComparer.Ordinal)
            .ToArray();

        return new WorkProjection
        {
            CurrentPhaseId = FindCurrentPhase(project),
            Phases = project.Phases
                .Select((phase, index) => new WorkPhaseProjection
                {
                    Id = phase.Id,
                    Name = ReaderFacingTextPolicy.CleanName(phase.Name, phase.Id),
                    Order = index,
                    WorkPackageIds = project.WorkPackages
                        .Where(package => packagePositions.ContainsKey(package.Id)
                            && HasValidPhaseParent(package, phasePositions)
                            && string.Equals(package.PhaseId, phase.Id, StringComparison.OrdinalIgnoreCase))
                        .Select(package => package.Id)
                        .ToArray()
                })
                .ToArray(),
            WorkPackages = project.WorkPackages
                .Select((package, index) => new WorkPackageProjection
                {
                    Id = package.Id,
                    PhaseId = package.PhaseId,
                    Name = ReaderFacingTextPolicy.CleanName(package.Name, package.Id),
                    Order = index,
                    DeliveryCardIds = packageCards.TryGetValue(package.Id, out var ids) ? ids : Array.Empty<string>()
                })
                .ToArray(),
            Cards = cards
        };
    }

    private static WorkCardProjection ToCard(
        CanonicalProject project,
        DeliveryCard card,
        IReadOnlyDictionary<string, WorkCardOrder> cardOrders,
        IReadOnlyDictionary<string, IReadOnlyList<WorkAttentionProjection>> attentionByCard,
        int sourceIndex) => new()
    {
        Key = CanonicalWorkItemKey.DeliveryCard(card.Id),
        Name = ReaderFacingTextPolicy.CleanName(card.Name, card.Id),
        PhaseId = card.PhaseId,
        WorkPackageId = card.WorkPackageId,
        Order = cardOrders.TryGetValue(card.Id, out var order)
            ? order
            : new WorkCardOrder { Card = sourceIndex },
        PlannedStart = ValidDate(card.PlannedStart) ? card.PlannedStart : null,
        PlannedFinish = ValidDate(card.PlannedFinish) ? card.PlannedFinish : null,
        PlannedEffortHours = card.PlannedEffortHours,
        PlannedEffortState = card.PlannedEffortState,
        Execution = BuildExecution(project, card.Id),
        Roles = BuildRoles(project, card.Id),
        Attention = attentionByCard.TryGetValue(card.Id, out var attention) ? attention : Array.Empty<WorkAttentionProjection>(),
        SourceReferences = card.SourceReferences
    };

    private static WorkExecutionProjection BuildExecution(CanonicalProject project, string cardId)
    {
        var record = ExecutionTruthResolver.ForCard(project, cardId);
        var isRecorded = record?.IsRecorded == true;
        return new WorkExecutionProjection
        {
            Recorded = isRecorded,
            State = isRecorded ? record!.ExecutionState : null,
            ResultState = isRecorded ? record!.ResultState : null,
            ActualStart = isRecorded ? record!.ActualStart : null,
            ActualFinish = isRecorded ? record!.ActualFinish : null,
            ActualEffortHours = isRecorded ? record!.ActualEffortHours : null,
            RemainingEffortHours = isRecorded ? record!.RemainingEffortHours : null,
            LastUpdatedAt = isRecorded ? record!.LastUpdatedAt : null
        };
    }

    private static IReadOnlyList<WorkRoleProjection> BuildRoles(CanonicalProject project, string cardId) => project.Assignments
        .Where(assignment => string.Equals(assignment.WorkItemId, cardId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(assignment.CarioRoleCode, "A", StringComparison.OrdinalIgnoreCase))
        .Select(assignment => new WorkRoleProjection
        {
            Label = ResolveRoleLabel(project, assignment.LogicalRoleCode),
            Person = string.Equals(assignment.MappingStatus, "MAPPED", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(assignment.ConcreteIdentity)
                    ? assignment.ConcreteIdentity.Trim()
                    : null
        })
        .Where(role => !string.IsNullOrWhiteSpace(role.Label))
        .Distinct()
        .OrderBy(role => role.Label, StringComparer.Ordinal)
        .ThenBy(role => role.Person, StringComparer.Ordinal)
        .ToArray();

    private static string ResolveRoleLabel(CanonicalProject project, string roleCode)
    {
        var mapped = ExecutiveProgressReportProjector.RoleLabel(roleCode);
        if (!string.IsNullOrWhiteSpace(mapped))
        {
            return mapped;
        }

        var sourceRole = project.ResponsibilityRoles.FirstOrDefault(role =>
            string.Equals(role.Code, roleCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role.LogicalRoleCode, roleCode, StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrWhiteSpace(sourceRole?.SourceMeaning)
            ? ReaderFacingTextPolicy.CleanName(sourceRole.SourceMeaning, sourceRole.Code)
            : ReaderFacingTextPolicy.CleanName(roleCode);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<WorkAttentionProjection>> BuildAttentionByCard(ManagementAnalysis analysis)
    {
        var eligible = analysis.Alerts
            .Where(alert => SupportedAttentionCodes.Contains(alert.AlertCode)
                && ProjectOverviewProjector.TryGetSupportedAttentionConsequence(alert.AlertCode, out _))
            .Select(alert =>
            {
                ProjectOverviewProjector.TryGetSupportedAttentionConsequence(alert.AlertCode, out var consequence);
                return new AlertWithConsequence(
                    alert,
                    consequence,
                    alert.ReasonWorkItemIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
            });

        return eligible
            .GroupBy(item => item.Alert.WorkItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<WorkAttentionProjection>)group
                    .OrderBy(item => item.Alert.AlertCode, StringComparer.Ordinal)
                    .ThenBy(item => item.Alert.DerivedAt)
                    .ThenBy(item => item.SortedReasonWorkItemIds, ReasonIdsComparer)
                    .Select(item => new WorkAttentionProjection
                    {
                        Code = item.Alert.AlertCode,
                        Consequence = item.Consequence
                    })
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string? FindCurrentPhase(CanonicalProject project)
    {
        var reportingDate = project.ImportMetadata is { Classification: ManifestImportClassification.OfficialCommit }
            ? project.ImportMetadata.RegisterStatusDate
            : null;
        if (reportingDate is null)
        {
            return null;
        }

        var matches = project.Phases
            .Where(phase => ValidDate(phase.PlannedStart)
                && ValidDate(phase.PlannedFinish)
                && phase.PlannedStart!.Value <= phase.PlannedFinish!.Value
                && phase.PlannedStart.Value <= reportingDate.Value
                && reportingDate.Value <= phase.PlannedFinish.Value)
            .ToArray();
        return matches.Length == 1 ? matches[0].Id : null;
    }

    private static Dictionary<string, int> UniquePositions<T>(
        IReadOnlyList<T> items,
        Func<T, string> id) => items
        .Select((item, index) => new { Item = item, Index = index, Id = id(item) })
        .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() == 1)
        .ToDictionary(group => group.Key, group => group.Single().Index, StringComparer.OrdinalIgnoreCase);

    private static bool HasValidPhaseParent(WorkPackage package, IReadOnlyDictionary<string, int> phasePositions) =>
        !string.IsNullOrWhiteSpace(package.ParentId)
        && string.Equals(package.ParentId, package.PhaseId, StringComparison.OrdinalIgnoreCase)
        && phasePositions.ContainsKey(package.PhaseId);

    private static bool HasValidPackageParent(DeliveryCard card, WorkPackage package) =>
        !string.IsNullOrWhiteSpace(card.ParentId)
        && string.Equals(card.ParentId, package.Id, StringComparison.OrdinalIgnoreCase)
        && string.Equals(card.WorkPackageId, package.Id, StringComparison.OrdinalIgnoreCase)
        && string.Equals(card.PhaseId, package.PhaseId, StringComparison.OrdinalIgnoreCase);

    private static bool ValidDate(DateOnly? date) => date is not null && date != DateOnly.MinValue;

    private sealed record AlertWithConsequence(
        Alert Alert,
        string Consequence,
        IReadOnlyList<string> SortedReasonWorkItemIds);

    private sealed class OrdinalStringSequenceComparer : IComparer<IReadOnlyList<string>>
    {
        public int Compare(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;

            var sharedCount = Math.Min(left.Count, right.Count);
            for (var index = 0; index < sharedCount; index++)
            {
                var comparison = StringComparer.Ordinal.Compare(left[index], right[index]);
                if (comparison != 0) return comparison;
            }

            return left.Count.CompareTo(right.Count);
        }
    }
}
