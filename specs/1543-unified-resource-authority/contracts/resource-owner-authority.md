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
| `vehicle` | One permanent accepted vehicle ID |
| `item` | One permanent item ID from the accepted item identity catalog |
| `afterlife_actor` | One permanent accepted afterlife profile/actor ID |
| `afterlife_conflict_side` | One client-owned identity scoped to an active conflict |
| `afterlife_scope` | One client-owned return-cycle/Abode scope identity |

All identity comparison is exact ordinal plus confusable detection. A display name is never a fallback.

## 3. Same-turn owners

The raw owner uses exactly one temporary ref declared by its owning contract:

- `combatantRef` for a new individual combatant;
- `memberRef` for a new group member;
- the existing actor/item/profile temporary reference field for a client-allocated identity;
- for a first Actor-Materialized afterlife profile, the immutable `materializationId`, while the already sealed canonical `actorId` remains the permanent profile/effect identity;
- for the reserved first `player_soul` without a generic Actor Materialization envelope, one client-derived ref fingerprinted from exact `player_soul` plus canonical realm;
- a client-created scope/ref supplied by the owning local transition where GM authorship is not allowed.

The common owner plan allocates every client-owned permanent ID once, rewrites all accepted owner after-images, and exports a ref-to-key map. It does not allocate or rename an afterlife `actorId` already sealed by Actor Materialization; it binds that exact ID through `materializationId`, creates client-owned realm/resource bindings, and consumes only the resource materialization envelope. The reserved first `player_soul` is the sole no-generic-envelope exception: the exact permanent actor identity is already closed, the client derives its same-turn ref, and an otherwise-valid closed `resourceMaterialization` is consumed normally. A first non-player afterlife actor without Actor Materialization remains invalid. Canonical state must contain no temporary refs after publication.

Client-generated afterlife IDs and refs are deterministic fingerprint-derived identities, never GUIDs. Actor realm refs bind exact `actorId` + canonical realm; conflict-side owner IDs and refs bind canonical realm + exact `conflictId`; Shining scope owner IDs and refs bind `shining_abode` + exact `returnCycleId`. Separate fingerprint domains prevent cross-purpose collisions. Turn, capacity, traversal order, and process randomness are not seeds, so repeated composition of the same accepted authority is byte-identical while a changed stable identity seed changes the derived identity.

A raw resource target may use the exact accepted ref but cannot submit the resulting permanent ID. Case/confusable/duplicate refs and cross-kind reuse fail the whole plan.

## 4. Named NPC in combat

A combat row with `NPCId` must resolve to exactly one composed named NPC. It references the NPC resource owner and must not initialize or carry a combat-local copy of the NPC's health/poise/resources.

An anonymous combatant must not claim an NPC binding. Two combat rows cannot claim the same named NPC where the owning combat contract requires one representation.

## 5. Combat groups

A group contains stable member records. New members carry `memberRef` and omit `memberId`; existing members carry `memberId` and omit `memberRef`. `healthStates[]`, list position, `count`, and unit display name are not resource identity.

Reordering members preserves coordinates. Removing a member requires exact terminal owner evidence and retires only that member's resources. Group deletion retires every still-live member atomically.

The group array is only a carrier. A detached member may appear as a top-level non-group combat row with the same exact `memberId`/`memberRef`; it remains the same `combat_group_member` resource owner and keeps its effects/history. The row must not also carry `NPCId`, `npcRef`, `combatantId`, or `combatantRef`. Moving the exact identity between group and top-level carriers is continuity, not retirement plus creation. For player-facing effect selection, both individual combatants and detached members use target kind `combatant`; duplicate/confusable cross-family target IDs fail closed.

## 6. Vehicles

Vehicles use permanent `vehicleId` resource ownership. Activation, parking, movement, and item-carrier participation do not change the coordinate. `UpdateVehicles` cannot write current/maximum resource values. New vehicle materialization supplies capacity authority; destruction requires terminal history before the vehicle resource is removed.

## 7. Items

Item resources use permanent item identity regardless of inventory, equipment, NPC inventory, location storage, offscreen storage, or another valid carrier. Movement changes no resource coordinate. Split/merge/stack operations must explicitly define which permanent item identities survive before any resource plan is accepted.

