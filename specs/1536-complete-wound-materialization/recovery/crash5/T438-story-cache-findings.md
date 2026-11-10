# T438 — оставшаяся стоимость восстановления сюжета

Статус: исследование сохранённого T429; реализация нового кэша не выполнялась.
Исходный Cut0 остаётся FAIL по тайм-ауту 600 секунд. Новых игровых прогонов нет.

## Доказательства и границы измерения

Использован последний полный архив на `D:\BoeDiagnostics\latest-T429-20261022`:
SHA-256 `DFC0F18F4E02FAFFEB33A5C17CAB99E3ADA763F6458EB6204142DBD9039FFEFC`.
Разобраны все 646 722 группы путей вызовов восстанавливающего процесса, без
выборки. Их 13 545 926 закрытых интервалов и все количества/длительности листьев
сверены с ранее проверенной полной сводкой. Потоковый разбор прошёл семь дешёвых
проверок: фрагментация, вложенный JSON, Unicode, отсутствие разделителя/закрытия
и однозначное отнесение вложенной очистки к одному разделу.

Измеренный runtime-source — прежний `9fd4ffc38fb7a9aa84d749d12a3842c46422dcfc`;
после удаления диагностической истории ему соответствует
`e3070b6a6e999209a242750592e9bca07034bcf2`. Различий runtime-кода, тестов,
TestSupport, scripts и native между ним и текущим исследованием нет.

Незакрытый вызов `JointStory.TryReconcileMortalJointStoryTurnAsync` наблюдался
с 345,724 до 599,843 секунды коллектора: сохранённый префикс составляет
254,119 с, итоговая длительность неизвестна. Счётчики формирования/записи
диагностики за этот префикс выросли на 71,499 с. Это не прогноз времени без
диагностики. Агрегаты ниже включают и короткую предыдущую попытку story-dispatch.
Открытые таймеры не выдаются за завершённые операции.

## Где накоплена стоимость

В story-ветке выполнено **1 945 021 `File.GetAttributes`, 96,623 с**. Из них
1 165 516 вызовов / 57,901 с находятся внутри последнего lease материализации,
включая очистку. Во всём прогоне максимальный завершённый `GetAttributes`
занял 3,982 мс: измеренная проблема здесь — число повторов, а не один долгий
завершённый системный вызов. Последний открытый `FileExists` этого не опровергает
и сам по себе не доказывает зависание API.

Соседние верхние этапы `ReadJointCompletedProofAsync` занимают 152,568 с.
Они выбраны так, чтобы не включать вложенный `JointProof` второй раз:

| Этап полного доказательства | Закрытых этапов | Время |
| --- | ---: | ---: |
| `JointProof.ValidateFootprintAsync` | 69 | 33,184 с |
| `JointProof.ValidateProgressionAsync` | 69 | 29,280 с |
| `JointProof.ReadOriginalProjection` | 69 | 23,968 с |
| `JointProof.ReadSourceSnapshot` | 70 | 22,375 с |
| `JointProof.ValidateResourcesAsync` | 69 | 16,971 с |
| Остальные соседние этапы | разные | 26,790 с |
| Всего закрытых соседних этапов | — | **152,568 с** |

В эти 152,568 с уже входят 44,018 с измеренной диагностики; прибавлять их нельзя.
Среди этапов есть одна проверка из тестового `AcceptedOutcomeCheckpoint`:
2,201 с. Без неё перечисленные этапы занимают 150,367 с. В таблице посчитаны
закрытые части: 69 закрытых footprint не означают 69 успешно возвращённых полных
доказательств. Следующий footprint был открыт при остановке процесса.

Дальнейшие числа описывают вложенные пути и не складываются с таблицей выше:

| Вложенный путь | Вызовов | Время |
| --- | ---: | ---: |
| `JointPhase.ReadCompletedProofAsync` | 41 | 104,244 с |
| `JointStorySource.ValidateAppliedBeforeAsync` | 9 | 27,542 с |
| `JointStorySource.ValidateAppliedAfterAsync` | 8 | 24,384 с |
| `JointStorySource.BindContinuationAsync` | 8 | 41,599 с |
| Повторный `JointPhase.ValidateProgressionDataAsync` после полного proof | 41 | 17,820 с |

## Почему проверки размножаются

