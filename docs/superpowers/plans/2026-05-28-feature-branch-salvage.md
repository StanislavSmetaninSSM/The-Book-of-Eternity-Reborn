# Feature Branch Salvage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Safely salvage the useful Browser Client work from remote `Feature` without merging its failing/fake-wired state into `main`, then delete/retire the unsafe branch after the salvage PRs land.

**Architecture:** Treat `Feature` as an untrusted donor branch, not as a PR candidate. Create new clean branches from `origin/main`, port only reviewed hunks by issue/slice, add or repair tests first, and merge each slice through a normal PR with local verification. Keep C# as the gameplay/application authority; React/Vite is presentation and typed endpoint plumbing only.

**Tech Stack:** Git/GitHub CLI, .NET 8, xUnit, Vite + React + TypeScript, Browser Client local web host.

---

## Current evidence

- `origin/main`: `e98768d773984ea0a2673d2ffed95435b55b3409`
- remote `Feature` audit ref: `f6481013dd7d638bbdd0e1e4e0c4d906718712a4`
- no PR exists for head `Feature`.
- `Feature` diff relative to `main`: 15 files, `+2075 / -615`.
- Focused Browser .NET guard tests failed during audit; `npm run verify` alone was not sufficient.
- Known functional blockers:
  - prose composer routes to invented `/prose` command instead of a real backend player-turn path;
  - read-only action results can render as empty `Выполнено` because safe blocks are dropped;
  - some Browser guard tests are either broken or intentionally stale and must be reconciled deliberately.

## Files involved in the donor branch

Donor branch changed:

- `BookOfEternityClient.WebFrontend/src/App.tsx` — large UI rewrite; do not wholesale merge.
- `BookOfEternityClient.WebFrontend/src/styles/base.css` — visual tokens/styles; possible salvage.
- `BookOfEternityClient.WebFrontend/src/styles/components.css` — large visual/layout changes; possible salvage after visual QA.
- `BookOfEternityClient.WebFrontend/src/styles/layout.css` — layout polish; possible salvage.
- `BookOfEternityClient.WebFrontend/src/styles/motion.css` — animation polish; optional/non-blocker.
- `BookOfEternityClient.WebFrontend/src/styles/tokens.css` — dark-fantasy design tokens; likely salvageable.
- `BookOfEternityClient.WebFrontend/public/main-menu-bg.webp` — background art; verify provenance and pixels before merge.
- `BookOfEternityClient.WebFrontend/src/playerFacingCommandResult.ts` — result sanitizer; must be fixed/tested before merge.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json` — fixture changes; accept only if aligned with C# contract.
- `BookOfEternityClient.WebFrontend/vite.config.ts` — dev proxy/workflow change; likely salvageable if tested.
- `BookOfEternityClient/WebUi/BrowserGameScreenService.cs` — C# game-screen metadata/service changes; must be reviewed against Browser parity and no gameplay logic drift.
- `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs` — guard tests changed; do not weaken tests just to pass.
- `BookOfEternityClient.Tests/LocalWebUiHostTests.cs` — host tests changed; review expected behavior.
- `.gitignore` — likely harmless but verify exact entry.
- `docs/superpowers/specs/2026-05-27-browser-client-ux-redesign.md` — planning/spec artifact; keep only if useful as documentation.

## Branch policy

Do not open a PR from `Feature` directly.

Use this pattern instead:

```bash
unset PYTHONHOME UV_INTERNAL__PYTHONHOME PYTHONPATH
cd 'E:/Games/The Book of Eternity Reborn'
git fetch origin main Feature --prune || true
git fetch origin Feature:refs/heads/audit/remote-Feature --force
git checkout main
git pull origin main
```

Before deleting the remote branch, preserve an audit handle:

```bash
git tag archive/feature-f648101 f6481013dd7d638bbdd0e1e4e0c4d906718712a4
git push origin archive/feature-f648101
```

Only delete `Feature` after all accepted salvage PRs are merged and the archive tag exists:

```bash
git push origin --delete Feature
```

---

### Task 1: Create a salvage tracking issue / update parent issue

**Files:**
- No repository files changed.
- GitHub issue: create one umbrella issue if none exists, or use #754 as the parent if it remains the Browser UX reset umbrella.

- [ ] **Step 1: Confirm current issue state**

Run:

```bash
gh issue view 754 --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --json number,title,state,url
```

Expected: issue exists and is open.

- [ ] **Step 2: Add a comment declaring the salvage strategy**

Comment body:

```markdown
Salvage strategy: `Feature` will not be merged directly. We will create clean PRs from `main`, port only reviewed hunks by slice, run local Browser verification, and delete/retire `Feature` only after accepted salvage PRs land and an archive tag exists.
```

Run:

```bash
gh issue comment 754 --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --body-file /path/to/comment.md
```

- [ ] **Step 3: Commit**

No commit for this task.

---

### Task 2: Build a hunk inventory from `Feature`

**Files:**
- Create: `.hermes/feature-salvage/inventory.md` (local scratch only; do not commit unless explicitly useful).
- Read donor files listed above.

- [ ] **Step 1: Generate file-level diff inventory**

Run:

```bash
git diff --name-status origin/main...refs/heads/audit/remote-Feature > .hermes/feature-salvage/changed-files.txt
git diff --stat origin/main...refs/heads/audit/remote-Feature > .hermes/feature-salvage/diff-stat.txt
```

Expected: same 15 changed files as the audit.

- [ ] **Step 2: Split hunks into categories**

Write `.hermes/feature-salvage/inventory.md` with these categories:

```markdown
# Feature salvage inventory

