# Revision49 — implementation decisions following DW10 analysis

Issue1536 / CACHEPORT-1..7. User approved moving from T430 analysis to implementation, independently and without approval waits.

Selected: preserve DW10 mechanism inheritance + domain composition and move current validation onto configurable storage. Merely unsealing the current context leaves private dictionary/lifetime fixed; directly copying synchronous miss callbacks under write-lock conflicts with async source work. Therefore inherited hooks remain synchronous memory operations, while source loading uses an async gate outside cache/coordinator monitors. Distinct storage per validation operation avoids cross-operation authority reuse. Other domain services can share their own storage lifecycle and configured TTL; generic repository orchestration shares that storage's async gate across transient services. No source callback under the cache monitor.

No dependency is needed: .NET8 TimeProvider/ITimer supplies monotonic TTL and idle cleanup. A controlled test clock delivers timer callbacks without real delay. Explicit presence permits false/0/null and caller-provided comparer preserves path policies. Domain copy functions isolate mutable values; storage does not dispose caller payloads. Save/Delete invalidate before and after actual source attempt, including partial failure; next read observes normalized source instead of trusting submitted input. Computing domains keep factories; no artificial repository is added to validation. The real FileSystemManager-backed repository/root writer protocol belongs to existing T427, after this architecture has passed cheap tests/review.

Implementation evidence before final qualification: source2edfaa2ef compiled and all56 distinct cases completed across fail-fast/remaining selections (27PASS/29expectedFAIL); full66payload archive preserved. First implemented d8d8d2bf3 compiled with XML0errors and59/59complete,58PASS/1FAIL, no timeout/owned/runtime cleanup complete. The sole remaining failure is the explicit long System timer regression: dueTime31536000000ms exceeds native4294967294ms. T432-candidate.zip preserves all46payloads/source/binaries and actual failure. The fix normalizes only timer scheduling to whole milliseconds and supported periodic range; absolute/sliding lifetimes remain exact monotonic comparisons. Subsequent verification and the independent review correction are recorded below. Entry envelopes additionally carry original algorithm/input identity, and validation claims the concrete trusted storage owner so backend substitution cannot bypass operation isolation.


## Actual infrastructure verification and review correction

| Evidence | Source | Actual result and scope |
| --- | --- | --- |
| Initial compiling causal RED |2edfaa2ef|56distinct cases across fail-fast plus remaining-only categories:27PASS/29expectedFAIL; all full raw/outputs retained. |
| First implemented candidate |d8d8d2bf3|59/59complete,58PASS/one long-System-timer range regression; exact failure retained and scheduling fixed without changing TTL. |
| Architecture GREEN |b278f152a|59/59PASS, no skips/timeouts, full selection and owned/runtime closing; XML build41.7156878s,0errors/0changed-file warnings. |
| Independent review T433-F1 RED |a6e8c3d89|33PASS/3FAIL in36complete cases: hit-copy child error, ForceFresh and independent overlap all wrongly kept compute count1 instead of2. Carried-core issue, not evidence against DW10 architecture. |
| Narrow correction GREEN |a7847d391|36/36PASS in the affected operation category; hit eligibility rechecked after callbacks, ineligible fresh computations remain permitted without retention. Other storage17/repository9 cases retain earlier GREEN because those modules are unchanged; reviewer explicitly selected this focused rerun. |
| XML audit |a7847d391|259blocks across15changed C# files parse and use multiline blocks/exact documented names; compiler reports0changed-file warnings. Original26 assertions remain identical except Default implementation construction. |
| Catalog and plan, no test bodies |416cafb1d|Fresh XML integration-project build0errors for discovery, unit outputs unchanged and C# closure matches a784. Inventory12174methods/902categories valid, no unmapped or stale selectors. Plan3descriptors/62cases,0executed. No integration game scenario was run. |

The independent full source review found one material issue, T433-F1. Targeted gpt-6-astra/high resolution accepted the exact correction and causal RED/GREEN evidence, with no remaining substantiated finding; see recovery/crash5/T433-final-review.json. The fresh remote-only recovery of5cfb8dae verified50exact blobs and a clean checkout. Final acceptance metadata still needs publication and recovery refresh before T434 closure. Full raw/source/binaries/CRC/SHA records are in T432-*-qualification.json and T433-catalog-qualification.json; prior integration outputs were separately archived before the discovery build. No diagnostics cap, test-budget increase, game/GM/schema/native change or recovery speedup claim. T427 filesystem integration and T429 original600/820/15 remain open.

# T430 — перенос архитектуры DW10 в общий кэш игры

Issue1536 / spec и plan revision48. Это результат чтения исходников и рекомендация по переносу архитектуры, а не утверждение об уже выполненном переносе. DW10 HEAD 691c0f0d9a1f23d445ec7087d05b720a7321c752; прочитанные source-каталоги совпадают с HEAD. В DW10 есть посторонние незавершённые изменения, которые анализ не затрагивал. Общий операционный компонент игры проверен на f6dedbd1ff94c90be8d54774994aa3fe1919184a; к восстановлению игры он ещё не подключён. Нет причин отказываться от структуры DW10: она становится основой рекомендуемого решения.

## Что фактически устроено в DW10

Проверен цельный пример ScreenPreset, а не только классы с похожими именами:

| Уровень | Фактическая ответственность | Источник в D:/Code/DW10 |
| --- | --- | --- |
| Предметный контракт | Abstract ScreenPresetService определяет операции получения, сохранения и удаления. | Dynamicweb.CoreUI/Data/ScreenConfiguration/ScreenPresetService.cs:9 |
| Реализация сервиса | DefaultScreenPresetService получает repository и два IServiceCacheStorage через конструктор. Читает кэш, после успешного Save сохраняет результат в кэш; Delete удаляет данные через repository и очищает соответствующие ключи. | Dynamicweb.CoreUI/Data/ScreenConfiguration/DefaultScreenPresetService.cs:9,100,113 |
| Repository | ScreenPresetRepository выполняет чтение/запись первичных данных в SQL, отдельно от сервисных правил и кэша. | Dynamicweb.CoreUI/Data/ScreenConfiguration/ScreenPresetRepository.cs:13 |
| CacheStorage | IServiceCacheStorage содержит выбранный ServiceCache, очистку, информацию и смену реализации. DefaultScreenPresetServiceCacheStorage связывает InitializeCache/FetchOnNotFound/RefreshOnClear с repository. | src/Core/Dynamicweb.Core/Caching/IServiceCacheStorage.cs:11; Dynamicweb.CoreUI/Data/ScreenConfiguration/DefaultScreenPresetServiceCacheStorage.cs:8 |
| Общая база | Abstract ServiceCache задаёт virtual/abstract точки чтения, сохранения, удаления, очистки и загрузки отсутствующих значений. | src/Core/Dynamicweb.Core/Caching/ServiceCache.cs:17,31,116,124,271,343 |
| Реализации | DictionaryCache — словарь с управляемой инициализацией; MemoryCache — хранение с политикой истечения и invalidation; NoCache — выполнение загрузки без удержания результатов. | src/Core/Dynamicweb.Core/Caching/DictionaryCache.cs:15; MemoryCache.cs:18; NoCache.cs:12 |
| Срок хранения | MemoryCache.CustomExpiration задаёт sliding expiration; CacheItemPolicy содержит absolute/sliding expiration и зависимости. | src/Core/Dynamicweb.Core/Caching/MemoryCache.cs:32,149; CacheItemPolicy.cs:25,54,60 |
| Время жизни объектов | DI регистрирует ScreenPresetService/repository transient, а два CacheStorage singleton: время жизни сервиса и его кэша намеренно разделено. | Dynamicweb.CoreUI/CoreUIDependencyProvider.cs:58–65 |

Таким образом, архитектура сочетает **наследование механизмов** и **композицию предметных сервисов**. Она не требует, чтобы каждый Service наследовался от самого Cache. Есть и кэш без repository: ScreenPresetsByUserIdServiceCacheStorage хранит временные пользовательские переопределения через set/get/clear. Для вычисленных значений игры аналогично допустим factory вместо искусственного repository без собственного источника данных.

## Что ограничивает текущий код игры

OperationValidationService — sealed-фабрика BeginOperation; ValidationOperation — sealed-контекст с собственным Dictionary. Типизированные вычисления, копии, зависимости, факты проверки и завершение операции уже работают, но смена контейнера, политики удержания и общей реализации get/set/remove не предусмотрена. Простое снятие sealed не добавит этих точек расширения: словарь и жизненный цикл останутся внутри private-полей.

T426 поэтому сохраняет ценность как проверенный координатор валидации, но ещё не представляет всю архитектуру, которую предлагает пользователь. Прошли 26 тестов; в причинном тесте 8 запросов дали 1 вычисление. Это не измерение восстановления. Код пока не используется в производственном восстановлении, поэтому сейчас подходящий момент отделить инфраструктуру до массового подключения.

## Рекомендуемая структура переноса

1. **ServiceCache<TKey,TValue>** — общая расширяемая база с явными TryGet/Get, Set, Remove, Clear и Dispose. Защищённые точки реализации позволяют наследникам менять чтение, сохранение, вытеснение и взаимодействие с контейнером. Наличие записи отделено от значения. Это общий механизм, пригодный всем игровым подсистемам, а не тип восстановления.
2. **DictionaryCache / MemoryCache / NoCache** — реализации того же контракта. DictionaryCache получает область владельца; MemoryCache — политику absolute/sliding expiration; NoCache даёт настоящий сопоставимый контроль без хранения. Наследники могут добавлять специализированное хранение и политику, не дублируя сервисную логику.
3. **IServiceCacheStorage<TKey,TValue>** — предметно настроенный кэш с отдельным жизненным циклом, информацией и invalidation. Валидационный storage создаёт отдельную область операции. Кэш сессии или TTL выбирается другим сервисом явно; завершение одной операции очищает только её область, а не общий store других сервисов.
4. **Предметные Service / DefaultService** — сервис объединяет repository, cache storage и правила. Для валидации это развитие OperationValidationService и default-реализация с текущим координатором ValidationOperation; для другой подсистемы — собственный предметный Service с теми же инфраструктурными контрактами. Репозиторий передаётся через конструктор, как в DW10.
5. **Repository** — типизированный доступ к исходным данным предметной области. В игре он опирается на существующий FileSystemManager и его настоящие lease/source-контракты. Он не становится вторым владельцем canonical state и не заменяет NativeLineage/AcceptedTurnAuthorityRegistry. Сохранение через repository остаётся настоящей записью источника; запись в CacheStorage — только обновлением кэша результата.
6. **Политика срока жизни и контекст** — до конца операции, до конца сессии либо absolute/sliding TTL. Срок хранения отвечает за удержание памяти; версии/зависимости/владелец — за допустимость использования. Даже ещё не истёкшая запись с изменившейся зависимостью недействительна. Для исходных recovery validation facts остаётся операционная область с немедленной очисткой при success/error/cancel. Иной сервис может хранить другие данные дольше; старое подтверждение игрового полномочия из-за этого не продлевается.

