# NPC Detail Drill-Down Contract

**Source issue**: #946 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/946

## Purpose

Define the player-facing read-only contract for opening focused detail sections from a selected Mortal World NPC without changing NPC mutation authority.

## Scope

In scope:

- Existing `/npc` / `/нпс` selected-NPC inspection.
- Console second-level section navigation or equivalent focused detail flow.
- Browser command-result/detail metadata or equivalent read-only C# DTO surface.
- Player-facing Russian labels, summaries, empty-state reasons, and back/overview affordances.

Out of scope:

- `/npc_talk`, `/npc_trade`, local-turn writes, pending action files, accepted-turn NPC update contracts, GM-authored schema changes, afterlife/Chaos Sea/Shining Abode contracts.

## Section Categories

Implementations should expose populated categories only:

| Section | Data sources already used by overview | Required player-facing behavior |
| --- | --- | --- |
| Overview | `npc_core.json` plus display name/summary fields | Preserve the existing full summary. |
| Thoughts / journal | `npc_journals.json`, `npc_interaction_journal.json` | Focused readable entries with count/latest hints. |
| Personal quests | `npc_goals.json` quest-like entries | Quest title/status plus objectives, rewards, and failure consequences when present. |
| Activities | `npc_activities.json` | Current/completed activity details and status/progress hints. |
| Relationships / locks | `npc_relationships.json` | Relationship values, caps, locks, and breakthrough hints already shown in overview, focused by section. |
| Skills / effects / inventory / equipment | `npc_skills.json`, `npc_effects.json`, `npc_inventory.json` | Separate focused mechanical sections when data exists. |
| Memory / masks / fate / custom states | `npc_memory.json`, `npc_masks.json`, `npc_fate_cards.json`, `npc_custom_states.json` | Focused lore/state sections when present; debug-only raw fields remain behind debug/advanced mode. |

## Safety Rules

- Default player-facing output must not expose raw JSON, DTO names, endpoint names, `/api`, debug labels, or unescaped dynamic text.
- Empty categories are omitted unless an existing disabled-entry pattern is reused with a clear player-facing reason.
- Section data is read-only. It does not authorize social/trade mutations or local-turn writes.
- Browser/React presentation must consume C# DTOs/command results and must not duplicate gameplay authority.
- If browser cannot open a focused detail in this slice, a linked follow-up issue must name the exact missing browser affordance before #946 is closed.

## Verification Obligations

- Tests include at least one rich NPC with journal/thought entries, one personal quest with objectives/rewards/failure consequences, and one activity.
- Console evidence proves a selected NPC has a second-level section affordance and focused sections.
- Browser evidence proves equivalent command-result/detail affordances or a linked follow-up.
- Existing #928 journal fallback remains read-only and green.
