using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Tests;

internal static class DomainModelTests
{
    public static void CanonicalProjectPreservesIndependentBaselineValues()
    {
        var sourceReference = new SourceReference
        {
            SourceId = "source-1",
            Repository = "fixture",
            RelativeFile = "planning/plan.md",
            ExtractionRule = "test"
        };
        var phase = new Phase
        {
            Id = "phase-1",
            Name = "Planning",
            PlannedStart = new DateOnly(2026, 9, 18),
            PlannedFinish = new DateOnly(2026, 9, 25),
            PlannedEffortHours = 12.5m,
            PlannedDurationWorkingMinutes = 2400,
            DurationState = DataState.Known,
            SourceReferences = [sourceReference]
        };
        var workPackage = new WorkPackage
        {
            Id = "wp-1",
            Name = "Define scope",
            PhaseId = phase.Id,
            PlannedStart = new DateOnly(2026, 9, 18),
            PlannedFinish = new DateOnly(2026, 9, 22),
            PlannedEffortHours = 4.5m,
            PlannedDurationWorkingMinutes = 960,
            DurationState = DataState.Known,
            SourceReferences = [sourceReference]
        };
        var card = new DeliveryCard
        {
            Id = "card-1",
            WorkPackageId = workPackage.Id,
            PhaseId = phase.Id,
            Name = "Draft scope",
            PlannedStart = new DateOnly(2026, 9, 18),
            PlannedFinish = new DateOnly(2026, 9, 18),
            PlannedEffortHours = 1.25m,
            PlannedDurationWorkingMinutes = 240,
            DurationState = DataState.Known,
            State = ExecutionState.NotStarted,
            SourceReferences = [sourceReference]
        };
        var milestone = new MilestoneDecision
        {
            Id = "milestone-1",
            Kind = MilestoneKind.Milestone,
            Name = "Scope approved",
            PlannedDate = new DateOnly(2026, 9, 25),
            State = ExecutionState.NotStarted,
            SourceReferences = [sourceReference]
        };
        var dependency = new Dependency
        {
            SubjectId = card.Id,
            SubjectKind = "DeliveryCard",
            PredecessorId = milestone.Id,
            PredecessorKind = "MilestoneDecision",
            DependencyType = DependencyType.FinishToStart,
            AnalysisEligible = true,
            ValidationState = ValidationState.Known,
            SourceReferences = [sourceReference]
        };
        var assignment = new Assignment
        {
            WorkItemId = card.Id,
            LogicalRoleCode = "DEV2",
            CarioRoleCode = "R",
            SourceReferences = [sourceReference]
        };
        var project = new CanonicalProject
        {
            SchemaVersion = "1.0",
            Project = new Project { Id = "project-1", Name = "Example project" },
            Sources = [new ProjectSource { Id = "source-1", Kind = "repository", Repository = "fixture" }],
            Baseline = new ProjectBaseline { Id = "baseline-1", Version = "0.1", Status = "Draft" },
            Phases = [phase],
            WorkPackages = [workPackage],
            DeliveryCards = [card],
            Milestones = [milestone],
            Dependencies = [dependency],
            Assignments = [assignment],
            Reserve = new Reserve { InitialHours = 2m, ConsumptionState = DataState.NotRun },
            Provenance = [sourceReference]
        };

        TestAssert.Equal(4.5m, project.WorkPackages[0].PlannedEffortHours, "Effort must be preserved.");
        TestAssert.Equal(960, project.WorkPackages[0].PlannedDurationWorkingMinutes, "Duration must be preserved independently.");
        TestAssert.Equal(new DateOnly(2026, 9, 18), project.WorkPackages[0].PlannedStart, "Start must be preserved.");
        TestAssert.Equal(new DateOnly(2026, 9, 22), project.WorkPackages[0].PlannedFinish, "Finish must be preserved.");
        TestAssert.Equal(1, project.Dependencies.Count, "Dependency must be part of the canonical model.");
        TestAssert.Equal(1, project.Assignments.Count, "Assignment must be part of the canonical model.");
        TestAssert.Equal(1, project.Provenance.Count, "Provenance must be part of the canonical model.");
    }
}
