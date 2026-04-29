# Сияющая Обитель: implementation spec

## 1. Canonical state

Добавить основной state file:

- `game_state/meta/shining_abode_state.json`

### 1.1. Полный shape v1

```json
{
  "availability": "active",
  "radiance": {
    "experience": 0,
    "tier": 0
  },
  "lightSparks": 100,
  "halls": [],
  "factions": [],
  "pendingNativeFactionDiscovery": null,
  "gates": {
    "draftVersion": 0,
    "hasOpenDraft": false,
    "isStale": false,
    "allCandidateBlessingCards": [],
    "availableBlessingCards": [],
    "shownBlessingCardIds": [],
    "selectedBlessingCardIds": [],
    "nextCandidateCursor": 0,
    "rerollsRemaining": 0
  },
  "preparedIncarnationPackage": null
}
```

### 1.2. Top-level invariants

- `availability = active | sealed_until_next_ascension`
- `radiance.tier` is derived from `radiance.experience`
- `lightSparks` must be `0..100`
- `pendingNativeFactionDiscovery` is single-entry or `null`
- `gates.hasOpenDraft = true` means the current Shining Abode draft context exists even if `availableBlessingCards` is shorter than `draftSize`
- `gates.isStale = true` is valid only when `gates.hasOpenDraft = true`
- `gates.availableBlessingCards` must be a subset of `gates.allCandidateBlessingCards`
- `gates.shownBlessingCardIds` must contain every `cardId` currently present in `gates.availableBlessingCards`
- `gates.nextCandidateCursor` must be within `0..gates.allCandidateBlessingCards.length`
- `gates.selectedBlessingCardIds.length <= pickCap(radiance.tier)`
- `gates.selectedBlessingCardIds` must contain unique `cardId`s only
- if `gates.hasOpenDraft = true`, every `selectedBlessingCardId` must be present in current `gates.availableBlessingCards`
- `preparedIncarnationPackage` may exist only after confirmed gate selection and before next mortal-life bootstrap
- `preparedIncarnationPackage.selectedCards[]` is the only authoritative snapshot for mortal bootstrap; cards must not be reconstructed from mutable faction state
- if `preparedIncarnationPackage != null`, then `selectedCardIds.length == selectedCards.length` and for every index `i`: `selectedCardIds[i] == selectedCards[i].cardId`
- if `preparedIncarnationPackage != null`, `currentRealm` must remain `Shining Abode` until successful mortal bootstrap writes the next mortal-world realm value
- if `preparedIncarnationPackage != null`, runtime is in canonical pending-bootstrap handoff mode rather than ordinary active Shining Abode mode
- if `preparedIncarnationPackage != null`, only mortal bootstrap lifecycle is valid; ordinary Shining Abode, Chaos Sea, Guardian, and Abode interactions are invalid until the package is consumed or explicitly cleared by lifecycle logic
- consumers that branch on realm/mode must treat the pair `currentRealm = Shining Abode` + `preparedIncarnationPackage != null` as higher priority than ordinary Shining Abode mode
- ordinary Shining Abode operations must require both `currentRealm = Shining Abode` and `availability = active`, not only one of them

---

## 2. Radiance and Light Sparks

### 2.1. Tier thresholds

| Tier | XP min | XP max | Pick cap | Draft size | Supported projects | Strength cap | Rarity ceiling |
|---|---:|---:|---:|---:|---:|---:|---|
| 0 | 0 | 99 | 1 | 4 | 1 | 50 | `common` |
| 1 | 100 | 219 | 2 | 6 | 1 | 65 | `uncommon` |
| 2 | 220 | 379 | 2 | 7 | 2 | 80 | `rare` |
| 3 | 380 | 579 | 3 | 8 | 2 | 90 | `rare` |
| 4 | 580 | 999999 | 4 | 10 | 3 | 100 | `radiant` |

`radiance.tier` must be recomputed from `radiance.experience` after every XP change.

### 2.2. XP sources

| Trigger | XP delta |
|---|---:|
| Native faction discovery resolved | `+20` |
| First completion of a project archetype in current ascension for a faction | `+10` |

Additional XP rule:

- seeded `completed` projects created by native discovery never grant the `+10 XP` first-completion bonus
- only player-facing `complete_project` may append an archetype to `projectArchetypesCountedThisAscension`

### 2.3. Light Sparks rules

- on Shining Abode activation or re-activation:
  - `lightSparks = 100`
- `lightSparks` are a persistent active-Abode scarce currency across ordinary post-life returns while stored `availability = active`
- current v1 full refill points are ascension activation / re-activation and sealed re-entry after `return_to_chaos_sea`
- ordinary `reenter_shining_abode` from `Chaos Sea` preserves the current stored `lightSparks` value and does not refill it
- normal `Life Evaluation -> Chaos Sea` does not itself mutate `lightSparks`
- current v1 does model `lightSparks` as a long-run scarce currency across repeated ordinary post-life returns into the same stored active Shining Abode state
- every spendable action checks `lightSparks` first
- if `lightSparks` is insufficient:
  - action fails
  - no state mutates
- while `availability = sealed_until_next_ascension`:
  - `lightSparks`-spending actions are disallowed
  - any stored `lightSparks` value is inert and has no gameplay meaning until next activation
  - next activation after ascension overwrites `lightSparks` to `100`

---

## 3. Factions, halls and strength

### 3.1. Hall shape

```json
{
  "hallId": "hall_azalia",
  "hallName": "Зал Лунного Узора",
  "description": "short flavor only",
  "serviceTags": ["social", "lore"]
}
```

`faction.hallId` is the only authoritative hall linkage.
If `hall.factionId` exists as a convenience mirror in UI/read models, it is derived only and not part of canonical state.

### 3.2. Faction shape

