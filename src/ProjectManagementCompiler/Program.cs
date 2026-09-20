using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Application.ManifestImport;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

const long MaximumRequestBodyBytes = 8 * 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5050");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaximumRequestBodyBytes);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
});
builder.Services.AddSingleton<ProjectCompiler>();
builder.Services.AddSingleton<IProjectCompiler>(services => services.GetRequiredService<ProjectCompiler>());
builder.Services.AddSingleton<CompilerApplicationState>();
builder.Services.AddSingleton<IManifestSourceReader, ManifestSourceReader>();
builder.Services.AddSingleton<IIdeaEngineeringManifestImporter, IdeaEngineeringManifestImporter>();
builder.Services.AddSingleton<ManifestImportApplicationService>();
builder.Services.AddSingleton<ExecutionProposalService>();
builder.Services.AddSingleton<XlsxPreviewImporter>();

var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.ContentLength is > MaximumRequestBodyBytes)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await context.Response.WriteAsJsonAsync(new ApiErrorResponse
        {
            Code = "REQUEST_TOO_LARGE",
            Message = "Request body exceeds the local MVP limit.",
            Phase = "http"
        });
        return;
    }

    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    binding = "http://127.0.0.1:5050",
    publicNetworkBinding = false
}));

app.MapPost("/api/manifest-import", async (
    ManifestImportApiRequest request,
    ManifestImportApplicationService service,
    CompilerApplicationState state,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await service.ImportAsync(new ManifestImportRequest
        {
            RepositoryRoot = request.RepositoryRoot,
            ManifestPath = request.ManifestPath,
            Mode = request.Mode,
            RequestedCommit = request.RequestedCommit,
            AnalysisAsOfOverride = request.AnalysisAsOfOverride,
            MaxFileBytes = request.MaxFileBytes,
            MaxTotalBytes = request.MaxTotalBytes
        }, request.Mapping, cancellationToken);
        return Results.Ok(ToManifestImportResponse(result, state));
    }
    catch (ProjectCompilationException exception)
    {
        return Results.UnprocessableEntity(ToError(exception));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiErrorResponse
        {
            Code = "INVALID_MANIFEST_IMPORT_REQUEST",
            Message = exception.Message,
            Phase = "manifest-import"
        });
    }
});

app.MapGet("/api/manifest-import/official", (CompilerApplicationState state) =>
{
    var snapshot = state.CurrentOfficialSnapshot;
    return snapshot is null
        ? Results.NotFound(new ApiErrorResponse { Code = "NO_OFFICIAL_SNAPSHOT", Message = "No official manifest snapshot is loaded.", Phase = "manifest-import" })
        : Results.Ok(ToManifestSnapshotResponse(snapshot, ManifestImportClassification.OfficialCommit, state.CurrentOfficialResult));
});

app.MapGet("/api/manifest-import/latest", (CompilerApplicationState state) =>
{
    var attempt = state.LatestImportAttempt;
    return attempt is null
        ? Results.NotFound(new ApiErrorResponse { Code = "NO_IMPORT_ATTEMPT", Message = "No manifest import has been attempted.", Phase = "manifest-import" })
        : Results.Ok(new { classification = attempt.Classification, attempt, diagnostics = attempt.Diagnostics });
});

app.MapGet("/api/manifest-import/preview", (CompilerApplicationState state) =>
{
    var preview = state.ActivePreview;
    return preview is null
        ? Results.NotFound(new ApiErrorResponse { Code = "NO_ACTIVE_PREVIEW", Message = "No candidate or working-tree preview is active.", Phase = "manifest-import" })
        : Results.Ok(ToManifestSnapshotResponse(preview, preview.Metadata.Classification, state.ActivePreviewResult));
});

app.MapGet("/api/manifest-import/official/latest/preview", (CompilerApplicationState state) =>
{
    var preview = state.ActivePreview;
    return preview is null
        ? Results.NotFound(new ApiErrorResponse { Code = "NO_ACTIVE_PREVIEW", Message = "No candidate or working-tree preview is active.", Phase = "manifest-import" })
        : Results.Ok(ToManifestSnapshotResponse(preview, preview.Metadata.Classification, state.ActivePreviewResult));
});

