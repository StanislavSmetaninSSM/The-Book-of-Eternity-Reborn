# NPC fixture fallback contract

**Source issue**: #928 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/928

## Contract Boundary

This contract defines a read-only display fallback for `/npc` when a local fixture has NPC journal data but does not have strict `game_state/npcs/npc_core.json` authority.

## Inputs

- Primary strict authority: `game_state/npcs/npc_core.json` with `npcs[]` or equivalent existing supported NPC core shapes.
- Fallback read-only authority: `game_state/npcs/npc_journals.json` with `npcJournals[]` entries containing:
  - `npcId` (stable identity when present)
  - `npcName` (player-facing display name when present)
  - `journalEntries[]` with optional `event`, `description`, `timestamp`, `relationshipChange`, `emotionalImpact`

## Output Requirements

- If strict NPC core has visible NPC entries, `/npc` keeps existing behavior.
- If strict NPC core is missing or empty and NPC journals exist, `/npc` returns player-facing Russian read-only fallback data:
  - NPC display name or stable id
  - latest readable journal description/event when present
  - journal entry count or last update summary when useful
  - clear wording that this is known journal/notes data, not a full actionable NPC roster
- If both strict NPC core and NPC journals are missing/empty, `/npc` may keep the existing empty-state copy.

## Safety Requirements

- Journal fallback does not authorize `/npc_talk`, `/npc_trade`, local-turn writes, trade, relationship mutation, activity completion, or accepted-turn NPC updates.
- Journal fallback does not require or create `pending_turn_snapshot` or signed accepted-turn authority.
- Journal fallback is player-facing display only and must not leak raw DTO/API/debug labels in default console/browser surfaces.
- Dynamic text from fixture/GM state must be escaped/sanitized before Spectre.Console markup and browser rendering paths.

## Verification Expectations

- Tests cover browser/read-only command result and console-visible NPC inspection or a shared projection helper used by both.
- Fixture validation remains clean without stale pending-turn snapshot files.
