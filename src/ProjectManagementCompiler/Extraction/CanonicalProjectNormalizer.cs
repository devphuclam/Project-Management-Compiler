using System.Globalization;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Extraction;

public sealed class CanonicalProjectNormalizer
{
    public CanonicalProject Normalize(ExtractedPlan extracted)
    {
        var warnings = extracted.Warnings.ToList();
        var phases = extracted.Phases
            .Select(phase => phase with
            {
                Id = NormalizeId(phase.Id),
                PhaseId = NormalizeId(phase.PhaseId),
                MilestoneIds = phase.MilestoneIds.Select(NormalizeId).OrderBy(id => id, StringComparer.Ordinal).ToArray()
            })
            .OrderBy(phase => phase.Id, StringComparer.Ordinal)
            .ToArray();
        var phaseIds = phases.Select(phase => phase.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var workPackages = extracted.WorkPackages
            .Select(workPackage => workPackage with
            {
                Id = NormalizeId(workPackage.Id),
                ParentId = NormalizeOptionalId(workPackage.ParentId),
                PhaseId = NormalizeId(workPackage.PhaseId),
                DependencyIds = workPackage.DependencyIds.Select(NormalizeId).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                DeliveryCardIds = workPackage.DeliveryCardIds.Select(NormalizeId).OrderBy(id => id, StringComparer.Ordinal).ToArray()
            })
            .OrderBy(workPackage => PhaseOrder(workPackage.PhaseId, phases), StringComparer.Ordinal)
            .ThenBy(workPackage => workPackage.Id, StringComparer.Ordinal)
            .ToArray();
        var workPackageIds = workPackages.Select(workPackage => workPackage.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cards = extracted.DeliveryCards
            .Select(card => card with
            {
                Id = NormalizeId(card.Id),
                ParentId = NormalizeId(card.ParentId),
                PhaseId = NormalizeId(card.PhaseId),
                WorkPackageId = NormalizeId(card.WorkPackageId),
                RoleAssignmentIds = card.RoleAssignmentIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()
            })
            .OrderBy(card => card.Id, StringComparer.Ordinal)
            .ToArray();

        var cardsByParent = cards
            .GroupBy(card => card.WorkPackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(card => card.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray(), StringComparer.OrdinalIgnoreCase);
        workPackages = workPackages
            .Select(workPackage => workPackage with
            {
                DeliveryCardIds = cardsByParent.TryGetValue(workPackage.Id, out var cardIds) ? cardIds : Array.Empty<string>()
            })
            .ToArray();

        var milestones = extracted.Milestones
            .Select(milestone => milestone with
            {
                Id = NormalizeId(milestone.Id),
                ParentId = NormalizeOptionalId(milestone.ParentId),
                DependencyIds = milestone.DependencyIds.Select(NormalizeId).OrderBy(id => id, StringComparer.Ordinal).ToArray()
            })
            .OrderBy(milestone => milestone.PlannedDate)
            .ThenBy(milestone => milestone.Id, StringComparer.Ordinal)
            .ToArray();
        var milestoneIdsByPhase = milestones
            .Where(milestone => milestone.ParentId is not null)
            .GroupBy(milestone => milestone.ParentId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(milestone => milestone.Id).ToArray(), StringComparer.OrdinalIgnoreCase);
        phases = phases.Select(phase => phase with
        {
            MilestoneIds = milestoneIdsByPhase.TryGetValue(phase.Id, out var milestoneIds) ? milestoneIds : Array.Empty<string>()
        }).ToArray();

        var dependencies = extracted.Dependencies
            .Select(dependency => dependency with
            {
                SubjectId = NormalizeId(dependency.SubjectId),
                PredecessorId = NormalizeId(dependency.PredecessorId),
                SubjectKind = dependency.SubjectKind.Trim(),
                PredecessorKind = dependency.PredecessorKind.Trim()
            })
            .OrderBy(dependency => dependency.SubjectId, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.PredecessorId, StringComparer.Ordinal)
            .ToArray();

        foreach (var workPackage in workPackages)
        {
            if (!phaseIds.Contains(workPackage.PhaseId))
            {
                warnings.Add(new ImportWarning
                {
                    Id = $"MISSING_PHASE_REFERENCE:{workPackage.Id}:{workPackage.PhaseId}",
                    Severity = WarningSeverity.Error,
                    Code = "MISSING_PHASE_REFERENCE",
                    Message = $"Work package '{workPackage.Id}' names missing phase '{workPackage.PhaseId}'.",
                    AffectedIds = [workPackage.Id, workPackage.PhaseId],
                    SourceReferences = workPackage.SourceReferences
                });
            }
        }

        foreach (var card in cards)
        {
            if (!workPackageIds.Contains(card.WorkPackageId))
            {
                warnings.Add(new ImportWarning
                {
                    Id = $"MISSING_WORK_PACKAGE_REFERENCE:{card.Id}:{card.WorkPackageId}",
                    Severity = WarningSeverity.Error,
                    Code = "MISSING_WORK_PACKAGE_REFERENCE",
                    Message = $"Delivery card '{card.Id}' names missing work package '{card.WorkPackageId}'.",
                    AffectedIds = [card.Id, card.WorkPackageId],
                    SourceReferences = card.SourceReferences
                });
            }
        }

        foreach (var workPackage in workPackages)
        {
            var detailHours = cards.Where(card => card.WorkPackageId == workPackage.Id).Sum(card => card.PlannedEffortHours ?? 0m);
            if (workPackage.PlannedEffortHours is not null && detailHours != workPackage.PlannedEffortHours.Value)
            {
                warnings.Add(new ImportWarning
                {
                    Id = $"EFFORT_RECONCILIATION:{workPackage.Id}",
                    Severity = WarningSeverity.Warning,
                    Code = "EFFORT_RECONCILIATION",
                    Message = $"Delivery-card effort for '{workPackage.Id}' is {detailHours.ToString(CultureInfo.InvariantCulture)} hours while Appendix A authorizes {workPackage.PlannedEffortHours.Value.ToString(CultureInfo.InvariantCulture)} hours; the parent remains authoritative.",
                    AffectedIds = [workPackage.Id],
                    SourceReferences = workPackage.SourceReferences.Concat(cards.Where(card => card.WorkPackageId == workPackage.Id).SelectMany(card => card.SourceReferences)).ToArray()
                });
            }
        }

        var source = new ProjectSource
        {
            Id = extracted.SourceId,
            Kind = "repository",
            Repository = extracted.Repository,
            ResolvedRef = extracted.ResolvedRef,
            CaptureState = CaptureState.Known,
            CapturedAtUtc = extracted.CapturedAtUtc,
            Documents = extracted.SourceDocuments
        };

        var provenance = extracted.ValueProvenance.Select(value => value.SourceReference)
            .Concat(phases.SelectMany(phase => phase.SourceReferences))
            .Concat(workPackages.SelectMany(workPackage => workPackage.SourceReferences))
            .Concat(cards.SelectMany(card => card.SourceReferences))
            .Concat(milestones.SelectMany(milestone => milestone.SourceReferences))
            .Concat(dependencies.SelectMany(dependency => dependency.SourceReferences))
            .Concat(extracted.ResponsibilityRoles.SelectMany(role => role.SourceReferences))
            .Concat(extracted.Assignments.SelectMany(assignment => assignment.SourceReferences))
            .Concat(extracted.Reserve.SourceReferences)
            .Where(reference => reference.RelativeFile.Length > 0)
            .GroupBy(ReferenceKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(reference => reference.RelativeFile, StringComparer.Ordinal)
            .ThenBy(reference => reference.Table, StringComparer.Ordinal)
            .ThenBy(reference => reference.Item, StringComparer.Ordinal)
            .ToArray();

        return new CanonicalProject
        {
            Project = extracted.Project,
            Sources = source.Id.Length == 0 ? Array.Empty<ProjectSource>() : [source],
            Baseline = extracted.Baseline,
            Phases = phases,
            WorkPackages = workPackages,
            DeliveryCards = cards,
            Milestones = milestones,
            Dependencies = dependencies,
            ResponsibilityRoles = extracted.ResponsibilityRoles.OrderBy(role => role.Code, StringComparer.Ordinal).ToArray(),
            Assignments = extracted.Assignments.OrderBy(assignment => assignment.WorkItemId, StringComparer.Ordinal).ToArray(),
            Capacity = extracted.Capacity,
            Reserve = extracted.Reserve,
            Policies = extracted.Policies,
            Provenance = provenance,
            Warnings = warnings.OrderBy(warning => warning.Id, StringComparer.Ordinal).ToArray()
        };
    }

    private static string NormalizeId(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;
    private static string? NormalizeOptionalId(string? value) => string.IsNullOrWhiteSpace(value) ? null : NormalizeId(value);
    private static string ReferenceKey(SourceReference reference) => string.Join("|", reference.SourceId, reference.RelativeFile, reference.Section, reference.Table, reference.Item, reference.ExtractionRule);

    private static string PhaseOrder(string phaseId, IReadOnlyList<Phase> phases) =>
        $"{phases.Select(phase => phase.Id).ToList().IndexOf(phaseId):D3}";
}
