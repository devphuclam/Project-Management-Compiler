# MVP2.1 Readiness / Execution Evidence Adapter Design

**Status:** Approved direction; implementation design for review

**Date:** 2026-09-19

**Feature:** Project Management Compiler MVP2.1 — IDEAEngineering readiness / execution evidence adapter

## 1. Context

MVP1 already compiles the IDEAEngineering planning sources into an immutable planning baseline, a typed WBS, a Gantt projection, management analysis, and a separate manual execution overlay. MVP2.1 adds a bounded adapter for the real `specs/004-technical-pilot-readiness/` package in the IDEAEngineering repository.

The adapter must make repository readiness evidence useful to a project manager without changing the meaning of the existing model. In particular:

- the authored plan remains the baseline;
- manual execution evidence remains an overlay;
- readiness, decisions, human actions, and gate evidence are management evidence;
- forecast and critical-path analysis continue to use only their existing inputs;
- source provenance and unresolved states remain visible;
- save/reopen must preserve evidence without rereading the repository.

The source package is not synthetic. On `origin/main` at `3dd955249cfaaec5c114b92dc4155f7e8d6e3ad1`, its current semantic state is:

- `P01` task state `IN-PROGRESS`, readiness result `NOT-RUN`;
- `P02` through `P07` task/result state `NOT-RUN`;
- `PG4` gate execution state `NOT-RUN`, gate outcome `NOT-APPLICABLE`;
- `D0` through `D5` are prepared/open decisions, not authority dispositions;
- `HA-001` through `HA-018` are human-action evidence, not delivery cards;
- the readiness package does not explicitly bind `PG4` to the existing `MS0` milestone.

That last fact is important: MVP2.1 must not infer a milestone relationship from a shared phase label, order, or ID resemblance.

## 2. Goals

1. Capture the minimum real readiness package needed to support management control views.
2. Preserve the source distinctions between task state, readiness result, gate execution state, gate outcome, decision state, and human action state.
3. Reconcile source targets against the canonical model using typed identity and explicit outcomes.
4. Preserve safe, relative, traceable provenance for every imported observation.
5. Expose one deterministic `ManagementControlView` consumed by all management UI surfaces.
6. Keep canonical JSON additive and backward-compatible with MVP1 documents.
7. Keep compile, recompile, manual overlay updates, and reopen semantics deterministic.
8. Fail safely to baseline-only when the active readiness increment cannot be identified.

## 3. Non-goals and hard boundaries

MVP2.1 does not add:

- XLSX/CSV execution import, CARIO API, GitHub/Jira synchronization, or other provider connectors;
- forecast/reforecast, what-if planning, multi-baseline history, leveling, configurable calendars, or portfolio aggregation;
- production qualification, runtime verification, security/performance/recovery claims, or authorization of a successor increment;
- a second execution/task store;
- automatic mapping based on mtime, current git commit, lexical directory order, proximity, shared raw IDs, or browser state;
- a new UI color/layout redesign or a replacement Gantt interaction model.

The current MVP1 management UI remains the shell. MVP2.1 enriches its existing control center, evidence scope, needs-attention, Gantt indicators, and inspector surfaces.

## 4. Architectural alternatives considered

### Option A — Put readiness fields on existing planning and execution records

This would add readiness properties directly to `WorkPackage`, `MilestoneDecision`, or `ExecutionOverlay`.

**Advantages:** fewer types and a small first patch.

**Rejected because:**

- it collapses baseline, manual actual, and repository evidence;
- it cannot represent a gate record, open decision, or human action without inventing a planning entity;
- it makes `WorkPackage:P04` and `DeliveryCard:P04` collisions easier to introduce;
- it would make a later adapter depend on IDEAEngineering-specific fields;
- it makes source conflicts indistinguishable from execution updates.

### Option B — A generic management evidence layer with a source adapter and reconciliation seam

The canonical project owns a separate evidence layer. A bounded source adapter emits source-shaped observations. A reconciliation component resolves explicit typed targets and records non-match outcomes. A control-view projector derives management summaries from evidence without rewriting the plan or overlay.

