using System.Globalization;
using System.Text.Json;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Extraction;

public sealed record ManifestExecutionAdapterResult
{
    public SourceExecutionSnapshot Snapshot { get; init; } = new();
    public IReadOnlyList<ManifestDiagnostic> Diagnostics { get; init; } = Array.Empty<ManifestDiagnostic>();
}

public sealed class ManifestExecutionAdapter
{
    public ManifestExecutionAdapterResult Adapt(
        ManifestContractDocument contract,
        ManifestSourceCapture capture,
        CanonicalProject planningProject)
    {
        var diagnostics = new List<ManifestDiagnostic>();
        if (!capture.Files.TryGetValue(contract.ExecutionRegisterPath, out var registerFile))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-PATH-002",
                WarningSeverity.Error,
                "The execution authority file was not captured.",
                contract.ExecutionRegisterPath,
                recommendedAction: "Restore the manifest-declared execution register."));
            return new ManifestExecutionAdapterResult { Diagnostics = diagnostics };
        }

        SourceExecutionSnapshot snapshot;
        try
        {
            using var document = JsonDocument.Parse(registerFile.Content);
            snapshot = ParseRegister(document.RootElement, contract, planningProject, registerFile.RelativePath, diagnostics);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SCHEMA-001",
                WarningSeverity.Error,
                $"The execution register is not valid JSON: {exception.Message}",
                registerFile.RelativePath,
                recommendedAction: "Repair the execution register or its declared schema."));
            snapshot = new SourceExecutionSnapshot { SourcePath = registerFile.RelativePath };
        }

        return new ManifestExecutionAdapterResult
        {
            Snapshot = snapshot,
            Diagnostics = diagnostics
        };
    }

    private static SourceExecutionSnapshot ParseRegister(
        JsonElement root,
        ManifestContractDocument contract,
        CanonicalProject planningProject,
        string sourcePath,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        var registerId = StringValue(root, "registerId");
        var projectId = StringValue(root, "projectId");
        var baselineId = StringValue(root, "baselineId");
        var revision = IntValue(root, "registerRevision");
        var statusDate = ParseDate(StringValue(root, "statusDate"), sourcePath, "statusDate", diagnostics);
        var timeZone = StringValue(root, "timeZone");
        if (revision != contract.ExpectedRegisterRevision)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SCHEMA-001",
                WarningSeverity.Error,
                $"Execution register revision {revision} does not match expected revision {contract.ExpectedRegisterRevision}.",
                sourcePath,
                "registerRevision",
                recommendedAction: "Import the manifest-declared accepted register revision."));
        }

        var records = new List<SourceExecutionRecord>();
        var seen = new HashSet<CanonicalWorkItemKey>();
        if (root.TryGetProperty("records", out var recordsElement)
            && recordsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var record in recordsElement.EnumerateArray())
            {
                var parsed = ParseRecord(record, sourcePath, planningProject, diagnostics);
                if (parsed is null)
                {
                    continue;
                }

                if (!seen.Add(parsed.Entity))
                {
                    diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                        "PMC-IDENTITY-001",
                        WarningSeverity.Error,
                        $"Execution register contains duplicate identity '{parsed.Entity}'.",
                        sourcePath,
                        "records",
                        parsed.Entity.Kind,
                        parsed.Entity.Id,
                        "Keep one authoritative execution record for each kind + id."));
                    continue;
                }

                records.Add(parsed);
            }
        }
        else
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SCHEMA-001",
                WarningSeverity.Error,
                "Execution register records must be an array.",
                sourcePath,
                "records",
                recommendedAction: "Restore the execution register records array."));
        }

        var cardKeys = planningProject.DeliveryCards
            .Select(card => CanonicalWorkItemKey.DeliveryCard(card.Id))
            .ToHashSet();
        foreach (var cardKey in cardKeys)
        {
            if (!seen.Contains(cardKey))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-IDENTITY-002",
                    WarningSeverity.Error,
                    $"Delivery card '{cardKey.Id}' is absent from the execution register.",
                    sourcePath,
                    "records",
                    cardKey.Kind,
                    cardKey.Id,
                    "Add an explicit NOT_RECORDED record or controlled disposition."));
            }
        }

        return new SourceExecutionSnapshot
        {
            ProjectId = projectId,
            BaselineId = baselineId,
            RegisterId = registerId,
            RegisterRevision = revision,
            StatusDate = statusDate,
            TimeZone = timeZone,
            Records = records.OrderBy(record => record.Entity.Kind, StringComparer.Ordinal)
                .ThenBy(record => record.Entity.Id, StringComparer.Ordinal)
                .ToArray(),
            SourcePath = sourcePath
        };
    }

    private static SourceExecutionRecord? ParseRecord(
        JsonElement element,
        string sourcePath,
        CanonicalProject planningProject,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("entity", out var entity)
            || entity.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SCHEMA-001",
                WarningSeverity.Error,
                "Execution record requires an entity object.",
                sourcePath,
                "records",
                recommendedAction: "Add a typed DeliveryCard entity reference."));
            return null;
        }

        var kind = StringValue(entity, "kind");
        var id = StringValue(entity, "id");
        var key = new CanonicalWorkItemKey(kind, id);
        if (!string.Equals(key.Kind, "DeliveryCard", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(key.Id))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-IDENTITY-003",
                WarningSeverity.Error,
                $"Execution record target '{kind}:{id}' is not a valid DeliveryCard identity.",
                sourcePath,
                "entity",
                kind,
                id,
                "Use an existing DeliveryCard kind + id."));
            return null;
        }

        if (!planningProject.DeliveryCards.Any(card => string.Equals(card.Id, key.Id, StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-IDENTITY-003",
                WarningSeverity.Error,
                $"Execution register contains unknown Delivery Card '{key.Id}'.",
                sourcePath,
                "entity",
                key.Kind,
                key.Id,
                "Add the card through planning authority or remove the unknown record."));
        }

        var recording = ParseRecordingState(StringValue(element, "recordingState"), sourcePath, key, diagnostics);
        var execution = ParseExecutionState(StringValue(element, "executionState"), sourcePath, key, diagnostics);
        var result = ParseResultState(StringValue(element, "resultState"), sourcePath, key, diagnostics);
        var actualStart = ParseDateTimeDate(element, "actualStart", sourcePath, key, diagnostics);
        var actualFinish = ParseDateTimeDate(element, "actualFinish", sourcePath, key, diagnostics);
        var actualEffort = ParseEffort(element, "actualEffortHours", sourcePath, key, diagnostics);
        var remainingEffort = ParseEffort(element, "remainingEffortHours", sourcePath, key, diagnostics);
        var forecastFinish = ParseDateTime(element, "forecastFinish", sourcePath, key, diagnostics);
        var lastUpdatedAt = ParseDateTime(element, "lastUpdatedAt", sourcePath, key, diagnostics);
        var evidence = ParseEvidence(element, sourcePath, key, diagnostics);
        var blocker = ParseBlocker(element, sourcePath);

        if (recording == SourceRecordingState.NotRecorded
            && (execution is not null || result is not null || actualStart is not null || actualFinish is not null
                || actualEffort is not null || remainingEffort is not null))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-STATE-001",
                WarningSeverity.Error,
                $"NOT_RECORDED card '{key.Id}' cannot carry recorded execution fields.",
                sourcePath,
                "recordingState",
                key.Kind,
                key.Id,
                "Keep absent source evidence unknown instead of inferring execution."));
        }

        if (recording == SourceRecordingState.Recorded
            && (execution is null || result is null || lastUpdatedAt is null))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-STATE-001",
                WarningSeverity.Error,
                $"RECORDED card '{key.Id}' requires execution state, result state, and lastUpdatedAt.",
                sourcePath,
                "recordingState",
                key.Kind,
                key.Id,
                "Complete the attributable execution record."));
        }

        if (execution == ExecutionState.Completed
            && (actualFinish is null || actualEffort is null || remainingEffort != 0 || evidence.Count == 0))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-COMPLETE-001",
                WarningSeverity.Error,
                $"Completed card '{key.Id}' lacks finish, actual effort, zero remaining effort, or controlled evidence.",
                sourcePath,
                "executionState",
                key.Kind,
                key.Id,
                "Supply controlled completion evidence or revert the state."));
        }

        return new SourceExecutionRecord
        {
            Entity = key,
            RecordingState = recording,
            ExecutionState = execution,
            ResultState = result,
            ActualStart = actualStart,
            ActualFinish = actualFinish,
            ActualEffortHours = actualEffort,
            RemainingEffortHours = remainingEffort,
            ForecastFinish = forecastFinish,
            Blocker = blocker,
            Evidence = evidence,
            LastUpdatedAt = lastUpdatedAt,
            RecordedBy = StringValue(element, "recordedBy"),
            SourcePath = sourcePath
        };
    }

    private static IReadOnlyList<SourceExecutionEvidence> ParseEvidence(
        JsonElement element,
        string sourcePath,
        CanonicalWorkItemKey key,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (!element.TryGetProperty("evidence", out var evidenceElement)
            || evidenceElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<SourceExecutionEvidence>();
        }

        var evidence = new List<SourceExecutionEvidence>();
        foreach (var item in evidenceElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var repoPath = NullableString(item, "repoPath");
            if (repoPath is not null
                && (!ManifestCaptureSupport.TryNormalizeRelativePath(repoPath, out var normalized)
                    || (normalized.StartsWith(".git/", StringComparison.OrdinalIgnoreCase))))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-EVIDENCE-001",
                    WarningSeverity.Error,
                    $"Evidence for '{key.Id}' uses an unsafe repository path.",
                    sourcePath,
                    "evidence.repoPath",
                    key.Kind,
                    key.Id,
                    "Use a repository-relative controlled evidence path."));
                repoPath = null;
            }

            evidence.Add(new SourceExecutionEvidence
            {
                EvidenceId = StringValue(item, "evidenceId"),
                Type = StringValue(item, "type"),
                RepositoryPath = repoPath ?? string.Empty,
                Commit = NullableString(item, "commit"),
                ExternalUri = NullableString(item, "externalUri"),
                Description = NullableString(item, "description"),
                Result = NullableString(item, "result"),
                RecordedAt = ParseDateTime(item, "recordedAt", sourcePath, key, diagnostics),
                RecordedBy = NullableString(item, "recordedBy")
            });
        }

        return evidence;
    }

    private static string? ParseBlocker(JsonElement element, string sourcePath)
    {
        if (!element.TryGetProperty("blockers", out var blockers)
            || blockers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return blockers.EnumerateArray()
            .Where(blocker => blocker.ValueKind == JsonValueKind.Object)
            .Select(blocker => NullableString(blocker, "description"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static SourceRecordingState ParseRecordingState(
        string value,
        string sourcePath,
        CanonicalWorkItemKey key,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        return value.ToUpperInvariant() switch
        {
            "RECORDED" => SourceRecordingState.Recorded,
            "NOT_RECORDED" => SourceRecordingState.NotRecorded,
            _ => InvalidRecordingState(sourcePath, key, diagnostics)
        };
    }

    private static ExecutionState? ParseExecutionState(
        string value,
        string sourcePath,
        CanonicalWorkItemKey key,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (value.Length == 0)
        {
            return null;
        }

        var state = value.ToUpperInvariant() switch
        {
            "NOT_STARTED" => ExecutionState.NotStarted,
            "IN_PROGRESS" => ExecutionState.InProgress,
            "COMPLETED" => ExecutionState.Completed,
            "SUSPENDED" => ExecutionState.Suspended,
            "CANCELLED" => ExecutionState.Cancelled,
            _ => (ExecutionState?)null
        };
        if (state is not null)
        {
            return state.Value;
        }

        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
            "PMC-STATE-001",
            WarningSeverity.Error,
            $"Execution state '{value}' for '{key.Id}' is unsupported.",
            sourcePath,
            "executionState",
            key.Kind,
            key.Id,
            "Use the controlled Execution State vocabulary."));
        return null;
    }

    private static SourceResultState? ParseResultState(
        string value,
        string sourcePath,
        CanonicalWorkItemKey key,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (value.Length == 0)
        {
            return null;
        }

        return value.ToUpperInvariant() switch
        {
            "NOT_RUN" => SourceResultState.NotRun,
            "PASS" => SourceResultState.Pass,
            "FAIL" => SourceResultState.Fail,
            "BLOCKED" => SourceResultState.Blocked,
            "NOT_APPLICABLE" => SourceResultState.NotApplicable,
            _ => InvalidResultState(sourcePath, key, diagnostics)
        };
    }

    private static SourceRecordingState InvalidRecordingState(
        string sourcePath,
        CanonicalWorkItemKey key,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
            "PMC-STATE-001",
            WarningSeverity.Error,
            $"Recording state for '{key.Id}' is unsupported.",
            sourcePath,
            "recordingState",
            key.Kind,
            key.Id,
            "Use RECORDED or NOT_RECORDED."));
        return SourceRecordingState.NotRecorded;
    }

    private static SourceResultState? InvalidResultState(
        string sourcePath,
        CanonicalWorkItemKey key,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
            "PMC-STATE-001",
            WarningSeverity.Error,
            $"Result state for '{key.Id}' is unsupported.",
            sourcePath,
            "resultState",
            key.Kind,
            key.Id,
            "Use the controlled Result State vocabulary."));
        return null;
    }

    private static decimal? ParseEffort(
        JsonElement element,
        string propertyName,
        string sourcePath,
        CanonicalWorkItemKey key,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (!value.TryGetDecimal(out var effort)
            || effort < 0
            || effort % 0.5m != 0)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-EFFORT-001",
                WarningSeverity.Error,
                $"Effort field '{propertyName}' for '{key.Id}' is negative, non-numeric, or not a 0.5-hour increment.",
                sourcePath,
                propertyName,
                key.Kind,
                key.Id,
                "Record a non-negative effort value in 0.5-hour increments."));
            return null;
        }

        return effort;
    }

    private static DateOnly? ParseDateTimeDate(
        JsonElement element,
        string propertyName,
        string sourcePath,
        CanonicalWorkItemKey key,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        var value = ParseDateTime(element, propertyName, sourcePath, key, diagnostics);
        return value is null ? null : DateOnly.FromDateTime(value.Value.DateTime);
    }

    private static DateTimeOffset? ParseDateTime(
        JsonElement element,
        string propertyName,
        string sourcePath,
        CanonicalWorkItemKey key,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        var raw = NullableString(element, propertyName);
        if (raw is null)
        {
            return null;
        }

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value))
        {
            return value;
        }

        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
            "PMC-SCHEMA-001",
            WarningSeverity.Error,
            $"Date-time field '{propertyName}' for '{key.Id}' is invalid.",
            sourcePath,
            propertyName,
            key.Kind,
            key.Id,
            "Use an ISO-8601 date-time value."));
        return null;
    }

    private static DateOnly? ParseDate(
        string value,
        string sourcePath,
        string field,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        if (value.Length > 0)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SCHEMA-001",
                WarningSeverity.Error,
                $"Date field '{field}' is invalid.",
                sourcePath,
                field,
                recommendedAction: "Use an ISO-8601 date value."));
        }

        return null;
    }

    private static string StringValue(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static string? NullableString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int IntValue(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
            ? result
            : 0;
}
