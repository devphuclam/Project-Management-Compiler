const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

function createAppHarness() {
  const appPath = path.resolve(__dirname, "../../src/ProjectManagementCompiler/wwwroot/app.js");
  const initializationMarker = '  document.querySelectorAll(".tab").forEach(tab => tab.addEventListener';
  const source = fs.readFileSync(appPath, "utf8");
  assert.equal(source.split(initializationMarker).length - 1, 1, "The app must have one initialization boundary for the isolated state harness.");

  const harness = `
  setExportAvailability = () => {};
  setExecutionPanelAvailability = () => {};
  setSourceIntakeCollapsed = () => {};
  setExecutionPanelOpen = () => {};
  renderSummary = () => {};
  renderActiveView = () => {};
  globalThis.__pmcReviewHarness = {
    state,
    activeWorkProjection,
    applyManifestImport,
    workCardScopeEntries,
    filterWorkEntries: typeof filterWorkEntries === "function" ? filterWorkEntries : null,
    buildWorkListHierarchy: typeof buildWorkListHierarchy === "function" ? buildWorkListHierarchy : null,
    clearWorkCriterion: typeof clearWorkCriterion === "function" ? clearWorkCriterion : null,
    buildWorkInspectorModel: typeof buildWorkInspectorModel === "function" ? buildWorkInspectorModel : null,
    selectWorkItem: typeof selectWorkItem === "function" ? selectWorkItem : null,
    applyOfficialWorkSnapshot: typeof applyOfficialWorkSnapshot === "function" ? applyOfficialWorkSnapshot : null,
    workScrollPlan: typeof workScrollPlan === "function" ? workScrollPlan : null,
    rememberWorkScroll: typeof rememberWorkScroll === "function" ? rememberWorkScroll : null,
    chooseWorkFocusReturn: typeof chooseWorkFocusReturn === "function" ? chooseWorkFocusReturn : null,
    prepareWorkToGantt: typeof prepareWorkToGantt === "function" ? prepareWorkToGantt : null,
    prepareGanttToWork: typeof prepareGanttToWork === "function" ? prepareGanttToWork : null
  };
  return;
`;
  const instrumentedSource = source.replace(initializationMarker, harness + initializationMarker);
  const context = { document: { getElementById: () => null, querySelectorAll: () => [] }, window: {} };
  vm.runInNewContext(instrumentedSource, context, { filename: appPath });
  return context.__pmcReviewHarness;
}

function manifestResponse(classification, work, snapshotId) {
  const dependencyNetwork = { snapshot: snapshotId };
  const wbs = { root: { snapshot: snapshotId } };
  const gantt = { milestones: [{ milestoneId: "G-D0", snapshot: snapshotId }] };
  return {
    classification,
    snapshot: {
      metadata: { snapshotId },
      views: { work, dependencyNetwork, wbs, gantt },
      projectSummary: { id: "project-1", name: "Fixture project" },
      baseline: {},
      analysis: {},
      sources: [],
      warnings: []
    }
  };
}

