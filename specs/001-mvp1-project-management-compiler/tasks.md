# MVP1 Spec Kit Task Baseline

These tasks are the dependency-ordered execution outline for the manually
maintained Spec Kit package. The detailed bite-sized plan is produced after the
written-spec review gate.

## Corrective remediation — current source compatibility

The following gate must complete before implementation continues past the
management/output work already present on the branch. It is anchored to the
current public IDEAEngineering reference at commit
`afa9638f629999de6faa881ca25226cda44820a5`.

- [x] R001 Inspect the current IDEAEngineering source structure and record field-level authority.
- [x] R002 Run clean verification from the pre-remediation compiler commit.
- [x] R003 Add failing compatibility tests shaped like DOC-07, Appendix A, and Kanban/CARIO.
- [x] R004 Amend design/specification/contracts, ADR, context, and task gates.
- [x] R005 Rebuild the public-safe controlled fixture from the current source shape.
- [x] R006 Implement field-level authority resolution and separate rendition validity.
- [x] R007 Extract DOC-07 control, baseline, phase schedule, reserve, and milestones.
- [x] R008 Extract Vietnamese Appendix A work packages, outputs, effort, and predecessor evidence.
- [x] R009 Extract Kanban cards, CARIO matrices, and many-to-many logical-role assignments.
- [x] R010 Preserve work-package, card, and gate dependency graphs without double-feeding CPM.
- [x] R011 Preserve nullable authored dates/states, capture provenance, safe source identity, and the source-content boundary.
- [x] R012 Run optional local-reference compatibility verification, review the diff, and record blockers before resuming Task 8+.

Remediation gate result: against IDEAEngineering reference commit
`afa9638f629999de6faa881ca25226cda44820a5`, Level-B extraction is canonical
with 6 phases, 35 work packages, 53 delivery cards, 7 milestones, 512 planned
hours, 88 reserve hours, 600 capacity hours, and 231 CARIO assignments. The
reference run has no errors; its retained diagnostics are six ordinary source
row conflicts and one authored effort/duration mismatch. The source identity is
safe and hashed, the resolved reference is retained, and captured source text
is excluded from canonical JSON. The gate is locally verified on the current
implementation worktree; the continuation branch has not yet been pushed at
this checkpoint.

## Continuation rebaseline — 2026-09-18

The following status is based on focused implementation tests, the dependency-
free test harness, the solution build, and the loopback verification workflow.
`COMPLETE` means the implementation and focused acceptance evidence exist;
`PARTIAL` means the implementation exists but the final gate is still open.

