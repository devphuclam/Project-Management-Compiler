# MVP2.1 Implementation Plan

## Architecture

MVP2.1 is an additive pipeline stage:

```text
bounded source capture
  -> IDEAEngineering readiness adapter
  -> authority/conflict validation
  -> typed reconciliation against CanonicalProject
  -> CanonicalProject.ManagementEvidence
  -> ManagementControlView
  -> JSON/API/UI/digest
```

The existing planning extractor remains the owner of the planning baseline.
The existing `ExecutionOverlay` remains the owner of actual execution facts.
The new adapter owns management/readiness evidence only.

## Work sequence

### 1. Contract and fixture foundations

Create the feature Spec Kit artifacts, management-evidence contract, source
adapter contract, and checklist. Add the minimum synthetic readiness fixture
under `tests/fixtures/ideaengineering-real-shaped/specs/` (or a clearly named
parallel fixture) with only safe facts required by the acceptance scenarios.

Inspect the fixture for secrets, absolute paths, private names, and accidental
reference-repository copies before committing it.

### 2. Test-first domain seam

Add named custom-runner tests before production implementation for:

* evidence enums, independent state/result, safe source references, and typed
  targets;
* additive canonical serialization/defaults and semantic digest behavior;
* profile path validation and bounded capture order;
* adapter extraction for control envelope, P01–P07, D0–D5, PG4, HA-*, and
  checklist context;
* authority/conflict diagnostics and typed reconciliation.

Run the focused tests and record the expected RED result before implementing the
seams. Keep test fixtures synthetic and deterministic.

### 3. Bounded source profile

Extend the existing source request and source-path policy with an optional
management-evidence profile. Preserve the five MVP1 planning paths and their
ordering when the profile is absent. With an explicit increment path, add only
the declared readiness package files and enforce:

* relative path and `specs/` boundary;
* safe segments and reparse-point checks;
* existing byte limits and strict UTF-8;
* stable diagnostics and safe source references.

Do not add recursive directory enumeration or latest-candidate guessing.

### 4. Adapter and validation

Implement `IManagementEvidenceSourceAdapter` and
`IdeaEngineeringReadinessAdapter` behind the existing parser abstractions.
Implement separate authority/conflict validation and typed reconciliation.
Keep raw source text out of domain results. Preserve all source references for
conflict and audit diagnostics.

The adapter must produce a baseline-safe result when evidence is absent,
unknown, ambiguous, unavailable, or invalid. It must never promote an
unexecuted checklist or task to a successful readiness result.

### 5. Pipeline, persistence, and projection

Compose the new stage in `ProjectCompiler`, initialize evidence for legacy
baseline-only compilation, and preserve it through execution updates and
reopen. Add JSON validation and semantic digest coverage. Add
`ManagementControlView` to `ManagementViewSet`, including evidence scope,
attention groups, gate context, and inspector sections. Extend API responses
and the existing UI shell without visual redesign.

### 6. Verification and handoff

Run focused tests after each seam, then:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\verify.ps1
```

Inspect `git diff --check`, `git status --short`, changed-file list, and
fixture content. Use focused commits, for example:

1. `docs: define MVP2.1 readiness evidence contract`
2. `test: specify bounded readiness evidence capture`
3. `feat: add typed management evidence model and adapter`
4. `feat: project readiness control view and persistence`
5. `test: verify MVP2.1 readiness fixture end to end`

Push only the coherent verified branch increment when authorized by the
workflow. Do not merge `main` in this pass without explicit approval.

## Files expected to change

### New

* feature Spec Kit artifacts under `specs/002-mvp2-readiness-evidence-adapter/`;
* `docs/superpowers/plans/2026-09-19-mvp2-readiness-evidence-adapter.md`;
* synthetic readiness fixture files and focused tests.

### Existing

* source request/path policy/capture seam;
* domain canonical-project model and validation;
* compiler orchestration, serializer, digest, and API DTO flow;
* management view projection and existing UI enrichment;
* verification script coverage.

No existing MVP1 planning document, baseline field, execution-overlay field,
or UI layout is to be removed or reinterpreted.

## Review focus

Reviewers should specifically challenge:

1. whether any path can escape `specs/` or trigger unbounded discovery;
2. whether a task checkbox, hash, or file timestamp can imply approval;
3. whether state and result are accidentally collapsed;
4. whether decisions/PG4/human actions are accidentally mapped to executable
   work;
5. whether source content, absolute paths, or sensitive fixture data can leak;
6. whether old schema 1.0 and MVP1 behavior remain unchanged;
7. whether the UI consumes only `ManagementControlView`;
8. whether tests actually prove the named regression categories.
