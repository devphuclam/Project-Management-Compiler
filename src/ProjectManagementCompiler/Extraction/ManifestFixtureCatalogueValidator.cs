using System.Globalization;
using System.Text.Json;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Extraction;

public sealed record ManifestFixtureCatalogueValidationResult
{
    public int FixtureCount { get; init; }
    public IReadOnlyList<ManifestDiagnostic> Diagnostics { get; init; } = Array.Empty<ManifestDiagnostic>();
}

/// <summary>
/// Validates the source-owned fixture oracle without discovering any files outside
/// the catalogue paths captured through the manifest.
/// </summary>
public sealed class ManifestFixtureCatalogueValidator
{
    private readonly BoundedJsonSchemaValidator schemaValidator;
    private readonly ManifestExecutionAdapter executionAdapter;

    public ManifestFixtureCatalogueValidator(
        BoundedJsonSchemaValidator? schemaValidator = null,
        ManifestExecutionAdapter? executionAdapter = null)
    {
        this.schemaValidator = schemaValidator ?? new BoundedJsonSchemaValidator();
        this.executionAdapter = executionAdapter ?? new ManifestExecutionAdapter();
    }

    public ManifestFixtureCatalogueValidationResult Validate(
        ManifestSourceCapture capture,
        ManifestContractDocument contract)
    {
        var diagnostics = new List<ManifestDiagnostic>();
        if (!capture.Files.TryGetValue(contract.FixtureCataloguePath, out var catalogueFile))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-PATH-002",
                WarningSeverity.Error,
                "The fixture catalogue was not captured.",
                contract.FixtureCataloguePath,
                recommendedAction: "Restore the manifest-declared fixture catalogue."));
            return new ManifestFixtureCatalogueValidationResult { Diagnostics = diagnostics };
        }

        if (!capture.Files.TryGetValue(contract.ExecutionSchemaPath, out var schemaFile))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-PATH-002",
                WarningSeverity.Error,
                "The execution schema needed by the fixture catalogue was not captured.",
                contract.ExecutionSchemaPath,
                recommendedAction: "Restore the manifest-declared execution schema."));
            return new ManifestFixtureCatalogueValidationResult { Diagnostics = diagnostics };
        }

        JsonDocument? catalogueDocument = null;
        JsonDocument? schemaDocument = null;
        try
        {
            catalogueDocument = JsonDocument.Parse(catalogueFile.Content);
            schemaDocument = JsonDocument.Parse(schemaFile.Content);
            var root = catalogueDocument.RootElement;
            var catalogueVersion = ManifestCaptureSupport.GetString(root, "contractVersion");
            if (!string.Equals(catalogueVersion, ManifestCaptureSupport.SupportedContractVersion, StringComparison.Ordinal))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-CONTRACT-002",
                    WarningSeverity.Error,
                    $"Fixture catalogue contract '{catalogueVersion ?? "<missing>"}' is not explicitly supported.",
                    contract.FixtureCataloguePath,
                    "contractVersion",
                    recommendedAction: "Use fixture catalogue contract 0.1.0."));
            }

            if (!root.TryGetProperty("fixtures", out var fixtures)
                || fixtures.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-SCHEMA-001",
                    WarningSeverity.Error,
                    "Fixture catalogue must contain a fixtures array.",
                    contract.FixtureCataloguePath,
                    "fixtures",
                    recommendedAction: "Restore the source-owned fixture catalogue entries."));
                return new ManifestFixtureCatalogueValidationResult { Diagnostics = diagnostics };
            }

            var count = 0;
            foreach (var fixture in fixtures.EnumerateArray())
            {
                count++;
                ValidateFixture(fixture, capture, contract, schemaDocument.RootElement, diagnostics);
            }

            return new ManifestFixtureCatalogueValidationResult
            {
                FixtureCount = count,
                Diagnostics = diagnostics
            };
        }
        catch (JsonException exception)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SCHEMA-001",
                WarningSeverity.Error,
                $"Fixture catalogue or execution schema is not valid JSON: {exception.Message}",
                contract.FixtureCataloguePath,
                recommendedAction: "Repair the manifest-declared fixture catalogue and schema."));
            return new ManifestFixtureCatalogueValidationResult { Diagnostics = diagnostics };
        }
        finally
        {
            schemaDocument?.Dispose();
            catalogueDocument?.Dispose();
        }
    }

    private void ValidateFixture(
        JsonElement fixture,
        ManifestSourceCapture capture,
        ManifestContractDocument contract,
        JsonElement schema,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (fixture.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-FIXTURE-001",
                WarningSeverity.Error,
                "Fixture catalogue entry must be an object.",
                contract.FixtureCataloguePath,
                "fixtures",
                recommendedAction: "Declare fixtureId, path, expectedResult and expectedDiagnostics."));
            return;
        }

        var fixtureId = ManifestCaptureSupport.GetString(fixture, "fixtureId") ?? "<missing>";
        var relativePath = ManifestCaptureSupport.GetString(fixture, "path") ?? string.Empty;
        var expectedResult = ManifestCaptureSupport.GetString(fixture, "expectedResult") ?? string.Empty;
        var expectedCodes = fixture.TryGetProperty("expectedDiagnostics", out var expectedDiagnostics)
            && expectedDiagnostics.ValueKind == JsonValueKind.Array
            ? expectedDiagnostics.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString() ?? string.Empty)
                .Where(value => value.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!ManifestCaptureSupport.TryNormalizeRelativePath(relativePath, out var normalizedPath))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-FIXTURE-001",
                WarningSeverity.Error,
                $"Fixture '{fixtureId}' declares an unsafe path.",
                relativePath,
                "path",
                "Fixture",
                fixtureId,
                "Use a captured repository-relative fixture path."));
            return;
        }

        if (!capture.Files.TryGetValue(normalizedPath, out var fixtureFile))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-FIXTURE-001",
                WarningSeverity.Error,
                $"Fixture '{fixtureId}' was declared by the catalogue but was not captured.",
                normalizedPath,
                "path",
                "Fixture",
                fixtureId,
                "Capture every fixture path declared by the source-owned catalogue."));
            return;
        }

        var actualDiagnostics = new List<ManifestDiagnostic>();
        try
        {
            using var fixtureDocument = JsonDocument.Parse(fixtureFile.Content);
            var fixtureRoot = fixtureDocument.RootElement;
            var fixtureContractVersion = ManifestCaptureSupport.GetString(fixtureRoot, "contractVersion");
            if (!string.Equals(fixtureContractVersion, ManifestCaptureSupport.SupportedContractVersion, StringComparison.Ordinal))
            {
                actualDiagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-CONTRACT-002",
                    WarningSeverity.Error,
                    $"Fixture contract '{fixtureContractVersion ?? "<missing>"}' is not explicitly supported.",
                    normalizedPath,
                    "contractVersion",
                    "Fixture",
                    fixtureId,
                    "Use the supported execution fixture contract version."));
            }

            actualDiagnostics.AddRange(schemaValidator.Validate(fixtureRoot, schema, normalizedPath));

            var planningProject = BuildFixturePlanningProject(fixtureRoot);
            var registerRevision = IntValue(fixtureRoot, "registerRevision");
            var fixtureContract = contract with
            {
                ExpectedRegisterRevision = registerRevision,
                ExecutionRegisterPath = normalizedPath
            };
            var fixtureCapture = capture with
            {
                Files = new Dictionary<string, ManifestSourceFile>(StringComparer.OrdinalIgnoreCase)
                {
                    [normalizedPath] = fixtureFile,
                    [contract.ExecutionSchemaPath] = capture.Files[contract.ExecutionSchemaPath]
                }
            };
            var execution = executionAdapter.Adapt(fixtureContract, fixtureCapture, planningProject);
            actualDiagnostics.AddRange(execution.Diagnostics);
            actualDiagnostics.AddRange(ValidateCadence(fixtureRoot, normalizedPath));
        }
        catch (JsonException exception)
        {
            actualDiagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SCHEMA-001",
                WarningSeverity.Error,
                $"Fixture JSON is invalid: {exception.Message}",
                normalizedPath,
                recommendedAction: "Repair the fixture JSON."));
        }

        var actualResult = actualDiagnostics.Any(diagnostic => diagnostic.Severity == WarningSeverity.Error)
            ? ManifestValidationResult.Fail
            : actualDiagnostics.Count > 0
                ? ManifestValidationResult.PassWithWarnings
                : ManifestValidationResult.Pass;
        var actualCodes = actualDiagnostics
            .Select(diagnostic => diagnostic.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!string.Equals(expectedResult, FormatResult(actualResult), StringComparison.Ordinal)
            || !actualCodes.SetEquals(expectedCodes))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-FIXTURE-001",
                WarningSeverity.Error,
                $"Fixture '{fixtureId}' expected {expectedResult} [{string.Join(", ", expectedCodes.OrderBy(code => code, StringComparer.OrdinalIgnoreCase))}] but produced {FormatResult(actualResult)} [{string.Join(", ", actualCodes.OrderBy(code => code, StringComparer.OrdinalIgnoreCase))}].",
                normalizedPath,
                "expectedResult",
                "Fixture",
                fixtureId,
                "Reconcile the bounded validator, source contract or fixture oracle."));
        }
    }

    private static CanonicalProject BuildFixturePlanningProject(JsonElement root)
    {
        var cards = new List<DeliveryCard>();
        if (root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
        {
            foreach (var record in records.EnumerateArray())
            {
                if (!record.TryGetProperty("entity", out var entity)
                    || !string.Equals(ManifestCaptureSupport.GetString(entity, "kind"), "DeliveryCard", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = ManifestCaptureSupport.GetString(entity, "id");
                if (!string.IsNullOrWhiteSpace(id)
                    && cards.All(card => !string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase)))
                {
                    cards.Add(new DeliveryCard { Id = id });
                }
            }
        }

        return new CanonicalProject { DeliveryCards = cards };
    }

    private static IReadOnlyList<ManifestDiagnostic> ValidateCadence(JsonElement root, string sourcePath)
    {
        var diagnostics = new List<ManifestDiagnostic>();
        var statusDateText = ManifestCaptureSupport.GetString(root, "statusDate");
        if (!DateOnly.TryParseExact(statusDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var statusDate)
            || !root.TryGetProperty("records", out var records)
            || records.ValueKind != JsonValueKind.Array)
        {
            return diagnostics;
        }

        foreach (var record in records.EnumerateArray())
        {
            if (!string.Equals(ManifestCaptureSupport.GetString(record, "recordingState"), "RECORDED", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(ManifestCaptureSupport.GetString(record, "executionState"), "IN_PROGRESS", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entity = record.TryGetProperty("entity", out var entityElement) ? entityElement : default;
            var entityId = ManifestCaptureSupport.GetString(entity, "id") ?? string.Empty;
            var lastUpdatedText = ManifestCaptureSupport.GetString(record, "lastUpdatedAt");
            if (DateTimeOffset.TryParse(lastUpdatedText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastUpdated)
                && CountWeekdays(lastUpdated.Date, statusDate) > 2)
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "EXEC-STALE-001",
                    WarningSeverity.Warning,
                    $"Delivery Card {entityId} has not been updated for more than two working days.",
                    sourcePath,
                    "lastUpdatedAt",
                    "DeliveryCard",
                    entityId,
                    "Record actual, remaining or an attributable no-change review."));
            }

            var reviewedText = ManifestCaptureSupport.GetString(record, "remainingReviewedAt");
            if (DateTimeOffset.TryParse(reviewedText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var reviewed)
                && statusDate.DayNumber - DateOnly.FromDateTime(reviewed.DateTime).DayNumber > 7)
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "EXEC-REMAINING-001",
                    WarningSeverity.Warning,
                    $"Remaining effort for {entityId} has not been reviewed within seven days.",
                    sourcePath,
                    "remainingReviewedAt",
                    "DeliveryCard",
                    entityId,
                    "Review and record the current remaining effort."));
            }
        }

        return diagnostics;
    }

    private static int CountWeekdays(DateTime from, DateOnly to)
    {
        var count = 0;
        for (var date = DateOnly.FromDateTime(from).AddDays(1); date <= to; date = date.AddDays(1))
        {
            var day = date.DayOfWeek;
            if (day is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                count++;
            }
        }

        return count;
    }

    private static int IntValue(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result) ? result : 0;

    private static string FormatResult(ManifestValidationResult result) => result switch
    {
        ManifestValidationResult.Pass => "PASS",
        ManifestValidationResult.PassWithWarnings => "PASS_WITH_WARNINGS",
        _ => "FAIL"
    };
}
