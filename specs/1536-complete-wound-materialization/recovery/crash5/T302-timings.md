# Именованные замеры восстановления — T302

Один фактический запуск: `20261020-181443-482-33552-aeded56899334cc2a70a5cceb937fe9d-categories`. Результат случая: **Failed**; тест 00:12:30.8351870, весь запуск 00:12:50.1941527.

Исходники: `98376e40dbaa0435654380815d2bffb602f068f4`; сохранённое состояние запуска: `c22733e22fdd412fa954b8f2425299182a9ef85a`. PID восстановления: 21000. Получено 1532 записей без квоты диагностического вывода. Последняя запись по часам коллектора: 597.544106 с.

## Завершённые интервалы

Значения включают вложенные вызовы. Строки нельзя складывать как независимые части общего времени. Конец интервала также записывается при выходе через исключение и сам по себе не означает успешную операцию.

| Участок кода | Завершено вызовов | Сумма, с | Среднее, с | Максимум, с |
| --- | ---: | ---: | ---: | ---: |
| `GameEngine.PublishCurrentMortalJointRepairAsync` | 1 | 390.367971 | 390.367971 | 390.367971 |
| `GameEngine.CreateJointRepairCommonPlanAsync` | 1 | 177.529510 | 177.529510 | 177.529510 |
| `JointCompletion.ValidatePostPublicationAsync` | 2 | 153.962349 | 76.981175 | 81.923368 |
| `JointStory.ValidateAcceptedTurnOutcomeWithRepairLoopAsync` | 1 | 92.725142 | 92.725142 | 92.725142 |
| `JointCompletion.TryReadCompletedContinuationAsync` | 1 | 89.612942 | 89.612942 | 89.612942 |
| `GameEngine.PrevalidateJointRepairPublicationTransactionAsync` | 1 | 81.854685 | 81.854685 | 81.854685 |
| `Continuation.ValidateCurrentAsync` | 54 | 73.565670 | 1.362327 | 2.535869 |
| `JointCompletion.ValidateGameStateAsync` | 2 | 64.196187 | 32.098094 | 32.978038 |
| `GameEngine.NormalizeAndValidateJointRepairAsync` | 1 | 56.421826 | 56.421826 | 56.421826 |
| `JointCompletion.FinalProgressionLeaseAndProjection` | 2 | 54.402799 | 27.201400 | 28.689353 |
| `Continuation.CompareBeforeImages` | 54 | 36.971607 | 0.684659 | 1.265526 |
| `Checkpoint.CaptureCoreAsync` | 20 | 35.902162 | 1.795108 | 6.857220 |
| `JointCompletion.CommitProgressionPhaseAsync` | 2 | 24.847612 | 12.423806 | 14.041087 |
| `GameEngine.CreateJointRepairResourcePublicationAsync` | 1 | 22.675299 | 22.675299 | 22.675299 |
| `Baselines.ReadPublicationBaselines` | 55 | 20.652746 | 0.375504 | 0.696667 |
| `Continuation.ReadPublicationBaselines` | 54 | 20.323489 | 0.376361 | 0.696727 |
| `GameEngine.CreateJointRepairCommonBaselineAsync` | 1 | 19.601519 | 19.601519 | 19.601519 |
| `JointCompletion.WriteOrVerifyProjectionAsync` | 2 | 18.577918 | 9.288959 | 9.343456 |
| `Continuation.ReadSnapshot` | 54 | 15.025856 | 0.278257 | 0.532745 |
| `Correction.RevalidateCorrectionAsync` | 23 | 14.215779 | 0.618077 | 0.908907 |
| `Correction.ComposeCorrectedQteResponse` | 23 | 12.595790 | 0.547643 | 0.832809 |
| `Checkpoint.ComposeRejectedQteResponse` | 20 | 11.120063 | 0.556003 | 0.767116 |
| `Checkpoint.ExportCurrentTreatmentAuthority` | 20 | 10.880295 | 0.544015 | 2.993847 |
| `GameEngine.CreateMechanicalAdmissionAsync` | 1 | 10.276577 | 10.276577 | 10.276577 |
| `JointStory.InitialInspectionLease` | 2 | 7.829284 | 3.914642 | 7.175340 |
| `Checkpoint.ReadSnapshot` | 20 | 6.394886 | 0.319744 | 0.974673 |
| `JointCompletion.ReadObservedProgressionAsync` | 2 | 5.465661 | 2.732831 | 2.960730 |
| `JointCompletion.ReadCurrentProgressionAsync` | 2 | 5.445814 | 2.722907 | 2.852652 |
| `JointCompletion.ValidateAppliedProgressionAsync` | 2 | 5.210336 | 2.605168 | 2.666084 |
| `JointCompletion.ValidateCurrentCompletedProofAsync` | 2 | 4.984930 | 2.492465 | 2.770795 |
| `JointCompletion.ReadInterfaceAtLateSlotAsync` | 2 | 4.892690 | 2.446345 | 2.515209 |
| `JointCompletion.ValidateCurrentJointCompletionAsync` | 2 | 4.678378 | 2.339189 | 2.614816 |
| `GameEngine.CreateJointRepairPreparationAsync` | 1 | 4.331458 | 4.331458 | 4.331458 |
| `Preparation.CreateJointRepairPreparationAsync` | 1 | 4.327263 | 4.327263 | 4.327263 |
| `JointStory.InspectCurrentAsync` | 2 | 3.030641 | 1.515321 | 2.536817 |
| `JointStory.AcceptedOutcomeCheckpoint` | 1 | 2.738011 | 2.738011 | 2.738011 |
| `GameEngine.InspectOrdinaryMortalJointRepairAsync` | 1 | 2.665472 | 2.665472 | 2.665472 |
| `Checkpoint.CreateGuaranteedAttempt` | 20 | 2.550079 | 0.127504 | 0.225882 |
| `JointStory.ReadOriginalProgressionPhaseAsync` | 1 | 2.417930 | 2.417930 | 2.417930 |
| `JointCompletion.ValidateReasoningAsync` | 2 | 2.172645 | 1.086322 | 1.114982 |
| `GameEngine.ReadJointCompletedWitnessAsync` | 1 | 2.157894 | 2.157894 | 2.157894 |
| `GameEngine.GetOrBuildFinalValidatedAsync` | 1 | 1.809538 | 1.809538 | 1.809538 |
| `GameEngine.GetOrBuildEffectValidatedAsync` | 1 | 1.725489 | 1.725489 | 1.725489 |
| `GameEngine.CreateJointRepairAnchors` | 1 | 1.573488 | 1.573488 | 1.573488 |
| `GameEngine.GetOrBuildPreparedValidatedAsync` | 1 | 1.504742 | 1.504742 | 1.504742 |
| `Preparation.ReconstructAsync` | 1 | 1.504132 | 1.504132 | 1.504132 |
| `GameEngine.ReadCurrentJointRejectedCaptureAsync` | 1 | 1.415279 | 1.415279 | 1.415279 |
| `GameEngine.ReserveMortalJointRepairPublicationAsync` | 1 | 1.356262 | 1.356262 | 1.356262 |
| `Preparation.ValidateContinuation` | 1 | 1.194330 | 1.194330 | 1.194330 |
| `Checkpoint.RestorePersistedTreatmentRequests` | 20 | 1.112400 | 0.055620 | 0.175159 |
| `GameEngine.CaptureMortalJointProgressionSourceAsync` | 1 | 1.018439 | 1.018439 | 1.018439 |
| `JointCompletion.ValidatePostPublicationAuthorityAsync` | 1 | 0.853050 | 0.853050 | 0.853050 |
| `JointCompletion.BindPublishedOutputAsync` | 1 | 0.720020 | 0.720020 | 0.720020 |
| `JointStory.TryReconcileMortalJointStoryTurnAsync` | 1 | 0.654324 | 0.654324 | 0.654324 |
| `GameEngine.Intake.ValidateCurrentAsync` | 1 | 0.643238 | 0.643238 | 0.643238 |
| `Preparation.CaptureBeforeImages` | 1 | 0.602349 | 0.602349 | 0.602349 |
| `Preparation.RevalidateCorrectionAsync` | 1 | 0.587812 | 0.587812 | 0.587812 |
| `Continuation.InspectConfirmedHeldClaim` | 54 | 0.452381 | 0.008377 | 0.019908 |
| `JointCompletion.ValidateNarrativeReadOnlyAsync` | 2 | 0.414033 | 0.207016 | 0.207975 |
| `GameEngine.CaptureCurrentHotCustodyAsync` | 1 | 0.373012 | 0.373012 | 0.373012 |
| `Correction.ReadPending` | 23 | 0.364317 | 0.015840 | 0.027812 |
| `Correction.ReadDraft` | 23 | 0.341246 | 0.014837 | 0.027663 |
| `Correction.ReadNarrative` | 23 | 0.338725 | 0.014727 | 0.024345 |
| `Correction.ReadCommand` | 23 | 0.334522 | 0.014544 | 0.025611 |
| `Preparation.ReadPublicationBaselines` | 1 | 0.332012 | 0.332012 | 0.332012 |
| `Checkpoint.ReadCommand` | 20 | 0.321671 | 0.016084 | 0.052521 |
| `Checkpoint.ReadAuthorityBytes` | 20 | 0.316470 | 0.015823 | 0.031427 |
| `Checkpoint.ReadManifestBytes` | 20 | 0.295568 | 0.014778 | 0.021061 |
| `JointCompletion.ValidateQteOfferAsync` | 2 | 0.182145 | 0.091073 | 0.093177 |
| `JointCompletion.ValidateProgressionScheduleAsync` | 1 | 0.109138 | 0.109138 | 0.109138 |
| `Checkpoint.ReadSessionGeneration` | 20 | 0.085552 | 0.004278 | 0.008811 |
| `JointCompletion.ValidateCombatMaterializationAsync` | 2 | 0.063588 | 0.031794 | 0.038512 |
| `Preparation.PrepareTreatmentOutcome` | 1 | 0.038618 | 0.038618 | 0.038618 |
| `Checkpoint.FinalizeResource` | 20 | 0.038338 | 0.001917 | 0.010566 |
| `JointCompletion.ValidateLevelUpMaterializationAsync` | 2 | 0.035528 | 0.017764 | 0.018974 |
| `JointCompletion.ValidateSpecialActionOutcomesAsync` | 2 | 0.033506 | 0.016753 | 0.022946 |
| `JointCompletion.ValidateMemoryLegacyAsync` | 2 | 0.024511 | 0.012256 | 0.013302 |
| `Preparation.ValidateInput` | 1 | 0.023169 | 0.023169 | 0.023169 |
| `JointStory.ReadCompletionState` | 2 | 0.018271 | 0.009135 | 0.012195 |
| `Preparation.ValidateTreatmentPublicationShell` | 1 | 0.014435 | 0.014435 | 0.014435 |
| `Preparation.ConstructContinuationAuthority` | 1 | 0.006923 | 0.006923 | 0.006923 |
| `Preparation.PrepareSeverityRematerialization` | 1 | 0.001375 | 0.001375 | 0.001375 |
| `Preparation.ReadPublishedInterface` | 1 | 0.001270 | 0.001270 | 0.001270 |

