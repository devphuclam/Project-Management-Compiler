# MVP2.1 Evidence Fidelity and Gate Semantics Hardening Implementation Plan

> **For agentic workers:** Use the native execution workflow for this plan. Work task-by-task with a failing test before each production change, then run the complete verification suite before handoff.

**Goal:** Make the existing MVP2.1 readiness adapter semantically faithful for roles, pending actions, standalone management evidence, gate authority, and explicit intake configuration without changing the planning, execution, CPM, alert, or UI architecture.

**Architecture:** Keep imported `ManagementEvidenceObservation` records immutable and add a derived `EffectiveEvidenceResolver` for field-level authority selection. The adapter remains the only IDEAEngineering-specific parser; the reconciler maps only canonical planning kinds and marks Gate/Decision/HumanAction/ChecklistContext/ControlEnvelope as valid standalone evidence. The existing `ManagementControlView` remains the only UI evidence projection.

**Tech Stack:** .NET 8/C# custom test runner, ASP.NET Core minimal API, vanilla JavaScript UI, bounded local repository capture, JSON canonical serializer.

**Spec:** `specs/002-mvp2-readiness-evidence-adapter/spec.md` plus the final hardening requirements recorded in this plan and the updated feature artifacts.

## Global Constraints

- Readiness capture requires an explicit relative increment path under `specs/`; there is no recursive or latest-candidate discovery.
- `PLAN`, `MANUAL ACTUAL`, `READINESS`, `GATE`, `DECISION`, `CPM`, and `ALERT` remain separate semantic layers.
- State, result, owner, waiting-for, blocker, due condition, gate effect, and authority remain separate fields.
- The public repository receives only bounded safe semantic summaries and relative provenance; no raw source rows, absolute paths, secrets, or IDEAEngineering repository copy.
- Canonical target kinds are `Project`, `Phase`, `WorkPackage`, `DeliveryCard`, `Milestone`, and `Role`; management-only objects are standalone evidence.
- Actual gate record evidence is optional before T026 and outranks summary/contract sources when present.
- No new packages, external services, recursive capture, live sync, forecast, database, authentication, or MVP2.2 scope.

## Review Focus

1. Missing readiness path must fail with `MANAGEMENT_EVIDENCE_PATH_REQUIRED`, not become `ACTIVE_INCREMENT_UNKNOWN`; test in the compiler/API intake seam.
2. Equal-authority state disagreement must produce `CONFLICT` with no selected value while preserving every observation; test in the resolver seam.
3. Contract/template evidence must never become a current gate PASS; test alongside actual-record precedence.
4. A same-ID `DeliveryCard:P04` must not inherit `WorkPackage:P04` readiness evidence; test reconciliation and Gantt inspector context.
5. Safe summaries must retain human meaning without raw table serialization; test fixture JSON and UI projection fields.

### Task 1: Domain semantics, standalone reconciliation, and effective selection

**Files:**
- Modify: `src/ProjectManagementCompiler/Domain/ManagementEvidenceEntities.cs`
- Create: `src/ProjectManagementCompiler/Management/EffectiveEvidenceResolver.cs`
- Modify: `src/ProjectManagementCompiler/Management/ManagementEvidenceReconciler.cs`
- Modify: `src/ProjectManagementCompiler/Outputs/CanonicalProjectValidator.cs`
- Test: `tests/ProjectManagementCompiler.Tests/Mvp21HardeningTests.cs`
- Modify: `tests/ProjectManagementCompiler.Tests/Program.cs`

**Interfaces:**
- `RoleReference` carries `Code`, bounded `DisplayLabel`, and bounded `SourceMeaning`.
- `ManagementEvidenceObservation` carries role references, `BlockerSummary`, `PendingActionSummary`, `DueConditionCode`, `DueConditionSummary`, `GateEffectCode`, `GateEffectSummary`, `Summary`, `ProposedSuccessorIncrementId`, `ProposedSuccessorSummary`, and `GateId` while retaining existing fields for JSON compatibility.
- `EvidenceReconciliationStatus.Standalone` represents valid management evidence without a canonical work-item target.
- `EffectiveEvidenceResolver.Resolve(ManagementEvidence)` returns immutable field selections with `SelectedObservation`, `SelectedValue`, `Status` (`RESOLVED`, `CONFLICT`, or `MISSING`), and source references.

- [x] Write tests proving role separation, standalone Gate/Decision/HumanAction, canonical-kind allow-list, field-level state/result conflict identity, and actual-vs-equal-authority effective selection.
- [x] Run the focused custom tests and confirm they fail for the missing enum/fields/resolver behavior.
- [x] Implement the smallest domain and resolver changes that make the tests pass; preserve observations and never select by document/parser order.
- [x] Run the focused tests and the existing reconciliation/JSON tests.
- [x] Commit with `feat: add effective management evidence semantics`.

### Task 2: Source adapter, safe semantic extraction, and bounded gate paths

**Files:**
- Modify: `src/ProjectManagementCompiler/Sources/SourcePathPolicy.cs`
- Modify: `src/ProjectManagementCompiler/Sources/LocalRepositorySourceAdapter.cs`
- Modify: `src/ProjectManagementCompiler/Management/IdeaEngineeringReadinessAdapter.cs`
- Modify: `tests/fixtures/ideaengineering-real-shaped/specs/004-technical-pilot-readiness/readiness-register.md`
- Modify: `tests/fixtures/ideaengineering-real-shaped/specs/004-technical-pilot-readiness/README.md`
- Create: `tests/fixtures/ideaengineering-real-shaped/specs/004-technical-pilot-readiness/pg4-gate-record.md` only in a dedicated precedence test fixture copy when needed
- Test: `tests/ProjectManagementCompiler.Tests/Mvp21HardeningTests.cs`

