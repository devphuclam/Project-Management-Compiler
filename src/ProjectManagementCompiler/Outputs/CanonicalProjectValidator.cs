using ProjectManagementCompiler.Domain;

using System.Globalization;

namespace ProjectManagementCompiler.Outputs;

public static class CanonicalProjectValidator
{
    private static readonly HashSet<string> CarioRoleCodes =
        ["A", "R+", "R", "C", "I", "O"];

    public static IReadOnlyList<ImportWarning> Validate(CanonicalProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var diagnostics = new List<ImportWarning>();
        var sourceIds = ValidateSources(project, diagnostics);
        ValidateProject(diagnostics, project, sourceIds);

        var phaseIds = ValidatePhases(project, diagnostics);
        var workPackageIds = ValidateWorkPackages(project, diagnostics, phaseIds);
        var cardIds = ValidateDeliveryCards(project, diagnostics, phaseIds, workPackageIds);
        var milestoneIds = ValidateMilestones(project, diagnostics, phaseIds, workPackageIds);

        ValidateHierarchyBackReferences(project, diagnostics, cardIds, milestoneIds);
        ValidateRolesAndAssignments(project, diagnostics, cardIds);
        ValidateDependencies(project, diagnostics, cardIds, workPackageIds, milestoneIds);
        ValidateSourceReferences(project, diagnostics, sourceIds);
        ValidateManagementEvidence(project, diagnostics, sourceIds);
        ValidateOverlay(project, diagnostics, cardIds);
        ValidateV2AuthorityData(project, diagnostics, cardIds);
        if (!string.Equals(project.SchemaVersion, "2.0", StringComparison.Ordinal)
            && project.ExecutionProposals.Count > 0)
        {
            ValidateExecutionProposals(project, null, diagnostics, cardIds);
        }
        ValidatePoliciesAndMeasures(project, diagnostics);

        return diagnostics;
    }

    private static HashSet<string> ValidateSources(CanonicalProject project, ICollection<ImportWarning> diagnostics)
    {
        var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in project.Sources)
        {
            var sourceId = source.Id?.Trim() ?? string.Empty;
            if (sourceId.Length == 0)
            {
                Add(diagnostics, "INVALID_SOURCE_ID", "Every project source requires a non-empty ID.", null);
            }
            else if (!sourceIds.Add(sourceId))
            {
                Add(diagnostics, "DUPLICATE_SOURCE_ID", $"Project source '{sourceId}' is declared more than once.", sourceId);
            }

            foreach (var document in source.Documents)
            {
                var documentId = document.Id?.Trim() ?? string.Empty;
                if (documentId.Length == 0)
                {
                    Add(diagnostics, "INVALID_SOURCE_DOCUMENT_ID", "Every source document requires a non-empty ID.", sourceId);
                }
                else if (!documentIds.Add(documentId))
                {
                    Add(diagnostics, "DUPLICATE_SOURCE_DOCUMENT_ID", $"Source document '{documentId}' is declared more than once.", documentId);
                }
            }
        }