test("official Work survives candidate and failed imports, then follows the next official snapshot", () => {
  const app = createAppHarness();
  const workA = { snapshot: "official-A" };
  const workB = { snapshot: "candidate-B" };
  const workC = { snapshot: "official-C" };
  const dependenciesA = { snapshot: "A" };

  assert.equal(app.activeWorkProjection(), null, "Without an official snapshot Work has no official projection.");

  assert.equal(app.applyManifestImport(manifestResponse("OFFICIAL_COMMIT", workA, "A")), true);
  assert.strictEqual(app.activeWorkProjection(), workA, "An official import makes its Work projection authoritative.");
  assert.strictEqual(app.state.officialDependencyNetwork.snapshot, dependenciesA.snapshot, "The inspector's supporting dependency view follows the official Work snapshot.");

  assert.equal(app.applyManifestImport(manifestResponse("UNCOMMITTED_PREVIEW", workB, "B")), true);
  assert.strictEqual(app.state.views.work, workB, "The candidate preview remains active for the existing preview views.");
  assert.strictEqual(app.activeWorkProjection(), workA, "The active Work destination continues to use official A, not candidate B or null.");
  assert.strictEqual(app.state.officialDependencyNetwork.snapshot, dependenciesA.snapshot, "Candidate dependencies must not replace the official Work inspector's dependency authority.");
  assert.notStrictEqual(app.activeWorkProjection(), workB, "A candidate projection must never be returned as authoritative Work.");

  assert.equal(app.applyManifestImport({ classification: "FAILED", snapshot: null, diagnostics: [] }), false);
  assert.strictEqual(app.activeWorkProjection(), workA, "A failed import leaves the last successful official Work projection unchanged.");

  assert.equal(app.applyManifestImport(manifestResponse("OFFICIAL_COMMIT", workC, "C")), true);
  assert.strictEqual(app.activeWorkProjection(), workC, "The next successful official snapshot replaces official A with official C.");
  assert.strictEqual(app.state.officialDependencyNetwork.snapshot, "C", "Supporting inspector projections advance with the new official snapshot.");
  assert.notStrictEqual(app.activeWorkProjection(), workB, "Candidate B never becomes Work authority after later transitions.");
});

test("Work inspector resolves evidence-backed details by typed identity and keeps zero, null, and execution state distinct", () => {
  const app = createAppHarness();
  assert.equal(typeof app.buildWorkInspectorModel, "function", "Missing approved behavior: Work has no shared evidence-backed inspector model.");
  const work = {
    phases: [{ id: "PH0", name: "Chuẩn bị", workPackageIds: ["WP0"] }],
    workPackages: [{ id: "WP0", phaseId: "PH0", name: "Hồ sơ", deliveryCardIds: ["P01"] }],
    cards: [{
      key: { kind: "DeliveryCard", id: "P01" },
      name: "Xác nhận đầu vào",
      phaseId: "PH0",
      workPackageId: "WP0",
      plannedStart: "2026-09-18",
      plannedFinish: "2026-09-21",
      plannedEffortHours: 0,
      plannedEffortState: "KNOWN",
      execution: {
        recorded: true,
        state: null,
        resultState: "NOT_RUN",
        actualStart: null,
        actualFinish: null,
        actualEffortHours: 0,
        remainingEffortHours: null,
        lastUpdatedAt: null
      },
      roles: [{ label: "Đầu mối dự án", person: null }],
      attention: [{ code: "OVERDUE", consequence: "Đang kéo dài quá ngày dự kiến." }],
      sourceReferences: [{ relativeFile: "docs/planning.md", section: "Work plan" }]
    }]
  };
  const views = {
    dependencyNetwork: {
      nodes: [
        { key: "DeliveryCard:P00", kind: "DeliveryCard", id: "P00", name: "Tài liệu được duyệt" },
        { key: "DeliveryCard:P01", kind: "DeliveryCard", id: "P01", name: "Xác nhận đầu vào" },
        { key: "DeliveryCard:P02", kind: "DeliveryCard", id: "P02", name: "Chuẩn bị môi trường" },
        { key: "DeliveryCard:P03", kind: "DeliveryCard", id: "P03", name: "Kiểm tra cuối" },
        { key: "WorkPackage:WP0", kind: "WorkPackage", id: "WP0", name: "Hồ sơ" }
      ],
      edges: [
        { subjectKey: "DeliveryCard:P01", predecessorKey: "DeliveryCard:P00", includedInAnalysis: true, dependencyType: "FINISH_TO_START" },
        { subjectKey: "DeliveryCard:P02", predecessorKey: "DeliveryCard:P01", includedInAnalysis: true, dependencyType: "FINISH_TO_START" },
        { subjectKey: "DeliveryCard:P03", predecessorKey: "DeliveryCard:P02", includedInAnalysis: true, dependencyType: "FINISH_TO_START" },
        { subjectKey: "DeliveryCard:P01", predecessorKey: "DeliveryCard:P03", includedInAnalysis: false, reason: "INVALID_SOURCE_EVIDENCE" },
        { subjectKey: "DeliveryCard:P01", predecessorKey: "WorkPackage:WP0", includedInAnalysis: false, reason: "WORK_PACKAGE_TRACEABILITY" },
        { subjectKey: "DeliveryCard:P01", predecessorKey: "DeliveryCard:MISSING", includedInAnalysis: true, dependencyType: "FINISH_TO_START" }
      ]
    },
    wbs: { root: { kind: "Project", id: "PROJECT", name: "Dự án", children: [] } },
    gantt: { milestones: [] }
  };

  const model = app.buildWorkInspectorModel("DeliveryCard:P01", work, views);
  assert.deepEqual([model.key.kind, model.key.id], ["DeliveryCard", "P01"]);
  assert.equal(model.identity, "DeliveryCard:P01");
  assert.equal(model.name, "Xác nhận đầu vào");
  assert.equal(model.planned.finish, "2026-09-21", "Baseline finish remains a planned date, not a deadline.");
  assert.equal(model.planned.effortHours, 0, "Known zero planned effort must remain zero.");
  assert.equal(model.execution.recorded, true);
  assert.equal(model.execution.state, null, "A recorded row without authored state is not NOT_STARTED.");
  assert.equal(model.execution.resultState, "NOT_RUN");
  assert.equal(model.execution.actualEffortHours, 0, "Known zero actual effort must remain zero.");
  assert.equal(model.execution.remainingEffortHours, null, "Absent remaining effort must not become zero.");
  assert.deepEqual(Array.from(model.roles, role => [role.label, role.person]), [["Đầu mối dự án", null]]);
  assert.equal(model.sourceReferences[0].relativeFile, "docs/planning.md");
  assert.deepEqual(Array.from(model.dependencies.predecessors, item => item.key), ["DeliveryCard:P00"]);
  assert.deepEqual(Array.from(model.dependencies.successors, item => item.key), ["DeliveryCard:P02"]);
  assert.equal(JSON.stringify(model.dependencies).includes("P03"), false, "Excluded edges and transitive items must not become primary relationships.");
  assert.equal(JSON.stringify(model.dependencies).includes("MISSING"), false, "An edge with an unresolved endpoint cannot be presented as a supported link.");
});

