# Implementation and Verification Quickstart: Unified Work

This guide is for the later implementation stage. Feature 009 is currently at plan review; this file does not authorize coding. Use the existing local toolchain and repository scripts only. Do not install packages or copy the private IDEAEngineering checkout into fixtures.

## Prerequisites

- Start from the human-approved Feature 009 specification and this reviewed plan on its planning branch.
- Existing .NET 10 SDK/runtime and Git executable.
- Existing browser and repository launcher `Run Project.cmd`.
- Public-safe reduced fixture under `tests/fixtures/ideaengineering-real-shaped`; do not use private employee/company data.
- Local app binds to loopback. `scripts/verify-web.ps1` refuses to contact another process if its verification port is occupied; use its supported `PMC_LOOPBACK_PORT` override for an isolated free port rather than targeting the user's running application.

## Test-first implementation checks

Implement later in small vertical slices. For each slice, first add the smallest failing test through the existing deep seam, run it and confirm the expected red result, then implement the minimum behavior and rerun it. Do not create a separate JavaScript/package test stack.

Focused harness command from repository root:

```powershell
dotnet run --project .\tests\ProjectManagementCompiler.Tests\ProjectManagementCompiler.Tests.csproj --no-restore
```

Focused coverage should include:

1. Work projector: arbitrary phase count; unique/missing/ambiguous current phase; stable typed identity; source/canonical hierarchy order; missing or invalid parent references; all authored states; missing record and null effective state; known numeric zero vs null; planned vs actual evidence; roles vs named-person mapping; provenance.
2. Attention contract: exactly `START_DELAY`, `OVERDUE`, `SUSPENDED`, `AT_RISK` for canonical Delivery Card targets; reject unlisted codes, non-card targets, readiness/governance, diagnostics, and arbitrary warnings. Preserve existing Overview consequence wording/behavior.
3. One collection: List and Kanban use identical card-key sets exactly once before and after shared filters. Verify ancestor-only display rules, search normalization/allowed fields, temporary expansion, per-mode scroll, and all group counts after active filters including zero counts.
4. State/inspector: state groups versus separate `Chưa ghi nhận`; suspended/cancelled groups; selected key vs inspector visibility vs focus target vs scroll; selection reset only after a different successful official `snapshotId`.
5. API/UI: aggregate contains additive `work`; named `/api/views/work` returns the same projection; no-project error remains `NO_PROJECT`; primary navigation/default List; shared inspector; keyboard focus and Work ↔ Gantt integration; responsive group selector; legacy routes remain reachable.
6. Regression: existing Overview, Gantt schedule/dependency, execution truth, WBS, legacy Kanban, and source/preview tests remain green.

Use small in-memory canonical fixtures for synthetic edge cases. The public-safe real-shaped fixture may prove hierarchy completeness and legacy parity shape; its current six-phase/35-package/53-card counts must not become UI or product constants.

## Repository checks

After implementation slices, and again for the complete increment, run:

```powershell
.\scripts\test.ps1
.\scripts\verify.ps1
```

Run `scripts/verify-web.ps1` when its local app DLL has been built and a safe loopback port is available. Preserve the script's refusal behavior when the chosen port is already occupied; do not let verification silently exercise an unrelated running app.

No tests or runtime verification are claimed by this plan-only artifact.

## Manual browser verification

1. Launch the application with `& ".\Run Project.cmd"` from the repository root. Use the normal source-import flow to load an official project; do not use a candidate/working-tree preview as official Work evidence.
2. Confirm primary navigation is `Tổng quan | Công việc | Gantt`; opening Công việc selects List, defaults to All phases, and focuses/opens the uniquely resolved current phase without hiding other phases.
3. Compare List and Kanban card identities. Change phase scope, search, state, and attention filters in List; switch to Kanban and back; verify the shared result state persists and counts are recomputed after filters.
4. Verify search using a Vietnamese name with and without diacritics, stable ID, parent Phase, and parent Work Package. Verify paths contain only surviving results when result filters are active and clearing search restores expansion.
5. Confirm missing execution state is in `Chưa ghi nhận`, explicit `NOT_STARTED` remains `Chưa bắt đầu`, and actual/planned zero/null values remain distinct. Confirm the baseline finish label is `Kết thúc kế hoạch` or equivalent, never a deadline label.
6. Inspect a card selected from List, Kanban, and Gantt. Confirm the same stable identity; read-only supported fields; direct predecessor/successor direction; source evidence; and no proposal/edit actions in this inspector. Verify only `IncludedInAnalysis == true` dependency edges appear as primary links; excluded traceability/invalid/unsupported edges are not promoted and no transitive impact is shown. Test a supported and unsupported alert and a role with no concrete person.
7. Close the inspector by button and Escape. Verify selection persists while focus returns to its invoking row when available, otherwise to the approved fallback. Filter a selected item out and confirm no silent scope widening.
8. Navigate Work → Gantt → Work. Verify Gantt focuses the matching card, inspector open/closed state is preserved, Work filters remain unchanged, and excluded targets are explained.
9. Change official project snapshot, make a failed import attempt, and open a candidate preview in controlled test state. Verify only successful official identity change clears selection/inspector context and the previous official project survives failure according to Feature 008.
10. Test keyboard-only operation and layouts at 360px and 1280px. On narrow Kanban, verify one labeled state group at a time and visible counts for every group, including `Chưa ghi nhận`. Ensure no required control is clipped and no action requires dragging.
11. Confirm legacy WBS and Kanban remain under Advanced until their explicit identity/state/filter/inspector parity gates are evidenced. Do not leave two Kanban products as equivalent normal primary destinations after parity.

## Public repository hygiene

Before any future commit/push, inspect `git status` and staged diff; include only intended source/docs/tests. Check for personal paths, private IDEAEngineering documents, employee data, credentials, secrets, generated output, build artifacts, and package-lock changes. The public fixture must remain synthetic/minimal. The planning branch may be pushed for the requested human/AI plan review; do not merge it to `main` until the human plan approval gate passes.
