# Дополнение — spec15 revision25 / plan13 revision25

[T356 SOURCE/PREP](T356-source-review.json) принят отдельным Astra/High; текущая сборка44320b27e, существующий полный сценарий владельца на независимых DATA без генерации мира. [T357](T357-findings.md) единожды достиг2минутного общего лимита:0завершённых кейсов/нетTRX-вердикта, полный журнал666040событий/333014закрытыхинтервалов/12открытых сохранён без квот. Измерения показали36мсна создание сервисов, существенную стоимость повторных файловых чтений/проверок; причина исторического зависания не установлена. До установки ответа/повторного вызова/закрытия результат не подтверждён. После остановки220файлов/60каталогов/поколение совпадают с исходной фикстурой; это не доказательство закрытия.

Новый корень joint-late-owned/a0e2395332bb422faae662dee3e9a652 сохранён из-за неизвестного закрытия; вручную не удалять. Старые защищённые корни, включая ACL-каталог с отклонённым автоматической проверкой удалением, не трогались. Лимит не повышался, повторного запуска не будет без новой обоснованной правки. [Независимое ревью фактического T357](T357-final-review.json) принято после исправления машинного поля ответа на null/неизвестно; других замечаний нет. Исходные пять/Linux не квалифицированы, исторический seed не перепривязан; NativeLineage/Linux-supervisor и Linux-обход сохранены.

---

# Дополнение — spec15 revision24 / plan13 revision24

Последняя проверенная Windows-сборка: `44320b27e77ff01315eec8685f12344f5c12386c`. [T353](T353-final-review.json) отверг обход блокировки при отсутствующем тексте: он терял бы проверки восстановления, поколения и владения. Эти проверки сохранены.

[T354 SOURCE/PREP](T354-source-review.json) принят отдельным Astra/High без замечаний:34новые минимальные файловые фикстуры+26существующих проверок PASS, без подготовки игрового мира и reflection. Девять реальных сравнений подтвердили сокращение открытий4→3,6→4,10→6. Новый путь применяется только к Windows-проверке служебных каталогов; общий/Linux-обход, все позиции проверок, восстановление и NativeLineage/native supervisor сохранены. [Текущие DLL/доказательства](T354-source-prep.json), [финальная проверка](T354-final-source.json).

[T355 выполнен](T355-profile-outcome.json):2/2PASS,82,68собщийзапуск; полныйжурнал281156событий/140578закрытыхинтервалов. Все счётчики операций совпадают с T352, DATA/поколение/ожидаемыйотказ/закрытие сохранены. [Выводы](T355-findings.md), [независимое ревью результата](T355-final-review.json) принято без замечаний (Sol6.1/xhigh). Полная диагностика без квот. Ускорение по времени и причина исторического зависания не заявлены; исходные пять, full-owner и Linux остаются неквалифицированными. Исторический source42seed не перепривязан.

Сохранён T345корень с неподтверждённым закрытием. Также сохранён точный временный ACL-каталог T354, ручное восстановление/удаление которого было отклонено автоматической проверкой (`blocked by policy`); команда не выполнялась и не повторяется через другой механизм. Путь и ограничения — [T354-red-fixture-errors.json](T354-red-fixture-errors.json). Новые независимые фикстуры восстановили свои ACL и удалились; сохранённые корни не трогать.

---

# Дополнение — spec15 revision22 / plan13 revision22

Последняя исполняемая Windows-сборка: `5c25cc3b24e72f1dd32a266df079d42f4c8cead3`. [T351 SOURCE/PREP](T351-source-review.json) принят:6минимальных файловых фикстур+12существующих физических/публикационных проверок PASS, исходная логика совпадает после удаления добавленных таймеров. NativeLineage/native supervisor не менялись.

[T352 реальный замер](T352-profile-outcome.json):2/2PASS; полный журнал без квот, все281156событий/140578пар, DATA/поколение/закрытие сохранены. [Новые именованные таймеры](T352-probe-analysis.json) показали6 264проверки служебных каталогов/7,66с внутри положительного consumer-вызова25,77с. Это измеренный участок стоимости, а не доказательство причины исторического зависания или разрешение удалить проверки. [Выводы/границы](T352-findings.md); [итоговое независимое ревью T352](T352-final-review.json) принято без замечаний. Linux и исходные пять сценариев здесь не запускались/не квалифицированы; исторический seed не перепривязан. Ниже — ранее принятые результаты и история.

---

# Актуальная передача — spec15 revision21 / plan13 revision21

