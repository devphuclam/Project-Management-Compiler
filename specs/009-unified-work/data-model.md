# Unified Work Projection and UI State

This document defines the minimal read model and transient browser state needed to implement the approved Feature 009 behavior. It adds no domain task entity, source authority, persistence, or write path.

## Authority and derivation

```text
CanonicalProject ───────┐
                        ├─ WorkProjector ─ WorkProjection ─┬─ List
ManagementAnalysis ─────┘                                  └─ Kanban
        │
        ├─ existing dependency network ─ shared inspector direct links
        └─ existing Gantt projection ───── shared inspector / cross-view selection
```

`WorkProjection` is a derived presentation model owned by `Management`. The canonical model remains authoritative for phase/package/card identity, planning fields, role assignments, and provenance. `ExecutionTruthResolver` remains authoritative for effective execution evidence. `ManagementAnalysis.Alerts` remains authoritative for supported derived attention. Existing dependency and Gantt projections remain authoritative for their current network and schedule semantics.

## Read projection

### `WorkProjection`

| Field | Meaning | Truth rule |
|---|---|---|
| `currentPhaseId` | Stable canonical identity of the unique current Phase, or null | Derived only from official reporting date and exactly one valid containing phase; not a scope filter. |
| `phases` | Imported Phase identity, reader-facing name, canonical order, and ordered Work Package references | Include phases from the imported canonical hierarchy; never assume a fixed count. |
| `workPackages` | Work Package identity, parent Phase identity, reader-facing name, canonical order, and ordered Delivery Card references | Existing canonical hierarchy only; no package progress or rollup state. |
| `cards` | The one flat Delivery Card collection consumed by List and Kanban | Exactly one projected item per canonical Delivery Card. No milestone, decision point, phase-rollup, or package-rollup cards. |

### `WorkCard`

| Field | Meaning | Truth rule |
|---|---|---|
| `key.kind`, `key.id` | Typed stable identity | `DeliveryCard` plus canonical card ID; selection and view joins use both. |
| `id`, `name` | Stable technical ID and cleaned reader-facing name | Name leads in UI; ID remains secondary/searchable. Clean via existing reader-facing text policy. |
| `phaseId`, `workPackageId` | Canonical parent identities | Link to hierarchy references; do not infer a missing parent. |
| canonical order | Source/canonical ordering information | Used for deterministic ordering; no browser insertion order. A stable ordinal ID comparison resolves equal or absent positions. |
| `plannedStart`, `plannedFinish`, `plannedEffortHours`, applicable planned data state | Immutable baseline values | Planned finish is never recast as a due date. A null or non-known state does not become zero. |
| execution-record availability | Whether a recorded effective execution record exists | Derived through `ExecutionTruthResolver.ForCard`; source-mode absence does not fall back to legacy overlay. |
| `executionState` | Nullable effective authored state | Null remains null and goes to the separate `Chưa ghi nhận` group. Do not infer from baseline, alerts, or readiness. |
| `resultState`, actual dates, actual effort, remaining effort, update time, supported execution evidence | Backed execution details when the record is recorded and the field exists | Null remains absent/unknown; numeric zero remains numeric zero. Do not emit values from an unrecorded record. Do not manufacture a per-field state absent from the authoritative imported record. |
| `roles` | Source-backed logical role labels and, only if authoritatively mapped, a concrete person identity | Reader-facing column label is `Đầu mối / vai trò`; reuse the existing executive-report role-label mapping if applicable. An unresolved or unknown role stays a role/source value, never a named person. |
| `attention` | Supported reader-facing attention consequences for the card | Only alerts targeted to this canonical card with code in the approved exact whitelist. No warnings/readiness/governance/diagnostics. |
| `sourceReferences` | Existing provenance/source references | Preserve existing references; keep technical provenance secondary to reader-facing work details. |

The projection may add a narrowly required field only when an approved FR cannot be represented from the listed fields or the existing shared projections. Any such addition remains derived and read-only; it must not become a second source of truth.

## Derived collections and view rules

- **List** traverses `phases` → `workPackages` → `cards` using identity references and displays card payloads from `cards`. Ancestor rows are context and never inflate item counts.
- **Kanban** groups the same filtered `cards`. Each card belongs to exactly one authored-state group when state is present, or the separate unrecorded group when effective state is null. Suspended/cancelled remain compact authored groups.
- One shared filter function produces the post-filter Delivery Card set for both views. Group counts are computed from that set and stay visible at zero.
- Canonical hierarchy order is retained by phase, package, and card positions. Kanban places the known current phase first as presentation-only; remaining phases follow canonical order, then package/card order, with ordinal ID tie-break. This never changes membership.
- Needs Attention is a separate derived boolean/reason collection, not an authored state. Eligible alert codes: `START_DELAY`, `OVERDUE`, `SUSPENDED`, `AT_RISK` only.
- Direct predecessor/successor relationships are obtained from existing `DependencyNetworkProjection` edges and node labels. No transitive impact is added to Work.

## Browser-session state

All state below is transient and belongs to the existing frontend state object. It is not sent as a mutation and is not persisted in browser storage.

| State | Scope | Rule |
|---|---|---|
| active Work mode | Shared Work view | List by default; switching List/Kanban does not clear other shared choices. |
| phase scope | Shared Work view | Defaults to all canonical phases; current-phase focus does not narrow it. Explicit single-phase scope applies to both modes. |
| query / authored-state / attention / unrecorded filters | Shared Work view | Apply identically to the one card collection in both modes. |
| List expansion state | List only | Preserve independently from filters; search may temporarily reveal needed paths and restore prior expansion after clearing. |
| scroll position | Per mode | Restore only when shared criteria match prior view state; otherwise follow selected-item/results-start behavior in FR-032. |
| selected item key | Workspace | Typed canonical identity; retained while the same official snapshot remains active, even if inspector closes or filters hide it. |
| inspector open state | Workspace/view integration | Boolean distinct from selected identity; navigation preserves the prior open/closed condition. |
| focus-return target | Transient interaction | Invoking rendered/enabled item when possible; otherwise the approved view heading or relevant control. It is not selection. |
| active official snapshot ID | Workspace boundary | Compare successful official snapshot identity. Clear selection and inspector context only when it changes. A failed import attempt or candidate preview must not reset official state. |

## Explicitly not modeled here

- User-authored tasks, editable statuses, a Work Package progress value, or local work database.
- Actual/proposal editing, source write-back, or proposal lifecycle state.
- A new alert/risk model, readiness/governance merge, or transitive dependency graph.
- Persisted filter preferences, per-user identity, or project setup workflow.
- A new Gantt calculation or scheduling representation.
