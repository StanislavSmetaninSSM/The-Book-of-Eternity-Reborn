# r68 — Что занимает оставшиеся секунды восстановления

Issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
spec REM1..8, tasks T519..525. Главная оставшаяся стоимость — массовый файловый
допуск и свежие наблюдения путей. Дополнительный кэш игровой арифметики имеет
существенно меньший резерв. Новая оптимизация в этой задаче не реализовывалась.

## Новый текущий замер

Предыдущий ordinary OFF r67: **163,3117708 с** первого восстановления. Его таймеры
не разделяли большой115-секундный промежуток. Поэтому добавлен sealed пассивный
аккумулятор существующих именованных scopes: без произвольных callback, дискового
вывода внутри операций или подавления кэша. По умолчанию он выключен; в этом
прогоне включён только на первом восстановлении resumer.

Новый профиль qualified source8a2b8db93 / metadataacce19b54: первое восстановление
**173,6956649 с**, корневой таймер173,6943117 с. 561 имя, **4 023 919 завершённых
scopes**, окно закрыто, открытых/поздних0. Полный исходный тест PASS276,1231591 с;
600child/820shared/15category сохранены. Это новый замер с профилировщиком, не
точная разбивка старых163с и не контроль производительности одной сборки.

Семь непересекающихся marker gaps нового прохода складываются точно в173,6956649:
начало0,1621826; первый callback0,0000945; до custody17,0282576; custody callback
0,0000227; от custody до accepted125,0253317; accepted callback2,1489313; завершение
29,3308445 с. Теперь внутренности этих окон имеют текущие именованные измерения.

## Крупные функциональные участки

| Именованный участок | Вызовы | Суммарно, с |
|---|---:|---:|
| PublishCurrentMortalJointRepairAsync — подготовка и публикация механики | 1 | **71,8901** |
| ValidatePostPublicationAsync — проверки после публикации | 2 | **49,5743** |
| FinalMaterializationLeaseAndCleanup — завершение истории и очистка | 1 | **29,3176** |
| CreateJointRepairCommonPlanAsync — внутри первого участка | 1 | 22,8112 |
| NormalizeAndValidateJointRepairAsync — внутри первого участка | 1 | 29,7880 |
| ValidateGameStateAsync — внутри post-publication | 2 | 24,1578 |
| CleanupEligibleTerminalAsync — внутри завершающего участка | 1 | 22,2739 |

Это inclusive scopes: вложенные строки не складываются с родителями. Два вызова
post-publication выполняются на разных этапах до/после progression; из самого
числа2 не следует, что второй безопасно удалить. TryReadCompletedContinuation
23,8966 с также оборачивает часть этих проверок, а не добавляет независимые24с.

## Что именно дорого внутри

| Низкоуровневый участок первого восстановления | Вызовы | Inclusive, с |
|---|---:|---:|
| TrustedScope.Probe.GetAttributes / File.GetAttributes | **1 036 393** | **56,954556** |
| TrustedScope.ValidateDirectories — цепочки предков | 197 584 | 47,863193 |
| TrustedScope.ValidateFile — допуск файла с предками | 89 188 | 38,267672 |
| TrustedScope.Construct — создание очередных grants | 108 226 | 21,591281 |
| CanonicalPath.WindowsMetadata | 80 120 | **24,511423** |
| WindowsMetadata.DirectoryRefreshAndCachedRead | 348 307 | 18,746016 |
| MainAdmission.Validate.Guard — весь guarded проход | 29 400 | **31,548973** |
| MainAdmission.Validate.GuardCheck — непосредственно guard | 29 400 | 20,772382 |
| MainAdmission.Validate.WorkerInventory | 29 400 | 5,644191 |
| MainAdmission.Validate.PersistenceRead | 29 400 | 5,074100 |
| OperationFileData.FreshFence | 38 | **19,880140** |
| OperationFileData.FreshSource — внутри этих fences | 4 782 | 19,532511 |
| ValidationReadSet.ExitProof / ExitSource | 19 / 2 449 | 9,247744 / 9,033732 |

