using ProjectManagementCompiler.Sources;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Tests;

internal static class HttpsCapabilityTests
{
    public static void DisabledHttpsRequestReturnsCapabilityDiagnosticWithoutDocuments()
    {
        var snapshot = new ExistingGitHttpsSourceAdapter()
            .CaptureAsync(
                new SourceRequest
                {
                    Location = "https://example.invalid/ideaengineering.git"
                },
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        TestAssert.Equal(CaptureState.Blocked, snapshot.CaptureState, "An unavailable optional capability must be safely blocked.");
        TestAssert.Equal(0, snapshot.Documents.Count, "A disabled HTTPS capability must not produce source documents.");
        TestAssert.Equal("CAPABILITY_GIT_HTTPS_UNAVAILABLE", snapshot.Diagnostics.Single().Code, "Unavailable HTTPS capture needs a stable capability diagnostic.");
    }

    public static void LocalFixtureCaptureRemainsAvailableThroughAdapter()
    {
        var fixtureRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering"));
        var snapshot = new ExistingGitHttpsSourceAdapter()
            .CaptureAsync(new SourceRequest { Location = fixtureRoot }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        TestAssert.Equal(CaptureState.Known, snapshot.CaptureState, "Local capture must remain the default available path.");
        TestAssert.Equal(5, snapshot.Documents.Count, "The local fixture must remain fully captured.");
    }
}
