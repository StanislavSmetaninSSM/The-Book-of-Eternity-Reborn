# Game Engine Lifecycle Lane Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep routine PreMerge useful and bounded by replacing exhaustive
GameEngine lifecycle coverage with ten fast sentinels, while preserving all
186 lifecycle cases in a dedicated ten-minute lane.

**Architecture:** Reclassify `GameEngineTurnLifecycleTests` under
`LifecycleIntegration`, mark an exact ten-method PreMerge sentinel manifest,
and teach the PowerShell runner to exclude the exhaustive category from its
core selection except for those sentinels. The dedicated lane uses one
descriptor and external parallelism one; obsolete cross-descriptor
`SerialGroup` scheduling is removed.

**Tech Stack:** C# 12, .NET 8, xUnit 2.9.2, PowerShell 7,
Microsoft.NET.Test.Sdk 17.11.1.

## Global Constraints

- Source task is GitHub issue
  [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505).
- Work only on branch `work/1505-test-suite-performance` in its existing
  linked worktree.
- Do not modify production code, gameplay behavior, validation behavior,
  prompts, examples, schemas, console/browser behavior, or frontend behavior.
- Do not modify the six pre-existing dirty `specs/1505-test-suite-performance`
  files, untracked `docs/testing.md`, `.serena/`, or generated `bin`, `obj`,
  and unrelated `TestResults` artifacts.
- `Fast` remains the default lane with a five-minute cap.
- `PreMerge` retains one global fifteen-minute deadline, target below ten
  minutes, ProcessIntegration then E2E as exclusive terminal phases, and a
  reviewed minimum of exactly `4490` cases.
- `LifecycleIntegration` has a hard ten-minute cap, exactly one descriptor,
  effective external parallelism one, and a reviewed minimum of exactly `186`
  cases.
- `DeepValidation` retains its exact filter membership and minimum of `1950`.
- `Complete` remains a byte-equivalent plan alias for `PreMerge`.
- No full Fast, DeepValidation, LifecycleIntegration, PreMerge, Complete, or
  diagnostic lane is run during implementation. Only focused tests and
  PlanOnly are allowed before independent review.
- Preserve xUnit
  `[CollectionDefinition(CollectionName, DisableParallelization = true)]` and
  `[Collection(GameEngineTurnLifecycleCollection.CollectionName)]`.
- Use ordinal comparisons for category manifests, method names, serializable
  plan data, and duplicate detection.

---

### Task 1: Separate exhaustive GameEngine lifecycle verification

**Files:**

- Modify:
  `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`
- Modify:
  `BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs`
- Modify: `scripts/test-csharp.ps1`
- Reference:
  `docs/superpowers/specs/2026-08-01-game-engine-lifecycle-lane-design.md`

**Interfaces:**

- Consumes:
  - existing xUnit `Trait("Category", value)` metadata;
  - existing `PreMergeSentinel` convention;
  - existing project-routed runner discovery, PlanOnly, owned-process, TRX,
    deadline, and cleanup behavior.
- Produces:
  - `Category=LifecycleIntegration` on the lifecycle class;
  - `Category=PreMergeSentinel` on exactly ten named methods;
  - `LifecycleIntegration` runner lane with filter
    `$lifecycleIntegrationFilter`, ten-minute cap, single descriptor, and
    minimum `186`;
  - routine PreMerge lifecycle exclusion with a sentinel exception;
  - `PreMergeMinimumCases = 4490`;
  - no `SerialGroup` or `$externallySerializedClasses` runner surface.

- [ ] **Step 1: Add the exact category-manifest boundary in RED**

In `IntegrationTestBoundaryTests.cs`, add:

```csharp
private const string LifecycleIntegrationTrait =
    "[Trait(\"Category\", \"LifecycleIntegration\")]";

private static readonly IReadOnlyDictionary<string, string[]>
    GameEngineLifecycleSentinelCategories =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["CheckLevelUpAsync_DoesNotAwardAlreadyProcessedLevelAfterEngineRestart"] =
                ["PreMergeSentinel"],
            ["CollectAcceptedTurnRawStateIssuesAsync_DirectNpcCoreMutation_IsRejectedBeforeNormalization"] =
                ["PreMergeSentinel"],
            ["RebindRuntimeAfterSessionReplacement_ActiveReplacementRebindsLoopAndClearsTransientState"] =
                ["PreMergeSentinel"],
            ["WriteValidationRepairRequestAsync_GuardianScopeErrors_AddsConcreteHarnessRepairPacket"] =
                ["PreMergeSentinel"],
            ["ProcessPlayerTurn_UnresolvedRealm_DoesNotCreatePendingDiceState"] =
                ["PreMergeSentinel"],
            ["CleanupAcceptedTurnTerminalArtifactsAsync_WithoutIncarnationTrigger_RemovesTerminalContext"] =
                ["PreMergeSentinel"],
            ["ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_ValidActiveManifest_Authorizes"] =
                ["PreMergeSentinel"],
            ["TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_ResetsEnlightenmentAndPreservesInkFeathers"] =
                ["PreMergeSentinel"],
            ["CreateCanonicalBaselineSnapshotAsync_PreservesAndHashesExactSnapshotBytes"] =
                ["PreMergeSentinel"],
            ["RestorePreTurnBackup_BrowserDirectGachaPreservesExactPreSpendSoulBytes"] =
                ["PreMergeSentinel"]
        };
```

Remove `GameEngineTurnLifecycleTests.cs` from
`RegressionIntegrationSources`. Extend
`FileBackedRegressionIntegrationSources_MatchReviewedManifest` with this exact
contract:

```csharp
AssertExactCategoryManifest(
    new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["GameEngineTurnLifecycleTests.cs"] = [LifecycleIntegrationTrait]
    },
    [LifecycleIntegrationTrait],
    "LifecycleIntegration");

Assert.Equal(
    ["LifecycleIntegration"],
    CategoryTraits("GameEngineTurnLifecycleTests.cs"));

var actualLifecycleMethodCategories =
    MethodCategoryTraits("GameEngineTurnLifecycleTests.cs");
Assert.Equal(
    GameEngineLifecycleSentinelCategories.Keys.Order(StringComparer.Ordinal),
    actualLifecycleMethodCategories.Keys.Order(StringComparer.Ordinal));
foreach (var (methodName, categories) in GameEngineLifecycleSentinelCategories)
{
    Assert.Equal(categories, actualLifecycleMethodCategories[methodName]);
}
```

Refactor the existing category parser without changing its behavior:

```csharp
private static IReadOnlyDictionary<string, string[]> MethodCategoryTraits(
    string fileName)
{
    var source = File.ReadAllText(SourcePath(IntegrationTestsDirectory, fileName));
    var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();

    return root.DescendantNodes()
        .OfType<MethodDeclarationSyntax>()
        .Select(method => (
            Name: method.Identifier.ValueText,
            Traits: CategoryTraits(method)))
        .Where(method => method.Traits.Length > 0)
        .ToDictionary(
            method => method.Name,
            method => method.Traits,
            StringComparer.Ordinal);
}

private static string[] CategoryTraits(
    string fileName,
    string? methodName = null)
{
    var source = File.ReadAllText(SourcePath(IntegrationTestsDirectory, fileName));
    var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
    MemberDeclarationSyntax node = methodName is null
        ? Assert.Single(
            root.DescendantNodes().OfType<ClassDeclarationSyntax>(),
            declaration => declaration.Identifier.ValueText ==
                Path.GetFileNameWithoutExtension(fileName))
        : Assert.Single(
            root.DescendantNodes().OfType<MethodDeclarationSyntax>(),
            method => method.Identifier.ValueText == methodName);

    return CategoryTraits(node);
}

private static string[] CategoryTraits(MemberDeclarationSyntax node)
{
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

- [ ] **Step 2: Run the category boundary and verify RED**

Run:

```powershell
dotnet test BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj `
  --no-restore `
  --filter "FullyQualifiedName~IntegrationTestBoundaryTests.FileBackedRegressionIntegrationSources_MatchReviewedManifest" `
  --verbosity minimal
```

