# Chaos Sea Command Fixture Checklist

Source issue: #1096 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1096

This checklist documents the tracked reusable Chaos Sea command-display save:

- Source archive: `FileSystemExample/game_session/saves/manual_saves/chaos_sea_command_display_fixture.zip`
- Sidecar metadata: `FileSystemExample/game_session/saves/manual_saves/chaos_sea_command_display_fixture_metadata.json`
- Internal save name: `Chaos Sea Command Display Fixture (#1096)`
- Load workflow: copy the archive into a live `BookOfEternityClient/game_session/saves/manual_saves/` directory and load it through the normal save list, or let `ChaosSeaCommandDisplaySaveTests` load the tracked archive into disposable roots.
- Fixture boundary: this is a Chaos Sea afterlife display save, not the dedicated Shining Abode #1097 fixture.

## Fixture Notes

- `SaveLoadService` excludes live `input/` and `game_state/control/pending_turn_snapshot*` artifacts from manual saves, so the fixture is an at-rest save.
- `game_state/meta/guardian_project_journal.json` carries the display target `project_archive_lighthouse_display_001` for `/guardian_projects проект guardian_azalia::project_archive_lighthouse`.
- `game_state/meta/guardian_projects.json` intentionally has no active tracker authority because `activeProjects` require a validated pre-turn tracker baseline.
- `/archive_project_fuel` renders the journal-backed project context plus a clear in-world unavailable reason until a validated active project exists; actual write paths still require canonical active project authority.

## Verification Evidence

