# Bounded PreMerge and DeepValidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the mathematically impossible all-inclusive PreMerge schedule with disjoint, bounded PreMerge and DeepValidation tiers while preserving every test and assertion.

**Architecture:** Existing `Category=FullValidation` tests and the exhaustive Guardian regression matrix form the explicit DeepValidation tier. PreMerge keeps all Fast tests, ordinary Integration tests, reviewed sentinels, ProcessIntegration, and E2E under one fifteen-minute deadline. Source-category guards and runner PlanOnly evidence prove that the two tiers are disjoint and that their union covers the full Integration discovery.

**Tech Stack:** .NET 8, C# 12, xUnit, Roslyn syntax APIs, PowerShell 7, VSTest filters, TRX, Git, GitHub CLI, Serena CLI.

## Global Constraints

- Work only in `E:\Games\The Book of Eternity Reborn-worktrees\1505-test-suite-performance`.
- GitHub issue `#1505` owns every commit.
- Do not delete, weaken, skip, or rewrite an assertion to make a lane faster.
- Do not increase Fast above five minutes or PreMerge/DeepValidation above fifteen minutes.
- Do not increase test-process parallelism above four or Fast parallelism above two.
- Do not run the legacy unbounded full suite.
- Do not serially run every diagnostic lane.
- `Complete` remains byte-for-byte plan-equivalent to `PreMerge`.
- DeepValidation is explicit and is not an ordinary post-edit or every-merge command.
- ProcessIntegration and E2E remain exclusive PreMerge phases.
- All process cleanup is exact-owned-tree cleanup; broad process enumeration or termination is forbidden.
- Production solution membership, gameplay behavior, production timeouts, and assertions must not change.
- `.serena/`, `bin/`, `obj/`, TestResults, TRX, logs, and SDD evidence remain untracked/unstaged.
- Preserve the six dirty Spec Kit files and untracked `docs/testing.md` until the documentation task stages them explicitly.

## File Structure

- `BookOfEternityClient.IntegrationTests/FullValidationEquivalenceTests.cs`
  - Moves the full-versus-explicit-all equivalence sentinel into PreMerge.
- `BookOfEternityClient.IntegrationTests/GuardianTradeServiceTests.cs`
  - Supplies the representative Guardian production-behavior sentinel.
- `BookOfEternityClient.IntegrationTests/MortalCommandDisplaySaveTests.cs`
  - Keeps archive/loadability smoke in PreMerge and the exhaustive render matrix in DeepValidation.
- `BookOfEternityClient.IntegrationTests/ChaosSeaCommandDisplaySaveTests.cs`
  - Keeps two archive/live-baseline smoke Facts in PreMerge and render matrices in DeepValidation.
- `BookOfEternityClient.IntegrationTests/ShiningAbodeCommandDisplaySaveTests.cs`
  - Keeps archive/loadability smoke in PreMerge and render matrices in DeepValidation.
- `BookOfEternityClient.IntegrationTests/GuardianSystemRegressionTests.cs`
  - Adds the deep category to the existing exhaustive Guardian regression partial class.
- `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`
  - Owns reviewed category manifests, method-level sentinel/deep guards, runner filter guards, and numeric partition floors.
- `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`
  - Guards the public runner interface and bounded lane set.
- `scripts/test-csharp.ps1`
  - Adds the DeepValidation lane, disjoint filters, fixed floors, balanced execution, and unchanged cleanup/deadline semantics.
- `docs/testing.md`
  - Documents ordinary versus diagnostic commands and measured accepted timings.
- `specs/1505-test-suite-performance/*.md`
  - Reconciles Spec Kit requirements, architecture, tasks, and measured evidence.

---

### Task 15: Classify Deep Tests and Preserve PreMerge Sentinels

**Files:**

