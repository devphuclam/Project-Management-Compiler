# Implementation Plan: Unified Work

**Branch**: `codex/feature009-unified-work-plan` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Review Status**: Human-approved for task generation (2026-09-29).

**Input**: Human-approved Feature 009 specification, approved for planning at main commit `f2d885cd710c7658dcbf65d295b6f57a10cdff33`.

## Summary

Add one read-only Work projection over the existing canonical Phase → Work Package → Delivery Card hierarchy and expose it through the current management-view composition and `/api/views` routes. The static browser UI will render List and Kanban from that same Delivery Card collection, with shared in-memory scope/filter/search/selection state and a shared evidence-backed inspector. Existing execution resolution, management-analysis alerts, dependency projection, source references, and Gantt identity remain authoritative. No task store, new source authority, write-back, proposal workflow, WIP policy, or scheduling engine is introduced.

## Technical Context

**Language/Version**: C# on .NET 10.0; browser-native JavaScript, HTML, and CSS.

**Primary Dependencies**: Existing ASP.NET Core shared framework and repository code only; no new packages, frontend framework, or test framework.

**Storage**: Existing in-memory compiled official snapshot and derived view projections. Work interaction state is in-memory in the browser session; no Work-specific persistence or database.

**Testing**: Existing C# executable harness at `tests/ProjectManagementCompiler.Tests`; `scripts/test.ps1`, `scripts/verify.ps1`, and the live/static checks in `scripts/verify-web.ps1`; manual browser checks through `Run Project.cmd`.

**Target Platform**: Existing local Windows-hosted ASP.NET application in a browser; desktop and narrow layouts, including 360px and 1280px verification widths.

**Project Type**: Existing single-project local web application.

**Performance Goals**: No new performance SLA. Work projection remains bounded by the already loaded canonical project and its management analysis; search/filter/grouping is local over that projection.

**Constraints**: Approved specification is authoritative; preserve official/candidate/preview boundaries, immutable baseline, execution evidence and unknowns, deterministic ordering, and Gantt semantics. No source writes, package installation, network dependency, private IDEAEngineering fixture, credential, or secret.

**Scale/Scope**: Arbitrary imported canonical phase count. Existing public-safe real-shaped fixture is useful for hierarchy parity but its present counts are not product contracts.

## Constitution Check

### Before Phase 0

| Principle | Gate | Evidence / rationale |
|---|---|---|
| I. Canonical model before views | PASS | Work is an additive Management projection of `CanonicalProject`; List and Kanban consume one projected card identity set. No parallel work domain or source-specific UI model. |
| II. Baseline and evidence are first-class | PASS | Planned baseline, recorded execution, derived attention, and provenance remain separate. Missing execution state is not converted to `NOT_STARTED` or zero. |
| III. Deterministic extraction | PASS | No extraction, AI inference, or business-data synthesis is added. Projection and ordering use existing canonical/analysis inputs and explicit stable tie-breaks. |
| IV. Test through deep interfaces | PASS | Projector and alert-consumption tests cross the Management projection seam; UI/API checks verify the aggregate and named view contract. |
| V. Restricted-environment delivery | PASS | No container, installation, registry, external service, or new dependency is planned. Public-safe fixtures only. |
| VI. Explicit scope and safe failure | PASS | Feature boundaries are explicit; unsupported state, role, attention, dependency, and evidence data stay unknown/absent rather than guessed. |
| Design/specification before implementation | PASS | Feature 009 spec is human-approved for planning. This plan is on a separate review branch; no implementation or tasks are included. |

No constitution violation requires complexity justification.

### Post-design re-check

All gates remain PASS. The design extends existing canonical and view seams; state is presentation-only and in-memory; no source authority, persisted model, dependency semantics, Gantt schedule, or proposal capability is changed. The detailed projection and contract below preserve approved unknown/zero, identity, filtering, ordering, and parity boundaries.

## Project Structure

### Planning artifacts

```text
specs/009-unified-work/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    └── work-view.md
```

`tasks.md` is intentionally not created in the planning stage.

### Source code (planned implementation touchpoints)

