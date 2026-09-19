# Feature Specification: IDEAEngineering Manifest Import

**Feature Branch**: `codex/ideaengineering-manifest-import`

**Created**: 2026-09-19

**Status**: Approved for implementation

**Input**: Import the official IDEAEngineering project-management source
package through its manifest at accepted commit
`0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4`, preserving source authority,
execution truth, atomic snapshots, diagnostics, and proposal-only local edits.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Import an authoritative project snapshot (Priority: P1)

As a project lead, I can provide an IDEAEngineering repository, its declared
manifest, and an exact source commit so that the Compiler shows one validated,
reproducible project snapshot without scanning for alternative source files.

**Why this priority**: Every management view depends on importing the correct
source and assigning each field to its declared owner.

**Independent Test**: Import the accepted source commit and verify the snapshot
identity, source metadata, authority mapping, and controlled totals without
using any local proposal or prior Compiler state.

**Acceptance Scenarios**:

1. **Given** the accepted repository commit and manifest contract `0.1.0`,
   **When** an official import is requested, **Then** the Compiler returns a
   validated official snapshot bound to that exact commit.
2. **Given** the accepted source, **When** the snapshot is inspected, **Then**
   it contains 6 phases, 35 Work Packages, 53 executable Delivery Cards, 7
   gates or milestones, 512 planned work hours, 88 reserve hours, and 600 total
   baseline-capacity hours.
3. **Given** an unsupported contract, an escaping path, ambiguous authority,
   or a mixed snapshot, **When** import is attempted, **Then** it fails closed
   with stable diagnostics and does not replace the last valid snapshot.

---

### User Story 2 - See execution truth without inference (Priority: P1)

As a project lead, I can distinguish recorded execution facts from absent
evidence, planning baseline, readiness evidence, calculated forecast, and local
proposals so that the dashboard never claims progress that IDEAEngineering did
not record.

**Why this priority**: Treating missing history as `NOT_STARTED`, or readiness
as actual progress, produces false management information.

**Independent Test**: Import the accepted source and inspect P01 plus all other
Delivery Cards without creating a proposal.

**Acceptance Scenarios**:

1. **Given** the accepted Execution Register, **When** P01 is inspected,
   **Then** it is `RECORDED / IN_PROGRESS / NOT_APPLICABLE` while actual start,
   actual effort, and remaining effort remain unknown.
2. **Given** the other 52 Delivery Cards, **When** execution is inspected,
   **Then** every Card is `NOT_RECORDED` and none is presented as confirmed
   `NOT_STARTED`.
3. **Given** insufficient remaining-effort evidence, **When** official forecast
   is requested, **Then** calculated forecast remains unknown while the
   immutable baseline remains visible.

---

### User Story 3 - Inspect candidates and previews safely (Priority: P2)

As a project lead, I can inspect a dirty working tree or an unready committed
candidate without losing the current official snapshot or mistaking candidate
data for accepted source.

**Why this priority**: Source preparation and diagnosis must be possible while
atomic official state remains trustworthy.

**Independent Test**: Load one valid official snapshot, then attempt an invalid
candidate and a valid working-tree preview and compare all three application
states.

**Acceptance Scenarios**:

1. **Given** a current official snapshot, **When** a candidate import fails,
   **Then** the official snapshot remains active and the failed attempt is shown
   separately with diagnostics.
2. **Given** a valid dirty working tree, **When** preview import succeeds,
   **Then** it is labelled `UNCOMMITTED_PREVIEW` and cannot replace the official
   snapshot.
3. **Given** a committed source whose readiness gate is not passed, **When** it
   is inspected, **Then** it appears only as `CANDIDATE_PREVIEW` with the
   readiness reason visible.

---

### User Story 4 - Prepare local execution proposals without write-back (Priority: P3)

As a project lead, I can draft a possible execution update and optionally view
its scenario impact without changing official actuals or writing directly to
IDEAEngineering.

**Why this priority**: Local planning support is useful only when its lack of
source authority is impossible to overlook.

**Independent Test**: Create a proposal against an official snapshot, inspect
official and proposal-preview views, export the proposal, and reimport a newer
source snapshot.

