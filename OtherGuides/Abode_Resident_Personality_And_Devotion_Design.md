# Abode Resident Personality And Devotion Design

## Summary

Current Shining Abode residents already have:

- `bondLevel` / `bondTier`
- `residentKind`, `originType`, `summary`
- `mortalWorldImprint` with `coreTraits`, `archetypeHints`, `appearanceMotifs`, `bondReason`
- `thoughtJournal`, `interactionLog`, `historyLog`
- talk/history/reward/manifestation hooks

This is enough to support memory and relationship progression, but not enough to make residents feel like autonomous personalities. The missing layer is not "more journal text"; it is a resident-specific internal model that answers:

1. who the resident is,
2. what the resident feels toward the Guardian,
3. what the resident feels toward the Abode itself,
4. why the resident stays, wavers, or eventually leaves.

The core design decision is:

- `bond` stays a personal bond with the Guardian,
- `abode devotion` becomes a separate dynamic relationship with the Abode,
- `abode power` influences that devotion,
- personality determines how strongly the resident reacts to power, decline, safety, prestige, and communal belonging.

Residents should become a distinct actor type: deeper than the current resident model, but still lighter and more focused than full mortal-world NPCs.

---

## Design Goals

The system should achieve all of the following:

- residents must feel like small autonomous personalities, not just bonded entourage,
- low/high Abode Power must have social consequences inside the Abode,
- two residents with the same `bondLevel` must still react differently to the same Abode state,
- migration away from an Abode must be possible, but only as a staged narrative outcome rather than a single threshold flip,
- the system must integrate with existing resident journals, interaction receipts, quests, rewards, and manifestation lines,
- the system must be compatible with actor reasoning, so resident changes are explainable in GM reasoning instead of being invisible number changes.

Non-goal for V1:

- do not try to make residents fully equivalent to world NPCs with the entire NPC schema and content surface.

---

## Conceptual Model

Each resident should be understood through 5 layers.

### 1. Identity

This is the stable descriptive layer that already mostly exists.

Use existing fields such as:

- `residentKind`
- `originType`
- `roleLabel`
- `summary`
- `mortalWorldImprint`

This layer answers: "what kind of being is this?"

### 2. Personality Profile

This is the rich current psychology of the resident.

It should follow the **NPC model as an inspiration**, not a tiny enum-only profile.
Residents still do not need the entire mortal-world NPC schema, but their personality layer should be rich enough to support:

- believable internal motives,
- distinct worldview and tone,
- reasoning-visible individuality,
- journal-visible emotional consequences.

Recommended new object on each resident entry:

`personalityProfile`

Recommended fields:

- `archetype`
- `worldview`
- `culturalLayer`
- `coreValues`
- `personalityTraits`

Recommended meanings:

- `archetype`: the compact high-level identity of the resident's personality
- `worldview`: how the resident interprets duty, meaning, belonging, and change
- `culturalLayer`: the symbolic/cultural texture through which the resident understands life in the Abode
- `coreValues`: the resident's most stable ideals or priorities
- `personalityTraits`: a richer trait array, closer to NPC `personalityTraits`, that can hold named traits with intensity and short explanation

Recommended `personalityTraits[]` item shape:

- `traitName`
- `value` (`1..10`)
- `valueDescription`
- optional `description`

Design default:

- `personalityProfile` is a **rich authored personality surface**, closer to NPC personality data than to a tiny canonical enum bundle,
- it should remain lighter than full NPC state,
- it should **not** be the primary deterministic mechanics surface for devotion formulas.

Practical meaning:

- `personalityProfile` exists for authored identity, reasoning, journaling tone, and player-facing richness,
- the deterministic devotion mechanics should read from a separate compact layer described below.

### Relationship to `mortalWorldImprint`

This must be explicit for implementation:

- `mortalWorldImprint` remains an origin-memory layer,
- `personalityProfile` becomes the current rich psychology layer,
- `personalityProfile` is derived once when the resident is created or first normalized into the new model,
- the same seed pass also derives a compact `abodeDisposition` mechanics layer,
- the derivation uses existing seed inputs such as `residentKind`, `originType`, `mortalWorldImprint.coreTraits`, `mortalWorldImprint.archetypeHints`, and `mortalWorldImprint.bondReason`,
- after that, there is no automatic two-way sync between `mortalWorldImprint` and either `personalityProfile` or `abodeDisposition`.

