# Project Management Compiler — MVP1 Architecture and Design

**Status:** Approved architecture; written design pending human review  
**Date:** 2026-09-17  
**Scope:** MVP1 compiler for the IDEAEngineering repository planning conventions  
**Target:** local, loopback-only browser application and dependency-free export pipeline

## 1. Design intent

Project Management Compiler turns project-planning evidence into one deliberate,
versioned canonical project model. The model is then consumed by management
calculations, browser views, canonical JSON, and a human-assisted CARIO workbook.

The core design rule is:

```text
Repository source
  -> repository snapshot
  -> document/planning discovery
  -> authority resolution
  -> deterministic planning extraction
  -> normalization
  -> canonical project model
  -> management analysis, views, and output adapters
```

MVP1 proves this flow against IDEAEngineering and its documented planning
conventions. It does not claim that an arbitrary GitHub repository can be
understood. A generic repository adapter can capture another repository, but the
MVP1 extraction strategy either recognizes the IDEA conventions or returns
explicit diagnostics.

The system is a modular monolith. The canonical model and management engine are
independent of the browser UI, repository technology, and CARIO format. No
database, AI/LLM parser, third-party UI framework, third-party Excel library,
CARIO API, or enterprise authentication is part of MVP1.

## 2. Evidence and source authority

The reference repository was inspected at commit
`4e5c964727e16486ea60fe4b7c3cc7daafed24e1` on 2026-09-17. Its own README
identifies DOC-07 as the planning authority. The following precedence is part
of the MVP1 extraction contract:

| Rank | Source | Owns | Does not own |
|---|---|---|---|
| 1 | `DOC-07-mvp-roadmap-and-delivery-plan.md` | Planning baseline identity, phase sequence, phase dates, milestones, capacity, reserve, dependencies, policy, scope boundary | Detailed card decomposition |
| 2 | `DOC-07-appendix-A-task-breakdown-december-2026.md` | 35 work-package IDs, work-package effort, work-package dependencies, outputs and completion checks | Product requirements or an independent schedule |
| 3 | `idea-roadmap-december-2026.html` | Visual rendition of the same baseline; useful for cross-checking | New planning values |
| 4 | `idea-technical-pilot-kanban-cario.md` | 53 delivery cards, 7 zero-effort decision/milestone cards, detailed card dates, card dependencies, execution states, WIP policy, CARIO role meanings | Authority over phase effort, source baseline, or product scope |
| 5 | `README.md` | Repository navigation and declaration that DOC-07 is the planning authority | Task data |

The current Appendix header contains a subordinate `DOC-07@0.13` reference while
the current DOC-07 control envelope is `0.14`. The resolver MUST retain the
current DOC-07 authority and emit a stale-subordinate-reference warning instead
of silently treating the two versions as equal.

The source baseline is `IE-PLAN-DEC2026-002@0.1`. The baseline records 35 work
packages, 512 planned effort hours, 88 controlled reserve hours, 600 total
weekday capacity hours, PH0–PH5, a single-coder policy, and a target Technical
Pilot date of 2026-12-31. These are source facts, not execution results.

## 3. Target runtime and restricted environment

The application targets `net10.0`. Both .NET 8 and .NET 10 are installed, but
.NET 10 provides the longer supported horizon in the discovered environment and
is already available without installation. The implementation uses only:

- ASP.NET Core shared framework for the local HTTP server;
- `System.Text.Json` for the deliberate canonical contract;
- `System.IO.Compression` and XML APIs for the narrow XLSX writer;
- .NET base-class-library file, process, date, and cryptography primitives;
- the existing Git executable only when the optional HTTPS source mode is used.

The build MUST NOT restore or install a third-party package. The automated tests
run fully offline. A controlled fixture supplies the source documents, and the
live repository is never cloned by normal tests.

The HTTP server binds to `127.0.0.1` by default. The UI is not exposed to the
LAN. MVP1 has no authentication, enterprise SSO, multi-user session model, or
remote deployment contract.

## 4. Module seams and responsibilities

The modules below are deliberately deep: callers use small interfaces while
source discovery, parsing, validation, calculation, formatting, and diagnostics
remain local to the implementation that owns those concerns.

