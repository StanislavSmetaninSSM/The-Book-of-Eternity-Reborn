# Implementation Plan: Complete Mortal Item Materialization

**Branch**: `1511-complete-item-materialization` | **Date**: 2026-08-11 | **Spec**: [spec.md](spec.md)

**Input**: Approved feature specification from `specs/1511-complete-item-materialization/spec.md`.

## Summary

Add one current-schema-only first-materialization boundary for every durable
ordinary Mortal World item. A complete GM-authored item and versioned envelope
enter through an existing route; the client resolves a same-turn `creationRef`,
assigns the permanent `itemId`, seals an immutable embedded receipt, and updates
`game_state/inventory/item_identity_index.json`. Exact-key carrier and companion
indexes validate creation, transfer, split, merge, retirement, and route
authority without quadratic scans. Receipt-less development fixtures are
migrated or retained only as explicit negative inputs; no save compatibility or
runtime promotion path is added.

## Technical Context

**Language/Version**: C# 12 on .NET 8; PowerShell 7 for bounded verification; existing TypeScript/React browser client remains unchanged unless projection tests expose a leak.

**Primary Dependencies**: Existing `ValidationService`, `CanonicalStateNormalizer`, `FileSystemManager`, `PendingTurnSnapshotAuthority`, `InventoryManagementService`, `StorageTransportMoveService`, xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1, `System.Text.Json` / `JsonNode`.

**Storage**: File-backed JSON canonical state. Item semantics remain embedded in their active carrier; immutable receipt evidence is embedded in each item; client-owned global identity and lineage authority is stored at `game_state/inventory/item_identity_index.json`.

**Testing**: Test-first xUnit contract/unit/integration coverage through `scripts/test-csharp.ps1`; documentation/source guards; one Fast checkpoint; FullValidation and LifecycleIntegration controls for the touched boundaries; one final clean-candidate PreMerge.

**Target Platform**: Local Windows console/browser game client on .NET 8; production logic remains portable .NET code.

**Project Type**: Local console/browser game-client repository with one C# runtime project, separate fast and integration test projects, file-backed GM contracts, and a React/Vite browser client.

**Performance Goals**: Build carrier, identity, companion, and route lookups in one pass; doubling a representative multi-carrier population must remain at or below 2.5x the measured validation work; ordinary player item operations remain interactive.

**Constraints**: No public-save compatibility; no runtime receipt-less promotion; exact case-sensitive identity; no semantic invention by the normalizer; one active carrier per item; atomic rollback on failed creation or local transition; no afterlife relic implementation; no location/transport/storage entity materialization; no network multiplayer.

**Scale/Scope**: Eight first-creation route classes, player/NPC/location-storage carriers plus continuity through already-valid vehicle carriers, existing item companion files, direct console/browser inventory actions, active repository fixtures/examples, and GM-facing item documentation.

