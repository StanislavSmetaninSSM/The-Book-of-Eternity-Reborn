# Именованные замеры восстановления — T316

Один фактический запуск: `20261020-221716-733-32068-a1d1d530afe846c792924d978c1b481f-categories`. Результат случая: **Failed**; тест 00:12:34.5470921, весь запуск 00:12:54.6524036.

Исходники: `3421990a448746b496ee9b4959a30a3a3bbd0aaa`; сохранённое состояние запуска: `e5bbe4b43735ef66b610a19d434cebe06e1491fa`. PID восстановления: 31856. Получено 2504 записей без квоты диагностического вывода. Последняя запись по часам коллектора: 596.746857 с.

## Завершённые интервалы

Значения включают вложенные вызовы. Строки нельзя складывать как независимые части общего времени. Конец интервала также записывается при выходе через исключение и сам по себе не означает успешную операцию.

| Участок кода | Завершено вызовов | Сумма, с | Среднее, с | Максимум, с |
| --- | ---: | ---: | ---: | ---: |
| `GameEngine.PublishCurrentMortalJointRepairAsync` | 1 | 314.355618 | 314.355618 | 314.355618 |
| `JointCompletion.ValidatePostPublicationAsync` | 2 | 169.005510 | 84.502755 | 90.166477 |
| `JointResources.ValidateCurrentAsync` | 13 | 145.952444 | 11.227111 | 11.732703 |
| `JointResources.RecomputeQteAsync` | 13 | 142.196918 | 10.938224 | 11.459429 |
| `JointBaseline.ValidateCurrentAsync` | 31 | 134.548738 | 4.340282 | 4.662342 |
| `GameEngine.CreateJointRepairCommonPlanAsync` | 1 | 130.374739 | 130.374739 | 130.374739 |
| `JointCommonAuthority.ValidateCurrentAsync` | 10 | 125.436337 | 12.543634 | 12.966713 |
| `JointStory.ValidateAcceptedTurnOutcomeWithRepairLoopAsync` | 1 | 102.020997 | 102.020997 | 102.020997 |
| `JointCompletion.TryReadCompletedContinuationAsync` | 1 | 98.535487 | 98.535487 | 98.535487 |
| `JointMechanical.ValidateCurrentAsync` | 35 | 91.708786 | 2.620251 | 2.922444 |
| `JointBaseline.ValidateMechanicalAsync` | 31 | 81.086653 | 2.615698 | 2.816022 |
| `JointCompletion.ValidateGameStateAsync` | 2 | 66.038542 | 33.019271 | 33.084825 |
| `GameState.ValidateCoreAsync` | 2 | 66.035835 | 33.017917 | 33.082150 |
| `JointCompletion.FinalProgressionLeaseAndProjection` | 2 | 63.017622 | 31.508811 | 33.645794 |
| `GameEngine.PrevalidateJointRepairPublicationTransactionAsync` | 1 | 61.030285 | 61.030285 | 61.030285 |
| `Continuation.ValidateCurrentAsync` | 41 | 59.926795 | 1.461629 | 1.650136 |
| `JointCommonAuthority.ValidateCandidateBoundsAsync` | 2 | 50.345742 | 25.172871 | 25.317771 |
| `GameEngine.NormalizeAndValidateJointRepairAsync` | 1 | 50.078004 | 50.078004 | 50.078004 |
| `JointPublish.NormalizeAndValidateAsync` | 1 | 50.073287 | 50.073287 | 50.073287 |
| `JointStory.AppendStoryAsync` | 1 | 49.419955 | 49.419955 | 49.419955 |
| `JointMechanical.RequireCurrentOriginalEvidence` | 35 | 44.774062 | 1.279259 | 1.484947 |
| `JointMechanical.CompareBeforeImages` | 35 | 38.334131 | 1.095261 | 1.235167 |
| `Continuation.CompareBeforeImages` | 41 | 32.388072 | 0.789953 | 0.910697 |
| `Checkpoint.CaptureCoreAsync` | 20 | 29.581298 | 1.479065 | 2.502594 |
| `JointCompletion.CommitProgressionPhaseAsync` | 2 | 27.645960 | 13.822980 | 15.698183 |
| `JointCommon.ComposeInputAsync` | 1 | 27.471531 | 27.471531 | 27.471531 |
| `JointInput.ComposeAsync` | 1 | 27.468827 | 27.468827 | 27.468827 |
| `JointCalculation.EnsureAsync` | 1 | 24.135426 | 24.135426 | 24.135426 |
| `JointCompletion.WriteOrVerifyProjectionAsync` | 2 | 22.037232 | 11.018616 | 11.324797 |
| `GameEngine.CreateJointRepairResourcePublicationAsync` | 1 | 20.282061 | 20.282061 | 20.282061 |
| `JointPublish.PublishAcceptedMechanicsAsync` | 1 | 19.343988 | 19.343988 | 19.343988 |
| `MechanicsPublish.PublishAsync` | 1 | 19.336103 | 19.336103 | 19.336103 |
| `JointPublish.TakeAsync` | 1 | 17.359803 | 17.359803 | 17.359803 |
| `Baselines.ReadPublicationBaselines` | 42 | 16.971464 | 0.404082 | 0.487000 |
| `Continuation.ReadPublicationBaselines` | 41 | 16.580628 | 0.404406 | 0.487038 |
| `GameState.MetaMiscStateFiles` | 2 | 16.355055 | 8.177528 | 8.280051 |
| `GameEngine.CreateJointRepairCommonBaselineAsync` | 1 | 16.136003 | 16.136003 | 16.136003 |
| `JointCommon.CreateRegistryCandidate` | 1 | 14.086581 | 14.086581 | 14.086581 |
| `Correction.RevalidateCorrectionAsync` | 23 | 13.347052 | 0.580307 | 0.648627 |
| `MechanicsPublish.ValidateTakenJointInputsAsync` | 1 | 12.412289 | 12.412289 | 12.412289 |
| `JointPublish.ValidateTakenInputsAsync` | 1 | 12.410805 | 12.410805 | 12.410805 |
| `GameState.ClientOwnedControlFiles` | 2 | 11.817876 | 5.908938 | 6.010159 |
| `Correction.ComposeCorrectedQteResponse` | 23 | 11.577754 | 0.503381 | 0.567490 |
| `GameEngine.CreateMechanicalAdmissionAsync` | 1 | 11.553325 | 11.553325 | 11.553325 |
| `JointStory.VerifyProjectionBeforeStoryAsync` | 1 | 10.821978 | 10.821978 | 10.821978 |
| `Continuation.ReadSnapshot` | 41 | 10.067704 | 0.245554 | 0.300720 |
| `Checkpoint.ComposeRejectedQteResponse` | 20 | 9.899776 | 0.494989 | 0.612361 |
| `JointStory.InitialInspectionLease` | 2 | 9.586543 | 4.793271 | 8.901255 |
| `GameState.CrossReferences` | 2 | 8.798707 | 4.399354 | 4.615660 |
| `JointCommonAuthority.ValidateSeal` | 10 | 8.732511 | 0.873251 | 0.960096 |
| `JointMechanical.ReadAndCheckSnapshot` | 35 | 8.250925 | 0.235741 | 0.295803 |
| `Checkpoint.ExportCurrentTreatmentAuthority` | 20 | 8.103517 | 0.405176 | 0.878105 |
| `JointInventory.ValidateCurrentAsync` | 3 | 7.750911 | 2.583637 | 2.778939 |
| `GameState.RealmSegregation` | 2 | 7.505206 | 3.752603 | 3.861792 |
| `JointBaseline.ValidateSeal` | 31 | 7.357758 | 0.237347 | 0.295235 |
| `JointCompletion.ValidateAppliedProgressionAsync` | 2 | 6.660951 | 3.330475 | 3.363158 |
| `JointCommon.SealRegistryCandidate` | 1 | 6.387344 | 6.387344 | 6.387344 |
| `JointCompletion.ReadCurrentProgressionAsync` | 2 | 6.352664 | 3.176332 | 3.187208 |
| `JointPublish.ValidateTransactionReadbackAsync` | 1 | 6.327443 | 6.327443 | 6.327443 |
| `JointCompletion.ReadObservedProgressionAsync` | 2 | 6.247291 | 3.123645 | 3.217838 |
| `JointPublish.ValidateReceiptLineage` | 1 | 5.913684 | 5.913684 | 5.913684 |
| `JointCompletion.ReadInterfaceAtLateSlotAsync` | 2 | 5.617778 | 2.808889 | 2.909297 |
| `JointCompletion.ValidateCurrentCompletedProofAsync` | 2 | 5.606097 | 2.803049 | 2.880564 |
| `JointProgression.ValidateCurrentSourceAsync` | 13 | 5.590812 | 0.430062 | 0.478441 |
| `Checkpoint.ReadSnapshot` | 20 | 5.480367 | 0.274018 | 0.304961 |
| `JointCompletion.ValidateCurrentJointCompletionAsync` | 2 | 5.258528 | 2.629264 | 2.697964 |
| `GameEngine.CreateJointRepairPreparationAsync` | 1 | 4.823367 | 4.823367 | 4.823367 |
| `Preparation.CreateJointRepairPreparationAsync` | 1 | 4.818210 | 4.818210 | 4.818210 |
| `JointPublish.CandidateAdmissionGate` | 1 | 3.991228 | 3.991228 | 3.991228 |
| `JointCommon.CandidateAdmissionGate` | 1 | 3.984555 | 3.984555 | 3.984555 |
| `MechanicsPublish.CreateQtePublicationAsync` | 1 | 3.883622 | 3.883622 | 3.883622 |
| `GameState.WorldQuestCombatFactionStateFiles` | 2 | 3.870467 | 1.935233 | 1.937245 |
| `JointInventory.ValidateOwnerAndSeal` | 3 | 3.802242 | 1.267414 | 1.339699 |
| `JointResources.ValidateSeal` | 13 | 3.750550 | 0.288504 | 0.318208 |
| `JointStory.InspectCurrentAsync` | 2 | 3.493774 | 1.746887 | 2.978322 |
| `JointProgression.ReadCurrentSnapshot` | 13 | 3.261255 | 0.250866 | 0.283694 |
| `JointInventory.CompareBeforeImages` | 3 | 3.214130 | 1.071377 | 1.179709 |
| `JointStory.ReadOriginalProgressionPhaseAsync` | 1 | 3.195501 | 3.195501 | 3.195501 |
| `JointStory.ReadAppliedProgressionPhaseAsync` | 1 | 3.161210 | 3.161210 | 3.161210 |
| `JointStory.AcceptedOutcomeCheckpoint` | 1 | 3.097338 | 3.097338 | 3.097338 |
| `GameEngine.InspectOrdinaryMortalJointRepairAsync` | 1 | 2.928585 | 2.928585 | 2.928585 |
| `GameEngine.ReadJointCompletedWitnessAsync` | 1 | 2.709138 | 2.709138 | 2.709138 |
| `GameState.NpcStateFiles` | 2 | 2.643608 | 1.321804 | 1.366292 |
| `GameState.AcceptedTurnActorMaterializationCompleteness` | 2 | 2.592829 | 1.296415 | 1.335541 |
| `JointPublish.ValidateTakenInventoryAsync` | 1 | 2.505788 | 2.505788 | 2.505788 |
| `JointPublish.CompleteRegistry` | 1 | 2.498439 | 2.498439 | 2.498439 |
| `GameState.PlayerStateFiles` | 2 | 2.487869 | 1.243934 | 1.299709 |
| `JointPublish.CreateTransaction` | 1 | 2.471663 | 2.471663 | 2.471663 |
| `JointCompletion.ValidateReasoningAsync` | 2 | 2.442411 | 1.221206 | 1.237617 |
| `JointCommon.CreateAuthority` | 1 | 2.326642 | 2.326642 | 2.326642 |
| `GameEngine.GetOrBuildFinalValidatedAsync` | 1 | 2.238111 | 2.238111 | 2.238111 |
| `Checkpoint.CreateGuaranteedAttempt` | 20 | 2.230657 | 0.111533 | 0.141618 |
| `JointCommon.BindInput` | 1 | 2.193012 | 2.193012 | 2.193012 |
| `GameEngine.GetOrBuildEffectValidatedAsync` | 1 | 2.026018 | 2.026018 | 2.026018 |
| `GameState.RequiredFields` | 2 | 1.727168 | 0.863584 | 0.888466 |
| `JointProgression.ValidateFrozenInputsAsync` | 13 | 1.700072 | 0.130775 | 0.147463 |
| `GameEngine.CreateJointRepairAnchors` | 1 | 1.685133 | 1.685133 | 1.685133 |
| `GameState.AcceptedTurnEffectMaterializationCompleteness` | 2 | 1.676089 | 0.838044 | 0.871705 |
| `GameEngine.GetOrBuildPreparedValidatedAsync` | 1 | 1.591656 | 1.591656 | 1.591656 |
| `GameEngine.ReserveMortalJointRepairPublicationAsync` | 1 | 1.570223 | 1.570223 | 1.570223 |
| `Preparation.ValidateContinuation` | 1 | 1.555268 | 1.555268 | 1.555268 |
| `GameEngine.ReadCurrentJointRejectedCaptureAsync` | 1 | 1.521068 | 1.521068 | 1.521068 |
| `GameState.AcceptedTurnItemMaterializationCompleteness` | 2 | 1.490781 | 0.745390 | 0.770561 |
| `Preparation.ReconstructAsync` | 1 | 1.463238 | 1.463238 | 1.463238 |
| `JointCalculation.CalculateArtifacts` | 1 | 1.271674 | 1.271674 | 1.271674 |
| `GameEngine.CaptureMortalJointProgressionSourceAsync` | 1 | 1.184000 | 1.184000 | 1.184000 |
| `JointBaseline.ConfirmItemBaseline` | 31 | 1.086974 | 0.035064 | 0.053097 |
| `GameState.AcceptedTurnLocationMaterializationCompleteness` | 2 | 1.030156 | 0.515078 | 0.550392 |
| `Checkpoint.RestorePersistedTreatmentRequests` | 20 | 0.932073 | 0.046604 | 0.073598 |
| `JointCompletion.ValidatePostPublicationAuthorityAsync` | 1 | 0.850201 | 0.850201 | 0.850201 |
| `Preparation.CaptureBeforeImages` | 1 | 0.757484 | 0.757484 | 0.757484 |
| `JointInventory.ReadAndCheckSnapshot` | 3 | 0.734167 | 0.244722 | 0.259387 |
| `GameState.AcceptedTurnFactionMaterializationCompleteness` | 2 | 0.730611 | 0.365305 | 0.366150 |
| `GameEngine.Intake.ValidateCurrentAsync` | 1 | 0.695455 | 0.695455 | 0.695455 |
| `JointStory.TryReconcileMortalJointStoryTurnAsync` | 1 | 0.685665 | 0.685665 | 0.685665 |
| `GameState.TrainingShowcases` | 2 | 0.674230 | 0.337115 | 0.343758 |
| `MechanicsPublish.ValidateAgreementAsync` | 1 | 0.616956 | 0.616956 | 0.616956 |
| `JointCalculation.CaptureInputsAsync` | 1 | 0.590720 | 0.590720 | 0.590720 |
| `JointInput.CaptureCalculationInputs` | 1 | 0.587315 | 0.587315 | 0.587315 |
| `JointCompletion.BindPublishedOutputAsync` | 1 | 0.566613 | 0.566613 | 0.566613 |
| `MechanicsPublish.ValidatePublishedWoundsAsync` | 1 | 0.566233 | 0.566233 | 0.566233 |
| `JointCompletion.ValidateNarrativeReadOnlyAsync` | 2 | 0.546668 | 0.273334 | 0.309808 |
| `Preparation.RevalidateCorrectionAsync` | 1 | 0.538028 | 0.538028 | 0.538028 |
| `MechanicsPublish.PublishLocalBatchAsync` | 1 | 0.524246 | 0.524246 | 0.524246 |
| `JointProgression.CompareOriginalGuards` | 13 | 0.505935 | 0.038918 | 0.057355 |
| `JointPublish.ValidateCanonicalItemAsync` | 1 | 0.499602 | 0.499602 | 0.499602 |
| `JointPublish.ValidateOutputAuthorityAsync` | 1 | 0.478955 | 0.478955 | 0.478955 |
| `GameState.SkillContractConsistency` | 2 | 0.468406 | 0.234203 | 0.245270 |
| `GameState.AcceptedTurnWoundMaterializationCompleteness` | 2 | 0.455465 | 0.227732 | 0.239165 |
| `GameEngine.CaptureCurrentHotCustodyAsync` | 1 | 0.427907 | 0.427907 | 0.427907 |
| `JointPublish.ValidateCanonicalEffectAsync` | 1 | 0.415073 | 0.415073 | 0.415073 |
| `Correction.ReadPending` | 23 | 0.413057 | 0.017959 | 0.029039 |
| `Preparation.ReadPublicationBaselines` | 1 | 0.393133 | 0.393133 | 0.393133 |
| `Correction.ReadDraft` | 23 | 0.391551 | 0.017024 | 0.027773 |
| `Correction.ReadNarrative` | 23 | 0.383015 | 0.016653 | 0.027716 |
| `Checkpoint.ReadAuthorityBytes` | 20 | 0.355326 | 0.017766 | 0.030142 |
| `JointPublish.ValidateCanonicalResourceAsync` | 1 | 0.351837 | 0.351837 | 0.351837 |
| `MechanicsPublish.CaptureAgreementBeforeImagesAsync` | 1 | 0.351063 | 0.351063 | 0.351063 |
| `Continuation.InspectConfirmedHeldClaim` | 41 | 0.346411 | 0.008449 | 0.017283 |
| `JointMechanical.ValidateOwnerAndFingerprint` | 35 | 0.344728 | 0.009849 | 0.016074 |
| `MechanicsPublish.BuildWrites` | 1 | 0.339502 | 0.339502 | 0.339502 |
| `Correction.ReadCommand` | 23 | 0.336061 | 0.014611 | 0.022102 |
| `Checkpoint.ReadManifestBytes` | 20 | 0.310414 | 0.015521 | 0.029086 |
| `Checkpoint.ReadCommand` | 20 | 0.303521 | 0.015176 | 0.018828 |
| `GameState.NoLifeEvaluationRewardsOnTriggerTurn` | 2 | 0.272420 | 0.136210 | 0.143506 |
| `MechanicsPublish.PrepareBeforeImages` | 1 | 0.267028 | 0.267028 | 0.267028 |
| `JointInput.ComposeReadSnapshot` | 1 | 0.245382 | 0.245382 | 0.245382 |
| `GameState.MortalBootstrapContentAnchors` | 2 | 0.232508 | 0.116254 | 0.116848 |
| `MechanicsPublish.ReadBackBatchAsync` | 1 | 0.229069 | 0.229069 | 0.229069 |
| `GameState.SourceOfLightCapstoneGlobalState` | 2 | 0.228844 | 0.114422 | 0.127491 |
| `JointInput.CaptureReadSnapshot` | 1 | 0.224924 | 0.224924 | 0.224924 |
| `GameState.LoreBootstrapRequiredFiles` | 2 | 0.215087 | 0.107544 | 0.124542 |
| `GameState.AfterlifeSpiritualConflictState` | 2 | 0.212452 | 0.106226 | 0.109376 |
| `JointCompletion.ValidateQteOfferAsync` | 2 | 0.197814 | 0.098907 | 0.099194 |
| `GameState.GuardianResonancePowerEvents` | 2 | 0.186813 | 0.093406 | 0.096864 |
| `GameState.ShiningLeadershipHeadReferences` | 2 | 0.182490 | 0.091245 | 0.095851 |
| `JointPublish.ValidateCanonicalWoundAsync` | 1 | 0.169717 | 0.169717 | 0.169717 |
| `JointPublish.ValidateCanonicalLocationAsync` | 1 | 0.155876 | 0.155876 | 0.155876 |
| `JointCompletion.ValidateProgressionScheduleAsync` | 1 | 0.146263 | 0.146263 | 0.146263 |
| `JointProgression.ValidateOwnerAndSeal` | 13 | 0.121361 | 0.009335 | 0.012809 |
| `MechanicsPublish.ValidatePublishedResourcesAsync` | 1 | 0.101772 | 0.101772 | 0.101772 |
| `Checkpoint.ReadSessionGeneration` | 20 | 0.084646 | 0.004232 | 0.008315 |
| `GameState.JsonIntegrity` | 2 | 0.075632 | 0.037816 | 0.038487 |
| `JointCompletion.ValidateCombatMaterializationAsync` | 2 | 0.064486 | 0.032243 | 0.033301 |
| `JointCompletion.ValidateLevelUpMaterializationAsync` | 2 | 0.061668 | 0.030834 | 0.040797 |
| `JointCompletion.ValidateSpecialActionOutcomesAsync` | 2 | 0.051255 | 0.025627 | 0.038141 |
| `GameState.MortalBootstrapPlayerVisibleNames` | 2 | 0.049532 | 0.024766 | 0.027626 |
| `Checkpoint.FinalizeResource` | 20 | 0.040164 | 0.002008 | 0.014460 |
| `JointCompletion.ValidateMemoryLegacyAsync` | 2 | 0.039106 | 0.019553 | 0.020035 |
| `Preparation.PrepareTreatmentOutcome` | 1 | 0.035722 | 0.035722 | 0.035722 |
| `MechanicsPublish.ValidateAuthorityBeforeImagesAsync` | 1 | 0.033071 | 0.033071 | 0.033071 |
| `Preparation.ValidateInput` | 1 | 0.025251 | 0.025251 | 0.025251 |
| `JointStory.ReadCompletionState` | 2 | 0.024402 | 0.012201 | 0.012419 |
| `GameState.RequiredFiles` | 2 | 0.021404 | 0.010702 | 0.013366 |
| `GameState.SoulStateConsistency` | 2 | 0.019368 | 0.009684 | 0.011054 |
| `Preparation.ValidateTreatmentPublicationShell` | 1 | 0.013415 | 0.013415 | 0.013415 |
| `JointCommonAuthority.ProjectCandidateBounds` | 2 | 0.009443 | 0.004721 | 0.004885 |
| `Preparation.ConstructContinuationAuthority` | 1 | 0.008998 | 0.008998 | 0.008998 |
| `MechanicsPublish.ReadCompletionBeforeImageAsync` | 1 | 0.006792 | 0.006792 | 0.006792 |
| `MechanicsPublish.ValidatePublishedOwnerTransitionsAsync` | 1 | 0.001927 | 0.001927 | 0.001927 |
| `MechanicsPublish.ValidateItemFinalBaselineAsync` | 1 | 0.001835 | 0.001835 | 0.001835 |
| `Preparation.ReadPublishedInterface` | 1 | 0.001600 | 0.001600 | 0.001600 |
| `GameState.LifeEvaluationRewardCycle` | 2 | 0.001495 | 0.000748 | 0.001442 |
| `JointResources.CompareCompanionProjection` | 13 | 0.001337 | 0.000103 | 0.001018 |
| `GameState.AfterlifeActiveThreatPreTurnContinuity` | 2 | 0.001181 | 0.000591 | 0.001167 |
| `Preparation.PrepareSeverityRematerialization` | 1 | 0.001179 | 0.001179 | 0.001179 |
| `GameState.AfterlifeGlobalFlagPreTurnContinuity` | 2 | 0.001111 | 0.000556 | 0.001095 |
| `GameState.ShiningTreasuryClientOwnedState` | 2 | 0.001005 | 0.000502 | 0.000948 |
| `JointCalculation.RetainArtifacts` | 1 | 0.000163 | 0.000163 | 0.000163 |

