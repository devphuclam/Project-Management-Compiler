# MVP2.1 — IDEAEngineering Readiness Evidence Adapter

## Status

Approved for implementation on the dedicated hardening branch
`codex/mvp2-readiness-hardening` after the MVP1 baseline was verified green.
This feature adds management evidence to the existing planning compiler; it
does not reopen the planning model or change the approved UI architecture.

## Problem

MVP1 compiles an authoritative planning baseline and an independent execution
overlay. IDEAEngineering also publishes readiness-package records that answer a
different question: what preparation, decisions, human actions, and gate
execution evidence exist for a named increment? The compiler currently has no
typed place to ingest those records, no safe way to distinguish a prepared
record from an executed result, and no reconciliation contract for mapping
readiness records to the planning hierarchy.

The result must let an operator answer, from one compiled project:

* which readiness increment was actually identified;
* which P01–P07 readiness records exist and what state/result they carry;
* which decisions and human actions remain open;
* whether PG4 execution and outcome are separately evidenced;
* which records match a planning target, do not match one, are ambiguous, or
  are invalid;
* what source references support each conclusion without exposing source
  content, local absolute paths, or private repository material.

## Scope

### In scope

1. Add an additive `CanonicalProject.ManagementEvidence` model.
2. Capture a bounded, allow-listed IDEAEngineering readiness package from an
   explicitly selected relative increment path under `specs/`.
3. Recognize only the declared package documents: package `README.md`,
   `readiness-register.md`, the decision/evidence and PG4 contracts,
   `tasks.md`, and `trace-matrix.md` when present.
4. Parse control-envelope, readiness-work-package, decision, gate, human-action,
   and checklist-context observations while preserving state and result as
   independent fields.
5. Reconcile observations through typed `(Kind, Id)` targets using explicit
   deterministic rules.
6. Emit structured diagnostics for unknown/ambiguous discovery, source
   unavailability, conflicts, and target/reconciliation failures.
7. Project a `ManagementControlView` for the existing control-center shell,
   Gantt indicators, and inspector; keep the MVP1 layout and visual language.
8. Persist, reopen, digest, and API-serve evidence and reconciliations.
9. Add a minimal safe public fixture shaped by IDEAEngineering readiness
   semantics. It must contain no private company content.

### Out of scope

* changing the MVP1 planning hierarchy, baseline dates, effort, dependencies,
  CARIO mapping, or execution-overlay semantics;
* treating `tasks.md` checkboxes, hashes, timestamps, or file presence as human
  approval or executed readiness results;
* arbitrary repository recursion, latest/mtime selection, or source-content
  persistence;
* automatic target mapping of decisions, PG4, or human actions to executable
  planning records;
* production-readiness authorization, equipment control, write-back, or
  autonomous decisions;
* importing the IDEAEngineering repository or copying its real private data;
* a visual redesign or a second management dashboard;
* declaring MVP2.1 complete before fresh build, tests, verification, and review.

## MVP2.1 hardening amendments

The readiness path is explicit for this increment. When repository readiness
evidence is requested, the caller must provide a safe relative path below
`specs/`; an empty path fails with `MANAGEMENT_EVIDENCE_PATH_REQUIRED`. The
compiler does not auto-discover readiness packages. `NOT_CONFIGURED` is kept
distinct from bounded-capture `UNKNOWN`, ambiguous identity, and unavailable
source states.

Management evidence preserves separate state, result, owner, waiting-for,
required authority, blocker, pending action, due condition, gate effect, and
bounded human-readable summaries. Role fields retain machine codes plus safe
display/source wording. Decision, human-action, checklist, control-envelope,
and gate records are valid standalone management evidence; only the canonical
target kinds `Project`, `Phase`, `WorkPackage`, `DeliveryCard`, `Milestone`,
and `Role` participate in canonical reconciliation.

Gate IDs are extracted from explicit source labels (`PG\d+`). No generic gate
view defaults to PG4. The bounded profile captures the gate contract and an
optional expected-later actual record at the increment root. Contract/template
files define permitted semantics but never current state. Effective gate and
field values are derived by authority precedence: actual attributable record,
readiness-register state/result, package README summary, contract/template
semantics, then bounded prose/checklist context. Equal-authority disagreement
returns `CONFLICT` with no selected value and preserves all observations and
source references.

