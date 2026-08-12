# Implementation Plan: Complete Mortal Location Materialization

**Branch**: `1520-complete-location-materialization` | **Date**: 2026-08-12 | **Spec**: [spec.md](spec.md)

**Input**: Approved feature specification from `specs/1520-complete-location-materialization/spec.md`.

## Summary

Make `game_state/world/world_map.json` the only durable Mortal location and
topology authority. Complete GM-authored locations enter through exactly one
current-scene or remote creation route with a versioned envelope. The client
assigns permanent location and link identities, seals immutable receipts,
updates a client-owned identity index, and derives `current_location.json` from
the selected canonical map location plus current-scene operational data. Raw
validation, field-aware reference planning, normalization, composed-state
validation, writes, and rollback share one accepted-turn transaction. Obsolete
receipt-less fixtures and compatibility readers are removed because the game
has not been released.

## Technical Context

**Language/Version**: C# 12 on .NET 8; PowerShell 7 for bounded verification; existing React/Vite/TypeScript browser frontend only changes if its current DTO cannot express the safe canonical projection.

**Primary Dependencies**: Existing `ValidationService`, `CanonicalStateNormalizer`, `FileSystemManager`, `PendingTurnSnapshotAuthority`, `AcceptedTurnCanonicalStateRefresh`, `StateDistributor`, `LocalMapViewService`, `LocalInteractionScopeService`, `MortalItemRouteAuthorityCatalog`, xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1, and `System.Text.Json` / `JsonNode`.

**Storage**: File-backed JSON. `world_map.json.locations[]` owns durable location semantics, `world_map.json.links[]` owns topology, `current_location.json` is a validated projection plus scene-local operational state, and `game_state/world/location_identity_index.json` is client-owned identity and lineage authority.

**Testing**: Test-first xUnit contract/unit/integration coverage through `scripts/test-csharp.ps1`; documentation/source guards; one Fast checkpoint; FullValidation and LifecycleIntegration for the affected documentation and accepted-turn boundaries; one final PreMerge.

**Target Platform**: Local Windows console/browser game client on .NET 8; production rules remain portable .NET code and offline-capable.

**Project Type**: Local console/browser game-client repository with a C# runtime, separate fast and integration test projects, file-backed GM contracts, and a React/Vite browser client.

**Performance Goals**: Build identity, coordinate, parent, link-endpoint, route, and cross-reference indexes in one pass; doubling a representative location/link population must remain at or below 2.5x measured validation work; ordinary map and location views remain interactive.

**Constraints**: No public-save compatibility; no receipt-less promotion; exact case-sensitive identity; no name identity or inferred reverse links; no semantic invention by the normalizer; one durable semantic map authority; one accepted-turn write lease and byte-exact rollback; no afterlife location implementation; no transport/storage entity materialization; no network multiplayer.

**Scale/Scope**: Two first-creation routes plus bootstrap reservation, location/link lifecycle operations, current projection, map/movement/locality readers, actor/faction/lore/threat/storage references, same-turn current-location item storage, active repository fixtures/examples, and GM-facing Mortal location documentation.

**Source Issue(s)**: [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)

**Contract Scope**: Player-facing map/location projections; GM-facing prompts/rules/examples; runtime canonical state; client-owned identity authority; validation/normalization; bootstrap; movement/locality; repair/rollback; console/browser parity; no visual redesign.

**Verification Commands**:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationMaterializationContractTests|FullyQualifiedName~MortalLocationIdentityStateTests|FullyQualifiedName~MortalLocationPlayerProjectionTests|FullyQualifiedName~LocalMapViewerServiceTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~ConsoleTrainingCommandTests|FullyQualifiedName~TrainingServiceTests|FullyQualifiedName~TrainingWebCommandServiceTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests|FullyQualifiedName~CanonicalStateNormalizerTests|FullyQualifiedName~MortalBootstrapValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~NpcCoreChangesTests|FullyQualifiedName~ActorMaterializationValidationTests|FullyQualifiedName~FactionMaterializationValidationTests|FullyQualifiedName~MortalItemMaterializationValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~GameEngineTurnLifecycleTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~ExampleDocumentationValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

`FullValidation` is required because the shared example manifest and validation
documentation change. `LifecycleIntegration` is required because bootstrap,
accepted-turn normalization, repair, snapshot, and rollback change. Do not run
another Fast immediately before PreMerge.

## Constitution Check

*GATE before research: PASS. Re-check after Phase 1 design: PASS.*

