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

The adapter evaluates competing records in this order:

1. explicit authority/contract field;
2. gate or decision record;
3. readiness-register state/result;
4. task/register state;
5. checklist context;
6. prose.

If same-level or cross-level values conflict, retain all references and emit
`EVIDENCE_CONFLICT`; do not apply last-write-wins.

## Default mappings

| Source record | Default target | Rule |
| --- | --- | --- |
| P01–P07 work-package readiness | `WorkPackage:P01`–`P07` | target only if canonical package exists |
| D0–D5 decision | none | `DECISION_NO_DEFAULT_TARGET` |
| PG4 execution/outcome | none | `GATE_NO_DEFAULT_TARGET` |
| HA-* human action | none | `HUMAN_ACTION_NO_DEFAULT_TARGET` |
| explicit typed relationship | declared target | validate kind and id |

The mapping layer is additive and cannot create canonical planning records.

## Required diagnostic codes

`ACTIVE_INCREMENT_UNKNOWN`, `ACTIVE_INCREMENT_AMBIGUOUS`,
`EVIDENCE_CONFLICT`, `EVIDENCE_TARGET_UNMATCHED`,
`EVIDENCE_TARGET_AMBIGUOUS`, `EVIDENCE_TARGET_INVALID`,
`READINESS_STATE_RESULT_CONFLICT`, `GATE_EXECUTION_OUTCOME_INVALID`, and
`EVIDENCE_SOURCE_UNAVAILABLE`.