test("Work inspector uses evidence-limited variants for non-card hierarchy and milestone identities", () => {
  const app = createAppHarness();
  assert.equal(typeof app.buildWorkInspectorModel, "function", "Missing approved behavior: Work has no shared evidence-backed inspector model.");
  const work = { phases: [], workPackages: [], cards: [] };
  const views = {
    dependencyNetwork: { nodes: [], edges: [] },
    gantt: { milestones: [{ milestoneId: "G-D0", name: "Quyết định D0", kind: "DECISION_GATE", plannedDate: "2026-09-25" }] },
    wbs: { root: { kind: "Project", id: "PROJECT", name: "Dự án", children: [
      { kind: "Phase", id: "PH0", name: "Chuẩn bị", plannedStart: "2026-09-18", plannedFinish: null, sourceReferences: [{ relativeFile: "phase.md" }], children: [] },
      { kind: "WorkPackage", id: "WP0", name: "Hồ sơ", plannedStart: null, plannedFinish: null, sourceReferences: [], children: [] },
      { kind: "Milestone", id: "G-D0", name: "Quyết định D0", plannedStart: "2026-09-25", plannedFinish: "2026-09-25", sourceReferences: [], children: [] }
    ] } }
  };

  const phase = app.buildWorkInspectorModel("Phase:PH0", work, views);
  assert.equal(phase.name, "Chuẩn bị");
  assert.equal(phase.planned.start, "2026-09-18");
  assert.equal(phase.execution, undefined, "A Phase inspector must not fabricate Delivery Card execution semantics.");
  assert.equal(phase.roles, undefined);

  const packageModel = app.buildWorkInspectorModel("WorkPackage:WP0", work, views);
  assert.equal(packageModel.name, "Hồ sơ");
  assert.equal(packageModel.progress, undefined, "A Work Package must not gain invented progress semantics.");
  assert.equal(packageModel.execution, undefined);

  const milestone = app.buildWorkInspectorModel("Milestone:G-D0", work, views);
  assert.equal(milestone.name, "Quyết định D0");
  assert.equal(milestone.kind, "Milestone");
  assert.equal(milestone.milestoneKind, "DECISION_GATE");
  assert.equal(milestone.planned.start, "2026-09-25");
  assert.equal(milestone.execution, undefined, "A milestone inspector must not fabricate Delivery Card execution semantics.");
});

