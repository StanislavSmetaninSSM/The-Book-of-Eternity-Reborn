# Implementation Plan: QTE Practice Mode

**Branch**: `work/925-qte-practice-mode`
**Spec**: `specs/925-qte-practice-mode/spec.md`
**Source Issues**: [#925](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/925), parent [#911](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911), related [#918](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/918), [#920](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/920), [#924](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/924), consumer [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Summary

Add a standalone, client-owned QTE Practice Mode that lets players train implemented QTE mini-games outside any normal campaign and without rewards. Practice attempts reuse existing QTE validation/input/resolution/browser mini-game paths, provide player-facing feedback, and remain isolated from campaign and permanent reward/profile state.

## Technical Context

- Runtime/application authority: `BookOfEternityClient/` C# services own QTE attempt generation, validation, result resolution, local web API/write semantics, and state isolation.
- Console surfaces: existing menu/help/command patterns should expose Practice Mode without turning the console into a debug shell.
- Browser presentation: `BookOfEternityClient.WebFrontend/` React/TypeScript should reuse #918 QTE mini-game components and existing API contracts, keeping React presentation/input-only.
- Tests: `BookOfEternityClient.Tests/` QTE runtime, validation, browser API, documentation/source guard, and no-mutation isolation tests; frontend player-facing QTE tests.
- Docs/examples: QTE rules/help/docs must state that Practice Mode is client-owned, no-reward, no-GM-scene training and separate from Daren #919.
- Spec Kit governance: `.specify/memory/constitution.md` version 1.1.0.

## Constitution Check

- GitHub issue traceability: all implementation is tied to #925; artifacts link #925, #911, #918, #920, #924, and #919.
- Player-facing integrity: practice UI must use player-facing Russian/in-world copy and hide raw DTO/API/debug/manual-grade language in default surfaces.
- Contract/state authority: the feature is intentionally client-owned training; if implementation adds commands, help text, or docs, those must stay synchronized. No afterlife pending/control contract changes are expected.
- Test-first verification: Codex must add RED tests/source guards for catalog, no-campaign launch, real QTE resolution, browser practice surface, and no-mutation guarantees before production code.
- Orchestration discipline: Hermes owns final PR, merge, issue closure, and Spec Kit reconciliation; Codex implements and reports evidence.

## Project Structure

Expected touched areas:

- `BookOfEternityClient/` QTE practice catalog/session/attempt services or integration points near existing QTE scene services.
- `BookOfEternityClient/WebUi/` local web API/projection endpoints if browser practice needs a new player-facing route or state DTO.
- `BookOfEternityClient.Tests/` focused QTE practice, state-isolation, browser API, and documentation/source guard tests.
- `BookOfEternityClient.WebFrontend/src/` route/component/API wiring that reuses existing QTE mini-game components for practice mode.
- `BookOfEternityClient.WebFrontend/test/` player-facing tests for practice catalog, mini-game attempt UI, no debug/manual-grade leakage, retry/change/exit affordances, and key handling.
- `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, help/docs or source guards only where the practice/no-reward boundary must be documented.
- `specs/925-qte-practice-mode/` spec, plan, tasks, and contract evidence.

## Implementation Phases

1. **RED catalog and no-campaign coverage**: add failing C#/browser/frontend tests that open practice mode without a campaign, list implemented QTE types, hide unavailable types, and assert no campaign/permanent state mutation.
2. **C# practice model and attempt authority**: add a practice catalog and deterministic preset/attempt generation that validates/generated QTE configs through existing QTE services and records only ephemeral practice result state.
3. **Console and help entry point**: expose practice mode from an appropriate player-facing menu/command/help surface with no-reward/no-campaign copy and existing #920 key label behavior.
4. **Browser practice surface**: add route/API/component wiring that reuses #918 QTE mini-games and existing grade submission authority; keep advanced/debug details out of default UI.
5. **Feedback, retry, scoring boundary**: show grade/improvement feedback, retry/change/exit actions, and local-only #924 score summaries without persistence.
6. **Docs/source guards and Spec evidence**: update docs/source guards for the client-owned practice boundary and record RED/GREEN/final verification in `tasks.md`.
7. **Review/PR/closure**: independent review, local gates, PR, squash merge, issue evidence comment, cleanup, and final Russian report remain Hermes-owned.

## Verification Plan

Baseline before Spec Kit edits on 2026-06-11:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` — passed 266/266.
- `npm ci --prefix BookOfEternityClient.WebFrontend` — completed, 52 packages, 0 vulnerabilities.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` — passed: typecheck, player-facing Vitest 60/60, and Vite build succeeded.

Required final gates before PR/merge:

- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` from branch `work/925-qte-practice-mode`.
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- Focused `dotnet test` over QTE practice/runtime/browser/docs filters with non-zero counts.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` when browser/frontend files or browser DTO contracts change.
- `git diff --check origin/main...HEAD`.
- Added-line static security scan over `origin/main...HEAD` excluding documentation/plan false positives.
- Independent review before PR/merge.

## Risk and Mitigation

- **Practice mutates campaign/reward state**: make no-mutation tests mandatory across campaign files and permanent profile/reward surfaces; keep practice state ephemeral.
- **Fake practice implementation**: require tests proving attempts use existing QTE resolution helpers/endpoints/components instead of explanatory-only screens.
- **Browser authority drift**: React can compute mini-game input grades like #918, but C# owns attempt lifecycle/result/write semantics.
- **Daren/reward scope creep**: #919 owns Daren route, achievements, and New Game Ink Feather rewards; #925 only provides training and local feedback.
- **Docs drift**: source guards must fail if no-reward/no-GM-scene practice boundaries are missing.
- **Keyboard-layout regression**: include #920 key normalization/label tests for the practice surface rather than introducing a separate input map.
