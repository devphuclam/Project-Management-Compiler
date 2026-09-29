# Management UI Workspace Foundation Implementation Plan

**Required subskill:** `superpowers:executing-plans` (recommended: native, single-agent implementation; no subagents)

**Goal:** Deliver the first approved management-UI increment: an explicit local-source start screen, a compact loaded-project shell, and a concise Overview whose progress and source freshness claims are evidence-backed.

**Architecture:** Keep the existing ASP.NET Core application and vanilla HTML/CSS/JavaScript client. Add a read-only default-branch resolver over the existing Git command abstraction, and route the primary local-source action through it into the existing exact-commit importer in one server request. Project the Overview from canonical project and execution evidence, sharing existing progress semantics rather than deriving completion from card counts. Reorganize the existing page progressively; preserve all current advanced routes, exports, proposal boundaries, and the approved Gantt behavior.

**Tech stack:** .NET 8 / ASP.NET Core, existing C# test-runner project, vanilla JavaScript, HTML, and CSS. No new runtime or test dependencies.

**Written design:** [2026-09-28-management-ui-redesign-design.md](../specs/2026-09-28-management-ui-redesign-design.md)

## Global Constraints

- Keep the current ASP.NET Core host and vanilla HTML/CSS/JavaScript approach. Do not add a frontend framework or dependency for this redesign.
- The default-branch action reads refs and objects already present in the selected local checkout. It must not run `git fetch`, contact the remote, or update Git metadata.
- Describe the resolved commit as the newest default-branch commit available in this local copy; do not imply GitHub freshness was checked.
- A valid official import alone may replace the official snapshot. Failed, candidate, preview, and capture-error imports retain the last valid official snapshot.
- The importer stays read-only with respect to the source repository. A working-tree capture is a non-authoritative preview.
- Recorded progress is actual effort / (actual effort + remaining effort), only when both values are valid and the denominator is greater than zero. Otherwise show progress as unknown. Completed-card counts are separate and never substitute for progress.
- Keep plan, actual, proposal-only scenarios, and derived alerts distinct. Unknown execution is not zero or “not started.”
- Persist only the local repository-root and manifest-path preferences in browser storage. Never persist credentials, project contents, or source snapshots there; never auto-import on page load.
- Preserve the existing Gantt schedule/dependency semantics and approved visual behavior. Keep source IDs and provenance available contextually, not as primary labels.
- The public repository must not receive private IDEAEngineering material, local absolute paths, employee data, credentials, or generated output.

## Review Focus: Five Main Failure Classes

1. **False freshness or mutation:** a resolver accidentally contacts origin, changes refs, follows an unsafe ref, or labels local data as remotely current.
2. **Official-state loss:** an invalid or preview import replaces the last valid official snapshot, or the new route bypasses existing import classification/validation.
3. **Misleading progress:** task counts, plan percentages, invalid effort, or missing evidence are presented as actual progress.
4. **UI regression / hidden capability:** shell cleanup breaks an existing view, export, readiness flow, source detail, keyboard path, or the approved Gantt.
5. **Privacy and accessibility regressions:** browser storage contains more than allowed, a page claims a project is loaded when it is not, or responsive/keyboard/error states become unusable.

## Implementation Tasks

### 1. Add failing tests for local default-branch resolution

**Files:** new `tests/ProjectManagementCompiler.Tests/ManifestDefaultBranchResolverTests.cs`; `tests/ProjectManagementCompiler.Tests/Program.cs`.

- Test a valid local `refs/remotes/origin/HEAD` symbolic target resolving to a full commit SHA.
- Test missing symbolic ref, malformed/unexpected ref, non-commit object, Git execution failure, cancellation, and bounded/invalid output as explicit failures; there is no fallback to `HEAD`, working tree, or a hard-coded branch.
- Assert the resolver invokes only read-only Git commands (symbolic-ref and rev-parse); it never invokes fetch, remote update, checkout, reset, or ref-writing commands.
- Use an injected fake `IManifestGitCommandRunner`; no network, credentials, or IDEAEngineering checkout is needed.
- Run the focused test and confirm it fails before implementation.

### 2. Implement and register the read-only resolver

**Files:** new resolver/result types under `src/ProjectManagementCompiler/Sources/`; existing Git runner and source-reader composition; `src/ProjectManagementCompiler/Program.cs` service registrations.

- Inject `IManifestGitCommandRunner`; production uses the existing process runner, while tests use the fake.
- Resolve the repository's configured local default branch through `refs/remotes/origin/HEAD`, validate the symbolic target as a safe full ref under `refs/remotes/`, then resolve and validate its commit object to a full SHA.
- Return a typed success/failure result with a safe user-facing recovery action; do not include raw stderr, credentials, or absolute paths in responses.
- Keep the existing exact-SHA import path unchanged and share runner registration where practical rather than introducing a second Git process implementation.
- Run focused resolver tests and the existing manifest capture tests.

### 3. Add one atomic default-branch import path

**Files:** `src/ProjectManagementCompiler/Application/ManifestImport/ManifestImportApplicationService.cs`; `src/ProjectManagementCompiler/Program.cs`; manifest-import tests.

- Add a default-branch import operation that resolves the SHA and passes that exact SHA to the existing importer within the same server request; return the resolved SHA and existing classification/snapshot response.
- Preserve request size limits, analysis-as-of behavior, mapping behavior, importer validation, and `CompilerApplicationState.RecordManifestImport` semantics.
- On resolver or import failure, prove the previously loaded official snapshot remains unchanged. Candidate/preview classification remains non-authoritative.
- Add API/application tests using fakes. Assert the exact resolved SHA reaches the importer and no alternate capture mode is selected.
- Run the focused tests and existing manifest-import regression tests.

