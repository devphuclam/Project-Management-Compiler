# Feature Specification: Project Management Compiler MVP1

**Feature Branch**: `001-mvp1-project-management-compiler`  
**Created**: 2026-09-17  
**Status**: Draft — written specification pending human review  
**Input**: Approved architecture and MVP1 scope for a local project-management intelligence tool

## User problem

Engineering planning evidence is distributed across repository documents,
roadmaps, Gantt renditions, and CARIO planning notes. A person must manually
reconcile hierarchy, dates, effort, dependencies, responsibility roles, and
warnings before the plan can be reviewed or entered into another system. This
creates a risk of flattening parent work packages into duplicate tasks, treating
derived views as authoritative, inventing missing organization mappings, or
mistaking repository activity for project completion.

MVP1 provides one deterministic path from the IDEAEngineering repository planning
conventions to a canonical project snapshot, management views, and a human-
assisted CARIO workbook.

## Actors

- **Project user** — supplies a local repository path, reviews extracted authority
  and warnings, opens management views, and exports files.
- **Source adapter** — captures a local repository snapshot and optionally uses
  existing Git for an HTTPS repository location.
- **IDEAEngineering extraction strategy** — recognizes the documented planning
  conventions; it is deterministic and not an AI agent.
- **Management reviewer** — reads WBS, Gantt, Kanban, critical-path, health,
  provenance, and warning information.
- **CARIO operator** — uses the workbook as a human fill file; the MVP does not
  claim native CARIO import compatibility.

## User scenarios and testing

### User Story 1 — Analyze an approved local source (Priority: P1)

As a project user, I want to select a local IDEAEngineering repository so that
the tool can discover its planning documents, identify the authority, and show a
reviewable canonical baseline.

**Why this priority**: This is the source-to-model value chain. Without it, the
views and exports would have no trustworthy input.

**Independent test**: Point the compiler at the offline controlled fixture and
verify the detected source authority, baseline, six phases, 35 work packages,
53 delivery cards, 7 decision/milestone cards, dependencies, policy, effort,
and provenance.

**Acceptance scenarios**:

1. **Given** a readable local repository containing the recognized IDEA
   planning documents, **when** the user selects `Analyze`, **then** the tool
   returns a canonical baseline and lists detected authority, counts, and
   warnings before export.
2. **Given** a repository with no recognized planning authority, **when** the
   user selects `Analyze`, **then** the tool returns `NO_PLANNING_AUTHORITY` or
   `UNSUPPORTED_PLANNING_CONVENTION` and does not invent a project plan.
3. **Given** a repository with a stale Appendix authority reference, **when**
   extraction completes, **then** current DOC-07 remains authoritative and the
   stale subordinate reference is visible as a warning.
4. **Given** the same fixture and extraction options twice, **when** both runs
   complete, **then** their canonical data is deterministically equivalent apart
   from explicitly non-contractual capture timestamps.

### User Story 2 — Review one canonical project through management views (Priority: P1)

As a management reviewer, I want WBS, Gantt, Kanban, critical-path, and dashboard
views generated from the same project data so that a status or relationship is
not represented differently in separate task stores.

**Why this priority**: The product must demonstrate project-management theory,
not merely produce another task list.

**Independent test**: Load the fixture’s canonical snapshot and verify that each
view contains the same IDs, hierarchy, dates, statuses, and diagnostic states.

**Acceptance scenarios**:

1. **Given** the canonical fixture, **when** the user opens WBS, **then** the
   tree shows Phase → WorkPackage → DeliveryCard and preserves milestone/gate
   relationships.
2. **Given** baseline dates and dependencies, **when** the user opens Gantt,
   **then** the view displays planned baseline bars, dependency information,
   milestones, statuses, and dependency-critical highlighting when CPM is valid.
3. **Given** the imported CARIO-compatible state policy, **when** the user
   opens Kanban, **then** cards are grouped as `Chưa bắt đầu`, `Đang thực hiện`,
   `Hoàn thành`, `Tạm ngưng`, or `Hủy`, with WIP=1 shown as project policy.
4. **Given** a fixed as-of date after a card deadline, **when** the card is not
   completed or cancelled, **then** the UI derives `Quá hạn` without changing
   the authored execution state.
5. **Given** planning-only input, **when** the user opens Dashboard, **then**
   actual work, remaining estimate, completion evidence, and forecast are shown
   as unknown rather than inferred from commits or elapsed time.
