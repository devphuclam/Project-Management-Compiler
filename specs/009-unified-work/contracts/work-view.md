# Contract: Unified Work View

## Endpoints

- `GET /api/views` adds a `work` property to the existing aggregate response.
- `GET /api/views/work` returns the same Work projection independently.
- With no compiled official project, the named view follows existing view behavior and returns `404` with code `NO_PROJECT`.
- The view is read-only and is derived from the active official compiled canonical project and its management analysis. Candidate/uncommitted and XLSX previews do not replace the official Work projection.
- List, Kanban, filter, selection, and inspector actions do not mutate the source or canonical project.

## Projection shape

The following is illustrative JSON. The exact serialized names follow the repository's camel-case JSON configuration and enum converter. Nulls and absent values are meaningful; consumers MUST NOT coerce them to zero or an authored state.

```json
{
  "currentPhaseId": "PH1",
  "phases": [
    { "id": "PH0", "name": "Chuẩn bị", "order": 0, "workPackageIds": ["WP01"] },
    { "id": "PH1", "name": "Triển khai", "order": 1, "workPackageIds": [] }
  ],
  "workPackages": [
    { "id": "WP01", "phaseId": "PH0", "name": "Chuẩn bị hồ sơ", "order": 0, "deliveryCardIds": ["P01"] }
  ],
  "cards": [
    {
      "key": { "kind": "DeliveryCard", "id": "P01" },
      "name": "Xác nhận tài liệu đầu vào",
      "phaseId": "PH0",
      "workPackageId": "WP01",
      "order": { "phase": 0, "workPackage": 0, "card": 0 },
      "plannedStart": "2026-09-18",
      "plannedFinish": "2026-09-21",
      "plannedEffortHours": 4,
      "plannedEffortState": "KNOWN",
      "execution": {
        "recorded": false,
        "state": null,
        "resultState": null,
        "actualStart": null,
        "actualFinish": null,
        "actualEffortHours": null,
        "remainingEffortHours": null,
        "lastUpdatedAt": null
      },
      "roles": [{ "label": "Đầu mối dự án", "person": null }],
      "attention": [],
      "sourceReferences": []
    }
  ]
}
```

The illustrative phase IDs and count are not product contracts. The projection includes every phase in the imported canonical hierarchy. Cards are emitted once in canonical order; hierarchy arrays refer to canonical card IDs and do not create alternate card identities.

`key.kind` plus `key.id` is the sole serialized Delivery Card identity. There is no top-level `id` alias. `key.id` is sufficient for the technical display-secondary ID and ID search; hierarchy references may use canonical IDs.

## Projection rules

### Identity and hierarchy

- Every card key is kind-qualified `DeliveryCard` identity using the existing `CanonicalWorkItemKey` semantics. Phase and Work Package references use canonical IDs.
- `cards` is the only Work item payload collection. List and Kanban MUST use the same card objects/identities; neither may independently synthesize or re-key a card.
- Milestones, decision points, phases, and Work Packages are not Delivery Card payloads. Hierarchy ancestors are rendered only as List context and do not count as work items.
- Keep canonical order fields/arrays. For Kanban presentation, place the known current phase first, then other phases in canonical order; within each, order Work Packages and cards canonically. Resolve equal or unavailable positions by stable ID ordinal ascending. This changes order only, never scope or membership.

### Phase focus and scope

- `currentPhaseId` is non-null only when the official source reporting date lies in exactly one phase having valid planned start and finish dates. Missing/ambiguous dates yield null; no phase is inferred from array position, current workstation date, or authored state.
- Work opens with All phases scope. The current phase can be opened/focused without excluding other phases. Only an explicit phase selection changes shared scope.

### Execution evidence, baseline, and zero

- `execution.recorded` indicates whether `ExecutionTruthResolver` returned a recorded execution record. The nullable `execution.state` remains the effective authored state; missing state is not defaulted to `NOT_STARTED`.
- A card with a null effective state belongs to `Chưa ghi nhận`, regardless of whether any separate evidence field exists. It never belongs to `Chưa bắt đầu` unless that state is explicitly authored.
- Baseline planned dates/effort are separate from actual/remaining execution evidence. `plannedFinish` is a baseline finish and MUST be presented as `Kết thúc kế hoạch` or equivalent, not a due date.
- Actual values are exposed only when supported by a recorded execution record. Null means no value is available; numeric zero is preserved as zero. Do not convert missing, unknown, invalid, blocked, unresolved, or not-run evidence into zero. Do not invent field-level source states not represented by the authoritative imported model.
- Reader-facing roles use the existing reader-facing role mapping where supported and the column label `Đầu mối / vai trò`; `person` is non-null only for an authoritative concrete person mapping. An unresolved/unknown role remains role/source evidence and a role code alone does not identify a person.
- Provenance stays available for inspection but secondary in normal rows. Raw source paths, diagnostics, SHA, and machine codes are not primary Work labels.

### Search and shared filters

