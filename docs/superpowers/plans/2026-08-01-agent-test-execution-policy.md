# Agent C# Test Execution Policy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the bounded C# test workflow a permanent root-level agent instruction and remove the remaining guidance that encourages duplicated or stale verification.

**Architecture:** Root `AGENTS.md` will contain the short mandatory policy that every agent sees, while `docs/testing.md` remains the canonical detailed runbook. The active #1505 plan and quickstart will be aligned only where they prescribe future work; their historical two-Fast acceptance evidence remains unchanged.

**Tech Stack:** Markdown repository instructions, PowerShell 7 static assertions, Git

## Global Constraints

- Work is tracked by GitHub issue #1507.
- Do not change test-lane composition, filters, deadlines, process containment, categories, project files, or executable code.
- Use `scripts/test-csharp.ps1` as the normal bounded C# verification entry point.
- During implementation, use the smallest relevant `Focused` selection and one `Fast` control at a meaningful checkpoint.
- Immediately before merge, use one `PreMerge` control without ritual duplicate Fast runs.
- Keep `DeepValidation`, `LifecycleIntegration`, and other diagnostic lanes conditional.
- Do not run a C# build or test lane for this instruction-only change.
- Preserve `.serena/` and generated `bin/` or `obj/` directories as unstaged local artifacts.

---

### Task 1: Make the bounded policy mandatory in root AGENTS.md

**Files:**
- Modify: `AGENTS.md:3-7`
- Modify: `AGENTS.md:96-116`

**Interfaces:**
- Consumes: `scripts/test-csharp.ps1` lane names and `docs/testing.md` as the canonical detailed guide.
- Produces: A root-level policy that tells every agent which bounded command to use and a corrected afterlife verification route.

- [ ] **Step 1: Run the pre-change policy assertion and verify RED**

Run:

```powershell
$agents = Get-Content -Raw -LiteralPath AGENTS.md
$required = @(
    "## C# test execution policy",
    '.\scripts\test-csharp.ps1',
    "docs/testing.md",
    "one `PreMerge`",
    "DeepValidation",
    "LifecycleIntegration"
)
$missing = @($required | Where-Object { -not $agents.Contains($_) })
if ($missing.Count -gt 0) {
    throw "Missing mandatory agent test policy: $($missing -join ', ')"
}
if ($agents.Contains("BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs")) {
    throw "Stale ExampleDocumentationValidationTests project path remains."
}
```

Expected: non-zero with `Missing mandatory agent test policy`, proving that
root `AGENTS.md` does not yet carry the durable policy.

- [ ] **Step 2: Add the mandatory policy after the task-tracking guardrail**

Insert this section after the task-tracking paragraph:

```markdown
## C# test execution policy

Use PowerShell 7 and `.\scripts\test-csharp.ps1` as the normal bounded entry
point for C# verification. Read `docs/testing.md` for lane selection, limits,
result artifacts, and failure diagnosis.

- During implementation, run the smallest relevant `Focused` selection, then
  one `Fast` control at a meaningful checkpoint.
- Immediately before merge, run one `PreMerge` control. Do not add duplicate
  Fast runs immediately before it because PreMerge already includes the full
  fast project.
- Run `DeepValidation`, `LifecycleIntegration`, or another diagnostic lane
  only for a related boundary change, failure diagnosis, or an explicitly
  requested exhaustive control.
- Do not use an unbounded full-solution or full-suite `dotnet test` command as
  an ordinary verification step.
```

- [ ] **Step 3: Correct the afterlife paths and bounded commands**

Replace:

```markdown
- `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs`
```

with:

```markdown
- `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
```

Replace the stale direct command with:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
.\scripts\test-csharp.ps1 -Lane FullValidation
```

Add one sentence immediately after the command block:

```markdown
Run `FullValidation` here only when the documentation/examples boundary is
affected; it remains a conditional diagnostic lane.
```

- [ ] **Step 4: Re-run the policy assertion and verify GREEN**

Run the exact PowerShell assertion from Step 1.

Expected: exit `0` with no output.

- [ ] **Step 5: Verify the edited root guidance is internally coherent**