```text
Application
  ProjectCompilationService
       |
       +--> IProjectSourceAdapter
       |       +--> LocalRepositorySourceAdapter
       |       `--> GitRepositorySourceAdapter (optional HTTPS mode)
       |
       +--> IPlanningExtractionStrategy
       |       `--> IdeaEngineeringPlanningStrategy
       |
       +--> CanonicalNormalizer
       |
       +--> ManagementAnalysisEngine
       |       +--> WbsBuilder
       |       +--> CpmAnalyzer
       |       +--> ScheduleVarianceAnalyzer
       |       +--> HealthAnalyzer
       |       `--> CapacityAndReserveAnalyzer
       |
       +--> IProjectOutputAdapter
       |       +--> CanonicalJsonOutputAdapter
       |       `--> CarioWorkbookOutputAdapter
       |
       `--> Browser API / view models
               `--> one canonical project + one calculated analysis
```

The external compilation interface is intentionally small:

```csharp
Task<CompilationResult> CompileAsync(
    SourceRequest request,
    CompilationOptions options,
    CancellationToken cancellationToken);
```

`CompilationResult` contains either a validated canonical project plus
calculated analysis and diagnostics, or a structured failure when no safe
canonical baseline can be formed. It never returns one task collection per
view. Views receive projections of the same canonical project.

### 4.1 Source adapter interface

The source seam returns a repository snapshot, never domain entities:

```csharp
public interface IProjectSourceAdapter
{
    Task<RepositorySnapshot> CaptureAsync(
        SourceRequest request,
        CancellationToken cancellationToken);
}
```

`SourceRequest` contains a required location, an optional ref, and an explicit
policy indicating whether optional Git-based HTTPS capture is allowed.
`RepositorySnapshot` contains:

- stable repository identity and display name;
- requested location and resolved ref/commit when known;
- `capturedAtUtc` as capture metadata;
- a collection of discovered source documents with repository-relative paths;
- capture diagnostics and a source-access state;
- no GitHub issue, milestone, pull-request, or other provider object in the
  canonical domain.

The local adapter is mandatory MVP1 infrastructure. It accepts a local
repository path, confirms it is readable, and reads only the files needed by the
discovery strategy. It records the local Git commit when available but remains
usable for a fixture that is not a Git checkout.

The repository is untrusted input data. The source adapters never execute
scripts, builds, hooks, binaries, macros, or commands from the captured project.
They read only allow-listed planning paths, normalize and contain every path
under the captured root, reject `..` traversal and reparse-point/symlink escape,
and enforce configurable safety limits of 2 MiB per recognized source file and
8 MiB total source content. Exceeding a limit returns a diagnostic and does not
load the unsafe document.

The optional HTTPS adapter uses the existing Git executable to create a temporary
shallow working copy when approved network access is available. Its Git command
arguments are application-owned; it never runs a project command. It does not
call the GitHub API and does not install or download a runtime, package, or tool.
If Git or network access is unavailable, the application returns an explicit
capability diagnostic and the offline fixture path remains fully usable. Optional
HTTPS capture is not an MVP1 completion prerequisite.

### 4.2 Discovery and extraction boundary

`DocumentPlanningDiscovery` maps repository-relative paths to source documents
and identifies candidate planning documents. It does not parse a `Task` or
`Project` directly.

`AuthorityResolution` identifies the controlling source documents, selected
baseline identity, recognized conventions, subordinate renditions, and
conflicts. It is the only module that decides whether a document is authoritative
for a field.

`IPlanningExtractionStrategy` is the strategy seam:

```csharp
Task<ExtractedPlanningData> ExtractAsync(
    RepositorySnapshot snapshot,
    AuthorityResolution authority,
    CancellationToken cancellationToken);
```

MVP1 supplies `IdeaEngineeringPlanningStrategy`. It uses deterministic Markdown
table/heading parsing and narrowly scoped HTML data extraction for the known
IDEAEngineering documents. It never uses an LLM, fuzzy semantic inference, or
commit-count heuristics. Unknown structure is a warning or error, not an
invented plan.

`CanonicalNormalizer` converts extracted records into neutral domain records,
validates relationships, preserves source references, and creates the canonical
JSON contract. The domain model does not know that a record came from GitHub or
from Markdown.

## 5. Canonical model

