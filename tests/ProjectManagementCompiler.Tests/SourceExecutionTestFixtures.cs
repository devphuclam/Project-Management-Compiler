using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class SourceExecutionTestFixtures
{
    public static CanonicalProject Apply(CanonicalProject project, ExecutionUpdate update)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(update);

        var card = project.DeliveryCards.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, update.WorkItemId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Test execution target '{update.WorkItemId}' was not found.");
        var statusDate = update.LastUpdatedAt is null
            ? new DateOnly(2026, 9, 28)
            : DateOnly.FromDateTime(update.LastUpdatedAt.Value.DateTime);
        var sourcePath = "tests/fixtures/execution-register.json";
        var existing = project.SourceExecution.Records
            .Where(record => record is not null)
            .ToDictionary(record => record.Entity.Id, StringComparer.OrdinalIgnoreCase);
        var records = project.DeliveryCards
            .Select(candidate =>
            {
                existing.TryGetValue(candidate.Id, out var previous);
                var isTarget = string.Equals(candidate.Id, card.Id, StringComparison.OrdinalIgnoreCase);
                return new SourceExecutionRecord
                {
                    Entity = CanonicalWorkItemKey.DeliveryCard(candidate.Id),
                    RecordingState = SourceRecordingState.Recorded,
                    ExecutionState = isTarget ? update.ExecutionState : previous?.ExecutionState ?? candidate.State ?? ExecutionState.NotStarted,
                    ResultState = isTarget ? SourceResultState.NotApplicable : previous?.ResultState ?? SourceResultState.NotApplicable,
                    ActualStart = isTarget ? update.ActualStart : previous?.ActualStart,
                    ActualFinish = isTarget ? update.ActualFinish : previous?.ActualFinish,
                    ActualEffortHours = isTarget ? update.ActualEffortHours : previous?.ActualEffortHours,
                    RemainingEffortHours = isTarget ? update.RemainingEffortHours : previous?.RemainingEffortHours,
                    ForecastFinish = isTarget ? null : previous?.ForecastFinish,
                    Blocker = isTarget ? update.Note : previous?.Blocker,
                    Evidence = isTarget ? Array.Empty<SourceExecutionEvidence>() : previous?.Evidence ?? Array.Empty<SourceExecutionEvidence>(),
                    LastUpdatedAt = isTarget ? update.LastUpdatedAt : previous?.LastUpdatedAt ?? update.LastUpdatedAt,
                    RecordedBy = isTarget ? "TEST" : previous?.RecordedBy ?? "TEST",
                    SourcePath = previous?.SourcePath ?? sourcePath
                };
            })
            .OrderBy(record => record.Entity.Id, StringComparer.Ordinal)
            .ToArray();

        var metadata = project.ImportMetadata ?? new ManifestSnapshotMetadata
        {
            RepositoryIdentity = "test/Project-Management-Compiler",
            ImportMode = ManifestImportMode.GitCommit,
            Classification = ManifestImportClassification.OfficialCommit,
            SourceIdentity = "test-commit",
            ManifestPath = "planning/project-management-compiler-manifest.json",
            ContractVersion = "0.1.0",
            ProjectId = project.Project.Id,
            BaselineId = project.Baseline.Id,
            RegisterRevision = 1,
            RegisterStatusDate = statusDate,
            ValidationResult = ManifestValidationResult.Pass,
            SourceReadiness = SourceReadinessState.Pass,
            SnapshotId = "snapshot-test-execution",
            ImportedAtUtc = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero),
            Calendars = new ProjectCalendarSet
            {
                TimeZone = "UTC",
                BaselineCalendarId = "test-baseline-calendar",
                ForecastCalendarId = "test-forecast-calendar"
            }
        };

        var sourceExecution = project.SourceExecution with
        {
            ProjectId = metadata.ProjectId,
            BaselineId = metadata.BaselineId,
            RegisterId = "test-execution-register",
            RegisterRevision = metadata.RegisterRevision,
            StatusDate = statusDate,
            TimeZone = "UTC",
            Records = records,
            SourcePath = sourcePath
        };
        return project with
        {
            SchemaVersion = "2.0",
            ImportMetadata = metadata,
            SourceExecution = sourceExecution,
            ExecutionOverlay = new ExecutionOverlay(),
            Analysis = null
        };
    }

    public static CompilationResult BuildResult(CanonicalProject project, DateOnly asOfDate) =>
        new ProjectCompiler().BuildImportedResult(
            new IdeaEngineeringSnapshot
            {
                Project = project,
                Metadata = project.ImportMetadata ?? throw new InvalidOperationException("Test project metadata is required."),
                SourceExecution = project.SourceExecution
            },
            asOfDate);
}
