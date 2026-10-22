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

Первое обоснованное продолжение — подключить общий сервис к **DATA-проверкам
phase/completed-original**, повторно используемым story и projection. Отдельный
словарь для `GetAttributes` и кэш целого разрешающего proof/capability не нужны.

Предлагаемая последовательность для следующего implementation-блока:

1. Открывать контекст отдельно внутри каждого подходящего реального физического
   lease, начиная с финальной материализации story. Данные между разными lease
   не переносить. Открытие контекста само по себе ничего не ускоряет: новые DATA
   тела должны быть явно подключены к сервису.
2. Отделить повторяемую DATA-сверку в `JointProgressionPhaseCapability` от живых
   owner/lease/generation/consumed-проверок. Для одной уже выданной capability
   допустимый кандидат identity — её точный приватный экземпляр; зависимости —
   тот же lease/root и текущий согласованный mutation epoch. Нельзя выдавать
   старую capability или кэшированное полномочие как результат.
3. Для повторного результата progression использовать уже существующие
   `OperationComputation<TResult>` / `ValidationOperation.GetOrComputeAsync`:
   копируемые DATA-значения и настоящие файловые источники. Текущий FSM-адаптер
   предоставляет только validation-fact путь; typed-result адаптер потребует
   отдельного изменения. Root JSON вновь разбирается в разных reader-вызовах:
   его новая reference identity не обеспечит reuse между такими вызовами.
   Нужна доказанная immutable DATA identity; конкретный ключ ещё не утверждён.
4. Исходный snapshot/projection и footprint являются следующими кандидатами
   внутри того же сервиса после causal-проверки шага 2. Общая стоимость их
   повторений измерена, но точные потенциальные hits неизвестны: в текущем trace
   нет identity capability и полного списка версий для каждого вызова.

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

До нового тяжёлого прогона нужны дешёвые причинные файловые фикстуры: одинаковые
DATA-запросы при неизменной версии сокращают тела и реальные `GetAttributes`;
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
в GitHub не загружаются. Независимая проверка результатов — перед закрытием T438.
