# Project Management Compiler Context

This context defines the project-management language used by the compiler. It separates planning evidence from calculated analysis so later source adapters can contribute to one canonical project model without changing the meaning of existing baselines.

## Planning structure

**Project**:
The managed body of work with an identity, a planning baseline, source evidence, and management views.
_Avoid_: repository, board, plan file

**Phase**:
A time-bounded stage of a project that groups work packages and may end at a review or decision gate.
_Avoid_: milestone, status column

**Work Package**:
An authored decomposition unit within a phase. A work package owns a planned effort and completion condition; its delivery cards are the execution decomposition, not additional effort to add to the work-package total.
_Avoid_: task, card, phase

**Delivery Card**:
An executable unit prepared for a project board or CARIO entry. Delivery cards may be children of a work package and carry the detailed date, status, responsibility, and dependency data used by the execution views.
_Avoid_: work package, issue, ticket

**Milestone**:
A zero-duration review or outcome point that can gate later work. A decision gate is a milestone whose outcome records a decision rather than delivery effort.
_Avoid_: zero-hour task, phase

**Dependency**:
A directed relationship that constrains the order of two canonical work items. MVP1 supports Finish-to-Start relationships only.
_Avoid_: blocker, parent-child relationship

**Canonical Work-Item Identity**:
An ID is stable and unique within its canonical kind. A polymorphic dependency
or graph reference resolves as `(kind, id)`, so `WorkPackage:P04` and
`DeliveryCard:P04` are distinct while their source-visible IDs remain `P04`.
MVP1 responsibility assignments and execution-overlay records are
`DeliveryCard`-only references.
_Avoid_: globally unique raw ID, rewritten prefixed source ID

## Schedule and evidence

**Source Baseline**:
The authoritative planning values extracted from source documents, including authored dates, effort, dependencies, phase policy, and reserve. Calculated results never mutate it.
_Avoid_: forecast, calculated schedule

**Dependency Critical Path**:
The path calculated from dependency edges and supported durations. It does not automatically include every item merely because the project has one coder.
_Avoid_: single-coder path, baseline schedule

**Resource / Baseline Schedule Constraint**:
A source-authored rule such as “one coder” or sequential phases that constrains the baseline schedule independently of the dependency-network CPM result.
_Avoid_: critical path

**Reserve**:
Controlled schedule or effort capacity held for recorded variance, defects, retest, or approved dependency impact. Reserve is not an unassigned work package or delivery card.
_Avoid_: buffer task, spare task

**Provenance**:
The evidence trail for an extracted value: repository, ref or commit, source file, section/table/item, extraction rule, authority level, and validation state.
_Avoid_: source note, comment

**Import Warning**:
A structured diagnostic that explains missing, ambiguous, conflicting, unsupported, or unresolved source information without silently guessing a business value.
_Avoid_: log message, validation noise

**Source Manifest**:
The sole discovery entry point for a manifest import. It declares the source
contract version, snapshot policy, exact source roles, expected totals, and
paths relative to one repository root. A source manifest does not become a
planning authority; the roles it declares assign field-level authority.
_Avoid_: optional index, repository scan hint, file-order configuration

**Import Context**:
The explicit snapshot selector supplied with an import: either one exact Git
commit or a working-tree preview. It is not inferred from file timestamps or
the newest visible branch.
_Avoid_: current folder state, latest file, implicit HEAD

**Official Snapshot**:
A validated manifest import whose declared inputs all come from one committed
source snapshot and whose Source Readiness Gate permits official handoff. A
failed candidate never replaces the last valid official snapshot.
_Avoid_: latest import attempt, working-tree preview

**Uncommitted Preview**:
A validated view of working-tree source that remains visibly non-authoritative
and cannot replace the official snapshot.
_Avoid_: draft baseline, current official project

**Actual Work**:
Recorded execution evidence for effort spent or work accepted. It is unknown when the source contains planning only.
_Avoid_: elapsed calendar time, commit count

**Forecast**:
A calculated future schedule or remaining effort based on actual and remaining-estimate evidence. MVP1 reports it as unknown when those inputs are unavailable.
_Avoid_: baseline date, predicted date from guesswork

## CARIO and execution

**Project Logical Role**:
A source-defined responsibility such as `LEAD`, `PDA`, or `QLHT`. It is not a concrete employee or account.
_Avoid_: person, department

