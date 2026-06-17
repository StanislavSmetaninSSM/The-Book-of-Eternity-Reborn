# Hermes GitHub Issue-to-PR Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish the repeatable workflow Hermes must follow when taking Book of Eternity Reborn work from GitHub issue to branch, implementation, PR, CI verification, and merge.

**Architecture:** Treat GitHub issues as the task ledger, `AGENTS.md` and GM-facing markdown as project law, and PRs as the only implementation delivery path. Every code or contract change must be traced to an issue, implemented on a feature branch, validated locally, reviewed through a PR, and merged only after approval/green checks.

**Tech Stack:** GitHub Issues/PRs via `gh`, git, .NET 8, xUnit, Book of Eternity Reborn project markdown contracts, Hermes tools, imported `superpowers-*` skills.

---

## File Structure

This is an operational plan, not a feature implementation. It should not require production code changes by itself.

- Read before every project task: `AGENTS.md`
- Read for current handoff and project context when present: `Ai handoff .md`
- Read for CLI/game contract work: `CLI_Rules_Index.md`, `CLI_API_Specification.md`, `CLI_Agent_Daemon_Specification.md`
- Read for launcher/daemon/client startup work: `BookOfEternityClient/Launcher/CLI_Launch_Script.md`
- Read for afterlife contract work: `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`
- Read for mortal-world mechanics work: relevant `Rules/*.txt`, `TaskGuides/*.txt`, `Examples/*.txt`, and GM-facing prompt/guide files located by content search
- Existing tests to consider for documentation-sensitive changes: `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`, `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs`
- Operational plan saved here: `docs/superpowers/plans/2026-05-23-hermes-github-issue-pr-workflow.md`

---

### Task 1: Verify GitHub and repository access

**Files:**
- Read: GitHub repo `StanislavSmetaninSSM/The-Book-of-Eternity-Reborn`
- Read: local repo at `E:/Games/The Book of Eternity Reborn`

- [ ] **Step 1: Check local tools and authentication**

Run:

```bash
git --version
gh --version
gh auth status
```

Expected:

```text
git is installed
gh is installed
gh is logged in to github.com as StanislavSmetaninSSM with repo/workflow scopes
```

- [ ] **Step 2: Verify the repository remote**

Run from repo root:

```bash
git remote -v
git branch --show-current
git status --short
```

Expected:

```text
origin points to github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn
working tree state is known before any edits
```

- [ ] **Step 3: List open issues before choosing work**

Run:

```bash
gh issue list --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --state open --limit 20
```

Expected:

```text
Open issues are visible. Pick only the issue explicitly assigned or requested by Stanislav.
```

- [ ] **Step 4: Commit nothing in this task**

No repository commit is required for access checks.

---

### Task 2: Confirm tracked task before implementation

**Files:**
- Read: selected GitHub issue
- Read: `AGENTS.md`

- [ ] **Step 1: View the issue body**

Run:

```bash
gh issue view <ISSUE_NUMBER> --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn
```

Expected:

```text
Issue contains a clear task, acceptance criteria, and enough scope to plan implementation.
```

- [ ] **Step 2: If no suitable issue exists, create one before editing**

Run only if Stanislav requests new implementation work without an existing issue:

```bash
gh issue create \
  --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn \
  --title "[Task] <specific task title>" \
  --body "## Goal
<one-paragraph goal from Stanislav's request>

## Scope
- <specific file/system area>
- <specific player/GM-facing behavior>

## Acceptance criteria
- <observable result>
- <docs/prompts updated if mechanics or contracts change>
- <tests or verification command>

## Verification
- <exact command or manual check>" \
  --label task
```

Expected:

```text
GitHub returns a new issue number and URL.
```

- [ ] **Step 3: Re-read project guardrails**

Run:

```bash
python - <<'PY'
from pathlib import Path
print(Path('AGENTS.md').read_text(encoding='utf-8'))
PY
```

Expected:

```text
AGENTS.md confirms no implementation changes without a tracked task and warns that contract changes require GM-facing docs/tests.
```

- [ ] **Step 4: Commit nothing in this task**

No repository commit is required for task confirmation.

---

### Task 3: Build project-specific context before editing

**Files:**
- Read: `AGENTS.md`
- Read if present: `Ai handoff .md`
- Read according to subsystem: `CLI_Rules_Index.md`, `CLI_API_Specification.md`, `CLI_Agent_Daemon_Specification.md`, `OtherGuides/*.md`, `docs/web-ui/*.md`, `Rules/*.txt`, `TaskGuides/*.txt`, `Examples/*.txt`

- [ ] **Step 1: Search for subsystem documentation**

Run examples from repo root, adapting search terms to the issue:

