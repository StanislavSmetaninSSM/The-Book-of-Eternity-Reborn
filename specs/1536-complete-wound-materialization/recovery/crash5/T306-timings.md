# Именованные замеры восстановления — T306

Один фактический запуск: `20261020-191200-379-35532-4fa790a0ef824c8a9859ef4d0c639561-categories`. Результат случая: **Failed**; тест 00:12:28.4733602, весь запуск 00:12:48.6517671.

Исходники: `8499922ae9f8a231545b3299137470e4d495ed27`; сохранённое состояние запуска: `55f756722af5249fa7754b3b4306a854ae64be9e`. PID восстановления: 27992. Получено 2846 записей без квоты диагностического вывода. Последняя запись по часам коллектора: 599.787871 с.

## Завершённые интервалы

Значения включают вложенные вызовы. Строки нельзя складывать как независимые части общего времени. Конец интервала также записывается при выходе через исключение и сам по себе не означает успешную операцию.

| Участок кода | Завершено вызовов | Сумма, с | Среднее, с | Максимум, с |
| --- | ---: | ---: | ---: | ---: |
| `GameEngine.PublishCurrentMortalJointRepairAsync` | 1 | 420.684341 | 420.684341 | 420.684341 |
| `JointResources.ValidateCurrentAsync` | 13 | 224.221301 | 17.247792 | 23.388386 |
| `JointBaseline.ValidateCurrentAsync` | 44 | 209.138929 | 4.753157 | 7.259270 |
| `JointCommonAuthority.ValidateCurrentAsync` | 10 | 190.090578 | 19.009058 | 25.498163 |
| `GameEngine.CreateJointRepairCommonPlanAsync` | 1 | 177.247348 | 177.247348 | 177.247348 |
| `JointResources.RecomputeQteAsync` | 13 | 158.575354 | 12.198104 | 17.484297 |
| `JointMechanical.ValidateCurrentAsync` | 48 | 138.232915 | 2.879852 | 4.332486 |
| `JointBaseline.ValidateMechanicalAsync` | 44 | 128.395080 | 2.918070 | 4.332638 |
| `JointCompletion.ValidatePostPublicationAsync` | 1 | 91.270708 | 91.270708 | 91.270708 |
| `GameEngine.PrevalidateJointRepairPublicationTransactionAsync` | 1 | 90.227317 | 90.227317 | 90.227317 |
| `GameEngine.NormalizeAndValidateJointRepairAsync` | 1 | 80.699291 | 80.699291 | 80.699291 |
| `JointPublish.NormalizeAndValidateAsync` | 1 | 80.691922 | 80.691922 | 80.691922 |
| `Continuation.ValidateCurrentAsync` | 54 | 78.510149 | 1.453892 | 2.747681 |
| `JointMechanical.RequireCurrentOriginalEvidence` | 48 | 75.201421 | 1.566696 | 2.580831 |
| `JointCommonAuthority.ValidateCandidateBoundsAsync` | 2 | 70.291664 | 35.145832 | 37.279310 |
| `JointMechanical.CompareBeforeImages` | 48 | 48.512210 | 1.010671 | 1.570274 |
| `JointCompletion.FinalProgressionLeaseAndProjection` | 1 | 44.725981 | 44.725981 | 44.725981 |
| `JointCommon.ComposeInputAsync` | 1 | 40.844654 | 40.844654 | 40.844654 |
| `JointInput.ComposeAsync` | 1 | 40.841654 | 40.841654 | 40.841654 |
| `Continuation.CompareBeforeImages` | 54 | 38.900770 | 0.720385 | 1.272372 |
| `JointPublish.PublishAcceptedMechanicsAsync` | 1 | 35.371921 | 35.371921 | 35.371921 |
| `MechanicsPublish.PublishAsync` | 1 | 35.364276 | 35.364276 | 35.364276 |
| `JointCompletion.ValidateGameStateAsync` | 1 | 34.602642 | 34.602642 | 34.602642 |
| `GameState.ValidateCoreAsync` | 1 | 34.599735 | 34.599735 | 34.599735 |
| `JointCalculation.EnsureAsync` | 1 | 34.343645 | 34.343645 | 34.343645 |
| `Checkpoint.CaptureCoreAsync` | 20 | 33.878003 | 1.693900 | 2.944317 |
| `JointPublish.TakeAsync` | 1 | 32.562869 | 32.562869 | 32.562869 |
| `MechanicsPublish.ValidateTakenJointInputsAsync` | 1 | 28.377103 | 28.377103 | 28.377103 |
| `JointPublish.ValidateTakenInputsAsync` | 1 | 28.375551 | 28.375551 | 28.375551 |
| `Baselines.ReadPublicationBaselines` | 55 | 23.033796 | 0.418796 | 1.042066 |
| `GameEngine.CreateJointRepairResourcePublicationAsync` | 1 | 22.680546 | 22.680546 | 22.680546 |
| `Continuation.ReadPublicationBaselines` | 54 | 22.621363 | 0.418914 | 1.042120 |
| `GameState.MetaMiscStateFiles` | 2 | 19.409678 | 9.704839 | 9.734304 |
| `JointCompletion.WriteOrVerifyProjectionAsync` | 1 | 18.539071 | 18.539071 | 18.539071 |
| `JointCompletion.CommitProgressionPhaseAsync` | 1 | 17.115345 | 17.115345 | 17.115345 |
| `JointCommon.CreateRegistryCandidate` | 1 | 15.995228 | 15.995228 | 15.995228 |
| `GameEngine.CreateJointRepairCommonBaselineAsync` | 1 | 15.909200 | 15.909200 | 15.909200 |
| `Continuation.ReadSnapshot` | 54 | 15.706088 | 0.290853 | 0.499970 |
| `JointPublish.ValidateReceiptLineage` | 1 | 15.357346 | 15.357346 | 15.357346 |
| `Correction.RevalidateCorrectionAsync` | 23 | 14.935700 | 0.649378 | 1.025018 |
| `JointMechanical.ReadAndCheckSnapshot` | 48 | 13.973583 | 0.291116 | 0.478351 |
| `JointInventory.ValidateCurrentAsync` | 3 | 13.420574 | 4.473525 | 5.359399 |
| `Correction.ComposeCorrectedQteResponse` | 23 | 13.205833 | 0.574167 | 0.898588 |
| `JointBaseline.ValidateSeal` | 44 | 13.022974 | 0.295977 | 0.454829 |
| `Checkpoint.ComposeRejectedQteResponse` | 20 | 12.198068 | 0.609903 | 1.087385 |
| `JointStory.InitialInspectionLease` | 2 | 11.498496 | 5.749248 | 10.827195 |
| `JointCommonAuthority.ValidateSeal` | 10 | 11.248266 | 1.124827 | 1.779515 |
| `GameEngine.CreateMechanicalAdmissionAsync` | 1 | 10.772371 | 10.772371 | 10.772371 |
| `Checkpoint.ExportCurrentTreatmentAuthority` | 20 | 9.256874 | 0.462844 | 1.194851 |
| `JointCommon.SealRegistryCandidate` | 1 | 7.954809 | 7.954809 | 7.954809 |
| `GameState.CrossReferences` | 2 | 7.859478 | 3.929739 | 3.979479 |
| `JointPublish.CandidateAdmissionGate` | 1 | 7.859172 | 7.859172 | 7.859172 |
| `JointInventory.ValidateOwnerAndSeal` | 3 | 7.136480 | 2.378827 | 3.090785 |
| `GameState.ClientOwnedControlFiles` | 1 | 6.954355 | 6.954355 | 6.954355 |
| `Checkpoint.ReadSnapshot` | 20 | 5.924414 | 0.296221 | 0.431786 |
| `JointCompletion.ReadCurrentProgressionAsync` | 1 | 5.771695 | 5.771695 | 5.771695 |
| `JointProgression.ValidateCurrentSourceAsync` | 13 | 5.710933 | 0.439303 | 0.683704 |
| `JointPublish.ValidateTransactionReadbackAsync` | 1 | 5.527471 | 5.527471 | 5.527471 |
| `JointStory.ReadOriginalProgressionPhaseAsync` | 1 | 5.495359 | 5.495359 | 5.495359 |
| `JointPublish.ValidateTakenInventoryAsync` | 1 | 5.158906 | 5.158906 | 5.158906 |
| `GameEngine.CreateJointRepairPreparationAsync` | 1 | 4.977820 | 4.977820 | 4.977820 |
| `Preparation.CreateJointRepairPreparationAsync` | 1 | 4.972426 | 4.972426 | 4.972426 |
| `JointResources.ValidateSeal` | 13 | 4.952850 | 0.380988 | 0.570740 |
| `JointInventory.CompareBeforeImages` | 3 | 4.811435 | 1.603812 | 2.017895 |
| `JointCommon.CandidateAdmissionGate` | 1 | 4.773435 | 4.773435 | 4.773435 |
| `JointCompletion.ReadObservedProgressionAsync` | 2 | 4.698598 | 2.349299 | 2.355970 |
| `MechanicsPublish.CreateQtePublicationAsync` | 1 | 4.229336 | 4.229336 | 4.229336 |
| `JointCompletion.ValidateCurrentCompletedProofAsync` | 1 | 3.974430 | 3.974430 | 3.974430 |
| `GameState.WorldQuestCombatFactionStateFiles` | 2 | 3.910039 | 1.955020 | 2.042510 |
| `JointCompletion.ValidateCurrentJointCompletionAsync` | 1 | 3.828419 | 3.828419 | 3.828419 |
| `JointProgression.ReadCurrentSnapshot` | 13 | 3.523481 | 0.271037 | 0.434781 |
| `JointPublish.CreateTransaction` | 1 | 3.185127 | 3.185127 | 3.185127 |
| `GameState.RealmSegregation` | 1 | 3.142340 | 3.142340 | 3.142340 |
| `JointCompletion.ValidateAppliedProgressionAsync` | 1 | 3.100731 | 3.100731 | 3.100731 |
| `JointStory.InspectCurrentAsync` | 2 | 2.994572 | 1.497286 | 2.484946 |
| `JointCommon.BindInput` | 1 | 2.929617 | 2.929617 | 2.929617 |
| `GameEngine.InspectOrdinaryMortalJointRepairAsync` | 1 | 2.791236 | 2.791236 | 2.791236 |
| `GameState.AcceptedTurnActorMaterializationCompleteness` | 2 | 2.758019 | 1.379009 | 1.540219 |
| `Checkpoint.CreateGuaranteedAttempt` | 20 | 2.602736 | 0.130137 | 0.180617 |
| `JointCommon.CreateAuthority` | 1 | 2.587530 | 2.587530 | 2.587530 |
| `JointPublish.CompleteRegistry` | 1 | 2.585179 | 2.585179 | 2.585179 |
| `GameState.NpcStateFiles` | 2 | 2.503814 | 1.251907 | 1.321419 |
| `JointCompletion.ReadInterfaceAtLateSlotAsync` | 1 | 2.469133 | 2.469133 | 2.469133 |
| `GameState.PlayerStateFiles` | 2 | 2.318815 | 1.159408 | 1.192274 |
| `GameEngine.ReadJointCompletedWitnessAsync` | 1 | 1.947054 | 1.947054 | 1.947054 |
| `GameEngine.GetOrBuildFinalValidatedAsync` | 1 | 1.855137 | 1.855137 | 1.855137 |
| `GameEngine.GetOrBuildEffectValidatedAsync` | 1 | 1.851164 | 1.851164 | 1.851164 |
| `Preparation.ReconstructAsync` | 1 | 1.850333 | 1.850333 | 1.850333 |
| `GameEngine.CreateJointRepairAnchors` | 1 | 1.669132 | 1.669132 | 1.669132 |
| `JointProgression.ValidateFrozenInputsAsync` | 13 | 1.604902 | 0.123454 | 0.173392 |
| `JointBaseline.ConfirmItemBaseline` | 44 | 1.600883 | 0.036384 | 0.067608 |
| `GameState.AcceptedTurnEffectMaterializationCompleteness` | 2 | 1.582485 | 0.791243 | 0.876270 |
| `GameState.RequiredFields` | 2 | 1.564334 | 0.782167 | 0.841704 |
| `GameEngine.ReadCurrentJointRejectedCaptureAsync` | 1 | 1.508171 | 1.508171 | 1.508171 |
| `JointInventory.ReadAndCheckSnapshot` | 3 | 1.472188 | 0.490729 | 0.680919 |
| `JointCalculation.CalculateArtifacts` | 1 | 1.469106 | 1.469106 | 1.469106 |
| `GameState.AcceptedTurnItemMaterializationCompleteness` | 2 | 1.422880 | 0.711440 | 0.773290 |
| `GameEngine.GetOrBuildPreparedValidatedAsync` | 1 | 1.352992 | 1.352992 | 1.352992 |
| `GameEngine.CaptureMortalJointProgressionSourceAsync` | 1 | 1.295074 | 1.295074 | 1.295074 |
| `GameEngine.ReserveMortalJointRepairPublicationAsync` | 1 | 1.250104 | 1.250104 | 1.250104 |
| `Preparation.ValidateContinuation` | 1 | 1.245024 | 1.245024 | 1.245024 |
| `GameState.AcceptedTurnLocationMaterializationCompleteness` | 2 | 1.114895 | 0.557447 | 0.578339 |
| `Checkpoint.RestorePersistedTreatmentRequests` | 20 | 1.061477 | 0.053074 | 0.079877 |
| `JointCompletion.ValidateReasoningAsync` | 1 | 1.050614 | 1.050614 | 1.050614 |
| `GameState.AcceptedTurnFactionMaterializationCompleteness` | 2 | 0.864243 | 0.432121 | 0.455519 |
| `JointCompletion.ValidatePostPublicationAuthorityAsync` | 1 | 0.792744 | 0.792744 | 0.792744 |
| `Preparation.RevalidateCorrectionAsync` | 1 | 0.689413 | 0.689413 | 0.689413 |
| `GameState.TrainingShowcases` | 2 | 0.677555 | 0.338777 | 0.359361 |
| `JointStory.TryReconcileMortalJointStoryTurnAsync` | 1 | 0.671736 | 0.671736 | 0.671736 |
| `JointCalculation.CaptureInputsAsync` | 1 | 0.661863 | 0.661863 | 0.661863 |
| `JointInput.CaptureCalculationInputs` | 1 | 0.658411 | 0.658411 | 0.658411 |
| `GameEngine.Intake.ValidateCurrentAsync` | 1 | 0.654153 | 0.654153 | 0.654153 |
| `JointCompletion.BindPublishedOutputAsync` | 1 | 0.652397 | 0.652397 | 0.652397 |
| `Preparation.CaptureBeforeImages` | 1 | 0.634843 | 0.634843 | 0.634843 |
| `MechanicsPublish.BuildWrites` | 1 | 0.586143 | 0.586143 | 0.586143 |
| `JointMechanical.ValidateOwnerAndFingerprint` | 48 | 0.537756 | 0.011203 | 0.025621 |
| `MechanicsPublish.PublishLocalBatchAsync` | 1 | 0.492387 | 0.492387 | 0.492387 |
| `Continuation.InspectConfirmedHeldClaim` | 54 | 0.472478 | 0.008750 | 0.019006 |
| `MechanicsPublish.ValidatePublishedWoundsAsync` | 1 | 0.447264 | 0.447264 | 0.447264 |
| `JointProgression.CompareOriginalGuards` | 13 | 0.439043 | 0.033773 | 0.058524 |
| `GameState.SkillContractConsistency` | 2 | 0.431722 | 0.215861 | 0.224774 |
| `MechanicsPublish.ValidateAgreementAsync` | 1 | 0.429457 | 0.429457 | 0.429457 |
| `GameEngine.CaptureCurrentHotCustodyAsync` | 1 | 0.418726 | 0.418726 | 0.418726 |
| `JointPublish.ValidateOutputAuthorityAsync` | 1 | 0.417584 | 0.417584 | 0.417584 |
| `Preparation.ReadPublicationBaselines` | 1 | 0.415470 | 0.415470 | 0.415470 |
| `GameState.AcceptedTurnWoundMaterializationCompleteness` | 2 | 0.387335 | 0.193668 | 0.228419 |
| `Correction.ReadDraft` | 23 | 0.383243 | 0.016663 | 0.029976 |
| `Correction.ReadPending` | 23 | 0.374123 | 0.016266 | 0.027949 |
| `Checkpoint.ReadAuthorityBytes` | 20 | 0.365853 | 0.018293 | 0.049726 |
| `Correction.ReadNarrative` | 23 | 0.361577 | 0.015721 | 0.030702 |
| `JointPublish.ValidateCanonicalItemAsync` | 1 | 0.359335 | 0.359335 | 0.359335 |
| `Correction.ReadCommand` | 23 | 0.352314 | 0.015318 | 0.025197 |
| `MechanicsPublish.CaptureAgreementBeforeImagesAsync` | 1 | 0.303409 | 0.303409 | 0.303409 |
| `Checkpoint.ReadManifestBytes` | 20 | 0.294461 | 0.014723 | 0.022467 |
| `Checkpoint.ReadCommand` | 20 | 0.288187 | 0.014409 | 0.029687 |
| `GameState.NoLifeEvaluationRewardsOnTriggerTurn` | 2 | 0.281270 | 0.140635 | 0.159027 |
| `JointInput.ComposeReadSnapshot` | 1 | 0.277190 | 0.277190 | 0.277190 |
| `JointPublish.ValidateCanonicalEffectAsync` | 1 | 0.246980 | 0.246980 | 0.246980 |
| `GameState.AfterlifeSpiritualConflictState` | 2 | 0.236972 | 0.118486 | 0.143514 |
| `JointCompletion.ValidateNarrativeReadOnlyAsync` | 1 | 0.236192 | 0.236192 | 0.236192 |
| `JointInput.CaptureReadSnapshot` | 1 | 0.232348 | 0.232348 | 0.232348 |
| `JointPublish.ValidateCanonicalResourceAsync` | 1 | 0.226797 | 0.226797 | 0.226797 |
| `MechanicsPublish.PrepareBeforeImages` | 1 | 0.213776 | 0.213776 | 0.213776 |
| `GameState.ShiningLeadershipHeadReferences` | 2 | 0.205066 | 0.102533 | 0.113302 |
| `GameState.SourceOfLightCapstoneGlobalState` | 2 | 0.204650 | 0.102325 | 0.122219 |
| `GameState.GuardianResonancePowerEvents` | 2 | 0.191351 | 0.095676 | 0.112641 |
| `GameState.MortalBootstrapContentAnchors` | 2 | 0.190269 | 0.095135 | 0.103513 |
| `MechanicsPublish.ReadBackBatchAsync` | 1 | 0.180475 | 0.180475 | 0.180475 |
| `GameState.LoreBootstrapRequiredFiles` | 2 | 0.158490 | 0.079245 | 0.079497 |
| `JointProgression.ValidateOwnerAndSeal` | 13 | 0.138919 | 0.010686 | 0.016481 |
| `JointCompletion.ValidateProgressionScheduleAsync` | 1 | 0.110529 | 0.110529 | 0.110529 |
| `JointPublish.ValidateCanonicalWoundAsync` | 1 | 0.104777 | 0.104777 | 0.104777 |
| `JointPublish.ValidateCanonicalLocationAsync` | 1 | 0.103210 | 0.103210 | 0.103210 |
| `JointCompletion.ValidateQteOfferAsync` | 1 | 0.093484 | 0.093484 | 0.093484 |
| `Checkpoint.ReadSessionGeneration` | 20 | 0.073671 | 0.003684 | 0.004734 |
| `MechanicsPublish.ValidatePublishedResourcesAsync` | 1 | 0.052950 | 0.052950 | 0.052950 |
| `Preparation.PrepareTreatmentOutcome` | 1 | 0.048910 | 0.048910 | 0.048910 |
| `GameState.MortalBootstrapPlayerVisibleNames` | 2 | 0.040548 | 0.020274 | 0.021120 |
| `GameState.JsonIntegrity` | 2 | 0.039985 | 0.019993 | 0.020925 |
| `MechanicsPublish.ValidateAuthorityBeforeImagesAsync` | 1 | 0.038257 | 0.038257 | 0.038257 |
| `Checkpoint.FinalizeResource` | 20 | 0.037731 | 0.001887 | 0.008684 |
| `JointStory.ReadCompletionState` | 2 | 0.037293 | 0.018646 | 0.031027 |
| `Preparation.ValidateInput` | 1 | 0.032474 | 0.032474 | 0.032474 |
| `JointCompletion.ValidateCombatMaterializationAsync` | 1 | 0.024169 | 0.024169 | 0.024169 |
| `JointCompletion.ValidateSpecialActionOutcomesAsync` | 1 | 0.022356 | 0.022356 | 0.022356 |
| `GameState.SoulStateConsistency` | 2 | 0.018991 | 0.009496 | 0.011604 |
| `Preparation.ValidateTreatmentPublicationShell` | 1 | 0.018053 | 0.018053 | 0.018053 |
| `JointCompletion.ValidateLevelUpMaterializationAsync` | 1 | 0.015911 | 0.015911 | 0.015911 |
| `GameState.RequiredFiles` | 2 | 0.012816 | 0.006408 | 0.006430 |
| `JointCommonAuthority.ProjectCandidateBounds` | 2 | 0.010413 | 0.005207 | 0.005349 |
| `JointCompletion.ValidateMemoryLegacyAsync` | 1 | 0.009637 | 0.009637 | 0.009637 |
| `MechanicsPublish.ReadCompletionBeforeImageAsync` | 1 | 0.008701 | 0.008701 | 0.008701 |
| `Preparation.ConstructContinuationAuthority` | 1 | 0.007660 | 0.007660 | 0.007660 |
| `MechanicsPublish.ValidateItemFinalBaselineAsync` | 1 | 0.002715 | 0.002715 | 0.002715 |
| `Preparation.PrepareSeverityRematerialization` | 1 | 0.001858 | 0.001858 | 0.001858 |
| `MechanicsPublish.ValidatePublishedOwnerTransitionsAsync` | 1 | 0.001589 | 0.001589 | 0.001589 |
| `GameState.LifeEvaluationRewardCycle` | 2 | 0.001516 | 0.000758 | 0.001461 |
| `Preparation.ReadPublishedInterface` | 1 | 0.001430 | 0.001430 | 0.001430 |
| `GameState.AfterlifeGlobalFlagPreTurnContinuity` | 2 | 0.001149 | 0.000574 | 0.001135 |
| `GameState.AfterlifeActiveThreatPreTurnContinuity` | 2 | 0.001127 | 0.000564 | 0.001113 |
| `GameState.ShiningTreasuryClientOwnedState` | 2 | 0.001054 | 0.000527 | 0.001003 |
| `JointResources.CompareCompanionProjection` | 13 | 0.000932 | 0.000072 | 0.000511 |
| `JointCalculation.RetainArtifacts` | 1 | 0.000316 | 0.000316 | 0.000316 |

