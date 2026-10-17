namespace BookOfEternityClient.Tests;

internal static class FastBoundaryTestInventories
{
    internal const string QteDeterministicLogic = """
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0071|string:q
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0051|string:q
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0439|string:q
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0419|string:q
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0446|string:w
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0426|string:w
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0443|string:e
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0423|string:e
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0444|string:a
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0424|string:a
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+044B|string:s
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+042B|string:s
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0432|string:d
        Theory|QteKeyInput_NormalizesConsoleFallbackCharacters|char:U+0412|string:d
        Theory|QteKeyInput_FormatsPhysicalKeyLabelsWithRuFallback|syntax:ConsoleKey.Q|string:Q / Й
        Theory|QteKeyInput_FormatsPhysicalKeyLabelsWithRuFallback|syntax:ConsoleKey.W|string:W / Ц
        Theory|QteKeyInput_FormatsPhysicalKeyLabelsWithRuFallback|syntax:ConsoleKey.E|string:E / У
        Theory|QteKeyInput_FormatsPhysicalKeyLabelsWithRuFallback|syntax:ConsoleKey.A|string:A / Ф
        Theory|QteKeyInput_FormatsPhysicalKeyLabelsWithRuFallback|syntax:ConsoleKey.S|string:S / Ы
        Theory|QteKeyInput_FormatsPhysicalKeyLabelsWithRuFallback|syntax:ConsoleKey.D|string:D / В
        Theory|QteKeyInput_FormatsPhysicalKeyLabelsWithRuFallback|syntax:ConsoleKey.Spacebar|string:Space
        Theory|QteKeyInput_MatchesConsoleFallbackInputToExpectedPhysicalKey|char:U+0439|syntax:ConsoleKey.Q
        Theory|QteKeyInput_MatchesConsoleFallbackInputToExpectedPhysicalKey|char:U+0446|syntax:ConsoleKey.W
        Theory|QteKeyInput_MatchesConsoleFallbackInputToExpectedPhysicalKey|char:U+0443|syntax:ConsoleKey.E
        Theory|QteKeyInput_MatchesConsoleFallbackInputToExpectedPhysicalKey|char:U+0444|syntax:ConsoleKey.A
        Theory|QteKeyInput_MatchesConsoleFallbackInputToExpectedPhysicalKey|char:U+044B|syntax:ConsoleKey.S
        Theory|QteKeyInput_MatchesConsoleFallbackInputToExpectedPhysicalKey|char:U+0432|syntax:ConsoleKey.D
        Theory|QteKeyInput_MatchesConsoleFallbackInputToExpectedPhysicalKey|char:U+0020|syntax:ConsoleKey.Spacebar
        Fact|QteKeyInput_LeavesUnsupportedCharactersUnmatched
        Fact|MashInputGrade_ResolvesSuccessPartialAndFailFromMatchingPressCounts
        Fact|MashInputGrade_EscapeCancelsAsFail
        Fact|MashInputGrade_CountsRuFallbackOnlyForConfiguredQteKeys
        Fact|MashInputEffectiveTarget_IsMonotonicForStatTierAndDifficulty
        Fact|PatternMemoryGrade_ResolvesSuccessPartialAndFailFromSequenceMatch
        Fact|PatternMemoryGrade_TimeoutResolvesAsFail
        Fact|PatternMemoryGrade_EscapeCancelsAsFail
        Fact|PatternMemoryGrade_UsesRuFallbackOnlyForConfiguredQteKeys
        Fact|PatternMemoryEffectiveRequirement_IsMonotonicForStatTierAndDifficulty
        Fact|PatternMemorySequenceGeneration_IsDeterministicAndUsesAlphabet
        Fact|RhythmPulseGrade_ResolvesSuccessPartialAndFailFromPulseWindows
        Fact|RhythmPulseGrade_NoMeaningfulInputResolvesAsFail
        Fact|RhythmPulseGrade_EscapeCancelsAsFail
        Fact|RhythmPulseScheduleVariation_IsDeterministicAndStrictlyIncreasing
        Fact|RhythmPulseEffectiveRequirement_IsMonotonicForStatTierAndDifficulty
        Fact|PrecisionChoiceGrade_ResolvesSuccessPartialAndFailFromSelectedChoiceIds
        Fact|PrecisionChoiceGrade_TimeoutResolvesConfiguredGradeOrDefaultFail
        Fact|PrecisionChoiceGrade_EscapeCancelsAsFail
        Fact|PrecisionChoiceGrade_UnknownChoiceResolvesAsFail
        Fact|PrecisionChoiceEffectiveRequirement_HigherStatDoesNotMakeChoiceHarder
        Fact|PrecisionChoiceEffectiveRequirement_HigherDifficultyDoesNotMakeChoiceEasier
        Fact|StealthNoiseGrade_ResolvesSuccessPartialAndFailFromNoisePressure
        Fact|StealthNoiseGrade_ExcessiveOverThresholdTimeResolvesFail
        Fact|StealthNoiseGrade_EscapeCancelsAsFail
        Fact|StealthNoiseGrade_MalformedConfigResolvesAsFail
        Fact|StealthNoiseEffectiveRequirement_HigherStatDoesNotMakeNoiseHarder
        Fact|StealthNoiseEffectiveRequirement_HigherDifficultyDoesNotMakeNoiseEasier
        Fact|LockPinSetGrade_ResolvesCleanPartialAndFailFromPinWindows
        Fact|LockPinSetGrade_BrokenPickOrExceededMistakesResolvesFail
        Fact|LockPinSetGrade_AttemptsAfterTimerDoNotOpenLock
        Fact|LockPinSetLiveAdjustment_CanReachCommittedExampleLowWindow
        Theory|LockPinSetGrade_HardDifficultyFullTimerPartialThresholdStillResolvesAllGrades|literal:4|literal:9500
        Theory|LockPinSetGrade_HardDifficultyFullTimerPartialThresholdStillResolvesAllGrades|literal:5|literal:9000
        Fact|LockPinSetGrade_EscapeCancelsAsFail
        Fact|LockPinSetGrade_MalformedConfigResolvesAsFail
        Fact|LockPinSetEffectiveRequirement_HigherStatDoesNotMakeLockHarder
        Fact|LockPinSetEffectiveRequirement_HigherDifficultyDoesNotMakeLockEasier
        """;