**Source Issue(s)**: [#1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)

**Contract Scope**: Player-facing inventory projections; GM-facing prompts/rules/examples; runtime canonical state; client-owned identity authority; validation/normalization; repair/rollback; console/browser parity; no visual redesign.

**Verification Commands**:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemMaterializationContractTests|FullyQualifiedName~MortalItemIdentityTransitionTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests|FullyQualifiedName~CanonicalStateNormalizerTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~QuestRewardAuthorityValidationTests|FullyQualifiedName~NpcTradeRequestValidationTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~ExampleDocumentationValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~BrowserInventoryManagementTests|FullyQualifiedName~BrowserStorageTransportParityTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

`FullValidation` is required because the shared example manifest and
validation-documentation boundary change. `LifecycleIntegration` is required
because the accepted-turn snapshot, normalization, repair, and rollback contour
changes. Do not repeat Fast immediately before PreMerge.

## Constitution Check

*GATE before research: PASS. Re-check after Phase 1 design: PASS.*

- **GitHub traceability**: All implementation, tests, contracts, fixtures, and documentation are tied to #1511; the spec, plan, and forthcoming tasks link the issue.
- **Spec Kit fit**: The feature changes canonical item state, validation, normalization, repair, local lifecycle operations, prompts, examples, and player projections across multiple sessions/files.
- **Player-facing integrity**: Existing Russian console/browser inventory flows remain semantically aligned and must hide `materialization`, `materializationReceipt`, identity-index, file-path, and repair terminology outside explicit debug mode.
- **Contract/state authority**: The GM owns item semantics and the envelope; the client owns permanent identity, sealed receipt, index state, and transition lineage. Rules, task guides, CLI docs, worked examples, manifest entries, daemon reminders, and source guards are updated together. No afterlife contract changes are made, so the afterlife matrix/examples require only a checked no-update rationale.
- **Test-first path**: Contract and transition tests are red before runtime code; route/carrier integration tests are red before adapters; repair/projection/docs tests are red before their implementation or fixture migrations.
- **Verification evidence**: Focused fast/integration filters, Fast, FullValidation, LifecycleIntegration, fixture/docs guards, manual console/browser projection inspection, and final PreMerge are listed.
- **Agent orchestration**: Work remains in the current Codex session under the approved Spec Kit and Superpowers artifacts. No agent report substitutes for inspected diffs or fresh test evidence.
- **Pre-release save policy**: Receipt-less positive fixtures are migrated and legacy compatibility branches are forbidden. Explicit malformed fixtures remain only where a negative test labels the expected rejection.

## Project Structure

### Documentation (this feature)

```text
specs/1511-complete-item-materialization/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── mortal-item-materialization-envelope.md
│   ├── mortal-item-identity-authority.md
│   ├── mortal-item-routes-and-transitions.md
│   └── mortal-item-repair-packet.md
└── tasks.md

docs/superpowers/
├── specs/2026-08-11-item-materialization-design.md
└── plans/2026-08-11-item-materialization.md
```

### Source Code

```text
BookOfEternityClient/
├── Services/
│   ├── MortalItemMaterializationContract.cs
│   ├── MortalItemIdentityState.cs
│   ├── MortalItemCarrierCatalog.cs
│   ├── InventoryManagementService.cs
│   ├── StorageTransportMoveService.cs
│   ├── CanonicalStateNormalizer.cs
│   ├── CanonicalStateNormalizer/
│   │   └── CanonicalStateNormalizer.MortalItems.cs
│   └── Validation/
│       ├── GameStateValidationPhase.cs
│       ├── ValidationService.ValidationPhases.cs
│       └── ValidationService.MortalItemMaterialization.cs
├── Core/GameEngine/
│   ├── GameEngine.SessionAndSnapshots.cs
│   └── GameEngine.ValidationAndRepair.cs
└── UI/ and WebUi/ existing allowlisted item projections (edit only if privacy tests fail)

BookOfEternityClient.Tests/
├── MortalItemMaterializationContractTests.cs
├── MortalItemIdentityTransitionTests.cs
├── PromptDocumentationCoverageTests.cs
└── WebUi/ existing inventory/storage parity tests

BookOfEternityClient.IntegrationTests/
├── MortalItemMaterializationValidationTests.cs
├── CanonicalStateNormalizerTests.Inventory.cs
├── ExplorerModeCommandTests.TradeAndInventory.cs
├── ExplorerWebCommandServiceTests.cs
├── QuestRewardAuthorityValidationTests.cs
├── NpcTradeRequestValidationTests.cs
├── FileSystemExampleFixtureIntegrityTests.cs
└── ExampleDocumentationValidationTests.cs

Rules/, TaskGuides/, Examples/, FileSystemExample/,
CLI_API_Specification.md, CLI_Agent_Daemon_Specification.md,
and the daemon prompt entrypoint
```

**Structure Decision**: Keep current full item objects in their established
carriers and add a small shared contract/catalog/index layer under `Services`.
Add one validation partial and one normalizer partial rather than spreading
identity rules across route validators. Reuse existing console/browser readers
through allowlisted semantic projection; do not add a new frontend store or a
reference-only item registry.

## Phase 0: Research Outcomes

Research decisions and rejected alternatives are recorded in [research.md](research.md).
The critical implementation observations are:

1. `UpdateInventory` currently remains a command-shaped property in
   `items.json`; item normalization must project it into canonical `items[]`.
2. New-NPC inventory and `NPCInventoryAdds` already carry complete item objects,
   but current permanent-ID assignment is incomplete and route-local.
3. Loot/drop is a route, not an independent durable carrier. Accepted loot must
   end in player, NPC, or existing storage authority. The current local Drop
   command is a destroy/retire transition.
4. Storage and vehicle moves physically transfer the same JSON node under a
   canonical write lease. Their item identity continuity is in scope; materializing
   the storage/vehicle entity itself remains #1515.
5. The accepted-turn normalizer already receives validated pre-turn backups and
   its tracked-file lists already feed pending snapshots, QTE rollback, and
   browser transactions. `RefreshCanonicalStateAsync` does not currently bind
   that normalization call to a canonical write lease, so #1511 must acquire
   one lease, bind the normalizer, and retain exact before-images for all
   carrier/companion/index writes through post-seal validation. Adding the
   index there then extends one coordinated rollback contour rather than
   introducing a second transaction system.
6. Player-facing item readers generally select known fields; explicit privacy
   tests determine whether any projection source needs changing.

## Phase 1: Design Outcomes

- [data-model.md](data-model.md) defines envelope, receipt, index, carrier,
  route authority, and transition invariants.
- [contracts/mortal-item-materialization-envelope.md](contracts/mortal-item-materialization-envelope.md)
  defines the GM-authored creation package and governed section map.
- [contracts/mortal-item-identity-authority.md](contracts/mortal-item-identity-authority.md)
  defines client-owned receipt/index ownership and exact identity rules.
- [contracts/mortal-item-routes-and-transitions.md](contracts/mortal-item-routes-and-transitions.md)
  defines route adapters, transfers, splits, merges, crafting, discard, and
  already-valid storage/vehicle continuity.
- [contracts/mortal-item-repair-packet.md](contracts/mortal-item-repair-packet.md)
  defines bounded repair targeting and replay protection.
- [quickstart.md](quickstart.md) provides authoring and verification examples.

## Implementation Strategy

1. Add contract-only red tests and immutable schema helpers.
2. Add exact-key carrier/index discovery and fail-closed current-state validation.
3. Add pre-seal raw creation validation, then run normalizer projection,
   permanent ID assignment, reference resolution, receipt sealing, and index
   transition under one bound `CanonicalWriteLease` with exact before-image
   restoration on any normalization or post-seal validation failure.
4. Cover each route adapter and companion authority with atomic negative tests.
5. Route client-side drop/split/merge/storage/vehicle operations through one
   coordinated item transition writer under the canonical lease.
6. Add narrow item repair packets and accepted-turn rollback/idempotence tests.
7. Prove console/browser privacy, then migrate repository fixtures and synchronize
   prompts, docs, examples, manifest, and guards.
8. Run the bounded controls and final clean-candidate PreMerge before integration.

## Complexity Tracking

No constitution violation requires an exception. The client-owned identity
index is justified by cross-carrier uniqueness and lineage requirements; it is
not a second semantic item registry and does not replace carrier-owned item data.
