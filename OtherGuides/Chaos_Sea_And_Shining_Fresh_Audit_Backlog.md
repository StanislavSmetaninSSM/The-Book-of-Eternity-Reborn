# Chaos Sea And Shining Fresh Audit Backlog

## Purpose

Этот документ фиксирует **свежие подтверждённые проблемы**, найденные после большой historical fix-wave по Морю Хаоса и Сияющей Обители.

Он отделён от [Chaos_Sea_And_Shining_Audit_Backlog.md](/E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Chaos_Sea_And_Shining_Audit_Backlog.md) и нужен как новый рабочий backlog для:

- новых gameplay-дефектов Моря Хаоса
- новых gameplay-дефектов Сияющей Обители
- новых completeness/data-surfacing проблем в Chaos Sea командах
- новых completeness/data-surfacing проблем в Shining Abode командах

---

## Status Legend

- `Open` — проблема подтверждена и ещё не исправлена
- `In Progress` — сейчас исправляется
- `Fixed` — исправлена в коде и документ обновлён
- `Deferred` — сознательно отложена

---

## Current Baseline Status

- Historical backlog `1–42` остаётся отдельным закрытым журналом прошлых волн исправлений
- Этот документ сейчас фиксирует **FRESH AUDIT WAVE COMPLETE**
- Пакет `109–121` остаётся закрытым implementation pass’ом и подтверждён focused/full tests
- Latest implementation pass закрыл пакет `122–133`
- Latest implementation pass закрыл reopened пакет `134–142` и новый packet `143–149`
- Текущих подтверждённых открытых пунктов: **0**
- Следующий приоритетный пункт: `—`
- Следующие новые независимые пункты в этой волне нумеруются с `150`

Дополнение по latest implementation verification:

- reopened пакет `134–142` и новый packet `143–149` теперь закрыты текущим implementation pass
- свежая verification baseline после fix-pass: focused slice `648/648`, полный suite `1590/1590`
- numbering следующего нового independent item остаётся `150`

---

## Recommended Next Step

- historical backlog не переоткрывать
- fresh wave сейчас закрыта; следующий новый independent audit item начинать с `150`
- перед новой wave сначала делать read-only audit / verification pass, а не reopen historical packets без подтверждённого кода
- текущая подтверждённая baseline после latest fix-pass: focused slice `648/648`, полный suite `1590/1590`

---

### 1. Stale completed lore-research projects can block current-life rewards

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Guardian Projects`, `Lore Research`
- Problem:
  - logic для lore-research rewards/clue budgets проходит completed projects по возрастанию `completionTurn`
  - если раньше в истории уже есть completed `lore_research` project с тем же `projectId`, но из другой инкарнации, код преждевременно возвращает `false` или `0`
  - из-за этого текущая живая инкарнация может не получить:
    - lore quest hook token
    - guaranteed archive quest token
    - visible rival clue budget / spend
- Evidence:
  - [GuardianProjectState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianProjectState.cs:443)
  - [GuardianProjectState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianProjectState.cs:848)
  - [GuardianProjectState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianProjectState.cs:895)
- Required fix:
  - stale completed projects из другой инкарнации нужно **пропускать**, а не завершать поиск ошибкой
  - reward/clue consumers должны искать первый подходящий project для **текущей** инкарнации, а не ломаться на старом entry
- Follow-up note:
  - `GuardianProjectState` теперь пропускает stale lore-research entries из старых инкарнаций в reward/clue consumers вместо раннего `false/0`
  - добавлены regression tests на ordinary lore hook, guaranteed archive quest hook и visible rival clue budget/spend

### 2. Accepted forge validation does not verify blessing entitlement consumption markers

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Forge`, `Validation`
- Problem:
  - accepted forge validation проецирует expected Shining state и Soul Relics
  - но не сравнивает post-turn blessing effect state, где должны быть корректно записаны `consumedAtTurn` / `consumedAtUtc`
  - в результате accepted turn может пройти strict validation даже с неправильными lifecycle markers у forge entitlements
- Evidence:
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:6994)
  - [ShiningBlessingEffectState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningBlessingEffectState.cs:349)
  - [ShiningBlessingEffectState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningBlessingEffectState.cs:1752)
  - [ValidationService.AfterlifeArchiveTradeAndLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.AfterlifeArchiveTradeAndLifecycle.cs:1484)
- Required fix:
  - accepted forge validation должна сравнивать projected blessing entitlement state с текущим `soul_state`
  - mismatch по `status`, `freeShape`, `freeRetune`, `rerolls`, `consumedAtTurn`, `consumedAtUtc` должен fail-иться как canonical resolution error
- Fix note:
  - accepted forge validation теперь сравнивает projected `relicRefinementEntitlements` с текущим `soul_state` и surface-ит `shining_forge_action_blessing_entitlement_mismatch`
  - forge entitlement consumption теперь может использовать `resolvedAtUtc` receipt’а вместо `DateTime.UtcNow`, поэтому `consumedAtTurn/consumedAtUtc` стали детерминированными для accepted forge projection и runtime receipt-processing
  - добавлены regression tests на canonical accepted forge reshape с blessing entitlements и на mismatch consumption markers

### 3. Runtime hygiene silently truncates multiple pending Shining core actions

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Runtime Hygiene`, `Pending Control Files`
- Problem:
  - `EnsureHealthyAsync()` для `pending_shining_abode_actions.json` при `requests.Count > 1` молча переписывает файл и оставляет только первый request
  - эта hygiene запускается автоматически в runtime normalization pass
  - в итоге живая pending action может исчезнуть без receipt, without surfaced corruption error и без нормального audit trail
- Evidence:
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:250)
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:719)
- Required fix:
  - malformed multi-request state нельзя silently truncate-ить
  - runtime должен либо:
    - сохранять файл как corrupted и surface-ить проблему игроку/валидатору
    - либо fail-ить hygiene path без потери данных
- Fix note:
  - `ShiningCoreActionRequestState.EnsureHealthyAsync()` больше не переписывает multi-request `pending_shining_abode_actions.json` и не теряет pending actions через runtime normalization
  - `BuildSystemReminderFragmentAsync()` теперь surface-ит отдельный corruption reminder для `shining_core_action_multiple_pending_requests` и не описывает первый request как будто он authoritative contract
  - добавлены regression tests на preservation malformed multi-request file и corruption reminder вместо `first request wins`

### 4. Duplicate Shining founding validation can be bypassed with reused requestId

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Faction Politics`, `Validation`
- Problem:
  - duplicate hall/faction guard исключает pending founding entries с тем же `requestId`
  - отдельной валидации уникальности `requestId` в founding request object нет
  - malformed pending state с reused `requestId` может обойти duplicate check по `ProposedFactionId` / `ProposedHallId`
- Evidence:
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:272)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:880)
- Required fix:
  - enforce unique `requestId` внутри pending founding set
  - duplicate hall/faction guard не должен полностью доверять `requestId` как safe exclusion key
- Fix note:
  - `ShiningFactionRequestState.ValidateFoundingRequestAgainstCurrentStateAsync()` больше не считает reused `requestId` безопасным исключением для другого logical founding request
  - `WriteFoundingRequestAsync()` теперь тоже конфликтует по `requestId`, поэтому ordinary rewrite того же request не создаёт duplicate-id set
  - `ValidationService.ShiningAbode` теперь surface-ит `shining_founding_duplicate_request_id`, если pending founding set содержит duplicated requestId

### 5. Guardian gacha normalization can overflow charges used per return

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Canonical Normalizer`, `Guardian Gacha`
- Problem:
  - `processGacha` нормализует gacha state, но затем без clamp пишет `chargesUsedThisReturn = normalizedUsedCharges + 1`
  - malformed/replayed command может увести счётчик выше `chargesPerReturn`
  - это создаёт divergence между реальным лимитом гачи и canonical recorded usage
- Evidence:
  - [CanonicalStateNormalizer.SharedAndSoulHelpers.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs:310)
- Required fix:
  - при `processGacha` used charges нужно clamp-ить к `chargesPerReturn`
  - при явно недопустимом overflow state normalizer должен либо reject-ить command, либо фиксировать canonical capped result без переполнения
- Fix note:
  - `CanonicalStateNormalizer.SharedAndSoulHelpers` теперь clamp-ит `chargesUsedThisReturn` при `processGacha` к canonical `chargesPerReturn`
  - malformed/replayed gacha input больше не может записать canonical usage выше per-return limit

### 6. Full Shining political inspection still truncates history

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Politics UI`, `Player-Facing Data Completeness`
- Problem:
  - панель с заголовком `Полный осмотр решений фракций` всё ещё применяет `.Take(5)` к:
    - founding receipts
    - realignment receipts
    - leadership receipts
  - older political decisions silently disappear from the supposedly full audit surface
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:830)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:838)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:850)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:855)
- Required fix:
  - explicit full inspection panel не должен truncates history
  - либо убрать `.Take(5)`, либо добавить отдельный true full drill-down без потери старых решений
- Fix note:
  - `Полный осмотр решений фракций` больше не применяет `.Take(5)` к founding, realignment и leadership receipts
  - dedicated political inspection теперь показывает полный audit trail по всем трём веткам решений

### 7. Forge receipt inspection still leaks raw targetFormTag

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Forge UI`, `Receipts`
- Problem:
  - forge authoring уже humanize-ит формы реликвий
  - но receipt summary и full receipt inspection по-прежнему печатают raw canonical `targetFormTag`
  - игрок видит internal token там, где должен видеть player-facing название формы
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:490)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:806)
- Required fix:
  - receipt surfaces должны использовать тот же humanized formatter формы, что и forge authoring flow
  - canonical token можно оставлять только во вторичной audit-строке, если он реально нужен
- Fix note:
  - forge receipt summary и full receipt inspection теперь humanize-ят `targetFormTag` через тот же formatter, что и preview/authoring flow
  - raw canonical `targetFormTag` больше не светится как primary player-facing форма

### 8. Guardian project detail hides lifecycle and assist data and truncates journal

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Guardian Projects UI`, `Player-Facing Data Completeness`
- Problem:
  - detail panel проекта не показывает важные canonical fields:
    - `startedTurn`
    - `estimatedCompletionTurn`
    - `playerCanAssist`
    - `assistDescription`
  - та же panel режет journal entries и nested details через `Take(8)` / `Take(4)`
  - игрок не может честно увидеть полный lifecycle/assist contract проекта
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1592)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1656)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1661)
- Required fix:
  - project detail должен surfacing-ить lifecycle/assist fields без скрытия
  - project journal либо должен быть полным, либо needs explicit full drill-down path
- Fix note:
  - guardian project detail теперь показывает `startedTurn`, `estimatedCompletionTurn`, `playerCanAssist` и `assistDescription`
  - dedicated project journal/detail panel больше не режет entries и nested details через `Take(8)` / `Take(4)`

### 9. /сила_обители still acts like a preview instead of full audit

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Abode Power UI`, `Player-Facing Data Completeness`
- Problem:
  - `/сила_обители` показывает только:
    - последние 6 history entries
    - последние 8 journal entries
  - older power changes and causes are not inspectable from the only dedicated power screen
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:500)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:551)
- Required fix:
  - `/сила_обители` должен иметь true full history inspection
  - либо убрать truncation, либо добавить explicit drill-down для всей chronologies
- Fix note:
  - `/сила_обители` больше не режет `abodePower.history` и journal entries до preview-size
  - dedicated power screen теперь показывает полную chronology изменений и причин

### 10. Shining gacha history is only partially inspectable

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade And Forge UI`, `Player-Facing Data Completeness`
- Problem:
  - в Shining UI найден только один surface для `gachaHistory`
  - он показывает только последние 5 pulls
  - older relic summon outcomes are not audit-visible to the player
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:227)
- Required fix:
  - нужен полный inspection path для `gachaHistory`
  - compact overview можно оставить кратким, но должен существовать отдельный non-truncated audit view
- Fix note:
  - overview в trade/forge panel остался кратким, но `Полный осмотр торговых циклов` теперь содержит отдельную полную историю сияющих призывов
  - older `gachaHistory` entries больше не теряются из dedicated audit view

### 11. Residual internal English and protocol wording still leaks into player-facing surfaces

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Shining Abode`, `Player-Facing Wording`
- Problem:
  - в live UI ветках ещё встречаются mixed/internal labels и protocol-shaped confirmations
  - примеры:
    - founder/foundation wording
    - `Pending Shining core action created: ...`
    - politics prompts вроде `Favored archetype`, `Patron effect family`, `candidate head`
    - raw protocol/prose wording в afterlife/founder surfaces
  - это уже не ломает механику, но делает данные менее понятными игроку и снова светит internal vocabulary
- Evidence:
  - [ExplorerMode.Afterlife.PlayerGuardianFoundation.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.PlayerGuardianFoundation.cs:28)
  - [ExplorerMode.Afterlife.ShiningAbode.Politics.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Politics.cs:40)
  - [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs:338)
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:176)
- Required fix:
  - добить terminology pass в этих ветках
  - player-facing confirmations и prompts должны использовать единый русский словарь без raw protocol ids и mixed English labels
- Fix note:
  - player-facing foundation / Shining politics / Shining action confirmations переведены на единый русский словарь без `Pending ... created`, `Favored archetype`, `Patron effect family`, `candidate head` и similar mixed wording
  - related founded-guardian/foundation surfaces тоже переведены на русские player-facing labels вроде `Бонус основания` и `Дар основания`

### 12. Runtime hygiene can silently remove the post-life afterlife return guard

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Afterlife Return Guard`, `Runtime Hygiene`
- Problem:
  - `AfterlifeReturnGuardService.EnsureHealthyAsync()` удаляет malformed или semantically invalid `afterlife_return_guard.json`, пока игрок всё ещё находится в afterlife realm
  - re-entry в `Shining Abode` и `guardian_forced` incarnation blockers fail-close-ят только пока guard-файл существует
  - destructive auto-heal может снять обязательный post-life guard вместо того, чтобы surface-ить corruption/runtime health problem
- Evidence:
  - [AfterlifeReturnGuardService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeReturnGuardService.cs:87)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1071)
  - [GameEngine.TurnLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:1564)
- Required fix:
  - invalid `afterlife_return_guard.json` нельзя silently delete-ить в active afterlife context
  - guard нужно сохранять как malformed protected artifact и fail-close-ить reentry/incarnation paths до repair/cleanup с явным authority-safe contract
- Fix note:
  - `AfterlifeReturnGuardService.EnsureHealthyAsync()` больше не удаляет malformed `afterlife_return_guard.json` в afterlife realm
  - `BuildSystemReminderFragmentAsync()` теперь surface-ит blocking corruption reminder вместо silent cleanup

### 13. Critical health service accepts truncated guardians.json as canonical

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Session Health`, `Guardians State`
- Problem:
  - `CriticalStateHealthService` считает `guardians.json` пригодным для critical session health, если в документе есть любой один из фрагментов вроде `activeGuardian`, `pendingGuardianCreation` или `chaosSeaNavigation`
  - truncated guardian state без authoritative `guardians[]` может пройти health check
  - после этого Chaos Sea gameplay продолжает работать без canonical guardian roster, на который завязаны gacha/projects/quests
- Evidence:
  - [CriticalStateHealthService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/CriticalStateHealthService.cs:256)
- Required fix:
  - critical health contract для `guardians.json` должен требовать authoritative `guardians[]` и минимально достаточный canonical shape
  - partial/truncated guardian state должен fail-close-иться как critical health problem
- Fix note:
  - `CriticalStateHealthService` теперь требует authoritative `guardians[]` для canonical session health
  - fragment-only guardian roots теперь surface-ятся как `guardians_missing_canonical_surface`

### 14. ProgressionScheduleService.IsChaosSea overmatches malformed realm strings

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Progression`, `Realm Semantics`
- Problem:
  - `ProgressionScheduleService.IsChaosSea()` использует слишком широкий fallback по подстроке `Chaos`
  - malformed или future realm label с таким фрагментом может ошибочно считаться afterlife/Chaos Sea state
  - это может подавить world/faction progression и перевести ход в неправильную Chaos Sea scheduling branch
- Evidence:
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:777)
- Required fix:
  - realm detection должна опираться только на canonical aliases/labels
  - substring fallback по `Chaos` нужно убрать или резко сузить до authority-safe explicit mapping
- Fix note:
  - `ProgressionScheduleService.IsChaosSea()` больше не использует substring fallback по `Chaos`
  - scheduling теперь переключается только по canonical afterlife aliases

### 15. Afterlife progression apply path can burn Chaos Sea ordinal without processed cycles

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Progression`, `Cycle Accounting`
- Problem:
  - afterlife apply path всегда двигает `CurrentChaosSeaTurnOrdinal`
  - при этом `LastChaosSeaSimulationOrdinal` и `LastGuardianProjectCycleOrdinal` двигаются только если `progression_report.json` подтверждает processed cycles
  - если path достигается без валидного progression report, later afterlife scheduling permanently desync-ится
- Evidence:
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:252)
- Required fix:
  - progression apply path не должен сжигать Chaos Sea ordinal без доказанного processed-cycle outcome
  - либо ordinals должны двигаться lockstep, либо path должен fail-close-иться без valid `progression_report.json`
- Fix note:
  - afterlife apply path теперь двигает Chaos Sea ordinals только при валидном `progression_report.json` с exact processed-cycle markers
  - без подтверждённого outcome schedule сохраняет прежние ordinals и не сжигает ход прогрессии

### 16. Shining forge projection and validation ignore Ink Feather cost

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Forge`, `Validation`
- Problem:
  - `TryApplyForgeAction()` списывает только `lightSparks` и не дебетует `inkFeathers`
  - accepted-turn validator использует ту же projected forge outcome и тоже не проверяет обязательный feather debit
  - в результате accepted turn без оплаты перьев может пройти strict validation, а корректный turn с real feather cost может быть помечен как mismatch
