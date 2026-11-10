# Именованные замеры восстановления — T297

Один фактический запуск: `20261020-172518-892-25424-2f8641eaaaf4499d833b81dc6162bb9e-categories`. Результат случая: **Failed**; тест 00:12:37.8268196, весь запуск 00:12:58.7870300.

Исходники: `d1c59346d644016c765b9078ecf2fbb9605b8532`; сохранённое состояние запуска: `0ab1f3c39526d3faa82ec6963efa5f771b0f0ed8`. PID восстановления: 22424. Получено 1025 записей, ограничение вывода достигнуто. Последняя запись по часам коллектора: 250.782182 с.

## Завершённые интервалы

Значения включают вложенные вызовы. Строки нельзя складывать как независимые части общего времени. Конец интервала также записывается при выходе через исключение и сам по себе не означает успешную операцию.

| Участок кода | Завершено вызовов | Сумма, с | Среднее, с | Максимум, с |
| --- | ---: | ---: | ---: | ---: |
| `Continuation.ValidateCurrentAsync` | 38 | 54.671169 | 1.438715 | 1.663945 |
| `Continuation.CompareBeforeImages` | 38 | 28.305819 | 0.744890 | 0.914043 |
| `Checkpoint.CaptureCoreAsync` | 14 | 21.700427 | 1.550030 | 2.548853 |
| `GameEngine.CreateJointRepairResourcePublicationAsync` | 1 | 20.328038 | 20.328038 | 20.328038 |
| `GameEngine.CreateJointRepairCommonBaselineAsync` | 1 | 17.071620 | 17.071620 | 17.071620 |
| `Baselines.ReadPublicationBaselines` | 39 | 15.615688 | 0.400402 | 0.479837 |
| `Continuation.ReadPublicationBaselines` | 38 | 15.211753 | 0.400309 | 0.479873 |
| `GameEngine.CreateMechanicalAdmissionAsync` | 1 | 11.591669 | 11.591669 | 11.591669 |
| `Continuation.ReadSnapshot` | 38 | 10.290675 | 0.270807 | 0.336401 |
| `Correction.RevalidateCorrectionAsync` | 17 | 10.157708 | 0.597512 | 0.703536 |
| `Correction.ComposeCorrectedQteResponse` | 17 | 8.923408 | 0.524906 | 0.645090 |
| `Checkpoint.ComposeRejectedQteResponse` | 14 | 7.411005 | 0.529357 | 0.629476 |
| `Checkpoint.ExportCurrentTreatmentAuthority` | 14 | 6.185895 | 0.441850 | 0.970240 |
| `GameEngine.CreateJointRepairPreparationAsync` | 1 | 4.828156 | 4.828156 | 4.828156 |
| `Preparation.CreateJointRepairPreparationAsync` | 1 | 4.823402 | 4.823402 | 4.823402 |
| `Checkpoint.ReadSnapshot` | 14 | 3.778817 | 0.269915 | 0.305681 |
| `GameEngine.InspectOrdinaryMortalJointRepairAsync` | 1 | 2.951503 | 2.951503 | 2.951503 |
| `GameEngine.GetOrBuildEffectValidatedAsync` | 1 | 2.117938 | 2.117938 | 2.117938 |
| `GameEngine.CreateJointRepairAnchors` | 1 | 1.998112 | 1.998112 | 1.998112 |
| `GameEngine.GetOrBuildFinalValidatedAsync` | 1 | 1.964513 | 1.964513 | 1.964513 |
| `GameEngine.GetOrBuildPreparedValidatedAsync` | 1 | 1.764584 | 1.764584 | 1.764584 |
| `Checkpoint.CreateGuaranteedAttempt` | 14 | 1.652910 | 0.118065 | 0.136200 |
| `Preparation.ValidateContinuation` | 1 | 1.534039 | 1.534039 | 1.534039 |
| `GameEngine.ReserveMortalJointRepairPublicationAsync` | 1 | 1.526652 | 1.526652 | 1.526652 |
| `GameEngine.ReadCurrentJointRejectedCaptureAsync` | 1 | 1.483149 | 1.483149 | 1.483149 |
| `Preparation.ReconstructAsync` | 1 | 1.388008 | 1.388008 | 1.388008 |
| `GameEngine.CaptureMortalJointProgressionSourceAsync` | 1 | 1.211392 | 1.211392 | 1.211392 |
| `Preparation.CaptureBeforeImages` | 1 | 0.759306 | 0.759306 | 0.759306 |
| `GameEngine.Intake.ValidateCurrentAsync` | 1 | 0.733131 | 0.733131 | 0.733131 |
| `Checkpoint.RestorePersistedTreatmentRequests` | 14 | 0.656913 | 0.046922 | 0.055732 |
| `Preparation.RevalidateCorrectionAsync` | 1 | 0.625331 | 0.625331 | 0.625331 |
| `GameEngine.CaptureCurrentHotCustodyAsync` | 1 | 0.429041 | 0.429041 | 0.429041 |
| `Preparation.ReadPublicationBaselines` | 1 | 0.405920 | 0.405920 | 0.405920 |
| `Continuation.InspectConfirmedHeldClaim` | 38 | 0.319722 | 0.008414 | 0.016904 |
| `Correction.ReadDraft` | 17 | 0.273338 | 0.016079 | 0.030133 |
| `Correction.ReadNarrative` | 17 | 0.266775 | 0.015693 | 0.027347 |
| `Correction.ReadPending` | 17 | 0.250633 | 0.014743 | 0.026223 |
| `Correction.ReadCommand` | 17 | 0.240445 | 0.014144 | 0.023759 |
| `Checkpoint.ReadCommand` | 14 | 0.214167 | 0.015298 | 0.027815 |
| `Checkpoint.ReadAuthorityBytes` | 14 | 0.211675 | 0.015120 | 0.021499 |
| `Checkpoint.ReadManifestBytes` | 14 | 0.195432 | 0.013959 | 0.017085 |
| `Checkpoint.ReadSessionGeneration` | 14 | 0.053846 | 0.003846 | 0.006386 |
| `Preparation.PrepareTreatmentOutcome` | 1 | 0.036800 | 0.036800 | 0.036800 |
| `Checkpoint.FinalizeResource` | 14 | 0.025292 | 0.001807 | 0.008280 |
| `Preparation.ValidateInput` | 1 | 0.023339 | 0.023339 | 0.023339 |
| `Preparation.ValidateTreatmentPublicationShell` | 1 | 0.013464 | 0.013464 | 0.013464 |
| `Preparation.ConstructContinuationAuthority` | 1 | 0.010026 | 0.010026 | 0.010026 |
| `Preparation.ReadPublishedInterface` | 1 | 0.003045 | 0.003045 | 0.003045 |
| `Preparation.PrepareSeverityRematerialization` | 1 | 0.001378 | 0.001378 | 0.001378 |