**CARIO Identity Mapping**:
Configuration that maps a project logical role to a concrete company/CARIO person, team, or organization when that mapping is known. Missing mappings remain unresolved.
_Avoid_: hardcoded employee, inferred department

**Execution State**:
An authored state such as `NOT_STARTED`, `IN_PROGRESS`, `COMPLETED`, `SUSPENDED`, or `CANCELLED`.
_Avoid_: overdue state

**Overdue**:
A derived display condition for an active `IN_PROGRESS` item whose as-of date is
after the planned deadline. A `NOT_STARTED` item past its planned start (or
finish) is reported as `START_DELAY`, not double-counted as active overdue;
completed items use `COMPLETED_LATE` or `COMPLETED_ON_TIME`, while cancelled
items have no active overdue condition.
_Avoid_: authored workflow state

## Execution overlay amendment

**Source Execution Snapshot**:
Recorded execution facts imported from the manifest's `EXECUTION_AUTHORITY`.
It preserves `NOT_RECORDED` separately from every execution state and never
replaces or edits the source baseline.
_Avoid_: inferred progress, manual overlay, second plan

**Execution Proposal Overlay**:
Mutable local proposals maintained by the Compiler for executable delivery
cards. A proposal is keyed by canonical identity and base Snapshot ID. It can
support an explicitly labelled scenario preview, but it is not official actual
evidence until reviewed, merged into IDEAEngineering, and reimported.
_Avoid_: execution authority, automatic write-back, current actuals

**Recording State**:
Whether attributable execution evidence exists for a delivery card. The
controlled values are `RECORDED` and `NOT_RECORDED`; the latter never implies
`NOT_STARTED`.
_Avoid_: execution state, missing parser value

**Result State**:
The recorded outcome of work or verification, independent from Execution
State. Controlled values are `NOT_RUN`, `PASS`, `FAIL`, `BLOCKED`, and
`NOT_APPLICABLE`.
_Avoid_: execution state, readiness summary

**Variance**:
Calculated information comparing actual or derived schedule information with the immutable baseline. Variance does not change execution state.
_Avoid_: revised baseline, forecast state

**Alert**:
A structured derived management condition such as `START_DELAY`, `OVERDUE`, `COMPLETED_LATE`, `AT_RISK`, `SUSPENDED`, or `CANCELLED`. It is not an execution state, task, dependency, or source authority.
_Avoid_: persisted status, work item

**As-of Date**:
The explicit date supplied to analysis for late-start, active-overdue, and in-progress actual-lane calculations. Analysis never silently substitutes wall-clock time.
_Avoid_: implicit current date

Source execution, proposal preview, actual, and forecast remain separate from
plan. Planned and actual effort remain separate from planned and actual working
duration. CARIO output stays plan-focused: planned start and deadline are never
replaced with actual dates.

**Field-level Authority**:
Authority is assigned per semantic field, not by a blind document rank. The
manifest discovers; DOC-07 owns roadmap and baseline fields; Appendix A owns
work packages; the Kanban/CARIO register owns delivery cards and responsibility
matrices; the Execution Register owns attributable actual execution facts;
Gantt cross-checks; readiness supplies readiness evidence; navigation files own
no planning or execution field.

**Unknown Authored Date**:
A missing or unsafe source date is null with an explicit state/diagnostic. It is
never represented by `DateOnly.MinValue` or `0001-01-01`.

**Unknown Authored State**:
An absent or unrecognized source execution state is not `NOT_STARTED`. Explicit
`NOT_STARTED`, `NOT-RUN`, missing, and extraction failure remain distinguishable.

**Work-Package Dependency Graph**:
Appendix-owned predecessor evidence retained for traceability. It is separate
from the delivery-card/milestone execution graph and is not double-fed into CPM.

**Rendition Validity**:
The validity of a subordinate Gantt or Kanban rendition is reported separately
from DOC-07 baseline validity and Appendix work-package validity.

**Authored Schedule Boundary**:
In the IDEAEngineering Kanban notation, a missing start-side AM/PM marker means
the start of that working day and a missing finish-side marker means the end of
that working day. An unrecognized marker leaves duration unknown.

**Effort/Duration Mismatch**:
An authored effort value and normalized working duration are independent facts.
When they differ, the compiler retains both and emits a warning; it does not
rewrite either value.

**Gate Dependency**:
A dependency from a known milestone/gate to a delivery card or later gate is a
valid execution-graph edge. Appendix work-package predecessor evidence remains
separate traceability data and is not double-fed into CPM.