```json
{
  "factionId": "faction_azalia",
  "factionName": "Дом Лунного Узора",
  "originType": "ascended_guardian",
  "headActorType": "guardian",
  "headActorId": "guardian_azalia",
  "hallId": "hall_azalia",
  "favoredArchetype": "accord",
  "patronEffectFamily": "social",
  "baseStrength": 35,
  "factionStrength": 44,
  "investCountThisAscension": 0,
  "projectArchetypesCountedThisAscension": [],
  "projects": []
}
```

Faction membership is not stored in a canonical `faction.residents[]` array.
The authoritative source of truth is resident state field `shiningFactionId`.

`residentCount` means:

- count residents where `ascensionState = ascended`
- and `shiningFactionId = factionId`

### 3.3. Base factionStrength

| Faction origin | Canonical `baseStrength` rule |
|---|---|
| `ascended_guardian` | `35`, written once when the faction is first materialized |
| `native_radiant` | computed once at discovery resolution as `55 + 5 * max(0, radiance.tier - 1)` and then written into `baseStrength` |

Native base strength is capped at `70`.

`baseStrength` is canonical faction state. Future recomputations of `factionStrength` must use stored `baseStrength`, not pending discovery history.

### 3.4. Strength formula

```text
factionStrength =
  baseStrength
  + residentBonus
  + completedProjectBonus
  + investmentBonus
```

Where:

- `residentBonus = min(15, 3 * residentCount)`
- `completedProjectBonus = sum(project.strengthReward for completed projects)`
- `investmentBonus = 8 * investCountThisAscension`
- `investCountThisAscension <= 3`
- final value is clamped to `Radiance Tier -> Strength cap`

`factionStrength` must be recomputed from this formula after:

- `invest_in_faction`
- `complete_project`
- `retire_project`
- native discovery resolution
- any resident roster change that changes `residentCount` for a faction
- re-entry on next ascension

### 3.5. Strength bands

| Band | Range | tradeTier | serviceMultiplier | rarityCeiling |
|---|---:|---:|---:|---|
| Dormant | 0-24 | 0 | `0.75` | `common` |
| Stable | 25-49 | 1 | `1.00` | `uncommon` |
| Strong | 50-74 | 2 | `1.25` | `rare` |
| Radiant | 75-100 | 3 | `1.50` | `radiant` |

`tradeTier`, `serviceMultiplier` and `rarityCeiling` must be recomputed from `factionStrength`.

### 3.6. Invest action

`invest_in_faction(factionId)`:

- cost:
  - `10 Ink Feathers`
  - `5 Light Sparks`
- requirements:
  - `currentRealm = Shining Abode`
  - `availability = active`
  - `preparedIncarnationPackage = null`
  - `investCountThisAscension < 3`
- effect:
  - `investCountThisAscension += 1`
  - recompute `factionStrength` from formula
  - if `gates.hasOpenDraft = true`:
    - set `gates.isStale = true`

---

## 4. Residents and relic logic

### 4.1. Resident state additions

In resident canonical state add:

- `ascensionState = ascended | remained_in_chaos_sea`
- `shiningFactionId`
- `residentRole = archive_support | forge_support | social_support | resource_support | descent_support`

`resident.shiningFactionId` is the only authoritative faction-membership field for residents.

### 4.2. Resident role application

Role effects apply per faction, not per resident instance.

If a faction has multiple residents with the same `residentRole`:

- apply the role effect once
- still count all residents for `residentBonus`

Resident mutation contract:

- changing `shiningFactionId` changes faction membership
- changing `residentRole`, `ascensionState` or `grantedRelicId` changes derived faction behavior even if resident count stays unchanged
- if `gates.hasOpenDraft = true`, any such change must set `gates.isStale = true`
- recompute `factionStrength` only when the change also alters `residentCount` under the canonical predicate above

### 4.3. Exact role effects

| Role | Passive effect |
|---|---|
| `archive_support` | `memory` cards of this faction get `rerolls += 1`; `revelation` projects of this faction cost `-5 feathers` |
| `forge_support` | all forge actions in that faction cost `-5 feathers` and `-5 lightSparks` |
| `social_support` | all `social` cards generated by that faction get `delta += 5`; `route` cards get `latestTurn -= 1` |
| `resource_support` | all `resource` cards generated by that faction get `money += 50` and `common += 1`; if `tradeTier >= 1`, trade stock `+1 slot` |
| `descent_support` | all `descent` cards generated by that faction get `latestTurn -= 3` and `quality += 15` |

### 4.4. Relic-bearing residents

Resident with `grantedRelicId != null` may generate a `descent` candidate card only if:

- resident is `ascended`
- resident belongs to a faction
- that faction has at least one supported completed project with `projectArchetype = passage`

Residents without relic never generate `descent` cards.

---

## 5. Native faction discovery

### 5.1. Pending shape

```json
{
  "requestId": "discover_native_faction:0007",
  "createdAtTurn": 152,
  "createdAtUtc": "2026-03-29T14:00:00Z",
  "radianceTierAtRequest": 2,
  "costFeathers": 25,
  "costLightSparks": 20
}
```

### 5.2. Rules

- valid only if `currentRealm = Shining Abode`
- valid only if `availability = active`
- valid only if `preparedIncarnationPackage = null`
- allowed only if `radiance.tier >= 1`
- at most one pending request at a time
- creating a second request while one is pending is invalid
- costs are paid immediately on request creation

### 5.3. Required GM output

When resolving `pendingNativeFactionDiscovery`, GM must materialize exactly one faction that includes:

- `originType = native_radiant`
- `1 hall` whose `hallId` is referenced by the new faction `hallId`
- `1 head`
- `2..4 resident records` with `ascensionState = ascended` and `shiningFactionId = newFactionId`
- `1 favoredArchetype`
- `1 patronEffectFamily`
- `2 completed projects`:
  - one project with `projectArchetype = favoredArchetype`
  - one project of any valid archetype

Discovery resolution contract:

- compute and persist `baseStrength` from current `radiance.tier` before discovery reward is applied
- seeded `completed` projects do not grant additional first-completion XP
- initialize `projectArchetypesCountedThisAscension = []`
- initialize `investCountThisAscension = 0`
- add `+20 radiance.experience`
- recompute `factionStrength` from stored `baseStrength`, residents and completed seeded projects
- if `gates.hasOpenDraft = true`:
  - set `gates.isStale = true`

