# Implementation Plan: Daren Rune Memory Fail Literary Aftermath

**Branch**: `work/1008-daren-rune-fail` | **Date**: 2026-06-14 | **Spec**: `specs/1008-daren-rune-fail/spec.md`

**Input**: Feature specification from `specs/1008-daren-rune-fail/spec.md`; source GitHub issue [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008); parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955); source scene [#975](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/975); completed siblings [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006) and [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007).

## Summary

Rewrite only Daren QTE `rune_memory_action` fail result into a substantial Russian dark-fantasy dangerous-outcome aftermath insert while preserving the existing shared route action contract. Add a focused failing guard first so the old one-sentence result is rejected and the final result proves console/browser parity through shared route data.

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

**Source Issue(s)**: [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#975](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/975).

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

- **GitHub traceability**: PASS. Spec, plan, tasks, checklist, and contract reference #1008 and parent #955.
- **Spec Kit fit**: PASS. The issue changes player-facing console/browser UX copy in shared route data and needs durable handoff evidence.
- **Player-facing integrity**: PASS. The plan requires Russian in-world copy, no implementation terminology in default prose, and shared C# route data for console/browser parity.
- **Contract/state authority**: PASS. No canonical state, GM prompt, validation, pending/control, or runtime contract changes are planned; the plan explicitly prohibits mechanics and runtime drift.
- **Test-first path**: PASS. A focused `DarenQteShowcaseTests` guard must be added and observed failing before production prose changes.
- **Verification evidence**: PASS. Focused tests, affected slice, builds, Spec Kit prerequisite check, diff check, and static scan are listed.
- **Agent orchestration**: PASS. Codex executes locally with Spec Kit artifacts and Superpowers TDD/debug/verification discipline; Hermes owns independent review, PR, merge, and issue lifecycle.

## Project Structure

### Documentation (this feature)

```text
specs/1008-daren-rune-fail/
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

**Structure Decision**: Keep route result prose in the existing shared C# route data and extend the existing Daren showcase test suite. Do not add frontend, endpoint, state, reward, or documentation contract files outside the #1008 Spec Kit artifacts unless verification exposes a real issue.

## TDD Strategy

1. Add a focused test such as `DarenRuneMemoryFail_ReadsAsDangerousRuneAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` before changing the fail prose.
2. The guard asserts:
   - title and shared route data parity;
   - action id, label, `PatternMemory`, Perception characteristic, difficulty, config pattern/sequence shape, routing, success/partial/fail identity, and score deltas;
   - fail result substantial aftermath length, sentence count, and Daren active POV;
   - grouped motif coverage for runes/glass/ward flare, Daren failed memory/body reaction, concrete house memory/evidence, alarm/witness/pursuit pressure, and next-Renara continuity;
   - absence of default player-facing technical terms including `QTE` and score/debug framing;
   - unchanged success/partial strings unless tests prove a minimal connective need.
3. Run the focused Daren test filter and record RED evidence against the existing one-sentence fail result.
4. Replace only the `rune_memory` action fail text in `QteSceneService.Daren.cs`.
5. Run focused and affected verification to GREEN.

## Baseline Evidence Before Implementation

- Branch `work/1008-daren-rune-fail` started from `origin/main` at `05c0fe0`; source issue #1008 and parent #955 are the tracked tasks.
- Focused Daren baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed: 59 passed / 0 failed / 0 skipped / 59 total.
- Affected slice baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed: 328 passed / 0 failed / 0 skipped / 328 total.
- Spec Kit prerequisite check before implementation: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR=E:\\Games\\worktrees\\boe-1008-daren-rune-fail\\specs\\1008-daren-rune-fail` with `contracts/` and `tasks.md`.

## Review Requirements

Independent review must check:

- `rune_memory_action` fail literary quality against issue #1008 and parent #955;
- Daren remains the active protagonist in a dangerous failed rune/ward aftermath;
- success/partial siblings (#1006/#1007), route mechanics, and runtime/browser contracts remain unchanged;
- Spec Kit artifacts align with the implementation and verification evidence;
- local verification evidence is fresh and non-zero.

## Implementation Verification Evidence

- RED: focused Daren test filter failed before production prose change because the new #1008 guard rejected the old one-sentence fail result; 59 passed / 1 failed / 0 skipped / 60 total.
- GREEN: focused Daren test filter passed after replacing only the fail prose; 60 passed / 0 failed / 0 skipped / 60 total.
- Affected C# slice passed after implementation; 329 passed / 0 failed / 0 skipped / 329 total.
- Client build and test-project build both passed with 0 warnings / 0 errors.
- Source/test added-line static scan for hardcoded secrets, shell execution, eval, unsafe deserialization, and SQL formatting returned `NO_MATCHES`.
- Diff inspection confirmed no route mechanics, success/partial prose, frontend/browser, runtime state, endpoint, reward/profile/New Game grant, or GM-facing documentation/example changes.

## Phase 0 Self-Review

- Placeholder scan: no `TBD` or vague implementation placeholders remain; command evidence is concrete where already run and remaining lifecycle evidence is represented as explicit tasks.
- Scope check: one result surface, one test guard, one shared route prose edit.
- Ambiguity check: fail grade semantics and out-of-scope sibling boundaries are explicit.
