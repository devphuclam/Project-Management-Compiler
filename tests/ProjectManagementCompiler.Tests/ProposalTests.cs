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

    public static void ProposalSaveReopenListRoundTripsThroughApplicationState()
    {
        var state = new CompilerApplicationState();
        var compiler = new ProjectCompiler();
        var service = new ManifestImportApplicationService(
            new IdeaEngineeringManifestImporter(new ManifestGitObjectReader()),
            compiler,
            state);
        var import = service.ImportAsync(new ManifestImportRequest
        {
            RepositoryRoot = FindIdeaEngineeringRoot(),
            ManifestPath = "planning/project-management-compiler-manifest.json",
            Mode = ManifestImportMode.GitCommit,
            RequestedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4"
        }).GetAwaiter().GetResult();

        TestAssert.Equal(ManifestImportClassification.OfficialCommit, import.Classification, "Proposal persistence needs an official source snapshot.");
        var proposals = new ExecutionProposalService(state);
        var created = proposals.Create(new CreateExecutionProposalRequest
        {
            TargetKind = "DeliveryCard",
            TargetId = "P01",
            ProposedChanges = new Dictionary<string, string?> { ["executionState"] = "IN_PROGRESS" }
        });

        var saved = compiler.SaveJson(state.CurrentOfficialResult!);
        var reopened = compiler.Reopen(saved, new DateOnly(2026, 9, 19));
        var reopenedState = new CompilerApplicationState();
        reopenedState.Set(reopened);

        var listed = new ExecutionProposalService(reopenedState).List();
        TestAssert.Equal(1, listed.Count, "Save/reopen must hydrate the proposal state from CanonicalProject.ExecutionProposals.");
        TestAssert.Equal(created.Id, listed[0].Id, "Save/reopen must retain the proposal identity.");
        TestAssert.False(reopened.Project.SourceExecution.Records.Any(record => record.Entity.Id == "P01" && record.RecordingState != SourceRecordingState.Recorded), "Proposal persistence must not mutate source execution authority.");
    }

    public static void MalformedEvidenceCannotQualifyReadyForReview()
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

        TestAssert.Equal(ManifestImportClassification.OfficialCommit, import.Classification, "Evidence validation needs an official source snapshot.");
        var proposals = new ExecutionProposalService(state);
        var proposal = proposals.Create(new CreateExecutionProposalRequest
        {
            TargetKind = "DeliveryCard",
            TargetId = "P01",
            ProposedChanges = new Dictionary<string, string?>
            {
                ["executionState"] = "COMPLETED",
                ["actualFinish"] = "2026-09-20",
                ["actualEffortHours"] = "8",
                ["remainingEffortHours"] = "0"
            },
            Evidence = [new SourceExecutionEvidence()],
            RequestedLifecycle = ProposalLifecycle.ReadyForReview
        });

        TestAssert.Equal(ProposalLifecycle.Draft, proposal.Lifecycle, "Malformed evidence must not qualify a completion proposal for review.");
        TestAssert.True(proposal.Diagnostics.Any(diagnostic => diagnostic.Code == "PMC-PROPOSAL-007"), "Malformed evidence must produce an explicit controlled-evidence diagnostic.");

        var valid = proposals.Create(new CreateExecutionProposalRequest
        {
            TargetKind = "DeliveryCard",
            TargetId = "P01",
            ProposedChanges = new Dictionary<string, string?>
            {
                ["executionState"] = "COMPLETED",
                ["actualFinish"] = "2026-09-20",
                ["actualEffortHours"] = "8",
                ["remainingEffortHours"] = "0"
            },
            Evidence =
            [new SourceExecutionEvidence
            {
                EvidenceId = "P01-PROPOSAL-001",
                Type = "SOURCE_RECORD",
                RepositoryPath = "specs/004-technical-pilot-readiness/readiness-register.md",
                Description = "Controlled completion evidence.",
                RecordedAt = new DateTimeOffset(2026, 9, 20, 1, 0, 0, TimeSpan.Zero),
                RecordedBy = "LEAD"
            }],
            RequestedLifecycle = ProposalLifecycle.ReadyForReview
        });
        TestAssert.Equal(ProposalLifecycle.ReadyForReview, valid.Lifecycle, "A complete proposal with controlled evidence should qualify for review.");
    }

    private static string FindIdeaEngineeringRoot()
    {
        var configured = Environment.GetEnvironmentVariable("IDEAENGINEERING_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

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
