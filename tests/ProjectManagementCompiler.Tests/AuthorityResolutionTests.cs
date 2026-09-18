using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class AuthorityResolutionTests
{
    public static void Doc07WinsPrecedenceAndResolvesTypedPlanningFacts()
    {
        var snapshot = new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("README.md", "# README\n\n| Field | Value |\n|---|---|\n| Project ID | readme-project |\n| WIP policy | 9 |"),
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | authoritative-project |\n| Baseline ID | baseline-01 |\n| Baseline version | 0.14 |\n\n## Phases\n\n| Phase ID | Name | Start | Finish | Gate |\n|---|---|---|---|---|\n| PH0 | Khởi động | 2026-09-18 | 2026-09-25 | G-D0 |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Phase | Start | Finish | Effort | Duration | Completion condition |\n|---|---|---|---|---|---:|---:|---|\n| P01 | Gói P01 | PH0 | 2026-09-18 | 2026-09-18 | 16 | 480 | Reviewed |"),
                Document("docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html", "<h1>Gantt</h1><table><tr><th>Phase</th><th>Name</th></tr><tr data-phase=\"PH0\"><td>PH0</td><td>Gantt</td></tr></table>"),
                Document("docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md", "# Kanban\n\n## Planning policy\n\n| Policy | Value |\n|---|---|\n| WIP policy | 1 |")
            ]
        };

        var resolution = AuthorityResolution.Resolve(snapshot);

        TestAssert.True(resolution.HasCanonicalBaseline, "DOC-07 and Appendix A should establish a canonical baseline.");
        TestAssert.Equal("authoritative-project", resolution.ProjectId, "DOC-07 should own project identity.");
        TestAssert.Equal("baseline-01", resolution.BaselineId, "DOC-07 should own baseline identity.");
        TestAssert.Equal("0.14", resolution.BaselineVersion, "DOC-07 should own baseline version.");
        TestAssert.Equal(1, resolution.PhaseRows.Count, "DOC-07 phase rows should be typed and retained.");
        TestAssert.Equal("1", resolution.PolicyFacts["WIP policy"].Value, "Higher-ranked policy facts should win.");
        TestAssert.Equal("Doc07", resolution.AuthorityDocument!.Kind.ToString(), "DOC-07 should be selected authority.");
        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "CONFLICTING_BASELINE"), "Lower-ranked conflicting facts must remain diagnosable.");
    }

    public static void MissingAuthorityIsSafeAndCannotBecomeCanonical()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.False(resolution.HasCanonicalBaseline, "Appendix A without DOC-07 must not establish a canonical baseline.");
        TestAssert.True(resolution.AuthorityDocument is null, "The authority document must be nullable when DOC-07 is absent.");
        TestAssert.True(resolution.Baseline.ProjectId is null, "Missing DOC-07 must not cause guessed authority values.");
    }

    public static void RequiredDocumentErrorsDisableCanonicalBaseline()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Not Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.False(resolution.HasCanonicalBaseline, "Required-document Error diagnostics must disable canonical baseline resolution.");
        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "MISSING_REQUIRED_HEADING"), "Required document heading errors must remain visible.");
    }

    public static void UnsupportedRequiredDocumentFormatDisablesCanonicalBaseline()
    {
        var unsupportedDoc07 = Document(
            "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
            "# Source plan\n\n## Source identity\n\n## Phases") with
        {
            Format = SourceDocumentFormat.Text
        };
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                unsupportedDoc07,
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.False(resolution.HasCanonicalBaseline, "Unsupported DOC-07 format must disable canonical baseline resolution.");
        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "UNSUPPORTED_DOCUMENT_FORMAT"), "Unsupported required document format must remain explicit.");
    }

    public static void GenericSemanticValuesProduceErrorsAndDoNotBecomeNullSilently()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Planning start | 18/09/2026 |\n| Planning finish | nope |\n| Target date | 2026-13-40 |\n| Authoritative effort | twelve hours |\n| Initial reserve | 4x hours |\n| Capacity | 100x hours |\n| WIP policy | one |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "MALFORMED_DATE"), "Malformed generic date values must be explicit.");
        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "MALFORMED_NUMERIC"), "Malformed generic numeric values must be explicit.");
        TestAssert.False(resolution.HasCanonicalBaseline, "Error diagnostics in DOC-07 must disable canonical baseline resolution.");
        TestAssert.True(resolution.PolicyFacts["Planning start"].Value == "18/09/2026", "The authored malformed value must remain observable in policy facts.");
        TestAssert.True(resolution.Baseline.PlanningStart is null && resolution.Baseline.PlanningFinish is null, "Invalid typed dates must not be guessed.");
        TestAssert.True(resolution.Baseline.PlannedEffortHours is null && resolution.Baseline.ReserveHours is null && resolution.Baseline.CapacityHours is null, "Invalid typed numerics must not be silently converted.");
    }

    public static void TypedBaselineValuesOnlyUseDoc07AuthorityFacts()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| Field | Value |\n|---|---|\n| Planning start | 2026-09-18 |\n| Authoritative effort | 8 hours |\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.True(resolution.Baseline.PlanningStart is null, "Lower-ranked sources must not fill DOC-07-owned typed baseline fields.");
        TestAssert.True(resolution.Baseline.PlannedEffortHours is null, "Lower-ranked effort must not fill an omitted DOC-07-owned field.");
        TestAssert.Equal("2026-09-18", resolution.PolicyFacts["Planning start"].Value, "Lower-ranked facts remain available for policy extraction.");
        TestAssert.Equal("8 hours", resolution.PolicyFacts["Authoritative effort"].Value, "Lower-ranked facts remain available for diagnostics and policy extraction.");
    }

    public static void AllRowsAreRetainedAndOrdinaryRowConflictsPreserveSources()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n\n## Phases\n\n| ID | Name |\n|---|---|\n| ROW-1 | Authority row |\n\n## Other\n\n| ID | Name |\n|---|---|\n| DOC-ROW | Doc row |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| ROW-1 | Appendix row | 1 |\n| P01 | Package | 2 |")
            ]
        });

        TestAssert.True(resolution.Rows.Count >= 4, "All valid parsed rows from authority and subordinate documents must be retained.");
        TestAssert.Equal("Project ID", resolution.Rows[0].Cells["Field"], "Rows must be retained in deterministic document order.");
        TestAssert.True(resolution.Rows.Any(row => row.Cells.TryGetValue("ID", out var id) && id == "ROW-1"), "The ordinary row must be retained.");
        var conflict = resolution.Conflicts.Single(d => d.Code == "CONFLICTING_ROW");
        TestAssert.Equal(2, conflict.SourceReferences.Count, "Ordinary row conflicts must preserve both source references.");
        TestAssert.Contains("ROW-1", conflict.Message, "Ordinary row conflicts must identify the logical row.");
    }

    public static void StaleAppendixControlEnvelopeReferenceIsWarning()
    {
        var snapshot = new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n\nDOC-07@0.14"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\nThis appendix references DOC-07@0.13.\n\n| ID | Name | Phase | Start | Finish | Effort | Duration | Completion condition |\n|---|---|---|---|---|---:|---:|---|\n| P01 | Gói P01 | PH0 | 2026-09-18 | 2026-09-18 | 16 | 480 | Reviewed |")
            ]
        };

        var resolution = AuthorityResolution.Resolve(snapshot);

        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "STALE_SUBORDINATE_REFERENCE"), "A stale Appendix control-envelope reference must be visible.");
        TestAssert.Equal("0.14", resolution.BaselineVersion, "The current DOC-07 authority must be retained.");
    }

    public static void FutureSubordinateControlEnvelopeReferenceUsesNumericVersionComparison()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.9 |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |\n\nDOC-07@0.9"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\nDOC-07@0.10\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "FUTURE_SUBORDINATE_REFERENCE"), "A numerically newer subordinate version must produce a distinct warning.");
        TestAssert.False(resolution.Diagnostics.Any(d => d.Code == "STALE_SUBORDINATE_REFERENCE"), "A numerically newer subordinate version must not be classified as stale.");
    }

    public static void ControlledFixtureResolvesBaselinePhasesAndPolicyFacts()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering");
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(new SourceRequest { Location = root }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        var resolution = AuthorityResolution.Resolve(snapshot);

        TestAssert.True(resolution.HasCanonicalBaseline, "The controlled fixture should have the two required baseline documents.");
        TestAssert.Equal("idea-ddm-technical-pilot-2026", resolution.ProjectId, "Fixture project ID should come from DOC-07.");
        TestAssert.Equal("IE-PLAN-DEC2026-002", resolution.BaselineId, "Fixture baseline ID should come from DOC-07.");
        TestAssert.Equal("0.1", resolution.BaselineVersion, "Fixture baseline version should come from DOC-07.");
        TestAssert.Equal(6, resolution.PhaseRows.Count, "Fixture DOC-07 should expose six phase rows.");
        TestAssert.Equal("1", resolution.PolicyFacts["WIP policy"].Value, "Fixture WIP policy should be retained as a typed fact.");
        TestAssert.Equal("512 hours", resolution.PolicyFacts["Authoritative effort"].Value, "Fixture authoritative effort should retain its source units.");
        TestAssert.Equal(512m, resolution.Baseline.PlannedEffortHours, "Fixture authoritative effort should also expose a safe typed numeric value.");
    }

    private static SourceDocument Document(string relativeFile, string content) => new()
    {
        Id = relativeFile,
        RelativeFile = relativeFile,
        Format = relativeFile.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? SourceDocumentFormat.Html
            : SourceDocumentFormat.Markdown,
        Content = content,
        SourceReference = new SourceReference
        {
            SourceId = "fixture",
            Repository = "fixture",
            RelativeFile = relativeFile,
            ExtractionRule = "test"
        }
    };
}
