# Chaos Sea And Shining Fresh Audit Next Session Handoff

## Статус

`FRESH AUDIT WAVE COMPLETE`

Текущее состояние после latest implementation pass по [Chaos_Sea_And_Shining_Fresh_Audit_Backlog.md](/E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Chaos_Sea_And_Shining_Fresh_Audit_Backlog.md):

- items `79–91` — `Fixed`
- items `92–99` — `Fixed`
- items `100–108` — `Fixed`
- items `109–121` — `Fixed`
- items `122–133` — `Fixed`
- items `134–142` — `Fixed`
- items `143–149` — `Fixed`
- открытых пунктов осталось: `0`
- следующий приоритетный пункт: `—`
- следующий новый независимый пункт: `150`
- последний focused slice: `648/648`
- последний полный прогон: `1590/1590`

Latest implementation update:

- reopened пакет `134–142` и новый independent packet `143–149` теперь закрыты текущим implementation pass
- current baseline обновлена до focused `648/648` и full `1590/1590`

Последняя подтверждённая зелёная baseline остаётся прежней:

- startup hygiene теперь refresh-ит persisted realm before destructive cleanup, manifestation housekeeping больше не resurrect-ит stale requests, а retry-marker ставится только после successful build
- Shining trade/runtime tightened: local buy больше не пишет stale owner snapshot, Russian realm alias accepted by validation, numeric `inkFeathers` canonicalized, pending cleanup требует exact receipt identity
- UI/history/readability добиты: trade same-cycle fallback marked as historical, Shining inspection screens pushed raw ids/payloads into secondary diagnostics, archive and guardian screens получили exact drill-down, archive-operation prompts переведены на русский
- latest implementation pass закрыл `122–133`
- latest implementation pass снова закрыл `134–149`
- новые focused verification results: `648/648`
- baseline теперь обновлена до полного suite `1590/1590`

## Закрытый пакет items 134–149

Current implementation pass снова закрыл весь reopened/new packet `134–149`.

- `134–137` — runtime/state-contract hardening
  - unresolved `currentRealm` больше не запускает destructive wrong-realm cleanup
  - whitespace Shining trade pending file подтверждён как malformed fail-close contract
  - blessing-card gates/package corruption больше не попадает в actionable self-heal path
  - local Shining buy теперь coordinated rollback-safe across `shining_abode_state.json` and `soul_state.json`
- `138–142` — lifecycle/history/readability alignment
  - ordered `selectedCardIds` contract подтверждён across cleanup/matching
  - full trade-cycle inspection снова умеет strict current-contract proof
  - malformed preserved afterlife pending contracts остаются видимыми в reminder/notification surfacing
  - duplicate same-cycle pending Shining trade requests reject-ятся validator’ом
  - politics history и notification summaries больше не используют raw tokens как primary text
- `143–149` — transitions, validation and exact-detail follow-up
  - realm transitions теперь abort/rollback на failed `soul_state` realm write
  - New Game Plus rebuild идёт через full game-session safety backup/restore path
  - life-evaluation reward validation fail-close проверяет unresolved realm authority
  - political enum-backed validation tightened
  - stale `selectedCards` receipt snapshots fail validation и не используются как trusted inspection payload
  - inbox fallback details и Codex/relic/forge panels humanize-ят raw archive/project/category/stat tokens

Дальше:

1. fresh wave сейчас закрыта
2. новые независимые findings добавлять с item `150`
3. historical backlog не переоткрывать
4. baseline использовать `648/648`, `1590/1590`

## Ранее открытый пакет items 122–133

Latest implementation pass закрыл весь independent defect batch `122–133`.

- `122–125` — Chaos Sea runtime/state-contract defects
  - explorer-side GM-bound Chaos Sea mutations теперь stage-ят pre-command rollback snapshot до локальных списаний/изъятий
  - `system_guardian_attraction.json` больше не auto-delete-ится на malformed active path и surface-ится как corruption reminder
  - guardian/NPC social pending files больше не fail-open как empty/overwritable state
  - save/load теперь treats resident/social/attraction control files as ephemeral
