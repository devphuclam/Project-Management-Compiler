using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;

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
builder.Services.AddSingleton<IProjectCompiler, ProjectCompiler>();
builder.Services.AddSingleton<CompilerApplicationState>();

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
    IProjectCompiler compiler,
    CompilerApplicationState state) =>
{
    var current = state.Current;
    if (current is null)
    {
        return Results.Conflict(new ApiErrorResponse
        {
            Code = "NO_PROJECT",
            Message = "Compile or reopen a project before applying execution updates.",
            Phase = "execution"
        });
    }

    var result = compiler.ApplyExecutionUpdate(current, update, current.Analysis.AsOfDate);
    if (!result.Accepted)
    {
        return Results.UnprocessableEntity(new ApiErrorResponse
        {
            Code = "INVALID_EXECUTION_UPDATE",
            Message = "Execution update was rejected.",
            Phase = "execution",
            Diagnostics = result.Diagnostics
        });
    }

    state.Set(result.Result);
    return Results.Ok(ToApplicationSummary(result.Result));
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
    var current = state.Current;
    if (current is null)
    {
        return Results.NotFound(new ApiErrorResponse { Code = "NO_PROJECT", Message = "No compiled project is loaded.", Phase = "export" });
    }

    var fileName = $"{SanitizeFileName(current.Project.Project.Name)}_CARIO.xlsx";
    return Results.File(compiler.ExportCarioXlsx(current), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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
    warnings = result.Warnings,
    semanticDigest = result.SemanticDigest
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

public sealed record CompileApiRequest
{
    public string SourcePath { get; init; } = string.Empty;
    public string? Ref { get; init; }
    public DateOnly? AsOfDate { get; init; }
    public int MaxDocumentBytes { get; init; } = 2 * 1024 * 1024;
    public int MaxTotalDocumentBytes { get; init; } = 8 * 1024 * 1024;
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
