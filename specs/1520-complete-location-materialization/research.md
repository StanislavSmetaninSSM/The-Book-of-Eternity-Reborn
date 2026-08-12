# Research: Complete Mortal Location Materialization

**Feature**: [spec.md](spec.md)
**Source issue**: [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)
**Date**: 2026-08-12

## Decision 1: Canonical authority

**Decision**: `game_state/world/world_map.json.locations[]` is the sole durable
Mortal location semantic authority and `world_map.json.links[]` is the sole
topology authority. `current_location.json` is a validated projection of one
canonical location plus current-scene operational state.

**Rationale**: The two existing files currently contain overlapping shapes and
command wrappers. Choosing the map as authority preserves the established file
and makes global topology natural, while projection removes bidirectional merge
ambiguity.

**Alternatives rejected**:

- A new `location_core.json`: adds a third semantic layer and broadens every reader.
- Peer map/current authorities: requires perpetual conflict resolution and makes rollback ambiguous.
- Current-location-only authority: cannot represent prepared hidden or remote places.

## Decision 2: Exact current canonical roots

**Decision**: Canonical `world_map.json` has only `schemaVersion`, `realm`,
`locations`, and `links`. Raw `worldMapUpdates`, `newLocations`,
`locationUpdates`, `newLinks`, link lifecycle commands, and distributor metadata
are transient and are consumed. Canonical `current_location.json` retains the
selected location's root-level shared fields for existing consumers, but those
fields must exactly equal the map object; it adds only allowlisted scene-local
weather, interaction, chronology, and storage-content fields.

**Rationale**: Root-level current fields minimize consumer churn without making
the file authoritative. An exact canonical map root prevents old wrapper readers
from silently becoming compatibility paths.

**Alternatives rejected**:

- Keep wrappers beside canonical arrays: lets rejected candidates leak into readers.
- Nest the selected location under a new `location` property: cleaner in isolation but unnecessarily rewrites all current-scene consumers in this checkpoint.
- Persist derived exits as GM-owned fields: duplicates topology and permits divergence.

## Decision 3: First-creation routes

**Decision**: A new selected location appears only as one complete
`currentLocationData` object. A new remote location appears only in
`worldMapUpdates.newLocations[]`. The same exact temporary reference cannot
appear in both. Current-scene creation automatically inserts the canonical map
location. Remote creation never changes current selection or visibility.

**Rationale**: Route exclusivity gives every first creation one owner and keeps
current selection distinct from semantic creation.

**Alternatives rejected**:

- Require the current creation to be duplicated in `newLocations[]`: creates two raw authorities.
- Allow partial current creation completed by map data: permits cross-carrier semantic assembly and unsafe repair.
- Infer identity by name/coordinates: ambiguous and incompatible with exact continuity.

## Decision 4: Client identity and pre-release compatibility

**Decision**: The GM writes a null permanent ID, exact `initialId`, independent
`materializationId`, route/source evidence, and complete section dispositions.
The client assigns `loc_<guid-n>` and `lnk_<guid-n>` identities, seals immutable
receipts, and records active/retired origin evidence in
`game_state/world/location_identity_index.json`. Receipt-less canonical data is
invalid. Repository fixtures are migrated; no runtime promotion exists.

**Rationale**: Client assignment makes permanent identity unforgeable and makes
history/replay checks independent of current carrier contents. The project has
not shipped, so supporting obsolete development saves would add complexity with
no product benefit.

**Alternatives rejected**:

- GM-authored permanent IDs or seals: gives the authoring model client authority.
- Deterministically reuse `initialId` as permanent ID: preserves temporary naming and weakens route separation.
- Runtime legacy promotion: reintroduces touch classifiers, historical ambiguity, and permissive readers.

## Decision 5: Complete location semantics

**Decision**: Every new location contains physical fields for identity,
presentation, physical setting, placement, discovery, two difficulty profiles,
chronicle, faction control, actor bindings, storage metadata, threats, lore,
custom states, and topology disposition. Every governed section is declared
`populated` or `empty_by_design`; an empty section has an in-world reason and its
canonical empty array/null value remains present.

