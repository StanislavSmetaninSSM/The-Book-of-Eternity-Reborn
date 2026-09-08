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

Mortal opportunity ingress uses a signed accepted-occurrence boundary rather than
trusting source-shaped JSON. One closed pending occurrence language covers formal, QTE,
combat, trap, check, hazard, and narrative producers; the active pending-turn snapshot
binds its exact bytes. Initial composition and validation independently reconstruct the
complete ordered accepted-event set, and the common plan atomically appends one durable
opportunity-decision receipt for both `none` and `materialize` while consuming the
pending occurrence. This closes cold replay without adding a second writer.

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
wounds, 20,000 history rows, 20,000 opportunity-decision receipts, 128 commands, 64
repair candidates, 32 pending Mortal occurrences with 1-160 accepted events each, 32
routes/diagnosis paths per wound, 16 requirements per route/path, 16 known-fact
prerequisites and 16 reveals per diagnosis path, 16 complications, four consequences,
and 32 wound transitions per accepted turn. Wound source indexing separately caps five definitions
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
│   ├── MortalWoundOccurrenceProducer.cs          # finalized typed source-result reducer seam
│   ├── MortalWoundOccurrenceState.cs             # signed seven-kind pending occurrence authority
│   ├── MortalWoundOpportunityAdapter.cs          # lease-bound source-shaped correlation ingress
│   ├── MortalWoundOpportunityValidationAuthority.cs # independent accepted-turn reconstruction
│   ├── MortalWoundOpportunityReceiptState.cs     # append-only decision replay state
│   ├── PendingTurnSnapshotReader.cs              # current manifest plus exact signed snapshot reads
│   ├── WoundAcceptedEventAuthorityComposer.cs    # one complete event-set reconstruction
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
├── Block_7.txt
├── Block_8.txt
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
├── E_Block_7.txt
├── E_Block_8.txt
├── E_Block_10.txt
├── E_Block_12.txt
├── E_CLI_Effect_Materialization.txt
├── E_CLI_Ink_Feather_Actions.txt
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
   Classify first-create provenance through one shared linear analyzer: original and
   genuinely new roots are parentless, severity-rematerialized roots retaining a
   definition/ownership coordinate record the exact prior canonical same-definition
   root, removed definitions leave terminal history, and reaction children record an
   exact persisted different-definition `apply_definition` producer. Use the same
   generation rule for worsen, treatment, and later recovery, require a unique acyclic
   non-branching ownership-preserving spine, and require every retired generation and
   its reaction closure to be terminal. Full rematerialization exports only new
   application roots as current lineage; old roots remain predecessor/terminal evidence.
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

1. Add RED signed formal/QTE/combat/trap/check/hazard/narrative occurrence,
   current-snapshot/event-set parity, optional create versus explicit worsen, and GM
   optional/lower/guaranteed/durable-replay tests.
2. Implement Mortal event adapters and complete free-form proposal validation without a
   wound catalog. A shared current-snapshot reader verifies the live manifest, detached
   authority, active request context, required path coverage, and exact signed bytes.
   The production-only `ComposeAcceptedResponse(fs, lease, sourceEvent, gameResponse)`
   ingress treats the closed source-shaped event as correlation only, resolves one
   signed formal/QTE/combat/trap/check/hazard/narrative occurrence, and derives the
   owner/profile/source/outcome/severity and optional explicit create-versus-worsen
   target, plus the exact nullable minimum/guaranteed-trigger authority already supported
   by the opportunity contract. The correlation may repeat the selected ordinal and
   public owner/profile/source/outcome/safe-context fields, but never the hard maximum,
   minimum/guarantee, binding, event coordinates/seals, receipt, transition, anchor, or
   after-image. A shared composer accepts only a closed typed response-
   event projection plus selected typed evidence at exact zero-based ordinals; it derives
   every selected semantic seal itself, preserves the compatible generic-event seal, and
   returns the complete ordered event set plus its fingerprint. It does not accept raw
   JSON or caller-provided fingerprints. The source-result boundary, later adapter, and
   `ValidationService` each build their own projection/evidence from their authoritative
   typed result or signed occurrence. `ValidationService` independently reads both
   occurrence and decision-receipt roots from the signed pre-turn snapshot, verifies
   their agreement and unchanged live bytes, reconstructs the complete event set and
   opportunity from those roots plus the signed wound carrier, and compares the result
   instead of replacing it with the command binding. Exact signed prior receipts are
   then used for command recomposition. The adapter returns
   the existing wound composition result without writing. Formal re-trauma then uses the
   ordinary `StateDistributor` -> `ValidationService` -> common-plan pipeline; callers
   cannot inject or reuse an occurrence, treatment binding, fingerprint, transition,
   receipt, anchor, or after-image. Each typed producer first reduces its accepted result
   to a write-free complete harmful occurrence-candidate batch; harmless results produce
   none. Producers use one sibling of the registered-system-outcome projection seam after
   actual resource/effect finalization; unsupported producer kinds remain unregistered
   and fail closed rather than inferring harm from combatant, hazard, die, or prose JSON.
   The seven names are the closed common language, not a mandate for placeholder
   implementors: a concrete registration arrives only with the owning finalized typed
   resolver. T064 freezes the pure reducer plus signed correlation/validation seam;
   T070 supplies the first reachable production orchestration and remains the sole
   occurrence/receipt publisher.
   The source-result common plan reads both pending occurrences and durable
   decision receipts, resolves exact pending/consumed replay versus changed-source
   conflict by producer operation key, and atomically appends only genuinely new
   candidates in sealed batch-ordinal order; only a subsequent pending-turn
   snapshot that seals its exact bytes may expose the opportunity to the GM. The later
   wound-decision common plan appends the exact opportunity-decision receipt (including
   `none`) and consumes the pending occurrence. While the same active pending-turn
   snapshot is retained, restart replay reconstructs authority from its signed pre-
   consumption occurrence plus the current append-only receipt/pending partition and is
   either an exact no-command result or a conflict; a newer snapshot makes the old
   correlation stale. Both state implementations reuse the length-prefixed
   UTF-8 SHA-256 writer; their exact v1 candidate/occurrence/receipt domain strings,
   field order, derived IDs, and null encoding are frozen in `data-model.md`, so tests
   can detect a reordered or caller-trusted seal independently.
3. Add RED route/discovery/requirement/procedure/course/guaranteed/resource/stale-ref
   tests using two unrelated setting fixtures. Discovery uses an explicit
   `requiresKnownFacts` least-fixed-point graph, sealed success/failure diagnosis
   evidence, and a distinct append-only `author_alternative_treatment` transition.
4. Implement structural treatment/discovery validation separately from fresh exact
   item/resource/skill/provider/facility/location reachability, then compose sealed
   diagnosis/alternative-route authority, reservation, and outcomes.
   The dependency contour is T066 accepted-state/coordinates/course/start/bundle types
   and first-course/non-course factories, then T067-A procedure die/Fate authority and
   immutable shells. The remaining mutually dependent work follows the production-only
   phase graph from research decision R-019: T068-A resource preparation/registry and
   T069-A deterioration-policy authority; T070-A canonical anchor representation plus
   accepted-create common composition; T069-B non-replay recovery scheduling/intents;
   T067-B typed-history continuation, request sealing/resolution/persistence; T068-B
   finalization and durable claim reconstruction; T070-B treatment/recovery/re-entry
   publication; then T069-C durable replay verification. No test-created request,
   resolution, binding, anchor, history row, receipt, claim, or raw authority is used to
   cross a phase boundary.
5. Add RED recovery/deterioration/canonical `world_time.currentTimeInMinutes`/retry/
   death-boundary tests and implement the registered Mortal policy scheduler without a
   parallel seconds or wall-time clock. The GM proposal cannot author canonical anchors:
   accepted create allocates the recovery anchor from the sealed current minute and
   transition ID; a separate condition/deterioration anchor preserves grace across
   recovery ticks. Due/grace boundaries and time jumps use checked elapsed-cadence plus
   next-anchor state, stabilization rebases recovery and clears its satisfied condition
   anchor, while an accepted worsening re-entry allocates a fresh deterioration anchor
   from the exact `worsen` transition without resetting recovery; death remains a lifecycle
   handoff. T070-A first makes the canonical anchor state reachable only through an
   accepted initial-create plan; T069-B then evaluates that real state. T070-B composes
   typed transition intents into the existing accepted-plan authority and publishes the
   durable receipt required by T069-C replay; `CanonicalStateNormalizer` remains the
   only publisher.
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

For command round-trip tests, `WoundResponseInputComposer` owns the existing sole
`ComposeAcceptedTransitionCommandRoot(WoundAcceptedTurnBinding,
WoundTransitionRequest, string)` entry point for reducer-produced transition requests.
T067 adds the distinct pure
`ComposeMortalWoundTreatmentCommandRoot(WoundAcceptedTurnBinding,
MortalWoundTreatmentResolution, string)` entry point because a pre-resolution attempt
request cannot supply the selected typed result and overloading the existing name would
break its frozen single-method surface. Both emit the closed safe command only from
planner-sealed authority; `ParseCommandRoot` followed by `RecomposeCommandRoot` must
reproduce that root exactly. Mutation after composition is rejected either by strict
parsing/seal validation or by recomposition, never accepted by trusting caller-supplied
fingerprints. Tests may hand-author malformed shapes for field/path diagnostics, but
only these production composers establish valid sealed command authority.

T070's diagnosis/alternative command codec first needs complete standalone member
validation as specified in
`docs/superpowers/plans/2026-09-06-t070-treatment-member-shapes.md`. Shared local
component/graph traversal and reciprocal slot validation are separate from the real
wound severity/owner/policy predicates. Standalone route/path parsing reuses the same
closed readers, nested validators and canonical writers as full wound parsing, without
inventing a wound or substituting a maximum severity. All registered nested complication
and heal-legacy constructors remain supported. A shape-valid draft has no fresh world
authority; full wound legality and current/signed-source authority are still mandatory.

The shared detached traversal must retain bounded-constructor input faults rather than
silently accepting the empty projection of an oversized expansion. Shape-only per-root
budget accounting includes mechanically derived slots even when an optional reciprocal
summary is absent. Existing full complication parsing already requires non-null slot
summaries; mechanical legacies use their separate derived-source graph, not a newly
imposed wound-consequence slot budget. The narrow diagnosis result-cardinality alignment
in `docs/superpowers/plans/2026-09-06-t070-diagnosis-result-cardinality.md` also ensures
that accepted diagnosis intents obey the existing closed history result contract before
command work proceeds: empty success is rejected, empty failure and already-known-fact
success remain representable and durable.

