# MVP1 Project Management Compiler Implementation Plan

> This plan is executable from the implementation worktree created from
> `main` after the written-design amendment commit `ad924b8`.

## Goal

Implement the approved MVP1 vertical slice for the public
`devphuclam/Project-Management-Compiler` repository:

```text
allow-listed local repository
  -> safe source snapshot
  -> deterministic IDEAEngineering planning extraction
  -> canonical project model
  -> management analysis
  -> browser review plus JSON/XLSX outputs
```

The implementation is a dependency-free .NET 10 modular monolith. It uses only
installed platform libraries, runs offline, and never executes content from the
input repository. The reference repository is used only to define the safe
fixture convention; its checkout is not copied into this repository.

## Corrective remediation gate

Before continuing persistence, CPM, dashboard, CARIO, or browser work, the
source/extraction slice must be compatible with the current IDEAEngineering
planning contract inspected at commit
`afa9638f629999de6faa881ca25226cda44820a5`. The committed offline fixture is
public-safe but must mirror that structure: DOC-07 control and schedule tables,
Vietnamese-shaped Appendix A, section-context phases, Kanban task tables,
separate CARIO matrices, unsplit and split card IDs, and multiple predecessors.

Authority is field-level: DOC-07 owns control/baseline/phase/gate/policy facts;
Appendix A owns work packages and their effort/dependencies; Kanban owns card
decomposition, card schedule/detail/dependencies and CARIO assignments; Gantt
is a subordinate cross-check. Baseline validity, work-package validity,
delivery-card completeness, rendition validity, dependency-analysis validity,
and output readiness are reported separately. A malformed subordinate
rendition does not erase a valid DOC-07 + Appendix baseline.

The remediation also requires nullable unknown dates, non-fabricated unknown
execution states, capture metadata and safe source identity preservation, and
exclusion of runtime source content from persisted canonical JSON. The
execution overlay remains additive and baseline-immutable. Card-to-known-gate
edges remain analysis-eligible; work-package predecessor evidence remains a
separate traceability graph. The source's one-sided AM/PM notation is
normalized with start-of-day and end-of-day boundary defaults, while malformed
markers remain unknown.

## Scope and invariants

The implementation must satisfy these non-negotiable rules:

1. Local repository capture is the required MVP path. HTTPS Git capture is an
   optional capability and its absence does not fail offline acceptance.
2. Only the five allow-listed planning paths are read. Paths are normalized,
   contained under the source root, rejected when they traverse `..` or a
   reparse point, and bounded to 2 MiB per recognized file and 8 MiB total by
   default. No script, build, hook, binary, macro, command, or repository
   automation is executed.
3. Work-package effort is the authoritative capacity roll-up and must sum to
   exactly 512 hours in the fixture. Card effort is retained for detail and
   reconciliation, never added a second time.
4. `plannedEffortHours`, `plannedDurationWorkingMinutes`, authored baseline
   dates, calculated CPM dates, actuals, forecast, and variance are separate
   values. CPM consumes duration; capacity/load consumes effort. Duration is
   never derived from effort unless an explicit source rule says it is safe.
5. Authored baseline dates are immutable. Calculated earliest/latest dates,
   float, forecast, and variance are stored only under `analysis`.
6. Structural canonical violations fail reopen: malformed required fields,
   duplicate IDs, impossible parent/phase membership, nonexistent structural
   assignments, or unmarked missing references. A missing source dependency
   target may remain only as `INVALID_SOURCE_EVIDENCE` with
   `analysisEligible: false` and a diagnostic.
7. Initial reserve is 88 hours. Consumption and remaining reserve are null with
   `NOT_RUN`/`UNKNOWN` state because source planning does not prove execution
   consumption.
8. The dashboard says `Delivery cards completed 0/53` and never presents this
   as universal project completion.
9. The workbook is a narrow Open XML ZIP package with six named sheets. Tests
   inspect package parts, relationships, XML, Unicode, dates, and numbers
   without Excel.
10. Every meaningful behavior is implemented red-green-refactor. Unexpected
    failures use the systematic-debugging loop before any fix is attempted.

## Approved MVP1 scope amendment: execution overlay and progress Gantt

The original source-to-canonical architecture remains intact. Future tasks
must now add a manual execution overlay rather than putting mutable actuals on
the extracted baseline.

The amended flow is:

```text
source baseline -> canonical project
                       + execution overlay
                       -> management analysis
                       -> PLAN / ACTUAL / ALERT Gantt
                       -> Kanban / dashboard / CARIO plan export
```

The overlay is keyed by executable delivery-card ID and contains execution
state, actual start/finish, actual effort, remaining effort, last-updated time,
and optional note/evidence. Planning-only input starts with unknown actuals and
remaining values. A manual update validates date order, non-negative finite
effort, and state/date consistency; it never changes planned dates, planned
effort, source references, or authority.