Run:

```powershell
rg -n -C 2 "C# test execution policy|test-csharp|PreMerge|DeepValidation|LifecycleIntegration|ExampleDocumentationValidationTests" AGENTS.md
git diff --check -- AGENTS.md
```

Expected: the permanent section and corrected integration path are shown;
`git diff --check` exits `0`.

- [ ] **Step 6: Commit the root policy**

```powershell
git add -- AGENTS.md
git commit -m "docs: require bounded C# tests for agents (#1507)"
```

Expected: one commit containing only `AGENTS.md`.

### Task 2: Align the canonical runbook and future-work guidance

**Files:**
- Modify: `docs/testing.md:77-95`
- Modify: `specs/1505-test-suite-performance/plan.md:72-77`
- Modify: `specs/1505-test-suite-performance/quickstart.md:47-69`

**Interfaces:**
- Consumes: The mandatory rhythm established in Task 1.
- Produces: One consistent future workflow while preserving #1505's historical two-Fast evidence.

- [ ] **Step 1: Run the pre-change workflow assertion and verify RED**

Run:

```powershell
$testing = Get-Content -Raw -LiteralPath docs/testing.md
$plan = Get-Content -Raw -LiteralPath specs/1505-test-suite-performance/plan.md
$quickstart = Get-Content -Raw -LiteralPath specs/1505-test-suite-performance/quickstart.md
$oldCurrentRules = @(
    $testing.Contains("At final verification, run two consecutive Fast controls and one"),
    $plan.Contains("two consecutive Fast controls at final verification"),
    $quickstart.Contains("At final verification, run two consecutive")
)
if ($oldCurrentRules -contains $true) {
    throw "Current workflow still prescribes duplicate final Fast controls."
}
```

Expected: non-zero with `Current workflow still prescribes duplicate final
Fast controls`.

- [ ] **Step 2: Replace the canonical Working Rhythm**

Replace `docs/testing.md` lines 79-95 with:

````markdown
During implementation, run the smallest relevant Focused filter first and one
Fast control at a meaningful checkpoint. Do not run every lane after every
edit. Immediately before merge, run one PreMerge control:

```powershell
.\scripts\test-csharp.ps1 -Lane PreMerge
```

Do not repeat Fast immediately before PreMerge solely as a ritual: PreMerge
already includes the complete fast project. Do not serially run all slow
diagnostic lanes before a green PreMerge. If a bounded control fails, inspect
its summary, log, and TRX evidence, then run only the smallest diagnostic lane
or focused filter needed to identify the cause. Run LifecycleIntegration or
DeepValidation when the changed boundary requires it, when diagnosing a
related failure, or for an explicitly requested exhaustive control.
````

- [ ] **Step 3: Align the active #1505 plan's future-work paragraph**

Replace the paragraph beginning `The diagnostic lanes are not serial final
gates` in `specs/1505-test-suite-performance/plan.md` with:

```markdown
The diagnostic lanes are not serial final gates. Run focused controls during
implementation, one Fast control at a meaningful checkpoint, and one PreMerge
control immediately before merge. Do not repeat Fast immediately before
PreMerge or serially run all diagnostic lanes unless a focused failure requires
diagnosis. `Complete` is a temporary alias for `PreMerge`.
LifecycleIntegration and DeepValidation are conditional and explicit; this
branch ran each once because their category boundaries changed.
```

Do not change the plan's historical requirements and evidence at lines 31-34,
88-90, or 209-224.

- [ ] **Step 4: Align the quickstart's Recommended Workflow**

Replace `specs/1505-test-suite-performance/quickstart.md` lines 49-69 with:

````markdown
Run the smallest relevant Focused control during implementation and one Fast
control at a meaningful checkpoint. Immediately before merge, run one PreMerge
control. Do not repeat Fast immediately before PreMerge or serially run all
diagnostic lanes unless a focused failure requires diagnosis.
LifecycleIntegration and DeepValidation are conditional and explicit; use them
for changes to those boundaries, related diagnosis, or an explicitly requested
exhaustive control.