Названия здесь определяют роли рекомендуемой архитектуры; точные интерфейсы будущего переноса должны быть записаны до реализации. Текущее ядро проверки версий/копирования/fresh fences переносится в эту структуру, а не выбрасывается ради повторной разработки.

## Какие договорённости игры нужно сохранить при переносе

- **False/0/null:** в DW10 GetCache возвращает default, а miss-loader пропускает null; MemoryCache.SetCacheInternal тоже отвергает null. Игра уже различает присутствие null/false и отсутствие записи. Общая архитектура переносится с явным TryGet/Found и при необходимости непрозрачной оболочкой значения, чтобы этот контракт не потерять.
- **Сравнение ключей:** ServiceCache DW10 выбирает OrdinalIgnoreCase для string. Игровые пути и различные политики чтения требуют своего явного comparer; raw path/reader family/strict absence не объединяются. Общая база должна принимать нужную политику ключа.
- **Async:** DW10 синхронно вызывает FetchOnNotFound внутри write-lock в ServiceCache.GetCache. Игровой координатор выполняет async factory/IO вне monitor; этот контракт сохраняется. Новый storage должен поддерживать безопасную работу разных контекстов, не вводя скрытого ожидания собственного in-flight вычисления.
- **Изменяемые объекты:** текущие защитные копии и live seal/input checks сохраняются независимо от реализации storage. Таймер или замена словаря не защищают от изменения выданного объекта.
- **Области очистки:** нынешний _entries.Clear безопасен потому, что словарь личный. При общем backend Invalidate/ForceFresh/Dispose очищают только свой scope; закрытая операция не может вернуть или снова занести запись. Уже проверенное D1 совпадение объявления зависимостей и D2 запрет старого успеха после fresh failure также сохраняются.
- **Сохранение источника:** правила версии root до мутации и после её cleanup/compensation не заменяются простым SetCache после успешного Save. Все живые claim/item/lease проверки, fresh take/publication, native expected-before/readback/settlement остаются прежними. Механизм срока хранения сам по себе их не заменяет.
- **Создание реализаций:** смена backend может использовать typed factory/DI. DW10 TrySwitchCache использует Activator, но архитектура не требует переносить именно этот механизм конструирования. Массовая инициализация всех игровых объектов также не обязательна: выбранный FetchOnNotFound выполняет нужную работу при miss.
- **Доступность наследования:** сам ServiceCache в DW10 публичный и абстрактный, но его конструктор internal (ServiceCache.cs:96). Наследники внутри сборки доступны, а внешнему коду для прямого наследования нужен другой уровень доступа. В игровом варианте доступность базы и конструктора задаётся явно под требуемое расширение; одного удаления sealed недостаточно.

Эти пункты — адаптация реализации к контрактам игры внутри выбранной DW10-архитектуры, а не основание отказываться от неё.

## Предлагаемый порядок работ

1. Записать конкретные ServiceCache/Storage/Policy/Service/Repository интерфейсы и критерии следующего implementation revision. Сохранить текущий T426 как доказанный baseline.
2. Вынести личный Dictionary из ValidationOperation в операционный CacheStorage; добавить NoCache через тот же контракт. Прогнать существующие26 сценариев и общие тесты storage, включая отсутствие/null/false, scoped clear и подмену реализации.
3. Добавить MemoryCache с настраиваемым absolute/sliding TTL и тестовым источником времени. Проверять истечение без Sleep и без многоминутного теста. Подтвердить, что invalidation действует до TTL, разные области не очищают друг друга, а закрытая операция не оживает в долгоживущем backend.
4. Оформить первый специализированный Service и Repository-адаптер для mechanical/continuation DATA. Завершить T427 writer/hook/lease/fresh-fence proof gates; соответствующая source-консультация сохранена, но не является разрешением включить неподтверждённый кэш. Whole-QTE bind/restore остаётся настоящим действием.
5. Выполнить current-source selected/XML/category checks и независимый coherent review, затем один genuine исходный Cut0 с неизменными600/820/15 и полной диагностикой. Сравнить actual DATA executions/GetAttributes/времена; одно переименование/разделение классов не считается ускорением.

Не требуется сразу переносить все сервисы игры, distributed cache, SQL backend, глобальные уведомления и административный UI DW10. Рекомендуется переносить его архитектурные роли и реальные точки расширения с первым измеряемым игровым потребителем. Диагностика не ограничивается. На момент этого анализа новых C#-изменений/сборок/игровых прогонов после T426 GREEN не выполнялось.

# T425 — generic temporary operation cache decision

Issue1536 / spec revision47 OPCACHE-1..10 / source c2c83d9c6228fcda10f4f0d40cd956b33e4427b3. User clarified scope and lifetime: general service throughout game, computed values or successful checks, operation-owned payloads cleared on success/error/cancellation. No global cross-operation cache. Existing explicit autonomous-work permission applies.

Source-informed bounded gpt-6-astra/high consultation is recovery/crash5/T425-validation-registry-consult.json (SHA25649CAA531585135613A38D8C45B62E87825862454BC7FF9FD4616562541118756). Parent inspected actual source, especially mechanical/continuation, the publication owner and TakeJointRepairPublicationAsync. This consultation is architecture input, not independent acceptance of future source.

Decision: cache successful pure DATA bodies of mechanical and continuation, not whole live ValidateCurrent/QTE delegates. Baseline retains seal/item checks and invokes both live wrappers. QTE reconstruction ExportCurrent/RestorePersistedTreatmentRequests binds/restores mutable AcceptedTurnAuthorityRegistry state and directly reads files, so treating it as pure is incorrect. This initial block gains transitive reuse within repeated baseline/QTE calls without promising14QTE->1.

Independent design review identified two validated core omissions, corrected before implementation: per-call direct dependency declaration must match an entry (same key with A->B cannot hit unchanged A); unsuccessful forced-fresh revalidation must not expose old child/parent positives afterwards. Initial protocol deliberately clears all previous entries on ForceFresh and failed/cancelled computation, poisons active ancestors, and disables insertion throughout a fresh subtree. Normal later requests in an open operation may compute new successes. These conservative rules avoid adding a reverse-dependency invalidation engine before the first measured consumer.

Choose conservative full-root content+namespace revision rather than automatic path tracing. BeginValidationMutation/actual MainAccess mutation lifetime is the initial shared seam; add ordinary mutation version distinct from SessionGenerationRevision, before effects and after cleanup. Audit writers and root/owner transitions before enabling. Existing summed per-path observation versions are not a complete, non-repeating operation dependency token. Unknown writer/domain/hook routes remain uncached. Capture versions and exact live lease/admission coherently without new inverse lock order, disk work per hit or IO inside state gates.

Fresh source proof already explicitly precedes BOTH cache takes in AcceptedTurnAuthorityRegistry.MortalJointRepairPublication.cs. ForceFresh must propagate through that call and all required subsequent taken-input/transaction source fences. Retiring entries before writes does not replace authority proof, and operation disposal cannot detect stale positives only after an intermediate publication. Existing trusted-local external-edit assumptions remain; in-memory versions alone do not detect raw external changes. Persistent drift is caught at fresh boundaries; supported callbacks must be excluded or invalidated before they can authorize writes.

Measured baseline: T420 same closed publication1882834 probes/378.8108s vs T4161962816/359.9428s; actual original600FAIL.35 mechanical,31 baseline,14 QTE and13 resources show repeat opportunities, not additive exclusive attribute totals or guaranteed savings. Root/input/lease churn and fresh boundaries determine actual cacheable counts. Distinguish logical request counts from DATA body executions and original live guards. No success or speed claim before measurements.

Alternatives rejected: global/path-only attribute cache (staleness and unbounded lifetime); Boolean around whole authority methods (suppresses mutable effects/guards); initially tracing only snapshot reads (misses FileExists/direct reads/namespace domains); whole-operation read reservation (legitimate writes conflict). Generic service stays independent of these storage choices; other consumers can supply immutable input/versioned subsystem domains.

Implementation proof gates remain explicit: complete writer inventory/lease retirement, eligible actual resumer hooks, complete two-body dependency closure and live OriginalReadEvidence identity split, stamp-versus-mutation/close race and ForceFresh source-boundaries. Resolve and record these with source/test evidence in T427 before enabling reuse. No additional product decision is silently assigned to a future exception.

Superseded r46 causal RED is fully preserved in T422-causal-red.zip and qualification. No narrow optimization was implemented;32 prior tests pass and2 deliberate r46 expectations fail. Those four experiment-specific cases may be retired as cancelled requirements, not relabelled GREEN. Existing32 read-set controls remain. Current archived outputs cannot be overwritten without retention.

# T421 — guarded reservation admission decision

Actual T420 all-path evidence identifies449680 added request-level probes, while original source checks remain. Decision: replace only those added full checks on the held quiescent Guard route with exact live reservation/owner/flow/revision checks in memory; retain every original source and full entry/exit proof, other modes unchanged. Bounded independent Astra/high consultation is recovery/crash5/T421-admission-consult.json. Reject removing original checks, broad mode caching and AsyncLocal-only authority. Persistent raw drift can refuse at final proof rather than r45's added hit check; canonical r46 records this explicitly. Required original control paths and exact type state already exist; no new dependency or GM contract. No unresolved feasibility probe is treated as production evidence.

# Spec12 — seed integration decision