The management seam accepts an explicit `asOfDate` and derives working-calendar
start/finish variance, late-start, active overdue, completed-on-time,
completed-late, suspended, cancelled, and conservative dependency `AT_RISK`
alerts. `OVERDUE` and `AT_RISK` are derived conditions, not authored states.
Dependency risk names the late predecessor and never fabricates a deterministic
successor delay when evidence is insufficient.

The Gantt projection uses one canonical card ID for three lanes: PLAN reads only
baseline dates, ACTUAL reads execution evidence (actual start through actual
finish or explicit as-of date while active), and ALERT reads derived analysis.
CARIO remains a six-sheet, plan-focused human-fill workbook; its planned start
and deadline columns continue to come from the baseline and are never replaced
with actual dates.

Canonical JSON remains schema 1.0 with an additive `executionOverlay` field.
Readers treat a missing field in an older 1.0 snapshot as an empty overlay;
new writers emit the field and validate present records. Reopen preserves the
baseline and overlay, while alerts and variance are recalculated.

Implementation order for the amendment is deliberately vertical:

1. Add domain overlay/alert records and failing seam tests.
2. Add manual update validation and focused red-green tests.
3. Add JSON serialization/reopen and overlay round-trip tests.
4. Add duration-based CPM/status/variance/alert analysis, including dependency
   risk and working-calendar semantics.
5. Add shared WBS/Gantt/Kanban/dashboard projections with the three lanes.
6. Preserve the CARIO mapping/writer contract and add plan-date regression tests.
7. Compose the application/UI flow and run end-to-end tests.

This amendment supersedes the future-task wording below wherever the original
plan describes actuals as deferred or a Gantt as baseline-only. Completed
source capture, authority, extraction, and canonical normalization work is not
reopened.

## Implementation worktree and commands

After this plan is committed on `main`, create the implementation worktree with
the `using-git-worktrees` skill:

```powershell
git worktree add "..\Project Management Compiler-001-mvp1" -b 001-mvp1-project-management-compiler main
```

All implementation commands run from that worktree. The branch is never force
pushed and `main` is not rewritten.

The only required commands are:

```powershell
dotnet restore .\ProjectManagementCompiler.sln
dotnet run --project .\tests\ProjectManagementCompiler.Tests\ProjectManagementCompiler.Tests.csproj --no-restore
dotnet build .\ProjectManagementCompiler.sln --no-restore
.\scripts\verify.ps1
```

No package, SDK, runtime, GitHub CLI, database, Docker image, browser driver,
or external service may be installed or downloaded.

## Target repository structure

```text
ProjectManagementCompiler.sln
CONTEXT.md
.specify/memory/constitution.md
docs/adr/
docs/superpowers/specs/
docs/superpowers/plans/
specs/001-mvp1-project-management-compiler/
src/ProjectManagementCompiler/
  Application/
  Domain/
  Extraction/
  Management/
  Outputs/
  Sources/
  Program.cs
  wwwroot/
tests/ProjectManagementCompiler.Tests/
tests/fixtures/ideaengineering/
scripts/
```

Generated JSON, XLSX, temporary captures, build output, and test output live
under ignored `artifacts/`, `bin/`, `obj/`, or system temporary directories and
are not committed.

## Test harness conventions

The test project is a console executable and has no test-framework package.
`Program.cs` runs named test delegates, prints one result per test, and exits
with code 0 only when all tests pass. `TestAssert` provides:

```csharp
Equal<T>(T expected, T actual, string message)
True(bool condition, string message)
False(bool condition, string message)
Contains(string expectedSubstring, string actual, string message)
Throws<TException>(Action action, string message)
```

Tests use an explicit `TestProjectFactory` to build canonical fixture data and
an explicit `TestFileSystem` seam for reparse-point and size-limit cases. They
do not depend on a live Git checkout, network, database, Office, or the
IDEAEngineering repository.

## Execution plan

### 1. Scaffold the build and test seams

**Files:**

- `ProjectManagementCompiler.sln`
- `src/ProjectManagementCompiler/ProjectManagementCompiler.csproj`
- `src/ProjectManagementCompiler/Program.cs`
- `tests/ProjectManagementCompiler.Tests/ProjectManagementCompiler.Tests.csproj`
- `tests/ProjectManagementCompiler.Tests/Program.cs`
- `tests/ProjectManagementCompiler.Tests/TestAssert.cs`
- `scripts/build.ps1`
- `scripts/test.ps1`
- `scripts/verify.ps1`

**Steps:**

1. Add a `net10.0` ASP.NET Core project with nullable reference types and
   implicit usings enabled. Keep all dependencies platform-only.
2. Add a `net10.0` console test project referencing the application project.
3. Add the test runner with one passing `RunnerStarts` test and the assertion
   helpers.
4. Add scripts that invoke only restore/build/test and later verification
   commands; scripts must stop on the first non-zero exit code.
5. Run `dotnet restore .\ProjectManagementCompiler.sln`; it must not add a
   package lock or external package reference.
6. Run the test project; the expected result is one passing test.
7. Commit as `build: scaffold MVP1 application and test harness`.

### 2. Define the canonical domain types

**Files:**