The control view consumes only these derived selections. Proposed successor
increment identity and scope are extracted from the control envelope and are
not inferred from gate effect or planning documents. Baseline, execution
overlay, CPM, and alert semantics remain unchanged.

## Inputs and outputs

### Input

The existing compile request remains valid. An optional management-evidence
profile supplies:

* `ManagementEvidenceIncrementPath`: a normalized relative path such as
  `specs/004-technical-pilot-readiness`; and
* the existing repository location, ref, capture limits, and explicit compile
  date.

The path is accepted only when it is rooted below `specs/`, contains safe
segments, and resolves to the declared package files. A request without the
profile retains MVP1 baseline-only behavior. A requested profile without a
resolvable increment produces baseline output plus a structured discovery
diagnostic.

### Output

The compiler returns the existing canonical project, analysis, views, CARIO
mapping, and digest, with these additive values:

* `CanonicalProject.ManagementEvidence` — immutable observations,
  reconciliations, discovery identity, and safe diagnostics;
* `ManagementViewSet.ManagementControl` — the sole UI-facing projection for
  evidence scope, gate state, attention groups, and inspector sections;
* JSON/API/reopen support for the same values;
* a semantic digest that changes when evidence or reconciliation meaning
  changes, but not when capture timestamps or analysis-only values change.

## Functional requirements

### Evidence capture and identity

* FR-001: The adapter must resolve the active increment only from the explicit
  relative path or the single declared package marker. It must never enumerate
  arbitrary `specs/` directories or choose lexical latest/mtime latest.
* FR-002: README identity and readiness-register identity must agree before an
  increment is marked known. No agreement yields
  `ACTIVE_INCREMENT_UNKNOWN`; multiple declared candidates yield
  `ACTIVE_INCREMENT_AMBIGUOUS`.
* FR-003: Every observation must carry an opaque stable observation id,
  evidence kind, source record id, and safe source references.
* FR-004: Source references may contain repository-relative paths, ref, and
  safe line/table metadata only. They must not contain absolute paths, raw
  document bodies, credentials, or private configuration.
* FR-005: Capture remains bounded by the existing per-document and total-byte
  limits and uses strict UTF-8 validation.

### Semantics and authority

* FR-006: `StateCode`/`StateMeaning` and `ResultCode`/`ResultMeaning`
  are separate fields; `NOT-RUN` is not a pass and `NOT-APPLICABLE` does
  not mean success.
* FR-007: PG4 gate execution and PG4 gate outcome are separate observations.
  The adapter must reject an outcome that violates the contract for the
  execution state and emit `GATE_EXECUTION_OUTCOME_INVALID`.
* FR-008: Decisions and human actions are evidence records, not executable
  planning work items. Their open/blocked states must remain visible.
* FR-009: Authority precedence is deterministic: explicit contract/authority
  field, gate/decision record, readiness-register state/result, task/register
  state, checklist context, then prose. Conflicts preserve all references and
  emit `EVIDENCE_CONFLICT`.
* FR-010: `tasks.md` may provide checklist context only. It can never promote a
  readiness result to PASS or close a decision/gate.

### Reconciliation

* FR-011: Reconciliation uses typed `(Kind, Id)` identity, never an untyped
  string id.
* FR-012: P01–P07 readiness work-package records map to
  `WorkPackage:P01`–`WorkPackage:P07` only when the canonical target exists.
* FR-013: Decisions, PGn records, and HA-* human actions remain valid
  standalone management evidence unless an explicit typed relationship is
  present; they are not canonical planning work items.
* FR-014: Every observation has exactly one reconciliation status:
  `MATCHED`, `STANDALONE`, `UNMATCHED`, `AMBIGUOUS`, or `INVALID`. Candidate
  targets and the rule/reason are retained for non-matches.
* FR-015: Reconciliation never changes planning facts or creates a planning
  record from management evidence.

