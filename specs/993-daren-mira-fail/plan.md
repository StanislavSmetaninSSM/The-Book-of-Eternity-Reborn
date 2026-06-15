# Implementation Plan: Daren Mira Whisper Fail Literary Aftermath

**Issue**: [#993](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/993)
**Branch**: `work/993-daren-mira-fail`
**Feature directory**: `specs/993-daren-mira-fail/`

## Summary

Rewrite exactly one shared Daren QTE result surface: `informant_parley_action` fail text in `BookOfEternityClient/Services/QteSceneService.Daren.cs`. The outcome must become a substantial Russian dangerous social aftermath for Mira's failed informant exchange, while preserving all route mechanics and sibling result surfaces.

## Technical Approach

- Add a focused test to `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` before production prose changes.
- The test should fail against the current terse fail text, then pass after rewriting the fail prose.
- Keep the production change inside the existing Daren route data string for `informant_parley_action` fail.
- Update only Spec Kit evidence/checklist rows owned by implementation after verification.
- Hermes owns independent review, PR, merge, issue evidence comment, label transition, and cleanup.

## Files

- Modify: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
  - Add `DarenInformantParleyFail_ReadsAsDangerousMiraWitnessAftermathWithoutMechanicDrift` or equivalent focused guard.
  - Reuse existing Daren route/test helpers for chapter/action lookup, text metrics, grouped motif assertions, forbidden technical terms, sibling sentinels, and mechanics invariants.
- Modify: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
  - Replace only the third result string for `informant_parley_action` (`fail`).
- Modify: `specs/993-daren-mira-fail/tasks.md`
  - Record RED/GREEN counts, build counts, static scan, and implementation commit evidence.
- Modify: `specs/993-daren-mira-fail/checklists/requirements.md`
  - Mark acceptance/verification rows only after evidence exists.
- Do not modify frontend/browser files, GM docs/examples, runtime state, endpoints, reward/profile services, or other Daren result surfaces.

## Data / Contract Constraints

See `contracts/daren-result-aftermath.md`. This is a shared C# authored-content change, not a new runtime contract.

## Test Plan

Required before Codex final response:

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

Also run an added-line static scan over the code/test/spec diff for hardcoded secrets, shell execution, `eval`/standalone `exec`, unsafe deserialization, and SQL string formatting. If the shell scan is noisy, use a tiny Python script after unsetting `PYTHONHOME`, `UV_INTERNAL__PYTHONHOME`, and `PYTHONPATH`.

## Scope Boundaries

- Preserve #991 success and #992 partial text exactly unless a test proves a real accidental drift that must be reverted.
- Preserve #988/#989/#990 previous result text and #994-#1008 downstream result text.
- Preserve route order, action ids, labels, check types/config, routing, score deltas, reward tiers, profile/New Game behavior, browser/console shared authority, endpoints, runtime state, and frontend files.
- Do not close parent #955 from this issue.