- `src/ProjectManagementCompiler/Domain/States.cs`
- `src/ProjectManagementCompiler/Domain/CanonicalProject.cs`
- `src/ProjectManagementCompiler/Domain/SourceReference.cs`
- `src/ProjectManagementCompiler/Domain/Diagnostics.cs`
- `src/ProjectManagementCompiler/Domain/PlanningEntities.cs`
- `src/ProjectManagementCompiler/Domain/AnalysisEntities.cs`
- `tests/ProjectManagementCompiler.Tests/DomainModelTests.cs`

**Steps:**

1. Add tests that construct a project with one phase, work package, card,
   milestone, dependency, assignment, reserve, and provenance reference. Assert
   the model preserves `plannedEffortHours`,
   `plannedDurationWorkingMinutes`, `plannedStart`, and `plannedFinish` as
   independent values.
2. Run the test project; it must fail to compile because the domain types do
   not exist.
3. Implement the enums `DataState`, `ValidationState`, `ExecutionState`,
   `CaptureState`, `DependencyType`, `SourceDocumentFormat`, and
   `WarningSeverity`.
4. Implement immutable records/classes for `CanonicalProject`, `Project`,
   `ProjectSource`, `ProjectBaseline`, `Phase`, `WorkPackage`,
   `DeliveryCard`, `MilestoneDecision`, `Dependency`, `Estimate`,
   `ResponsibilityRole`, `Assignment`, `CapacityPlan`, `CalendarDefinition`,
   `Reserve`, `PolicySet`, `SourceReference`, and `ImportWarning`.
5. Use `DateOnly` for authored dates, `DateTimeOffset?` for
   `capturedAtUtc`, `decimal?` for hours, and `int?` for working minutes.
   `ProjectSource.CapturedAtUtc` is persisted metadata, not business content.
6. Add `ManagementAnalysis`, `CpmNodeMetric`, `ScheduleVariance`,
   `CapacityAnalysis`, `ReserveAnalysis`, `HealthIndicator`, and
   `ViewSummary` under `Domain/AnalysisEntities.cs`.
7. Run the tests; the expected result is green.
8. Commit as `feat: add canonical project domain model`.

### 3. Add the controlled public-safe fixture

**Files:**

- `tests/fixtures/ideaengineering/README.md`
- `tests/fixtures/ideaengineering/docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md`
- `tests/fixtures/ideaengineering/docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md`
- `tests/fixtures/ideaengineering/docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html`
- `tests/fixtures/ideaengineering/docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md`
- `tests/ProjectManagementCompiler.Tests/FixtureShapeTests.cs`

**Steps:**

1. Add synthetic, public-safe planning text using the recognized filenames and
   simple Markdown/HTML tables. Use generic work names such as
   `Planning package P01`; do not copy private business text or the reference
   repository checkout.
2. Encode the six phases `PH0` through `PH5`, 35 work packages, 53 delivery
   cards, and seven gate/milestone records `G-D0` and `G-MS0` through `G-MS5`.
3. Make Appendix A work-package effort the authoritative total. Use these
   groups and totals so the fixture has a mechanically obvious 512-hour sum:
   `P01-P07 = 112`, `F01-F05 = 75`, `C01-C05 = 70`, `W01-W07 = 105`,
   `L01-L05 = 80`, and `Q01-Q06 = 70`.
4. Include authored start/finish dates and complete or one-sided half-day
   markers for enough cards to test the Monday-Friday, eight-hour calendar.
   Include at least one
   source dependency whose predecessor is absent and mark it only during
   extraction as invalid source evidence.
5. Preserve explicit source states when present; leave planning-only states
   unknown when the source does not author them. Encode WIP policy `1`, initial
   reserve `88`, capacity `600`, and logical CARIO/project role codes without
   concrete people or departments.
6. Add a test that reads only fixture metadata and asserts the five recognized
   paths exist, no private reference checkout is present, and the fixture
   headers describe the expected counts.
7. Run the test; it must fail until the fixture files are present, then pass.
8. Commit as `test: add controlled planning fixture`.

### 4. Implement source contracts and safe local capture

**Files:**

- `src/ProjectManagementCompiler/Sources/IProjectSourceAdapter.cs`
- `src/ProjectManagementCompiler/Sources/SourceRequest.cs`
- `src/ProjectManagementCompiler/Sources/RepositorySnapshot.cs`
- `src/ProjectManagementCompiler/Sources/SourcePathPolicy.cs`
- `src/ProjectManagementCompiler/Sources/LocalRepositorySourceAdapter.cs`
- `src/ProjectManagementCompiler/Sources/IRepositoryFileSystem.cs`
- `tests/ProjectManagementCompiler.Tests/SourceCaptureTests.cs`
- `tests/ProjectManagementCompiler.Tests/FakeRepositoryFileSystem.cs`

**Steps:**

1. Add failing tests for the mandatory interface contract, allow-list selection,
   relative path normalization, deterministic document order, `capturedAtUtc`
   capture metadata, missing-file diagnostics, and unreadable-root diagnostics.
2. Run the tests; they must fail because the adapter and safety policy are not
   implemented.