### 5.4. Allowed favoredArchetype values

- `revelation`
- `accord`
- `provision`
- `remembrance`
- `refinement`
- `passage`
- `warding`
- `subversion`

---

## 6. Projects

### 6.1. Project shape

```json
{
  "projectId": "project:sareth:soft_dusk",
  "displayName": "Окутать Палаты Памяти мягкой тьмой",
  "summary": "A shadowed attempt to bend the mood of the halls.",
  "toneTags": ["shadowed", "forbidden", "memory"],
  "targetFactionIds": ["faction_memory_keepers"],
  "projectArchetype": "subversion",
  "outputEffectFamily": "memory",
  "tier": 2,
  "status": "completed",
  "isSupported": true,
  "strengthReward": 12,
  "completedAtTurn": 154,
  "completedAtUtc": "2026-03-29T14:05:00Z"
}
```

### 6.2. Statuses

Allowed:

- `active`
- `completed`
- `retired`

Exact meaning:

- `active`
  - project exists in authored state
  - does not contribute `strengthReward`
  - `isSupported` must be `false`
  - does not generate cards
- `completed`
  - contributes `strengthReward`
  - may set `isSupported = true`
  - may generate cards through Gates
- `retired`
  - remains in history only
  - does not contribute `strengthReward`
  - `isSupported` must be `false`
  - does not generate cards

Support is allowed only for `completed` projects.

Additional rules:

- `projectId` must be globally unique
- a faction may have multiple `completed` projects of the same `projectArchetype`
- support attaches to project instance, not to archetype
- two supported projects of the same archetype may coexist; dedupe happens later at card level only

### 6.3. Allowed archetypes

- `revelation`
- `accord`
- `provision`
- `remembrance`
- `refinement`
- `passage`
- `warding`
- `subversion`

### 6.4. Allowed outputEffectFamily values

- `lore`
- `social`
- `resource`
- `memory`
- `descent`
- `survival`
- `relic`
- `route`

### 6.5. Archetype to family compatibility

| Project archetype | Allowed output families |
|---|---|
| `revelation` | `lore`, `memory` |
| `accord` | `social`, `route` |
| `provision` | `resource`, `route` |
| `remembrance` | `memory`, `lore` |
| `refinement` | `relic`, `resource` |
| `passage` | `descent`, `route` |
| `warding` | `survival`, `social` |
| `subversion` | `social`, `lore`, `memory`, `descent` |

### 6.6. Tier table

| Tier | Cost | strengthReward | Base project-card rarity |
|---|---|---:|---|
| 1 | `20 feathers + 10 lightSparks` | `8` | `common` |
| 2 | `30 feathers + 15 lightSparks` | `12` | `uncommon` |
| 3 | `40 feathers + 20 lightSparks` | `16` | `rare` |

If `projectArchetype == favoredArchetype` of the faction:

- `feathers -= 5`
- `lightSparks -= 5`
- minimum floor is `1 feather`, `1 lightSpark`

### 6.7. Support effects by archetype

| Archetype | Supported effect |
|---|---|
| `revelation` | generate `1` project-card; generated `lore` cards get `latestTurn -= 2` |
| `accord` | generate `1` project-card; generated `social` cards get `delta += 5` |
| `provision` | generate `1` project-card; if `tradeTier >= 1`, faction trade stock `+1 slot` |
| `remembrance` | generate `1` project-card; `gates.rerollsRemaining += 1` |
| `refinement` | generate `1` project-card; forge actions enabled for the faction |
| `passage` | generate `1` project-card; relic-bearing residents of faction may generate descent cards |
| `warding` | generate `1` project-card; generated `survival` cards get `recovery += 10` |
| `subversion` | generate `1` project-card; exactly one target faction gets `effectiveStrengthModifier = -5` for current gates build |

Additional `subversion` rules:

- `targetFactionIds.length` must equal `1`
- target faction must not equal source faction
- multiple supported `subversion` projects targeting the same faction do not stack beyond `-5`
- `effectiveStrength = max(0, factionStrength + effectiveStrengthModifier)`
- modifier affects only:
  - rarity ceiling derivation
  - effective source strength used by Gates sorting and tie-breaks
- modifier never mutates stored `factionStrength`

### 6.8. Supported project cap

The count of `projects[].isSupported = true` across all factions must not exceed `Radiance Tier -> Supported projects`.

### 6.9. Support operations

- `support_project(factionId, projectId)`
  - no cost
  - requires `currentRealm = Shining Abode`
  - requires `availability = active`
  - `preparedIncarnationPackage = null`
  - requires `projectId` to exist inside faction `factionId`
  - requires `status = completed`
  - if project is already supported: no-op
  - fails if global support cap would be exceeded
  - set `isSupported = true`
  - if `gates.hasOpenDraft = true`:
    - set `gates.isStale = true`
- `unsupport_project(factionId, projectId)`
  - no cost
  - requires `currentRealm = Shining Abode`
  - requires `availability = active`
  - `preparedIncarnationPackage = null`
  - requires `projectId` to exist inside faction `factionId`
  - if project is already unsupported: no-op
  - clears `isSupported`
  - if `gates.hasOpenDraft = true`:
    - set `gates.isStale = true`

### 6.10. Player-facing project completion contract

V1 does not use a client-facing `start_project` action.

Player-facing mechanical flow uses only `complete_project`, while `active` remains available for GM-authored in-progress/history records.

`complete_project(factionId, projectDraft)`:

- requirements:
  - `currentRealm = Shining Abode`
  - `availability = active`
  - `preparedIncarnationPackage = null`
  - enough `Ink Feathers`
  - enough `lightSparks`
  - valid `projectArchetype`
  - valid `outputEffectFamily`
  - valid `projectArchetype + outputEffectFamily` compatibility according to section `6.5`
  - valid `tier`
  - if `projectArchetype = subversion`, then exactly one valid `targetFactionId`
