using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Outputs;

public sealed class CanonicalJsonSerializer
{
    private static readonly string[] RequiredTopLevelFields =
    [
        "project",
        "sources",
        "baseline",
        "phases",
        "workPackages",
        "deliveryCards",
        "milestones",
        "dependencies",
        "responsibilityRoles",
        "assignments",
        "capacity",
        "reserve",
        "policies",
        "provenance",
        "warnings",
        "analysis"
    ];

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public string Serialize(CanonicalProject project) =>
        JsonSerializer.Serialize(project, Options);

    public CanonicalProject Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Canonical JSON root must be an object.");
        }

        if (!document.RootElement.TryGetProperty("schemaVersion", out var schemaVersion)
            || schemaVersion.ValueKind != JsonValueKind.String
            || !string.Equals(schemaVersion.GetString(), "1.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unsupported or missing canonical schemaVersion; expected '1.0'.");
        }

        foreach (var requiredField in RequiredTopLevelFields)
        {
            if (!document.RootElement.TryGetProperty(requiredField, out _))
            {
                throw new InvalidDataException($"Canonical JSON is missing required field '{requiredField}'.");
            }
        }

        var project = JsonSerializer.Deserialize<CanonicalProject>(json, Options)
            ?? throw new InvalidDataException("Canonical JSON did not contain a project document.");

        var reopened = project with
        {
            ExecutionOverlay = project.ExecutionOverlay ?? new ExecutionOverlay()
        };

        var diagnostics = CanonicalProjectValidator.Validate(reopened);
        if (diagnostics.Any(diagnostic => diagnostic.Severity == WarningSeverity.Error))
        {
            throw new InvalidDataException(string.Join(" ", diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return reopened;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
        return options;
    }
}