Existing broad fingerprint intentionally includes integration code. Options were narrowing its producer closure, retaining a stale binding, or one genuine generation after final integration source. Chosen: one new accepted-source generation, preserving the existing validation contract and avoiding a second compatibility protocol. Existing natural Stage43 already creates a fresh engine; source private fields are not required downstream. Imported DATA supplies only detached session/request/token/occurrence expectations, while original Engine42 proof remains in the actual-source wrapper. Full current-owner cost remains unknown. Exact-byte fingerprint also binds checkout newline settings: producing core.autocrlf=true,32inputs GitBlobExact/1737 GitBlobLfToCrLf; all1769inputdigests/reconstruction recipes in T278-source-inputs.json independently reproduce the generator fingerprint. Git recovery preserves blobs and artifact DATA, not universal matching working bytes. A changed checkout refuses; separately authorized bounded regeneration is never an automatic fallback or extra attempt in this slice.

---

# CRASH5 spec5 — Phase 0, plan3

Источник: [согласованная spec5](spec.md), [plan3](plan.md), issue1536. Read-only consultation: отдельный agent spec5_borrowed_lease_consultation, явно gpt-6-astra/high; код/тесты/диагностика не запускались и не редактировались. Это решение конкретного borrowed-lifetime вопроса, не независимое принятие готового плана.

| Решение | Основание | Отклонённая альтернатива |
| --- | --- | --- |
| Второй scope factory, общий runner | PendingSnapshotValidation.cs уже содержит entry/final proof, error preservation и instance reservation; меняется владение physical lease, не checker | Дублировать весь validator/cache или передать прежний prevalidated object |
| Existing exact lease + physical admission | QteSceneService.WithOwnedQteLeaseAsync приобретает lease до ApplyTerminalOutcomeValidatedStateChangesAsync; два validations находятся внутри. AcquireCanonicalWriteLeaseAsync синхронно устанавливает ambient registration до await | Вложенное acquisition/recovery, новый MainAdmission, выдача console за browser |
| Nullable RetainedPhysicalAdmission | MainAccess.ReserveWorldRead сейчас отвергает все PhysicalScope. Для borrowed нужен один exact зарегистрированный physical frame из ancestry | Разрешить все physical borrowers либо только совпадение root/SharesAccess |
| Close/reserve атомарны под MainAccess._state | CanonicalWriteLease.DisposeAsync сейчас обнуляет/освобождает stream до MainAdmission.Dispose; BeginClosing имеет check→assignment разрыв; Freeze меняет frozen до проверки refs | Одна предварительная проверка вне общего lock либо отказ после повреждения lease |
| Active mutation tokens в shared access | Boundary guards до awaited hooks не исключают writer, уже прошедший guard. Existing per-path registrations не охватывают DeleteFileCore/directory pruning/recovery и раннюю portable preparation | Root-only path observer, один guard на входе, считать paused writer уже покрытым same-lease test |
| Token вокруг целой mutation envelope | Под _state register/refuse; release в finally после publication/rollback/cleanup. T240 начинает audit всего partial FileSystemManager family и фиксирует finite same-lease inventory; direct PublishLocalFilesAsync/PublishLocalCoreAsync и RecoverTrustedLocalStorage включены в guard-only file map | Менять storage algorithms; держать lock через await; объявлять coverage доказанным без source audit/behavioral probes |
| Две отдельные QTE invocation | QteSceneService preexisting validation2035 и final2055 разделены resource/state writes и RefreshGameStateAsync | Cache через весь terminal outcome либо через runtime refresh |
| Точная copied-context гарантия | Competing entrypoints отвергаются shared validator reservation, retired private lookups — Closed/Invalidated | Обещать идентифицировать любое клонирование ExecutionContext или активный reflection lookup; текущие AsyncLocal этого не доказывают |
| R3/N1,8,32; ≤6 invocation reads | Реальные lookup consumers под существующим lease; acquisition измеряется отдельным интервалом. Предыдущие warm9/cold12 остаются whole-fixture controls | Вычитать preparation из actual220s или сравнивать разные trace endpoints как доказательство ускорения |

Архитектурный выбор определён; полный inventory mutation envelopes — обязательный bounded investigation deliverable T240 до production-изменения. Непокрытый supported path блокирует acceptance; выход за указанные filesystem guards возвращается к review scope. Причинность именно QTE и достаточность для220s остаются runtime-гипотезами; первая проверяется RED, вторая — единственной новой source42 после PREP.

---

# CRASH5 spec4 — Phase 0, plan2

Основание: [spec4](spec.md), [plan2](plan.md), issue1536, source da3efec34. Read-only консультация отдельного `gpt-6-astra/high` (`spec4_lifetime_consultation`) проверила конкретный lifetime/locking риск; не является runtime verification или независимым review готовой реализации.

| Решение | Доказательство в исходниках / rationale | Отклонённая альтернатива |
| --- | --- | --- |
| Lifetime ровно один console post-materialization ValidateGameState | `GameEngine.ValidationAndRepair.cs:227` сначала чинит system files; :240 запускает game-state validation, :244+ payload checks могут писать. Новая repair iteration требует нового source | Обернуть весь repair loop или передать старый activeSnapshotContext |
| Синхронный issuer + original canonical lease | `FileSystemManager.cs:3760` устанавливает ambient registration в caller; :1335/1482/1572/4314 подавляют nested recovery при active ambient | Async factory с потерей registration, повторное открытие owner, самостоятельная browser identity |
| Общий typed reservation с сохранением browser guards | `MainRunFence` уже исключает competing admission/physical sibling/mutation при World reservation; PublicationReadQuiescence сам по себе read-only не является | Считать purpose достаточным; широко запретить этот purpose и сломать opening recovery |
| Entry authentication + final original-dependency proof | `AcceptedTurnAndInkFeathers.cs:3319/4963` повторно проверяют source; `PendingTurnSnapshotAuthority.cs:594/793` перечитывает rollback set. Final scan обязан сравнить entry bytes, не только валидность новой пары | Process cache, mtime cache, одна начальная проверка, detached valid replacement как continuation |
| Не более2 algorithm full-auth; R=3/N=1,8,32 causal contract | Поддерживаемые writers исключены на всё окно, число consumers не требует новых full scans. Current и snapshot leaf reads остаются физическими | Увеличить220s или убрать game assertions; объявить ограничение всего процесса по одному callback |
| Warm3R/cold4R для clean fixture отдельно | `ConsoleRollbackEvidence.cs:49` cohort scan; :56 cold signed authority. `BrowserLocalStorage.cs:90` physical reader обходит ordinary read observer | Назвать2R полным I/O или вычитать acquisition без указания |
| Отказ до возврата результата при durable dependency change | Guard исключает участвующие изменения; final byte proof ловит raw fixture overwrite/delete/BOM/mtime-preserving replacement между consumers | Обещать наблюдение произвольного внешнего edit→restore между двумя scans; это не гарантируется и конституцией не требуется |

Нерешённые технические альтернативы выбраны в plan2. Отдельная граница review: нельзя принимать этот дизайн, если spec интерпретируется как обязательный next-consumer отказ на любую transient external raw race; такой scope потребовал бы отдельного решения, а не тихого уменьшения acceptance. Локальная подготовка не доказывает достаточность оптимизации для source42/five.

---

# CRASH5 Phase 0 — NativeLineage/Windows, 2026-10-19

Источник: [согласованная spec редакции3](spec.md), [issue1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), source12533289. Это research для нового CRASH5 plan; прежние исследования ниже сохранены.

| Решение | Основание в исходниках | Отклонённая альтернатива |
| --- | --- | --- |
| Прямой finite pipe entrypoint того же NativeLineageOwner | C host-v2 уже запускает абсолютный executable, передаёт pidfd до exec и отдельные stdout/stderr. StartOwnedAsync требует GmWorkerProcessHostLaunch; existing terminal API даёт PTY | Fake GM handshake/intermediate host или PTY изменили бы child identity/потоки; копия supervisor создаёт вторую реализацию |
| Identity снимается на B2/A2 gate | GmWorkerHostIdentity проверяет transferred pidfd, supervisor parent и живую incarnation до exec. BirthTick field22 сохраняется через exec | Запрашивать birth по PID после exit или доверять только child JSON |
| Finite terminal без Started допустим | observe_exec может прочитать EAGAIN; reap заметит root exit и запечатает stopping; следующий EOF уже не эмитит Started. Доказано чтением C, не runtime | Только убрать EnsureLive, оставив обязательный await _startedHost |
| Natural-exit/control race обрабатывается только finite policy | StopAndObserveAsync после HasExited может получить broken S pipe; accepted reason root-exited + pidfd exit + supervisor0 + полные status/helper-error отличают завершение от genuine loss | Считать любую ошибку S успешной, сбрасывать sticky Uncertain или менять старые stop defaults |
| Environment/full output — explicit finite options | Pipe startup не копирует host.Environment; bridge capture ограничен64KiB characters. Terminal копирует environment уже сейчас | Глобальный TMPDIR или снятие output cap у всех bridge consumers |
| Original-owner retention per case | GmWorkerOwnedLaunchException содержит owner даже до admitted PID; Dispose отказывается при uncertainty. Каталог fixture не должен исчезнуть под живым root | Хранить только PID, создать новый owner, Dispose/GC как доказательство cleanup |
| Windows identity — StartTime UTC ticks | Локальный direct Process существует у родителя; ребёнок независимо читает self. Linux kernel starttick имеет другой смысл | Записать Windows ticks в Linux BirthTick или менять G01 delivery protocol |
| Finite policy и используемые env/output helpers позволяют локальный TDD | Decision/await не требуют libc/socket/Process.Start; start-info builder и stream reader проверяются напрямую. RED coverage gate предшествует native wiring; для непокрытого поведения нужен tests-only cloud RED до production-правки | Выдавать source guards/моки за native runtime PASS или получать первый RED после реализации |
| Одна ordinary category, разные backend/результаты по OS | Spec T2 требует один Theory/five, отдельные qualification/provenance. Runner позволяет локально выполнять одну category независимо от ciRunner | Предложенная в research отдельная Windows category отклонена |
| Native package готовится до ordinary tests | build-linux-supervisor.ps1 уже создаёт binary+manifest; validator проверяет Linux x64/glibc, executable/hash/metadata. Runner не принимает MSBuild native flags | Сборка C внутри каждого из пяти cases или запуск Linux binary на Windows |
| Stage report ограничен и вспомогателен | Старые явные test checkpoints уже есть; ordinary diagnostic=false не включает capture. 64x2048-byte JSONL достаточно для конечного stage набора | Внешний collector, dump, новый logger на каждый engine message |

