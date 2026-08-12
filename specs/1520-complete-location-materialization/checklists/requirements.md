# Specification Quality Checklist: Complete Mortal Location Materialization

**Purpose**: Validate specification completeness and quality before proceeding to planning

**Created**: 2026-08-12

**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation-only design choices appear as user requirements
- [x] Focused on player, GM, and canonical-state outcomes
- [x] Written so product decisions and acceptance behavior are reviewable without reading source code
- [x] All mandatory template sections are completed

## Requirement Completeness

- [x] No `[NEEDS CLARIFICATION]` markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions are identified

## Feature Readiness

- [x] All functional requirements have clear acceptance evidence
- [x] User scenarios cover current creation, remote creation, bootstrap, topology, repair, visibility, and cross-entity references
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] Canonical authority, current-scene projection, and link ownership do not conflict
- [x] GM prompts, documentation, worked examples, manifests, fixtures, and source guards are included in scope
- [x] Mortal and afterlife location contracts are separated explicitly

## Notes

- User clarification completed in conversation on 2026-08-12: #1513 and #1514 are sequential features; the canonical world map owns Mortal locations and links; current-location state is a projection plus operational scene data; current and remote creations have exclusive carriers; bootstrap uses ordinary complete materialization; links are explicitly directed; and discovery uses a closed visibility contract.
- Pre-release save compatibility is explicitly not required by the project constitution and direct user instruction. The source issue's legacy-promotion wording is superseded: receipt-less runtime state is rejected and repository fixtures are migrated.
- The specification preserves the existing `initialId` concept for exact same-turn references and rejects name-based identity.
- Storage metadata is location-governed, while storage contents remain governed by the item transition contract.
- Afterlife documentation is deliberately deferred to #1514 because #1513 changes no Chaos Sea or Shining Abode runtime contract.
- No unresolved placeholders, contradictory authority owners, or deferred product decisions remain.
