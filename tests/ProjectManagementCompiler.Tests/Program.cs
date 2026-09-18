namespace ProjectManagementCompiler.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    [
        ("RunnerStarts", RunnerStarts),
        ("DataStateIncludesCanonicalDerivedAndResolutionStates", DomainModelTests.DataStateIncludesCanonicalDerivedAndResolutionStates),
        ("PlannedEntityPreservesPlannedEffortState", DomainModelTests.PlannedEntityPreservesPlannedEffortState),
        ("ManagementAnalysisPreservesForecastConstraintAndEffortAccounting", DomainModelTests.ManagementAnalysisPreservesForecastConstraintAndEffortAccounting),
        ("CanonicalProjectPreservesIndependentBaselineValues", DomainModelTests.CanonicalProjectPreservesIndependentBaselineValues),
        ("RecognizedFixturePathsExistAndArePublicSafe", FixtureShapeTests.RecognizedFixturePathsExistAndArePublicSafe),
        ("FixtureHasRequiredCountsAndAuthoritativeEffort", FixtureShapeTests.FixtureHasRequiredCountsAndAuthoritativeEffort),
        ("SourceRequestUsesSafeSizeDefaults", SourceCaptureTests.SourceRequestUsesSafeSizeDefaults),
        ("CaptureReadsAllowListedDocumentsInFixedOrder", SourceCaptureTests.CaptureReadsAllowListedDocumentsInFixedOrder),
        ("CapturePreservesNormalizedReferencesAndOneTimestamp", SourceCaptureTests.CapturePreservesNormalizedReferencesAndOneTimestamp),
        ("MissingRecognizedFileProducesStableDiagnostic", SourceCaptureTests.MissingRecognizedFileProducesStableDiagnostic),
        ("UnreadableRootProducesBlockedDiagnostic", SourceCaptureTests.UnreadableRootProducesBlockedDiagnostic),
        ("RootedAndTraversalPathsAreRejected", SourceCaptureTests.RootedAndTraversalPathsAreRejected),
        ("ReparsePointEscapeProducesDiagnosticWithoutReading", SourceCaptureTests.ReparsePointEscapeProducesDiagnosticWithoutReading),
        ("OversizedFilesAreRejectedBeforeRead", SourceCaptureTests.OversizedFilesAreRejectedBeforeRead),
        ("TotalSizeLimitIsRejectedBeforeReadingOverLimitDocument", SourceCaptureTests.TotalSizeLimitIsRejectedBeforeReadingOverLimitDocument),
        ("FixtureCaptureWorksAgainstRealFileSystem", SourceCaptureTests.FixtureCaptureWorksAgainstRealFileSystem)
    ];

    public static int Main()
    {
        var failures = 0;

        foreach (var (name, test) in Tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        return failures == 0 ? 0 : 1;
    }

    private static void RunnerStarts()
    {
        TestAssert.True(true, "The test runner should execute a test delegate.");
    }
}
