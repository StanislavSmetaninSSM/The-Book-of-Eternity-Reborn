# Shining Abode Command Fixture Checklist

Source issue: #1097 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1097

This checklist documents the tracked reusable Shining Abode command-display save:

- Source archive: `FileSystemExample/game_session/saves/manual_saves/shining_abode_command_display_fixture.zip`
- Sidecar metadata: `FileSystemExample/game_session/saves/manual_saves/shining_abode_command_display_fixture_metadata.json`
- Internal save name: `Shining Abode Command Display Fixture (#1097)`
- Load workflow: copy the archive into a live `BookOfEternityClient/game_session/saves/manual_saves/` directory and load it through the normal save list, or let `ShiningAbodeCommandDisplaySaveTests` load the tracked archive into disposable roots.
- Fixture boundary: this is a Shining Abode afterlife display save, not the dedicated Chaos Sea #1096 fixture. The archive carries mandatory Chaos Sea bootstrap lore files under `lore/chaos_sea/` because afterlife validation expects those universal lore dependencies, but it does not carry Chaos Sea command-display state.

## Fixture Notes

- `SaveLoadService` excludes live `input/` and `game_state/control/pending_turn_snapshot*` artifacts from manual saves, so the fixture is an at-rest save.
- `game_state/meta/shining_abode_state.json` carries the primary Shining Abode surface: halls, factions, politics, treasury, trade, forge, incarnation gates, blessing cards, receipts, and Source of Light context.
- `game_state/meta/guardian_abode_residents.json` carries representative resident data for `/abode_residents`, `/resident_interaction`, and `/resident_transfer`.
- `game_state/meta/guardian_project_journal.json` carries Shining faction projects and the archive-project display target used by `/archive_project_fuel`; `game_state/meta/guardian_projects.json` is present as the empty canonical project registry for this idle manual-save fixture.
- Validation permits idle manual saves to resolve resident and soul-quest guardian references from the current stored guardian state only when there is no live input, pending snapshot, or current guardian mutation surface.

## Verification Evidence

Focused #1097 RED before the archive existed:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ShiningAbodeCommandDisplaySaveTests.NamedShiningAbodeCommandDisplaySave_IsDiscoverableLoadableValidAndRepeatable" --logger "console;verbosity=minimal"
```

Result: 0 passed / 1 failed / 0 skipped / 1 total. Intended failure: missing `shining_abode_command_display_fixture.zip`.

Latest focused #1097 verification on 2026-06-18:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ShiningAbodeCommandDisplaySaveTests" --logger "console;verbosity=minimal"
```

Result: 101 passed / 0 failed / 0 skipped / 101 total.