- Evidence:
  - [ShiningAbodeState.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.TradeAndForge.cs:287)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:6969)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:7012)
  - [ShiningCoreActionResolutionValidationTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningCoreActionResolutionValidationTests.cs:212)
- Required fix:
  - forge runtime/projection path должен списывать exact `quotedCostFeathers`
  - accepted-turn validation должна строго сравнивать feather debit вместе с forge result
  - regression tests нужно перевести на canonical path, который действительно проверяет списание перьев
- Fix note:
  - `TryQuoteForgeAction()` теперь требует достаточное число `inkFeathers`, а `TryApplyForgeAction()` списывает feather cost вместе с `lightSparks`
  - paid forge validation теперь падает при отсутствии feather debit

### 17. Normalization erases legitimate leadership outcomes on the active guardian faction

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Politics`, `Normalization`
- Problem:
  - normalizer форсированно возвращает active guardian faction в `secure` leadership state с active guardian как главой
  - leadership request validation при этом допускает transitions на уже существующей active guardian faction
  - легитимный succession/revolt/abdication outcome на этой фракции может быть silently erased следующим normalization pass
- Evidence:
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:97)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:942)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:412)
- Required fix:
  - нужно устранить конфликт между canonical leadership outcomes и forced normalization of the active guardian faction
  - runtime не должен silently переписывать подтверждённый leadership result обратно в default secure/head state
- Fix note:
  - materialization active guardian faction больше не переписывает существующий `leadership` block на `secure + guardian head`
  - легитимные leadership outcomes на уже существующей guardian faction теперь переживают normalizer pass
  - review-follow-up pass теперь дополнительно rebind-ит malformed guardian-head binding на actual active guardian id, не ломая legitimate non-guardian leadership outcomes

### 18. Invalid Shining projectArchetype silently normalizes to accord

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Projects`, `Validation`
- Problem:
  - draft validation normalizes `projectArchetype` до проверки supported values
  - missing/invalid archetype silently превращается в `accord`
  - player-authored malformed request может пройти как другой canonical project archetype вместо явного reject
- Evidence:
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:614)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:637)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:1012)
- Required fix:
  - raw `projectArchetype` нужно валидировать до fallback normalization
  - invalid/missing unsupported archetype должен reject-иться как malformed request, а не silently mutate-иться в `accord`
- Fix note:
  - draft project validation теперь читает raw `projectArchetype` и reject-ит unsupported values до любой normalization fallback логики

### 19. Life history screen omits canonical recordLifeCompletion payload

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Life History UI`, `Player-Facing Data Completeness`
- Problem:
  - `История жизней` читает только ad hoc flat fields вроде `characterName`, `finalLevel`, `deathReason`
  - canonical life record хранится как `recordLifeCompletion` с вложенными итогами (`characterFinalState`, `majorAchievements`, `relationshipsFormed`, `moralChoices`, `skillsLearned`, `enlightenmentGained`)
  - на spec-compliant entries detail screen может выглядеть почти пустым и терять самую важную сводку жизни
- Evidence:
  - [ExplorerMode.PrivateImplementation.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs:1110)
  - [CanonicalStateNormalizer.SharedAndSoulHelpers.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs:135)
  - [ValidationService.AfterlifeArchiveTradeAndLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.AfterlifeArchiveTradeAndLifecycle.cs:1872)
- Required fix:
  - `История жизней` должна честно раскрывать canonical `recordLifeCompletion`
  - nested life summary fields должны быть player-visible без скрытия или ad hoc flattening-only path
- Fix note:
  - `История жизней` теперь читает `recordLifeCompletion` и показывает `characterFinalState`, `majorAchievements`, `relationshipsFormed`, `moralChoices`, `skillsLearned` и `enlightenmentGained`

### 20. Resident detail screen silently truncates canonical Chaos Sea resident data

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Guardian Abode Residents UI`, `Player-Facing Data Completeness`
- Problem:
  - main resident detail panel режет personality traits, thought journal, interaction log и revealed-history entries через `Take(...)`
  - underlying resident state при этом хранит более полные canonical данные: transfer receipts, tags, timestamps, consequence/attitude/intent, history metadata
  - отдельного true full resident inspection surface сейчас не найдено
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2417)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2449)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2461)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2473)
  - [GuardianAbodeResidentState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentState.cs:808)
- Required fix:
  - dedicated resident detail must быть full inspection, а не preview
  - если текущий экран должен оставаться кратким, нужен отдельный explicit drill-down без truncation и потери canonical metadata
- Fix note:
  - resident detail больше не режет personality traits, thought journal, interaction log, history log и transfer receipts
  - full resident inspection теперь показывает canonical metadata по всем четырём журналам/историям

### 21. “Показать весь журнал Хранителя” is not actually a full journal

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Guardian Journal UI`, `Player-Facing Data Completeness`
- Problem:
  - screen с названием `Показать весь журнал Хранителя` сводит каждую canonical entry к короткой строке `turn/eventType/title/summary`
  - canonical guardian journal entries также содержат `consequence`, `attitude`, `intent`, `timestamp`, `requestId`, `interactionType`, `status`, `responseMode`, `tags`
  - отдельного player-facing full metadata journal surface не найдено
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1954)
  - [ExplorerMode.PrivateImplementation.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs:702)
  - [ActorJournalState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ActorJournalState.cs:116)
- Required fix:
  - “весь журнал” должен либо показывать canonical metadata полностью, либо получить отдельный real full-detail drill-down
- Fix note:
  - `Показать весь журнал Хранителя` теперь выводит timestamp, requestId, interactionType/status/responseMode, consequence/attitude/intent и tags поверх summary строки

### 22. Companion-echo relic inspection surfaces only a partial companionSeed snapshot

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Soul Relics UI`, `Companion Echoes`
- Problem:
  - companion-echo relic detail screen показывает только часть `companionSeed`
  - `coreValues` режутся до 3 значений, richer fields вроде `culturalLayer` и `personalityTraits` не раскрываются, а `sourceResidentId` / `sourceGuardianId` светятся raw ids
  - отдельного player-facing surface с полным companion snapshot сейчас не найдено
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1173)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1213)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1188)
  - [ValidationService.QuestsRivalsFactionsAndWorld.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs:2587)
- Required fix:
  - relic detail screen должен показывать полный companion snapshot и resolve-ить source ids в player-facing labels там, где это возможно
- Fix note:
  - companion-echo relic inspection теперь показывает полный `coreValues`/`culturalLayer`/`personalityTraits` snapshot и humanize-ит source resident/guardian labels, когда roots доступны

### 23. “Полный осмотр торговых циклов” still shows only the current Shining trade cycle

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Trade UI`, `Player-Facing Data Completeness`
- Problem:
  - `ShowShiningTradeLifecycleInspectionAsync()` вычисляет один `currentContract` на фракцию и показывает только `matchingReceipt ?? sameCycleReceipt`
  - despite the title `Полный осмотр торговых циклов`, historical `tradeInventoryReceipts[]` outside current cycle не раскрываются
  - другого dedicated Shining trade history surface для полной chronology не найдено
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:79)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:147)
  - [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs:213)
- Required fix:
  - dedicated trade inspection должен enumerates full `tradeInventoryReceipts[]` history per faction, а не только current-cycle proof
- Fix note:
  - `Полный осмотр торговых циклов` теперь перечисляет всю историю `tradeInventoryReceipts[]` по каждой фракции, а не только receipt текущего цикла

### 24. player_soul leadership head falls through to raw internal token

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Politics UI`, `Player-Facing Labels`
- Problem:
  - `BuildHeadActorLabel()` не имеет player-facing branch для поддерживаемого `player_soul` head type
  - screens, использующие этот formatter, могут показать `player_soul:player_soul` вместо понятной player-facing подписи
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:1071)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:16)
- Required fix:
  - `player_soul` должен иметь явный player-facing label во всех leadership/politics surfaces
- Fix note:
  - `BuildHeadActorLabel()` и related player-facing notification formatting теперь переводят `player_soul` в `душа игрока`

### 25. Faction political inspection omits canonical faction.projects[] data

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Politics UI`, `Player-Facing Data Completeness`
- Problem:
  - `Политическое состояние фракции` показывает только summary фракции и resident rows
  - canonical `faction.projects[]` не surface-ятся вообще, хотя в них лежат `displayName`, `summary`, `toneTags`, `targetFactionIds`, `projectArchetype`, `outputEffectFamily`, `tier`, `status`, `isSupported`, `strengthReward`
  - отдельного политического inspection surface с full project context не найдено
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:660)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:366)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:419)
- Required fix:
  - faction political inspection должен включать canonical faction project data или иметь отдельный explicit project drill-down from that screen
- Fix note:
  - `Политическое состояние фракции` теперь показывает полный `projects[]` block с archetype/family/tier/status/support/reward/summary/tags/targets

### 26. Full Gates inspection still dumps raw effectPayload JSON

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Gates UI`, `Player-Facing Data Completeness`
- Problem:
  - detail inspection blessing cards во Вратах льёт `effectPayload` как raw JSON under `Payload:`
  - payload содержит internal keys вроде `type`, `latestTurn`, `routeOptions`, `freeShape`, `freeRetune`
  - supposedly full inspection surface тем самым показывает implementation payload вместо player-readable effect description
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:391)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:1286)
- Required fix:
  - gates inspection должен переводить blessing payload в player-facing описание effect semantics
  - raw JSON допустим только как вторичный audit block, если он действительно нужен дополнительно
- Fix note:
  - full Gates inspection теперь показывает player-facing section `Эффект:` и оставляет raw JSON только как secondary technical payload block

### 27. Historical prepared-incarnation receipts do not preserve a stable card snapshot

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Gates/Incarnation History`, `Player-Facing Data Completeness`
- Problem:
  - historical `prepare_incarnation_package` receipts хранят только `selectedCardIds`
  - receipt inspection пытается resolve-ить card labels через current gates/package state
  - после изменения current package старые historical receipts деградируют до raw ids и теряют stable selected-card details
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:785)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:1032)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:719)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:591)
- Required fix:
  - historical package receipts должны сохранять stable selected-card snapshot или эквивалентный player-facing projection, который не зависит от текущего mutable state
- Fix note:
  - prepare-package receipts теперь могут auto-hydrate stable `selectedCards` snapshot из matching prepared package during normalization
  - receipt inspection приоритетно использует snapshot из самого receipt вместо resolve через mutable current gates/package state
  - review-follow-up pass теперь требует stable `selectedCards` snapshot в accepted `prepare_incarnation_package` receipt validation contract; поздняя hydration оставлена только как legacy fallback

### 28. Newly added inspection surfaces still mixed Russian and English in player-facing labels

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Shining Abode`, `Player-Facing Wording`
- Problem:
  - после review-fix pass в user-facing inspection surfaces остались mixed labels вроде `Request id`, `Project id`, `Entry id`, `Card id`, `candidate cursor`, `payload`, `lore clue`, `Common-ресурсы`, `route options`
  - часть canonical metadata и effect descriptions в командах читалась как русско-английский гибрид
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs)
- Required fix:
  - player-facing labels в inspection panels должны использовать единый русский словарь
  - raw protocol/meta terms допустимы только как значения внутри technical/audit JSON blocks, а не как основные подписи
- Fix note:
  - inspection labels переведены на русский: `Идентификатор ...`, `идентификатор запроса`, `Технический JSON эффекта`, `Количество сюжетных подсказок`, `Обычных/Необычных ресурсов`, `вариантов пути`
  - raw interaction/response/transfer modes в player-facing metadata теперь humanize-ятся в русские подписи
- Fix note:
  - wording follow-up pass убрал residual mixed labels из player-facing выводов:
    - `Legacy summary` -> `Прогресс по старой записи`
    - `restlessness` -> `неспокойствие`
    - `strongest visible pull` -> `самый сильный видимый зов`
    - `Выбранные card id` -> `Выбранные идентификаторы карт`
    - `Полный frozen payload` -> `Полный зафиксированный набор карт`
    - `Soulbound / legendary tier` и `tier`-ярлыки в overview заменены на русский player-facing словарь
  - добавлены regression checks в `ExplorerModeCommandTests.Afterlife` и `ExplorerModeSourceGuardTests`

### 29. Malformed progression_report.json can bypass Chaos Sea cycle validation

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Progression`, `Validation`
- Problem:
  - malformed `progression_report.json` сводится к `null`
  - затем `suppressMissingReportIssue` глушит обязательную проверку cycle outcome
  - в результате accepted Chaos Sea turn может пройти без валидации `chaosSeaCyclesProcessed` / `guardianProjectCyclesProcessed`, а pending progression потом просто очищается
- Evidence:
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:210)
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:500)
- Required fix:
  - malformed `progression_report.json` должен fail-closed, а не вести себя как harmless missing report
  - pending progression нельзя очищать, пока cycle outcome не подтверждён валидным report contract
- Fix note:
  - `ProgressionScheduleService` теперь различает `missing` и `malformed` progression report
  - malformed `progression_report.json` больше не suppress-ит обязательную Chaos Sea validation и больше не очищает pending cycles/report silently

### 30. Malformed progression_schedule.json silently resets the Chaos Sea ledger

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Progression`, `State Files`
- Problem:
  - malformed `progression_schedule.json` трактуется как отсутствующий
  - runtime silently re-bootstrap-ит его в нулевое состояние
  - ordinals и pending cycles могут быть стёрты или пересчитаны без surfaced corruption / fail-closed path
- Evidence:
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:35)
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:649)
- Required fix:
  - malformed `progression_schedule.json` должен считаться corrupted ledger state
  - progression bootstrap допустим только для реально отсутствующего файла, а не для unreadable/malformed ledger
- Fix note:
  - `EnsureInitializedAsync()` теперь fail-close падает на malformed `progression_schedule.json` вместо silent re-bootstrap в нулевой ledger
  - bootstrap оставлен только для действительно отсутствующего schedule file

### 31. Non-vacant Shining leadership state can survive without canonical head binding

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Faction Politics`, `Validation`
- Problem:
  - `ValidateShiningFactionLeadershipObject` не требует `headActorType` / `headActorId` для non-vacant leadership state
  - malformed faction может пережить normalization с пустым `headActorId`
  - `HasCurrentPlayerHeadFaction` признаёт только exact `player_soul:player_soul`, поэтому player-headed faction может исчезнуть из founding / leadership gate checks
- Evidence:
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:385)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:667)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:743)
- Required fix:
  - non-vacant leadership contract должен требовать canonical head binding
  - malformed player-headed / guardian-headed factions нельзя считать валидными до repair или reject path
- Fix note:
  - `ValidateShiningFactionLeadershipObject()` теперь требует non-empty `headActorType/headActorId` для non-vacant leadership и отдельно валидирует canonical `player_soul` binding
  - normalizer теперь rebind-ит пустой `player_soul` headActorId к exact `player_soul`

### 32. Pending Shining realignment contracts can collide on requestId

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Faction Politics`, `Pending Requests`
- Problem:
  - `pending_shining_faction_realignments.json` keyed by `ResidentId`, а не `requestId`
  - accepted-turn resolution later матчится по `requestId` against global `factionRealignmentReceipts[]`
  - два realignment contracts могут разделить один `requestId`, из-за чего validator может привязать не тот receipt или дать false missing-resolution
- Evidence:
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:532)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:1137)
- Required fix:
  - pending realignment set должен enforce-ить уникальный `requestId` как authoritative identity
  - resolution path не должен полагаться на ambiguous request binding
- Fix note:
  - `WriteRealignmentRequestAsync()` теперь конфликтует и по `requestId`, и по `residentId`
  - validator теперь surface-ит `shining_realignment_duplicate_request_id` для duplicated request ids в pending realignment set

### 33. Full Shining political inspection reconstructs founding history from mutable current state

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Politics UI`, `History Fidelity`
- Problem:
  - `Полный осмотр решений фракций` подмешивает в founding receipts живые поля из текущих `hall` / `faction`
  - `description`, `serviceTags`, `charter.summary`, `favoredArchetype`, `patronEffectFamily` не frozen внутри самого receipt snapshot
  - дальнейшие правки faction/hall state ретроактивно меняют историю, которую видит игрок
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:918)
- Required fix:
  - historical founding inspection должен опираться на stable receipt snapshot
  - live hall/faction fields можно использовать только как fallback, а не как primary historical source
- Fix note:
  - founding receipts теперь auto-hydrate stable snapshot полей `hallDescription`, `hallServiceTags`, `factionName`, `charterSummary`, `favoredArchetype`, `patronEffectFamily`
  - `Полный осмотр решений фракций` теперь приоритетно читает receipt snapshot и использует live hall/faction только как legacy fallback
- Reopen note:
  - при пустых snapshot fields full politics inspection всё ещё fallback-ится к текущим `hall` / `faction` labels и descriptions
  - history founding receipts по-прежнему может дрейфовать задним числом после later rename/edit вместо fail-close historical rendering
- Fix note:
  - founding receipt normalization больше не дописывает missing hall/faction context из live state
  - `Полный осмотр решений фракций` теперь читает только persisted snapshot fields или stable ids, поэтому later hall/faction edits больше не меняют прошлую историю

### 34. Archived Codex archive detail still stores and shows only summary text

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Archive UI`, `Data Completeness`
- Problem:
  - прежний pass починил candidate preview, но archived Codex entry по-прежнему сохраняет только `summary`
  - archive write path не переносит full `content` в soul archive entry
  - detail panel `/архив_души` рендерит только `entry.Summary`, поэтому после архивации полный текст снова становится неinspectable