The accepted-command wire projection has its own production-owned deterministic
integrity seal, distinct from the factory's private full-request seal. It binds every
represented authority coordinate, recomputed typed result and available accepted-turn
binding plus final scene. Parsing may accept a structurally well-formed draft before
recomposition verifies these seals unconditionally. Alternative full route/path content
is rehashed, including display names. Diagnosis's omitted wound/event/transition fields
are not guessed, hidden in a memory registry, or added to the closed schema. Fresh
publication later reconstructs those coordinates from the real attempt/request and
canonical/signed sources; a recalculable public checksum is not proof of world authority.
Every parsed family must be retained, counted and checked. Until that fresh adapter
exists, both distribution and validation explicitly reject new accepted variants;
nonempty commands may not succeed as empty wound work. Existing opportunity and treat
codecs remain distinct and preserve the existing mixed-family rejection.
The bounded implementation is specified in
`docs/superpowers/plans/2026-09-06-t070-accepted-diagnosis-alternative-commands.md`.
The subsequent local GM response and kind-specific safe-repair boundary follows
`docs/superpowers/plans/2026-09-06-t070-alternative-treatment-response-repair.md`.
It preserves complete author/decline drafts, rejects raw nonempty authorings at the
pre-write distributor until fresh authority exists, and distinguishes an alternative
public packet from construction semantics. The approved repair contract requires
`candidateKind` on every packet kind, so the following shared packet change must update
all producers, strict consumers, documentation/examples and guards in one current-format
cutover, without a tagless compatibility path. Public persisted format
validation cannot rehydrate the private rejected-response authority required by live
retry. A rejected draft must never be staged as an accepted command just to reuse
opportunity-only capture; the fresh transient-request adapter remains mandatory.
The reviewed local response checkpoint is `da6f5c64..6b94bdac` (2026-09-06).
The executable repair detail is
`docs/superpowers/plans/2026-09-06-t070-alternative-repair-projection.md`.
It phases raw/semantic validation to preserve original array coordinates, narrows
dependent pairing diagnostics before granting edits, and permits a checked local
intermediate correction to expose the next diagnostic phase. Intermediate progress
is not a completed repair or fresh authority; a later private adapter must refresh
the rejected draft's semantic fingerprint before minting its next repair packet.
Private aliases are scrubbed without erasing unrelated authored IDs or valid siblings.
The public packet can be inspected while its unsupported live capture/retry remains
explicitly closed. Shared packet-tag changes require the afterlife documentation
controls even though alternative treatments themselves remain Mortal-only.
The reviewed repair checkpoint is `af5e94a3..0baceaf5` (2026-09-06): all four packet
kinds now carry the mandatory discriminator; strict local correction/public transport
and unsupported live-wave gates are implemented and independently approved. Actual
controls: pure456/456, Integration60/60, afterlife120/120, FullValidation1855/1855;
the final selector-test refinement additionally passes its entire class74/74. The
FullValidation result precedes the final root-guard refinement, independently covered
by focused RED/GREEN; it is not misrepresented as a final-tree broad run.
The scalar-course checkpoint `a393a4a0..3c0c77d1` (2026-09-06) is independently Spec
compliant / Quality approved and parent-accepted. It implements exact scalar selection,
pointer/history/resource lifecycle and course-milestone exclusivity through existing
accepted-command persistence, fresh rehydration and the sole atomic publisher. Both
original stale-history test bodies remain unchanged. Authority-dependent fixtures move
to Integration with their assertions preserved; pure projection remains Fast. Parent
audited all28 actual artifact sets and the complete diff; final corrective owners pass
pure42/42 and Integration46/46. The recorded Fast2655/2656 fail-fast control is incomplete,
although its create diagnostic fixture is now covered by focused GREEN. The mandatory
legacy RED is not hidden or reclassified as success.
The recovery-publication checkpoint `48911240..189ba934` (2026-09-06) is independently
Spec compliant / Quality approved and parent-accepted. Checked ordered recovery points
now publish through the existing single pipeline, with no implicit threshold tick,
severity change or healing. Parent audited the complete nine-file change and all19
actual artifact sets. Final Focused pure24/24 and split Integration12/12 +62/62 prove
the exact required74-row Integration union; final builds/cleanup are clean. The earlier
ten-minute combined control is explicitly incomplete, not a passing run. One redundant
test assertion is retained as Minor M1 for final whole-branch review.
The selective-removal checkpoint `51c97c24..58f29a5c` is independently Spec compliant /
Task quality Approved and parent-accepted. One batch now performs ordered selective
causal-lineage termination, authenticated0/0 removal and mixed severity reduction.
Shared generation validation preserves the exact canonical parent even when its
effect is already terminal and carrierless, without reviving or rewriting history.
Parent audited the complete22-file change and all24 actual C# artifact sets; exact
Integration24+52=76 and relevant pure controls pass. Mortal and afterlife worked
guidance/guards are synchronized; no new spiritual gameplay was introduced.
Fast4110/4211 and FullValidation299/300 are incomplete due to independently confirmed
stale snapshot/section-reader fixtures, not claimed green. Next execute
`docs/superpowers/plans/2026-09-06-t177-verification-fixture-alignment.md` on this branch.
Review Minor M1 (misleading no-batch rejection wording) is retained for the later
direct-addition finalizer change/final triage. Full remaining outcomes, heal-legacies,
fresh diagnosis/alternative authority and T070/T177/#1536 closure remain required/open.
The T177 fixture checkpoint `b4f177c9..c7301340` is independently Spec compliant /
Task quality Approved and parent-accepted. Parent inspected the two-file diff, review
and all four actual artifact sets. The named worked-example owner passes1/1 and
FullValidation passes all1856 rows in11 TRX with clean builds/cleanup. Focused
requirement175/235 and incomplete Fast4168/4211 expose the same unchanged strict
test projection omitting production SkillId, with all60/43 failures parent-confirmed.
Task2 in the same fixture-alignment plan now owns exact test-view/member/value
alignment, with no production or GM contract change and no repeated FullValidation.
The required unfinished legacy source remains explicit; T070/T177/#1536 stay open.
The follow-up T177 test-view checkpoint `580f682e..9f111e7b` is independently
Spec compliant / Task quality Approved and parent-accepted, with no findings.
Exact SkillId shape/value and separate capability/skill fingerprint assertions now
pass all236 requirement rows, also green across both Fast shards. Parent audited
both artifact sets and the one-file diff. Fast5627/5628 remains partial on an
unchanged console-input1second registration timeout; one source-targeted diagnostic
passes1/1 in41ms without changes. Load-sensitive timing is suspected, not claimed
fixed; preserve this for final T177 triage. No production or GM contract changed.
Next execute Task1 of `docs/superpowers/plans/2026-09-06-t070-complication-addition-publication.md`:
one symbolic graph/common canonical rules, exact ordered cumulative pre-roll checks,
preserved typed policy applicability and no preselection runtime identity allocation.
Task2 subsequently binds only selected direct additions through T067 and the one
atomic effect publisher. Full policy publication/heal-legacies and the remaining
Mortal/spiritual/healing/UI scope remain open; the legacy choice is not inferred.
The local GM parser must also preserve the approved dialect boundary: GM operations use
offered `complicationRef`, reject canonical `complicationId`, and do not acquire world
authority through a standalone canonical member parser. The response task's shared
selector-shape design and complete distinct typed draft model are specified in
`docs/superpowers/plans/2026-09-06-t070-gm-treatment-draft-model.md`; no placeholder
canonical identity or narrowed treatment operation is permitted. Private diagnosis check/cost authority,
alternative evidence/request/decline lifecycle and attainable hidden-path proof require
explicit executable contracts before the subsequent live vertical slices.
The same recognition change must also gate persisted treatment-catalog ingestion,
pending repair capture, cold treatment replay and opportunity-only repair retry;
none may silently drop the retained new family. Shared full-member fingerprints keep
the existing route/path domain/version semantics, while the independent wire seal
uses `book_of_eternity.wound.accepted_transition_wire`, version `1`.

T060/T066 use one production-owned, side-effect-free requirement boundary:
`MortalWoundTreatmentAuthority.ResolveRequirements(route, context, currentSnapshot)`.
`context` is a typed exact binding for the Mortal realm, target, provider, and current
location. Provider and target are generic typed actor coordinates: for procedure/course
either can be a player, named NPC, exact combatant, or exact combatant member, and the
two coordinates may be identical for self-treatment. Combat coordinates are accepted-
root identities, never display names or array indices.
`currentSnapshot` is a typed immutable projection assembled from the existing
canonical item identity/carrier, resource owner/ledger, actor skill/capability, location,
facility, quest, effect, and environment authorities; it is neither persisted nor
GM-authored. Its detached
entries carry exact ordinal identities, realm/owner/location bindings, current
lifecycle/availability/reservation or state values, and display text only as
non-authoritative diagnostics. Actor entries additionally carry exact reachability and
target-specific treatment consent.

The production T066 adapter does not accept lookalike fields from a signed file merely
because they match the transient projection. It uses this exact projection policy:

| Transient row | Canonical source and mapping |
|---|---|
| Item | Exact active current item carrier plus item identity/current-transition quantity agreement. Only actor carriers project: `player_inventory` maps its catalog owner `player` to `(player, player_current)`, while `npc_inventory` maps to `(npc, exact NPC ID)`. Valid location/offscreen-storage/vehicle/other non-actor carrier siblings are omitted and cannot satisfy an actor-owned treatment requirement. `count` is the canonical positive stack quantity; before T068, `availableCount=count`, `reservationState=available`, `lifecycle=active`, and `active=true`. Retired identities without a current carrier are not projected. |
| Resource | The validated definition/state/history quartet plus recomposed persisted owner authority. Project only `mortal_world` integer resources owned by `player|npc|combatant|combat_group_member`, translate the last kind to `combatant_member`, and require a non-negative integral `current` within signed-32-bit range. Valid decimal, foreign-realm, non-actor, negative, or out-of-range sibling rows remain valid canonical data but are omitted. Before T068 an active row uses `availableValue=currentValue`, `reservationState=available`, `lifecycle=active`, `active=true`; a suspended row uses zero availability, `reservationState=available`, `lifecycle=active`, `active=false`. |
| NPC actor | Exact canonical NPC membership supplies identity/lifecycle. Exact `NPCsInScene` membership at the agreed current location alone supplies `reachable=true` and presence. A known NPC outside that array remains projected with its canonical location and `reachable=false`; it is not absent or invalid merely for being off-scene. |
| Combat actor | Exact signed current enemy/ally roots and their canonical combat identity/promotion agreement supply `combatant|combatant_member` identity and current-scene presence. Wound occurrences never create combat actors. |
| Player/NPC skill | Current active/passive catalog membership supplies `lifecycle=active` and `active=true`; no source `lifecycle`, `active`, or test-only `tier` field is read. Player active tier comes from the exact matching canonical mastery row, NPC active tier from its canonical `currentMasteryLevel`, and passive tier from `masteryLevel`. A production-valid current active skill without applicable mastery remains a valid skill/capability source but emits no `skill_tier` row; no zero or favorable tier is invented. |
| Regular quest | Every exact retained `quests[]` member maps canonical `status` to transient `state`, with `lifecycle=active` and `active=true` even for completed/failed terminal status. |

Current-location identity must agree with the world map and location identity index;
player presence, exact `NPCsInScene` membership, and signed current combat roots compose
co-presence. NPC `reachable`, location `presentActors/resources/facilities`, wound
occurrences, and other unregistered shadow fields grant no authority. Malformed canonical
collections fail the accepted-state export; a valid non-projectable sibling does not.

Facility, environment, and consent use three closed version-1 registered members of the
already canonical current location's location `customStates[]`:
`mortal_wound_treatment_facility`, `mortal_wound_treatment_environment`, and
`mortal_wound_treatment_consent`. These rows are GM-authored setting semantics but are
strictly shape-validated and re-bound by the client. Their exact IDs and environment
states are world-specific; there is no universal facility, medicine, environment, or
wound catalog. Facility IDs, environment IDs, and consent refs are exact/confusable-
unique inside their respective kind; two distinct environments may legitimately share
the same exact state value. Reserved treatment kinds in link `customStates[]` are invalid.
Absence means no current authority. A retained facility with
`available=false` and a retained consent with `status=withdrawn` are valid negative
predicate evidence. Membership in the exact current location supplies active lifecycle;
moving/removing the row makes the old reference absent rather than silently portable.

The GM authors these rows only through existing location routes. A known location uses
`worldMapUpdates.locationUpdates[]` and supplies the complete replacement
`customStates[]`, preserving every unrelated existing member. A newly selected location
uses its complete `currentLocationData` creation envelope; a newly materialized remote
location uses `worldMapUpdates.newLocations[]`. Treatment rows never use
`currentLocationData` as a full resend for a known location and never belong to a link.

Tests and integration adapters obtain those types only through the production-owned
`ParseContext(json, path)` and `ParseSnapshot(json, path)` entry points on
`MortalWoundTreatmentAuthority`. They return `IsValid`, ordinary `ValidationIssue`s,
and respectively one nullable typed `Context` or `Snapshot`. Both parsers validate a
recursively closed version-1 transient projection, reject impossible available-versus-
total numeric bounds, and return detached externally immutable values; no
test or caller may deserialize an arbitrary JSON fixture directly into a future runtime
type or retain `JsonNode`, `JsonElement`, or `JsonDocument` anywhere in the parsed typed
graph, and no caller may pass `JsonObject` to `ResolveRequirements`. The transient JSON
exists only as a strict adapter/testing seam for already-authoritative canonical exports
and is never a second persisted game-state or GM response contract.
`Context` also carries private parser-origin provenance bound to its canonical fields;
`ExportCurrent` recomputes and requires that provenance and `schemaVersion=1`. A directly
constructed, copied-with-mutated-fields, wrong-version, or missing-provenance value is
invalid even when its visible properties look source-shaped.

The resolver returns an externally immutable detached
`MortalWoundRequirementAuthorityResult` containing
only `Success`, frozen `Issues`, ordered `ResolvedRequirements`, and a production-owned
`AuthorityFingerprint`. Each resolved row repeats the zero-based `RequirementIndex`,
`Kind`, exact `AuthorityRef`, `Realm`, applicable `OwnerKind`/`OwnerId`,
`ProviderKind`/`ProviderId`, `TargetKind`/`TargetId`, and `LocationId` coordinates, and
applicable `RequestedQuantity`, `MinimumTier`, `CurrentTier`, and `CurrentState`, plus
its production-owned `AuthorityFingerprint`; every inapplicable coordinate is null.
It resolves all route requirements conjunctively and rechecks
every reference against the supplied fresh snapshot using ordinal identity; display
names, case folding, Unicode-confusable aliases, previous identities, and cross-realm
rows are never fallback keys. Inactive/retired, unavailable/reserved, insufficient,
wrong-owner, unreachable, unconsented, wrong-location, absent-target, or wrong-state
authority fails closed. Repeated requirements for the same exact item/resource authority
are accumulated in route order and their total demand must fit the one current available
amount; individually satisfiable predicates cannot overbook the same authority.

The result `AuthorityFingerprint` binds a versioned domain, snapshot token, the complete
ordered canonical requirement predicates, exact context coordinates, ordered resolved
rows, and ordered issues/current mechanical authority values. Changing requirement
quantity/order/reference, context, lifecycle, owner, availability, tier, reachability,
consent, presence, or state changes the fingerprint. Display names and other explicitly
diagnostic projection text are excluded, so diagnostic-only wording changes do not
change it. Each row fingerprint binds that row's exact authority coordinates and current
mechanical values, its requirement index/kind/reference/requested predicate values, and
all applicable exact context coordinates under its own production domain.

