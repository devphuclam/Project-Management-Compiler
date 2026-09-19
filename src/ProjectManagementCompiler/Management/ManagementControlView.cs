using System.Text.RegularExpressions;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed record ManagementControlView
{
    public ManagementEvidenceDiscoveryState DiscoveryState { get; init; } = ManagementEvidenceDiscoveryState.NotRequested;
    public string? IncrementId { get; init; }
    public string? IncrementPhaseId { get; init; }
    public string? IncrementName { get; init; }
    public string? IncrementStatus { get; init; }
    public ManagementEvidenceScopeView EvidenceScope { get; init; } = new();
    public ManagementGateView CurrentGate { get; init; } = new();
    public ManagementBaselineContextView BaselineContext { get; init; } = new();
    public IReadOnlyList<ManagementReadinessRow> Readiness { get; init; } = Array.Empty<ManagementReadinessRow>();
    public IReadOnlyList<ManagementAttentionGroup> AttentionGroups { get; init; } = Array.Empty<ManagementAttentionGroup>();
    public IReadOnlyList<ManagementEvidenceInspectorItem> Inspector { get; init; } = Array.Empty<ManagementEvidenceInspectorItem>();
    public ManagementReconciliationSummary Reconciliation { get; init; } = new();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed record ManagementEvidenceScopeView
{
    public string? IncrementPath { get; init; }
    public int ObservationCount { get; init; }
    public int SourceReferenceCount { get; init; }
    public IReadOnlyList<string> SourceFiles { get; init; } = Array.Empty<string>();
}

public sealed record ManagementGateView
{
    public string? GateId { get; init; }
    public string? ExecutionState { get; init; }
    public string? Outcome { get; init; }
    public string? ExecutionSelectionStatus { get; init; }
    public string? OutcomeSelectionStatus { get; init; }
    public string? AuthoritySourceSummary { get; init; }
    public string? GateRecordStatus { get; init; }
    public string? ProposedSuccessorIncrement { get; init; }
    public string? ProposedSuccessorSummary { get; init; }
    public bool IsDecisionPending { get; init; }
    public IReadOnlyList<string> AttentionCodes { get; init; } = Array.Empty<string>();
}

public sealed record ManagementBaselineContextView
{
    public string BaselineId { get; init; } = string.Empty;
    public string BaselineVersion { get; init; } = string.Empty;
    public string BaselineStatus { get; init; } = string.Empty;
    public string? ProposedSuccessorIncrement { get; init; }
    public string? ProposedSuccessorSummary { get; init; }
}

public sealed record ManagementReadinessRow
{
    public string WorkPackageId { get; init; } = string.Empty;
    public string? TaskState { get; init; }
    public string? ReadinessResult { get; init; }
    public string? StateCode { get; init; }
    public string? ResultCode { get; init; }
    public string? OwnerRole { get; init; }
    public string? OwnerRoleLabel { get; init; }
    public string? WaitingForRole { get; init; }
    public string? WaitingForRoleLabel { get; init; }
    public string? PendingActionSummary { get; init; }
    public string? DueCondition { get; init; }
    public string? DueConditionSummary { get; init; }
    public string? GateEffect { get; init; }
    public string? GateEffectSummary { get; init; }
    public string? BlockerSummary { get; init; }
    public string? EvidenceState { get; init; }
    public int SourceCount { get; init; }
    public EvidenceReconciliationStatus ReconciliationStatus { get; init; }
}

public sealed record ManagementAttentionGroup
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public IReadOnlyList<ManagementAttentionItem> Items { get; init; } = Array.Empty<ManagementAttentionItem>();
}