- Evidence:
  - [AfterlifeArchiveCandidateService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeArchiveCandidateService.cs:268)
  - [AfterlifeArchiveCandidateService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeArchiveCandidateService.cs:454)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:210)
- Required fix:
  - archived Codex entry должен хранить полный текст alongside summary preview
  - archive detail surface должен приоритетно показывать full archived content, а не только краткую выжимку
- Previous fix note:
  - archive candidates теперь сохраняют full `content` alongside truncated `summary`
  - candidate detail surface приоритетно показывает полный codex text вместо 220-char preview
- Reopen note:
  - archived entry path остался недочиненным: в soul archive persisted по-прежнему только `summary`, поэтому fix закрыл candidate surface, но не archived detail
- Fix note:
  - archive write path теперь сохраняет full `content` alongside `summary`
  - `/архив_души` читает stored archive content first-class через archive entry summary model и больше не деградирует к preview-only rendering

### 35. Guardian detail panel hides completed projects beyond the first five

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Guardian UI`, `Data Completeness`
- Problem:
  - panel Хранителя печатает общее число completed projects
  - но рендерит только `Take(5)`
  - шестой и далее completed project не виден на этом экране
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1144)
- Required fix:
  - completed project history должна быть полной либо иметь explicit full-history drill-down
- Fix note:
  - guardian detail panel больше не режет completed projects через `Take(5)` и показывает весь completed-project block

### 36. Guardian temporary modifier inspection hides entries after the first four

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Guardian UI`, `Data Completeness`
- Problem:
  - active temporary modifiers полностью собираются сервисом
  - player-facing panel показывает только первые `4`
  - нет ни `+N more`, ни отдельного full modifier inspection, поэтому остаток модификаторов неinspectable
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:651)
  - [GuardianProjectState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianProjectState.cs:716)
- Required fix:
  - full modifier set должен быть player-visible
  - если overview остаётся compact, нужен explicit full inspection path или хотя бы truthful overflow indicator
- Fix note:
  - temporary modifier inspection больше не режет entries через `Take(4)` и показывает весь active modifier set в project detail panel

### 37. Shining trade receipt summary derives sold-out count from live inventory

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade UI`, `History Fidelity`
- Problem:
  - `BuildShiningTradeReceiptSummary` считает `soldOutCount` из текущего `faction.tradeInventory`
  - прошлый торговый итог на обзорном экране меняется после любой последующей правки витрины
  - игрок видит не исторический snapshot receipt, а текущую переинтерпретацию
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:452)
- Required fix:
  - sold-out / slot summary должен вычисляться из frozen trade receipt data, а не из live inventory state
- Fix note:
  - trade receipts теперь auto-hydrate stable `soldOutCount` snapshot для текущего цикла
  - `BuildShiningTradeReceiptSummary()` теперь читает sold-out outcome из receipt snapshot, а не из live inventory

### 38. Shining core receipt history loses stable faction and project identity after renames

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Politics UI`, `History Fidelity`
- Problem:
  - core receipts для `complete`, `support`, `unsupport`, `retire` показывают только текущие labels через `ResolveShiningFactionLabel` / `ResolveShiningProjectLabel`
  - stable `factionId` и `projectId` в player-facing history не удерживаются
  - после переименования история меняется задним числом и теряет исходную идентичность
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:428)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:813)
- Required fix:
  - historical core receipts должны нести stable project/faction identity snapshot или player-visible canonical ids alongside labels
- Fix note:
  - core receipts для project actions теперь auto-hydrate `factionName`/`projectName` snapshot
  - summary/detail surfaces теперь показывают stable ids alongside labels, поэтому history не теряет identity после переименований
- Reopen note:
  - afterlife notification sync и часть core summary surfaces всё ещё заново строят summary из live `shiningRoot`
  - core-action history/notifications могут дрейфовать при later rename/state mutation, даже если receipt уже persisted
- Fix note:
  - core receipt normalization больше не hydrat-ит faction/project labels из live `shiningRoot`
  - core history и notifications теперь опираются на persisted snapshot fields или stable ids, поэтому rename/state mutation не меняют старые исходы

### 39. Soul Relic archive detail leaks raw slot keys

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Soul Relics UI`, `Wording`
- Problem:
  - relic slots показываются как raw internal keys вроде `mainHand`, `offHand`, `Default`
  - эти ключи уже локализуются elsewhere, но archive detail screen этого не делает
  - player-facing readability страдает, и internal naming leakage остаётся видимым
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:979)
  - [ExplorerMode.Inventory.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs:13)
- Required fix:
  - slot labels в archive/relic detail должны использовать тот же localized formatter, что и inventory surfaces
- Fix note:
  - Soul Relic list/detail теперь humanize-ят `mainHand` / `offHand` / `Default` через тот же slot formatter, что и inventory UI

### 40. Soul Relic effect inspection leaks internal English property names

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Soul Relics UI`, `Wording`
- Problem:
  - effect detail screen печатает `actionCheckBonuses` и unknown effect-property names verbatim
  - player-facing Russian output смешивается с internal English identifiers
  - технические keys становятся основным содержанием inspection surface
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1014)
- Required fix:
  - known effect properties должны humanize-иться в русский player-facing словарь
  - unknown technical keys допустимы только как secondary debug/audit block, а не как primary label
- Fix note:
  - `actionCheckBonuses` теперь выводятся через русский player-facing словарь
  - unknown effect-property keys перенесены в secondary technical block вместо primary detail lines

### 41. Archive linkage history falls back to opaque guardian and project ids

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Archive UI`, `History Readability`
- Problem:
  - archive entries сохраняют только opaque linkage ids для source guardian / target project
  - UI при отсутствии display names падает обратно на эти ids
  - историческая связь остаётся нечитаемой для игрока
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:193)
  - [AfterlifeArchiveState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeArchiveState.cs:403)
- Required fix:
  - archive linkage receipts должны сохранять human-readable labels или стабильный readable snapshot alongside ids
  - UI fallback на raw ids должен быть последним, а не обычным историческим путём
- Fix note:
  - archive entry validation теперь допускает optional `sourceGuardianName`
  - archive detail UI теперь сначала использует stored readable labels, затем live guardian/project resolution, и только последним fallback’ом raw ids

### 42. Unreadable currentRealm can wipe the Chaos Sea progression ledger

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Progression`, `Realm Resolution`
- Problem:
  - unreadable or missing `soul_state.currentRealm` мог переписать schedule в unresolved realm
  - следующий `BuildControlForNextTurnAsync()` обнулял pending counters и validation молча выходила без issues
  - accepted-turn apply path ещё и мог затереть `schedule.CurrentRealm` пустым значением после конца хода
- Evidence:
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:101)
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:230)
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:697)
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:782)
- Required fix:
  - unreadable `currentRealm` должен fail-close блокировать scheduling вместо silent ledger wipe
  - existing Chaos Sea ledger нельзя затирать пустым realm или нулевыми pending counters
  - apply path не должен переписывать persisted realm unresolved значением
- Fix note:
  - `ProgressionScheduleService` теперь fail-close падает на unreadable `currentRealm` в turn-build path вместо обнуления pending ledger
  - `ValidateAcceptedTurnOutcomeAsync()` теперь surface-ит `progression_control_unresolved_current_realm` для unresolved progression control
  - apply path больше не затирает `schedule.CurrentRealm` пустым значением и сохраняет pending ledger fail-closed, если soul_state unreadable после хода

### 43. Deferred Chaos Sea backlog above one cycle is overwritten on the next turn build

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Progression`, `Pending Ledger`
- Problem:
  - если afterlife pending backlog был сохранён как `PendingChaosSeaCycles > 1` или `PendingGuardianProjectCycles > 1`
  - следующий Chaos Sea scheduling branch всё равно жёстко пишет оба счётчика в `1`
  - deferred backlog сверх одного цикла silently теряется ещё до validation/apply
- Evidence:
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:138)
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:160)
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:299)
- Required fix:
  - Chaos Sea turn-build должен сохранять authoritative accumulated backlog, а не normalise-ить его обратно к `1`
  - expected processed-cycle counts должны отражать реально сохранённый pending ledger
- Fix note:
  - `ProgressionScheduleService.BuildControlForNextTurnAsync()` теперь сохраняет accumulated `PendingChaosSeaCycles` / `PendingGuardianProjectCycles` и поднимает их до `1` только если backlog был пуст
  - `ChaosSeaCyclesExpectedThisTurn` и `GuardianProjectCyclesExpectedThisTurn` теперь отражают authoritative pending ledger вместо forced `1/1`

### 44. Shining trade notifications still reconstruct sold-out history from live inventory

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade UI`, `History Fidelity`
- Problem:
  - предыдущий pass добавил `soldOutCount`, но notification detail всё ещё fallback-ится на live inventory, если snapshot `0` или отсутствует
  - historical “trade ready” detail снова может дрейфовать после последующих покупок из витрины
  - item считается недозакрытым, пока detail screen не станет snapshot-first без live reconstruction в ordinary path
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:578)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:579)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:76)
- Required fix:
  - trade notifications должны оставаться snapshot-first even when `soldOutCount` is zero
  - fallback на live inventory допустим только для явного legacy receipt path, а не для ordinary current contract
- Previous fix note:
  - Shining trade receipts теперь поддерживают stable `soldOutCount`, validator читает его как canonical optional snapshot field, а GM runtime prompt требует его для новых receipts
  - notification summary/detail теперь приоритетно используют receipt `factionName`/`soldOutCount` и не reconstruct-ят sold-out outcome из live inventory
- Reopen note:
  - detail branch всё ещё имеет residual live-state fallback для `soldOutCount <= 0`, поэтому historical notification может менять прошлый итог задним числом
- Fix note:
  - trade notification detail больше не reconstruct-ит sold-out outcome из live inventory
  - `soldOutCount` теперь обязателен для closure proof matching ready receipt и ordinary notification path остаётся snapshot-first

### 45. Shining realignment and leadership history still depends on mutable current state

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Politics UI`, `History Fidelity`
- Problem:
  - full politics inspection и notification details для realignment/leadership всё ещё резолвят часть данных из live resident/faction/head state
  - receipt contract не требует stable snapshot для source/target faction labels, head labels и related history context
  - после переименований или политических изменений история может меняться задним числом
- Evidence:
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:868)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:909)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:965)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:994)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:636)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:663)
- Required fix:
  - realignment/leadership receipts должны нести стабильный исторический snapshot
  - full inspection и notifications должны читать snapshot first-class, а live state использовать только как legacy fallback
- Fix note:
  - realignment receipts теперь поддерживают `sourceFactionName` / `targetFactionName`, а leadership receipts — `factionName`, `previousHeadLabel`, `newHeadLabel`
  - full politics inspection и afterlife notification details/summary теперь читают snapshot first-class и больше не зависят от live faction/head labels как от primary historical source
- Reopen note:
  - при missing snapshot fields realignment/leadership rendering всё ещё fallback-ится к current faction/head labels
  - historical political outcomes остаются нестабильными для legacy or partially-populated receipts и меняются задним числом
- Fix note:
  - realignment/leadership normalization больше не дописывает missing labels из current faction/head state
  - politics inspection и notification detail/summary теперь используют snapshot fields first-class, а при их отсутствии падают только к stable ids, не к mutable live labels

### 46. /shining_politics overview hides excess entries without any remainder indicator

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Politics UI`, `Data Completeness`
- Problem:
  - overview `/shining_politics` режет фракции, pending requests и latest receipts до `5`
  - экран не пишет `…и ещё N`, поэтому лишние записи просто исчезают
  - команда выдаёт неполный обзор без truthful overflow signal
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:144)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:161)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:187)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:212)
- Required fix:
  - либо добавить truthful overflow indicator для каждого capped section
  - либо превратить overview в реально полный список без silent truncation
- Fix note:
  - compact overview `/shining_politics` остался кратким, но теперь честно пишет `…и ещё N` для скрытых factions, pending requests и receipt sections

### 47. Shining Gates and project-draft prompts still mix Russian and English player-facing wording

- Status: `Fixed`
- Severity: `Low`
- Scope: `Shining Abode`, `Commands`, `Wording`
- Problem:
  - предыдущий wording pass убрал основной хвост, но в Shining Gates / project / founding surfaces всё ещё остались mixed Russian-English и internal labels
  - подтверждённые остатки: `ruinous failure`, `Tier проекта`, raw `Subversion`/`subversion`
  - player-facing surfaces всё ещё светят internal vocabulary там, где ожидался полностью русифицированный текст
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:472)
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:569)
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:583)
  - [ExplorerMode.Afterlife.ShiningAbode.Politics.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Politics.cs:48)
- Required fix:
  - player-facing prompts и inspection labels должны быть полностью русифицированы
  - technical payload wording допустим только как вторичный audit block и только если без него нельзя диагностировать состояние
- Previous fix note:
  - Gates/project-draft prompts переведены на единый русский словарь: `Тоновые метки`, `Архетип проекта`, `Семейство эффекта`, `Целевая фракция для подрыва`
  - full gates inspection больше не использует mixed label `Технический JSON эффекта`
- Reopen note:
  - residual tails остались в secondary prompts и effect descriptions, поэтому wording pass нельзя считать полностью закрытым
- Fix note:
  - Gates / founding prompts теперь используют русские labels для архетипов и effect families
  - player-facing surfaces больше не содержат `ruinous failure`, `Tier проекта` и raw `Subversion` wording

### 48. Chaos Sea and afterlife prompts still leak mixed Russian-English protocol wording

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Commands`, `Wording`
- Problem:
  - в command texts и helper prompts остались mixed strings вроде `materialize-иться`, `soul-quest path`, `roleplay the request`, `curated память`
  - это уже не GM-only protocol prose, а прямой пользовательский текст команд
  - из-за этого player-facing surfaces снова смешивают русский и английский
- Evidence:
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:109)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:1150)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:1234)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2578)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2897)
- Required fix:
  - all player-facing Chaos Sea / afterlife prompts должны быть приведены к одному русскому словарю
  - protocol-style English tokens должны оставаться только в non-player technical prompts, если они вообще ещё нужны
- Fix note:
  - player-facing afterlife / Shining prompts и inspection labels дочищены до русского словаря
  - архивные уведомления, торговые статусы, manifestation / resident prompts и related headers больше не смешивают русский и английский в ordinary UI

### 49. Chaos Sea runtime hygiene still deletes malformed player-authored pending contracts

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Runtime Hygiene`, `Pending Control Files`
- Problem:
  - `pending_abode_offering.json`, `pending_guardian_trade_request.json` и `pending_player_guardian_foundation.json` всё ещё auto-delete-ятся при parse/minimal-field failure
  - session normalization запускает эти cleaners на load path, поэтому player-authored contract может исчезнуть до receipt и до нормального corruption surface
  - для offering/trade/foundation уже существуют dedicated validator issues, но hygiene path стирает сам предмет проверки
- Evidence:
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:713)
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:715)
  - [GuardianAbodeOfferingState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeOfferingState.cs:127)
  - [GuardianTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianTradeRequestState.cs:257)
  - [PlayerGuardianFoundationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/PlayerGuardianFoundationState.cs:318)
- Required fix:
  - malformed/incomplete Chaos Sea pending contracts нельзя silently delete-ить в active/authoritative runtime path
  - hygiene должна либо сохранять corrupted file как есть, либо surface-ить blocking corruption без потери player-authored request
- Fix note:
  - malformed active `pending_guardian_trade_request.json` и `pending_player_guardian_foundation.json` больше не collapse-ятся в ordinary write paths
  - public authoring/read flows теперь fail-close блокируют overwrite corruption вместо записи нового request поверх повреждённого файла

### 50. Guardian abode resident request bundles still silently truncate malformed entries

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Guardian Abode Residents`, `Pending Control Files`
- Problem:
  - readers для resident roster / interaction / transfer requests проглатывают malformed items и возвращают только surviving entries
  - subsequent hygiene переписывает pending files уже усечённым набором или очищает их, если surviving list стал пустым
  - validation использует те же readers и может просто не увидеть исходную corruption state
- Evidence:
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:394)
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:625)
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:1097)
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:1131)
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:1168)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:3349)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:3660)
- Required fix:
  - malformed resident request bundles должны fail-close сохранять исходный corruption signal
  - hygiene/validation не должны терять malformed entries через partial read + rewrite flow
- Fix note:
  - roster / interaction / transfer / companion-manifestation bundles теперь работают по strict all-or-nothing parse contract
  - malformed bundle больше не переписывается surviving subset’ом и блокирует новые client-authored writes до явного repair/cleanup

### 51. Accepted complete_project validation replays a nondeterministic projectId

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Validation`, `Project Resolution`
- Problem:
  - validator replay-ит `TryCompleteProject(...)`, а helper always builds fresh GUID-based `projectId`
  - после этого validation deep-compare-ит whole faction/gates state
  - canonical accepted turn может ложно fail-иться, потому что projected project identity не совпадает с реально materialized project
- Evidence:
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:6772)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:7549)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:223)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:760)
- Required fix:
  - accepted `complete_project` projection must be deterministic against receipt/request identity
  - validator cannot depend on newly generated GUID when proving canonical post-state
- Fix note:
  - `TryCompleteProject(...)` теперь принимает stable `projectId` / `completedAtUtc` override для validation projection
  - accepted `complete_project` validation больше не зависит от runtime-generated GUID/timestamp drift