## Safe or likely safe after local tests
- Vite dev proxy/workflow hunks from `BookOfEternityClient.WebFrontend/vite.config.ts`
- `.gitignore` entry if it only ignores local/generated artifacts
- design-token CSS from `tokens.css` if no player-flow behavior depends on it

## Salvage only after repair
- composer/action shell from `App.tsx` after replacing `/prose` with real backend flow
- result surface code after preserving safe result blocks
- `BrowserGameScreenService.cs` action metadata after parity tests prove no contract drift

## Do not salvage as-is
- any code path that executes invented `/prose`
- any test change that merely weakens player-facing guard tests
- any UI copy that exposes browser/API/file/DTO/debug wording in default player mode

## Separate task, not part of Browser salvage
- Windows CNG / pending snapshot authority work: #773
```

- [ ] **Step 3: No commit**

This is audit scratch unless the user asks to keep it in repo.

---

### Task 3: Salvage PR A — low-risk dev workflow and design tokens

**Files:**
- Modify selectively: `.gitignore`
- Modify selectively: `BookOfEternityClient.WebFrontend/vite.config.ts`
- Modify selectively: `BookOfEternityClient.WebFrontend/src/styles/tokens.css`
- Possibly modify: `BookOfEternityClient.WebFrontend/src/styles/base.css`
- Test: existing frontend verify and Browser workspace guard tests.

- [ ] **Step 1: Create clean branch from main**

```bash
git checkout main
git pull origin main
git checkout -b salvage/browser-dev-workflow-design-tokens
```

- [ ] **Step 2: Port only selected hunks**

Use interactive checkout, not a full merge:

```bash
git checkout -p refs/heads/audit/remote-Feature -- .gitignore BookOfEternityClient.WebFrontend/vite.config.ts BookOfEternityClient.WebFrontend/src/styles/tokens.css BookOfEternityClient.WebFrontend/src/styles/base.css
```

Reject hunks that depend on the broken composer/result flow.

- [ ] **Step 3: Run frontend verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: typecheck/tests/build pass.

- [ ] **Step 4: Run focused Browser guard tests**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"
```

Expected: non-zero test count, 0 failures. If tests fail, inspect whether the hunk changed behavior or tests need an intentional update; do not weaken guards blindly.

- [ ] **Step 5: Commit and PR**

```bash
git add .gitignore BookOfEternityClient.WebFrontend/vite.config.ts BookOfEternityClient.WebFrontend/src/styles/tokens.css BookOfEternityClient.WebFrontend/src/styles/base.css
git commit -m "chore(browser): salvage dev workflow and design tokens from Feature"
git push -u origin HEAD
gh pr create --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --title "chore(browser): salvage dev workflow and design tokens from Feature" --body "## Summary
- Ports low-risk Browser Client dev workflow/design token hunks from Feature onto a clean main-based branch.
- Does not merge the broken composer/result-flow implementation.

## Verification
- npm run verify --prefix BookOfEternityClient.WebFrontend
- dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter \"FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests\"

Refs #754
Refs #758
Refs #771"
```

---

### Task 4: Salvage PR B — dark-fantasy visual shell/background, without broken flows

**Files:**
- Add or modify: `BookOfEternityClient.WebFrontend/public/main-menu-bg.webp`
- Modify selectively: `BookOfEternityClient.WebFrontend/src/styles/components.css`
- Modify selectively: `BookOfEternityClient.WebFrontend/src/styles/layout.css`
- Modify selectively: `BookOfEternityClient.WebFrontend/src/styles/motion.css`
- Modify selectively: `BookOfEternityClient.WebFrontend/src/App.tsx` only for markup/classes needed by the visual shell.
- Test: frontend verify, Browser guard tests, visual dogfood screenshots.

