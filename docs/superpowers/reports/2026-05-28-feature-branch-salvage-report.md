# Feature Branch Salvage — Completion Report

**Date:** 2026-05-28  
**Plan:** `docs/superpowers/plans/2026-05-28-feature-branch-salvage.md`  
**Tracking issue:** [#754](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/754)

---

## Summary

The remote `Feature` branch contained Browser Client development work that couldn't be merged wholesale (broken `/prose` endpoint wiring, placeholder code, English debug text leaking to players). This salvage operation extracted all reusable work into 4 clean PRs built from `main`, verified each independently, then archived and deleted the donor branch.

---

## Pull Requests Created

| # | Branch | Title | Status |
|---|--------|-------|--------|
| [#774](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/774) | `salvage/browser-dev-workflow-design-tokens` | chore(browser): salvage dev workflow and design tokens from Feature | Open |
| [#775](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/775) | `salvage/browser-dark-fantasy-shell` | feat(browser): salvage dark fantasy visual shell from Feature | Open |
| [#776](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/776) | `salvage/browser-real-composer-flow` | feat(browser): implement real player-action endpoint for composer | Open |
| [#777](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/777) | `salvage/browser-result-surface-fix` | fix(browser): harden sanitizer patterns and localize game screen copy | Open |

---

## What Each PR Contains

### PR #774 — Dev Workflow and Design Tokens
- Updated `.gitignore` for Vite/frontend artifacts
- `vite.config.ts` — explicit outDir to prevent stale builds
- `tokens.css` — CSS custom property design tokens (dark fantasy palette)
- `base.css` — minor ember particle positioning fix

### PR #775 — Dark Fantasy Visual Shell
- `main-menu-bg.webp` — hand-painted background asset
- `components.css` — scroll-styled buttons, input fields, health bars
- `layout.css` — responsive grid with sidebar + game area
- `motion.css` — ember particle keyframes and pulse animations
- Updated `tokens.css` and `base.css` with full dark fantasy theme

### PR #776 — Real Composer/Player-Action Flow
- **New:** `BrowserPlayerActionService.cs` — validates player text, checks write coordinator lock state, atomically writes `input/pending_player_action.json`
- **New:** `POST /api/explorer/player-action` endpoint registered in `LocalWebUiHost.cs`
- **New:** `BrowserPlayerActionRequest/Result` contract types in frontend
- **New:** `submitPlayerAction` method in `BrowserApiClient`
- **Modified:** `submitComposer` in `App.tsx` calls real endpoint (was: placeholder notice)

### PR #777 — Result Sanitizer & Game Screen Localization
- 7 new Russian-language forbidden patterns in `playerFacingCommandResult.ts` (blocks JSON/file/protocol diagnostics from reaching players — issue #745)
- `BrowserGameScreenService.cs` — all turn state `playerGuidance` strings localized from English technical terms to Russian player-facing copy (issue #767)
- Updated test assertions to match new Russian text (same structural invariants)

---

## Verification Results

Each PR independently verified:

| Check | Result |
|-------|--------|
| `npm run verify` (typecheck + player-facing tests + production build) | ✅ Pass |
| `dotnet build` | ✅ Pass |
| Guard tests (72 tests: BrowserFrontendWorkspace + LocalWebUiHost + LocalWebUiBuiltFrontendSmoke) | ✅ 72/72 Pass |

---

## Donor Branch Disposition

- **Archive tag:** `archive/feature-f648101` → commit `f6481013dd7d638bbdd0e1e4e0c4d906718712a4`
- **Remote branch `Feature`:** Deleted from origin
- Full commit history preserved via the archive tag

---

## Rejected Hunks (Not Ported)

| File | Reason |
|------|--------|
| `App.tsx` full replacement | Fake-wired `/prose ${text}` command, removes error handling |
| `client.ts` — `submitCommand` with `/prose` | Broken endpoint (doesn't exist on server) |
| `index.html` full replacement | Removes `<noscript>` fallback and CSP meta tags |
| `package.json` devDependencies churn | Introduced Tailwind/PostCSS deps not used by current UI |

---

## Issues Referenced

- **#754** — Salvage tracking (strategy comment + retirement comment added)
- **#745** — Russian diagnostic text leaking to players (addressed in PR #777)
- **#767** — Game screen English tech terms (addressed in PR #777)

---

## Post-Merge Verification Gate

After all 4 PRs merge into main, run:

```bash
git checkout main && git pull origin main
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet build
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "BrowserFrontendWorkspaceTests|LocalWebUiHostTests|LocalWebUiBuiltFrontendSmokeTests"
```

All should pass with 72+ guard tests green.
