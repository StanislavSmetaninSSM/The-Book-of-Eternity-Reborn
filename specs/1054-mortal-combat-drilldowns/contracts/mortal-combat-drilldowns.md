# Contract: Mortal `/combat` / `/бой` Read-Only Drill-Downs

Source issue: #1054 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1054

## Command aliases

- `/combat`
- `/бой`

## Authority

The feature reads existing Mortal World combat state only:

- `game_state/combat/enemies.json`
- `game_state/combat/allies.json`
- `game_state/combat/combat_log.json`
- Existing player/status/effect files may be read only if the current `/бой` overview already uses them or a focused test proves they are necessary for player-facing context.

No new runtime-state file, pending/control file, validation rule, GM response field, or afterlife contract is introduced by this feature.

## Required player-facing surfaces

The ordinary/default command output must include or expose:

1. A preserved Mortal combat overview for quick reading.
2. A player-facing enemy list and a way to inspect at least one enemy detail.
3. A player-facing ally list and a way to inspect at least one ally detail.
4. A player-facing combat-log list and a way to inspect at least one log-entry detail.

"Expose" can mean existing command-result block/action metadata, an existing console selection affordance, or another established project pattern. Do not invent a broad frontend gameplay handler in React when the C# command-result path can carry the detail affordance.

## Player-facing copy boundary

Default output must use Russian/in-world terminology and must not require the player to read raw JSON, raw canonical enum values, state file paths, or technical labels such as `DTO`, `API`, `endpoint`, or `debug`.

Existing raw diagnostic sidecars may remain only where the current architecture already exposes them as advanced/raw details; they are not sufficient to satisfy this contract.

## Console/browser parity

Console and browser surfaces may differ visually, but they must be semantically equivalent:

- Both must let a player discover that enemy, ally, and log entry inspection exists.
- Both must provide enough player-facing detail for the same canonical entities.
- If one frontend cannot receive the full interaction shape safely in this slice, the implementation must document the narrower gap and create/link a follow-up issue before merge.

## Safety

GM-authored text is untrusted. Any text rendered through Spectre.Console markup or browser HTML must follow existing escaping/sanitization patterns.

## Out of scope

- Afterlife spiritual combat commands and files.
- New combat write actions.
- New GM-authored response fields or validation contracts.
- #1055 `/world_news`, #1056 `/interactions`, #1057 reference-command browser detail actions, and #949 afterlife drill-down audit.
