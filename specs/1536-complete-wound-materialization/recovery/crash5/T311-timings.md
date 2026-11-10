# Именованные замеры восстановления — T311

Один фактический запуск: `20261020-205402-927-27872-c043737257484db1bdd96c54578824be-categories`. Результат случая: **Failed**; тест 00:12:19.0149637, весь запуск 00:12:38.2904705.

Исходники: `5d4562cb3ad2f0d423e1283c39afc6dec7ac515a`; сохранённое состояние запуска: `ea47ae13e2ee89c1cb88831d6c2755fd09cad1fc`. PID восстановления: 29796. Получено 2510 записей без квоты диагностического вывода. Последняя запись по часам коллектора: 567.372006 с.

## Завершённые интервалы

Значения включают вложенные вызовы. Строки нельзя складывать как независимые части общего времени. Конец интервала также записывается при выходе через исключение и сам по себе не означает успешную операцию.

| Участок кода | Завершено вызовов | Сумма, с | Среднее, с | Максимум, с |
| --- | ---: | ---: | ---: | ---: |
| `GameEngine.PublishCurrentMortalJointRepairAsync` | 1 | 296.970538 | 296.970538 | 296.970538 |
| `JointCompletion.ValidatePostPublicationAsync` | 2 | 149.753651 | 74.876826 | 83.440596 |
| `JointResources.ValidateCurrentAsync` | 13 | 133.795679 | 10.291975 | 10.408122 |
| `JointResources.RecomputeQteAsync` | 13 | 129.621828 | 9.970910 | 10.093269 |
| `GameEngine.CreateJointRepairCommonPlanAsync` | 1 | 126.288127 | 126.288127 | 126.288127 |
| `JointBaseline.ValidateCurrentAsync` | 31 | 120.714239 | 3.894008 | 3.975402 |
| `JointCommonAuthority.ValidateCurrentAsync` | 10 | 115.545964 | 11.554596 | 11.656601 |
| `JointStory.ValidateAcceptedTurnOutcomeWithRepairLoopAsync` | 1 | 92.975284 | 92.975284 | 92.975284 |
| `JointCompletion.TryReadCompletedContinuationAsync` | 1 | 90.010780 | 90.010780 | 90.010780 |
| `JointMechanical.ValidateCurrentAsync` | 35 | 84.206125 | 2.405889 | 2.551275 |
| `JointBaseline.ValidateMechanicalAsync` | 31 | 74.517289 | 2.403784 | 2.484052 |
| `JointCompletion.ValidateGameStateAsync` | 2 | 60.743368 | 30.371684 | 30.775318 |
| `GameState.ValidateCoreAsync` | 2 | 60.740401 | 30.370200 | 30.775285 |
| `GameEngine.PrevalidateJointRepairPublicationTransactionAsync` | 1 | 56.864893 | 56.864893 | 56.864893 |
| `JointCompletion.FinalProgressionLeaseAndProjection` | 2 | 55.312531 | 27.656265 | 29.146094 |
| `GameEngine.NormalizeAndValidateJointRepairAsync` | 1 | 49.791825 | 49.791825 | 49.791825 |
| `JointPublish.NormalizeAndValidateAsync` | 1 | 49.787199 | 49.787199 | 49.787199 |
| `Continuation.ValidateCurrentAsync` | 41 | 48.900133 | 1.192686 | 1.269920 |
| `JointMechanical.RequireCurrentOriginalEvidence` | 35 | 46.506587 | 1.328760 | 1.375487 |
| `JointStory.AppendStoryAsync` | 1 | 46.297873 | 46.297873 | 46.297873 |
| `JointCommonAuthority.ValidateCandidateBoundsAsync` | 2 | 46.120144 | 23.060072 | 23.253827 |
| `JointMechanical.CompareBeforeImages` | 35 | 28.558230 | 0.815949 | 0.944024 |
| `Checkpoint.CaptureCoreAsync` | 20 | 28.081877 | 1.404094 | 2.318263 |
| `JointCommon.ComposeInputAsync` | 1 | 26.350871 | 26.350871 | 26.350871 |
| `JointInput.ComposeAsync` | 1 | 26.346310 | 26.346310 | 26.346310 |
| `Continuation.CompareBeforeImages` | 41 | 24.184908 | 0.589876 | 0.642529 |
| `JointCompletion.CommitProgressionPhaseAsync` | 2 | 23.540150 | 11.770075 | 12.353476 |
| `JointCalculation.EnsureAsync` | 1 | 22.672279 | 22.672279 | 22.672279 |
| `JointPublish.PublishAcceptedMechanicsAsync` | 1 | 20.832467 | 20.832467 | 20.832467 |
| `MechanicsPublish.PublishAsync` | 1 | 20.823210 | 20.823210 | 20.823210 |
| `JointCompletion.WriteOrVerifyProjectionAsync` | 2 | 19.721780 | 9.860890 | 11.063393 |
| `GameEngine.CreateJointRepairResourcePublicationAsync` | 1 | 18.319794 | 18.319794 | 18.319794 |
| `GameState.MetaMiscStateFiles` | 2 | 15.930887 | 7.965443 | 7.998904 |
| `JointPublish.TakeAsync` | 1 | 15.837625 | 15.837625 | 15.837625 |
| `JointCommon.CreateRegistryCandidate` | 1 | 15.291248 | 15.291248 | 15.291248 |
| `GameEngine.CreateJointRepairCommonBaselineAsync` | 1 | 14.524065 | 14.524065 | 14.524065 |
| `MechanicsPublish.ValidateTakenJointInputsAsync` | 1 | 14.483286 | 14.483286 | 14.483286 |
| `JointPublish.ValidateTakenInputsAsync` | 1 | 14.481579 | 14.481579 | 14.481579 |
| `Baselines.ReadPublicationBaselines` | 42 | 13.851591 | 0.329800 | 0.347912 |
| `Continuation.ReadPublicationBaselines` | 41 | 13.528679 | 0.329968 | 0.347946 |
| `Correction.RevalidateCorrectionAsync` | 23 | 12.769776 | 0.555208 | 0.657837 |
| `GameState.ClientOwnedControlFiles` | 2 | 11.945775 | 5.972888 | 6.248135 |
| `Correction.ComposeCorrectedQteResponse` | 23 | 11.420457 | 0.496542 | 0.596794 |
| `Continuation.ReadSnapshot` | 41 | 10.334962 | 0.252072 | 0.274119 |
| `JointStory.VerifyProjectionBeforeStoryAsync` | 1 | 10.176446 | 10.176446 | 10.176446 |
| `GameEngine.CreateMechanicalAdmissionAsync` | 1 | 10.119084 | 10.119084 | 10.119084 |
| `Checkpoint.ComposeRejectedQteResponse` | 20 | 9.857148 | 0.492857 | 0.576395 |
| `JointStory.ValidateStoryProofAsync` | 1 | 9.744723 | 9.744723 | 9.744723 |
| `JointCommonAuthority.ValidateSeal` | 10 | 9.360491 | 0.936049 | 0.972983 |
| `JointMechanical.ReadAndCheckSnapshot` | 35 | 8.811125 | 0.251746 | 0.291540 |
| `JointStory.VerifyProjectionAfterStoryAsync` | 1 | 8.490996 | 8.490996 | 8.490996 |
| `JointBaseline.ValidateSeal` | 31 | 8.070134 | 0.260327 | 0.307071 |
| `Checkpoint.ExportCurrentTreatmentAuthority` | 20 | 7.887525 | 0.394376 | 0.885251 |
| `JointStory.InitialInspectionLease` | 2 | 7.721647 | 3.860824 | 7.069671 |
| `GameState.CrossReferences` | 2 | 7.528257 | 3.764129 | 3.838110 |
| `JointInventory.ValidateCurrentAsync` | 3 | 7.422951 | 2.474317 | 2.558903 |
| `JointCommon.SealRegistryCandidate` | 1 | 7.063259 | 7.063259 | 7.063259 |
| `JointPublish.ValidateReceiptLineage` | 1 | 6.816909 | 6.816909 | 6.816909 |
| `GameState.RealmSegregation` | 2 | 6.024440 | 3.012220 | 3.407758 |
| `JointCompletion.ValidateAppliedProgressionAsync` | 2 | 5.900592 | 2.950296 | 3.362776 |
| `JointCompletion.ReadCurrentProgressionAsync` | 2 | 5.796360 | 2.898180 | 3.334901 |
| `JointPublish.ValidateTransactionReadbackAsync` | 1 | 5.754034 | 5.754034 | 5.754034 |
| `JointPublish.CandidateAdmissionGate` | 1 | 5.105442 | 5.105442 | 5.105442 |
| `JointCompletion.ReadInterfaceAtLateSlotAsync` | 2 | 5.003902 | 2.501951 | 2.745052 |
| `Checkpoint.ReadSnapshot` | 20 | 4.995131 | 0.249757 | 0.260573 |
| `JointCompletion.ValidateCurrentCompletedProofAsync` | 2 | 4.949092 | 2.474546 | 2.687673 |
| `JointCompletion.ReadObservedProgressionAsync` | 2 | 4.930466 | 2.465233 | 2.469106 |
| `JointCompletion.ValidateCurrentJointCompletionAsync` | 2 | 4.623341 | 2.311670 | 2.523042 |
| `JointProgression.ValidateCurrentSourceAsync` | 13 | 4.496129 | 0.345856 | 0.394525 |
| `JointCommon.CandidateAdmissionGate` | 1 | 4.310880 | 4.310880 | 4.310880 |
| `JointResources.ValidateSeal` | 13 | 4.169588 | 0.320738 | 0.333965 |
| `JointInventory.ValidateOwnerAndSeal` | 3 | 4.118537 | 1.372846 | 1.410109 |
| `GameEngine.CreateJointRepairPreparationAsync` | 1 | 4.101314 | 4.101314 | 4.101314 |
| `Preparation.CreateJointRepairPreparationAsync` | 1 | 4.096537 | 4.096537 | 4.096537 |
| `MechanicsPublish.CreateQtePublicationAsync` | 1 | 3.837216 | 3.837216 | 3.837216 |
| `GameState.WorldQuestCombatFactionStateFiles` | 2 | 3.760434 | 1.880217 | 1.967382 |
| `JointStory.ReadAppliedProgressionPhaseAsync` | 1 | 3.089090 | 3.089090 | 3.089090 |
| `JointPublish.CompleteRegistry` | 1 | 3.069102 | 3.069102 | 3.069102 |
| `JointProgression.ReadCurrentSnapshot` | 13 | 2.884702 | 0.221900 | 0.259016 |
| `JointStory.InspectCurrentAsync` | 2 | 2.873293 | 1.436647 | 2.386844 |
| `JointPublish.CreateTransaction` | 1 | 2.758046 | 2.758046 | 2.758046 |
| `JointStory.AcceptedOutcomeCheckpoint` | 1 | 2.717979 | 2.717979 | 2.717979 |
| `GameEngine.InspectOrdinaryMortalJointRepairAsync` | 1 | 2.649323 | 2.649323 | 2.649323 |
| `JointCommon.CreateAuthority` | 1 | 2.569383 | 2.569383 | 2.569383 |
| `JointPublish.ValidateTakenInventoryAsync` | 1 | 2.559121 | 2.559121 | 2.559121 |
| `JointInventory.CompareBeforeImages` | 3 | 2.548495 | 0.849498 | 0.888238 |
| `JointStory.ValidateAppliedProgressionAsync` | 1 | 2.526565 | 2.526565 | 2.526565 |
| `JointCommon.BindInput` | 1 | 2.448332 | 2.448332 | 2.448332 |
| `JointStory.ReadOriginalProgressionPhaseAsync` | 1 | 2.435194 | 2.435194 | 2.435194 |
| `GameState.NpcStateFiles` | 2 | 2.416865 | 1.208433 | 1.222762 |
| `GameState.AcceptedTurnActorMaterializationCompleteness` | 2 | 2.332228 | 1.166114 | 1.170370 |
| `JointCompletion.ValidateReasoningAsync` | 2 | 2.327636 | 1.163818 | 1.250028 |
| `GameState.PlayerStateFiles` | 2 | 2.220057 | 1.110028 | 1.130958 |
| `Checkpoint.CreateGuaranteedAttempt` | 20 | 2.161204 | 0.108060 | 0.121343 |
| `GameEngine.ReadJointCompletedWitnessAsync` | 1 | 2.090578 | 2.090578 | 2.090578 |
| `GameEngine.GetOrBuildFinalValidatedAsync` | 1 | 1.755418 | 1.755418 | 1.755418 |
| `GameEngine.GetOrBuildEffectValidatedAsync` | 1 | 1.712428 | 1.712428 | 1.712428 |
| `GameEngine.CreateJointRepairAnchors` | 1 | 1.544048 | 1.544048 | 1.544048 |
| `GameState.RequiredFields` | 2 | 1.470976 | 0.735488 | 0.740821 |
| `GameState.AcceptedTurnEffectMaterializationCompleteness` | 2 | 1.413032 | 0.706516 | 0.716593 |
| `GameEngine.ReadCurrentJointRejectedCaptureAsync` | 1 | 1.387529 | 1.387529 | 1.387529 |
| `JointCalculation.CalculateArtifacts` | 1 | 1.371289 | 1.371289 | 1.371289 |
| `Preparation.ReconstructAsync` | 1 | 1.340496 | 1.340496 | 1.340496 |
| `GameState.AcceptedTurnItemMaterializationCompleteness` | 2 | 1.339889 | 0.669944 | 0.728304 |
| `GameEngine.GetOrBuildPreparedValidatedAsync` | 1 | 1.326914 | 1.326914 | 1.326914 |
| `GameEngine.ReserveMortalJointRepairPublicationAsync` | 1 | 1.201796 | 1.201796 | 1.201796 |
| `Preparation.ValidateContinuation` | 1 | 1.189327 | 1.189327 | 1.189327 |
| `JointProgression.ValidateFrozenInputsAsync` | 13 | 1.173961 | 0.090305 | 0.096892 |
| `JointBaseline.ConfirmItemBaseline` | 31 | 0.963068 | 0.031067 | 0.039920 |
| `GameState.AcceptedTurnLocationMaterializationCompleteness` | 2 | 0.928654 | 0.464327 | 0.476085 |
| `Checkpoint.RestorePersistedTreatmentRequests` | 20 | 0.922987 | 0.046149 | 0.061192 |
| `GameEngine.CaptureMortalJointProgressionSourceAsync` | 1 | 0.915077 | 0.915077 | 0.915077 |
| `JointCompletion.ValidatePostPublicationAuthorityAsync` | 1 | 0.832991 | 0.832991 | 0.832991 |
| `JointInventory.ReadAndCheckSnapshot` | 3 | 0.755519 | 0.251840 | 0.260427 |
| `GameState.AcceptedTurnFactionMaterializationCompleteness` | 2 | 0.715481 | 0.357740 | 0.367026 |
| `JointCompletion.BindPublishedOutputAsync` | 1 | 0.657073 | 0.657073 | 0.657073 |
| `JointStory.TryReconcileMortalJointStoryTurnAsync` | 1 | 0.652493 | 0.652493 | 0.652493 |
| `GameEngine.Intake.ValidateCurrentAsync` | 1 | 0.628154 | 0.628154 | 0.628154 |
| `JointCalculation.CaptureInputsAsync` | 1 | 0.602572 | 0.602572 | 0.602572 |
| `JointInput.CaptureCalculationInputs` | 1 | 0.598992 | 0.598992 | 0.598992 |
| `GameState.TrainingShowcases` | 2 | 0.592034 | 0.296017 | 0.297654 |
| `Preparation.CaptureBeforeImages` | 1 | 0.581569 | 0.581569 | 0.581569 |
| `Preparation.RevalidateCorrectionAsync` | 1 | 0.538373 | 0.538373 | 0.538373 |
| `MechanicsPublish.PublishLocalBatchAsync` | 1 | 0.481237 | 0.481237 | 0.481237 |
| `MechanicsPublish.ValidatePublishedWoundsAsync` | 1 | 0.476739 | 0.476739 | 0.476739 |
| `JointCompletion.ValidateNarrativeReadOnlyAsync` | 2 | 0.466785 | 0.233393 | 0.274435 |
| `MechanicsPublish.ValidateAgreementAsync` | 1 | 0.442324 | 0.442324 | 0.442324 |
| `GameState.SkillContractConsistency` | 2 | 0.418621 | 0.209311 | 0.210853 |
| `JointPublish.ValidateOutputAuthorityAsync` | 1 | 0.410384 | 0.410384 | 0.410384 |
| `JointPublish.ValidateCanonicalItemAsync` | 1 | 0.404300 | 0.404300 | 0.404300 |
| `MechanicsPublish.BuildWrites` | 1 | 0.361252 | 0.361252 | 0.361252 |
| `GameEngine.CaptureCurrentHotCustodyAsync` | 1 | 0.339356 | 0.339356 | 0.339356 |
| `JointProgression.CompareOriginalGuards` | 13 | 0.328527 | 0.025271 | 0.030419 |
| `Preparation.ReadPublicationBaselines` | 1 | 0.325111 | 0.325111 | 0.325111 |
| `JointMechanical.ValidateOwnerAndFingerprint` | 35 | 0.324354 | 0.009267 | 0.011928 |
| `Continuation.InspectConfirmedHeldClaim` | 41 | 0.307414 | 0.007498 | 0.013217 |
| `GameState.AcceptedTurnWoundMaterializationCompleteness` | 2 | 0.296217 | 0.148109 | 0.150064 |
| `Correction.ReadPending` | 23 | 0.289395 | 0.012582 | 0.014373 |
| `MechanicsPublish.CaptureAgreementBeforeImagesAsync` | 1 | 0.288777 | 0.288777 | 0.288777 |
| `Correction.ReadCommand` | 23 | 0.288264 | 0.012533 | 0.014864 |
| `Correction.ReadDraft` | 23 | 0.281858 | 0.012255 | 0.015666 |
| `Correction.ReadNarrative` | 23 | 0.274253 | 0.011924 | 0.013579 |
| `JointPublish.ValidateCanonicalEffectAsync` | 1 | 0.255442 | 0.255442 | 0.255442 |
| `Checkpoint.ReadManifestBytes` | 20 | 0.251238 | 0.012562 | 0.013737 |
| `Checkpoint.ReadAuthorityBytes` | 20 | 0.247145 | 0.012357 | 0.013563 |
| `Checkpoint.ReadCommand` | 20 | 0.246894 | 0.012345 | 0.012955 |
| `JointPublish.ValidateCanonicalResourceAsync` | 1 | 0.240841 | 0.240841 | 0.240841 |
| `GameState.NoLifeEvaluationRewardsOnTriggerTurn` | 2 | 0.239663 | 0.119831 | 0.122897 |
| `JointInput.CaptureReadSnapshot` | 1 | 0.216466 | 0.216466 | 0.216466 |
| `JointInput.ComposeReadSnapshot` | 1 | 0.215283 | 0.215283 | 0.215283 |
| `GameState.SourceOfLightCapstoneGlobalState` | 2 | 0.183919 | 0.091960 | 0.094729 |
| `MechanicsPublish.PrepareBeforeImages` | 1 | 0.182448 | 0.182448 | 0.182448 |
| `GameState.AfterlifeSpiritualConflictState` | 2 | 0.182006 | 0.091003 | 0.094775 |
| `JointCompletion.ValidateQteOfferAsync` | 2 | 0.181158 | 0.090579 | 0.092459 |
| `GameState.MortalBootstrapContentAnchors` | 2 | 0.178814 | 0.089407 | 0.091287 |
| `MechanicsPublish.ReadBackBatchAsync` | 1 | 0.173841 | 0.173841 | 0.173841 |
| `GameState.ShiningLeadershipHeadReferences` | 2 | 0.169562 | 0.084781 | 0.089406 |
| `GameState.GuardianResonancePowerEvents` | 2 | 0.169206 | 0.084603 | 0.086072 |
| `GameState.LoreBootstrapRequiredFiles` | 2 | 0.156075 | 0.078038 | 0.081512 |
| `JointPublish.ValidateCanonicalLocationAsync` | 1 | 0.113037 | 0.113037 | 0.113037 |
| `JointPublish.ValidateCanonicalWoundAsync` | 1 | 0.111147 | 0.111147 | 0.111147 |
| `JointCompletion.ValidateProgressionScheduleAsync` | 1 | 0.107729 | 0.107729 | 0.107729 |
| `JointProgression.ValidateOwnerAndSeal` | 13 | 0.106780 | 0.008214 | 0.009460 |
| `Checkpoint.ReadSessionGeneration` | 20 | 0.069514 | 0.003476 | 0.004001 |
| `MechanicsPublish.ValidatePublishedResourcesAsync` | 1 | 0.057876 | 0.057876 | 0.057876 |
| `JointCompletion.ValidateCombatMaterializationAsync` | 2 | 0.049660 | 0.024830 | 0.025813 |
| `Preparation.PrepareTreatmentOutcome` | 1 | 0.043451 | 0.043451 | 0.043451 |
| `GameState.JsonIntegrity` | 2 | 0.041548 | 0.020774 | 0.021972 |
| `GameState.MortalBootstrapPlayerVisibleNames` | 2 | 0.038027 | 0.019013 | 0.020385 |
| `JointCompletion.ValidateLevelUpMaterializationAsync` | 2 | 0.035206 | 0.017603 | 0.018598 |
| `JointCompletion.ValidateSpecialActionOutcomesAsync` | 2 | 0.033365 | 0.016682 | 0.025061 |
| `Checkpoint.FinalizeResource` | 20 | 0.027940 | 0.001397 | 0.008586 |
| `MechanicsPublish.ValidateAuthorityBeforeImagesAsync` | 1 | 0.027128 | 0.027128 | 0.027128 |
| `Preparation.ValidateInput` | 1 | 0.027115 | 0.027115 | 0.027115 |
| `GameState.SoulStateConsistency` | 2 | 0.019106 | 0.009553 | 0.011230 |
| `JointCompletion.ValidateMemoryLegacyAsync` | 2 | 0.018840 | 0.009420 | 0.010533 |
| `JointStory.ReadCompletionState` | 2 | 0.018655 | 0.009328 | 0.012564 |
| `Preparation.ValidateTreatmentPublicationShell` | 1 | 0.015412 | 0.015412 | 0.015412 |
| `GameState.RequiredFiles` | 2 | 0.013922 | 0.006961 | 0.007195 |
| `JointCommonAuthority.ProjectCandidateBounds` | 2 | 0.010459 | 0.005230 | 0.006460 |
| `Preparation.ConstructContinuationAuthority` | 1 | 0.007702 | 0.007702 | 0.007702 |
| `MechanicsPublish.ReadCompletionBeforeImageAsync` | 1 | 0.006436 | 0.006436 | 0.006436 |
| `Preparation.ReadPublishedInterface` | 1 | 0.002874 | 0.002874 | 0.002874 |
| `MechanicsPublish.ValidateItemFinalBaselineAsync` | 1 | 0.002393 | 0.002393 | 0.002393 |
| `GameState.LifeEvaluationRewardCycle` | 2 | 0.001618 | 0.000809 | 0.001565 |
| `MechanicsPublish.ValidatePublishedOwnerTransitionsAsync` | 1 | 0.001506 | 0.001506 | 0.001506 |
| `Preparation.PrepareSeverityRematerialization` | 1 | 0.001490 | 0.001490 | 0.001490 |
| `GameState.AfterlifeGlobalFlagPreTurnContinuity` | 2 | 0.001103 | 0.000552 | 0.001090 |
| `GameState.AfterlifeActiveThreatPreTurnContinuity` | 2 | 0.001078 | 0.000539 | 0.001065 |
| `GameState.ShiningTreasuryClientOwnedState` | 2 | 0.000979 | 0.000489 | 0.000922 |
| `JointResources.CompareCompanionProjection` | 13 | 0.000839 | 0.000065 | 0.000459 |
| `JointCalculation.RetainArtifacts` | 1 | 0.000175 | 0.000175 | 0.000175 |