The canonical JSON uses a deliberate schema version (`schemaVersion: "1.0"`).
The public contract is not generated by serializing internal implementation
classes. The root contains:

```text
CanonicalProjectDocument
├── schemaVersion
├── project
├── sources[]
├── baseline
├── phases[]
├── workPackages[]
├── deliveryCards[]
├── milestones[]
├── dependencies[]
├── responsibilityRoles[]
├── assignments[]
├── capacity
├── reserve
├── policies
├── executionOverlay
├── provenance[]
├── warnings[]
└── analysis
```

### 5.1 Project and baseline

`Project` contains a neutral project ID, name, description, target date, and
source-independent metadata.

`ProjectSource` records source kind (`repository`), repository identity, source
location label, resolved ref/commit, `capturedAtUtc`, and discovered documents.
`capturedAtUtc` is retained for audit/context metadata and is excluded from
semantic-equivalence and canonical-content digest comparisons.

`ProjectBaseline` records the authoritative baseline ID/version, authority state,
source document references, baseline status, planning window, target date,
capacity, reserve policy, and a `validationState`. It is immutable within one
compilation result.

`ExecutionOverlay` is a separate mutable collection keyed by executable
delivery-card ID. It holds explicit manual execution evidence without changing
the baseline record or its provenance. A planning-only source therefore has an
empty overlay with actual and remaining values explicitly unknown.

The first baseline name is `IDEA DDM — Technical Pilot 2026`, as authored by the
Kanban planning document. The provisional product name “Project Management
Compiler” is not stored as the domain project identity.

### 5.2 Hierarchy without double-counting

The hierarchy is represented by IDs and parent relationships:

```text
Project
└── Phase
    ├── WorkPackage
    │   └── DeliveryCard
    └── Milestone / DecisionGate
```

The model has separate arrays for `phases`, `workPackages`, `deliveryCards`,
and `milestones`. Each record has a stable ID, a `parentId` where applicable,
display name, source references, and relevant baseline attributes.

For IDEAEngineering:

- 6 phases are detected: PH0 through PH5;
- 35 work packages are detected: P01–P07, F01–F05, C01–C05, W01–W07,
  L01–L05, and Q01–Q06;
- 53 delivery cards are retained as children, including F01-A/F01-B;
- 7 decision/milestone cards are retained as zero-duration milestone records:
  G-D0 and G-MS0 through G-MS5.

Each work package and delivery card stores both authored effort and normalized
baseline duration:

- `plannedEffortHours` is the amount of work/resources consumed;
- `plannedDurationWorkingMinutes` is elapsed working time for scheduling;
- `plannedStart` and `plannedFinish` are the authored baseline dates;
- `durationState` records whether the duration was safely normalized from the
  source calendar.

Effort and duration are not interchangeable. Work-package planned effort comes
from Appendix A and is the authoritative capacity-accounting unit. Delivery-card
effort comes from the Kanban detail. Duration preferably comes from the authored
start/finish schedule and working-calendar semantics; it is never derived from
effort unless the source explicitly establishes that equivalence. The normalizer
checks that child delivery-card effort sums to the work-package effort; any
mismatch is a warning. Parent work-package effort is never added to child effort
in capacity totals. The canonical `EffortAccounting` section states which level
is used for each calculation.

### 5.3 Dependencies

MVP1 supports Finish-to-Start (`FS`) dependencies only. A dependency records:

- subject ID and subject kind;
- predecessor ID and predecessor kind;
- dependency type;
- source authority and provenance;
- validation state;
- whether the edge is eligible for dependency CPM.

The Kanban card graph is the analysis graph. Appendix work-package dependencies
remain in the canonical model for traceability and baseline reporting but are
not added to the card graph, which would duplicate relationships. The graph
contains both delivery-card and milestone/decision nodes. A parent-child
relationship is not itself a dependency.

Missing targets, unsupported types, self-dependencies, and cycles create
diagnostics. CPM is marked `UNKNOWN` when the graph or durations are not safe to
calculate.

### 5.4 Estimates, actuals, and remaining work

Each delivery card and work package has an `Estimate` containing:

