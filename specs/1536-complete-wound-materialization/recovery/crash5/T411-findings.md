# T411: точные дорогие участки воспроизведённого Windows600 таймаута

Исходный Fact `JointInterfaceFixtureBackedWindows_Cut0_Prepared` снова упал с `TimeoutException` в реальном resumer на `C:/Temp`. Лимиты не менялись: child600, shared820, category15min. Сам Fact занял759.3070718s; команда781.8702836s. Это исходный сценарий с реальным выходом producer73 и отдельным resumer, не компонентная аналогия. Успешного завершения восстановления этот запуск не доказывает.

Основной дорогой путь — многократная свежая проверка одних и тех же предков каталогов при допуске и обычных чтениях: `ValidateDirectories/ObserveNamespace → Probe → File.GetAttributes`. Здесь зарегистрировано3,840,627 завершённых вызовов `GetAttributes` на200.8635681s; максимум одного10.8213ms. Это накопление миллионов коротких обращений. Вместе с двумя видами `Refresh` измеренная стоимость этих метаданных249.5934688s. Следующая отдельная причина тяжести подробного диагностического запуска — синхронное создание/сериализация и запись26.3млн timing-сообщений:189.2974953s. Это число относится к текущей инструментированной репродукции; его нельзя переносить на старый T365 с другим объёмом журнала.

## Измеренные нижние границы операций

| Граница | Завершённых вызовов | Сумма, s | Максимум одного, ms |
|---|---:|---:|---:|
| `File.GetAttributes` | 3,840,627 | 200.863568 | 10.8213 |
| `DirectoryInfo.Refresh` + кэшированные `Exists/Attributes` | 766,076 | 39.862525 | 7.9096 |
| `FileInfo.Refresh` + кэшированные `Exists/Attributes` | 182,960 | 8.867376 | 0.6406 |
| Запись канонического JSON (блок сериализатора) | 177,426 | 14.081000 | 5.0519 |
| `GetFinalPathNameByHandle` | 108,554 | 6.756804 | 0.5481 |
| Открытие `FileStream` | 42,595 | 5.612636 | 1.7393 |
| `SHA256.HashData` | 643,198 | 3.516998 | 2.1223 |
| `CreateFileW`, combined directory: существующие аргументы/result/error handling | 17,668 | 2.146478 | 0.6440 |
| `CreateFileW`, namespace entry: существующие аргументы/result/error handling | 8,178 | 1.033057 | 0.6474 |
| `CreateFileW`, directory authority: существующие аргументы/result/error handling | 8,178 | 0.964165 | 0.5650 |
| `GetFileInformationByHandleEx`, StandardInfo | 108,554 | 0.951586 | 0.2847 |
| `GetFileInformationByHandleEx`, AttributeTag | 20,620 | 0.108449 | 0.1869 |
| `Directory.EnumerateFileSystemEntries(...).Any()` | 2,952 | 0.563055 | 1.0708 |

`Refresh` включает следующие чтения кэшированных полей; JSON — блок стандартного сериализатора. CreateFile-границы включают исходную обработку аргументов/результата/ошибки и не являются ETW-замером чистого syscall. Fallback в данном запуске не выполнялся: события для него не придуманы. Остальные native-проверки оказались небольшими относительно200s `GetAttributes`; вложенные totals к этой таблице не прибавляются.

## Где возникают повторения

1. `TrustedLocalFileScope.cs:351–364` строит и проходит всю цепочку от корня тома; `Probe`, строка409, вызывает `File.GetAttributes` для каждого компонента. `ValidateFile`, строка136, заново проверяет предков; `ObserveNamespace`, строка258, также проходит цепочку.
2. `GmSessionRunPersistence.cs:177` в каждом `GmMainOwnerGuard.Validate()` создаёт `new TrustedLocalFileScope([Root])`, который проверяет корень, затем сразу выполняет `ValidateFile(_path,false)`, ещё раз обходящий общий префикс. Зарегистрированные вызовы из конструктора в этой ветке:310,440 /16.5409928s; из `ValidateFile`:496,704 /25.7504668s. Это две части одной повторной проверки, не добавка поверх общей таблицы.
3. `FileSystemManager.MainRunFence.cs:277–288` делает guard-check, проверку отсутствия worker inventory и чтение run metadata. Вторая ветка (`:540`) создаёт собственный scope и вызывает `ObserveNamespace`; третья (`GmSessionRunPersistence.cs:29–30`) снова создаёт scope и вызывает `ObserveNamespace` уже для run metadata. Поэтому общий префикс дерева проверяется заново несколькими отдельными проходами одного допуска.

