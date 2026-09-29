# Feature Specification: Unified Work

**Feature Branch**: `codex/feature009-unified-work-spec`

**Created**: 2026-09-29

**Status**: Approved for planning — user approval received 2026-09-29

**Input**: Human-approved Unified Work product/UX design proposal dated 2026-09-29, clarification register UW-01…UW-18, the approved Management UI redesign, Feature 008 workspace-foundation boundary, and existing canonical/source and management-analysis contracts.

## Authority and scope

This specification converts the approved design into observable behavior. It does not reopen product-design decisions. The 2026-09-29 design review accepted every `PROPOSED DESIGN DECISION` in the reviewed design for Increment 2. The approved design and its UW-01…UW-18 decisions govern the Work experience; this specification makes them testable without changing their meaning.

Canonical identity, hierarchy, source authority, authored state, baseline values, actual evidence, provenance, and derived analysis remain governed by the existing contracts listed under **Contract and authority references**. Work is a read-oriented projection over one imported official project; it is not a second planning or task store.

Feature 008 remains the boundary for the unloaded/open-project experience, official import, primary Overview/Gantt foundation, and existing Gantt behavior. Feature 009 adds the completed Work destination to a loaded workspace. It does not alter import authority or Gantt calculations.

Where the earlier Management UI redesign mentions a fixed six-phase illustration or a current-phase Kanban default, the later approved Unified Work design and UW-03 govern this increment: phase count is data-driven, both List and Kanban default to All phases, and current phase is focus/order only unless explicitly selected as scope.

### Contract and authority references

- Approved product direction: [`docs/superpowers/specs/2026-09-28-management-ui-redesign-design.md`](../../docs/superpowers/specs/2026-09-28-management-ui-redesign-design.md).
- Approved Increment 2 design: [`docs/superpowers/specs/2026-09-29-unified-work-product-ux-design-proposal.md`](../../docs/superpowers/specs/2026-09-29-unified-work-product-ux-design-proposal.md).
- Workspace foundation boundary: [`specs/008-management-ui-foundation/spec.md`](../008-management-ui-foundation/spec.md) and [`specs/008-management-ui-foundation/contracts/overview-view.md`](../008-management-ui-foundation/contracts/overview-view.md).
- Canonical hierarchy, authored state, source ordering, execution overlay, alerts, and provenance: [`specs/001-mvp1-project-management-compiler/spec.md`](../001-mvp1-project-management-compiler/spec.md), [`specs/001-mvp1-project-management-compiler/data-model.md`](../001-mvp1-project-management-compiler/data-model.md), and [`specs/001-mvp1-project-management-compiler/contracts/canonical-json.md`](../001-mvp1-project-management-compiler/contracts/canonical-json.md).
- Readiness/governance evidence separation: [`specs/002-mvp2-readiness-evidence-adapter/contracts/management-evidence.md`](../002-mvp2-readiness-evidence-adapter/contracts/management-evidence.md).
- Existing producer and supported reader-facing consequence mapping used to resolve the Work attention whitelist: `StatusAnalyzer` and `ProjectOverviewProjector` in `src/ProjectManagementCompiler/Management/`. These establish only the signals already produced and mapped today; they do not authorize new analysis.

### Needs Attention: resolved eligible-signal whitelist

The Work filter MUST match only a structured existing `ManagementAnalysis` alert whose `WorkItemId` resolves to a canonical `DeliveryCard`, and whose `AlertCode` is one of:

| Eligible code | Existing supported meaning | Contract/source basis |
|---|---|---|
| `START_DELAY` | Work has not started by its baseline planned start, or recorded start is later than planned. | Produced by existing management analysis; supported consequence mapping in the existing Overview projector. |
| `OVERDUE` | Recorded in-progress work extends beyond baseline planned finish. | Produced by existing management analysis; supported consequence mapping in the existing Overview projector. |
| `SUSPENDED` | The Delivery Card has an authored suspended state and corresponding structured alert. | Produced by existing management analysis; supported consequence mapping in the existing Overview projector. |
| `AT_RISK` | A not-started successor has an eligible, evidenced card-to-card Finish-to-Start predecessor that is delayed. | Produced conservatively by existing management analysis; supported consequence mapping in the existing Overview projector. |

This whitelist is the intersection of alerts emitted by the existing `ManagementAnalysis` producer and alert types with an existing concise reader-facing consequence mapping. The code is a contract identifier, not UI copy. The UI MUST use readable consequence text and MUST NOT expose raw alert messages or machine codes as primary labels.

The following are explicitly outside the Work filter: `COMPLETED_LATE`, `COMPLETED_ON_TIME`, and `CANCELLED` (emitted by analysis but not in the existing supported consequence map); `BLOCKED` and readiness/gate/decision/human-action evidence (not supported Work alert types and, where present, separate management evidence); diagnostics, import/source warnings, arbitrary warnings, governance/readiness concerns, and any unlisted or unattributed signal. `SUSPENDED` remains an authored state/group as well as an eligible attention signal; those are separate dimensions. If no eligible signal is present, Needs Attention returns no matching cards and MUST NOT imply that the project is risk-free.

