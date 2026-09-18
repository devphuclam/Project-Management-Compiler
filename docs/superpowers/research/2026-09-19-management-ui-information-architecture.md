# Management UI Information Architecture Research

Date: 2026-09-19  
Scope: research-only input for the Project-Management-Compiler management UI de-clutter pass.

This note records primary-source findings, the repository state observed during this task, and design inferences for a later presentation-layer change. It does not change the compiler's canonical model, analysis semantics, tests, or source adapters.

## 1. Repository state and evidence boundary

### HEADs verified

| Repository | Local observation | Main reference verified | Worktree note |
|---|---|---|---|
| Project-Management-Compiler | branch `main`, HEAD `81d93694f80692072a13552d73e468e1c70d5391` (`docs: close MVP1 documentation truth pass`) | local `origin/main` and remote `refs/heads/main` both resolve to `81d93694f80692072a13552d73e468e1c70d5391` | local uncommitted changes were present in `scripts/verify-web.ps1`, `src/ProjectManagementCompiler/wwwroot/app.js`, and `src/ProjectManagementCompiler/wwwroot/styles.css` |
| IDEAEngineering | checkout branch `codex/q15-client-qualification`, HEAD `3dd955249cfaaec5c114b92dc4155f7e8d6e3ad1` (`docs: pin refreshed diagram evidence baseline`) | local `origin/main` and remote `refs/heads/main` both resolve to `3dd955249cfaaec5c114b92dc4155f7e8d6e3ad1` | checkout is dirty outside this research task: `.gitattributes`, `RUN_VERIFIERS.bat`, and generated `qualification/q15/**/TestResults/` paths |

The compiler and source HEAD values above are source-control facts observed with `git rev-parse`, `git show`, and `git ls-remote`. The dirty worktrees were not modified.

### Real-source inventory observed

The inventory below comes from the current IDEAEngineering checkout, not only the compiler's `tests/fixtures/ideaengineering-real-shaped` fixture.

