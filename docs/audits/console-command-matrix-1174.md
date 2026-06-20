# Console Command Matrix for 9/10 Polish Pass (#1174-#1180)

Date: 2026-06-20

Branch: `work/1174-console-polish-9`

Scope: ordinary console-client commands from `ExplorerCommandCatalog`. Browser UI and QTE are explicitly out of scope.

## Sources

- `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`
- `BookOfEternityClient.Tests/MortalCommandDisplaySaveTests.cs`
- `BookOfEternityClient.Tests/ChaosSeaCommandDisplaySaveTests.cs`
- `BookOfEternityClient.Tests/ShiningAbodeCommandDisplaySaveTests.cs`
- `BookOfEternityClient.Tests/ExplorerModeCommandTests*.cs`

## Coverage Key

- `Mortal fixture`: `mortal_world_command_display_fixture.zip`; test enumerates all `MortalWorld` descriptors and practical universal preview commands.
- `Chaos fixture`: `chaos_sea_command_display_fixture.zip`; test enumerates all `ChaosSea` and `AfterlifeCombatAndEntities` descriptors plus practical universal preview commands.
- `Shining fixture`: `shining_abode_command_display_fixture.zip`; test enumerates all `ShiningAbode` and `AfterlifeCombatAndEntities` descriptors plus practical universal preview commands.
- `General tests`: covered by targeted `ExplorerModeCommandTests*`, source guards, or command protocol tests, but not always by a reusable display save.
- `Dry sweep target`: should be included in the #1176 dry sweep unless noted as diagnostic or lifecycle-mutating preview.
- `Gap`: needs either fixture enrichment, a focused test, or a follow-up issue.

## Summary

- Catalog descriptors found: 91.
- QTE descriptors in catalog: 0. QTE remains excluded by user instruction.
- Browser/frontend commands: excluded.
- Existing reusable display-save coverage is strongest for `MortalWorld`, `ChaosSea`, `ShiningAbode`, and shared afterlife combat/entity commands.
- Main audit risk is not missing command registration, but uneven quality gates for universal/debug/Saref/lifecycle commands and local-turn preview flows.

## Universal and Lifecycle Commands

