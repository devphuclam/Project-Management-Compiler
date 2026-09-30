# Tasks: Feature 009 — Unified Work

**Plan baseline**: Human-approved plan on `main` at `3e19509b73ef8553a14df1a32d0e990816942d3f`.

**Task-review branch**: `codex/feature009-unified-work-tasks`.

**Authority**: `spec.md`, the approved Unified Work design and its UW-01…UW-18 provenance, `plan.md`, `research.md`, `data-model.md`, `contracts/work-view.md`, `quickstart.md`, and `.specify/memory/constitution.md`.

**Execution boundary**: This file is a reviewed task graph, not authorization to implement before the task-review gate. Tasks add no database, persistence, framework, package, source write-back, baseline/Actual/proposal editing, Work Package progress, second dependency graph/scheduler/execution authority, or new attention signal. Keep the exact attention whitelist, `key.kind + key.id` identity without a top-level alias, `IncludedInAnalysis == true` primary dependency eligibility, and all approved unknown/null/zero semantics.

**Test approach**: Follow the existing C# executable test harness and browser-native static UI regression conventions. Tests precede implementation in each vertical slice; do not add a JavaScript test framework or package.

## Phase 1: Setup and authority characterization

**Purpose**: Lock existing source, execution, dependency, Gantt, and test-harness seams before adding Work.

- [X] T001 Add a synthetic in-memory Work fixture and fill only missing characterization gaps for official execution versus legacy fallback, dependency `IncludedInAnalysis`/exclusion reasons, and typed Gantt selection/direct-versus-transitive behavior in `tests/ProjectManagementCompiler.Tests/WorkTestFixtures.cs`, `tests/ProjectManagementCompiler.Tests/WorkAuthorityCharacterizationTests.cs`, and `tests/ProjectManagementCompiler.Tests/Program.cs`; use no private IDEAEngineering data, register the tests in the existing runner, and confirm the characterization suite passes before production changes (FR-002, FR-026…FR-029, FR-030, FR-033; SC-008…SC-009).

---

## Phase 2: Foundational Work projection and API

**Purpose**: Add one canonical-backed, read-only projection and expose it through existing view composition before any browser Work story depends on it.