### Projection and persistence

* FR-016: `ManagementControlView` is the only UI-facing evidence projection.
  Consumers must not reconstruct evidence from raw observations.
* FR-017: The view exposes evidence scope, active increment, current gate and
  next-baseline context, grouped attention items, and inspector-safe details.
* FR-018: Old schema 1.0 JSON without management evidence reopens with an
  empty/not-requested evidence object and unchanged MVP1 behavior.
* FR-019: Reopen preserves evidence and reconciliation exactly enough for
  semantic digest equality. Execution updates preserve management evidence.
* FR-020: The digest includes semantic evidence fields and excludes capture
  timestamps, analysis summaries, and UI-only ordering.
* FR-021: `EffectiveEvidenceResolver` selects each semantic field by explicit
  authority rank; equal-rank conflicting values produce `CONFLICT` with no
  effective value and do not discard imported observations.
* FR-022: A missing optional actual gate record before its owning task executes
  is represented as `Not yet recorded`, not as a required-source failure.
* FR-023: Proposed successor identity and bounded scope are projected only
  when explicitly present in the control envelope.

## User scenarios and acceptance

### Scenario A — Baseline-only compatibility

Given an MVP1 planning fixture and no management-evidence profile, when the
project is compiled, then all existing MVP1 tests and views remain valid,
`ManagementEvidence` is empty/not-requested, and no readiness source is
recursively searched.

### Scenario B — Readiness package ingestion

Given a safe readiness-shaped fixture and an explicit increment path, when the
project is compiled, then the active increment is identified, P01–P07 records,
decisions, PG4 execution/outcome, and human actions are captured with safe
provenance, and the resulting view answers the control-center questions.

### Scenario C — Honest incomplete readiness

Given IDEAEngineering-shaped content where P01 is `IN-PROGRESS`/`NOT-RUN`,
P02–P07 are `NOT-RUN`, decisions are open, and PG4 is `NOT-RUN` /
`NOT-APPLICABLE`, then the output retains those exact states and never presents
the package as passed or production-ready.

### Scenario D — Ambiguous or conflicting evidence

Given duplicate candidate identities, conflicting authoritative fields, an
invalid target, or an illegal gate state/result pair, then compilation remains
safe and baseline facts remain intact while structured diagnostics and
reconciliation statuses expose the problem.

### Scenario E — Persistence and execution overlay

Given a compiled evidence-bearing project, when it is serialized, reopened,
and receives a valid execution update, then evidence, baseline, overlay, and
semantic digest behavior remain correct and independent.

## Regression categories

The implementation must cover at least these categories with named tests:

1. active-increment identity agreement; 2. explicit path boundary;
3. unknown discovery; 4. ambiguous discovery; 5. bounded readiness paths;
6. safe provenance; 7. no raw source persistence; 8. no absolute-path leak;
9. strict UTF-8/size limits; 10. P01–P07 extraction; 11. state/result
separation; 12. `NOT-RUN` is not pass; 13. decision extraction; 14. human
action extraction; 15. gate execution extraction; 16. gate outcome extraction;
17. illegal gate pair; 18. authority precedence; 19. conflict diagnostics;
20. typed matching; 21. unmatched decision/PG4/HA behavior; 22. ambiguous
target; 23. invalid target; 24. immutable baseline; 25. overlay independence;
26. JSON round-trip; 27. legacy schema compatibility; 28. semantic digest;
29. control-view attention groups; 30. API/UI readiness context and
verification security checks.

## Security and public-repository constraints

The repository is public. Fixtures and tests use synthetic, minimum-necessary
values. The implementation must not commit credentials, tokens, private
company configuration, employee data, proprietary CARIO internals, or a copy
of IDEAEngineering. Source content is parsed in memory and reduced to typed
facts plus safe provenance.

## Review gate

Before implementation code is merged, review the feature artifacts, model
invariants, source allow-list, authority rules, reconciliation table, and
public-fixture content. Before push, run the full applicable verification,
inspect status and diff, and report branch, commits, SHA, push state, and any
remaining work. Merging to `main` requires explicit user approval.