**Acceptance Scenarios**:

1. **Given** an official snapshot, **When** a local execution edit is saved,
   **Then** it is stored as a proposal and official analysis is unchanged.
2. **Given** an explicit proposal-preview mode, **When** a scenario assumption
   is enabled, **Then** derived values are labelled estimated and the assumption
   is not exported as a source fact.
3. **Given** a proposal based on an older source snapshot, **When** a newer
   commit or register revision is imported, **Then** the proposal becomes
   `STALE_BASE` until explicitly reviewed.
4. **Given** a proposal for completed work, **When** required completion facts
   or evidence are absent, **Then** it remains a draft with diagnostics rather
   than becoming ready for review.

---

### User Story 5 - Reopen and export with authority intact (Priority: P3)

As a project lead, I can reopen prior Compiler data and export official,
preview, or proposal artifacts while preserving their authority labels and
source identity.

**Why this priority**: Saved artifacts must not lose the distinction between
official source facts and local proposals.

**Independent Test**: Reopen both a legacy snapshot and a current snapshot,
then compare their authority state, diagnostics, and exported metadata.

**Acceptance Scenarios**:

1. **Given** a legacy canonical snapshot containing a manual execution
   overlay, **When** it is reopened, **Then** those records become proposals and
   a migration diagnostic is visible.
2. **Given** an official or preview snapshot, **When** it is exported, **Then**
   its mode, source repository, commit, contract version, validation result,
   Snapshot ID, and import time remain visible.
3. **Given** an invalid candidate, **When** export is requested, **Then** no
   artifact can be presented as an official or valid preview snapshot.

### Edge Cases

- The requested Git commit does not exist or is not a commit object.
- The working tree changes while preview capture is in progress.
- The manifest path is absolute, traverses outside the repository, points to a
  link, or names a missing file.
- A manifest role is missing, duplicated, unknown, or claims a field owned by
  another role.
- The manifest, register, calendar, baseline reference, or fixture catalogue
  uses an unsupported contract version.
- The Execution Register violates its schema, duplicates typed identity,
  contains an unknown Delivery Card, has an unexpected revision, records
  negative effort, or completes work without evidence.
- A dependency targets an unknown identity or forms a cycle.
- The Baseline and forecast calendars intentionally differ.
- Readiness evidence and the Execution Register mention the same Card with
  different state vocabulary.
- A preview or proposal export could otherwise be confused with an official
  artifact.
- A schema `1.0` snapshot contains execution records with no source snapshot
  identity.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The importer MUST accept repository root, repository-relative
  manifest path, and an explicit Git-commit or working-tree import context.
- **FR-002**: The manifest MUST be the sole discovery entry point; the importer
  MUST NOT scan the repository or fall back to a compiler-owned source list.
- **FR-003**: Contract version `0.1.0` MUST be explicitly allow-listed; every
  other version MUST fail closed with `PMC-CONTRACT-002`.
- **FR-004**: Every declared path MUST be repository-relative, remain inside
  the repository after normalization, identify a regular source object, and be
  rejected when absolute, escaping, undeclared, missing, or unsafe.
- **FR-005**: An official import MUST read every declared input from one exact
  commit. A preview MUST detect a working-tree change during capture and reject
  a mixed candidate.
- **FR-006**: A failed candidate MUST retain the last valid official snapshot
  and expose the attempt separately.
- **FR-007**: The importer MUST assign fields according to the seven manifest
  roles: roadmap, Work Package, Delivery Card, execution, rendition
  cross-check, readiness evidence, and navigation-only.
- **FR-008**: Reading order MUST NOT decide authority. A subordinate conflict
  MUST retain the declared owner and emit a diagnostic; missing, duplicate, or
  ambiguous ownership MUST fail import.
- **FR-009**: The importer MUST validate required JSON documents against the
  selected schema and reject unsupported schema dialects or semantics.
- **FR-010**: Diagnostics MUST use the stable source catalogue codes and carry
  severity, entity identity where applicable, source path, field, message, and
  recommended action.
