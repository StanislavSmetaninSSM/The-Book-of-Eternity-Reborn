# Open Task Closure Roadmap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close and merge all currently open tracked tasks in a dependency-safe order without destabilizing `main`.

**Architecture:** Treat this as a roadmap plan, not one monolithic implementation. Each leaf issue is implemented on a fresh branch from `main`, verified, merged, pushed, and closed before moving to the next dependent issue. Umbrella/meta issues close only after their child issues are done or their design/decomposition acceptance criteria are already satisfied.

**Tech Stack:** GitHub issues, git branches, .NET 8 test suite, existing C# client, afterlife contract documentation tests, Superpowers execution workflow.

---

## Task 1: Close completed design/meta issues

**Files:**
- No repo file changes required.

- [ ] **Step 1: Verify design specs exist**

Run:

```powershell
Test-Path docs\superpowers\specs\2026-05-20-wings-over-the-abyss-design.md
Test-Path docs\superpowers\specs\2026-05-20-local-web-ui-design.md
```

Expected: both commands print `True`.

- [ ] **Step 2: Close #532 if all child issues are linked**

Run:

```powershell
gh issue view 532 --comments
```

Expected: comments include child issues `#533` through `#558`.

Then close:

```powershell
gh issue close 532 --comment "Design spec is committed and implementation has been split into child issues #533-#558."
```

- [ ] **Step 3: Close #559 if all child issues are linked**

Run:

```powershell
gh issue view 559 --comments
```

Expected: comments include child issues `#560` through `#577`.

Then close:

```powershell
gh issue close 559 --comment "Design spec is committed and implementation has been split into child issues #560-#577."
```

## Task 2: Afterlife contract registry and docs hardening

**Issues:** `#510`, `#511`, `#512`, then close umbrella `#504`.

- [ ] **Step 1: Implement #510**

Create branch:

```powershell
git checkout main
git pull --ff-only
git checkout -b issue-510-afterlife-contract-inventory
```

Implement the inventory of afterlife pending/control surfaces. Commit with:

```powershell
git commit -m "docs: inventory afterlife pending contracts" -m "Refs #510"
```

Verify with relevant docs/audit tests, merge to `main`, push, close `#510`.

- [ ] **Step 2: Implement #511**

Create `issue-511-afterlife-contract-registry` from fresh `main`. Implement reusable registry without behavior drift. Run targeted validation and afterlife tests. Merge, push, close `#511`.

- [ ] **Step 3: Implement #512**

Create `issue-512-afterlife-registry-coverage` from fresh `main`. Add completeness tests for blockers/status/docs. Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests"
```

Merge, push, close `#512`.

- [ ] **Step 4: Close #504**

Close only after `#510`, `#511`, and `#512` are closed.

## Task 3: Live reminders, terminology, and entity profile follow-ups

**Issues:** `#524`-`#531`, then close umbrellas `#508`, `#507`, `#509`.

- [ ] **Step 1: Close live reminder/doc consistency**

Implement in order:

```text
#524 -> #525 -> #526
```

Run docs-sensitive tests after each issue. Close `#508` after all three are done.

- [ ] **Step 2: Close Russian terminology tasks**

Implement in order:

```text
#521 -> #522 -> #523
```

Run relevant ExplorerMode/player-facing tests after each issue. Close `#507` after all three are done.

- [ ] **Step 3: Close entity profile follow-ups**

Implement in order:

```text
#527 -> #528 -> #529 -> #530 -> #531
```

Run entity profile, afterlife combat, and terminal game-over targeted tests. Close `#509` after all five are done.

## Task 4: Afterlife combat and lifecycle regression coverage

**Issues:** `#513`-`#520`, then close umbrellas `#505`, `#506`.

- [ ] **Step 1: Close combat balance tasks**

Implement in order:

```text
#513 -> #514 -> #515 -> #516 -> #517
```

After each task run targeted spiritual combat tests. After `#517`, run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeSpiritualConflict|AfterlifeCombat|AfterlifeDocumentationCoverageTests"
```

Close `#505`.

- [ ] **Step 2: Close lifecycle E2E tasks**

Implement in order:

```text
#518 -> #519 -> #520
```

Run Soul Gates, Shining, Source of Light, rollback/restart targeted tests. Close `#506`.

## Task 5: Saref narrative foundation

**Issues:** `#548`, `#536`, `#537`, `#538`-`#546`.

- [ ] **Step 1: Implement #548 Saref character bible**

Start with narrative documentation only. Do not add runtime state in this task. Merge, push, close `#548`.

- [ ] **Step 2: Implement new Eternal Guardians**

