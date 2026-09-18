using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class CarioMappingTests
{
    public static void CarioMappingPreservesManyToManyAssignmentsAndLeavesUnresolvedIdentityBlank()
    {
        var project = CaptureCanonicalProject();
        var result = new CarioMappingProjector().Build(project, new CarioMappingConfiguration());

        TestAssert.Equal(project.Assignments.Count, result.Assignments.Count, "CARIO mapping must preserve every assignment row.");
        TestAssert.True(result.Assignments.All(assignment => string.IsNullOrEmpty(assignment.ConcreteIdentity)), "Default mapping must not fabricate concrete employees.");
        TestAssert.True(result.Warnings.Any(warning => warning.Code == "CARIO_MAPPING_UNRESOLVED"), "Unresolved identity mapping must emit a structured warning.");
        TestAssert.True(result.Assignments.Select(assignment => assignment.CarioRoleCode).Where(code => code is not null).All(code => new[] { "A", "R+", "R", "C", "I", "O" }.Contains(code!)), "All emitted CARIO role codes must be contract-valid.");
    }

    public static void CarioMappingConfigurationMapsRoleAndTaskMetadataWithoutChangingBaselineDates()
    {
        var project = CaptureCanonicalProject();
        var updated = new ExecutionOverlayUpdater().Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P01-A",
            ExecutionState = ExecutionState.Completed,
            ActualStart = new DateOnly(2026, 9, 21),
            ActualFinish = new DateOnly(2026, 9, 22),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero)
        });
        TestAssert.True(updated.Accepted, "The execution update should be accepted for export regression coverage.");

        var result = new CarioMappingProjector().Build(updated.Project, new CarioMappingConfiguration
        {
            LogicalRoles = new Dictionary<string, CarioRoleMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["LEAD"] = new CarioRoleMapping
                {
                    LogicalRoleCode = "LEAD",
                    CarioRoleCode = "A",
                    ConcreteIdentity = "configured-identity"
                }
            },
            TaskMappings = new Dictionary<string, CarioTaskMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["P01-A"] = new CarioTaskMapping { Priority = "HIGH", Department = "Engineering", Team = "Pilot" }
            }
        });

        var task = result.Tasks.Single(task => task.TaskId == "P01-A");
        TestAssert.Equal(new DateOnly(2026, 9, 18), task.PlannedStart, "CARIO planned start must come from the immutable baseline.");
        TestAssert.Equal(new DateOnly(2026, 9, 18), task.PlannedDeadline, "CARIO planned deadline must come from the immutable baseline.");
        TestAssert.Equal("HIGH", task.Priority, "Configured task priority must be projected.");
        TestAssert.Equal("configured-identity", result.Assignments.First(assignment => assignment.TaskId == "P01-A").ConcreteIdentity, "Configured role identity must be projected.");
        TestAssert.False(result.Tasks.Any(task => task.TaskId == "P01-A" && task.PlannedStart == new DateOnly(2026, 9, 21)), "Actual start must never overwrite the CARIO planned start.");
    }

    private static CanonicalProject CaptureCanonicalProject()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering");
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(new SourceRequest { Location = root }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var resolution = AuthorityResolution.Resolve(snapshot);
        return new CanonicalProjectNormalizer().Normalize(new IdeaEngineeringExtractor().Extract(resolution));
    }
}
