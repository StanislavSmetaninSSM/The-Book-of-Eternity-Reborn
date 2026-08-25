# Remove Legacy CLI Bootstrap Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish PR #1545 by removing the unreachable CLI bootstrap contract and aligning its source-guard tests with the active retry-based daemon architecture.

**Architecture:** Keep Ivan's deletion of the disconnected PowerShell state and function. Replace the two tests that require the dormant prompt with negative source guards while retaining positive coverage for bounded context-pack guidance and `Dispatch-WithRetry` in every active dispatch phase.

**Tech Stack:** PowerShell 7, C# 12, .NET 8, xUnit, repository-bounded C# test lanes.

## Global Constraints

- GitHub issue #1549 tracks every repository change; PR #1545 remains the implementation PR.
- Preserve Ivan's original commit and use maintainer edits on his existing fork branch.
- Do not restore automatic or manual dispatch of a separate bootstrap request.
- Do not change GM-facing docs/examples because the deleted path was unreachable; record this rationale in the PR and closing comment.
- Keep `.serena/` local and untracked.
- Use PowerShell 7 and `scripts/test-csharp.ps1`; run one final PreMerge without adding a duplicate Fast run immediately before it.

---

### Task 1: Replace the legacy-positive daemon contract with absence guards

**Files:**
- Modify: `BookOfEternityClient.IntegrationTests/GmTurnHelperContractTests.cs:2365`
- Verify: `BookOfEternityClient/game_master_daemon.ps1`

**Interfaces:**
- Consumes: daemon source loaded by `ReadRepoFile(string)` and function slices returned by `ExtractFunctionBlock(string, string)`.
- Produces: xUnit guards named `DaemonContextPackGuidance_RemainsBoundedWithoutLegacyBootstrapRequest` and `DaemonAutomaticDispatch_UsesCorrelatedRetriesWithoutLegacyBootstrapRequest`.

- [ ] **Step 1: Restore the legacy daemon only in the working tree for RED proof**

Run:

```powershell
git restore --source=origin/main -- BookOfEternityClient/game_master_daemon.ps1
```

Expected: the working tree temporarily contains `$script:BootstrapSent` and `Ensure-CliBootstrapSent`; this restoration is never committed.

- [ ] **Step 2: Replace the two outdated tests with the desired contract**

Use this exact test code:

```csharp
[Fact]
public void DaemonContextPackGuidance_RemainsBoundedWithoutLegacyBootstrapRequest()
{
    var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

    Assert.Contains("Bootstrap scope:", daemon, StringComparison.Ordinal);
    Assert.Contains("read only context_pack_manifest.json and README.md", daemon, StringComparison.Ordinal);
    Assert.Contains("Do not open copied guides/examples during bootstrap", daemon, StringComparison.Ordinal);
    Assert.Contains("Open large copied docs only when a per-turn, repair, or terminal-failure prompt explicitly names them.", daemon, StringComparison.Ordinal);
    Assert.DoesNotContain("BOOTSTRAP GM SESSION", daemon, StringComparison.Ordinal);
    Assert.DoesNotContain("BOE_GM_BOOTSTRAP_READY", daemon, StringComparison.Ordinal);
}

[Fact]
public void DaemonAutomaticDispatch_UsesCorrelatedRetriesWithoutLegacyBootstrapRequest()
{
    var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
    var turnBlock = ExtractFunctionBlock(daemon, "function Process-Turn");
    var repairBlock = ExtractFunctionBlock(daemon, "function Process-RepairRequest");
    var terminalFailureBlock = ExtractFunctionBlock(daemon, "function Process-TerminalProtocolFailureRequest");

    Assert.DoesNotContain("$script:BootstrapSent", daemon, StringComparison.Ordinal);
    Assert.DoesNotContain("function Ensure-CliBootstrapSent", daemon, StringComparison.Ordinal);
    Assert.DoesNotContain("Bootstrap launch script dispatched", daemon, StringComparison.Ordinal);
    Assert.Contains("Dispatch-WithRetry", turnBlock, StringComparison.Ordinal);
    Assert.Contains("Dispatch-WithRetry", repairBlock, StringComparison.Ordinal);
    Assert.Contains("Dispatch-WithRetry", terminalFailureBlock, StringComparison.Ordinal);
}
```