## Начатые интервалы без записи конца

Внешний → внутренний. Длительность завершения не измерена; родительский лимит 600 с не приписан отдельному методу.

| Участок кода | Номер начала | Начало по часам коллектора, с | Наблюдаемое окно до последней обычной записи, с |
| --- | ---: | ---: | ---: |
| `Resumer.FirstCurrentOwnerCall` | 1 | 5.228174 | 594.559697 |
| `JointStory.TryReconcileMortalJointStoryTurnAsync` | 2772 | 535.509012 | 64.278860 |
| `JointStory.ValidateAcceptedTurnOutcomeWithRepairLoopAsync` | 2781 | 546.336321 | 53.451551 |
| `JointCompletion.TryReadCompletedContinuationAsync` | 2782 | 551.828376 | 47.959496 |
| `JointCompletion.ValidatePostPublicationAsync` | 2783 | 559.021747 | 40.766125 |
| `JointCompletion.ValidateGameStateAsync` | 2786 | 575.890377 | 23.897494 |
| `GameState.ValidateCoreAsync` | 2787 | 575.890416 | 23.897455 |
| `GameState.ClientOwnedControlFiles` | 2846 | 599.787871 | 0.000000 |

Наблюдаемое окно — разность двух отметок вывода одних часов, с накладными расходами наблюдателя; это не завершённая длительность метода.

