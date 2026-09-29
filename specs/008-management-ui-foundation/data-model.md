# Data Model: Management UI Workspace Foundation

**Feature**: [spec.md](spec.md)
**Source of truth**: existing canonical project, import result, compiled analysis, and `CompilerApplicationState`.

This feature adds projections and request state, not a second project persistence model.

## Local Source Preference

| Field | Type | Required | Rule |
|---|---|---:|---|
| repositoryRoot | string | yes to import | Local folder selected by the user; may be remembered in browser storage. |
| manifestPath | string | yes to import | Relative manifest path; may be remembered in browser storage. |

Only these two preferences may be stored by this feature in browser local storage. They do not imply that the server has an active project and never initiate an import by themselves.

## Local default-ref resolution result

| Field | Type | Rule |
|---|---|---|
| targetRef | string | Validated symbolic ref under `refs/remotes/origin/`; internal diagnostic/provenance only. |
| commitSha | string | Full commit object ID resolved from the target ref; this exact value is sent to the existing importer. |
| failure | typed error | Missing, malformed, disallowed, unresolvable, non-commit, command failure, or cancellation; no fallback. |

The ref is resolved once per import request. `targetRef` is not a substitute for `commitSha` and must not be used as the imported source identity.

## Official Project Snapshot

Reuse `IdeaEngineeringSnapshot`, import classification, and official compiled state. A valid official snapshot contains:

- the canonical project and its source provenance;
- exact imported commit identity;
- source reporting date / analysis-as-of date;
- official classification and existing validation result.

Candidate and working-tree previews remain separate from this snapshot. A failed/default-ref resolution does not mutate the last official snapshot.

## Recorded Progress Summary

| Field | Type | Meaning |
|---|---|---|
| recordedPercent | integer 0–100 or null | Project-wide effort progress; null unless complete valid coverage exists. |
| coverageComplete | boolean | True only when every delivery card has valid non-negative actual and remaining effort and a positive per-card sum. |
| eligibleCardCount | integer ≥ 0 | Number of delivery cards with an eligible effort pair. |
| totalCardCount | integer ≥ 0 | Total delivery cards in the active project. |
| actualEffortHours | decimal or null | Existing recorded actual effort aggregate. When coverage is incomplete, if shown, it is explicitly partial. |
| remainingEffortHours | decimal or null | Existing recorded remaining effort aggregate. When coverage is incomplete, if shown, it is explicitly partial. |
| completedCardCount | integer ≥ 0 | Completion evidence count, separate from effort-based progress. |

Calculation invariant:

```text
eligible card := actual and remaining are present, valid, non-negative,
                  and actual + remaining > 0
coverage complete := totalCardCount > 0 and eligibleCardCount == totalCardCount
recordedPercent := round to the nearest whole percent, midpoint away from zero,
                   of actualEffortHours / (actualEffortHours + remainingEffortHours) * 100
                   only when coverage complete and aggregate denominator > 0
otherwise recordedPercent := null
```

Never substitute completed-card count, planned completion, status labels, or an incomplete subset ratio for `recordedPercent`.

## Project Overview Projection

The Overview is a read-only projection with these conceptual sections:

| Section | Fields | Derivation |
|---|---|---|
| Project identity | displayName, reportingDate, sourceIdentity | Active official canonical project and import metadata. Keep source identity available in details, not as the primary headline. |
| Current phase | name, plannedStart, plannedFinish, state | Unique phase whose valid planned range contains the official source reporting date (`RegisterStatusDate`); otherwise unknown with a concise reason. If source dates conflict or overlap ambiguously, do not guess. |
| Recorded progress | Recorded Progress Summary | Existing effort evidence and shared eligibility semantics. |
| Next control point | name, plannedDate, kind, sourceState | Earliest valid planned milestone/decision-point date strictly after the official source reporting date that is not recorded complete; null when unavailable. Preserve its source-backed state when known; never derive a state from dates. Stable tie-break by source order, then identifier. |
| Attention items | up to three `{workItemName, consequence, severity, destination}` items | Existing actionable analysis alerts only; supported alert types use concise deterministic Vietnamese consequence labels. Never forward raw diagnostic messages or machine codes to the main summary; omit unsupported alert types rather than inventing a paraphrase. Order by severity descending, then source date/work item ID for deterministic output. |
| Completion count | completedCardCount, totalCardCount | Existing execution-status analysis; always distinct from recorded effort percentage. |

Reader-facing names are passed through the existing `ReaderFacingTextPolicy.CleanName` so known identity prefixes, duplicated IDs, and formatting noise do not dominate the workspace. IDs remain available only as secondary provenance/navigation metadata where needed to disambiguate repeated names.

## Existing models retained

- `CanonicalProject`, `ManagementAnalysis`, `Alert`, `Phase`, `MilestoneDecision`, `DeliveryCard`.
- `ManagementViewSet` becomes the owner of the `Overview` projection alongside current views.
- `CompilerApplicationState` remains the owner of active official/candidate import state.

## Invariants

1. An absent in-memory official project is an unloaded state, even if browser preferences exist.
2. Only an explicit user gesture starts an import.
3. Default-ref import resolves and imports one exact local commit.
4. Failed or non-official imports do not replace official state.
5. Overview facts derive only from the active official projection; candidate/preview data is not silently mixed in.
6. Missing, invalid, or partial source evidence is represented as unknown/partial, never filled by inference.
