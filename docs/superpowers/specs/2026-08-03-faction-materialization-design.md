# Faction Materialization Design

Date: 2026-08-03

Status: Approved for implementation planning

Issue: [#1510](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1510)

Related: [#1500](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500), [#1222](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1222), [#1368](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1368), [#1462](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1462)

Branch: `1510-faction-materialization-design`

## Decision

The project will introduce one setting-agnostic Faction Materialization
Contract with a shared kernel and two domain profiles:

1. Mortal World factions stored across the existing faction core and sidecar
   files.
2. Shining Abode factions stored inside canonical Shining Abode state.

Every newly created faction must be semantically complete on the accepted turn
that creates it. Every legacy faction without materialization authority remains
loadable while untouched, but its first accepted GM-authored mutation must
promote it to the current complete contract in that same turn.

Completeness does not require every optional collection to contain invented
content. A governed section may be empty only when its canonical empty surface
is physically present and its materialization disposition is
`empty_by_design` with a meaningful in-world reason.

The client may derive mechanical projections, but neither normalization nor
repair may invent faction purpose, agenda, principles, leadership, visibility,
memory, behavior, or capabilities from names, descriptions, IDs, tags, or
genre vocabulary.

## Problem

Current validation proves many local shapes without proving that a faction was
fully authored when it first became canonical.

For Mortal factions, `factionDataChanges` already resembles a full-object
carrier, but completeness is split across:

- `game_state/factions/faction_core.json`;
- `game_state/factions/faction_structure.json`;
- `game_state/factions/faction_resources.json`;
- `game_state/factions/faction_projects.json`;
- `game_state/factions/faction_custom.json`;
- `game_state/factions/faction_chronicles.json`;
- location-control references;
- NPC faction-affiliation references.

A valid-looking core object therefore does not prove that the faction has
deliberate hierarchy, resources, relations, projects, territory, membership,
custom mechanics, or initial memory state.

For Shining factions, the runtime already validates identity, origin, charter,
leadership, lifecycle, projects, receipts, political memory, trade, and several
cross-file relations. It does not distinguish a deliberately complete first
materialization from a historical or partially defaulted object. In
particular, normalization can currently supply semantic-looking defaults or
empty surfaces before final validation. This can launder a hollow creation into
a structurally valid canonical faction.

The absence of a creation/promotion boundary also leaves full-object carriers
available as an accidental update channel for existing factions, bypassing
narrower mutation authority.

## Goals

- Make every new Mortal and Shining faction complete, inspectable, and usable
  by gameplay systems on its first accepted turn.
- Permit legitimate empty sections without forcing the client or GM to invent
  content.
- Preserve load compatibility for untouched legacy saves.
- Require complete, bounded promotion when a legacy faction first receives an
  accepted GM-authored mutation.
- Separate first creation/promotion from ordinary existing-faction updates.
- Validate raw authored semantics before normalization.
- Bind all domain files, receipts, actors, halls, residents, locations, and NPC
  affiliations through exact IDs.
- Produce focused repair packets that preserve valid authored data.
- Keep materialization metadata private to the harness and canonical state.
- Reuse the principles of Actor Materialization without treating a faction as
  an actor.

## Non-goals

- Implementing the faction-content worker proposed by #1222.
- Implementing autonomous Mortal or afterlife living-world scheduling.
- Implementing the broader Mortal living-world behavior tracked by #1462.
- Treating Chaos Sea Guardian politics as a faction entity.
- Changing player-facing faction UI or exposing materialization metadata.
- Defining a universal fantasy hierarchy, economy, government, power model, or
  ideology.
- Migrating every historical faction eagerly during load.
- Increasing test timeouts, increasing test-process concurrency, or restoring
  an unbounded all-tests workflow.

## Common Materialization Kernel

### Canonical envelope

Each materialized canonical faction contains one private `materialization`
object:

```json
{
  "materialization": {
    "schemaVersion": 1,
    "materializationId": "mat_faction_ashen_archive_turn_42",
    "factionType": "mortal_faction",
    "factionId": "faction_ashen_archive",
    "materializedAtTurn": 42,
    "state": "complete",
    "capabilities": {
      "hasFormalHierarchy": true,
      "usesFactionResources": true,
      "maintainsRelations": false,
      "runsProjects": true,
      "holdsTerritoryOrInfluence": false,
      "supportsPlayerMembership": false,
      "usesCustomMechanics": false
    },
    "sections": {
      "hierarchy": { "state": "populated" },
      "resources": { "state": "populated" },
      "relations": {
        "state": "empty_by_design",
        "reason": "Фракция только возникла и ещё не установила отношений."
      },
      "projects": { "state": "populated" },
      "territoryAndInfluence": {
        "state": "empty_by_design",
        "reason": "Архив действует через странствующих хранителей и не удерживает территорию."
      },
      "playerMembership": {
        "state": "empty_by_design",
        "reason": "Орден пока не принимает внешних участников."
      },
      "customStates": {
        "state": "empty_by_design",
        "reason": "У фракции нет отдельной механики помимо общих систем."
      }
    }
  }
}
```

Common fields have these rules:

| Field | Rule |
|---|---|
| `schemaVersion` | Exactly `1` for the initial contract |
| `materializationId` | Non-empty, stable, and unique within the save |
| `factionType` | Exactly `mortal_faction` or `shining_faction` |
| `factionId` | Exact effective faction identity |
| `materializedAtTurn` | Exact non-negative accepted-turn number |
| `state` | Exactly `complete`; partial canonical envelopes are forbidden |
| `capabilities` | Exact closed boolean schema for the selected domain profile |
| `sections` | Exact closed disposition schema for the selected domain profile |

For a new same-turn Mortal faction, raw validation binds
`materialization.factionId` to the carrier's exact `initialId`. Canonicalization
must then leave the envelope bound to the exact resulting permanent
`factionId`. Existing and promoted factions always use their permanent
`factionId`.

Unknown envelope members, unknown capabilities, unknown section names,
duplicate JSON members, empty reasons, and identity mismatches fail closed.
Reasons are private authoring evidence and are not automatically rendered as
player-facing prose.

### Section dispositions

A governed section has exactly one of these forms:

```json
{ "state": "populated" }
```

or:

```json
{
  "state": "empty_by_design",
  "reason": "Meaningful in-world explanation."
}
```

`populated` forbids `reason` and requires the complete canonical section to
contain production-valid data. `empty_by_design` requires a non-empty reason
and the exact canonical empty array, object, or nullable surface defined by the
domain profile. Omission, accidental `null`, a missing sidecar record, and an
empty stand-in object are not deliberate emptiness unless that exact shape is
the canonical empty representation for the section.

The envelope is a persistent receipt of the faction's first complete state.
Its identity, initial capability snapshot, and section dispositions remain
stable during ordinary later updates. Current gameplay validity continues to
be enforced by the existing dedicated commands and canonical validators; the
receipt is not silently rewritten to describe later history.

### Classification

Before normalization, every touched faction is classified against the
duplicate-sensitive validated pre-turn snapshot:

| Classification | Required behavior |
|---|---|
| New | No exact pre-turn faction exists; require the complete domain profile and new envelope |
| Legacy promotion | Exact pre-turn faction exists without an envelope and receives a GM-authored mutation; require one complete promotion |
| Already materialized | Exact pre-turn faction has an envelope; reject full-object resend and use narrow commands |
| Client-derived only | Only explicitly client-owned projections are recomputed; preserve authored state and do not force promotion |
| Untouched legacy | Load and preserve without inventing data or requiring migration |

Newness and promotion are determined only from exact validated pre-turn
authority and accepted mutation channels. A permanent-looking ID, reused name,
description, tag, or temporary-ID collision cannot disguise an existing
faction as new.

Loading a save, rendering UI, rebuilding read models, and recalculating
explicit client-owned projections do not trigger promotion. Any accepted
GM-authored change to faction core, structure, resources, relations, projects,
custom state, chronicle, territory/influence, location control, membership,
leadership, Shining political memory, or exact faction-owned affiliation does.

## Mortal Faction Profile

### Always-populated semantic core

A new or promoted Mortal faction must always contain:

- exact identity, display profile, description, color/visual identity, and the
  existing production-valid English `image_prompt` where required;
- explicit purpose, current agenda, and principles;
- a production-valid, setting-authored power profile and progression state;
- explicit governance and leadership state, including a structured vacant or
  distributed-leadership representation when no individual leader exists;
- initialized faction memory and at least one initial chronicle entry tied to
  the accepted turn.

These are mandatory core semantics and are not converted into
`empty_by_design` dispositions.

### Governed sections and capabilities

The Mortal envelope has exactly these governed section keys:

- `hierarchy`;
- `resources`;
- `relations`;
- `projects`;
- `territoryAndInfluence`;
- `playerMembership`;
- `customStates`.

Its capability snapshot has exactly these keys:

- `hasFormalHierarchy`;
- `usesFactionResources`;
- `maintainsRelations`;
- `runsProjects`;
- `holdsTerritoryOrInfluence`;
- `supportsPlayerMembership`;
- `usesCustomMechanics`.

Capabilities describe the faction's authored operational model at first
materialization. They must agree with the same-turn canonical bundle. For the
Mortal profile, each capability is the direct evidence bit for the
corresponding section in the order listed above: `true` requires `populated`
and production-valid content; `false` requires `empty_by_design` and the exact
canonical empty surface. A capability cannot be granted by prose, and a
`false` capability cannot coexist with active canonical content for that
system.

### Atomic canonical bundle

The materialization boundary spans one exact `factionId` across:

| Authority | Materialization responsibility |
|---|---|
| `faction_core.json` | Identity, semantic core, power/progression, membership summary, envelope |
| `faction_structure.json` | Governance, leadership, branches, ranks, relations where currently owned |
| `faction_resources.json` | Meta-resources and strategic goods |
| `faction_projects.json` | Active and completed faction projects |
| `faction_custom.json` | Faction-specific states and mechanics |
| `faction_chronicles.json` | Initialized memory/chronicle and later history |
| Location authority | Exact territory, influence, and faction-control links |
| NPC authority | Exact NPC-to-faction affiliations and roles |

The accepted turn must produce a mutually consistent bundle. If a governed
section is empty, its exact canonical empty carrier still exists. A sidecar
record cannot be omitted merely because its arrays are empty. Every populated
cross-file reference resolves to the same permanent or same-turn effective
identity, and no orphaned mirror is accepted.

### Creation, promotion, and ordinary updates

`factionDataChanges` remains the full Mortal faction carrier, but it is legal
only for:

1. genuine first creation; or
2. the first complete promotion of an exact legacy faction.

Promotion preserves every valid historical field and sidecar entry. It may add
the missing semantic core, exact empty surfaces, envelope, and required
cross-file bindings. A simultaneous gameplay mutation still uses its dedicated
command when one exists; promotion is not authority to rewrite unrelated
history, resources, ranks, projects, custom states, affiliations, or
chronicles.

Ordinary existing-faction core updates use a new closed
`FactionCoreChanges` command. Each entry:

- targets exactly one existing permanent `factionId`;
- contains a non-empty reason;
- uses absolute resulting values rather than patch expressions;
- permits only reviewed core groups such as profile/visual identity,
  purpose-agenda-principles, power/progression, governance/leadership, and
  player-membership core state;
- rejects unknown members recursively;
- protects identity and `materialization`;
- cannot mutate sidecars or fields owned by dedicated commands.

Existing dedicated rank, resource, relation, project, custom-state, chronicle,
location-control, and NPC-affiliation commands retain their authority. A valid
`FactionCoreChanges` entry is reduced into canonical state and consumed; an
invalid entry remains visible for repair. A full object for an already
materialized faction is rejected even when most values are unchanged.

## Shining Faction Profile

### Always-populated semantic core

A new or promoted Shining faction must always contain:

- exact identity and supported creation provenance;
- exact binding to one canonical hall;
- complete charter and purpose;
- explicit `factionLifecycle`;
- explicit leadership, including structured vacancy where applicable;
- initialized strategic memory and at least one initial chronicle entry;
- supported visibility and story authority.

The normalizer must not manufacture origin, charter specialization,
leadership, visibility, agenda, or strategic memory. Missing authored semantics
must fail raw validation before defaults can hide the omission.

### Governed sections and capabilities

The Shining envelope has exactly these governed section keys:

- `projects`;
- `territorialInfluence`;
- `resourceLedger`;
- `residentAffiliations`;
- `trade`;
- `leadershipHistory`;
- `storyState`.

Its capability snapshot has exactly these keys:

- `runsProjects`;
- `holdsTerritorialInfluence`;
- `usesResourceLedger`;
- `hasResidentAffiliations`;
- `canTrade`;
- `hasLeadershipHistory`;
- `usesStoryState`.

The capability snapshot is checked against exact canonical state and existing
mechanical authority. `runsProjects`, `holdsTerritorialInfluence`,
`usesResourceLedger`, `hasResidentAffiliations`, `hasLeadershipHistory`, and
`usesStoryState` are direct evidence bits for their corresponding section:
`true` requires `populated`; `false` requires `empty_by_design` and its exact
empty surface.

`canTrade` is the one non-disposition capability. It is not inferred from a
faction name or charter: it follows the existing operational lifecycle,
leadership, derived trade-tier, and realm-local rules. The separate `trade`
disposition records whether current inventory or trade-history content exists.
A currently trade-capable faction may still have an `empty_by_design` trade
section when no inventory cycle has yet been materialized; the required reason
must say so. A defeated or otherwise ineligible faction may retain populated
historical trade receipts while `canTrade=false`.

### Creation routes

The common profile does not erase route-specific contracts.

#### Native discovery

An accepted `discover_native_faction` closure must atomically create:

- one new hall;
- one new native Shining faction bound to that hall;
- two through four new ascended residents;
- exactly two seeded completed faction projects;
- the matching request/receipt and cost audit required by the active core-action
  or legacy native-discovery contract;
- complete Actor Materialization authority for every newly significant
  non-player actor.

The constrained discovery diff must not rewrite pre-existing halls, factions,
projects, residents, political actors, or unrelated Soul state.

#### Player founding

An accepted `pending_shining_faction_foundings.json` closure must bind:

- the exact pending request and unique request ID;
- the authored charter and supported founding provenance;
- the exact supporter residents and their existing conflict/locking rules;
- `player_soul` as the faction head;
- the already reserved Ink Feather and Light Spark costs;
- one matching `factionFoundingReceipts[]` entry with exact quoted-cost audit;
- the complete hall, faction, affiliation, history, and materialization state.

The founding route cannot create a second unresolved player-soul leadership
conflict or substitute Mortal `factionDataChanges`.

#### Story or hidden creation

A story-owned or hidden faction must have exact documented story authority,
supported provenance, and a valid visibility state. Hidden is a visibility
contract, not permission to omit charter, leadership, hall, memory, lifecycle,
or materialization. The validator must use exact story IDs, receipts, or
contract fields; it must not infer story ownership from a secretive name,
description, tag, or archetype.

### Actor, resident, and hall bindings

Every non-vacant non-player head, political actor, and newly significant
resident resolves by exact type and ID to complete Actor Materialization
authority from #1500. Vacant leadership requires no head actor. Player-soul
leadership resolves to the existing client-owned player profile.

Resident membership remains resident-owned. The faction envelope proves the
resident-affiliation section and exact cross-links, but it does not duplicate
an independent resident roster inside the faction merely to satisfy
materialization.

Each faction binds to exactly one existing or same-turn new hall. A hall cannot
be silently selected by display name. Project, influence, ledger, trade,
leadership-history, and receipt records all target the same exact faction ID.

### Derived values

`factionStrength`, derived faction tier, and service multiplier remain
client-owned projections of canonical authored inputs. Recalculation alone is
`client-derived only` and does not promote an untouched legacy faction.

Normalization may:

- canonicalize explicitly supported aliases;
- bind validated same-turn temporary identities;
- calculate documented numeric projections;
- preserve or initialize purely mechanical containers where the contract
  explicitly defines them as client-owned.

Normalization may not:

- invent origin, charter, purpose, agenda, principles, leadership, visibility,
  memory, chronicle prose, or empty-by-design reasons;
- add a missing materialization envelope;
- choose a hall, head, resident, political actor, or story authority;
- infer capabilities from prose;
- turn an omitted semantic section into an accepted empty section.

## Validation and Acceptance Flow

The accepted-turn pipeline is:

1. Parse current and validated pre-turn faction authority with duplicate-member
   detection and exact identity maps.
2. Classify each touched faction as new, legacy promotion, already
   materialized, client-derived-only, or untouched legacy.
3. Validate raw GM-authored carriers and candidate state before semantic
   normalization.
4. For new and promoted factions, require the complete envelope, domain
   semantic core, governed dispositions, capabilities, all required sidecars,
   route receipts, and cross-file bindings in the same turn.
5. Reject an already materialized full-object resend and require the exact
   narrow command.
6. Run only permitted structural and mechanical normalization.
7. Validate the resulting canonical shape, envelope identity, bundle
   consistency, route invariants, actor/hall/resident/location bindings, and
   client-derived projections.
8. Accept atomically, or enter the existing bounded repair/rollback path.

No invalid materialization may be partially persisted. Failure in one member
of the bundle rejects the entire accepted-turn mutation.

## Repair Contract

Every issue uses one stable coordinate:

- `mortal_faction:<factionId>`;
- `shining_faction:<factionId>`.

Same-turn new Mortal factions use the exact effective `initialId` until
canonical identity binding completes.

The initial stable issue-code families are:

- `faction_materialization_missing`;
- `faction_materialization_invalid`;
- `faction_materialization_identity_mismatch`;
- `faction_materialization_section_missing`;
- `faction_materialization_disposition_mismatch`;
- `faction_materialization_capability_mismatch`;
- `faction_materialization_bundle_incomplete`;
- `faction_materialization_cross_reference_invalid`;
- `faction_legacy_promotion_required`;
- `faction_existing_full_resend_forbidden`.

Domain-specific validation may add a suffix or a narrower code, but repair
routing must remain based on the exact faction coordinate and exact target
surface rather than display names.

A repair packet identifies:

- faction type and exact identity;
- creation/promotion classification;
- stable issue code;
- exact target file and faction selector;
- exact missing or contradictory semantic section;
- allowed disposition forms;
- required route, receipt, actor, hall, resident, location, or NPC references;
- read-only valid sections that must be preserved;
- prohibited unrelated roots and commands.

For Mortal factions, a packet may target one or more exact faction sidecars
only when the same materialization bundle requires them. For Shining factions,
it targets the exact faction subtree and exact related state records inside
`game_state/meta/shining_abode_state.json` plus an exact external actor profile
when that cross-link is the defect.

Repair never asks for a wholesale rewrite when only one section is missing or
contradictory. It preserves every valid authored section, does not change
another faction, and does not generate narrative content. The future
faction-content worker in #1222 may consume this packet contract, but #1510
does not implement or enable that worker.

## Chaos Sea Guardian Politics Boundary

Chaos Sea Guardian Politics is not Faction Materialization.

Guardians are actors and remain governed by Actor Materialization in #1500.
Their `relations[]`, `projects[]`, `influenceZones[]`, `chronicle[]`, privacy,
consequences, player role, and Saref links belong to the afterlife living-world
politics model. Existing completed Guardian-politics work remains authoritative;
live-world behavior and enforcement findings belong under #1368.

If live testing reveals that Guardian politics is not advancing or is missing
a validator, the remedy is a focused bug linked to #1368. It must not create a
fake Mortal or Shining faction or broaden #1510. Mortal living-world
progression remains separately tracked by #1462.

## Documentation and Client Boundaries

Implementation must keep runtime behavior and GM-facing authoring guidance
synchronized. Expected documentation surfaces include:

- `Rules/Block_21.txt` and relevant faction sub-blocks;
- `CLI_API_Specification.md`;
- `CLI_Agent_Daemon_Specification.md`;
- `TaskGuides/CLI_Step_Main.txt`;
- `Examples/E_CLI_Step_Main.txt`;
- `OtherGuides/Afterlife_Contract_Matrix.md`;
- `docs/audits/afterlife/shining-abode/Shining_Abode_Faction_Politics_Addendum.md`;
- `Examples/E_CLI_Afterlife_Turns.txt`;
- `Examples/example_validation_manifest.json`;
- documentation coverage and source-guard tests.

Worked examples must include:

- one populated Mortal first materialization;
- one minimal Mortal first materialization with exact empty surfaces and
  meaningful reasons;
- one Mortal legacy promotion;
- one ordinary `FactionCoreChanges` update;
- one Shining native discovery;
- one player-founded Shining faction;
- one supported story/hidden Shining faction;
- one bounded repair for each domain.

Console and browser readers continue to consume canonical gameplay state. They
must not display schema version, materialization ID, section-state tokens,
private empty-by-design reasons, or other harness metadata in ordinary
player-facing views. Existing readable legacy factions remain compatible.

## Verification Strategy

Implementation follows focused RED/GREEN work:

1. Add failing focused tests for common envelope parsing, exact identity,
   duplicate members, capabilities, dispositions, classification, and
   normalization ordering.
2. Add Mortal creation, promotion, atomic-bundle, full-resend, and
   `FactionCoreChanges` tests before implementing each behavior.
3. Add Shining native-discovery, player-founding, story/hidden, Actor
   Materialization binding, and semantic-default laundering tests before
   implementing each behavior.
4. Add focused repair-packet, documentation, manifest, metadata-privacy, and
   legacy-compatibility tests.
5. Run the default Fast lane once when the focused implementation is stable.
6. Run the relevant bounded FullValidation/documentation selection when the
   afterlife contract boundary changes or focused evidence requires it.
7. Run one clean-checkout PreMerge before final integration, with the existing
   fifteen-minute hard cap and the preferred target below ten minutes.

Exhaustive matrices remain explicit diagnostics. They are not run after every
edit and are not serially appended to Fast or PreMerge. Deep validation runs
only when the repository lane policy or a related failure specifically
requires it. This issue must not increase lane timeouts or test-process
concurrency.

The design worktree baseline is green: Fast passed 2,633 of 2,633 tests in
4 minutes 16 seconds with no failures, duplicates, timeout, or owned-process
cleanup failure.

## Acceptance Criteria

The implementation is acceptable when:

- every new Mortal faction has a valid envelope, mandatory semantic core,
  complete governed dispositions, capability consistency, and one atomic
  cross-file bundle;
- a valid minimal Mortal faction passes with exact empty surfaces and
  meaningful `empty_by_design` reasons;
- full Mortal faction objects are accepted only for genuine creation or legacy
  promotion;
- `FactionCoreChanges` and existing dedicated commands cover ordinary
  existing-faction updates without exposing protected fields;
- every new Shining faction satisfies its exact native-discovery,
  player-founding, or story/hidden route;
- Shining hall, actor, resident, political actor, leadership, memory,
  chronicle, influence, resource, trade, story, and receipt links are enforced
  when applicable;
- raw validation proves that normalization cannot launder missing Shining
  semantics;
- untouched legacy factions remain readable;
- the first accepted GM-authored mutation of a legacy faction requires a
  complete same-turn promotion;
- client-derived-only recomputation does not force promotion;
- repair packets are exact, bounded, preservation-oriented, and incapable of
  inventing content;
- Chaos Sea Guardian politics remains outside the faction entity contract;
- docs, prompts, examples, manifests, runtime validators, and source guards
  agree;
- ordinary console/browser UI does not expose materialization metadata;
- focused tests, one Fast checkpoint, required bounded contract validation,
  and one clean-checkout PreMerge are green within their existing limits.

## Implementation Planning Boundary

Before production edits, #1510 requires a dedicated Spec Kit feature at
`specs/1510-complete-faction-materialization/`. Its specification, research,
data model, contracts, implementation plan, and task list must trace this
design and preserve the Mortal/Shining/Guardian-politics boundaries above.

The detailed plan should sequence the common classifier and raw validation
fence first, then the Mortal profile and update authority, then the Shining
routes and cross-links, followed by repair/docs/client privacy and bounded
verification. This document authorizes planning; it does not itself authorize
implementation shortcuts or schema changes outside issue #1510.