- [X] T002 Add failing projector tests in `tests/ProjectManagementCompiler.Tests/WorkProjectionTests.cs` and register them in `tests/ProjectManagementCompiler.Tests/Program.cs` for arbitrary canonical phase counts, Phase → Work Package → Delivery Card parentage/order, malformed or missing parents not being guessed, control points excluded from `cards`, sole `key.kind + key.id` identity with no top-level `id`, and current phase resolved only from the official reporting date when exactly one valid phase contains it (FR-002…FR-005, FR-007; SC-001…SC-002, SC-005); run the existing test executable and record the intended RED result.
- [X] T003 Implement the minimal `WorkProjection` and initial `WorkProjector` in `src/ProjectManagementCompiler/Management/WorkProjection.cs` and `src/ProjectManagementCompiler/Management/WorkProjector.cs`; derive hierarchy only from canonical entities, retain canonical order, use `CanonicalWorkItemKey.DeliveryCard`, include all imported phases without a fixed count, clean reader-facing names with `ReaderFacingTextPolicy`, and leave current phase null for missing/ambiguous dates; make T002 green without changing Overview output (FR-002…FR-005, FR-007; SC-001…SC-002, SC-005).
- [X] T004 Add failing field-authority tests in `tests/ProjectManagementCompiler.Tests/WorkProjectionTests.cs` for every authored state versus null/unrecorded, official source records not falling back to legacy overlay, recorded Actual/Remaining values versus unrecorded records, known zero versus `UNKNOWN`/`INVALID`/`BLOCKED`/`UNRESOLVED`/`NOT_RUN`, planned values remaining separate, logical role versus authoritative person, exact attention targets/codes/consequences/order and no raw message, and stable provenance; register and run them through `tests/ProjectManagementCompiler.Tests/Program.cs` (FR-011, FR-013, FR-022, FR-026…FR-028, FR-037…FR-039; SC-003, SC-006, SC-009); confirm intended RED before implementation.
- [X] T005 Complete `WorkCard` projection in `src/ProjectManagementCompiler/Management/WorkProjection.cs` and `src/ProjectManagementCompiler/Management/WorkProjector.cs` by reusing `ExecutionTruthResolver.ForCard`, canonical baseline/provenance, source-backed assignments and the existing `ProjectOverviewProjector` consequence mapping; serialize identity only as `key.kind`/`key.id`; emit `attention[]` entries with exactly `{ code, consequence }`, only `START_DELAY`, `OVERDUE`, `SUSPENDED`, `AT_RISK`, and deterministic code-ordinal → `DerivedAt` → ordinal-sorted reason-ID ordering; do not emit Actual values from an unrecorded record or change Overview wording; make T004 green (FR-002, FR-011, FR-013, FR-022, FR-026…FR-028, FR-037…FR-039; SC-003, SC-006, SC-009).
- [X] T006 Add failing aggregate and named-view contract tests in `tests/ProjectManagementCompiler.Tests/WorkApiTests.cs`, register them in `tests/ProjectManagementCompiler.Tests/Program.cs`, and verify `GET /api/views` contains `work`, `GET /api/views/work` returns the same projection, and no-project behavior is the existing `404`/`NO_PROJECT`; use the existing source-contract test style rather than adding an HTTP test framework (FR-002); confirm intended RED.
- [X] T007 Add `Work` to `ManagementViewSet` and build it from the existing canonical project/`ManagementAnalysis` in `src/ProjectManagementCompiler/Management/ManagementViewProjector.cs`; add only the `work` case to the existing named-view switch in `src/ProjectManagementCompiler/Program.cs`; preserve aggregate compatibility and existing no-project/error behavior, and make T006 green (FR-002).

**Checkpoint**: One read-only canonical Work projection is available from the aggregate and named route; no UI or source authority has been duplicated.

---

## Phase 3: User Story 1 — Find project work in its hierarchy (Priority: P1)

**Goal**: Make `Công việc` a real primary destination that opens a readable List over every phase in the imported hierarchy, focusing (not filtering to) the uniquely current phase.

**Independent test**: Load a synthetic official project with a variable number of phases, nested packages/cards, and control points; open Work and confirm List default, All phases scope, focus only when uniquely determined, canonical hierarchy/identity, no milestone cards, and an explicit phase choice affecting the shared scope.

- [X] T008 [US1] Add failing UI-shell regression assertions in `tests/ProjectManagementCompiler.Tests/ManagementUiShellTests.cs` for primary navigation `Tổng quan | Công việc | Gantt`, Work opening List, Feature 008 unloaded/import behavior remaining intact, All phases being the default, current-phase focus not becoming a filter, explicit phase selection being shared with both Work modes, and milestones/decisions staying out of Work; update registration in `tests/ProjectManagementCompiler.Tests/Program.cs` and confirm intended RED (FR-001, FR-003…FR-007; SC-001…SC-002).
- [X] T009 [US1] Implement the primary Work navigation and loaded-workspace List entry in `src/ProjectManagementCompiler/wwwroot/index.html`, `src/ProjectManagementCompiler/wwwroot/app.js`, and `src/ProjectManagementCompiler/wwwroot/styles.css`; consume `state.views.work` from the existing aggregate, add transient Work state with List default and All phases scope, focus/open only the known current phase without narrowing scope, and leave unloaded/import and Advanced destinations unchanged; make T008 green (FR-001…FR-007; SC-001…SC-002).
- [X] T010 [US1] Add failing List hierarchy/content regressions in `tests/ProjectManagementCompiler.Tests/WorkUiRegressionTests.cs` and register them in `tests/ProjectManagementCompiler.Tests/Program.cs` for one row per canonical Delivery Card, correct Phase/Work Package ancestry and order, no control-point rows, reader-facing name first, stable ID secondary, `Đầu mối / vai trò`, no effort columns by default, and `Kết thúc kế hoạch` wording; verify provenance, source SHA, diagnostics, CPM, and extended evidence do not dominate default rows; confirm intended RED and retain existing shell/Gantt regression coverage (FR-003…FR-004, FR-008, FR-011…FR-013; SC-002, SC-009).
- [X] T011 [US1] Implement canonical List rows and explicit phase-scope control in `src/ProjectManagementCompiler/wwwroot/app.js` and `src/ProjectManagementCompiler/wwwroot/styles.css`; render hierarchy from the Work projection without synthesizing items, keep milestone/decision points out, show the reader-facing work name first and technical identity second, use `Đầu mối / vai trò`, omit effort columns, label baseline finish `Kết thúc kế hoạch`, keep provenance/source SHA/diagnostics/CPM/extended evidence secondary rather than row-leading, and retain the explicit scope for later List/Kanban sharing; make T010 green (FR-003…FR-008, FR-011…FR-013; SC-001…SC-002, SC-009).

