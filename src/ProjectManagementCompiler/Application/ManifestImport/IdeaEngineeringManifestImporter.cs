using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Application.ManifestImport;

public sealed class IdeaEngineeringManifestImporter : IIdeaEngineeringManifestImporter
{
    private readonly IManifestSourceReader sourceReader;
    private readonly BoundedJsonSchemaValidator schemaValidator;
    private readonly ManifestContractParser contractParser;
    private readonly IdeaEngineeringExtractor planningExtractor;
    private readonly CanonicalProjectNormalizer normalizer;
    private readonly ManifestExecutionAdapter executionAdapter;
    private readonly ManifestFixtureCatalogueValidator fixtureValidator;
    private readonly IdeaEngineeringReadinessAdapter readinessAdapter;
    private readonly ManagementEvidenceReconciler evidenceReconciler;

    public IdeaEngineeringManifestImporter(
        IManifestSourceReader sourceReader,
        BoundedJsonSchemaValidator? schemaValidator = null,
        ManifestContractParser? contractParser = null,
        IdeaEngineeringExtractor? planningExtractor = null,
        CanonicalProjectNormalizer? normalizer = null,
        ManifestExecutionAdapter? executionAdapter = null,
        ManifestFixtureCatalogueValidator? fixtureValidator = null,
        IdeaEngineeringReadinessAdapter? readinessAdapter = null,
        ManagementEvidenceReconciler? evidenceReconciler = null)
    {
        this.sourceReader = sourceReader;
        this.schemaValidator = schemaValidator ?? new BoundedJsonSchemaValidator();
        this.contractParser = contractParser ?? new ManifestContractParser();
        this.planningExtractor = planningExtractor ?? new IdeaEngineeringExtractor();
        this.normalizer = normalizer ?? new CanonicalProjectNormalizer();
        this.executionAdapter = executionAdapter ?? new ManifestExecutionAdapter();
        this.fixtureValidator = fixtureValidator ?? new ManifestFixtureCatalogueValidator(schemaValidator, this.executionAdapter);
        this.readinessAdapter = readinessAdapter ?? new IdeaEngineeringReadinessAdapter();
        this.evidenceReconciler = evidenceReconciler ?? new ManagementEvidenceReconciler();
    }