| ID | Aliases | Mode | Fixture/Test Coverage | 9/10 Expectation | Status |
|---|---|---:|---|---|---|
| `help` | `/help`, `/помощь` | ReadOnly | Mortal/Chaos/Shining practical previews; help tests | Help is localized, realm-aware, and does not advertise QTE in this pass as required coverage. | Dry sweep target |
| `math` | `/math`, `/математик` | ReadOnly | General tests | Calculator output is readable and does not leak parser internals. | Gap: add to dry sweep |
| `soul` | `/soul`, `/душа` | ReadOnly | Mortal/Chaos/Shining practical previews | Soul summary is readable in every lifecycle. | Dry sweep target |
| `soul_relics` | `/soul_relics`, `/реликвии` | ReadOnly | Chaos/Shining details; practical previews | Relic list/detail uses localized labels and no raw ids unless needed as explicit detail command. | Dry sweep target |
| `afterlife_archive` | `/afterlife_archive`, `/архив_души` | ReadOnly | Chaos/Shining details; practical previews | Archive entries summarize first, detail selected entry separately. | Dry sweep target |
| `archive_candidates` | `/archive_candidates`, `/архив_кандидаты` | ReadOnly | Chaos/Shining details; practical previews | Candidate detail shows player-facing reason and next action. | Dry sweep target |
| `soul_quests` | `/soul_quests`, `/квесты_души` | ReadOnly | Chaos/Shining practical previews | Soul quests show status, reward, and blockers in Russian. | Dry sweep target |
| `codex` | `/codex`, `/кодекс` | ReadOnly | Mortal/Chaos/Shining practical previews; general tests | Search/list output is readable and avoids raw file names by default. | Dry sweep target |
| `achievements` | `/achievements`, `/достижения` | ReadOnly | Mortal/Chaos/Shining practical previews; general tests | Achievements show source, reward, and permanent/current status. | Dry sweep target |
| `chronicle` | `/chronicle`, `/хроника` | ReadOnly | Chaos/Shining practical previews | Chronicle summaries are useful and detailed records are discoverable. | Dry sweep target |
| `story` | `/story`, `/рассказ`, `/история` | ReadOnly | Mortal/Chaos/Shining practical previews; general tests | Reader output is player-facing prose, no file-contract leakage. | Dry sweep target |
| `behavior` | `/behavior`, `/поведение` | ReadOnly | Mortal/Chaos/Shining practical previews | Shows visible behavior settings in player language. | Dry sweep target |
| `lives` | `/lives`, `/жизни` | ReadOnly | Mortal/Chaos/Shining practical previews | Life history is summarized with detail access. | Dry sweep target |
| `feathers` | `/feathers`, `/перья` | ReadOnly | Mortal/Chaos/Shining practical previews; afterlife action tests | Ink feather status/actions are readable and realm-aware. | Dry sweep target |
| `world_rules` | `/world_rules`, `/правила_мира` | ReadOnly | Mortal/Chaos/Shining practical previews; general tests | Player sees world rules, not file/directive internals. | Dry sweep target |
| `gallery` | `/gallery`, `/галерея` | ReadOnly | Mortal/Chaos/Shining practical previews | Gallery output is readable or clearly empty. | Dry sweep target |
| `status` | `/status`, `/статус`; subcommand `audit` | ReadOnly | All fixtures; many focused tests | Default status is player-facing; `audit` is explicit diagnostic. | Dry sweep target |
| `gm` | `/gm`, `/гм` | ReadOnly | General tests | Shows bridge/GM state without agent internals unless diagnostic. | Gap: add to dry sweep |
| `debug` | `/debug`, `/отладка` | ReadOnly | General tests | Explicit diagnostic surface; raw/internal output allowed only here. | Diagnostic, sweep separately |
| `mods` | `/mods`, `/моды` | ReadOnly | Mortal practical preview; general tests | Mod list/detail is readable and navigable. | Dry sweep target |
| `system_guardians` | `/system_guardians`, `/системные_хранители`, `/извечные_хранители` | ReadOnly | General tests | Preset overview/detail hides technical English fields. | Dry sweep target |
| `validate` | `/validate`, `/валидация` | LocalTurn | Mortal/Chaos/Shining practical previews | Validation result is understandable and does not force JSON reading. | Dry sweep target |
| `world_setup` | `/world_setup`, `/настройка_мира` | LocalTurn | Chaos/Shining practical previews; general tests | Setup flow is explicit local action, with preview/cancel. | Local preview sweep |

## Mortal World Commands

