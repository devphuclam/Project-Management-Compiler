# Project Management Compiler — Unified Work product / UX design proposal

**Status:** Approved by human design review on 2026-09-29.
**Date:** 2026-09-29
**Scope:** Increment 2, Unified Work, of the approved management UI redesign.

## Authority and reading guide

This proposal uses the approved [Management UI redesign design](2026-09-28-management-ui-redesign-design.md), the [Feature 008 workspace foundation specification](../../../specs/008-management-ui-foundation/spec.md) for its increment boundary, and the user's Unified Work clarification register UW-01 through UW-18. The user's latest decisions take precedence where they refine the earlier design. In particular, the default Work scope is now **All phases** in both modes; the current phase is opened or focused without silently restricting the collection. Earlier language about Kanban defaulting to the current phase is superseded by UW-03.

**APPROVED HUMAN DECISION** marks a decision supplied in the Unified Work clarification. **ESTABLISHED DESIGN RULE** marks a rule already in the management UI design. Every additional choice made here is explicitly marked **PROPOSED DESIGN DECISION**. In the version reviewed on 2026-09-29, all such proposed decisions were accepted by the human reviewer for Increment 2; the label records their origin and does not mean they remain pending. This document does not change the canonical model, source authority, or the approved Gantt calculations.

**Decision provenance:** UW-01…UW-05, UW-07, UW-09, and UW-12…UW-17 are direct human clarification decisions. UW-06, UW-08, UW-10, UW-11, and UW-18 are clarification topics whose final behaviors were resolved as **PROPOSED DESIGN DECISION**s in this proposal and became authoritative when the human reviewer approved the complete design on 2026-09-29. These five are design-resolved clarification topics, not direct human answers in the original clarification.

## 1. Purpose

Give a non-technical project manager one place to find and inspect delivery work. A hierarchical List and a state-based Kanban must present the same imported work identities. The manager can change view, narrow the scope, and inspect an item without mistaking missing execution evidence for a recorded state or a local proposal for official progress.

**APPROVED HUMAN DECISION — UW-01, UW-02:** After Unified Work is usable, primary navigation is **Tổng quan | Công việc | Gantt**. Opening Công việc starts in List mode.

## 2. Problems being solved

- Work is currently split across WBS, Kanban, and Gantt surfaces. The manager must reconstruct which rows refer to the same underlying item. **ESTABLISHED DESIGN RULE**
- Technical detail and internal IDs compete with the work name and execution state. **ESTABLISHED DESIGN RULE**
- An item with no recorded execution state can be mistaken for an item explicitly recorded as not started. **ESTABLISHED DESIGN RULE**
- Selecting work, changing the view, and examining dependencies can cause the user to lose context. **ESTABLISHED DESIGN RULE**
- Legacy specialist views need a controlled transition without discarding useful audit capability. **APPROVED HUMAN DECISION — UW-13, UW-14**

Unified Work addresses these as a view and interaction change over the imported project. It does not create or maintain a separate task database.

## 3. Users and jobs-to-be-done

- **Project manager:** Find a card by work name, determine its authored execution state and baseline planned finish (“Kết thúc kế hoạch”) when source-backed, identify supported work or schedule attention, inspect evidence-backed details, and move between List, Kanban, and Gantt.
- **Sponsor or reviewer:** Open a concise work item from Overview or Gantt, understand its place in the phase/package hierarchy and what a direct dependency affects, without reading machine diagnostics.
- **Specialist or auditor:** Reach legacy WBS and other technical views under Advanced while hierarchy parity is being checked.

**ESTABLISHED DESIGN RULE:** A CARIO logical role alone does not establish a person assignment or an “assigned to me” view.

## 4. Information architecture

```text
Project workspace
├── Tổng quan
├── Công việc
│   ├── List (default)
│   ├── Kanban
│   ├── shared scope: All phases or one selected phase
│   ├── shared search and applicable filters
│   └── contextual, read-oriented work inspector
├── Gantt
│   └── same work identity and contextual inspector
└── Nâng cao
    ├── legacy WBS, temporarily for specialist/audit parity
    ├── existing proposal access, without workflow redesign
    └── existing specialist/diagnostic and secondary capabilities
```