- Search is applied locally and case-/Vietnamese-diacritic-insensitively to the reader-facing Delivery Card name, stable ID, parent Phase name, and parent Work Package name only.
- A phase/package match makes descendant cards candidates; explicit phase scope and all other active filters still apply.
- Search MUST NOT match source paths, raw diagnostics, proposal text, or unrelated provenance.
- Shared phase scope, query, authored-state filter, Needs Attention, explicit unrecorded filter, and active Work mode survive List/Kanban switching. List expansion is List-only.
- With no result filter active, render the approved in-scope hierarchy, including its applicable collapsed headings. With a result filter active, render only ancestor paths containing at least one surviving card. Ancestors are not results/counts. Search may reveal a matching path temporarily; clearing search restores prior expansion state.

### Kanban grouping and counts

- Authored state groups use `NOT_STARTED`, `IN_PROGRESS`, and `COMPLETED`; suspended/cancelled remain compact separately counted groups. Null effective state is a distinct separately counted `Chưa ghi nhận` area.
- Apply phase scope, query, authored-state filter, attention filter, and explicit unrecorded filter to the shared card collection before grouping and counting.
- Each card contributes to no more than one state group. Group counts are group-specific, not whole-project totals. All group headings and counts remain visible at zero; collapsed and unrecorded counts follow the same post-filter calculation.
- A state filter may therefore reduce another group's count to zero; do not show pre-filter counts alongside filtered cards.
- On narrow Kanban, show one labeled state group at a time while keeping the counts of all groups visible.

### Needs Attention

- A card matches only if an existing structured `ManagementAnalysis` alert targets its canonical Delivery Card identity and its code is exactly `START_DELAY`, `OVERDUE`, `SUSPENDED`, or `AT_RISK`.
- Each serialized `attention[]` entry has exactly this minimal shape:

  ```json
  { "code": "START_DELAY", "consequence": "Chưa bắt đầu đúng kế hoạch." }
  ```

- The only permitted codes and existing reader-facing consequences are:

  | `code` | `consequence` |
  |---|---|
  | `START_DELAY` | `Chưa bắt đầu đúng kế hoạch.` |
  | `OVERDUE` | `Đang kéo dài quá ngày dự kiến.` |
  | `SUSPENDED` | `Đang tạm dừng.` |
  | `AT_RISK` | `Có nguy cơ chậm do công việc trước bị trễ.` |

- If multiple entries are present, order by `code` ordinal ascending, then source `DerivedAt` ascending, then the ordinal-sorted `ReasonWorkItemIds` sequence. `consequence` is the primary UI copy. `code` is a technical signal/filter identifier only; do not present it or raw analysis `Message` as primary UI text.
- Do not include `COMPLETED_LATE`, `COMPLETED_ON_TIME`, `CANCELLED`, `BLOCKED`, arbitrary warnings, readiness/gate/decision/human-action evidence, import diagnostics, governance signals, or unlisted codes.
- Attention remains a derived signal separate from authored state, baseline, and actuals. An empty result means no supported matching signal, not guaranteed risk-free work.

### Shared inspector and dependencies

- Selecting a card from List, Kanban, or Gantt resolves the same kind-qualified identity and opens the same read-oriented Delivery Card detail shell.
- The browser composes details from `work.cards` plus existing Gantt/source/provenance and `dependencyNetwork` projections. Other selectable hierarchy/control kinds use only their existing evidence-backed fields and omit Delivery Card-only semantics.
- Primary `Phụ thuộc vào` / `Ảnh hưởng trực tiếp đến` links use only existing typed dependency-network edges where `IncludedInAnalysis == true` (or the exact equivalent predicate already authoritative in the current Gantt/management path), and use the existing node labels. The current `IncludedInAnalysis` contract excludes invalid-source-evidence edges, unsupported dependency types, Work Package traceability-only edges, missing endpoints, and edges otherwise not eligible for analysis. Retained-but-excluded edges MUST NOT be promoted to primary dependency claims. If exposed for audit/provenance, they remain Advanced evidence and are clearly distinguished as excluded. No transitive impact is inferred.
- The inspector cannot edit baseline, record actuals, modify source, create proposals, or change Gantt calculations. Existing proposal actions remain in their existing Advanced path.

### Selection, focus, scroll, and Gantt navigation

- Selected item identity persists independently of inspector visibility, keyboard focus, and scroll while the same official `snapshotId` is active.
- Closing with Escape or Close returns focus to the invoking item if still rendered/enabled; otherwise it returns to the current heading or relevant scope/search control. Do not widen filters or expand ancestors just to restore focus.
- Each Work mode has its own scroll position. Restore only when scope/search/filters match the saved view criteria; otherwise scroll a selected matching item into view or start at current results as FR-032 defines.
- Work → Gantt carries typed card identity, reveals/focuses the Gantt item and visible ancestors when available, and preserves whether the inspector was open. Gantt → Work keeps Work state and explains if active controls exclude the target; it does not silently clear filters.
- A different successfully active official snapshot clears previous selected-item/inspector context. Failed imports and candidate previews do not reset the official context.

## Compatibility and non-goals

- The aggregate API addition is additive; the named Work route follows current route/error conventions. Existing Overview, Gantt, WBS, Kanban, CPM, dependency, and management-control behavior remains unchanged except for required shared-inspector/selection integration that preserves the Gantt contract.
- No persistence schema, migration, database, new work/task model, source write-back, proposal editing, Work Package progress, Work WIP policy, alert producer, or Gantt calculation is introduced.
- Legacy WBS and Kanban stay reachable in Advanced until FR-040/FR-041 parity is documented. This endpoint does not authorize retiring them.
