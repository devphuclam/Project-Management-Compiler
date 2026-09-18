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
        ("SnapshotMetadataNeverExposesAbsoluteLocalRoot", SourceCaptureTests.SnapshotMetadataNeverExposesAbsoluteLocalRoot),
        ("MissingRecognizedFileProducesStableDiagnostic", SourceCaptureTests.MissingRecognizedFileProducesStableDiagnostic),
        ("RefIsCopiedToEveryDocumentAndDiagnosticReference", SourceCaptureTests.RefIsCopiedToEveryDocumentAndDiagnosticReference),
        ("DiagnosticsAreProducedInRecognizedPathOrder", SourceCaptureTests.DiagnosticsAreProducedInRecognizedPathOrder),
        ("UnreadableRootProducesBlockedDiagnostic", SourceCaptureTests.UnreadableRootProducesBlockedDiagnostic),
        ("RootedAndTraversalPathsAreRejected", SourceCaptureTests.RootedAndTraversalPathsAreRejected),
        ("DriveRootNormalizationPreservesTrailingSeparator", SourceCaptureTests.DriveRootNormalizationPreservesTrailingSeparator),
        ("ReparsePointEscapeProducesDiagnosticWithoutReading", SourceCaptureTests.ReparsePointEscapeProducesDiagnosticWithoutReading),
        ("FileLevelReparsePointIsRejectedWithoutReading", SourceCaptureTests.FileLevelReparsePointIsRejectedWithoutReading),
        ("CaptureUsesValidatedReadBoundaryInsteadOfLegacyChecks", SourceCaptureTests.CaptureUsesValidatedReadBoundaryInsteadOfLegacyChecks),
        ("OversizedFilesAreRejectedBeforeRead", SourceCaptureTests.OversizedFilesAreRejectedBeforeRead),
        ("ValidatedReadRejectsBoundaryReparseWithoutReading", SourceCaptureTests.ValidatedReadRejectsBoundaryReparseWithoutReading),
        ("ValidatedReadRejectsBoundaryOversizeWithoutPartialRead", SourceCaptureTests.ValidatedReadRejectsBoundaryOversizeWithoutPartialRead),
        ("TotalSizeLimitIsRejectedBeforeReadingOverLimitDocument", SourceCaptureTests.TotalSizeLimitIsRejectedBeforeReadingOverLimitDocument),
        ("TotalSizeAccountingUsesActualReadBytes", SourceCaptureTests.TotalSizeAccountingUsesActualReadBytes),
        ("StrictUtf8AcceptsValidContent", SourceCaptureTests.StrictUtf8AcceptsValidContent),
        ("StrictUtf8RejectsInvalidBytesWithoutPartialDocument", SourceCaptureTests.StrictUtf8RejectsInvalidBytesWithoutPartialDocument),
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