### 4. Add a minimal, truthful Overview projection

**Files:** `src/ProjectManagementCompiler/Management/` overview model/projector or shared progress helper; `src/ProjectManagementCompiler/Management/ManagementViewProjector.cs` or the narrow overview API in `src/ProjectManagementCompiler/Program.cs`; related tests.

- Project project identity, current phase as of the source reporting date, next dated milestone, up to three useful attention items, completed-card count, and effort-backed recorded progress.
- Reuse the current executive progress rules through a shared pure calculation or equivalent centralized logic; do not create a second divergent percentage formula.
- If actual/remaining effort is missing, negative, invalid, or sums to zero, return an explicit unknown-progress state. Do not infer 100% from a completed state or divide completed cards by total cards.
- Handle absent official reporting date/milestone honestly for non-official or incomplete project states; do not fabricate a date or phase.
- Test effort-known and effort-unknown cases, rounding, zero denominator, invalid evidence, current-phase boundary, next milestone, and attention cap/link targets.

### 5. Restructure the HTML shell and source-intake states

**Files:** `src/ProjectManagementCompiler/wwwroot/index.html`; relevant static UI contract tests.

- Create a focused unloaded state that explains the selected local source and has one explicit primary action: “Đọc phiên bản hiện có trên máy”. Keep exact-SHA, working-tree preview, alternate manifest path, as-of override, legacy/JSON/XLSX paths accessible under clearly named advanced/disclosure areas.
- Never trigger an import automatically on initial load or when restoring preferences.
- After official import, present a compact project identity/header and primary navigation: Overview, Công việc placeholder/view entry for the later increment, and Gantt. Group existing technical views and secondary tools under Advanced without deleting their entry points.
- Make Overview the post-import destination. Keep export actions discoverable but subordinate to the primary navigation.
- Add accessible labels, landmarks, focus order, disclosure semantics, and inline loading/error/success messages; retain stable DOM hooks needed by existing `app.js` behavior until migrated.
- Tests assert essential existing actions remain available and no screen copy claims a project is loaded before a successful import.

### 6. Implement the compact shell and explicit import flow

**Files:** `src/ProjectManagementCompiler/wwwroot/app.js`; `src/ProjectManagementCompiler/wwwroot/index.html`; relevant UI contract tests.

- Store only repository-root and manifest-path preferences in browser storage. Treat malformed/unavailable storage as a recoverable condition and continue without persistence.
- Keep exact SHA as traceable imported-source detail, not a required value in the normal primary flow. Advanced exact-SHA import remains available.
- Primary action posts to the new default-branch operation. Show its local-only freshness caveat before/with the result; on success show resolved SHA and source date in secondary source details.
- Loading, no saved path, resolver failure, import failure, candidate, preview, and successful official import each have distinct inline copy and recovery actions.
- Keep the previous official project visible after a failed or non-authoritative import and make the result's authority explicit.
- Route successful official imports to Overview; preserve existing route/view behavior for technical screens and avoid duplicate summary rendering.
- Verify no sensitive or project snapshot data is written to browser storage using static/behavioral tests.

### 7. Apply the visual system and responsive behavior

**Files:** `src/ProjectManagementCompiler/wwwroot/styles.css`; focused UI tests where available.

- Follow a single-line “Design Read” before editing CSS: the target is a practical, information-dense project workspace, not a marketing page; use the applicable taste-skill principles (hierarchy, restraint, readable typography, clear states) while preserving the existing vanilla stack.
- Establish restrained navigation, spacing, typography, and surface tokens using the existing application palette. Avoid decorative gradients, repetitive metric cards, acronym-heavy headings, and dense permanent warning banners.
- Make the unloaded and Overview states work at laptop and narrow widths. Advanced disclosure and navigation remain operable; Gantt keeps its own horizontal timeline and focused dependency treatment.
- Ensure focus-visible, selected, hover, disabled, loading, error, and success states have sufficient contrast and are not color-only.
- Do not alter approved Gantt dependency geometry as part of this increment except for necessary shell sizing; any such change must have a regression test and screenshot/browser check.

### 8. Verify the complete foundation increment and prepare review

**Files:** tests and verification scripts only as needed; no generated reports or local source data committed.

- Run `powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1` and `powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1` with `IDEAENGINEERING_ROOT` set only in the local process environment when needed; report missing fixture prerequisites rather than counting them as pass.
- Run `git diff --check` and the C# UI/import contracts.
- Run the application against the public real-shaped fixture and perform a browser pass at desktop and narrow widths: first open, remembered path without auto-import, explicit import, Overview truth states, source-freshness message, import error retaining the official project, keyboard navigation, and Gantt regression.
- Inspect browser console and network behavior; confirm the primary local action does not contact GitHub or mutate source checkout metadata.
- Inspect the final diff/status and scan for absolute local paths, credentials, private IDEAEngineering content, private employee/company data, generated files, and unrelated changes.
- Commit coherent implementation slices on the feature branch with meaningful messages. Do not push or merge before applicable verification and the user's explicit review of the increment.

## Out of Scope for This Plan

- Building the hierarchical Work List/Kanban and shared filters/inspector (increment 2).
- Building or revising local execution-proposal workflows (increment 3).
- Advanced-view reorganization beyond preserving access, broad responsive cleanup, and final cross-surface polish (increment 4).
- New-project setup wizard, direct source editing/write-back, drag-and-drop state mutation, framework migration, or dependency installation.

## Approved Interpretation

The saved-source policy is intentionally conservative: browser storage remembers only repository root and manifest path. After a cold application restart, the UI must not claim an official snapshot is still loaded unless the server actually has it. No snapshot contents or identity will be persisted in browser storage under this plan.