Ветка `1536-crash5-linux-windows-spec-20261019`; канонические [spec](../../spec.md), [plan](../../plan.md), [tasks](../../tasks.md). Исполняемые исходники последнего Windows-замера: `eb8a903ca81e8321e7c50eae88856fef752b12c4`. Ниже сохранена история прежних версий; её слова «текущий» относятся к соответствующей исторической записи.

Правила тестирования сохранены в [docs/testing.md](../../../../docs/testing.md) и AGENTS.md: независимые минимальные фикстуры, типизированные внутренние API и friend assemblies, без reflection приватных/internal членов; полный объём диагностики без квот. Общая реальная фабрика GameEngine выделена без подготовки мира; direct consumer fixture использует независимые immutable cleanup DATA и настоящий выпуск доказательств/проверку поколения. Изменения NativeLineage и native Linux supervisor в последних блоках отсутствуют.

[T347 SOURCE/PREP](T347-source-review.json) и [T348 два реальных теста](T348-final-review.json) приняты. [T349 подготовка измерения](T349-source-review.json) принята:8коротких проверок PASS. [T350 Windows-результат](T350-profile-outcome.json):2/2PASS, полный журнал сохранён, точные счётчики и неизменность220файлов/60каталогов/поколения проверены; [итоговое независимое ревью T350](T350-final-review.json) принято. [Измерение записи](T350-write-cost-analysis.json) составляет около4%положительного и2,2%отрицательного реального consumer-вызова; это не доказательство причины исторического зависания. [Выводы и ограничения](T350-findings.md).

Историческое полное восстановление и исходные пять crash-тестов ещё не квалифицированы; Linux здесь не запускался. Нельзя считать component replay заменой оригинальному/cold процессному сценарию. Старый source42 seed привязан к `778f8c4e...` и не переименовывается/перепривязывается к новой сборке. Не повторять прежний запуск без изменения причины/согласованного плана, не удалять сохранённые корни с неподтверждённым закрытием. Актуальный следующий шаг находится в plan.md.

---

# История передачи — spec15 revision14 / plan13 revision14

Ветка `1536-crash5-linux-windows-spec-20261019`; активные [spec](../../spec.md), [plan](../../plan.md), [tasks](../../tasks.md). Ниже сохранена история; старые номера отборов, лимиты диагностических записей и статусы PREP описывают соответствующие прежние версии. Текущие именованные таймеры и обычные стадии не имеют квоты записей; полный вывод сохраняется.

Исходники текущей исполняемой Windows-сборки: `778f8c4e0fc5c928d1facd6e89a1be1f8f113c9c`; метаданные перед одним текущим cut0: `6987d79c799f410c36fc3c510679df61386a79b5`. Source-bound4DLL и копии зависимостей: [T329 PREP](T329-prep.json). Только создание TrustedLocalFileScope теперь проверяет обязательных предков один раз, затем сам корень; классификатор, native код и все позднейшие границы чтения/целостности сохранены. Причинные RED/GREEN и реальные физические проверки приняты:66неизменённых прежних PASS плюс1исправленный after-open Windows PASS.

Прямой сценарий на настоящих AppliedBeforeStory DATA: [T330](T330-profile-outcome.json),1PASS9.9588457с,3полных доказательства,220файлов/59каталогов и поколение неизменны;18 274записи/9 137пар/0незавершённых, точная копия таймеров вTRX, собственный корень удалён/отсутствует. [Независимая итоговая проверка](T330-final-review.json) принята. PrepareScope включает ResolvePath и конструктор;1170.8769→906.8572мс — один замер общего участка, не изолированное время конструктора и не доказательство устранения исходного timeout. Исторический T327FAIL сохранён.

Новый подлинный Windows source42: [T331](T331-generator.json),1PASS189.520408с,82файла/513711байт/38каталогов, две независимые загрузки и уборка подтверждены. [1784производящих входа](T331-source-inputs.json) восстановлены из Git-рецептов и совпадают с фактическим fingerprint; [DATA/source/DLL binding](T332-seed-binding.json) и [отдельная проверка](T332-binding-review.json) приняты. Старый d41набор целиком сохранён:85файлов/40каталогов,[архив и индекс](T331-historical-seed-preserved.json). Старый manifest не перепривязывался.

