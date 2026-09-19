using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed record EffectiveEvidenceSelection
{
    public string ObjectKind { get; init; } = string.Empty;
    public string ObjectId { get; init; } = string.Empty;
    public ManagementEvidenceKind EvidenceKind { get; init; }
    public string FieldName { get; init; } = string.Empty;
    public string? Value { get; init; }
    public EffectiveEvidenceSelectionStatus Status { get; init; }
    public ManagementEvidenceObservation? SelectedObservation { get; init; }
    public IReadOnlyList<ManagementEvidenceObservation> Candidates { get; init; } = Array.Empty<ManagementEvidenceObservation>();
    public IReadOnlyList<SourceReference> SourceReferences { get; init; } = Array.Empty<SourceReference>();
    public string Reason { get; init; } = string.Empty;
}

public sealed class EffectiveEvidenceResolver
{
    private static readonly string[] SemanticFields =
    [
        "StateCode",
        "ResultCode",
        "OwnerRole",
        "WaitingForRole",
        "RequiredAuthorityRole",
        "BlockerSummary",
        "PendingActionSummary",
        "DueConditionSummary",
        "GateEffectCode",
        "Summary",
        "ActionSummary",
        "AffectedTargetSummary",
        "CompletionCondition",
        "ProposedSuccessorIncrementId",
        "GateId"
    ];

    public IReadOnlyList<EffectiveEvidenceSelection> Resolve(ManagementEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        return evidence.Observations
            .SelectMany(observation => SemanticFields
                .Where(field => GetValue(observation, field) is not null)
                .Select(field => (Observation: observation, Field: field)))
            .GroupBy(item =>
            {
                var key = GetObjectKey(item.Observation);
                return (key.Kind, key.Id, item.Observation.EvidenceKind, item.Field);
            })
            .OrderBy(group => group.Key.Kind, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Id, StringComparer.Ordinal)
            .ThenBy(group => group.Key.EvidenceKind)
            .ThenBy(group => group.Key.Field, StringComparer.Ordinal)
            .Select(group => Select(
                evidence,
                group.Key.Kind,
                group.Key.Id,
                group.Key.EvidenceKind,
                group.Key.Field))
            .ToArray();
    }

    public EffectiveEvidenceSelection Select(
        ManagementEvidence evidence,
        string objectKind,
        string objectId,
        ManagementEvidenceKind evidenceKind,
        string fieldName)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var candidates = evidence.Observations
            .Where(observation => observation.EvidenceKind == evidenceKind)
            .Where(observation => MatchesObject(observation, objectKind, objectId))
            .Where(observation => GetValue(observation, fieldName) is not null)
            .OrderBy(observation => observation.AuthorityRank ?? int.MaxValue)
            .ThenBy(observation => observation.Id, StringComparer.Ordinal)
            .ToArray();

        var sourceReferences = candidates
            .SelectMany(observation => observation.SourceReferences)
            .Distinct()
            .ToArray();

        if (candidates.Length == 0)
        {
            return new EffectiveEvidenceSelection
            {
                ObjectKind = objectKind,
                ObjectId = objectId,
                EvidenceKind = evidenceKind,
                FieldName = fieldName,
                Status = EffectiveEvidenceSelectionStatus.Missing,
                SourceReferences = sourceReferences,
                Reason = "No attributable value was captured for this semantic field."
            };
        }

        var highestRank = candidates.Min(observation => observation.AuthorityRank ?? int.MaxValue);
        var highestAuthority = candidates
            .Where(observation => (observation.AuthorityRank ?? int.MaxValue) == highestRank)
            .ToArray();
        var distinctValues = highestAuthority
            .Select(observation => GetValue(observation, fieldName)!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (distinctValues.Length > 1)
        {
            return new EffectiveEvidenceSelection
            {
                ObjectKind = objectKind,
                ObjectId = objectId,
                EvidenceKind = evidenceKind,
                FieldName = fieldName,
                Status = EffectiveEvidenceSelectionStatus.Conflict,
                Candidates = highestAuthority,
                SourceReferences = highestAuthority.SelectMany(observation => observation.SourceReferences).Distinct().ToArray(),
                Reason = $"Equal-authority evidence contains conflicting values for {fieldName}."
            };
        }

        var selected = highestAuthority[0];
        return new EffectiveEvidenceSelection
        {
            ObjectKind = objectKind,
            ObjectId = objectId,
            EvidenceKind = evidenceKind,
            FieldName = fieldName,
            Value = distinctValues[0],
            Status = EffectiveEvidenceSelectionStatus.Resolved,
            SelectedObservation = selected,
            Candidates = candidates,
            SourceReferences = sourceReferences,
            Reason = $"Selected authority rank {highestRank}."
        };
    }

    private static bool MatchesObject(ManagementEvidenceObservation observation, string objectKind, string objectId)
    {
        var key = GetObjectKey(observation);
        return string.Equals(key.Kind, objectKind, StringComparison.OrdinalIgnoreCase)
            && string.Equals(key.Id, objectId, StringComparison.OrdinalIgnoreCase);
    }

    private static (string Kind, string Id) GetObjectKey(ManagementEvidenceObservation observation)
    {
        if (observation.ExplicitTarget is not null)
        {
            return (observation.ExplicitTarget.Kind, observation.ExplicitTarget.Id);
        }

        return observation.EvidenceKind switch
        {
            ManagementEvidenceKind.ReadinessCheck => ("WorkPackage", observation.RawTargetId ?? observation.SourceRecordId),
            ManagementEvidenceKind.GateExecution or ManagementEvidenceKind.GateOutcome => ("Gate", observation.GateId ?? observation.RawTargetId ?? observation.SourceRecordId),
            ManagementEvidenceKind.DecisionRecord => ("Decision", observation.SourceRecordId),
            ManagementEvidenceKind.HumanAction => ("HumanAction", observation.SourceRecordId),
            ManagementEvidenceKind.ChecklistContext => ("ChecklistContext", observation.SourceRecordId),
            ManagementEvidenceKind.ControlEnvelope => ("ControlEnvelope", observation.SourceRecordId),
            _ => (observation.EvidenceKind.ToString(), observation.SourceRecordId)
        };
    }

    private static string? GetValue(ManagementEvidenceObservation observation, string fieldName) =>
        fieldName switch
        {
            "StateCode" => observation.StateCode,
            "ResultCode" => observation.ResultCode,
            "OwnerRole" => observation.OwnerRole,
            "WaitingForRole" => observation.WaitingForRole,
            "RequiredAuthorityRole" => observation.RequiredAuthorityRole,
            "BlockerSummary" => observation.BlockerSummary,
            "PendingActionSummary" => observation.PendingActionSummary,
            "DueConditionSummary" => observation.DueConditionSummary ?? observation.DueCondition,
            "GateEffectCode" => observation.GateEffectCode ?? observation.GateEffect,
            "Summary" => observation.Summary,
            "ActionSummary" => observation.ActionSummary,
            "AffectedTargetSummary" => observation.AffectedTargetSummary,
            "CompletionCondition" => observation.CompletionCondition,
            "ProposedSuccessorIncrementId" => observation.ProposedSuccessorIncrementId,
            "GateId" => observation.GateId,
            _ => null
        };
}
