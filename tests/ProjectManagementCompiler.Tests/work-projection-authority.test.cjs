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
  globalThis.__pmcReviewHarness = { state, activeWorkProjection, applyManifestImport };
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
