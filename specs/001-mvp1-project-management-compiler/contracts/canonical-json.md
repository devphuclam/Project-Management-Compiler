# Canonical JSON Contract

## Version

The first public contract is `schemaVersion: "1.0"`. A reader MUST reject an
unsupported major version rather than treating it as the current contract.

## Stability rules

- arrays are emitted in deterministic source order;
- IDs are stable source IDs where available;
- `ProjectSource.capturedAtUtc` is capture metadata and does not participate in
  semantic equivalence, deterministic project-content comparison, or content
  digests;
- absolute temporary clone paths and machine-specific working paths are not
  serialized as repository identity;
- null/unknown values are explicit and are not omitted when their absence changes
  interpretation;
- warnings and provenance are part of the snapshot, not transient logging only.
- `executionOverlay` is additive within schema 1.0. New writers emit it with a
  `records` array; readers accept older 1.0 snapshots that omit it and use an
  empty overlay. A present overlay is validated strictly and is never merged
  into planned dates or planned effort.

## Required top-level fields

```text
schemaVersion
project
sources
baseline
phases
workPackages
deliveryCards
milestones
dependencies
responsibilityRoles
assignments
capacity
reserve
policies
executionOverlay
provenance
warnings
analysis
```

The entity definitions and invariants are maintained in
`specs/001-mvp1-project-management-compiler/data-model.md`.

## Reopen contract

The application accepts a previously generated canonical document when:

1. `schemaVersion` is supported and required fields have the declared types;
2. IDs are unique in every collection;
3. parent, phase, work-package/card, and assignment relationships resolve;
4. baseline dates, effort, duration, and state fields satisfy their invariants;
5. every dependency target either resolves or is explicitly retained as source
   evidence with `validationState: INVALID_SOURCE_EVIDENCE`,
   `analysisEligible: false`, and a diagnostic;
6. baseline and source metadata are present; and
7. every execution record targets an executable canonical work item, has a
   supported authored execution state, uses non-negative finite effort values,
   and does not place actual finish before actual start; and
8. the document is not silently upgraded to a different source ref.

Duplicate IDs, malformed schema, impossible hierarchy, nonexistent structural
assignments, invalid baseline fields, and unmarked missing references are hard
failures. A missing source dependency target is the narrow exception because
the evidence itself is useful; it remains visible but is excluded from CPM.

Reopen produces the same views and output contracts without source capture or
extraction. Calculated analysis may be recomputed from the immutable baseline;
the baseline itself remains unchanged. The execution overlay is reopened as
mutable evidence and recalculation may replace alerts, variance, and other
derived analysis without changing either baseline or overlay values.

## Execution and analysis compatibility

`actualStart`, `actualFinish`, `actualEffortHours`, `remainingEffortHours`,
execution state, and update metadata are persisted under `executionOverlay`.
`OVERDUE` and `AT_RISK` are never persisted authored states. An explicit
`asOfDate` is an analysis input; alerts and working-calendar variance may be
recomputed after reopen. The semantic digest excludes capture timestamps and
derived analysis, but includes the execution overlay because it is user-owned
snapshot content.

Canonical persistence contains source metadata, references, sizes and optional
content digests, not full captured Markdown/HTML source text. Source content is
a runtime extraction input and is not serialized by default. Nullable authored
dates remain JSON `null`; `0001-01-01` is invalid business data. A missing source
execution state remains null/unknown and is not serialized as `NOT_STARTED`.