---

## Phase 4: User Story 2 — Understand recorded state on Kanban (Priority: P1)

**Goal**: Present authored execution states faithfully, keep `Chưa ghi nhận` separate, and compute stable counts over the same card identities as List.

**Independent test**: With cards in every authored state, null/unrecorded records, eligible and excluded alerts, and tied/missing canonical positions, compare List/board identity sets and verify state groups, visible zero/post-filter counts, and repeatable ordering.

- [X] T012 [US2] Add failing Kanban UI/projection regressions in `tests/ProjectManagementCompiler.Tests/WorkUiRegressionTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs` for `NOT_STARTED`/`IN_PROGRESS`/`COMPLETED`, compact `SUSPENDED`/`CANCELLED`, separate counted `Chưa ghi nhận`, each card appearing in at most one group, post-filter counts including zero, current-phase-first then canonical hierarchy/source order with ordinal ID tie-break, and no legacy WIP semantics; confirm intended RED (FR-008, FR-020…FR-025; SC-002…SC-005).
- [X] T013 [US2] Implement Unified Kanban in `src/ProjectManagementCompiler/wwwroot/app.js` and `src/ProjectManagementCompiler/wwwroot/styles.css` from the same filtered Work Delivery Card collection used by List; retain distinct authored-state groups, the compact expandable `SUSPENDED`/`CANCELLED` area with separate counts, and the separate `Chưa ghi nhận` area; calculate every group count after active criteria including zero, use the approved deterministic presentation order, and do not reuse the legacy Kanban projection or add WIP policy; make T012 green (FR-008, FR-020…FR-025; SC-002…SC-005).
- [X] T014 [US2] Add failing attention-display regressions in `tests/ProjectManagementCompiler.Tests/WorkUiRegressionTests.cs` and register them in `tests/ProjectManagementCompiler.Tests/Program.cs` to prove only structured alerts whose `WorkItemId` resolves to a canonical Delivery Card and whose code is allowlisted are filterable; exclude `COMPLETED_LATE`, `COMPLETED_ON_TIME`, `CANCELLED`, `BLOCKED`, readiness/gate/decision/human-action records, diagnostics, import/source warnings, governance concerns, arbitrary warnings, and unlisted codes; verify consequence—not machine code/message—is reader-facing, attention stays separate from authored state, and an empty result says no supported signal is available rather than “risk-free”; confirm intended RED (FR-037…FR-039; SC-006).
- [X] T015 [US2] Render Work attention in `src/ProjectManagementCompiler/wwwroot/app.js` using only each projected entry's `consequence` as primary copy and its allowlisted `code` only as a technical filter key; keep attention independent of authored-state grouping, show the approved no-supported-signal empty-state meaning without implying risk-free status, add no alert interpretation in the browser, and make T014 green (FR-037…FR-039; SC-006).