Текущий T333: один completed FAIL,12:14.9518693; фактическая ошибка — System.ArgumentException при JSON-значении длиной169321202. Полный resumer-поток не сохранился после ошибки и уборки; его exit/первичный error/игровой verdict неизвестны. Это не подтверждённый600с timeout. [Доступные raw/outcome](T333-profile-cut0-outcome.json) и [source-backed inspection](T334-inspection.json) сохранены. Есть структурно полный наблюдённый cleanup-entry DATA:221файл/1503472байта/62каталога с совпадающими parent nonce/run/seed/builds; это не native/game qualification или перенос полномочий. Обе owned-папки удалены/отсутствуют;4DLL/copies/seed не изменены. Исходное восстановление открыто, cold/default пять0/5, LinuxUNRUN. Следующий локальный блок исправляет постоянное сохранение полных потоков вне удаляемых папок и компактные JSON-ссылки; только дешёвые тесты, без повторного GameEngine/source42/crash.

Linux здесь не запускался. Runtime должен использовать собственную Linux-сборку и native package, свежие DLL/source/package hashes, фактический catalog/PlanOnly и NativeLineage controls перед обычными пятью. Windows fingerprint отражает его рабочие байты (в том числе CRLF); Git-совпадение и Windows DATA не означают готовность Linux runtime. Windows-only source42-generator/seededcut0 на Linux не выбирать. Ни компонентный PASS, ни Windows результат не устанавливают причину прежних Linux52/57.

Штатный Linux-путь остаётся `JointInterfaceCrashProcessSupport.RunNativeAsync` → `NativeLineageOwner.StartFinitePipeAsync` → `StopAndObserveAsync`; исходный owner удерживается до подтверждения завершения, сохраняются фактические identity/status/stdout/stderr/EOF/join. Используется существующий `native/linux/boe-lineage-supervisor.c` и C# `NativeLineageOwner.cs`. В этом блоке launcher, backend, native-протокол и настройки среды не менялись. Ошибку штатной подготовки или остановки следует сохранить как фактический результат.

Подготовка Linux: существующий `scripts/build-linux-supervisor.ps1` с `-IncludeHostGuardian -IncludeTerminalFixture`, затем XML-сборки unit/integration с `BoeNativePackageDirectory` и `BoeRequireNativePackage=true`; полный вывод сохранить. Текущие команды подготовки/controls ниже остаются ориентиром; новый PREP обязан получить собственные фактические отборы и независимую проверку. Не переносить исторические Windows counts как Linux PASS. `tests/selections/1536-crash5-linux-controls.json` и категория `fr063-joint-interface-crash-current` остаются раздельными gate. Облачное исполнение и его результат остаются работой Астры.

---

T318 SOURCE/PREP ready on248ed70603158fa6596ecf3c90e99283d23ea6de: exactDATA importer10PASS+uncappedcollector1PASS, actual9FAIL→9PASS and missing-parent1FAIL/9PASS→10PASS retained; both final XML builds0errors/changedAPIXML0; catalog850/11961 and exact13casePlanOnly0execution.26passive scopes across2files preserve business source. Direct2phase/story cases prepared with independent realDATA roots/fresh production proofs, no GameEngine constructor; immediate AutoFlush diagnostics outsideownedroot/noquota. T318-prep.json/raw5358397E691A16AEC9E4C22D623997DD44C2110ADF6F39A0A4CB48A93A41577C;4DLLbinding and169exactGitblobsource recovery. Independent coherent SOURCE/PREP review pending,2servicecasesUNRUN. Next acceptedPREP→run only2actualcomponents once→preserve/analyze measured fanout/costs/refusals/cleanup→write cause-driven correction before implementation. No new source42/Stage43 for component tests. Original T315FAIL/Windowsfive0/5/LinuxUNRUN unchanged. T317 final independent DATA-acquisition acceptance saved.

T317 distinct fixture acquisition1PASS, test11:27.1815829, producer73/resumer74, actual accepted checkpoint, both EOF/join and2ownedrootremovals verified.2499uncappedrecords/1248completed/3intentionallyunfinished; prepared/appliedexport464.3163/468.5342ms. Prepared223files/1300605bytes, applied221files/1503036bytes.4DLL and source42 unchanged. Full T317-fixture-acquisition-outcome/raw retain both DATA ZIPs; actual applied DATA also fixtures/joint-applied-data. Final evidence review pending. Spec15r6/plan13r6/tasksT318a–d now selects direct fresh phase/story components with independent copies and real validators; no source42/Stage43 per component, no production optimization yet. Original T315FAIL/Windowsfive0/5/LinuxUNRUN unchanged. Continue through actual component evidence and cause-driven correction, no diagnostic quotas or unchanged full retry.