## User Scenarios & Testing

### User Story 1 — Find project work in its hierarchy (Priority: P1)

As a project manager, I want one Work destination showing the imported Phase → Work Package → Delivery Card hierarchy, so that I can locate a piece of work without reconciling separate task lists.

**Why this priority**: A coherent, identity-preserving work collection is the foundation for every other Unified Work interaction.

**Independent Test**: Load an official project containing phases, packages, delivery cards, and control points. Verify primary navigation, List default, phase focus, hierarchy, explicit phase scoping, and exclusion of control points from the work collection.

**Acceptance Scenarios**:

1. **Given** an official project is loaded, **when** the workspace is displayed, **then** primary navigation is `Tổng quan | Công việc | Gantt`, and selecting `Công việc` opens List.
2. **Given** the imported canonical hierarchy contains any number of phases, **when** Work first opens, **then** its scope is `Tất cả giai đoạn`, all canonical phases remain included, and the uniquely determined current phase is opened/focused without becoming an implicit phase filter.
3. **Given** the official reporting date or valid planned dates do not identify exactly one current phase, **when** Work first opens, **then** it preserves All phases and explicitly avoids inventing a current phase or focus.
4. **Given** the user explicitly selects one phase, **when** List or Kanban is displayed, **then** both views use that same phase scope until the user changes it.
5. **Given** a project contains milestones or decision points, **when** the Work collection is displayed, **then** those control points are not Delivery Card rows or Kanban cards and remain available in their existing Overview/Gantt contexts.
6. **Given** the Work destination is used, **when** its items are compared with the canonical project and Gantt, **then** each Delivery Card retains the same canonical kind and stable identity and no independent Work/task record is created.

### User Story 2 — Understand recorded state and find a card on Kanban (Priority: P1)

As a project manager, I want cards grouped by recorded execution state while missing state remains clearly separate, so that I do not mistake missing evidence for a confirmed status.

**Why this priority**: State misrepresentation would create false progress and undermine trust in the entire workspace.

**Independent Test**: Use a canonical fixture with each authored state, a null/unrecorded state, eligible and ineligible alerts, dependencies, and multiple phases. Compare List, Kanban, and canonical records after each filter combination.

**Acceptance Scenarios**:

1. **Given** cards have authored `NOT_STARTED`, `IN_PROGRESS`, or `COMPLETED` states, **when** Kanban is shown, **then** they appear respectively under `Chưa bắt đầu`, `Đang làm`, and `Hoàn thành`.
2. **Given** a Delivery Card has no recorded execution state, **when** Kanban is shown, **then** it appears only in the visibly separate `Chưa ghi nhận` area with its own count and never in `Chưa bắt đầu`.
3. **Given** cards have authored `SUSPENDED` or `CANCELLED` states, **when** Kanban is shown, **then** they remain in the compact suspended/cancelled area and their authored grouping is not replaced by attention status.
4. **Given** All phases are in scope, **when** cards are ordered inside any Kanban state group, **then** ordering is deterministic: current phase first when known; remaining phases in canonical hierarchy order; Work Packages in canonical hierarchy order; Delivery Cards in canonical/source order; and stable Delivery Card ID in ordinal ascending order as the tie-break when canonical position is equal or unavailable. The ordering is presentation-only.
5. **Given** phase scope, search, authored-state filter, Needs Attention, or the explicit unrecorded-state filter changes, **when** group counts are shown, **then** each count is the number of Delivery Cards remaining in that group after all active criteria; each card contributes to exactly one state group; ancestors and control points contribute zero; and zero-count group headings remain visible.
6. **Given** the supported Needs Attention whitelist is active, **when** it filters cards, **then** only cards with at least one eligible alert defined above remain; unlisted alert codes, diagnostics, readiness/governance records, and unattached alerts do not match.
7. **Given** an item has an eligible `SUSPENDED` alert, **when** state groups and Needs Attention are considered, **then** it remains in the authored `Tạm dừng` group and may match Needs Attention independently.

### User Story 3 — Narrow the same work collection in List and Kanban (Priority: P1)

As a project manager, I want to search and filter in either Work view without losing my scope or seeing orphaned hierarchy, so that I can find relevant cards quickly and understand where they belong.

**Why this priority**: Search and filters make a multi-phase hierarchy usable without changing what project data means.

**Independent Test**: Apply identical phase, query, state, and attention selections in List and Kanban; verify shared Delivery Card membership, visible ancestor paths, post-filter group counts, and retained view state when switching modes.

**Acceptance Scenarios**:

1. **Given** Work is in List with a phase scope, search query, authored-state filter, and/or Needs Attention selected, **when** the user switches to Kanban and back, **then** those shared Work choices and the result set are unchanged.
2. **Given** a query matches a Delivery Card name or stable ID, **when** List results are shown, **then** the matching card is included if it satisfies the active filters and only its required Phase and Work Package ancestor path is shown.
3. **Given** a query matches a Phase or Work Package name, **when** List results are shown, **then** descendant Delivery Cards are search candidates and only ancestor paths for surviving cards are visible.
4. **Given** active search and filters leave no descendant Delivery Card under an ancestor, **when** List results are shown, **then** that ancestor is not rendered as an orphan result; ancestors are context only and are not counted as cards.
5. **Given** a matching descendant is inside a collapsed path, **when** filtered List results appear, **then** only the path needed to reveal the result is opened; clearing the query restores the expansion state held before search.
6. **Given** filters remove all matching cards, **when** the empty result appears, **then** it distinguishes no cards in the chosen phase from no search/filter matches and provides a relevant clear-filter or phase-scope action.

### User Story 4 — Inspect work and move between Work and Gantt (Priority: P1)

As a project manager or reviewer, I want the same read-oriented inspector and stable item navigation from List, Kanban, and Gantt, so that I can understand plan, actual evidence, dependencies, and provenance without losing my place.

**Why this priority**: Work details are useful only when they preserve identity and distinguish official baseline from actual evidence and analysis.

**Independent Test**: Select the same Delivery Card in List, Kanban, and Gantt; compare identity and evidence-backed fields; close/reopen the inspector with keyboard and pointer; alter filters while an item remains selected; and navigate in both directions between Work and Gantt.

**Acceptance Scenarios**:

1. **Given** a Delivery Card is selected in List, Kanban, or Gantt, **when** its inspector is opened, **then** the same stable identity and evidence-backed Delivery Card fields are shown in a shared read-oriented inspector.
2. **Given** a Phase, Work Package, milestone, or decision point is selectable, **when** its inspector is opened, **then** it uses a read-only kind-appropriate variant and omits unsupported Delivery Card state, effort, owner, or dependency semantics.
3. **Given** actual or remaining effort is known to be zero, unknown/null, not run, or inapplicable to the selected kind, **when** the inspector is displayed, **then** it shows zero only for the known-zero value, preserves the applicable unknown/not-run meaning, and omits only the inapplicable field.
4. **Given** an inspector is open and the user closes it, **when** the selected item still exists in the same official snapshot, **then** selection persists by stable canonical identity independently of keyboard focus; focus returns to the invoking rendered/enabled item, or otherwise to the current view heading or relevant scope/search control.
5. **Given** scope or filters hide a still-existing selected item, **when** Work remains open, **then** the selection is not cleared, the inspector explains that the item is outside the current results, and no filter or hierarchy is changed solely to restore focus.
6. **Given** the user returns to a view with the same phase scope, query, and filters, **when** that view is displayed, **then** its prior scroll position is restored independently of selection and keyboard focus.
7. **Given** that view state has changed, **when** it is displayed, **then** the selected item is scrolled into view only if it matches current results; otherwise the view returns to the start of current results.
8. **Given** a Delivery Card is selected in Work, **when** the user chooses `Xem trên Gantt`, **then** Gantt focuses the same stable identity and visible hierarchy path without changing Gantt schedule/dependency semantics and does not newly open an inspector that was closed.
9. **Given** a Delivery Card is selected in Gantt, **when** the user opens it in Work, **then** Work retains its active search/filter state; if that state excludes the card, the UI explains the active scope rather than silently widening or clearing it.
10. **Given** a source-backed direct dependency is present, **when** the Delivery Card inspector shows dependency context, **then** it names the direction (`Phụ thuộc vào` or `Ảnh hưởng trực tiếp đến`) and readable linked work identity; it does not claim unsupported transitive impact.

### User Story 5 — Use Work accessibly and preserve specialist parity (Priority: P2)

As a manager using a narrow screen or keyboard, and as a specialist checking legacy behavior, I want Work to remain operable and legacy views to transition only after parity is evidenced.

**Why this priority**: The unified destination must not exclude users or remove audit access before equivalent hierarchy and identity behavior is available.

**Independent Test**: Exercise Work at 360px and 1280px widths and keyboard-only; compare unified/legacy Delivery Card identities, state grouping, filters, and inspector reachability before any legacy Kanban handoff.

**Acceptance Scenarios**:

1. **Given** a 360px or 1280px viewport, **when** Work is used, **then** no primary action or keyboard control is clipped or inaccessible; Gantt may scroll within its own timeline surface.
2. **Given** a narrow viewport, **when** Kanban is used, **then** a labeled state-group selector exposes every group's count, including `Chưa ghi nhận`, and only one group is presented at a time without changing group membership or phase scope.
3. **Given** a narrow viewport, **when** the inspector opens, **then** it occupies a full-screen detail surface, receives focus at its heading or Close control, and Escape/Close returns focus by the restoration rule in FR-026.
4. **Given** any supported input modality, **when** state, alert, selection, filter, or group membership is conveyed, **then** meaning is not conveyed by color or motion alone and no drag gesture is required.
5. **Given** Unified Work is undergoing parity review, **when** the workspace is used, **then** legacy WBS remains under Advanced and is not removed before hierarchy parity is verified.
6. **Given** unified Kanban has not passed parity review, **when** the workspace is used, **then** the existing Kanban remains reachable through Advanced and the new Work board does not silently replace/remove it.
7. **Given** parity evidence confirms the unified Kanban covers the same canonical Delivery Card identities, authored-state/unrecorded distinctions, relevant filters, and inspector reachability (or documents each intentional difference), **when** the user-facing Kanban entry transitions, **then** Unified Kanban becomes the sole normal user-facing Kanban destination; legacy WBS remains available under Advanced until its separate hierarchy-parity review.

