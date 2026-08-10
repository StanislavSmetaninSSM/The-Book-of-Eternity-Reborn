# Data Model: Authoritative Saref Story Materialization

## Authority map

| Fact | Authoritative owner | Derived/consumer surfaces | Forbidden alternate owner |
|---|---|---|---|
| Fixed cast, quest definitions, rewards, reveal routes | Packaged Saref Story Catalog | GM compact index/full packages, validators, projectors | Mutable save files, prose inference, user presets |
| Catalog version used by a game | `main_story_saref_state.json.catalogBinding` | Validation, prompt context, receipts | Runtime-selected latest version, GM rewrite |
| Fixed quest progress | `main_story_saref_state.json.guardianQuestlines[].questStates[]` | Guardian quest management, actor-profile personal quests, player views | Independently authored Guardian/profile copies |
| Story discovery evidence | `main_story_saref_state.json.latentTraces[]` | GM relevance selection, player clue projection when reveal permits | A second mutable quest status |
| Non-story Guardian quest lifecycle | Exact Guardian `questManagement` commands/state | Guardian/profile/player summaries where already supported | Saref progress or catalog membership |
| Guardian identity/profile/receipt | `guardians.json` plus matching afterlife profile and Actor Materialization envelope | Active Guardian mirror, Actor Brain context, player Guardian views | Dossier prose or story state as actor profile |
| Guardian actor-owned thought memory | `guardian_thought_journal.json.entries[]` | Actor Brain memory context | Duplicate `musings` entry for the same materialization |
| Saref actor authority | Exact `afterlife_entity_profiles.json` profile `saref:saref_001` | GM Actor Brain and reveal-filtered player view | `saref_agent`, faction record, prose-only mention |
| Wings faction authority | Exact Shining faction `shine_faction_wings_of_angels_001` | Shining politics, player faction views after reveal | Mortal faction file, alias IDs, story state as full faction |
| Wings memory/history | Faction `strategicMemory` plus append-only `chronicle[]` | GM faction Actor Brain, revealed history | Strategic-memory rewrite without chronicle |
| Guardian plane and Wings hall identity | Shared #1514 location authority | Guardian/faction references | Story-private location objects |

## 1. Saref Story Catalog

The catalog is immutable packaged content, not save data.

```json
{
  "schemaVersion": 1,
  "catalogId": "saref_story_catalog",
  "catalogVersion": "1.0.0",
  "catalogDigest": "sha256:<lowercase-hex>",
  "storyId": "saref_main_story",
  "contentFiles": [
    "story_content/saref/catalog.json",
    "system_guardians/built_in/azalia/guardian_materialization.json",
    "system_guardians/built_in/azalia/saref_questline.json"
  ],
  "guardians": [
    {
      "presetId": "azalia",
      "guardianId": "guard_system_azalia_001",
      "materializationTemplateId": "saref_guardian_azalia_v1",
      "questlineId": "saref_questline_azalia_v1",
      "quests": [
        {
          "questId": "azalia_saref_q1",
          "ordinal": 1,
          "title": "Роза в горле королевы",
          "difficulty": "normal",
          "finalRewardRole": null
        }
      ]
    }
  ],
  "sarefActorTemplateId": "saref_actor_v1",
  "wingsFactionTemplateId": "wings_of_angels_faction_v1",
  "revealRules": {}
}
```

### Catalog invariants

- Exactly ten `guardians[]` entries in approved preset order: Azalia, Brann, Elyara, Ilarion, Lissara, Lucian, Myriel, Seret, Varak, Veyra.
- Exactly four `quests[]` per Guardian, ordinals 1–4, for exactly forty quest IDs.
- Exact Guardian IDs are `guard_system_<lowercase-preset>_001`.
- Exact quest IDs are `<lowercase-preset>_saref_q1` through `q4`.
- Guardian, quest, template, revelation, and advantage IDs are globally unique under ordinal comparison.
- Each q4 has exactly one registered revelation and advantage binding; q1–q3 cannot grant q4 rewards.
- `contentFiles[]` names every semantic JSON input exactly once and names no file outside allowed packaged roots.
- `catalogDigest` matches deterministic semantic hashing described in `research.md`.
- The compact rendered index is complete and at most 32 KiB UTF-8.

## 2. Guardian Materialization Template

Each `guardian_materialization.json` is a complete immutable authoring template.

