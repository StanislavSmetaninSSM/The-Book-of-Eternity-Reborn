# r70 — Крупные кандидаты после кэша подготовки чтения

Issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
AUD1..5, T531..534. Исследование текущего профиля; нового production-кэша,
инструментирования и дополнительного полного запуска в этом блоке нет.

**Вывод:** основной резерв остаётся в повторных файловых наблюдениях внутри
первых чтений и свежих сверок. Простого ещё не включённого кэша игровой
арифметики с сопоставимой стоимостью не найдено. Следующий разумный шаг —
дешёвый причинный probe общего контекста положительных наблюдений каталогов
для замкнутого пакета чтений. Экономия этого пакета ещё не измерена.

## Последний фактический замер

Последняя пара r69 использовала одни DLL/новый source42 INPUT; различался только
переключатель directory DATA подготовки ordinary read. Оба исходных Cut0 PASS,
лимиты600child/820shared/15category, C: SSD фикстуры, D: uncapped диагностика.

| Интервал | OFF, с | ON, с | Разница ON−OFF, с |
|---|---:|---:|---:|
| Полный тест |269,4711405|263,1057419|−6,3653986|
| Производитель до exit/enter |88,4731789|85,5689228|−2,9042561|
| Первое восстановление, stage markers |168,4595757|164,8300682|−3,6295075|
| Повторное восстановление |0,7910652|0,8594145|+0,0683493|

Это одна упорядоченная пара, а не статистическая оценка. Текущий ON root scope
164,8291649 с отличается от marker interval на0,0009033 с. Проверены все563 имени,
3 898 905 завершённых scopes, открытых/поздних0. Удалено158 390 managed metadata
попыток, но выигрыш первого восстановления только3,63 с: стоимость сохранения,
поиска и проверки пригодности DATA также входит в результат. Не обещаем100 с.

Qualified code227183db6, metadata0440208ca, INPUT contract0703D8EF…04378.
Полные исходные записи — D:\BoeDiagnostics\ordinary-read-path-r69-20261025\
T529-cached-raw.zip и T529-uncached-raw.zip; компактное сравнение T530-comparison.json.
Исходный профиль ON перечитан из архива, SHA проверен и значения всех563 строк
совпали с принятым сравнением. D:\BoeDiagnostics\remaining-cache-r70-20261025\
T532-current-profile.json содержит происхождение и весь именованный профиль.

## Что сейчас занимает время

| Текущий таймер ON | Вызовы | Inclusive, с |
|---|---:|---:|
| TrustedScope.Probe.GetAttributes |936 265|53,660771|
| TrustedScope.ValidateDirectories |197 584|46,117600|
| TrustedScope.Construct |108 226|23,583313|
| CanonicalPath.WindowsMetadata |80 120|23,759535|
| MainAdmission.Validate.Guard |29 400|31,639425|
| ↳ GuardCheck / WorkerInventory / PersistenceRead |29 400 каждый|21,127788 /5,456115 /4,997018|
| OperationFileData.FreshFence / FreshSource |38 /4 782|20,616251 /20,274855|
| ValidationReadSet.ExitProof / ExitSource |19 /2 449|8,797489 /8,555040|
| OrdinaryRead.Open.ValidateFile |17 720|9,165712|
| OrdinaryRead.Complete.ValidateFile |17 720|10,315351|
| OrdinaryRead.Complete.CanonicalSafety |17 720|5,891908|
| OrdinaryRead.Open.FileStream |17 720|2,418041|

Все строки inclusive и перекрываются. Нельзя сложить GetAttributes, цепочки,
конструкторы, допуск и fences или назвать их сумму устранимым временем.
Большие функциональные обёртки: PublishCurrentMortalJointRepairAsync64,341495 с,
ValidatePostPublicationAsync49,276129 с/2вызова, FinalMaterializationLeaseAndCleanup
28,783151 с. Эти обёртки также включают приведённую файловую работу.

## Сначала исключены ложные кандидаты

**Байты и ранний DATA-hit уже переиспользуются.** OperationFileData.RequestSync:
29 124 запроса/6,131177 с, SourceSync2 090/5,748703 с, ReuseSync27 034. Разница
0,382474 с — остаток внутри этой обёртки для всех запросов, не время только hits.
Ещё9 254 LeaseDataHit.Request занимают0,128533 с,836 GameStateDataHit.Request —
0,018414 с. Эти последние таймеры не включают весь внешний admission bookkeeping.
Явные публичные adapters уже пробуют DATA до EnsureValidCanonicalWriteLease
(`FileSystemManager.cs:1751`, `:1967`); implicit выбранный GameState делает то же
(`:1696`, `:1908`). Dispatch ordinary snapshots выбирает service до uncached
подготовки (`FileSystemManager.OrdinaryReads.cs:250`, `:273`).
Поэтому гипотеза «каждый hit сначала проходит весь ordinary read» неверна.
У невыбранных implicit readers остаётся recovery wrapper, но HasPending4,440335 с,
а RecoverBeforeReadSync0,570158 с не являются основным десятками секунд резервом.

**CleanupProofData.Fallback не означает отсутствие кэша.**82вызова/17,133844 с
передают управление ReadStableRecoveryDataAsync. При допустимости тот заимствует
живой source operation либо пытается открыть reserved read set; при suspended
owner или недопустимом read set сохраняется свежий маршрут. Название обозначает
отсутствие специального prepared-publisher window и само по себе не доказывает
82полностью некэшированных proof.
Специальное окно успешно работало5раз/6,992621 с. В87 proof самое большое тело —
ValidateFootprintAsync8,015481 с; ReadSourceSnapshot2,479377 с, progression3,031771 с,
история лечения1,082134 с. Сам факт87вызовов не доказывает87одинаковых входов:
между ними есть recovery, публикация и смена marker/progression.

