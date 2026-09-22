# Research: Management Report Readability

## Decision: Replace the reader workbook in place

**Decision**: Keep the existing `Xuất báo cáo tiến độ` action and official-only
export seam, and replace its five-sheet reader contract with the approved
six-sheet Management Report contract.

**Rationale**: The user needs one authoritative management file. Creating a
second reader export would preserve the confusing output instead of fixing it
and would force managers to choose between competing reports.

**Alternatives considered**:

- Add a second "clean report" action: rejected because it creates two files for
  the same management purpose.
- Repurpose `Xuất dữ liệu CARIO + Gantt`: rejected because that workbook has a
  separate technical and preview/import contract.
- Turn the report into the future Project Workbook: rejected because a
  presentation-first report is not a safe round-trip authority.

## Decision: Use the approved six-sheet reader journey

**Decision**: Produce exactly `Tổng quan`, `Điều hành 30 ngày`, `Gantt`, `WBS`,
`Chi tiết công việc`, and `Thông tin báo cáo`, in that order.

**Rationale**: The order moves from decision to operation to schedule to scope
to traceability. It consolidates the old `30 ngày tới` and `Vấn đề cần xử lý`
journeys without losing their action populations, adds the missing WBS, and
moves technical metadata away from the first five sheets.

**Alternatives considered**:

- Keep five sheets and put WBS inside Gantt: rejected because schedule and scope
  decomposition answer different questions and need different disclosure.
- Add a full Kanban sheet: deferred to the future Project Workbook because the
  30-day operating sheet already covers the current management questions.
- Keep a separate issues sheet: rejected because the approved operating view
  intentionally unifies decisions, blockers, overdue work, and active work.

## Decision: Introduce one deterministic reader-language policy

**Decision**: Centralize source-title cleanup and fixed Vietnamese presentation
labels behind a small `ReaderFacingTextPolicy` used by executive projections.
The policy removes only recognized identity/formatting noise and never
generates or paraphrases business meaning.

**Rationale**: The current executive projectors contain different private name
cleaners and use several obsolete missing-data phrases. One policy prevents
drift across summary, Gantt, WBS, operating, and detail views and makes the
anti-AI-slop rules directly testable.

**Alternatives considered**:

- Clean text independently in each sheet composer: rejected because it creates
  inconsistent names and moves domain decisions into spreadsheet layout code.
- Use AI/LLM rewriting: rejected by the deterministic-extraction constitution
  and because generated wording could change source meaning.
- Remove every leading bracketed token: rejected because meaningful bracketed
  text must survive when it is not a recognized identity prefix.

## Decision: Model Actual presentation shape explicitly

**Decision**: Add an explicit presentation classification for each Gantt row:
recorded interval, open recorded interval, completion point, effort-only
evidence, or no execution evidence. Keep the underlying dates and effort as
separate facts.

**Rationale**: The existing date-pair model cannot distinguish finish-only
completion from missing evidence during rendering, which caused completed work
to appear visually empty. An explicit shape lets the workbook display `✓` and
`● Có ghi nhận` without fabricating a date range.

**Alternatives considered**:

- Infer a one-day interval from Actual finish: rejected because it invents an
  Actual start and duration.
- Place effort-only evidence on the reporting date: rejected because that date
  is not an execution date.
- Infer dates from state, update time, plan, or effort: rejected because those
  facts answer different questions.

## Decision: Add a report-specific WBS projection

**Decision**: Add `ExecutiveWbsProjector` and report-specific WBS row types over
the canonical Project → Phase → Work Package → Delivery Card hierarchy.
Milestones remain outside this projection.

**Rationale**: The existing generic `WbsProjector` includes milestones, sorts
technical nodes by ID, and carries source-oriented fields. Changing that
contract would risk existing application views. A focused report projection can
preserve canonical source order, add reader names and execution summaries, and
enforce the approved four-level membership without weakening the generic WBS.

**Alternatives considered**:

- Reuse the generic WBS directly: rejected because its membership and ordering
  differ from the approved management WBS.
- Build WBS rows in the workbook composer: rejected because parentage,
  numbering, Actual, dependency, and attention rules are domain projection
  concerns, not spreadsheet layout concerns.
- Derive WBS from dates: rejected because schedule does not determine scope
  hierarchy.

## Decision: Project one unified 30-day operating population

**Decision**: Add a deterministic operating-item projection that combines
attention items with qualifying near-term schedule rows. It deduplicates by
stable target identity and applies category precedence: decisions/blockers,
overdue unfinished, active, then planned/milestone.

**Rationale**: Simply concatenating the old issue and near-term sheets would
repeat the same work and preserve two incompatible row shapes. One projection
can state action, consequence, owner, required date, state, and schedule context
once per operating concern.

**Alternatives considered**:

- Keep two separate populations in one worksheet: rejected because duplicate
  rows would still require reconciliation.
- Drop issues outside the 30-day schedule window: rejected because an immediate
  decision may concern later work and remains operationally relevant.
- Create a Kanban workflow model now: rejected as out of scope for this report.

## Decision: Extend the neutral workbook document for progressive disclosure

**Decision**: Extend `ExecutiveWorkbookDocument` with row outline metadata,
column outline groups, initial hidden/collapsed state, and an optional auto-
filter range. Serialize these capabilities with the existing BCL Open XML
writer.

**Rationale**: Excel row and column outlines are the native deterministic way
to open WBS at Work Package depth while keeping all Delivery Cards and optional
details available. The neutral document keeps Open XML syntax out of the WBS
projector and sheet composers.

**Alternatives considered**:

- Omit detailed rows or optional columns: rejected because the approved report
  requires complete hierarchy and on-demand detail.
- Hide rows/columns without outlines: rejected because readers could not
  discover and expand them reliably.
- Add an external spreadsheet package: rejected by the restricted-environment
  constitution and unnecessary for the required Open XML features.

## Decision: Keep report composition separate from package serialization

**Decision**: Keep `ExecutiveProgressWorkbookComposer` as the six-sheet
orchestrator, move substantial sheet-specific logic into focused internal
composers, and keep `ExecutiveProgressXlsxExporter` responsible only for
deterministic package/XML serialization.

**Rationale**: The current composer is already large. Adding WBS, operating,
metadata, cleanup, and outline logic directly would make sheet contracts harder
to review and test. Focused composers preserve one workbook contract while
keeping deep interfaces around management projection, worksheet composition,
and serialization.

**Alternatives considered**:

- Add all new behavior to the existing composer: rejected because unrelated
  sheet rules would be coupled in one file.
- Build a generic spreadsheet framework: rejected because the feature needs a
  small report-specific abstraction, not a reusable office suite.

## Decision: Preserve authority, endpoint, and non-importability

**Decision**: Keep `IProjectCompiler.ExportExecutiveProgressXlsx`,
`GET /api/exports/executive-progress.xlsx`, dated file naming, and
`CurrentOfficialResult` selection unchanged. Keep the management workbook free
of technical preview markers so existing import paths reject it.

**Rationale**: The requested change is the reader contract, not a new
application workflow. Preserving these seams minimizes risk and protects the
official-only boundary.

**Alternatives considered**:

- Add new endpoint or UI controls: rejected as unnecessary scope.
- Let the exporter fall back to a working-tree or XLSX preview: rejected because
  preview facts cannot become official Actual.
- Add report import support: rejected by ADR 0007.

## Resolution status

All product and technical decisions required for planning are resolved. No
`NEEDS CLARIFICATION` remains.