**Advantages:** preserves semantic boundaries, supports the current adapter without designing a provider framework, makes save/reopen auditable, and gives UI consumers one stable contract.

**Cost:** adds domain types, one extra compilation stage, and validation/serialization work.

**Decision:** selected. This is the approved MVP2.1 direction.

### Option C — Keep evidence outside canonical JSON and rebuild a temporary view

The adapter would emit a view-only result while the canonical project stayed unchanged.

**Rejected because:** evidence would disappear after save/reopen, a reopened result would require source access, and the application could not prove which evidence produced a displayed control state.

## 5. Proposed architecture

The compilation pipeline becomes:

```text
bounded repository capture
        |
        v
planning authority resolution + MVP1 extraction
        |                              \
        v                               v
canonical planning baseline       readiness source adapter
        |                               |
        +---------------+---------------+
                        v
          management evidence reconciliation
                        |
                        v
              canonical evidence layer
                        |
                        +--> management analysis
                        +--> ManagementControlView
                        +--> existing WBS/Gantt/Kanban views
                        +--> canonical JSON / semantic digest
```

The adapter reads only `RepositorySnapshot.Documents`; it never opens a path outside the bounded capture result. This keeps capture policy, parsing, and reconciliation independently testable.

### 5.1 Domain seam

Add a top-level additive property to `CanonicalProject`:

```text
ManagementEvidence Evidence
```

`ManagementEvidence` contains:

- discovery result and active increment identity;
- immutable imported observations;
- typed reconciliation results;
- evidence-specific diagnostics that are also surfaced through the existing warning contract.

The name is deliberately not `ExecutionOverlay`. Readiness evidence is not manual actuals and must never be accepted by `ExecutionOverlayUpdater`.

### 5.2 Generic observation model

The first implementation should use one observation shape with explicit kind and optional fields rather than one large source-specific class hierarchy. The model retains distinctions instead of forcing every source value into existing execution enums.

Conceptual fields:

```text
ManagementEvidenceObservation
  Id
  EvidenceKind                  // ReadinessCheck, GateExecution, GateOutcome,
                                // DecisionRecord, HumanAction, ControlEnvelope,
                                // ChecklistContext
  SourceRecordId                // P01, PG4, D0, HA-001, etc.
  RawTargetKind
  RawTargetId
  ExplicitTarget               // typed kind + id when the source declares one
  StateCode / StateMeaning
  ResultCode / ResultMeaning
  EvidenceDate / ObservedAt
  OwnerRole
  WaitingForRole
  DueCondition
  GateEffect
  BlockerOrDeviation
  EvidenceLinks                 // safe relative source references only
  SourceReferences
  AuthorityRank / AuthorityKind
  ValidationState
```

`StateCode` and `ResultCode` remain separate because the source explicitly distinguishes, for example, `IN-PROGRESS` task state from `NOT-RUN` readiness result and `NOT-RUN` gate execution from `NOT-APPLICABLE` gate outcome. Source tokens and meanings are preserved; a normalized `DataState` is added only where the mapping is lossless and is never used to replace the source tokens.

`EvidenceKind` is a closed enum for this feature. Unknown source rows are retained as `ValidationState.InvalidSourceEvidence` diagnostics rather than silently treated as readiness checks.

### 5.3 Typed targets and reconciliation

The existing `CanonicalWorkItemKey` is the identity vocabulary for canonical work items. Evidence reconciliation uses an equivalent serialized target shape:

```text
EvidenceTarget
  Kind                         // WorkPackage, DeliveryCard, Milestone, Phase
  Id
```

Raw IDs are never resolved without a kind. The initial explicit mappings are:

| Source record | Canonical target | Rule |
|---|---|---|
| readiness register `P01`–`P07` | `WorkPackage:P01`–`WorkPackage:P07` | explicit source schema says these are work packages |
| `D0`–`D5` | no canonical work item by default | decision records remain evidence unless an explicit source relationship exists |
| `PG4` gate execution/outcome | no milestone by default | gate is a distinct evidence object |
| `HA-*` | no delivery card by default | human action is management evidence, not executable work |
| any source target with an explicit typed relationship | declared target | preserve the relationship and its provenance |