---

## Phase 5: User Story 3 — Narrow the same work collection in List and Kanban (Priority: P1)

**Goal**: Let a manager search/filter either view without changing shared scope, losing view context, or seeing orphaned hierarchy.

**Independent test**: Apply phase, Vietnamese-insensitive query, authored-state, Needs Attention, and explicit unrecorded criteria in List; switch List/Kanban repeatedly and verify identical surviving identities, deterministic counts, allowed search fields, required ancestor paths, and recovery behavior.

- [X] T016 [US3] Add failing shared-search/filter regressions in `tests/ProjectManagementCompiler.Tests/WorkUiRegressionTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs` for case- and Vietnamese-diacritic-insensitive matches over only card name, stable ID, parent Phase name, and parent Work Package name; parent matches making descendant cards candidates; all shared choices surviving List/Kanban switches; and Work choices not mutating canonical/source, execution, analysis, or Gantt-specific state; confirm intended RED (FR-006, FR-008…FR-010, FR-014…FR-015, FR-024; SC-002, SC-004…SC-005).
- [X] T017 [US3] Implement one Work filter/search pipeline and its controls in `src/ProjectManagementCompiler/wwwroot/app.js`, with control markup in `src/ProjectManagementCompiler/wwwroot/index.html` and styling in `src/ProjectManagementCompiler/wwwroot/styles.css`; apply phase scope, normalized query, authored-state, Needs Attention, and explicit unrecorded selection to the shared card collection, search only approved reader-facing fields, preserve choices and active mode across List/Kanban, and keep Work state in memory rather than `localStorage`; make T016 green (FR-006, FR-008…FR-010, FR-014…FR-015, FR-024; SC-002, SC-004…SC-005).
- [X] T018 [US3] Add failing List hierarchy/empty-state regressions in `tests/ProjectManagementCompiler.Tests/WorkUiRegressionTests.cs` and register them in `tests/ProjectManagementCompiler.Tests/Program.cs` for ancestor-only paths with surviving descendants, no orphan ancestors/count inflation, search-revealed paths that restore pre-search expansion, phase scope alone not being a result filter, and distinct empty-phase versus no-match recovery actions; confirm intended RED (FR-016…FR-019; SC-007).
- [X] T019 [US3] Implement filtered List ancestry and recovery behavior in `src/ProjectManagementCompiler/wwwroot/app.js` and `src/ProjectManagementCompiler/wwwroot/styles.css`; reveal only paths required by surviving cards, never widen phase scope, keep expansion separate from shared filters, restore prior expansion when search clears, and distinguish empty phase from no results; make T018 green (FR-016…FR-019; SC-007).

---

## Phase 6: User Story 4 — Inspect work and move between Work and Gantt (Priority: P1)

**Goal**: Reuse one read-only, evidence-backed inspector and stable canonical identity across List, Kanban, and Gantt while keeping selection, inspector visibility, keyboard focus, and scroll independent.

**Independent test**: Select one card from each view, compare identity and evidence-backed fields, inspect non-card variants, filter a still-selected card out, close/reopen by keyboard, and navigate both directions to Gantt without losing the approved state.

