# Implementation Plan: Project methodology bridge

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1587
Spec: [spec.md](spec.md). Tasks: [tasks.md](tasks.md).
Date: 2026-11-03. Git branch: `process/1587-neutral-methodology-bridge`.
Base: `6ef42415ef2d16d4b54a62da380e87da71913ae2`.

## Summary and technical context

Copy the approved two-file Markdown/YAML distribution unchanged into `.agents/skills/spec-kit-superpowers-bridge/`. Tools: Windows PowerShell 7, Python skill validator, GitHub connector. No runtime dependencies or performance impact. Source: reviewed `workflow-system/skills/spec-kit-superpowers-bridge` distribution.

## Constitution Check

Pass before and after design: tracked issue and multi-session Spec Kit record exist; player/GM/runtime/state/save contracts remain unchanged. No production code is written, so runtime TDD and XML documentation builds are inapplicable. No new tests mirror prose. Checks cover validator, two hashes, exact diff, independent review and remote recovery.

Category selection: none. Catalog responsibilities cover game/runtime/frontend/test infrastructure and existing documentation guards, not new agent skill distribution. No game consumer or GM documentation root changes. Preserve the unrelated `tests/selection.json`; no C#/frontend run is justified. Use a docs-only `[skip ci]` checkpoint to avoid executing that unrelated selection; independent review must assess this rationale. CI configuration/protections remain unchanged.

Current direct owner policy supersedes the older repository Astra XHigh review paragraph: separate routine reviewer `gpt-6.1-sol`/`xhigh`, dispatched by parent. Required GitHub code-owner review remains a separate pending gate.

## Execution and dependencies

1. Record approved requirements, run native setup-plan/setup-tasks using explicit feature overrides, then read-only Spec Kit consistency analysis.
2. Copy exactly the two reviewed source files; run `python <skill-creator>/scripts/quick_validate.py .agents/skills/spec-kit-superpowers-bridge`; compare source/target SHA-256 for both.
3. Inspect explicit paths, publish WIP using GitHub Git data API based on verified base tree, non-force ref update with expected SHA, read back remote ref.
4. Recover changed files and required AGENTS/workflow/constitution/testing instructions at that exact SHA into a fresh empty directory; verify bytes/blob identities.
5. Parent obtains independent whole-block review and resolves findings before acceptance or PR/merge decisions.

## Phase artifacts and actual method use

[research.md](research.md), [data-model.md](data-model.md), [quickstart.md](quickstart.md). No external interface changes; contracts directory inapplicable.

Loaded by local-file fallback: reviewed bridge; speckit-plan/tasks/analyze; Superpowers writing-plans, executing-plans, test-driven-development, verification-before-completion. This bounded delegated contribution continues approved parent feature 002; it is not a new brainstorming/specify invocation. Native setup-plan succeeded. Optional commit/context hooks were inspected and deferred; no mandatory plan/tasks/analyze hook exists.

Preserve the unrelated persisted feature pin; set per-command `SPECIFY_FEATURE_DIRECTORY=specs/1587-neutral-methodology-bridge` and `SPECIFY_FEATURE=1587-neutral-methodology-bridge`. Script BRANCH reports that logical override; actual Git branch remains the process branch above. This plan is the durable execution ledger.

## Checkpoint — WIP, 2026-11-03

Completed: remote branch/base verification, approved scope, native setup-plan. Initial partial-clone sparse restore failed to load some blobs and was stopped; exact-SHA GitHub API reads supplied only required workflow files. Never stage the incomplete index; publish explicit new paths against the base tree.

At the initial checkpoint, native task setup/analysis and skill copy/checks were pending; evidence below supersedes that initial state. Current status: independently reviewed source block; required GitHub code-owner approval remains pending. No acceptance, game tests, full checkout or Kimi execution is claimed.


### Pre-publication evidence

