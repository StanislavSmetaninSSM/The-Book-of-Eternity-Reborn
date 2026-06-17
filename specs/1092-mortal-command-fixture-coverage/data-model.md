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

## Fixture Data Surface

- A JSON or content file under the session root.
- May support several commands, for example `game_state/inventory/items.json` covers `/инв`, `/книги`, equipment actions, split/merge/drop, and trade context.
- Must avoid raw debug-only usefulness; data should include names, descriptions, relationships, and detail fields.
