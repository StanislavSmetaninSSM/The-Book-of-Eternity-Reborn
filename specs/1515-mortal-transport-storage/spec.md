# Complete Mortal transport and storage materialization

Issue: [#1515](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1515)
Branch: `1515-mortal-transport-storage`; base `f3d0bc668481d1b648381a83dfc39d425fec8bde`.
Status: WIP design, implementation and verification pending.

## Scope and clarification

The user delegated the next Mortal materialization task and routine implementation decisions. #1511 items and #1513 locations are closed and supply the existing identity/carrier/location foundations. #1514 is an afterlife roadmap predecessor, not a runtime prerequisite. Reuse the accepted resource owner, item and location machinery; do not create competing authorities. The game's current pre-release policy explicitly excludes legacy save promotion. Update active bootstrap/templates/fixtures instead. Spiritual combat, afterlife travel, new vehicle simulation and UI redesign are excluded.

## Stories and requirements

### US1 (P1): Complete vehicle creation and active selection

A GM creates a usable vehicle with one transient vehicleRef, complete descriptive/type/purpose fields, explicit owner/controller/access, cargo capacity, condition and movement capabilities, current location and provenance. The existing resource owner composer assigns its permanent vehicleId and materializes health; cargo capacity is separate from health. No arbitrary permanent IDs or prose-derived mechanics.

- FR1 Validate complete semantics before creation, including exact references, finite nonnegative capacity, explicit condition and applicable movement, supported Mortal location and coherent availability. Validate the new shape at birth and preserve interoperability with existing partial-update semantics; do not add a new ordinary-lifecycle policy. No old-shape birth fallback.
- FR2 Consume activeVehicleChange into one canonical activeVehicleId, resolve same-turn vehicleRef exactly, reject unknown/removed/ambiguous/inaccessible choices and maintain agreement with availability. Removal clears or explicitly replaces selection; do not leave a dangling active ID.
- FR3 Seal versioned creation evidence bound to accepted source turn and client identity in the existing atomic accepted-turn package. Rejected/rolled-back turns publish neither entity nor receipt/health/items/active selection. Replaying birth does not allocate duplicate identity or resources.

### US2 (P1): Complete storage creation and effective access

A GM creates storage within its governed location, with stable exact identity, purpose, owner/access, capacity, condition and provenance. Storage metadata remains the location authority; item contents remain the existing companion authority. Public/shared/restricted cases are explicit, not guessed from prose.

- FR4 Reuse existing storage metadata contract, require complete creation semantics and accepted location ownership. Reconcile storage_access.json with effective storage metadata/projection; represent accepted birth access as effective state rather than pending commands. Existing grant/share/revoke lifecycle remains authoritative and is changed only if necessary for the new schema. Unknown/confusable targets and contradictory ownership/access fail with bounded indexed repair.
- FR5 Preserve exact item identity/ownership/location and reject dangling/duplicate or incompatible contents. Validate capacity against actual governed contents, including same-turn item birth and offscreen storage. No parallel inventory writer or inferred content.
- FR6 Persist versioned source-turn evidence and effective access with the entity in the existing atomic transaction, backup/freshness/readback/rollback surfaces. No detached access write that can survive a rejected entity creation.

### US3 (P1): Existing lifecycle interoperability

- FR7 Existing deposit/retrieve must interoperate with the newly accepted schema under its existing canonical lease. Preserve current item/resource identities, receipts and lifecycle checks. Historical birth capacity evidence must not reject later legitimate consumption or movement. Do not redesign ordinary movement/access or cache permission.
- FR8 CLI/browser display canonical effective vehicle/storage state, not pending access commands or legacy aliases. No redesigned screens or new travel mechanics.
- FR9 GM rules, daemon and bounded repair guidance, worked vehicle and shared/restricted storage examples, source guards and category metadata match the supported contract. No afterlife prompt change unless an actually shared contract requires isolation guidance.

## Success boundary

All FR1–FR9 require concrete implementation and impact-selected evidence. Cheap pure policy tests precede real accepted-turn creation, moves and rollback checks. Source-only or PlanOnly results are not PASS. No whole-suite or hosted CI. No main merge or issue closure is authorized in this task. Optional capabilities and historical fixtures do not indefinitely expand this boundary.

## Concrete contract decisions (existing vocabulary)

Purpose and descriptive condition use the existing visual/functional `description`; no new purpose or condition enum. Vehicle mechanical condition remains the health resource and availability, not duplicate HP. Vehicle cargo reuses `capacity` (nonnegative integer distinct item stacks) and `volume` (nonnegative finite dm³), like storage. Quantity is not stack count; no new weight limit is introduced. Vehicle ownership/access reuses storage's closed owner/authorizedUsers/hasFullAccess vocabulary. Exact controller identity must resolve through existing accepted actor authority, not an invented actor registry.

Storage retains its existing complete metadata and location materialization receipt plus exact storageId. It does not receive a second location identity/receipt. Its contents remain the existing current/offscreen companion authority; travel parking/hydration does not change item identities or receipt history.

Active selection reduction uses client-owned `vehicles.json.activeVehicleId` (exact permanent ID or null). `activeVehicleChange` selects an existing permanent ID or an exact same-turn vehicleRef; the composer resolves the latter through its already allocated owner export. The selected final vehicle must have availability Active; Parked/Pocket cannot be silently activated. A null selection requires no Active final vehicle. Parking/replacement must be explicit coherent updates, not an inferred currentLocationId. Commands are consumed atomically. Client-owned activeVehicleId cannot be directly changed by GM input.

Ownership decision: the existing strict single-player authority is `player_current` (resource/effect/wound owners). Player and Shared primary ownership must use that exact identifier; Faction references resolve through existing faction authority. No old GUID is implicitly converted and no multiplayer registry is invented. Unknown recipient players fail closed with bounded guidance. Shared is not public. Issue #1515 accepts a shared OR restricted storage example. A restricted Faction storage meets that criterion without inventing multiplayer identity. Successful sharing to an unknown distinct player is not claimed. Ordinary moves/access changes after valid first materialization remain out of scope except compatibility with the new accepted schema.

## First-birth cargo measurement

Count direct cargo stacks, not units. Sum their whole-stack external volumes; a loaded container contributes its measured outer footprint once, not its internal capacity plus descendants. The complete containment graph still passes existing item authority. Equality with capacity passes. There is no added weight rule.

Carrier birth evidence contains `cargoMeasurements` rows with exactly one `itemId` or same-turn `creationRef`, exact `count`, finite nonnegative `externalVolumeDm3` for the whole stack, and a submitted `measuredConfiguration` basis. Explicit measurement is required for containers and multi-unit stacks. A single noncontainer uses its existing physical `volume`; redundant explicit measurement is rejected. Missing, extra, duplicate, wrong-carrier and unresolved rows fail.

The client binds each admitted row to the resolved identity, exact count, existing item birth identity where applicable, versioned semantic shape/contained-configuration fingerprint and carrier/source-turn capture. The GM supplies neither hash nor authority. Quantity/configuration/containment edits during birth invalidate the measurement; bounded repair must explicitly resubmit the affected row against the new packet. Existing source freshness invalidates stale prepared publication. Reference allocation alone is not a shape edit.

After accepted birth this evidence is immutable history, not current capacity authority. Do not compare it with later live contents or reuse it for ordinary movement, consumption or a different carrier's birth. No measurement field is added to the item itself and no second inventory writer is introduced.

### Recoverable measurement basis v1

For explicit measurements, `measuredConfiguration` is a closed object with
`schemaVersion: 1`, `item` (semantic object) and `contents` (descendant nodes).
Each descendant has exactly one itemId/creationRef, count, item and contents.
The client compares this assertion with the actual governed projected catalog;
it never creates or repairs actual inventory from this assertion. Sibling sets
compare by resolved exact identity; semantic arrays inside item properties retain
their order. Parent/child count and containment must agree with the real catalog.

Semantic projection clones all admitted item fields, removing only top-level
itemId, id, initialId, existedId, creationRef, materializationReceipt, materialization,
resourceMaterialization, count, quantity, contentsPath, isCarried, currentLocationId
and currentLocationName. No nested custom fields are removed. This conservative
v1 also requires reconfirmation after presentation/price/journal/bond changes at
birth; it does not guess which such edits can never affect configuration.

The supplied basis survives a fresh process and is not silently refreshed. Any
count/shape/membership change requires replacement of the affected asserted basis
and explicit volume, even when the reconfirmed numeric volume remains equal.
Client-owned fingerprints are calculated only after exact basis equality.
Allocation and receipt/resource normalization alone do not change the basis.
After birth the evidence remains immutable historical DATA, never current capacity
or access authority. This representation implements the approved historical
quantity/shape binding without inventing container exterior from interior volume.

Measurement rows select direct cargo roots only; descendants appear exclusively
in the configuration graph. Basis clones use the existing governed reference
rewriter with the same frozen item allocation map before semantic comparison.
Validate the actual nonempty container graph before selecting roots; zero roots
is not proof of an empty load. Arithmetic compares exact decimal values of
invariant round-trip finite-double representations (not original JSON tokens).
It normalizes signed zero, preserves the finite-double numeric domain and avoids
epsilon-based over-capacity acceptance, including fractional equality.

## Vehicle birth actor references

Faction owners resolve through the existing exact Mortal faction authority,
including independently admitted same-turn creation, and retain the exact name
after the existing accepted factionCoreChanges projection (including profile rename).
NPC controllers must belong to successful current Mortal resource owner composition;
that existing composer rejects duplicate/divergent identities and permits legitimate
identical cross-section mirrors. Validate the selected NPC's existing complete Mortal
materialization evidence, not unrelated actors. Existing identity bytes are retained
for same-turn NPC/faction initialId after normalization. No registry or inferred actor
is introduced. Birth reference observations remain fenced through common publication.
This is separate from location resolution and effective access.


### Exact location at vehicle birth
A nonnull birth currentLocationId must resolve to one exact Mortal map location
or the existing accepted same-turn location initialId mapping. Unlike NPC/Faction
stable IDs, location initialIds become newly allocated permanent IDs. Resolve only
the detached final vehicle companion; never mutate the raw prepared birth. Empty
cargo is not an exemption from fresh location source fences. Existing post-birth
vehicle updates and travel remain outside this binding change.


### Explicit restricted access
The current single-player authority is player_current. Unknown Player/Shared
storage owners or authorizedUsers are unsupported and fail closed. A known owner
does not override explicit hasFullAccess=false: a closed owned object remains
unavailable to the existing storage move consumer. Do not infer access from faction
membership or silently widen permission while materializing the entity.

### Planned full vehicle creation evidence v1
The existing `vehicles.json` companion owns append-only client-authored
`birthReceipts`. Each receipt has exactly `schemaVersion: 1`, `vehicleId`,
`vehicleRef`, `sourceTurn`, `sourceAuthority` (sessionId, requestId, snapshotToken),
`birthState`, `resourceInitialization` and `seal`. The source fields come from the
validated pending-turn manifest, never GM declarations. `birthState` is the
complete detached resolved vehicle after item/actor/location/cargo materialization;
resourceInitialization reuses the accepted health capacity draft. The consistency
seal uses the project's canonical JSON fingerprint conventions, not a new signing
key or publication authority. Signed-original continuity remains authoritative.

Receipts survive ordinary updates, cargo movement/consumption and vehicle removal.
Never recompare historical birthState to mutable current cargo or re-resolve its
historical actors/locations. Current input must preserve the exact original receipt
collection; only the existing accepted-turn vehicle companion can append births.
Raw receipt injection/deletion, duplicate or reused historical identity and missing
required evidence fail closed. Canonical receipt reads must not enumerate historical
cargo as active items. Current bootstrap/fixtures use the declared schema; no
production migration or fabricated old receipts. Full-entity validation is separate
from cheap partial resource-owner composition. Exact canonical boundary and
historical-resource cross-checks are being verified before runtime wiring.

### Bounded existing-location empty storage admission

`worldMapUpdates.storagesToAdd[]` contains closed `{targetLocationId, storage}`
operations. The target is an exact active pre-turn permanent Mortal location,
not a same-turn initialId. `storage` declares all existing metadata fields and
explicit `contents: []`; this checkpoint rejects nonempty contents, transfers
and measurement fields rather than silently discarding them. Zero capacity and
volume are valid for an empty declaration. Ordinary access remains explicit;
ownership does not override false. Faction owners reuse existing exact authority.

The metadata is inserted through the existing detached location plan. The
original location envelope/receipt is immutable; an existing-index child
`storage_addition` transition records source turn, exact operationRef, child ID,
absent before-state and detached metadata after-state. A coordinate cannot reuse
an active, historical or same-turn confusable storage ID, or combine birth with
update/removal. Equal storage IDs in different locations remain independent.
Empty offscreen births create no contents carrier. Existing travel reconciliation
and effective-access projection consume the resulting accepted map.

## Storage cargo birth evidence (implementation decision, FR5–FR7)

For the supported current-scene storage birth, finalize detached version-1 cargo
capacity evidence in the existing location identity index's `storage_addition`
transition `afterState`. The parent location receipt remains unchanged. Allocate
one exact child transition during preparation, including for empty new storages;
finalization uses admitted projected item DATA and allocates nothing. The expected
birth coordinates and transition IDs belong to the prepared plan. The existing
writer and readback must require every expected draft to be finalized before the
accepted transaction succeeds. Mutable current contents are not birth evidence.

The capacity core uses real `location_storage` location/storage coordinates and the
same first-birth stack/volume semantics and measurement basis as vehicles; vehicle
persisted format remains unchanged. Evidence binds exact child/location/source turn,
validated session/request/snapshot and detached historical measurement/configuration
rows. No new inventory writer, receipt file, cache or independent authority exists.

Every present historical evidence object is strictly validated. Untouched canonical
storage paths are not retroactively required to acquire new records. Current-turn
raw index mutation/deletion is refused by existing signed-original continuity;
standalone cold detection of a wholly removed record without the trusted expected
birth context is not claimed. No new coverage marker or legacy-save promotion is
introduced. Later accepted movement/consumption must preserve historical evidence
without rechecking birth capacity against changed live contents. Actual populated
offscreen birth remains a separate required follow-on in this same task.

## Canonical vehicle display (FR8 implementation decision)

Both the real CLI transport screen and shared browser command result consume only
the canonical `vehicles` array of an object root. Pending UpdateVehicles and its
aliases, legacy root arrays, birth receipts, id/status/isActive aliases do not
become accepted cards, actions, selection or availability. Selection is an exact
ordinal join of canonical activeVehicleId and vehicleId. Unknown availability is
shown as unknown rather than inferred. Retain existing layouts, resource display
and movement commands. Do not add semantic validation or permission caching to UI.
Each consumer reuses its existing native parsed DOM; trivial scalar presentation
can share policy without converting/reparsing whole roots.

## Populated offscreen storage birth (next bounded FR5–FR7 block)

This approved design extends the accepted empty `storagesToAdd` checkpoint; it is
not an implementation or verification claim. The required new case creates a
fresh storage directly at an exact active pre-turn Mortal location B while A
remains current. Its fresh items are born at B in that same accepted turn.
Creating a current-scene storage and subsequently parking it is not a substitute.
The accepted current-scene creation path and existing empty additions remain
supported without a retroactive evidence migration.

### Raw authority and prepared coordinates

Reuse the closed `worldMapUpdates.storagesToAdd[i]` operation
`{targetLocationId, storage}`. The target remains an exact permanent pre-turn
location identity, never a same-turn initialId or an inferred/confusable target.
The storage retains all existing complete metadata fields and explicit `contents`;
nonempty contents contain fresh, complete item declarations. Their existing route
is `storage_placement`, with source authority kind `location_storage` and exact
client-derived authorityId `locationId:storageId`. Optional `cargoMeasurements`
uses the already specified whole-stack/configuration format. No new item route,
transfer permission or GM-authored permanent identity is introduced.

Reject canonical-item injection without an independently authorized transfer,
dangling/duplicate contents, reused active/historical/confusable child identities,
conflicting birth/update/removal, wrong source coordinates and authored history.
This block does not add a transfer-to-new-storage operation. Empty existing
additions retain their accepted empty-carrier behavior.

The first metadata plan retains detached raw storage DATA, derives its explicit
item source coordinate, and allocates the existing child transition once. Each
populated addition has an exact expected draft containing locationId, storageId,
transitionId, operationRef `worldMapUpdates.storagesToAdd[i]`, source turn and raw
storage path `worldMapUpdates.storagesToAdd[i].storage`. Authorization comes only
from these validated commands, not all final-map storages, catalog membership or
the absence of a location initialId. Conflicting alias/destination mappings fail
explicitly; dictionary insertion must not silently choose one.

Project raw cargo into the held plan's existing item-owned carrier after ordinary
source parking and existing-destination hydration. For the required noncurrent B
case this is the exact `(location_storage, B, storageId)` entry in
`location_storage_contents.json`; do not place items in world-map metadata. The
original location envelope/receipt stays unchanged. Preserve current storage,
existing offscreen entries and unrelated location metadata.

### Acyclic DATA reuse and fresh publication authority

Keep the dependency order: validated location commands → stable coordinates/raw
carrier projection → item catalog and route admission → frozen item allocations
and projected item phase → storage evidence completion → existing writers.
Location preparation must not depend on completed item allocation or evidence.
The existing item loader and projector already consume the held
`FinalStorageContents`; extend that seam rather than adding an inventory writer.

Route admission may borrow the already built raw item catalog from the same
`LoadMortalItemCatalogAsync` invocation and canonical lease. It is operation-local,
read-only DATA over the same effective current/offscreen roots and prepared vehicle
IDs, not a retained cache or publication permission. Freeze/detach admitted roots,
routes and allocations through the existing snapshot owner. No borrowed mutable
alias may escape or survive a repair, retry, source mutation or generation change.
Preserve request/manifest, signed-baseline and NPC/quest/craft/trade authority
reads. Eliminate only player/current/offscreen/vehicle reads and catalog rebuilding
whose sole purpose was reconstructing that same occurrence catalog.

Keep occurrence FilePath/JsonPath values on their effective canonical carriers;
they participate in identity/allocation fingerprints. Separately remap repair
errors through detached draft provenance to the exact raw command/storage/item
field. Never redirect authority fingerprints or instruct the GM to repair a
client-owned projected companion or history record.

Use the existing fresh observed-input capture and common before-image fences for
raw world-map commands, current/offscreen item roots, location/item indices,
storage access, scaffold and relevant NPC/faction dependencies. Effective-root
equality alone is insufficient. A route-based trigger can request fresh checks,
but only the fresh matching held plan identifies expected births. Retain actual
owner, lease, generation, source and opening/closing publication checks.

### Atomic evidence and acceptance

Finalize the existing typed storage-birth completion over the admitted item phase
and one shared projected graph. Reuse the real-coordinate capacity core, exact
arithmetic and historical configuration validation. Compare immutable metadata
with the prepared accepted map, and contents with the exact projected carrier.
Allocate nothing during finalization and never mutate the cached metadata plan.
Persist the existing version-1 `cargoBirthEvidence` only in the exact
`storage_addition.afterState`; bind child/location, accepted source turn,
session/request/snapshot and detached measurements. The parent receipt is unchanged.
Every expected populated draft must complete before the first governed write, and
closing readback must match the completed index. Existing metadata/item writers,
common transaction, backups and rollback own all effects together: storage, item
identities/receipts, capacity history and effective access. No archive, new receipt
file, authority service, coverage marker or cache is added.

Minimal evidence for this block is:

- Cheap independent typed controls for direct offscreen preparation, exact draft
  and route coordinates, conflicting mappings, detached completion, wrong-carrier
  refusal and raw diagnostic provenance. Retain current-scene and existing empty
  addition/parking/hydration controls rather than broadening their suites.
- A real noncurrent B birth with fresh same-turn items, permanent identities and
  exact source-bound child history/access. Include a measured nested/stack load;
  equality with the declared limits succeeds and violated limits fail with indexed
  diagnostics and unchanged tracked bytes/absence. Repeated admission must not
  allocate duplicate identities or history.
- Invalid item/source/coordinate and post-admission source-drift refusal, including
  proof that raw map and companion dependencies remain fenced.
- A reached existing offscreen-contents publication cut proving restoration of
  all tracked bytes/absence, history/access and generation, not merely an earlier
  validation exception.
- A subsequent accepted travel to B followed by the existing retrieve/deposit
  consumer, preserving item/resource identity and immutable birth evidence after
  live contents change. Explicit restricted access must remain restricted.

Synchronize the GM rules, actual daemon, worked noncurrent birth example and narrow
source/repair guards with the supported raw contract. Use fresh matching builds
and impact-selected official categories after a saved checkpoint, followed by
independent whole-block review. No full suite, hosted CI, merge or issue closure.
Optimization evidence must distinguish eliminated duplicate DATA reconstruction
from mandatory fresh source/closing checks; no speedup is claimed without measurement.