- **FR-011**: Canonical and source identity MUST resolve by `kind + id`; raw IDs
  MUST NOT be treated as globally unique across kinds.
- **FR-012**: Import validation MUST cover source totals, dependency targets and
  cycles, calendar rules, register revision, execution/result consistency,
  completion evidence, and fixture-oracle outcomes.
- **FR-013**: A valid snapshot MUST record source repository, source commit or
  preview identity, contract version, Baseline ID, register revision,
  validation result, diagnostics, import time, and Snapshot ID.
- **FR-014**: Snapshot ID MUST be deterministically derived from project ID,
  source commit or preview identity, Baseline ID, and register revision.
- **FR-015**: Source execution MUST preserve `RECORDED` and `NOT_RECORDED`
  independently from Execution State.
- **FR-016**: Recorded Execution State MUST use only `NOT_STARTED`,
  `IN_PROGRESS`, `COMPLETED`, `SUSPENDED`, or `CANCELLED`.
- **FR-017**: Result State MUST remain separate and use only `NOT_RUN`, `PASS`,
  `FAIL`, `BLOCKED`, or `NOT_APPLICABLE`.
- **FR-018**: P01 from the accepted source MUST import as `RECORDED`,
  `IN_PROGRESS`, and `NOT_APPLICABLE` without invented actual or remaining
  effort.
- **FR-019**: The remaining 52 Delivery Cards from the accepted source MUST
  import as `NOT_RECORDED` without an inferred Execution State.
- **FR-020**: Baseline, Actual, Remaining, Calculated Forecast, Blocker, and
  Schedule Health MUST remain separately identifiable in the canonical model
  and management views.
- **FR-021**: Official forecast MUST remain unknown when source execution lacks
  sufficient remaining-effort or forecast evidence; it MUST NOT write a
  planning value into an execution field.
- **FR-022**: The Baseline calendar MUST remain Monday-Friday while the current
  forecast calendar includes the second and fourth Saturday; this accepted
  difference MUST emit `PMC-CALENDAR-001` and MUST NOT rebaseline the project.
- **FR-023**: Readiness evidence MUST remain separate from actual execution and
  MUST NOT overwrite Execution or Result State.
- **FR-024**: Official source execution and local execution proposals MUST be
  represented separately.
- **FR-025**: Official analysis and official exports MUST ignore proposals.
- **FR-026**: Every proposal MUST identify its base Snapshot ID and have a
  lifecycle state of `DRAFT`, `READY_FOR_REVIEW`, or `STALE_BASE`.
- **FR-027**: Importing a different source commit or register revision MUST mark
  retained proposals stale rather than silently rebasing them.
- **FR-028**: Proposal preview MUST be explicitly selected and visibly
  non-authoritative. Scenario assumptions MUST be labelled estimated and MUST
  NOT be exported as source facts.
- **FR-029**: A completion proposal MUST remain draft unless actual finish,
  actual effort, zero remaining effort, and controlled evidence are present.
- **FR-030**: Users MUST be able to export a deterministic proposal artifact
  containing project ID, base Snapshot ID, expected register revision, proposed
  field changes, evidence, and proposal diagnostics.
- **FR-031**: The Compiler MUST NOT modify IDEAEngineering or implement direct
  write-back in this feature.
- **FR-032**: The application MUST visibly distinguish `OFFICIAL_COMMIT`,
  `UNCOMMITTED_PREVIEW`, and `CANDIDATE_PREVIEW`.
- **FR-033**: Users MUST be able to inspect manifest identity, source repository,
  source commit, contract version, validation result, diagnostics, import time,
  and source readiness without reading raw source files.
- **FR-034**: Row-level execution inspection MUST show recording state,
  Execution State, Result State, actual/remaining values and provenance without
  substituting readiness or planning values.
- **FR-035**: Official analysis MUST default to the Execution Register status
  date. A user-selected analysis date MUST be labelled as analysis context, not
  source evidence.
- **FR-036**: Canonical persistence MUST use schema `2.0`; schema `1.0` snapshots
  MUST remain readable by migrating manual execution records into proposals,
  neutralizing the legacy `ExecutionOverlay`, and retaining an explicit
  migration diagnostic. Migrated overlay records MUST NOT remain effective
  execution evidence.
