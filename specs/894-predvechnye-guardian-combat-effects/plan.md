# Implementation Plan: Predvechnye Guardian special-art combat niches (#894)

**Source issue:** https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/894
**Spec:** `specs/894-predvechnye-guardian-combat-effects/spec.md`
**Branch/worktree:** `codex/894-predvechnye-combat-effects` in `E:/Games/worktrees/boe-894-predvechnye-combat-effects`
**Constitution:** `.specify/memory/constitution.md`
**Dependencies:** #898, #897, and #895 are closed on `main`; #896 remains a non-closing follow-up.

## Technical Context

The touched product surface is the built-in system Guardian dossier library. Runtime code reads each `BookOfEternityClient/system_guardians/built_in/*/dossier.md` into `SystemGuardianLibraryService` prompt packages and attraction requests. The current #894 task should update Russian dossier text and source guards; it should not add a new runtime JSON field, change `specialArts[].combatEffect` validation, or modify browser/console gameplay logic.

Relevant existing files:

- `BookOfEternityClient/system_guardians/built_in/{azalia,brann,elyara,ilarion,lissara,lucian,myriel,seret,varak,veyra}/dossier.md` — the ten target dossier paragraphs.
- `OtherGuides/System_Guardian_Dossier_Standard.md` — authoring standard for future dossier edits.
- `BookOfEternityClient.Tests/SystemGuardianLibraryServiceTests.cs` — existing source guards for built-in dossier structure and prompt package preservation.
- `specs/897-special-art-combat-effect/contracts/special-art-combat-effect.md` — existing structured `combatEffect` semantics that #894 must use without reshaping.
- `OtherGuides/Saref_Guardian_Questlines/*.md` — consistency-only check; update only if the dossier wording contradicts reward text.

## Architecture / Design

1. Add a focused source-guard test to `SystemGuardianLibraryServiceTests` that enumerates the ten target built-in dossier files and asserts every listed special art has a nearby `Боевой эффект:` clause.
2. Keep the production implementation as data/documentation updates: insert a short but explicit combat-effect clause into each `Особое духовное искусство` paragraph. Each clause should cover ordinary combat niche, trigger/target, legal #897/#898 payoff/axis, and finite limit/counterplay.
3. Update the dossier authoring standard so future special spiritual art paragraphs include both the original artistic/GM note layer and the ordinary-combat `combatEffect` authoring layer.
4. Keep #896 separate. Do not add broad worked examples unless a minimal test fixture requires it; this #894 branch should prepare content that #896 can later use.

## Spec Kit Applicability

Spec Kit is required because #894 is Saref/afterlife GM-facing content spanning ten prompt/dossier files plus source guards. The feature directory is `specs/894-predvechnye-guardian-combat-effects/`. Source links appear in `spec.md`, this `plan.md`, `tasks.md`, and `contracts/predvechnye-special-art-combat-effects.md`.

## TDD / Debugging Method

- Use test-first discipline for the source guard: add the guard before editing dossiers, run the focused test, and confirm it fails because `Боевой эффект:` clauses are missing.
- Add dossier text only after the RED guard is observed.
- If the focused test or docs gate fails unexpectedly, follow systematic debugging: read the full failure, trace the exact missing/unsafe text, compare to a working dossier clause, then make one focused fix.
- Preserve user/unrelated changes and avoid generated artifacts in the commit.

## Verification Plan

Baseline already run from the issue worktree before implementation:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SystemGuardianLibraryServiceTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"
```

Observed baseline: 115 passed, 0 failed, 0 skipped.

Required final gates:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~SystemGuardianLibraryServiceTests" --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"

dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal

git diff --check origin/main...HEAD
```

Added-line static scan must exclude `specs/**` and inspect any matches manually:

```bash
git diff origin/main...HEAD -- . ':(exclude)specs/**' \
  | grep '^+' \
  | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|(^|[^.])\bexec\(|\beval\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" \
  || echo NO_MATCHES
```

## Risks / Constraints

- The new clauses must not reveal unrevealed Saref/Wings secrets in ordinary player-facing dossier text.
- Do not weaken existing narrative effects; the user explicitly clarified they are intentionally required for Saref-specific progression/counters.
- Keep #896 out of PR lifecycle keywords. Use safe wording such as `Non-closing references: #896 remains the follow-up coverage issue.`
- If `OtherGuides/Saref_Guardian_Questlines/*.md` already stays consistent, leave it unchanged and report that no questline-doc change was needed.
