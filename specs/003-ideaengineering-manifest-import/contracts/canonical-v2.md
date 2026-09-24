# Contract: Canonical JSON 2.0 and Migration

## Schema 2.0 write shape

Canonical output includes:

- `schema: "2.0"`;
- project and mapping data;
- import/snapshot metadata and safe provenance;
- validation result, source readiness, and diagnostics;
- source execution register revision/status date and records;
- retained execution-proposal projection with lifecycle/base metadata;
- separate baseline and forecast calendar identities;
- existing analysis and view projections derived from the canonical model.

No local absolute path, raw source body, secret, or unbounded diagnostic context may
be serialized. A schema `2.0` writer must emit the retained proposal projection,
not a second service-owned proposal store.

## Schema 2.0 semantic reopen validation

After deserialization the reader MUST validate, before returning a project:

- `importMetadata` identity, mode/classification, manifest path, contract,
  project/baseline/snapshot IDs, register revision, counts, calendars, and safe
  repository identity. A `GIT_COMMIT` `SourceIdentity` is exactly 40 hex
  characters, and a URI-shaped repository identity has no user-info credentials;
- `sourceExecution` project/baseline identities matching `importMetadata`, typed
  DeliveryCard identities, target existence, uniqueness,
  recording/state/result combinations, non-negative decimal source effort/date
  semantics without rounding, safe source paths, revision at least `1`, and
  controlled evidence;
- `executionProposals` supported DeliveryCard targets, lifecycle/base metadata,
  supported change fields and syntactically valid values, safe evidence, and no
  source-authority promotion. Proposal effort values use non-negative 0.5-hour
  increments. A migrated schema-1.0 proposal may carry expected revision `0` as
  its explicit legacy compatibility marker.

The metadata project and baseline IDs must equal `CanonicalProject.Project.Id` and
`CanonicalProject.Baseline.Id`; source execution IDs, register revision, and
status date must equal the corresponding metadata values. This is an authority
relationship, not merely a JSON shape check.

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