| ID | Aliases | Mode | Fixture/Test Coverage | 9/10 Expectation | Status |
|---|---|---:|---|---|---|
| `inventory` | `/inv`, `/inventory`, `/инв`, `/инвентарь` | ReadOnly | Mortal fixture; inventory focused tests | Item list/detail, bonuses, structural bonuses, documents, markup safety. | Dry sweep target |
| `npcs` | `/npc`, `/npcs`, `/characters`, `/нпс`, `/персонажи` | ReadOnly | Mortal fixture; NPC tests | Overview remains useful; thoughts, quests, relationships, and details are drillable. | Dry sweep target |
| `npc_talk` | `/npc_talk`, `/talk_npc`, `/поговорить_с_нпс`, `/разговор_с_нпс` | LocalTurn | Mortal fixture | Preview/prompt makes target and expected GM action clear. | Local preview sweep |
| `quests` | `/quests`, `/квесты` | ReadOnly | Mortal fixture; quest tests | Quest summary, history, rewards, blockers without raw ids. | Dry sweep target |
| `map` | `/map`, `/карта` | ReadOnly | Mortal fixture; map tests | Shows Mortal map/location, not afterlife guardian blocker text. | Dry sweep target |
| `where_am_i` | `/where_am_i`, `/где_я` | ReadOnly | Mortal fixture; location tests | Current location summary and available routes are readable. | Dry sweep target |
| `factions` | `/factions`, `/фракции` | ReadOnly | Mortal fixture; faction drilldown tests | Overview plus faction detail menus for resources, projects, hierarchy, chronicle. | Dry sweep target |
| `skills` | `/skills`, `/навыки` | ReadOnly | Mortal fixture; skill tests | Skill bonuses and scaling fields localized. | Dry sweep target |
| `stats` | `/stats`, `/статы`, `/характеристики` | ReadOnly | Mortal fixture | Character stats are readable and explain derived values. | Dry sweep target |
| `world_news` | `/world_news`, `/новости_мира` | ReadOnly | Mortal fixture; world-news tests | Useful summary first; detailed event/flag/progress records; no raw footer noise. | Dry sweep target |
| `rival_threads` | `/rival_threads`, `/чужие_нити` | ReadOnly | Mortal fixture; rival tests | Rival threads show state, stakes, and detail actions. | Dry sweep target |
| `guardian_corrections` | `/guardian_corrections`, `/коррективы_хранителя` | ReadOnly | Mortal fixture; general tests | Current-life correction journal is readable. | Dry sweep target |
| `locations` | `/locations`, `/локации` | ReadOnly | Mortal fixture; location tests | Location list and detail have player terms and no hidden errors. | Dry sweep target |
| `transport` | `/transport`, `/транспорт` | ReadOnly | Mortal fixture | Transport inventory/routes readable; no raw storage shape. | Dry sweep target |
| `effects` | `/effects`, `/эффекты` | ReadOnly | Mortal fixture; effects tests | Summary effects link to details or clear fallback from status conditions. | Dry sweep target |
| `combat` | `/combat`, `/бой` | ReadOnly | Mortal fixture; combat drilldown tests | Combat overview plus enemy/ally/log details. QTE is excluded. | Dry sweep target |
| `weather` | `/weather`, `/погода` | ReadOnly | Mortal fixture; weather tests | Weather is readable or clearly unavailable. | Dry sweep target |
| `books` | `/books`, `/книги`, `/читать` | ReadOnly | Mortal fixture; books tests | Shelf first, selected document detail, return to shelf. | Dry sweep target |
| `storage_access` | `/storage_access`, `/доступ_к_хранилищам` | ReadOnly | Mortal fixture; storage tests | Storage access list is readable and explains unavailable entries. | Dry sweep target |
| `interactions` | `/interactions`, `/взаимодействия` | ReadOnly | Mortal fixture; interaction tests | Player/record drilldowns are available. | Dry sweep target |
| `ink_feather_reveal_fate` | `/reveal_fate`, `/открыть_судьбу` | LocalTurn | Mortal fixture; fate tests | Preview explains cost and outcome boundary. | Local preview sweep |
| `ink_feather_rewrite_fate` | `/rewrite_fate`, `/переписать_судьбу` | LocalTurn | Mortal fixture | Preview explains cost, target, and limitation. | Local preview sweep |
| `distribute` | `/distribute`, `/распределить` | LocalTurn | Mortal fixture | Point distribution prompt is clear and reversible before commit. | Local preview sweep |
| `companion_directive` | `/companion_directive`, `/директива_компаньону` | LocalTurn | Mortal fixture; directive tests | Prompt does not expose file contracts. | Local preview sweep |
| `faction_directive` | `/faction_directive`, `/директива_фракции` | LocalTurn | Mortal fixture; directive tests | Prompt does not expose file contracts. | Local preview sweep |
| `inventory_equip` | `/экипировать`, `/equip` | LocalTurn | Mortal fixture; inventory action tests | Preview names item/slot and blocker reason. | Local preview sweep |
| `inventory_unequip` | `/снять`, `/unequip` | LocalTurn | Mortal fixture; inventory action tests | Preview names item/slot and blocker reason. | Local preview sweep |
| `inventory_drop` | `/выбросить_предмет`, `/inventory_drop` | LocalTurn | Mortal fixture | Confirmation describes exact item and quantity. | Local preview sweep |
| `inventory_split` | `/разделить_стопку`, `/inventory_split` | LocalTurn | Mortal fixture | Stack split prompt is clear. | Local preview sweep |
| `inventory_merge` | `/объединить_стопки`, `/inventory_merge` | LocalTurn | Mortal fixture | Stack merge prompt is clear. | Local preview sweep |
| `storage_item_move` | `/storage_move`, `/хранилище_предметы` | LocalTurn | Mortal fixture; storage tests | Direction/item choices use player-facing names. | Local preview sweep |
| `vehicle_item_move` | `/vehicle_move`, `/транспорт_предметы` | LocalTurn | Mortal fixture; transport tests | Direction/item choices use player-facing names. | Local preview sweep |
| `npc_trade` | `/npc_trade`, `/торговля_нпс` | LocalTurn | Mortal fixture; trade tests | Buy/sell/buyback choices are readable. | Local preview sweep |
| `craft` | `/craft`, `/ремесло` | LocalTurn | Mortal fixture; craft tests | Craft options explain requirements and blockers. | Local preview sweep |

