# Contract: Project Overview View

## Endpoints

- `GET /api/views` includes an `overview` property in the current aggregate view response.
- `GET /api/views/overview` returns the same Overview projection independently.
- When no compiled official project is active, both follow existing view behavior and return `404` with code `NO_PROJECT`.

The view is read-only and is derived from the active official compiled project, analysis, and source reporting/as-of date. It does not combine candidate or working-tree preview data.

## Projection shape

```json
{
  "project": {
    "name": "Dự án mẫu",
    "reportingDate": "2026-09-29",
    "sourceIdentity": "<exact source commit identity>"
  },
  "currentPhase": {
    "name": "Giai đoạn triển khai",
    "plannedStart": "2026-09-01",
    "plannedFinish": "2026-10-15",
    "state": "KNOWN"
  },
  "progress": {
    "recordedPercent": null,
    "coverageComplete": false,
    "eligibleCardCount": 2,
    "totalCardCount": 5,
    "actualEffortHours": 12,
    "remainingEffortHours": 18,
    "effortLabel": "PARTIAL",
    "completedCardCount": 1
  },
  "nextControlPoint": {
    "name": "Rà soát hồ sơ",
    "plannedDate": "2026-10-02",
    "kind": "MILESTONE",
    "sourceState": null
  },
  "attentionItems": [
    {
      "workItemName": "Chuẩn bị môi trường thử nghiệm",
      "consequence": "Công việc đang chậm so với kế hoạch.",
      "severity": "WARNING",
      "destination": "gantt"
    }
  ]
}
```

The example is illustrative; null/unknown fields are expected when evidence does not support a value. Reader-facing project/work names use the existing `ReaderFacingTextPolicy.CleanName`. IDs may be included as secondary traceability fields but must not replace readable names in the primary UI.

## Projection rules

### Reporting date and phase

- Use the official source reporting date (`RegisterStatusDate`) for phase and control-point position, not the workstation's current date or optional analysis-date override.
- A current phase is known only when valid planned dates uniquely identify one phase containing that date.
- Missing, conflicting, or ambiguous dates produce an explicit unknown state; do not choose a phase by array order or infer from status text.

### Effort progress

- A delivery card is effort-eligible only when actual and remaining effort are both known, non-negative, and sum to more than zero.
- Project-wide `recordedPercent` is present only if all delivery cards are eligible and the aggregate denominator is positive.
- Otherwise it is `null`, `coverageComplete` is false, counts are shown, and any aggregate effort values are labeled `partial`.
- Completed count is a separate field and must not be presented as an effort percentage.

### Next control point

- Select the earliest source-backed milestone or decision-point date strictly after the official reporting date that is not recorded complete.
- Preserve the point's source-recorded state when known; a missing state stays unknown and is never inferred from its date.
- If no candidate exists, return null and let the client show concise copy indicating that no future dated control point is available.
- Resolve equal dates deterministically; do not expose tie-break IDs as the user-facing name.

### Attention items

- Use existing actionable alerts only; no generated claims or new risk inference.
- Each item identifies a readable affected work name and a concise Vietnamese consequence selected by a deterministic mapping for supported alert types.
- Never render raw analysis alert messages or machine codes in the primary summary. Omit unsupported diagnostic types rather than exposing machine wording or guessing a translation.
- Return at most three, in deterministic severity/date/work-item order.
- If no actionable alert can be supported, return an empty list.

## UI contract

- A successful official import opens Overview.
- Overview is a primary destination beside Gantt.
- WBS, Kanban, and specialist tools remain reachable under Advanced.
- Loading, success, candidate/preview, and failure states are distinguishable; failed import preserves the previous official project.
- Primary controls remain keyboard-operable and visible at desktop and 360px viewport widths. Gantt may scroll within its own surface.
