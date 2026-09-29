# Implementation Plan: Management UI Workspace Foundation

**Branch**: `codex/project-ui-redesign` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/008-management-ui-foundation/spec.md`

## Summary

Deliver the first incremental UI redesign: make local source reading explicit and truthful, open an understandable Overview after a valid official import, and simplify loaded-project navigation without removing current work tools. Resolve the local configured official source ref to one exact commit in the same request that imports it; do not fetch or update the source checkout. Use the existing canonical project, authority classification, and executive-progress evidence rules. Withhold project-wide progress until every delivery card has a valid effort pair, showing eligible-card coverage and labeling subset totals when coverage is incomplete.

## Technical Context

**Language/Version**: C# on .NET 10.0; browser-native JavaScript, HTML, CSS.

**Primary Dependencies**: Existing ASP.NET Core shared framework and repository code only; no new packages or frontend framework.

**Storage**: In-memory application state for the loaded official/candidate snapshots; browser local storage for repository-root and manifest-path preferences only.

**Testing**: Existing C# executable test harness (`tests/ProjectManagementCompiler.Tests`); `scripts/test.ps1` and `scripts/verify.ps1`; manual browser verification on existing local app. No new test dependency.

**Target Platform**: Local Windows-hosted ASP.NET app in a browser; desktop and narrow viewport use.

**Project Type**: Existing single-project local web application with source, tests, and Spec Kit artifacts in this repository.

**Performance Goals**: No new performance SLA. Preserve existing bounded source-capture limits and cancellation behavior; the new ref lookup must be local-only and must not add a network wait.

**Constraints**: Exact-source reproducibility; source read-only; failure preserves prior official snapshot; no automatic import; only two approved browser preferences; support the existing public real-shaped project fixture; no secrets/private source material in artifacts; no installation or external-service requirement.

**Scale/Scope**: One local planning repository per import; current representative project shape is 6 phases, 35 work packages, 53 delivery cards, and 7 control points. This increment does not increase importer capacity or redesign the later Work/Proposal/Advanced increments.

## Constitution Check

### Before Phase 0

| Principle | Gate | Rationale |
|---|---|---|
| Canonical model before views | PASS | Overview and all retained views consume the existing neutral canonical project and analysis; source-specific Git resolution stays in the source adapter seam. |
| Baseline and evidence are first-class | PASS | The plan preserves official/candidate/preview boundaries, exact source identity, sparse evidence, and unknown progress. |
| Deterministic extraction | PASS | No inference or AI is introduced; the new import path resolves an existing local ref to an exact object and invokes current validation. |
| Test through deep interfaces | PASS | Resolver tests inject `IManifestGitCommandRunner`; import and Overview tests exercise service/projector boundaries. |
| Restricted-environment delivery | PASS | No containers, package installation, registry, runtime installation, external API, or credentials. |
| Explicit scope and safe failure | PASS | Missing/unsafe local ref fails with recovery guidance; all current advanced tools stay accessible. |
| Design/specification before implementation | PASS | Spec, plan, task graph, and cross-artifact analysis are created before any code change. |

No constitution violation requires complexity justification.

### Post-design re-check

All gates remain PASS. The local ref resolver is read-only; project-wide progress with incomplete evidence stays unknown; shell work does not change canonical source authority; no dependency or extra project is introduced.

## Project Structure

### Documentation (this feature)

```text
specs/008-management-ui-foundation/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── default-branch-import.md
│   └── overview-view.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/ProjectManagementCompiler/
├── Application/ManifestImport/     # official default-ref import orchestration and request contract
├── Management/                     # Overview projection and shared effort-coverage semantics
├── Sources/                        # read-only local Git resolver and existing command seam
├── Program.cs                      # HTTP routes and dependency registration
└── wwwroot/                        # existing index.html, app.js, styles.css shell

