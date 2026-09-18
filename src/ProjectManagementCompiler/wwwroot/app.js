(() => {
  "use strict";

  function createGanttState() {
    return {
      projectId: null,
      zoom: "week",
      fit: false,
      preset: "plan",
      expandedKeys: new Set(),
      expansionInitialized: false,
      phaseFilter: "ALL",
      executionFilter: "ALL",
      attentionOnly: false,
      criticalOnly: false,
      overdueOnly: false,
      atRiskOnly: false,
      lateStartOnly: false,
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

  function setSourceIntakeCollapsed(collapsed) {
    const panel = byId("source-intake-panel");
    const body = byId("source-intake-body");
    const toggle = byId("source-intake-toggle");
    if (!panel || !body || !toggle) return;
    panel.classList.toggle("is-collapsed", collapsed);
    body.hidden = collapsed;
    toggle.setAttribute("aria-expanded", collapsed ? "false" : "true");
    toggle.textContent = collapsed ? "Show setup" : "Hide setup";
  }

  function setExecutionPanelOpen(open, focusInput) {
    const panel = byId("execution-panel");
    const body = byId("execution-panel-body");
    const toggle = byId("execution-panel-toggle");
    if (!panel || !body || !toggle) return;
    panel.classList.toggle("is-open", open);
    body.hidden = !open;
    toggle.setAttribute("aria-expanded", open ? "true" : "false");
    toggle.textContent = open ? "Close updater" : "Open updater";
    if (open && focusInput) {
      const input = byId("execution-work-item");
      if (input) input.focus();
    }
  }

  function hasExecutionEvidence(summary) {
    const overallHealth = (summary && summary.analysis && summary.analysis.healthIndicators || []).find(indicator => indicator.id === "health.overall");
    if (overallHealth) return String(overallHealth.status || "").toUpperCase() === "KNOWN";
    return Boolean(summary && summary.views && summary.views.gantt && (summary.views.gantt.items || []).some(item =>
      (item.lanes || []).some(lane => String(lane.lane || "").toUpperCase() === "ACTUAL" && (lane.start || lane.finish || lane.effortHours !== null && lane.effortHours !== undefined))
    ));
  }

  function executionValue(summary, value) {
    return hasExecutionEvidence(summary) ? value : "—";
  }

  function displaySummaryLabel(label) {
    if (label === "Delivery cards completed") return "Recorded completion";
    if (label === "CPM calculated finish") return "CPM (dependency-only)";
    return label;
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

  function handleSummaryClick(event) {
    const target = event.target.closest("[data-summary-view]");
    if (!target) return;
    openSummaryView(target.dataset.summaryView, target.dataset.summaryKey || null, target.dataset.summaryAttention === "true");
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
    const healthLabel = overdue ? "Intervention needed" : attentionCount ? "Watch closely" : "No active alerts";
    const healthClass = overdue ? "danger" : attentionCount ? "warning" : "success";
    const completed = Number(dashboard.completed || 0);
    const totalCards = Number(dashboard.totalCards || 0);
    const progressKnown = hasExecutionEvidence(summary);
    const progress = progressKnown && totalCards ? Math.round((completed / totalCards) * 100) : null;
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
    heroMeta.appendChild(node("span", attentionCount ? attentionCount + " attention alerts" : "No active alerts", "summary-signal-count"));
    hero.appendChild(heroMeta);
    panel.appendChild(hero);

    const mainGrid = node("div", null, "summary-main-grid");
    const progressCard = node("section", null, "summary-progress-card");
    const progressHeading = node("div", null, "summary-card-heading");
    progressHeading.appendChild(node("span", "DELIVERY PROGRESS", "eyebrow"));
    progressHeading.appendChild(node("strong", progressKnown ? progress + "%" : "—", "summary-progress-value"));
    progressCard.appendChild(progressHeading);
    const progressTrack = node("div", null, "summary-progress-track");
    if (!progressKnown) progressTrack.classList.add("is-unknown");
    const progressFill = node("span");
    progressFill.style.width = progressKnown ? progress + "%" : "0%";
    progressTrack.appendChild(progressFill);
    progressCard.appendChild(progressTrack);
    progressCard.appendChild(node("p", progressKnown ? "Recorded completion · " + completed + " / " + totalCards + " delivery cards (" + progress + "% of delivery-card count)." : "Execution data unavailable · planning evidence only."));
    const progressStats = node("div", null, "summary-inline-stats");
    if (progressKnown) {
      progressStats.appendChild(node("span", String(dashboard.inProgress || 0) + " in progress"));
      progressStats.appendChild(node("span", String(dashboard.notStarted || 0) + " not started"));
    } else {
      progressStats.appendChild(node("span", "No execution overlay recorded"));
    }
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
    [["Work items", dashboard.totalCards, ""], ["Recorded completion", executionValue(summary, dashboard.completed), "success"], ["Schedule alerts", overdue + atRisk, overdue + atRisk ? "danger" : ""], ["Late starts", dashboard.lateToStart, "warning"]].forEach(([label, value, tone]) => {
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
    const baseline = summary && summary.baseline || {};
    const metrics = [
      ["Planned effort", baseline.plannedEffortHours === null || baseline.plannedEffortHours === undefined ? "UNKNOWN" : baseline.plannedEffortHours + "h", "accent"],
      ["Capacity", baseline.capacityHours === null || baseline.capacityHours === undefined ? "UNKNOWN" : baseline.capacityHours + "h", ""],
      ["Reserve", baseline.reserveHours === null || baseline.reserveHours === undefined ? "UNKNOWN" : baseline.reserveHours + "h", "success"],
      ["Execution evidence", hasExecutionEvidence(summary) ? "Recorded" : "Planning only", ""]
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
    [["Baseline finish", view.baselineFinish], ["CPM (dependency-only)", view.cpmFinish], ["Forecast finish", view.forecastFinish]].forEach(([label, value]) => {
      const row = node("div", null, "readout-row");
      row.appendChild(node("span", label, "muted"));
      row.appendChild(node("strong", value || "UNKNOWN"));
      schedule.appendChild(row);
    });
    dashboardGrid.appendChild(schedule);
    if (summary) dashboardGrid.appendChild(renderAttentionQueue(summary, true));
    fragment.appendChild(dashboardGrid);
    fragment.appendChild(node("h3", "Analysis summaries"));
    const summaryRows = (view.summaries || []).map(item => {
      if (item.label === "Delivery cards completed" && !hasExecutionEvidence(summary)) {
        return ["Recorded completion", "UNKNOWN", "UNKNOWN"];
      }
      return [displaySummaryLabel(item.label), item.value, item.state];
    });
    fragment.appendChild(renderTable(["View", "Value", "State"], summaryRows));
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
    if (value === null || value === undefined || value === "") return null;
    if (typeof value === "number") return Number.isFinite(value) ? value : null;
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

  function ganttStateLabel(row) {
    if (row && row.state) return stateLabel(row.state);
    return row && row.hasExecutionEvidence ? "Not recorded" : hasExecutionEvidence(currentSummary()) ? "Not recorded" : "Plan only";
  }

  function resolveWorkItemVariance(row, analysis) {
    if (!row || row.kind !== "DeliveryCard") return null;
    return (analysis && analysis.workItemVariances || []).find(item => item.workItemId === row.id) || null;
  }

  function resolveCpmRow(row, cpmView) {
    if (!row || !cpmView) return null;
    return (cpmView.rows || []).find(item => typedKey(item.nodeKind, item.nodeId) === row.key) || null;
  }

  function resolveAlertAnchor(alert, row, analysis) {
    const code = String(alert && alert.alertCode || "").toUpperCase();
    if (["START_DELAY", "OVERDUE", "AT_RISK", "SUSPENDED", "CANCELLED"].includes(code)) return analysis && analysis.asOfDate || null;
    if (["COMPLETED_LATE", "COMPLETED_ON_TIME"].includes(code)) return row && row.actual && row.actual.finish || null;
    return null;
  }

  function milestoneKindLabel(row) {
    if (!row || row.kind !== "Milestone") return "";
    return String(row.milestoneKind || "Milestone").toLowerCase() === "decision" ? "Decision gate" : "Milestone";
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
    return formatIsoWeek(timestamp);
  }

  function formatIsoWeek(timestamp) {
    const date = new Date(timestamp);
    date.setUTCHours(0, 0, 0, 0);
    date.setUTCDate(date.getUTCDate() + 4 - (date.getUTCDay() || 7));
    const yearStart = Date.UTC(date.getUTCFullYear(), 0, 1);
    const week = Math.ceil((((date.getTime() - yearStart) / DAY_MS) + 1) / 7);
    return "W" + String(week).padStart(2, "0");
  }

  function dateAt(timestamp, days) {
    return timestamp + days * DAY_MS;
  }

  function startOfIsoWeek(timestamp) {
    const date = new Date(timestamp);
    date.setUTCHours(0, 0, 0, 0);
    return dateAt(date.getTime(), -((date.getUTCDay() + 6) % 7));
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
        hasExecutionEvidence: Boolean(item && item.hasExecutionEvidence),
        roles: item && item.logicalRoles || [],
        dependencyIds: item && item.dependencyIds || milestone && milestone.dependencyIds || [],
        plan: { start: planStart, finish: planFinish },
        planStartOrigin: planStart ? "AUTHORED" : "UNKNOWN",
        planFinishOrigin: planFinish ? "AUTHORED" : "UNKNOWN",
        sourceReferences: kind === "DeliveryCard"
          ? item && item.sourceReferences || []
          : kind === "Milestone"
            ? milestone && milestone.sourceReferences || []
            : wbsNode.sourceReferences || [],
        milestoneKind: milestone && milestone.kind || null,
        isDerivedPlan: false,
        actual: actualLane ? {
          start: actualLane.start || null,
          finish: actualLane.finish || null,
          state: actualLane.state,
          isOpenEnded: Boolean(actualLane.isOpenEnded),
          actualEffortHours: actualLane.actualEffortHours,
          remainingEffortHours: actualLane.remainingEffortHours,
          lastUpdatedAt: actualLane.lastUpdatedAt
        } : null,
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

    function rollupSummaryPlans() {
      const byKey = new Map(rows.map(row => [row.key, row]));
      for (let index = rows.length - 1; index >= 0; index -= 1) {
        const row = rows[index];
        if (!row.isSummary || !row.childKeys.length) continue;
        const children = row.childKeys.map(key => byKey.get(key)).filter(Boolean);
        const starts = children.map(child => parseDate(child.plan.start)).filter(timestamp => timestamp !== null);
        const finishes = children.map(child => parseDate(child.plan.finish)).filter(timestamp => timestamp !== null);
        if (!starts.length || !finishes.length) continue;
        const derivedStart = new Date(Math.min(...starts)).toISOString().slice(0, 10);
        const derivedFinish = new Date(Math.max(...finishes)).toISOString().slice(0, 10);
        // Use derived summary dates only for missing edges; source-authored dates remain authoritative.
        if (!row.plan.start) {
          row.plan.start = derivedStart;
          row.planStartOrigin = "DERIVED";
        }
        if (!row.plan.finish) {
          row.plan.finish = derivedFinish;
          row.planFinishOrigin = "DERIVED";
        }
        row.isDerivedPlan = row.planStartOrigin === "DERIVED" || row.planFinishOrigin === "DERIVED";
      }
    }

    if (wbsRoot) addRow(wbsRoot, null, 0, null, null);
    rollupSummaryPlans();

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
        hasExecutionEvidence: Boolean(item.hasExecutionEvidence),
        roles: item.logicalRoles || [],
        dependencyIds: item.dependencyIds || [],
        plan: { start: planLane && planLane.start || null, finish: planLane && planLane.finish || null },
        planStartOrigin: planLane && planLane.start ? "AUTHORED" : "UNKNOWN",
        planFinishOrigin: planLane && planLane.finish ? "AUTHORED" : "UNKNOWN",
        sourceReferences: item.sourceReferences || [],
        milestoneKind: null,
        isDerivedPlan: false,
        actual: actualLane ? {
          start: actualLane.start || null,
          finish: actualLane.finish || null,
          state: actualLane.state,
          isOpenEnded: Boolean(actualLane.isOpenEnded),
          actualEffortHours: actualLane.actualEffortHours,
          remainingEffortHours: actualLane.remainingEffortHours,
          lastUpdatedAt: actualLane.lastUpdatedAt
        } : null,
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
      row.hasExecutionEvidence = row.hasExecutionEvidence || children.some(child => child.hasExecutionEvidence);
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
    let minor = [];
    const first = new Date(range.timelineStart);
    first.setUTCHours(0, 0, 0, 0);
    const month = new Date(Date.UTC(first.getUTCFullYear(), first.getUTCMonth(), 1));
    for (let cursor = month.getTime(); cursor <= range.timelineEnd + DAY_MS; cursor = Date.UTC(new Date(cursor).getUTCFullYear(), new Date(cursor).getUTCMonth() + 1, 1)) {
      major.push(cursor);
      if (major.length > 240) break;
    }
    if (zoom === "month") {
      minor = [];
    } else if (zoom === "week") {
      for (let cursor = startOfIsoWeek(range.timelineStart); cursor <= range.timelineEnd + DAY_MS; cursor = dateAt(cursor, 7)) minor.push(cursor);
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
    const marker = createMarker(analysis && analysis.asOfDate, range, "gantt-as-of-marker", "AS OF " + formatDay(parseDate(analysis && analysis.asOfDate)), true);
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
    const isOpenActual = isActual && entry.finish == null && row.state === "IN_PROGRESS";
    const displayFinish = isOpenActual ? analysis.asOfDate : entry.finish || entry.start;
    const finish = parseDate(displayFinish);
    const end = finish === null ? start + DAY_MS : finish + DAY_MS;
    const criticalClass = state.gantt.criticalPath && row.isCritical ? " gantt-critical-bar" : "";
    const openClass = isOpenActual ? " gantt-open-bar" : "";
    const bar = node("button", null, "gantt-bar " + className + criticalClass + openClass);
    bar.type = "button";
    bar.dataset.ganttSelect = row.key;
    bar.dataset.ganttLane = isActual ? "ACTUAL" : "PLAN";
    bar.style.left = datePercent(start, range) + "%";
    bar.style.width = Math.max(0.8, ((end - start) / range.span) * 100) + "%";
    const rangeLabel = isOpenActual
      ? label + " from " + formatDate(entry.start) + " through AS OF " + formatDate(analysis.asOfDate)
      : label + " " + formatDate(entry.start) + " to " + formatDate(finish);
    bar.setAttribute("aria-label", rangeLabel);
    bar.title = rangeLabel;
    return bar;
  }

  function createActualFinishMarker(row, entry, range) {
    const finish = parseDate(entry && entry.finish);
    if (finish === null) return null;
    const marker = node("button", null, "gantt-actual-finish-marker");
    marker.type = "button";
    marker.dataset.ganttSelect = row.key;
    marker.dataset.ganttLane = "ACTUAL";
    marker.style.left = datePercent(finish, range) + "%";
    const label = "ACTUAL finish recorded " + formatDate(entry.finish);
    marker.setAttribute("aria-label", label);
    marker.title = label;
    return marker;
  }

  function renderTimelineRow(row, range, analysis) {
    const timelineRow = node("div", null, "gantt-timeline-row" + (row.isSummary ? " gantt-summary-row" : "") + (state.gantt.criticalPath && row.isCritical ? " gantt-critical-row" : ""));
    timelineRow.dataset.rowKey = row.key;
    const planLane = node("div", null, "gantt-sub-lane gantt-plan-lane");
    const actualLane = node("div", null, "gantt-sub-lane gantt-actual-lane");
    if (row.isDerivedPlan) planLane.classList.add("is-derived");
    const planLabel = row.isMilestone ? "PLAN milestone" : "PLAN";
    if (row.isMilestone && row.plan.start) {
      const milestoneLabel = milestoneKindLabel(row);
      const milestone = node("button", null, "gantt-milestone" + (state.gantt.criticalPath && row.isCritical ? " gantt-critical-bar" : ""));
      milestone.type = "button";
      milestone.dataset.ganttSelect = row.key;
      milestone.style.left = datePercent(parseDate(row.plan.start), range) + "%";
      milestone.setAttribute("aria-label", milestoneLabel + " " + row.id + " on " + formatDate(row.plan.start));
      milestone.title = milestoneLabel + ": " + formatDate(row.plan.start);
      planLane.appendChild(milestone);
    } else if (row.plan.start) {
      const plan = createBar(row, row.plan, range, "gantt-plan-bar", planLabel, analysis || {}, false);
      if (plan) planLane.appendChild(plan);
    }
    if (row.actual) {
      const actual = createBar(row, row.actual, range, "gantt-actual-bar", "ACTUAL", analysis || {}, true);
      if (actual) actualLane.appendChild(actual);
      else if (row.actual.finish) {
        const completion = createActualFinishMarker(row, row.actual, range);
        if (completion) actualLane.appendChild(completion);
      } else if (row.actual.actualEffortHours !== null && row.actual.actualEffortHours !== undefined
        || row.actual.remainingEffortHours !== null && row.actual.remainingEffortHours !== undefined) {
        const evidence = node("span", "Evidence", "gantt-actual-evidence");
        evidence.setAttribute("aria-label", "Actual evidence recorded without a supported actual date");
        actualLane.appendChild(evidence);
      }
    } else if (!row.isSummary && !row.isMilestone) {
      const empty = node("span", "—", "gantt-actual-empty");
      empty.setAttribute("aria-label", "No actual evidence");
      actualLane.appendChild(empty);
    }
    timelineRow.appendChild(planLane);
    timelineRow.appendChild(actualLane);
    row.alerts.forEach(alert => {
      const anchorDate = resolveAlertAnchor(alert, row, analysis);
      const marker = parseDate(anchorDate) === null ? null : node("button", null, "gantt-alert-marker");
      if (marker) {
        marker.type = "button";
        marker.style.left = datePercent(parseDate(anchorDate), range) + "%";
        marker.dataset.ganttSelect = row.key;
        const label = (alert.alertCode || "ALERT") + ": " + ganttAlertText(alert);
        marker.setAttribute("aria-label", label);
        marker.title = label;
        timelineRow.appendChild(marker);
      }
    });
    return timelineRow;
  }

  function createDependencySvg(visibleRows, dependencyView, range, width, selectedRowKey) {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("class", "gantt-connectors");
    svg.setAttribute("aria-hidden", "true");
    const rowHeight = 40;
    const height = visibleRows.length * rowHeight;
    svg.setAttribute("width", String(width));
    svg.setAttribute("height", String(height));
    svg.setAttribute("viewBox", "0 0 " + width + " " + height);
    const defs = document.createElementNS("http://www.w3.org/2000/svg", "defs");
    [
      ["gantt-dependency-arrow", "gantt-dependency-arrow"],
      ["gantt-dependency-arrow-related", "gantt-dependency-arrow-related"]
    ].forEach(([id, className]) => {
      const marker = document.createElementNS("http://www.w3.org/2000/svg", "marker");
      marker.setAttribute("id", id);
      marker.setAttribute("viewBox", "0 0 10 10");
      marker.setAttribute("refX", "9");
      marker.setAttribute("refY", "5");
      marker.setAttribute("markerWidth", "5");
      marker.setAttribute("markerHeight", "5");
      marker.setAttribute("orient", "auto-start-reverse");
      const arrow = document.createElementNS("http://www.w3.org/2000/svg", "path");
      arrow.setAttribute("d", "M 0 0 L 10 5 L 0 10 z");
      arrow.setAttribute("class", className);
      marker.appendChild(arrow);
      defs.appendChild(marker);
    });
    svg.appendChild(defs);
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
      path.setAttribute("marker-end", "url(#" + (isRelated ? "gantt-dependency-arrow-related" : "gantt-dependency-arrow") + ")");
      svg.appendChild(path);
    });
    return svg;
  }

  function renderTaskRow(row, analysis) {
    const taskRow = node("div", null, "gantt-task-row" + (row.isSummary ? " gantt-summary-row" : "") + (state.gantt.criticalPath && row.isCritical ? " gantt-critical-row" : ""));
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
    select.appendChild(node("span", row.kind === "Milestone" ? milestoneKindLabel(row) : row.kind, "gantt-row-kind"));
    const context = [];
    if (row.phaseId && row.kind !== "Phase") context.push("Phase " + row.phaseId);
    if (row.workPackageId && row.kind !== "WorkPackage") context.push("WP " + row.workPackageId);
    if (context.length) select.appendChild(node("span", context.join(" · "), "gantt-row-context"));
    select.setAttribute("aria-label", (row.kind === "Milestone" ? milestoneKindLabel(row) : row.kind) + " " + row.id + ": " + row.name);
    identity.appendChild(select);
    taskRow.appendChild(identity);

    const status = node("div", null, "gantt-task-cell gantt-task-status");
    status.appendChild(node("span", ganttStateLabel(row), "gantt-state" + (row.state ? "" : " is-neutral")));
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
    if (state.gantt.executionFilter === "WITH_EVIDENCE" && !row.hasExecutionEvidence) return false;
    if (state.gantt.executionFilter !== "ALL" && state.gantt.executionFilter !== "WITH_EVIDENCE" && String(row.state || "UNKNOWN") !== state.gantt.executionFilter) return false;
    if (state.gantt.attentionOnly && !row.alerts.some(isAttentionAlert)) return false;
    if (state.gantt.criticalOnly && !row.isCritical) return false;
    if (state.gantt.overdueOnly && !row.alerts.some(alert => alert.alertCode === "OVERDUE")) return false;
    if (state.gantt.atRiskOnly && !row.alerts.some(alert => alert.alertCode === "AT_RISK")) return false;
    if (state.gantt.lateStartOnly && !row.alerts.some(alert => alert.alertCode === "START_DELAY")) return false;
    return true;
  }

  function visibleGanttRows(rows, byKey) {
    const matches = new Map(rows.map(row => [row.key, rowMatchesFilters(row)]));
    const filterActive = state.gantt.phaseFilter !== "ALL" || state.gantt.executionFilter !== "ALL" || state.gantt.attentionOnly || state.gantt.criticalOnly || state.gantt.overdueOnly || state.gantt.atRiskOnly || state.gantt.lateStartOnly;
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
        if (parent.isSummary && !state.gantt.expandedKeys.has(parent.key) && !filterActive) return false;
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

  function applyGanttPreset(preset) {
    state.gantt.preset = preset;
    state.gantt.phaseFilter = "ALL";
    state.gantt.executionFilter = "ALL";
    state.gantt.attentionOnly = false;
    state.gantt.criticalOnly = false;
    state.gantt.overdueOnly = false;
    state.gantt.atRiskOnly = false;
    state.gantt.lateStartOnly = false;
    state.gantt.showDependencies = false;
    state.gantt.criticalPath = false;
    state.gantt.zoom = "week";
    state.gantt.fit = false;
    if (preset === "execution") state.gantt.executionFilter = "WITH_EVIDENCE";
    if (preset === "risks") state.gantt.attentionOnly = true;
    if (preset === "dependencies") state.gantt.showDependencies = true;
    if (preset === "critical-path") state.gantt.criticalPath = true;
  }

  function renderGanttToolbar(rows, visibleRows, range, analysis) {
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
      if (action === "zoom-out") button.setAttribute("aria-label", "Zoom out");
      if (action === "zoom-in") button.setAttribute("aria-label", "Zoom in");
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

    const presets = node("div", null, "gantt-preset-nav");
    presets.appendChild(node("span", "View", "gantt-preset-label"));
    [["plan", "Plan"], ["execution", "Execution"], ["risks", "Risks"], ["dependencies", "Dependencies"], ["critical-path", "Critical path"]].forEach(([value, label]) => {
      const button = node("button", label, "secondary gantt-preset" + (state.gantt.preset === value ? " is-active" : ""));
      button.type = "button";
      button.dataset.ganttPreset = value;
      button.setAttribute("aria-pressed", state.gantt.preset === value ? "true" : "false");
      presets.appendChild(button);
    });
    toolbar.appendChild(presets);

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
    [["ALL", "All execution states"], ["WITH_EVIDENCE", "Execution evidence"], ["NOT_STARTED", "Not started"], ["IN_PROGRESS", "In progress"], ["COMPLETED", "Completed"], ["SUSPENDED", "Suspended"], ["CANCELLED", "Cancelled"], ["UNKNOWN", "No state"]].forEach(([value, label]) => {
      const option = node("option", label);
      option.value = value;
      execution.appendChild(option);
    });
    execution.value = state.gantt.executionFilter;
    filters.appendChild(execution);

    [["critical-only", "Critical only", state.gantt.criticalOnly], ["overdue", "Overdue", state.gantt.overdueOnly], ["at-risk", "At risk", state.gantt.atRiskOnly], ["late-start", "Late start", state.gantt.lateStartOnly]].forEach(([value, label, checked]) => {
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

    const advancedFilters = node("details", null, "gantt-filter-disclosure");
    advancedFilters.open = state.gantt.phaseFilter !== "ALL" || state.gantt.executionFilter !== "ALL" || state.gantt.criticalOnly || state.gantt.overdueOnly || state.gantt.atRiskOnly || state.gantt.lateStartOnly;
    advancedFilters.appendChild(node("summary", "Advanced filters", "gantt-filter-summary"));
    advancedFilters.appendChild(filters);
    toolbar.appendChild(advancedFilters);

    const presetLabels = { plan: "Plan", execution: "Execution", risks: "Risks", dependencies: "Dependencies", "critical-path": "Critical path" };
    const presetLabel = presetLabels[state.gantt.preset] || "Custom view";
    const modeLabels = [];
    if (state.gantt.phaseFilter !== "ALL") modeLabels.push("Phase " + state.gantt.phaseFilter);
    if (state.gantt.executionFilter === "WITH_EVIDENCE") modeLabels.push("execution evidence");
    else if (state.gantt.executionFilter !== "ALL") modeLabels.push(stateLabel(state.gantt.executionFilter));
    if (state.gantt.criticalPath) modeLabels.push("CPM overlay on");
    if (state.gantt.showDependencies) modeLabels.push("connectors on");
    if (state.gantt.attentionOnly) modeLabels.push("attention only");
    const modeSuffix = modeLabels.length ? " · " + modeLabels.join(" · ") : "";
    const status = node("div", presetLabel + " · " + visibleRows.length + " visible / " + rows.length + " rows" + modeSuffix + " · As of " + formatDate(analysis && analysis.asOfDate), "gantt-toolbar-status muted");
    toolbar.appendChild(status);
    return toolbar;
  }

  function appendDetailField(parent, label, value) {
    const field = node("div", null, "gantt-detail-field");
    field.appendChild(node("span", label, "muted"));
    field.appendChild(node("strong", value));
    parent.appendChild(field);
  }

  function appendDetailSection(parent, title, className) {
    const section = node("section", null, "gantt-detail-section" + (className ? " " + className : ""));
    section.appendChild(node("h4", title));
    parent.appendChild(section);
    return section;
  }

  function displayDate(value) {
    return value ? formatDate(value) : "UNKNOWN";
  }

  function displayOptionalNumber(value, suffix) {
    return value === null || value === undefined ? "UNKNOWN" : String(value) + (suffix || "");
  }

  function displaySourceFile(value) {
    const text = String(value || "");
    if (!text || /^[A-Za-z]:[\\/]/.test(text) || text.startsWith("/") || text.includes("..")) return "UNKNOWN";
    return text;
  }

  function renderSourceEvidence(parent, row) {
    const section = appendDetailSection(parent, "SOURCE EVIDENCE", "gantt-detail-source");
    const references = row.sourceReferences || [];
    if (!references.length) {
      section.appendChild(node("p", "UNKNOWN · no safe item-level source reference.", "muted"));
      return;
    }
    references.forEach(reference => {
      const evidence = node("article", null, "gantt-source-evidence");
      evidence.appendChild(node("strong", reference.sourceId || "UNKNOWN"));
      const details = node("div", null, "gantt-source-evidence-grid");
      [["Relative file", displaySourceFile(reference.relativeFile)], ["Section", reference.section || "UNKNOWN"], ["Table", reference.table || "UNKNOWN"], ["Item", reference.item || "UNKNOWN"], ["Extraction rule", reference.extractionRule || "UNKNOWN"], ["Authority", reference.authorityRank === null || reference.authorityRank === undefined ? "UNKNOWN" : "Rank " + reference.authorityRank], ["Confidence", reference.confidenceState || "UNKNOWN"], ["Validation", reference.validationState || "UNKNOWN"]].forEach(([label, value]) => appendDetailField(details, label, value));
      evidence.appendChild(details);
      section.appendChild(evidence);
    });
  }

  function renderGanttDetail(row, analysis, dependencyView, cpmView) {
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
    heading.appendChild(node("span", (row.kind === "Milestone" ? milestoneKindLabel(row) : row.kind) + " · " + row.id, "eyebrow"));
    heading.appendChild(node("h3", row.name));
    panel.appendChild(heading);
    const identity = appendDetailSection(panel, "IDENTITY");
    const identityFields = node("div", null, "gantt-detail-fields");
    appendDetailField(identityFields, "Kind", row.kind === "Milestone" ? milestoneKindLabel(row) : row.kind);
    appendDetailField(identityFields, "ID", row.id);
    appendDetailField(identityFields, "Name", row.name);
    appendDetailField(identityFields, "Phase", row.phaseId || "UNKNOWN");
    appendDetailField(identityFields, "Work package", row.workPackageId || "UNKNOWN");
    identity.appendChild(identityFields);

    const plan = appendDetailSection(panel, "PLAN / SOURCE");
    const planFields = node("div", null, "gantt-detail-fields");
    appendDetailField(planFields, "Plan start", displayDate(row.plan.start) + " · " + row.planStartOrigin);
    appendDetailField(planFields, "Plan finish", displayDate(row.plan.finish) + " · " + row.planFinishOrigin);
    appendDetailField(planFields, "Source state", row.sourceReferences && row.sourceReferences.length ? "Known · " + row.sourceReferences.length + " reference(s)" : "UNKNOWN");
    appendDetailField(planFields, "Baseline", "Immutable");
    plan.appendChild(planFields);
    renderSourceEvidence(plan, row);

    const actualSection = appendDetailSection(panel, "ACTUAL");
    const actualFields = node("div", null, "gantt-detail-fields");
    appendDetailField(actualFields, "State", row.state ? stateLabel(row.state) : "UNKNOWN");
    appendDetailField(actualFields, "Actual start", row.actual ? displayDate(row.actual.start) : "No actual evidence");
    appendDetailField(actualFields, "Actual finish", row.actual && row.actual.isOpenEnded ? "UNKNOWN · open through as-of" : row.actual ? displayDate(row.actual.finish) : "No actual evidence");
    appendDetailField(actualFields, "Actual effort", row.actual ? displayOptionalNumber(row.actual.actualEffortHours, "h") : "No actual evidence");
    appendDetailField(actualFields, "Remaining effort", row.actual ? displayOptionalNumber(row.actual.remainingEffortHours, "h") : "No actual evidence");
    appendDetailField(actualFields, "Last update", row.actual && row.actual.lastUpdatedAt ? formatDate(row.actual.lastUpdatedAt) : "UNKNOWN");
    actualSection.appendChild(actualFields);

    const analysisSection = appendDetailSection(panel, "ANALYSIS");
    const analysisFields = node("div", null, "gantt-detail-fields");
    const variance = resolveWorkItemVariance(row, analysis);
    appendDetailField(analysisFields, "As-of date", displayDate(analysis && analysis.asOfDate));
    appendDetailField(analysisFields, "Start variance", variance && variance.startVarianceWorkingMinutes !== null && variance.startVarianceWorkingMinutes !== undefined ? variance.startVarianceWorkingMinutes + " working minutes" : "UNKNOWN");
    appendDetailField(analysisFields, "Finish variance", variance && variance.finishVarianceWorkingMinutes !== null && variance.finishVarianceWorkingMinutes !== undefined ? variance.finishVarianceWorkingMinutes + " working minutes" : "UNKNOWN");
    appendDetailField(analysisFields, "Logical roles", row.roles.length ? row.roles.join(", ") : "UNKNOWN");
    analysisSection.appendChild(analysisFields);

    const cpmSection = appendDetailSection(panel, "CALCULATED · DEPENDENCY CPM");
    const cpmFields = node("div", null, "gantt-detail-fields");
    const cpm = resolveCpmRow(row, cpmView);
    appendDetailField(cpmFields, "Calculated start", cpm ? displayDate(cpm.calculatedStart) : "UNKNOWN");
    appendDetailField(cpmFields, "Calculated finish", cpm ? displayDate(cpm.calculatedFinish) : "UNKNOWN");
    appendDetailField(cpmFields, "Float", cpm ? displayOptionalNumber(cpm.floatWorkingMinutes, " working minutes") : "UNKNOWN");
    appendDetailField(cpmFields, "Critical", cpm ? (cpm.isCritical ? "Yes" : "No") : "UNKNOWN");
    cpmSection.appendChild(cpmFields);

    const dependencies = appendDetailSection(panel, "DEPENDENCIES");
    const edges = (dependencyView && dependencyView.edges || []).filter(edge => edge.includedInAnalysis !== false && ((edge.subjectKey || typedKey(edge.subjectKind, edge.subjectId)) === row.key || (edge.predecessorKey || typedKey(edge.predecessorKind, edge.predecessorId)) === row.key));
    const blockedBy = edges.filter(edge => (edge.subjectKey || typedKey(edge.subjectKind, edge.subjectId)) === row.key);
    const blocking = edges.filter(edge => (edge.predecessorKey || typedKey(edge.predecessorKind, edge.predecessorId)) === row.key);
    const dependencyFields = node("div", null, "gantt-detail-fields");
    appendDetailField(dependencyFields, "Blocked by", blockedBy.length ? blockedBy.map(edge => edge.predecessorKey || typedKey(edge.predecessorKind, edge.predecessorId)).join(", ") : "UNKNOWN");
    appendDetailField(dependencyFields, "Blocking", blocking.length ? blocking.map(edge => edge.subjectKey || typedKey(edge.subjectKind, edge.subjectId)).join(", ") : "UNKNOWN");
    dependencies.appendChild(dependencyFields);
    edges.forEach(edge => {
      const subjectKey = edge.subjectKey || typedKey(edge.subjectKind, edge.subjectId);
      const predecessorKey = edge.predecessorKey || typedKey(edge.predecessorKind, edge.predecessorId);
      dependencies.appendChild(node("p", predecessorKey + " → " + subjectKey + " · " + (edge.dependencyType || ""), "gantt-detail-dependency"));
    });

    const alerts = appendDetailSection(panel, "ALERTS");
    if (!row.alerts.length) alerts.appendChild(node("p", "No active alerts.", "muted"));
    row.alerts.forEach(alert => {
      const item = node("p", null, "gantt-detail-alert");
      item.appendChild(node("strong", alert.alertCode || "ALERT"));
      item.appendChild(document.createTextNode(" " + ganttAlertText(alert)));
      if (alert.reasonWorkItemIds && alert.reasonWorkItemIds.length) item.appendChild(node("span", " Predecessor reason IDs: " + alert.reasonWorkItemIds.join(", "), "muted"));
      alerts.appendChild(item);
    });

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
    shell.appendChild(renderGanttToolbar(rows, visibleRows, range, analysis || {}));

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
    timeline.style.height = Math.max(40, visibleRows.length * 40) + "px";
    timeline.appendChild(renderTimelineBackground(range, width));
    const bodyMarker = createMarker(analysis && analysis.asOfDate, range, "gantt-as-of-marker", "AS OF " + formatDay(parseDate(analysis && analysis.asOfDate)), false);
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
    const workspace = node("div", null, "gantt-workspace");
    workspace.appendChild(scroll);
    const inspectorColumn = node("div", null, "gantt-inspector-column");
    inspectorColumn.appendChild(renderGanttDetail(selectedRow, analysis || {}, dependencyView, cpmView));
    workspace.appendChild(inspectorColumn);
    shell.appendChild(workspace);

    shell.addEventListener("click", event => {
      const presetTarget = event.target.closest("[data-gantt-preset]");
      const actionTarget = event.target.closest("[data-gantt-action]");
      const selectTarget = event.target.closest("[data-gantt-select]");
      if (presetTarget) {
        applyGanttPreset(presetTarget.dataset.ganttPreset);
        renderActiveView();
        return;
      }
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
            const executionPanel = byId("execution-panel");
            if (executionPanel) executionPanel.classList.add("is-targeted");
            setExecutionPanelOpen(true, false);
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
      if (key === "late-start") state.gantt.lateStartOnly = filter.checked;
      renderActiveView();
    });
    return shell;
  }

  function ganttAlertText(alert) {
    return alert.message || alert.label || alert.reason || "Derived schedule condition";
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
    setSourceIntakeCollapsed(true);
    setExecutionPanelOpen(false, false);
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
      setSourceIntakeCollapsed(false);
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
      setSourceIntakeCollapsed(false);
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
  byId("summary-panel").addEventListener("click", handleSummaryClick);
  byId("source-intake-toggle").addEventListener("click", () => {
    const panel = byId("source-intake-panel");
    setSourceIntakeCollapsed(!panel.classList.contains("is-collapsed"));
  });
  byId("execution-panel-toggle").addEventListener("click", () => {
    const body = byId("execution-panel-body");
    setExecutionPanelOpen(Boolean(body && body.hidden), false);
  });
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
