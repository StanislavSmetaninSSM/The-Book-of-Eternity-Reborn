# Implementation Plan: Complete Effect Materialization

**Branch**: `1535-effect-materialization` | **Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

**Input**: Approved feature specification from `specs/1535-complete-effect-materialization/spec.md`.

## Summary

Replace the unrelated Mortal player/NPC/combat and afterlife active-effect write paths with one setting-agnostic materialization boundary. The GM requests `apply`, `dispel`, or `remove` through a transient `effectChanges[]` command. The client resolves exact source and target authority, assigns permanent effect and transition identities once, applies source-owned stacking and lifetime rules, schedules deterministic lifecycle work, validates the complete composed effect set, and publishes distributed owner carriers plus the client-owned identity index atomically through the one common `AcceptedMechanicsPlan`. Deferred QTE terminal resource producers capture their effect continuation when the offer is accepted and later resume the same planner from that immutable authority; they never rebuild from live post-turn files. Static `combatEffect`, `structuredBonuses`, Fate Card, item, skill, wound, quest, location, event, and spiritual-art definitions remain source templates. No runtime migration or legacy compatibility path is added.

## Technical Context

**Language/Version**: C# 12 on .NET 8; PowerShell 7 for bounded local verification; existing React/Vite/TypeScript frontend changes only if current generic DTO rendering cannot express the safe effect projection.