```json
{
  "schemaVersion": 1,
  "templateId": "saref_guardian_azalia_v1",
  "catalogBinding": {
    "catalogId": "saref_story_catalog",
    "catalogVersion": "1.0.0",
    "catalogDigest": "sha256:<lowercase-hex>"
  },
  "presetId": "azalia",
  "guardianId": "guard_system_azalia_001",
  "guardian": {
    "identity": {},
    "manifestation": {},
    "appearance": {},
    "personality": {},
    "worldview": {},
    "motivation": {},
    "goals": [],
    "plan": {},
    "authoredArts": [],
    "relationshipPosture": {},
    "capabilities": {},
    "intentionalEmptySections": []
  },
  "afterlifeProfile": {},
  "initialThought": {
    "entryId": "guardian_story_memory_azalia_001",
    "thought": "<first-person authored thought>"
  },
  "locationBinding": {
    "locationTemplateId": "<from #1514>",
    "initialAbodeId": "<exact canonical id>"
  },
  "storyBinding": {
    "storyId": "saref_main_story",
    "questlineId": "saref_questline_azalia_v1"
  }
}
```

Runtime may fill only documented dynamic fields: materialization/request ID, turn/time, session binding, current soul relationship baseline, active-Guardian selection, and existing receipt fields. It cannot rewrite authored identity, worldview, goals, arts, story bindings, or initial thought.

## 3. Fixed Guardian Quest Template

Each Guardian's `saref_questline.json` contains four immutable definitions.

```json
{
  "schemaVersion": 1,
  "questlineId": "saref_questline_azalia_v1",
  "guardianId": "guard_system_azalia_001",
  "quests": [
    {
      "questId": "azalia_saref_q1",
      "ordinal": 1,
      "storyScope": "saref_main_story",
      "questOrigin": "saref_main_story_catalog",
      "title": "Роза в горле королевы",
      "description": "<authored player-facing premise>",
      "objective": {},
      "successAuthority": {},
      "difficulty": "normal",
      "allowedRealms": ["Mortal World", "Chaos Sea", "Shining Abode"],
      "evidenceRequirements": {},
      "narrativeBoundaries": [],
      "rewardOutline": {},
      "q4RewardBinding": null
    }
  ]
}
```

Quest 4 additionally carries memory-scene boundaries and exact revelation/advantage IDs. Difficulty is authored per quest; it is not inferred mechanically from ordinal.

## 4. Catalog Binding

```json
{
  "catalogId": "saref_story_catalog",
  "catalogVersion": "1.0.0",
  "catalogDigest": "sha256:<lowercase-hex>"
}
```

All three fields are required and immutable for one game. Every catalog-backed request, transition, receipt, and materialization binding must equal this tuple exactly. There is no `latest`, alias, compatible-version range, or fallback.

## 5. Saref Main Story State v2

```json
{
  "schemaVersion": 2,
  "storyId": "saref_main_story",
  "catalogBinding": {
    "catalogId": "saref_story_catalog",
    "catalogVersion": "1.0.0",
    "catalogDigest": "sha256:<lowercase-hex>"
  },
  "revealStage": "unknown",
  "guardianQuestlines": [],
  "latentTraces": [],
  "sarefRevelations": [],
  "sarefAdvantages": [],
  "sarefAdvantageUses": [],
  "memoryScene": null,
  "factionLinks": {
    "wingsFactionId": null
  },
  "actorLinks": {
    "sarefActorType": null,
    "sarefActorId": null
  },
  "finalConfrontation": null,
  "endings": [],
  "postStoryAgenda": null,
  "defeatOutcomes": []
}
```

### Guardian questline progress

```json
{
  "guardianId": "guard_system_azalia_001",
  "questStates": [
    {
      "questId": "azalia_saref_q1",
      "ordinal": 1,
      "status": "active",
      "discoveredAtTurn": 12,
      "recognizedAtTurn": 18,
      "acceptedAtTurn": 21,
      "updatedAtTurn": 21,
      "lastEvidence": {
        "realm": "Chaos Sea",
        "kind": "player_acceptance",
        "summary": "Игрок принял поручение Азалии.",
        "requestId": "guardian_quest_acceptance_<id>"
      },
      "memorySceneProof": null
    }
  ]
}
```

Supported statuses are `latent`, `recognized`, `active`, `ready_to_turn_in`, and `completed`. Undiscovered means no `questStates[]` row. Timestamp/turn fields are monotonic and become immutable once set. `ordinal` and IDs must match the catalog rather than GM input.

### Latent trace evidence

```json
{
  "traceId": "saref_trace_<stable-id>",
  "guardianId": "guard_system_azalia_001",
  "questId": "azalia_saref_q1",
  "discoveredAtTurn": 12,
  "sourceRealm": "Mortal World",
  "evidenceKind": "symbolic_echo",
  "summary": "Игрок заметил белое перо на королевской печати.",
  "sourceReference": "<optional exact current-world id>"
}
```

Traces are append-only evidence. They do not contain a mutable status and cannot independently grant progress or rewards. A valid recognition references at least one matching trace unless it uses the direct materialized-Guardian encounter exception, which appends its own trace atomically.