**APPROVED HUMAN DECISION — UW-03:** Initial scope is All phases. The current phase is opened or focused by default. Explicit selection of one phase scopes both List and Kanban to that phase.

**PROPOSED DESIGN DECISION:** The phase selector visibly says “Tất cả giai đoạn” on entry. Its selected value is a shared Work-level choice, so switching List/Kanban never quietly changes the number of phases included. A separate “Giai đoạn hiện tại” shortcut selects that phase explicitly rather than being an implicit filter.

The inspector is contextual detail, not a fourth primary destination. The existing Gantt time-scale and timeline controls remain local to Gantt. Advanced retains source, readiness, dependency/CPM, import, and export affordances according to their existing authority boundaries.

## 5. Work collection semantics

**ESTABLISHED DESIGN RULE:** Work is a read view of the imported canonical hierarchy. Phase and Work Package supply hierarchy and context; Delivery Card is the execution card shared across List, Kanban, and Gantt. Each underlying item keeps its stable identity across views. No independent Work record is created by this UI.

**APPROVED HUMAN DECISION — UW-05:** Milestones and decision points are not Work List work items and are not Kanban cards. They remain visible in Overview and Gantt. A selectable control point in Gantt may have read-only contextual detail, but must not acquire Delivery Card state, effort, or owner fields without source evidence.

**ESTABLISHED DESIGN RULE:** Authored execution state, missing execution state, derived attention, planned schedule, recorded actual evidence, and local proposal state are different concepts. Kanban groups Delivery Cards by authored execution state. A proposal never silently changes the group of an official card. No drag-and-drop state mutation is authorized.

**APPROVED HUMAN DECISION — UW-15:** No Work Package progress percentage is introduced in this increment. Completed-card counts, planned percent, and partial effort are not substitutes for effort-backed progress.

## 6. List design

**ESTABLISHED DESIGN RULE:** List follows Phase → Work Package → Delivery Card. It shows all phases in the imported canonical hierarchy by default, opens the current phase, and progressively expands packages and cards. The default row leads with the reader-facing name and shows authored state, primary owner/role, and baseline planned finish (“Kết thúc kế hoạch”) when known. Stable IDs are secondary.

**APPROVED HUMAN DECISION — UW-16, UW-17:** The reader-facing column label is “Đầu mối / vai trò”. A person is identified as responsible only when the authoritative source identifies a person. No effort columns appear in the default List; Planned, Actual, and Remaining effort are available in the inspector.

**PROPOSED DESIGN DECISION:** In the initial All phases view, phase headings are visible in canonical hierarchy order. The current phase is expanded to show its Work Packages; other phases and packages start collapsed. A user can expand any phase or package without changing the shared scope. When the user selects one phase, List shows that phase's hierarchy and keeps its prior expansion state during the session. This makes “focused phase” different from “filtered phase”.

**PROPOSED DESIGN DECISION:** Each default Delivery Card row shows name, authored state or explicit “Chưa ghi nhận”, “Đầu mối / vai trò” when source-backed, and baseline planned finish (“Kết thúc kế hoạch”) when source-backed. An attention marker appears only for a supported item-linked work/schedule signal. Empty values are presented as unknown or absent, not as zero or not started. IDs remain in inspector detail and are searchable, but do not lead the row.

**PROPOSED DESIGN DECISION:** Long names wrap or truncate with an accessible full-name affordance while retaining the hierarchy cue and row selection target. Search highlighting never changes the official display name. The List does not add package progress, effort summaries, source SHA, provenance, or CPM columns by default.

**PROPOSED DESIGN DECISION:** List filtering is performed over Delivery Cards within the active phase scope. Search matches a card's reader-facing name or stable ID; a match against a phase or Work Package name includes that ancestor's descendant cards as search candidates. Apply the other active filters to those cards, then show only the ancestor paths of cards that remain. Ancestor rows provide hierarchy context and do not count as Delivery Card results. Expand the paths needed to reveal matching cards without opening unrelated branches. If filters remove every descendant, show the no-results state rather than an orphan ancestor. Clearing the search restores the expansion state that existed before search.