- authored input required in `projectDraft`:
  - `displayName`
  - `summary`
  - `toneTags`
  - `targetFactionIds`
  - `projectArchetype`
  - `outputEffectFamily`
  - `tier`
- effect:
  - deduct tier cost, including favored-archetype discount if applicable
  - append a new project instance with:
    - unique `projectId`
    - `status = completed`
    - `isSupported = false`
    - derived `strengthReward`
    - `completedAtTurn`
    - `completedAtUtc`
  - if `projectArchetype` is not in `projectArchetypesCountedThisAscension`:
    - `radiance.experience += 10`
    - append `projectArchetype` to `projectArchetypesCountedThisAscension`
    - recompute `radiance.tier`
  - recalculate `factionStrength`
  - if `gates.hasOpenDraft = true`:
    - set `gates.isStale = true`

`retire_project(factionId, projectId)`:

- cost:
  - none
- requirements:
  - `currentRealm = Shining Abode`
  - `availability = active`
  - `preparedIncarnationPackage = null`
  - `projectId` must exist inside faction `factionId`
  - project `status = completed`
- effect:
  - `status = retired`
  - `isSupported = false`
  - recalculate `factionStrength`
  - if `gates.hasOpenDraft = true`:
    - set `gates.isStale = true`

---

## 7. Trade and services

`tradeTier`, `serviceMultiplier` and `tradeProfile` are derived view values in v1. They must not be treated as authored canonical faction fields.

### 7.1. Trade profile derivation

Base trade profile from strength band:

| tradeTier | stockItemCount | rarityCeiling |
|---:|---:|---|
| 0 | 0 | `none` |
| 1 | 4 | `uncommon` |
| 2 | 6 | `rare` |
| 3 | 8 | `radiant` |

If `tradeTier = 0`:

- final `stockItemCount` must remain `0`
- final `rarityCeiling` must remain `none`
- provision/resource slot bonuses do not create a shop from dormant state

If faction has one or more supported completed projects with `projectArchetype = provision`:

- if `tradeTier >= 1`, `stockItemCount += count(supported provision projects)`

If faction has `resource_support`:

- if `tradeTier >= 1`, `stockItemCount += 1`

### 7.2. Service multiplier usage

In v1, only one numeric faction service is in scope:

- forge `stabilize_echo`

Rule:

- multiply positive numeric output by `serviceMultiplier`
- round down
- do not multiply costs

Costs are modified only by resident-role or favored-archetype rules.

No other faction services are specified in current scope.

---

## 8. Forge

### 8.1. Requirements

Forge action is valid only if:

- `currentRealm = Shining Abode`
- `availability = active`
- `preparedIncarnationPackage = null`
- `radiance.tier` meets action requirement
- faction has at least one supported completed project with `projectArchetype = refinement`

### 8.2. Exact costs before modifiers

| Action | Base feathers | Base Light Sparks |
|---|---:|---:|
| `reshape` | 10 | 10 Light Sparks |
| `retune_property` | 20 | 15 Light Sparks |
| `strengthen_band` | 30 | 20 Light Sparks |
| `stabilize_echo` | 25 | 15 Light Sparks |
| `uplift_rarity` | 45 | 30 Light Sparks |

If faction has `forge_support`:

- `feathers -= 5`
- `lightSparks -= 5`
- minimum cost floor is `1 feather`, `1 lightSpark`

### 8.3. Exact outputs

| Action | Exact state mutation |
|---|---|
| `reshape` | replace relic `formTag` with chosen valid `formTag`; no other field changes |
| `retune_property` | replace one chosen property with another valid property of same band |
| `strengthen_band` | increase chosen property band by one step |
| `stabilize_echo` | add `companionManifestationQualityBonus += floor(15 * serviceMultiplier)` to chosen relic |
| `uplift_rarity` | increase relic rarity by one step and ensure property count is at least rarity minimum |

---

## 9. Blessing cards

### 9.1. Card shape

```json
{
  "cardId": "card:faction_azalia:patron:gentle_recognition",
  "dedupeKey": "social:first_ally:+15",
  "sourceType": "head",
  "sourceFactionId": "faction_azalia",
  "sourceActorId": "guardian_azalia",
  "effectFamily": "social",
  "rarity": "uncommon",
  "displayName": "Песнь Рассветного Узнавания",
  "displaySummary": "The first worthy ally starts closer to trust.",
  "effectPayload": {
    "type": "modify_first_ally_relation",
    "delta": 15
  }
}
```

### 9.2. Source types

Allowed:

- `head`
- `project`
- `resident_descent`

### 9.3. Effect families

- `lore`
- `social`
- `resource`
- `memory`
- `descent`
- `survival`
- `relic`
- `route`

### 9.4. Required payload shapes by family and rarity

| Family | Common | Uncommon | Rare | Radiant |
|---|---|---|---|---|
| `lore` | `{ clueCount: 1, latestTurn: 12 }` | `{ 1, 10 }` | `{ 1, 8 }` | `{ 2, 8 }` |
| `social` | `{ delta: 10 }` | `{ 15 }` | `{ 20 }` | `{ 25 }` |
| `resource` | `{ money: 100, common: 1, uncommon: 0 }` | `{ 150, 2, 1 }` | `{ 225, 3, 1 }` | `{ 300, 4, 2 }` |
| `memory` | `{ options: 1, rerolls: 0 }` | `{ 1, 1 }` | `{ 2, 1 }` | `{ 2, 2 }` |
| `descent` | `{ latestTurn: 12, quality: 5 }` | `{ 10, 10 }` | `{ 8, 15 }` | `{ 6, 20 }` |
| `survival` | `{ downgrade: 1, recovery: 0 }` | `{ 1, 10 }` | `{ 1, 20 }` | `{ 1, 30 }` |
| `relic` | `{ rerolls: 1, freeShape: false, freeRetune: false }` | `{ 2, false, false }` | `{ 2, true, false }` | `{ 3, true, true }` |
| `route` | `{ routeOptions: 1, latestTurn: 10 }` | `{ 1, 8 }` | `{ 1, 6 }` | `{ 2, 6 }` |

