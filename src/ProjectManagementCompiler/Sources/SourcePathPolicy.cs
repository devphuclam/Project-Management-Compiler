namespace ProjectManagementCompiler.Sources;

public static class SourcePathPolicy
{
    public static IReadOnlyList<string> RecognizedPaths { get; } =
    [
        "README.md",
        "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
        "docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md",
        "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html",
        "docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md"
    ];

    public static string ResolvePath(string root, string relativePath, IRepositoryFileSystem fileSystem)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Source path must be a non-rooted relative path.", nameof(relativePath));
        }

        var slashNormalized = relativePath.Replace('\\', '/');
        if (slashNormalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            throw new ArgumentException("Source path traversal is not permitted.", nameof(relativePath));
        }

        var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, slashNormalized));
        var relativeFromRoot = Path.GetRelativePath(normalizedRoot, fullPath);
        if (relativeFromRoot == ".." || relativeFromRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Source path escapes the repository root.", nameof(relativePath));
        }

        return fullPath;
    }

    public static bool HasSafeSegments(string root, string fullPath, IRepositoryFileSystem fileSystem)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fileSystem.IsReparsePoint(normalizedRoot))
        {
            return false;
        }

        var relative = Path.GetRelativePath(normalizedRoot, fullPath);
        var current = normalizedRoot;
        var segments = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            current = Path.Combine(current, segments[index]);
            if (fileSystem.IsReparsePoint(current))
            {
                return false;
            }

            if (index < segments.Length - 1 && !fileSystem.DirectoryExists(current))
            {
                break;
            }
        }

        return true;
    }
}
