# MVP2.1 Readiness Evidence Adapter — Implementation Plan

> This plan is executable in the current repository and is based on the
> approved MVP2.1 prompt plus the verified IDEAEngineering readiness package.

## Goal

Add a safe, typed, additive management-evidence pipeline that can ingest the
IDEAEngineering readiness package, reconcile it to the MVP1 planning baseline,
project it through `ManagementControlView`, and survive persistence/reopen
without changing baseline or execution-overlay semantics.

## Constraints

* Work only in `codex/mvp2-readiness-evidence` worktree.
* Use existing .NET/runtime dependencies; do not install packages or CLIs.
* Preserve current 151-test MVP1 green baseline.
* Never copy the real IDEAEngineering repository or private source data.
* Do not use raw source content, absolute paths, latest/mtime selection, or
  unbounded directory scanning.
* Do not merge to `main` without explicit approval.

## Step 1 — Contract artifacts and safe fixture

Files:

* feature Spec Kit artifacts under
  `specs/002-mvp2-readiness-evidence-adapter/`;
* `docs/superpowers/plans/2026-09-19-mvp2-readiness-evidence-adapter.md`;
* minimum synthetic readiness fixture files.

Actions:

1. Commit the feature contract separately.
2. Inspect the fixture for secrets, absolute paths, private names, and
   unnecessary source material.

## Step 2 — Red tests for the domain seam

Add named custom-runner tests before production implementation for evidence
kinds, independent state/result, safe source references, typed targets,
additive serialization/defaults, digest behavior, and baseline/overlay
immutability. Run the focused tests and capture the expected RED result before
adding production model code.

## Step 3 — Red tests for bounded source capture

Add tests for explicit `specs/` path acceptance and traversal/root rejection,
fixed readiness path ordering, optional-file diagnostics, unchanged MVP1 path
sets, size/UTF-8/reparse behavior, safe references, and no arbitrary
recursion/latest-candidate choice. Extend the existing source request/path
policy only after those tests are red.

## Step 4 — Adapter and reconciliation

Implement `IManagementEvidenceSourceAdapter`,
`IdeaEngineeringReadinessAdapter`, authority/conflict validation, and typed
reconciliation. Test real-shaped synthetic P01–P07, D0–D5, HA-*, PG4
execution/outcome, conflicts, and all four reconciliation statuses. Raw source
text must not cross the domain boundary.

## Step 5 — Pipeline and persistence

Make the profile optional in compile requests. Attach evidence after planning
extraction and before views. Preserve it on execution updates and reopen.
Support old schema 1.0 as empty evidence. Include semantic evidence in the
digest while excluding timestamps and analysis.

## Step 6 — Control view, API, and UI enrichment

Expose only `ManagementControlView` to UI consumers. Add readiness profile
input without changing MVP1 shell structure. Display scope, current gate,
attention groups, and safe inspector provenance. Keep existing DOM security
checks and avoid raw HTML/source rendering.

## Step 7 — Fresh verification and review

Run:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\verify.ps1
```

Then inspect:

```powershell
git diff --check
git status --short
git diff --stat
```

Review all 30 regression categories, public fixture content, safe-path
boundary, and exact commit history. Push only a coherent verified increment;
report that MVP2.1 remains unmerged until the user approves.