- [ ] **Step 1: Create clean branch after PR A is merged**

```bash
git checkout main
git pull origin main
git checkout -b salvage/browser-dark-fantasy-shell
```

- [ ] **Step 2: Inspect the background asset before porting**

```bash
file BookOfEternityClient.WebFrontend/public/main-menu-bg.webp || true
git show refs/heads/audit/remote-Feature:BookOfEternityClient.WebFrontend/public/main-menu-bg.webp > /tmp/main-menu-bg.webp
file /tmp/main-menu-bg.webp
```

Expected: the asset type matches `.webp`; visual inspection shows no embedded title text, logos, copied IP, or unwanted lettering.

- [ ] **Step 3: Port only visual hunks**

```bash
git checkout refs/heads/audit/remote-Feature -- BookOfEternityClient.WebFrontend/public/main-menu-bg.webp
git checkout -p refs/heads/audit/remote-Feature -- BookOfEternityClient.WebFrontend/src/styles/components.css BookOfEternityClient.WebFrontend/src/styles/layout.css BookOfEternityClient.WebFrontend/src/styles/motion.css BookOfEternityClient.WebFrontend/src/App.tsx
```

Reject hunks that introduce `/prose`, broken action-result handling, or broad route/control changes.

- [ ] **Step 4: Run verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"
```

Expected: both pass with non-zero test counts.

- [ ] **Step 5: Dogfood first screen visually**

Launch with disposable root and non-default port:

```bash
mkdir -p .hermes/browser-salvage-session
npm run build --prefix BookOfEternityClient.WebFrontend
dotnet run --project BookOfEternityClient -- .hermes/browser-salvage-session --web --web-url http://127.0.0.1:8798
```

Open the page and verify:

- default screen feels like a game launcher, not a dev dashboard;
- no raw API/DTO/file/browser/debug wording in ordinary mode;
- no JS console errors;
- screenshots are saved as audit evidence.

- [ ] **Step 6: Commit and PR**

```bash
git add BookOfEternityClient.WebFrontend/public/main-menu-bg.webp BookOfEternityClient.WebFrontend/src/styles/components.css BookOfEternityClient.WebFrontend/src/styles/layout.css BookOfEternityClient.WebFrontend/src/styles/motion.css BookOfEternityClient.WebFrontend/src/App.tsx
git commit -m "feat(browser): salvage dark fantasy visual shell from Feature"
git push -u origin HEAD
gh pr create --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --title "feat(browser): salvage dark fantasy visual shell from Feature" --body "## Summary
- Ports reviewed dark-fantasy visual shell/background work from Feature onto a clean main-based branch.
- Excludes the broken prose composer and action-result implementation.

## Verification
- npm run verify --prefix BookOfEternityClient.WebFrontend
- focused Browser .NET guard tests
- local browser dogfood with disposable session root

Refs #754
Refs #758
Refs #759"
```

---

### Task 5: Implement real composer/player-action flow before salvaging composer UI

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify or create C# endpoint/service files only after inspecting existing Explorer/prompt-session services.
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Add focused tests for the new endpoint/flow if no existing test class covers it.

- [ ] **Step 1: Write failing tests first**

Add or update tests so these assertions fail on current `Feature` behavior:

- ordinary prose composer must not submit a raw slash command;
- ordinary prose text reaches a real C#-owned player-action/turn/prompt-session path;
- if the backend rejects the action, the UI shows a player-facing error instead of a fake success notice.

- [ ] **Step 2: Verify failing tests**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "BrowserFrontendWorkspaceTests"
```

Expected before fix: failure mentioning `/prose` or missing real player-action endpoint/flow.

- [ ] **Step 3: Implement the backend-owned flow**

Use the smallest safe design after inspecting current C# services:

- preferred: add a local endpoint such as `POST /api/explorer/player-action` or reuse an existing prompt-session/turn endpoint if it exists;
- keep C# authoritative for turn/session writes;
- React sends typed request data only;
- no invented slash command in ordinary mode.

- [ ] **Step 4: Port only the useful composer UI hunks**

Use:

```bash
git checkout -p refs/heads/audit/remote-Feature -- BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/styles/components.css
```

Reject all hunks that submit `/prose` or display fake completion notices.

- [ ] **Step 5: Verify green**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "BrowserFrontendWorkspaceTests|LocalWebUiHostTests|ExplorerWeb"
```

Expected: 0 failures, non-zero test count.

- [ ] **Step 6: Commit and PR**

PR should close or partially close:

- #755 if central composer acceptance is fully satisfied;
- #764 when real player-action submission is proven.

Do not close #755/#764 if the UI still only prepares/fakes a turn.

---

### Task 6: Fix/salvage action palette and result surfaces

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/playerFacingCommandResult.ts`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify or verify: `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
- Modify tests: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs` and/or frontend player-facing tests.

