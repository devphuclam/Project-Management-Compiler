using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class ReconciliationTests
{
    public static void TypedReadinessTargetMatchesWorkPackageAndManagementRecordsRemainUnmatched()
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
                },
                new ManagementEvidenceObservation
                {
                    Id = "decision:D0",
                    EvidenceKind = ManagementEvidenceKind.DecisionRecord,
                    SourceRecordId = "D0"
                },
                new ManagementEvidenceObservation
                {
                    Id = "gate:execution",
                    EvidenceKind = ManagementEvidenceKind.GateExecution,
                    SourceRecordId = "PG4",
                    ExplicitTarget = new EvidenceTarget { Kind = "Gate", Id = "PG4" }
                }
            ]
        };
        var project = new CanonicalProject
        {
            WorkPackages = [new WorkPackage { Id = "P04" }],
            DeliveryCards = [new DeliveryCard { Id = "P04", WorkPackageId = "P04" }]
        };

        var result = new ManagementEvidenceReconciler().Reconcile(evidence, project);

        TestAssert.Equal(EvidenceReconciliationStatus.Matched, result.Reconciliations.Single(item => item.ObservationId == "readiness:P04").Status, "P04 readiness must match the typed WorkPackage target.");
        TestAssert.Equal(EvidenceReconciliationStatus.Unmatched, result.Reconciliations.Single(item => item.ObservationId == "decision:D0").Status, "Decisions must not be assigned to executable work by default.");
        TestAssert.Equal(EvidenceReconciliationStatus.Unmatched, result.Reconciliations.Single(item => item.ObservationId == "gate:execution").Status, "PG4 must remain a management record without a default planning target.");
    }

    public static void InvalidAndAmbiguousTargetsAreExplicit()
    {
        var evidence = new ManagementEvidence
        {
            DiscoveryState = ManagementEvidenceDiscoveryState.Known,
            Observations =
            [
                new ManagementEvidenceObservation
                {
                    Id = "invalid",
                    EvidenceKind = ManagementEvidenceKind.ReadinessCheck,
                    SourceRecordId = "P04",
                    ExplicitTarget = new EvidenceTarget { Kind = "UnknownKind", Id = "P04" }
                },
                new ManagementEvidenceObservation
                {
                    Id = "ambiguous",
                    EvidenceKind = ManagementEvidenceKind.ReadinessCheck,
                    SourceRecordId = "P04",
                    ExplicitTarget = new EvidenceTarget { Kind = "WorkPackage", Id = "P04" }
                }
            ]
        };
        var project = new CanonicalProject
        {
            WorkPackages =
            [
                new WorkPackage { Id = "P04" },
                new WorkPackage { Id = "P04" }
            ]
        };

        var result = new ManagementEvidenceReconciler().Reconcile(evidence, project);

        TestAssert.Equal(EvidenceReconciliationStatus.Invalid, result.Reconciliations.Single(item => item.ObservationId == "invalid").Status, "Unknown target kinds must be invalid.");
        TestAssert.Equal(EvidenceReconciliationStatus.Ambiguous, result.Reconciliations.Single(item => item.ObservationId == "ambiguous").Status, "Duplicate typed candidates must be ambiguous.");
        TestAssert.Contains("EVIDENCE_TARGET_INVALID", string.Join('|', result.Diagnostics.Select(diagnostic => diagnostic.Code)), "Invalid targets need a structured diagnostic.");
        TestAssert.Contains("EVIDENCE_TARGET_AMBIGUOUS", string.Join('|', result.Diagnostics.Select(diagnostic => diagnostic.Code)), "Ambiguous targets need a structured diagnostic.");
    }

    public static void ReconciliationDoesNotChangePlanningFacts()
    {
        var project = new CanonicalProject
        {
            Project = new Project { Id = "project-1", Name = "Planning" },
            Baseline = new ProjectBaseline { Id = "baseline-1", Version = "1.0", Status = "Draft" },
            WorkPackages = [new WorkPackage { Id = "P01", Name = "Baseline package" }]
        };
        var evidence = new ManagementEvidence
        {
            Observations =
            [
                new ManagementEvidenceObservation
                {
                    Id = "readiness:P01",
                    EvidenceKind = ManagementEvidenceKind.ReadinessCheck,
                    SourceRecordId = "P01",
                    ExplicitTarget = new EvidenceTarget { Kind = "WorkPackage", Id = "P01" },
                    ResultCode = "FAIL"
                }
            ]
        };

        var reconciled = new ManagementEvidenceReconciler().Reconcile(evidence, project);

        TestAssert.Equal("Baseline package", project.WorkPackages.Single().Name, "Reconciliation must not mutate planning names.");
        TestAssert.Equal("1.0", project.Baseline.Version, "Reconciliation must not mutate baseline version.");
        TestAssert.Equal("FAIL", reconciled.Observations.Single().ResultCode, "Evidence result must remain evidence.");
    }
}
