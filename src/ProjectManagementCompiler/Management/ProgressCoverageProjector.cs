using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public enum ProgressCoverageState
{
    Unknown,
    Partial,
    Complete
}

public sealed record ProgressCoverageSummary
{
    public int? RecordedPercent { get; init; }
    public bool CoverageComplete { get; init; }
    public int EligibleCardCount { get; init; }
    public int TotalCardCount { get; init; }
    public ProgressCoverageState CoverageState { get; init; } = ProgressCoverageState.Unknown;
    public decimal? ActualEffortHours { get; init; }
    public decimal? RemainingEffortHours { get; init; }
    public int CompletedCardCount { get; init; }
}

public sealed class ProgressCoverageProjector
{
    public ProgressCoverageSummary Build(CanonicalProject project, ManagementAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(analysis);

        var total = project.DeliveryCards.Count;
        var eligible = project.DeliveryCards.Count(card => IsEligible(ExecutionTruthResolver.ForCard(project, card.Id)));
        var actual = analysis.ExecutionEffort.ActualEffortHours;
        var remaining = analysis.ExecutionEffort.RemainingEffortHours;
        var coverageComplete = total > 0 && eligible == total;
        var effortKnown = actual is not null || remaining is not null;
        var aggregateEligible = actual is >= 0m
            && remaining is >= 0m
            && actual.Value + remaining.Value > 0m;

        return new ProgressCoverageSummary
        {
            RecordedPercent = coverageComplete && aggregateEligible
                ? RoundPercent(actual!.Value, remaining!.Value)
                : null,
            CoverageComplete = coverageComplete,
            EligibleCardCount = eligible,
            TotalCardCount = total,
            CoverageState = coverageComplete
                ? ProgressCoverageState.Complete
                : effortKnown ? ProgressCoverageState.Partial : ProgressCoverageState.Unknown,
            ActualEffortHours = actual,
            RemainingEffortHours = remaining,
            CompletedCardCount = Math.Max(0, analysis.ExecutionStatus.Completed)
        };
    }

    public static bool IsEligible(EffectiveExecutionRecord? record) =>
        record?.IsRecorded == true
        && record.ActualEffortHours is >= 0m
        && record.RemainingEffortHours is >= 0m
        && record.ActualEffortHours.Value + record.RemainingEffortHours.Value > 0m;

    public static int? PercentFor(EffectiveExecutionRecord? record) =>
        IsEligible(record)
            ? RoundPercent(record!.ActualEffortHours!.Value, record.RemainingEffortHours!.Value)
            : null;

    public static int RoundPercent(decimal actualEffortHours, decimal remainingEffortHours) =>
        decimal.ToInt32(decimal.Round(
            actualEffortHours / (actualEffortHours + remainingEffortHours) * 100m,
            0,
            MidpointRounding.AwayFromZero));
}
