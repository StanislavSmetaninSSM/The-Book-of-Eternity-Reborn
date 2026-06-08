# Tasks: Predvechnye Guardian special-art combat niches (#894)

**Source issue:** https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/894
**Spec:** `specs/894-predvechnye-guardian-combat-effects/spec.md`
**Plan:** `specs/894-predvechnye-guardian-combat-effects/plan.md`
**Contract/design map:** `specs/894-predvechnye-guardian-combat-effects/contracts/predvechnye-special-art-combat-effects.md`

## T001 — Preflight / issue selection / baseline

- [x] Confirm #898, #897, and #895 are closed on `main` before starting #894.
- [x] Inspect #894 body/comments and confirm #896 remains a non-closing follow-up.
- [x] Create isolated ASCII worktree `E:/Games/worktrees/boe-894-predvechnye-combat-effects` on branch `codex/894-predvechnye-combat-effects` from `origin/main`.
- [x] Run baseline focused gate before dossier implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SystemGuardianLibraryServiceTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` -> 115 passed, 0 failed, 0 skipped.

## T002 — Source guard RED for dossier combat-effect clauses

- [x] RED: add a focused `SystemGuardianLibraryServiceTests` source guard that enumerates the ten target `dossier.md` files and asserts each required art paragraph contains an explicit `Боевой эффект:` clause.
- [x] RED: the guard must verify required content dimensions for every clause: ordinary combat niche, trigger/target, legal #897/#898 payoff/axis vocabulary, and finite limit/counterplay.
- [x] RED: the guard must preserve the original art names and ensure the existing `Особое духовное искусство` / `Художественный эффект` layer is still present.
- [x] Run the focused new test before dossier edits and record the observed failure count/reason.

## T003 — Update ten built-in Guardian dossiers

- [x] Add `Боевой эффект:` text to `BookOfEternityClient/system_guardians/built_in/azalia/dossier.md` for `Пламя Избранной Клятвы` using the voluntary allegiance / false devotion niche.
- [x] Add `Боевой эффект:` text to `BookOfEternityClient/system_guardians/built_in/brann/dossier.md` for `Клеймо Честной Трещины` using the structural defect / defense-order weakening niche.
- [x] Add `Боевой эффект:` text to `BookOfEternityClient/system_guardians/built_in/elyara/dossier.md` for `Милость Незаживающей Раны` using the delayed severe consequence / real remaining price niche.
- [x] Add `Боевой эффект:` text to `BookOfEternityClient/system_guardians/built_in/ilarion/dossier.md` for `Якорь Невытравленного Имени` using the anchored truth/name/memory against erasure niche.
- [x] Add `Боевой эффект:` text to `BookOfEternityClient/system_guardians/built_in/lissara/dossier.md` for `След, Которого Не Было` using the false trace / enemy tempo misdirection niche.
- [x] Add `Боевой эффект:` text to `BookOfEternityClient/system_guardians/built_in/lucian/dossier.md` for `Лунный Разрез Клятвы` using the oath/seal/false-light layer reveal niche.
- [x] Add `Боевой эффект:` text to `BookOfEternityClient/system_guardians/built_in/myriel/dossier.md` for `Пепельная Формула Чужого Мира` using the alien-law incompatibility niche.
- [x] Add `Боевой эффект:` text to `BookOfEternityClient/system_guardians/built_in/seret/dossier.md` for `Разомкнутый Договор` using the legal exit clause / hidden condition becomes contestable niche.
- [x] Add `Боевой эффект:` text to `BookOfEternityClient/system_guardians/built_in/varak/dossier.md` for `Трещина в Строю` using the formation discipline / returned agency to one node niche.
- [x] Add `Боевой эффект:` text to `BookOfEternityClient/system_guardians/built_in/veyra/dossier.md` for `Маска Среди Крыльев` using the temporary role/access vector / contradiction risk niche.
- [x] Ensure every clause preserves existing narrative/Saref-safe wording and does not expose raw JSON, debug vocabulary, or premature Wings/Saref spoilers.

## T004 — Update authoring standard and consistency notes

- [x] Update `OtherGuides/System_Guardian_Dossier_Standard.md` to require a special-art combat-effect clause with combat niche, trigger/target, legal payoff/axis, limit/counterplay, and GM audit note guidance.
- [x] Inspect `OtherGuides/Saref_Guardian_Questlines/*.md` for direct contradiction with new dossier wording; update only if needed, otherwise leave unchanged and record that no questline-doc change was necessary.
- [x] If source guards need a standard-doc assertion, add it in the focused test rather than broadening #894 into #896.

## T005 — GREEN verification and Spec Kit reconciliation

- [x] Run focused `SystemGuardianLibraryServiceTests` and record exact counts.
- [x] Run `AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests` and record exact counts.
- [x] Run `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal` and record warnings/errors.
- [x] Run `git diff --check origin/main...HEAD` and record result.
- [x] Run added-line static scan excluding `specs/**`; inspect/report any matches.
- [x] Update this tasks file with RED/GREEN/final verification evidence before PR if the implementation changes expected commands or evidence.
- [x] Check that no run artifacts, `bin/`, `obj/`, `node_modules/`, `.hermes/`, `.review-*`, or test output artifacts are staged.

## T006 — Independent review / PR / merge / closure owned by Hermes

- [ ] Independent review approves final diff or all Critical/Important findings are fixed and re-reviewed.
- [ ] Create PR with local verification evidence. PR body may close #894 only; #896 must be mentioned only as a non-closing reference.
- [ ] Squash-merge after local gates and review approval.
- [ ] Verify #894 is `CLOSED` / `COMPLETED` only after merge to `main`.
- [ ] Post final Russian closure report with verification evidence, review summary, docs impact, Spec Kit reconciliation, and next target #896.

## Verification Evidence

- Baseline focused gate before implementation: 115 passed, 0 failed, 0 skipped.
- RED source-guard run before dossier edits: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~SystemGuardianLibraryServiceTests" --logger "console;verbosity=minimal"` -> 15 total, 14 passed, 1 failed, 0 skipped. Expected failure: new `BuiltInPermanentGuardianDossiers_DescribeDistinctSpecialArtCombatEffects` guard reported missing `Боевой эффект:` clauses/fragments in all ten target dossiers and missing standard-doc guidance.
- GREEN focused guard after dossier/doc edits: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~SystemGuardianLibraryServiceTests" --logger "console;verbosity=minimal"` -> 15 passed, 0 failed, 0 skipped.
- Questline consistency inspection: `OtherGuides/Saref_Guardian_Questlines/*.md` reward/result sections remain Saref-specific and do not contradict the new ordinary-combat dossier clauses; no questline files changed.
- Afterlife docs/examples gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` -> 101 passed, 0 failed, 0 skipped.
- Build gate: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal` -> build succeeded, 0 warnings, 0 errors.
- Diff hygiene gate after local commit: `git diff --check origin/main...HEAD` -> clean, no output.
- Added-line static scan after local commit, excluding `specs/**`: `git diff origin/main...HEAD -- . ':(exclude)specs/**' | <required added-line pattern scan>` -> `NO_MATCHES`.
- Artifact/status check after local commit: `git status --short` -> clean; no `bin/`, `obj/`, `.hermes/`, `.review-*`, `node_modules/`, or test output artifacts staged.
