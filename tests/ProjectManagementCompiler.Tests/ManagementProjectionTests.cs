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
