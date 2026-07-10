# Contract: Local Interaction Scope

**Source issue**: [#1493](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1493)

## Player Command Contract

1. Player enters `/обучение` or `/торговля` without an ID.
2. Client resolves the current realm and canonical local interaction scope.
3. Client filters capable entities through that scope.
4. Client displays a named selector when one or more targets exist.
5. The selected option may submit an internal ID hidden from the player.
6. Before request creation or purchase, the service resolves scope again and rejects a source that is no longer local.

## Fail-Closed Contract

- Missing Mortal current location: no teachers/merchants and no pending request.
- Missing or inconsistent Chaos navigation: no mentors/Guardian trade and no pending request.
- Unresolved realm: no target list.
- Remote direct action: localized rejection, zero state mutation.

## GM Authoring Contract

- A Mortal teacher must carry canonical `currentLocationId`/`currentLocation` consistent with the player's location to be actionable.
- A Chaos Sea Guardian mentor must be the current active Guardian in the current abode; non-Guardian mentors require explicit abode location evidence.
- A Shining mentor must have Shining Abode realm authority.
- Showcase data does not override location. A fresh showcase from a remote actor remains unavailable.