Linux ограничения — рабочая гипотеза владельца; причина прежнего220s timeout и policy refusal неизвестна. Ни выбранный launcher, ни известные успешные применения мостика не доказывают устранение зависания crash5. SDK/runtime/preflight availability и native timing устанавливаются fresh PREP/cloud execution.

Category research отдельным gpt-6.1-sol/high: existing host controls8 unique methods/11 literal cases при объединении regression-host+bootstrap-output; с owner-retention10/13. Native terminal4/12, held/systemd-affected4/4, managed-policy5/7. Это статические количества, actual discovery не запускался. Нет прямого source guard по NativeLineageOwner/entrypoint names; два generic GmWorkers source guard метода остаются обязательными. Выбор подробно зафиксирован в plan, старые category metadata сохраняются.

Нет неизвестного архитектурного выбора, мешающего записать план. Runtime-неизвестные остаются критериями проверки, не обещаниями PASS. Внешний API/новая persisted schema не вводятся; `contracts/` этой внутренней адаптации не нужен.

## Исторический research feature1536 (без изменения)

# Phase 0 Research: Complete Wound Materialization and Healing

## Approved terminal binding refinement — 2026-09-29 (#1536)

- Decision: owner-approved RESULT-CLOSURE-BINDING revision1 uses exact GM-authored
  failed-binding result A followed by a separate exact terminal-control echo B.
- Evidence: pressure5/15 and binding13/10 preserve player_success after a wound
  removes position leverage; ordinary binding/strong-binding prerequisites, rather
  than a generic dice-band conversion, determine the bounded permission.
- Strong-binding fixture candidate: signed later dice13/11 with canonical +2
  give17/11, margin6; burden1 leaves effective +1 and15/11, margin4. Both bands
  remain player_success (decisive begins at8). Only setup:true does not meet the
  separate strong prerequisite. Preserve the original force-binding payoff's
  two restricted operations; prove the unmodified original normally validates
  before using this candidate as the regression.
- Strong-binding source prerequisites: register `force_binding` tier3 in both
  original soul combat artTiers and player standardArts before signing; use its
  own matchup lane/risk `force_binding`/`control_leverage`, original cost2 and
  exact none→player-hindered control with sourceOperation=force_binding and
  two distinct restrictions (nearest fixture uses maneuver/binding). With first
  pressure5/15 and canonical+2, harmful margin is only6: equal pressure/resilience
  tiers with clear→strained yield no wound. A genuine rank-I candidate therefore
  needs applied pressure one tier above resilience, with all original authority
  mirrors and pressure costs aligned before capture. Do not proceed from an
  invalid or zero-offer original; B1 validates this candidate at runtime.
- Intermediate validation may project only a genuinely proved final-control echo
  mismatch in a detached view. Other ordinary errors still reject; physical raw
  input stays frozen until its own response and must validate fully after B.
- Independently proved existing cost corrections remain available. Final-exchange
  A may create genuine progress; cold replay derives B with no exchange remaining,
  and B creates no synthetic row. Existing schema and hashes are preserved.
- Current implementation routing: `NeedsSequentialDependentContext` requires
  more than one remaining original exchange. The last-binding refinement must
  route its bounded owner proof with one remaining exchange and recover the
  independently proved terminal B after that genuine exchange closes. Routing
  from the operation family alone must never grant result/control permissions;
  preserve the ordinary cost-only compatibility path and measure its owning gate.
- The current walk also returns before replaying the last exchange's dependent
  correction, rejects progress count equal to remaining exchanges, and rejects a
  completed walk with no next exchange fields. B2–B4 must handle only the proved
  last-binding refinement through these boundaries: real GM A must validate and
  close its original exchange before terminal B is derived; diagnostic arithmetic
  projection must never invent the failed outcome/control. Equality alone cannot
  authorize terminal B, and neither B nor a retry may append another progress row.
- Mixed position/cost fixture candidate: rank I cannot carry both burdens because
  spiritual slot count equals rank. Use rank II, one owned definition with two
  distinct components/slot coordinates, each burden1 for the same operation.
  Before signing, the binding candidate's pressure5/15 with canonical+1 must
  author clear→fractured; equal pressure/resilience gives13 trauma pressure and
  maximumII under controlled/hostile danger. Its ordinary tier2 binding cost2
  becomes3, leaving playerAP6→3→0. The force-binding variant additionally needs
  applied pressure one tier above resilience because canonical+2 reduces the
  first harmful margin to6. These are read-only source-derived candidates,
  not accepted fixtures: B1 must genuinely validate the original and offer rankII
  before selecting the wound; never alter an already accepted prefix.
- Raw carrier research: direct control uses `/activeConflict/controlState`.
  A replacement-wrapper candidate uses
  `/afterlifeSpiritualConflictUpdate/activeConflictAfter/controlState`; the
  `conflictStateAfter` sibling is ignored when `activeConflictAfter` is an object.
  Explicit exchange after-control has precedence even when null, then replacement
  presence, then canonical presence. Preserve those presence distinctions and
  prove the actual effective carrier before granting terminal B. A malformed
  present wrapper must not fall back to direct authority.
  For the two-exchange wrapper fixture, the explicit pressure row must omit
  `after.controlState` if the replacement is to own the final binding control:
  an explicit pressure `none` would override the replacement's `hindered` value
  and make that proposed original invalid before any wound. Retain the signed
  initial control and ordinary snapshot validation; do not bypass precedence.
- Rejected alternatives: automatic physical root rewrite expands client authority;
  general refusal does not complete the approved saved-choice flow. Detailed
  implementation/verification ordering is canonical in plan.md B0–B5/tasks.md.
- B2 implementation seam, independently inspected 2026-09-30: immediately before
  the actual selected wound, use the existing prospective next-source preparation
  inside a discarded conflict-validation probe. It must check the exact frozen
  pre-selection raw layer, one newly checked original last exchange, no missing
  audit or pending requirement, and the corresponding prepared resource batch.
  Read effective position from the real pre-selection mechanics, including older
  wounds. Retain only that diagnostic's exact comparison inputs and rank; neither
  its temporary ticket, projection, time nor allocation becomes execution authority.
  Cold replay rederives the comparison before materializing the same saved wound;
  warm retries retain it with that selection. Never substitute the corrected
  proposed layer, canonical position alone, or a fabricated no-wound decision.
  Failed eligibility adds no result permission and preserves existing independent
  cost/position validation. This implements the approved original-lawful condition
  without a second ordinary validator or a checkpoint schema change.

**Feature**: `1536-complete-wound-materialization`  
**Source issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)  
**Research date**: 2026-08-26

This document resolves the technical choices required to implement the approved
specification. It is intentionally a direct replacement of the loose pre-alpha wound
shape; no legacy save migration or compatibility reader is required.

## R-001: One common kernel with realm adapters

**Decision**: Introduce one closed wound contract, identity/history authority, and
accepted-turn planner. Put setting-specific occurrence, location, treatment, recovery,
and consequence validation behind Mortal and afterlife adapters.

**Rationale**: Stable identity, replay protection, archive rules, effect ownership,
atomicity, and player projection are invariant. Mortal wounds need freely authored
world-specific care, while spiritual wounds need closed strain, art, and safe-cycle
rules. Sharing only the invariant kernel avoids both duplicated lifecycle code and a
false universal damage model.

**Alternatives rejected**:

- Two independent systems: duplicates identity, history, rollback, and UI semantics.
- Model wounds as effects: makes effect removal capable of silently healing a wound.
- One universal treatment/damage table: erases the setting-specific Mortal design.

## R-002: Extend the accepted-mechanics transaction

**Decision**: Compose wounds as a typed input/output inside
`AcceptedMechanicsPlanningContext` and `AcceptedMechanicsPlan`. Use a staged
`WoundAcceptedTurnPlanner.Prepare -> EffectAcceptedTurnPlanner ->
WoundAcceptedTurnPlanner.Finalize` contour: preparation validates opportunity and
owner authority and exports a complete bounded wound source-definition graph plus only
the directly requested root applications as a typed batch of correlation refs; the
effect planner remains the sole allocator of opaque permanent effect IDs, materializes
those roots under distinct derived operation events, and returns the exact result map;
finalization proves exact reciprocal root links and consequence budgets. Reaction-only
descendant definitions persist in the graph without an application ref or preallocated
identity and are resolved later through the indexed exact wound source. A valid plan carries
wound carrier after-images, identity-index after-image, append-only history after-image,
wound-owned effect changes, resource mutations, scheduler outcomes, and pending/receipt
state under one input fingerprint and one canonical write lease.

**Rationale**: `AcceptedMechanicsPlanner`, its validated handoff/cache, exact
before-images, and `CanonicalStateNormalizer.AcceptedMechanics` already provide the
atomic boundary for resources and effects. A parallel wound normalizer would introduce
write-order races and partial commits. The staged contour resolves the otherwise cyclic
dependency between wound source allocation and effect-link validation, while all
after-images still publish together.

The handoff uses a per-source `SourceExportFingerprint`, then distinct
`WoundPreparationFingerprint`, `EffectInputFingerprint`,
`EffectAcceptedTurnPlanFingerprint`, and `WoundFinalPlanFingerprint` stage seals. The
common `AcceptedMechanicsPlan.PreparedPlanFingerprint` is computed only after semantic
agreement and seals every stage, exact subordinate result map and after-image, final
wound binding, resource result, path set, pending/scheduler state, and output binding.
It is never accepted as a caller-supplied constructor value; the cache independently
recomputes it from the detached plan before validation and on take/peek. This makes
cross-plan mixing of otherwise valid stage objects fail closed. A newly created wound
is selected by same-turn `sourceRef`, never by its not-yet-published permanent source ID.
Permanent effect IDs are random inside the cached effect plan, as required by #1535, and
are never deterministically derived from GM input.

Each earlier consumer also recomputes its predecessor seal from detached payload rather
than trusting a fingerprint property: composer for source export/wound preparation,
effect planner for effect input, and wound finalizer for the effect plan.

Direct-root materialization agreement uses no universal numeric power. Prepare derives
`ExpectedComponentCount` and `ExpectedMaterializationFingerprint`; the effect result
derives `ComponentCount` and `MaterializationFingerprint` from the actual created active
effect, and Finalize recomputes that value from the detached effect after-image. The
version-1 fingerprint domain is
`book_of_eternity.wound.effect_materialization`. Its UTF-8 length-prefixed SHA-256 input
is domain/version, exact source realm/kind/id/definition key, effect schema version,
recursively canonical compact application parameters, component count, and every
fully bound component's original ordinal plus recursively canonical compact JSON.
Object keys sort ordinally while component array order remains semantic. The digest is
internal handoff authority, not canonical wound state or player output. Catalog
validation remains the heterogeneous severity/power authority.

