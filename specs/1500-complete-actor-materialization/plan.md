# Implementation Plan: Complete Actor Materialization

**Branch**: `1500-complete-actor-materialization` | **Date**: 2026-07-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/1500-complete-actor-materialization/spec.md`

## Summary

Add a setting-agnostic, versioned first-materialization contract for persistent Mortal NPCs and significant afterlife actors. The validator will require an actor-bound private `materialization` envelope for current-turn creations and promotions, compare declared section dispositions and capabilities with canonical actor data, bind afterlife type-specific records to common profiles, preserve untouched legacy saves, and emit bounded repair packets. Positive afterlife trade authority is derived only from exact current Guardian/Shining state, while actor-owned memory and real goals/quests/activity prove agency. The worker apply gate mechanically preserves protected actor data outside the named repair scope, pins exact proposal bytes, and applies or rolls back through cross-process compare/exchange. Only completed proposals may enter apply; terminal failure proposals remain mutation-free diagnostics. Worker audit append is concurrency-safe and cannot revoke accepted canonical bytes on telemetry failure. Worker dispatch owns a repair before legacy fallback is exposed, including direct revalidation after an accepted apply whose ready publication fails. The client will never infer skills, possessions, roles, or capabilities from prose. System Guardian seeds remain client-owned and receive deterministic envelopes that match their exact seeded capabilities. Mortal and afterlife prompts, examples, manifests, contract documentation, and source guards are updated with the runtime change.

## Technical Context

**Language/Version**: C# 12 / .NET 8; PowerShell GM launcher and documentation entrypoints; JSON state contracts

**Primary Dependencies**: `System.Text.Json`, existing file-system abstraction, validation/normalization services, Spectre.Console client stack; no new package

**Storage**: Local file-backed JSON (`game_state/npcs/npc_core.json`, `game_state/meta/afterlife_entity_profiles.json`, Guardian/resident/Shining files, validated pre-turn snapshots)

**Testing**: xUnit through `dotnet test`; documentation/source-guard tests; fixture-driven canonical-state validation

**Target Platform**: Local Windows game client on .NET 8; contracts remain platform-neutral JSON

**Project Type**: Console/browser RPG client with local daemon/bridge and file-backed canonical state

**Performance Goals**: Linear validation in actor and section counts; no network or model calls; negligible additional latency for ordinary save sizes

**Constraints**: Preserve legacy saves; no client-authored narrative data; no genre keyword inference; no new cloud dependency; dedicated delta commands remain authoritative; metadata stays out of player projections

**Scale/Scope**: Mortal NPC core validation, afterlife common profiles and cross-file actors, repair harness, System Guardian seed generation, prompts/docs/examples/source guards, focused tests

**Source Issue(s)**: [#1500](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500), related [#1446](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1446) and [#1461](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1461)

**Contract Scope**: GM-facing prompts, runtime state, validation, normalization preservation, repair packets, Mortal World, Chaos Sea, Shining Abode, docs, examples, manifests, console/browser metadata non-leakage

**Verification Commands**:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ActorMaterialization|NpcFullObject|AfterlifeEntityProfile|GuardianAbodeResident|ShiningLeadership|SystemGuardianLibrary|GmWorker|ValidationRepair"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|PromptDocumentationCoverageTests|ValidationSourceGuardTests|GameEngineSourceGuardTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **GitHub traceability**: Issue #1500 and related startup findings are linked in spec, plan, and tasks.
- **Spec Kit fit**: This is a multi-file canonical-state, validation, afterlife, repair, prompt, documentation, and example contract.
- **Player-facing integrity**: The envelope is private harness metadata; tests prevent console/browser player projections from exposing it.
- **Contract/state authority**: Canonical JSON and existing dedicated delta commands remain authoritative; prompts/docs/examples are synchronized for Mortal World and afterlife.
- **Test-first path**: Each actor family, cross-file binding, legacy path, repair packet, and deterministic seed starts with focused failing tests.
- **Verification evidence**: Focused, documentation-sensitive, and full test commands are listed above.
- **Agent orchestration**: Any delegated implementation packet must include #1500, this spec/plan/tasks set, Superpowers TDD/review requirements, and verification commands.

**Gate result before research**: PASS. Issue #1500 is tracked; harness-first validation, legacy safety, documentation synchronization, and verification evidence are explicit.

**Gate result after design**: PASS. The data model keeps narrative authority with the GM, uses validated pre-turn state for legacy/new classification, preserves dedicated delta contracts, and documents both Mortal and afterlife authoring paths.

## Project Structure

### Documentation (this feature)

```text
specs/1500-complete-actor-materialization/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/actor-materialization.schema.json
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/
├── Services/
│   ├── ActorMaterializationContract.cs
│   ├── SystemGuardianLibraryService.cs
│   ├── Validation/ValidationService.ActorMaterializationContinuity.cs
│   ├── Validation/ValidationService.ActorMaterializationBinding.cs
│   ├── Validation/ValidationService.ActorMaterializationTradeAuthority.cs
│   ├── GmWorkers/ActorMaterializationRepairPreservationGuard.cs
│   ├── GmWorkers/GmWorkerApplyGate.cs
│   ├── GmWorkers/GmWorkerTaskPacketBuilder.cs
│   └── CanonicalStateNormalizer/
├── Core/GameEngine/GameEngine.ValidationAndRepair.cs
└── game_master_daemon.ps1

BookOfEternityClient.Tests/
├── ActorMaterializationContractTests.cs
├── ActorMaterializationValidationTests.cs
├── SystemGuardianLibraryServiceTests.cs
├── AfterlifeDocumentationCoverageTests.cs
├── ExampleDocumentationValidationTests.cs
├── PromptDocumentationCoverageTests.cs
└── ValidationSourceGuardTests.cs

OtherGuides/
├── Actor_Brain_2_0.md
└── Afterlife_Contract_Matrix.md

Examples/
├── E_CLI_Step_Main.txt
├── E_CLI_Afterlife_Turns.txt
└── example_validation_manifest.json
```

**Structure Decision**: Keep the contract parser and semantic rules in one reusable service, while thin partial `ValidationService` files integrate it with current-turn/pre-turn authority and cross-file actor types. Existing canonical files and normalizers remain storage authority. No frontend source change is planned because the envelope is private metadata and existing object projections ignore unknown fields.

## Complexity Tracking

No constitution violations require an exception.