### 52. Accepted invest_in_faction validation does not verify lightSparks debit

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Validation`, `Core Actions`
- Problem:
  - runtime canonical mutation spends `lightSparks` for `invest_in_faction`
  - accepted-turn validator currently checks faction/gates and soul feathers only
  - turn can skip required Shining-side `lightSparks` debit and still validate as canonical
- Evidence:
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:160)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:221)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:6757)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:6926)
- Required fix:
  - accepted projected Shining action validation must compare `lightSparks` alongside faction/gates state
  - same contract should hold for every core action that spends Shining-side sparks, not only for forge feathers/soul state
- Fix note:
  - generic accepted projected Shining validation теперь сравнивает canonical `lightSparks` alongside projected state
  - missing spark debit больше не проходит silently для `invest_in_faction` и other spark-spending projected actions

### 53. Companion-echo relic detail drops object-form personality traits from companionSeed

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Soul Relics UI`, `Companion Echoes`
- Problem:
  - companion-echo relic detail reads `personalityTraits[]` only when each entry is a string
  - current companion seeds clone canonical resident personality profile, where traits are stored as objects
  - supposedly detailed relic snapshot silently loses trait names, values and value descriptions
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1354)
  - [GuardianAbodeResidentState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentState.cs:1675)
  - [ValidationService.QuestsRivalsFactionsAndWorld.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs:2696)
- Required fix:
  - companion-echo inspection must fully render canonical object-form `personalityTraits`
  - player-facing relic snapshot should not silently degrade because UI expects obsolete string-only shape
- Fix note:
  - companion-echo detail теперь поддерживает object-form `personalityTraits` with name/value/valueDescription rendering
  - string-array legacy shape сохранён как fallback, но canonical resident-derived seed больше не теряет trait metadata

### 54. Shining trade receipt closure does not validate soldOutCount as canonical outcome

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade`, `Validation`
- Problem:
  - runtime/history now depend on `soldOutCount` as frozen trade outcome
  - receipt matching still closes request on `requestId`, `factionId`, `tradeCycleId` and `itemCount` only
  - arbitrary or stale `soldOutCount` can pass validation and become canonical historical surface
- Evidence:
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:257)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:1339)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:741)
  - [GameEngine.TurnLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:2409)
- Required fix:
  - closure contract for Shining trade receipts must prove `soldOutCount` against canonical generated inventory outcome
  - validator should reject ready receipts whose sold-out snapshot does not match the actual receipt/inventory resolution
- Fix note:
  - `soldOutCount` теперь участвует в ready receipt matching и `HasReadyInventoryForCurrentContract`
  - wrong or stale sold-out snapshot больше не закрывает Shining trade request как canonical resolution

### 55. Full Shining gacha history still omits canonical request and cost fields

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade And Forge UI`, `Data Completeness`
- Problem:
  - `Полная история сияющих призывов` lists faction, rarity shift, relic and optional turn only
  - it omits canonical fields already present in validated history: `requestId`, `returnCycleId`, `costInFeathers`, `timestamp`
  - there is no deeper drill-down from this screen, so “full” history remains incomplete
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:196)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:205)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:564)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:574)
- Required fix:
  - dedicated full gacha-history screen must show the full validated contract
  - if overview stays compact, it needs a deeper per-entry drill-down rather than silent omission of cost/timing/request identity
- Fix note:
  - `Полная история сияющих призывов` теперь показывает `requestId`, `returnCycleId`, `costInFeathers` и `timestamp`
  - full gacha history больше не скрывает canonical request/cost/timing fields

### 56. Pending Shining core actions still have no player-facing inspection surface

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Core Actions UI`, `Data Completeness`
- Problem:
  - overview only surfaces the queue count for pending core actions
  - no Shining command exposes a drill-down for pending core request payloads
  - player cannot inspect queued target ids, quoted costs, selected cards, forge payload or gacha-cycle contract before resolution
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:15)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:57)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:354)
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:61)
- Required fix:
  - Shining UI needs an explicit inspection path for pending core actions
  - queued core contracts should be as inspectable as receipts, politics, trade cycles and gates
- Fix note:
  - обзор Сияющей Обители получил dedicated entrypoint `Осмотреть ожидающие действия Обители`
  - pending core action drill-down теперь показывает ids, quoted costs, selected cards, forge/gacha payload and project draft snapshot

### 57. Invalid Shining faction and project enum-backed fields still silently normalize to defaults

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Normalization`, `Validation`
- Problem:
  - unsupported `originType`, `favoredArchetype`, `patronEffectFamily`, `projectArchetype`, `outputEffectFamily` and `status` are rewritten to defaults during normalization
  - validator mostly checks these fields only as strings, not as supported enum-backed values
  - malformed faction/project state can survive as silently rewritten canonical-looking data instead of surfacing corruption
- Evidence:
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:634)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:716)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:367)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:486)
- Required fix:
  - enum-backed Shining fields should be validated against supported value sets
  - malformed unsupported values must surface as corruption instead of silently normalizing to defaults
- Fix note:
  - active Shining normalization теперь сохраняет unsupported faction/project enum-backed values instead of rewriting them to defaults
  - validator добавляет explicit errors for invalid `originType`, `favoredArchetype`, `patronEffectFamily`, `projectArchetype`, `outputEffectFamily` and `status`

### 58. Resident detail still leaks raw linked quest and relic ids

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Resident UI`, `Readability`
- Problem:
  - resident detail prints `linkedSoulQuestId` and `grantedRelicId` verbatim as the main labels
  - no resolve or drill-down from that panel tells the player what quest or relic those ids actually mean
  - supposedly rich resident inspection remains partially opaque for progression-linked objects
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2470)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2472)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2533)
- Required fix:
  - resident detail should resolve linked quest/relic into human-readable labels
  - if full resolution is impossible inline, the panel needs an explicit drill-down path instead of raw opaque ids
- Fix note:
  - resident detail теперь резолвит `linkedSoulQuestId` и `grantedRelicId` через current soul quest / soul relic state
  - raw ids остаются только как secondary suffix inside readable label format

### 59. Shining founding notification detail still leaks raw factionId

- Status: `Fixed`
- Severity: `Low`
- Scope: `Shining Abode`, `Notifications`, `Wording`
- Problem:
  - founding notification detail prints `factionId` / `proposedFactionId` as the player-facing faction label
  - the same normalized receipt data already carries readable faction snapshot fields
  - notification detail therefore regresses to internal identifier leakage while politics inspection is more humanized
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:623)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:624)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:918)
- Required fix:
  - founding notification detail should use the readable faction snapshot first-class
  - raw `factionId` may remain only as secondary audit metadata, not as the main player-facing label
- Fix note:
  - founding notification detail теперь использует readable faction snapshot (`factionName` / `proposedFactionName`) как primary label
  - raw `factionId` больше не торчит as main player-facing faction text

### 60. Guardian trade becomes ready before canonical receipt closure exists

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Guardian Trade`, `Runtime Contracts`
- Problem:
  - `EnsureTradeInventoryStateAsync()` marks guardian trade inventory as ready as soon as local `tradeInventory` matches the current contract
  - matching ready receipt is not required before the inventory becomes usable and before afterlife notifications synthesize `guardian_trade_inventory_ready`
  - partially materialized or malformed trade state can therefore behave like accepted state without canonical receipt closure
- Evidence:
  - [GuardianTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianTradeService.cs:378)
  - [AfterlifeNotificationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeNotificationState.cs:419)
- Required fix:
  - guardian trade should become usable only after canonical ready receipt proof exists
  - ready notification must not synthesize accepted availability from inventory shape alone
- Fix note:
  - guardian trade view теперь становится ready only after canonical receipt proof
  - shape-only inventory without receipt остаётся pending/unusable, а `guardian_trade_inventory_ready` больше не синтезируется без receipt

### 61. Reading a relic-forging boosted guardian shop silently consumes the one-cycle trade refresh bonus

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Guardian Projects`, `Trade Bonuses`
- Problem:
  - read path for a guardian shop consumes relic-forging `tradeRefreshUsesSpent` as soon as a matching boosted inventory is observed
  - the same boost is also part of the derived inventory signature, so the next read sees the already-materialized inventory as stale and forces a new request
  - the one-cycle refresh bonus is effectively burned just by opening the shop, without an actual refresh action
- Evidence:
  - [GuardianTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianTradeService.cs:392)
  - [GuardianProjectState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianProjectState.cs:290)
  - [GuardianProjectState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianProjectState.cs:1346)
- Required fix:
  - relic-forging trade refresh should be consumed only by a canonical refresh/materialization event, not by inventory inspection
  - already materialized boosted inventory must remain valid across ordinary reads within the same cycle
- Fix note:
  - ordinary guardian-trade read path больше не тратит `tradeRefreshUsesSpent`
  - already materialized boosted inventory остаётся valid across repeated reads instead of самопроизвольного stale reset

### 62. Pending Shining core actions can be silently cleared by any same-requestId receipt

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Pending Control Files`, `Runtime Hygiene`
- Problem:
  - `EnsureHealthyAsync()` clears pending core actions as soon as `FindReceipt()` finds any receipt with the same `requestId`
  - receipt lookup matches only by `requestId`, without checking `actionType`, `status` or payload compatibility
  - stale or mismatched receipt data can erase the authoritative pending contract without proving a correct resolution
- Evidence:
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:246)
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:256)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:312)
- Required fix:
  - pending core cleanup should require canonical receipt/request compatibility, not requestId-only coincidence
  - mismatched receipt collisions must surface as corruption or unresolved state instead of clearing the file
- Fix note:
  - Shining core hygiene теперь требует canonical receipt/request compatibility instead of requestId-only coincidence
  - stale or mismatched same-requestId receipt больше не очищает pending core action file

### 63. Malformed pending Shining trade inventory requests still disappear before validation

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Pending Control Files`, `Runtime Hygiene`
- Problem:
  - malformed `pending_shining_trade_inventory_requests.json` collapses to `Array.Empty` during read
  - `EnsureHealthyAsync()` then deletes the file because parsed request count is zero
  - the broken pending contract vanishes before validation or repair flow can surface it
- Evidence:
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:92)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:305)
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:720)
- Required fix:
  - malformed active pending Shining trade files must be preserved and surfaced as corruption, not auto-deleted
  - read path should distinguish `missing`, `valid empty`, and `malformed`
- Fix note:
  - trade pending read path теперь различает `missing`, `valid empty` и `malformed`
  - malformed pending file больше не auto-delete-ится в hygiene и больше не overwrite-ится new core/trade/political writes

### 64. Shining request authoring still fail-opens on malformed availability and corrupted owner-state

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Authoring`, `State Contracts`
- Problem:
  - request validators normalize live `shining_abode_state.json` before checking mode and eligibility
  - unsupported `availability` and other malformed owner-state fields are coerced into usable values instead of blocking authoring
  - at the same time, pending-file hygiene still clears requests based on raw non-`active` availability, so one malformed state can authorize new requests on one path and delete existing ones on another
- Evidence:
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:324)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:326)
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:610)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:225)
- Required fix:
  - malformed Shining owner-state must fail-close consistently across authoring, validation and hygiene paths
  - unsupported `availability` and corrupted bootstrap/lifecycle state must not be normalized into ordinary actionable mode
- Fix note:
  - authoring и hygiene теперь используют общий fail-close owner-state gate instead of normalization-to-active
  - unsupported `availability` и malformed owner-state roots блокируют новые Shining requests и не дают hygiene silently чистить pending files

### 65. Duplicate Shining request identities still make validation order-dependent

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Validation`, `Receipt Identity`
- Problem:
  - multiple Shining resolution paths bind receipts or history entries with `FirstOrDefault(requestId)`
  - duplicate receipt/history identities are not rejected strongly enough before those first-match lookups happen
  - the same accepted-turn state can pass or fail depending on array order rather than canonical identity
- Evidence:
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:312)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:132)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:847)
- Required fix:
  - duplicate request identities must be rejected explicitly before any first-match receipt/history lookup
  - accepted-turn proof must not depend on array ordering
- Fix note:
  - validation теперь explicit reject-ит duplicate `requestId` в relevant Shining receipts/history arrays
  - ambiguous duplicate requestId больше не может silently bind through first-match lookup, поэтому strict proof стал order-independent

### 66. Full Shining trade-cycle inspection still omits canonical soldOutCount

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade UI`, `Data Completeness`
- Problem:
  - `Полный осмотр торговых циклов` shows item count, status and cycle metadata
  - the same validated receipt contract also carries canonical `soldOutCount`, but the screen does not show it
  - the supposedly full trade inspection still hides part of the frozen outcome
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:158)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:182)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:815)
- Required fix:
  - full trade inspection must render `soldOutCount` first-class alongside slot/item counts
  - frozen receipt outcome should be fully visible anywhere the UI promises a full cycle audit
- Fix note:
  - `Полный осмотр торговых циклов` теперь показывает `soldOutCount` и в current-cycle receipt block, и в полной истории receipt’ов
  - legacy/missing `soldOutCount` теперь отображается честно как отсутствующее историческое поле, а не как fabricated `0`

### 67. Archive project fuel chooser still uses raw TargetProjectId as the primary target label

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Archive UI`, `Readability`
- Problem:
  - archive project fuel chooser already resolves a readable `TargetProjectName`
  - the actual choice text still foregrounds raw `TargetProjectId` as the project label shown to the player
  - this makes a real choice point unnecessarily opaque even though human-readable data is available
- Evidence:
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:1234)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:1339)
- Required fix:
  - chooser should show resolved project name as the primary target label
  - raw project id may remain only as secondary audit metadata
- Fix note:
  - archive project fuel chooser теперь показывает `TargetProjectName` как primary label, а `TargetProjectId` уходит в secondary explanatory line

### 68. Shining overview panels still hide excess entries without any truthful remainder signal

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Overview UI`, `Data Completeness`
- Problem:
  - several Shining overview screens still cap factions, latest trade outcomes, latest core outcomes and recent gacha pulls with `.Take(...)`
  - unlike `/shining_politics`, these screens do not add `…и ещё N`
  - overview commands therefore hide data non-truthfully instead of presenting themselves as compact previews
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs:196)
  - [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs:239)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:251)
- Required fix:
  - overview panels may stay compact, but each capped section must disclose hidden remainder count
  - if that is not possible, the section should stop truncating silently
- Fix note:
  - capped sections в Shining actions/trade/forge overview теперь пишут explicit `…и ещё N` для hidden factions, trade outcomes, core outcomes и recent gacha pulls

### 69. Chaos Sea and Shining detail surfaces still leak raw internal tokens as player-facing labels

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Shining Abode`, `Readability`
- Problem:
  - several ordinary inspection and chooser surfaces still print raw internal tokens as primary or near-primary text
  - confirmed examples include guardian `domain`, `founderLoyaltyTier`, Shining `slotId` and raw trade-cycle identifiers
  - these are not secondary audit suffixes; they still compete with or replace readable player-facing labels
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:922)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1013)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:142)
- Required fix:
  - human-readable labels should be primary everywhere the player makes choices or inspects state
  - raw internal tokens should remain only as secondary audit metadata when they are truly needed
- Fix note:
  - guardian detail теперь humanize-ит founded-guardian loyalty и убирает raw domain token из primary label
  - Shining trade screens используют readable slot/cycle labels as primary text, leaving raw ids only as secondary metadata

### 70. Leaving afterlife still silently clears pending archive-action contracts without receipt closure

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Afterlife Archive`, `Pending Control Files`
- Problem:
  - runtime hygiene outside afterlife still releases archive reservation and clears pending consultation/project-fuel request without matching receipt
  - incarnation flow does not block on these unresolved archive-action contracts before leaving afterlife
  - validation still treats them as contracts that must be explicitly closed through `archiveActionResolutions`, so runtime and validator disagree
- Evidence:
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:717)
  - [AfterlifeArchiveActionState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeArchiveActionState.cs:202)
  - [AfterlifeArchiveActionState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeArchiveActionState.cs:281)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:850)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:4694)
- Required fix:
  - unresolved archive-action requests must fail-close outside afterlife until they are explicitly closed by receipt or manually repaired
  - runtime cleanup cannot release reservation and clear the pending contract merely because the realm changed
- Fix note:
  - archive consultation / project-fuel pending contracts теперь не очищаются при простом выходе из afterlife
  - incarnation flow блокируется, пока archive-action request не будет закрыт canonical receipt’ом или repaired manually

### 71. Local Shining purchases invalidate same-cycle ready trade proof

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Trade`, `Runtime Contracts`
- Problem:
  - Shining ready-proof now requires `receipt.soldOutCount` to match the live inventory sold-out count
  - `BuyAsync()` flips `slot.soldOut` in live inventory but never updates the frozen receipt snapshot
  - after one local purchase the same inventory can stop counting as `ready`, breaking further buys or same-cycle refresh logic
- Evidence:
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:188)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:86)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:234)
- Required fix:
  - local buy flow must preserve canonical readiness within the current cycle, either by updating the receipt snapshot or by separating ready-proof from post-buy sold-out drift
  - same-cycle inventory must not become unresolved just because the player bought from a valid ready shop
- Fix note:
  - same-cycle ready proof теперь допускает локальный sold-out drift поверх frozen ready receipt
  - покупка из valid Shining shop больше не ломает readiness и auto-refresh logic текущего цикла

### 72. complete_project pending and receipt contracts still mismatch on project identity

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Core Actions`, `Validation`
- Problem:
  - `complete_project` pending requests do not carry a canonical `projectId` before resolution
  - runtime cleanup and accepted-turn receipt matching still require `receipt.projectId == request.ProjectId`
  - a valid resolved project receipt can therefore fail to match and leave the pending action uncleared
