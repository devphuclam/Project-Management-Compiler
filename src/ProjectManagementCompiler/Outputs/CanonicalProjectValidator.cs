using ProjectManagementCompiler.Domain;

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
        ValidateRolesAndAssignments(project, diagnostics, cardIds, workPackageIds, milestoneIds);
        ValidateDependencies(project, diagnostics, cardIds, workPackageIds, milestoneIds);
        ValidateSourceReferences(project, diagnostics, sourceIds);
        ValidateOverlay(project, diagnostics, cardIds);
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
        ISet<string> cardIds,
        ISet<string> workPackageIds,
        ISet<string> milestoneIds)
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
            if (!cardIds.Contains(assignment.WorkItemId)
                && !workPackageIds.Contains(assignment.WorkItemId)
                && !milestoneIds.Contains(assignment.WorkItemId))
            {
                Add(diagnostics, "INVALID_ASSIGNMENT_TARGET", $"Assignment target '{assignment.WorkItemId}' does not resolve to a canonical work item.", assignment.WorkItemId);
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
        var knownKinds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in cardIds) knownKinds[id] = "DeliveryCard";
        foreach (var id in workPackageIds) knownKinds[id] = "WorkPackage";
        foreach (var id in milestoneIds) knownKinds[id] = "Milestone";

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in project.Dependencies)
        {
            var edgeKey = string.Join("|", dependency.SubjectId, dependency.PredecessorId, dependency.DependencyType);
            if (!seen.Add(edgeKey))
            {
                Add(diagnostics, "DUPLICATE_DEPENDENCY", $"Dependency '{dependency.SubjectId}' -> '{dependency.PredecessorId}' is duplicated.", dependency.SubjectId);
            }

            if (!knownKinds.TryGetValue(dependency.SubjectId, out var subjectKind))
            {
                Add(diagnostics, "INVALID_DEPENDENCY_SUBJECT", $"Dependency subject '{dependency.SubjectId}' does not resolve to a canonical work item.", dependency.SubjectId);
            }
            else if (!string.Equals(subjectKind, dependency.SubjectKind, StringComparison.OrdinalIgnoreCase))
            {
                Add(diagnostics, "INVALID_DEPENDENCY_SUBJECT_KIND", $"Dependency subject '{dependency.SubjectId}' has kind '{dependency.SubjectKind}' but resolves as '{subjectKind}'.", dependency.SubjectId);
            }

            var predecessorExists = knownKinds.TryGetValue(dependency.PredecessorId, out var predecessorKind);
            if (string.Equals(dependency.SubjectId, dependency.PredecessorId, StringComparison.OrdinalIgnoreCase))
            {
                Add(diagnostics, "INVALID_DEPENDENCY_SELF", $"Dependency '{dependency.SubjectId}' cannot depend on itself.", dependency.SubjectId);
            }

            if (predecessorExists)
            {
                if (!string.Equals(predecessorKind, dependency.PredecessorKind, StringComparison.OrdinalIgnoreCase))
                {
                    Add(diagnostics, "INVALID_DEPENDENCY_PREDECESSOR_KIND", $"Dependency predecessor '{dependency.PredecessorId}' has kind '{dependency.PredecessorKind}' but resolves as '{predecessorKind}'.", dependency.SubjectId);
                }

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
        }
    }

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
