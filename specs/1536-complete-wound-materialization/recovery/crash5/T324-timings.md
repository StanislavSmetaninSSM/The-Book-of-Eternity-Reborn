# Именованные замеры восстановления — T324

Один фактический запуск: `20261021-004553-398-7796-8a8ce06528814c24a6d89b16302e85c6-categories`. Результат случая: **Failed**; тест 00:12:33.2382874, весь запуск 00:12:53.0330783.

Исходники: `d41f80f347a4764164de866a2c5ae7a100f373cc`; сохранённое состояние запуска: `fd5d599796c857fd9a71208ab15008ae3334f184`. PID восстановления: 24512. Получено 3197 записей без квоты диагностического вывода. Последняя запись по часам коллектора: 598.068333 с.

## Завершённые интервалы

Значения включают вложенные вызовы. Строки нельзя складывать как независимые части общего времени. Конец интервала также записывается при выходе через исключение и сам по себе не означает успешную операцию.

| Участок кода | Завершено вызовов | Сумма, с | Среднее, с | Максимум, с |
| --- | ---: | ---: | ---: | ---: |
| `GameEngine.PublishCurrentMortalJointRepairAsync` | 1 | 316.912996 | 316.912996 | 316.912996 |
| `JointCompletion.ValidatePostPublicationAsync` | 2 | 168.501024 | 84.250512 | 90.078835 |
| `JointResources.ValidateCurrentAsync` | 13 | 149.661807 | 11.512447 | 12.074678 |
| `JointResources.RecomputeQteAsync` | 13 | 145.825466 | 11.217344 | 11.783936 |
| `JointBaseline.ValidateCurrentAsync` | 31 | 138.292832 | 4.461059 | 4.892346 |
| `GameEngine.CreateJointRepairCommonPlanAsync` | 1 | 131.005028 | 131.005028 | 131.005028 |
| `JointCommonAuthority.ValidateCurrentAsync` | 10 | 127.906536 | 12.790654 | 13.348028 |
| `JointStory.ValidateAcceptedTurnOutcomeWithRepairLoopAsync` | 1 | 101.811239 | 101.811239 | 101.811239 |
| `JointCompletion.TryReadCompletedContinuationAsync` | 1 | 98.394352 | 98.394352 | 98.394352 |
| `JointMechanical.ValidateCurrentAsync` | 35 | 93.233733 | 2.663821 | 3.033911 |
| `JointPhase.ReadCompletedProofAsync` | 34 | 89.329293 | 2.627332 | 2.837734 |
| `JointBaseline.ValidateMechanicalAsync` | 31 | 82.798400 | 2.670916 | 3.034092 |
| `JointCompletion.ValidateGameStateAsync` | 2 | 65.326666 | 32.663333 | 32.791577 |
| `GameState.ValidateCoreAsync` | 2 | 65.323946 | 32.661973 | 32.788879 |
| `GameEngine.PrevalidateJointRepairPublicationTransactionAsync` | 1 | 63.565552 | 63.565552 | 63.565552 |
| `JointCompletion.FinalProgressionLeaseAndProjection` | 2 | 62.613403 | 31.306702 | 33.384922 |
| `Continuation.ValidateCurrentAsync` | 41 | 61.086340 | 1.489911 | 1.722523 |
| `JointCommonAuthority.ValidateCandidateBoundsAsync` | 2 | 51.399963 | 25.699982 | 26.425558 |
| `GameEngine.NormalizeAndValidateJointRepairAsync` | 1 | 49.884739 | 49.884739 | 49.884739 |
| `JointPublish.NormalizeAndValidateAsync` | 1 | 49.880066 | 49.880066 | 49.880066 |
| `JointMechanical.RequireCurrentOriginalEvidence` | 35 | 45.970265 | 1.313436 | 1.504138 |
| `JointMechanical.CompareBeforeImages` | 35 | 38.479151 | 1.099404 | 1.276241 |
| `JointStory.AppendStoryAsync` | 1 | 37.120776 | 37.120776 | 37.120776 |
| `Continuation.CompareBeforeImages` | 41 | 33.432830 | 0.815435 | 0.965688 |
| `Checkpoint.CaptureCoreAsync` | 20 | 29.708897 | 1.485445 | 2.442523 |
| `JointCommon.ComposeInputAsync` | 1 | 28.719034 | 28.719034 | 28.719034 |
| `JointInput.ComposeAsync` | 1 | 28.716253 | 28.716253 | 28.716253 |
| `JointStorySource.BindContinuationAsync` | 5 | 27.726720 | 5.545344 | 5.797468 |
| `JointCompletion.CommitProgressionPhaseAsync` | 2 | 27.654125 | 13.827063 | 15.725022 |
| `JointCalculation.EnsureAsync` | 1 | 24.545801 | 24.545801 | 24.545801 |
| `JointStoryComplete.ReadInitialSourceAsync` | 2 | 24.279354 | 12.139677 | 12.264894 |
| `JointCompletion.WriteOrVerifyProjectionAsync` | 2 | 22.017057 | 11.008529 | 11.233591 |
| `GameEngine.CreateJointRepairResourcePublicationAsync` | 1 | 20.798527 | 20.798527 | 20.798527 |
| `JointPublish.PublishAcceptedMechanicsAsync` | 1 | 19.759265 | 19.759265 | 19.759265 |
| `MechanicsPublish.PublishAsync` | 1 | 19.747393 | 19.747393 | 19.747393 |
| `Baselines.ReadPublicationBaselines` | 42 | 17.221354 | 0.410032 | 0.480328 |
| `Continuation.ReadPublicationBaselines` | 41 | 16.786717 | 0.409432 | 0.480367 |
| `JointPublish.TakeAsync` | 1 | 16.726202 | 16.726202 | 16.726202 |
| `GameEngine.CreateJointRepairCommonBaselineAsync` | 1 | 16.613687 | 16.613687 | 16.613687 |
| `JointStorySource.ValidateAppliedAfterAsync` | 5 | 15.890509 | 3.178102 | 3.315938 |
| `GameState.MetaMiscStateFiles` | 2 | 15.723182 | 7.861591 | 7.995647 |
| `JointStorySource.ValidateAppliedBeforeAsync` | 5 | 15.707232 | 3.141446 | 3.273308 |
| `JointCommon.CreateRegistryCandidate` | 1 | 13.765941 | 13.765941 | 13.765941 |
| `JointPhase.ValidateProgressionDataAsync` | 34 | 13.702640 | 0.403019 | 0.471431 |
| `Correction.RevalidateCorrectionAsync` | 23 | 13.597882 | 0.591212 | 0.799770 |
| `MechanicsPublish.ValidateTakenJointInputsAsync` | 1 | 12.780987 | 12.780987 | 12.780987 |
| `JointPublish.ValidateTakenInputsAsync` | 1 | 12.777303 | 12.777303 | 12.777303 |
| `JointStoryComplete.ReadBackSourceAsync` | 1 | 12.605644 | 12.605644 | 12.605644 |
| `JointStory.ValidateStoryProofAsync` | 1 | 12.540799 | 12.540799 | 12.540799 |
| `JointStoryComplete.ReadBeforeAppendSourceAsync` | 1 | 12.335922 | 12.335922 | 12.335922 |
| `Correction.ComposeCorrectedQteResponse` | 23 | 11.780715 | 0.512205 | 0.684772 |
| `GameState.ClientOwnedControlFiles` | 2 | 11.698124 | 5.849062 | 6.049524 |
| `JointStory.VerifyProjectionBeforeStoryAsync` | 1 | 10.822766 | 10.822766 | 10.822766 |
| `GameEngine.CreateMechanicalAdmissionAsync` | 1 | 10.667283 | 10.667283 | 10.667283 |
| `Continuation.ReadSnapshot` | 41 | 9.959624 | 0.242918 | 0.290243 |
| `Checkpoint.ComposeRejectedQteResponse` | 20 | 9.852614 | 0.492631 | 0.600052 |
| `JointStory.InitialInspectionLease` | 2 | 9.828629 | 4.914315 | 9.140954 |
| `GameState.CrossReferences` | 2 | 8.937050 | 4.468525 | 4.473663 |
| `JointCommonAuthority.ValidateSeal` | 10 | 8.792958 | 0.879296 | 0.994431 |
| `JointMechanical.ReadAndCheckSnapshot` | 35 | 8.418321 | 0.240523 | 0.292564 |
| `Checkpoint.ExportCurrentTreatmentAuthority` | 20 | 8.364478 | 0.418224 | 0.946192 |
| `JointInventory.ValidateCurrentAsync` | 3 | 8.219903 | 2.739968 | 2.759204 |
| `GameState.RealmSegregation` | 2 | 7.600394 | 3.800197 | 3.912913 |
| `JointBaseline.ValidateSeal` | 31 | 7.580092 | 0.244519 | 0.305875 |
| `JointCompletion.ReadObservedProgressionAsync` | 2 | 6.622419 | 3.311209 | 3.376494 |
| `JointCompletion.ReadCurrentProgressionAsync` | 2 | 6.316828 | 3.158414 | 3.189537 |
| `JointPublish.ValidateTransactionReadbackAsync` | 1 | 6.308753 | 6.308753 | 6.308753 |
| `JointCompletion.ValidateAppliedProgressionAsync` | 2 | 6.297231 | 3.148616 | 3.161198 |
| `JointCommon.SealRegistryCandidate` | 1 | 5.898368 | 5.898368 | 5.898368 |
| `JointPublish.ValidateReceiptLineage` | 1 | 5.879286 | 5.879286 | 5.879286 |
| `JointCompletion.ValidateCurrentCompletedProofAsync` | 2 | 5.505099 | 2.752550 | 2.798892 |
| `Checkpoint.ReadSnapshot` | 20 | 5.458915 | 0.272946 | 0.323420 |
| `JointProgression.ValidateCurrentSourceAsync` | 13 | 5.402341 | 0.415565 | 0.476365 |
| `JointCompletion.ReadInterfaceAtLateSlotAsync` | 2 | 5.340167 | 2.670083 | 2.812498 |
| `JointCompletion.ValidateCurrentJointCompletionAsync` | 2 | 5.159119 | 2.579560 | 2.616669 |
| `GameEngine.CreateJointRepairPreparationAsync` | 1 | 4.846645 | 4.846645 | 4.846645 |
| `Preparation.CreateJointRepairPreparationAsync` | 1 | 4.840611 | 4.840611 | 4.840611 |
| `JointPublish.CandidateAdmissionGate` | 1 | 4.138275 | 4.138275 | 4.138275 |
| `JointCommon.CandidateAdmissionGate` | 1 | 3.993581 | 3.993581 | 3.993581 |
| `JointInventory.ValidateOwnerAndSeal` | 3 | 3.945686 | 1.315229 | 1.348229 |
| `GameState.WorldQuestCombatFactionStateFiles` | 2 | 3.889772 | 1.944886 | 1.954991 |
| `MechanicsPublish.CreateQtePublicationAsync` | 1 | 3.880006 | 3.880006 | 3.880006 |
| `JointResources.ValidateSeal` | 13 | 3.832530 | 0.294810 | 0.356289 |
| `JointStory.InspectCurrentAsync` | 2 | 3.522294 | 1.761147 | 2.994576 |
| `JointInventory.CompareBeforeImages` | 3 | 3.485336 | 1.161779 | 1.168456 |
| `JointProgression.ReadCurrentSnapshot` | 13 | 3.240514 | 0.249270 | 0.289747 |
| `JointStory.ReadOriginalProgressionPhaseAsync` | 1 | 3.160595 | 3.160595 | 3.160595 |
| `JointStory.ReadAppliedProgressionPhaseAsync` | 1 | 2.902557 | 2.902557 | 2.902557 |
| `GameEngine.InspectOrdinaryMortalJointRepairAsync` | 1 | 2.819632 | 2.819632 | 2.819632 |
| `JointStory.AcceptedOutcomeCheckpoint` | 1 | 2.782288 | 2.782288 | 2.782288 |
| `JointPublish.ValidateTakenInventoryAsync` | 1 | 2.759615 | 2.759615 | 2.759615 |
| `GameState.NpcStateFiles` | 2 | 2.703110 | 1.351555 | 1.361858 |
| `JointPublish.CreateTransaction` | 1 | 2.641099 | 2.641099 | 2.641099 |
| `GameState.AcceptedTurnActorMaterializationCompleteness` | 2 | 2.627717 | 1.313859 | 1.314058 |
| `JointPublish.CompleteRegistry` | 1 | 2.548396 | 2.548396 | 2.548396 |
| `GameEngine.ReadJointCompletedWitnessAsync` | 1 | 2.526210 | 2.526210 | 2.526210 |
| `GameState.PlayerStateFiles` | 2 | 2.466242 | 1.233121 | 1.333275 |
| `JointCompletion.ValidateReasoningAsync` | 2 | 2.432069 | 1.216035 | 1.264025 |
| `Checkpoint.CreateGuaranteedAttempt` | 20 | 2.271329 | 0.113566 | 0.155962 |
| `JointCommon.BindInput` | 1 | 2.207893 | 2.207893 | 2.207893 |
| `JointCommon.CreateAuthority` | 1 | 2.195823 | 2.195823 | 2.195823 |
| `GameEngine.GetOrBuildEffectValidatedAsync` | 1 | 2.073183 | 2.073183 | 2.073183 |
| `GameEngine.GetOrBuildFinalValidatedAsync` | 1 | 2.002514 | 2.002514 | 2.002514 |
| `GameState.RequiredFields` | 2 | 1.801646 | 0.900823 | 0.950716 |
| `GameEngine.CreateJointRepairAnchors` | 1 | 1.715083 | 1.715083 | 1.715083 |
| `GameState.AcceptedTurnEffectMaterializationCompleteness` | 2 | 1.629611 | 0.814805 | 0.864635 |
| `JointProgression.ValidateFrozenInputsAsync` | 13 | 1.602591 | 0.123276 | 0.162328 |
| `GameEngine.GetOrBuildPreparedValidatedAsync` | 1 | 1.597932 | 1.597932 | 1.597932 |
| `GameEngine.ReserveMortalJointRepairPublicationAsync` | 1 | 1.522534 | 1.522534 | 1.522534 |
| `Preparation.ReconstructAsync` | 1 | 1.437655 | 1.437655 | 1.437655 |
| `GameState.AcceptedTurnItemMaterializationCompleteness` | 2 | 1.426783 | 0.713391 | 0.723086 |
| `GameEngine.ReadCurrentJointRejectedCaptureAsync` | 1 | 1.369304 | 1.369304 | 1.369304 |
| `Preparation.ValidateContinuation` | 1 | 1.332664 | 1.332664 | 1.332664 |
| `JointCalculation.CalculateArtifacts` | 1 | 1.291105 | 1.291105 | 1.291105 |
| `JointStorySource.ReadOriginal` | 5 | 1.210100 | 0.242020 | 0.281715 |
| `JointBaseline.ConfirmItemBaseline` | 31 | 1.183800 | 0.038187 | 0.058836 |
| `GameEngine.CaptureMortalJointProgressionSourceAsync` | 1 | 1.167681 | 1.167681 | 1.167681 |
| `JointPhase.ResolveRecovery` | 35 | 1.059133 | 0.030261 | 0.049080 |
| `GameState.AcceptedTurnLocationMaterializationCompleteness` | 2 | 0.947277 | 0.473638 | 0.498588 |
| `Checkpoint.RestorePersistedTreatmentRequests` | 20 | 0.921714 | 0.046086 | 0.072585 |
| `Preparation.CaptureBeforeImages` | 1 | 0.897551 | 0.897551 | 0.897551 |
| `JointPhase.VerifyTupleAndNoContinuationAsync` | 34 | 0.885589 | 0.026047 | 0.045282 |
| `JointInventory.ReadAndCheckSnapshot` | 3 | 0.788458 | 0.262819 | 0.270207 |
| `JointCompletion.ValidatePostPublicationAuthorityAsync` | 1 | 0.784428 | 0.784428 | 0.784428 |
| `GameState.TrainingShowcases` | 2 | 0.738149 | 0.369075 | 0.384562 |
| `GameEngine.Intake.ValidateCurrentAsync` | 1 | 0.713095 | 0.713095 | 0.713095 |
| `GameState.AcceptedTurnFactionMaterializationCompleteness` | 2 | 0.703198 | 0.351599 | 0.358708 |
| `JointStory.TryReconcileMortalJointStoryTurnAsync` | 1 | 0.688120 | 0.688120 | 0.688120 |
| `JointCompletion.BindPublishedOutputAsync` | 1 | 0.663894 | 0.663894 | 0.663894 |
| `JointPhase.RecheckMarkerAsync` | 34 | 0.638028 | 0.018766 | 0.030466 |
| `JointCalculation.CaptureInputsAsync` | 1 | 0.620867 | 0.620867 | 0.620867 |
| `JointInput.CaptureCalculationInputs` | 1 | 0.617330 | 0.617330 | 0.617330 |
| `Preparation.RevalidateCorrectionAsync` | 1 | 0.609499 | 0.609499 | 0.609499 |
| `JointPhase.ReadMarkerAsync` | 35 | 0.591817 | 0.016909 | 0.027334 |
| `MechanicsPublish.PublishLocalBatchAsync` | 1 | 0.588769 | 0.588769 | 0.588769 |
| `MechanicsPublish.ValidateAgreementAsync` | 1 | 0.584407 | 0.584407 | 0.584407 |
| `MechanicsPublish.ValidatePublishedWoundsAsync` | 1 | 0.528797 | 0.528797 | 0.528797 |
| `JointCompletion.ValidateNarrativeReadOnlyAsync` | 2 | 0.511282 | 0.255641 | 0.256671 |
| `JointPublish.ValidateCanonicalItemAsync` | 1 | 0.482349 | 0.482349 | 0.482349 |
| `GameEngine.CaptureCurrentHotCustodyAsync` | 1 | 0.476891 | 0.476891 | 0.476891 |
| `GameState.SkillContractConsistency` | 2 | 0.459932 | 0.229966 | 0.254775 |
| `JointProgression.CompareOriginalGuards` | 13 | 0.441644 | 0.033973 | 0.051032 |
| `Preparation.ReadPublicationBaselines` | 1 | 0.437080 | 0.437080 | 0.437080 |
| `JointPublish.ValidateOutputAuthorityAsync` | 1 | 0.422829 | 0.422829 | 0.422829 |
| `Correction.ReadNarrative` | 23 | 0.400935 | 0.017432 | 0.028617 |
| `MechanicsPublish.CaptureAgreementBeforeImagesAsync` | 1 | 0.398047 | 0.398047 | 0.398047 |
| `Checkpoint.ReadAuthorityBytes` | 20 | 0.395254 | 0.019763 | 0.028311 |
| `Correction.ReadDraft` | 23 | 0.393670 | 0.017116 | 0.026490 |
| `Correction.ReadPending` | 23 | 0.384680 | 0.016725 | 0.026058 |
| `GameState.AcceptedTurnWoundMaterializationCompleteness` | 2 | 0.366917 | 0.183459 | 0.208312 |
| `Correction.ReadCommand` | 23 | 0.362468 | 0.015759 | 0.023806 |
| `JointPublish.ValidateCanonicalEffectAsync` | 1 | 0.361249 | 0.361249 | 0.361249 |
| `JointMechanical.ValidateOwnerAndFingerprint` | 35 | 0.360659 | 0.010305 | 0.021372 |
| `MechanicsPublish.BuildWrites` | 1 | 0.348057 | 0.348057 | 0.348057 |
| `Continuation.InspectConfirmedHeldClaim` | 41 | 0.344739 | 0.008408 | 0.017015 |
| `JointPublish.ValidateCanonicalResourceAsync` | 1 | 0.337158 | 0.337158 | 0.337158 |
| `Checkpoint.ReadCommand` | 20 | 0.311093 | 0.015555 | 0.026099 |
| `Checkpoint.ReadManifestBytes` | 20 | 0.306940 | 0.015347 | 0.017846 |
| `JointStorySource.RecheckSourceImagesAsync` | 5 | 0.306120 | 0.061224 | 0.078874 |
| `MechanicsPublish.PrepareBeforeImages` | 1 | 0.284359 | 0.284359 | 0.284359 |
| `GameState.NoLifeEvaluationRewardsOnTriggerTurn` | 2 | 0.258733 | 0.129366 | 0.143810 |
| `JointStorySource.VerifyHistoryInventory` | 5 | 0.253215 | 0.050643 | 0.060036 |
| `JointPhase.ReadDecisionAsync` | 34 | 0.245998 | 0.007235 | 0.016768 |
| `JointInput.CaptureReadSnapshot` | 1 | 0.226317 | 0.226317 | 0.226317 |
| `MechanicsPublish.ReadBackBatchAsync` | 1 | 0.223825 | 0.223825 | 0.223825 |
| `JointStorySource.ReadSourceObjectsAsync` | 5 | 0.212296 | 0.042459 | 0.054679 |
| `GameState.MortalBootstrapContentAnchors` | 2 | 0.210427 | 0.105213 | 0.109606 |
| `JointInput.ComposeReadSnapshot` | 1 | 0.210408 | 0.210408 | 0.210408 |
| `GameState.LoreBootstrapRequiredFiles` | 2 | 0.203192 | 0.101596 | 0.122971 |
| `GameState.AfterlifeSpiritualConflictState` | 2 | 0.198995 | 0.099498 | 0.102030 |
| `GameState.SourceOfLightCapstoneGlobalState` | 2 | 0.197080 | 0.098540 | 0.105644 |
| `JointCompletion.ValidateQteOfferAsync` | 2 | 0.189450 | 0.094725 | 0.099842 |
| `GameState.GuardianResonancePowerEvents` | 2 | 0.173519 | 0.086760 | 0.093536 |
| `JointStoryComplete.AppendAtomicAsync` | 1 | 0.164225 | 0.164225 | 0.164225 |
| `GameState.ShiningLeadershipHeadReferences` | 2 | 0.161634 | 0.080817 | 0.088043 |
| `JointPublish.ValidateCanonicalWoundAsync` | 1 | 0.158297 | 0.158297 | 0.158297 |
| `JointPublish.ValidateCanonicalLocationAsync` | 1 | 0.133610 | 0.133610 | 0.133610 |
| `JointCompletion.ValidateProgressionScheduleAsync` | 1 | 0.133567 | 0.133567 | 0.133567 |
| `JointProgression.ValidateOwnerAndSeal` | 13 | 0.115426 | 0.008879 | 0.010746 |
| `JointStorySource.VerifyOtherHistoriesAsync` | 5 | 0.104817 | 0.020963 | 0.025400 |
| `JointPhase.VerifyLease` | 35 | 0.094787 | 0.002708 | 0.005417 |
| `JointStorySource.ReadReferencesAsync` | 5 | 0.093529 | 0.018706 | 0.026740 |
| `JointStorySource.ClassifyHistoryAsync` | 5 | 0.083125 | 0.016625 | 0.017648 |
| `GameState.JsonIntegrity` | 2 | 0.081406 | 0.040703 | 0.041602 |
| `JointStorySource.ReadManifestAsync` | 5 | 0.080265 | 0.016053 | 0.018056 |
| `Checkpoint.ReadSessionGeneration` | 20 | 0.072671 | 0.003634 | 0.004044 |
| `MechanicsPublish.ValidatePublishedResourcesAsync` | 1 | 0.066687 | 0.066687 | 0.066687 |
| `JointCompletion.ValidateLevelUpMaterializationAsync` | 2 | 0.062429 | 0.031215 | 0.033055 |
| `JointCompletion.ValidateSpecialActionOutcomesAsync` | 2 | 0.059252 | 0.029626 | 0.041035 |
| `JointCompletion.ValidateCombatMaterializationAsync` | 2 | 0.059173 | 0.029586 | 0.031685 |
| `GameState.MortalBootstrapPlayerVisibleNames` | 2 | 0.058572 | 0.029286 | 0.031764 |
| `JointStorySource.VerifyCurrentSessionOperation` | 5 | 0.054474 | 0.010895 | 0.017941 |
| `MechanicsPublish.ValidateAuthorityBeforeImagesAsync` | 1 | 0.043808 | 0.043808 | 0.043808 |
| `Preparation.PrepareTreatmentOutcome` | 1 | 0.042096 | 0.042096 | 0.042096 |
| `Checkpoint.FinalizeResource` | 20 | 0.038764 | 0.001938 | 0.007010 |
| `JointCompletion.ValidateMemoryLegacyAsync` | 2 | 0.029963 | 0.014981 | 0.015199 |
| `GameState.RequiredFiles` | 2 | 0.028129 | 0.014064 | 0.016327 |
| `JointStorySource.CalculateRow` | 5 | 0.027618 | 0.005524 | 0.010409 |
| `Preparation.ValidateInput` | 1 | 0.027227 | 0.027227 | 0.027227 |
| `JointStory.ReadCompletionState` | 2 | 0.019904 | 0.009952 | 0.013748 |
| `GameState.SoulStateConsistency` | 2 | 0.018439 | 0.009219 | 0.011101 |
| `Preparation.ValidateTreatmentPublicationShell` | 1 | 0.015950 | 0.015950 | 0.015950 |
| `MechanicsPublish.ReadCompletionBeforeImageAsync` | 1 | 0.014909 | 0.014909 | 0.014909 |
| `JointCommonAuthority.ProjectCandidateBounds` | 2 | 0.009711 | 0.004856 | 0.005696 |
| `Preparation.ConstructContinuationAuthority` | 1 | 0.008145 | 0.008145 | 0.008145 |
| `Preparation.ReadPublishedInterface` | 1 | 0.002468 | 0.002468 | 0.002468 |
| `MechanicsPublish.ValidateItemFinalBaselineAsync` | 1 | 0.002124 | 0.002124 | 0.002124 |
| `MechanicsPublish.ValidatePublishedOwnerTransitionsAsync` | 1 | 0.001705 | 0.001705 | 0.001705 |
| `GameState.LifeEvaluationRewardCycle` | 2 | 0.001541 | 0.000770 | 0.001495 |
| `Preparation.PrepareSeverityRematerialization` | 1 | 0.001414 | 0.001414 | 0.001414 |
| `GameState.AfterlifeActiveThreatPreTurnContinuity` | 2 | 0.001137 | 0.000569 | 0.001121 |
| `GameState.AfterlifeGlobalFlagPreTurnContinuity` | 2 | 0.001101 | 0.000551 | 0.001087 |
| `GameState.ShiningTreasuryClientOwnedState` | 2 | 0.001010 | 0.000505 | 0.000957 |
| `JointResources.CompareCompanionProjection` | 13 | 0.000747 | 0.000057 | 0.000416 |
| `JointCalculation.RetainArtifacts` | 1 | 0.000186 | 0.000186 | 0.000186 |