- [ ] T020 [US4] Add failing inspector contract/regression tests in `tests/ProjectManagementCompiler.Tests/WorkUiRegressionTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs` for one `key.kind + key.id` identity across views; applicable plan/Actual/Remaining/role/provenance fields; known zero versus `UNKNOWN`/`INVALID`/`BLOCKED`/`UNRESOLVED`/`NOT_RUN`; no edits/proposals; kind-appropriate non-card detail; and primary dependency links using only direct `dependencyNetwork` edges with `IncludedInAnalysis == true`, excluding invalid-source, unsupported, Work Package traceability, missing-endpoint, and other non-analysis edges; verify directional predecessor/successor labels and linked canonical item names, with no transitive Work impact; confirm intended RED (FR-011…FR-013, FR-026…FR-029, FR-037…FR-039; SC-006, SC-008…SC-009).
- [ ] T021 [US4] Implement the shared read-only inspector in `src/ProjectManagementCompiler/wwwroot/app.js` and `src/ProjectManagementCompiler/wwwroot/styles.css`; compose Delivery Card details from `work.cards` and existing source/Gantt/provenance views, render only evidence-backed fields, preserve known zero while keeping `UNKNOWN`/`INVALID`/`BLOCKED`/`UNRESOLVED` distinct and no execution record explicitly not recorded, use `Kết thúc kế hoạch`, keep kinds other than Delivery Card evidence-limited, and place a clearly titled collapsible dependency section after primary details with directional `Phụ thuộc vào` / `Ảnh hưởng trực tiếp đến` claims only for direct edges where `includedInAnalysis === true`; if excluded evidence is ever displayed, label it excluded in Advanced, add no transitive impact or edit/proposal action, and make T020 green (FR-011…FR-013, FR-026…FR-029, FR-037…FR-039; SC-006, SC-008…SC-009).
- [ ] T022 [US4] Add failing interaction regressions in `tests/ProjectManagementCompiler.Tests/WorkUiRegressionTests.cs`, `tests/ProjectManagementCompiler.Tests/GanttUiRegressionTests.cs`, and `tests/ProjectManagementCompiler.Tests/Program.cs` for selection persistence by stable identity while the same official snapshot remains active, separate inspector-open/focus/scroll state, focus return to the invoking enabled item or approved fallback, per-view scroll restoration when criteria are unchanged, selected-item reveal only if it matches after criteria change (otherwise start of current results), hidden-selection explanation without filter widening, Work→Gantt identity/reveal with prior drawer condition preserved, Gantt→Work filter preservation, and no reset on failed import/candidate preview; confirm intended RED (FR-030…FR-033; SC-008).
- [ ] T023 [US4] Implement selection, focus, scroll, and cross-view state in `src/ProjectManagementCompiler/wwwroot/app.js`; use the active official snapshot identity only to clear stale selection, keep selection separate from inspector visibility and keyboard focus, restore each Work mode's scroll when criteria are unchanged, when criteria change reveal the selected item only if it matches or otherwise go to the start of current results, navigate by the typed canonical key, reveal the matching Gantt row/ancestors when available, and preserve existing Gantt scheduling/dependency behavior; make T022 green (FR-030…FR-033; SC-008).

---

## Phase 7: User Story 5 — Use Work accessibly and preserve specialist parity (Priority: P2)

**Goal**: Keep Work operable at narrow and wide viewports and by keyboard, while retaining WBS/legacy Kanban until evidence supports the approved parity transition.

**Independent test**: Use keyboard only at 360px and 1280px; verify all Work actions and state-group counts, full-screen inspector focus/close, no drag-only action, and compare Work hierarchy/card/state/filter/inspector access with legacy views before any legacy route transition.