### 9.5. Payload semantics

| Payload field / concept | Exact meaning |
|---|---|
| `clueCount` + `latestTurn` | GM must insert that many explicit lore clues by or before the specified mortal turn; if multiple eligible insertion anchors exist on the same earliest turn, choose lexical `anchorId` |
| `social.delta` | applied to the first mortal-world NPC or faction contact that is not hostile at first relation commit and has relation-state; if multiple candidates qualify on the same turn, choose lexical `entityId` or `factionId` |
| `memory.options` | additional choices in next-life memory-selection UI before turn 1 |
| `memory.rerolls` | reroll tokens only for that memory-selection UI; never mixed with `gates.rerollsRemaining` |
| `routeOptions` + `latestTurn` | extra early-life route/opportunity seeds that must appear by or before the specified turn; if multiple qualifying route seeds open on the same earliest turn, choose lexical `routeSeedId` |
| `survival.downgrade` | downgrade the first mortal-life failure tagged `ruinous` by one severity band |
| `survival.recovery` | restore that percent of immediately lost primary gauges after the downgraded failure resolves |
| `descent.latestTurn` | matching resident encounter or manifestation hook must occur by or before that turn |
| `descent.quality` | additive modifier to the descent-resolution score for the matching resident |

### 9.6. Dedupe key rules by source type

- `head` and `project` cards use payload-based `dedupeKey`
- `resident_descent` cards must include `sourceActorId` in `dedupeKey`
- therefore two different residents with identical `descent` payload remain two distinct cards

### 9.7. Candidate generation

At `open_gates`:

1. Start with empty candidate list.
2. For each faction:
   - add `1` patron card from `patronEffectFamily`
   - for each supported completed project:
     - add `1` project-card from `outputEffectFamily`
   - if there is at least one supported completed `passage` project:
     - add `1` descent card per eligible relic-bearing resident
3. Build temporary `effectiveStrength` for each faction:
   - start from stored `factionStrength`
   - apply at most one `-5` `subversion` penalty per target faction
   - clamp to `>= 0`
4. Recompute `effectiveStrengthBand` and `effectiveFactionCeiling`.
5. Derive final rarity:
   - `head` rarity = `min(radianceCeiling, effectiveFactionCeiling)`
   - `resident_descent` rarity = `min(radianceCeiling, effectiveFactionCeiling)`
   - `project` rarity:
     - start from tier base rarity
     - if `effectiveStrengthBand = Radiant`, upgrade by one rarity step
     - clamp to `min(radianceCeiling, effectiveFactionCeiling)`
6. Build family payload from final rarity table.
7. Apply resident role modifiers.
8. Apply supported-project archetype modifiers.
9. Compute `dedupeKey` from final payload after all modifiers.

### 9.8. Modifier order and caps

Processing order is mandatory:

1. raw candidate creation
2. temporary `subversion` penalties
3. final rarity derivation
4. family payload generation
5. resident role modifiers
6. supported-project archetype modifiers
7. final `dedupeKey`
8. dedupe
9. sort
10. take top `draftSize`

Caps and stack rules:

- duplicate same-role residents in one faction do not stack role effect
- `revelation/accord/refinement/passage/warding/subversion` support modifiers apply once per supported project-card source and do not re-apply to unrelated cards
- `provision -> stockItemCount += 1` stacks per supported provision project only when `tradeTier >= 1`
- `remembrance -> gates.rerollsRemaining += 1` stacks per supported remembrance project
- `memory.rerolls` inside card payload are independent from `gates.rerollsRemaining`

### 9.9. Dedupe and sorting

Duplicates are cards with identical `dedupeKey`.

Keep exactly one card by:

1. higher `rarity`
2. higher effective source factionStrength
3. `sourceType` priority:
   - `head = 3`
   - `project = 2`
   - `resident_descent = 1`
4. lexical `cardId`

Sort surviving cards by:

1. `rarityWeight desc`
   - `common=1`
   - `uncommon=2`
   - `rare=3`
   - `radiant=4`
2. effective source factionStrength desc
3. sourceType priority desc
4. `cardId asc`

Take the first `draftSize(radiance.tier)` cards.

### 9.10. Picks and rerolls

- `pickCap = table by radiance tier`
- `rerollsRemaining = count(supported remembrance projects)`
- `allCandidateBlessingCards` is the frozen fully sorted candidate snapshot produced by the latest `open_gates`
- `shownBlessingCardIds` is the set of cards that have ever been surfaced in the current draftVersion
- `nextCandidateCursor` points to the next unseen card inside `allCandidateBlessingCards`

`reroll_gates_draft`:

1. remove the two lowest-ranked cards from `availableBlessingCards` that are not selected
2. replace them with the next two cards from `allCandidateBlessingCards` whose `cardId` is not present in `shownBlessingCardIds`, advancing `nextCandidateCursor` as cards are consumed
3. append the inserted `cardId`s to `shownBlessingCardIds`
4. re-sort `availableBlessingCards` by the same canonical ranking used by `open_gates`
5. decrement `rerollsRemaining`

---

## 10. Gates lifecycle

### 10.1. Open gates

`open_gates`:

- requires `currentRealm = Shining Abode`
- requires `availability = active`
- requires `preparedIncarnationPackage = null`
- rebuilds candidate list
- rebuilds sorted draft
- writes:
  - `gates.draftVersion += 1`
  - `gates.hasOpenDraft = true`
  - `gates.isStale = false`
  - `gates.allCandidateBlessingCards = fully sorted candidate list`
  - `gates.availableBlessingCards = first min(draftSize, allCandidateBlessingCards.length) cards from that sorted list`
  - `gates.shownBlessingCardIds = cardIds of current draft`
  - `gates.selectedBlessingCardIds = []`
  - `gates.nextCandidateCursor = gates.shownBlessingCardIds.length`
  - `gates.rerollsRemaining = count(supported remembrance projects)`

