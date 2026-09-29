# Project Management Compiler — Management UI redesign

**Status:** Written design approved by the user (2026-09-29).
**Date:** 2026-09-28
**Scope:** Progressive redesign of the current local Project Management Compiler experience.

## 1. Purpose

The application already compiles a planning source into a detailed management model, but its interface exposes too many technical views and repeated summaries at once. A non-specialist should be able to open the current project, understand where it stands, find the work that matters, and record a local execution proposal without learning the compiler's internal vocabulary.

The user approved a phased redesign inspired by familiar interaction patterns from ClickUp, Jira, and other project-management products. The design should use their conventions—clear project navigation, one work collection with multiple views, hierarchy, contextual detail, and progressive disclosure—without copying any product's visual identity.

### Success criteria

- A first-time user can identify how to open the current IDEAEngineering project without first navigating technical panels.
- After a valid official import, the first loaded view answers: which phase is current, what progress is supported by recorded effort, what control point is next, and what needs attention.
- Project work has one consistent identity across hierarchical list, Kanban, and Gantt views.
- A user can inspect a work item and create a proposal without losing their place in the plan.
- The plan remains immutable; a local proposal never appears as official source actuals and never writes back to IDEAEngineering.
- Dependency and readiness diagnostics remain available, but do not compete with routine project work in the primary navigation.
- The interface remains usable at narrow widths and by keyboard, while preserving the data-rich desktop Gantt experience.

## 2. Scope and boundaries

### In scope

- The current local IDEAEngineering source workflow and the loaded-project experience.
- A simplified application shell and primary navigation.
- A concise project overview, a shared Work List/Kanban surface, and integration with the existing Gantt.
- Contextual work-item inspection and the local execution-proposal workflow.
- Progressive disclosure for analysis, source diagnostics, readiness, and compatibility flows.
- Responsive layout and interaction-state improvements needed by these surfaces.
- The import capability needed to resolve the repository's configured default branch to an exact commit before importing it as an official snapshot.

### Out of scope

- A guided workspace for creating and setting up a brand-new project from scratch. The current release remains focused on the established IDEAEngineering flow; navigation should not prevent such a workspace from being added later.
- A new project-management data model, a second task collection, or a replacement for the canonical project model.
- Direct source editing, source write-back, or promoting a local proposal to official actuals inside the Compiler.
- Replacing the existing Gantt scheduling, dependency, or calculation semantics.
- Copying ClickUp/Jira branding, proprietary assets, or exact screen layouts.

## 3. Product and visual principles

1. **One project, one source, several views.** Overview, Work List, Kanban, Gantt, exports, and analysis all refer to the same imported project and canonical work identities.
2. **Progressive disclosure.** Put routine actions first. Keep exact SHA selection, manifest override, as-of override, legacy analysis, JSON reopening, XLSX preview, dependency diagnostics, CPM, readiness evidence, and source warnings discoverable under contextual or Advanced controls.
3. **Explain evidence honestly.** Keep PLAN, ACTUAL, proposal-only scenarios, and derived ALERT conditions distinct. Unknown execution remains unknown, not zero or “not started.”
4. **Names before identifiers.** Reader-facing Vietnamese names lead. Internal IDs and provenance remain available where needed for traceability and proposal creation, but do not dominate everyday labels.
5. **Familiar, restrained management UI.** Use clear hierarchy, compact tables, recognizable view switching, state labels, and a contextual inspector. Keep a calm neutral surface and a single controlled accent; reserve warning colors for meaningful conditions. Avoid card repetition, decorative gradients, and dense acronym-heavy copy.
6. **Preserve the working Gantt.** Treat the approved focused Gantt implementation as a product asset. Integrate it with the new shell and detail behavior without regressing its compact columns, focused dependency paths, weekly grid, or explainable impact inspector.
7. **Keep the implementation local and lightweight.** Retain the current ASP.NET Core host and vanilla HTML/CSS/JavaScript approach. Do not add a frontend framework or dependency for this redesign.

## 4. Information architecture

### Primary navigation

The loaded-project workspace has three routine destinations:

- **Tổng quan** — concise project status and next actions.
- **Gantt** — schedule, actual lane, milestones, and focused dependency impact.
- **Công việc** — one work collection with **Danh sách** and **Bảng Kanban** modes.

An **Nâng cao** entry groups supporting views without removing them:

- **Phân tích:** dependencies and CPM/critical path.
- **Nguồn & kiểm tra:** source identity, warnings, and import diagnostics.
- **Readiness:** shown when readiness evidence is present; otherwise not presented as an empty or failed control center.
- **Tương thích / nhập khác:** XLSX read-only preview, legacy local analysis, and reopening canonical JSON.

The executive progress workbook remains the primary export action. The CARIO + Gantt workbook and raw JSON remain available as secondary project actions. Export eligibility and preview-vs-official boundaries stay explicit.

### Before a project is loaded

Show a focused **Mở dự án** state rather than an empty dashboard or blank analysis views.

