(() => {
  "use strict";

  const state = { project: null, sources: [], views: null, warnings: [], activeView: "dashboard" };
  const byId = (id) => document.getElementById(id);

  function node(tag, text, className) {
    const element = document.createElement(tag);
    if (className) element.className = className;
    if (text !== undefined && text !== null) element.textContent = String(text);
    return element;
  }

  function clear(element) {
    while (element.firstChild) element.removeChild(element.firstChild);
  }

  async function request(url, options) {
    const response = await fetch(url, options);
    const contentType = response.headers.get("content-type") || "";
    const payload = contentType.includes("json") ? await response.json() : await response.text();
    if (!response.ok) {
      const message = payload && payload.message ? payload.message : String(payload);
      const diagnostics = payload && payload.diagnostics ? payload.diagnostics.map(item => item.code + ": " + item.message).join("\n") : "";
      throw new Error(diagnostics ? message + "\n" + diagnostics : message);
    }
    return payload;
  }

  function jsonOptions(body) {
    return { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) };
  }

  function showError(error) {
    const element = byId("error-message");
    element.hidden = !error;
    element.textContent = error ? error.message : "";
  }

  function setStatus(message) {
    byId("connection-status").textContent = message;
  }

  function renderSummary(summary) {
    const panel = byId("summary-panel");
    clear(panel);
    if (!summary) {
      panel.appendChild(node("div", "No project loaded. Analyze a local planning source to begin.", "empty-state"));
      return;
    }
    const dashboard = summary.views.dashboard;
    const values = [
      ["Project", summary.project.name || summary.project.id],
      ["Cards", dashboard.totalCards],
      ["Delivery cards completed", dashboard.completed + "/" + dashboard.totalCards],
      ["In progress", dashboard.inProgress],
      ["Overdue", dashboard.overdue],
      ["At risk", dashboard.atRisk]
    ];
    values.forEach(([label, value]) => {
      const card = node("div", null, "summary-card");
      card.appendChild(node("span", label, "label"));
      card.appendChild(node("strong", value, "value"));
      panel.appendChild(card);
    });
  }

  function renderTable(headers, rows) {
    const wrap = node("div", null, "table-wrap");
    const table = node("table");
    const head = node("thead");
    const headRow = node("tr");
    headers.forEach(header => headRow.appendChild(node("th", header)));
    head.appendChild(headRow);
    table.appendChild(head);
    const body = node("tbody");
    rows.forEach(row => {
      const tr = node("tr");
      row.forEach(value => tr.appendChild(node("td", value === null || value === undefined ? "UNKNOWN" : value)));
      body.appendChild(tr);
    });
    table.appendChild(body);
    wrap.appendChild(table);
    return wrap;
  }

  function renderDashboard(view) {
    const fragment = document.createDocumentFragment();
    const metrics = [
      ["Completed", view.completed], ["In progress", view.inProgress], ["Not started", view.notStarted],
      ["Suspended", view.suspended], ["Cancelled", view.cancelled], ["Late to start", view.lateToStart],
      ["Overdue", view.overdue], ["At risk", view.atRisk], ["Completed late", view.completedLate],
      ["Delivery cards completed", view.completed + "/" + view.totalCards],
      ["Baseline finish", view.baselineFinish || "UNKNOWN"], ["CPM finish", view.cpmFinish || "UNKNOWN"],
      ["Forecast finish", view.forecastFinish || "UNKNOWN"]
    ];
    const grid = node("div", null, "metric-grid");
    metrics.forEach(([label, value]) => {
      const metric = node("div", null, "metric");
      metric.appendChild(node("strong", value));
      metric.appendChild(node("span", label));
      grid.appendChild(metric);
    });
    fragment.appendChild(grid);
    fragment.appendChild(node("h3", "Analysis summaries"));
    fragment.appendChild(renderTable(["View", "Value", "State"], (view.summaries || []).map(item => [item.label, item.value, item.state])));
    return fragment;
  }

  function appendTree(parent, item) {
    const li = node("li");
    const label = node("span");
    label.appendChild(node("span", item.kind, "kind"));
    label.appendChild(node("strong", item.id));
    label.appendChild(document.createTextNode(" " + (item.name || "")));
    li.appendChild(label);
    if (item.children && item.children.length) {
      const children = node("ul");
      item.children.forEach(child => appendTree(children, child));
      li.appendChild(children);
    }
    parent.appendChild(li);
  }

  function renderWbs(view) {
    const list = node("ul", null, "tree");
    appendTree(list, view.root);
    return list;
  }

  function parseDate(value) {
    if (!value) return null;
    const parsed = Date.parse(value + "T00:00:00Z");
    return Number.isFinite(parsed) ? parsed : null;
  }

  function renderGanttLane(entries, kind, timelineStart, timelineEnd) {
    const lane = node("div", null, "lane " + kind);
    const knownEntries = (entries || []).filter(entry => entry);
    const labels = knownEntries.map(entry => {
      if (kind === "alert") return (entry.alertCode || "ALERT") + (entry.label ? ": " + entry.label : "");
      return (entry.start || "UNKNOWN") + " → " + (entry.finish || "UNKNOWN");
    });
    if (kind !== "alert" && knownEntries.some(entry => entry.start)) {
      const track = node("div", null, "gantt-track");
      const span = Math.max(86400000, timelineEnd - timelineStart);
      knownEntries.forEach(entry => {
        const start = parseDate(entry.start);
        const finish = parseDate(entry.finish || entry.start);
        if (start === null || finish === null) return;
        const left = Math.max(0, Math.min(100, ((start - timelineStart) / span) * 100));
        const right = Math.max(left, Math.min(100, ((finish - timelineStart + 86400000) / span) * 100));
        const bar = node("span", null, "gantt-bar");
        bar.style.left = left + "%";
        bar.style.width = Math.max(2, right - left) + "%";
        bar.setAttribute("aria-label", labels.join("; "));
        track.appendChild(bar);
      });
      lane.appendChild(track);
    }
    lane.appendChild(node("span", labels.length ? labels.join("; ") : (kind === "alert" ? "—" : "UNKNOWN"), "lane-text"));
    return lane;
  }

  function renderGantt(view) {
    const datedEntries = (view.items || []).flatMap(item => (item.lanes || []).filter(lane => lane.lane !== "ALERT" && lane.start).map(lane => [lane.start, lane.finish || lane.start]));
    const timestamps = datedEntries.flatMap(range => range.map(parseDate)).filter(value => value !== null);
    const timelineStart = timestamps.length ? Math.min(...timestamps) : Date.now();
    const timelineEnd = timestamps.length ? Math.max(...timestamps) : timelineStart + 86400000;
    const rows = view.items.map(item => {
      const plan = (item.lanes || []).filter(lane => lane.lane === "PLAN");
      const actual = (item.lanes || []).filter(lane => lane.lane === "ACTUAL");
      const alerts = (item.lanes || []).filter(lane => lane.lane === "ALERT");
      const tr = node("tr");
      const identity = node("td");
      identity.appendChild(node("strong", item.workItemId));
      identity.appendChild(document.createTextNode(" " + item.name));
      if (item.isCritical) identity.appendChild(node("span", "critical", "pill critical"));
      tr.appendChild(identity);
      const context = node("td");
      context.appendChild(node("div", item.phaseId || "UNKNOWN"));
      context.appendChild(node("div", item.workPackageId || "UNKNOWN", "muted"));
      tr.appendChild(context);
      tr.appendChild(node("td", item.executionState || "UNKNOWN"));
      tr.appendChild(node("td", (item.logicalRoles || []).join(", ") || "UNKNOWN"));
      tr.appendChild(node("td", (item.dependencyIds || []).join(", ") || "—"));
      [["plan", plan], ["actual", actual], ["alert", alerts]].forEach(([kind, entries]) => {
        const td = node("td");
        td.appendChild(renderGanttLane(entries, kind, timelineStart, timelineEnd));
        tr.appendChild(td);
      });
      return tr;
    });
    const table = renderTable(["Delivery card", "Phase / Work package", "Status", "Logical roles", "Dependencies", "PLAN", "ACTUAL", "ALERT"], []);
    table.querySelector("tbody").append(...rows);
    const section = node("div");
    section.appendChild(node("p", "PLAN is the immutable baseline; ACTUAL uses recorded evidence; ALERT is derived analysis. Bars span the displayed plan/actual dates and retain exact dates as text.", "muted"));
    section.appendChild(table);
    section.appendChild(node("h3", "Milestones"));
    section.appendChild(renderTable(["ID", "Kind", "Planned date", "Dependencies"], (view.milestones || []).map(item => [item.milestoneId, item.kind, item.plannedDate || "UNKNOWN", (item.dependencyIds || []).join(", ") || "—"])));
    return section;
  }

  function renderKanban(view) {
    const board = node("div", null, "kanban");
    (view.columns || []).forEach(column => {
      const panel = node("section", null, "kanban-column");
      const heading = node("h3");
      heading.appendChild(node("span", column.label));
      heading.appendChild(node("span", column.wipLimit === null ? column.items.length : column.items.length + "/" + column.wipLimit));
      panel.appendChild(heading);
      (column.items || []).forEach(item => {
        const card = node("article", null, "kanban-card");
        card.appendChild(node("strong", item.workItemId));
        card.appendChild(node("span", item.name));
        if (item.isOverdue) card.appendChild(node("span", "OVERDUE", "badge danger"));
        if (item.isAtRisk) card.appendChild(node("span", "AT RISK", "badge"));
        if (item.isLateToStart) card.appendChild(node("span", "START DELAY", "badge"));
        panel.appendChild(card);
      });
      board.appendChild(panel);
    });
    return board;
  }

  function renderDependencies(view) {
    return renderTable(["Subject kind", "Subject", "Predecessor kind", "Predecessor", "Type", "Validation", "Included", "Reason"], (view.edges || []).map(edge => [
      edge.subjectKind, edge.subjectId, edge.predecessorKind, edge.predecessorId, edge.dependencyType, edge.validationState,
      edge.includedInAnalysis ? "YES" : "NO", edge.reason
    ]));
  }

  function renderCpm(view) {
    return renderTable(["Node kind", "Node", "Name", "ES", "EF", "LS", "LF", "Float", "Critical", "Calculated finish"], (view.rows || []).map(row => [
      row.nodeKind, row.nodeId, row.name, row.earliestStartWorkingMinutes, row.earliestFinishWorkingMinutes,
      row.latestStartWorkingMinutes, row.latestFinishWorkingMinutes, row.floatWorkingMinutes,
      row.isCritical ? "YES" : "NO", row.calculatedFinish || "UNKNOWN"
    ]));
  }

  function renderWarnings(warnings) {
    const list = node("div", null, "warning-list");
    if (!warnings.length) {
      list.appendChild(node("div", "No warnings.", "empty-state"));
      return list;
    }
    warnings.forEach(warning => {
      const item = node("article", null, "warning-item" + (warning.severity === "ERROR" ? " error" : ""));
      item.appendChild(node("strong", warning.code + " "));
      item.appendChild(node("span", warning.message));
      if (warning.affectedIds && warning.affectedIds.length) item.appendChild(node("div", "Affected: " + warning.affectedIds.join(", "), "muted"));
      list.appendChild(item);
    });
    return list;
  }

  function renderSource() {
    const section = node("div");
    const project = state.project;
    section.appendChild(node("h3", project.project.name || project.project.id));
    section.appendChild(renderTable(["Field", "Value"], [
      ["Project ID", project.project.id], ["Baseline", project.baseline.id + " / " + project.baseline.version],
      ["Planning window", (project.baseline.planningStart || "UNKNOWN") + " → " + (project.baseline.planningFinish || "UNKNOWN")],
      ["Target date", project.baseline.targetDate || "UNKNOWN"], ["Sources", (project.project.sourceIds || []).join(", ")]
    ]));
    section.appendChild(node("h3", "Captured source metadata"));
    const sourceRows = state.sources.flatMap(source => (source.documents || []).map(document => [
      source.sourceId,
      source.repository || "UNKNOWN",
      source.resolvedRef || "UNKNOWN",
      source.captureState || "UNKNOWN",
      source.capturedAtUtc || "UNKNOWN",
      document.documentId,
      document.relativeFile,
      document.format,
      document.sizeBytes,
      document.provenance && document.provenance.extractionRule
        ? document.provenance.extractionRule
        : "UNKNOWN"
    ]));
    section.appendChild(renderTable(
      ["Source ID", "Repository", "Resolved ref", "Capture state", "Captured at", "Document ID", "Relative file", "Format", "Size (bytes)", "Provenance"],
      sourceRows.length ? sourceRows : [["UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN", "UNKNOWN"]]
    ));
    section.appendChild(node("h3", "Warnings"));
    section.appendChild(renderWarnings(state.warnings));
    return section;
  }

  function renderActiveView() {
    const content = byId("view-content");
    clear(content);
    if (!state.views || !state.project) {
      content.appendChild(node("div", "Views will appear here after analysis.", "empty-state"));
      return;
    }
    const view = state.activeView === "source" ? renderSource() :
      state.activeView === "dashboard" ? renderDashboard(state.views.dashboard) :
      state.activeView === "wbs" ? renderWbs(state.views.wbs) :
      state.activeView === "gantt" ? renderGantt(state.views.gantt) :
      state.activeView === "kanban" ? renderKanban(state.views.kanban) :
      state.activeView === "dependencies" ? renderDependencies(state.views.dependencyNetwork) :
      renderCpm(state.views.cpm);
    content.appendChild(view);
  }

  function applySummary(summary) {
    state.project = { project: summary.project, baseline: summary.baseline, analysis: summary.analysis };
    state.sources = summary.sources || [];
    state.views = summary.views;
    state.warnings = summary.warnings || [];
    renderSummary(summary);
    renderActiveView();
  }

  async function analyze() {
    showError(null);
    const sourcePath = byId("source-path").value.trim();
    const asOfDate = byId("as-of-date").value || null;
    if (!asOfDate) {
      showError(new Error("As-of date is required."));
      setStatus("Analysis failed.");
      return;
    }
    try {
      setStatus("Capturing and analyzing…");
      const summary = await request("/api/compile", jsonOptions({ sourcePath, asOfDate }));
      applySummary(summary);
      setStatus("Loaded " + (summary.project.name || summary.project.id) + ".");
    } catch (error) {
      showError(error);
      setStatus("Analysis failed.");
    }
  }

  async function refresh() {
    try {
      const views = await request("/api/views");
      const project = await request("/api/project");
      const warnings = await request("/api/warnings");
      state.views = views;
      state.project = project;
      state.sources = project.sources || [];
      state.warnings = warnings;
      renderActiveView();
      setStatus("Analysis refreshed.");
    } catch (error) {
      showError(error);
    }
  }

  async function reopenJson() {
    showError(null);
    const input = byId("reopen-json");
    const file = input.files && input.files[0];
    if (!file) {
      showError(new Error("Choose a saved canonical JSON file first."));
      return;
    }
    if (file.size > 8 * 1024 * 1024) {
      showError(new Error("The selected JSON file exceeds the 8 MiB local limit."));
      return;
    }
    try {
      setStatus("Reopening canonical project…");
      const json = await file.text();
      const asOfDate = byId("as-of-date").value || null;
      if (!asOfDate) {
        showError(new Error("As-of date is required."));
        setStatus("Reopen failed.");
        return;
      }
      const summary = await request("/api/reopen", jsonOptions({ json, asOfDate }));
      applySummary(summary);
      setStatus("Reopened " + (summary.project.name || summary.project.id) + ".");
    } catch (error) {
      showError(error);
      setStatus("Reopen failed.");
    }
  }

  async function applyExecution(event) {
    event.preventDefault();
    showError(null);
    const numberOrNull = id => byId(id).value === "" ? null : Number(byId(id).value);
    const dateOrNull = id => byId(id).value === "" ? null : byId(id).value;
    const payload = {
      workItemId: byId("execution-work-item").value.trim(),
      executionState: byId("execution-state").value,
      actualStart: dateOrNull("actual-start"),
      actualFinish: dateOrNull("actual-finish"),
      actualEffortHours: numberOrNull("actual-effort"),
      remainingEffortHours: numberOrNull("remaining-effort"),
      lastUpdatedAt: new Date().toISOString(),
      note: byId("execution-note").value
    };
    try {
      setStatus("Applying execution update…");
      const summary = await request("/api/execution", jsonOptions(payload));
      applySummary(summary);
      setStatus("Execution overlay updated; baseline remains unchanged.");
    } catch (error) {
      showError(error);
    }
  }

  document.querySelectorAll(".tab").forEach(tab => tab.addEventListener("click", () => {
    document.querySelectorAll(".tab").forEach(item => item.classList.remove("active"));
    tab.classList.add("active");
    state.activeView = tab.dataset.view;
    renderActiveView();
  }));
  byId("analyze-button").addEventListener("click", analyze);
  byId("refresh-button").addEventListener("click", refresh);
  byId("reopen-button").addEventListener("click", reopenJson);
  byId("execution-form").addEventListener("submit", applyExecution);
  byId("save-json-button").addEventListener("click", () => { window.location.href = "/api/exports/project.json"; });
  byId("export-xlsx-button").addEventListener("click", () => { window.location.href = "/api/exports/cario.xlsx"; });
  if (!byId("as-of-date").value) {
    const now = new Date();
    const localDate = [
      now.getFullYear(),
      String(now.getMonth() + 1).padStart(2, "0"),
      String(now.getDate()).padStart(2, "0")
    ].join("-");
    byId("as-of-date").value = localDate;
  }
})();