Implement in order:

```text
#536 -> #537
```

Run System Guardian library and ExplorerMode guardian tests after each.

- [ ] **Step 3: Implement guardian questline bible tasks**

Implement in order:

```text
#538 -> #539 -> #540 -> #541 -> #542 -> #543 -> #544 -> #545 -> #546
```

Each issue should add one Guardian's four quest blueprints, fragment, advantage, and GM constraints. Keep runtime integration out until `#547`.

## Task 6: Saref core runtime

**Issues:** `#533`, `#547`, `#552`, `#534`, `#535`.

- [ ] **Step 1: Implement #533**

Add `main_story_saref_state.json` contract, default/legacy behavior, validation, and baseline handling.

- [ ] **Step 2: Implement #547**

Connect Guardian questline state with latent progress, ordered completion, and quest 4 reveal/advantage unlock.

- [ ] **Step 3: Implement #552**

Add explicit advantage usage, state transitions, and validation.

- [ ] **Step 4: Implement #534**

Add `/сареф` and `/saref` no-spoiler command. Before reveal it must not mention Saref or Wings of Angels.

- [ ] **Step 5: Implement #535**

Add `/сареф найти_крылья` and `pending_saref_wings_infiltration.json`.

Run after each issue:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Saref|AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests"
```

## Task 7: Saref factions and endings

**Issues:** `#550`, `#551`, `#549`, `#553`-`#558`.

- [ ] **Step 1: Implement general Shining faction lifecycle**

Implement `#550` before Wings-specific faction behavior. Factions must not be deleted from `factions[]`; they receive lifecycle/status.

- [ ] **Step 2: Implement player conflict campaigns**

Implement `#551` and connect breakthroughs to lifecycle transitions.

- [ ] **Step 3: Implement Wings of Angels hidden faction**

Implement `#549` using the lifecycle model from `#550`.

- [ ] **Step 4: Implement Saref defeat/final/endgame tasks**

Implement in order:

```text
#553 -> #554 -> #555 -> #556 -> #557 -> #558
```

After `#558`, run docs-sensitive and broad afterlife tests:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Saref|Shining|Afterlife|ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

## Task 8: Web UI foundation

**Issues:** `#560`-`#568`.

- [ ] **Step 1: Implement architecture prerequisites**

Implement in order:

```text
#560 -> #561 -> #562 -> #563 -> #564
```

Do not start local web host until DTO protocol, migration registry, and console renderer exist.

- [ ] **Step 2: Implement local web runtime**

Implement in order:

```text
#568 -> #565 -> #566 -> #567
```

Session lock `#568` must be in place before mutating browser commands are exposed.

Run Web UI targeted tests after each issue.

## Task 9: Web UI full command migration

**Issues:** `#569`-`#577`.

- [ ] **Step 1: Migrate command groups**

Implement in order:

```text
#569 -> #570 -> #571 -> #572 -> #573 -> #574 -> #575
```

Each task must update the migration registry and leave no command accidentally unclassified.

- [ ] **Step 2: Add parity and launch coverage**

Implement:

```text
#576 -> #577
```

Run final web UI + full test pass:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

## Task 10: Per-issue merge protocol

- [ ] **Step 1: Start each issue from clean main**

```powershell
git checkout main
git pull --ff-only
git status --short --branch
git checkout -b issue-<number>-<short-slug>
```

- [ ] **Step 2: Implement only that issue**

Do not mix unrelated issues in one branch unless the issue explicitly depends on closing a parent/meta task with no code changes.

- [ ] **Step 3: Verify**

Run targeted tests and required docs tests for afterlife contract changes.

- [ ] **Step 4: Commit**

```powershell
git add <changed files>
git commit -m "<type>: <short summary>" -m "Refs #<number>"
```

- [ ] **Step 5: Merge and close**

```powershell
git checkout main
git merge --no-ff issue-<number>-<short-slug>
git push origin main
gh issue close <number> --comment "Implemented and merged to main."
```

## Task 11: Final verification

- [ ] **Step 1: Confirm no open issues from the tracked set**

```powershell
gh issue list --state open --limit 200 --json number,title --jq '.[] | select(.number >= 504 and .number <= 578) | "#\\(.number) \\(.title)"'
```

Expected: no output.

- [ ] **Step 2: Run full test suite**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

Expected: `Failed: 0`.

- [ ] **Step 3: Confirm main is clean and pushed**

```powershell
git status --short --branch --untracked-files=no
git rev-parse HEAD
git rev-parse origin/main
```

Expected: no ahead/behind and identical hashes.