The unchanged T060 resolver emits no reservation, item mutation, resource mutation,
treatment transition, or consumption intent. T066 wraps successful rows in complete
typed success witnesses. T068 alone creates pre-resolution reservation authority and
later typed consume/release intents, so any T060/T066 failure leaves all game state and
all reservation registries untouched.

T061/T066 add a separate `MortalWoundTreatmentCapabilityAuthority`; they do not widen
or reinterpret the completed T060 method, parser, resolved row, or result surface. A
guaranteed route is backed by an optional closed
`mortalWoundTreatmentCapabilities[]` extension on the exact current Mortal active or
passive skill row owned by the selected player/NPC actor. A skill carrying this
extension must have one permanent exact `skillId`; legacy/idless skills remain usable
for ordinary `skill_tier`/`source_capability` requirements but cannot prove guaranteed
treatment. Each extension row has `schemaVersion=1`, one exact/confusable-unique
`capabilityRef`, exact `woundDomain=physical`, inclusive severity ranks I-IV, and a
closed guaranteed-operation envelope. The envelope bounds stabilization, recovery
points, severity-reduction steps, removable complication kinds, severity-I healing,
and aggregate cosmetic/mechanical-effect legacy counts across the whole ordered result;
it cannot authorize a new complication, deterioration, owner/domain change, reopening,
or any operation outside the closed treatment language.

Player and NPC skill validators, their accepted-state composers, and the canonical
normalizer validate this optional extension in place. A production export adapter reads
only the exact currently present owner/skill row and returns immutable
`MortalWoundTreatmentCapabilityProof` containing owner kind/ID, skill kind/ID,
capability ref, domain/rank/envelope values, source semantic fingerprint, and one
proof fingerprint. Missing/idless/confusable/duplicate/wrong-owner, removed,
wrong-domain/rank, or changed source rows fail closed. A route requiring a mastery tier
uses a sibling `skill_tier` requirement resolved by unchanged T060 rather than a second
tier authority here. The strict JSON parser used by
unit fixtures can validate shape but never grants canonical provenance; only the
player/NPC canonical export factories can create an accepted proof. Issue #1533 must
preserve and adopt this wound-specific extension when it later unifies the complete
Mortal skill lifecycle; #1536 does not pretend that broader skill refactor is complete.

Every extension-bearing skill's permanent `skillId` is exact and case/Unicode-
confusable unique against every current active and passive skill row for the same actor,
including non-extension rows. The capability ref is likewise exact/confusable-unique
across both skill kinds. Idless ordinary rows remain legal but can never carry or prove
the extension.

The extension-bearing current skill is also the sole source of the ordinary unchanged
T060 `source_capability` row. T066's canonical snapshot composer deterministically emits
that existing four-field capability projection (`capabilityRef`, diagnostic skill
`displayName`, current `lifecycle`, current `active`) without guarantee data; missing,
duplicate, idless, inactive, or ambiguous skills cannot yield it. T060 remains unchanged
and resolves that row, while the separate exporter proves the guarantee from the same
exact skill identity.

T061/T067 add a separate production-owned treatment-resolution boundary. Tests locate
and invoke only public-to-the-assembly factories on `MortalWoundTreatmentPlanner` and
the resolver result; they do not instantiate evidence records, implement fingerprint
writers, select outcome bands locally, or claim terminality.

`MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(fs, writeLease, context,
woundId)` is the sole new-attempt production adapter. Under the active canonical lease
it revalidates the strict production-parsed selection context against the live turn,
derives the binding/events internally, and strictly reads
the unchanged T060 authority projection, world minute, player/NPC skill sources, the
accepted combat-actor treatment projection,
accepted effect mechanics, wound carriers, wound identity, and wound history. The
selected current wound/owner/fingerprint and complete history fingerprint are part of
the authority. Absolute root identity and current revision remain runtime-only admission
guards; serialized fingerprints use stable session generation/session/request/snapshot/
turn plus semantic context and canonical file fingerprints, so restart/root relocation
does not invalidate durable evidence. No raw JSON/dice/modifier factory exists.

The unchanged-shaped T060 actor projection accepts exact current
`player|npc|combatant|combatant_member` coordinates for procedure/course treatment;
combat actors are exported only from the accepted combat root and existing promotion
authority. A combatant wound can therefore be treated in place. Guaranteed capability
proof still comes only from a canonical player/NPC skill: a provider-owned guarantee may
target a combatant, while `actorRole=target` requires the existing accepted promotion/
persistence transition first.

`MortalWoundTreatmentPlanner.CreateAttemptCoordinates(acceptedState, before,
operationKey, routeId, eventRef)` is used only for a new operation. It rejects unless
`before` is the authority's exact selected current Mortal wound, target/owner/context
agree, and the later new-attempt history parse equals the authority's canonical history
fingerprint. It derives and freezes stable session generation, session/request/snapshot/
turn, realm/provider/target/location, context/accepted-state seals, wound/route/event,
expected-before fingerprint, operation key, and attempt ID. The event ref must resolve
exactly once in `binding.AcceptedEvents`; kind, authority ID, and semantic fingerprint
are copied into the coordinate seal. The exact operation key must be unique across the
reconstructed typed command/pending/history attempt set. Production then calls one
of `SealProcedureRequest(MortalWoundTreatmentAttemptCoordinates,
MortalWoundProcedureCheckAuthority, MortalWoundTreatmentRequirementAuthorityBundle,
MortalWoundTreatmentResourceReservationAuthority, WoundMaterializationEnvelope)`,
`SealCourseMilestoneRequest(MortalWoundTreatmentAttemptCoordinates, int,
MortalWoundCourseModeAuthority, MortalWoundTreatmentRequirementAuthorityBundle,
MortalWoundTreatmentResourceReservationAuthority, WoundMaterializationEnvelope)`, or
`SealGuaranteedRequest(MortalWoundTreatmentAttemptCoordinates,
MortalWoundTreatmentCapabilityProof, MortalWoundTreatmentRequirementAuthorityBundle,
MortalWoundTreatmentResourceReservationAuthority, WoundMaterializationEnvelope)`.
Each returns an immutable
`MortalWoundTreatmentAttemptRequestResult` (`IsValid`, frozen `Issues`, nullable
`Request`). The resulting closed
`MortalWoundTreatmentAttemptRequest` contains exactly mode, coordinates, nullable course
ordinal, the canonical detached pre-attempt `RouteSourceWound` plus its recomputed
`RouteSourceWoundFingerprint`, immutable typed mode authority, full requirement authority,
pre-resolution resource authority, and one `RequestFingerprint`. The sealer rejects unless
the detached wound fingerprint equals `ExpectedBeforeFingerprint` and its exact selected
route fingerprint equals the requirement/resource authorities; the request fingerprint
binds that source fingerprint. Course mode authority is the complete
`MortalWoundCourseModeAuthority`, not bare game time. Repair/retry restores and reuses that original sealed request; it
never rebuilds coordinates from the current after-state. The planner exposes:

- `PrepareProcedureRequest(acceptedState, history, before, operationKey, routeId,
  eventRef)`;
- `PrepareCourseMilestoneRequest(acceptedState, history, before, operationKey, routeId,
  eventRef)`;
- `PrepareGuaranteedRequest(acceptedState, history, before, operationKey, routeId,
  eventRef)`.

Each is the exact six-argument public-to-assembly request factory and returns
`MortalWoundTreatmentAttemptRequestResult`. It selects the route and creates coordinates
itself. Procedure/guaranteed build the full requirement bundle, matching mode authority,
resources, then request; course builds game time and complete course-mode/start authority
before its milestone-bound bundle, resources, and request. Any failed stage releases
all provisional claims; no caller supplies requirements, witnesses, baseline, dice,
reservation, or fingerprint. The resolver entry points are:

For procedure specifically, the satisfied requirement bundle is followed by the
production-owned all-band current-applicability simulation before
`MortalWoundProcedureCheckAuthority.Create` can reserve dice or a Fate candidate. Thus an
unusable partial/failed branch rejects without exposing or advancing the accepted roll
pool.

```csharp
MortalWoundTreatmentResolutionResult CreateProcedureAttempt(
    MortalWoundTreatmentAttemptRequest request,
    WoundHistoryParseResult history,
    WoundMaterializationEnvelope? before,
    MortalWoundTreatmentAcceptedStateAuthority? acceptedState)

MortalWoundTreatmentResolutionResult CreateCourseMilestoneAttempt(
    MortalWoundTreatmentAttemptRequest request,
    WoundHistoryParseResult history,
    WoundMaterializationEnvelope? before,
    MortalWoundTreatmentAcceptedStateAuthority? acceptedState)

MortalWoundTreatmentResolutionResult CreateGuaranteedAttempt(
    MortalWoundTreatmentAttemptRequest request,
    WoundHistoryParseResult history,
    WoundMaterializationEnvelope? before,
    MortalWoundTreatmentAcceptedStateAuthority? acceptedState)
```

The exact authority factories used by those request sealers are:

- `MortalWoundProcedureCheckAuthority.Create(coordinates, route, before,
  requirementAuthority, acceptedState)`;
- `MortalWoundGameTimeAuthority.Create(acceptedState, coordinates)`;
- `MortalWoundCourseModeAuthority.Create(acceptedState, coordinates, before, history,
  gameTimeAuthority)`;
- `MortalWoundTreatmentCapabilityAuthority.ExportCurrent(acceptedState, coordinates,
  capabilityRef, actorRole)`.

They return respectively `MortalWoundProcedureCheckAuthorityResult`,
`MortalWoundGameTimeAuthorityResult`, `MortalWoundCourseModeAuthorityResult`, and
`MortalWoundTreatmentCapabilityProofResult`; each has exactly `IsValid`, frozen
`Issues`, and nullable typed `Authority`/`Proof`. `CreateAttemptCoordinates` analogously
returns `MortalWoundTreatmentAttemptCoordinatesResult` with nullable `Coordinates`.
None accepts a caller fingerprint, and there is no generic proof parser or constructor.
The publication-only sibling
`ExportForPublication(acceptedState, coordinates, capabilityRef, actorRole,
AcceptedMechanicsPlan publicationPlan)` returns the same proof-result type. It reads a
strictly validated final composed player/NPC skill after-image whenever that canonical
root is touched by the plan; otherwise it uses the lease-bound current root. T070
requires exact equality with the sealed proof, closing same-turn skill removal or
capability mutation before commit.

Course-mode disposition is exactly
`TooEarly|Ready|DeadlineExceeded|InvalidAuthority`; only ready/deadline-exceeded carry
authority. That authority seals game time, course ID/ordinal, due/deadline/window,
complete `CourseStartAuthority` with the full typed starting wound, and coordinate/
accepted-state fingerprints. Its separate course-coordinate fingerprint binds course
ID/ordinal, wound/route, and the complete start-authority fingerprint for recomputation
through bundle/resource/request/history. Too-early returns before requirement or resource work.

For a new request the route is selected by exact `request.Coordinates.RouteId` from
`before`; no detached
caller route, result category, band, total, margin, course ID, terminal flag, or outcome
is accepted. Each factory internally invokes the applicable fresh requirement authority,
derives every route/attempt/result seal, and delegates pure semantics to one internal
`MortalWoundTreatmentResolver`.

The full canonical request, including its nested complete requirement bundle and
resource authority, is persisted first in the client-owned typed `treat`
authority in `wound_commands.json` and any bounded pending/repair packet, then copied
verbatim into accepted `transitionResult.requestAuthority` in wound history; no second
sibling bundle copy exists. The three parsers share strict serializers and
independently recompute coordinate/mode/resource/request plus bundle context/accepted-state/
route/common/course/scoped-row/non-overbooking seals, result/receipt fingerprints, and
all enclosing-row agreements. The detached receipt projects only the already-verified
bundle fingerprint and cannot be reused as resource authority.

The pending copy is a private top-level `submittedTreatmentRequests[]` collection, not a
new GM repair-candidate kind. It is composed only alongside a real non-empty wound-repair
wave from the already parsed/recomposed command root, retains exact outer coordinates and
the full detached typed request, and is absent otherwise. Public repair packets never
expose it.

Command and pending storage may hold the same request during one repair wave. Parsers
and dice/Fate/resource registries coalesce byte-semantic exact copies by
`(OperationKey, AttemptId, RequestFingerprint)` into one logical request/claim. A
different request, nested bundle/resource authority, or outer coordinate under a reused
coordinate conflicts. After exact-copy coalescing, operation key, attempt ID, accepted
event ref, and every non-null `(courseId, milestoneOrdinal)` are independently
exact/confusable unique; exact copies never double-reserve.