test("official snapshot changes clear only stale selection while previews, failures, and same-snapshot refresh retain Work context", () => {
  const app = createAppHarness();
  assert.equal(typeof app.applyOfficialWorkSnapshot, "function", "Missing approved behavior: official Work context is not reconciled on refresh.");
  const workA = { snapshot: "A", cards: [] };
  const workB = { snapshot: "B", cards: [] };
  const workC = { snapshot: "C", cards: [] };
  app.applyManifestImport(manifestResponse("OFFICIAL_COMMIT", workA, "A"));
  app.state.work.mode = "kanban";
  app.state.work.phaseScope = { kind: "phase", phaseId: "PH0" };
  app.state.work.query = "retain this search";
  app.state.work.authoredStateFilter = "IN_PROGRESS";
  app.state.work.needsAttentionOnly = true;
  app.state.work.includeUnrecorded = false;
  app.state.work.scrollPositions = { list: { criteria: "list-criteria", top: 73 }, kanban: { criteria: "kanban-criteria", top: 146 } };
  app.selectWorkItem("DeliveryCard:P01");
  app.state.work.focusReturnKey = "DeliveryCard:P01";
  const preserved = {
    selectedItemKey: app.state.work.selectedItemKey,
    inspectorOpen: app.state.work.inspectorOpen,
    focusReturnKey: app.state.work.focusReturnKey,
    query: app.state.work.query,
    mode: app.state.work.mode,
    phaseScope: app.state.work.phaseScope,
    authoredStateFilter: app.state.work.authoredStateFilter,
    needsAttentionOnly: app.state.work.needsAttentionOnly,
    includeUnrecorded: app.state.work.includeUnrecorded,
    scrollPositions: app.state.work.scrollPositions
  };

  app.applyManifestImport(manifestResponse("UNCOMMITTED_PREVIEW", workB, "B"));
  assert.strictEqual(app.activeWorkProjection(), workA);
  assert.equal(app.state.work.selectedItemKey, preserved.selectedItemKey);
  assert.equal(app.state.work.inspectorOpen, preserved.inspectorOpen);
  assert.equal(app.state.work.query, preserved.query);
  assert.equal(app.state.work.scrollPositions, preserved.scrollPositions);
  app.applyManifestImport({ classification: "FAILED", snapshot: null, diagnostics: [] });
  assert.equal(app.state.work.selectedItemKey, preserved.selectedItemKey);
  assert.equal(app.state.work.focusReturnKey, preserved.focusReturnKey);

  app.applyOfficialWorkSnapshot(manifestResponse("OFFICIAL_COMMIT", workA, "A").snapshot, { classification: "OFFICIAL_COMMIT", metadata: { snapshotId: "A" } });
  assert.equal(app.state.work.selectedItemKey, preserved.selectedItemKey, "Refreshing the same official snapshot retains selection.");
  assert.equal(app.state.work.inspectorOpen, preserved.inspectorOpen);

  app.applyOfficialWorkSnapshot(manifestResponse("OFFICIAL_COMMIT", workC, "C").snapshot, { classification: "OFFICIAL_COMMIT", metadata: { snapshotId: "C" } });
  assert.equal(app.state.work.selectedItemKey, null, "A different official snapshot must clear the old selected identity immediately.");
  assert.equal(app.state.work.inspectorOpen, false, "A different official snapshot must close the old inspector context.");
  assert.equal(app.state.work.focusReturnKey, null);
  assert.equal(app.state.work.query, preserved.query, "Snapshot identity must not silently reset shared Work filters.");
  assert.equal(app.state.work.mode, preserved.mode);
  assert.equal(app.state.work.phaseScope, preserved.phaseScope);
  assert.equal(app.state.work.scrollPositions, preserved.scrollPositions, "Snapshot identity is used to clear selection, not unrelated view state.");
  assert.strictEqual(app.activeWorkProjection(), workC);
});

