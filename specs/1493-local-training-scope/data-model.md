# Data Model: Local Training And Trade Scope

**Source issue**: [#1493](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1493)

## Resolved Local Interaction Scope

- `realm`: normalized Mortal World, Chaos Sea, or Shining Abode.
- `isResolved`: false when required authority is absent or contradictory.
- Mortal fields: `locationId`, `locationName`.
- Chaos Sea fields: `activeGuardianId`, `currentAbodeId`, `currentAbodeName`.
- Shining fields: realm-wide active scope only.
- `unavailableReason`: localized player-facing explanation when resolution fails.

This is a runtime view derived from existing canonical files; it is not a new persisted state file.

## Local Target Rules

### Mortal NPC

- Must have a usable stable identity.
- NPC `currentLocationId` or `currentLocation` must match current location ID/name using case-insensitive exact alias comparison.
- Teacher and merchant capability are evaluated only after locality succeeds.

### Chaos Sea Mentor

- Profile realm, when present, must be Chaos Sea.
- Guardian profile is local when `actorId` equals the active/current Guardian and its canonical abode equals `currentAbodeId`.
- Non-Guardian profile requires explicit location ID/name matching the current abode.

### Shining Abode Mentor

- Profile realm must resolve to Shining Abode.
- No finer location filter is applied until a canonical Shining sublocation authority exists.

## Invariants

- Unknown scope never means global access.
- Listing and purchase use the same locality predicate.
- Locality is re-evaluated at purchase time.
- Pending request creation is downstream of locality.
- No resource mutation or receipt is allowed when locality fails.
