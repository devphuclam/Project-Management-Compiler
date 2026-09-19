# Handoff — IDEAEngineering manifest import design

## Implementation status

The authority model, feature specification, implementation plan, and runtime
continuation are complete. The importer is implemented behind the public
`IIdeaEngineeringManifestImporter.ImportAsync` seam and verified against the
accepted source commit. The executable quickstart is in
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
  `ExecutionProposalOverlay` is local and non-authoritative.
- Official views and exports ignore proposals. Proposal impact is available
  only in an explicit preview mode.
- Canonical persistence advances to schema `2.0`; legacy schema `1.0` execution
  overlays migrate to proposals with an explicit warning.
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

At this pause, the feature branch was
`codex/ideaengineering-manifest-import`. The only unrelated workspace files are
local untracked `package.json` and `package-lock.json`; they are user-owned and
must remain uncommitted unless the user separately decides otherwise.

The repository is public. Do not copy the IDEAEngineering repository wholesale
or commit raw private material. Vendor only the minimum public-safe catalogue,
schema, and controlled fixtures needed to prove the consumer contract.
