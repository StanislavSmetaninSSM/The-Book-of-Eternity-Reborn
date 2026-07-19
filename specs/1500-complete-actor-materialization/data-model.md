# Data Model: Complete Actor Materialization

## Actor Materialization Envelope v1

```json
{
  "materialization": {
    "schemaVersion": 1,
    "materializationId": "mat_npc_marius_turn_3",
    "actorType": "mortal_npc",
    "actorId": "npc_marius_de_valmont",
    "materializedAtTurn": 3,
    "state": "complete",
    "capabilities": {
      "canFight": true,
      "canTeach": false,
      "canTrade": true,
      "ownsItems": true
    },
    "sections": {
      "skills": { "state": "populated" },
      "inventory": { "state": "populated" },
      "fateCards": {
        "state": "empty_by_design",
        "reason": "Его судьба пока не открыла отдельной карты."
      },
      "personalQuests": { "state": "populated" },
      "relationships": { "state": "populated" }
    }
  }
}
```

### Common fields

| Field | Type | Rule |
|---|---|---|
| `schemaVersion` | integer | Exactly `1` for this contract |
| `materializationId` | string | Non-empty, stable, unique in actor scope |
| `actorType` | enum | Exact supported actor type |
| `actorId` | string | Exact canonical stable ID; same-turn Mortal actors use their `initialId` binding until canonicalization |
| `materializedAtTurn` | integer | Non-negative accepted-turn number |
| `state` | enum | Exactly `complete` |
| `capabilities` | object | Exact allowed boolean keys for actor family |
| `sections` | object | Exact governed keys for actor family |

### Section disposition

```json
{ "state": "populated" }
```

or:

```json
{
  "state": "empty_by_design",
  "reason": "In-world explanation written by the GM."
}
```

Rules:

- `populated` forbids `reason` and requires non-empty canonical section content.
- `empty_by_design` requires non-empty `reason` and requires canonical section content to be empty.
- Unknown fields and unknown section names are rejected.
- Reasons are private GM/harness evidence and are not rendered as player prose automatically.

## Mortal actor contract

### Identity binding

- `actorType`: `mortal_npc`
- `actorId`: existing `NPCId` for permanent actors; exact `initialId` for same-turn first creation before canonical ID assignment.

### Capabilities

| Capability | Required canonical evidence |
|---|---|
| `canFight` | At least one structurally valid active or passive skill |
| `canTeach` | `teacherProfile.canTeach=true` and non-empty `teacherProfile.skills` |
| `canTrade` | Existing NPC trade/merchant authority indicates trading is available |
| `ownsItems` | Non-empty inventory; equipped references resolve to inventory |

### Sections

| Section | Canonical source |
|---|---|
| `skills` | `activeSkills` + `passiveSkills` |
| `inventory` | `inventory` and equipment references |
| `fateCards` | `fateCards` |
| `personalQuests` | `personalQuests` |
| `relationships` | `relationshipLevel`, `attitude`, `relationshipLock`, and any initial inter-NPC relationship authority |

The existing core NPC fields remain mandatory and are not duplicated in `sections`.

## Afterlife actor contract

### Supported `actorType` values

- `guardian`
- `resident`
- `shining_resident`
- `shining_faction_head`
- `radiant_actor`
- `saref_agent`
- `system_actor`
- `custom_afterlife_actor`

Exact aliases already accepted by canonical profile identity are normalized only for comparison; the stored value must use a documented canonical token.

### Capabilities

| Capability | Required canonical evidence |
|---|---|
| `canFight` | At least one usable standard or special spiritual art |
| `canTeach` | Existing mentor authority and at least one teachable art/showcase entry |
| `canTrade` | Realm-appropriate Guardian/Shining trade authority. When that authoritative evidence is unavailable at the validation boundary, validation fails closed and requires `false`; it must never infer trade authority from prose, role names, or genre vocabulary. |

Afterlife envelopes do not use `ownsItems`; Mortal inventory remains forbidden.

### Sections

| Section | Canonical source |
|---|---|
| `standardArts` | Common profile standard arts |
| `specialArts` | Common profile special arts |
| `customStates` | Common profile custom states |
| `fateCards` | Common profile Fate Cards |
| `relationships` | Common profile relationship records |
| `agency` | Goals, personal quests, activities, masks/disposition, and current strategy inputs |
| `progressionHistory` | Ledger and progression ledger evidence |

### Cross-file binding

A significant non-player afterlife record must resolve by exact actor type and ID to one common profile. Guardians, residents, and Shining leadership retain their type-specific dossiers. The common profile is complementary authority for spiritual progression, Actor Brain inputs, relationships, and memory.

Exceptions:

- A vacant Shining seat has no head profile.
- The player soul resolves to its existing client-owned profile.
- A System Guardian fresh-game seed remains `actorType=guardian`, is recognized through its existing client-owned source authority, and still receives a deterministic valid envelope.

## State transitions

1. `absent` -> `complete`: permitted only on first materialization with complete envelope.
2. `legacy_without_envelope` -> unchanged: accepted for load compatibility.
3. `legacy_without_envelope` -> significant promotion: blocked until materialized.
4. `complete` -> ordinary update: dedicated delta commands mutate gameplay fields; envelope identity remains stable.
5. `complete` -> resent as first materialization: rejected for existing actors when it attempts to bypass delta authority.
6. `complete` -> invalid contradiction: blocked with bounded repair.

## Validation issue families

- `actor_materialization_missing`
- `actor_materialization_invalid_envelope`
- `actor_materialization_duplicate_property`
- `actor_materialization_invalid_actor_type`
- `actor_materialization_actor_binding_mismatch`
- `actor_materialization_duplicate_id`
- `actor_materialization_section_missing`
- `actor_materialization_section_content_mismatch`
- `actor_materialization_capability_mismatch`
- `actor_materialization_inventory_reference_mismatch`
- `afterlife_actor_materialization_profile_missing`
- `afterlife_actor_materialization_profile_ambiguous`
- `actor_materialization_existing_resend_forbidden`

Every issue records actor type/ID, section or capability where applicable, expected canonical target, and a bounded repair hint.
