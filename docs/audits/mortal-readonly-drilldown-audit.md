# Mortal Read-Only Detail Drill-Down Audit

Source issue: #948 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/948

Audit date: 2026-06-16

Latest refresh: 2026-06-24, issue #1268 -
https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1268

## Scope

This audit covers every Mortal World read-only command whose descriptor uses
`ExplorerCommandGroup.MortalWorld`, `ExplorerCommandMutationMode.ReadOnly`, and
`ExplorerCommandBrowserHandlerKind.MortalWorld`, then compares the shared browser
command-result builder with the matching console `ExplorerMode` handler.

Out of scope for this branch:

- NPC detail-section implementation, already tracked by #946.
- Books/document reading flow, already tracked by #947.
- Afterlife analogues, tracked separately by #949.
- A broad redesign of the browser client. The current browser direction remains a
  minimal command input plus command result surface.

## Classification Criteria

- `Covered` means the command already has a player-facing detail path or does
  not expose a rich repeated entity list.
- `Small fix in #948` means this branch made a focused correction without
  changing GM-authored contracts.
- `Follow-up` means the gap needs command-specific or framework-level detail
  design beyond a small audit branch change.
- `Tracked separately` means a confirmed gap belongs to an existing sibling
  issue and is intentionally not duplicated here.

Severity uses the project-wide audit scale: `P2` for serious UX/parity drift,
`P3` for lower-risk discoverability or test-guard gaps.

## Command Audit