After a true cold restart from copied durable bytes under a different filesystem root
and fresh process-local registries, the typed command/pending authority restores the
full detached request, submitted operation key, attempt ID, and request fingerprint
without consulting current state or a cached canonical-root identity. The ordinary
mode-specific T067 `Create*Attempt(request, history, before, acceptedState)` reducer then
recomputes the exact full Resolution, including ordered `OutcomeIntents` and nullable
`CriticalReactionIntent`, from that request plus fresh canonical authority. A second cold
consumer passes only that typed request/resolution pair through the existing six-argument
T070 publication. Actionable intents are not serialized as a trusted command-result
shortcut: the persisted history-shaped result and every nested request/result seal are
recomputed, and any post-seal result-semantic replacement fails parsing or recomposition.
Accepted-state binding performs one atomic claim-recovery phase before any new live
procedure reservation. It consumes the catalog's existing first-authoritative order
(typed history transitions, then command rows, then pending exact copies after
coalescing), never reorders by dice coordinates, restores the carried exact spans, and
proves a player natural-1 Fate candidate reachable from the shared oldest-candidate
producer. Lower unoccupied spans may explain only roll-topology-compatible historical
claims. Their temporary span-to-effect mapping remains recovery-local; when a later
accepted span overlaps one, that overlap is the evidence that the earlier temporary
reservation was released. Fresh validation precedes the sole atomic registry swap, so
virtual claims never become live. A second exact recovery is idempotent, a changed set
conflicts, and a recovery attempted after live reservation rejects.
Failed persistence or rollback restores the
original command/pending/history bytes, releases provisional die/Fate/resource claims,
and permits one exact retry without duplicate claims or intents. The planner first
calls `WoundHistoryParseResult.ProbeTreatmentAttempt(operationKey, attemptId,
requestFingerprint)`, which returns exactly `NotFound|ExactReplay|Conflict|InvalidHistory`,
frozen issues, nullable detached restored request, and nullable detached original typed
receipt. An invalid parse returns `InvalidHistory` with its parse issues; stored
seal/row inconsistency does the same and dominates other outcomes. Exact replay returns
request plus receipt before dereferencing nullable current wound/accepted state or
building any live authority,
requirement, resource, clock, or capability state and emits no intents; this is required
because accepted work may already have consumed a dose or changed the wound/capability.
A conflicting coordinate reuse stops at the same boundary. Only `NotFound` reaches
fresh before-state and requirement resolution. Replay comparison uses the original full
sealed request/result semantics, never just a carried fingerprint or current after-image.

The internal production bundle factories are exactly
`MortalWoundTreatmentRequirementAuthorityBundle.CreateForProcedure(acceptedState,
coordinates, before)`, `CreateForGuaranteed(acceptedState, coordinates, before)`, and
`CreateForCourseMilestone(acceptedState, coordinates, before, history,
MortalWoundCourseModeAuthority)`. Procedure/guaranteed return
`MortalWoundTreatmentRequirementAuthorityBundleResult` with exactly `IsValid`, frozen
issues, and nullable full authority, producing it only for satisfied common requirements.
Course returns `MortalWoundCourseRequirementAuthorityResult` with exactly
`Status=Satisfied|Unsatisfied|InvalidAuthority`, frozen issues, and nullable full bundle
authority; only invalid has null authority. Each factory chooses the exact route/scopes,
calls unchanged T060 internally, and accepts no caller requirement array, snapshot, or
fingerprint. Non-invalid bundles contain unchanged successful T060 row bindings plus
typed failure witnesses; invalid authority returns no bundle.
`Unsatisfied` means a trustworthy current canonical export proves that
an exact previously required row is now absent/consumed/unavailable, a capability is
inactive, consent is withdrawn, the provider is unreachable/elsewhere, or another live
predicate is false. Malformed/ambiguous/confusable/cross-realm input, changed sealed
coordinates or predicates, non-contiguous history, and any fingerprint mismatch are
`InvalidAuthority`; they reject and can never manufacture an interruption.

Each new resolution embeds a sealed immutable
`MortalWoundTreatmentRequirementAuthorityBundle` rather than only a fingerprint. Its
exact fields are mode, context/accepted-state/route fingerprints, nullable course
ID/ordinal/coordinate fingerprint/status/reason, frozen scope authorities, and one
bundle fingerprint. Each common or course-milestone scope occurs once and stores a
nullable milestone ordinal plus `Satisfied|Unsatisfied`, successful
unchanged T060 row bindings with complete typed success witnesses, complete typed
mechanical failure witnesses, and its own fingerprint. Every authored local index appears
exactly once as binding or witness. A success witness is a closed kind-specific
non-display source/context slice containing every quantity/availability/reservation/
lifecycle/active/tier/state/reachability/consent/presence value needed by the unchanged
T060 fingerprint writer. History reconstructs and verifies the T060 row seal from that
witness before binding/scope/bundle seals; it never trusts an opaque carried row hash.
Failure witnesses use a closed kind-specific observation matching the complete success-
witness mechanical slice and exact reasons including absent, insufficient, reserved,
retired, inactive, state/location mismatch, unreachable, unconsented, unavailable, and
not-present. Only absent has no observation; reservation/lifecycle/current+required
location/total+available quantity and every applicable sibling predicate remain sealed.
They are not persisted diagnostics. Aggregate non-overbooking is evaluated
over successful rows across scopes. Every accepted course runs
the classifier after the too-early gate and stores its real `Satisfied|Unsatisfied`
status; `InvalidAuthority` rejects, and a deadline reason dominates simultaneous trusted
predicate loss.

All course coordinates/status/reason are null outside course mode. A course bundle has
all coordinates and real status; its reason is present exactly for accepted interruption
and null for an accepted milestone.

T068 is a two-stage typed authority. After the provisional mode authority but before
semantic outcome selection, production calls matching `PrepareProcedure`,
`PrepareCourse`, or `PrepareGuaranteed` with accepted state, coordinates, exact before,
the complete requirement bundle, and respectively procedure-check, complete course-mode,
or capability-proof authority. The overload resolves the
route and its closed policy internally, reserves every current quantity requirement in a
lease/generation registry, and returns an immutable reservation authority. It accepts no
route/policy JSON, caller quantity, or fingerprint. Too-early/invalid course input rejects;
ordinal-1 unsatisfied input rejects without a course; only an already-active trusted
interruption or satisfied scopes without quantities return `not_required`.

The closed policy requires reservation-before-resolution and fixed refund outcomes.
Its only mutation declaration is an exact scoped/milestone/indexed
`consume_requirement` selector resolving to one authored item/resource quantity. Thus a
reusable tool may be required/reserved without being consumed. `consumeOn` is a bounded
mode-valid category set; the resolver emits that category only when listed, otherwise
`None`. The reservation authority seals deterministic ID/disposition, coordinates,
accepted-state/route/bundle, nullable all-or-none course ID/ordinal/coordinate fingerprint,
policy, and typed quantity claims backed by success-witness fingerprints. Course values
must agree with both course mode and bundle. Request persistence confirms provisional resource/dice/Fate claims; any
failure releases all three, and restart reconstructs held command/pending claims without
treating finalized history as still reserved.

After resolution, `MortalWoundTreatmentResourceComposer.Finalize(resolution)` recomputes
request/resource/bundle/policy/claim seals, consumes only matching current-scope selectors,
and releases every other claim. `None` is release-only and `not_required` is empty.
Commit finalizes the reservation only as the last fallible semantic action of the full
accepted-turn transaction: after common publication/readback, helper validators, runtime
refresh, wound post-seal/output checks, critical/full-state validation, cleanup, and the
final runtime refresh. A private production-minted marker atomically takes the exact plan
into a one-use receipt; only that receipt may commit or re-arm the same vacant cache slot.
Terminal cancellation, validation rejection, or rollback first removes the exact durable
command/pending authority and then releases it. By contrast, retryable in-flight
publication compensation restores the bytes and durable command, retains the exact
`ConfirmedHeld` agreement, and re-arms only the same plan; it is not the lifecycle outcome
`rolled_back` and emits no refund reason. This makes concurrency, `consumeOn`, reusable
tools, downstream validation, and replay implementable without granting T061 raw mutation
authority or leaving a durable command without a hold.

T070-B.4 adds selected `item_quantity` without changing any frozen T067/T068 or public
publication surface. Freeze one closed treatment response envelope: the existing six skill
properties plus only `UpdateInventory`, `moveInventoryItems`, `removeInventoryItems`,
`NPCInventoryAdds`, `NPCInventoryUpdates`, `NPCInventoryRemovals`, and
`NPCEquipmentChanges`; reject every other non-null property. Extract a write-free
`MortalItemCanonicalProjectionPlanner` from accepted Mortal item normalization and an
ordered `MortalItemPublicationBaselinePlanner` for the complete root-touching tail. Its
current/backup and output dictionaries carry `JsonNode?`, retaining a null value for an
absent file,
preserving exact registered paths, file presence/absence, content, and top-level object/array topology.
Given the complete bidirectional carrier/command/index/companion root-path set, effective
post-location roots, the already validated route and transfer catalogs, the closed envelope,
and the exact `MortalItemAcceptedTurnNormalizationSnapshot`, the planners replay accepted
item transfer/materialization/equipment/identity and only later transforms that touch the
selected item graph, in exact production order: quest history -> NPC core -> conditional
NPC trade -> inventory items journal -> item bonds -> item text updates -> NPC item journals.
The planner exposes the exact base order as `TransformRegistry`:
`quest_history:v1`, `npc_core:v1`, `npc_trade:v1`, `inventory_items_journal:v1`,
`item_bonds:v1`, `item_text_updates:v1`, and `npc_item_journals:v1`. Its sole registry
dispatch loop calls `ApplyRegisteredTransform` once per entry and appends the returned
applied ID inside the loop. The baseline result exposes and fingerprints those
`AppliedTransformIds` as `quest_history:v1`, `npc_core:v1`, the disposition-matching
`npc_trade:apply:v1|npc_trade:skip_untouched_treatment_continuation:v1`,
`inventory_items_journal:v1`, `item_bonds:v1`, `item_text_updates:v1`, and
`npc_item_journals:v1`. `Apply` consumes and removes `UpdateNpcTradeInventoryReceipts`;
`SkipUntouchedTreatmentContinuation` leaves the post-NPC-core root and command unchanged
and emits no receipt. The list is not an after-the-fact report over separately ordered code.
The NPC-core input includes detached `NpcCoreChangesContract.Authority` plus exact NPC-trade
and training pending-file bytes, all sealed by semantic/byte fingerprint; an authenticated,
sealed `MortalItemNpcTradeTailDisposition` with exact values `Apply` and
`SkipUntouchedTreatmentContinuation` preserves the current treatment-continuation skip gate. Each
ordinary normalizer calls the same extracted pure transform. The sealed baseline is the
deterministic carrier/index state immediately before `PublishAcceptedMechanicsAsync`, not
the earlier output of `NormalizeMortalItemsAsync`; live roots must match it before common
publication. It excludes the B.2 treatment skill projection, which remains a common-plan
mutation applied to this verified baseline before item consumption. This prevents a
pre-normalization plan from rejecting or erasing valid same-turn work.

The item phase includes one shared pure `MortalItemTransferPlanner`: it derives accepted
transfers from complete detached backup/current roots, applies them before creation, and
removes their exact command rows. The accepted snapshot owns detached cloned proof DTOs,
fingerprints, exact root-path/presence/topology evidence, and derives creation root receipt/
create-transition IDs and transfer-transition IDs from session/snapshot/turn, exact route
or transfer authority, and ordinal. Creation ordinal is the exact production collector
order `UpdateInventory` -> NPC core -> NPC commands -> current location -> offscreen
storage, never lexical `creationRef` order. The ordinary normalizer and treatment projection
are forwarded the already validated route/transfer catalogs and snapshots and pass those same IDs
into the shared pure receipt/transition logic; neither rereads or rebuilds a catalog, invokes
the writing transfer service, or allocates a fresh random receipt/history row.

The frozen internal proof API is
`MortalItemCanonicalProjectionPlanner.ProjectionRootPaths` plus snapshot-owned
`CloneCurrentProjectionRoots()` and `CloneBackupProjectionRoots()`, each returning a detached
`IReadOnlyDictionary<string, JsonNode?>`. No alternate path registry or borrowed root map is
permitted.

On that baseline, a pure `MortalItemConsumptionPlanner` has exactly one entry point:

```csharp
internal static MortalItemConsumptionPlanningResult Plan(
    MortalItemConsumptionPlanningInput input);
```

The frozen input contains `Turn`, the exact upstream `BaselineFingerprint`, a complete
detached `MortalItemCarrierCatalogInput` including companion roots, the parsed
`MortalItemIdentityParseResult`, ordered commands, `ResourceDefinitionCatalog`,
`ResourceStateLedger`, one attempt-derived `ResourceSourceEvidence`, and the exact capacity
policy fingerprint. The planner clones JSON inputs and its fingerprint covers the entire
input/output graph. It does not accept `ResourceHistoryState`: registered capacity intents
flow into the existing common reducer, which remains the sole history/replay authority.
Invalid planning returns issues but no carrier/index after-image, identity transition,
capacity intent, or terminal owner subset.

The planner accepts only exact selected claims and processes one transition per
finalization intent in frozen order. Partial consumption
retains item identity/receipt/carrier and decrements count. Full consumption removes the
item, clears only supported inline equipment, records `consumed`, and returns a terminal
item owner. A container, quest, bond, or other companion reference requires its own genuine
atomic transition authority; B.4 mints none and rejects the complete plan. Explicit
transition IDs and all item-resource identities are derived
from request/result/finalization/claim/ordinal authority, never from caller input or a
shared allocation counter. The ordinary `MortalItemTransitionWriter` is refactored to use
the same pure planner before its own commit path, so treatment does not create a divergent
second item model.

