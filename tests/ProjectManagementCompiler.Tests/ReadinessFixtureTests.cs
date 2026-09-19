using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ReadinessFixtureTests
{
    private const string Increment = "specs/004-technical-pilot-readiness";

    public static void PublicReadinessFixtureCapturesThroughRealFileSystem()
    {
        var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering-real-shaped"));
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(
                new SourceRequest
                {
                    Location = root,
                    ManagementEvidenceIncrementPath = Increment
                },
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        TestAssert.Equal(11, snapshot.Documents.Count, "The public-safe real-shaped fixture must capture five planning and six readiness files.");
        var evidence = new IdeaEngineeringReadinessAdapter()
            .Adapt(
                snapshot,
                new CanonicalProject
                {
                    WorkPackages = Enumerable.Range(1, 7)
                        .Select(index => new WorkPackage { Id = $"P{index:00}" })
                        .ToArray()
                })
            .Evidence;

        TestAssert.Equal(ManagementEvidenceDiscoveryState.Known, evidence.DiscoveryState, "The fixture must have agreeing active increment identity.");
        TestAssert.Equal(7, evidence.Observations.Count(observation => observation.EvidenceKind == ManagementEvidenceKind.ReadinessCheck), "The fixture must expose P01-P07.");
        TestAssert.Equal(6, evidence.Observations.Count(observation => observation.EvidenceKind == ManagementEvidenceKind.DecisionRecord), "The fixture must expose D0-D5.");
        TestAssert.Equal(3, evidence.Observations.Count(observation => observation.EvidenceKind == ManagementEvidenceKind.HumanAction), "The fixture must expose the minimum human action board rows.");
    }

    public static void PublicReadinessFixtureContainsNoAbsoluteOrPrivateMaterial()
    {
        var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering-real-shaped", "specs", "004-technical-pilot-readiness"));
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(path);
            TestAssert.False(content.Contains("C:\\Users\\", StringComparison.OrdinalIgnoreCase), "Public readiness fixtures must not contain local absolute paths.");
            TestAssert.False(content.Contains("password", StringComparison.OrdinalIgnoreCase), "Public readiness fixtures must not contain credentials.");
            TestAssert.False(content.Contains("secret", StringComparison.OrdinalIgnoreCase), "Public readiness fixtures must not contain secret material.");
        }
    }
}
