# Implementation Plan: GM Worker CLI Runner

**Branch**: `1149-gm-worker-cli-runner` | **Date**: 2026-06-20 | **Spec**: `specs/1149-gm-worker-cli-runner/spec.md`

**Input**: Feature specification from `specs/1149-gm-worker-cli-runner/spec.md`

## Summary

Add a PowerShell-based local worker runner that reads the existing `BOE_WORKER_*` environment contract, builds a strict prompt for Codex/Gemini-style worker CLIs, supports dry-run prompt verification, launches a configured agent command in real mode through stdin, and documents runner-based worker profile examples.

## Technical Context

**Language/Version**: C#/.NET 8 tests; PowerShell launcher script.

**Primary Dependencies**: Existing `GmWorkerBridgePool`, `GmWorkerContractValidator`, xUnit tests, PowerShell process launching.

**Storage**: File-backed worker task/proposal handoff under `game_session`.

**Testing**: xUnit via `dotnet test`; PowerShell script invoked from C# focused tests.

**Target Platform**: Local Windows development/runtime environment.

**Project Type**: Desktop/console game client with local GM bridge scripts.

**Performance Goals**: Runner startup should add negligible overhead compared with an external agent task; no player-facing latency target because worker tasks already run as background bridge work.

**Constraints**: No cloud dependency; no CI dependency on Codex/Gemini; no direct canonical state writes from runner or workers; preserve hidden/background worker launch expectations.

**Scale/Scope**: One script, focused tests, existing worker docs/contracts/examples.

**Source Issue(s)**: #1149 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1149

**Contract Scope**: GM-facing docs, runtime worker launch protocol, tests.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerCliRunner|GmWorkerBridgeDocumentation|WorkerBridge" -p:BaseOutputPath=TestResults/bin/1149-runner/`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:BaseOutputPath=TestResults/bin/1149-full/`

## Constitution Check

- **GitHub traceability**: PASS. Source issue #1149 is linked in spec, plan, tasks, and docs updates.
- **Spec Kit fit**: PASS. This is cross-file GM worker protocol work that affects runtime launch contracts and docs.
- **Player-facing integrity**: PASS. No player-facing UI is changed.
- **Contract/state authority**: PASS. Runner explicitly preserves proposal-only handoff and existing apply gate authority.
- **Test-first path**: PASS. C# tests for the missing runner and missing-env behavior are written before implementation.
- **Verification evidence**: PASS. Focused and full dotnet commands are listed.
- **Agent orchestration**: PASS. Runner prompt preserves source issue, task packet, proposal output, and Superpowers-style worker boundaries.

## Project Structure

### Documentation (this feature)

```text
specs/1149-gm-worker-cli-runner/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── gm-worker-cli-runner-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Launcher/
└── gm_worker_cli_runner.ps1        # new local runner script

BookOfEternityClient.Tests/
├── GmWorkerCliRunnerTests.cs       # new script behavior/source tests
└── GmWorkerBridgeDocumentationTests.cs

OtherGuides/
└── GM_Worker_Bridges.md            # runner usage docs

Examples/
├── E_CLI_GM_Worker_Validation_Repair.txt
└── E_CLI_GM_Worker_Narrative_Draft.txt
```

**Structure Decision**: Keep the runner beside existing launcher/daemon PowerShell scripts because worker profiles need a stable repo path and existing GM launcher documentation already points to `BookOfEternityClient/Launcher/`.

## Complexity Tracking

No constitution violations.
