using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed record ExecutionUpdate
{
    public string WorkItemId { get; init; } = string.Empty;
    public ExecutionState ExecutionState { get; init; } = ExecutionState.NotStarted;
    public DateOnly? ActualStart { get; init; }
    public DateOnly? ActualFinish { get; init; }
    public decimal? ActualEffortHours { get; init; }
    public decimal? RemainingEffortHours { get; init; }
    public DateTimeOffset? LastUpdatedAt { get; init; }
    public string? Note { get; init; }
    public SourceReference? EvidenceReference { get; init; }
}

public sealed record ExecutionUpdateResult
{
    public bool Accepted { get; init; }
    public CanonicalProject Project { get; init; } = new();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed class ExecutionOverlayUpdater
{
    public ExecutionUpdateResult Apply(CanonicalProject project, ExecutionUpdate update)
    {
        var card = project.DeliveryCards.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, update.WorkItemId?.Trim(), StringComparison.OrdinalIgnoreCase));
        var errors = Validate(card, update);
        if (errors.Count > 0)
        {
            return new ExecutionUpdateResult
            {
                Accepted = false,
                Project = project,
                Diagnostics = errors
            };
        }

        var record = new ExecutionRecord
        {
            WorkItemId = card!.Id,
            ExecutionState = update.ExecutionState,
            ActualStart = update.ActualStart,
            ActualStartState = StateFor(update.ActualStart),
            ActualFinish = update.ActualFinish,
            ActualFinishState = StateFor(update.ActualFinish),
            ActualEffortHours = update.ActualEffortHours,
            ActualEffortState = StateFor(update.ActualEffortHours),
            RemainingEffortHours = update.RemainingEffortHours,
            RemainingEffortState = StateFor(update.RemainingEffortHours),
            LastUpdatedAt = update.LastUpdatedAt,
            Note = update.Note,
            EvidenceReference = update.EvidenceReference
        };

        var records = project.ExecutionOverlay.Records
            .Where(existing => !string.Equals(existing.WorkItemId, record.WorkItemId, StringComparison.OrdinalIgnoreCase))
            .Append(record)
            .OrderBy(existing => existing.WorkItemId, StringComparer.Ordinal)
            .ToArray();

        return new ExecutionUpdateResult
        {
            Accepted = true,
            Project = project with
            {
                ExecutionOverlay = project.ExecutionOverlay with { Records = records },
                Analysis = null
            }
        };
    }

    private static IReadOnlyList<ImportWarning> Validate(DeliveryCard? card, ExecutionUpdate update)
    {
        var messages = new List<string>();
        if (card is null)
        {
            messages.Add($"Work item '{update.WorkItemId}' does not resolve to an executable delivery card.");
        }

        if (string.IsNullOrWhiteSpace(update.WorkItemId))
        {
            messages.Add("Work item ID is required.");
        }

        if (update.LastUpdatedAt is null)
        {
            messages.Add("Last-updated timestamp is required for a manual execution update.");
        }

        if (update.ActualStart is not null && update.ActualFinish is not null && update.ActualFinish < update.ActualStart)
        {
            messages.Add("Actual finish cannot precede actual start.");
        }

        if (update.ActualEffortHours is < 0m)
        {
            messages.Add("Actual effort cannot be negative.");
        }

        if (update.RemainingEffortHours is < 0m)
        {
            messages.Add("Remaining effort cannot be negative.");
        }

        if (update.ExecutionState == ExecutionState.InProgress && update.ActualStart is null)
        {
            messages.Add("In-progress work requires an actual start.");
        }

        if (update.ExecutionState == ExecutionState.InProgress && update.ActualFinish is not null)
        {
            messages.Add("In-progress work cannot have an actual finish.");
        }

        if (update.ExecutionState == ExecutionState.Completed && update.ActualFinish is null)
        {
            messages.Add("Completed work requires an actual finish.");
        }

        if (update.ExecutionState == ExecutionState.NotStarted
            && (update.ActualStart is not null || update.ActualFinish is not null))
        {
            messages.Add("Not-started work cannot have actual dates.");
        }

        return messages.Count == 0
            ? Array.Empty<ImportWarning>()
            : [new ImportWarning
            {
                Id = $"INVALID_EXECUTION_UPDATE:{update.WorkItemId}",
                Severity = WarningSeverity.Error,
                Code = "INVALID_EXECUTION_UPDATE",
                Message = string.Join(" ", messages),
                AffectedIds = string.IsNullOrWhiteSpace(update.WorkItemId) ? Array.Empty<string>() : [update.WorkItemId.Trim()]
            }];
    }

    private static DataState StateFor(DateOnly? value) => value is null ? DataState.Unknown : DataState.Known;
    private static DataState StateFor(decimal? value) => value is null ? DataState.Unknown : DataState.Known;
}
