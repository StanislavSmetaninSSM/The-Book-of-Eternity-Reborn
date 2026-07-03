# Implementation Plan: Training Vitrines

**Branch**: `feature/1378-training-system` | **Date**: 2026-07-03 | **Spec**: `specs/1378-training-vitrines/spec.md`

**Input**: Feature specification from `specs/1378-training-vitrines/spec.md`

## Summary

Add a teacher/mentor training showcase system that lets the GM materialize readable training offers, then lets the client complete legal purchases locally with validation-safe receipts. Mortal World training spends money plus current-level XP progress; afterlife mentor training discounts Spiritual Art upgrades while self-training remains a very expensive fallback. The implementation must update console, browser, validators, GM docs/examples, and live-test checklist coverage.

## Technical Context

**Language/Version**: C#/.NET 8, TypeScript/React/Vite, JSON file-backed game session state.

**Primary Dependencies**: Existing game client runtime, ExplorerMode command protocol, Spectre.Console, browser command-result renderer, local validators/normalizers, GM bridge prompt/docs/examples.

**Storage**: Game session JSON under `BookOfEternityClient/game_session` and canonical state files under `game_state/`, plus pending/control files for showcase refresh requests and receipts.

**Testing**: xUnit in `BookOfEternityClient.Tests`, frontend verify script, manual console/browser smoke checks, later live GM bridge test.

**Target Platform**: Local Windows console client and local browser client.

**Project Type**: Local desktop/console game client with browser frontend and local GM bridge.

**Performance Goals**: Training command output should be instant for ordinary saves; browser rendering must remain usable with dozens of offers through selectors/collapsible cards.

**Constraints**:
- No implementation without source issues #1377-#1385.
- The GM cannot be expected to read implementation source during play.
- GM-authored contracts must be documented with worked examples.
- Training purchases must be impossible from stale or impossible showcases.
- Console remains the reference for complete player-facing facts; browser must reach parity.

**Scale/Scope**: One local player, one active realm, multiple NPC teachers/afterlife mentors, dozens of offers.

**Source Issue(s)**:
- #1377 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1377
- #1378 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1378
- #1379 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1379
- #1380 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1380
- #1381 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1381
- #1382 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1382
- #1383 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1383
- #1384 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1384
- #1385 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1385

**Contract Scope**: player-facing, GM-facing prompts, runtime-state, validation, docs, examples, console, browser, frontend.

**Verification Commands**:

```powershell
dotnet restore BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "Training|Skill|SpiritualArt|Validation"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
npm --prefix BookOfEternityClient.WebFrontend run verify
```

Manual:

- Run console `/обучение` in Mortal World, Chaos Sea, and Shining Abode test saves.
- Compare browser training output with console facts.
- Add training vitrines to the next live GM bridge checklist after implementation.

## Constitution Check

- **GitHub traceability**: PASS. Source issues #1377-#1385 are linked.
- **Spec Kit fit**: PASS. This is player-facing, cross-client, validation/runtime, GM contract, docs/examples, and multi-session work.
- **Player-facing integrity**: PASS. Console/browser Russian labels, no raw JSON, no internal keys, and browser prototype parity are explicitly required.
- **Contract/state authority**: PASS. Training showcase, purchase receipt, refresh request, validator authority, and GM docs/examples are planned.
- **Test-first path**: PASS. Contract and validation tests precede service/UI implementation.
- **Verification evidence**: PASS. Focused C#, docs, frontend, and manual checks are listed.
- **Agent orchestration**: PASS. This plan and issues will be passed to any worker; no agent report can close issues without inspected diffs and verification.

## Project Structure

### Documentation (this feature)

```text
specs/1378-training-vitrines/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── training-showcases.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/
├── Services/                    # training showcase services, purchase receipts, refresh requests
├── UI/ExplorerMode/             # console command and menus
├── CommandProtocol/             # command catalog and browser command payloads
└── game_session/                # local test fixtures/manual smoke state

BookOfEternityClient.Tests/      # C# unit/integration/docs/source-guard tests
BookOfEternityClient.WebFrontend # React command result cards
TaskGuides/                      # GM task guidance
OtherGuides/                     # contracts and terminology
Examples/                        # worked GM examples and validation manifest
docs/                            # live-test checklist and audit notes
```

**Structure Decision**: Add the feature beside existing trade/progression services. Do not bury training inside NPC trade because afterlife mentor training and self-training fallback share the same progression authority but not the same inventory semantics.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Cross-client implementation | Training is player-facing in both console and browser | Console-only would repeat the current browser parity problem |
| GM docs/examples plus validators | The GM authors teacher/mentor showcases | Prompt-only guidance would not prevent stale or impossible purchases |
