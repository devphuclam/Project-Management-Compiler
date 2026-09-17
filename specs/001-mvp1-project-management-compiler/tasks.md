# MVP1 Spec Kit Task Baseline

These tasks are the dependency-ordered execution outline for the manually
maintained Spec Kit package. The detailed bite-sized plan is produced after the
written-spec review gate.

## Foundation

- [ ] T001 Create the `net10.0` solution and platform-only project structure.
- [ ] T002 Add the dependency-free executable test harness and test command.
- [ ] T003 Add the constitution, context, ADR, and source fixture metadata to the repository checks.
- [ ] T004 Define canonical IDs, data-state enums, source references, diagnostics, execution states, and separate effort/duration/baseline fields.

## Source and extraction

- [ ] T005 Define `IProjectSourceAdapter`, `SourceRequest`, and `RepositorySnapshot`.
- [ ] T006 Implement mandatory local repository capture with allow-listed paths, normalized containment checks, reparse-point rejection, file/total size limits, and no-execution guarantees.
- [ ] T007 (Optional capability) Implement existing-Git HTTPS capture with temporary workspace cleanup; offline MVP acceptance must not depend on this task.
- [ ] T008 Implement planning-document discovery and authority resolution.
- [ ] T009 Implement deterministic IDEAEngineering Markdown/HTML extraction.
- [ ] T010 Add fixture tests for authority precedence, stale subordinate references, and unsupported input.

## Canonical normalization and persistence

- [ ] T011 Normalize phases, 35 work packages, 53 delivery cards, and 7 milestones/decisions.
- [ ] T012 Preserve parent-child relationships, keep `plannedEffortHours` separate from authored working duration and baseline dates, and distinguish parent/card effort accounting.
- [ ] T013 Normalize provenance, roles, assignments, policies, capacity, reserve, and warnings.
- [ ] T014 Implement deliberate schema-versioned canonical JSON export.
- [ ] T015 Implement canonical JSON structural validation and reopen; fail duplicate IDs, impossible hierarchy, nonexistent structural references, malformed fields, and unmarked missing references while retaining only explicitly invalid source dependency evidence.
- [ ] T016 Add deterministic regression comparison against the IDEAEngineering fixture.

## Management analysis

- [ ] T017 Implement hierarchy/WBS projections from canonical IDs.
- [ ] T018 Implement dependency target validation and cycle detection.
- [ ] T019 Implement Finish-to-Start CPM and dependency critical-path metrics from normalized planned duration, never raw effort.
- [ ] T020 Implement baseline-vs-calculated variance without baseline mutation, including authored baseline dates versus earliest/latest/float/forecast.
- [ ] T021 Implement overdue derivation, effort-based capacity/load, reserve semantics, health, and unknown-state rules; preserve 88 initial hours and do not fabricate consumption.
- [ ] T022 Add tests that separate dependency critical path from single-coder baseline constraints.

## CARIO output

- [ ] T023 Define mapping configuration for logical roles and concrete identities.
- [ ] T024 Implement unresolved mapping and organization warning generation.
- [ ] T025 Implement the narrow Open XML workbook writer with six required sheets and explicit package relationships.
- [ ] T026 Add ZIP-part, relationship/XML, Unicode, dates, numeric cells, six-sheet, warnings, hierarchy, dependency, and no-fabrication tests without Excel.

## Browser application

- [ ] T027 Implement the loopback-only ASP.NET Core host and compilation endpoints.
- [ ] T028 Implement source review and warning presentation using generic “Source” language.
- [ ] T029 Implement WBS, Gantt, Kanban, critical-path, dashboard, and export views from shared view models.
- [ ] T030 Implement canonical JSON reopen in the UI.
- [ ] T031 Add an end-to-end fixture smoke test for analyze → review → views → JSON/XLSX export, including `Delivery cards completed 0/53` labeling.

## Verification and handoff

- [ ] T032 Run offline tests, build, deterministic rerun, source safety fixture tests, and workbook XML/package verification.
- [ ] T033 Review the diff against the approved design/specification and remediate findings.
- [ ] T034 Run final verification-before-completion checks and document exact application/export commands.