## 7. Kanban design

**ESTABLISHED DESIGN RULE:** Kanban uses the same Delivery Card identities and authored states as List. Its three common columns are **Chưa bắt đầu**, **Đang làm**, and **Hoàn thành**. **Tạm dừng** and **Đã hủy** remain in a compact expandable area with counts. A card with no recorded state is never placed in Chưa bắt đầu.

**APPROVED HUMAN DECISION — UW-04:** “Chưa ghi nhận” is a visibly separate area with its own count, not a normal authored-state column. It remains distinct from Chưa bắt đầu.

**APPROVED HUMAN DECISION — UW-03:** Kanban initially includes All phases even though the earlier management UI design suggested a current-phase default. Choosing one phase scopes both Work modes.

**PROPOSED DESIGN DECISION:** The “Chưa ghi nhận (N)” area sits before the authored-state board and remains discoverable even when it has no cards. Its label explains that no execution state has been recorded; it does not imply a late start or failure. The three authored columns count only cards with those authored states. Suspended/cancelled counts are separate.

**PROPOSED DESIGN DECISION:** In All phases, a card shows its phase context and reader-facing title; cards from the current phase appear first within each state group, while the group count still covers all phases. Within each state group, ordering is deterministic: current phase first; then remaining phases in canonical hierarchy order; then Work Packages in canonical hierarchy order; then Delivery Cards in their canonical/source order. If two cards have the same or unavailable canonical position, sort by stable Delivery Card ID using ordinal ascending comparison as the deterministic tie-break. Never rely on browser insertion order or an unstable sort. This ordering is presentation-only and does not change state, hierarchy, schedule, or source data. The default card may show a source-backed baseline planned finish (“Kết thúc kế hoạch”) and supported attention marker; IDs, effort inventory, dependency paths, and long notes stay in the inspector. The phase selector and group counts state the active scope so ordering cannot masquerade as filtering.

**PROPOSED DESIGN DECISION:** Every Kanban group count is the number of Delivery Cards in that group after applying the active phase scope, search, authored-state filter, Needs Attention filter, and explicit “Chưa ghi nhận” filter. A group count is group-specific; it is never presented as a whole-project total. Each Delivery Card contributes to exactly one group; phase/package ancestor rows and milestones/control points are not counted. The unrecorded area and each suspended/cancelled subgroup use the same rule, including while collapsed. Keep group headings visible when a filter yields zero cards and show a zero count, so the board structure remains stable. Scope controls and active filter chips identify why counts changed.

**PROPOSED DESIGN DECISION:** Selecting a card opens the same Delivery Card inspector as List. Moving a card to another state by dragging is not offered. Kanban does not reinterpret a proposal draft as an authored execution state.

## 8. Search and filters

**ESTABLISHED DESIGN RULE:** Search and applicable filters persist when switching List ↔ Kanban. The small filter set is phase, authored execution state, and Needs Attention. “Assigned to me” is not inferred from logical roles.

**APPROVED HUMAN DECISION — UW-07:** Needs Attention includes only supported work/schedule attention attributable to a work item. Governance or readiness concerns are not merged into this filter.

**SPECIFICATION BOUNDARY:** The exact eligible Needs Attention signal whitelist MUST be resolved from authoritative existing management-analysis contracts during specification. Implementation may include only signals supported by those contracts and attributable to a work item. Arbitrary warnings, readiness issues, governance issues, or diagnostics must not be treated as Work attention. If the specification cannot identify an authoritative supported signal, that signal remains outside the Work filter.

**PROPOSED DESIGN DECISION:** Search matches the reader-facing Delivery Card name, its stable ID, and reader-facing parent Work Package/Phase names, case and Vietnamese-diacritic insensitively. The UI indicates when a match came from parent context. It does not silently search proposal text, raw diagnostics, source paths, or role codes. A match to a collapsed List descendant exposes the matching ancestor path for that result without changing the selected phase.

