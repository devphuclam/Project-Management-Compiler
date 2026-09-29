# Phase 0 Research: Unified Work

**Feature**: [Unified Work specification](spec.md)
**Planning branch**: `codex/feature009-unified-work-plan`
**Research date**: 2026-09-29

This research records the repository evidence used for the implementation approach. The approved specification and design remain the authority for product behavior; the notes below explain how to implement them within the current architecture.

## Repository and workflow baseline

- The project constitution is `.specify/memory/constitution.md`. Its governing rules are canonical-model-first views, first-class baseline/evidence, deterministic extraction, tests through deep interfaces, restricted-environment delivery, explicit scope/safe failure, and design approval before implementation.
- Feature 008 (`specs/008-management-ui-foundation/plan.md`, `contracts/overview-view.md`, and `quickstart.md`) established the implementation pattern: extend the existing local ASP.NET Core app and management projections, keep browser-native HTML/JS/CSS, use the existing C# executable test harness, add no packages, preserve candidate/official source separation, and run repository scripts plus manual browser checks.
- The local launcher is `Run Project.cmd`, which delegates to `scripts/run-project.ps1`. Planning-time manual verification must use the supported launcher and its configured loopback behavior.
- No root or nested repository `AGENTS.md` was found during planning inspection. Constitution and feature contracts are the applicable project governance sources.

## Decision: Add Work as a management projection, not a second work model

**Evidence**: `src/ProjectManagementCompiler/Domain/CanonicalProject.cs` owns `Phases`, `WorkPackages`, `DeliveryCards`, `Milestones`, `Dependencies`, `Assignments`, `ExecutionOverlay`, `SourceExecution`, and provenance. `ManagementViewProjector.Build` in `src/ProjectManagementCompiler/Management/ManagementViewProjector.cs` already builds all current views into `ManagementViewSet`; `ProjectCompiler.BuildResult` invokes management analysis and that projector once per compiled result. `Program.cs` exposes `/api/views` plus a named view switch.

**Decision**: Add an additive `WorkProjection` to `ManagementViewSet`, build it in the existing projector composition, and expose the existing aggregate plus `/api/views/work`.

**Why**: This preserves a single canonical/source authority and existing API/view lifetimes. The selected project snapshot is already compiled and projected; a Work-specific service, persistence model, importer, or database would duplicate authority without solving a current architectural gap.

**Rejected**: A new task repository/table, a Work API subsystem with independent state, or a second source importer.

## Decision: One flat Delivery Card collection plus canonical hierarchy references

**Evidence**: `Domain/PlanningEntities.cs` models each Delivery Card with `WorkPackageId` and each Work Package with `DeliveryCardIds`; phases and packages are in `CanonicalProject`. `CanonicalProjectNormalizer` preserves a deterministic normalized hierarchy. `CanonicalWorkItemKey` in `Domain/WorkItemIdentity.cs` defines kind-qualified stable identity.

**Decision**: The new projection will contain one flat Delivery Card payload collection and hierarchy/order references for phases and work packages. List traverses those references; Kanban groups that same card collection. Use kind+ID identity (`CanonicalWorkItemKey.DeliveryCard`) when selection crosses view boundaries.

**Why**: A shared payload collection structurally prevents List and Kanban from drifting into distinct item identities or task data. Milestones/decision points already have distinct canonical entities and remain outside this collection.

**Rejected**: Separate List and Kanban projections that independently create cards, milestone flattening, or manual IDs as the identity source.

## Decision: Reuse execution-truth resolution; preserve record and state independently

**Evidence**: `Management/ExecutionTruthResolver.cs` is the current effective execution seam. In official source mode, it resolves only `SourceExecution.Records`; a missing record returns null and does not fall back to legacy `ExecutionOverlay`. In legacy mode it maps the existing overlay. Current Gantt and legacy Kanban projectors both call this resolver; source-mode code avoids falling back to canonical planning state when effective execution is absent.

**Decision**: Work uses `ExecutionTruthResolver.ForCard` and keeps at least the record/evidence-availability bit separate from nullable effective `ExecutionState`. Null effective state and no record are represented in the distinct unrecorded area as the approved FR-022 requires; neither is converted to `NOT_STARTED`. Baseline state is never treated as execution evidence.

