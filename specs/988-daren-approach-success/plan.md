# Implementation Plan: Daren Approach Manor Success Literary Aftermath

**Issue**: [#988](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/988)
**Branch**: `work/988-daren-approach-success`
**Feature directory**: `specs/988-daren-approach-success/`

## Summary

Rewrite exactly one shared Daren QTE result surface: `approach_manor_action` success text in `BookOfEternityClient/Services/QteSceneService.Daren.cs`. The outcome must become a substantial Russian clean stealth aftermath for the approach to the manor, while preserving all route mechanics, same-scene sibling results, and downstream result surfaces.

## Technical Approach

- Add a focused test to `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` before production prose changes.
- The test should fail against the current terse success text, then pass after rewriting the success prose.
- Keep the production change inside the existing Daren route data string for `approach_manor_action` success.
- Update only Spec Kit evidence/checklist rows owned by implementation after verification.
- Hermes owns independent review, PR, merge, issue evidence comment, label transition, and cleanup.

## Files

- Modify: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`
  - Add `DarenApproachManorSuccess_ReadsAsCleanStealthAftermathWithoutMechanicDrift` or equivalent focused guard.
  - Reuse existing Daren route/test helpers for chapter/action lookup, text metrics, grouped motif assertions, forbidden technical terms, sibling sentinels, and mechanics invariants.
- Modify: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
  - Replace only the first result string for `approach_manor_action` (`success`).
- Modify: `specs/988-daren-approach-success/tasks.md`
  - Record RED/GREEN counts, build counts, static scan, and implementation commit evidence.
- Modify: `specs/988-daren-approach-success/checklists/requirements.md`
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

- Preserve #989 partial and #990 fail text exactly unless a test proves a real accidental drift that must be reverted.
- Preserve #991-#1008 downstream result text.
- Preserve route order, action ids, labels, check types/config, routing, score deltas, reward tiers, profile/New Game behavior, browser/console shared authority, endpoints, runtime state, and frontend files.
- Do not close parent #955 from this issue.