```bash
python - <<'PY'
from pathlib import Path
terms = ['Chaos Sea', 'Guardian politics', 'Mortal World', 'GM', 'prompt']
for path in Path('.').rglob('*'):
    if path.suffix.lower() not in {'.md', '.txt'}:
        continue
    try:
        text = path.read_text(encoding='utf-8', errors='ignore')
    except Exception:
        continue
    hits = [t for t in terms if t.lower() in text.lower()]
    if hits:
        print(f'{path}: {", ".join(hits)}')
PY
```

Expected:

```text
Relevant GM-facing docs, prompts, rules, examples, and specs are identified before code changes.
```

- [ ] **Step 2: Decide whether docs/prompts are primary deliverables**

Apply this rule:

```text
If runtime mechanics affect how the GM resolves play, update GM-facing prompts/docs/examples/tests in the same PR. Code alone is insufficient.
```

Expected:

```text
For afterlife and mortal-world mechanics, the implementation plan includes prompt/docs updates, not just code.
```

- [ ] **Step 3: Write a short implementation note for the current issue**

Use this exact note structure in the PR body or plan comment:

```markdown
## Project Context Read
- `AGENTS.md`: read
- Issue: #<ISSUE_NUMBER>
- GM-facing docs/prompts affected: <yes/no and list>
- Runtime code affected: <yes/no and list>
- Tests expected: <list exact commands>
```

- [ ] **Step 4: Commit nothing in this task**

Context gathering should leave the working tree unchanged.

---

### Task 4: Create branch from current master/main

**Files:**
- Modify: git branch state only

- [ ] **Step 1: Fetch and identify default branch**

Run:

```bash
git fetch origin
gh repo view StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --json defaultBranchRef --jq '.defaultBranchRef.name'
```

Expected:

```text
Default branch is printed, usually master or main.
```

- [ ] **Step 2: Checkout default branch and pull**

Run with the printed default branch:

```bash
git checkout <DEFAULT_BRANCH>
git pull origin <DEFAULT_BRANCH>
```

Expected:

```text
Local default branch is up to date with origin.
```

- [ ] **Step 3: Create issue branch**

Run:

```bash
git checkout -b feat/issue-<ISSUE_NUMBER>-<short-kebab-description>
```

Expected:

```text
New branch is active and named after the issue.
```

- [ ] **Step 4: Commit nothing in this task**

Branch creation should not create a commit.

---

### Task 5: Implement with TDD and documentation parity

**Files:**
- Modify: files identified by issue-specific context search
- Test: relevant xUnit tests under `BookOfEternityClient.Tests/`
- Docs/prompts: relevant GM-facing `.md`/`.txt` files when mechanics/contracts change

- [ ] **Step 1: Write or update failing tests first**

For .NET tests, run targeted tests like:

```bash
dotnet test BookOfEternityClient/BookOfEternityClient.sln --no-restore --filter "<SpecificTestClassOrTrait>"
```

Expected before implementation:

```text
New or updated test fails for the expected reason.
```

- [ ] **Step 2: Implement minimal runtime change**

Only modify files required by the issue. Do not widen scope beyond the issue acceptance criteria.

Expected:

```text
Runtime behavior now satisfies the failing test without unrelated refactors.
```

- [ ] **Step 3: Update GM-facing docs/prompts/examples for mechanics changes**

Apply this rule:

```text
Afterlife contract change: update afterlife matrix/examples/manifest/documentation coverage tests.
Mortal-world mechanics change: update relevant GM prompts/rules/task guides/examples so GM does not need code as the primary authority.
```

Expected:

```text
The GM-facing source of truth describes the new behavior explicitly.
```

- [ ] **Step 4: Run targeted tests**

For afterlife documentation-sensitive changes, run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

For other changes, run the most specific relevant test class first:

```bash
dotnet test BookOfEternityClient/BookOfEternityClient.sln --no-restore --filter "<SpecificTestClassOrTrait>"
```

Expected:

```text
Targeted tests pass.
```

- [ ] **Step 5: Commit the coherent slice**

Run:

```bash
git status --short
git add <changed-files>
git commit -m "feat: implement issue <ISSUE_NUMBER> <short description>"
```

Expected:

```text
Commit includes code, tests, and docs/prompts for the same coherent behavior slice.
```

---

### Task 6: Run verification before PR

**Files:**
- Read: test output
- Read: git diff

- [ ] **Step 1: Review final diff**

Run:

```bash
git diff --stat <DEFAULT_BRANCH>...HEAD
git diff <DEFAULT_BRANCH>...HEAD -- AGENTS.md
```

Expected:

```text
Diff is limited to intended files. AGENTS.md is normally unchanged.
```

- [ ] **Step 2: Run relevant verification**

Run at minimum:

```bash
dotnet test BookOfEternityClient/BookOfEternityClient.sln --no-restore
```

If this is too broad or slow, run targeted tests and clearly report that full solution tests were not run.

Expected:

```text
Tests pass or failures are understood and reported before PR creation.
```

- [ ] **Step 3: Check working tree cleanliness**

Run:

