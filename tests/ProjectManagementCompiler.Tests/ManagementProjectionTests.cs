using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ManagementProjectionTests
{
    public static void WbsProjectionBuildsPhasePackageCardAndMilestoneTree()
    {
        var project = CaptureCanonicalProject();
        var projection = new WbsProjector().Build(project);

        TestAssert.Equal(WbsNodeKind.Project, projection.Root.Kind, "WBS must expose one project root.");
        TestAssert.Equal(project.Project.Id, projection.Root.Id, "WBS root must use canonical project ID.");
        TestAssert.Equal(6, projection.Root.Children.Count(child => child.Kind == WbsNodeKind.Phase), "WBS must preserve all six phases.");
        TestAssert.Equal(35, AllNodes(projection.Root).Count(node => node.Kind == WbsNodeKind.WorkPackage), "WBS must preserve all work packages.");
        TestAssert.Equal(53, AllNodes(projection.Root).Count(node => node.Kind == WbsNodeKind.DeliveryCard), "WBS must preserve all delivery cards.");
        TestAssert.Equal(7, AllNodes(projection.Root).Count(node => node.Kind == WbsNodeKind.Milestone), "WBS must preserve all milestones and decisions.");

        var package = AllNodes(projection.Root).Single(node => node.Id == "F01" && node.Kind == WbsNodeKind.WorkPackage);
        TestAssert.True(package.Children.Any(child => child.Id == "F01-A" && child.Kind == WbsNodeKind.DeliveryCard), "Delivery cards must remain children of their work package.");
        TestAssert.True(package.Children.Any(child => child.Id == "F01-B" && child.Kind == WbsNodeKind.DeliveryCard), "All work-package delivery cards must be projected.");
        TestAssert.True(projection.Diagnostics.Count == 0, "The controlled fixture should produce a complete WBS without hierarchy diagnostics.");
    }

    public static void WbsProjectionDoesNotDoubleCountExecutableCards()
    {
        var projection = new WbsProjector().Build(CaptureCanonicalProject());
        var executableIds = AllNodes(projection.Root)
            .Where(node => node.Kind == WbsNodeKind.DeliveryCard)
            .Select(node => node.Id)
            .ToArray();

        TestAssert.Equal(executableIds.Length, executableIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Each executable delivery card must appear once in WBS.");
        TestAssert.False(AllNodes(projection.Root).Any(node => node.Kind == WbsNodeKind.WorkPackage && node.Id.EndsWith("-A", StringComparison.Ordinal)), "Delivery cards must not be reclassified as work packages.");
    }

    public static void SharedManagementViewsPreservePlanningExecutionAndRiskSemantics()
    {
        var project = CaptureCanonicalProject();
        var analysis = new ManagementAnalysisOrchestrator().Analyze(project, new DateOnly(2026, 9, 28));
        var views = new ManagementViewProjector().Build(project, analysis, new DateOnly(2026, 9, 28));

        TestAssert.Equal(53, views.Gantt.Items.Count, "Shared Gantt projection must expose every delivery card.");
        TestAssert.Equal(7, views.Gantt.Milestones.Count, "Shared Gantt projection must expose milestone markers.");
        TestAssert.Equal(7, views.DependencyNetwork.Nodes.Count(node => node.IsMilestone), "Dependency network must retain milestone nodes.");
        TestAssert.True(
            views.DependencyNetwork.Edges.Any(edge => edge.PredecessorId == "X99" && !edge.IncludedInAnalysis && edge.Reason == "INVALID_SOURCE_EVIDENCE"),
            "Dependency network must retain and explain excluded invalid source evidence.");
        TestAssert.Equal("Chưa bắt đầu|Đang thực hiện|Hoàn thành|Tạm ngưng|Hủy", string.Join('|', views.Kanban.Columns.Select(column => column.Label)), "Kanban labels must preserve the approved Vietnamese state policy.");
        TestAssert.Equal(1, views.Kanban.Columns.Single(column => column.Id == "IN_PROGRESS").WipLimit, "Kanban must use the canonical WIP policy rather than a hardcoded limit.");
        TestAssert.Equal(analysis.CalculatedFinish, views.Dashboard.CpmFinish, "Dashboard CPM finish must remain separate from forecast finish.");
        TestAssert.Equal(null, views.Dashboard.ForecastFinish, "Dashboard forecast must remain unknown without forecast evidence.");
        TestAssert.False(typeof(DashboardProjection).GetProperty("CompletionPercentage") is not null, "Dashboard API must not expose an ambiguous overall completion percentage.");
        TestAssert.Equal(analysis.CriticalPathIds.Count, views.Cpm.Rows.Count(row => row.IsCritical), "CPM view must preserve critical-path flags.");
    }

    private static IEnumerable<WbsNode> AllNodes(WbsNode node) =>
        new[] { node }.Concat(node.Children.SelectMany(AllNodes));

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