- `plannedEffortHours` and its source state (`KNOWN` for this baseline);
- `plannedDurationWorkingMinutes` and its source/normalization state;
- authored `plannedStart` and `plannedFinish` baseline dates;
- actual hours (`UNKNOWN` in MVP1 unless explicit evidence is supplied);
- remaining estimate (`UNKNOWN` in MVP1 unless explicit evidence is supplied);
- completion evidence state;
- an optional source reference.

No completion percentage is derived from commits, files, issues, elapsed time,
or repository activity.

### 5.4A Execution overlay

Each overlay record contains `workItemId`, authored `executionState`,
`actualStart`, `actualFinish`, `actualEffortHours`, `remainingEffortHours`,
`lastUpdatedAt`, and an optional note/evidence reference. Date ordering and
non-negative effort values are validated at the manual-update seam. A completed
record requires actual finish and an in-progress record requires actual start;
no missing value is guessed.

Actual effort is not actual duration. Actual duration is calculated from actual
dates and the working calendar, or from actual start through an explicit as-of
date for an in-progress record. `OVERDUE` and `AT_RISK` remain derived analysis
conditions and are never execution states.

### 5.5 Responsibility, capacity, and reserve

`ResponsibilityRole` stores source-defined logical role codes and meanings. MVP1
loads the CARIO semantics from the Kanban source:

| Code | Meaning in the source plan |
|---|---|
| A | Accountable for the result |
| R+ | Directly performs most of the work |
| R | Performs part of the work |
| C | Consulted before the result is settled |
| I | Informed of the result or change |
| O | Observes progress or risk |

Project logical roles such as `LEAD`, `PDA`, `PROC`, `DEV2`, `QLHT`, `HTKT`,
`SPEC`, and `PILOT` are preserved separately from CARIO role codes.
`Assignment` connects a work item to a logical role and CARIO role; the concrete
person/account is optional and is resolved only through configuration.

`ResourceCapacity` represents the source plan’s one-coder policy and 75 weekday
capacity days at 8 hours per day. It is planning policy data, not a global rule
for all projects. `Reserve` contains initial, consumed, and remaining amounts
with independent data states. For the current source, `initialHours` is 88 and
known; the planning documents do not provide execution evidence proving that
reserve was consumed, so `consumedHours` and `remainingHours` are null with
`NOT_RUN`/`UNKNOWN` states rather than fabricated zero values.

`Calendar` records Monday–Friday working days and 8 planned hours per day. A
future calendar adapter can replace it without changing task semantics.

### 5.6 Status, gates, evidence, and diagnostics

Core authored execution states are:

```text
NOT_STARTED, IN_PROGRESS, COMPLETED, SUSPENDED, CANCELLED
```

All imported IDEAEngineering cards begin as `NOT_STARTED`, as the source
requires. `OVERDUE` is a derived view state:

```text
currentDate > deadline
AND state is not COMPLETED or CANCELLED
```

`Evidence` and `SourceReference` retain the source repository, ref/commit,
relative file, section/table/item, extraction rule, authority rank, confidence,
and validation state. Warnings are first-class records with code, severity,
message, affected IDs, and provenance.

MVP1 does not need a separate risk register entity for imported planning data;
source diagnostics and unresolved blockers are represented as warnings and
blocker records. A future source may add typed risks without changing the
management engine interface.

## 6. Scheduling semantics

The scheduler exposes three separate concepts:

### Source baseline

The authored start, finish, phase sequence, effort, milestone date, reserve,
and single-coder policy from the source documents. This is displayed as the
baseline schedule and is never rewritten.

### Dependency analysis

The CPM module calculates dependency-network results only when supported
Finish-to-Start edges and normalized card/milestone durations are available. It
calculates earliest start/finish, latest start/finish, total float, and the
dependency critical path. CPM consumes `plannedDurationWorkingMinutes`, not raw
effort. For IDEAEngineering, date ranges and explicit AM/PM half-day markers are
normalized through the authored Monday–Friday calendar; effort is used for
capacity/load only. If the source does not make a duration safe to normalize,
CPM is unknown rather than deriving a duration from effort.

The algorithm is:

1. validate nodes and edges;
2. topologically sort the CPM graph;
3. forward-pass earliest times;
4. set project duration to the maximum earliest finish;
5. reverse-pass latest times;
6. compute float and mark zero-float nodes as dependency-critical;
7. map calculated offsets back to calculated working dates for display;
8. compare calculated dates with authored baseline dates;
9. preserve the source baseline alongside the result.

