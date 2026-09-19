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

## MVP2.2 audit findings and decisions

The independent audit found correctness defects in migration, canonical reopen,
working-tree stability, Git capture bounds, proposal validation, proposal
persistence, and authority wording. These are corrections to the accepted Feature
003 semantics, not a new feature surface.

### Migration authority

Schema `1.0` `ExecutionOverlay` records are retained as deterministic local
proposals, then the overlay is neutralized in the reopened project. This keeps the
historical values and migration diagnostic without leaving a second effective
execution source for `ExecutionTruthResolver`, management analysis, alerts, or
Gantt actual lanes.

### Canonical v2 semantic validation

Reopen validates `ImportMetadata`, `SourceExecution`, and `ExecutionProposals`
against the canonical planning graph and safe-path policy after JSON
deserialization. Target identity, duplicate identity, state/evidence semantics,
safe provenance, lifecycle, and proposal field values are fail-closed. A JSON
document that parses but violates authority semantics is invalid.

### Capture atomicity and bounds

Working-tree stability compares independently read pre/post manifests, declared
boundaries, captured files, and verified Git state. Git-state failure is an error,
not a comparable sentinel. Exact Git capture obtains tree mode and blob size before
reading body content, rejects mode `120000`, and clamps caller limits to explicit
application ceilings.

### Proposal ownership and evidence

`CompilerApplicationState` owns the retained proposal set. The canonical project's
`ExecutionProposals` is the persistence projection; import/reopen hydrates state,
and create/update updates the current canonical projection without mutating
`SourceExecution`. Proposal completion requires valid controlled evidence, not a
non-empty list.

The controlled evidence type allow-list follows the accepted execution-register
contract: `SOURCE_RECORD`, `COMMIT`, `PULL_REQUEST`, `TEST_RESULT`,
`REVIEW_RECORD`, `ARTIFACT`, and `EXTERNAL_RECORD`. Optional commit values are
hexadecimal object tokens; external URIs are HTTP(S) without embedded user info.
The canonical source-execution snapshot retains the validated project and
baseline identities used by the canonical import metadata; their equality and
revision/status-date relationships are checked so metadata tampering cannot be
accepted merely because JSON parses.

### Presentation and environment truth

The UI uses source/proposal/forecast terminology that matches authority. Feature
003 documentation targets .NET 10 and the dependency-free project file; the
project has no third-party spreadsheet dependency. The accepted IDEAEngineering
commit and its seven fixture outcomes remain the compatibility oracle.

## Final correctness micro-pass findings

The post-hardening audit found narrow compatibility and semantic gaps. They do
not add a product surface or change the source repository.

- The legacy fixed-path compiler remains an explicit compatibility workflow. Its
  non-empty `ExecutionOverlay` is effective only when no manifest metadata is
  present. Manifest-backed `SourceExecution` remains authoritative, and schema
  `1.0` migration still clears the overlay after converting it to proposals.
- The source execution schema requires `evidenceId`, `type`, `description`,
  `result`, `recordedAt`, and `recordedBy`. Repository path, commit, and
  external URI are optional fields, so proposal validation must not invent a
  locator requirement. Each supplied locator is independently checked.
- A proposal service must reject malformed supplied evidence at its create/update
  boundary rather than retain it as a diagnosed draft. This preserves atomic
  state and lets `READY_FOR_REVIEW` mean both completion semantics and valid
  evidence. Empty evidence remains a legitimate draft state.
- The compatibility execution route cannot turn a legacy file reference into a
  controlled source record. A safe `legacyEvidenceReference` proposal field is
  the non-authoritative representation; no fake type, recorder, or result is
  acceptable.
- The canonical validator must enforce the source schema's full commit identity,
  credential-free URI identity, revision/date/ID relationships, and 0.5-hour
  granularity. Schema-1.0 migrated proposal revision zero is the deliberate
  compatibility exception.
- Exact Git capture needs the remaining aggregate budget before the body read;
  checking only after `git show` is too late. A fake runner must prove that the
  show operation is not called.
- Mixed sessions need one projection operation for the retained proposal list.
  Reopening a legacy document must not make it official, but it must not leave
  `Current.Project.ExecutionProposals` stale when an existing official result is
  also retained.