Latest focused #1096 verification on 2026-06-18:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ChaosSeaCommandDisplaySaveTests" --logger "console;verbosity=minimal"
```

Result: 96 passed / 0 failed / 0 skipped / 96 total.

## Chaos Sea Commands

| Command id | Representative invocation | Fixture data source | Expected visible data |
| --- | --- | --- | --- |
| chaos_sea | `/море_хаоса` | `soul_state.json`, `guardians.json`, Chaos Sea lore | Realm overview for Море Хаоса, current soul/guardian context, and navigation summary. |
| guardians | `/guardians хранитель guardian_azalia` | `guardians.json` | Azalia guardian profile, relationship, abode, lore, mood, and detail action. |
| abode_power | `/abode_power запись power_azalia_archive_oath_001` | `guardians.json` abode power history | `Клятва архивного света` power-history detail and visible reason/summary. |
| guardian_projects | `/guardian_projects проект guardian_azalia::project_archive_lighthouse` | `guardian_project_journal.json`; empty canonical `guardian_projects.json` | Journal-backed `Архивный маяк` project display target without active tracker authority. |
| guardian_politics | `/политика_хранителей` | `chaos_sea_guardian_politics.json`, `guardians.json` | Azalia/Seret relationship and visible Chaos Sea political context. |
| abodes | `/abodes обитель abode_azalia` | `guardians.json` abode objects and navigation | `Шелковый Архив` detail with guardian, anchor, description, and current navigation. |
| gacha | `/гача` | `soul_state.json`, `guardians.json` | Clear Chaos Sea local-turn gacha preview or unavailable reason using current feathers/guardian state. |
| abode_offering | `/подношение_обители` | `soul_state.json`, `guardians.json` | Clear offering prompt/unavailable state tied to Azalia's abode and available resources. |
| found_guardian_mantle | `/учредить_хранителя` | `soul_state.json`, `guardians.json` | Clear in-world status for founding a new guardian mantle in the current Chaos Sea state. |
| guardian_trade | `/торговля_хранителя` | `guardians.json`, afterlife notifications | Azalia trade context or clear unavailable reason without raw control-file details. |
| guardian_social | `/общение_хранителя` | `guardians.json` | Azalia social/interaction context or clear unavailable reason. |
| abode_residents | `/обитатели_обители` | `guardians.json` and empty/missing resident state | Clear resident overview/unavailable reason for the current abode. |
| resident_interaction | `/общение_резидента` | Current abode/resident state | Clear unavailable reason when no resident target is present. |
| resident_transfer | `/переход_резидента` | Current abode/resident state | Clear unavailable reason when no transfer target is present. |
| soul_relic_equip | `/экипировать_реликвию` | `soul_state.json` | Soul relic equipment context for `Зеркало Пепельной Искры` or clear already-equipped state. |
| soul_relic_unequip | `/снять_реликвию` | `soul_state.json` | Soul relic unequip context or clear unavailable/already-current state. |

## Afterlife Combat And Entity Commands

| Command id | Representative invocation | Fixture data source | Expected visible data |
| --- | --- | --- | --- |
| archive_consultation | `/archive_consultation хранитель guardian_azalia` | `soul_state.json`, `guardians.json` | Azalia consultation target and free archive entry context. |
| archive_project_fuel | `/archive_project_fuel проект guardian_azalia::project_archive_lighthouse` | `guardian_project_journal.json`, empty canonical `guardian_projects.json` | `Архивный маяк` context plus clear unavailable reason because no validated active project exists. |
| afterlife_profiles | `/afterlife_profiles профиль player_soul` and `/afterlife_profiles профиль guardian_azalia` | `afterlife_entity_profiles.json` | Player soul and Azalia profiles with goals, arts, masks, and relationships. |
| afterlife_threats | `/afterlife_threats угроза chaos_soul_hunter_pack_example` | `afterlife_active_threats.json` | Visible `Стая охотников` threat and pressure/context fields. |
| afterlife_chronicles | `/afterlife_chronicles хроника chronicle_chaos_black_tide_example` | `afterlife_chronicles.json` | Visible `Черный прилив` chronicle entry and consequences. |
| afterlife_inbox | `/afterlife_inbox уведомление notif_guardian_trade_ready_001` | `afterlife_notifications.json` | Guardian trade notification mentioning Azalia's showcase/context. |
| spiritual_conflict | `/spiritual_conflict обмен exchange_chaos_hunter_001` | `afterlife_spiritual_conflict_state.json` | Current conflict and `зеркальная защита` exchange detail. |
| spiritual_combat_log | `/spiritual_combat_log итог recent_conflict_hunter_pack_044` | `afterlife_spiritual_conflict_state.json` | Recent combat outcome where the hunters retreated. |
| spiritual_combat_help | `/духовный_бой` | Command help/static combat guidance | Player-facing spiritual combat guidance without debug/raw JSON. |
| spiritual_arts | `/spiritual_arts особое ash_mirror_guard` | `afterlife_entity_profiles.json`, `afterlife_spiritual_conflict_state.json` | `Зеркальная защита` special art and standard art details such as `Давление`. |
| spiritual_action | `/духовное_действие` | `afterlife_spiritual_conflict_state.json` | Clear spiritual-action prompt/unavailable state for the current conflict. |

## Practical Universal Afterlife Preview Commands

| Command id | Representative invocation | Fixture data source | Expected visible data |
| --- | --- | --- | --- |
| help | `/help` | Command catalog/help builder | Normal player-facing help without debug-only `/debug` leakage. |
| status | `/статус` | Core state and soul state | Current Chaos Sea status and resources. |
| soul | `/душа` | `soul_state.json` | Soul name, realm, lives, feathers, archive, relics, and afterlife progression. |
| soul_relics | `/реликвии` | `soul_state.json` | `Зеркало Пепельной Искры` relic overview and detail target. |
| afterlife_archive | `/afterlife_archive запись archive_black_tide_oath` | `soul_state.json` archive entries | `Черный прилив` archive record detail. |
| archive_candidates | `/archive_candidates кандидат candidate_hunter_echo` | `archive_candidate_manifest.json`, `afterlife_archive_candidates.json` | Candidate `эхо охотников` and archive decision context. |
| soul_quests | `/квесты_души` | Empty `soul_quests.json` | Clear no-active-soul-quest state. |
| achievements | `/достижения` | Achievements state | Achievement overview or clear no-achievement state. |
| chronicle | `/хроника` | Chronicle state | Character/afterlife chronicle overview. |
| story | `/story` | Narrative/story state | Current story context without raw JSON. |
| behavior | `/поведение` | Player behavior state | Behavior overview or clear unavailable state. |
| lives | `/жизни` | `soul_state.json` | Past/current life summary. |
| feathers | `/перья` | `soul_state.json` | Ink feather count and afterlife resource context. |
| codex | `/кодекс` | Lore/codex state | Lore/codex overview or clear no-entry state. |
| world_rules | `/правила_мира` | World directive/lore state | Current rules/directives overview. |
| gallery | `/галерея` | Gallery state | Gallery overview or clear no-image state. |
| validate | `/валидация` | `ValidationService` | Zero blocking validation issues after the save loads. |
| world_setup | `/настройка_мира` | Current realm/soul state | Clear in-world unavailable/status reason for setup in Chaos Sea. |
