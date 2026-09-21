# Research: Executive Daily Gantt Report

## Decision: Upgrade the existing management export

**Decision**: Keep one user-facing progress-report action and replace the
Feature 005 four-sheet workbook contract with the approved five-sheet contract.

**Rationale**: The application already has an official-only export seam,
download naming, validation, and UI action. A second management workbook would
create two files that claim to answer the same executive question and would
force users to decide which one is authoritative.

**Alternatives considered**:

- Add a separate daily-Gantt download: rejected because it creates competing
  management reports and duplicate reconciliation work.
- Replace the report with only the three prototype sheets: rejected because it
  removes the complete action list and delivery-card traceability already
  delivered by Feature 005.

## Decision: Use exactly five sheets

**Decision**: Export `Tổng quan`, `Gantt theo ngày`, `30 ngày tới`, `Vấn đề cần
xử lý`, and `Chi tiết công việc`, in that order.

**Rationale**: The first three sheets implement the approved visual prototype
and operating view. The last two preserve management follow-up and audit
traceability without exposing the technical CARIO workbook.

**Alternatives considered**:

- Four sheets with issues embedded in the 30-day view: rejected because issues
  can concern work outside the 30-day window.
- Six sheets with a separate sources tab: rejected because provenance belongs
  in the canonical/technical export and a compact source line is sufficient for
  management.

## Decision: Render Plan and Actual as paired physical rows

**Decision**: Each logical Gantt item occupies a task band containing one Plan
row and one Actual row. Identity columns appear once for the task band; the two
lanes remain adjacent and share the same daily axis.

**Rationale**: Spreadsheet cells cannot reliably display two independent solid
fills in the same cell. Two narrow rows provide a clear Tracking-Gantt pattern,
allow different date intervals, and remain accessible without drawings or
macros.

**Alternatives considered**:

- Overwrite the beginning of the Plan bar with green progress: rejected because
  it hides the immutable baseline and conflates dates with percentage.
- Use top/bottom cell borders in one row: rejected because Actual and forecast
  become too thin at normal zoom and color changes at daily boundaries are hard
  to read.
- Use shapes: rejected because shape anchoring and printing are less
  deterministic and harder to verify in the current BCL-only exporter.

## Decision: Keep date progress and effort progress separate

**Decision**: Gantt lane position/length represents dates only. Percentage uses
`actualEffort / (actualEffort + remainingEffort)` only when both values are
finite, non-negative, and their sum is greater than zero.

**Rationale**: Calendar duration and completed effort are different measures.
Making a green bar 45% of a planned date span would fabricate an execution
date. A numeric percentage next to date-based lanes keeps both facts truthful.

**Alternatives considered**:

- Derive percentage from elapsed planned days: rejected as false execution
  evidence.
- Derive percentage from state labels or completed-card counts: rejected
  because those are useful counts but not effort completion.

## Decision: Use different authoritative dates for different questions

**Decision**:

- Source reporting date anchors provenance and the exact 30-date operating
  window.
- Analysis as-of date ends an open in-progress Actual lane.
- Recorded actual finish ends a completed Actual lane.

**Rationale**: The dates answer different questions. The register's reporting
date says how current the official source is; the analysis date says where an
open observation is evaluated; actual finish is a recorded execution fact.

**Alternatives considered**:

- Use wall-clock today: rejected because exports would change without a source
  change and would no longer be reproducible.
- Use source date for all open Actual lanes: rejected because analysis already
  has an explicit as-of boundary and the two dates may intentionally differ.

## Decision: Render forecast only from official forecast evidence

**Decision**: An amber future segment appears only when an official
`forecastFinish` is present. It starts after the analysis boundary and ends on
the official forecast date.

**Rationale**: Remaining effort alone cannot produce a finish date without a
resource calendar and capacity assumption. The compiler must not silently
invent those assumptions.

**Alternatives considered**:

- Convert remaining hours to calendar days: rejected because no approved
  capacity/allocation model exists.