Design default:

- this is a one-way seed relationship, not a mirrored relationship.

Practical meaning:

- `mortalWorldImprint` answers "what remained from the resident's earlier identity or mortal imprint",
- `personalityProfile` answers "how this resident currently behaves and interprets Abode life",
- `abodeDisposition` answers "how this resident mechanically reacts to the condition of the Abode".

### 3. Abode Disposition

This is the compact machine-readable mechanics layer for abode loyalty dynamics.

Recommended new object on each resident entry:

`abodeDisposition`

Recommended fields:

- `powerSensitivity`
- `migrationDisposition`
- `communalOrientation`
- `stabilityNeed`

Recommended meanings:

- `powerSensitivity`: how strongly the resident reacts to Abode strength, prestige, or decline
- `migrationDisposition`: how naturally the resident endures, hesitates, or seeks relocation
- `communalOrientation`: how much shared life in the Abode matters beyond personal attachment
- `stabilityNeed`: how painful instability, weakness, or disorder feels

Recommended canonical values can stay compact, for example:

- `powerSensitivity`: `low` / `medium` / `high`
- `migrationDisposition`: `rooted` / `selective` / `opportunistic` / `wandering`
- `communalOrientation`: `low` / `medium` / `high`
- `stabilityNeed`: `low` / `medium` / `high`

Design default:

- `abodeDisposition` is the deterministic mechanics profile used by devotion/restlessness formulas,
- `personalityProfile` remains the richer authored explanation layer,
- do not try to infer devotion mechanics directly from freeform personality prose during V1.

### 4. Abode Relation

This is the new missing layer.

Recommended new dynamic fields on each resident entry:

- `abodeDevotionLevel` (`0..100`)
- `abodeDevotionTier`
- `restlessness` (`0..100`)
- `migrationState`

Canonical contract:

- `abodeDevotionLevel` is the authoritative scalar,
- `abodeDevotionTier` is a derived canonical label from `abodeDevotionLevel`,
- `restlessness` is the authoritative scalar,
- `migrationState` is a derived canonical state from `abodeDevotionLevel` and `restlessness`,
- in V1, neither `abodeDevotionTier` nor `migrationState` should be treated as freeform independently-authored fields.

Recommended `abodeDevotionTier` values and thresholds:

- `alienated` = `0..19`
- `uncertain` = `20..39`
- `attached` = `40..59`
- `devoted` = `60..79`
- `steadfast` = `80..100`

Implementation default:

- `abodeDevotionTier` should normalize from `abodeDevotionLevel` exactly the way `bondTier` already normalizes from `bondLevel`.

Recommended `migrationState` values:

- `settled`
- `wavering`
- `restless`
- `considering_departure`
- `ready_to_transfer`

Important distinction:

- `bondLevel` = personal relationship with the Guardian
- `abodeDevotionLevel` = willingness to remain in this Abode as a place and order
- `restlessness` = internal pressure to move, detach, or seek another state

These must remain separate. They are related, but not interchangeable.

### Canonical `migrationState` resolver

For V1, `migrationState` should be derived by a deterministic resolver, not by ad hoc authored prose.

Recommended resolver order:

1. `ready_to_transfer` if `abodeDevotionLevel <= 15` **and** `restlessness >= 70`
2. `considering_departure` if `abodeDevotionLevel <= 30` **and** `restlessness >= 55`
3. `restless` if `abodeDevotionLevel <= 45` **or** `restlessness >= 45`
4. `wavering` if `abodeDevotionLevel <= 60` **or** `restlessness >= 30`
5. otherwise `settled`

Design default:

- personality influences how `abodeDevotionLevel` and `restlessness` change,
- `migrationState` is then derived from those already-filtered values,
- do not add hysteresis or a separate manual transition state machine in V1.

### 5. Memory And Meaning

This layer already exists in partial form and must now affect behavior mechanically.

Sources:

- `thoughtJournal`
- `interactionLog`
- `historyLog`
- resident quest / reward / relic outcomes

