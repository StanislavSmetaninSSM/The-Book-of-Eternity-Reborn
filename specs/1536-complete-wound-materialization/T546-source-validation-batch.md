# #1536 / T546 — Общий пакет проверок источников, r72

Результат: общий SourceValidationBatch через существующий сервис внедрён в
основной код. На текущей одинаковой сборке/INPUT OFF→ON первое восстановление
**167,8579042→147,9249174с (−19,9329868с)**; весь исходный тест
**269,7758544→247,4158425с (−22,3600119с)**. Устранены **268558** повторных
managed File.GetAttributes попыток в первом окне:914435→645877. Это одно
последовательное сравнение; универсальный выигрыш/статистическая значимость и
дополнительные100с не заявляются. Диагностика в обоих режимах включена без квот.

## Реализация и целостность

Новый явный SourceValidationBatch хранит только положительные сведения о
каталогах в ValidationOperation/ServiceCacheStorage. Ordinal пути, Trusted/
Windows API и роли раздельны. Контекст живёт внутри одного чтения либо одного
последовательного fresh-source цикла; первая и итоговая сверки не делят epoch.
Точные manager/lease/entry/registration/work/root проверяются при reuse.
Короткий r71 phase сохраняет прежнюю привязку к потоку и границы.

Leaf/type/absence, native handle/path, main-record/worker, байты/mtime/generation,
эффекты и закрытие остаются свежими. Retry/missing-open/ошибка/смена версии
очищают DATA. Protected reader, произвольные callbacks и prepared окна с
активными мутациями остаются на прежнем пути. Это клиентская оптимизация
чтения; GM-команды, примеры, схема и игровое поведение не меняются.

## Проверка реального покрытия

Первая полная пара T545 прошла263,4663568/262,2500806с, первое восстановление
163,9209606/161,212649с, но resumer имел0пакетов/0hits. Разницу2,7083116с
нельзя приписывать новому кэшу. Причина — отдельные canonical/exact диагностические
Action в реальном resumer, которые правильно отключали более длинный batch.

Обычный resumer теперь использует sealed буфер всех scalar read-events с
исходными Stopwatch timestamps вместо двух callbacks. Event-driven15с samples
воспроизводятся по времени записи; stage writer ставит текущее монотонное
время доставки. Перед обеими границами checkpoint и final/error tail события
выгружаются. Записей/байтовых квот нет. Доставка отложена; принудительное
убийство до выгрузки может потерять pending tail, поэтому эквивалентная живая
crash-диагностика не обещается. Полный диагностический callback-режим сохранён.

В принятой второй паре T545b отдельные before/after/delta охватывают только
первый вызов восстановления: OFF всё0; ON before0, delta **6222пакета,
773169запросов,198090source classifier attempts,575079hits**. After совпадает
с cumulative first. Это Trusted+Windows DATA;575079hits не равны сэкономленным
GetAttributes. Setup-only положительные cumulative значения отвергаются
отдельным negative control. В repeat новых пакетов нет.

## Дешёвые проверки и квалификация

| Владелец /10файлов | Первые GetAttributes OFF→ON | Fresh exit OFF→ON | Физические чтения | main/native OFF=ON |
|---|---:|---:|---:|---:|
| Source async |448→308|384→208|20|74/74|
| Source sync |368→258|304→158|20|54/54|
| Reserved async |600→460|400→224|20|83/83|
| Reserved sync |520→410|320→174|20|63/63|

Байты/mtime одинаковы. Причинный metadata RED2b981706a; исправление lifecycle
пассивного счётчика4cbf68370 дало4PASS. Actual-resumer factory RED8e1f11763:
23PASS/1causalFAIL; GREEN93b3:44/44PASS, passive2пакета/84hits, callback0.
Проверены raw byte/absence/namespace drift, очистка/версия/flow/hook/role/threadhop,
каждое событие/исходные due samples/partition/error tail/exactly-once/concurrency.

Финальный код **0b648cd21ae49e757eb37b0c545075fd0dafcf5c**: XML Tests85,4006с /
Integration54,2059с,8uniformDLL; bindingC399F70E...2A737. **584distinctPASS /
22категории /25descriptors**,252,8886с команды, complete/bothcleanup/no timeout.
Каталог12462метода/936категорий/0unmapped/stale; PlanOnly25/584/0executed.
155исходных schema controls и154исправленных window controlsPASS. Независимое
Astra/high code review45E1813C...3157/0unresolved. Выявленные P2 setup-coverage
и P3 XML исправлены; ранние9падений нового negative-набора были assertion/setup
ошибками при реально сработавшем InvalidDataException отказе, не runtime дефектом.

