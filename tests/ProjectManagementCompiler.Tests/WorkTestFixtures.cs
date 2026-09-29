using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Tests;

internal static class WorkTestFixtures
{
    public static readonly DateOnly ReportingDate = new(2026, 9, 15);

    public static CanonicalProject SyntheticProject() => new()
    {
        Project = new Project { Id = "SYNTH-PROJECT", Name = "Synthetic Work Fixture" },
        Baseline = new ProjectBaseline
        {
            Id = "SYNTH-BASELINE",
            Version = "1",
            Status = "APPROVED",
            PlanningStart = new DateOnly(2026, 9, 1),
            PlanningFinish = new DateOnly(2026, 10, 31),
            ValidationState = ValidationState.Known
        },
        Phases =
        [
            new Phase
            {
                Id = "PH-A",
                Name = "[PH-A] Foundation",
                PlannedStart = new DateOnly(2026, 9, 1),
                PlannedFinish = new DateOnly(2026, 9, 10),
                SourceReferences = [Reference("PH-A")]
            },
            new Phase
            {
                Id = "PH-B",
                Name = "[PH-B] Delivery",
                PlannedStart = new DateOnly(2026, 9, 11),
                PlannedFinish = new DateOnly(2026, 9, 20),
                SourceReferences = [Reference("PH-B")]
            },
            new Phase
            {
                Id = "PH-C",
                Name = "[PH-C] Review",
                PlannedStart = new DateOnly(2026, 9, 21),
                PlannedFinish = new DateOnly(2026, 9, 30),
                SourceReferences = [Reference("PH-C")]
            }
        ],
        WorkPackages =
        [
            new WorkPackage
            {
                Id = "WP-A",
                ParentId = "PH-A",
                PhaseId = "PH-A",
                Name = "Foundation package",
                DeliveryCardIds = ["P01", "P02"],
                SourceReferences = [Reference("WP-A")]
            },
            new WorkPackage
            {
                Id = "WP-B",
                ParentId = "PH-B",
                PhaseId = "PH-B",
                Name = "Delivery package",
                DeliveryCardIds = ["P03"],
                SourceReferences = [Reference("WP-B")]
            },
            new WorkPackage
            {
                Id = "WP-C",
                ParentId = "PH-C",
                PhaseId = "PH-C",
                Name = "Review package",
                DeliveryCardIds = ["P04"],
                SourceReferences = [Reference("WP-C")]
            }
        ],
        DeliveryCards =
        [
            Card("P01", "PH-A", "WP-A", "Prepare the synthetic workspace"),
            Card("P02", "PH-A", "WP-A", "Validate the synthetic workspace"),
            Card("P03", "PH-B", "WP-B", "Deliver the synthetic increment"),
            Card("P04", "PH-C", "WP-C", "Review the synthetic increment")
        ],
        Milestones =
        [
            new MilestoneDecision
            {
                Id = "P01",
                Name = "Synthetic decision with a colliding raw ID",
                Kind = MilestoneKind.Decision,
                PlannedDate = new DateOnly(2026, 9, 20),
                SourceReferences = [Reference("P01-milestone")]
            }
        ],
        Dependencies =
        [
            Direct("DeliveryCard", "P02", "DeliveryCard", "P01"),
            Direct("DeliveryCard", "P03", "DeliveryCard", "P02"),
            Direct("DeliveryCard", "P04", "Milestone", "P01"),
            Direct("WorkPackage", "WP-B", "DeliveryCard", "P03"),
            Direct("DeliveryCard", "P03", "DeliveryCard", "P99") with
            {
                ValidationState = ValidationState.InvalidSourceEvidence
            },
            Direct("DeliveryCard", "P04", "DeliveryCard", "P02") with
            {
                DependencyType = DependencyType.StartToStart
            }
        ],
        ResponsibilityRoles =
        [
            new ResponsibilityRole
            {
                Code = "LEAD",
                LogicalRoleCode = "LEAD",
                SourceMeaning = "Project lead",
                SourceReferences = [Reference("LEAD")]
            }
        ],
        Assignments =
        [
            new Assignment
            {
                WorkItemId = "P03",
                LogicalRoleCode = "LEAD",
                MappingStatus = "UNRESOLVED",
                SourceReferences = [Reference("P03-assignment")]
            }
        ],
        Provenance = [Reference("project")]
    };

    public static ManifestSnapshotMetadata OfficialMetadata() => new()
    {
        Classification = ManifestImportClassification.OfficialCommit,
        SourceIdentity = "0123456789abcdef0123456789abcdef01234567",
        RegisterStatusDate = ReportingDate,
        SnapshotId = "synthetic-official-snapshot"
    };

    public static ExecutionRecord LegacyRecord(
        string cardId,
        ExecutionState state,
        decimal? actualHours = null,
        decimal? remainingHours = null) => new()
    {
        WorkItemId = cardId,
        ExecutionState = state,
        ActualEffortHours = actualHours,
        ActualEffortState = actualHours is null ? DataState.Unknown : DataState.Known,
        RemainingEffortHours = remainingHours,
        RemainingEffortState = remainingHours is null ? DataState.Unknown : DataState.Known
    };

    public static SourceExecutionRecord SourceRecord(
        string cardId,
        ExecutionState? state,
        decimal? actualHours = null,
        decimal? remainingHours = null,
        SourceRecordingState recordingState = SourceRecordingState.Recorded) => new()
    {
        Entity = CanonicalWorkItemKey.DeliveryCard(cardId),
        RecordingState = recordingState,
        ExecutionState = state,
        ActualEffortHours = actualHours,
        RemainingEffortHours = remainingHours,
        LastUpdatedAt = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero)
    };

    private static DeliveryCard Card(string id, string phaseId, string workPackageId, string name) => new()
    {
        Id = id,
        Name = $"[{id}] {name}",
        ParentId = workPackageId,
        PhaseId = phaseId,
        WorkPackageId = workPackageId,
        PlannedStart = new DateOnly(2026, 9, 1).AddDays(id switch
        {
            "P01" => 0,
            "P02" => 2,
            "P03" => 10,
            _ => 20
        }),
        PlannedFinish = new DateOnly(2026, 9, 1).AddDays(id switch
        {
            "P01" => 1,
            "P02" => 3,
            "P03" => 11,
            _ => 21
        }),
        PlannedEffortHours = 4m,
        PlannedEffortState = DataState.Known,
        DurationState = DataState.Known,
        State = ExecutionState.NotStarted,
        SourceReferences = [Reference(id)]
    };

    private static Dependency Direct(string subjectKind, string subjectId, string predecessorKind, string predecessorId) => new()
    {
        SubjectKind = subjectKind,
        SubjectId = subjectId,
        PredecessorKind = predecessorKind,
        PredecessorId = predecessorId,
        DependencyType = DependencyType.FinishToStart,
        AnalysisEligible = true,
        ValidationState = ValidationState.Known,
        SourceReferences = [Reference($"{subjectId}-depends-on-{predecessorId}")]
    };

    private static SourceReference Reference(string item) => new()
    {
        SourceId = "synthetic-fixture",
        Repository = "synthetic",
        RelativeFile = "fixture/planning.md",
        Section = "Synthetic planning fixture",
        Item = item,
        ExtractionRule = "synthetic-test-data",
        AuthorityRank = 1,
        ConfidenceState = DataState.Known,
        ValidationState = ValidationState.Known
    };
}
