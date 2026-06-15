# Mortal Read-Only Detail Drill-Down Audit

Source issue: #948 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/948

Audit date: 2026-06-16

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
| `quests` | `/quests` | `/квесты` | Interactive quest selector with active, soul, and history detail panels. | Generic bundle summary plus raw JSON. | Browser detail parity gap for an otherwise adequate console reference pattern. | P3 | Follow-up #1057. |
| `map` | `/map` | `/карта` | Visual map viewer plus summary. Location details moved to `/locations`. | Shared DTO emits `UiMapBlock` plus summary/raw state. | Covered. The command is map overview, not the location detail surface. | P3 | No new follow-up from #948. |
| `where_am_i` | `/where_am_i` | `/где_я` | Current-location detail panel with description, threats, time, and weather. | Generic current-location bundle with summary/raw state. | Low-risk parity gap, but not a repeated-entity drill-down surface. | P3 | Fold into #1057 only if browser reference-detail work needs current-location parity. |
| `factions` | `/factions` | `/фракции` | Interactive faction selector with detailed faction, projects, resources, bonuses, ranks, and chronicles. | Generic bundle summary plus raw JSON. | Browser detail parity gap for an otherwise adequate console reference pattern. | P3 | Follow-up #1057. |
| `skills` | `/skills` | `/навыки` | Interactive skill selector with active/passive detail panels and mastery context. | Generic bundle summary plus raw JSON. | Browser detail parity gap for an otherwise adequate console reference pattern. | P3 | Follow-up #1057. |
| `stats` | `/stats` | `/статы` | Player stat/derived-combat-stat panel. | Shared DTO shows status-derived stats and raw characteristics sidecars. | Covered. No repeated rich entity list requiring drill-down. | P3 | No new follow-up from #948. |
| `world_news` | `/world_news` | `/новости_мира` | Large all-in-one panels for events, threats, NPC activity, faction projects, flags, and progression. | Generic bundle summary plus raw JSON. | Confirmed command-specific drill-down gap. | P2 | Follow-up #1055. |
| `rival_threads` | `/rival_threads` | `/чужие_нити` | Rival soul thread list/detail flow exists through the console afterlife/mortal story helpers. | Generic bundle summary plus raw JSON. | Browser detail parity gap for rich thread entities. | P3 | Follow-up #1057. |
| `guardian_corrections` | `/guardian_corrections` | `/коррективы_хранителя` | Console renders the current-life correction record with budgets, claimants, contested slots, and scenario-core context. | Generic bundle summary plus raw JSON. | Browser detail parity gap for a structured current-life correction record. | P3 | Follow-up #1057. |
| `locations` | `/locations` | `/локации` | Interactive current/adjacent/discovered location selector with detailed location panels. | Shared DTO lists current, adjacent, and discovered/updated locations plus raw state. | Browser detail parity gap for an otherwise adequate console reference pattern. | P3 | Follow-up #1057. |
| `transport` | `/transport` | `/транспорт` | Interactive vehicle selector with per-vehicle health, location, capacity, actions, abilities, inventory, and vehicle-inventory management. | Before #948, generic bundle read only `world_map.transportRoutes` and `current_location.availableTransport`; it missed canonical `game_state/misc/vehicles.json`. | Small browser overview authority gap fixed in #948; remaining browser per-vehicle detail parity is larger. | P2 | Fixed vehicle overview in this branch. Follow-up #1057 for browser detail parity. |
| `effects` | `/effects` | `/эффекты` | Rich table-style view for active effects, wounds, custom states, stealth, and experience; falls back to visible status when structured effect files are absent. | Shared DTO builds effect summary rows, effect detail rows, visible-status fallback, and raw effect state. | Covered for #948. Per-effect navigation could be improved later, but the command is not raw-only or summary-only now. | P3 | No new follow-up from #948. |
| `combat` | `/combat` | `/бой` | Large all-in-one panel for player combat state, enemies, allies, actions, effects, wounds, and combat log. | Generic bundle summary plus raw JSON. | Confirmed command-specific drill-down gap. | P2 | Follow-up #1054. |
| `weather` | `/weather` | `/погода` | Time and weather detail panel. | Generic bundle summary plus raw JSON, but the rich surface is not a repeated entity list. | Covered for #948. | P3 | No new follow-up from #948. |
| `books` | `/books` | `/книги` | Books/document flow is handled by the dedicated document reading surface. | Shared DTO supports document shelf, read actions, and selected document detail. | Tracked separately. | P2 | Use #947. |
| `storage_access` | `/storage_access` | `/доступ_к_хранилищам` | Console renders access grants/shares/revokes with readable nested fields. | Generic bundle summary plus raw JSON. | Browser detail parity gap if access records become long, but lower priority than combat/news/interactions. | P3 | Follow-up #1057. |
| `interactions` | `/interactions` | `/взаимодействия` | Console expands nested player interaction records in one panel. | Generic bundle summary plus raw JSON. | Confirmed command-specific drill-down gap. | P2 | Follow-up #1056. |

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

## Follow-Up Issues Created

- #1054 - `/combat` / `/бой` enemy, ally, and combat-log detail drill-downs.
- #1055 - `/world_news` / `/новости_мира` event and sub-section detail drill-downs.
- #1056 - `/interactions` / `/взаимодействия` player/record detail drill-downs.
- #1057 - browser detail actions for reference-style mortal read-only commands:
  `/quests`, `/skills`, `/factions`, `/locations`, `/rival_threads`,
  `/guardian_corrections`, `/storage_access`, and `/transport`.

## Documentation And Contract Impact

The #948 branch changes only player-facing command-result rendering for
`/transport`, adds tests/source guards, and records audit/follow-up decisions.
It does not change GM-authored response fields, state schema, validation,
normalizer behavior, prompts, examples, or afterlife contracts.