**Alternatives rejected**:

- A standalone wound normalizer after effects: cannot prove bidirectional ownership
  before effect publication.
- Direct mutation in `ValidationService`: validation must not write canonical state.
- Best-effort multi-file writes outside the current lease: violates rollback and retry
  requirements.
- A decimal power agreement: no canonical producer exists for heterogeneous numeric,
  string, cadence/resource-relative, restriction, and reaction profiles, so any mapping
  would be lossy and arbitrary.
- A per-profile power-evidence union in the effect handoff: duplicates T019 policy and
  contextual evidence in T020 while still only echoing Prepare's validation; exact bound
  materialization equality is the narrower cross-stage authority.

## R-003: Dedicated identity/history roots and owner-appropriate carriers

**Decision**: Add these common roots:

- `game_state/wounds/wound_identity_index.json`
- `game_state/wounds/wound_history.json`
- `game_state/wounds/wound_commands.json`
- `game_state/control/pending_wound_resolutions.json` when a bounded GM decision is
  still required.

Active wounds remain in owner carriers:

- Mortal player: `game_state/player/wounds.json`;
- named NPCs: new `game_state/npcs/npc_wounds.json`, never `npc_effects.json`;
- anonymous Mortal combatants: `activeWounds[]` inside the exact existing
  `game_state/combat/enemies.json` or `allies.json` combatant;
- afterlife player and persistent actors: `activeWounds[]` in their exact accepted
  profile in `game_state/meta/afterlife_entity_profiles.json`; Shining roster roles
  remain in `game_state/meta/guardian_abode_residents.json` and bind to those profiles
  by exact actor identity;
- the active spiritual-conflict root references an opportunity/result, not a duplicate
  persistent wound.

**Rationale**: This mirrors the proven effect-carrier approach while removing the
current NPC wound/effect entanglement. The index proves one occurrence per active
identity. The history root preserves immutable transition and terminal replay evidence
without turning every owner file into an ever-growing log.

**Alternatives rejected**:

- One global mutable wound array: creates a hot file and weakens owner lifecycle rules.
- History embedded only in active wounds: healed removal would erase replay authority.
- Continue writing NPC wounds to `npc_effects.json`: violates wound/effect independence.

## R-004: Client-owned identity and exact owner coordinates

**Decision**: Reuse the exact-identifier, confusable rejection, owner-resolution, and
promotion patterns established by effects/resources. The GM supplies a temporary
semantic wound proposal reference only. The client allocates `woundId`, transition IDs,
attempt IDs, course/cycle keys, ordinals, fingerprints, receipts, and history rows. The
effect planner, not the wound planner, allocates all permanent effect IDs.

Supported owner kinds are closed: Mortal player, named NPC, Mortal combatant or group
member, player soul, Guardian, Shining resident, radiant actor, and another accepted
afterlife profile. Realm and owner coordinates must match the sealed event and linked
effect target. Anonymous combatant wounds move only through the existing accepted
combatant-persistence transition.

The closed effect-target adapter maps `player_soul` to #1535 `player`,
`combatant_member` to `combatant`, and every other wound owner to its identically named
effect target. It chooses `targetRef` only for an accepted same-turn target and
`targetId` for an already stable target.

**Rationale**: GM-authored permanent IDs or name lookup would permit replay,
cross-realm retargeting, and ambiguity. Existing effect target authority already proves
the necessary owner identities and promotion semantics.

## R-005: A wound opportunity is a sealed maximum, not an automatic injury

**Decision**: Every accepted Mortal producer reduces a harmful exact formal, QTE,
combat, trap, check, hazard, or narrative result to one immutable complete occurrence-
candidate batch; a harmless result produces none. The source-result common accepted
plan checks stable producer operation keys against pending occurrences and durable
consumed receipts, publishes only new candidates to the client-owned pending root in
sealed batch-ordinal order, and the next active pending-turn snapshot seals their bytes
before exposing an opportunity.
Correlation-only source input resolves exactly
one such occurrence; current source state, combat membership, a hazard, dice, or prose
cannot prove that the event occurred. It may repeat only public correlation and never
the hard maximum, guarantee, binding, event authority, receipt, or after-image. The
occurrence seals its exact source snapshot token. The client first verifies the stored
historical event vector under those source coordinates, then rebinds the same complete
ordered coordinate vector to the active decision snapshot during initial composition
and validation. Selected wound semantics remain derived while generic sibling seals are
recomputed. It then derives a
`WoundOpportunity` containing exact event/target/realm/domain evidence, legal severity
range, profile, and any guarantee. Absence of `worseningTarget` means create; worsening
requires one explicit exact active-wound coordinate. The GM may choose none or any
severity up to the maximum for ordinary opportunities. Only a previously materialized
guaranteed trigger requires a wound. The GM also supplies the readable acquisition
narration; the client adds an explicit notification and rejects contradictory
narration/state.

Every accepted `none` or `materialize` decision appends one client-owned receipt and
consumes the occurrence through the same atomic accepted plan. While the same active
pending-turn snapshot is retained, exact cold replay uses its signed pre-consumption
occurrence plus the current append-only receipt/pending partition and emits no command
or transition; changed semantics conflict. A newer snapshot makes the old correlation
stale. A decline has no wound-history transition but remains durably auditable through
its receipt.

**Rationale**: This preserves GM narrative judgment and prevents the client from
inventing wounds. The signed occurrence prevents an available source from being
mistaken for an event that actually happened; the complete event-set seal prevents
sibling-event tampering; the durable receipt closes restart replay even for a decline.
It also makes a guaranteed skill/item/source promise enforceable.

**Alternatives rejected**:

- Always create the computed maximum: turns every eligible transition into deterministic
  injury and breaks training/fictional context.
- Let prose alone decide legality: cannot validate severity, owner, or guaranteed
  outcomes.

## R-006: Closed wound envelope, no wound catalog

**Decision**: Parse a versioned closed proposal containing domain, explicit
`locationProfile`, readable nature/display, severity I-IV, care/recovery state,
complications, consequences, and treatment/diagnosis routes. Validate structure and
registered primitive bounds; do not look up the wound name in a predefined catalog.

`locationProfile.kind` is closed (`anatomical`, `systemic`, `mental`,
`spiritual_axis`, `other`) while its readable locus remains setting-authored. An exact
body-part or spiritual-axis reference is optional and must resolve when present.

**Rationale**: The client needs a finite data language it can validate, while the GM
must remain free to create injuries appropriate to arbitrary Mortal universes and the
fixed afterlife setting.

## R-007: Reuse the effect engine with strict wound ownership

**Decision**: Every mechanical consequence is a normal #1535 effect with
`source.kind=wound` and the exact `woundId`. The wound stores one mandatory bounded
`ownedEffectSources` value: the complete detached definition graph plus separate
`effectId -> definitionKey` bindings for roots materialized directly by a wound
transition. A reaction-only descendant persists as a definition but has no root binding
or preallocated identity. Admission requires exact bidirectional owner/realm/source
agreement. Direct roots have unique stack coordinates and each must produce a new effect
identity. Effect operations cannot mutate wound state. Severity rematerialization first
terminates the complete old active/suspended wound source group and then creates every
new root; full healing finds and terminates all active roots and descendants through an
indexed exact wound-source coordinate.

An original or genuinely new direct root has empty first-create parent
evidence. Every root replacing an exact canonical root during a severity change records
that old same-definition root as its singleton first-create generation parent. The rule
is shared by retained coordinates in worsen, treatment, and later recovery; removed
definitions leave terminal history and newly introduced coordinates remain parentless.
It needs no schema migration because
the identity format already admits a singleton `sourceEffectIds`. Runtime authority
classifies generation edges separately from reaction edges and requires one sealed,
acyclic, non-branching ownership-preserving spine whose retired generations are fully
terminal.

Wound source exports set `Materializable = false`, so ordinary GM `effectChanges[]`
cannot apply graph definitions. The sealed typed batch is a one-shot internal allowlist
for exact roots; downstream definitions remain reaction-only. Every graph definition
has a unique stack key and exact `maxStacks = 1`, with the remaining #1535 stacking
combination intact, bounding simultaneous source-group membership without discarding
refresh/replace/merge semantics.

Version 1 permits zero or one leaf `apply_definition` expansion and no nested wound
expansion. When present, its #1535 `maxExpansion` is exactly 2 (the reaction plus the
reachable leaf), while it consumes one wound expansion budget. A root-bound reaction
target must use the exact legal `replace` policy and share the producer's reconstructed
root-ownership domain. The planner derives each root's ephemeral domain as `base_wound`
or its exact `complicationId`; descendants inherit their producer's domain, and
cross-domain stack/refresh/merge/replace is rejected before mutation. A reaction-created
identity records the producing effect in its first create transition's
`sourceEffectIds`; replacement succession remains separate non-ownership lifecycle
evidence. Complication-owned root sets are pairwise disjoint; selective cleanup traverses
only that same-source, same-domain first-create lineage from declared roots, including
terminal roots, and visits each source-group identity once.
It removes the active complication, its root bindings and reciprocal slots, recomputes
slot use, and prunes only definitions unreachable from every remaining root. Terminal
provenance remains in the global effect identity history; an active effect that would
lose its source definition rejects the transition.

Mortal severity I-IV allows at most 1/2/3/4 independent consequences. Spiritual
severity I-IV requires exactly 1/2/3/4 consequences. Each consequence uses one
registered component/profile and consumes one slot; display text and linkage consume no
slot. Profile-specific severity envelopes cap magnitude, cadence, duration, scope, and
action restriction. The closed version-1 profile list and exact per-severity limits are
normative in `contracts/wound-effects-and-atomicity.md`; they include every permitted
generic Mortal component and eight fixed-setting spiritual axes. A Mortal wound with no
mechanical slot still requires an active complication or care/recovery constraint that
changes its legal lifecycle.