- Modify: `BookOfEternityClient.IntegrationTests/FullValidationEquivalenceTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/GuardianTradeServiceTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalCommandDisplaySaveTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ChaosSeaCommandDisplaySaveTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ShiningAbodeCommandDisplaySaveTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/GuardianSystemRegressionTests.cs`
- Test: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`

**Interfaces:**

- Consumes: existing xUnit `Trait("Category", value)` metadata.
- Produces:
  - `Category=PreMergeSentinel` on exactly the reviewed lightweight sentinel tests;
  - `Category=DeepValidation` on `GuardianSystemRegressionTests`;
  - method-level `FullValidation` on exhaustive command-display theories;
  - a static partition of Integration discovery:
    - Core/PreMerge Integration: 1,637;
    - DeepValidation: 1,950;
    - ProcessIntegration excluding E2E: 440;
    - E2E: 15;
    - total: 4,042.

- [ ] **Step 1: Add failing reviewed-category guards**

In `IntegrationTestBoundaryTests.cs`, add constants:

```csharp
private const string DeepValidationTrait =
    "[Trait(\"Category\", \"DeepValidation\")]";
private const string PreMergeSentinelTrait =
    "[Trait(\"Category\", \"PreMergeSentinel\")]";
```

Change the RegressionIntegration manifest from a flat filename array to an
exact dictionary. `GuardianSystemRegressionTests.cs` must carry both traits;
all other existing regression sources retain only RegressionIntegration:

```csharp
private static readonly IReadOnlyDictionary<string, string[]>
    RegressionIntegrationCategories =
        RegressionIntegrationSources.ToDictionary(
            fileName => fileName,
            fileName => string.Equals(
                fileName,
                "GuardianSystemRegressionTests.cs",
                StringComparison.Ordinal)
                    ? new[] { RegressionIntegrationTrait, DeepValidationTrait }
                    : new[] { RegressionIntegrationTrait },
            StringComparer.Ordinal);
```

Extend the existing exact-category test rather than adding a new test case:

```csharp
AssertExactCategoryManifest(
    RegressionIntegrationCategories,
    [RegressionIntegrationTrait, DeepValidationTrait],
    "RegressionIntegration/DeepValidation");
```

Add an exact source/method manifest helper using Roslyn. It must return only
`Trait("Category", "...")` values from a class or method:

```csharp
private static string[] CategoryTraits(
    string fileName,
    string? methodName = null)
{
    var source = File.ReadAllText(SourcePath(IntegrationTestsDirectory, fileName));
    var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
    MemberDeclarationSyntax node = methodName is null
        ? Assert.Single(root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        : Assert.Single(root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText == methodName));

    return node.AttributeLists
        .SelectMany(list => list.Attributes)
        .Where(attribute =>
            attribute.Name.ToString() is "Trait" or "TraitAttribute")
        .Select(attribute => attribute.ArgumentList?.Arguments
            .Select(argument => argument.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Select(literal => literal.Token.ValueText)
            .ToArray() ?? [])
        .Where(arguments =>
            arguments.Length == 2 &&
            arguments[0] == "Category")
        .Select(arguments => arguments[1])
        .Order(StringComparer.Ordinal)
        .ToArray();
}
```

Within the existing category-manifest test, assert:

```csharp
Assert.Equal(
    ["PreMergeSentinel"],
    CategoryTraits("FullValidationEquivalenceTests.cs"));
Assert.Equal(
    ["PreMergeSentinel"],
    CategoryTraits("GuardianTradeServiceTests.cs"));
Assert.Equal(
    ["DeepValidation", "RegressionIntegration"],
    CategoryTraits("GuardianSystemRegressionTests.cs"));

var commandDisplayCategories =
    new Dictionary<string, IReadOnlyDictionary<string, string[]>>(StringComparer.Ordinal)
    {
        ["MortalCommandDisplaySaveTests.cs"] =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["NamedMortalCommandDisplaySave_IsDiscoverableLoadableValidAndRepeatable"] =
                    ["PreMergeSentinel"],
                ["LoadedMortalCommandDisplaySave_RendersCoveredCommandInBrowserAndConsole"] =
                    ["FullValidation"],
                ["LoadedMortalCommandDisplaySave_WorldNewsLocalizesVisibilityEnums"] =
                    ["FullValidation"]
            },
        ["ChaosSeaCommandDisplaySaveTests.cs"] =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["NamedChaosSeaCommandDisplaySave_IsDiscoverableLoadableValidAndRepeatable"] =
                    ["PreMergeSentinel"],
                ["NamedChaosSeaCommandDisplaySave_HasCleanAcceptedTurnBaselineForLiveE2E"] =
                    ["PreMergeSentinel"],
                ["LoadedChaosSeaCommandDisplaySave_RendersAvailableCommandInBrowserAndConsole"] =
                    ["FullValidation"],
                ["LoadedChaosSeaCommandDisplaySave_RendersRepresentativeDetailTargets"] =
                    ["FullValidation"]
            },
        ["ShiningAbodeCommandDisplaySaveTests.cs"] =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["NamedShiningAbodeCommandDisplaySave_IsDiscoverableLoadableValidAndRepeatable"] =
                    ["PreMergeSentinel"],
                ["LoadedShiningAbodeCommandDisplaySave_RendersAvailableCommandInBrowserAndConsole"] =
                    ["FullValidation"],
                ["LoadedShiningAbodeCommandDisplaySave_RendersRepresentativeDetailTargets"] =
                    ["FullValidation"]
            }
    };