| Task | Classification | Evidence / remaining gate |
|---|---|---|
| T014 | COMPLETE | Schema 1.0 JSON writer emits additive `executionOverlay`; round-trip tests pass. |
| T015 | COMPLETE | Structural validator and reopen rejection tests cover canonical/overlay invariants. |
| T016 | COMPLETE | Fixture digest and overlay round-trip regression tests pass. |
| T017 | COMPLETE | Shared WBS projection preserves phase, work-package, card, and milestone hierarchy. |
| T018 | COMPLETE | Dependency validation, invalid-source retention, self-edge, duplicate, and cycle tests pass. |
| T019 | COMPLETE | Duration-based Finish-to-Start CPM and milestone nodes are covered by tests. |
| T020 | COMPLETE | Working-calendar variance and explicit as-of date are covered by tests. |
| T021 | COMPLETE | Manual overlay update validates evidence and preserves the baseline. |
| T022 | COMPLETE | Status, alert, capacity, reserve, health, and unknown-state behavior are covered. |
| T022A | COMPLETE | Dependency critical path, resource constraint, and WIP policy are separated in views/tests. |
| T023 | COMPLETE | Configuration-driven logical-role and task metadata mappings exist. |
| T024 | COMPLETE | Unresolved/invalid CARIO mappings remain blank and emit structured warnings. |
| T025 | COMPLETE | BCL-only deterministic six-sheet XLSX writer exists and follows the workbook contract. |
| T026 | COMPLETE | ZIP/XML, Unicode, numeric, warning, hierarchy, dependency, and baseline-date tests pass. |
| T027 | COMPLETE | Loopback API exposes compile, reopen, execution, views, warnings, and exports. |
| T028 | COMPLETE | Source and warning review is available through the browser UI. |
| T029 | COMPLETE | Shared WBS/Gantt/Kanban/dependency/CPM/dashboard projections are rendered. |
| T030 | COMPLETE | UI JSON reopen uses `/api/reopen` and recalculates from an explicit as-of date. |
| T031 | COMPLETE | Fixture E2E script covers analyze/update/alert/reopen/XLSX and UI labels `Delivery cards completed X/53`. |
| T032 | COMPLETE | `scripts/verify.ps1` runs build, tests, web/API, JSON, security, Gantt, and XLSX checks. |
| T033 | COMPLETE | Manual standards/spec review completed; baseline mutation, actual/duration conflation, forecast overclaiming, overdue semantics, JSON reopen, source safety, Gantt context, and CARIO planned-date regressions were checked and remediated. |
| T034 | COMPLETE | `docs/runbook/mvp1-local.md` documents build, test, verify, run, API, browser, JSON reopen, and CARIO export commands; final verification and repository hygiene checks pass. |

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
- [x] T014 Implement deliberate schema-versioned canonical JSON export with an additive schema-1.0 `executionOverlay` containing manual execution records.
- [x] T015 Implement canonical JSON structural validation and reopen; validate overlay targets, states, dates, and effort without mutating baseline, while retaining only explicitly invalid source dependency evidence.
- [x] T016 Add deterministic regression comparison against the IDEAEngineering fixture, including execution-overlay round-trip and baseline semantic equivalence.

## Management analysis

- [x] T017 Implement hierarchy/WBS projections from canonical IDs.
- [x] T018 Implement dependency target validation and cycle detection.
- [x] T019 Implement Finish-to-Start CPM and dependency critical-path metrics from normalized planned duration, never raw effort.
- [x] T020 Implement baseline-vs-calculated variance without baseline mutation, including working-calendar start/finish variance and explicit as-of date.
- [x] T021 Implement the manual execution-update seam and overlay validation: actual dates, actual/remaining effort states, state consistency, and no plan mutation.
- [x] T022 Implement status/alert analysis: start delay, active overdue, completed on time/late, suspended, cancelled, conservative dependency `AT_RISK`, effort-based capacity/load, reserve semantics, health, and unknown-state rules.
- [x] T022A Add tests that separate dependency critical path from single-coder baseline constraints and prove invalid dependencies cannot fabricate risk.

## CARIO output

- [x] T023 Define mapping configuration for logical roles and concrete identities.
- [x] T024 Implement unresolved mapping and organization warning generation.
- [x] T025 Implement the narrow Open XML workbook writer with six required sheets and explicit package relationships; keep planned dates separate from overlay actuals.
- [x] T026 Add ZIP-part, relationship/XML, Unicode, dates, numeric cells, six-sheet, warnings, hierarchy, dependency, no-fabrication, and plan-date-preservation tests without Excel.

## Browser application

- [x] T027 Implement the loopback-only ASP.NET Core host and compilation endpoints, including manual execution update and JSON snapshot routes.
- [x] T028 Implement source review and warning presentation using generic “Source” language.
- [x] T029 Implement WBS, three-lane PLAN/ACTUAL/ALERT Gantt, Kanban, critical-path, dashboard, and export views from shared view models.
- [x] T030 Implement canonical JSON reopen in the UI and recalculate derived alerts from an explicit as-of date.
- [x] T031 Add an end-to-end fixture smoke test for analyze → update execution → review lanes/alerts → views → JSON/XLSX export, including `Delivery cards completed 0/53` labeling.

## Verification and handoff

- [x] T032 Run offline tests, build, deterministic rerun, execution-overlay round-trip, source safety fixture tests, Gantt lane tests, and workbook XML/package verification.
- [x] T033 Review the diff against the approved design/specification for baseline mutation, actual/duration conflation, alert overclaiming, JSON compatibility, and CARIO planned-date regression; remediate findings.
- [x] T034 Run final verification-before-completion checks and document exact application/export commands.
