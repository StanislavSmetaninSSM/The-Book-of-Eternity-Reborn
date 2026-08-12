# Complete Mortal Location Materialization Design

**Date**: 2026-08-12

**Source issue**: [GitHub #1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)

**Spec Kit feature**: `specs/1520-complete-location-materialization/`

**Status**: Design approved in conversation; written specification awaiting final review

## Purpose

Mortal locations currently arrive through current-location and world-map command shapes, while map, current scene, exits, actors, factions, storage, and player projections can each retain overlapping fragments. Existing validators check many individual fields, but the project has no single boundary proving that a location was complete when it first became durable, that current-scene and map semantics agree, or that topology and cross-entity references commit atomically.

This design adds that first-materialization boundary while preserving the existing movement, map, location-list, weather, interaction, and storage experiences.

## Accepted Product Decisions

1. Mortal location materialization (#1513) and afterlife hall/Guardian-plane materialization (#1514) are separate features and pull requests.
2. The canonical world map is the sole durable semantic authority for Mortal locations and links.
3. Current-location state is a validated projection of the selected map location plus operational scene data, not a second semantic owner.
4. A new current scene is authored once through `currentLocationData`; a new remote location is authored once through `worldMapUpdates.newLocations[]`. The same new location may not appear in both.
5. Existing temporary field `initialId` remains the same-turn location reference. Names never identify locations or links.
6. Bootstrap reserves the starting references and requests ordinary complete materialization. It does not create a playable receipt-less placeholder.
7. Links have independent identities and explicit direction, access, and visibility. No reverse link is inferred.
8. Discovery is a closed contract: hidden/GM-only, rumored/player-known, discovered/player-known, or visited/player-known. The current scene is always visited/player-known.
9. The game is unreleased. Receipt-less development state is invalid; repository fixtures are migrated and runtime legacy promotion is removed from scope.

## Current-State Observations

- State distribution can write `currentLocationData` to `game_state/world/current_location.json` and `worldMapUpdates` to `game_state/world/world_map.json`, but no shared location normalizer establishes a single semantic owner.
- Existing map state is command-shaped in places (`newLocations`, `locationUpdates`, `newLinks`) rather than a sealed canonical location/link registry.
- Current-location state contains both durable location-like fields and operational scene data, so it can diverge from world-map summaries.
- Existing validators already understand substantial location semantics such as coordinates, biome/indoor type, difficulty, adjacency, storages, threats, links, and NPC placement. The missing layer is first-materialization identity, route authority, atomic reconciliation, and immutable continuity.
- Existing map and location readers accept aliases, wrapper shapes, case-insensitive identifiers, and some name fallbacks. Those paths are incompatible with exact canonical identity.
- Bootstrap currently writes location-like scaffolding that can look playable before complete GM materialization.
- Shining halls and Chaos Sea Guardian abodes use different state contours; forcing them into the Mortal contract would mix realms and enlarge the checkpoint unnecessarily.

## Approaches Considered

### A. Canonical `world_map` with projected `current_location` — selected

The world map stores every durable location and link. Current-location state is rebuilt from one selected canonical location and augments it with operational scene data.

Advantages:

- Establishes one semantic source of truth without adding a parallel location registry.
- Preserves existing map and current-scene files and most player-facing flows.
- Makes topology naturally global and current exits derivable.
- Allows hidden locations to exist for the GM without becoming current or player-visible.

Costs:

- Requires coordinated normalization and validation across two existing files.
- Requires readers to stop consuming raw update wrappers and name aliases.

### B. Separate global `location_core.json` registry — rejected

A separate registry could normalize locations cleanly, but it would leave the world map as another topology/summary authority and require broader reader, fixture, movement, and projection rewrites. It solves duplication by adding another durable layer.

### C. Keep current-location and world-map as peer authorities — rejected

Cross-validating two full semantic copies appears smaller initially, but every accepted update would require bidirectional synchronization. Failure and repair would retain permanent ambiguity about which copy owns the truth.

## Architecture

```text
GM currentLocationData or worldMapUpdates.newLocations[]
                         |
                         v
             raw route + semantic validation
                         |
                         v
          in-memory identity/reference/link plan
                         |
              +----------+----------+
              |                     |
              v                     v
 world_map locations + links   client location identity authority
              |                     |
              +----------+----------+
                         v
           reconciled current_location projection
                         |
                         v
             console/browser safe projections
```

`world_map.locations[]` owns durable location semantics. `world_map.links[]` owns topology. `current_location.json` identifies one selected canonical location and repeats only validated shared fields needed by current-scene consumers, plus operational data that is meaningful only for the active scene.

## Component Boundaries

### 1. GM-authored location envelope

Every independent location creation carries a versioned envelope alongside the complete location. Conceptually it contains:

- schema version;
- unique independent `materializationId`;
- `mortal_world` realm;
- current or remote creation route;
- source turn and source authority;
- unique same-turn `initialId`;
- one disposition for every governed semantic section.

A disposition is either `populated` or `empty_by_design`. An intentional-empty disposition includes a non-empty in-world reason, and the mapped canonical collection or nullable value is still physically present. The envelope cannot substitute narrative for canonical state.

### 2. Client-sealed receipt

After the raw package passes semantic and route checks, the client assigns the permanent location identity and seals immutable evidence binding:

- the permanent location identity;
- the accepted root envelope;
- the consumed same-turn reference;
- accepted source turn and route;
- client request/session/route evidence;
- receipt identity and seal.

The GM cannot author or patch the permanent identity, receipt, or seal. Existing-location updates never replace them.

### 3. Client-owned location identity authority

The proposed canonical location is `game_state/world/location_identity_index.json`. Each location entry records:

- exact permanent location identity;
- accepted `initialId` and materialization identity;
- sealed receipt identity;
- realm;
- accepted source turn and source authority;
- active lifecycle state.

The same authority or a sibling section records permanent link identities and their accepted temporary references. It also retains settled origin evidence so a retired or historical `initialId`, materialization identity, location identity, or link identity cannot be reused through case or Unicode variants.

This file is client-owned and is never supplied to the GM as a writable target. Validation builds exact dictionaries once per pass rather than scanning every location for each candidate.

### 4. Canonical world map

The canonical map contains:

- `locations[]`: all accepted Mortal locations, including hidden GM-only locations;
- `links[]`: all accepted directed topology relations;
- only narrow canonical metadata needed for map evolution, never raw creation/update command wrappers.

`currentLocationData`, `worldMapUpdates.newLocations`, `worldMapUpdates.locationUpdates`, and link lifecycle commands are transient inputs. The normalizer consumes them into canonical collections and removes their command wrappers from durable state.

### 5. Current-scene projection

The current scene repeats the selected map location's shared fields exactly:

- location identity and realm;
- name, display name, purpose, description, image guidance;
- physical type, biome/indoor type;
- region, parent, and coordinates;
- discovery/visibility;
- difficulty profiles and initial chronology;
- faction, actor, storage-metadata, threat, lore, and custom-state bindings;
- derived topology summary;
- immutable materialization status.

It may additionally own operational scene fields such as:

- current weather;
- local interactions and scene-local opportunities;
- current scene chronology updates;
- `locationStorages[].contents`.

Storage identity, name, owner, and capacity must reconcile with map metadata. Contents remain under the item transition authority from #1511, so location repair does not silently repair, create, or move items.

## Creation Routes

### Current-scene creation

`currentLocationData` carries one complete new location when the story enters a genuinely new scene. The normalizer:

1. proves the `initialId` is new against the pre-turn map and identity authority;
2. validates all semantics and the envelope;
3. plans permanent identity and supported same-turn reference rewrites;
4. inserts the new canonical map location;
5. applies accepted links;
6. seals the receipt and identity entry;
7. rebuilds current-location state from the accepted map location plus operational scene data.

The GM does not repeat the new location in `worldMapUpdates.newLocations[]`.

### Remote-location creation

`worldMapUpdates.newLocations[]` carries a complete location not selected as the current scene. It may be hidden, rumored, discovered, visited, or referenced by accepted same-turn state. Acceptance inserts it into the canonical map and identity authority without selecting it or widening its visibility.

### Bootstrap creation

Fresh bootstrap creates a neutral pending scaffold with reserved starting identity references, coordinate constraints, and materialization requests. The first GM result must provide:

- one complete visited/player-known starting scene; and
- either one complete nearest reachable point with an explicit valid link, or a narrative-only unresolved route without a location or link identity.

These are normal first creations. There is no bootstrap exemption or promotion classifier.

## Complete Location Sections

| Section | Required meaning |
| --- | --- |
| Identity | Null permanent ID before acceptance, unique `initialId`, Mortal realm, source and materialization evidence |
| Presentation | Stable name/display name, purpose, full description, and English image-generation prompt |
| Physical | Location type plus biome for outdoor places or indoor type for indoor places |
| Placement | Non-empty region, optional exact parent, unique x/y/z coordinates, acyclic same-realm hierarchy |
| Discovery | One closed knowledge/audience pair; current scene always visited/player-known |
| Difficulty | Both complete Mortal difficulty profiles |
| Chronicle | Initial last-events description that can later be narrowly updated |
| Faction control | Exact faction identities, roles/control semantics, or explicit absence |
| Actor bindings | Exact actor identities and resident/owner/staff/prisoner/other roles, reconciled with actor state |
| Storage metadata | Exact storage identities and governed name/owner/capacity, never item-content authorship |
| Threats | Complete active-threat references/semantics or explicit absence |
| Lore | Exact codex, quest, and world-event references or explicit absence |
| Custom states | Setting-specific structured state with player/internal projection rules |
| Topology | At least one valid accepted link, or explicit isolated/sealed/non-topological absence reason |

Reachability is derived from accepted links. It is not a trusted standalone flag.

## Link Contract

Every new link has its own temporary reference and client-assigned permanent identity. Endpoints use exact permanent location identities or same-turn location `initialId` values from accepted candidates.

Each link explicitly states:

- source and destination;
- direction;
- traversal/access semantics;
- visibility/discovery semantics;
- link type, including ordinary route, one-way route, portal, hidden path, sealed passage, or other governed type;
- source turn/authority and materialization evidence where applicable.

The normalizer never creates a reverse link unless an explicit second link is authored and validated. `knownExits`, `adjacencyMap`, and similar current-scene summaries are projections derived from canonical links, not GM topology authority.

## Existing-Location Lifecycle

### Movement

Movement to a materialized location identifies the destination by exact permanent `locationId`. The GM may provide scene chronology and other operational current-scene changes, but does not resend the full durable location. The client selects the canonical map object and rebuilds the current projection.

### Narrow semantic update

`locationUpdates` identifies one exact existing location and changes only an explicit mutable-field allowlist. It cannot replace identity, realm, original envelope, receipt, coordinates or hierarchy without their separately governed operation, or unrelated topology.

### Topology update

Links use their own create/update/reveal/seal/remove lifecycle commands. Location semantic update cannot smuggle topology through embedded exits or adjacency summaries.

### Discovery transition

Discovery transitions follow the closed progression and audience contract. A hidden location can become rumored, discovered, or visited only through authorized structured evidence. A current location is always visited/player-known. Visibility transitions affect player projection; they do not recreate the location.

## Same-Turn Cross-References

Only an explicit supported-field catalog is rewritten from temporary to permanent identities. Expected categories include:

- link endpoints;
- parent location;
- NPC physical location and location actor bindings;
- faction control/bindings;
- lore, quest, and event bindings;
- active threat bindings;
- storage metadata references;
- current-scene selected location.

The rewrite is exact, case-sensitive, and field-aware. The implementation must not walk arbitrary JSON strings and replace values recursively.

Cross-authority validation runs against the composed turn. For example, a location cannot claim an NPC as resident while the accepted NPC authority places the actor elsewhere, and a Mortal location cannot bind an afterlife-only entity.

## Atomic Acceptance

One accepted operation follows this order:

1. Validate pre-turn snapshot and all raw location/link carriers without mutation.
2. Build exact identity, receipt, coordinate, hierarchy, topology, and cross-entity indexes.
3. Classify current creation, remote creation, narrow update, movement selection, and link lifecycle operations.
4. Plan permanent identities, receipts, supported reference rewrites, and final canonical objects entirely in memory.
5. Validate the complete composed map, current scene, identity authority, and governed companions.
6. Capture before-images and acquire one bounded write lease for every touched path.
7. Commit canonical map, identity authority, current projection, and governed companion rewrites.
8. Run post-normalization validation before releasing the transaction.
9. Restore every touched path byte-for-byte if any write or post-check fails.

The transaction cannot commit a location without its links/companions when the envelope declares them populated, and cannot leave a link whose endpoint or identity entry failed.

## Validation Invariants

- All canonical locations and links carry valid immutable materialization evidence.
- Every active location/link identity is unique and matches the client identity authority.
- `initialId`, materialization IDs, permanent IDs, and receipt IDs are never reused, including case and Unicode-confusable variants.
- The GM never authors client-owned identity, receipt, seal, index, request, or session evidence.
- Exactly one creation carrier owns each new location.
- Parent graphs are acyclic and same-realm.
- Coordinate tuples are unique where the canonical contract requires uniqueness.
- Every link has two exact accepted endpoints and explicit direction/access/visibility.
- Discovery state and audience form one allowed pair.
- Current-location shared fields equal the selected map location exactly.
- Existing receipts and original envelopes are immutable; full resends are rejected.
- Hidden/internal authority is absent from player projections.
- Validation and normalization do not invent missing GM semantics.

## Discovery and Player Projection

The canonical map may know more than the player:

- **Hidden / GM-only**: absent from ordinary map, list, details, counts, and actions.
- **Rumored / player-known**: exposes a safe in-world rumor summary but no precise coordinates, full hidden description, closed endpoints, or actionable path.
- **Discovered / player-known**: exposes the permitted discovered-location detail and visible topology.
- **Visited / player-known**: exposes the full permitted player-facing location semantics and current/revisit actions.

Console and browser use the same canonical acceptance and visibility predicate. They may present information differently, but must preserve equivalent semantics and actions.

The recursive player projection removes temporary references, envelope and receipt data, seals, identity-index fields, route evidence, repair objects, file paths, validation details, and agent terminology. A rejected or receipt-less candidate contributes no row, node, count, detail, or action.

## Repair and Rollback

A bounded location repair packet contains:

- one exact `initialId`, permanent location identity, link identity, or unambiguous raw carrier coordinate;
- transition class and creation/update route;
- missing or contradictory section dispositions;
- invalid endpoint, parent, coordinate, discovery, realm, or companion evidence;
- exact raw command targets and safe GM-owned correction rules;
- explicit instruction not to author permanent identities, seals, receipts, or identity-index state.

If identity is missing, ambiguous, historically reused, or conflicts with settled authority, no normal GM repair packet is dispatched. The system rolls back and emits a path-bound operator diagnostic because guessing could repair the wrong world entity.

The pre-turn snapshot remains authority until the corrected composed state passes. A narrated discovery or movement cannot settle twice across repair retries.

## Documentation and Examples

The same change reviews and updates at least:

- `Rules/Block_20.txt`;
- `Examples/E_Block_20.txt`;
- `CLI_API_Specification.md`;
- `TaskGuides/CLI_Step_Main.txt`;
- `Examples/E_CLI_Step_Main.txt`;
- daemon and CLI operation guidance that tells the GM what to read and author;
- `Examples/example_validation_manifest.json`;
- documentation and source-guard tests;
- active bootstrap state, positive fixtures, and helper-generated locations.

Required worked evidence:

1. A complete visited starting location, complete reachable neighbor, and explicit link.
2. A hidden remote location with an explicit one-way route and later reveal transition.
3. An invalid location/link package and its bounded repair without replay or client-owned edits.

This Mortal-only feature does not update `OtherGuides/Afterlife_Contract_Matrix.md` or other afterlife contract documents unless implementation unexpectedly changes an afterlife surface. The final report records the no-update rationale. #1514 owns afterlife location documentation.

## Test Strategy

Implementation follows red-green-refactor:

- contract tests for envelope, receipt, section dispositions, exact identity, immutable fields, and client-owned protection;
- integration tests for current and remote creation routes;
- fresh-bootstrap materialization tests;
- same-turn reference tests for links, parentage, actors, factions, lore, threats, storage, and current selection;
- normalizer and canonical-wrapper consumption tests;
- movement, narrow update, discovery transition, and receipt continuity tests;
- topology tests for directed, one-way, portal, hidden, sealed, isolated, duplicate, dangling, cycle, coordinate, and realm cases;
- atomic rollback and bounded repair matrices for failures before and after identity assignment;
- console/browser canonical-only visibility, discovery privacy, semantic parity, and recursive-internal-field tests;
- movement, weather, interactions, storage, and #1511 item-transition regressions;
- fixture migration inventory and documentation/example/source-guard tests;
- a scaling control that detects quadratic location/link scans;
- one Fast checkpoint, related FullValidation and LifecycleIntegration controls, and one clean-checkout PreMerge at the final candidate commit.

Frontend source changes only if existing typed projections cannot enforce the canonical/visibility contract. Visual redesign is explicitly excluded.

## Explicit Non-Goals

- Shining Abode halls, Chaos Sea Guardian planes, or afterlife navigation.
- Transport or storage entity materialization and capacity enforcement.
- Autonomous living-world projects, politics, events, or GM workers.
- New map artwork, atlas redesign, or broad navigation UX work.
- A separate global `location_core.json` registry.
- General rewrites of NPC, faction, item, lore, quest, or threat materialization.
- Compatibility readers, promotion classifiers, migration commands, or runtime support for receipt-less development saves.
- Natural-language inference of location ownership, topology, discovery, or mechanical authority.

## Design Self-Review

- One durable semantic authority and one topology authority are unambiguous.
- Current-scene operational data is separated from durable location semantics.
- Current and remote creation routes cannot both own the same new location.
- Bootstrap exercises ordinary creation and has no compatibility exception.
- Exact identity, link direction, discovery privacy, and same-turn rewrite boundaries are explicit.
- Storage metadata and item contents remain under separate authorities.
- Atomic acceptance, rollback, and bounded repair cover all touched surfaces.
- Player projections fail closed and do not leak hidden world or harness internals.
- #1514 and #1515 boundaries remain explicit.
- No unresolved product decisions, placeholders, or legacy-promotion requirements remain.
