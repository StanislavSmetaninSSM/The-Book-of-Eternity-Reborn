# Contract: Effect Player Projection

**Feature**: [Complete Effect Materialization](../spec.md)
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

## 1. Projection Gate

Console and browser may project active effects only from one accepted `EffectMechanicsSnapshot`/`EffectPlayerProjection` result. They must not parse raw `effectChanges[]`, legacy aliases, unvalidated carrier arrays, identity-index state, pending requests, or repair diagnostics.

If carrier/index/source/target agreement fails, the projection is unavailable as a whole. The player receives an in-world unavailable/status explanation without partial mechanical claims.

## 2. Visible Effect Facts

For `visibility=visible`, expose all applicable facts:

- readable name and description;
- category (`усиление`, `ослабление`, `состояние`, `воздействие среды`, or mixed in-world label);
- readable source label when source visibility permits;
- each player-relevant component effect, target axis/resource/action, value/bound, and trigger interval;
- current and maximum stacks;
- remaining turns, uses, exact-time in-world explanation, scene, source-bound, condition-bound, permanent, or manual state;
- readable counterplay, cure, or dispel options;
- readable linked wound/item/skill/art/quest/environment context when its own visibility permits;
- suspended/active status and source-declared reason when player-visible.

Unknown registered setting-specific component semantics must still render through that component's projection contract; a generic renderer must not silently drop them.

## 3. Hidden and GM-Only Effects

For `hidden` or `gm_only`:

- omit ordinary rows, cards, tables, counts, detail selectors, and actions;
- omit direct source/target/stack/lifetime facts;
- omit identifying tokens from combat/spiritual audits, news, quests, maps, dossiers, status summaries, and serialized game-screen data;
- show a derived total only when a separate accepted visibility rule explicitly permits the consequence without revealing the source;
- never allow name/action lookup to infer the hidden instance.

## 4. Internal DTO Suppression

Suppress whole objects whose exact shape identifies:

- active effect identity/index entry or transition;
- effect source/target authority object in technical context;
- carrier coordinate or stack coordinate;
- pending effect resolution or its receipt;
- effect repair packet;
- validation repair request wrapper;
- diagnostic failure report wrapper;
- before-image/rollback/path-bound report;
- GM/worker/agent instruction wrapper.

Shape suppression must match valid annotated supersets of a technical DTO, not only exact property sets. Preserve adjacent legitimate semantic objects that merely use generic keys such as `kind`, `title`, `steps`, `turn`, or `source` without the technical signature.

## 5. Action Selectors

Player-visible cure/counterplay/dispel actions use a short-lived opaque UI selector resolved back through the current accepted projection. They do not expose permanent `effectId` or source/target IDs in display text or serialized public DTO fields.

At submit time, re-resolve the selector against the latest accepted snapshot and revalidate:

- effect remains active and visible/actionable;
- target and realm are unchanged;
- selected action is still source-authorized;
- required item/skill/resource/quest/wound/treatment authority remains exact;
- no hidden effect can be selected by a forged/stale index.

Failure produces generic in-world text and no state change.

## 6. Surface Coverage

The shared projection must drive or sanitize:

- Mortal player `/effects`/status/characteristics summaries and details;
- NPC list/detail/status and combatant dossiers;
- afterlife actor/Guardian/resident/radiant profile views;
- spiritual-conflict condition cards, tables, audits, and previews;
- local cure/dispel/counterplay forms and result confirmations;
- browser game-screen status/action DTOs;
- effect-bearing quest/news/map/item/source-context narratives where an active instance is embedded;
- any generic recursive reference/detail renderer that can encounter active-effect DTOs.

## 7. Console/Browser Parity Matrix

For the same accepted state, both clients must agree on:

| Semantic | Required parity |
| --- | --- |
| Presence | Same visible effects; hidden/GM-only absent |
| Mechanics | Same registered component facts and values |
| Source | Same readable label/unavailable explanation |
| Lifetime | Same mode and remaining state |
| Stacks | Same current/max count |
| Status | Same active/suspended/terminal absence |
| Actions | Same cure/counterplay/dispel eligibility and block reason |
| Failure | Same safe in-world meaning without technical vocabulary |

Visual layout may differ. No client may expose a mechanic or action the other suppresses because it bypassed the shared authority.

## 8. Wound Projection Boundary

An effect detail may show a readable linked wound label and symptom context. It must not imply that removing the effect heals the wound. Action copy must distinguish:

- suppressing/dispelling/removing the active consequence;
- treating/healing the wound through the independent wound contract (#1536).

Effect-only success confirmation must never claim wound treatment, severity change, or wound deletion.

## 9. Negative Projection Tests

Required fixtures include:

- hidden and GM-only active instances in every owner family;
- visible effect beside a full identity-index DTO;
- visible effect beside pending/repair/diagnostic wrappers with readable generic residual fields;
- annotated technical DTO supersets;
- stale/forged UI selector;
- malformed carrier with one otherwise readable effect;
- nested active effect in quest/news/reference/status data;
- wound-derived effect removal with unchanged wound row/detail;
- non-item/non-effect semantic objects containing legitimate `kind`, `title`, `steps`, `turn`, `source`, or `route` values to prove no global overfiltering.