- **GitHub traceability**: Every planned implementation, fixture, test, prompt, and contract change is tied to #1513; the spec, plan, tasks, and implementation plan link it.
- **Spec Kit fit**: The feature changes canonical state, validation, normalization, bootstrap, repair, movement, player projections, GM contracts, and examples across multiple sessions and files.
- **Player-facing integrity**: Console and browser consume the same accepted discovery-aware projection, retain Russian in-world terminology, and hide temporary references, envelopes, receipts, identity indexes, paths, repair data, and validation vocabulary.
- **Contract/state authority**: The GM owns complete location/link semantics and creation envelopes. The client owns permanent IDs, receipts, seals, the identity index, derived exits, and current projection reconciliation. Rules, CLI guidance, examples, manifest, daemon reminders, and source guards change together.
- **Test-first path**: Contract/identity tests go red first; route and cross-reference tests precede normalizers; repair/rollback and projection tests precede their production changes; fixture and documentation migrations follow executable authority.
- **Verification evidence**: Bounded fast/integration filters, one Fast checkpoint, FullValidation, LifecycleIntegration, fixture/docs guards, manual projection inspection, and one final PreMerge are specified.
- **Agent orchestration**: The active Codex session follows the approved Spec Kit and Superpowers artifacts. No delegated report substitutes for diff inspection and fresh verification.
- **Pre-release save policy**: Receipt-less positive fixtures and compatibility aliases are removed or migrated. Explicit malformed fixtures remain only as labeled negative validation inputs.

## Project Structure

### Documentation (this feature)

```text
specs/1520-complete-location-materialization/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── mortal-location-materialization-envelope.md
│   ├── mortal-location-identity-authority.md
│   ├── mortal-location-routes-and-topology.md
│   ├── mortal-location-repair-packet.md
│   └── mortal-location-player-projection.md
└── tasks.md

docs/superpowers/
├── specs/2026-08-12-mortal-location-materialization-design.md
└── plans/2026-08-12-mortal-location-materialization.md
```

### Source Code

```text
BookOfEternityClient/
├── Services/
│   ├── MortalLocationMaterializationContract.cs
│   ├── MortalLocationIdentityState.cs
│   ├── MortalLocationAcceptedTurnPlan.cs
│   ├── MortalLocationPlayerProjection.cs
│   ├── LocalMapViewService.cs
│   ├── LocalInteractionScopeService.cs
│   ├── MortalItemRouteAuthorityCatalog.cs
│   ├── CanonicalStateNormalizer.cs
│   ├── CanonicalStateNormalizer/
│   │   ├── CanonicalStateNormalizer.MortalLocations.cs
│   │   └── existing NPC/faction/item partials with field-aware location reference integration
│   └── Validation/
│       ├── GameStateValidationPhase.cs
│       ├── ValidationService.ValidationPhases.cs
│       └── ValidationService.MortalLocationMaterialization.cs
├── Core/GameEngine/
│   ├── GameEngine.TurnLifecycle.cs
│   ├── GameEngine.SessionAndSnapshots.cs
│   └── GameEngine.ValidationAndRepair.cs
├── UI/ existing console map/location readers
└── WebUi/ existing browser map/location DTO builder

BookOfEternityClient.Tests/
├── MortalLocationMaterializationContractTests.cs
├── MortalLocationIdentityStateTests.cs
├── MortalLocationPlayerProjectionTests.cs
├── LocalMapViewerServiceTests.cs
└── PromptDocumentationCoverageTests.cs

BookOfEternityClient.IntegrationTests/
├── MortalLocationMaterializationValidationTests.cs
├── CanonicalStateNormalizerTests.MortalLocations.cs
├── MortalBootstrapValidationTests.cs
├── ExplorerModeCommandTests.GeneralPanels.cs
├── ExplorerWebCommandServiceTests.cs
├── GameEngineTurnLifecycleTests.cs
├── FileSystemExampleFixtureIntegrityTests.cs
└── ExampleDocumentationValidationTests.cs

Rules/, TaskGuides/, Examples/, FileSystemExample/,
CLI_API_Specification.md, CLI_Agent_Daemon_Specification.md,
and BookOfEternityClient/game_master_daemon.ps1
```

**Structure Decision**: Keep durable semantics and topology in the established
world-map file, and add a small shared contract/planner/index/projection layer
under `Services`. Add one validation partial and one normalizer partial rather
than scattering identity rules among map readers. Preserve existing console and
browser commands while replacing their raw/wrapper/name-based inputs with one
safe canonical projection. Do not add a second semantic location registry.

