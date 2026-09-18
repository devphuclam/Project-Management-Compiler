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
        ("CapturePassesNormalizedAllowedRootToValidatedReadBoundary", SourceCaptureTests.CapturePassesNormalizedAllowedRootToValidatedReadBoundary),
        ("Win32ReadFailureProducesBlockedCaptureDiagnostic", SourceCaptureTests.Win32ReadFailureProducesBlockedCaptureDiagnostic),
        ("OversizedFilesAreRejectedBeforeRead", SourceCaptureTests.OversizedFilesAreRejectedBeforeRead),
        ("ValidatedReadRejectsBoundaryReparseWithoutReading", SourceCaptureTests.ValidatedReadRejectsBoundaryReparseWithoutReading),
        ("ValidatedReadRejectsBoundaryOversizeWithoutPartialRead", SourceCaptureTests.ValidatedReadRejectsBoundaryOversizeWithoutPartialRead),
        ("TotalSizeLimitIsRejectedBeforeReadingOverLimitDocument", SourceCaptureTests.TotalSizeLimitIsRejectedBeforeReadingOverLimitDocument),
        ("TotalSizeAccountingUsesActualReadBytes", SourceCaptureTests.TotalSizeAccountingUsesActualReadBytes),
        ("StrictUtf8AcceptsValidContent", SourceCaptureTests.StrictUtf8AcceptsValidContent),
        ("StrictUtf8RejectsInvalidBytesWithoutPartialDocument", SourceCaptureTests.StrictUtf8RejectsInvalidBytesWithoutPartialDocument),
        ("FixtureCaptureWorksAgainstRealFileSystem", SourceCaptureTests.FixtureCaptureWorksAgainstRealFileSystem),
        ("DisabledHttpsRequestReturnsCapabilityDiagnosticWithoutDocuments", HttpsCapabilityTests.DisabledHttpsRequestReturnsCapabilityDiagnosticWithoutDocuments),
        ("LocalFixtureCaptureRemainsAvailableThroughAdapter", HttpsCapabilityTests.LocalFixtureCaptureRemainsAvailableThroughAdapter)
        ,("MarkdownParserPreservesVietnameseAndStableSourceMetadata", ExtractionParserTests.MarkdownParserPreservesVietnameseAndStableSourceMetadata)
        ,("HtmlParserReadsTablesAndDataAttributesInStableOrder", ExtractionParserTests.HtmlParserReadsTablesAndDataAttributesInStableOrder)
        ,("HtmlParserRejectsDuplicateDataAttributesWithoutThrowing", ExtractionParserTests.HtmlParserRejectsDuplicateDataAttributesWithoutThrowing)
        ,("HtmlParserRejectsMismatchedCellTagsWithoutGuessingRows", ExtractionParserTests.HtmlParserRejectsMismatchedCellTagsWithoutGuessingRows)
        ,("ParsersDiagnoseMissingHeadingsAndMalformedCells", ExtractionParserTests.ParsersDiagnoseMissingHeadingsAndMalformedCells)
        ,("MarkdownParserRejectsDuplicateHeadersAndWrongCellCounts", ExtractionParserTests.MarkdownParserRejectsDuplicateHeadersAndWrongCellCounts)
        ,("MarkdownParserIgnoresBacktickAndTildeFencedContent", ExtractionParserTests.MarkdownParserIgnoresBacktickAndTildeFencedContent)
        ,("MarkdownParserPreservesEscapedPipesAsCellContent", ExtractionParserTests.MarkdownParserPreservesEscapedPipesAsCellContent)
        ,("HtmlParserRejectsDuplicateHeadersAndWrongCellCounts", ExtractionParserTests.HtmlParserRejectsDuplicateHeadersAndWrongCellCounts)
        ,("HtmlParserRejectsTablesWithoutHeaderRows", ExtractionParserTests.HtmlParserRejectsTablesWithoutHeaderRows)
        ,("DiscoveryIgnoresUnrecognizedCapturedDocuments", ExtractionParserTests.DiscoveryIgnoresUnrecognizedCapturedDocuments)
        ,("Doc07WinsPrecedenceAndResolvesTypedPlanningFacts", AuthorityResolutionTests.Doc07WinsPrecedenceAndResolvesTypedPlanningFacts)
        ,("MissingAuthorityIsSafeAndCannotBecomeCanonical", AuthorityResolutionTests.MissingAuthorityIsSafeAndCannotBecomeCanonical)
        ,("RequiredDocumentErrorsDisableCanonicalBaseline", AuthorityResolutionTests.RequiredDocumentErrorsDisableCanonicalBaseline)
        ,("MissingRequiredBaselineDataAndRowsDisableCanonicalBaseline", AuthorityResolutionTests.MissingRequiredBaselineDataAndRowsDisableCanonicalBaseline)
        ,("MissingRequiredBaselineFactsDisableCanonicalBaselineWithoutGuessing", AuthorityResolutionTests.MissingRequiredBaselineFactsDisableCanonicalBaselineWithoutGuessing)
        ,("UnsupportedRequiredDocumentFormatDisablesCanonicalBaseline", AuthorityResolutionTests.UnsupportedRequiredDocumentFormatDisablesCanonicalBaseline)
        ,("GenericSemanticValuesProduceErrorsAndDoNotBecomeNullSilently", AuthorityResolutionTests.GenericSemanticValuesProduceErrorsAndDoNotBecomeNullSilently)
        ,("TypedBaselineValuesOnlyUseDoc07AuthorityFacts", AuthorityResolutionTests.TypedBaselineValuesOnlyUseDoc07AuthorityFacts)
        ,("AllRowsAreRetainedAndOrdinaryRowConflictsPreserveSources", AuthorityResolutionTests.AllRowsAreRetainedAndOrdinaryRowConflictsPreserveSources)
        ,("StaleAppendixControlEnvelopeReferenceIsWarning", AuthorityResolutionTests.StaleAppendixControlEnvelopeReferenceIsWarning)
        ,("FutureSubordinateControlEnvelopeReferenceUsesNumericVersionComparison", AuthorityResolutionTests.FutureSubordinateControlEnvelopeReferenceUsesNumericVersionComparison)
        ,("MalformedSubordinateControlEnvelopeReferencesAreErrorsAndDoNotThrow", AuthorityResolutionTests.MalformedSubordinateControlEnvelopeReferencesAreErrorsAndDoNotThrow)
        ,("UnterminatedMarkdownFenceDisablesCanonicalBaseline", AuthorityResolutionTests.UnterminatedMarkdownFenceDisablesCanonicalBaseline)
        ,("ControlledFixtureResolvesBaselinePhasesAndPolicyFacts", AuthorityResolutionTests.ControlledFixtureResolvesBaselinePhasesAndPolicyFacts)
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