For each partial transition, extract the existing proportional resource policy into one
pure item-owned-resource planner. Every live coordinate must have `instance_fixed`
capacity, and maximum/current must scale by `remainingCount/sourceCount` with exact decimal
and quantum representation. Add a private registered-capacity draft alongside
`IResourceRegisteredSystemOutcomeDraft`; `AcceptedMechanicsPlanner` includes those intents
without pretending they came from a GM resource command, and the treatment draft verifies
the exact applied/replay capacity transitions. Every partial capacity row uses one
attempt-derived origin, fixed registered-system priority `70`, and an invariant four-digit
zero-padded finalization ordinal in its event key, so the existing stable capacity sort
executes repeated claims in finalization order. Full consumption supplies the terminal
owner key so the common reducer retires all coordinates. Compose the final owner authority
from projected active items plus terminal historical item keys before final resource-state
agreement.

The B.2 treatment skill projector receives the supplied semantic final ordinary NPC root,
but its transaction still owns the distinct true live canonical before-image used for
write/rollback. Neither value may substitute for the other.

The existing `MortalWoundTreatmentResourcePublicationAuthority` privately owns the item
publication capability and seals the item-normalization projection, item-consumption
projection, capacities/terminal owners, exact before-images, and final owner authority.
Selected item consumption remains fail-closed unless that genuine authority is present.
The common input composer contributes its exact carrier/index after-images, registered
capacities, and final owner authority to the same B.3 plan/transaction. `npc_core.json` and
every selected companion root are composed in the strict order item phase -> quest history
-> NPC core -> conditional NPC trade -> inventory items journal -> item bonds -> item text
updates -> NPC item journals -> verified live pre-publication baseline -> sealed B.2 skill
projection -> item consumption. Only transforms touching the selected item graph are
included; untouched roots remain exact pass-through members of the sealed set. Candidate
admission allows overlap only through the genuine envelope, baseline, skill, and item
authorities, verifies the live final pre-publication roots before applying plan-owned
mutations, and independently re-derives the final active/passive skill catalogs. No global
subtree ignore or last-writer-wins rule is permitted.

RED coverage precedes each extraction/integration step: deterministic write-free item
normalization; partial and full resource-free stacks; exact/inexact resource-bearing
partials; sequential repeated claims; mixed selected item/resource publication; NPC
skill/item root composition; every tail sidecar; both legacy vehicle object and array
topologies; stale count/carrier/index/capacity; cold replay; and every
post-write compensation boundary. B.4 ends only when carrier/index/resource owner/state/
history/wound/output/command roots restore atomically on failure and exact replay produces
no second transition. Procedure/course/Fate/heal/recovery and T069-C remain later T070
contours. B.4 proves its publication/replay authority through guaranteed-treatment cases;
it does not require a procedure/course publication test to become GREEN.

Procedure route resolution has a closed `modifierSource`: exact zero/provider roll actor,
or one zero-based satisfied T060 `skill_tier` requirement whose current tier, exact
canonical `skillId`, and resolved owner become modifier, `RollSkillId`, and roll actor.
`fixed_zero` uses null `RollSkillId`. The check factory sums every active complication's
difficulty modifier, then asks the shared scope-aware resolver for accepted roll-actor
`roll_modifier` components matching the trusted `skill_check` context. Broad scope and
an exact usable focused scope pass; a different, missing, or unavailable skill does not.
Filtering precedes the unchanged reduction: same-direction sources collapse, opposing
sources cancel, and version 1 admits only normal/advantage/disadvantage. Normal claims one die; advantage/disadvantage claim
two and choose high/low, with the lower source index winning a tie. A generation-scoped
registry reconstructs all occupied indices from strictly valid full requests in typed
wound commands, pending packets, and history for the same turn, then provisionally
reserves the lowest contiguous free span. Exact retry returns its claim; successful
seal+persistence confirms it under the lease; failed sealing/persistence releases it.
The check authority stores ordered contribution IDs, source indices/rolls, selected die,
modifier, complication sum, effective difficulty, requirement/context/accepted-state
seals, and no caller total/margin/band. The resolver uses checked 64-bit total/margin and
applies hard gates before natural 20/1; pool exhaustion/overflow rejects.

Natural-1 treatment also preserves Fate Shield. The check factory uses a shared
production arbiter extracted from the existing `EffectAcceptedEventReportCatalog`
eligibility/oldest-selector logic and, only for roll actor `player/player_current`, seals
a nullable prepared effect/trigger/carrier-fingerprint candidate before Resolution. The
legacy GM five-argument Compose/leading-dice API remains unchanged. A typed overload
shares the accepted-state reservation registry: it reconstructs candidates already held
by full command/pending/history requests, excludes them, and provisionally chooses the
next oldest in deterministic transition order. Persistence confirms and failed sealing
releases the reservation, preventing two requests or a restart from claiming one shield.
The exact typed seam is
`EffectAcceptedEventReportCatalog.ResolvePreparedMortalWoundCriticalReaction(request,
acceptedState)`, returning `MortalWoundCriticalReactionResolutionResult` with exactly
`IsValid`, frozen `Issues`, and nullable `Intent`. Both this method and unchanged GM
`Compose(JsonNode?, int, string, IReadOnlyList<int>, EffectCarrierCatalogInput)` delegate
eligibility and oldest selection to one extracted `FateShieldReactionArbiter`.
The accepted-input coordinator rejects a typed procedure request combined with a legacy
GM `owner_critical_failure` report for the same accepted turn before either path confirms
a claim, using `wound_treatment_fate_reaction_cross_surface_duplicate` and releasing all
provisional claims. Legacy reports remain legal for non-treatment Mortal actions.
The typed method revalidates the prepared candidate and returns an immutable intent with
exact event type, deterministic per-attempt event/causal refs, turn/realm/player target,
effect/trigger/effect seal/prepared seal/request seal, and intent seal; it accepts no GM
report, raw dice, or carrier mutation. Absent
shield selects the last band as `critical_failure`; valid shield records
`critical_failure -> failure` and selects the first authored `failed_attempt` band.
Consumption still occurs if those indices coincide. Mode evidence/receipt stores the
closed original/resolved outcome, selected band/index, and nullable reaction triple.
T070 applies the shield consume/expire intent atomically with wound/result/history or
writes nothing.

Every route outcome uses a closed array of typed operations rather than colon-encoded
strings or an open payload. Version 1 admits `no_improvement`, `stabilize`, positive
bounded `add_recovery`, `reduce_severity` by one/two, exact existing
`remove_complication`, complete `add_complication`, exact current-policy
`apply_deterioration`, and `heal` with its complete bounded legacy drafts.
Canonical routes store `remove_complication.complicationId`, but GM proposals never
invent it: initial proposals use a same-proposal `complicationRef`, while alternative-
authoring packets expose bounded opaque existing-complication selectors through the same
field. `WoundResponseInputComposer` alone resolves/allocates and rewrites to canonical ID
before parsing; canonical refs in GM input and local refs in persisted state fail closed.
`add_complication` contains exactly the existing GM-safe wound sub-proposal with one
response-local complication and its current `consequenceDefinitions` wrappers; the
existing adapter derives application/operation refs, wound links, slots, fingerprints,
and IDs only if that operation is selected. A cosmetic heal legacy is its bounded
readable declaration. A mechanical legacy is exactly `kind=mechanical_effect` with a
bounded non-empty proposal-safe #1535 draft: response-local definition wrappers with
complete definitions and `links=[]`, plus response-local applications referring only to
same-draft definitions and parameters. Target/source/event/carrier state/fingerprints/
IDs are absent and derived. There is no open skill/trait/other materializer branch.
Missing/extra payloads invalidate the route before any roll. `no_improvement` is the sole operation in its
result. Every result has at most one `heal`, it is final, and its maximum eight legacies
are aggregate for that selected result. Operations are applied in order to a working wound state, and every possible
outcome must be legal under the current severity/slot/source envelope before a roll is
consumed. `heal` is legal only after earlier declared reductions reach rank I and emits
a separate canonical follow-up heal transition; legacies can materialize only through
that terminal transition, never as a free-standing mid-treatment token.

Recovery-point accumulation/application and signed-32-bit-to-64-bit capability aggregate
comparison are checked; overflow rejects before die consumption/publication. Aggregate
severity reduction is at most two without heal, or at most three only when the result's
final heal first requires the ordered working state to reach exactly I. T067 preserves
the existing non-heal reducer bound and updates the sealed follow-up-heal working-state
gate.

Every procedure row categorized `success`, including the first natural-20 row, must
structurally contain a positive kind and exclude no-op/harm kinds. Before a fresh
attempt, every row is independently simulated against the sealed current wound and must
be a legal complete transition; every success row must additionally make an actual
monotone improvement. A categorized no-op or harm-only result cannot complete a route,
and an inapplicable partial/failed row cannot be used to obtain a free reroll. The old
route remains structurally valid when the wound changes, but cannot start again until
all possible rows apply. Guaranteed singleton applicability is checked the same way.

Within a selected result, `add_complication` refs are exact/confusable unique across all
such operations. Their nested definition/application/operation refs are namespaced by
operation ordinal plus complication ref. Mechanical-legacy nested refs are namespaced by
`localLegacyRef`; separate drafts may reuse local spellings, but all namespaced refs,
permanent IDs, result-map keys, and working-wound/source coordinates must be collision-
free.

The exact immutable T067 -> T070 handoff is one
`MortalWoundTreatmentOutcomeIntent` per declared operation in identical order. Every
branch carries operation ordinal, kind, declared-operation fingerprint, and intent
fingerprint; scalar branches repeat their exact points/steps/ID/policy authority.
`add_complication` additionally carries deterministic complication ID and frozen local-
to-namespaced definition/application bindings; `heal` carries derived child coordinates
and frozen legacy seeds with ordinal/local ref/deterministic legacy ID/kind/namespaced
maps/fingerprints. T070 consumes this typed array plus matching typed `DeclaredResult`,
may build existing wound/#1535 batches, and may not parse raw route JSON or allocate or
rename identity. Cardinality/order/value/binding disagreement rejects before composition.

For a selected draft, the treatment identity allocator derives complication and legacy
IDs from the sealed request fingerprint plus operation/legacy ordinal and exact local refs. Wound effect roots
retain the completed #1535 handoff: the draft supplies response-local application refs
and the accepted effect planner alone returns permanent effect IDs. A mechanical
legacy's durable `legacyId` is also the source ID of client-only, non-public,
non-GM-materializable `sourceKind=wound_legacy`. Same-turn source authority is the sealed
T067 legacy seed plus immutable preparation; only after Finalize is its complete durable
reload authority the typed legacy history row linked to the terminal wound. Healing excludes this independent
source, and later dispel/removal never erases its history/provenance. Collision, missing
result-map entry, or local/permanent/source/history disagreement rejects the whole atomic
plan. T070 uses the sole
`MortalWoundHealLegacyPlanner.Prepare(binding, resolution, workingWound)` boundary to
produce immutable legacy draft bindings, a frozen ordered effect-operation batch array,
and one preparation fingerprint—no effect ID or durable history intent—before #1535
allocates effect IDs. The array contains exactly one `WoundEffectOperationBatch` per
mechanical legacy in legacy-ordinal order and none for cosmetic legacies; every batch
exports only that seed's independent `sourceKind=wound_legacy/sourceId=legacyId` source,
and an all-cosmetic heal has an empty array. The preparation seal binds the exact
one-to-one binding/batch/source/ref agreement. It then calls the sole
`MortalWoundHealLegacyPlanner.Finalize(preparation, acceptedEffectPlan)` boundary, which
recomputes the preparation/effect-plan seals and exact namespaced
per-legacy application-to-effect/materialization result maps before returning resolved
legacy bindings, durable history intents, and a finalization fingerprint. Missing,
extra, reordered, merged, or split batches fail closed.
The final `ApplicationResults` array contains one exact
`MortalWoundHealLegacyApplicationResultGroup` per mechanical batch in matching order,
with legacy ordinal/ID, source-export seal, ordered complete existing
`EffectAcceptedApplicationResult` rows, and a group fingerprint; cosmetic legacies have
no group.

The sealed request owns the primary treat coordinates. Fixed versioned derivations from
that request create one follow-up-heal child operation/transition/event/causal coordinate
and one distinct legacy-row coordinate per legacy ordinal/local ref. All must be unique
against sibling and durable operation/event/history coordinates; collision rejects the
atomic plan. Exact replay exits before regenerating any child row.