## Начатые интервалы без записи конца

Внешний → внутренний. Длительность завершения не измерена; родительский лимит 600 с не приписан отдельному методу.

| Участок кода | Номер начала | Начало по часам коллектора, с | Наблюдаемое окно до последней обычной записи, с |
| --- | ---: | ---: | ---: |
| `Resumer.FirstCurrentOwnerCall` | 1 | 4.748384 | 562.623622 |
| `JointStory.TryReconcileMortalJointStoryTurnAsync` | 2382 | 384.126956 | 183.245050 |
| `JointStory.FinalMaterializationLeaseAndCleanup` | 2497 | 486.890049 | 80.481958 |
| `JointStory.CleanupEligibleTerminalAsync` | 2510 | 567.372006 | 0.000000 |

Наблюдаемое окно — разность двух отметок вывода одних часов, с накладными расходами наблюдателя; это не завершённая длительность метода.

Самый внутренний наблюдавшийся незавершённый участок: **`JointStory.CleanupEligibleTerminalAsync`**.

## Проверка и границы вывода

Идентификатор процесса, последовательность записей и вложенность проверены по сырому stdout фактического дочернего восстановления. Извлечённые потоки побайтно сверены с TRX внутри архива; CRC, уникальность имён и SHA-256 каждого файла архива проверены. Три DLL сверены родительским отчётом до и после запуска; файлы исходного снимка повторно сверены в сохранённом генераторском архиве.

Сырой архив: [T310-profile-cut0-raw.zip](T310-profile-cut0-raw.zip); SHA-256 `866B97A3F691ECC986DFF5F8DB77489A5EEABE7A6A5A0B0789A1F6CE0D4CDB6B`. Полные интервалы, ссылки на строки исходников и индекс архива: [T311-timings.json](T311-timings.json).

EOF/join обоих процессов: True; ошибок завершения контейнеров: 0; отчётов с игровым результатом: 0.

Отдельный существующий канал стадий зафиксировал `resume-activity/enter` (`None`) на 589.228948 с своих часов. Это отдельное наблюдение контрольной точки; его часы имеют другое начало отсчёта. Оно не является игровым PASS.

- Удаление собственного тестового корня: True; отсутствие подтверждено: True.
- Удаление собственного тестового корня: True; отсутствие подтверждено: True.

Это замер одного запуска с наблюдателями. Он показывает стоимость участков, но не разделяет работу CPU, файловой системы и внешних проверок. Удалена одна внешняя baseline-проверка; лимиты не увеличивались. Исходные пять Windows-случаев: 0/5 PASS; Linux локально не запускался.