- `126–129` — Shining runtime/state-contract defects
  - player-founded faction creation теперь rollback-safe и не оставляет spent resources без persisted pending contract
  - trade receipt validation теперь требует тот же ready/closure marker set, что и runtime
  - runtime trade readiness теперь уважает exact `requestId` for request-backed readiness
  - `prepare_incarnation_package` теперь uniformly reject-ит duplicate `selectedCardIds`
- `130–133` — completeness/history/readability gaps
  - archive entry/candidate screens теперь показывают exact Codex provenance and drill-down
  - full founding history больше не re-resolve-ит supporter labels из live resident roster
  - Shining trade/gacha/forge/prompts humanized from raw canonical/workflow tokens
  - `/душа` теперь humanize-ит memory-legacy state instead of raw protocol dumps

Дальше:

1. новые независимые findings добавлять с item `150`
2. historical backlog не переоткрывать
3. использовать текущую baseline: focused `648/648`, full `1590/1590`

## Ранее открытый пакет items 109–121

Latest implementation pass закрыл весь independent defect batch `109–121`.

- `109` — `progression_report.json` теперь treated as ephemeral save/load control artifact
- `110–111` — resident-roster wrong-realm cleanup и receipt-backed closure sync’нуты с canonical pending contract
- `112–114` — Shining political / forge validation теперь требует same closure and post-state proofs as runtime cleanup
- `115–118` — departed resident history, inbox resident drill-down, soul quest resident link и revealed resident snapshot completeness закрыты
- `119–121` — Shining receipt/pending inspection теперь humanized, blessing/prompt UX очищен от system wording, gacha/forge outcome rendering стал snapshot-first

Дальше:

1. новые независимые findings добавлять с item `122`
2. historical backlog не переоткрывать
3. использовать текущую baseline: focused `268/268`, full `1560/1560`

## Ранее закрытый пакет items 42–48

Fix-pass закрыл весь independent defect batch `42–48`.

- Earlier follow-up audit временно переоткрывал `44` и `47`, но оба пункта уже снова закрыты.
- Latest fix-pass также снова закрыл `45` и `48`.

- Chaos Sea progression:
  - `42` unreadable `currentRealm` больше не может silently wipe progression ledger; turn-build теперь fail-close блокируется, validation surface-ит unresolved realm, apply path больше не затирает persisted realm пустым значением
  - `43` accumulated Chaos Sea backlog >1 cycle теперь сохраняется в turn-build path и больше не схлопывается до `1/1`
- Shining history/completeness:
  - `44` Shining trade notifications теперь читают `soldOutCount` и `factionName` из receipt snapshot вместо live inventory reconstruction
  - `45` realignment / leadership history теперь держит snapshot fields и не зависит от mutable current faction/head labels как от primary source
  - `46` `/shining_politics` overview по-прежнему компактный, но теперь честно пишет overflow indicator `…и ещё N`
- Wording follow-up:
  - `47` Shining Gates / project-draft prompts переведены на русский player-facing словарь
  - `48` Chaos Sea / afterlife prompts и related result strings больше не содержат mixed Russian-English protocol prose

## Ранее закрытый пакет items 29–41

Fix-pass закрыл весь independent defect batch `29–41`.

- Earlier follow-up audit временно переоткрывал `34`, но этот пункт уже снова закрыт.
- Latest fix-pass также снова закрыл `33` и `38`.

- Chaos Sea gameplay/state contracts:
  - `29` malformed `progression_report.json` теперь fail-close валидируется как malformed report и больше не очищает pending progression silently
  - `30` malformed `progression_schedule.json` больше не re-bootstrap-ится в нулевой ledger и теперь блокирует progression initialization
- Shining gameplay/state contracts:
  - `31` non-vacant leadership теперь требует canonical head binding; malformed `player_soul` binding больше не выпадает из gate checks
  - `32` pending realignment contracts теперь enforce-ят unique `requestId`, поэтому ambiguous receipt binding по reused requestId закрыт
- Shining history fidelity:
  - `33` founding receipts теперь hydrated stable hall/faction snapshot и political inspection больше не reconstruct-ит историю из mutable current state
  - `37` trade receipt summary теперь читает `soldOutCount` из stable receipt snapshot
  - `38` core project receipts теперь hydrated stable `factionName/projectName` snapshot и всегда показывают canonical ids alongside labels