T315 original seeded Windowscut0 FAIL/TimeoutException at unchanged600s/shared820/category15m; compiled3421990a, runtime metadatae5bbe4b4. Test12:34.547; producer73, resumer timed out, EOF/join and both owned root removals verified.2504uncapped records/1250completed/4unfinished; lastbeginJointStory.ValidateStoryProofAsync596.747s. Cleanup was not entered;0captureevents/0archives, so its inner cause remainsUNKNOWN. T316timings/findings and complete T315raw retained. Original220 Windowsfive0/5, LinuxUNRUN; no OS/scanner attribution. Spec15r5/plan13r5 proposes a distinct real prepared/applied fixture-generation spike stopping at accepted checkpoint with diagnostic74, preserving original assertions/budgets; not implemented or qualified yet. No unchanged retry.

# Текущее состояние: spec11 / plan9 seed prototype

T273 prototype PASS: one complete actual generator case/0skip; source42=206.249s, capture/export/open=1.121s, imports=0.105s/0.083s, read-only validation=0.385s/0.299s. 82 payload files/513708bytes; two own copies and source removed/absence confirmed. Immutable artifact retained and independently rehashed; no Stage43/crash/current-owner qualification or resume speedup claim. Compiled ef93fbde7cde3fddd0b0cf20c57102fa785dc330; runtime metadata 22acebccb41a986632cf586b8f8d4b184dbe2099; run 20261020-123808-320-56712-7cb9a4cc15534c0f8827ef726eb139ca-categories. Exact source/DLL/raw/artifact evidence T273-generator.json/zip. Windows0/5, original220 five NOT_RERUN, LinuxUNRUN; T219 historical fixture retained. Final independent gpt-6-astra/high evidence/recovery review ACCEPTED at aea971007e24108a19a6b942f982ececc85fb031. Fresh GitHub recovery d2ed51d00f2c1667f0cac68ff02e5121cd774c6b:329 files/allGitblobs/clean verified independently, zero mismatches; T274-clean-recovery.json.

Artifact in T273-generator.zip under generated/artifact. Extract immutable DATA separately; Open requires current contract fingerprint. The generator is explicit category fr063-joint-interface-seed-generation,1case/6min; it is not the default test path. No unchanged regeneration or crash runtime is part of this slice.

---

# Текущий результат spec8 / plan6

**T259 actual:** one completed FAIL/0PASS/0skip, resumer220s timeout; category15m did not time out. Producer exit73 at414.722s; resumer observations continued to214.292s. Named custody checkpoint32.829s; since then14822 canonical-initial hook observations,0observed canonical/main contention,0publication callbacks. This is ongoing observed read activity, not proof of finite completion or successful I/O. Both children EOF/join, two owned roots removed/absence confirmed; historical T219 fixture retained. Windows0/5PASS, cut0 attempted twice (T254 andT259); cut1…4 not run; LinuxUNRUN. No unchanged retry is authorized.

T260 evidence review/recovery pending. Next bounded candidate: share the existing820s producer+resumer observation window, cap each child600s and retain category15m. No implementation or new runtime by this record; requires next canonical plan/controls/review.

---

# Текущий этап: spec8 / plan6

T254 cut0FAIL: producer395.163s/exit73, resumer220s timeout;0/5Windows PASS. T256 accepted evidence and fresh276-file recovery. T257…T260 add bounded ordinary Windows resumer observations, then one cut0 investigation at unchanged600/220/15m. No Linux execution or production change. T255 remains stopped; historical results below retain their original scope.

---

# Текущая работа: spec7 / plan5

**Windows qualification spec7:** 0/5 cases PASS; 1/5 attempted. Producer600s/resumer220s/category15m. Остальные случаи ещё не квалифицированы; продолжать только по gate T254/T255. Original220 five не повторены, T219/T244 history сохранена; Linux UNRUN.

[Spec7](../../spec.md), [plan5](../../plan.md), [tasks](../../tasks.md), T252…T256. T250 source42 PASS258.780s; independent review/recovery T251 приняты. Следующий шаг — пять отдельно выбранных Windows cut0…4, producer600s/resumer220s и15m/category; код и SOURCE/PREP приняты (compiled652bdd76a,25PASS, reviewer gpt-6.1-sol/xhigh); gameplay qualification отражена выше. Original Theory220/Linux NativeLineage остаются прежними. Ниже сохранена история; новые результаты будут записаны отдельно.

---

# Текущая работа: spec6 / plan4

