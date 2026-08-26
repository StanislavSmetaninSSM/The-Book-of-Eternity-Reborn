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
paths per wound, 16 requirements/complications, four consequences, and 32 wound
transitions per accepted turn.

**Constraints**: Direct pre-alpha cutover with no migration, legacy reader, dual write,
or fallback; no wound catalog; no automatic ordinary wound or soul dissipation; exact
ordinal/confusable authority; one new spiritual wound per side/conflict; effect removal
cannot heal; one complete accepted transaction and snapshot rollback; one safe-cycle
healing attempt per wound/cycle; console/browser semantic parity; Russian in-world copy;
recursive privacy and renderer escaping; afterlife docs/examples/registry synchronized.

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
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundAcceptedTurnPlannerTests"
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
│   ├── ExplorerCommandCatalog.cs / ExplorerMode.cs
│   ├── ExplorerWoundCommandResultBuilder.cs
│   └── ExplorerMode/*                             # remove legacy raw preview; guided console flow
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
├── src/                                           # wound cards/flows only where generic blocks are insufficient
└── test/                                          # player-facing copy/layout/parity/privacy coverage

BookOfEternityClient.Tests/
├── Wound*Tests.cs                                 # strict contract/kernel/planner/projection/repair
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
          │ provisional IDs + source exports
          ▼
        Effect accepted-turn planner
          │ exact effect after-images/identities
          ▼
      Wound planner Finalize (links/budgets)
          │ wound transitions + resource/item/scheduler intents
          ▼
         AcceptedMechanicsPlanner
          │ one fingerprint / before-images / after-images
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
4. Implement staged prepare/finalize planning and accepted wound source export to the
   existing effect planner.
5. Add RED common plan/cache/fingerprint/before-image/same-root/scale/replay/rollback
   tests.
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
   tests using two unrelated setting fixtures.
4. Implement treatment capabilities, exact item/resource/skill/provider/facility/
   location resolution, reservation, and outcome composition.
5. Add RED recovery/deterioration/time/retry/death-boundary tests and implement the
   registered Mortal policy scheduler.
6. Replace legacy Mortal rule/example/UI preview shapes and add a complete GM worked
   lifecycle before exposing the Mortal command flow.

Checkpoint: both cross-setting fixtures complete create -> diagnose -> treat/recover ->
heal -> History, with exact rollback and no catalog lookup.

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
6. Implement browser presentation/menus through shared blocks, add visual-specific React
   work only where generic rendering is insufficient, run frontend verification, and
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
| Cyclic wound/effect dependency | Staged prepare/source export/effect/finalize handshake |
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
