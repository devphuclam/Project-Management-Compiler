using System.ComponentModel;
using System.Text;
using System.Text.Json;
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

    public static void SnapshotMetadataNeverExposesAbsoluteLocalRoot()
    {
        const string userRoot = @"C:\Users\alice\project";
        var request = new SourceRequest { Location = userRoot, Ref = "refs/heads/task-4" };
        var successful = Capture(FixtureFileSystem(userRoot), request);
        var failed = Capture(new FakeRepositoryFileSystem(), request);

        foreach (var snapshot in new[] { successful, failed })
        {
            var json = JsonSerializer.Serialize(snapshot);
            TestAssert.False(json.Contains(userRoot, StringComparison.OrdinalIgnoreCase), "Snapshot metadata must not expose the absolute local root.");
            TestAssert.Equal("local-repository", snapshot.LocationLabel, "Local snapshots should use a stable safe location label.");
        }

        TestAssert.True(successful.Documents.All(document => document.SourceReference.Repository == "local-repository"), "Document references must use a safe repository label.");
        TestAssert.True(failed.Diagnostics.SelectMany(diagnostic => diagnostic.SourceReferences).All(reference => reference.Repository == "local-repository"), "Failed diagnostics must use a safe repository label.");
        TestAssert.True(failed.Diagnostics.SelectMany(diagnostic => diagnostic.SourceReferences).All(reference => reference.ResolvedRef == request.Ref), "Failed diagnostics should retain the requested ref.");
    }

    public static void MissingRecognizedFileProducesStableDiagnostic()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.Remove(Path.Combine(Root, Kanban));
        var snapshot = Capture(fileSystem);

        TestAssert.Contains("SOURCE_FILE_MISSING", string.Join('|', snapshot.Diagnostics.Select(diagnostic => diagnostic.Code)), "Missing files should be diagnosed.");
        TestAssert.Equal(Kanban, snapshot.Diagnostics.Single(diagnostic => diagnostic.Code == "SOURCE_FILE_MISSING").SourceReferences.Single().RelativeFile, "Missing diagnostics should preserve the path.");
        TestAssert.Equal(WarningSeverity.Warning, snapshot.Diagnostics.Single(diagnostic => diagnostic.Code == "SOURCE_FILE_MISSING").Severity, "Missing files are warnings rather than capture errors.");
    }

    public static void RefIsCopiedToEveryDocumentAndDiagnosticReference()
    {
        var request = new SourceRequest { Location = Root, Ref = "refs/tags/v1" };
        var fileSystem = FixtureFileSystem();
        fileSystem.Remove(Path.Combine(Root, Kanban));
        var snapshot = Capture(fileSystem, request);

        TestAssert.Equal(request.Ref, snapshot.ResolvedRef, "The snapshot should retain the requested ref.");
        TestAssert.True(snapshot.Documents.Select(document => document.SourceReference).All(reference => reference.ResolvedRef == request.Ref), "Every document reference should retain the requested ref.");
        TestAssert.True(snapshot.Diagnostics.SelectMany(diagnostic => diagnostic.SourceReferences).All(reference => reference.ResolvedRef == request.Ref), "Every diagnostic reference should retain the requested ref.");
    }

    public static void DiagnosticsAreProducedInRecognizedPathOrder()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.Remove(Path.Combine(Root, Readme));
        fileSystem.Remove(Path.Combine(Root, Appendix));
        fileSystem.Remove(Path.Combine(Root, Html));
        fileSystem.Remove(Path.Combine(Root, Kanban));
        fileSystem.SetLength(Path.Combine(Root, Roadmap), 100);
        var snapshot = Capture(fileSystem, new SourceRequest { Location = Root, MaxDocumentBytes = 10 });

        var codes = snapshot.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray();
        TestAssert.True(
            codes.SequenceEqual(new[] { "SOURCE_FILE_MISSING", "SOURCE_FILE_TOO_LARGE", "SOURCE_FILE_MISSING", "SOURCE_FILE_MISSING", "SOURCE_FILE_MISSING" }),
            $"Diagnostics should remain deterministic and follow recognized path order. Actual: {string.Join(",", codes)}");
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
            () => SourcePathPolicy.ResolvePath(Root, @"C:\outside.md", FixtureFileSystem()),
            "A rooted candidate path must not be accepted.");
        TestAssert.Throws<ArgumentException>(
            () => SourcePathPolicy.ResolvePath(Root, "docs/../outside.md", FixtureFileSystem()),
            "A ref-like traversal must not change local path capture.");
    }

    public static void DriveRootNormalizationPreservesTrailingSeparator()
    {
        TestAssert.Equal(@"C:\", SourcePathPolicy.NormalizeRoot(@"C:\"), "Drive roots must retain their trailing separator.");
        TestAssert.Equal(@"C:\README.md", SourcePathPolicy.ResolvePath(@"C:\", Readme, FixtureFileSystem()), "A drive-rooted candidate must resolve under the drive root.");
    }

    public static void ReparsePointEscapeProducesDiagnosticWithoutReading()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.MarkReparse(Path.Combine(Root, "docs"));
        var snapshot = Capture(fileSystem);

        TestAssert.Contains("SOURCE_PATH_ESCAPE", string.Join('|', snapshot.Diagnostics.Select(diagnostic => diagnostic.Code)), "Reparse-point segments must be rejected.");
        TestAssert.False(fileSystem.ReadPaths.Any(path => path.Contains("docs", StringComparison.OrdinalIgnoreCase)), "Capture must not read through a reparse point.");
    }

    public static void FileLevelReparsePointIsRejectedWithoutReading()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.MarkReparse(Path.Combine(Root, Readme));
        var snapshot = Capture(fileSystem);

        var diagnostic = snapshot.Diagnostics.Single(diagnostic => diagnostic.Code == "SOURCE_PATH_ESCAPE");
        TestAssert.Equal(WarningSeverity.Error, diagnostic.Severity, "Unsafe source paths remain capture errors.");
        TestAssert.False(fileSystem.ReadPaths.Contains(Path.GetFullPath(Path.Combine(Root, Readme))), "A reparse-point file must not be read.");
    }

    public static void CaptureUsesValidatedReadBoundaryInsteadOfLegacyChecks()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.ThrowOnLegacyRead = true;
        var snapshot = Capture(fileSystem);

        TestAssert.Equal(5, fileSystem.ValidatedReadPaths.Count, "Every selected file should use the validated read operation.");
        TestAssert.Equal(0, fileSystem.LegacyLengthReadPaths.Count, "Capture must not perform a separate length check.");
        TestAssert.Equal(0, fileSystem.LegacyTextReadPaths.Count, "Capture must not perform a separate text read.");
        TestAssert.Equal(5, snapshot.Documents.Count, "Validated reads should still capture the fixture.");
    }

    public static void CapturePassesNormalizedAllowedRootToValidatedReadBoundary()
    {
        var fileSystem = FixtureFileSystem();
        var snapshot = Capture(fileSystem, new SourceRequest { Location = Root + Path.DirectorySeparatorChar });

        TestAssert.Equal(5, snapshot.Documents.Count, "A normalized-root capture should still read the fixture.");
        TestAssert.Equal(5, fileSystem.ValidatedReadRoots.Count, "Every validated read should receive the allowed root.");
        TestAssert.True(fileSystem.ValidatedReadRoots.All(path => path == Root), "Validated reads must receive the normalized allowed root, not the source file path or raw request path.");
    }

    public static void Win32ReadFailureProducesBlockedCaptureDiagnostic()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.ExceptionAtValidatedRead = new Win32Exception(5);
        var snapshot = Capture(fileSystem);

        var diagnostic = snapshot.Diagnostics.Single(diagnostic => diagnostic.SourceReferences.Any(reference => reference.RelativeFile == Readme));
        TestAssert.Equal(CaptureState.Blocked, snapshot.CaptureState, "A Win32 read failure must block source capture.");
        TestAssert.Equal("SOURCE_CAPTURE_FAILED", diagnostic.Code, "A Win32 read failure must be mapped to the stable capture failure diagnostic.");
        TestAssert.False(fileSystem.ReadPaths.Contains(Path.GetFullPath(Path.Combine(Root, Readme))), "A failed Win32 read must not produce a captured document.");
    }

    public static void OversizedFilesAreRejectedBeforeRead()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.SetLength(Path.Combine(Root, Readme), 2 * 1024 * 1024 + 1);
        var snapshot = Capture(fileSystem);

        var diagnostic = snapshot.Diagnostics.Single(diagnostic => diagnostic.Code == "SOURCE_FILE_TOO_LARGE");
        TestAssert.Equal(WarningSeverity.Error, diagnostic.Severity, "Oversized files remain capture errors.");
        TestAssert.False(fileSystem.ReadPaths.Contains(Path.GetFullPath(Path.Combine(Root, Readme))), "An oversized file must not be read partially.");
    }

    public static void ValidatedReadRejectsBoundaryReparseWithoutReading()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.ReparseAtValidatedRead.Add(Path.GetFullPath(Path.Combine(Root, Readme)));
        var snapshot = Capture(fileSystem);

        TestAssert.Equal("SOURCE_PATH_ESCAPE", snapshot.Diagnostics.First(diagnostic => diagnostic.SourceReferences.Any(reference => reference.RelativeFile == Readme)).Code, "A reparse detected at the read boundary must be reported as a path escape.");
        TestAssert.False(fileSystem.ReadPaths.Contains(Path.GetFullPath(Path.Combine(Root, Readme))), "A file rejected at the read boundary must not be read.");
    }

    public static void ValidatedReadRejectsBoundaryOversizeWithoutPartialRead()
    {
        var fileSystem = FixtureFileSystem();
        fileSystem.OversizeAtValidatedRead.Add(Path.GetFullPath(Path.Combine(Root, Readme)));
        var snapshot = Capture(fileSystem);

        TestAssert.Equal("SOURCE_FILE_TOO_LARGE", snapshot.Diagnostics.First(diagnostic => diagnostic.SourceReferences.Any(reference => reference.RelativeFile == Readme)).Code, "A size rejection at the read boundary needs a precise diagnostic.");
        TestAssert.False(fileSystem.ReadPaths.Contains(Path.GetFullPath(Path.Combine(Root, Readme))), "A file rejected for size must not be partially read.");
    }

    public static void TotalSizeLimitIsRejectedBeforeReadingOverLimitDocument()
    {
        var fileSystem = FixtureFileSystem();
        var request = new SourceRequest { Location = Root, MaxTotalDocumentBytes = 10 };
        var snapshot = Capture(fileSystem, request);

        TestAssert.Contains("SOURCE_TOTAL_TOO_LARGE", string.Join('|', snapshot.Diagnostics.Select(diagnostic => diagnostic.Code)), "The total limit needs a precise diagnostic.");
        TestAssert.True(fileSystem.ReadPaths.Count < 5, "Capture must stop before reading a document that would exceed the total limit.");
    }

    public static void TotalSizeAccountingUsesActualReadBytes()
    {
        var fileSystem = MinimalFileSystem();
        fileSystem.AddFileBytes(Path.Combine(Root, Readme), new UTF8Encoding(false, true).GetBytes("é"));
        fileSystem.AddFileBytes(Path.Combine(Root, Roadmap), new UTF8Encoding(false, true).GetBytes("x"));
        var snapshot = Capture(fileSystem, new SourceRequest { Location = Root, MaxDocumentBytes = 2, MaxTotalDocumentBytes = 3 });

        TestAssert.Equal(2, snapshot.Documents.Count, "Both files that exactly fit the byte total should be captured.");
        TestAssert.Equal(2L, snapshot.Documents[0].SizeBytes, "The first document size should be its UTF-8 byte count.");
        TestAssert.Equal(1L, snapshot.Documents[1].SizeBytes, "The second document size should be its UTF-8 byte count.");
        TestAssert.Equal(3L, fileSystem.TotalBytesRead, "Total accounting should equal actual bytes read.");
    }

    public static void StrictUtf8AcceptsValidContent()
    {
        var fileSystem = MinimalFileSystem();
        fileSystem.AddFileBytes(Path.Combine(Root, Readme), new UTF8Encoding(false, true).GetBytes("café"));
        var snapshot = Capture(fileSystem, new SourceRequest { Location = Root, MaxDocumentBytes = 16, MaxTotalDocumentBytes = 16 });

        TestAssert.Equal("café", snapshot.Documents.Single().Content, "Valid UTF-8 should be decoded with the supplied encoding.");
    }

    public static void StrictUtf8RejectsInvalidBytesWithoutPartialDocument()
    {
        var fileSystem = MinimalFileSystem();
        fileSystem.AddFileBytes(Path.Combine(Root, Readme), [0xC3, 0x28]);
        var snapshot = Capture(fileSystem, new SourceRequest { Location = Root, MaxDocumentBytes = 16, MaxTotalDocumentBytes = 16 });

        var diagnostic = snapshot.Diagnostics.Single(diagnostic => diagnostic.SourceReferences.Any(reference => reference.RelativeFile == Readme));
        TestAssert.Equal("SOURCE_CAPTURE_FAILED", diagnostic.Code, "Invalid UTF-8 should fail capture precisely.");
        TestAssert.Equal(WarningSeverity.Error, diagnostic.Severity, "Capture failures remain errors.");
        TestAssert.False(snapshot.Documents.Any(document => document.RelativeFile == Readme), "Invalid UTF-8 must not produce a partial document.");
        TestAssert.False(fileSystem.ReadPaths.Contains(Path.GetFullPath(Path.Combine(Root, Readme))), "Invalid UTF-8 must not be committed as a read document.");
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

    private static FakeRepositoryFileSystem FixtureFileSystem(string root = Root)
    {
        var fileSystem = new FakeRepositoryFileSystem();
        fileSystem.AddDirectory(root);
        fileSystem.AddDirectory(Path.Combine(root, "docs"));
        fileSystem.AddDirectory(Path.Combine(root, "docs/product/instances/idea-engineering"));
        fileSystem.AddDirectory(Path.Combine(root, "docs/product/instances/idea-engineering/planning"));
        foreach (var path in new[] { Readme, Roadmap, Appendix, Html, Kanban })
        {
            fileSystem.AddFile(Path.Combine(root, path), $"# {path}");
        }

        fileSystem.AddFile(Path.Combine(root, "run.ps1"), "throw 'must never execute'");
        return fileSystem;
    }

    private static FakeRepositoryFileSystem MinimalFileSystem()
    {
        var fileSystem = new FakeRepositoryFileSystem();
        fileSystem.AddDirectory(Root);
        fileSystem.AddDirectory(Path.Combine(Root, "docs"));
        fileSystem.AddDirectory(Path.Combine(Root, "docs/product/instances/idea-engineering"));
        fileSystem.AddDirectory(Path.Combine(Root, "docs/product/instances/idea-engineering/planning"));
        return fileSystem;
    }
}