Здесь тоже много вложенности: 56,95+47,86+38,27 — НЕ независимые затраты.
Аналогично Guard включает GuardCheck/Inventory/Persistence, а FreshSource входит
в FreshFence. OrdinaryRead async40,1079 с и sync36,2201 с включают существенную
часть тех же проверок. Физическое открытие FileStream лишь2,5506 с/17 720 раз;
оно не является главным кандидатом.

Точный повторяющийся механизм виден в коде:

- `TrustedLocalFileScope.cs:122`: каждый constructor заново проверяет grants;
  `:256` вызывает ValidateDirectories, `:535` проходит цепочку до корня тома,
  `:590` исполняет File.GetAttributes на каждом наблюдении.
- `FileSystemManager.OrdinaryReads.cs:119` и `:303`: ResolvePath + новый scope,
  затем допуск; далее OpenValidatedOrdinaryFile повторяет ValidateFile, а
  CompleteOrdinaryRead снова проверяет файл и canonical safety после чтения.
  Между этапами есть open/read/await/тестовые callbacks, поэтому их нельзя
  объявить одним неизменным окном и убрать все повторения.
- `FileSystemManager.cs:809–844`: ResolvePath выполняет физическую Windows-проверку,
  а не только вычисляет строку. DirectoryInfo.Refresh находится в
  `WindowsPathMetadataGuard.cs:322`. Кэшировать одну строку пути почти бесполезно:
  из25,0204 с ResolvePath24,5114 с занимает именно metadata guard.
- `FileSystemManager.MainRunFence.cs:412–432`: guard, отсутствие workers и чтение
  main-run record выполняются при каждом допуске. Имеющийся directory cache
  уже устраняет дубли ВНУТРИ этого одного вызова; продлить его на весь ход —
  изменение контракта свежести, а не простое подключение кэша.

Счётчик GetAttributes агрегирован по имени операции; количества уникальных путей
в нём нет. Поэтому56,95с — измеренная область, не доказанная целиком устранимая
экономия. Для будущего изменения потребуются отдельные запросы/источники/попадания
по выбранной файловой фазе, на дешёвой фикстуре до полного прогона.

## Что безопасно пробовать через существующий сервис

**Приоритет1: DATA наблюдений каталогов в одной синхронной фазе файлового допуска.**
Распространить уже используемый принцип invocation-owned observations на повторные
проверки shared ancestors в ordinary read / canonical safety. Хранилище — новый
адаптер к существующему ValidationOperation/ServiceCacheStorage, не отдельный глобальный
словарь. Ключ: точный owner текущего запроса/фазы, исходный валидированный путь,
backend и семантика запроса/reader family. Значение — наблюдённый EntryKind или
PathMetadataObservation, не разрешение на чтение и не `Validate=true`.

Граница короткая: один synchronous admission без await, open/read, arbitrary hook
или эффекта. Новый этап/open attempt/post-read — новые наблюдения. Ошибка закрывает
контекст, значения не переезжают в другой запрос; mutation/generation/worker/fresh
режимы и неподдержанные/наблюдаемые маршруты сохраняют прежний fallback. Проверки
native handle, main/worker owner, leaf/absence и итоговые source fences остаются
свежими. Не подменять разные Win32/.NET semantics общей записью без доказательства
эквивалентных отказов. Это безопасное направление с тестами контракта, не уже
доказанный размер выигрыша. Сначала проверить, сколько повторов находится именно
в разрешённой короткой фазе; только их часть57с можно считать резервом.

**Приоритет2: более широкий batch/read window для файловых наблюдений.**
Именно он может убрать большую часть повторных полных проходов, если узкое окно
даёт мало попаданий. Нужен явный протокол стабильности и новый адаптер существующего
сервиса: exact lease/registration/root version/reservation, отсутствие конкурирующих
writers, сброс при любом эффекте/worker/generation/failure и свежая проверка перед
эффектом/успехом. Внешняя замена файла или reparse point не обязана менять внутренний
epoch, поэтому одного epoch недостаточно. Сегодня такой общий протокол не доказан.
Нельзя просто держать EntryKind/«lease валиден» все163с или расширить eligibility.
Это кандидат на отдельный архитектурный шаг, не готовый безопасный bool-кэш.