## Начатые интервалы без записи конца

Внешний → внутренний. Длительность завершения не измерена; родительский лимит 600 с не приписан отдельному методу.

| Участок кода | Номер начала | Начало по часам коллектора, с | Наблюдаемое окно до последней обычной записи, с |
| --- | ---: | ---: | ---: |
| `GameEngine.PublishCurrentMortalJointRepairAsync` | 139 | 24.366951 | 223.816955 |
| `GameEngine.CreateJointRepairCommonPlanAsync` | 510 | 95.200848 | 152.983059 |

Наблюдаемое окно — разность двух отметок вывода одних часов, с накладными расходами наблюдателя; это не завершённая длительность метода.

Самый внутренний наблюдавшийся незавершённый участок: **`GameEngine.CreateJointRepairCommonPlanAsync`**.
Лимит вывода достигнут: это последнее наблюдение, а не доказательство участка, активного при тайм-ауте.

## Проверка и границы вывода

Идентификатор процесса, последовательность записей и вложенность проверены по сырому stdout фактического дочернего восстановления. Извлечённые потоки побайтно сверены с TRX внутри архива; CRC, уникальность имён и SHA-256 каждого файла архива проверены. Текущие три DLL и все файлы исходного снимка сверены с сохранённой привязкой.

Сырой архив: [T296-profile-cut0-raw.zip](T296-profile-cut0-raw.zip); SHA-256 `CB77713836602E6B0991F85ED0810254A9D05D975823513B3A17363CA8255AA2`. Полные интервалы, ссылки на строки исходников и индекс архива: [T297-timings.json](T297-timings.json).

EOF/join обоих процессов: True; ошибок завершения контейнеров: 0; отчётов с игровым результатом: 0.

Отдельный существующий канал стадий после исчерпания именованного вывода всё ещё зафиксировал `finalization-after/return` (`AcceptedOutcomeValidatedBeforeMaterialization`) на 593.199386 с своих часов. Это подтверждает более позднюю стадию, но не восстанавливает потерянные длительности методов и не является игровым PASS.

- Удаление собственного тестового корня: True; отсутствие подтверждено: True.
- Удаление собственного тестового корня: True; отсутствие подтверждено: True.

Это замер одного запуска с наблюдателями. Он показывает стоимость участков, но не разделяет работу CPU, файловой системы и внешних проверок. Оптимизаций и увеличения лимитов в этом блоке нет. Исходные пять Windows-случаев: 0/5 PASS; Linux локально не запускался.
