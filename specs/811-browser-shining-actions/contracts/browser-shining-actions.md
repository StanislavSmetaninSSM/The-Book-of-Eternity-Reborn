# Browser Contract: Shining Abode Actions

**Source Issue**: [#811](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/811)

## Browser Command Metadata

The browser command catalog must expose these supported mutating guided forms in the Shining Abode group:

| Command ID | Primary alias | Browser behavior |
| --- | --- | --- |
| `shining_native_faction_discovery` | `/shining_native_faction_discovery` | Prompt + local pending write |
| `shining_faction_investment` | `/shining_faction_investment` | Prompt + local pending write |
| `shining_project_support` | `/shining_project_support` | Prompt + local pending write |
| `shining_project_unsupport` | `/shining_project_unsupport` | Prompt + local pending write |
| `shining_project_retirement` | `/shining_project_retirement` | Prompt + local pending write |

Default menu/help labels must be player-facing Russian text. Raw API, DTO, endpoint, debug, `.json`, and `pending_` details are not part of the default contract.

## Prompt Contract

All five commands use the existing browser prompt session model:

- Opening a command returns a `RequiresInput` browser command result when options are valid.
- Opening outside Shining Abode or during a blocker returns a player-facing blocked result without a prompt.
- Submitting a prompt writes through `BrowserAfterlifeWriteService`.
- Mutating prompt sessions require the existing local UI write lock.

## Write Contract

Submissions must create exactly the existing Shining core action pending request shape written by the console. The feature does not add a browser-only action type or a new pending file.

Submit-time validation must re-check:

- current realm is Shining Abode,
- Shining Abode state is actionable,
- no blocking prepared package or local write conflict exists,
- no conflicting Shining core action is already pending,
- selected faction/project is still canonical and visible for the submitted action,
- current resource/cap/eligibility validation succeeds in `ShiningCoreActionRequestState`.

## Non-Contract Changes

This feature does not change:

- afterlife GM prompt instructions,
- Shining Abode response receipts/reports,
- validation manifest shape,
- afterlife documentation matrix,
- console command behavior.
