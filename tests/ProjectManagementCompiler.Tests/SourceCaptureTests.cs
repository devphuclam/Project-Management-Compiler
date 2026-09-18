using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class SourceCaptureTests
{
    private const string Root = @"C:\fixture";
    private const string Readme = "README.md";
    private const string Roadmap = "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md";
    private const string Appendix = "docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md";
    private const string Html = "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html";
    private const string Kanban = "docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md";

    public static void SourceRequestUsesSafeSizeDefaults()
    {
        var request = new SourceRequest { Location = Root };

        TestAssert.Equal(2 * 1024 * 1024, request.MaxDocumentBytes, "Per-document limit should default to 2 MiB.");
        TestAssert.Equal(8 * 1024 * 1024, request.MaxTotalDocumentBytes, "Total limit should default to 8 MiB.");
        TestAssert.False(request.AllowGitHttps, "HTTPS capture should be opt-in.");
    }

    public static void CaptureReadsAllowListedDocumentsInFixedOrder()
    {
        var fileSystem = FixtureFileSystem();
        var snapshot = Capture(fileSystem);

        TestAssert.Equal(5, snapshot.Documents.Count, "Capture should select exactly the five recognized documents.");
        TestAssert.Equal(Readme, snapshot.Documents[0].RelativeFile, "README should be selected first.");
        TestAssert.Equal(Roadmap, snapshot.Documents[1].RelativeFile, "DOC-07 should be selected second.");
        TestAssert.Equal(Appendix, snapshot.Documents[2].RelativeFile, "Appendix should be selected third.");
        TestAssert.Equal(Html, snapshot.Documents[3].RelativeFile, "Roadmap HTML should be selected fourth.");
        TestAssert.Equal(Kanban, snapshot.Documents[4].RelativeFile, "Kanban should be selected fifth.");
        TestAssert.Equal(CaptureState.Known, snapshot.CaptureState, "A readable fixture should be known.");
        TestAssert.Equal(5, fileSystem.ReadPaths.Count, "Capture must not enumerate or read arbitrary files.");
    }

    public static void CapturePreservesNormalizedReferencesAndOneTimestamp()
    {
        var snapshot = Capture(FixtureFileSystem());

        TestAssert.True(snapshot.CapturedAtUtc.HasValue, "Capture metadata should include a timestamp.");
        TestAssert.True(snapshot.Documents.All(document => !Path.IsPathRooted(document.RelativeFile)), "References must be relative.");
        TestAssert.True(snapshot.Documents.All(document => document.RelativeFile == document.RelativeFile.Replace('\\', '/')), "References use stable slash separators.");
        TestAssert.True(snapshot.Documents.All(document => document.SourceReference.RelativeFile == document.RelativeFile), "Source references preserve selected paths.");
    }

    public static void MissingRecognizedFileProducesStableDiagnostic()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.Remove(Path.Combine(Root, Kanban));
        var snapshot = Capture(fileSystem);

        TestAssert.Contains("SOURCE_FILE_MISSING", string.Join('|', snapshot.Diagnostics.Select(diagnostic => diagnostic.Code)), "Missing files should be diagnosed.");
        TestAssert.Equal(Kanban, snapshot.Diagnostics.Single(diagnostic => diagnostic.Code == "SOURCE_FILE_MISSING").SourceReferences.Single().RelativeFile, "Missing diagnostics should preserve the path.");
    }

    public static void UnreadableRootProducesBlockedDiagnostic()
    {
        var snapshot = Capture(new FakeRepositoryFileSystem());

        TestAssert.Equal(CaptureState.Blocked, snapshot.CaptureState, "An unreadable root must block capture.");
        TestAssert.Equal("SOURCE_CAPTURE_FAILED", snapshot.Diagnostics.Single().Code, "An unreadable root needs a stable diagnostic.");
    }

    public static void RootedAndTraversalPathsAreRejected()
    {
        TestAssert.Throws<ArgumentException>(
            () => SourcePathPolicy.ResolvePath(Root, "docs/../outside.md", FixtureFileSystem()),
            "A ref-like traversal must not change local path capture.");
    }

    public static void ReparsePointEscapeProducesDiagnosticWithoutReading()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.MarkReparse(Path.Combine(Root, "docs"));
        var snapshot = Capture(fileSystem);

        TestAssert.Contains("SOURCE_PATH_ESCAPE", string.Join('|', snapshot.Diagnostics.Select(diagnostic => diagnostic.Code)), "Reparse-point segments must be rejected.");
        TestAssert.False(fileSystem.ReadPaths.Any(path => path.Contains("docs", StringComparison.OrdinalIgnoreCase)), "Capture must not read through a reparse point.");
    }

    public static void OversizedFilesAreRejectedBeforeRead()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.SetLength(Path.Combine(Root, Readme), 2 * 1024 * 1024 + 1);
        var snapshot = Capture(fileSystem);

        TestAssert.Contains("SOURCE_FILE_TOO_LARGE", string.Join('|', snapshot.Diagnostics.Select(diagnostic => diagnostic.Code)), "Oversized files need a precise diagnostic.");
        TestAssert.False(fileSystem.ReadPaths.Contains(Path.GetFullPath(Path.Combine(Root, Readme))), "An oversized file must not be read partially.");
    }

    public static void TotalSizeLimitIsRejectedBeforeReadingOverLimitDocument()
    {
        var fileSystem = FixtureFileSystem();
        var request = new SourceRequest { Location = Root, MaxTotalDocumentBytes = 10 };
        var snapshot = Capture(fileSystem, request);

        TestAssert.Contains("SOURCE_TOTAL_TOO_LARGE", string.Join('|', snapshot.Diagnostics.Select(diagnostic => diagnostic.Code)), "The total limit needs a precise diagnostic.");
        TestAssert.True(fileSystem.ReadPaths.Count < 5, "Capture must stop before reading a document that would exceed the total limit.");
    }

    public static void FixtureCaptureWorksAgainstRealFileSystem()
    {
        var fixtureRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering"));
        var snapshot = Capture(new LocalRepositorySourceAdapter(), new SourceRequest { Location = fixtureRoot });

        TestAssert.Equal(5, snapshot.Documents.Count, "The local fixture should capture all recognized documents.");
        TestAssert.True(snapshot.Documents.All(document => document.Content.Length > 0), "Fixture documents should contain captured text.");
    }

    private static RepositorySnapshot Capture(FakeRepositoryFileSystem fileSystem, SourceRequest? request = null) =>
        Capture(new LocalRepositorySourceAdapter(fileSystem), request ?? new SourceRequest { Location = Root });

    private static RepositorySnapshot Capture(LocalRepositorySourceAdapter adapter, SourceRequest request) =>
        adapter.CaptureAsync(request, CancellationToken.None).GetAwaiter().GetResult();

    private static FakeRepositoryFileSystem FixtureFileSystem()
    {
        var fileSystem = new FakeRepositoryFileSystem();
        fileSystem.AddDirectory(Root);
        fileSystem.AddDirectory(Path.Combine(Root, "docs"));
        fileSystem.AddDirectory(Path.Combine(Root, "docs/product/instances/idea-engineering"));
        fileSystem.AddDirectory(Path.Combine(Root, "docs/product/instances/idea-engineering/planning"));
        foreach (var path in new[] { Readme, Roadmap, Appendix, Html, Kanban })
        {
            fileSystem.AddFile(Path.Combine(Root, path), $"# {path}");
        }

        fileSystem.AddFile(Path.Combine(Root, "run.ps1"), "throw 'must never execute'");
        return fileSystem;
    }
}
