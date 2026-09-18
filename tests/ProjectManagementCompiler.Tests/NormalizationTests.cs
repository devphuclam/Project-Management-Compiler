using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class NormalizationTests
{
    public static void CanonicalNormalizationPreservesIndependentEffortAndDuration()
    {
        var canonical = new CanonicalProjectNormalizer().Normalize(
            new IdeaEngineeringExtractor().Extract(CaptureFixture()));

        TestAssert.Equal(512m, canonical.WorkPackages.Sum(workPackage => workPackage.PlannedEffortHours ?? 0m), "Appendix A work-package effort must sum exactly to 512 hours.");
        TestAssert.Equal(16m, canonical.WorkPackages.Single(workPackage => workPackage.Id == "P01").PlannedEffortHours, "Work-package effort must come from Appendix A.");
        TestAssert.Equal(480, canonical.WorkPackages.Single(workPackage => workPackage.Id == "P01").PlannedDurationWorkingMinutes, "A one-working-day authored package should have 480 working minutes.");
        TestAssert.Equal(4m, canonical.DeliveryCards.Single(card => card.Id == "P01-A").PlannedEffortHours, "Card effort must remain available as detail.");
        TestAssert.Equal(480, canonical.DeliveryCards.Single(card => card.Id == "P01-A").PlannedDurationWorkingMinutes, "AM-to-PM authored card dates should normalize through the working calendar.");
        TestAssert.True(
            canonical.WorkPackages.Single(workPackage => workPackage.Id == "P01").PlannedEffortHours
            != canonical.WorkPackages.Single(workPackage => workPackage.Id == "P01").PlannedDurationWorkingMinutes / 60m,
            "Effort and duration must remain independent values.");
        TestAssert.Equal(212m, canonical.DeliveryCards.Sum(card => card.PlannedEffortHours ?? 0m), "Card effort should be retained without replacing the authoritative parent roll-up.");
        TestAssert.True(canonical.Warnings.Any(warning => warning.Code == "EFFORT_RECONCILIATION"), "Detail/parent effort differences must be diagnosed.");
        TestAssert.True(canonical.Warnings.Any(warning => warning.Code == "EFFORT_DURATION_MISMATCH"), "An authored effort/schedule mismatch must be diagnosed without rewriting either value.");
    }

    public static void CanonicalNormalizationRetainsPolicyReserveStatesAndDeterministicRelationships()
    {
        var canonical = new CanonicalProjectNormalizer().Normalize(
            new IdeaEngineeringExtractor().Extract(CaptureFixture()));

        TestAssert.Equal(600m, canonical.Capacity.CapacityHours, "Capacity must remain 600 hours.");
        TestAssert.Equal(88m, canonical.Reserve.InitialHours, "Initial reserve must remain 88 hours.");
        TestAssert.True(canonical.Reserve.ConsumedHours is null, "Planning-only input must not invent reserve consumption.");
        TestAssert.True(canonical.Reserve.RemainingHours is null, "Planning-only input must not invent reserve remaining hours.");
        TestAssert.Equal(DataState.NotRun, canonical.Reserve.ConsumptionState, "Reserve consumption must be explicit NOT_RUN.");
        TestAssert.Equal(DataState.Unknown, canonical.Reserve.RemainingState, "Reserve remaining must be explicit UNKNOWN.");
        TestAssert.Equal(1, canonical.Policies.WorkInProgressLimit, "WIP must remain one.");

        TestAssert.True(canonical.WorkPackages.All(workPackage => canonical.Phases.Any(phase => phase.Id == workPackage.PhaseId)), "Every work package must normalize to an existing phase.");
        TestAssert.True(canonical.DeliveryCards.All(card => canonical.WorkPackages.Any(workPackage => workPackage.Id == card.WorkPackageId && workPackage.PhaseId == card.PhaseId)), "Every card must normalize to its existing work package and phase.");
        TestAssert.True(canonical.WorkPackages.All(workPackage => workPackage.DeliveryCardIds.SequenceEqual(canonical.DeliveryCards.Where(card => card.WorkPackageId == workPackage.Id).Select(card => card.Id))), "Child card IDs must be deterministic and parent-aligned.");
        TestAssert.True(canonical.Provenance.Count > canonical.Phases.Count + canonical.WorkPackages.Count + canonical.DeliveryCards.Count, "Canonical normalization should preserve value and entity provenance, not only counts.");
    }

    public static void WorkingCalendarNormalizesAuthoredHalfDaysWithoutUsingEffort()
    {
        var calendar = new WorkingCalendarNormalizer();
        var duration = calendar.Normalize(
            new DateOnly(2026, 9, 18),
            "AM",
            new DateOnly(2026, 9, 21),
            "PM");

        TestAssert.Equal(960, duration.WorkingMinutes, "Friday AM through Monday PM should cover two eight-hour working days.");
        TestAssert.Equal(DataState.Known, duration.State, "A complete authored date/marker schedule should be known.");
    }

    public static void WorkingCalendarAcceptsSourceDefinedOneSidedHalfDayBoundaries()
    {
        var calendar = new WorkingCalendarNormalizer();
        var duration = calendar.Normalize(
            new DateOnly(2026, 10, 8),
            "PM",
            new DateOnly(2026, 10, 9),
            null);

        TestAssert.Equal(720, duration.WorkingMinutes, "A source range with only a start PM marker should end at the authored finish date boundary.");
        TestAssert.Equal(DataState.Known, duration.State, "A one-sided source boundary marker should use the documented full-day boundary default.");
    }

    private static AuthorityResolution CaptureFixture()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering");
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(new SourceRequest { Location = root }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return AuthorityResolution.Resolve(snapshot);
    }
}
