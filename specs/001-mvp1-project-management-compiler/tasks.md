# MVP1 Spec Kit Task Baseline

These tasks are the dependency-ordered execution outline for the manually
maintained Spec Kit package. The detailed bite-sized plan is produced after the
written-spec review gate.

## Foundation

- [ ] T001 Create the `net10.0` solution and platform-only project structure.
- [ ] T002 Add the dependency-free executable test harness and test command.
- [ ] T003 Add the constitution, context, ADR, and source fixture metadata to the repository checks.
- [ ] T004 Define canonical IDs, data-state enums, source references, diagnostics, and execution states.

## Source and extraction

- [ ] T005 Define `IProjectSourceAdapter`, `SourceRequest`, and `RepositorySnapshot`.
- [ ] T006 Implement mandatory local repository capture with deterministic document selection.
- [ ] T007 Implement optional existing-Git HTTPS capture with temporary workspace cleanup.
- [ ] T008 Implement planning-document discovery and authority resolution.
- [ ] T009 Implement deterministic IDEAEngineering Markdown/HTML extraction.
- [ ] T010 Add fixture tests for authority precedence, stale subordinate references, and unsupported input.

## Canonical normalization and persistence

- [ ] T011 Normalize phases, 35 work packages, 53 delivery cards, and 7 milestones/decisions.
- [ ] T012 Preserve parent-child relationships and distinguish parent/card effort accounting.
- [ ] T013 Normalize provenance, roles, assignments, policies, capacity, reserve, and warnings.
- [ ] T014 Implement deliberate schema-versioned canonical JSON export.
- [ ] T015 Implement canonical JSON validation and reopen.
- [ ] T016 Add deterministic regression comparison against the IDEAEngineering fixture.

## Management analysis

- [ ] T017 Implement hierarchy/WBS projections from canonical IDs.
- [ ] T018 Implement dependency target validation and cycle detection.
- [ ] T019 Implement Finish-to-Start CPM and dependency critical-path metrics.
- [ ] T020 Implement baseline-vs-calculated variance without baseline mutation.
- [ ] T021 Implement overdue derivation, capacity/load, reserve, health, and unknown-state rules.
- [ ] T022 Add tests that separate dependency critical path from single-coder baseline constraints.

## CARIO output

- [ ] T023 Define mapping configuration for logical roles and concrete identities.
- [ ] T024 Implement unresolved mapping and organization warning generation.
- [ ] T025 Implement the narrow Open XML workbook writer with six required sheets.
- [ ] T026 Add workbook structure, UTF-8, headers, warnings, hierarchy, dependency, and no-fabrication tests.

## Browser application

- [ ] T027 Implement the loopback-only ASP.NET Core host and compilation endpoints.
- [ ] T028 Implement source review and warning presentation using generic “Source” language.
- [ ] T029 Implement WBS, Gantt, Kanban, critical-path, dashboard, and export views from shared view models.
- [ ] T030 Implement canonical JSON reopen in the UI.
- [ ] T031 Add an end-to-end fixture smoke test for analyze → review → views → JSON/XLSX export.

## Verification and handoff

- [ ] T032 Run offline tests, build, deterministic rerun, and workbook XML verification.
- [ ] T033 Review the diff against the approved design/specification and remediate findings.
- [ ] T034 Run final verification-before-completion checks and document exact application/export commands.
