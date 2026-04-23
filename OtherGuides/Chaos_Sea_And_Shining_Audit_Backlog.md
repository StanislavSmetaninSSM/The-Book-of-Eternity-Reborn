# Chaos Sea And Shining Audit Backlog

## Purpose

Этот документ фиксирует найденные проблемы по:

- геймплею Моря Хаоса
- геймплею Сияющей Обители
- полноте player-facing данных в командах Моря Хаоса
- полноте player-facing данных в командах Сияющей Обители

Документ нужен как **рабочий backlog на исправление**.
После каждого fix-pass его надо обновлять:

- переводить пункты из `Open` в `In Progress` или `Fixed`
- добавлять короткую заметку, что именно было исправлено
- не удалять уже закрытые пункты, а оставлять их в истории этого backlog

---

## Status Legend

- `Open` — проблема подтверждена и ещё не исправлена
- `In Progress` — сейчас исправляется
- `Fixed` — исправлена в коде и документ обновлён
- `Deferred` — сознательно отложена

---

## Current Baseline Status

- Текущая implementation baseline: **`1–22` завершены как исходная fix-wave**
- Verification follow-up wave: **`23–42` закрыты**
- Текущих открытых residual/test-hardening follow-up пунктов: **нет**
- Этот документ теперь служит:
  - историей уже выполненных fix-pass'ов
  - текущим source of truth для **новых** проблем Chaos Sea / Shining
- Рабочее правило:
  - не переоткрывать закрытые пункты без нового фактического дефекта
  - новые находки добавлять **новыми пунктами ниже**, начиная с `43`

---

## Recommended Fix Order

Закрыто в текущем pass:

- `/reenter_shining_abode` больше не должен сбрасывать активную Shining-session
- `/return_to_chaos_sea` больше не должен обнулять Просветление в обычном выходе
- normalizer больше не должен терять Shining faction membership у ascended residents
- ordinary `/return_to_chaos_sea` теперь purge-ит pending Shining request files и runtime hygiene больше не держит их вне active Shining mode
- Shining trade теперь enforce-ит уникальные `slotId` в validation/contract и не допускает ambiguous purchase path
- forge blessing entitlements теперь пишут реальный `consumedAtTurn/consumedAtUtc`, а не lifecycle marker с turn `0`
- duplicate check для pending founding requests теперь реально ловит повторный `ProposedFactionId`/`ProposedHallId`
- persistence write-path для pending founding requests теперь тоже дедуплицирует и `ProposedFactionId`, и `ProposedHallId`, даже если validation-path был обойдён
- founded guardian больше не downcast-ится silently в generic `ascended_guardian` при guardian-derived Shining materialization
- `/guardians` теперь явно маркирует active guardian в списке и detail-view, `/душа` раскрывает полный soul-state overview, companion relic detail показывает missing canonical payload, а guardian trade detail использует реальный derived slot count
- guardian journals получили explicit full drill-down, а `/проекты_хранителей` и `/сила_обители` переведены на полный player-facing vocabulary без developer shorthand
- `/shining_abode` теперь раскрывает halls и radiant actors не только через counts, у prepared package виден frozen selected-card payload, а в Shining politics появился resident-level faction inspection
- `/shining_abode` теперь даёт полный осмотр исходов Обители, торговых циклов и политических решений фракций: core receipts, trade lifecycle/accounting и political resolution context больше не скрывают canonical payload
- Shining forge authoring переведён на preview-driven flow: кузня больше не начинает с raw action ids / `formTag` / `propertyIndex`, а ведёт игрока через выбор действия, реликвии, projected outcome preview и отдельное подтверждение запроса
- help text и Shining screens выровнены на player-facing русский словарь: больше нет `Trade и forge`, `Stored ... contracts`, `Pending request`, `Receipt proof` и похожего mixed shorthand в основных Shining surfaces
- остаточные English/internal labels в `/проекты_хранителей` и `/сила_обители` убраны из audit-блоков: больше нет `Power loss`, `Pressure relief`, `Stability relief`, `Safe pressure`, `Defense rating` и `Последний power event`
- основной reshape-flow кузни теперь humanize-ит формы реликвии: raw `formTag` больше не торчит в chooser и preview, а request payload по-прежнему пишет canonical token
- `/сила_обители` больше не течёт остаточными internal labels: `canonical history`, `value`, `applications`, `modifierId` и `Terminal state` переведены на player-facing русский словарь
- `/сила_обители` больше не светит raw `modifierType` и mixed `rival-Хранителя`: временные модификаторы и hostile power reasons теперь используют единый player-facing русский словарь
- fallback reshape-flow кузни больше не светит raw `currentFormTag`: default prompt humanize-ит форму реликвии, а player-facing ввод формы нормализуется обратно в canonical `formTag` только во внутреннем payload
- fallback-ветки `retune_property` и `uplift_rarity` в кузне больше не начинают authoring с raw JSON: теперь они сначала предлагают player-facing шаблон/подготовленный набор и только потом уводят в manual JSON как explicit advanced path
- forge-local prompts и ошибки больше не текут mixed English/canonical wording: вместо `blessing rerolls`, `JSON object`, `JSON array`, `Soul Relics` и `canonical properties array` кузня теперь использует единый русский player-facing словарь
- completed guardian project surfaces больше не светят raw `finalState`: summary завершённых проектов в `/хранители` и detail panel completed project в `/проекты_хранителей` теперь используют тот же player-facing state formatter, что и active surfaces
- guardian trade detail в `/хранители` теперь подтверждён behavior-level command test: player-facing detail-view реально рендерит derived `TradeSlotCount`, а не только проходит source-guard
- полный guardian journal drill-down теперь подтверждён behavior-level command test: `/хранители` не только показывает action полного журнала, но и реально дорисовывает поздние thought/social entries за пределами preview truncation
- validation ветка founding requests теперь подтверждена отдельным hall-collision regression test: duplicate pending `ProposedHallId` reject-ится не только в write-path, но и в `ValidateFoundingRequestAgainstCurrentStateAsync(...)`
- validation founding requests теперь покрыта и на ветке уже materialized hall: collision с существующим `hallId` в canonical Shining state подтверждён отдельным regression test
- detail-view активного Хранителя теперь подтверждён command-level test: player-facing header действительно показывает `Азалия · активный`, а не держится только на source-guard
- `/помощь` теперь подтверждена render-level tests в Chaos Sea и Shining Abode: help panel действительно рендерит player-facing формулировки без `Late-game` и `New Game+ reset`, а не закрыта только source-guard’ом
- completed guardian project summary в `/хранители` теперь подтверждён command-level test: summary row реально показывает player-facing state label `Завершён`, а не raw `Completed`

Следующие шаги:

1. Новые находки, если появятся, добавлять новыми пунктами, начиная с `43`

---

## Gameplay Defects

### 1. Shining re-entry resets active session

- Status: `Fixed`
- Severity: `Critical`
- Scope: `Shining Abode`, `Chaos Sea`
- Problem:
  - обычный `/reenter_shining_abode` сейчас вызывает `ActivateForAscension`
  - из-за этого повторный вход ведёт себя как новое Вознесение:
    - `lightSparks` снова становятся `100`
    - сбрасываются per-ascension counters
    - сбрасываются gates / draft-related parts active session
  - это превращает обычный возврат в exploit/reset, а не в honest re-entry
- Evidence:
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1113)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:65)
  - [GameEngine.TurnLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:2200)
- Required fix:
  - отделить ordinary re-entry от ascension activation
  - re-entry должен возвращать игрока в уже существующий active Shining state без refill/reset