If a cycle, missing dependency target, unsupported edge, or missing duration
prevents a safe result, CPM reports `UNKNOWN` and retains the diagnostic.

### Resource and baseline schedule constraint

The source says one coder and sequential phases. That rule is represented as a
resource/baseline constraint and preserved in the Gantt and dashboard. It is not
silently passed into classic CPM, and MVP1 does not implement resource leveling
or a Critical Chain optimizer. The dashboard can therefore say that the
dependency critical path and the source sequential baseline are different
analyses.

### Variance and forecast

The analysis reports:

- authored baseline start/finish;
- calculated CPM earliest/latest start/finish;
- calculated dependency finish;
- baseline-vs-calculated variance;
- actual start variance and actual finish variance in working minutes;
- active overdue and late-start conditions derived from an explicit as-of date;
- completed-on-time, completed-late, suspended, cancelled, and conservative
  dependency-risk alerts;
- forecast finish when actual/remaining evidence is sufficient.

For the initial IDEAEngineering baseline, actual and remaining estimates are
unknown, so forecast is explicitly `UNKNOWN`. A baseline/calculated mismatch
produces a variance warning; it does not rewrite the baseline.

## 7. Management views

The browser API exposes view models generated from one `CanonicalProjectDocument`
and one `ManagementAnalysis`. No view owns a second task store.

### WBS

The WBS view builds the tree from `parentId` relationships and displays phases,
work packages, delivery cards, and milestones. It can show work-package hours
without presenting them as extra executable tasks.

### Gantt

The Gantt view includes hierarchy, source baseline start/finish, duration,
dependency summary, milestone markers, status, logical owner, and dependency
critical-path highlighting where calculated. Each delivery card is projected
with three lanes using the same canonical ID:

- `PLAN`: the immutable source baseline bar from planned start/finish;
- `ACTUAL`: actual start through actual finish, or through the explicit as-of
  date while in progress, with no fabricated future duration;
- `ALERT`: structured derived variance/risk information, not another task or
  schedule baseline.

The plan bar never moves when execution slips. Actual and forecast bars are
omitted or marked unknown when evidence is insufficient.

### Kanban

The Kanban view groups the same delivery cards by the source-compatible states:

- `Chưa bắt đầu`;
- `Đang thực hiện`;
- `Hoàn thành`;
- `Tạm ngưng`;
- `Hủy`.

It applies the source WIP policy as project policy metadata (`maxActiveItems: 1`)
for IDEAEngineering. It derives `Quá hạn` from date and state; it does not add a
manual overdue column to the canonical state machine. The overlay may change the
effective authored state for management views, but `OVERDUE` and `AT_RISK` are
never persisted as states.

### Critical path

The critical-path view clearly labels dependency CPM results and the separate
single-coder/sequential baseline constraint. It does not call every sequential
baseline item “critical” unless the dependency calculation makes it critical.

### Dashboard

Dashboard indicators use documented rules:

| Indicator | Rule |
|---|---|
| Delivery cards completed | Explicit count shown as `0 / 53` for the untouched source baseline; this is a card-completion indicator, not universal project percent complete |
| In progress | Count of explicit `IN_PROGRESS` cards |
| Not started | Count of explicit `NOT_STARTED` cards |
| Suspended/blocked | Count of suspended cards plus blocking diagnostics |
| Cancelled | Count of explicit `CANCELLED` cards |
| Overdue | Derived from as-of date, deadline, and state |
| Late to start | Derived from a late as-of date or actual start after baseline start |
| Completed late | Count of completed cards whose actual finish exceeds baseline finish |
| At risk | Conservative downstream dependency-risk alerts only |
| Planned effort | Sum of authoritative work-package effort, 512 h |
| Reserve | Initial reserve is 88 h known; consumed and remaining are `NOT-RUN`/`UNKNOWN` because source planning does not prove execution consumption |
| Actual effort | `UNKNOWN` until actual evidence exists |
| Forecast | `UNKNOWN` until actual/remaining evidence exists |
| Schedule health | `UNKNOWN` when actual evidence is insufficient; otherwise calculated from documented variance rules |
| Critical path | Dependency CPM result or `UNKNOWN` with its diagnostic |
| Upcoming milestones | Milestones whose dates are after the as-of date and not completed |
| Major blockers | High-severity diagnostics and explicit blocker records |

