using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;
using System.Text.Json.Nodes;

namespace ProjectManagementCompiler.Tests;

internal static class CanonicalMigrationTests
{
    public static void Schema20PersistsSourceAuthorityAndIgnoresImportTimestampInDigest()
    {
        var project = CreateValidSchema20Project();

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

    public static void Schema10OverlayIsNeutralizedFromAllExecutionProjections()
    {
        var project = CanonicalJsonTests.CaptureCanonicalProject() with
        {
            ExecutionOverlay = new ExecutionOverlay
            {
                Records =
                [new ExecutionRecord
                {
                    WorkItemId = "P01-A",
                    ExecutionState = ExecutionState.Completed,
                    ActualStart = new DateOnly(2026, 9, 19),
                    ActualStartState = DataState.Known,
                    ActualFinish = new DateOnly(2026, 9, 25),
                    ActualFinishState = DataState.Known,
                    ActualEffortHours = 8m,
                    ActualEffortState = DataState.Known,
                    RemainingEffortHours = 0m,
                    RemainingEffortState = DataState.Known,
                    LastUpdatedAt = new DateTimeOffset(2026, 9, 25, 1, 0, 0, TimeSpan.Zero)
                }]
            }
        };

        var reopened = new CanonicalJsonSerializer().Deserialize(new CanonicalJsonSerializer().Serialize(project));
        var analysis = new ManagementAnalysisOrchestrator().Analyze(reopened, new DateOnly(2026, 9, 30));
        var cleanAnalysis = new ManagementAnalysisOrchestrator().Analyze(CanonicalJsonTests.CaptureCanonicalProject(), new DateOnly(2026, 9, 30));
        var gantt = new GanttProjector().Build(reopened, analysis, new DateOnly(2026, 9, 30));
        var item = gantt.Items.Single(candidate => candidate.WorkItemId == "P01-A");

        TestAssert.Equal(0, reopened.ExecutionOverlay.Records.Count, "Schema 1.0 migration must clear the legacy overlay after copying values to proposals.");
        TestAssert.Equal(1, reopened.ExecutionProposals.Count, "Schema 1.0 migration must retain the legacy value only as a proposal.");
        TestAssert.True(ExecutionTruthResolver.ForCard(reopened, "P01-A") is null, "Execution truth must not resolve a neutralized legacy overlay.");
        TestAssert.Equal(
            string.Join("|", cleanAnalysis.Alerts.Select(alert => $"{alert.WorkItemId}:{alert.AlertCode}:{alert.VarianceWorkingMinutes}")),
            string.Join("|", analysis.Alerts.Select(alert => $"{alert.WorkItemId}:{alert.AlertCode}:{alert.VarianceWorkingMinutes}")),
            "Legacy overlay values must not change the alert projection.");
        TestAssert.Equal(cleanAnalysis.ExecutionEffort.ActualEffortHours, analysis.ExecutionEffort.ActualEffortHours, "Legacy overlay values must not affect actual effort analysis.");
        TestAssert.False(item.Lanes.Any(lane => lane.Lane == GanttLane.Actual), "Legacy overlay values must not create a Gantt ACTUAL lane.");
    }

    public static void Schema20RejectsTamperedAuthorityDataAfterDeserialization()
    {
        var serializer = new CanonicalJsonSerializer();
        var project = CreateValidSchema20Project();
        var validJson = serializer.Serialize(project);

        var invalidMetadata = JsonNode.Parse(validJson)!.AsObject();
        invalidMetadata["importMetadata"]!["projectId"] = "";
        TestAssert.Throws<InvalidDataException>(
            () => serializer.Deserialize(invalidMetadata.ToJsonString()),
            "Schema 2.0 must reject empty import metadata identity after JSON deserialization.");

        var invalidSource = JsonNode.Parse(validJson)!.AsObject();
        var sourceRecords = (JsonArray)invalidSource["sourceExecution"]!["records"]!;
        ((JsonObject)sourceRecords[0]!["entity"]!)["id"] = "X99";
        TestAssert.Throws<InvalidDataException>(
            () => serializer.Deserialize(invalidSource.ToJsonString()),
            "Schema 2.0 must reject source execution records whose typed target is not canonical.");

        var projectWithProposal = project with
        {
            ExecutionProposals =
            [new ExecutionProposal
            {
                Id = "proposal-1",
                BaseSnapshotId = "snapshot-test",
                ExpectedRegisterRevision = 1,
                TargetKind = "DeliveryCard",
                TargetId = "P01",
                Lifecycle = ProposalLifecycle.Draft,
                ProposedChanges = new Dictionary<string, string?>
                {
                    ["executionState"] = "IN_PROGRESS"
                },
                CreatedAtUtc = new DateTimeOffset(2026, 9, 19, 1, 0, 0, TimeSpan.Zero),
                UpdatedAtUtc = new DateTimeOffset(2026, 9, 19, 1, 0, 0, TimeSpan.Zero)
            }]
        };
        var invalidProposal = JsonNode.Parse(serializer.Serialize(projectWithProposal))!.AsObject();
        var proposals = (JsonArray)invalidProposal["executionProposals"]!;
        ((JsonObject)proposals[0]!)["targetId"] = "X99";
        TestAssert.Throws<InvalidDataException>(
            () => serializer.Deserialize(invalidProposal.ToJsonString()),
            "Schema 2.0 must reject proposals whose typed target is not canonical.");
    }

    public static void Schema20RejectsSemanticAuthorityTampering()
    {
        var serializer = new CanonicalJsonSerializer();
        var project = CreateValidSchema20Project();
        var validJson = serializer.Serialize(project);

        AssertTamperedJsonRejected(serializer, validJson, root =>
        {
            root["importMetadata"]!["sourceIdentity"] = "abc";
        }, "Schema 2.0 must reject a Git source identity that is not a full SHA.");
        AssertTamperedJsonRejected(serializer, validJson, root =>
        {
            root["importMetadata"]!["repositoryIdentity"] = "https://user:pass@example.com/repository";
        }, "Schema 2.0 must reject repository identities containing URI user info.");
        AssertTamperedJsonRejected(serializer, validJson, root =>
        {
            root["importMetadata"]!["registerRevision"] = 0;
        }, "Schema 2.0 must reject a non-positive import register revision.");
        AssertTamperedJsonRejected(serializer, validJson, root =>
        {
            root["importMetadata"]!["projectId"] = "OTHER-PROJECT";
        }, "Schema 2.0 must reject metadata whose project identity differs from the canonical project.");
        AssertTamperedJsonRejected(serializer, validJson, root =>
        {
            root["sourceExecution"]!["projectId"] = "OTHER-PROJECT";
        }, "Schema 2.0 must reject source execution whose project identity differs from metadata.");
        AssertTamperedJsonRejected(serializer, validJson, root =>
        {
            root["sourceExecution"]!["registerRevision"] = 0;
        }, "Schema 2.0 must reject a non-positive source register revision.");
        AssertTamperedJsonRejected(serializer, validJson, root =>
        {
            root["sourceExecution"]!["statusDate"] = "2026-09-20";
        }, "Schema 2.0 must reject a source status date that differs from metadata.");
        var fractionalSourceProject = project with
        {
            SourceExecution = project.SourceExecution with
            {
                Records = project.SourceExecution.Records
                    .Select(record => record with
                    {
                        ActualEffortHours = 1.25m
                    })
                    .ToArray()
            }
        };
        var fractionalSourceReopened = serializer.Deserialize(serializer.Serialize(fractionalSourceProject));
        TestAssert.Equal(1.25m, fractionalSourceReopened.SourceExecution.Records.Single().ActualEffortHours,
            "Schema 2.0 must preserve non-negative decimal effort received from the authoritative source.");

        var projectWithProposal = project with
        {
            ExecutionProposals =
            [new ExecutionProposal
            {
                Id = "proposal-semantic-test",
                BaseSnapshotId = "snapshot-test",
                ExpectedRegisterRevision = 1,
                TargetKind = "DeliveryCard",
                TargetId = "P01",
                Lifecycle = ProposalLifecycle.Draft,
                ProposedChanges = new Dictionary<string, string?>
                {
                    ["executionState"] = "IN_PROGRESS"
                },
                CreatedAtUtc = new DateTimeOffset(2026, 9, 19, 1, 0, 0, TimeSpan.Zero),
                UpdatedAtUtc = new DateTimeOffset(2026, 9, 19, 1, 0, 0, TimeSpan.Zero)
            }]
        };
        AssertTamperedJsonRejected(serializer, serializer.Serialize(projectWithProposal), root =>
        {
            ((JsonObject)((JsonArray)root["executionProposals"]!)[0]!["proposedChanges"]!)!["actualEffortHours"] = "1.25";
        }, "Schema 2.0 must reject proposal effort outside the half-hour granularity.");
    }

    private static void AssertTamperedJsonRejected(
        CanonicalJsonSerializer serializer,
        string validJson,
        Action<JsonObject> tamper,
        string message)
    {
        var root = JsonNode.Parse(validJson)!.AsObject();
        tamper(root);
        TestAssert.Throws<InvalidDataException>(
            () => serializer.Deserialize(root.ToJsonString()),
            message);
    }

    private static CanonicalProject CreateValidSchema20Project()
    {
        var planningProject = CanonicalJsonTests.CaptureCanonicalProject();
        var cardId = planningProject.DeliveryCards.First().Id;
        return planningProject with
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
                ProjectId = planningProject.Project.Id,
                BaselineId = planningProject.Baseline.Id,
                RegisterRevision = 1,
                RegisterStatusDate = new DateOnly(2026, 9, 19),
                ValidationResult = ManifestValidationResult.PassWithWarnings,
                SourceReadiness = SourceReadinessState.Pass,
                SnapshotId = "snapshot-test",
                ImportedAtUtc = new DateTimeOffset(2026, 9, 19, 1, 0, 0, TimeSpan.Zero)
            },
            SourceExecution = new SourceExecutionSnapshot
            {
                ProjectId = planningProject.Project.Id,
                BaselineId = planningProject.Baseline.Id,
                RegisterId = "register",
                RegisterRevision = 1,
                StatusDate = new DateOnly(2026, 9, 19),
                TimeZone = "Asia/Ho_Chi_Minh",
                SourcePath = "planning/idea-technical-pilot-execution-register.json",
                Records =
                [new SourceExecutionRecord
                {
                    Entity = CanonicalWorkItemKey.DeliveryCard(cardId),
                    RecordingState = SourceRecordingState.Recorded,
                    ExecutionState = ExecutionState.InProgress,
                    ResultState = SourceResultState.NotApplicable,
                    ActualStart = new DateOnly(2026, 9, 19),
                    LastUpdatedAt = new DateTimeOffset(2026, 9, 19, 1, 0, 0, TimeSpan.Zero),
                    SourcePath = "planning/idea-technical-pilot-execution-register.json"
                }]
            }
        };
    }
}
