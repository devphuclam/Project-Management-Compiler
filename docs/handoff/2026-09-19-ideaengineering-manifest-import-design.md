# Handoff — IDEAEngineering manifest import design

## Implementation status

The original Feature 003 importer is implemented behind the public
`IIdeaEngineeringManifestImporter.ImportAsync` seam and verified against the
accepted source commit. MVP2.2 correctness/hardening has already been merged to
compiler `main` at the prior verified baseline. This handoff now records the
separate final correctness micro-pass on branch
`codex/feature003-final-micro-pass`; its implementation starts only after this
pass's Spec Kit artifact gate. The executable quickstart is in
`specs/003-ideaengineering-manifest-import/quickstart.md`.

The design increment is:

- commit `67495c1` — `docs: define manifest import authority and specification`;
- accepted ADR: `docs/adr/0006-manifest-source-and-execution-authority.md`;
- approved feature spec: `specs/003-ideaengineering-manifest-import/spec.md`;
- completed requirements checklist:
  `specs/003-ideaengineering-manifest-import/checklists/requirements.md`;
- updated shared vocabulary and settled decisions in `CONTEXT.md`.

The implementation plan, data model, contracts, tasks, and quickstart now live
under `specs/003-ideaengineering-manifest-import/`.

The runtime keeps official source execution separate from local proposal
previews, retains failed candidates without replacing official state, and
captures the manifest-declared readiness register as independent evidence.

The completed MVP2.2 hardening was explicitly scoped to correctness and trust
boundaries:
schema `1.0` overlay neutralization, semantic schema `2.0` validation,
independent working-tree capture, fail-closed Git capture, bounded tree/blob
reads, controlled proposal evidence, single proposal ownership in
`CompilerApplicationState`, and authority-aware UI wording. It added no package,
database, connector, Docker setup, or source-repository change.

## Source compatibility evidence

The read-only source package was inspected in the separately checked-out
`devphuclam/IDEAEngineering` repository. Its machine-local path is deliberately
not persisted in this public repository.

Accepted compatibility baseline:

- source commit: `0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4`;
- sole discovery entry point:
  `planning/project-management-compiler-manifest.json`;
- source contract: `0.1.0`.

The source-owned validator was run with fixtures and exited successfully with
`PASS_WITH_WARNINGS`. The only warning was the accepted calendar difference
`PMC-CALENDAR-001`; no errors were reported. The Compiler importer independently
replays the seven source-owned fixture outcomes and preserves that warning.

## Decisions that must not be reopened silently

- Official imports read manifest-declared files from one exact Git commit;
  working-tree reads are visibly labelled `UNCOMMITTED_PREVIEW`.
- The manifest is the only discovery entry point. A failed manifest import may
  not fall back to the legacy fixed-path importer.
- In a manifest-import session, `SourceExecutionSnapshot` is source-authoritative
  and `ExecutionProposal` is local and non-authoritative; the legacy
  `ExecutionOverlay` is migration input only. The explicit fixed-path legacy
  workflow may still use its overlay when `ImportMetadata` is absent.
- Official views and exports ignore proposals. Proposal impact is available
  only in an explicit preview mode.
- Canonical persistence advances to schema `2.0`; legacy schema `1.0` execution
  overlays migrate to proposals with an explicit warning and are then cleared or
  neutralized so they cannot be consumed by any execution resolver or view.
- `CompilerApplicationState` is the single mutable runtime owner of local
  proposals. `CanonicalProject.ExecutionProposals` is the persistence
  projection; save/reopen hydrates the same state. `SourceExecutionSnapshot` is
  immutable for proposal operations.
- Schema `2.0` reopen validates `ImportMetadata`, `SourceExecution`, proposal
  identities/targets/lifecycle, and controlled evidence semantically; valid JSON
  syntax alone is not acceptance.
- Working-tree pre/post capture reads the manifest and declared boundary
  independently. Git `HEAD`/status failure, symlink mode `120000`, oversized
  entries, and body reads beyond hard application ceilings fail closed.
- Proposal completion requires a valid controlled-evidence record; non-empty
  malformed evidence cannot produce `READY_FOR_REVIEW`.
