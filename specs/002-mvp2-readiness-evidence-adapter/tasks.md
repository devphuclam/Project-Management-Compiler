# MVP2.1 Task Checklist

## Documentation and contract

- [x] T001 Create and review the feature specification.
- [x] T002 Create the implementation plan and review boundaries.
- [x] T003 Define evidence data model, source contract, and checklist.
- [x] T004 Add and inspect the minimum synthetic readiness fixture.

## Test-first seams

- [x] T005 Add failing model/serialization/digest tests.
- [x] T006 Add failing bounded-capture and path-policy tests.
- [x] T007 Add failing adapter extraction tests for P01–P07, decisions,
      gate, actions, and checklist context.
- [x] T008 Add failing authority/conflict and typed-reconciliation tests.

## Implementation

- [x] T009 Add additive management-evidence domain entities and diagnostics.
- [x] T010 Add optional readiness source profile and safe allow-list.
- [x] T011 Implement active increment identity and bounded package capture.
- [x] T012 Implement readiness adapter parsing and authority precedence.
- [x] T013 Implement typed reconciliation and immutable result composition.
- [x] T014 Compose evidence into compile, execution update, and reopen flows.
- [x] T015 Add JSON backward compatibility and semantic digest coverage.
- [x] T016 Add `ManagementControlView` and API projection.
- [x] T017 Enrich the existing UI shell and inspector without redesign.

## Verification

- [x] T018 Run focused red/green tests at each seam.
- [x] T019 Add end-to-end readiness fixture coverage.
- [x] T020 Extend launcher/web verification and inspect security assertions.
- [x] T021 Run build, test, and full verify from a fresh process.
- [x] T022 Inspect status, diff, fixture content, and commit boundaries.
- [x] T023 Review against all 30 regression categories.
- [x] T024 Report exact branch/commit/push/handoff state.

## Completion rule

MVP2.1 is not complete while any checked item lacks test evidence, while the
full verification is stale, or while the source/fixture safety review has not
been performed. A later human approval is required before merging to `main`.

## MVP2.1.1 evidence-fidelity hardening

- [x] H001 Add explicit readiness-path validation and distinguish
      `NOT_CONFIGURED` from unknown/ambiguous/unavailable discovery.
- [x] H002 Preserve owner, waiting-for, required-authority, blocker, pending,
      due, gate-effect, decision, and human-action meaning in bounded fields.
- [x] H003 Add `STANDALONE` reconciliation for management-only objects and
      remove unsupported canonical target kinds.
- [x] H004 Parse source-derived gate IDs and capture the optional increment-root
      actual gate record without treating its absence as a source failure.
- [x] H005 Add field-level effective evidence selection with authority
      precedence and honest equal-authority conflicts.
- [x] H006 Project effective gate/readiness state, proposed successor, source
      count, semantic attention, and inspector-safe provenance.
- [x] H007 Update the public-safe fixture, contracts, runbook, compatibility
      note, and verification evidence before the hardening closeout commit.

The hardening closeout is limited to **MVP2.1 Repository Readiness /
Management Evidence Ingestion + Evidence Fidelity Hardening**. It does not
declare the whole MVP2 complete and does not authorize production readiness.