**Rationale**: A location is not useful merely because it has a name and
coordinates. Explicit emptiness prevents missing data from masquerading as a
deliberate design choice and gives bounded repair exact targets.

**Alternatives rejected**:

- Require only fields used by the current scene: remote places remain skeletal.
- Treat omitted arrays as empty: loses authorship intent and makes later repair indeterminate.
- Allow prose to substitute for structured mechanics: prevents validation and downstream authority.

## Decision 6: Directed link entities

**Decision**: Links are independent materialized entities with null permanent
`linkId`, exact temporary `initialId`, envelope, explicit endpoint selectors,
direction label, type/travel mode, access, visibility/discovery, and description.
Each raw endpoint supplies exactly one of permanent location ID or accepted
same-turn location reference. The client rewrites both endpoints and assigns a
permanent link identity. Reverse links are never inferred.

**Rationale**: One-way passages, portals, hidden paths, and sealed routes are
first-class topology. Endpoint identity cannot safely be derived from target
coordinates or names.

**Alternatives rejected**:

- Embedded adjacency objects: no independent identity or lifecycle.
- Source ID plus target coordinates: coordinates are placement, not identity.
- Automatic symmetry: changes authored access and can reveal hidden routes.

## Decision 7: Bootstrap uses ordinary materialization

**Decision**: Fresh bootstrap writes neutral canonical roots, an empty location
identity index, and a client-owned scaffold reserving start/neighbor temporary
references, permanent-ID reservations, coordinate constraints, and one request
identity. The first GM result must materialize the visited start and either a
complete reachable neighbor plus explicit link or a narrative-only unresolved
exit with no location/link identity.

**Rationale**: Bootstrap is the first production route and must prove the normal
contract. A neutral state can remain pending without pretending a placeholder
is playable.

**Alternatives rejected**:

- Pre-populated start and neighbor: bypasses first materialization.
- A bootstrap-only reduced schema: creates a permanent exception.
- A fake neighboring ID for narrative possibility: creates an inspectable entity without semantics.

## Decision 8: Accepted-turn ordering and cross-entity references

**Decision**: Raw validation occurs before mutation. A pure
`MortalLocationAcceptedTurnPlan` then builds one-pass exact indexes and plans all
location/link identities, receipts, canonical objects, and supported field-aware
reference rewrites. Location normalization runs before Mortal item
materialization and before existing NPC/faction accumulated normalizers. It
rewrites only catalogued location-reference fields. Accepted actors and factions
retain their existing effective same-turn identities; #1513 does not redesign
their identity systems.

**Rationale**: Current actor/faction normalization treats accepted `initialId`
as effective identity, while item materialization already assigns client-owned
IDs. Materializing the current location first gives same-turn storage a real
location receipt and stable storage coordinate before item IDs are assigned.
Field-aware rewriting avoids corrupting arbitrary narrative strings.

**Alternatives rejected**:

- Keep the current normalizer order with items first: a new current-location storage has no accepted location authority when item routes are resolved.
- Recursively replace every matching JSON string: can rewrite names and prose.
- Make location materialization wait for redesigned actor/faction IDs: expands #1513 into unrelated entity identity work.
- Commit each file as soon as its normalizer succeeds: permits partial composed state.

## Decision 9: Same-turn storage and item ownership

**Decision**: Map locations store only storage semantics (identity, name, owner,
capacity, access, description). Only the selected current projection may carry
`locationStorages[].contents`. The location normalizer preserves contents
verbatim until #1511 item materialization runs. `MortalItemRouteAuthorityCatalog`
accepts a new location-storage route only when it binds the exact storage of an
accepted same-turn current-location creation. Location repair never creates,
moves, seals, or repairs an item.

**Rationale**: This enables a complete new scene to contain inspectable items
without making location semantics a competing item authority.

**Alternatives rejected**:

- Store item contents in the global map: duplicates active carriers and exposes hidden inventories.
- Reject every same-turn new-location item: prevents ordinary complete scene creation.
- Let location repair edit item identity: crosses the #1511 authority boundary.

## Decision 10: Atomicity and post-validation