6. **Given** a dependency network and durations, **when** the user opens
   Critical Path, **then** dependency CPM is labeled separately from the
   single-coder/sequential baseline constraint.

### User Story 3 — Export an explainable CARIO fill file (Priority: P1)

As a CARIO operator, I want a readable workbook with tasks, assignments,
children/milestones, dependencies, project information, and warnings so that I
can manually complete the company’s CARIO Create Task fields without fabricated
people or organization values.

**Why this priority**: CARIO is the first required enterprise-compatible output,
but the source does not prove a native import contract.

**Independent test**: Generate the fixture workbook, inspect its six worksheet
parts and headers, and verify that unresolved mappings are blank in task rows and
explicit in `06_IMPORT_WARNINGS`.

**Acceptance scenarios**:

1. **Given** a valid canonical snapshot, **when** the user selects CARIO export,
   **then** `<ProjectName>_CARIO.xlsx` contains exactly the six documented
   worksheets.
2. **Given** `LEAD` and `QLHT` logical roles without identity configuration,
   **when** assignments are exported, **then** no employee is fabricated and
   each unresolved mapping is visible in the assignment and warning sheets.
3. **Given** a missing department or board priority mapping, **when** tasks are
   exported, **then** the required cell remains blank and the reason appears in
   the warning sheet.
4. **Given** a work package with child delivery cards, **when** the workbook is
   opened, **then** parent/child and milestone relationships are preserved and
   parent effort is not counted again as a child task.

### User Story 4 — Persist and reopen a canonical snapshot (Priority: P2)

As a reviewer, I want to save and reopen the deliberate canonical JSON so that I
can inspect or export a previously analyzed baseline without live source access.

**Independent test**: Export the fixture JSON, load it through the reopen path,
and compare view summaries and workbook content with the original compilation.

**Acceptance scenarios**:

1. **Given** a supported `schemaVersion`, **when** the user opens the JSON,
   **then** the tool validates and displays the snapshot without re-running
   repository extraction.
2. **Given** an unsupported schema version or invalid relationship, **when** the
   user opens the JSON, **then** the tool rejects it with a structured error.
3. **Given** a reopened snapshot, **when** the user exports it, **then** its
   original provenance and baseline values remain intact.

### User Story 5 — Diagnose unsafe or unsupported input (Priority: P2)

As a project user, I want missing, contradictory, unsupported, and unresolved
source information to be explicit so that I can decide what requires confirmation
before treating the baseline as authoritative.

**Independent test**: Run fixture variants with missing authority, missing
dependency targets, a dependency cycle, conflicting dates, and empty mapping
configuration; assert structured diagnostics and safe behavior.

**Acceptance scenarios**:

1. **Given** a missing dependency target, **when** extraction completes, **then**
   the dependency is retained as invalid evidence, excluded from CPM, and
   reported as a warning.
2. **Given** a dependency cycle, **when** analysis runs, **then** CPM is marked
   unknown and the baseline remains unchanged.
3. **Given** conflicting baseline sources, **when** authority resolution runs,
   **then** the higher-ranked authority value is retained and the conflict is
   visible.
4. **Given** unsupported source content, **when** extraction runs, **then** it
   reports the unsupported convention rather than using AI or guesses.

## Functional requirements

### Source and authority

- **FR-001**: The system MUST accept a local repository path as a first-class
  MVP1 source mechanism.
- **FR-002**: The system MAY accept an HTTPS Git repository URL only through the
  existing approved Git executable and permitted network access; it MUST NOT
  require a live GitHub API.
- **FR-003**: The system MUST return a repository snapshot containing source
  documents and resolved ref/commit metadata, not canonical project entities.
- **FR-004**: The system MUST discover the IDEAEngineering planning documents
  and distinguish DOC-07, Appendix A, Gantt, Kanban, and README roles.
- **FR-005**: DOC-07 MUST be authoritative for baseline identity, phase sequence,
  milestones, capacity, reserve, dependencies, scope, and planning policy.
- **FR-006**: Appendix A MUST be authoritative for work-package IDs, work-package
  effort, outputs, dependencies, and completion checks, subject to DOC-07.
- **FR-007**: Gantt and Kanban renditions MUST NOT silently override a higher-
  ranked source.