- [ ] **Step 1: Write failing tests for safe block preservation**

Test case:

- input command result contains safe `blocks` with location/description text;
- default player UI result surface renders that text;
- raw JSON/API/debug details remain advanced-only.

- [ ] **Step 2: Verify failure on current donor implementation**

```bash
npm run test --prefix BookOfEternityClient.WebFrontend -- --run
```

or use the repo's existing frontend test command from `npm run verify`.

Expected before fix: test fails because result blocks are dropped or only `Выполнено` appears.

- [ ] **Step 3: Implement sanitizer/result-surface fix**

Required behavior:

- preserve safe blocks for read-only action results;
- render safe result cards/panels in Russian player-facing copy;
- expose raw/debug output only behind explicit advanced/details affordance.

- [ ] **Step 4: Port useful action palette UI only after result rendering is correct**

Use patch checkout for `App.tsx`/CSS, rejecting broad control-panel regressions and fake command wiring.

- [ ] **Step 5: Verify**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "BrowserFrontendWorkspaceTests|LocalWebUiHostTests|ExplorerWeb"
```

Manual dogfood:

- prepared disposable session root;
- choose read-only action like `Где я`;
- verify result panel contains useful location/description, not only `Выполнено`.

- [ ] **Step 6: Commit and PR**

PR should reference:

- #756 if searchable palette is included and usable;
- #757 if polished result surfaces are actually fixed;
- #744/#769 only if World route action density is demonstrably improved.

---

### Task 7: Reconcile Browser guard tests honestly

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`
- Modify source only if the current tests correctly catch a real regression.

- [ ] **Step 1: Run each high-signal guard class individually**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~LocalWebUiHostTests"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"
```

Expected after each salvage PR: non-zero test count, 0 failures.

- [ ] **Step 2: If a test fails, classify it**

Use this rule:

- If test catches player-facing debug/API/dashboard leakage, fix source.
- If test encodes stale markup/class names but the new UI is intentionally better, update the test to assert the new player-facing invariant.
- If test was weakened in `Feature`, reject the hunk and restore the stronger invariant.

- [ ] **Step 3: Commit test reconciliation in the same PR as the behavior it verifies**

Do not create a PR that only weakens tests.

---

### Task 8: Keep #773 separate from Browser salvage

**Files:**
- Future branch only: `BookOfEternityClient/Services/PendingTurnSnapshotAuthority.cs`
- Future tests: pending snapshot authority tests.
- Possible docs: contract/validation docs if accepted-turn authority semantics are documented for GM/devs.

- [ ] **Step 1: Do not mix #773 into Browser PRs**

Reason: Windows CNG/Linux validation is a cross-platform lifecycle-integrity problem, not a Browser UI salvage hunk.

- [ ] **Step 2: Later implement #773 with its own plan/branch**

Target behavior:

- Linux/non-Windows can create/validate authority envelopes;
- tampered payload/file hashes still fail closed;
- mechanism is documented as local lifecycle integrity/tamper evidence, not a strong external trust boundary.

---

### Task 9: Retire the donor branch

**Files:**
- No repository files changed.
- GitHub branch/tag state changes only.

- [ ] **Step 1: Confirm accepted salvage PRs are merged**

Run:

```bash
gh pr list --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --state open --search "Feature salvage"
```

Expected: no open salvage PRs that still need donor hunks.

- [ ] **Step 2: Confirm issues reflect actual state**

Do not close issues merely because the branch was cleaned up. Close only issues whose acceptance criteria landed in `main` and were locally verified.

- [ ] **Step 3: Archive donor HEAD**

```bash
git tag archive/feature-f648101 f6481013dd7d638bbdd0e1e4e0c4d906718712a4
git push origin archive/feature-f648101
```

Expected: tag push succeeds or tag already exists.

- [ ] **Step 4: Delete remote branch**

```bash
git push origin --delete Feature
```

Expected: remote branch deleted. If GitHub refuses because of branch protection, rename/lock it instead and document the reason on #754.

---

## Final verification gate before saying the cleanup is complete

Run from clean `main` after all salvage PRs merge:

```bash
git checkout main
git pull origin main
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet build
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests"
```

For any PR touching shared C# session/save/runtime state, also run a broader test suite:

```bash
dotnet test
```

Report exact pass/fail counts. GitHub Actions are optional for this project unless the user asks to wait for CI.