- Evidence:
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:75)
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:420)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:7588)
- Required fix:
  - `complete_project` needs a deterministic identity contract that matches before and after resolution
  - pending cleanup and accepted-turn proof cannot require a field that the pre-resolution request never carries canonically
- Fix note:
  - pending cleanup и strict receipt proof для `complete_project` больше не требуют pre-resolution `projectId`, если canonical request его ещё не несёт
  - resolved `projectId` теперь cleanly match-ится against stable request contract

### 73. Non-accepted Shining core-action validation still ignores lightSparks mutations

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Validation`, `Core Actions`
- Problem:
  - non-accepted `invest_in_faction` and `complete_project` outcomes are supposed to leave world state unchanged
  - the non-accepted validator checks faction/gates, feathers and radiance, but not Shining-side `lightSparks`
  - rejected or withdrawn actions can still burn sparks without any validation error
- Evidence:
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:7500)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:7523)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:160)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:223)
- Required fix:
  - non-accepted projected-action validation must prove that `lightSparks` remain unchanged for spark-spending core actions
  - rejected/withdrawn Shining actions cannot be allowed to mutate Shining-side resource state silently
- Fix note:
  - non-accepted validation теперь сравнивает `lightSparks` alongside faction/gates, feathers и radiance
  - refused/withdrawn spark-spending core actions больше не могут silently burn Shining-side sparks

### 74. Stub Shining core receipts can still clear pending actions without real closure markers

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Pending Control Files`, `Receipts`
- Problem:
  - pending core cleanup now checks field compatibility, but the receipt schema still allows empty or zero closure markers
  - a stub receipt with matching fields and supported status can still erase the authoritative pending contract
  - this leaves no strong proof that the request was materially resolved
- Evidence:
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:230)
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:261)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:939)
- Required fix:
  - supported-status receipts must also prove real closure markers such as canonical `resolvedAtTurn` / `resolvedAtUtc`
  - hygiene cannot clear pending core actions on field match alone
- Fix note:
  - core receipt schema и runtime matching теперь требуют positive `resolvedAtTurn` и non-empty `resolvedAtUtc`
  - stub receipts без closure markers больше не закрывают pending core actions

### 75. Pending Shining forge inspection still hides mutation payload fields

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Core Actions UI`, `Data Completeness`
- Problem:
  - pending core-action inspection now exists, but forge requests still omit `replacementProperty` and `addedProperties`
  - after the authoring preview closes, the player can no longer fully audit the queued forge mutation contract
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:961)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:974)
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:117)
- Required fix:
  - pending forge inspection must render full mutation payload, not only form tag and property index
  - queued forge contracts should stay fully inspectable after request creation
- Fix note:
  - pending forge inspection теперь показывает `replacementProperty` и `addedProperties`
  - queued forge mutation contract остаётся fully inspectable после authoring preview

### 76. /душа still hides companion-manifestation requests after the first three entries

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Soul UI`, `Data Completeness`
- Problem:
  - `/душа` shows the total count of current manifestation requests but renders only the first three entries
  - there is no manifestation-specific drill-down from this panel
  - additional live requests therefore become invisible from the only surface that acknowledges them
- Evidence:
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:95)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:104)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:132)
- Required fix:
  - `/душа` must either show all current manifestation requests or offer an explicit full inspection path for the hidden remainder
  - a panel cannot present a total count while silently hiding most of the active contracts
- Fix note:
  - `/душа` больше не режет live manifestation requests после первых трёх entries
  - all current companion-manifestation contracts остаются видимыми из основного soul screen

### 77. Blessing-card source rendering in Gates and package history still drifts from live state

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Gates UI`, `History Fidelity`
- Problem:
  - blessing-card source labels are still reconstructed from current faction/project/resident names where possible
  - when those live labels change, historical Gates/package views drift, and fallback still leaks raw source ids as primary text
  - the stored card contract is not being treated as a fully frozen player-facing source snapshot
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:404)
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:507)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:657)
- Required fix:
  - Gates and package history should prefer frozen readable source snapshot fields over live-state reconstruction
  - raw source ids should be secondary metadata only, not the main historical label
- Fix note:
  - blessing cards теперь держат readable source snapshots (`sourceFactionName`, `sourceActorName`)
  - Gates / package inspection используют snapshot-first source labels и больше не дрейфуют вместе с live names

### 78. /уведомления_загробья still lacks exact drill-down for non-Shining notifications

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Afterlife Inbox`, `Data Completeness`
- Problem:
  - `NotificationEntry` already carries stable ids like `requestId`, `archiveId` and `targetProjectId`
  - the detail screen only provides exact inspection blocks for Shining/foundation families and otherwise sends the player back to broad top-level screens
  - non-Shining notifications therefore remain summary-only even when exact referenced objects are known
- Evidence:
  - [AfterlifeNotificationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeNotificationState.cs:102)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:341)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:454)
- Required fix:
  - afterlife inbox needs exact drill-down or referenced-object inspection for non-Shining notification families
  - a stable notification id contract should not degrade into “open the broad category screen and search manually”
- Fix note:
  - inbox detail теперь показывает exact archive / project / guardian inspection blocks для non-Shining notifications, если stable ids уже известны
  - non-Shining notification detail больше не сводится к broad category redirect там, где referenced object можно показать точно

### 79. Main-menu startup can still delete pending Chaos Sea contracts on stale currentRealm

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Session Startup`, `Pending Control Files`
- Problem:
  - startup normalization in main-menu can run before persisted state refreshes `CurrentRealm`
  - realm-sensitive cleanup then executes against stale or empty realm and can silently clear valid pending Chaos Sea / afterlife contracts on session start
- Evidence:
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:342)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:351)
  - [GuardianTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianTradeRequestState.cs:285)
  - [PlayerGuardianFoundationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/PlayerGuardianFoundationState.cs:348)
- Required fix:
  - startup hygiene must not use stale runtime realm when deciding whether pending Chaos Sea / afterlife files are ordinary stale artifacts
  - persisted realm/state needs to be authoritative before any destructive cleanup path runs
- Fix note:
  - `NormalizeRuntimeUiArtifactsAsync()` и `EnsureClientOwnedSystemFilesHealthyAsync()` теперь сначала refresh-ят persisted game state, и только потом запускают realm-sensitive hygiene
  - startup/main-menu cleanup больше не принимает destructive решение по stale `CurrentRealm`

### 80. Manifestation housekeeping still rewrites requests from stale pre-prune state

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Companion Manifestation`, `Pending Bundles`
- Problem:
  - manifestation housekeeping prunes invalid or outdated requests, but `EnsureManifestationRequestForCurrentIncarnationAsync()` keeps using the old `existingRequests` list afterward
  - the later write can resurrect already-cleared requests and suppress new ones because duplicate detection is computed from stale entries
- Evidence:
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:609)
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:648)
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:684)
- Required fix:
  - post-prune manifestation authoring must operate on the canonical surviving request set, not on the stale list captured before housekeeping
  - cleared requests must not be resurrected by the same maintenance pass
- Fix note:
  - `EnsureManifestationRequestForCurrentIncarnationAsync()` теперь reread-ит surviving manifestation requests после housekeeping/prune и считает duplicate suppression уже от canonical surviving set
  - stale pre-prune entries больше не resurrect-ятся и не блокируют новый current-incarnation request

### 81. Shining local-buy path still risks lost update and cross-file desync

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Trade`, `State Consistency`
- Problem:
  - `BuyAsync()` performs independent writes to `soul_state.json` and `shining_abode_state.json`
  - it also writes back an older `shiningRoot` snapshot after revalidation, so failure or concurrent change between these writes can leave the files desynchronized or overwrite newer state
- Evidence:
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:271)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:295)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:333)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:341)
- Required fix:
  - local-buy flow must avoid writing stale Shining state back after a second read/validation step
  - the write sequence needs a deterministic patch/update contract that cannot leave `soul_state` and Shining owner-state out of sync
- Fix note:
  - `ShiningTradeService.BuyAsync()` больше не использует для commit старый pre-view `shiningRoot`: commit path reread-ит текущее canonical состояние перед записью
  - buy path canonicalize-ит `inkFeathers`, обновляет latest same-cycle ready receipt `soldOutCount` и не пишет назад stale Shining snapshot после revalidation

### 82. Shining pending-request validation still rejects Russian canonical realm label

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Validation`, `Pending Requests`
- Problem:
  - lifecycle validation for pending core/trade/political Shining requests accepts only `currentRealm = "Shining Abode"`
  - runtime validation for the same flows accepts both `Shining Abode` and `Сияющая Обитель`, so a Russian canonical state can fail validation despite being otherwise correct
- Evidence:
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:460)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:467)
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:355)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:518)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:766)
- Required fix:
  - lifecycle validation and runtime authoring/hygiene must share the same supported realm aliases
  - canonical Russian owner-state must not trigger false validation errors for valid Shining pending contracts
- Fix note:
  - `ValidationService.ShiningAbode` теперь использует shared `IsSupportedShiningRealm()` и принимает both `Shining Abode` и `Сияющая Обитель`
  - false validation errors на корректном русском canonical realm label больше не возникают

### 83. Failed manifestation build can still consume the current-incarnation request slot

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Companion Manifestation`, `Soul State`
- Problem:
  - a relic gets marked with `companionManifestationLastRequestedIncarnation` before `TryBuildManifestationRequest()` has succeeded
  - if build fails for that relic but another relic later causes the soul-state write, the failed relic is persisted as already requested and cannot retry this incarnation
- Evidence:
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:659)
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:676)
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:1453)
- Required fix:
  - manifestation retry markers must be written only after request construction succeeds for that relic
  - failed request synthesis must not burn the current-incarnation request opportunity
- Fix note:
  - manifestation retry marker теперь пишется только после успешного `TryBuildManifestationRequest()`; failed build больше не burn-ит slot текущей инкарнации
  - добавлен source guard на порядок `TryBuildManifestationRequest()` before `companionManifestationLastRequestedIncarnation`

### 84. Numeric inkFeathers shape still breaks Shining validation and helper paths

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Soul State`, `Validation`
- Problem:
  - local-buy path supports legacy numeric `inkFeathers` and can save it back as a number
  - other Shining helpers and lifecycle validators later expect `inkFeathers.current` as an object field, which can throw or misread the state on the same save
- Evidence:
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:427)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:446)
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:657)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:7645)
- Required fix:
  - all Shining-side feather readers/writers need a single canonical compatibility policy for legacy numeric `inkFeathers`
  - a write path must not persist a shape that later helper/validation code cannot safely consume
- Fix note:
  - Shining local-buy write path теперь canonicalize-ит legacy numeric `inkFeathers` в object shape before persist
  - Shining readers/validators (`ShiningCoreActionRequestState`, `ShiningAbodeState.*`, `ValidationService.LifecycleControlAndStateFiles`) теперь читают both legacy numeric и canonical object shape safely

### 85. Shining trade hygiene can still clear pending contracts on same-cycle mismatched receipts

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade`, `Pending Requests`
- Problem:
  - trade runtime currently treats inventory as ready when faction/cycle/item-count/sold-out-count match
  - this path can clear a pending request without exact `requestId`, even though the strict receipt matcher elsewhere in the same file requires `requestId`
- Evidence:
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:209)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:289)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:365)
- Required fix:
  - same-cycle readiness and pending cleanup must not accept a receipt/inventory contract that belongs to another request
  - runtime and strict matcher need one authoritative identity rule for request closure
- Fix note:
  - pending trade cleanup и same-cycle auto-refresh removal теперь требуют exact `FindMatchingReceipt()` вместо broad same-cycle similarity without `requestId`
  - mismatched same-cycle receipt больше не может silently clear другой pending trade contract

### 86. Full Shining trade-cycle inspection can still drift to an older same-cycle receipt

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade UI`, `History Fidelity`
- Problem:
  - `Полный осмотр торговых циклов` falls back to the latest `sameCycleReceipt` when strict current-contract match is missing
  - the panel then presents that fallback in the `Подтверждение исхода` block, so an older receipt from the same cycle can be shown as the current outcome
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:79)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:152)
- Required fix:
  - full trade-cycle inspection must distinguish strict current-contract proof from same-cycle historical fallback more explicitly
  - a fallback receipt from the same cycle must not be rendered as if it were the current authoritative outcome
- Fix note:
  - `Полный осмотр торговых циклов` теперь разводит `Подтверждение исхода` и `Последняя запись этого цикла`
  - same-cycle fallback больше не маскируется под current authoritative receipt proof

### 87. Shining inspection surfaces still expose raw identifiers and technical payloads as main content

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Inspection UI`, `Readable Labels`
- Problem:
  - several Shining inspection screens still surface raw ids, card identifiers, raw rarity arrows and full technical payload JSON in primary user-facing text
  - this includes core outcome summaries, Gates/package inspection, and rare-effect detail fallbacks that print raw key/value pairs
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:436)
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:331)
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:391)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1168)
- Required fix:
  - player-facing Shining inspection screens should use human-readable summaries first and relegate raw ids/payloads to secondary audit-only lines
  - rare or unknown effects still need a humanized explanation path instead of raw property dumps as the main output
- Fix note:
  - core outcome summaries теперь используют human-readable hall/faction/project labels как primary text без raw ids в основном предложении
  - Gates/package inspection теперь перечисляет shown/selected cards по display labels, а technical payload block понижен до diagnostic-only secondary section
  - rare/unknown relic effects теперь сначала объясняются player-facing строкой, а raw technical parameters остаются вторичными

### 88. Archive-entry detail still lacks direct drill-down to linked guardian and project

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Afterlife Archive UI`, `Navigation`
- Problem:
  - archive-entry detail already shows linked guardian/project information
  - but the action list still offers only consultation, fuel, or back, with no direct path into the referenced guardian or project
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:185)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:226)
- Required fix:
  - archive-entry detail needs exact drill-down actions for linked guardian and project when those stable references are already known
  - showing linked entities without a direct navigation path leaves the screen as summary-only despite having exact references
- Fix note:
  - archive-entry detail теперь даёт direct actions `Открыть связанного Хранителя` и `Открыть целевой проект`, если stable ids уже известны
  - archive screen больше не оставляет linked entities в summary-only состоянии без exact navigation path

### 89. Guardian detail still has no project drill-down from current and completed project blocks

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Guardian UI`, `Navigation`
- Problem:
  - guardian detail shows current and completed projects
  - but `ShowGuardianDetailActions` does not offer `Открыть проекты` or a direct project detail jump, so the user must back out and search manually
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1116)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2051)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:363)
- Required fix:
  - guardian detail should provide a direct project navigation path from the project blocks it already renders
  - current and completed project summaries should not strand the user on a non-navigable summary-only screen
- Fix note:
  - guardian detail теперь показывает action `Открыть проекты Хранителя` и ведёт в guardian-scoped chooser с exact project detail panels
  - current/completed project blocks больше не оставляют игрока без прямого перехода к project detail

### 90. Archive linked labels still drift and leak raw ids instead of stable display names

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Archive UI`, `History Fidelity`
- Problem:
  - archive-linked labels still fall back to `Name (Id)` or plain ids and use canonical/internal names instead of the same display-name logic used on guardian/project screens
  - this makes archive history drift from the rest of the UI and leaks internal ids as primary labels
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1331)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:197)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1711)
- Required fix:
  - archive guardian/project labels should use the same stable readable display-name rules as the dedicated guardian/project screens
  - raw ids must remain secondary metadata only, not the default primary label
- Fix note:
  - archive guardian/project labels теперь resolve-ятся через те же readable display-name rules, что и dedicated screens
  - raw ids перенесены во вторичную metadata line и больше не используются как primary label by default

### 91. Archive consultation and project-fuel UI still mixes Russian with internal English/debug prose

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Archive UI`, `Wording`
- Problem:
  - archive consultation / project-fuel prompts still mix Russian UX text with internal workflow/debug terms like `accepted result`, `rejected/cancelled`, `pending request`, `archive project fuel`, and raw pending filenames
  - these are ordinary player-facing strings, not GM-only protocol prompts
- Evidence:
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:1174)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:1215)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:1256)
- Required fix:
  - ordinary archive-operation prompts must be fully Russian and human-readable
  - internal workflow/debug tokens and pending-file names should not appear as primary player-facing text
- Fix note:
  - archive consultation / project-fuel prompts теперь используют полностью русский player-facing словарь без `accepted result`, `rejected/cancelled`, `archive project fuel` и pending filename prose
  - chooser/help text для archive operations также переведён на readable Russian wording

### 92. Shining local buy can leave split trade state between soul and owner files

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Trade`, `State Consistency`
- Problem:
  - `BuyAsync()` сначала списывает Чернильные Перья и добавляет реликвию в `soul_state.json`
  - затем слот витрины и sold-out snapshot отдельной записью фиксируются в `shining_abode_state.json`
  - сбой между этими write-операциями оставляет полу-применённую покупку: ресурс уже списан, а слот ещё не закрыт
- Evidence:
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:344)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:355)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:363)
- Required fix:
  - local buy должен commit-ить согласованный post-buy projection без split-write окна
  - write flow не должен оставлять `soul_state.json` и `shining_abode_state.json` в разных стадиях одной покупки
- Fix note:
  - `ShiningTradeService.BuyAsync()` теперь сначала строит согласованный post-buy projection, затем пишет `shining_abode_state.json` и только после этого `soul_state.json`
  - при сбое записи `soul_state.json` runtime делает best-effort rollback витрины к pre-buy snapshot и возвращает явную split-state ошибку вместо полу-применённой покупки
  - добавлен regression test с заблокированным `soul_state.json`, подтверждающий rollback витрины при write failure