tests/ProjectManagementCompiler.Tests/
├── Program.cs                      # register test methods in the existing runner
├── ManifestDefaultBranchResolverTests.cs
├── ManifestDefaultBranchImportTests.cs
├── ProjectOverviewProjectionTests.cs
└── ManagementUiShellTests.cs
```

**Structure Decision**: Extend the current ASP.NET application and C# test executable in place. Add focused source, management-projection, and UI-contract files only where needed; no new project, framework, package, generated asset, or duplicated fixture repository.

## Architecture and Ownership

1. **Local ref resolution**: Add a small resolver over `IManifestGitCommandRunner`. It reads the configured local `refs/remotes/origin/HEAD` symbolic ref, validates the target as an allowed remote-tracking ref, resolves it to a full commit object, and returns a typed result. It never runs `fetch`, checks out a ref, changes refs, or uses the working tree as fallback. Extract the current private production process runner only as needed to share the existing Git command seam between import capture and the new resolver.
2. **Atomic official import**: Add an application-service operation and `POST /api/manifest-import/default-branch`. Resolve once, force official commit mode, pass the exact SHA to the existing importer, and record the result through the existing state boundary. A single request prevents the ref from being re-resolved between UI lookup and import. Retain the exact-SHA route for advanced/manual use.
3. **Overview projection**: Add an `Overview` view projection to the current `ManagementViewSet` and existing `/api/views` view routing. Build from canonical project/analysis and the official register-status date. Reuse or extract the current `ExecutiveProgressReportProjector` effort-eligibility calculation; do not create a second progress formula. Clean project/work display names with the existing `ReaderFacingTextPolicy.CleanName`. Map only supported alert types to concise Vietnamese consequence labels; never display raw diagnostic alert strings in the primary summary. Include current phase, next control point, up to three deterministic attention items, completion count, effort-coverage count, and a project-wide percentage only with complete valid effort coverage.
4. **UI shell**: Keep the current static frontend. Make the unloaded/source-import state primary when there is no active official project, and save only source-root/manifest-path preferences. After a valid official import, route to Overview with Overview/Gantt as primary navigation; place existing WBS/Kanban and technical/specialized capabilities under Advanced until their future redesign. No dead Work placeholder.
5. **Visual implementation**: Apply the applicable frontend taste principles to this information-dense management workspace—clear hierarchy, restrained surfaces, readable type, explicit state feedback, keyboard focus, and responsive behavior—without copying a marketing layout or altering approved Gantt dependency semantics.

## Planned Interfaces

- `IManifestDefaultBranchResolver.ResolveAsync(repositoryRoot, cancellationToken)` returns the exact local commit or a safe typed failure.
- `ManifestImportApplicationService.ImportDefaultBranchAsync(request, mapping, cancellationToken)` resolves and imports through the existing official path.
- `POST /api/manifest-import/default-branch` accepts repository root, manifest path, optional analysis date override, existing capture size limits, and optional mapping; it does not accept a caller-selected mode or SHA.
- `GET /api/views/overview` (also included in `GET /api/views`) returns `ProjectOverviewProjection` and is `404 NO_PROJECT` when no compiled project is active.
- Browser storage keys contain only the repository-root and manifest-path preferences. No snapshot, credential, project JSON, or personal data is stored there.

Full request/response and projection fields are specified in `contracts/`.

## Phase 0: Research Decisions

### Decision: Resolve the configured local default ref in one request

- **Decision**: Resolve the repository's local `refs/remotes/origin/HEAD` to a full commit and pass that exact object to the existing importer in the same server request.
- **Rationale**: The importer currently requires `RequestedCommit`; this adds the normal “read current local version” flow without adding remote access or a race between a separate resolve call and import call.
- **Alternatives considered**: Require users to copy a SHA (retained only as Advanced); query a branch name from UI (duplicates source details and permits race); run `git fetch` (violates offline/read-only boundary); fall back to `HEAD` or working tree (ambiguous authority, rejected).

### Decision: Keep default-ref lookup on the existing Git process seam

- **Decision**: Reuse `IManifestGitCommandRunner`; extract its private production runner into a shareable internal registration only if constructor composition requires it.
- **Rationale**: Enables deterministic fake-runner tests and avoids a second process execution abstraction.
- **Alternatives considered**: Shell a separate Git implementation in the route (duplicate error/limit behavior); invoke a network Git library or add a dependency (unnecessary and prohibited by project constraints).

### Decision: Require complete eligible effort coverage for project-wide progress

- **Decision**: Withhold the percentage unless every delivery card has valid non-negative actual and remaining effort with a positive per-card total. Show eligible-card coverage and label subset effort as partial otherwise.
- **Rationale**: `ExecutiveProgressReportProjector.ProjectProgress` already checks `ProgressEligibleChildCount == TotalChildCount`; `ManagementMetricsAnalyzer` sums available Actual and Remaining values independently and can otherwise produce totals for only a subset. Using partial totals as the project-wide denominator would overstate completeness.
- **Alternatives considered**: Show subset progress without a coverage label (misleading); display a percentage for only recorded cards (could be mistaken for project completion); completed-card count (not effort progress, rejected).

### Decision: Preserve current WBS/Kanban behind Advanced until Work is complete

- **Decision**: Primary shell exposes Overview and Gantt only for this increment. WBS/Kanban remain available under Advanced; do not show a nonfunctional Work placeholder.
- **Rationale**: User-approved grill decision, keeps primary destinations functional and defers unified filters/inspector to increment 2.
- **Alternatives considered**: Dead/disabled Work tab (misleading); pull a partial new Work surface into this increment (scope expansion).

### Decision: Keep UI dependency-free and verify through existing tools

- **Decision**: Retain static HTML/CSS/JavaScript; use the existing test harness, scripts, and a manual browser pass.
- **Rationale**: Matches project constitution and the requested incremental redesign; avoids dependency and build-chain churn.
- **Alternatives considered**: Framework rewrite, install Playwright/Jest, or use third-party component library (not needed and out of scope).

Research was conducted directly against the local repository and existing test contracts rather than dispatched to subagents, honoring the user's preference to avoid agent fan-out.

## Phase 1: Design Artifacts

- `data-model.md` defines source preference, official snapshot, effort coverage/progress, and Overview projection invariants.
- `contracts/default-branch-import.md` defines the atomic local-only official import request, response, and failure retention semantics.
- `contracts/overview-view.md` defines the Overview projection/read contract, including unknown progress and partial coverage.
- `quickstart.md` provides runnable local validation, fixture use, browser checks, and full verification without private paths or data.

### Post-design constitution re-check

The new API remains an adapter into canonical import state; projection data remains evidence-based and derived; no source write-back, network, package, or private fixture is introduced. All constitution gates PASS.

## TDD Implementation Sequence

Implementation is sequential and vertical; do not write the whole feature's tests before any of its production code. For every pair below, first add the narrow failing test, run it and confirm the intended failure, then add the minimum implementation and rerun that focused test before moving forward:

1. Characterize existing navigation/actions before any UI changes.
2. Resolve a valid local `origin/HEAD` target to an exact commit; then harden missing, malformed, disallowed, non-commit, command-error, output-limit, cancellation, and no-mutation behavior.
3. Establish the shared effort-coverage contract and reuse it in the executive progress output.
4. Prove application-service resolve-once/exact-SHA/state-retention behavior; then prove and register the one-request API route.
5. Prove explicit-only browser source reads, restricted preference persistence, honest local freshness, and recovery behavior; then implement the source-entry flow.
6. Prove Overview date/progress/control-point projection behavior; implement it; then prove readable supported attention mapping and implement the bounded summary.
7. Prove the aggregate/named Overview view contract and no-project behavior; expose the projection through the existing view set and route.
8. Prove Overview rendering and post-import navigation; implement the loaded Overview.
9. Prove primary/Advanced navigation and retained affordances; reorganize the shell.
10. Prove responsive and keyboard contracts; apply the CSS refinement as its own slice while preserving Gantt semantics.
11. Run the focused test executable, repository test/verification scripts, manual browser checks, and public-repository privacy review. Record environmental blockers separately from product failures; commit only verified cohesive increments on the feature branch and do not push without the required approval.

## Complexity Tracking

No constitution violations or additional architectural projects are introduced. One focused import route and one focused Overview projection extend existing boundaries; no extra dependency is justified or proposed.
