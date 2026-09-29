using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed class WorkProjector
{
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

        var cards = project.DeliveryCards
            .Select((card, sourceIndex) => ToCard(card, cardOrders, sourceIndex))
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
        DeliveryCard card,
        IReadOnlyDictionary<string, WorkCardOrder> cardOrders,
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
        SourceReferences = card.SourceReferences
    };

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
}
