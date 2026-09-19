using System.Text;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ManagementEvidenceTests
{
    private const string Root = @"C:\fixture";
    private const string Increment = "specs/004-technical-pilot-readiness";

    public static void ManagementEvidencePreservesIndependentStatesAndTypedTargets()
    {
        var observation = new ManagementEvidenceObservation
        {
            Id = "readiness:P01",
            EvidenceKind = ManagementEvidenceKind.ReadinessCheck,
            SourceRecordId = "P01",
            StateCode = "IN-PROGRESS",
            ResultCode = "NOT-RUN",
            ExplicitTarget = new EvidenceTarget { Kind = "WorkPackage", Id = "P01" },
            SourceReferences =
            [
                new SourceReference
                {
                    Repository = "fixture",
                    RelativeFile = $"{Increment}/readiness-register.md",
                    ExtractionRule = "test"
                }
            ]
        };
        var evidence = new ManagementEvidence
        {
            DiscoveryState = ManagementEvidenceDiscoveryState.Known,
            IncrementPath = Increment,
            IncrementId = "IE-INC-READY-001",
            Observations = [observation],
            Reconciliations =
            [
                new EvidenceReconciliation
                {
                    ObservationId = observation.Id,
                    Status = EvidenceReconciliationStatus.Matched,
                    ResolvedTarget = new EvidenceTarget { Kind = "WorkPackage", Id = "P01" },
                    RuleId = "READINESS_WORK_PACKAGE",
                    Reason = "P01 exists in the canonical planning baseline."
                }
            ]
        };

        TestAssert.Equal("IN-PROGRESS", evidence.Observations.Single().StateCode, "Readiness state must be retained.");
        TestAssert.Equal("NOT-RUN", evidence.Observations.Single().ResultCode, "Readiness result must remain independent.");
        TestAssert.Equal("WorkPackage", evidence.Observations.Single().ExplicitTarget!.Kind, "Targets must retain their type.");
        TestAssert.Equal(EvidenceReconciliationStatus.Matched, evidence.Reconciliations.Single().Status, "Reconciliation status must be explicit.");
    }

    public static void CanonicalProjectDefaultsManagementEvidenceWithoutProfile()
    {
        var project = new CanonicalProject();

        TestAssert.Equal(ManagementEvidenceDiscoveryState.NotRequested, project.ManagementEvidence.DiscoveryState, "MVP1 projects need an empty not-requested evidence value.");
        TestAssert.Equal(0, project.ManagementEvidence.Observations.Count, "Baseline-only projects must not fabricate observations.");
    }

    public static void CanonicalJsonRoundTripsManagementEvidence()
    {
        var project = new CanonicalProject
        {
            Project = new Project { Id = "project-1", Name = "Readiness fixture", SourceIds = ["source-1"] },
            Sources =
            [
                new ProjectSource
                {
                    Id = "source-1",
                    Kind = "repository",
                    Repository = "fixture",
                    Documents =
                    [
                        new SourceDocument
                        {
                            Id = "doc-1",
                            RelativeFile = "README.md",
                            SourceReference = new SourceReference
                            {
                                SourceId = "source-1",
                                Repository = "fixture",
                                RelativeFile = "README.md",
                                ExtractionRule = "test"
                            }
                        }
                    ]
                }
            ],
            Baseline = new ProjectBaseline
            {
                Id = "baseline-1",
                Version = "1.0",
                Status = "Draft",
                AuthorityDocumentId = "doc-1",
                ValidationState = ValidationState.Known
            },
            ManagementEvidence = new ManagementEvidence
            {
                DiscoveryState = ManagementEvidenceDiscoveryState.Known,
                IncrementPath = Increment,
                IncrementId = "IE-INC-READY-001",
                Observations =
                [
                    new ManagementEvidenceObservation
                    {
                        Id = "gate:execution",
                        EvidenceKind = ManagementEvidenceKind.GateExecution,
                        SourceRecordId = "PG4",
                        StateCode = "NOT-RUN",
                        ResultCode = "NOT-APPLICABLE",
                        ObservedAtUtc = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero)
                    }
                ]
            }
        };

        var serializer = new CanonicalJsonSerializer();
        var reopened = serializer.Deserialize(serializer.Serialize(project));

        TestAssert.Equal(ManagementEvidenceDiscoveryState.Known, reopened.ManagementEvidence.DiscoveryState, "Evidence discovery state must survive JSON.");
        TestAssert.Equal("IE-INC-READY-001", reopened.ManagementEvidence.IncrementId, "Increment identity must survive JSON.");
        TestAssert.Equal("NOT-APPLICABLE", reopened.ManagementEvidence.Observations.Single().ResultCode, "Gate result must survive JSON.");
    }

    public static void ManagementEvidencePathPolicyStaysBelowSpecs()
    {
        var paths = SourcePathPolicy.GetManagementEvidencePaths(Increment);

        TestAssert.Equal("specs/004-technical-pilot-readiness/README.md", paths[0], "Readiness README must be selected first.");
        TestAssert.Equal("specs/004-technical-pilot-readiness/readiness-register.md", paths[1], "Readiness register must be selected second.");
        TestAssert.Equal(6, paths.Count, "The readiness profile must contain only its six declared files.");
        TestAssert.Throws<ArgumentException>(
            () => SourcePathPolicy.GetManagementEvidencePaths("docs/other"),
            "A readiness profile outside specs must be rejected.");
        TestAssert.Throws<ArgumentException>(
            () => SourcePathPolicy.GetManagementEvidencePaths("specs/../private"),
            "Traversal must be rejected before capture.");
    }

    public static void CaptureReadsReadinessAllowListInFixedOrder()
    {
        var fileSystem = new FakeRepositoryFileSystem();
        fileSystem.AddDirectory(Root);
        fileSystem.AddDirectory(Path.Combine(Root, "specs"));
        fileSystem.AddDirectory(Path.Combine(Root, "specs/004-technical-pilot-readiness"));
        fileSystem.AddDirectory(Path.Combine(Root, "specs/004-technical-pilot-readiness/contracts"));
        foreach (var path in SourcePathPolicy.RecognizedPaths.Concat(SourcePathPolicy.GetManagementEvidencePaths(Increment)))
        {
            fileSystem.AddFile(Path.Combine(Root, path), $"# {path}");
        }

        var snapshot = new LocalRepositorySourceAdapter(fileSystem)
            .CaptureAsync(
                new SourceRequest { Location = Root, ManagementEvidenceIncrementPath = Increment },
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        TestAssert.Equal(11, snapshot.Documents.Count, "The optional profile must add six declared readiness files to the five MVP1 files.");
        TestAssert.True(
            snapshot.Documents.Skip(5).Select(document => document.RelativeFile).SequenceEqual(SourcePathPolicy.GetManagementEvidencePaths(Increment)),
            "Readiness documents must follow the declared stable order.");
        TestAssert.False(snapshot.Documents.Any(document => document.RelativeFile.Contains("run.ps1", StringComparison.OrdinalIgnoreCase)), "Capture must not enumerate arbitrary files.");
    }

    public static void SemanticDigestWillIncludeEvidenceMeaningButNotCaptureTime()
    {
        var first = new CanonicalProject
        {
            ManagementEvidence = new ManagementEvidence
            {
                DiscoveryState = ManagementEvidenceDiscoveryState.Known,
                IncrementId = "IE-INC-READY-001",
                CapturedAtUtc = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero),
                Observations =
                [
                    new ManagementEvidenceObservation
                    {
                        Id = "readiness:P01",
                        EvidenceKind = ManagementEvidenceKind.ReadinessCheck,
                        SourceRecordId = "P01",
                        ResultCode = "NOT-RUN"
                    }
                ]
            }
        };
        var second = first with
        {
            ManagementEvidence = first.ManagementEvidence with
            {
                CapturedAtUtc = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero)
            }
        };

        TestAssert.Equal(CanonicalJsonDigest.Compute(first), CanonicalJsonDigest.Compute(second), "Capture timestamps must not change semantic digest.");
    }
}