Expected: exit `1`. The failure must report that
`GameEngineTurnLifecycleTests.cs` still has `RegressionIntegration`, is missing
`LifecycleIntegration`, or does not have the exact sentinel-method manifest.
No compilation error is accepted as RED.

- [ ] **Step 3: Apply the minimal lifecycle categories**

In `GameEngineTurnLifecycleTests.cs`, replace:

```csharp
[Trait("Category", "RegressionIntegration")]
```

with:

```csharp
[Trait("Category", "LifecycleIntegration")]
```

Add this method-level attribute immediately after `[Fact]` on exactly the ten
methods from Step 1:

```csharp
[Trait("Category", "PreMergeSentinel")]
```

Do not change a method body, assertion, timeout, fixture, collection,
constructor, or cleanup path.

- [ ] **Step 4: Re-run the category boundary and verify GREEN**

Run the exact Step 2 command.

Expected: exit `0`, `1/1` passed.

- [ ] **Step 5: Replace the obsolete serialization boundary with a RED lane boundary**

Replace
`CSharpLaneRunner_PreservesExternalSerializationForGameEngineTurnLifecycleTests`
with
`CSharpLaneRunner_SeparatesLifecycleIntegrationFromRoutinePreMerge`.
Retain its Roslyn assertions proving the collection definition has
`DisableParallelization = true` and the test class retains the matching
`Collection` attribute. Replace its runner assertions with:

```csharp
var runnerPath = Path.Combine(
    TestRepoPaths.RepoRoot,
    "scripts",
    "test-csharp.ps1");
var runnerSource = File.ReadAllText(runnerPath);
var normalizedRunner = Regex.Replace(
    runnerSource.Replace("`", ""),
    @"\s+",
    " ");

var requiredTokens = new[]
{
    "\"LifecycleIntegration\"",
    "$LifecycleIntegrationMinimumCases = 186",
    "$coreIntegrationFilter = " +
        "\"Category!=FullValidation&Category!=DeepValidation&\" + " +
        "\"Category!=ProcessIntegration&Category!=E2E&\" + " +
        "\"(Category!=LifecycleIntegration|Category=PreMergeSentinel)\"",
    "$lifecycleIntegrationFilter = " +
        "\"Category=LifecycleIntegration&\" + " +
        "\"Category!=ProcessIntegration&Category!=E2E\"",
    "LifecycleIntegration = @{ Project = \"Integration\" " +
        "Filter = $lifecycleIntegrationFilter TimeoutMinutes = 10 }",
    "\"LifecycleIntegration\" { $LifecycleIntegrationMinimumCases }",
    "elseif ($effectiveLane -eq \"LifecycleIntegration\") { 1 }"
};
Assert.All(
    requiredTokens,
    token => Assert.Contains(token, normalizedRunner, StringComparison.Ordinal));

Assert.DoesNotContain(
    "$externallySerializedClasses",
    runnerSource,
    StringComparison.Ordinal);
Assert.DoesNotContain(
    "SerialGroup",
    runnerSource,
    StringComparison.Ordinal);
```

Update
`CSharpLaneRunner_DefinesNonOverlappingProjectRoutedPreMergeSchedule` before
changing the runner:

```csharp
// Add to lanes:
"LifecycleIntegration",

// Add to diagnosticDefinitions:
"LifecycleIntegration = @{ Project = \"Integration\" " +
"Filter = $lifecycleIntegrationFilter TimeoutMinutes = 10 }",

// Replace normalized core/deep/floor tokens:
"$coreIntegrationFilter = " +
"\"Category!=FullValidation&Category!=DeepValidation&\" + " +
"\"Category!=ProcessIntegration&Category!=E2E&\" + " +
"\"(Category!=LifecycleIntegration|Category=PreMergeSentinel)\"",
"$deepValidationFilter = " +
"\"(Category=FullValidation|Category=DeepValidation)&\" + " +
"\"Category!=LifecycleIntegration&\" + " +
"\"Category!=ProcessIntegration&Category!=E2E\"",
"$lifecycleIntegrationFilter = " +
"\"Category=LifecycleIntegration&\" + " +
"\"Category!=ProcessIntegration&Category!=E2E\"",
"$PreMergeMinimumCases = 4490",
"$LifecycleIntegrationMinimumCases = 186",

