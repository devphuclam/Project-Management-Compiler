---
status: accepted
date: 2026-09-19
---

# Import IDEAEngineering through its manifest and keep source execution authoritative

## Context

ADR-0005 aligned the compiler with an earlier IDEAEngineering planning shape,
but capture still relies on a compiler-owned fixed path list. ADR-0004 also
allows manual execution evidence in a compiler-owned `ExecutionOverlay` to
drive management analysis. IDEAEngineering now publishes source contract
`0.1.0` at accepted commit
`0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4`. Its manifest is the sole
discovery entry point and declares an `EXECUTION_AUTHORITY` that owns recorded
actual state, result, actual and remaining effort, forecast overrides,
blockers, and evidence.

Continuing to discover source files through a fixed list would bypass the
manifest's authority declarations. Continuing to treat local manual updates as
official actuals would create a competing execution record and make
`NOT_RECORDED` indistinguishable from `NOT_STARTED`.

## Decision

Introduce one deep manifest-import module. Its interface accepts repository
root, repository-relative manifest path, and an explicit import context. The
implementation hides Git/filesystem capture, manifest and schema validation,
field-level authority resolution, source-role adaptation, and atomic snapshot
validation.

The module observes these rules:

- the manifest is the only discovery entry point for manifest imports;
- contract version `0.1.0` is the only initially supported source contract;
- an official import reads every declared file from one exact Git commit;
- a working-tree import is labelled `UNCOMMITTED_PREVIEW` and cannot replace
  the last valid official snapshot;
- absolute paths, traversal, reparse-point escape, undeclared files, mixed
  snapshots, unsupported semantics, and ambiguous authority fail closed;
- source roles own only the fields declared by the source contract; file read
  order never establishes authority;
- a failed candidate leaves the current official snapshot unchanged.

Imported execution facts are represented as `SourceExecutionSnapshot` and
remain distinct from the immutable planning baseline. Local edits are
represented as `ExecutionProposalOverlay`. Proposals are never official actual
evidence and never affect official analysis. An explicitly selected proposal
preview may calculate a scenario, but it is labelled as estimated and cannot
overwrite or export as an official snapshot.

Canonical JSON advances from schema `1.0` to `2.0` because the meaning of the
execution data changes. A schema `1.0` execution overlay can be reopened only
through a migration that marks its records as proposals and emits a migration
diagnostic. Source contract version `0.1.0` and canonical schema version `2.0`
remain independent version domains.

The existing manual execution operation becomes a compatibility alias that
creates proposals only. Direct write-back to IDEAEngineering is not part of
this decision. A deterministic proposal artifact may be exported for human
review and later source-side change control.

## Supersession

This ADR supersedes ADR-0004 only where ADR-0004 allowed compiler-maintained
manual execution evidence to act as effective official actuals. ADR-0004 still
governs separation of mutable local data from the immutable baseline and the
requirement to recalculate derived analysis rather than persist it.

This ADR extends ADR-0005 by replacing compiler-owned fixed-path discovery with
manifest-owned discovery. ADR-0005's field-level planning authority, typed
identity, rendition treatment, and planning extraction decisions remain in
force.

## Consequences

- The canonical domain gains explicit recording state, result state, source
  execution, proposal, snapshot metadata, and import-attempt concepts.
- Official analysis becomes reproducible from source commit, baseline ID,
  register revision, and explicit as-of date.
- Existing local execution JSON remains readable but no longer silently gains
  source authority.
- The UI and every export must identify official, preview, and proposal modes.
- The compiler stays read-only with respect to IDEAEngineering.
- Contract fixtures and the accepted real source commit become executable
  compatibility oracles without copying the reference repository wholesale.
