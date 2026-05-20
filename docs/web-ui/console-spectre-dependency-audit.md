# Console/Spectre Dependency Audit For Local Web UI

Tracked task: #560  
Parent epic: #559

## Summary

The current codebase is not yet browser-ready because the command layer is still presentation-bound:

- `IExplorerConsole` is useful as a first seam, but it still exposes Spectre types: `IRenderable` and `IPrompt<T>`.
- Most `ExplorerMode` commands write `Panel`, `Table`, `Rule`, raw Spectre markup strings, and prompts directly instead of returning a UI-neutral result.
- Main menu, lifecycle, validation/repair, QTE, and turn-processing paths still call `AnsiConsole` / `Console.ReadKey` directly and bypass `IExplorerConsole`.
- Browser support should therefore be implemented through a UI-neutral command DTO protocol first, then console/browser renderers.

Recommended implementation order is the existing issue chain:

1. #561 defines UI-neutral DTOs.
2. #562 adds command migration registry and coverage.
3. #563 renders DTOs back to Spectre for console parity.
4. #564 migrates the first safe read-only command group.
5. #568 adds local session locking before broad mutating-command browser exposure.
6. #565, #566, and #567 add local web host, command API, and browser renderer.
7. #569-#575 migrate command groups and interactive protocols.
8. #576 adds parity tests.
9. #577 documents launch/troubleshooting.

## Direct Dependency Inventory

Search patterns:

```text
AnsiConsole|Console.Read(Key|Line)|SelectionPrompt|TextPrompt|ConfirmationPrompt|IPrompt<|IRenderable|new Panel|new Table
```

Current major hit counts:

| File | Hits | Recommendation | Follow-up |
|---|---:|---|---|
| `BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs` | 134 | Lifecycle/menu protocol, not simple renderer migration. Contains direct menus, confirmations, incarnation/reentry/return previews, load/save prompts. | #574 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` | 116 | Turn-processing status/progress output should become host-neutral notifications/events. | #574 |
| `BookOfEternityClient/UI/GameInterface.cs` | 77 | Console renderer helper. Keep console-only, but move logical content upstream into DTOs. | #563 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.OptionsAndSettings.cs` | 65 | Browser settings UI needs separate protocol; initially registry can mark some settings console-only. | #562, #574 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs` | 54 | Mixed read-only status plus mutating directives/stat allocation. Split DTO migration by command. | #569, #570 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs` | 45 | Good first DTO migration candidate for read-only story/status panels. | #564, #569 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs` | 40 | Mutating afterlife economy/pending-contract commands need session lock. | #568, #571 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs` | 39 | Chaos Sea mixed read/write commands; migrate after lock and DTO renderer. | #571 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs` | 37 | Shared Explorer helpers, pending-turn rollback, scenario-core prompts. Needs lifecycle protocol treatment. | #574 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs` | 35 | Mostly afterlife read/action surfaces; split read-only archive/inbox before mutating archive actions. | #571, #573 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaWorldSetupAndDebug.cs` | 32 | Debug/world setup is mixed read/write; keep guarded until registry and lock exist. | #562, #569 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs` | 29 | Shining economy/forge commands are stateful and lock-sensitive. | #568, #572 |
| `BookOfEternityClient/Services/QteSceneService.cs` | 21 | Special protocol required; direct timed/branching prompts cannot be rendered as simple one-shot command DTOs. | #575 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs` | 20 | Shining overview/actions split: read-only overview first, actions after lock. | #572 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs` | 19 | Repair loops need structured errors/actions, not just text panels. | #574 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.FactionsAndWorldNews.cs` | 17 | Mortal faction/news DTO migration. | #570 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs` | 16 | Combat help/log/profile output must preserve Russian terminology; mutating spiritual actions need lock. | #573 |
| `BookOfEternityClient/Services/ImageService.cs` | 15 | Image generation progress/status should be host-neutral notifications; not part of first command DTO migration. | #566, #574 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.Trade.cs` | 14 | Mortal trade commands are mutating and lock-sensitive. | #568, #570 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaLoreAndTravel.cs` | 14 | Mixed read-only lore/travel/status surfaces. | #569, #570 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Politics.cs` | 13 | Shining political commands/campaigns. | #572 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs` | 13 | Soul Gates/lifecycle adjacent; needs lifecycle protocol and lock. | #568, #574 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.PlayerGuardianFoundation.cs` | 11 | Mutating Chaos Sea foundation pending contract. | #568, #571 |
| `BookOfEternityClient/UI/SpectreExplorerConsole.cs` | 9 | Keep as concrete console renderer only; DTO protocol must not depend on it. | #563 |
| `BookOfEternityClient/UI/ConsoleLayout.cs` | 9 | Console-only layout utility; later used by DTO-to-Spectre renderer. | #563 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SourceOfLight.cs` | 8 | Shining capstone pending contract; lock-sensitive. | #568, #572 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs` | 7 | Mortal inventory read-only first; trade/use mutations later. | #570 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs` | 6 | Shining core action pending contracts. | #568, #572 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.IncarnationAndAfterlife.cs` | 6 | Memory-selection and afterlife lifecycle protocol. | #574 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SarefStory.cs` | 6 | Read-only story display plus Wings search pending command. Story display can migrate early; search needs lock. | #569, #572 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.QuestsAndRivals.cs` | 6 | Mortal quests/rivals, some selection prompts. | #570 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Treasury.cs` | 5 | Local Shining economy writes; must wait for lock. | #568, #572 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.ListAndDetails.cs` | 5 | Mortal NPC list/details mostly read-only DTO candidate. | #570 |
| `BookOfEternityClient/Core/StandardTextComposerConsole.cs` | 5 | Console adapter for text composer; should remain console-only. | #563 |
| `BookOfEternityClient/Core/GameEngine.cs` | 4 | Top-level exception/status console output; wrap as host notification later. | #574 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.StatusAudit.cs` | 4 | Read-only afterlife status audit DTO candidate. | #573 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ChaosSea.ActionPreviews.cs` | 3 | Shared mutating action preview confirmation. | #568, #571 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.ActionPreviews.cs` | 3 | Shared Shining action preview confirmation. | #568, #572 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.EntityProfiles.cs` | 2 | Good read-only DTO candidate for afterlife profiles. | #573 |
| `BookOfEternityClient/UI/IExplorerConsole.cs` | 2 | Interface leak: `IRenderable` and `IPrompt<T>` make it console-oriented. | #561 |

