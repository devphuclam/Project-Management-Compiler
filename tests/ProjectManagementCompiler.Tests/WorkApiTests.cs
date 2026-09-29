using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class WorkApiTests
{
    private static readonly JsonSerializerOptions WebJsonOptions = CreateWebJsonOptions();

    public static void WorkIsIncludedInAggregateAndNamedRouteReturnsTheSameProjection()
    {
        var project = WorkTestFixtures.SyntheticProject() with
        {
            ImportMetadata = WorkTestFixtures.OfficialMetadata()
        };
        var views = new ManagementViewProjector().Build(project, new ManagementAnalysis(), WorkTestFixtures.ReportingDate);
        var workProperty = typeof(ManagementViewSet).GetProperty("Work");
        TestAssert.True(workProperty is not null, "Missing Feature 009 behavior: ManagementViewSet does not expose the aggregate Work projection.");

        var work = workProperty!.GetValue(views);
        TestAssert.True(work is not null, "The aggregate ManagementViewSet must build the Work projection from the active canonical project.");
        var aggregate = JsonSerializer.SerializeToElement(views, WebJsonOptions);
        TestAssert.True(aggregate.TryGetProperty("work", out var aggregateWork), "GET /api/views aggregate contract must serialize a work property.");
        TestAssert.Equal(
            JsonSerializer.Serialize(work, WebJsonOptions),
            aggregateWork.GetRawText(),
            "The aggregate work property must be the same projection returned by the named Work view.");

        var program = ReadProgram();
        TestAssert.Contains(
            "\"work\" => Results.Ok(current.Views.Work)",
            program,
            "GET /api/views/work must return the same active Work projection rather than a separately built model.");
    }

    public static void WorkNamedRouteRetainsTheExistingNoProjectResponse()
    {
        var program = ReadProgram();
        var routeStart = program.IndexOf("app.MapGet(\"/api/views/{viewName}\"", StringComparison.Ordinal);
        TestAssert.True(routeStart >= 0, "The Work named view must use the existing named-view route.");
        var nextRouteStart = program.IndexOf("app.MapGet(\"/api/warnings\"", routeStart, StringComparison.Ordinal);
        TestAssert.True(nextRouteStart > routeStart, "The existing named-view route boundary must remain identifiable.");
        var route = program[routeStart..nextRouteStart];

        TestAssert.Contains("Results.NotFound(new ApiErrorResponse { Code = \"NO_PROJECT\"", route, "The Work route must preserve the existing no-project HTTP 404 response.");
        TestAssert.Contains("\"work\" => Results.Ok(current.Views.Work)", route, "The Work route must be additive within the existing view switch.");
    }

    private static string ReadProgram() => File.ReadAllText(Path.Combine(
        Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "Program.cs"));

    private static JsonSerializerOptions CreateWebJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
        return options;
    }
}
