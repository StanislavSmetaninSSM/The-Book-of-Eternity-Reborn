# Mortal living-world DATA and ownership model

Issue #1516. Working contract, not acceptance evidence.

## Private candidate report

`game_state/control/mortal_living_world_report.json` is a closed version-1 object:
`schemaVersion`, `sessionId`, `requestId`, `turn`, `fromMinute`, `toMinute`,
`resolution`, `audit`, `events`. Resolution is `changes` or
`noSignificantChange`. Audit covers each of `npcs`, `factions`, `locations`,
`projects`, `world` exactly once with a nonempty reason. No-change has no events.
A current meaningful cycle requires a report even when it first becomes due
inside this turn. The evaluated window includes unprocessed original backlog.

Admission checks detached clock/identity DATA only. It does not authenticate
that DATA, prove a complete world assessment, or grant publication authority.
A legal no-change assessment does not require all canonical state to remain
unchanged: ordinary player-driven effects can coexist with no autonomous event.

## Event and effect evidence

A complete private event has exact unique `eventId`, `fromMinute`, `toMinute`,
`actors`, `targets`, `cause`, `effects`, `consequences`, `knowledge`.
References are typed exact `kind`/`id` plus `ownerId` for composite identities.
NPC activity is a canonical NPC slot, not an invented activity identity.
Faction projects are identified by faction and project together; threats by
location and threat together. An actor may instead name the declared
`worldCycle` or `factionCycle` system. Descriptions never allocate identities.

Cause contains `summary` and typed `references`. Each effect contains unique
`effectId`, `target`, `operation`, `field`, `before`, `after`. Before and after
use `{present:false}` or `{present:true,value:...}`; absence differs from null.
Effects must match authenticated original and settled canonical values. A
closed semantic field catalog prevents arbitrary path writes and rejects
cosmetic-only renaming or reorder-only changes as sole mechanical proof.
The same target/field cannot justify multiple events. Consequences name actual
effect IDs. News creation cannot prove its own mechanical consequence.

Initial pure event DATA work qualifies updates only. Creation/removal remains
an explicit implementation task, not silently accepted by a permissive parser.
Final creation requires authenticated absence and a valid canonical object;
removal must follow an already supported lifecycle. Project completion is an
active-to-completed transition. Unresolved creation references are rejected
unless the existing domain owner supplies an exact authenticated mapping.

## Canonical source catalog

- NPC: `npcs/npc_core.json`, exact `NPCId` in `UpdateNPCs`/`NPCsInScene`;
  duplicate projections must agree.
- Faction: `factions/faction_core.json`, `factions[].factionId`.
- Location: `world/world_map.json`, `locations[].locationId`; current location
  is a synchronized projection, not separate authority.
- Project: `factions/faction_projects.json`, active/completed projects with
  exact faction/project identity.
- Activity: canonical NPC current/completed activity, never mere presence of
  `NPCActivityUpdates` in a command sidecar.
- Threat: canonical location active threats with exact location/threat identity.
- World flag: `world/world_flags.json`, `worldStateFlags[].flagId` and value.

All paths above are under `game_state/`. Existing domain validators remain
responsible for lifecycle and object completeness. This contract neither
replaces reducers nor legitimizes their inputs merely because a report exists.

## Private receipt and public projection

`game_state/world/mortal_living_world_history.json` is client-owned versioned
append-only history relative to the authenticated original. Each receipt binds
session/request/turn/window, source report fingerprint, previous receipt,
accepted assessment/events and verified effects. Deterministic hashes are
integrity DATA, not serialized rights. Exact retry must not append twice;
conflicting reuse of a source or event identity fails closed.

Knowledge declares `player` as `hidden` or `known` and exact `newsReferences`.
Hidden events have no public references. Known event references must resolve
into actual safe public news. Private targets/causes/provenance are never
copied into the legacy news object that UI currently renders field-by-field.
This does not claim a general NLP privacy audit or complete #1540 remediation.

The owning transaction captures fresh authenticated originals, current source
and domain inputs under its existing lease; verifies settled effects; and
publishes history inside the same rollback cohort. Joint publication must
include history before sealing the common plan, never append a sidecar after
seal. Terminal QTE ownership is separately examined before final integration.

## Limits that remain visible

The client verifies completeness and consistency of declared evidence, not
causal truth of free prose or a simulator's exhaustive event forecast. It does
not prove that a no-change reason omitted no possible event. A before/final
comparison cannot prove an intermediate A-to-B-to-A change without an existing
domain journal. These boundaries are not permission to accept missing fields,
wrong identities, stale source, malformed audit, or invented mechanical effects.
