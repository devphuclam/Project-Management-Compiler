using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed record EffectiveExecutionRecord
{
    public string WorkItemId { get; init; } = string.Empty;
    public bool IsRecorded { get; init; }
    public ExecutionState? ExecutionState { get; init; }
    public DateOnly? ActualStart { get; init; }
    public DateOnly? ActualFinish { get; init; }
    public decimal? ActualEffortHours { get; init; }
    public decimal? RemainingEffortHours { get; init; }
    public DateTimeOffset? LastUpdatedAt { get; init; }
}

public static class ExecutionTruthResolver
{
    public static bool UsesSourceExecution(CanonicalProject project) =>
        project.ImportMetadata is not null;

    public static EffectiveExecutionRecord? ForCard(CanonicalProject project, string cardId)
    {
        if (!UsesSourceExecution(project))
        {
            return null;
        }

        var source = project.SourceExecution.Records
            .FirstOrDefault(record => string.Equals(record.Entity.Kind, "DeliveryCard", StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.Entity.Id, cardId, StringComparison.OrdinalIgnoreCase));
        return source is null
            ? null
            : new EffectiveExecutionRecord
            {
                WorkItemId = cardId,
                IsRecorded = source.RecordingState == SourceRecordingState.Recorded,
                ExecutionState = source.ExecutionState,
                ActualStart = source.ActualStart,
                ActualFinish = source.ActualFinish,
                ActualEffortHours = source.ActualEffortHours,
                RemainingEffortHours = source.RemainingEffortHours,
                LastUpdatedAt = source.LastUpdatedAt
            };
    }

    public static IReadOnlyList<EffectiveExecutionRecord> Recorded(CanonicalProject project) =>
        project.DeliveryCards
            .Select(card => ForCard(project, card.Id))
            .Where(record => record?.IsRecorded == true)
            .Select(record => record!)
            .ToArray();
}
