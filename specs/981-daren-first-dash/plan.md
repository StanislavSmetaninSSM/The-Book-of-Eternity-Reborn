# Implementation Plan: Daren Scene 13 Full Literary Page

**Branch**: `work/981-daren-first-dash` | **Date**: 2026-06-15 | **Spec**: `specs/981-daren-first-dash/spec.md`

**Input**: Feature specification from `specs/981-daren-first-dash/spec.md`; source GitHub issue [#981](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/981); parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955).

## Summary

Rewrite only Daren QTE scene `pursuit` / "Первый рывок" into a substantial Russian dark-fantasy first-dash escape page while preserving the existing shared route action contract. Add a focused failing test first so the old synopsis is rejected and the final page proves console/browser parity through shared route data.

## Technical Context

**Language/Version**: C#/.NET 8 for shared route data and tests.

**Primary Dependencies**: Existing `QteSceneService` route model, xUnit tests, and local Spec Kit scripts.

**Storage**: N/A; no persistence or runtime state shape changes.

**Testing**: `dotnet test` focused Daren route tests and affected C# slice; `dotnet build` for client and tests.

**Target Platform**: Local console client and local browser client consuming shared C# Daren route data.

**Project Type**: Local game client with authored route content.

**Performance Goals**: N/A; copy-only scene rewrite must not introduce runtime work.

**Constraints**: Preserve route mechanics, action ids, check type/config, routing, score deltas, rewards, endpoints, runtime state, browser/console parity, and frontend files.

**Scale/Scope**: One scene, one focused test guard, one Spec Kit feature directory.

**Source Issue(s)**: [#981](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/981), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955).

**Contract Scope**: Player-facing shared route prose for console/browser; no GM-facing or runtime-state contract change.

**Verification Commands**:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj \
  -p:IsTestProject=true \
  --filter "FullyQualifiedName~DarenQteShowcaseTests" \
  --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj \
  -p:IsTestProject=true \
  --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" \
  --logger "console;verbosity=minimal"

dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore

dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true

git diff --check origin/main...HEAD
```

Run frontend verification only if frontend/React/browser files change or a browser rendering bug is found.

## Constitution Check

- **GitHub traceability**: PASS. Spec, plan, tasks, checklist, and contract reference #981 and parent #955.
- **Spec Kit fit**: PASS. The issue changes player-facing console/browser UX copy in shared route data and needs durable handoff evidence.
- **Player-facing integrity**: PASS. The plan requires Russian in-world copy, no implementation terminology in default prose, and shared C# route data for console/browser parity.
- **Contract/state authority**: PASS. No canonical state, GM prompt, validation, pending/control, or runtime contract changes are planned; the plan explicitly prohibits mechanics and runtime drift.
- **Test-first path**: PASS. A focused `DarenQteShowcaseTests` guard must be added and observed failing before production prose changes.
- **Verification evidence**: PASS. Focused tests, affected slice, builds, Spec Kit prerequisite check, diff check, and static scan are listed.
- **Agent orchestration**: PASS. Codex executes locally with Spec Kit artifacts and Superpowers TDD/debug/verification discipline; Hermes owns independent review, PR, merge, and issue lifecycle.

## Project Structure

### Documentation (this feature)

```text
specs/981-daren-first-dash/
├── spec.md
├── plan.md
├── contracts/
│   └── daren-scene-page.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Services/QteSceneService.Daren.cs
BookOfEternityClient.Tests/DarenQteShowcaseTests.cs
```

**Structure Decision**: Keep route prose in the existing shared C# route data and extend the existing Daren showcase test suite. Do not add frontend, endpoint, state, reward, or documentation contract files outside the #981 Spec Kit artifacts unless verification exposes a real issue.

## TDD Strategy

1. Add `DarenPursuit_ReadsAsFirstDashPageWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` before changing the scene prose.
2. The guard asserts:
   - title and shared route data parity;
   - action id, `TimingBar`, characteristic, difficulty, label, routing, and score deltas;
   - substantial page length, sentence count, and Daren active POV;
   - grouped motif coverage for hall/window/courtyard, Daren body/breath/step timing, stolen staff/belt/balance, Orvald/Lukyan/witness or guard pressure, lantern/voice/pursuit control, and the timing-window lead-in;
   - absence of default player-facing technical terms.
3. Run the focused Daren test filter and record RED evidence against the existing compact synopsis.
4. Replace only the `pursuit` narrative / `DarenShowcaseBeat.PlayerText` in `QteSceneService.Daren.cs`.
5. Run focused and affected verification to GREEN.

## Baseline Evidence Before Implementation

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed: 80 passed / 0 failed / 0 skipped / 80 total.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed: 349 passed / 0 failed / 0 skipped / 349 total.
- Spec Kit prerequisite check is expected to resolve `specs/981-daren-first-dash` after `tasks.md` is present.

## Review Requirements

Independent review must check:

- `pursuit` literary quality against issue #981 and parent #955;
- Daren remains the active protagonist in a first-dash escape/timing scene;
- the prose carries forward the staff theft, ring/noise/evidence risk, Orvald/Lukyan/witness pressure, open window/courtyard, and lantern/guard pursuit stakes;
- the scene naturally narrows into the existing "Рвануть в окно погони" action;
- no route/action/check/config/routing/scoring/reward/frontend/runtime drift occurred;
- tests use grouped motif coverage rather than a weak one-token bucket;
- Spec Kit artifacts do not stale-reference #980/staff theft as the current source issue.

## Complexity Tracking

No constitution violations are planned.