- **FR-008**: The system MUST return explicit diagnostics when no recognized
  planning authority or extraction convention is available.
- **FR-009**: MVP1 extraction MUST be deterministic and MUST NOT use AI/LLM
  inference, commit count, issue count, lines of code, or file count as project
  progress evidence.

### Canonical model and provenance

- **FR-010**: The system MUST expose a deliberate canonical JSON contract with
  `schemaVersion: "1.0"`.
- **FR-011**: The canonical model MUST contain project, source, baseline, phase,
  work-package, delivery-card/task, milestone/decision, dependency, estimate,
  logical responsibility, assignment, capacity, calendar, reserve, provenance,
  and warning information required by MVP1.
- **FR-012**: The canonical model MUST preserve Phase → WorkPackage →
  DeliveryCard hierarchy and Phase/WorkPackage → Milestone/Decision relationships.
- **FR-013**: Work-package and delivery-card effort MUST remain distinct, with an
  explicit accounting rule that prevents double-counting.
- **FR-014**: Important extracted values MUST retain repository, ref/commit,
  source file, section/table/item, extraction rule, authority rank, and
  validation/confidence information where available.
- **FR-015**: The canonical model MUST distinguish `KNOWN`, `CALCULATED`,
  `ESTIMATED`, `UNKNOWN`, `NOT-RUN`, `BLOCKED`, and `UNRESOLVED` states where
  those distinctions affect interpretation.
- **FR-016**: The system MUST preserve source baseline values separately from
  calculated CPM, variance, and forecast results.

### Management analysis

- **FR-017**: The system MUST generate a WBS view from canonical parent IDs and
  MUST NOT maintain a second WBS task store.
- **FR-018**: The system MUST generate a useful Gantt view with hierarchy,
  baseline start/finish, duration, dependency summary, milestone markers,
  status, and critical-path indication when valid.
- **FR-019**: The system MUST generate a Kanban view from the canonical delivery
  cards and source-compatible authored states.
- **FR-020**: The system MUST derive overdue display state from date and execution
  state rather than authoring `OVERDUE` as a separate truth.
- **FR-021**: The system MUST calculate Finish-to-Start dependency CPM when
  graph and duration data are sufficient, including earliest/latest times,
  total float, and dependency critical path.
- **FR-022**: The system MUST detect dependency cycles and unsafe/missing targets.
- **FR-023**: The system MUST distinguish dependency critical path from a
  resource or source sequential-schedule constraint.
- **FR-024**: The system MUST NOT implement resource leveling or Critical Chain
  optimization in MVP1.
- **FR-025**: The system MUST show baseline finish, calculated dependency finish,
  and forecast finish as separate concepts.
- **FR-026**: Forecast and actual effort MUST remain unknown unless explicit
  actual/remaining evidence is supplied.
- **FR-027**: Dashboard health indicators MUST have documented rules and MUST
  show `UNKNOWN` when evidence is insufficient; no unsupported RAG score may be
  invented.
- **FR-028**: The IDEAEngineering WIP limit MUST be imported as project policy
  metadata, not hardcoded as a global application rule.

### CARIO mapping and export

- **FR-029**: Logical project roles MUST be modeled separately from concrete
  company/CARIO identities.
- **FR-030**: Concrete role, person, department, team, and priority mappings MUST
  be configuration, not hardcoded into the domain model.
- **FR-031**: Missing mappings MUST remain unresolved and MUST appear in
  `06_IMPORT_WARNINGS`.
- **FR-032**: The system MUST generate a human-assisted workbook named
  `<ProjectName>_CARIO.xlsx` with `01_TASKS`, `02_ASSIGNMENTS`,
  `03_CHILDREN_MILESTONES`, `04_DEPENDENCIES`, `05_PROJECT_INFO`, and
  `06_IMPORT_WARNINGS`.
- **FR-033**: The workbook MUST preserve enough task, date, hierarchy, milestone,
  dependency, estimate, role, and provenance data for manual CARIO Create Task
  entry.
- **FR-034**: The system MUST NOT claim native CARIO import compatibility or
  implement CARIO API/browser automation.
- **FR-035**: XLSX generation MUST use only already-available platform capabilities
  and MUST NOT require Office COM or a third-party Excel library.

### Persistence and UI

- **FR-036**: The system MUST export `<ProjectName>_project.json` as a deliberate
  machine-readable canonical snapshot.