## Chaos Sea Commands

| ID | Aliases | Mode | Fixture/Test Coverage | 9/10 Expectation | Status |
|---|---|---:|---|---|---|
| `chaos_sea` | `/chaos_sea`, `/море_хаоса` | ReadOnly | Chaos fixture; afterlife tests | Overview is player-facing and hides pending contract audit by default. | Dry sweep target |
| `guardians` | `/guardians`, `/хранители` | ReadOnly | Chaos fixture details; guardian tests | Guardian list/detail, resident actions, and waiting states are readable. | Dry sweep target |
| `abode_power` | `/abode_power`, `/сила_обители` | ReadOnly | Chaos fixture details | Power entries and derived rules are localized. | Dry sweep target |
| `guardian_projects` | `/guardian_projects`, `/проекты_хранителей` | ReadOnly | Chaos fixture details | Project detail exposes state, resources, and next actions. | Dry sweep target |
| `guardian_politics` | `/guardian_politics`, `/политика_хранителей` | ReadOnly | Chaos fixture | Politics overview is readable and not a raw faction dump. | Dry sweep target |
| `abodes` | `/abodes`, `/обители` | ReadOnly | Chaos fixture details | Abode overview/detail is readable. | Dry sweep target |
| `gacha` | `/gacha`, `/гача` | LocalTurn | Chaos fixture | Cost/charges/outcome boundary is clear before action. | Local preview sweep |
| `abode_offering` | `/abode_offering`, `/подношение_обители` | LocalTurn | Chaos fixture | Offering choices and costs are clear. | Local preview sweep |
| `found_guardian_mantle` | `/found_guardian_mantle`, `/учредить_хранителя` | LocalTurn | Chaos fixture | Founding preview explains cost and requirements. | Local preview sweep |
| `guardian_trade` | `/guardian_trade`, `/торговля_хранителя` | LocalTurn | Chaos fixture; trade tests | Trade choices are readable and rollback-safe. | Local preview sweep |
| `guardian_social` | `/guardian_social`, `/talk_guardian`, `/поговорить_с_хранителем`, `/общение_хранителя` | LocalTurn | Chaos fixture; social tests | Talk/action preview names guardian and intent. | Local preview sweep |
| `abode_residents` | `/abode_residents`, `/обитатели_обители` | LocalTurn | Chaos fixture; resident tests | Resident roster/request/waiting states are readable. | Local preview sweep |
| `resident_interaction` | `/resident_interaction`, `/общение_резидента`, `/поговорить_с_резидентом`, `/история_резидента` | LocalTurn | Chaos fixture; resident tests | Resident interaction prompt is player-facing. | Local preview sweep |
| `resident_transfer` | `/resident_transfer`, `/переход_резидента` | LocalTurn | Chaos fixture; resident tests | Transfer preview names source/target and blockers. | Local preview sweep |
| `soul_relic_equip` | `/soul_relic_equip`, `/экипировать_реликвию` | LocalTurn | Chaos fixture; relic tests | Relic/slot/action result is readable. | Local preview sweep |
| `soul_relic_unequip` | `/soul_relic_unequip`, `/снять_реликвию` | LocalTurn | Chaos fixture; relic tests | Relic/slot/action result is readable. | Local preview sweep |

