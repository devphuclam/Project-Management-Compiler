# Implementation and Verification Quickstart: Unified Work

This guide supports the approved Feature 009 implementation. User Story 5 and the T028 final-verification checkpoint are recorded below. Use the existing local toolchain and repository scripts only. Do not install packages or copy the private IDEAEngineering checkout into fixtures.

## Prerequisites

- Start from the human-approved Feature 009 specification and implementation plan on the implementation branch.
- Existing .NET 10 SDK/runtime and Git executable.
- Existing browser and repository launcher `Run Project.cmd`.
- Public-safe reduced fixture under `tests/fixtures/ideaengineering-real-shaped`; do not use private employee/company data.
- Local app binds to loopback. `scripts/verify-web.ps1` refuses to contact another process if its verification port is occupied; use its supported `PMC_LOOPBACK_PORT` override for an isolated free port rather than targeting the user's running application.

## Test-first implementation checks

For each production slice, add the smallest failing test through the existing deep seam, confirm the expected red result, implement the minimum approved behavior, then rerun the focused tests. Do not create a separate JavaScript/package test stack.

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

Use small in-memory canonical fixtures for synthetic edge cases. The public-safe real-shaped fixture may prove hierarchy completeness and legacy parity shape. Fixture cardinalities are test data only and must not become UI or product constants.

## Repository checks

After implementation slices, and again for the complete increment, run:

```powershell
.\scripts\test.ps1
.\scripts\verify.ps1
```

Run `scripts/verify-web.ps1` when its local app DLL has been built and a safe loopback port is available. Preserve the script's refusal behavior when the chosen port is already occupied; do not let verification silently exercise an unrelated running app.

The parity evidence below distinguishes synthetic projection/test coverage from live imported-project evidence. It does not claim completion of T028 or overall Feature 009 verification.

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

## T028 final verification evidence record

Automated parity checks use the public-safe synthetic canonical fixture. Live evidence below was collected from one local, read-only official import on 2026-09-30. The repository record intentionally omits the private source checkout path, source commit SHA, source-project files, and employee/company material. Snapshot-specific counts are observations, not product constants.

### Work List ↔ Unified Kanban live matrix

- The imported snapshot contained 53 Delivery Cards across its canonical hierarchy (six phases, 35 Work Packages, and seven control points in this snapshot). List and Unified Kanban each rendered the same 53 unique typed Delivery Card identities; no milestone or decision point appeared as a Work card.
- Search matched reader-facing card names, stable IDs, parent Phase names, and parent Work Package names. A reader-facing name query returned both matching cards (`P01` and `L03-A`); stable ID `P03` returned only `P03`; a Phase-name query returned its seven descendants; and a Work Package-name query returned its one descendant. Case-insensitive and Vietnamese-diacritic-insensitive queries returned the same results as their normalized forms.
- Explicit `PH0` scope returned its seven cards. Combining explicit `PH1` scope with a query matching only `PH0` returned zero in both views; scope remained `PH1` and was not widened. A commit-hash-shaped query and the machine alert code `OVERDUE` returned no cards, confirming those unsupported fields/codes are not search text.
- `IN_PROGRESS` with unrecorded excluded returned `P01`; `COMPLETED` with unrecorded excluded returned zero. Needs Attention returned zero for this snapshot and showed the supported-signal empty-result explanation rather than claiming the project was risk-free. Excluding unrecorded returned the one authored card; including unrecorded returned all 53, with the other 52 in the separate `Chưa ghi nhận` group. The include control is not an “unrecorded only” filter.
- Each exercised scenario produced the same identity set in List and Kanban. Group counts reflected active scope/search/filters, displayed all six groups including zero counts, and did not count Phase or Work Package ancestors. For the search result containing `P01` and `L03-A`, List displayed only their PH0/P01 and PH4/L03 ancestor paths; it displayed no orphan ancestors. Temporary search-driven expansion was removed on clear and the prior manual expansion state was restored.
- A live no-results case (explicit `PH1` plus a PH0-only query) retained its scope and offered query recovery. Every phase in this imported snapshot had cards, so a genuinely empty selected phase was not available for live exercise; the automated regression `WorkFilteredListHasScopedEmptyRecoveryAndExpansionRestoration` covers the distinct `empty-phase` and `no-results` states.

