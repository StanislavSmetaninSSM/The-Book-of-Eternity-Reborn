# Implementation Plan: Browser Action Result Surfaces (#757)

**Source issue:** [#757](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/757)
**Spec:** `specs/757-browser-action-surfaces/spec.md`
**Constitution:** `.specify/memory/constitution.md`

## Technical Approach

Use the existing C# `ExplorerCommandResult` and browser command/prompt-session APIs as authority. The React frontend should only sanitize and present the returned result data. The narrow root problem to verify/fix is default player rendering for selected action/command results: safe blocks must survive and render as player-facing surfaces, while unsafe raw technical blocks remain hidden or sanitized unless advanced mode is explicitly enabled.

## Baseline Evidence

Collected on branch `fix/757-browser-action-surfaces` from `origin/main` (`74ed912`):

- `unset PYTHONHOME UV_INTERNAL__PYTHONHOME PYTHONPATH; specify version` works: Specify CLI `0.9.3`, default integration `codex`. The first attempt without unsetting inherited Python variables failed with `AssertionError: SRE module mismatch`; use the unset form in cron/Git-Bash contexts.
- `npm ci --prefix BookOfEternityClient.WebFrontend`: succeeded, 54 packages, 0 vulnerabilities.
- `npm run verify --prefix BookOfEternityClient.WebFrontend`: passed; typecheck passed; Vitest `2 files / 27 tests` passed; Vite build succeeded.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~CommandResult"`: passed `99/99`, `0 failed`, `0 skipped`.

## Implementation Evidence

Collected by Codex on 2026-06-06 for GitHub issue #757:

- Root cause / current behavior: the current React selected-action path already
  uses `sanitizeExplorerCommandResultForPlayer(result.data)` and renders
  `CommandResultView` through `BlockList`, so safe blocks are preserved in that
  primary path. The remaining closure risk was
  `sanitizePlayerDefaultCommandResult()` defaulting `preserveSafeBlocks` to
  `false`, preserving the reopened failure mode for any default player action
  path that used the shared sanitizer without an explicit override.
- RED: after adding the safe read-only action regression case to
  `BookOfEternityClient.WebFrontend/test/playerFacingCommandResult.test.ts`,
  `npm run test:player-facing --prefix BookOfEternityClient.WebFrontend` failed
  with `Expected default player command presentation to preserve safe read-only
  result blocks.`
- GREEN: changed the shared default to `preserveSafeBlocks: true` and made the
  new-chapter launcher suppression explicit with `preserveSafeBlocks: false`.
  The same focused command passed; Vitest reported `2` files and `27` tests
  passed.
- Full frontend verification:
  `npm run verify --prefix BookOfEternityClient.WebFrontend` passed. Typecheck
  passed; player-facing tests passed; Vitest reported `2` files / `27` tests;
  Vite build completed with `45` modules transformed.
- Focused .NET verification:
  `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~CommandResult"`
  passed `99` total, `99` passed, `0` failed, `0` skipped.
- `git diff --check` passed for the working diff. Git reported only expected
  CRLF normalization warnings for the edited frontend files.
- Added-line static scan over non-spec/non-TestResults changes found no
  hardcoded secret, shell execution, eval/new Function, unsafe deserialization,
  or SQL string-formatting hits.
- Visual smoke artifact: not generated or committed because this hardening
  changes sanitizer behavior only; there was no layout, modal, or styling change
  that required a visual smoke artifact.

## Files Expected to Change

Likely frontend presentation/test files:

- `BookOfEternityClient.WebFrontend/src/utils/playerCopy.ts` — default `ExplorerCommandResult` player sanitizer.
- `BookOfEternityClient.WebFrontend/src/playerFacingCommandResult.ts` — default command-result sanitizer used by launcher/new-chapter flows if relevant.
- `BookOfEternityClient.WebFrontend/src/components/CommandResultView.tsx` — selected action result surface and prompt rendering.
- `BookOfEternityClient.WebFrontend/src/components/CommandResult.tsx` — launcher/local action result rendering if needed.
- `BookOfEternityClient.WebFrontend/src/components/BlockRenderer.tsx` — shared block rendering if a player-facing empty/fallback state is needed.
- `BookOfEternityClient.WebFrontend/test/*.test.ts` — TDD regression tests/source guards for safe blocks and technical hiding.
- `BookOfEternityClient.Tests/*Browser*Tests.cs` — focused C# source guards only if frontend/source-guard coverage needs hardening.
- `TestResults/browser-smoke/*` — optional generated visual smoke artifact; do not commit unless project convention/issue acceptance requires it.
- `specs/757-browser-action-surfaces/{spec.md,plan.md,tasks.md}` — update with final evidence.

## Constraints

- Do not add gameplay logic in React.
- Do not change C# runtime contracts, validation/normalizer behavior, pending/control files, GM prompts, or afterlife/Mortal contracts unless a newly discovered contract gap is explicitly recorded in the spec first.
- Keep the current minimalist Browser Client direction: tabs + single command/composer flow + `/help` discovery.
- Default player UI must be Russian/player-facing and must not expose raw slash commands, API, DTO, endpoint, protocol, file paths, raw JSON, or debug wording.
- Advanced diagnostics may remain behind explicit advanced mode.

## Verification Plan

Run at minimum after implementation:

1. Focused new/updated frontend tests that first fail on current behavior and pass after the fix.
2. `npm run verify --prefix BookOfEternityClient.WebFrontend`.
3. `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~CommandResult"`.
4. `git diff --check origin/main...HEAD`.
5. Added-line static security scan excluding docs/spec text false positives.
6. Independent review before PR/merge.

## PR/Closure Notes

Hermes owns independent acceptance, PR creation/merge, and GitHub issue closure. Codex may commit a focused implementation commit with `[skip ci]`, but must not merge, close issues, or mark all Spec Kit tasks complete without evidence.
