# Сияющая Обитель: consolidation addendum

## Статус документа

Этот документ закрывает оставшиеся implementation gaps в:

- `OtherGuides/Shining_Abode_Implementation_Plan.md`
- `OtherGuides/Shining_Abode_Implementation_Plan_Rebased.md`
- `OtherGuides/Shining_Abode_Faction_Politics_Addendum.md`

При конфликте приоритет такой:

1. этот consolidation addendum
2. `Shining_Abode_Faction_Politics_Addendum.md`
3. `Shining_Abode_Implementation_Plan_Rebased.md`
4. `Shining_Abode_Implementation_Plan.md`
5. `Shining_Abode_Endgame_Design_Plan.md`

Этот файл не переписывает старые formula tables, но делает Shining-документы **decision-complete** по:

- final faction schema
- actor ownership and validation
- political request/receipt wire contracts
- `player_founded` / player-led faction mechanics
- precedence between Shining politics and the already-implemented resident migration system

---

## 1. Final ownership model

### 1.1. `shining_abode_state.json`

`game_state/meta/shining_abode_state.json` canonical owner для:

- `availability`
- `radiance`
- `lightSparks`
- `halls[]`
- `factions[]`
- `shiningPoliticalActors[]`
- `pendingNativeFactionDiscovery`
- `gates`
- `preparedIncarnationPackage`
- `factionFoundingReceipts[]`
- `factionRealignmentReceipts[]`

Новые политические receipts живут именно здесь, потому что они описывают состояние самой Сияющей Обители, а не отдельного resident-file или guardian-file owner.

### 1.2. `guardian_abode_residents.json`

`game_state/meta/guardian_abode_residents.json` остаётся canonical owner для resident identity и всех resident-facing Shining additions:

- `ascensionState`
- `shiningFactionId`
- `residentRole`
- `factionLoyaltyLevel`
- `factionLoyaltyTier`
- `factionRestlessness`
- `factionRealignmentState`

Membership в сияющей фракции по-прежнему derived only from `resident.shiningFactionId`.

Canonical `faction.residents[]` не вводить.

### 1.3. Control files

Новые pending political flows materialize-ятся только через control files:

- `game_state/control/pending_shining_faction_foundings.json`
- `game_state/control/pending_shining_faction_realignments.json`
- `game_state/control/pending_shining_faction_leadership_transitions.json`

Никакой Shining political flow не должен жить как prose-only mutation без request/receipt pair.

### 1.4. `soul_state.json` and player ownership

`player_soul` не получает отдельный actor object внутри `shining_abode_state.json`.

Правило:

- `headActorType = player_soul`
- `headActorId = player_soul`

означает singleton player actor, который существует по факту soul lifecycle и не требует отдельного cross-file registry.

---

## 2. Final canonical schemas

### 2.1. Final faction shape

Старый flat shape фракции считается устаревшим. Canonical shape теперь такой:

```json
{
  "factionId": "faction_dawn_choir",
  "originType": "player_founded",
  "hallId": "hall_dawn_choir",
  "charter": {
    "factionName": "Хор Рассвета",
    "favoredArchetype": "accord",
    "patronEffectFamily": "social",
    "summary": "Союз резидентов, которые строят силу через согласие и общее сияние."
  },
  "leadership": {
    "headActorType": "player_soul",
    "headActorId": "player_soul",
    "leadershipState": "secure"
  },
  "baseStrength": 35,
  "factionStrength": 44,
  "investCountThisAscension": 0,
  "projectArchetypesCountedThisAscension": [],
  "projects": [],
  "leadershipReceipts": [],
  "leadershipHistory": []
}
```

Explicit defaults:

- `charter` и `leadership` — обязательные nested objects
- old flat fields `factionName`, `favoredArchetype`, `patronEffectFamily`, `headActorType`, `headActorId` больше не materialize-ятся как top-level canonical fields
- `originType` остаётся stable top-level field
- `leadership.headActorType` и `leadership.headActorId`:
  - оба обязательны when `leadershipState != vacant`
  - оба должны быть `null` when `leadershipState = vacant`
- `baseStrength` canonical and stable after faction creation
- `factionStrength` derived from stored `baseStrength` and current formula from the old implementation plan

### 2.2. Allowed faction origins

Supported `originType` values:

- `ascended_guardian`
- `native_radiant`
- `player_founded`

`originType` immutable after faction creation.

Leadership change never rewrites `originType`.

### 2.3. Final `shiningPoliticalActors[]` shape

