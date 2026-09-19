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
    public string GateId { get; init; } = "PG4";
    public string? ExecutionState { get; init; }
    public string? Outcome { get; init; }
    public bool IsDecisionPending { get; init; }
    public IReadOnlyList<string> AttentionCodes { get; init; } = Array.Empty<string>();
}

public sealed record ManagementBaselineContextView
{
    public string BaselineId { get; init; } = string.Empty;
    public string BaselineVersion { get; init; } = string.Empty;
    public string BaselineStatus { get; init; } = string.Empty;
    public string? ProposedSuccessorIncrement { get; init; }
}

public sealed record ManagementReadinessRow
{
    public string WorkPackageId { get; init; } = string.Empty;
    public string? StateCode { get; init; }
    public string? ResultCode { get; init; }
    public string? OwnerRole { get; init; }
    public string? GateEffect { get; init; }
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
}

public sealed class ManagementControlViewProjector
{
    public ManagementControlView Build(CanonicalProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var evidence = project.ManagementEvidence;
        var reconciliationByObservation = evidence.Reconciliations
            .GroupBy(item => item.ObservationId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var readiness = evidence.Observations
            .Where(observation => observation.EvidenceKind == ManagementEvidenceKind.ReadinessCheck)
            .OrderBy(observation => observation.SourceRecordId, StringComparer.Ordinal)
            .Select(observation => new ManagementReadinessRow
            {
                WorkPackageId = observation.SourceRecordId,
                StateCode = observation.StateCode,
                ResultCode = observation.ResultCode,
                OwnerRole = observation.OwnerRole,
                GateEffect = observation.GateEffect,
                ReconciliationStatus = reconciliationByObservation.GetValueOrDefault(observation.Id)?.Status
                    ?? EvidenceReconciliationStatus.Unmatched
            })
            .ToArray();

        var execution = evidence.Observations.FirstOrDefault(item => item.EvidenceKind == ManagementEvidenceKind.GateExecution);
        var outcome = evidence.Observations.FirstOrDefault(item => item.EvidenceKind == ManagementEvidenceKind.GateOutcome);
        var gateAttention = evidence.Diagnostics
            .Where(diagnostic => diagnostic.Code.Contains("GATE", StringComparison.OrdinalIgnoreCase))
            .Select(diagnostic => diagnostic.Code)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        var attentionGroups = BuildAttentionGroups(evidence, reconciliationByObservation);
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
                ExecutionState = execution?.StateCode,
                Outcome = outcome?.ResultCode,
                IsDecisionPending = execution?.StateCode is "NOT-RUN" or "IN-PROGRESS"
                    || outcome?.ResultCode == "NOT-APPLICABLE",
                AttentionCodes = gateAttention
            },
            BaselineContext = new ManagementBaselineContextView
            {
                BaselineId = project.Baseline.Id,
                BaselineVersion = project.Baseline.Version,
                BaselineStatus = project.Baseline.Status,
                ProposedSuccessorIncrement = evidence.Observations
                    .FirstOrDefault(item => item.EvidenceKind == ManagementEvidenceKind.ControlEnvelope)
                    ?.GateEffect
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
                    DueCondition = observation.DueCondition,
                    GateEffect = observation.GateEffect,
                    EvidenceLinks = observation.EvidenceLinks,
                    SourceReferences = observation.SourceReferences
                })
                .ToArray(),
            Reconciliation = new ManagementReconciliationSummary
            {
                Matched = evidence.Reconciliations.Count(item => item.Status == EvidenceReconciliationStatus.Matched),
                Unmatched = evidence.Reconciliations.Count(item => item.Status == EvidenceReconciliationStatus.Unmatched),
                Ambiguous = evidence.Reconciliations.Count(item => item.Status == EvidenceReconciliationStatus.Ambiguous),
                Invalid = evidence.Reconciliations.Count(item => item.Status == EvidenceReconciliationStatus.Invalid)
            },
            Diagnostics = evidence.Diagnostics
        };
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
                .Select(item => Attention(item, "Decision remains open.")));
        AddGroup(
            groups,
            "PENDING_HUMAN_ACTIONS",
            "Pending human actions",
            evidence.Observations
                .Where(item => item.EvidenceKind == ManagementEvidenceKind.HumanAction && item.StateCode is "NOT-RUN" or "OPEN")
                .Select(item => Attention(item, "Human action has no attributable completion.")));
        AddGroup(
            groups,
            "BLOCKERS",
            "Blockers and deviations",
            evidence.Observations
                .Where(item => item.ResultCode == "BLOCKED" || !string.IsNullOrWhiteSpace(item.BlockerOrDeviation))
                .Select(item => Attention(item, item.BlockerOrDeviation ?? "Evidence is blocked.")));
        AddGroup(
            groups,
            "RECONCILIATION_ISSUES",
            "Evidence reconciliation issues",
            evidence.Observations
                .Where(item => reconciliationByObservation.GetValueOrDefault(item.Id)?.Status is EvidenceReconciliationStatus.Ambiguous or EvidenceReconciliationStatus.Invalid)
                .Select(item => Attention(item, reconciliationByObservation[item.Id].Reason)));
        return groups;
    }

    private static ManagementAttentionItem Attention(ManagementEvidenceObservation observation, string reason) =>
        new()
        {
            ObservationId = observation.Id,
            RecordId = observation.SourceRecordId,
            StateCode = observation.StateCode,
            ResultCode = observation.ResultCode,
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
