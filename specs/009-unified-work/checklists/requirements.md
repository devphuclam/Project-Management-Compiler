# Requirements Quality Checklist: Unified Work

**Purpose**: Verify that Feature 009 requirements are complete, unambiguous, traceable, and bounded before specification approval.
**Created**: 2026-09-29
**Feature**: [spec.md](../spec.md)
**Review Ownership**: Reviewer-owned requirements-quality review artifact. `[x]` means reviewed and satisfied for requirements quality; it does not mean implementation is complete.

## Completeness and clarity

- [x] CHK001 User scenarios describe independently testable user outcomes and have priorities.
- [x] CHK002 Functional requirements use observable MUST/MUST NOT behavior rather than implementation instructions.
- [x] CHK003 Edge cases cover unknown/ambiguous phase, no results, unrecorded state, filtered-out selection, and unsupported evidence.
- [x] CHK004 Success criteria are measurable without prescribing implementation technology.
- [x] CHK005 The specification defines Phase → Work Package → Delivery Card membership and excludes milestone/decision-point work cards.

## Semantic authority and truthfulness

- [x] CHK006 All phases, current-phase focus, and explicit phase scoping are distinct and deterministic.
- [x] CHK007 List and Kanban share stable canonical Delivery Card identity and shared filters.
- [x] CHK008 Authored state, unrecorded state, derived attention, planned baseline, actual evidence, and known zero remain distinct.
- [x] CHK009 Needs Attention lists an exact source-supported alert whitelist and explicitly excludes unsupported alert/readiness/governance/diagnostic categories.
- [x] CHK010 Date wording preserves baseline planned-finish semantics and does not invent a due-date contract.
- [x] CHK011 Canonical/source authority, immutable baseline, execution overlay, provenance, and Gantt analysis are not altered by UI behavior.

## Interaction and boundary traceability

- [x] CHK012 Kanban ordering and post-filter group-count behavior are deterministic and testable.
- [x] CHK013 List search/filter ancestor visibility and no-orphan behavior are defined.
- [x] CHK014 Selection persistence, keyboard-focus return, per-view scroll restoration, and Work ↔ Gantt identity navigation are specified separately.
- [x] CHK015 Responsive/accessibility criteria and legacy WBS/Kanban parity gates are testable.
- [x] CHK016 Every UW-01…UW-18 decision has a documented provenance class distinguishing direct human clarification answers from design-resolved topics accepted by human approval, and maps to exact Feature 009 requirements.
- [x] CHK017 Scope explicitly excludes Proposal Workflow redesign, implementation plan/tasks, source editing, Work Package progress, and Gantt semantic changes.

## Notes

- This checklist records specification-quality review only; it is not an implementation checklist or task list.
- The only eligible Work Needs Attention codes are `START_DELAY`, `OVERDUE`, `SUSPENDED`, and `AT_RISK`, when attached to a canonical Delivery Card.
- The provenance matrix records UW-06, UW-08, UW-10, and UW-11, plus UW-18, as clarification topics resolved in the approved design; it does not misrepresent them as direct human answers.
- No code, implementation plan, task breakdown, or Proposal Workflow artifact is created by this feature-specification step.