### 10.2. Select cards

`select_blessing_card(cardId)`:

- valid only if `currentRealm = Shining Abode`
- valid only if `availability = active`
- valid only if `preparedIncarnationPackage = null`
- valid only if `gates.hasOpenDraft = true`
- valid only if `gates.isStale = false`
- valid only if `cardId` is in `availableBlessingCards`
- valid only if `selectedBlessingCardIds.length < pickCap`
- on duplicate select, no-op

`deselect_blessing_card(cardId)`:

- valid only if `currentRealm = Shining Abode`
- valid only if `availability = active`
- valid only if `preparedIncarnationPackage = null`
- valid only if `gates.hasOpenDraft = true`
- valid only if `gates.isStale = false`
- remove from `selectedBlessingCardIds`

`reroll_gates_draft`:

- valid only if `currentRealm = Shining Abode`
- valid only if `availability = active`
- valid only if `preparedIncarnationPackage = null`
- valid only if `gates.hasOpenDraft = true`
- valid only if `gates.isStale = false`
- valid only if `rerollsRemaining > 0`
- valid only if at least two unselected cards in `availableBlessingCards` exist
- valid only if at least two unseen replacement cards remain in `allCandidateBlessingCards`

### 10.3. Enter mortal life

`enter_mortal_life_from_shining_abode` requires:

- `currentRealm = Shining Abode`
- `availability = active`
- `gates.hasOpenDraft = true`
- `gates.isStale = false`
- `selectedBlessingCardIds.length >= 1`
- `selectedBlessingCardIds.length <= pickCap`

On success:

```json
{
  "preparedIncarnationPackage": {
    "selectedCardIds": ["..."],
    "selectedCards": [
      {
        "cardId": "card:faction_azalia:patron:gentle_recognition",
        "dedupeKey": "social:first_ally:+15",
        "sourceType": "head",
        "sourceFactionId": "faction_azalia",
        "sourceActorId": "guardian_azalia",
        "effectFamily": "social",
        "rarity": "uncommon",
        "effectPayload": {
          "type": "modify_first_ally_relation",
          "delta": 15
        }
      }
    ],
    "generatedFromDraftVersion": 3,
    "preparedAtTurn": 155,
    "preparedAtUtc": "2026-03-29T14:30:00Z"
  }
}
```

Package invariant:

- `selectedCardIds.length == selectedCards.length`
- `selectedCardIds[i] == selectedCards[i].cardId` for every index `i`

Then:

- `currentRealm` remains `Shining Abode` until successful mortal bootstrap materializes the next life
- Shining Abode interaction ends and control leaves the realm for mortal-life bootstrap flow
- `preparedIncarnationPackage != null` now marks canonical pending-bootstrap handoff mode for the whole runtime
- while `preparedIncarnationPackage != null`, ordinary Shining Abode, Chaos Sea, Guardian, and Abode interactions are invalid
- mortal bootstrap must use `preparedIncarnationPackage.selectedCards`
- cards must not be reconstructed from current factions/projects
- only successful mortal bootstrap may set `currentRealm` to the concrete mortal world name of the generated life
- `gates.hasOpenDraft = false`
- `gates.allCandidateBlessingCards = []`
- `gates.availableBlessingCards = []`
- `gates.isStale = false`
- `gates.shownBlessingCardIds = []`
- `gates.selectedBlessingCardIds = []`
- `gates.nextCandidateCursor = 0`
- `gates.rerollsRemaining = 0`

No faction, hall, project or strength data is cleared.

After the next successful mortal bootstrap that consumes the package:

- apply only `preparedIncarnationPackage.selectedCards`
- set `currentRealm` to the concrete mortal world name of the generated life
- then set `preparedIncarnationPackage = null`

---

### 10.4. Normal post-life lifecycle

Current v1 runtime does **not** define a player-facing `return_from_mortal_life_to_shining_abode` operation.

Instead:

- `TriggerLifeEnd` starts the dedicated Life Evaluation lifecycle
- the accepted Life Evaluation turn whose `manifest.SourceLabel` satisfies `LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(...)` is the canonical completion point of that lifecycle
- current v1 canonical Life Evaluation source labels are `оценки жизни` and `автоматической оценки жизни`
- the normal post-life destination of that lifecycle remains `Chaos Sea`
- this normal post-life route does **not** mutate stored `shining_abode_state.availability`; if Shining Abode was `active` before entering the mortal life, it remains stored as `active` while `currentRealm = Chaos Sea`
- every accepted Life Evaluation turn activates `game_state/control/afterlife_return_guard.json` with `reason = post_life_return`
- `afterlife_return_guard.json` is only a protective guard for the first ordinary afterlife turn after Life Evaluation
- `afterlife_return_guard.json` is **not** a separate lifecycle completion marker and must not be interpreted as automatic return to `Shining Abode`
- ordinary afterlife turn consumption applies only to a semantic-valid `afterlife_return_guard.json`; malformed guard or parsed guard with the wrong `reason` is not consumed by ordinary turns and remains blocked fail-closed until validation repair or explicit client/runtime clear
- current v1 ordinary post-life route does **not** automatically return the soul to active Shining Abode
- later access from `Chaos Sea` into an already-stored active Shining Abode uses a separate explicit afterlife route `reenter_shining_abode`
- `AscensionTrigger` remains reserved for maximum-Enlightenment ascension into Shining Abode and is not reused as the generic re-entry path after every post-life return

### 10.5. Explicit re-entry from Chaos Sea

`reenter_shining_abode` is valid only if:

- `currentRealm = Chaos Sea`
- `shining_abode_state.availability = active`
- `preparedIncarnationPackage = null`
- `game_state/control/afterlife_return_guard.json` is absent, or semantic-valid (`reason = post_life_return`) and inactive
- malformed/unreadable `afterlife_return_guard.json`, or a parsed guard with the wrong `reason`, blocks re-entry fail-closed until validation repair or explicit client/runtime clear
- it is executed as a client-owned local lifecycle command, not as a GM-authored accepted turn