| Command id | Primary alias | Russian alias | Console behavior | Browser behavior | Gap status | Severity | Decision |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `inventory` | `/inv` | `/инв` | Interactive inventory list with equipment, item selection, local equip/unequip, storage links, and item management flows. | Shared DTO shows inventory table, equipment/resources, raw sidecars, and equip/unequip actions. | Covered for #948. Document reading remains #947; broader item-bonus authority remains separate prior work. | P3 | No new follow-up from #948. |
| `npcs` | `/npc` | `/нпс` | Interactive NPC selector with rich NPC detail panels and action entry points. | Shared DTO uses NPC overview/section projection and journal fallback, but NPC drill-down sections are already a dedicated task. | Tracked separately. | P2 | Use #946. |
| `quests` | `/quests` | `/квесты` | Interactive quest selector with active, soul, and history detail panels. | Shared DTO keeps the overview and exposes read-only quest detail commands/actions for selected quest records. | Covered by #1057. | P3 | Implemented in #1057. |
| `map` | `/map` | `/карта` | Visual map viewer plus summary. Location details moved to `/locations`. | Shared DTO emits `UiMapBlock` plus summary/raw state. | Covered. The command is map overview, not the location detail surface. | P3 | No new follow-up from #948. |
| `where_am_i` | `/where_am_i` | `/где_я` | Current-location detail panel with description, threats, time, and weather. | Shared DTO renders the current location, description, features, faction control, active threats, recent events, world time, and weather summary, with raw sidecars kept as advanced diagnostics only. | Covered by #1121. | P3 | Implemented in #1121. |
| `factions` | `/factions` | `/фракции` | Interactive faction selector with detailed faction, projects, resources, bonuses, ranks, and chronicles. | Shared DTO keeps the overview and exposes read-only faction detail commands/actions for selected faction records. | Covered by #1057. | P3 | Implemented in #1057. |
| `skills` | `/skills` | `/навыки` | Interactive skill selector with active/passive detail panels and mastery context. | Shared DTO keeps the overview and exposes read-only skill detail commands/actions for selected skill records. | Covered by #1057. | P3 | Implemented in #1057. |
| `stats` | `/stats` | `/статы` | Player stat/derived-combat-stat panel. | Shared DTO shows status-derived stats and raw characteristics sidecars. | Covered. No repeated rich entity list requiring drill-down. | P3 | No new follow-up from #948. |
| `world_news` | `/world_news` | `/новости_мира` | Shared command-result renderer now preserves the world-news overview sections for events, location threats, NPC activity, faction projects, flags, and progression, and exposes typed detail commands for events, world flags, and progression entries. | Shared command-result renderer exposes the same overview sections plus non-mutating detail actions for events, world flags, and progression entries, with raw sidecars kept as secondary overview diagnostics. | Covered by #1055. | P2 | Implemented in #1055. |
| `rival_threads` | `/rival_threads` | `/чужие_нити` | Rival soul thread list/detail flow exists through the console afterlife/mortal story helpers. | Shared DTO keeps the overview and exposes read-only rival-thread detail commands/actions for selected thread records. | Covered by #1057. | P3 | Implemented in #1057. |
| `guardian_corrections` | `/guardian_corrections` | `/коррективы_хранителя` | Console renders the current-life correction record with budgets, claimants, contested slots, and scenario-core context. | Shared DTO keeps the overview and exposes read-only correction detail commands/actions for selected correction records. | Covered by #1057. | P3 | Implemented in #1057. |
| `locations` | `/locations` | `/локации` | Interactive current/adjacent/discovered location selector with detailed location panels. | Shared DTO lists current, adjacent, and discovered/updated locations, and exposes read-only location detail commands/actions. | Covered by #1057. | P3 | Implemented in #1057. |
| `transport` | `/transport` | `/транспорт` | Interactive vehicle selector with per-vehicle health, location, capacity, actions, abilities, inventory, and vehicle-inventory management. | Shared DTO reads canonical vehicles, keeps route/current-location overview data, and exposes read-only per-vehicle detail commands/actions. | Covered by #1057, with the #948 vehicle overview authority fix preserved. | P2 | Implemented in #1057. |
| `effects` | `/effects` | `/эффекты` | Rich table-style view for active effects, wounds, custom states, stealth, and experience; falls back to visible status when structured effect files are absent. | Shared DTO builds effect summary rows, effect detail rows, visible-status fallback, and raw effect state. | Covered for #948. Per-effect navigation could be improved later, but the command is not raw-only or summary-only now. | P3 | No new follow-up from #948. |
| `combat` | `/combat` | `/бой` | Shared command-result renderer now shows a combat overview, enemy/ally/log lists, typed detail commands, and individual enemy/ally/log detail panels. | Shared DTO now shows the same overview/list/detail content and exposes non-mutating detail actions for enemy, ally, and combat-log inspection, with raw sidecars kept as secondary diagnostics. | Covered by #1054. | P2 | Implemented in #1054. |
| `weather` | `/weather` | `/погода` | Time and weather detail panel. | Shared DTO renders absolute/set world time plus weather state, biome, season, temperature, wind, visibility, tendency, and mechanical effects, with raw sidecars kept as advanced diagnostics only. | Covered by #1121. | P3 | Implemented in #1121. |
| `books` | `/books` | `/книги` | Books/document flow is handled by the dedicated document reading surface. | Shared DTO supports document shelf, read actions, and selected document detail. | Tracked separately. | P2 | Use #947. |
| `storage_access` | `/storage_access` | `/доступ_к_хранилищам` | Console renders access grants/shares/revokes with readable nested fields. | Shared DTO keeps the overview and exposes read-only storage-access detail commands/actions for selected access records. | Covered by #1057. | P3 | Implemented in #1057. |
| `interactions` | `/interactions` | `/взаимодействия` | Shared command-result renderer preserves the interactions overview and exposes typed player and record detail commands for other-player interaction entries. | Shared command-result renderer exposes the same overview plus non-mutating player and record detail actions, with raw state kept as secondary overview diagnostics. | Covered by #1056. | P2 | Implemented in #1056. |

## Fix Applied In #948

The shared browser DTO builder for `transport` now reads
`game_state/misc/vehicles.json`, renders a player-facing vehicle table, preserves
the previous transport route/current-location summary, and still includes raw
state blocks for advanced inspection. This keeps `/транспорт` from regressing to
`Данные ещё не созданы` when canonical vehicle state exists.