    internal const string QteSceneService = """
        Fact|EnsureRuntimeStateHealthyAsync_InvalidJsonFailsWithoutMutation
        Fact|EnsureRuntimeStateHealthyAsync_RemovesPendingOfferWithoutActiveScene
        Fact|EnsureRuntimeStateHealthyAsync_BrokenActiveSceneFailsWithoutMutation
        Theory|TryReadOfferAsync_InvalidTurnAuthorityIsDistinctFromMissingOffer|string(len=91,sha256=3d6f271fe3399e0a3d94edca5518760968f697eb0cfdfe8b16c6e19127b4faed)
        Theory|TryReadOfferAsync_InvalidTurnAuthorityIsDistinctFromMissingOffer|string(len=67,sha256=1298b8c3866256dd0e2fb2d9cc5dc27ab097919e68720de986a28a217d3a16fb)
        Theory|TryReadOfferAsync_InvalidTurnAuthorityIsDistinctFromMissingOffer|string:{"qteId":"qte_malformed_turn","sourceTurnNumber":
        Theory|TryReadOfferAsync_InvalidTurnAuthorityIsDistinctFromMissingOffer|string(len=3,sha256=0aad7da77d2ed59c396c99a74e49f3a4524dcdbcb5163251b1433d640247aeb4)
        Theory|TryReadOfferAsync_InvalidTurnAuthorityIsDistinctFromMissingOffer|string:null
        Theory|ResolveActiveActionAsync_InvalidPersistedTurnAuthorityFailsWithoutMutation|string(len=241,sha256=ccf8bb8b52f7aa1c1cff148ffd9c2affe0c371a4f67c21c25a8465c05d1aa6a8)
        Theory|ResolveActiveActionAsync_InvalidPersistedTurnAuthorityFailsWithoutMutation|string(len=217,sha256=39be458899e7bcc3638858023379a2ac2d245a4cc78422bf3a2053ba806d6c8d)
        Theory|ResolveActiveActionAsync_InvalidPersistedTurnAuthorityFailsWithoutMutation|string(len=69,sha256=2085dfc828d07cf1a152d9fafdd9e110f0fb93d6663000e8c9ad592b0be11602)
        Fact|BindAcceptedTurnAuthorityAsync_PersistsTrustedPositiveSourceTurn
        Theory|BindAcceptedTurnAuthorityAsync_NonPositiveTurnFailsWithoutMutation|literal:0
        Theory|BindAcceptedTurnAuthorityAsync_NonPositiveTurnFailsWithoutMutation|syntax:-1
        Theory|BeginAcceptedSceneAsync_UnboundOrMismatchedTurnFailsWithoutRuntimeMutation|null|literal:12
        Theory|BeginAcceptedSceneAsync_UnboundOrMismatchedTurnFailsWithoutRuntimeMutation|literal:0|literal:12
        Theory|BeginAcceptedSceneAsync_UnboundOrMismatchedTurnFailsWithoutRuntimeMutation|syntax:-1|literal:12
        Theory|BeginAcceptedSceneAsync_UnboundOrMismatchedTurnFailsWithoutRuntimeMutation|literal:11|literal:12
        Theory|RecordDeclineAsync_UnboundOrMismatchedTurnFailsWithoutMutation|null|literal:12
        Theory|RecordDeclineAsync_UnboundOrMismatchedTurnFailsWithoutMutation|literal:0|literal:12
        Theory|RecordDeclineAsync_UnboundOrMismatchedTurnFailsWithoutMutation|syntax:-1|literal:12
        Theory|RecordDeclineAsync_UnboundOrMismatchedTurnFailsWithoutMutation|literal:11|literal:12
        Fact|EnsureRuntimeStateHealthyAsync_NonPositiveAcceptedTurnFailsWithoutMutation
        Fact|ResolveActiveActionAsync_NonPositiveRuntimeAuthorityFailsWithoutMutation
        Fact|ApplyTerminalOutcomeStateChangesAsync_CapturesBaselineForGuardianProjectNormalization
        Fact|ApplyTerminalOutcomeStateChangesAsync_CapturesWorldEventsBaselineForRivalNormalization
        Fact|ResolveActiveActionAsync_AppliesScoreDeltasComputesRankAndWritesHistory
        Fact|StartAcceptedSceneAsync_RendersFinalScoreSummaryInConsoleAndResponse
        Fact|ResolveActiveActionAsync_LeavesUnscoredQteHistoryUnchanged
        Theory|ResolveActiveActionAsync_AppliesOnlySelectedTerminalResourceDamageThroughCanonicalQuartet|string:success|literal:5
        Theory|ResolveActiveActionAsync_AppliesOnlySelectedTerminalResourceDamageThroughCanonicalQuartet|string:fail|literal:20
        Fact|ResolveActiveActionAsync_ResurrectedRuntimeAgainstTerminalContinuationFailsWithoutDuplicateDamage
        Fact|ResolveActiveActionAsync_DirectLatePersistenceFailureRollsBackResourceQuartetAndPreservesSelectedBoundary
        Fact|ResolveActiveActionAsync_LateFailureRestoresBomAndNonTextBytesExactly
        Fact|ResolveActiveActionAsync_MissingPresentFileBackupFailsClosedAndRetainsRecoveryEvidence
        Fact|ResolveActiveActionAsync_RollbackRefreshFailureAggregatesOriginalAndRetainsBackups
        Fact|ResolveActiveActionAsync_BackupCleanupFailureDoesNotMaskCommittedResult
        Fact|ResolveActiveActionAsync_RejectsMalformedResourceOwnerAuthorityBeforeAnyOutcomeMutation
        Fact|ResolveActiveActionAsync_RawEffectWithoutImmutableContinuation_PersistsSelectedBoundaryAndPreservesFinalPublicationState
        Fact|QteTerminalResourceOutcome_WorkCountersRemainLinearForMultiCommandSelection
        Fact|ApplyTerminalOutcomeValidatedStateChangesAsync_ExistedIdNullOnlyWithoutImmutableContinuation_FailsClosedAndRollsBack
        Fact|ApplyTerminalOutcomeStateChangesAsync_CreationRefOnlyWithoutImmutableContinuation_FailsBeforeWriteLeaseOrMutation
        Fact|ApplyTerminalOutcomeStateChangesAsync_PermanentExistedIdUpdate_DoesNotRequireContinuation
        Fact|ApplyTerminalOutcomeValidatedStateChangesAsync_RawEffectWithoutImmutableContinuation_FailsClosedAndRollsBack
        Fact|ApplyTerminalOutcomeValidatedStateChangesAsync_MalformedEffectSurface_FailsClosedBeforeTypedDeserialization
        Fact|ApplyTerminalOutcomeValidatedStateChangesAsync_RawOwnerWithoutImmutableContinuation_FailsClosedAndRollsBack
        Fact|ApplyTerminalOutcomeValidatedStateChangesAsync_RestoresStateAfterValidationFailure
        Fact|ApplyTerminalOutcomeValidatedStateChangesAsync_RestoresGuardianProjectJournalAfterNormalizationFailure
        Fact|SaveGameAsync_ExcludesQteNormalizerBackupsFromArchive
        Fact|ApplyTerminalOutcomeStateChangesAsync_RemovesQteBackupRootAfterSuccessfulRun
        Fact|ApplyTerminalOutcomeValidatedStateChangesAsync_PreservesSiblingBackupRunDirectory
        """;

}