## Edge Cases

- The imported hierarchy may contain any number of phases; no current fixture count is a product contract.
- The current phase may be unknown or ambiguous because reporting date or valid planned dates are missing or non-unique. Preserve All phases and do not choose by array position, status text, or workstation date.
- A Delivery Card may have a nullable source state, a source-authored state, an execution-overlay state, or no record at all. Only the effective state established by existing canonical/execution-truth rules is authored state; absence of a record is not `NOT_STARTED`.
- State `NOT_STARTED` may coexist with a derived `START_DELAY` alert. State and alert are independent; a card can be in the authored state group and also match Needs Attention.
- Duplicate-like IDs across canonical kinds do not merge identity. Phase, Work Package, Delivery Card, and control-point identity is kind-qualified as required by the canonical contract.
- A search can match a parent name while some descendants are removed by state or attention filters. Only surviving Delivery Cards and their necessary ancestor paths remain visible.
- Every group can be empty after filtering. Its heading/count remains stable with zero; no card is duplicated across groups.
- A suspended card can match the `SUSPENDED` attention signal; this does not move it out of the suspended authored-state area.
- An alert can have a supported code but no Delivery Card target, or target a non-card canonical item. It is excluded from Work Needs Attention.
- A selected item may be filtered out, removed by official snapshot replacement, or become unavailable in the target Gantt range. Selection is retained only while the same official snapshot identity remains active; navigation explains unavailable visibility without mutating scope.
- Unknown, invalid, not-run, null/absent, not-applicable, and known-zero values are not interchangeable. An inapplicable field is omitted; an applicable field without established evidence is presented according to its existing evidence state (for example, unknown or not recorded); numeric zero is displayed as zero only when it is explicitly known.
- Planned finish remains a baseline planned date. It is never relabeled or interpreted as a distinct due date or deadline.
- Work Package progress is not introduced or inferred from card counts, status counts, effort fragments, or phase aggregates.

## Requirements

### Functional Requirements

#### Navigation, collection, and scope

- **FR-001**: For a loaded official project, the primary navigation MUST expose `Tổng quan`, `Công việc`, and `Gantt`; `Công việc` MUST open List by default. The unloaded/import experience MUST remain governed by Feature 008.
- **FR-002**: Work MUST project the active official canonical project and MUST NOT create a separate task/work database, duplicate Delivery Card identity, or write changes to the authoritative source.
- **FR-003**: The Work List MUST represent the imported canonical hierarchy as Phase → Work Package → Delivery Card in canonical parent relationships and deterministic canonical/source order. It MUST support any number of canonical phases and MUST NOT encode a fixture-specific phase count.
- **FR-004**: Milestones and decision points MUST NOT appear as Work List work items or Kanban cards. Their existing Overview/Gantt representation MUST remain intact.
- **FR-005**: Initial Work scope MUST be All phases. A uniquely identified current phase MUST be opened/focused while remaining within that scope. If current phase is unknown or ambiguous, the UI MUST preserve All phases and MUST NOT fabricate a focus target.
- **FR-006**: Selecting one explicit phase MUST scope List and Kanban to that phase. Switching Work view MUST preserve that shared scope.
- **FR-007**: The current phase, when known, MUST be identifiable in the hierarchy and receive the approved current-phase focus/order treatment; that presentation MUST NOT imply that other phases are excluded under All phases.

#### Shared identity and view state

- **FR-008**: List, Kanban, and Gantt MUST refer to the same Delivery Card by the canonical kind-qualified stable identity. List and Kanban MUST expose the same card population after applying the same shared Work criteria.
- **FR-009**: Work MUST maintain shared phase scope, search query, authored-state filter, Needs Attention filter, and explicit unrecorded-state filter across List ↔ Kanban navigation. It MUST NOT silently clear or reinterpret a shared choice on view switch.
- **FR-010**: Work-specific shared filter choices MUST NOT change canonical/source data, baseline values, execution evidence, analysis, or Gantt-specific scale/date-range/scroll state.
- **FR-011**: The Work List MUST NOT show effort columns by default. Its reader-facing owner/role column MUST be labeled `Đầu mối / vai trò`; a named responsible person MUST appear only when authoritative evidence identifies that person. Logical role alone MUST NOT imply a named-person assignment.
- **FR-012**: The default List MUST show reader-facing work name first; stable technical ID, provenance, source SHA, diagnostics, CPM fields, and extended evidence MUST remain secondary/advanced rather than dominating default rows.
- **FR-013**: Baseline planned finish MUST be labeled `Kết thúc kế hoạch` or equivalent wording that unmistakably preserves baseline planned-date meaning. The UI MUST NOT call it a due date/deadline unless the authoritative model has a distinct due-date concept.