- Fix note:
  - ordinary re-entry переведён на отдельный `ReenterOrdinaryActiveState`, который нормализует и возвращает активное состояние без сброса `lightSparks`, gates и per-ascension counters
  - обновлены:
    - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs)
    - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs)
    - [ShiningAbodeStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningAbodeStateTests.cs)
  - покрыто тестом:
    - `ReenterOrdinaryActiveState_PreservesTransientStateAndCounters`

### 2. Ordinary return from Shining resets enlightenment

- Status: `Fixed`
- Severity: `High`
- Scope: `Chaos Sea`, `Shining Abode`
- Problem:
  - локальный `/return_to_chaos_sea` декларируется как обычный выход без destructive New Game+ reset
  - код всё равно обнуляет:
    - `enlightenment.currentTier`
    - `enlightenment.experience`
    - `enlightenment.level`
    - `enlightenment.progressPercent`
  - это ломает player-facing contract команды
- Evidence:
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1153)
  - [GameEngine.TurnLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:2200)
- Required fix:
  - ordinary return не должен трогать enlightenment
  - destructive reset должен оставаться только в explicit New Game+ path
- Fix note:
  - обычный `/return_to_chaos_sea` больше не переписывает `soul_state.enlightenment`; теперь локальный exit-path меняет только `currentRealm = Chaos Sea` и сохраняет остальной soul-state без destructive reset
  - обновлены:
    - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs)
    - [GameEngineSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/GameEngineSourceGuardTests.cs)
  - покрыто тестом:
    - `OrdinaryReturnToChaosSea_MustNotResetEnlightenment`

### 3. Normalizer can silently wipe ascended resident faction membership

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, `guardian_abode_residents`
- Problem:
  - normalizer сначала прогоняет `guardian_abode_residents`, а только потом `shining_abode_state`
  - active guardian faction materialize-ится позже
  - до materialization resident normalizer может решить, что фракция не существует, и занулить:
    - `shiningFactionId`
    - loyalty/restlessness fields
  - это риск тихой потери faction membership
- Evidence:
  - [CanonicalStateNormalizer.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/CanonicalStateNormalizer.cs:147)
  - [CanonicalStateNormalizer.GuardiansAndProjects.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs:348)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:514)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:775)
- Required fix:
  - выровнять порядок normalizer passes или расширить context resident pass’а
  - resident normalization не должна уничтожать Shining affiliation до завершения Shining materialization
- Fix note:
  - resident normalization теперь строит temporary Shining context с `guardians.json`, а не только с сырым `shining_abode_state.json`; это materialize-ит active-guardian faction до `NormalizeResidentShiningFields` и сохраняет affiliation ascended residents
  - normalizer serialization заодно ужесточён через `TypeInfoResolver`, чтобы materialized Shining writes не падали на программно созданных `JsonNode` values
  - обновлены:
    - [CanonicalStateNormalizer.GuardiansAndProjects.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs)
    - [CanonicalStateNormalizer.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/CanonicalStateNormalizer.cs)
    - [CanonicalStateNormalizerTests.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/CanonicalStateNormalizerTests.ShiningAbode.cs)
  - покрыто тестом:
    - `NormalizeAccumulatedStateAsync_AscendedResidentKeepsActiveGuardianFactionMembership`

### 4. Pending Shining request files survive exit to Chaos Sea

- Status: `Fixed`
- Severity: `High`
- Scope: `Shining Abode`, lifecycle, validation
- Problem:
  - при выходе через `/return_to_chaos_sea` pending Shining request files не чистятся
  - core/trade requests могут пережить уход в `Chaos Sea`
  - political requests вообще не имеют cleanup-path
  - дальше stale requests продолжают участвовать в reminder/validation loop
- Evidence:
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1125)
  - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs:227)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:277)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:520)
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:298)
- Required fix:
  - при ordinary exit из active Shining надо purge-ить или canonical-closeить pending Shining requests
  - cleanup path должен быть симметричным для core, trade и faction politics
- Fix note:
  - ordinary `/return_to_chaos_sea` теперь явно purge-ит pending core/trade/politics request files перед post-exit refresh
  - runtime hygiene тоже ужесточён: core/trade/politics pending Shining requests теперь очищаются вне `currentRealm = Shining Abode` и вне `availability = active`
  - для political requests добавлен отсутствовавший раньше `EnsureHealthyAsync`, и он подключён в общий runtime artifact normalizer
  - обновлены:
    - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs)
    - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs)
    - [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs)
    - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs)
    - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs)
    - [ShiningCoreActionRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningCoreActionRequestStateTests.cs)
    - [ShiningTradeRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningTradeRequestStateTests.cs)
    - [ShiningFactionRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningFactionRequestStateTests.cs)
    - [GameEngineSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/GameEngineSourceGuardTests.cs)
  - покрыто тестами:
    - `EnsureHealthyAsync_ChaosSeaClearsPendingRequests` для core/trade/politics request states
    - `OrdinaryReturnToChaosSea_MustPurgePendingShiningRequests`
    - `RuntimeUiArtifactNormalizer_MustEnsureShiningFactionRequestsHealthy`

### 5. Shining trade allows duplicate slot ids

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining trade`
- Problem:
  - нигде не проверяется уникальность `tradeInventory.items[].slotId`
  - UI выбирает товар по индексу, а покупка потом идёт по `slotId` и берёт первый матч
  - при duplicate `slotId` игрок может выбрать один слот, а купить другой
- Evidence:
  - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs:601)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:233)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:279)
  - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs:287)
- Required fix:
  - enforce unique `slotId` in validation
  - optionally harden buy path against duplicates too
- Fix note:
  - `shining_abode_state` validation теперь поднимает explicit `shining_trade_inventory_duplicate_slot_id`
  - trade request contract тоже reject-ит duplicate `slotId`, так что stale/invalid ready inventories больше не считаются canonical current-cycle stock
  - local `BuyAsync` получил early corruption-check и не пытается покупать из ambiguous duplicate-slot inventory
  - обновлены:
    - [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs)
    - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs)
    - [ShiningTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeService.cs)
    - [ShiningTradeRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningTradeRequestStateTests.cs)
    - [ShiningTradeResolutionValidationTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningTradeResolutionValidationTests.cs)
  - покрыто тестами:
    - `InventoryMatchesRequestContract_DuplicateSlotIds_Fails`
    - `BuyAsync_DuplicateSlotIds_Fails`
    - `ValidateShiningTradeInventoryObject_DuplicateSlotIds_Fails`

### 6. Forge blessing consumption writes `consumedAtTurn = 0`

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining forge`, blessing lifecycle
- Problem:
  - forge helper тратит blessing entitlements с `currentTurnNumber: 0`
  - в результате consumed lifecycle markers пишутся с ложным turn number
  - audit/history и strict lifecycle projection становятся неконсистентными
- Evidence:
  - [ShiningAbodeState.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.TradeAndForge.cs:331)
  - [ShiningBlessingEffectState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningBlessingEffectState.cs:1749)
- Required fix:
  - consumption helper должен получать реальный current turn
  - blessing lifecycle markers должны писать canonical turn/utc values
- Fix note:
  - `TryApplyForgeAction` теперь принимает явный `currentTurnNumber` и пробрасывает его в `ConsumeForgeEntitlements`
  - accepted-turn forge projection в lifecycle validation теперь использует `receipt.resolvedAtTurn`, так что expected blessing audit и current state больше не расходятся по turn marker
  - обновлены:
    - [ShiningAbodeState.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.TradeAndForge.cs)
    - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs)
    - [ShiningAbodeTradeAndForgeStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningAbodeTradeAndForgeStateTests.cs)
    - [ShiningCoreActionResolutionValidationTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningCoreActionResolutionValidationTests.cs)
  - покрыто тестом:
    - `ForgeBlessingEntitlements_MakeReshapeFreeAndConsumeTheFlag`

