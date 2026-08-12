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

## Implementation preflight and issue drift

Issue #1513 remained open on 2026-08-12 and the implementation worktree was on
`1520-complete-location-materialization`. Re-reading the repository governance,
constitution, specification, plan, data model, contracts, quickstart, and tasks
found no scope drift. Mortal World locations remain the only implementation
target; Shining Abode halls and Chaos Sea Guardian planes remain assigned to
#1514. The project is unreleased, so receipt-less repository fixtures are
migrated or retained only as explicitly malformed negative inputs rather than
supported by a runtime compatibility path.

The preflight found generated `bin/obj` directories in the integration and
test-support projects. They are preserved on disk and ignored explicitly; no
generated directory was deleted or treated as source work.

## Production reader/writer inventory (T005)

This inventory was built from exact searches for `current_location.json`,
`world_map.json`, `knownExits`, `adjacencyMap`, location aliases, and coordinate
endpoints. “Replace” means the consumer must use the canonical receipt-bearing
map/projection or the accepted-turn plan. “Retain” means the path is a raw
authoring route or an intentionally player-facing derived projection, not a
legacy canonical fallback.

| Production surface | Present behavior / authority risk | #1513 disposition |
|---|---|---|
| `Configuration/FileMapping.cs` | Maps raw `currentLocationData` and `worldMapUpdates` response carriers to state files. | Retain only as pre-seal route mapping; canonical readers must not consume the wrappers. |
| `Core/GameEngine/GameEngine.TurnLifecycle.cs` | Creates pseudo-ready Mortal start/neighbor data and emits legacy bootstrap guidance. | Replace with neutral roots, exact reservations, and ordinary materialization requests. |
| `Core/GameEngine/GameEngine.ValidationAndRepair.cs` | Dispatches generic location repair packets and accepted-turn refresh. | Add the selectable location phase and bounded location repair/rollback path. |
| `Core/StateManager.cs` | Reads current-location display state directly. | Read the validated canonical current projection only. |
| `Services/MortalBootstrapStateBuilder.cs` | Writes abbreviated start, neighbor, `knownExits`, and `adjacencyMap` state. | Replace with empty canonical map/current/index plus client-owned scaffold reservations. |
| `Services/CanonicalStateNormalizer.cs` | Tracks current location but has no location normalizer/index and runs item normalization first. | Register map/current/index and normalize locations before items under the shared lease. |
| `Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs` | Resolves NPC location authority through recursive/current/map aliases. | Resolve only exact active canonical location IDs from the accepted plan/index. |
| `Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs` | Validates legacy current/map wrappers, adjacency, and coordinate/name links. | Replace with exact canonical-root and current-projection coherence validation. |
| `Services/Validation/ValidationService.InventoryNpcWorldCrossRefs.cs` | Recursively enumerates location-like objects and compares identity case-insensitively. | Use one exact canonical location/link index; reject aliases and confusables. |
| `Services/Validation/ValidationService.NpcWorldAndMeta.cs` | Accepts several location-name/ID comparison paths. | Bind governed references to exact active IDs; names remain display-only. |
| `Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs` | Allowlists existing world/current files but not the location identity index. | Register and protect the current-schema roots and client-owned index. |
| `Services/Validation/ValidationService.PrivateImplementation.cs` | Supplies legacy location helpers and file-shape checks. | Remove alias/name authority helpers and add exact current-schema primitives. |
| `Services/Validation/ValidationService.CoreBootstrapAndCrossRefs.cs` | Cross-validates bootstrap/location references using present abbreviated state. | Validate neutral scaffold reservations and exact accepted canonical references. |
| `Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs` | Participates in accepted-turn validation sequencing. | Include the location completeness phase without changing unrelated story authority. |
| `Services/Validation/ValidationService.MortalBootstrapContentAnchors.cs` | Recognizes bootstrap content anchors in current/map state. | Resolve reserved anchors through scaffold evidence and accepted IDs only. |
| `Services/Validation/ValidationService.MortalBootstrapPlaceholderNames.cs` | Treats placeholder names as bootstrap evidence. | Remove name-as-authority behavior; exact reserved refs are authoritative. |
| `Services/Validation/ValidationService.MortalFactionMaterialization.cs` | Recursively gathers current/map location carriers for faction territory references. | Consume the shared exact active location index. |
| `Services/LocalMapViewService.cs` | Reads wrappers, `knownExits`, `adjacencyMap`, name fallbacks, and derived fallback layout. | Read accepted locations/links and discovery projection only; no raw/name fallback. |
| `Services/LocalInteractionScopeService.cs` | Uses Mortal location ID/name aliases for locality. | Compare exact canonical location IDs; names remain presentation. |
| `Services/ActorMemoryService.cs` | Resolves current location and memories with name/case-insensitive fallback. | Resolve the canonical current ID and exact active location references. |
| `Services/NpcTradeService.cs` | Uses location names/fallbacks to establish NPC trade locality. | Use exact accepted current/NPC location IDs. |
| `Services/RivalSoulArcService.cs` | Reads location context for rival story state. | Use canonical current projection and exact location references. |
| `Services/NpcCoreChangesContract.cs` | Recursively accepts current/map location evidence for NPC changes. | Bind changes to the accepted-turn location plan/exact canonical index. |
| `Services/MortalItemCarrierCatalog.cs` | Catalogs location storage and current-location item carriers. | Bind storage carriers to exact accepted location/storage identities. |
| `Services/MortalItemRouteAuthorityCatalog.cs` | Resolves item routes through current/map storage coordinates. | Consume post-location-normalization exact carrier coordinates. |
| `Services/MortalItemRepairPacketBuilder.cs` | Includes location carrier coordinates in item repair context. | Retain exact coordinates, sourced from accepted location authority only. |
| `Services/StorageTransportMoveService.cs` | Resolves location storage targets through current/map data. | Enumerate accepted storages from canonical locations and select exact IDs. |
| `UI/ExplorerMode/ExplorerMode.MetaLoreAndTravel.cs` | Builds map/navigation from adjacency and raw update collections with name fallbacks. | Use shared discovery-filtered canonical location/link projection. |
| `UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs` | Renders raw location details, adjacency, storage, and counts. | Render accepted projected semantics only; hidden/rejected data changes no count. |
| `UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs` | Reads current-location context for story/status panels. | Read the canonical current projection only. |
| `UI/ExplorerMode/ExplorerMode.MetaWorldSetupAndDebug.cs` | Exposes setup/debug location state. | Keep operator-only diagnostics separate from player projection. |
| `UI/ExplorerMode/ExplorerMode.Inventory.cs` | Resolves current-location storage links and item placement. | Use accepted canonical storage identities and the shared player projection. |
| `UI/ExplorerMode/ExplorerMode.Npcs.ListAndDetails.cs` | Displays NPC locality from location fields. | Resolve exact accepted location identity, then display safe semantic names. |
| `UI/ExplorerMode/ExplorerMode.Npcs.Trade.cs` | Carries trade locality into console actions. | Gate actions on exact accepted locality. |
| `UI/ExplorerMortalWorldCommandResultBuilder.cs` | Builds browser current/location/map/storage DTOs from raw shapes. | Project only accepted canonical objects and recursively remove authority fields. |
| `UI/ExplorerMortalWorldNewsCommandResultBuilder.cs` | Reads `worldMapUpdates`, `newLocations`, and `locationUpdates` for news/threats. | Resolve news against accepted canonical IDs; raw candidates are never player-visible. |

No afterlife production reader is migrated by #1513. Any shared primitive touched
later must preserve the explicit Mortal realm gate, and an afterlife docs update
is required only if that boundary actually changes.
