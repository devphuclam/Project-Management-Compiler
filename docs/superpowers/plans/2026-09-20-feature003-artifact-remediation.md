# Feature 003 Artifact Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reconcile the Feature 003 specification, plan, and task ledger with the findings from the latest read-only Spec Kit analysis, close the verifier-discovered typed Gantt identity regression, then verify and integrate the correction.

**Architecture:** Keep the existing Feature 003 design intact. Remove stale lifecycle/branch ambiguity, make evidence and equality semantics explicit, distinguish historical duplicate tasks from the current gate, and add a verifiable performance/evidence boundary. The only production correction is contract-preserving: XLSX preview identity is keyed by typed Gantt kind, raw ID, and lane so a `WorkPackage:P01` cannot collide with a `DeliveryCard:P01`.

**Tech Stack:** Markdown artifacts, C# regression test/source fix, PowerShell Spec Kit prerequisite/analyze workflow, Git fast-forward integration.

**Spec:** `specs/003-ideaengineering-manifest-import/spec.md`

## Global Constraints

- Do not modify the IDEAEngineering source repository.
- Do not add features, dependencies, database, connector, Docker, or UI redesign. The typed Gantt identity correction is the single verifier-driven source change allowed by this plan.
- Keep Feature 003 authority, proposal ownership, and fail-closed semantics unchanged.
- Preserve unrelated local changes in the main checkout.
- Run Spec Kit analyze read-only after the edits and do not push until verification is green.

---

### Task 1: Reconcile feature lifecycle and branch metadata

**Files:**
- Modify: `specs/003-ideaengineering-manifest-import/spec.md`
- Modify: `specs/003-ideaengineering-manifest-import/plan.md`
- Modify: `specs/003-ideaengineering-manifest-import/tasks.md`

- [X] Update the feature status and branch metadata so the three artifacts identify one current remediation state and branch without claiming unverified closure.
- [X] Preserve the original accepted source and authority decisions.
- [X] Review the diff for unrelated semantic changes.

### Task 2: Remove ambiguity from requirements and acceptance criteria

**Files:**
- Modify: `specs/003-ideaengineering-manifest-import/spec.md`
- Modify: `specs/003-ideaengineering-manifest-import/tasks.md`

- [X] Replace the truncated accepted commit in SC-001 with the exact source commit.
- [X] Define SC-006 and proposal-preview acceptance in terms of the canonical semantic digest rather than mixing byte equality and semantic equality.
- [X] Update the historical evidence tasks so the current controlled-evidence contract, including `Result`, is unambiguous.

### Task 3: Make task history and verification gates auditable

**Files:**
- Modify: `specs/003-ideaengineering-manifest-import/tasks.md`
- Modify: `specs/003-ideaengineering-manifest-import/plan.md`

- [X] Mark superseded hardening and integration tasks as historical records or consolidate their current acceptance text without deleting audit traceability.
- [X] Add a measurable performance verification task and name the artifact that records exact commands, elapsed time, and outputs for SC-024.
- [X] Recalculate requirement traceability and ensure every current FR/SC still has a task.

### Task 4: Spec Kit analysis and integration verification

**Files:**
- Verify: `specs/003-ideaengineering-manifest-import/spec.md`
- Verify: `specs/003-ideaengineering-manifest-import/plan.md`
- Verify: `specs/003-ideaengineering-manifest-import/tasks.md`
- Modify: `src/ProjectManagementCompiler/Outputs/XlsxPreviewImporter.cs`
- Modify: `tests/ProjectManagementCompiler.Tests/XlsxPreviewImporterTests.cs`
- Modify: `tests/ProjectManagementCompiler.Tests/XlsxPreviewTestFixtures.cs`
- Modify: `tests/ProjectManagementCompiler.Tests/Program.cs`
- Modify: `scripts/verify-web.ps1`

- [X] Run the prerequisite check and read-only Spec Kit analyze.
- [X] Run the repository verification commands, including the red/green typed-identity regression and the fail-closed `.xlsx` verifier-harness regression discovered by the full verifier.
- [X] Review the staged diff and commit only intended artifacts.
- [X] Fast-forward `main` to the verified commit and push `main` after the final
  pre-push gate.