**Более узкий резерв: wound-bundle DATA.** 58 DetachedCopy/constructor стоят
**6,2714 с**; prepared validation1,0894 с, final validation3,0478 с и detachment
0,8171 с вложены. В CommonAuthority.Bundle повторный DetachedCopy используется
даже при ComputeFingerprint. Можно исследовать повторное использование вычисленных
проекций через existing service: private owner + полное содержимое четырёх графов,
структурные зависимости, lifetime одной операции, независимые выдаваемые копии.
Сохранить seal/input/prepared/effect/final проверки; не кэшировать HasValidSeal=true.
Верхняя измеренная область всего6,27с, реальные сбережения меньше. Для цели100с
это не следующий приоритет.

## Где кэш уже работает и что нельзя вычеркнуть

QTE:14 запросов,2 source calculation (2,6911 с),12 reuse; actual reconstruction
1,8118 с, replay источников на hits6,7658 с. Большие13 RecomputeQteAsync20,9466 с
включают source/authority/read-set/fresh-exit работу, а не13 полных новых расчётов.
Повторный кэш расчёта не решает эту стоимость.

GameState:24,1511 с общего ValidateCore в двух проходах;8 кэшируемых фаз9,1950 с,
их source validation2,3325 с. ClientOwned/Realm уже имеют66+366 fast hits. Превратить
два полных ValidateGameState в cached verdict нельзя: progression/история меняются.

Cleanup: prepared namespace12,1059 с, из них ValidateStory7,9623 с. Уже включено
reusePreparedProofData:true;5 CleanupProofData.Window7,3063 с,82 Fallback16,7431 с
во всём первом восстановлении. Fallback вызывает ReadStableRecoveryDataAsync и
может уже использовать обычный read-set; это не82 доказанных cache misses.
Количество само по себе не разрешает распространить prepared-mutation capability
на чужие фазы. FreshFence38 и ExitProof19 нужны для сравнения точных bytes/absence/
mtime и generation перед публикацией/успехом; их цена не даёт права вернуть старый
снимок вместо проверки. Если улучшать, объединять допустимые наблюдения/серии
чтений с сохранением тех же финальных сравнений, а не кэшировать исход fresh proof.

## Ограничения, проверка и результат

Кэши не отключились от нового профилировщика: на первой cumulative отметке ровно
26 950 contexts /592 900 requests /269 500 queries /323 400 hits; ClientOwned/Realm
66/366 fast hits, как в принятом r67. Эти cumulative counters включают setup до
первого окна и не являются полным census всех File.GetAttributes.

+10,3838941 с к прежнему OFF не выделяют точную цену диагностики. Resumer CPU
186,0625 с: user85,984375/kernel100,078125; раньше177,453125 =77,5+99,953125.
Основной рост наблюдается в user CPU, но источник/INPUT/профилирование и условия
изменились; это не причинный A/B контроль. GC delivered3410,28ms на181,0062661 с
интервалов/181sample, не lifetime coverage и не прибавка к вложенным таймерам.
Producer CPU91,546875 с; GC388,457ms на90,0000637 с/90sample.

317/317 дешёвых случаев PASS, meaningful unwired RED7PASS/3FAIL, свежие XML builds
и8uniform DLL; новый source42/две независимые загрузки; исходные mechanics/repeat/
bytes/oracles/native73+0/EOF/join/cleanup PASS. Аккумулятор не имеет quotas, считает
каждое завершение; сырые данные только на D, в Git исходники и этот компактный отчёт.

Текущие561строка/все markers/source pins/runtime в D-only
remaining-recovery-r68-20261024/T525-current-profile-summary.json; полный последний
архив T524-profile-raw.zip7 750 039B, SHA256
08E900D65D1E3C1C6BCB7E06206647FAC0E8A9D918DB43825D8440CB7B41AD82.
Независимая проверка всех33members/144nativeJSON+2text/всех561timers пройдена;
финальное ревью отчёта и точная latest-measurement retention пока выполняются.

Следующий оправданный шаг — **крупная файловая фаза и совместное использование
наблюдений через существующий сервис**. Готового нового безопасного кэша с
доказанной экономией100с не найдено. Большие оставшиеся области установлены;
размер экономии выясняется только после дешёвого доказательства допустимых hits.
