# Feature Specification: Management UI Workspace Foundation

**Feature Branch**: `codex/project-ui-redesign`

**Created**: 2026-09-29

**Status**: Approved for implementation — user approval received 2026-09-29

**Input**: User request to continue the approved management UI redesign using Spec Kit, with the approved workspace-foundation plan as the scope boundary.

## Clarifications

### Session 2026-09-29

- Q: How should primary navigation handle “Công việc” before the unified Work List/Kanban is delivered? → A: Do not show a placeholder in primary navigation; keep existing WBS/Kanban under Advanced until the later Work increment.
- Q: When effort evidence is only complete for part of the project, how should Overview present project-wide progress? → A: Withhold the project-wide percentage until every delivery card has valid effort coverage; show coverage and label any effort totals as partial.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open the current project from a local source (Priority: P1)

As a project manager, I want the app to remember where my local planning source is and let me explicitly read the newest committed version already available in that local copy, so that I can start work without learning source identifiers or accidentally importing unreviewed changes.

**Why this priority**: A clear, trustworthy entry point is necessary before any project overview or management view can be useful.

**Independent Test**: Start with no project loaded and a valid local source. Verify that the app does not read it automatically, that an explicit user action loads the valid official snapshot, and that the resulting source identity is reproducible.

**Acceptance Scenarios**:

1. **Given** a fresh browser with no saved source, **when** the user opens the app, **then** the app clearly asks them to choose or enter a local repository location and does not claim a project is loaded.
2. **Given** a previously used local source preference, **when** the user returns to the app, **then** the repository location and manifest location are available, but no source read occurs until the user explicitly requests it.
3. **Given** a selected local source with an available official committed version, **when** the user chooses “Đọc phiên bản hiện có trên máy”, **then** the app reads the newest such version available locally and reports the exact source identity used.
4. **Given** the app cannot locate the official committed version in the local source, **when** the user requests a read, **then** the app explains the problem and offers precise source-version selection without silently using another source or uncommitted changes.
5. **Given** a valid official snapshot is already loaded, **when** a later read fails or produces only a candidate/preview, **then** the valid official snapshot remains available and its authority is not confused with the unsuccessful result.
6. **Given** the default local-source action is used, **when** its result is presented, **then** the app says it used the newest committed version available in this local copy and that the online source was not checked for newer changes.
7. **Given** an official import succeeds, **when** the loaded source details are opened, **then** the commit identity and source reporting date can be found without dominating the main workspace.
8. **Given** a failed, preview, or candidate read, **when** the user reviews the outcome, **then** the app labels its authority and provides a relevant recovery action.

---

### User Story 2 - Understand the loaded project at a glance (Priority: P1)

As a sponsor or project manager, I want a concise overview of the current phase, recorded progress, next milestone, and a few actionable concerns, so that I can understand the project before opening detailed tools.

**Why this priority**: The overview is the first useful destination after a successful read and replaces the current information-heavy landing experience.

**Independent Test**: Load a controlled project fixture containing complete and partial effort coverage, phases, milestones, and attention items. Verify that a project-wide percentage appears only for complete valid coverage; otherwise the overview shows the coverage count and labels recorded effort as partial.

**Acceptance Scenarios**:

1. **Given** an official project with dated phases, **when** its overview is shown, **then** the current phase is identified relative to the official source reporting date, or the app explicitly says it cannot be determined.
2. **Given** every delivery card has valid non-negative actual and remaining effort whose sum is greater than zero, **when** project-wide recorded progress is shown, **then** it is total actual effort divided by total actual plus total remaining effort.
3. **Given** one or more delivery cards lack a valid effort pair, **when** progress is shown, **then** the project-wide percentage is explicitly unknown and the Overview shows how many cards have complete effort coverage; any effort totals are labeled as partial.
4. **Given** complete coverage includes invalid, negative, or zero-total effort for any delivery card, **when** progress is shown, **then** the project-wide percentage is unknown rather than inferred from completion state, planned percentage, or completed-item count.
5. **Given** a project with a future dated milestone/control point, **when** the overview is shown, **then** it identifies the next such point and shows its date only when that date is source-backed.
6. **Given** more than three actionable concerns, **when** the overview is shown, **then** it presents no more than three concise items, each identifying the affected work and consequence; the detailed concern remains available in its appropriate view.
7. **Given** no current phase, dated milestone, or actionable concern can be supported, **when** the overview is shown, **then** each missing item has a concise truthful empty state and no fact is invented.

