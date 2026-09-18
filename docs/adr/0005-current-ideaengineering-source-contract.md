# ADR-0005: Align extraction with the current IDEAEngineering source contract

## Status

Accepted for corrective remediation

## Context

The first implementation proved a parser against an invented English fixture
shape. The current IDEAEngineering planning source at commit
`afa9638f629999de6faa881ca25226cda44820a5` uses DOC-07 control/schedule tables,
section-context phases, Vietnamese Appendix A headers, Kanban task tables and
separate CARIO matrices. Treating one ranked document as owner of every field
also made subordinate rendition failures too destructive.

## Decision

Adapt the compiler to the source contract with explicit field-level authority:

- DOC-07 owns control, schedule baseline, phase windows, policy and gates;
- Appendix A owns work-package decomposition, effort, dependencies and outputs;
- Kanban/CARIO owns delivery-card decomposition, card detail/schedule and
  many-to-many responsibility assignments;
- HTML Gantt is a subordinate cross-check and README is navigation.

Canonical validity is reported by capability. Unknown dates and source states
remain unknown/null, capture provenance survives normalization, and runtime
source content is excluded from persisted canonical JSON. Work-package
dependencies remain traceability evidence and are not blindly duplicated into
the delivery-card CPM graph.

## Consequences

The controlled fixture must mirror the real structure while remaining
public-safe. Existing management/output work is not considered source-compatible
until the remediation contract tests pass. A malformed Gantt can produce a
rendition diagnostic without destroying a valid DOC-07 + Appendix baseline.