test("Work scroll and focus restoration are independent from selection and criteria", () => {
  const app = createAppHarness();
  assert.equal(typeof app.workScrollPlan, "function", "Missing approved behavior: Work cannot decide scroll restoration from shared criteria.");
  assert.equal(typeof app.rememberWorkScroll, "function", "Missing approved behavior: List and Kanban do not own independent transient scroll positions.");
  assert.equal(typeof app.chooseWorkFocusReturn, "function", "Missing approved behavior: inspector focus return is not independent from selection.");
  assert.deepEqual(JSON.parse(JSON.stringify(app.workScrollPlan({ criteria: "same", top: 240 }, "same", false))), { kind: "restore", top: 240 });
  assert.deepEqual(JSON.parse(JSON.stringify(app.workScrollPlan({ criteria: "old", top: 240 }, "new", true))), { kind: "reveal" });
  assert.deepEqual(JSON.parse(JSON.stringify(app.workScrollPlan({ criteria: "old", top: 240 }, "new", false))), { kind: "start" });

  app.rememberWorkScroll("list", "scope-a", 90);
  app.rememberWorkScroll("kanban", "scope-a", 180);
  assert.deepEqual(JSON.parse(JSON.stringify(app.state.work.scrollPositions)), {
    list: { criteria: "scope-a", top: 90 },
    kanban: { criteria: "scope-a", top: 180 }
  });
  assert.deepEqual(JSON.parse(JSON.stringify(app.chooseWorkFocusReturn("DeliveryCard:P01", ["DeliveryCard:P01"], "work-view-heading"))), { kind: "item", key: "DeliveryCard:P01" });
  assert.deepEqual(JSON.parse(JSON.stringify(app.chooseWorkFocusReturn("DeliveryCard:P01", [], "work-search"))), { kind: "control", id: "work-search" });
});

test("Work and Gantt navigation preserve typed selection, drawer condition, and Work filters", () => {
  const app = createAppHarness();
  assert.equal(typeof app.prepareWorkToGantt, "function", "Missing approved behavior: Work has no stable-identity Gantt navigation.");
  assert.equal(typeof app.prepareGanttToWork, "function", "Missing approved behavior: Gantt has no stable-identity Work navigation.");
  app.state.views = {
    wbs: { root: { kind: "Project", id: "PROJECT", children: [
      { kind: "Phase", id: "PH0", children: [
        { kind: "WorkPackage", id: "WP0", children: [{ kind: "DeliveryCard", id: "P01", children: [] }] }
      ] }
    ] } },
    gantt: { items: [{ workItemId: "P01", phaseId: "PH0", workPackageId: "WP0", lanes: [] }], milestones: [] }
  };
  app.state.project = { baseline: {} };
  app.state.work.mode = "kanban";
  app.state.work.phaseScope = { kind: "phase", phaseId: "PH0" };
  app.state.work.query = "keep-query";
  app.state.work.authoredStateFilter = "IN_PROGRESS";
  app.state.work.inspectorOpen = false;
  const ganttBefore = { showDependencies: false, zoom: "week", preset: "plan" };
  app.state.gantt.showDependencies = ganttBefore.showDependencies;
  app.state.gantt.zoom = ganttBefore.zoom;
  app.state.gantt.preset = ganttBefore.preset;

  app.prepareWorkToGantt("DeliveryCard:P01");
  assert.equal(app.state.gantt.selectedRowKey, "DeliveryCard:P01");
  assert.equal(app.state.gantt.detailsOpen, false, "A closed Work inspector must not open the Gantt drawer during navigation.");
  assert.equal(app.state.gantt.expandedKeys.has("Phase:PH0"), true);
  assert.equal(app.state.gantt.expandedKeys.has("WorkPackage:WP0"), true);
  assert.equal(app.state.gantt.showDependencies, ganttBefore.showDependencies, "Navigation must not change Gantt dependency controls.");

  app.prepareGanttToWork("DeliveryCard:P01", true);
  assert.equal(app.state.work.selectedItemKey, "DeliveryCard:P01");
  assert.equal(app.state.work.inspectorOpen, true);
  assert.equal(app.state.work.mode, "kanban");
  assert.equal(app.state.work.phaseScope.phaseId, "PH0");
  assert.equal(app.state.work.query, "keep-query");
  assert.equal(app.state.work.authoredStateFilter, "IN_PROGRESS");
});