There is no unsubstantiated red/yellow/green score.

## 8. CARIO mapping and workbook output

MVP1 produces a human-assisted fill file, not a native CARIO import contract.
There is no evidence in the source that CARIO accepts this workbook directly, so
the UI and documentation must not claim direct import compatibility.

The mapping configuration has the conceptual shape:

```text
logicalRoleCode -> concreteIdentity / department / team
LEAD            -> optional configured person/account
QLHT            -> unresolved when no mapping is supplied
```

Project-management domain logic never contains an employee ID, department name,
team name, or priority value. Missing values remain blank in the workbook and
produce warnings.

The workbook is `<ProjectName>_CARIO.xlsx` with these sheets:

1. `01_TASKS` — 53 delivery-card rows plus 7 milestone/decision rows, with work
   item type, task ID, phase, work package, Vietnamese task name, planned dates,
   blank unresolved CARIO fields, initial state, planned effort where applicable,
   baseline/analysis note, and source reference.
2. `02_ASSIGNMENTS` — one row per source responsibility assignment with task ID,
   logical role, unresolved or configured concrete identity, CARIO role, and
   mapping status.
3. `03_CHILDREN_MILESTONES` — phase/work-package parent, child card or
   milestone, relationship type, and provenance.
4. `04_DEPENDENCIES` — subject, predecessor, dependency type, validation state,
   and provenance.
5. `05_PROJECT_INFO` — project, baseline, authority, target date, capacity,
   reserve, WIP policy, and status/forecast data states.
6. `06_IMPORT_WARNINGS` — warning ID, severity, code, message, affected item,
   and source reference.

The XLSX adapter uses only `System.IO.Compression` and Open XML worksheet parts
needed by these sheets: `[Content_Types].xml`, `_rels/.rels`,
`xl/workbook.xml`, `xl/_rels/workbook.xml.rels`, six
`xl/worksheets/sheet*.xml` parts, and simple styles/inline strings as selected by
the writer. It is not a general Excel library and does not require Microsoft
Excel or Office COM. Tests open the ZIP structurally, resolve relationships,
parse worksheet XML, verify names, dates, numbers, and Vietnamese Unicode.

## 9. Canonical JSON persistence and reopen

The canonical JSON is a deliberate persisted snapshot. The UI supports:

- analyzing a source into a canonical snapshot;
- downloading the snapshot as `<ProjectName>_project.json`;
- loading a previously generated canonical JSON snapshot into the same view and
  export pipeline without running extraction again, when the schema version is
  supported.

Schema `1.0` remains compatible with the amendment as an additive change:
new snapshots emit `executionOverlay.records`; readers treat the field as an
empty overlay when it is absent from an older 1.0 snapshot. A present overlay
is validated strictly. Reopen preserves both baseline and overlay, while alerts,
variance, and other calculated analysis are recomputed from an explicit as-of
date.

Reopen validates the schema version, required fields, global/collection ID
uniqueness, parent and phase ownership, work-package/card hierarchy, assignment
targets, baseline shape, and canonical field types. These structural invariant
failures cause reopen to fail. A source-evidence relationship may remain invalid
only when it is explicitly represented as such: for example, a dependency with
an unresolved predecessor is retained with `validationState=INVALID_SOURCE_EVIDENCE`
and `analysisEligible=false`; its subject must still resolve structurally. An
unmarked missing reference fails reopen. The reopened snapshot retains its
original source provenance and baseline; it does not masquerade as a newly
captured source.

## 10. Browser application

The local UI uses plain HTML, CSS, and browser JavaScript served from ASP.NET
Core static files. It uses generic source language:

```text
New Project -> Source -> Analyze -> Review extraction -> Confirm baseline
             -> Overview / WBS / Gantt / Kanban / Critical Path / Dashboard
             -> Export
```

The initial screen allows a local repository path and an optional HTTPS Git URL.
The review area shows detected phases, work packages, delivery cards,
milestones, dependencies, authority choice, and warnings before export. It does
not silently turn warnings into approvals; the baseline can be confirmed only
after the user sees the diagnostics.