// PlanOnly projection no longer exposes SerialGroup:
"Select-Object Phase, Name, Project, Filter, EstimatedCases, EstimatedCost",
```

Remove the old expectations for `$PreMergeMinimumCases = 4666`,
`$externallySerializedClasses`, `SerialGroup`, active serial groups, and
same-group scheduling.

- [ ] **Step 6: Run the two runner boundaries and verify RED**

Run:

```powershell
dotnet test BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj `
  --no-restore `
  --filter "FullyQualifiedName~IntegrationTestBoundaryTests.CSharpLaneRunner_SeparatesLifecycleIntegrationFromRoutinePreMerge|FullyQualifiedName~IntegrationTestBoundaryTests.CSharpLaneRunner_DefinesNonOverlappingProjectRoutedPreMergeSchedule" `
  --verbosity minimal
```

Expected: exit `1`, with missing `LifecycleIntegration`, wrong PreMerge floor,
or still-present `SerialGroup` as the intended failure. No compilation error is
accepted as RED.

- [ ] **Step 7: Implement the minimal runner partition**

In `scripts/test-csharp.ps1`:

1. Add `"LifecycleIntegration"` to the lane `ValidateSet`.
2. Set exact constants:

```powershell
$PreMergeMinimumCases = 4490
$DeepValidationMinimumCases = 1950
$LifecycleIntegrationMinimumCases = 186
```

3. Delete `$externallySerializedClasses` and its only GameEngine entry.
4. Replace the filters with:

```powershell
$coreIntegrationFilter =
    "Category!=FullValidation&Category!=DeepValidation&" +
    "Category!=ProcessIntegration&Category!=E2E&" +
    "(Category!=LifecycleIntegration|Category=PreMergeSentinel)"
$deepValidationFilter =
    "(Category=FullValidation|Category=DeepValidation)&" +
    "Category!=LifecycleIntegration&" +
    "Category!=ProcessIntegration&Category!=E2E"
$lifecycleIntegrationFilter =
    "Category=LifecycleIntegration&" +
    "Category!=ProcessIntegration&Category!=E2E"
```

5. Add the exact lane definition:

```powershell
LifecycleIntegration = @{
    Project = "Integration"
    Filter = $lifecycleIntegrationFilter
    TimeoutMinutes = 10
}
```

6. Remove the `SerialGroup` parameter and property from
   `New-RunDescriptor`.
7. In `New-SelectionRuns`, remove `$serialGroup`, the special full-class
   `EstimatedCost`, and `-SerialGroup`. Every method bin returns to:

```powershell
-EstimatedCases $bin.Weight `
-EstimatedCost $bin.Weight
```

8. In `Invoke-DescriptorBatch`, remove `$activeSerialGroups` construction and
   the serial-group eligibility conjunction. Preserve the Fast cap exactly:

```powershell
$descriptor = $pending |
    Where-Object {
        $MaximumFastParallelism -eq 0 -or
        -not (Test-SamePath $_.ProjectPath $fastTestProject) -or
        $activeFastCount -lt $MaximumFastParallelism
    } |
    Select-Object -First 1
```

9. Keep the balanced-lane set exactly:

```powershell
$balanced = $effectiveLane -in @(
    "Fast",
    "FullValidation",
    "RegressionIntegration",
    "DeepValidation"
)
```

`LifecycleIntegration` is intentionally absent so it produces one descriptor.

10. PlanOnly projects exactly:

```powershell
Select-Object Phase, Name, Project, Filter, EstimatedCases, EstimatedCost
```

11. Add the minimum-case switch arm:

```powershell
"LifecycleIntegration" { $LifecycleIntegrationMinimumCases }
```

12. Make single-process execution explicit:

```powershell
elseif ($effectiveLane -eq "LifecycleIntegration") {
    1
}
```

Do not change deadline creation, ProcessIntegration/E2E ordering, process
ownership, cleanup, TRX aggregation, duplicate detection, frontend verification,
or any other lane limit.

- [ ] **Step 8: Run focused runner boundaries and verify GREEN**

Run the exact Step 6 command.

Expected: exit `0`, `2/2` passed.

Then run:

```powershell
dotnet test BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj `
  --no-restore `
  --filter "FullyQualifiedName~IntegrationTestBoundaryTests" `
  --verbosity minimal

dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj `
  --no-restore `
  --filter "FullyQualifiedName~FastTestBoundaryTests" `
  --verbosity minimal