Course input is an immutable game-time authority produced only from canonical
`world_time.currentTimeInMinutes` plus the accepted session/request/snapshot binding;
tests may inspect it but cannot supply its value or fingerprint directly. The first
milestone derives one stable course ID from the same versioned deterministic identity
scope used for wound accepted-turn IDs and is legal only when `activeCourseId` is null.
Its complete course-mode authority stores a typed `CourseStartAuthority` with route/seal,
full detached starting wound plus fingerprint, start minute, and accepted-state/
coordinate seals. At start the planner simulates the complete ordinal sequence against
that baseline and requires an actual monotone improvement; structural parsing only
checks closed positive kinds. Later milestones reconstruct and verify that full authority,
current active pointer, route fingerprint, start time, prior contiguous ordinals, and the
next unused `(courseId, ordinal)` from durable history. A second course cannot start
while the pointer is non-null. The inclusive canonical-minute interval is `due=start+afterMinutes`
through `deadline=due+maximumGapMinutes`; exact deadline completion wins. Exact
precedence is replay/conflict, coordinate/history/clock integrity, too-early rejection,
fresh milestone-requirement classification, then resolution. The classifier still runs
for an elapsed deadline so malformed authority cannot fabricate interruption. Within the window a
`Satisfied` result accepts the milestone; an `Unsatisfied` result on an active course
accepts the declared interruption. After the deadline an otherwise valid active course
also interrupts; `deadline_exceeded` dominates `requirements_unsatisfied` when both are
true, while the bundle retains the actual trusted classification. `InvalidAuthority`
always rejects. Interruption consumes neither the
unmet/current nor future milestone. All clock/delay values are non-negative signed
64-bit and checked addition overflow rejects without accepting or interrupting.
Only an intermediate `completion=active` milestone may have an empty result. The final
milestone is non-empty, and the complete course contains at least one applicable positive
treatment operation, so route completion can never be earned by an all-empty schedule.
Every non-empty milestone result contains only monotone positive kinds;
`no_improvement`, `add_complication`, and `apply_deterioration` are forbidden outside the
separate interruption branch.
Positive is exactly an applicable state-changing `stabilize`, `add_recovery`,
`reduce_severity`, `remove_complication`, or `heal`; the complete ordinal sequence is
evaluated against the stored detached starting wound and each live milestone is rechecked
against current state. Procedure/guaranteed treatment may run during a course; heal,
final completion, or interruption clears `activeCourseId`, and any later milestone then
rejects.

Course interruption is deliberately non-beneficial: its category is exactly
`failed_attempt`, and its result is either sole `no_improvement` or only complete harmful
`add_complication`/`apply_deterioration` operations already legal for the current wound.
It cannot stabilize, add recovery, reduce severity, remove a complication, heal, or add
a legacy. This prevents waiting or deliberately dropping a requirement from becoming a
free positive treatment result.
An interruption complication is provably harmful only when its reused sub-proposal has
no consequence definitions and the sole complication has difficulty modifier 1-4; an
effectful/zero-difficulty row is invalid. A deterioration policy must be typed by T062 as
strictly worsening, never neutral or beneficial.

Guaranteed input requires both the matching unchanged T060 `source_capability` row and
the separate canonical `MortalWoundTreatmentCapabilityProof`. The sealed request holds
the proof originally selected; a genuinely new operation also requires a fresh current
canonical export with the same source/proof fingerprint. Exact replay may pass no fresh
proof because it returns before that gate. Exact actor role,
capability ref, actor owner, permanent skill identity, source fingerprint,
wound domain/rank, and every sibling requirement must agree. The route's singleton
success outcome must stay within every bound of the proof's guaranteed-operation
envelope, contain at least one applicable positive operation, and contain no
`no_improvement`, new complication, or deterioration; generic active capability
presence or a shape-valid transient JSON row is
insufficient. No roll or GM success field exists.

Immediately before publication the production composer exports the capability once more
and requires exact agreement with the accepted resolution. A removed/changed source
therefore rolls back the new atomic plan, while an already durable exact replay still
emits no plan and needs no live source.

Resolution returns immutable `MortalWoundTreatmentResolutionResult` with exactly
`Disposition`, frozen `Issues`, nullable `Resolution`, and nullable `ReplayReceipt`.
Only `Resolved` has the immutable resolution and one-shot intents; only `ExactReplay`
has the detached original receipt and no intents; `Rejected|Conflict` have neither. An
accepted resolution carries mode, exact coordinates,
`AttemptDisposition=AcceptedTerminal`, category, selected outcome/interruption,
mode-specific proof, a zero-based nullable selected outcome index, ordered typed outcome
intents, nullable immutable `CriticalReactionIntent`, policy-derived `ConsumptionTrigger`,
the full immutable `RequestAuthority`, embedded requirement-authority bundle and pre-
resolution resource-reservation authority, authority
fingerprints, and result fingerprint. It exposes no raw JSON or resource/item mutation.
All duplicated request/mode/bundle/resource/course fields agree exactly, and the result
seal binds the complete request so resource finalization is self-contained.
`SelectedOutcomeIndex` is the procedure band index, milestone ordinal minus one for an
accepted course milestone, zero for guaranteed, and null for interruption.
The reaction intent is non-null only for mitigated player natural 1, is sealed into the
result fingerprint, and reaches only T070; history/receipt store its audit fingerprint,
not actionable effect authority.
Rejected/cancelled/rolled-back work creates no terminal attempt. Treatment history rows
remain wound-lifecycle `terminal=false`; only a separate heal row is terminal.

The one exact detached `MortalWoundTreatmentReceipt` type is returned both by the history
probe and as outer `ReplayReceipt`. It preserves mode/coordinates, terminal attempt
disposition, category/index/interruption, immutable declared result, consumption trigger,
course coordinates/disposition, requirement bundle fingerprint, resource-authority
fingerprint, immutable mode evidence,
route/resolution/request/result/route-completion seals, and its recomputed receipt seal.
It contains no actionable outcome, requirement row, resource, wound, effect, history, or
publication intent. The history parser derives this same type from the complete stored
request/result, including after restart; no unnamed or second replay payload exists.

The resolver also derives route-completion intent. `completedRouteIds[]` is a monotone
first-completion audit, not a reuse lock: append once for a successful procedure, the
final completed course milestone, or an accepted guarantee; do not append for partial/
failed procedures, intermediate milestones, interruptions, or exact replay. A later
legal use receives a new attempt identity. `RouteCompletion=AppendOnce` iff the
qualifying success sees the route absent from the sealed before-image; a later success
for an already recorded route deterministically uses `None`.

`WoundHistoryState` stores and returns the closed typed `treat` transition result and
enforces exact semantic uniqueness independently for `operationKey`, every non-null
`attemptId`, and every `(courseId, milestoneOrdinal)` pair. Exact repeats return the
original receipt; reuse with changed semantics conflicts. `WoundTransitionReducer`
derives `WoundAttemptTerminalIntent` solely from a validated planner result and no longer
trusts caller-authored `WoundDeclaredTransitionOutcome.TerminalAttempt`. T068 alone maps
the immutable consumption trigger and resolved requirement rows into atomic mutations.

Bounded T070 follow-up prerequisite (2026-09-07):
`docs/superpowers/plans/2026-09-07-mortal-follow-up-heal-staging.md` implements
FR-064's Mortal physical III/IV-to-active-I treatment staging allowance only.
It preserves the spiritual/recovery limits and the independent terminal heal
validator. This two-file reducer/test task does not expose healing publication,
register legacy sources, or settle the unanswered legacy preparation decision.
Accepted at `ca29359c` after parent source/artifact inspection and independent
Spec Compliant / Quality Approved, zero open findings. Actual eight-row RED
(four intended bound failures/four preservation PASS), eight GREEN, and full
reducer 148/148 PASS prove this narrow exception. One Fast completed 6,093 of
7,388 discovery rows with one required unchanged legacy failure; arithmetic
1,295 uncompleted, not full GREEN. All runs used five-minute bounds and clean
build/cleanup evidence. The linked sub-plan records exact artifacts; T070 remains
open for actual heal/legacy publication and the separate preparation decision.

Accepted bounded T070 catalog prerequisite (2026-09-07), commit `690288f4`:
`docs/superpowers/plans/2026-09-07-wound-legacy-source-catalog.md` supplies a
complete-code source catalog/composer-guard plan with 28 new rows plus the
unchanged mandatory legacy source test. Registering the exact non-public kind
must be paired with rejection/filtering of generic caller exports. This neither
chooses the unresolved legacy preparation architecture nor enables canonical
legacy effects, history reconstruction or actual heal publication. Full T070 and
its GM/example synchronization stay open. Parent source/artifact audit and
independent Spec Compliant / Quality Approved found no defects. Semantic RED29
(23FAIL/six controls PASS) -> GREEN29, owners50/50; all28 new rows plus the
unchanged mandatory legacy test pass in Fast. One five-minute Fast completed6450
of7526 discovery rows, 6447PASS/three reflection-harness FAIL, arithmetic1076
uncompleted. No full GREEN claim; exact artifacts are in the linked plan.

The bounded canonical prerequisite is accepted through `642eb4e2` (BASE `d4a0a06c`):
`docs/superpowers/plans/2026-09-07-wound-legacy-canonical-vocabulary.md`.
The existing source-bound planner derives canonical `linkKind` from the exact
source kind, so canonical SourceKinds/LinkKinds must admit `wound_legacy`.
Authored legacy definitions still require `links=[]`; the broader definition
LinkKinds set remains unchanged. The source inventory exposed that distinction
before code, and the provisional definition-link expansion was removed from the
plan. Explicit non-public catalog/repair tests and authored-link rejection controls
preserve the existing authority boundary. Shared guide, worked continuation and
manifest distinguish this internal vocabulary from the unchanged eleven GM
selectors. Parent source/artifact audit and independent Spec Compliant / Quality
Approved found zero open defects. Semantic RED35/48 -> owning GREEN371/371
(all48 new rows); one Fast7,628/7,628 in3:23.368 and conditional
FullValidation1,857/1,857 in10:46.920 passed. The final manifest's two actual
consumers passed2/2 after duplicate-description cleanup; final source guard1/1.
All builds/cleanup clean, no timeouts/skips/duplicate IDs. Exact artifacts and
discovery/theory reconciliation are in the linked plan. This does not decide the
unresolved private preparation witness, durable recovery receipt or spiritual-art
schema, and does not complete legacy creation/history/reload/publication.

`docs/superpowers/plans/2026-09-07-mortal-recovery-current-history-guard.md` is a
separate T069-C admission prerequisite: after exact registry authority, read the
current history under the supplied active lease without recovery/reacquisition,
propagate malformed-history diagnostics or reject a valid semantic mismatch with
the signed accepted-state seal, then retain existing sealed-clock arithmetic.
Fresh/cold binding, missing/bad/changed history, no ambient context, equivalent
JSON/BOM and authority precedence are covered by real file-backed tests. Mortal
GM guidance, worked negative continuation, manifest and source guard stay aligned.
No durable recovery receipt/schema, publisher or spiritual contract is chosen;
full T069-C/T070/T074/T177 and the77/177 top-level count remain open.
This bounded admission guard is accepted through `f9cd385c` (BASE `b2bc29f8`):
new Integration16/16, owners42/42 and parsers/docs51/51 PASS. One Fast timed out
at5:00.289 after7,282 PASS; exact missing347 cases passed once in bounded Focused.
Parent inspected all actual artifacts and reconciled7,629 total cases against
7,575 discovery plus54 known runtime expansions, with no cross-run method overlap.
Fresh review is Spec Compliant / Quality Approved with zero source defects and
no open Critical/Important findings under the explicit development-checkpoint
policy correction in the bounded plan. Original Fast remains exit124; T177's
performance/final verification and PreMerge stay open. Three initial RED runs
overlapped, causing two MSB3026 and one MSB3101 warning; this historical Minor
process finding remains recorded for final review. All final owner builds and
cleanup checks are clean. Mortal-only documentation does not require FullValidation.

The bounded T177 verification prerequisite
`docs/superpowers/plans/2026-09-07-wound-cache-reflection-fixture.md` is accepted
through `7460800d` (exact BASE `9f3e1455`). Two ordinary test wrappers now send
the current null/null authority defaults; the empty-item registration wrapper
supplies the existing typed empty routes/transfers and independently cloned
projection-root inputs. No production signature/validator or assertion changed.
Parent inspected the source, exact diff and all actual artifacts; fresh review
is Spec Compliant / Quality Approved, zero findings. Fixture RED1/7 then5/7 is
followed by affected GREEN7/7, owner208/208 and one Fast7,580/7,580 in3:13.691
within five minutes. Fast's7,526 discovery entries expand by54 cases in seven
theories; parent method-level reconciliation matches every method. All runs have
zero build diagnostics/timeouts/duplicates and clean cleanup. No GM surface was
changed; no prompt/example/manifest/source-guard update or FullValidation is
needed. Detailed artifacts are in the linked plan; full T177/T070/#1536 remain open.

### Phase 3 — Spiritual conflict, arts, healing, and entity recovery

The live integration boundary is pinned in
`contracts/spiritual-wound-live-turn-boundary.md` (2026-09-08). Spiritual source
validation and GM decision continuation stay within one original player turn and
one final common publication. The draft preceding the decision is not accepted
narrative or canonically committed source. Retain a causally completed prefix,
original dice/resource coordinates and allocated identities; compose applicable
wound effects before validating a dependent later exchange. Separate spiritual
pending and append-only receipt roots preserve the closed Mortal schemas.