**PROPOSED DESIGN DECISION:** Phase scope, search text, authored-state filter, and Needs Attention filter form one shared Work view state. A card is visible only if it satisfies the active choices; no filter changes source data. A chosen authored-state value never captures “Chưa ghi nhận”. The latter has its own explicit choice so users can inspect unrecorded cards without relabeling them.

**PROPOSED DESIGN DECISION:** Derived attention is displayed as a filterable signal only when the existing management analysis can associate a supported work/schedule condition with a stable work item. Unattributed warnings and readiness/governance concerns remain in their contextual specialist surfaces. If no supported signal exists, the filter explains that absence instead of claiming the work is risk-free.

## 9. Shared inspector

**ESTABLISHED DESIGN RULE:** Selecting a work item from List, Kanban, or Gantt opens the same contextual side inspector; opening and closing it preserves the previous view, filters, location, and selection. Escape closes it. Narrow screens use a full-screen detail surface.

**APPROVED HUMAN DECISION — UW-09, UW-12:** Delivery Card receives the complete work inspector. Other selectable hierarchy/control-point kinds may use a read-only variant with only evidence-backed fields. Unified Work inspector is read-oriented; it does not introduce a new proposal form or update action.

The initially visible Delivery Card detail contains display name, kind, phase/package context, authored state or “Chưa ghi nhận”, source-backed owner/role, planned dates and effort, recorded Actual/Remaining effort, and a concise supported alert if present. Stable ID is available as secondary identity. Unknown, absent, and zero remain different values.

**PROPOSED DESIGN DECISION:** Direct dependencies appear in a clearly named section immediately after the primary details when they exist. It distinguishes “Phụ thuộc vào” from “Ảnh hưởng trực tiếp đến” and names linked work items. This section can collapse to keep the summary readable; full provenance, source references, extended evidence, and CPM calculations remain advanced disclosures. The inspector does not manufacture a transitive dependency or a downstream consequence the existing analysis cannot support.

**PROPOSED DESIGN DECISION:** Phase, Work Package, and selectable Gantt control-point variants reuse the inspector shell, identity, source context, and navigation behavior. Each omits Delivery Card-only state, effort, or dependency fields when unsupported; omission is preferable to invented values. Gantt's existing dependency-impact explanation can occupy a contextual section in this shared shell without changing its calculation.

**PROPOSED DESIGN DECISION:** Selected-item persistence and keyboard focus are separate. The selected item is keyed by stable canonical identity and remains selected when the inspector closes or the user changes views; it changes only when another item is selected or the active official snapshot changes. If scope, search, or filters hide that item, do not clear the selection or alter those controls. When the inspector is open, explain that the selected item is outside the visible result set. On close, the row/card is highlighted if it is currently rendered. Scroll position is separate from both selection and focus: restore a view's prior scroll when returning with the same phase scope, search, and filters; if that view state changed, scroll the selected item into view only when it matches the current result set, otherwise return to the start of the current result set.

**PROPOSED DESIGN DECISION:** Keyboard focus is transient interface focus and does not define which item is selected. On Escape or the inspector's Close action, return focus to the row/card that opened it if that target is still rendered and enabled. If it is hidden or no longer exists, focus the current view heading or the relevant scope/search control. Do not change selection, expand filtered ancestors, or widen filters solely to restore focus. On a narrow full-screen inspector, focus enters the inspector when it opens and follows this same return rule when it closes.

## 10. Gantt integration

**ESTABLISHED DESIGN RULE:** Gantt keeps its approved schedule, actual lane, compact columns, focused direct predecessor/successor connectors, time controls, and dependency calculation semantics. Unified Work shares imported identities and contextual inspection, not a new scheduling engine.

**PROPOSED DESIGN DECISION:** From a selected Delivery Card in Work, “Xem trên Gantt” carries its stable ID to Gantt, reveals its visible hierarchy path, focuses the corresponding row, and keeps the existing inspector open only if it was already open. It does not open the inspector merely because the user switched views. If the selected item has no visible Gantt row under the active Gantt controls, the UI explains that condition and offers a way back to Work without claiming the item disappeared from the project.

