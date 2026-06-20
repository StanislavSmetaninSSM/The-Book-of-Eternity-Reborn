# Feature Specification: Player-authored Soul Identity

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1194

## User Story

As a player in afterlife play, I want to define how my soul identifies and appears so the GM can write scenes around the soul I chose instead of an empty generic entity.

## Scope

- Extend canonical `game_state/meta/soul_state.json` with an optional player-authored `soulFormDescription` string.
- Preserve existing `soulName` and `previousSoulNames` behavior.
- Let the player provide the initial soul form description during new game creation.
- Let the player change the soul form description later from the `/soul` / `/душа` screen without sending a GM turn.
- Show the current form description in player-facing soul/status views.
- Expose the field to browser read-side DTOs so browser UI can display it when frontend work catches up.
- Update GM-facing prompts/docs/examples so the GM reads and respects the player-authored identity.

## Non-Goals

- No mechanical bonuses, gender logic, pronoun engine, or validation of identity categories.
- No GM-authored mutation of the player identity field.
- No large browser UI redesign in this issue.

## Acceptance Criteria

1. A new game writes `soul_state.soulFormDescription` together with `soulName`.
2. `/душа` shows the current soul form description or clearly says it is not set.
3. `/душа` lets the player change the form description locally and persists it to `soul_state.json`.
4. Empty or whitespace-only form descriptions are rejected when setting the field.
5. Existing soul rename behavior and `previousSoulNames` history remain unchanged.
6. Canonical write sanitizers, strict state-file validation, and browser read DTOs preserve the new field.
7. GM-facing guidance states that `soulName` and `soulFormDescription` are player-authored identity and must be respected, not overwritten by normal GM output.

## Data Contract

`game_state/meta/soul_state.json`

```json
{
  "soulName": "Пепельная Искра",
  "soulFormDescription": "Женщина из теплого янтарного света с голосом живого человека и следами прежних смертных лиц.",
  "previousSoulNames": [],
  "currentRealm": "Chaos Sea"
}
```

The value is text-only roleplay context. The client normalizes it to a single trimmed line for safe JSON/UI rendering.
