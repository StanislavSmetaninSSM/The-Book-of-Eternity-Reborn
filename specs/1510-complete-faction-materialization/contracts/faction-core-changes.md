# Contract: `FactionCoreChanges`

## Response and file mapping

GM response field:

```text
factionCoreChanges
```

Mapped canonical command file:

```text
game_state/factions/faction_core.json
```

The root command array is consumed only after successful validation and
normalization.

## Closed command schema

```json
{
  "factionId": "faction_northern_guild",
  "reason": "The council changed its winter mandate.",
  "profile": {},
  "purposeAndPrinciples": {},
  "progressionAndPower": {},
  "governanceAndLeadership": {},
  "playerMembership": {},
  "relations": {}
}
```

Only `factionId`, `reason`, and the six named groups are legal. At least one
group is required. `factionId` is an exact permanent materialized Mortal faction
ID. `reason` is a non-empty authored explanation.

## Group schemas

### `profile`

```json
{
  "name": "absolute non-empty result",
  "description": "absolute non-empty result",
  "image_prompt": "production-valid English prompt",
  "factionColor": "#RRGGBB"
}
```

The group is complete: all four members are required when supplied.

### `purposeAndPrinciples`

```json
{
  "purpose": "absolute non-empty result",
  "currentAgenda": "absolute non-empty result",
  "principles": [
    "one or more unique non-empty strings"
  ]
}
```

### `progressionAndPower`

```json
{
  "level": 4,
  "experience": 120,
  "experienceForNextLevel": 200,
  "developmentArchetype": "existing supported value",
  "customArchetypePriorities": null,
  "powerProfile": {
    "military": 2,
    "economic": 4,
    "social": 3,
    "covert": 1,
    "logistics": 4,
    "stability": 3,
    "arcane_tech": 0,
    "exploration": 2
  }
}
```

All root fields and all eight power scales are required. The custom priorities
value is either explicit `null` or a complete existing object.

### `governanceAndLeadership`

```json
{
  "governance": {
    "model": "Elected council",
    "decisionProcess": "Five seats vote by simple majority."
  },
  "leadership": {
    "leadershipState": "collective",
    "summary": "Five charter masters rule jointly.",
    "leaderNpcIds": [
      "npc_charter_master_vesna"
    ]
  }
}
```

Both nested objects are required and replace their complete absolute canonical
values.

### `playerMembership`

```json
{
  "isPlayerFaction": false,
  "isPlayerMember": true,
  "playerRank": "Road Warden",
  "playerBranch": "western_road",
  "playerStrategyDirective": null,
  "reputation": 85,
  "reputationDescription": "Trusted road ally"
}
```

All members are required; nullable fields use explicit JSON `null`.

### `relations`

```json
{
  "entries": [
    {
      "targetFactionId": "faction_river_compact",
      "status": "allied",
      "description": "The factions share bridge tolls."
    }
  ]
}
```

`entries` is a complete absolute relation snapshot. Every target ID must resolve
exactly and cannot equal the source faction ID. Duplicate targets and partial
relation objects fail.

## Protected authority

The command rejects at any depth:

- `factionId` mutation, `initialId`, `initialFactionId`, `isNewFaction`;
- `materialization`;
- ranks, branches, structured bonuses;
- resources and strategic goods;
- active/completed projects;
- custom states;
- `scribeChronicle` or chronicle append payload;
- territory/location-control payload;
- NPC-affiliation payload;
- unknown members.

## Apply and failure behavior

The command uses absolute values, applies only supplied complete groups, and
preserves every unrelated field and sidecar. Governance/leadership updates are
written to structure authority; other groups are written to core authority.
The historical envelope is copied unchanged.

A valid command is removed from `faction_core.json`. An invalid command remains
visible, emits a stable `faction_core_changes_*` issue using
`mortal_faction:<id>`, and enters bounded repair.
