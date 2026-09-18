(() => {
  "use strict";

  function createGanttState() {
    return {
      projectId: null,
      zoom: "month",
      fit: true,
      expandedKeys: new Set(),
      expansionInitialized: false,
      phaseFilter: "ALL",
      executionFilter: "ALL",
      attentionOnly: false,
      criticalOnly: false,
      overdueOnly: false,
      atRiskOnly: false,
      showDependencies: false,
      criticalPath: false,
      selectedRowKey: null
    };
  }

  const state = { project: null, sources: [], views: null, warnings: [], activeView: "dashboard", gantt: createGanttState() };
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

  function currentSummary() {
    if (!state.project || !state.views) return null;
    return {
      project: state.project.project,
      baseline: state.project.baseline,
      analysis: state.project.analysis,
      views: state.views,
      sources: state.sources,
      warnings: state.warnings
    };
  }

  function isAttentionAlert(alert) {
    const code = String(alert && alert.alertCode || "").toUpperCase();
    return code !== "COMPLETED_ON_TIME" && code !== "CANCELLED";
  }

  function attentionRows(summary) {
    if (!summary || !summary.views || !summary.views.gantt) return [];
    const items = new Map((summary.views.gantt.items || []).map(item => [String(item.workItemId), item]));
    const alerts = ((summary.analysis && summary.analysis.alerts) || []).filter(isAttentionAlert);
    const rows = alerts.map(alert => {
      const item = items.get(String(alert.workItemId));
      return {
        id: alert.workItemId || "UNKNOWN",
        name: item && item.name || alert.workItemId || "Unidentified work item",
        code: alert.alertCode || "ATTENTION",
        label: alert.message || alert.label || alert.reason || "Derived schedule condition"
      };
    });
    if (rows.length) return rows;
    items.forEach(item => {
      (item.lanes || []).filter(lane => String(lane.lane || "").toUpperCase() === "ALERT" && isAttentionAlert(lane)).forEach(alert => {
        rows.push({
          id: item.workItemId || "UNKNOWN",
          name: item.name || item.workItemId || "Unidentified work item",
          code: alert.alertCode || "ATTENTION",
          label: alert.message || alert.label || "Derived schedule condition"
        });
      });
    });
    return rows;
  }

  function renderAttentionQueue(summary, compact) {
    const queue = node("section", null, "attention-queue" + (compact ? " attention-queue-compact" : ""));
    const heading = node("div", null, "section-heading");
    const headingCopy = node("div");
    headingCopy.appendChild(node("p", "INTERVENTION", "eyebrow"));
    headingCopy.appendChild(node("h3", "Needs attention"));
    heading.appendChild(headingCopy);
    const rows = attentionRows(summary);
    const attentionCount = rows.length;
    heading.appendChild(node("span", attentionCount ? attentionCount + " signals" : "Clear", "attention-count" + (attentionCount ? " is-active" : "")));
    queue.appendChild(heading);

    const visibleRows = rows.slice(0, compact ? 4 : 5);
    if (!visibleRows.length) {
      const clearState = node("div", null, "attention-empty");
      clearState.appendChild(node("strong", "No active attention signals."));
      clearState.appendChild(node("span", "The current baseline is not asking for intervention.", "muted"));
      queue.appendChild(clearState);
      return queue;
    }
    visibleRows.forEach(row => {
      const item = node("button", null, "attention-item");
      item.type = "button";
      item.dataset.summaryView = "gantt";
      item.dataset.summaryKey = typedKey("DeliveryCard", row.id);
      item.dataset.summaryAttention = "true";
      item.appendChild(node("span", row.code, "attention-code"));
      const copy = node("span", null, "attention-copy");
      copy.appendChild(node("strong", row.name));
      copy.appendChild(node("span", row.label, "muted"));
      item.appendChild(copy);
      item.appendChild(node("span", "→", "attention-arrow"));
      queue.appendChild(item);
    });
    if (rows.length > visibleRows.length) {
      const more = node("button", "View all attention in Gantt", "text-action");
      more.type = "button";
      more.dataset.summaryView = "gantt";
      more.dataset.summaryAttention = "true";
      queue.appendChild(more);
    }
    return queue;
  }

  function updateActiveTab(view) {
    document.querySelectorAll(".tab").forEach(item => item.classList.toggle("active", item.dataset.view === view));
  }

  function activateView(view) {
    state.activeView = view;
    updateActiveTab(view);
    renderActiveView();
  }

  function openSummaryView(view, key, attention) {
    if (view === "gantt") {
      const projectId = state.project && state.project.project && state.project.project.id || state.project && state.project.id || "project";
      prepareGanttState(projectId);
      state.gantt.selectedRowKey = key || null;
      state.gantt.attentionOnly = Boolean(attention);
      if (key && state.views && state.views.wbs && state.views.gantt) {
        const model = buildGanttRows(state.views.wbs.root, state.views.gantt, state.project && state.project.baseline || {});
        let row = model.byKey.get(key) || null;
        while (row) {
          if (row.isSummary) state.gantt.expandedKeys.add(row.key);
          row = row.parentKey ? model.byKey.get(row.parentKey) : null;
        }
      }
    }
    activateView(view);
    const content = byId("view-content");
    const reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (content) content.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth", block: "start" });
  }

  function renderSummary(summary) {
    const panel = byId("summary-panel");
    clear(panel);
    if (!summary) {
      panel.appendChild(node("div", "No project loaded. Analyze a local planning source to begin.", "empty-state"));
      return;
    }
    const dashboard = summary.views.dashboard;
    panel.className = "summary-region project-control-center";
    const overdue = Number(dashboard.overdue || 0);
    const atRisk = Number(dashboard.atRisk || 0);
    const lateToStart = Number(dashboard.lateToStart || 0);
    const attentionCount = overdue + atRisk + lateToStart;
    const healthLabel = overdue ? "Intervention needed" : attentionCount ? "Watch closely" : "On track";
    const healthClass = overdue ? "danger" : attentionCount ? "warning" : "success";
    const completed = Number(dashboard.completed || 0);
    const totalCards = Number(dashboard.totalCards || 0);
    const progress = totalCards ? Math.round((completed / totalCards) * 100) : 0;
    const milestones = (summary.views.gantt && summary.views.gantt.milestones || []).filter(item => parseDate(item.plannedDate) !== null).sort((left, right) => parseDate(left.plannedDate) - parseDate(right.plannedDate));
    const asOfTimestamp = parseDate(summary.analysis && summary.analysis.asOfDate);
    const nextMilestone = milestones.find(item => asOfTimestamp === null || parseDate(item.plannedDate) >= asOfTimestamp) || milestones[0] || null;

    const hero = node("div", null, "summary-hero");
    const heroCopy = node("div", null, "summary-hero-copy");
    heroCopy.appendChild(node("p", "PROJECT CONTROL CENTER", "eyebrow"));
    heroCopy.appendChild(node("h2", summary.project.name || summary.project.id));
    heroCopy.appendChild(node("p", "Baseline " + (summary.baseline.version || summary.baseline.id || "UNKNOWN") + " · reporting as of " + formatDate(summary.analysis && summary.analysis.asOfDate), "summary-meta"));
    hero.appendChild(heroCopy);
    const heroMeta = node("div", null, "summary-hero-meta");
    heroMeta.appendChild(node("span", healthLabel, "summary-status " + healthClass));
    heroMeta.appendChild(node("span", attentionCount ? attentionCount + " attention signals" : "No active signals", "summary-signal-count"));
    hero.appendChild(heroMeta);
    panel.appendChild(hero);

    const mainGrid = node("div", null, "summary-main-grid");
    const progressCard = node("section", null, "summary-progress-card");
    const progressHeading = node("div", null, "summary-card-heading");
    progressHeading.appendChild(node("span", "DELIVERY PROGRESS", "eyebrow"));
    progressHeading.appendChild(node("strong", progress + "%", "summary-progress-value"));
    progressCard.appendChild(progressHeading);
    const progressTrack = node("div", null, "summary-progress-track");
    const progressFill = node("span");
    progressFill.style.width = progress + "%";
    progressTrack.appendChild(progressFill);
    progressCard.appendChild(progressTrack);
    progressCard.appendChild(node("p", completed + " of " + totalCards + " delivery cards completed."));
    const progressStats = node("div", null, "summary-inline-stats");
    progressStats.appendChild(node("span", String(dashboard.inProgress || 0) + " in progress"));
    progressStats.appendChild(node("span", String(dashboard.notStarted || 0) + " not started"));
    progressCard.appendChild(progressStats);
    mainGrid.appendChild(progressCard);

    const milestoneCard = node("section", null, "summary-milestone-card");
    milestoneCard.appendChild(node("p", "UP NEXT", "eyebrow"));
    milestoneCard.appendChild(node("h3", "Next milestone"));
    milestoneCard.appendChild(node("strong", nextMilestone ? nextMilestone.name || nextMilestone.milestoneId : "No dated milestone"));
    milestoneCard.appendChild(node("p", nextMilestone ? formatDate(nextMilestone.plannedDate) + " · " + (nextMilestone.milestoneId || "Milestone") : "Add a dated decision point to the baseline.", "muted"));
    if (nextMilestone) {
      const milestoneAction = node("button", "Open in Gantt →", "text-action");
      milestoneAction.type = "button";
      milestoneAction.dataset.summaryView = "gantt";
      milestoneAction.dataset.summaryKey = typedKey("Milestone", nextMilestone.milestoneId);
      milestoneCard.appendChild(milestoneAction);
    }
    mainGrid.appendChild(milestoneCard);
    mainGrid.appendChild(renderAttentionQueue(summary, false));
    panel.appendChild(mainGrid);

    const metrics = node("div", null, "summary-metric-grid");
    [["Cards", dashboard.totalCards, ""], ["Completed", dashboard.completed, "success"], ["In progress", dashboard.inProgress, "accent"], ["Overdue", dashboard.overdue, "danger"], ["At risk", dashboard.atRisk, "warning"], ["Late to start", dashboard.lateToStart, "warning"]].forEach(([label, value, tone]) => {
      const card = node("div", null, "summary-card " + tone);
      card.appendChild(node("span", label, "label"));
      card.appendChild(node("strong", value, "value"));
      metrics.appendChild(card);
    });
    panel.appendChild(metrics);
    const footer = node("div", null, "summary-footer");
    footer.appendChild(node("span", (summary.baseline.planningStart || "UNKNOWN") + " → " + (summary.baseline.planningFinish || "UNKNOWN"), "muted"));
    footer.appendChild(node("span", "Baseline remains immutable · execution is recorded separately", "muted"));
    panel.appendChild(footer);
    panel.addEventListener("click", event => {
      const target = event.target.closest("[data-summary-view]");
      if (!target) return;
      openSummaryView(target.dataset.summaryView, target.dataset.summaryKey || null, target.dataset.summaryAttention === "true");
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
    const summary = currentSummary();
    const heading = node("div", null, "dashboard-heading");
    heading.appendChild(node("p", "OPERATING PULSE", "eyebrow"));
    heading.appendChild(node("h3", "Delivery pulse"));
    heading.appendChild(node("p", "A compact read on progress, schedule pressure, and the decisions that need a human next.", "muted"));
    fragment.appendChild(heading);
    const metrics = [
      ["Completed", view.completed, "success"], ["In progress", view.inProgress, "accent"], ["Not started", view.notStarted, ""],
      ["Late to start", view.lateToStart, "warning"], ["Overdue", view.overdue, "danger"], ["At risk", view.atRisk, "warning"],
      ["Completed late", view.completedLate, "danger"], ["Delivery cards completed", view.completed + "/" + view.totalCards, "accent"]
    ];
    const grid = node("div", null, "metric-grid");
    metrics.forEach(([label, value, tone]) => {
      const metric = node("div", null, "metric " + tone);
      metric.appendChild(node("strong", value));
      metric.appendChild(node("span", label));
      grid.appendChild(metric);
    });
    fragment.appendChild(grid);
    const dashboardGrid = node("div", null, "dashboard-grid");
    const schedule = node("section", null, "dashboard-card");
    schedule.appendChild(node("p", "SCHEDULE READOUT", "eyebrow"));
    schedule.appendChild(node("h3", "Three dates to keep visible"));
    [["Baseline finish", view.baselineFinish], ["CPM finish", view.cpmFinish], ["Forecast finish", view.forecastFinish]].forEach(([label, value]) => {
      const row = node("div", null, "readout-row");
      row.appendChild(node("span", label, "muted"));
      row.appendChild(node("strong", value || "UNKNOWN"));
      schedule.appendChild(row);
    });
    dashboardGrid.appendChild(schedule);
    if (summary) dashboardGrid.appendChild(renderAttentionQueue(summary, true));
    fragment.appendChild(dashboardGrid);
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

  const DAY_MS = 86400000;

  function parseDate(value) {
    if (!value) return null;
    const text = String(value);
    const parsed = Date.parse(text.includes("T") ? text : text + "T00:00:00Z");
    return Number.isFinite(parsed) ? parsed : null;
  }

  function canonicalKind(value) {
    const compact = String(value || "").replace(/[\s_-]/g, "").toLowerCase();
    if (compact === "project") return "Project";
    if (compact === "phase") return "Phase";
    if (compact === "workpackage") return "WorkPackage";
    if (compact === "deliverycard" || compact === "card") return "DeliveryCard";
    if (compact === "milestone" || compact === "decision") return "Milestone";
    return String(value || "");
  }

  function typedKey(kind, id) {
    return canonicalKind(kind) + ":" + String(id || "");
  }

  function stateLabel(value) {
    if (!value) return "No state";
    return String(value).replace(/_/g, " ").toLowerCase().replace(/(^|\s)\S/g, letter => letter.toUpperCase());
  }

  function formatDate(value) {
    const timestamp = parseDate(value);
    if (timestamp === null) return "—";
    return new Date(timestamp).toISOString().slice(0, 10);
  }

  function formatMonth(timestamp) {
    return new Date(timestamp).toLocaleDateString(undefined, { month: "short", year: "numeric", timeZone: "UTC" });
  }

  function formatDay(timestamp) {
    return new Date(timestamp).toLocaleDateString(undefined, { day: "numeric", month: "short", timeZone: "UTC" });
  }

  function formatWeek(timestamp) {
    return "Week of " + formatDate(timestamp);
  }

  function dateAt(timestamp, days) {
    return timestamp + days * DAY_MS;
  }

  function dayCount(start, end) {
    return Math.max(1, Math.round((end - start) / DAY_MS) + 1);
  }

  function laneEntries(item, laneName) {
    return (item && item.lanes || []).filter(entry => String(entry.lane || "").toUpperCase() === laneName);
  }

  function buildGanttRows(wbsRoot, ganttView, baseline) {
    const cardByKey = new Map((ganttView.items || []).map(item => [typedKey("DeliveryCard", item.workItemId), item]));
    const milestoneByKey = new Map((ganttView.milestones || []).map(item => [typedKey("Milestone", item.milestoneId), item]));
    const rows = [];
    const seen = new Set();

    function addRow(wbsNode, parentKey, depth, inheritedPhaseId, inheritedWorkPackageId) {
      const kind = canonicalKind(wbsNode.kind);
      const id = String(wbsNode.id || "");
      if (!kind || !id) return null;
      const key = typedKey(kind, id);
      const item = kind === "DeliveryCard" ? cardByKey.get(key) : null;
      const milestone = kind === "Milestone" ? milestoneByKey.get(key) : null;
      const phaseId = kind === "Phase" ? id : (item && item.phaseId) || wbsNode.phaseId || inheritedPhaseId || null;
      const workPackageId = kind === "WorkPackage" ? id : (item && item.workPackageId) || inheritedWorkPackageId || null;
      const planLane = laneEntries(item, "PLAN")[0];
      const actualLane = laneEntries(item, "ACTUAL")[0];
      const alerts = laneEntries(item, "ALERT");
      let planStart = wbsNode.plannedStart || null;
      let planFinish = wbsNode.plannedFinish || null;
      if (kind === "Project") {
        planStart = baseline && baseline.planningStart || planStart;
        planFinish = baseline && baseline.planningFinish || planFinish;
      }
      if (kind === "DeliveryCard") {
        planStart = planLane && planLane.start || planStart;
        planFinish = planLane && planLane.finish || planFinish;
      }
      if (kind === "Milestone") {
        planStart = milestone && milestone.plannedDate || planStart;
        planFinish = milestone && milestone.plannedDate || planFinish || planStart;
      }
      const row = {
        key,
        kind,
        id,
        name: (item && item.name) || (milestone && milestone.name) || wbsNode.name || id,
        parentKey,
        depth,
        phaseId,
        workPackageId,
        isSummary: kind === "Project" || kind === "Phase" || kind === "WorkPackage",
        isMilestone: kind === "Milestone",
        isExecutable: kind === "DeliveryCard" || kind === "Milestone",
        isCritical: Boolean((item && item.isCritical) || (milestone && milestone.isCritical)),
        state: (item && item.executionState) || (milestone && milestone.state) || wbsNode.executionState || null,
        roles: item && item.logicalRoles || [],
        dependencyIds: item && item.dependencyIds || milestone && milestone.dependencyIds || [],
        plan: { start: planStart, finish: planFinish },
        actual: actualLane ? { start: actualLane.start || null, finish: actualLane.finish || null, state: actualLane.state } : null,
        alerts,
        childKeys: [],
        descendantKeys: [],
        hasDescendantAlert: false
      };
      rows.push(row);
      seen.add(key);

      (wbsNode.children || []).forEach(child => {
        const childRow = addRow(child, key, depth + 1, kind === "Phase" ? id : phaseId, kind === "WorkPackage" ? id : workPackageId);
        if (childRow) row.childKeys.push(childRow.key);
      });
      return row;
    }

    if (wbsRoot) addRow(wbsRoot, null, 0, null, null);

    for (const item of ganttView.items || []) {
      const key = typedKey("DeliveryCard", item.workItemId);
      if (seen.has(key)) continue;
      const planLane = laneEntries(item, "PLAN")[0];
      const actualLane = laneEntries(item, "ACTUAL")[0];
      rows.push({
        key,
        kind: "DeliveryCard",
        id: item.workItemId,
        name: item.name || item.workItemId,
        parentKey: rows[0] && rows[0].key || null,
        depth: 1,
        phaseId: item.phaseId || null,
        workPackageId: item.workPackageId || null,
        isSummary: false,
        isMilestone: false,
        isExecutable: true,
        isCritical: Boolean(item.isCritical),
        state: item.executionState || null,
        roles: item.logicalRoles || [],
        dependencyIds: item.dependencyIds || [],
        plan: { start: planLane && planLane.start || null, finish: planLane && planLane.finish || null },
        actual: actualLane ? { start: actualLane.start || null, finish: actualLane.finish || null, state: actualLane.state } : null,
        alerts: laneEntries(item, "ALERT"),
        childKeys: [],
        descendantKeys: [],
        hasDescendantAlert: false
      });
      seen.add(key);
    }

    const byKey = new Map(rows.map(row => [row.key, row]));
    for (let index = rows.length - 1; index >= 0; index -= 1) {
      const row = rows[index];
      const children = row.childKeys.map(key => byKey.get(key)).filter(Boolean);
      row.isCritical = row.isCritical || children.some(child => child.isCritical);
      row.hasDescendantAlert = row.alerts.length > 0 || children.some(child => child.hasDescendantAlert);
      row.descendantKeys = children.flatMap(child => [child.key, ...child.descendantKeys]);
    }
    return { rows, byKey };
  }

  function buildTimelineRange(rows, analysis, baseline) {
    const timestamps = [];
    const addDate = value => {
      const timestamp = parseDate(value);
      if (timestamp !== null) timestamps.push(timestamp);
    };
    addDate(baseline && baseline.planningStart);
    addDate(baseline && baseline.planningFinish);
    addDate(analysis && analysis.asOfDate);
    rows.forEach(row => {
      addDate(row.plan.start);
      addDate(row.plan.finish);
      addDate(row.actual && row.actual.start);
      addDate(row.actual && row.actual.finish);
    });
    if (!timestamps.length) return null;
    const timelineStart = Math.min(...timestamps);
    let timelineEnd = Math.max(...timestamps);
    if (timelineEnd <= timelineStart) timelineEnd = dateAt(timelineStart, 1);
    return { timelineStart, timelineEnd, span: Math.max(DAY_MS, timelineEnd - timelineStart + DAY_MS) };
  }

  function datePercent(timestamp, range) {
    if (timestamp === null || !range) return 0;
    return Math.max(0, Math.min(100, ((timestamp - range.timelineStart) / range.span) * 100));
  }

  function createTicks(range, zoom) {
    const major = [];
    const minor = [];
    const first = new Date(range.timelineStart);
    first.setUTCHours(0, 0, 0, 0);
    const month = new Date(Date.UTC(first.getUTCFullYear(), first.getUTCMonth(), 1));
    for (let cursor = month.getTime(); cursor <= range.timelineEnd + DAY_MS; cursor = Date.UTC(new Date(cursor).getUTCFullYear(), new Date(cursor).getUTCMonth() + 1, 1)) {
      major.push(cursor);
      if (major.length > 240) break;
    }
    if (zoom === "month") {
      minor.push(...major);
    } else if (zoom === "week") {
      for (let cursor = range.timelineStart; cursor <= range.timelineEnd + DAY_MS; cursor = dateAt(cursor, 7)) minor.push(cursor);
    } else {
      for (let cursor = range.timelineStart; cursor <= range.timelineEnd + DAY_MS; cursor = dateAt(cursor, 1)) minor.push(cursor);
    }
    return { major, minor };
  }

  function timelineWidth(range) {
    const viewContent = byId("view-content");
    if (state.gantt.fit) return Math.max(760, (viewContent && viewContent.clientWidth || 1080) - 380);
    const days = dayCount(range.timelineStart, range.timelineEnd);
    if (state.gantt.zoom === "day") return Math.max(1100, days * 38);
    if (state.gantt.zoom === "week") return Math.max(1000, Math.ceil(days / 7) * 110);
    return Math.max(900, Math.ceil(days / 30) * 170);
  }

  function appendAxisRow(parent, ticks, range, width, className, formatter) {
    const row = node("div", null, "gantt-axis-row " + className);
    row.style.width = width + "px";
    ticks.forEach((timestamp, index) => {
      const next = ticks[index + 1] || range.timelineEnd + DAY_MS;
      const tick = node("span", null, "gantt-axis-tick");
      tick.style.left = datePercent(timestamp, range) + "%";
      tick.style.width = Math.max(0.5, ((next - timestamp) / range.span) * 100) + "%";
      tick.appendChild(node("span", formatter(timestamp), "gantt-axis-label"));
      row.appendChild(tick);
    });
    parent.appendChild(row);
  }

  function renderTimelineHeader(range, width, analysis) {
    const header = node("div", null, "gantt-timeline-header gantt-timeline");
    header.style.width = width + "px";
    const axis = node("div", null, "gantt-axis");
    axis.style.width = width + "px";
    const ticks = createTicks(range, state.gantt.zoom);
    appendAxisRow(axis, ticks.major, range, width, "gantt-axis-month", formatMonth);
    appendAxisRow(axis, ticks.minor, range, width, "gantt-axis-detail", state.gantt.zoom === "day" ? formatDay : state.gantt.zoom === "week" ? formatWeek : formatMonth);
    header.appendChild(axis);
    const marker = createMarker(analysis && analysis.asOfDate, range, "gantt-as-of-marker", "As of " + formatDate(analysis && analysis.asOfDate), true);
    if (marker) header.appendChild(marker);
    return header;
  }

  function createMarker(date, range, className, label, showLabel) {
    const timestamp = parseDate(date);
    if (timestamp === null) return null;
    const marker = node("div", null, className);
    marker.style.left = datePercent(timestamp, range) + "%";
    marker.setAttribute("role", "img");
    marker.setAttribute("aria-label", label);
    if (showLabel) marker.appendChild(node("span", label, "gantt-marker-label"));
    return marker;
  }

  function renderTimelineBackground(range, width) {
    const background = node("div", null, "gantt-timeline-background");
    background.style.width = width + "px";
    for (let cursor = range.timelineStart; cursor <= range.timelineEnd; cursor = dateAt(cursor, 1)) {
      const day = new Date(cursor).getUTCDay();
      if (day !== 0 && day !== 6) continue;
      const weekend = node("span", null, "gantt-weekend");
      weekend.style.left = datePercent(cursor, range) + "%";
      weekend.style.width = Math.max(0.25, (DAY_MS / range.span) * 100) + "%";
      weekend.setAttribute("aria-hidden", "true");
      background.appendChild(weekend);
    }
    return background;
  }

  function createBar(row, entry, range, className, label, analysis, isActual) {
    const start = parseDate(entry && entry.start);
    if (start === null) return null;
    const actualFinish = entry.finish || (row.state === "IN_PROGRESS" ? analysis.asOfDate : null);
    const finish = parseDate(isActual ? actualFinish : entry.finish || entry.start);
    const end = finish === null ? start + DAY_MS : finish + DAY_MS;
    const bar = node("button", null, "gantt-bar " + className + (finish === null ? " gantt-open-bar" : ""));
    bar.type = "button";
    bar.dataset.ganttSelect = row.key;
    bar.dataset.ganttLane = isActual ? "ACTUAL" : "PLAN";
    bar.style.left = datePercent(start, range) + "%";
    bar.style.width = Math.max(0.8, ((end - start) / range.span) * 100) + "%";
    bar.setAttribute("aria-label", label + " " + formatDate(entry.start) + " to " + formatDate(finish));
    bar.title = label + ": " + formatDate(entry.start) + " → " + formatDate(finish);
    return bar;
  }

  function renderTimelineRow(row, range, analysis) {
    const timelineRow = node("div", null, "gantt-timeline-row" + (row.isSummary ? " gantt-summary-row" : ""));
    timelineRow.dataset.rowKey = row.key;
    const planLane = node("div", null, "gantt-sub-lane gantt-plan-lane");
    const actualLane = node("div", null, "gantt-sub-lane gantt-actual-lane");
    const planLabel = row.isMilestone ? "PLAN milestone" : "PLAN";
    if (row.isMilestone && row.plan.start) {
      const milestone = node("button", null, "gantt-milestone");
      milestone.type = "button";
      milestone.dataset.ganttSelect = row.key;
      milestone.style.left = datePercent(parseDate(row.plan.start), range) + "%";
      milestone.setAttribute("aria-label", planLabel + " " + row.id + " on " + formatDate(row.plan.start));
      milestone.title = planLabel + ": " + formatDate(row.plan.start);
      planLane.appendChild(milestone);
    } else if (row.plan.start) {
      const plan = createBar(row, row.plan, range, "gantt-plan-bar", planLabel, analysis || {}, false);
      if (plan) planLane.appendChild(plan);
    }
    if (row.actual) {
      const actual = createBar(row, row.actual, range, "gantt-actual-bar", "ACTUAL", analysis || {}, true);
      if (actual) actualLane.appendChild(actual);
    } else if (!row.isSummary && !row.isMilestone) {
      const empty = node("span", "—", "gantt-actual-empty");
      empty.setAttribute("aria-label", "No actual evidence");
      actualLane.appendChild(empty);
    }
    timelineRow.appendChild(planLane);
    timelineRow.appendChild(actualLane);
    row.alerts.forEach(alert => {
      const anchorDate = (row.actual && (row.actual.finish || row.actual.start)) || row.plan.finish || row.plan.start || analysis && analysis.asOfDate;
      const marker = createMarker(anchorDate, range, "gantt-alert-marker", (alert.alertCode || "ALERT") + (alert.label ? ": " + alert.label : ""), false);
      if (marker) {
        marker.dataset.ganttSelect = row.key;
        marker.setAttribute("role", "button");
        marker.tabIndex = 0;
        marker.title = (alert.alertCode || "ALERT") + (alert.label ? ": " + alert.label : "");
        timelineRow.appendChild(marker);
      }
    });
    return timelineRow;
  }

  function createDependencySvg(visibleRows, dependencyView, range, width, selectedRowKey) {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("class", "gantt-connectors");
    svg.setAttribute("aria-hidden", "true");
    const rowHeight = 44;
    const height = visibleRows.length * rowHeight;
    svg.setAttribute("width", String(width));
    svg.setAttribute("height", String(height));
    svg.setAttribute("viewBox", "0 0 " + width + " " + height);
    const visibleIndex = new Map(visibleRows.map((row, index) => [row.key, index]));
    const dependencyEdges = (state.views && state.views.dependencyNetwork && state.views.dependencyNetwork.edges) || dependencyView && dependencyView.edges || [];
    const eligibleEdges = dependencyEdges.filter(edge => edge.includedInAnalysis !== false);
    eligibleEdges.forEach(edge => {
      const predecessorKey = edge.predecessorKey || typedKey(edge.predecessorKind, edge.predecessorId);
      const subjectKey = edge.subjectKey || typedKey(edge.subjectKind, edge.subjectId);
      if (!visibleIndex.has(predecessorKey) || !visibleIndex.has(subjectKey)) return;
      const isRelated = selectedRowKey && (selectedRowKey === predecessorKey || selectedRowKey === subjectKey);
      if (!state.gantt.showDependencies && !isRelated) return;
      const predecessor = visibleRows[visibleIndex.get(predecessorKey)];
      const subject = visibleRows[visibleIndex.get(subjectKey)];
      const predecessorTimestamp = parseDate(predecessor.plan.finish || predecessor.plan.start);
      const subjectTimestamp = parseDate(subject.plan.start || subject.plan.finish);
      if (predecessorTimestamp === null || subjectTimestamp === null) return;
      const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
      const x1 = (datePercent(predecessorTimestamp, range) / 100) * width;
      const x2 = (datePercent(subjectTimestamp, range) / 100) * width;
      const y1 = visibleIndex.get(predecessorKey) * rowHeight + rowHeight / 2;
      const y2 = visibleIndex.get(subjectKey) * rowHeight + rowHeight / 2;
      const bend = x1 + Math.max(18, Math.abs(x2 - x1) / 2);
      path.setAttribute("d", "M " + x1 + " " + y1 + " C " + bend + " " + y1 + ", " + bend + " " + y2 + ", " + x2 + " " + y2);
      path.setAttribute("class", "gantt-dependency-path" + (isRelated ? " is-related" : ""));
      svg.appendChild(path);
    });
    return svg;
  }

  function renderTaskRow(row, analysis) {
    const taskRow = node("div", null, "gantt-task-row" + (row.isSummary ? " gantt-summary-row" : ""));
    taskRow.dataset.rowKey = row.key;
    const identity = node("div", null, "gantt-task-cell gantt-task-identity");
    identity.style.paddingInlineStart = (10 + row.depth * 18) + "px";
    if (row.isSummary) {
      const toggle = node("button", row.childKeys.length && state.gantt.expandedKeys.has(row.key) ? "▾" : "▸", "gantt-row-toggle");
      toggle.type = "button";
      toggle.dataset.ganttAction = "toggle-row";
      toggle.dataset.ganttKey = row.key;
      toggle.disabled = row.childKeys.length === 0;
      toggle.setAttribute("aria-expanded", row.childKeys.length && state.gantt.expandedKeys.has(row.key) ? "true" : "false");
      toggle.setAttribute("aria-label", (state.gantt.expandedKeys.has(row.key) ? "Collapse " : "Expand ") + row.name);
      identity.appendChild(toggle);
    } else {
      identity.appendChild(node("span", "", "gantt-row-spacer"));
    }
    const select = node("button", null, "gantt-row-select");
    select.type = "button";
    select.dataset.ganttSelect = row.key;
    select.appendChild(node("span", row.id, "gantt-row-id"));
    select.appendChild(node("span", row.name, "gantt-row-name"));
    select.appendChild(node("span", row.kind, "gantt-row-kind"));
    const context = [];
    if (row.phaseId && row.kind !== "Phase") context.push("Phase " + row.phaseId);
    if (row.workPackageId && row.kind !== "WorkPackage") context.push("WP " + row.workPackageId);
    if (context.length) select.appendChild(node("span", context.join(" · "), "gantt-row-context"));
    select.setAttribute("aria-label", row.kind + " " + row.id + ": " + row.name);
    identity.appendChild(select);
    taskRow.appendChild(identity);

    const status = node("div", null, "gantt-task-cell gantt-task-status");
    status.appendChild(node("span", stateLabel(row.state), "gantt-state"));
    if (state.gantt.criticalPath && row.isCritical) status.appendChild(node("span", "Critical", "gantt-signal critical"));
    taskRow.appendChild(status);

    const owner = node("div", null, "gantt-task-cell gantt-task-owner");
    owner.appendChild(node("span", row.roles.length ? row.roles.join(", ") : "—", row.roles.length ? "" : "muted"));
    taskRow.appendChild(owner);

    const signals = node("div", null, "gantt-task-cell gantt-task-signals");
    row.alerts.forEach(alert => signals.appendChild(node("span", alert.alertCode || "ALERT", "gantt-signal alert")));
    if (!row.alerts.length && row.isCritical && state.gantt.criticalPath) signals.appendChild(node("span", "CPM", "gantt-signal critical"));
    taskRow.appendChild(signals);
    return taskRow;
  }

  function rowMatchesFilters(row) {
    if (state.gantt.phaseFilter !== "ALL" && row.phaseId !== state.gantt.phaseFilter) return false;
    if (state.gantt.executionFilter !== "ALL" && String(row.state || "UNKNOWN") !== state.gantt.executionFilter) return false;
    if (state.gantt.attentionOnly && !row.alerts.some(isAttentionAlert)) return false;
    if (state.gantt.criticalOnly && !row.isCritical) return false;
    if (state.gantt.overdueOnly && !row.alerts.some(alert => alert.alertCode === "OVERDUE")) return false;
    if (state.gantt.atRiskOnly && !row.alerts.some(alert => alert.alertCode === "AT_RISK")) return false;
    return true;
  }

  function visibleGanttRows(rows, byKey) {
    const matches = new Map(rows.map(row => [row.key, rowMatchesFilters(row)]));
    const hasMatchingDescendant = row => {
      if (matches.get(row.key)) return true;
      return row.childKeys.some(childKey => {
        const child = byKey.get(childKey);
        return child && hasMatchingDescendant(child);
      });
    };
    return rows.filter(row => {
      if (!hasMatchingDescendant(row)) return false;
      let parent = row.parentKey && byKey.get(row.parentKey);
      while (parent) {
        if (parent.isSummary && !state.gantt.expandedKeys.has(parent.key)) return false;
        parent = parent.parentKey && byKey.get(parent.parentKey);
      }
      return true;
    });
  }

  function relatedGanttKeys(selectedRowKey, dependencyView) {
    const related = new Set();
    if (!selectedRowKey) return related;
    related.add(selectedRowKey);
    const dependencyEdges = dependencyView && dependencyView.edges || [];
    dependencyEdges.filter(edge => edge.includedInAnalysis !== false).forEach(edge => {
      const predecessorKey = edge.predecessorKey || typedKey(edge.predecessorKind, edge.predecessorId);
      const subjectKey = edge.subjectKey || typedKey(edge.subjectKind, edge.subjectId);
      if (predecessorKey === selectedRowKey) related.add(subjectKey);
      if (subjectKey === selectedRowKey) related.add(predecessorKey);
    });
    return related;
  }

  function renderGanttToolbar(rows, range, analysis) {
    const toolbar = node("div", null, "gantt-toolbar");
    const toolbarTop = node("div", null, "gantt-toolbar-top");
    const toolbarHeading = node("div", null, "gantt-toolbar-heading");
    toolbarHeading.appendChild(node("p", "PLAN CONTROL", "eyebrow"));
    toolbarHeading.appendChild(node("h3", "Schedule view"));
    toolbarHeading.appendChild(node("p", "Read the immutable baseline first; use attention mode to isolate the work that needs a decision.", "muted"));
    toolbarTop.appendChild(toolbarHeading);
    const actions = node("div", null, "gantt-toolbar-actions");
    const addAction = (label, action, pressed) => {
      const button = node("button", label, "secondary gantt-action");
      button.type = "button";
      button.dataset.ganttAction = action;
      if (pressed !== undefined) button.setAttribute("aria-pressed", pressed ? "true" : "false");
      actions.appendChild(button);
    };
    addAction("Expand all", "expand-all");
    addAction("Collapse all", "collapse-all");
    addAction("Needs attention", "attention", state.gantt.attentionOnly);
    addAction("−", "zoom-out");
    addAction("+", "zoom-in");
    addAction("Fit project", "fit-project");
    addAction("Critical path", "critical-path", state.gantt.criticalPath);
    addAction("Show dependencies", "dependencies", state.gantt.showDependencies);
    toolbarTop.appendChild(actions);
    toolbar.appendChild(toolbarTop);

    const filters = node("div", null, "gantt-filters");
    const phase = node("select");
    phase.dataset.ganttFilter = "phase";
    phase.setAttribute("aria-label", "Filter by phase");
    phase.appendChild(node("option", "All phases"));
    phase.lastChild.value = "ALL";
    rows.filter(row => row.kind === "Phase").forEach(row => {
      const option = node("option", row.id + " · " + row.name);
      option.value = row.id;
      phase.appendChild(option);
    });
    phase.value = state.gantt.phaseFilter;
    filters.appendChild(phase);

    const execution = node("select");
    execution.dataset.ganttFilter = "execution";
    execution.setAttribute("aria-label", "Filter by execution state");
    [["ALL", "All execution states"], ["NOT_STARTED", "Not started"], ["IN_PROGRESS", "In progress"], ["COMPLETED", "Completed"], ["SUSPENDED", "Suspended"], ["CANCELLED", "Cancelled"], ["UNKNOWN", "No state"]].forEach(([value, label]) => {
      const option = node("option", label);
      option.value = value;
      execution.appendChild(option);
    });
    execution.value = state.gantt.executionFilter;
    filters.appendChild(execution);

    [["critical-only", "Critical only", state.gantt.criticalOnly], ["overdue", "Overdue", state.gantt.overdueOnly], ["at-risk", "At risk", state.gantt.atRiskOnly]].forEach(([value, label, checked]) => {
      const wrapper = node("label", null, "gantt-filter-check");
      const input = node("input");
      input.type = "checkbox";
      input.dataset.ganttFilter = value;
      input.checked = checked;
      wrapper.appendChild(input);
      wrapper.appendChild(node("span", label));
      filters.appendChild(wrapper);
    });

    const zoom = node("select");
    zoom.dataset.ganttFilter = "zoom";
    zoom.setAttribute("aria-label", "Timeline zoom");
    [["month", "Month"], ["week", "Week"], ["day", "Day"]].forEach(([value, label]) => {
      const option = node("option", label);
      option.value = value;
      zoom.appendChild(option);
    });
    zoom.value = state.gantt.zoom;
    filters.appendChild(zoom);
    toolbar.appendChild(filters);

    const status = node("p", "Timeline " + formatDate(range.timelineStart) + " → " + formatDate(range.timelineEnd) + " · as-of " + formatDate(analysis && analysis.asOfDate) + " · " + rows.length + " rows", "gantt-toolbar-status muted");
    toolbar.appendChild(status);
    return toolbar;
  }

  function appendDetailField(parent, label, value) {
    const field = node("div", null, "gantt-detail-field");
    field.appendChild(node("span", label, "muted"));
    field.appendChild(node("strong", value));
    parent.appendChild(field);
  }

  function renderGanttDetail(row, analysis, dependencyView) {
    const panel = node("aside", null, "gantt-detail-panel");
    panel.id = "gantt-detail-panel";
    panel.setAttribute("aria-live", "polite");
    if (!row) {
      panel.appendChild(node("p", "ROW INSPECTOR", "eyebrow"));
      panel.appendChild(node("h3", "Select a row to inspect"));
      panel.appendChild(node("p", "Choose a phase, work package, delivery card, or milestone to compare plan, actual evidence, variance, and dependencies.", "muted"));
      return panel;
    }
    const heading = node("div", null, "gantt-detail-heading");
    heading.appendChild(node("span", row.kind + " · " + row.id, "eyebrow"));
    heading.appendChild(node("h3", row.name));
    panel.appendChild(heading);
    const fields = node("div", null, "gantt-detail-fields");
    appendDetailField(fields, "State", stateLabel(row.state));
    appendDetailField(fields, "PLAN", formatDate(row.plan.start) + " → " + formatDate(row.plan.finish));
    if (row.actual) {
      const actualFinish = row.actual.finish || (row.state === "IN_PROGRESS" ? analysis && analysis.asOfDate : null);
      appendDetailField(fields, "ACTUAL", formatDate(row.actual.start) + " → " + formatDate(actualFinish));
    } else {
      appendDetailField(fields, "ACTUAL", "No actual evidence");
    }
    appendDetailField(fields, "As-of", formatDate(analysis && analysis.asOfDate));
    const variance = (analysis && analysis.workItemVariances || []).find(item => item.workItemId === row.id);
    appendDetailField(fields, "Start variance", variance && variance.startVarianceWorkingMinutes !== null && variance.startVarianceWorkingMinutes !== undefined ? variance.startVarianceWorkingMinutes + " working minutes" : "—");
    appendDetailField(fields, "Finish variance", variance && variance.finishVarianceWorkingMinutes !== null && variance.finishVarianceWorkingMinutes !== undefined ? variance.finishVarianceWorkingMinutes + " working minutes" : "—");
    appendDetailField(fields, "Logical roles", row.roles.length ? row.roles.join(", ") : "—");
    panel.appendChild(fields);

    const alerts = node("div", null, "gantt-detail-section");
    alerts.appendChild(node("h4", "Alerts"));
    if (!row.alerts.length) alerts.appendChild(node("p", "No active alerts.", "muted"));
    row.alerts.forEach(alert => {
      const item = node("p", null, "gantt-detail-alert");
      item.appendChild(node("strong", alert.alertCode || "ALERT"));
      item.appendChild(document.createTextNode(" " + (alert.label || "")));
      if (alert.reasonWorkItemIds && alert.reasonWorkItemIds.length) item.appendChild(node("span", " Reasons: " + alert.reasonWorkItemIds.join(", "), "muted"));
      alerts.appendChild(item);
    });
    panel.appendChild(alerts);

    const dependencies = node("div", null, "gantt-detail-section");
    dependencies.appendChild(node("h4", "Dependencies"));
    const edges = (dependencyView && dependencyView.edges || []).filter(edge => edge.includedInAnalysis !== false && ((edge.subjectKey || typedKey(edge.subjectKind, edge.subjectId)) === row.key || (edge.predecessorKey || typedKey(edge.predecessorKind, edge.predecessorId)) === row.key));
    if (!edges.length) dependencies.appendChild(node("p", "No analysis-eligible dependencies.", "muted"));
    edges.forEach(edge => {
      const subjectKey = edge.subjectKey || typedKey(edge.subjectKind, edge.subjectId);
      const predecessorKey = edge.predecessorKey || typedKey(edge.predecessorKind, edge.predecessorId);
      dependencies.appendChild(node("p", predecessorKey + " → " + subjectKey + " · " + (edge.dependencyType || ""), "gantt-detail-dependency"));
    });
    panel.appendChild(dependencies);

    if (row.kind === "DeliveryCard") {
      const record = node("button", "Record execution", "primary gantt-record-execution");
      record.type = "button";
      record.dataset.ganttAction = "record-execution";
      record.dataset.ganttKey = row.id;
      panel.appendChild(record);
    }
    return panel;
  }

  function prepareGanttState(projectId) {
    if (state.gantt.projectId === projectId) return;
    state.gantt = createGanttState();
    state.gantt.projectId = projectId;
  }

  function renderGantt(view, analysis, baseline, dependencyView, cpmView, projectId) {
    prepareGanttState(projectId || baseline && baseline.id || "project");
    const model = buildGanttRows(state.views && state.views.wbs && state.views.wbs.root, view, baseline || {});
    const rows = model.rows;
    if (!state.gantt.expansionInitialized) {
      rows.filter(row => row.kind === "Project" || row.kind === "Phase").forEach(row => state.gantt.expandedKeys.add(row.key));
      state.gantt.expansionInitialized = true;
    }
    const range = buildTimelineRange(rows, analysis || {}, baseline || {});
    if (!range) return node("div", "No dated planning evidence is available for a timeline.", "empty-state");
    const visibleRows = visibleGanttRows(rows, model.byKey);
    const selectedRow = model.byKey.get(state.gantt.selectedRowKey) || null;
    const relatedKeys = relatedGanttKeys(state.gantt.selectedRowKey, dependencyView);
    const width = timelineWidth(range);
    const shell = node("section", null, "gantt-shell");
    shell.dataset.ganttProject = projectId || "project";
    shell.style.setProperty("--timeline-width", width + "px");
    shell.appendChild(renderGanttToolbar(rows, range, analysis || {}));

    const legend = node("div", null, "gantt-legend");
    [["gantt-plan-key", "PLAN · immutable baseline"], ["gantt-actual-key", "ACTUAL · recorded evidence"], ["gantt-alert-key", "ALERT · derived condition"], ["gantt-milestone-key", "Milestone · zero duration"]].forEach(([className, label]) => {
      const item = node("span", null, "gantt-legend-item");
      item.appendChild(node("span", "", className));
      item.appendChild(node("span", label));
      legend.appendChild(item);
    });
    shell.appendChild(legend);

    const scroll = node("div", null, "gantt-scroll");
    const canvas = node("div", null, "gantt-canvas");
    canvas.style.setProperty("--timeline-width", width + "px");
    const headerRow = node("div", null, "gantt-header-row");
    const taskHeader = node("div", null, "gantt-task-header gantt-task-pane");
    ["Task / ID", "Status", "Owner / role", "Signal"].forEach(label => taskHeader.appendChild(node("span", label)));
    headerRow.appendChild(taskHeader);
    headerRow.appendChild(renderTimelineHeader(range, width, analysis || {}));
    canvas.appendChild(headerRow);

    const contentGrid = node("div", null, "gantt-grid");
    const taskRows = node("div", null, "gantt-task-rows gantt-task-pane");
    const timeline = node("div", null, "gantt-timeline");
    timeline.style.width = width + "px";
    timeline.style.height = Math.max(44, visibleRows.length * 44) + "px";
    timeline.appendChild(renderTimelineBackground(range, width));
    const bodyMarker = createMarker(analysis && analysis.asOfDate, range, "gantt-as-of-marker", "As of " + formatDate(analysis && analysis.asOfDate), false);
    if (bodyMarker) timeline.appendChild(bodyMarker);
    timeline.appendChild(createDependencySvg(visibleRows, dependencyView, range, width, state.gantt.selectedRowKey));
    const timelineRows = node("div", null, "gantt-timeline-rows");
    visibleRows.forEach(row => {
      const taskRow = renderTaskRow(row, analysis || {});
      const timelineRow = renderTimelineRow(row, range, analysis || {});
      const selected = row.key === state.gantt.selectedRowKey;
      const related = relatedKeys.has(row.key) && !selected;
      const dimmed = state.gantt.selectedRowKey && !selected && !related;
      [taskRow, timelineRow].forEach(element => {
        if (selected) element.classList.add("gantt-selected");
        if (related) element.classList.add("gantt-related");
        if (dimmed) element.classList.add("gantt-dimmed");
      });
      taskRows.appendChild(taskRow);
      timelineRows.appendChild(timelineRow);
    });
    timeline.appendChild(timelineRows);
    contentGrid.appendChild(taskRows);
    contentGrid.appendChild(timeline);
    canvas.appendChild(contentGrid);
    scroll.appendChild(canvas);
    shell.appendChild(scroll);
    shell.appendChild(renderGanttDetail(selectedRow, analysis || {}, dependencyView));

    shell.addEventListener("click", event => {
      const actionTarget = event.target.closest("[data-gantt-action]");
      const selectTarget = event.target.closest("[data-gantt-select]");
      if (actionTarget) {
        const action = actionTarget.dataset.ganttAction;
        if (action === "expand-all") {
          state.gantt.expandedKeys = new Set(rows.filter(row => row.isSummary).map(row => row.key));
          state.gantt.expansionInitialized = true;
        } else if (action === "collapse-all") {
          state.gantt.expandedKeys = new Set();
          state.gantt.expansionInitialized = true;
        } else if (action === "toggle-row") {
          const key = actionTarget.dataset.ganttKey;
          if (state.gantt.expandedKeys.has(key)) state.gantt.expandedKeys.delete(key);
          else state.gantt.expandedKeys.add(key);
        } else if (action === "attention") {
          state.gantt.attentionOnly = !state.gantt.attentionOnly;
        } else if (action === "zoom-in" || action === "zoom-out") {
          const order = ["day", "week", "month"];
          const current = order.indexOf(state.gantt.zoom);
          const next = action === "zoom-in" ? Math.max(0, current - 1) : Math.min(order.length - 1, current + 1);
          state.gantt.zoom = order[next];
          state.gantt.fit = false;
        } else if (action === "fit-project") {
          state.gantt.fit = true;
        } else if (action === "critical-path") {
          state.gantt.criticalPath = !state.gantt.criticalPath;
        } else if (action === "dependencies") {
          state.gantt.showDependencies = !state.gantt.showDependencies;
        } else if (action === "record-execution") {
          const input = byId("execution-work-item");
          if (input) {
            input.value = actionTarget.dataset.ganttKey || "";
            const form = byId("execution-form");
            if (form) form.scrollIntoView({ behavior: "smooth", block: "start" });
            input.focus();
          }
        }
        if (action !== "record-execution") renderActiveView();
        return;
      }
      if (selectTarget) {
        state.gantt.selectedRowKey = selectTarget.dataset.ganttSelect || null;
        renderActiveView();
      }
    });
    shell.addEventListener("change", event => {
      const filter = event.target.closest("[data-gantt-filter]");
      if (!filter) return;
      const key = filter.dataset.ganttFilter;
      if (key === "phase") state.gantt.phaseFilter = filter.value;
      if (key === "execution") state.gantt.executionFilter = filter.value;
      if (key === "zoom") {
        state.gantt.zoom = filter.value;
        state.gantt.fit = false;
      }
      if (key === "critical-only") state.gantt.criticalOnly = filter.checked;
      if (key === "overdue") state.gantt.overdueOnly = filter.checked;
      if (key === "at-risk") state.gantt.atRiskOnly = filter.checked;
      renderActiveView();
    });
    return shell;
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
      state.activeView === "gantt" ? renderGantt(
        state.views.gantt,
        state.project.analysis || {},
        state.project.baseline || {},
        state.views.dependencyNetwork || {},
        state.views.cpm || {},
        state.project.project && state.project.project.id || state.project.id || "project") :
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

  document.querySelectorAll(".tab").forEach(tab => tab.addEventListener("click", () => activateView(tab.dataset.view)));
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
