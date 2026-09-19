using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed record ManagementMetricsResult
{
    public CapacityAnalysis Capacity { get; init; } = new();
    public ReserveAnalysis Reserve { get; init; } = new();
    public EffortAccountingReconciliation EffortAccounting { get; init; } = new();
    public ExecutionEffortSummary ExecutionEffort { get; init; } = new();
    public DataState ForecastState { get; init; } = DataState.Unknown;
    public DateOnly? ForecastFinish { get; init; }
    public IReadOnlyList<HealthIndicator> HealthIndicators { get; init; } = Array.Empty<HealthIndicator>();
    public IReadOnlyList<ViewSummary> ViewSummaries { get; init; } = Array.Empty<ViewSummary>();
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}

public sealed class ManagementMetricsAnalyzer
{
    public ManagementMetricsResult Analyze(CanonicalProject project, DependencyAnalysisResult dependency)
    {
        var diagnostics = new List<ImportWarning>();
        var workPackageEfforts = project.WorkPackages.Select(workPackage => workPackage.PlannedEffortHours).ToArray();
        var authoritativeEffort = project.Baseline.PlannedEffortHours;
        var workPackageLoad = workPackageEfforts.All(value => value is not null)
            ? workPackageEfforts.Sum(value => value ?? 0m)
            : (decimal?)null;
        var capacityHours = project.Baseline.CapacityHours ?? project.Capacity.CapacityHours;
        var capacityState = workPackageLoad is not null && capacityHours is not null
            ? DataState.Calculated
            : DataState.Unknown;
        if (authoritativeEffort is not null && workPackageLoad is not null && authoritativeEffort != workPackageLoad)
        {
            diagnostics.Add(new ImportWarning
            {
                Id = "WORK_PACKAGE_EFFORT_RECONCILIATION",
                Severity = WarningSeverity.Warning,
                Code = "WORK_PACKAGE_EFFORT_RECONCILIATION",
                Message = $"Work-package effort {workPackageLoad} hours differs from authoritative baseline effort {authoritativeEffort} hours; the baseline remains authoritative."
            });
        }

        var detailedEffort = project.DeliveryCards
            .Select(card => card.PlannedEffortHours)
            .All(value => value is not null)
            ? project.DeliveryCards.Sum(card => card.PlannedEffortHours ?? 0m)
            : (decimal?)null;
        var effortAccounting = new EffortAccountingReconciliation
        {
            AccountingLevel = "WORK_PACKAGE_AUTHORITATIVE_CARD_DETAIL_RECONCILIATION",
            AuthoritativeEffortHours = authoritativeEffort,
            DetailedEffortHours = detailedEffort,
            DifferenceHours = authoritativeEffort is not null && detailedEffort is not null
                ? authoritativeEffort - detailedEffort
                : null,
            State = authoritativeEffort is not null && detailedEffort is not null
                ? DataState.Calculated
                : DataState.Unknown,
            ReconciliationWarnings = authoritativeEffort is not null && detailedEffort is not null && authoritativeEffort != detailedEffort
                ? ["Delivery-card detail is not the authoritative load and must not replace work-package effort."]
                : Array.Empty<string>()
        };

        var recordedExecution = ExecutionTruthResolver.Recorded(project);
        var executionEffort = AnalyzeExecutionEffort(recordedExecution);
        var hasExecutionEvidence = project.ImportMetadata is not null
            ? recordedExecution.Count > 0
            : recordedExecution.Any(record =>
                record.ActualEffortHours is not null || record.RemainingEffortHours is not null || record.ActualStart is not null || record.ActualFinish is not null);
        // MVP1 deliberately does not convert a CPM result into a forecast. A
        // forecast needs an explicit remaining-work/resource rule; actual start
        // evidence alone is not enough. Keep this distinct from CalculatedFinish.
        var forecastState = DataState.Unknown;
        var health = new HealthIndicator
        {
            Id = "health.overall",
            RuleId = "HEALTH_REQUIRES_EXECUTION_EVIDENCE",
            Name = "Overall delivery health",
            Status = hasExecutionEvidence ? "KNOWN" : "UNKNOWN",
            Detail = hasExecutionEvidence
                ? "Health is derived from captured execution evidence and baseline analysis."
                : "Planning-only input has no actual execution evidence; health is not inferred."
        };

        var views = new List<ViewSummary>
        {
            new()
            {
                ViewId = "dashboard.capacity",
                Label = "Authoritative work-package load",
                Value = workPackageLoad is null || capacityHours is null ? "UNKNOWN" : $"{workPackageLoad:0.##}h/{capacityHours:0.##}h",
                State = capacityState
            },
            new()
            {
                ViewId = "dashboard.planned-effort",
                Label = "Planned effort",
                Value = FormatHours(authoritativeEffort),
                State = authoritativeEffort is null ? DataState.Unknown : DataState.Known
            },
            new()
            {
                ViewId = "dashboard.capacity-hours",
                Label = "Capacity",
                Value = FormatHours(capacityHours),
                State = capacityHours is null ? DataState.Unknown : DataState.Known
            },
            new()
            {
                ViewId = "dashboard.reserve",
                Label = "Reserve remaining",
                Value = project.Reserve.RemainingHours is null ? "UNKNOWN" : $"{project.Reserve.RemainingHours:0.##}h",
                State = project.Reserve.RemainingState
            },
            new()
            {
                ViewId = "dashboard.reserve-initial",
                Label = "Initial reserve",
                Value = FormatHours(project.Reserve.InitialHours),
                State = project.Reserve.InitialHours is null ? DataState.Unknown : DataState.Known
            },
            new()
            {
                ViewId = "dashboard.actual-effort",
                Label = "Actual effort",
                Value = FormatHours(executionEffort.ActualEffortHours),
                State = executionEffort.ActualState
            },
            new()
            {
                ViewId = "dashboard.remaining-effort",
                Label = "Remaining effort",
                Value = FormatHours(executionEffort.RemainingEffortHours),
                State = executionEffort.RemainingState
            },
            new()
            {
                ViewId = "dashboard.health",
                Label = "Overall delivery health",
                Value = health.Status,
                State = health.Status == "UNKNOWN" ? DataState.Unknown : DataState.Calculated
            },
            new()
            {
                ViewId = "dashboard.baseline-finish",
                Label = "Baseline finish",
                Value = FormatDate(project.Baseline.PlanningFinish),
                State = project.Baseline.PlanningFinish is null ? DataState.Unknown : DataState.Known
            },
            new()
            {
                ViewId = "dashboard.cpm-finish",
                Label = "CPM calculated finish",
                Value = FormatDate(dependency.CalculatedFinish),
                State = dependency.CalculatedFinish is null ? DataState.Unknown : DataState.Calculated
            },
            new()
            {
                ViewId = "dashboard.forecast-finish",
                Label = "Execution forecast finish",
                Value = FormatDate(null),
                State = forecastState
            }
        };

        return new ManagementMetricsResult
        {
            Capacity = new CapacityAnalysis
            {
                PlannedEffortHours = authoritativeEffort,
                CapacityHours = capacityHours,
                LoadHours = workPackageLoad,
                State = capacityState,
                Diagnostics = diagnostics
            },
            Reserve = new ReserveAnalysis
            {
                InitialHours = project.Reserve.InitialHours,
                ConsumedHours = project.Reserve.ConsumedHours,
                RemainingHours = project.Reserve.RemainingHours,
                ConsumptionState = project.Reserve.ConsumptionState,
                RemainingState = project.Reserve.RemainingState
            },
            EffortAccounting = effortAccounting,
            ExecutionEffort = executionEffort,
            ForecastState = forecastState,
            ForecastFinish = null,
            HealthIndicators = [health],
            ViewSummaries = views,
            Diagnostics = diagnostics
        };
    }

    private static ExecutionEffortSummary AnalyzeExecutionEffort(IReadOnlyList<EffectiveExecutionRecord> records)
    {
        var actualValues = records.Where(record => record.ActualEffortHours is not null).Select(record => record.ActualEffortHours!.Value).ToArray();
        var remainingValues = records.Where(record => record.RemainingEffortHours is not null).Select(record => record.RemainingEffortHours!.Value).ToArray();
        return new ExecutionEffortSummary
        {
            ActualEffortHours = actualValues.Length == 0 ? null : actualValues.Sum(),
            ActualState = actualValues.Length == 0 ? DataState.Unknown : DataState.Calculated,
            RemainingEffortHours = remainingValues.Length == 0 ? null : remainingValues.Sum(),
            RemainingState = remainingValues.Length == 0 ? DataState.Unknown : DataState.Calculated
        };
    }

    private static string FormatDate(DateOnly? value) => value?.ToString("yyyy-MM-dd") ?? "UNKNOWN";
    private static string FormatHours(decimal? value) => value is null ? "UNKNOWN" : $"{value:0.##}h";
}
