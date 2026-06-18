# Contract: Console Faction Detail Drill-Down Menu Sections

Source issue: #1086 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1086

## Contract Type

Read-only player-facing Console Client UI contract over existing faction state.

## Inputs

- Selected faction from `game_state/factions/faction_core.json`.
- Optional existing sidecar data from `game_state/factions/faction_resources.json`, `game_state/factions/faction_projects.json`, `game_state/factions/faction_structure.json`, `game_state/factions/faction_chronicles.json`, and existing custom/territory/strategy fields embedded in the selected faction object.
- Existing Shining faction read-only data only if the selected faction surface already loads it through current command-result/console paths.

## Outputs

- Selected faction summary remains visible.
- A player-facing section menu/action set is available beneath or after the selected faction detail when section data exists.
- Section detail views render Russian/in-world labels for available data, including representative resources/economics, chronicles, ranks/hierarchy, projects/operations, strategic state, territorial influence, or ledgers.
- Missing/sparse data returns useful Russian empty-state copy.
- Terminal/plain-text evidence can demonstrate the selected faction section choices.

## Invariants

- The feature is read-only. It must not write pending files, mutate faction JSON, submit prompt sessions, change validation/normalizer rules, or create new GM-authored state contracts.
- Default player-facing output must not expose raw JSON, local file paths, API, DTO, endpoint, debug, internal id, hidden faction, hidden chronicle, raw `strategicMemory`, or raw `resourceLedger` wording/content.
- Dynamic GM-authored/user-authored text must be escaped before Spectre.Console markup.
- Existing `/factions` overview and selected faction summary remain available.
- #1085 shared column alignment expectations remain preserved.

## Non-Goals

- No browser React UI implementation.
- No Shining politics write-flow or afterlife pending/control contract change.
- No new faction authoring schema.
- No mutating faction management actions.
- No reopening of #1085 unless tests discover a regression.

## Verification Obligations

- Focused RED/GREEN tests or source guards for section menu/action presence and representative section details.
- Focused visibility/safety test for hidden/raw/default output boundaries.
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- Spec Kit prerequisite check resolving `specs/1086-console-faction-drilldowns` using `SPECIFY_FEATURE_DIRECTORY=specs/1086-console-faction-drilldowns` while main's `.specify/feature.json` still points at the previous active feature.
- `git diff --check origin/main...HEAD` and added-line static/security scan.
- Console screenshot/terminal/plain-text capture evidence showing the new faction-detail menu/actions.