- **FR-037**: Official, preview, and proposal exports MUST include authority and
  snapshot metadata; preview filenames MUST visibly identify preview status.
- **FR-038**: A committed candidate without a passed Source Readiness Gate MUST
  remain a candidate preview and MUST NOT replace official state.
- **FR-039**: The import form and local launcher MUST support repository root,
  manifest path, source commit, and preview mode without hardcoding a personal
  filesystem path.
- **FR-040**: The accepted source commit and all catalogue fixtures MUST match
  their declared outcomes, including `PASS`, `PASS_WITH_WARNINGS`, and each
  required invalid case.
- **FR-041**: Source processing and persisted artifacts MUST exclude credentials,
  absolute local paths, raw source bodies, prohibited company configuration,
  sensitive employee data, and unauthorized proprietary material.
- **FR-042**: The legacy fixed-path flow MAY remain for compatibility but MUST
  never be an automatic fallback from a failed manifest import.

### MVP2.2 Correctness and Hardening Addendum

This corrective pass does not add a new product surface. It makes the accepted
authority, persistence, capture, validation, and presentation semantics
executable after an independent audit.

- **FR-043**: Schema `1.0` migration MUST preserve every legacy execution value
  in a local `ExecutionProposal`, then clear or neutralize the legacy
  `ExecutionOverlay`. The migrated overlay MUST NOT be consumed by
  `ExecutionTruthResolver`, official analysis, dashboard counts, alerts, actual
  Gantt lanes, or actual-effort calculations.
- **FR-044**: Reopening schema `2.0` MUST semantically validate `ImportMetadata`,
  `SourceExecution`, and `ExecutionProposals` after deserialization. Invalid
  typed identities, targets, state combinations, paths, lifecycle values,
  metadata, evidence, or proposal changes MUST fail closed and MUST NOT become
  authoritative through reopen.
- **FR-045**: A working-tree preview MUST read the manifest and its declared
  boundary independently for pre- and post-capture fingerprints. Mutation of
  the manifest, declarations, declared files, or repository state MUST reject
  the preview and retain the previous official snapshot.
- **FR-046**: Failure to verify Git `HEAD` or status MUST fail closed. A failed
  Git-state read MUST NOT be represented by a comparable sentinel value that
  can make two failed reads appear stable.
- **FR-047**: Exact Git capture MUST inspect the requested tree entry and mode,
  determine blob size before reading blob contents, reject symlink mode `120000`,
  and enforce hard application ceilings in addition to caller-provided limits.
- **FR-048**: Proposal completion readiness MUST require at least one valid
  controlled-evidence record with required identity, type, description, recording
  time, and recorder fields. Non-empty but malformed evidence MUST remain
  diagnosed and MUST NOT qualify `READY_FOR_REVIEW`.
- **FR-049**: `CompilerApplicationState` MUST be the single runtime owner of
  retained local proposals. `CanonicalProject.ExecutionProposals` is its
  persistence projection; create, update, schema `1.0` migration, save, reopen,
  list, and stale-base evaluation MUST use the same retained proposal set.
- **FR-050**: Manifest workflow UI wording MUST identify source execution as
  source-authoritative, local edits as proposals, and source-provided forecast
  values as source forecasts. It MUST NOT call source execution manual, call a
  proposal action official recording, or call source forecast calculated.

### Key Entities

- **Manifest Import Request**: Repository root, manifest path, import mode,
  requested commit when applicable, and analysis date override.
- **Source Manifest**: Contract identity, project identity, snapshot policy,
  source-role declarations, expected totals, source readiness, and declared
  paths.
- **Import Attempt**: Candidate identity, mode, time, validation result,
  diagnostics, and optional imported snapshot; distinct from current official
  state.
- **Import Snapshot**: Deterministic identity and metadata for one atomic source
  capture.
- **Source Execution Snapshot**: Attributable execution records imported from
  the execution authority, including recording, execution, result, effort,
  blocker, forecast override, evidence, and revision facts.