### 7. Player-founded guardian loses special semantics in Shining materialization

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Player-founded guardian`, `Shining Abode`
- Problem:
  - при интеграции в Shining materialization founded guardian маршрутизируется как generic `ascended_guardian`
  - теряется distinction между обычным guardian и founded-guardian branch
- Evidence:
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:775)
  - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs:818)
  - [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs:6598)
- Required fix:
  - либо сохранить founded semantics в Shining projection
  - либо явно зафиксировать canonical downcast и surfacing, если это сознательный design choice
- Fix note:
  - guardian-derived Shining materialization теперь сохраняет founded semantics: active guardian с `originType=player_founded_ascended_soul` materialize-ится как founded projection с `originType=player_founded`, а не как generic `ascended_guardian`
  - upgrade-path тоже закрыт: уже существующие derived factions/halls для founded guardian при re-entry и normalizer pass'е обновляются до founded-specific summary/description вместо молчаливого downcast
  - Shining overview теперь различает такого главу как `основанный хранитель`, так что founded branch виден и в player-facing summaries
  - обновлены:
    - [ShiningAbodeState.Gameplay.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs)
    - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
    - [ShiningAbodeStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningAbodeStateTests.cs)
    - [CanonicalStateNormalizerTests.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/CanonicalStateNormalizerTests.ShiningAbode.cs)
  - покрыто тестами:
    - `ActivateForAscension_WithFoundedGuardian_MaterializesPlayerFoundedProjection`
    - `ReenterOrdinaryActiveState_UpgradesExistingFoundedGuardianProjection`
    - `NormalizeAccumulatedStateAsync_PlayerFoundedGuardianKeepsFoundedShiningProjection`

### 8. Founding request duplicate check is ineffective

- Status: `Fixed`
- Severity: `Low`
- Scope: `Shining politics`
- Problem:
  - проверка дубликатов founding requests устроена так, что duplicate `ProposedFactionId` можно пропустить
  - это contract hole для malformed/manual pending state
- Evidence:
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:272)
- Required fix:
  - переписать duplicate check на прямую проверку по всем pending founding requests
- Fix note:
  - founding duplicate-check теперь исключает только тот же `requestId`, а не весь matching `ProposedFactionId`, поэтому pending duplicate faction/hall requests действительно режутся contract validation
  - обновлены:
    - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs)
    - [ShiningFactionRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningFactionRequestStateTests.cs)
  - покрыто тестом:
    - `ValidateFoundingRequestAgainstCurrentStateAsync_DuplicatePendingFactionId_Fails`

---

## Player-Facing Data Gaps: Chaos Sea

### 9. `/guardians` does not show which guardian is active

- Status: `Fixed`
- Severity: `High`
- Problem:
  - список Хранителей не показывает `activeGuardian`
  - при этом active-state влияет на торговлю и interactions
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:64)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:124)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1822)
- Required fix:
  - list labels и detail header должны явно маркировать active guardian
- Fix note:
  - `/guardians` теперь помечает active guardian прямо в choice labels, summary/status и detail header; в banner части списка также surfaced текущий active guardian по имени
  - обновлены:
    - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_Guardians_ShowsActiveGuardianMarker`
    - `ExplorerMode_GuardianUi_MustMarkActiveGuardianAndUseDerivedTradeSlotCount`

### 10. `/душа` underreports canonical soul state

- Status: `Fixed`
- Severity: `High`
- Problem:
  - экран показывает только realm, текущие Перья и rough enlightenment tier
  - скрыты:
    - `inkFeathers.total`
    - полный enlightenment breakdown
    - `previousSoulNames`
    - `pendingMemoryLegacy.applicationState`
    - `pendingMemoryLegacy.grantSnapshot`
- Evidence:
  - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs:36)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:717)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1157)
  - [ValidationService.AfterlifeArchiveTradeAndLifecycle.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.AfterlifeArchiveTradeAndLifecycle.cs:824)
- Required fix:
  - развернуть `/душа` до полного soul-state overview без резких сокращений
- Fix note:
  - `/душа` теперь показывает current и total ink feathers, полный breakdown `enlightenment`, прежние имена души и развёрнутый `pendingMemoryLegacy` с `applicationState` и `grantSnapshot`
  - обновлены:
    - [ExplorerMode.Afterlife.InkFeathersAndOfferings.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
  - покрыто тестом:
    - `TryProcessCommand_SoulInfo_ShowsExpandedCanonicalSoulState`

### 11. Companion relic detail hides most canonical payload

- Status: `Fixed`
- Severity: `High`
- Problem:
  - у companion/echo relic detail почти не видны:
    - `originWorldSummary`
    - `futureCompanionPrompt`
    - `bondReason`
    - `coreTraits`
    - `archetypeHints`
    - `appearanceMotifs`
- Evidence:
  - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs:1173)
  - [GuardianPolicyContracts.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianPolicyContracts.cs:1339)
  - [GuardianPolicyContracts.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianPolicyContracts.cs:1363)
- Required fix:
  - relic detail должен показывать полный canonical companion payload, а не только seed snapshot
- Fix note:
  - detail companion/echo relic теперь показывает `originWorldSummary`, `futureCompanionPrompt`, `bondReason`, `coreTraits`, `archetypeHints`, `appearanceMotifs` и source ids alongside existing snapshot lines
  - обновлены:
    - [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
  - покрыто тестом:
    - `TryProcessCommand_SoulRelics_CompanionDetailShowsFullCanonicalPayload`

### 12. Guardian trade detail hardcodes “4 local slots”

- Status: `Fixed`
- Severity: `High`
- Problem:
  - player-facing text захардкожен на `4 локальных слота`
  - canonical slot count уже вычисляется отдельно
  - игрок может видеть неверную торговую ёмкость
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1422)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:927)
  - [GuardianTradeService.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianTradeService.cs:695)
- Required fix:
  - выводить реальный derived slot count
- Fix note:
  - guardian detail panel больше не использует literal `4 локальных слота`; local trade text теперь читает `derivedState.TradeSlotCount`
  - обновлены:
    - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестом:
    - `ExplorerMode_GuardianUi_MustMarkActiveGuardianAndUseDerivedTradeSlotCount`

### 13. Guardian journals are truncated without full drill-down

- Status: `Fixed`
- Severity: `Medium`
- Problem:
  - detail panel показывает только часть мыслей и памяти общения
  - полного player-facing drill-down в canonical journal нет
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1342)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1350)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1846)
- Required fix:
  - добавить полный journal drill-down или развёрнутый “показать всё”
- Fix note:
  - guardian detail теперь показывает preview как осознанный preview, а не как тупик: блоки мыслей и памяти общения помечают `показано N из M`
  - в `Действие` добавлен explicit path `Показать весь журнал Хранителя`, который открывает полный player-facing drill-down по всем guardian thought/social entries
  - обновлены:
    - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестом:
    - `TryProcessCommand_Guardians_ShowFullGuardianJournalActionRendersAllEntries`

### 14. `/проекты_хранителей` and `/сила_обители` still leak shorthand/internal terminology

- Status: `Fixed`
- Severity: `Medium`
- Problem:
  - экраны до сих пор используют developer shorthand:
    - `projectType`
    - `projectMode`
    - `Hooks`
    - `Clarity`
    - `Hostile cap`
    - `Temp modifiers`
  - игрок получает плотные внутренние ярлыки вместо полностью расшифрованной информации
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1458)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1737)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:496)
- Required fix:
  - перевести эти breakdowns в full player-facing wording
