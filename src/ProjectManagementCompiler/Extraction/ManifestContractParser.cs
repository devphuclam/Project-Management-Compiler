using System.Text.Json;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Extraction;

public sealed record ManifestExpectedTotals
{
    public int Phases { get; init; }
    public int WorkPackages { get; init; }
    public int DeliveryCards { get; init; }
    public int GatesAndMilestones { get; init; }
    public decimal PlannedWorkHours { get; init; }
    public decimal ControlledReserveHours { get; init; }
    public decimal TotalBaselineCapacityHours { get; init; }
}

public sealed record ManifestContractDocument
{
    public string ContractVersion { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string BaselineId { get; init; } = string.Empty;
    public string? BaselineReferencePath { get; init; }
    public string ExecutionRegisterPath { get; init; } = string.Empty;
    public string ExecutionSchemaPath { get; init; } = string.Empty;
    public int ExpectedRegisterRevision { get; init; }
    public string CalendarPath { get; init; } = string.Empty;
    public string SourceContractPath { get; init; } = string.Empty;
    public string DiagnosticCataloguePath { get; init; } = string.Empty;
    public string FixtureCataloguePath { get; init; } = string.Empty;
    public IReadOnlyDictionary<ManifestSourceRole, string> RolePaths { get; init; } =
        new Dictionary<ManifestSourceRole, string>();
    public ManifestExpectedTotals ExpectedTotals { get; init; } = new();
    public SourceReadinessState SourceReadiness { get; init; } = SourceReadinessState.Unknown;
    public ManifestValidationResult DeclaredValidationResult { get; init; } = ManifestValidationResult.Fail;
    public IReadOnlyList<string> OpenWarnings { get; init; } = Array.Empty<string>();
}

public sealed record ManifestContractParseResult
{
    public ManifestContractDocument? Contract { get; init; }
    public IReadOnlyList<ManifestDiagnostic> Diagnostics { get; init; } = Array.Empty<ManifestDiagnostic>();
}

public sealed class ManifestContractParser
{
    private static readonly IReadOnlyDictionary<string, ManifestSourceRole> Roles =
        new Dictionary<string, ManifestSourceRole>(StringComparer.OrdinalIgnoreCase)
        {
            ["ROADMAP_AUTHORITY"] = ManifestSourceRole.RoadmapAuthority,
            ["WORK_PACKAGE_AUTHORITY"] = ManifestSourceRole.WorkPackageAuthority,
            ["DELIVERY_CARD_AUTHORITY"] = ManifestSourceRole.DeliveryCardAuthority,
            ["EXECUTION_AUTHORITY"] = ManifestSourceRole.ExecutionAuthority,
            ["RENDITION_CROSS_CHECK"] = ManifestSourceRole.RenditionCrossCheck,
            ["READINESS_EVIDENCE"] = ManifestSourceRole.ReadinessEvidence,
            ["NAVIGATION_ONLY"] = ManifestSourceRole.NavigationOnly
        };