## Shining Abode Commands

| ID | Aliases | Mode | Fixture/Test Coverage | 9/10 Expectation | Status |
|---|---|---:|---|---|---|
| `shining_abode` | `/shining_abode`, `/сияющая_обитель` | ReadOnly | Shining fixture details | Overview and details for gates/projects/outcomes are navigable. | Dry sweep target |
| `shining_politics` | `/shining_politics`, `/сияющая_политика` | ReadOnly | Shining fixture details | Faction/resource/chronicle/decision details are readable. | Dry sweep target |
| `shining_faction_founding` | `/shining_faction_founding`, `/основание_сияющей_фракции` | LocalTurn | Shining fixture; politics tests | Founding preview explains supporters and costs. | Local preview sweep |
| `shining_faction_realignment` | `/shining_faction_realignment`, `/перестройка_сияющей_фракции` | LocalTurn | Shining fixture; politics tests | Realignment target/source choices are clear. | Local preview sweep |
| `shining_faction_leadership` | `/shining_faction_leadership`, `/смена_главы_сияющей_фракции` | LocalTurn | Shining fixture; politics tests | Leadership candidates and blockers are clear. | Local preview sweep |
| `shining_native_faction_discovery` | `/shining_native_faction_discovery`, `/открытие_нативной_фракции` | LocalTurn | Shining fixture; action tests | Discovery preview explains costs and result boundary. | Local preview sweep |
| `shining_faction_investment` | `/shining_faction_investment`, `/инвестиция_в_сияющую_фракцию` | LocalTurn | Shining fixture; action tests | Eligible factions and costs are readable. | Local preview sweep |
| `shining_project_support` | `/shining_project_support`, `/поддержать_сияющий_проект` | LocalTurn | Shining fixture; action tests | Project support choices are readable. | Local preview sweep |
| `shining_project_unsupport` | `/shining_project_unsupport`, `/снять_поддержку_сияющего_проекта` | LocalTurn | Shining fixture; action tests | Unsupported/supported states are clear. | Local preview sweep |
| `shining_project_retirement` | `/shining_project_retirement`, `/отправить_сияющий_проект_в_историю` | LocalTurn | Shining fixture; action tests | Retirement consequence is clear. | Local preview sweep |
| `shining_gates_open` | `/shining_gates_open`, `/открыть_врата_инкарнации` | LocalTurn | Shining fixture; gates tests | Gate state and costs are readable. | Local preview sweep |
| `shining_gates_select` | `/shining_gates_select`, `/выбрать_благословение` | LocalTurn | Shining fixture; gates tests | Blessing card choices are readable. | Local preview sweep |
| `shining_gates_deselect` | `/shining_gates_deselect`, `/снять_благословение` | LocalTurn | Shining fixture; gates tests | Deselect result is readable. | Local preview sweep |
| `shining_gates_reroll` | `/shining_gates_reroll`, `/обновить_врата` | LocalTurn | Shining fixture; gates tests | Reroll entitlement/cost is clear. | Local preview sweep |
| `shining_incarnation_prepare` | `/shining_incarnation_prepare`, `/подготовить_новую_жизнь` | LocalTurn | Shining fixture; gates tests | Prepared package summary is clear. | Local preview sweep |
| `shining_relic_forge` | `/shining_relic_forge`, `/сияющая_ковка` | LocalTurn | Shining fixture; forge tests | Forge choices/costs/property changes are readable. | Local preview sweep |
| `shining_trade` | `/shining_trade`, `/сияющая_торговля` | LocalTurn | Shining fixture; trade tests | Buy/sell boundary is player-facing. | Local preview sweep |
| `shining_treasury` | `/shining_treasury`, `/казначейство` | LocalTurn | Shining fixture; treasury tests | Treasury blockers/costs are readable. | Local preview sweep |
| `source_of_light` | `/source_of_light`, `/источник_света` | LocalTurn | Shining fixture; source tests | Source action/result boundary is readable. | Local preview sweep |

## Shared Afterlife Combat and Entity Commands