## Начатые интервалы без записи конца

Внешний → внутренний. Длительность завершения не измерена; родительский лимит 600 с не приписан отдельному методу.

| Участок кода | Номер начала | Начало по часам коллектора, с | Наблюдаемое окно до последней обычной записи, с |
| --- | ---: | ---: | ---: |
| `Resumer.FirstCurrentOwnerCall` | 1 | 7.046678 | 589.700179 |
| `JointStory.TryReconcileMortalJointStoryTurnAsync` | 2382 | 419.151444 | 177.595413 |
| `JointStory.FinalMaterializationLeaseAndCleanup` | 2497 | 533.171353 | 63.575504 |
| `JointStory.ValidateStoryProofAsync` | 2504 | 596.746857 | 0.000000 |

Наблюдаемое окно — разность двух отметок вывода одних часов, с накладными расходами наблюдателя; это не завершённая длительность метода.

Самый внутренний наблюдавшийся незавершённый участок: **`JointStory.ValidateStoryProofAsync`**.

## Проверка и границы вывода

Идентификатор процесса, последовательность записей и вложенность проверены по сырому stdout фактического дочернего восстановления. Извлечённые потоки побайтно сверены с TRX внутри архива; CRC, уникальность имён и SHA-256 каждого файла архива проверены. 4 DLL сверены родительским отчётом до и после запуска; файлы исходного снимка повторно сверены в сохранённом генераторском архиве.

Сырой архив: [T315-profile-cut0-raw.zip](T315-profile-cut0-raw.zip); SHA-256 `F72B6CFA4CB541B4122B8F9E5A9BC5D88B321B5CD6853BE298F5D66928167E3E`. Полные интервалы, ссылки на строки исходников и индекс архива: [T316-timings.json](T316-timings.json).

EOF/join обоих процессов: True; ошибок завершения контейнеров: 0; отчётов с игровым результатом: 0.

Отдельный существующий канал стадий зафиксировал `resume-activity/enter` (`None`) на 590.110172 с своих часов. Это отдельное наблюдение контрольной точки; его часы имеют другое начало отсчёта. Оно не является игровым PASS.

- Удаление собственного тестового корня: True; отсутствие подтверждено: True.
- Удаление собственного тестового корня: True; отсутствие подтверждено: True.

Это замер одного запуска с наблюдателями. Он показывает стоимость участков, но не разделяет работу CPU, файловой системы и внешних проверок. Оптимизаций и увеличения лимитов в этом блоке нет. Исходные пять Windows-случаев: 0/5 PASS; Linux локально не запускался.
