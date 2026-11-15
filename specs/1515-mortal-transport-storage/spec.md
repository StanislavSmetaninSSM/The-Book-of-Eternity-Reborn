# Complete Mortal transport and storage materialization

Issue: [#1515](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1515)
Branch: `1515-mortal-transport-storage`; base `f3d0bc668481d1b648381a83dfc39d425fec8bde`.
Status: WIP design, implementation and verification pending.

## Scope and clarification

The user delegated the next Mortal materialization task and routine implementation decisions. #1511 items and #1513 locations are closed and supply the existing identity/carrier/location foundations. #1514 is an afterlife roadmap predecessor, not a runtime prerequisite. Reuse the accepted resource owner, item and location machinery; do not create competing authorities. The game's current pre-release policy explicitly excludes legacy save promotion. Update active bootstrap/templates/fixtures instead. Spiritual combat, afterlife travel, new vehicle simulation and UI redesign are excluded.

## Stories and requirements

### US1 (P1): Complete vehicle creation and active selection

A GM creates a usable vehicle with one transient vehicleRef, complete descriptive/type/purpose fields, explicit owner/controller/access, cargo capacity, condition and movement capabilities, current location and provenance. The existing resource owner composer assigns its permanent vehicleId and materializes health; cargo capacity is separate from health. No arbitrary permanent IDs or prose-derived mechanics.

- FR1 Validate complete semantics before creation, including exact references, finite nonnegative capacity, explicit condition and applicable movement, supported Mortal location and coherent availability. Existing partial updates must produce a complete valid final object; no old-shape fallback.
- FR2 Consume activeVehicleChange into one canonical activeVehicleId, resolve same-turn vehicleRef exactly, reject unknown/removed/ambiguous/inaccessible choices and maintain agreement with availability. Removal clears or explicitly replaces selection; do not leave a dangling active ID.
- FR3 Seal versioned creation evidence bound to accepted source turn and client identity in the existing atomic accepted-turn package. Rejected/rolled-back turns publish neither entity nor receipt/health/items/active selection. Replaying birth does not allocate duplicate identity or resources.

### US2 (P1): Complete storage creation and effective access

A GM creates storage within its governed location, with stable exact identity, purpose, owner/access, capacity, condition and provenance. Storage metadata remains the location authority; item contents remain the existing companion authority. Public/shared/restricted cases are explicit, not guessed from prose.

- FR4 Reuse existing storage metadata contract, require complete creation semantics and accepted location ownership. Reconcile storage_access.json with effective storage metadata/projection; consume grant/share/revoke commands instead of retaining them as effective state. Unknown/confusable targets and contradictory ownership/access fail with bounded indexed repair.
- FR5 Preserve exact item identity/ownership/location and reject dangling/duplicate or incompatible contents. Validate capacity against actual governed contents, including same-turn item birth and offscreen storage. No parallel inventory writer or inferred content.
- FR6 Persist versioned source-turn evidence and effective access with the entity in the existing atomic transaction, backup/freshness/readback/rollback surfaces. No detached access write that can survive a rejected entity creation.

### US3 (P1): Existing lifecycle interoperability

- FR7 Existing deposit/retrieve must use fresh accepted location/access/capacity under its current canonical lease. A full/inaccessible/wrong-location carrier fails without moving items; valid moves preserve item/resource identities and receipts. Do not cache permission or share mutable test fixtures.
- FR8 CLI/browser display canonical effective vehicle/storage state, not pending access commands or legacy aliases. No redesigned screens or new travel mechanics.
- FR9 GM rules, daemon and bounded repair guidance, worked vehicle and shared/restricted storage examples, source guards and category metadata match the supported contract. No afterlife prompt change unless an actually shared contract requires isolation guidance.

## Success boundary

All FR1–FR9 require concrete implementation and impact-selected evidence. Cheap pure policy tests precede real accepted-turn creation, moves and rollback checks. Source-only or PlanOnly results are not PASS. No whole-suite or hosted CI. No main merge or issue closure is authorized in this task. Optional capabilities and historical fixtures do not indefinitely expand this boundary.