```json
{
  "actorId": "radiant_actor_memory_keepers_head",
  "actorType": "radiant_actor",
  "displayName": "Архонтка Немирия",
  "summary": "Старая хранительница памяти и ритуала.",
  "originFactionId": "faction_memory_keepers",
  "currentFactionId": "faction_memory_keepers",
  "politicalStatus": "head"
}
```

Supported `politicalStatus` values:

- `head`
- `former_head`
- `claimant`
- `elder`
- `retired`

Rules:

- `actorId` unique across `shiningPoliticalActors[]`
- `originFactionId` required and immutable
- `currentFactionId` nullable
- if `politicalStatus = head`, `currentFactionId` required and exactly one faction may point to that actor as current head
- `former_head`, `claimant`, `elder`, `retired` may keep `currentFactionId` or clear it to `null`

### 2.4. Head actor validation matrix

| `leadership.headActorType` | Canonical owner | Required validation |
|---|---|---|
| `guardian` | `guardians.json` | guardian exists; same guardian cannot be current head of two Shining factions at once |
| `player_soul` | singleton player actor | `headActorId` must equal exactly `player_soul`; only one faction may have player as current head |
| `resident` | `guardian_abode_residents.json` | resident exists, `ascensionState = ascended`, `shiningFactionId = factionId` |
| `radiant_actor` | `shining_abode_state.json.shiningPoliticalActors[]` | actor exists, `actorType = radiant_actor`, `currentFactionId = factionId` when actor is current head |

Additional rules:

- `leadershipState = vacant` requires `headActorType = null` and `headActorId = null`
- every current head may head at most one faction at a time
- if a former head remains referenced anywhere, this must happen through receipts/history or `shiningPoliticalActors[]`, not through stale current-head linkage

### 2.5. Final resident Shining political fields

The following additive fields are canonical in `guardian_abode_residents.json`:

- `factionLoyaltyLevel: 0..100`
- `factionLoyaltyTier`
- `factionRestlessness: 0..100`
- `factionRealignmentState`

`factionLoyaltyTier` uses the same thresholds as current ordinary `abodeDevotionTier`:

- `0..19 -> alienated`
- `20..39 -> uncertain`
- `40..59 -> attached`
- `60..79 -> devoted`
- `80..100 -> steadfast`

`factionRealignmentState` mirrors the already-implemented migration thresholds, but uses Shining-specific labels:

- `ready_to_realign` if `factionLoyaltyLevel <= 15` and `factionRestlessness >= 70`
- `considering_realignment` if `factionLoyaltyLevel <= 30` and `factionRestlessness >= 55`
- `restless` if `factionLoyaltyLevel <= 45` or `factionRestlessness >= 45`
- `wavering` if `factionLoyaltyLevel <= 60` or `factionRestlessness >= 30`
- otherwise `settled`

These states are derived only. They are not free-authored prose labels.

---

## 3. Political flow wire contracts

### 3.1. Player-founded faction flow

Control file:

- `game_state/control/pending_shining_faction_foundings.json`

Canonical shape:

```json
{
  "requests": [
    {
      "requestId": "founding_req_dawn_choir",
      "proposedFactionId": "faction_dawn_choir",
      "proposedHallId": "hall_dawn_choir",
      "proposedHallName": "Зал Рассветного Хора",
      "proposedHallDescription": "Светлый зал для союзов, клятв и общих песен.",
      "proposedHallServiceTags": ["social", "lore"],
      "charter": {
        "factionName": "Хор Рассвета",
        "favoredArchetype": "accord",
        "patronEffectFamily": "social",
        "summary": "Союз резидентов, которые строят силу через согласие и общее сияние."
      },
      "supportingResidentIds": [
        "resident_liora",
        "resident_mael",
        "resident_serit"
      ],
      "createdAtTurn": 184,
      "createdAtUtc": "2026-04-16T15:20:00Z"
    }
  ]
}
```

Founding request eligibility:

- `currentRealm = Shining Abode`
- `availability = active`
- `preparedIncarnationPackage = null`
- player does not already head another faction
- `proposedFactionId` and `proposedHallId` are unique against current Shining state and other pending founding requests
- `proposedHallName` required and non-empty
- `proposedHallDescription` required and non-empty
- `proposedHallServiceTags` contains `1..2` unique tags from:
  - `social`
  - `lore`
  - `resource`
  - `memory`
  - `descent`
  - `relic`
- `proposedHallServiceTags` must include `charter.patronEffectFamily`
- `supportingResidentIds.length >= 3`
- each supporter:
  - exists
  - `ascensionState = ascended`
  - not locked by another pending relocation/political flow
  - is allowed to move faction this turn

V1 founding cost is fixed:

- `25 Ink Feathers`
- `15 Light Sparks`

Canonical receipt owner:

- `shining_abode_state.json.factionFoundingReceipts[]`

Receipt shape:

```json
{
  "requestId": "founding_req_dawn_choir",
  "proposedFactionId": "faction_dawn_choir",
  "proposedHallId": "hall_dawn_choir",
  "hallName": "Зал Рассветного Хора",
  "factionId": "faction_dawn_choir",
  "hallId": "hall_dawn_choir",
  "status": "accepted",
  "supportingResidentIds": [
    "resident_liora",
    "resident_mael",
    "resident_serit"
  ],
  "resolvedAtTurn": 184,
  "resolvedAtUtc": "2026-04-16T15:24:00Z",
  "reason": "founding_accepted"
}
```

Supported founding receipt statuses:

- `accepted`
- `refused`
- `withdrawn`

Successful founding must:

- create new `hall` exactly from:
  - `proposedHallId`
  - `proposedHallName`
  - `proposedHallDescription`
  - `proposedHallServiceTags`
- create new `faction`
- set:
  - `originType = player_founded`
  - `leadership.headActorType = player_soul`
  - `leadership.headActorId = player_soul`
  - `leadership.leadershipState = secure`
- move supporting residents by updating their `shiningFactionId`
- write one founding receipt

No separate founding history array is introduced in v1.
Resident `historyLog` entries for moved supporters are enough.

### 3.2. Resident faction realignment flow

Control file:

- `game_state/control/pending_shining_faction_realignments.json`

Canonical shape:

```json
{
  "requests": [
    {
      "requestId": "realign_req_liora",
      "residentId": "resident_liora",
      "residentName": "Лиора",
      "sourceFactionId": "faction_memory_keepers",
      "sourceFactionName": "Хранители Памяти",
      "targetFactionId": "faction_dawn_choir",
      "targetFactionName": "Хор Рассвета",
      "realignmentMode": "accepted_transfer",
      "factionLoyaltyLevel": 14,
      "factionLoyaltyTier": "alienated",
      "factionRestlessness": 76,
      "factionRealignmentState": "ready_to_realign",
      "createdAtTurn": 192,
      "createdAtUtc": "2026-04-16T16:05:00Z"
    }
  ]
}
```

Supported `realignmentMode` values:

- `accepted_transfer`
- `refused_transfer`
- `departure_to_neutral`

Rules:

- `targetFactionId/targetFactionName` required for `accepted_transfer` and `refused_transfer`
- `targetFactionId/targetFactionName` must be absent for `departure_to_neutral`
- `targetFactionId` must differ from `sourceFactionId`
- request may be opened only if:
  - resident exists
  - `ascensionState = ascended`
  - resident currently belongs to `sourceFactionId`
  - `factionRealignmentState = ready_to_realign`
  - no conflicting pending transfer/founding/leadership lock exists on the same resident

Canonical receipt owner:

- `shining_abode_state.json.factionRealignmentReceipts[]`

Receipt shape:

```json
{
  "requestId": "realign_req_liora",
  "residentId": "resident_liora",
  "residentName": "Лиора",
  "sourceFactionId": "faction_memory_keepers",
  "targetFactionId": "faction_dawn_choir",
  "status": "accepted",
  "realignmentMode": "accepted_transfer",
  "residentHistoryEntryId": "history_resident_liora_faction_shift",
  "resolvedAtTurn": 192,
  "resolvedAtUtc": "2026-04-16T16:08:00Z",
  "reason": "accepted_by_target_faction"
}
```

Supported realignment receipt statuses:

- `accepted`
- `refused`
- `departed_to_neutral`
- `withdrawn`

Successful realignment must:

- update resident `shiningFactionId`
- recompute resident `factionLoyaltyLevel`, `factionLoyaltyTier`, `factionRestlessness`, `factionRealignmentState` against the new faction
- write one realignment receipt
- write one resident `historyLog` entry

No separate faction membership history array is introduced in v1.

### 3.3. Leadership transition flow

Control file:

- `game_state/control/pending_shining_faction_leadership_transitions.json`

Canonical shape:

```json
{
  "requests": [
    {
      "requestId": "leadership_req_dawn_choir",
      "factionId": "faction_dawn_choir",
      "factionName": "Хор Рассвета",
      "transitionMode": "peaceful_succession",
      "incumbentHeadActorType": "guardian",
      "incumbentHeadActorId": "guardian_azalia",
      "candidateHeadActorType": "player_soul",
      "candidateHeadActorId": "player_soul",
      "supportingResidentIds": [
        "resident_liora",
        "resident_mael"
      ],
      "createdAtTurn": 203,
      "createdAtUtc": "2026-04-16T16:40:00Z"
    }
  ]
}
```