#### List search and filtered hierarchy

- **FR-014**: Work search MUST match Delivery Card reader-facing name and stable ID and the reader-facing Phase/Work Package names defined in the approved design. Matching MUST be case- and Vietnamese-diacritic-insensitive. It MUST NOT search raw diagnostics, source paths, or proposal text.
- **FR-015**: A Phase or Work Package name match MUST make its descendant Delivery Cards search candidates; the active phase scope and remaining filters MUST still apply.
- **FR-016**: With no search, authored-state, Needs Attention, or unrecorded-state result filter active, the List MUST show the approved hierarchy for the in-scope phases, including visible collapsed phase headings where applicable. When any such result filter is active, it MUST render a Phase/Work Package ancestor only when at least one surviving Delivery Card descendant is visible. Phase scope alone is not a result filter. Ancestors are context, not Delivery Card results or counts.
- **FR-017**: For filtered results, the List MUST expose only ancestor paths needed to reveal surviving Delivery Cards and MUST NOT display an orphan ancestor after all its descendants are filtered out.
- **FR-018**: Revealing result paths MUST NOT widen phase scope or change shared filters. Clearing search MUST restore the pre-search List expansion state. List-only expansion state MUST remain separate from shared List/Kanban filter state.
- **FR-019**: Empty results MUST distinguish an empty selected phase from no matches under current query/filters and MUST provide the corresponding phase or filter recovery action.

#### Kanban grouping, ordering, and counts

- **FR-020**: Kanban MUST use the same Delivery Card identities and effective authored execution states as List/canonical execution-truth resolution. The common authored-state groups MUST be `Chưa bắt đầu` (`NOT_STARTED`), `Đang làm` (`IN_PROGRESS`), and `Hoàn thành` (`COMPLETED`).
- **FR-021**: Cards with authored `SUSPENDED` and `CANCELLED` states MUST remain available in the compact expandable suspended/cancelled area with separate counts.
- **FR-022**: A Delivery Card whose effective execution state is unrecorded/null MUST appear in a distinct `Chưa ghi nhận` area with a visible count and MUST NOT be assigned to `Chưa bắt đầu`. No readiness status or inferred state may fill this gap.
- **FR-023**: Kanban MUST order cards deterministically within each state group: known current phase first; other phases in canonical hierarchy order; then Work Packages in canonical hierarchy order; then cards in canonical/source order; stable Delivery Card ID ordinal ascending as a tie-break for equal or unavailable canonical positions. No browser insertion order or unstable sorting may determine the result. This ordering is presentation-only.
- **FR-024**: Each Kanban group count MUST equal the number of Delivery Cards in that group after active phase scope, query, authored-state filter, Needs Attention filter, and explicit unrecorded-state filter. Counts MUST be group-specific, not presented as whole-project totals; every card MUST contribute to at most one state group; ancestors and milestones MUST contribute to no group count.
- **FR-025**: Group headings and counts MUST remain visible at zero. Counts for collapsed suspended/cancelled groups and the separate unrecorded area MUST obey the same post-filter rule as visible common groups.

#### Inspector, identity navigation, and accessibility state