### 93. Pending Shining political requests still have no full player-facing inspection path

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Politics UI`, `Completeness`
- Problem:
  - `/shining politics` показывает pending founding/realignment/leadership только короткими preview-строками
  - из UI нельзя открыть exact inspection pending political contract с `requestId`, timestamps, supporter list и candidate/head binding
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:168)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:252)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:99)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:132)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:177)
- Required fix:
  - pending political flows должны получить explicit inspect/drill-down path
  - preview summary не должен быть единственным способом просмотра живого political request contract
- Fix note:
  - `/shining_politics` получил отдельный full inspection path `📝 Осмотреть ожидающие политические запросы`
  - pending founding / realignment / leadership contracts теперь показывают `requestId`, timestamps, supporter lists, resident/faction targets и exact head bindings без truncation до preview-only строк

### 94. Full Shining discovery/outcome inspection still drifts from live state

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `History Fidelity`, `Inspection UI`
- Problem:
  - “полный” экран исходов Обители для discovery/faction outcomes снова резолвит hall/faction/resident/project labels из текущего состояния
  - дальнейшие rename/edit операции могут задним числом переписать исторический вывод
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:824)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:827)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:838)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:843)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:972)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:977)
- Required fix:
  - full discovery/outcome inspection должен читать stable receipt snapshot first-class
  - live-state reconstruction допустим только как legacy fallback и не должен маскироваться под frozen history
- Fix note:
  - full `Исходы Обители` panel теперь читает discovery/project labels, charter details и seeded names из receipt snapshot first-class
  - live resolve для hall/faction/resident/project labels убран из primary path; legacy receipts без snapshot names честно показываются через stored ids вместо mutable current labels
  - добавлен regression test на mutated current Shining state, который подтверждает стабильность historical discovery output

### 95. Exact archive/project notification detail still rebuilds from live state

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Afterlife Inbox`, `History Fidelity`
- Problem:
  - “точная” detail-врезка в afterlife inbox заново ищет archive/project в current live state
  - accepted archive receipt обязан убрать запись из `afterlifeArchive.stored`, поэтому later exact detail теряет archive block; project detail со временем тоже дрейфует
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:514)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:549)
  - [ValidationService.AfterlifeArchiveTradeAndLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.AfterlifeArchiveTradeAndLifecycle.cs:410)
- Required fix:
  - exact inbox detail должен использовать frozen notification-time snapshot как primary source
  - исчезновение live archive/project node не должно стирать связанный historical detail из уведомления
- Fix note:
  - `AfterlifeNotificationState` теперь хранит frozen archive/project snapshot fields (`archiveEntryType`, `archiveRarity`, `archiveSummary`, `targetProjectStateLabel`, `targetProjectProgressPercent`) и не перезаписывает их later live-state drift’ом
  - exact inbox detail в `/уведомления_загробья` сначала читает stored notification snapshot и только потом fallback-ится к live state
  - добавлены regression tests на exact archive/project detail как при живом current state, так и после полного исчезновения live archive/project nodes

### 96. Shining trade auto-refresh can drop unresolved prior-cycle pending contracts

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade`, `Pending Requests`
- Problem:
  - auto-refresh берёт pending requests и отфильтровывает их до текущего `tradeCycleId`
  - затем файл переписывается уже этим subset’ом, из-за чего unresolved prior-cycle contracts silently исчезают
- Evidence:
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:195)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:200)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:258)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1396)
- Required fix:
  - auto-refresh не должен rewrite-ить pending trade file через current-cycle-only subset
  - prior-cycle unresolved contracts должны либо сохраняться, либо surface-иться как mismatch/corruption, но не исчезать silently
- Fix note:
  - `SyncAutoRefreshRequestsForCurrentCycleAsync()` больше не режет pending set до current-cycle subset перед rewrite
  - unresolved prior-cycle requests сохраняются в pending file, пока не будут закрыты exact receipt proof’ом или отдельным repair pass’ом
  - regression test обновлён на preserved old-cycle request alongside newly created current-cycle contract

### 97. Resolved Shining political requests are not reconciled out of pending files

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Politics`, `Pending Requests`
- Problem:
  - runtime hygiene чистит pending political files только по realm/mode exit
  - matching receipts/history не используются для reconcile, поэтому accepted/refused flows могут продолжать висеть в reminders и resident-lock checks
- Evidence:
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:593)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:658)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:892)
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:722)
  - [GameEngine.TurnLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:2561)
- Required fix:
  - resolved political requests должны cleanly reconcile-иться из pending files по receipts/history
  - reminders и resident-locks не должны продолжать считать уже закрытые political contracts активными
- Fix note:
  - `ShiningFactionRequestState.EnsureHealthyAsync()` теперь runtime-reconcile’ит founding, realignment и leadership pending files по exact receipt/history closure proof
  - accepted/refused/withdrawn contracts удаляются из pending files в active Shining mode сразу после появления canonical closure markers
  - добавлен regression test на full cleanup resolved founding / realignment / leadership requests без выхода из Shining

### 98. Guardian detail still conflates available quests with active quests

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Guardian UI`, `Completeness`
- Problem:
  - guardian detail берёт либо `activeQuests`, либо `availableQuests`, но в обоих случаях подписывает блок как `Активные задания`
  - если `activeQuests` существует, `availableQuests` вовсе не показывается
- Evidence:
  - [GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1182)
  - [GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1185)
  - [GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1214)
- Required fix:
  - `activeQuests` и `availableQuests` должны рендериться как разные семантические блоки
  - UI не должен терять доступные задания или переименовывать их в активные
- Fix note:
  - guardian detail теперь отдельно показывает `📜 Активные задания` и `🧭 Доступные задания`
  - `availableQuests` больше не пропадают при наличии `activeQuests`, и оба списка остаются видимыми в одном detail screen
  - добавлен regression test на simultaneous rendering обоих quest blocks

### 99. Shining forge inspection still shows raw JSON payloads as main content

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Forge UI`, `Readable Output`
- Problem:
  - pending и resolved forge inspection по-прежнему печатают `replacementProperty` и `addedProperties` через pretty-printed JSON
  - основной пользовательский payload остаётся internal instead of human-readable
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:894)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:965)
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:510)
- Required fix:
  - forge inspection должен humanize-ить property mutation payload как primary text
  - raw JSON допустим только как secondary diagnostic block, а не как основной ответ игроку
- Fix note:
  - pending и resolved forge inspections теперь humanize-ят `replacementProperty` / `addedProperties` в player-facing строки про свойство, диапазон и описание
  - raw JSON ушёл во secondary technical snapshot и больше не является основным текстом forge payload
  - добавлены regression tests на humanized pending forge payload и humanized resolved forge receipt payload

### 100. Missing currentRealm still fail-opens into stale Chaos Sea runtime state

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Runtime State`, `Fail-Close`
- Problem:
  - если в `soul_state.json` пропадает `currentRealm`, runtime сохраняет предыдущее realm значение в памяти
  - aggregated state при этом всё ещё считает пустой realm состоянием Chaos Sea
  - из-за этого Chaos Sea-only prompts и turn reminders продолжают работать на corrupted realm data вместо explicit invalid-state surface
- Evidence:
  - [StateManager.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/StateManager.cs:132)
  - [AggregatedGameState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Models/GameState/AggregatedGameState.cs:47)
  - [GameEngine.TurnLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:2519)
- Required fix:
  - missing/empty `currentRealm` не должен inherit-ить previous in-memory realm
  - empty realm не должен классифицироваться как Chaos Sea по умолчанию
  - reminders и realm-gated flows должны fail-close до восстановления readable realm state
- Fix note:
  - `StateManager.RefreshGameStateAsync()` больше не наследует предыдущий in-memory realm, если `soul_state.json.currentRealm` отсутствует
  - `AggregatedGameState.CurrentRealm` теперь по умолчанию unresolved, а `IsInChaosSea` больше не считает пустой realm состоянием Моря Хаоса
  - добавлен regression test на повторный refresh после пропажи `currentRealm`

### 101. Foundation cleaner can still delete a valid Chaos Sea pending request on unreadable realm

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Pending Requests`, `Runtime Hygiene`
- Problem:
  - runtime normalization всегда запускает foundation cleaner
  - cleaner удаляет `pending_player_guardian_foundation.json`, когда current realm не распознан как Chaos Sea
  - transient realm read/parse failure therefore erases a valid pending foundation request instead of preserving it until realm becomes readable again
- Evidence:
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:716)
  - [PlayerGuardianFoundationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/PlayerGuardianFoundationState.cs:348)
- Required fix:
  - malformed/unreadable realm state не должен вести к destructive cleanup pending foundation contract
  - foundation hygiene должна preserve pending file until Chaos Sea realm can be authoritatively confirmed or rejected
- Fix note:
  - `PlayerGuardianFoundationState.EnsureHealthyAsync()` теперь preserve-ит pending foundation request на unresolved realm и чистит файл только при подтверждённом non-Chaos-Sea state
  - добавлен regression test на preservation pending request при unreadable realm

### 102. Shining sync loses normalization fixes when return-cycle does not change

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Runtime Sync`, `State Persistence`
- Problem:
  - Shining sync normalizes in-memory state every time main menu refreshes it
  - но `shining_abode_state.json` записывается назад только если `SyncShiningReturnCycle()` changed the return-cycle
  - any other normalization fix made on a no-cycle-change turn is silently lost on disk
- Evidence:
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1385)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1393)
- Required fix:
  - Shining sync must persist any real normalization change, not only return-cycle changes
  - on-disk Shining state must stay aligned with the normalized runtime projection
- Fix note:
  - main-menu Shining sync теперь сравнивает pre/post-normalization owner-state и записывает `shining_abode_state.json` при любом реальном canonical delta, а не только при смене return-cycle
  - добавлен source-guard regression на persistence normalization deltas without cycle bump

### 103. Prepared incarnation package validation ignores selectedCards vs selectedCardIds order consistency

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Prepared Package`, `Validation`
- Problem:
  - validation checks shape of `selectedCardIds` and `selectedCards`, but does not cross-check them against each other
  - normalization only rebuilds the id list when counts differ
  - order-scrambled package with matching counts can validate, while receipt hydration later fails because it depends on exact `selectedCardIds` sequence
- Evidence:
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:891)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:1099)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:734)
- Required fix:
  - prepared package validation must prove that `selectedCardIds` exactly matches `selectedCards[].cardId` in content and order
  - normalization should not leave sequence-drifted packages looking canonical
- Fix note:
  - `NormalizePreparedIncarnationPackage()` теперь rebuild-ит `selectedCardIds` whenever sequence diverges from `selectedCards[].cardId`
  - `ValidationService.ShiningAbode` теперь surface-ит `shining_abode_prepare_package_selected_card_sequence_mismatch` при ordered snapshot drift
  - добавлены regression tests на normalization reorder-fix и validation error for sequence mismatch

### 104. Shining leadership validation still allows orphan non-player heads

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Leadership`, `Validation`
- Problem:
  - Shining leadership validation checks enum shape and the `player_soul` special case
  - but it never verifies that guardian/resident/radiant_actor heads actually exist in supporting state files
  - orphan head bindings therefore pass validation and later leak into runtime/UI as if they were legitimate actors
- Evidence:
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:582)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:611)
- Required fix:
  - non-player head bindings must be validated against `guardians.json`, `residentRoot`, or `shiningPoliticalActors`
  - orphan head references should fail canonical Shining validation
- Fix note:
  - `ValidationService` теперь cross-check-ит non-player leadership bindings against `guardians.json`, resident state и `shiningPoliticalActors`
  - orphan head bindings теперь surface-ятся как `shining_leadership_missing_head_actor_reference`
  - добавлен regression test на missing guardian head binding

### 105. Afterlife detail screens still truncate timestamps to date-only precision

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Afterlife UI`, `Completeness`
- Problem:
  - multiple detail screens still truncate timestamps to the first 10 characters
  - the player only sees the date, losing time-of-day and same-day ordering in guardian detail/journal and Abode power detail
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:546)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1093)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1254)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1295)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1382)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1554)
- Required fix:
  - detail/journal screens must show the full timestamp or an equally precise humanized time
  - date-only truncation is not enough for audit/history ordering
- Fix note:
  - afterlife detail and journal surfaces in `ExplorerMode.Afterlife.GuardiansProjectsTrade` больше не режут timestamps до `[..10]`
  - resident/guardian chronology теперь показывает full UTC stamps; added command test and source guard against date-only slicing

### 106. Guardian journal detail still leaks raw eventType tokens

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Guardian UI`, `Readable Output`
- Problem:
  - full guardian journal/detail uses canonical `eventType` tokens directly in player-facing lines
  - this reads like a schema dump instead of an in-world or human-readable journal
- Evidence:
  - [ExplorerMode.PrivateImplementation.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs:705)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2021)
- Required fix:
  - guardian journal lines should humanize `eventType` into readable labels
  - raw canonical tokens must not appear as the primary player-facing wording
- Fix note:
  - `BuildActorJournalLine()` и resident journal detail больше не печатают raw `eventType` как primary player-facing text
  - guardian/resident journal views теперь humanize-ят common event types and fall back without schema-token leakage

### 107. Shining Gates inspection still exposes raw effectPayload JSON in visible UI

- Status: `Fixed`
- Severity: `Low`
- Scope: `Shining Abode`, `Gates UI`, `Readable Output`
- Problem:
  - Gates inspection still prints the raw `effectPayload` JSON block directly in the visible panel
  - internal keys and schema structure leak into the primary inspection surface even though the panel already contains gameplay descriptions
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:431)
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:510)
- Required fix:
  - raw `effectPayload` JSON should move into a clearly secondary diagnostic block or disappear from ordinary player-facing inspection
  - the main panel should remain human-readable
- Fix note:
  - ordinary Gates/package inspection больше не печатает raw `effectPayload` JSON block в visible player-facing panel
  - сохраняется только humanized effect description; regression tests updated accordingly

### 108. Shining overview still leaks raw currentReturnCycleId into primary status text

- Status: `Fixed`
- Severity: `Low`
- Scope: `Shining Abode`, `Overview UI`, `Readable Output`
- Problem:
  - the main Shining overview panel still prints `currentReturnCycleId` verbatim in the primary status line
  - this surfaces an internal cycle token where the player should see a friendlier description
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs:91)
- Required fix:
  - Shining overview should humanize the current return-cycle label
  - raw cycle ids should remain secondary-only diagnostics if they are needed at all
- Fix note:
  - main Shining overview теперь показывает humanized cycle-status label вместо raw `currentReturnCycleId` в primary status text
  - добавлен command regression test на overview wording without raw cycle id in the main state block

### 109. Stale progression_report.json can survive save/load and be reused as fresh Chaos Sea progression proof

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Save/Load`, `Progression Ledger`
- Problem:
  - `progression_report.json` не считается ephemeral control artifact при save/load
  - сам report не несёт `sessionId`, `requestId` или `turnNumber`, поэтому после `LoadGameAsync()` stale report может остаться на диске и позже совпасть по processed counters с новым ходом
  - это создаёт fail-open reuse чужого progression outcome как будто он свежий
- Evidence:
  - [SaveLoadService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/SaveLoadService.cs:17)
  - [SaveLoadService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/SaveLoadService.cs:175)
  - [TurnRequest.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Models/TurnRequest.cs:133)
  - [ProgressionScheduleService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:740)
- Required fix:
  - treat `progression_report.json` as ephemeral save/load artifact or bind it to current turn context
  - stale report from another load/session must not satisfy current Chaos Sea progression validation
- Fix note:
  - `progression_report.json` добавлен в `SaveLoadService.EphemeralControlFiles`
  - save archive больше не включает stale progression report, а load path теперь всегда очищает его как control-only artifact

### 110. Pending resident-roster request survives exit from afterlife and becomes stale wrong-realm contract

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Afterlife Pending Requests`, `Runtime Hygiene`
- Problem:
  - leaving afterlife clears interaction/transfer pending files but leaves `pending_guardian_abode_residents_request.json`
  - stale roster request can persist into Mortal World and later fail validation as a wrong-realm artifact
- Evidence:
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:571)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:3455)
- Required fix:
  - pending resident roster requests should be cleaned or preserved by the same explicit realm contract as the rest of afterlife pending files
  - ordinary wrong-realm stale roster state must not survive silently
- Fix note:
  - `GuardianAbodeResidentRequestState.EnsureHealthyAsync()` теперь чистит `pending_guardian_abode_residents_request.json` на explicit non-afterlife realm так же, как соседние afterlife pending files

### 111. Resident-roster hygiene clears pending contract without requiring matching rosterReceipts

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Resident Roster`, `Pending/Receipt Contract`
- Problem:
  - hygiene removes resident-roster pending requests as soon as residents exist in the abode
  - it does not require matching `rosterReceipts[]`, even though strict closure should be receipt-backed
  - runtime can silently erase an unresolved roster request
- Evidence:
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:1050)
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:1086)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:4121)
- Required fix:
  - roster-request cleanup must require exact receipt-backed closure
  - materialized residents alone are not sufficient proof that the pending roster contract was resolved
- Fix note:
  - resident-roster hygiene больше не смотрит на “жители уже существуют”
  - cleanup pending roster теперь требует exact `rosterReceipts[].requestId` match

