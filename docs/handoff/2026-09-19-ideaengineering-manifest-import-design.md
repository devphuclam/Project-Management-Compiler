# Handoff — IDEAEngineering manifest import design

## Implementation status

The original Feature 003 importer is implemented behind the public
`IIdeaEngineeringManifestImporter.ImportAsync` seam and verified against the
accepted source commit. This handoff is now being continued by the dedicated
MVP2.2 correctness/hardening branch `codex/feature003-hardening`; implementation
must not start until the updated Spec Kit artifact gate has passed. The
executable quickstart is in
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

MVP2.2 hardening is explicitly scoped to correctness and trust boundaries:
schema `1.0` overlay neutralization, semantic schema `2.0` validation,
independent working-tree capture, fail-closed Git capture, bounded tree/blob
reads, controlled proposal evidence, single proposal ownership in
`CompilerApplicationState`, and authority-aware UI wording. It adds no package,
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
- `SourceExecutionSnapshot` is source-authoritative.
  `ExecutionProposal` is local and non-authoritative; the legacy
  `ExecutionOverlay` is migration input only.
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
- Manifest UI labels source-authoritative execution as `Source execution`, local
  edits as proposals, and `ExecutionProposal` forecast values as `Source
  forecast` where they originate from the source.
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

No subagent was required for this implementation.

## Git and local workspace notes

The hardening work is isolated in the managed worktree for
`codex/feature003-hardening`. The main checkout and the separately checked-out
IDEAEngineering source repository must remain untouched by implementation.

The repository is public. Do not copy the IDEAEngineering repository wholesale
or commit raw private material. Vendor only the minimum public-safe catalogue,
schema, and controlled fixtures needed to prove the consumer contract.
