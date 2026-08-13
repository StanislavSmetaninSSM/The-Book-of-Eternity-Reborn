# Contract: Mortal Location Player Projection

**Feature**: [Complete Mortal Location Materialization](../spec.md)
**Source issue**: [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)

## Purpose

Define the single discovery-aware, canonical-only projection used by Mortal map,
location, movement, locality, training, trade, storage, NPC, faction, and news
player surfaces. Console and browser may differ in layout, not in authority,
visibility, semantics, or actions.

## Accepted input

A location is eligible only when:

- it occurs exactly once in canonical `world_map.locations[]`;
- it has an exact permanent `locationId`;
- its envelope, receipt, seal, and active identity-index entry agree;
- it passes current-schema materialization and semantic validation;
- its discovery pair permits the requested player projection.

A link is eligible only under the analogous link rules and when both accepted
endpoints resolve exactly once. A current-location projection is eligible only
when all shared fields equal its selected map object.

Receipt-less, raw, command-wrapper, duplicate, name-only, case-variant,
confusable, orphan, or malformed candidates contribute nothing—not even counts,
labels, fallback nodes, or disabled actions.

## Discovery projection

### Hidden / GM-only

The entity is absent from:

- map nodes and edges;
- location lists, search results, counts, and detail routes;
- current/remote navigation actions;
- NPC/faction/location summaries;
- news/threat/location joins;
- storage, training, trade, and locality choices.

Its name, coordinates, description, parent, link endpoints, and existence are not
represented by placeholders.

### Rumored / player-known

Allowed fields:

- a safe player-facing rumor label;
- non-empty `rumorSummary`;
- a broad region only when the contract marks it rumor-safe;
- non-actionable rumor status.

Forbidden fields include permanent and temporary IDs, exact coordinates, full
description, image prompt, parent, detailed mechanics/difficulty, inhabitants,
faction ownership, storage, threats, lore, hidden/sealed endpoints, and movement
actions.

### Discovered / player-known

Allowed fields include presentation, physical setting, permitted region,
difficulty/known threats, discovered lore and occupants, and links whose own
discovery/access permits display. Exact coordinates are shown only where the
existing player map requires them and the discovery contract permits them.

### Visited / player-known

Expose all accepted player-facing semantics, current/revisit state, and valid
actions. Operational current weather, interactions, chronology, and accepted
current storage contents come only from the validated current projection.
Offscreen storage contents never create a location row, count, detail, or
action and are not an alternate player projection source.

## Semantic projection

The shared projector emits explicit in-world fields for:

- name/display name, purpose, description;
- outdoor biome or indoor type and features;
- player-permitted placement and parent context;
- both difficulty profiles in player terminology;
- chronology and current operational scene state;
- faction control and actor roles resolved through exact accepted authority;
- storage metadata and accepted item projection for current contents;
- threats and lore references resolved to safe player labels/details;
- setting-specific custom state through recursive sanitization;
- visible directed exits, access state, direction, travel mode, and blocked reason.

Unknown setting-specific semantic scalar/array/object fields are preserved after
recursive sanitization when the containing governed schema permits them. The
projector does not reduce all locations to a fixed fantasy setting vocabulary.

## Internal fields and DTO shapes

The projection recursively removes item/location/link/actor/faction authority
fields and complete internal DTO shapes, including:

- `initialId`, materialization IDs, envelopes, receipts, seals, creation/source
  authority, request/session/reservation/route evidence;
- identity-index entries, lifecycle transitions, carrier coordinates, pending
  snapshot data, repair packets, validation issues, file paths, raw command
  wrapper names, and protocol collections;
- the complete closed offscreen location-storage contents DTO, including its
  schema, coordinates, raw item arrays, receipts, and any annotated copy;
- internal image-generation prompts (the player may receive an in-world visual
  description, not raw `image_prompt`);
- exact hidden endpoint or permanent selector data not needed by the rendered UI.

Sanitization is context-aware. A normal in-world object with a legitimate field
named `route`, `state`, `kind`, `title`, or `steps` remains visible outside a
recognized authority/repair DTO shape. A complete or annotated authority DTO is
suppressed as a whole, including generic residual fields.

## Exact action binding

Internal commands retain exact permanent IDs in non-rendered payloads. Visible
labels never expose them. Selection resolution follows:

1. exact permanent ID carried by the generated action;
2. no name, slug, ordinal, case-insensitive, whitespace, or Unicode fallback;
3. revalidate visibility, access, current reachability, and canonical receipt at
   write time;
4. fail with a player-safe Russian message if state changed.

Numeric or name-like IDs still receive exact identity priority. Duplicate names
are disambiguated with in-world context, not by printing IDs.

## Current scene and derived exits

The current panel reads the validated current projection. Map/list/detail reads
canonical locations and exact links. Both use the same discovery predicate and
same derived directed-edge set.

`knownExits` and `adjacencyMap`, if retained as internal compatibility fields for
current consumers, are never trusted inputs and are not recursively exposed as
raw JSON. A blocked visible link shows an in-world block reason without revealing
a hidden target. No reverse action is invented.

## Cross-surface consumers

The accepted projection or its internal exact authority view is used by:

- `LocalMapViewService`;
- console location list/detail/map and ASCII map;
- browser location/map/current DTOs;
- Mortal world news location/threat summaries;
- movement and local interaction scope;
- training and NPC trade locality;
- NPC/faction location display and authority checks;
- current-location storage panels;
- actor memory/current-scene matching.

Authority-only consumers may retain permanent IDs internally but use the same
exact accepted set. They may not widen the set through names or raw wrappers.

## Console/browser parity

For the same canonical state, both clients must agree on:

- which locations and links exist for the player;
- rumor/discovered/visited semantic content;
- current location and visible exits;
- access blocks and movement affordances;
- faction, actor, storage, threat, and lore semantics;
- absence of rejected/internal data.

Visual shape, ordering appropriate to each UI, and concise phrasing may differ.
No frontend visual redesign is required. If existing browser DTOs can carry the
projection, React/CSS source remains unchanged.

## Failure messages

Player errors are generic and in-world. They do not echo permanent IDs, raw file
paths, receipt/index/seal terms, exact route references, validation issue codes,
or repair instructions. Exceptions and service diagnostics are sanitized or
mapped at every console/browser write boundary.

## Required regressions

- hidden entity absent from every player surface and aggregate count;
- rumor shows safe summary only;
- discovered/visited semantics agree between console/browser;
- receipt-less/raw candidate never creates row/node/detail/action;
- nested full envelope/receipt/index/repair/transition/carrier DTO is absent while
  adjacent legitimate semantic objects survive;
- duplicate names do not change exact action target or expose IDs;
- one-way/hidden/sealed links produce exact directed and visibility behavior;
- current projection mismatch fails closed;
- storage contents include only accepted item projections;
- a nested full offscreen storage authority disappears recursively from
  console/browser/news output while adjacent legitimate storage semantics remain;
- locality/training/trade/news cannot resolve a location by name or case variant.
