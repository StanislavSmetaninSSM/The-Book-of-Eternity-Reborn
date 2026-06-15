# Implementation Plan: Daren Mira Whisper Partial Literary Aftermath

**Branch**: `work/992-daren-mira-partial` | **Date**: 2026-06-15 | **Spec**: `specs/992-daren-mira-partial/spec.md`

**Input**: Feature specification from `specs/992-daren-mira-partial/spec.md`; source GitHub issue [#992](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/992); parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955); source scene [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970); completed same-scene sibling [#991](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/991); open same-scene sibling [#993](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/993); previous-result follow-ups [#988](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/988)/[#989](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/989)/[#990](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/990); completed downstream result trios [#994](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/994)-[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008).

## Summary

Rewrite only Daren QTE `informant_parley_action` partial result into a substantial Russian dark-fantasy mixed social aftermath insert while preserving the existing shared route action contract. Add a focused failing guard first so the old one-sentence partial result is rejected and the final result proves console/browser parity through shared route data.

## Technical Context

**Language/Version**: C#/.NET 8 for shared route data and tests.

**Primary Dependencies**: Existing `QteSceneService` route model, xUnit tests, and local Spec Kit scripts.

**Storage**: N/A; no persistence or runtime state shape changes.

**Testing**: `dotnet test` focused Daren route tests and affected C# slice; `dotnet build` for client and tests.

**Target Platform**: Local console client and local browser client consuming shared C# Daren route data.

**Project Type**: Local game client with authored route content.

**Performance Goals**: N/A; copy-only result rewrite must not introduce runtime work.

**Constraints**: Preserve route mechanics, action ids, check type/config, choice ids/grades, routing, grade identities, score deltas, rewards, endpoints, runtime state, browser/console parity, #991 success result text, #993 fail result text, previous #988-#990 result text, downstream #994-#1008 result text, and frontend files.

**Scale/Scope**: One result surface, one focused test guard, one Spec Kit feature directory.

**Source Issue(s)**: [#992](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/992), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), same-scene siblings [#991](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/991) and [#993](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/993).

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

- **GitHub traceability**: PASS. Spec, plan, tasks, checklist, and contract reference #992 and parent #955.
- **Spec Kit fit**: PASS. The issue changes player-facing console/browser UX copy in shared route data and needs durable handoff evidence.
- **Player-facing integrity**: PASS. The plan requires Russian in-world copy, no implementation terminology in default prose, and shared C# route data for console/browser parity.
- **Contract/state authority**: PASS. No canonical state, GM prompt, validation, pending/control, or runtime contract changes are planned; the plan explicitly prohibits mechanics and runtime drift.
- **Test-first path**: PASS. A focused `DarenQteShowcaseTests` guard must be added and observed failing before production prose changes.
- **Verification evidence**: PASS. Focused tests, affected slice, builds, Spec Kit prerequisite check, diff check, and static scan are listed.
- **Agent orchestration**: PASS. Codex executes locally with Spec Kit artifacts and Superpowers TDD/debug/verification discipline; Hermes owns independent review, PR, merge, and issue lifecycle.

## Project Structure

### Documentation (this feature)

```text
specs/992-daren-mira-partial/
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

**Structure Decision**: Keep route result prose in the existing shared C# route data and extend the existing Daren showcase test suite. Do not add frontend, endpoint, state, reward, or documentation contract files outside the #992 Spec Kit artifacts unless verification exposes a real issue.

## TDD Strategy

1. Add a focused test such as `DarenInformantParleyPartial_ReadsAsMixedMiraTrustAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` before changing the partial prose.
2. The guard asserts:
   - title and shared route data parity;
   - action id, label, `PrecisionChoice`, Wisdom characteristic, base difficulty, correct partial choice id `pay_for_rumor`, choice labels/grades, routing, success/partial/fail identity, and score deltas;
   - partial result substantial aftermath length, sentence count, and Daren active POV;
   - grouped motif coverage for Mira/Night Thread presence, coin/debt/bargain, incomplete information, source-pressure/future consequence, wet-awning/social pressure atmosphere, Daren voice/body control, and next hook-line continuity;
   - absence of default player-facing technical terms including `QTE` and score/debug framing;
   - unchanged #991 success and #993 fail surfaces, unchanged previous #988-#990 approach surfaces, and unchanged downstream #994-#1008 result strings unless tests prove a minimal connective need.
3. Run the focused Daren test filter and record RED evidence against the existing one-sentence partial result.
4. Replace only the `informant_parley` action partial text in `QteSceneService.Daren.cs`.
5. Run focused and affected verification to GREEN.

## Baseline Evidence Before Implementation

- Branch `work/992-daren-mira-partial` started from `origin/main` at `995be5d`; source issue #992 and parent #955 are the tracked tasks.
- Focused Daren baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed: 73 passed / 0 failed / 0 skipped / 73 total.
- Affected slice baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed: 342 passed / 0 failed / 0 skipped / 342 total.
- Spec Kit prerequisite command: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolves `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-992-daren-mira-partial\\specs\\992-daren-mira-partial` with `contracts/` and `tasks.md` before Codex starts.

## Implementation Evidence To Record During This Closure Unit

- RED focused Daren guard count and expected failure against the old `informant_parley_action` partial string.
- Rewritten partial aftermath character count, sentence count, `Дарен` mentions, and word count.
- GREEN focused Daren test count.
- Affected Daren/QTE/docs/browser C# slice count.
- Client build and test-project build results.
- Working-tree and post-commit code-focused added-line static scan results.
- `git diff --check origin/main...HEAD` result.

## Review Requirements

Independent review must check:

- `informant_parley_action` partial literary quality against issue #992 and parent #955;
- Daren remains the active protagonist in a mixed Mira/informant aftermath where the bargain works but leaves debt, doubt, source pressure, incomplete information, delay, or future consequence visible;
- #991 success, #993 fail, previous #988-#990 approach results, downstream hook/gallery/keykeeper/cabinet/rune results #994-#1008, route mechanics, and runtime/browser contracts remain unchanged;
- Spec Kit artifacts align with the implementation and verification evidence;
- local verification evidence is fresh and non-zero.

## Phase 0 Self-Review

- Placeholder scan: no unresolved implementation placeholder is used as acceptance; setup rows name exact commands Hermes ran before Codex and exact evidence Codex/Hermes must record.
- Scope check: one result surface, one test guard, one shared route prose edit.
- Ambiguity check: partial grade semantics and out-of-scope sibling/downstream boundaries are explicit.