- If a source was used in this browser before, show its repository path and last successfully loaded official snapshot as a local recent-source entry.
- The user must explicitly choose **Đọc phiên bản hiện có trên máy**; merely reopening the app must not import or replace a snapshot in the background.
- If no source is remembered, guide the user to enter the local repository root once and keep advanced import settings collapsed.
- Persist only the local repository-root and manifest-path preferences in the browser on this machine. Do not persist credentials, project contents, or source snapshots in browser storage.
- Keep the advanced exact-commit, working-tree preview, manifest path, as-of override, XLSX preview, legacy capture, and JSON reopening paths accessible without putting them on the first screen.

### After a project is loaded

Use one project workspace with a single content area. Remove the persistent control-center/summary block that currently repeats project status above the selected view. Project identity and source snapshot identity remain available in a compact header/context area, not as another dashboard.

## 5. Source update and import behavior

The current official importer requires an exact Git commit SHA. The primary **Đọc phiên bản hiện có trên máy** action therefore needs a server-side capability to resolve the repository's configured default-branch ref (normally the local `origin/HEAD` symbolic ref) to a commit object, then pass the exact resolved SHA through the existing bounded Git-object capture and validation flow. Do not hard-code a branch name for every future source.

This operation reads only refs and objects already present in the selected local checkout. It does not run `git fetch`, contact the remote, or update Git metadata. The UI must describe this as the newest default-branch commit **available in this local copy**, not as a verified latest remote commit. Show the SHA and say that GitHub freshness has not been checked; explain that the user can update the local repository before reading again if newer source changes are needed. If the local default-branch ref cannot be resolved safely, provide a clear recovery path to the advanced exact-SHA field rather than silently using the current working tree.

On success, show the resolved commit SHA and source date in source details so the loaded snapshot is reproducible. Continue applying the existing authority classification: only a valid official import replaces the official snapshot. A candidate, preview, invalid import, or capture failure must retain the last valid official snapshot and explain the result.

The importer remains read-only with respect to the source repository. A working-tree capture remains an explicitly non-authoritative preview. A compiler-exported XLSX remains a session-only read-only preview and cannot create execution proposals or replace official state.

## 6. Project Overview

Overview is the default destination after a successful official import and the first reader-facing page for an already loaded project. It should fit the manager's quick scan and avoid reproducing a technical dashboard.

Show only these primary facts:

1. **Current phase** and a short project identity.
2. **Recorded progress**, only when actual and remaining effort are both valid and their sum is greater than zero. Calculate `actual effort / (actual effort + remaining effort)`. Otherwise display that effort progress is not yet known. Show completed-card counts separately; never substitute task-count completion or plan percentage.
3. **Next milestone/control point**, with its date and source-backed state when known.
4. **Up to three actionable attention items**, each linking to the affected work item and explaining the consequence without compiler jargon.

The official source date and snapshot identity should be findable but secondary. Detailed effort inventory, full warnings, source provenance, CPM numbers, and complete hierarchy belong in their contextual views or inspector disclosures.

## 7. Work List, Kanban, and work-item inspection

### Shared work collection

The Work page is a view over the imported hierarchy, not a new store. The default List mode shows the six phases, opens the current phase, and progressively expands packages and delivery cards. Selecting another phase scopes the List and Kanban consistently.

Provide a focused search and a small set of useful filters: phase, authored state, and needs-attention. Keep the search and applicable filters when switching List ↔ Kanban. Do not introduce unsupported “assigned to me” semantics based on CARIO logical roles.

The default row communicates the reader-facing work name, authored execution state, primary owner/role, and due date. Show planned/actual effort only when useful and known. Keep stable IDs secondary; use them in contextual detail and proposal/API interactions, not as the main display name.

### Kanban

Kanban groups the same delivery cards by authored state and defaults to the current phase. A phase selector can switch to another phase or all phases. The three common columns are **Chưa bắt đầu**, **Đang làm**, and **Hoàn thành**. **Tạm dừng** and **Đã hủy** are grouped behind a compact expandable area with counts. Items with no recorded state must remain clearly “Chưa ghi nhận”; they must not be silently placed in “Chưa bắt đầu.” Milestones and decision points stay in Overview/Gantt, not as Kanban cards.

This redesign does not silently authorize drag-and-drop state mutation. Any future editing still uses the local-proposal workflow and the proposal-only boundary.

### Shared inspector

Selecting a work item from the List, Kanban, or Gantt opens the same contextual side inspector and preserves the user's location and filters. On narrow screens it becomes a full-screen detail surface. Escape/close returns to the prior view and selection state.

The initially visible detail is: display name, item kind, phase/package context, state, owner/role, planned dates/effort, recorded actual/remaining effort, and any concise alert. Dependency relationships and downstream impact are explicit and directional. Source references, full provenance, calculated CPM, and extended evidence remain expandable rather than being dumped into the default view.

## 8. Local execution proposals

