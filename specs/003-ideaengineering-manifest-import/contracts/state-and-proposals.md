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

## Ownership and persistence

`CompilerApplicationState` is the single runtime owner of retained proposals.
`CanonicalProject.ExecutionProposals` is the persistence projection of that set.
The proposal service MUST NOT keep an independent mutable dictionary.

- import and canonical reopen hydrate the state owner from retained proposals;
- create/update write the state owner and current canonical projection together;
- save → reopen → list returns the same semantic proposal;
- schema `1.0` migrated proposals are listed through the same API;
- a newer official snapshot marks retained proposals `STALE_BASE`; and
- no proposal operation mutates `SourceExecution` or official analysis.

## Controlled evidence

`Evidence.Count > 0` is not sufficient. A record qualifies only when it has a
non-empty ID, supported type, description, recording timestamp, recorder, and
safe optional repository path/commit/URI. Invalid records remain diagnosed and a
completion proposal remains `DRAFT`.

`/api/execution` remains a compatibility alias only. It creates or updates a
proposal and returns a proposal-only marker; it cannot mutate source execution or
official analysis.
