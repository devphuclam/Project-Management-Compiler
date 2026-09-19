using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
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
        SerializeCanonical(project);

    public CanonicalProject Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Canonical JSON root must be an object.");
        }

        var schemaVersion = document.RootElement.TryGetProperty("schema", out var schema)
            ? schema
            : document.RootElement.TryGetProperty("schemaVersion", out var legacySchema)
                ? legacySchema
                : default;
        if (schemaVersion.ValueKind != JsonValueKind.String
            || schemaVersion.GetString() is not ("1.0" or "2.0"))
        {
            throw new InvalidDataException("Unsupported or missing canonical schema; expected '1.0' or '2.0'.");
        }

        var version = schemaVersion.GetString()!;

        foreach (var requiredField in RequiredTopLevelFields)
        {
            if (!document.RootElement.TryGetProperty(requiredField, out _))
            {
                throw new InvalidDataException($"Canonical JSON is missing required field '{requiredField}'.");
            }
        }

        if (version == "2.0")
        {
            foreach (var requiredField in new[] { "importMetadata", "sourceExecution", "executionProposals" })
            {
                if (!document.RootElement.TryGetProperty(requiredField, out _))
                {
                    throw new InvalidDataException($"Canonical schema 2.0 is missing required field '{requiredField}'.");
                }
            }
        }

        var deserializeJson = version == "2.0"
            ? AddLegacySchemaVersion(json)
            : json;
        var project = JsonSerializer.Deserialize<CanonicalProject>(deserializeJson, Options)
            ?? throw new InvalidDataException("Canonical JSON did not contain a project document.");

        var reopened = project with
        {
            ExecutionOverlay = project.ExecutionOverlay ?? new ExecutionOverlay()
        };

        var migrated = version == "1.0" ? MigrateLegacyOverlay(reopened) : reopened;

        var diagnostics = CanonicalProjectValidator.Validate(migrated);
        if (diagnostics.Any(diagnostic => diagnostic.Severity == WarningSeverity.Error))
        {
            throw new InvalidDataException(string.Join(" ", diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return migrated;
    }

    private static string SerializeCanonical(CanonicalProject project)
    {
        var json = JsonSerializer.Serialize(project, Options);
        if (!string.Equals(project.SchemaVersion, "2.0", StringComparison.Ordinal))
        {
            return json;
        }

        var node = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidDataException("Canonical JSON could not be represented as an object.");
        node.Remove("schemaVersion");
        node["schema"] = "2.0";
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static string AddLegacySchemaVersion(string json)
    {
        var node = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidDataException("Canonical JSON could not be represented as an object.");
        if (!node.ContainsKey("schemaVersion"))
        {
            node["schemaVersion"] = "2.0";
        }

        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static CanonicalProject MigrateLegacyOverlay(CanonicalProject project)
    {
        if (project.ExecutionOverlay.Records.Count == 0)
        {
            return project;
        }

        var proposals = project.ExecutionProposals
            .Concat(project.ExecutionOverlay.Records.Select(record => new ExecutionProposal
            {
                Id = LegacyProposalId(record),
                BaseSnapshotId = "legacy-schema-1.0",
                ExpectedRegisterRevision = 0,
                TargetKind = "DeliveryCard",
                TargetId = record.WorkItemId,
                Lifecycle = ProposalLifecycle.Draft,
                ProposedChanges = new Dictionary<string, string?>
                {
                    ["executionState"] = record.ExecutionState.ToString().ToUpperInvariant(),
                    ["actualStart"] = record.ActualStart?.ToString("yyyy-MM-dd"),
                    ["actualFinish"] = record.ActualFinish?.ToString("yyyy-MM-dd"),
                    ["actualEffortHours"] = record.ActualEffortHours?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["remainingEffortHours"] = record.RemainingEffortHours?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                },
                CreatedAtUtc = record.LastUpdatedAt ?? DateTimeOffset.UnixEpoch,
                UpdatedAtUtc = record.LastUpdatedAt ?? DateTimeOffset.UnixEpoch,
                Diagnostics =
                [new ManifestDiagnostic
                {
                    Code = "PMC-MIGRATION-001",
                    Severity = WarningSeverity.Warning,
                    Message = "Schema 1.0 execution overlay was reopened as a local proposal; it was not promoted to source execution.",
                    EntityKind = "DeliveryCard",
                    EntityId = record.WorkItemId,
                    RecommendedAction = "Review the proposal against an official manifest snapshot before treating it as current."
                }]
            }))
            .ToArray();

        return project with
        {
            ExecutionOverlay = new ExecutionOverlay(),
            ExecutionProposals = proposals,
            Warnings = project.Warnings.Concat(
                [new ImportWarning
                {
                    Id = "PMC-MIGRATION-001",
                    Severity = WarningSeverity.Warning,
                    Code = "PMC-MIGRATION-001",
                    Message = "Schema 1.0 execution overlay was retained for compatibility and migrated into local proposals; no source execution actuals were fabricated."
                }]).GroupBy(warning => warning.Id, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray()
        };
    }

    private static string LegacyProposalId(ExecutionRecord record)
    {
        var material = $"legacy-schema-1.0|{record.WorkItemId}|{record.ExecutionState}|{record.ActualStart:yyyy-MM-dd}|{record.ActualFinish:yyyy-MM-dd}|{record.ActualEffortHours}|{record.RemainingEffortHours}";
        return $"proposal-migrated-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant()[..20]}";
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