Users create or inspect proposals from the selected work item's inspector. The core update fields are state, actual hours, and remaining hours; actual dates and a note are optional. A proposal can be saved as Draft with no evidence, but the interface explains what evidence is needed before it can be marked ready for review. The UI must not invent an author, evidence locator, result, date, or completion claim.

Proposals remain local and tied to their base snapshot. They never change official source execution, the immutable plan, or the selected source repository. When the official snapshot changes, existing proposals remain visible and are clearly identified as stale where applicable.

Provide a **Đề xuất (N)** project action only when proposals exist. It opens the proposal list with lifecycle state, linked work item, base-snapshot/stale context, and supported next action. The work-item inspector also links to its own proposals. This is not a new primary navigation destination.

## 9. Gantt and advanced views

Keep the current approved Gantt as the schedule work surface: compact configurable columns, calm weekly/monthly grid, separate plan/actual meaning, sparse milestones, focused direct predecessor/successor connectors, and a contextual inspector that names which items the selected dependency affects. Closing the inspector restores the full chart width. The shell may share project identity and the global work filters where appropriate, but Gantt-specific time scale and timeline controls remain local.

Dependencies/CPM and source/readiness diagnostics remain fully available under Advanced. They should retain their underlying data and existing calculations; the change is their navigation placement, plain-language framing, and progressive disclosure—not removal or altered authority.

## 10. Responsive, accessibility, and content requirements

- Desktop/laptop is the optimized workspace, especially for Gantt and hierarchy browsing.
- At narrow widths, navigation and toolbars wrap cleanly; tables remain usable; Gantt scrolls horizontally; the inspector occupies the available screen instead of squeezing the schedule.
- Preserve visible keyboard focus, semantic navigation/main/aside landmarks, accessible labels on icon-only controls, keyboard-operable disclosure and selection, and Escape behavior for the inspector.
- Respect `prefers-reduced-motion`; interaction remains understandable without animation or color alone.
- Vietnamese is the default user-facing language. Explain a common technical acronym in Vietnamese at first use where it helps; do not repeat internal codes or machine labels in reader-facing headings.
- Loading, empty, success, stale, preview, and import-error states must be explicit and consistent. Errors appear inline with a recovery action; avoid browser alerts and vague “failed” labels.

## 11. Delivery decomposition

The complete target is intentionally delivered in reviewable increments rather than one replacement of all pages:

1. **Workspace foundation:** focused unloaded/open-project state, remembered local source preference, safe default-branch-to-SHA resolution, compact loaded shell/navigation, and concise Overview. This is the first implementation plan after written-spec approval.
2. **Unified Work:** phase-scoped hierarchical List and Kanban, shared search/filter state, and a consistent inspector reachable from all work surfaces.
3. **Proposal workflow:** contextual proposal form, list/count entry point, lifecycle/staleness messaging, and evidence guidance.
4. **Advanced and responsive finish:** group technical views without loss, harmonize narrow-screen behavior and keyboard states, and regression-check the Gantt and exports.

Each increment retains existing routes and domain behavior unless a tested change is explicitly required to support the approved UI. New project setup remains a separate future design and delivery cycle.

## 12. Verification expectations

Before completion of each increment:

- Existing .NET test and verification scripts pass, with `IDEAENGINEERING_ROOT` configured when tests require the local reference checkout. Missing external/local fixtures are reported as environment prerequisites, not counted as passing.
- UI contracts cover first-open and remembered-source behavior, explicit (not automatic) import, exact-SHA traceability, preservation of the previous official snapshot on failed/candidate imports, and no source write-back.
- Overview tests cover all effort-known/unknown cases and ensure completed task counts do not become the progress percentage.
- List/Kanban tests prove both views use the same card identities and filters, and that unrecorded state is not treated as not started.
- Proposal tests prove draft/ready/stale boundaries and that proposals never mutate official source actuals or the baseline.
- Browser verification checks a realistic fixture at desktop and narrow viewports, keyboard selection/close, empty/loading/error states, and the compact Gantt's dependency focus. No new browser-console errors are acceptable.
- `git diff --check` passes and the final change set contains no local project paths, private IDEAEngineering documents, employee data, credentials, or generated output.

## 13. Existing evidence and constraints

- `docs/handoff/2026-09-19-gantt-visual-redesign.md` records the approved focused Gantt behavior and verification evidence. This redesign preserves it.
- `docs/research/2026-09-18-management-apps-ux-patterns.md` records product-documentation-based interaction research and labels repository recommendations as design inferences.
- The current UI has a static vanilla HTML/CSS/JavaScript shell, a persistent summary region, eight primary view tabs, a form at the bottom of the page, and an exact-commit manifest import form. Backend routes already expose proposal listing/creation and official/preview import state; the browser currently requires the user to supply an exact commit for an official import.
- The current implementation and domain context remain the authority for data truth. This document describes presentation and the minimal import capability needed by the agreed “update from source” action; it does not redefine source authority.
