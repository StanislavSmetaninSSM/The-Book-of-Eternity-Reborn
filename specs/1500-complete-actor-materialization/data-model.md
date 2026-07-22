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
- Effective identity is resolved once from the canonical permanent fields or the exact same-turn `initialId`. If validated pre-turn state already owns that value as a permanent `NPCId`, `NPCId=null` plus the colliding `initialId` is invalid and inventory continuity still treats the actor as existing.

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
`characteristics` must contain at least one numeric property. Property names come from the current world's schema; the materialization contract does not define a universal characteristic vocabulary.
Current first materialization contains 3-5 `personalityTraits`; every trait has a mandatory integer `value` from 1 through 10. Untouched historical legacy records are not retroactively rejected only for older cardinality.

### Ordinary-existing `NPCCoreChanges`

`NPCCoreChanges` is a Mortal-only non-carrier command mapped to `game_state/npcs/npc_core.json`. It is validated before normalization, reduced into every unambiguous canonical mirror of one existing actor, removed after successful reduction, and absent from final canonical state. Invalid commands remain unconsumed for repair.

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

Unused optional mutation groups are omitted. A present empty object or array is
not a placeholder and is rejected by the runtime contract.

Boundaries:

- `NPCId` is one exact existing permanent identity. Names, `initialId`, new/stale targets, case-variant ambiguity, and divergent mirrors fail closed.
- `reason` is non-empty and at least one mutation group is non-empty. Values are absolute resulting values; expressions and prose-derived arithmetic are invalid.
- Unknown members are rejected recursively. Identity, name, materialization, inventory/equipment, skills/mastery, relationships/locks, journals/memory, goals/quests, activities, masks, custom states, teacher/trade capabilities, and arbitrary paths remain protected or owned by dedicated commands.
- Current and validated pre-turn `npc_core.json` are parsed duplicate-sensitively before command evaluation. Malformed, non-object, or duplicate-member authority is a blocking structured error rather than an absent command.
- Historical `NPCsInScene` and envelope-free `UpdateNPCs` preserve every actor-owned field. Inventory and materialization use their narrower continuity diagnostics; every other direct carrier mutation is rejected by the shared actor-ownership boundary.
- `characteristicValues` contains finite numeric results only for actor-owned keys or explicit current-world characteristic authority. Carrying and progression formulas also require explicit current-world authority; absent carrying authority leaves the setting-owned nullable result null.
- A location mutation carries both fields and uses exactly one authority branch: permanent `currentLocationId` plus null `initialLocationId`, or null current plus exact same-turn `initialLocationId`.
- Changing any level/experience threshold field requires the coherent non-negative tuple. Include `lastPlayerXPValueOnSync` when a role/progression transition requires synchronization.
- Faction upserts use exact faction identity and the existing complete affiliation shape. Fate Card additions reuse the full production Fate Card, active/passive skill, Combat Action, and effect validator, have unique IDs, and begin locked; any nested failure keeps the command unconsumed and all mirrors unchanged. Removals target only validated pre-turn locked/unrealized cards. Unlocking remains `NPCFateCardUnlocks`.

Mortal combat capability and promotion evidence use the canonical active/passive
skill structure. A complete Block 7 skill does not require `skillId` or `id`;
legacy identifier-only records remain detectable for migration without changing
the production skill validator.

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
| `canTrade` | Exact current realm authority: the one active Guardian whose abode is the current Chaos Sea abode, or a non-player secure/contested head of an operational Shining faction at trade tier 1 or higher. Missing or ambiguous authority fails closed to `false`; prose, role names, and genre vocabulary never count. |

Afterlife envelopes do not use `ownsItems`; Mortal inventory remains forbidden.

### Sections

| Section | Canonical source |
|---|---|
| `standardArts` | Common profile standard arts |
| `specialArts` | Common profile special arts |
| `customStates` | Common profile custom states |
| `fateCards` | Common profile Fate Cards |
| `relationships` | Common profile relationship records |
| `agency` | Meaningful goals, non-empty personal quests, a meaningful current activity, or non-empty completed activity history. Masks, disposition, and progression strategy may inform behavior but do not satisfy this section alone. |
| `progressionHistory` | Ledger and progression ledger evidence |

### Cross-file binding

A significant non-player afterlife record must resolve by exact actor type and ID to one common profile. Guardians, residents, and Shining leadership retain their type-specific dossiers. The common profile is complementary authority for spiritual progression, Actor Brain inputs, relationships, and memory. Every newly materialized profile, including the first envelope on an existing bound profile, must have actor-owned memory: the exact dedicated Guardian/resident thought journal when that surface exists, otherwise a non-empty exact profile `gmThoughtsSummary`. An unchanged legacy profile with no envelope transition remains grandfathered.

