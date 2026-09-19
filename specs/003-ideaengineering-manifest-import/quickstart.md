# Quickstart: IDEAEngineering Manifest Import

## Start the compiler

From the Project Management Compiler repository:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-project.ps1 `
  -RepositoryRoot C:\path\to\IDEAEngineering `
  -ManifestPath planning/project-management-compiler-manifest.json `
  -SourceCommit 0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4 `
  -ImportMode GIT_COMMIT `
  -ImportManifest
```

The repository root is a local input only. It is never persisted in canonical output.
Use `-ImportMode UNCOMMITTED_PREVIEW` to inspect a working tree without changing official state.
Omit `-ImportManifest` to start the legacy browser shell without an automatic import; the
source-of-truth form remains available in the UI. `-SourcePath` is retained as an alias for
`-RepositoryRoot`.

## Import through the API

```powershell
$body = @{
  repositoryRoot = 'C:\path\to\IDEAEngineering'
  manifestPath = 'planning/project-management-compiler-manifest.json'
  mode = 'GIT_COMMIT'
  requestedCommit = '0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4'
} | ConvertTo-Json

Invoke-RestMethod http://127.0.0.1:5050/api/manifest-import `
  -Method Post -ContentType 'application/json' -Body $body
```

Expected accepted-source state:

- classification `OFFICIAL_COMMIT`;
- validation `PASS_WITH_WARNINGS`;
- source readiness `PASS`;
- one warning, `PMC-CALENDAR-001`;
- 6 phases, 35 work packages, 53 executable cards, 7 gates/milestones;
- 512 planned hours, 88 reserve hours, 600 total hours;
- P01 recorded/in progress/not applicable with unknown actual and remaining;
- 52 cards with `NOT_RECORDED` recording state.

The canonical source-execution snapshot also retains the register `projectId` and
`baselineId`; these must match import metadata during v2 reopen validation.

## Preview and proposal demonstration

1. Import the working tree with `mode = UNCOMMITTED_PREVIEW`.
2. Confirm the result is visibly preview and current official metadata/digest is
   unchanged.
3. Create a proposal against the official snapshot, including its base snapshot ID
   and register revision.
4. Request proposal preview explicitly; treat its metrics as estimated and
   non-authoritative.
5. Export `execution-proposal.json` and review it without write-back.
6. Import a newer register revision and confirm the retained proposal is
   `STALE_BASE`.

## Verification

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
node --check .\src\ProjectManagementCompiler\wwwroot\app.js
```

## MVP2.2 hardening verification

The hardening pass is still local and dependency-free. Before implementation,
the artifact gate must run with the feature directory selected explicitly:

```powershell
$env:SPECIFY_FEATURE_DIRECTORY = 'specs/003-ideaengineering-manifest-import'
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks
```

The TDD regression set must prove, through public seams, that:

- schema `1.0` overlays migrate to proposals and cannot affect official
  execution analysis, alerts, dashboards, effort, or Gantt `ACTUAL` lanes;
- a proposal survives canonical save, reopen, hydration, and list;
- tampered schema `2.0` authority data fails closed;
- working-tree pre/post manifest reads are independent and mutation rejects the
  preview;
- unavailable Git state, symlink entries, and oversized blobs fail closed before
  body reads; and
- malformed evidence cannot qualify `READY_FOR_REVIEW`.

The final fresh verification must include the focused regression commands,
`scripts/test.ps1`, `scripts/verify.ps1`, `scripts/verify-web.ps1`,
`node --check src/ProjectManagementCompiler/wwwroot/app.js`, `git diff --check`,
and the repository's changed-file safety scans. Do not inspect or modify the
IDEAEngineering checkout as part of these checks.

Do not run the source validator by checking out or modifying the IDEAEngineering
working tree. Compatibility checks use the accepted Git object/fixture boundary.
