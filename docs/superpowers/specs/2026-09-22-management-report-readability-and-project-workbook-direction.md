# Management Report Readability and Project Workbook Direction

**Status:** Approved through grilling rounds Q1-Q19 on 2026-09-22
**Current increment:** Humanize `Xuất báo cáo tiến độ` and add a proper WBS view
**Future direction:** Guided low-code project setup and a separate round-trip Project Workbook

## 1. Problem

The current executive workbook is factually traceable, but it still reads like
a technical export. Source IDs and prefixes are repeated inside titles, phrases
such as evidence coverage and provenance are overused, related operating views
are fragmented, and finish-only completion evidence can leave the Actual lane
visually empty. A management reader must decode the compiler instead of reading
the project.

The user also clarified a broader product direction: Project Management
Compiler must eventually support projects that have no pre-existing management
structure like IDEAEngineering. Those users need a guided way to define the
project, choose one tracking source, and then manage progress without editing
source documents manually.

These are two different horizons. The current increment improves only the
reader-facing report. It must not prematurely turn Excel into the application's
primary low-code authoring surface.

## 2. Approved product boundary

```text
Current supported project
  -> import one official source snapshot
  -> compile canonical project facts
  -> export a clean Management Report

Future unconfigured project
  -> guided Project Setup Workspace
  -> define WBS, schedule, roles, dependencies, and evidence policy
  -> select exactly one Tracking Source
  -> monitor and update through the application
  -> export a clean Management Report
  -> optionally export/import a separate versioned Project Workbook
```

The Management Report and Project Workbook are separate concepts:

| Artifact | Purpose | Import authority | Reader style |
| --- | --- | --- | --- |
| Management Report | Sponsor and manager review | Never | Clean, concise, presentation-first |
| Technical CARIO + Gantt workbook | Existing technical/audit workflow | Existing read-only preview only | Contract-first |
| Future Project Workbook | Archive and compatible round trip | Versioned and validated | Complete project views plus structured data |

## 3. Current increment scope

Change only the workbook downloaded by `Xuất báo cáo tiến độ`.

In scope:

- replace the five-sheet reader contract with the approved six-sheet contract;
- reorganize the reader journey from summary to action to supporting detail;
- add a complete WBS sheet with progressive disclosure;
- remove redundant IDs, source prefixes, raw states, and technical wording from
  reader-facing names and labels;
- present sparse Actual evidence visibly without inventing dates;
- preserve official-source truth, deterministic generation, traceability, and
  the existing export endpoint.

Out of scope:

- building the future Project Setup Workspace;
- changing source selection or manifest authority;
- adding a round-trip Project Workbook contract;
- changing the CARIO + Gantt workbook or XLSX preview importer;
- importing the Management Report;
- source write-back;
- a full Kanban board in the Management Report.

## 4. Reader journey and sheet contract

The workbook contains exactly these reader-facing sheets in this order:

1. `Tổng quan`
2. `Điều hành 30 ngày`
3. `Gantt`
4. `WBS`
5. `Chi tiết công việc`
6. `Thông tin báo cáo`

### 4.1 Tổng quan

The first sheet answers, in order:

1. Where is the project now?
2. How much supported progress is recorded?
3. What has changed from plan?
4. What milestone comes next?
5. What needs a decision?

It uses one-page landscape print layout. It contains concise headline values,
one phase/milestone schedule summary, and at most five priority actions. It does
not repeat source paths, commit hashes, validation codes, or long methodology
sentences.

### 4.2 Điều hành 30 ngày

This sheet replaces separate near-term and issue-reading journeys. It contains:

- decisions and blockers first;
- overdue unfinished work second;
- active work third;
- planned work and milestones intersecting the exact 30-day window last.

Each row states the action, consequence, owner, required date, state, and the
minimum schedule context needed to act. The sheet uses one-page landscape print
layout when the fixture population permits it and remains readable when it
continues to additional pages.

### 4.3 Gantt

The full daily schedule retains Project, Phase, Work Package, Delivery Card, and
milestone visibility. Each work item shows its readable name once, followed by
adjacent `Kế hoạch` and `Thực tế` lanes. The schedule is optimized for screen
reading and may paginate horizontally; daily columns must not be shrunk into
unreadable content.

### 4.4 WBS

The WBS includes every scope level:

```text
L0 Project
  L1 Phase
    L2 Work Package
      L3 Delivery Card
```

Milestones are not WBS elements and remain in Gantt and operating views.

Rows use outline levels so the workbook opens at Work Package depth while every
Delivery Card remains available through expansion. The visible core columns are:

1. `WBS`
2. `Mã`
3. `Hạng mục`
4. `Loại`
5. `Đầu mối`
6. `Trạng thái`
7. `% thực tế`
8. `Cần chú ý`

The `Hạng mục` column contains only the meaningful Reader-Facing Name. It must
not repeat `PH0`, `PLN01`, `[PH0][PLN01]`, entity-kind labels, hierarchy arrows,
Markdown decoration, or other identity noise. Stable identity appears once in
the separate `Mã` column.

Optional detail columns are grouped for collapse rather than omitted:

- plan group: planned start and planned finish;
- Actual group: actual start, actual finish, actual effort, remaining effort,
  and latest official update;
- relationship group: predecessor and dependency state;
- evidence group: concise completion/evidence summary and source reference.

The WBS describes scope decomposition. Dates and execution fields supplement
the hierarchy but never determine parentage or numbering.

### 4.5 Chi tiết công việc

