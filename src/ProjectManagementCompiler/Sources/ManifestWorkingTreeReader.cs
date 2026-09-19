using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Sources;

public interface IManifestWorkingTreeProbe
{
    ManifestSourceFile ReadFile(
        string repositoryRoot,
        string relativePath,
        long maxFileBytes,
        long remainingTotalBytes);

    Task<string> ReadGitStateAsync(
        string repositoryRoot,
        CancellationToken cancellationToken);
}

public sealed class ManifestWorkingTreeReader : IManifestSourceReader
{
    private readonly IManifestWorkingTreeProbe probe;

    public ManifestWorkingTreeReader(IManifestWorkingTreeProbe? probe = null)
    {
        this.probe = probe ?? new PhysicalManifestWorkingTreeProbe();
    }

    public async Task<ManifestSourceCapture> CaptureAsync(
        ManifestImportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = new List<ManifestDiagnostic>();
        if (string.IsNullOrWhiteSpace(request.RepositoryRoot) || !Directory.Exists(request.RepositoryRoot))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic("PMC-PATH-002", WarningSeverity.Error, "The repository root does not exist."));
            return Failed(request, diagnostics);
        }

        if (!string.Equals(request.ManifestPath.Replace('\\', '/'), ManifestCaptureSupport.SupportedManifestPath, StringComparison.Ordinal))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic("PMC-PATH-001", WarningSeverity.Error, "The supported manifest path is the sole discovery entry point.", request.ManifestPath));
            return Failed(request, diagnostics);
        }

        if (request.MaxFileBytes <= 0 || request.MaxTotalBytes <= 0 || request.MaxTotalBytes < request.MaxFileBytes)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-PATH-002",
                WarningSeverity.Error,
                "Working-tree capture limits are invalid.",
                recommendedAction: "Set positive file and total limits with the total at least as large as the file limit."));
            return Failed(request, diagnostics);
        }

        try
        {
            var normalizedRoot = SourcePathPolicy.NormalizeRoot(request.RepositoryRoot);
            var pre = CaptureSnapshot(normalizedRoot, request, cancellationToken);
            var preFingerprint = Fingerprint(pre);
            var preHeadAndStatus = await ReadVerifiedGitStateAsync(normalizedRoot, cancellationToken);
            var post = CaptureSnapshot(normalizedRoot, request, cancellationToken);
            var postFingerprint = Fingerprint(post);
            var postHeadAndStatus = await ReadVerifiedGitStateAsync(normalizedRoot, cancellationToken);
            var stable = string.Equals(preFingerprint, postFingerprint, StringComparison.Ordinal)
                && string.Equals(preHeadAndStatus, postHeadAndStatus, StringComparison.Ordinal);
            if (!stable)
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-SNAPSHOT-002",
                    WarningSeverity.Error,
                    "The working tree changed while the manifest-declared files were captured.",
                    recommendedAction: "Commit the source or retry the preview after the working tree is stable."));
                return new ManifestSourceCapture
                {
                    RepositoryIdentity = "IDEAEngineering",
                    PreviewIdentity = postFingerprint,
                    Mode = ManifestImportMode.UncommittedPreview,
                    IsStable = false,
                    Diagnostics = diagnostics
                };
            }

            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SNAPSHOT-001",
                WarningSeverity.Warning,
                "This source was captured from an uncommitted working tree and is not an official snapshot.",
                recommendedAction: "Commit and validate the exact source snapshot before treating it as official."));
            return new ManifestSourceCapture
            {
                RepositoryIdentity = "IDEAEngineering",
                PreviewIdentity = postFingerprint,
                Mode = ManifestImportMode.UncommittedPreview,
                IsStable = true,
                Files = post,
                Diagnostics = diagnostics
            };
        }
        catch (ManifestUnsafePathException exception)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic("PMC-PATH-001", WarningSeverity.Error, exception.Message, exception.PathValue));
            return Failed(request, diagnostics);
        }
        catch (GitStateUnavailableException exception)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SNAPSHOT-002",
                WarningSeverity.Error,
                $"Git HEAD/status could not be verified: {exception.Message}",
                recommendedAction: "Run the preview from a readable Git working tree or import an exact commit."));
            return Failed(request, diagnostics);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic("PMC-SCHEMA-001", WarningSeverity.Error, exception.Message, ManifestCaptureSupport.SupportedManifestPath));
            return Failed(request, diagnostics);
        }
        catch (IOException exception)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic("PMC-PATH-002", WarningSeverity.Error, exception.Message));
            return Failed(request, diagnostics);
        }
    }

    private Dictionary<string, ManifestSourceFile> CaptureSnapshot(
        string root,
        ManifestImportRequest request,
        CancellationToken cancellationToken)
    {
        var manifestFile = probe.ReadFile(
            root,
            ManifestCaptureSupport.SupportedManifestPath,
            EffectiveFileLimit(request),
            EffectiveTotalLimit(request));
        var paths = ManifestCaptureSupport.ReadDeclaredPaths(manifestFile.Content);
        var first = CaptureFiles(root, paths, request, cancellationToken, manifestFile);
        paths = ExpandFixturePaths(paths, manifestFile.Content, first);
        return CaptureFiles(root, paths, request, cancellationToken, manifestFile);
    }

    private Dictionary<string, ManifestSourceFile> CaptureFiles(
        string root,
        IReadOnlyList<string> paths,
        ManifestImportRequest request,
        CancellationToken cancellationToken,
        ManifestSourceFile manifestFile)
    {
        var files = new Dictionary<string, ManifestSourceFile>(StringComparer.OrdinalIgnoreCase)
        {
            [ManifestCaptureSupport.SupportedManifestPath] = manifestFile
        };
        long total = manifestFile.SizeBytes;
        foreach (var path in paths.Where(path => !string.Equals(path, ManifestCaptureSupport.SupportedManifestPath, StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = probe.ReadFile(root, path, EffectiveFileLimit(request), EffectiveTotalLimit(request) - total);
            total = checked(total + file.SizeBytes);
            files[path] = file;
        }

        return files;
    }

    private async Task<string> ReadVerifiedGitStateAsync(string root, CancellationToken cancellationToken)
    {
        try
        {
            var state = await probe.ReadGitStateAsync(root, cancellationToken);
            if (string.IsNullOrWhiteSpace(state))
            {
                throw new IOException("Git state command returned no verifiable state.");
            }

            return state;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new GitStateUnavailableException(exception.Message, exception);
        }
    }

    private static long EffectiveFileLimit(ManifestImportRequest request) =>
        Math.Min(request.MaxFileBytes, ManifestImportRequest.HardMaxFileBytes);

    private static long EffectiveTotalLimit(ManifestImportRequest request) =>
        Math.Min(request.MaxTotalBytes, ManifestImportRequest.HardMaxTotalBytes);

    private static string Fingerprint(IReadOnlyDictionary<string, ManifestSourceFile> files)
    {
        var builder = new StringBuilder();
        foreach (var file in files.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(file.Key).Append('\0').Append(file.Value.Content).Append('\0');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static IReadOnlyList<string> ExpandFixturePaths(
        IReadOnlyList<string> paths,
        string manifest,
        IReadOnlyDictionary<string, ManifestSourceFile> files)
    {
        var cataloguePath = ManifestCaptureSupport.ReadFixtureCataloguePath(manifest);
        if (cataloguePath is null || !files.TryGetValue(cataloguePath, out var catalogue))
        {
            return paths;
        }

        return paths
            .Concat(ManifestCaptureSupport.ReadFixturePaths(catalogue.Content))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ManifestSourceCapture Failed(ManifestImportRequest request, IReadOnlyList<ManifestDiagnostic> diagnostics) => new()
    {
        RepositoryIdentity = "IDEAEngineering",
        Mode = request.Mode,
        IsStable = false,
        Diagnostics = diagnostics
    };

    private sealed class PhysicalManifestWorkingTreeProbe : IManifestWorkingTreeProbe
    {
        public ManifestSourceFile ReadFile(string repositoryRoot, string relativePath, long maxFileBytes, long remainingTotalBytes)
        {
            if (!ManifestCaptureSupport.TryNormalizeRelativePath(relativePath, out var normalized))
            {
                throw new ManifestUnsafePathException(relativePath);
            }

            var fullPath = SourcePathPolicy.ResolvePath(repositoryRoot, normalized, new PhysicalManifestFileSystem());
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                throw new FileNotFoundException($"Manifest-declared source '{normalized}' does not exist.", fullPath);
            }

            if (!SourcePathPolicy.HasSafeSegments(repositoryRoot, fullPath, new PhysicalManifestFileSystem())
                || File.GetAttributes(fullPath).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new ManifestUnsafePathException(relativePath);
            }

            if (info.Length > maxFileBytes || info.Length > remainingTotalBytes)
            {
                throw new IOException($"Manifest-declared source '{normalized}' exceeds its bounded capture size.");
            }

            var content = File.ReadAllText(fullPath, new UTF8Encoding(false, true));
            var bytes = Encoding.UTF8.GetByteCount(content);
            if (bytes > maxFileBytes || bytes > remainingTotalBytes)
            {
                throw new IOException($"Manifest-declared source '{normalized}' exceeds its bounded capture size.");
            }

            return new ManifestSourceFile
            {
                RelativePath = normalized,
                Content = content,
                SizeBytes = bytes,
                Format = ManifestCaptureSupport.GetFormat(normalized)
            };
        }

        public async Task<string> ReadGitStateAsync(string repositoryRoot, CancellationToken cancellationToken)
        {
            try
            {
                var head = await ManifestGitObjectReader.RunGitAsync(repositoryRoot, ["rev-parse", "HEAD"], 256, cancellationToken);
                var status = await ManifestGitObjectReader.RunGitAsync(repositoryRoot, ["status", "--porcelain=v1", "--untracked-files=all"], 4 * 1024 * 1024, cancellationToken);
                return head.Stdout.Trim() + "\n" + status.Stdout;
            }
            catch (IOException exception)
            {
                throw new GitStateUnavailableException(exception.Message, exception);
            }
        }

        private sealed class PhysicalManifestFileSystem : IRepositoryFileSystem
        {
            public bool DirectoryExists(string path) => Directory.Exists(path);
            public bool FileExists(string path) => File.Exists(path);
            public bool IsReparsePoint(string path)
            {
                try
                {
                    return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
                {
                    return true;
                }
            }

            public RepositoryFileReadResult ReadFile(string allowedRoot, string path, Encoding encoding, long maxFileBytes, long remainingTotalBytes) =>
                throw new NotSupportedException("Working tree capture uses the manifest-specific bounded reader.");
        }
    }

    private sealed class GitStateUnavailableException : IOException
    {
        public GitStateUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
