# Implementation Plan: Complete Actor Materialization

**Branch**: `1500-complete-actor-materialization` | **Date**: 2026-07-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/1500-complete-actor-materialization/spec.md`

## Summary

Add a setting-agnostic, versioned first-materialization contract for persistent Mortal NPCs and significant afterlife actors. The validator requires an actor-bound private `materialization` envelope for current-turn creations and promotions, compares declared section dispositions and capabilities with canonical actor data, requires 3-5 integer-valued Mortal personality traits and at least one setting-defined numeric Mortal characteristic, derives one effective Mortal identity so null permanent-ID plus colliding `initialId` cannot evade continuity, and retains validated pre-turn inventory so true legacy promotion can resend only a semantically unchanged snapshot through either full carrier. Ordinary existing core changes use the Mortal-only closed `NPCCoreChanges` command: exact permanent identity, reason, absolute resulting values, bounded profile/location/progression/setting-owned characteristic/faction/Fate Card groups, deterministic mirror reduction, and command consumption. Malformed, non-object, and duplicate-member current authority fails closed; historical full carriers preserve every actor-owned domain; Fate Card additions pass the complete production nested validator before reduction; and canonical ID-less Block 7 skills remain valid combat evidence. Duplicate-key current and validated pre-turn authority fails closed through structured issues while semantic equality stays property-order-insensitive. Afterlife type-specific records bind to common profiles; the first envelope on an existing bound profile revalidates actual Guardian/resident journal or exact common-profile memory while untouched legacy profiles remain readable. Worker subprocesses run in ephemeral detached `.worker_runtime` snapshots exposing only exact pinned task context; only a valid proposal and its declared `contentRef` bytes may be imported. The worker apply gate routes memory repairs by actor type, mechanically preserves every field except the exact actor-owned target, classifies every `game_state/meta/` repair as afterlife-scoped, and holds one canonical write lease from final authority verification through all writes, validation, read-only revalidation, rollback, and decision. Mortal collision repair remains main-GM-only; exact-snapshot inventory and empty-characteristics repairs are constrained to one issue-bound actor, carrier, and field, and characteristic values must parse as finite numbers rather than merely use JSON number syntax. Proposal `status` and changed-file operation are mandatory: omission/unspecified values are invalid, only explicit completed proposals with exact Add/Replace/Delete operations may enter apply, and terminal failure proposals remain mutation-free diagnostics. Worker audit append is concurrency-safe, all producers use one source-guarded UTC-millisecond plus GUID generator, and telemetry failure cannot revoke accepted canonical bytes. Worker dispatch owns a repair before legacy fallback is exposed, including direct revalidation after an accepted apply whose ready publication fails. Derived output-only retries retain the original canonical target set and accept only output strictly newer than every target's latest actual write. The client never infers skills, possessions, roles, capabilities, characteristic keys, carrying formulas, or class-stat allocations from prose, and generated templates use current-world authority with nullable setting-owned carrying results. System Guardian seeds remain client-owned and receive deterministic envelopes that match their exact seeded capabilities. Prompts, examples, manifests, contract documentation, and source guards are synchronized; Phases 19-21 harden shared worker isolation, import publication, dispatch uniqueness, atomic identity reservation, runtime concurrency, explicit Mortal/afterlife path ownership, save/load linearization, cleanup, and afterlife repair authority but add no new afterlife pending/control gameplay surface.

## Technical Context

**Language/Version**: C# 12 / .NET 8; PowerShell GM launcher and documentation entrypoints; JSON state contracts

**Primary Dependencies**: `System.Text.Json`, existing file-system abstraction, validation/normalization services, Spectre.Console client stack; no new package

**Storage**: Local file-backed JSON (`game_state/npcs/npc_core.json`, `game_state/meta/afterlife_entity_profiles.json`, Guardian/resident/Shining files, validated pre-turn snapshots)

**Testing**: xUnit through `dotnet test`; documentation/source-guard tests; fixture-driven canonical-state validation

**Target Platform**: Local Windows game client on .NET 8; contracts remain platform-neutral JSON

**Project Type**: Console/browser RPG client with local daemon/bridge and file-backed canonical state

**Performance Goals**: Linear validation in actor and section counts; no network or model calls; negligible additional latency for ordinary save sizes

**Constraints**: Preserve legacy saves; duplicate/malformed current and continuity authority fails closed without escaping validation; all historical actor-owned fields remain command-owned; Fate Card reduction reuses full production validation; canonical skills do not require synthetic IDs; no client-authored narrative data; no genre keyword inference or universal characteristic/carrying/class-stat formula; no new cloud dependency; dedicated delta commands and bounded `NPCCoreChanges` retain separate authority; worker repair remains exact-actor/exact-field bounded with explicit operations, detached pinned-context execution, pre-publication content digest verification, globally unique repair dispatch IDs, atomically reserved immutable task/proposal IDs, runtime `MaxConcurrentTasks`, case-insensitive canonical path identity, explicit Mortal ownership for current-world lore/core player status, afterlife validation-repair surfaces confined to exact paths under `game_state/meta/` while typed content tasks retain exact task-provided control/report surfaces, no-follow cleanup, exact-byte realm/setting authority, and one external canonical write lease shared by apply plus built-in backup/restore/clear/save/load operations; output freshness retains its original target chain and uses strict ordering; metadata stays out of player projections

**Scale/Scope**: Mortal NPC core validation and bounded reduction, afterlife common profiles and cross-file actors, repair harness, System Guardian seed generation, prompts/docs/examples/source guards, focused tests. R3-I-2/R3-I-3/R3-M-1 are Mortal-only and do not alter afterlife pending/control, response, receipt, scheduler, lifecycle, or authority contracts.

**Source Issue(s)**: [#1500](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500), related [#1446](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1446) and [#1461](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1461)

**Contract Scope**: GM-facing prompts, runtime state, validation, normalization preservation, repair packets, Mortal World, Chaos Sea, Shining Abode, docs, examples, manifests, console/browser metadata non-leakage. Phases 19-21 move every worker to a detached pinned-context runtime, verify complete handoff imports before publication, uniquely identify every dispatch, atomically preserve task/proposal evidence, enforce profile concurrency, preserve timeout truth, treat every `game_state/meta/` repair as exact-path afterlife scope, classify current-world lore and core player status as Mortal, unify Windows path identity, make apply/save/load and built-in canonical writers one external lease protocol, and clean detached workspaces without following reparse points. They add no new afterlife pending/control action, response field, receipt, report, scheduler contour, or normalizer side effect.

**Verification Commands**:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ActorMaterialization|NpcCoreChanges|NpcFullObject|AfterlifeEntityProfile|GuardianAbodeResident|ShiningLeadership|SystemGuardianLibrary|GmWorker|ValidationRepair"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|PromptDocumentationCoverageTests|ValidationSourceGuardTests|GameEngineSourceGuardTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **GitHub traceability**: Issue #1500 and related startup findings are linked in spec, plan, and tasks.
- **Spec Kit fit**: This is a multi-file canonical-state, validation, afterlife, repair, prompt, documentation, and example contract.
- **Player-facing integrity**: The envelope is private harness metadata; tests prevent console/browser player projections from exposing it.
- **Contract/state authority**: Canonical JSON, existing dedicated delta commands, and the closed Mortal-only `NPCCoreChanges` reducer remain authoritative; workers receive only detached pinned context, complete imports are digest-verified before publication, IDs and timeout outcomes are immutable, path identity/scope is exact, operations are explicit, setting/Soul authorities remain read-only and exact-byte pinned under one canonical lease shared by built-in writers, output freshness retains its target chain, and prompts/docs/examples are synchronized for both realms.
- **Test-first path**: Each actor family, cross-file binding, legacy path, repair packet, and deterministic seed starts with focused failing tests.
- **Verification evidence**: Focused, documentation-sensitive, and full test commands are listed above.
- **Agent orchestration**: Any delegated implementation packet must include #1500, this spec/plan/tasks set, Superpowers TDD/review requirements, and verification commands.

**Gate result before research**: PASS. Issue #1500 is tracked; harness-first validation, legacy safety, documentation synchronization, and verification evidence are explicit.

**Gate result after design**: PASS. The data model keeps narrative authority with the GM, uses validated pre-turn state for legacy/new classification, preserves dedicated delta contracts, bounds `NPCCoreChanges` to named existing-core fields, gives workers only a detached pinned-context snapshot, hash-pins setting authority for finite worker-authored characteristics, binds every meta repair to exact read-only Soul authority, holds one canonical apply lease through validation and decision, rejects unspecified changed-file operations, retains the full canonical target chain for strict output freshness, and documents both Mortal and afterlife authoring paths.

## Project Structure

### Documentation (this feature)

```text
specs/1500-complete-actor-materialization/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/actor-materialization.schema.json
├── contracts/npc-core-changes.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/
├── Services/
│   ├── ActorMaterializationContract.cs
│   ├── NpcCoreChangesContract.cs
│   ├── SystemGuardianLibraryService.cs
│   ├── Validation/ValidationService.ActorMaterializationContinuity.cs
│   ├── Validation/ValidationService.ActorMaterializationBinding.cs
│   ├── Validation/ValidationService.ActorMaterializationTradeAuthority.cs
│   ├── Validation/ValidationService.NpcCoreChanges.cs
│   ├── GmWorkers/ActorMaterializationRepairPreservationGuard.cs
│   ├── GmWorkers/GmWorkerApplyGate.cs
│   ├── GmWorkers/GmWorkerTaskPacketBuilder.cs
│   └── CanonicalStateNormalizer/
├── Core/GameEngine/GameEngine.ValidationAndRepair.cs
└── game_master_daemon.ps1

BookOfEternityClient.Tests/
├── ActorMaterializationContractTests.cs
├── ActorMaterializationValidationTests.cs
├── NpcCoreChangesTests.cs
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
