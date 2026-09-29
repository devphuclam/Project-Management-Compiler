using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class WorkProjectionTests
{
    private static readonly JsonSerializerOptions WebJsonOptions = CreateWebJsonOptions();

    public static void WorkProjectionPreservesCanonicalHierarchyAndUsesOneTypedCardIdentity()
    {
        var canonical = WorkTestFixtures.SyntheticProject();
        var variablePhaseProject = canonical with
        {
            Phases = canonical.Phases.Append(new Phase
            {
                Id = "PH-D",
                Name = "[PH-D] Follow-up",
                PlannedStart = new DateOnly(2026, 10, 1),
                PlannedFinish = new DateOnly(2026, 10, 10)
            }).ToArray()
        };

        using var json = Project(variablePhaseProject, new ManagementAnalysis { AsOfDate = new DateOnly(2026, 9, 4) });
        var root = json.RootElement;
        var phases = root.GetProperty("phases").EnumerateArray().ToArray();
        var packages = root.GetProperty("workPackages").EnumerateArray().ToArray();
        var cards = root.GetProperty("cards").EnumerateArray().ToArray();

        TestAssert.Equal(4, phases.Length, "Work must include the variable canonical phase count without a fixture-specific limit.");
        TestAssert.Equal("PH-D", phases[^1].GetProperty("id").GetString(), "Phases must retain their canonical order.");
        TestAssert.Equal(3, packages.Length, "Work Packages must be projected from the canonical hierarchy.");
        TestAssert.Equal("WP-A|WP-B|WP-C", string.Join('|', packages.Select(item => item.GetProperty("id").GetString())), "Work Packages must retain canonical order.");
        TestAssert.Equal(4, cards.Length, "Every canonical Delivery Card must appear exactly once, even when a phase has no cards.");
        TestAssert.Equal("P01|P02|P03|P04", string.Join('|', cards.Select(item => item.GetProperty("key").GetProperty("id").GetString())), "Cards must retain canonical order.");

        var phaseA = phases.Single(item => item.GetProperty("id").GetString() == "PH-A");
        TestAssert.Equal("Foundation", phaseA.GetProperty("name").GetString(), "Reader-facing phase names must use the existing identity-prefix cleanup policy.");
        TestAssert.Equal("WP-A", string.Join('|', StringArray(phaseA, "workPackageIds")), "Phase references must preserve canonical Work Package parentage.");
        var packageA = packages.Single(item => item.GetProperty("id").GetString() == "WP-A");
        TestAssert.Equal("Foundation package", packageA.GetProperty("name").GetString(), "Reader-facing Work Package names must use the existing cleanup policy.");
        TestAssert.Equal("P01|P02", string.Join('|', StringArray(packageA, "deliveryCardIds")), "Work Package references must preserve canonical card parentage and order.");
        TestAssert.Equal("Prepare the synthetic workspace", cards[0].GetProperty("name").GetString(), "Reader-facing Delivery Card names must use the existing cleanup policy.");

        foreach (var card in cards)
        {
            var key = card.GetProperty("key");
            TestAssert.Equal("DeliveryCard", key.GetProperty("kind").GetString(), "Every Work item key must be kind-qualified as a Delivery Card.");
            TestAssert.False(card.TryGetProperty("id", out _), "The serialized Work Card must not add a duplicate top-level ID.");
        }

        TestAssert.Equal(1, cards.Count(card => card.GetProperty("key").GetProperty("id").GetString() == "P01"), "A milestone sharing a raw ID must not create a duplicate or alternate Work card.");
        TestAssert.False(cards.Any(card => card.GetProperty("key").GetProperty("kind").GetString() != "DeliveryCard"), "Milestones and decision points must remain outside the Work card collection.");
    }

    public static void WorkProjectionDoesNotGuessMalformedOrMissingParents()
    {
        var canonical = WorkTestFixtures.SyntheticProject();
        var malformed = canonical with
        {
            WorkPackages = canonical.WorkPackages
                .Select(package => package.Id == "WP-C"
                    ? package with { ParentId = "PH-MISSING", PhaseId = "PH-MISSING", DeliveryCardIds = Array.Empty<string>() }
                    : package)
                .ToArray(),
            DeliveryCards = canonical.DeliveryCards
                .Select(card => card.Id == "P04"
                    ? card with { ParentId = "WP-MISSING", WorkPackageId = "WP-MISSING", PhaseId = "PH-C" }
                    : card)
                .ToArray()
        };

        using var json = Project(malformed, new ManagementAnalysis());
        var root = json.RootElement;
        var phases = root.GetProperty("phases").EnumerateArray().ToArray();
        var packages = root.GetProperty("workPackages").EnumerateArray().ToArray();
        var card = root.GetProperty("cards").EnumerateArray()
            .Single(item => item.GetProperty("key").GetProperty("id").GetString() == "P04");

        TestAssert.False(phases.Any(phase => StringArray(phase, "workPackageIds").Contains("WP-C", StringComparer.Ordinal)), "A Work Package with a missing parent phase must not be attached to a guessed phase.");
        TestAssert.Equal("PH-MISSING", packages.Single(package => package.GetProperty("id").GetString() == "WP-C").GetProperty("phaseId").GetString(), "A malformed canonical phase reference must not be rewritten to a plausible phase.");
        TestAssert.Equal("WP-MISSING", card.GetProperty("workPackageId").GetString(), "A card with a missing Work Package must retain the unresolved canonical reference.");
        TestAssert.False(packages.Any(package => StringArray(package, "deliveryCardIds").Contains("P04", StringComparer.Ordinal)), "A card with no valid canonical parent must not be placed under a guessed Work Package.");
    }

    public static void WorkProjectionUsesOnlyOneValidPhaseContainingTheOfficialReportingDate()
    {
        var canonical = WorkTestFixtures.SyntheticProject();
        var official = canonical with { ImportMetadata = WorkTestFixtures.OfficialMetadata() };
        using (var unique = Project(official, new ManagementAnalysis { AsOfDate = new DateOnly(2026, 9, 4) }))
        {
            TestAssert.Equal("PH-B", unique.RootElement.GetProperty("currentPhaseId").GetString(), "Current phase must use the official reporting date rather than a different analysis date.");
        }

        var missingOfficialDate = official with
        {
            ImportMetadata = WorkTestFixtures.OfficialMetadata() with { RegisterStatusDate = null }
        };
        using (var missing = Project(missingOfficialDate, new ManagementAnalysis { AsOfDate = WorkTestFixtures.ReportingDate }))
        {
            TestAssert.Equal(JsonValueKind.Null, missing.RootElement.GetProperty("currentPhaseId").ValueKind, "Analysis date must not substitute for a missing official reporting date.");
        }

        var ambiguous = official with
        {
            Phases = official.Phases.Select(phase => phase.Id == "PH-A"
                ? phase with { PlannedFinish = WorkTestFixtures.ReportingDate }
                : phase).ToArray()
        };
        using (var overlap = Project(ambiguous, new ManagementAnalysis()))
        {
            TestAssert.Equal(JsonValueKind.Null, overlap.RootElement.GetProperty("currentPhaseId").ValueKind, "Overlapping valid phases must not be resolved by array order.");
        }

        var invalidRange = official with
        {
            Phases = official.Phases.Select(phase => phase.Id == "PH-B"
                ? phase with { PlannedStart = new DateOnly(2026, 9, 20), PlannedFinish = new DateOnly(2026, 9, 10) }
                : phase).ToArray()
        };
        using var invalid = Project(invalidRange, new ManagementAnalysis());
        TestAssert.Equal(JsonValueKind.Null, invalid.RootElement.GetProperty("currentPhaseId").ValueKind, "Invalid phase date ranges must not identify a current phase.");
    }

    private static JsonDocument Project(CanonicalProject project, ManagementAnalysis analysis)
    {
        var projectorType = typeof(ManagementViewProjector).Assembly.GetType("ProjectManagementCompiler.Management.WorkProjector");
        TestAssert.True(projectorType is not null, "Missing Feature 009 behavior: the public WorkProjector projection does not exist yet.");

        var projector = Activator.CreateInstance(projectorType!);
        TestAssert.True(projector is not null, "The WorkProjector must be constructible for the approved read projection.");
        var build = projectorType!.GetMethod("Build", [typeof(CanonicalProject), typeof(ManagementAnalysis)]);
        TestAssert.True(build is not null, "The WorkProjector must expose the approved canonical-project/management-analysis build seam.");
        var result = build!.Invoke(projector, [project, analysis]);
        TestAssert.True(result is not null, "The WorkProjector must return its read-only projection.");
        return JsonDocument.Parse(JsonSerializer.Serialize(result, WebJsonOptions));
    }

    private static string[] StringArray(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName)
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();

    private static JsonSerializerOptions CreateWebJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
        return options;
    }
}