## Command Group Inventory

### Universal / Meta

Registered in `_universalCommands`:

- Help/status/story: `/help`, `/помощь`, `/status`, `/статус`, `/story`, `/рассказ`, `/история`, `/chronicle`, `/хроника`, `/lives`, `/жизни`, `/codex`, `/кодекс`, `/achievements`, `/достижения`, `/gallery`, `/галерея`.
- Soul/afterlife meta: `/soul`, `/душа`, `/soul_relics`, `/реликвии`, `/afterlife_archive`, `/архив_души`, `/archive_candidates`, `/архив_кандидаты`, `/soul_quests`, `/квесты_души`, `/feathers`, `/перья`.
- Debug/config/world setup: `/gm`, `/гм`, `/debug`, `/отладка`, `/validate`, `/валидация`, `/mods`, `/моды`, `/system_guardians`, `/системные_хранители`, `/извечные_хранители`, `/world_setup`, `/настройка_мира`, `/world_rules`, `/правила_мира`.
- Saref read-only story aliases: `/saref`, `/сареф`, `/saref_story`, `/история_сарефа`, `/wings_of_angels`, `/крылья_над_бездной`.

Recommendation:

- First migrate safe read-only commands into `ExplorerCommandResult` DTOs (#564).
- Keep mutating/debug commands explicitly registry-marked until locking and API semantics exist (#562, #568).
- Use console renderer adapter for existing console output (#563), then expose through web command API (#566).

### Mortal World Commands

Registered in `_mortalOnlyCommands`:

- Inventory/world/status: `/inv`, `/inventory`, `/инв`, `/инвентарь`, `/map`, `/карта`, `/where_am_i`, `/где_я`, `/locations`, `/локации`, `/transport`, `/транспорт`, `/weather`, `/погода`, `/effects`, `/эффекты`.
- NPCs/trading/interactions: `/npc`, `/npcs`, `/characters`, `/нпс`, `/персонажи`, `/interactions`, `/взаимодействия`.
- Quests/factions/news: `/quests`, `/квесты`, `/factions`, `/фракции`, `/world_news`, `/новости_мира`, `/rival_threads`, `/чужие_нити`, `/guardian_corrections`, `/коррективы_хранителя`.
- Player stats/actions: `/skills`, `/навыки`, `/stats`, `/статы`, `/характеристики`, `/distribute`, `/распределить`, `/companion_directive`, `/директива_компаньону`, `/faction_directive`, `/директива_фракции`, `/craft`, `/ремесло`, `/combat`, `/бой`, `/books`, `/книги`, `/читать`, `/storage_access`, `/доступ_к_хранилищам`.

Recommendation:

- Read-only state commands can migrate after DTO/renderer foundation (#570).
- Directives, stat distribution, craft/trade-like flows, and anything writing state require session lock (#568).

### Chaos Sea Commands

Registered in `_chaosSeaOnlyCommands` and exact-Chaos gated:

- Guardian/Abode: `/chaos_sea`, `/море_хаоса`, `/guardians`, `/хранители`, `/abodes`, `/обители`, `/abode_power`, `/сила_обители`.
- Cost/pending actions: `/abode_offering`, `/подношение_обители`, `/guardian_projects`, `/проекты_хранителей`, `/gacha`, `/гача`, `/found_guardian_mantle`, `/учредить_хранителя`.
- Chaos-accessible afterlife surfaces: profiles, spiritual conflict/help/log/arts/action, inbox, archive, relics.

Recommendation:

- Pure status panels migrate after DTO foundation (#571).
- Pending-contract creation and local economy writes must wait for lock (#568).

### Shining Abode Commands

Registered through the afterlife command group but Shining-gated internally:

- Overview/politics/gates: `/shining_abode`, `/сияющая_обитель`, `/shining_politics`, `/сияющая_политика`.
- Economy/core actions: `/shining_treasury`, `/казначейство`, trade/forge/core-action surfaces.
- Capstones/story: `/source_of_light`, `/источник_света`, `/saref`, `/сареф`, `/сареф найти_крылья`.

Recommendation:

- Overview/politics display can migrate to DTOs after #561/#563.
- Treasury, trade, forge, Source of Light, Gates, and Saref Wings search must be guarded by #568.
- Shining command group migration is #572.

### Afterlife Combat / Entity Systems

Commands:

- `/spiritual_conflict`, `/духовный_конфликт`.
- `/spiritual_combat_log`, `/журнал_духовного_боя`.
- `/spiritual_combat_help`, `/духовный_бой`.
- `/spiritual_arts`, `/духовные_искусства`.
- `/spiritual_action`, `/духовное_действие`.
- `/afterlife_profiles`, `/профили_загробья`.

Recommendation:

- Help/log/profile/status commands are good DTO migration candidates (#573).
- `spiritual_action` is a mutating/action-generation flow and should wait for session lock (#568).
- Russian terminology must remain canonical in both console and browser renderers (#573, #576).

### Lifecycle / Local-Turn / Repair

Major direct console surfaces:

- Main menu navigation and save loading.
- `/incarnate`, world setup prompts, pending setup preview, rollback/cancel.
- `return_to_chaos_sea`, `reenter_shining_abode`, Soul Gates, memory selection.
- Validation repair loops, active GM-turn notices, late response state.

Recommendation:

- Treat as stateful protocols, not simple command output (#574).
- Browser needs explicit operation states: preview, confirm, pending, cancelled, failed, rollback-restored, completed.
- Do not expose broad local-turn mutations over web until #568 exists.

### QTE / Interactive Scenes

`QteSceneService` directly uses `AnsiConsole.Clear`, `Panel`, `Markup`, and `SelectionPrompt`.

Recommendation:

- Requires dedicated browser-compatible timed/interactive protocol (#575).
- Until then, registry should mark QTE handling as blocked or console-only with an issue link (#562).

## Structural Findings

- `IExplorerConsole` is not enough for browser UI because it is a console abstraction, not a logical result model.
- Current tests usually assert rendered Spectre text; parity tests should assert DTOs before rendering (#576).
- A local web API must not expose mutating commands until session locking prevents console/browser concurrent writes (#568).
- The browser should not duplicate game logic. It should render DTO blocks/actions and call the same command/application services (#566, #567).
- `GameInterface`, `ConsoleLayout`, `SpectreExplorerConsole`, and `StandardTextComposerConsole` should remain console-specific adapters.

## Migration Status By Issue

| Issue | Status from this audit |
|---:|---|
| #561 | Required next: define `ExplorerCommandResult`, blocks, actions, prompts, notifications. |
| #562 | Required early: every registered command must get migration status and blocked reason. |
| #563 | Required before command migrations: render DTOs through existing console UI. |
| #564 | Safe first migration: read-only universal/meta command group. |
| #565 | Web host can start after DTO skeleton exists. |
| #566 | Command API should initially expose only migrated read-only commands. |
| #567 | Browser renderer should consume DTOs only. |
| #568 | Required before browser mutating commands. |
| #569 | Universal/help/status/meta full migration after #564 proves pattern. |
| #570 | Mortal command migration after lock for mutating flows. |
| #571 | Chaos Sea command migration after lock for pending/local economy writes. |
| #572 | Shining Abode migration after lock and lifecycle blockers are represented. |
| #573 | Afterlife combat/entity migration; preserve Russian terminology and log/profile parity. |
| #574 | Lifecycle/local-turn commands need protocol states. |
| #575 | QTE needs special event/state protocol. |
| #576 | Parity tests should compare DTO logical output and renderer output. |
| #577 | Docs should be updated after actual launch commands and temporary console-only registry entries exist. |
