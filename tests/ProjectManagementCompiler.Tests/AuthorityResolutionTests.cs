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
        TestAssert.Equal("Doc07", resolution.AuthorityDocument.Kind.ToString(), "DOC-07 should be selected authority.");
        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "CONFLICTING_BASELINE"), "Lower-ranked conflicting facts must remain diagnosable.");
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