```powershell
# During implementation
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"

# Meaningful checkpoint
.\scripts\test-csharp.ps1

# Immediately before merge
.\scripts\test-csharp.ps1 -Lane PreMerge
```

If PreMerge is green, do not follow it with serial FullValidation,
RegressionIntegration, ProcessIntegration, E2E, LifecycleIntegration, and
DeepValidation runs.
````

Do not change the quickstart's Fresh Final Evidence table, which remains the
historical performance evidence for #1505.

- [ ] **Step 5: Re-run the workflow assertion and verify GREEN**

Run the exact PowerShell assertion from Step 1.

Expected: exit `0` with no output.

- [ ] **Step 6: Verify present policy and historical evidence are distinct**

Run:

```powershell
rg -n -C 2 "meaningful checkpoint|Immediately before merge|two consecutive Fast|two final Fast|Fast 1|Fast 2" `
  docs/testing.md `
  specs/1505-test-suite-performance/plan.md `
  specs/1505-test-suite-performance/quickstart.md
git diff --check -- `
  docs/testing.md `
  specs/1505-test-suite-performance/plan.md `
  specs/1505-test-suite-performance/quickstart.md
```

Expected: current-workflow sections prescribe one meaningful-checkpoint Fast
and one immediate PreMerge; two-Fast wording appears only in #1505 historical
requirements/evidence; `git diff --check` exits `0`.

- [ ] **Step 7: Commit the aligned detailed guidance**

```powershell
git add -- `
  docs/testing.md `
  specs/1505-test-suite-performance/plan.md `
  specs/1505-test-suite-performance/quickstart.md
git commit -m "docs: align bounded test workflow guidance (#1507)"
```

Expected: one commit containing exactly the three guidance files.

### Task 3: Perform final static verification

**Files:**
- Verify: `AGENTS.md`
- Verify: `docs/testing.md`
- Verify: `specs/1505-test-suite-performance/plan.md`
- Verify: `specs/1505-test-suite-performance/quickstart.md`
- Verify: `docs/superpowers/specs/2026-08-01-agent-test-execution-policy-design.md`
- Verify: `docs/superpowers/plans/2026-08-01-agent-test-execution-policy.md`

**Interfaces:**
- Consumes: Tasks 1 and 2.
- Produces: Reviewable static evidence that the durable agent policy matches the approved design.

- [ ] **Step 1: Run the combined policy assertions**

Run both exact GREEN assertions from Task 1 Step 1 and Task 2 Step 1 in one
PowerShell session.

Expected: exit `0` with no output.

- [ ] **Step 2: Scan for stale paths and unbounded ordinary guidance**

Run:

```powershell
$files = @(
    "AGENTS.md",
    "docs/testing.md",
    "specs/1505-test-suite-performance/plan.md",
    "specs/1505-test-suite-performance/quickstart.md"
)
rg -n "BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs" @files
if ($LASTEXITCODE -eq 0) {
    throw "Stale ExampleDocumentationValidationTests path remains."
}
if ($LASTEXITCODE -ne 1) {
    throw "rg failed while checking stale paths."
}
rg -n -C 2 "unbounded|dotnet test|test-csharp|PreMerge|DeepValidation|LifecycleIntegration" @files
```

Expected: no stale path; displayed context consistently favors the bounded
runner. Historical direct commands outside the edited policy remain out of
scope unless they claim to be the ordinary full-suite workflow.

- [ ] **Step 3: Check the complete branch diff**

Run:

```powershell
git diff --check main...HEAD
git diff --stat main...HEAD
git status --short
```

Expected: `git diff --check` exits `0`; the diff contains only the design,
implementation plan, and four intended guidance files; status contains only
the pre-existing local `.serena/` and generated `obj/` artifacts.

- [ ] **Step 4: Review without running C# tests**

Inspect:

```powershell
git diff --word-diff=plain main...HEAD -- `
  AGENTS.md `
  docs/testing.md `
  specs/1505-test-suite-performance/plan.md `
  specs/1505-test-suite-performance/quickstart.md
```

Expected: no executable source, test source, project, or runner changes. Record
that C# tests were intentionally not run because the approved design requires
static verification for this instruction-only task.