**Why**: This preserves official-vs-legacy execution authority and the approved difference between no authored status and an authored `NOT_STARTED` value.

**Rejected**: Defaulting null to `NOT_STARTED`, falling back from official source records to legacy overlay, or deriving state from readiness/status prose.

**Field-state note**: Canonical planning entities carry `DataState` for planned effort/duration; legacy `ExecutionRecord` carries per-field actual/remaining effort states. The official `SourceExecutionRecord` has `RecordingState`, nullable state and values, but no per-field `DataState`; its adapter validates malformed inputs and emits diagnostics. The Work projection must retain null/zero and record availability faithfully, must not present diagnostics as field values, and must not claim an explicit per-field source state that the imported model does not carry. If implementation discovers another authoritative field-level contract already consumed by the app, reuse it without broadening Work attention or changing the source schema.

## Decision: Derive only the approved attention subset from ManagementAnalysis

**Evidence**: `Management/StatusAnalyzer.cs` is the producer of structured management alerts. `Domain/AnalysisEntities.cs` types them as `Alert` entries in `ManagementAnalysis.Alerts`, with `WorkItemId`, `AlertCode`, severity, message, and reasons. `ProjectOverviewProjector` has an existing supported reader-facing consequence mapping for `START_DELAY`, `OVERDUE`, `SUSPENDED`, and `AT_RISK`.

**Decision**: Work matches only those four exact codes, only when the alert target resolves case-insensitively to a canonical Delivery Card. Reuse or safely share the existing concise consequence mapping, with tests guarding unchanged Overview behavior. Keep alert signal distinct from authored execution state.

**Why**: The reviewed whitelist is already supported by the current producer and consequence mapping; no new risk inference is needed.

**Rejected**: Treating all warnings, diagnostics, readiness evidence, governance records, all alert codes, or Gantt row styles as Work attention.

## Decision: Reuse existing reader-facing role labels without inventing people

**Evidence**: Canonical `Assignment` records carry logical/CARIO role codes, `ConcreteIdentity`, mapping status, and source references. The IDEAEngineering extractor currently sets concrete identity to null and mapping status to `UNRESOLVED`. `ExecutiveProgressReportProjector` has an existing Vietnamese reader-facing role-code mapping; other projectors have narrower private mappings.

**Decision**: Work presents the source-backed role assignment(s) using the existing broader reader-facing mapping. If sharing requires extraction, add a small common mapper and regression-test report output. Keep concrete person identity separate and show a person only when authoritative mapping provides one. Do not silently replace an unresolved role assignment with a person or a fabricated owner.

**Why**: This meets UW-16 while reusing language already present in the app and preserving source responsibility semantics.

**Rejected**: Treating `LEAD`/another role code as an employee name, reducing many-to-many role assignments to an unsupported single person, or creating a new role taxonomy in the UI.

## Decision: Resolve the focused current phase using the existing unique-date rule

**Evidence**: `ProjectOverviewProjector.BuildCurrentPhase` uses the official `ImportMetadata.RegisterStatusDate`, requires valid planned start/finish and exactly one containing Phase, and otherwise returns unknown. It currently exposes the readable phase name/date/state but not its stable ID.

**Decision**: Work carries a nullable current phase ID by applying that same unique-match rule to the canonical phases and official reporting date. Prefer extracting a small shared resolver only if it preserves Overview's current output and is covered by its existing projection tests. Current-phase focus/order must not mutate All phases scope.

**Rejected**: First/last phase fallback, matching by authored status text, workstation date, optional analysis override, or making the focused phase an implicit filter.

## Decision: Reuse the dependency projection; do not compute a second graph

**Evidence**: `ManagementViewProjector.BuildDependencyNetwork` creates typed `DependencyNetworkNode` and `DependencyNetworkEdge` projections from canonical items/dependencies, including validation and analysis-inclusion information. `GanttProjector` and management analysis have their own established scheduling/dependency calculations.

**Decision**: The shared inspector reads direct predecessor/successor relations and names from the existing dependency projection. It identifies direction in reader-facing text. It does not walk transitive impact or replace Gantt/CPM analysis.