**Primary Dependencies**: Shared canonical resource authority [#1543](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543), whose US4/T085 slice supplies Task 9 trigger/resource/pending execution, the sole accepted publication plan, and the direct QTE terminal resource producer; existing `ValidationService`, `CanonicalStateNormalizer`, `FileSystemManager`, `PendingTurnSnapshotAuthority` for ordinary accepted turns only, `SessionOperationContext`, `QteSceneService`, a dedicated deferred-QTE receipt coordinator/daemon transport, the ordinary accepted-turn repair/rollback loop for non-QTE work, `StateDistributor`, `CharacteristicsService`, `AfterlifeSpiritualConflictState`, `AfterlifeEntityProfileState`, existing item/location/actor/faction materialization plan authorities, `MortalItemPlayerProjection` technical DTO suppression patterns, Spectre.Console, xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1, and `System.Text.Json` / `JsonNode`.

**Storage**: File-backed JSON. Transient commands stage in `game_state/effects/effect_commands.json`; active instances remain in `game_state/player/effects.json`, `game_state/npcs/npc_effects.json`, accepted Mortal combatant `activeBuffs`/`activeDebuffs`, accepted afterlife profile `activeEffects[]`, or active spiritual-conflict `combatConditions[]`; `game_state/effects/effect_identity_index.json` is client-owned identity/history authority; `game_state/control/pending_effect_resolutions.json` is client-owned bounded GM-work authority. Deferred QTE authority persists in `game_state/control/qte_deferred_effect_continuation.json`; its GM receipt transport is `input/qte_effect_resolution_request.json`, `output/qte_effect_resolution_receipts.json`, and ready-last `ready/qte_effect_resolution_complete.json`.

**Testing**: Test-first xUnit unit/contract/integration coverage through `scripts/test-csharp.ps1`; documentation/source guards; failure-injection lifecycle tests; one meaningful Fast checkpoint; FullValidation because shared Mortal/afterlife docs/examples/manifests change; LifecycleIntegration because accepted-turn/afterlife transaction, pending control, rollback, repair, and stale output change; one final PreMerge.

**Target Platform**: Local Windows console/browser game client on .NET 8; production contracts remain portable .NET code and offline-capable.

**Project Type**: Local console/browser game-client repository with a C# runtime, separate fast and integration test projects, file-backed GM contracts, and a React/Vite browser client.

**Performance Goals**: Build source, target, owner, stack, event, and identity indexes once per accepted plan; doubling a representative effect/component/trigger population must remain at or below 2.5x measured validation/lifecycle work; ordinary status and actor views remain interactive.

**Constraints**: No public-save compatibility; no legacy effect promotion; no GitHub Actions; exact case-sensitive authority with confusable/historical detection; client-generated IDs once per plan; no prose mechanics or duration sentinels; source-owned stack/lifetime policy; deterministic phase order; one accepted transaction and byte/existence-exact rollback; deferred QTE effects require an acceptance-time immutable continuation and may not use live rebuild, bootstrap/random fallback, the ordinary turn request, a synthetic pending-turn snapshot, or the ordinary validation-repair loop; no wound healing through effect removal; no general combat/source-entity rematerialization.

**Scale/Scope**: Nine registered component profiles, five stacking policies, eight lifetime modes, player/NPC/combatant/afterlife-profile/spiritual-conflict carriers, representative skill/art/item/wound/quest/location/hazard/faction/event/Fate Card/combat sources, ordinary and deferred-QTE resource-event producers, bounded multi-wave trigger resolution, player projection, GM docs/examples, and local verification.

**Source Issue(s)**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535); blocking foundation [#1543](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543)

**Contract Scope**: Player-facing console/browser; GM-facing rules/prompts/examples; Mortal and afterlife runtime state; validation/normalization; identity/source/target authority; deterministic lifecycle; derived mechanics; pending resolution; repair/rollback; documentation/manifests/source guards; no visual redesign unless required by an existing typed frontend boundary.

**Afterlife documentation boundary**: The dedicated deferred-QTE receipt transport is Mortal-only and changes no Chaos Sea/Shining Abode action, pending/control, response, scheduler, or authority contract, so it requires no QTE-specific edit to `OtherGuides/Afterlife_Contract_Matrix.md`. The feature's separate shared effect/afterlife adapter changes remain covered by T098/T105/T108 and their required examples/guards.

**Verification Commands**:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectMaterializationContractTests|FullyQualifiedName~EffectSourceDefinitionContractTests|FullyQualifiedName~EffectIdentityStateTests|FullyQualifiedName~EffectSourceAuthorityTests|FullyQualifiedName~EffectTargetAuthorityTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectAcceptedTurnPlannerTests|FullyQualifiedName~EffectLifecycleSchedulerTests|FullyQualifiedName~EffectMechanicsSnapshotTests|FullyQualifiedName~CharacteristicsServiceTests|FullyQualifiedName~EffectPlayerProjectionTests|FullyQualifiedName~EffectRepairPacketBuilderTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~EffectMaterializationValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalStateNormalizerEffectTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectMaterializationLifecycleTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectResourceTriggerRoutingScaleTests|FullyQualifiedName~AcceptedMechanicsPlannerScaleTests|FullyQualifiedName~PendingCausalAuthorityScaleTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~AfterlifeSpiritualConflictValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeEntityProfileValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectAfterlifeAdapterTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 8 -Filter "FullyQualifiedName~ExplorerModeCommandTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 15 -Filter "FullyQualifiedName~ExplorerWebCommandServiceTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectMaterializationRepairLifecycleTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~QteSceneServiceTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~QteDeferredEffectContinuationIntegrationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~BrowserQteGenerationFencingTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~GmTurnHelperContractTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration -TimeoutMinutes 30
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

`FullValidation` is required because shared GM examples/manifests and validation-documentation boundaries change. `LifecycleIntegration` is required because accepted-turn/afterlife planning, pending resolution, repair, snapshot, rollback, and stale-output boundaries change. Do not run another Fast immediately before PreMerge.

## Constitution Check

*GATE before research: PASS. Re-check after Phase 1 design: PASS.*

- **GitHub traceability**: Issue #1535 is open, triaged P1, and linked from the approved Superpowers design, spec, plan, tasks, contracts, data model, quickstart, and implementation plan.
- **Spec Kit fit**: The feature spans canonical state, validation, normalization, identity, lifecycle, derived mechanics, afterlife, console/browser parity, GM contracts, repair, and multiple sessions/files.
- **Player-facing integrity**: One accepted projection supplies both clients; visible mechanics and actions remain in-world Russian, hidden/GM-only effects stay absent, and internal IDs/routes/receipts/repair/agent data are recursively suppressed.
- **Contract/state authority**: Sources own materializable definitions; targets own actor identity; the client owns active effect identity, stack/lifetime result, scheduler, history, identity index, pending requests, and repair bounds; relevant Mortal and afterlife prompts/docs/examples/tests change together.
- **Test-first path**: Contract/identity/source/target RED tests precede planner code; lifecycle/afterlife/mechanics RED tests precede integration; repair/rollback and projection privacy tests precede their production changes; docs/source guards precede documentation migration.
- **Verification evidence**: Bounded focused controls, one Fast checkpoint, FullValidation, LifecycleIntegration, failure-injection matrix, manual console/browser checks, and one final PreMerge are specified. GitHub Actions remain disabled.
- **Agent orchestration**: The active Codex session follows the approved Spec Kit and Superpowers artifacts; no agent report substitutes for local diff inspection and fresh verification.
- **Pre-release save policy**: Active bootstrap/templates/examples/tests are rewritten to the current schema. Non-empty legacy effect carriers are rejected, and no migration or compatibility branch is planned.

## Project Structure

### Documentation (this feature)

```text
specs/1535-complete-effect-materialization/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── checklists/
│   └── requirements.md
├── contracts/
│   ├── effect-command-and-envelope.md
│   ├── effect-identity-source-target-authority.md
│   ├── effect-stacking-and-lifecycle.md
│   ├── effect-afterlife-adapter.md
│   ├── effect-atomic-repair-and-rollback.md
│   └── effect-player-projection.md
└── tasks.md

docs/superpowers/
├── specs/2026-08-14-effect-materialization-design.md
└── plans/2026-08-14-complete-effect-materialization.md
```

### Source Code

```text
BookOfEternityClient/
├── Configuration/FileMapping.cs
├── Models/GameResponse.cs
├── Services/
│   ├── EffectMaterializationContract.cs
│   ├── EffectSourceDefinitionContract.cs
│   ├── EffectComponentProfiles.cs
│   ├── EffectIdentityState.cs
│   ├── EffectCarrierCatalog.cs
│   ├── CombatantIdentityState.cs
│   ├── EffectSourceAuthority.cs
│   ├── EffectTargetAuthority.cs
│   ├── EffectAcceptedTurnPlan.cs
│   ├── EffectAcceptedTurnPlanner.cs
│   ├── EffectAcceptedTurnPlanCache.cs
│   ├── EffectLifecycleScheduler.cs
│   ├── ResourcePendingResolutionState.cs
│   ├── AcceptedMechanicsPlan.cs
│   ├── AcceptedMechanicsPlanCache.cs
│   ├── AcceptedMechanicsPlanner.cs
│   ├── EffectMechanicsSnapshot.cs
│   ├── EffectRepairPacketBuilder.cs
│   ├── CharacteristicsService.cs
│   ├── AfterlifeSpiritualConflictState.cs
│   ├── AfterlifeEntityProfileState.cs
│   ├── CanonicalStateNormalizer.cs
│   ├── CanonicalStateNormalizer/
│   │   ├── CanonicalStateNormalizer.Effects.cs
│   │   ├── CanonicalStateNormalizer.AfterlifeSpiritualConflict.cs
│   │   └── CanonicalStateNormalizer.AfterlifeEntityProfiles.cs
│   └── Validation/
│       ├── ValidationService.ValidationPhases.cs
│       ├── ValidationService.EffectMaterialization.cs
│       ├── ValidationService.PlayerAndInventory.cs
│       ├── ValidationService.NpcWorldAndMeta.cs
│       ├── ValidationService.QuestsRivalsFactionsAndWorld.cs
│       ├── ValidationService.AfterlifeCombatConditions.cs
│       └── source-owning validators that invoke the shared definition contract
├── Core/GameEngine/
│   ├── GameEngine.TurnLifecycle.cs
│   ├── GameEngine.SessionAndSnapshots.cs
│   └── GameEngine.ValidationAndRepair.cs
├── UI/
│   ├── EffectPlayerProjection.cs
│   ├── ExplorerMortalEffectDetailActions.cs
│   ├── ExplorerMortalWorldCommandResultBuilder.cs
│   ├── ExplorerAfterlifeCombatCommandResultBuilder.cs
│   └── ExplorerMode/ affected Mortal/NPC/afterlife status readers
└── WebUi/ affected write/action services if cure/dispel forms need exact submit support

BookOfEternityClient.Tests/
├── EffectMaterializationContractTests.cs
├── EffectSourceDefinitionContractTests.cs
├── EffectIdentityStateTests.cs
├── EffectCarrierCatalogTests.cs
├── EffectSourceAuthorityTests.cs
├── EffectTargetAuthorityTests.cs
├── EffectAcceptedTurnPlannerTests.cs
├── EffectLifecycleSchedulerTests.cs
├── EffectMechanicsSnapshotTests.cs
├── EffectPlayerProjectionTests.cs
├── EffectRepairPacketBuilderTests.cs
├── CharacteristicsServiceTests.cs
├── PromptDocumentationCoverageTests.cs
└── AfterlifeDocumentationCoverageTests.cs

BookOfEternityClient.IntegrationTests/
├── EffectMaterializationTestContext.cs
├── EffectMaterializationValidationTests.cs
├── CanonicalStateNormalizerTests.Effects.cs
├── EffectMaterializationLifecycleTests.cs
├── EffectMaterializationRepairLifecycleTests.cs
├── EffectAfterlifeAdapterTests.cs
├── AfterlifeSpiritualConflictValidationTests.cs
├── AfterlifeEntityProfileValidationTests.cs
├── ExplorerModeCommandTests.Effects.cs
├── ExplorerWebCommandServiceTests.Effects.cs
├── GameEngineTurnLifecycleTests.cs
├── FileSystemExampleFixtureIntegrityTests.cs
└── ExampleDocumentationValidationTests.cs

Rules/, TaskGuides/, OtherGuides/, Examples/, FileSystemExample/,
CLI_API_Specification.md, CLI_Agent_Daemon_Specification.md,
BookOfEternityClient/game_master_daemon.ps1
```

**Structure Decision**: Keep active semantic instances with their owners and one focused effect contract/planner/scheduler/index/projection layer under `Services`. The effect planner is a subordinate builder inside the shared accepted-mechanics transaction, not a second publisher. Stage all effect operations in one transient file, consume them after source-owning normalizers have established accepted same-turn identities, and publish effect/resource/pending after-images through the sole cached `AcceptedMechanicsPlan`. Use afterlife profiles as the sole persistent afterlife actor-effect carrier and an adapter for specialized `combatConditions[]`; do not duplicate effects in Guardian/resident source files or add a global semantic effect registry.

## Phase 0: Research Outcomes

Research decisions and rejected alternatives are recorded in [research.md](research.md). Implementation-critical outcomes are:

1. Active instances are distinct from static `combatEffect`, `structuredBonuses`, Fate Card, item, skill/art, wound, quest, location, faction, event, hazard, and combat-action definitions.
2. `effectChanges[]`/`effectResolutionReceipts[]`/`effectEventReports[]` are the only common application/result/event-evidence routes and are fully consumed; registered event adapters bind sealed evidence while the client selects effect/trigger/post-state, and old player/NPC and direct carrier routes are removed.
3. A closed optional `activeEffectDefinitions[]` or an equivalent proven adapter supplies exact materializable source policy.
4. Random permanent identities are generated once in the subordinate effect plan retained by one cached `AcceptedMechanicsPlan`, never independently regenerated, separately published, or derived from GM input.
5. Source/target catalogs compose pre-turn authority only with explicit exact
   same-turn DTO exports: client-assigned item/location refs come from their
   accepted plans, while stable-ID owners pass their own validator before an
   adapter exports them. Names, raw sibling scans, aliases, cross-realm
   inference, and commit-time plan rebuilding are forbidden.
6. Anonymous Mortal combatants use exact same-turn `combatantRef` and receive stable client-owned combat-local anchors once. Named combat representations use exact `npcRef -> NPCId` authority and never receive a second combat identity.
7. Nine registered component profiles, five stack policies, and eight lifetime modes replace arbitrary pseudo-mechanics and duration sentinels.
8. The client advances lifecycle in one deterministic phase order; only bounded story-facing work reaches the GM.
9. Afterlife `combatConditions[]` retain specialized axes/counterplay but use common identity/history; persistent afterlife actor effects live only in accepted profiles.
10. Mechanics and player views consume one all-or-nothing accepted snapshot; legacy raw aliases disappear.
11. Effect state extends the existing accepted write/rollback/repair loop rather than creating a second transaction.
12. Missing pristine carriers initialize empty; non-empty legacy state is unsupported and never migrated.

## Phase 1: Design Outcomes

- [data-model.md](data-model.md) defines transient commands, source definitions, canonical active instances, registered components, stack/lifetime/trigger/removal state, owner carriers, identity index, accepted plan, pending resolution, mechanics snapshot, player projection, and lifecycle transitions.
- [contracts/effect-command-and-envelope.md](contracts/effect-command-and-envelope.md) defines the only effect command surface and closed canonical envelope.
- [contracts/effect-identity-source-target-authority.md](contracts/effect-identity-source-target-authority.md) defines client identity, exact source/target catalogs, same-turn composition, and protected direct-mutation failures.
- [contracts/effect-stacking-and-lifecycle.md](contracts/effect-stacking-and-lifecycle.md) defines closed stack/lifetime policies, deterministic scheduler order, conflicts, triggers, replay, wound, and realm transitions.
- [contracts/effect-afterlife-adapter.md](contracts/effect-afterlife-adapter.md) preserves spiritual-condition mechanics while aligning identity/lifecycle and places persistent afterlife actor effects in accepted profiles.
- [contracts/effect-atomic-repair-and-rollback.md](contracts/effect-atomic-repair-and-rollback.md) defines the transaction, failure-injection matrix, repair eligibility/protection, coherent retry, and player-safe failure boundary.
- [contracts/effect-player-projection.md](contracts/effect-player-projection.md) defines visible facts, hidden/GM-only suppression, whole-DTO privacy, action revalidation, surface coverage, parity, and wound copy.
- [quickstart.md](quickstart.md) gives locally runnable contract, lifecycle, afterlife, mechanics, repair, rollback, projection, documentation, performance, and manual validation scenarios.

## Implementation Strategy

1. Add RED contract/identity/source-definition tests, then implement closed envelope/component/source-definition/index parsing without changing consumers.
2. Add RED source/target authority tests and build one-pass exact catalogs, stable combat-local anchors, and same-turn plan exports.
3. Add RED planner/plan-cache tests and implement `effectChanges[]`, random one-time identity allocation, stack/lifetime resolution, direct-mutation protection, trigger graph validation, and complete effect carrier/index subplanning.
4. Add RED Mortal carrier and normalizer integration tests; replace player/NPC legacy writes, preserve adjacent wound state, carry accepted combat arrays, feed the validated effect subplan into the common planner after source-owning normalizers, consume commands, and publish only through the accepted-mechanics rollback boundary.
5. Add RED lifecycle/mechanics tests; implement deterministic scheduler, common bounded resource receipts, effect-owned companions, and all-or-nothing `EffectMechanicsSnapshot`; migrate `CharacteristicsService` and affected combat/action consumers off raw aliases.
6. Add RED afterlife adapter tests; route spiritual condition creation/consumption through common operations, retain legal axes/counterplay, and add persistent profile effects without touching Shining blessing entitlements.
7. Add RED repair/failure-injection tests; integrate bounded packets, baseline-before-dispatch, coherent full-response resubmission, operator diagnostics, helper-failure safety, stale-output suppression, and byte/existence-exact rollback.
8. Add RED console/browser parity and privacy tests; implement one `EffectPlayerProjection`, opaque action selectors, submit-time accepted revalidation, whole technical DTO suppression, and wound-independent copy.
9. Migrate active new-game files, helper fixtures, Mortal/afterlife prompts, rules, examples, manifest, daemon reminders, and documentation/source guards; remove positive legacy routes/sentinels while retaining labeled negative fixtures.
10. Run quickstart-focused filters through the implementation, one meaningful Fast checkpoint, required FullValidation/LifecycleIntegration, manual console/browser checks, and one clean-candidate PreMerge immediately before integration.

## Concrete Active-Effect Path Matrix

The detailed reader/writer/GM/example inventory is maintained in
[effect-surface-inventory.md](effect-surface-inventory.md). Canonical ownership
used by fixtures, validation, planning, normalization, snapshots, rollback, and
player projection is:

| Logical surface | Physical path | GM direct post-state | Client responsibility | First tasks |
| --- | --- | --- | --- | --- |
| Mortal player | `game_state/player/effects.json` | forbidden | Validate/project `activeEffects[]` after accepted commands | T014–T038 |
| Named NPC | `game_state/npcs/npc_effects.json` | forbidden | Preserve adjacent wounds and project exact NPC entries | T014–T038 |
| Enemy combatants | `game_state/combat/enemies.json` | non-empty active arrays forbidden | Assign anonymous anchors; bind named rows to the one NPC identity; publish complete anonymous buff/debuff arrays | T017–T036 |
| Ally combatants | `game_state/combat/allies.json` | non-empty active arrays forbidden | Assign anonymous anchors; bind named rows to the one NPC identity; publish complete anonymous buff/debuff arrays | T017–T036 |
| Persistent afterlife actors | `game_state/meta/afterlife_entity_profiles.json` | direct active post-state forbidden | Preserve profiles and publish `activeEffects[]` | T053–T063 |
| Spiritual conditions | `game_state/meta/afterlife_spiritual_conflict_state.json` | direct lifecycle post-state forbidden | Adapt `combatConditions[]` to common identity/lifecycle | T054–T063 |
| Identity/history | `game_state/effects/effect_identity_index.json` | never | Allocate, reconcile, retain terminal history | T012–T036 |
| Transient commands | `game_state/effects/effect_commands.json` | response distribution only | Validate, plan, consume, delete | T023–T036 |
| Pending resolution | `game_state/control/pending_effect_resolutions.json` | never | Create requests, validate receipts, snapshot, clean | T043, T049–T050 |

Static source definitions remain embedded with their owning accepted entities;
there is no global semantic definition registry or additional blessing carrier.

## Complexity Tracking

No constitution exception is required.

- `effect_identity_index.json` owns only identity, carrier coordinate, stack coordinate, lifecycle, and replay evidence. It is not a second semantic active-effect store.
- `effect_commands.json` is transient staging consumed inside one accepted transaction, not durable canonical state.
- `pending_effect_resolutions.json` is the common client-owned bounded resource-work root for effect triggers, not a GM-editable mechanics registry.
- `EffectAcceptedTurnPlanCache` may retain one validated subordinate result only for common-plan construction; it has no independent consume/publication API.
- `event_reaction` outputs remain unopened candidates until the common accepted-use transcript and any exact applied predecessor authorize them; candidate discovery never mutates carriers or spends expansion. Uses/lifetime/terminal projection is one later phase, not dependency-specific special cases.
- `AcceptedMechanicsPlanner` owns one immutable resource-event boundary transcript and one monotonic mechanics order. The effect reducer only projects typed accepted releases; it cannot re-arbitrate deferred reactions. Boundary batches are frozen, terminal availability affects later boundaries only, and reaction-created effects wait until the next accepted transition, so the existing statically bounded resource graph remains the only scheduler.
- The single active Mortal QTE has a separate persistent continuation and narrow receipt transport because an ordinary pending-turn snapshot is no longer valid after offer acceptance. This is continuation authority, not a second turn or a second mechanics planner.
- Distributed owner carriers prevent a global semantic hot file while the shared planner/index/snapshot prevent divergent contracts.
- The specialized afterlife adapter preserves existing gameplay rather than forking identity and retry semantics.