Exceptions:

- A vacant Shining seat has no head profile.
- The player soul resolves to its existing client-owned profile.
- A System Guardian fresh-game seed remains `actorType=guardian`, is recognized through its existing client-owned source authority, and receives a deterministic valid envelope whose capability booleans, including `canTrade`, match that exact seeded current authority.

## State transitions

1. `absent` -> `complete`: permitted only on first materialization with complete envelope.
2. `legacy_without_envelope` -> unchanged: accepted for load compatibility.
3. `legacy_without_envelope` -> significant promotion: blocked until materialized; Mortal effective identity cannot be disguised with a colliding `initialId`, the complete object may repeat `inventory` only when it is semantically identical to the validated pre-turn snapshot, and an afterlife first envelope must prove actual actor-owned memory.
4. `complete` -> ordinary update: dedicated delta commands mutate their owned gameplay fields; valid `NPCCoreChanges` mutates only its closed core groups; envelope identity remains stable.
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
- `npc_characteristics_empty`
- `npc_existing_inventory_resend_forbidden`
- `npc_initial_id_collides_with_existing_permanent_id`
- `npc_core_changes_unknown_member`
- `npc_existing_core_direct_mutation_forbidden`
- `afterlife_actor_materialization_memory_missing`

Every issue records actor type/ID, section or capability where applicable, expected canonical target, and a bounded repair hint.

Raw continuity authority is duplicate-sensitive before semantic comparison. Current Mortal/afterlife actor subtrees, current inventory/materialization values, and validated pre-turn actor/inventory authority reject duplicate members through structured issues. Only duplicate-free valid JSON reaches order-insensitive semantic equality.

The three Mortal continuity issue policies are explicit:

| Issue code | Required metadata | Worker policy |
|---|---|---|
| `npc_initial_id_collides_with_existing_permanent_id` | exact `mortal_npc:<id>`, `NPCIdentity` | Never dispatch/apply through a worker; use main-GM rollback/repair because identity correction may have cross-file consequences. |
| `npc_existing_inventory_resend_forbidden` | exact actor, `NPCInventory`, current inventory in `actual`; exact validated pre-turn JSON array in `expected` only for a true legacy promotion | Dispatch only with the exact JSON-array snapshot. Apply may replace only the named actor/carrier inventory with that snapshot. Ordinary existing resends remain main-GM-only. |
| `npc_characteristics_empty` | exact actor, `NPCCharacteristics`, setting-defined numeric requirement | Dispatch pins `game_state/misc/characteristics.json` as read-only context. Apply may replace only the named actor/carrier empty object with finite numeric keys present in that authority; missing/malformed/empty authority rejects. |

For every supported worker correction, deleting/adding the file or actor, changing a sibling field, another actor, root data, a different carrier, or a non-snapshot target remains protected and rejects before full validation.

The high-priority main-GM inventory repair packet never turns an ordinary existing full object into a partial object: because canonical full-object shape requires `inventory`, the whole ordinary-existing `UpdateNPCs` resend is removed. Skill, inventory, relationship, journal, goal/quest, activity, equipment/resource, rename, and unlock changes are re-authored through dedicated commands; the bounded profile/location/progression/setting-owned characteristic/faction/Fate Card definition groups use `NPCCoreChanges`. A protected field outside both contracts forces main-GM rollback/repair rather than widening the command or deleting required fields. Genuinely new initial inventory and exact-snapshot legacy promotion remain the two complete-object branches.

The third re-review additions in this section are Mortal-only. They do not change Chaos Sea or Shining Abode pending/control files, response fields, receipts, reports, actor-profile schema, validation, normalization, scheduler, lifecycle mode, or authority path.

The fourth re-review adds no afterlife pending/control surface. The shared Block 5
Combat Action validator now enforces its already documented mandatory effect
`value`; the afterlife matrix/example call out inherited item/relic/skill use and
explicitly keep `specialArts[].combatEffect` on its separate spiritual-conflict schema.

