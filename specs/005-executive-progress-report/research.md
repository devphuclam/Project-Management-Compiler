# Research: Executive Progress Report Export

## Decision: Use a separate executive projection and XLSX exporter

**Rationale:** The existing CARIO + Gantt workbook is a technical/audit
contract and an accepted preview input. Management readability has different
sheet, terminology, density, and printing requirements. A distinct projection
keeps business semantics testable without XML details, while a distinct
exporter prevents presentation changes from altering the seven-sheet technical
contract.

**Alternatives considered:**

- Add an "executive mode" to `CarioXlsxExporter`: rejected because conditional
  sheets and styles would couple two contracts and make import compatibility
  harder to prove.
- Post-process the CARIO workbook: rejected because technical labels and row
  hierarchy would remain the source of the management layout.
- Export only selected CARIO sheets: rejected because their schemas still use
  technical identifiers and do not satisfy the approved progressive disclosure.

## Decision: Require an official manifest snapshot at two seams

**Rationale:** The endpoint will resolve `CurrentOfficialResult`, and the
compiler export method will independently require
`ImportMetadata.Classification == OfficialCommit`, a valid source reporting
date, and a compiled analysis. This prevents direct method use from labelling a
candidate, uncommitted working tree, XLSX preview, or local proposal projection
as the official management report.

**Alternatives considered:**

- Trust only the UI action state: rejected because endpoints and application
  methods remain callable without the browser.
- Treat any current compilation as official, matching the legacy fallback in
  `WorkbookExportSelection`: rejected for this export because the approved
  report explicitly represents official manifest truth.
- Allow preview export with a watermark: rejected because this feature is the
  file intended to be sent to management.

## Decision: Use source reporting date as the report identity

**Rationale:** `ImportMetadata.RegisterStatusDate` identifies how current the
source register is and drives the approved file name and `Ngày báo cáo` marker.
If `Analysis.AsOfDate` differs because an override was used, the compact
provenance text will disclose both dates instead of silently relabelling the
source date or recomputing analysis in the exporter.

**Alternatives considered:**

- Use export wall-clock time: rejected because it says when a file was created,
  not how current its facts are.
- Always use analysis override date without mentioning source currency:
  rejected because it can overstate evidence freshness.
- Re-run analysis inside the exporter: rejected because output adapters must
  not create a second analysis path.

## Decision: Keep title cleanup mechanical and lossless

**Rationale:** Reader-facing titles will remove surrounding backticks, repeated
leading bracketed identity prefixes, and a redundant leading identifier that
exactly matches the entity identity. Whitespace is normalized, but remaining
text and Vietnamese diacritics are preserved. Long text wraps; it is not
summarized or rewritten.

**Alternatives considered:**

- Generate friendly summaries: rejected because it introduces inference and
  can change source meaning.
- Truncate titles to a fixed character count: rejected because it can hide the
  distinguishing part of a task.
- Preserve every source decoration: rejected because codes such as
  `[PH2][C01-A]` are the main source of visual clutter.

## Decision: Calculate percentage only from valid recorded effort

**Rationale:** Completion percentage is
`actual / (actual + remaining) * 100`, rounded once to a whole percent with
`.NET MidpointRounding.AwayFromZero`, only when both values are present, finite,
non-negative, and the sum is greater than zero. Otherwise the report emits exactly
`Chưa đủ dữ liệu để tính % hoàn thành` and shows supported card-state counts.
No planned effort, elapsed time, card count, or proposal substitutes for
recorded actual/remaining effort.

**Alternatives considered:**

- Completed cards divided by total cards: rejected because cards are not
  guaranteed to have equal effort.
- Planned time elapsed: rejected because elapsed calendar time is not evidence
  of delivered work.
- Display `0%` when evidence is missing: rejected because zero is a factual
  claim, not an unknown state.

## Decision: Derive management attention through deterministic categories

**Rationale:** Candidates come only from valid official evidence and approved
analysis alerts with a supported management consequence. They are deduplicated
by source identity. The overview ranks only eligible items: blocked, overdue,
open decisions deterministically attributable to the nearest milestone,
at-risk, then important unowned work. Within a category, an attributable due
date sorts earliest first, missing dates follow, and canonical/source order is
the final tie-breaker. The overview takes the first five eligible items.

