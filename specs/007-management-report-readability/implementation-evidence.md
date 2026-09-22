# Implementation Evidence: Management Report Readability

**Feature**: 007 — Management Report Readability

**Branch**: `codex/feature007-management-report-readability`

**Implementation authorized**: 2026-09-22

**Starting code commit**: `e7fa44baf21298a2bddad6ec8c130c46900303e9`

**Approved design commit**: `cf4d153`

## Execution rulings

- Continue in the existing dedicated feature branch rather than move the
  approved uncommitted design into another checkout. Existing user-owned
  untracked files are out of scope, and every feature commit must use explicit
  paths.
- The user's explicit instruction to implement is the approval required to
  proceed past the reviewer-owned custom checklist. The built-in requirements
  checklist is 16/16 complete; the 40 custom review prompts remain unchanged
  and will be evaluated again through implementation tests, visual acceptance,
  Spec Kit convergence, and final review.
- Spec Kit tasks use `Tnnn` checklist rows rather than the heading format
  expected by the generic task-brief helper. Exact task text and phase context
  are therefore read directly from `tasks.md`.

## T001 — Baseline

### Repository state

```text
## codex/feature007-management-report-readability
?? outputs/
?? package-lock.json
?? package.json
```

The three untracked entries above are user-owned and excluded from Feature 007
commits.

### Build

Command:

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Result: PASS — exit 0, 0 warnings, 0 errors.

### Tests

The first run without `IDEAENGINEERING_ROOT` reached the suite but reported 13
environment-only failures because the accepted local source checkout was not
discoverable from this repository layout. No product assertion failed.

The approved local checkout was then supplied to the test process through the
existing `IDEAENGINEERING_ROOT` environment variable; the workstation path is
intentionally not recorded in this public repository.

Command:

```powershell
$env:IDEAENGINEERING_ROOT = '<approved local IDEAEngineering checkout>'
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

Result: PASS — exit 0, 295 passed, 0 failed.

## Task ledger

- T001 complete — clean build and 295/295 baseline tests after supplying the
  approved local source checkout.
- T002 complete — reusable Feature 007 fixture now covers nested/repeated title
  noise, all five Actual evidence shapes, the 1/6/35/53 WBS oracle, operating
  populations, and official metadata while the existing suite remains 295/295.
- T003 RED — 295 passed, 3 failed because
  `ReaderFacingTextPolicy` did not yet exist.
- T004 GREEN — 298 passed, 0 failed after implementing deterministic cleanup
  and approved missing-data labels.
- T005/T006 RED — 298 passed, 1 failed because both executive projectors still
  exposed legacy kind/Markdown text instead of the shared reader-facing name.
- T005/T006 GREEN — 299 passed, 0 failed after migrating both projectors and
  preserving existing schedule, ownership, and execution facts.
- T007 RED — 299 passed, 1 failed because the neutral document had no typed
  column-group contract.
- T008 GREEN — 300 passed, 0 failed after adding validated row outlines,
  column groups, summary direction, and auto-filter metadata.
- T009 RED — 300 passed, 1 failed because worksheet XML omitted
  `outlinePr`.
- T010 GREEN — 301 passed, 0 failed after deterministic outline/group/filter
  serialization; package inspection found no macro, connection, or
  external-link parts.
## T011-T047 - Implementation evidence

The approved six-sheet reader-facing report was implemented and integrated at
the official executive-progress export boundary. The implementation includes:

- shared Reader-Facing Name and missing-data policies;
- concise overview projection with current phase, evidence-backed progress,
  variance, next milestone, and at most five priority actions;
- explicit daily Plan/Actual Gantt shapes: `RecordedInterval`,
  `OpenRecordedInterval`, `CompletionPoint`, `EffortOnly`, and `None`;
- canonical Project -> Phase -> Work Package -> Delivery Card WBS projection,
  with milestones and decision gates excluded from the WBS hierarchy;
- a deduplicated 30-day operating agenda with decision/blocker precedence;
- reader-first detail rows and centralized official metadata;
- deterministic workbook composition with exactly these sheets, in this order:
  `Tổng quan`, `Điều hành 30 ngày`, `Gantt`, `WBS`, `Chi tiết công việc`,
  `Thông tin báo cáo`;
- the existing technical CARIO + Gantt export kept separate and unchanged;
- explicit report limitations: official evidence only, no inferred actual
  dates, and no report-to-project import contract.

The implementation was kept in the existing Feature 007 branch and was
covered by the existing regression suite plus 12 new projection/workbook
assertion paths. No user-owned untracked files were staged.

## T048 - Runbook

Updated `docs/runbook/mvp1-local.md` with the six-sheet reader journey,
official-only selection boundary, report non-importability, and the future
Project Workbook boundary. The technical CARIO + Gantt export remains
documented as a separate output.

## T049 - Final automated verification

Commands and results:

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

PASS - exit 0, 0 warnings, 0 errors.

```powershell
$env:IDEAENGINEERING_ROOT = '<approved local IDEAEngineering checkout>'
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