Распределение **только `GetAttributes`** по веткам допуска (взаимоисключающие строки):

| Ветка | Вызовов | Сумма, s |
|---|---:|---:|
| `<outside-MainAdmission.Guard>` | 1,531,114 | 77.620056 |
| `MainAdmission.Validate.GuardCheck` | 807,144 | 42.291460 |
| `MainAdmission.Validate.WorkerInventory` | 757,325 | 41.124202 |
| `MainAdmission.Validate.PersistenceRead` | 745,044 | 39.827850 |

Всего внутри `MainAdmission.Validate.Guard`:2,309,513 /123.2435124s; вне него1,531,114 /77.6200557s. Это части200.8635681s, а не отдельные дополнительные затраты. Есть ещё1,023 `GuardCheck` без дочерних таймеров на0.7631268s: они сохранены как небольшая составная граница. Поэтому приведённые API-счётчики означают **зарегистрированные вызовы**, не доказанный полный process-wide native census.

Какие семантические вызывающие методы создают чтения и эти `GetAttributes` (все группы, вне-read ветка сохранена в exact-summary):

| Вызывающий метод обычного чтения | `GetAttributes` | Сумма, s |
|---|---:|---:|
| `JointMechanical.RequireCurrentOriginalEvidence` | 405,020 | 19.692987 |
| `JointMechanical.CompareBeforeImages` | 301,840 | 16.042649 |
| `Continuation.CompareBeforeImages` | 259,079 | 14.090111 |
| `Baselines.ReadPublicationBaselines` | 152,208 | 8.646550 |
| `JointProof.ValidateFootprintAsync` | 133,252 | 7.446547 |
| `Correction.ComposeCorrectedQteResponse` | 122,061 | 6.108065 |
| `Checkpoint.ComposeRejectedQteResponse` | 106,140 | 5.282159 |
| `Continuation.ReadSnapshot` | 112,791 | 5.172868 |
| `JointProof.ValidateProgressionAsync` | 97,621 | 5.096518 |
| `JointMechanical.ReadAndCheckSnapshot` | 96,285 | 4.718028 |
| `JointProof.ReadOriginalProjection` | 77,028 | 3.881385 |
| `JointProof.ReadSourceSnapshot` | 71,148 | 3.668429 |
| `Checkpoint.ExportCurrentTreatmentAuthority` | 69,588 | 3.611053 |
| `JointProofResources.ComposeOwnersAsync` | 54,544 | 2.952959 |
| `Checkpoint.ReadSnapshot` | 55,020 | 2.775489 |
| `JointPhase.ValidateProgressionDataAsync` | 50,582 | 2.482594 |
| `Resumer.FirstCurrentOwnerCall` | 41,907 | 2.379651 |
| `GameEngine.CreateMechanicalAdmissionAsync` | 46,198 | 2.187402 |
| `JointPhase.ReadCompletedProofAsync` | 37,002 | 1.882245 |
| `JointQte.ReadOriginal` | 38,514 | 1.818545 |
| `JointProgression.ReadCurrentSnapshot` | 31,395 | 1.541029 |
| `JointInventory.CompareBeforeImages` | 26,727 | 1.458121 |
| `JointProof.ValidateInactiveTailAsync` | 23,044 | 1.230455 |
| `JointProof.ValidateOutputCarriersAsync` | 20,160 | 1.157990 |
| `CanonicalExists.FileExists` | 19,467 | 0.976999 |
| `JointPublish.ValidateTransactionReadbackAsync` | 11,370 | 0.716669 |
| `JointProgression.ValidateFrozenInputsAsync` | 11,830 | 0.634373 |
| `GameState.RealmSegregation` | 10,794 | 0.609902 |
| `JointCompletion.WriteOrVerifyProjectionAsync` | 12,029 | 0.587417 |
| `JointStory.InspectCurrentAsync` | 11,155 | 0.547642 |
| `JointProof.ValidatePublicOutputsBeforeAsync` | 9,324 | 0.515542 |
| `JointProof.ValidatePublicOutputsAfterAsync` | 8,991 | 0.487810 |
| `JointProofResources.ReadStateHistoryAsync` | 8,064 | 0.468992 |
| `JointProof.ValidateSourceBindingAsync` | 8,064 | 0.457289 |
| `JointCompletion.TryReadCompletedContinuationAsync` | 9,435 | 0.445201 |
| `Preparation.CaptureBeforeImages` | 6,319 | 0.427939 |
| `JointInventory.ReadAndCheckSnapshot` | 8,253 | 0.414868 |
| `GameEngine.PrevalidateJointRepairPublicationTransactionAsync` | 9,053 | 0.391694 |
| `GameEngine.InspectOrdinaryMortalJointRepairAsync` | 6,738 | 0.373037 |
| `MechanicsPublish.CreateQtePublicationAsync` | 6,877 | 0.362701 |
| `JointStorySource.BindContinuationAsync` | 6,792 | 0.348703 |
| `GameState.ClientOwnedControlFiles` | 4,836 | 0.281404 |
| `MechanicsPublish.ValidateAgreementAsync` | 4,626 | 0.274723 |
| `GameState.CrossReferences` | 4,788 | 0.274298 |
| `GameState.MetaMiscStateFiles` | 4,896 | 0.261868 |
| `GameEngine.CaptureMortalJointProgressionSourceAsync` | 5,322 | 0.247007 |
| `JointStory.ValidateAcceptedTurnOutcomeWithRepairLoopAsync` | 5,799 | 0.236050 |
| `JointPublish.ValidateCanonicalItemAsync` | 4,043 | 0.222408 |
| `JointProofResources.ReadDefinitionsAsync` | 4,032 | 0.221950 |
| `JointProof.ValidateInterfaceAsync` | 3,948 | 0.221210 |
| `JointProof.RecoverStableMarkerAsync` | 4,032 | 0.220889 |
| `JointProof.ValidateTreatmentHistoryAsync` | 4,032 | 0.205091 |
| `Correction.ReadPending` | 3,312 | 0.204104 |
| `JointProgression.CompareOriginalGuards` | 3,744 | 0.193410 |
| `JointProof.ValidateFinalMarkerAsync` | 3,888 | 0.192876 |
| `GameEngine.CaptureCurrentHotCustodyAsync` | 2,988 | 0.190311 |
| `Correction.ReadDraft` | 3,243 | 0.189299 |
| `Correction.ReadCommand` | 3,312 | 0.184430 |
| `Checkpoint.CreateGuaranteedAttempt` | 2,880 | 0.182216 |
| `Correction.ReadNarrative` | 3,243 | 0.181995 |
| `JointCompletion.CommitProgressionPhaseAsync` | 3,323 | 0.181076 |
| `Checkpoint.ReadManifestBytes` | 2,880 | 0.173453 |
| `MechanicsPublish.CaptureAgreementBeforeImagesAsync` | 2,983 | 0.173098 |
| `Checkpoint.CaptureCoreAsync` | 2,880 | 0.170893 |
| `Checkpoint.ReadAuthorityBytes` | 2,880 | 0.163600 |
| `JointPublish.ValidateCanonicalEffectAsync` | 2,668 | 0.157186 |
| `Checkpoint.ReadCommand` | 2,880 | 0.156011 |
| `JointCompletion.ValidateCurrentJointCompletionAsync` | 2,799 | 0.155632 |
| `JointPublish.ValidateCanonicalResourceAsync` | 2,539 | 0.149953 |
| `GameEngine.ReadJointCompletedWitnessAsync` | 2,643 | 0.146714 |
| `JointCompletion.ReadInterfaceAtLateSlotAsync` | 2,643 | 0.138293 |
| `JointStorySource.ReadOriginal` | 2,373 | 0.136638 |
| `JointQte.ReconstructAsync` | 2,016 | 0.135531 |
| `JointStory.InitialInspectionLease` | 2,835 | 0.133415 |
| `MechanicsPublish.PrepareBeforeImages` | 2,065 | 0.133178 |
| `JointProof.ValidateReceiptPartition` | 2,772 | 0.129722 |
| `JointCompletion.ValidatePostPublicationAsync` | 2,643 | 0.123817 |
| `JointPhase.ReadMarkerAsync` | 2,016 | 0.119826 |
| `JointInput.ComposeReadSnapshot` | 2,373 | 0.112961 |
| `GameEngine.PublishCurrentMortalJointRepairAsync` | 2,284 | 0.112958 |
| `JointPhase.RecheckMarkerAsync` | 2,016 | 0.108311 |
| `JointPhase.VerifyTupleAndNoContinuationAsync` | 2,184 | 0.106427 |
| `LocalPublication.Core.ValidatePreparedNamespace` | 1,719 | 0.106271 |
| `MechanicsPublish.ReadBackBatchAsync` | 1,935 | 0.105370 |
| `JointInput.CaptureReadSnapshot` | 2,373 | 0.101748 |
| `JointProof.ValidateConsumedWaveAsync` | 1,456 | 0.084209 |
| `JointCompletion.ValidateReasoningAsync` | 1,542 | 0.083377 |
| `GameEngine.CreateJointRepairCommonBaselineAsync` | 1,816 | 0.078398 |
| `GameState.PlayerStateFiles` | 1,092 | 0.065524 |
| `MechanicsPublish.ValidatePublishedWoundsAsync` | 1,023 | 0.062968 |
| `GameState.AcceptedTurnActorMaterializationCompleteness` | 1,176 | 0.062797 |
| `JointPublish.ValidateCanonicalWoundAsync` | 1,060 | 0.061642 |
| `JointPublish.ValidateCanonicalLocationAsync` | 977 | 0.053967 |
| `GameEngine.Intake.ValidateCurrentAsync` | 858 | 0.051049 |
| `GameState.NpcStateFiles` | 930 | 0.050990 |
| `GameState.AcceptedTurnItemMaterializationCompleteness` | 996 | 0.050853 |
| `GameState.WorldQuestCombatFactionStateFiles` | 894 | 0.046856 |
| `GameState.RequiredFields` | 804 | 0.043144 |
| `GameState.AcceptedTurnEffectMaterializationCompleteness` | 738 | 0.042363 |
| `JointPublish.ValidateOutputAuthorityAsync` | 618 | 0.037760 |
| `MechanicsPublish.ValidatePublishedResourcesAsync` | 591 | 0.037541 |
| `JointPhase.ReadDecisionAsync` | 714 | 0.035502 |
| `JointStorySource.RecheckSourceImagesAsync` | 429 | 0.031060 |
| `MechanicsPublish.ValidateAuthorityBeforeImagesAsync` | 288 | 0.028063 |
| `JointCompletion.ValidateNarrativeReadOnlyAsync` | 492 | 0.025237 |
| `GameState.AcceptedTurnLocationMaterializationCompleteness` | 312 | 0.017967 |
| `GameState.AcceptedTurnWoundMaterializationCompleteness` | 261 | 0.017808 |
| `GameEngine.ReadCurrentJointRejectedCaptureAsync` | 288 | 0.016865 |
| `JointCompletion.ValidatePostPublicationAuthorityAsync` | 151 | 0.014227 |
| `GameState.TrainingShowcases` | 255 | 0.013803 |
| `GameState.NoLifeEvaluationRewardsOnTriggerTurn` | 171 | 0.013471 |
| `GameState.SkillContractConsistency` | 234 | 0.012259 |
| `JointStorySource.ReadSourceObjectsAsync` | 285 | 0.012245 |
| `Preparation.ReconstructAsync` | 144 | 0.009212 |
| `JointStorySource.VerifyOtherHistoriesAsync` | 144 | 0.009187 |
| `JointStory.ReadCompletionState` | 196 | 0.008616 |
| `JointCommon.CandidateAdmissionGate` | 144 | 0.008239 |
| `JointPublish.CandidateAdmissionGate` | 144 | 0.007936 |
| `GameEngine.CreateJointRepairAnchors` | 144 | 0.007805 |
| `JointStorySource.ReadManifestAsync` | 144 | 0.007554 |
| `GameState.AcceptedTurnFactionMaterializationCompleteness` | 120 | 0.006455 |
| `JointStorySource.ReadReferencesAsync` | 144 | 0.006182 |
| `JointStorySource.ClassifyHistoryAsync` | 141 | 0.005549 |
| `JointCompletion.ValidateProgressionScheduleAsync` | 93 | 0.005276 |
| `Preparation.CreateJointRepairPreparationAsync` | 104 | 0.005261 |
| `GameState.LoreBootstrapRequiredFiles` | 66 | 0.004631 |
| `JointCompletion.ValidateLevelUpMaterializationAsync` | 63 | 0.004456 |
| `JointCompletion.ValidateCombatMaterializationAsync` | 90 | 0.003904 |
| `GameState.SourceOfLightCapstoneGlobalState` | 63 | 0.003052 |
| `GameState.MortalBootstrapContentAnchors` | 63 | 0.002934 |
| `GameState.GuardianResonancePowerEvents` | 30 | 0.002270 |
| `JointCompletion.ValidateMemoryLegacyAsync` | 33 | 0.002228 |
| `GameState.SoulStateConsistency` | 33 | 0.002225 |
| `JointCompletion.ValidateQteOfferAsync` | 28 | 0.002074 |
| `MechanicsPublish.ReadCompletionBeforeImageAsync` | 52 | 0.002028 |
| `JointCompletion.BindPublishedOutputAsync` | 30 | 0.001880 |
| `GameState.AfterlifeSpiritualConflictState` | 30 | 0.001322 |
| `JointCompletion.ValidateSpecialActionOutcomesAsync` | 30 | 0.001320 |
| `GameState.ShiningLeadershipHeadReferences` | 30 | 0.001310 |
| `GameState.MortalBootstrapPlayerVisibleNames` | 33 | 0.001279 |