```bash
git status --short
```

Expected:

```text
No uncommitted implementation files remain, unless intentionally documented.
```

- [ ] **Step 4: Commit fixes if verification changed files**

Run only if verification caused legitimate file updates:

```bash
git add <changed-files>
git commit -m "test: update verification coverage for issue <ISSUE_NUMBER>"
```

Expected:

```text
All intended changes are committed before push.
```

---

### Task 7: Push branch and create PR

**Files:**
- Modify: remote branch and GitHub PR only

- [ ] **Step 1: Push current branch**

Run:

```bash
git push -u origin HEAD
```

Expected:

```text
Remote branch is created on origin.
```

- [ ] **Step 2: Create PR linked to issue**

Run:

```bash
gh pr create \
  --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn \
  --base <DEFAULT_BRANCH> \
  --title "feat: <short issue description>" \
  --body "## Summary
- <specific runtime behavior changed>
- <specific GM-facing docs/prompts/examples updated>
- <specific tests added or updated>

## Project Context Read
- \`AGENTS.md\`: read
- Issue: #<ISSUE_NUMBER>
- GM-facing docs/prompts affected: <yes/no and list>
- Runtime code affected: <yes/no and list>
- Tests expected: <exact commands>

## Verification
- [ ] \`dotnet test BookOfEternityClient/BookOfEternityClient.sln --no-restore\`
- [ ] Additional targeted command: \`<command>\`

Closes #<ISSUE_NUMBER>"
```

Expected:

```text
GitHub returns PR URL. PR body links issue with Closes #<ISSUE_NUMBER>.
```

- [ ] **Step 3: Report PR to Stanislav**

Message format:

```text
PR created: <PR_URL>
Issue: #<ISSUE_NUMBER>
Verification run: <commands and result>
Docs/prompts updated: <yes/no and list>
Waiting for CI/review before merge.
```

- [ ] **Step 4: Commit nothing in this task**

PR creation should not create local commits.

---

### Task 8: Monitor CI, fix failures, and merge only when allowed

**Files:**
- Modify: code/docs/tests only if CI failure requires fixes
- Modify: GitHub PR state when merging

- [ ] **Step 1: Check PR checks**

Run:

```bash
gh pr checks --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --watch
```

Expected:

```text
Checks finish as passing, failing, or absent.
```

- [ ] **Step 2: If CI fails, inspect logs**

Run:

```bash
gh run list --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --branch $(git branch --show-current) --limit 5
gh run view <RUN_ID> --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --log-failed
```

Expected:

```text
Failure cause is identified before editing.
```

- [ ] **Step 3: Fix CI failure with a separate commit**

Run after editing the minimal fix:

```bash
git add <changed-files>
git commit -m "fix: resolve CI failure for issue <ISSUE_NUMBER>"
git push
```

Expected:

```text
CI fix is pushed to the same PR branch.
```

- [ ] **Step 4: Ask/confirm before merge when user approval is required**

Do not merge a PR that is still under Codex work, under user review, failing CI, or awaiting explicit approval.

Expected:

```text
Stanislav has indicated the PR is ready to merge, or repository policy permits merge after green checks.
```

- [ ] **Step 5: Merge PR**

Run when approved and green:

```bash
gh pr merge --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --squash --delete-branch
```

Expected:

```text
PR is merged into the default branch and remote branch is deleted.
```

- [ ] **Step 6: Sync local default branch after merge**

Run:

```bash
git checkout <DEFAULT_BRANCH>
git pull origin <DEFAULT_BRANCH>
git status --short
```

Expected:

```text
Local default branch contains the merged PR and working tree is clean.
```

---

## Self-Review

**Spec coverage:**
- GitHub access and issue visibility are covered by Task 1.
- Tracked-task guardrail from `AGENTS.md` is covered by Task 2.
- Project `.md` inheritance and GM-facing documentation rules are covered by Task 3.
- Branch, implementation, tests, commit, PR, CI, and merge workflow are covered by Tasks 4-8.
- Afterlife and mortal-world prompt/doc parity is explicitly covered in Tasks 3 and 5.

**Placeholder scan:**
- The only angle-bracket values are deliberate per-issue parameters to replace at execution time, such as `<ISSUE_NUMBER>` and `<DEFAULT_BRANCH>`.
- There are no `TBD`, `TODO`, or undocumented "implement later" steps.

**Type consistency:**
- Commands consistently use `StanislavSmetaninSSM/The-Book-of-Eternity-Reborn` as the GitHub repo.
- Local project path assumption is `E:/Games/The Book of Eternity Reborn`.
- Test commands consistently target .NET 8 solution/project files already present in the repository.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-23-hermes-github-issue-pr-workflow.md`.

Two execution options when Stanislav hands off a concrete issue:

**1. Subagent-Driven (recommended)** - dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** - execute tasks in this session using executing-plans, batch execution with checkpoints.
