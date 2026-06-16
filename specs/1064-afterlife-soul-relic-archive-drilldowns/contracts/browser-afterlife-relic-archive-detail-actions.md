# Browser Afterlife Relic/Archive Detail Actions Contract

## Purpose

This contract records the player-facing browser command-result affordance expected by #1064. It is not a new runtime/GM state contract. It constrains how existing afterlife Soul Relic and Archive command outputs expose read-only selected-detail actions through shared C# command-result metadata.

## Covered Commands

- `/soul_relics` and existing Russian aliases
- `/soul_relic_equip`
- `/soul_relic_unequip`
- `/afterlife_archive`
- `/archive_candidates`
- `/archive_consultation`
- `/archive_project_fuel`

## Action Shape

For each useful player-visible row, the browser command result may expose a read-only `UiAction` with:

- a stable `Id` scoped to the surface and canonical row id;
- a Russian/in-world `Label` that makes clear the action inspects details;
- a command string routed through the existing Explorer command parser/service;
- a non-danger secondary style;
- `RequiresConfirmation = false`;
- no raw file path, raw JSON payload, hidden GM-only data, or local write payload.

## Selected Detail Result Shape

A selected-detail command must:

1. return a successful player-facing command result when the selected row is visible and available;
2. keep the detail focused on one relic/archive/candidate/fuel row;
3. preserve safe linked context such as Guardian/project/codex references as readable text or safe follow-through actions when existing command authority supports it;
4. return a graceful unavailable result for missing, stale, hidden, or sparse ids;
5. avoid default `UiRawJsonBlock`, API/DTO/endpoint/protocol/debug language, raw slash-command leakage, `game_state/` paths, and local filesystem paths.

## Read-Only Boundary

The new detail actions must not create or modify:

- pending/control files under `game_state/control/`;
- local-turn write contracts;
- validation or normalizer behavior;
- afterlife runtime state schemas;
- GM prompts, examples, manifests, or daemon/launcher guidance.

If implementation proves one of those changes is required, #1064 must either update the required GM-facing docs/tests in the same PR or split that work into a tracked follow-up before closure.

## Existing Local Action Forms

Existing equip/unequip/archive consultation/project fuel forms and prompt/write flows must continue to work. #1064 may add read-only inspection actions around those forms but must not replace, duplicate, or reimplement the write authority in React.