Например, `Services/MortalQteDecisionPublication.MortalJointRepair.cs:237–240` при каждой проверке proof заново читает каждый `JointFootprintPaths`; строки222–223 и257–258 снова читают source snapshot и original projection. Каждый такой вызов до чтения данных оплачивает описанную цепочку допуска и проверки пути. Полные контексты вызова сохранены: ни один path aggregate не отфильтрован из исходной полной таблицы.

## Учитывается почти весь600-секундный интервал

Из26,313,374 **полных** событий получены13,156,679 закрытых интервалов,463 имени с завершёнными вызовами и471,414 таких invocation-path groups; ещё16 интервалов открыты при завершении процесса. Два независимых прохода совпали по всем завершённым named/path count, inclusive/max/leaf sums и direct-child residual, а не только по первым строкам.

* Взаимоисключающее self-time закрытых интервалов после вычитания измеренного observer build/write: **407.7905606s**, включая299.7257021s структурных листьев и108.0648585s остальных именных self-участков. Это оценка: остатки не приравниваются к CPU или чистому syscall.
* Отдельно синхронное создание/сериализация timing JSON: **83.0734231s**, `_output.WriteLine(record)`: **106.2240722s** (`GameEngineTurnLifecycleTests.JointOperationTimings.cs:51–75`). Сумма**189.2974953s**. Gate wait1.2548175s может пересекаться с работой другого наблюдателя и отдельно к wall time не прибавляется.
* Оценка учтённого времени: **597.0880559s** против**599.1168283s** между первой и последней полной записью. Разность**2.0287724s** не приписана произвольно диску/API: здесь открытые родители, время вне активного таймера, синхронизация и bookkeeping/погрешности привязки clocks. Последняя незавершённая запись и её observer имеют неизвестную конечную стоимость.

