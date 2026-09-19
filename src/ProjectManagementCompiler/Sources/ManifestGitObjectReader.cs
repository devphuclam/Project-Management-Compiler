using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Sources;

public sealed class ManifestGitObjectReader : IManifestSourceReader
{
    public async Task<ManifestSourceCapture> CaptureAsync(
        ManifestImportRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = new List<ManifestDiagnostic>();
        if (!TryValidateRequest(request, diagnostics))
        {
            return Failed(request, diagnostics);
        }

        try
        {
            var remote = await RunGitAsync(request.RepositoryRoot, ["remote", "get-url", "origin"], request.MaxFileBytes, cancellationToken);
            var repositoryIdentity = SafeRepositoryIdentity(remote.Stdout);
            var commit = await RunGitAsync(
                request.RepositoryRoot,
                ["rev-parse", "--verify", $"{request.RequestedCommit}^{'{'}commit{'}'}"],
                256,
                cancellationToken);
            var resolvedCommit = commit.Stdout.Trim();
            if (!IsSha(resolvedCommit))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-SNAPSHOT-002",
                    WarningSeverity.Error,
                    "The requested Git object does not resolve to a commit.",
                    recommendedAction: "Provide an exact commit object from the source repository."));
                return Failed(request, diagnostics, repositoryIdentity);
            }

            var manifest = await ReadBlobAsync(
                request.RepositoryRoot,
                resolvedCommit,
                ManifestCaptureSupport.SupportedManifestPath,
                request.MaxFileBytes,
                cancellationToken);
            IReadOnlyList<string> declaredPaths;
            try
            {
                declaredPaths = ManifestCaptureSupport.ReadDeclaredPaths(manifest);
            }
            catch (ManifestUnsafePathException exception)
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-PATH-001",
                    WarningSeverity.Error,
                    exception.Message,
                    exception.PathValue,
                    recommendedAction: "Use a repository-relative manifest path inside the source repository."));
                return Failed(request, diagnostics, repositoryIdentity, resolvedCommit);
            }
            catch (JsonException exception)
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-SCHEMA-001",
                    WarningSeverity.Error,
                    $"The source manifest is not valid JSON: {exception.Message}",
                    ManifestCaptureSupport.SupportedManifestPath,
                    recommendedAction: "Repair the manifest JSON before importing."));
                return Failed(request, diagnostics, repositoryIdentity, resolvedCommit);
            }

            var files = new Dictionary<string, ManifestSourceFile>(StringComparer.OrdinalIgnoreCase)
            {
                [ManifestCaptureSupport.SupportedManifestPath] = ToFile(ManifestCaptureSupport.SupportedManifestPath, manifest)
            };
            long totalBytes = files[ManifestCaptureSupport.SupportedManifestPath].SizeBytes;
            foreach (var path in declaredPaths.Where(path => !string.Equals(path, ManifestCaptureSupport.SupportedManifestPath, StringComparison.OrdinalIgnoreCase)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var content = await ReadBlobAsync(request.RepositoryRoot, resolvedCommit, path, request.MaxFileBytes, cancellationToken);
                var file = ToFile(path, content);
                totalBytes = checked(totalBytes + file.SizeBytes);
                if (totalBytes > request.MaxTotalBytes)
                {
                    diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                        "PMC-PATH-002",
                        WarningSeverity.Error,
                        $"The manifest-declared source exceeds the {request.MaxTotalBytes} byte total limit.",
                        path,
                        recommendedAction: "Reduce the selected source boundary or increase the bounded limit explicitly."));
                    return Failed(request, diagnostics, repositoryIdentity, resolvedCommit);
                }

                files[path] = file;
            }

            var fixtureCataloguePath = ManifestCaptureSupport.ReadFixtureCataloguePath(manifest);
            if (fixtureCataloguePath is not null
                && files.TryGetValue(fixtureCataloguePath, out var fixtureCatalogue))
            {
                foreach (var path in ManifestCaptureSupport.ReadFixturePaths(fixtureCatalogue.Content)
                    .Where(path => !files.ContainsKey(path)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var content = await ReadBlobAsync(request.RepositoryRoot, resolvedCommit, path, request.MaxFileBytes, cancellationToken);
                    var file = ToFile(path, content);
                    totalBytes = checked(totalBytes + file.SizeBytes);
                    if (totalBytes > request.MaxTotalBytes)
                    {
                        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                            "PMC-PATH-002",
                            WarningSeverity.Error,
                            $"The manifest-declared fixture source exceeds the {request.MaxTotalBytes} byte total limit.",
                            path,
                            recommendedAction: "Reduce the selected source boundary or increase the bounded limit explicitly."));
                        return Failed(request, diagnostics, repositoryIdentity, resolvedCommit);
                    }

                    files[path] = file;
                }
            }

            return new ManifestSourceCapture
            {
                RepositoryIdentity = repositoryIdentity,
                ResolvedCommit = resolvedCommit,
                PreviewIdentity = resolvedCommit,
                Mode = ManifestImportMode.GitCommit,
                IsStable = true,
                Files = files,
                Diagnostics = diagnostics
            };
        }
        catch (ManifestUnsafePathException exception)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-PATH-001",
                WarningSeverity.Error,
                exception.Message,
                exception.PathValue,
                recommendedAction: "Use a repository-relative manifest or fixture path inside the source repository."));
            return Failed(request, diagnostics);
        }
        catch (GitCommandException exception)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SNAPSHOT-002",
                WarningSeverity.Error,
                exception.Message,
                recommendedAction: "Verify the repository, commit object, and Git availability."));
            return Failed(request, diagnostics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-PATH-002",
                WarningSeverity.Error,
                exception.Message,
                recommendedAction: "Restore the manifest-declared source files and retry the import."));
            return Failed(request, diagnostics);
        }
    }

    internal static async Task<(string Stdout, string Stderr)> RunGitAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        long maxOutputBytes,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false, true),
            StandardErrorEncoding = new UTF8Encoding(false, true),
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new GitCommandException("Git could not be started.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (Encoding.UTF8.GetByteCount(stdout) > maxOutputBytes)
        {
            throw new IOException($"Git output exceeded the {maxOutputBytes} byte limit.");
        }

        if (process.ExitCode != 0)
        {
            throw new GitCommandException(string.IsNullOrWhiteSpace(stderr) ? "Git command failed." : stderr.Trim());
        }

        return (stdout, stderr);
    }

    internal static async Task<string> ReadBlobAsync(
        string repositoryRoot,
        string commit,
        string relativePath,
        long maxFileBytes,
        CancellationToken cancellationToken)
    {
        var type = await RunGitAsync(repositoryRoot, ["cat-file", "-t", $"{commit}:{relativePath}"], 64, cancellationToken);
        if (!string.Equals(type.Stdout.Trim(), "blob", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Manifest-declared source '{relativePath}' is not a regular Git blob.");
        }

        var result = await RunGitAsync(repositoryRoot, ["show", $"{commit}:{relativePath}"], maxFileBytes, cancellationToken);
        var bytes = Encoding.UTF8.GetByteCount(result.Stdout);
        if (bytes > maxFileBytes)
        {
            throw new IOException($"Manifest-declared source '{relativePath}' exceeds the {maxFileBytes} byte limit.");
        }

        return result.Stdout;
    }

    private static bool TryValidateRequest(ManifestImportRequest request, ICollection<ManifestDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryRoot))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic("PMC-PATH-002", WarningSeverity.Error, "A repository root is required."));
        }
        else if (!Directory.Exists(request.RepositoryRoot))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic("PMC-PATH-002", WarningSeverity.Error, "The repository root does not exist."));
        }

        if (!string.Equals(request.ManifestPath.Replace('\\', '/'), ManifestCaptureSupport.SupportedManifestPath, StringComparison.Ordinal))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic("PMC-PATH-001", WarningSeverity.Error, "The supported manifest path is the sole discovery entry point.", request.ManifestPath));
        }

        if (string.IsNullOrWhiteSpace(request.RequestedCommit))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic("PMC-SNAPSHOT-002", WarningSeverity.Error, "An exact Git commit is required for GIT_COMMIT imports."));
        }
        else if (!IsCommitToken(request.RequestedCommit))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic("PMC-SNAPSHOT-002", WarningSeverity.Error, "The requested commit contains unsafe characters."));
        }

        if (request.MaxFileBytes <= 0 || request.MaxTotalBytes <= 0 || request.MaxTotalBytes < request.MaxFileBytes)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic("PMC-PATH-002", WarningSeverity.Error, "Source size limits must be positive and the total limit must cover one file."));
        }

        return diagnostics.Count == 0;
    }

    private static ManifestSourceCapture Failed(
        ManifestImportRequest request,
        IReadOnlyList<ManifestDiagnostic> diagnostics,
        string repositoryIdentity = "IDEAEngineering",
        string? resolvedCommit = null) =>
        new()
        {
            RepositoryIdentity = repositoryIdentity,
            ResolvedCommit = resolvedCommit,
            PreviewIdentity = string.Empty,
            Mode = request.Mode,
            IsStable = false,
            Diagnostics = diagnostics
        };

    private static ManifestSourceFile ToFile(string path, string content) => new()
    {
        RelativePath = path,
        Content = content,
        SizeBytes = Encoding.UTF8.GetByteCount(content),
        Format = ManifestCaptureSupport.GetFormat(path)
    };

    private static string SafeRepositoryIdentity(string remote)
    {
        var value = remote.Trim();
        if (value.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^4];
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath.Trim('/').ToLowerInvariant();
        }

        if (value.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            var separator = value.IndexOf(':');
            if (separator > 0)
            {
                return value[(separator + 1)..].Trim('/').ToLowerInvariant();
            }
        }

        return "IDEAEngineering";
    }

    private static bool IsSha(string value) =>
        value.Length == 40 && value.All(character => Uri.IsHexDigit(character));

    private static bool IsCommitToken(string value) =>
        value.Length is >= 7 and <= 64 && value.All(character => Uri.IsHexDigit(character));

    private sealed class GitCommandException : IOException
    {
        public GitCommandException(string message)
            : base(message)
        {
        }
    }
}