app.MapDelete("/api/manifest-import/preview", (ManifestImportApplicationService service) =>
{
    service.ClearPreview();
    return Results.NoContent();
});

app.MapPost("/api/xlsx-preview", async (
    HttpRequest request,
    XlsxPreviewImporter importer,
    CompilerApplicationState state,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new ApiErrorResponse
        {
            Code = "INVALID_XLSX_PREVIEW_REQUEST",
            Message = "The preview upload must be multipart/form-data with a file field named 'file'.",
            Phase = "xlsx-preview"
        });
    }

    try
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return Results.BadRequest(new ApiErrorResponse
            {
                Code = "INVALID_XLSX_PREVIEW_REQUEST",
                Message = "The preview upload must include a file field named 'file'.",
                Phase = "xlsx-preview"
            });
        }

        if (file.Length > MaximumRequestBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        var result = importer.Import(file.FileName, buffer.ToArray());
        if (!result.IsValid)
        {
            return Results.UnprocessableEntity(ToXlsxPreviewError(result));
        }

        state.SetXlsxPreview(result.Preview!);
        return Results.Ok(result.Preview);
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (InvalidDataException exception)
    {
        return Results.UnprocessableEntity(new ApiErrorResponse
        {
            Code = "INVALID_XLSX_PREVIEW",
            Message = exception.Message,
            Phase = "xlsx-preview"
        });
    }
});

app.MapGet("/api/xlsx-preview", (CompilerApplicationState state) =>
{
    var preview = state.ActiveXlsxPreview;
    return preview is null
        ? Results.NotFound(new ApiErrorResponse
        {
            Code = "NO_ACTIVE_XLSX_PREVIEW",
            Message = "No XLSX preview is active.",
            Phase = "xlsx-preview"
        })
        : Results.Ok(preview);
});

app.MapDelete("/api/xlsx-preview", (CompilerApplicationState state) =>
{
    state.ClearXlsxPreview();
    return Results.NoContent();
});

app.MapPost("/api/compile", async (
    CompileApiRequest request,
    IProjectCompiler compiler,
    CompilerApplicationState state,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.SourcePath) || !Directory.Exists(request.SourcePath))
        {
            return Results.BadRequest(new ApiErrorResponse
            {
                Code = "INVALID_SOURCE_PATH",
                Message = "sourcePath must identify an existing local directory.",
                Phase = "capture"
            });
        }

        if (request.MaxDocumentBytes <= 0
            || request.MaxDocumentBytes > 2 * 1024 * 1024
            || request.MaxTotalDocumentBytes <= 0
            || request.MaxTotalDocumentBytes > MaximumRequestBodyBytes
            || request.MaxDocumentBytes > request.MaxTotalDocumentBytes)
        {
            return Results.BadRequest(new ApiErrorResponse
            {
                Code = "INVALID_SOURCE_LIMITS",
                Message = "Source limits must be positive, bounded to 2 MiB per file and 8 MiB total, and the per-file limit cannot exceed the total limit.",
                Phase = "capture"
            });
        }

        var result = await compiler.CompileAsync(new CompilationRequest
        {
            SourcePath = Path.GetFullPath(request.SourcePath),
            Ref = request.Ref,
            AsOfDate = request.AsOfDate,
            IncludeManagementEvidence = request.IncludeManagementEvidence,
            ManagementEvidenceIncrementPath = request.ManagementEvidenceIncrementPath,
            MaxDocumentBytes = request.MaxDocumentBytes,
            MaxTotalDocumentBytes = request.MaxTotalDocumentBytes,
            Mapping = request.Mapping ?? new()
        }, cancellationToken);
        state.Set(result);
        return Results.Ok(ToApplicationSummary(result));
    }
    catch (ProjectCompilationException exception)
    {
        return Results.UnprocessableEntity(ToError(exception));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiErrorResponse
        {
            Code = "INVALID_COMPILE_REQUEST",
            Message = exception.Message,
            Phase = "capture"
        });
    }
});

