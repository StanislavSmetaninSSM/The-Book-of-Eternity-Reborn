# Data Model: Complete Faction Materialization

## 1. Common receipt

Every new or promoted faction contains exactly one private
`materialization` object:

```json
{
  "materialization": {
    "schemaVersion": 1,
    "materializationId": "fmat_northern_guild_184",
    "factionType": "mortal_faction",
    "factionId": "faction_northern_guild",
    "materializedAtTurn": 184,
    "state": "complete",
    "capabilities": {},
    "sections": {}
  }
}
```

### Scalar rules

| Field | Type | Rule |
|---|---|---|
| `schemaVersion` | integer | Exactly `1` |
| `materializationId` | string | Non-empty, stable, unique among all validated Mortal and Shining factions |
| `factionType` | string | Exactly `mortal_faction` or `shining_faction` |
| `factionId` | string | Exact case-sensitive effective/canonical faction identity |
| `materializedAtTurn` | integer | Accepted turn, `>= 0` |
| `state` | string | Exactly `complete` |
| `capabilities` | object | Closed profile-specific boolean object |
| `sections` | object | Closed profile-specific disposition object |

The envelope rejects unknown or duplicate members at every closed level. Its
semantic JSON value is immutable after first acceptance. Formatting and object
member order are not semantic.

Capabilities and dispositions describe the accepted creation/promotion
snapshot, not live mutable faction content. Their evidence is checked with full
consistency against the raw full carrier only on
`new`/`legacy_promotion`, before same-turn narrow commands normalize. Canonical
post-normalization validation checks receipt shape/continuity and complete live
state but defers snapshot-to-live evidence, because the supported gameplay
mutation that triggered promotion may already have changed a section in that
same accepted turn. Later narrow updates likewise preserve the receipt
semantically while existing live-state validators govern changed projects,
relations, resources, leadership, memory, trade, and other runtime surfaces.

### Section disposition

Each required section is exactly one of:

```json
{ "state": "populated" }
```

or:

```json
{
  "state": "empty_by_design",
  "reason": "The newly formed guild has not established external relations yet."
}
```

`populated` forbids `reason`. `empty_by_design` requires a non-whitespace
setting-authored reason and exact canonical emptiness. Unknown members,
additional states, `null`, omission, and contradictory content fail.

## 2. Touch classification

The classifier consumes duplicate-sensitive validated pre-turn authority plus
raw current output.

| Classification | Pre-turn state | Current accepted-turn evidence | Required behavior |
|---|---|---|---|
| `new` | Exact ID absent | Supported creation carrier/route creates the ID | Complete materialization required |
| `legacy_promotion` | Exact ID present without receipt | Any GM-authored semantic or gameplay mutation touches the faction | Complete materialization required in the same turn |
| `already_materialized` | Exact ID present with receipt | Any current state, including a GM-authored mutation | Historical receipt unchanged; any authored update uses only narrow command/route authority |
| `client_derived_only` | Exact ID present | Only documented client-owned projections change | Preserve receipt/legacy state; no promotion |
| `untouched_legacy` | Exact ID present without receipt | No GM-authored touch | Load and preserve without receipt |

GM-authored touch includes core fields, structure, resources, relations,
projects, custom state, chronicles, location control, NPC affiliation,
leadership, Shining political memory, story state, resident affiliation, or a
supported creation/political route.

Load, render, save/archive handling, container canonicalization, and
recalculation of `factionStrength`, derived tier, and service multiplier are not
GM-authored touches.

## 3. Mortal faction model

### 3.1 Full raw carrier

`factionDataChanges[]` remains the only full Mortal carrier. It is legal only
for `new` or `legacy_promotion`. In addition to the existing production fields,
it requires these semantic objects:

```json
{
  "purpose": "Protect independent trade through the northern passes.",
  "currentAgenda": "Reopen the winter road before the first snows.",
  "principles": [
    "Contracts bind leaders and novices alike.",
    "No caravan is abandoned beyond the walls."
  ],
  "memory": {
    "summary": "The guild survived a blockade and now distrusts royal toll keepers.",
    "lastUpdatedTurn": 184,
    "enduringFacts": [
      "The guild was founded by displaced caravan masters."
    ],
    "openThreads": [
      "The eastern warehouse debt remains unpaid."
    ]
  },
  "governance": {
    "model": "Elected trade council",
    "decisionProcess": "Five charter houses vote; ties are broken by the road marshal."
  },
  "leadership": {
    "leadershipState": "collective",
    "summary": "The five charter masters rule jointly.",
    "leaderNpcIds": [
      "npc_charter_master_vesna"
    ]
  },
  "scribeChronicle": [
    "#184 - The Northern Guild entered canonical faction authority."
  ]
}
```