**PROPOSED DESIGN DECISION:** Gantt-to-Work navigation applies the same identity and preserves the Work search/filter choices. If those choices exclude the item, the user is told which Work scope is active rather than having filters silently reset. Gantt-specific date range, scale, and timeline scroll do not become global Work filters.

## 11. Empty, unknown, and loading states

- No official project: use the existing **Mở dự án** experience; Work does not claim a loaded project. **ESTABLISHED DESIGN RULE**
- No Delivery Cards in a selected phase, no search results, and no items in an authored-state group are three different outcomes. **PROPOSED DESIGN DECISION:** Each message names its active scope or filter and offers the relevant clear-filter or change-phase action.
- Missing execution state is “Chưa ghi nhận”, including the dedicated Kanban area and count. It is not a blank state, zero progress, or Chưa bắt đầu. **APPROVED HUMAN DECISION — UW-04**
- Missing dates, responsible person, or effort are shown as unsupported/unknown in the appropriate field; no substitute values are generated. **ESTABLISHED DESIGN RULE**
- **PROPOSED DESIGN DECISION:** During a view switch or filter response, keep the current scope controls visible, show an in-place loading state, and avoid temporarily displaying cards under the wrong scope. An error is inline, retains the last valid view state, and offers retry without implying an official source change.

## 12. Responsive and accessibility

**ESTABLISHED DESIGN RULE:** Desktop/laptop remains the primary dense workspace. Narrow screens must keep navigation and toolbar controls operable, List usable, Gantt horizontally scrollable, and inspector full-screen. Focus must be visible; disclosure and selection must work by keyboard; Escape closes the inspector. Meaning cannot depend only on color or motion.

**PROPOSED DESIGN DECISION:** At narrow widths, List keeps phase/package hierarchy labels and places name, authored state, and baseline planned finish (“Kết thúc kế hoạch”) ahead of the role line; it does not squeeze four fixed-width desktop columns into the viewport. Kanban presents one state group at a time with an explicit labeled group selector and visible counts for every group, including the separate Chưa ghi nhận area. This changes presentation only: group membership and All phases scope stay the same.

**PROPOSED DESIGN DECISION:** Opening the full-screen inspector moves keyboard focus to its heading or Close control. Closing it restores focus according to Section 9, independently of selected-item persistence. View switching, group selection, and expand/collapse have text labels and keyboard paths; no drag gesture is required.

## 13. Legacy WBS / Kanban transition

**APPROVED HUMAN DECISION — UW-13:** Legacy WBS stays under Advanced temporarily for specialist/audit parity. Its UI is not removed before Unified Work hierarchy parity is verified.

**APPROVED HUMAN DECISION — UW-14:** Unified Kanban is intended to replace the old user-facing Kanban after parity is verified. Two normal Kanban products must not persist long-term.

**PROPOSED DESIGN DECISION:** During parity review, Advanced distinguishes legacy WBS and legacy Kanban as existing specialist/compatibility surfaces; the primary Work destination always opens the unified collection. Once card identity, authored-state groups, filtering, and key inspector access match or explicitly account for the old behavior, the old Kanban navigation can hand off to unified Kanban. Retiring a legacy UI does not retire canonical hierarchy data, source diagnostics, or exports.

**PROPOSED DESIGN DECISION:** Parity review compares identities and reachability, not visual similarity. Any specialist-only field or action found in the legacy screens remains reachable in contextual detail or Advanced before that screen is retired. The decision to retire legacy WBS remains a later review, not an automatic consequence of shipping Unified Work.

## 14. Scope and non-goals

**In this increment:** Work primary destination; List default; shared delivery-card identities; All phases and explicit phase scoping; authored-state Kanban with separate unrecorded area; shared search/filters; read-oriented inspector; Gantt selection integration; responsive and keyboard behavior; parity-aware legacy transition.

**Not in this increment:** a new source or task store; project setup wizard; source write-back; editable official Actual; drag-and-drop state changes; proposal form/list/lifecycle redesign; Work Package progress; a new Gantt engine or changed dependency semantics; readiness/governance signals in Needs Attention; changed export authority.

