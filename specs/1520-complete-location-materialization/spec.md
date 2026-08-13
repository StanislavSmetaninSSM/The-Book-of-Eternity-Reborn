# Feature Specification: Complete Mortal Location Materialization

**Feature Branch**: `1520-complete-location-materialization`

**Created**: 2026-08-12

**Status**: Approved

**Input**: Enforce complete first materialization of every durable Mortal World location and its topology, with one canonical map authority, exact identity, atomic current-scene projection, bounded repair, and no runtime compatibility for obsolete development saves.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#1513 — Enforce complete first materialization of Mortal World locations](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)
- **Issue type**: P1 enhancement / contract-hardening task
- **Spec Kit justification**: The feature changes canonical Mortal World map state, first-creation routes, validation, normalization, bootstrap, rollback/repair, GM-authored contracts, worked examples, fixtures, and console/browser projections across multiple files and sessions.
- **Contract scope**: GM-facing prompts; Mortal runtime state; current-scene projection; world topology; validation; normalization; identity continuity; rollback/repair; bootstrap; movement integration; faction, actor, lore, threat, and storage references; console/browser map and location projections; docs; examples; fixtures; manifests; source guards.
- **Save compatibility**: Not required. The game is unreleased; obsolete receipt-less development state, examples, bootstrap data, and positive test fixtures are migrated to the current contract rather than promoted or supported at runtime.
- **Out of scope**: Shining Abode halls and Chaos Sea Guardian planes (#1514); transport and storage entity materialization or capacity rules (#1515); autonomous living-world event execution; general map/browser visual redesign; network multiplayer; ordinary later edits except the narrow location and link lifecycle rules defined here; runtime legacy promotion or save migration.

## Clarifications

### Session 2026-08-12

- Q: Should Mortal locations and afterlife locations be delivered together? → A: No. #1513 covers Mortal World locations first; #1514 follows as a separate Spec Kit feature and pull request.
- Q: Which state owns durable Mortal location semantics? → A: The canonical world map owns locations and links. The current-location state is a validated projection plus current-scene operational data, not a second semantic authority.
- Q: How should obsolete receipt-less locations be handled? → A: Reject them and migrate repository fixtures. The unreleased game has no save-compatibility requirement and no runtime promotion path.
- Q: How are starting locations created? → A: Bootstrap reserves exact starting references and requests ordinary complete materialization. It does not write a fake-ready legacy location.
- Q: How does the GM create current and remote locations? → A: A new current scene has one complete current-location creation carrier; a new remote location has one complete world-map creation carrier. Duplicating the same new location across both is invalid.
- Q: Does a new map link imply a reverse link? → A: No. Direction, access, and visibility are explicit; one-way, portal, hidden, and sealed links are supported without inferred symmetry.
- Q: What can identify a location or link? → A: Exact client-issued permanent identity or an authorized exact same-turn temporary reference. Names never establish identity.
- Q: Can a hidden location be materialized before the player knows it? → A: Yes. It remains available to the GM as canonical state while player projections reveal only the discovery tier permitted by the contract.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A New Mortal Location Is Complete on First Acceptance (Priority: P1)

As a player, when the story enters or reveals a new Mortal World place, I receive one coherent location with stable identity, physical setting, placement, difficulty, history, occupants, ownership, storage metadata, and topology rather than disconnected prose or a skeletal map point.

**Why this priority**: A partial location cannot safely support movement, NPC placement, faction control, storage, threats, map inspection, or later living-world changes.

**Independent Test**: Create one complete current location and one complete remote location through their authorized routes. Each is accepted once with stable identity and canonical topology, while each route rejects the same omitted required section without writing partial state.

**Acceptance Scenarios**:

1. **Given** a valid pre-turn Mortal state without the location, **When** the GM authors one complete current-scene location through the current-location creation route, **Then** the system accepts one canonical map location, assigns permanent identity, seals materialization evidence, and builds a coherent current-scene projection.
2. **Given** a valid pre-turn Mortal state without the location, **When** the GM authors one complete remote, hidden, rumored, discovered, or visited location through the world-map creation route, **Then** the system accepts one canonical map location without making it the current scene.
3. **Given** a new location missing a governed semantic section, explicit empty disposition, source authority, exact placement, or required topology disposition, **When** the turn is validated, **Then** no part of that location, its links, or its current projection is committed.
4. **Given** the same new location in both creation carriers, **When** the turn is validated, **Then** the duplicate creation is rejected rather than merged by name or temporary reference.

---

### User Story 2 - A New Game Starts in a Materialized World (Priority: P1)

As a player, a new Mortal life starts in a real visited location with a coherent first route into the world, not in a placeholder that only becomes valid after unrelated play.

**Why this priority**: Bootstrap is the first golden path and must exercise the same rules as later play instead of bypassing them.

**Independent Test**: Start a fresh Mortal session, complete the initial GM turn, and verify that the reserved starting location and its first reachable neighbor or explicit narrative-only exit follow the normal creation contract.

**Acceptance Scenarios**:

1. **Given** a fresh Mortal bootstrap scaffold, **When** the GM supplies a complete starting scene and a complete reachable neighbor, **Then** both locations and their link are accepted through ordinary first materialization using the reserved exact references.
2. **Given** the first neighboring place is not yet semantically created, **When** the GM only narrates a possible route, **Then** it remains a player-facing hint and does not become a canonical location or link identity.
3. **Given** bootstrap output lacks the complete starting scene, **When** validation runs, **Then** the session remains pending and no receipt-less placeholder is treated as playable canonical state.

---

### User Story 3 - Topology and Existing-Location Updates Stay Exact (Priority: P1)

As a player, travel exits and map relationships lead to the exact intended places, and revisiting a location never silently recreates or rewrites its original identity.

**Why this priority**: Name-based or divergent topology can send the player to the wrong location, leak hidden paths, or create duplicate worlds.

**Independent Test**: Create directed, one-way, hidden, portal, and isolated topology; revisit and narrowly update existing locations; verify exact endpoints, immutable materialization evidence, and derived current-scene exits.

**Acceptance Scenarios**:

1. **Given** two new locations with explicit temporary references, **When** a same-turn link connects them, **Then** both endpoints are rewritten to exact permanent location identities and the link receives its own permanent identity.
2. **Given** a one-way or hidden link, **When** it is accepted, **Then** no reverse link or player-visible exit is invented.
3. **Given** an existing location, **When** the GM sends a narrow authorized semantic update, **Then** only the permitted fields change while original identity, materialization envelope, receipt, and unrelated topology remain immutable.
4. **Given** an existing location is resent as a full creation object or identified only by name, **When** validation runs, **Then** the turn is rejected.
5. **Given** the player moves to an existing location by exact identity, **When** one exact pre-turn directed current-to-destination link is open, requirement-free, and player-known/non-hidden, **Then** the current-scene projection is rebuilt from the canonical map rather than accepting a second full semantic copy from the GM.
6. **Given** an exact destination with no authorized outgoing link, a reverse-only link, or a hidden, conditional, sealed, or unmet route, **When** movement is requested, **Then** the turn fails closed without changing selection or revealing the destination.
7. **Given** accepted storage and threat children, **When** the GM uses the six governed storage/threat lifecycle commands, **Then** exact metadata, removal, creation, update, deletion, and activity completion are applied atomically without rewriting creation evidence or item contents.

---

### User Story 4 - Location State Commits Atomically or Rolls Back (Priority: P1)

As a GM operator and player, malformed location creation can be repaired without leaving orphan links, conflicting coordinates, partial actor/faction placement, or a current scene that disagrees with the map.

**Why this priority**: A partially committed map can make navigation and subsequent turns irrecoverably ambiguous.

**Independent Test**: Submit malformed packages that fail before and after identity assignment, verify exact rollback of every touched surface, then repair the same raw command through one bounded location-specific packet.

**Acceptance Scenarios**:

1. **Given** a location package with a dangling endpoint, conflicting coordinates, parent cycle, realm leak, or invalid companion reference, **When** validation fails, **Then** the validated pre-turn state remains authoritative across the map, current scene, identity authority, and governed companions.
2. **Given** a repairable package with one exact location or link coordinate, **When** repair is requested, **Then** the GM receives one bounded packet naming only the raw carrier, invalid or missing sections, conflicts, and safe correction rules.
3. **Given** missing or ambiguous identity evidence, **When** repair classification runs, **Then** the system fails closed with a path-bound diagnostic and does not ask the GM to invent permanent identities, receipts, seals, or index entries.
4. **Given** a corrected package, **When** post-normalization validation succeeds, **Then** all canonical surfaces commit once and no earlier failed attempt remains visible.

---

### User Story 5 - Discovery Reveals Only What the Player Knows (Priority: P2)

As a player, the map and location browsers show visited and discovered places meaningfully, rumored places safely, and hidden GM-only places not at all.

**Why this priority**: The GM must be able to prepare the world in advance without player clients exposing hidden coordinates, descriptions, closed paths, or internal materialization data.

**Independent Test**: Materialize locations and links in every allowed discovery tier, inspect console and browser map/location surfaces, and compare their visible semantics and forbidden internal fields.

**Acceptance Scenarios**:

1. **Given** a hidden GM-only location or link, **When** the player opens map, location list, detail, or navigation actions, **Then** no name, coordinate, full description, endpoint, or actionable path is exposed.
2. **Given** a rumored player-known location, **When** it is shown, **Then** only the safe rumor subset appears without precise coordinates, closed routes, or full hidden detail.
3. **Given** a discovered or visited location, **When** it is shown, **Then** its permitted in-world detail is available consistently in console and browser surfaces.
4. **Given** any accepted location, **When** the player inspects it, **Then** temporary references, envelopes, receipts, seals, identity-index data, file paths, repair packets, and validation vocabulary remain hidden.
5. **Given** an invalid or receipt-less candidate, **When** player projections run, **Then** it creates no location row, map node, detail, or navigation action.

---

### User Story 6 - Cross-Entity Location References Are Coherent (Priority: P2)

As a player, NPCs, factions, lore, threats, and storage presented at a location all refer to the same exact place and do not contradict their own canonical authority.

**Why this priority**: A complete location is useful only if the other materialized world entities can safely bind to it.

**Independent Test**: Materialize a location with same-turn actor, faction, threat, and storage metadata references plus pre-existing canonical lore references; verify exact resolution, realm consistency, and rejection of name-only, dangling, or contradictory references.

**Acceptance Scenarios**:

1. **Given** valid same-turn temporary actor/faction/storage/threat references in their explicitly supported fields, **When** all referenced entities are accepted, **Then** each field is rewritten or bound to the exact permanent identity from its own authority. Lore bindings use only pre-existing canonical permanent IDs because #1513 does not rematerialize codex, quest, or world-event identity.
2. **Given** an actor binding that contradicts the actor's canonical physical location, **When** validation runs, **Then** the composed turn is rejected.
3. **Given** a location-storage declaration, **When** the location is current, **Then** storage identity, name, owner, and capacity agree between map semantics and current-scene projection while item contents remain under item transition authority.
4. **Given** a name-only, dangling, cross-realm, case-variant, or Unicode-confusable reference, **When** validation runs, **Then** it is rejected without guessing the intended target.

### Edge Cases

- Two new locations or links reuse an `initialId`, materialization identity, permanent identity, or confusable case/Unicode variant.
- A same-turn parent, endpoint, actor, faction, threat, or storage reference, or a pre-existing canonical lore reference, resolves to zero or multiple accepted entities.
- A location names itself as parent, forms a longer parent cycle, or crosses realm boundaries through parentage.
- Two distinct locations claim the same exact coordinate tuple, including a hidden location and a visible location.
- A current scene is not `visited` and player-known, or a hidden location is selected as current without an authorized reveal transition.
- Discovery status and audience contradict one another, such as hidden/player-known or visited/GM-only.
- A rumored location contains precise coordinates, full description, hidden endpoints, or an actionable closed route in player projection.
- A location declares `populated` topology without a valid link, or `empty_by_design` topology while a same-turn link targets it.
- An isolated, sealed, or non-topological destination legitimately has no links but omits the required in-world reason.
- A one-way link is mistakenly treated as bidirectional, or two explicit reverse links disagree about access or visibility.
- A link endpoint references an existing location by name, case-insensitive identity, surrounding-whitespace variant, or an unsupported nested string.
- A raw command contains `knownExits` or an adjacency map that contradicts canonical links; topology remains link-owned and the claim is rejected or ignored according to the closed contract.
- Current-location semantics disagree with the selected canonical map location while weather or local interactions are valid.
- Current-scene storage contents contain accepted items, rejected item candidates, or a stale storage identity; location repair must not take ownership of item transition repair.
- A complete location appears in a lore or NPC companion before any authorized location creation carrier owns it.
- A failed post-normalization check occurs after identities were reserved; all touched state and reservations are restored or remain safely reusable according to the transaction contract.
- A receipt-less repository fixture is encountered; positive data is migrated and runtime validation rejects the obsolete shape.
- A Mortal location command attempts to materialize a Shining hall, Chaos Sea Guardian plane, or afterlife topology.
- The player map has no visible locations while hidden locations exist; the UI shows an in-world empty/unknown state without leaking hidden count or authority data.

## Requirements *(mandatory)*

### Functional Requirements

#### Canonical Authority and Identity

- **FR-001**: The system MUST use one canonical Mortal world-map location collection as the sole durable semantic authority for materialized Mortal locations.
- **FR-002**: The system MUST use one canonical world-map link collection as the sole durable authority for Mortal topology.
- **FR-003**: The current-location state MUST be a validated projection of one selected canonical map location plus current-scene operational data; it MUST NOT act as an independent durable semantic authority.
- **FR-004**: Fields shared by the selected map location and current-location projection MUST agree exactly, including identity, realm, presentation, type, placement, coordinates, discovery, difficulty, faction/actor bindings, storage metadata, topology, and materialization status.
- **FR-005**: Current-scene weather, local interactions, scene chronology, and selected-location storage contents MAY remain operational current-location data when they do not replace canonical location or item authority. Non-current storage contents MUST reside only in the closed client-owned offscreen carrier and MUST never be GM-authored.
- **FR-006**: The system MUST maintain a client-owned location identity authority that records exact permanent identity, accepted temporary reference, materialization receipt, realm, source turn, source authority, and lifecycle status.
- **FR-007**: The GM and player-facing clients MUST NOT author, patch, or derive permanent location/link identities, sealed receipts, or identity-authority entries.
- **FR-008**: Identity comparison MUST be exact and case-sensitive, and validation MUST reject surrounding-whitespace, case-only, Unicode-normalization, and other confusable variants rather than normalizing them into equality.
- **FR-009**: Display name, description, coordinates, or narrative MUST NOT establish location or link identity.
- **FR-010**: Receipt-less canonical locations and links MUST be invalid. The runtime MUST NOT promote, complete, or tolerate obsolete development shapes.

#### First-Creation Routes and Bootstrap

- **FR-011**: A new current-scene location MUST be authored exactly once through the complete current-location creation carrier.
- **FR-012**: A new remote, hidden, rumored, discovered, visited, or referenced location that is not the current scene MUST be authored exactly once through the complete world-map creation carrier.
- **FR-013**: The same new location MUST NOT appear in both creation carriers in one turn, even when the objects are otherwise identical.
- **FR-014**: Every new location MUST use one exact, unique same-turn `initialId`; the client MUST assign the permanent location identity only after pre-seal semantic validation succeeds.
- **FR-015**: Accepted same-turn references MUST be rewritten only in an explicit catalog of supported fields; recursive arbitrary string replacement is forbidden.
- **FR-016**: Fresh bootstrap MUST reserve exact starting references and request ordinary complete materialization of the starting scene rather than writing a playable receipt-less placeholder.
- **FR-017**: Bootstrap MUST require a complete starting location and MAY require either a complete first reachable neighbor plus valid link or an explicitly narrative-only unresolved route that receives no location/link identity.
- **FR-018**: A current-location creation MUST register the new location in the canonical map automatically; the GM MUST NOT duplicate the same semantic object in a second map carrier.
- **FR-019**: A remote-location creation MUST NOT implicitly select the location as current or make it player-visible beyond its accepted discovery tier.

#### Complete Location Semantics

- **FR-020**: Every first materialization MUST include a complete semantic location object and a versioned GM-authored materialization envelope in the same atomic package.
- **FR-021**: The envelope MUST identify the Mortal realm, source turn, source authority, creation route, independent materialization identity, temporary reference, and disposition of every governed semantic section.
- **FR-022**: Governed section dispositions MUST be closed to `populated` or `empty_by_design`; every intentional-empty section MUST include a non-empty in-world reason and the corresponding canonical field MUST be physically present in its correct empty shape.
- **FR-023**: Every new location MUST include null permanent identity, unique temporary identity, `mortal_world` realm, stable name, display name, purpose, full setting-compatible description, and an English image-generation prompt.
- **FR-024**: Every new location MUST include a physical type; outdoor locations MUST identify a biome and indoor locations MUST identify an indoor type.
- **FR-025**: Every new location MUST include a non-empty region, an optional exact parent reference, and a unique three-dimensional coordinate tuple; parent relationships MUST be acyclic and same-realm.
- **FR-026**: Every new location MUST include both complete difficulty profiles required by the current Mortal location contract and an initial last-events description.
- **FR-027**: Every new location MUST declare exactly one closed discovery combination: hidden/GM-only, rumored/player-known, discovered/player-known, or visited/player-known; the selected current scene MUST be visited/player-known.
- **FR-028**: Reachability MUST be derived from accepted canonical links and MUST NOT be trusted as a free-standing GM-authored Boolean.
- **FR-029**: Every new location MUST physically include faction control, actor bindings, storage metadata, active threats, lore bindings, and custom states as populated or explicitly empty collections. The raw creation carrier MUST keep `activeThreats` empty because permanent threat identity is client-owned; same-turn threats MUST be submitted separately through `threatsToAdd` against the exact new-location `initialId`.
- **FR-030**: Actor bindings MUST use an explicit role such as resident, owner, staff, prisoner, or other and MUST agree with the actor's own canonical physical-location authority.
- **FR-031**: Lore bindings MUST use exact canonical identifiers for supported codex, quest, and world-event authorities; file paths and names are not valid references.
- **FR-032**: Location-storage metadata MUST include exact storage identity and the governed name, owner, and capacity semantics, while contained items remain governed by item identity and transition authority.
- **FR-033**: The location envelope's topology section MUST be `populated` only when at least one valid accepted link applies, or `empty_by_design` with an in-world reason for an isolated, sealed, or non-topological destination.

#### Link and Existing-State Lifecycle

- **FR-034**: Every new canonical link MUST receive a permanent link identity and MUST bind exact existing location identities or authorized same-turn temporary location references as endpoints.
- **FR-035**: Every link MUST declare direction, access, and visibility semantics explicitly; one-way, portal, hidden, sealed, and other setting-compatible link types MUST NOT cause an inferred reverse link.
- **FR-036**: Canonical links MUST be the source for derived current-scene exits and adjacency; GM-authored `knownExits`, adjacency maps, or equivalent summaries MUST NOT override link authority.
- **FR-037**: Existing-location movement MUST identify the destination by exact permanent identity, MUST be authorized by one exact pre-turn directed current-to-destination link with open access, no requirements, and player-known/non-hidden link and destination, MAY carry current-scene operational chronology, and MUST NOT resend the full durable semantic location. Same-location refresh MAY remain allowed without traversal.
- **FR-038**: Ordinary semantic changes to an existing location MUST use narrow authorized updates; topology changes MUST use link-specific lifecycle commands; storage and threat children MUST change only through `storageUpdates`, `storagesToRemove`, `threatsToAdd`, `threatsToUpdate`, `threatsToRemove`, and `completeThreatActivities`.
- **FR-039**: Existing-location updates and movement MUST preserve permanent identity, original materialization envelope, sealed receipt, unrelated semantics and topology, and item-owned storage contents. A changed selection MUST atomically park non-empty source contents in the client-owned offscreen carrier, hydrate the destination, retain the same logical `location_storage(locationId, storageId)` item carrier, and roll back every involved file byte-for-byte on failure. Section dispositions remain immutable creation evidence after later governed child-count changes; threat completion archive entries are client-owned.
- **FR-040**: A full resend of an existing location, attempted receipt/materialization mutation, name-based update, or client-owned identity edit MUST be rejected.
- **FR-041**: The system MUST reject dangling endpoints, duplicate active identities, coordinate conflicts, parent cycles, impossible discovery combinations, topology contradictions, and Mortal/afterlife realm leakage.

#### Atomicity, Repair, and Rollback

- **FR-042**: Location acceptance MUST validate raw commands without mutation, plan identity and reference changes in memory, validate the complete composed state, commit every governed surface under one bounded transaction, and validate the committed canonical result before success.
- **FR-043**: A failed creation or update MUST restore the exact validated pre-turn authority for the canonical map, current scene, location identity authority, and every touched governed companion; no orphan location, link, receipt, reference rewrite, or player-facing confirmation may remain.
- **FR-044**: Bootstrap-reserved identities MUST be honored exactly for the starting package; ordinary accepted creation MUST use collision-free client identities that cannot be chosen by the GM.
- **FR-045**: Validation MUST reconcile exact parent, actor, faction, lore, threat, storage, topology, realm, and current-scene references against their applicable canonical authorities.
- **FR-046**: A bounded location repair packet MUST target one exact location, link, or unambiguous raw carrier and list only invalid/missing sections, affected raw paths, reference conflicts, and safe GM-owned corrections.
- **FR-047**: Repair guidance MUST NOT instruct the GM to author permanent identities, receipts, seals, identity-index entries, or unrelated state.
- **FR-048**: Missing, ambiguous, reused, retired, or historically conflicting identity evidence MUST fail closed before GM repair dispatch and retain a path-bound operator diagnostic.
- **FR-049**: The validated pre-turn snapshot MUST remain rollback authority through the complete repair loop, and a corrected package MUST settle at most once.
- **FR-050**: Validation and repair MUST build bounded exact indexes rather than repeatedly scanning every location and link per candidate.

#### Player Projection, Documentation, and Migration

- **FR-051**: Console and browser map, location-list, location-detail, and navigation surfaces MUST read only receipt-bearing canonical map locations and links.
- **FR-052**: The current-scene projection MUST be used by player surfaces only after its shared fields have been reconciled with the selected canonical map location.
- **FR-053**: Hidden GM-only locations and links MUST be absent from ordinary player surfaces; rumored locations MUST expose only a safe rumor subset; discovered and visited locations MUST expose equivalent permitted semantics in console and browser.
- **FR-054**: Player-facing surfaces MUST hide temporary references, materialization envelopes, receipts, seals, identity-authority entries, route evidence, repair packets, file paths, and validation/agent terminology, including nested structured data.
- **FR-055**: Invalid, receipt-less, ambiguous, or rejected location candidates MUST NOT create map nodes, location rows, detail panels, navigation actions, counts, or structured acquisition/discovery claims.
- **FR-056**: Existing movement and storage interactions MUST remain available for accepted canonical locations without a visual redesign, and failures MUST use player-safe in-world messages.
- **FR-057**: GM-facing rules, CLI guidance, daemon/operation entrypoints, worked examples, manifests, and documentation/source guards MUST describe and prove the new Mortal location creation, topology, update, and repair contracts in the same change.
- **FR-058**: Worked GM evidence MUST include at least a visited starting location with a reachable neighbor, a hidden remote location with an explicit one-way or reveal transition, and a malformed package corrected through bounded repair.
- **FR-059**: Active bootstrap state, positive fixtures, examples, and helper-generated locations MUST be migrated to receipt-bearing current-schema locations and links; receipt-less data may remain only in explicitly labelled negative validation fixtures.
- **FR-060**: The Mortal feature MUST reject Shining Abode and Chaos Sea location carriers and MUST record that afterlife contract documentation is unchanged because #1514 owns that separate materialization contract.

### Key Entities

- **Mortal Location**: The durable semantic representation of one place in the Mortal World, including identity, presentation, physical type, placement, discovery, difficulty, chronology, governed bindings, and materialization evidence.
- **Location Link**: A separately identified directed topology relation between exact locations, with explicit access, visibility, and link-type semantics.
- **Current Scene Projection**: The selected canonical location's reconciled shared semantics plus operational scene data such as weather, local interactions, scene chronology, and only the selected location's storage contents.
- **Offscreen Location Storage Contents**: Closed client-owned state that physically holds non-empty item arrays for non-current exact `locationId/storageId` coordinates while preserving the same logical item carrier used by the selected projection.
- **Location Materialization Envelope**: Versioned GM-authored evidence declaring creation route, source, realm, temporary identity, independent materialization identity, and the populated/intentional-empty disposition of every governed section.
- **Location Materialization Receipt**: Immutable client-sealed evidence binding the accepted envelope to a permanent location identity and accepted turn.
- **Location Identity Authority**: Client-owned record of permanent location and link identities, accepted temporary references, receipts, realm, sources, and lifecycle status.
- **Discovery State**: Closed pairing of knowledge tier and audience that governs what GM and player projections may reveal.
- **Location Repair Packet**: Bounded GM-facing description of one unambiguous raw location/link defect and only the safe semantic corrections required to retry it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Complete representative current-scene and remote-location creations are accepted, while the same missing governed section is rejected in both routes with zero partial canonical writes.
- **SC-002**: A fresh Mortal bootstrap reaches its first playable scene with one visited canonical starting location and either one valid reachable neighbor/link or an explicitly non-canonical narrative-only route; no playable receipt-less placeholder remains.
- **SC-003**: One hundred percent of active repository bootstrap state, positive fixtures, examples, and helper-generated Mortal locations and links carry valid current-schema materialization evidence; receipt-less samples remain only as labelled negative cases.
- **SC-004**: Every tested directed, one-way, portal, hidden, sealed, or isolated topology case preserves its explicit direction/access/visibility without inferred reverse links or dangling endpoints.
- **SC-005**: Every tested existing-location move or narrow update preserves exact permanent identity and original receipt; no full resend, name-based identity, no-link, reverse-only, hidden, conditional, or sealed movement route is accepted.
- **SC-006**: Every tested parent, actor, faction, lore, threat, storage, and topology reference resolves exactly and fails closed for missing, duplicate, case-variant, confusable, cross-realm, or contradictory authority.
- **SC-007**: Every injected pre-commit or post-normalization failure restores all touched canonical surfaces byte-for-byte and leaves no location/link receipt, identity reservation, reference rewrite, or player-facing success claim.
- **SC-008**: Every repairable malformed case produces one bounded packet naming all and only the required location/link targets; ambiguous or historical identity conflicts dispatch no unsafe GM repair request.
- **SC-009**: Console and browser expose equivalent permitted semantics for visited, discovered, and rumored locations while exposing zero hidden-location data or raw materialization, receipt, identity, path, repair, validation, or agent fields.
- **SC-010**: Doubling a representative location/link population does not cause more than 2.5 times the validation work in the repository performance control, preventing quadratic topology and identity scans.
- **SC-011**: Mortal GM documentation contains the five required worked flows—starting connected world, existing movement with storage continuity, hidden one-way/reveal flow, governed storage/threat lifecycle, and bounded repair—plus receipt-less rejection, and all referenced examples pass documentation validation.
- **SC-012**: Focused contract/integration tests, one Fast checkpoint, related documentation and lifecycle lanes, and one clean-checkout PreMerge complete with zero failures, zero duplicate tests, no timeout, and successful owned-process cleanup.

## Verification Plan *(mandatory)*

- **C# verification**: During TDD, run `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "<location-materialization and touched-boundary filters>"`; at a meaningful checkpoint run one `Fast`; run `LifecycleIntegration` for bootstrap/turn/rollback changes; immediately before merge run one clean-candidate `PreMerge` and do not duplicate it with another Fast.
- **Documentation/contract verification**: Run focused documentation/source-guard selections for Mortal location guidance and `ExampleDocumentationValidationTests`; run `FullValidation` because shared GM examples/manifests and validation-documentation boundaries change. Afterlife documentation coverage is not required for #1513 unless implementation unexpectedly changes an afterlife contract.
- **Frontend verification**: No visual frontend redesign is expected. Existing C# browser projection and map DTO tests MUST prove canonical-only visibility, discovery privacy, and console/browser semantic parity. Run frontend `npm run verify` only if implementation changes frontend source or its typed contracts.
- **Manual/player-facing verification**: In a generated Mortal session, inspect the starting visited location, a rumored location, a hidden location, a one-way exit, and a rejected location through existing console and browser map/location flows; confirm safe in-world Russian output, correct actions, and absence of internal authority data.

## Assumptions

- The game remains unreleased and no public save population requires compatibility.
- The existing current-location and world-map GM command families remain the route entry points, but their transient wrappers are consumed into the new canonical authority rather than retained as parallel state.
- Existing map/list/detail and movement experiences remain recognizable; the feature hardens their data authority and may adjust projections without redesigning their visual presentation.
- The current canonical NPC, faction, item, quest, codex, threat, and storage semantics remain their own authorities; location materialization validates and binds exact references but does not rematerialize those entities.
- Storage contents remain governed by the Mortal item transition contract established by #1511. Location materialization owns storage metadata and the atomic current/offscreen physical swap only; the logical item carrier and item identity index do not change merely because a location becomes current or non-current.
- Weather and local interactions remain scene-operational state and may change independently under their existing contracts after the selected location is materialized.
- #1514 will define Shining hall and Chaos Sea Guardian-plane materialization, including its own afterlife documentation and contract matrix updates.
- #1515 may later strengthen transport and storage entity identity without changing the location identity, topology, and projection guarantees established here.