This layer answers: "why does the resident feel this way right now?"

It should be the main explanation layer for future devotion shifts.

---

## Abode Power Interaction

Abode Power must influence residents, but never as a simple direct command like:

- "power below X means resident leaves."

Instead:

- Abode Power creates pressure,
- `abodeDisposition` filters that pressure mechanically,
- `personalityProfile` colors how that pressure is expressed and narrated,
- bond and memory modulate the result.

Use the existing Abode Power tiers from `AbodePowerRules`:

- `0-19` = `Угасающая`
- `20-39` = `Хрупкая`
- `40-59` = `Стабильная`
- `60-79` = `Могущественная`
- `80-100` = `Сияющая`

Treat these tiers as the emotional/political climate of the Abode.

### How disposition should filter power

Suggested examples:

- a resident with high `powerSensitivity` cares a lot about whether the Abode is `Могущественная` or `Сияющая`
- a resident with high `stabilityNeed` reacts strongly to `Угасающая` or `Хрупкая`
- a `rooted` resident can remain stable even in decline
- a `wandering` resident may leave even without hostility, simply because motion is part of its nature

This means the same power shift should produce different results for different residents.

---

## Dynamic Behavior Model

Resident state should update through meaningful events, not noisy every-turn randomness.

### Main inputs

Recommended event sources:

- Abode Power tier changes
- strong Guardian successes/failures that visibly strengthen or weaken the Abode
- resident talk/history outcomes
- resident quest progression
- resident reward / relic outcomes
- long neglect or repeated rejection
- protection, shelter, rescue, recognition, or betrayal motifs recorded in journals

### Recommended internal update logic

Think of each update as:

`devotion change = abode pressure + bond protection + memory weight + disposition filter`

Where:

- `abode pressure` comes from current Abode Power and its recent direction
- `bond protection` comes from personal connection with the Guardian
- `memory weight` comes from recent scenes and outcomes
- `disposition filter` decides what the resident mechanically cares about

Additional rule:

- `personalityProfile` should still shape the wording and meaning of these changes in journals, reasoning, and UI, even when the mechanical shift is produced by `abodeDisposition`.

This lets a resident:

- love the Guardian but lose faith in the Abode,
- remain loyal to the Abode despite modest personal bond,
- become unsettled without immediately leaving,
- recover from decline if new scenes rebuild meaning.

---

## Migration Model

Do not implement instant transfer on a single threshold.

Migration should be staged.

### Recommended progression

1. `abodeDevotionLevel` drops
2. `restlessness` rises
3. resident becomes `wavering`
4. then `restless`
5. then `considering_departure`
6. only then `ready_to_transfer`

This ensures migration feels like narrative consequence, not algorithmic teleportation.

### Important rule

`ready_to_transfer` should not automatically mean:

- immediate reassignment to another Guardian

Instead it should mean:

- the resident is now a valid candidate for a transfer/resolution event

Recommended V1 behavior:

- allow all the internal stages up to `ready_to_transfer`
- surface them in journals and reasoning-visible state only
- do not yet perform full automatic inter-Abode migration unless another explicit design pass covers it

Recommended V2/V3 behavior:

- add candidate selection for a new Abode,
- add reminders / notifications / receipts / explicit resolution,
- then allow actual transfer.

---

## Relationship To Existing Bond System

Bond must remain important, but it should stop being the only meaningful axis.

Recommended interpretation:

- `bondLevel` measures closeness, trust, and personal history with the Guardian
- `abodeDevotionLevel` measures belonging, faith, and willingness to remain within the current Abode order
- `restlessness` measures pressure to leave or seek change

This gives interesting combinations:

- high bond, low devotion
- low bond, high devotion
- medium bond, high restlessness
- high devotion but low intimacy

These combinations are much richer than a single "bond goes up/down" model.

---

## Brain Protocol / Reasoning Integration

Residents should not silently change state.

If a turn changes any of the following:

- resident entry identity or relationship fields,
- `abodeDevotionLevel`,
- `restlessness`,
- `migrationState`,
- resident journal outcomes,
- resident quest/reward progression,

then the resident should be treated as a relevant actor in reasoning.