- **Execution Proposal**: Local proposed changes formerly represented by the
  legacy overlay, bound to a base Snapshot ID with lifecycle state and
  diagnostics but no source authority.
- **Project Calendar Set**: Separate approved Baseline and selected forecast
  calendars plus their difference treatment.
- **Source Diagnostic**: Stable coded validation finding with source and field
  context.
- **Controlled Proposal Evidence**: A proposal evidence record that satisfies the
  source-compatible controlled-evidence fields and safe-path/URI rules required
  before a completion proposal can become review-ready.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Importing accepted commit `0cf89de…` produces exactly 6 phases,
  35 Work Packages, 53 Delivery Cards, 7 gates/milestones, 512 planned hours,
  88 reserve hours, and 600 total capacity hours.
- **SC-002**: All seven fixture-catalogue cases produce exactly their declared
  result and diagnostic-code set.
- **SC-003**: P01 and all 52 other Delivery Cards match their declared recording,
  execution, result, actual, and remaining evidence with zero inferred values.
- **SC-004**: Every tested unsupported version, unsafe path, duplicate identity,
  negative effort, missing completion evidence, dependency cycle, or mixed
  snapshot fails without replacing the current official snapshot.
- **SC-005**: A user can identify official versus preview mode, repository,
  commit, contract, validation result, warning count, and import time from the
  primary application surface without opening a source file.
- **SC-006**: Official analysis remains byte-for-byte semantically unchanged
  after creating, editing, previewing, or exporting a local proposal.
- **SC-007**: A legacy schema `1.0` snapshot reopens with all former manual
  execution records preserved as proposals and none presented as source
  actuals.
- **SC-008**: A failed import, a working-tree preview, and an unready committed
  candidate all leave the last valid official Snapshot ID unchanged.
- **SC-009**: Verification detects zero committed credentials, absolute local
  source paths, raw imported document bodies, or prohibited private data.
- **SC-010**: Existing Compiler behavior outside manifest import continues to
  pass its established verification suite.
- **SC-011**: Reopening schema `1.0` `IN_PROGRESS` and `COMPLETED` overlays
  creates deterministic local proposals while official execution counts,
  actual effort, alerts, project health, and Gantt actual lanes remain unchanged.
- **SC-012**: Tampered schema `2.0` metadata, source execution, target identity,
  source evidence, or proposal data fails closed with diagnostics and cannot
  replace or promote official source state.
- **SC-013**: Deterministic working-tree tests reject manifest/declaration/file
  mutation and two unavailable Git-state reads; no unstable preview is accepted.
- **SC-014**: Exact Git capture rejects oversized blobs before body reads and
  rejects symlink tree entries while accepting a bounded regular blob.
- **SC-015**: Empty/default/malformed proposal evidence keeps a completion
  proposal `DRAFT`; valid controlled evidence permits `READY_FOR_REVIEW` without
  changing official source execution.
- **SC-016**: A created proposal survives canonical save, reopen, and list with
  the same semantic identity, while a newer official snapshot makes its base
  stale without mutating source execution.
- **SC-017**: The manifest UI uses `Source execution`, proposal wording, and
  `Source forecast`; no manifest source value is presented as manual or
  compiler-calculated execution.

## Assumptions

- Git is already installed and available; no package, SDK, service, or runtime
  installation is permitted.
- The accepted source commit exists in the selected local IDEAEngineering
  repository, and the application remains loopback-only.
- IDEAEngineering source contract `0.1.0` is the sole new source contract in
  this feature. Later versions require an explicit allow-list and contract
  tests.
- Source fixtures published in the accepted source package are intended as
  public-safe consumer contract examples; only minimum required content is
  copied.
- Last-valid official state, latest attempt, and active preview are retained in
  the running process only. Durable import history is outside this feature.
- Proposal export is a review handoff artifact, not a write-back mechanism or
  an accepted Execution Register revision.
- Future valid commits may be imported when they retain contract `0.1.0` and
  pass source readiness and validation; the accepted commit remains the
  mandatory compatibility baseline.