Наибольшие оставшиеся **собственные** участки после вычитания прямых детей и измеренной диагностики:

| Именной участок | Закрытых вызовов | Оценка self, s |
|---|---:|---:|
| `GameEngine.PrevalidateJointRepairPublicationTransactionAsync` | 1 | 5.796880 |
| `JointCommon.SealRegistryCandidate` | 1 | 5.726711 |
| `JointPublish.ValidateReceiptLineage` | 1 | 4.832462 |
| `TrustedScope.ValidateDirectories` | 431,966 | 4.747251 |
| `TreatmentOutcome.AgreesWith` | 7,871 | 4.284251 |
| `TrustedScope.ObserveNamespace` | 125,199 | 4.219963 |
| `CanonicalPath.WindowsMetadata` | 182,972 | 4.114645 |
| `TrustedScope.ValidateFile` | 196,242 | 4.021309 |
| `TrustedScope.Construct` | 235,695 | 3.620673 |
| `TreatmentResources.Finalize` | 7,820 | 3.558352 |
| `JointCommon.CandidateAdmissionGate` | 1 | 3.191096 |
| `JointPublish.CandidateAdmissionGate` | 1 | 3.058345 |

Таким образом, скрытого остатка на сотни секунд не оставлено. Полная таблица всех463 имён — `T411-all-named-costs.tsv`; точные агрегаты/доли — `T411-original-cut0-exact-summary.json`; все471,414 завершённых путей и исходные records сохранены в lossless raw/analysis packages. Текущий scope — диагностика причины, не уже выполненное ускорение и не PASS исходного теста.

