---
status: accepted
date: 2026-09-22
---

# Separate the reader-facing management report from the future project workbook

Project Management Compiler will treat the management report and the future
round-trip project workbook as different artifacts. The management report is a
clean, presentation-only projection of one official snapshot; it cannot become
a tracking source or project-authoring surface. A future project workbook may
carry a versioned import contract, but project setup and normal low-code
management belong to the application's guided Project Setup Workspace, which
selects exactly one Tracking Source for ongoing progress.

This boundary was chosen over one all-purpose workbook because reader-oriented
formatting, concise wording, and management summaries are unsafe foundations for
round-trip data authority. Keeping the artifacts separate lets the report remove
technical clutter without weakening provenance, while a future project workbook
can preserve stable IDs, schema versions, revisions, and validation rules.

## Consequences

- The current `Xuất báo cáo tiến độ` output remains official-only and
  presentation-only.
- The current CARIO + Gantt workbook and its read-only preview contract remain
  unchanged by the report-readability increment.
- A future Project Workbook may include WBS, Gantt, Kanban, and structured
  round-trip data, but it will not replace the guided setup experience.
- The report may hide or relocate technical identifiers, but it must never
  alter source facts or fabricate missing Actual evidence.
