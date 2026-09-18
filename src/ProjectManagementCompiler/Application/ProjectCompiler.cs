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
    private readonly CanonicalJsonSerializer jsonSerializer;
    private readonly ExecutionOverlayUpdater executionUpdater;

    public ProjectCompiler(
        IProjectSourceAdapter? sourceAdapter = null,
        IdeaEngineeringExtractor? extractor = null,
        CanonicalProjectNormalizer? normalizer = null,
        ManagementAnalysisOrchestrator? analysisOrchestrator = null,
        ManagementViewProjector? viewProjector = null,
        CarioMappingProjector? carioMappingProjector = null,
        CarioXlsxExporter? carioXlsxExporter = null,
        CanonicalJsonSerializer? jsonSerializer = null,
        ExecutionOverlayUpdater? executionUpdater = null)
    {
        this.sourceAdapter = sourceAdapter ?? new LocalRepositorySourceAdapter();
        this.extractor = extractor ?? new IdeaEngineeringExtractor();
        this.normalizer = normalizer ?? new CanonicalProjectNormalizer();
        this.analysisOrchestrator = analysisOrchestrator ?? new ManagementAnalysisOrchestrator();
        this.viewProjector = viewProjector ?? new ManagementViewProjector();
        this.carioMappingProjector = carioMappingProjector ?? new CarioMappingProjector();
        this.carioXlsxExporter = carioXlsxExporter ?? new CarioXlsxExporter();
        this.jsonSerializer = jsonSerializer ?? new CanonicalJsonSerializer();
        this.executionUpdater = executionUpdater ?? new ExecutionOverlayUpdater();
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

        var asOfDate = RequireAsOfDate(request.AsOfDate, "compile-request");

        var snapshot = await sourceAdapter.CaptureAsync(new SourceRequest
        {
            Location = request.SourcePath,
            Ref = request.Ref,
            MaxDocumentBytes = request.MaxDocumentBytes,
            MaxTotalDocumentBytes = request.MaxTotalDocumentBytes
        }, cancellationToken);
        var resolution = AuthorityResolution.Resolve(snapshot);
        var extracted = extractor.Extract(resolution);
        var project = normalizer.Normalize(extracted);
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
        return carioXlsxExporter.Export(result.Cario);
    }

    private CompilationResult BuildResult(CanonicalProject project, DateOnly asOfDate, CarioMappingConfiguration mapping)
    {
        var validation = CanonicalProjectValidator.Validate(project);
        var validationErrors = validation.Where(diagnostic => diagnostic.Severity == WarningSeverity.Error).ToArray();
        var warnings = project.Warnings
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
