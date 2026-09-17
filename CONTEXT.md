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
A derived display condition: the current date is after the deadline and the execution state is neither completed nor cancelled.
_Avoid_: authored workflow state