## Phase 0: Research Outcomes

Research decisions and rejected alternatives are recorded in
[research.md](research.md). The implementation-critical findings are:

1. Fresh bootstrap currently writes a pseudo-playable start and neighbor. It must instead reserve exact references, permanent-ID slots, coordinates, and materialization requests in the scaffold while canonical map/current roots remain neutral until the first accepted GM result.
2. `StateDistributor` stores `currentLocationData` and `worldMapUpdates` as transient wrappers. The location normalizer must consume them into exact `locations[]` and `links[]`, then remove all command wrappers from durable state.
3. Existing validators and readers recursively accept location aliases, names, coordinate-based endpoints, `knownExits`, `adjacencyMap`, and case-insensitive IDs. Those paths must be replaced rather than retained as compatibility fallbacks.
4. Accepted actors and factions currently use their accepted effective identity without a client-side remap. The location plan resolves their same-turn references exactly but does not redesign actor/faction identity in #1513.
5. Items already receive client-owned permanent IDs. The location normalizer therefore runs before item materialization, preserves raw current-storage contents, and makes accepted same-turn current-location storage authority available to the item route catalog.
6. `AcceptedTurnCanonicalStateRefresh` already owns a lease, before-images, normalization, post-check, and rollback. Adding world map/current/index and combined location/item post-validation extends that transaction instead of creating a second writer.
7. Existing map, location, news, locality, training, trade, storage, actor, and faction consumers derive authority from wrappers or names. They require exact canonical adapters and regression coverage.

## Phase 1: Design Outcomes

- [data-model.md](data-model.md) defines canonical location/link roots, envelopes, receipts, identity authority, current projection, discovery, and cross-reference invariants.
- [contracts/mortal-location-materialization-envelope.md](contracts/mortal-location-materialization-envelope.md) defines complete GM-authored location/link creation packages and governed section dispositions.
- [contracts/mortal-location-identity-authority.md](contracts/mortal-location-identity-authority.md) defines exact permanent identity, sealed receipts, history, lifecycle, and anti-replay rules.
- [contracts/mortal-location-routes-and-topology.md](contracts/mortal-location-routes-and-topology.md) defines creation, bootstrap, movement, narrow update, discovery, and directed link lifecycle routes.
- [contracts/mortal-location-repair-packet.md](contracts/mortal-location-repair-packet.md) defines bounded repair targeting, protected-state exclusions, and fail-closed rollback.
- [contracts/mortal-location-player-projection.md](contracts/mortal-location-player-projection.md) defines discovery tiers, safe rumor data, canonical-only readers, and console/browser parity.
- [quickstart.md](quickstart.md) gives authoring, rejection, repair, projection, and verification examples.

## Implementation Strategy

1. Add contract-only red tests for complete sections, exact identities, discovery pairs, link endpoints, receipts, and client-owned protection.
2. Add the exact location/link identity state and one-pass pre-turn/raw indexes, including case, whitespace, Unicode, coordinate, parent-cycle, and historical replay rejection.
3. Add raw route classification and a pure `MortalLocationAcceptedTurnPlan` that plans IDs, receipts, canonical map/current objects, exact supported reference rewrites, and link transitions without mutating files.
4. Run location planning/normalization before Mortal item normalization. Preserve current-storage item carriers, expose only accepted same-turn storage route authority to #1511, then normalize items and remaining accumulated state.
5. Extend the existing accepted-turn lease, before-image list, post-validation phase, and rollback to world map, current projection, identity index, and governed cross-reference files.
6. Add movement, narrow location update, discovery transition, and exact link lifecycle operations; remove trusted GM `knownExits`/`adjacencyMap` and derive them from links.
7. Replace map/current/locality/training/trade/news/NPC/faction readers with exact canonical authority and one shared discovery-aware player projection.
8. Add bounded repair packets and prove replay, ambiguity, client-owned target, and post-seal failure all stop before GM dispatch or roll back byte-for-byte.
9. Migrate bootstrap, active fixtures, helper-generated locations, prompts, docs, three worked examples, manifest, and source guards; remove receipt-less compatibility expectations.
10. Run bounded focused controls, one Fast checkpoint, conditional broad controls, and one clean-candidate PreMerge before integration.

## Complexity Tracking

No constitution exception is required. `location_identity_index.json` is a
client-owned uniqueness and lineage authority, not a second semantic location
store. The accepted-turn plan is a pure transaction plan shared by validation
and normalization boundaries, not a new persistence layer.
