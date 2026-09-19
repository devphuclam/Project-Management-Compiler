using System.Text;
using System.Text.Json;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ReadinessAdapterTests
{
    private const string Increment = "specs/004-technical-pilot-readiness";

    public static void ReadinessAdapterCapturesWorkPackagesDecisionsGateActionsAndChecklistContext()
    {
        var result = new IdeaEngineeringReadinessAdapter().Adapt(Snapshot(), PlanningProject());
        var evidence = result.Evidence;

        TestAssert.Equal(ManagementEvidenceDiscoveryState.Known, evidence.DiscoveryState, "Agreeing package identities must resolve the active increment.");
        TestAssert.Equal("IE-INC-READY-001", evidence.IncrementId, "The adapter must retain the stable increment identity.");
        TestAssert.True(evidence.Observations.Any(observation => observation.EvidenceKind == ManagementEvidenceKind.ReadinessCheck && observation.SourceRecordId == "P01"), "P01 must be captured as readiness evidence.");
        TestAssert.True(evidence.Observations.Any(observation => observation.EvidenceKind == ManagementEvidenceKind.DecisionRecord && observation.SourceRecordId == "D0"), "D0 must be captured as a decision record.");
        TestAssert.True(evidence.Observations.Any(observation => observation.EvidenceKind == ManagementEvidenceKind.HumanAction && observation.SourceRecordId == "HA-001"), "HA-001 must be captured as human action evidence.");
        TestAssert.True(evidence.Observations.Any(observation => observation.EvidenceKind == ManagementEvidenceKind.GateExecution && observation.StateCode == "NOT-RUN"), "PG4 gate execution must be separate evidence.");
        TestAssert.True(evidence.Observations.Any(observation => observation.EvidenceKind == ManagementEvidenceKind.GateOutcome && observation.ResultCode == "NOT-APPLICABLE"), "PG4 gate outcome must be separate evidence.");
        TestAssert.True(evidence.Observations.Any(observation => observation.EvidenceKind == ManagementEvidenceKind.ChecklistContext), "Checklist context must remain distinguishable from execution evidence.");
    }

    public static void ReadinessAdapterRetainsStateAndResultWithoutFabricatingPass()
    {
        var evidence = new IdeaEngineeringReadinessAdapter().Adapt(Snapshot(), PlanningProject()).Evidence;
        var p01 = evidence.Observations.Single(observation => observation.EvidenceKind == ManagementEvidenceKind.ReadinessCheck && observation.SourceRecordId == "P01");

        TestAssert.Equal("IN-PROGRESS", p01.StateCode, "P01 task state must remain IN-PROGRESS.");
        TestAssert.Equal("NOT-RUN", p01.ResultCode, "P01 result must remain NOT-RUN.");
        TestAssert.False(evidence.Observations.Any(observation => observation.ResultCode == "PASS"), "Prepared checklist content must not fabricate a PASS.");
    }

    public static void ReadinessAdapterRejectsIllegalGateExecutionOutcomePair()
    {
        var readmePath = $"{Increment}/README.md";
        var readme = Snapshot().Documents.Single(document => document.RelativeFile == readmePath).Content;
        var snapshot = Snapshot().WithDocument(
            readmePath,
            readme.Replace("NOT-RUN", "IN-PROGRESS", StringComparison.Ordinal)
                .Replace("NOT-APPLICABLE", "PASS", StringComparison.Ordinal));

        var evidence = new IdeaEngineeringReadinessAdapter().Adapt(snapshot, PlanningProject()).Evidence;

        TestAssert.Contains(
            "GATE_EXECUTION_OUTCOME_INVALID",
            string.Join('|', evidence.Diagnostics.Select(diagnostic => diagnostic.Code)),
            "An illegal gate execution/outcome pair must be diagnosed.");
    }

    public static void ReadinessAdapterResultNeverSerializesRawSourceOrAbsolutePath()
    {
        var evidence = new IdeaEngineeringReadinessAdapter().Adapt(Snapshot(), PlanningProject()).Evidence;
        var json = JsonSerializer.Serialize(evidence);

        TestAssert.False(json.Contains("P01 | Baseline", StringComparison.Ordinal), "Evidence JSON must not contain raw source rows.");
        TestAssert.False(json.Contains("Users", StringComparison.OrdinalIgnoreCase), "Evidence JSON must not contain absolute local paths.");
        TestAssert.True(evidence.Observations.SelectMany(observation => observation.SourceReferences).All(reference => !Path.IsPathRooted(reference.RelativeFile)), "Evidence references must stay relative.");
    }

    public static void ReadinessAdapterReturnsUnknownWithoutAnAgreedActiveIncrement()
    {
        var snapshot = Snapshot();
        var withoutIdentity = snapshot with
        {
            Documents = snapshot.Documents
                .Where(document => !document.RelativeFile.EndsWith("/README.md", StringComparison.OrdinalIgnoreCase)
                                   && !document.RelativeFile.EndsWith("/readiness-register.md", StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };

        var evidence = new IdeaEngineeringReadinessAdapter().Adapt(withoutIdentity, PlanningProject()).Evidence;

        TestAssert.Equal(ManagementEvidenceDiscoveryState.Unknown, evidence.DiscoveryState, "The adapter must not guess an increment when identity documents are absent.");
        TestAssert.Contains("ACTIVE_INCREMENT_UNKNOWN", string.Join('|', evidence.Diagnostics.Select(diagnostic => diagnostic.Code)), "Unknown active increment discovery must be explicit.");
    }

    public static void ReadinessAdapterReturnsAmbiguousForMultipleAgreedIncrements()
    {
        var snapshot = Snapshot();
        const string secondIncrement = "specs/005-another-readiness";
        var duplicateDocuments = snapshot.Documents
            .Where(document => document.RelativeFile.StartsWith(Increment, StringComparison.Ordinal))
            .Select(document => document with
            {
                Id = $"duplicate-{document.Id}",
                RelativeFile = document.RelativeFile.Replace(Increment, secondIncrement, StringComparison.Ordinal),
                SourceReference = document.SourceReference with
                {
                    RelativeFile = document.SourceReference.RelativeFile.Replace(Increment, secondIncrement, StringComparison.Ordinal)
                }
            })
            .ToArray();
        var ambiguousSnapshot = snapshot with { Documents = snapshot.Documents.Concat(duplicateDocuments).ToArray() };

        var evidence = new IdeaEngineeringReadinessAdapter().Adapt(ambiguousSnapshot, PlanningProject()).Evidence;

        TestAssert.Equal(ManagementEvidenceDiscoveryState.Ambiguous, evidence.DiscoveryState, "The adapter must not choose between multiple agreeing increment identities.");
        TestAssert.Contains("ACTIVE_INCREMENT_AMBIGUOUS", string.Join('|', evidence.Diagnostics.Select(diagnostic => diagnostic.Code)), "Ambiguous active increment discovery must be explicit.");
    }

    public static void ReadinessAdapterReportsMissingDeclaredSource()
    {
        var snapshot = Snapshot();
        var withoutTrace = snapshot with
        {
            Documents = snapshot.Documents
                .Where(document => !document.RelativeFile.EndsWith("/trace-matrix.md", StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };

        var evidence = new IdeaEngineeringReadinessAdapter().Adapt(withoutTrace, PlanningProject()).Evidence;

        TestAssert.Contains("EVIDENCE_SOURCE_UNAVAILABLE", string.Join('|', evidence.Diagnostics.Select(diagnostic => diagnostic.Code)), "A missing declared readiness file must remain a structured availability diagnostic.");
    }

    public static void ReadinessAdapterPreservesConflictingRowsInsteadOfLastWriteWins()
    {
        var registerPath = $"{Increment}/readiness-register.md";
        var snapshot = Snapshot();
        var register = snapshot.Documents.Single(document => document.RelativeFile == registerPath).Content;
        var conflictingRow = "| P01 | Product Author | Before P02 | BLOCKS_PG4 | IN-PROGRESS | PASS | baseline-manifest.md | Reviewer pending |";
        var conflictSnapshot = snapshot.WithDocument(registerPath, register.Replace(
            "| P01 | Product Author | Before P02 | BLOCKS_PG4 | IN-PROGRESS | NOT-RUN | baseline-manifest.md | Reviewer pending |",
            "| P01 | Product Author | Before P02 | BLOCKS_PG4 | IN-PROGRESS | NOT-RUN | baseline-manifest.md | Reviewer pending |\n" + conflictingRow,
            StringComparison.Ordinal));

        var evidence = new IdeaEngineeringReadinessAdapter().Adapt(conflictSnapshot, PlanningProject()).Evidence;

        TestAssert.Equal(2, evidence.Observations.Count(observation => observation.EvidenceKind == ManagementEvidenceKind.ReadinessCheck && observation.SourceRecordId == "P01"), "Conflicting source rows must both remain observable.");
        TestAssert.Contains("EVIDENCE_CONFLICT", string.Join('|', evidence.Diagnostics.Select(diagnostic => diagnostic.Code)), "Conflicting attributable evidence must produce a structured conflict diagnostic.");
    }

    private static CanonicalProject PlanningProject() =>
        new()
        {
            WorkPackages = Enumerable.Range(1, 7)
                .Select(index => new WorkPackage { Id = $"P{index:00}", Name = $"Package {index:00}" })
                .ToArray()
        };

    private static RepositorySnapshot Snapshot()
    {
        var documents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"{Increment}/README.md"] =
                "# PH0 Readiness Register\n"
                + "**Increment**: IE-INC-READY-001 — Technical Pilot Implementation Readiness\n"
                + "**Version / status**: 1.1 / Draft\n\n"
                + "| Field | Value |\n|---|---|\n"
                + "| PG4 Gate Execution State | NOT-RUN |\n"
                + "| PG4 Gate Outcome | NOT-APPLICABLE |\n",
            [$"{Increment}/readiness-register.md"] =
                "# PH0 Readiness Register\n"
                + "**Increment**: IE-INC-READY-001 — Technical Pilot Implementation Readiness\n\n"
                + "## Work-package status\n"
                + "| Work package | Owner | Due condition | Gate effect | Task state | Result | Evidence link | Blocker / deviation |\n"
                + "|---|---|---|---|---|---|---|---|\n"
                + "| P01 | Product Author | Before P02 | BLOCKS_PG4 | IN-PROGRESS | NOT-RUN | baseline-manifest.md | Reviewer pending |\n"
                + "| P02 | Project Reviewer | After P01 | BLOCKS_PG4 | NOT-RUN | NOT-RUN | trace-matrix.md | P01 pending |\n"
                + "| P07 | Gate Authority | After P01–P06 | Authorization | NOT-RUN | NOT-RUN | NOT-RUN | Gate not organized |\n\n"
                + "## Decision records\n"
                + "| ID | Question / current state | Accountable owner / authority | Due condition / closure evidence | Affected work / gate effect |\n"
                + "|---|---|---|---|---|\n"
                + "| D0 | Successor disposition: OPEN | Product Decision Authority | Before PG4; decision record | BLOCKS_PG4 |\n\n"
                + "## Human Action Board\n"
                + "| Action ID | Task / package | Required role | Status | Effect |\n"
                + "|---|---|---|---|---|\n"
                + "| HA-001 | T006 / P01 | Project Reviewer | NOT-RUN | P01 remains open |\n",
            [$"{Increment}/contracts/decision-and-evidence-register.md"] = "# Decision and evidence register contract\n",
            [$"{Increment}/contracts/pg4-gate-record.md"] = "# PG4 gate record contract\n",
            [$"{Increment}/tasks.md"] = "# Tasks\n\n- [x] T004 prepare baseline\n- [ ] T006 obtain reviewer evidence\n",
            [$"{Increment}/trace-matrix.md"] = "# Trace matrix\n\n| Evidence status | Meaning |\n|---|---|\n| NOT-RUN | Not executed |\n"
        };

        return new RepositorySnapshot
        {
            RepositoryId = "local-fixture",
            RepositoryLabel = "local-repository",
            LocationLabel = "local-repository",
            ResolvedRef = "refs/heads/main",
            CaptureState = CaptureState.Known,
            Documents = documents.Select((pair, index) => new SourceDocument
            {
                Id = $"document-{index + 1:D2}",
                RelativeFile = pair.Key,
                Format = SourceDocumentFormat.Markdown,
                Content = pair.Value,
                SizeBytes = Encoding.UTF8.GetByteCount(pair.Value),
                SourceReference = new SourceReference
                {
                    SourceId = "local-fixture",
                    Repository = "local-repository",
                    ResolvedRef = "refs/heads/main",
                    RelativeFile = pair.Key,
                    ExtractionRule = "test"
                }
            }).ToArray()
        };
    }

    private static RepositorySnapshot WithDocument(this RepositorySnapshot snapshot, string relativeFile, string content) =>
        snapshot with
        {
            Documents = snapshot.Documents
                .Select(document => document.RelativeFile == relativeFile
                    ? document with { Content = content, SizeBytes = Encoding.UTF8.GetByteCount(content) }
                    : document)
                .ToArray()
        };
}