Самый внутренний наблюдавшийся незавершённый участок: **`GameState.ClientOwnedControlFiles`**.

## Проверка и границы вывода

Идентификатор процесса, последовательность записей и вложенность проверены по сырому stdout фактического дочернего восстановления. Извлечённые потоки побайтно сверены с TRX внутри архива; CRC, уникальность имён и SHA-256 каждого файла архива проверены. Три DLL сверены родительским отчётом до и после запуска; файлы исходного снимка повторно сверены в сохранённом генераторском архиве.

Сырой архив: [T305-profile-cut0-raw.zip](T305-profile-cut0-raw.zip); SHA-256 `E8CA0709E358A821A05C51788EE94BAE341BC974E1F88D9E2734355FB0A01A5E`. Полные интервалы, ссылки на строки исходников и индекс архива: [T306-timings.json](T306-timings.json).

EOF/join обоих процессов: True; ошибок завершения контейнеров: 0; отчётов с игровым результатом: 0.

Отдельный существующий канал стадий зафиксировал `resume-activity/enter` (`None`) на 585.379800 с своих часов. Это отдельное наблюдение контрольной точки; его часы имеют другое начало отсчёта. Оно не является игровым PASS.

- Удаление собственного тестового корня: True; отсутствие подтверждено: True.
- Удаление собственного тестового корня: True; отсутствие подтверждено: True.

## Счётчики CPU того же дочернего процесса

Все 33 снимка связаны с фактическим PID и точным временем рождения из native-отчёта. Наблюдатель читал стандартные счётчики процесса каждые 15 с до его выхода; квоты количества или длительности не было. Первый отдельный снимок сделан до запуска периодического наблюдателя.

Последний снимок `2026-10-20T09:24:47.0505149Z`: накоплено CPU 567.437500 с, пользовательское время 184.078125 с, время ядра 383.359375 с. Между первым и последним снимками: CPU 524.859375 с за 559.213312 с по UTC.

Это процессные суммы всех потоков, не CPU отдельного именованного интервала. Значения считываются последовательно; небольшая разница total и user+kernel возможна из-за продвижения счётчиков. UTC, часы коллектора и родительского ожидания различаются. Время ядра не устанавливает конкретный драйвер, системный вызов или влияние антивируса. Снимки, состояние и исходник наблюдателя сохранены в сыром архиве.

Это замер одного запуска с наблюдателями. Он показывает стоимость участков, но не разделяет работу CPU, файловой системы и внешних проверок. Оптимизаций и увеличения лимитов в этом блоке нет. Исходные пять Windows-случаев: 0/5 PASS; Linux локально не запускался.
