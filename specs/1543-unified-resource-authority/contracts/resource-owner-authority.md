# Resource Owner Authority Contract

## 1. Principle

A resource coordinate is valid only when its owner exists in the complete accepted owner catalog. Resource commands never create permanent owners. The owning actor, item, combat, or afterlife materializer creates/resolves identity and exports resource capability.

## 2. Existing owners

| Owner kind | Exact authority |
| --- | --- |
| `player` | The current Mortal player, ID `player_current` |
| `npc` | One permanent accepted Mortal NPC ID |
| `combatant` | One client-owned permanent anonymous combatant ID |
| `combat_group_member` | One client-owned permanent group member ID |
| `item` | One permanent item ID from the accepted item identity catalog |
| `afterlife_actor` | One permanent accepted afterlife profile/actor ID |
| `afterlife_conflict_side` | One client-owned identity scoped to an active conflict |
| `afterlife_scope` | One client-owned return-cycle/Abode scope identity |

All identity comparison is exact ordinal plus confusable detection. A display name is never a fallback.

## 3. Same-turn owners

The raw owner uses exactly one temporary ref declared by its owning contract:

- `combatantRef` for a new individual combatant;
- `memberRef` for a new group member;
- the existing actor/item/profile temporary reference field for its materializer;
- a client-created scope/ref supplied by the owning local transition where GM authorship is not allowed.

The common owner plan allocates every permanent ID once, rewrites all accepted owner after-images, and exports a ref-to-key map. Canonical state must contain no temporary refs after publication.

A raw resource target may use the exact accepted ref but cannot submit the resulting permanent ID. Case/confusable/duplicate refs and cross-kind reuse fail the whole plan.

## 4. Named NPC in combat

A combat row with `NPCId` must resolve to exactly one composed named NPC. It references the NPC resource owner and must not initialize or carry a combat-local copy of the NPC's health/poise/resources.

An anonymous combatant must not claim an NPC binding. Two combat rows cannot claim the same named NPC where the owning combat contract requires one representation.

## 5. Combat groups

A group contains stable member records. New members carry `memberRef` and omit `memberId`; existing members carry `memberId` and omit `memberRef`. `healthStates[]`, list position, `count`, and unit display name are not resource identity.

Reordering members preserves coordinates. Removing a member requires exact terminal owner evidence and retires only that member's resources. Group deletion retires every still-live member atomically.

## 6. Items

Item resources use permanent item identity regardless of inventory, equipment, NPC inventory, location storage, offscreen storage, or another valid carrier. Movement changes no resource coordinate. Split/merge/stack operations must explicitly define which permanent item identities survive before any resource plan is accepted.

Item destruction or terminal consumption retires all item resources only after immutable terminal evidence is prepared. A raw removal cannot silently discard nonempty resource state.

## 7. Afterlife owners and scopes

Persistent profiles keep one permanent resource owner per realm binding. Active spiritual-conflict sides use conflict-scoped IDs. Per-return Guardian/Shining reserves use accepted actor/scope IDs and registered return-cycle capacity authority.

Conflict close or return-cycle replacement retires scoped resources. Persistent actor resources survive unrelated conflict closure. Currencies, progression, relationships, and faction accounting are not owner exports for this ledger.

## 8. Capability and lifecycle

An owner export contains an exact set of resource keys/capabilities derived from its accepted definition/materialization state. A definition's `allowedOwnerKinds` is necessary but insufficient: the owner must also permit that resource.

Owner states:

```text
active -> suspended -> active
active/suspended -> terminal
```

An inactive, removed, wrong-realm, historical, ambiguous, or unaccepted owner cannot initialize or mutate a coordinate. Terminal owners cannot be revived by a resource command.

## 9. Fingerprint

The canonical owner fingerprint includes every accepted owner key, lifecycle state, capability, exact NPC binding, and same-turn ref mapping. Owners with no current resource entries still participate so a late client-owned ID or capability mutation invalidates publication.