- Fix note:
  - `/проекты_хранителей` больше не показывает raw `projectType/projectMode` и английские pressure/stability breakdowns: project detail и journal detail переведены в player-facing wording
  - `/сила_обители` больше не течёт через `Derived-эффекты`, `Clues`, `Clarity`, `Hostile cap`, `Temp modifiers` и соседний shorthand; derived breakdown остаётся полным, но теперь объясняется на русском и без внутренних токенов
  - это был purely player-facing UI/data pass без изменений механики
  - обновлены:
    - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_GuardianProjects_DetailUsesPlayerFacingWording`
    - `TryProcessCommand_AbodePower_DetailUsesPlayerFacingWording`
    - `ExplorerMode_GuardianProjectAndAbodePower_UiMustExposeFullGuardianJournalAndPlayerFacingVocabulary`

---

## Player-Facing Data Gaps: Shining Abode

### 15. Gates and prepared package hide the actual frozen blessing data

- Status: `Fixed`
- Severity: `High`
- Problem:
  - Gates и overview в основном показывают counts и краткие blurbs
  - игрок не видит полный frozen package:
    - `pickCap`
    - `draftSize`
    - source info
    - `effectPayload`
    - `selectedBlessingCardIds`
    - `preparedAtTurn`
    - `preparedAtUtc`
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs:107)
  - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs:264)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:823)
- Required fix:
  - добавить full package inspection panel без сокращения canonical blessing payload
- Fix note:
  - в `/shining_abode` появился прямой action `Осмотреть набор и пакет`, доступный и в ordinary active mode, и при уже подготовленном package handoff
  - Gates/package inspection теперь показывает `pickCap`, `draftSize`, `selectedBlessingCardIds`, `preparedAtTurn`, `preparedAtUtc`, source info и полный `effectPayload`
  - frozen selected-card payload дополнительно surfaced прямо в overview block `Следующая жизнь`, так что canonical package state не теряется даже вне отдельного inspect loop
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
    - [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs)
    - [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_ShiningAbode_GatesInspectionShowsFrozenPackagePayload`
    - `ExplorerMode_ShiningUi_MustExposePackagePoliticsAndStructureInspection`

### 16. Shining screens hide ordinary resident political state

- Status: `Fixed`
- Severity: `High`
- Problem:
  - player обычно видит только headcount и subset ready-to-realign
  - скрыты canonical resident fields:
    - `residentRole`
    - `factionLoyaltyLevel`
    - `factionLoyaltyTier`
    - `factionRestlessness`
    - `factionRealignmentState`
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:110)
  - [ExplorerMode.Afterlife.ShiningAbode.Politics.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Politics.cs:301)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:530)
- Required fix:
  - дать полноценный resident-level political inspection path
- Fix note:
  - `/shining_politics` теперь даёт explicit faction inspection path, который показывает обычных ascended residents, а не только subset `ready_to_realign`
  - player-facing panel раскрывает `residentRole`, `factionLoyaltyLevel`, `factionLoyaltyTier`, `factionRestlessness` и `factionRealignmentState` для каждого члена выбранной фракции
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестом:
    - `TryProcessCommand_ShiningPolitics_FactionInspectionShowsResidentPoliticalState`

### 17. Halls and radiant actors are collapsed to counts

- Status: `Fixed`
- Severity: `High`
- Problem:
  - overview почти не показывает hall- и radiant-actor-level data
  - скрыты:
    - hall names/descriptions/service tags
    - radiant actor names/summaries/status
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs:92)
  - [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs:94)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:621)
  - [ShiningAbodeState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.cs:739)
- Required fix:
  - добавить richer hall/radiant actor summaries
- Fix note:
  - main `/shining_abode` overview теперь показывает halls и radiant actors не только через counts, но и через readable summaries с именами, описаниями, service tags и political status
  - добавлен отдельный structure inspection path `Осмотреть залы и светозарных акторов`, где видны все materialized halls, их service tags/related factions и все radiant actors с summaries/status/faction links
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
    - [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_ShiningAbode_OverviewShowsHallAndRadiantActorSummaries`
    - `ExplorerMode_ShiningUi_MustExposePackagePoliticsAndStructureInspection`

### 18. Core-action receipts underreport canonical outcomes

- Status: `Fixed`
- Severity: `Medium`
- Problem:
  - receipt summaries слишком короткие
  - игрок не видит:
    - `reason`
    - `selectedCardIds`
    - `newResidentIds`
    - `seededProjectIds`
    - и другие action-specific payloads
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:351)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:373)
- Required fix:
  - receipts нужно сделать audit-friendly, не только summary-friendly
- Fix note:
  - `/shining_abode` получил отдельный inspect action `Осмотреть исходы Обители`, который раскрывает full core-action receipt detail: `requestId`, `resolvedAtTurn/UTC`, `reason`, `selectedCardIds`, `newResidentIds`, `seededProjectIds` и action-specific target context
  - compact overview summaries оставлены, но больше не являются единственным player-facing surface для canonical outcomes
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестом:
    - `TryProcessCommand_ShiningAbode_CoreReceiptInspectionShowsCanonicalOutcomePayload`

### 19. Trade UI hides most lifecycle/accounting details

- Status: `Fixed`
- Severity: `Medium`
- Problem:
  - витрина показывается как `готова / ожидает / не запрошена`
  - почти не видны:
    - derived contract snapshot
    - timestamps
    - receipt proof for current cycle
    - exact accounting detail витрины
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:94)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:148)
  - [ShiningTradeRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningTradeRequestState.cs:31)
- Required fix:
  - добавить lifecycle/accounting panel для Shining trade contract
- Fix note:
  - у Shining trade появился explicit inspect path `Осмотреть торговые циклы`, доступный из `/shining_abode` и из trade/forge surface
  - panel теперь показывает `tradeCycleId`, расчётный contract snapshot текущего цикла, pending request timestamps, materialized inventory snapshot, sold/remaining accounting и receipt proof текущего цикла
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
    - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестом:
    - `TryProcessCommand_ShiningTradeAndForge_TradeLifecycleInspectionShowsContractAndReceiptProof`

### 20. Political resolution summaries omit too much decision context

- Status: `Fixed`
- Severity: `Medium`
- Problem:
  - founding/realignment/leadership outcomes underreport:
    - charter details
    - hall metadata
    - `reason`
    - `residentHistoryEntryId`
    - leadership transition context
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:403)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:413)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:421)
- Required fix:
  - political receipts нужно расширить до полноценного player-facing audit summary
- Fix note:
  - `/shining_politics` получил отдельный inspect action `Осмотреть решения фракций`, который раскрывает founding / realignment / leadership receipts с `reason`, hall metadata, charter detail, `residentHistoryEntryId`, leadership transition context и matching history summary
  - compact political summaries сохранены, но больше не скрывают canonical decision context
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестом:
    - `TryProcessCommand_ShiningPolitics_ResolutionInspectionShowsDecisionContext`

### 21. Forge authoring is still contract-shaped instead of player-shaped

- Status: `Fixed`
- Severity: `Medium`
- Problem:
  - forge flow всё ещё вынуждает игрока мыслить raw fields:
    - action ids
    - `formTag`
    - `propertyIndex`
    - raw JSON for replacement/additions
  - при этом не хватает projected outcome preview
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:371)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:418)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:541)
- Required fix:
  - forge request flow надо перевести в richer preview-driven UI
- Fix note:
  - forge authoring теперь начинается с player-facing выбора действия кузни, а не с raw `actionType` identifiers
  - выбор формы, свойства и uplift additions переведён на guided prompts с manual JSON только как fallback для advanced path
  - перед записью request теперь показывается отдельный preview/confirmation panel с действием, реликвией, projected outcome и quoted cost
  - request schema и canonical payload не менялись: по-прежнему пишется тот же `pending_shining_abode_actions.json`
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs)
    - [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_ShiningTradeAndForge_ForgeAuthoringUsesPreviewDrivenFlow`
    - `ExplorerMode_ShiningUi_MustExposePackagePoliticsAndStructureInspection`

### 22. Shining terminology still leaks canonical tokens and mixed shorthand

- Status: `Fixed`
- Severity: `Medium`
- Problem:
  - часть labels и help text всё ещё используют mixed English/Russian shorthand и raw canonical tokens
  - это снижает понятность даже там, где данные уже есть
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:550)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:559)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:590)
  - [ExplorerMode.MetaStoryAndStatus.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs:186)