User waiver2026-10-20 разрешает продолжать без промежуточных согласований. [Spec6](../../spec.md), [plan4](../../plan.md), tasks T248…T251: отдельная Windows source42-проба600s/category12m после policy GREEN и independent PREP. T250 завершена:1 planned/completed/PASS,0failed/skip; source-turn return258.778s, engine return258.780s. Child76132 exit0, EOF/join true, оба owned root удалены и отсутствие подтверждено. Наблюдённое выполнение превышает220s; зависания в этой попытке нет. Это не гарантия времени всех сценариев и не SC-WIN five PASS. T244220FAIL сохраняется; original five не повторены, Linux UNRUN. T251 принята независимым gpt-6.1-sol/xhigh на5149447e0 без замечаний; fresh GitHub recovery261files подтверждена, запись T251-clean-recovery.json. Исходные220s probe/five budgets и Linux NativeLineage остаются прежними; T244FAIL сохранён. Ниже — законченный plan3 checkpoint.
---

# CRASH5 — передача Linux NativeLineage и результаты Windows

Репозиторий: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn
Ветка: `1536-crash5-linux-windows-spec-20261019`.

Активная feature: [согласованная spec5 и предыдущие редакции](../../spec.md), [согласованный plan3](../../plan.md), [tasks](../../tasks.md), [validation guide](../../quickstart.md).
Исходная версия: `1253328971a27ffafe3820ad59a2515ebbd35628`; согласованная spec3: `f22e51730e090e5cdc86531f589bdff05193158d`; согласованный plan1: `6f2d4d48615db910168d6c1a32841a1ffb037147`.
NativeLineage implementation/source review: `5f0c08ae5b9303bca7c0a44b1509edcf89fa974c`. Предыдущий C# source с отдельным Windows-only source42 probe и bounded read counters: `25ef748ce8a0b3c8131e0d3b6e9cbb7107a7d090`; accepted PREP checkpoint `779a8e2a8a071f3e5e420b7ad13820c06d40d134`. Probe не входит в Linux/CI selection. Между51f и25ef production не менялся; новая реализация spec4 описана ниже.

## Текущий checkpoint spec5 / plan3

T240…T243 завершены. Исходники 1e0a5fcd7 и подготовка SOURCE/PREP приняты отдельным gpt-6-astra/high. Адресная квалификация:17 категорий,180 distinct PASS,0skip; это union89 на85f4 +23 на8617692 +68 на1e0a5fcd7 с отдельными source/DLL bindings, а не один общий прогон. Обе XML builds exit0, changed XML diagnostics0; catalog и PlanOnly подтверждены. T244 выполнена один раз:FAIL_CHILD_TIMEOUT220,1 completed/1 failed/0skip, полного возврата хода нет; подробности и cleanup в T244-probe-outcome.json. T245 НЕ ЗАПУЩЕНА: gate не выполнен. Повтор T244 или увеличение лимита этим plan3 не разрешены. Windows5 сохраняет исторический T219 FAIL_INCOMPLETE; Linux UNRUN.

Compiled source: `1e0a5fcd7522839c869e3d603b93fb23d65b238d`. Полный source/DLL/TRX union: [T243](T243-final-prep.json); история причинных RED и исправлений: [investigation](source42-investigation.md). Windows component PASS не устанавливает причину исторических Linux52/57 и не доказывает обычные пять.

Linux: взять итоговый сохранённый commit этой ветки после T246; выполнить fresh native package и XML builds по разделу подготовки ниже. Затем ValidateCatalog, snapshot PlanOnly (текущий Windows discovery17/180), каждый из17 snapshot category отдельно, NativeLineage controls, независимый Linux PREP и ordinary5. Прежний отбор8/90 ниже только исторический. При отказе среды сохранить конкретную штатную ошибку и ownership observations; не менять ограничения среды. Windows-only source42 не запускать.

## Сохранённый spec4 checkpoint

Plan2 одобрен владельцем из bd9c8dd434. Source9c30fc6b8 принят отдельным gpt-6-astra/high: один console post-materialization invocation удерживает исходный owner/lease/generation, сохраняет неизменное доказательство pending snapshot, физически сверяет его перед возвратом и закрывается до repair. Текущее состояние читается физически; NativeLineage и C protocol не менялись. 90 distinct component cases PASS по отдельным документированным runs; SOURCE/Windows PREP принят gpt-6-astra/high на evidence d8791bab6. Единственная разрешённая T234 source42 проба завершилась FAIL_CHILD_TIMEOUT220:1 completed/1 failed/0 skipped; QTE192.405s и клиентская история наблюдались, возврата хода нет. Child EOF/join и owned cleanup подтверждены. Windows5 не повторялись; T219 остаётся FAIL_INCOMPLETE. Linux runtime UNRUN. Подробности: [outcome](T234-probe-outcome.json), [investigation](source42-investigation.md). Согласованная spec5 расширяет следующий этап на held-lease QTE validation; планирование этого расширения не является его реализацией или разрешением нового облачного запуска.


