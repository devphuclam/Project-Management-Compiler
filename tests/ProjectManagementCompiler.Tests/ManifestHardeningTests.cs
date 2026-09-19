using System.Text;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ManifestHardeningTests
{
    public static void WorkingTreeRejectsUnavailableGitStateInsteadOfComparingSentinel()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pmc-working-tree-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "planning"));
        File.WriteAllText(
            Path.Combine(root, "planning", "project-management-compiler-manifest.json"),
            "{\"sources\":[]}",
            new UTF8Encoding(false));

        try
        {
            var capture = new ManifestWorkingTreeReader().CaptureAsync(new ManifestImportRequest
            {
                RepositoryRoot = root,
                Mode = ManifestImportMode.UncommittedPreview,
                MaxFileBytes = 1024,
                MaxTotalBytes = 2048
            }).GetAwaiter().GetResult();

            TestAssert.False(capture.IsStable, "A working-tree capture without verifiable Git state must fail closed.");
            TestAssert.True(capture.Diagnostics.Any(diagnostic => diagnostic.Code == "PMC-SNAPSHOT-002"), "Unavailable Git state must produce a snapshot stability diagnostic.");
            TestAssert.False(capture.Diagnostics.Any(diagnostic => diagnostic.Code == "PMC-SNAPSHOT-001"), "Unavailable Git state must not be treated as a stable preview.");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    public static void WorkingTreeRejectsUnexpectedGitStateFailureInsteadOfEscaping()
    {
        var capture = new ManifestWorkingTreeReader(new UnexpectedGitStateProbe()).CaptureAsync(new ManifestImportRequest
        {
            RepositoryRoot = Directory.GetCurrentDirectory(),
            Mode = ManifestImportMode.UncommittedPreview,
            MaxFileBytes = 1024,
            MaxTotalBytes = 2048
        }).GetAwaiter().GetResult();

        TestAssert.False(capture.IsStable, "Any unverifiable Git-state failure must fail closed.");
        TestAssert.True(capture.Diagnostics.Any(diagnostic => diagnostic.Code == "PMC-SNAPSHOT-002"), "Unexpected Git-state failures must become snapshot diagnostics.");
    }

    public static void WorkingTreeReadsManifestIndependentlyForPreAndPostCapture()
    {
        var probe = new SequencedWorkingTreeProbe();
        var capture = new ManifestWorkingTreeReader(probe).CaptureAsync(new ManifestImportRequest
        {
            RepositoryRoot = Directory.GetCurrentDirectory(),
            Mode = ManifestImportMode.UncommittedPreview,
            MaxFileBytes = 1024,
            MaxTotalBytes = 2048
        }).GetAwaiter().GetResult();

        TestAssert.False(capture.IsStable, "A manifest mutation between pre and post reads must reject the working-tree preview.");
        TestAssert.True(capture.Diagnostics.Any(diagnostic => diagnostic.Code == "PMC-SNAPSHOT-002"), "Manifest mutation must produce the snapshot stability diagnostic.");
        TestAssert.True(probe.ManifestReadCount >= 2, "Pre and post capture must independently read the manifest.");
    }

    public static void OversizedGitBlobIsRejectedBeforeBodyRead()
    {
        var runner = new FakeGitCommandRunner
        {
            PayloadMode = "100644",
            PayloadSize = 2048,
            ThrowIfPayloadBodyRead = true
        };
        var capture = new ManifestGitObjectReader(runner).CaptureAsync(new ManifestImportRequest
        {
            RepositoryRoot = Directory.GetCurrentDirectory(),
            Mode = ManifestImportMode.GitCommit,
            RequestedCommit = FakeCommit,
            MaxFileBytes = 1024,
            MaxTotalBytes = 4096
        }).GetAwaiter().GetResult();

        TestAssert.False(runner.BodyReadPaths.Contains("payload.txt"), "An oversized Git blob must be rejected before git show reads its body.");
        TestAssert.True(capture.Diagnostics.Any(diagnostic => diagnostic.Message.Contains("blob size", StringComparison.OrdinalIgnoreCase)), "The capture must explain the bounded blob-size rejection.");
    }

    public static void SymlinkGitTreeEntryIsRejectedBeforeBodyRead()
    {
        var runner = new FakeGitCommandRunner
        {
            PayloadMode = "120000",
            PayloadSize = 32,
            ThrowIfPayloadBodyRead = true
        };
        var capture = new ManifestGitObjectReader(runner).CaptureAsync(new ManifestImportRequest
        {
            RepositoryRoot = Directory.GetCurrentDirectory(),
            Mode = ManifestImportMode.GitCommit,
            RequestedCommit = FakeCommit,
            MaxFileBytes = 1024,
            MaxTotalBytes = 4096
        }).GetAwaiter().GetResult();

        TestAssert.False(runner.BodyReadPaths.Contains("payload.txt"), "A symlink Git entry must be rejected before git show reads its body.");
        TestAssert.True(capture.Diagnostics.Any(diagnostic => diagnostic.Message.Contains("symlink", StringComparison.OrdinalIgnoreCase)), "The capture must explain the symlink rejection.");
    }

    public static void CallerGitLimitCannotBypassApplicationHardCeiling()
    {
        var runner = new FakeGitCommandRunner
        {
            PayloadMode = "100644",
            PayloadSize = ManifestImportRequest.HardMaxFileBytes + 1,
            ThrowIfPayloadBodyRead = true
        };
        var capture = new ManifestGitObjectReader(runner).CaptureAsync(new ManifestImportRequest
        {
            RepositoryRoot = Directory.GetCurrentDirectory(),
            Mode = ManifestImportMode.GitCommit,
            RequestedCommit = FakeCommit,
            MaxFileBytes = ManifestImportRequest.HardMaxFileBytes * 4,
            MaxTotalBytes = ManifestImportRequest.HardMaxTotalBytes * 4
        }).GetAwaiter().GetResult();

        TestAssert.False(runner.BodyReadPaths.Contains("payload.txt"), "Caller-configured Git limits must not bypass the application hard ceiling.");
        TestAssert.True(capture.Diagnostics.Any(diagnostic => diagnostic.Message.Contains("blob size", StringComparison.OrdinalIgnoreCase)), "The hard ceiling rejection must be reported before body capture.");
    }

    private const string FakeCommit = "0123456789012345678901234567890123456789";

    private sealed class SequencedWorkingTreeProbe : IManifestWorkingTreeProbe
    {
        private int manifestReadCount;

        public int ManifestReadCount => manifestReadCount;

        public ManifestSourceFile ReadFile(string repositoryRoot, string relativePath, long maxFileBytes, long remainingTotalBytes)
        {
            if (string.Equals(relativePath, "planning/project-management-compiler-manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                var version = Interlocked.Increment(ref manifestReadCount);
                var content = version == 1
                    ? "{\"version\":1,\"sources\":[{\"path\":\"payload.txt\"}]}"
                    : "{\"version\":2,\"sources\":[{\"path\":\"payload.txt\"}]}";
                return File(relativePath, content);
            }

            return File(relativePath, "payload");
        }

        public Task<string> ReadGitStateAsync(string repositoryRoot, CancellationToken cancellationToken) =>
            Task.FromResult("HEAD\nstatus");

        private static ManifestSourceFile File(string path, string content) => new()
        {
            RelativePath = path,
            Content = content,
            SizeBytes = Encoding.UTF8.GetByteCount(content),
            Format = SourceDocumentFormat.Text
        };
    }

    private sealed class UnexpectedGitStateProbe : IManifestWorkingTreeProbe
    {
        public ManifestSourceFile ReadFile(string repositoryRoot, string relativePath, long maxFileBytes, long remainingTotalBytes) => new()
        {
            RelativePath = relativePath,
            Content = "{\"sources\":[]}",
            SizeBytes = 15,
            Format = SourceDocumentFormat.Text
        };

        public Task<string> ReadGitStateAsync(string repositoryRoot, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("git executable could not be started");
    }

    private sealed class FakeGitCommandRunner : IManifestGitCommandRunner
    {
        public string PayloadMode { get; init; } = "100644";
        public long PayloadSize { get; init; }
        public bool ThrowIfPayloadBodyRead { get; init; }
        public List<string> BodyReadPaths { get; } = [];

        public Task<(string Stdout, string Stderr)> RunAsync(
            string repositoryRoot,
            IReadOnlyList<string> arguments,
            long maxOutputBytes,
            CancellationToken cancellationToken)
        {
            var command = arguments[0];
            if (command == "remote")
            {
                return Task.FromResult(("https://example.com/ideaengineering.git\n", string.Empty));
            }

            if (command == "rev-parse")
            {
                return Task.FromResult(($"{FakeCommit}\n", string.Empty));
            }

            if (command == "ls-tree")
            {
                var path = arguments[^1];
                var mode = string.Equals(path, "payload.txt", StringComparison.Ordinal)
                    ? PayloadMode
                    : "100644";
                return Task.FromResult(($"{mode} blob abcdefabcdefabcdefabcdefabcdefabcdefabcd\t{path}\0", string.Empty));
            }

            if (command == "cat-file" && arguments[1] == "-t")
            {
                return Task.FromResult(("blob\n", string.Empty));
            }

            if (command == "cat-file" && arguments[1] == "-s")
            {
                var path = arguments[2].Split(':', 2)[1];
                var size = string.Equals(path, "payload.txt", StringComparison.Ordinal) ? PayloadSize : 32;
                return Task.FromResult(($"{size}\n", string.Empty));
            }

            if (command == "show")
            {
                var path = arguments[1].Split(':', 2)[1];
                if (string.Equals(path, "payload.txt", StringComparison.Ordinal))
                {
                    BodyReadPaths.Add(path);
                    if (ThrowIfPayloadBodyRead)
                    {
                        throw new InvalidOperationException("payload body was read");
                    }
                }

                var manifest = "{\"sources\":[{\"path\":\"payload.txt\"}]}";
                return Task.FromResult((string.Equals(path, "payload.txt", StringComparison.Ordinal) ? "payload" : manifest, string.Empty));
            }

            throw new InvalidOperationException($"Unexpected fake Git command: {string.Join(' ', arguments)}");
        }
    }
}