- [ ] T024 [US5] Add failing responsive/accessibility regressions in `tests/ProjectManagementCompiler.Tests/WorkUiRegressionTests.cs`, `tests/ProjectManagementCompiler.Tests/ManagementUiShellTests.cs`, and `tests/ProjectManagementCompiler.Tests/Program.cs` for readable narrow List hierarchy; 360px/1280px controls; visible keyboard focus; one labeled narrow Kanban group with all counts; full-screen inspector focus/Escape return; state not conveyed by color/motion alone; and no drag-only interaction; confirm intended RED (FR-034…FR-036; SC-010).
- [ ] T025 [US5] Implement responsive and keyboard behavior in `src/ProjectManagementCompiler/wwwroot/index.html`, `src/ProjectManagementCompiler/wwwroot/app.js`, and `src/ProjectManagementCompiler/wwwroot/styles.css`; keep List hierarchy/readability at narrow width; show one labeled Kanban state group while retaining every group's post-filter count; make the inspector full-screen and focusable with Escape/Close restoration; retain native keyboard-operable controls; and avoid page-level horizontal clipping without changing Gantt's contained timeline scroll; make T024 green (FR-034…FR-036; SC-010).
- [ ] T026 [P] [US5] Add parity-gate regressions in `tests/ProjectManagementCompiler.Tests/WorkProjectionTests.cs`, `tests/ProjectManagementCompiler.Tests/WorkUiRegressionTests.cs`, `tests/ProjectManagementCompiler.Tests/ManagementUiShellTests.cs`, and `tests/ProjectManagementCompiler.Tests/Program.cs` comparing canonical Delivery Card identities, authored plus unrecorded state, applicable scope/search/filter behavior, hierarchy paths, and inspector reachability; prove legacy WBS and existing Kanban remain reachable under Advanced until the corresponding evidence passes, Work is the only normal Kanban destination, and WBS is not retired; record unmet parity as unmet rather than assuming completion (FR-040…FR-041; SC-002…SC-003, SC-011).
- [ ] T027 [P] [US5] Update `specs/009-unified-work/quickstart.md` with a concise parity evidence record/checklist for the separate WBS hierarchy and legacy Kanban gates, the compared identities/states/filters/inspector access, and the required action when parity fails; use only the synthetic/public-safe fixture and do not claim parity without test/browser evidence (FR-040…FR-041; SC-011).

**Parallel note**: T026 and T027 may run in parallel after T025 because they touch separate test/runner versus documentation files and neither depends on the other's edits. No other task is marked parallel due the shared test registry and centralized `app.js` state/render path.

---

## Phase 8: Polish and cross-cutting verification

**Purpose**: Verify the complete increment and public-repository hygiene; do not treat an unavailable prerequisite as a pass.

- [ ] T028 Run the focused C# executable plus `scripts/test.ps1`, `scripts/verify.ps1`, and `scripts/verify-web.ps1` only when its documented safe loopback preconditions are met; manually launch with `Run Project.cmd` and verify the approved scenarios at 360px/1280px; inspect `git status` and staged diff for only intended files, secrets/private IDEAEngineering data, generated artifacts, and package changes; record which FR/SC checks actually passed and report environment blockers separately (FR-001…FR-043; SC-001…SC-011).

---

## Dependencies and critical sequencing

- **Setup**: T001 establishes the authority baseline and synthetic fixture; no project initialization or dependency installation is needed.
- **Foundation**: T002 → T003 → T004 → T005 → T006 → T007. The Work UI stories cannot start until the projection, execution/evidence mapping, and additive API are available.
- **Story order**: US1 (T008…T011) → US2 (T012…T015) → US3 (T016…T019) → US4 (T020…T023) → US5 (T024…T027). Every story is independently testable at its checkpoint, but the product sequence is intentionally dependent because Kanban/search/inspector build on the shared List collection and identity.
- **Within each implementation slice**: run its test task first and observe the expected RED before its implementation task; rerun the focused executable after implementation. Existing tests must remain green as regression coverage.
- **Legacy transition**: T026/T027 are evidence gates, not an assumption that parity already exists. Until they pass, keep legacy WBS and Kanban under Advanced. Do not retire WBS in Feature 009.
- **Final verification**: T028 depends on every prior task and is not a substitute for the story-level tests or manual responsive/parity evidence.

## Parallel execution opportunities

- **US1**: None safe; navigation, shared phase state, List rendering, and the central test registry are coupled.
- **US2**: None safe; group counts/order and the single Work collection must be implemented and verified together.
- **US3**: None safe; search, filters, ancestor paths, and shared view state use the same filter pipeline and `app.js` state.
- **US4**: None safe; inspector identity and keyboard/scroll/Gantt transitions share state and focus ownership.
- **US5**: T026 and T027 can run together after T025 as marked `[P]` above.

## Requirement-to-task traceability

