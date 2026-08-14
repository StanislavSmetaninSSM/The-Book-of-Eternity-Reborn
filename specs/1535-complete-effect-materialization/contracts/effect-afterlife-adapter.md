# Contract: Afterlife Effect Adapter

**Feature**: [Complete Effect Materialization](../spec.md)
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

## 1. Scope

Afterlife uses two active-effect contours:

1. Persistent actor/profile effects for player soul, Guardians, residents, Shining faction heads, radiant actors, and other accepted afterlife profile kinds.
2. Short-lived spiritual-conflict `combatConditions[]` with specialized mechanics.

Both share common identity, exact authority, stacking, event replay, and terminal history. Their specialized mechanics remain distinct.

## 2. Persistent Actor Effects

- Canonical carrier: `game_state/meta/afterlife_entity_profiles.json` profile `activeEffects[]`.
- Owner selector: exact `(actorType, actorId)` accepted by actor materialization/profile authority.
- Realm: exact `chaos_sea` or `shining_abode`, consistent with the profile's accepted realm/location.
- Source definitions may come from an accepted spiritual art, Fate Card, Soul Relic/item, quest, actor source, Shining faction state, location/hazard, or registered afterlife rule.
- Guardian/resident/radiant actor source files remain their own semantic authority; effect state is not duplicated into them when the accepted profile is the logical carrier.
- `pendingShiningBlessingEffects` and equivalent client-derived entitlement fields are not active effects and cannot be targeted by `effectChanges[]`.

## 3. Spiritual Combat Conditions

Canonical carrier: `game_state/meta/afterlife_spiritual_conflict_state.json` under `activeConflict.combatConditions[]`.

Existing specialized values remain:

- kind: `mark`, `ward`, `burden`, `opening`, `vow`;
- exact target side/participant;
- source and readable display;
- non-empty affected operations;
- legal mechanical axes only;
- counterplay and payoff;
- visibility `visible`, `hidden`, or `gm_only`;
- finite exchange/scene duration or bounded uses;
- explicit create/consume/expire/clear behavior.

The common adapter additionally requires:

- client-generated permanent `effectId`;
- exact source kind/source ID/definition key;
- exact conflict/side target binding;
- source-authorized stack coordinate and policy;
- exact transition/event evidence;
- matching identity-index entry;
- replay-safe terminal history.

## 4. Command Change

`afterlifeSpiritualConflictUpdate` continues to author legal conflict/exchange state, audits, positions, strain, control, tempo, and outcomes. It no longer directly creates, rewrites, consumes, or clears `combatConditions[]`.

Condition lifecycle is requested through `effectChanges[]`:

```json
{
  "operation": "apply",
  "target": {
    "kind": "spiritual_conflict_side",
    "targetId": "conflict_exact:player"
  },
  "source": {
    "kind": "spiritual_art",
    "sourceId": "art_exact",
    "definitionKey": "binding-mark"
  },
  "parameters": {},
  "eventRef": {
    "kind": "afterlife_exchange",
    "authorityId": "exchange_exact"
  },
  "reason": "Метка закрепилась после обмена."
}
```

The effect plan composes this with the same exchange update and writes the final `combatConditions[]` once.

## 5. Legal Mechanical Mapping

The `afterlife_combat_condition` component may affect only the existing legal axes:

- `rollMode.*.advantageSources` / `disadvantageSources`;
- `conflictPosition`;
- `controlState` only through registered anti-control softening/narrowing/clearing operations;
- `playerSideStrain` and `oppositionSideStrain`;
- `tempoAdvantage`;
- `counterPayoff`;
- action-cost audit / ОД cost;
- `specialArtAudit.effectNote`.

It cannot create generic passive stat stacking, arbitrary control state, undocumented action economy, or Mortal HP mechanics.

## 6. Visibility and Projection

- `visible` conditions show name, target, source label, affected operations, remaining duration/uses, counterplay, payoff summary, and available action.
- `hidden` and `gm_only` conditions and their identifying tokens are removed from ordinary console/browser state, audits, counts, and actions.
- Internal `effectId`, exact technical source/target selectors, stack key, transition history, and pending/repair data never enter player DTOs.
- Console and browser derive the same visible condition cards/table facts through the common projection adapter.

## 7. Composition and Atomicity

The afterlife exchange update, profile effects, spiritual conditions, identity index, pending resolutions, audits, output, and companions share one accepted transaction. If either the conflict-specific validator or common effect validator fails, no partial axis, condition, profile effect, reward, narrative, or output remains.

## 8. Required Negative Cases

Reject before publication:

- direct non-empty `combatConditions[]` change without a matching common effect operation;
- condition with missing identity/source/target/finite lifetime/counterplay/legal axis;
- effect and conflict target disagreement;
- Mortal target/source routed into afterlife without explicit policy;
- hidden condition token appearing in player-facing audit/output;
- replayed exchange applying or consuming the condition twice;
- actor profile effect written to both profile and Guardian/resident source files;
- attempt to author Shining blessing entitlement as an active effect.
