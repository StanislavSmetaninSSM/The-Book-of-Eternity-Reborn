# Contract: Effect Identity, Source, and Target Authority

**Feature**: [Complete Effect Materialization](../spec.md)
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

## 1. Identity Ownership

The client exclusively owns:

- permanent `effectId`;
- lifecycle `transitionId`;
- terminal-history entry;
- identity-index root and entries;
- current logical carrier coordinate;
- current stack coordinate and client-computed count;
- pending-resolution and validation-repair request IDs;
- receipt acceptance and replay evidence.

The GM may receive opaque existing `effectId` values in technical context only to request an allowed dispel/removal. Player projections never expose them.

## 2. Identity Allocation

- Allocate random opaque identities once inside one cached `EffectAcceptedTurnPlan`.
- Reuse the same plan instance across raw validation, companion validation, normalization, and post-check.
- Replaying the same accepted operation under the same session/snapshot reuses its transition outcome or becomes a no-op; it never allocates a second result.
- A changed session, snapshot, source catalog, target catalog, input, or relevant same-turn plan invalidates the cache.
- Do not derive permanent IDs from GM-visible input.

## 3. Identity Index Agreement

For each active/suspended instance:

1. Exactly one index entry has its `effectId`.
2. Exactly one logical carrier occurrence exists at the index owner coordinate.
3. Instance and index agree ordinally on state, realm, target, source, stack coordinate, creation turn, and latest transition.
4. Owner path and collection are allowlisted for the declared target kind.
5. Active IDs, terminal IDs, transition IDs, event replay keys, and applicable receipt IDs are exact/confusable unique.

For each terminal entry:

1. State is `expired`, `dispelled`, `removed`, or `replaced`.
2. No active carrier occurrence remains.
3. The terminal transition is complete and immutable.
4. Replay of the same event cannot recreate a reward, tick, stack, expiry, or removal.

## 4. Source Authority

The source catalog is read-only and composed from:

- validated pre-turn canonical source entities;
- accepted same-turn effective identities exported by applicable materialization plans;
- closed built-in operation definitions where the game itself owns the source rule.

Supported source kinds:

```text
skill, spiritual_art, item, wound, quest, location, hazard, faction,
world_event, fate_card, combat_action
```

An exact source match requires:

- exact source kind and permanent/effective source ID;
- exact `definitionKey` inside that source;
- source realm compatible with target realm;
- allowed target kind;
- all requested parameter keys and values inside declared bounds;
- complete component/stack/lifetime/trigger/removal/link policy;
- source current state authorizes application (for example equipped, learned, active, unlocked, wounded, quest-active, or event-active when required).

Rejected source selectors include:

- display names or inferred source from prose;
- trimmed/case/Unicode-confusable aliases;
- retired/historical IDs;
- multiple exact/confusable candidates;
- raw same-turn entities that did not pass their owning materializer;
- cross-realm sources without an explicit registered cross-life policy;
- definitions that describe only an instantaneous action or passive static mechanic.

## 5. Target Authority

The target catalog is composed from validated pre-turn owners plus accepted same-turn effective actor/combat identities.

| Target kind | Required authority |
| --- | --- |
| `player` | Exact current Mortal player or accepted `player_soul` profile, realm-specific |
| `npc` | Exact accepted named-NPC identity |
| `combatant` | Stable client-owned combat-local `combatantId`; a new raw combatant uses exact same-turn `combatantRef`, and named NPC binding must agree if present |
| `guardian` | Exact accepted Guardian/profile binding |
| `resident` | Exact accepted Abode/Shining resident/profile binding |
| `radiant_actor` | Exact accepted radiant actor/profile binding |
| `afterlife_actor` | Exact accepted afterlife profile kind and actor ID |
| `spiritual_conflict_side` | Exact active conflict plus legal side/participant target |

Target matching rejects:

- name, array position, or UI selector as authority;
- case/whitespace/Unicode-confusable/historical alias;
- duplicate owner entries or ambiguous profile bindings;
- target absent from the composed accepted state;
- realm mismatch;
- target kind not allowed by the source definition.

For a same-turn target, the raw selector contains `targetRef` instead of `targetId`. The target's accepted plan must export exactly one permanent effective identity for that reference. The canonical effect stores only the permanent identity. The GM may not submit a new `combatantId`.

## 6. Same-Turn Composition

The effect planner may consume only explicit effective-identity exports from another accepted plan. It must not parse raw sibling objects to invent authority.

Representative valid compositions:

- a newly materialized item authorizes an effect applied after exact acquisition/equip authority;
- a same-turn accepted NPC or afterlife profile receives a source-authorized effect;
- a same-turn accepted wound authorizes a linked consequence without effect-side wound mutation;
- a same-turn quest/event/location/faction source exports a definition used by the effect plan;
- an accepted combatant target receives an effect by its stable combat-local anchor.

If an owning plan later fails, the effect plan and the entire accepted response fail atomically.

## 7. Direct-Mutation Protection

Before planning, compare every current effect carrier and the identity index with the validated pre-turn snapshot after accounting only for recognized raw commands owned by other authorities. Reject:

- added/removed/edited active instances without `effectChanges[]`;
- added/edited identity/index/transition/receipt fields;
- carrier movement not derived by the effect planner;
- raw non-empty initial combat/profile conditions outside the common apply route;
- duplicated effect copies across carriers;
- changed source, target, realm, stack key, current count, or remaining state.

## 8. Protected Failures

The following never produce an actionable repair:

- identity, transition, receipt, index, or carrier mismatch;
- source or target unresolved/ambiguous/confusable/historical/cross-realm;
- stack coordinate conflict;
- duplicate event or replay evidence;
- direct canonical mutation;
- malformed or forged client-owned DTO;
- missing/invalid wound source binding;
- stale cached plan or session replacement.

These failures restore baseline, write operator-only path-bound diagnostics, and show generic in-world player text.
