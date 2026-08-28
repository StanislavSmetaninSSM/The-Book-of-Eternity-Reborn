# Implementation Plan: Complete Wound Materialization and Healing

**Branch**: `1536-complete-wound-materialization` | **Date**: 2026-08-26 | **Spec**: [spec.md](./spec.md)

**Input**: Approved feature specification for GitHub issue
[#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
plus the approved Superpowers design at
`docs/superpowers/specs/2026-08-26-complete-wound-materialization-design.md`.

## Summary

Replace the loose pre-alpha wound arrays with one strict, setting-agnostic wound
identity/lifecycle kernel and two setting adapters. Mortal wounds retain free GM
construction and world-specific diagnosis/treatment without a wound catalog. Spiritual
wounds attach to bounded afterlife strain opportunities, add standard resilience and
healing arts, and recover through combat treatment, safe world cycles, providers, or
natural entity progression.

The implementation extends the existing accepted-mechanics transaction rather than
adding an independent writer. A staged
`WoundAcceptedTurnPlanner.Prepare -> EffectAcceptedTurnPlanner ->
WoundAcceptedTurnPlanner.Finalize` handshake resolves wound/effect source ownership;
the wound persists a bounded complete source-definition graph separately from the
directly materialized root effect bindings, while later `apply_definition` descendants
remain effect-runtime identities resolved through an indexed exact wound source;
the prepare stage emits only typed root application refs, the effect planner remains the
sole allocator of opaque permanent effect IDs and returns the exact result map with
actual component counts and domain/versioned materialization fingerprints derived from
the created roots and consumed by finalization;
the exported wound source is non-materializable to ordinary GM effect commands and the
sealed typed batch is the only direct-root authority;
each root receives a unique derived creation event while retaining the accepted wound
event as separate causal chronology, and non-interchangeable stage seals culminate in an
internally computed/revalidated common prepared-plan fingerprint;
the common plan then publishes wound carriers, identity/history, linked effects,
resources/items, scheduler outcomes, provider payment, journals, and player output
under one snapshot/write-lease/rollback boundary. Console and browser use one
visibility-safe application/projection service for `/раны` and `/лечить` workflows.

## Technical Context

**Language/Version**: C#/.NET 8 (`net8.0`, nullable enabled); TypeScript 6.0 and React
19.2 for browser surfaces; PowerShell 7 for bounded verification and daemon entrypoints.

**Primary Dependencies**: ASP.NET Core local/loopback host; Spectre.Console 0.49.1;
FluentValidation 11.9; existing accepted-mechanics, effect materialization, resource,
Mortal item/location/actor, afterlife profile/conflict/progression, Guardian, and
Shining Abode services; React/Vite 8/Vitest for browser presentation.

**Storage**: Local file-backed strict JSON below `game_state/`, with client-owned
identity/history/command/pending roots, exact before-images, atomic file writes, pending
turn snapshots, and whole-turn rollback. No database or remote service.

**Testing**: xUnit 2.9 unit/integration projects through
`pwsh .\scripts\test-csharp.ps1`; Roslyn/source/documentation guards; Vitest and compiled
player-facing tests through `npm run verify`; rendered interaction/privacy checks with
the project-preferred browser skill when browser UI changes.

**Target Platform**: Local Windows game client and PowerShell 7 daemon, console client,
and loopback desktop/mobile-responsive browser client. No cloud dependency or telemetry.

**Project Type**: Multi-surface local game client/runtime with C# console/host,
file-backed canonical state, GM bridge/daemon prompts, and React browser frontend.

**Performance Goals**: One indexed catalog pass per root and near-linear planning in
the number of wound/effect/resource operations; no wound-by-effect nested scans; bounded
repair/pending waves; ordinary wound query/choice response comparable to existing
effects/status commands; no new unbounded test lane. Version-1 limits are 2,000 active
wounds, 20,000 history rows, 128 commands, 64 pending candidates, 32 routes/diagnosis
paths per wound, 16 requirements per route/path, 16 known-fact prerequisites and 16
reveals per diagnosis path, 16 complications, four consequences, and 32 wound
transitions per accepted turn. Wound source indexing separately caps five definitions
and five root bindings per wound, 10,000 of either across 2,000 pre-turn active wounds,
at most 10,000 active/suspended wound-owned effect instances, and 160
definitions/root applications across 32 same-turn transitions; generic
non-wound source limits remain independent. The five-definition bound charges a slot to
the reaction producer and each flattened non-marker leaf component. Selective
complication cleanup builds one first-create causal parent index, visits each parsed
source-group identity at most once, reconstructs `base_wound | complicationId` root
domains, and rejects cyclic, ambiguous, foreign, or cross-domain lineage before
mutation; replacement succession is non-ownership lifecycle evidence.

**Constraints**: Direct pre-alpha cutover with no migration, legacy reader, dual write,
or fallback; no wound catalog; no automatic ordinary wound or soul dissipation; exact
ordinal/confusable authority; one new spiritual wound per side/conflict; eight
first-class persistent spiritual wound effect profiles, never combat-condition aliases;
effect removal cannot heal; one complete accepted transaction and snapshot rollback;
one safe-cycle healing attempt per wound/cycle; console/browser semantic parity;
Russian in-world copy; recursive privacy and renderer escaping; afterlife
docs/examples/registry synchronized.

**Scale/Scope**: One common kernel; Mortal and afterlife adapters; player, named NPC,
combatant, player soul, Guardian, resident, radiant/afterlife actor owners; wound
creation through terminal History; linked effects/resources/items/scheduler; spiritual
conflict/art/progression/providers; console/browser commands; GM rules, daemon prompts,
guides, examples, manifests, and source guards. Implementation is expected to span many
source/test/doc files but one accepted Spec Kit feature and issue.

**Source Issue(s)**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
depends on completed [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535);
related #1500, #1511, #1512, #1516, #1517, #1518. Create a separate linked issue if the
implementation audit proves Saref `memory_suppression` is only metadata rather than a
complete independent effect.

**Contract Scope**: Player-facing Mortal/afterlife mechanics and copy; GM-authored wound
constructor/treatment/narration/repair; canonical runtime state; accepted events,
validation, normalizer, pending/control and rollback; effects/resources/items;
afterlife strain/arts/progression/providers/factions; console and browser; daemon and GM
prompts; rules, guides, examples, manifests, documentation/source guards.

**Verification Commands**:

```powershell
# During each TDD slice: use the smallest owning test class/filter.
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundEffectBatchPlannerTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~WoundMaterializationLifecycleTests"

# One meaningful checkpoint after integrated behavior exists.
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast

# Documentation/afterlife boundary required by AGENTS.md.
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~PromptDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation

# Conditional exhaustive conflict diagnostic when its complete matrix changes.
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane RegressionIntegration

# Browser surface, when touched.
Push-Location .\BookOfEternityClient.WebFrontend
npm run verify
Pop-Location

# One final bounded merge control; no redundant adjacent Fast run.
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

The clean feature-worktree baseline passed Fast `4339/4339`, failures `0`, timeout
`false`, cleanup complete, in `00:04:33.8143170`; result directory
`TestResults/test-lanes/20260826-154530-205-21516-38708e3e7c1243dbb26267659d11b6b4-fast`.

## Constitution Check

*GATE: Passed before Phase 0 research and re-checked after Phase 1 design.*

| Gate | Pre-research | Post-design evidence |
| --- | --- | --- |
| GitHub traceability | PASS | #1536 is linked in spec, plan, research, data model, contracts, quickstart, and generated tasks; #1535 dependency is explicit. |
| Spec Kit fit | PASS | Epic crosses canonical state, validation, Mortal/afterlife, console/browser, prompts/docs/examples, and multiple sessions. |
| Player-facing integrity | PASS | One projection/application service, Russian in-world copy, hidden-ID targeting, parity, escaping, and privacy are contractual. |
| Contract/state authority | PASS | Client/GM authority, staged accepted plan, carriers/index/history, pending repair, docs/examples/registry updates, and no code-only GM surface are specified. |
| Test-first path | PASS | Each slice begins with named owning RED unit/integration/guard tests before production changes. |
| Verification evidence | PASS | Focused, Fast checkpoint, conditional docs/FullValidation/conflict diagnostics, frontend/browser, and final PreMerge controls are listed. |
| Agent orchestration | PASS | Three read-only research agents mapped Mortal, afterlife, and accepted-mechanics boundaries; reports were independently reconciled with code. Any later delegation packet must include #1536 and all active Spec Kit artifacts/commands. |
| Pre-release save policy | PASS | Version-1 direct cutover removes legacy wrappers/fallbacks and updates current bootstrap/fixtures/docs; no migration is planned per constitution and explicit user decision. |

No constitution violation or complexity exception is required.

## Project Structure

### Documentation (this feature)

```text
specs/1536-complete-wound-materialization/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── wound-materialization-command.md
│   ├── wound-canonical-lifecycle.md
│   ├── wound-effects-and-atomicity.md
│   ├── mortal-wound-treatment.md
│   ├── afterlife-spiritual-wounds-and-healing.md
│   ├── wound-player-commands-and-projection.md
│   └── wound-repair-retry-and-rollback.md
└── tasks.md
```

### Source code (repository root)

```text
BookOfEternityClient/
├── Models/GameResponse.cs                         # remove loose wound arrays / bind strict response surface
├── Configuration/FileMapping.cs                  # remove direct wound file mapping
├── Services/
│   ├── WoundMaterializationContract*.cs          # strict parsers, bounds, canonical models
│   ├── WoundIdentityState.cs                     # client index/history agreement
│   ├── WoundCarrierCatalog.cs                    # player/NPC/combatant/afterlife occurrences
│   ├── WoundAcceptedOwnerCarrierAuthority.cs     # accepted owner shape plus client-owned wound baseline
│   ├── WoundSourceAuthority.cs                   # event/source/owner/opportunity adapters
│   ├── WoundConsequenceEnvelopeCatalog.cs        # slot/power profiles
│   ├── WoundAcceptedTurnPlan*.cs                 # prepare/finalize/cache/handoff
│   ├── WoundTreatment*.cs                        # Mortal routes, spiritual resolver, quotes/attempts
│   ├── WoundRecovery*.cs                         # Mortal clocks and safe-cycle outcomes
│   ├── WoundRepairPacketBuilder.cs               # bounded GM repair
│   ├── WoundPlayerProjection.cs                  # privacy-safe shared read model
│   ├── WoundApplicationService.cs                # target/route/provider/command workflow
│   ├── AcceptedMechanicsPlan*.cs                 # wound input/fingerprints/after-images
│   ├── AcceptedMechanicsPlanner.cs               # staged wound/effect/resource composition
│   ├── AcceptedTurnAuthorityRegistry.cs           # generation-scoped wound authority
│   ├── EffectAcceptedTurnInputComposer.cs         # accepted wound source exports
│   ├── EffectSourceAuthority.cs                   # strict reciprocal wound bindings
│   ├── EffectIdentityState.cs                     # exact wound-source/definition index
│   ├── EffectComponentProfiles.cs                 # registered primitives, not severity policy
│   ├── AfterlifeSpiritualConflictState.cs         # danger/opportunity/defeat/healing action
│   ├── AfterlifeSpiritualConflictTurnPreviewService.cs
│   ├── AfterlifeActionCostRules.cs
│   ├── AfterlifeEntityProfileState.cs             # standard arts/service/profile wounds
│   ├── ProgressionScheduleService.cs              # exact safe-cycle recovery outcome
│   ├── ShiningAbodeState*.cs                      # healing_support role
│   ├── GuardianAbodeResidentState.cs              # visible roster binding
│   ├── SystemGuardianLibraryService.cs            # protected Elyara profile/service
│   ├── AfterlifeContractRegistry.cs               # pending/control registry
│   ├── Validation/ValidationService.Wounds*.cs    # strict orchestration/adapters
│   ├── Validation/ValidationService.Afterlife*.cs # conflict/profile/Shining gates
│   └── CanonicalStateNormalizer/
│       ├── CanonicalStateNormalizer.AcceptedMechanics.cs
│       ├── CanonicalStateNormalizer.Wounds.cs
│       └── existing same-root profile/conflict assemblers
├── Core/GameEngine/
│   ├── GameEngine.ValidationAndRepair.cs          # wound raw/post validation and repair
│   ├── GameEngine.SessionAndSnapshots.cs          # exact wound/scheduler/output coverage
│   └── GameEngine.TurnLifecycle.cs                # accept only after full publication validation
├── UI/
│   ├── ExplorerMode.cs
│   ├── ExplorerWoundCommandResultBuilder.cs
│   └── ExplorerMode/*                             # remove legacy raw preview; guided console flow
├── CommandProtocol/ExplorerCommandCatalog.cs      # canonical command/alias registry
├── WebUi/
│   ├── ExplorerWebCommandService.cs
│   ├── BrowserAfterlifeWriteService.cs
│   ├── BrowserPlayerCommandMenuBuilder.cs
│   └── BrowserCommandCoverageService.cs
├── system_guardians/built_in/elyara/
│   ├── manifest.json
│   └── dossier.md
└── game_master_daemon.ps1                         # mandatory GM contract entrypoint

BookOfEternityClient.WebFrontend/
├── src/                                           # dedicated wound cards and guided treatment steps over shared blocks
└── test/                                          # player-facing copy/layout/parity/privacy coverage

BookOfEternityClient.Tests/
├── Wound*Tests.cs                                 # strict contract/kernel/planner/projection/repair
├── SpiritualWound*Tests.cs                        # registered profiles and conflict contribution
├── AcceptedMechanics*Tests.cs                     # cache/fingerprint/ordering/publication
├── Effect*Wounds*.cs                              # independence and reciprocal links
├── Afterlife*Tests.cs                             # arts/services/docs/source guards
└── PromptDocumentationCoverageTests.cs

BookOfEternityClient.IntegrationTests/
├── WoundMaterializationTestContext*.cs
├── WoundMaterializationLifecycleTests*.cs
├── MortalWoundMaterializationLifecycleTests.cs
├── AfterlifeWoundProgressionLifecycleTests.cs
├── WoundConsoleBrowserParityTests.cs
├── AfterlifeSpiritualConflict*Tests.cs
├── ExampleDocumentationValidationTests.cs
└── rollback/scale/lifecycle extensions

Rules/
├── Block_2.txt
├── Block_5.txt
├── Block_10.txt
├── Block_12.txt
├── Block_21.txt
├── Block_32_Guardians.txt
└── Block_CLI_Operations.txt

TaskGuides/CLI_Step_Main.txt
OtherGuides/
├── Wound_Materialization_Contract.md
├── Effect_Materialization_Contract.md
├── Afterlife_Contract_Matrix.md
└── relevant Mortal/afterlife/Guardian guidance
Examples/
├── E_Block_5.txt
├── E_Block_10.txt
├── E_Block_12.txt
├── E_CLI_Effect_Materialization.txt
├── E_CLI_Afterlife_Turns.txt
└── example_validation_manifest.json
CLI_API_Specification.md
```

**Structure Decision**: Keep one C# runtime/application layer and extend its existing
partial services rather than add projects or repositories. New wound classes own the
strict domain and staged planner. Existing effect/resource/owner/snapshot/profile/
conflict services remain their domain authorities and receive narrow typed integration
points. Active state stays in owner-appropriate carriers; a small global wound index and
append-only history provide identity/replay agreement. Console/browser are adapters to
one shared projection/action service. GM capability changes always update rules, daemon
entrypoints, guides, examples, manifests, and guards in the same slice.

## Architecture and execution order

```text
sealed event / player command / scheduler cycle
                    │
                    ▼
        owner + wound opportunity authority
                    │
                    ▼
      Wound planner Prepare (no canonical writes)
          │ wound ID + complete source graph + typed root refs
          │ + expected component counts/materialization fingerprints (no effect IDs)
          ▼
        Effect accepted-turn planner
          │ opaque root IDs + actual recomputed counts/fingerprints
          │ + exact application result map/after-images
          ▼
      Wound planner Finalize (root links/graph/budgets/exact materialization agreement)
          │ wound transitions + resource/item/scheduler intents
          ▼
         AcceptedMechanicsPlanner
          │ trusted common prepared-plan fingerprint / before-images / after-images
          ▼
 validated handoff + canonical write lease + snapshot rollback
          │
          ▼
  wound/effect/resource/profile/schedule/history/output commit
```

Same-root changes are typed and composed once. In particular,
`afterlife_entity_profiles.json` may receive art progression, wound, effect, and service
state in one accepted turn; independent whole-root producers are rejected.

## Implementation phases

### Phase 0 — Research and design closure

Complete and commit the approved design, feature spec, research decisions, data model,
contracts, quickstart, tasks, and cross-artifact analysis. Reconcile direct code
inspection with read-only Mortal, afterlife, and accepted-mechanics research reports.
No production code is edited in this phase.

### Phase 1 — Common kernel and transactional authority

1. Add RED strict parser/bounds/legacy rejection tests.
2. Implement the version-1 wound envelope, owner coordinate, identity index, append-only
   history, carrier catalog, transition reducer, and agreement checks.
3. Add RED opportunity/source/guarantee and consequence budget/power tests.
4. Persist the strict bounded wound-owned source graph and separate root bindings, then
   implement staged prepare/finalize planning, an immutable typed wound-effect operation
   batch, accepted wound source export, a shared domain/versioned materialization-
   fingerprint writer, effect-owned opaque root allocation, and exact
   `applicationRef -> effectId` results with actual component counts/fingerprints.
   Prepare, effect planning, and Finalize independently derive or recompute the exact
   source/component agreement from detached payloads. Do not preallocate any effect ID
   in the wound planner; reaction-only descendants have no application ref or reserved
   identity.
   Treat prepared slot ordinals as correlations, then canonically renumber the accepted
   result slots per wound batch after random opaque IDs exist (`effectId` ordinal,
   prepared within-root semantic order, contiguous `1..N`); Finalize independently
   recomputes that mapping before persistence.
   Carry reducer/history-only transition facts through an internal immutable authority
   record with a separate seal bound to the prepared input and local/permanent wound
   identities, so zero-operation wounds retain exact provenance without changing the
   public source-export or preparation fingerprint format.
   Preserve pre-turn wound carriers, identity, and history through a second detached
   internal authority record whose seal binds their fixed-order canonical bytes to the
   prepared input, so Finalize can derive honest collection and history after-images
   without recovering state from an irreversible fingerprint.
   Require the same-turn wound source-authority set to equal the prepared exports
   exactly, reserve every accepted event reference against derived wound operation
   events before allocation, and freeze/detach returned predicate evidence.
   During Finalize, independently require the exact reciprocal prepared-root effect set
   in active effects, runtime carriers, publication carriers, and identity entries;
   verify full identity semantics and allocator-backed create-transition evidence after
   resealing.
   Reconcile the optional wound single-leaf expansion with #1535 by requiring exact
   `apply_definition.maxExpansion = 2` when present, requiring same-domain `replace` for
   a root-bound target, and recording the producing effect in every reaction-created
   identity's first create transition.
   Register the eight closed spiritual consequence profiles in the common effect
   component registry with persistent-afterlife-actor/wound-link scope and a safe generic
   player projection. Their canonical payload remains owner-relative; do not route them
   through `afterlife_combat_condition` or `combatConditions[]`.
5. Add RED common plan/cache/fingerprint/before-image/same-root/scale/replay/rollback
   tests, including cross-plan stage mixing and created-event versus causal-event
   tampering.
6. Extend accepted-mechanics authority, planner, normalizer publication, snapshot
   coverage, and bounded repair. Do not expose player commands yet.
7. Remove loose response/file-mapping/state-distributor wound authority and legacy
   `WoundReference` fallbacks only after replacement tests are GREEN.

Checkpoint: a synthetic accepted wound can create/worsen/heal with exact effects and
history atomically for every owner carrier; effect removal never heals it.

### Phase 2 — Mortal occurrence, diagnosis, treatment, and recovery

1. Add RED formal/narrative opportunity and GM optional/lower/guaranteed tests.
2. Implement Mortal event adapters and complete free-form proposal validation without a
   wound catalog.
3. Add RED route/discovery/requirement/procedure/course/guaranteed/resource/stale-ref
   tests using two unrelated setting fixtures. Discovery uses an explicit
   `requiresKnownFacts` least-fixed-point graph, sealed success/failure diagnosis
   evidence, and a distinct append-only `author_alternative_treatment` transition.
4. Implement structural treatment/discovery validation separately from fresh exact
   item/resource/skill/provider/facility/location reachability, then compose sealed
   diagnosis/alternative-route authority, reservation, and outcomes.
5. Add RED recovery/deterioration/time/retry/death-boundary tests and implement the
   registered Mortal policy scheduler.
6. Replace legacy Mortal rule/example/UI preview shapes and add a complete GM worked
   lifecycle before exposing the Mortal command flow.

Checkpoint: both cross-setting fixtures complete create -> diagnosis success/failure ->
optional alternative-route authoring -> treat/recover -> heal -> History, with exact
rollback and no catalog lookup.

T067 uses one production-owned sealing boundary rather than exposing evidence-record
construction or fingerprint recipes to callers. `MortalWoundTreatmentPlanner` is the
sole producer of `WoundTransitionRequest` values for `diagnose` and
`author_alternative_treatment`: its `CreateDiagnosisTransition` and
`CreateAlternativeTreatmentTransition` factories accept transition coordinates,
the parsed before/proposed-after wounds, and only the already validated external
authority fingerprints. The factories derive the complete-or-empty diagnosis reveal
set, the appended route/path identities, every wound-local before/after/route/path/
result seal, and the typed durable result. Reducer tests consume those factories and
may tamper with the returned request or after-image; they must not instantiate a
concrete evidence record by reflection or implement a test-only hash domain.

`WoundHistoryState` similarly owns `ComputeTransitionResultFingerprint` and immutable
`AppendTransition` operations. Append consumes a validated
`WoundTransitionHistoryIntent`, assigns the next global/per-wound ordinals, preserves
all prior rows byte-for-byte, recomputes the typed result seal, and returns a newly
validated state. Replay probes and already-accepted receipts carry that same typed
result. This is the stable semantic seam for T059/T067; concrete evidence-record and
hash-writer topology remains an implementation detail.

For command round-trip tests, `WoundResponseInputComposer` owns
`ComposeAcceptedTransitionCommandRoot(binding, request, finalSceneText)`. It emits the
closed safe command from a planner-sealed request; `ParseCommandRoot` followed by
`RecomposeCommandRoot` must reproduce that root exactly. Mutation after composition is
rejected either by strict parsing/seal validation or by recomposition, never accepted
by trusting caller-supplied fingerprints. Tests may hand-author malformed shapes for
field/path diagnostics, but only this production composer establishes a valid sealed
command authority.

### Phase 3 — Spiritual conflict, arts, healing, and entity recovery

1. Add RED danger-mode, formula threshold, strain cap, optional/lower/one-per-side/
   re-trauma/defeat/dissipation tests.
2. Extend conflict state, validation, preview, and GM context with client-authored wound
   opportunity audits and bounded defeat outcomes.
3. Add RED standard-art bootstrap/profile/progression/training tests; add
   `spiritual_resilience` and `spiritual_healing` tier 0-V without making resilience an
   operation.
4. Add RED active-healing tier/roll/natural-1/20/action-cost/counter/resource tests and
   implement one resolver.
5. Add RED safe-cycle/self/provider/player-to-entity/entity-natural-recovery/replay
   tests and integrate exact cycle outcomes into accepted progression scheduling.
6. Run the relevant focused spiritual-conflict matrix while iterating; use the full
   RegressionIntegration lane only when the exhaustive boundary changes or diagnosis
   requires it.

Checkpoint: players and persistent entities have complete bounded spiritual wound and
healing lifecycles; every non-training defeat is durable without mandatory injury or
dissipation.

### Phase 4 — Providers, commands, console/browser parity, and documentation

1. Add RED quote/rounding/payment/compensation/rollback tests and implement explicit
   healing service profiles.
2. Protect Elyara's tier-V Lazaret public service in manifest/profile/bootstrap/
   validator/normalizer and make it discoverable from first Chaos Sea entry.
3. Add `healing_support` to the visible Shining resident role contract and validate at
   least one tier I-V healer per faction without implying public access.
4. Add RED projection/target/privacy/stale-selection/History and console/browser parity
   tests.
5. Implement shared `WoundApplicationService`/projection and register `/раны`,
   `/wounds`, `/лечить`, `/treat`, `/исцелить`, `/heal`; remove raw legacy status wound
   parsing.
6. Implement dedicated browser wound cards/steps over the shared semantic blocks, run
   frontend verification, and
   perform rendered desktop/mobile interaction checks.
7. Update Mortal and afterlife rules, CLI specification, daemon/launcher prompts,
   contract guides/matrix/registry, Elyara docs, worked examples, validation manifest,
   documentation/source guards, and Russian terminology scans.
8. Audit Saref `memory_suppression`. Prove it is a complete independent effect and
   survives healing, or create a separate linked issue before #1536 closes.

Checkpoint: all player/GM surfaces are complete and parity-safe; healed wounds are
History-only; docs teach every authoring/repair workflow.

### Phase 5 — Hardening, review, and integration

1. Run focused identity/effect/resource/scheduler/output failure injection and linear
   scale controls; use systematic debugging for any unexpected failure.
2. Run one meaningful Fast checkpoint if not already run after full integration.
3. Run focused documentation guards and conditional FullValidation; run the exhaustive
   conflict diagnostic only when required by the changed boundary/evidence.
4. Run `npm run verify` and preferred browser interaction/visual/privacy checks when
   browser source changed.
5. Reconcile every FR/scenario/task, remove placeholders/legacy fallbacks, inspect diff,
   and request independent code review focused on authority, replay, privacy, and
   docs/console/browser parity.
6. Apply verified review findings with focused RED tests.
7. Run one final PreMerge control, record exact result artifacts/counts, update Spec Kit
   task checkboxes only from evidence, commit, push, open PR, review GitHub checks/diff,
   merge into `main`, verify remote main, and close #1536 only after all work is present.

## Risk controls

| Risk | Control |
| --- | --- |
| Cyclic wound/effect dependency | Staged typed prepare/source export/effect-result/finalize handshake plus a persisted bounded definition graph separated from root materializations; only the effect plan allocates permanent effect IDs |
| Zero-slot or late reaction effect becomes orphaned | Root bindings retain direct roots; first-create transitions retain exact causal parent identity; indexed exact wound-source and lineage lookup validates graph membership and terminates the intended closure |
| One wound event needs several root operations | The typed batch derives exact/confusable-unique internal created/transition event refs by common mechanics ordinal, retains the shared wound event as separate causal chronology, and returns one sealed result per application ref |
| Selective complication cleanup captures another complication | Pairwise-disjoint root sets reconstruct exact ownership domains; cross-domain stack/refresh/merge/replace is rejected before mutation and traversal uses only same-domain first-create causal lineage |
| Valid stage objects are mixed across plans | Non-interchangeable stage fingerprints bind source exports, operations, result maps, after-images, and final bindings; the common fingerprint is planner-computed and cache-revalidated, never caller asserted |
| GM applies an internal wound definition directly | Wound source exports are non-materializable; only the fingerprinted typed root batch or sealed reaction executor grants application authority |
| Descendant instances grow without a source-group bound | Wound definition stack keys are unique and `maxStacks=1`, so at most one active/suspended instance exists per persisted definition |
| Partial cross-file publication | One common plan, exact before-images, write lease, post-agreement, snapshot rollback |
| Same-root afterlife collisions | Typed root assembly and one producer per path |
| GM invents client authority | Closed DTOs; client IDs/progress/history/receipts/fingerprints; bounded repair |
| Wound becomes an effect | Independent carrier/index/history; effect operations cannot mutate wounds |
| Resource/payment double spend | Accepted intents plus attempt/quote replay keys and one atomic commit |
| Safe-cycle double recovery | Exact progression cycle key and per-wound attempt/tick seals |
| Hidden information leakage | Shared recursive visibility projection; no raw JSON; source/renderer guards |
| Console/browser drift | One application/result model plus parity tests |
| Elyara downgraded or absent | Client-protected built-in profile/service invariants and bootstrap tests |
| Shining role mistaken for public clinic | Separate visible role, capability tier, and optional service profile |
| Test runtime pressure | Small coherent Focused filters, one Fast checkpoint, conditional diagnostics, explicit bounded timeout headroom from measured evidence |
| Legacy schema weakens authority | Direct cutover and source guards forbidding old fields/mappings/fallbacks |

## Complexity Tracking

No constitution violations. The cross-domain breadth is inherent to #1536's accepted
scope; it is contained by one kernel, two adapters, one transaction, one application
service, and four independently verifiable implementation slices rather than new
projects or parallel authorities.
