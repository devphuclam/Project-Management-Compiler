using System.Text.Json;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Application;

public sealed class ProjectCompiler : IProjectCompiler
{
    private readonly IProjectSourceAdapter sourceAdapter;
    private readonly IdeaEngineeringExtractor extractor;
    private readonly CanonicalProjectNormalizer normalizer;
    private readonly ManagementAnalysisOrchestrator analysisOrchestrator;
    private readonly ManagementViewProjector viewProjector;
    private readonly CarioMappingProjector carioMappingProjector;
    private readonly CarioXlsxExporter carioXlsxExporter;
    private readonly ExecutiveProgressReportProjector executiveProgressReportProjector;
    private readonly ExecutiveProgressXlsxExporter executiveProgressXlsxExporter;
    private readonly CanonicalJsonSerializer jsonSerializer;
    private readonly ExecutionOverlayUpdater executionUpdater;
    private readonly IManagementEvidenceSourceAdapter managementEvidenceAdapter;
    private readonly ManagementEvidenceReconciler managementEvidenceReconciler;

    public ProjectCompiler(
        IProjectSourceAdapter? sourceAdapter = null,
        IdeaEngineeringExtractor? extractor = null,
        CanonicalProjectNormalizer? normalizer = null,
        ManagementAnalysisOrchestrator? analysisOrchestrator = null,
        ManagementViewProjector? viewProjector = null,
        CarioMappingProjector? carioMappingProjector = null,
        CarioXlsxExporter? carioXlsxExporter = null,
        ExecutiveProgressReportProjector? executiveProgressReportProjector = null,
        ExecutiveProgressXlsxExporter? executiveProgressXlsxExporter = null,
        CanonicalJsonSerializer? jsonSerializer = null,
        ExecutionOverlayUpdater? executionUpdater = null,
        IManagementEvidenceSourceAdapter? managementEvidenceAdapter = null,
        ManagementEvidenceReconciler? managementEvidenceReconciler = null)
    {
        this.sourceAdapter = sourceAdapter ?? new LocalRepositorySourceAdapter();
        this.extractor = extractor ?? new IdeaEngineeringExtractor();
        this.normalizer = normalizer ?? new CanonicalProjectNormalizer();
        this.analysisOrchestrator = analysisOrchestrator ?? new ManagementAnalysisOrchestrator();
        this.viewProjector = viewProjector ?? new ManagementViewProjector();
        this.carioMappingProjector = carioMappingProjector ?? new CarioMappingProjector();
        this.carioXlsxExporter = carioXlsxExporter ?? new CarioXlsxExporter();
        this.executiveProgressReportProjector = executiveProgressReportProjector ?? new ExecutiveProgressReportProjector();
        this.executiveProgressXlsxExporter = executiveProgressXlsxExporter ?? new ExecutiveProgressXlsxExporter();
        this.jsonSerializer = jsonSerializer ?? new CanonicalJsonSerializer();
        this.executionUpdater = executionUpdater ?? new ExecutionOverlayUpdater();
        this.managementEvidenceAdapter = managementEvidenceAdapter ?? new IdeaEngineeringReadinessAdapter();
        this.managementEvidenceReconciler = managementEvidenceReconciler ?? new ManagementEvidenceReconciler();
    }