#### Mortal leadership invariants

| State | `leaderNpcIds` | Meaning |
|---|---|---|
| `headed` | One or more exact IDs | Named individual leadership |
| `collective` | Zero or more exact IDs | Council, distributed office, or non-person institution; `summary` explains it |
| `vacant` | Exactly `[]` | Explicit current vacancy |

Every supplied NPC ID resolves to exact Mortal NPC authority. A same-turn new
leader must satisfy Actor Materialization independently; no actor data is
duplicated in the faction.

### 3.2 Mortal capabilities and sections

The exact one-to-one mapping is:

| Capability | Section | `true` evidence | `false` evidence |
|---|---|---|---|
| `hasFormalHierarchy` | `hierarchy` | Structure entry has at least one complete branch/rank or structured bonus | Governance/leadership remain complete; `ranks.branches=[]`, `structuredBonuses=[]` |
| `usesFactionResources` | `resources` | At least one production-valid meta resource or strategic good | Exact resource entry with both arrays empty |
| `maintainsRelations` | `relations` | At least one complete relation | Exact `relations=[]` |
| `runsProjects` | `projects` | At least one target-bound active/completed project | Raw carrier has both arrays and both are empty; no canonical target rows |
| `holdsTerritoryOrInfluence` | `territoryAndInfluence` | At least one complete territory/influence/location-control link | Exact `controlledTerritories=[]` and no target control link |
| `supportsPlayerMembership` | `playerMembership` | Player-faction or member/rank/reputation authority is active and consistent | Exact non-member state below |
| `usesCustomMechanics` | `customStates` | At least one production-valid custom state | Exact custom entry with `customStates=[]` |

Exact non-member state:

```json
{
  "isPlayerFaction": false,
  "isPlayerMember": false,
  "playerRank": null,
  "playerBranch": null,
  "playerStrategyDirective": null,
  "reputation": 0,
  "reputationDescription": null
}
```

When `supportsPlayerMembership=true`, the existing rank, branch, leadership,
reputation, and player-faction invariants determine which values are required.

### 3.3 Canonical Mortal ownership

| Authority | Fields/records owned for materialized factions |
|---|---|
| `faction_core.json.factions[]` | Identity/display, `purpose`, `currentAgenda`, `principles`, `memory`, power/progression, player membership/reputation, `relations`, territory summary, immutable `materialization` |
| `faction_structure.json.entries[]` | Exact faction identity, `governance`, `leadership`, `ranks`, `structuredBonuses` |
| `faction_resources.json.entries[]` | Exact identity, `metaResources`, `strategicGoods` |
| `faction_projects.json` | Target-bound active/completed rows; raw carrier preserves explicit emptiness evidence |
| `faction_custom.json.entries[]` | Exact identity and `customStates` |
| `faction_chronicles.json.entries[]` | Exact identity and turn-anchored history |
| Mortal location authority | Exact faction-control/territory references |
| Mortal NPC authority | Exact faction affiliation IDs and roles |

For a new/promoted faction, carrier-only sidecar payloads are extracted and
removed from canonical core. Untouched legacy faction objects retain their
read-compatible shape until promotion.

### 3.4 Chronicle transition

- New faction: `scribeChronicle` contains at least one valid entry.
- Promotion with existing history: existing target-bound canonical chronicle
  satisfies initial-memory history; carrier must not resend that history.
- Promotion without history: `scribeChronicle` contains at least one valid
  promotion/initial entry.
- Ordinary update: `scribeChronicle` is forbidden;
  `factionChronicleUpdates[]` appends one entry.

The normalizer binds creation entries to the effective identity, emits
deterministic canonical entries, and consumes `scribeChronicle`.

## 4. `FactionCoreChanges`

Root response field:

```json
{
  "factionCoreChanges": [
    {
      "factionId": "faction_northern_guild",
      "reason": "The council adopted a winter-road mandate.",
      "purposeAndPrinciples": {
        "purpose": "Protect independent trade through the northern passes.",
        "currentAgenda": "Reopen the winter road before the first snows.",
        "principles": [
          "Contracts bind leaders and novices alike."
        ]
      }
    }
  ]
}
```

### Closed groups

| Group | Absolute resulting fields |
|---|---|
| `profile` | `name`, `description`, `image_prompt`, `factionColor` |
| `purposeAndPrinciples` | `purpose`, `currentAgenda`, `principles` |
| `progressionAndPower` | `level`, `experience`, `experienceForNextLevel`, `developmentArchetype`, optional complete `customArchetypePriorities`, complete `powerProfile` |
| `governanceAndLeadership` | Complete `governance` and complete `leadership` |
| `playerMembership` | Complete player-faction/member/rank/branch/directive/reputation group |
| `relations` | Complete absolute `relations[]` snapshot with exact target IDs |

At least one group is required. Partial group objects, delta expressions,
unknown members, `factionId` changes, `initialId`, `isNewFaction`,
`materialization`, sidecar fields, `scribeChronicle`, location control, and NPC
affiliation payloads are forbidden.

Command targets must already be materialized permanent Mortal factions. A
legacy target must be promoted through the full carrier in the same turn before
an independent gameplay mutation may apply.

## 5. Shining faction model

### 5.1 Mandatory semantic core

A new/promoted Shining faction requires:

```json
{
  "factionId": "shine_faction_dawn_archive",
  "originType": "native_radiant",
  "hallId": "hall_dawn_archive",
  "creationProvenance": {
    "route": "native_discovery",
    "authorityType": "shining_core_action_request",
    "authorityId": "request_discover_dawn_archive"
  },
  "charter": {
    "factionName": "Dawn Archive",
    "favoredArchetype": "remembrance",
    "patronEffectFamily": "memory",
    "summary": "Preserve the truths carried into light."
  },
  "currentAgenda": "Recover the names erased from the western gallery.",
  "visibility": "revealed",
  "storyAuthority": null,
  "factionLifecycle": {
    "state": "active"
  },
  "leadership": {
    "leadershipState": "secure",
    "headActorType": "shining_faction_head",
    "headActorId": "radiant_archivist_elya"
  },
  "strategicMemory": {
    "summary": "The Archive remembers the first dimming.",
    "lastUpdatedTurn": 184,
    "recentCampaigns": [],
    "losses": [],
    "alliances": [],
    "enemies": []
  },
  "chronicle": [
    {
      "entryId": "shine_chronicle_dawn_archive_founding",
      "turnNumber": 184,
      "eventType": "faction_materialized",
      "summary": "The Dawn Archive opened its hall.",
      "visibility": "known",
      "consequences": []
    }
  ]
}
```

The route is closed to `native_discovery`, `player_founding`, and `story`.
`authorityType` must match the route:

| Route | Authority |
|---|---|
| `native_discovery` | Exact core-action or legacy native-discovery request/receipt ID |
| `player_founding` | Exact pending founding request/receipt ID |
| `story` | Exact supported story contract ID, including guardian-ascension authority where applicable |

`visibility` is exactly `revealed`, `rumored`, or `hidden`.
`storyAuthority` is exact `null` for non-story creation. Story creation requires:

```json
{
  "authorityType": "saref_main_story",
  "authorityId": "shine_faction_wings",
  "factionRole": "wings_of_angels"
}
```

Supported authority types are closed:

| Type | Authority ID | Additional exact proof |
|---|---|---|
| `saref_main_story` | `main_story_saref_state.json.factionLinks.wingsFactionId` | The authority ID also equals the enclosing faction ID; `factionLinks.visibility`, generic `visibility`, and legacy `sarefVisibility` match; `storyAuthority.factionRole=sarefFactionRole=wings_of_angels`, including before reveal |
| `guardian_ascension` | Exact `guardianId` from canonical `guardians.json.activeGuardian`/`guardians[]` union | `originType=ascended_guardian`, role is `patron_guardian`, visibility is `revealed`, leadership points to that Guardian, and its Actor Materialization is complete |