## Целостность и пределы вывода

Raw ZIP665,274,231bytes, SHA256`6D79BFA61E73E0D82FE3F5D91905C4ACB1C69C315E5AAF31C7979349D315BC03`. Все40 payloads полностью проверены SHA256/CRC. Полученный resumer stdout9,608,597,865bytes сохранён целиком. Семь транспортных частей для Git восстанавливают **те же байты**; это транспорт, не квота диагностики. Никаких лимитов строк/размера/хранения не вводилось.

Последняя строка —256-byte незавершённый BEGIN, SHA256`A3703C5FB13344E0D172E94D2D119EE5741BB5DFEE4F1F60C0424F483036A94C`, partial Sequence26,313,375. Полные события заканчиваются26,313,374. Все байты/текст/base64 сохранены. BEGIN испускается **до** запуска stopwatch и вызова API: он не доказывает, что следующий `GetAttributes` начал выполняться или завис. Последний реальный открытый путь: story postpublication validation → progression proof → async read → lease admission → worker inventory → ObserveNamespace. Cleanup capture не достигнут, поэтому прошлой begin-observer поправки здесь нет (0).

Producer47612exit73; resumer45740 остановлен после timeout, exit-1; оба имеют разные Windows birth identities, EOF/join/полное сохранение streams, без cleanup failure. Новые принадлежащие этому запуску game/class roots удалены исходным cleanup. Финальные turn43/history/interface/inert-repeat success assertions не достигнуты и не выданы за PASS. Дополнительный driver post-error восстановления env (null→empty в PowerShell7.6.6/.NET10.0.12) отдельно воспроизведён без игры и не подменяет основную ошибку `TimeoutException`. Исторический PASS-only analyzer, остановившийся до разбора FAIL, сохранён; его заранее заполненные scope-строки не являются выполненными проверками. См. отдельный `T411-original-cut0-timeout-qualification.json`.

