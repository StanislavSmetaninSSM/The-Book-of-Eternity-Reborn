# Implementation Plan: Complete Faction Materialization

**Branch**: `1510-faction-materialization-design` | **Date**: 2026-08-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from
`/specs/1510-complete-faction-materialization/spec.md`

## Summary

Implement one immutable, embedded Faction Materialization receipt with separate
Mortal and Shining evidence profiles. The accepted-turn pipeline will compare
duplicate-sensitive current output with the validated pre-turn snapshot,
classify each faction as new, legacy promotion, already materialized,
client-derived-only, or untouched legacy, and reject incomplete GM-authored
state before semantic normalization can fill defaults.

Mortal first creation and promotion continue through the full
`factionDataChanges` carrier, but the resulting canonical bundle must contain
the complete semantic core and exact faction-bound sidecar surfaces. Ordinary
updates move to closed `FactionCoreChanges` groups or the existing dedicated
commands. The existing `scribeChronicle` creation field becomes an actual input
to `faction_chronicles.json` instead of validated-but-discarded data.

Shining creation retains native-discovery, player-founding, and
story/hidden route authority. Each route must create a complete hall-bound
faction, exact route provenance, strategic memory, chronicle, visibility, and
all required actor/resident links. Raw checks run before
`ShiningAbodeState.NormalizeStateRoot`, which remains responsible only for
mechanical shape and client-owned projections.

## Technical Context

**Language/Version**: C# / .NET 8; PowerShell 7 for bounded test orchestration;
JSON-backed canonical state and GM response contracts

**Primary Dependencies**: `System.Text.Json`, existing
`FileSystemManager`, `ValidationService`, `CanonicalStateNormalizer`,
`CriticalStateHealthService`, `GameEngine` repair loop, xUnit

**Storage**: Local JSON files under `game_state/factions/`,
`game_state/meta/shining_abode_state.json`,
`game_state/meta/afterlife_entity_profiles.json`,
`game_state/meta/guardian_abode_residents.json`, Mortal location/NPC state, and
validated pending-turn snapshots

**Testing**: xUnit in `BookOfEternityClient.Tests` and
`BookOfEternityClient.IntegrationTests`, invoked only through
`pwsh .\scripts\test-csharp.ps1`

**Target Platform**: Local Windows game client and loopback browser client;
offline-capable and no new remote services

**Project Type**: Desktop/console game client with local browser frontend and
file-backed GM harness

**Performance Goals**: Raw and canonical faction validation remain linear in
the number of factions and related records; no new unbounded scan or extra test
lane; Fast remains under 5 minutes and PreMerge under 15 minutes, preferably
under 10

**Constraints**: Preserve untouched legacy saves; reject duplicate JSON members;
no semantic default laundering; no private materialization metadata in ordinary
player UI; no timeout or concurrency widening; no #1222 worker,
#1462 living-world scheduler, or #1368 Guardian-politics implementation

**Scale/Scope**: Two faction domains, six Mortal faction files plus exact
location/NPC references, one composite Shining state plus resident/actor/story
authority, accepted-turn classification, repair routing, docs/examples, and
bounded verification