The reconciler emits one record per imported observation:

```text
EvidenceReconciliation
  ObservationId
  Status                       // MATCHED, UNMATCHED, AMBIGUOUS, INVALID
  ResolvedTarget
  CandidateTargets
  RuleId
  Reason
  SourceReferences
```

Rules:

- `MATCHED` requires exactly one valid typed target;
- `UNMATCHED` means the source record is valid evidence but has no canonical target;
- `AMBIGUOUS` means more than one typed candidate remains and no explicit source mapping disambiguates it;
- `INVALID` means the source row or its target contract is malformed;
- none of these statuses is repaired by selecting the nearest row or shared raw ID;
- unresolved evidence remains visible in diagnostics and the control view.

The `P04` collision fixture must prove that readiness `P04` resolves to `WorkPackage:P04` and never to `DeliveryCard:P04`.

### 5.4 Source adapter contract

Add a bounded interface at the management/source seam:

```text
IManagementEvidenceSourceAdapter
  Adapt(RepositorySnapshot snapshot, CanonicalProject planningProject)
    -> ManagementEvidenceAdapterResult
```

`ManagementEvidenceAdapterResult` contains source-shaped observations, safe provenance, and adapter diagnostics. It does not mutate the project and does not resolve targets by itself.

The only implementation in MVP2.1 is `IdeaEngineeringReadinessAdapter`. Its parser recognizes the current readiness package’s bounded sources:

- `README.md` as package identity/status context;
- `readiness-register.md` for control envelope, P01–P07 rows, D0–D5, PG4 state/outcome, and Human Action Board;
- `contracts/decision-and-evidence-register.md` for field semantics and validation rules;
- `contracts/pg4-gate-record.md` for gate field/outcome semantics;
- `tasks.md` only as author-preparation context, never as execution authority;
- `trace-matrix.md` only where needed to retain P07/gate trace evidence.

The adapter does not ingest every file under `specs/004-technical-pilot-readiness/`. A source row that links to an un-captured file keeps the safe relative link and receives an explicit unresolved/limited-evidence diagnostic.

### 5.5 Active-increment discovery and capture safety

The current planning capture policy remains unchanged for MVP1. Readiness capture is a separate optional profile with a strict declared candidate:

1. The caller may provide an explicit relative increment path under `specs/`.
2. Without one, the IDEAEngineering readiness profile probes only its declared package marker, not arbitrary recursively enumerated files.
3. The package is accepted only when its README and readiness-register identity markers agree.
4. The adapter never chooses by mtime, current commit, directory name ordering, or “latest” content.
5. If no declared candidate is valid, emit `ACTIVE_INCREMENT_UNKNOWN` and compile the planning baseline without readiness evidence.
6. If a future configuration declares multiple candidates without an explicit selection, emit `ACTIVE_INCREMENT_AMBIGUOUS` and remain baseline-only.

Every captured readiness path must pass the existing root, segment, reparse-point, UTF-8, per-file, and total-byte checks. The browser/API receives relative paths and sanitized section/table/item references only; absolute filesystem paths and raw source document content remain unavailable.

### 5.6 Authority and conflicts

The adapter records authority rather than inventing it. For the current package, precedence is:

1. explicit contract/authority register field;
2. gate or decision record state;
3. readiness-register state/result row;
4. task/register state;
5. `tasks.md` checkbox context;
6. explanatory prose.

If two sources assert incompatible values for the same semantic field at the same applicable authority level, the result is an `EVIDENCE_CONFLICT` diagnostic with all safe source references. There is no last-write-wins behavior. A conflict does not rewrite either observation and does not change the planning baseline.

### 5.7 Analysis and management control view

`ManagementAnalysis` receives an evidence-derived management summary in a separate property, or an equivalent sibling projection if that is cleaner for the existing API. The summary includes:

- readiness counts by state/result;
- current gate execution and outcome as separate values;
- decision counts and unresolved/open decisions;
- human action counts grouped by state/role/effect;
- matched/unmatched/ambiguous/invalid reconciliation counts;
- evidence diagnostics and authority conflicts;
- an explicit evidence as-of/capture scope.

