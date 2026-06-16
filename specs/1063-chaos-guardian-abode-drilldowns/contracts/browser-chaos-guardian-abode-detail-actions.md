# Contract: Browser Chaos Sea Guardian/Abode Detail Actions

Source issue: #1063 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1063
Origin audit: #949 / `docs/audits/afterlife-drilldown-audit.md` AFD-001.

## Purpose

Browser command results for Chaos Sea Guardian/Abode overview commands expose safe read-only detail actions that invoke shared C# command handling. React presents the returned actions/results; it does not own Guardian/Abode selection or gameplay rules.

## Covered Commands

- `/guardians` / `/хранители`
- `/abodes` / `/обители`
- `/abode_power` / `/сила_обители`
- `/guardian_projects` / `/проекты_хранителей`
- Related read-only entry links from Guardian/Abode local systems when the implementation can reuse the same read-only command/result path.

## Overview Result Requirements

For canonical seeded data, each covered overview result must:

1. Complete with `CommandExecutionState.Completed`.
2. Preserve existing player-facing overview blocks/tables/sections.
3. Include stable secondary `UiAction` detail affordances for representative listed entities/entries.
4. Use player-facing Russian labels and avoid default raw/API/DTO/debug/path copy.
5. Keep `RequiresConfirmation=false` because detail actions are read-only.

## Selected Detail Result Requirements

For a selected id/entry, each implemented detail command must:

1. Complete with a focused detail result for one Guardian, Abode, power entry/section, or Guardian project.
2. Include a player-facing title and meaningful detail text from existing canonical data.
3. Avoid `UiRawJsonBlock` as default output and avoid `game_state/`, `API`, `DTO`, `endpoint`, `debug`, and local path wording.
4. Return a graceful in-world unavailable/unknown message for missing ids, sparse fixtures, or hidden/unavailable entries.
5. Remain read-only: no pending/control writes, no local-turn state mutations, no write service calls.

## Action Shape

Recommended shape, adapting exact ids/grammar to existing project conventions:

```text
Id: guardians-detail-<guardianId>
Label: Подробно: <Guardian display name>
Command: /guardians хранитель <guardianId>
Style: Secondary
RequiresConfirmation: false
```

Equivalent stable ids/subcommands are acceptable when tests document the final grammar and the command parser handles aliases consistently.

## Out-of-Scope Contract Changes

This contract does not authorize:

- New or renamed afterlife JSON schema fields.
- New pending/control files.
- Validation or normalizer behavior changes.
- GM prompt/example/manifest changes unless an implementation finding proves a runtime/GM contract changed.
- Mutating Guardian trade/social/resident flows.
- React-side gameplay authority.

If any of those become necessary, create/link a focused follow-up issue or update the required GM-facing docs/tests in the same PR.
