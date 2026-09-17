# Canonical JSON Contract

## Version

The first public contract is `schemaVersion: "1.0"`. A reader MUST reject an
unsupported major version rather than treating it as the current contract.

## Stability rules

- arrays are emitted in deterministic source order;
- IDs are stable source IDs where available;
- source capture timestamps are metadata and do not participate in the
  deterministic project-content comparison;
- absolute temporary clone paths and machine-specific working paths are not
  serialized as repository identity;
- null/unknown values are explicit and are not omitted when their absence changes
  interpretation;
- warnings and provenance are part of the snapshot, not transient logging only.

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
provenance
warnings
analysis
```

The entity definitions and invariants are maintained in
`specs/001-mvp1-project-management-compiler/data-model.md`.

## Reopen contract

The application accepts a previously generated canonical document when:

1. `schemaVersion` is supported;
2. all IDs are unique in their collection;
3. parent, phase, dependency, and assignment references resolve or are retained
   as explicitly invalid diagnostics;
4. baseline and source metadata are present;
5. the document is not silently upgraded to a different source ref.

Reopen produces the same views and output contracts without source capture or
extraction. Calculated analysis may be recomputed from the immutable baseline;
the baseline itself remains unchanged.
