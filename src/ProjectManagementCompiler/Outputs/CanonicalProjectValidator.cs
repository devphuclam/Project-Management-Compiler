using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Outputs;

public static class CanonicalProjectValidator
{
    public static IReadOnlyList<ImportWarning> Validate(CanonicalProject project)
    {
        var diagnostics = new List<ImportWarning>();
        var cardIds = project.DeliveryCards
            .Select(card => card.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var records = project.ExecutionOverlay?.Records;
        if (records is null)
        {
            Add(diagnostics, "INVALID_EXECUTION_OVERLAY", "Execution overlay records are required.", null);
            return diagnostics;
        }

        var seenRecordIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
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

            if (record.LastUpdatedAt is null)
            {
                Add(diagnostics, "INVALID_EXECUTION_RECORD", $"Execution record '{workItemId}' requires lastUpdatedAt.", workItemId);
            }

            ValidateState(diagnostics, workItemId, record.ActualStart, record.ActualStartState, "actualStart");
            ValidateState(diagnostics, workItemId, record.ActualFinish, record.ActualFinishState, "actualFinish");
            ValidateState(diagnostics, workItemId, record.ActualEffortHours, record.ActualEffortState, "actualEffortHours");
            ValidateState(diagnostics, workItemId, record.RemainingEffortHours, record.RemainingEffortState, "remainingEffortHours");

            if (record.ActualStart is not null
                && record.ActualFinish is not null
                && record.ActualFinish < record.ActualStart)
            {
                Add(diagnostics, "INVALID_EXECUTION_DATE_RANGE", $"Execution record '{workItemId}' has an actual finish before its actual start.", workItemId);
            }

            if (record.ActualEffortHours is < 0m)
            {
                Add(diagnostics, "INVALID_EXECUTION_EFFORT", $"Execution record '{workItemId}' has negative actual effort.", workItemId);
            }

            if (record.RemainingEffortHours is < 0m)
            {
                Add(diagnostics, "INVALID_EXECUTION_EFFORT", $"Execution record '{workItemId}' has negative remaining effort.", workItemId);
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

        foreach (var dependency in project.Dependencies)
        {
            if (dependency.ValidationState == ValidationState.InvalidSourceEvidence && dependency.AnalysisEligible)
            {
                Add(
                    diagnostics,
                    "INVALID_DEPENDENCY_ANALYSIS_STATE",
                    $"Dependency '{dependency.SubjectId}' -> '{dependency.PredecessorId}' is invalid source evidence and cannot be analysis eligible.",
                    dependency.SubjectId);
            }
        }

        return diagnostics;
    }

    private static void ValidateState<T>(
        ICollection<ImportWarning> diagnostics,
        string workItemId,
        T? value,
        DataState state,
        string field)
        where T : struct
    {
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
            Id = $"{code}:{affectedId ?? "overlay"}",
            Severity = WarningSeverity.Error,
            Code = code,
            Message = message,
            AffectedIds = affectedId is null ? Array.Empty<string>() : [affectedId]
        });
    }
}