- **FR-037**: The system MUST validate and reopen supported canonical JSON without
  re-running extraction.
- **FR-038**: The local browser UI MUST use generic “Source” language and expose
  extraction review before export.
- **FR-039**: All UI views MUST consume one canonical project and one calculated
  analysis result.
- **FR-040**: The server MUST bind to loopback by default and MUST NOT expose
  itself to the LAN automatically.

## Non-functional requirements

- **NFR-001 Determinism**: Equivalent fixture input and options produce
  equivalent canonical content and stable ordering.
- **NFR-002 Offline tests**: The test suite runs without Internet access, GitHub,
  package restoration, Python, or database services.
- **NFR-003 Explainability**: A reviewer can trace an important task/date/effort/
  dependency value to a source item through JSON and the UI warning/provenance
  display.
- **NFR-004 Safe failure**: Unsafe extraction never silently fills business data.
- **NFR-005 Scope discipline**: No database, microservices, Docker, third-party
  UI framework, third-party Excel library, AI extraction, or enterprise auth is
  required for MVP1.
- **NFR-006 Local security**: Default HTTP binding is loopback-only and remote
  access is not an implicit deployment mode.
- **NFR-007 Maintainability**: Source extraction, canonical management logic,
  calculations, UI, and outputs communicate through explicit interfaces.

## Key entities

- **Project**: neutral managed project identity.
- **ProjectSource**: repository and capture metadata.
- **ProjectBaseline**: authoritative source planning snapshot.
- **Phase**: time-bounded grouping and gate context.
- **WorkPackage**: authored decomposition and effort-accounting unit.
- **DeliveryCard**: executable planning/card unit.
- **MilestoneDecision**: zero-duration review or decision gate.
- **Dependency**: directed Finish-to-Start or retained unsupported relationship.
- **Estimate**: planned, actual, and remaining work states.
- **ResponsibilityRole**: source logical role and CARIO role semantics.
- **Assignment**: logical-role assignment with optional configured identity.
- **CapacityPlan**: source capacity and calendar policy.
- **Reserve**: controlled reserve summary.
- **SourceReference / Evidence**: provenance for extracted values.
- **ImportWarning / Blocker**: structured unsafe or unresolved condition.
- **ManagementAnalysis**: calculated CPM, variance, health, and forecast results.

## Assumptions

- The local repository path is readable by the process and may be a fixture that
  is not a Git checkout.
- The IDEAEngineering planning convention remains represented by the documented
  relative paths and headings used by the fixture.
- All imported source cards begin `NOT_STARTED`, as required by the source plan.
- The source provides planning effort but not actual execution evidence.
- A blank CARIO person/department/priority field is safer than an inferred value.
- Optional HTTPS source capture is an environment capability, not a test or
  deployment prerequisite.

## Explicit non-goals

- Arbitrary GitHub project understanding.
- GitHub Issues, Projects, Pull Requests, or live GitHub API integration.
- Jira, GitLab, Azure DevOps, Microsoft Project, Trello, or other adapters.
- DOCX, PDF, AI/LLM, or unrecognized-document semantic extraction.
- Multi-source merge, resource leveling, Critical Chain, full EVM, or portfolio
  management.
- CARIO API integration or browser automation.
- Database persistence, multi-user remote service, enterprise authentication,
  SaaS, billing, or multi-tenancy.
- Any equipment control, write-back, remote command, or autonomous management
  decision.

## Success criteria

- **SC-001**: The offline fixture produces the expected project identity,
  baseline, PH0–PH5, 35 work packages, 53 delivery cards, and 7
  milestone/decision cards.
- **SC-002**: F01-A and F01-B remain children of F01; work-package and card
  effort are not double-counted.
- **SC-003**: All supported dependency edges, cycle failures, milestone gates,
  and dependency CPM outputs are test-covered.
- **SC-004**: Every important fixture-derived value has traceable provenance in
  the canonical JSON.
- **SC-005**: Missing concrete role and organization mappings remain blank and
  appear as explicit workbook warnings.
- **SC-006**: All six workbook sheets are generated and contain expected headers,
  UTF-8 text, relationships, and warning rows without Office installed.
- **SC-007**: Re-running extraction against the same fixture yields equivalent
  canonical output and view summaries.
- **SC-008**: The application runs locally through the documented loopback
  command without external package installation or live-service dependencies.
