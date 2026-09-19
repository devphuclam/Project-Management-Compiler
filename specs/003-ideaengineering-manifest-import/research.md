# Research: IDEAEngineering Manifest Import

## Approved source and authority

The compatibility source is `devphuclam/IDEAEngineering` at commit
`0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4`. The only discovery file is
`planning/project-management-compiler-manifest.json`, and the only supported source
contract is `0.1.0`. Runtime code reads Git objects or a working tree directly; the
PowerShell source validator remains an oracle for compatibility tests, not a runtime
dependency.

The manifest declares seven roles. Roadmap owns objective, horizon, phases, and
milestones. Appendix A owns work packages, effort, and package dependencies. Kanban/
CARIO owns executable cards, dates, card dependencies, and CARIO mapping. The
execution register owns recording state, execution state, result state, actuals,
remaining effort, forecasts, blockers, evidence, revision, and status date. HTML
Gantt is a cross-check only. The readiness register owns readiness evidence only.
README navigation content owns no management field. Role ownership is independent of
read order; owner values win and subordinate conflicts become diagnostics.

## Repository boundary findings

The existing compiler already has typed identities, planning extraction, management
evidence/reconciliation, analysis, Gantt projection, canonical JSON, CARIO export,
loopback API, and a custom executable test runner. It also has a legacy fixed-path
source adapter and `ExecutionOverlay`. The new workflow must be additive: retain the
legacy adapter for compatibility, but do not let manifest import fall back to it.

IDEAEngineering's accepted package has a small, explicit source contract and seven
fixture outcomes. The accepted real package reports `PASS_WITH_WARNINGS`, zero
errors, and the intentional `PMC-CALENDAR-001` warning. Its totals are 6 phases,
35 work packages, 53 executable cards, 7 gates/milestones, 512 planned work hours,
88 controlled reserve hours, and 600 total capacity hours. P01 is recorded and
in-progress with result `NOT_APPLICABLE` and unknown actual/remaining values; the
other 52 cards are `NOT_RECORDED`, not confirmed `NOT_STARTED`.

## Technical decisions

### In-process validation

Use `System.Text.Json` and a bounded evaluator for the exact Draft 2020-12 subset
used by the source schemas: object/array typing, properties, required,
additionalProperties, items, uniqueItems, const, enum, oneOf, allOf, if/then,
minimum, multipleOf, minLength, format, pattern, and local `$ref`. Reject an
unsupported dialect or keyword with `PMC-CONTRACT-002`; do not claim generic JSON
Schema support. Semantic validation remains explicit and produces stable diagnostic
codes from the source catalogue.

### Capture and safety

For `GIT_COMMIT`, validate the requested object is a commit, read the manifest first,
then read only the manifest-declared paths from that same commit. Use `ProcessStartInfo`
argument lists rather than shell-concatenated commands. For a working tree, enforce
repository-relative paths, containment, reparse/link protections, size limits, and
pre/post fingerprints. A mutation rejects the capture as mixed. Persist only safe
repository identity, commit/preview identity, and relative provenance.

### State and migration

Application state has `CurrentOfficialSnapshot`, `LatestImportAttempt`, and
`ActivePreview`. A failed result cannot replace official. Canonical writes use schema
`2.0`; schema `1.0` reads are supported and old manual execution records migrate into
local proposals with an explicit migration diagnostic. They do not become source
actuals.

### Analysis and proposals

Official metrics consume source execution only. A proposal contains its base snapshot
ID and expected register revision, has `DRAFT`, `READY_FOR_REVIEW`, or `STALE_BASE`
lifecycle, and is never written back to IDEAEngineering. Explicit proposal preview
may calculate an estimated scenario but does not alter official digest, state, or
exports. Completion requires finish, actual effort, zero remaining, and controlled
evidence.

## Alternatives rejected

- Scanning known/fixed source paths: violates manifest sole discovery and could mix
  authority from unrelated files.
- Checking out a requested commit: mutates or depends on a user checkout and cannot
  guarantee all reads come from one immutable object.
- Calling the source PowerShell validator at runtime: adds process/environment
  coupling and violates dependency-free runtime behavior.
- Treating `NOT_RECORDED` as `NOT_STARTED`: invents execution evidence.
- Overlaying proposals onto official state: makes local estimates appear authoritative.

## Verification research outcome

The existing baseline suite passes before production changes. The implementation will
add public-seam tests for exact commit capture, working-tree mutation, manifest role
resolution, schema/semantic failures, source execution truth, state retention,
proposal immutability, canonical migration, and HTTP/launcher behavior. Full
verification will include the repository test script, `verify.ps1`, JavaScript syntax
checking if the UI changes, fixture catalogue comparison, and `git diff --check`.
