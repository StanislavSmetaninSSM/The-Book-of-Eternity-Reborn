# r68 — Крупные затраты оставшихся 163 секунд (промежуточный отчёт)

Задача [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
spec REM1..5, tasks T519..521. Исследование сохранённых замеров и исходников;
это описание первоначального read-only этапа. Затем scope REM6..8 расширен
необходимым пассивным профилированием: код уже подготовлен,317 дешёвых случаев
прошли; новый INPUT и полный замер ещё впереди. Это пока не финальный ответ.

Последний полный r67 ordinary: qualified source7272b0e29, metadataa4c03a251,
первое восстановление **163,3117708 с**. Архив и анализ привязаны к принятому
T517-ordinary-raw.zip. На первоначальном read-only этапе runtime совпадал с qualified source.
Новый диагностический qualified source8a2b8db93; его полный замер ещё не выполнен.

## Что действительно измерено сейчас

| Непересекающееся окно текущего восстановления | Время, с | Что находится между точками по коду |
|---|---:|---|
| После LateTerminalAndIdleOperationBound до завершения custody capture | 16,0182716 | Возобновление interface publication, исходные before-images, intake/custody и допуск к подготовке |
| После custody capture до AcceptedOutcomeValidatedBeforeMaterialization | **115,3186501** | Подготовка/публикация механики, создание common plan, post-publication проверки и повторный проход принятого результата |
| После accepted callback до возврата первого восстановления | **29,8064416** | Проверки progression/projection, публикация истории, terminal cleanup и завершающие source fences |
| Остальные начальные участки и callbacks | 2,1684075 | В том числе accepted callback тестовой обвязки 1,9926133 с |

Все семь исходных marker gaps точно складываются в163,3117708. Это интервалы
между точками, не времена отдельных внутренних методов. В частности, **115,32 с
нельзя назвать временем одного validator или одного constructor**. В текущем
OFF-замере нет именованных таймеров, позволяющих честно разделить его дальше.

Пути: GameEngine.MortalJointRepairCanonicalPublication.cs:75–100 начинает подготовку
после custody checkpoint; GameEngine.MortalJointInterfaceResume.cs:116–117 затем
вызывает post-publication; GameEngine.MortalJointStoryTurn.cs:99–106 повторяет
ValidateAcceptedTurnOutcomeWithRepairLoopAsync и достигает accepted checkpoint.
StoryTurn.cs:110–150 содержит последний интервал: progression, projection до/после
story append, proof validation, runtime advance, cleanup, CompleteAsync источников.

## Большие внутренние кандидаты по последнему подробному профилю

Детальные секунды ниже — **исторический r62 FULL**, до directory-cache r64 и двух
control DATA фаз r65, с дорогим observer. Его первое восстановление295,0478829 с,
а не нынешние163,31 с. Они определяют направления исследования; переносить их в
текущий бюджет, складывать вложенные строки или обещать такую экономию нельзя.
Старые raw удалены по принятой политике: используется ранее проверенный компактный
AllNamedAggregates, сверенный с принятым r66 evidence; нового raw replay нет.

| Исторический раздел | Вызовы | Inclusive, с | Вывод для поиска |
|---|---:|---:|---|
| MainAdmission.Validate.Guard | 33706 | 82,9880517 | Многократный допуск/guard/namespace — самый большой общий файловый слой |
| Lease.Validate.Total | 32270 | 81,8581062 | Пересекается с предыдущим; это не ещё82 с |
| OperationFileData.FreshSource | 3874 | 27,4205202 | Свежие источники, включая завершающие сверки |
| OperationFileData.FreshFence | 30 | 27,8801244 | Включает чтения источников; не добавлять к FreshSource |
| WoundBundle.DetachedCopy / constructor | 58 | 14,8775972 / 14,8764018 | Повторная реконструкция и проверка четырёх стадий |
| JointCommonAuthority.Bundle | 37 | 9,7004049 | Вложенная часть тех же копирований |

Весь domain pipeline тоже имел крупные wrappers: PublishCurrentMortalJointRepair
112,6103549 с; ValidatePostPublication два раза94,8001378 с; GameState два раза
51,7496826 с; финальный materialization/cleanup48,7961244 с. Это исторические
вложенные оболочки. Они показывают, где ставить уточняющие измерения, но сами
не доказывают ни лишнюю работу, ни безопасный кэш.

## Приоритет1: файловые первые чтения и свежие сверки — большой, сложный резерв

В текущем r67 к первому возврату учтено **26950 каталоговых admission contexts**,
592900 requests,269500 source queries и323400 hits. Накопленные счётчики включают
работу менеджера до первого возврата; это не перепись всех filesystem-вызовов и
не строгая внутренняя разбивка163с. Кэш внутри одного допуска уже работает.

SourceDataOperation.VerifyCoreAsync:742–767 для каждого затронутого ключа снова
читает источник, сравнивает bytes/absence/mtime, проверяет generation, lease и
root version. Этим завершается допустимость ранее использованных DATA. Простое
помещение MainAdmission.Validate=true, валидности lease/native guard или результата
этого fence в кэш нарушило бы текущие гарантии. Уже прочитанные bytes не заменяют
обнаружение внешней записи с прежними размером и timestamp.

Большой следующий архитектурный probe — **пакет provisional чтений**, в котором
через существующую операцию сервиса разделяются только DATA общего path/source
наблюдения. Свежие persisted-main/worker/native/generation проверки и точный
конечный fence остаются. Нужен формальный контракт read-only участка и того,
почему допустимы групповые проверки вместо нынешнего допуска для каждого чтения.
Это изменение протокола, а не готовый кэш, который можно включить одним флагом.

Ключ/зависимости: exact root identity, generation bytes/binding, physical lease и
registration, operation/root version, family и ordinal raw path. Изменение данных,
worker attachment, reentry/observer, новая регистрация/поколение, ошибка, закрытие
или эффект прекращают переиспользование. Перед публикацией/эффектом/выдачей
результата исходные guards и bytes/absence проверяются заново. Если это нельзя
доказать без ослабления нынешних контрактов, такой batch вводить нельзя.

## Приоритет2: повторная реконструкция wound-bundle через существующий сервис

Найден конкретный повторный путь: CommonPlan.cs:97–103 getter Bundle всегда
вызывает Baseline.Bundle.DetachedCopy. Даже ComputeFingerprint:135 получает
Bundle.BundleFingerprint через полный новый admitted bundle. AcceptedMechanicsPlan
constructor:444–482 снова проверяет input/prepared/effect/final, затем отделяет
все возвращаемые графы. ValidatePreparedResult вызывает PreparedReplayAgrees,
который снова запускает PrepareJointRepairCandidate с клонированным input.

Здесь можно исследовать повторное использование **ожидаемых расчётных DATA и
неизменяемых проекций одного private bundle**, используя имеющиеся
OperationComputation + ComputeRecoveryDataAsync/ValidationOperation. Хранить нужно
расчётные структуры, не HasValidSeal=true или успешный verdict. Общий mutable
JsonNode/массив не отдаётся; возвращаемые графы по-прежнему отделяются.

Полный key/capture должен охватывать реального owner и четыре stage graphs,
исходные authority dependencies и live version. Одна сохранённая fingerprint
строка не доказывает неизменность. Текущие authority/seal/lease/source проверки
выполняются; изменение nested input, before-image, continuation/reservation,
графа результата, закрытие и failure должны инвалидировать зависимые DATA.
Сервис при отсутствии live recovery segment вызывает исходное вычисление;
обещать попадания только по повторному вызову метода нельзя.

Это ограниченный кандидат, не готовая гарантия14,88 с. В старом профиле
CrossChecksAndDetach суммарно1,8702944 с, а PreparedReplay только1,9571356 с.
Поэтому кэш одного клонирования коллекций или одного replay не предлагается как
отдельная оптимизация: нужно измерить весь admitted constructor и его зависимости.
Final retained reduction уже используется; предлагать его повторно нельзя.

Дешёвая проверка возможна на существующих typed JointBundleComponentTests
(copy/admission timings/current-bytes/refusals) без GameEngine/world. Нужны
раздельные суммы constructor: prepared/effect/final admission/seal и detached DATA,
реальные hits/повторные вычисления, мутация каждого nested графа, чужой owner,
смена lease/generation, окончание операции и независимость возвращённых объектов.

## Что уже кэшируется и куда пока не тратить работу

QTE: JointQteContributionData.RecomputeAsync:174 использует ComputeRecoveryDataAsync.
Исторический wrapper36,685с не равен ещё36с свободной реконструкции. Progression
и completed proof/history уже имеют DATA reuse; terminal cleanup передаёт
reusePreparedProofData:true (GameEngine.MortalJointTerminalCleanup.cs:164).
Исторические35,685с cleanup не являются35с ещё не подключённого кэша.

ClientOwned/Realm в текущем r67:2,9416067 и4,7559499с накопленно к первому возврату,
66+366fast hits. Повторно оптимизировать их как основной резерв оснований нет.
Проекции characteristics в старом профиле до/после story и cleanup суммарно около
2,54с; отдельный кэш этих трёх вызовов сейчас уступает большим направлениям.
Сериализация, мелкие getters, отдельные файлы и кэш decode main-record не выбраны.

53 current semantic recovery contexts имеют разные IDs и закрыты/очищены.
Их196 requests/66sources/130reuses относятся к этому semantic service, а не ко
всем чтениям. Нельзя превратить53 завершённых контекста в один долгоживущий кэш,
игнорируя изменения/публикации между ними, только ради большего hit rate.

## Как получить недостающие текущие внутренние секунды

Legacy OperationTimingObserver не подходит для сопоставимого уточнения:
BeginAdmissionDirectoryObservations отказывает при ненулевом hook или текущем
Observer; OperationTimer.Notify также включает SuppressValidationReuse на время
callback. FULL меняет режим, в котором были получены163с.

Следующий корректный измерительный probe — sealed внутренний пассивный аккумулятор
именных timing scopes без произвольного делегата и без ослабления прежних observer
проверок. Собрать все выбранные функциональные scopes, counts/inclusive elapsed,
незавершённые/failure scopes; не ограничивать записи или байты. Сначала на дешёвой
реальной фикстуре доказать тот же source/directory hit rate и отказы, default-off,
изоляцию потоков и exception behavior. Затем один исходный600-limit прогон с
теми же включёнными кэшами, native ownership и fresh INPUT. Только он позволит
назвать сегодняшнюю стоимость каждого внутреннего метода и выбрать кэш.
На первоначальном этапе это был проект будущего probe. В amended REM6..8
он включён в эту задачу: реализация и дешёвые проверки готовы, полного замера нет.

## Результат и проверка

Крупный резерв в основном коде вероятнее связан с массовым файловым допуском;
его безопасный batch требует отдельного протокола. Bundle DATA — более узкий
кандидат с конкретной повторной реконструкцией. **Готового доказанного нового
кэша, обещающего ещё100с без изменения протокола, сейчас не найдено.**
Текущую точную внутреннюю цену кандидатов по сохранённому OFF-профилю определить
нельзя; исторические inclusive цифры её не заменяют.

D-only remaining-recovery-r68-20261024/T520-evidence.json хранит полный текущий
набор stages/semantic metrics/counters, весь retained r62 AllNamedAggregates,
19 source pins и32 проверки SHA/source/arithmetic/closed IDs. Первоначальный
read-only этап не менял код и ничего не запускал. Затем пассивная диагностика
получила XML/uniform8DLL и317PASS; original и очистка всё ещё не выполнялись. Текущие r67 original/CPU/GC/INPUT/backup
остались неизменными; в Git исходники диагностики/тестов и компактные документы; raw только на D.
Независимое ревью выводов и доказательств ещё выполняется.