    public async Task<ManifestImportResult> ImportAsync(
        ManifestImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var capture = await sourceReader.CaptureAsync(request, cancellationToken);
        var diagnostics = new List<ManifestDiagnostic>(capture.Diagnostics);

        if (!ValidateRequest(request, capture, diagnostics))
        {
            return Failed(request, capture, diagnostics);
        }

        var parsedManifest = contractParser.Parse(capture);
        diagnostics.AddRange(parsedManifest.Diagnostics);
        var contract = parsedManifest.Contract;
        if (contract is null || HasErrors(diagnostics))
        {
            return Failed(request, capture, diagnostics);
        }

        ValidateSourceContract(capture, contract, diagnostics);
        var planningProject = ImportPlanningProject(capture, contract, diagnostics);
        if (planningProject is null)
        {
            return Failed(request, capture, diagnostics);
        }

        var readinessEvidence = ImportReadinessEvidence(capture, contract, planningProject, diagnostics);
        planningProject = planningProject with { ManagementEvidence = readinessEvidence };

        var execution = ValidateAndImportExecution(capture, contract, planningProject, diagnostics);
        var fixtureValidation = fixtureValidator.Validate(capture, contract);
        diagnostics.AddRange(fixtureValidation.Diagnostics);
        var calendars = ReadCalendars(capture, contract, diagnostics);
        ValidateTotals(planningProject, contract.ExpectedTotals, diagnostics);
        AddManifestWarnings(contract, diagnostics);

        var validationResult = HasErrors(diagnostics)
            ? ManifestValidationResult.Fail
            : diagnostics.Count > 0 || contract.DeclaredValidationResult == ManifestValidationResult.PassWithWarnings
                ? ManifestValidationResult.PassWithWarnings
                : ManifestValidationResult.Pass;
        var sourceReadiness = contract.SourceReadiness;
        var classification = Classify(request.Mode, sourceReadiness, validationResult, capture.IsStable);
        var sourceIdentity = capture.ResolvedCommit ?? capture.PreviewIdentity;
        var importedAtUtc = DateTimeOffset.UtcNow;
        var snapshotId = BuildSnapshotId(
            contract.ProjectId,
            sourceIdentity,
            contract.BaselineId,
            execution.RegisterRevision);
        var metadata = new ManifestSnapshotMetadata
        {
            RepositoryIdentity = capture.RepositoryIdentity,
            ImportMode = request.Mode,
            Classification = classification,
            SourceIdentity = sourceIdentity,
            ManifestPath = ManifestCaptureSupport.SupportedManifestPath,
            ContractVersion = contract.ContractVersion,
            ProjectId = contract.ProjectId,
            BaselineId = contract.BaselineId,
            BaselineVersion = planningProject.Baseline.Version,
            RegisterRevision = execution.RegisterRevision,
            RegisterStatusDate = execution.StatusDate,
            ValidationResult = validationResult,
            SourceReadiness = sourceReadiness,
            SnapshotId = snapshotId,
            ImportedAtUtc = importedAtUtc,
            WarningCount = diagnostics.Count(diagnostic => diagnostic.Severity == WarningSeverity.Warning),
            ErrorCount = diagnostics.Count(diagnostic => diagnostic.Severity == WarningSeverity.Error),
            Calendars = calendars
        };

        var importedProject = planningProject with
        {
            SchemaVersion = "2.0",
            ImportMetadata = metadata,
            SourceExecution = execution,
            Warnings = planningProject.Warnings
                .Concat(diagnostics.Select(diagnostic => diagnostic.ToImportWarning()))
                .GroupBy(warning => warning.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(warning => warning.Id, StringComparer.Ordinal)
                .ToArray()
        };

        if (HasErrors(diagnostics))
        {
            return Failed(request, capture, diagnostics, validationResult, sourceReadiness);
        }

        var snapshot = new IdeaEngineeringSnapshot
        {
            Project = importedProject,
            Metadata = metadata,
            SourceExecution = execution,
            Diagnostics = diagnostics
        };
        var attempt = new ManifestImportAttempt
        {
            Classification = classification,
            Mode = request.Mode,
            RepositoryIdentity = capture.RepositoryIdentity,
            SourceIdentity = sourceIdentity,
            AttemptedAtUtc = importedAtUtc,
            ValidationResult = validationResult,
            SourceReadiness = sourceReadiness,
            Diagnostics = diagnostics
        };
        return new ManifestImportResult
        {
            Classification = classification,
            Snapshot = snapshot,
            Attempt = attempt,
            Diagnostics = diagnostics
        };
    }

    private CanonicalProject? ImportPlanningProject(
        ManifestSourceCapture capture,
        ManifestContractDocument contract,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        var roleKinds = BuildPlanningRoleMap(contract, diagnostics);
        var repositorySnapshot = BuildRepositorySnapshot(capture);

        try
        {
            var resolution = AuthorityResolution.Resolve(repositorySnapshot, roleKinds);
            foreach (var diagnostic in resolution.Diagnostics.Select(ToManifestDiagnostic))
            {
                diagnostics.Add(diagnostic);
            }
            var extracted = planningExtractor.Extract(resolution);
            foreach (var diagnostic in extracted.Warnings.Select(ToManifestDiagnostic))
            {
                diagnostics.Add(diagnostic);
            }
            return normalizer.Normalize(extracted);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or FormatException)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SCHEMA-001",
                WarningSeverity.Error,
                $"Planning authority extraction failed: {exception.Message}",
                recommendedAction: "Correct the manifest-declared planning authority documents."));
            return null;
        }
    }

    private static RepositorySnapshot BuildRepositorySnapshot(ManifestSourceCapture capture)
    {
        var sourceDocuments = capture.Files.Values
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select((file, index) => new SourceDocument
            {
                Id = $"manifest-source-document-{index + 1:D3}",
                RelativeFile = file.RelativePath,
                Format = file.Format,
                SizeBytes = file.SizeBytes,
                Content = file.Content,
                SourceReference = new SourceReference
                {
                    SourceId = SafeSourceId(capture.RepositoryIdentity),
                    Repository = SafeRepositoryIdentity(capture.RepositoryIdentity),
                    ResolvedRef = capture.ResolvedCommit ?? capture.PreviewIdentity,
                    RelativeFile = file.RelativePath,
                    ExtractionRule = "ideaengineering-manifest-declared-source",
                    ConfidenceState = DataState.Known,
                    ValidationState = ValidationState.Known
                }
            })
            .ToArray();

        return new RepositorySnapshot
        {
            RepositoryId = SafeSourceId(capture.RepositoryIdentity),
            RepositoryLabel = SafeRepositoryIdentity(capture.RepositoryIdentity),
            LocationLabel = "manifest-source",
            ResolvedRef = capture.ResolvedCommit ?? capture.PreviewIdentity,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            CaptureState = CaptureState.Known,
            Documents = sourceDocuments,
            Diagnostics = capture.Diagnostics.Select(diagnostic => diagnostic.ToImportWarning()).ToArray()
        };
    }

    private ManagementEvidence ImportReadinessEvidence(
        ManifestSourceCapture capture,
        ManifestContractDocument contract,
        CanonicalProject planningProject,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (!contract.RolePaths.TryGetValue(ManifestSourceRole.ReadinessEvidence, out var readinessPath))
        {
            return new ManagementEvidence();
        }

        var adapted = readinessAdapter.AdaptDeclaredReadiness(
            BuildRepositorySnapshot(capture),
            planningProject,
            readinessPath);
        foreach (var warning in adapted.Evidence.Diagnostics)
        {
            diagnostics.Add(ToManifestDiagnostic(warning));
        }

        var reconciled = evidenceReconciler.Reconcile(adapted.Evidence, planningProject);
        foreach (var warning in reconciled.Diagnostics)
        {
            if (!adapted.Evidence.Diagnostics.Any(existing => string.Equals(existing.Id, warning.Id, StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(ToManifestDiagnostic(warning));
            }
        }

        return reconciled;
    }

    private static IReadOnlyDictionary<string, PlanningDocumentKind> BuildPlanningRoleMap(
        ManifestContractDocument contract,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        var map = new Dictionary<string, PlanningDocumentKind>(StringComparer.OrdinalIgnoreCase);
        AddRole(contract, ManifestSourceRole.RoadmapAuthority, PlanningDocumentKind.Doc07, map, diagnostics);
        AddRole(contract, ManifestSourceRole.WorkPackageAuthority, PlanningDocumentKind.AppendixA, map, diagnostics);
        AddRole(contract, ManifestSourceRole.DeliveryCardAuthority, PlanningDocumentKind.Kanban, map, diagnostics);
        AddRole(contract, ManifestSourceRole.RenditionCrossCheck, PlanningDocumentKind.Gantt, map, diagnostics);
        AddRole(contract, ManifestSourceRole.NavigationOnly, PlanningDocumentKind.Readme, map, diagnostics);
        return map;
    }

    private static void AddRole(
        ManifestContractDocument contract,
        ManifestSourceRole role,
        PlanningDocumentKind kind,
        IDictionary<string, PlanningDocumentKind> map,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (!contract.RolePaths.TryGetValue(role, out var path))
        {
            return;
        }

        if (!map.TryAdd(path, kind))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-MANIFEST-001",
                WarningSeverity.Error,
                $"Planning source path '{path}' claims more than one authority role.",
                path,
                "sources",
                recommendedAction: "Use distinct declared files for each planning role."));
        }
    }

    private SourceExecutionSnapshot ValidateAndImportExecution(
        ManifestSourceCapture capture,
        ManifestContractDocument contract,
        CanonicalProject planningProject,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (capture.Files.TryGetValue(contract.ExecutionSchemaPath, out var schemaFile)
            && capture.Files.TryGetValue(contract.ExecutionRegisterPath, out var registerFile))
        {
            try
            {
                using var schemaDocument = JsonDocument.Parse(schemaFile.Content);
                using var registerDocument = JsonDocument.Parse(registerFile.Content);
                foreach (var diagnostic in schemaValidator.Validate(
                    registerDocument.RootElement,
                    schemaDocument.RootElement,
                    contract.ExecutionSchemaPath))
                {
                    diagnostics.Add(diagnostic);
                }
            }
            catch (JsonException exception)
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-SCHEMA-001",
                    WarningSeverity.Error,
                    $"Execution schema or register JSON is invalid: {exception.Message}",
                    contract.ExecutionRegisterPath,
                    recommendedAction: "Repair the execution schema/register pair."));
            }
        }

        var adapted = executionAdapter.Adapt(contract, capture, planningProject);
        foreach (var diagnostic in adapted.Diagnostics)
        {
            diagnostics.Add(diagnostic);
        }
        return adapted.Snapshot;
    }

    private static ProjectCalendarSet ReadCalendars(
        ManifestSourceCapture capture,
        ManifestContractDocument contract,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (!capture.Files.TryGetValue(contract.CalendarPath, out var calendarFile))
        {
            return new ProjectCalendarSet();
        }

        try
        {
            using var document = JsonDocument.Parse(calendarFile.Content);
            var root = document.RootElement;
            var timeZone = ManifestCaptureSupport.GetString(root, "timeZone") ?? string.Empty;
            var baseline = root.TryGetProperty("baselineCalendar", out var baselineElement)
                ? ManifestCaptureSupport.GetString(baselineElement, "calendarId") ?? string.Empty
                : string.Empty;
            var forecast = root.TryGetProperty("forecastCalendar", out var forecastElement)
                ? ManifestCaptureSupport.GetString(forecastElement, "calendarId") ?? string.Empty
                : string.Empty;
            var treatment = ManifestCaptureSupport.GetString(root, "differenceTreatment") ?? string.Empty;
            var hasSaturdayRule = forecastElement.ValueKind == JsonValueKind.Object
                && forecastElement.TryGetProperty("monthlyWorkingDayRules", out var rules)
                && rules.ValueKind == JsonValueKind.Array
                && rules.GetArrayLength() > 0;
            if (hasSaturdayRule)
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-CALENDAR-001",
                    WarningSeverity.Warning,
                    "The selected forecast calendar differs from the approved Monday-Friday baseline calendar; baseline remains unchanged.",
                    contract.CalendarPath,
                    "calendar",
                    recommendedAction: "Keep both calendars visible and request controlled rebaseline before changing commitment."));
            }

            return new ProjectCalendarSet
            {
                TimeZone = timeZone,
                BaselineCalendarId = baseline,
                ForecastCalendarId = forecast,
                DifferenceTreatment = treatment
            };
        }
        catch (JsonException exception)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SCHEMA-001",
                WarningSeverity.Error,
                $"Calendar JSON is invalid: {exception.Message}",
                contract.CalendarPath,
                recommendedAction: "Repair the manifest-declared calendar."));
            return new ProjectCalendarSet();
        }
    }

    private static void ValidateSourceContract(
        ManifestSourceCapture capture,
        ManifestContractDocument contract,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (!capture.Files.TryGetValue(contract.SourceContractPath, out var sourceContract))
        {
            return;
        }

        if (!sourceContract.Content.Contains("0.1.0", StringComparison.Ordinal))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-CONTRACT-002",
                WarningSeverity.Error,
                "The declared source contract does not identify supported contract 0.1.0.",
                contract.SourceContractPath,
                "contractVersion",
                recommendedAction: "Use the accepted source contract version."));
        }
    }

    private static void ValidateTotals(
        CanonicalProject project,
        ManifestExpectedTotals expected,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (project.Phases.Count != expected.Phases
            || project.WorkPackages.Count != expected.WorkPackages
            || project.DeliveryCards.Count != expected.DeliveryCards
            || project.Milestones.Count != expected.GatesAndMilestones
            || project.Baseline.PlannedEffortHours != expected.PlannedWorkHours
            || project.Baseline.ReserveHours != expected.ControlledReserveHours
            || project.Baseline.CapacityHours != expected.TotalBaselineCapacityHours)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SOURCE-001",
                WarningSeverity.Error,
                $"Planning totals do not match the manifest: phases={project.Phases.Count}, workPackages={project.WorkPackages.Count}, deliveryCards={project.DeliveryCards.Count}, gates={project.Milestones.Count}, planned={project.Baseline.PlannedEffortHours}, reserve={project.Baseline.ReserveHours}, capacity={project.Baseline.CapacityHours}.",
                recommendedAction: "Reconcile the declared planning authority before import."));
        }
    }

    private static void AddManifestWarnings(
        ManifestContractDocument contract,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        foreach (var warning in contract.OpenWarnings)
        {
            if (!diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, warning, StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    warning,
                    WarningSeverity.Warning,
                    $"The source manifest retains open warning '{warning}'.",
                    ManifestCaptureSupport.SupportedManifestPath,
                    recommendedAction: "Review the source diagnostic catalogue treatment."));
            }
        }
    }

    private static bool ValidateRequest(
        ManifestImportRequest request,
        ManifestSourceCapture capture,
        ICollection<ManifestDiagnostic> diagnostics)
    {
        if (!string.Equals(request.ManifestPath.Replace('\\', '/'), ManifestCaptureSupport.SupportedManifestPath, StringComparison.Ordinal))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-PATH-001",
                WarningSeverity.Error,
                "Manifest workflow accepts only the sole project-management manifest entry point.",
                request.ManifestPath,
                recommendedAction: "Use planning/project-management-compiler-manifest.json."));
        }

        if (request.Mode == ManifestImportMode.GitCommit && string.IsNullOrWhiteSpace(capture.ResolvedCommit))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SNAPSHOT-002",
                WarningSeverity.Error,
                "Official import requires a resolved exact commit.",
                recommendedAction: "Provide an existing commit object."));
        }

        if (!capture.IsStable)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-SNAPSHOT-002",
                WarningSeverity.Error,
                "The captured source is not stable enough to form an atomic snapshot.",
                recommendedAction: "Retry after the source is stable or import an exact commit."));
        }

        return !HasErrors(diagnostics);
    }

    private static ManifestImportClassification Classify(
        ManifestImportMode mode,
        SourceReadinessState readiness,
        ManifestValidationResult validation,
        bool stable)
    {
        if (!stable || validation == ManifestValidationResult.Fail)
        {
            return ManifestImportClassification.Failed;
        }

        if (mode == ManifestImportMode.UncommittedPreview)
        {
            return ManifestImportClassification.UncommittedPreview;
        }

        return readiness == SourceReadinessState.Pass
            ? ManifestImportClassification.OfficialCommit
            : ManifestImportClassification.CandidatePreview;
    }

    private static ManifestImportResult Failed(
        ManifestImportRequest request,
        ManifestSourceCapture capture,
        IReadOnlyList<ManifestDiagnostic> diagnostics,
        ManifestValidationResult validation = ManifestValidationResult.Fail,
        SourceReadinessState readiness = SourceReadinessState.Unknown)
    {
        var attempt = new ManifestImportAttempt
        {
            Classification = ManifestImportClassification.Failed,
            Mode = request.Mode,
            RepositoryIdentity = capture.RepositoryIdentity,
            SourceIdentity = capture.ResolvedCommit ?? capture.PreviewIdentity,
            AttemptedAtUtc = DateTimeOffset.UtcNow,
            ValidationResult = validation,
            SourceReadiness = readiness,
            Diagnostics = diagnostics
        };
        return new ManifestImportResult
        {
            Classification = ManifestImportClassification.Failed,
            Attempt = attempt,
            Diagnostics = diagnostics
        };
    }

    private static string BuildSnapshotId(
        string projectId,
        string sourceIdentity,
        string baselineId,
        int registerRevision)
    {
        var material = string.Join('|', projectId, sourceIdentity, baselineId, registerRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return $"snapshot-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant()[..24]}";
    }

    private static bool HasErrors(IEnumerable<ManifestDiagnostic> diagnostics) =>
        diagnostics.Any(diagnostic => diagnostic.Severity == WarningSeverity.Error);

    private static ManifestDiagnostic ToManifestDiagnostic(ImportWarning warning) => new()
    {
        Code = warning.Code,
        Severity = warning.Severity,
        EntityId = warning.AffectedIds.FirstOrDefault(),
        SourcePath = warning.SourceReferences.FirstOrDefault()?.RelativeFile,
        Message = warning.Message,
        RecommendedAction = "Review the declared authority source and correct the candidate before import."
    };

    private static string SafeSourceId(string repositoryIdentity)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(repositoryIdentity));
        return $"manifest-{Convert.ToHexString(bytes)[..16].ToLowerInvariant()}";
    }

    private static string SafeRepositoryIdentity(string repositoryIdentity) =>
        string.IsNullOrWhiteSpace(repositoryIdentity) ? "IDEAEngineering" : repositoryIdentity;
}