**Rejected**: Recompute dependencies in browser code, infer edge validity, or conflate a successor chain with direct impact.

## Decision: Keep List/Kanban interaction state in the existing browser session

**Evidence**: `wwwroot/app.js` is a browser-native IIFE with one UI state object and a central active-view render path. Feature 008 stores only local repository-root/manifest preferences; it does not establish persisted Work interaction state. Official compiled metadata exposes `snapshotId`; `CompilerApplicationState` maintains official and preview results separately and failed imports retain the prior official result.

**Decision**: Add a Work state slice in the current app state. Keep shared filter/scope/search/mode, List expansion, per-view scroll, selected identity, inspector visibility, and focus-return target as separately named concepts. Do not use `localStorage` for Work state. Detect a successful official snapshot identity change using `metadata.snapshotId`; failed attempts and non-authoritative previews must not reset official Work context.

**Why**: The existing app is session-oriented and makes official-source identity explicit. This supports approved persistence semantics without inventing persistence or binding selection across snapshots.

**Rejected**: A browser task database, storing Work filters/selection in localStorage, or resetting Work state on a failed import attempt.

## Decision: Extend the current static UI and preserve specialist parity paths

**Evidence**: The current UI is `wwwroot/index.html`, `app.js`, and `styles.css`; `Program.cs` routes `/api/views/{viewName}`. The present legacy `KanbanProjection` uses fixed state columns, places null state in `UNKNOWN`, sorts cards by ID, and carries WIP limit/usage. It is not a compliant source for new Work semantics without adaptation. The current Gantt row detail/drawer behavior is implemented in `app.js`; selection and close behavior are presently coupled and must be characterized before changing it. Feature 008 leaves WBS/Kanban in Advanced.

**Decision**: Implement List/Kanban in the existing UI and use the new Work projection, not the legacy Kanban projection. Keep existing WBS and Kanban accessible under Advanced through parity verification. Add a shared read-only inspector surface and only decouple Gantt identity/open state as required for approved navigation behavior.

**Why**: The new interaction requires state semantics and counts different from the legacy Kanban; retaining legacy parity aids audit and gives a safe transition boundary.

**Rejected**: Removing legacy tools at launch, leaving two normal Kanban destinations, or changing Gantt schedule/dependency calculations to simplify the Work screen.

## Decision: Use current deterministic project/test artifacts, not private source material

**Evidence**: `tests/fixtures/ideaengineering-real-shaped/README.md` describes a deliberately reduced synthetic fixture safe for public compatibility tests. The repository uses a custom C# executable test harness; `scripts/test.ps1` runs it without restore, while `scripts/verify.ps1` and `scripts/verify-web.ps1` provide broader build/API/static checks. No JavaScript test framework is installed or needed by the established plan.

**Decision**: Use in-memory canonical fixtures to cover state combinations, ambiguous/unknown dates, alert whitelist, search inputs, deterministic ties, and dependency variants; use the existing public-safe fixture only for shape/parity checks. Extend the current C# harness and browser/static regression tests; do not add packages or copy the private IDEAEngineering checkout.

**Rejected**: A full reference-repository fixture, new browser test dependency, package installation, or reliance on personal/company paths.

## Remaining technical verification during implementation

These are bounded verification points, not unresolved product decisions:

1. Characterize which `GET /api/views` consumers deserialize/view the aggregate and confirm additive `work` serialization remains compatible; preserve the current `NO_PROJECT` response on the named route.
2. Verify all Delivery Cards resolve to valid canonical phase and Work Package references at the point the projection is built. Invalid/unresolved hierarchy must remain diagnostic/absent as existing validation dictates; Work must not repair it by guessing.
3. Characterize focus return and selection behavior around Gantt render/re-render before extracting a shared inspector, then lock the current unaffected Gantt behavior with regression checks.
4. Ensure source execution field nulls and invalid-input diagnostics continue to render without zeros or fabricated status. The Work surface consumes canonical import results; it must not expose raw diagnostic prose as attention.

No item above requires a human change to the approved behavior or a new product decision.