Исторический рецепт spec4 (не текущий отбор): после fresh native package и XML builds той версии предполагалось выполнить `./scripts/test-csharp.ps1 -SelectionFile tests/selections/1536-snapshot-validation.json -PlanOnly -NoBuild` и соответствующий адресный прогон. Историческое ожидание spec4:8 descriptors/90 cases (36 scope +3 caller +22 Meta в трёх группах7/7/8 +2 World +24 World controls +3 source guards); облачный исполнитель обязан подтвердить фактический discovery. Затем прежние Linux NativeLineage controls по разделам ниже, независимый PREP, и ordinary5. Windows-only source42 probe на Linux не запускать. Принятый Windows source/DLL binding не заменяет fresh Linux package/DLL binding.
## Что изменено

Оба обычных Linux child запускаются прямо через новый `NativeLineageOwner.StartFinitePipeAsync` и существующий неизменённый C supervisor в режиме host-v2. Parent читает identity до A2; конечный child может закончить до Started. При остановке проверяются настоящие root/supervisor/status/EOF observations, а незавершённая control write сохраняет неопределённость. Полные stdout/stderr и child environment включены только для finite pipe; прежние worker/terminal defaults сохранены. Исходный owner удерживается до подтверждённого Dispose, в том числе при частичном запуске без PID.

Windows использует оригинальный owned Process, dotnet.exe, child-only TEMP/TMP и независимо прочитанные UTC creation ticks с явным видом токена. Оба публичных entrypoint отвергают неподдерживаемый mode до fixture. Ordinary stages ограничены64 JSONL records/2048 UTF8 bytes each/131072 total; их ошибки вспомогательные. Diagnostic/integrity Linux пути, G01 delivery/capture и игровые assertions не переписаны.

Пять исходных cut0…4, настоящий Environment.Exit73, новый resumer0, current owner, механика, en-GB/en-US contrast и инертный повтор сохранены. Замена launcher не доказывает устранение исторического Linux timeout. Его причина остаётся открытой до фактического облачного запуска; ограничения среды также не установлены как доказанная причина.

## Статус evidence

- Independent SOURCE: gpt-6-astra/high выявил один P2 на51f и принял его исправление на5f: реальный natural-stop/S race теперь координируется в Linux fixture, не обходится ожиданием HostExited. Production после51f не менялся.
- Managed helpers на51f: **37/37 PASS,0skip**,43.624s,4 descriptors, cleanup complete. [Summary](T213-T218-green-summary.json), [plan](T213-T218-green-plan.json), TRX/logs рядом. Это13 finite policy +2 generic source guards +9 Windows/common +6 stage +7 existing managed consumer cases.
- Final PREP на5f: обе XML builds exit0; changed-source XML warnings0. Catalog813/11831 valid, ordinary PlanOnly1descriptor/5cases, contracts4descriptors/37cases; все NoBuild,0executed,cleanup complete. [Source/DLL binding](T217-prep.json). Independent SOURCE/PREP accepted; [review](independent-review.md).
- Windows ordinary5: **FAIL / INCOMPLETE**. Первый run достиг category15m; четыре producer timeout220s до Exit73, пятый interrupted, TRX отсутствует. Runner cleanup complete; fixture пятого сохранён. [Outcome](T219-outcome.json), [summary](T219-summary.json), [raw log](T219-dotnet-test.log). Непроверенные игровые критерии не объявлены выполненными; неизменный прогон не повторялся. Причина исследуется.
- T227 source42 Windows investigation: **1/1 completed, 1 FAIL** по child220s, category не исчерпана. Actual inspection85.8s → terminal capture87.7s → accepted-validation checkpoint161.5s, без QTE/turn return до220s. Это сужает интервал, но не устанавливает причину. Owned child exit-1/EOF/join/cleanup подтверждены. [Outcome](T227-probe-outcome.json), [trace](T227-probe-stages.json), [investigation](source42-investigation.md). Fresh XML/source8326 принят; обновлённые stages **7/7PASS**, catalog814/11833. Это отдельный исторический этап; T228 ниже добавляет ещё два counter regressions.
- T228 existing-hook count probe: **1/1 completed,1FAIL** по child220s; source25, fresh integration XML45.33s/changedXML0, stages **9/9PASS**, catalog814/11835. SOURCE/PREP accepted. К186.508s после validated160.223s наблюдались1740 rollback initial-read callbacks/59distinct/1681repeats; это нижняя граница, не измерение одного метода. Parent exit/join/EOF/cleanup подтверждены, retained root этого run нет. [Outcome](T228-probe-outcome.json), [counts](T228-last-observed-counts.json). Актуальный contract union ожидает40cases (=прежние37+3 новых scalar regressions); полного прогона40 не было и не заявляется. Прежние неизменённые checks не повторялись.
- [Дополнение spec4](../../spec.md) о production validation — **одобрено владельцем из da3efec34; plan2 из bd9c8dd434 также одобрен**. Реализация и её новый статус приведены выше; это не разрешение облачному исполнителю менять игровые контракты. Подготовленный NativeLineage остаётся доступным для Linux PREP/controls/five по текущей передаче; Windows FAIL и неизвестная достаточность оптимизации явно сохраняются.
- Linux native controls и ordinary5: **UNRUN**, ни одна локальная сборка/managed проверка не заменяет Linux qualification. T222…T225 остаются открытыми.
- Historical52/57 остаётся на своём прежнем source/run. Новые результаты не увеличивают этот исторический счётчик автоматически.