C# source`ebf3f5ea17d7a025a6b555c88563b16b53c07a4f`, metadata release6dc6ed39f7f0e57d53ed2ea7afad126fe5b3b293; genuine current source42 original-leaf-v2 с82payloads/two independent loads. До полного сценария XML-build и65 выбранных controls PASS; исходный600/820/15 бюджет, ownership/integrity/refusal/native-error/handle contracts сохранены. Linux не запускался/не изменялся. Дорогие циклы TrustedLocalFileScope и Windows metadata API в сравнении с T365 менялись только добавлением таймеров/документации; GmSessionRunPersistence.cs byte-identical. Другие source/package/observer различия существуют: исторические per-API времена не восстановлены, причинность SSD/HDD, антивируса или облачных ограничений этими данными не установлена.

Конкретная цель следующего исправления — убрать повторную подготовку grants и повторные проходы общих предков **в пределах сохранённых свежих границ проверки**, начиная с трёх веток main admission, и отдельно снизить стоимость кодирования/вывода всех диагностических records без квот. Это кандидаты на проверяемую оптимизацию; нельзя автоматически удалять повторный freshness-check после await, native-handle/permission/namespace/generation/claim проверки или считать чтение снимком файловой системы. В текущем диагностическом блоке production speedup не внедрялся.