```

Expected: every selected boundary test passes with zero failures.

- [ ] **Step 9: Verify exact PlanOnly partition without executing a lane**

Run:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane PreMerge -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane LifecycleIntegration -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane DeepValidation -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane Complete -PlanOnly
```

Required results:

- PreMerge: `22` descriptors, current discovery `4503`, fixed floor `4490`;
- PreMerge contains exactly the ten reviewed GameEngine method FQNs and no
  other GameEngine method;
- PreMerge has no `93`- or `186`-case GameEngine descriptor;
- LifecycleIntegration: one descriptor, `186` discovered cases, ten-minute
  cap, no balancing or external concurrency;
- DeepValidation: unchanged `23` descriptors / `1950` planned cases and no
  GameEngine method;
- Complete raw `PLAN-BEGIN` through `PLAN-END` block is byte-identical to
  PreMerge after normalizing only `EffectiveLane=Complete` to
  `EffectiveLane=PreMerge`;
- no plan row, source token, or JSON property named `SerialGroup`.

If any exact count differs, stop and inspect discovery. Do not lower a floor or
change the sentinel manifest to make the check pass.

- [ ] **Step 10: Run the exact sentinel control**

Run:

```powershell
$sentinelMethods = @(
    "CheckLevelUpAsync_DoesNotAwardAlreadyProcessedLevelAfterEngineRestart",
    "CollectAcceptedTurnRawStateIssuesAsync_DirectNpcCoreMutation_IsRejectedBeforeNormalization",
    "RebindRuntimeAfterSessionReplacement_ActiveReplacementRebindsLoopAndClearsTransientState",
    "WriteValidationRepairRequestAsync_GuardianScopeErrors_AddsConcreteHarnessRepairPacket",
    "ProcessPlayerTurn_UnresolvedRealm_DoesNotCreatePendingDiceState",
    "CleanupAcceptedTurnTerminalArtifactsAsync_WithoutIncarnationTrigger_RemovesTerminalContext",
    "ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_ValidActiveManifest_Authorizes",
    "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_ResetsEnlightenmentAndPreservesInkFeathers",
    "CreateCanonicalBaselineSnapshotAsync_PreservesAndHashesExactSnapshotBytes",
    "RestorePreTurnBackup_BrowserDirectGachaPreservesExactPreSpendSoulBytes"
)
$sentinelFilter = ($sentinelMethods | ForEach-Object {
    "FullyQualifiedName=BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.$_"
}) -join "|"
dotnet test BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj `
  --no-restore `
  --no-build `
  --filter $sentinelFilter `
  --verbosity minimal
```

Expected: exit `0`, exactly `10/10` passed. The accepted pre-change control was
six seconds of test duration and `13.329` seconds external wall; investigate
instead of accepting a material regression.

- [ ] **Step 11: Run static acceptance**

Run:

```powershell
dotnet build BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj `
  --no-restore `
  --verbosity minimal

[scriptblock]::Create(
    (Get-Content -LiteralPath scripts/test-csharp.ps1 -Raw)
) | Out-Null

git diff --check
git status --short
```

Expected:

- build exit `0`, zero errors;
- PowerShell parser exit `0`;
- `git diff --check` emits nothing;
- only the three task files are newly modified; all pre-existing dirty files
  remain preserved and unstaged.

- [ ] **Step 12: Commit only the accepted implementation**

```powershell
git add -- `
  BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs `
  BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs `
  scripts/test-csharp.ps1
git diff --cached --check
git commit -m "test: separate lifecycle integration lane (#1505)"
```

Report:

- RED commands and their intended failures;
- GREEN commands and exact pass counts;
- all four PlanOnly descriptor/case counts;
- exact sentinel duration and count;
- commit SHA;
- unchanged pre-existing dirty state;
- any concern without running a heavy lane.