Add `ManagementControlView` to the existing `ManagementViewSet`. It is the sole UI-facing source for:

- EVIDENCE SCOPE;
- needs-attention groupings;
- compact Gantt readiness/gate indicators;
- work-package and inspector evidence sections;
- current gate evidence versus next baseline control point.

The control view must not turn an evidence state into a forecast, schedule variance, completion percentage, or execution actual. In particular:

- `NOT-RUN` is not `PASS`;
- a source date is not an actual completion date;
- a readiness result is not an `ExecutionState`;
- `PG4` outcome is not `MS0` completion;
- `D0` recommendation is not authority approval;
- `HA-*` is not a `DeliveryCard`.

### 5.8 UI integration

The existing UI surfaces are enriched without a new visual system:

- **EVIDENCE SCOPE:** baseline, manual execution, repository readiness, gate/decision evidence, and as-of date;
- **Needs Attention:** separate `SCHEDULE`, `READINESS`, `DECISION`, and `GATE` groups;
- **Gantt:** compact evidence indicators attached to the reconciled typed target or an explicit unbound evidence lane; no readiness dates are painted as actual bars;
- **WorkPackage inspector:** readiness evidence for `WorkPackage:P01`; a same-ID `DeliveryCard:P01` does not inherit it;
- **Gate/milestone inspector:** baseline milestone information and current gate execution/outcome are separate blocks;
- **Phase context:** a readiness phase is shown only when the source explicitly binds it; otherwise `Readiness phase: Not resolved`;
- **Current action language:** only source roles, waiting-for roles, due conditions, and blockers are shown. The UI does not invent “current user action.”

The pre-existing day-granularity Gantt behavior remains intact and is not redesigned as part of this evidence adapter.

## 6. Persistence and lifecycle semantics

### Compile

1. Capture the bounded planning and readiness documents.
2. Resolve and normalize the planning baseline exactly as MVP1 does.
3. Adapt readiness documents into observations.
4. Reconcile observations against the normalized project.
5. Attach the immutable `ManagementEvidence` layer.
6. Rebuild analysis, `ManagementControlView`, existing views, CARIO mapping, and semantic digest.

### Recompile

Recompile creates a fresh evidence snapshot from the current source capture but does not delete or reinterpret the manual `ExecutionOverlay`. If a source observation conflicts with a manual actual for the same semantic field, preserve both and emit a structured conflict; never silently replace the overlay.

### Manual execution update

`ApplyExecutionUpdate` changes only the existing `ExecutionOverlay`, then rebuilds all derived views. It does not update or delete repository evidence.

### Save/reopen

Canonical JSON stores observations, reconciliation records, safe provenance, discovery status, and the manual overlay. Reopen validates and projects the stored model without source access. Missing `managementEvidence` in a legacy MVP1 schema-1.0 document means an empty evidence layer, not a failed reopen.

### Digest

The semantic digest includes the canonical evidence layer and reconciliation records, while continuing to ignore capture timestamps and derived `Analysis`. Two source captures with different timestamps but equal evidence bytes must have the same digest.

## 7. Validation and diagnostics

Add validator rules for:

- unique observation IDs and reconciliation IDs;
- valid evidence kinds and non-empty source record IDs;
- safe relative provenance only;
- typed targets that resolve to the declared canonical kind;
- no `DeliveryCard` target for a readiness work-package row unless an explicit source relationship exists;
- gate execution and gate outcome stored separately;
- gate outcome `NOT-APPLICABLE` before a complete attributable decision;
- `NOT-APPLICABLE` never normalized to `PASS`;
- source date never copied into manual actual fields;
- reconciliation candidate sets and status consistency;
- conflict diagnostics include all relevant source references;
- no absolute path, browser content, secret, or raw source document content in serialized/API output.

Required diagnostic codes include at least:

```text
ACTIVE_INCREMENT_UNKNOWN
ACTIVE_INCREMENT_AMBIGUOUS
EVIDENCE_CONFLICT
EVIDENCE_TARGET_UNMATCHED
EVIDENCE_TARGET_AMBIGUOUS
EVIDENCE_TARGET_INVALID
READINESS_STATE_RESULT_CONFLICT
GATE_EXECUTION_OUTCOME_INVALID
EVIDENCE_SOURCE_UNAVAILABLE
```