1. `JointProgressionPhaseCapability.ValidateCurrentCoreAsync` сохраняет живые
   lease/owner/generation/consumed-проверки, а затем каждый раз вызывает
   `ReadJointProgressionPhaseAsync`, который заново читает полное доказательство.
   См. [phase capability, строки 69–78](../../../../BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointProgression.cs#L69).
2. Один `ReadMortalJointStorySourceAsync` проверяет applied-фазу до и после чтений.
   Между ними `TryBindExactJointContinuationAsync` тоже читает полный proof,
   а перед возвратом вызывает `ValidateCurrentJointCompletionAsync` — ещё один
   полный proof. Таким образом, один завершённый story-source проходит четыре
   полных proof-пути. Это композиция действующих проверок, а не рекурсия.
   См. [story-source](../../../../BookOfEternityClient/Services/StoryService.MortalJointRepair.cs#L161),
   [continuation binding](../../../../BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointRepair.cs#L87).
3. Проверка вычисленных характеристик трижды вызывает `applied.ValidateCurrentAsync`.
   Только три проверки projection до story, после story и при cleanup заняли
   вместе 31,885 с; каждая вызвала phase-reader три раза.
   См. [projection validator](../../../../BookOfEternityClient/Services/CharacteristicsService.MortalJointProjection.cs#L37).
4. `ReadJointCompletedProofAsync` проверяет progression через
   `ValidateJointProgressionDataAsync`, но возвращает только root/receipt.
   `ReadJointProgressionPhaseAsync` сразу после полного proof повторяет этот же
   алгоритм для получения результата. В story измерены 69 внутренних и 41
   дополнительных вызовов, всего 110. Последние 41 заняли 17,820 с.
   Это подтверждённое дублирование алгоритма, но повторное наблюдение текущих
   файлов нельзя просто удалить: между чтениями возможны мутации и callbacks.
   См. [первый вызов](../../../../BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointRepair.cs#L334),
   [повторный вызов](../../../../BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointProgression.cs#L158).

Сборка текста строки истории сама по себе мала: восемь `CalculateRow` заняли
всего 0,034 с. Первыми кэшировать форматирование текста или весь результат
story-операции не обосновано.

Также очистка ещё не дошла до удаления файлов. Захват 139 удаляемых members
занял 2,115 с, захват 84 сохраняемых образов — 1,030 с, проверка before-images —
3,030 с. Эти три соседних участка дают 6,174 с. Основная стоимость перед cleanup
publication находится в повторной аутентификации phase/story, а не в размерах
этого набора файлов. Prepared-namespace callback оставался незакрытым при timeout.

## Что уже делает кэш и куда его подключать дальше

Единственное production-открытие `BeginRecoveryValidation` находится в
[публикации механики](../../../../BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs#L53).
Её фактическая закрывающая запись: 76 requests, 6 source executions, 70 reuses;
четыре source executions выполнены fresh, три fresh boundaries, четыре пары
mutation begin/end, ноль failure closures и active mutations, состояние закрыто
с нулём entries/dependencies. Эти числа относятся к работающему общему сервису,
а не только к прежнему read-set. В story такого открытия нет; существующие два
DATA-правила подключены только к mechanical/continuation-подготовке механики.

### Конкретное препятствие: recovery сам меняет версию

На обычном lease `ReadJointProgressionPhaseAsync` сначала вызывает
`ResolveBackupPublicationRecovery`, а затем `ReadJointCompletedProofAsync`
вызывает его ещё раз. Обычная ветка resolver вызывает
`RecoverTrustedLocalStorage`; тот **сначала** начинает `BeginValidationMutation`
и только затем проверяет `HasStorageEvidence`. Даже возврат без journal evidence
завершает mutation и меняет opaque epoch. Это две существующие границы на одном
phase-read, а не только гипотетическая запись между вызовами.
См. [phase recovery](../../../../BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointProgression.cs#L152),
[proof recovery](../../../../BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointRepair.cs#L207),
[resolver](../../../../BookOfEternityClient/Core/FileSystemManager.BackupLifecycle.cs#L31),
[no-op recovery](../../../../BookOfEternityClient/Core/FileSystemManager.TrustedLocalStorage.cs#L146).

Поэтому обёртка всего phase-reader или full-proof тела в текущий сервис не
сохранит пригодный повторный успех: вычисление само изменит свою зависимость.
Перенос recovery перед каждым cache-request тоже меняет epoch перед каждым
request. Одно открытие story-контекста эту проблему не решает. Recovery,
классификацию journal debt и no-op invalidation нельзя пропускать ради hits.
Повторное использование по точному экземпляру capability само по себе также
недостаточно: это не доказательство неизменной версии.

### Следующие кандидаты и порядок проверки

Обоснованное направление — **DATA-проверки phase/completed-original**,
повторяемые story и projection. Отдельный словарь для `GetAttributes` и кэш
целого разрешающего proof/capability не нужны. Но работоспособность конкретных
cache-сегментов ещё предстоит доказать; измеренная стоимость не равна экономии.

1. Первый узкий кандидат — передача копируемого результата
   `ValidateJointProgressionDataAsync` из полного proof в его phase-reader.
   Оба вызова находятся после внутренних recovery-границ. Между ними остаются
   проверки public outputs, final marker, tuple/commands и decision: их свежесть
   сохраняется. До реализации нужно на настоящем lease доказать, что на всём
   пути нет intervening mutation, fresh boundary, callback или изменения
   зависимости. При их появлении второй DATA-вызов обязан выполниться свежим.
   **41 повтор / 17,820 с** — верхний измеренный объём этого второго прохода,
   не обещанное ускорение и не число будущих hits.
2. Для такого результата использовать существующие `OperationComputation<TResult>`
   / `ValidationOperation.GetOrComputeAsync`, immutable DATA identity, defensive
   copies и настоящие файловые зависимости. Текущий FSM-адаптер предоставляет
   только validation-fact путь; typed-result адаптер потребует отдельного
   изменения. Новый reference вновь разобранного root JSON не даст reuse между
   reader-вызовами. Готовый ключ и допустимость конкретного lease ещё не доказаны.
3. Для reuse между разными full-proof/phase-вызовами сначала отдельно исследовать
   расположение recovery и стабильных DATA-сегментов. Сохранить обе существующие
   recovery-границы, no-op invalidation и live owner/lease/generation/consumed
   guards. Нельзя объявить recovery выполненным «один раз на lease» без отдельного
   доказательства обработки нового debt, callbacks и внешних изменений. Текущее
   исследование такого изменения контракта не разрешает.
4. Открывать контекст отдельно внутри подходящего реального физического lease
   только после квалификации его eligibility и стабильного сегмента; данные между
   lease не переносить. Snapshot/projection и footprint — следующие измеренные
   кандидаты внутри того же сервиса. Точные потенциальные hits неизвестны: trace
   не содержит identity capability и полный список версий каждого вызова.

Обязательные свежие границы: phase publication, запись вычисленных характеристик,
append истории, cleanup batch, callbacks и повторное получение lease. Активная,
ошибочная, компенсированная или no-op мутация, в том числе другого manager,
должна исключать старый успех; отсутствие файла и namespace замены тоже входят
в зависимости. Изменение зависимости во время await не сохраняет успех.

Особенно важно: `PublishLocalCoreAsync` начинает mutation на строке 301,
а prepared-namespace callback вызывает позже, на строках 365–368.
См. [публикацию](../../../../BookOfEternityClient/Core/FileSystemManager.TrustedLocalStorage.cs#L294).
Текущий version-capture во время такой mutation непригоден. Последнюю открытую
проверку cleanup нельзя ускорять, переиспользуя кэш из состояния до публикации.
Её owner, signed before-images и послеcommit readback сохраняют свежие оракулы.
Произвольные raw внешние изменения по-прежнему должны обнаруживаться на свежих
границах; mutation epoch не обнаруживает их сам.

## Проверка следующего изменения

До нового тяжёлого прогона нужны дешёвые причинные файловые фикстуры. Сначала
воспроизвести обе реальные recovery-границы, включая отсутствие evidence, и
доказать отказ от reuse при изменении epoch. Затем проверить узкий post-recovery
сегмент: одинаковые DATA-запросы при неизменной версии сокращают тела и реальные
`GetAttributes`;
живые guards выполняются каждый раз. Затем write/delete/absence/namespace,
другой manager, изменение версии во время await, consumed/owner/lease loss,
ошибка/отмена/закрытие и fresh publication callback должны исключать reuse.
Для typed результатов проверяются defensive copies и неизменность источника.
Свежая фаза/доказательство не подменяется fixture-объектом с придуманным issuer.

Это план дальнейшей реализации, не заявление об уже полученном ускорении или
возможности удалить все 69 повторений. T427–429 остаются открыты. В этом блоке
проверялись анализатор и данные; игровые тесты и сборки не запускались.

Полный производный отчёт: локальный
`D:\BoeDiagnostics\latest-T429-20261022\T438-remaining-branch-analysis.json`.
Там сохранены все выбранные semantic paths, все partitions, reconciliations,
фактическая запись metrics и хеши инструментов. Сырые данные и машинный отчёт
в GitHub не загружаются.

Независимая проверка отдельным gpt-6-astra/high приняла T438 как исследование:
числа, вложенность таймеров, источники и происхождение анализа сверены;
замечание T438-F1 о двух recovery-границах исправлено и закрыто. Новых открытых
замечаний нет. Локальная запись — `T438-independent-review.json` в том же
каталоге D:. Это не приёмка будущего кэша или исходного игрового теста.