| ID | Aliases | Mode | Fixture/Test Coverage | 9/10 Expectation | Status |
|---|---|---:|---|---|---|
| `afterlife_profiles` | `/afterlife_profiles`, `/профили_загробья` | ReadOnly | Chaos/Shining fixtures details | Profiles summarize and show full localized detail by selection. | Dry sweep target |
| `afterlife_threats` | `/afterlife_threats`, `/угрозы_загробья` | ReadOnly | Chaos/Shining fixtures details | Threats show stakes, state, and actions without raw ids by default. | Dry sweep target |
| `afterlife_chronicles` | `/afterlife_chronicles`, `/хроники_посмертия` | ReadOnly | Chaos/Shining fixtures details | Chronicle list/detail is readable. | Dry sweep target |
| `afterlife_inbox` | `/afterlife_inbox`, `/уведомления_загробья` | LocalTurn | Chaos/Shining fixtures details | Notifications show player-facing action options. | Local preview sweep |
| `spiritual_conflict` | `/spiritual_conflict`, `/духовный_конфликт` | ReadOnly | Chaos/Shining fixtures details | Current conflict is readable; QTE is excluded. | Dry sweep target |
| `spiritual_combat_log` | `/spiritual_combat_log`, `/журнал_духовного_боя` | ReadOnly | Chaos/Shining fixtures details | Exchange/recent conflict log is readable. | Dry sweep target |
| `spiritual_combat_help` | `/spiritual_combat_help`, `/духовный_бой` | ReadOnly | Afterlife tests | Help explains tactics/position/fair criticals in player terms. | Dry sweep target |
| `spiritual_arts` | `/spiritual_arts`, `/духовные_искусства` | LocalTurn | Chaos/Shining fixtures details; arts tests | Arts list/detail/upgrade preview has localized mechanics. | Local preview sweep |
| `spiritual_action` | `/spiritual_action`, `/духовное_действие` | LocalTurn | Afterlife tests | Action preview is clear and no raw contract leaks. | Local preview sweep |
| `archive_consultation` | `/archive_consultation`, `/архивная_консультация` | LocalTurn | Chaos/Shining fixtures details | Consultation target and cost are clear. | Local preview sweep |
| `archive_project_fuel` | `/archive_project_fuel`, `/архивная_подпитка_проекта` | LocalTurn | Chaos/Shining fixtures details | Project fuel target/cost/result boundary is clear. | Local preview sweep |

## Saref and Memory Commands

| ID | Aliases | Mode | Fixture/Test Coverage | 9/10 Expectation | Status |
|---|---|---:|---|---|---|
| `saref_story` | `/saref`, `/сареф`, `/saref_story`, `/история_сарефа`, `/wings_of_angels`, `/крылья_над_бездной` | ReadOnly + subcommands | Saref focused tests | Hidden-story state and actions must be readable and not expose internal route names by default. | Gap: add dry sweep if fixture allows |
| `saref_memory_scene` | `/воспоминание`, `/воспоминание_статус`, `/воспоминание_начать`, `/воспоминание_способности` | ReadOnly + subcommands | Memory-scene focused tests | Memory scene status/start/abilities are readable and lifecycle-safe. | Gap: add dry sweep if fixture allows |

## Initial Findings for #1175

1. Existing reusable fixtures are strong enough to start #1176 without creating new saves first.
2. `MortalCommandDisplaySaveTests`, `ChaosSeaCommandDisplaySaveTests`, and `ShiningAbodeCommandDisplaySaveTests` already behave like a partial dry sweep for catalog commands. The #1176 work should consolidate their duplicate violation checks into reusable classification/reporting rather than invent a separate output-quality policy.
3. Universal commands `math`, `gm`, `debug`, `system_guardians`, and Saref/memory commands need explicit dry-sweep inclusion or an intentional diagnostic/fixture-scope note.
4. LocalTurn commands should be swept as preview/prompt surfaces only. The sweep must not commit real game mutations unless it uses a disposable session and reports it.
5. The first implementation target should be a shared command-output quality classifier used by the three display-save tests and by a new report artifact.