```text
src/ProjectManagementCompiler/
├── Management/
│   ├── ManagementViewProjector.cs       # add Work to the existing aggregate
│   ├── WorkProjector.cs                 # focused canonical-to-Work projection
│   ├── WorkProjection.cs                # minimal read model for hierarchy/cards
│   ├── ProjectOverviewProjector.cs       # share current-phase resolution if needed
│   └── ...                              # existing execution, analysis, dependency, text policies
├── Program.cs                           # additive named /api/views/work route
└── wwwroot/
    ├── app.js                           # Work state, List/Kanban, shared inspector/navigation
    ├── index.html                        # primary navigation and Work controls
    └── styles.css                        # responsive/accessibility presentation

tests/ProjectManagementCompiler.Tests/
├── Program.cs                            # register focused test methods in existing harness
├── WorkProjectionTests.cs                # canonical projection/state/order/attention tests
├── ManagementUiShellTests.cs             # extend primary nav and Work shell contract checks
└── ...                                   # existing Gantt, analysis, API/static UI regression tests
```

**Structure Decision**: Extend the existing ASP.NET application, `ManagementViewSet`, browser shell, and C# executable harness in place. Add the smallest Work read model/projector and focused tests that the existing boundaries require. Do not create a new project, frontend module system, database, generic task abstraction, or copied source fixture. Whether the current-phase rule needs a shared resolver is a narrow implementation detail: preserve the existing unique-valid-date rule and Overview output exactly if extracting it.

## Architecture and Ownership

1. **Canonical Work projection**: Add a read-only `WorkProjection` built from the canonical project and current `ManagementAnalysis`. Its hierarchy contains canonical phases and work packages, and its one flat Delivery Card collection carries kind-qualified `CanonicalWorkItemKey` identity and only fields needed for List, Kanban, inspector, search, and Gantt linking. Milestones/decision points remain outside the Work collection. Keep canonical arrays/source order available for deterministic hierarchy ordering.
2. **Execution/state authority**: Resolve effective card execution through `ExecutionTruthResolver.ForCard`. Preserve record availability separately from nullable authored `ExecutionState`; cards with no effective state enter `Chưa ghi nhận`, never `Chưa bắt đầu`. Actual values are emitted only when supported by recorded execution evidence; nullable/unknown stays null, while a known numeric zero remains zero. Reuse planned fields and their `DataState` from canonical entities; do not calculate card or Work Package progress.
3. **Current phase, attention, and roles**: Determine current phase only from the official reporting date and the existing rule: exactly one phase with valid planned dates containing that date. Carry its canonical ID in the Work projection for focus/order; retain All phases scope. Work attention is a filtered view of existing `ManagementAnalysis.Alerts`: target must resolve to a Delivery Card and code must be exactly `START_DELAY`, `OVERDUE`, `SUSPENDED`, or `AT_RISK`. Reuse the existing supported consequence mapping where possible; never feed diagnostics, readiness, governance, or arbitrary warnings into the filter. Present source-backed logical role assignments using the existing reader-facing role mapping in `ExecutiveProgressReportProjector`; if a shared mapper is needed, extract it with regression coverage so existing report wording is unchanged. A logical role is never presented as a named person.
4. **Existing view composition and API**: Add `Work` to `ManagementViewSet` and `ManagementViewProjector.Build`; expose it in `GET /api/views` and the existing named switch as `GET /api/views/work`. Keep existing no-project behavior (`404 NO_PROJECT`). The browser continues using the compiled official aggregate; do not add a Work-specific import/service/store. Shared inspector resolves supported direct dependencies from the existing dependency network and other selectable kinds from existing canonical-backed view projections.
5. **Shared List/Kanban collection**: Derive both views from the same flat projected card collection. Apply active shared phase scope, normalized query, authored-state selection, Needs Attention, and explicit unrecorded selection once to get `filteredCards`. List renders hierarchy paths for survivors; Kanban groups those same survivors, and all visible counts (including zero and collapsed groups) are computed after all filters. Preserve separate compact suspended/cancelled groups and the distinct counted unrecorded area.
6. **Stable order/search**: Use canonical hierarchy/source positions for phase, Work Package, and card order. Present the known current phase first for Kanban, then remaining phases in canonical order; within each phase, Work Packages and cards follow canonical order. Compare stable IDs ordinal ascending as a final tie-break for equal or missing positions. Never depend on browser insertion order or unstable sorting. Search is local and case-/Vietnamese-diacritic-insensitive over only the approved reader-facing card name, stable ID, and parent phase/package names.
7. **Browser state ownership**: Extend the existing single frontend state object with a Work state slice. Shared choices (phase scope, query, state/attention/unrecorded filters, List/Kanban mode) survive view switches; list expansion and each view's scroll are view-local. Keep selected canonical identity separate from inspector-open state, invoking keyboard focus, and scroll. Do not persist Work state in `localStorage`. Reset selection/inspector only when the successful active official snapshot identity changes; failed import attempts and candidate previews must not replace/reset official Work context.
8. **Shared inspector/Gantt**: Use one read-oriented inspector shell for a Delivery Card selected from List, Kanban, or Gantt; render only backed fields and supported direct directional dependencies. For primary Work dependency claims, consume only existing dependency-network edges with `IncludedInAnalysis == true` (or the exact equivalent eligibility predicate already authoritative in the current Gantt/management path). Excluded invalid-source-evidence, unsupported-type, Work Package traceability-only, missing-endpoint, or otherwise non-analysis-eligible edges MUST NOT be presented as authoritative `Phụ thuộc vào` / `Ảnh hưởng trực tiếp đến` relationships. If surfaced for audit/provenance, keep them in Advanced evidence and label them as excluded. Do not add transitive impact. Kind-specific non-card variants omit Delivery Card-only fields. Preserve the Gantt engine, schedule, dependency display/calculations, and specialist tools. Decouple Gantt selection identity from drawer-open state as needed so Work ↔ Gantt navigation preserves the inspector's previous open/closed condition. Reverse navigation keeps Work filters and explains when the target is excluded.
9. **Responsive/accessibility and legacy transition**: Keep the current native HTML/CSS/JS implementation. Desktop uses the existing workspace shell with a shared inspector surface; narrow Kanban shows one labeled state group at a time with all counts, and the inspector becomes full-screen. Implement focus-visible/keyboard behavior with native controls; no drag-only actions. Keep legacy WBS and Kanban in Advanced until their distinct parity gates are evidenced; do not remove either as part of the initial implementation slice.