`reenter_shining_abode` performs:

- `currentRealm = Shining Abode`
- keep existing stored `lightSparks`
- keep existing `investCountThisAscension`
- keep existing `projectArchetypesCountedThisAscension`
- do not apply any ascension-local reset
- if needed, recompute only purely derived Shining values without mutating canonical per-ascension counters

This is the canonical v1 route back into an already-stored active Shining Abode after the soul has completed the normal `Life Evaluation -> Chaos Sea` post-life path.

---

## 11. Return to Chaos Sea

### 11.1. Operation

`return_to_chaos_sea` is valid only if:

- `currentRealm = Shining Abode`
- `availability = active`
- `preparedIncarnationPackage = null`
- it is executed as a client-owned local lifecycle command, not as a GM-authored accepted turn

`return_to_chaos_sea` performs:

- `currentRealm = Chaos Sea`
- `shining_abode_state.availability = sealed_until_next_ascension`
- `gates.hasOpenDraft = false`
- `gates.allCandidateBlessingCards = []`
- `gates.availableBlessingCards = []`
 - `gates.isStale = false`
- `gates.shownBlessingCardIds = []`
- `gates.selectedBlessingCardIds = []`
- `gates.nextCandidateCursor = 0`
- `gates.rerollsRemaining = 0`
- `preparedIncarnationPackage = null`
- `pendingNativeFactionDiscovery = null`
- `game_state/meta/soul_state.json.enlightenment.currentTier = Новичок`
- `game_state/meta/soul_state.json.enlightenment.experience = 0`
- `game_state/meta/soul_state.json.enlightenment.level = 0`
- if `game_state/meta/soul_state.json.enlightenment.progressPercent` exists, set it to `0`

### 11.2. Preserved state

Do not clear:

- `radiance.experience`
- `radiance.tier`
- `halls`
- `factions` including their nested `projects[]` and `baseStrength`
- stored `factionStrength` value, as the historical snapshot from the last active-Abode computation

Investment bonus is not preserved across the next ascension because it is derived only from `investCountThisAscension`, which is reset only on ascension re-entry before `factionStrength` is recomputed.
While `availability = sealed_until_next_ascension`, stored `factionStrength` is read-only history/reference data only and must not be used for live trade, forge, Gates, or other gameplay derivation.

If `lightSparks` remains stored in sealed state, treat it as inert data only. It must not be used for gameplay decisions because the next activation after ascension overwrites it to `100`, while ordinary `reenter_shining_abode` is unavailable in sealed state.

Meaning of `sealed_until_next_ascension`:

- Shining Abode UI actions are unavailable
- `open_gates`, `support_project`, `invest_in_faction`, `discover_native_faction`, forge actions and any other Shining Abode operation are invalid
- data remains readable for history/reference only

### 11.3. Re-entry after sealed exit

When player later ascends again after `return_to_chaos_sea`:

- `availability = active`
- `currentRealm = Shining Abode`
- `lightSparks = 100`
- for each faction:
  - `investCountThisAscension = 0`
  - `projectArchetypesCountedThisAscension = []`
  - recompute `factionStrength` from stored `baseStrength`, current residents and completed projects
- all preserved structural state remains unchanged except for ascension-local counters and derived strength values
- this sealed re-entry path is distinct from ordinary `reenter_shining_abode` from `Chaos Sea`

---

## 12. Example materializations

These are non-binding presentation examples over fixed mechanics.

| Example display name | favoredArchetype | patronEffectFamily |
|---|---|---|
| `Кузнецы Луча` | `refinement` | `relic` |
| `Хранители Памяти` | `remembrance` | `memory` |
| `Смотрители Врат` | `passage` | `descent` |
| `Хор Рассвета` | `accord` | `social` |
| `Писцы Сияния` | `revelation` | `lore` |
| `Дом Тихой Щедрости` | `provision` | `resource` |
| `Дом Нежной Тени` | `subversion` | `social` |

---

## 13. Acceptance tests for later implementation

### 13.1. Radiance

- tier recalculates correctly from XP thresholds
- `lightSparks` refills to `100` on activation or re-activation after ascension
- ordinary `reenter_shining_abode` preserves the stored `lightSparks` value and does not refill it
- normal `Life Evaluation -> Chaos Sea` itself does not refill `lightSparks`
- `lightSparks`-spending actions fail cleanly on insufficient currency

### 13.2. Factions

- ascended guardian faction starts at `35 + residentBonus`, clamped by tier cap
- `discover_native_faction` requires `availability = active`
- native faction persists canonical `baseStrength` computed once at discovery resolution before discovery `+20 XP`
- invest action adds `+8` and respects `3 uses per ascension`
- band derivation correctly updates trade tier and rarity ceiling
- re-ascension clears investment bonus and recomputes `factionStrength`
- resident membership is derived only from resident `shiningFactionId`
- `residentCount` includes only `ascended` residents linked to the faction
- native discovery materializes resident records linked through `shiningFactionId`
- resident join/leave/move between factions recomputes `factionStrength`
- resident role/relic changes with unchanged headcount still invalidate opened gates
- `tradeTier = 0` always yields `stockItemCount = 0`
- `support_project/unsupport_project` look up `projectId` only inside the specified `factionId`
- `faction.hallId` is the authoritative hall linkage

### 13.3. Residents

- duplicate same-role residents do not stack role effects
- all ascended linked residents still contribute `+3 strength`
- relic-bearing resident generates descent card only with supported `passage`

### 13.4. Projects