Supported `transitionMode` values:

- `abdication`
- `peaceful_succession`
- `revolt`

Transition rules:

- `abdication`:
  - incumbent consent required
  - candidate may be absent; accepted result may set the faction to `vacant`
- `peaceful_succession`:
  - incumbent consent required
  - candidate required
  - candidate must differ from incumbent
  - support threshold = `max(2, ceil(ascendedFactionResidents / 3))`
- `revolt`:
  - faction must already be `contested`
  - candidate required
  - candidate must differ from incumbent
  - support threshold = `max(3, ceil(ascendedFactionResidents / 2))`

Only one pending leadership request may exist per faction at a time.

Candidate-specific rules:

- `resident` candidate must be `ascended` and currently belong to the same faction
- `guardian` candidate must exist in `guardians.json`
- `player_soul` candidate always uses `candidateHeadActorId = player_soul`
- `radiant_actor` candidate must already exist in `shiningPoliticalActors[]` or be materialized in the same accepted turn before the leadership link is written
- every `supportingResidentId` must:
  - exist
  - be `ascended`
  - currently belong to the same `factionId`
  - not be locked by another pending relocation/political flow
- leadership support thresholds count only ascended residents who currently belong to the same `factionId`
- external residents from other factions never count toward succession or revolt support
- a current resident-head cannot open ordinary inter-Abode transfer, Shining faction realignment, or founding support while remaining the current head; leadership must resolve first in the same accepted turn or earlier

Canonical receipt owner:

- `shining_abode_state.json.factions[].leadershipReceipts[]`

Receipt shape:

```json
{
  "requestId": "leadership_req_dawn_choir",
  "transitionMode": "peaceful_succession",
  "previousHeadActorType": "guardian",
  "previousHeadActorId": "guardian_azalia",
  "newHeadActorType": "player_soul",
  "newHeadActorId": "player_soul",
  "status": "accepted",
  "resolvedAtTurn": 203,
  "resolvedAtUtc": "2026-04-16T16:44:00Z",
  "reason": "recognized_succession"
}
```

Supported leadership receipt statuses:

- `accepted`
- `refused`
- `withdrawn`

Canonical history owner:

- `shining_abode_state.json.factions[].leadershipHistory[]`

History shape:

```json
{
  "eventId": "leadership_evt_dawn_choir_203",
  "requestId": "leadership_req_dawn_choir",
  "eventType": "succeeded",
  "summary": "Игрок мирно принял руководство Хором Рассвета.",
  "turnNumber": 203,
  "occurredAtUtc": "2026-04-16T16:44:00Z"
}
```

Supported `eventType` values:

- `abdicated`
- `succeeded`
- `revolted`
- `refused`
- `vacated`

Leadership resolution parity:

- accepted transition requires both receipt and history
- refused transition requires both receipt and history
- `vacant` result requires `newHeadActorType = null` and `newHeadActorId = null`

---

## 4. Mechanical defaults that are now fixed

### 4.1. `player_founded` base strength

Canonical `baseStrength` rules are now:

| `originType` | `baseStrength` rule |
|---|---|
| `ascended_guardian` | `35`, written once at materialization |
| `native_radiant` | `55 + 5 * max(0, radiance.tier - 1)`, capped at `70`, written once at discovery resolution |
| `player_founded` | `35`, written once at accepted founding |

`player_founded` uses the same starting base as `ascended_guardian`.
Its immediate practical differentiation comes from supporter residents and later projects/investment, not from a unique hidden multiplier.

### 4.2. Charter owns patron mechanics

The old assumption “current head defines the faction patron identity” is deprecated.

New canonical rule:

- patron/head card family generation always reads `faction.charter.patronEffectFamily`
- favored-archetype discounts always read `faction.charter.favoredArchetype`
- current leader identity may affect narrative wording only
- current leader identity must not affect:
  - card family
  - card rarity
  - dedupe behavior
  - base strength

This means:

- player capture of existing faction does not rewrite its patron generation
- guardian losing leadership does not erase the faction theme
- `player_soul` never creates a special player-only patron family by becoming head

### 4.3. Captured faction defaults

If player becomes head of an existing faction:

- `charter` remains unchanged
- `originType` remains unchanged
- `baseStrength` remains unchanged
- only `leadership` mutates

If a successful `revolt` occurs:

- `gates.isStale = true`
- no separate persistent `revoltPenalty` field is introduced in v1
- aftermath is expressed through:
  - the leadership receipt/history
  - resident `factionLoyaltyLevel` and `factionRestlessness` shifts
  - normal accepted-turn political consequences

### 4.4. Contested leadership is explicit state

`leadership.leadershipState = contested` is not a silent derived formula.

It may be written only when an accepted turn materializes political pressure and at least one of the following is true:

- at least `2` ascended residents explicitly support a challenger
- at least `1/3` ascended faction residents are already in `considering_realignment` or `ready_to_realign`
- faction suffered a major recent setback and the turn explicitly records legitimacy crisis

Accepted succession or revolt normally ends with:

- `leadershipState = secure`

Accepted abdication with no successor ends with:

- `leadershipState = vacant`

---

## 5. Precedence and lock model

A resident may carry multiple derived pressure states at once, but may not participate in multiple pending relocation/political flows at once.

### 5.1. Hard precedence order

Canonical precedence of requestable flows:

1. ordinary inter-Abode transfer
2. Shining faction realignment
3. Shining founding support
4. Shining leadership participation

“Higher priority” means:

- if two flows are only derived-state eligible and no request exists yet, the higher-priority one may be opened first
- once a pending request exists, lower-priority flows for the same resident are blocked until it resolves or is withdrawn

### 5.2. Lock table

| Resident condition | New flow openings that must be rejected |
|---|---|
| resident already has pending ordinary inter-Abode transfer request | Shining faction realignment, founding support, leadership support/candidacy |
| resident already has pending Shining faction realignment request | ordinary inter-Abode transfer, founding support, leadership support/candidacy |
| resident is listed in pending founding `supportingResidentIds[]` | ordinary inter-Abode transfer, Shining faction realignment, leadership support/candidacy |
| resident is listed in pending leadership `supportingResidentIds[]` | ordinary inter-Abode transfer, Shining faction realignment, founding support |
| resident is current candidate or incumbent in pending leadership transition | ordinary inter-Abode transfer, Shining faction realignment, founding support |
| resident is the current `leadership.headActorType = resident` head of a faction | ordinary inter-Abode transfer, Shining faction realignment, founding support, external leadership support/candidacy until leadership resolves |

### 5.3. Derived-state coexistence

Allowed:

- a resident may simultaneously be:
  - `migrationState = ready_to_transfer`
  - `factionRealignmentState = ready_to_realign`

Not allowed:

- opening both pending requests at the same time

Default choice:

- if both states are eligible and no pending request exists yet, inter-Abode transfer wins and must be resolved first

---

## 6. Explicit implementation defaults

To avoid future re-interpretation, the following are now fixed:

- canonical faction shape uses nested `charter` and `leadership`
- old flat faction fields are deprecated and must not be implemented in parallel
- `headActorType` may be `guardian | player_soul | resident | radiant_actor`
- guardian losing leadership never becomes resident
- former `radiant_actor` heads remain in `shiningPoliticalActors[]`
- `player_founded` faction requires exactly the same minimum supporter rule everywhere: `3` ascended supporting residents
- founding uses an explicit control file and receipt, not freeform accepted-turn prose
- player-founded hall payload is explicit in the founding request and is not derived later from charter prose
- Shining faction realignment uses its own request/receipt flow and does not reuse ordinary Abode transfer files
- leadership transition uses explicit request/receipt/history parity
- patron/head cards read `charter.patronEffectFamily`, not current head identity
- no resident may be pending in two relocation/political flows at once

---

## 7. Minimum validator expectations

The future implementation is not complete unless validator parity enforces all of the following:

- `factions[]` reject old flat current-head fields if they are materialized alongside nested `leadership`
- `leadershipState = vacant` rejects non-null current head references
- every current head cross-ref resolves against the correct owner by `headActorType`
- `player_soul` may head at most one faction at a time
- `resident` head must belong to the same faction
- `player_founded` founding without `3` valid supporters fails
- duplicate `proposedFactionId` / `proposedHallId` across current state and pending founding requests fail
- founding with malformed or incompatible hall payload fails
- `targetFactionId = sourceFactionId` in realignment request fails
- pending Shining request files reject malformed enums and partial contracts
- successful realignment/founding/leadership transitions reject missing receipts
- successful leadership transition rejects missing `leadershipHistory[]`
- simultaneous pending leadership requests for the same faction fail
- leadership support from residents outside the target faction fails
- current resident-head cannot change `shiningFactionId`, open ordinary transfer, or act as founding supporter without same-turn leadership resolution
- conflicting pending flows on one resident fail even if prose claims all sides agreed

This closes the remaining implementation questions left open by the older Shining documents.