foreach (var (fileName, methodManifest) in commandDisplayCategories)
{
    Assert.Empty(CategoryTraits(fileName));
    foreach (var (methodName, categories) in methodManifest)
        Assert.Equal(categories, CategoryTraits(fileName, methodName));
}
```

Permit the one reviewed broad equivalence sentinel in the existing broad-call
guard, while still requiring its exact sentinel trait:

```csharp
private static readonly string[] BroadValidationPreMergeSentinelSources =
[
    "FullValidationEquivalenceTests.cs"
];
```

Use the manifest in the existing broad FullValidation query:

```csharp
var broadSentinelPaths = BroadValidationPreMergeSentinelSources
    .Select(fileName => SourcePath(IntegrationTestsDirectory, fileName))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

var uncategorized = EnumerateIntegrationAndSupportSources()
    .Where(candidate => !string.Equals(
        candidate.Path,
        supportHarnessPath,
        StringComparison.OrdinalIgnoreCase))
    .Where(candidate => !broadSentinelPaths.Contains(candidate.Path))
    .Where(candidate => broadCall.IsMatch(candidate.Source))
    .Where(candidate => !candidate.Source.Contains(
        FullValidationTrait,
        StringComparison.Ordinal))
    .Select(candidate => Path.GetRelativePath(TestRepoPaths.RepoRoot, candidate.Path))
    .Order(StringComparer.Ordinal)
    .ToArray();

Assert.All(BroadValidationPreMergeSentinelSources, fileName =>
    Assert.Contains(
        PreMergeSentinelTrait,
        File.ReadAllText(SourcePath(IntegrationTestsDirectory, fileName)),
        StringComparison.Ordinal));
```

- [ ] **Step 2: Run the focused guard and verify RED**

Run:

```powershell
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj `
  --no-restore `
  --filter "FullyQualifiedName~IntegrationTestBoundaryTests.FileBackedRegressionIntegrationSources_MatchReviewedManifest|FullyQualifiedName~IntegrationTestBoundaryTests.IntegrationAndSupportBroadValidationSources_AreExplicitlyCategorized" `
  --verbosity minimal
```

Expected: exit 1. Failures must name missing `DeepValidation`,
`PreMergeSentinel`, or method-level category assignments. No unrelated test may
fail.

- [ ] **Step 3: Apply the minimal category changes**

Use exactly these category assignments:

```csharp
// FullValidationEquivalenceTests.cs
[Trait("Category", "PreMergeSentinel")]
public sealed class FullValidationEquivalenceTests : IDisposable

// GuardianTradeServiceTests.cs
[Trait("Category", "PreMergeSentinel")]
public sealed class GuardianTradeServiceTests : IDisposable

// GuardianSystemRegressionTests.cs
[Trait("Category", "RegressionIntegration")]
[Trait("Category", "DeepValidation")]
public sealed partial class GuardianSystemRegressionTests : IDisposable
```

For each command-display class, remove the class-level FullValidation trait.
Apply `PreMergeSentinel` only to the named lightweight Facts and
`FullValidation` only to the named exhaustive theory methods from Step 1.
Do not modify method bodies, data providers, assertions, or fixtures.