- **FR-026**: Selecting a Delivery Card in List, Kanban, or Gantt MUST open the same read-oriented Delivery Card inspector. It MUST present only source/canonical/analysis-backed fields, including applicable identity/hierarchy, effective authored state or `Chưa ghi nhận`, `Đầu mối / vai trò`, baseline planned dates/effort, recorded actual/remaining effort, supported attention, dependencies, and provenance. A field that does not apply to the selected entity kind MUST be omitted; an applicable field with `UNKNOWN`, `INVALID`, `BLOCKED`, or `UNRESOLVED` evidence MUST NOT be shown as zero or as an authored state; `NOT_RUN`/no execution record MUST remain explicitly not recorded; and an explicitly known numeric zero MUST remain zero.
- **FR-027**: The Delivery Card inspector MUST keep baseline planned values distinct from execution-overlay Actual/Remaining evidence and derived analysis. It MUST NOT edit the official source, rewrite baseline, record Actuals, or create proposals in this increment.
- **FR-028**: A selectable Phase, Work Package, milestone, or decision point MAY use the shared inspector shell, but MUST use a read-only kind-appropriate variant and MUST NOT receive fabricated Delivery Card state, effort, owner, or dependency semantics.
- **FR-029**: Dependency detail MUST identify whether a link is a direct predecessor (`Phụ thuộc vào`) or direct successor (`Ảnh hưởng trực tiếp đến`) and name the linked canonical item when supported. It MUST NOT invent transitive impact or replace approved Gantt dependency calculations.
- **FR-030**: Selection MUST be maintained by stable canonical identity independently from keyboard focus and scroll position. Closing the inspector MUST NOT clear selection while the same official snapshot remains active. When the active official snapshot changes, the prior selected-item and inspector context MUST be cleared before the new snapshot is shown; matching IDs in a different snapshot MUST NOT silently carry selection across snapshots.
- **FR-031**: Closing the inspector by Escape or Close MUST return keyboard focus to the invoking item if it remains rendered and enabled; otherwise focus MUST move to the current view heading or relevant scope/search control. Selection persistence MUST NOT force filters to widen or ancestors to expand.
- **FR-032**: Each Work view MUST keep its own scroll position. Returning with unchanged phase scope, search, and filters MUST restore that view's prior scroll; if those criteria changed, scroll the selected item into view only when it matches current results, otherwise go to the start of current results.
- **FR-033**: Work ↔ Gantt navigation MUST carry stable Delivery Card identity, focus its Gantt row and visible ancestors when available, and preserve the inspector's prior open/closed condition. Gantt-to-Work MUST preserve Work state and explain when that state excludes the target rather than resetting it.
- **FR-034**: On narrow layouts, the inspector MUST become a full-screen detail surface, receive focus at its heading or Close control, and return focus by FR-031. Escape MUST close it. State, attention, focus, selection, and group membership MUST NOT be conveyed by color or motion alone.
- **FR-035**: At 360px and 1280px viewport widths, Work's primary actions and keyboard controls MUST remain accessible without page-level horizontal clipping. Gantt's existing timeline may scroll within its own surface. Narrow Kanban MUST present one state group at a time with a labeled group selector and visible counts for all groups, including `Chưa ghi nhận`.
- **FR-036**: Keyboard users MUST be able to switch List/Kanban, change scope and filters, expand/collapse hierarchy, select a card, open/read/close the inspector, and navigate to/from Gantt without requiring drag interaction.

#### Needs Attention authority boundary

- **FR-037**: Needs Attention MUST include only a structured `ManagementAnalysis` alert with `WorkItemId` resolving to a canonical Delivery Card and an `AlertCode` in the exact whitelist `START_DELAY`, `OVERDUE`, `SUSPENDED`, `AT_RISK`.
- **FR-038**: Work attention MUST exclude `COMPLETED_LATE`, `COMPLETED_ON_TIME`, `CANCELLED`, `BLOCKED`, readiness/gate/decision/human-action records, diagnostics, import/source warnings, governance concerns, arbitrary warnings, and all codes not listed in FR-037. No new alert type or risk inference is authorized by this feature.
- **FR-039**: A supported signal MUST remain distinct from authored state and baseline/actual values. An empty Needs Attention result MUST communicate that no supported matching signal is available, not that the work is guaranteed risk-free. Raw machine codes/messages MUST NOT be used as primary labels.

#### Legacy and increment boundary

- **FR-040**: Legacy WBS MUST remain reachable under Advanced during this increment and MUST NOT be removed until Unified Work hierarchy parity is verified. This specification does not authorize legacy WBS retirement.
- **FR-041**: Existing Kanban MUST remain available under Advanced until Unified Kanban parity is verified against the canonical Delivery Card identity set, authored states plus unrecorded distinction, applicable scope/search/filter semantics, and inspector reachability (or each difference is explicitly accounted for). After verification, Unified Kanban MUST be the sole normal user-facing Kanban destination; two normal Kanban products MUST NOT remain indefinitely.
- **FR-042**: Unified Work MUST NOT add Work Package progress, a task store, a project-setup workflow, source write-back, direct baseline editing, actuals/proposal editing, Proposal Workflow redesign, or a changed Gantt scheduling/dependency engine.
- **FR-043**: The existing proposal capability MAY remain accessible through its existing Advanced path. Its lifecycle, forms, and approval semantics are outside this feature.

### Key Entities

- **Canonical Phase**: Imported source-backed top-level work-hierarchy item. Work can include any number of phases; its identity/order comes from the canonical project.
- **Canonical Work Package**: Imported source-backed child of a phase that groups delivery cards. It is hierarchy/context, not a Delivery Card or a new Work record.
- **Canonical Delivery Card**: Imported executable work item with a stable canonical identity, parent package/phase, baseline fields, and effective authored state as resolved by existing source/execution-overlay authority. It is the shared item across List, Kanban, Gantt, and inspector.
- **Milestone/Decision Point**: Imported control point that remains in Overview/Gantt and is not a Work List/Kanban work item.
- **Authored Execution State**: One of the canonical states `NOT_STARTED`, `IN_PROGRESS`, `COMPLETED`, `SUSPENDED`, `CANCELLED`, supported by source or execution evidence under existing precedence rules. It is not a derived alert.
- **Unrecorded State**: No effective execution state is established. It is presented as `Chưa ghi nhận`, not as `NOT_STARTED`.
- **Management Analysis Alert**: Derived, as-of-sensitive signal over baseline, execution evidence, calendar, and dependencies. It is never a persisted authored state; only FR-037 signals can enter Work Needs Attention.
- **Shared Work View State**: Current phase scope, search query, authored-state filter, Needs Attention filter, and explicit unrecorded-state filter preserved between List and Kanban. List expansion, per-view scroll, selected identity, and keyboard focus are separate state concepts.
- **Work Inspector**: Read-only contextual detail for the selected stable canonical identity, complete for Delivery Cards and kind-appropriate/evidence-limited for other selectable hierarchy/control items.