function workSearchFixture() {
  return {
    currentPhaseId: "PH1",
    phases: [
      { id: "PH0", name: "Chuẩn Bị Nền Tảng", order: 0, workPackageIds: ["WP0"] },
      { id: "PH1", name: "Đảm Bảo Vận Hành", order: 1, workPackageIds: ["WP1"] },
      { id: "PH2", name: "Giai Đoạn Trống", order: 2, workPackageIds: ["WP_EMPTY"] }
    ],
    workPackages: [
      { id: "WP0", phaseId: "PH0", name: "Hồ Sơ Kỹ Thuật", order: 0, deliveryCardIds: ["P01", "P02"] },
      { id: "WP1", phaseId: "PH1", name: "Kiểm Thử Tích Hợp", order: 0, deliveryCardIds: ["P03", "P04"] },
      { id: "WP_EMPTY", phaseId: "PH2", name: "Chưa có gói giao việc", order: 0, deliveryCardIds: [] }
    ],
    cards: [
      {
        key: { kind: "DeliveryCard", id: "P01" }, name: "Tài Liệu Đầu Vào", phaseId: "PH0", workPackageId: "WP0",
        execution: { state: "NOT_STARTED" }, attention: [],
        sourceReferences: [{ relativePath: "private-project-path" }],
        diagnostics: [{ message: "diagnostic-private" }], proposalText: "proposal-private", analysisMessage: "analysis-private"
      },
      {
        key: { kind: "DeliveryCard", id: "P02" }, name: "Xác Nhận Chứng Chỉ", phaseId: "PH0", workPackageId: "WP0",
        execution: { state: "IN_PROGRESS" }, attention: [{ code: "OVERDUE", consequence: "Trễ kế hoạch" }]
      },
      {
        key: { kind: "DeliveryCard", id: "P03" }, name: "Tổng Hợp Lỗi", phaseId: "PH1", workPackageId: "WP1",
        execution: { state: null }, attention: [],
        sourceReferences: [{ relativePath: "another-private-path" }], diagnostics: [{ message: "needle-raw-diagnostic" }]
      },
      {
        key: { kind: "DeliveryCard", id: "P04" }, name: "Rà Soát Cuối", phaseId: "PH1", workPackageId: "WP1",
        execution: { state: "COMPLETED" }, attention: [{ code: "AT_RISK", consequence: "Tín hiệu được hỗ trợ" }], unrelatedTechnicalMetadata: "search-forbidden"
      }
    ]
  };
}

test("Work search is case and Vietnamese-diacritic insensitive and searches only approved names and stable ID", () => {
  const app = createAppHarness();
  assert.equal(typeof app.filterWorkEntries, "function", "Missing approved behavior: Work has one shared search/filter pipeline.");
  const work = workSearchFixture();
  const resultIds = () => Array.from(app.filterWorkEntries(work), entry => entry.card.key.id);
  app.state.work.query = "tai lieu dau vao";
  assert.deepEqual(resultIds(), ["P01"]);

  app.state.work.query = "p04";
  assert.deepEqual(resultIds(), ["P04"]);

  app.state.work.query = "dam bao van hanh";
  assert.deepEqual(resultIds(), ["P03", "P04"]);

  app.state.work.query = "ho so ky thuat";
  assert.deepEqual(resultIds(), ["P01", "P02"]);

  for (const forbiddenTerm of ["private-project-path", "diagnostic-private", "needle-raw-diagnostic", "proposal-private", "analysis-private", "search-forbidden"]) {
    app.state.work.query = forbiddenTerm;
    assert.equal(app.filterWorkEntries(work).length, 0, `Search must not match unrelated source/diagnostic/proposal/technical content: ${forbiddenTerm}`);
  }
});

