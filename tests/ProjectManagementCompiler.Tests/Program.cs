namespace ProjectManagementCompiler.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    [
        ("RunnerStarts", RunnerStarts),
        ("ManagementEvidencePreservesIndependentStatesAndTypedTargets", ManagementEvidenceTests.ManagementEvidencePreservesIndependentStatesAndTypedTargets),
        ("CanonicalProjectDefaultsManagementEvidenceWithoutProfile", ManagementEvidenceTests.CanonicalProjectDefaultsManagementEvidenceWithoutProfile),
        ("CanonicalJsonRoundTripsManagementEvidence", ManagementEvidenceTests.CanonicalJsonRoundTripsManagementEvidence),
        ("ManagementEvidencePathPolicyStaysBelowSpecs", ManagementEvidenceTests.ManagementEvidencePathPolicyStaysBelowSpecs),
        ("CaptureReadsReadinessAllowListInFixedOrder", ManagementEvidenceTests.CaptureReadsReadinessAllowListInFixedOrder),
        ("SemanticDigestWillIncludeEvidenceMeaningButNotCaptureTime", ManagementEvidenceTests.SemanticDigestWillIncludeEvidenceMeaningButNotCaptureTime),
        ("ReadinessAdapterCapturesWorkPackagesDecisionsGateActionsAndChecklistContext", ReadinessAdapterTests.ReadinessAdapterCapturesWorkPackagesDecisionsGateActionsAndChecklistContext),
        ("ReadinessAdapterRetainsStateAndResultWithoutFabricatingPass", ReadinessAdapterTests.ReadinessAdapterRetainsStateAndResultWithoutFabricatingPass),
        ("ReadinessAdapterRejectsIllegalGateExecutionOutcomePair", ReadinessAdapterTests.ReadinessAdapterRejectsIllegalGateExecutionOutcomePair),
        ("ReadinessAdapterResultNeverSerializesRawSourceOrAbsolutePath", ReadinessAdapterTests.ReadinessAdapterResultNeverSerializesRawSourceOrAbsolutePath),
        ("ReadinessAdapterReturnsUnknownWithoutAnAgreedActiveIncrement", ReadinessAdapterTests.ReadinessAdapterReturnsUnknownWithoutAnAgreedActiveIncrement),
        ("ReadinessAdapterReturnsAmbiguousForMultipleAgreedIncrements", ReadinessAdapterTests.ReadinessAdapterReturnsAmbiguousForMultipleAgreedIncrements),
        ("ReadinessAdapterReportsMissingDeclaredSource", ReadinessAdapterTests.ReadinessAdapterReportsMissingDeclaredSource),
        ("ReadinessAdapterPreservesConflictingRowsInsteadOfLastWriteWins", ReadinessAdapterTests.ReadinessAdapterPreservesConflictingRowsInsteadOfLastWriteWins),
        ("IncludeManagementEvidenceWithoutPathFailsExplicitly", Mvp21HardeningTests.IncludeManagementEvidenceWithoutPathFailsExplicitly),
        ("ReadinessFixturePreservesOwnerWaitingAndSemanticSummaries", Mvp21HardeningTests.ReadinessFixturePreservesOwnerWaitingAndSemanticSummaries),
        ("StandaloneManagementEvidenceIsValidWithoutCanonicalTarget", Mvp21HardeningTests.StandaloneManagementEvidenceIsValidWithoutCanonicalTarget),
        ("ReadinessP04ResolvesOnlyToWorkPackage", Mvp21HardeningTests.ReadinessP04ResolvesOnlyToWorkPackage),
        ("EffectiveGateSelectionUsesAuthorityAndConflictsHonestly", Mvp21HardeningTests.EffectiveGateSelectionUsesAuthorityAndConflictsHonestly),
        ("ActualGateRecordOutranksSummaryAndProjectsRecordStatus", Mvp21HardeningTests.ActualGateRecordOutranksSummaryAndProjectsRecordStatus),
        ("StateAndResultAreSeparateEffectiveFields", Mvp21HardeningTests.StateAndResultAreSeparateEffectiveFields),
        ("GateViewHasNoFakeGateWhenEvidenceIsAbsent", Mvp21HardeningTests.GateViewHasNoFakeGateWhenEvidenceIsAbsent),
        ("ProposedSuccessorIsExtractedFromControlEnvelope", Mvp21HardeningTests.ProposedSuccessorIsExtractedFromControlEnvelope),
        ("DecisionAndHumanActionSummariesReachAttentionProjection", Mvp21HardeningTests.DecisionAndHumanActionSummariesReachAttentionProjection),
        ("ActualGateRecordIsOptionalAndContractDoesNotPassGate", Mvp21HardeningTests.ActualGateRecordIsOptionalAndContractDoesNotPassGate),
        ("CanonicalJsonPreservesHardeningSemanticsWithoutRawRows", Mvp21HardeningTests.CanonicalJsonPreservesHardeningSemanticsWithoutRawRows),
        ("TypedReadinessTargetMatchesWorkPackageAndManagementRecordsRemainStandalone", ReconciliationTests.TypedReadinessTargetMatchesWorkPackageAndManagementRecordsRemainStandalone),
        ("InvalidAndAmbiguousTargetsAreExplicit", ReconciliationTests.InvalidAndAmbiguousTargetsAreExplicit),
        ("ReconciliationDoesNotChangePlanningFacts", ReconciliationTests.ReconciliationDoesNotChangePlanningFacts),
        ("PublicReadinessFixtureCapturesThroughRealFileSystem", ReadinessFixtureTests.PublicReadinessFixtureCapturesThroughRealFileSystem),
        ("PublicReadinessFixtureContainsNoAbsoluteOrPrivateMaterial", ReadinessFixtureTests.PublicReadinessFixtureContainsNoAbsoluteOrPrivateMaterial),
        ("CompilerComposesReadinessEvidenceAndReopenPreservesIt", ManagementEvidenceApplicationTests.CompilerComposesReadinessEvidenceAndReopenPreservesIt),
        ("CompilerKeepsMvp1BaselineOnlyEvidenceNotRequested", ManagementEvidenceApplicationTests.CompilerKeepsMvp1BaselineOnlyEvidenceNotRequested),
        ("DisabledReadinessIgnoresPathAndRemainsNotRequested", Mvp21FinalMicroPassTests.DisabledReadinessIgnoresPathAndRemainsNotRequested),
        ("DisabledReadinessCompileCapturesPlanningAllowListOnly", Mvp21FinalMicroPassTests.DisabledReadinessCompileCapturesPlanningAllowListOnly),
        ("DisabledReadinessKeepsCanonicalBaselineIdenticalToPlanningOnlyCompile", Mvp21FinalMicroPassTests.DisabledReadinessKeepsCanonicalBaselineIdenticalToPlanningOnlyCompile),
        ("FrontendIntakeUsesCheckboxAsAuthoritativeSwitch", Mvp21FinalMicroPassTests.FrontendIntakeUsesCheckboxAsAuthoritativeSwitch),
        ("OverviewWordingSeparatesLoadedReadinessFromBaselineControls", Mvp21FinalMicroPassTests.OverviewWordingSeparatesLoadedReadinessFromBaselineControls),
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
        ,("HtmlParserPreservesCellTextWhenNestedTagAttributesContainGreaterThan", ExtractionParserTests.HtmlParserPreservesCellTextWhenNestedTagAttributesContainGreaterThan)
        ,("HtmlParserExtractsHeadingWhenAttributesContainGreaterThan", ExtractionParserTests.HtmlParserExtractsHeadingWhenAttributesContainGreaterThan)
        ,("HtmlParserRejectsDuplicateDataAttributesWithoutThrowing", ExtractionParserTests.HtmlParserRejectsDuplicateDataAttributesWithoutThrowing)
        ,("HtmlParserParsesQuotedAndUnquotedDataAttributesAndRejectsUnquotedDuplicates", ExtractionParserTests.HtmlParserParsesQuotedAndUnquotedDataAttributesAndRejectsUnquotedDuplicates)
        ,("HtmlParserIgnoresDataAttributeTextInsideOtherAttributeValues", ExtractionParserTests.HtmlParserIgnoresDataAttributeTextInsideOtherAttributeValues)
        ,("HtmlParserRejectsMismatchedCellTagsWithoutGuessingRows", ExtractionParserTests.HtmlParserRejectsMismatchedCellTagsWithoutGuessingRows)
        ,("HtmlParserRejectsExtraCellTagsAndPreservesFollowingRows", ExtractionParserTests.HtmlParserRejectsExtraCellTagsAndPreservesFollowingRows)
        ,("HtmlParserRejectsMalformedHeaderCellTags", ExtractionParserTests.HtmlParserRejectsMalformedHeaderCellTags)
        ,("ParsersDiagnoseMissingHeadingsAndMalformedCells", ExtractionParserTests.ParsersDiagnoseMissingHeadingsAndMalformedCells)
        ,("MarkdownParserRejectsDuplicateHeadersAndWrongCellCounts", ExtractionParserTests.MarkdownParserRejectsDuplicateHeadersAndWrongCellCounts)
        ,("MarkdownParserIgnoresBacktickAndTildeFencedContent", ExtractionParserTests.MarkdownParserIgnoresBacktickAndTildeFencedContent)
        ,("MarkdownParserPreservesEscapedPipesAsCellContent", ExtractionParserTests.MarkdownParserPreservesEscapedPipesAsCellContent)
        ,("MarkdownParserStopsAdjacentTablesBeforeNextHeader", ExtractionParserTests.MarkdownParserStopsAdjacentTablesBeforeNextHeader)
        ,("MarkdownParserStopsAtHeadingsContainingPipes", ExtractionParserTests.MarkdownParserStopsAtHeadingsContainingPipes)
        ,("MarkdownParserUsesBackslashParityForEscapedPipes", ExtractionParserTests.MarkdownParserUsesBackslashParityForEscapedPipes)
        ,("MarkdownParserDiagnosesEvenBackslashPipeShapeMismatch", ExtractionParserTests.MarkdownParserDiagnosesEvenBackslashPipeShapeMismatch)
        ,("HtmlParserRejectsDuplicateHeadersAndWrongCellCounts", ExtractionParserTests.HtmlParserRejectsDuplicateHeadersAndWrongCellCounts)
        ,("HtmlParserRejectsTablesWithoutHeaderRows", ExtractionParserTests.HtmlParserRejectsTablesWithoutHeaderRows)
        ,("HtmlParserDiagnosesUnclosedTables", ExtractionParserTests.HtmlParserDiagnosesUnclosedTables)
        ,("HtmlParserDiagnosesUnclosedRowsInsideMatchedTables", ExtractionParserTests.HtmlParserDiagnosesUnclosedRowsInsideMatchedTables)
        ,("HtmlParserDiagnosesUnmatchedClosingTagsAndPreservesValidTables", ExtractionParserTests.HtmlParserDiagnosesUnmatchedClosingTagsAndPreservesValidTables)
        ,("HtmlParserDropsUnclosedRowBeforeFollowingRow", ExtractionParserTests.HtmlParserDropsUnclosedRowBeforeFollowingRow)
        ,("HtmlParserRejectsMisorderedRelevantTagsWithoutGuessingRows", ExtractionParserTests.HtmlParserRejectsMisorderedRelevantTagsWithoutGuessingRows)
        ,("HtmlParserRejectsMalformedRowAttributeTokens", ExtractionParserTests.HtmlParserRejectsMalformedRowAttributeTokens)
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
        ,("OrdinaryRowConflictsAreDetectedWithinOneSourceDocument", AuthorityResolutionTests.OrdinaryRowConflictsAreDetectedWithinOneSourceDocument)
        ,("StaleAppendixControlEnvelopeReferenceIsWarning", AuthorityResolutionTests.StaleAppendixControlEnvelopeReferenceIsWarning)
        ,("BaselineVersionControlsSubordinateEnvelopeReferencesWithoutLiteralAuthorityMarker", AuthorityResolutionTests.BaselineVersionControlsSubordinateEnvelopeReferencesWithoutLiteralAuthorityMarker)
        ,("LiteralAuthorityEnvelopeMustMatchExtractedBaselineVersion", AuthorityResolutionTests.LiteralAuthorityEnvelopeMustMatchExtractedBaselineVersion)
        ,("HistoricalAuthorityEnvelopeReferencesUseDocumentVersionAndDoNotConflict", AuthorityResolutionTests.HistoricalAuthorityEnvelopeReferencesUseDocumentVersionAndDoNotConflict)
        ,("FutureSubordinateControlEnvelopeReferenceUsesNumericVersionComparison", AuthorityResolutionTests.FutureSubordinateControlEnvelopeReferenceUsesNumericVersionComparison)
        ,("MalformedSubordinateControlEnvelopeReferencesAreErrorsAndDoNotThrow", AuthorityResolutionTests.MalformedSubordinateControlEnvelopeReferencesAreErrorsAndDoNotThrow)
        ,("FencedSubordinateControlEnvelopeReferencesAreIgnored", AuthorityResolutionTests.FencedSubordinateControlEnvelopeReferencesAreIgnored)
        ,("ConflictingAuthorityControlEnvelopeReferencesAreErrors", AuthorityResolutionTests.ConflictingAuthorityControlEnvelopeReferencesAreErrors)
        ,("EquivalentAuthorityControlEnvelopeReferencesAreSafe", AuthorityResolutionTests.EquivalentAuthorityControlEnvelopeReferencesAreSafe)
        ,("MalformedBaselineVersionsDisableCanonicalBaseline", AuthorityResolutionTests.MalformedBaselineVersionsDisableCanonicalBaseline)
        ,("EquivalentDottedBaselineVersionsRemainCanonical", AuthorityResolutionTests.EquivalentDottedBaselineVersionsRemainCanonical)
        ,("UnterminatedMarkdownFenceDisablesCanonicalBaseline", AuthorityResolutionTests.UnterminatedMarkdownFenceDisablesCanonicalBaseline)
        ,("ProjectNameFallbackIgnoresHeadingsInsideMarkdownFences", AuthorityResolutionTests.ProjectNameFallbackIgnoresHeadingsInsideMarkdownFences)
        ,("UnmatchedHtmlClosingTagsDisableCanonicalBaseline", AuthorityResolutionTests.UnmatchedHtmlClosingTagsDisableCanonicalBaseline)
        ,("ControlledFixtureResolvesBaselinePhasesAndPolicyFacts", AuthorityResolutionTests.ControlledFixtureResolvesBaselinePhasesAndPolicyFacts)
        ,("ControlledFixtureExtractsCanonicalPlanningRecords", ExtractionFixtureTests.ControlledFixtureExtractsCanonicalPlanningRecords)
        ,("ExtractionRetainsInvalidSourceDependencyEvidence", ExtractionFixtureTests.ExtractionRetainsInvalidSourceDependencyEvidence)
        ,("UnknownAuthoredExecutionStateRemainsUnknown", ExtractionFixtureTests.UnknownAuthoredExecutionStateRemainsUnknown)
        ,("CanonicalNormalizationPreservesIndependentEffortAndDuration", NormalizationTests.CanonicalNormalizationPreservesIndependentEffortAndDuration)
        ,("CanonicalNormalizationRetainsPolicyReserveStatesAndDeterministicRelationships", NormalizationTests.CanonicalNormalizationRetainsPolicyReserveStatesAndDeterministicRelationships)
        ,("WorkingCalendarNormalizesAuthoredHalfDaysWithoutUsingEffort", NormalizationTests.WorkingCalendarNormalizesAuthoredHalfDaysWithoutUsingEffort)
        ,("WorkingCalendarAcceptsSourceDefinedOneSidedHalfDayBoundaries", NormalizationTests.WorkingCalendarAcceptsSourceDefinedOneSidedHalfDayBoundaries)
        ,("PlanningOnlyCanonicalProjectStartsWithEmptyExecutionOverlay", ExecutionOverlayTests.PlanningOnlyCanonicalProjectStartsWithEmptyExecutionOverlay)
        ,("ManualUpdateStoresActualEvidenceWithoutChangingPlan", ExecutionOverlayTests.ManualUpdateStoresActualEvidenceWithoutChangingPlan)
        ,("InvalidActualFinishBeforeStartIsRejectedWithoutChangingOverlay", ExecutionOverlayTests.InvalidActualFinishBeforeStartIsRejectedWithoutChangingOverlay)
        ,("InvalidWorkItemAndNegativeEffortAreRejected", ExecutionOverlayTests.InvalidWorkItemAndNegativeEffortAreRejected)
        ,("CanonicalJsonEmitsExecutionOverlayAndExplicitUnknownFields", CanonicalJsonTests.CanonicalJsonEmitsExecutionOverlayAndExplicitUnknownFields)
        ,("CanonicalJsonRoundTripsOverlayWithoutChangingBaseline", CanonicalJsonTests.CanonicalJsonRoundTripsOverlayWithoutChangingBaseline)
        ,("Schema10JsonWithoutOverlayUsesEmptyOverlay", CanonicalJsonTests.Schema10JsonWithoutOverlayUsesEmptyOverlay)
        ,("Schema10JsonWithoutManagementEvidenceUsesEmptyEvidence", CanonicalJsonTests.Schema10JsonWithoutManagementEvidenceUsesEmptyEvidence)
        ,("SemanticDigestIgnoresCaptureAndAnalysisButIncludesOverlay", CanonicalJsonTests.SemanticDigestIgnoresCaptureAndAnalysisButIncludesOverlay)
        ,("CanonicalJsonRejectsOverlayForUnknownWorkItem", CanonicalJsonTests.CanonicalJsonRejectsOverlayForUnknownWorkItem)
        ,("CanonicalJsonRejectsInconsistentOverlayStateAndEffort", CanonicalJsonTests.CanonicalJsonRejectsInconsistentOverlayStateAndEffort)
        ,("CanonicalJsonRetainsInvalidSourceDependencyEvidence", CanonicalJsonTests.CanonicalJsonRetainsInvalidSourceDependencyEvidence)
        ,("CanonicalValidatorRejectsMissingProjectAndSourceRelationships", CanonicalValidationTests.CanonicalValidatorRejectsMissingProjectAndSourceRelationships)
        ,("CanonicalValidatorRejectsBaselineAndHierarchyViolations", CanonicalValidationTests.CanonicalValidatorRejectsBaselineAndHierarchyViolations)
        ,("CanonicalValidatorRejectsRolesAssignmentsAndDependencyContracts", CanonicalValidationTests.CanonicalValidatorRejectsRolesAssignmentsAndDependencyContracts)
        ,("CanonicalValidatorRejectsDuplicateExecutableAndInvalidOverlayRecords", CanonicalValidationTests.CanonicalValidatorRejectsDuplicateExecutableAndInvalidOverlayRecords)
        ,("CanonicalValidatorUsesTypedIdsForRealShapedP04AndDependencies", CanonicalValidationTests.CanonicalValidatorUsesTypedIdsForRealShapedP04AndDependencies)
        ,("CanonicalValidatorRejectsMalformedManagementEvidence", CanonicalValidationTests.CanonicalValidatorRejectsMalformedManagementEvidence)
        ,("StatusAnalysisUsesWorkingCalendarForLateStart", AnalysisTests.StatusAnalysisUsesWorkingCalendarForLateStart)
        ,("StatusAnalysisDerivesOverdueWithoutChangingExecutionState", AnalysisTests.StatusAnalysisDerivesOverdueWithoutChangingExecutionState)
        ,("StatusAnalysisDerivesCompletedOnTimeAndLateSeparately", AnalysisTests.StatusAnalysisDerivesCompletedOnTimeAndLateSeparately)
        ,("CancelledIsNotOverdueAndSuspendedIsExplicit", AnalysisTests.CancelledIsNotOverdueAndSuspendedIsExplicit)
        ,("UnstartedPastFinishIsLateToStartButNotActiveOverdue", AnalysisTests.UnstartedPastFinishIsLateToStartButNotActiveOverdue)
        ,("LatePredecessorMarksUnstartedSuccessorAtRiskConservatively", AnalysisTests.LatePredecessorMarksUnstartedSuccessorAtRiskConservatively)
        ,("InvalidDependencyEvidenceCannotFabricateDownstreamRisk", AnalysisTests.InvalidDependencyEvidenceCannotFabricateDownstreamRisk)
        ,("AtRiskAggregatesDelayedCardPredecessorsAndIgnoresWorkPackageEdges", AnalysisTests.AtRiskAggregatesDelayedCardPredecessorsAndIgnoresWorkPackageEdges)
        ,("WbsProjectionBuildsPhasePackageCardAndMilestoneTree", ManagementProjectionTests.WbsProjectionBuildsPhasePackageCardAndMilestoneTree)
        ,("WbsProjectionDoesNotDoubleCountExecutableCards", ManagementProjectionTests.WbsProjectionDoesNotDoubleCountExecutableCards)
        ,("SharedManagementViewsPreservePlanningExecutionAndRiskSemantics", ManagementProjectionTests.SharedManagementViewsPreservePlanningExecutionAndRiskSemantics)
        ,("CarioMappingPreservesManyToManyAssignmentsAndLeavesUnresolvedIdentityBlank", CarioMappingTests.CarioMappingPreservesManyToManyAssignmentsAndLeavesUnresolvedIdentityBlank)
        ,("CarioMappingConfigurationMapsRoleAndTaskMetadataWithoutChangingBaselineDates", CarioMappingTests.CarioMappingConfigurationMapsRoleAndTaskMetadataWithoutChangingBaselineDates)
        ,("CarioXlsxUsesTheApprovedContractHeadersAndRecordSemantics", CarioXlsxTests.CarioXlsxUsesTheApprovedContractHeadersAndRecordSemantics)
        ,("CarioXlsxUsesSixExactSheetsAndBclPackageParts", CarioXlsxTests.CarioXlsxUsesSixExactSheetsAndBclPackageParts)
        ,("CarioXlsxNeverReplacesBaselineDatesWithActualDates", CarioXlsxTests.CarioXlsxNeverReplacesBaselineDatesWithActualDates)
        ,("CarioXlsxExportIsDeterministicForTheSameModel", CarioXlsxTests.CarioXlsxExportIsDeterministicForTheSameModel)
        ,("CompilerComposesCaptureCanonicalAnalysisViewsAndCarioExport", ApplicationTests.CompilerComposesCaptureCanonicalAnalysisViewsAndCarioExport)
        ,("CompilerCompilesRealShapedFixtureThroughFullPipelineWithTypedP04Ids", ApplicationTests.CompilerCompilesRealShapedFixtureThroughFullPipelineWithTypedP04Ids)
        ,("CompilerRequiresExplicitAsOfDateForCompileAndReopen", ApplicationTests.CompilerRequiresExplicitAsOfDateForCompileAndReopen)
        ,("CompilerReopenUsesExplicitAsOfDateDeterministically", ApplicationTests.CompilerReopenUsesExplicitAsOfDateDeterministically)
        ,("CompilerExecutionUpdateSaveAndReopenPreserveBaselineAndRecalculateAlerts", ApplicationTests.CompilerExecutionUpdateSaveAndReopenPreserveBaselineAndRecalculateAlerts)
        ,("CompilerRejectsInvalidExecutionUpdateWithoutMutatingResult", ApplicationTests.CompilerRejectsInvalidExecutionUpdateWithoutMutatingResult)
        ,("CompilerRejectsMalformedCanonicalJsonWithStructuredReopenError", ApplicationTests.CompilerRejectsMalformedCanonicalJsonWithStructuredReopenError)
        ,("CpmUsesNormalizedDurationAndExcludesInvalidSourceEdges", CpmTests.CpmUsesNormalizedDurationAndExcludesInvalidSourceEdges)
        ,("CpmDetectsCyclesAndLeavesBaselineUntouched", CpmTests.CpmDetectsCyclesAndLeavesBaselineUntouched)
        ,("CpmRejectsUnsupportedEligibleEdges", CpmTests.CpmRejectsUnsupportedEligibleEdges)
        ,("CpmExcludesWorkPackageEdgesFromTheTypedCardGraph", CpmTests.CpmExcludesWorkPackageEdgesFromTheTypedCardGraph)
        ,("CpmUsesInclusiveProjectAndNodeFinishDates", CpmTests.CpmUsesInclusiveProjectAndNodeFinishDates)
        ,("OrchestratorCombinesCpmAndExecutionAnalysis", CpmTests.OrchestratorCombinesCpmAndExecutionAnalysis)
        ,("GanttKeepsPlanAndAddsActualAndAlertLanes", GanttTests.GanttKeepsPlanAndAddsActualAndAlertLanes)
        ,("GanttLeavesActualLaneUnknownWithoutExecutionEvidence", GanttTests.GanttLeavesActualLaneUnknownWithoutExecutionEvidence)
        ,("GanttKeepsInProgressActualLaneOpenForPresentation", GanttTests.GanttKeepsInProgressActualLaneOpenForPresentation)
        ,("GanttRetainsFinishOnlyExecutionEvidenceForInspector", GanttTests.GanttRetainsFinishOnlyExecutionEvidenceForInspector)
        ,("DashboardUsesAuthoritativeWorkPackageEffortAndReserveSemantics", MetricsTests.DashboardUsesAuthoritativeWorkPackageEffortAndReserveSemantics)
        ,("PlanningOnlyActualForecastAndHealthRemainUnknown", MetricsTests.PlanningOnlyActualForecastAndHealthRemainUnknown)
        ,("ActualStartAloneDoesNotFabricateForecastFromCpmFinish", MetricsTests.ActualStartAloneDoesNotFabricateForecastFromCpmFinish)
        ,("DashboardSummariesExposeKnownActualAndRemainingEffort", MetricsTests.DashboardSummariesExposeKnownActualAndRemainingEffort)
        ,("ManagementCalendarUsesWeekdaysSignedVarianceAndWorkingDayDurations", CalendarTests.ManagementCalendarUsesWeekdaysSignedVarianceAndWorkingDayDurations)
        ,("InclusiveFinishOffsetsRespectPartialDaysFridaysAndWeekends", CalendarTests.InclusiveFinishOffsetsRespectPartialDaysFridaysAndWeekends)
        ,("RealShapedAuthorityExtractsCurrentPlanningContract", RealSourceCompatibilityTests.RealShapedAuthorityExtractsCurrentPlanningContract)
        ,("RealShapedAppendixAndKanbanPreserveCardsDependenciesAndManyToManyAssignments", RealSourceCompatibilityTests.RealShapedAppendixAndKanbanPreserveCardsDependenciesAndManyToManyAssignments)
        ,("RealShapedDoc07OwnsMilestonesAndGateDependencies", RealSourceCompatibilityTests.RealShapedDoc07OwnsMilestonesAndGateDependencies)
        ,("RealShapedCaptureProvenanceAndCanonicalSourceBoundaryAreSafe", RealSourceCompatibilityTests.RealShapedCaptureProvenanceAndCanonicalSourceBoundaryAreSafe)
        ,("GanttOffersOptionalMetadataColumns", GanttUiRegressionTests.GanttOffersOptionalMetadataColumns)
        ,("GanttMetadataColumnLayoutCannotOverflowTaskPane", GanttUiRegressionTests.GanttMetadataColumnLayoutCannotOverflowTaskPane)
        ,("GanttMetadataColumnOptionsAreAlwaysVisible", GanttUiRegressionTests.GanttMetadataColumnOptionsAreAlwaysVisible)
        ,("GanttDependencyControlsExplainDirection", GanttUiRegressionTests.GanttDependencyControlsExplainDirection)
        ,("GanttDependencyConnectorsOnlyRenderWhenEnabled", GanttUiRegressionTests.GanttDependencyConnectorsOnlyRenderWhenEnabled)
        ,("GanttDependencyInspectorShowsImpact", GanttUiRegressionTests.GanttDependencyInspectorShowsImpact)
        ,("SelectingGanttRowKeepsTimelineEvidenceVisible", GanttUiRegressionTests.SelectingGanttRowKeepsTimelineEvidenceVisible)
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