## Success Criteria

### Measurable Outcomes

- **SC-001**: For a test project with any phase count, Work opens with All phases and the independently determined current phase focused, without excluding another phase.
- **SC-002**: For every canonical Delivery Card in the official snapshot, List and Kanban contain the same stable identity exactly once before and after applying equivalent shared filters; no milestone, decision point, or ancestor appears as a Delivery Card.
- **SC-003**: For every execution-state fixture, 100% of recorded cards appear only in the correct authored-state group, and 100% of unrecorded cards appear only in `Chưa ghi nhận`; no unrecorded card appears in `Chưa bắt đầu`.
- **SC-004**: For every combination of supported scope/search/state/attention filters, displayed group counts equal a recomputation over the matching Delivery Cards; ancestor/control items are excluded and all zero groups remain visible.
- **SC-005**: Repeated rendering of the same official snapshot and Work state yields identical Kanban ordering, including ties and missing canonical positions.
- **SC-006**: Needs Attention returns exactly cards targeted by one or more of the four eligible alert codes in FR-037 and returns none solely because of excluded readiness/governance/diagnostic or unlisted alert data.
- **SC-007**: List shows exactly the ancestor paths required by surviving card results, with no orphan ancestors and no change to shared phase scope when paths are revealed.
- **SC-008**: Selecting the same Delivery Card from List, Kanban, and Gantt produces the same inspector identity; close/reopen, filtering, and view switching preserve selected identity, focus restoration, and scroll behavior according to FR-030…FR-033.
- **SC-009**: No value in the inspector converts unknown, absent/not-applicable, not-run/unrecorded, or known zero into another semantic state; baseline planned finish is never displayed as a due date.
- **SC-010**: At 360px and 1280px, all Work primary operations can be completed by keyboard without clipped required controls, and the inspector can be opened, read, and closed with a meaningful returned focus target.
- **SC-011**: Legacy WBS and Kanban retirement/handoff occurs only after the parity conditions in FR-040…FR-041 are evidenced; the released normal workspace does not indefinitely expose two normal Kanban products.

## Assumptions

- Feature 008 provides the active official project, source-import/unloaded behavior, Overview/Gantt foundation, and canonical/management analysis inputs; Feature 009 consumes them without changing their authority.
- The exact supported Work attention set is resolved by the existing alert producer and current Overview supported-consequence mapping: `START_DELAY`, `OVERDUE`, `SUSPENDED`, and `AT_RISK`. If either authoritative producer attribution or supported reader-facing mapping is removed or contradicted before implementation, the affected signal must be removed from Work attention until its authority is re-established; a new signal requires a contract change outside this specification.
- Canonical arrays provide deterministic source order; where positions are tied or unavailable, ordinal ascending stable Delivery Card ID is the presentation tie-break.
- The official source reporting/as-of date and existing analysis are used; Work does not substitute the workstation date or infer a current phase where the existing contract returns unknown/ambiguous.
- Only behaviors already accepted in the approved Unified Work design are in scope. This specification does not approve implementation details or a new visual redesign.

## Explicit Specification Boundaries

- No fixed phase count; use all phases present in the imported canonical hierarchy.
- No new task store, identity system, source, importer, or project-setup experience.
- No writes from Work to the source repository or immutable baseline.
- No direct editing of actual evidence, authored state, or proposals in this increment.
- No Work Package progress or inferred percentage.
- No milestone/decision-point cards in List/Kanban and no invented Delivery Card semantics for rollups/control points.
- No Needs Attention signal beyond FR-037 and no merge of readiness/governance/diagnostic concerns into Work attention.
- No Proposal Workflow redesign; no altered Gantt semantics/calculation; no removal of legacy WBS before a separate verified parity gate.
- This artifact is a feature specification only. It creates no implementation plan, tasks, or Proposal Workflow specification.

## Traceability Matrix — Clarification Provenance to Approved Design and Requirements