## 6. Story Quest Transition

```json
{
  "mode": "advance_guardian_quest",
  "catalogBinding": {
    "catalogId": "saref_story_catalog",
    "catalogVersion": "1.0.0",
    "catalogDigest": "sha256:<lowercase-hex>"
  },
  "guardianId": "guard_system_azalia_001",
  "questId": "azalia_saref_q1",
  "expectedFromStatus": "recognized",
  "toStatus": "active",
  "resolvedAtTurn": 21,
  "realm": "Chaos Sea",
  "evidence": {
    "kind": "player_acceptance",
    "summary": "Игрок явно принял квест.",
    "requestId": "guardian_quest_acceptance_<id>"
  }
}
```

### Transition rules

| From | To | Required authority |
|---|---|---|
| absent | latent | Exact compact-index membership and discovery evidence |
| latent | recognized | Full package and matching trace |
| recognized | active | Exact pending player acceptance and materialized Guardian |
| active | ready_to_turn_in | Allowed realm/objective evidence; non-physical proof for item-like Mortal objectives |
| ready_to_turn_in | completed | Afterlife hand-in, full package, correct prior ordinal completions |
| active q4 | completed | Successful `record_memory_scene` composite with registered proof/reward only |

No stage may be skipped. A direct encounter with an exact materialized Guardian can supply discovery evidence only for `absent -> latent`; it does not authorize direct recognition. Every qN recognition/activation/completion requires q1…qN-1 completed. A later quest may have a latent trace early but may not advance beyond latent until its predecessors are complete. Quest 4 alone completes from `active` through the successful composite `record_memory_scene` contract.

## 7. Derived Story Quest Projection

The client projector emits a catalog snapshot into exact Guardian and profile views. It is regenerated from story progress and immutable template; it is never separately authored.

```json
{
  "questId": "azalia_saref_q1",
  "storyScope": "saref_main_story",
  "questOrigin": "saref_main_story_catalog",
  "title": "Роза в горле королевы",
  "description": "<catalog text>",
  "difficulty": "normal",
  "status": "active",
  "catalogBinding": {
    "catalogId": "saref_story_catalog",
    "catalogVersion": "1.0.0",
    "catalogDigest": "sha256:<lowercase-hex>"
  },
  "guardianId": "guard_system_azalia_001",
  "ordinal": 1,
  "objective": {},
  "rewardOutline": {}
}
```

Projection mapping:

- `latent`: no ordinary quest projection.
- `recognized`: `availableQuests[]` when cap/difficulty permit; matching profile quest is offered.
- `active` and `ready_to_turn_in`: `activeQuests[]`; matching profile quest has the same status.
- `completed`: `completedQuests[]`; profile history has the same completion identity/outcome.

If the exact Guardian/profile has not materialized, progress remains in story state and no shell actor is created by projection.

Player-facing projections map `storyScope=non_story` to the in-world Russian marker `Несюжетный квест` in both console and browser. They do not render the raw `non_story` enum. Story-quest labeling continues to obey reveal filtering.

## 8. Non-Story Guardian Quest

```json
{
  "questId": "guardian_varak_border_oath_0042",
  "storyScope": "non_story",
  "questOrigin": "guardian_post_story_personal_request",
  "title": "Клятва у треснувшего рубежа",
  "description": "Варак просит проверить новый строй своих последователей.",
  "objective": {
    "kind": "world_action",
    "summary": "Испытай строй в текущей смертной жизни."
  },
  "successAuthority": {
    "requiredEvidenceKinds": ["lifeEventEvidence"]
  },
  "difficulty": "hard",
  "rewardOutline": {
    "relationship": "possible",
    "abodePower": "validated on hand-in"
  },
  "grounding": {
    "kind": "guardian_goal",
    "sourceId": "<exact goal/project/politics/relationship/memory id>",
    "summary": "<current-state reason>"
  },
  "status": "available"
}
```

### Non-story invariants

- `storyScope` is exactly `non_story` and the ID is not in the catalog.
- Stable `questId`, `title`, `description`, objective, success authority, difficulty, reward outline, ordinary origin, and current-state grounding are required.
- The offer is created only through `UpdateGuardians.offerQuest` and fits the same available cap/difficulty ceiling as story offers.
- Acceptance is pending-backed and moves the exact offer through `UpdateGuardians.acceptQuest`.
- Mortal progress uses `guardianQuestProgressUpdates`; afterlife hand-in uses `UpdateGuardians.completeQuest`.
- Completion preserves `storyScope`, title, origin, grounding, and reward audit in the historical snapshot.
- It cannot create or update any `guardianQuestlines`, revelation, advantage, reveal, memory-scene proof, or deep-victory fact.

## 9. Guardian Quest Acceptance Request and Resolution

