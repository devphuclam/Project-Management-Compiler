using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class Mvp21HardeningTests
{
    private const string Increment = "specs/004-technical-pilot-readiness";

    public static void IncludeManagementEvidenceWithoutPathFailsExplicitly()
    {
        try
        {
            _ = new ProjectCompiler().CompileAsync(
                new CompilationRequest
                {
                    SourcePath = RealShapedFixturePath(),
                    AsOfDate = new DateOnly(2026, 9, 28),
                    IncludeManagementEvidence = true
                },
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (ProjectCompilationException exception)
        {
            TestAssert.Contains("MANAGEMENT_EVIDENCE_PATH_REQUIRED", string.Join('|', exception.Diagnostics.Select(item => item.Code)), "Readiness intake without a path must be explicit.");
            TestAssert.Contains("Select the readiness increment path", exception.Message, "The path-required error must guide the operator.");
            return;
        }

        throw new InvalidOperationException("Including management evidence without an increment path must not compile silently.");
    }

    public static void ReadinessFixturePreservesOwnerWaitingAndSemanticSummaries()
    {
        var result = CompileReadinessFixture();
        var p01 = result.Project.ManagementEvidence.Observations.Single(item => item.EvidenceKind == ManagementEvidenceKind.ReadinessCheck && item.SourceRecordId == "P01");

        TestAssert.Equal("IN-PROGRESS", p01.StateCode, "P01 task state must remain separate.");
        TestAssert.Equal("NOT-RUN", p01.ResultCode, "P01 readiness result must remain separate.");
        TestAssert.Equal("PRODUCT_AUTHOR", p01.OwnerRole, "P01 owner must identify the author-side executor.");
        TestAssert.Contains("Principal Product Author", p01.OwnerRoleLabel ?? string.Empty, "Owner display wording must be retained.");
        TestAssert.Equal("PROJECT_REVIEWER", p01.WaitingForRole, "P01 waiting-for role must identify the reviewer.");
        TestAssert.Contains("Project Reviewer", p01.WaitingForRoleLabel ?? string.Empty, "Waiting-for display wording must be retained.");
        TestAssert.Contains("T006", p01.PendingActionSummary ?? string.Empty, "Pending action must retain the source task reference.");
        TestAssert.Contains("reviewer disposition", (p01.PendingActionSummary ?? string.Empty).ToLowerInvariant(), "Pending action must preserve reviewer disposition meaning.");
        TestAssert.Contains("T006", p01.BlockerSummary ?? string.Empty, "Generic blocker code must not erase the safe blocker summary.");
    }

    public static void StandaloneManagementEvidenceIsValidWithoutCanonicalTarget()
    {
        var evidence = new ManagementEvidence
        {
            DiscoveryState = ManagementEvidenceDiscoveryState.Known,
            Observations =
            [
                new ManagementEvidenceObservation
                {
                    Id = "gate:PG4:execution",
                    EvidenceKind = ManagementEvidenceKind.GateExecution,
                    SourceRecordId = "PG4",
                    GateId = "PG4",
                    ExplicitTarget = new EvidenceTarget { Kind = "Gate", Id = "PG4" },
                    StateCode = "NOT-RUN"
                },
                new ManagementEvidenceObservation
                {
                    Id = "decision:D0",
                    EvidenceKind = ManagementEvidenceKind.DecisionRecord,
                    SourceRecordId = "D0",
                    Summary = "Successor disposition",
                    StateCode = "OPEN"
                },
                new ManagementEvidenceObservation
                {
                    Id = "human-action:HA-001",
                    EvidenceKind = ManagementEvidenceKind.HumanAction,
                    SourceRecordId = "HA-001",
                    Summary = "Record reviewer disposition",
                    StateCode = "NOT-RUN"
                }
            ]
        };

        var result = new ManagementEvidenceReconciler().Reconcile(evidence, new CanonicalProject());

        TestAssert.Equal(EvidenceReconciliationStatus.Standalone, result.Reconciliations.Single(item => item.ObservationId == "gate:PG4:execution").Status, "A gate is valid standalone management evidence.");
        TestAssert.Equal(EvidenceReconciliationStatus.Standalone, result.Reconciliations.Single(item => item.ObservationId == "decision:D0").Status, "A decision is valid standalone management evidence.");
        TestAssert.Equal(EvidenceReconciliationStatus.Standalone, result.Reconciliations.Single(item => item.ObservationId == "human-action:HA-001").Status, "A human action is valid standalone management evidence.");
        TestAssert.False(result.Diagnostics.Any(item => item.Code == "EVIDENCE_TARGET_UNMATCHED"), "Valid standalone evidence must not emit an unmatched-target warning.");
    }

    public static void ReadinessP04ResolvesOnlyToWorkPackage()
    {
        var evidence = new ManagementEvidence
        {
            DiscoveryState = ManagementEvidenceDiscoveryState.Known,
            Observations =
            [
                new ManagementEvidenceObservation
                {
                    Id = "readiness:P04",
                    EvidenceKind = ManagementEvidenceKind.ReadinessCheck,
                    SourceRecordId = "P04",
                    ExplicitTarget = new EvidenceTarget { Kind = "WorkPackage", Id = "P04" }
                }
            ]
        };
        var project = new CanonicalProject
        {
            WorkPackages = [new WorkPackage { Id = "P04" }],
            DeliveryCards = [new DeliveryCard { Id = "P04", WorkPackageId = "P04" }]
        };

        var reconciliation = new ManagementEvidenceReconciler().Reconcile(evidence, project).Reconciliations.Single();

        TestAssert.Equal(EvidenceReconciliationStatus.Matched, reconciliation.Status, "P04 readiness must resolve.");
        TestAssert.Equal("WorkPackage", reconciliation.ResolvedTarget!.Kind, "P04 readiness must resolve to a work package.");
        TestAssert.False(reconciliation.CandidateTargets.Any(item => item.Kind == "DeliveryCard"), "A same-ID delivery card must not become a readiness candidate.");
    }

    public static void EffectiveGateSelectionUsesAuthorityAndConflictsHonestly()
    {
        var evidence = new ManagementEvidence
        {
            DiscoveryState = ManagementEvidenceDiscoveryState.Known,
            Observations =
            [
                GateObservation("readme", "NOT-RUN", 3, "readme-summary"),
                GateObservation("actual", "COMPLETE", 1, "actual-gate-record")
            ]
        };

        var selected = new EffectiveEvidenceResolver().Select(evidence, "Gate", "PG4", ManagementEvidenceKind.GateExecution, "StateCode");

        TestAssert.Equal(EffectiveEvidenceSelectionStatus.Resolved, selected.Status, "The higher-authority actual gate record must resolve.");
        TestAssert.Equal("COMPLETE", selected.Value, "The actual gate record must outrank the README summary.");
        TestAssert.Equal("actual-gate-record", selected.SelectedObservation!.AuthorityKind, "The effective source must be visible.");

        var conflict = evidence with
        {
            Observations =
            [
                GateObservation("actual-a", "NOT-RUN", 1, "actual-gate-record"),
                GateObservation("actual-b", "COMPLETE", 1, "actual-gate-record")
            ]
        };
        var conflicted = new EffectiveEvidenceResolver().Select(conflict, "Gate", "PG4", ManagementEvidenceKind.GateExecution, "StateCode");

        TestAssert.Equal(EffectiveEvidenceSelectionStatus.Conflict, conflicted.Status, "Equal-authority gate disagreement must not select a value.");
        TestAssert.Equal(null, conflicted.Value, "A conflict must not fabricate an effective state.");
        TestAssert.Equal(2, conflicted.Candidates.Count, "A conflict must retain both source observations.");
    }

    public static void StateAndResultAreSeparateEffectiveFields()
    {
        var evidence = new ManagementEvidence
        {
            Observations =
            [new ManagementEvidenceObservation
            {
                Id = "readiness:P01",
                EvidenceKind = ManagementEvidenceKind.ReadinessCheck,
                SourceRecordId = "P01",
                ExplicitTarget = new EvidenceTarget { Kind = "WorkPackage", Id = "P01" },
                StateCode = "IN-PROGRESS",
                ResultCode = "NOT-RUN",
                AuthorityRank = 2
            }]
        };
        var resolver = new EffectiveEvidenceResolver();

        TestAssert.Equal(EffectiveEvidenceSelectionStatus.Resolved, resolver.Select(evidence, "WorkPackage", "P01", ManagementEvidenceKind.ReadinessCheck, "StateCode").Status, "State must resolve independently.");
        TestAssert.Equal(EffectiveEvidenceSelectionStatus.Resolved, resolver.Select(evidence, "WorkPackage", "P01", ManagementEvidenceKind.ReadinessCheck, "ResultCode").Status, "Result must resolve independently.");
    }

    public static void GateViewHasNoFakeGateWhenEvidenceIsAbsent()
    {
        var view = new ManagementControlViewProjector().Build(new CanonicalProject());

        TestAssert.Equal(null, view.CurrentGate.GateId, "The generic gate view must not default to PG4.");
        TestAssert.Equal(null, view.CurrentGate.ExecutionState, "No gate evidence must not fabricate execution state.");
        TestAssert.Equal(null, view.CurrentGate.Outcome, "No gate evidence must not fabricate outcome.");
    }

    public static void ProposedSuccessorIsExtractedFromControlEnvelope()
    {
        var result = CompileReadinessFixture();
        var control = result.Project.ManagementEvidence.Observations.Single(item => item.EvidenceKind == ManagementEvidenceKind.ControlEnvelope);

        TestAssert.Equal("IE-INC-PH1-FOUNDATION-CUSTODY-001", control.ProposedSuccessorIncrementId, "The control envelope successor ID must be source-derived.");
        TestAssert.Contains("PH1 F01-F05", control.ProposedSuccessorSummary ?? string.Empty, "The successor scope summary must remain available.");
        TestAssert.Equal("IE-INC-PH1-FOUNDATION-CUSTODY-001", result.Views.ManagementControl.BaselineContext.ProposedSuccessorIncrement, "The view must not read successor data from GateEffect.");
    }

    public static void DecisionAndHumanActionSummariesReachAttentionProjection()
    {
        var result = CompileReadinessFixture();
        var decision = result.Project.ManagementEvidence.Observations.Single(item => item.SourceRecordId == "D0");
        var action = result.Project.ManagementEvidence.Observations.Single(item => item.SourceRecordId == "HA-001");

        TestAssert.Contains("Successor", decision.Summary ?? string.Empty, "Decision meaning must remain in a bounded summary.");
        TestAssert.Equal("PRODUCT_DECISION_AUTHORITY", decision.RequiredAuthorityRole, "Decision authority must remain distinct from an executor role.");
        TestAssert.Contains("T006", action.ActionSummary ?? string.Empty, "Human action summary must retain its task context.");
        TestAssert.Equal("PROJECT_REVIEWER", action.WaitingForRole, "Human action required role must remain visible as waiting-for.");
        TestAssert.Contains("reviewer", string.Join('|', result.Views.ManagementControl.AttentionGroups.SelectMany(group => group.Items).Select(item => item.Reason)).ToLowerInvariant(), "Attention projection must use source-supported action meaning.");
    }

    public static void ActualGateRecordIsOptionalAndContractDoesNotPassGate()
    {
        var snapshot = CaptureFixture();
        var withoutActual = new IdeaEngineeringReadinessAdapter().Adapt(snapshot, PlanningProject()).Evidence;
        TestAssert.False(withoutActual.Diagnostics.Any(item => item.Code == "EVIDENCE_SOURCE_UNAVAILABLE" && item.Message.Contains("pg4-gate-record.md", StringComparison.OrdinalIgnoreCase)), "A pre-T026 missing actual gate record is expected later, not an error.");

        var contractOnly = snapshot with
        {
            Documents = snapshot.Documents
                .Select(document => document.RelativeFile.EndsWith("/README.md", StringComparison.OrdinalIgnoreCase)
                    ? document with { Content = document.Content.Replace("| PG4 Gate Execution State | NOT-RUN |", "| PG4 Gate Execution State | ").Replace("| PG4 Gate Outcome | NOT-APPLICABLE |", "| PG4 Gate Outcome | ") }
                    : document)
                .ToArray()
        };
        var contractEvidence = new IdeaEngineeringReadinessAdapter().Adapt(contractOnly, PlanningProject()).Evidence;
        var contractGate = contractEvidence.Observations.FirstOrDefault(item => item.EvidenceKind == ManagementEvidenceKind.GateExecution);
        TestAssert.Equal(null, contractGate, "A contract/template must not become current gate state.");
    }

    public static void CanonicalJsonPreservesHardeningSemanticsWithoutRawRows()
    {
        var result = CompileReadinessFixture();
        var compiler = new ProjectCompiler();
        var json = compiler.SaveJson(result);
        var reopened = compiler.Reopen(json, new DateOnly(2026, 9, 28));
        var original = result.Project.ManagementEvidence.Observations.Single(item => item.SourceRecordId == "P01");
        var restored = reopened.Project.ManagementEvidence.Observations.Single(item => item.SourceRecordId == "P01");

        TestAssert.Equal(original.OwnerRole, restored.OwnerRole, "Owner code must survive save/reopen.");
        TestAssert.Equal(original.WaitingForRole, restored.WaitingForRole, "Waiting-for code must survive save/reopen.");
        TestAssert.Equal(original.PendingActionSummary, restored.PendingActionSummary, "Pending summary must survive save/reopen.");
        TestAssert.Equal(result.SemanticDigest, reopened.SemanticDigest, "Hardening semantics must preserve digest on reopen.");
        TestAssert.False(json.Contains("| P01 |", StringComparison.Ordinal), "Canonical JSON must not contain a raw markdown table row.");
        TestAssert.False(json.Contains("C:\\Users\\", StringComparison.OrdinalIgnoreCase), "Canonical JSON must not contain an absolute workstation path.");
    }

    private static ManagementEvidenceObservation GateObservation(string id, string state, int authorityRank, string authorityKind) =>
        new()
        {
            Id = id,
            EvidenceKind = ManagementEvidenceKind.GateExecution,
            SourceRecordId = "PG4",
            GateId = "PG4",
            ExplicitTarget = new EvidenceTarget { Kind = "Gate", Id = "PG4" },
            StateCode = state,
            AuthorityRank = authorityRank,
            AuthorityKind = authorityKind
        };

    private static CompilationResult CompileReadinessFixture() =>
        new ProjectCompiler().CompileAsync(
            new CompilationRequest
            {
                SourcePath = RealShapedFixturePath(),
                AsOfDate = new DateOnly(2026, 9, 28),
                IncludeManagementEvidence = true,
                ManagementEvidenceIncrementPath = Increment
            },
            CancellationToken.None).GetAwaiter().GetResult();

    private static RepositorySnapshot CaptureFixture() =>
        new LocalRepositorySourceAdapter().CaptureAsync(
            new SourceRequest
            {
                Location = RealShapedFixturePath(),
                ManagementEvidenceIncrementPath = Increment
            },
            CancellationToken.None).GetAwaiter().GetResult();

    private static CanonicalProject PlanningProject() =>
        new()
        {
            WorkPackages = Enumerable.Range(1, 7)
                .Select(index => new WorkPackage { Id = $"P{index:00}", Name = $"Package {index:00}" })
                .ToArray()
        };

    private static string RealShapedFixturePath() =>
        Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering-real-shaped");
}