- Required fix:
  - пройтись по terminology pass и убрать raw tokens / uneven shorthand
- Fix note:
  - Shining help text, overview panels, trade lifecycle detail и politics screens переведены на единый player-facing русский словарь
  - из основных Shining surfaces убраны `Trade и forge`, `Stored ... contracts`, `Pending request`, `Receipt proof`, `Charter`, `Hall id`, `Actor id` и соседний mixed shorthand
  - updated wording не скрывает canonical data, а только переименовывает его в понятные игроку labels
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
    - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs)
    - [ExplorerMode.MetaStoryAndStatus.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_ShiningTradeAndForge_TradeLifecycleInspectionShowsContractAndReceiptProof`
    - `TryProcessCommand_ShiningPolitics_ResolutionInspectionShowsDecisionContext`
    - `TryProcessCommand_ShiningPolitics_FactionInspectionShowsResidentPoliticalState`
    - `ExplorerMode_ShiningUi_MustExposePackagePoliticsAndStructureInspection`

---

## Verification Follow-Ups

### 23. Founding request writer still allows duplicate hall ids if validation is bypassed

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining politics`, `request persistence`
- Problem:
  - пункт `8` был закрыт по validation-path, но persistence helper всё ещё дедуплицирует founding requests только по `ProposedFactionId`
  - это значит, что duplicate `ProposedHallId` всё ещё может быть записан в pending file, если request проходит не через обычный UI validation path
  - текущие тесты тоже покрывают только duplicate faction id, а не duplicate hall id branch
- Evidence:
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:260)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:507)
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:885)
  - [ShiningFactionRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningFactionRequestStateTests.cs:208)
- Required fix:
  - write-path для founding requests должен учитывать duplicate `ProposedHallId`, а не только `ProposedFactionId`
  - добавить отдельный test branch на duplicate hall id
- Fix note:
  - `WriteFoundingRequestAsync` больше не дедуплицирует founding requests только по `ProposedFactionId`: pending set теперь очищается по любому конфликту `ProposedFactionId` или `ProposedHallId`
  - generic writer helper ужесточён до conflict-based replacement, так что conflicting founding entries не переживают повторную запись даже если pending file уже был загрязнён дублями
  - добавлены отдельные regression tests на duplicate hall id и case-insensitive duplicate hall id
  - обновлены:
    - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs)
    - [ShiningFactionRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningFactionRequestStateTests.cs)
  - покрыто тестами:
    - `WriteFoundingRequestAsync_ReplacesRequestByFactionId`
    - `WriteFoundingRequestAsync_ReplacesRequestByHallId`
    - `WriteFoundingRequestAsync_ReplacesRequestByHallId_CaseInsensitive`

### 24. Guardian project and abode power screens still leak English/internal audit labels

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea UI`, `/проекты_хранителей`, `/сила_обители`
- Problem:
  - пункт `14` закрыт не полностью
  - в player-facing audit text всё ещё остаются mixed labels вроде:
    - `Power loss`
    - `Pressure relief`
    - `Stability relief`
    - `Safe pressure`
    - `Defense rating`
    - `Последний power event`
  - это ломает claim, что terminology pass уже целиком перевёл эти экраны на player-facing vocabulary
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:633)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:642)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:646)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1082)
- Required fix:
  - добить vocabulary pass в guardian project / abode power audit blocks
  - убрать оставшиеся English/internal phrases из основного player-facing текста
- Fix note:
  - остаточные mixed labels в audit-блоках `/проекты_хранителей` и `/сила_обители` заменены на полностью player-facing русский словарь
  - `Политический удар`, `Контр-операция` и `Фортификация` больше не показывают `Power loss`, `Pressure relief`, `Stability relief`, `Safe pressure`, `Defense rating`
  - строка истории силы Обители больше не использует `Последний power event` и теперь оформлена как player-facing summary последнего изменения силы
  - обновлены:
    - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_GuardianProjects_DetailUsesPlayerFacingWording`
    - `TryProcessCommand_AbodePower_DetailUsesPlayerFacingWording`
    - `ExplorerMode_GuardianProjectsAndAbodePower_MustNotLeakResidualEnglishAuditLabels`

### 25. Forge reshape flow still exposes raw `formTag` tokens in the main path

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining forge UI`
- Problem:
  - пункт `21` закрыт не полностью
  - основной reshape flow уже guided, но suggested form chooser и preview всё ещё показывают raw canonical `formTag`
  - это значит, что canonical snake_case tokens всё ещё торчат в happy path, а не только в advanced fallback
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:849)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:1026)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:1141)
- Required fix:
  - `DescribeForgeFormTag` должен стать player-facing formatter, а не passthrough raw token
  - reshape chooser и preview должны показывать человекочитаемые формы реликвии
- Fix note:
  - `DescribeForgeFormTag` больше не passthrough raw token: formatter теперь даёт player-facing формы для текущих canonical `formTag` и humanized fallback для будущих неизвестных значений
  - основной reshape-flow кузни больше не показывает `glass_path` / `solar_crown` в chooser и preview, хотя request payload по-прежнему пишет canonical `targetFormTag`
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_ShiningTradeAndForge_ForgeAuthoringUsesPreviewDrivenFlow`
    - `ExplorerMode_ShiningForge_ReshapeFlow_MustHumanizeFormTags`

### 26. Shining help and inspection panels still leak mixed English/Russian terminology

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining UI`, help text, inspection panels
- Problem:
  - пункт `22` закрыт не полностью
  - в help matrix и Shining inspection panels всё ещё встречаются mixed phrases вроде:
    - `New Game+ reset`
    - `Late-game`
    - `Resolved core-action receipts`
    - `Added properties`
    - `Resolved political receipts`
  - это противоречит claim, что основные Shining surfaces уже полностью выровнены на player-facing русский словарь
- Evidence:
  - [ExplorerMode.MetaStoryAndStatus.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs:193)
  - [ExplorerMode.MetaStoryAndStatus.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs:214)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:734)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:812)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:862)
- Required fix:
  - добить terminology pass по help text и Shining inspection headings
  - raw/mixed headings должны уйти из headline/player-facing lines и остаться только там, где без них теряется audit usefulness
- Fix note:
  - help matrix и панели полного осмотра Сияющей Обители больше не используют mixed phrases `New Game+ reset`, `Late-game`, `Resolved core-action receipts`, `Added properties`, `Resolved political receipts`
  - команды `/return_to_chaos_sea` и `/found_guardian_mantle` в `/помощь` теперь описаны через полностью русский player-facing словарь
  - заголовки пустых inspection panels и label блока добавленных свойств тоже переведены на единый русский audit-friendly словарь
  - обновлены:
    - [ExplorerMode.MetaStoryAndStatus.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs)
    - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_ShiningAbode_CoreReceiptInspectionShowsCanonicalOutcomePayload`
    - `ExplorerMode_ShiningHelpAndInspectionPanels_MustUsePlayerFacingRussianTerminology`