Next production decomposition is: detached conflict checker consumed by the
existing validator plus a genuinely zero-error signed fixture; source-only
resource/effect intermediate consumed by the existing common planner; strict
spiritual witness/finalization and same-turn continuation; decline then complete
materialize/worsen/re-trauma/persistent effects; all existing source families and
the remaining danger/defeat/optional-dissipation gates. The detailed contract
records exact lifetime, rollback, restart, multi-exchange and GM synchronization
obligations. A decline-only test is an internal milestone, not a reduced rollout.
Each unit still needs its complete-code execution plan and verification. This
planning update does not implement the producer, close T081/T084/T085, resurrect
historical Fast failures, or decide the independent T070 preparation question.

The first T081-A executable fixture is specified in
`docs/superpowers/plans/2026-09-08-spiritual-conflict-frame-baseline.md`.
It reuses the existing resource cutover fixture through an Integration partial,
with optional genuinely signed dice, real common publication and zero Error
issues from both the complete raw resource and selected final conflict phases.
Accepted at `a54fc3d5`, with final 2/2, unchanged-neighbor 47/47, clean sensitivity
RED/GREEN and independent Spec compliant / Quality Approved; parent inspected
the complete diff and exact artifacts. This is a test-only prerequisite, not yet
detached validation or wound-source admission. Existing code-selected resource
assertions remain intact. The
production extraction separately retains current arithmetic, offline fallback,
snapshot security and early-return read behavior; it must consume the new frame
through the current validator and receive its own complete-code plan/review.
That production step now follows
`docs/superpowers/plans/2026-09-08-spiritual-conflict-frame-extraction.md`
and its complete companion patch. It stages an executable pre-API RED before
28 new captured-input, repeated-state and real resource-failure rows; the two
accepted baseline rows remain. T081-A is accepted at `4cb7d774`: the parent
inspected the complete diff and actual nine-run artifact history; independent
Spec compliant / Quality Approved review has no open findings. Corrected 8/8,
complete frame 30/30, resource owner 71/71, spiritual owner 449/449 (445 discovered
descriptors) and one Fast 7753/7753 in 4:23.090 passed with warning-free builds,
complete cleanup and no timeout/skips/duplicate executions. The extraction plan
records the historical failures, rejected transient code and reconciled dynamic
theory accounting. No GM contract changed. T081-B..E and top-level T081/T084/T085
remain open; this is complete validation, not wound-source admission.

T081-B is further split into B1 ordinary reduction/assembly and B2 actual causal
execution. B1's complete three-file code/test plan is
`docs/superpowers/plans/2026-09-08-accepted-mechanics-ordinary-reduction.md`
with its companion patch. The production wrapper consumes a typed completed
ordinary result and exactly-once final assembly; existing receipt/replay/identity,
effect completion and Mortal publication behavior stay unchanged. Its six new
tests plus signed/pending/Mortal controls do not prove a source prefix. B2 remains
mandatory: retain the real scheduler, graph identities, ledger/history and causal
transcript state, advance prefix effects without terminal sealing, resume after
the authorized wound decision and recompose the unaccepted dependent suffix.
The plan records concrete source seams and stop/resume/restart/failure acceptance
requirements. No GM capability is exposed by B1; later runtime changes carry
their own synchronized prompts, examples, manifests and validation guards.

B1 implementation at `c9230065` awaits required pending-owner verification:
six new,423owning,two signed and four Mortal rows pass, but11pending rows expose
old array-form wound fixtures. T177's bounded two-test-file source correction is
specified in `docs/superpowers/plans/2026-09-08-pending-effect-fixture-sources.md`.
Existing registered skill sources replace only generic setup; runtime and every
receipt/replay assertion stay unchanged. The observed11-row failure is retained
as RED, not waived or called proof of B1 non-regression. B1 remains open for its
corrected cohort, Fast, actual artifact audit and independent review.

The corrected cohort now passes11/11 at `05a35ba6`, with the exact original names
and assertions. B1 review is Spec compliant / Quality Approved,0Critical/Important/
Minor. Its parent Fast executed2809 rows,2808passed and one browser preflight wait
timed out; the unchanged isolated browser diagnostic passed1/1. T177's bounded
physical ownership correction is specified in
`docs/superpowers/plans/2026-09-08-browser-action-test-ownership.md`: that real
canonical-file/session-replacement contention row belongs in RegressionIntegration
under the already approved taxonomy. Preserve all behavior and historical results;
B1 still requires the corrected full Fast gate and acceptance evidence.

B1 acceptance (2026-09-08): production `c9230065` and fixture `05a35ba6` are
independently Spec compliant / Quality Approved, no code findings. All required
Focused cohorts pass; exact pending11/11 is restored without assertion changes.
The bounded T177 browser/manifest correction `02aa0dbf..5b57534c` is independently
review-clean, retains the original browser Fact, and records existing spiritual
art Integration ownership (final inventories67/40). Actual parent Fast artifact
`20260908-051357-858-38248-99236dfb6b8748218d3425370e7c0d7f-fast` is7758/7758,
wall4:54.9235103 under the unchanged5m limit, clean build/cleanup, no skips or
duplicate executions/cross-descriptor IDs. All26 TRXs were inspected;7704 discovery
IDs expand through seven existing dynamic theories (54extra cases). Both reviews'
Fast evidence gaps are resolved; historical failures remain recorded in the
bounded plans. Only nested B1 is accepted; top-level79/177 and B2/C/D/E remain open.
No GM capability/contract changed, so no prompt/example/matrix update is needed.

The accepted independently testable prerequisite T081-B2A is specified completely in
`docs/superpowers/plans/2026-09-08-retained-resource-execution-session.md` and its
companion patch. A production-consumed iterator retains the real fixed graph,
identities, scheduler, mutable resource/history work, use arbiter and transcript.
It can pause at closed resource causal frontiers without final sealing; ordinary
Drain creates no checkpoint images and preserves existing results/work counters.
Nine concrete tests plus owning, scale, pending and signed controls are required.
This does not imply a whole spiritual exchange boundary or support replacing a
prepared suffix; zero-mutation lawful sources remain mandatory downstream.

B2A accepted2026-09-08 atf5e4de2f: full recordedfc6887e6..f5e4de2f review is
Spec compliant / Quality Approved,0C/I/M. Parent verified exact production/test
diffs and actual Focused9/9,432/432,6/6,11/11,2/2 plusFast7767/7767,wall5:00.6051031,
default5m withTimedOut=false,build0/0,cleanupcomplete,no skips/duplicate executions
or cross-descriptor IDs. All26FastTRXs and nine new session Facts were inspected.
The bounded plan retains both RED artifacts and source-grounded fixture corrections.
Only B2A is accepted; top-level79/177 and fullB2 remain open.

Accepted T081-B2B is fully specified in
`docs/superpowers/plans/2026-09-08-nonterminal-effect-prefix.md` and its companion.
Its distinct ClosedPrefix view preserves all structural checks and validates the
actual allocated mechanics frontier, including trailing orphan evidence. The
retained session consumes the view only on explicit closed-boundary stepping;
it is neither completed effect state nor spiritual source authority.

B2B accepted2026-09-08 at02fafbdf: full3e4518cb..02fafbdf review is Speccompliant /
QualityApproved,0C/I/M. Parent reconstructed all five companion postimages (only
Validate parameter wrapping differs), read actual RED0/1 and Focused45/45,109/109,
5/5 plusFast7776/7776 in4:59.1412643 underdefault5m; build0/0,cleanupcomplete,
no skips/duplicate executions/cross-descriptor IDs. All26FastTRXs include all8new
builder Facts and10session Facts. The bounded plan records exact artifacts and
resolves non-diff-verifiable execution history; fullB2 and top-level79/177 remain.

The subsequent effect boundary must distinguish live state from final history
assembly. The existing finalizer globally preflights released applications, writes
non-consuming evidence, applies nonterminal reactions, projects consuming uses
(including insertion before a replacement), applies non-bound lifecycles and
folds terminal winners. Repeating it over prefixes would change ordering or apply
work twice. After a nonterminal evidence view, implement an owned effect draft and
operation journal with actual already-allocated materialization results, preserved
ordinary compatibility ordering and exact frozen replacement dependencies. Admit
authorized wounds into that same draft as causal operations and consume their
timing-eligible effects when preparing the next exchange; final canonical history
and common publication consume the retained results exactly once. Observation-only
types cannot close B2. Whole-source authority, both-side/zero-cost exchange mapping,
suffix recomposition and authenticated receipt/cold recovery remain required.

The source-grounded ordering/ownership design is now durable in
`docs/superpowers/plans/2026-09-08-spiritual-effect-draft-journal-design.md`
(T081-B2C). Authorized materializing/worsening insertions create dependency-local
barriers; declines and unrelated captures do not flush ordinary phases. Actual
history edits retain exact consume-before-replacement/earlier-terminal anchors,
point-in-execution replacement agreement and allocated result identities.
Non-bound lifetime and terminal winners remain one global final pass. Physical
reaction materialization does not grant new current-turn routing eligibility;
new wound generations use their actual profile-bound eligibility. Real insertion
and next-exchange consumers plus13 executable scenarios remain required before
acceptance. This architecture record is not evidence that these paths exist.

The first write-owning production unit T081-B2C-J1 is accepted with its code/test
plan, companion and parent verification record in
`docs/superpowers/plans/2026-09-08-effect-identity-history-owner.md`.
Its owner is consumed by shared initial/final effect planning and all14 identity
mutation helpers; actual creates/appends/anchored inserts and allocation order
are retained without per-operation whole-workspace snapshots. Parent source audit
caught and corrected a proposed early uniqueness exception: duplicate child IDs
in the real two-consuming cascade must retain the old second-release replacement
authority failure before consuming allocations, while malformed transition IDs
retain their applicable final-validation path. Old-production characterization
and13Fast+1Integration rows were executed and parent-verified. Commit80b09558
has a complete e96de0d7..80b09558 independent Spec compliant / Quality Approved
review, zero findings. Actual OLD RED0/1, OLD characterizations1/1 and3/3,
GREEN13/13,131/131 and Integration7/7, plus parent Fast7789/7789 in
4:48.8006972/default5m are recorded in the accepted plan. All28FastTRXs were
inspected, with no non-passing or cross-descriptor duplicate executions;
all runs have build0/0 and complete cleanup. Finalizing the user-paused review
required no duplicate C# execution on unchanged source.
J1 remains only identity/history ownership. J2 in the same feature slice must own
carrier/source/target/skill/processed-event/phase execution and consume real wound
insertion/current-generation before authority in the next exchange. No further
observer-only prerequisite or fullB2 acceptance follows from J1 alone.

The next bounded unit T081-B2C-J2-A is tracked for implementation in
`docs/superpowers/plans/2026-09-08-effect-draft-write-owner-unit-a.md`
and its complete six-file companion. Production completion will consume the
actual owned carrier/source/target/skill state, J1 writer and ordinary phase
runner; exact application receipts include non-create stack/refresh/merge
updates and actual carrier/history/allocation ranges. Parent reviewed the
candidate bodies and mechanically confirmed the relocated finalizer preserves
the old algorithms and ordering. Seven OLD payload/allocation goldens, the
real-production ownership-contract RED,23 GREEN rows, existing Integration
consumers, independent review and parent Fast remain required. No C# evidence
or implementation acceptance is claimed by this planning record.
This internal refactor changes no GM-authored contract, so it needs no Mortal
or afterlife prompt/example/matrix/manifest change. Live source acquisition,
current-generation insertion, versioned routing and the next-exchange consumer
remain mandatory J2 work with their tracked GM synchronization.

The accepted bounded T177 fixture correction is specified in
`docs/superpowers/plans/2026-09-07-mortal-effect-rollback-fixtures.md`.
Generic effect publication/rollback scenarios previously seeded old array-form
wounds, so source validation rejected them before their injected write failure.
The core correction uses the existing registered materializable skill for those
ordinary effects, with aligned command/canonical selectors and identity index.
All fourteen original rollback rows, snapshots, probes and byte assertions stay
intact; afterlife spiritual_art source overrides are unchanged. It introduces
no wound gameplay, shared fixture, production or GM-contract change. The adjacent
one-line analyzer correction changes only Assert.Single's predicate overload.
Commits5235d413/14d2dfd6 are independently Spec compliant / Quality Approved,
zero findings. Actual14-row RED/GREEN, Fast7698PASS in4:38.665/5m and final clean
50-row Integration control are parent-verified. The plan corrects earlier warning
provenance and records exact dynamic-case accounting. T177 and the complete
feature remain open until their broader work is actually verified.

