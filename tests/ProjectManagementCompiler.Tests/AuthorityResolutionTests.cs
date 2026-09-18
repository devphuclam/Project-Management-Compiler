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
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | authoritative-project |\n| Baseline ID | baseline-01 |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name | Start | Finish | Gate |\n|---|---|---|---|---|\n| PH0 | Khởi động | 2026-09-18 | 2026-09-25 | G-D0 |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Phase | Start | Finish | Effort | Duration | Completion condition |\n|---|---|---|---|---|---:|---:|---|\n| P01 | Gói P01 | PH0 | 2026-09-18 | 2026-09-18 | 16 | 480 | Reviewed |"),
                Document("docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html", "<h1>Gantt</h1><table><tr><th>Phase</th><th>Name</th></tr><tr data-phase=\"PH0\"><td>PH0</td><td>Gantt</td></tr></table>"),
                Document("docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md", "# Kanban\n\n## Planning policy\n\n| Policy | Value |\n|---|---|\n| WIP policy | 1 |\n\n## Delivery cards")
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

    public static void MissingRequiredBaselineDataAndRowsDisableCanonicalBaseline()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n## Phases"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A")
            ]
        });

        TestAssert.False(resolution.HasCanonicalBaseline, "Headings without usable baseline data or rows must not establish a canonical baseline.");
        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "MISSING_REQUIRED_BASELINE_DATA" && d.Severity == WarningSeverity.Error), "Missing core baseline data must be an explicit Error diagnostic.");
        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "MISSING_REQUIRED_BASELINE_ROWS" && d.Severity == WarningSeverity.Error), "Missing phase and work-package rows must be an explicit Error diagnostic.");
    }

    public static void MissingRequiredBaselineFactsDisableCanonicalBaselineWithoutGuessing()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.False(resolution.HasCanonicalBaseline, "Omitted core DOC-07 facts must block canonical baseline status.");
        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "MISSING_REQUIRED_BASELINE_DATA" && d.Severity == WarningSeverity.Error && d.AffectedIds.Contains("Baseline ID")), "Missing baseline identity must identify the omitted fact.");
        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "MISSING_REQUIRED_BASELINE_DATA" && d.AffectedIds.Contains("Authoritative effort")), "Missing authoritative effort must identify the omitted fact.");
        TestAssert.True(resolution.Baseline.BaselineId is null && resolution.Baseline.PlanningStart is null && resolution.Baseline.PlannedEffortHours is null, "Missing facts must remain nullable rather than being guessed.");
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

    public static void OrdinaryRowConflictsAreDetectedWithinOneSourceDocument()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |\n\n## First rendition\n\n| ID | Name |\n|---|---|\n| ROW-1 | First value |\n\n## Second rendition\n\n| ID | Name |\n|---|---|\n| ROW-1 | Different value |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        var conflict = resolution.Conflicts.Single(diagnostic => diagnostic.Code == "CONFLICTING_ROW");
        TestAssert.Contains("ROW-1", conflict.Message, "Same-document ordinary row conflicts must identify the logical row.");
        TestAssert.Equal(2, conflict.SourceReferences.Count, "Same-document row conflicts must retain both source references.");
        TestAssert.Equal(2, resolution.Rows.Count(row => row.Cells.TryGetValue("ID", out var id) && id == "ROW-1"), "Both differing same-document rows must be retained.");
    }

    public static void StaleAppendixControlEnvelopeReferenceIsWarning()
    {
        var snapshot = new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |\n\nDOC-07@0.14"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\nThis appendix references DOC-07@0.13.\n\n| ID | Name | Phase | Start | Finish | Effort | Duration | Completion condition |\n|---|---|---|---|---|---:|---:|---|\n| P01 | Gói P01 | PH0 | 2026-09-18 | 2026-09-18 | 16 | 480 | Reviewed |")
            ]
        };

        var resolution = AuthorityResolution.Resolve(snapshot);

        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "STALE_SUBORDINATE_REFERENCE"), "A stale Appendix control-envelope reference must be visible.");
        TestAssert.Equal("0.14", resolution.BaselineVersion, "The current DOC-07 authority must be retained.");
        TestAssert.True(resolution.HasCanonicalBaseline, "A warning-only stale subordinate reference must preserve canonical baseline status.");
    }

    public static void BaselineVersionControlsSubordinateEnvelopeReferencesWithoutLiteralAuthorityMarker()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\nDOC-07@0.13\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.Equal("0.14", resolution.BaselineVersion, "The extracted DOC-07 baseline version must be authoritative without a literal marker.");
        TestAssert.True(resolution.Diagnostics.Any(diagnostic => diagnostic.Code == "STALE_SUBORDINATE_REFERENCE"), "A subordinate marker must be compared with the extracted DOC-07 baseline version.");
        TestAssert.True(resolution.HasCanonicalBaseline, "A warning-only stale subordinate reference must preserve canonical status.");
    }

    public static void LiteralAuthorityEnvelopeMustMatchExtractedBaselineVersion()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |\n\nDOC-07@0.15"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.Equal("0.14", resolution.BaselineVersion, "The extracted baseline version must remain deterministic when a literal marker conflicts.");
        TestAssert.True(resolution.Diagnostics.Any(diagnostic => diagnostic.Code == "CONFLICTING_AUTHORITY_CONTROL_ENVELOPE" && diagnostic.Severity == WarningSeverity.Error), "A valid literal authority marker that differs from the baseline fact must be an Error.");
        TestAssert.False(resolution.HasCanonicalBaseline, "Conflicting authority-envelope evidence must fail the global Error gate.");
    }

    public static void HistoricalAuthorityEnvelopeReferencesUseDocumentVersionAndDoNotConflict()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Control envelope\n\n| Field | Recorded value |\n|---|---|\n| Stable Document ID | project |\n| Document Version | 0.14 |\n| Document Status | Draft |\n| Target date | 2026-09-25 |\n| Change Record | predecessor DOC-07@0.12 |\n| Supersedes / Superseded by | Supersedes DOC-07@0.13 |\n\n## Technical Pilot schedule\n\nSchedule baseline: `baseline@0.1`\n\n| Item | Planned hours / condition |\n|---|---|\n| Planning window | 18 September–25 September 2026 |\n| Planned phase work | 16 hours |\n\n## Phase sequence\n\n| Phase | Work-package range | Planned start | Planned finish |\n|---|---|---|---|\n| PH0 | P01 | 2026-09-18 | 2026-09-25 |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.Equal("0.1", resolution.BaselineVersion, "Schedule baseline version must remain separate from the DOC-07 document version.");
        TestAssert.False(resolution.Diagnostics.Any(diagnostic => diagnostic.Code == "CONFLICTING_AUTHORITY_CONTROL_ENVELOPE"), "Historical change/supersedes references must not be treated as current authority conflicts.");
        TestAssert.True(resolution.HasCanonicalBaseline, "A valid DOC-07 and Appendix pair with historical envelope references must remain canonical.");
    }

    public static void FutureSubordinateControlEnvelopeReferenceUsesNumericVersionComparison()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.9 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |\n\nDOC-07@0.9"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\nDOC-07@0.10\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "FUTURE_SUBORDINATE_REFERENCE"), "A numerically newer subordinate version must produce a distinct warning.");
        TestAssert.False(resolution.Diagnostics.Any(d => d.Code == "STALE_SUBORDINATE_REFERENCE"), "A numerically newer subordinate version must not be classified as stale.");
        TestAssert.True(resolution.HasCanonicalBaseline, "A warning-only future subordinate reference must preserve canonical baseline status.");
    }

    public static void MalformedSubordinateControlEnvelopeReferencesAreErrorsAndDoNotThrow()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |\n\nDOC-07@0.14"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\nDOC-07@999999999999999999999.1 and DOC-07@1.bad\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.True(resolution.Diagnostics.Count(d => d.Code == "MALFORMED_CONTROL_ENVELOPE_REFERENCE" && d.Severity == WarningSeverity.Error) >= 2, "Overflow and malformed control-envelope versions must produce explicit Error diagnostics.");
        TestAssert.False(resolution.Diagnostics.Any(d => d.Code is "STALE_SUBORDINATE_REFERENCE" or "FUTURE_SUBORDINATE_REFERENCE"), "Malformed versions must not be classified as stale or future.");
        TestAssert.False(resolution.HasCanonicalBaseline, "Any Error diagnostic, including a subordinate control-envelope error, must disable canonical baseline status.");
    }

    public static void FencedSubordinateControlEnvelopeReferencesAreIgnored()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |\n\nDOC-07@0.14"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |\n\n```text\nDOC-07@999.999\n```")
            ]
        });

        TestAssert.False(resolution.Diagnostics.Any(d => d.Code is "STALE_SUBORDINATE_REFERENCE" or "FUTURE_SUBORDINATE_REFERENCE" or "MALFORMED_CONTROL_ENVELOPE_REFERENCE"), "DOC-07 references inside Markdown fences must not be scanned.");
        TestAssert.True(resolution.HasCanonicalBaseline, "Ignoring a fenced subordinate reference must preserve canonical baseline status.");
    }

    public static void ConflictingAuthorityControlEnvelopeReferencesAreErrors()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |\n\nDOC-07@0.14\nDOC-07@0.15"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.Equal("0.14", resolution.BaselineVersion, "The first normalized authority envelope version must remain current.");
        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "CONFLICTING_AUTHORITY_CONTROL_ENVELOPE" && d.Severity == WarningSeverity.Error), "Semantically different authority envelope versions must be explicit Error diagnostics.");
        TestAssert.False(resolution.HasCanonicalBaseline, "A conflicting authority envelope must fail the global Error gate.");
    }

    public static void EquivalentAuthorityControlEnvelopeReferencesAreSafe()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |\n\nDOC-07@0.14\nDOC-07@0.14.0"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.False(resolution.Diagnostics.Any(d => d.Code == "CONFLICTING_AUTHORITY_CONTROL_ENVELOPE"), "Equivalent dotted authority envelope versions must not conflict.");
        TestAssert.True(resolution.HasCanonicalBaseline, "Equivalent authority envelope versions must preserve canonical baseline status.");
    }

    public static void MalformedBaselineVersionsDisableCanonicalBaseline()
    {
        foreach (var version in new[] { "1", "1.x", "999999999999999999999999.1" })
        {
            var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
            {
                RepositoryId = "fixture",
                Documents =
                [
                    Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", $"# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | {version} |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |"),
                    Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
                ]
            });

            TestAssert.True(resolution.Diagnostics.Any(diagnostic => diagnostic.Code == "MALFORMED_BASELINE_VERSION" && diagnostic.Severity == WarningSeverity.Error), $"Baseline version '{version}' must produce an explicit Error diagnostic.");
            TestAssert.False(resolution.HasCanonicalBaseline, $"Malformed baseline version '{version}' must disable canonical baseline status.");
        }
    }

    public static void EquivalentDottedBaselineVersionsRemainCanonical()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14.0 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |\n\nDOC-07@0.14"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.False(resolution.Diagnostics.Any(diagnostic => diagnostic.Code == "MALFORMED_BASELINE_VERSION"), "A valid dotted baseline version must not be diagnosed as malformed.");
        TestAssert.True(resolution.HasCanonicalBaseline, "A valid dotted baseline version equivalent to a control-envelope reference must remain canonical.");
    }

    public static void UnterminatedMarkdownFenceDisablesCanonicalBaseline()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |\n\n```markdown\n| Fake | Value |\n|---|---|\n| F01 | Do not parse |")
            ]
        });

        TestAssert.True(resolution.Diagnostics.Any(d => d.Code == "UNTERMINATED_MARKDOWN_FENCE" && d.Severity == WarningSeverity.Error), "An unterminated Markdown fence must be an explicit Error diagnostic.");
        TestAssert.False(resolution.HasCanonicalBaseline, "An unterminated Markdown fence must fail canonical status through the complete error rule.");
    }

    public static void ProjectNameFallbackIgnoresHeadingsInsideMarkdownFences()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "```markdown\n# Fake fenced project\n```\n\n# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |")
            ]
        });

        TestAssert.Equal("Source plan", resolution.ProjectName, "Project-name fallback must use visible Markdown headings only.");
    }

    public static void UnmatchedHtmlClosingTagsDisableCanonicalBaseline()
    {
        var resolution = AuthorityResolution.Resolve(new RepositorySnapshot
        {
            RepositoryId = "fixture",
            Documents =
            [
                Document("docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md", "# Source plan\n\n## Source identity\n\n| Field | Value |\n|---|---|\n| Project ID | project |\n| Baseline ID | baseline |\n| Baseline version | 0.14 |\n| Planning start | 2026-09-18 |\n| Planning finish | 2026-09-25 |\n| Target date | 2026-09-25 |\n| Authoritative effort | 16 hours |\n\n## Phases\n\n| Phase ID | Name |\n|---|---|\n| PH0 | Phase |"),
                Document("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md", "# Appendix A\n\n| ID | Name | Effort |\n|---|---|---:|\n| P01 | Package | 1 |"),
                Document("docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html", "<h1>Gantt</h1></table><table><tr><th>ID</th></tr><tr><td>A01</td></tr></table>")
            ]
        });

        TestAssert.True(resolution.Diagnostics.Any(diagnostic => diagnostic.Code == "UNMATCHED_HTML_CLOSING_TAG" && diagnostic.Severity == WarningSeverity.Error), "Malformed HTML must remain an explicit Error in authority resolution.");
        TestAssert.True(resolution.HasCanonicalBaseline, "Malformed subordinate Gantt HTML must not destroy a valid DOC-07 plus Appendix baseline.");
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