- [ ] **Step 3: Run the exact RED selection**

Run:

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~DaemonContextPackGuidance_RemainsBoundedWithoutLegacyBootstrapRequest|FullyQualifiedName~DaemonAutomaticDispatch_UsesCorrelatedRetriesWithoutLegacyBootstrapRequest"
```

Expected: two failures because the temporarily restored daemon still contains the legacy bootstrap prompt/state/function.

- [ ] **Step 4: Restore Ivan's minimal implementation**

Run:

```powershell
git restore --source=HEAD -- BookOfEternityClient/game_master_daemon.ps1
```

Expected: only `GmTurnHelperContractTests.cs` remains modified; Ivan's 31-line daemon deletion is restored.

- [ ] **Step 5: Run the exact GREEN selection**

Run the same Focused command from Step 3.

Expected: 2/2 pass, zero failures, no timeout, cleanup complete, zero duplicate test IDs.

- [ ] **Step 6: Parse the PowerShell daemon**

Run:

```powershell
$tokens = $null
$parseErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path '.\BookOfEternityClient\game_master_daemon.ps1'),
    [ref]$tokens,
    [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }
```

Expected: exit code 0 and no parse errors.

- [ ] **Step 7: Commit the test-contract correction**

Run:

```powershell
git add -- BookOfEternityClient.IntegrationTests/GmTurnHelperContractTests.cs
git diff --cached --check
git commit -m "test: retire legacy CLI bootstrap contract (#1549)"
```

Expected: one test-only commit; `.serena/` remains untracked.

### Task 2: Verify and integrate the completed external PR

**Files:**
- Review: `BookOfEternityClient/game_master_daemon.ps1`
- Review: `BookOfEternityClient.IntegrationTests/GmTurnHelperContractTests.cs`
- Review: `docs/superpowers/specs/2026-08-26-remove-legacy-cli-bootstrap-design.md`
- Review: `docs/superpowers/plans/2026-08-26-remove-legacy-cli-bootstrap.md`

**Interfaces:**
- Consumes: completed commits from Task 1 and repository test-lane summaries.
- Produces: updated PR #1545, merge commit in `main`, and completed issue #1549.

- [ ] **Step 1: Run the complete focused contract class**

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~GmTurnHelperContractTests"
```

Expected: all 120 tests pass with no timeout or duplicate IDs and complete owned-tree cleanup.

- [ ] **Step 2: Run one meaningful Fast checkpoint**

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Expected: exit code 0, zero failed tests, no timeout, complete cleanup, zero duplicate IDs.

- [ ] **Step 3: Review the exact candidate**

```powershell
git status --short --branch
git diff origin/main...HEAD --check
git diff origin/main...HEAD --stat
git diff origin/main...HEAD -- BookOfEternityClient/game_master_daemon.ps1 BookOfEternityClient.IntegrationTests/GmTurnHelperContractTests.cs docs/superpowers/specs/2026-08-26-remove-legacy-cli-bootstrap-design.md docs/superpowers/plans/2026-08-26-remove-legacy-cli-bootstrap.md
```

Expected: only issue-owned paths plus the merge ancestry; no `.serena/`, workflow, setting, prompt, rule, example, manifest, or unrelated change.

- [ ] **Step 4: Run the final PreMerge gate**

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Expected: exit code 0, all selected C# and frontend tests pass, no timeout, complete cleanup, zero duplicate IDs.

- [ ] **Step 5: Push through maintainer edits and update PR #1545**

Push the current HEAD to `IvanSkainet/The-Book-of-Eternity-Reborn:arena-ai/remove-dead-cli-bootstrap`, then update the PR body with `Relates to #1549`, RED/GREEN evidence, final lane evidence, and the explicit GM-documentation no-update rationale.

Expected: PR #1545 head matches local HEAD and remains targeted at `main`.

- [ ] **Step 6: Review, merge, and close tracking**

Approve PR #1545 as CODEOWNER, merge it with the repository-supported merge strategy, fetch `origin/main`, and verify the feature tip is its ancestor. Close #1549 as completed with the merge commit, verification counts, and no-update rationale.

Expected: PR #1545 is `MERGED`, issue #1549 is `CLOSED/COMPLETED`, and the merged feature tip is reachable from `origin/main`.