app.MapPost("/api/reopen", (
    ReopenApiRequest request,
    IProjectCompiler compiler,
    CompilerApplicationState state) =>
{
    try
    {
        var result = compiler.Reopen(request.Json, request.AsOfDate, request.Mapping);
        state.Set(result);
        return Results.Ok(ToApplicationSummary(result));
    }
    catch (ProjectCompilationException exception)
    {
        return Results.UnprocessableEntity(ToError(exception));
    }
});

app.MapPost("/api/execution", (
    ExecutionUpdate update,
    ExecutionProposalService proposals,
    CompilerApplicationState state) =>
{
    if (state.CurrentOfficialSnapshot is null)
    {
        return Results.Conflict(new ApiErrorResponse
        {
            Code = "NO_OFFICIAL_SNAPSHOT",
            Message = "Import an official manifest snapshot before creating proposal-only execution updates.",
            Phase = "execution"
        });
    }

    try
    {
        var proposedChanges = new Dictionary<string, string?>
        {
            ["executionState"] = FormatExecutionState(update.ExecutionState),
            ["actualStart"] = update.ActualStart?.ToString("yyyy-MM-dd"),
            ["actualFinish"] = update.ActualFinish?.ToString("yyyy-MM-dd"),
            ["actualEffortHours"] = update.ActualEffortHours?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["remainingEffortHours"] = update.RemainingEffortHours?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["blocker"] = update.Note
        };
        if (update.EvidenceReference is not null)
        {
            proposedChanges["legacyEvidenceReference"] = update.EvidenceReference.RelativeFile;
        }

        var proposal = proposals.Create(new CreateExecutionProposalRequest
        {
            TargetKind = "DeliveryCard",
            TargetId = update.WorkItemId,
            ProposedChanges = proposedChanges,
            Evidence = Array.Empty<SourceExecutionEvidence>()
        });
        return Results.Ok(new { proposalOnly = true, authoritative = false, proposal });
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
    {
        return Results.UnprocessableEntity(new ApiErrorResponse
        {
            Code = "INVALID_EXECUTION_PROPOSAL",
            Message = exception.Message,
            Phase = "execution"
        });
    }
});

app.MapGet("/api/proposals", (string? snapshotId, ExecutionProposalService proposals) => Results.Ok(proposals.List(snapshotId)));

app.MapPost("/api/proposals", (CreateExecutionProposalRequest request, ExecutionProposalService proposals) =>
{
    try
    {
        return Results.Ok(proposals.Create(request));
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
    {
        return Results.UnprocessableEntity(new ApiErrorResponse { Code = "INVALID_PROPOSAL", Message = exception.Message, Phase = "proposal" });
    }
});

app.MapPut("/api/proposals/{id}", (string id, UpdateExecutionProposalRequest request, ExecutionProposalService proposals) =>
{
    try
    {
        return Results.Ok(proposals.Update(id, request));
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
    {
        return Results.UnprocessableEntity(new ApiErrorResponse { Code = "INVALID_PROPOSAL", Message = exception.Message, Phase = "proposal" });
    }
});

app.MapPost("/api/proposals/{id}/preview", (string id, ExecutionProposalService proposals) =>
{
    try
    {
        return Results.Ok(proposals.Preview(id));
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
    {
        return Results.UnprocessableEntity(new ApiErrorResponse { Code = "INVALID_PROPOSAL_PREVIEW", Message = exception.Message, Phase = "proposal" });
    }
});

app.MapGet("/api/proposals/{id}/export", (string id, ExecutionProposalService proposals) =>
{
    try
    {
        return Results.Text(proposals.Export(id), "application/json");
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
    {
        return Results.UnprocessableEntity(new ApiErrorResponse { Code = "INVALID_PROPOSAL_EXPORT", Message = exception.Message, Phase = "proposal" });
    }
});

app.MapGet("/api/manifest-import/exports/official.json", (IProjectCompiler compiler, CompilerApplicationState state) =>
{
    var current = state.CurrentOfficialResult;
    return current is null
        ? Results.NotFound(new ApiErrorResponse { Code = "NO_OFFICIAL_SNAPSHOT", Message = "No official manifest snapshot is loaded.", Phase = "export" })
        : Results.File(
            System.Text.Encoding.UTF8.GetBytes(compiler.SaveJson(current)),
            "application/json",
            "manifest-official.json");
});

app.MapGet("/api/manifest-import/exports/preview.json", (IProjectCompiler compiler, CompilerApplicationState state) =>
{
    var current = state.ActivePreviewResult;
    var preview = state.ActivePreview;
    return current is null || preview is null
        ? Results.NotFound(new ApiErrorResponse { Code = "NO_ACTIVE_PREVIEW", Message = "No valid preview snapshot is available for export.", Phase = "export" })
        : Results.File(
            System.Text.Encoding.UTF8.GetBytes(compiler.SaveJson(current)),
            "application/json",
            $"manifest-preview-{preview.Metadata.SnapshotId}.json");
});

app.MapGet("/api/project", (CompilerApplicationState state) =>
{
    var current = state.Current;
    return current is null
        ? Results.NotFound(new ApiErrorResponse { Code = "NO_PROJECT", Message = "No compiled project is loaded.", Phase = "project" })
        : Results.Ok(current.Project);
});

app.MapGet("/api/views", (CompilerApplicationState state) =>
{
    var current = state.Current;
    return current is null
        ? Results.NotFound(new ApiErrorResponse { Code = "NO_PROJECT", Message = "No compiled project is loaded.", Phase = "views" })
        : Results.Ok(current.Views);
});

app.MapGet("/api/views/{viewName}", (string viewName, CompilerApplicationState state) =>
{
    var current = state.Current;
    if (current is null)
    {
        return Results.NotFound(new ApiErrorResponse { Code = "NO_PROJECT", Message = "No compiled project is loaded.", Phase = "views" });
    }

    return viewName.ToLowerInvariant() switch
    {
        "dashboard" => Results.Ok(current.Views.Dashboard),
        "wbs" => Results.Ok(current.Views.Wbs),
        "gantt" => Results.Ok(current.Views.Gantt),
        "kanban" => Results.Ok(current.Views.Kanban),
        "dependencies" => Results.Ok(current.Views.DependencyNetwork),
        "cpm" => Results.Ok(current.Views.Cpm),
        "management-control" or "management" => Results.Ok(current.Views.ManagementControl),
        _ => Results.NotFound(new ApiErrorResponse { Code = "UNKNOWN_VIEW", Message = $"Unknown view '{viewName}'.", Phase = "views" })
    };
});

app.MapGet("/api/warnings", (CompilerApplicationState state) =>
{
    var current = state.Current;
    return current is null
        ? Results.NotFound(new ApiErrorResponse { Code = "NO_PROJECT", Message = "No compiled project is loaded.", Phase = "warnings" })
        : Results.Ok(current.Warnings);
});

app.MapGet("/api/exports/project.json", (IProjectCompiler compiler, CompilerApplicationState state) =>
{
    var current = state.Current;
    return current is null
        ? Results.NotFound(new ApiErrorResponse { Code = "NO_PROJECT", Message = "No compiled project is loaded.", Phase = "export" })
        : Results.File(
            System.Text.Encoding.UTF8.GetBytes(compiler.SaveJson(current)),
            "application/json",
            $"{SanitizeFileName(current.Project.Project.Name)}_project.json");
});

app.MapGet("/api/exports/cario.xlsx", (IProjectCompiler compiler, CompilerApplicationState state) =>
{
    var current = WorkbookExportSelection.ResolveOfficial(state);
    if (current is null)
    {
        var code = state.Current is null ? "NO_PROJECT" : "NO_OFFICIAL_SNAPSHOT";
        var message = state.Current is null
            ? "No compiled project is loaded."
            : "The active workbook export is limited to the official source snapshot; import an official commit first.";
        return Results.NotFound(new ApiErrorResponse { Code = code, Message = message, Phase = "export" });
    }

    var fileName = $"{SanitizeFileName(current.Project.Project.Name)}_CARIO_GANTT.xlsx";
    return Results.File(compiler.ExportCarioXlsx(current), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
});

app.MapGet("/api/exports/cario-preview.xlsx", (IProjectCompiler compiler, CompilerApplicationState state) =>
{
    var preview = WorkbookExportSelection.ResolvePreview(state);
    if (preview is null)
    {
        return Results.NotFound(new ApiErrorResponse
        {
            Code = "NO_ACTIVE_PREVIEW",
            Message = "No non-authoritative preview is available for export.",
            Phase = "export"
        });
    }

    var fileName = $"{SanitizeFileName(preview.Project.Project.Name)}_CARIO_GANTT_PREVIEW.xlsx";
    return Results.File(compiler.ExportCarioXlsx(preview), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
});

app.MapFallbackToFile("index.html");
app.Run();

static object ToApplicationSummary(CompilationResult result) => new
{
    project = result.Project.Project,
    baseline = result.Project.Baseline,
    sources = result.Project.Sources.Select(source => new
    {
        sourceId = source.Id,
        kind = source.Kind,
        repository = SafeRepositoryIdentity(source),
        resolvedRef = source.ResolvedRef,
        captureState = source.CaptureState,
        capturedAtUtc = source.CapturedAtUtc,
        documents = source.Documents.Select(document => new
        {
            documentId = document.Id,
            relativeFile = document.RelativeFile,
            format = document.Format,
            sizeBytes = document.SizeBytes,
            provenance = new
            {
                sourceId = document.SourceReference.SourceId,
                repository = SafeRepositoryIdentity(source),
                resolvedRef = document.SourceReference.ResolvedRef,
                relativeFile = document.SourceReference.RelativeFile,
                extractionRule = document.SourceReference.ExtractionRule,
                section = document.SourceReference.Section,
                table = document.SourceReference.Table,
                item = document.SourceReference.Item,
                confidenceState = document.SourceReference.ConfidenceState,
                validationState = document.SourceReference.ValidationState
            }
        }).ToArray()
    }).ToArray(),
    analysis = result.Analysis,
    views = result.Views,
    managementEvidence = result.Project.ManagementEvidence,
    managementControl = result.Views.ManagementControl,
    warnings = result.Warnings,
    semanticDigest = result.SemanticDigest
};

static object ToManifestImportResponse(ManifestImportResult result, CompilerApplicationState state) => new
{
    classification = result.Classification,
    snapshot = result.Snapshot is null ? null : ToManifestSnapshotResponse(
        result.Snapshot,
        result.Classification,
        result.Classification == ManifestImportClassification.OfficialCommit
            ? state.CurrentOfficialResult
            : state.ActivePreviewResult),
    attempt = result.Attempt,
    diagnostics = result.Diagnostics
};

static object ToManifestSnapshotResponse(
    IdeaEngineeringSnapshot snapshot,
    ManifestImportClassification classification,
    CompilationResult? compiled = null) => new
{
    classification,
    metadata = snapshot.Metadata,
    project = snapshot.Project,
    projectSummary = compiled?.Project.Project ?? snapshot.Project.Project,
    baseline = compiled?.Project.Baseline ?? snapshot.Project.Baseline,
    sources = compiled?.Project.Sources ?? snapshot.Project.Sources,
    analysis = compiled?.Analysis ?? snapshot.Project.Analysis,
    views = compiled?.Views,
    managementEvidence = compiled?.Project.ManagementEvidence ?? snapshot.Project.ManagementEvidence,
    managementControl = compiled?.Views.ManagementControl,
    warnings = compiled?.Warnings ?? snapshot.Project.Warnings,
    semanticDigest = compiled?.SemanticDigest,
    sourceExecution = snapshot.SourceExecution,
    diagnostics = snapshot.Diagnostics
};

static string SafeRepositoryIdentity(ProjectSource source)
{
    var repository = source.Repository?.Trim() ?? string.Empty;
    if (repository.Length == 0
        || Path.IsPathRooted(repository)
        || repository.Contains('\\')
        || repository.Contains(':')
        || (Uri.TryCreate(repository, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.UserInfo)))
    {
        return source.Id;
    }

    return repository;
}

static ApiErrorResponse ToError(ProjectCompilationException exception) => new()
{
    Code = "COMPILATION_FAILED",
    Message = exception.Message,
    Phase = exception.Phase,
    Diagnostics = exception.Diagnostics
};

static ApiErrorResponse ToXlsxPreviewError(XlsxPreviewImportResult result) => new()
{
    Code = "INVALID_XLSX_PREVIEW",
    Message = "The selected workbook did not satisfy the PMC preview contract.",
    Phase = "xlsx-preview",
    Diagnostics = result.Diagnostics
        .Select(diagnostic => new ImportWarning
        {
            Id = diagnostic.Code,
            Code = diagnostic.Code,
            Severity = WarningSeverity.Error,
            Message = diagnostic.Message
        })
        .ToArray()
};

static string SanitizeFileName(string value)
{
    var invalid = Path.GetInvalidFileNameChars();
    var builder = new System.Text.StringBuilder();
    var separatorPending = false;
    foreach (var character in value.Trim())
    {
        if (char.IsWhiteSpace(character) || invalid.Contains(character))
        {
            separatorPending = builder.Length > 0;
            continue;
        }

        if (separatorPending)
        {
            builder.Append('_');
            separatorPending = false;
        }

        builder.Append(character);
    }

    var safe = builder.ToString().Trim('_');
    return string.IsNullOrWhiteSpace(safe) ? "Project" : safe;
}

static string FormatExecutionState(ExecutionState state) => state switch
{
    ExecutionState.NotStarted => "NOT_STARTED",
    ExecutionState.InProgress => "IN_PROGRESS",
    ExecutionState.Completed => "COMPLETED",
    ExecutionState.Suspended => "SUSPENDED",
    ExecutionState.Cancelled => "CANCELLED",
    _ => state.ToString().ToUpperInvariant()
};

public sealed record CompileApiRequest
{
    public string SourcePath { get; init; } = string.Empty;
    public string? Ref { get; init; }
    public DateOnly? AsOfDate { get; init; }
    public bool IncludeManagementEvidence { get; init; }
    public string? ManagementEvidenceIncrementPath { get; init; }
    public int MaxDocumentBytes { get; init; } = 2 * 1024 * 1024;
    public int MaxTotalDocumentBytes { get; init; } = 8 * 1024 * 1024;
    public CarioMappingConfiguration? Mapping { get; init; }
}

public sealed record ManifestImportApiRequest
{
    public string RepositoryRoot { get; init; } = string.Empty;
    public string ManifestPath { get; init; } = "planning/project-management-compiler-manifest.json";
    public ManifestImportMode Mode { get; init; } = ManifestImportMode.GitCommit;
    public string? RequestedCommit { get; init; }
    public DateOnly? AnalysisAsOfOverride { get; init; }
    public int MaxFileBytes { get; init; } = 4 * 1024 * 1024;
    public int MaxTotalBytes { get; init; } = 32 * 1024 * 1024;
    public CarioMappingConfiguration? Mapping { get; init; }
}

public sealed record ReopenApiRequest
{
    public string Json { get; init; } = string.Empty;
    public DateOnly? AsOfDate { get; init; }
    public CarioMappingConfiguration? Mapping { get; init; }
}

public sealed record ApiErrorResponse
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Phase { get; init; } = string.Empty;
    public IReadOnlyList<ImportWarning> Diagnostics { get; init; } = Array.Empty<ImportWarning>();
}
