# Contract: Mortal Faction Authority

## Entry routes

| Route | Full `factionDataChanges` legal? | Receipt required? |
|---|---:|---:|
| Genuine new faction | Yes | Yes |
| First GM-authored mutation of legacy faction | Yes, as complete promotion | Yes |
| Already materialized faction | No | Existing receipt must be preserved |
| Untouched legacy load | No mutation carrier required | No |
| Client-owned projection only | No | Preserve existing state |

Newness and promotion are decided from validated pre-turn exact IDs, never from
`isNewFaction`, names, or prose.

## Mandatory full-carrier groups

The raw full carrier must include:

1. exact creation/promotion identity;
2. display profile: `name`, `description`, `image_prompt`, `factionColor`;
3. `purpose`, `currentAgenda`, and non-empty `principles[]`;
4. complete power/progression authority;
5. complete `governance` and `leadership`;
6. complete `memory`;
7. initial `scribeChronicle[]` when canonical history does not already exist;
8. all seven governed surfaces, even when empty;
9. exactly one complete Mortal materialization envelope.

Every existing production field remains subject to its current numeric, enum,
shape, identity, and English image-prompt rules.

## Raw minimal example

This abridged example demonstrates deliberate emptiness. Production examples
must include every existing required core field as documented in
`Rules/Block_21.txt`.

```json
{
  "factionId": null,
  "initialId": "temp-faction-wayfarer-watch",
  "isNewFaction": true,
  "name": "Wayfarer Watch",
  "description": "A small watch formed to keep one road safe.",
  "image_prompt": "weathered road wardens beneath a wooden watchtower",
  "factionColor": "#7B6852",
  "purpose": "Keep the old western road open.",
  "currentAgenda": "Repair the bridge before the spring thaw.",
  "principles": [
    "Every traveler receives warning before judgment."
  ],
  "memory": {
    "summary": "The watch formed after the bridge massacre.",
    "lastUpdatedTurn": 12,
    "enduringFacts": [
      "The first wardens were caravan survivors."
    ],
    "openThreads": [
      "The bridge attackers were never identified."
    ]
  },
  "governance": {
    "model": "Open moot",
    "decisionProcess": "Active wardens decide by simple majority."
  },
  "leadership": {
    "leadershipState": "vacant",
    "summary": "The founder died and no successor has been chosen.",
    "leaderNpcIds": []
  },
  "ranks": {
    "branches": []
  },
  "structuredBonuses": [],
  "resources": {
    "metaResources": [],
    "strategicGoods": []
  },
  "relations": [],
  "activeProjects": [],
  "completedProjects": [],
  "controlledTerritories": [],
  "customStates": [],
  "scribeChronicle": [
    "#12 - The Wayfarer Watch took responsibility for the western road."
  ],
  "isPlayerFaction": false,
  "isPlayerMember": false,
  "playerRank": null,
  "playerBranch": null,
  "playerStrategyDirective": null,
  "reputation": 0,
  "reputationDescription": null,
  "materialization": {
    "schemaVersion": 1,
    "materializationId": "fmat_wayfarer_watch_12",
    "factionType": "mortal_faction",
    "factionId": "temp-faction-wayfarer-watch",
    "materializedAtTurn": 12,
    "state": "complete",
    "capabilities": {
      "hasFormalHierarchy": false,
      "usesFactionResources": false,
      "maintainsRelations": false,
      "runsProjects": false,
      "holdsTerritoryOrInfluence": false,
      "supportsPlayerMembership": false,
      "usesCustomMechanics": false
    },
    "sections": {
      "hierarchy": {
        "state": "empty_by_design",
        "reason": "The watch has not established formal ranks."
      },
      "resources": {
        "state": "empty_by_design",
        "reason": "Wardens currently contribute supplies personally."
      },
      "relations": {
        "state": "empty_by_design",
        "reason": "No formal relation with another faction exists yet."
      },
      "projects": {
        "state": "empty_by_design",
        "reason": "The bridge repair has not been chartered as a faction project yet."
      },
      "territoryAndInfluence": {
        "state": "empty_by_design",
        "reason": "The watch protects a road but does not claim territorial control."
      },
      "playerMembership": {
        "state": "empty_by_design",
        "reason": "The player has no formal standing with the new watch."
      },
      "customStates": {
        "state": "empty_by_design",
        "reason": "The watch has no unique tracked mechanic."
      }
    }
  }
}
```

## Atomic canonical result

The validator requires one exact identity across:

- `faction_core.json`;
- a structure entry;
- a resource entry;
- relevant project rows or proven empty project surface;
- a custom-state entry;
- at least one chronicle entry;
- every location-control reference;
- every NPC affiliation reference.

An omitted structure/resource/custom entry is not deliberate emptiness. A
single incomplete member rejects the entire accepted turn and enters repair.

## Promotion

Promotion:

- preserves all validated historical core/sidecar/chronicle/reference data;
- adds missing semantic core and receipt;
- supplies exact empty carriers only where the target truly has no content;
- uses a dedicated command for any simultaneous gameplay mutation when one
  exists;
- may not overwrite another faction or unrelated history.

## Ordinary updates

Already materialized full objects are forbidden. Use:

- `FactionCoreChanges` for its six closed groups;
- `factionRankChanges` and `factionBonusChanges`;
- `factionResourceChanges`;
- `factionProjectUpdates` and `completeFactionProjects`;
- `factionCustomStateChanges`;
- `factionChronicleUpdates`;
- existing location-control authority;
- `NPCCoreChanges.factionAffiliationsToUpsert` for NPC membership.

The immutable receipt is never included in an update command.
