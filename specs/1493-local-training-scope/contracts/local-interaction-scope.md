# Contract: Local Interaction Scope

**Source issue**: [#1493](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1493)

## Player Command Contract

1. Player enters `/обучение` or `/торговля` without an ID.
2. Client resolves the current realm and canonical local interaction scope.
3. Client filters capable entities through that scope.
4. Client displays a named selector when one or more targets exist.
5. The selected option may submit an internal ID hidden from the player.
6. Before request creation or purchase, the service resolves scope again and rejects a source that is no longer local.
7. After price, inventory, showcase, and pending-state reads, mutation paths recheck locality immediately before commit; a changed realm/location/hall produces zero mutation.
8. Before returning local targets or details, read paths reconcile the source against fresh scope and actor state; a moved source is omitted and client-owned refresh data is not written.
9. Pending-request cleanup performs a serialized latest-state merge and removes only unchanged fulfilled request snapshots; concurrent client additions or updates are preserved.

## Fail-Closed Contract

- Missing Mortal current location: no teachers/merchants and no pending request.
- Contradictory Mortal location ID/name aliases: no teachers/merchants even if one alias happens to match.
- Missing or inconsistent Chaos navigation: no mentors/Guardian trade and no pending request.
- Non-canonical generic `afterlife`: unresolved realm because neither Chaos Sea nor Shining Abode authority was selected.
- Missing or invalid Shining `currentHallId`: no hall-local mentors/faction trade and no player-triggered pending request.
- Conflicting Shining evidence: an explicit non-local hall rejects the actor even when an indirect faction/resident association is local.
- Unresolved realm: no target list.
- Remote direct action: localized rejection, zero state mutation.
- Stale direct NPC/Guardian trade action from the wrong actual realm: localized rejection, zero inventory, wallet, receipt, or pending mutation.

## GM Authoring Contract

- A Mortal teacher must carry canonical `currentLocationId`/`currentLocation` consistent with the player's location to be actionable. When both aliases are supplied they must describe the same location; contradictory location aliases fail closed.
- A Chaos Sea Guardian mentor must be the current active Guardian in the current abode; non-Guardian mentors require explicit abode location evidence.
- A Shining mentor must have Shining Abode realm authority and resolve to the current hall directly or through canonical faction/resident/leadership/political association.
- A Shining faction is locally tradable only when it is player-visible and its `hallId` equals `currentHallId`.
- Showcase data does not override location. A fresh showcase from a remote actor remains unavailable.

## Existing Background Trade Refresh

The client-owned Shining return-cycle trade auto-refresh is world-lifecycle work, not a player selection or local interaction request. It remains realm-wide so stored faction inventories can advance between visits. Local `/торговля` discovery, explicit inventory requests, details, and purchases still fail closed against `currentHallId`; auto-refresh never makes a remote faction actionable.
