# Feature Specification: Read-only XLSX Gantt Preview Import

**Feature Branch**: `codex/excel-gantt-export`

**Created**: 2026-09-20

**Status**: Approved for planning

**Input**: User description: "Allow Project Management Compiler to import a
PMC-generated CARIO + Gantt workbook and show it as a read-only, non-authoritative
preview without changing the official source project."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View an exported workbook in the compiler (Priority: P1)

As a project manager, I want to choose a workbook exported by Project
Management Compiler and see its daily Gantt and task information in the app so
that I can review the exported artifact without leaving the management
workspace.

**Why this priority**: The exported workbook is currently a one-way artifact.
Read-only preview is the smallest useful round-trip that lets a user confirm
what was exported while preserving the source-of-truth boundary.

**Independent Test**: With a valid workbook exported by the compiler, choose the
file and confirm that the app shows the workbook project identity, task table,
daily date axis, and Gantt lanes in a clearly labelled preview mode.

**Acceptance Scenarios**:

1. **Given** a valid compiler workbook, **when** the user chooses it from the
   source-intake panel, **then** the app shows an `XLSX Preview` mode with the
   project identity and the workbook's task table.
2. **Given** a valid compiler workbook with a daily Gantt sheet, **when** the
   preview loads, **then** the app shows the exported date axis and PLAN,
   ACTUAL, ALERT, and MILESTONE lanes without changing their displayed values.
3. **Given** an active XLSX preview, **when** the user selects `Official source`,
   **then** the app returns to the currently loaded official project views.

---

### User Story 2 - Reject untrusted workbook content (Priority: P2)

As a project manager, I want an invalid or altered workbook to be rejected
clearly so that a plausible-looking spreadsheet cannot be mistaken for an
official or compiler-generated project snapshot.

**Why this priority**: The preview is useful only if its trust boundary is
explicit. Fail-closed validation prevents partial or fabricated Gantt data from
being presented as a valid export.

**Independent Test**: Submit workbooks with missing sheets, missing or wrong
provenance markers, malformed required cells, duplicate task identities, and
invalid package structure; confirm each is rejected with diagnostics and that
the current official project remains unchanged.

**Acceptance Scenarios**:

1. **Given** a workbook that is missing a required sheet or marker, **when** the
   user attempts to import it, **then** the app rejects the workbook and names
   the validation problem.
2. **Given** a workbook with malformed, tampered, oversized, or unsupported
   content, **when** the user attempts to import it, **then** the app does not
   render a partial preview and retains the last valid official state.
3. **Given** a failed workbook import while a valid preview already exists,
   **when** validation fails, **then** the existing valid preview remains
   available and the failed file is not substituted.

---

### User Story 3 - Preserve source authority and preview lifecycle (Priority: P3)

As a project manager, I want the imported workbook to remain a temporary,
read-only preview so that it cannot alter official source execution, analysis,
alerts, or local execution proposals.

**Why this priority**: The compiler's central trust model requires authoritative
source data and local proposals to remain separate from derived views and output
artifacts.

**Independent Test**: Load an official project, create or list its proposals,
import a valid workbook, switch between modes, import a new official manifest,
and restart the app; confirm that official data and proposals are unchanged and
the preview lifecycle follows the defined rules.

**Acceptance Scenarios**:

1. **Given** an official project and local proposals, **when** a valid workbook
   preview is imported, **then** official project data, source execution,
   analysis, alerts, and proposals remain unchanged.
2. **Given** an active XLSX preview, **when** the user views it, **then** the
   execution updater is unavailable and the preview is labelled read-only and
   non-authoritative.
3. **Given** an active XLSX preview, **when** the user imports a new official
   manifest or restarts the application, **then** the old XLSX preview is no
   longer active.

---

### Edge Cases

- A non-`.xlsx` file is selected.
- The file is not a valid ZIP/Open XML workbook.
- A required worksheet is missing, duplicated, or renamed.
- The workbook has no supported PMC provenance marker or has an unsupported
  contract version.
