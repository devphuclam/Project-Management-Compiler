using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Sources;

public sealed class ManifestWorkingTreeReader : IManifestSourceReader
{
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
            var manifest = ReadText(normalizedRoot, ManifestCaptureSupport.SupportedManifestPath, request.MaxFileBytes, request.MaxTotalBytes, out var manifestBytes);
            var paths = ManifestCaptureSupport.ReadDeclaredPaths(manifest);
            var pre = CaptureFiles(normalizedRoot, paths, request, cancellationToken, manifest, manifestBytes);
            paths = ExpandFixturePaths(paths, manifest, pre);
            pre = CaptureFiles(normalizedRoot, paths, request, cancellationToken, manifest, manifestBytes);
            var preFingerprint = Fingerprint(pre);
            var preHeadAndStatus = await GitStateAsync(normalizedRoot, cancellationToken);
            var post = CaptureFiles(normalizedRoot, paths, request, cancellationToken, manifest, manifestBytes);
            var postFingerprint = Fingerprint(post);
            var postHeadAndStatus = await GitStateAsync(normalizedRoot, cancellationToken);
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

    private static Dictionary<string, ManifestSourceFile> CaptureFiles(
        string root,
        IReadOnlyList<string> paths,
        ManifestImportRequest request,
        CancellationToken cancellationToken,
        string manifest,
        long manifestBytes)
    {
        var files = new Dictionary<string, ManifestSourceFile>(StringComparer.OrdinalIgnoreCase)
        {
            [ManifestCaptureSupport.SupportedManifestPath] = ToFile(ManifestCaptureSupport.SupportedManifestPath, manifest, manifestBytes)
        };
        long total = manifestBytes;
        foreach (var path in paths.Where(path => !string.Equals(path, ManifestCaptureSupport.SupportedManifestPath, StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = ReadText(root, path, request.MaxFileBytes, request.MaxTotalBytes - total, out var bytes);
            total = checked(total + bytes);
            files[path] = ToFile(path, content, bytes);
        }

        return files;
    }

    private static string ReadText(string root, string relativePath, long maxFileBytes, long remainingTotalBytes, out long bytes)
    {
        if (!ManifestCaptureSupport.TryNormalizeRelativePath(relativePath, out var normalized))
        {
            throw new ManifestUnsafePathException(relativePath);
        }

        var fullPath = SourcePathPolicy.ResolvePath(root, normalized, new PhysicalManifestFileSystem());
        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException($"Manifest-declared source '{normalized}' does not exist.", fullPath);
        }

        if (!SourcePathPolicy.HasSafeSegments(root, fullPath, new PhysicalManifestFileSystem())
            || File.GetAttributes(fullPath).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new ManifestUnsafePathException(relativePath);
        }

        if (info.Length > maxFileBytes || info.Length > remainingTotalBytes)
        {
            throw new IOException($"Manifest-declared source '{normalized}' exceeds its bounded capture size.");
        }

        var content = File.ReadAllText(fullPath, new UTF8Encoding(false, true));
        bytes = Encoding.UTF8.GetByteCount(content);
        if (bytes > maxFileBytes || bytes > remainingTotalBytes)
        {
            throw new IOException($"Manifest-declared source '{normalized}' exceeds its bounded capture size.");
        }

        return content;
    }

    private static async Task<string> GitStateAsync(string root, CancellationToken cancellationToken)
    {
        try
        {
            var head = await ManifestGitObjectReader.RunGitAsync(root, ["rev-parse", "HEAD"], 256, cancellationToken);
            var status = await ManifestGitObjectReader.RunGitAsync(root, ["status", "--porcelain=v1", "--untracked-files=all"], 4 * 1024 * 1024, cancellationToken);
            return head.Stdout.Trim() + "\n" + status.Stdout;
        }
        catch (IOException)
        {
            return "git-state-unavailable";
        }
    }

    private static string Fingerprint(IReadOnlyDictionary<string, ManifestSourceFile> files)
    {
        var builder = new StringBuilder();
        foreach (var file in files.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(file.Key).Append('\0').Append(file.Value.Content).Append('\0');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static ManifestSourceFile ToFile(string path, string content, long bytes) => new()
    {
        RelativePath = path,
        Content = content,
        SizeBytes = bytes,
        Format = ManifestCaptureSupport.GetFormat(path)
    };

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