**Decision**: Extend the existing accepted-turn lease and before-image rollback
contour. Add world map, current location, location index, and every governed
reference carrier to tracked paths. Sequence: validate raw state, build plan,
validate composed state in memory, acquire one lease, write all planned state,
run combined item/location post-normalization checks, and restore every touched
path byte-for-byte on any failure.

**Rationale**: `AcceptedTurnCanonicalStateRefresh` already provides the right
transaction boundary. Extending it avoids a second lock protocol and ensures
links/current projection cannot commit without the map/index.

**Alternatives rejected**:

- Independent map/current/index writes: leaves partial topology on failure.
- Best-effort cleanup: cannot guarantee exact recovery after several writes.
- Validate only before IDs are assigned: misses receipt/index/projection mismatches.

## Decision 11: Validation and repair

**Decision**: Add one selectable accepted-turn location materialization phase
using the free phase bit 30. Validation distinguishes raw creation/lifecycle
commands from canonical state, builds exact indexes once, and emits stable
location-specific issue codes with structured repair context. A dedicated
`MortalLocationRepairPacketBuilder` emits a bounded packet only for one exact
repairable carrier. Missing/ambiguous/replayed identity, protected fields, index
state, receipts, seals, and coordinate collisions fail closed before GM repair
dispatch.

**Rationale**: Existing generic location packets instruct the GM to repair
legacy adjacency and coordinate/name shapes and cannot safely target the new
identity boundary.

**Alternatives rejected**:

- Expand generic repair text: lacks structured identity and transition context.
- Ask the GM to create client fields: violates ownership.
- Repair ambiguous candidates by name: may edit the wrong world entity.

## Decision 12: Canonical-only readers and discovery projection

**Decision**: `LocalMapViewService`, console location/map panels, browser
location/map DTOs, Mortal news threat lookups, locality checks, training/trade,
NPC/faction location authority, and movement read receipt-bearing canonical map
objects and exact links only. A shared `MortalLocationPlayerProjection` applies
discovery visibility and recursively strips identity/repair/protocol internals.
Hidden entities contribute no row, count, edge, detail, or action; rumors expose
only their safe summary.

**Rationale**: Filtering only the main map view would leave raw data reachable
through detail, locality, action, news, or nested DTO paths.

**Alternatives rejected**:

- Keep name/slug fallback for navigation: lets display text become authority.
- Filter after a raw DTO is assembled: risks nested leaks and false counts.
- Maintain separate console/browser visibility policies: creates semantic drift.

## Decision 13: Documentation and fixture synchronization

**Decision**: Update Rule 20, Example 20, CLI API, main task guide/example, CLI
operations, daemon reminder, validation manifest, active bootstrap state,
positive fixtures, generated helpers, and documentation/source guards. Worked
examples cover start plus neighbor/link, hidden remote plus reveal, and invalid
package plus bounded repair. Audit other Mortal docs and edit only those that
actually repeat the changed contract.

**Rationale**: The GM cannot inspect client code during play. The executable
contract and authoring guidance must change together.

**Alternatives rejected**:

- Code-only implementation: guarantees GM authoring errors.
- Prompt-only correction: cannot enforce identity, atomicity, or repair safety.
- Update afterlife documents now: #1514 owns a distinct realm contract.

## Decision 14: Verification and performance

**Decision**: Use focused fast and integration lanes after each coherent TDD
slice, one Fast checkpoint after runtime/projection integration, FullValidation
for examples/manifest, LifecycleIntegration for bootstrap/transaction/repair,
and one final PreMerge. Add a deterministic scaling test for location/link
indexing with a 2.5x doubling threshold.

**Rationale**: The repository test runner provides bounded logs, timeout
diagnostics, and lane-specific evidence. Exact indexes must be protected from
regressing into nested scans.

**Alternatives rejected**:

- Unbounded full-solution `dotnet test`: violates repository policy and obscures failures.
- Wall-clock microbenchmark only: unstable on shared machines.
- Skip broad controls because focused tests pass: insufficient for a cross-contract accepted-turn change.

## Resolved Questions

No design clarification remains open. After Phase 1, the constitution gate still
passes: the issue is tracked; save compatibility is intentionally absent; TDD,
player privacy/parity, GM documentation, atomic state authority, and bounded
verification are all explicit.