## Linux: подготовка

Нужны отдельный checkout выбранного переданного commit, PowerShell7, SDK10/.NET8, Linux x64/glibc, cc/readelf и штатные возможности NativeLineage. Использовать обычные разрешённые возможности среды. Отказ prerequisite сохранять как результат подготовки, без смены backend или настройки обхода ограничений.

Все команды ниже из корня checkout. Source и чистый diff сохранить до build. Package собирается один раз до ordinary5. Изменения environment ограничены текущим процессом и восстанавливаются в finally. Каждая внешняя команда проверяет exit code.

```powershell
$ErrorActionPreference = 'Stop'
$crash5PreviousDotnet = $env:DOTNET_ROOT
$crash5PreviousPackage = $env:BOE_CRASH5_NATIVE_PACKAGE
try {
    git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source.' }
    git status --porcelain
    $crash5DotnetCommand = (Get-Command dotnet -CommandType Application).Source
    $crash5ResolvedDotnet = [IO.File]::ResolveLinkTarget($crash5DotnetCommand, $true)
    $env:DOTNET_ROOT = Split-Path $(if ($null -eq $crash5ResolvedDotnet) { $crash5DotnetCommand } else { $crash5ResolvedDotnet.FullName })
    $crash5PreparedDotnet = $env:DOTNET_ROOT
    $crash5Package = Join-Path (Get-Location) ('TestResults/crash5-native-' + [Guid]::NewGuid().ToString('N'))
    & pwsh -NoProfile -File ./scripts/build-linux-supervisor.ps1 -OutputDirectory $crash5Package -IncludeHostGuardian -IncludeTerminalFixture
    if ($LASTEXITCODE -ne 0) { throw 'Native package preparation failed.' }
    $env:BOE_CRASH5_NATIVE_PACKAGE = $crash5Package
    foreach ($crash5Project in @('BookOfEternityClient.Tests', 'BookOfEternityClient.IntegrationTests')) {
        dotnet build "$crash5Project/$crash5Project.csproj" --verbosity minimal -p:GenerateDocumentationFile=true "-p:BoeNativePackageDirectory=$crash5Package" -p:BoeRequireNativePackage=true
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $crash5Project" }
    }
    & pwsh -NoProfile -File ./scripts/test-csharp.ps1 -ValidateCatalog -NoBuild
    if ($LASTEXITCODE -ne 0) { throw 'Catalog validation failed.' }
    & pwsh -NoProfile -File ./scripts/test-csharp.ps1 -SelectionFile tests/selections/1536-crash5-linux-controls.json -PlanOnly -NoBuild
    if ($LASTEXITCODE -ne 0) { throw 'Linux controls planning failed.' }
    & pwsh -NoProfile -File ./scripts/test-csharp.ps1 -Category fr063-joint-interface-crash-current -PlanOnly -NoBuild
    if ($LASTEXITCODE -ne 0) { throw 'Ordinary-five planning failed.' }
    # Save package-manifest.json, source/native/DLL hashes and exact build/discovery artifacts.
    # Review Linux PREP before running the separate runtime block below.
} finally {
    $env:DOTNET_ROOT = $crash5PreviousDotnet
    $env:BOE_CRASH5_NATIVE_PACKAGE = $crash5PreviousPackage
}
```

## Linux: фактические проверки после PREP

В той же PowerShell session после принятого PREP (сохранённые переменные указывают проверенные пути):