Regression coverage:

- `ExplorerWebCommandServiceTests.ExecuteAsync_MortalReadOnlySummaries_ReadCanonicalStateKeys`
  now includes `/транспорт` with a canonical `vehicles[]` fixture.
- `MortalReadOnlyDrilldownAuditTests.MortalReadOnlyDrilldownAudit_CoversEveryMortalWorldReadOnlyDescriptor`
  keeps this audit artifact synchronized with the Mortal World read-only command
  descriptor set.

## Fix Applied In #1054

The shared Mortal World command-result builder for `/combat` / `/бой` now reads
`game_state/combat/enemies.json`, `game_state/combat/allies.json`, and
`game_state/combat/combat_log.json`, renders a preserved combat overview, and
adds player-facing drill-down commands/actions for one enemy, one ally, and one
combat-log entry. The console `/бой` path renders the same shared command-result
surface, so browser buttons and console typed commands stay semantically
equivalent without changing afterlife spiritual combat.

Regression coverage:

- `ExplorerWebCommandServiceTests` covers enemy, ally, and combat-log overview
  actions plus individual detail output without raw-JSON dependency.
- `ExplorerModeCommandTests` covers console discovery of the same typed detail
  commands and guards the console handler's shared DTO renderer path.

## Fix Applied In #1055

The shared Mortal World command-result builder for `/world_news` /
`/новости_мира` now reads the existing world-news sources used by the command:
`game_state/world/world_events.json`, current/world-map location threats,
`game_state/npcs/npc_activities.json`, `game_state/factions/faction_projects.json`,
`game_state/world/world_flags.json`, and `game_state/world/progression.json`.
The overview remains player-facing and sectioned, while events, world flags, and
progression entries receive typed detail commands/actions:

- `/новости_мира событие <метка>` / `/world_news event <selector>`
- `/новости_мира флаг <метка>` / `/world_news flag <selector>`
- `/новости_мира прогресс <метка>` / `/world_news progression <selector>`

The console handler now renders the same shared command-result surface as the
browser path, so typed console commands and browser actions inspect the same
canonical entries. Raw JSON remains only as secondary overview diagnostics; the
detail views render Russian player-facing blocks and do not require raw JSON.

Regression coverage:

- `ExplorerWebCommandServiceTests` covers overview actions for a rich
  world-news fixture, event detail output, a representative world-flag
  subsection detail, and progression detail output without raw-JSON dependency.
- `ExplorerModeCommandTests` covers console discovery of the same typed detail
  commands and guards the console handler's shared DTO renderer path.

## Fix Applied In #1056

The shared Mortal World command-result builder for `/interactions` /
`/взаимодействия` now reads `game_state/misc/player_interactions.json`, preserves
the interactions overview, and adds typed detail commands/actions for player
entries and nested interaction records:

- `/взаимодействия игрок <метка>` / `/interactions player <selector>`
- `/взаимодействия запись <метка>` / `/interactions record <selector>`

The console handler now renders the same shared command-result surface as the
browser path, so typed console commands and browser actions inspect the same
canonical entries. Raw state remains only as secondary overview diagnostics;
player and record detail views render Russian player-facing blocks and do not
require raw JSON.

Regression coverage:

- `ExplorerWebCommandServiceTests` covers rich player-interaction overview
  actions, one player-entry detail, and one nested record detail without
  raw-JSON dependency.
- `ExplorerModeCommandTests` covers console discovery of the same typed detail
  commands and guards the console handler's shared command-result renderer path.

## Fix Applied In #1057

The shared Mortal World command-result builder now keeps the existing overview
outputs and adds browser-safe read-only detail commands/actions for the
reference-style commands tracked by #1057:

- `/квесты квест <метка>` / `/quests quest <selector>`
- `/навыки навык <метка>` / `/skills skill <selector>`
- `/фракции фракция <метка>` / `/factions faction <selector>`
- `/локации локация <метка>` / `/locations location <selector>`
- `/чужие_нити нить <метка>` / `/rival_threads thread <selector>`
- `/коррективы_хранителя корректировка <метка>` /
  `/guardian_corrections correction <selector>`
- `/доступ_к_хранилищам хранилище <метка>` /
  `/storage_access storage <selector>`
- `/транспорт транспорт <метка>` / `/transport vehicle <selector>`

The browser service now preserves arguments for these read-only commands through
`ExplorerCommandCatalog`, so browser actions and typed command-result detail
paths inspect one selected record without depending on raw JSON. Raw JSON remains
only in the overview diagnostics already used by these command-result surfaces;
detail results render Russian player-facing blocks and back actions.

Regression coverage:

- `ExplorerWebCommandServiceTests` covers all eight affected commands for
  overview detail actions and selected-record detail output without raw-JSON
  dependency.
- `MortalReadOnlyDrilldownAuditTests` guards that the eight command descriptors
  remain Mortal World read-only browser commands that accept detail arguments.

## Fix Applied In #1121

The shared Mortal World command-result builder now replaces the generic bundle
outputs for `/where_am_i` / `/где_я` and `/weather` / `/погода` with dedicated
player-facing panels. `/where_am_i` unwraps `currentLocationData` and combines
the current location with features, faction control, active threats, recent
events, world time, and weather. `/weather` unwraps `setWorldTime` and
`weatherChange`, then renders biome, state, description, season, temperature,
wind, visibility, tendency, and mechanical effects. Raw JSON remains available
only as advanced diagnostics and is not required for the default browser view.

Regression coverage:

- `ExplorerWebCommandServiceTests.ExecuteAsync_WhereAmI_RendersLocationContextWithoutRawJson`
  covers current-location context without exposing raw JSON in player-default
  browser output.
- `ExplorerWebCommandServiceTests.ExecuteAsync_Weather_RendersDetailedTimeAndWeatherWithoutRawJson`
  covers rich time/weather context without exposing raw JSON in player-default
  browser output.

## Follow-Up Issues Created