3. Implement `IProjectSourceAdapter.CaptureAsync(SourceRequest,
   CancellationToken)`, `SourceRequest` defaults, and `RepositorySnapshot`.
4. Define the exact five allow-listed paths in `SourcePathPolicy`. Resolve each
   with `Path.GetFullPath`, require a non-rooted relative candidate, reject a
   relative path beginning with `..`, and verify the final path remains below
   the normalized root.
5. Check every directory and file segment through `IRepositoryFileSystem` for
   reparse points. Reject symlink, junction, and other reparse-point escape
   with `SOURCE_PATH_ESCAPE`.
6. Check `FileInfo.Length` before reading. Reject a file above 2 MiB or a total
   selected payload above 8 MiB with `SOURCE_FILE_TOO_LARGE` or
   `SOURCE_TOTAL_TOO_LARGE`; never partially read an oversized file.
7. Read only recognized Markdown/HTML/text content as UTF-8. Do not enumerate
   arbitrary files, invoke a process, load an assembly, or inspect executable
   content. Preserve repository-relative paths and source metadata.
8. Return documents in the fixed allow-list order and diagnostics in stable
   code/path order. Capture timestamp is set once per capture.
9. Add fake-file-system tests that represent a reparse point without requiring
   Windows symlink privileges. Add a fixture containing a non-allow-listed
   command-looking file and assert it is never read or executed.
10. Run the source tests; the expected result is green.
11. Commit as `feat: add safe allow-listed local source capture`.

### 5. Make HTTPS capture explicitly optional

**Files:**

- `src/ProjectManagementCompiler/Sources/ExistingGitHttpsSourceAdapter.cs`
- `tests/ProjectManagementCompiler.Tests/HttpsCapabilityTests.cs`

**Steps:**

1. Add a test for an HTTPS request when the optional capability is disabled.
   It must return `CAPABILITY_GIT_HTTPS_UNAVAILABLE`, no source documents, and
   must not affect local fixture compilation.
2. Run the test; it must fail because the capability adapter is absent.
3. Implement the capability-gated adapter. The default MVP registration returns
   the explicit diagnostic. A future enabled implementation may call only the
   existing Git executable with application-owned arguments in a temporary
   directory, then pass the checkout through the same allow-listed capture
   policy; it may not run repository commands or require GitHub APIs.
4. Run the offline test suite; it must pass without network access.
5. Commit as `feat: define optional HTTPS source capability`.

### 6. Parse planning documents and resolve authority

**Files:**

- `src/ProjectManagementCompiler/Extraction/PlanningDocument.cs`
- `src/ProjectManagementCompiler/Extraction/MarkdownTableParser.cs`
- `src/ProjectManagementCompiler/Extraction/HtmlTableParser.cs`
- `src/ProjectManagementCompiler/Extraction/AuthorityResolution.cs`
- `src/ProjectManagementCompiler/Extraction/IdeaPlanningDiscovery.cs`
- `tests/ProjectManagementCompiler.Tests/ExtractionParserTests.cs`
- `tests/ProjectManagementCompiler.Tests/AuthorityResolutionTests.cs`

**Steps:**

1. Add parser tests for Markdown pipe tables, HTML tables, UTF-8 Vietnamese
   text, stable row order, missing required headings, and malformed numeric/date
   cells. Add authority tests for DOC-07 precedence and the stale subordinate
   Appendix reference.
2. Run the tests; they must fail because no parser or authority resolver exists.
3. Implement parsers that return rows with source line/table metadata. They
   parse only the recognized fixture documents and do not infer arbitrary
   repository meaning.
4. Implement discovery from `RepositorySnapshot.Documents`. Require DOC-07 and
   Appendix A for a canonical baseline; use Gantt and Kanban as subordinate
   renditions and preserve absent/malformed documents as diagnostics.
5. Implement authority rank `DOC-07 > Appendix A > Gantt > Kanban > README`.
   Record the selected baseline and `STALE_SUBORDINATE_REFERENCE` when the
   Appendix names an older control-envelope version.
6. Run the parser and authority tests; the expected result is green.
7. Commit as `feat: parse planning documents and resolve source authority`.

### 7. Extract and normalize the canonical baseline

**Files:**

- `src/ProjectManagementCompiler/Extraction/ExtractedPlan.cs`
- `src/ProjectManagementCompiler/Extraction/IdeaEngineeringExtractor.cs`
- `src/ProjectManagementCompiler/Extraction/WorkingCalendarNormalizer.cs`
- `src/ProjectManagementCompiler/Extraction/CanonicalProjectNormalizer.cs`
- `tests/ProjectManagementCompiler.Tests/ExtractionFixtureTests.cs`
- `tests/ProjectManagementCompiler.Tests/NormalizationTests.cs`

**Steps:**

1. Add failing extraction tests asserting the fixture produces project identity,
   baseline ID/version, PH0-PH5, 35 work packages, 53 cards, seven milestones,
   WIP=1, six CARIO codes, logical role assignments, and provenance.
