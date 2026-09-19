# MVP2.1 Data Model

## `ManagementEvidence`

An additive, immutable value on `CanonicalProject`.

| Field | Meaning |
| --- | --- |
| `DiscoveryState` | `NOT-REQUESTED`, `NOT-CONFIGURED`, `KNOWN`, `UNKNOWN`, `AMBIGUOUS`, or `UNAVAILABLE` |
| `IncrementPath` | safe repository-relative package path, when resolved |
| `IncrementId` | package/increment identity from agreeing source records |
| `IncrementName` | safe display name, when supplied |
| `IncrementStatus` | source status such as `Draft` |
| `Observations` | immutable typed management evidence records |
| `Reconciliations` | one deterministic result per observation |
| `Diagnostics` | safe structured evidence warnings/errors |

Capture timestamps are metadata only and are excluded from the semantic digest.

## `ManagementEvidenceObservation`

Each observation has:

* stable `Id` and `SourceRecordId`;
* closed `EvidenceKind`;
* optional raw target kind/id and optional explicit typed target;
* independent `StateCode`/`StateMeaning` and `ResultCode`/`ResultMeaning`;
* optional evidence date/observation time;
* owner, waiting-for, and required-authority role references, each retaining
  code, safe display label, and source wording;
* due condition code/summary, gate effect code/summary, blocker summary, and
  pending-action summary;
* bounded decision/human-action summary, affected target, completion
  condition, source-derived gate ID, and explicit proposed successor fields;
* evidence links and safe source references;
* authority rank/kind and validation state.

The model never stores the source document body.

## `EvidenceTarget`

```text
Kind: Project | Phase | WorkPackage | DeliveryCard | Milestone | Role
Id:   canonical typed identifier, for example P04 or PG4
```

`Gate`, `Decision`, `HumanAction`, `ChecklistContext`, and `ControlEnvelope`
are standalone management objects and are not canonical target kinds. Identity
comparison is ordinal and case-normalized only according to the declared
target contract. A bare `P04` is not a typed target until its kind is known.

## `EvidenceReconciliation`

| Field | Meaning |
| --- | --- |
| `ObservationId` | source observation being reconciled |
| `Status` | `MATCHED`, `STANDALONE`, `UNMATCHED`, `AMBIGUOUS`, or `INVALID` |
| `ResolvedTarget` | one canonical typed target for `MATCHED` |
| `CandidateTargets` | all candidates for `AMBIGUOUS` |
| `RuleId` | stable mapping rule identifier |
| `Reason` | concise safe explanation |
| `SourceReferences` | supporting references |

## `ManagementControlView`

The UI-facing projection contains:

* evidence scope and discovery state;
* active increment identity and status;
* current gate execution/outcome and next-baseline context;
* work-package readiness rows;
* grouped attention items for open decisions, pending human actions,
  blockers, conflicts, and reconciliation failures;
* inspector-safe evidence details and provenance;
* reconciliation counts and diagnostic codes.

The projection uses `EffectiveEvidenceResolver` for authority-sensitive fields.
Selections are field-level and derived: `RESOLVED` selects the highest
authority value, `MISSING` has no attributable value, and `CONFLICT` retains
all highest-authority candidates without selecting a value. Imported
observations are never rewritten.

It is derived from `ManagementEvidence` and canonical planning facts. It is
not a second source of truth and is not used to mutate the canonical model.

## State invariants

1. A missing management-evidence property in schema 1.0 is an empty evidence
   object.
2. `NOT-RUN` is never represented as `PASS`.
3. A gate outcome is `NOT-APPLICABLE` until its source-derived gate execution
   is `COMPLETE`; the generic model has no default PG4.
4. `MATCHED` has exactly one resolved typed target.
5. `AMBIGUOUS` has at least two candidates and no resolved target.
6. `STANDALONE` has no resolved canonical target and is valid for management
   objects that do not represent planning work.
7. `UNMATCHED` has no resolved target and explains an absence that should have
   resolved.
8. `INVALID` has a validation diagnostic and cannot affect planning facts.
9. Evidence cannot overwrite baseline or execution-overlay values.
