# Implementation Plan: GM Worker Profile Templates

**Branch**: `1151-gm-worker-profile-templates` | **Date**: 2026-06-20 | **Spec**: `specs/1151-gm-worker-profile-templates/spec.md`

**Input**: Feature specification from `specs/1151-gm-worker-profile-templates/spec.md`

## Summary

Add a reusable disabled-by-default GM worker template catalog, wire empty settings to receive safe templates, update fixtures/docs away from bare agent commands, and add tests that prove templates are runner-based, valid, and non-dispatching until enabled.

## Technical Context

**Language/Version**: C#/.NET 8; Markdown docs.

**Primary Dependencies**: `WorkerBridgeProfile`, `GmWorkerContractValidator`, `GameSettings`, xUnit.

**Storage**: `game_session/config.json` settings.

**Testing**: xUnit via `dotnet test`.

**Target Platform**: Local Windows console/daemon environment.

**Project Type**: Local game client runtime/settings.

**Constraints**: No worker enabled by default; no remote/cloud dependency; no browser work.

**Source Issue(s)**: #1151 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1151

**Contract Scope**: runtime settings, GM-facing docs, examples/contracts, tests.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerProfileTemplate|GmWorkerBridgeContract|GmWorkerBridgeDocumentation" -p:BaseOutputPath=TestResults/bin/1151-templates/`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:BaseOutputPath=TestResults/bin/1151-full/`

## Constitution Check

- **GitHub traceability**: PASS. Source issue #1151 is linked.
- **Spec Kit fit**: PASS. Settings/docs/contracts change together.
- **Player-facing integrity**: PASS. No player-facing UI changes.
- **Contract/state authority**: PASS. Templates are disabled and preserve proposal-only/apply-gate authority.
- **Test-first path**: PASS. Tests will be added before implementation.
- **Verification evidence**: PASS. Focused and full commands listed.
- **Agent orchestration**: PASS. Templates use the #1149 runner protocol.

## Project Structure

```text
BookOfEternityClient/Services/GmWorkers/
└── GmWorkerBridgeProfileTemplates.cs

BookOfEternityClient/Configuration/
└── GameSettings.cs

BookOfEternityClient.Tests/
├── GmWorkerProfileTemplateTests.cs
├── GmWorkerBridgeContractTests.cs
├── GmWorkerBridgeDocumentationTests.cs
└── GmWorkerBridgeTestFixtures.cs

OtherGuides/
└── GM_Worker_Bridges.md

Examples/
├── E_CLI_GM_Worker_Validation_Repair.txt
└── E_CLI_GM_Worker_Narrative_Draft.txt
```

**Structure Decision**: Place templates in `Services/GmWorkers` with existing worker contract code. `GameSettings` consumes the catalog only when no profiles are configured.

## Complexity Tracking

No constitution violations.