No UI code performs CPM, hierarchy construction, source parsing, or CARIO
mapping. It renders application view models returned by the compilation service.
The UI also provides a manual execution-update form for a selected delivery
card. It submits overlay changes through the application seam and refreshes the
shared analysis/Gantt/Kanban/dashboard projections.

## 11. Error and warning model

The compilation result has a stable diagnostic contract with severity:

```text
INFO, WARNING, ERROR, BLOCKED
```

Representative codes are:

| Code | Meaning | Safe behavior |
|---|---|---|
| `NO_PLANNING_AUTHORITY` | No recognized authoritative planning source | Do not create a canonical baseline |
| `UNSUPPORTED_PLANNING_CONVENTION` | Repository content is readable but not recognized | Return diagnostics; do not infer tasks |
| `STALE_SUBORDINATE_REFERENCE` | Appendix/rendition names a stale authority version | Use higher-ranked current authority and surface variance |
| `CONFLICTING_BASELINE` | Sources disagree on an authoritative field | Keep the authority-ranked value and mark conflict for review |
| `AMBIGUOUS_DATE` | A date cannot be resolved safely | Keep date unknown and warn |
| `MISSING_DEPENDENCY_TARGET` | A dependency names no known item | Exclude edge from CPM and warn |
| `UNSUPPORTED_DEPENDENCY_TYPE` | Dependency is not Finish-to-Start | Preserve source edge, mark CPM ineligible, warn |
| `DEPENDENCY_CYCLE` | CPM graph contains a cycle | Return CPM unknown; do not mutate baseline |
| `EFFORT_RECONCILIATION` | Child card hours differ from parent work-package hours | Preserve both levels and warn about accounting variance |
| `MISSING_CARIO_MAPPING` | Logical role has no configured concrete identity | Leave field blank and add workbook warning |
| `MISSING_ORGANIZATION_MAPPING` | Department/team is not authored or configured | Leave field blank and warn |
| `UNKNOWN_ACTUALS` | Source contains planning only | Show actual/forecast as unknown |
| `INVALID_EXECUTION_UPDATE` | Manual actual/state evidence is internally inconsistent | Reject update; keep baseline and prior overlay |
| `START_DELAY` | Unstarted work is past planned start | Derive alert; do not change execution state |
| `OVERDUE` | In-progress work is past planned finish without actual finish | Derive alert; do not change execution state |
| `COMPLETED_LATE` | Actual finish exceeds planned finish | Derive variance/alert |
| `AT_RISK` | Valid late predecessor exposes an unstarted successor | Derive conservative downstream risk only |
| `SOURCE_CAPTURE_FAILED` | Local path, Git, or optional network capture failed | Stop source capture with actionable diagnostic |

Errors that invalidate the canonical baseline prevent normal exports. Warnings
permit review/export when the model is structurally safe, but are always visible
in JSON, UI, and the warning worksheet.

## 12. Test strategy

The test suite is dependency-free and fully offline. A controlled fixture under
`tests/fixtures/ideaengineering/` contains only the minimum README, DOC-07,
Appendix A, Kanban, and Gantt fragments needed to assert the extraction contract.
It is pinned to the inspected source commit in fixture metadata. Normal tests do
not clone or contact the live repository.

Tests cross the deepest useful interfaces:

| Area | Required evidence |
|---|---|
| Source capture | Local repository path produces a stable snapshot; missing path returns a structured failure |
| Authority | DOC-07 wins over Appendix/Gantt/Kanban; stale subordinate version is warned |
| Extraction | Project identity, baseline, PH0–PH5, 35 work packages, 53 delivery cards, 7 milestones |
| Hierarchy | F01 contains F01-A and F01-B; milestones/gates retain their phase/work-package relationship |
| Effort | Parent work-package hours do not double-count child-card hours; reconciliation is tested |
| Dependencies | Edges and missing targets are preserved; unsupported types are not fed to CPM |
| Graph safety | Cycles are detected and CPM becomes unknown |
| CPM | Earliest/latest times, float, and dependency critical path consume normalized duration rather than effort; baseline dates remain unchanged |
| Baseline | Baseline dates remain unchanged when calculated schedule differs |
| Status | Initial states, completion counts, and derived overdue semantics are correct |
| Execution overlay | Manual updates validate and persist actual state/date/effort separately from the baseline |
| Variance and alerts | Fixed as-of tests cover working-calendar variance, overdue, late completion, suspension, cancellation, and dependency risk |
| Policy | WIP=1 is imported as project policy, not a global constant |
| CARIO | Role meanings and logical role assignments are preserved; missing concrete mappings warn |
| JSON | Schema version, deliberate field names, provenance, and deterministic equivalent reruns |
| XLSX | Required ZIP/XML parts and relationships resolve; all six sheets, headers, dates, numbers, UTF-8 Vietnamese text, and warnings are exported |
| Reopen | Generated canonical JSON reopens without source extraction and produces equivalent views/exports |
| Regression | Expected fixture summary and canonical snapshot digest remain stable unless the fixture contract changes |

