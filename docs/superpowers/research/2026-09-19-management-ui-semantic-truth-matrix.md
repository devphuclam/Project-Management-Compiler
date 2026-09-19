# Management UI Semantic-Truth Matrix

Date: 2026-09-19  
Scope: presentation rules for the MVP1 shell and the additive MVP2.1
repository-readiness control view.

The compiler must not make a stronger claim than the evidence class loaded into
the current view supports. This matrix is presentation guidance only; it does
not change the canonical model, analysis engine, or source adapters.

| Evidence available | Allowed UI claim | Claim not allowed in MVP1 |
|---|---|---|
| Baseline dates and `asOfDate` | `SCHEDULED PHASE`; baseline position at the reporting date | Current execution phase, actual phase entry, authorized transition |
| Planned milestone date | `NEXT BASELINE CONTROL POINT`; planned chronology | Gate passed, gate failed, next real governance action |
| Milestone `KNOWN` state from the Gantt projection | Planned date with `result not evaluated` | `COMPLETED`, `OPEN`, `AUTHORIZED`, or any execution result |
| Execution overlay record | Actual execution state, actual dates, actual effort | Readiness or governance outcome not present in the overlay |
| Dependency CPM calculation | `Dependency CPM` start/finish/float/criticality | Resource-levelled forecast or actual schedule |
| Derived schedule alert | Scoped schedule exception such as overdue, late start, or at risk | Whole-project health, absence of governance blockers |
| No execution overlay | `No execution evidence` and `Planning baseline only` | Zero completion, healthy execution, or no project issues |
| Baseline-only source intake | Planning baseline, captured provenance, derived schedule analysis | Readiness-register state, human-action state, or authority decision |
| Explicit MVP2.1 readiness profile | Attributable readiness state/result, roles, safe summaries, gate/decision evidence and provenance | Authorization, production readiness, or claims outside the captured evidence |
| Canonical role code without exposed source meaning | Short role code plus conservative project-specific wording | Organization-wide title, concrete person, or invented authority |

Baseline-only compilation still does not ingest readiness records. MVP2.1 may
ingest the bounded readiness profile only after the operator supplies the
explicit increment path; the checkbox without that path is a configuration
error. The UI must keep readiness, gate, decision, planning, execution, CPM,
and alert claims separate. No readiness evidence authorizes production work or
replaces the source project's human gate.