- Chaos Sea completeness / readability:
  - `34` archive candidates теперь сохраняют полный `content`, а detail path больше не ограничен 220-char summary
  - `35` guardian detail теперь показывает весь completed-project block
  - `36` project detail теперь показывает весь temporary-modifier block
  - `39–41` Soul Relic/archive surfaces теперь humanize-ят slot labels, уводят unknown effect keys в technical block и резолвят guardian/project labels перед raw ids

## Что уже закрыто в fresh wave

### Item 1. stale lore-research history больше не блокирует текущую жизнь

Сделано:

- `GuardianProjectState` больше не возвращает ранний `false/0` на stale `lore_research` entries из старых инкарнаций
- reward/clue consumers теперь пропускают старые completed entries и продолжают поиск current-life project

Ключевые файлы:

- [GuardianProjectState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/GuardianProjectState.cs)
- [GuardianProjectStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/GuardianProjectStateTests.cs)

### Item 2. accepted forge validation теперь проверяет entitlement lifecycle

Сделано:

- accepted forge validation теперь сравнивает projected `relicRefinementEntitlements` с текущим `soul_state`
- добавлен новый validation error: `shining_forge_action_blessing_entitlement_mismatch`
- forge entitlement consumption больше не зависит только от `DateTime.UtcNow`: apply/validation path может пробрасывать `resolvedAtUtc`, поэтому `consumedAtTurn/consumedAtUtc` стали детерминированными для accepted forge

Ключевые файлы:

- [ValidationService.LifecycleControlAndStateFiles.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs)
- [ShiningBlessingEffectState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningBlessingEffectState.cs)
- [ShiningAbodeState.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningAbodeState.TradeAndForge.cs)
- [ShiningCoreActionResolutionValidationTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningCoreActionResolutionValidationTests.cs)
- [ShiningAbodeTradeAndForgeStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningAbodeTradeAndForgeStateTests.cs)

### Item 3. multi-request Shining core pending state больше не auto-heal-ится с потерей данных

Сделано:

- `ShiningCoreActionRequestState.EnsureHealthyAsync()` больше не переписывает `pending_shining_abode_actions.json` в режим `first request wins`, если в файле несколько pending requests
- live malformed multi-request set теперь сохраняется как есть до validation/repair path, поэтому runtime normalization больше не теряет pending action без receipt
- `BuildSystemReminderFragmentAsync()` теперь surface-ит corruption reminder для `shining_core_action_multiple_pending_requests` и не притворяется, что первый request — это единственный authoritative contract

Ключевые файлы:

- [ShiningCoreActionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningCoreActionRequestState.cs)
- [ShiningCoreActionRequestStateTests.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient.Tests/ShiningCoreActionRequestStateTests.cs)

### Items 4-11. correctness holes, full inspections и terminology pass закрыты

Сделано:

- founding validation больше не позволяет обход duplicate hall/faction guard через reused `requestId`; добавлен validation error `shining_founding_duplicate_request_id`
- guardian `processGacha` больше не может переполнить `chargesUsedThisReturn` выше `chargesPerReturn`
- `Полный осмотр решений фракций`, `/сила_обители`, project detail и Shining gacha audit больше не режут историю там, где surface обещает полный осмотр
- forge receipt summary/full inspection теперь humanize-ят форму реликвии вместо raw `targetFormTag`
- player-facing foundation / Shining politics / Shining action confirmations переведены на единый русский словарь без подтверждённых residual English-хвостов в inspection surfaces

Ключевые файлы:

- [ShiningFactionRequestState.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/ShiningFactionRequestState.cs)
- [ValidationService.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs)
- [CanonicalStateNormalizer.SharedAndSoulHelpers.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs)
- [ExplorerMode.Afterlife.ShiningAbode.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs)
- [ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs)
- [ExplorerMode.Afterlife.GuardiansProjectsTrade.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs)
- [ExplorerMode.Afterlife.PlayerGuardianFoundation.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.PlayerGuardianFoundation.cs)
- [ExplorerMode.Afterlife.ShiningAbode.Actions.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs)
- [ExplorerMode.Afterlife.ShiningAbode.Gates.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs)
- [ExplorerMode.Afterlife.ShiningAbode.Politics.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Politics.cs)
- [ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs](/E:/Games/The%20Book%20of%20Eternity%20Reborn/BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs)

