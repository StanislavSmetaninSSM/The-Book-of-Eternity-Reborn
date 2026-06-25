# Сияющая Обитель: rebased implementation plan

## Статус

Этот документ обновляет старый `Shining_Abode_Implementation_Plan.md` под текущую codebase.

Правило при чтении:

- если старый implementation plan и этот файл конфликтуют, **этот файл главнее**;
- таблицы, числовые формулы, rarity/service/project rules и общий gameplay-intent по-прежнему брать из:
  - `docs/audits/afterlife/shining-abode/Shining_Abode_Endgame_Design_Plan.md`
  - `docs/audits/afterlife/shining-abode/Shining_Abode_Implementation_Plan.md`
- этот rebased plan отвечает не за lore/formula detail, а за **правильную интеграцию в нынешние runtime, lifecycle, validation и resident contracts**.

---

## 1. Что уже существует и должно быть сохранено

Текущая codebase уже закрепила несколько Shining Abode инвариантов. Новый implementation pass не должен их ломать.

### 1.1. Realm bucket semantics

Source of truth:

- `game_state/meta/soul_state.json.currentRealm`
- `game_state/meta/shining_abode_state.json.preparedIncarnationPackage`

Уже существующий контракт:

- `currentRealm = Shining Abode` и `preparedIncarnationPackage = null` означает **ordinary active Shining Abode**
- `currentRealm = Shining Abode` и `preparedIncarnationPackage != null` означает **pending-bootstrap handoff**
- pending-bootstrap handoff имеет **более высокий приоритет**, чем обычный active-Shining mode
- ordinary Shining Abode actions в handoff mode недопустимы

Этот контракт уже отражён в:

- `BookOfEternityClient/Models/GameState/AggregatedGameState.cs`
- `BookOfEternityClient/Core/StateManager.cs`
- `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- `BookOfEternityClient/Launcher/CLI_Launch_Script.md`

### 1.2. Local client-owned realm commands

Уже существуют и должны остаться **client-owned local commands**, а не GM-authored accepted turns:

- `/reenter_shining_abode`
- `/return_to_chaos_sea`

Их текущее поведение:

- `reenter_shining_abode`:
  - доступен только из `Chaos Sea`
  - требует stored `availability = active`
  - блокируется `preparedIncarnationPackage != null`
  - блокируется active/malformed/wrong-semantic `afterlife_return_guard.json`
- `return_to_chaos_sea`:
  - доступен только из ordinary active `Shining Abode`
  - недоступен в pending-bootstrap handoff
  - seal'ит stored Shining state и переводит soul realm обратно в `Chaos Sea`

Новый implementation pass не должен превращать эти две операции в обычные GM gameplay actions.

### 1.3. Guardian-policy baseline

После hardening текущая codebase живёт по strict guardian-policy rules:

- policy-sensitive afterlife state нельзя silently reconstruct-ить из permissive fallback surfaces;
- lifecycle/realm mutations должны уважать pre-turn/current authority split;
- malformed current owner-state должен fail-close-иться, а не “чиниться по пути”;
- новые control/state files нельзя вводить как ad hoc side channel без validation/normalizer parity.

Поэтому Shining Abode нельзя реализовывать как isolated mini-mode с raw local writes. Он должен встроиться в ту же модель strict current-state validation и accepted-turn canonicalization, что и остальная afterlife plumbing.

---

## 2. Canonical ownership model

### 2.1. `shining_abode_state.json`

`game_state/meta/shining_abode_state.json` остаётся canonical owner для:

- `availability`
- `radiance`
- `lightSparks`
- `halls`
- `factions`
- `pendingNativeFactionDiscovery`
- `gates`
- `preparedIncarnationPackage`

Никакой другой файл не должен дублировать эти структуры как independent source of truth.

### 2.2. `soul_state.json`

`game_state/meta/soul_state.json` по-прежнему владеет:

- `currentRealm`
- canonical soul lifecycle progression

Shining Abode state **не дублирует realm** и не должен вводить свой параллельный “active realm” field.

### 2.3. `guardians.json`

`game_state/meta/guardians.json` остаётся authoritative owner для:

- guardian identity
- guardian mood/domain/meta state
- ordinary guardian-abode data

Shining factions должны ссылаться на guardian через `guardianId`, но не копировать guardian object целиком.

### 2.4. `guardian_abode_residents.json`

`game_state/meta/guardian_abode_residents.json` остаётся authoritative owner для resident identity и всей уже реализованной resident model:

- `personalityProfile`
- `abodeDisposition`
- `abodeDevotionLevel`
- `abodeDevotionTier`
- `restlessness`
- `migrationState`
- journals / receipts / transfer flow / companion seed contracts

Для Shining Abode resident layer добавляются **только additive fields**, а не новая параллельная resident system:

- `ascensionState = ascended | remained_in_chaos_sea`
- `shiningFactionId`
- `residentRole = archive_support | forge_support | social_support | resource_support | descent_support`

Жёсткие правила:

- `resident.shiningFactionId` — единственный canonical faction-membership field для Shining layer
- `faction.residents[]` не вводить
- `ascensionState` / `shiningFactionId` / `residentRole` должны coexist with current personality/devotion/transfer model, а не заменять её
- ordinary resident transfer/competition system between Abodes остаётся отдельной системой; Shining faction membership не является shortcut в неё

---

## 3. Обновлённый implementation order

Старый implementation plan был написан до текущего guardian-policy convergence и resident-system expansion. Новый порядок реализации должен быть таким.

### Phase A. Foundation and authority layer

Сначала закрыть базовый state/validation/runtime contract:

- материализовать canonical `shining_abode_state.json` shape из старой спеки
- добавить strict validation для file shape и top-level invariants
- добавить read-model loading в `StateManager` и explorer/UI surfaces без ad hoc parsing
- включить `shining_abode_state.json` в policy-sensitive lifecycle/accepted-turn validation на Shining-dependent paths
- зафиксировать, что pending-bootstrap handoff mode блокирует все ordinary Shining operations

Важно:

- foundation phase не должна добавлять реальные gameplay actions раньше, чем file contract начинает нормально валидироваться и отображаться в runtime
- malformed current `shining_abode_state.json` должен fail-close-ить Shining-dependent paths, а не silently regenerate-иться

### Phase B. Factions, halls and resident integration

После foundation:

- реализовать canonical `hall` и `faction` shapes
- materialize-ить ascended-guardian factions как references to existing guardians
- расширить resident contract additive Shining fields
- реализовать resident-derived faction membership and strength contribution
- реализовать derived `factionStrength`, band, trade tier and service multiplier

Обязательная интеграция с уже существующими residents:

- residentCount для Shining factions считает только residents with `ascensionState = ascended` and matching `shiningFactionId`
- resident personality/devotion fields сохраняются и не сбрасываются
- changes to `shiningFactionId`, `residentRole`, `ascensionState`, `grantedRelicId` должны invalidat-ить opened gates exactly as build-affecting mutations

### Phase C. Discovery, investment and project engine

После появления faction layer:

- `discover_native_faction`
- `invest_in_faction`
- `complete_project`
- `retire_project`
- `support_project`
- `unsupport_project`

Делать как ordinary Shining Abode gameplay actions внутри accepted-turn pipeline, а не как client-local raw file rewrites.

Rules:

- все эти действия требуют:
  - `currentRealm = Shining Abode`
  - `availability = active`
  - `preparedIncarnationPackage = null`
- никакое из них не должно быть допустимо из Chaos Sea, Mortal World или pending-bootstrap handoff
- strength, rarity ceilings and supportable outputs пересчитываются только из canonical state, не из prose

### Phase D. Gates and frozen incarnation package

Только после того, как есть factions/projects/residents:

- `open_gates`
- draft generation
- select / deselect / reroll
- `enter_mortal_life_from_shining_abode`

Жёсткие требования:

- gates используют frozen candidate snapshot, а не recompute-on-every-click
- build-affecting mutation делает open draft stale
- successful `enter_mortal_life_from_shining_abode`:
  - пишет frozen `preparedIncarnationPackage`
  - не переводит realm сразу в Mortal World
  - оставляет `currentRealm = Shining Abode` до successful mortal bootstrap
  - переводит runtime в pending-bootstrap handoff mode

### Phase E. Trade, forge and service layer

После faction/project/gates core:

- forge actions
- trade profile / shop availability
- service multiplier consumers
- descent / patron / project card families

Это уже ordinary active-Shining systems и не должны быть доступны:

- в sealed state
- в pending-bootstrap handoff
- вне `currentRealm = Shining Abode`

### Phase F. Lifecycle completion and return flow parity

Последним слоем:

- strict parity между:
  - accepted life evaluation
  - post-life return to Chaos Sea
  - explicit `reenter_shining_abode`
  - explicit `return_to_chaos_sea`
  - re-ascension
- никакой новый auto-return path не вводить
- `afterlife_return_guard` semantics не ослаблять
- re-ascension reuses stored Shining state and recomputes ascension-local counters as the old spec describes

---

## 4. Validation and normalizer requirements

### 4.1. New validation slice

Shining Abode должен получить отдельный validation slice, а не набор scattered ad hoc checks.

Он обязан проверять:

- top-level `shining_abode_state.json` contract
- hall/faction/project/gates/package shapes
- additive resident Shining fields inside `guardian_abode_residents.json`
- cross-file references:
  - `faction.headActorId -> guardians.json`
  - `resident.shiningFactionId -> shining_abode_state.factions[]`
  - `preparedIncarnationPackage` internal consistency

### 4.2. Current/pre-turn parity

На Shining-dependent accepted-turn validation paths:

- если turn реально меняет Shining state, current readable `shining_abode_state.json` must participate in validation
- если lifecycle/repair path relies on pre-turn evidence, validated pending snapshot must include `shining_abode_state.json` whenever that file is authority-relevant for the turn
- нельзя делать permissive fallback на “ну в UI же видно, что Обитель была active”

### 4.3. No duplicate sources of truth

Validation должна explicitly reject attempts to model the same thing in two places, например:

- canonical `faction.residents[]`
- duplicated guardian snapshots inside faction objects
- duplicated realm ownership inside `shining_abode_state.json`
- reconstructed package from mutable gates state after package was already frozen

### 4.4. Mutation discipline

Новые Shining actions должны жить в ordinary accepted-turn mutation flow.

Допустимые local client mutations — только уже существующие lifecycle commands:

- `/reenter_shining_abode`
- `/return_to_chaos_sea`

Все остальные state changes:

- materialize-ятся в accepted turn
- валидируются как afterlife state changes
- не bypass-ят guardian-policy hardening

---

## 5. Что именно надо обновить в старом implementation plan

При практическом использовании старого `Shining_Abode_Implementation_Plan.md` считай устаревшими или неполными следующие места:

- любой section, который описывает Shining implementation без учёта `preparedIncarnationPackage` как higher-priority mode flag
- любой section, который не учитывает локальные команды `reenter_shining_abode` и `return_to_chaos_sea` как уже существующие client-owned paths
- любой section, где resident model подразумевается как узкий старый contract без:
  - `personalityProfile`
  - `abodeDisposition`
  - `abodeDevotionLevel`
  - `restlessness`
  - `migrationState`
  - transfer/competition system
- любой section, который implicitly позволяет raw or permissive local writes в policy-sensitive afterlife files
- любой section, который не требует validator/normalizer parity для нового file contract

Старый документ по-прежнему годится как source of:

- formulas
- costs
- reward tables
- project archetype/effect family mappings
- gates/card-generation detail

Но implementation architecture нужно брать уже из этого rebased plan.

---

## 6. Test matrix for the updated plan

Минимальная acceptance matrix для новой реализации:

### 6.1. Realm and lifecycle

- active Shining mode и pending-bootstrap handoff correctly distinguish-ятся в runtime/UI
- `reenter_shining_abode` работает только при current allowed guard matrix
- `return_to_chaos_sea` недоступен в pending-bootstrap handoff и sealed state
- ordinary life evaluation return ends in `Chaos Sea`, not auto-return to Shining Abode

### 6.2. State integrity

- malformed current `shining_abode_state.json` fail-close-ит Shining-dependent paths
- `preparedIncarnationPackage` cannot be reconstructed from mutable gates state
- `availability`, `radiance`, `lightSparks`, gates and faction invariants validate strictly

### 6.3. Resident integration

- adding `ascensionState` / `shiningFactionId` / `residentRole` does not regress current resident personality/devotion/transfer contracts
- resident membership in Shining faction is derived only from `resident.shiningFactionId`
- resident changes that affect gates correctly mark draft stale

### 6.4. Faction/project/gates mechanics

- faction strength derives only from canonical formula
- project actions are blocked outside active Shining mode
- gates draft and reroll use frozen snapshot semantics
- successful enter writes frozen package and switches runtime into pending-bootstrap handoff

### 6.5. Validation/authority

- accepted-turn validation and runtime behavior agree on Shining-dependent current/pre-turn requirements
- lifecycle-sensitive turns that depend on `shining_abode_state.json` require validated snapshot coverage when needed
- no cross-file duplicate-source drift slips through validation

---

## 7. Explicit defaults

Чтобы implementer не принимал заново уже решённые решения:

- canonical gameplay formulas remain those from the old Shining docs
- current local commands for re-entry/return stay local and are **not** converted into GM-authored accepted turns
- all ordinary Shining gameplay mutations are GM/accepted-turn driven
- pending-bootstrap handoff remains a higher-priority mode than ordinary active Shining Abode
- resident Shining membership is additive to the already-implemented resident personality/devotion system
- no new duplicate guardian or resident source-of-truth files are introduced
- `Shining Abode` implementation must be validator-first and authority-safe, not UI-first
