# Contract: Faction Materialization Envelope

## Purpose

The envelope is immutable private proof that one faction was semantically
complete when first accepted. It is not a live gameplay summary and is not
rendered in ordinary console/browser views.

## Closed schema

```json
{
  "schemaVersion": 1,
  "materializationId": "fmat_northern_guild_184",
  "factionType": "mortal_faction",
  "factionId": "faction_northern_guild",
  "materializedAtTurn": 184,
  "state": "complete",
  "capabilities": {
    "hasFormalHierarchy": true,
    "usesFactionResources": false,
    "maintainsRelations": false,
    "runsProjects": false,
    "holdsTerritoryOrInfluence": false,
    "supportsPlayerMembership": false,
    "usesCustomMechanics": false
  },
  "sections": {
    "hierarchy": {
      "state": "populated"
    },
    "resources": {
      "state": "empty_by_design",
      "reason": "Members currently contribute supplies personally."
    },
    "relations": {
      "state": "empty_by_design",
      "reason": "No formal relation has been established."
    },
    "projects": {
      "state": "empty_by_design",
      "reason": "No project has been chartered."
    },
    "territoryAndInfluence": {
      "state": "empty_by_design",
      "reason": "The guild does not claim territorial control."
    },
    "playerMembership": {
      "state": "empty_by_design",
      "reason": "The player has no formal guild standing."
    },
    "customStates": {
      "state": "empty_by_design",
      "reason": "The guild has no unique tracked mechanic."
    }
  }
}
```

No additional or duplicate member is legal.

## Mortal profile

Capabilities:

```text
hasFormalHierarchy
usesFactionResources
maintainsRelations
runsProjects
holdsTerritoryOrInfluence
supportsPlayerMembership
usesCustomMechanics
```

Sections:

```text
hierarchy
resources
relations
projects
territoryAndInfluence
playerMembership
customStates
```

Each capability is the direct evidence bit for the section in the same row of
the data-model mapping.

## Shining profile

Capabilities:

```text
runsProjects
holdsTerritorialInfluence
usesResourceLedger
hasResidentAffiliations
canTrade
hasLeadershipHistory
usesStoryState
```

Sections:

```text
projects
territorialInfluence
resourceLedger
residentAffiliations
trade
leadershipHistory
storyState
```

All capabilities except `canTrade` are direct evidence bits for the
corresponding section. `canTrade` is checked against the existing mechanical
trade rules; the `trade` section independently records current inventory or
history content.

## Disposition schema

Populated:

```json
{
  "state": "populated"
}
```

Deliberately empty:

```json
{
  "state": "empty_by_design",
  "reason": "Non-empty in-world reason tied to this exact faction."
}
```

The validator rejects:

- a populated section with no production-valid content;
- a populated section with `reason`;
- an empty section without exact empty canonical evidence;
- an empty section without a meaningful reason;
- an empty section with active content;
- unknown state/member names;
- missing or duplicated sections/capabilities.

## Identity and uniqueness

- Outer faction identity and envelope `factionId` match exactly.
- `factionType` matches the domain being validated.
- A same-turn Mortal temporary `initialId` is the effective identity until the
  normalizer binds the permanent ID.
- `materializationId` is unique across the complete validated Mortal/Shining
  set.
- No display name, description, tag, or object position participates in
  identity.

## Continuity

When validated pre-turn state has an envelope, current state must contain a
semantically equal envelope. Any changed scalar, capability, disposition,
reason, missing member, or removed envelope yields an immutable-continuity
failure.

Full capability/disposition-to-content evidence is evaluated against the raw
full carrier only when a faction is `new` or `legacy_promotion`. Canonical
post-normalization validation and every `already_materialized` check still
validate the closed envelope shape and exact pre-turn equality but defer live
evidence consistency: the same-turn mutation that triggered promotion, or a
later narrow command, may legitimately add the first project, relation,
resource, trade receipt, or other mutable content without rewriting the
historical snapshot. Ordinary canonical validators continue to validate that
live content.

## Required issue families

```text
faction_materialization_missing
faction_materialization_invalid
faction_materialization_identity_mismatch
faction_materialization_section_missing
faction_materialization_disposition_mismatch
faction_materialization_capability_mismatch
faction_materialization_bundle_incomplete
faction_materialization_cross_reference_invalid
faction_legacy_promotion_required
faction_existing_full_resend_forbidden
```

Duplicate properties and duplicate IDs may use narrower stable suffixes while
remaining routable as faction materialization errors.