2. Add a failing normalization test asserting `plannedEffortHours` is preserved
   independently from duration and that the authoritative work-package sum is
   exactly 512.
3. Run the tests; they must fail because extraction and normalization are not
   implemented.
4. Implement `IdeaEngineeringExtractor` against the typed table rows. Parse
   dates and decimal hours using invariant culture, retain source references for
   every important value, and emit explicit warnings for missing values.
5. Implement the working calendar as Monday-Friday, eight hours/day. Convert
   authored start/finish plus complete or one-sided AM/PM markers to working
   minutes; missing start/finish markers use the start/end of the working day.
   Set duration state to `KNOWN` only when the authored schedule is safe; leave
   it unknown rather than using effort as a substitute, and diagnose effort /
   duration mismatches without rewriting either value.
6. Normalize parent IDs and phase IDs. Preserve Appendix work-package effort as
   the capacity roll-up. Preserve card effort and add an
   `EFFORT_RECONCILIATION` warning when card detail does not equal its parent.
7. Normalize dependencies. A missing source predecessor becomes
   `INVALID_SOURCE_EVIDENCE`, `analysisEligible=false`, and a diagnostic; a
   missing subject or structural parent remains a hard validation error.
8. Populate initial reserve as 88 known hours, consumed/remaining null with
   explicit `NOT_RUN`/`UNKNOWN`, and never use source planning state as actual
   progress.
9. Run extraction and normalization tests; the expected result is green.
10. Commit as `feat: normalize IDEA planning baseline into canonical model`.

### 8. Implement deterministic canonical JSON persistence, execution overlay, and semantic digest

**Files:**

- `src/ProjectManagementCompiler/Outputs/CanonicalJsonSerializer.cs`
- `src/ProjectManagementCompiler/Outputs/CanonicalJsonDigest.cs`
- `tests/ProjectManagementCompiler.Tests/CanonicalJsonTests.cs`

**Steps:**

1. Add tests for schema version `1.0`, explicit null/unknown fields, stable
   ordering, UTF-8 output, round-trip values, and semantic digest equality for
   two otherwise identical projects with different `capturedAtUtc` values.
2. Run the tests; they must fail because the serializer and digest do not exist.
3. Implement JSON options with camel-case names, no null omission, invariant
   date/number formatting, and deterministic list ordering established by the
   normalizer.
4. Add `executionOverlay.records` with explicit execution states, actual start/
   finish, actual/remaining effort states, update metadata, and optional
   evidence. Emit it in new 1.0 snapshots without placing fields on the
   immutable baseline entities.
5. Implement semantic digest by serializing source metadata without
   `capturedAtUtc` and without replaceable derived analysis, while retaining
   execution overlay evidence in semantic content. Hash UTF-8 bytes with
   SHA-256.
6. Implement read/write methods that never silently upgrade schema versions;
   missing `executionOverlay` in older 1.0 snapshots becomes an empty overlay.
7. Run the JSON and overlay round-trip tests; the expected result is green.
8. Commit as `feat: add deterministic canonical JSON persistence`.

### 9. Add structural validation and reopen, including execution evidence

**Files:**

- `src/ProjectManagementCompiler/Domain/CanonicalValidator.cs`
- `src/ProjectManagementCompiler/Outputs/CanonicalReopener.cs`
- `tests/ProjectManagementCompiler.Tests/ReopenValidationTests.cs`

**Steps:**

1. Add failing tests for duplicate IDs, malformed required values, impossible
   phase ownership, nonexistent work-package/card relationships, invalid
   assignment targets, unresolved dependency subjects, and unmarked missing
   predecessors. Add a passing case for explicitly invalid source evidence.
2. Run the tests; they must fail because structural validation is absent.
3. Implement collection and global ID uniqueness checks, required source and
   baseline checks, parent/phase/card/assignment checks, date and effort type
   checks, dependency rules, and execution-overlay target/state/date/effort
   validation.
4. Implement the narrow dependency-evidence exception exactly as specified:
   unresolved predecessor plus `INVALID_SOURCE_EVIDENCE`, `analysisEligible=false`,
   and diagnostic is retained; every other unresolved structural reference is a
   hard error.
5. Implement reopen from canonical JSON without source capture or extraction.
   Recompute replaceable analysis only after structural validation succeeds and
   never mutate the baseline.
6. Run the reopen tests; the expected result is green.
7. Commit as `feat: enforce canonical structural reopen invariants`.

### 10. Validate dependencies and calculate CPM from duration

**Files:**

- `src/ProjectManagementCompiler/Management/DependencyValidator.cs`
- `src/ProjectManagementCompiler/Management/CpmCalculator.cs`
- `src/ProjectManagementCompiler/Management/WorkingCalendar.cs`
- `tests/ProjectManagementCompiler.Tests/DependencyTests.cs`
- `tests/ProjectManagementCompiler.Tests/CpmTests.cs`

**Steps:**

1. Add dependency tests for valid Finish-to-Start edges, missing target
   diagnostics, unsupported edge type, self-edge, and cycle detection.