| UW | Provenance | Accepted decision / final behavior | Approved design source | Feature 009 coverage |
|---|---|---|---|---|
| UW-01 | Direct human clarification decision | Add Work to primary navigation: `Tổng quan \| Công việc \| Gantt`. | §§1, 4 | FR-001; Story 1, Scenario 1 |
| UW-02 | Direct human clarification decision | Default Work view is List. | §§1, 4, 6 | FR-001; Story 1, Scenario 1 |
| UW-03 | Direct human clarification decision | Default scope is All phases; current phase is focused, not implicitly filtered; explicit phase selection scopes both Work views. | §§4, 6, 7 | FR-005…FR-007; Stories 1–3 |
| UW-04 | Direct human clarification decision | `Chưa ghi nhận` is a separate counted area, not an authored-state column or `Chưa bắt đầu`. | §§5, 7, 11 | FR-020…FR-025; Stories 2–3 |
| UW-05 | Direct human clarification decision | Milestones and decision points are not Work List/Kanban work items; retain them in Overview/Gantt and use only evidence-backed read-only details. | §§5, 7, 9 | FR-003…FR-004, FR-028; Story 1 |
| UW-06 | Clarification topic → design-resolved proposal → accepted by human approval of complete design on 2026-09-29 | Search covers reader-facing Delivery Card name, stable ID, and reader-facing Phase/Work Package names, with approved matching and exclusions. Not a direct human answer. | §8, Search and filters | FR-014, FR-015 |
| UW-07 | Direct human clarification decision | Needs Attention includes only supported item-attributable work/schedule signals, not readiness/governance concerns. | §8, Search and filters | FR-037…FR-039; resolved whitelist under Authority; Story 2 |
| UW-08 | Clarification topic → design-resolved proposal → accepted by human approval of complete design on 2026-09-29 | Shared Work choices persist across List/Kanban; selected identity, keyboard focus, and scroll have distinct persistence/restoration behavior. Not a direct human answer. | §§8, Search and filters; 9, Shared inspector | FR-009, FR-030, FR-031, FR-032 |
| UW-09 | Direct human clarification decision | Delivery Card gets the complete read-oriented inspector; other selectable kinds get evidence-limited read-only variants. | §9, Shared inspector | FR-026…FR-029; Story 4 |
| UW-10 | Clarification topic → design-resolved proposal → accepted by human approval of complete design on 2026-09-29 | Direct dependency information appears after primary inspector details when applicable, is directional, may collapse, and does not invent unsupported transitive impact. Not a direct human answer. | §9, Shared inspector | FR-029 |
| UW-11 | Clarification topic → design-resolved proposal → accepted by human approval of complete design on 2026-09-29 | Work ↔ Gantt navigation uses stable identity, focuses/reveals the item when available, preserves inspector open/closed state, retains Work filters in reverse navigation, and explains when active controls exclude the item. Not a direct human answer. | §10, Gantt integration | FR-033 |
| UW-12 | Direct human clarification decision | Inspector remains read-oriented; do not redesign Proposal Workflow; existing proposal capability may remain under Advanced. | §§9, 14 | FR-027, FR-042…FR-043; Stories 4–5 |
| UW-13 | Direct human clarification decision | Keep legacy WBS under Advanced until hierarchy parity is verified. | §13, Legacy WBS / Kanban transition | FR-040…FR-041; Story 5 |
| UW-14 | Direct human clarification decision | Unified Kanban replaces the old normal user-facing Kanban only after parity is verified; do not retain two normal products long-term. | §13, Legacy WBS / Kanban transition | FR-041; Story 5 |
| UW-15 | Direct human clarification decision | Do not add Work Package progress in this increment. | §§5, 14 | FR-042; explicit boundary |
| UW-16 | Direct human clarification decision | Use `Đầu mối / vai trò`; identify a person as responsible only when the authoritative source identifies that person. | §6, List design | FR-011, FR-026; Story 4 |
| UW-17 | Direct human clarification decision | Do not show effort columns by default in List; Planned/Actual/Remaining effort belongs in the inspector. | §6, List design; §9, Shared inspector | FR-011, FR-026…FR-027; Story 4 |
| UW-18 | Clarification topic → design-resolved proposal → accepted by human approval of complete design on 2026-09-29 | Narrow List retains hierarchy/readability; narrow Kanban presents one labeled group at a time with every count; inspector becomes full-screen with defined focus behavior; no drag-only interaction. Not a direct human answer. | §12, Responsive and accessibility | FR-034, FR-035, FR-036; SC-010 |

**Provenance rule:** “Direct human clarification decision” identifies decisions answered directly by the human in the clarification register. “Clarification topic → design-resolved proposal → accepted by human approval” identifies topics whose final behavior was proposed in this design and became authoritative only when the complete design was approved on 2026-09-29. The latter are not represented as original direct answers.

### Cross-document authority traceability

| Authority | Feature 009 coverage |
|---|---|
| Approved Management UI redesign — one project/source with several views; Overview/Gantt remain; Advanced retains specialist tools; Gantt semantics preserved | FR-001…FR-002, FR-010, FR-033, FR-040…FR-043; Authority references |
| Feature 008 boundary — preserve unloaded/import and official source experience; do not expose a dead placeholder; retain Overview/Gantt foundation | FR-001, FR-010, FR-033; Authority references |
| Canonical/source and management-analysis authority — stable kind-qualified IDs, deterministic source order, authored state vs missing, immutable baseline, actual overlay, derived alerts, provenance | FR-002…FR-003, FR-008, FR-013, FR-020…FR-039; Entity definitions and contract references |

## Remaining Human Decisions

None. The provenance matrix distinguishes direct clarification answers from design-resolved clarification topics later accepted through human approval of the complete design. The specification is approved for planning; no specification-level human decision remains open. The accepted Needs Attention whitelist remains unchanged and contract-source-derived.
