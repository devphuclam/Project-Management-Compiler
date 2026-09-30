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
    clearWorkCriterion: typeof clearWorkCriterion === "function" ? clearWorkCriterion : null
  };
  return;
`;
  const instrumentedSource = source.replace(initializationMarker, harness + initializationMarker);
  const context = { document: { getElementById: () => null, querySelectorAll: () => [] }, window: {} };
  vm.runInNewContext(instrumentedSource, context, { filename: appPath });
  return context.__pmcReviewHarness;
}

function manifestResponse(classification, work, snapshotId) {
  return {
    classification,
    snapshot: {
      metadata: { snapshotId },
      views: { work },
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

  assert.equal(app.activeWorkProjection(), null, "Without an official snapshot Work has no official projection.");

  assert.equal(app.applyManifestImport(manifestResponse("OFFICIAL_COMMIT", workA, "A")), true);
  assert.strictEqual(app.activeWorkProjection(), workA, "An official import makes its Work projection authoritative.");

  assert.equal(app.applyManifestImport(manifestResponse("UNCOMMITTED_PREVIEW", workB, "B")), true);
  assert.strictEqual(app.state.views.work, workB, "The candidate preview remains active for the existing preview views.");
  assert.strictEqual(app.activeWorkProjection(), workA, "The active Work destination continues to use official A, not candidate B or null.");
  assert.notStrictEqual(app.activeWorkProjection(), workB, "A candidate projection must never be returned as authoritative Work.");

  assert.equal(app.applyManifestImport({ classification: "FAILED", snapshot: null, diagnostics: [] }), false);
  assert.strictEqual(app.activeWorkProjection(), workA, "A failed import leaves the last successful official Work projection unchanged.");

  assert.equal(app.applyManifestImport(manifestResponse("OFFICIAL_COMMIT", workC, "C")), true);
  assert.strictEqual(app.activeWorkProjection(), workC, "The next successful official snapshot replaces official A with official C.");
  assert.notStrictEqual(app.activeWorkProjection(), workB, "Candidate B never becomes Work authority after later transitions.");
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