```powershell
$crash5RuntimePreviousDotnet = $env:DOTNET_ROOT
$crash5RuntimePreviousPackage = $env:BOE_CRASH5_NATIVE_PACKAGE
try {
    if (!(Test-Path -LiteralPath $crash5PreparedDotnet -PathType Container) -or
        !(Test-Path -LiteralPath $crash5Package -PathType Container)) { throw 'Restore reviewed PREP paths before runtime.' }
    $env:DOTNET_ROOT = $crash5PreparedDotnet
    $env:BOE_CRASH5_NATIVE_PACKAGE = $crash5Package
    & pwsh -NoProfile -File ./scripts/test-csharp.ps1 -SelectionFile tests/selections/1536-crash5-linux-controls.json -NoBuild
    if ($LASTEXITCODE -ne 0) { throw 'Controls failed; investigate before ordinary-five.' }
    & pwsh -NoProfile -File ./scripts/test-csharp.ps1 -Category fr063-joint-interface-crash-current -NoBuild
    if ($LASTEXITCODE -ne 0) { throw 'Preserve ordinary-five outcome and investigate; do not retry blindly.' }
} finally {
    $env:DOTNET_ROOT = $crash5RuntimePreviousDotnet
    $env:BOE_CRASH5_NATIVE_PACKAGE = $crash5RuntimePreviousPackage
}
```

Controls selection: new finite12 cases и существующие host/bootstrap-output/owner-retention/native-terminal/held-Main controls. Общее число брать из фактического PlanOnly после deduplication, не складывать пересекающиеся категории. Negative controls проверяют удержание Uncertain; их PASS не является положительным cleanup/game outcome. Existing guardian относится только к isolated native control driver. Обычные пять не запускают guardian и не компилируют package внутри cases.

Ordinary обязательно1 Theory/5planned=5completed=5passed,0skip, original root73/resumer root0, независимая identity/nonce, полные исходные assertions, EOF/join/cleanup. Supervisor0 не подменяет root exit. Лимиты220s child +10s stop/join +10s EOF и15m ordinary category сохранены.

Сохранить исходные stdout/stderr, TRX, plan/summary, durations, source/package/DLL hashes, root/supervisor IDs и статус владения. При Uncertain удерживать original owner и каталоги, не объявлять cleanup успешным. При разрыве связи сначала установить исход прежнего запуска, не повторять неизвестный прогон. Не запускать diagnostic G01/G02, integrity4, full suite или CI как часть этой передачи. После облачного выполнения нужен отдельный независимый разбор T225.

## Итог T244

Windows source42 FAIL_CHILD_TIMEOUT220; QTE179.958s и client story append наблюдались, полный ход не вернулся. Child EOF/join и удаление обоих owned roots подтверждены; original five после этой пробы не запускались. [Полный outcome](T244-probe-outcome.json), [raw archive](T244-source42.zip). Linux qualification остаётся отдельной работой по этому handoff; Windows timeout не устанавливает причину исторического Linux сбоя.

T249 SOURCE/PREP accepted by separate gpt-6.1-sol/xhigh atcee572dc65e4c5e1ea59fe0c946bb9aa768f8a93; no findings. Policy17PASS, original/default/extended/five discovery verified, current3DLL hashes match. Archive T249-prep.zip SHA2568A5FE194C6481C2E1F73AD171A3935F7D4C1D32ED76B3017962E7E45B468CA12. T250 not started at this checkpoint; exactly one explicit600/10/10/12m run permitted by spec6 and user waiver. Historical T244FAIL and LinuxUNRUN unchanged.
T317 SOURCE/PREP checkpoint: ddadb036b1bddfa16ef3d85cb7f38fd322000ea4 implements the distinct fixture-only Windows cut0 path; ordinary recovery assertions,600/shared820/15m and existing process owners remain unchanged. Actual14-case controls RED13FAIL/1PASS→14PASS; final selected42/42PASS, plus final-source unit binding4/4PASS; both XML builds0errors, catalog/exactPlanOnly success,0metadata tests. T317-prep.json/raw.zip preserve all evidence and exact4DLL/version/hash binding;140Gitblobs recovered clean from GitHub. Independent Astra/high coherent implementation/PREP review pending. No current-source genuine seed or fixture probe yet; old342-source DATA cannot be rebound. Next: accepted SOURCE/PREP→one matching genuine source42 generator→validate/archive whole old seed/publish+review binding→one explicit fr063-joint-applied-fixture-acquisition attempt. Diagnostic producer73/resumer74 and fixture PASS are not recovery PASS. Continue T318 with fresh production proofs from actual available DATA; no unchanged full retry or diagnostic quota. T315 recovery remains FAIL/open and LinuxUNRUN.

