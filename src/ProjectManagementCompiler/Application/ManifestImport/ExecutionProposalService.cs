using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Application.ManifestImport;

public sealed record CreateExecutionProposalRequest
{
    public string? BaseSnapshotId { get; init; }
    public int? ExpectedRegisterRevision { get; init; }
    public string TargetKind { get; init; } = "DeliveryCard";
    public string TargetId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string?> ProposedChanges { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<SourceExecutionEvidence> Evidence { get; init; } = Array.Empty<SourceExecutionEvidence>();
    public ProposalLifecycle? RequestedLifecycle { get; init; }
}

public sealed record UpdateExecutionProposalRequest
{
    public IReadOnlyDictionary<string, string?>? ProposedChanges { get; init; }
    public IReadOnlyList<SourceExecutionEvidence>? Evidence { get; init; }
    public ProposalLifecycle? RequestedLifecycle { get; init; }
}

public sealed class ExecutionProposalService
{
    private static readonly HashSet<string> AllowedFields =
    [
        "executionState",
        "resultState",
        "actualStart",
        "actualFinish",
        "actualEffortHours",
        "remainingEffortHours",
        "forecastFinish",
        "blocker",
        "legacyEvidenceReference"
    ];

    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };

    private readonly CompilerApplicationState state;

    public ExecutionProposalService(CompilerApplicationState state)
    {
        this.state = state;
    }