Diagnostics are deterministic, safe to show in the UI, and traceable to relative source references.

## 8. Test strategy

The custom runner remains the test harness. New tests follow red-green-refactor and target the seams rather than browser DOM details.

### Domain and adapter tests

- model preserves separate state/result values;
- P01 maps to `WorkPackage:P01`;
- P04 never resolves to `DeliveryCard:P04`;
- P01 `IN-PROGRESS` plus result `NOT-RUN` remains exactly that;
- P02–P07 `NOT-RUN` remain visible;
- PG4 execution and outcome are independent;
- `NOT-APPLICABLE` is not `PASS`;
- a source date does not become actual completion;
- D0 remains a decision record, not a milestone completion;
- HA-001…HA-018 remain human-action evidence;
- `tasks.md` checked rows do not become execution complete;
- unmatched, ambiguous, invalid, and conflict outcomes are deterministic;
- bounded capture rejects traversal/reparse/oversize/invalid UTF-8 and preserves safe refs;
- absolute paths, browser content, and source document bodies never enter JSON/API.

### Pipeline, persistence, and regression tests

- planning baseline is byte/semantic equivalent when readiness files are absent;
- current MVP1 fixtures compile with an empty evidence layer;
- real-shaped readiness fixture compiles with expected P01/P02–P07/PG4/D0/HA evidence;
- save/reopen preserves evidence, reconciliation, provenance, digest semantics, and manual overlay;
- reopen does not reread source;
- recompile refreshes evidence while preserving manual overlay;
- manual overlay and evidence conflicts are both retained;
- `ManagementControlView` is shared by control-center, attention, Gantt, and inspector projections;
- evidence does not alter scheduled phase, baseline control point, CPM, or forecast;
- baseline-only, readiness-only, readiness-plus-manual, post-MS0/PG4-NOT-RUN, typed-P04 collision, and unmatched decision/action scenarios are covered.

The implementation plan will turn this list into the required 30 named regressions and will add only a small real-shaped fixture, not a copy of the full readiness package.

## 9. Delivery sequence after design approval

1. Create the next Spec Kit feature directory without reusing the MVP1 directory; record the feature spec, plan, tasks, data model, contracts, authority rules, security boundary, and acceptance scenarios.
2. Add failing domain/adapter tests for the evidence layer and typed reconciliation.
3. Implement bounded capture, adapter parsing, reconciliation, validation, serialization, and digest support.
4. Add analysis/control projection and UI/API wiring without changing the existing visual shell.
5. Add real-shaped compatibility tests against the captured semantics from IDEAEngineering `origin/main` without modifying that checkout.
6. Run focused tests after each seam, then the fresh full verification suite, launcher verification, JSON save/reopen checks, and browser acceptance cases.
7. Request code review against the feature spec and this design; fix findings with evidence.
8. Commit focused changes, push `codex/mvp2-readiness-evidence`, and only merge to `main` after explicit integration approval.

## 10. Design review checklist

- [x] Baseline, manual execution, and repository evidence have separate owners.
- [x] State/result, gate execution/outcome, and decision/action semantics are not collapsed.
- [x] Typed identity protects `WorkPackage:P04` versus `DeliveryCard:P04`.
- [x] Unmatched and ambiguous source rows remain visible instead of being guessed.
- [x] Active-increment discovery is bounded and has a baseline-only unknown path.
- [x] Provenance is relative and source-safe.
- [x] Save/reopen does not require source access.
- [x] Recompile does not delete manual overlay.
- [x] Existing UI layout remains the integration surface.
- [x] The source facts observed on `origin/main` are represented without upgrading author preparation to execution evidence.

## 11. Approval gate for implementation

This document records the approved architecture direction. Implementation must not begin until the Spec Kit feature artifacts and the implementation plan have been written and reviewed against this design. Any change to canonical terminology, source authority, evidence ownership, or baseline/reopen semantics requires an updated design decision before code changes proceed.