## Начатые интервалы без записи конца

Внешний → внутренний. Длительность завершения не измерена; родительский лимит 600 с не приписан отдельному методу.

| Участок кода | Номер начала | Начало по часам коллектора, с | Наблюдаемое окно до последней обычной записи, с |
| --- | ---: | ---: | ---: |
| `Resumer.FirstCurrentOwnerCall` | 1 | 9.150944 | 588.393163 |
| `JointStory.TryReconcileMortalJointStoryTurnAsync` | 1480 | 494.761480 | 102.782626 |
| `JointStory.FinalMaterializationLeaseAndCleanup` | 1531 | 597.400117 | 0.143990 |
| `JointStory.ReadAppliedProgressionPhaseAsync` | 1532 | 597.544106 | 0.000000 |

Наблюдаемое окно — разность двух отметок вывода одних часов, с накладными расходами наблюдателя; это не завершённая длительность метода.

Самый внутренний наблюдавшийся незавершённый участок: **`JointStory.ReadAppliedProgressionPhaseAsync`**.

## Проверка и границы вывода

Идентификатор процесса, последовательность записей и вложенность проверены по сырому stdout фактического дочернего восстановления. Извлечённые потоки побайтно сверены с TRX внутри архива; CRC, уникальность имён и SHA-256 каждого файла архива проверены. Три DLL сверены родительским отчётом до и после запуска; файлы исходного снимка повторно сверены в сохранённом генераторском архиве.

Сырой архив: [T301-profile-cut0-raw.zip](T301-profile-cut0-raw.zip); SHA-256 `D2DA3706842E0BDD780F59BB62AC6A0FF7CFCB16C56BADF7A2185978AA15A960`. Полные интервалы, ссылки на строки исходников и индекс архива: [T302-timings.json](T302-timings.json).

EOF/join обоих процессов: True; ошибок завершения контейнеров: 0; отчётов с игровым результатом: 0.

Отдельный существующий канал стадий зафиксировал `finalization-after/return` (`AcceptedOutcomeValidatedBeforeMaterialization`) на 597.401370 с своих часов. Это отдельное наблюдение контрольной точки; его часы имеют другое начало отсчёта. Оно не является игровым PASS.

- Удаление собственного тестового корня: True; отсутствие подтверждено: True.
- Удаление собственного тестового корня: True; отсутствие подтверждено: True.

Это замер одного запуска с наблюдателями. Он показывает стоимость участков, но не разделяет работу CPU, файловой системы и внешних проверок. Оптимизаций и увеличения лимитов в этом блоке нет. Исходные пять Windows-случаев: 0/5 PASS; Linux локально не запускался.
