# Contract: Canonical JSON 2.0 and Migration

## Schema 2.0 write shape

Canonical output includes:

- `schema: "2.0"`;
- project and mapping data;
- import/snapshot metadata and safe provenance;
- validation result, source readiness, and diagnostics;
- source execution register revision/status date and records;
- retained proposal overlay with lifecycle/base metadata;
- separate baseline and forecast calendar identities;
- existing analysis and view projections derived from the canonical model.

No local absolute path, raw source body, secret, or unbounded diagnostic context may
be serialized. A schema `2.0` writer must emit the retained proposal projection,
not a second service-owned proposal store.

## Schema 2.0 semantic reopen validation

After deserialization the reader MUST validate, before returning a project:

- `importMetadata` identity, mode/classification, manifest path, contract,
  project/baseline/snapshot IDs, register revision, counts, calendars, and safe
  repository identity;
- `sourceExecution` typed DeliveryCard identities, target existence, uniqueness,
  recording/state/result combinations, effort/date semantics, safe source paths,
  and controlled evidence; and
- `executionProposals` supported DeliveryCard targets, lifecycle/base metadata,
  supported change fields and syntactically valid values, safe evidence, and no
  source-authority promotion.

Any semantic error fails closed with diagnostics; parse success is insufficient.

## Schema 1.0 read behavior

Schema `1.0` remains readable. Existing `ExecutionOverlay` records are preserved as
local proposals with a deterministic migration ID and an explicit migration
diagnostic, then `executionOverlay.records` is neutralized in the reopened model.
They are not copied into `SourceExecutionSnapshot`, not presented as official
actuals, and do not change official execution metrics, alerts, Gantt actual lanes,
or source digest. Missing v2 source metadata is represented as unknown/legacy
context rather than invented.

Unsupported schema versions fail closed with `PMC-SCHEMA-001` or the corresponding
contract diagnostic. Canonical digest excludes capture timestamps and other
non-semantic display metadata.