### WBS and legacy Kanban parity

- **WBS: PASS.** Live Work and legacy WBS each contained 53 unique typed Delivery Card identities: no missing or extra identity. Phase → Work Package ancestry matched exactly; WBS showed six phases and 35 Work Packages for this snapshot. Milestones/control points remained outside Work. WBS remains available under Advanced.
- **Legacy Kanban identity/state: PASS.** It contained the same 53 unique cards. State comparison had no semantic mismatch: `P01` was `Đang làm` in Unified Kanban and `Đang thực hiện` in legacy (both `IN_PROGRESS`); the other 52 were `Chưa ghi nhận` in Work and `Unknown` in legacy. Both missing-record representations remained distinct from `NOT_STARTED`; legacy `Chưa bắt đầu` count was zero.
- **Legacy controls: N/A where unsupported.** The existing legacy Kanban exposes no phase scope, search, or shared filter controls, so no such behavior was claimed equivalent or added to legacy. Its cards are static articles and do not open an inspector. This is an **intentional, documented transition difference**: Unified Work provides the stable-identity shared inspector; legacy Kanban remains reachable under Advanced for specialist parity/audit until the applicable transition gate is accepted. No legacy inspector behavior was fabricated, and no legacy route was retired.

### Work ↔ Gantt navigation

- **First navigation: PASS.** Before visiting Gantt in this app session, selecting `P01` in Work with its inspector open and invoking `Xem trên Gantt` navigated to the exact `DeliveryCard:P01`. The Gantt row was selected, keyboard-focused, and visible in the viewport; its visible PH0 ancestor was opened. The Gantt inspector remained open, matching the Work inspector's prior open state.
- **Closed inspector: PASS.** With Work selection retained and its inspector closed, navigating to the Gantt primary destination left the Gantt inspector closed and kept the selected row visible; selection and drawer visibility remained separate.
- **Different/filtered card: PASS.** `P02` was selected via the Work inspector CTA and resolved to `DeliveryCard:P02`. Applying Gantt's `COMPLETED` filter hid the row without clearing the selected P02 identity or closing/replacing its details; the Gantt filter remained unchanged. Returning through `Mở trong Công việc` restored the prior Work scope (`PH0`), query (`P02`), authored-state/attention/unrecorded criteria, selection, and open inspector.
- Gantt's existing default Plan view does not render Work Package rollup rows. Therefore this verification confirms the visible Phase ancestor and the target Delivery Card; it does not claim a Work Package row was revealed or alter Gantt behavior.

### Responsive and keyboard evidence

- **360px: PASS.** Page-level document width remained within the viewport. Work search/scope/state/attention/unrecorded controls remained operable. Each of the six Kanban groups was selected and exactly one narrow group was shown at a time; the summary kept counts for every group, including `Chưa ghi nhận`. The Work inspector occupied the full viewport. Enter/Space opened it, Escape closed it, the selected identity persisted, and focus returned to the invoking P02 row/card in both List and Kanban.
- **1280px: PASS.** List and Kanban each displayed the shared 53-card collection without page-level horizontal clipping; the inspector was adjacent to the collection. Keyboard selection/inspection and Work ↔ Gantt navigation were exercised at this width.
- Viewport override was reset after verification. The user's existing application on port 5050 was not used as a verification server or disrupted.

### Automated verification gates

All commands below completed successfully against the unchanged implementation code:

| Gate | Result |
| --- | --- |
| Full Node regression suite (`node --test` over `*.test.cjs`) | **PASS — 17/17** |
| Full C# executable harness (`dotnet run --project .\tests\ProjectManagementCompiler.Tests\ProjectManagementCompiler.Tests.csproj --no-restore`) | **PASS** |
| `scripts/test.ps1` (Node and full C# harness) | **PASS** |
| `node --check src\ProjectManagementCompiler\wwwroot\app.js` | **PASS** |
| `dotnet build .\ProjectManagementCompiler.sln --no-restore` | **PASS — 0 warnings, 0 errors** |
| `scripts/verify.ps1` | **PASS** — isolated loopback port 5154; Health `ok`, official manifest classification, 53 cards, workbook/report checks, `SecurityChecks=PASS` |
| Standalone `scripts/verify-web.ps1` | **PASS** — isolated loopback port 5155; Health `ok`, official manifest classification, 53 cards, `SecurityChecks=PASS` |
| `git diff --check` | **PASS** before this evidence-only documentation update; rerun before commit |

For the safe restore used by `verify.ps1`, the project files were checked for external package references and restore-source overrides. No package references were present. Restore was constrained to a newly created empty local feed with NuGet audit disabled; the effective MSBuild properties were checked before running verification. No public package registry was contacted. The empty temporary feed remains outside the repository and is not part of this commit. An initial standalone C# invocation without the local source-root environment was not valid test evidence; it was rerun with the real local read-only checkout configured and passed. No source checkout path or SHA is recorded here.

### Fresh Feature 009 acceptance ledger

`PASS` means the approved requirement has automated regression evidence and, where applicable, the live checks above. Dataset-specific behaviors not present in the imported snapshot are backed by synthetic regression coverage and are explicitly identified above.

| Requirements | Status | Evidence grouping |
| --- | --- | --- |
| FR-001–FR-007 | **PASS** | Workspace/navigation, All-phases/current-focus, arbitrary hierarchy, canonical ordering and explicit phase scope. |
| FR-008–FR-013 | **PASS** | Shared typed identity and state, transient Work criteria, List labels/columns, reader-facing names, planned-finish wording, source authority. |
| FR-014–FR-019 | **PASS** | Search fields/normalization, ancestor matching, filtered paths, expansion restoration, scope-safe recovery and distinct empty states. |
| FR-020–FR-025 | **PASS** | Authored/unrecorded separation, deterministic groups, post-filter counts and visible zero groups. |
| FR-026–FR-033 | **PASS** | Kind-appropriate read-only inspector, evidence semantics, dependencies, selection/focus/scroll and stable Work ↔ Gantt navigation. |
| FR-034–FR-036 | **PASS** | Narrow/full-screen inspector, 360px/1280px and keyboard behavior. |
| FR-037–FR-039 | **PASS** | Exact supported Needs Attention whitelist, exclusions, authored-state separation and non-risk-free empty wording. |
| FR-040–FR-043 | **PASS** | WBS/legacy routes and parity transition, no new store/write-back/proposal workflow, existing Advanced proposal route. |
| SC-001–SC-011 | **PASS** | Automated scenarios plus live identity/filter/state/ancestry, ordering/count, inspector/navigation, responsive and accounted legacy differences. |

No requirement is marked `FAIL` or `BLOCKED-NOT VERIFIED`. The only live-data limitation is that this official snapshot has no empty phase; that branch is covered by regression, not claimed as a live observation. Legacy-only scope/search/filter and inspector parity are classified as unsupported/intentional transition differences above, not as equivalent behavior.

**T028 final result: PASS.** The implementation branch remains a review branch; keep WBS and legacy Kanban under Advanced and do not merge to `main` until human review approves this final verification record.

## Public repository hygiene

Before any commit/push, inspect `git status` and staged diff; include only intended source/docs/tests. Check for personal paths, private IDEAEngineering documents, employee data, credentials, secrets, generated output, build artifacts, and package-lock changes. The public fixture must remain synthetic/minimal. Push the implementation branch for the requested human code review, but do not merge it to `main` until this checkpoint is approved.