- [ ] **Step 4: Run the focused guard and verify GREEN**

Run the same command as Step 2.

Expected: exit 0, both focused guards pass, zero warnings.

- [ ] **Step 5: Prove the discovery partition**

Run `--list-tests --no-build --no-restore` for these filters:

```powershell
Category!=FullValidation&Category!=DeepValidation&Category!=ProcessIntegration&Category!=E2E
(Category=FullValidation|Category=DeepValidation)&Category!=ProcessIntegration&Category!=E2E
Category=ProcessIntegration&Category!=E2E
Category=E2E
```

Expected discovery counts:

```text
Core Integration: 1637
DeepValidation: 1950
ProcessIntegration excluding E2E: 440
E2E: 15
Sum: 4042
All Integration discovery: 4042
```

The sum must equal the unfiltered Integration discovery exactly.

- [ ] **Step 6: Commit**

Stage only the seven files listed in this task and commit:

```powershell
git commit -m "test: classify bounded validation tiers (#1505)"
```

---

### Task 16: Add the DeepValidation Runner Lane and Disjoint Floors

**Files:**

- Modify: `scripts/test-csharp.ps1`
- Test: `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`
- Test: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`

**Interfaces:**

- Consumes: Task 15 category partition and counts.
- Produces:
  - public `-Lane DeepValidation`;
  - PreMerge Integration filter excluding FullValidation and DeepValidation;
  - DeepValidation filter selecting their union;
  - PreMerge planned floor 4,666;
  - DeepValidation planned floor 1,950;
  - unchanged Complete→PreMerge alias;
  - unchanged 5/15-minute limits and 4/2 process caps.

- [ ] **Step 1: Extend boundary expectations first**

In both boundary files, add `"DeepValidation"` to the reviewed lane list.

In `IntegrationTestBoundaryTests.CSharpLaneRunner_DefinesNonOverlappingProjectRoutedPreMergeSchedule`,
require these normalized definitions/tokens:

```csharp
"DeepValidation = @{ Project = \"Integration\" " +
"Filter = $deepValidationFilter TimeoutMinutes = 15 }"

"$coreIntegrationFilter = " +
"\"Category!=FullValidation&Category!=DeepValidation&" +
"Category!=ProcessIntegration&Category!=E2E\""

"$deepValidationFilter = " +
"\"(Category=FullValidation|Category=DeepValidation)&" +
"Category!=ProcessIntegration&Category!=E2E\""

"$PreMergeMinimumCases = 4666"
"$DeepValidationMinimumCases = 1950"
```

Require that duplicate detection applies to both composed lanes and that
`Complete` still aliases only PreMerge. Replace the obsolete `6560` token with
the two exact floors.

In `FastTestBoundaryTests`, require `DeepValidation` in the public lane set and
keep the default Fast project/filter assertions unchanged.

- [ ] **Step 2: Run boundary tests and verify RED**

Run sequentially:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj `
  --no-restore `
  --filter "FullyQualifiedName~FastTestBoundaryTests" `
  --verbosity minimal

dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj `
  --no-restore `
  --filter "FullyQualifiedName~IntegrationTestBoundaryTests.CSharpLaneRunner_DefinesNonOverlappingProjectRoutedPreMergeSchedule" `
  --verbosity minimal
```

Expected: both commands exit 1 only because DeepValidation/filter/floor tokens
are absent from the old runner.

- [ ] **Step 3: Add lane constants and definition**

In `scripts/test-csharp.ps1`, add `"DeepValidation"` to the lane ValidateSet.
Add:

```powershell
$PreMergeMinimumCases = 4666
$DeepValidationMinimumCases = 1950
$coreIntegrationFilter =
    "Category!=FullValidation&Category!=DeepValidation&" +
    "Category!=ProcessIntegration&Category!=E2E"
$deepValidationFilter =
    "(Category=FullValidation|Category=DeepValidation)&" +
    "Category!=ProcessIntegration&Category!=E2E"
```

Add the lane definition:

```powershell
DeepValidation = @{
    Project = "Integration"
    Filter = $deepValidationFilter
    TimeoutMinutes = 15
}
```

Do not change the existing FullValidation or RegressionIntegration diagnostic
definitions.

- [ ] **Step 4: Route PreMerge and DeepValidation**

Replace the PreMerge Integration parallel filter with
`$coreIntegrationFilter`.

Include DeepValidation in balanced descriptor generation and four-process
execution:

```powershell
$balanced = $effectiveLane -in @(
    "Fast",
    "FullValidation",
    "RegressionIntegration",
    "DeepValidation"
)
```

and:

```powershell
elseif ($effectiveLane -in @(
    "FullValidation",
    "RegressionIntegration",
    "DeepValidation"
)) {
    $Parallelism
}
```

DeepValidation uses only the Integration build, does not run frontend
verification, and does not enter the ProcessIntegration/E2E exclusive loop.

- [ ] **Step 5: Apply composed-lane result guards**

After TRX parsing:

```powershell
$isComposedCoverageLane = $effectiveLane -in @(
    "PreMerge",
    "DeepValidation"
)
if ($isComposedCoverageLane -and $runSummary.DuplicateTests.Count -ne 0) {
    $exitCode = 1
    throw "$effectiveLane produced duplicate TRX test IDs: " +
        "$($runSummary.DuplicateTests -join ', ')"
}

$minimumCases = switch ($effectiveLane) {
    "PreMerge" { $PreMergeMinimumCases }
    "DeepValidation" { $DeepValidationMinimumCases }
    default { 0 }
}
if ($minimumCases -gt 0 -and $runSummary.Total -lt $minimumCases) {
    $exitCode = 1
    throw "$effectiveLane produced $($runSummary.Total) cases; " +
        "expected at least the $minimumCases-case reviewed baseline."
}
```

Do not change TRX theory-row deduplication, missing-storage failure behavior,
deadline assignment, self-test parameter sets, or owned-process cleanup.

- [ ] **Step 6: Run parser and focused GREEN controls**

Run:

```powershell
$errors = $null
[void][System.Management.Automation.Language.Parser]::ParseFile(
  (Resolve-Path scripts/test-csharp.ps1),
  [ref]$null,
  [ref]$errors)
if ($errors.Count -ne 0) { throw ($errors -join [Environment]::NewLine) }

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj `
  --no-restore `
  --filter "FullyQualifiedName~FastTestBoundaryTests" `
  --verbosity minimal

dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj `
  --no-restore `
  --filter "FullyQualifiedName~IntegrationTestBoundaryTests" `
  --verbosity minimal
```

Expected: parser has zero errors; all Fast and Integration boundary tests pass
with zero warnings.

- [ ] **Step 7: Verify PlanOnly semantics**

Run:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane DeepValidation -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane PreMerge -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane Complete -PlanOnly
```

Parse every `PLAN {json}` line. Expected:

```text
DeepValidation:
  Integration project only
  planned cases = 1950
  every filter includes FullValidation or DeepValidation
  no ProcessIntegration or E2E selection

PreMerge:
  planned cases = 4666
  Fast planned discovery = 2574
  Core Integration planned discovery = 1637
  ProcessIntegration planned discovery = 440
  E2E planned discovery = 15
  no FullValidation or DeepValidation selection in the core Integration rows
  exactly one ProcessIntegration row
  exactly one E2E row

Complete:
  normalized PLAN rows byte-for-byte equal PreMerge
```

Across each lane, descriptor names and selection tokens are unique.

- [ ] **Step 8: Verify negative hard caps**

Run:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 `
  -Lane DeepValidation -TimeoutMinutes 16 -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 `
  -Lane PreMerge -TimeoutMinutes 16 -PlanOnly
```

Expected: each exits 1 in under one second, reports requested limit above the
fifteen-minute hard cap, and launches no build/frontend/test child.

- [ ] **Step 9: Commit**

Stage exactly the runner and two boundary files. Commit:

```powershell
git commit -m "test: add bounded deep validation lane (#1505)"
```

---

### Task 17: Verify Both Tiers and Reconcile Documentation

