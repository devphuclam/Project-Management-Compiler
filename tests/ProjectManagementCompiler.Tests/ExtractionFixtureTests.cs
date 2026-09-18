using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ExtractionFixtureTests
{
    public static void ControlledFixtureExtractsCanonicalPlanningRecords()
    {
        var resolution = CaptureFixture();
        var plan = new IdeaEngineeringExtractor().Extract(resolution);

        TestAssert.Equal("idea-ddm-technical-pilot-2026", plan.Project.Id, "Project identity should come from DOC-07.");
        TestAssert.Equal("IE-PLAN-DEC2026-002", plan.Baseline.Id, "Baseline ID should come from DOC-07.");
        TestAssert.Equal("0.1", plan.Baseline.Version, "Baseline version should come from DOC-07.");
        TestAssert.Equal("Draft", plan.Baseline.Status, "Baseline status should come from DOC-07.");
        TestAssert.Equal(new DateOnly(2026, 9, 18), plan.Baseline.PlanningStart, "Planning start should be authored by DOC-07.");
        TestAssert.Equal(new DateOnly(2026, 12, 31), plan.Baseline.PlanningFinish, "Planning finish should be authored by DOC-07.");
        TestAssert.Equal(new DateOnly(2026, 12, 31), plan.Project.TargetDate, "Target date should be authored by DOC-07.");
        TestAssert.Equal(512m, plan.Baseline.PlannedEffortHours, "Authoritative effort should be authored by DOC-07.");
        TestAssert.Equal(88m, plan.Baseline.ReserveHours, "Initial reserve should be authored by DOC-07.");
        TestAssert.Equal(600m, plan.Baseline.CapacityHours, "Capacity should be authored by DOC-07.");

        TestAssert.Equal(6, plan.Phases.Count, "The fixture should produce PH0-PH5.");
        TestAssert.Equal("PH0,PH1,PH2,PH3,PH4,PH5", string.Join(',', plan.Phases.Select(phase => phase.Id)), "Phases should have deterministic ordering.");
        TestAssert.Equal(35, plan.WorkPackages.Count, "Appendix A should produce 35 work packages.");
        TestAssert.Equal(53, plan.DeliveryCards.Count, "Kanban should produce 53 delivery cards.");
        TestAssert.Equal(7, plan.Milestones.Count, "The Gantt should produce seven milestones and gates.");
        TestAssert.Equal(1, plan.Policies.WorkInProgressLimit, "WIP policy should be retained.");
        TestAssert.Equal(6, plan.ResponsibilityRoles.Count(role => role.CarioRoleCode is not null), "All six CARIO codes should be retained.");
        TestAssert.Equal(53, plan.Assignments.Count, "Each delivery card should retain its logical-role assignment.");
        TestAssert.True(plan.Assignments.All(assignment => assignment.ConcreteIdentity is null), "The fixture does not authorize concrete identity inference.");

        TestAssert.True(plan.ValueProvenance.Count >= 9, "Important DOC-07 values should retain field-level provenance.");
        TestAssert.True(plan.Phases.Concat<PlannedEntity>(plan.WorkPackages).Concat(plan.DeliveryCards).All(entity => entity.SourceReferences.Count > 0), "Every normalized planning entity should retain source provenance.");
        TestAssert.True(plan.Milestones.All(milestone => milestone.SourceReferences.Count > 0), "Every normalized milestone should retain source provenance.");
    }

    public static void ExtractionRetainsInvalidSourceDependencyEvidence()
    {
        var plan = new IdeaEngineeringExtractor().Extract(CaptureFixture());
        var missing = plan.Dependencies.Single(dependency => dependency.PredecessorId == "X99");

        TestAssert.Equal(ValidationState.InvalidSourceEvidence, missing.ValidationState, "A missing source predecessor must be marked invalid source evidence.");
        TestAssert.False(missing.AnalysisEligible, "A missing source predecessor must not be analysis eligible.");
        TestAssert.True(plan.Warnings.Any(warning => warning.Code == "MISSING_DEPENDENCY_TARGET" && warning.AffectedIds.Contains("X99")), "Missing predecessor evidence must produce a diagnostic.");
        TestAssert.True(missing.SourceReferences.Count > 0, "Invalid dependency evidence must retain its source reference.");
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