Every active item may export the closed `local_item_cost` and `local_item_outcome` source routes. The export is keyed and fingerprinted by the exact permanent item owner; source resolution additionally requires the target resource coordinate to have that same realm, owner kind, and item ID. Carrier location and display identity never grant source authority.

Item destruction or terminal consumption retires all item resources only after immutable terminal evidence is prepared. A raw removal cannot silently discard nonempty resource state.

## 8. Afterlife owners and scopes

`player_soul` is one permanent cross-realm owner. Its profile carries exact realm bindings plus a sealed, duplicate/confusable-free `realmIndependentResourceCapabilities` subset of the owner's exported capabilities. The common authority resolves activity per capability: an ordinary capability is active only while its exact realm binding is active, while a sealed realm-independent capability may remain active when that binding is suspended. Unknown, unexported, ambiguous, or unsealed capability exceptions fail closed and the policy participates in the owner fingerprint.

Version 1 seals `blessing_rerolls` as realm-independent for `player_soul`; `spiritual_action_points` remains realm-bound. The same capability-activity resolver is mandatory for owner resolution, lifecycle transition generation, and canonical state agreement. No caller may special-case a resource key outside the sealed owner export.

Active spiritual-conflict sides use conflict-scoped IDs. Per-return Guardian/Shining reserves use accepted actor/scope IDs and registered return-cycle capacity authority.

A complete first non-player afterlife profile may initialize pre-sealed setting-defined `afterlife_actor` resources through one closed `resourceMaterialization.resources[]` envelope. The shared owner materialization planner validates exact/confusable-unique resource keys, sealed owner-kind/capacity authority, and exact maximums, then publishes bindings plus the complete resource quartet atomically. The reserved first `player_soul` may use the same closed resource envelope without a generic Actor Materialization envelope; the client consumes it through the deterministic identity-derived ref and publishes its binding. Existing materialized profiles may not resend the envelope; `actorRef`, a second generated actor ID, direct current values, and prose-derived capabilities are forbidden.

Only Fresh New Game may create an absent `resource_owner_authority.json`. Its preset-Guardian and freeform-Guardian builders feed the same deterministic bootstrap plan, which produces definitions/state/history/authority plus all player, Guardian, profile, Soul, and Shining owner after-images. Those resource and owner roots publish as one coordinated resource write set, and the applied result must immediately pass exact existing-session authority validation. Mortal incarnation and every other existing-session route require the prior exact quartet; none is a migration or self-heal path.

Every client-owned Shining transition plan declares exactly one transition kind:

- `SynchronizeCurrentShining`: fresh canonical Soul is Shining Abode and remains Shining Abode;
- `OrdinaryReentryFromChaosSea`: fresh canonical Soul is Chaos Sea and the accepted after-image is Shining Abode;
- `AscensionFromChaosSea`: the same exact realm edge plus fresh maximum-enlightenment authority and an absent `life_transitions.json` command.

The transition service acquires one canonical write lease before reading Soul, profile, Shining, Guardian, conflict, resource state/history, return guard, and lifecycle-control roots. It retains that lease through owner composition, resource planning, exact before-image checks, coordinated publication, and rollback. A caller's earlier cached realm or ascension check is never transition authority.

Conflict close or return-cycle replacement retires scoped resources. Persistent actor resources survive unrelated conflict closure. Currencies, progression, relationships, and faction accounting are not owner exports for this ledger.

## 9. Capability and lifecycle

An owner export contains an exact set of resource keys/capabilities and its sealed realm-independent subset, both derived from accepted definition/materialization state. A definition's `allowedOwnerKinds` is necessary but insufficient: the owner must also permit that resource and the shared capability-activity resolver must consider it active for the accepted transition.

Owner states:

```text
active -> suspended -> active
active/suspended -> terminal
```

An inactive, removed, wrong-realm, historical, ambiguous, or unaccepted owner cannot initialize or mutate a coordinate. Terminal owners cannot be revived by a resource command.

## 10. Fingerprint

The canonical owner fingerprint includes every accepted owner key, lifecycle state, capability, exact NPC binding, and same-turn ref mapping. Owners with no current resource entries still participate so a late client-owned ID or capability mutation invalidates publication.