### User Story 3 - Reach detailed tools without losing the simple workspace (Priority: P1)

As a project manager, I want a small set of obvious primary destinations and a clear place for less frequent technical tools, so that the app is approachable without removing capabilities I still rely on.

**Why this priority**: The redesign must reduce visual overload without breaking the existing Gantt, analysis, export, source, and readiness workflows.

**Independent Test**: From a loaded project, reach Overview and Gantt directly, then reach every existing advanced view or secondary project action through the grouped advanced area. Repeat with keyboard navigation and a narrow viewport.

**Acceptance Scenarios**:

1. **Given** an official project is loaded, **when** import completes, **then** the user lands on Overview and sees a short primary navigation for Overview and Gantt; existing WBS/Kanban remain reachable under Advanced until the unified Work destination is delivered.
2. **Given** an existing analysis, source, readiness, import, or export capability, **when** the user opens the advanced/secondary area, **then** that capability remains available and retains its existing data-authority rules.
3. **Given** the user is at a narrow viewport or uses a keyboard, **when** they navigate or open detail, **then** controls remain readable and operable without obscuring or removing the approved Gantt timeline behavior.
4. **Given** an import is loading, succeeds, fails, or returns a non-authoritative candidate, **when** the state changes, **then** the user receives an inline, distinguishable status and a recovery action where applicable.

### Edge Cases

- No saved local source exists; saved preferences are malformed or browser storage is unavailable.
- The official committed version cannot be found locally or points to an unreadable source object; there is no silent fallback.
- The newest official committed version advances while an import is being read; the displayed identity must match the exact source object actually imported.
- No official snapshot is active after app/server restart even though source preferences remain saved; the UI must not display stale project facts as loaded.
- A failed import, working-tree preview, or candidate import occurs while a valid official snapshot is active; the official snapshot must remain intact and clearly identified.
- Current phase, next dated milestone, effort evidence, or attention items are absent, conflicting, or not trustworthy.
- Actual or remaining effort is missing, invalid, negative, or totals zero.
- Effort coverage is complete for some cards but incomplete for the project; a partial effort ratio must not be presented as project-wide progress.
- The project name, phase name, milestone name, or attention text is long or contains internal identifiers; reader-facing text remains concise without destroying traceability.
- The browser viewport is narrow or the user navigates by keyboard; controls remain reachable and the Gantt retains its own timeline scrolling.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The app MUST present a clear unloaded state when no official project is active and MUST NOT claim project facts are available in that state.
- **FR-002**: The user MUST explicitly initiate every read of the selected local planning source; restoring saved preferences MUST NOT trigger a read.
- **FR-003**: The app MUST allow the user to reuse a local repository location and manifest location while persisting no credentials, project contents, or snapshot data in browser storage.
- **FR-004**: The normal source-read action MUST use the newest official committed version available in the selected local copy, without checking or implying online freshness.
- **FR-005**: The app MUST report the exact source identity used for a successful official read and make its source reporting date available in contextual source details.
- **FR-006**: If the official committed version cannot be located safely, the app MUST explain the failure and offer precise source-version selection; it MUST NOT silently fall back to another source, uncommitted content, or an inferred version.
- **FR-007**: A failed, candidate, or preview read MUST NOT replace an already valid official snapshot.
- **FR-008**: After a successful official read, the app MUST open a concise project Overview as the default destination.
- **FR-009**: The Overview MUST present the current phase when source dates support it, effort-backed recorded progress or its coverage status, the next dated milestone/control point when known, and no more than three actionable concerns.
- **FR-010**: Project-wide recorded progress MUST use total actual effort divided by total actual plus total remaining effort only when every delivery card has a valid non-negative actual/remaining pair with a positive sum; otherwise the project-wide percentage MUST be unknown and effort coverage MUST be visible.
- **FR-011**: Completed-item counts MUST be shown separately from effort-backed progress. Partial effort totals MUST be labeled as partial and MUST NOT be presented as project-wide totals or used to imply project-wide progress.
- **FR-012**: The primary loaded-project navigation MUST provide direct access to Overview and Gantt. Existing WBS/Kanban and less frequent technical views MUST remain reachable under Advanced until the unified Work destination is delivered; no nonfunctional Work placeholder may appear in primary navigation.
- **FR-013**: Import, empty, success, candidate/preview, and error states MUST be distinguishable in the interface; failures MUST include a usable recovery path when one exists.
- **FR-014**: The redesigned workspace MUST remain operable by keyboard and usable at desktop and narrow viewport widths, while preserving the approved Gantt's schedule, dependency, and timeline behavior.
- **FR-015**: Reader-facing headings and summary labels MUST use understandable project/work names instead of leading with internal identifiers. Main-summary concerns MUST use concise Vietnamese wording and MUST NOT expose raw machine codes or diagnostic strings; source identity and provenance MUST remain available in contextual details for traceability.