Broader afterlife/fixture/Explorer/Validation gate on 2026-06-18:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~FileSystemExampleAfterlifeStateExamplesTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~Validation" --logger "console;verbosity=minimal"
```

Result: 1780 passed / 0 failed / 0 skipped / 1780 total.

## Shining Abode Commands

| Command id | Representative invocation | Fixture data source | Expected visible data |
| --- | --- | --- | --- |
| shining_abode | `/shining_abode врата card_social` | `shining_abode_state.json`, `soul_state.json` | Shining Abode overview plus `Песнь Рассвета` gate card detail. |
| shining_abode | `/shining_abode проект faction_lanterns::project_dawn` | `shining_abode_state.json`, `guardian_project_journal.json` | `Проект Рассвета` project detail with faction and resource context. |
| shining_abode | `/shining_abode исход core_receipt_open` | `shining_abode_state.json` | `Врата открылись` receipt/history detail. |
| shining_politics | `/shining_politics фракция faction_lanterns` | `shining_abode_state.json` | `Дом Фонарей` faction detail, politics, relationships, and influence. |
| shining_politics | `/shining_politics хроника chronicle_dawn` | `shining_abode_state.json` | `Рассветный спор` political chronicle detail. |
| shining_politics | `/shining_politics ресурс ledger_sparks` | `shining_abode_state.json` | `Искры Света` ledger/resource detail. |
| shining_politics | `/shining_politics решение founding_receipt_dawn` | `shining_abode_state.json` | `Дом Рассвета` decision/receipt detail. |
| shining_faction_founding | `/shining_faction_founding` | `shining_abode_state.json`, `soul_state.json` | Clear founding prompt/status using available Shining faction state. |
| shining_faction_realignment | `/shining_faction_realignment` | `shining_abode_state.json` | Clear realignment prompt/status with visible faction choices or unavailable reason. |
| shining_faction_leadership | `/shining_faction_leadership` | `shining_abode_state.json`, `guardian_abode_residents.json` | Clear leadership prompt/status tied to visible faction/resident data. |
| shining_native_faction_discovery | `/shining_native_faction_discovery` | `shining_abode_state.json` | Clear native-faction discovery context or unavailable reason. |
| shining_faction_investment | `/shining_faction_investment` | `shining_abode_state.json` | Clear investment prompt/status with treasury/resource context. |
| shining_project_support | `/shining_project_support` | `shining_abode_state.json`, `guardian_project_journal.json` | Clear project-support prompt/status with visible project context. |
| shining_project_unsupport | `/shining_project_unsupport` | `shining_abode_state.json`, `guardian_project_journal.json` | Clear project-support removal prompt/status. |
| shining_project_retirement | `/shining_project_retirement` | `shining_abode_state.json`, `guardian_project_journal.json` | Clear project-retirement prompt/status. |
| shining_gates_open | `/shining_gates_open` | `shining_abode_state.json`, `soul_state.json` | Incarnation gate opening context with current blessing-card pool. |
| shining_gates_select | `/shining_gates_select card_memory` | `shining_abode_state.json` | Blessing-card selection context for the visible gate cards. |
| shining_gates_deselect | `/shining_gates_deselect card_social` | `shining_abode_state.json` | Blessing-card deselection context for the selected card. |
| shining_gates_reroll | `/shining_gates_reroll` | `shining_abode_state.json` | Gate reroll context with available replacements and remaining rerolls. |
| shining_incarnation_prepare | `/shining_incarnation_prepare` | `shining_abode_state.json`, `incarnation_world_setup.json` | New-life preparation status or clear unavailable reason. |
| shining_relic_forge | `/shining_relic_forge` | `shining_abode_state.json`, `soul_state.json` | Forge/relic context for available Shining resources. |
| shining_trade | `/shining_trade` | `shining_abode_state.json`, `afterlife_notifications.json` | Shining trade/market context and visible ready notification. |
| shining_treasury | `/shining_treasury` | `shining_abode_state.json` | Treasury/resource ledger context. |
| source_of_light | `/source_of_light` | `shining_abode_state.json` | Source of Light status and offering context. |

## Shared Afterlife Combat And Entity Commands

| Command id | Representative invocation | Fixture data source | Expected visible data |
| --- | --- | --- | --- |
| archive_consultation | `/archive_consultation хранитель guardian_azalia` | `soul_state.json`, `guardians.json` | Azalia consultation target and archive context. |
| archive_project_fuel | `/archive_project_fuel проект guardian_azalia::project_shining_archive_lighthouse` | `guardian_project_journal.json` | `Архивный маяк` project display target with useful project-fuel context; `guardian_projects.json` remains the empty canonical registry in this fixture. |
| afterlife_profiles | `/afterlife_profiles профиль player_soul` | `afterlife_entity_profiles.json`, `soul_state.json` | Player soul profile for `Пепельная Искра`. |
| afterlife_profiles | `/afterlife_profiles профиль resident_mirel` | `afterlife_entity_profiles.json`, `guardian_abode_residents.json` | `Мирель` resident profile, goals, relationships, and masks. |
| afterlife_threats | `/afterlife_threats угроза shining_oath_cell_fixture` | `afterlife_active_threats.json` | Visible `Тихая ячейка` threat and pressure/context fields. |
| afterlife_chronicles | `/afterlife_chronicles хроника chronicle_shining_silver_hall_oath` | `afterlife_chronicles.json` | `Серебряный Зал` chronicle detail and consequences. |
| afterlife_inbox | `/afterlife_inbox уведомление notif_shining_trade_ready_001` | `afterlife_notifications.json` | `Сияющая витрина` notification and trade context. |
| spiritual_conflict | `/spiritual_conflict обмен exchange_shining_oath_001` | `afterlife_spiritual_conflict_state.json` | Current conflict and `серебряная печать` exchange detail. |
| spiritual_combat_log | `/spiritual_combat_log итог recent_shining_oath_cell_001` | `afterlife_spiritual_conflict_state.json` | Recent combat result with `оттиск клятвы`. |
| spiritual_combat_help | `/духовный_бой` | Command help/static combat guidance | Player-facing spiritual combat guidance without debug/raw JSON. |
| spiritual_arts | `/spiritual_arts особое radiance_oath_cut` | `afterlife_entity_profiles.json`, `afterlife_spiritual_conflict_state.json` | `Разрез клятвы` special art and standard art details such as `Давление`. |
| spiritual_action | `/духовное_действие` | `afterlife_spiritual_conflict_state.json` | Clear spiritual-action prompt/status for the current Shining conflict. |

## Practical Universal Afterlife Preview Commands

| Command id | Representative invocation | Fixture data source | Expected visible data |
| --- | --- | --- | --- |
| help | `/help` | Command catalog/help builder | Normal player-facing help without debug-only leakage. |
| status | `/статус` | Core state and soul state | Current Shining Abode status and resources. |
| soul | `/душа` | `soul_state.json` | Soul name, realm, lives, feathers, archive, relics, and afterlife progression. |
| soul_relics | `/soul_relics реликвия relic_lantern_memory` | `soul_state.json` | `Фонарь Памяти` relic overview and detail target. |
| afterlife_archive | `/afterlife_archive запись archive_silver_hall_oath` | `soul_state.json` archive entries | `Серебряный Зал` archive record detail. |
| archive_candidates | `/archive_candidates кандидат candidate_shining_oath_trace` | `archive_candidate_manifest.json`, soul archive candidates | Candidate `оттиск клятвы` and archive decision context. |
| soul_quests | `/квесты_души` | `soul_quests.json` | Active soul quest context tied to Shining Abode state. |
| achievements | `/достижения` | `achievements.json` | Achievement overview for Shining Abode progress. |
| chronicle | `/хроника` | `character_chronicle.json`, story JSONL | Character/afterlife chronicle overview. |
| story | `/story` | `stories/shining_abode_command_display_fixture.jsonl` | Current story context without raw JSON. |
| behavior | `/поведение` | `player_behavior.json` | Behavior overview for the current soul. |
| lives | `/жизни` | `soul_state.json` | Past/current life summary. |
| feathers | `/перья` | `soul_state.json` | Ink feather count and afterlife resource context. |
| codex | `/кодекс` | `lore/codex_entries.json` | Lore/codex overview with Shining-relevant entries. |
| world_rules | `/правила_мира` | `lore/current_world/world_directives.json` | Current directives/rules overview. |
| gallery | `/галерея` | Session state | Gallery overview or clear no-image state. |
| validate | `/валидация` | `ValidationService` | Zero blocking validation issues after the save loads. |
| world_setup | `/настройка_мира` | Current realm/soul state | Clear in-world unavailable/status reason for setup in Shining Abode. |