2. Add a CPM test with two paths where effort is large on a short-duration
   node and effort is small on a long-duration node. Assert the critical path
   follows duration, not effort.
3. Add a test that calculated dates and float do not mutate authored baseline
   dates and that an invalid source-evidence edge is excluded.
4. Run the tests; they must fail because validation and CPM are absent.
5. Implement topological validation over card and milestone nodes. Preserve
   all source edges in the model but pass only valid, supported, analysis-
   eligible edges to CPM.
6. Implement forward pass `ES=max(predecessor EF)`, `EF=ES+duration`, reverse
   pass from the calculated finish, `LS=LF-duration`, and `float=LS-ES` in
   working minutes. Mark zero-float nodes as the dependency CPM critical path.
7. Map working minutes to calculated working dates through the source calendar.
   Keep calculated earliest/latest/forecast fields under `analysis` and compare
   calculated finish to immutable authored baseline finish.
8. If a cycle, unsupported edge, or unknown duration prevents safe analysis,
   return CPM `UNKNOWN` and a diagnostic rather than deriving duration from
   effort.
9. Run dependency and CPM tests; the expected result is green.
10. Commit as `feat: calculate duration-based dependency CPM`.

### 11. Add capacity, reserve, execution variance, alerts, forecast, and dashboard analysis

**Files:**

- `src/ProjectManagementCompiler/Management/CapacityAnalyzer.cs`
- `src/ProjectManagementCompiler/Management/StatusAnalyzer.cs`
- `src/ProjectManagementCompiler/Management/ManagementAnalysisOrchestrator.cs`
- `tests/ProjectManagementCompiler.Tests/AnalysisTests.cs`

**Steps:**

1. Add tests asserting authoritative planned effort is 512, capacity is 600,
   initial reserve is 88, and consumed/remaining reserve remain null with
   unknown/not-run states.
2. Add tests that the dashboard label is exactly `Delivery cards completed
   0/53`, manual actuals are unknown without evidence, explicit source states
   are preserved, unrecognized source states remain unknown, and fixed as-of
   dates derive late-start/overdue without mutating authored execution state.
3. Add a test that the analysis labels dependency CPM separately from the
   single-coder/resource baseline constraint.
4. Add failing tests for actual start/finish variance, completed-on-time/late,
   suspended/cancelled, working-calendar semantics, and conservative
   predecessor-based `AT_RISK` alerts. Invalid source dependencies must never
   fabricate downstream risk.
5. Run the tests; they must fail because the analysis orchestrator and
   execution update seam are absent.
6. Implement the manual execution-update module with a small public seam. It
   validates dates and numeric evidence and returns a new overlay without
   mutating the canonical baseline.
7. Implement effort-based capacity/load and the explicit reserve state rules.
   Do not use card effort in the authoritative project total.
8. Implement working-calendar variance, status counts, derived alerts,
   unknown actual/remaining/forecast states, documented health indicators,
   dependency risk, and the delivery-card count.
9. Implement `ManagementAnalysisOrchestrator` to compose dependency, CPM,
   schedule variance, capacity, reserve, status, alerts, and health results
   without changing source entities.
10. Run analysis tests; the expected result is green.
11. Commit as `feat: add management analysis and truthful dashboard metrics`.

### 12. Build shared WBS/Gantt/Kanban/CPM view projections with three lanes

**Files:**

- `src/ProjectManagementCompiler/Management/ViewModels.cs`
- `src/ProjectManagementCompiler/Management/ViewProjectionService.cs`
- `tests/ProjectManagementCompiler.Tests/ViewProjectionTests.cs`

**Steps:**

1. Add tests that all views use canonical IDs and one analysis result. Assert
   WBS hierarchy, baseline bars, calculated CPM fields, card states, warnings,
   and separate resource constraint labels.
2. Run the tests; they must fail because view projections are absent.
3. Implement projections for WBS, three-lane Gantt, Kanban, Dependency Network,
   CPM Critical Path, dashboard, and source/warning review. Use generic
   `Source` labels in user-facing view models.
4. Ensure PLAN bars read only authored start/finish, ACTUAL bars read only the
   overlay, ALERT lanes read only analysis, and no view creates a second task
   collection.
5. Run the view tests; the expected result is green.
6. Commit as `feat: project canonical analysis into management views`.

### 13. Implement CARIO mapping and warning rows

**Files:**

- `src/ProjectManagementCompiler/Outputs/CarioMapping.cs`
- `src/ProjectManagementCompiler/Outputs/CarioRowProjection.cs`
- `tests/ProjectManagementCompiler.Tests/CarioMappingTests.cs`

**Steps:**

1. Add tests for all six CARIO codes, logical project roles, blank concrete
   identity mappings, and warning generation for unresolved organization,
   person, department, or priority fields.
2. Run the tests; they must fail because mappings are absent.
3. Implement configuration records for logical-to-concrete mappings. Keep
   logical roles and CARIO semantics in the domain; never invent identities.
4. Implement six-sheet row projections with source references and warning IDs.
5. Run mapping tests; the expected result is green.
6. Commit as `feat: add configuration-driven CARIO row mappings`.