**Files:**

- Modify: `docs/testing.md`
- Modify: `specs/1505-test-suite-performance/data-model.md`
- Modify: `specs/1505-test-suite-performance/plan.md`
- Modify: `specs/1505-test-suite-performance/quickstart.md`
- Modify: `specs/1505-test-suite-performance/research.md`
- Modify: `specs/1505-test-suite-performance/spec.md`
- Modify: `specs/1505-test-suite-performance/tasks.md`
- Verify: all production/test/runner changes.

**Interfaces:**

- Consumes: Task 15 partition and Task 16 runner.
- Produces: fresh accepted build/Fast/DeepValidation/PreMerge evidence and the
  final documented workflow.

- [ ] **Step 1: Reconcile command documentation before measurements**

Document exactly:

```powershell
.\scripts\test-csharp.ps1
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"
.\scripts\test-csharp.ps1 -Lane FullValidation
.\scripts\test-csharp.ps1 -Lane RegressionIntegration
.\scripts\test-csharp.ps1 -Lane ProcessIntegration
.\scripts\test-csharp.ps1 -Lane E2E
.\scripts\test-csharp.ps1 -Lane DeepValidation
.\scripts\test-csharp.ps1 -Lane PreMerge
```

State:

- default Fast is the ordinary post-edit control;
- final merge verification is two Fast runs plus one PreMerge;
- DeepValidation is conditional and explicit;
- this branch runs DeepValidation once because it changes the category
  boundary;
- Complete is a temporary PreMerge alias;
- do not serially run all diagnostic lanes.

Do not add a measurement table until Steps 3–5 produce fresh summaries.

- [ ] **Step 2: Build sequentially**

Run:

```powershell
dotnet build BookOfEternityClient/BookOfEternityClient.sln `
  --no-restore --verbosity minimal
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj `
  --no-restore --verbosity minimal
dotnet build BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj `
  --no-restore --verbosity minimal
```

Expected: all exit 0, warnings 0, errors 0.

- [ ] **Step 3: Run Fast twice**

Run the exact command twice, sequentially:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane Fast
```

Expected for each:

- exit 0;
- wall below five minutes;
- all discovered tests pass;
- duplicates 0;
- timeout false;
- owned-tree cleanup true.

Record exact external wall, runner duration, result count, and summary path.

- [ ] **Step 4: Run DeepValidation once**

Run:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane DeepValidation
```

Expected:

- exit 0;
- wall below fifteen minutes;
- at least 1,950 results;
- failures 0;
- duplicate IDs 0;
- timeout false;
- owned-tree cleanup true.

If it fails, do not rerun blindly. Inspect the exact summary/TRX/descriptor
log, run only one smallest focused diagnostic, and return to the relevant task
with a new RED/GREEN fix.

- [ ] **Step 5: Run PreMerge once**

Run:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane PreMerge
```

Expected:

- exit 0;
- wall below fifteen minutes, preferably below ten;
- at least 4,666 results;
- failures 0;
- duplicate IDs 0;
- timeout false;
- owned-tree cleanup true;
- ProcessIntegration and E2E both completed.

Do not follow a green PreMerge with serial diagnostic lanes.

- [ ] **Step 6: Fill measured evidence**

Add the measurement section to the seven documentation/spec files using exact
values from the two Fast summaries, DeepValidation summary, and PreMerge
summary.

Record the rejected all-inclusive evidence too:

```text
15:00.393, exit 124, 4,738/4,738 completed tests passed,
0 duplicates, cleanup succeeded, projected lower bound 25:37.741.
```

Explain that the failure proved a capacity limit and motivated the approved
two-tier design; do not describe it as a correctness failure.

- [ ] **Step 7: Re-index and health-check Serena**

From the worktree root:

```powershell
serena project index
serena project health-check
```

Expected: both exit 0 and health-check is green. Do not modify Serena’s
installed source. Keep `.serena/` untracked.

- [ ] **Step 8: Run final static acceptance checks**

Run:

```powershell
git diff --check
git diff -- BookOfEternityClient/BookOfEternityClient.sln
git status --short
```

