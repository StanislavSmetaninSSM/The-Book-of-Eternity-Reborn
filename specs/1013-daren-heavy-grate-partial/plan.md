# Implementation Plan: Daren Heavy-Grate Partial Literary Aftermath

**Branch**: `work/1013-daren-heavy-grate-partial` | **Date**: 2026-06-12 | **Spec**: `specs/1013-daren-heavy-grate-partial/spec.md`

**Input**: Feature specification from `specs/1013-daren-heavy-grate-partial/spec.md`; source GitHub issue [#1013](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1013); parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955); source scene [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977); preceding result [#1012](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1012).

## Summary

Rewrite only Daren QTE `physical_pressure_action` partial result into a substantial Russian dark-fantasy aftermath insert that preserves the mixed-outcome grade: the staff/posoh is freed, but Daren pays with pain, delay, trace, doubt, noise, or later pursuit/evidence risk. Add a focused failing guard first so the old one-sentence result is rejected and the final result proves console/browser parity through shared route data.

## Technical Context

**Language/Version**: C#/.NET 8 for shared route data and tests.

**Primary Dependencies**: Existing `QteSceneService` route model, xUnit tests, and local Spec Kit scripts.

**Storage**: N/A; no persistence or runtime state shape changes.

**Testing**: `dotnet test` focused Daren route tests and affected C# slice; `dotnet build` for client and tests.

**Target Platform**: Local console client and local browser client consuming shared C# Daren route data.

**Project Type**: Local game client with authored route content.

**Performance Goals**: N/A; copy-only result rewrite must not introduce runtime work.

**Constraints**: Preserve route mechanics, action ids, check type/config, routing, grade identities, score deltas, rewards, endpoints, runtime state, browser/console parity, success/fail result text, and frontend files.

**Scale/Scope**: One result surface, one focused test guard, one Spec Kit feature directory.

**Source Issue(s)**: [#1013](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1013), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977), context [#1012](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1012).

**Contract Scope**: Player-facing shared route result prose for console/browser; no GM-facing or runtime-state contract change.

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

- **GitHub traceability**: PASS. Spec, plan, tasks, checklist, and contract reference #1013 and parent #955.
- **Spec Kit fit**: PASS. The issue changes player-facing console/browser UX copy in shared route data and needs durable handoff evidence.
- **Player-facing integrity**: PASS. The plan requires Russian in-world copy, no implementation terminology in default prose, and shared C# route data for console/browser parity.
- **Contract/state authority**: PASS. No canonical state, GM prompt, validation, pending/control, or runtime contract changes are planned; the plan explicitly prohibits mechanics and runtime drift.
- **Test-first path**: PASS. A focused `DarenQteShowcaseTests` guard must be added and observed failing before production prose changes.
- **Verification evidence**: PASS. Focused tests, affected slice, builds, Spec Kit prerequisite check, diff check, and static scan are listed.
- **Agent orchestration**: PASS. Codex executes locally with Spec Kit artifacts and Superpowers TDD/debug/verification discipline; Hermes owns independent review, PR, merge, and issue lifecycle.

## Project Structure

### Documentation (this feature)

```text
specs/1013-daren-heavy-grate-partial/
├── spec.md
├── plan.md
├── contracts/
│   └── daren-result-aftermath.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Services/QteSceneService.Daren.cs
BookOfEternityClient.Tests/DarenQteShowcaseTests.cs
```

**Structure Decision**: Keep route result prose in the existing shared C# route data and extend the existing Daren showcase test suite. Do not add frontend, endpoint, state, reward, or documentation contract files outside the #1013 Spec Kit artifacts unless verification exposes a real issue.

## TDD Strategy

1. Add a focused test such as `DarenPhysicalPressurePartial_ReadsAsCostlyAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` before changing the partial prose.
2. The guard asserts:
   - title and shared route data parity;
   - action id, label, `MashInput`, Strength characteristic, difficulty, config, routing, success/partial/fail identity, and score deltas;
   - partial result substantial aftermath length, sentence count, and Daren active POV;
   - grouped motif coverage for mixed heavy-grate resolution, Daren body/breath/control, staff-case/niche extraction, cost/trace/doubt/pursuit stakes, and next-corridor continuity;
   - absence of default player-facing technical terms including `QTE` and score/debug framing;
   - unchanged success/fail strings unless tests prove a minimal connective need.
3. Run the focused Daren test filter and record RED evidence against the existing one-sentence partial result.
4. Replace only the `physical_pressure` action partial text in `QteSceneService.Daren.cs`.
5. Run focused and affected verification to GREEN.

## Baseline Evidence Before Implementation

- Branch `work/1013-daren-heavy-grate-partial` started from `origin/main` at `30b6622`; source issue #1013 and parent #955 are the tracked tasks.
- Spec Kit prerequisite check before implementation: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR=E:\Games\worktrees\boe-1013-daren-heavy-grate-partial\specs\1013-daren-heavy-grate-partial` with `contracts/` and `tasks.md`.
- `specify version` reported CLI 0.9.3; `specify integration list` reported Codex CLI installed/default.
- Focused Daren baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed: 52 passed / 0 failed / 0 skipped / 52 total.
- Affected slice baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed: 321 passed / 0 failed / 0 skipped / 321 total.

## Review Requirements

Independent review must check:

- `physical_pressure_action` partial literary quality against issue #1013 and parent #955;
- Daren remains the active protagonist in a mixed physical-action aftermath;
- the prose clearly communicates success-with-cost through wound, delay, trace, doubt, noise, or pursuit/evidence risk without naming score, QTE, or implementation mechanics;
- the result naturally bridges from the struck grate/staff case toward the next alarm-pulse corridor;
- no route/action/check/config/routing/scoring/reward/frontend/runtime drift occurred;
- tests use grouped motif coverage rather than a weak one-token bucket;
- Spec Kit artifacts do not treat #1012/#1014 or parent #955 as current closure scope.

## Complexity Tracking

No constitution violations are planned.