- Native setup-plan.ps1 -Json and setup-tasks.ps1 -Json succeeded for the explicit feature overrides above. The newer analyze instruction requests unsupported -RequireSpec; that invocation failed before analysis. Inspected the actual script and ran check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks plus an explicit spec-file existence check: exit 0, correct feature and all three required files present. No script or governance changes made.
- Read-only speckit-analyze semantic pass before skill copying: 5 requirements, 7 tasks, 100% requirement coverage (FR-001 T003/T004; FR-002 T003/T007; FR-003 T001/T002; FR-004 T004/T007; FR-005 T005/T006). Success criteria map to T004/T007, T005/T007, T006/T007. Zero critical/high/medium findings, ambiguous requirements, duplicate requirements or unmapped tasks. Constitution pre/post gates pass. Next action was T003.
- Copied only the two approved source files after analysis. Python 3.14.7 skill-creator/scripts/quick_validate.py .agents/skills/spec-kit-superpowers-bridge: exit 0, 'Skill is valid!'. Source/target SHA-256 matched 2/2: SKILL.md 790475132AD615F9D130B39FFA67210C58A1D5DF8D7AC229EDE058744BB309E2; agents/openai.yaml 3031D1921F7C6D2EE615E4BCB00219996CC843C164E56B192E5614614F419C1C.
- Platform Windows; PowerShell 7.6.6. No .NET/Node version or runtime execution is needed/proved. Parent owns Kimi scenario evidence and independent review.
- Publishing this WIP with the explicit eight new files (two skill, six feature documents). Base tree bbe57d96e1cfa923d0701242b8610a9d15adb9d8 preserves all pre-existing paths. Pending: remote SHA/readback, clean-directory API recovery and independent review. No PR, merge, issue closure or acceptance claim.

### Remote publication and bounded recovery evidence

- WIP source commit: 58402b9d2bd6dd89fb95b56e1399bfb3a8d29404; tree ce85f2527c80b643be46a1a832a32273672c4fb6. GitHub update_ref used force=false and expected base 6ef42415ef2d16d4b54a62da380e87da71913ae2. Fresh remote ref read returned the exact WIP SHA.
- GitHub compare_commits(base, WIP SHA) returned ahead_by=1 and exactly eight added paths, zero deletions: both bridge files and this feature's spec, plan, tasks, research, data-model and quickstart. No pre-existing file changed.
- Fresh directory recovery downloaded those eight paths plus AGENTS.md, docs/development-workflow.md, docs/testing.md and .specify/memory/constitution.md from GitHub fetch_file(ref=58402b9d2bd6dd89fb95b56e1399bfb3a8d29404, encoding=base64). No old checkout objects or source copies were used. Recomputed Git blob SHA-1 over 'blob <length> NUL <bytes>' matched API blob SHA for 12/12 files; recovered bridge quick_validate.py exit 0.
- Skill blob identities: SKILL.md 8484226c005a8bd45d45fcb140235e715647aa76; agents/openai.yaml 2270b3155377c93625ec03af6b990c039c4138d6. These source files remain unchanged in this checkpoint update.
- Recovery destination was a newly created task scratch directory. An initial direct command for the long governance document exceeded Windows process-command length; the same fresh GitHub responses were decoded through a temporary manifest, then all 12 files were reverified. This is bounded API source recovery only: no full clone, clean Git checkout, game build or runtime test was performed or claimed.
- T005/T006 complete for the WIP source SHA above. This follow-up only records evidence and task state. T007 independent review remains pending; next step is the parent's separate whole-block reviewer. Required GitHub code-owner approval, any PR, merge and issue closure are still pending. No review/model success is inferred from this executor report.

### Independent review checkpoint — 2026-11-03

- Separate reviewer context: review_book_skill_copy, actual model gpt-6.1-sol with xhigh reasoning, dispatched by the parent. Reviewed exact base 6ef42415ef2d16d4b54a62da380e87da71913ae2 through head 2bff6c16ea200d22a2ce90f6ddb672540bbe8007, including the original issue, canonical artifacts, exact eight additions and verification evidence.
- Parent reported verdict: approved; Critical 0, Important 0, Minor 0. The reviewer confirmed preserved source contracts, 12/12 recovery/hash evidence and no applicable game-test category. No findings require correction. T007 is complete for that reviewed block.
- This final checkpoint changes only plan.md and tasks.md to record review and task state. Skill source and earlier validation/recovery remain unchanged; no additional runtime checks or full recovery repeated without a changed source or unresolved risk. The parent retains final acceptance responsibility.
- Required protected GitHub code-owner approval remains pending. No PR, merge, protection change or issue closure was performed. All seven tasks in this bounded repository contribution are complete; this is independent source review, not protected-branch integration approval.