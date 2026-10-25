# r71 — Общие данные каталогов подготовки и начального допуска

Issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
FUSE1..7, T535..540. Изменение реализовано через существующий сервис. На исходном
сценарии оба прогона PASS: полный тест сократился на3,99 с, первое восстановление —
на1,00 с. Это небольшой дополнительный выигрыш; дальнейшее дробление коротких
соседних контекстов не выглядит основным путём к большому ускорению.

## Изменение и сохранённые договоры

Обычный reader явно передаёт свой `ReadPathObservationPhase` только в начальную
проверку lease/main admission. Положительный вид общего каталога берётся из
`ValidationOperation`/`ServiceCacheStorage`. Точный manager, поток, root version,
callback/worker/suppression policy и оба переключателя проверяются при выборе.
BasePath/GameSessionPath и исходные grants различаются; API/роль Windows metadata
не смешивается с Trusted classifier. Другие вызовы получают прежний null/default.

Внутренний `AdmissionDirectoryObservations` не сохраняет заимствованное значение
в своём кэше: после закрытия, ошибки, смены версии или политики оно не переживает
внешний phase. Свои свежие наблюдения внутри одного admission переиспользуются
по прежнему правилу. Custom classifier не получает чужие DATA. Phase закрывается
до open/hook/await/retry; более широкий пакет через await не реализован.

Guard-файл, native handle/path, текущие main-record bytes, worker inventory,
поколение/lease и свежие bytes/absence/mtime/effect/exit fences проверяются как
раньше. Сохранён только вид каталога, а не успешный результат проверки authority.
Game/GM schema, native lineage, правила механики и форматы сохранений не менялись.

## Дешёвое причинное доказательство

RED28073cb2c:3случая,1PASS/2ожидаемых отказа на отсутствии сокращения. Реальные
async470→470 и sync390→390 запросов на10файлах. Отдельная композиция реального
guard показала3повторных общих предка:6→3вызова classifier, guard leaf1в обоих.

GREENda4048f74:18/18PASS. Async470→440, sync390→360 физических GetAttributes;
main/native проверки40/40 для async и30/30 для sync. Содержимое и timestamps равны.
Проверены inner-cache escape после outer close, foreign owner/thread, смена
переключателей, custom source/error identity, native wrong-handle, current main
record/worker inventory, поддержанная mutation и поздние same-mtime bytes/absence/
directory-at-leaf.90borrowed hits дают30сэкономленных физических запросов: часть
borrowed hits заменяет прежние попадания внутреннего кэша, а не файловое I/O.

Финальная квалификация code1d2bd1220d88d79f6f486081d1fac89c87f2595c:
**522различных PASS**,19категорий/22TRX, полный selection и обе cleanup-проверки.
Включены8новых строгих parent/child configuration случаев. XML builds0errors,
восемь DLL одинакового source; независимое Astra/high review0нерешённых замечаний.
Catalog valid,0unmapped/stale; PlanOnly22descriptors/522cases,0executed.

## Исходная пара с прежним лимитом600 секунд

Metadata1204946056ff7958f2fcc8b251b7bc4b12a80a09; одни qualified DLL и **новый**
source42 INPUT, проверенный генерацией и двумя независимыми imports.82файла,
513712B, fingerprint9548AE94ED5D734A9CBA91418B0A34AD3D053664D8BFEF594AEEF49269035478.
Менялся только `BOE_TEST_JOINT_READ_ADMISSION_CACHE`: off→on. Старые directory,
source/control и ordinary-path caches ON, producer forwarders OFF, callback timers
OFF, sealed first-resumer profiler ON; frozen CPU/GC. C: SSD fixtures, D: uncapped
output,600child/820shared/15category, без конкурирующих тяжёлых работ.

| Интервал | OFF, с | ON, с | ON−OFF, с |
|---|---:|---:|---:|
| Полный тест, TRX |263,7146212|259,7268413|**−3,9877799**|
| Producer до exit/enter |87,0296702|83,9925761|−3,0370941|
| Первое восстановление, stage markers |164,7464169|163,7462976|**−1,0001193**|
| Повторное восстановление |0,8331125|0,7539137|−0,0791988|

Корневой scope первого восстановления164,7449306→163,7447393 с; это другой
измеритель, его разница−1,0001913 с не подменяет marker delta. Одна упорядоченная
пара не даёт статистической оценки и не доказывает универсальное ускорение.
Диагностический overhead отдельно не измерен; phase включает полезную работу,
его26,70 с нельзя назвать стоимостью таймеров или кэша.