        return sourceIds;
    }

    private static void ValidateProject(
        ICollection<ImportWarning> diagnostics,
        CanonicalProject project,
        ISet<string> sourceIds)
    {
        if (string.IsNullOrWhiteSpace(project.SchemaVersion))
        {
            Add(diagnostics, "INVALID_SCHEMA_VERSION", "Canonical project schemaVersion is required.", null);
        }

        if (string.IsNullOrWhiteSpace(project.Project.Id))
        {
            Add(diagnostics, "INVALID_PROJECT_ID", "Canonical project requires a non-empty project ID.", null);
        }

        if (project.Project.SourceIds.Count == 0)
        {
            Add(diagnostics, "INVALID_PROJECT_SOURCE", "Canonical project must identify at least one captured source.", project.Project.Id);
        }

        foreach (var sourceId in project.Project.SourceIds)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || !sourceIds.Contains(sourceId))
            {
                Add(diagnostics, "INVALID_PROJECT_SOURCE", $"Project source '{sourceId}' does not resolve to a captured source.", project.Project.Id);
            }
        }

        ValidateDate(diagnostics, project.Project.TargetDate, "INVALID_PROJECT_DATE", project.Project.Id, "project target date");

        if (string.IsNullOrWhiteSpace(project.Baseline.Id))
        {
            Add(diagnostics, "INVALID_BASELINE_ID", "Baseline requires a non-empty ID.", null);
        }

        if (string.IsNullOrWhiteSpace(project.Baseline.Version))
        {
            Add(diagnostics, "INVALID_BASELINE_VERSION", "Baseline requires a non-empty version.", project.Baseline.Id);
        }

        if (string.IsNullOrWhiteSpace(project.Baseline.Status))
        {
            Add(diagnostics, "INVALID_BASELINE_STATUS", "Baseline requires an explicit status.", project.Baseline.Id);
        }

        if (!Enum.IsDefined(project.Baseline.ValidationState))
        {
            Add(diagnostics, "INVALID_BASELINE_VALIDATION_STATE", "Baseline validationState is unsupported.", project.Baseline.Id);
        }

        var documentIds = project.Sources
            .SelectMany(source => source.Documents)
            .Where(document => !string.IsNullOrWhiteSpace(document.Id))
            .Select(document => document.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(project.Baseline.AuthorityDocumentId)
            || !documentIds.Contains(project.Baseline.AuthorityDocumentId))
        {
            Add(diagnostics, "INVALID_BASELINE_AUTHORITY", "Baseline authorityDocumentId must resolve to a captured source document.", project.Baseline.Id);
        }

        ValidateDate(diagnostics, project.Baseline.PlanningStart, "INVALID_BASELINE_DATE", project.Baseline.Id, "baseline planning start");
        ValidateDate(diagnostics, project.Baseline.PlanningFinish, "INVALID_BASELINE_DATE", project.Baseline.Id, "baseline planning finish");
        ValidateDate(diagnostics, project.Baseline.TargetDate, "INVALID_BASELINE_DATE", project.Baseline.Id, "baseline target date");
        if (project.Baseline.PlanningStart is not null
            && project.Baseline.PlanningFinish is not null
            && project.Baseline.PlanningFinish < project.Baseline.PlanningStart)
        {
            Add(diagnostics, "INVALID_BASELINE_DATE_RANGE", "Baseline planning finish cannot precede planning start.", project.Baseline.Id);
        }

        if (project.Baseline.TargetDate is not null
            && ((project.Baseline.PlanningStart is not null && project.Baseline.TargetDate < project.Baseline.PlanningStart)
                || (project.Baseline.PlanningFinish is not null && project.Baseline.TargetDate < project.Baseline.PlanningFinish)))
        {
            Add(diagnostics, "INVALID_BASELINE_TARGET_DATE", "Baseline target date cannot precede the planning window.", project.Baseline.Id);
        }

        ValidateNonNegative(diagnostics, project.Baseline.PlannedEffortHours, "INVALID_BASELINE_EFFORT", project.Baseline.Id, "baseline planned effort");
        ValidateNonNegative(diagnostics, project.Baseline.ReserveHours, "INVALID_BASELINE_EFFORT", project.Baseline.Id, "baseline reserve");
        ValidateNonNegative(diagnostics, project.Baseline.CapacityHours, "INVALID_BASELINE_EFFORT", project.Baseline.Id, "baseline capacity");
    }

    private static HashSet<string> ValidatePhases(CanonicalProject project, ICollection<ImportWarning> diagnostics)
    {
        var phaseIds = ValidateIds(project.Phases, phase => phase.Id, "INVALID_PHASE_ID", "DUPLICATE_PHASE_ID", diagnostics);
        foreach (var phase in project.Phases)
        {
            if (string.IsNullOrWhiteSpace(phase.PhaseId) || !string.Equals(phase.Id, phase.PhaseId, StringComparison.OrdinalIgnoreCase))
            {
                Add(diagnostics, "INVALID_PHASE_OWNERSHIP", $"Phase '{phase.Id}' must own itself through phaseId.", phase.Id);
            }

            ValidatePlannedEntityDatesAndMeasures(diagnostics, phase, "phase");
        }

        return phaseIds;
    }

    private static HashSet<string> ValidateWorkPackages(
        CanonicalProject project,
        ICollection<ImportWarning> diagnostics,
        ISet<string> phaseIds)
    {
        var workPackageIds = ValidateIds(project.WorkPackages, workPackage => workPackage.Id, "INVALID_WORK_PACKAGE_ID", "DUPLICATE_WORK_PACKAGE_ID", diagnostics);
        foreach (var workPackage in project.WorkPackages)
        {
            if (!phaseIds.Contains(workPackage.PhaseId))
            {
                Add(diagnostics, "INVALID_WORK_PACKAGE_PHASE", $"Work package '{workPackage.Id}' names missing phase '{workPackage.PhaseId}'.", workPackage.Id);
            }

            if (workPackage.ParentId is not null)
            {
                if (!workPackageIds.Contains(workPackage.ParentId))
                {
                    Add(diagnostics, "INVALID_WORK_PACKAGE_PARENT", $"Work package '{workPackage.Id}' names missing parent '{workPackage.ParentId}'.", workPackage.Id);
                }
                else if (string.Equals(workPackage.Id, workPackage.ParentId, StringComparison.OrdinalIgnoreCase))
                {
                    Add(diagnostics, "INVALID_WORK_PACKAGE_PARENT", $"Work package '{workPackage.Id}' cannot parent itself.", workPackage.Id);
                }
            }

            ValidatePlannedEntityDatesAndMeasures(diagnostics, workPackage, "work package");
        }

        return workPackageIds;
    }

    private static HashSet<string> ValidateDeliveryCards(
        CanonicalProject project,
        ICollection<ImportWarning> diagnostics,
        ISet<string> phaseIds,
        ISet<string> workPackageIds)
    {
        var cardIds = ValidateIds(project.DeliveryCards, card => card.Id, "INVALID_CARD_ID", "DUPLICATE_CARD_ID", diagnostics);
        var workPackages = project.WorkPackages
            .Where(workPackage => !string.IsNullOrWhiteSpace(workPackage.Id))
            .GroupBy(workPackage => workPackage.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var card in project.DeliveryCards)
        {
            if (!workPackageIds.Contains(card.WorkPackageId))
            {
                Add(diagnostics, "INVALID_CARD_WORK_PACKAGE", $"Delivery card '{card.Id}' names missing work package '{card.WorkPackageId}'.", card.Id);
            }
            else if (!string.Equals(workPackages[card.WorkPackageId].PhaseId, card.PhaseId, StringComparison.OrdinalIgnoreCase))
            {
                Add(diagnostics, "INVALID_CARD_PHASE", $"Delivery card '{card.Id}' phase does not agree with its work package.", card.Id);
            }

            if (!phaseIds.Contains(card.PhaseId))
            {
                Add(diagnostics, "INVALID_CARD_PHASE", $"Delivery card '{card.Id}' names missing phase '{card.PhaseId}'.", card.Id);
            }

            if (card.ParentId is not null && !string.Equals(card.ParentId, card.WorkPackageId, StringComparison.OrdinalIgnoreCase))
            {
                Add(diagnostics, "INVALID_CARD_PARENT", $"Delivery card '{card.Id}' parent must be its work package.", card.Id);
            }

            if (card.State is not null && !Enum.IsDefined(card.State.Value))
            {
                Add(diagnostics, "INVALID_CARD_STATE", $"Delivery card '{card.Id}' has an unsupported execution state.", card.Id);
            }

            ValidatePlannedEntityDatesAndMeasures(diagnostics, card, "delivery card");
        }

        return cardIds;
    }

    private static HashSet<string> ValidateMilestones(
        CanonicalProject project,
        ICollection<ImportWarning> diagnostics,
        ISet<string> phaseIds,
        ISet<string> workPackageIds)
    {
        var milestoneIds = ValidateIds(project.Milestones, milestone => milestone.Id, "INVALID_MILESTONE_ID", "DUPLICATE_MILESTONE_ID", diagnostics);
        foreach (var milestone in project.Milestones)
        {
            if (!Enum.IsDefined(milestone.Kind))
            {
                Add(diagnostics, "INVALID_MILESTONE_KIND", $"Milestone '{milestone.Id}' has an unsupported kind.", milestone.Id);
            }

            if (milestone.State is not null && !Enum.IsDefined(milestone.State.Value))
            {
                Add(diagnostics, "INVALID_MILESTONE_STATE", $"Milestone '{milestone.Id}' has an unsupported execution state.", milestone.Id);
            }

            if (milestone.ParentId is not null
                && !phaseIds.Contains(milestone.ParentId)
                && !workPackageIds.Contains(milestone.ParentId))
            {
                Add(diagnostics, "INVALID_MILESTONE_PARENT", $"Milestone '{milestone.Id}' names missing parent '{milestone.ParentId}'.", milestone.Id);
            }

            ValidateDate(diagnostics, milestone.PlannedDate, "INVALID_MILESTONE_DATE", milestone.Id, "milestone planned date");
            ValidateNonNegative(diagnostics, milestone.PlannedEffortHours, "INVALID_MILESTONE_EFFORT", milestone.Id, "milestone planned effort");
            ValidateNonNegative(diagnostics, milestone.PlannedDurationWorkingMinutes, "INVALID_MILESTONE_DURATION", milestone.Id, "milestone planned duration");
        }

        return milestoneIds;
    }

    private static void ValidateHierarchyBackReferences(
        CanonicalProject project,
        ICollection<ImportWarning> diagnostics,
        ISet<string> cardIds,
        ISet<string> milestoneIds)
    {
        var cards = project.DeliveryCards
            .Where(card => !string.IsNullOrWhiteSpace(card.Id))
            .GroupBy(card => card.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var milestones = project.Milestones
            .Where(milestone => !string.IsNullOrWhiteSpace(milestone.Id))
            .GroupBy(milestone => milestone.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var workPackage in project.WorkPackages)
        {
            foreach (var cardId in workPackage.DeliveryCardIds)
            {
                if (!cardIds.Contains(cardId))
                {
                    Add(diagnostics, "INVALID_WORK_PACKAGE_CARD", $"Work package '{workPackage.Id}' names missing delivery card '{cardId}'.", workPackage.Id);
                }
                else if (!string.Equals(cards[cardId].WorkPackageId, workPackage.Id, StringComparison.OrdinalIgnoreCase))
                {
                    Add(diagnostics, "INVALID_WORK_PACKAGE_CARD", $"Delivery card '{cardId}' is assigned to the wrong work package.", workPackage.Id);
                }
            }
        }

        foreach (var phase in project.Phases)
        {
            foreach (var milestoneId in phase.MilestoneIds)
            {
                if (!milestoneIds.Contains(milestoneId))
                {
                    Add(diagnostics, "INVALID_PHASE_MILESTONE", $"Phase '{phase.Id}' names missing milestone '{milestoneId}'.", phase.Id);
                }
                else if (milestones[milestoneId].ParentId is not null
                    && !string.Equals(milestones[milestoneId].ParentId, phase.Id, StringComparison.OrdinalIgnoreCase))
                {
                    Add(diagnostics, "INVALID_PHASE_MILESTONE", $"Milestone '{milestoneId}' is attached to the wrong phase.", phase.Id);
                }
            }
        }
    }

    private static void ValidateRolesAndAssignments(
        CanonicalProject project,
        ICollection<ImportWarning> diagnostics,
        ISet<string> cardIds)
    {
        var roleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var logicalRoleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in project.ResponsibilityRoles)
        {
            if (string.IsNullOrWhiteSpace(role.Code))
            {
                Add(diagnostics, "INVALID_ROLE_CODE", "Responsibility role code is required.", null);
            }
            else if (!roleCodes.Add(role.Code))
            {
                Add(diagnostics, "DUPLICATE_ROLE_CODE", $"Responsibility role '{role.Code}' is declared more than once.", role.Code);
            }

            if (role.LogicalRoleCode is not null && !logicalRoleCodes.Add(role.LogicalRoleCode))
            {
                Add(diagnostics, "DUPLICATE_LOGICAL_ROLE_CODE", $"Logical role '{role.LogicalRoleCode}' is declared more than once.", role.LogicalRoleCode);
            }

            ValidateCarioCode(diagnostics, role.CarioRoleCode, role.Code);
        }

        foreach (var assignment in project.Assignments)
        {
            if (!cardIds.Contains(assignment.WorkItemId))
            {
                Add(diagnostics, "INVALID_ASSIGNMENT_TARGET", $"Assignment target '{assignment.WorkItemId}' must resolve to a DeliveryCard.", assignment.WorkItemId);
            }

            if (string.IsNullOrWhiteSpace(assignment.LogicalRoleCode)
                || !logicalRoleCodes.Contains(assignment.LogicalRoleCode))
            {
                Add(diagnostics, "INVALID_ASSIGNMENT_ROLE", $"Assignment '{assignment.WorkItemId}' names missing logical role '{assignment.LogicalRoleCode}'.", assignment.WorkItemId);
            }

            ValidateCarioCode(diagnostics, assignment.CarioRoleCode, assignment.WorkItemId);
        }
    }

    private static void ValidateDependencies(
        CanonicalProject project,
        ICollection<ImportWarning> diagnostics,
        ISet<string> cardIds,
        ISet<string> workPackageIds,
        ISet<string> milestoneIds)
    {
        var knownItems = new HashSet<CanonicalWorkItemKey>();
        foreach (var id in cardIds) knownItems.Add(CanonicalWorkItemKey.DeliveryCard(id));
        foreach (var id in workPackageIds) knownItems.Add(CanonicalWorkItemKey.WorkPackage(id));
        foreach (var id in milestoneIds) knownItems.Add(CanonicalWorkItemKey.Milestone(id));

        var seen = new HashSet<DependencyIdentity>();
        foreach (var dependency in project.Dependencies)
        {
            var subjectKey = new CanonicalWorkItemKey(dependency.SubjectKind, dependency.SubjectId);
            var predecessorKey = new CanonicalWorkItemKey(dependency.PredecessorKind, dependency.PredecessorId);
            var edgeKey = new DependencyIdentity(subjectKey, predecessorKey, dependency.DependencyType);
            if (!seen.Add(edgeKey))
            {
                Add(diagnostics, "DUPLICATE_DEPENDENCY", $"Dependency '{dependency.SubjectId}' -> '{dependency.PredecessorId}' is duplicated.", dependency.SubjectId);
            }

            if (!knownItems.Contains(subjectKey))
            {
                Add(diagnostics, "INVALID_DEPENDENCY_SUBJECT", $"Dependency subject '{dependency.SubjectId}' does not resolve to a canonical work item.", dependency.SubjectId);
            }

            var predecessorExists = !string.Equals(CanonicalWorkItemKey.NormalizeKind(dependency.PredecessorKind), "Unknown", StringComparison.OrdinalIgnoreCase)
                && knownItems.Contains(predecessorKey);
            if (subjectKey == predecessorKey)
            {
                Add(diagnostics, "INVALID_DEPENDENCY_SELF", $"Dependency '{dependency.SubjectId}' cannot depend on itself.", dependency.SubjectId);
            }

            if (predecessorExists)
            {
                if (dependency.ValidationState == ValidationState.InvalidSourceEvidence)
                {
                    Add(diagnostics, "INVALID_DEPENDENCY_SOURCE_STATE", $"Known predecessor '{dependency.PredecessorId}' cannot be marked as invalid source evidence.", dependency.SubjectId);
                }
            }
            else
            {
                if (dependency.ValidationState == ValidationState.InvalidSourceEvidence && dependency.AnalysisEligible)
                {
                    Add(diagnostics, "INVALID_DEPENDENCY_ANALYSIS_STATE", $"Dependency '{dependency.SubjectId}' -> '{dependency.PredecessorId}' is invalid source evidence and cannot be analysis eligible.", dependency.SubjectId);
                }

                if (dependency.ValidationState != ValidationState.InvalidSourceEvidence || dependency.AnalysisEligible)
                {
                    Add(diagnostics, "INVALID_DEPENDENCY_PREDECESSOR", $"Unresolved predecessor '{dependency.PredecessorId}' must be invalid source evidence and analysis-ineligible.", dependency.SubjectId);
                }

                if (!string.Equals(dependency.PredecessorKind, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    Add(diagnostics, "INVALID_DEPENDENCY_PREDECESSOR_KIND", $"Unresolved predecessor '{dependency.PredecessorId}' must use kind 'Unknown'.", dependency.SubjectId);
                }
            }

            if (dependency.ValidationState == ValidationState.InvalidSourceEvidence && dependency.AnalysisEligible && predecessorExists)
            {
                Add(diagnostics, "INVALID_DEPENDENCY_ANALYSIS_STATE", $"Dependency '{dependency.SubjectId}' -> '{dependency.PredecessorId}' is invalid source evidence and cannot be analysis eligible.", dependency.SubjectId);
            }

            if (!Enum.IsDefined(dependency.DependencyType))
            {
                Add(diagnostics, "INVALID_DEPENDENCY_TYPE", $"Dependency '{dependency.SubjectId}' has an unsupported dependency type.", dependency.SubjectId);
            }

            if (!Enum.IsDefined(dependency.ValidationState))
            {
                Add(diagnostics, "INVALID_DEPENDENCY_VALIDATION_STATE", $"Dependency '{dependency.SubjectId}' has an unsupported validation state.", dependency.SubjectId);
            }
        }
    }

    private readonly record struct DependencyIdentity(
        CanonicalWorkItemKey Subject,
        CanonicalWorkItemKey Predecessor,
        DependencyType DependencyType);

    private static void ValidateSourceReferences(
        CanonicalProject project,
        ICollection<ImportWarning> diagnostics,
        ISet<string> sourceIds)
    {
        var documentsBySource = project.Sources
            .SelectMany(source => source.Documents.Select(document => (SourceId: source.Id, Document: document)))
            .GroupBy(item => item.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Document).ToArray(), StringComparer.OrdinalIgnoreCase);

        foreach (var source in project.Sources)
        {
            foreach (var document in source.Documents)
            {
                ValidateSourceReference(diagnostics, document.SourceReference, sourceIds, documentsBySource, $"source document '{document.Id}'");
            }
        }

        ValidateSourceReferenceList(diagnostics, project.Provenance, sourceIds, documentsBySource, "project provenance");
        foreach (var phase in project.Phases) ValidateSourceReferenceList(diagnostics, phase.SourceReferences, sourceIds, documentsBySource, $"phase '{phase.Id}'");
        foreach (var workPackage in project.WorkPackages) ValidateSourceReferenceList(diagnostics, workPackage.SourceReferences, sourceIds, documentsBySource, $"work package '{workPackage.Id}'");
        foreach (var card in project.DeliveryCards) ValidateSourceReferenceList(diagnostics, card.SourceReferences, sourceIds, documentsBySource, $"delivery card '{card.Id}'");
        foreach (var milestone in project.Milestones) ValidateSourceReferenceList(diagnostics, milestone.SourceReferences, sourceIds, documentsBySource, $"milestone '{milestone.Id}'");
        foreach (var dependency in project.Dependencies) ValidateSourceReferenceList(diagnostics, dependency.SourceReferences, sourceIds, documentsBySource, $"dependency '{dependency.SubjectId}'");
        foreach (var role in project.ResponsibilityRoles) ValidateSourceReferenceList(diagnostics, role.SourceReferences, sourceIds, documentsBySource, $"role '{role.Code}'");
        foreach (var assignment in project.Assignments) ValidateSourceReferenceList(diagnostics, assignment.SourceReferences, sourceIds, documentsBySource, $"assignment '{assignment.WorkItemId}'");
        ValidateSourceReferenceList(diagnostics, project.Reserve.SourceReferences, sourceIds, documentsBySource, "reserve");

        foreach (var record in project.ExecutionOverlay.Records)
        {
            if (record.EvidenceReference is not null)
            {
                ValidateSourceReference(diagnostics, record.EvidenceReference, sourceIds, documentsBySource, $"execution record '{record.WorkItemId}'");
            }
        }
    }

    private static void ValidateSourceReferenceList(
        ICollection<ImportWarning> diagnostics,
        IEnumerable<SourceReference> references,
        ISet<string> sourceIds,
        IReadOnlyDictionary<string, SourceDocument[]> documentsBySource,
        string owner)
    {
        foreach (var reference in references)
        {
            ValidateSourceReference(diagnostics, reference, sourceIds, documentsBySource, owner);
        }
    }

    private static void ValidateSourceReference(
        ICollection<ImportWarning> diagnostics,
        SourceReference? reference,
        ISet<string> sourceIds,
        IReadOnlyDictionary<string, SourceDocument[]> documentsBySource,
        string owner)
    {
        if (reference is null
            || string.IsNullOrWhiteSpace(reference.SourceId)
            || !sourceIds.Contains(reference.SourceId)
            || string.IsNullOrWhiteSpace(reference.RelativeFile)
            || Path.IsPathRooted(reference.RelativeFile)
            || reference.RelativeFile.StartsWith("/", StringComparison.Ordinal)
            || reference.RelativeFile.StartsWith("\\", StringComparison.Ordinal)
            || reference.RelativeFile.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            Add(diagnostics, "INVALID_SOURCE_REFERENCE", $"{owner} contains a source reference that is missing, unsafe, or not captured.", owner);
            return;
        }

        if (!documentsBySource.TryGetValue(reference.SourceId, out var documents)
            || !documents.Any(document => string.Equals(document.RelativeFile, reference.RelativeFile, StringComparison.OrdinalIgnoreCase)))
        {
            Add(diagnostics, "INVALID_SOURCE_REFERENCE", $"{owner} references uncaptured source file '{reference.RelativeFile}'.", owner);
        }
    }

    private static void ValidateManagementEvidence(
        CanonicalProject project,
        ICollection<ImportWarning> diagnostics,
        ISet<string> sourceIds)
    {
        var evidence = project.ManagementEvidence;
        if (evidence is null)
        {
            Add(diagnostics, "INVALID_MANAGEMENT_EVIDENCE", "Canonical project management evidence cannot be null.", null);
            return;
        }

        if (!Enum.IsDefined(evidence.DiscoveryState))
        {
            Add(diagnostics, "INVALID_MANAGEMENT_EVIDENCE_STATE", "Management evidence discoveryState is unsupported.", null);
        }

        if (!string.IsNullOrWhiteSpace(evidence.IncrementPath) && !IsSafeManagementPath(evidence.IncrementPath))
        {
            Add(diagnostics, "INVALID_MANAGEMENT_INCREMENT_PATH", "Management evidence incrementPath must be a safe relative path below specs/.", evidence.IncrementPath);
        }

        var documentsBySource = project.Sources
            .SelectMany(source => source.Documents.Select(document => (SourceId: source.Id, Document: document)))
            .GroupBy(item => item.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Document).ToArray(), StringComparer.OrdinalIgnoreCase);

        var observationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var observation in evidence.Observations ?? Array.Empty<ManagementEvidenceObservation>())
        {
            if (observation is null)
            {
                Add(diagnostics, "INVALID_MANAGEMENT_EVIDENCE_OBSERVATION", "Management evidence observations cannot contain null entries.", null);
                continue;
            }

            var observationId = observation.Id?.Trim() ?? string.Empty;
            if (observationId.Length == 0)
            {
                Add(diagnostics, "INVALID_MANAGEMENT_EVIDENCE_ID", "Every management evidence observation requires a non-empty ID.", null);
            }
            else if (!observationIds.Add(observationId))
            {
                Add(diagnostics, "DUPLICATE_MANAGEMENT_EVIDENCE_ID", $"Management evidence observation '{observationId}' is declared more than once.", observationId);
            }

            if (!Enum.IsDefined(observation.EvidenceKind))
            {
                Add(diagnostics, "INVALID_MANAGEMENT_EVIDENCE_KIND", $"Management evidence observation '{observationId}' has an unsupported evidence kind.", observationId);
            }

            if (!Enum.IsDefined(observation.ValidationState))
            {
                Add(diagnostics, "INVALID_MANAGEMENT_EVIDENCE_VALIDATION_STATE", $"Management evidence observation '{observationId}' has an unsupported validation state.", observationId);
            }

            if (observation.ExplicitTarget is not null)
            {
                ValidateEvidenceTarget(diagnostics, observation.ExplicitTarget, observationId, "explicit target");
            }

            foreach (var reference in observation.SourceReferences ?? Array.Empty<SourceReference>())
            {
                ValidateSourceReference(diagnostics, reference, sourceIds, documentsBySource, $"management evidence observation '{observationId}'");
            }

            foreach (var link in observation.EvidenceLinks ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(link) || !IsSafeEvidenceLink(link))
                {
                    Add(diagnostics, "INVALID_MANAGEMENT_EVIDENCE_LINK", $"Management evidence observation '{observationId}' contains an unsafe evidence link.", observationId);
                }
            }
        }

        var reconciliationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reconciliation in evidence.Reconciliations ?? Array.Empty<EvidenceReconciliation>())
        {
            if (reconciliation is null)
            {
                Add(diagnostics, "INVALID_EVIDENCE_RECONCILIATION", "Evidence reconciliations cannot contain null entries.", null);
                continue;
            }

            var observationId = reconciliation.ObservationId?.Trim() ?? string.Empty;
            if (observationId.Length == 0 || !observationIds.Contains(observationId))
            {
                Add(diagnostics, "INVALID_EVIDENCE_RECONCILIATION", $"Evidence reconciliation '{observationId}' does not resolve to a captured observation.", observationId);
            }
            else if (!reconciliationIds.Add(observationId))
            {
                Add(diagnostics, "DUPLICATE_EVIDENCE_RECONCILIATION", $"Evidence observation '{observationId}' has more than one reconciliation result.", observationId);
            }

            if (!Enum.IsDefined(reconciliation.Status))
            {
                Add(diagnostics, "INVALID_EVIDENCE_RECONCILIATION", $"Evidence reconciliation '{observationId}' has an unsupported status.", observationId);
            }

            if (reconciliation.ResolvedTarget is not null)
            {
                ValidateEvidenceTarget(diagnostics, reconciliation.ResolvedTarget, observationId, "resolved target");
            }

            foreach (var candidate in reconciliation.CandidateTargets ?? Array.Empty<EvidenceTarget>())
            {
                ValidateEvidenceTarget(diagnostics, candidate, observationId, "candidate target");
            }

            var candidateCount = reconciliation.CandidateTargets?.Count ?? 0;
            switch (reconciliation.Status)
            {
                case EvidenceReconciliationStatus.Matched when reconciliation.ResolvedTarget is null:
                    Add(diagnostics, "INVALID_EVIDENCE_RECONCILIATION", $"Matched evidence observation '{observationId}' requires a resolved target.", observationId);
                    break;
                case EvidenceReconciliationStatus.Matched when candidateCount > 1:
                    Add(diagnostics, "INVALID_EVIDENCE_RECONCILIATION", $"Matched evidence observation '{observationId}' cannot retain multiple candidate targets.", observationId);
                    break;
                case EvidenceReconciliationStatus.Ambiguous when reconciliation.ResolvedTarget is not null || candidateCount < 2:
                    Add(diagnostics, "INVALID_EVIDENCE_RECONCILIATION", $"Ambiguous evidence observation '{observationId}' requires at least two candidates and no resolved target.", observationId);
                    break;
                case EvidenceReconciliationStatus.Standalone when reconciliation.ResolvedTarget is not null || candidateCount > 0:
                    Add(diagnostics, "INVALID_EVIDENCE_RECONCILIATION", $"Standalone evidence observation '{observationId}' cannot resolve to a canonical target or retain candidate targets.", observationId);
                    break;
                case EvidenceReconciliationStatus.Unmatched or EvidenceReconciliationStatus.Invalid when reconciliation.ResolvedTarget is not null:
                    Add(diagnostics, "INVALID_EVIDENCE_RECONCILIATION", $"Unmatched or invalid evidence observation '{observationId}' cannot have a resolved target.", observationId);
                    break;
            }

            foreach (var reference in reconciliation.SourceReferences ?? Array.Empty<SourceReference>())
            {
                ValidateSourceReference(diagnostics, reference, sourceIds, documentsBySource, $"evidence reconciliation '{observationId}'");
            }
        }
    }

    private static void ValidateEvidenceTarget(
        ICollection<ImportWarning> diagnostics,
        EvidenceTarget target,
        string owner,
        string label)
    {
        if (target is null || string.IsNullOrWhiteSpace(target.Kind) || string.IsNullOrWhiteSpace(target.Id))
        {
            Add(diagnostics, "EVIDENCE_TARGET_INVALID", $"Management evidence '{owner}' has an invalid {label}.", owner);
        }
    }

    private static bool IsSafeManagementPath(string path)
    {
        if (Path.IsPathRooted(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || path.StartsWith("\\", StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2
            && string.Equals(segments[0], "specs", StringComparison.OrdinalIgnoreCase)
            && segments.All(segment => segment is not "." and not ".."
                                       && !segment.Contains(':', StringComparison.Ordinal)
                                       && !segment.Any(char.IsControl));
    }

    private static bool IsSafeEvidenceLink(string link)
    {
        var normalized = link.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(normalized)
            || normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not ".." && !segment.Any(char.IsControl));
    }

    private static void ValidateOverlay(
        CanonicalProject project,
        ICollection<ImportWarning> diagnostics,
        ISet<string> cardIds)
    {
        if (project.ExecutionOverlay is null)
        {
            Add(diagnostics, "INVALID_EXECUTION_OVERLAY", "Execution overlay is required.", null);
            return;
        }

        var seenRecordIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in project.ExecutionOverlay.Records)
        {
            var workItemId = record.WorkItemId?.Trim() ?? string.Empty;
            if (workItemId.Length == 0)
            {
                Add(diagnostics, "INVALID_EXECUTION_RECORD", "Execution record work item ID is required.", null);
                continue;
            }

            if (!cardIds.Contains(workItemId))
            {
                Add(diagnostics, "INVALID_EXECUTION_TARGET", $"Execution record '{workItemId}' does not resolve to a delivery card.", workItemId);
            }

            if (!seenRecordIds.Add(workItemId))
            {
                Add(diagnostics, "DUPLICATE_EXECUTION_RECORD", $"Execution overlay contains more than one record for '{workItemId}'.", workItemId);
            }

            if (record.LastUpdatedAt is null || record.LastUpdatedAt == DateTimeOffset.MinValue)
            {
                Add(diagnostics, "INVALID_EXECUTION_RECORD", $"Execution record '{workItemId}' requires a valid lastUpdatedAt.", workItemId);
            }

            ValidateState(diagnostics, workItemId, record.ActualStart, record.ActualStartState, "actualStart");
            ValidateState(diagnostics, workItemId, record.ActualFinish, record.ActualFinishState, "actualFinish");
            ValidateState(diagnostics, workItemId, record.ActualEffortHours, record.ActualEffortState, "actualEffortHours");
            ValidateState(diagnostics, workItemId, record.RemainingEffortHours, record.RemainingEffortState, "remainingEffortHours");
            ValidateDate(diagnostics, record.ActualStart, "INVALID_EXECUTION_DATE", workItemId, "actual start");
            ValidateDate(diagnostics, record.ActualFinish, "INVALID_EXECUTION_DATE", workItemId, "actual finish");

            if (record.ActualStart is not null
                && record.ActualFinish is not null
                && record.ActualFinish < record.ActualStart)
            {
                Add(diagnostics, "INVALID_EXECUTION_DATE_RANGE", $"Execution record '{workItemId}' has an actual finish before its actual start.", workItemId);
            }

            ValidateNonNegative(diagnostics, record.ActualEffortHours, "INVALID_EXECUTION_EFFORT", workItemId, "actual effort");
            ValidateNonNegative(diagnostics, record.RemainingEffortHours, "INVALID_EXECUTION_EFFORT", workItemId, "remaining effort");

            if (!Enum.IsDefined(record.ExecutionState))
            {
                Add(diagnostics, "INVALID_EXECUTION_STATE", $"Execution record '{workItemId}' has an unsupported execution state.", workItemId);
            }

            switch (record.ExecutionState)
            {
                case ExecutionState.InProgress when record.ActualStart is null:
                    Add(diagnostics, "INVALID_EXECUTION_STATE", $"In-progress execution record '{workItemId}' requires actualStart.", workItemId);
                    break;
                case ExecutionState.InProgress when record.ActualFinish is not null:
                    Add(diagnostics, "INVALID_EXECUTION_STATE", $"In-progress execution record '{workItemId}' cannot have actualFinish.", workItemId);
                    break;
                case ExecutionState.Completed when record.ActualFinish is null:
                    Add(diagnostics, "INVALID_EXECUTION_STATE", $"Completed execution record '{workItemId}' requires actualFinish.", workItemId);
                    break;
                case ExecutionState.NotStarted when record.ActualStart is not null || record.ActualFinish is not null:
                    Add(diagnostics, "INVALID_EXECUTION_STATE", $"Not-started execution record '{workItemId}' cannot have actual dates.", workItemId);
                    break;
            }
        }
    }

    private static void ValidatePoliciesAndMeasures(CanonicalProject project, ICollection<ImportWarning> diagnostics)
    {
        ValidateNonNegative(diagnostics, project.Capacity.CapacityHours, "INVALID_CAPACITY", "capacity", "capacity hours");
        ValidateNonNegative(diagnostics, project.Capacity.Calendar.HoursPerWorkingDay, "INVALID_CALENDAR", "calendar", "hours per working day");
        ValidateNonNegative(diagnostics, project.Reserve.InitialHours, "INVALID_RESERVE", "reserve", "initial reserve");
        ValidateNonNegative(diagnostics, project.Reserve.ConsumedHours, "INVALID_RESERVE", "reserve", "consumed reserve");
        ValidateNonNegative(diagnostics, project.Reserve.RemainingHours, "INVALID_RESERVE", "reserve", "remaining reserve");
        if (project.Policies.WorkInProgressLimit is < 0)
        {
            Add(diagnostics, "INVALID_WIP_POLICY", "Work-in-progress limit cannot be negative.", "policy");
        }
    }

    private static void ValidateV2AuthorityData(
        CanonicalProject project,
        ICollection<ImportWarning> diagnostics,
        ISet<string> cardIds)
    {
        if (!string.Equals(project.SchemaVersion, "2.0", StringComparison.Ordinal))
        {
            return;
        }

        var metadata = project.ImportMetadata;
        if (metadata is null)
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Schema 2.0 requires semantic import metadata.", null);
            return;
        }

        ValidateImportMetadata(project, metadata, diagnostics);
        ValidateSourceExecution(project, metadata, diagnostics, cardIds);
        ValidateExecutionProposals(project, metadata, diagnostics, cardIds);

        if (project.ExecutionOverlay.Records.Count > 0)
        {
            Add(diagnostics, "LEGACY_EXECUTION_OVERLAY_PRESENT", "Schema 2.0 cannot retain effective legacy execution overlay records.", null);
        }
    }

    private static void ValidateImportMetadata(
        CanonicalProject project,
        ManifestSnapshotMetadata metadata,
        ICollection<ImportWarning> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(metadata.RepositoryIdentity)
            || Path.IsPathRooted(metadata.RepositoryIdentity)
            || metadata.RepositoryIdentity.Any(char.IsControl))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata requires a safe repository identity.", "repositoryIdentity");
        }
        else if (Uri.TryCreate(metadata.RepositoryIdentity, UriKind.Absolute, out var repositoryUri)
            && !string.IsNullOrEmpty(repositoryUri.UserInfo))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata repository identity cannot contain URI user info.", "repositoryIdentity");
        }

        if (!Enum.IsDefined(metadata.ImportMode))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata has an unsupported import mode.", "importMode");
        }

        if (!Enum.IsDefined(metadata.Classification))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata has an unsupported classification.", "classification");
        }

        if (string.IsNullOrWhiteSpace(metadata.SourceIdentity)
            || metadata.SourceIdentity.Any(char.IsControl)
            || (metadata.ImportMode == ManifestImportMode.GitCommit && !IsFullSha(metadata.SourceIdentity)))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata requires a safe full 40-hex source identity for Git commits.", "sourceIdentity");
        }

        if (!IsSafeRelativePath(metadata.ManifestPath)
            || !string.Equals(metadata.ManifestPath.Replace('\\', '/'), "planning/project-management-compiler-manifest.json", StringComparison.Ordinal))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata manifestPath must be the supported repository-relative manifest path.", "manifestPath");
        }

        if (!string.Equals(metadata.ContractVersion, "0.1.0", StringComparison.Ordinal))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata contractVersion must be supported contract 0.1.0.", "contractVersion");
        }

        if (string.IsNullOrWhiteSpace(metadata.ProjectId))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata requires a projectId.", "projectId");
        }

        if (string.IsNullOrWhiteSpace(metadata.BaselineId))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata requires a baselineId.", "baselineId");
        }

        if (metadata.RegisterRevision < 1)
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata registerRevision must be at least 1.", "registerRevision");
        }

        if (!string.Equals(metadata.ProjectId, project.Project.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(metadata.BaselineId, project.Baseline.Id, StringComparison.OrdinalIgnoreCase))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata projectId and baselineId must match the canonical project and baseline identities.", "projectId");
        }

        if (!Enum.IsDefined(metadata.ValidationResult)
            || !Enum.IsDefined(metadata.SourceReadiness))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata validation and readiness states must be supported enum values.", "validationResult");
        }

        if (string.IsNullOrWhiteSpace(metadata.SnapshotId)
            || metadata.ImportedAtUtc == default
            || metadata.WarningCount < 0
            || metadata.ErrorCount < 0)
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata requires a snapshot ID, import time, and non-negative diagnostic counts.", "snapshotId");
        }

        if (metadata.Classification == ManifestImportClassification.OfficialCommit
            && (metadata.ImportMode != ManifestImportMode.GitCommit
                || metadata.SourceReadiness != SourceReadinessState.Pass
                || metadata.ValidationResult == ManifestValidationResult.Fail))
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "An official commit must be a Git commit with passed readiness and non-failed validation.", "classification");
        }

        if (metadata.ImportMode == ManifestImportMode.UncommittedPreview
            && metadata.Classification != ManifestImportClassification.UncommittedPreview
            && metadata.Classification != ManifestImportClassification.Failed)
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "An uncommitted import must remain an uncommitted preview or failed attempt.", "classification");
        }

        if (metadata.RegisterStatusDate == DateOnly.MinValue)
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata registerStatusDate cannot use DateOnly.MinValue.", "registerStatusDate");
        }

        if (metadata.Calendars is null)
        {
            Add(diagnostics, "INVALID_IMPORT_METADATA", "Import metadata calendars cannot be null.", "calendars");
        }
    }

    private static void ValidateSourceExecution(
        CanonicalProject project,
        ManifestSnapshotMetadata metadata,
        ICollection<ImportWarning> diagnostics,
        ISet<string> cardIds)
    {
        var snapshot = project.SourceExecution;
        if (snapshot is null)
        {
            Add(diagnostics, "INVALID_SOURCE_EXECUTION", "Schema 2.0 requires a source execution snapshot.", null);
            return;
        }

        if (string.IsNullOrWhiteSpace(snapshot.ProjectId)
            || string.IsNullOrWhiteSpace(snapshot.BaselineId)
            || string.IsNullOrWhiteSpace(snapshot.RegisterId)
            || snapshot.RegisterRevision < 1
            || snapshot.StatusDate is null
            || !IsSafeRelativePath(snapshot.SourcePath))
        {
            Add(diagnostics, "INVALID_SOURCE_EXECUTION", "Source execution requires project/baseline/register identities, a positive revision, status date, and safe source path.", "sourceExecution");
        }

        if (!string.Equals(snapshot.ProjectId, metadata.ProjectId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.BaselineId, metadata.BaselineId, StringComparison.OrdinalIgnoreCase))
        {
            Add(diagnostics, "INVALID_SOURCE_EXECUTION", "Source execution project and baseline identities must match import metadata.", "sourceExecution");
        }

        if (snapshot.RegisterRevision != metadata.RegisterRevision
            || snapshot.StatusDate != metadata.RegisterStatusDate)
        {
            Add(diagnostics, "INVALID_SOURCE_EXECUTION", "Source execution revision and status date must match import metadata.", "sourceExecution");
        }

        if (snapshot.Records is null)
        {
            Add(diagnostics, "INVALID_SOURCE_EXECUTION", "Source execution records cannot be null.", "records");
            return;
        }

        var seen = new HashSet<CanonicalWorkItemKey>();
        foreach (var record in snapshot.Records)
        {
            if (record is null)
            {
                Add(diagnostics, "INVALID_SOURCE_EXECUTION", "Source execution records cannot contain null entries.", "records");
                continue;
            }

            var key = record.Entity;
            if (!string.Equals(key.Kind, "DeliveryCard", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(key.Id)
                || !cardIds.Contains(key.Id))
            {
                Add(diagnostics, "INVALID_SOURCE_EXECUTION_TARGET", $"Source execution target '{key}' must resolve to a canonical DeliveryCard.", key.Id);
            }

            if (!string.IsNullOrWhiteSpace(key.Kind)
                && !string.IsNullOrWhiteSpace(key.Id)
                && !seen.Add(key))
            {
                Add(diagnostics, "DUPLICATE_SOURCE_EXECUTION_IDENTITY", $"Source execution contains duplicate identity '{key}'.", key.Id);
            }

            if (!Enum.IsDefined(record.RecordingState)
                || (record.ExecutionState is not null && !Enum.IsDefined(record.ExecutionState.Value))
                || (record.ResultState is not null && !Enum.IsDefined(record.ResultState.Value)))
            {
                Add(diagnostics, "INVALID_SOURCE_EXECUTION_STATE", $"Source execution record '{key.Id}' contains an unsupported state.", key.Id);
            }

            if (!IsSafeRelativePath(record.SourcePath))
            {
                Add(diagnostics, "INVALID_SOURCE_EXECUTION_PATH", $"Source execution record '{key.Id}' has an unsafe source path.", key.Id);
            }

            ValidateDateRangeAndEffort(
                diagnostics,
                key.Id,
                record.ActualStart,
                record.ActualFinish,
                record.ActualEffortHours,
                record.RemainingEffortHours,
                "INVALID_SOURCE_EXECUTION");

            if (record.RecordingState == SourceRecordingState.NotRecorded
                && (record.ExecutionState is not null
                    || record.ResultState is not null
                    || record.ActualStart is not null
                    || record.ActualFinish is not null
                    || record.ActualEffortHours is not null
                    || record.RemainingEffortHours is not null
                    || record.ForecastFinish is not null
                    || !string.IsNullOrWhiteSpace(record.Blocker)
                    || (record.Evidence?.Count ?? 0) > 0
                    || record.LastUpdatedAt is not null
                    || !string.IsNullOrWhiteSpace(record.RecordedBy)))
            {
                Add(diagnostics, "INVALID_SOURCE_EXECUTION_STATE", $"NOT_RECORDED source execution record '{key.Id}' cannot carry execution evidence.", key.Id);
            }

            if (record.RecordingState == SourceRecordingState.Recorded
                && (record.ExecutionState is null || record.ResultState is null || record.LastUpdatedAt is null))
            {
                Add(diagnostics, "INVALID_SOURCE_EXECUTION_STATE", $"RECORDED source execution record '{key.Id}' requires execution state, result state, and lastUpdatedAt.", key.Id);
            }

            ValidateControlledEvidence(diagnostics, record.Evidence, key.Id, "source execution evidence");
            if (record.ExecutionState == ExecutionState.Completed
                && (record.ActualFinish is null
                    || record.ActualEffortHours is null
                    || record.RemainingEffortHours != 0
                    || (record.Evidence?.Count ?? 0) == 0))
            {
                Add(diagnostics, "INVALID_SOURCE_EXECUTION_COMPLETION", $"Completed source execution record '{key.Id}' requires finish, actual effort, zero remaining effort, and controlled evidence.", key.Id);
            }
        }
    }

    private static void ValidateExecutionProposals(
        CanonicalProject project,
        ManifestSnapshotMetadata? metadata,
        ICollection<ImportWarning> diagnostics,
        ISet<string> cardIds)
    {
        if (project.ExecutionProposals is null)
        {
            Add(diagnostics, "INVALID_EXECUTION_PROPOSALS", "Schema 2.0 executionProposals cannot be null.", null);
            return;
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "executionState", "resultState", "actualStart", "actualFinish",
            "actualEffortHours", "remainingEffortHours", "forecastFinish", "blocker",
            "note", "legacyEvidenceReference"
        };

        foreach (var proposal in project.ExecutionProposals)
        {
            if (proposal is null)
            {
                Add(diagnostics, "INVALID_EXECUTION_PROPOSAL", "Execution proposals cannot contain null entries.", null);
                continue;
            }

            if (string.IsNullOrWhiteSpace(proposal.Id) || !seenIds.Add(proposal.Id))
            {
                Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_ID", "Every execution proposal requires a unique non-empty ID.", proposal.Id);
            }

            if (string.IsNullOrWhiteSpace(proposal.BaseSnapshotId)
                || proposal.ExpectedRegisterRevision < 0
                || proposal.CreatedAtUtc == default
                || proposal.UpdatedAtUtc == default
                || proposal.UpdatedAtUtc < proposal.CreatedAtUtc)
            {
                Add(diagnostics, "INVALID_EXECUTION_PROPOSAL", $"Execution proposal '{proposal.Id}' has invalid base or timestamp metadata.", proposal.Id);
            }

            if (!Enum.IsDefined(proposal.Lifecycle))
            {
                Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_LIFECYCLE", $"Execution proposal '{proposal.Id}' has an unsupported lifecycle.", proposal.Id);
            }

            if (!string.Equals(CanonicalWorkItemKey.NormalizeKind(proposal.TargetKind), "DeliveryCard", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(proposal.TargetId)
                || !cardIds.Contains(proposal.TargetId))
            {
                Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_TARGET", $"Execution proposal '{proposal.Id}' target must resolve to a canonical DeliveryCard.", proposal.Id);
            }

            if (proposal.ProposedChanges is null)
            {
                Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_CHANGES", $"Execution proposal '{proposal.Id}' changes cannot be null.", proposal.Id);
            }
            else
            {
                ValidateProposalChanges(diagnostics, proposal, allowedFields);
            }

            ValidateControlledEvidence(diagnostics, proposal.Evidence, proposal.Id, "proposal evidence");
            if (proposal.Lifecycle == ProposalLifecycle.ReadyForReview && !ProposalCompletionIsReady(proposal))
            {
                Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_LIFECYCLE", $"Execution proposal '{proposal.Id}' cannot be READY_FOR_REVIEW without complete changes and controlled evidence.", proposal.Id);
            }

            if (metadata is not null
                && !string.Equals(proposal.BaseSnapshotId, metadata.SnapshotId, StringComparison.OrdinalIgnoreCase)
                && proposal.Lifecycle != ProposalLifecycle.StaleBase)
            {
                Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_BASE", $"Execution proposal '{proposal.Id}' must be STALE_BASE when its base snapshot differs from the current official snapshot.", proposal.Id);
            }
        }
    }

    private static void ValidateProposalChanges(
        ICollection<ImportWarning> diagnostics,
        ExecutionProposal proposal,
        ISet<string> allowedFields)
    {
        foreach (var pair in proposal.ProposedChanges)
        {
            if (!allowedFields.Contains(pair.Key))
            {
                Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_CHANGE", $"Execution proposal '{proposal.Id}' contains unsupported change field '{pair.Key}'.", proposal.Id);
                continue;
            }

            if (pair.Value is null)
            {
                continue;
            }

            switch (pair.Key)
            {
                case "executionState" when !TryParseExecutionState(pair.Value):
                case "resultState" when !TryParseResultState(pair.Value):
                    Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_CHANGE", $"Execution proposal '{proposal.Id}' contains an unsupported state value for '{pair.Key}'.", proposal.Id);
                    break;
                case "actualStart":
                case "actualFinish":
                    if (!DateOnly.TryParseExact(pair.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_CHANGE", $"Execution proposal '{proposal.Id}' contains an invalid date for '{pair.Key}'.", proposal.Id);
                    }

                    break;
                case "actualEffortHours":
                case "remainingEffortHours":
                    if (!decimal.TryParse(pair.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var hours)
                        || hours < 0
                        || decimal.Remainder(hours, 0.5m) != 0)
                    {
                        Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_CHANGE", $"Execution proposal '{proposal.Id}' contains an invalid non-negative half-hour value for '{pair.Key}'.", proposal.Id);
                    }

                    break;
                case "forecastFinish":
                    if (!DateTimeOffset.TryParse(pair.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                    {
                        Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_CHANGE", $"Execution proposal '{proposal.Id}' contains an invalid date-time for forecastFinish.", proposal.Id);
                    }

                    break;
                case "legacyEvidenceReference" when !IsSafeRelativePath(pair.Value):
                    Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_CHANGE", $"Execution proposal '{proposal.Id}' contains an unsafe legacy evidence reference.", proposal.Id);
                    break;
            }
        }

        if (proposal.ProposedChanges.TryGetValue("actualStart", out var start)
            && proposal.ProposedChanges.TryGetValue("actualFinish", out var finish)
            && DateOnly.TryParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate)
            && DateOnly.TryParseExact(finish, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var finishDate)
            && finishDate < startDate)
        {
            Add(diagnostics, "INVALID_EXECUTION_PROPOSAL_CHANGE", $"Execution proposal '{proposal.Id}' has a finish before its start.", proposal.Id);
        }
    }

    private static void ValidateDateRangeAndEffort(
        ICollection<ImportWarning> diagnostics,
        string owner,
        DateOnly? start,
        DateOnly? finish,
        decimal? actualEffort,
        decimal? remainingEffort,
        string code)
    {
        ValidateDate(diagnostics, start, code + "_DATE", owner, "actual start");
        ValidateDate(diagnostics, finish, code + "_DATE", owner, "actual finish");
        if (start is not null && finish is not null && finish < start)
        {
            Add(diagnostics, code + "_DATE_RANGE", $"Execution record '{owner}' has an actual finish before its actual start.", owner);
        }

        ValidateNonNegative(diagnostics, actualEffort, code + "_EFFORT", owner, "actual effort");
        ValidateNonNegative(diagnostics, remainingEffort, code + "_EFFORT", owner, "remaining effort");
        ValidateHalfHour(diagnostics, actualEffort, code + "_EFFORT_GRANULARITY", owner, "actual effort");
        ValidateHalfHour(diagnostics, remainingEffort, code + "_EFFORT_GRANULARITY", owner, "remaining effort");
    }

    private static void ValidateControlledEvidence(
        ICollection<ImportWarning> diagnostics,
        IEnumerable<SourceExecutionEvidence>? evidence,
        string owner,
        string label)
    {
        foreach (var item in evidence ?? Array.Empty<SourceExecutionEvidence>())
        {
            if (!IsControlledEvidenceValid(item))
            {
                Add(diagnostics, "INVALID_CONTROLLED_EVIDENCE", $"{label} for '{owner}' does not satisfy the controlled evidence contract.", owner);
            }
        }
    }

    private static bool ProposalCompletionIsReady(ExecutionProposal proposal)
    {
        var changes = proposal.ProposedChanges;
        return changes.TryGetValue("executionState", out var state)
            && TryParseExecutionState(state, out var parsedState)
            && parsedState == ExecutionState.Completed
            && changes.TryGetValue("actualFinish", out var finish)
            && DateOnly.TryParseExact(finish, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            && changes.TryGetValue("actualEffortHours", out var actual)
            && decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var actualHours)
            && actualHours >= 0
            && changes.TryGetValue("remainingEffortHours", out var remaining)
            && decimal.TryParse(remaining, NumberStyles.Number, CultureInfo.InvariantCulture, out var remainingHours)
            && remainingHours == 0
            && (proposal.Evidence?.Count ?? 0) > 0
            && (proposal.Evidence ?? Array.Empty<SourceExecutionEvidence>()).All(item => IsControlledEvidenceValid(item));
    }

    private static bool IsControlledEvidenceValid(SourceExecutionEvidence? item) =>
        ControlledEvidenceRules.IsValid(item);

    private static bool IsFullSha(string value) =>
        value.Length == 40 && value.All(Uri.IsHexDigit);

    private static bool TryParseExecutionState(string? value) =>
        TryParseExecutionState(value, out _);

    private static bool TryParseExecutionState(string? value, out ExecutionState state)
    {
        var parsed = Enum.TryParse<ExecutionState>(value?.Replace("_", string.Empty, StringComparison.Ordinal), true, out state);
        return parsed && Enum.IsDefined(state);
    }

    private static bool TryParseResultState(string? value) =>
        Enum.TryParse<SourceResultState>(value?.Replace("_", string.Empty, StringComparison.Ordinal), true, out var parsed)
        && Enum.IsDefined(parsed);

    private static bool IsSafeRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || Path.IsPathRooted(value)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith("\\", StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = value.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0
            && !segments.Any(segment => segment is "." or ".."
                                        || segment.Contains(':')
                                        || segment.Any(char.IsControl))
            && !normalized.Equals(".git", StringComparison.OrdinalIgnoreCase)
            && !normalized.StartsWith(".git/", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidatePlannedEntityDatesAndMeasures(
        ICollection<ImportWarning> diagnostics,
        PlannedEntity entity,
        string label)
    {
        ValidateDate(diagnostics, entity.PlannedStart, "INVALID_PLANNED_DATE", entity.Id, $"{label} planned start");
        ValidateDate(diagnostics, entity.PlannedFinish, "INVALID_PLANNED_DATE", entity.Id, $"{label} planned finish");
        if (entity.PlannedStart is not null
            && entity.PlannedFinish is not null
            && entity.PlannedFinish < entity.PlannedStart)
        {
            Add(diagnostics, "INVALID_PLANNED_DATE_RANGE", $"{label} '{entity.Id}' has a finish before its start.", entity.Id);
        }

        ValidateNonNegative(diagnostics, entity.PlannedEffortHours, "INVALID_PLANNED_EFFORT", entity.Id, $"{label} planned effort");
        ValidateNonNegative(diagnostics, entity.PlannedDurationWorkingMinutes, "INVALID_PLANNED_DURATION", entity.Id, $"{label} planned duration");
    }

    private static HashSet<string> ValidateIds<T>(
        IEnumerable<T> items,
        Func<T, string?> idSelector,
        string emptyCode,
        string duplicateCode,
        ICollection<ImportWarning> diagnostics)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var id = idSelector(item)?.Trim() ?? string.Empty;
            if (id.Length == 0)
            {
                Add(diagnostics, emptyCode, "Canonical hierarchy item requires a non-empty ID.", null);
            }
            else if (!ids.Add(id))
            {
                Add(diagnostics, duplicateCode, $"Canonical hierarchy item '{id}' is declared more than once.", id);
            }
        }

        return ids;
    }

    private static void ValidateCarioCode(ICollection<ImportWarning> diagnostics, string? code, string? owner)
    {
        if (code is not null && !CarioRoleCodes.Contains(code))
        {
            Add(diagnostics, "INVALID_CARIO_ROLE_CODE", $"'{code}' is not a supported CARIO role code.", owner);
        }
    }

    private static void ValidateDate(
        ICollection<ImportWarning> diagnostics,
        DateOnly? value,
        string code,
        string? owner,
        string label)
    {
        if (value == DateOnly.MinValue)
        {
            Add(diagnostics, code, $"{label} cannot use DateOnly.MinValue as a placeholder.", owner);
        }
    }

    private static void ValidateNonNegative<T>(
        ICollection<ImportWarning> diagnostics,
        T? value,
        string code,
        string? owner,
        string label)
        where T : struct, IComparable<T>
    {
        if (value is not null && value.Value.CompareTo(default) < 0)
        {
            Add(diagnostics, code, $"{label} cannot be negative.", owner);
        }
    }

    private static void ValidateHalfHour(
        ICollection<ImportWarning> diagnostics,
        decimal? value,
        string code,
        string? owner,
        string label)
    {
        if (value is not null && decimal.Remainder(value.Value, 0.5m) != 0)
        {
            Add(diagnostics, code, $"{label} must use 0.5-hour granularity.", owner);
        }
    }

    private static void ValidateState<T>(
        ICollection<ImportWarning> diagnostics,
        string workItemId,
        T? value,
        DataState state,
        string field)
        where T : struct
    {
        if (!Enum.IsDefined(state))
        {
            Add(diagnostics, "INVALID_EXECUTION_DATA_STATE", $"Execution record '{workItemId}' field '{field}' has an unsupported data state.", workItemId);
            return;
        }

        var expected = value.HasValue ? DataState.Known : DataState.Unknown;
        if (state != expected)
        {
            Add(
                diagnostics,
                "INVALID_EXECUTION_DATA_STATE",
                $"Execution record '{workItemId}' field '{field}' has state '{state}' but value presence requires '{expected}'.",
                workItemId);
        }
    }

    private static void Add(ICollection<ImportWarning> diagnostics, string code, string message, string? affectedId)
    {
        diagnostics.Add(new ImportWarning
        {
            Id = $"{code}:{affectedId ?? "canonical"}",
            Severity = WarningSeverity.Error,
            Code = code,
            Message = message,
            AffectedIds = affectedId is null ? Array.Empty<string>() : [affectedId]
        });
    }
}
