using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class Mvp21FinalMicroPassTests
{
    private const string Increment = "specs/004-technical-pilot-readiness";

    public static void DisabledReadinessIgnoresPathAndRemainsNotRequested()
    {
        var sourceAdapter = new RecordingSourceAdapter();
        var result = Compile(sourceAdapter, includeManagementEvidence: false, Increment);

        TestAssert.Equal(null, sourceAdapter.LastRequest!.ManagementEvidenceIncrementPath, "A disabled readiness switch must not pass the path to source capture.");
        TestAssert.Equal(ManagementEvidenceDiscoveryState.NotRequested, result.Project.ManagementEvidence.DiscoveryState, "A disabled readiness switch must remain NOT_REQUESTED even when a path is retained in the form.");
        TestAssert.Equal(0, result.Project.ManagementEvidence.Observations.Count, "A disabled readiness switch must not attach management evidence.");
    }

    public static void DisabledReadinessCompileCapturesPlanningAllowListOnly()
    {
        var sourceAdapter = new RecordingSourceAdapter();
        _ = Compile(sourceAdapter, includeManagementEvidence: false, Increment);

        TestAssert.True(
            sourceAdapter.LastSnapshot!.Documents.All(document => !document.RelativeFile.StartsWith(Increment + "/", StringComparison.OrdinalIgnoreCase)),
            "A disabled readiness switch must not capture files below the readiness increment.");
    }

    public static void DisabledReadinessKeepsCanonicalBaselineIdenticalToPlanningOnlyCompile()
    {
        var planningOnly = Compile(new RecordingSourceAdapter(), includeManagementEvidence: false, path: null);
        var disabledWithRetainedPath = Compile(new RecordingSourceAdapter(), includeManagementEvidence: false, Increment);

        TestAssert.Equal(planningOnly.Project.Baseline, disabledWithRetainedPath.Project.Baseline, "A retained but disabled readiness path must not change the canonical baseline.");
        TestAssert.Equal(planningOnly.Project.Project.Id, disabledWithRetainedPath.Project.Project.Id, "A retained but disabled readiness path must not change project identity.");
    }

    public static void FrontendIntakeUsesCheckboxAsAuthoritativeSwitch()
    {
        var appJs = File.ReadAllText(AppJsPath());

        TestAssert.Contains("const rawPath = byId(\"management-evidence-path\").value.trim();", appJs, "Frontend intake must normalize the path separately from the checkbox.");
        TestAssert.Contains("includeManagementEvidence: includeManagementEvidence,", appJs, "Frontend payload must send the checkbox state directly.");
        TestAssert.Contains("managementEvidenceIncrementPath: includeManagementEvidence ? rawPath : null", appJs, "Frontend payload must ignore the path when readiness is disabled.");
        TestAssert.False(appJs.Contains("includeManagementEvidence: includeManagementEvidence || Boolean(managementEvidenceIncrementPath)", StringComparison.Ordinal), "Path presence must not implicitly enable readiness in the frontend.");
    }

    public static void OverviewWordingSeparatesLoadedReadinessFromBaselineControls()
    {
        var appJs = File.ReadAllText(AppJsPath());

        TestAssert.Contains("Readiness and gate evidence are shown separately.", appJs, "Loaded scheduled-phase wording must disclose the separate readiness context.");
        TestAssert.Contains("Current gate evidence is shown separately.", appJs, "Loaded baseline-control wording must disclose the separate gate context.");
        TestAssert.Contains("Actual phase entry and gate authorization are not loaded.", appJs, "Unloaded scheduled-phase wording must remain truthful.");
        TestAssert.Contains("Authoritative gate evidence is not loaded.", appJs, "Unloaded baseline-control wording must remain truthful.");
        TestAssert.Contains("context.readinessLoaded", appJs, "Overview wording must branch on the loaded readiness state.");
    }

    private static CompilationResult Compile(RecordingSourceAdapter sourceAdapter, bool includeManagementEvidence, string? path) =>
        new ProjectCompiler(sourceAdapter: sourceAdapter).CompileAsync(
            new CompilationRequest
            {
                SourcePath = RealShapedFixturePath(),
                AsOfDate = new DateOnly(2026, 9, 28),
                IncludeManagementEvidence = includeManagementEvidence,
                ManagementEvidenceIncrementPath = path
            },
            CancellationToken.None).GetAwaiter().GetResult();

    private static string RealShapedFixturePath() =>
        Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering-real-shaped");

    private static string AppJsPath() =>
        Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "app.js");

    private sealed class RecordingSourceAdapter : IProjectSourceAdapter
    {
        private readonly LocalRepositorySourceAdapter inner = new();

        public SourceRequest? LastRequest { get; private set; }

        public RepositorySnapshot? LastSnapshot { get; private set; }

        public async Task<RepositorySnapshot> CaptureAsync(SourceRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastSnapshot = await inner.CaptureAsync(request, cancellationToken);
            return LastSnapshot;
        }
    }
}
