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
        var before = state.Proposals.Count;
        TestAssert.Throws<ArgumentException>(() => proposals.Create(new CreateExecutionProposalRequest
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
        }), "An empty evidence object must be rejected at proposal creation.");
        TestAssert.Equal(before, state.Proposals.Count, "Rejected evidence must leave the proposal owner unchanged.");

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
                Description = "Controlled completion evidence.",
                Result = "NOT_APPLICABLE",
                RecordedAt = new DateTimeOffset(2026, 9, 20, 1, 0, 0, TimeSpan.Zero),
                RecordedBy = "LEAD"
            }],
            RequestedLifecycle = ProposalLifecycle.ReadyForReview
        });
        TestAssert.Equal(ProposalLifecycle.ReadyForReview, valid.Lifecycle, "A complete proposal with controlled evidence should qualify for review.");
    }

    public static void EmptyProposalEvidenceRemainsDraft()
    {
        var (_, proposals) = ImportOfficial();
        var draft = proposals.Create(new CreateExecutionProposalRequest
        {
            TargetId = "P01",
            ProposedChanges = new Dictionary<string, string?> { ["executionState"] = "IN_PROGRESS" }
        });

        TestAssert.Equal(ProposalLifecycle.Draft, draft.Lifecycle, "Zero evidence is valid for a non-completion draft proposal.");
        TestAssert.Equal(0, draft.Evidence.Count, "An empty evidence list must remain empty.");
    }

    public static void ProposalRejectsEachMalformedControlledEvidenceItem()
    {
        var (state, proposals) = ImportOfficial();
        var cases = new (string Name, SourceExecutionEvidence Evidence)[]
        {
            ("empty object", new SourceExecutionEvidence()),
            ("missing recorded by", ValidEvidence() with { RecordedBy = null }),
            ("missing result", ValidEvidence() with { Result = null }),
            ("unsupported type", ValidEvidence() with { Type = "COMPATIBILITY_UPDATE" }),
            ("unsafe path", ValidEvidence() with { RepositoryPath = "../secret.txt" }),
            ("credential URI", ValidEvidence() with { ExternalUri = "https://user:pass@example.com/evidence" }),
            ("malformed commit", ValidEvidence() with { Commit = "not-a-commit" })
        };

        foreach (var (name, evidence) in cases)
        {
            var before = state.Proposals.Count;
            TestAssert.Throws<ArgumentException>(() => proposals.Create(new CreateExecutionProposalRequest
            {
                TargetId = "P01",
                ProposedChanges = new Dictionary<string, string?> { ["executionState"] = "IN_PROGRESS" },
                Evidence = [evidence]
            }), $"The {name} evidence case must be rejected.");
            TestAssert.Equal(before, state.Proposals.Count, $"The {name} evidence case must not mutate state.");
        }
    }

    public static void ProposalUpdateRejectsMalformedEvidenceWithoutMutation()
    {
        var (state, proposals) = ImportOfficial();
        var created = proposals.Create(new CreateExecutionProposalRequest
        {
            TargetId = "P01",
            ProposedChanges = new Dictionary<string, string?> { ["executionState"] = "IN_PROGRESS" }
        });
        var before = state.FindProposal(created.Id)!;

        TestAssert.Throws<ArgumentException>(() => proposals.Update(created.Id, new UpdateExecutionProposalRequest
        {
            Evidence = [ValidEvidence() with { Result = null }]
        }), "An update with missing evidence result must be rejected.");

        var after = state.FindProposal(created.Id)!;
        TestAssert.Equal(before.Evidence.Count, after.Evidence.Count, "Rejected evidence update must preserve the existing evidence.");
        TestAssert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc, "Rejected evidence update must not replace proposal metadata.");
    }

    public static void ValidEvidenceWithoutLocatorQualifiesCompletion()
    {
        var (_, proposals) = ImportOfficial();
        var proposal = proposals.Create(new CreateExecutionProposalRequest
        {
            TargetId = "P01",
            ProposedChanges = CompletionChanges(),
            Evidence = [ValidEvidence()],
            RequestedLifecycle = ProposalLifecycle.ReadyForReview
        });

        TestAssert.Equal(ProposalLifecycle.ReadyForReview, proposal.Lifecycle, "A source-compatible evidence record without a locator must qualify completion.");
    }

    private static (CompilerApplicationState State, ExecutionProposalService Service) ImportOfficial()
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
        TestAssert.Equal(ManifestImportClassification.OfficialCommit, import.Classification, "The proposal test needs an official source snapshot.");
        return (state, new ExecutionProposalService(state));
    }

    private static Dictionary<string, string?> CompletionChanges() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["executionState"] = "COMPLETED",
        ["actualFinish"] = "2026-09-20",
        ["actualEffortHours"] = "8",
        ["remainingEffortHours"] = "0"
    };

    private static SourceExecutionEvidence ValidEvidence() => new()
    {
        EvidenceId = "P01-PROPOSAL-VALID-001",
        Type = "SOURCE_RECORD",
        Description = "Controlled completion evidence.",
        Result = "NOT_APPLICABLE",
        RecordedAt = new DateTimeOffset(2026, 9, 20, 1, 0, 0, TimeSpan.Zero),
        RecordedBy = "LEAD"
    };

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