The test runner uses fixed `asOfDate` values for overdue and milestone tests so
that wall-clock time cannot make the suite nondeterministic.

## 13. MVP1 required, deferred, and future model concepts

| Concept | MVP1 treatment |
|---|---|
| Project, source, baseline | Required and exported |
| Phase, work package, delivery card, milestone/decision gate | Required and hierarchical |
| Finish-to-Start dependency | Required; cycle detection and CPM included |
| Estimate, actual, remaining estimate | Planned required; actual/remaining explicit unknown unless supplied |
| Logical responsibility role and configurable CARIO mapping | Required |
| Capacity, calendar, reserve/buffer | Required at source-policy level |
| WBS, Gantt, Kanban, critical path, dashboard | Required views from one model |
| Evidence, source reference, import warning | Required |
| Risk and blocker records | Minimal diagnostic/blocker representation; full risk register deferred |
| Baseline/actual/forecast comparison | Baseline and manual execution overlay required; forecast remains unknown unless evidence is sufficient |
| Manual execution overlay and three-lane Gantt | Required; actual updates are local, explicit, and recalculable |
| Resource leveling optimizer / Critical Chain | Deferred |
| Full dependency type set | Deferred; MVP1 supports FS only |
| Multi-source merge | Future extension; model keeps source and baseline provenance ready |
| Jira, GitLab, Azure DevOps, local non-repository documents | Future adapters |
| DOCX/PDF/AI extraction | Future strategies; explicitly excluded from MVP1 |
| CARIO API or browser automation | Explicitly excluded |
| Authentication, SaaS, multi-tenant operation | Explicitly excluded |

## 14. Design self-review result

The design has no implementation placeholders. The following potentially
ambiguous terms are resolved explicitly:

- “Repository support” means local path first, optional Git HTTPS capture second;
  it does not mean arbitrary-repository understanding.
- “Task” in the canonical contract means a delivery card; a work package is a
  separate planning parent and a milestone is a zero-duration gate.
- “Critical path” means dependency CPM; single-coder and phase sequencing are
  separately labeled constraints.
- “WBS” means the Phase → WorkPackage → DeliveryCard hierarchy; “Dependency
  Network” means the validated graph used for CPM; “CPM Critical Path” means
  the dependency-derived path, not a resource-constrained schedule.
- “Resource Constraint” means a source capacity or single-coder scheduling
  policy; “Capacity” means available planned effort hours; “Reserve” means the
  separately authored contingency amount.
- “Baseline” means authored source dates and effort; “Actual” means explicit
  execution evidence; “Forecast” means a calculated future result; “Variance”
  means the comparison between baseline and calculated/forecast values.
- “Forecast” is not baseline and is `UNKNOWN` without actual/remaining evidence.
- “Effort” is work consumed; “duration” is elapsed working time. CPM uses normalized duration and capacity uses effort.
- “Execution overlay” is mutable actual evidence keyed by canonical work-item ID;
  it never mutates source baseline.
- “Alert” is derived management information, not an authored execution state;
  `OVERDUE` and `AT_RISK` are never persisted as states.
- “As-of date” is explicit analysis input, not implicit wall-clock state.
- “CARIO export” means a human-assisted workbook, not a claimed native import.
- “Project name” in the source model is the source project identity, not the
  provisional compiler product name.

The scope is one coherent vertical product slice: source-to-canonical analysis,
management views, and two outputs. Future adapters and broad enterprise
capabilities are extension points or explicit non-goals, not hidden work.