- any completed project with valid `projectArchetype + outputEffectFamily` is supportable
- invalid `projectArchetype + outputEffectFamily` compatibility fails with no mutation
- `support_project`, `unsupport_project` and `retire_project` all look up `projectId` only inside the specified `factionId`
- `support_project`, `unsupport_project` and `retire_project` all require `availability = active`
- `support_project`, `unsupport_project`, `complete_project` and `retire_project` all require `preparedIncarnationPackage = null`
- favored archetype discount applies correctly
- `subversion` project applies only temporary `effectiveStrength -5`, not permanent mutation
- project tier controls both cost and strengthReward
- `complete_project` writes a fresh `completed` project with timestamps and `isSupported = false`
- `retire_project` removes strength contribution and clears support
- discovery-seeded completed projects do not grant extra first-completion XP
- `projectArchetypesCountedThisAscension` blocks duplicate first-completion XP only within the current ascension
- re-ascension clears `projectArchetypesCountedThisAscension`

### 13.5. Cards

- head cards are generated from `patronEffectFamily`, not fixed names
- project cards are generated from `outputEffectFamily`
- two differently named cards with the same `dedupeKey` dedupe correctly
- two `resident_descent` cards from different residents never dedupe into one card
- rarity and payload are derived from family tables, not from free prose
- `social`, `route` and `lore` first/early effects use the documented tie-break order instead of hidden heuristic

### 13.6. Gates

- candidate generation matches exact source rules
- all ordinary Gates actions require `currentRealm = Shining Abode`, `availability = active`, and `preparedIncarnationPackage = null`
- draft size matches tier
- `open_gates` with candidate pool smaller than `draftSize` still produces valid `availableBlessingCards`, `shownBlessingCardIds` and `nextCandidateCursor`
- `selectedBlessingCardIds` always remains unique and a subset of current `availableBlessingCards`
- reroll replaces exactly two weakest eligible cards
- reroll requires at least two unselected cards
- reroll also requires at least two unseen replacement cards in frozen candidate snapshot
- successful reroll re-sorts `availableBlessingCards` by canonical ranking before the next action
- reroll uses frozen `allCandidateBlessingCards`, `shownBlessingCardIds` and `nextCandidateCursor`, so repeated rerolls are reproducible
- enter-mortal-life clears gates state and writes prepared package
- successful enter exits Shining Abode interaction context and hands control to mortal bootstrap flow
- successful enter keeps `currentRealm = Shining Abode` until successful mortal bootstrap materializes mortal-world state
- while `preparedIncarnationPackage != null`, runtime is in canonical pending-bootstrap handoff mode rather than ordinary active Shining Abode
- while `preparedIncarnationPackage != null`, ordinary Shining Abode, Chaos Sea, Guardian, and Abode interactions are invalid until lifecycle consumes or clears it
- runtime/CLI mode resolution must treat `preparedIncarnationPackage != null` as higher priority than ordinary `currentRealm = Shining Abode`
- prepared package contains frozen selected card snapshots
- `preparedIncarnationPackage.selectedCardIds[]` matches `selectedCards[].cardId` one-to-one and in order
- successful mortal bootstrap sets `currentRealm` to the concrete mortal world name, consumes the frozen package, and clears `preparedIncarnationPackage`
- any build-affecting mutation after `open_gates` marks draft stale and blocks select/reroll/enter until `open_gates` is called again
- `invest_in_faction`, native discovery resolution, and resident mutations to `shiningFactionId`, `residentRole`, `ascensionState` or `grantedRelicId` also mark an already-open draft stale

### 13.7. Returns and exit

- dedicated accepted Life Evaluation turn with a source label recognized by `LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(...)` is the canonical completion point of `TriggerLifeEnd -> Life Evaluation`
- current v1 canonical Life Evaluation source labels are `оценки жизни` and `автоматической оценки жизни`
- normal post-life return in current v1 runtime ends in `Chaos Sea`, not `Shining Abode`
- this normal post-life route does not auto-seal Shining Abode and does not mutate stored `availability`; only explicit `return_to_chaos_sea` seals it
- `afterlife_return_guard.json(reason=post_life_return)` is always activated after accepted Life Evaluation and is a protective first-afterlife-turn guard, not a separate completion marker
- malformed `afterlife_return_guard.json`, or a parsed guard whose `reason != post_life_return`, must not weaken protection of the mandatory first ordinary afterlife turn; for `reenter_shining_abode` it is treated fail-closed until validation repair or explicit client/runtime clear
- ordinary afterlife turn consumption decrements or clears only a semantic-valid `afterlife_return_guard.json`; malformed or wrong-`reason` guard state is never consumed by ordinary afterlife turns
- ordinary Shining Abode operations fail when `currentRealm != Shining Abode`
- ordinary later access from `Chaos Sea` into an already-active Shining Abode uses explicit `reenter_shining_abode`, not `AscensionTrigger`
- `reenter_shining_abode` requires `currentRealm = Chaos Sea`, stored `availability = active`, and `afterlife_return_guard.json` to be absent, or semantic-valid (`reason = post_life_return`) and inactive
- `reenter_shining_abode` preserves stored `lightSparks` and does not reset `investCountThisAscension` or `projectArchetypesCountedThisAscension`
- current v1 intentionally treats `lightSparks` as a persistent scarce currency across ordinary post-life returns into the same stored active Shining Abode state
- `return_to_chaos_sea` is valid only from active `currentRealm = Shining Abode`
- `return_to_chaos_sea` is invalid from Mortal World, pending-bootstrap handoff mode, or already sealed state
- forge actions require `availability = active`
- sealing clears transient gates/discovery state only
- factions, halls, base strength, projects and radiance XP/tier remain intact
- sealed stored `factionStrength` is historical snapshot only and regains live gameplay meaning only after ordinary active-Abode continuation or true re-ascension recompute
- `return_to_chaos_sea` is a Shining-Abode-local seal/exit route and is not the same operation as destructive New Game+ reset
- re-ascension reactivates same shining state, refills `lightSparks` to `100`, resets `investCountThisAscension`, clears `projectArchetypesCountedThisAscension`, and recomputes `factionStrength`
- re-ascension restores `currentRealm = Shining Abode`
