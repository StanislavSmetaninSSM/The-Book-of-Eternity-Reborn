# Data Model: Mortal Command Fixture Coverage

## Mortal Command Coverage Entry

- `commandId`: Catalog id, for example `inventory`.
- `mode`: `ReadOnly` or `LocalTurn`.
- `representativeCommand`: A command invocation suitable for manual preview.
- `detailCommands`: Optional invocations for supported detail surfaces.
- `fixtureFiles`: State/lore files that should contain representative data.
- `expectedVisibleData`: Human-readable description of what the user should see.
- `coverageStatus`: `covered`, `needs-data`, `needs-follow-up`, or `not-applicable`.

## Local Test Game Session

- Root: `BookOfEternityClient/game_session`
- Status: Ignored by git.
- Purpose: Manual preview state for console and browser clients.
- Required property: Valid enough that command screens render without blocking errors or malformed markup.

## Reusable Mortal Command Display Save

- Source archive: `FileSystemExample/game_session/saves/manual_saves/mortal_world_command_display_fixture.zip`
- Sidecar metadata: `FileSystemExample/game_session/saves/manual_saves/mortal_world_command_display_fixture_metadata.json`
- Internal save name: `Mortal World Command Display Fixture (#1095)`.
- Purpose: Tracked save/load-compatible package of the rich #1092 Mortal World command-display fixture.
- Load behavior: The tracked archive is the durable source. For manual console/browser QA, copy it into `BookOfEternityClient/game_session/saves/manual_saves/` and load it through the normal save list; recopy from the tracked source before repeated manual loads because the active `game_session` is replaced by the loader.
- Required property: Loading into a disposable root with clean-checkout tracked dependencies produces a Mortal World session that has zero blocking validation issues and renders the covered command set in browser and console.

## Reusable Chaos Sea Command Display Save

- Source archive: `FileSystemExample/game_session/saves/manual_saves/chaos_sea_command_display_fixture.zip`
- Sidecar metadata: `FileSystemExample/game_session/saves/manual_saves/chaos_sea_command_display_fixture_metadata.json`
- Internal save name: `Chaos Sea Command Display Fixture (#1096)`.
- Purpose: Tracked save/load-compatible package of representative Chaos Sea afterlife command-display fixture state.
- Load behavior: The tracked archive is the durable source. For manual console/browser QA, copy it into `BookOfEternityClient/game_session/saves/manual_saves/` and load it through the normal save list; recopy from the tracked source before repeated manual loads because the active `game_session` is replaced by the loader.
- Required property: Loading into a disposable root with clean-checkout tracked dependencies produces a Chaos Sea session that has zero blocking validation issues and renders every command available in that save in browser and console with player-facing data or a clear in-world unavailable reason.
- Fixture surfaces: `game_state/meta/soul_state.json`, `game_state/meta/guardians.json`, `game_state/meta/guardian_project_journal.json`, `game_state/meta/guardian_projects.json`, `game_state/meta/chaos_sea_guardian_politics.json`, `game_state/meta/afterlife_entity_profiles.json`, `game_state/meta/afterlife_active_threats.json`, `game_state/meta/afterlife_chronicles.json`, `game_state/meta/afterlife_spiritual_conflict_state.json`, `game_state/control/afterlife_notifications.json`, `game_state/control/archive_candidate_manifest.json`, and Chaos Sea lore files.
- Project display rule: `guardian_project_journal.json` supplies the reusable display target `project_archive_lighthouse_display_001`; `guardian_projects.json` intentionally contains no active tracker state because canonical `activeProjects` require a validated pre-turn tracker baseline that is stripped from manual saves.
- Unavailable-action rule: `/archive_project_fuel` is available as a display command in the save but returns a clear in-world unavailable reason until a validated active project exists.
- Scope boundary: Dedicated Shining Abode fixture data belongs to #1097; any Shining references in this save must be historical/contextual support for Chaos Sea output rather than the primary Shining command-display scenario.

## Fixture Data Surface

- A JSON or content file under the session root.
- May support several commands, for example `game_state/inventory/items.json` covers `/инв`, `/книги`, equipment actions, split/merge/drop, and trade context.
- Must avoid raw debug-only usefulness; data should include names, descriptions, relationships, and detail fields.