Also verify:

- TestSupport has no Test SDK or xUnit dependency;
- IntegrationTests does not reference Tests;
- Fast cannot discover Integration tests;
- the Integration partition remains 1,637 + 1,950 + 440 + 15 = 4,042;
- PreMerge/Deep filters are disjoint;
- all named sentinels are PreMerge-only;
- runner has exactly one deadline assignment;
- broad full-validation call budget remains at most eight;
- no generated artifact is staged.

- [ ] **Step 9: Commit documentation**

Stage exactly:

```text
docs/testing.md
specs/1505-test-suite-performance/data-model.md
specs/1505-test-suite-performance/plan.md
specs/1505-test-suite-performance/quickstart.md
specs/1505-test-suite-performance/research.md
specs/1505-test-suite-performance/spec.md
specs/1505-test-suite-performance/tasks.md
```

Commit:

```powershell
git commit -m "docs: record bounded validation workflow (#1505)"
```

---

### Task 18: Review, Integrate, and Verify Main

**Files:**

- Review: full branch range `e9099980..HEAD`.
- External state: GitHub PR and issue `#1505`.
- Local main: `E:\Games\The Book of Eternity Reborn`.

**Interfaces:**

- Consumes: all accepted commits and Task 17 evidence.
- Produces: independently reviewed merged PR, closed issue, synchronized local
  and remote main, and green Serena health-check on local main.

- [ ] **Step 1: Run complete branch review**

Inspect:

```powershell
git diff e9099980..HEAD --stat
git diff e9099980..HEAD -- BookOfEternityClient
git diff e9099980..HEAD -- `
  BookOfEternityClient.Tests `
  BookOfEternityClient.IntegrationTests `
  BookOfEternityClient.TestSupport
git diff e9099980..HEAD -- scripts docs specs
git log --oneline e9099980..HEAD
```

Reject any Critical or Important finding involving:

- gameplay semantics;
- production timeout changes;
- assertion weakening;
- reverse test-project references;
- overlapping tier filters;
- a missing sentinel;
- an unowned process cleanup path;
- committed generated artifacts;
- production solution membership.

- [ ] **Step 2: Verify clean publication scope**

Run:

```powershell
git diff --check
git status --short
git ls-files .serena TestResults `
  BookOfEternityClient.Tests/bin `
  BookOfEternityClient.Tests/obj `
  BookOfEternityClient.IntegrationTests/bin `
  BookOfEternityClient.IntegrationTests/obj
```

Expected: no whitespace errors; only intentional local generated/untracked
directories remain; `git ls-files` prints nothing for generated paths.

- [ ] **Step 3: Push and create the issue-linked PR**

Run:

```powershell
git push -u origin work/1505-test-suite-performance
$prUrl = gh pr create `
  --base main `
  --head work/1505-test-suite-performance `
  --title "test: isolate and bound C# verification lanes" `
  --body "Closes #1505`n`n- keeps Fast below five minutes`n- separates exhaustive DeepValidation from the bounded merge gate`n- keeps PreMerge under one global fifteen-minute deadline"
```

- [ ] **Step 4: Merge and verify GitHub state**

Run:

```powershell
gh pr merge $prUrl --merge --delete-branch=false
gh pr view $prUrl --json state,mergeCommit,url
gh issue view 1505 --json state,url
```

Expected: PR state `MERGED`; issue state `CLOSED`.

- [ ] **Step 5: Synchronize local main**

Run:

```powershell
git -C "E:\Games\The Book of Eternity Reborn" pull --ff-only
$localMain = git -C "E:\Games\The Book of Eternity Reborn" rev-parse main
$remoteMain = git -C "E:\Games\The Book of Eternity Reborn" rev-parse origin/main
if ($localMain -ne $remoteMain) {
    throw "Local main does not match origin/main after merge."
}
```

- [ ] **Step 6: Re-index Serena on merged local main**

From `E:\Games\The Book of Eternity Reborn`:

```powershell
serena project index
serena project health-check
```

Expected: both exit 0, health-check green, and review-worktree directories are
not indexed as part of the main project.

Do not remove the externally located worktree automatically.
