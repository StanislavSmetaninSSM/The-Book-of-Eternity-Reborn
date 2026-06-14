# Implementation Plan: Daren Silent Gallery Success Literary Aftermath

**Branch**: `work/997-daren-gallery-success` | **Date**: 2026-06-14 | **Spec**: `specs/997-daren-gallery-success/spec.md`

**Input**: Feature specification from `specs/997-daren-gallery-success/spec.md`; source GitHub issue [#997](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/997); parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955); source scene [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972); sibling follow-ups [#998](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/998)/[#999](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/999); completed downstream result trios [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000)/[#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001)/[#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003)/[#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004)/[#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), and [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)/[#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)/[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008).

## Summary

Rewrite only Daren QTE `stealth_crossing_action` success result into a substantial Russian dark-fantasy clean stealth aftermath insert while preserving the existing shared route action contract. Add a focused failing guard first so the old one-sentence success result is rejected and the final result proves console/browser parity through shared route data.

## Technical Context

**Language/Version**: C#/.NET 8 for shared route data and tests.

**Primary Dependencies**: Existing `QteSceneService` route model, xUnit tests, and local Spec Kit scripts.

**Storage**: N/A; no persistence or runtime state shape changes.

**Testing**: `dotnet test` focused Daren route tests and affected C# slice; `dotnet build` for client and tests.

**Target Platform**: Local console client and local browser client consuming shared C# Daren route data.

**Project Type**: Local game client with authored route content.

**Performance Goals**: N/A; copy-only result rewrite must not introduce runtime work.

**Constraints**: Preserve route mechanics, action ids, check type/config, routing, grade identities, score deltas, rewards, endpoints, runtime state, browser/console parity, partial/fail result text, downstream keykeeper/cabinet/rune result text, and frontend files.

**Scale/Scope**: One result surface, one focused test guard, one Spec Kit feature directory.

**Source Issue(s)**: [#997](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/997), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972).

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

- **GitHub traceability**: PASS. Spec, plan, tasks, checklist, and contract reference #997 and parent #955.
- **Spec Kit fit**: PASS. The issue changes player-facing console/browser UX copy in shared route data and needs durable handoff evidence.
- **Player-facing integrity**: PASS. The plan requires Russian in-world copy, no implementation terminology in default prose, and shared C# route data for console/browser parity.
- **Contract/state authority**: PASS. No canonical state, GM prompt, validation, pending/control, or runtime contract changes are planned; the plan explicitly prohibits mechanics and runtime drift.
- **Test-first path**: PASS. A focused `DarenQteShowcaseTests` guard must be added and observed failing before production prose changes.
- **Verification evidence**: PASS. Focused tests, affected slice, builds, Spec Kit prerequisite check, diff check, and static scan are listed.
- **Agent orchestration**: PASS. Codex executes locally with Spec Kit artifacts and Superpowers TDD/debug/verification discipline; Hermes owns independent review, PR, merge, and issue lifecycle.

## Project Structure

### Documentation (this feature)

```text
specs/997-daren-gallery-success/
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

**Structure Decision**: Keep route result prose in the existing shared C# route data and extend the existing Daren showcase test suite. Do not add frontend, endpoint, state, reward, or documentation contract files outside the #997 Spec Kit artifacts unless verification exposes a real issue.

## TDD Strategy

1. Add a focused test such as `DarenStealthCrossingSuccess_ReadsAsCleanGalleryAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` before changing the success prose.
2. The guard asserts:
   - title and shared route data parity;
   - action id, label, `StealthNoise`, Dexterity characteristic, base difficulty, StealthNoise config, routing, success/partial/fail identity, and score deltas;
   - success result substantial aftermath length, sentence count, and Daren active POV;
   - grouped motif coverage for listening gallery surfaces, floorboards/noise, Daren breath/body/boot control, absence of alarm/evidence, clean passage/momentum, and next keykeeper/service-door continuity;
   - absence of default player-facing technical terms including `QTE` and score/debug framing;
   - unchanged partial/fail and downstream keykeeper/cabinet/rune-memory strings unless tests prove a minimal connective need.
3. Run the focused Daren test filter and record RED evidence against the existing one-sentence success result.
4. Replace only the `stealth_crossing` action success text in `QteSceneService.Daren.cs`.
5. Run focused and affected verification to GREEN.

## Baseline Evidence Before Implementation

- Branch `work/997-daren-gallery-success` started from `origin/main` at `e7e2f4b`; source issue #997 and parent #955 are the tracked tasks.
- Focused Daren baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed: 66 passed / 0 failed / 0 skipped / 66 total.
- Affected slice baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed: 335 passed / 0 failed / 0 skipped / 335 total.
- Spec Kit prerequisite check resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-997-daren-gallery-success\\specs\\997-daren-gallery-success` with `contracts/` and `tasks.md` before Codex implementation.

## Implementation Evidence To Record During This Closure Unit

- RED focused Daren guard count and expected failure against the old `stealth_crossing_action` success string.
- Rewritten success aftermath character count, sentence count, `Дарен` mentions, and word count.
- GREEN focused Daren test count.
- Affected Daren/QTE/docs/browser C# slice count.
- Client build and test-project build results.
- Working-tree and post-commit code-focused added-line static scan results.
- `git diff --check origin/main...HEAD` result.

## Review Requirements

Independent review must check:

- `stealth_crossing_action` success literary quality against issue #997 and parent #955;
- Daren remains the active protagonist in a clean, reduced-risk silent-gallery aftermath;
- #998 partial, #999 fail, downstream keykeeper/cabinet/rune results #1000-#1008, route mechanics, and runtime/browser contracts remain unchanged;
- Spec Kit artifacts align with the implementation and verification evidence;
- local verification evidence is fresh and non-zero.

## Phase 0 Self-Review

- Placeholder scan: no unresolved implementation placeholder is used as acceptance; evidence rows name the exact values Codex/Hermes must record during the closure unit.
- Scope check: one result surface, one test guard, one shared route prose edit.
- Ambiguity check: success grade semantics and out-of-scope sibling/downstream boundaries are explicit.