- `DOC-07` describes six delivery phases, `PH0` through `PH5`, and six zero-duration review milestones, `MS0` through `MS5`. `D0` is a decision checkpoint before `MS0`; the current roadmap makes `MS0 / PG4` the implementation-readiness gate. See the [current roadmap](https://github.com/devphuclam/IDEAEngineering/blob/3dd955249cfaaec5c114b92dc4155f7e8d6e3ad1/docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md).
- The current plan states 35 work packages, 512 planned work hours, 88 controlled reserve hours, and 600 weekday capacity hours. The appendix records 53 delivery cards and 7 decision/milestone cards, for 60 Kanban entries; reserve is not represented as task cards. See the [roadmap appendix](https://github.com/devphuclam/IDEAEngineering/blob/3dd955249cfaaec5c114b92dc4155f7e8d6e3ad1/docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md) and [Kanban/CARIO rendition](https://github.com/devphuclam/IDEAEngineering/blob/3dd955249cfaaec5c114b92dc4155f7e8d6e3ad1/docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md).
- The roadmap explicitly assumes one project user as the only coder and says to keep at most one primary implementation task in progress. This is a planning/resource constraint, not a replacement for dependency analysis. See the [roadmap capacity and execution sections](https://github.com/devphuclam/IDEAEngineering/blob/3dd955249cfaaec5c114b92dc4155f7e8d6e3ad1/docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md).
- The PH0 readiness package is substantially richer than the schedule: `README.md`, `spec.md`, `plan.md`, `baseline-manifest.md`, `readiness-register.md`, `trace-matrix.md`, `canonical-scenario.md`, environment/test-data/recovery plans, contracts, checklists, quickstart, and analysis findings. Its current register distinguishes P01 `IN-PROGRESS`, P02–P07 `NOT-RUN`, PG4 execution `NOT-RUN`, and PG4 outcome `NOT-APPLICABLE` until an attributable decision. See the [PH0 package README](https://github.com/devphuclam/IDEAEngineering/blob/3dd955249cfaaec5c114b92dc4155f7e8d6e3ad1/specs/004-technical-pilot-readiness/README.md) and [readiness register](https://github.com/devphuclam/IDEAEngineering/blob/3dd955249cfaaec5c114b92dc4155f7e8d6e3ad1/specs/004-technical-pilot-readiness/readiness-register.md).

### Current compiler presentation inventory

This is an inventory of what the current browser renderer exposes, not a recommendation that every field remain on the first screen. It was observed in `src/ProjectManagementCompiler/wwwroot/app.js` at the compiler HEAD plus the local daily-scale changes.

| Surface | Currently visible or directly rendered |
|---|---|
| Summary/control center | project name; baseline version; reporting `asOfDate`; health/attention count; delivery progress; execution evidence wording; next milestone; attention queue; work-item count; recorded completion; schedule-alert and late-start counts; planning window; baseline immutability statement |
| Dashboard | planned effort; capacity; reserve; execution-evidence state; baseline finish; dependency-only CPM finish; forecast finish; analysis summaries; attention queue |
| Gantt row | typed ID; name; kind; phase/work-package context; execution state; owner/role summary; alert/CPM signals; PLAN lane; ACTUAL lane when evidence exists; ALERT markers; milestone marker |
| Gantt controls | overview/plan/execution/risks/dependencies/critical-path presets; attention, expand/collapse, zoom, fit, critical-path, dependency toggles; phase/execution/advanced filters |
| Inspector | identity; plan/source; source evidence; actual evidence; as-of and variance analysis; logical roles; dependency edges; alerts and reason IDs; dependency CPM dates/float/critical flag; execution recording action |
| WBS/dependency/CPM views | hierarchy tree, dependency network and CPM projections remain separate question-oriented views |

The renderer already has a browser-side `ManagementPresentationRow` projection helper. That is compatible with keeping presentation selection separate from the domain model; the research conclusion below is to narrow and consistently apply that projection rather than add a second canonical model.

## 2. Primary-source findings

### PMI: WBS is hierarchical decomposition, not a flat task dump

PMI's Lexicon defines a work breakdown structure as a “hierarchical decomposition of the total scope of work” needed to accomplish project objectives and create deliverables. It also defines a work package as the lowest WBS level where cost, effort, duration, and resources are estimated and managed. See the [PMI Lexicon of Project Management Terms](https://www.pmi.org/-/media/pmi/documents/registered/pdf/pmbok-standards/pmi-lexicon-pm-terms.pdf?rev=447328d841c249af985d14177ddd5f95).

PMI's WBS practice-standard overview says the WBS organizes total project scope and supports tracking schedule, budget, risk, and performance. Its published guidance also states that each descending level represents increasingly detailed work and that decomposition should continue only until the work is sufficiently defined for execution, monitoring, and control. See [PMI's Work Breakdown Structure Practice Standard](https://www.pmi.org/standards/work-breakdown-structures-third-edition) and the [PMI WBS guidance article](https://www.pmi.org/learning/library/practice-standard-work-breakdown-structures-8063).

**Research implication:** Project, phase, work package, and executable card can remain a real hierarchy without receiving equal visual weight. A summary surface should aggregate upward; a structure surface should expose the full tree; an item inspector should expose the selected node's detail. This is a design inference from the hierarchy/decomposition facts, not a change to the compiler's domain semantics.

### PRINCE2 / PeopleCert: manage by exception and stages focus reporting on decisions

PeopleCert describes manage by exception as delegated authority within agreed tolerances, so senior authorities are not pulled into routine work and can focus on issues or opportunities when the boundary is reached. See [PeopleCert's PRINCE2 community explanation](https://community.peoplecert.org/public/clubs/prince2/blogs/success-and-failure-what-does-good-look-like) and the [PeopleCert/PRINCE2 case study on management by exception](https://www.peoplecert.org/-/media/folders-reorganized/pdfs/prince2-p3-m3-maturity-model/cs-unops.pdf).

PeopleCert's PRINCE2 material also describes management stages as manageable sections with review and a decision about whether to continue; its current explanation of planning horizons says the current stage is planned in more detail, while later stages can remain higher-level until the next stage boundary. See [PeopleCert's stage/planning-horizon article](https://community.peoplecert.org/public/clubs/prince2/blogs/planning-horizon-in-project-management-2026-05-28). A compact PRINCE2 principle summary also states that a project running within tolerance does not need project-board intervention and that each stage should be planned, monitored, and controlled before the next continuation decision. See [PRINCE2's principles overview](https://www.prince2.com/eur/blog/the-7-principles-themes-and-processes-of-prince2).

**Research implication:** The default management surface should lead with current phase, next gate/milestone, exceptions, and decisions requiring action. Healthy work can remain available but quiet. The compiler should reuse its existing alert and gate/readiness states; “manage by exception” is an information-priority principle here, not authorization to invent tolerances or new alert semantics.

### Microsoft Project: baseline, current schedule, actuals, and variance are different meanings

Microsoft Support states that after a baseline is saved, it can be compared with scheduled and actual data to understand how the project is tracking against its initial goals. The Variance table places scheduled and baseline start/finish information side by side so the plan can be compared with how the project is progressing. See [Create or update a baseline or an interim plan in Project](https://support.microsoft.com/et-EE/project/create-or-update-a-baseline-or-an-interim-plan-in-project-desktop).

Microsoft's progress guidance distinguishes current work, baseline work, variance, actual work, and remaining work. For an in-progress task, current work includes actual plus remaining work; for a not-started task, current work is a projection that is often the same as the baseline. See [Review the progress of your schedule](https://support.microsoft.com/en-us/project/review-the-progress-of-your-schedule).

**Research implication:** UI labels should explain semantics, not present a pile of similarly named date columns. PLAN/baseline, ACTUAL/evidence, calculated dependency analysis, and alert/variance should have separate visual channels and explanatory labels. An unknown actual is “no execution evidence,” not evidence that the baseline itself is unknown.

### Oracle Primavera: Data Date is a controlled status/scheduling boundary

Oracle's P6 documentation defines the Data Date as the date used as the starting point to calculate the schedule. Oracle's project reference says project status is up to date as of the data date, and that the data date changes when actuals are applied. See [P6 Dates tab documentation](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/dates_tab_-_project_details.htm), [Oracle P6 project API reference](https://docs.oracle.com/cd/G48897_01/English/Integration_Documentation/p6_eppm_api_reference/com/primavera/integration/client/bo/object/Project.html), and [Applying actuals to a project](https://docs.oracle.com/cd/G48897_01/English/User_Guides/p6_eppm_user/7995.htm).

Oracle's scheduling guide further says that each project can have its own data date when projects are scheduled, and that the data date is distinct from an optional forecast start. See [Schedule a project](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/schedule_a_project.htm). Oracle also describes progress spotlight as the interval between an earlier data date and a new data date, which is useful evidence that the boundary is not the same thing as an actual finish. See [Highlight activities for updating](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/highlight_activities_for_updating.htm).

**Research implication:** The compiler's explicit `asOfDate` should remain prominent as the reporting boundary. It must not be rendered as “actual finish,” and the UI should not silently substitute the workstation's wall-clock date. Dependency CPM, actual evidence, and forecast should remain separate states.

### NN/g: progressive disclosure preserves power without making the first screen a data dump

Nielsen Norman Group defines progressive disclosure as deferring advanced or rarely used features to a secondary screen so applications remain easier to learn and less error-prone. Its guidance says to show only a few important options initially, disclose specialized options on request, and make the path to the secondary level obvious. See [NN/g: Progressive Disclosure](https://www.nngroup.com/articles/progressive-disclosure/).

NN/g also warns that the initial list must still contain information users frequently need, and that the mechanism for moving to the secondary level must be simple and clearly labelled. Its application-design guidance connects this to predictable placement, clear hierarchy, and avoiding overload. See [NN/g's application-design guidance](https://www.nngroup.com/topic/ui-design/?page=2) and [NN/g's progressive-disclosure video summary](https://www.nngroup.com/videos/progressive-disclosure/).

**Research implication:** Level 1 should answer project-state questions; Level 2 should answer control/schedule/exception questions; Level 3 should expose the selected item's full roles, dependencies, variance, provenance, completion rule, and source evidence. Hidden detail must remain reachable through an explicit row selection, inspector, structure mode, or question-oriented view within one or two interactions.

## 3. Evidence-to-IA mapping

| Management question | First-level answer | Deferred detail |
|---|---|---|
| What is the state of the project? | project identity, explicit as-of date, current phase, next gate, attention summary, planning-only/evidence state | full counts, role matrix, source catalogue, all work items |
| Where is the schedule problem? | exception list and PLAN/ACTUAL/ALERT timeline channels | dependency IDs, CPM rows, variance arithmetic, source evidence |
| How is scope decomposed? | phase summary and a quiet default hierarchy | full Project → Phase → WorkPackage → DeliveryCard tree in Structure/WBS mode |
| What execution state is evidenced? | recorded evidence / no execution evidence; actual lane where present | actual effort, remaining effort, update timestamp, raw overlay record |
| Why is an item flagged? | concise alert label and severity | alert reason IDs, dependency edges, rule/provenance, authority record |
| Where did the information come from? | source indicator in selected-item detail | relative file, section/table/item, authority, confidence, validation, extraction rule |

The “Level 1 / Level 2 / Level 3” split is a presentation inference grounded in the sources above and the current renderer inventory. It does not authorize removing canonical Project, Phase, WorkPackage, DeliveryCard, Milestone, dependency, PLAN, ACTUAL, ALERT, CPM, CARIO, provenance, or `asOfDate` data.

## 4. Design guardrails for the later UI pass

These are proposed presentation rules, not source facts:

1. Keep the canonical hierarchy and typed identity authoritative. Use a narrow presentation projection to choose what a surface exposes.
2. Make Overview/management summary answer “what needs management attention?” before showing raw inventory counts.
3. Make Gantt answer “when is work planned/occurring and where is deviation?” Default to executable schedule rows; offer an explicit Structure/WBS mode for the complete hierarchy.
4. Use concise state language such as “No execution evidence,” “Not calculated,” “Unassigned,” or `—` instead of repeating large `UNKNOWN` labels where the meaning is already clear.
5. Keep PLAN immutable, ACTUAL evidence-based, dependency CPM clearly labelled as dependency analysis, and alerts visibly derived rather than blended into one status badge.
6. Show a primary responsibility summary on a normal row; keep the complete CARIO matrix in the inspector. Do not fabricate role definitions or personal actions that the source does not assign.
7. Normalize redundant `[PHx][ID]` prefixes for display only. Preserve the exact source title in the inspector/source evidence.
8. Treat milestones and decision gates as sparse event markers with date/state/impact at the summary level; leave detailed gate evidence for the inspector.

## 5. Sources

- PMI, [Work Breakdown Structure Practice Standard, Third Edition](https://www.pmi.org/standards/work-breakdown-structures-third-edition).
- PMI, [Lexicon of Project Management Terms, Version 5.0](https://www.pmi.org/-/media/pmi/documents/registered/pdf/pmbok-standards/pmi-lexicon-pm-terms.pdf?rev=447328d841c249af985d14177ddd5f95).
- PMI, [Practice Standard for Work Breakdown Structures guidance](https://www.pmi.org/learning/library/practice-standard-work-breakdown-structures-8063).
- PeopleCert, [Manage by exception](https://community.peoplecert.org/public/clubs/prince2/blogs/success-and-failure-what-does-good-look-like).
- PeopleCert, [Management by exception case study](https://www.peoplecert.org/-/media/folders-reorganized/pdfs/prince2-p3-m3-maturity-model/cs-unops.pdf).
- PeopleCert, [Planning horizon and management stages](https://community.peoplecert.org/public/clubs/prince2/blogs/planning-horizon-in-project-management-2026-05-28).
- PRINCE2, [Seven principles overview](https://www.prince2.com/eur/blog/the-7-principles-themes-and-processes-of-prince2).
- Microsoft Support, [Baseline/interim plan comparison](https://support.microsoft.com/et-EE/project/create-or-update-a-baseline-or-an-interim-plan-in-project-desktop).
- Microsoft Support, [Review schedule progress](https://support.microsoft.com/en-us/project/review-the-progress-of-your-schedule).
- Oracle, [P6 Dates tab / Data Date](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/dates_tab_-_project_details.htm).
- Oracle, [P6 project Data Date API reference](https://docs.oracle.com/cd/G48897_01/English/Integration_Documentation/p6_eppm_api_reference/com/primavera/integration/client/bo/object/Project.html).
- Oracle, [Schedule a project](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/schedule_a_project.htm).
- Oracle, [Apply actuals](https://docs.oracle.com/cd/G48897_01/English/User_Guides/p6_eppm_user/7995.htm).
- Oracle, [Progress spotlight between data dates](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/highlight_activities_for_updating.htm).
- Nielsen Norman Group, [Progressive Disclosure](https://www.nngroup.com/articles/progressive-disclosure/).
- Nielsen Norman Group, [Application/UI hierarchy guidance](https://www.nngroup.com/topic/ui-design/?page=2).
- Nielsen Norman Group, [Progressive Disclosure video summary](https://www.nngroup.com/videos/progressive-disclosure/).