- Extend to planned finish: rejected because that is the baseline, not a
  forecast.

## Decision: Make roll-ups conservative and coverage-aware

**Decision**: Delivery cards remain the atomic Actual truth. Phase and work-
package summaries may show known child activity, but they always show
`recorded/total` coverage. A roll-up percentage appears only when all relevant
delivery cards satisfy the percentage evidence rule.

**Rationale**: Averaging only recorded children makes sparse updates appear
like complete project coverage. Coverage must be visible next to any summary.

**Alternatives considered**:

- Treat missing cards as zero: rejected because not recorded is not the same as
  not started or zero progress.
- Calculate a partial percentage and add a footnote: rejected because the
  headline remains easy to misread.

## Decision: Define the near-term window as exactly 30 dates

**Decision**: `30 ngày tới` spans source reporting date through source
reporting date plus 29 days, inclusive. It includes unfinished overdue work and
planned intervals that intersect this window. Overdue open work sorts first,
then planned finish, then source order.

**Rationale**: An exact inclusive definition avoids 30-versus-31-day ambiguity
and keeps overdue work visible to operators.

**Alternatives considered**:

- Source date through plus 30 days inclusive: rejected because it contains 31
  calendar dates.
- Include only tasks whose start lies inside the window: rejected because it
  omits work that starts earlier but remains active during the window.

## Decision: Bound the full axis by attributable dates

**Decision**: The detailed axis includes baseline planning bounds and any
valid direct delivery-card Actual or official forecast date that falls outside
the baseline. Missing or invalid dates do not expand the axis.

**Rationale**: Clipping late Actual hides schedule variance. Allowing arbitrary
or invalid values to expand the workbook would make the report unusable.

**Alternatives considered**:

- Always clamp to baseline: rejected because late execution disappears.
- Use an arbitrary fixed padding: rejected because it can still clip evidence
  and adds unexplained empty dates.

## Decision: Separate projection, composition, and serialization

**Decision**: Add `ExecutiveDailyGanttProjector` for semantic rows,
`ExecutiveProgressWorkbookComposer` for the five-sheet layout, and a neutral
`ExecutiveWorkbookDocument` consumed by `ExecutiveProgressXlsxExporter`.

**Rationale**: The current executive projector and exporter are already large.
Adding hierarchy, Actual intervals, daily axes, merged headers, paired rows,
and print behavior directly would further mix unrelated responsibilities and
make focused tests harder.

**Alternatives considered**:

- Extend both existing files in place: rejected because semantic and XML
  changes would be tightly coupled in files already near one thousand lines.
- Introduce a general spreadsheet framework shared with CARIO: rejected as
  unnecessary scope and a risk to the stable technical workbook contract.

## Decision: Continue BCL-only XLSX generation

**Decision**: Keep deterministic Open XML package creation with installed BCL
APIs and add only the package capabilities needed by this report: worksheet
documents, merges, paired rows, borders, pane state, widths, and print setup.

**Rationale**: The restricted environment forbids package installation, and
the current exporter already proves the package path. Deterministic entry
timestamps and source-independent workbook values support reproducible tests.

**Alternatives considered**:

- Office automation: rejected because it requires desktop Office state and is
  unsuitable for deterministic local service execution.
- Add a spreadsheet package: rejected by restricted-environment rules and not
  needed for the bounded workbook features.

## Decision: Preserve the authority and compatibility boundaries

**Decision**: Keep the existing official-only compiler/application selection,
endpoint, file name, and UI action. The management workbook remains non-
importable. `CarioXlsxExporter` is regression-tested but not modified.

**Rationale**: Presentation changes must not create a new source of truth,
promote proposals/previews, or destabilize the technical integration artifact.

**Alternatives considered**:

- Allow proposal preview in the management workbook: rejected because a file
  sent to management would appear official.
- Reuse CARIO worksheet internals: rejected because it couples a presentation
  contract to a technical import contract.
