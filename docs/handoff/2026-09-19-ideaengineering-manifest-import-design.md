# Handoff — IDEAEngineering manifest import design

## Pause point

The authority model and feature specification for the IDEAEngineering manifest
integration are approved and committed. Implementation has **not** started.
This is an intentional pause before Spec Kit planning, not a partially shipped
runtime feature.

The committed design increment is:

- commit `67495c1` — `docs: define manifest import authority and specification`;
- accepted ADR: `docs/adr/0006-manifest-source-and-execution-authority.md`;
- approved feature spec: `specs/003-ideaengineering-manifest-import/spec.md`;
- completed requirements checklist:
  `specs/003-ideaengineering-manifest-import/checklists/requirements.md`;
- updated shared vocabulary and settled decisions in `CONTEXT.md`.

The generated but unfilled `plan.md` template was deliberately removed before
handoff. It must be regenerated and completed when work resumes; it is not
evidence that planning was finished.

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
`PMC-CALENDAR-001`; no errors were reported. This is compatibility-oracle
evidence, not proof that the Compiler importer has been implemented.

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

## Resume workflow

1. Create a new implementation branch from the pushed `main`.
2. Regenerate the plan template:

   ```powershell
   .\.specify\scripts\powershell\setup-plan.ps1 -Json
   ```

3. Complete and commit:

   - `specs/003-ideaengineering-manifest-import/plan.md`;
   - `research.md`;
   - `data-model.md`;
   - `contracts/`;
   - `quickstart.md`.

4. Re-run the constitution gate, generate `tasks.md` with
   `.\.specify\scripts\powershell\setup-tasks.ps1 -Json`, and run the
   non-destructive Spec Kit analysis before implementation.
5. Implement in dependency-ordered TDD slices. Start with a failing contract
   test for `ImportAsync`; then add safe commit capture, manifest/schema and
   semantic validation, source execution, last-valid state, proposals,
   canonical v2 migration, APIs, UI, exports, launcher parameters, fixtures,
   and verifier checks.
6. Finish with Spec Kit converge, the full repository verification gate,
   public-repository secret/path inspection, and a focused code review.

No subagent is required for this continuation.

## Git and local workspace notes

At this pause, the feature branch was
`codex/ideaengineering-manifest-import`. The only unrelated workspace files are
local untracked `package.json` and `package-lock.json`; they are user-owned and
must remain uncommitted unless the user separately decides otherwise.

The repository is public. Do not copy the IDEAEngineering repository wholesale
or commit raw private material. Vendor only the minimum public-safe catalogue,
schema, and controlled fixtures needed to prove the consumer contract.
