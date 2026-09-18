using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Tests;

internal static class DomainModelTests
{
    public static void DataStateIncludesCanonicalDerivedAndResolutionStates()
    {
        TestAssert.True(Enum.IsDefined(DataState.Calculated), "Calculated must be a canonical data state.");
        TestAssert.True(Enum.IsDefined(DataState.Estimated), "Estimated must be a canonical data state.");
        TestAssert.True(Enum.IsDefined(DataState.Unresolved), "Unresolved must be a canonical data state.");
    }

    public static void PlannedEntityPreservesPlannedEffortState()
    {
        var workPackage = new WorkPackage
        {
            PlannedEffortHours = 4.5m,
            PlannedEffortState = DataState.Estimated
        };

        TestAssert.Equal(4.5m, workPackage.PlannedEffortHours, "Planned effort must remain nullable and preserved.");
        TestAssert.Equal(DataState.Estimated, workPackage.PlannedEffortState, "Planned effort state must describe the effort value.");
    }

    public static void ManagementAnalysisPreservesForecastConstraintAndEffortAccounting()
    {
        var analysis = new ManagementAnalysis
        {
            ForecastState = DataState.Unknown,
            ForecastFinish = null,
            ResourceBaselineScheduleConstraint = new ScheduleConstraintSummary
            {
                ResourceConstraint = "One coder",
                BaselineConstraint = "Sequential phases"
            },
            EffortAccounting = new EffortAccountingReconciliation
            {
                AccountingLevel = "WorkPackage",
                AuthoritativeEffortHours = 512m,
                DetailedEffortHours = 512m,
                DifferenceHours = 0m,
                State = DataState.Calculated
            }
        };

        TestAssert.Equal(DataState.Unknown, analysis.ForecastState, "Forecast state must be explicit.");
        TestAssert.Equal(null, analysis.ForecastFinish, "Forecast finish must remain nullable when evidence is unavailable.");
        TestAssert.Equal("One coder", analysis.ResourceBaselineScheduleConstraint!.ResourceConstraint, "Resource constraint must be retained.");
        TestAssert.Equal("Sequential phases", analysis.ResourceBaselineScheduleConstraint.BaselineConstraint, "Baseline constraint must be retained.");
        TestAssert.Equal("WorkPackage", analysis.EffortAccounting!.AccountingLevel, "Effort accounting level must be retained.");
        TestAssert.Equal(0m, analysis.EffortAccounting.DifferenceHours, "Effort reconciliation must retain its difference.");
    }

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