**Source Issue(s)**:
[#1510](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1510);
related boundaries
[#1500](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500),
[#1222](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1222),
[#1368](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1368),
and
[#1462](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1462)

**Contract Scope**: GM-facing prompts; Mortal and Shining runtime state;
accepted-turn validation, normalization, rollback, and repair; worked examples
and manifests; console/browser metadata privacy; no player-facing redesign

**Verification Commands**:

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FactionMaterializationContractTests|FullyQualifiedName~FactionMaterializationValidationTests|FullyQualifiedName~FactionCoreChangesContractTests|FullyQualifiedName~FactionCoreChangesTests|FullyQualifiedName~CanonicalStateNormalizerTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests|FullyQualifiedName~FullValidationEquivalenceTests|FullyQualifiedName~IntegrationTestBoundaryTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~ValidationSourceGuardTests|FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~AfterlifeShiningPlayerFacingSourceGuardTests|FullyQualifiedName~ShiningAbodeStateTests|FullyQualifiedName~ExampleDocumentationValidationTests"
pwsh .\scripts\test-csharp.ps1 -Lane Fast
pwsh .\scripts\test-csharp.ps1 -Lane FullValidation
pwsh .\scripts\test-csharp.ps1 -Lane PreMerge
```

`FullValidation` is required once for this issue because the Shining afterlife
contract documentation and examples change. Fast runs once at the stable
cross-domain checkpoint. PreMerge runs once from a clean checkout immediately
before integration and replaces a duplicate final Fast run.

## Constitution Check

*GATE: Passed before Phase 0 research and re-checked after Phase 1 design.*

- **GitHub traceability — PASS**: #1510 owns the work. Related issues are
  boundaries, not substitute implementation trackers.
- **Spec Kit fit — PASS**: This is a multi-session validation, canonical-state,
  normalizer, GM-contract, Mortal World, and Shining Abode change.
- **Player-facing integrity — PASS**: No UI redesign is planned. Existing
  console/browser readers remain compatible and receive source guards proving
  that private envelope fields and reasons are not rendered.
- **Contract/state authority — PASS**: Runtime, prompts, rules, examples,
  manifest, Afterlife Contract Matrix, and documentation/source guards change
  together. The canonical authority for every new field is defined in
  [data-model.md](./data-model.md).
- **Test-first path — PASS**: Each contract and route starts with a focused
  failing test. Production edits follow the RED/GREEN order in `tasks.md`.
- **Verification evidence — PASS**: Focused selections, one Fast checkpoint,
  one required FullValidation selection, and one clean PreMerge are bounded by
  the repository lane policy.
- **Agent orchestration — PASS**: Any delegated implementation packet must
  include #1510, this feature directory, the Superpowers TDD/debug/review
  method, and the commands above. Agent reports are never accepted as proof
  without diff inspection and fresh verification.

No constitution violation requires a complexity exception.

## Project Structure

### Documentation (this feature)

```text
specs/1510-complete-faction-materialization/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── faction-core-changes.md
│   ├── faction-materialization-envelope.md
│   ├── faction-repair-packet.md
│   ├── mortal-faction-authority.md
│   └── shining-faction-authority.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/
├── Configuration/FileMapping.cs
├── Core/GameEngine/GameEngine.ValidationAndRepair.cs
├── Models/GameResponse.cs
└── Services/
    ├── FactionMaterializationContract.cs
    ├── FactionCoreChangesContract.cs
    ├── CanonicalStateNormalizer.cs
    ├── CanonicalStateNormalizer/
    │   ├── CanonicalStateNormalizer.Factions.cs
    │   ├── CanonicalStateNormalizer.FactionAndInventoryHelpers.cs
    │   └── CanonicalStateNormalizer.ShiningAbode.cs
    ├── ShiningAbodeState.cs
    ├── ShiningAbodeState.Gameplay.cs
    └── Validation/
        ├── GameStateValidationPhase.cs
        ├── ValidationService.FactionMaterializationContinuity.cs
        ├── ValidationService.MortalFactionMaterialization.cs
        ├── ValidationService.ShiningFactionMaterialization.cs
        ├── ValidationService.FactionCoreChanges.cs
        ├── ValidationService.FactionsAndProjects.cs
        ├── ValidationService.ShiningAbode.cs
        ├── ValidationService.ValidationPhases.cs
        └── ValidationService.LifecycleControlAndStateFiles.cs

BookOfEternityClient.Tests/
├── FactionMaterializationContractTests.cs
├── FactionCoreChangesContractTests.cs
├── ValidationPhaseSelectionTests.cs
├── AfterlifeDocumentationCoverageTests.cs
├── PromptDocumentationCoverageTests.cs
└── ValidationSourceGuardTests.cs

BookOfEternityClient.IntegrationTests/
├── FactionMaterializationValidationTests.cs
├── FactionCoreChangesTests.cs
├── IntegrationValidationProfiles.cs
├── CanonicalStateNormalizerTests.Factions.cs
├── CanonicalStateNormalizerTests.ShiningAbode.cs
├── FullValidationEquivalenceTests.cs
├── IntegrationTestBoundaryTests.cs
└── ExampleDocumentationValidationTests.cs

Rules/Block_21.txt
Rules/Block_CLI_Operations.txt
TaskGuides/CLI_Step_Main.txt
Examples/E_Block_21.txt
Examples/E_CLI_Step_Main.txt
Examples/E_CLI_Afterlife_Turns.txt
Examples/example_validation_manifest.json
OtherGuides/Afterlife_Contract_Matrix.md
CLI_API_Specification.md
CLI_Agent_Daemon_Specification.md
BookOfEternityClient/game_master_daemon.ps1
docs/audits/afterlife/shining-abode/Shining_Abode_Faction_Politics_Addendum.md
```

**Structure Decision**: Follow the existing Actor Materialization and
`NPCCoreChanges` split: one pure closed contract, separate accepted-turn
continuity/domain validators, one narrow mutation contract, and integration at
the existing validation/normalization/repair seams. Mortal and Shining evidence
builders remain separate so the common kernel does not absorb setting-specific
logic. Documentation stays in its established canonical files; there is no new
parallel guide.

## Phase 0: Research Outcome

Research decisions and rejected alternatives are recorded in
[research.md](./research.md). The key source findings are:

1. Actor Materialization already provides the appropriate closed-envelope,
   disposition, capability, uniqueness, continuity, and repair-code pattern.
2. `factionDataChanges` is already the raw full Mortal carrier and its
   normalizer distributes ranks, resources, projects, and custom state.
3. `scribeChronicle` is validated on creation but is currently discarded; the
   implementation must bind it to canonical history.
4. No standalone Mortal relation-change command exists. To avoid inventing a
   second top-level command, the closed `FactionCoreChanges.relations` group
   owns absolute relation snapshots; all actually existing dedicated commands
   keep their current authority.
5. Shining normalization currently supplies semantic origin, charter,
   leadership, lifecycle, visibility-like chronicle defaults, and strategic
   memory containers. Raw validation must run first, and post-feature
   normalization may only canonicalize already-authored values and compute
   documented projections.
6. Native discovery and player founding already have strict request/receipt
   diff checks. Faction Materialization composes with those checks and does not
   replace them.

## Phase 1: Design Outcome

- [data-model.md](./data-model.md) defines the exact envelope, semantic cores,
  canonical surfaces, touch classification, and state transitions.
- [contracts/faction-materialization-envelope.md](./contracts/faction-materialization-envelope.md)
  defines the shared immutable receipt.
- [contracts/mortal-faction-authority.md](./contracts/mortal-faction-authority.md)
  defines raw/full/canonical Mortal authority and exact empty surfaces.
- [contracts/shining-faction-authority.md](./contracts/shining-faction-authority.md)
  defines route authority, visibility/story evidence, and Actor Materialization
  cross-links.
- [contracts/faction-core-changes.md](./contracts/faction-core-changes.md)
  defines the closed ordinary Mortal mutation command.
- [contracts/faction-repair-packet.md](./contracts/faction-repair-packet.md)
  defines stable coordinates and bounded repair scope.
- [quickstart.md](./quickstart.md) defines the implementation and verification
  loop without an unbounded suite.

## Implementation Sequence

1. Add the common envelope tests and pure contract.
2. Add duplicate-sensitive pre-turn classification and the raw validation
   phase before normalization.
3. Add Mortal evidence, atomic bundle validation, real `scribeChronicle`
   extraction, and exact empty-sidecar behavior.
4. Add `FactionCoreChanges`, including the closed relations group, and reject
   already-materialized full resends.
5. Add Shining semantic-core/provenance validation, then cover native,
   founding, and story/hidden routes plus actor/resident/hall links.
6. Restrict Shining normalization to mechanical work for new/promoted
   candidates while preserving legacy load compatibility.
7. Add immutable-continuity checks, repair packet routing, metadata privacy,
   docs/examples/manifests, and source guards.
8. Run the bounded verification ladder and reconcile every Spec Kit task before
   merge.

## Complexity Tracking

No constitution violations. The cross-file accepted-turn validator is necessary
because completeness cannot be established from any single Mortal sidecar or
from normalized Shining state, and a global registry was explicitly rejected in
favor of embedded faction authority.
