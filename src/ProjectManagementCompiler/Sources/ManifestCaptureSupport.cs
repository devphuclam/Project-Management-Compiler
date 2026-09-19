using System.Text.Json;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Sources;

internal static class ManifestCaptureSupport
{
    public const string SupportedManifestPath = "planning/project-management-compiler-manifest.json";
    public const string SupportedContractVersion = "0.1.0";

    public static bool TryNormalizeRelativePath(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || Path.IsPathRooted(value)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith("\\", StringComparison.Ordinal))
        {
            return false;
        }

        var slashValue = value.Replace('\\', '/');
        var segments = slashValue.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0
            || segments.Any(segment => segment is "." or ".."
                || segment.Contains(':', StringComparison.Ordinal)
                || segment.Any(char.IsControl)))
        {
            return false;
        }

        normalized = string.Join('/', segments);
        return true;
    }

    public static IReadOnlyList<string> ReadDeclaredPaths(string manifestContent)
    {
        using var document = JsonDocument.Parse(manifestContent);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The source manifest must be a JSON object.");
        }

        var paths = new List<string>();
        AddString(document.RootElement, "activeBaseline", "referencePath", paths);
        AddString(document.RootElement, "execution", "registerPath", paths);
        AddString(document.RootElement, "execution", "schemaPath", paths);
        AddString(document.RootElement, null, "calendarPath", paths);
        AddString(document.RootElement, null, "diagnosticCataloguePath", paths);
        AddString(document.RootElement, null, "sourceContractPath", paths);
        AddString(document.RootElement, null, "fixtureCataloguePath", paths);

        if (document.RootElement.TryGetProperty("sources", out var sources)
            && sources.ValueKind == JsonValueKind.Array)
        {
            foreach (var source in sources.EnumerateArray())
            {
                if (source.ValueKind == JsonValueKind.Object
                    && source.TryGetProperty("path", out var path)
                    && path.ValueKind == JsonValueKind.String)
                {
                    paths.Add(path.GetString() ?? string.Empty);
                }
            }
        }

        return paths
            .Select(path =>
            {
                if (!TryNormalizeRelativePath(path, out var normalized))
                {
                    throw new ManifestUnsafePathException(path);
                }

                return normalized;
            })
            .Append(SupportedManifestPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? ReadFixtureCataloguePath(string manifestContent)
    {
        using var document = JsonDocument.Parse(manifestContent);
        return document.RootElement.ValueKind == JsonValueKind.Object
            ? GetString(document.RootElement, "fixtureCataloguePath")
            : null;
    }

    public static IReadOnlyList<string> ReadFixturePaths(string catalogueContent)
    {
        using var document = JsonDocument.Parse(catalogueContent);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("fixtures", out var fixtures)
            || fixtures.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var paths = new List<string>();
        foreach (var fixture in fixtures.EnumerateArray())
        {
            if (fixture.ValueKind != JsonValueKind.Object
                || !fixture.TryGetProperty("path", out var path)
                || path.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var rawPath = path.GetString() ?? string.Empty;
            if (!TryNormalizeRelativePath(rawPath, out var normalized))
            {
                throw new ManifestUnsafePathException(rawPath);
            }

            paths.Add(normalized);
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string? GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    public static string? GetNestedString(JsonElement root, string objectName, string propertyName)
    {
        return root.TryGetProperty(objectName, out var child)
            && child.ValueKind == JsonValueKind.Object
            ? GetString(child, propertyName)
            : null;
    }

    public static SourceDocumentFormat GetFormat(string path) =>
        Path.GetExtension(path).Equals(".html", StringComparison.OrdinalIgnoreCase)
            ? SourceDocumentFormat.Html
            : Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase)
                ? SourceDocumentFormat.Markdown
                : SourceDocumentFormat.Text;

    public static ManifestDiagnostic Diagnostic(
        string code,
        WarningSeverity severity,
        string message,
        string? sourcePath = null,
        string? field = null,
        string? entityKind = null,
        string? entityId = null,
        string? recommendedAction = null) =>
        new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            SourcePath = sourcePath,
            Field = field,
            EntityKind = entityKind,
            EntityId = entityId,
            RecommendedAction = recommendedAction ?? "Inspect the declared source contract and correct the candidate."
        };

    private static void AddString(
        JsonElement root,
        string? objectName,
        string propertyName,
        ICollection<string> paths)
    {
        var value = objectName is null
            ? GetString(root, propertyName)
            : GetNestedString(root, objectName, propertyName);
        if (value is not null)
        {
            paths.Add(value);
        }
    }
}

internal sealed class ManifestUnsafePathException : IOException
{
    public ManifestUnsafePathException(string path)
        : base($"Manifest path '{path}' is absolute, escaping, or otherwise unsafe.")
    {
        PathValue = path;
    }

    public string PathValue { get; }
}
