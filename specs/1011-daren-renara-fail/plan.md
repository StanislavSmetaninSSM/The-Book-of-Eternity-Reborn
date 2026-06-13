# Implementation Plan: Daren Renara Voice Fail Literary Aftermath

**Branch**: `work/1011-daren-renara-fail` | **Date**: 2026-06-13 | **Spec**: `specs/1011-daren-renara-fail/spec.md`

**Input**: Feature specification from `specs/1011-daren-renara-fail/spec.md`; source GitHub issue [#1011](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1011); parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955); source scene [#976](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/976); completed siblings [#1009](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1009) and [#1010](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1010).

## Summary

Rewrite only Daren QTE `ward_steward_parley_action` fail result into a substantial Russian dark-fantasy social/ward-alarm aftermath insert while preserving the existing shared route action contract and the already-landed success/partial sibling prose. Add a focused failing guard first so the old one-sentence fail result is rejected and the final result proves console/browser parity through shared route data.

## Technical Context

**Language/Version**: C#/.NET 8 for shared route data and tests.

**Primary Dependencies**: Existing `QteSceneService` route model, xUnit tests, and local Spec Kit scripts.

**Storage**: N/A; no persistence or runtime state shape changes.

**Testing**: `dotnet test` focused Daren route tests and affected C# slice; `dotnet build` for client and tests.

**Target Platform**: Local console client and local browser client consuming shared C# Daren route data.

**Project Type**: Local game client with authored route content.

**Performance Goals**: N/A; copy-only result rewrite must not introduce runtime work.

**Constraints**: Preserve route mechanics, action ids, check type/config, routing, grade identities, score deltas, rewards, endpoints, runtime state, browser/console parity, success/partial result text, and frontend files.

**Scale/Scope**: One result surface, one focused test guard, one Spec Kit feature directory.

**Source Issue(s)**: [#1011](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1011), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#976](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/976), completed siblings [#1009](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1009) and [#1010](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1010).

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

- **GitHub traceability**: PASS. Spec, plan, tasks, checklist, and contract reference #1011 and parent #955.
- **Spec Kit fit**: PASS. The issue changes player-facing console/browser UX copy in shared route data and needs durable handoff evidence.
- **Player-facing integrity**: PASS. The plan requires Russian in-world copy, no implementation terminology in default prose, and shared C# route data for console/browser parity.
- **Contract/state authority**: PASS. No canonical state, GM prompt, validation, pending/control, or runtime contract changes are planned; the plan explicitly prohibits mechanics and runtime drift.
- **Test-first path**: PASS. A focused `DarenQteShowcaseTests` guard must be added and observed failing before production prose changes.
- **Verification evidence**: PASS. Focused tests, affected slice, builds, Spec Kit prerequisite check, diff check, and static scan are listed.
- **Agent orchestration**: PASS. Codex executes locally with Spec Kit artifacts and Superpowers TDD/debug/verification discipline; Hermes owns independent review, PR, merge, and issue lifecycle.

## Project Structure

### Documentation (this feature)

```text
specs/1011-daren-renara-fail/
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

**Structure Decision**: Keep route result prose in the existing shared C# route data and extend the existing Daren showcase test suite. Do not add frontend, endpoint, state, reward, or documentation contract files outside the #1011 Spec Kit artifacts unless verification exposes a real issue.

## TDD Strategy

1. Add a focused test such as `DarenWardStewardParleyFail_ReadsAsDangerousSocialAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` before changing the fail prose.
2. The guard asserts:
   - title and shared route data parity;
   - action id, label, `PrecisionChoice`, Wisdom characteristic, difficulty, config choice/outcome mapping, routing, success/partial/fail identity, and score deltas;
   - fail result substantial aftermath length, sentence count, and Daren active POV;
   - grouped motif coverage for Renara/ward voice, failed challenge or wrong answer, awakened alarm/pursuit pressure, retained evidence/identity/witness pressure, and next-heavy-grate continuity;
   - absence of default player-facing technical terms including `QTE` and score/debug framing;
   - unchanged success/partial strings unless tests prove a minimal connective need.
3. Run the focused Daren test filter and record RED evidence against the existing one-sentence fail result.
4. Replace only the `ward_steward_parley` action fail text in `QteSceneService.Daren.cs`.
5. Run focused and affected verification to GREEN.

## Baseline Evidence Before Implementation

- Branch `work/1011-daren-renara-fail` started from `origin/main` at `514fc0f`; source issue #1011 and parent #955 are the tracked tasks.
- Spec Kit CLI version check before implementation reported 0.9.3.
- Focused and affected-slice baseline counts are recorded in `tasks.md` before Codex implementation.

## Review Requirements

Independent review must check:

- `ward_steward_parley_action` fail literary quality against issue #1011 and parent #955;
- Daren remains the active protagonist in a dangerous social/ward-alarm aftermath;
- Renara Wardova and the listening house become concrete threat/pursuit pressure without adding a new dialogue runtime;
- the prose shows failed challenge/wrong answer escalating alarm while preserving route continuation and without naming score, QTE, or implementation mechanics;
- the result naturally bridges from Renara's recognition toward the next heavy-grate beat;
- no route/action/check/config/routing/scoring/reward/frontend/runtime drift occurred;
- tests use grouped motif coverage rather than a weak one-token bucket;
- Spec Kit artifacts do not treat #1009/#1010 or parent #955 as current closure scope.

## Complexity Tracking

No constitution violations are planned.