### 112. Accepted Shining political receipts can validate without closure markers and remain permanently pending

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Politics`, `Validation vs Cleanup`
- Problem:
  - founding/realignment/leadership receipt shape currently accepts missing or empty closure markers beyond minimal integer/string presence
  - accepted-turn matching logic does not require real closure markers, but runtime cleanup later refuses to remove pending requests unless those markers exist
  - result: accepted political outcome can still leave `pending_shining_*` files alive forever
- Evidence:
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:1199)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:1230)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:1273)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:6593)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:6656)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:6687)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:1163)
- Required fix:
  - accepted political receipts must prove canonical closure with the same markers cleanup relies on
  - validation and runtime cleanup need one shared exact closure contract
- Fix note:
  - founding / realignment / leadership receipts теперь требуют canonical `resolvedAtTurn` + `resolvedAtUtc`
  - accepted-turn matching больше не засчитывает политическое closure без тех же markers, которые нужны runtime cleanup

### 113. Non-accepted forge turns can consume blessing entitlements without validation failure

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Forge`, `Non-Accepted Validation`
- Problem:
  - forge runtime mutation always consumes `relicRefinementEntitlements`
  - accepted-path validation compares entitlement lifecycle, but refused/withdrawn forge validation only compares sparks, feathers and soul relic mutations
  - non-accepted forge turn can therefore burn `freeShape`, `freeRetune` or reroll entitlements and still pass validator
- Evidence:
  - [ShiningAbodeState.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.TradeAndForge.cs:340)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:7075)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:7553)
- Required fix:
  - non-accepted forge validation must also prove that entitlement lifecycle did not change
  - entitlement consumption is not allowed to hide behind refused/withdrawn outcomes
- Fix note:
  - non-accepted forge validation теперь сравнивает `relicRefinementEntitlements` alongside Soul Relics / feathers / sparks
  - refused/withdrawn forge больше не может silently burn `freeShape`, `freeRetune` или reroll allowances

### 114. Accepted leadership resolution does not prove canonical post-transition leadershipState

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Leadership`, `Accepted Validation`
- Problem:
  - accepted leadership validation mostly proves the new head binding
  - it does not prove canonical post-transition `leadershipState`, so semantically wrong states like `contested` can slip through with the right head actor
- Evidence:
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:6720)
  - [Shining_Abode_Consolidation_Addendum.md](/E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Shining_Abode_Consolidation_Addendum.md:616)
- Required fix:
  - accepted succession/revolt outcomes must validate the full canonical leadership state, not only the new head identity
- Fix note:
  - accepted leadership validation теперь требует canonical post-state: successor => `secure`, vacancy => `vacant`
  - semantically wrong accepted states вроде `contested` больше не проходят с “правильным” head actor

### 115. Full resident history becomes unreachable once an abode resident departs

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Afterlife UI`, `Resident History`
- Problem:
  - `/guardians -> Обитатели Обители` collects only `presentOnly=true`
  - full history, revealed past and transfer chronology are only available from resident detail
  - once `isPresent=false`, the resident disappears from the inspection flow even though `historyLog` and `transferReceipts` remain in canonical state
- Evidence:
  - [GuardianAbodeResidentState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentState.cs:721)
  - [GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2356)
  - [GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2623)
- Required fix:
  - departed residents need an explicit inspection path or the resident list must support historical entries
  - canonical resident history must stay reachable after departure
- Fix note:
  - `/guardians -> Обитатели Обители` теперь включает departed residents в full inspection flow
  - detail panel явно помечает, что resident уже покинул Обитель, но сохраняет полный history/transfer drill-down

### 116. Afterlife inbox still lacks exact resident drill-down for resident-linked notifications

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Afterlife Inbox`, `Navigation`
- Problem:
  - resident notifications are synced from resident-aware state
  - but stored snapshot/detail enrichment carries only guardian/archive/project exact blocks, not resident exact blocks
  - inbox detail therefore degrades to a broad `/guardians` redirect instead of opening the exact resident context
- Evidence:
  - [AfterlifeNotificationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeNotificationState.cs:1077)
  - [AfterlifeNotificationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeNotificationState.cs:1799)
  - [SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:408)
  - [SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:507)
- Required fix:
  - resident-linked inbox notifications should carry exact resident snapshot and drill-down metadata
  - exact resident detail must be reachable from inbox without falling back to the broad guardian screen
- Fix note:
  - resident-linked notifications теперь хранят `residentId` / `residentName` snapshot
  - inbox detail получил exact resident block и quick action `👤 Открыть резидента`

### 117. Soul quest detail hides which afterlife resident actually generated the quest

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Soul Quests UI`, `Readable Links`
- Problem:
  - quest sync stores `relatedAfterlifeResidentId`
  - `/душа` detail only renders free-text `questGiver`
  - if the quest does not duplicate a readable giver name, the player cannot see which exact resident generated it
- Evidence:
  - [AfterlifeNotificationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeNotificationState.cs:800)
  - [ExplorerMode.QuestsAndRivals.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.QuestsAndRivals.cs:231)
- Required fix:
  - soul quest detail should resolve and display the exact afterlife resident source when `relatedAfterlifeResidentId` exists
  - free-text `questGiver` is not enough for canonical afterlife linkage
- Fix note:
  - quest detail теперь резолвит `relatedAfterlifeResidentId` в readable resident label и показывает exact afterlife source

### 118. Resident detail still omits revealed future-companion prompt and appearance motifs

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Resident Detail`, `Completeness`
- Problem:
  - resident model carries `futureCompanionPrompt` and `appearanceMotifs`
  - companion-related relic surfaces already use comparable snapshot data
  - resident detail can claim that history is revealed while still hiding these revealed canonical fragments
- Evidence:
  - [GuardianAbodeResidentState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentState.cs:1280)
  - [GuardianAbodeResidentState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentState.cs:1284)
  - [GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2556)
  - [SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1558)
  - [SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1588)
- Required fix:
  - resident detail should surface `futureCompanionPrompt` and `appearanceMotifs` as part of the revealed resident snapshot
  - canonical revealed history cannot stay half-hidden
- Fix note:
  - resident detail теперь показывает `futureCompanionPrompt` и `appearanceMotifs` рядом с остальным revealed imprint snapshot

### 119. Shining resolution and pending-politics inspection still leak raw internal reason and binding tokens

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Politics UI`, `Readable Output`
- Problem:
  - receipt detail prints `reason` verbatim, exposing raw reason codes like `founding_accepted`
  - pending leadership inspection appends raw binding tokens such as `resident:resident_mirael` or `radiant_actor:...` into player-facing lines
- Evidence:
  - [ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:1280)
  - [ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:1100)
  - [ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:1581)
- Required fix:
  - political receipt and pending-inspection output should humanize reason/result text and actor bindings
  - raw ids/tokens may remain only secondary diagnostics where really necessary
- Fix note:
  - politics receipt reasons теперь humanized into Russian player-facing phrases
  - pending leadership inspection больше не печатает raw composite bindings вроде `resident:resident_x` в primary text

### 120. Shining prompts and blessing inspection still expose pipeline/system tokens in player-facing text

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Prompts`, `Readable Output`
- Problem:
  - summon confirmation still says `accepted turn` and `gachaBaseResult.baseRarity`
  - forge confirmation still frames the result around `accepted turn`
  - politics/gates prompts still surface raw tokens like `abdication`, `peaceful_succession`, `supported`, `unsupported`, `meetingTag` and `routeSeedId`
- Evidence:
  - [ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:681)
  - [ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:904)
  - [ShiningAbode.Politics.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Politics.cs:217)
  - [ShiningAbode.Politics.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Politics.cs:343)
  - [ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:463)
  - [ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:503)
  - [ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:659)
- Required fix:
  - ordinary player-facing prompts and confirmations must use fully humanized Russian wording
  - pipeline/system vocabulary should not be the primary command UX
- Fix note:
  - Shining summon / forge confirmations переведены на русский player-facing словарь without `accepted turn` / `gachaBaseResult.baseRarity`
  - politics mode chooser и project selection больше не показывают raw `abdication` / `peaceful_succession` / `supported` / `unsupported`
  - Gates inspection больше не выводит `meetingTag` и `routeSeedId` в ordinary visible effect text

### 121. Shining gacha outcome and forge property inspection still drift or dump raw technical payload

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Outcome History`, `Forge UI`
- Problem:
  - gacha outcome detail resolves the banner through live faction labels instead of a stable receipt snapshot
  - forge inspection still prints a full raw technical property snapshot in visible UI
  - both problems make outcome screens less truthful and less human-readable than they should be
- Evidence:
  - [ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:889)
  - [ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:1416)
- Required fix:
  - gacha receipt detail should use snapshot-first faction/banner rendering
  - forge property mutation should be humanized first, with any raw technical dump moved to secondary diagnostics only
- Fix note:
  - gacha outcome inspection теперь берёт banner/faction label из stable receipt snapshot first
  - forge property inspection убрал raw technical property dump из ordinary visible UI, оставив humanized mutation summary

### 122. Chaos Sea rollback snapshot is taken after local resource mutations

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Turn Lifecycle`, `Rollback`
- Problem:
  - rollback backup is created only after local Chaos Sea commands already mutate state
  - feather spending and offering consumption can therefore happen before the engine takes the snapshot it later uses for rollback
  - failed or rejected turn paths can leave resources permanently spent because the rollback target is already post-mutation
- Evidence:
  - [GameEngine.TurnLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:567)
  - [GameEngine.TurnLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:629)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:529)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:818)
- Required fix:
  - rollback snapshot must be taken before any local Chaos Sea resource mutation that belongs to the pending turn
  - failed turn handling cannot rely on a snapshot that already includes the player-side spend
- Fix note:
  - explorer-side GM-bound Chaos Sea mutations now stage a pre-command rollback snapshot before local writes to `soul_state.json`, archive/offering state and related pending files
  - `ProcessPlayerTurn` merges that staged snapshot into the ordinary full rollback backup, so cancelled/failed turns restore true pre-spend state instead of post-mutation local state

### 123. system_guardian_attraction.json can still be silently deleted before validation

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Guardian Attraction`, `Pending Hygiene`
- Problem:
  - attraction request hygiene deletes the file on parse failure, missing fields and wrong-realm paths
  - the same helper runs during startup normalization and before validation
  - malformed deterministic attraction requests can disappear without ever surfacing as validation corruption
- Evidence:
  - [SystemGuardianLibraryService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/SystemGuardianLibraryService.cs:334)
  - [SystemGuardianLibraryService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/SystemGuardianLibraryService.cs:362)
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:713)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:1548)
- Required fix:
  - malformed active attraction contracts must be preserved and surfaced as corruption
  - destructive cleanup is only acceptable for truly stale/wrong-realm state, not parse/shape failure in active Chaos Sea flow
- Fix note:
  - `SystemGuardianLibraryService` now distinguishes `missing` from `malformed`, preserves malformed active `system_guardian_attraction.json`, and surfaces a corruption reminder instead of deleting the file on parse/shape failure
  - wrong-realm cleanup remains only for structurally valid stale attraction requests

### 124. Malformed guardian social request files still fail open as empty and get cleared or overwritten

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Guardian Social`, `Pending Contracts`
- Problem:
  - malformed guardian social request bundles currently deserialize to `Array.Empty`
  - health cleanup then deletes the file, and write paths can rebuild over the same corruption as if no pending contract existed
  - validation uses the same lossy reader and can therefore miss the malformed state entirely
- Evidence:
  - [ActorSocialInteractionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ActorSocialInteractionRequestState.cs:109)
  - [ActorSocialInteractionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ActorSocialInteractionRequestState.cs:261)
  - [ActorSocialInteractionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ActorSocialInteractionRequestState.cs:300)
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:723)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:4157)
- Required fix:
  - malformed pending guardian-social files must be distinguished from missing/empty state
  - active corruption cannot be silently deleted or overwritten by ordinary new request authoring
- Fix note:
  - guardian/NPC social pending readers now expose malformed state explicitly instead of collapsing to empty arrays
  - ordinary write paths fail closed on malformed pending files, runtime hygiene preserves corruption, and validation now surfaces malformed runtime state instead of silently skipping it

### 125. Save/load still preserves live Chaos Sea control files that should be ephemeral

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Save/Load`, `Control Files`
- Problem:
  - `SaveLoadService` only treats a subset of control files as ephemeral
  - resident pending bundles, guardian social requests and system guardian attraction are still serialized into saves and restored on load
  - stale pending contracts can therefore resurrect in a fresh runtime session
- Evidence:
  - [SaveLoadService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/SaveLoadService.cs:17)
  - [SaveLoadService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/SaveLoadService.cs:176)
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:18)
  - [ActorSocialInteractionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ActorSocialInteractionRequestState.cs:11)
  - [SystemGuardianLibraryService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/SystemGuardianLibraryService.cs:21)
- Required fix:
  - live Chaos Sea request/control files must be treated as ephemeral save/load artifacts unless they are explicitly designed to survive session boundaries
- Fix note:
  - `SaveLoadService` now treats resident pending bundles, guardian/NPC social request files and `system_guardian_attraction.json` as ephemeral control artifacts
  - save archives no longer serialize these live Chaos Sea contracts, and load cleanup removes them before runtime resumes

### 126. Player-founded Shining faction creation is still non-atomic

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Politics`, `Authoring`
- Problem:
  - the founding flow deducts Ink Feathers and mutates `lightSparks` before the pending founding request is durably persisted
  - `DeductInkFeathers` writes `soul_state.json` immediately, while `WriteFoundingRequestAsync` can still fail on malformed pending state
  - failure mid-sequence can leave spent resources without a matching pending founding contract
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.Politics.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Politics.cs:124)
  - [ExplorerMode.Afterlife.ShiningAbode.Politics.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Politics.cs:131)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:2619)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:1070)
- Required fix:
  - founding authoring must become projection-first / atomic enough that spent resources cannot outlive the missing pending contract
  - malformed pending-state write failure must not strand feathers or sparks
- Fix note:
  - Shining founding authoring now stages a local rollback snapshot, persists the pending founding contract before irreversible spend, and restores the local snapshot on any mid-sequence failure
  - feathers/sparks can no longer remain spent if pending founding persistence or Shining state write fails

### 127. Shining trade receipt validation still accepts receipts runtime will never treat as ready

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Trade`, `Validation vs Runtime`
- Problem:
  - validator still treats `soldOutCount` and closure markers too loosely
  - runtime readiness/cleanup requires those fields to be materially present and consistent
  - a receipt can therefore pass validation while trade remains blocked and the pending contract stays alive
- Evidence:
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:1065)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:209)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:289)
- Required fix:
  - receipt validation and runtime readiness must share one exact ready/closure contract
  - accepted-looking trade receipts cannot remain semantically unresolved at runtime
- Fix note:
  - `ValidationService.ShiningAbode` now requires canonical ready markers for Shining trade receipts: non-negative `soldOutCount`, positive `resolvedAtTurn` and non-empty `resolvedAtUtc`
  - static trade receipt validation and runtime ready/closure checks now use the same required marker set

### 128. Shining trade runtime can still unlock buying from a non-matching receipt because requestId is ignored

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade`, `Receipt Identity`
- Problem:
  - runtime readiness uses faction/cycle/status/item-count matching but ignores `receipt.requestId`
  - `ReadTradeViewAsync` can therefore mark inventory ready, and `BuyAsync` can proceed, on a receipt state that strict contract matching would reject
- Evidence:
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:209)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:76)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:292)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:1070)
- Required fix:
  - exact `requestId` must be part of authoritative trade readiness
  - local buy availability cannot be broader than strict receipt validation
- Fix note:
  - request-backed readiness now requires exact `requestId` through `FindMatchingReceipt`, and local view readiness only falls back to a unique authoritative same-cycle ready receipt when no pending request exists
  - runtime buying is no longer unlocked by a mismatched receipt that only happens to share faction/cycle/item counts

### 129. prepare_incarnation_package still handles duplicate selectedCardIds inconsistently across validation paths

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Core Actions`, `Prepared Package`
- Problem:
  - pending request validation de-duplicates `selectedCardIds` before package building
  - accepted-turn validation later replays the raw duplicate list
  - the same authored request can therefore pass one path and fail another, or produce a different expected package projection
- Evidence:
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:535)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:1418)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:7127)
- Required fix:
  - all request/accepted validation paths must use one duplicate-handling rule for `selectedCardIds`
  - prepared package projection cannot depend on which validator happened to read the request first
- Fix note:
  - duplicate `selectedCardIds` are now rejected consistently on request validation and accepted-turn projection paths
  - prepared package helpers no longer de-duplicate on one path while replaying raw duplicates on another

### 130. Archive entry and archive candidate screens still lack exact source-entry provenance and drill-down

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Afterlife Archive`, `Completeness`
- Problem:
  - archive entry detail does not surface full provenance/timing fields such as source-entry linkage and archived-at chronology in a player-usable way
  - archive candidates already carry `SourceEntryId`, but candidate detail still does not offer exact drill-down to the originating Codex entry
  - the player sees summary-level archive material without a direct path back to the source record
- Evidence:
  - [ExplorerMode.PrivateImplementation.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs:59)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:187)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:972)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1052)
- Required fix:
  - archive entry and candidate screens need exact source-entry drill-down and clearer provenance/timing rendering
  - source linkage cannot stay hidden when the canonical id already exists in the snapshot
- Fix note:
  - archive entries now surface `sourceEntryId` and archive timing directly in the detail screen
  - archive entry and candidate screens now offer an exact Codex-source drill-down path instead of leaving source provenance at summary level only

### 131. Full Shining politics history still drifts because founding supporters are resolved from the live resident roster

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Politics History`, `Snapshot Fidelity`
- Problem:
  - full founding history still resolves supporter labels through current resident state instead of a frozen receipt snapshot
  - resident renames or edits can therefore rewrite older political history
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:1190)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:1588)
- Required fix:
  - supporter names for historical founding receipts must be snapshot-first
  - live resident lookup should remain only a clearly legacy fallback, not the primary rendered history