## Reuse vs. New Components

| Concern | Reuse | Minimal addition or change |
|---|---|---|
| Source identity/hierarchy | `CanonicalProject`, `Phase`, `WorkPackage`, `DeliveryCard`, canonical deterministic arrays | Work-specific read projection; no new domain entities/store |
| Item identity | `CanonicalWorkItemKey` | Use its DeliveryCard factory and kind+ID across Work/Gantt |
| Effective execution | `ExecutionTruthResolver` | Expose recorded/unrecorded and nullable state without fallback inference |
| Attention | `ManagementAnalysis.Alerts`, `StatusAnalyzer`, `ProjectOverviewProjector` consequence mapping | Work allowlist consumption; centralize shared mapping only if required, with unchanged Overview behavior |
| Dependencies | Existing `DependencyNetworkProjection` and canonical dependency keys | Primary inspector uses only edges with `IncludedInAnalysis == true`; excluded edges stay out of primary claims and may only appear as clearly distinguished Advanced audit evidence |
| Reader-facing content/provenance/roles | `ReaderFacingTextPolicy`, role-label mapping in `ExecutiveProgressReportProjector`, `SourceReference`, existing Gantt/source projections | Share role mapping only if needed and preserve report output; Work card fields only where existing projections do not provide the approved contract |
| App/API composition | `ManagementViewProjector`, `ManagementViewSet`, `Program.cs` routes, current `GET /api/views` consumer | Add Work property and named route |
| UI state and shell | Existing `app.js` state/render pipeline, navigation shell, CSS, Gantt focus behavior | Work state slice, List/Kanban renderers, shared inspector shell and cross-view bridge |
| Tests | Existing console test runner and public-safe real-shaped fixture | Focused Work projection tests and extensions to current shell/API/Gantt regressions |

