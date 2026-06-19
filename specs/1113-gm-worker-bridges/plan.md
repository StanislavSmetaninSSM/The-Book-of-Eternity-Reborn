# Implementation Plan: Explicit GM Worker Bridges

**Branch**: `1113-gm-worker-bridges` | **Date**: 2026-06-20 | **Spec**: specs/1113-gm-worker-bridges/spec.md

**Input**: Feature specification from `specs/1113-gm-worker-bridges/spec.md`

## Summary

Add explicit GM worker bridge support so the main GM can delegate validation repair, narrative drafting, and proposal-only analysis tasks to configured local agents while preserving a single canonical state authority. The implementation will add worker profile configuration, role-based task routing, worker task/proposal contracts, a bridge pool, an apply gate, audit diagnostics, tests, and GM-facing documentation/examples.

## Technical Context

**Language/Version**: C# / .NET 8, PowerShell launch scripts, Markdown/JSON contracts

**Primary Dependencies**: Existing `BookOfEternityClient`, `BookOfEternityGMBridge`, Spectre.Console, file-backed JSON state, local loopback bridge services

**Storage**: Existing JSON files under `game_session`, client profile/settings files, new worker task/proposal/audit files under controlled runtime locations

**Testing**: xUnit in `BookOfEternityClient.Tests`, focused source guards, live agent-console smoke tests, documentation coverage tests

**Target Platform**: Local Windows desktop/dev environment first, preserving existing local/offline play

**Project Type**: Local game client and local GM bridge orchestration

**Performance Goals**: Worker lifecycle diagnostics visible within 2 seconds; worker dispatch for validation repair or narrative drafting must not block the UI indefinitely

**Constraints**: No cloud dependency; no direct worker writes to canonical state; main GM remains final authority; worker proposals must pass scope gates; repair proposals must pass validation gates; narrative drafts are not player-visible until the main GM uses them

**Scale/Scope**: MVP supports general role-based worker task routing with at least validation repair and proposal-only narrative drafting; broader lore/NPC/QTE/console-output delegation uses the same proposal-only path and can expand after the MVP contract is stable

**Source Issue(s)**: #1141 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1141

**Contract Scope**: GM-facing prompts, runtime-state, validation, docs, examples, console diagnostics, agent-console/e2e

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "WorkerBridge|GmBridge|ValidationRepair|ProposalOnly|AgentConsoleLiveSmokeTests"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|SourceGuard"`
- Full client test run before merge: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **GitHub traceability**: PASS. Source issue #1141 is linked in spec, plan, and tasks.
- **Spec Kit fit**: PASS. This is a cross-contract architecture epic affecting runtime, validation, GM bridge, docs, examples, and e2e.
- **Player-facing integrity**: PASS. MVP is mostly GM/daemon-facing; narrative drafts must not become player-facing until the main GM explicitly uses or rewrites them; any console diagnostics must avoid player-facing debug leakage unless in advanced diagnostics.
- **Contract/state authority**: PASS. Main GM and daemon apply gate remain canonical state authority; worker output is proposal-only.
- **Test-first path**: PASS. Tasks include contract and regression tests before implementation.
- **Verification evidence**: PASS. Focused and full C# verification commands are listed.
- **Agent orchestration**: PASS. Feature itself defines the safe orchestration model; implementation tasks require explicit delegation packets and audit logs.

## Project Structure

### Documentation (this feature)

```text
specs/1113-gm-worker-bridges/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── gm-worker-bridge-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/
├── Configuration/                 # worker profile settings and user configuration
├── Core/GameEngine/               # main GM integration, validation repair, and narrative delegation entrypoints
├── Services/                      # worker task/proposal/apply gate services
├── Services/Validation/           # validation issue packaging and post-proposal validation
├── UI/                            # console advanced diagnostics
└── game_master_daemon.ps1         # daemon launch/config plumbing

BookOfEternityGMBridge/
└── Program.cs                     # bridge worker mode, status, task dispatch, proposal protocol

BookOfEternityClient.Tests/
├── *WorkerBridge*Tests.cs         # new worker bridge contract, lifecycle, apply gate tests
├── *ValidationRepair*Tests.cs     # validation repair delegation regression tests
├── *ProposalOnly*Tests.cs         # narrative/analysis proposal-only delegation tests
└── source/documentation guards    # docs/examples/prompt coverage

OtherGuides/
Examples/
docs/
```

**Structure Decision**: Worker orchestration belongs in runtime services and bridge integration, not in console rendering. The daemon/main GM dispatches typed task packets; worker bridge processes return proposals; the client-side apply gate validates repair changes and stores proposal-only creative/analysis output without applying files. Browser UI remains out of scope.

## Complexity Tracking

No constitution violations. Complexity is justified by explicit multi-process orchestration, state authority protection, and auditability requirements.
