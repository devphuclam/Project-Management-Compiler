# Research: Management UI Workspace Foundation

**Date**: 2026-09-29
**Feature**: [spec.md](spec.md)

## Repository findings

Research was performed against the current local checkout on `codex/project-ui-redesign`; no subagents, network access, dependency installation, or source-repository writes were used.

| Finding | Evidence | Design consequence |
|---|---|---|
| The app already has an exact-commit manifest import endpoint and request contract. | `src/ProjectManagementCompiler/Program.cs`, `ManifestImportApiRequest`; `src/ProjectManagementCompiler/Domain/ManifestImportEntities.cs` | Preserve the exact-commit path as an Advanced/manual recovery route; add a separate default-local-ref operation that cannot accept caller-selected mode or SHA. |
| Git process calls already have an injectable `IManifestGitCommandRunner`, but the default process implementation is private to `ManifestGitObjectReader`. | `src/ProjectManagementCompiler/Sources/ManifestGitObjectReader.cs` | Extend the existing command seam and extract/share its production implementation only as necessary; test command arguments and failures through a fake runner. |
| The existing importer records outcomes through `ManifestImportApplicationService` and `CompilerApplicationState`; candidate/preview outcomes do not replace the official snapshot. | `src/ProjectManagementCompiler/Application/ManifestImport/ManifestImportApplicationService.cs`; `src/ProjectManagementCompiler/Application/CompilerApplicationState.cs` | The new operation must delegate to this existing path and preserve state ownership and failure semantics. |
| Generic compiled views are served by `GET /api/views` and `GET /api/views/{viewName}`. | `src/ProjectManagementCompiler/Program.cs` | Add Overview to `ManagementViewSet`, include it in the aggregate response, and add the `overview` named view without creating a parallel state store. |
| The existing executive progress projector only emits a project percentage with valid aggregate effort and complete delivery-card eligibility. | `src/ProjectManagementCompiler/Management/ExecutiveProgressReportProjector.cs`, `ProjectProgress`; `ExecutiveDailyGanttRow.ProgressEligibleChildCount` | Reuse/extract eligibility behavior so Overview and the executive report cannot disagree; keep completed-item count separate from effort progress. |
| Metrics can sum available actual and remaining values while effort coverage is incomplete. | `src/ProjectManagementCompiler/Management/ManagementMetricsAnalyzer.cs` | In the Overview, retain coverage count and explicitly label incomplete-coverage totals as partial; never calculate or display that partial ratio as project-wide progress. |
| The app is a .NET 10 ASP.NET Core app with a static browser UI and custom executable C# test harness. | `src/ProjectManagementCompiler/ProjectManagementCompiler.csproj`; `tests/ProjectManagementCompiler.Tests`; `scripts/test.ps1`; `scripts/verify.ps1` | Keep implementation within the current runtime and test workflow; do not add a frontend framework or test package. |
| Browser code already has intentional storage restrictions in tests; transient workbook previews are specifically not persisted. | `src/ProjectManagementCompiler/wwwroot/app.js`; `tests/ProjectManagementCompiler.Tests/XlsxPreviewUiTests.cs` | Persist only repository-root and manifest-path preferences; no project/snapshot/credential storage and no auto-import on page load. |
| A shared `ReaderFacingTextPolicy.CleanName` already removes recognized identity prefixes and formatting noise in user-facing management output. | `src/ProjectManagementCompiler/Management/ReaderFacingTextPolicy.cs`; executive Gantt/WBS/report projectors | The new Overview must reuse this policy for project, phase, milestone, and affected-work labels rather than inventing a second ID-cleanup rule. |
| Raw `ManagementAnalysis.Alerts[].Message` values currently contain English diagnostic phrasing and work-item IDs. | `src/ProjectManagementCompiler/Management/StatusAnalyzer.cs` | Do not render raw alert messages in the Overview; map only supported alert types to concise Vietnamese consequences and omit unsupported machine diagnostics from the main summary. |

## Decisions

### Use a local configured default ref, resolved atomically with import

Read the local symbolic reference `refs/remotes/origin/HEAD`, validate that its target is under `refs/remotes/origin/`, resolve it to a full commit object, then pass that exact SHA to the existing official import operation in the same HTTP request. Do not fetch, update refs, check out a commit, or silently substitute `HEAD`, the working tree, or an arbitrary branch. If the configured ref is absent or unsafe, explain that no default is available and direct the user to the existing exact-version action.

This meets the normal user need without implying internet freshness and avoids a resolve/import race. The displayed result must use the actual resolved commit stored in the import result, not the symbolic-ref string.

### Keep the Overview a projection of existing canonical truth

Add one Overview read model to the current `ManagementViewSet`. Use the compiled canonical project, its analysis, and the official reporting/as-of date. Derive current phase and next milestone only from valid source dates. Derive concerns from existing actionable analysis alerts and cap at three with deterministic ordering. Reuse the existing effort eligibility rule and clearly represent coverage. Clean every reader-facing project/work name with `ReaderFacingTextPolicy.CleanName`; preserve IDs only as secondary traceability metadata.

### Keep the workspace shell incremental

The primary loaded navigation is Overview and Gantt. Existing WBS/Kanban and specialist screens remain accessible through Advanced. Do not add a nonfunctional Work placeholder, new data model, project setup flow, or shared inspector in this increment.

### Keep the runtime and test stack unchanged

Use ASP.NET Core shared framework, browser-native HTML/CSS/JavaScript, current test executable, and current PowerShell scripts. UI behavior can be tested using existing source/contract tests; visual acceptance also requires manual browser checks.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| `origin/HEAD` is absent in a local checkout. | Fail explicitly with a recovery path to exact source selection; do not guess a branch. |
| Symbolic refs can point outside the approved remote-tracking namespace. | Validate the complete symbolic-ref target before resolving it; reject malformed or unexpected targets. |
| The default ref moves during an import. | Resolve once and pass the full commit SHA to the existing importer in the same request. |
| Partial effort appears to be total project progress. | Show eligible/total cards; null the project-wide percentage until coverage is complete; label partial sums and show completed count separately. |
| Reorganizing navigation hides existing workflows. | Inventory and test every current navigation/view/action route before moving them under Advanced. |
| The static frontend changes break Gantt behavior or narrow layouts. | Keep the existing Gantt surface and semantics; run narrow/desktop keyboard checks after UI changes. |
| Existing alert text leaks machine wording or IDs into the Overview. | Use readable names and deterministic Vietnamese consequence labels for supported alert codes; never display raw diagnostic strings in the primary summary. |

## Open research questions

None block the approved foundation increment. The source setup wizard and unified Work surface remain explicitly deferred.
