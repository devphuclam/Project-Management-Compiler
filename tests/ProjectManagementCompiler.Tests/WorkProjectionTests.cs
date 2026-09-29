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

    public static void WorkProjectionPreservesAuthoredExecutionAndRecordedActualAuthority()
    {
        var (project, analysis) = FieldAuthorityScenario();
        using var json = Project(project, analysis);
        var cards = CardsById(json.RootElement);

        var expectedStates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P01"] = "NOT_STARTED",
            ["P02"] = "IN_PROGRESS",
            ["P03"] = "COMPLETED",
            ["P04"] = "SUSPENDED",
            ["P05"] = "CANCELLED"
        };
        foreach (var (id, state) in expectedStates)
        {
            TestAssert.True(cards[id].TryGetProperty("execution", out var execution), "Missing Feature 009 behavior: WorkCard execution authority fields have not been projected.");
            TestAssert.True(execution.GetProperty("recorded").GetBoolean(), $"{id} has an authoritative recorded source execution record.");
            TestAssert.Equal(state, execution.GetProperty("state").GetString(), $"{id} must preserve its exact authored execution state.");
        }

        var knownZero = cards["P01"];
        TestAssert.Equal(0m, knownZero.GetProperty("plannedEffortHours").GetDecimal(), "A known zero baseline value must remain numeric zero.");
        TestAssert.Equal("KNOWN", knownZero.GetProperty("plannedEffortState").GetString(), "Known zero must remain distinct from an unknown value.");
        TestAssert.Equal(0m, knownZero.GetProperty("execution").GetProperty("actualEffortHours").GetDecimal(), "A recorded zero Actual value must remain numeric zero.");
        TestAssert.Equal(0m, knownZero.GetProperty("execution").GetProperty("remainingEffortHours").GetDecimal(), "A recorded zero Remaining value must remain numeric zero.");
        TestAssert.Equal("NOT_RUN", knownZero.GetProperty("execution").GetProperty("resultState").GetString(), "A not-run result must remain distinct from the authored execution state.");

        var recordedValues = cards["P02"];
        TestAssert.Equal(4m, recordedValues.GetProperty("plannedEffortHours").GetDecimal(), "The baseline planned effort must remain independent of Actual effort.");
        TestAssert.Equal(4m, recordedValues.GetProperty("execution").GetProperty("actualEffortHours").GetDecimal(), "Recorded Actual effort must come from the official source record.");
        TestAssert.Equal(0m, recordedValues.GetProperty("execution").GetProperty("remainingEffortHours").GetDecimal(), "Recorded Remaining effort must preserve known zero.");
        TestAssert.Equal("2026-09-04", recordedValues.GetProperty("plannedFinish").GetString(), "The baseline planned finish must remain a distinct field from Actual finish.");
        TestAssert.Equal("2026-09-15", recordedValues.GetProperty("execution").GetProperty("actualFinish").GetString(), "Actual finish must remain in the execution evidence object.");
        TestAssert.False(recordedValues.TryGetProperty("dueDate", out _), "Baseline planned finish must not be reinterpreted as a due date.");

        foreach (var id in new[] { "P06", "P07" })
        {
            var execution = cards[id].GetProperty("execution");
            TestAssert.False(execution.GetProperty("recorded").GetBoolean(), $"{id} must remain unrecorded despite any legacy overlay or unrecorded source row.");
            TestAssert.Equal(JsonValueKind.Null, execution.GetProperty("state").ValueKind, $"{id} must not inherit a legacy or unrecorded authored state.");
            TestAssert.Equal(JsonValueKind.Null, execution.GetProperty("actualEffortHours").ValueKind, $"{id} must not expose Actual effort from an unrecorded source row or legacy overlay.");
            TestAssert.Equal(JsonValueKind.Null, execution.GetProperty("remainingEffortHours").ValueKind, $"{id} must not expose Remaining effort without recorded source evidence.");
        }
    }

    public static void WorkProjectionPreservesPlannedEvidenceStatesRolesAndProvenance()
    {
        var (project, analysis) = FieldAuthorityScenario();
        using var json = Project(project, analysis);
        var cards = CardsById(json.RootElement);
        var expectedStates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P01"] = "KNOWN",
            ["P02"] = "UNKNOWN",
            ["P03"] = "INVALID",
            ["P04"] = "BLOCKED",
            ["P05"] = "UNRESOLVED",
            ["P06"] = "NOT_RUN"
        };
        foreach (var (id, state) in expectedStates)
        {
            TestAssert.Equal(state, cards[id].GetProperty("plannedEffortState").GetString(), $"{id} must retain its authoritative planned-effort data state.");
        }
        TestAssert.Equal(0m, cards["P01"].GetProperty("plannedEffortHours").GetDecimal(), "Known zero must be emitted as zero, not null or a missing field.");
        foreach (var id in new[] { "P02", "P03", "P04", "P05", "P06" })
        {
            TestAssert.Equal(JsonValueKind.Null, cards[id].GetProperty("plannedEffortHours").ValueKind, $"{id} non-known effort must not be coerced to zero.");
        }

        TestAssert.True(cards["P02"].TryGetProperty("roles", out var mappedRoles), "Missing Feature 009 behavior: source-backed reader-facing Work roles have not been projected.");
        var mappedRole = mappedRoles.EnumerateArray().Single();
        TestAssert.Equal("Đầu mối dự án", mappedRole.GetProperty("label").GetString(), "A supported logical role must use the existing reader-facing role mapping.");
        TestAssert.Equal("Synthetic Owner", mappedRole.GetProperty("person").GetString(), "A mapped concrete identity may be shown only when the authoritative assignment identifies the person.");
        var unresolvedRole = cards["P03"].GetProperty("roles").EnumerateArray().Single();
        TestAssert.Equal("Đầu mối dự án", unresolvedRole.GetProperty("label").GetString(), "An unresolved assignment must remain readable as a logical role.");
        TestAssert.Equal(JsonValueKind.Null, unresolvedRole.GetProperty("person").ValueKind, "A logical role without an authoritative concrete identity must not be presented as a person.");

        var references = cards["P03"].GetProperty("sourceReferences").EnumerateArray().ToArray();
        TestAssert.Equal(2, references.Length, "Work projection must preserve all card-level provenance references.");
        TestAssert.Equal("fixture/planning.md", references[0].GetProperty("relativeFile").GetString(), "Provenance must keep its safe source-relative location.");
        TestAssert.Equal("P03-primary", references[0].GetProperty("item").GetString(), "Provenance must preserve source order and item identity.");
        TestAssert.Equal("P03-secondary", references[1].GetProperty("item").GetString(), "Provenance must preserve the second source observation.");
        TestAssert.False(JsonSerializer.Serialize(cards["P03"]).Contains("C:\\", StringComparison.Ordinal), "Synthetic public-safe provenance must not acquire an absolute local path.");
    }

    public static void WorkProjectionLimitsAttentionToTargetedAllowlistedSignalsAndOrdersItDeterministically()
    {
        var (project, analysis) = FieldAuthorityScenario();
        using var json = Project(project, analysis);
        var cards = CardsById(json.RootElement);
        TestAssert.True(cards["P01"].TryGetProperty("attention", out var projectedAttention), "Missing Feature 009 behavior: the approved Work attention projection has not been added.");
        var attention = projectedAttention.EnumerateArray().ToArray();

        TestAssert.Equal(5, attention.Length, "Every targeted supported signal for the card must remain represented.");
        TestAssert.Equal("AT_RISK|AT_RISK|AT_RISK|OVERDUE|START_DELAY", string.Join('|', attention.Select(item => item.GetProperty("code").GetString())), "Attention must have deterministic code-first ordering.");
        TestAssert.Equal(
            "Có nguy cơ chậm do công việc trước bị trễ.",
            attention[0].GetProperty("consequence").GetString(),
            "The existing reader-facing consequence must be primary attention copy.");
        foreach (var item in attention)
        {
            var properties = item.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            TestAssert.Equal("code|consequence", string.Join('|', properties), "Attention entries must serialize only the approved code/consequence contract.");
            TestAssert.False(item.TryGetProperty("message", out _), "Raw analysis messages must not enter Work attention.");
        }

        TestAssert.Equal("SUSPENDED", cards["P02"].GetProperty("attention")[0].GetProperty("code").GetString(), "The supported suspended signal must remain separate from authored state.");
        TestAssert.Equal("IN_PROGRESS", cards["P02"].GetProperty("execution").GetProperty("state").GetString(), "Attention must not overwrite authored state.");
        TestAssert.Equal(0, cards["P03"].GetProperty("attention").GetArrayLength(), "Unsupported codes must not enter Work attention.");
        TestAssert.Equal(0, cards["P04"].GetProperty("attention").GetArrayLength(), "A blocked/readiness-like signal must not be inferred as Work attention.");
        TestAssert.Equal(0, cards["P05"].GetProperty("attention").GetArrayLength(), "An unlisted status must not enter Work attention.");
        TestAssert.Equal(0, cards["P06"].GetProperty("attention").GetArrayLength(), "An alert targeting a non-canonical Delivery Card must not be attached to a Work item.");

        var repeated = Project(project, analysis);
        using (repeated)
        {
            TestAssert.Equal(
                JsonSerializer.Serialize(attention),
                JsonSerializer.Serialize(CardsById(repeated.RootElement)["P01"].GetProperty("attention").EnumerateArray().ToArray()),
                "Repeated projection of the same analysis must produce byte-stable attention ordering.");
        }
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

    private static Dictionary<string, JsonElement> CardsById(JsonElement root) => root.GetProperty("cards")
        .EnumerateArray()
        .ToDictionary(card => card.GetProperty("key").GetProperty("id").GetString() ?? string.Empty, StringComparer.Ordinal);

    private static (CanonicalProject Project, ManagementAnalysis Analysis) FieldAuthorityScenario()
    {
        var canonical = WorkTestFixtures.SyntheticProject();
        var references = new[]
        {
            new SourceReference
            {
                SourceId = "synthetic-fixture",
                Repository = "synthetic",
                RelativeFile = "fixture/planning.md",
                Section = "Work package definition",
                Item = "P03-primary",
                ExtractionRule = "synthetic-test-data",
                AuthorityRank = 1,
                ConfidenceState = DataState.Known,
                ValidationState = ValidationState.Known
            },
            new SourceReference
            {
                SourceId = "synthetic-fixture",
                Repository = "synthetic",
                RelativeFile = "fixture/decisions.md",
                Section = "Approved design",
                Item = "P03-secondary",
                ExtractionRule = "synthetic-test-data",
                AuthorityRank = 2,
                ConfidenceState = DataState.Known,
                ValidationState = ValidationState.Known
            }
        };
        var extraCards = new[]
        {
            new DeliveryCard { Id = "P05", Name = "[P05] Synthetic cancelled card", ParentId = "WP-C", WorkPackageId = "WP-C", PhaseId = "PH-C", PlannedEffortState = DataState.Unresolved },
            new DeliveryCard { Id = "P06", Name = "[P06] Synthetic unrecorded card", ParentId = "WP-C", WorkPackageId = "WP-C", PhaseId = "PH-C", PlannedEffortState = DataState.NotRun },
            new DeliveryCard { Id = "P07", Name = "[P07] Synthetic missing-record card", ParentId = "WP-C", WorkPackageId = "WP-C", PhaseId = "PH-C", PlannedEffortState = DataState.Unknown }
        };
        var cards = canonical.DeliveryCards
            .Select(card => card.Id switch
            {
                "P01" => card with { PlannedEffortHours = 0m, PlannedEffortState = DataState.Known },
                "P02" => card with { PlannedEffortHours = null, PlannedEffortState = DataState.Unknown },
                "P03" => card with { PlannedEffortHours = null, PlannedEffortState = DataState.Invalid, SourceReferences = references },
                "P04" => card with { PlannedEffortHours = null, PlannedEffortState = DataState.Blocked },
                _ => card
            })
            .Concat(extraCards)
            .ToArray();
        var workPackages = canonical.WorkPackages
            .Select(package => package.Id == "WP-C"
                ? package with { DeliveryCardIds = ["P04", "P05", "P06", "P07"] }
                : package)
            .ToArray();
        var assignments = new[]
        {
            new Assignment
            {
                WorkItemId = "P02",
                LogicalRoleCode = "LEAD",
                CarioRoleCode = "A",
                ConcreteIdentity = "Synthetic Owner",
                MappingStatus = "MAPPED",
                SourceReferences = [references[0]]
            },
            new Assignment
            {
                WorkItemId = "P03",
                LogicalRoleCode = "LEAD",
                CarioRoleCode = "A",
                MappingStatus = "UNRESOLVED_IDENTITY",
                SourceReferences = [references[1]]
            }
        };
        var sourceRecords = new[]
        {
            WorkTestFixtures.SourceRecord("P01", ExecutionState.NotStarted, 0m, 0m) with
            {
                ResultState = SourceResultState.NotRun,
                ActualStart = new DateOnly(2026, 9, 15),
                ActualFinish = new DateOnly(2026, 9, 15)
            },
            WorkTestFixtures.SourceRecord("P02", ExecutionState.InProgress, 4m, 0m) with
            {
                ActualStart = new DateOnly(2026, 9, 14),
                ActualFinish = new DateOnly(2026, 9, 15)
            },
            WorkTestFixtures.SourceRecord("P03", ExecutionState.Completed, 3m, 0m) with
            {
                ResultState = SourceResultState.Pass
            },
            WorkTestFixtures.SourceRecord("P04", ExecutionState.Suspended) with
            {
                ResultState = SourceResultState.Blocked
            },
            WorkTestFixtures.SourceRecord("P05", ExecutionState.Cancelled) with
            {
                ResultState = SourceResultState.NotApplicable
            },
            WorkTestFixtures.SourceRecord("P06", ExecutionState.Completed, 77m, 8m, SourceRecordingState.NotRecorded) with
            {
                ResultState = SourceResultState.Pass
            }
        };
        var project = canonical with
        {
            ImportMetadata = WorkTestFixtures.OfficialMetadata(),
            WorkPackages = workPackages,
            DeliveryCards = cards,
            SourceExecution = new SourceExecutionSnapshot { Records = sourceRecords },
            ExecutionOverlay = new ExecutionOverlay
            {
                Records =
                [
                    WorkTestFixtures.LegacyRecord("P01", ExecutionState.Completed, 88m, 99m),
                    WorkTestFixtures.LegacyRecord("P02", ExecutionState.Completed, 55m, 66m),
                    WorkTestFixtures.LegacyRecord("P06", ExecutionState.Completed, 123m, 456m),
                    WorkTestFixtures.LegacyRecord("P07", ExecutionState.Completed, 123m, 456m)
                ]
            },
            Assignments = assignments
        };
        var analysis = new ManagementAnalysis
        {
            Alerts =
            [
                Alert("P01", "AT_RISK", new DateOnly(2026, 9, 10), ["P03", "P01"], "PRIVATE RAW MESSAGE A"),
                Alert("P01", "AT_RISK", new DateOnly(2026, 9, 10), ["P02"], "PRIVATE RAW MESSAGE B"),
                Alert("P01", "AT_RISK", new DateOnly(2026, 9, 11), ["P01"], "PRIVATE RAW MESSAGE C"),
                Alert("P01", "OVERDUE", new DateOnly(2026, 9, 12), [], "PRIVATE RAW MESSAGE D"),
                Alert("P01", "START_DELAY", new DateOnly(2026, 9, 13), [], "PRIVATE RAW MESSAGE E"),
                Alert("P02", "SUSPENDED", new DateOnly(2026, 9, 14), [], "PRIVATE RAW MESSAGE F"),
                Alert("P03", "COMPLETED_LATE", new DateOnly(2026, 9, 14), [], "PRIVATE RAW MESSAGE G"),
                Alert("P04", "BLOCKED", new DateOnly(2026, 9, 14), [], "PRIVATE RAW MESSAGE H"),
                Alert("P05", "CANCELLED", new DateOnly(2026, 9, 14), [], "PRIVATE RAW MESSAGE I"),
                Alert("P99", "OVERDUE", new DateOnly(2026, 9, 14), [], "PRIVATE RAW MESSAGE J")
            ]
        };
        return (project, analysis);
    }

    private static Alert Alert(string cardId, string code, DateOnly date, IReadOnlyList<string> reasons, string rawMessage) => new()
    {
        WorkItemId = cardId,
        AlertCode = code,
        Severity = WarningSeverity.Warning,
        DerivedAt = date,
        ReasonWorkItemIds = reasons,
        Message = rawMessage
    };

    private static JsonSerializerOptions CreateWebJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
        return options;
    }
}
