using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Application.ManifestImport;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ProposalTests
{
    public static void ProposalPreviewIsLocalAndStaleAware()
    {
        var state = new CompilerApplicationState();
        var service = new ManifestImportApplicationService(
            new IdeaEngineeringManifestImporter(new ManifestGitObjectReader()),
            new ProjectCompiler(),
            state);
        var import = service.ImportAsync(new ManifestImportRequest
        {
            RepositoryRoot = FindIdeaEngineeringRoot(),
            ManifestPath = "planning/project-management-compiler-manifest.json",
            Mode = ManifestImportMode.GitCommit,
            RequestedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4"
        }).GetAwaiter().GetResult();

        TestAssert.Equal(ManifestImportClassification.OfficialCommit, import.Classification, "Proposal tests need an official source snapshot.");
        var proposals = new ExecutionProposalService(state);
        var proposal = proposals.Create(new CreateExecutionProposalRequest
        {
            TargetKind = "DeliveryCard",
            TargetId = "P01",
            ProposedChanges = new Dictionary<string, string?>
            {
                ["executionState"] = "COMPLETED",
                ["actualFinish"] = "2026-09-20"
            },
            RequestedLifecycle = ProposalLifecycle.ReadyForReview
        });

        TestAssert.Equal(ProposalLifecycle.Draft, proposal.Lifecycle, "Incomplete completion proposals must remain draft.");
        TestAssert.True(proposal.Diagnostics.Any(diagnostic => diagnostic.Code == "PMC-PROPOSAL-002"), "Incomplete completion must be explained.");
        var preview = proposals.Preview(proposal.Id);
        TestAssert.False(preview.IsAuthoritative, "Proposal preview must never be authoritative.");
        TestAssert.True(preview.IsEstimated, "Proposal preview must be explicitly estimated.");
        TestAssert.Equal(SourceRecordingState.Recorded, state.CurrentOfficialSnapshot!.SourceExecution.Records.Single(record => record.Entity.Id == "P01").RecordingState, "Proposal preview must not rewrite official source execution.");
        TestAssert.False(proposals.Export(proposal.Id).Contains(FindIdeaEngineeringRoot(), StringComparison.OrdinalIgnoreCase), "Proposal export must not contain a local source root.");

        var stale = proposals.Create(new CreateExecutionProposalRequest
        {
            BaseSnapshotId = "snapshot-stale",
            ExpectedRegisterRevision = 1,
            TargetKind = "DeliveryCard",
            TargetId = "P01",
            ProposedChanges = new Dictionary<string, string?> { ["executionState"] = "IN_PROGRESS" }
        });
        TestAssert.Equal(ProposalLifecycle.StaleBase, stale.Lifecycle, "A proposal based on an older snapshot must be stale instead of rebased.");
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

        throw new DirectoryNotFoundException("The local IDEAEngineering checkout was not found for the proposal test.");
    }
}