Before applying a worker-authored actor materialization repair, the apply gate routes memory issues by actor type, removes only the exact mutable subtree named by the issue, and semantically compares the remaining canonical JSON. Guardian targets use the canonical/supported Guardian journal path, residents use resident state/journal, and Radiant/Saref/other common-profile actors use only their exact profile `gmThoughtsSummary`. Any change to protected actor data, another actor, root state, currencies, progression, envelope, or unrelated scalar rejects the proposal. An ambiguous-profile repair may only remove duplicates while retaining one otherwise unchanged canonical profile. Dedicated Guardian/resident memory repair is stricter than ordinary scalar repair: all existing journal entries remain an exact prefix, and the worker may append exactly one meaningful thought for the issue-bound actor without rewriting or deleting history. If and only if `game_state/meta/guardian_thought_journal.json` is absent and every scoped issue is `afterlife_actor_materialization_memory_missing` for one exact `guardian:<id>`, preservation uses `{ "entries": [] }` as the baseline; the normal proposal contract still requires Add, `beforeSha256=missing`, exact `afterSha256`, and the proposal-bound content reference.

Every validation-repair context path has one exact byte state: a 64-character SHA-256 digest or `missing`. A changed-file entry repeats that state as `beforeSha256`; add is legal only from `missing`, replace/delete only from an existing digest. The worker runs in an ephemeral detached `.worker_runtime` snapshot containing only those pinned context bytes. Non-delete content is imported only when declared at `worker_proposals/<proposalId>/<path>` and its bytes match `afterSha256`; delete uses `afterSha256=missing` and no content reference. Direct snapshot edits and undeclared artifacts are discarded. Apply and rollback both compare expected bytes under one canonical write lease retained through full validation, read-only context revalidation, and final decision. A mismatch is a conflict and never overwrites the newer owner.

All declared non-delete content is loaded and digest-verified before any proposal
artifact is published. Task and proposal IDs are immutable identities; a
collision preserves the earlier artifact and rejects the new handoff. Canonical
session-path identity is case-insensitive, duplicate aliases reject, and
afterlife validation-repair surface authority is an exact wildcard-free path
set wholly under `game_state/meta/`; typed afterlife content tasks retain their
exact task-provided control/report surfaces. `lore/current_world/**` and
`game_state/core/player_status.json` are Mortal authorities even when an issue
points at a nested field. Validation-repair dispatch IDs combine the readable
attempt number with a unique dispatch suffix. Task and proposal identities are
atomically claimed before publication, and a per-session/per-worker gate owns a
`MaxConcurrentTasks` slot before any task artifact is written. An observed
timeout remains the execution result even when malformed proposal bytes also
exist. Built-in backup, restore, game-state clear, and current-world lore clear
participate in the canonical write lease. Save reads and live-session replacement
on load participate in that lease as well; the lock file lives outside the
replaceable `game_session` directory. Detached cleanup traverses only
ordinary child entries, removes reparse entries as links, and records exhausted
cleanup failure without replacing the worker result.

`WorkerProposalStatus.Unspecified=0` is never a completion state. JSON `status` is required; omission fails deserialization, while direct unspecified/unknown models fail contract validation and apply. Only explicit `status=completed` proposals may reach the apply path. `failed`, `timed-out`, and `rejected` proposals carry no `changedFiles` and remain diagnostic records. Worker audit lines use one lock-protected read-and-append operation so concurrent writers preserve every event. Every producer calls one shared generator with `worker_audit_<UTC yyyyMMddHHmmssfff>_<32 lowercase hex GUID>`; a source guard forbids hand-built prefixes elsewhere, and audit publication failure is telemetry loss that cannot revoke an already accepted canonical commit.

Repair handoff has one owner state. Before dispatch, request/ready/stall artifacts from older cycles are removed. `Applied + ReadySignalCreated` enters the correlated ready path; `Applied + !ReadySignalCreated` records `worker_apply_gate_accepted` and immediately revalidates; every non-applied result writes one fresh legacy request. Player-facing output freshness retains the original repaired canonical target set through all derived output-only retries and requires every output write to be strictly newer than the latest actual write of every retained target, including worker-only cycles where no legacy request exists. Equal timestamps are stale; rewriting an original target recomputes the boundary and re-stales older output. Request creation, dispatch, and repair-loop start timestamps are not canonical mutation boundaries; an unobservable required target mutation fails freshness closed.

`WorkerFileChangeKind` is a closed operation authority: `Unspecified=0` is invalid, and only explicit `Add`, `Replace`, or `Delete` reaches apply semantics. Omitted or undefined numeric values reject during proposal contract validation.

Every `game_state/meta/` validation-repair target is afterlife-scoped, including non-actor metadata. `game_state/meta/soul_state.json` is an exact-byte, hash-pinned, read-only realm-authority context file. Strict parsing requires one supported `currentRealm` without duplicate members. The authority file is never an allowed proposal path; mixed Mortal/meta batches and missing, malformed, unsupported, changed, or contract-mismatched authority fail task construction or apply closed. `game_state/misc/characteristics.json` follows the same read-only rule in every empty-characteristics task, including mixed Mortal batches.