The eight spiritual axes are registered directly as deterministic first-class #1535
component profiles with only `profile_specific` merge and a closed
`{ operation, axis, magnitude }` payload. They are persistent
actor effects in afterlife realms, require an exact wound source link, and cannot target
`spiritual_conflict_side`. They are deliberately distinct from
`afterlife_combat_condition`, whose payload, finite conflict lifetime, side carrier,
and `combatConditions[]` projection describe a transient conflict condition. Runtime
conflict mechanics later derive a typed owner-to-current-side contribution without
creating another canonical condition or changing the persistent payload.

**Rationale**: This preserves one mechanical effect engine while keeping wound
lifecycle authority separate. Persisting only slot effect IDs would lose a legal
zero-slot marker and the definitions needed by later `apply_definition` reactions;
preallocating every descendant would instead create effects before their trigger. The
two-layer graph/root model avoids both failures. Exact slot counting blocks a GM from
packing several mechanics into one prose object.

## R-008: Mortal diagnosis and treatment are materialized routes

**Decision**: Require every Mortal wound to have at least one mechanically complete
treatment route. Hidden routes require a reachable diagnosis path. Route-local
requirements are AND; routes are OR alternatives. Reference exact accepted items,
resources, capabilities, skills, providers, facilities, locations, clocks, and
environmental conditions.

The intra-wound discovery graph is explicit rather than inferred from prose or world
names. Diagnosis paths carry typed `requiresKnownFacts[]` and `reveals[]`; the client
computes a least fixed point seeded by already known routes and public/player-known
complications. Hidden paths require a non-empty satisfied prerequisite, GM-only paths
never prove player reachability, and self/unseeded cycles fail closed. Exact external
world reachability is a separate fresh authority proof so structural parsing does not
scan mutable canonical roots.

Diagnosis attempt evidence has one sealed `success|failure` result. Success reveals the
complete declared fact set; failure reveals none and remains terminal/retry-safe. A
later cure uses its own `author_alternative_treatment` transition, appending exactly one
complete route plus a required reachable path when hidden and one new history row. It
never reuses `diagnose`, replaces prior route definitions, or rewrites old history.

Support three closed modes:

- `procedure`: one sealed check and bounded result bands;
- `course`: ordered doses/milestones with declared interruption behavior;
- `guaranteed`: deterministic result backed by an already materialized capability.

The common planner reserves resources and commits consumption only with the accepted
attempt. A valid failed attempt may consume only its predeclared resources. Mortal
natural recovery uses `progressive`, `requires_stabilization`, or
`no_natural_recovery`, plus an optional deterministic deterioration policy.

**Rationale**: A ready-made medicine list cannot work across arbitrary universes.
Materialized routes preserve setting freedom while making resource use, retry, and
progression mechanically enforceable.

## R-009: Spiritual wounds attach to strain transitions

**Decision**: Extend the existing spiritual-conflict state with a declared danger mode
(`training`, `controlled`, `hostile`, `annihilation`) and per-side wound opportunity.
Calculate maximum severity from:

```text
traumaPressure = harmfulMargin
               + 2 * (appliedArtTier - targetResilienceTier)
               + 2 * (newStrainRank - 1)
               + 3 * extraJumpSteps

< 8  => none
8-12 => I
13-17 => II
18-22 => III
>=23 => IV
```

Then apply the destination-strain and danger-mode hard caps. Natural 1/20 do not raise
the wound maximum. Training forbids a wound absent an explicit earlier escalation;
controlled caps at II. Destination caps are exactly `clear -> none`, `strained -> I`,
`fractured -> II`, `overwhelmed -> III`, and `broken -> IV`. One new wound per side per conflict is allowed; later accepted
opportunities may worsen that wound. An older wound changes only through explicit
re-trauma.

**Source-envelope clarification (user-approved 2026-09-08)**: Ordinary spiritual
actions have no extra source cap, represented by neutral IV when combining caps.
An exact pre-materialized canonical special source may restrict that maximum or
guarantee a wound within every harder bound. Neither cost nor a caller-authored
audit establishes that declaration. Without it, a special source retains ordinary
harmful-strain eligibility and does not guarantee a wound. The source producer
must prove both the prior declaration and the actual trigger; a conflicting
guarantee cannot bypass formula, destination-strain, or danger limits.

**Rationale**: Strain is the existing afterlife harm contour. Basing the opportunity on
its accepted transition avoids introducing HP or automatic post-battle fatigue and
keeps training safe by default.

## R-010: Defeat and soul dissipation stay separate

**Decision**: Every non-training defeat produces one bounded anti-repeat outcome even
when the GM declines a wound. Soul dissipation remains a separate optional winner
choice available only under existing annihilation/authority rules; it is never wound
severity V and never automatic.

**Rationale**: Without a bounded defeat outcome, the loser can immediately repeat the
same aggression. Making injury or dissipation compulsory would contradict the approved
conflict model.

## R-011: Standard spiritual arts and active-healing resolver

**Decision**: Add visible standard arts `spiritual_resilience` / `Духовная стойкость`
(passive) and `spiritual_healing` / `Духовное исцеление` (active), tiers 0-V, to the
existing afterlife art/progression authority. Tier 0 diagnoses but cannot reduce a
wound. A healer may actively reduce a wound only when healing tier `H >= W`.

Resolve the accepted check as `d20 + 2H + validatedModifiers` against
`10 + 2W + complications`:

- margin at least 8 or natural 20: reduce two severities, bounded at healed;
- margin 0 through 7: reduce one severity;
- margin -1 through -4: add one recovery point;
- margin at most -5 or natural 1: no active improvement.

The tier gate precedes natural-20 handling. In conflict the action costs base 5
spiritual action points, uses ordinary validated reductions, has a floor of 2, and is
counterable.

**Rationale**: One final resolver prevents command, roleplay, self, and NPC healing
from drifting. Tier 0 keeps the system discoverable without granting free treatment.

## R-012: Safe-cycle healing and universal natural recovery

**Decision**: An out-of-conflict healing session advances exactly one safe afterlife
world cycle, consumes no action points, and permits one active attempt per wound per
cycle regardless of healer. Self-healing consumes no item or currency; provider
compensation is separate. A failed session still advances the world and applies only
ordinary natural recovery.

Each safe cycle adds `1 + SpiritualHealingTier` natural recovery points. Thresholds
per severity step are I=2, II=4, III=6, IV=8. Excess progress carries forward;
worsening resets progress for the current step. This applies to players, Guardians,
residents, leaders, and other persistent afterlife actors. Ordinary entity progression
may increase the art tier. The accepted cycle key comes from `ProgressionScheduleService`
and `game_state/control/progression_schedule.json`; wound recovery does not use wall
clock time or overload effect expiry scheduling.

**Rationale**: Every entity has an eventual exit from wounds without requiring a
stronger healer to exist. The world-time cost prevents risk-free sequential self-heal
from being instantaneous or outside play.

## R-013: Capability and public service are separate

**Decision**: Store optional `healingServiceProfile` beside an afterlife actor's
capability. It binds provider, realm/location, visible availability, access conditions,
compensation routes, and a 50%-200% price multiplier. Public command prices for wound
I-IV are 25/50/100/200 Ink Feathers, multiplied and rounded upward to a whole Feather.
The exact quote is sealed before confirmation and charged once per accepted attempt,
including partial/failure; cancel/rollback charges nothing. Accepted roleplay
compensation replaces currency but not the same healing resolver or safe cycle.

Protect built-in Guardian `elyara`: Spiritual Healing V, initially visible from first Chaos Sea
entry at `Лазарет Незаживающего Света`, public at 100%, with negotiated
compensation where her character contract permits it. Owner clarification (2026-09-22):
she may accompany the player to the Shining Abode or die. The universal command uses
her current canonical location/access and disappears after death; stale offers are
revalidated before side effects, and bootstrap must not undo relocation/death.
Every materialized Shining
faction must expose at least one resident whose visible primary role is
`healing_support` and whose tier is I-V; the role does not make service public.
The role is stored in the existing `guardian_abode_residents.json` roster while art
and service capability resolve through the exact afterlife actor profile.

**Rationale**: A healer can be known but unwilling, inaccessible, or faction-bound.
Separating capability from service preserves faction play and avoids a mandatory
universal Shining clinic.

## R-014: One shared command application service

**Decision**: Add `/раны` (`/wounds`) and `/лечить` (`/treat`), with afterlife aliases
`/исцелить` and `/heal`, through one C# query/action service consumed by Spectre.Console
and the browser host. The primary list contains current-realm active wounds only;
History is a separate action. Target selection lists Self first, then visible reachable
entities, binds a hidden exact coordinate, visibly disambiguates duplicate names, and
revalidates reachability/consent/state immediately before commit.

Projection is recursive visibility-aware and removes IDs, seals, hidden symptoms,
unknown routes, private NPC data, and GM/validator terminology. Dynamic text is escaped
for console markup and browser rendering.

**Rationale**: Typed names or opaque IDs are not player-usable and are inconsistent
with established guided entity selection. A common application service gives browser
and console semantic parity.

## R-015: Typed validation, bounded repair, and snapshot rollback

**Decision**: Replace loose `JsonElement[]` wound validation with a dedicated
`WoundMaterializationContract`, typed proposal/command composer, strict parsers, source
and target catalogs, transition planner, plan cache/handoff, and repair packet builder.
Bind the plan to the pending-turn session/request/snapshot, exact before-images, accepted
event, owner, effect source/target, resource authority, scheduler cycle, and command
authority fingerprints.

Repair reports only offending semantic paths, closed legal shapes/ranges, and safe
readable context. It preserves unrelated valid response content. A changed event,
target, roll, or snapshot invalidates the packet. Publication uses the existing
snapshot/write-lease rollback contour across wounds, effects, resources, inventory,
characteristics, quests, scheduler/journals, and final output.

**Rationale**: Prompt-only instructions cannot prevent partial state or replay. The
harness can make invalid state unrepresentable and give the GM a bounded repair loop.

## R-016: Direct pre-alpha schema cutover

**Decision**: Remove the loose legacy wrapper/object acceptance, old English severity
names, `canBeImprovedBy`, embedded generated-effect payloads, NPC wound writes to the
effect carrier, and removal-on-heal behavior. Update current bootstrap roots, fixtures,
examples, manifests, prompts, and tests to schema version 1. Do not add migration,
dual-write, compatibility parsing, or legacy promotion. A schema-version-1 wound whose
`consequences` omit `ownedEffectSources`, including the former slot-only technical
shape created earlier during #1536 development, is rejected rather than upgraded.

