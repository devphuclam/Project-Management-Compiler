# MVP1 Spec Kit Task Baseline

These tasks are the dependency-ordered execution outline for the manually
maintained Spec Kit package. The detailed bite-sized plan is produced after the
written-spec review gate.

## Foundation

- [x] T001 Create the `net10.0` solution and platform-only project structure.
- [x] T002 Add the dependency-free executable test harness and test command.
- [x] T003 Add the constitution, context, ADR, and source fixture metadata to the repository checks.
- [x] T004 Define canonical IDs, data-state enums, source references, diagnostics, execution states, and separate effort/duration/baseline fields.

## Source and extraction

- [x] T005 Define `IProjectSourceAdapter`, `SourceRequest`, and `RepositorySnapshot`.
- [x] T006 Implement mandatory local repository capture with allow-listed paths, normalized containment checks, reparse-point rejection, file/total size limits, and no-execution guarantees.
- [x] T007 (Optional capability) Implement existing-Git HTTPS capture with temporary workspace cleanup; offline MVP acceptance must not depend on this task.
- [x] T008 Implement planning-document discovery and authority resolution.
- [x] T009 Implement deterministic IDEAEngineering Markdown/HTML extraction.
- [x] T010 Add fixture tests for authority precedence, stale subordinate references, and unsupported input.

## Canonical normalization and persistence

- [x] T011 Normalize phases, 35 work packages, 53 delivery cards, and 7 milestones/decisions.
- [x] T012 Preserve parent-child relationships, keep `plannedEffortHours` separate from authored working duration and baseline dates, and distinguish parent/card effort accounting.
- [x] T013 Normalize provenance, roles, assignments, policies, capacity, reserve, and warnings.
- [ ] T014 Implement deliberate schema-versioned canonical JSON export with an additive schema-1.0 `executionOverlay` containing manual execution records.
- [ ] T015 Implement canonical JSON structural validation and reopen; validate overlay targets, states, dates, and effort without mutating baseline, while retaining only explicitly invalid source dependency evidence.
- [ ] T016 Add deterministic regression comparison against the IDEAEngineering fixture, including execution-overlay round-trip and baseline semantic equivalence.

## Management analysis

- [ ] T017 Implement hierarchy/WBS projections from canonical IDs.
- [ ] T018 Implement dependency target validation and cycle detection.
- [ ] T019 Implement Finish-to-Start CPM and dependency critical-path metrics from normalized planned duration, never raw effort.
- [ ] T020 Implement baseline-vs-calculated variance without baseline mutation, including working-calendar start/finish variance and explicit as-of date.
- [ ] T021 Implement the manual execution-update seam and overlay validation: actual dates, actual/remaining effort states, state consistency, and no plan mutation.
- [ ] T022 Implement status/alert analysis: start delay, active overdue, completed on time/late, suspended, cancelled, conservative dependency `AT_RISK`, effort-based capacity/load, reserve semantics, health, and unknown-state rules.
- [ ] T022A Add tests that separate dependency critical path from single-coder baseline constraints and prove invalid dependencies cannot fabricate risk.

## CARIO output

- [ ] T023 Define mapping configuration for logical roles and concrete identities.
- [ ] T024 Implement unresolved mapping and organization warning generation.
- [ ] T025 Implement the narrow Open XML workbook writer with six required sheets and explicit package relationships; keep planned dates separate from overlay actuals.
- [ ] T026 Add ZIP-part, relationship/XML, Unicode, dates, numeric cells, six-sheet, warnings, hierarchy, dependency, no-fabrication, and plan-date-preservation tests without Excel.

## Browser application

- [ ] T027 Implement the loopback-only ASP.NET Core host and compilation endpoints, including manual execution update and JSON snapshot routes.
- [ ] T028 Implement source review and warning presentation using generic “Source” language.
- [ ] T029 Implement WBS, three-lane PLAN/ACTUAL/ALERT Gantt, Kanban, critical-path, dashboard, and export views from shared view models.
- [ ] T030 Implement canonical JSON reopen in the UI and recalculate derived alerts from an explicit as-of date.
- [ ] T031 Add an end-to-end fixture smoke test for analyze → update execution → review lanes/alerts → views → JSON/XLSX export, including `Delivery cards completed 0/53` labeling.

## Verification and handoff

- [ ] T032 Run offline tests, build, deterministic rerun, execution-overlay round-trip, source safety fixture tests, Gantt lane tests, and workbook XML/package verification.
- [ ] T033 Review the diff against the approved design/specification for baseline mutation, actual/duration conflation, alert overclaiming, JSON compatibility, and CARIO planned-date regression; remediate findings.
- [ ] T034 Run final verification-before-completion checks and document exact application/export commands.