- Fix note:
  - full founding history now renders supporter blocks snapshot-first through `supportingResidentLabels` when present
  - when stable supporter labels are absent, the UI falls back to stable ids instead of re-resolving current resident names and drifting historical receipts

### 132. Shining trade, gacha, forge and prompt surfaces still leak raw canonical or workflow tokens

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `UI Readability`, `Prompts`
- Problem:
  - full trade/gacha inspection still prints raw canonical tokens like rarity enums and merchant profile identifiers
  - some prompts still expose workflow/system wording such as `accepted turn`, `pending request` or raw cycle ids
  - forge inspection also leaks raw zero-based `propertyIndex` in player-facing text
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:105)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:271)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:895)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:907)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:978)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2188)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:1136)
- Required fix:
  - ordinary Shining and related afterlife prompts must be fully humanized in primary text
  - raw canonical/workflow identifiers may remain only in secondary diagnostics where they are truly needed
- Fix note:
  - Shining trade/gacha inspections now humanize merchant profile and rarity output, replace raw property index with player-facing property numbering, and stop using raw return-cycle text as primary overview wording
  - affected afterlife/Shining prompts were rewritten away from `accepted turn`, `pending request` and raw `gachaBaseResult.baseRarity` wording in their primary text

### 133. /душа still exposes raw memory-legacy protocol tokens and key=value dumps

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea`, `Soul UI`, `Readable Output`
- Problem:
  - soul screen still renders `legacyType`, `grantSource`, `applicationState` and generic memory-legacy snapshot blobs as raw protocol text
  - the player sees canonical storage vocabulary instead of a human-readable description of the memory legacy state
- Evidence:
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:232)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:299)
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:313)
  - [ValidationService.AfterlifeArchiveTradeAndLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.AfterlifeArchiveTradeAndLifecycle.cs:838)
- Required fix:
  - memory-legacy state in `/душа` should be rendered through readable labels and descriptions
  - raw protocol fields and `key=value` blobs should leave the primary player-facing surface
- Fix note:
  - `/душа` now humanizes memory-legacy type/source/application state and renders snapshot details through readable labels instead of raw protocol values
  - the old `key=value` dump format has been replaced with player-facing `метка — значение` rendering in the primary soul inspection surface

### 134. Unresolved currentRealm still triggers destructive wrong-realm cleanup across afterlife and Shining pending contracts

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Shining Abode`, `Runtime Hygiene`
- Problem:
  - when `soul_state.currentRealm` is missing or unreadable, `StateManager` leaves runtime realm as empty string
  - session hygiene immediately feeds that unresolved realm into afterlife/Shining cleaners
  - multiple cleaners still interpret `""` as confirmed wrong realm and delete live pending/control contracts instead of preserving them fail-closed
- Evidence:
  - [StateManager.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/StateManager.cs:124)
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:705)
  - [AfterlifeReturnGuardService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeReturnGuardService.cs:80)
  - [GuardianAbodeOfferingState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeOfferingState.cs:116)
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:250)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:339)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:593)
- Required fix:
  - unresolved realm must be treated as blocking/ambiguous state, not as ordinary wrong realm
  - destructive cleanup may run only on proven non-afterlife / non-Shining realm authority
- Fix note:
  - unresolved realm теперь preserve-ит guardian/NPC social и NPC trade pending contracts так же fail-closed, как и afterlife/Shining cleaners
  - destructive wrong-realm cleanup больше не запускается на `currentRealm = ""`

### 135. Whitespace pending_shining_trade_inventory_requests.json still fails open as empty state

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Trade`, `Pending Contracts`
- Problem:
  - whitespace-only `pending_shining_trade_inventory_requests.json` is classified as valid empty state instead of malformed corruption
  - `EnsureHealthyAsync()` then deletes the file as if no pending request existed
  - ordinary write paths can proceed over this corruption and recreate a new trade contract on top of it
- Evidence:
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:107)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:139)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:349)
- Required fix:
  - whitespace-only active pending trade file must be classified as malformed
  - malformed pending trade state cannot be auto-deleted or overwritten by new writes
- Fix note:
  - verification текущего кода подтвердила, что whitespace-only `pending_shining_trade_inventory_requests.json` уже классифицируется как malformed corruption и не auto-delete-ится
  - write path по-прежнему blocked на malformed pending file; дополнительных code changes в этом pass не потребовалось

### 136. Gates and prepared-package normalization still silently rewrites corrupted blessing-card state and persists it

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Gates`, `Prepared Package`, `Normalization`
- Problem:
  - normalization still defaults invalid blessing-card fields instead of surfacing corruption
  - unsupported `sourceType`, `effectFamily`, `rarity` and missing `effectPayload` are silently rewritten to canonical defaults
  - mismatched `selectedCardIds` are rebuilt from `selectedCards`, and main-menu sync then writes the rewritten state back to disk
- Evidence:
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:871)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:891)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1385)
- Required fix:
  - active corrupted gates/package blessing-card state must fail-close instead of self-healing into silently persisted defaults
  - normalization should remain limited to non-destructive legacy/readability cleanup only
- Fix note:
  - verification текущего raw-owner validation подтвердила fail-close contract для corrupted blessing-card data: actionable mode блокируется до normalization rewrite
  - main-menu sync больше не пишет self-healed gates/package contracts обратно в ordinary flow; дополнительных code changes в этом pass не потребовалось

### 137. Local Shining buy still is not atomic across shining_abode_state.json and soul_state.json

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `Trade`, `State Consistency`
- Problem:
  - local buy still writes `shining_abode_state.json` and `soul_state.json` separately
  - if the second write fails, the code only performs best-effort rollback of the first file and explicitly warns that manual recovery may be needed
  - split state between sold-out trade slot and soul relic / feather state is therefore still possible under partial write failure
- Evidence:
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:353)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:374)
- Required fix:
  - local buy needs one stronger atomicity contract or explicit rollback authority that cannot leave split Shining/soul state behind
  - partial-write manual recovery should not remain part of the ordinary successful user flow
- Fix note:
  - local Shining buy теперь commit-ит coordinated state writes с rollback только уже записанных файлов
  - ordinary failure path больше не ведёт в manual-recovery wording; partial second-write failure откатывает витрину к pre-buy snapshot и возвращает explicit failed result

### 138. prepare_incarnation_package cleanup and lifecycle matching still treat selectedCardIds as an unordered set

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Core Actions`, `Prepared Package`
- Problem:
  - request validation already treats `selectedCardIds` as exact ordered snapshot
  - cleanup and lifecycle receipt matching still collapse those ids into unordered sets
  - reordered receipts can therefore clear pending package actions even though the canonical package contract is order-sensitive
- Evidence:
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:690)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:7628)
- Required fix:
  - cleanup, receipt matching and accepted-turn validation must all use the same exact ordered selected-card contract
- Fix note:
  - verification текущего cleanup/receipt matching подтвердила ordered `selectedCardIds` contract через `SequenceEqual`
  - reordered receipts больше не закрывают pending package actions; дополнительных code changes в этом pass не потребовалось

### 139. Full trade-cycle inspection cannot reach its strict current-contract receipt branch

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade UI`, `History Fidelity`
- Problem:
  - the full trade-cycle inspection builds a synthetic current contract with a fresh autogenerated `requestId`
  - exact receipt matching then necessarily fails, because strict matching requires the real pending request id
  - even when an authoritative current-cycle ready receipt exists, the screen falls back to same-cycle historical mode instead of proving the strict current contract
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:79)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:341)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:304)
- Required fix:
  - full inspection must distinguish true strict current-contract proof from same-cycle historical fallback using the real authoritative request identity
- Fix note:
  - verification текущего inspection path подтвердила strict current-contract branch по реальному pending `requestId`
  - same-cycle fallback остаётся отдельным historical mode и больше не подменяет strict proof; дополнительных code changes в этом pass не потребовалось

### 140. Preserved malformed afterlife pending contracts still disappear from reminder and notification surfacing

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Afterlife`, `Reminders`
- Problem:
  - several pending files are now preserved fail-closed when malformed, but their public readers still collapse corruption into `null` or empty collections
  - reminder and notification builders then behave as if no pending contract exists
  - this hides live corruption from ordinary runtime/GM surfacing even though the file remains on disk
- Evidence:
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:416)
  - [GuardianAbodeResidentRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs:695)
  - [PlayerGuardianFoundationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/PlayerGuardianFoundationState.cs:154)
  - [PlayerGuardianFoundationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/PlayerGuardianFoundationState.cs:400)
  - [GuardianTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianTradeRequestState.cs:109)
  - [AfterlifeNotificationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeNotificationState.cs:441)
- Required fix:
  - fail-closed malformed pending contracts must remain visible in reminder/notification paths instead of dropping out as absent state
- Fix note:
  - verification reminder/notification paths подтвердила corruption surfacing для preserved malformed afterlife pending contracts
  - live malformed state больше не исчезает как absent contract; дополнительных code changes в этом pass не потребовалось

### 141. Duplicate same-cycle pending Shining trade requests are still not rejected and make runtime behavior order-dependent

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Trade`, `Validation`
- Problem:
  - validator still does not reject duplicate same-cycle pending trade requests for the same faction
  - runtime trade view then reads them with `FirstOrDefault(factionId + tradeCycleId)`
  - corrupted pending files can therefore produce order-dependent readiness, inspection and cleanup behavior
- Evidence:
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:191)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:1524)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:69)
- Required fix:
  - pending Shining trade request validation must reject duplicate same-cycle contracts per faction before runtime ever reads them
- Fix note:
  - verification текущего validator path подтвердила explicit reject duplicate same-cycle pending trade requests per faction
  - runtime больше не зависит от `FirstOrDefault` на corrupted same-cycle set; дополнительных code changes в этом pass не потребовалось

### 142. Shining politics and notification surfaces still expose raw internal ids and technical tokens in player-facing history

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Politics UI`, `Notifications`, `Readable Output`
- Problem:
  - full political realignment history still stops at raw `residentHistoryEntryId` instead of showing the linked resident-history fragment or exact drill-down
  - Shining notification summaries still leak technical tokens such as raw rarity transitions, raw target-form tags and internal forge property suffixes
  - this leaves historical/player-facing surfaces partially readable only to someone who already knows the protocol layer
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:1218)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:1219)
  - [AfterlifeNotificationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeNotificationState.cs:1643)
  - [AfterlifeNotificationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeNotificationState.cs:1645)
  - [AfterlifeNotificationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeNotificationState.cs:1686)
  - [AfterlifeNotificationState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/AfterlifeNotificationState.cs:1692)
- Required fix:
  - political history should expose the actual resident-history outcome or an exact drill-down path
  - Shining notification/player-history text must humanize these internal tokens and keep protocol details secondary only
- Fix note:
  - verification текущих politics/history and notification summaries подтвердила resident-history fragment rendering и humanized rarity/form/property summaries
  - raw protocol tokens больше не используются как primary text в этих surfaces; дополнительных code changes в этом pass не потребовалось

### 143. Realm transition helpers still report success when soul_state realm write fails

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Shining Abode`, `Realm Transitions`, `Atomicity`
- Problem:
  - `UpdateSoulStateRealm()` swallows missing/unreadable `soul_state.json` and write failures
  - callers continue as if the realm switch succeeded, clear pending state, refresh runtime and render transition UI
  - this can leave canonical soul realm unchanged while the game announces a successful move to `Chaos Sea` or `Shining Abode`
- Evidence:
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1162)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1186)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1201)
  - [GameEngine.IncarnationAndAfterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.IncarnationAndAfterlife.cs:95)
  - [GameEngine.IncarnationAndAfterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.IncarnationAndAfterlife.cs:130)
  - [GameEngine.TurnLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:1909)
- Required fix:
  - realm transition helpers must propagate failure to callers instead of silently swallowing it
  - cleanup/UI success paths cannot run unless canonical `soul_state.currentRealm` was durably updated
- Fix note:
  - `UpdateSoulStateRealm()` теперь возвращает explicit success/failure instead of swallowing missing/unreadable/write failures
  - re-enter Shining, return-to-Chaos-Sea и ascension flows теперь abort/rollback coordinated writes и не рендерят success transition без durable soul realm update

### 144. New Game Plus rebuild still destroys game_state before replacement state is durably written

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `New Game Plus`, `Save Integrity`
- Problem:
  - `HandleNewGamePlus()` clears the full `game_state` tree before any replacement files are durably restored
  - any failure mid-rebuild can leave a partially erased save with no rollback path
- Evidence:
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1258)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1277)
- Required fix:
  - New Game Plus reset must stage a recoverable rebuild plan instead of destructive clear-first semantics
  - partial failure cannot strand the save in a half-erased state
- Fix note:
  - New Game Plus теперь создаёт full game-session safety backup перед destructive reset
  - failure path восстанавливает исходный `game_session` вместо clear-first partial save corruption

### 145. Life-evaluation reward delta validation still fails open when currentRealm cannot be resolved

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Validation`, `TriggerLifeEnd`
- Problem:
  - `ValidateNoLifeEvaluationRewardsOnTriggerTurnAsync()` reads `currentRealm` through `TryResolveCurrentRealmAsync()`
  - when realm resolution fails, the validator returns early and skips reward-delta enforcement instead of surfacing a fail-closed issue
- Evidence:
  - [ValidationService.BootstrapAndProtocol.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.BootstrapAndProtocol.cs:376)
  - [ValidationService.BootstrapAndProtocol.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.BootstrapAndProtocol.cs:387)
  - [ValidationService.BootstrapAndProtocol.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.BootstrapAndProtocol.cs:388)
- Required fix:
  - unresolved realm must not bypass life-evaluation reward validation
  - unreadable trigger-turn realm should surface as an explicit validation failure
- Fix note:
  - TriggerLifeEnd reward validation теперь fail-close surface-ит `life_trigger_turn_missing_realm_authority`, если canonical realm authority unreadable
  - unresolved `currentRealm` больше не bypass-ит reward-delta enforcement

### 146. Shining political validation still accepts enum-backed values that runtime silently coerces to defaults

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Validation`, `Political State`
- Problem:
  - validation currently checks shape and required fields, but not canonical enum values for `leadershipState`, `actorType` and `politicalStatus`
  - runtime normalization later coerces these invalid values into defaults such as secure/elder/radiant actor
  - this leaves a validator/runtime divergence where malformed political state passes validation and then gets silently rewritten
- Evidence:
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:604)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:661)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:716)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:805)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:812)
- Required fix:
  - validation must enforce canonical enum-backed values for political objects
  - runtime normalization cannot remain the first component that notices malformed political enums
- Fix note:
  - validation теперь explicit reject-ит invalid `leadershipState`, `actorType` и `politicalStatus`
  - runtime normalization больше не остаётся первым местом, где malformed political enums silently коэрсятся в defaults

### 147. prepare_incarnation_package receipts can still display stale selectedCards snapshots

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode`, `Core Actions`, `Receipt History`
- Problem:
  - receipt hydration returns early when `selectedCards` already exists
  - validation mostly checks array shape, while inspection renders `selectedCards` before falling back to `selectedCardIds`
  - a receipt can therefore keep a stale selected-card snapshot that no longer matches the canonical id list
- Evidence:
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:1168)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:1224)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:916)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:928)
- Required fix:
  - receipt validation must prove `selectedCards` matches `selectedCardIds`, not just array shape
  - stale selected-card snapshots cannot remain player-facing canonical history
- Fix note:
  - state validation теперь explicit reject-ит prepare-package receipts, где `selectedCards` расходится с `selectedCardIds`
  - inspection fallback больше не доверяет stale stored snapshot и уходит к canonical id-based rendering при mismatch

### 148. Exact inbox detail fallbacks still expose raw archive and project state tokens

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Afterlife Inbox`, `Readable Detail`
- Problem:
  - exact archive detail fallback still prints raw `archiveType`
  - exact project detail fallback still prints raw `activeState` / `finalState` instead of already existing humanized labels
  - when richer snapshot labels are missing, the fallback path still leaks internal tokens into player-facing exact detail
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:575)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:629)
- Required fix:
  - fallback archive/project detail must use the same humanized labels as dedicated screens
  - raw internal state/type tokens may remain only as secondary diagnostics
- Fix note:
  - exact inbox archive fallback теперь humanize-ит `archiveType` через archive entry label formatter
  - project detail fallback и stored snapshot path теперь humanize-ят `activeState` / `finalState` через shared guardian project state labels

### 149. Codex, relic and forge detail screens still use canonical keys as primary labels

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea`, `Shining Abode`, `Detail UI`, `Readable Output`
- Problem:
  - Codex detail still prints raw `category` / `subcategory`
  - soul relic detail still prints raw `category`
  - forge-property fallback still prints raw `stat` when no friendly mapping exists
  - several detail panes therefore remain protocol-shaped instead of player-facing
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1146)
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1338)
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:1441)
- Required fix:
  - primary detail text must use human-readable labels and descriptions
  - canonical keys can remain only in secondary diagnostic blocks when unavoidable
- Fix note:
  - Codex detail теперь humanize-ит `category/subcategory`, soul relic detail humanize-ит `category`, а forge property fallback humanize-ит `stat`
  - raw canonical keys остались только вторичным diagnostic слоем, где это действительно нужно

---

## Working Rule For Future Passes

- Этот документ теперь считается отдельным рабочим backlog для **свежих** проблем
- Идти по нему нужно последовательными fix-pass’ами, не пытаясь исправить всё за раз
- После каждого pass:
  - переводить пункт в `In Progress` или `Fixed`
  - добавлять `Fix note`
  - перечислять изменённые файлы и tests
- Если в ходе исправления всплывёт новая независимая проблема, добавлять её новым пунктом начиная с `150`
