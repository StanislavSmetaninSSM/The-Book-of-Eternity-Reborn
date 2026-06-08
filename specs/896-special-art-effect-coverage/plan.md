# Implementation Plan: Special-art combat-effect examples and regression coverage (#896)

**Source issue:** GitHub #896 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/896  
**Spec:** `specs/896-special-art-effect-coverage/spec.md`  
**Branch/worktree:** `codex/896-special-art-coverage` at `E:/Games/worktrees/boe-896-special-art-coverage`  
**Created:** 2026-06-08

## Summary

Issue #896 finalizes the afterlife special-art combat-effect documentation/testing cluster after #898, #897, #895, and #894 landed. The implementation should update worked afterlife examples and documentation/source guards so future edits prove `specialArts[].combatEffect` and `specialArtAudit.effectNote` are combat-actionable, not generic flavor.

## Technical Context

- Stack: .NET 8 C# tests, Spectre.Console client docs/examples, JSON-like worked examples under `Examples/`.
- Runtime contract authority already exists in #897/#898; this plan should avoid changing runtime schema unless tests reveal an existing doc/example parser needs a non-contract adjustment.
- GM-facing documentation is product behavior for this repository. Example/doc updates and source-guard tests must land together.
- GitHub Actions are not mandatory; local verification is the closure gate.

## File Map

Likely files to modify after inspection:

- `Examples/E_CLI_Afterlife_Turns.txt` — add/update the worked player-owned and non-player special-art effect examples.
- `Examples/example_validation_manifest.json` — add/update example-section coverage if the changed example requires manifest entries.
- `OtherGuides/Afterlife_Combat_Terminology_Glossary.md` — ensure `specialArts[].combatEffect` and `specialArtAudit.effectNote` guidance says examples must show combat-actionable effects.
- `OtherGuides/Afterlife_Contract_Matrix.md` — update only if the matrix currently owns example/coverage requirements for special-art audit output.
- `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs` — add focused source guards for #896 coverage requirements.
- `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs` — update only if example parsing/manifest validation needs to recognize the new section(s).
- `specs/896-special-art-effect-coverage/tasks.md` — record RED/GREEN/final verification evidence.

## Implementation Strategy

1. Read the current example and test organization before editing prose.
2. Write a RED coverage test/source guard first. The guard should fail because current examples do not yet contain the #896 special-art combat-effect scenario coverage.
3. Update examples and GM-facing docs to satisfy the guard.
4. Keep examples player-safe: avoid raw DTO/debug framing, generic passive `+X` stacking, Mortal HP/status wording, and premature Saref/Wings spoilers.
5. Run focused docs/example tests and the test-project build.
6. Update `tasks.md` with exact evidence before PR.

## Verification Plan

Baseline observed before Spec Kit artifact edits:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SystemGuardianLibraryServiceTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"
# Result: 116 passed, 0 failed, 0 skipped
```

Required final local gates:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests|FullyQualifiedName~SystemGuardianLibraryServiceTests" --logger "console;verbosity=minimal"

dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal

git diff --check origin/main...HEAD

git diff origin/main...HEAD -- . ':(exclude)specs/**' \
  | grep '^+' \
  | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|(^|[^.])\bexec\(|\beval\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" \
  || echo NO_MATCHES
```

## Spec Kit / Governance Notes

- Source issue #896 is linked in `spec.md`, `plan.md`, and `tasks.md`.
- This is a Spec Kit feature because it is afterlife/Saref GM-facing example and test coverage across multiple files.
- No runtime contract reshaping is planned. If implementation discovers that existing #897/#898 docs are wrong, update the Spec Kit artifacts before changing code.
- PR body should close #896 only. If related issues are mentioned, use a safe non-closing section without GitHub lifecycle keywords before issue numbers.
