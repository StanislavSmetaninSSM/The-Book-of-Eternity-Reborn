# Mortal living-world DATA and ownership model

Issue #1516. Working contract, not acceptance evidence.

## Private candidate report

`game_state/control/mortal_living_world_report.json` is a closed version-1 object:
`schemaVersion`, `sessionId`, `requestId`, `turn`, `fromMinute`, `toMinute`,
`resolution`, `audit`, `events`. Resolution is `changes` or
`noSignificantChange`. Audit covers each of `npcs`, `factions`, `locations`,
`projects`, `world` exactly once with a nonempty reason. No-change has no events.
For an ordinary assessed Mortal turn, a meaningful cycle requires a report even
when it first becomes due inside this turn. The evaluated window includes unprocessed original backlog.

Admission checks detached clock/identity DATA only. It does not authenticate
that DATA, prove a complete world assessment, or grant publication authority.
A legal no-change assessment does not require all canonical state to remain
unchanged: ordinary player-driven effects can coexist with no autonomous event.

## Event and effect evidence

A complete private event has exact unique `eventId`, `fromMinute`, `toMinute`,
`actors`, `targets`, `cause`, `effects`, `consequences`, `knowledge`.
References are typed exact `kind`/`id` plus `ownerId` for composite identities.
NPC activity is a canonical NPC slot, not an invented activity identity.
Location links use exact linkId in world_map.links. Faction projects are identified by faction and project together; threats by
location and threat together. An actor may instead name the declared
`worldCycle` or `factionCycle` system. Descriptions never allocate identities.

Cause contains `summary` and typed `references`. Each effect contains unique
`effectId`, `target`, `operation`, `field`, `before`, `after`. Before and after
use `{present:false}` or `{present:true,value:...}`; absence differs from null.
Effects must match authenticated original and settled canonical values. A
closed kind/field-aware semantic projection prevents arbitrary path writes and rejects
cosmetic-only renaming or reorder-only changes as sole mechanical proof.
The same target/field cannot justify multiple events. Consequences name actual
effect IDs. News creation cannot prove its own mechanical consequence.

Supported whole-entity evidence is deliberately narrow: `operation:create` with
`field:entity` supports a complete worldFlag birth or a complete active faction
project birth. The exact original identity must be absent in authenticated source
DATA, including explicit path absence where relevant; unresolved/malformed source
is not absence. Faction projects retain exact faction/project ownership and cannot
borrow another owner's identity or an already completed project. `operation:complete`
proves the exact active-to-completed project transition, removal from the active
collection, complete final object and completionTurn matching the report turn.
These transitions use existing domain reducers and the common receipt/history
owner; the report does not create objects itself. Generic removal and unsupported
creation/lifecycle kinds fail closed. Field updates still require real semantic
before/after evidence and cannot overlap whole-entity evidence. The current worked
GM examples remain update/no-change DATA examples, not lifecycle acceptance proof.

## Canonical source catalog

- NPC: `npcs/npc_core.json`, exact `NPCId` in `UpdateNPCs`/`NPCsInScene`;
  duplicate projections must agree.
- Faction: `factions/faction_core.json`, `factions[].factionId`.
- Location: `world/world_map.json`, `locations[].locationId`; storage updates target actual `locationStorages`, not a section-name property. Existing topology updates target `links[].linkId`; current location
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

`game_state/world/mortal_living_world_history.json` is client-owned revision-2
append-only history relative to the authenticated original. Each receipt binds
session/request/turn/window, the authenticated original snapshot token,
accepted assessment/events and the exact assessed numeric window. The shared deterministic receipt hash includes the complete report and source tuple;
its explicit `declarationFingerprint` binds DATA, not settled evidence. It does
not claim to hash unrelated settled world state. Actual final effect and
clock validation plus exact output guards remain separate mandatory live checks; the signed original binds the complete prior
history. No redundant chained previous-receipt hash is required. Deterministic hashes are
integrity DATA, not serialized rights. Exact retry must not append twice;
conflicting reuse of a source or event identity fails closed.

Knowledge declares `player` as `hidden` or `known` and exact `newsReferences`.
Hidden events have no public references. Known event references must resolve
into actual safe public news. A declared referenced projection has exactly
`eventId`, `title`, `description`, `visibility:"public"`, plus optional
`locationId` resolving to a uniquely discovered/visited canonical location.
Unknown fields, structured private evidence and hidden/rumored location
references are rejected; prose truth/secrecy is not inferred. Private targets/causes/provenance are never
copied into the legacy news object that UI currently renders field-by-field.
This does not claim a general NLP privacy audit or complete #1540 remediation.

## Actual owner routes

- Ordinary publication captures fresh authenticated original/current source under
  its existing lease, verifies settled effects and clock, then publishes private
  history and consumes the report in its existing rollback cohort. A live request
  without an authenticated original is not a bootstrap exemption. Bare maintenance
  is exempt only when original manifest, request and report are all absent.
- Diagnosis admits `noSignificantChange` only, using the typed actual planned T1
  from its diagnosis owner rather than stale raw T0. Its private contribution joins
  before seal; retained completion reconstructs/re-admits current DATA with fresh
  source/output checks. Autonomous changes cannot be relabeled no-change to pass.
- Joint publication contributes report consumption/history before seal/take to the
  existing common owner and reuses its one stable calculation, never an after-seal
  sidecar. Real joint-consumer runtime qualification remains pending; this model
  records architecture, not GREEN evidence or whole-feature acceptance.
- Local QTE cannot consume a living-world report or introduce an additional
  world/faction cycle. Such work belongs to an ordinary assessed turn. Already due
  backlog alone does not prohibit a short local action crossing no additional cycle.
  No retained ordinary report or receipt grants local-QTE publication authority.

The joint completion marker retains the accepted declaration and exact history
footprint for recovery. Portable hashes bind integrity DATA, not cryptographic
provenance against a same-user coordinated rewrite of the affected joint completion
marker and living-world history. These two relevant client-owned witnesses can be
rewritten coherently without changing the authenticated original or clocks. A
single-file rehashed-history refusal does not prove resistance to that two-witness
rewrite. Fresh ownership/source/effect checks remain required in supported flows.

## Limits that remain visible

The client verifies completeness and consistency of declared evidence, not
causal truth of free prose or a simulator's exhaustive event forecast. It does
not prove that a no-change reason omitted no possible event. A before/final
comparison cannot prove an intermediate A-to-B-to-A change without an existing
domain journal. These boundaries are not permission to accept missing fields,
wrong identities, stale source, malformed audit, or invented mechanical effects.