The complete action sheet additionally retains valid open decisions that cannot
be attributed to the nearest milestone and valid pending human actions, after
the overview-priority categories. Decision attribution uses only exact identity,
resolved target, strict ISO date, structured work-package due target, or exact
gate/milestone token rules in `data-model.md`; fuzzy title matching is forbidden.
Only canonical entities whose kind is `MilestoneKind.Milestone` may become the
next milestone or appear as milestone rows on the timeline. A
`MilestoneKind.Decision` remains a decision/action concern and is never promoted
to a schedule milestone merely because the domain stores both kinds together.
Important unowned work means a critical, blocked, overdue, or at-risk item with
no resolvable person or approved role; ordinary ownership gaps are not promoted.
Invalid, ambiguous, and purely technical diagnostics do not silently become
executive actions.

**Alternatives considered:**

- Reuse diagnostic severity ordering: rejected because technical severity is
  not management priority.
- List every warning: rejected because it recreates the audit workbook's noise.
- Let the exporter choose the top five: rejected because priority is a domain
  projection rule and must be testable without XLSX parsing.

## Decision: Separate schedule condition, readiness, and progress

**Rationale:** These answer different questions. Schedule condition derives
only from supported schedule alerts and analysis availability. Readiness
derives from valid management evidence/gate state. Progress derives only from
recorded execution effort and state counts. No absence of one signal is allowed
to turn another signal green. The exact precedence, Vietnamese labels, tones,
and sentence templates are normative in `data-model.md`; an implementer does
not choose new labels during coding.

**Alternatives considered:**

- One traffic-light project health value: rejected because it conceals whether
  the concern is schedule, readiness, or missing execution evidence.
- Use the absence of alerts as `Theo kế hoạch` unconditionally: rejected when
  schedule analysis is unavailable or insufficient.

## Decision: Use deterministic owner presentation

**Rationale:** Responsibility is selected from the source semantics before it
is translated. A delivery card uses only its CARIO `A` (accountable)
assignment; consulted, informed, observer, and performer assignments are not
promoted to owner. A work package prefers its valid direct readiness owner and
otherwise rolls up only when every child card resolves to the same accountable
owner. Evidence actions use their explicit owner/waiting-for/required-authority
field according to action kind. After this selection, one concrete identity is
preferred, or exactly one recognized role code is translated through the
approved Vietnamese role table in `data-model.md`. Arbitrary source prose is
not translated. Conflicts, unknown codes, and missing assignments display
`Chưa xác định đầu mối`; raw code lists are never concatenated.

**Alternatives considered:**

- Print every role code: rejected because it is unreadable and can imply
  ownership where mapping is unresolved.
- Infer a person from repository activity: rejected because repository activity
  is not approved assignment authority.

## Decision: Retain BCL Open XML and leave CARIO implementation untouched

**Rationale:** The project is dependency-free and the current exporter already
proves that the required XLSX subset can be emitted with ZIP/XML APIs. The new
exporter will own the four-sheet package and its styles. It may use small new
format-agnostic helpers local to the executive exporter, but this feature will
not extract or rewrite private CARIO internals.

**Alternatives considered:**

- Add ClosedXML or another spreadsheet package: rejected by the restricted
  environment and because no package is needed.
- Refactor both exporters onto a new shared framework first: rejected as a
  high-risk, unrelated change to the existing importable workbook contract.
- Use Excel/Office automation: rejected because it requires installed desktop
  software and is unsuitable for the local server process.

## Decision: Make layout and printing part of the contract

**Rationale:** A workbook sent to management must open on `Tổng quan`, use
100% zoom, wrap long reader text, freeze useful headers/labels, hide gridlines,
and configure the first two sheets for landscape fit-to-width printing. Month
groups with weekly columns provide useful schedule detail without one column
per calendar day.

**Alternatives considered:**

- Treat style as manual polish after implementation: rejected because the
  current problem is reader usability, not only data completeness.
- Daily columns: rejected because they create an excessively wide management
  report.
- One summary sheet only: rejected because managers still need traceable
  schedule, action, and work-item follow-up detail.