PASS - exit 0, 307 passed, 0 failed.

```powershell
$env:IDEAENGINEERING_ROOT = '<approved local IDEAEngineering checkout>'
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
```

PASS - exit 0. The final verification reported `Health=ok`, project
`IE-PROD-ROADMAP-001`, `Cards=53`, `ManifestClassification=OFFICIAL_COMMIT`,
`ProposalLifecycle=DRAFT`, `ReopenOverdue=0`, `ReopenAtRisk=0`,
`ExecutiveSheetCount=6`, and `SecurityChecks=PASS`.

```powershell
git diff --check
```

PASS - no whitespace errors. Git only reported normal LF/CRLF conversion
notices for modified text files.

## T050 - Exact-commit import/export evidence

The real source flow used the approved IDEAEngineering commit
`0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4` through the existing official
manifest import boundary. The accepted fixture reconciled to `6` phases,
`35` work packages, `53` delivery cards, and `7` control points.

Two cold/warm measurements and one warmed measurement were recorded for the
same application session:

- first run: `6,865 ms`;
- second run: `5,290 ms`;
- warmed steady-state run: `4,671 ms`.

The steady-state run met the under-five-second target. The produced executive
workbook was `110,413` bytes and contained the six reader-facing sheets. The
artifact was written below the system temporary directory as
`pmc-feature007-final-run3-94a84f...xlsx`; it is not a repository artifact.

Before/after checks confirmed that the source repository HEAD and working tree
were unchanged, and the official imported snapshot/digest was unchanged by
export. Export remained a read-only presentation operation.

## T051 - Visual workbook inspection

The final workbook was rendered read-only with the bundled spreadsheet
artifact tooling and inspected at normal-zoom-equivalent views:

- `Tổng quan` is compact and does not duplicate the full daily timeline;
- `Điều hành 30 ngày` presents one readable row per deduplicated concern;
- `Gantt` keeps the full daily axis in its own sheet with separate Plan and
  Actual lanes and truthful evidence shapes;
- `WBS` opens at the Project/Phase/Work Package levels, hides Delivery Card
  rows initially, and keeps optional detail groups collapsed;
- `Chi tiết công việc` puts reader-facing fields before technical provenance;
- `Thông tin báo cáo` presents authority, source, baseline, dates, contract,
  and limitations as a concise label/value page;
- the first two print contracts were checked for Landscape and fit-to-width;
- no clipping or ambiguous generated label was found in the inspected views.

The artifact renderer could not auto-render the full WBS height because of its
bitmap-size limit. The WBS was therefore inspected in visible top and bottom
ranges; this was a renderer limitation, not a workbook or product failure.

## T052 - Public-repository inventory and security

The intended Feature 007 diff contains source, tests, runbook, and Spec Kit
evidence only. No generated workbook, `.xlsm`, `.xls`, `.csv`, credentials,
tokens, secrets, absolute private configuration, or proprietary IDEA source
was added to the intended commit. Existing user-owned untracked files remain
outside the commit, specifically `outputs/`, `package.json`, and
`package-lock.json`.

## T053 - Post-implementation Spec Kit and review result

Post-implementation analysis inspected `28` functional requirements,
`11` success criteria, `5` user stories, and `53` tasks. Result: `100%`
requirement/task coverage, with `0` duplication, ambiguity, constitution, or
coverage findings. No `.specify/extensions.yml` was present.

Convergence found the implementation consistent with `spec.md`, `plan.md`,
`data-model.md`, `contracts/`, `quickstart.md`, and `tasks.md`; no actionable
convergence task was appended.

A manual standards-plus-spec review found no critical or high finding. One
non-blocking low observation remains: the workbook composer is still the
single composition boundary and retains a few unused legacy private helpers
(`BuildNearTerm`, `BuildAttention`, `BuildProvenance`, and
`AddOverviewSummaryMerges`). They are not called or serialized and do not
affect the six-sheet contract; cleanup can be handled separately.

No user-owned untracked files were staged.