- Controlled evidence uses the accepted seven-type allow-list and rejects invalid
  commit tokens or external URIs with embedded user information. Source execution
  retains validated project/baseline identities for metadata cross-checking; the
  final micro-pass also enforces canonical ID equality, revision, and status-date
  relationships.
- Manifest UI labels source-authoritative execution as `Source execution`, local
  edits as proposals, and `SourceExecutionSnapshot.ForecastFinish` as `Source
  forecast`.
- A failed or unready candidate cannot replace the last valid official
  snapshot. Runtime state keeps current official, latest attempt, and active
  preview separately.
- `RECORDED`/`NOT_RECORDED`, Execution State, and Result State are independent.
  Missing source execution must never be presented as confirmed `NOT_STARTED`.
- P01 must import as `RECORDED / IN_PROGRESS / NOT_APPLICABLE` with unknown
  actual and remaining effort. The other 52 cards remain `NOT_RECORDED`.
- No direct write-back to IDEAEngineering is in scope. Proposal export is a
  deterministic human-review artifact only.
- The legacy fixed-path workflow may remain for compatibility but is never an
  automatic fallback from the manifest workflow.
- The final micro-pass restores the explicit legacy overlay only for a legacy
  project with no `ImportMetadata`, checks Git aggregate size before body reads,
  rejects malformed proposal evidence atomically using the source contract,
  removes compatibility-route fake evidence, tightens canonical v2 identity and
  granularity checks, and keeps mixed-session proposal projections coherent.
- Repository root, manifest path, commit, and preview mode are user inputs;
  no personal absolute path may be committed.

## Agreed implementation seam

Use one deep public importer interface, conceptually:

```csharp
Task<ManifestImportResult> ImportAsync(
    ManifestImportRequest request,
    CancellationToken cancellationToken);
```

The module should hide manifest capture, Git/working-tree adapters, bounded
JSON-Schema validation, role/authority resolution, semantic validation, and
atomic snapshot assembly. Tests should enter through this public seam and
through public application operations; Git and filesystem boundaries may use
small fakes. Do not write tests against internal implementation details.

## Maintenance workflow

For future source-contract changes, update the manifest contract and its fixture
catalogue first, add a public-seam regression test, then rerun the full
`scripts/verify.ps1` gate. Do not make the legacy fixed-path compiler an
automatic fallback, do not mutate the IDEAEngineering checkout, and do not
promote proposal or readiness evidence into source execution.

The original implementation, MVP2.2 hardening, and final micro-pass were
completed locally; this handoff does not claim remote CI. The final micro-pass
artifact gate, TDD regressions, post-implementation analyze/converge, review,
and fresh verification are green on the dedicated branch. The implementation
was then fast-forward integrated into compiler `main` at
`a99a5e0de2d4f8f019f07747084f7fb83846ed1f`.

Fresh local verification recorded for this pass:

- `dotnet build tests/ProjectManagementCompiler.Tests/ProjectManagementCompiler.Tests.csproj --no-restore`: 0 warnings, 0 errors;
- `scripts/test.ps1`: 234 PASS, 0 FAIL;
- `scripts/verify.ps1`: PASS, including launcher and web gates;
- `scripts/verify-web.ps1`: PASS, loopback health, 53 cards, six CARIO XLSX
  sheets plus the daily Gantt sheet, and security checks;
- `scripts/verify-launcher.ps1`: `PASS LauncherContract`;
- `node --check src/ProjectManagementCompiler/wwwroot/app.js`: PASS;
- `git diff --check` and changed-code safety scan: PASS.

## Git and local workspace notes

The final micro-pass is isolated in the managed worktree for
`codex/feature003-final-micro-pass`. The compiler main baseline before this
pass was `d4acdda85a39b5543253e63cc292bf8d3fdd6338`; final integration is
complete at `a99a5e0de2d4f8f019f07747084f7fb83846ed1f` after the post-pass
analyze/converge, code review, and fresh verification gates.
The main checkout and the separately checked-out IDEAEngineering source repository
must remain untouched by implementation.

The repository is public. Do not copy the IDEAEngineering repository wholesale
or commit raw private material. Vendor only the minimum public-safe catalogue,
schema, and controlled fixtures needed to prove the consumer contract.