### 14. Write and verify the six-sheet plan-focused XLSX package

**Files:**

- `src/ProjectManagementCompiler/Outputs/CarioWorkbookWriter.cs`
- `tests/ProjectManagementCompiler.Tests/XlsxPackageTests.cs`
- `tests/ProjectManagementCompiler.Tests/XlsxPackageVerifier.cs`

**Steps:**

1. Add a failing package test that requires these exact sheets and package
   parts:

   ```text
   01_TASKS
   02_ASSIGNMENTS
   03_CHILDREN_MILESTONES
   04_DEPENDENCIES
   05_PROJECT_INFO
   06_IMPORT_WARNINGS
   [Content_Types].xml
   _rels/.rels
   xl/workbook.xml
   xl/_rels/workbook.xml.rels
   xl/worksheets/sheet1.xml through sheet6.xml
   ```

2. Add assertions for relationship targets, sheet names, UTF-8 Vietnamese
   headers/cells, ISO baseline dates, invariant numeric cells, null/unknown
   state text, warning rows, and proof that actual dates never replace planned
   start/deadline fields.
3. Run the tests; they must fail because the writer is absent.
4. Implement `CarioWorkbookWriter` with `System.IO.Compression.ZipArchive` and
   `System.Xml.XmlWriter`. Use inline strings, date cells with a declared date
   type, invariant numeric cells, escaped XML, and explicit relationship parts.
5. Do not use Excel COM, a third-party workbook library, shared global state, or
   a native CARIO import claim.
6. Implement the verifier used by tests to open the ZIP and parse every required
   XML part. It must resolve all workbook relationship IDs to existing parts.
7. Run Xlsx package tests; the expected result is green.
8. Commit as `feat: export verified six-sheet CARIO workbook`.

### 15. Add the application compiler service, execution updates, and canonical export flow

**Files:**

- `src/ProjectManagementCompiler/Application/IProjectCompiler.cs`
- `src/ProjectManagementCompiler/Application/CompilationRequest.cs`
- `src/ProjectManagementCompiler/Application/CompilationResult.cs`
- `src/ProjectManagementCompiler/Application/ProjectCompiler.cs`
- `tests/ProjectManagementCompiler.Tests/CompilerFlowTests.cs`

**Steps:**

1. Add an end-to-end test that calls the application service on the controlled
   fixture and asserts capture → discovery → extraction → validation → manual
   execution update → analysis → views → JSON/XLSX export.
2. Assert the result contains six phases, 35 work packages, 53 cards, seven
   milestones, 512 authoritative effort hours, source warnings, and a valid
   workbook package.
3. Run the test; it must fail because the application service is absent.
4. Implement `IProjectCompiler.CompileAsync(CompilationRequest,
   CancellationToken)` by composing capture, authority, extraction,
   normalization, validation, analysis, view projection, JSON serialization,
   and workbook output through explicit interfaces.
5. Block normal exports when structural errors prevent a canonical baseline;
   permit warnings and marked invalid source dependency evidence.
6. Implement `ReopenAsync` using canonical JSON only and verify it preserves
   both baseline and execution overlay, recalculates alerts from an explicit
   as-of date, and produces the same semantic digest/view summary without
   source capture.
7. Run compiler flow tests; the expected result is green.
8. Commit as `feat: compose the end-to-end compiler workflow`.

### 16. Add the loopback browser application with manual execution and three-lane Gantt

**Files:**

- `src/ProjectManagementCompiler/Web/WebApplicationSetup.cs`
- `src/ProjectManagementCompiler/Web/ApiEndpoints.cs`
- `src/ProjectManagementCompiler/wwwroot/index.html`
- `src/ProjectManagementCompiler/wwwroot/app.js`
- `src/ProjectManagementCompiler/wwwroot/styles.css`
- `src/ProjectManagementCompiler/Program.cs`
- `tests/ProjectManagementCompiler.Tests/WebEndpointContractTests.cs`

**Steps:**

1. Add endpoint contract tests against the application service seam for compile,
   reopen, project summary, each required view, JSON export, XLSX export, and
   warning review. The test must assert the output uses generic `Source` terms.
2. Run the tests; they must fail because routes and static UI do not exist.
3. Configure the ASP.NET Core host to bind to `http://127.0.0.1:5050` by
   default. Do not bind `0.0.0.0` or expose the LAN automatically.
4. Add `POST /api/compile`, `POST /api/reopen`, `POST /api/execution`, `GET /api/project`,
   `GET /api/views/{view}`, `GET /api/export/json`, and
   `GET /api/export/xlsx` routes. Keep all route data behind the application
   service and shared view models.
5. Add a plain HTML/CSS/JavaScript interface with source path input, analyze,
   extraction review, warning list, manual execution form, WBS, three-lane
   Gantt, Kanban, CPM Critical Path, dashboard, and export controls. Display
   `Delivery cards completed 0/53`.
6. Run endpoint contract tests and the application; the expected result is
   green and the browser can be opened at the loopback URL.