    public ManifestContractParseResult Parse(ManifestSourceCapture capture)
    {
        var diagnostics = new List<ManifestDiagnostic>();
        if (!capture.Files.TryGetValue(ManifestCaptureSupport.SupportedManifestPath, out var manifestFile))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-PATH-002",
                WarningSeverity.Error,
                "The sole manifest entry point was not captured.",
                ManifestCaptureSupport.SupportedManifestPath,
                recommendedAction: "Restore planning/project-management-compiler-manifest.json."));
            return new ManifestContractParseResult { Diagnostics = diagnostics };
        }

        try
        {
            using var document = JsonDocument.Parse(manifestFile.Content);
            var root = document.RootElement;
            var contractVersion = ManifestCaptureSupport.GetString(root, "contractVersion") ?? string.Empty;
            if (!string.Equals(contractVersion, ManifestCaptureSupport.SupportedContractVersion, StringComparison.Ordinal))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-CONTRACT-002",
                    WarningSeverity.Error,
                    $"Source contract '{contractVersion}' is not explicitly supported.",
                    ManifestCaptureSupport.SupportedManifestPath,
                    "contractVersion",
                    recommendedAction: "Use contract 0.1.0 or add a reviewed compatibility contract."));
            }

            var projectId = RequiredString(root, "projectId", diagnostics);
            var projectName = RequiredString(root, "projectName", diagnostics);
            var baseline = Child(root, "activeBaseline", diagnostics);
            var execution = Child(root, "execution", diagnostics);
            var expectedTotals = Child(root, "expectedSourceTotals", diagnostics);
            var readiness = Child(root, "sourceReadiness", diagnostics);
            var rolePaths = ParseRoles(root, diagnostics);

            var parsed = new ManifestContractDocument
            {
                ContractVersion = contractVersion,
                ProjectId = projectId,
                ProjectName = projectName,
                BaselineId = RequiredString(baseline, "baselineId", diagnostics),
                BaselineReferencePath = OptionalString(baseline, "referencePath"),
                ExecutionRegisterPath = RequiredString(execution, "registerPath", diagnostics),
                ExecutionSchemaPath = RequiredString(execution, "schemaPath", diagnostics),
                ExpectedRegisterRevision = RequiredInt(execution, "expectedRegisterRevision", diagnostics),
                CalendarPath = RequiredString(root, "calendarPath", diagnostics),
                SourceContractPath = RequiredString(root, "sourceContractPath", diagnostics),
                DiagnosticCataloguePath = RequiredString(root, "diagnosticCataloguePath", diagnostics),
                FixtureCataloguePath = RequiredString(root, "fixtureCataloguePath", diagnostics),
                RolePaths = rolePaths,
                ExpectedTotals = new ManifestExpectedTotals
                {
                    Phases = RequiredInt(expectedTotals, "phases", diagnostics),
                    WorkPackages = RequiredInt(expectedTotals, "workPackages", diagnostics),
                    DeliveryCards = RequiredInt(expectedTotals, "deliveryCards", diagnostics),
                    GatesAndMilestones = RequiredInt(expectedTotals, "gatesAndMilestones", diagnostics),
                    PlannedWorkHours = RequiredDecimal(expectedTotals, "plannedWorkHours", diagnostics),
                    ControlledReserveHours = RequiredDecimal(expectedTotals, "controlledReserveHours", diagnostics),
                    TotalBaselineCapacityHours = RequiredDecimal(expectedTotals, "totalBaselineCapacityHours", diagnostics)
                },
                SourceReadiness = ParseReadinessState(readiness, diagnostics),
                DeclaredValidationResult = ParseValidationResult(readiness, diagnostics),
                OpenWarnings = ParseWarnings(readiness)
            };

            ValidatePathsAndPresence(parsed, capture, diagnostics);
            return new ManifestContractParseResult
            {
                Contract = parsed,
                Diagnostics = diagnostics
            };
        }
        catch (JsonException exception)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SCHEMA-001",
                WarningSeverity.Error,
                $"The source manifest is not valid JSON: {exception.Message}",
                ManifestCaptureSupport.SupportedManifestPath,
                recommendedAction: "Repair the manifest JSON before importing."));
            return new ManifestContractParseResult { Diagnostics = diagnostics };
        }
    }

    private static IReadOnlyDictionary<ManifestSourceRole, string> ParseRoles(
        JsonElement root,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        var roles = new Dictionary<ManifestSourceRole, string>();
        if (!root.TryGetProperty("sources", out var sources) || sources.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-MANIFEST-001",
                WarningSeverity.Error,
                "The manifest must declare a sources array.",
                ManifestCaptureSupport.SupportedManifestPath,
                "sources",
                recommendedAction: "Declare exactly one source path for each required authority role."));
            return roles;
        }

        foreach (var source in sources.EnumerateArray())
        {
            var roleValue = source.ValueKind == JsonValueKind.Object
                ? ManifestCaptureSupport.GetString(source, "role")
                : null;
            var pathValue = source.ValueKind == JsonValueKind.Object
                ? ManifestCaptureSupport.GetString(source, "path")
                : null;
            if (roleValue is null || pathValue is null || !Roles.TryGetValue(roleValue, out var role))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-MANIFEST-001",
                    WarningSeverity.Error,
                    $"Manifest source role '{roleValue ?? "<missing>"}' is missing, unknown, or malformed.",
                    ManifestCaptureSupport.SupportedManifestPath,
                    "sources",
                    recommendedAction: "Declare one supported authority role and a safe relative path."));
                continue;
            }

            if (!ManifestCaptureSupport.TryNormalizeRelativePath(pathValue, out var normalizedPath))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-PATH-001",
                    WarningSeverity.Error,
                    $"Manifest source path '{pathValue}' is not safe.",
                    pathValue,
                    "path",
                    recommendedAction: "Use a repository-relative path without traversal or links."));
                continue;
            }

            if (!roles.TryAdd(role, normalizedPath))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-MANIFEST-001",
                    WarningSeverity.Error,
                    $"Manifest authority role '{roleValue}' is declared more than once.",
                    ManifestCaptureSupport.SupportedManifestPath,
                    "sources",
                    recommendedAction: "Keep exactly one declared path for each source role."));
            }
        }

        foreach (var role in Enum.GetValues<ManifestSourceRole>())
        {
            if (!roles.ContainsKey(role))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-MANIFEST-001",
                    WarningSeverity.Error,
                    $"Manifest authority role '{role}' is missing.",
                    ManifestCaptureSupport.SupportedManifestPath,
                    "sources",
                    recommendedAction: "Restore the missing declared authority role."));
            }
        }

        return roles;
    }

    private static void ValidatePathsAndPresence(
        ManifestContractDocument contract,
        ManifestSourceCapture capture,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        var manifestPaths = new HashSet<string>(contract.RolePaths.Values, StringComparer.OrdinalIgnoreCase);
        foreach (var path in new[]
        {
            contract.BaselineReferencePath,
            contract.ExecutionRegisterPath,
            contract.ExecutionSchemaPath,
            contract.CalendarPath,
            contract.SourceContractPath,
            contract.DiagnosticCataloguePath,
            contract.FixtureCataloguePath
        }.Where(path => path is not null))
        {
            if (!ManifestCaptureSupport.TryNormalizeRelativePath(path, out var normalizedPath))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-PATH-001",
                    WarningSeverity.Error,
                    $"Manifest-declared path '{path}' is unsafe.",
                    path,
                    recommendedAction: "Use a safe repository-relative path."));
                continue;
            }

            manifestPaths.Add(normalizedPath);
        }

        if (capture.Files.TryGetValue(contract.FixtureCataloguePath, out var fixtureCatalogue))
        {
            try
            {
                foreach (var path in ManifestCaptureSupport.ReadFixturePaths(fixtureCatalogue.Content))
                {
                    manifestPaths.Add(path);
                }
            }
            catch (ManifestUnsafePathException exception)
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-PATH-001",
                    WarningSeverity.Error,
                    exception.Message,
                    exception.PathValue,
                    recommendedAction: "Use safe repository-relative fixture paths."));
            }
            catch (JsonException exception)
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-SCHEMA-001",
                    WarningSeverity.Error,
                    $"The fixture catalogue is not valid JSON: {exception.Message}",
                    contract.FixtureCataloguePath,
                    recommendedAction: "Repair the manifest-declared fixture catalogue."));
            }
        }

        foreach (var path in manifestPaths)
        {
            if (!capture.Files.ContainsKey(path))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-PATH-002",
                    WarningSeverity.Error,
                    $"Manifest-declared source '{path}' was not captured.",
                    path,
                    recommendedAction: "Restore the declared source file or correct the manifest."));
            }
        }

        foreach (var path in capture.Files.Keys)
        {
            if (!string.Equals(path, ManifestCaptureSupport.SupportedManifestPath, StringComparison.OrdinalIgnoreCase)
                && !manifestPaths.Contains(path))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-PATH-001",
                    WarningSeverity.Error,
                    $"Captured source '{path}' was not declared by the manifest.",
                    path,
                    recommendedAction: "Remove undeclared capture input; manifest is the sole discovery entry point."));
            }
        }
    }

    private static JsonElement Child(JsonElement root, string propertyName, ICollection<ManifestDiagnostic> diagnostics)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var child)
            && child.ValueKind == JsonValueKind.Object)
        {
            return child;
        }

        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
            "PMC-MANIFEST-001",
            WarningSeverity.Error,
            $"Manifest object '{propertyName}' is missing or malformed.",
            ManifestCaptureSupport.SupportedManifestPath,
            propertyName,
            recommendedAction: "Restore the required manifest object."));
        return default;
    }

    private static string RequiredString(JsonElement root, string propertyName, ICollection<ManifestDiagnostic> diagnostics)
    {
        var value = root.ValueKind == JsonValueKind.Object
            ? ManifestCaptureSupport.GetString(root, propertyName)
            : null;
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
            "PMC-MANIFEST-001",
            WarningSeverity.Error,
            $"Manifest field '{propertyName}' is required.",
            ManifestCaptureSupport.SupportedManifestPath,
            propertyName,
            recommendedAction: "Restore the required manifest field."));
        return string.Empty;
    }

    private static string? OptionalString(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object ? ManifestCaptureSupport.GetString(root, propertyName) : null;

    private static int RequiredInt(JsonElement root, string propertyName, ICollection<ManifestDiagnostic> diagnostics)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out var value))
        {
            return value;
        }

        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
            "PMC-MANIFEST-001",
            WarningSeverity.Error,
            $"Manifest integer field '{propertyName}' is required.",
            ManifestCaptureSupport.SupportedManifestPath,
            propertyName,
            recommendedAction: "Restore the required numeric manifest field."));
        return 0;
    }

    private static decimal RequiredDecimal(JsonElement root, string propertyName, ICollection<ManifestDiagnostic> diagnostics)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var property)
            && property.TryGetDecimal(out var value))
        {
            return value;
        }

        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
            "PMC-MANIFEST-001",
            WarningSeverity.Error,
            $"Manifest numeric field '{propertyName}' is required.",
            ManifestCaptureSupport.SupportedManifestPath,
            propertyName,
            recommendedAction: "Restore the required numeric manifest field."));
        return 0;
    }

    private static SourceReadinessState ParseReadinessState(JsonElement root, ICollection<ManifestDiagnostic> diagnostics)
    {
        var value = OptionalString(root, "gateState")?.ToUpperInvariant();
        return value switch
        {
            "PASS" => SourceReadinessState.Pass,
            "FAIL" => SourceReadinessState.Fail,
            _ => UnknownReadiness(root, diagnostics)
        };
    }

    private static ManifestValidationResult ParseValidationResult(JsonElement root, ICollection<ManifestDiagnostic> diagnostics)
    {
        var value = OptionalString(root, "validationResult")?.ToUpperInvariant();
        return value switch
        {
            "PASS" => ManifestValidationResult.Pass,
            "PASS_WITH_WARNINGS" => ManifestValidationResult.PassWithWarnings,
            "FAIL" => ManifestValidationResult.Fail,
            _ => UnknownValidation(root, diagnostics)
        };
    }

    private static IReadOnlyList<string> ParseWarnings(JsonElement root)
    {
        if (!root.TryGetProperty("openWarnings", out var warnings) || warnings.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return warnings.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()!)
            .ToArray();
    }

    private static SourceReadinessState UnknownReadiness(JsonElement root, ICollection<ManifestDiagnostic> diagnostics)
    {
        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
            "PMC-MANIFEST-001",
            WarningSeverity.Error,
            "Manifest sourceReadiness.gateState must be PASS or FAIL.",
            ManifestCaptureSupport.SupportedManifestPath,
            "sourceReadiness.gateState",
            recommendedAction: "Publish an explicit source readiness state."));
        return SourceReadinessState.Unknown;
    }

    private static ManifestValidationResult UnknownValidation(JsonElement root, ICollection<ManifestDiagnostic> diagnostics)
    {
        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
            "PMC-MANIFEST-001",
            WarningSeverity.Error,
            "Manifest sourceReadiness.validationResult must be PASS, PASS_WITH_WARNINGS, or FAIL.",
            ManifestCaptureSupport.SupportedManifestPath,
            "sourceReadiness.validationResult",
            recommendedAction: "Publish an explicit source validation result."));
        return ManifestValidationResult.Fail;
    }
}
