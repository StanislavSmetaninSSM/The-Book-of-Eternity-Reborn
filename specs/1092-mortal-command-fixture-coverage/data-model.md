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

## Fixture Data Surface

- A JSON or content file under the session root.
- May support several commands, for example `game_state/inventory/items.json` covers `/инв`, `/книги`, equipment actions, split/merge/drop, and trade context.
- Must avoid raw debug-only usefulness; data should include names, descriptions, relationships, and detail fields.