**QTE и небольшие вычисления.**12 CalculationReused; только2 CalculationSource
занимают2,290131 с. RecomputeQteAsync19,412062 с включает baseline/source проверки;
ещё один кэш арифметики не уберёт эти19с. WoundBundle.DetachedCopy5,185354 с,
история/парсинг и текущие ClientOwnedControlFiles3,102978 с/RealmSegregation
4,478564 с не выбраны следующей крупной работой. Последние уже дали432 fast hits.

## Приоритет следующего probe: общий directory DATA для пакета первых чтений/сверок

Конкретный повтор: `TrustedLocalFileScope.cs:548` каждый раз проходит предков до
тома и вызывает Probe/GetAttributes (`:600`). В ordinary reader ResolvePath,
новый scope, начальный допуск, open и completion снова проходят путь
(`FileSystemManager.OrdinaryReads.cs:124`, `:313`). r69 разделяет DATA только между
подготовкой и начальным admission одного чтения; закрывает phase до open/await.
Допуск владельца внутри чтения создаёт **другой** directory operation
(`FileSystemManager.MainRunFence.cs:406`, AdmissionDirectoryObservations.cs).
Он уже устраняет дубли внутри своего вызова, но не разделяет DATA с ordinary
подготовкой и следующими чтениями. GameSessionPath и BasePath различны: общий
адаптер обязан явно различать их grants и точную область общих предков.

Ещё два точных места пакетов: `SourceDataOperation.VerifyCoreAsync:742` проходит
все touched identities и заново читает4 782 source; `ReservedValidationReadSet.
CompleteAsync:498` читает2 449 source на выходе19фаз. Здесь можно исследовать
общие положительные directory observations **внутри одного** read-only пакета,
сохраняя свежие leaf reads. Это новый протокол стабильности, а не разрешение
растянуть нынешний synchronous phase через await или весь ход.

| Часть договора кандидата | Требование |
|---|---|
| DATA / сервис | Только положительный ordinary-directory kind через существующие ValidationOperation/ServiceCacheStorage; не bool «lease valid» |
| Владелец | Один явный read-only batch под исходным live lease/reservation и root version; начать с общей синхронной подготовки/допуска |
| Ключ | Точный root/domain +ordinal absolute component +classifier/API role; Windows и Trusted виды не смешивать, lexical/grant checks прежние |
| Срок | Только выбранный пакет; для первого дешёвого этапа до open/await как сейчас. Более широкая версия требует отдельного доказательства исключения эффектов |
| Сброс | Mutation/root version/registration/lease/generation change, worker attach, callback, reentry, overlap, error или close немедленно прекращают reuse |
| Возвращаемое | Enum/неизменяемое наблюдение; никаких mutable grants, lease, handles, capability или источников вне прежней области |
| Свежесть | Leaf/type/absence, native handle/path owner, main record, worker inventory, фактические bytes/mtime/generation на прежних границах сохраняются |

Текущий счётчик не содержит caller→path→batch раскладки; нельзя отнести все936 265
GetAttributes к этому кандидату или утверждать, что31,64 с допуска целиком уйдут.
CanReuseSourceData/callback fallback и текущие negative/refusal tests сохраняются.
Внешняя одновременная правка открытого save по конституции не поддерживается;
новых привилегий, глобального TTL или adversarial OS модели здесь не требуется.

Дешёвый probe до production-подключения: настоящий manager/main owner,10малых
файлов, независимые off/on fixtures. Раздельно измерить prepare, первый допуск,
fresh fence, metadata requests/unique paths/source queries/hits и native/bytes.
Сначала показать пересечение путей между двумя текущими contexts; затем проверить
реальное сокращение source queries у общего контекста при равных bytes/mtime.
Не ставить PathMetadataQueryObserver: произвольный callback выключит reuse;
использовать sealed passive counters. Негативы: link/missing directory на следующем
пакете, ошибочный/Running/changed main record, worker inventory, lost handle,
same-mtime changed bytes, generation, новая регистрация, mutation, callback,
ошибка/close. До расширения через await отдельно доказать lifetime/reservation.
Только после дешёвого эффекта и независимого review — новый настоящий INPUT и
тот же600-секундный original. В этом исследовании probe не реализован/не запускался.

## Что пока не рекомендуем кэшировать

Не сохранять успешный FreshFence/ExitProof между границами: они заново сравнивают
bytes/absence/mtime и generation, а новый read в SourceDataOperation.EnterRead
сбрасывает одноразовую verification receipt (`:309`, `:394`). Root token сам по
себе не заменяет эти сравнения. Отдельная гипотеза — совпадение физических путей
у разных raw/family/explicit keys внутри одного fence; его доля не измерена.
Сливать семейства на основании имени файла сейчас нельзя.

Не кэшировать completed capability/прошлый validation verdict вместо исходных
DATA; не отменять повторный GameState только потому, что вызовов2. Наиболее
перспективен общий пакет directory DATA; точный ожидаемый выигрыш неизвестен.
Простой гарантированный дополнительный выигрыш на100с текущие данные не показывают.

Проверки блока: provenance/hash/completeness и563строки текущего профиля,
точная арифметика, текущие source anchors и отсутствие runtime diff. C# не менялся,
поэтому380прошедших r69 случаев и полный исходный тест не повторялись для Markdown.
Все первичные записи сохранены на D; в Git идут только этот краткий отчёт и
канонические документы. GM/schema/native/процесс-владелец не изменялись.
