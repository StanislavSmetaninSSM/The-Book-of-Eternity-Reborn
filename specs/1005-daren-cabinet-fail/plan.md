# Implementation Plan: Daren Cabinet Lock Fail Literary Aftermath

**Branch**: `work/1005-daren-cabinet-fail` | **Date**: 2026-06-14 | **Spec**: `specs/1005-daren-cabinet-fail/spec.md`

**Input**: Feature specification from `specs/1005-daren-cabinet-fail/spec.md`; source GitHub issue [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005); parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955); source scene [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974); completed siblings [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003) and [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004); completed downstream result trio [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)/[#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)/[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008).

## Summary

Rewrite only Daren QTE `lock_pick_action` fail result into a substantial Russian dark-fantasy dangerous-outcome aftermath insert while preserving the existing shared route action contract. Add a focused failing guard first so the old one-sentence fail result is rejected and the final result proves console/browser parity through shared route data.

## Technical Context

**Language/Version**: C#/.NET 8 for shared route data and tests.

**Primary Dependencies**: Existing `QteSceneService` route model, xUnit tests, and local Spec Kit scripts.

**Storage**: N/A; no persistence or runtime state shape changes.

**Testing**: `dotnet test` focused Daren route tests and affected C# slice; `dotnet build` for client and tests.

**Target Platform**: Local console client and local browser client consuming shared C# Daren route data.

**Project Type**: Local game client with authored route content.

**Performance Goals**: N/A; copy-only result rewrite must not introduce runtime work.

**Constraints**: Preserve route mechanics, action ids, check type/config, routing, grade identities, score deltas, rewards, endpoints, runtime state, browser/console parity, success/partial result text, downstream rune-memory result text, and frontend files.

**Scale/Scope**: One result surface, one focused test guard, one Spec Kit feature directory.

**Source Issue(s)**: [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974).

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

- **GitHub traceability**: PASS. Spec, plan, tasks, checklist, and contract reference #1005 and parent #955.
- **Spec Kit fit**: PASS. The issue changes player-facing console/browser UX copy in shared route data and needs durable handoff evidence.
- **Player-facing integrity**: PASS. The plan requires Russian in-world copy, no implementation terminology in default prose, and shared C# route data for console/browser parity.
- **Contract/state authority**: PASS. No canonical state, GM prompt, validation, pending/control, or runtime contract changes are planned; the plan explicitly prohibits mechanics and runtime drift.
- **Test-first path**: PASS. A focused `DarenQteShowcaseTests` guard must be added and observed failing before production prose changes.
- **Verification evidence**: PASS. Focused tests, affected slice, builds, Spec Kit prerequisite check, diff check, and static scan are listed.
- **Agent orchestration**: PASS. Codex executes locally with Spec Kit artifacts and Superpowers TDD/debug/verification discipline; Hermes owns independent review, PR, merge, and issue lifecycle.

## Project Structure

### Documentation (this feature)

```text
specs/1005-daren-cabinet-fail/
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

**Structure Decision**: Keep route result prose in the existing shared C# route data and extend the existing Daren showcase test suite. Do not add frontend, endpoint, state, reward, or documentation contract files outside the #1005 Spec Kit artifacts unless verification exposes a real issue.

## TDD Strategy

1. Add a focused test such as `DarenLockPickFail_ReadsAsDangerousCabinetAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` before changing the fail prose.
2. The guard asserts:
   - title and shared route data parity;
   - action id, label, `LockPinSet`, Dexterity characteristic, difficulty, config pin windows/timer/durability/grade thresholds, routing, success/partial/fail identity, and score deltas;
   - fail result substantial aftermath length, sentence count, and Daren active POV;
   - grouped motif coverage for pins/pick failure, body/breath control, loud lock/noise consequence, trace/evidence/pursuit pressure, guard/house awareness, and next-rune/futlar continuity;
   - absence of default player-facing technical terms including `QTE` and score/debug framing;
   - unchanged success/partial and downstream rune-memory strings unless tests prove a minimal connective need.
3. Run the focused Daren test filter and record RED evidence against the existing one-sentence fail result.
4. Replace only the `lock_pick` action fail text in `QteSceneService.Daren.cs`.
5. Run focused and affected verification to GREEN.

## Baseline Evidence Before Implementation

- Branch `work/1005-daren-cabinet-fail` started from `origin/main` at `bb33bf7`; source issue #1005 and parent #955 are the tracked tasks.
- Focused Daren baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed: 62 passed / 0 failed / 0 skipped / 62 total.
- Affected slice baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed: 331 passed / 0 failed / 0 skipped / 331 total.
- Spec Kit prerequisite check resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1005-daren-cabinet-fail\\specs\\1005-daren-cabinet-fail` with `contracts/` and `tasks.md` before Codex implementation.

## Implementation Evidence To Record During This Closure Unit

- RED focused Daren guard count and expected failure against the old `lock_pick_action` fail string.
- Rewritten fail aftermath character count, sentence count, `Дарен` mentions, and word count.
- GREEN focused Daren test count.
- Affected Daren/QTE/docs/browser C# slice count.
- Client build and test-project build results.
- Working-tree and post-commit code-focused added-line static scan results.
- `git diff --check origin/main...HEAD` result.

## Review Requirements

Independent review must check:

- `lock_pick_action` fail literary quality against issue #1005 and parent #955;
- Daren remains the active protagonist in a dangerous, evidence/escalation cabinet-lock aftermath;
- #1003 success, #1004 partial, downstream rune-memory results #1006/#1007/#1008, route mechanics, and runtime/browser contracts remain unchanged;
- Spec Kit artifacts align with the implementation and verification evidence;
- local verification evidence is fresh and non-zero.

## Phase 0 Self-Review

- Placeholder scan: no unresolved implementation placeholder is used as acceptance; evidence rows name the exact values Codex/Hermes must record during the closure unit.
- Scope check: one result surface, one test guard, one shared route prose edit.
- Ambiguity check: fail grade semantics and out-of-scope sibling boundaries are explicit.