    public ExecutionProposal Create(CreateExecutionProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var official = RequireOfficial();
        ValidateTarget(request.TargetKind, request.TargetId, official);
        var changes = NormalizeChanges(request.ProposedChanges);
        var evidence = NormalizeEvidence(request.Evidence);
        var baseSnapshotId = string.IsNullOrWhiteSpace(request.BaseSnapshotId)
            ? official.Metadata.SnapshotId
            : request.BaseSnapshotId.Trim();
        var expectedRevision = request.ExpectedRegisterRevision ?? official.Metadata.RegisterRevision;
        var proposal = new ExecutionProposal
        {
            Id = BuildProposalId(baseSnapshotId, expectedRevision, request.TargetKind, request.TargetId, changes, evidence),
            BaseSnapshotId = baseSnapshotId,
            ExpectedRegisterRevision = expectedRevision,
            TargetKind = request.TargetKind.Trim(),
            TargetId = request.TargetId.Trim(),
            ProposedChanges = changes,
            Evidence = evidence,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        proposal = Evaluate(proposal, request.RequestedLifecycle, official.Metadata);
        state.UpsertProposal(proposal);

        return proposal;
    }

    public ExecutionProposal Update(string id, UpdateExecutionProposalRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(request);
        var official = RequireOfficial();
        var existing = state.FindProposal(id)
            ?? throw new KeyNotFoundException($"Execution proposal '{id}' was not found.");

        var changes = existing.ProposedChanges;
        if (request.ProposedChanges is not null)
        {
            var merged = new Dictionary<string, string?>(existing.ProposedChanges, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in request.ProposedChanges)
            {
                merged[pair.Key] = pair.Value;
            }

            changes = NormalizeChanges(merged);
        }
        var evidence = request.Evidence is null ? existing.Evidence : NormalizeEvidence(request.Evidence);
        var updatedAtUtc = DateTimeOffset.UtcNow;
        if (updatedAtUtc < existing.CreatedAtUtc)
        {
            updatedAtUtc = existing.CreatedAtUtc;
        }

        var updated = existing with
        {
            ProposedChanges = changes,
            Evidence = evidence,
            UpdatedAtUtc = updatedAtUtc
        };
        updated = Evaluate(updated, request.RequestedLifecycle, official.Metadata);
        state.UpsertProposal(updated);

        return updated;
    }

    public IReadOnlyList<ExecutionProposal> List(string? snapshotId = null)
    {
        var official = state.CurrentOfficialSnapshot;
        return state.Proposals
            .Select(proposal => official is null ? proposal : Evaluate(proposal, null, official.Metadata))
            .Where(proposal => snapshotId is null || string.Equals(proposal.BaseSnapshotId, snapshotId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(proposal => proposal.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public ProposalPreviewResult Preview(string id)
    {
        var official = RequireOfficial();
        var proposal = List().SingleOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Execution proposal '{id}' was not found.");
        var diagnostics = proposal.Diagnostics.ToList();
        var records = official.Project.SourceExecution.Records.ToList();
        var target = records.FindIndex(record => string.Equals(record.Entity.Kind, proposal.TargetKind, StringComparison.OrdinalIgnoreCase)
            && string.Equals(record.Entity.Id, proposal.TargetId, StringComparison.OrdinalIgnoreCase));
        if (target < 0)
        {
            diagnostics.Add(Diagnostic("PMC-PROPOSAL-003", WarningSeverity.Error, $"Proposal target '{proposal.TargetKind}:{proposal.TargetId}' is not present in the official execution snapshot.", proposal.TargetKind, proposal.TargetId));
        }
        else
        {
            records[target] = ApplyChanges(records[target], proposal, diagnostics);
        }

        return new ProposalPreviewResult
        {
            IsAuthoritative = false,
            IsEstimated = true,
            Label = "PROPOSAL_PREVIEW · NON-AUTHORITATIVE · ESTIMATED",
            BaseSnapshotId = proposal.BaseSnapshotId,
            Diagnostics = diagnostics,
            EffectiveExecution = official.Project.SourceExecution with { Records = records }
        };
    }

    public string Export(string id)
    {
        var proposal = List().SingleOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Execution proposal '{id}' was not found.");
        return JsonSerializer.Serialize(new
        {
            schema = "1.0",
            proposalOnly = true,
            authority = "LOCAL_PROPOSAL",
            proposal
        }, ExportOptions);
    }

    private IdeaEngineeringSnapshot RequireOfficial() =>
        state.CurrentOfficialSnapshot
        ?? throw new InvalidOperationException("A valid official manifest snapshot is required before creating an execution proposal.");

    private ExecutionProposal Evaluate(
        ExecutionProposal proposal,
        ProposalLifecycle? requestedLifecycle,
        ManifestSnapshotMetadata official)
    {
        var diagnostics = proposal.Diagnostics.ToList();
        var stale = !string.Equals(proposal.BaseSnapshotId, official.SnapshotId, StringComparison.OrdinalIgnoreCase)
            || proposal.ExpectedRegisterRevision != official.RegisterRevision;
        if (stale)
        {
            return proposal with
            {
                Lifecycle = ProposalLifecycle.StaleBase,
                Diagnostics = diagnostics.Concat(
                    [Diagnostic("PMC-PROPOSAL-004", WarningSeverity.Warning, "The proposal base no longer matches the current official snapshot; it was not rebased.", proposal.TargetKind, proposal.TargetId)])
                    .DistinctBy(item => item.Code)
                    .ToArray()
            };
        }

        var requestedReady = requestedLifecycle == ProposalLifecycle.ReadyForReview || proposal.Lifecycle == ProposalLifecycle.ReadyForReview;
        if (proposal.Evidence.Count > 0 && !proposal.Evidence.All(IsControlledEvidenceValid))
        {
            diagnostics.Add(Diagnostic("PMC-PROPOSAL-007", WarningSeverity.Warning, "Proposal evidence must include an ID, type, description, recorded time, recorder, and a safe repository path or external URI before it can qualify completion.", proposal.TargetKind, proposal.TargetId));
        }

        if (requestedReady && !CompletionIsReady(proposal))
        {
            diagnostics.Add(Diagnostic("PMC-PROPOSAL-002", WarningSeverity.Warning, "A completion proposal remains DRAFT until finish, actual effort, zero remaining effort, and controlled evidence are present.", proposal.TargetKind, proposal.TargetId));
        }

        var lifecycle = requestedReady && CompletionIsReady(proposal)
            ? ProposalLifecycle.ReadyForReview
            : ProposalLifecycle.Draft;
        return proposal with
        {
            Lifecycle = lifecycle,
            Diagnostics = diagnostics.DistinctBy(item => item.Code).OrderBy(item => item.Code, StringComparer.Ordinal).ToArray()
        };
    }

    private static bool CompletionIsReady(ExecutionProposal proposal)
    {
        var state = proposal.ProposedChanges.GetValueOrDefault("executionState");
        var finish = proposal.ProposedChanges.GetValueOrDefault("actualFinish");
        var actual = proposal.ProposedChanges.GetValueOrDefault("actualEffortHours");
        var remaining = proposal.ProposedChanges.GetValueOrDefault("remainingEffortHours");
        return string.Equals(state, "COMPLETED", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(finish)
            && decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var actualHours)
            && actualHours >= 0
            && decimal.TryParse(remaining, NumberStyles.Number, CultureInfo.InvariantCulture, out var remainingHours)
            && remainingHours == 0
            && proposal.Evidence.Count > 0
            && proposal.Evidence.All(IsControlledEvidenceValid);
    }

    private static SourceExecutionRecord ApplyChanges(
        SourceExecutionRecord source,
        ExecutionProposal proposal,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        var changes = proposal.ProposedChanges;
        var executionState = source.ExecutionState;
        if (changes.TryGetValue("executionState", out var rawState)
            && TryParseExecutionState(rawState, out var parsedState))
        {
            executionState = parsedState;
        }

        var resultState = source.ResultState;
        if (changes.TryGetValue("resultState", out var rawResult)
            && TryParseResultState(rawResult, out var parsedResult))
        {
            resultState = parsedResult;
        }

        return source with
        {
            RecordingState = SourceRecordingState.Recorded,
            ExecutionState = executionState,
            ResultState = resultState,
            ActualStart = ParseDateOnly(changes, "actualStart", source.ActualStart, diagnostics, proposal),
            ActualFinish = ParseDateOnly(changes, "actualFinish", source.ActualFinish, diagnostics, proposal),
            ActualEffortHours = ParseDecimal(changes, "actualEffortHours", source.ActualEffortHours, diagnostics, proposal),
            RemainingEffortHours = ParseDecimal(changes, "remainingEffortHours", source.RemainingEffortHours, diagnostics, proposal),
            ForecastFinish = ParseDateTime(changes, "forecastFinish", source.ForecastFinish, diagnostics, proposal),
            Blocker = changes.GetValueOrDefault("blocker") ?? source.Blocker,
            Evidence = source.Evidence.Concat(proposal.Evidence).ToArray()
        };
    }

    private static DateOnly? ParseDateOnly(
        IReadOnlyDictionary<string, string?> changes,
        string key,
        DateOnly? fallback,
        ICollection<ManifestDiagnostic> diagnostics,
        ExecutionProposal proposal)
    {
        if (!changes.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : InvalidDate(key, raw, diagnostics, proposal, fallback);
    }

    private static DateTimeOffset? ParseDateTime(
        IReadOnlyDictionary<string, string?> changes,
        string key,
        DateTimeOffset? fallback,
        ICollection<ManifestDiagnostic> diagnostics,
        ExecutionProposal proposal)
    {
        if (!changes.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : InvalidDateTime(key, raw, diagnostics, proposal, fallback);
    }

    private static decimal? ParseDecimal(
        IReadOnlyDictionary<string, string?> changes,
        string key,
        decimal? fallback,
        ICollection<ManifestDiagnostic> diagnostics,
        ExecutionProposal proposal)
    {
        if (!changes.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : InvalidDecimal(key, raw, diagnostics, proposal, fallback);
    }

    private static DateOnly? InvalidDate(string key, string raw, ICollection<ManifestDiagnostic> diagnostics, ExecutionProposal proposal, DateOnly? fallback)
    {
        diagnostics.Add(Diagnostic("PMC-PROPOSAL-005", WarningSeverity.Error, $"Proposal field '{key}' value '{raw}' is not an ISO date.", proposal.TargetKind, proposal.TargetId));
        return fallback;
    }

    private static DateTimeOffset? InvalidDateTime(string key, string raw, ICollection<ManifestDiagnostic> diagnostics, ExecutionProposal proposal, DateTimeOffset? fallback)
    {
        diagnostics.Add(Diagnostic("PMC-PROPOSAL-005", WarningSeverity.Error, $"Proposal field '{key}' value '{raw}' is not an ISO date-time.", proposal.TargetKind, proposal.TargetId));
        return fallback;
    }

    private static decimal? InvalidDecimal(string key, string raw, ICollection<ManifestDiagnostic> diagnostics, ExecutionProposal proposal, decimal? fallback)
    {
        diagnostics.Add(Diagnostic("PMC-PROPOSAL-006", WarningSeverity.Error, $"Proposal field '{key}' value '{raw}' is not a non-negative number.", proposal.TargetKind, proposal.TargetId));
        return fallback;
    }

    private static IReadOnlyDictionary<string, string?> NormalizeChanges(IReadOnlyDictionary<string, string?> changes)
    {
        var normalized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in changes)
        {
            if (!AllowedFields.Contains(pair.Key))
            {
                throw new ArgumentException($"Proposal field '{pair.Key}' is not supported.", nameof(changes));
            }

            var value = pair.Value?.Trim();
            if (string.Equals(pair.Key, "legacyEvidenceReference", StringComparison.OrdinalIgnoreCase)
                && !ControlledEvidenceRules.IsSafeRepositoryPath(value))
            {
                throw new ArgumentException("Legacy evidence references must be safe repository-relative paths.", nameof(changes));
            }

            if (pair.Key is "actualEffortHours" or "remainingEffortHours"
                && value is not null
                && (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var hours)
                    || hours < 0
                    || decimal.Remainder(hours, 0.5m) != 0))
            {
                throw new ArgumentException($"Proposal field '{pair.Key}' must be a non-negative half-hour value.", nameof(changes));
            }

            normalized[pair.Key] = value;
        }

        return normalized;
    }

    private static IReadOnlyList<SourceExecutionEvidence> NormalizeEvidence(IEnumerable<SourceExecutionEvidence>? evidence)
    {
        var normalized = (evidence ?? Array.Empty<SourceExecutionEvidence>())
            .Select(NormalizeEvidenceItem)
            .ToArray();
        foreach (var item in normalized)
        {
            if (!ControlledEvidenceRules.IsValid(item))
            {
                throw new ArgumentException("Proposal evidence must satisfy the source-controlled evidence contract.", nameof(evidence));
            }
        }

        return normalized;
    }

    private static SourceExecutionEvidence NormalizeEvidenceItem(SourceExecutionEvidence? item)
    {
        var source = item ?? new SourceExecutionEvidence();
        return source with
        {
            EvidenceId = source.EvidenceId.Trim(),
            Type = source.Type.Trim(),
            RepositoryPath = NormalizeEvidencePath(source.RepositoryPath),
            ExternalUri = string.IsNullOrWhiteSpace(source.ExternalUri) ? null : source.ExternalUri.Trim(),
            Description = string.IsNullOrWhiteSpace(source.Description) ? null : source.Description.Trim(),
            Commit = string.IsNullOrWhiteSpace(source.Commit) ? null : source.Commit.Trim(),
            Result = string.IsNullOrWhiteSpace(source.Result) ? null : source.Result.Trim(),
            RecordedBy = string.IsNullOrWhiteSpace(source.RecordedBy) ? null : source.RecordedBy.Trim()
        };
    }

    private static string NormalizeEvidencePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!ManifestCaptureSupport.TryNormalizeRelativePath(value, out var normalized))
        {
            throw new ArgumentException("Proposal evidence paths must be safe repository-relative paths.", nameof(value));
        }

        if (normalized.StartsWith(".git/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, ".git", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Proposal evidence paths cannot point into the Git control directory.", nameof(value));
        }

        return normalized;
    }

    private static bool IsControlledEvidenceValid(SourceExecutionEvidence? item) =>
        ControlledEvidenceRules.IsValid(item);

    private static void ValidateTarget(string targetKind, string targetId, IdeaEngineeringSnapshot official)
    {
        if (!string.Equals(targetKind?.Trim(), "DeliveryCard", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Execution proposals currently target DeliveryCard entities only.", nameof(targetKind));
        }

        if (string.IsNullOrWhiteSpace(targetId)
            || !official.Project.DeliveryCards.Any(card => string.Equals(card.Id, targetId.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new KeyNotFoundException($"DeliveryCard '{targetId}' is not present in the current official planning snapshot.");
        }
    }

    private static bool TryParseExecutionState(string? value, out ExecutionState state)
    {
        var normalized = value?.Replace("_", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        return Enum.TryParse(normalized, true, out state);
    }

    private static bool TryParseResultState(string? value, out SourceResultState state)
    {
        var normalized = value?.Replace("_", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        return Enum.TryParse(normalized, true, out state);
    }

    private static string BuildProposalId(
        string baseSnapshotId,
        int revision,
        string kind,
        string id,
        IReadOnlyDictionary<string, string?> changes,
        IReadOnlyList<SourceExecutionEvidence> evidence)
    {
        var material = string.Join('|', baseSnapshotId, revision.ToString(CultureInfo.InvariantCulture), kind.Trim(), id.Trim(),
            string.Join(';', changes.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}={pair.Value}")),
            string.Join(';', evidence.Select(item => item.EvidenceId).OrderBy(value => value, StringComparer.Ordinal)));
        return $"proposal-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant()[..20]}";
    }

    private static ManifestDiagnostic Diagnostic(string code, WarningSeverity severity, string message, string kind, string id) =>
        new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            EntityKind = kind,
            EntityId = id,
            RecommendedAction = "Review the local proposal and compare it with the current official source snapshot."
        };

}