## Planned Interfaces

- `WorkProjection` is an additive property on `ManagementViewSet`; it contains current phase identity (nullable), canonical hierarchy ordering data, and one Delivery Card collection used by both Work views.
- `GET /api/views/work` returns the same Work projection included in `GET /api/views`; with no compiled project, it follows existing view behavior and returns `404` / `NO_PROJECT`.
- The Work projection exposes stable typed identity, parent phase/package identity and display name, reader-facing name, canonical order, effective nullable state plus evidence/recording availability, planned baseline fields, backed execution fields, logical role labels, supported attention consequences, and provenance. Exact serialization fields are specified in [contracts/work-view.md](contracts/work-view.md).
- Dependency detail is resolved from the existing aggregate's `dependencyNetwork` and uses only edges with `IncludedInAnalysis == true`, with typed endpoints and node labels. Excluded edges are not primary dependency claims; any audit exposure remains Advanced and clearly marked excluded. No new dependency calculation endpoint or transitive-impact field is introduced.
- UI-only scope, search, filters, active List/Kanban mode, expansion, scroll, selected identity, inspector visibility, and focus-return target remain browser-session state and are not part of the API or persisted data.

## Phase 0: Research Decisions

Decisions and repository evidence are captured in [research.md](research.md). Key resolved choices are to extend the existing management projection, derive both Work modes from one canonical card collection, reuse the execution and alert authorities, and keep all Work interaction state in memory. No material product/domain uncertainty remains; any missing source evidence stays absent/unknown.

## Phase 1: Design Artifacts

- [data-model.md](data-model.md) defines the projected hierarchy/card collection and the separation between authored state, record availability, baseline, actual evidence, attention, selection, focus, and scroll.
- [contracts/work-view.md](contracts/work-view.md) defines the additive aggregate/named endpoint, projection shape, filtering/order inputs, unknown semantics, and compatibility boundaries.
- [quickstart.md](quickstart.md) defines implementation-time focused tests, repository verification, manual UI checks, and public-fixture safety.

### Post-design constitution re-check

All principles remain PASS. Work stays a derived view over one canonical project. Work filters do not mutate domain state; official execution evidence and management-analysis alerts are consumed without reinterpretation; all nondeterministic ordering is explicitly resolved; no dependency or persistence layer is added. Feature 008 routes and Gantt behavior remain compatible, and legacy tools remain available pending evidence-based parity.

## Implementation Sequencing Direction

The future task graph should use vertical, test-first slices in this dependency order; this is sequencing guidance for Spec Kit planning, not an implementation task list:

1. Characterize existing official snapshot identity, canonical ordering, execution truth, alert mapping, aggregate/named view behavior, and Gantt selection/inspector state.
2. Add and test the minimal Work projection, including hierarchy/card identity, all supported execution states plus unrecorded/null, baseline/evidence distinctions, deterministic order, roles/provenance, and exact attention whitelist.
3. Expose the additive projection through aggregate and named API views; verify no-project and existing view/Gantt regressions.
4. Add shared browser Work state and List projection; prove All phases/current-phase focus, search/filter ancestor visibility, selection/focus/scroll separation, and empty/recovery states.
5. Add Kanban over the same filtered card set; prove authored/unrecorded groups, post-filter counts, deterministic ordering, responsive group selection, and shared state with List.
6. Add shared inspector and bidirectional Work/Gantt identity navigation; verify detail parity, that only analysis-included direct dependency edges appear as primary directional links (excluded edges and transitive impact do not), open/closed preservation, and unchanged Gantt behavior.
7. Complete keyboard/responsive handling and legacy parity evidence. Keep WBS and old Kanban under Advanced until the explicit FR-040/FR-041 gates pass.
8. Run focused tests, `scripts/test.ps1`, `scripts/verify.ps1`, live `scripts/verify-web.ps1` where its local-port precondition is satisfied, manual browser verification at 360px/1280px, and public-repository hygiene review before an implementation increment is considered complete.

## Complexity Tracking

No constitution violations. No additional project, package, persistent store, new authority, or alternate frontend architecture is justified or planned.