**Rationale**: The project constitution and user explicitly waive pre-release save
compatibility. Carrying both models would substantially weaken the new authority.

## R-017: Saref memory suppression is not a wound

**Decision**: Treat `memory_suppression` as an independent Saref defeat effect and prove
that wound healing leaves it unchanged. Audit its current runtime materialization during
implementation. If it is only audit metadata, create a separate linked issue before
closing #1536; do not expand the wound kernel to absorb it.

**Rationale**: Memory loss caused by a specific trauma may be one wound consequence,
but deliberate Saref suppression has its own source/lifecycle and must not be healed as
collateral damage.

## R-018: Test and performance strategy

**Decision**: Build reusable wound contract/test fixtures, then use TDD in four slices:
kernel; Mortal; spiritual; commands/providers/docs. Start each behavior with the
smallest `Focused` filter. Run one `Fast` control at a meaningful checkpoint, the
conditional afterlife documentation/`FullValidation` controls, and one `PreMerge`
control immediately before merge. Use explicit bounded timeout headroom when a coherent
selection legitimately exceeds its default; do not remove coverage to win seconds.

Add bounded descriptor counts and indexed identity/source/target lookups to prevent
quadratic scans. Suggested version-1 ceilings are 2,000 active wounds, 20,000 history
rows, 20,000 opportunity-decision receipts, 128 wound commands per turn, 64 repair
resolutions, 32 pending Mortal occurrences with 1-160 accepted events each, 32 treatment
routes per wound, 16 requirements per route, 4 consequences, and 32 transition steps per
accepted turn. The complete source graph adds exact derived bounds of five definitions and five
root bindings per wound, 10,000 pre-turn definitions/root bindings across 2,000 active
wounds, and 160 same-turn definitions/root applications across 32 transitions. These
wound partitions are checked before concatenation with independently bounded generic
effect sources.

The five-definition proof charges one slot to the `event_reaction` producer and one to
every flattened non-marker leaf component. A severity-IV wound may therefore contain a
reaction root, two other one-slot roots, one mechanical downstream-only definition, and
one marker (five definitions, four slots). Four ordinary one-slot roots plus a reaction
and mechanical leaf exceeds the slot envelope before the optional marker is considered.
A zero-edge graph remains legal.

Selective lineage work is linear in the already parsed exact source-group membership:
build the first-create causal parent/child index once, classify each zero/singleton
parent as origin, sealed same-definition severity generation, or persisted
different-definition reaction, and visit each member at most once. Reject cycles,
generation forks, disconnected siblings, more than one causal `sourceEffectId`,
same-definition parents without exact prior-root authority, foreign-source/domain
edges, or current/active/reaction-visited definition keys absent from the persisted
graph before mutation. Fully terminal identities whose definitions were removed by a
legal graph-changing worsen remain historical and need not rejoin a current spine. Legal
replacement succession is ignored for ownership traversal. Statistics prove visited
identities do not exceed the parsed exact source-group identity count; no unrelated
global effect-history limit is invented inside #1536.

**Rationale**: The repository's test runner and mature effect/resource planners already
enforce bounded execution and report artifacts. Explicit limits prevent malicious or
accidental GM payload expansion without constraining ordinary play.

## R-019: Break the Mortal authority cycle with production-only phases

**Decision**: Execute the mutually dependent T067/T068/T069/T070 work as explicit
production-only phases rather than pretending each numbered task can close in one
contiguous block:

1. T068-A builds typed resource preparation, immutable policy/claim/reservation
   authority, and the generation-scoped provisional registry.
2. T069-A builds both typed deterioration-policy factories and their strict-worsening
   classification.
3. T070-A adds canonical recovery-anchor representation plus the sealed wound-stage to
   common-plan overload and allocates anchors only during accepted initial creation.
4. T069-B builds non-replay recovery scheduling and closed typed intents from the
   production-created canonical wound.
5. T067-B seals/persists requests and resolves attempts using T068-A and T069-A.
6. T068-B completes `Finalize(resolution)`, command/pending reconstruction, confirmation,
   cancellation, rollback, and restart behavior from production-created T067-B values.
7. T070-B composes treatment/recovery/re-entry after-images and durable receipts through
   the sole common accepted-plan publisher.
8. T069-C closes durable recovery replay against the T070-B history/receipt evidence.

No phase may expose a test constructor, raw JSON authority seam, caller fingerprint,
alternate writer, or temporary compatibility DTO. The same immutable resource and
deterioration authorities flow forward into the later phases.

**Rationale**: A complete T068 finalizer cannot be behaviorally verified before T067-B
can create a sealed `MortalWoundTreatmentResolution`, while T067-B cannot seal a request
before T068-A supplies its resource authority. Likewise, the T069 scheduler cannot read
or verify canonical recovery anchors before T070-A creates them through the ordinary
accepted-plan path, and its durable replay cannot be proved before T070-B publishes a
receipt. This ordering makes every positive test use production-created authority and
keeps the client harness responsible for making forged state unrepresentable.

**Alternatives rejected**:

- Complete T067-B before T068: creates a circular or incomplete request because the
  request contract requires the full resource authority.
- Complete all T069 scheduler tests before any T070 work: requires hand-written anchors,
  bindings, history, or after-images and violates the frozen recovery authority boundary.
- Mint private requests/resolutions or receipts from tests: makes the exact authority
  paths untestable and would hide lifecycle/rollback defects.
- Add placeholder adapters and replace them later: introduces two competing contracts
  and recreates the fragile half-model explicitly rejected for this feature.

## R-020: Consume treatment item stacks through one pure common-plan projection

**Decision**: T070-B.4 uses one write-free ordered pre-publication projection shared with
the ordinary Mortal normalizer pipeline and the common accepted-mechanics planner. The
projection derives the exact final carrier/index roots immediately before
`PublishAcceptedMechanicsAsync`, then applies every selected `item_quantity` intent in
frozen finalization order. It is not limited to the output of `NormalizeMortalItemsAsync`:
accepted item transfer/materialization/equipment/identity and only the later ordinary
transforms touching the selected item graph are factored into shared pure functions and
replayed in exact production order: quest history -> NPC core -> conditional NPC trade ->
inventory items journal -> item bonds -> item text updates -> NPC item journals. Untouched
companion roots remain exact pass-through members of the projection. Root dictionaries use
nullable `JsonNode` inputs and outputs, with every key retained and null proving an absent
file; snapshot proof preserves the complete
bidirectional path set, file presence/absence, content, and legacy top-level object/array
topology. The B.2 treatment skill projection remains a later common-plan mutation. The pure NPC-core input includes detached
`NpcCoreChangesContract.Authority`
plus exact NPC-trade and training pending-file bytes, and their semantic/byte fingerprints
are part of the seal. The ordinary normalizers and treatment authority call the same
ordinary transforms, and the live final roots must equal the sealed projection before the
common plan applies the B.2 skill projection and item consumption. The NPC-trade transform
receives authenticated, sealed, and fingerprinted `MortalItemNpcTradeTailDisposition.Apply`
or `MortalItemNpcTradeTailDisposition.SkipUntouchedTreatmentContinuation`, reproducing the
current treatment-continuation skip gate without a live registry read. B.2 consumes the
supplied semantic final ordinary NPC baseline while preserving the distinct true live
canonical before-image used for transaction rollback.

The baseline planner exposes the exact base `TransformRegistry` as `quest_history:v1`,
`npc_core:v1`, `npc_trade:v1`, `inventory_items_journal:v1`, `item_bonds:v1`,
`item_text_updates:v1`, and `npc_item_journals:v1`. Its sole loop invokes
`ApplyRegisteredTransform` and records the returned applied ID for every entry. The result
exposes and fingerprints those `AppliedTransformIds`: `quest_history:v1`, `npc_core:v1`, either
`npc_trade:apply:v1` or `npc_trade:skip_untouched_treatment_continuation:v1` according to
the sealed disposition, `inventory_items_journal:v1`, `item_bonds:v1`,
`item_text_updates:v1`, and `npc_item_journals:v1`. Execution dispatches through this
ordered registry so the proof cannot claim an order different from the one applied.
The Apply trade step consumes/removes `UpdateNpcTradeInventoryReceipts`; the Skip step
leaves the post-NPC-core root and command byte-semantically untouched and emits no receipt.

The item phase also owns a shared pure transfer transform. It classifies transfers from
detached complete backup/current carrier roots, including effective post-location roots,
applies them before accepted creation,
removes the exact consumed command rows, and never calls the writing transition service.
The accepted snapshot privately derives creation root receipt/create-transition IDs and
transfer-transition IDs from session, snapshot, turn, exact route or transfer authority,
and ordinal. Creation ordinal preserves production collector order `UpdateInventory` ->
NPC core -> NPC commands -> current location -> offscreen storage. The already validated
route and transfer catalogs and snapshots are forwarded into the snapshot/projection and
are never reread or rebuilt. Ordinary normalization and treatment projection therefore
recompute byte-identical item receipts/history without random ID reallocation or a live
cache lookup.

The treatment response envelope remains closed. In addition to the existing six skill
properties, B.4 admits only `UpdateInventory`, `moveInventoryItems`,
`removeInventoryItems`, `NPCInventoryAdds`, `NPCInventoryUpdates`,
`NPCInventoryRemovals`, and `NPCEquipmentChanges`; every other non-null response property
is rejected. Partial consumption preserves the permanent item identity and carrier while
reducing count. Full consumption clears only supported inline equipment and retires the
identity; a container, quest, bond, or other companion reference without its own genuine
atomic transition authority rejects the entire plan. Repeated claims remain separate
sequential transitions whose identities are derived from the sealed request,
result/finalization, claim fingerprint, and ordinal.

A partial resource-bearing stack uses the existing resource model's strict ratio rule:
every live item-owned resource must be `instance_fixed`, and both maximum and current are
scaled by `remainingCount/sourceCount` with exact decimal and quantum representability.
The common reducer receives private registered capacity intents and validates their exact
history result. Full consumption makes the item a historical owner and retires every live
coordinate. The final item carrier/index roots, resource state/history/owner authority,
skill projection, wound/effect/history/output, and command consumption are one plan and
one normalizer transaction.