public sealed record ManagementAttentionItem
{
    public string ObservationId { get; init; } = string.Empty;
    public string RecordId { get; init; } = string.Empty;
    public string? StateCode { get; init; }
    public string? ResultCode { get; init; }
    public string? Summary { get; init; }
    public string? ActionSummary { get; init; }
    public string? WaitingForRole { get; init; }
    public string? RequiredAuthorityRole { get; init; }
    public string? AffectedTargetSummary { get; init; }
    public string? GateEffect { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record ManagementEvidenceInspectorItem
{
    public string ObservationId { get; init; } = string.Empty;
    public ManagementEvidenceKind EvidenceKind { get; init; }
    public string SourceRecordId { get; init; } = string.Empty;
    public string? StateCode { get; init; }
    public string? ResultCode { get; init; }
    public string? OwnerRole { get; init; }
    public string? OwnerRoleLabel { get; init; }
    public string? WaitingForRole { get; init; }
    public string? WaitingForRoleLabel { get; init; }
    public string? RequiredAuthorityRole { get; init; }
    public string? RequiredAuthorityRoleLabel { get; init; }
    public string? Summary { get; init; }
    public string? ActionSummary { get; init; }
    public string? PendingActionSummary { get; init; }
    public string? BlockerSummary { get; init; }
    public string? DueConditionSummary { get; init; }
    public string? GateEffectSummary { get; init; }
    public string? AffectedTargetSummary { get; init; }
    public string? CompletionCondition { get; init; }
    public string? AuthorityKind { get; init; }
    public string? ReconciliationStatus { get; init; }
    public string? ReconciliationTarget { get; init; }
    public string? DueCondition { get; init; }
    public string? GateEffect { get; init; }
    public IReadOnlyList<string> EvidenceLinks { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
}

public sealed record ManagementReconciliationSummary
{
    public int Matched { get; init; }
    public int Unmatched { get; init; }
    public int Ambiguous { get; init; }
    public int Invalid { get; init; }
    public int Standalone { get; init; }
}

public sealed class ManagementControlViewProjector
{
    public ManagementControlView Build(CanonicalProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var evidence = project.ManagementEvidence;
        var resolver = new EffectiveEvidenceResolver();
        var reconciliationByObservation = evidence.Reconciliations
            .GroupBy(item => item.ObservationId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var readiness = evidence.Observations
            .Where(observation => observation.EvidenceKind == ManagementEvidenceKind.ReadinessCheck)
            .OrderBy(observation => observation.SourceRecordId, StringComparer.Ordinal)
            .Select(observation => ProjectReadinessRow(observation, evidence, resolver, reconciliationByObservation))
            .ToArray();

        var gateAttention = evidence.Diagnostics
            .Where(diagnostic => diagnostic.Code.Contains("GATE", StringComparison.OrdinalIgnoreCase))
            .Select(diagnostic => diagnostic.Code)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        var attentionGroups = BuildAttentionGroups(evidence, reconciliationByObservation);
        var gateId = evidence.Observations
            .Where(observation => observation.EvidenceKind is ManagementEvidenceKind.GateExecution or ManagementEvidenceKind.GateOutcome)
            .Select(observation => observation.GateId ?? observation.ExplicitTarget?.Id ?? observation.RawTargetId ?? observation.SourceRecordId)
            .Where(candidate => RegexGateId(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate, StringComparer.Ordinal)
            .FirstOrDefault();
        var executionSelection = gateId is null
            ? null
            : resolver.Select(evidence, "Gate", gateId, ManagementEvidenceKind.GateExecution, "StateCode");
        var outcomeSelection = gateId is null
            ? null
            : resolver.Select(evidence, "Gate", gateId, ManagementEvidenceKind.GateOutcome, "ResultCode");
        var controlObservation = evidence.Observations
            .Where(observation => observation.EvidenceKind == ManagementEvidenceKind.ControlEnvelope)
            .OrderBy(observation => observation.AuthorityRank ?? int.MaxValue)
            .ThenBy(observation => observation.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        var successorSelection = controlObservation is null
            ? null
            : resolver.Select(
                evidence,
                "ControlEnvelope",
                controlObservation.SourceRecordId,
                ManagementEvidenceKind.ControlEnvelope,
                "ProposedSuccessorIncrementId");
        var successor = successorSelection?.Status == EffectiveEvidenceSelectionStatus.Resolved
            ? successorSelection.SelectedObservation
            : null;
        var references = evidence.Observations
            .SelectMany(observation => observation.SourceReferences)
            .Where(reference => !Path.IsPathRooted(reference.RelativeFile))
            .Select(reference => reference.RelativeFile.Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        return new ManagementControlView
        {
            DiscoveryState = evidence.DiscoveryState,
            IncrementId = evidence.IncrementId,
            IncrementPhaseId = evidence.IncrementPhaseId,
            IncrementName = evidence.IncrementName,
            IncrementStatus = evidence.IncrementStatus,
            EvidenceScope = new ManagementEvidenceScopeView
            {
                IncrementPath = evidence.IncrementPath,
                ObservationCount = evidence.Observations.Count,
                SourceReferenceCount = evidence.Observations.SelectMany(observation => observation.SourceReferences).Count(),
                SourceFiles = references
            },
            CurrentGate = new ManagementGateView
            {
                GateId = gateId,
                ExecutionState = executionSelection?.Value,
                Outcome = outcomeSelection?.Value,
                ExecutionSelectionStatus = executionSelection?.Status.ToString().ToUpperInvariant(),
                OutcomeSelectionStatus = outcomeSelection?.Status.ToString().ToUpperInvariant(),
                AuthoritySourceSummary = DescribeAuthority(executionSelection, outcomeSelection),
                GateRecordStatus = gateId is null
                    ? null
                    : evidence.Observations.Any(observation =>
                        string.Equals(observation.GateId ?? observation.SourceRecordId, gateId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(observation.AuthorityKind, "actual-gate-record", StringComparison.OrdinalIgnoreCase))
                        ? "Recorded"
                        : "Not yet recorded",
                ProposedSuccessorIncrement = successor?.ProposedSuccessorIncrementId,
                ProposedSuccessorSummary = successor?.ProposedSuccessorSummary,
                IsDecisionPending = executionSelection?.Status == EffectiveEvidenceSelectionStatus.Conflict
                    || outcomeSelection?.Status == EffectiveEvidenceSelectionStatus.Conflict
                    || executionSelection?.Value is "NOT-RUN" or "IN-PROGRESS"
                    || outcomeSelection?.Value == "NOT-APPLICABLE",
                AttentionCodes = gateAttention
            },
            BaselineContext = new ManagementBaselineContextView
            {
                BaselineId = project.Baseline.Id,
                BaselineVersion = project.Baseline.Version,
                BaselineStatus = project.Baseline.Status,
                ProposedSuccessorIncrement = successor?.ProposedSuccessorIncrementId,
                ProposedSuccessorSummary = successor?.ProposedSuccessorSummary
            },
            Readiness = readiness,
            AttentionGroups = attentionGroups,
            Inspector = evidence.Observations
                .OrderBy(observation => observation.EvidenceKind)
                .ThenBy(observation => observation.SourceRecordId, StringComparer.Ordinal)
                .Select(observation => new ManagementEvidenceInspectorItem
                {
                    ObservationId = observation.Id,
                    EvidenceKind = observation.EvidenceKind,
                    SourceRecordId = observation.SourceRecordId,
                    StateCode = observation.StateCode,
                    ResultCode = observation.ResultCode,
                    OwnerRole = observation.OwnerRole,
                    OwnerRoleLabel = observation.OwnerRoleLabel,
                    WaitingForRole = observation.WaitingForRole,
                    WaitingForRoleLabel = observation.WaitingForRoleLabel,
                    RequiredAuthorityRole = observation.RequiredAuthorityRole,
                    RequiredAuthorityRoleLabel = observation.RequiredAuthorityRoleLabel,
                    Summary = observation.Summary,
                    ActionSummary = observation.ActionSummary,
                    PendingActionSummary = observation.PendingActionSummary,
                    BlockerSummary = observation.BlockerSummary,
                    DueConditionSummary = observation.DueConditionSummary,
                    GateEffectSummary = observation.GateEffectSummary,
                    AffectedTargetSummary = observation.AffectedTargetSummary,
                    CompletionCondition = observation.CompletionCondition,
                    AuthorityKind = observation.AuthorityKind,
                    ReconciliationStatus = reconciliationByObservation.GetValueOrDefault(observation.Id)?.Status.ToString(),
                    ReconciliationTarget = reconciliationByObservation.GetValueOrDefault(observation.Id)?.ResolvedTarget?.ToString(),
                    DueCondition = observation.DueCondition,
                    GateEffect = observation.GateEffect,
                    EvidenceLinks = observation.EvidenceLinks,
                    SourceReferences = observation.SourceReferences
                })
                .ToArray(),
            Reconciliation = new ManagementReconciliationSummary
            {
                Matched = evidence.Reconciliations.Count(item => item.Status == EvidenceReconciliationStatus.Matched),
                Standalone = evidence.Reconciliations.Count(item => item.Status == EvidenceReconciliationStatus.Standalone),
                Unmatched = evidence.Reconciliations.Count(item => item.Status == EvidenceReconciliationStatus.Unmatched),
                Ambiguous = evidence.Reconciliations.Count(item => item.Status == EvidenceReconciliationStatus.Ambiguous),
                Invalid = evidence.Reconciliations.Count(item => item.Status == EvidenceReconciliationStatus.Invalid)
            },
            Diagnostics = evidence.Diagnostics
        };
    }

    private static ManagementReadinessRow ProjectReadinessRow(
        ManagementEvidenceObservation observation,
        ManagementEvidence evidence,
        EffectiveEvidenceResolver resolver,
        IReadOnlyDictionary<string, EvidenceReconciliation> reconciliationByObservation)
    {
        var targetId = observation.ExplicitTarget?.Id ?? observation.RawTargetId ?? observation.SourceRecordId;
        var state = resolver.Select(evidence, "WorkPackage", targetId, ManagementEvidenceKind.ReadinessCheck, "StateCode");
        var result = resolver.Select(evidence, "WorkPackage", targetId, ManagementEvidenceKind.ReadinessCheck, "ResultCode");
        var owner = resolver.Select(evidence, "WorkPackage", targetId, ManagementEvidenceKind.ReadinessCheck, "OwnerRole");
        var waiting = resolver.Select(evidence, "WorkPackage", targetId, ManagementEvidenceKind.ReadinessCheck, "WaitingForRole");
        var pending = resolver.Select(evidence, "WorkPackage", targetId, ManagementEvidenceKind.ReadinessCheck, "PendingActionSummary");
        var due = resolver.Select(evidence, "WorkPackage", targetId, ManagementEvidenceKind.ReadinessCheck, "DueConditionSummary");
        var gate = resolver.Select(evidence, "WorkPackage", targetId, ManagementEvidenceKind.ReadinessCheck, "GateEffectCode");
        var blocker = resolver.Select(evidence, "WorkPackage", targetId, ManagementEvidenceKind.ReadinessCheck, "BlockerSummary");
        var selections = new[] { state, result, owner, waiting, pending, due, gate, blocker };
        var reconciliation = reconciliationByObservation.GetValueOrDefault(observation.Id);
        var selectedOwner = owner.SelectedObservation;
        var selectedWaiting = waiting.SelectedObservation;
        var selectedGate = gate.SelectedObservation;

        return new ManagementReadinessRow
        {
            WorkPackageId = targetId,
            TaskState = state.Value,
            ReadinessResult = result.Value,
            StateCode = state.Value,
            ResultCode = result.Value,
            OwnerRole = owner.Value,
            OwnerRoleLabel = selectedOwner?.OwnerRoleLabel,
            WaitingForRole = waiting.Value,
            WaitingForRoleLabel = selectedWaiting?.WaitingForRoleLabel,
            PendingActionSummary = pending.Value,
            DueCondition = selectedOwner?.DueCondition ?? selectedWaiting?.DueCondition ?? observation.DueCondition,
            DueConditionSummary = due.Value,
            GateEffect = gate.Value,
            GateEffectSummary = selectedGate?.GateEffectSummary,
            BlockerSummary = blocker.Value,
            EvidenceState = selections.Any(selection => selection.Status == EffectiveEvidenceSelectionStatus.Conflict)
                ? "CONFLICT"
                : observation.ValidationState.ToString().ToUpperInvariant(),
            SourceCount = evidence.Observations.Count(candidate =>
                candidate.EvidenceKind == ManagementEvidenceKind.ReadinessCheck
                && string.Equals(candidate.ExplicitTarget?.Id ?? candidate.RawTargetId ?? candidate.SourceRecordId, targetId, StringComparison.OrdinalIgnoreCase)),
            ReconciliationStatus = reconciliation?.Status ?? EvidenceReconciliationStatus.Unmatched
        };
    }

    private static bool RegexGateId(string? value) =>
        value is not null && Regex.IsMatch(value, "^PG\\d+$", RegexOptions.IgnoreCase);

    private static string? DescribeAuthority(
        EffectiveEvidenceSelection? execution,
        EffectiveEvidenceSelection? outcome)
    {
        var references = EffectiveReferences(execution)
            .Concat(EffectiveReferences(outcome))
            .Select(reference => reference.RelativeFile.Replace('\\', '/'))
            .Where(path => !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        return references.Length == 0 ? null : string.Join(" / ", references);
    }

    private static IReadOnlyList<SourceReference> EffectiveReferences(EffectiveEvidenceSelection? selection)
    {
        if (selection is null)
        {
            return Array.Empty<SourceReference>();
        }

        return selection.Status == EffectiveEvidenceSelectionStatus.Resolved
            ? selection.SelectedObservation?.SourceReferences ?? Array.Empty<SourceReference>()
            : selection.SourceReferences;
    }

    private static IReadOnlyList<ManagementAttentionGroup> BuildAttentionGroups(
        ManagementEvidence evidence,
        IReadOnlyDictionary<string, EvidenceReconciliation> reconciliationByObservation)
    {
        var groups = new List<ManagementAttentionGroup>();
        AddGroup(
            groups,
            "OPEN_DECISIONS",
            "Open decisions",
            evidence.Observations
                .Where(item => item.EvidenceKind == ManagementEvidenceKind.DecisionRecord && item.StateCode == "OPEN")
                .Select(item => Attention(item, BuildDecisionReason(item))));
        AddGroup(
            groups,
            "PENDING_HUMAN_ACTIONS",
            "Pending human actions",
            evidence.Observations
                .Where(item => item.EvidenceKind == ManagementEvidenceKind.HumanAction && item.StateCode is "NOT-RUN" or "OPEN")
                .Select(item => Attention(item, BuildHumanActionReason(item))));
        AddGroup(
            groups,
            "BLOCKERS",
            "Blockers and deviations",
            evidence.Observations
                .Where(item => item.ResultCode == "BLOCKED" || !string.IsNullOrWhiteSpace(item.BlockerOrDeviation))
                .Select(item => Attention(item, item.BlockerSummary ?? item.BlockerOrDeviation ?? "Evidence is blocked.")));
        AddGroup(
            groups,
            "RECONCILIATION_ISSUES",
            "Evidence reconciliation issues",
            evidence.Observations
                .Where(item => reconciliationByObservation.GetValueOrDefault(item.Id)?.Status is EvidenceReconciliationStatus.Ambiguous or EvidenceReconciliationStatus.Invalid)
                .Select(item => Attention(item, reconciliationByObservation[item.Id].Reason)));
        return groups;
    }

    private static string BuildDecisionReason(ManagementEvidenceObservation observation)
    {
        var parts = new List<string>();
        AddPart(parts, observation.Summary ?? observation.StateMeaning ?? "Decision remains open.");
        AddPart(parts, FormatRole("Waiting for", observation.RequiredAuthorityRoleLabel ?? observation.RequiredAuthorityRole));
        AddPart(parts, FormatRole("Due", observation.DueConditionSummary ?? observation.DueCondition));
        AddPart(parts, FormatRole("Gate effect", observation.GateEffectSummary ?? observation.GateEffect));
        return string.Join(" · ", parts);
    }

    private static string BuildHumanActionReason(ManagementEvidenceObservation observation)
    {
        var parts = new List<string>();
        AddPart(parts, observation.ActionSummary ?? observation.Summary ?? "Human action remains pending.");
        AddPart(parts, FormatRole("Waiting for", observation.WaitingForRoleLabel ?? observation.WaitingForRole ?? observation.RequiredAuthorityRoleLabel));
        AddPart(parts, FormatRole("Affects", observation.AffectedTargetSummary));
        AddPart(parts, FormatRole("Completion", observation.CompletionCondition));
        return string.Join(" · ", parts);
    }

    private static string? FormatRole(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"{label} {value}";

    private static void AddPart(ICollection<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(value.Trim());
        }
    }

    private static ManagementAttentionItem Attention(ManagementEvidenceObservation observation, string reason) =>
        new()
        {
            ObservationId = observation.Id,
            RecordId = observation.SourceRecordId,
            StateCode = observation.StateCode,
            ResultCode = observation.ResultCode,
            Summary = observation.Summary,
            ActionSummary = observation.ActionSummary,
            WaitingForRole = observation.WaitingForRole,
            RequiredAuthorityRole = observation.RequiredAuthorityRole,
            AffectedTargetSummary = observation.AffectedTargetSummary,
            GateEffect = observation.GateEffect,
            Reason = reason
        };

    private static void AddGroup(
        ICollection<ManagementAttentionGroup> groups,
        string code,
        string label,
        IEnumerable<ManagementAttentionItem> items)
    {
        var materialized = items.OrderBy(item => item.RecordId, StringComparer.Ordinal).ToArray();
        if (materialized.Length > 0)
        {
            groups.Add(new ManagementAttentionGroup
            {
                Code = code,
                Label = label,
                Items = materialized
            });
        }
    }
}
