using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Outputs;

namespace ProjectManagementCompiler.Tests;

internal static class CanonicalMigrationTests
{
    public static void Schema20PersistsSourceAuthorityAndIgnoresImportTimestampInDigest()
    {
        var project = CanonicalJsonTests.CaptureCanonicalProject() with
        {
            SchemaVersion = "2.0",
            ImportMetadata = new ManifestSnapshotMetadata
            {
                RepositoryIdentity = "devphuclam/IDEAEngineering",
                ImportMode = ManifestImportMode.GitCommit,
                Classification = ManifestImportClassification.OfficialCommit,
                SourceIdentity = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4",
                ManifestPath = "planning/project-management-compiler-manifest.json",
                ContractVersion = "0.1.0",
                ProjectId = "IDEA-ENGINEERING",
                BaselineId = "IE-PLAN-DEC2026-002@0.1",
                RegisterRevision = 1,
                RegisterStatusDate = new DateOnly(2026, 9, 19),
                ValidationResult = ManifestValidationResult.PassWithWarnings,
                SourceReadiness = SourceReadinessState.Pass,
                SnapshotId = "snapshot-test",
                ImportedAtUtc = new DateTimeOffset(2026, 9, 19, 1, 0, 0, TimeSpan.Zero)
            },
            SourceExecution = new SourceExecutionSnapshot
            {
                RegisterId = "register",
                RegisterRevision = 1,
                StatusDate = new DateOnly(2026, 9, 19),
                Records =
                [new SourceExecutionRecord
                {
                    Entity = CanonicalWorkItemKey.DeliveryCard("P01"),
                    RecordingState = SourceRecordingState.Recorded,
                    ExecutionState = ExecutionState.InProgress,
                    ResultState = SourceResultState.NotApplicable
                }]
            }
        };

        var serializer = new CanonicalJsonSerializer();
        var json = serializer.Serialize(project);
        TestAssert.Contains("\"schema\":\"2.0\"", json, "Schema 2.0 must use the canonical schema field.");
        TestAssert.False(json.Contains("\"schemaVersion\"", StringComparison.Ordinal), "Schema 2.0 must not emit the legacy schemaVersion field.");
        var reopened = serializer.Deserialize(json);
        TestAssert.Equal("2.0", reopened.SchemaVersion, "Schema 2.0 must reopen as version 2.0.");

        var later = project with { ImportMetadata = project.ImportMetadata! with { ImportedAtUtc = project.ImportMetadata.ImportedAtUtc.AddHours(3) } };
        TestAssert.Equal(CanonicalJsonDigest.Compute(project), CanonicalJsonDigest.Compute(later), "Import capture time must not alter the semantic digest.");
    }

    public static void Schema10OverlayReopensAsLocalProposalWithoutSourcePromotion()
    {
        var project = CanonicalJsonTests.CaptureCanonicalProject() with
        {
            ExecutionOverlay = new ExecutionOverlay
            {
                Records =
                [new ExecutionRecord
                {
                    WorkItemId = "P01-A",
                    ExecutionState = ExecutionState.InProgress,
                    ActualStart = new DateOnly(2026, 9, 19),
                    ActualStartState = DataState.Known,
                    LastUpdatedAt = new DateTimeOffset(2026, 9, 19, 1, 0, 0, TimeSpan.Zero)
                }]
            }
        };

        var reopened = new CanonicalJsonSerializer().Deserialize(new CanonicalJsonSerializer().Serialize(project));
        TestAssert.Equal(1, reopened.ExecutionProposals.Count, "Schema 1.0 overlay evidence must be represented as one local proposal.");
        TestAssert.Equal(0, reopened.SourceExecution.Records.Count, "Schema 1.0 overlay evidence must not become source execution.");
        TestAssert.True(reopened.Warnings.Any(warning => warning.Code == "PMC-MIGRATION-001"), "Schema migration must retain an explicit diagnostic.");
    }
}
