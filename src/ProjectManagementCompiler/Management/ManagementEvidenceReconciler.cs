using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed class ManagementEvidenceReconciler
{
    private static readonly HashSet<string> SupportedTargetKinds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Project",
            "Phase",
            "WorkPackage",
            "DeliveryCard",
            "Milestone",
            "Role"
        };

    private static readonly HashSet<string> StandaloneEvidenceKinds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Gate",
            "Decision",
            "HumanAction",
            "ChecklistContext",
            "ControlEnvelope"
        };

    public ManagementEvidence Reconcile(ManagementEvidence evidence, CanonicalProject planningProject)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(planningProject);

        var diagnostics = evidence.Diagnostics.ToList();
        var reconciliations = evidence.Observations
            .Select(observation => ReconcileObservation(observation, planningProject, diagnostics))
            .ToArray();

        return evidence with
        {
            Reconciliations = reconciliations,
            Diagnostics = diagnostics
        };
    }

    private static EvidenceReconciliation ReconcileObservation(
        ManagementEvidenceObservation observation,
        CanonicalProject planningProject,
        ICollection<ImportWarning> diagnostics)
    {
        var sourceReferences = observation.SourceReferences;
        if (observation.ValidationState == ValidationState.InvalidSourceEvidence)
        {
            return new EvidenceReconciliation
            {
                ObservationId = observation.Id,
                Status = EvidenceReconciliationStatus.Invalid,
                RuleId = "INVALID_OBSERVATION",
                Reason = "The source observation is already invalid.",
                SourceReferences = sourceReferences
            };
        }

        var target = observation.ExplicitTarget;
        if (target is null)
        {
            if (IsStandaloneManagementEvidence(observation))
            {
                return Standalone(observation, "STANDALONE_MANAGEMENT_OBJECT", "This management evidence is valid without a canonical work-item target.");
            }

            var rule = observation.EvidenceKind switch
            {
                ManagementEvidenceKind.DecisionRecord => "DECISION_NO_DEFAULT_TARGET",
                ManagementEvidenceKind.GateExecution or ManagementEvidenceKind.GateOutcome => "GATE_NO_DEFAULT_TARGET",
                ManagementEvidenceKind.HumanAction => "HUMAN_ACTION_NO_DEFAULT_TARGET",
                ManagementEvidenceKind.ChecklistContext => "CHECKLIST_NO_DEFAULT_TARGET",
                ManagementEvidenceKind.ControlEnvelope => "CONTROL_ENVELOPE_NO_DEFAULT_TARGET",
                _ => "EVIDENCE_NO_EXPLICIT_TARGET"
            };
            return Unmatched(observation, rule, "No explicit typed target was supplied.", diagnostics);
        }

        if (StandaloneEvidenceKinds.Contains(target.Kind))
        {
            return Standalone(observation, "STANDALONE_MANAGEMENT_OBJECT", $"{target.Kind}:{target.Id} is a management evidence object, not a canonical work item.");
        }

        var normalizedKind = CanonicalWorkItemKey.NormalizeKind(target.Kind);
        if (!SupportedTargetKinds.Contains(normalizedKind))
        {
            return Invalid(
                observation,
                "EVIDENCE_TARGET_INVALID",
                $"Target kind '{target.Kind}' is not supported.",
                diagnostics);
        }

        if (string.IsNullOrWhiteSpace(target.Id))
        {
            return Invalid(
                observation,
                "EVIDENCE_TARGET_INVALID",
                "A typed target requires a non-empty ID.",
                diagnostics);
        }

        var candidates = FindCandidates(normalizedKind, target.Id, planningProject);
        if (candidates.Count == 0)
        {
            return Unmatched(
                observation,
                "EVIDENCE_TARGET_UNMATCHED",
                $"No canonical target exists for {normalizedKind}:{target.Id}.",
                diagnostics);
        }

        if (candidates.Count > 1)
        {
            var result = new EvidenceReconciliation
            {
                ObservationId = observation.Id,
                Status = EvidenceReconciliationStatus.Ambiguous,
                CandidateTargets = candidates,
                RuleId = "EXPLICIT_TYPED_TARGET",
                Reason = $"More than one canonical target exists for {normalizedKind}:{target.Id}.",
                SourceReferences = sourceReferences
            };
            AddDiagnostic(diagnostics, "EVIDENCE_TARGET_AMBIGUOUS", observation, result.Reason);
            return result;
        }

        return new EvidenceReconciliation
        {
            ObservationId = observation.Id,
            Status = EvidenceReconciliationStatus.Matched,
            ResolvedTarget = candidates[0],
            RuleId = observation.EvidenceKind == ManagementEvidenceKind.ReadinessCheck
                ? "READINESS_WORK_PACKAGE"
                : "EXPLICIT_TYPED_TARGET",
            Reason = $"Matched {candidates[0]}.",
            SourceReferences = sourceReferences
        };
    }

    private static IReadOnlyList<EvidenceTarget> FindCandidates(
        string kind,
        string id,
        CanonicalProject project) =>
        kind switch
        {
            "Project" => Matches([new EvidenceTarget { Kind = "Project", Id = project.Project.Id }], kind, id),
            "Phase" => Matches(project.Phases.Select(phase => new EvidenceTarget { Kind = "Phase", Id = phase.Id }), kind, id),
            "WorkPackage" => Matches(project.WorkPackages.Select(workPackage => new EvidenceTarget { Kind = "WorkPackage", Id = workPackage.Id }), kind, id),
            "DeliveryCard" => Matches(project.DeliveryCards.Select(card => new EvidenceTarget { Kind = "DeliveryCard", Id = card.Id }), kind, id),
            "Milestone" => Matches(project.Milestones.Select(milestone => new EvidenceTarget { Kind = "Milestone", Id = milestone.Id }), kind, id),
            "Role" => Matches(project.ResponsibilityRoles.Select(role => new EvidenceTarget { Kind = "Role", Id = role.Code }), kind, id),
            _ => Array.Empty<EvidenceTarget>()
        };

    private static IReadOnlyList<EvidenceTarget> Matches(
        IEnumerable<EvidenceTarget> candidates,
        string kind,
        string id) =>
        candidates
            .Where(candidate => string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    private static EvidenceReconciliation Unmatched(
        ManagementEvidenceObservation observation,
        string ruleId,
        string reason,
        ICollection<ImportWarning> diagnostics)
    {
        var result = new EvidenceReconciliation
        {
            ObservationId = observation.Id,
            Status = EvidenceReconciliationStatus.Unmatched,
            RuleId = ruleId,
            Reason = reason,
            SourceReferences = observation.SourceReferences
        };
        if (ruleId == "EVIDENCE_TARGET_UNMATCHED")
        {
            AddDiagnostic(diagnostics, ruleId, observation, reason);
        }

        return result;
    }

    private static EvidenceReconciliation Standalone(
        ManagementEvidenceObservation observation,
        string ruleId,
        string reason) =>
        new()
        {
            ObservationId = observation.Id,
            Status = EvidenceReconciliationStatus.Standalone,
            RuleId = ruleId,
            Reason = reason,
            SourceReferences = observation.SourceReferences
        };

    private static bool IsStandaloneManagementEvidence(ManagementEvidenceObservation observation) =>
        observation.EvidenceKind is ManagementEvidenceKind.GateExecution
            or ManagementEvidenceKind.GateOutcome
            or ManagementEvidenceKind.DecisionRecord
            or ManagementEvidenceKind.HumanAction
            or ManagementEvidenceKind.ChecklistContext
            or ManagementEvidenceKind.ControlEnvelope;

    private static EvidenceReconciliation Invalid(
        ManagementEvidenceObservation observation,
        string code,
        string reason,
        ICollection<ImportWarning> diagnostics)
    {
        AddDiagnostic(diagnostics, code, observation, reason);
        return new EvidenceReconciliation
        {
            ObservationId = observation.Id,
            Status = EvidenceReconciliationStatus.Invalid,
            RuleId = "INVALID_TYPED_TARGET",
            Reason = reason,
            SourceReferences = observation.SourceReferences
        };
    }

    private static void AddDiagnostic(
        ICollection<ImportWarning> diagnostics,
        string code,
        ManagementEvidenceObservation observation,
        string message) =>
        diagnostics.Add(new ImportWarning
        {
            Id = $"{code}:{observation.Id}",
            Severity = WarningSeverity.Warning,
            Code = code,
            Message = message,
            AffectedIds = [observation.SourceRecordId],
            SourceReferences = observation.SourceReferences
        });
}