### 27. `return_to_chaos_sea` enlightenment preservation is covered mostly by source-guard tests

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea lifecycle`, `tests`
- Problem:
  - функционально пункт `2` выглядит закрытым
  - но текущая защита от регрессии опирается в основном на source-guard, а не на behavior-level test реального exit handler path
  - это оставляет окно для регрессии при future refactor через helper
- Evidence:
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1125)
  - [GameEngine.IncarnationAndAfterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.IncarnationAndAfterlife.cs:73)
  - [GameEngine.IncarnationAndAfterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.IncarnationAndAfterlife.cs:117)
  - [GameEngineSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/GameEngineSourceGuardTests.cs:317)
- Required fix:
  - добавить behavior-level regression test на ordinary exit path
  - тест должен подтверждать, что handler реально сохраняет enlightenment state, а не только что в исходнике больше нет старого reset block
- Fix note:
  - ordinary exit path теперь покрыт behavior-level lifecycle regression test через private non-UI core helper, чтобы тест не упирался в `Console.ReadKey(true)` из transition render
  - новый test подтверждает, что ordinary `/return_to_chaos_sea` меняет realm и seal-ит Shining state, но сохраняет весь `soul_state.enlightenment` object без изменений
  - updated files:
    - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs)
    - [GameEngineTurnLifecycleTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/GameEngineTurnLifecycleTests.cs)
  - covered by tests:
    - `TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_PreservesEnlightenmentState`

### 28. Exit-time purge of pending Shining requests lacks handler-level regression coverage

- Status: `Fixed`
- Severity: `Low`
- Scope: `Shining lifecycle`, `tests`
- Problem:
  - функционально пункт `4` выглядит закрытым
  - но текущие тесты покрывают в основном `EnsureHealthyAsync` helpers и source-guard assertions, а не сам ordinary exit/refresh flow
  - это делает backlog claim сильнее, чем текущая regression защита
- Evidence:
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1125)
  - [GameEngine.MainMenu.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1160)
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:705)
  - [GameEngine.SessionAndSnapshots.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:721)
  - [GameEngineSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/GameEngineSourceGuardTests.cs:330)
- Required fix:
  - добавить handler-level regression test на ordinary `/return_to_chaos_sea`, который подтверждает purge всех pending Shining request files и post-exit refresh behavior
- Fix note:
  - ordinary exit purge теперь покрыт behavior-level regression test через `TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync`, а не только `EnsureHealthyAsync` tests и source-guards
  - новый test подтверждает, что ordinary `/return_to_chaos_sea` удаляет pending files для:
    - `pending_shining_abode_actions.json`
    - `pending_shining_trade_inventory_requests.json`
    - `pending_shining_faction_foundings.json`
    - `pending_shining_faction_realignments.json`
    - `pending_shining_faction_leadership_transitions.json`
  - этот же test подтверждает post-exit refresh behavior: realm переключается в `Chaos Sea`, runtime state перестаёт считать игрока находящимся в `Shining Abode`, а `shining_abode_state` остаётся в sealed ordinary-exit состоянии
  - updated files:
    - [GameEngineTurnLifecycleTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/GameEngineTurnLifecycleTests.cs)
  - covered by tests:
    - `TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_ClearsPendingShiningRequestsAndRefreshesRuntimeState`

### 29. `/сила_обители` still leaks residual internal audit terminology

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea UI`, `abode power detail`
- Problem:
  - после широкой verification-wave `/сила_обители` всё ещё держал остаточные internal/mixed labels в player-facing detail:
    - `canonical history`
    - `value`
    - `applications`
    - `modifierId`
    - `Terminal state`
  - это оставляло item `24` фактически не до конца закрытым, хотя крупный terminology pass уже был проведён
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:539)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:662)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:664)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1633)
- Required fix:
  - добить residual terminology pass в `ShowAbodePowerDetailPanel(...)` и связанных detail lines
  - заменить эти labels на единый русский player-facing словарь без изменения механики, state и audit payload
- Fix note:
  - `/сила_обители` теперь использует полностью русский player-facing словарь и для последних residual audit labels:
    - `Последние изменения силы Обители`
    - `сила эффекта`
    - `осталось срабатываний`
    - `Идентификатор модификатора`
    - `Конечное состояние`
  - структура экрана и все canonical числовые данные сохранены; изменились только labels и wording
  - updated files:
    - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
  - covered by tests:
    - `ExplorerMode_GuardianProjectsAndAbodePower_MustNotLeakResidualEnglishAuditLabels`
    - `TryProcessCommand_AbodePower_DetailUsesPlayerFacingWording`

### 30. Full Shining receipt inspection still truncates older resolved outcomes

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining Abode UI`, `receipt inspection`
- Problem:
  - экран `Полный осмотр исходов Обители` позиционируется как полный audit-view resolved core-action receipts
  - по факту список всё ещё режется до последних восьми записей через `.Take(8)`
  - это скрывает более старые canonical outcomes и делает inspection panel не полностью честной по отношению к названию и backlog claims по пункту `18`
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:718)
  - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs:723)
- Required fix:
  - убрать искусственное ограничение `.Take(8)` из full receipt inspection panel
  - если нужен preview-limit для overview, оставить его только в краткой summary-подаче, но не в полном inspection path
- Fix note:
  - `ShowShiningCoreReceiptInspectionPanel(...)` больше не truncates список resolved core-action receipts до последних восьми записей; полный inspection path теперь показывает весь canonical receipt list в текущем sorted order
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - covered by tests:
    - `TryProcessCommand_ShiningAbode_CoreReceiptInspectionDoesNotTruncateOlderResolvedOutcomes`
    - `ExplorerMode_ShiningUi_MustExposePackagePoliticsAndStructureInspection`

### 31. Forge reshape fallback still exposes raw canonical `formTag`

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining forge UI`, `reshape flow`
- Problem:
  - основной happy path reshape-flow уже humanize-ит формы реликвии
  - но fallback-ветка, когда альтернативных форм нет, всё ещё передаёт raw `currentFormTag` как default в `Ask(...)`
  - из-за этого игрок в edge-case path снова видит canonical token вместо player-facing формы
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:830)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:840)
- Required fix:
  - fallback reshape prompt тоже должен использовать player-facing/humanized form label
  - canonical `formTag` может оставаться только во внутреннем payload, но не в основном prompt default для игрока
- Fix note:
  - fallback reshape prompt больше не использует raw `currentFormTag` как default: теперь он показывает player-facing форму через `DescribeForgeFormTag(...)`
  - player-facing ввод формы в reshape prompts теперь локально нормализуется обратно в canonical `formTag`, так что UI не светит raw token, а request payload остаётся canonical
  - updated files:
    - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
  - covered by tests:
    - `ExplorerMode_ShiningForge_ReshapeFlow_MustHumanizeFormTags`
    - `TryProcessCommand_ShiningTradeAndForge_ReshapeFallbackHumanizesPromptAndNormalizesCanonicalFormTag`