### Practical rule

Extend the existing actor reasoning discipline so that affected residents must be represented in:

- `Relevant actors`
- actor reasoning blocks

Minimum reasoning questions for a resident:

1. what does the resident want now?
2. how does the resident perceive the current state of the Abode?
3. what is the resident feeling toward the Guardian right now?
4. why is the resident staying, wavering, or preparing to leave?

This is the resident equivalent of a brain protocol. It does not need to be as wide as NPC reasoning, but it must be explicit whenever the system changes resident state with agency implications.

---

## Recommended Data Model Direction

Recommended additions to each resident entry:

```json
{
  "personalityProfile": {
    "archetype": "Steady Attendant",
    "worldview": "Believes belonging is proven through service and constancy.",
    "culturalLayer": "Ritual household spirit shaped by vows of continuity.",
    "coreValues": ["service", "continuity", "gratitude"],
    "personalityTraits": [
      {
        "traitName": "Loyalty",
        "value": 9,
        "valueDescription": "Clings to chosen bonds and duties.",
        "description": "Feels most whole when serving a lasting order."
      },
      {
        "traitName": "Pride",
        "value": 4,
        "valueDescription": "Prefers dignity but not dominance."
      }
    ]
  },
  "abodeDisposition": {
    "powerSensitivity": "medium",
    "migrationDisposition": "rooted",
    "communalOrientation": "high",
    "stabilityNeed": "high"
  },
  "abodeDevotionLevel": 72,
  "abodeDevotionTier": "devoted",
  "restlessness": 12,
  "migrationState": "settled"
}
```

The conceptual split should remain exactly this:

- rich authored personality profile,
- compact mechanical abode disposition,
- dynamic devotion/restlessness/migration state.

For V1, however, two things are fixed and should not be left open:

- `abodeDevotionTier` uses the canonical 5-tier mapping defined above,
- `migrationState` uses the canonical derived resolver defined above.

Do not overload `mortalWorldImprint` with all of this. That imprint should remain about remembered mortal identity and origin motifs, not become the full current resident psychology container.

Implementation rule:

- existing `mortalWorldImprint.coreTraits` and `mortalWorldImprint.archetypeHints` are treated as seed inputs,
- new `personalityProfile` fields are treated as the authoritative current rich personality surface,
- new `abodeDisposition` fields are treated as the authoritative current mechanics surface for devotion filtering,
- no automatic back-write from `personalityProfile` or `abodeDisposition` into `mortalWorldImprint`.

---

## Recommended Implementation Phasing

### V1

Implement:

- resident `personalityProfile`
- resident `abodeDisposition`
- `abodeDevotionLevel`
- `abodeDevotionTier`
- `restlessness`
- `migrationState`
- integration with Abode Power
- resident reasoning requirement when these fields change
- journal-visible consequences for wavering/restless states

Do not implement full automatic transfer yet.
Do not add new reminder/notification surfaces yet.

### V2

Implement:

- devotion/restlessness changes driven by concrete accepted-turn events
- notifications and reminder hooks for wavering/restless states
- narrative hooks for "resident may leave"

### V3

Implement:

- explicit migration / transfer resolution between Abodes
- canonical receipts / history for departure/arrival
- optional competition between Abodes for certain resident personalities

---

## Key Design Defaults

- residents are not full NPC clones,
- residents should borrow NPC-like richness for personality authoring,
- devotion mechanics should read from `abodeDisposition`, not from freeform personality text alone,
- bond and abode devotion are separate systems,
- Abode Power influences devotion but does not dictate it,
- migration is staged, not instant,
- disposition determines mechanically whether low Abode Power matters,
- personality explains and colors that reaction,
- journals and reasoning must explain changes, not just store them,
- V1 should stop before full automatic inter-Abode transfer.

---

## Final Design Intent

The target state is:

- a resident is not just "someone with bond points,"
- a resident is a small autonomous being with a temperament and a readable inner world,
- the Abode becomes a real social environment rather than a static venue,
- power, belonging, care, decline, and aspiration all become visible in resident behavior,
- and the possibility of staying or leaving becomes an earned narrative consequence.

That is the intended direction for implementation.
