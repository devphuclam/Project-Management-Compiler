using System.Text.Json;
using System.Text.RegularExpressions;
using ProjectManagementCompiler.Application.ManifestImport;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ManifestImportTests
{
    public static void ManifestImportAcceptsValidatedCapture()
    {
        var importer = new IdeaEngineeringManifestImporter(new FakeManifestSourceReader());
        var result = importer.ImportAsync(new ManifestImportRequest
        {
            RepositoryRoot = @"C:\test-source",
            ManifestPath = "planning/project-management-compiler-manifest.json",
            Mode = ManifestImportMode.GitCommit,
            RequestedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4"
        }).GetAwaiter().GetResult();

        TestAssert.Equal(ManifestImportClassification.OfficialCommit, result.Classification, $"A validated exact commit should import as official. Diagnostics: {string.Join(" | ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}:{diagnostic.Message}"))}");
        TestAssert.True(result.Snapshot is not null, "A successful import should return a snapshot.");
        TestAssert.Equal("0.1.0", result.Snapshot!.Metadata.ContractVersion, "The source contract version should be retained.");
        TestAssert.Equal("IDEA-ENGINEERING", result.Snapshot.Metadata.ProjectId, "The source project identity should be retained.");
    }

    public static void ManifestImportNeverFallsBackToLegacyCapture()
    {
        var reader = new FakeManifestSourceReader
        {
            Capture = new ManifestSourceCapture
            {
                RepositoryIdentity = "devphuclam/IDEAEngineering",
                ResolvedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4",
                Mode = ManifestImportMode.GitCommit,
                IsStable = false,
                Diagnostics =
                [
                    new ManifestDiagnostic
                    {
                        Code = "PMC-SNAPSHOT-002",
                        Severity = WarningSeverity.Error,
                        Message = "mixed capture",
                        RecommendedAction = "capture one exact commit"
                    }
                ]
            }
        };

        var result = new IdeaEngineeringManifestImporter(reader)
            .ImportAsync(new ManifestImportRequest
            {
                RepositoryRoot = @"C:\test-source",
                ManifestPath = "planning/project-management-compiler-manifest.json",
                Mode = ManifestImportMode.GitCommit,
                RequestedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4"
            })
            .GetAwaiter()
            .GetResult();

        TestAssert.Equal(ManifestImportClassification.Failed, result.Classification, "A mixed capture must fail.");
        TestAssert.True(result.Snapshot is null, "A mixed capture must not produce a snapshot.");
        TestAssert.True(result.Diagnostics.Any(diagnostic => diagnostic.Code == "PMC-SNAPSHOT-002"), "The mixed capture diagnostic must be preserved.");
    }

    public static void ManifestGitReaderCapturesAcceptedCommitWithoutCheckoutMutation()
    {
        var sourceRoot = FindIdeaEngineeringRoot();
        var capture = new ManifestGitObjectReader()
            .CaptureAsync(new ManifestImportRequest
            {
                RepositoryRoot = sourceRoot,
                ManifestPath = "planning/project-management-compiler-manifest.json",
                Mode = ManifestImportMode.GitCommit,
                RequestedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4"
            })
            .GetAwaiter()
            .GetResult();

        TestAssert.True(capture.IsStable, "The accepted commit should be captured atomically.");
        TestAssert.Equal("0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4", capture.ResolvedCommit, "The reader must resolve the requested exact commit.");
        TestAssert.True(capture.Files.ContainsKey("planning/project-management-compiler-manifest.json"), "The manifest must be read first and retained in the capture.");
        TestAssert.False(capture.Files.Keys.Any(path => Path.IsPathRooted(path)), "Persisted source paths must remain relative.");
    }

    public static void AcceptedManifestImportsExactTotalsAndP01Truth()
    {
        var result = new IdeaEngineeringManifestImporter(new ManifestGitObjectReader())
            .ImportAsync(new ManifestImportRequest
            {
                RepositoryRoot = FindIdeaEngineeringRoot(),
                ManifestPath = "planning/project-management-compiler-manifest.json",
                Mode = ManifestImportMode.GitCommit,
                RequestedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4"
            })
            .GetAwaiter()
            .GetResult();

        TestAssert.Equal(ManifestImportClassification.OfficialCommit, result.Classification, $"Accepted source should be official. Diagnostics: {string.Join(" | ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}:{diagnostic.Message}"))}");
        var snapshot = result.Snapshot!;
        TestAssert.Equal(6, snapshot.Project.Phases.Count, "Accepted source should retain six phases.");
        TestAssert.Equal(35, snapshot.Project.WorkPackages.Count, "Accepted source should retain 35 work packages.");
        TestAssert.Equal(53, snapshot.Project.DeliveryCards.Count, "Accepted source should retain 53 delivery cards.");
        TestAssert.Equal(7, snapshot.Project.Milestones.Count, "Accepted source should retain seven gates/milestones.");
        TestAssert.Equal(512m, snapshot.Project.Baseline.PlannedEffortHours, "Accepted source planned effort should remain 512 hours.");
        TestAssert.Equal(88m, snapshot.Project.Baseline.ReserveHours, "Accepted source reserve should remain 88 hours.");
        TestAssert.Equal(600m, snapshot.Project.Baseline.CapacityHours, "Accepted source capacity should remain 600 hours.");
        var p01 = snapshot.SourceExecution.Records.Single(record => record.Entity == CanonicalWorkItemKey.DeliveryCard("P01"));
        TestAssert.Equal(SourceRecordingState.Recorded, p01.RecordingState, "P01 should be recorded.");
        TestAssert.Equal(ExecutionState.InProgress, p01.ExecutionState, "P01 should be in progress.");
        TestAssert.Equal(SourceResultState.NotApplicable, p01.ResultState, "P01 result should be not applicable.");
        TestAssert.True(p01.ActualEffortHours is null && p01.RemainingEffortHours is null, "P01 effort must remain unknown.");
        TestAssert.Equal(52, snapshot.SourceExecution.Records.Count(record => record.RecordingState == SourceRecordingState.NotRecorded), "The other 52 cards must remain not recorded.");
        TestAssert.True(result.Diagnostics.Any(diagnostic => diagnostic.Code == "PMC-CALENDAR-001"), "The accepted calendar delta must remain a warning.");
    }

    public static void AcceptedManifestCapturesDeclaredReadinessEvidenceSeparately()
    {
        var result = new IdeaEngineeringManifestImporter(new ManifestGitObjectReader())
            .ImportAsync(new ManifestImportRequest
            {
                RepositoryRoot = FindIdeaEngineeringRoot(),
                ManifestPath = "planning/project-management-compiler-manifest.json",
                Mode = ManifestImportMode.GitCommit,
                RequestedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4"
            })
            .GetAwaiter()
            .GetResult();

        var snapshot = result.Snapshot!;
        TestAssert.Equal(ManagementEvidenceDiscoveryState.Known, snapshot.Project.ManagementEvidence.DiscoveryState, "The manifest-declared readiness register should remain visible as management evidence.");
        TestAssert.Equal("IE-INC-READY-001", snapshot.Project.ManagementEvidence.IncrementId, "The readiness increment identity should come from the declared register.");
        TestAssert.True(snapshot.Project.ManagementEvidence.Observations.Count > 0, "The readiness register should produce typed observations without requiring repository scanning.");
        var p01 = snapshot.SourceExecution.Records.Single(record => record.Entity == CanonicalWorkItemKey.DeliveryCard("P01"));
        TestAssert.Equal(SourceRecordingState.Recorded, p01.RecordingState, "Readiness evidence must not replace source execution recording state.");
        TestAssert.Equal(SourceResultState.NotApplicable, p01.ResultState, "Readiness evidence must not replace the source result state.");
    }

    public static void AcceptedFixtureCatalogueMatchesDeclaredOracle()
    {
        var capture = new ManifestGitObjectReader()
            .CaptureAsync(new ManifestImportRequest
            {
                RepositoryRoot = FindIdeaEngineeringRoot(),
                ManifestPath = "planning/project-management-compiler-manifest.json",
                Mode = ManifestImportMode.GitCommit,
                RequestedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4"
            })
            .GetAwaiter()
            .GetResult();
        var parsed = new ManifestContractParser().Parse(capture);
        TestAssert.True(parsed.Contract is not null, "The accepted source fixture test requires a parsed manifest contract.");
        var result = new ManifestFixtureCatalogueValidator().Validate(capture, parsed.Contract!);
        TestAssert.Equal(7, result.FixtureCount, "The accepted source fixture catalogue must contain seven cases.");
        TestAssert.Equal(0, result.Diagnostics.Count, $"Fixture catalogue diagnostics: {string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Code + ":" + diagnostic.Message))}");
    }

    private sealed class FakeManifestSourceReader : IManifestSourceReader
    {
        public ManifestSourceCapture? Capture { get; init; }

        public Task<ManifestSourceCapture> CaptureAsync(ManifestImportRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Capture ?? FixtureCapture(request.Mode));

        private static ManifestSourceCapture FixtureCapture(ManifestImportMode mode)
        {
            var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering-real-shaped");
            var files = new Dictionary<string, ManifestSourceFile>(StringComparer.OrdinalIgnoreCase);
            void Add(string relativePath, string content) =>
                files[relativePath] = new ManifestSourceFile
                {
                    RelativePath = relativePath,
                    Content = content,
                    SizeBytes = System.Text.Encoding.UTF8.GetByteCount(content),
                    Format = relativePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                        ? SourceDocumentFormat.Html
                        : relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                            ? SourceDocumentFormat.Markdown
                            : SourceDocumentFormat.Text
                };

            foreach (var relativePath in new[]
            {
                "README.md",
                "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
                "docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md",
                "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html",
                "docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md",
                "specs/004-technical-pilot-readiness/readiness-register.md"
            })
            {
                Add(relativePath, File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))));
            }

            var cardIds = Regex.Matches(
                    files["docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md"].Content,
                    @"^\|\s*\`?(?<id>[A-Z]\d+(?:-[A-Z])?)\`?\s*\|",
                    RegexOptions.Multiline)
                .Select(match => match.Groups["id"].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var records = cardIds.Select((id, index) => new
            {
                entity = new { kind = "DeliveryCard", id },
                recordingState = index == 0 ? "RECORDED" : "NOT_RECORDED",
                executionState = index == 0 ? "IN_PROGRESS" : null,
                resultState = index == 0 ? "NOT_APPLICABLE" : null,
                priority = "NORMAL",
                forecastPlannedOrder = index + 1,
                actualStart = (string?)null,
                actualFinish = (string?)null,
                actualEffortHours = (decimal?)null,
                remainingEffortHours = (decimal?)null,
                forecastFinish = (string?)null,
                lastUpdatedAt = index == 0 ? "2026-09-19T00:00:00+07:00" : null,
                recordedBy = index == 0 ? "LEAD" : null,
                disposition = "ACTIVE",
                evidence = index == 0
                    ? new object[] { new { evidenceId = "P01-EVIDENCE", type = "SOURCE_RECORD", repoPath = "specs/004-technical-pilot-readiness/readiness-register.md", commit = (string?)null, sha256 = (string?)null, externalUri = (string?)null, description = "Attributable source record.", result = "NOT_APPLICABLE", recordedAt = "2026-09-19T00:00:00+07:00", recordedBy = "LEAD" } }
                    : Array.Empty<object>(),
                blockers = Array.Empty<object>(),
                events = index == 0
                    ? new object[] { new { eventId = "P01-EVENT", effectiveAt = "2026-09-19T00:00:00+07:00", recordedAt = "2026-09-19T00:00:00+07:00", recordedBy = "LEAD", kind = "STATE_CHANGE", reason = "Attributable source state." } }
                    : Array.Empty<object>(),
                successorRefs = Array.Empty<object>()
            }).ToArray();
            Add("planning/idea-technical-pilot-execution-register.json", JsonSerializer.Serialize(new
            {
                contractVersion = "0.1.0",
                registerId = "IE-PMC-EXECUTION-REGISTER-001",
                registerRevision = 1,
                status = "DRAFT",
                projectId = "IDEA-ENGINEERING",
                baselineId = "IE-PLAN-DEC2026-002@0.1",
                statusDate = "2026-09-19",
                timeZone = "Asia/Ho_Chi_Minh",
                defaults = new { priority = "NORMAL", dependencyType = "FINISH_TO_START", wipLimit = 1 },
                records,
                changeHistory = new[] { new { revision = 1, recordedAt = "2026-09-19T00:00:00+07:00", recordedBy = "LEAD", reason = "Initialize fixture." } }
            }));
            Add("planning/idea-technical-pilot-execution-register.schema.json", """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": ["records"],
              "properties": {
                "records": { "type": "array" }
              }
            }
            """);
            Add("planning/idea-technical-pilot-project-calendar.json", """
            {
              "contractVersion": "0.1.0",
              "timeZone": "Asia/Ho_Chi_Minh",
              "baselineCalendar": { "calendarId": "IE-CALENDAR-TP-BASELINE-001" },
              "forecastCalendar": { "calendarId": "IE-CALENDAR-TP-FORECAST-001", "monthlyWorkingDayRules": [{ "dayOfWeek": "SATURDAY", "occurrences": [2, 4] }] },
              "differenceTreatment": "Keep baseline unchanged."
            }
            """);
            Add("planning/idea-technical-pilot-baseline-reference.json", "{\"contractVersion\":\"0.1.0\"}");
            Add("planning/project-management-compiler-diagnostics.md", "contractVersion 0.1.0");
            Add("planning/project-management-compiler-source-contract.md", "contractVersion 0.1.0");
            Add("planning/project-management-compiler-fixtures/fixture-catalog.json", "{\"contractVersion\":\"0.1.0\",\"fixtures\":[]}");
            Add("planning/project-management-compiler-manifest.json", """
            {
              "contractVersion": "0.1.0",
              "manifestId": "IE-PMC-MANIFEST-001",
              "projectId": "IDEA-ENGINEERING",
              "projectName": "IDEA Engineering",
              "activeBaseline": { "baselineId": "IE-PLAN-DEC2026-002@0.1", "referencePath": "planning/idea-technical-pilot-baseline-reference.json" },
              "execution": { "registerPath": "planning/idea-technical-pilot-execution-register.json", "schemaPath": "planning/idea-technical-pilot-execution-register.schema.json", "expectedRegisterRevision": 1 },
              "calendarPath": "planning/idea-technical-pilot-project-calendar.json",
              "diagnosticCataloguePath": "planning/project-management-compiler-diagnostics.md",
              "sourceContractPath": "planning/project-management-compiler-source-contract.md",
              "fixtureCataloguePath": "planning/project-management-compiler-fixtures/fixture-catalog.json",
              "sources": [
                { "role": "ROADMAP_AUTHORITY", "path": "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md" },
                { "role": "WORK_PACKAGE_AUTHORITY", "path": "docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md" },
                { "role": "DELIVERY_CARD_AUTHORITY", "path": "docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md" },
                { "role": "EXECUTION_AUTHORITY", "path": "planning/idea-technical-pilot-execution-register.json" },
                { "role": "RENDITION_CROSS_CHECK", "path": "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html" },
                { "role": "READINESS_EVIDENCE", "path": "specs/004-technical-pilot-readiness/readiness-register.md" },
                { "role": "NAVIGATION_ONLY", "path": "README.md" }
              ],
              "expectedSourceTotals": { "phases": 6, "workPackages": 35, "deliveryCards": 53, "gatesAndMilestones": 7, "plannedWorkHours": 512, "controlledReserveHours": 88, "totalBaselineCapacityHours": 600 },
              "sourceReadiness": { "gateState": "PASS", "validationResult": "PASS_WITH_WARNINGS", "openWarnings": ["PMC-CALENDAR-001"] }
            }
            """);

            return new ManifestSourceCapture
            {
                RepositoryIdentity = "devphuclam/IDEAEngineering",
                ResolvedCommit = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4",
                PreviewIdentity = "fixture-preview",
                Mode = mode,
                IsStable = true,
                Files = files
            };
        }
    }

    private static string FindIdeaEngineeringRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var depth = 0; depth < 6 && current is not null; depth++, current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "IDEAEngineering");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var sibling = current.Parent is null ? null : Path.Combine(current.Parent.FullName, "IDEAEngineering");
            if (sibling is not null && Directory.Exists(sibling))
            {
                return sibling;
            }
        }

        throw new DirectoryNotFoundException("The local IDEAEngineering checkout was not found for the accepted-source compatibility test.");
    }
}