Client-owned pending file:

```json
{
  "requests": [
    {
      "requestId": "guardian_quest_acceptance_<id>",
      "guardianId": "guard_system_azalia_001",
      "questId": "azalia_saref_q1",
      "storyScope": "saref_main_story",
      "expectedStatus": "available",
      "catalogBinding": {
        "catalogId": "saref_story_catalog",
        "catalogVersion": "1.0.0",
        "catalogDigest": "sha256:<lowercase-hex>"
      },
      "offerSnapshotDigest": "sha256:<lowercase-hex>",
      "createdAtTurn": 21,
      "createdAtUtc": "2026-08-10T12:00:00Z"
    }
  ]
}
```

Accepted/rejected GM resolution:

```json
{
  "requestId": "guardian_quest_acceptance_<id>",
  "guardianId": "guard_system_azalia_001",
  "questId": "azalia_saref_q1",
  "storyScope": "saref_main_story",
  "status": "accepted",
  "resolvedAtTurn": 21,
  "reason": "Игрок принял поручение."
}
```

`status` is `accepted` or `rejected`. Accepted requires exactly one matching authority mutation in the same validated response. Rejected requires no mutation. The client clears only resolutions accepted by validation. Duplicate request IDs, stale offer digests, wrong realm, mismatched scope/binding, missing Guardian, or non-available quests fail closed.

`offerSnapshotDigest` uses the same recursively sorted semantic JSON canonicalization as the catalog digest. The pending path is client-owned and must appear in `AfterlifeContractRegistry`, `OtherGuides/Afterlife_Pending_Control_Surface_Inventory.json`, validated snapshot/client-owned path classification, Soul Gates blockers, accepted-resolution cleanup, daemon prompt routing, and registry/documentation tests.

## 10. Saref Actor Template and Canonical Profile

The packaged template fixes exact identity and all authored sections. Runtime materialization publishes a normal current-schema common afterlife profile:

```json
{
  "actorType": "saref",
  "actorId": "saref_001",
  "displayName": "Сареф",
  "appearance": {},
  "profileSummary": "<authored>",
  "personality": {},
  "worldview": {},
  "motivation": {},
  "currentRealm": "Shining Abode",
  "currentLocationId": "<exact #1514 location>",
  "goals": [],
  "plan": {},
  "standardArts": [],
  "specialArts": [],
  "relationships": [],
  "publicMasks": [],
  "privateTruth": {},
  "gmThoughtsSummary": "<meaningful actor-owned first-person memory>",
  "ActorMaterialization": {}
}
```

The materialization receipt is immutable. Later activity uses dedicated actor deltas. `saref_agent` cannot use `actorId=saref_001`, and `saref` cannot represent supporters.

## 11. Wings Faction Template and Canonical Faction

The packaged template binds #1510's complete Shining story route:

```json
{
  "factionId": "shine_faction_wings_of_angels_001",
  "factionRole": "wings_of_angels",
  "storyAuthority": "saref_main_story",
  "creationRoute": "story",
  "charter": {},
  "lifecycle": {},
  "leadership": {
    "leaderActorType": "saref",
    "leaderActorId": "saref_001"
  },
  "hallLocationId": "<exact #1514 location>",
  "strategicMemory": {},
  "chronicle": [],
  "relationships": [],
  "capabilities": {},
  "visibility": "hidden",
  "FactionMaterialization": {}
}
```

All cross-links to `factionLinks.wingsFactionId`, Saref leadership, hall, visibility, provenance, and Guardian q4 rewards must equal the exact catalog/template values. Old aliases are invalid only when used as `factionId`/`wingsFactionId`; `factionRole=wings_of_angels` and the existing player command aliases remain valid. Later decisions append chronicle entries; strategic memory may change only with matching history.

## 12. Atomic publication and repair boundary

A multi-root operation constructs proposed JSON in memory, validates catalog integrity, current binding, command/request authority, every proposed root, exact cross-links, and protected-root equivalence, then publishes with the existing atomic file primitives. If any write or post-write verification fails, the transaction restores the validated pre-turn versions of every targeted root before exposing success.

Allowed target sets are operation-specific:

- Guardian materialization: exact Guardian root, exact profile root, Guardian thought journal, and exact story projection/state roots when needed.
- Story transition: story state plus exact Guardian/profile projection roots only.
- Non-story offer/accept/progress/complete: exact Guardian root and already documented ordinary derived/audit roots only; never Saref state.
- Saref materialization: exact profile root and story actor link.
- Wings materialization: exact Shining faction root, story faction link, and exact #1514 reference validation; never Mortal faction authority.

Repair packets enumerate target files, entity identities, JSON pointers, expected binding/digest, protected roots, and allowed operation. Packaged content is never a GM repair target.