7. Commit as `feat: add loopback browser review application`.

### 17. Add deterministic regression and safety verification scripts

**Files:**

- `tests/ProjectManagementCompiler.Tests/RegressionTests.cs`
- `tests/ProjectManagementCompiler.Tests/VerificationTests.cs`
- `scripts/verify.ps1`
- `docs/README.md`
- `CONTEXT.md`

**Steps:**

1. Add a regression test that compiles the fixture twice with different capture
   timestamps and asserts equal semantic digest, stable counts, stable view
   summaries, and no generated fixture files in the source tree.
2. Add verification tests for traversal, reparse-point fake entries, bounded
   files, no command execution, duplicate IDs, invalid source dependency
   retention, 512-hour accounting, reserve unknown states, and XLSX package
   integrity.
3. Run the tests; any failure is an unexpected failure and must be handled via
   the systematic-debugging skill: reproduce the smallest failing test, inspect
   the first incorrect boundary, state a root-cause hypothesis, make one
   minimal fix, and rerun the focused test before the full suite.
4. Implement `scripts/verify.ps1` to run the test harness, solution build,
   deterministic regression, and package verification. It must report blocked
   checks as failures or explicit unavailable capability, never as passes.
5. Document local commands, loopback URL, fixture safety boundary, output file
   names, and optional HTTPS capability in `docs/README.md` and `CONTEXT.md`.
6. Run the full verification script; the expected result is green.
7. Commit as `test: add deterministic MVP1 verification workflow`.

### 18. Review and final verification

**Files inspected:** all changed files plus:

- `docs/superpowers/specs/2026-09-17-project-management-compiler-design.md`
- `specs/001-mvp1-project-management-compiler/spec.md`
- `specs/001-mvp1-project-management-compiler/data-model.md`
- `specs/001-mvp1-project-management-compiler/contracts/canonical-json.md`
- `specs/001-mvp1-project-management-compiler/contracts/cario-workbook.md`
- `specs/001-mvp1-project-management-compiler/contracts/source-adapter.md`
- `specs/001-mvp1-project-management-compiler/tasks.md`

**Steps:**

1. Use the requesting-code-review skill for a standards review and a
   specification-alignment review. Resolve every actionable finding with a
   focused test and commit.
2. Run `git diff --check`, `git status --short --branch`, and a tracked-file
   scan for credentials, tokens, private IDEA material, generated output, and
   package-lock/artifact files.
3. Run `dotnet restore`, the dependency-free test harness, `dotnet build`, and
   `scripts/verify.ps1` from a clean working tree state as far as the platform
   permits. Record exact output and distinguish optional HTTPS capability from
   required offline checks.
4. Use the verification-before-completion skill. Do not claim completion from
   an earlier run; inspect the final command output and final diff immediately
   before the claim.
5. Confirm `main` has not been rewritten, the implementation branch contains
   meaningful commits, and no push occurs until the user’s requested pre-push
   status/security inspection is complete.

## Commit sequence

The implementation branch should contain focused commits in this order:

1. `build: scaffold MVP1 application and test harness`
2. `feat: add canonical project domain model`
3. `test: add controlled planning fixture`
4. `feat: add safe allow-listed local source capture`
5. `feat: define optional HTTPS source capability`
6. `feat: parse planning documents and resolve source authority`
7. `feat: normalize IDEA planning baseline into canonical model`
8. `feat: add deterministic canonical JSON persistence`
9. `feat: enforce canonical structural reopen invariants`
10. `feat: calculate duration-based dependency CPM`
11. `feat: add management analysis and truthful dashboard metrics`
12. `feat: project canonical analysis into management views`
13. `feat: add configuration-driven CARIO row mappings`
14. `feat: export verified six-sheet CARIO workbook`
15. `feat: compose the end-to-end compiler workflow`
16. `feat: add loopback browser review application`
17. `test: add deterministic MVP1 verification workflow`
18. Any review remediation as a narrowly named focused commit.

## Definition of done

- The implementation branch builds on installed .NET 10 without package
  installation or network access.
- The offline fixture compiles to the expected hierarchy and counts with 512
  authoritative work-package effort hours and no double counting.
- Effort, duration, baseline, execution overlay, actual, forecast, variance,
  reserve, and resource-constraint semantics are visible and tested separately.
- Source capture is allow-listed, path-safe, size-bounded, reparse-safe, and
  non-executing.
- Structural canonical corruption fails reopen while marked invalid source
  dependency evidence remains visible and excluded from CPM.
- Duration-based CPM, baseline preservation, working-calendar variance,
  manual execution updates, PLAN/ACTUAL/ALERT Gantt lanes, dependency-risk
  alerts, capacity/load, reserve unknown states, dashboard labeling,
  WBS/Gantt/Kanban/CPM/dashboard views, canonical JSON reopen, and verified
  six-sheet XLSX output are covered by offline tests.
- The final review reports current branch, commit SHA, pushed/unpushed state,
  uncommitted files, verification commands, and any remaining blocker before a
  push is considered.