    public async Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SourcePath))
        {
            throw new ProjectCompilationException(
                "capture",
                [new ImportWarning
                {
                    Id = "INVALID_SOURCE_PATH",
                    Severity = WarningSeverity.Error,
                    Code = "INVALID_SOURCE_PATH",
                    Message = "A local source path is required."
                }]);
        }

        if (request.IncludeManagementEvidence && string.IsNullOrWhiteSpace(request.ManagementEvidenceIncrementPath))
        {
            throw new ProjectCompilationException(
                "capture",
                [new ImportWarning
                {
                    Id = "MANAGEMENT_EVIDENCE_PATH_REQUIRED",
                    Severity = WarningSeverity.Error,
                    Code = "MANAGEMENT_EVIDENCE_PATH_REQUIRED",
                    Message = "Select the readiness increment path before including repository readiness evidence."
                }]);
        }

        var asOfDate = RequireAsOfDate(request.AsOfDate, "compile-request");
        var managementEvidenceIncrementPath = request.IncludeManagementEvidence
            ? request.ManagementEvidenceIncrementPath
            : null;

        var snapshot = await sourceAdapter.CaptureAsync(new SourceRequest
        {
            Location = request.SourcePath,
            Ref = request.Ref,
            ManagementEvidenceIncrementPath = managementEvidenceIncrementPath,
            MaxDocumentBytes = request.MaxDocumentBytes,
            MaxTotalDocumentBytes = request.MaxTotalDocumentBytes
        }, cancellationToken);
        var resolution = AuthorityResolution.Resolve(snapshot);
        var extracted = extractor.Extract(resolution);
        var project = normalizer.Normalize(extracted);
        if (request.IncludeManagementEvidence)
        {
            var evidence = managementEvidenceAdapter.Adapt(snapshot, project).Evidence;
            project = project with
            {
                ManagementEvidence = managementEvidenceReconciler.Reconcile(evidence, project)
            };
        }

        return BuildResult(project, asOfDate, request.Mapping);
    }

    public CompilationResult Reopen(string json, DateOnly? asOfDate = null, CarioMappingConfiguration? mapping = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ProjectCompilationException(
                "reopen",
                [new ImportWarning
                {
                    Id = "EMPTY_CANONICAL_JSON",
                    Severity = WarningSeverity.Error,
                    Code = "EMPTY_CANONICAL_JSON",
                    Message = "Canonical JSON content is required."
                }]);
        }

        var requiredAsOfDate = RequireAsOfDate(asOfDate, "reopen");

        try
        {
            var project = jsonSerializer.Deserialize(json);
            return BuildResult(project, requiredAsOfDate, mapping ?? new CarioMappingConfiguration());
        }
        catch (ProjectCompilationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or NotSupportedException)
        {
            throw new ProjectCompilationException(
                "reopen",
                [new ImportWarning
                {
                    Id = "INVALID_CANONICAL_JSON",
                    Severity = WarningSeverity.Error,
                    Code = "INVALID_CANONICAL_JSON",
                    Message = exception.Message
                }]);
        }
    }

    public ExecutionApplicationResult ApplyExecutionUpdate(CompilationResult current, ExecutionUpdate update, DateOnly? asOfDate = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(update);

        var updateResult = executionUpdater.Apply(current.Project, update);
        if (!updateResult.Accepted)
        {
            return new ExecutionApplicationResult
            {
                Accepted = false,
                Result = current,
                Diagnostics = updateResult.Diagnostics
            };
        }

        var requiredAsOfDate = asOfDate ?? current.Analysis.AsOfDate;
        if (requiredAsOfDate is null)
        {
            throw new ProjectCompilationException(
                "execution",
                [new ImportWarning
                {
                    Id = "MISSING_AS_OF_DATE",
                    Severity = WarningSeverity.Error,
                    Code = "MISSING_AS_OF_DATE",
                    Message = "An explicit as-of date is required to recalculate execution analysis."
                }]);
        }

        return new ExecutionApplicationResult
        {
            Accepted = true,
            Result = BuildResult(updateResult.Project, requiredAsOfDate.Value, current.Mapping),
            Diagnostics = updateResult.Diagnostics
        };
    }

    public string SaveJson(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return jsonSerializer.Serialize(result.Project);
    }

    public byte[] ExportCarioXlsx(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var asOfDate = result.Analysis.AsOfDate
            ?? throw new InvalidOperationException("A compiled result must have an analysis as-of date before it can be exported.");
        var metadata = result.Project.ImportMetadata;
        var sourceIdentity = metadata?.SourceIdentity
            ?? string.Join(",", result.Project.Sources
                .Select(source => source.ResolvedRef)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal));
        var gantt = new GanttXlsxModel
        {
            ProjectName = result.Project.Project.Name,
            SnapshotScope = metadata?.Classification switch
            {
                ManifestImportClassification.OfficialCommit => "Official source commit",
                ManifestImportClassification.CandidatePreview or ManifestImportClassification.UncommittedPreview => "Non-authoritative preview",
                _ => "Compiled source snapshot"
            },
            SourceIdentity = sourceIdentity,
            SnapshotId = metadata?.SnapshotId ?? string.Empty,
            AsOfDate = asOfDate,
            Wbs = result.Views.Wbs,
            Gantt = result.Views.Gantt
        };
        return carioXlsxExporter.ExportWithGantt(result.Cario, gantt);
    }

    public byte[] ExportExecutiveProgressXlsx(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var metadata = result.Project.ImportMetadata;
        if (metadata is null
            || metadata.Classification != ManifestImportClassification.OfficialCommit
            || metadata.RegisterStatusDate is null
            || metadata.RegisterStatusDate.Value == DateOnly.MinValue
            || result.Analysis is null
            || result.Analysis.AsOfDate is null
            || result.Views is null)
        {
            throw new ProjectCompilationException(
                "executive-export",
                [new ImportWarning
                {
                    Id = "EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL",
                    Code = "EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL",
                    Severity = WarningSeverity.Error,
                    Message = "An official manifest snapshot with a valid source reporting date, analysis as-of date, and compiled management views is required for the management report."
                }]);
        }

        var report = executiveProgressReportProjector.Build(result);
        return executiveProgressXlsxExporter.Export(report);
    }

    public CompilationResult BuildImportedResult(
        IdeaEngineeringSnapshot snapshot,
        DateOnly? asOfDate = null,
        CarioMappingConfiguration? mapping = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var effectiveAsOfDate = asOfDate
            ?? snapshot.Metadata.RegisterStatusDate
            ?? throw new ProjectCompilationException(
                "manifest-import",
                [new ImportWarning
                {
                    Id = "MISSING_AS_OF_DATE",
                    Severity = WarningSeverity.Error,
                    Code = "MISSING_AS_OF_DATE",
                    Message = "The manifest snapshot has no register status date; provide an explicit analysis override."
                }]);
        return BuildResult(snapshot.Project, effectiveAsOfDate, mapping ?? new CarioMappingConfiguration());
    }

    private CompilationResult BuildResult(CanonicalProject project, DateOnly asOfDate, CarioMappingConfiguration mapping)
    {
        var validation = CanonicalProjectValidator.Validate(project);
        var validationErrors = validation.Where(diagnostic => diagnostic.Severity == WarningSeverity.Error).ToArray();
        var warnings = project.Warnings
            .Concat(project.ManagementEvidence.Diagnostics)
            .Concat(validation)
            .GroupBy(warning => warning.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(warning => warning.Id, StringComparer.Ordinal)
            .ToArray();
        if (validationErrors.Length > 0)
        {
            throw new ProjectCompilationException("canonical-validation", warnings);
        }

        var analysis = analysisOrchestrator.Analyze(project, asOfDate);
        var analyzedProject = project with
        {
            Warnings = warnings,
            Analysis = analysis
        };
        var views = viewProjector.Build(analyzedProject, analysis, asOfDate);
        var cario = carioMappingProjector.Build(analyzedProject, mapping);
        return new CompilationResult
        {
            Project = analyzedProject,
            Analysis = analysis,
            Views = views,
            Cario = cario,
            Mapping = mapping,
            SemanticDigest = CanonicalJsonDigest.Compute(analyzedProject)
        };
    }

    private static DateOnly RequireAsOfDate(DateOnly? asOfDate, string phase)
    {
        if (asOfDate is not null)
        {
            return asOfDate.Value;
        }

        throw new ProjectCompilationException(
            phase,
            [new ImportWarning
            {
                Id = "MISSING_AS_OF_DATE",
                Severity = WarningSeverity.Error,
                Code = "MISSING_AS_OF_DATE",
                Message = "An explicit as-of date is required; the compiler never derives one from the system clock."
            }]);
    }
}
