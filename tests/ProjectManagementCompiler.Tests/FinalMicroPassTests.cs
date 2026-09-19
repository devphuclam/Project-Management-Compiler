using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Application.ManifestImport;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class FinalMicroPassTests
{
    public static void LegacyPublicExecutionUpdateDrivesLegacyAnalysisAndGantt()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering");
        var compiler = new ProjectCompiler();
        var initial = compiler.CompileAsync(new CompilationRequest
        {
            SourcePath = root,
            AsOfDate = new DateOnly(2026, 9, 28)
        }).GetAwaiter().GetResult();

        TestAssert.True(initial.Project.ImportMetadata is null, "The fixed-path compile must remain an explicit legacy project.");

        var applied = compiler.ApplyExecutionUpdate(initial, new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 25),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)
        }, new DateOnly(2026, 9, 28));

        TestAssert.True(applied.Accepted, "The public legacy execution operation must accept a valid explicit update.");
        TestAssert.Equal(1, applied.Result.Analysis.ExecutionStatus.InProgress, "Legacy analysis must consume the explicit overlay record.");

        var gantt = new GanttProjector().Build(applied.Result.Project, applied.Result.Analysis, new DateOnly(2026, 9, 28));
        var actual = gantt.Items.Single(item => item.WorkItemId == "P04-A").Lanes
            .SingleOrDefault(lane => lane.Lane == GanttLane.Actual);
        TestAssert.True(actual is not null, "The explicit legacy overlay must produce an ACTUAL lane.");
        TestAssert.Equal(new DateOnly(2026, 9, 25), actual!.Start, "Legacy ACTUAL must retain the authored actual start.");
    }

    public static void MixedOfficialAndMigratedSessionKeepsProposalProjectionCoherent()
    {
        var compiler = new ProjectCompiler();
        var state = new CompilerApplicationState();
        var importService = new ManifestImportApplicationService(
            new IdeaEngineeringManifestImporter(new ManifestGitObjectReader()),
            compiler,
            state);
        var official = importService.ImportAsync(new ManifestImportRequest
        {
            RepositoryRoot = FindIdeaEngineeringRoot(),
            ManifestPath = "planning/project-management-compiler-manifest.json",
            Mode = ManifestImportMode.GitCommit,
            RequestedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4"
        }).GetAwaiter().GetResult();
        TestAssert.Equal(ManifestImportClassification.OfficialCommit, official.Classification, "Mixed-session test needs an official source snapshot.");

        var officialSource = state.CurrentOfficialResult!.Project.SourceExecution;
        var legacy = compiler.CompileAsync(new CompilationRequest
        {
            SourcePath = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering"),
            AsOfDate = new DateOnly(2026, 9, 28)
        }).GetAwaiter().GetResult();
        var legacyWithOverlay = compiler.ApplyExecutionUpdate(legacy, new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 25),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)
        }, new DateOnly(2026, 9, 28));
        TestAssert.True(legacyWithOverlay.Accepted, "The legacy source must provide an overlay for schema 1.0 migration.");

        var migrated = compiler.Reopen(compiler.SaveJson(legacyWithOverlay.Result), new DateOnly(2026, 9, 28));
        TestAssert.Equal(1, migrated.Project.ExecutionProposals.Count, "Schema 1.0 reopen must hydrate the migrated proposal before the mixed session update.");
        state.Set(migrated);

        var proposalService = new ExecutionProposalService(state);
        var migratedProposal = state.Proposals.Single();
        var updated = proposalService.Update(migratedProposal.Id, new UpdateExecutionProposalRequest
        {
            ProposedChanges = new Dictionary<string, string?>
            {
                ["blocker"] = "Legacy proposal remains review-only."
            }
        });

        TestAssert.Equal(updated.Id, state.Current!.Project.ExecutionProposals.Single().Id, "Current reopened state must project the updated proposal.");
        TestAssert.Equal("Legacy proposal remains review-only.", state.Current.Project.ExecutionProposals.Single().ProposedChanges["blocker"], "Current reopened state must contain the updated proposal value.");
        TestAssert.Equal(updated.Id, state.CurrentOfficialResult!.Project.ExecutionProposals.Single().Id, "Official result must project the same proposal owner state.");
        TestAssert.Equal(updated.Id, state.CurrentOfficialSnapshot!.Project.ExecutionProposals.Single().Id, "Official snapshot projection must remain coherent with the state owner.");
        TestAssert.Equal(officialSource, state.CurrentOfficialResult.Project.SourceExecution, "Proposal updates must not mutate official source execution.");
        TestAssert.Equal(0, state.Current.Project.SourceExecution.Records.Count, "Migrated legacy proposal updates must not create source execution records.");

        var persisted = compiler.SaveJson(state.Current);
        var reopened = compiler.Reopen(persisted, new DateOnly(2026, 9, 28));
        var reopenedState = new CompilerApplicationState();
        reopenedState.Set(reopened);
        var listed = new ExecutionProposalService(reopenedState).List();
        TestAssert.Equal(1, listed.Count, "Migrated proposal updates must survive save/reopen through the same state owner.");
        TestAssert.Equal("Legacy proposal remains review-only.", listed[0].ProposedChanges["blocker"], "Save/reopen must retain the migrated proposal update.");
        TestAssert.Equal(0, reopened.Project.SourceExecution.Records.Count, "Save/reopen must not promote migrated proposal data into source execution.");
    }

    private static string FindIdeaEngineeringRoot()
    {
        var configured = Environment.GetEnvironmentVariable("IDEAENGINEERING_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        throw new DirectoryNotFoundException("The local IDEAEngineering checkout was not configured for the mixed-session test.");
    }
}