- #1054 - `/combat` / `/бой` enemy, ally, and combat-log detail drill-downs
  (implemented by the #1054 branch).
- #1055 - `/world_news` / `/новости_мира` event and sub-section detail drill-downs
  (implemented by the #1055 branch).
- #1056 - `/interactions` / `/взаимодействия` player/record detail drill-downs
  (implemented by the #1056 branch).
- #1057 - browser detail actions for reference-style mortal read-only commands:
  `/quests`, `/skills`, `/factions`, `/locations`, `/rival_threads`,
  `/guardian_corrections`, `/storage_access`, and `/transport`
  (implemented by the #1057 branch).

## Documentation And Contract Impact

The #948 branch changes only player-facing command-result rendering for
`/transport`, adds tests/source guards, and records audit/follow-up decisions.
It does not change GM-authored response fields, state schema, validation,
normalizer behavior, prompts, examples, or afterlife contracts.

The #1055 branch changes only player-facing command-result rendering and command
argument preservation for the existing Mortal World `/world_news` /
`/новости_мира` read-only command. It does not change GM-authored response
fields, state schema, validation, normalizer behavior, prompts, examples, or
afterlife contracts.

The #1056 branch changes only player-facing command-result rendering and command
argument preservation for the existing Mortal World `/interactions` /
`/взаимодействия` read-only command. It does not change GM-authored response
fields, state schema, validation, normalizer behavior, prompts, examples, or
afterlife contracts.

The #1057 branch changes only player-facing command-result rendering and command
argument preservation for the existing Mortal World reference-style read-only
commands listed above. It does not change GM-authored response fields, state
schema, validation, normalizer behavior, prompts, examples, or afterlife
contracts.

## Audit Refresh In #1268

The #1268 pass re-ran the Mortal World command-display fixture through the
browser command-result surface and the shared console renderer. The console
client remains the completeness reference; browser output was adjusted where it
lost player-facing meaning, flattened detail, or leaked UI/debug wording.

### #1268 Command Status

| Command id | Primary alias | Status | #1268 notes |
| --- | --- | --- | --- |
| `inventory` | `/inv` | fixed | Item cards expose useful facts and direct open actions; book/document summaries no longer duplicate counts or unreadable reasons. |
| `npcs` | `/npc` | fixed | NPC overview cards expose direct profile actions, merchant affordances, relationship values, and no longer use generic technical summaries. |
| `quests` | `/quests` | fixed | Plot-outline entries render as individual quest cards; reference-bundle section summaries are now quest-specific. |
| `map` | `/map` | ok / follow-up | Read-only map summary distinguishes created locations from planned exits. Rich full-screen map interaction remains tracked separately by #1264. |
| `where_am_i` | `/where_am_i` | fixed | Current-location difficulty profiles now split into localized facts such as combat, environment, social, and exploration danger instead of one glued line. |
| `factions` | `/factions` | fixed | Faction cards include ranks, branches, chronicles, resources, and no duplicate self-cards; reference summaries are faction-specific. |
| `skills` | `/skills` | fixed | Skill overview/detail output localizes protocol values and is covered by representative detail-command anti-pattern tests. |
| `stats` | `/stats` | fixed | Computed characteristics and temporary modifiers render as localized sections/cards, not one-line key dumps. |
| `world_news` | `/world_news` | fixed | Overview and details use player-facing summaries; visibility enums are localized and service-style action guidance was removed. |
| `rival_threads` | `/rival_threads` | fixed | Reference summaries are rival-thread specific and classifier guards against generic reference placeholders. |
| `guardian_corrections` | `/guardian_corrections` | fixed | Reference summaries are correction-specific and detail commands remain covered by read-only argument tests. |
| `locations` | `/locations` | fixed / follow-up | Overview/detail location facts are separated from narrative and storages are opened through a separate detail surface; richer browser map UX remains #1264. |
| `transport` | `/transport` | fixed | Transport cards split state, location, route, capacity, and durability; detail command is covered by representative anti-pattern tests. |
| `effects` | `/effects` | fixed | Effect summaries no longer tell the player about UI mechanics and visible fallback states remain readable. |
| `combat` | `/combat` | fixed | Combatants omit empty `не указано` state/intent facts, health/poise are labeled, and combat log markdown/check notation is normalized. |
| `weather` | `/weather` | ok | The current fixture contains only a description and stable tendency; renderer keeps the player-facing weather summary and preserves clock formatting. |
| `books` | `/books` | fixed | Shelf summaries show source/access/excerpt without repeated `N записей` prefixes and without duplicated unreadable reasons. |
| `storage_access` | `/storage_access` | fixed | Reference summaries are storage-specific and storage detail commands are covered by representative anti-pattern tests. |
| `interactions` | `/interactions` | fixed | Player cards use real linked interaction summaries; record summaries no longer say that full details open elsewhere. |

### #1268 Regression Guards

- `BrowserCommandPresentationAuditTests` covers every Mortal World read-only
  command listed above and 15 representative detail commands.
- `MortalCommandDisplaySaveTests` loads
  `mortal_world_command_display_fixture.zip`, validates it, executes all
  Mortal World command descriptors/aliases plus practical universal previews,
  and renders each result through `ExplorerCommandResultConsoleRenderer`.
- `ConsoleCommandOutputQualityClassifier` now fails on raw JSON/technical
  markers and on player-facing UI instruction copy such as `Полные сведения`,
  `Подробности открываются`, `Что уже отмечено в книге`, and
  `Известные записи этого раздела`.

Contract impact for #1268: player-facing command-result rendering and tests
only. The pass does not change GM-authored response fields, state schema,
validation, normalizer behavior, prompts, examples, or afterlife contracts.
