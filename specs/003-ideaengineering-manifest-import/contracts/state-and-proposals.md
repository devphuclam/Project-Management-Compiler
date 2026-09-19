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
actual effort, zero remaining, and at least one valid controlled-evidence item are
all present. An empty evidence list is valid for a draft. Any supplied item that
does not match the source-compatible evidence contract is rejected at create/update
and does not mutate the retained proposal set.

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
non-empty ID, one of `SOURCE_RECORD`, `COMMIT`, `PULL_REQUEST`, `TEST_RESULT`,
`REVIEW_RECORD`, `ARTIFACT`, or `EXTERNAL_RECORD`, description, source-compatible
result, recording timestamp, and recorder. Repository path, commit, and HTTP(S)
URI without user info are optional fields, each validated independently when
present. Invalid input is rejected before state mutation.

`/api/execution` remains a compatibility alias only. It creates or updates a
proposal and returns a proposal-only marker; it cannot mutate source execution or
official analysis. Its legacy file reference is a safe non-controlled proposal
field; it must not be encoded as fabricated controlled evidence.

If an official result is retained while `Current` is a reopened schema-1.0
legacy result, proposal updates project to both applicable canonical results from
the same proposal list. The legacy result is never promoted to official source
state, and source execution remains immutable.