This sheet retains one traceable row per Delivery Card. It owns the full
planning, Actual, forecast, effort, owner, evidence, and reference detail that
would overload WBS or the first two sheets. Filters and frozen identity columns
are required. Reader-facing columns precede technical identity and provenance.

### 4.6 Thông tin báo cáo

This sheet is the one place for report metadata, authority, source commit,
snapshot identity, baseline, contract version, reporting date, analysis date,
and concise limitations. Provenance is not repeated as prose on every sheet.

## 5. Reader-language contract

The workbook uses deterministic presentation cleanup, not generative rewriting.
Source meaning remains unchanged.

Required rules:

- one cell communicates one idea;
- headings are short and action-oriented;
- names do not repeat IDs already present in `Mã`;
- exact ID prefixes, bracket prefixes, hierarchy markers, Markdown decoration,
  raw enum values, and validation codes are removed from reader-facing fields;
- `Chưa ghi nhận` replaces verbose insufficient-data prose at row level;
- `Chưa phân công` replaces `Chưa xác định đầu mối`;
- `Cập nhật đến dd/MM/yyyy` replaces repeated provenance sentences;
- technical concepts such as authority rank, capture state, snapshot ID, and
  source path appear only where needed for traceability;
- raw English states such as `KNOWN`, `RECORDED`, `PLAN_ONLY`, or `COMPLETED`
  do not appear on the first five sheets;
- long descriptions wrap, but the report does not synthesize explanatory
  paragraphs merely to fill space.

The cleanup is stronger than simply removing one prefix. Tests must cover
repeated IDs, nested bracket prefixes, kind labels, Markdown markers, raw state
codes, source boilerplate, and long Vietnamese titles.

## 6. Plan, Actual, forecast, and missing evidence

The report preserves the existing evidence boundary:

| Evidence shape | Gantt presentation |
| --- | --- |
| Actual start + Actual finish | Green Actual interval |
| Actual start only and in progress | Green open interval through analysis date |
| Actual finish only | `✓` completion marker on the recorded finish date |
| Effort evidence but no Actual date | `●` marker with `Có ghi nhận` text |
| No execution evidence | Empty Actual timeline and `Chưa ghi nhận` text |

A finish-only completion marker proves a completion point, not a one-day
duration. The report must never infer an Actual start from Actual finish,
effort, planned dates, update timestamps, or completion state.

Progress percentage remains evidence-based:

```text
actual effort / (actual effort + remaining effort)
```

It is shown only when both values are finite, non-negative, and have a positive
sum. Otherwise the row uses `Chưa ghi nhận` or a concise evidence-specific
label; it never substitutes zero.

## 7. Visual and print contract

- Blue means plan.
- Green means recorded Actual.
- Amber means supported forecast or attention.
- Red means blocked, overdue, or required decision.
- Gray means missing or unavailable evidence.
- Every color meaning also has text or a symbol.
- `Tổng quan` and `Điều hành 30 ngày` target landscape print readability.
- `Gantt`, `WBS`, and `Chi tiết công việc` prioritize normal-zoom screen
  readability over forced one-page printing.
- Freeze panes preserve context during horizontal and vertical scrolling.
- WBS row groups and optional column groups provide progressive disclosure.
- Decorative gradients, oversized titles, repeated legends, and empty visual
  panels are excluded.

## 8. Kanban decision

The current Management Report does not add a full Kanban board. The operating
questions that matter to its reader are represented in `Điều hành 30 ngày`:
active work, blocked work, overdue work, upcoming work, and decisions.

A full Kanban belongs to the future Project Workbook because that artifact is
intended to preserve complete project views and support round-trip restoration.
This avoids duplicating the existing technical CARIO + Gantt export inside the
reader report.

## 9. Authority and safety

- Only the current official snapshot supplies report facts.
- Local proposals and previews never appear as official Actual.
- Export remains read-only with respect to application state and source
  repositories.
- The Management Report remains deliberately non-importable.
- Sparse but valid evidence produces truthful markers and labels.
- Contradictory evidence that cannot be represented safely still fails closed.

## 10. Acceptance outcomes

The increment is complete when all of the following are demonstrably true:

1. A management reader identifies current position, supported progress, next
   milestone, and top decision from `Tổng quan` within 60 seconds.
2. The workbook contains exactly the six approved sheets in order and opens on
   `Tổng quan`.
3. `Hạng mục` never repeats its row's `Mã` or source hierarchy prefixes.
4. Every Project, Phase, Work Package, and Delivery Card appears once in WBS,
   with valid hierarchy and outline depth.
5. WBS opens at Work Package depth and can expand to every Delivery Card.
6. Finish-only completion appears as a visible `✓` on the recorded finish date.
7. Effort-only Actual evidence remains visible without a fabricated date.
8. The first five sheets contain no raw technical state or validation code.
9. The first two sheets pass normal-zoom and print review; all remaining sheets
   pass normal-zoom screen review without clipped primary content.
10. Existing CARIO + Gantt export/import tests remain unchanged and green.
11. Export changes no official snapshot, proposal, preview, or source value.

## 11. Supersession and next workflow

This design supersedes only the reader-facing workbook contract delivered by
Feature 006. It does not rewrite Feature 006's completed history or change its
technical export boundaries.

The next delivery workflow should create a new feature specification from this
document, then run clarification only if implementation details remain open,
followed by plan, tasks, consistency analysis, TDD implementation, visual
verification, and the normal human review gate before push.

No grilling decision remains open for the current increment.