| Requirement | Coverage tasks |
|---|---|
| FR-001 | T008, T009 |
| FR-002 | T001, T003, T005, T006, T007, T009, T028 |
| FR-003 | T002, T003, T010, T011 |
| FR-004 | T002, T010, T011 |
| FR-005 | T002, T003, T008, T009 |
| FR-006 | T008, T009, T016, T017 |
| FR-007 | T002, T003, T008, T009 |
| FR-008 | T002, T003, T012, T013, T016, T017 |
| FR-009 | T016, T017 |
| FR-010 | T016, T017, T028 |
| FR-011 | T010, T011, T020, T021 |
| FR-012 | T010, T011, T021 |
| FR-013 | T010, T011, T021 |
| FR-014 | T016, T017 |
| FR-015 | T016, T017 |
| FR-016 | T018, T019 |
| FR-017 | T018, T019 |
| FR-018 | T018, T019 |
| FR-019 | T018, T019 |
| FR-020 | T012, T013 |
| FR-021 | T012, T013 |
| FR-022 | T004, T005, T012, T013 |
| FR-023 | T012, T013 |
| FR-024 | T012, T013, T016, T017 |
| FR-025 | T012, T013 |
| FR-026 | T020, T021 |
| FR-027 | T020, T021 |
| FR-028 | T020, T021 |
| FR-029 | T001, T020, T021 |
| FR-030 | T022, T023 |
| FR-031 | T022, T023 |
| FR-032 | T022, T023 |
| FR-033 | T022, T023 |
| FR-034 | T024, T025 |
| FR-035 | T024, T025 |
| FR-036 | T024, T025 |
| FR-037 | T004, T005, T014, T015 |
| FR-038 | T004, T005, T014, T015 |
| FR-039 | T004, T005, T014, T015 |
| FR-040 | T026, T027 |
| FR-041 | T026, T027 |
| FR-042 | T001, T003, T005, T011, T021, T028 |
| FR-043 | T026, T027, T028 |

## Success-criterion verification map

| Success criterion | Verification path |
|---|---|
| SC-001 | T002, T003, T008, T009, T028: variable phase fixture; unique/unknown current phase; List opens All phases and focus does not filter. |
| SC-002 | T002, T010, T011, T012, T013, T016, T017, T026, T028: compare exact canonical keys in List/Kanban before and after shared filters; exclude control points/ancestors as cards. |
| SC-003 | T004, T005, T012, T013, T028: every authored state maps once; null/unrecorded appears only under `Chưa ghi nhận`. |
| SC-004 | T012, T013, T016, T017, T028: recompute each group count from matching cards for combined criteria; keep zero headings and exclude ancestors/control points. |
| SC-005 | T002, T003, T012, T013, T028: repeat projection/grouping for tied or missing positions and compare deterministic order. |
| SC-006 | T004, T005, T014, T015, T028: exact four-code target whitelist; excluded/unattributed signals yield no Work attention. |
| SC-007 | T018, T019, T028: inspect filtered List paths for surviving descendants only and confirm no scope widening. |
| SC-008 | T020…T023, T028: same selected identity across views; separately verify inspector, focus, scroll, filter, and Gantt restoration. |
| SC-009 | T004, T005, T010, T011, T020, T021, T028: known zero/null/not-run/absent remain distinct and planned finish remains a baseline label. |
| SC-010 | T024, T025, T028: keyboard-only browser pass at 360px and 1280px; required controls visible; inspector focus and return verified. |
| SC-011 | T026, T027, T028: record separate WBS/Kanban parity evidence; retain Advanced routes when a gate is unmet; never claim parity by default. |

## MVP and delivery strategy

- **Suggested first usable increment**: Foundation plus User Story 1 (T001…T011): additive read-only projection/API and a real primary List over the canonical hierarchy.
- Deliver each later story as a verified vertical slice in priority order. Do not begin Proposal Workflow or implementation until the human task-review gate approves this task graph.