### 32. `/сила_обители` still leaks raw modifier tokens and mixed rival wording

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea UI`, `abode power detail`
- Problem:
  - после закрытия пункта `29` экран `/сила_обители` всё ещё держит ещё два residual player-facing leaks:
    - raw `modifier.ModifierType` в списке временных модификаторов
    - mixed wording `rival-Хранителя` в labels причин силы Обители
  - это уже не старые headline leaks, а остаточные внутренние токены и смешанный словарь внутри detail blocks
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:604)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:662)
- Required fix:
  - humanize/локализовать `modifier.ModifierType` для player-facing detail path
  - заменить mixed wording `rival-Хранителя` на единый русский player-facing label
  - не менять derived semantics и audit payload, только wording/surfacing
- Fix note:
  - `/сила_обители` больше не показывает raw `modifier.ModifierType` в списке временных модификаторов: теперь там используется player-facing label `стартовое давление следующего внутреннего проекта` и мягкий humanized fallback для неизвестных значений
  - причина `rival_strike` больше не выводится как mixed `rival-Хранителя` и теперь surfaced как `Удар Хранителя-соперника`
  - updated files:
    - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
  - covered by tests:
    - `ExplorerMode_GuardianProjectsAndAbodePower_MustNotLeakResidualEnglishAuditLabels`
    - `TryProcessCommand_AbodePower_DetailUsesPlayerFacingWording`

### 33. Guardian trade detail still lacks behavior-level proof for derived slot count

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea UI`, `guardian trade detail`, `test coverage`
- Problem:
  - код уже использует `derivedState.TradeSlotCount` вместо захардкоженного `4 локальных слота`
  - но текущее покрытие этого фикса держится только на source-guard'е
  - нет behavior-level test, который действительно рендерит detail-view и доказывает, что игрок видит derived slot count, а не старый literal
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1558)
  - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs:94)
- Required fix:
  - добавить command-level regression test, который открывает guardian detail/trade inspection и asserts rendered derived slot count
  - source-guard оставить, но перестать полагаться только на него для этого закрытия
- Fix note:
  - `/хранители` теперь покрыт command-level regression test, который реально открывает detail-view активного Хранителя в текущей обители и подтверждает player-facing строку `Доступна: 5 локальных слотов...`
  - этот test доказывает behavior-level rendering derived `TradeSlotCount`, а не только отсутствие старого literal через source inspection
  - updated files:
    - [ExplorerModeCommandTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [Chaos_Sea_And_Shining_Audit_Backlog.md](/E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Chaos_Sea_And_Shining_Audit_Backlog.md)
  - covered by tests:
    - `TryProcessCommand_Guardians_TradeDetailRendersDerivedSlotCount`

### 34. Full guardian journal drill-down still lacks behavior-level rendering proof

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea UI`, `guardian journal`, `test coverage`
- Problem:
  - полный journal drill-down уже существует и preview честно пишет `показано N из M`
  - но текущий command test подтверждает только наличие action и факт открытия панели
  - он не доказывает, что более поздние journal entries действительно дорисовываются в полном журнале, а не теряются после preview truncation
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1473)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1933)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:2035)
  - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs:1899)
  - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs:106)
- Required fix:
  - усилить command-level regression test так, чтобы он проверял рендер конкретных поздних thought/social/history entries в full journal panel
  - source-guard и текущий action-exists test оставить как структурные guards, но не как единственное доказательство закрытия
- Fix note:
  - `TryProcessCommand_Guardians_ShowFullGuardianJournalActionRendersAllEntries` теперь проверяет не только наличие action и повторный action-loop, но и реальный рендер полного журнала
  - command-level proof теперь подтверждает preview counters `показано 3 из 4` / `показано 5 из 6`, full journal headings и поздние записи `Мысль 4` и `Разговор 6`, которые лежат за пределами preview truncation
  - source-guard оставлен как structural guard, но закрытие пункта больше не опирается только на него
  - обновлены:
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [Chaos_Sea_And_Shining_Audit_Backlog.md](/E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Chaos_Sea_And_Shining_Audit_Backlog.md)
  - покрыто тестами:
    - `TryProcessCommand_Guardians_ShowFullGuardianJournalActionRendersAllEntries`

### 35. Forge authoring is still partially contract-shaped in retune and uplift branches

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining forge UI`, `authoring flow`
- Problem:
  - основной forge flow уже preview-driven, но часть веток всё ещё сваливается в raw contract-shaped authoring
  - при отсутствии готовых suggestions `retune_property` сразу уходит в `JSON нового свойства`
  - `uplift_rarity` всё ещё уводит в `JSON дополнительных свойств`
  - это оставляет item `21` фактически частично закрытым: happy path стал player-shaped, но advanced/common fallback paths по-прежнему мыслят контрактом, а не projected outcome
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:919)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:959)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:1033)
- Required fix:
  - довести `retune_property` и `uplift_rarity` до того же preview-driven стандарта, что уже есть у reshape-flow
  - raw JSON/manual editing оставить только как явный advanced fallback, а не как основной path при отсутствии удобных suggestions
- Fix note:
  - `retune_property` при отсутствии suggestions теперь сначала предлагает player-facing базовый шаблон нового свойства и только потом открывает manual JSON как explicit advanced path
  - `uplift_rarity` теперь строит подготовленный набор дополнительных свойств и предлагает его как основной fallback вместо немедленного `JSON дополнительных свойств`
  - confirmation preview и canonical request payload не менялись: `replacementProperty` / `addedProperties` по-прежнему пишутся в тот же contract
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_ShiningTradeAndForge_RetuneFallbackOffersTemplateBeforeManualJson`
    - `TryProcessCommand_ShiningTradeAndForge_UpliftFallbackOffersPreparedSetBeforeManualJson`
    - `ExplorerMode_ShiningForge_RetuneAndUpliftFallbacks_MustOfferPreviewChoicesBeforeManualJson`

### 36. Forge UI still leaks mixed English and canonical terminology

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Shining forge UI`, `player-facing wording`
- Problem:
  - targeted terminology pass по help/inspection screens уже закрыт
  - но внутри forge UI всё ещё текут mixed English/canonical strings:
    - `blessing rerolls`
    - `JSON object`
    - `JSON array`
    - `Soul Relics`
    - `canonical properties array`
  - это означает, что item `22` закрыт только для overview/help surfaces, но не для forge authoring itself
- Evidence:
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:881)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:924)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:1036)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:1051)
  - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs:1083)
- Required fix:
  - провести отдельный forge-local terminology pass
  - перевести эти prompts/errors/help lines на единый русский player-facing словарь без изменения request schema
