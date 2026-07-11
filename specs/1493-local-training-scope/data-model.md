# Data Model: Local Training And Trade Scope

**Source issue**: [#1493](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1493)

## Resolved Local Interaction Scope

- `realm`: normalized Mortal World, Chaos Sea, or Shining Abode.
- `isResolved`: false when required authority is absent or contradictory.
- Mortal fields: `locationId`, `locationName`.
- Chaos Sea fields: `activeGuardianId`, `currentAbodeId`, `currentAbodeName`.
- Shining fields: `currentHallId`, `currentHallName`, local faction ids, and actor ids derived from hall/faction/resident/leadership/political links.
- `unavailableReason`: localized player-facing explanation when resolution fails.

This is a runtime view derived from existing canonical files; it is not a new persisted state file.

## Local Target Rules

### Mortal NPC

- Must have a usable stable identity.
- NPC `currentLocationId` or `currentLocation` must match current location ID/name using case-insensitive exact alias comparison. If actor and authority both provide an ID and a name, their alias sets must agree completely; a matching ID paired with a contradictory name (or the reverse) is unresolved.
- Teacher and merchant capability are evaluated only after locality succeeds.

### Chaos Sea Mentor

- Profile realm, when present, must be Chaos Sea.
- Guardian profile is local when `actorId` equals the active/current Guardian and its canonical abode equals `currentAbodeId`.
- Non-Guardian profile requires explicit location ID/name matching the current abode.

### Shining Abode Mentor

- Profile realm must resolve to Shining Abode.
- `currentHallId` must resolve to `halls[].hallId`.
- Direct profile `hallId`/location aliases may prove locality.
- A resident or political actor may inherit locality through its `shiningFactionId`/`originFactionId` and the faction's `hallId`.
- A faction head may inherit locality through `leadership.headActorId`.
- A faction profile is local only when its faction `hallId` equals the current hall.

## Invariants

- Unknown scope never means global access.
- Contradictory location aliases fail closed; one matching field cannot excuse another supplied field that points elsewhere.
- Listing and purchase use the same locality predicate.
- Direct trade service entry points re-resolve the actual `currentRealm` before inventory or wallet mutation.
- Locality is re-evaluated at purchase time.
- Mutation paths recheck locality immediately before commit after other state reads and validation.
- Pending request creation is downstream of locality.
- No resource mutation or receipt is allowed when locality fails.
