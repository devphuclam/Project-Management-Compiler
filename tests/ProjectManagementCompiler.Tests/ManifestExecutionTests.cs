using ProjectManagementCompiler.Application.ManifestImport;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ManifestExecutionTests
{
    public static void OfficialAnalysisUsesRecordedSourceExecutionOnly()
    {
        var sourceRoot = FindIdeaEngineeringRoot();
        var imported = new IdeaEngineeringManifestImporter(new ManifestGitObjectReader())
            .ImportAsync(new ManifestImportRequest
            {
                RepositoryRoot = sourceRoot,
                ManifestPath = "planning/project-management-compiler-manifest.json",
                Mode = ManifestImportMode.GitCommit,
                RequestedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4"
            })
            .GetAwaiter()
            .GetResult();

        var snapshot = imported.Snapshot!;
        var p01 = snapshot.SourceExecution.Records.Single(record => record.Entity.Id == "P01");
        var sourceWithRecordedEffort = snapshot.Project with
        {
            SourceExecution = snapshot.Project.SourceExecution with
            {
                Records = snapshot.Project.SourceExecution.Records
                    .Select(record => record.Entity.Id == "P01"
                        ? record with
                        {
                            ActualStart = new DateOnly(2026, 9, 19),
                            ActualEffortHours = 8m,
                            RemainingEffortHours = 4m
                        }
                        : record)
                    .ToArray(),
                },
            ExecutionProposals =
            [
                new ExecutionProposal
                {
                    Id = "proposal-only",
                    BaseSnapshotId = snapshot.Metadata.SnapshotId,
                    ExpectedRegisterRevision = snapshot.Metadata.RegisterRevision,
                    TargetKind = "DeliveryCard",
                    TargetId = "P01",
                    ProposedChanges = new Dictionary<string, string?>
                    {
                        ["actualEffortHours"] = "999"
                    }
                }
            ]
        };

        var analysis = new ManagementAnalysisOrchestrator().Analyze(sourceWithRecordedEffort, new DateOnly(2026, 9, 19));

        TestAssert.Equal(8m, analysis.ExecutionEffort.ActualEffortHours, "Official metrics must read actual effort from the source register.");
        TestAssert.Equal(4m, analysis.ExecutionEffort.RemainingEffortHours, "Official metrics must read remaining effort from the source register.");
        TestAssert.Equal(1, analysis.ExecutionStatus.InProgress, "Recorded source state must reach official status analysis.");
        TestAssert.Equal(DataState.Unknown, analysis.ForecastState, "A source register without a forecast rule must remain unknown.");
        TestAssert.Equal(SourceRecordingState.Recorded, p01.RecordingState, "The accepted source must preserve P01 recording state.");
        TestAssert.True(sourceWithRecordedEffort.SourceExecution.Records.Count(record => record.RecordingState == SourceRecordingState.NotRecorded) == 52, "Unrecorded source cards must remain unrecorded.");

        var sourceWithoutRecords = sourceWithRecordedEffort with
        {
            SourceExecution = new SourceExecutionSnapshot(),
            ExecutionOverlay = new ExecutionOverlay
            {
                Records =
                [new ExecutionRecord
                {
                    WorkItemId = "P01",
                    ExecutionState = ExecutionState.Completed,
                    ActualStart = new DateOnly(2026, 9, 19),
                    LastUpdatedAt = new DateTimeOffset(2026, 9, 19, 1, 0, 0, TimeSpan.Zero)
                }]
            }
        };
        TestAssert.True(ExecutionTruthResolver.ForCard(sourceWithoutRecords, "P01") is null, "A schema 2.0 source snapshot with no execution records must not fall back to the legacy overlay.");
    }

    private static string FindIdeaEngineeringRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var depth = 0; depth < 6 && current is not null; depth++, current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "IDEAEngineering");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var sibling = current.Parent is null ? null : Path.Combine(current.Parent.FullName, "IDEAEngineering");
            if (sibling is not null && Directory.Exists(sibling))
            {
                return sibling;
            }
        }

        throw new DirectoryNotFoundException("The local IDEAEngineering checkout was not found for the accepted-source compatibility test.");
    }
}