Оба original полностью прошли: игровые byte/state/custody/ChangingI/повторные
проверки, birth/EOF/join, native producer73/resumer0 и очистка. Обработаны все
**298JSON-записей и4текстовые строки** обоих процессов/прогонов. Каждое окно
содержит565имён;3 904 251→3 882 421 завершённых scopes, open/late0. Ничего не
отбрасывалось по числу записей/байтов. Все66членов двух raw ZIP проверены SHA/CRC.

## Где сэкономлено время и I/O

| Таймер первого восстановления | Вызовы OFF→ON | Секунды OFF→ON |
|---|---:|---:|
| TrustedScope.Probe.GetAttributes |936265→**914435**|54,0860888→52,2774072|
| MainAdmission.Validate.Guard |29400→29400|31,9813749→30,3255229|
| ↳ GuardCheck |29400→29400|21,3623666→19,5732793|
| OrdinaryRead.Async.LeaseAdmission |5031→5031|5,9612037→4,4906255|
| OrdinaryRead.Sync.Admission.Lease |315→315|0,2357592→0,1758207|
| MainAdmission.Validate.WorkerInventory |29400→29400|5,5330691→5,6023132|
| MainAdmission.Validate.PersistenceRead |29400→29400|5,0269501→5,0897297|

В текущем первом timing window устранено**21830managed GetAttributes попыток**,
а не21тысяча доказанных kernel syscalls. Свежие main/worker/persistence проверки
не удалялись. Два targeted initial-admission таймера показывают уменьшение1,5305167с;
они описывают выбранную часть работы, а не всё восстановление. Вложенные Guard,
GuardCheck, GetAttributes и parent scopes складывать нельзя.

Cumulative счётчики включают setup до первого окна. В resumer first physical
admission queries269500→246475 (**−23025**), requests592900 и invocations26950
сохранены.1195из23025 лежат вне timing window. Producer249950→236100 (−13850),
invocations24995 одинаковы. Borrowed hits0→41550producer,0→69075resumer first и
70635repeat; это не столько же сэкономленных файловых запросов. Счётчик phase
теперь включает и новые main-source обращения, поэтому его source queries
254918→277943 не являются ростом глобального I/O. Phase/main серии перекрываются.

Resumer CPU178,4375→176,859375с: kernel94,875→92,359375, user83,5625→84,5с.
Наблюдаемые GC pauses2964,018→3058,935мс; интервалы172,0131→171,0083с. Это
доставленные интервалы с неполным startup/tail coverage, не все pauses процесса.
Producer CPU87,953125→85,3125с. CPU/GC не добавляются к wall или вложенным scopes.

## Что осталось крупным

Текущие ON: GetAttributes52,2774с/914435попыток, ValidateDirectories44,9906с,
WindowsMetadata23,8837с, main Guard30,3255с, FreshFence20,7063с/38 и ExitProof
8,8902с/19. Open.ValidateFile9,2253с и Complete.ValidateFile10,3989с проверяют
источник на границах открытия/завершения. Эти времена также перекрываются.

PublishCurrentMortalJointRepair63,8830с, post-publication48,5732с/2 и final cleanup
29,0019с — большие обёртки той же работы. Первая выросла на0,6875с при уменьшении
targeted lease checks; это вариативность остальной работы, не дополнительный
независимый расход поверх всех строк. QTE19с включает source checks; его чистый
calculation уже переиспользуется. DetachedCopy5,25с не выбран следующей целью.

**Оценка:** узкое соединение контекстов сохраняет целостность и убирает лишние
вызовы, но резерв порядка секунды восстановления мал. Дальнейшее заметное
ускорение требует отдельного доказанного read-only batch для первых чтений и
fresh source сверок через существующий сервис. Там надо разделить directory
DATA при точных lease/registration/root version, сохраняя свежие leaf/native/
main/worker/bytes/absence/mtime/generation границы. Растягивать нынешний phase
через await или кэшировать успешный FreshFence/authority нельзя. Новый широкий
batch в r71 не сделан; гарантированных дополнительных100с данные не показывают.

Полные записи и hashes находятся только в
`D:\BoeDiagnostics\shared-read-admission-r71-20261025`: T539-*-raw.zip,
T540-comparison.json, qualification/CPU-GC/INPUT/backups. Git содержит исходники,
фикстурные контракты и этот компактный отчёт. Предыдущие raw удаляются только
после принятия новой пары и независимой проверки точного плана очистки;
CPU/GC/INPUT/instrumentation/binary backups сохраняются.
