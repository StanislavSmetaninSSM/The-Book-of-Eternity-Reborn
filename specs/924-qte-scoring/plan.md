# Implementation Plan: QTE Score Metrics and Ending Ranks

**Branch**: `work/924-qte-scoring`
**Spec**: `specs/924-qte-scoring/spec.md`
**Source Issues**: [#924](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/924), parent [#911](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911), consumers [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919) and [#925](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/925), related [#918](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/918)

## Summary

Add an optional generic score model to GM-authored QTE offers. The C# QTE runtime validates score models, applies deterministic grade-based metric deltas, records score audit/history, computes final ending ranks, and projects read-only score state to console/browser surfaces. Existing unscored QTE scenes remain backward compatible.

## Technical Context

- Runtime/application authority: `BookOfEternityClient/` C# services, especially existing QTE scene validation/resolution/web projection patterns.
- Tests: `BookOfEternityClient.Tests/` QTE runtime, validation, browser API, documentation/source guard, and example validation tests.
- Browser presentation: `BookOfEternityClient.WebFrontend/` React/TypeScript QTE components and player-facing tests.
- GM-facing docs/examples: `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`.
- Spec Kit governance: `.specify/memory/constitution.md` version 1.1.0.

## Constitution Check

- GitHub issue traceability: all implementation is tied to #924; artifacts link #924 and parent/consumer issues.
- Player-facing integrity: console/browser score summaries must use in-world Russian text and hide raw DTO/API/debug wording in default UI.
- Contract/state authority: score model is a GM-authored QTE contract, so validation, docs, examples, manifests/source guards, and UI projections must update together.
- Test-first verification: Codex must add RED validation/runtime/browser/docs tests before production code and record RED/GREEN evidence in `tasks.md`.
- Orchestration discipline: Hermes owns PR, merge, final verification, issue closure; Codex implements and reports evidence.

## Project Structure

Expected touched areas:

- `BookOfEternityClient/` QTE model/service files that currently parse, validate, resolve, record, and project QTE scene state.
- `BookOfEternityClient/WebUi/` QTE browser DTO/projection files for read-only score state and final summary.
- `BookOfEternityClient.Tests/` focused QTE validation/runtime/browser/docs tests.
- `BookOfEternityClient.WebFrontend/src/` QTE panel/result rendering and typed contracts if browser score surfaces are projected.
- `BookOfEternityClient.WebFrontend/test/` player-facing score rendering tests if frontend surfaces change.
- `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, documentation/source guards.
- `specs/924-qte-scoring/` spec, plan, tasks, and contract evidence.

## Implementation Phases

1. **RED validation and runtime tests**: encode valid scored offer, malformed score models, deterministic score application, rank selection, and unscored backward compatibility.
2. **C# model/validation/runtime**: add score model parsing/validation helpers, runtime score state, delta application, clamping, final rank computation, and audit/history fields.
3. **Console/browser projection**: show active/final score state according to visibility rules; project read-only browser DTOs and update TypeScript contracts/UI if needed.
4. **Docs/examples/source guards**: update QTE contract docs and worked example for ordinary GM-authored scored QTE scenes.
5. **Spec evidence and verification**: update `tasks.md` with RED/GREEN/final verification evidence, run focused/broad local gates, and leave PR/merge/closure to Hermes.

## Verification Plan

Baseline before spec edits on 2026-06-10:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` — passed 247/247.
- `npm ci --prefix BookOfEternityClient.WebFrontend` — completed, 52 packages, 0 vulnerabilities.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` — passed: typecheck, player-facing tests 59/59, Vite build succeeded.

Required final gates before PR/merge:

- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` from branch `work/924-qte-scoring`.
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- Focused `dotnet test` over QTE validation/runtime/browser/docs filters with non-zero counts.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend or browser contracts change.
- `git diff --check origin/main...HEAD`.
- Added-line static security scan over `origin/main...HEAD` excluding implementation-plan false positives.
- Independent review before PR/merge.

## Risk and Mitigation

- **Score model overreach into Daren rewards**: keep #924 generic; Daren rewards/unlocks remain #919.
- **Browser authority drift**: browser may render score state and local QTE mini-games, but C# owns score state mutation and final rank/audit.
- **Backward compatibility regression**: require tests proving unscored offers still resolve exactly as before.
- **Hidden metric spoilers**: visibility rules and frontend tests must prove hidden metrics do not leak in default active UI.
- **GM docs drift**: documentation/source guard tests must fail if score model fields/examples are not synchronized.