**Interfaces:**
- `SourcePathPolicy.GetManagementEvidencePaths` returns required bounded files separately from optional expected-later `pg4-gate-record.md`.
- The adapter parses explicit `PG\\d+` gate labels, actual root gate records, summary rows, contract rows, and control-envelope successor fields.
- Safe semantic text is normalized to bounded display text; raw table rows are never put in an observation.

- [x] Add failing fixture/adapter tests for P01 owner vs waiting-for, pending reviewer disposition, blocker summary, D0/HA summaries, parsed gate ID, explicit successor, missing actual gate record without noisy warning, and contract-not-current-state.
- [x] Run focused adapter tests and confirm the current implementation loses role/pending/successor meaning and hard-codes PG4.
- [x] Implement bounded path policy and source precedence metadata; retain contract capture but classify it as semantic contract, not current state.
- [x] Implement safe extraction rules with conservative ambiguity diagnostics rather than blind slash splitting.
- [x] Run adapter, fixture-safety, source-capture, and JSON tests.
- [x] Commit with `feat: preserve readiness meaning and gate provenance`.

### Task 3: Explicit intake contract, persistence, and compatibility

**Files:**
- Modify: `src/ProjectManagementCompiler/Application/ProjectCompiler.cs`
- Modify: `src/ProjectManagementCompiler/Application/IProjectCompiler.cs`
- Modify: `src/ProjectManagementCompiler/Program.cs`
- Modify: `src/ProjectManagementCompiler/Outputs/CanonicalJsonDigest.cs` only if new semantic fields need explicit digest coverage
- Test: `tests/ProjectManagementCompiler.Tests/Mvp21HardeningTests.cs`

- [x] Add failing tests for checkbox-without-path, explicit path loading, save/reopen of new semantic fields, baseline semantic equality, overlay preservation, and no absolute-path/raw-row serialization.
- [x] Run those tests and record the expected red failures.
- [x] Reject `IncludeManagementEvidence=true` without a non-empty path with `MANAGEMENT_EVIDENCE_PATH_REQUIRED` and the exact user-facing guidance; preserve baseline-only when the checkbox is false.
- [x] Ensure newly added semantic fields participate in canonical JSON and digest while capture timestamps remain excluded.
- [x] Run compiler/application, legacy-schema, overlay, and digest tests.
- [x] Commit with `fix: require explicit readiness evidence intake`.

### Task 4: Effective management-control projection and UI copy

**Files:**
- Modify: `src/ProjectManagementCompiler/Management/ManagementControlView.cs`
- Modify: `src/ProjectManagementCompiler/wwwroot/index.html`
- Modify: `src/ProjectManagementCompiler/wwwroot/app.js`
- Modify: `src/ProjectManagementCompiler/wwwroot/styles.css` only for additive inspector/readiness fields if existing styles cannot display them
- Test: `tests/ProjectManagementCompiler.Tests/Mvp21HardeningTests.cs`

- [x] Add failing projection tests for no fake PG4, effective gate state/outcome, proposed successor, compact readiness rows, source counts, meaningful decision/action attention, and detailed inspector fields.
- [x] Run focused projection tests and verify current `FirstOrDefault()`/hard-coded UI behavior fails them.
- [x] Project resolver selections and standalone reconciliation; show gate source/record status separately from next baseline control point.
- [x] Replace obsolete MVP1 intake wording with state-dependent repository-readiness wording and add path-required intake validation/helper.
- [x] Extend Gantt parent-context and management inspector details without changing execution state or layout architecture.
- [x] Run `node --check` when Node is available and web security checks.
- [x] Commit with `feat: project effective readiness control context`.

### Task 5: Spec/contract/runbook updates and full verification

**Files:**
- Modify: `specs/002-mvp2-readiness-evidence-adapter/spec.md`
- Modify: `specs/002-mvp2-readiness-evidence-adapter/data-model.md`
- Modify: `specs/002-mvp2-readiness-evidence-adapter/plan.md`
- Modify: `specs/002-mvp2-readiness-evidence-adapter/tasks.md`
- Modify: `specs/002-mvp2-readiness-evidence-adapter/contracts/source-adapter.md`
- Modify: `specs/002-mvp2-readiness-evidence-adapter/contracts/management-evidence.md`
- Modify: `docs/runbook/*` or the specific MVP2.1 runbook identified during inspection
- Create: `docs/superpowers/research/2026-09-19-ideaengineering-readiness-compatibility.md`

- [x] Document explicit path intake, standalone evidence, role semantics, bounded summaries, gate path/precedence, optional actual record, effective selection, and conflict behavior.
- [x] Record current IDEAEngineering HEAD review and approval-policy compatibility finding without copying private documents.
- [x] Update stale test counts and verification/runbook naming.
- [x] Run the complete custom test runner, approved restore/build, `scripts/verify.ps1`, `scripts/verify-web.ps1`, launcher verification, Node syntax check, and `git diff --check`.
- [x] Run real current IDEAEngineering compatibility cases A–J using the authorized checkout and a temporary synthetic gate-record projection outside the public fixture.
- [x] Perform self code review against the hardening prompt and report exact files, commits, branch/worktree, final SHA, status, push/merge state, and deferred items.
- [x] Commit with `docs: close MVP2.1 evidence fidelity hardening`.

## Self-review coverage

- The plan keeps the existing additive architecture and assigns each hardening requirement to a domain, adapter, compiler, projector/UI, or documentation seam.
- No production code is planned before a named failing test in every implementation task.
- The plan explicitly covers the five highest-risk inputs in Review Focus and the authority matrix cases A–D.
- The real IDEAEngineering package is used only for compatibility inspection; the public fixture remains synthetic and minimum-necessary.