Новый genuine source42:1PASS180,9589с, два импорта/обе очистки,82файла513712B;
contractBBC9C603...FC63, packageD2ABB83F...B8A1/320331B, без rebind. Frozen ready
323C2669...8303/157pins, независимый admissionA2C16AC9...7CCB. Metadata обоих
original3b2e368eb711bba93b20b7b509d4701334625b32;600child/820shared/15category,
Cfixtures/Ddiagnostics, прочие кэшиon, producer forwardersoff, legacy timingsoff,
sealed first profileron, прежний CPU-GC observer. Конкурирующих тяжёлых работ нет.

## Полные замеры

| Интервал | OFF,с | ON,с | ON−OFF,с |
|---|---:|---:|---:|
| Весь TRX тест |269,7758544|247,4158425|−22,3600119|
| Producer exit/enter |89,7764652|87,1758916|−2,6005736|
| Первое восстановление, stage |167,8579042|147,9249174|−19,9329868|
| Повторное восстановление |0,8399946|0,8005552|−0,0394394|

Оба original полностьюPASS: игровые byte/state/custody/ChangingI/repeat,
birth/EOF/join, native producer73/resumer0 и обе очистки. Профили565/566имён,
3882421→3519081completed scopes,0open/late. Прочитаны все306JSON и4text records
обоих процессов двух запусков, без отбрасывания по размеру/числу.

| Inclusive таймер | Вызовы OFF→ON | Секунды OFF→ON |
|---|---:|---:|
|TrustedScope.Probe.GetAttributes|914435→645877|54.1063487→36.7565503|
|TrustedScope.ValidateDirectories|197584→197584|46.5838674→31.2892997|
|CanonicalPath.WindowsMetadata|80120→80120|24.2228459→20.0511405|
|MainAdmission.Validate.Guard|29400→29400|31.7037917→28.8546239|
|OperationFileData.FreshFence|38→38|20.8373303→11.2090976|
|OperationFileData.FreshSource|4782→4782|20.4843998→10.8909392|
|ValidationReadSet.ExitProof|19→19|9.4540106→6.2434335|
|ValidationReadSet.ExitSource|2449→2449|9.196901→6.0127028|
|OrdinaryRead.Open.ValidateFile|17720→17720|9.2459944→4.8910773|
|OrdinaryRead.Complete.ValidateFile|17720→17720|10.4236288→5.6783905|

Main guard29400, native handle/path65676, lease28334, source4782/2449 и
open/complete file17720 вызовов сохранены. Windows directory refresh attempts
стало на75632меньше. Вложенные времена складывать нельзя. Batch.Lifetime31,9088с
содержит полезную подготовку/чтение/проверки, а не измеренную стоимость одного
кэша или таймеров. Отдельная стоимость диагностики этим сравнением не измерена.

Resumer CPU181,28125→162,015625с: kernel96,6875→75,703125, user84,59375→86,3125.
Наблюдаемые GC pauses3065,365→3198,152мс за175,0089→155,0056с доставленных
интервалов; FullProcessPauseCoverage=false, startup/tail не полностью покрыты.
CPU/GC не добавляются к wall или inclusive строкам.

## Оставшиеся крупные участки

Текущие ON: GetAttributes36,7566с/645877, directory chains31,2893с,
Windows metadata20,0511с, main guard28,8546с (worker5,8230с, persisted main5,2486с),
fresh source fence11,2091с и reserved exit6,2434с. Большие обёртки: accepted
validation116,6122с/2, publication61,0252с, post-publication40,7434с/2, final
materialization/cleanup26,2026с; это перекрывающиеся расходы одной работы.

Крупный следующий кандидат для исследования — directory DATA в устойчивых
read-only участках публикации/очистки, сейчас исключённых из quiescent batch
при активных мутациях; existing CleanupProofReadWindow уже имеет отдельную
версию точных publisher mutations. Нового такого допуска в r72 нет. По плоским
агрегатам его доля не доказана: сначала нужна точная атрибуция/дешёвое owner
доказательство. Кэшировать успешный native/main/source verdict нельзя. Новых
обоснованных дополнительных100с данные не показывают; это уже отдельная граница
срока жизни. DetachedCopy6,0289с/58 не выбран крупной целью этого блока.

Полные записи/все hashes находятся только в
D:\BoeDiagnostics\source-validation-batch-r72-20261025: T545b-*-raw.zip,
T546b-comparison.json и qualification/INPUT/CPU-GC/instrument/backups. Git содержит
исходники, тесты и компактные человеческие результаты. Старые raw r71 и первой
нулевой пары заменяются только после независимой проверки полного нового
пакета и точного списка удаления; INPUT/CPU-GC/instrument/wrappers/binary backups
сохраняются. Коллекция внутри замера не ограничивается.
