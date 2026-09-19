# Contract: Import State and Proposals

## State operations

The application exposes explicit operations equivalent to:

```text
ImportManifest(request) -> ManifestImportResult
GetCurrentOfficial() -> SnapshotEnvelope | 404
GetLatestImportAttempt() -> ManifestImportAttempt | 404
GetActivePreview() -> SnapshotEnvelope | 404
ClearActivePreview() -> 204
```

Official state is replaced only by a valid `GIT_COMMIT` result whose source
readiness gate is `PASS`. A valid unready commit is `CANDIDATE_PREVIEW`. A working
tree is `UNCOMMITTED_PREVIEW`. Failed attempts never replace official or active
preview.

## Proposal operations

```text
CreateProposal(baseSnapshotId, expectedRegisterRevision, target, changes, evidence)
UpdateProposal(proposalId, changes, evidence, lifecycle request)
ListProposals(snapshotId?)
PreviewProposal(proposalId) -> non-authoritative effective view
ExportProposal(proposalId) -> deterministic execution-proposal.json
```

Proposal lifecycle is `DRAFT`, `READY_FOR_REVIEW`, or `STALE_BASE`. The server
recomputes staleness from current official snapshot ID and register revision. It
does not rebase. Completion proposals remain `DRAFT` with diagnostics until finish,
actual effort, zero remaining, and controlled evidence are all present.

`/api/execution` remains a compatibility alias only. It creates or updates a
proposal and returns a proposal-only marker; it cannot mutate source execution or
official analysis.
