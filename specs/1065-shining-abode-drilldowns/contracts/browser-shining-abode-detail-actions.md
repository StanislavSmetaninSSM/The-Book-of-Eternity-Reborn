# Browser Shining Abode Detail Actions Contract

## Purpose

This contract records the player-facing browser command-result affordance expected by #1065. It is not a new runtime/GM state contract. It constrains how existing Shining Abode command outputs expose read-only selected-detail actions through shared C# command-result metadata.

## Covered Commands

- `/shining_abode` and existing Russian aliases
- `/shining_politics`
- `/shining_faction_founding`
- `/shining_faction_realignment`
- `/shining_faction_leadership`
- `/shining_native_faction_discovery`
- `/shining_faction_investment`
- `/shining_project_support`
- `/shining_project_unsupport`
- `/shining_project_retirement`
- `/shining_gates_open`
- `/shining_gates_select`
- `/shining_gates_deselect`
- `/shining_gates_reroll`
- `/shining_incarnation_prepare`
- `/shining_relic_forge`
- `/shining_trade`
- `/shining_treasury`
- `/source_of_light`

## Action Shape

For each useful player-visible Shining row, the browser command result may expose a read-only `UiAction` with:

- a stable `Id` scoped to the surface and canonical row id;
- a Russian/in-world `Label` that makes clear the action inspects details;
- a command string routed through the existing Explorer command parser/service;
- a non-danger secondary style;
- `RequiresConfirmation = false`;
- no raw file path, raw JSON payload, hidden GM-only data, local write payload, or pending/control mutation payload.

## Selected Detail Result Shape

A selected-detail command must:

1. return a successful player-facing command result when the selected row is visible and available;
2. keep the detail focused on one Shining gate, core receipt, pending core action, trade lifecycle entry, resident project audit row, structure, faction, chronicle, pending political action, political resolution, treasury/source/resource row, forge context, or related Shining inspection row;
3. preserve safe linked context such as resident/project/faction/gate/resource references as readable text or safe follow-through actions when existing command authority supports it;
4. preserve existing guided local action forms and prompt/write paths for mutating Shining operations;
5. return a graceful unavailable result for missing, stale, hidden, or sparse ids;
6. avoid default `UiRawJsonBlock`, API/DTO/endpoint/protocol/debug language, raw slash-command leakage, `game_state/` paths, and local filesystem paths.

## Read-Only Boundary

The new detail actions must not create or modify:

- pending/control files under `game_state/control/`;
- local-turn write contracts;
- validation or normalizer behavior;
- afterlife runtime state schemas;
- GM prompts, examples, manifests, or daemon/launcher guidance.

If implementation proves one of those changes is required, #1065 must either update the required GM-facing docs/tests in the same PR or split that work into a tracked follow-up before closure.

## Existing Local Action Forms

Existing Shining gates, politics, faction, project support, incarnation, forge, trade, treasury, and source-of-light forms and prompt/write flows must continue to work. #1065 may add read-only inspection actions around those forms but must not replace, duplicate, or reimplement write authority in React.