The pure boundary uses exactly
`MortalItemConsumptionPlanner.Plan(MortalItemConsumptionPlanningInput)`. The single input
contains the turn, upstream baseline fingerprint, complete detached carrier/companion
catalog roots, parsed identity state, ordered commands, resource definitions/state, and
attempt-derived capacity source/policy evidence. The result exposes only detached carrier/
index after-images, ordered identity transitions, registered capacity intents, terminal
owners, issues, and a complete-graph fingerprint. Invalid planning exposes no actionable
subset. `ResourceHistoryState` is intentionally excluded because replay/history remains
the sole responsibility of the common reducer.

The snapshot proof surface owns detached cloned DTOs/fingerprints and exact path/presence/
topology evidence rather than retaining a planner result. Equality is bidirectional across
every carrier, command, identity, and companion path; omitted frozen and extra supplied
paths both reject. Parity covers every tail sidecar and both legacy vehicle object and array
roots.

This is a closed-envelope, client-owned publication refactor: it introduces no migration,
compatibility path, public request/response surface, or GM-authored contract change.

The final pre-publication projection is mandatory because accepted item normalization is
followed by other ordinary normalizers before common-plan publication. NPC inventory and
the plan-owned treatment skill projection share `npc_core.json`, while inventory journal
normalization can rewrite `items.json`; sealing only the earlier item-phase output would
therefore reject or erase valid same-turn work. The planner first verifies the complete
ordinary live baseline, then composes the B.2 skill projection and item consumption into
one exact common-plan root and independently re-derives the final skill catalog. It never
lets one whole-root producer overwrite the other.

**Rationale**: Calling `MortalItemTransitionWriter` would write outside the common
transaction and currently supports terminal whole-stack consumption only. Planning from
raw pre-normalization roots could erase same-turn item work, while ignoring item-owned
capacities would leave invalid live resource authority. A shared pure projection makes
the state transition deterministic, testable before writes, and reusable by the ordinary
item writer instead of creating a wound-only item implementation.

**Alternatives rejected**:

- Reject every partial resource-bearing stack: contradicts declared quantity semantics
  and makes otherwise valid stacked supplies unusable for treatment.
- Round proportional capacity values: invents canonical resources and makes replay depend
  on an unstated rounding policy.
- Aggregate repeated selected claims: loses per-selector authority and changes durable
  transition history.
- Apply live JSON patches after item normalization without a sealed projection: creates a
  mutable publication-time plan and a second item mutation authority.
- Keep a treatment-only item writer: duplicates item identity/resource lifecycle logic and
  can diverge from crafting, transfer, and ordinary consumption.

## R-021: Persist minimal normalized roll source, not a full effect snapshot

**Decision**: `EffectRollContributionResolver` owns one bounded, versioned,
detached authority containing every active common `roll_modifier` row before context
filtering. Each ordered row contains only effect/component identity, realm, exact target,
complete registered operations, contribution, and closed scope. Live, fresh, and detached
treatment resolution use one validation/filter/reduction core. Detached skill usability is
derived only from the recursively valid selected requirement witness; fresh validation
recaptures the source from accepted mechanics and is the canonical origin check.

**Rationale**: Persisted compact contribution rows are already reduced and cannot prove
that scope filtering was performed. A normalized source lets detached replay reproduce the
same semantics without storing display text, hidden descriptions, owners, carrier roots,
arbitrary payloads, or internal snapshot layout. Ordinary hashes establish integrity only
relative to the submitted source, not its canonical origin, so cold recovery must retain
fresh comparison and transactional restoration of die, Fate, and resource registries.

**Alternatives rejected**:

- Persist the full `EffectMechanicsSnapshot`: retains unrelated and potentially hidden
  data, couples durable replay to an internal projection, and still provides no origin
  authenticity after a joint reseal.
- Keep a detached reducer over compact contribution rows: duplicates gameplay reduction
  and cannot verify actor, realm, operation, or exact-skill filtering.
- Add a secret MAC or external authenticated snapshot: unnecessary new key/snapshot
  infrastructure for the current local pre-release trust boundary.

## Live spiritual source and decision ordering — 2026-09-08

**Decision**: Use one original player turn and one final common publication,
with client-owned internal GM continuation over a frozen causal prefix when the
wound envelope becomes known. Separate pending spiritual decision evidence from
append-only accepted spiritual receipts; leave the closed Mortal schemas intact.
The concrete lifecycle contract is `contracts/spiritual-wound-live-turn-boundary.md`.

**Rationale**: The existing raw resource path projects conflict owners before
effect/wound preparation, but actual resource transition and effect transcript
proof completes later inside the common planner. The later filesystem state
validation is not prior proof. Extract the existing complete checker and the
planner's source intermediate as production-consumed boundaries, then finalize
strict source authority before the GM decision. Current conflict replacement
logs support multiple new exchanges: wound-dependent suffix work cannot be frozen
as already valid under pre-wound mechanics. Keep original action/die/resource
coordinates and apply registered effects to the detached candidate in order.

**Alternatives rejected**:

- Commit source now and require the next player action for its wound: introduces
  an unintended visible delay and separates acquisition from its causing turn.
- Nest two canonical subturn publications inside an outer transaction: adds
  unnecessary rollback/identity complexity compared with a retained intermediate.
- Rebuild the whole nominally pure plan for each continuation: can allocate new
  resource/effect identities and repeat turn-wide triggers.
- Freeze the whole multi-exchange batch before any wound choice: can accept a
  later calculation under stale actor effects; atomic publication does not imply
  a universal end-turn activation rule.
- Reuse the generic-looking Mortal receipt path for spiritual rows: its concrete
  parser, identities and fingerprints are a closed Mortal contract.

**Implementation evidence boundary**: Source inventory and exact call-seam reads
support this design. The required overall-zero-error signed fixture has not yet
been executed, and arithmetic overflow is a risk, not a reproduced bug. Current
ordinary-art Fast/Full controls pass; historical failing notes are not a present
blocker. T081-A..E keep live callers and full source mechanics explicitly open.

### Durable continuation and receipt decision — approved 2026-09-22

**Decision**: Persist unfinished same-turn continuation and accepted history in two
different closed version-1 roots. `pending_spiritual_wound_decisions.json` owns only
the original snapshot authority, retained source prefix, staged decisions, decision
cursor, detached candidate evidence and preserved draft needed to resume the current
turn. `spiritual_wound_opportunity_receipts.json` owns append-only conflict-instance,
source and final-decision evidence and is written only by the one common accepted plan.
An explicit `none` decision creates a receipt without a wound transition; a
`materialize` decision must bind exactly one matching wound/history transition.

Cold recovery reconstructs authority from the original pending snapshot and canonical
evidence. A persisted hash is comparison data and never replaces origin validation.
The final accepted plan consumes the pending packet and publishes the receipt, wound,
history, effects, conflict and narrative together. Exact replay emits no command,
resource spend, transition or notification. Rollback restores the original existence
and bytes of both roots and every other touched path.

**Rationale**: A single mixed queue/ledger would make an unfinished GM continuation look
accepted or require post-publication sealing. Separate roots preserve the distinction
between resumable work and durable history while the common transaction preserves
atomicity across source, decision and wound state. Reusing the Mortal root would also
violate its closed physical-occurrence schema and different lifecycle semantics.

**Alternatives rejected**:

- Put spiritual rows into the Mortal pending/receipt schemas: their closed fields,
  fingerprints and occurrence lifecycle do not represent spiritual conflict instances.
- Emit a durable receipt when the continuation packet is saved: records acceptance
  before the original turn passes final validation and publication.
- Keep continuation authority only in memory: cannot resume the same turn after a cold
  process restart and cannot prove the original rollback baseline.
- Publish source/resource state first and seal the decision afterward: creates a second
  writer and permits a partial accepted turn.

## Resolved research conclusion

All design-critical unknowns are resolved. Implementation can proceed without a
clarification gate. File-level refinements may be made during TDD when nearby code
proves a more appropriate partial-class split, provided the canonical paths, authority
boundaries, approved mechanics, and no-migration decision above remain unchanged.

## C4 transport refinement — 2026-09-26, approved revision2 (#1536)

- Decision: extend existing repair request/ready and worker task/proposal with
  one closed spiritualWoundContinuation envelope. Narrative remains response/
  timestamp only. Source: actual GameEngine.PrivateImplementation, file helper,
  GmWorkerModels/Delegator and SpiritualC2PrivateAdapter inspection.
- Rationale: neither current ready nor worker proposal transports the existing
  typed wound decision. A new root duplicates persistence/cleanup; narrative or
  note as an implicit command weakens closed contracts. Both clients already
  converge on GameEngine, so no new HTTP transport is needed.
- Durable choice: independent Astra XHigh found that a dependent failure leaves
  only uncommitted physical command bytes. Existing checkpoints cannot identify
  that frozen choice on restart. Owner-approved pendingSubmission in the existing
  checkpoint retains the selection and exact allocation prefix, with real-owner
  replay and atomic advancement/clearing. Public correlation is not authority.
- Remaining separate C4 risk: a C3 receipt identifies published spiritual work
  but does not prove the complete common publication/output or story finalization.
  The cold-after-success requirement remains open; transport work does not claim
  to solve it. This qualifies the earlier resolved-research conclusion for the
  later live lifecycle integration, without changing approved gameplay rules.

## Mortal recovery publication revision1 — 2026-09-29 (#1536)

- Decision: use a private recovery continuation through existing wound/effect
  stages and the common accepted plan; sole normalizer remains the writer.
- Rationale: treatment continuation demonstrates the required sealed stages,
  but treatment reservations/skill/item authority do not authorize recovery.
- Rejected alternatives: synthetic create/treatment, direct history writes,
  hash-shaped caller records and a separate receipt/control file.
- Owner approved the exact MORTAL-RECOVERY-PUBLICATION revision1. A cadence
  contributes one progress point, threshold lowers one tier, I threshold fully
  heals, overflow follows the declared flag, and one adverse result consumes all
  newly elapsed adverse ordinals. Original epochs stay fixed; durable consumed
  ordinals prevent repeated application and a skipped next deadline.
- Replay must authenticate full history before live-clock reads and return the
  original receipt under a fresh binding at the same signed minute. Healed
  archive selection authorizes replay only, never a fresh recovery operation.
- Consultation: bounded read-only Astra High examined existing registry/stage/
  common/normalizer hooks. No unresolved product choice remains in this slice;
  full death-contour consumption stays outside acceptance.