## Начатые интервалы без записи конца

Внешний → внутренний. Длительность завершения не измерена; родительский лимит 600 с не приписан отдельному методу.

| Участок кода | Номер начала | Начало по часам коллектора, с | Наблюдаемое окно до последней обычной записи, с |
| --- | ---: | ---: | ---: |
| `Resumer.FirstCurrentOwnerCall` | 1 | 6.755141 | 591.313192 |
| `JointStory.TryReconcileMortalJointStoryTurnAsync` | 2542 | 420.725137 | 177.343196 |
| `JointStory.FinalMaterializationLeaseAndCleanup` | 2877 | 534.459941 | 63.608392 |
| `JointStory.ValidateAppliedProgressionAsync` | 3190 | 598.010414 | 0.057919 |
| `JointPhase.ReadCompletedProofAsync` | 3197 | 598.068333 | 0.000000 |

Наблюдаемое окно — разность двух отметок вывода одних часов, с накладными расходами наблюдателя; это не завершённая длительность метода.

Самый внутренний наблюдавшийся незавершённый участок: **`JointPhase.ReadCompletedProofAsync`**.

## Проверка и границы вывода

Идентификатор процесса, последовательность записей и вложенность проверены по сырому stdout фактического дочернего восстановления. Извлечённые потоки побайтно сверены с TRX внутри архива; CRC, уникальность имён и SHA-256 каждого файла архива проверены. 4 DLL сверены родительским отчётом до и после запуска; файлы исходного снимка повторно сверены в сохранённом генераторском архиве.

Сырой архив: [T323-profile-cut0-raw.zip](T323-profile-cut0-raw.zip); SHA-256 `D73732539557BE3877165669C86EFA08AD805C0C7787FC4F5167870B6DB2E184`. Полные интервалы, ссылки на строки исходников и индекс архива: [T324-timings.json](T324-timings.json).

EOF/join обоих процессов: True; ошибок завершения контейнеров: 0; отчётов с игровым результатом: 0.

Отдельный существующий канал стадий зафиксировал `resume-activity/enter` (`None`) на 591.699152 с своих часов. Это отдельное наблюдение контрольной точки; его часы имеют другое начало отсчёта. Оно не является игровым PASS.

- Удаление собственного тестового корня: True; отсутствие подтверждено: True.
- Удаление собственного тестового корня: True; отсутствие подтверждено: True.

Это замер одного запуска с наблюдателями. Он показывает стоимость участков, но не разделяет работу CPU, файловой системы и внешних проверок. Повторное чтение при первоначальной выдаче story-proof заменено полным сравнением с проверенным readback; последующие проверки остаются свежими. Лимиты не увеличивались. Исходные пять Windows-случаев: 0/5 PASS; Linux локально не запускался.