- Fix note:
  - forge-local wording выровнен на единый русский словарь: сообщения про перебросы благословением, ручное JSON-редактирование свойств, отсутствие реликвий души и отсутствие списка свойств у реликвии больше не светят mixed English/canonical terms
  - manual JSON path и forge payload не менялись; изменены только player-facing prompts/errors
  - обновлены:
    - [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_ShiningTradeAndForge_RetuneFallbackOffersTemplateBeforeManualJson`
    - `TryProcessCommand_ShiningTradeAndForge_UpliftFallbackOffersPreparedSetBeforeManualJson`
    - `TryProcessCommand_ShiningTradeAndForge_NoSoulRelicsUsesPlayerFacingRussianWording`
    - `TryProcessCommand_ShiningTradeAndForge_MissingRelicPropertiesUsesPlayerFacingRussianWording`
    - `ExplorerMode_ShiningForge_MustUsePlayerFacingRussianTerminology`

### 37. Completed guardian project surfaces still leak raw project state labels

- Status: `Fixed`
- Severity: `Medium`
- Scope: `Chaos Sea UI`, `guardian project detail`, `terminology`
- Problem:
  - основной vocabulary pass уже ввёл `FormatGuardianProjectStateLabel(...)`
  - но completed project surfaces всё ещё местами выводят raw `finalState` как есть
  - это касается и summary списка завершённых проектов, и detail panel completed project
  - в результате canonical state values вроде `Completed` или другие terminal tokens всё ещё могут светить игроку, хотя item `24` декларируется как закрытый terminology cleanup
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:717)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1155)
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs:1640)
  - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs:251)
- Required fix:
  - completed project summary/detail должны использовать тот же player-facing state formatter, что и active/in-progress project surfaces
  - терминология должна быть выровнена без изменения project state semantics
- Fix note:
  - completed project summary в `/хранители` и completed project detail в `/проекты_хранителей` больше не выводят raw `finalState`; теперь обе поверхности используют `FormatGuardianProjectStateLabel(...)`
  - обновлены:
    - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
    - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
  - покрыто тестами:
    - `TryProcessCommand_GuardianProjects_CompletedProjectDetailUsesPlayerFacingStateLabel`
    - `ExplorerMode_CompletedGuardianProjects_MustUsePlayerFacingStateLabels`

### 38. Founding validation still lacks explicit duplicate-hall collision proof

- Status: `Fixed`
- Severity: `Low`
- Scope: `Shining politics validation`, `test coverage`
- Problem:
  - validation path уже reject-ит duplicate `ProposedFactionId` и `ProposedHallId`
  - но после fix-wave оставался только косвенный proof для hall collision:
    - hall dedupe был подтверждён write-path tests
    - explicit validation test для `same ProposedHallId / different ProposedFactionId` отсутствовал
  - из-за этого пункт `8` был закрыт в коде, но не до конца доказан на уровне test coverage
- Evidence:
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs)
  - [ShiningFactionRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningFactionRequestStateTests.cs)
- Required fix:
  - добавить отдельный validation regression test на duplicate pending `ProposedHallId`
  - production behavior не менять, если validation уже корректна
- Fix note:
  - добавлен отдельный validation regression test на collision `same ProposedHallId / different ProposedFactionId`, который напрямую вызывает `ValidateFoundingRequestAgainstCurrentStateAsync(...)` и подтверждает reject по duplicate hall id
  - production код не менялся; pass закрыл только недостающий proof для validation branch
  - updated files:
    - [ShiningFactionRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningFactionRequestStateTests.cs)
  - covered by tests:
    - `ValidateFoundingRequestAgainstCurrentStateAsync_DuplicatePendingHallId_Fails`

### 39. Active guardian detail header still lacks behavior-level proof

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea UI`, `guardian detail`, `test coverage`
- Problem:
  - `/хранители` уже маркирует активного Хранителя в списке, баннере и detail-view
  - но behavior-level coverage подтверждала только list/banner marker
  - explicit render proof для detail header отсутствовал, поэтому пункт `9` оставался under-tested
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
  - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
- Required fix:
  - добавить command-level render proof для detail header активного Хранителя
  - source-guard оставить как structural check, но не как единственное доказательство
- Fix note:
  - добавлен command-level test, который открывает `/хранители`, заходит в detail-view активного Хранителя и подтверждает player-facing header `Азалия · активный` вместе со строкой `Текущий активный Хранитель`
  - заодно test extractor расширен, чтобы честно читать panel headers из rendered output
  - updated files:
    - [ExplorerModeCommandTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
  - covered by tests:
    - `TryProcessCommand_Guardians_DetailHeaderMarksActiveGuardian`

### 40. `/помощь` wording still lacks render-level proof

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea UI`, `Shining help`, `test coverage`
- Problem:
  - help text уже переведён на player-facing русский словарь
  - но cleanup `/помощь` долгое время держался в основном на source-guard coverage
  - не было render-level proof для Chaos Sea и Shining Abode help surfaces, поэтому пункт `26` оставался under-tested
- Evidence:
  - [ExplorerMode.MetaStoryAndStatus.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs)
  - [ExplorerModeSourceGuardTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs)
- Required fix:
  - добавить render-level command tests для `/помощь` в Chaos Sea и Shining Abode
  - production text не менять, если текущий render уже корректен
- Fix note:
  - добавлены command-level tests для `/помощь` в Chaos Sea и Shining Abode: help panel теперь подтверждён реальным rendered output, а не только source-guard’ом
  - test extractor расширен чтением `Panel(Table)` surfaces, чтобы help-table можно было проверять напрямую
  - updated files:
    - [ExplorerModeCommandTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.cs)
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
  - covered by tests:
    - `TryProcessCommand_Help_ChaosSeaUsesPlayerFacingRussianWording`
    - `TryProcessCommand_Help_ShiningAbodeUsesPlayerFacingRussianWording`

### 41. Completed guardian project summary row still lacks behavior-level proof

- Status: `Fixed`
- Severity: `Low`
- Scope: `Chaos Sea UI`, `guardian projects`, `test coverage`
- Problem:
  - completed project detail уже был покрыт behavior-level test
  - summary row завершённых проектов в `/хранители` использовал player-facing formatter в коде, но не был доказан отдельным render-level assert
  - из-за этого пункт `37` оставался under-tested именно по summary surface
- Evidence:
  - [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
  - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
- Required fix:
  - добавить command-level test, который читает completed-project summary row прямо из `/хранители`
  - detail-panel proof оставить, но перестать полагаться только на него
- Fix note:
  - добавлен command-level test, который открывает `/хранители`, рендерит completed project summary и подтверждает player-facing label `Завершён` без raw `Completed`
  - production код не менялся; pass закрыл только недостающий behavior proof для summary surface
  - updated files:
    - [ExplorerModeCommandTests.Afterlife.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs)
  - covered by tests:
    - `TryProcessCommand_Guardians_CompletedProjectSummaryUsesPlayerFacingStateLabel`

### 42. Founding validation still lacks direct proof for materialized hall collision

- Status: `Fixed`
- Severity: `Low`
- Scope: `Shining politics validation`, `test coverage`
- Problem:
  - после закрытия пункта `38` validation branch уже имела proof для duplicate pending `ProposedHallId`
  - но оставалась ещё одна отдельная ветка в `ValidateFoundingRequestAgainstCurrentStateAsync(...)`:
    - reject founding request, если `ProposedHallId` уже materialized в `shining_abode_state.json`
  - на эту canonical-state collision branch не было прямого regression test, поэтому verification wave показала residual test-hardening gap
- Evidence:
  - [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs:265)
  - [ShiningFactionRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningFactionRequestStateTests.cs)
- Required fix:
  - добавить отдельный validation regression test на collision между новым founding request и уже существующим materialized hall id в canonical Shining state
  - production behavior не менять, если validation already rejects this branch
- Fix note:
  - добавлен direct regression test на `ProposedHallId = hall_new`, уже materialized в seeded `shining_abode_state.json`
  - test подтверждает, что `ValidateFoundingRequestAgainstCurrentStateAsync(...)` reject-ит request именно по materialized hall collision branch, а не только по pending founding collision
  - production код не менялся; pass закрыл только недостающий proof для canonical-state validation branch
  - updated files:
    - [ShiningFactionRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningFactionRequestStateTests.cs)
    - [Chaos_Sea_And_Shining_Audit_Backlog.md](/E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Chaos_Sea_And_Shining_Audit_Backlog.md)
  - covered by tests:
    - `ValidateFoundingRequestAgainstCurrentStateAsync_MaterializedHallId_Fails`

---

## Working Rule For Future Passes

Если обнаружена **новая** проблема, сначала:

1. добавить её новым отдельным пунктом ниже текущего списка
2. дать ей новый номер, начиная с `43`
3. только после этого проводить fix-pass и обновлять её статус

После каждого fix-pass:

1. перевести соответствующий пункт в `Fixed`
2. добавить короткую заметку:
   - что было изменено
   - в каких файлах
   - какой тест это покрыл
3. если fix породил новый follow-up issue, добавить его отдельным новым пунктом, а не переписывать старый silently