The accepted bounded T077/T083/T085 declaration prerequisite is specified in
`docs/superpowers/plans/2026-09-07-spiritual-danger-declaration.md`. It requires
one exact danger token on new starts and canonical active/recent objects,
preserves it through ordinary exchange and terminal publication, and reuses the
already-parsed signed pre-turn root to check every retained same-ID occurrence.
One internal policy supplies the unchanged 0/2/4 mode caps and exact token
reader. Invalid/ambiguous baseline authority fails closed without a migration;
legal history truncation and the separate terminal predicate are unchanged.
The complete-code plan includes 66 deterministic reducer rows, 50 actual
response/file/snapshot rows, one GM guard, explicit current-fixture cutover,
six synchronized GM surfaces and the existing worked start's runtime persisted-
value assertion. Its bounded Spec Kit consistency pass found no new scoped
ambiguity or constitution conflict. Runtime947832f0 plus fixture correction1576aa57
are independently final-state Spec compliant / Quality Approved, with parent
actual source, archive and result-artifact inspection. Fast7698/7698 passes in
4:11.077/5m. The original FullValidation failed1622/1623; corrected archive/command
reruns plus exact missing-descriptor follow-through reconcile1859rows/1075methods
without failed/missing/extra latest results, but do not relabel that official
control as passing. The plan preserves the skipped historical Integration RED50
as a Minor sequencing deviation and four separately traced Mortal fixture failures
under T177. This bounded acceptance does not close any top-level task.
Accepted escalation, full side seals, defeat/dissipation, first-exchange UI,
arts/progression and real wound/healing production remain their open tasks;
that historical prerequisite did not decide the art schema or legacy preparation.
The 2026-09-07 art clarification below resolves the former; legacy preparation
remains separate.

The bounded T085 authority prerequisite
`docs/superpowers/plans/2026-09-07-spiritual-exchange-history-authority.md`
closes marker-alone historical exemption before an exchange can underpin spiritual
wound evidence. It uses the existing signed payload tracker, scopes it to the same
active conflict and consumes each occurrence once. Exact matching remains first;
the existing older-marker/readable-summary audit regression is preserved only for
missing/null/string top-level summary differences with all other members exact.
This is validation compatibility, not permission to mutate a published history:
the resource outcome builder still rejects any pre-turn prefix difference. Sixteen
file-backed cases exercise current dice/matchup/action-cost authority and retained
compatibility. Turn guide/matrix/worked contrast/manifest/source guard change
together, with bounded Focused/oneFast/conditionalFullValidation evidence.
Source of Light's independent marker path, cross-exchange dice consumption, full
strain/actor/resilience proof, danger/wound seals, actual wound opportunity
publication and healing remain separate unfinished T084/T085 contours. This
prerequisite neither decides the art schema nor closes the full spiritual story.

The classifier prerequisite is accepted through `27f6804c` (original BASE
`a8e551c7`, runtime `921feff0`), after parent source/raw-artifact inspection and
independent Spec Compliant / Quality Approved re-review with zero open findings.
Actual new behavior16/16, neighboring37/37 and final documentation122/122 PASS;
one Fast7,630/7,630 in4:35.585 within five minutes and one conditional
FullValidation1,857/1,857 in10:09.537 within fifteen minutes. Discovery/runtime
theory expansions reconcile exactly; builds and cleanup are clean. Targeted
semantic RED/GREEN also proves preservation of surrounding example guidance and
removal of the obsolete marker-only prose. The initial pre-edit corrective
1/1 GREEN is explicitly not regression proof. Detailed chronology and artifacts
are in the linked bounded plan. This does not close any full spiritual task or
change the top-level77/177 count; Source of Light's independent turn authority
and actual wound/healing producers remain unfinished.

The adjacent T085 prerequisite is specified in
`docs/superpowers/plans/2026-09-07-light-incarnate-history-authority.md`.
Light Incarnate validation receives the existing active exchange membership or
an exact one-use recent-resolution membership from a separate pre-turn list.
It rejects unmatched pre-grant markers and no-marker current payloads even when
a validated baseline has no dice. Existing offline compatibility, grant closure,
bonus arithmetic, marker precedence and resource-prefix enforcement are unchanged.
Twenty-three real file-backed cases and synchronized GM API/daemon/turn/matrix/
example/manifest guidance cover only this admission boundary; full grant authority,
spiritual wound production, arts and healing remain open. Parent has checked the
complete-code plan against the current private interfaces before implementation.
Accepted on 2026-09-07: `cbbfa98f` plus `d9547c52`, independently reviewed
Spec compliant / Task quality Approved, with no Critical/Important findings.
The correction restores the exact Integration source and adds a mutation-proved
recent-summary-only rejection. Final owner54/54 and docs123/123 are GREEN;
one Fast7631/7631 in3:50.280/5m and one Full1857/1857 in10:50.932/15m are verified
against all raw artifacts and discovery. The bounded plan preserves the temporary
CS1026 build failure, command-form deviation and two non-blocking Minor items.
No full feature task is closed; top-level77/177 and the final T177 gate remain open.

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

First bounded implementation plan (2026-09-07):
`docs/superpowers/plans/2026-09-07-spiritual-wound-opportunity-math.md` isolates
the approved pressure/threshold/destination/mode/source-cap arithmetic from the
still-unimplemented accepted-conflict adapter. Its immutable result is explicitly
not authority; it never creates a wound or decides for the GM. Long arithmetic
preserves negative margins and fails closed on invalid domains or overflow.
The pure 66-row test inventory covers arithmetic only, not accepted harmful-margin
provenance, art registration, mode/escalation seals, per-side/re-trauma decisions
or opportunity publication. T076/T084 remain open after this prerequisite.
Source preflight found that the former new-art object example differed from current
scalar-tier progression. The user's explicit 2026-09-07 answer resolves this for
T082/T093/T100: both new arts use the existing integer 0..5 tiers, player
training/direct upgrades and persistent-entity automatic progression. The player
`afterlifeCombatProfile.artTiers` remains authoritative and the persistent
`player_soul.standardArts` remains a mirror excluded from automatic upgrades.
Bootstrap/current profile fixtures receive explicit zero entries; validators reject
missing required new-art tiers and malformed/object tiers after cutover. No per-art
experience, mixed schema, new progression pipeline, or compatibility reader is added.
Registration does not make passive resilience an operation, nor does it implement
the later healing operation/resolver. The data model now matches this decision.
The concrete current-profile/training/presentation cutover plan is
`docs/superpowers/plans/2026-09-07-standard-wound-arts.md`.
That cutover is accepted through `c5a8349d` (whole scope `072d0cd7..c5a8349d`):
independent whole-task review plus correction re-review is Spec Compliant /
Quality Approved with zero remaining findings, and parent source/archive/raw
artifact audits are complete. T082/T100 are accepted, but T093's actual diagnosis
and insufficient-tier workflow and all live wound/healing producers remain open.
Final integrated Fast7753/7753, FullValidation1909/1909, documentation125/125,
Training follow-through80/80 and post-copy3/3 are recorded in the linked plan,
separately from the historical failed controls and setup/direct-build deviations.
Bounded arithmetic is accepted at `1969a695` after
parent source/artifact inspection and independent Spec Compliant / Quality
Approved with zero open findings. Actual staged 42 RED -> 42 GREEN -> 24 RED ->
66 GREEN, all at five minutes with clean builds/cleanup, proves only this math.
One Fast completed 6,208 of 7,380 discovery rows: 6,207 PASS and the required
unchanged legacy-source failure; arithmetic 1,172 uncompleted, not full GREEN.
No runtime caller or GM contract was added, so no GM prompt/example or conditional
FullValidation update was needed. Full T076/T084 remain open; the linked sub-plan
contains the exact evidence and boundaries. The art-schema decision is resolved
above and must not be asked again.

Next numerical prerequisite (2026-09-07):
`docs/superpowers/plans/2026-09-07-spiritual-healing-outcome-math.md` defines the
unused internal spiritual-healing tier/formula/band/terminal-bound calculator.
Its 44 valid-value and 14 invalid-domain/overflow rows cover arithmetic only;
the return value supplies no accepted healer/target/modifier/die authority,
resource/time spend, art progression or mutation. Accepted at `4711d6a8` after
parent source/artifact inspection and independent Spec Compliant / Quality
Approved, zero open findings. Actual 44 RED -> 44 GREEN -> 14 RED -> 58 GREEN,
all clean five-minute runs; one Fast completed 6,266 of 7,446 discovery rows with
6,265 PASS / one required unchanged legacy failure, arithmetic 1,180 uncompleted,
not full GREEN. Full T094/T101 stay open for the actual shared resolver
and accepted publication, while the existing GM synchronization tasks cover the
future exposed workflow. This arithmetic task did not change the art schema;
the subsequent 2026-09-07 clarification above selects ordinary scalar progression.

Accepted recovery arithmetic prerequisite (2026-09-07):
`docs/superpowers/plans/2026-09-07-spiritual-natural-recovery-math.md` supplies an
unused numeric step with 43 valid-value and nine invalid/overflow cases. Commits
`45beccd1` plus comment-only review fix `8aff3325` are independently Spec Compliant /
Quality Approved and parent source/artifact-audited. Actual 43 semantic RED ->
43 GREEN -> nine invalid RED -> 52 GREEN; one five-minute Fast completed 6203 of
7498 discovery rows, 6202 PASS / one mandatory legacy-source FAIL, arithmetic 1295
uncompleted. All new rows pass; clean build/cleanup, no timeout/duplicates. Exact
artifacts are in the linked plan, with no full Fast GREEN claim. It preserves nonnegative-long carried
progress and the approved thresholds, not accepted safe-cycle or actor authority.
Full T097/T105 remain open, including worsening, safe/unsafe gating and all entity
paths. Actual T105/T107 must handle the generic recovery follow-up heal bound and
active partial-point then natural-recovery ordering without losing progress or
reusing the Mortal-only exception. Scheduler/profile/wound publication still needs
one coordinated accepted transaction; no extra authority is inferred from the math.

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

## 2026-09-05 — exact skill scope extension from #1536

The approved design at
`docs/superpowers/specs/2026-09-05-roll-modifier-skill-scope-design.md` extends the
completed #1535 common profile. Every accepted `roll_modifier` is cut directly to the
closed three-field payload; no migration or compatibility reader is added:

```json
{
  "operations": ["skill_check"],
  "contribution": "disadvantage",
  "scope": { "kind": "skill", "skillId": "skill_lockpicking" }
}
```

### Architecture and data flow

1. `EffectComponentProfiles` validates the closed `all|skill` discriminator and the
   exact `skill_check` cross-field rule without reading target state.
2. One detached `EffectRollSkillScopeAuthority` builds bounded offered and composed-final
   player/NPC active/passive catalogs. It binds new focused components by exact ordinal
   permanent identity, rejects duplicate/confusable/wrong-owner/stale authority, creates
   the advisory `effectSkillScopeCatalog`, and fingerprints both catalog halves.
3. Ordinary, reaction, and wound applications structurally revalidate bound components
   and seal that authority after parameter binding and before identity allocation or
   mutation; the closed `scope` object is never a scalar `parameterBounds` target. The
   retained authority fingerprint participates in wound-candidate, accepted-boundary
   transcript, and staged/final seals. Final revalidation rejects a same-response
   removal/disable; a same-response new skill was never offered. Only an authenticated
   treatment rematerialization continues the unchanged accepted selector without
   rebinding it after later skill loss.
4. `EffectMechanicsSnapshot` carries current scope authority. The sole
   `EffectRollContributionResolver` captures every active common `roll_modifier` into a
   bounded, versioned `EffectDetachedRollSourceAuthority` before filtering, then validates,
   filters exact actor, realm, operation, and scope, and performs the unchanged reducer in
   one shared core. The detached authority contains only ordered mechanical fields and no
   display, owner, carrier, arbitrary payload, or full-snapshot data. Legitimate later
   absence or unavailability derives dormancy; corrupt or ambiguous authority fails closed.
5. Mortal procedure authority seals both nullable `RollSkillId` and the normalized source.
   `resolved_skill_tier` derives an exact usability proof from its recursively validated
   requirement row/witness; `fixed_zero` supplies null and no proof. Detached validation
   recomputes the compact rows, roll mode, and dice shape with the common core. Fresh
   validation recaptures the normalized source from current accepted mechanics and requires
   exact source/result agreement; a fresh mismatch after cold recovery restores the prior
   die, Fate, and treatment-resource registries.
6. The procedure fingerprint domain moves directly to its new source-bearing version;
   missing source authority and the compatibility constructor that inferred `SkillId` from
   `CapabilityRef` are unsupported. Typed serialization, restore clones, and both live and
   detached fingerprints preserve every normalized row and explicit null position.
7. Player projection resolves readable current names without exposing IDs or changing
   hidden-effect visibility. One scoped component remains one wound slot regardless of
   scope, and every serialization/cache/snapshot/rematerialization/rollback path preserves
   the selector exactly.

### Delivery and lane placement

Tasks T167–T177 own the scope sub-slice and execute before the still-pending Phase 10
documentation/control tasks T148–T166 resume. T167–T175 follow RED/GREEN ownership from
pure structural tests through accepted planning, the shared resolver and its detached
roll-source authority, treatment replay/fresh-recovery trust boundary, projection, GM
catalog, direct-cutover fixtures, and file-backed lifecycle evidence.
T176 synchronizes the effect/wound Mortal and afterlife GM contracts, examples,
manifests, projections, and source guards, preserving the existing Task 7 documentation
work. T177 runs semantic scans, one meaningful Fast checkpoint, required documentation
and FullValidation controls, conditional regression integration, independent review,
and diff/status safety checks; PreMerge remains reserved for an actual merge request.

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