test("Work search and authored, attention, and unrecorded filters intersect without mutating authorities or Gantt state", () => {
  const app = createAppHarness();
  assert.equal(typeof app.filterWorkEntries, "function", "Missing approved behavior: Work has one shared search/filter pipeline.");
  const work = workSearchFixture();
  const originalWork = JSON.parse(JSON.stringify(work));
  const project = { canonical: { value: "unchanged" } };
  const views = { analysis: { alerts: ["unchanged"] }, gantt: { start: "2026-01-01" } };
  const gantt = { zoom: "week", phaseFilter: "PH0", scrollTop: 27 };
  app.state.project = project;
  app.state.views = views;
  app.state.gantt = gantt;
  app.state.work.phaseScope = { kind: "all", phaseId: null };
  app.state.work.focusedPhaseId = "PH1";
  app.state.work.query = "";
  app.state.work.authoredStateFilter = "IN_PROGRESS";
  app.state.work.needsAttentionOnly = false;
  app.state.work.includeUnrecorded = true;
  const resultIds = () => Array.from(app.filterWorkEntries(work), entry => entry.card.key.id);

  app.state.work.mode = "list";
  const listResults = resultIds();
  app.state.work.mode = "kanban";
  const kanbanResults = resultIds();
  assert.deepEqual(listResults, ["P02", "P03"], "Unrecorded cards remain an explicit separate selection alongside the chosen authored state.");
  assert.deepEqual(kanbanResults, listResults, "List and Kanban must resolve the same identities under the same shared criteria.");

  app.state.work.needsAttentionOnly = true;
  assert.deepEqual(resultIds(), ["P02"]);

  app.state.work.authoredStateFilter = "ALL";
  app.state.work.includeUnrecorded = false;
  app.state.work.needsAttentionOnly = false;
  assert.deepEqual(resultIds(), ["P01", "P02", "P04"], "Excluding unrecorded cards must not relabel or remove authored states.");

  app.state.work.phaseScope = { kind: "phase", phaseId: "PH0" };
  app.state.work.query = "dam bao van hanh";
  app.state.work.includeUnrecorded = true;
  assert.equal(app.filterWorkEntries(work).length, 0, "Explicit phase scope must still apply after a parent-name match.");

  assert.deepEqual(work, originalWork, "Filtering must not mutate canonical Work cards or their evidence.");
  assert.strictEqual(app.state.project, project, "Filtering must not mutate the canonical project authority.");
  assert.strictEqual(app.state.views, views, "Filtering must not mutate analysis or other view projections.");
  assert.strictEqual(app.state.gantt, gantt, "Filtering must not mutate Gantt-specific filters, range, or scroll state.");
});

test("filtered Work List keeps only surviving ancestor paths and does not count ancestors as cards", () => {
  const app = createAppHarness();
  assert.equal(typeof app.buildWorkListHierarchy, "function", "Missing approved behavior: the filtered List hierarchy projection is not implemented.");
  const work = workSearchFixture();
  app.state.work.query = "P02";
  const projection = app.buildWorkListHierarchy(work);

  assert.equal(projection.resultFilterActive, true);
  assert.equal(projection.cardCount, 1, "Only the surviving Delivery Card contributes to the result count.");
  assert.deepEqual(Array.from(projection.phases, item => item.phase.id), ["PH0"]);
  assert.deepEqual(Array.from(projection.phases[0].workPackages, item => item.workPackage.id), ["WP0"]);
  assert.deepEqual(Array.from(projection.phases[0].workPackages[0].cards, card => card.key.id), ["P02"]);
  assert.equal(projection.phases.some(item => item.phase.id === "PH2"), false, "Empty phases must not become orphan filter results.");

  app.state.work.query = "not-a-real-card";
  const noMatches = app.buildWorkListHierarchy(work);
  assert.equal(noMatches.cardCount, 0);
  assert.deepEqual(Array.from(noMatches.phases), [], "No-match results must contain no orphan Phase or Work Package ancestors.");
  assert.equal(noMatches.emptyKind, "no-results");
});