## Review follow-up pass closed items 17, 27 and 28

Review follow-up chain теперь закрыт полностью: items `17`, `27` и `28` снова имеют статус `Fixed`.

- Item `17` closed:
  - active guardian faction по-прежнему сохраняет legitimate non-guardian leadership outcomes
  - malformed guardian-head binding теперь rebind-ится к actual active guardian id вместо `guardian + empty id`
- Item `27` closed:
  - accepted `prepare_incarnation_package` receipt теперь обязан проходить с stable `selectedCards` snapshot
  - поздняя hydration snapshot из `preparedIncarnationPackage` оставлена только как legacy fallback для старых receipt’ов
- Item `28` closed:
  - wording follow-up pass убрал residual mixed labels в player-facing inspection surfaces
  - подтверждённо убраны хвосты `Legacy summary`, `restlessness`, `strongest visible pull`, `Выбранные card id`, `Полный frozen payload`, `Soulbound / legendary tier` и overview `tier`-ярлыки

Что уже сделано:

- Chaos Sea correctness:
  - malformed `afterlife_return_guard.json` больше не auto-heal-ится с потерей fail-close guard
  - `guardians.json` теперь требует authoritative `guardians[]` для critical session health
  - `IsChaosSea()` больше не overmatch-ит произвольные `Chaos*` realm labels
  - afterlife progression apply path больше не сжигает Chaos Sea ordinals без valid `progression_report.json`
- Shining correctness:
  - forge runtime/projection теперь списывает `Ink Feathers` и validator ловит missing feather debit
  - active guardian faction больше не теряет legitimate leadership outcome в normalizer pass
  - invalid `projectArchetype` больше не silently normalizes to `accord`
- Chaos Sea completeness:
  - `История жизней` теперь раскрывает canonical `recordLifeCompletion`
  - resident detail теперь full inspection без `Take(...)` на traits/journals/history/transfers
  - `Показать весь журнал Хранителя` теперь показывает canonical metadata, а не только summary line
  - companion-echo relic detail теперь показывает полный snapshot и humanized source labels
- Shining completeness:
  - `Полный осмотр торговых циклов` теперь показывает всю receipt history
  - `player_soul` теперь отображается как `душа игрока`
  - faction political inspection теперь включает `faction.projects[]`
  - Gates inspection теперь даёт player-facing effect description до technical payload
  - prepare-package receipt inspection теперь опирается на stable `selectedCards` snapshot как на обязательный accepted contract для новых receipt’ов
- Additional wording cleanup:
  - inspection surfaces больше не смешивают `Request id` / `Project id` / `payload` / `route options` с русскими подписями
  - wording follow-up pass перевёл residual labels в guardian detail, resident detail и Shining overview/package panels

## Что делать следующим

Сейчас fresh backlog снова закрыт.

- следующий новый independent audit item начинать с `150`
- использовать текущую baseline: focused slice `648/648`, full `1590/1590`
- latest pass был implementation fix-pass по `134–149`: reopened/new пакет снова закрыт и green tests подтверждены
- historical backlog не переоткрывать

## Проверки items 134–149 fix-pass

Focused slice:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~ActorSocialInteractionRequestStateTests|FullyQualifiedName~NpcTradeServiceRequestFlowTests|FullyQualifiedName~ShiningTradeRequestStateTests|FullyQualifiedName~ShiningStateValidationTests|FullyQualifiedName~GuardianSystemRegressionTests|FullyQualifiedName~GameEngineTurnLifecycleTests|FullyQualifiedName~GameEngineSourceGuardTests|FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~ExplorerModeCommandTests.Afterlife|FullyQualifiedName~AfterlifeNotificationStateTests|FullyQualifiedName~ShiningPoliticalResolutionValidationTests|FullyQualifiedName~ShiningCoreActionResolutionValidationTests" --no-restore
```

Результат:

- `648/648`

Полный suite:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

Результат:

- `1590/1590`

## Проверки items 43–48 fix-pass

Focused slice:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~ProgressionScheduleServiceTests|FullyQualifiedName~ShiningTradeRequestStateTests|FullyQualifiedName~AfterlifeNotificationStateTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~GameEngineSourceGuardTests" --no-restore
```

Результат:

- `137/137`

Полный suite:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

Результат:

- `1485/1485`

