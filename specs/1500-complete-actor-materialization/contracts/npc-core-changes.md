# Contract: NPCCoreChanges v1

**Source issue**: [#1500](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500)

## Scope

`NPCCoreChanges` is a Mortal-only, non-carrier response command mapped to
`game_state/npcs/npc_core.json`. It closes supported ordinary-existing core
mutation gaps without permitting a complete-object resend or generic JSON
Patch. It does not change any Chaos Sea or Shining Abode contract.

The command is validated before normalization. A valid command is applied to
every unambiguous canonical mirror of the exact actor and then consumed. An
invalid command remains present for repair and does not partially mutate the
actor.

## Shape

```json
{
  "NPCCoreChanges": [
    {
      "NPCId": "exact-existing-permanent-id",
      "reason": "non-empty in-world/mechanical reason",
      "profile": {
        "worldview": "absolute replacement from the current story"
      }
    }
  ]
}
```

Include only mutation groups that actually change. Unused optional groups are
omitted; a present empty object or array is runtime-invalid rather than a
template placeholder.

## Entry invariants

1. `NPCId` is required and identifies one exact existing permanent actor.
   Names, `initialId`, missing/new/stale targets, case-variant ambiguity, and
   divergent canonical copies are invalid.
2. `reason` is required and non-empty. At least one allowed mutation group is
   present and non-empty.
3. Values are absolute resulting values. Arithmetic expressions, prose-derived
   math, and arbitrary paths are invalid.
4. Unknown members are rejected recursively.
5. Protected fields include identity, name, materialization, inventory,
   equipment, skills/mastery, relationships/locks, journals/memory,
   goals/quests, activities, masks, custom states, and teacher/trade capability
   state. Existing dedicated commands retain authority for those domains.

## Allowed groups

### `profile`

Closed members: `worldview`, `race`, and `history`. Each supplied value is an
absolute replacement.

### `location`

Both members are required together. Exactly one authority branch is used:

- known permanent location: non-empty `currentLocationId` and null
  `initialLocationId`;
- same-turn new location: null `currentLocationId` and exact same-turn
  `initialLocationId`.

### `progression`

If any of `level`, `experience`, or `experienceForNextLevel` changes, all three
form one coherent non-negative tuple. `progressionType` is an absolute result.
A role/progression transition that requires synchronization includes
`lastPlayerXPValueOnSync`.

### `characteristicValues`

Values are finite numbers. Keys already belong to the actor or explicit
current-world characteristic authority. The command never infers or publishes a
universal vocabulary, carrying formula, point grant, or class allocation.

### `factionAffiliationsToUpsert`

Each entry uses exact existing faction identity and the complete affiliation
shape already enforced by the NPC validator. Faction hierarchy commands are not
membership commands.

### `fateCardsToAdd`

Each card reuses full Fate Card validation, has a unique new ID, and starts with
`isUnlocked=false`. Nested active/passive skills, Combat Actions, and effects pass
the same production validator as a complete canonical NPC before any reduction;
every effect includes required `value` and type-specific duration/poise fields.

### `fateCardIdsToRemove`

Each ID resolves to a validated pre-turn locked/unrealized card. Unlocked cards
are permanent development history and cannot be removed. Unlocking an existing
card remains `NPCFateCardUnlocks`.

## Reduction guarantees

- Resolve one logical actor from validated pre-turn/current permanent identity.
- Reject malformed, non-object, or duplicate-member current `npc_core.json`
  authority through a blocking structured issue before command materialization.
- Reject missing, ambiguous, case-variant, or divergent mirrors.
- Preserve every actor-owned field in historical full carriers. Inventory and the
  materialization envelope retain their narrower dedicated continuity diagnostics;
  every other domain changes only through its exact dedicated command.
- Apply only named allowed subfields to every canonical mirror.
- Preserve all siblings and the historical materialization envelope.
- Consume `NPCCoreChanges` only after successful reduction.
- Run ordinary full-object validation on the resulting canonical state.
- Leave invalid commands unconsumed so bounded repair can address them.