test("phase scope alone keeps normal hierarchy and search temporarily opens paths without changing saved expansion", () => {
  const app = createAppHarness();
  assert.equal(typeof app.buildWorkListHierarchy, "function", "Missing approved behavior: the filtered List hierarchy projection is not implemented.");
  const work = workSearchFixture();
  app.state.work.phaseScope = { kind: "phase", phaseId: "PH2" };
  const emptyPhase = app.buildWorkListHierarchy(work);
  assert.equal(emptyPhase.resultFilterActive, false, "Phase scope is a collection boundary, not a result filter.");
  assert.equal(emptyPhase.emptyKind, "empty-phase", "An explicitly selected phase with no canonical cards is a distinct empty phase.");
  assert.deepEqual(Array.from(emptyPhase.phases, item => item.phase.id), ["PH2"], "The normal hierarchy keeps the selected empty phase visible.");
  assert.deepEqual(Array.from(emptyPhase.phases[0].workPackages, item => item.workPackage.id), ["WP_EMPTY"]);

  app.state.work.phaseScope = { kind: "all", phaseId: null };
  app.state.work.expandedPhaseIds = new Set(["PH1"]);
  app.state.work.expandedWorkPackageIds = new Set(["WP1"]);
  const savedPhaseExpansion = Array.from(app.state.work.expandedPhaseIds);
  const savedPackageExpansion = Array.from(app.state.work.expandedWorkPackageIds);
  app.state.work.query = "P02";
  const searchResults = app.buildWorkListHierarchy(work);
  assert.equal(searchResults.phases[0].temporarilyExpanded, true, "Search must reveal the matching collapsed Phase path.");
  assert.equal(searchResults.phases[0].workPackages[0].temporarilyExpanded, true, "Search must reveal the matching collapsed Work Package path.");
  assert.deepEqual(Array.from(app.state.work.expandedPhaseIds), savedPhaseExpansion, "Temporary search opening must not overwrite user Phase expansion.");
  assert.deepEqual(Array.from(app.state.work.expandedWorkPackageIds), savedPackageExpansion, "Temporary search opening must not overwrite user Work Package expansion.");

  app.state.work.query = "";
  const restored = app.buildWorkListHierarchy(work);
  assert.equal(restored.resultFilterActive, false);
  assert.equal(restored.phases[0].temporarilyExpanded, false);
  assert.equal(restored.phases.find(item => item.phase.id === "PH1").temporarilyExpanded, false);
  assert.deepEqual(Array.from(app.state.work.expandedPhaseIds), savedPhaseExpansion, "Clearing search must restore the exact saved Phase expansion state.");
  assert.deepEqual(Array.from(app.state.work.expandedWorkPackageIds), savedPackageExpansion, "Clearing search must restore the exact saved Work Package expansion state.");
});

test("empty-state recovery clears only its named criterion or widens only the empty phase scope", () => {
  const app = createAppHarness();
  assert.equal(typeof app.clearWorkCriterion, "function", "Missing approved behavior: empty-state recovery does not expose criterion-scoped clearing.");
  app.state.work.query = "no-match";
  app.state.work.authoredStateFilter = "IN_PROGRESS";
  app.state.work.needsAttentionOnly = true;
  app.state.work.includeUnrecorded = false;
  app.clearWorkCriterion("query");
  assert.equal(app.state.work.query, "");
  assert.equal(app.state.work.authoredStateFilter, "IN_PROGRESS");
  assert.equal(app.state.work.needsAttentionOnly, true);
  assert.equal(app.state.work.includeUnrecorded, false);
});