### Scope Boundaries

- This increment covers the unloaded/local-source entry state, concise loaded shell/navigation, and Overview only.
- The source workflow remains read-only. No source write-back, project setup wizard, or automatic promotion of proposals to official Actual evidence is included.
- Building the Work List/Kanban, shared filters and inspector, proposal workflow redesign, and broad Advanced-view redesign are later increments.
- The approved Gantt remains a working product surface; this increment does not redesign its dependency semantics or replace its existing timeline.
- This increment does not require users to set up a new project or source service.

### Key Entities *(include if feature involves data)*

- **Local Source Preference**: The reusable location of a planning repository and its manifest; contains no credentials or project snapshot.
- **Official Project Snapshot**: The last valid, authoritative project read held by the application, identified by its exact source object and reporting date; distinct from preview and candidate results.
- **Recorded Progress Summary**: A reader-facing progress value available only when every delivery card has complete valid effort coverage; otherwise it has an explicit unknown percentage, a covered-card count, and any partial effort totals are labeled as partial.
- **Project Overview**: A concise read model of current phase, recorded progress, next dated control point, and a limited set of actionable concerns for the active official snapshot.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a fresh browser session with no source preference, the app makes zero source-read attempts before the user explicitly requests one.
- **SC-002**: With a valid saved local source preference and an available official committed version, the user can start an official read with one explicit action and the result identifies the exact source object read.
- **SC-003**: In every normal source-read outcome, the interface accurately distinguishes “newest available in this local copy” from remote freshness; no normal-flow message claims the remote was checked.
- **SC-004**: In a 60-second review of a representative loaded project, a manager can identify the current phase, whether evidence-backed project-wide progress is known, the effort-coverage count when it is not known, the next dated control point when present, and the highest-priority concern when one exists.
- **SC-005**: Across the representative project fixture, every Overview percentage matches the defined effort formula only when all delivery cards have valid positive-total effort pairs; partial coverage always yields an unknown project-wide percentage and a visible coverage count.
- **SC-006**: All existing Gantt and advanced capabilities remain reachable from the loaded workspace, with no more than two deliberate navigation steps from the primary shell.
- **SC-007**: At a 360-pixel-wide viewport and a 1280-pixel-wide viewport, the start state and Overview expose no horizontally clipped primary action or inaccessible keyboard control; the Gantt timeline may scroll within its own surface.
- **SC-008**: After a failed, candidate, or preview read, the last valid official project and its source identity remain unchanged.

## Assumptions

- The primary user is a project manager or sponsor who is not expected to understand Git terminology or compiler internals.
- The planning source is already present in a local folder. The app does not download or update it.
- The existing manifest/import validation remains the authority for whether a source snapshot is official and valid.
- Browser storage may remember only repository and manifest locations. A restarted app with no active in-memory snapshot must show the unloaded state even when those preferences exist.
- Existing advanced screens, exports, readiness details, and proposal boundaries remain governed by their current contracts; this feature only reorganizes access and adds the Overview experience.
- The representative acceptance fixture is the repository's existing public real-shaped fixture; private IDEAEngineering source files are not copied into this feature's artifacts.