`creationProvenance.authorityType/authorityId` and
`storyAuthority.authorityType/authorityId` are identical for the `story`
route. No arbitrary prose authority, additional story file, or Guardian-name
derivation is accepted.

### 5.2 Shining capabilities and sections

| Capability | Section | Evidence |
|---|---|---|
| `runsProjects` | `projects` | Direct disposition evidence from `projects[]` |
| `holdsTerritorialInfluence` | `territorialInfluence` | Direct evidence from `territorialInfluence[]` |
| `usesResourceLedger` | `resourceLedger` | Direct evidence from `resourceLedger[]` |
| `hasResidentAffiliations` | `residentAffiliations` | Exact resident-owned `shiningFactionId` links |
| `canTrade` | `trade` | Operational lifecycle, leadership, derived tier, and realm-local trade rules; not a direct disposition bit |
| `hasLeadershipHistory` | `leadershipHistory` | Direct evidence from `leadershipHistory[]` and `leadershipReceipts[]` |
| `usesStoryState` | `storyState` | Direct evidence from non-null exact `storyAuthority` and matching canonical story state |

Exact empty surfaces are:

- `projects=[]`;
- `territorialInfluence=[]`;
- `resourceLedger=[]`;
- no resident whose `shiningFactionId` targets the faction;
- `tradeInventory=null` and `tradeInventoryReceipts=[]`;
- `leadershipHistory=[]` and `leadershipReceipts=[]`;
- `storyAuthority=null`.

The `trade` disposition records current inventory/history content and can differ
from `canTrade`. For example, an eligible new faction may have
`canTrade=true` and `trade=empty_by_design`; a broken faction may have
`canTrade=false` with populated historical receipts.

### 5.3 Route-specific additions

#### Native discovery

- exactly one new hall;
- exactly one `native_radiant` faction;
- two through four new ascended residents targeting the faction;
- exactly two newly identified completed projects;
- exact core-action/legacy request, receipt, costs, and constrained diff;
- complete Actor Materialization for new residents, non-player head, and new
  political actors.

#### Player founding

- exact pending request and unique request ID;
- exact proposed faction/hall/charter/supporters;
- `originType=player_founded`;
- secure `player_soul` leadership;
- exact reserved Ink Feather and Light Spark audit;
- exactly one matching root `factionFoundingReceipts[]` entry;
- founding leadership-history evidence and resident-owned supporter links.

#### Story/hidden

- `creationProvenance.route=story`;
- supported non-null `storyAuthority`;
- exact matching story state/reference;
- `visibility=rumored|hidden|revealed` as allowed by that authority;
- all normal hall, charter, agenda, lifecycle, leadership, memory, chronicle,
  section, and actor requirements.

### 5.4 Actor and hall references

- Every faction has exactly one `hallId` resolving to one canonical hall.
- A non-vacant non-player head resolves to an exact afterlife actor profile with
  complete Actor Materialization.
- `player_soul` resolves to the existing client-owned profile and needs no
  envelope.
- Vacant leadership has `headActorType=null` and `headActorId=null`.
- Every newly significant resident and political actor resolves to complete
  Actor Materialization.
- Resident membership is proven from resident state and is not copied into a
  faction-local roster.

## 6. Validation issue model

All faction materialization issues use:

- `section = "FactionMaterialization"`;
- `actor = "mortal_faction:<id>"` or
  `actor = "shining_faction:<id>"`;
- a stable issue family from the approved specification;
- exact `path`, `expected`, `actual`, and preservation-oriented `repairHint`.

Domain validators may add narrower suffixes while repair routing continues to
match the stable family and coordinate.

## 7. State transitions

```text
absent
  └─ supported creation + complete bundle ─> materialized

legacy (no receipt)
  ├─ load/client projection ───────────────> legacy
  └─ GM touch + complete promotion ────────> materialized

materialized
  ├─ narrow valid command/route ───────────> materialized
  ├─ client projection ────────────────────> materialized
  ├─ receipt mutation/removal ─────────────> rejected/repair
  └─ full-object resend ───────────────────> rejected/repair
```

Acceptance is atomic. Any failed member of the new/promotion bundle enters the
existing repair/rollback loop; no partial faction or sidecar persistence is
accepted.