**APPROVED HUMAN DECISION — UW-12:** The existing proposal capability may remain reachable through its existing Advanced path. Proposal Workflow remains increment 3.

## 15. Risks

| Risk | Design guardrail |
|---|---|
| All phases makes Kanban crowded. | **PROPOSED DESIGN DECISION:** show phase context, current-phase ordering, visible scope and counts; retain explicit one-phase selection. |
| “Chưa ghi nhận” is misread as Chưa bắt đầu. | **APPROVED HUMAN DECISION — UW-04:** separate named area and count; never merge state groups. |
| A derived warning appears to be an authored state. | **ESTABLISHED DESIGN RULE:** state and alert remain distinct; **APPROVED HUMAN DECISION — UW-07:** only item-attributable work/schedule attention enters the Work filter. |
| Logical role is presented as a named person. | **APPROVED HUMAN DECISION — UW-16:** label “Đầu mối / vai trò”; person only with authoritative person evidence. |
| Gantt/Work focus points to different records. | **PROPOSED DESIGN DECISION:** navigate by stable identity; state clearly when current filters hide a row. |
| Legacy and unified views disagree during transition. | **APPROVED HUMAN DECISION — UW-13, UW-14:** verify hierarchy and Kanban parity before retirement. |
| Compact mobile layout hides hierarchy or unknown state. | **PROPOSED DESIGN DECISION:** retain phase/package labels and explicit state group counts; verify keyboard/focus paths. |

## 16. Remaining open questions

There is no missing human decision that prevents review of this proposal. The group-count semantics, List ancestor visibility, and separation of selected-item persistence from keyboard-focus restoration are now explicitly stated as reversible **PROPOSED DESIGN DECISIONS** in Sections 6, 7, 9, and 12. Other proposed choices remain subject to design review, including search fields and matching, dependency-section placement, Gantt focus and filter interactions, narrow-screen Kanban presentation, and the exact parity threshold for retiring the old Kanban navigation.

The existing documents do not enumerate the complete set of item-attributable work/schedule alert types. The design boundary is settled by UW-07, but the exact eligible signals must be named from authoritative existing contracts during subsequent specification. No unsupported signal is assumed here.

## 17. Acceptance direction

These are product-facing outcomes for later specification, not implementation tasks:

1. With an official project loaded, primary navigation reads **Tổng quan | Công việc | Gantt**. Công việc opens List with All phases and the current phase visibly opened/focused. Explicit phase selection scopes both Work modes.
2. List and Kanban represent the same Delivery Cards by stable identity. List retains Phase → Work Package → Delivery Card hierarchy. Milestones and decision points remain in Overview/Gantt.
3. Kanban has the three authored-state columns, compact suspended/cancelled groups, and a separate counted “Chưa ghi nhận” area. A card with no recorded state never appears in Chưa bắt đầu.
4. Work search/filter switching retains applicable choices. Needs Attention is limited to supported item-attributable work/schedule concerns. Readiness and governance remain separate.
5. A Delivery Card opens evidence-backed, read-oriented detail. Other selectable kinds show only supported fields. The same identity is inspectable from Gantt without changing Gantt schedule/dependency behavior.
6. Default List remains readable without effort columns or dominant IDs. Actual/Remaining effort and traceability remain available in the inspector. Logical roles are not presented as named people without authority.
7. A narrow-screen and keyboard user can change Work mode, scope and state group, select an item, read its inspector, close it, and regain a meaningful focus target. Unknown, empty, loading and error states remain distinguishable.
8. Legacy WBS remains reachable for parity review; old user-facing Kanban is not retired until unified behavior is verified. Proposal and source authority are unchanged.
9. Kanban counts describe cards remaining after active scope, search, and filters. List search/filter results retain only ancestors needed to show surviving cards. Closing the inspector preserves the selected item independently from restoring keyboard focus.

## 18. Delivery boundary

This proposal ends at Unified Work product and UX design. It records authoritative human decisions and reversible design proposals for review. It does not approve an implementation, define a Feature 009 specification, create a task plan, or start Proposal Workflow. After design review, a separate specification can convert the accepted behaviors and any amended proposals into testable requirements.