- Required headers are missing, reordered in an unsupported way, or contain
  malformed values.
- The daily axis is empty, contains invalid dates, or contains duplicate dates.
- Task identities are duplicated or a Gantt row has no matching exported task
  identity where one is required.
- The workbook exceeds the application upload or package-size ceilings.
- The user imports an invalid workbook while a valid official project or valid
  preview is already loaded.
- The user imports an official manifest after loading a preview.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow the user to choose a compiler-generated
  CARIO + Gantt workbook from the source-intake UI.
- **FR-002**: The system MUST accept only the supported compiler workbook
  contract, including all seven required worksheets and the supported PMC
  provenance marker and contract version.
- **FR-003**: The system MUST display a successful import as a distinct
  `XLSX Preview` mode marked `Read-only` and `Non-authoritative`.
- **FR-004**: The preview MUST show the workbook project identity, exported task
  table, daily date axis, and exported Gantt lanes.
- **FR-005**: The preview MUST preserve the workbook's displayed date, lane,
  status, percentage, evidence, and source-reference values; it MUST NOT infer
  missing actuals or recalculate the schedule.
- **FR-006**: The system MUST reject invalid, incomplete, tampered, malformed,
  unsupported, or oversized workbooks before displaying any preview rows.
- **FR-007**: A failed workbook import MUST leave the current official project
  and any previously valid preview unchanged.
- **FR-008**: The system MUST keep preview data separate from the official
  project, source execution, analysis, alerts, and local execution proposals.
- **FR-009**: The system MUST make execution proposal actions unavailable while
  the user is viewing an XLSX preview.
- **FR-010**: The system MUST allow the user to return explicitly to the
  official source view and to clear the active preview.
- **FR-011**: A new official manifest import MUST clear the active XLSX preview.
- **FR-012**: The system MUST NOT persist or rehydrate an XLSX preview across
  application restarts.
- **FR-013**: The system MUST provide actionable diagnostics for each rejected
  workbook without writing to the source repository or uploaded file.
- **FR-014**: The system MUST enforce upload and package safety ceilings that
  cannot be raised by a workbook or client request.

### Key Entities

- **XLSX Preview**: A temporary, read-only projection of one validated compiler
  workbook, including project identity, task rows, date axis, Gantt lanes, and
  provenance details.
- **Workbook Contract**: The supported identity, worksheet, header, marker,
  date, and value rules required for a compiler workbook to qualify for preview.
- **Preview Mode**: The UI mode that presents the XLSX Preview separately from
  the official source project and disables proposal actions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a valid compiler workbook within the supported size ceiling,
  the user sees the preview identity, task table, and daily Gantt within 5
  seconds of choosing the file on the local application.
- **SC-002**: 100% of validation fixtures with a missing sheet, invalid marker,
  malformed required value, corrupt package, duplicate identity, or size-limit
  violation are rejected without rendering partial workbook rows.
- **SC-003**: In 100% of preview-import regression scenarios, the official
  project, source execution, analysis, alerts, and proposals remain unchanged.
- **SC-004**: In 100% of lifecycle scenarios, a new official manifest import or
  application restart removes the active XLSX preview.
- **SC-005**: A reviewer can identify the preview as read-only and
  non-authoritative from the UI without relying on hidden implementation
  details.

## Assumptions

- Users have access to the local Project Management Compiler UI and a workbook
  previously exported by the compiler.
- Only the compiler's supported seven-sheet workbook is in scope; arbitrary
  third-party Excel Gantt layouts are out of scope.
- The preview is intentionally session-only and is not a second persistence
  format for canonical project state.
- A workbook exported before the provenance marker is introduced must be
  exported again before it can qualify for preview.
- The implementation uses the existing restricted local runtime and does not
  add packages, connectors, databases, Docker, or Office automation.
