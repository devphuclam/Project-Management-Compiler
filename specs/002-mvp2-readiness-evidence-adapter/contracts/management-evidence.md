# Management Evidence Contract

## Closed evidence kinds

```text
CONTROL_ENVELOPE
READINESS_CHECK
GATE_EXECUTION
GATE_OUTCOME
DECISION_RECORD
HUMAN_ACTION
CHECKLIST_CONTEXT
```

Unknown kinds are diagnostics, not silently accepted facts.

## State/result contract

Readiness checks may contain independent task state and result. The adapter
must retain both. The following examples are deliberately not equivalent:

| State | Result | Meaning |
| --- | --- | --- |
| `IN-PROGRESS` | `NOT-RUN` | preparation is underway; no result exists |
| `NOT-RUN` | `NOT-RUN` | check has not executed |
| `COMPLETE` | `PASS-WITH-ACTIONS` | execution complete with named follow-up actions |
| `COMPLETE` | `FAIL` | execution complete and failed |
| `BLOCKED` | `NOT-APPLICABLE` | blocked before an applicable result |

`PASS` and `PASS-WITH-ACTIONS` require attributable executed evidence. A task
checkbox, source hash, or author preparation note is not enough.

## PG4 contract

`GATE_EXECUTION` records execution state independently from
`GATE_OUTCOME`.

* `NOT-RUN` or `IN-PROGRESS` execution requires `NOT-APPLICABLE` outcome.
* `COMPLETE` execution permits `PASS`, `PASS-WITH-ACTIONS`, `FAIL`, or
  `BLOCKED`, subject to attributable evidence.
* An outcome without an execution record is invalid for readiness purposes.

Invalid pairs emit `GATE_EXECUTION_OUTCOME_INVALID` and cannot authorize a
successor or production implementation.

## Authority precedence

The adapter evaluates competing records in this order for a semantic field:

1. attributable actual gate/decision record;
2. authoritative readiness-register state/result;
3. package README summary;
4. contract/template semantics (permitted values only, never current state);
5. checklist context or bounded prose.

`AuthorityRank`/`AuthorityKind` are used by the derived
`EffectiveEvidenceResolver`; parser order, file order, mtime, and last-write
wins are not selection rules. If the highest-authority observations disagree
for the same semantic field, the resolver returns `CONFLICT` with no effective
value and preserves every candidate/source reference. Different fields, such
as `StateCode` and `ResultCode`, are resolved independently.

## Management-only evidence and typed reconciliation

Canonical target kinds are limited to `Project`, `Phase`, `WorkPackage`,
`DeliveryCard`, `Milestone`, and `Role`. `Gate`, `Decision`, `HumanAction`,
`ChecklistContext`, and `ControlEnvelope` are management evidence objects, not
canonical work items. They are valid `STANDALONE` reconciliations when no
canonical target is required; they do not produce `EVIDENCE_TARGET_UNMATCHED`.

Readiness records still map only through an explicit typed
`WorkPackage:<id>` target. A same-ID `DeliveryCard` is not a readiness target.

## Default mappings

| Source record | Default target | Rule |
| --- | --- | --- |
| P01–P07 work-package readiness | `WorkPackage:P01`–`P07` | target only if canonical package exists |
| D0–D5 decision | none | `STANDALONE_MANAGEMENT_OBJECT` |
| PG4/PGn execution/outcome | none | `STANDALONE_MANAGEMENT_OBJECT` |
| HA-* human action | none | `STANDALONE_MANAGEMENT_OBJECT` |
| explicit typed relationship | declared target | validate kind and id |

The mapping layer is additive and cannot create canonical planning records.

## Required diagnostic codes

`MANAGEMENT_EVIDENCE_PATH_REQUIRED`, `ACTIVE_INCREMENT_UNKNOWN`,
`ACTIVE_INCREMENT_AMBIGUOUS`,
`EVIDENCE_CONFLICT`, `EVIDENCE_TARGET_UNMATCHED`,
`EVIDENCE_TARGET_AMBIGUOUS`, `EVIDENCE_TARGET_INVALID`,
`READINESS_STATE_RESULT_CONFLICT`, `GATE_EXECUTION_OUTCOME_INVALID`, and
`EVIDENCE_SOURCE_UNAVAILABLE`.

## Safe management meaning

Observations retain bounded semantic summaries instead of only normalized
codes. Owner, waiting-for, and required-authority roles are separate and may
carry a machine code, safe display label, and source wording. Readiness rows
may additionally carry blocker/pending, due-condition, and gate-effect
summaries. Decision and human-action observations may carry summary,
affected-target, completion-condition, and required-role fields. Raw rows,
documents, absolute paths, and secrets remain excluded.

## Gate records and successor proposals

Gate identifiers are source-derived from explicit labels such as `PG4`; the
generic view has no default gate. The bounded profile captures the contract
under `contracts/pg4-gate-record.md` and optionally captures the expected-later
actual record at `<increment>/pg4-gate-record.md`. The optional file is not a
missing-source error before its owning task executes. When present, an
attributable actual record outranks README/register summaries. A control
envelope's `ProposedSuccessorIncrementId` and bounded summary are projected
separately from gate effect and never inferred from planning documents.