## Проверки previous fix-pass

Focused slice:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~AfterlifeArchiveActionStateTests|FullyQualifiedName~ChaosSeaPendingRequestHygieneTests|FullyQualifiedName~ShiningCoreActionRequestStateTests|FullyQualifiedName~ShiningTradeRequestStateTests|FullyQualifiedName~ShiningCoreActionResolutionValidationTests|FullyQualifiedName~ShiningStateValidationTests|FullyQualifiedName~AfterlifeNotificationStateTests|FullyQualifiedName~ExplorerModeCommandTests.Afterlife|FullyQualifiedName~GameEngineSourceGuardTests" --no-restore
```

Результат:

- `148/148`

Полный suite:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

Результат:

- `1529/1529`

## Проверки latest closure verification pass

Focused verification slice:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~AfterlifeArchiveActionStateTests|FullyQualifiedName~ChaosSeaPendingRequestHygieneTests|FullyQualifiedName~GuardianAbodeResidentRequestStateTests|FullyQualifiedName~GuardianTradeServiceTests|FullyQualifiedName~ShiningTradeRequestStateTests|FullyQualifiedName~ShiningStateValidationTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~ValidationSourceGuardTests|FullyQualifiedName~GameEngineSourceGuardTests" --no-restore
```

Результат:

- `316/316`

Полный suite:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

Результат:

- `1538/1538`
- reopen-пункты не подтверждены, wave остаётся закрытой

## Проверки wording follow-up pass

Focused wording slice:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerModeSourceGuardTests" --no-restore
```

Результат:

- `141/141`

Полный suite:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

Результат:

- `1459/1459`

## Проверки предыдущего verification pass

Clustered focused slices:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~GuardianProjectStateTests|FullyQualifiedName~AfterlifeReturnGuardServiceTests|FullyQualifiedName~CriticalStateHealthServiceTests|FullyQualifiedName~ProgressionScheduleServiceTests|FullyQualifiedName~GameEngineTurnLifecycleTests|FullyQualifiedName~CanonicalStateNormalizerTests.AfterlifeLore|FullyQualifiedName~AbodePowerRulesTests" --no-restore
```

Результат:

- `80/80`

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~ShiningCoreActionResolutionValidationTests|FullyQualifiedName~ShiningAbodeStateTests|FullyQualifiedName~ShiningAbodeTradeAndForgeStateTests|FullyQualifiedName~ShiningFactionRequestStateTests|FullyQualifiedName~ShiningPoliticalResolutionValidationTests|FullyQualifiedName~ShiningTradeResolutionValidationTests|FullyQualifiedName~ShiningTradeRequestStateTests|FullyQualifiedName~ShiningBlessingEffectStateTests|FullyQualifiedName~ShiningBlessingValidationTests|FullyQualifiedName~ShiningCoreActionRequestStateTests|FullyQualifiedName~CanonicalStateNormalizerTests.ShiningAbode" --no-restore
```

Результат:

- `89/89`

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerModeSourceGuardTests" --no-restore
```

Результат:

- `140/140`

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~ValidationSourceGuardTests|FullyQualifiedName~GameEngineSourceGuardTests" --no-restore
```

Результат:

- `70/70`

Focused slice completion pass:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~ShiningAbodeStateTests|FullyQualifiedName~ShiningCoreActionResolutionValidationTests|FullyQualifiedName~ExplorerModeCommandTests" --no-restore
```

Результат:

- `134/134`

Полный suite:

```powershell
dotnet test .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

Результат:

- `1458/1458`
- это теперь последняя подтверждённая зелёная baseline для всей fresh Chaos Sea / Shining wave

## Важные замечания

- В рабочем дереве много уже существующих незакоммиченных изменений; при продолжении нельзя ничего массово откатывать.
- Fresh backlog — отдельный рабочий документ. Historical [Chaos_Sea_And_Shining_Audit_Backlog.md](/E:/Games/The%20Book%20of%20Eternity%20Reborn/OtherGuides/Chaos_Sea_And_Shining_Audit_Backlog.md) не переоткрывать.
- После каждого следующего pass:
  - обновлять `Chaos_Sea_And_Shining_Fresh_Audit_Backlog.md`
  - добавлять `Fix note`
  - запускать focused slice, затем полный suite
