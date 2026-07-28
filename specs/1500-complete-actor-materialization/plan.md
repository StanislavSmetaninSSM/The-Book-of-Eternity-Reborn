# Implementation Plan: Complete Actor Materialization

**Branch**: `1500-complete-actor-materialization` | **Date**: 2026-07-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/1500-complete-actor-materialization/spec.md`

## Summary

Add a setting-agnostic, versioned first-materialization contract for persistent Mortal NPCs and significant afterlife actors. The validator requires an actor-bound private `materialization` envelope for current-turn creations and promotions, compares declared section dispositions and capabilities with canonical actor data, requires 3-5 integer-valued Mortal personality traits and at least one setting-defined numeric Mortal characteristic, derives one effective Mortal identity so null permanent-ID plus colliding `initialId` cannot evade continuity, and retains validated pre-turn inventory so true legacy promotion can resend only a semantically unchanged snapshot through either full carrier. Ordinary existing core changes use the Mortal-only closed `NPCCoreChanges` command: exact permanent identity, reason, absolute resulting values, bounded profile/location/progression/setting-owned characteristic/faction/Fate Card groups, deterministic mirror reduction, and command consumption. Malformed, non-object, and duplicate-member current authority fails closed; historical full carriers preserve every actor-owned domain; Fate Card additions pass the complete production nested validator before reduction; and canonical ID-less Block 7 skills remain valid combat evidence. Duplicate-key current and validated pre-turn authority fails closed through structured issues while semantic equality stays property-order-insensitive. Afterlife type-specific records bind to common profiles; the first envelope on an existing bound profile revalidates actual Guardian/resident journal or exact common-profile memory while untouched legacy profiles remain readable. Worker subprocesses run in ephemeral detached `.worker_runtime` snapshots exposing only exact pinned task context; only a valid proposal and its declared `contentRef` bytes may be imported. The worker apply gate routes memory repairs by actor type, mechanically preserves every field except the exact actor-owned target, classifies every `game_state/meta/` repair as afterlife-scoped, and holds one canonical write lease from final authority verification through all writes, validation, read-only revalidation, rollback, and decision. Mortal collision repair remains main-GM-only; exact-snapshot inventory and empty-characteristics repairs are constrained to one issue-bound actor, carrier, and field, and characteristic values must parse as finite numbers rather than merely use JSON number syntax. Proposal `status` and changed-file operation are mandatory: omission/unspecified values are invalid, only explicit completed proposals with exact Add/Replace/Delete operations may enter apply, and terminal failure proposals remain mutation-free diagnostics. Worker audit append is concurrency-safe, all producers use one source-guarded UTC-millisecond plus GUID generator, and telemetry failure cannot revoke accepted canonical bytes. Worker task publication is create-only; proposal publication stages a complete bundle and exposes it through one create-only atomic directory rename only after exact task bytes still prove the current session generation. Reference-counted worker gates retire when idle, and cancellation awaits the entire process tree before slot reuse. Save/load uses the same external canonical lease as worker apply plus a durable `.boe_runtime/load-transactions` journal so interrupted swaps recover before directory initialization and failed rollback preserves the last valid backup. Worker dispatch owns a repair before legacy fallback is exposed, including direct revalidation after an accepted apply whose ready publication fails. Derived output-only retries retain the original canonical target set and accept only output strictly newer than every target's latest actual write. The client never infers skills, possessions, roles, capabilities, characteristic keys, carrying formulas, or class-stat allocations from prose, and generated templates use current-world authority with nullable setting-owned carrying results. System Guardian seeds remain client-owned and receive deterministic envelopes that match their exact seeded capabilities. Prompts, examples, manifests, contract documentation, and source guards are synchronized; Phases 19-22 harden shared worker isolation, complete bundle publication, dispatch uniqueness, runtime concurrency/cancellation, explicit Mortal/afterlife path ownership, recoverable save/load linearization, cleanup, and afterlife repair authority but add no new afterlife pending/control gameplay surface.

Phase 23 closes generation and load lifecycle races: every canonical writer performs post-lease interrupted-load recovery, durable generation authority moves to `.boe_runtime/session-generation/current.json` and rotates on load/New Game, save/load transitions strip worker evidence, and public refresh becomes one lease-scoped read/modify/write.

Phase 24 makes worker launch and apply mechanically recoverable. The parent creates private current-user named control/status pipe servers, starts a hidden host with endpoint names plus a per-launch nonce only, uses parent-side client PID authentication to accept both clients only from that exact PID, and sends the executable, arguments, working directory, and environment only in a strict typed `Launch` frame. The host retains both channels; the configured worker receives no pipe handle and no worker-accessible marker file exists. The direct worker cannot start before complete-tree attachment and typed `Release`; typed `Completed` with a non-null exit code is published immediately after direct-process exit and before output drain, followed by typed `OutputDrained` after bounded capture. A Windows Job Object is the supported queryable descendant boundary; unsupported platforms fail closed before release. Timeout/cancellation remain authoritative, all termination waits are bounded, and cleanup uncertainty quarantines capacity. Multi-file worker apply persists intent, exact before-images, and expected applied hashes under `.boe_runtime/worker-apply-transactions` before the first canonical write. Every later writer recovers or fails closed; a committed cleanup failure remains retryable and cannot revoke accepted bytes. These shared harness guarantees add no Mortal or afterlife GM-authored state surface.

Phase 25 closes the remaining authority handoff gaps. The exact durable reserved task is reloaded under the canonical lease and is the sole apply authority; caller mutation cannot expand it. Lowercase canonical GUID text in `N` format is required for the session generation, and the proposal id `inbox` is reserved for the derived inbox directory. Typed `SessionReplaced` propagates through reservation, publication, apply, ready publication, and the game loop, aborting the old repair without legacy fallback or rollback into a replacement session. Repair telemetry uses a generation-bound atomic append, the latest validation-repair task is excluded from saves, and committed apply cleanup removes the transaction directory before its active journal so failure remains retryable.

Phase 27 extends durable generation from worker handoff to every complete logical
GM flow. A player turn, raw life evaluation, transition check, incarnation
trigger, GM terminal wait, and New Game bootstrap each run as one immutable
session operation. Every canonical mutation verifies the bound generation after
recovery and under the canonical write lease, while terminal polling and the
outer return path perform explicit durable checks. The client must not hold the
lifecycle lease while waiting for the GM; load/New Game hold that short lease
only while replacing state, in lifecycle-then-canonical order. A typed
`SessionReplaced` remains sticky through nested catches and blocks old cleanup,
rollback, telemetry, and finalization writes. Detached worker runtime also moves
outside the replaceable session and enforces 1 MiB proposal, 4 MiB per-artifact,
16 MiB aggregate-import, and 65,536-character per-stream capture budgets.

Phase 28 closes the final independent-review gaps without adding a new gameplay
surface. Incarnation staging binds its durable generation before the first
canonical read, terminal completion/error bytes are captured together under one
canonical lease and resolved from that immutable snapshot, and load rebinds all
runtime identity and transient state. Failed or unconfirmed worker executions
remain diagnostic-only, rollback persists a durable `rolledBack` journal phase
before cleanup, and configured worker runtime aliases into `game_session` fail
closed. Mortal bootstrap remains a setting-neutral scaffold: prose never creates
skills, items, actors, capabilities, money, experience, or carrying values;
only explicit GM-authored `structuredGmAuthority` can require those mechanics.
Contract tests use their own explicit complete actor fixtures so production
bootstrap behavior cannot silently become test authority.

Phase 29 closes the fourth exact-diff review. `prepare-live-turn` binds the
durable session generation before its first canonical read and performs cleanup,
snapshot capture, manifest/authority writes, and request publication under one
generation-bound no-follow canonical transaction. Worker cancellation/timeout
and successful proposal publication use one atomic transition: cancellation
before publication leaves no applyable bundle, while publication first makes the
complete bundle durable. Production `GmWorkerApplyGate` construction requires
the real `ValidationService`. Fresh Mortal bootstrap keeps experience, carrying,
faction resources/control, and faction power/progression mechanics unassigned;
any first-turn values require matching structured GM authority rather than
client defaults.

Phase 31 closes the post-T156 Sol/max filesystem review. Browser rollback
recovery retains evidence until every canonical and typed external before-image
has restored and permits cleanup only for exact client-owned roots.
`.boe_runtime` becomes an explicitly no-follow physical authority for locks,
proposal staging, and save staging. The external Daren reward profile receives
a durable typed exact-byte before-image whose staged/committed state survives
process interruption and session replacement. Saves are built outside the
canonical session and published through one generation-bound no-follow move;
autosave deletion revalidates each canonical relative target. Browser rollback
roots are omitted from saves and stripped from replacement archives.

## Technical Context

**Language/Version**: C# 12 / .NET 8; PowerShell GM launcher and documentation entrypoints; JSON state contracts

**Primary Dependencies**: `System.Text.Json`, existing file-system abstraction, validation/normalization services, Spectre.Console client stack; no new package

**Storage**: Local file-backed JSON (`game_state/npcs/npc_core.json`, `game_state/meta/afterlife_entity_profiles.json`, Guardian/resident/Shining files, validated pre-turn snapshots) plus client-owned external runtime authority under `.boe_runtime/load-transactions`, `.boe_runtime/worker-apply-transactions`, `.boe_runtime/session-generation/current.json`, `.boe_runtime/proposal-staging`, and `.boe_runtime/save-staging`; detached workers use a separate external runtime base selected by `BOE_WORKER_RUNTIME_BASE_PATH` or the platform default

**Testing**: xUnit through `dotnet test`; documentation/source-guard tests; fixture-driven canonical-state validation

**Target Platform**: Local Windows game client on .NET 8 with a Job Object complete-process-tree boundary; worker execution fails closed on unsupported platforms while gameplay contracts remain platform-neutral JSON

**Project Type**: Console/browser RPG client with local daemon/bridge and file-backed canonical state

**Performance Goals**: Linear validation in actor and section counts; no network or model calls; negligible additional latency for ordinary save sizes

**Constraints**: Preserve legacy saves; duplicate/malformed current and continuity authority fails closed without escaping validation; all historical actor-owned fields remain command-owned; Fate Card reduction reuses full production validation; canonical skills do not require synthetic IDs; no client-authored narrative data; no genre keyword inference or universal characteristic/carrying/class-stat formula; no new cloud dependency; dedicated delta commands and bounded `NPCCoreChanges` retain separate authority; worker repair remains exact-actor/exact-field bounded with explicit operations, detached pinned-context execution, pre-publication content digest verification, globally unique repair dispatch IDs, create-only immutable task reservation, one complete proposal-bundle directory rename guarded by exact current-session task bytes, runtime `MaxConcurrentTasks` with idle gate retirement and process-tree-safe cancellation, private nonce-bound host frames with no worker-visible marker files, Windows Job Object containment with fail-closed unsupported platforms, case-insensitive canonical path identity, explicit Mortal ownership for current-world lore/core player status, afterlife validation-repair surfaces confined to exact paths under `game_state/meta/` while typed content tasks retain exact task-provided control/report surfaces, no-follow cleanup, exact-byte realm/setting authority, and one external canonical write lease shared by apply, bundle publication, built-in backup/restore/clear/save/load operations, lease-aware refresh, and mirror repair; load and worker apply each use durable recoverable external journals; output freshness retains its original target chain and uses strict ordering; metadata stays out of player projections

**Lifecycle constraints**: Post-lease recovery fails every canonical writer closed while a load, worker-apply, or staged browser-write transaction remains unresolved; browser recovery runs before ordinary mutation and before replacement, retains evidence on partial restore, and never imports rollback authority from a save; session-generation authority is external to the swappable save and invalidates stale workers across load/New Game; complete GM flows use an immutable session operation whose generation is checked inside every canonical write and at terminal/final return boundaries; the short lifecycle lease is never held across GM waits and always precedes the canonical lease; public refresh cannot split mirror read and repair across authority moments; configured worker code cannot run before Windows Job attachment or after handshake timeout/cancellation; private host frames require the exact nonce and complete schema; process-tree and unattached-host termination confirmation are bounded; timeout/cancellation remain authoritative; and unconfirmed cleanup quarantines rather than reuses capacity.

**Scale/Scope**: Mortal NPC core validation and bounded reduction, afterlife common profiles and cross-file actors, repair harness, System Guardian seed generation, prompts/docs/examples/source guards, focused tests. R3-I-2/R3-I-3/R3-M-1 are Mortal-only and do not alter afterlife pending/control, response, receipt, scheduler, lifecycle, or authority contracts.

**Source Issue(s)**: [#1500](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500), related [#1446](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1446) and [#1461](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1461)

**Contract Scope**: GM-facing prompts, runtime state, validation, normalization preservation, repair packets, Mortal World, Chaos Sea, Shining Abode, docs, examples, manifests, console/browser metadata non-leakage. Phases 19-22 move every worker to a detached pinned-context runtime, verify complete handoff imports before publication, uniquely identify every dispatch, publish proposals as complete session-generation-bound bundles, enforce profile concurrency and process-tree cancellation, preserve timeout truth, treat every `game_state/meta/` repair as exact-path afterlife scope, classify current-world lore and core player status as Mortal, unify Windows path identity, make apply/save/load, proposal publication, refresh/mirror repair, and built-in canonical writers one external lease protocol, recover interrupted loads through a durable external journal, and clean detached workspaces without following reparse points. They add no new afterlife pending/control action, response field, receipt, report, scheduler contour, or normalizer side effect.

**Phase 23 scope**: Durable generation, writer recovery fencing, atomic refresh, attach-before-launch process ownership, and slot quarantine change only client/daemon harness behavior. Mortal and afterlife prompts explain the safety boundary, but no gameplay command, pending/control action, response field, receipt, report, scheduler contour, normalizer side effect, or GM-authored schema changes.

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
- **Contract/state authority**: Canonical JSON, existing dedicated delta commands, and the closed Mortal-only `NPCCoreChanges` reducer remain authoritative; workers receive only detached pinned context, complete imports are digest-verified before one atomic bundle publication bound to external durable generation, task IDs and timeout outcomes are immutable, path identity/scope is exact, operations are explicit, setting/Soul authorities remain read-only and exact-byte pinned under one external canonical lease shared by built-in writers and recoverable load, refresh is one lease-scoped read/modify/write, worker code starts only inside owned process-tree authority, output freshness retains its target chain, and prompts/docs/examples are synchronized for both realms.
- **Test-first path**: Each actor family, cross-file binding, legacy path, repair packet, and deterministic seed starts with focused failing tests.
- **Verification evidence**: Focused, documentation-sensitive, and full test commands are listed above.
- **Agent orchestration**: Any delegated implementation packet must include #1500, this spec/plan/tasks set, Superpowers TDD/review requirements, and verification commands.

**Gate result before research**: PASS. Issue #1500 is tracked; harness-first validation, legacy safety, documentation synchronization, and verification evidence are explicit.

**Gate result after design**: PASS. The data model keeps narrative authority with the GM, uses validated pre-turn state for legacy/new classification, preserves dedicated delta contracts, bounds `NPCCoreChanges` to named existing-core fields, gives workers only a detached pinned-context snapshot, hash-pins setting authority for finite worker-authored characteristics, binds every meta repair to exact read-only Soul authority, holds one canonical apply lease through validation and decision, fences writers on recovery, externalizes generation identity, owns complete worker process trees before launch, rejects unspecified changed-file operations, retains the full canonical target chain for strict output freshness, and documents both Mortal and afterlife authoring paths.

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
│   ├── GmWorkers/GmWorkerOwnerMonitor.cs
│   ├── GmWorkers/GmWorkerProcessHost.cs
│   ├── GmWorkers/GmWorkerProcessTree.cs
│   ├── GmWorkers/GmWorkerTaskPacketBuilder.cs
│   └── CanonicalStateNormalizer/
├── Core/GameEngine/GameEngine.ValidationAndRepair.cs
└── game_master_daemon.ps1

BookOfEternityClient.Tests/
├── ActorMaterializationContractTests.cs
├── ActorMaterializationValidationTests.cs
├── NpcCoreChangesTests.cs
├── SystemGuardianLibraryServiceTests.cs
├── GmWorkerBridgeLifecycleTests.cs
├── GmWorkerOwnerMonitorTests.cs
├── GmWorkerProcessTreeTests.cs
├── SessionOperationContextTests.cs
├── StateDistributorCanonicalLeaseTests.cs
├── StoryServiceTests.cs
├── SaveLoadServiceTests.cs
├── StateManagerTests.cs
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

## Phase 30 implementation strategy

The four final exact-diff reviews are remediated in dependency order:

1. close the session-operation and replacement authority races;
2. harden physical canonical confinement, exact-byte rollback, and atomic
   snapshots;
3. make durable worker reservation and terminal publication the sole authority;
4. bind browser multi-write and generated-media commits;
5. close Mortal/afterlife actor authority, bootstrap, and projection gaps;
6. synchronize every active prompt/example/manifest/source guard and verify from
   a clean checkout.

Each behavior starts with a deterministic RED test. Concurrency claims use
barriers or fault injection, never delays. Production code and tests added in
earlier phases are tracked before the first Phase 30 verification checkpoint.
No task may close from an agent report alone; the controller inspects the diff
and reruns the named focused suite.
The final independent exact-diff review uses `gpt-5.6-sol` with max reasoning.

## Phase 31 implementation strategy

The five post-T156 findings are remediated as one auxiliary-authority chain:

1. retain browser evidence until complete restore and constrain cleanup to exact
   owned roots;
2. make `.boe_runtime` locks and staging paths physical no-follow authority;
3. persist and recover the Daren external profile before-image;
4. stage save archives externally and revalidate save publication/autosave
   deletion at the canonical mutation boundary;
5. exclude browser rollback roots from every save and replacement import.

Each boundary has a deterministic RED test using injected barriers, exact bytes,
or Windows junctions. The final gate repeats focused suites, the complete test
project, Release build, static parsing, Spec Kit analysis, and a fresh
`gpt-5.6-sol`/max exact-diff review.

## Phase 32 implementation strategy

The second post-T156 exact-diff review found five remaining authority gaps. They
are remediated in dependency order:

1. extend physical no-follow validation to durable load, generation, and worker
   transaction siblings;
2. validate the physical final path of each opened canonical/lifecycle lock
   handle before granting its lease;
3. add a durable cleanup-only browser rollback phase and delete the manifest
   last;
4. route console Daren completion through canonical recovery before reading or
   writing the shared external profile;
5. strip both exact-file and manifestless-directory rollback roots during load
   and New Game.

Each item starts with a deterministic RED regression: junctions for durable
runtime siblings, an injected lock-directory swap for opened-handle identity, a
locked backup for retryable cleanup, an interrupted-browser/console-completion
sequence for Daren, and crafted archives plus manifestless trees for replacement
cleanup. The final gate repeats focused and complete verification and requires a
fresh `gpt-5.6-sol`/max exact-diff review with no Critical or Important finding.

## Complexity Tracking

No constitution violations require an exception.
