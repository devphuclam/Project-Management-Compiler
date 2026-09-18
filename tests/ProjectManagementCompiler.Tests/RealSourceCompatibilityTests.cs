using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class RealSourceCompatibilityTests
{
    public static void RealShapedAuthorityExtractsCurrentPlanningContract()
    {
        var project = CaptureCanonicalProject();

        TestAssert.Equal("IE-PROD-ROADMAP-001", project.Project.Id, "DOC-07 stable document identity must become the project identity.");
        TestAssert.Equal("IE-PLAN-DEC2026-002", project.Baseline.Id, "The applicable Technical Pilot schedule baseline must be extracted.");
        TestAssert.Equal("0.1", project.Baseline.Version, "The schedule baseline version must be extracted from the current source contract.");
        TestAssert.Equal(6, project.Phases.Count, "The real-shaped contract must expose PH0 through PH5.");
        TestAssert.Equal(new DateOnly(2026, 9, 18), project.Baseline.PlanningStart, "DOC-07 must own the planning window start.");
        TestAssert.Equal(new DateOnly(2026, 12, 31), project.Baseline.PlanningFinish, "DOC-07 must own the planning window finish.");
        TestAssert.Equal(512m, project.Baseline.PlannedEffortHours, "DOC-07 must expose authoritative planned work.");
        TestAssert.Equal(88m, project.Baseline.ReserveHours, "DOC-07 must expose controlled reserve.");
        TestAssert.Equal(600m, project.Baseline.CapacityHours, "DOC-07 must expose weekday capacity.");
    }

    public static void RealShapedAppendixAndKanbanPreserveCardsDependenciesAndManyToManyAssignments()
    {
        var project = CaptureCanonicalProject();

        TestAssert.Equal(35, project.WorkPackages.Count, "Appendix A must expose all 35 work packages.");
        TestAssert.Equal(512m, project.WorkPackages.Sum(workPackage => workPackage.PlannedEffortHours ?? 0m), "Appendix A work-package effort must sum to 512 hours.");
        TestAssert.True(project.WorkPackages.Single(workPackage => workPackage.Id == "P03").DependencyIds.SequenceEqual(new[] { "P01", "P02" }), "Appendix A must preserve multiple work-package predecessors deterministically.");
        TestAssert.True(project.DeliveryCards.Any(card => card.Id == "P01"), "Unsplit real card IDs must be supported.");
        TestAssert.True(project.DeliveryCards.Any(card => card.Id == "F01-A"), "Split real card IDs must be supported.");
        TestAssert.Equal(53, project.DeliveryCards.Count, "The real-shaped Kanban register must expose 53 delivery cards.");
        TestAssert.True(project.Dependencies.Count(dependency => dependency.SubjectId == "P03") == 2, "Multiple card predecessors must become separate dependency edges.");
        TestAssert.True(project.Assignments.Count > project.DeliveryCards.Count, "CARIO assignments must be many-to-many rather than one assignment per card.");
        TestAssert.True(project.Assignments.Count(assignment => assignment.WorkItemId == "P04") >= 5, "A representative multi-role card must expand populated CARIO cells.");
        TestAssert.True(project.Assignments.Any(assignment => assignment.WorkItemId == "P02" && assignment.CarioRoleCode == "C" && assignment.LogicalRoleCode == "PDA"), "A single CARIO cell with multiple logical roles must expand into separate assignments.");
    }

    public static void RealShapedDoc07OwnsMilestonesAndGateDependencies()
    {
        var project = CaptureCanonicalProject();

        TestAssert.Equal(7, project.Milestones.Count, "DOC-07 must own D0 and MS0 through MS5.");
        TestAssert.True(project.Milestones.All(milestone => milestone.SourceReferences.Any(reference => reference.RelativeFile.EndsWith("DOC-07-mvp-roadmap-and-delivery-plan.md", StringComparison.Ordinal))), "Milestone authority must be DOC-07, not the Gantt rendition.");
        TestAssert.True(project.Dependencies.Any(dependency => dependency.SubjectId == "G-MS0" && dependency.PredecessorId == "G-D0"), "Gate dependency D0 -> MS0 must remain in the canonical graph.");
        TestAssert.True(project.Dependencies.Any(dependency => dependency.SubjectId == "G-MS0" && dependency.PredecessorId == "P07"), "Milestone gate prerequisites must preserve work-item to gate edges.");
    }

    public static void RealShapedCaptureProvenanceAndCanonicalSourceBoundaryAreSafe()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering-real-shaped");
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(new SourceRequest { Location = root, Ref = "fixture-real-shaped" }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var project = new CanonicalProjectNormalizer().Normalize(
            AuthorityResolution.Resolve(snapshot) is { } resolution
                ? new IdeaEngineeringExtractor().Extract(resolution)
                : throw new InvalidOperationException("Authority resolution did not return a result."));
        var json = new CanonicalJsonSerializer().Serialize(project);

        TestAssert.True(project.Sources.Single().CapturedAtUtc is not null, "Capture timestamp must survive normalization into canonical source metadata.");
        TestAssert.True(project.Sources.Single().Id != "local-repository", "Safe local source identity must distinguish the fixture source.");
        TestAssert.False(json.Contains("Mục đích: cả nhóm dùng đúng tài liệu", StringComparison.Ordinal), "Canonical JSON must not persist full captured source text.");
        TestAssert.False(json.Contains("0001-01-01", StringComparison.Ordinal), "Unknown dates must not serialize DateOnly.MinValue as business data.");
    }

    private static CanonicalProject CaptureCanonicalProject()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering-real-shaped");
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(new SourceRequest { Location = root, Ref = "fixture-real-shaped" }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var resolution = AuthorityResolution.Resolve(snapshot);
        return new CanonicalProjectNormalizer().Normalize(new IdeaEngineeringExtractor().Extract(resolution));
    }
}
