# Implementation Plan: Daren Scene 10 Full Literary Page

**Branch**: `work/978-daren-alarm-pulse` | **Date**: 2026-06-12 | **Spec**: `specs/978-daren-alarm-pulse/spec.md`

**Input**: Feature specification from `specs/978-daren-alarm-pulse/spec.md`; source GitHub issue [#978](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/978); parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955).

## Summary

Rewrite only Daren QTE scene `timed_rhythm` / "Пульс сигнализации" into a substantial Russian dark-fantasy rhythm-stealth page while preserving the existing shared route action contract. Add a focused failing test first so the old synopsis is rejected and the final page proves console/browser parity through shared route data.

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

**Source Issue(s)**: [#978](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/978), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955).

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

- **GitHub traceability**: PASS. Spec, plan, tasks, checklist, and contract reference #978 and parent #955.
- **Spec Kit fit**: PASS. The issue changes player-facing console/browser UX copy in shared route data and needs durable handoff evidence.
- **Player-facing integrity**: PASS. The plan requires Russian in-world copy, no implementation terminology in default prose, and shared C# route data for console/browser parity.
- **Contract/state authority**: PASS. No canonical state, GM prompt, validation, pending/control, or runtime contract changes are planned; the plan explicitly prohibits mechanics and runtime drift.
- **Test-first path**: PASS. A focused `DarenQteShowcaseTests` guard must be added and observed failing before production prose changes.
- **Verification evidence**: PASS. Focused tests, affected slice, builds, Spec Kit prerequisite check, diff check, and static scan are listed.
- **Agent orchestration**: PASS. Codex executes locally with Spec Kit artifacts and Superpowers TDD/debug/verification discipline; Hermes owns independent review, PR, merge, and issue lifecycle.

## Project Structure

### Documentation (this feature)

```text
specs/978-daren-alarm-pulse/
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

**Structure Decision**: Keep route prose in the existing shared C# route data and extend the existing Daren showcase test suite. Do not add frontend, endpoint, state, reward, or documentation contract files outside the #978 Spec Kit artifacts unless verification exposes a real issue.

## TDD Strategy

1. Add `DarenTimedRhythm_ReadsAsAlarmPulsePageWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` before changing the scene prose.
2. The guard asserts:
   - title and shared route data parity;
   - action id, `RhythmPulse`, Speed characteristic, difficulty, label, routing, config, and score deltas;
   - substantial page length, sentence count, and Daren active POV;
   - grouped motif coverage for signal crystal/red corridor, pulse/pauses/rhythm/intervals, Daren body/breath/step/shadow control, staff/posoh/heavy-grate continuity, silence/noise/alarm/guards/trace stakes, and RhythmPulse lead-in;
   - absence of default player-facing technical terms.
3. Run the focused Daren test filter and record RED evidence against the existing compact synopsis.
4. Replace only the `timed_rhythm` narrative / `DarenShowcaseBeat.PlayerText` in `QteSceneService.Daren.cs`.
5. Run focused and affected verification to GREEN.

## Baseline Evidence Before Implementation

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed: 50 passed / 0 failed / 0 skipped / 50 total.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed: 319 passed / 0 failed / 0 skipped / 319 total.

## Implementation Evidence

- Spec Kit prerequisite check: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR=E:\Games\worktrees\boe-978-daren-alarm-pulse\specs\978-daren-alarm-pulse` with `contracts/` and `tasks.md`.
- RED focused Daren test: `DarenTimedRhythm_ReadsAsAlarmPulsePageWithoutMechanicDrift` failed against the original synopsis with `Daren timed_rhythm narrative should be a substantial alarm-pulse literary page, not a compact synopsis.` Command result: 50 passed / 1 failed / 0 skipped / 51 total.
- GREEN focused Daren test after prose rewrite: 51 passed / 0 failed / 0 skipped / 51 total.
- Affected C# slice after prose rewrite: 320 passed / 0 failed / 0 skipped / 320 total.
- Builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` both succeeded with 0 warnings / 0 errors.
- Committed-range whitespace check: `git diff --check origin/main...HEAD` returned exit 0 with no whitespace errors.
- Added-line static scan over non-Spec changed files for hardcoded secrets, shell invocation/injection, `eval`, unsafe deserialization, and SQL string-formatting patterns returned `NO_MATCHES`.
- Scope reconciliation: implementation replaced only the `timed_rhythm` shared narrative and added a focused C# guard; no QTE mechanics/check/config/routing/scoring/reward/profile/New Game grant, endpoint, runtime-state, frontend, GM-facing docs/examples, sibling scene, result/aftermath issue, or parent #955 lifecycle change was made.

## Review Requirements

Independent review must check:

- `timed_rhythm` literary quality against issue #978 and parent #955;
- Daren remains the active protagonist in a rhythm-stealth scene;
- the prose carries forward heavy-grate/staff-case/alarm pressure without adding a new social dialogue system;
- the scene naturally narrows into the existing "Двигаться между ударами кристалла" action;
- no route/action/check/config/routing/scoring/reward/frontend/runtime drift occurred;
- tests use grouped motif coverage rather than a weak one-token bucket;
- Spec Kit artifacts do not stale-reference #977/heavy-grate as the current source issue.

## Complexity Tracking

No constitution violations are planned.
