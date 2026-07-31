# Isolated Fast and Integration Test Projects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the default C# test project finish within five minutes while preserving a complete, non-duplicated pre-merge control with one global fifteen-minute deadline.

**Architecture:** Keep unit and scoped-validation tests in `BookOfEternityClient.Tests`, physically move filesystem-heavy, process, full-validation, and end-to-end tests into `BookOfEternityClient.IntegrationTests`, and move cross-assembly fixture infrastructure into non-discoverable `BookOfEternityClient.TestSupport`. Route every local C# test command through `scripts/test-csharp.ps1`; `Fast` and `Focused` target only the fast assembly, diagnostic lanes target only the integration assembly, and `PreMerge` covers both assemblies without overlapping test filters.

**Tech Stack:** .NET 8, C# 12, xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1, PowerShell 7, VSTest filters/TRX, Git, GitHub CLI, Serena.

## Global Constraints

- Work is tracked by GitHub issue [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505).
- `BookOfEternityClient.Tests` has a target and hard limit of five minutes.
- `Focused` has a hard limit of five minutes.
- `PreMerge` has a target below ten minutes and one global hard limit of fifteen minutes.
- An individual diagnostic integration lane has a hard limit of fifteen minutes.
- The public parameterless `ValidationService.ValidateGameStateAsync()` remains the production all-phase contract.
- Scoped validation remains internal to production, `BookOfEternityClient.TestSupport`, and the two test assemblies.
- At most eight direct parameterless full-pipeline calls may exist across integration and test-support source; this plan targets exactly seven reviewed call sites.
- No production timeout is increased.
- No gameplay, GM contract, assertion, or player-facing behavior is weakened.
- Every partial test class is compiled by exactly one test project.
- `BookOfEternityClient.IntegrationTests` does not reference `BookOfEternityClient.Tests`.
- Neither test project is added to `BookOfEternityClient/BookOfEternityClient.sln`.
- Every bounded run writes TRX, a text log, a JSON summary, wall time, duplicate-test count, and owned-process cleanup status.
- `.serena/` remains local and is never staged.

---

## Starting Point

The worktree is `E:\Games\The Book of Eternity Reborn-worktrees\1505-test-suite-performance` on branch `work/1505-test-suite-performance`.

The approved design is committed as `02764ab9`. The worktree also contains intentional, uncommitted work that must be preserved:

- the `GameStateValidationPhase` and `GameStateValidationSelection` API;
- conditional dispatch of 26 validation phases plus the targeted rival/resident phase;
- reviewed Guardian profiles and a prepared Guardian fixture snapshot;
- scoped afterlife spiritual-conflict validation;
- test-lane traits, source guards, documentation, and `scripts/test-csharp.ps1`.

Measured evidence to preserve in the final documentation:

- original complete suite: 40–60 minutes;
- original Fast control: 2,580 tests in 2 minutes 35 seconds;
- Fast with the large afterlife class included: 2,940 tests in 6 minutes 33 seconds;
- scoped afterlife class: 359 tests in 3 minutes 47 seconds;
- current FullValidation lane: 1,636 tests in 9 minutes 59 seconds;
- current RegressionIntegration lane: 2,111 passed, one overloaded GameEngine test timed out, in 14 minutes 33 seconds;
- ProcessIntegration: 455 tests in 2 minutes 32 seconds;
- E2E: 15 tests in 49 seconds;
- existing complete discovery floor: 6,560 tests;
- Guardian two-test benchmark: median wall time about 7.6 seconds and at least 6.7× faster than baseline.

## File Structure

### Create

- `BookOfEternityClient.TestSupport/BookOfEternityClient.TestSupport.csproj` — non-test helper assembly and friend-assembly declarations.
- `BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj` — explicit slow test assembly.
- `BookOfEternityClient.TestSupport/ExampleDocumentationFixtures.cs` — documentation snippet/manifest types extracted from the mixed test source.
- `BookOfEternityClient.IntegrationTests/FullValidationEquivalenceTests.cs` — the one public-versus-explicit-All sentinel extracted from fast selection tests.
- `BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs` — reviewed phase selections used by migrated integration tests.
- `BookOfEternityClient.Tests/FastTestBoundaryTests.cs` — fast-project source and project-reference guards.
- `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs` — integration category, sentinel, partial-class, and runner guards.

### Move to `BookOfEternityClient.TestSupport`

- `ConsoleCommandOutputQualityClassifier.cs`
- `FileSystemManagerHookTestHelper.cs`
- `GmWorkerBridgeTestFixtures.cs`
- `MortalActorTestFixtures.cs`
- `PendingTurnSnapshotTestAuthority.cs`
- `SequenceLocalInteractionScopeResolver.cs`
- `SessionReplacementTestHarness.cs`
- `TestClipboardService.cs`
- `TestExplorerConsole.cs`
- `ValidatorFixtureHarness.cs`
- `WindowsHardLinkTestHelper.cs`

These files retain namespace `BookOfEternityClient.Tests` to avoid mechanical namespace churn. Their assembly, not their namespace, defines the architectural boundary.

### Modify Production

- `BookOfEternityClient/BookOfEternityClient.csproj` — grant internal access to TestSupport and IntegrationTests.
- `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs`
- `BookOfEternityClient/Services/ValidationService.cs`
- `BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs`
- `BookOfEternityClient/Services/Validation/ValidationService.CoreBootstrapAndCrossRefs.cs`
- `BookOfEternityClient/Services/Validation/ValidationService.GuardianPolicyKernel.cs`
- `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs`
- `BookOfEternityClient/Core/GameEngine.cs`
- `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`

### Modify Orchestration and Documentation

- `scripts/test-csharp.ps1`
- `docs/testing.md`
- `specs/1505-test-suite-performance/spec.md`
- `specs/1505-test-suite-performance/plan.md`
- `specs/1505-test-suite-performance/research.md`
- `specs/1505-test-suite-performance/data-model.md`
- `specs/1505-test-suite-performance/quickstart.md`
- `specs/1505-test-suite-performance/tasks.md`

---

### Task 1: Seal the Scoped Validation Foundation

**Files:**

- Create: `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs`
- Modify: `BookOfEternityClient/Services/ValidationService.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.CoreBootstrapAndCrossRefs.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.GuardianPolicyKernel.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs`
- Test: `BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs`

**Interfaces:**

- Produces: `internal enum GameStateValidationPhase : uint`.
- Produces: `internal sealed class GameStateValidationSelection`.
- Produces: `internal Task<List<ValidationIssue>> ValidateGameStateAsync(GameStateValidationPhase phases)`.
- Produces: `internal Task<List<ValidationIssue>> ValidateGameStateAsync(GameStateValidationSelection selection)`.
- Preserves: `public Task<List<ValidationIssue>> ValidateGameStateAsync()` delegating to `GameStateValidationSelection.All`.

- [ ] **Step 1: Inspect the existing dirty diff and verify that the selection tests cover fail-closed masks, canonical order, explicit-All equivalence, per-call isolation, and state-file filtering**

Run:

```powershell
git diff -- BookOfEternityClient/Services/Validation BookOfEternityClient/Services/ValidationService.cs BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs
```

Expected: the public no-argument method still delegates to the complete production pipeline, and the internal overload rejects `None` and unknown bits.

- [ ] **Step 2: Run the focused selector test class**

Run:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~ValidationPhaseSelectionTests" --verbosity minimal
```

Expected: 7 tests pass, including `ValidateGameStateAsync_ExplicitAll_MatchesPublicFacade`.

- [ ] **Step 3: Verify runtime callers still use only the public no-argument method**

Run:

```powershell
rg -n "ValidateGameStateAsync\s*\(" BookOfEternityClient -g "*.cs"
```

Expected: production callers outside `ValidationService` pass no selection; scoped overload declarations and dispatch remain inside the validation implementation.

- [ ] **Step 4: Build the production solution**

Run:

```powershell
dotnet build BookOfEternityClient/BookOfEternityClient.sln --no-restore --verbosity minimal
```

Expected: exit code 0, zero warnings, zero errors.

- [ ] **Step 5: Commit only the selector foundation**

```powershell
git add BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs
git add BookOfEternityClient/Services/ValidationService.cs
git add BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs
git add BookOfEternityClient/Services/Validation/ValidationService.CoreBootstrapAndCrossRefs.cs
git add BookOfEternityClient/Services/Validation/ValidationService.GuardianPolicyKernel.cs
git add BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs
git add BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs
git commit -m "perf: add scoped game-state validation (#1505)"
```

Expected: `.serena/` and all unrelated test-lane work remain unstaged.

---

### Task 2: Seal the Measured Guardian and Afterlife Optimizations

**Files:**

- Create: `BookOfEternityClient.Tests/GuardianValidationProfiles.cs`
- Create: `BookOfEternityClient.Tests/GuardianFixtureIsolationTests.cs`
- Modify: `BookOfEternityClient.Tests/GuardianSystemRegressionTests.cs`
- Modify: every `BookOfEternityClient.Tests/GuardianSystemRegressionTests.*.cs`
- Modify: `BookOfEternityClient.Tests/MortalFactPersistenceValidationTests.cs`
- Modify: `BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs`
- Test: `BookOfEternityClient.Tests/TestLaneSourceGuardTests.cs`

**Interfaces:**

- Produces: named `GuardianValidationProfiles` selections for all eight Guardian domains.
- Produces: one immutable prepared Guardian snapshot per test host and an independently materialized writable root per test instance.
- Produces: `ValidateCoreAsync()` and `ValidateWithContextAsync()` helpers in the afterlife spiritual-conflict suite.

- [ ] **Step 1: Run the Guardian source guards and fixture-isolation test**

Run:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~TestLaneSourceGuardTests|FullyQualifiedName~GuardianFixtureIsolationTests" --verbosity minimal
```

Expected: all guards pass; direct Guardian broad calls are zero; two Guardian instances receive different writable roots; the prepared snapshot build count is one.

- [ ] **Step 2: Run every Guardian regression test with a five-minute bound**

Run:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~GuardianSystemRegressionTests" -TimeoutMinutes 5
```

Expected: all discovered Guardian cases pass before the five-minute deadline.

- [ ] **Step 3: Run the complete afterlife spiritual-conflict class with a five-minute bound**

Run:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeSpiritualConflictValidationTests" -TimeoutMinutes 5
```

Expected: 358 class cases pass; together with its focused source guard, the recorded control remains 359/359 and below five minutes.

- [ ] **Step 4: Verify scoped assertions execute their owning phase**

Run:

```powershell
rg -n "ValidateCoreAsync|ValidateWithContextAsync|AfterlifeSpiritualConflictState|RealmSegregation" BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs
```

Expected: state-contract assertions call the core profile, realm/lifecycle/forced-incarnation assertions call the context profile, and there is no parameterless `_validator.ValidateGameStateAsync()` call.

- [ ] **Step 5: Commit the two measured hotspot optimizations**

```powershell
git add BookOfEternityClient.Tests/GuardianValidationProfiles.cs
git add BookOfEternityClient.Tests/GuardianFixtureIsolationTests.cs
git add BookOfEternityClient.Tests/GuardianSystemRegressionTests.cs
git add BookOfEternityClient.Tests/GuardianSystemRegressionTests.*.cs
git add BookOfEternityClient.Tests/MortalFactPersistenceValidationTests.cs
git add BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs
git commit -m "perf: scope guardian and afterlife validation tests (#1505)"
```

Keep the still-untracked combined lane guard for working-tree verification; Task 7 replaces it with project-specific committed guards.

---

### Task 3: Create the Three-Project Topology

**Files:**

- Create: `BookOfEternityClient.TestSupport/BookOfEternityClient.TestSupport.csproj`
- Create: `BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj`
- Modify: `BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj`
- Modify: `BookOfEternityClient/BookOfEternityClient.csproj`
- Test: `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`

**Interfaces:**

- `BookOfEternityClient.TestSupport` references production and exposes its internal helpers to both test assemblies through `InternalsVisibleTo`.
- Both test assemblies reference production and TestSupport.
- IntegrationTests never references Tests.

- [ ] **Step 1: Write a failing topology guard**

Create `BookOfEternityClient.Tests/FastTestBoundaryTests.cs` with a test that resolves the repository through `TestRepoPaths.RepoRoot`, loads the three project files, and asserts:

```csharp
[Fact]
public void TestProjectTopology_SeparatesFastIntegrationAndSupportAssemblies()
{
    var fastProject = ReadProject("BookOfEternityClient.Tests", "BookOfEternityClient.Tests.csproj");
    var integrationProject = ReadProject(
        "BookOfEternityClient.IntegrationTests",
        "BookOfEternityClient.IntegrationTests.csproj");
    var supportProject = ReadProject(
        "BookOfEternityClient.TestSupport",
        "BookOfEternityClient.TestSupport.csproj");

    Assert.Contains(@"..\BookOfEternityClient.TestSupport\BookOfEternityClient.TestSupport.csproj", fastProject);
    Assert.Contains(@"..\BookOfEternityClient.TestSupport\BookOfEternityClient.TestSupport.csproj", integrationProject);
    Assert.Contains(@"..\BookOfEternityClient\BookOfEternityClient.csproj", integrationProject);
    Assert.DoesNotContain("BookOfEternityClient.Tests.csproj", integrationProject);
    Assert.DoesNotContain("Microsoft.NET.Test.Sdk", supportProject);
    Assert.DoesNotContain("xunit", supportProject, StringComparison.OrdinalIgnoreCase);
}

private static string ReadProject(string directory, string fileName) =>
    File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, directory, fileName));
```

- [ ] **Step 2: Run the topology guard and capture RED**

Run:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~TestProjectTopology_SeparatesFastIntegrationAndSupportAssemblies" --verbosity minimal
```

Expected: FAIL because the TestSupport and IntegrationTests projects do not exist.

- [ ] **Step 3: Create the framework-neutral TestSupport project**

Create `BookOfEternityClient.TestSupport/BookOfEternityClient.TestSupport.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <AssemblyName>BookOfEternityClient.TestSupport</AssemblyName>
    <RootNamespace>BookOfEternityClient.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\BookOfEternityClient\BookOfEternityClient.csproj" />
  </ItemGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>BookOfEternityClient.Tests</_Parameter1>
    </AssemblyAttribute>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>BookOfEternityClient.IntegrationTests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create the integration test project**

Create `BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <AssemblyName>BookOfEternityClient.IntegrationTests</AssemblyName>
    <RootNamespace>BookOfEternityClient.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BookOfEternityClient\BookOfEternityClient.csproj" />
    <ProjectReference Include="..\BookOfEternityClient.TestSupport\BookOfEternityClient.TestSupport.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Reference TestSupport from the fast project and grant production internals to the two new assemblies**

Add this reference to `BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj`:

```xml
<ProjectReference Include="..\BookOfEternityClient.TestSupport\BookOfEternityClient.TestSupport.csproj" />
```

Add these attributes beside the existing Tests friend declaration in `BookOfEternityClient/BookOfEternityClient.csproj`:

```xml
<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
  <_Parameter1>BookOfEternityClient.TestSupport</_Parameter1>
</AssemblyAttribute>
<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
  <_Parameter1>BookOfEternityClient.IntegrationTests</_Parameter1>
</AssemblyAttribute>
```

- [ ] **Step 6: Run the topology guard GREEN and prove the production solution stayed production-only**

Run:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~TestProjectTopology_SeparatesFastIntegrationAndSupportAssemblies" --verbosity minimal
dotnet sln BookOfEternityClient/BookOfEternityClient.sln list
```

Expected: the guard passes; the solution list contains production projects only and contains neither test project.

- [ ] **Step 7: Commit the project topology**

```powershell
git add BookOfEternityClient.TestSupport/BookOfEternityClient.TestSupport.csproj
git add BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj
git add BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj
git add BookOfEternityClient.Tests/FastTestBoundaryTests.cs
git add BookOfEternityClient/BookOfEternityClient.csproj
git commit -m "test: create isolated test project topology (#1505)"
```

---

### Task 4: Extract Shared Test Infrastructure

**Files:**

- Move: the eleven TestSupport files listed in the File Structure section.
- Modify: `BookOfEternityClient.TestSupport/FileSystemManagerHookTestHelper.cs`
- Test: existing focused consumers in both future assemblies.

**Interfaces:**

- Preserves all existing helper type names in namespace `BookOfEternityClient.Tests`.
- Removes the only xUnit dependency from shared helper sources.
- Keeps all helper types internal; friend assemblies provide access.

- [ ] **Step 1: Replace xUnit assertion logic in `FileSystemManagerHookTestHelper`**

Remove `using Xunit;` and replace both reflection lookups with:

```csharp
private static PropertyInfo GetRequiredProperty(string propertyName) =>
    typeof(FileSystemManagerHooks).GetProperty(
        propertyName,
        BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException(
        $"FileSystemManagerHooks property was not found: {propertyName}");
```

Then set hook values with:

```csharp
GetRequiredProperty(propertyName).SetValue(hooks, callback);
```

and:

```csharp
GetRequiredProperty(propertyName).SetValue(hooks, value);
```

- [ ] **Step 2: Move the shared files with history**

Run one `git mv` per explicit file:

```powershell
$supportFiles = @(
    "ConsoleCommandOutputQualityClassifier.cs",
    "FileSystemManagerHookTestHelper.cs",
    "GmWorkerBridgeTestFixtures.cs",
    "MortalActorTestFixtures.cs",
    "PendingTurnSnapshotTestAuthority.cs",
    "SequenceLocalInteractionScopeResolver.cs",
    "SessionReplacementTestHarness.cs",
    "TestClipboardService.cs",
    "TestExplorerConsole.cs",
    "ValidatorFixtureHarness.cs",
    "WindowsHardLinkTestHelper.cs"
)
foreach ($file in $supportFiles) {
    git mv "BookOfEternityClient.Tests/$file" "BookOfEternityClient.TestSupport/$file"
}
```

Expected: every resolved source and destination is within the worktree, and no test class moves into TestSupport.

- [ ] **Step 3: Build TestSupport and the fast test project**

Run:

```powershell
dotnet build BookOfEternityClient.TestSupport/BookOfEternityClient.TestSupport.csproj --no-restore --verbosity minimal
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity minimal
```

Expected: both builds pass; TestSupport restores no test SDK or xUnit package.

- [ ] **Step 4: Run representative shared-helper consumers**

Run:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~ConsoleCommandOutputQualityClassifierTests|FullyQualifiedName~PendingTurnSnapshotAuthorityTests|FullyQualifiedName~TextComposerTests|FullyQualifiedName~BrowserLocalWriteCoordinatorTests" --verbosity minimal
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit TestSupport extraction**

```powershell
git add BookOfEternityClient.TestSupport
git commit -m "refactor: extract shared test support (#1505)"
```

The preceding `git mv` commands already stage the source removals. Before committing, verify `git diff --cached --name-only` contains only the eleven moves and the TestSupport changes.

---

### Task 5: Split Sources That Contain Both Fast Support and Full Validation

**Files:**

- Modify: `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs`
- Create: `BookOfEternityClient.TestSupport/ExampleDocumentationFixtures.cs`
- Modify: `BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/FullValidationEquivalenceTests.cs`

**Interfaces:**

- `ExampleSnippetExtractor`, `ExampleSnippet`, `ExampleExpected`, `ExampleValidationManifest`, and all manifest DTOs remain available to both test assemblies.
- Fast selection tests retain six scoped/fail-closed cases.
- Integration owns the single public-versus-explicit-All full-pipeline equivalence sentinel.

- [ ] **Step 1: Extract documentation fixture types without changing their logic**

Move the complete top-level declaration block currently beginning with `ExampleSnippetExtractor` and ending with `ExampleRuntimeFileDoesNotContainAssertion` from `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs` into `BookOfEternityClient.TestSupport/ExampleDocumentationFixtures.cs`.

The new file starts with:

```csharp
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BookOfEternityClient.Tests;
```

Move these declarations intact:

```text
ExampleSnippetExtractor
ExampleSnippet
ExampleExpected
ExampleValidationManifest
ExampleContractCoverage
ActorMaterializationExampleCoverage
InkFeatherReceiptCoverage
ExampleSyntaxExemption
ExampleRuntimeScenario
AfterlifeExampleCoverage
ExampleRuntimePreStateFile
ExampleRuntimeCompanionFile
ExampleRuntimeFileContainsAssertion
ExampleRuntimeFileDoesNotContainAssertion
```

- [ ] **Step 2: Verify fast documentation consumers**

Run:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~PromptDocumentationCoverageTests" --verbosity minimal
```

Expected: both existing consumer classes compile and pass using TestSupport.

- [ ] **Step 3: Extract the full equivalence sentinel**

Remove `ValidateGameStateAsync_ExplicitAll_MatchesPublicFacade` from `ValidationPhaseSelectionTests`.

Create `BookOfEternityClient.IntegrationTests/FullValidationEquivalenceTests.cs`:

```csharp
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class FullValidationEquivalenceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fileSystem;
    private readonly ValidationService _validator;

    public FullValidationEquivalenceTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-full-validation-equivalence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        _fileSystem.EnsureDirectoryStructure();
        _validator = new ValidationService(
            _fileSystem,
            NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ExplicitAll_MatchesPublicFacade()
    {
        await _fileSystem.WriteFileAtomicAsync(
            "game_state/misc/phase_selection_invalid.json",
            "{");

        var publicIssues = await _validator.ValidateGameStateAsync();
        var explicitAllIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.All);

        Assert.Equal(Snapshot(publicIssues), Snapshot(explicitAllIssues));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private static ValidationIssueSnapshot[] Snapshot(
        IEnumerable<ValidationIssue> issues) =>
        issues.Select(issue => new ValidationIssueSnapshot(
                issue.Severity,
                issue.Code,
                issue.FilePath,
                issue.Message))
            .ToArray();

    private sealed record ValidationIssueSnapshot(
        IssueSeverity Severity,
        string? Code,
        string FilePath,
        string Message);
}
```

- [ ] **Step 4: Run both halves**

Run:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~ValidationPhaseSelectionTests" --verbosity minimal
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~FullValidationEquivalenceTests" --verbosity minimal
```

Expected: six fast selector tests pass and one integration equivalence sentinel passes.

- [ ] **Step 5: Commit the mixed-source split**

```powershell
git add BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs
git add BookOfEternityClient.TestSupport/ExampleDocumentationFixtures.cs
git add BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs
git add BookOfEternityClient.IntegrationTests/FullValidationEquivalenceTests.cs
git commit -m "refactor: split mixed fast and full validation sources (#1505)"
```

---

### Task 6: Physically Move Every Reviewed Slow Source

**Files:**

- Move: all files in the explicit manifest below from `BookOfEternityClient.Tests` to `BookOfEternityClient.IntegrationTests`.
- Preserve: namespace `BookOfEternityClient.Tests`.
- Preserve: all `[Fact]`, `[Theory]`, `[InlineData]`, `[MemberData]`, and assertions.

**Interfaces:**

- An unfiltered build/test of the fast project cannot discover a moved test.
- All four partial groups move atomically: `BrowserCommandPresentationAuditTests`, `CanonicalStateNormalizerTests`, `ExplorerModeCommandTests`, and `GuardianSystemRegressionTests`.

- [ ] **Step 1: Record the pre-move discovery count**

Run:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --list-tests --verbosity quiet |
    Set-Content TestResults/test-project-split-baseline.txt
```

Expected: discovery completes without executing tests; retain the untracked artifact until post-move comparison.

- [ ] **Step 2: Move class-level FullValidation and explicit afterlife sources**

Move these exact files:

```text
ActorMaterializationValidationTests.cs
AfterlifeActiveThreatValidationTests.cs
AfterlifeArchiveActionStateTests.cs
AfterlifeChronicleValidationTests.cs
AfterlifeEntityProfileValidationTests.cs
AfterlifeGlobalFlagValidationTests.cs
AfterlifeRealmSegregationValidationTests.cs
AfterlifeSpiritualConflictBalanceTests.cs
AfterlifeSpiritualConflictValidationTests.cs
AfterlifeStoryOutlineValidationTests.cs
BookOfEternityClientGameSessionIntegrityTests.cs
ChaosSeaCommandDisplaySaveTests.cs
ChaosSeaGuardianPoliticsStateTests.cs
ChaosSeaPendingRequestHygieneTests.cs
ExampleDocumentationValidationTests.cs
FactionIdentityValidationTests.cs
FileSystemExampleFixtureIntegrityTests.cs
GuardianArchiveAndTradeRequestValidationTests.cs
GuardianPolicyKernelTests.cs
GuardianTradeServiceTests.cs
MathAssistantContractValidationTests.cs
MechanicalBonusAuthorityValidationTests.cs
MortalBootstrapValidationTests.cs
MortalCommandDisplaySaveTests.cs
NpcCoreChangesTests.cs
NpcStateFileValidationTests.cs
NpcTradeRequestValidationTests.cs
PlayerGuardianFoundationValidationTests.cs
QuestRewardAuthorityValidationTests.cs
ReadableDocumentAuthorityValidationTests.cs
RealmSemanticsValidationTests.cs
SarefMainStoryStateValidationTests.cs
ShiningAbodeCommandDisplaySaveTests.cs
ShiningPoliticalResolutionValidationTests.cs
ShiningStateValidationTests.cs
SoulIdentityValidationTests.cs
SourceOfLightCapstoneValidationTests.cs
SystemGuardianLibraryServiceTests.cs
TrainingValidationTests.cs
ValidationServiceQteTests.cs
ValidatorFixtureTests.cs
WeatherValidationTests.cs
```

- [ ] **Step 3: Move every file in the four partial groups**

Move these exact groups:

```text
BrowserCommandPresentationAuditTests.cs
BrowserCommandPresentationAuditTests.Parity.cs

CanonicalStateNormalizerTests.cs
CanonicalStateNormalizerTests.ActorMemory.cs
CanonicalStateNormalizerTests.AfterlifeActiveThreats.cs
CanonicalStateNormalizerTests.AfterlifeChronicles.cs
CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs
CanonicalStateNormalizerTests.AfterlifeGlobalFlags.cs
CanonicalStateNormalizerTests.AfterlifeLore.cs
CanonicalStateNormalizerTests.AfterlifeStoryOutline.cs
CanonicalStateNormalizerTests.GuardianAbodeResidents.cs
CanonicalStateNormalizerTests.GuardianProjects.cs
CanonicalStateNormalizerTests.GuardianQuestLifecycle.cs
CanonicalStateNormalizerTests.Inventory.cs
CanonicalStateNormalizerTests.Npcs.cs
CanonicalStateNormalizerTests.RivalClues.cs
CanonicalStateNormalizerTests.SarefMainStory.cs
CanonicalStateNormalizerTests.ShiningAbode.cs

ExplorerModeCommandTests.cs
ExplorerModeCommandTests.Afterlife.cs
ExplorerModeCommandTests.GeneralPanels.cs
ExplorerModeCommandTests.LocalSessionLock.cs
ExplorerModeCommandTests.MathAssistant.cs
ExplorerModeCommandTests.RivalAndWorld.cs
ExplorerModeCommandTests.TradeAndInventory.cs
ExplorerModeCommandTests.UniversalMeta.cs

GuardianSystemRegressionTests.cs
GuardianSystemRegressionTests.AcceptedAuthority.cs
GuardianSystemRegressionTests.ActorBrain.cs
GuardianSystemRegressionTests.IdleValidation.cs
GuardianSystemRegressionTests.LifecycleSnapshots.cs
GuardianSystemRegressionTests.PowerJournalOfferings.cs
GuardianSystemRegressionTests.ProjectsPower.cs
GuardianSystemRegressionTests.QuestProgress.cs
GuardianSystemRegressionTests.RivalResidents.cs
GuardianSystemRegressionTests.TradeOfferingResonance.cs
MortalFactPersistenceValidationTests.cs
```

- [ ] **Step 4: Move standalone regression, process, and E2E sources**

Move these exact files:

```text
AgentConsoleLiveSmokeTests.cs
ConsoleE2ESmokeTests.cs
ExplorerWebCommandServiceTests.cs
ExplorerWebCommandServiceTestsAfterlifeProfileInboxDrilldowns.cs
ExplorerWebCommandServiceTestsSpiritualConflictArtDrilldowns.cs
FileSystemManagerTests.cs
GameEngineTurnLifecycleTests.cs
GmMemorySearchToolTests.cs
GmTurnHelperContractTests.cs
GmWorkerBridgeLifecycleTests.cs
GmWorkerCliRunnerTests.cs
GmWorkerProcessHostTests.cs
GmWorkerProcessTreeTests.cs
GmWorkerProposalStoreTests.cs
ImageServiceTests.cs
LocalWebUiBuiltFrontendSmokeTests.cs
LocalWebUiHostTests.cs
SaveLoadServiceTests.cs
WebUi/BrowserMediaGenerationServiceTests.cs
WebUi/BrowserQteGenerationFencingTests.cs
```

Also move integration-only optimization support:

```text
GuardianFixtureIsolationTests.cs
GuardianValidationProfiles.cs
```

- [ ] **Step 5: Mark the large scoped afterlife class as regression integration**

Add this class-level trait to `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.cs`:

```csharp
[Trait("Category", "RegressionIntegration")]
```

- [ ] **Step 6: Build both assemblies and resolve only cross-assembly helper errors**

Run:

```powershell
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity minimal
dotnet build BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --verbosity minimal
```

Expected: both builds pass. Do not add a project reference from IntegrationTests to Tests to resolve an error.

- [ ] **Step 7: Compare discovery without executing the suite**

Run:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-build --no-restore --list-tests --verbosity quiet |
    Set-Content TestResults/test-project-split-fast.txt
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-build --no-restore --list-tests --verbosity quiet |
    Set-Content TestResults/test-project-split-integration.txt
```

Expected: the combined discoverable inventory is at least the 6,560-case baseline, and no fully qualified test display name occurs in both files.

- [ ] **Step 8: Commit only physical ownership changes**

```powershell
git add BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.cs
git diff --cached --name-only
git commit -m "test: move slow tests into integration assembly (#1505)"
```

Expected staged paths: the explicit move manifest, `GuardianFixtureIsolationTests.cs`, `GuardianValidationProfiles.cs`, and the afterlife category edit; no remaining fast source is staged.

---

### Task 7: Enforce Project and Source Boundaries

**Files:**

- Delete locally: `BookOfEternityClient.Tests/TestLaneSourceGuardTests.cs` (the untracked combined predecessor)
- Modify: `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`

**Interfaces:**

- Fast guard scans only fast source for forbidden categories and parameterless full-pipeline calls.
- Integration guard scans IntegrationTests plus TestSupport for broad calls.
- Partial-class guard proves each partial class name exists under one source root only.

- [ ] **Step 1: Add a failing fast-source boundary test**

Add:

```csharp
[Fact]
public void FastSources_ContainNoIntegrationCategoriesOrBroadValidationCalls()
{
    var fastRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.Tests");
    var broadCall = new Regex(
        @"\.ValidateGameStateAsync\s*\(\s*\)",
        RegexOptions.CultureInvariant);
    var forbiddenCategories = new[]
    {
        "FullValidation",
        "RegressionIntegration",
        "ProcessIntegration",
        "E2E"
    };

    var violations = Directory.EnumerateFiles(fastRoot, "*.cs", SearchOption.AllDirectories)
        .Select(path => (Path: path, Source: File.ReadAllText(path)))
        .SelectMany(candidate =>
            forbiddenCategories
                .Where(category => candidate.Source.Contains(
                    $"[Trait(\"Category\", \"{category}\")]",
                    StringComparison.Ordinal))
                .Select(category => $"{candidate.Path}: {category}")
                .Concat(
                    broadCall.IsMatch(candidate.Source)
                        ? new[] { $"{candidate.Path}: parameterless full validation" }
                        : Array.Empty<string>()))
        .ToArray();

    Assert.Empty(violations);
}
```

Run it before deleting the old self-matching guard source. Expected: RED until the old broad-call regex literal is built from concatenated strings.

- [ ] **Step 2: Make the fast guard self-scan safe and assert reviewed heavy anchors**

Construct the broad call token as:

```csharp
var broadCall = new Regex(
    @"\.ValidateGameState" + @"Async\s*\(\s*\)",
    RegexOptions.CultureInvariant);
```

Assert these anchors exist only under IntegrationTests:

```text
AfterlifeSpiritualConflictValidationTests.cs
GameEngineTurnLifecycleTests.cs
GuardianSystemRegressionTests.cs
FileSystemManagerTests.cs
ConsoleE2ESmokeTests.cs
LocalWebUiBuiltFrontendSmokeTests.cs
```

- [ ] **Step 3: Create integration category and partial-class guards**

`IntegrationTestBoundaryTests` must:

1. enumerate integration and support `.cs` files;
2. verify the reviewed process/E2E manifest from the approved design;
3. verify the reviewed regression anchor files carry `RegressionIntegration`;
4. parse `partial class <name>` and assert every name occurs under exactly one of the two test roots;
5. verify all Guardian profile partials call their reviewed profile;
6. verify afterlife spiritual-conflict calls no parameterless full validation;
7. verify `scripts/test-csharp.ps1` contains `Fast`, `Focused`, `FullValidation`, `RegressionIntegration`, `ProcessIntegration`, `E2E`, `PreMerge`, and `Complete`.

Use the exact process/E2E manifest already present in the old guard and update its root from `BookOfEternityClient.Tests` to `BookOfEternityClient.IntegrationTests`.

- [ ] **Step 4: Run all boundary guards**

Run:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~FastTestBoundaryTests" --verbosity minimal
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~IntegrationTestBoundaryTests|FullyQualifiedName~GuardianFixtureIsolationTests" --verbosity minimal
```

Expected: all tests pass; no guard depends on loading the other test assembly.

- [ ] **Step 5: Commit boundary enforcement**

```powershell
git add BookOfEternityClient.Tests/FastTestBoundaryTests.cs
git add BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs
git commit -m "test: enforce fast and integration boundaries (#1505)"
```

Verify the obsolete untracked `TestLaneSourceGuardTests.cs` no longer exists before committing.

---

### Task 8: Add Reviewed Integration Validation Profiles

**Files:**

- Create: `BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs`
- Modify: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`
- Test: existing validation classes migrated in Tasks 9–11.

**Interfaces:**

- Produces named, non-empty `GameStateValidationSelection` values.
- Every profile selects the phase that emits the diagnostics asserted by its mapped test files.

- [ ] **Step 1: Create the complete profile catalog**

Create:

```csharp
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static class IntegrationValidationProfiles
{
    internal static readonly GameStateValidationSelection ActorMaterialization = Select(
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.NpcStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.AcceptedTurnActorMaterializationCompleteness |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection AfterlifeActiveThreat = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.AfterlifeActiveThreatPreTurnContinuity);

    internal static readonly GameStateValidationSelection AfterlifeArchive = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection AfterlifeChronicle = Select(
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection AfterlifeEntityProfile = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection AfterlifeGlobalFlag = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.AfterlifeGlobalFlagPreTurnContinuity);

    internal static readonly GameStateValidationSelection AfterlifeRealm = Select(
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection AfterlifeConflict = Select(
        GameStateValidationPhase.AfterlifeSpiritualConflictState);

    internal static readonly GameStateValidationSelection AfterlifeStory = Select(
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection CanonicalGuardian = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents);

    internal static readonly GameStateValidationSelection CanonicalInventory = Select(
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.PlayerStateFiles);

    internal static readonly GameStateValidationSelection CanonicalNpc = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.NpcStateFiles |
        GameStateValidationPhase.RivalAndResidentCrossReferences);

    internal static readonly GameStateValidationSelection CommandDisplaySave = Select(
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection FactionState = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles);

    internal static readonly GameStateValidationSelection GuardianArchiveTrade = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection GuardianPolicy = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection MechanicalBonus = Select(
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.PlayerStateFiles |
        GameStateValidationPhase.NpcStateFiles |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection MortalBootstrap = Select(
        GameStateValidationPhase.RequiredFiles |
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.LoreBootstrapRequiredFiles |
        GameStateValidationPhase.MortalBootstrapPlayerVisibleNames |
        GameStateValidationPhase.MortalBootstrapContentAnchors |
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.PlayerStateFiles |
        GameStateValidationPhase.NpcStateFiles |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles);

    internal static readonly GameStateValidationSelection NpcState = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.NpcStateFiles |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.AcceptedTurnActorMaterializationCompleteness |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RivalAndResidentCrossReferences);

    internal static readonly GameStateValidationSelection PlayerGuardian = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection QuestReward = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection ReadableDocument = Select(
        GameStateValidationPhase.JsonIntegrity |
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection RealmSemantics = Select(
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection SarefStory = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection ShiningState = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ShiningLeadershipHeadReferences |
        GameStateValidationPhase.ShiningTreasuryClientOwnedState |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection SoulIdentity = Select(
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection SourceOfLight = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.SourceOfLightCapstoneGlobalState |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection SystemGuardianLibrary = Select(
        GameStateValidationPhase.RequiredFiles |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection Training = Select(
        GameStateValidationPhase.PlayerStateFiles |
        GameStateValidationPhase.SkillContractConsistency |
        GameStateValidationPhase.TrainingShowcases |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection Qte = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection Weather = Select(
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles);

    private static GameStateValidationSelection Select(GameStateValidationPhase phases) =>
        new(phases);
}
```

- [ ] **Step 2: Add a profile-shape theory**

Reflect all static `GameStateValidationSelection` fields and assert every profile has neither `None` nor unknown bits:

```csharp
[Fact]
public void IntegrationValidationProfiles_AreNonEmptyAndSelectable()
{
    var profiles = typeof(IntegrationValidationProfiles)
        .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
        .Where(field => field.FieldType == typeof(GameStateValidationSelection))
        .Select(field => (GameStateValidationSelection)field.GetValue(null)!)
        .ToArray();

    Assert.NotEmpty(profiles);
    Assert.All(profiles, profile =>
    {
        Assert.NotEqual(GameStateValidationPhase.None, profile.Phases);
        Assert.Equal(
            GameStateValidationPhase.None,
            profile.Phases & ~GameStateValidationPhase.Selectable);
    });
}
```

- [ ] **Step 3: Run the profile-shape test**

Run:

```powershell
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~IntegrationValidationProfiles_AreNonEmptyAndSelectable" --verbosity minimal
```

Expected: PASS.

- [ ] **Step 4: Commit the profile catalog**

```powershell
git add BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs
git add BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs
git commit -m "test: define integration validation profiles (#1505)"
```

---

### Task 9: Migrate Actor and Afterlife Full-Pipeline Repetition

**Files:**

- Modify:
  - `ActorMaterializationValidationTests.cs`
  - `AfterlifeActiveThreatValidationTests.cs`
  - `AfterlifeArchiveActionStateTests.cs`
  - `AfterlifeChronicleValidationTests.cs`
  - `AfterlifeEntityProfileValidationTests.cs`
  - `AfterlifeGlobalFlagValidationTests.cs`
  - `AfterlifeRealmSegregationValidationTests.cs`
  - `AfterlifeSpiritualConflictBalanceTests.cs`
  - `AfterlifeStoryOutlineValidationTests.cs`
  - `FactionIdentityValidationTests.cs`
  - `RealmSemanticsValidationTests.cs`
  - `SoulIdentityValidationTests.cs`
- Modify: `IntegrationTestBoundaryTests.cs`

**Interfaces:**

- Every file maps to one explicit profile:

| Source | Profile |
|---|---|
| `ActorMaterializationValidationTests.cs` | `ActorMaterialization` |
| `AfterlifeActiveThreatValidationTests.cs` | `AfterlifeActiveThreat` |
| `AfterlifeArchiveActionStateTests.cs` | `AfterlifeArchive` |
| `AfterlifeChronicleValidationTests.cs` | `AfterlifeChronicle` |
| `AfterlifeEntityProfileValidationTests.cs` | `AfterlifeEntityProfile` |
| `AfterlifeGlobalFlagValidationTests.cs` | `AfterlifeGlobalFlag` |
| `AfterlifeRealmSegregationValidationTests.cs` | `AfterlifeRealm` |
| `AfterlifeSpiritualConflictBalanceTests.cs` | `AfterlifeConflict` |
| `AfterlifeStoryOutlineValidationTests.cs` | `AfterlifeStory` |
| `FactionIdentityValidationTests.cs` | `FactionState` |
| `RealmSemanticsValidationTests.cs` | `RealmSemantics` |
| `SoulIdentityValidationTests.cs` | `SoulIdentity` |

- [ ] **Step 1: Add a RED guard for the twelve source files**

Add their exact relative names to an `ActorAndAfterlifeScopedSources` dictionary and assert each file has no parameterless `ValidateGameStateAsync()` and contains the exact `IntegrationValidationProfiles` field named in the mapping table.

Run:

```powershell
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ActorAndAfterlifeValidationSources_UseScopedProfiles" --verbosity minimal
```

Expected: FAIL and list the files that still contain broad calls.

- [ ] **Step 2: Replace every broad call according to the table**

For example:

```csharp
var issues = await _validator.ValidateGameStateAsync(
    IntegrationValidationProfiles.ActorMaterialization);
```

For inline service creation:

```csharp
var issues = await new ValidationService(
        _fs,
        NullLogger<ValidationService>.Instance)
    .ValidateGameStateAsync(IntegrationValidationProfiles.AfterlifeArchive);
```

Do not change assertions, fixture writes, or expected issue codes.

- [ ] **Step 3: Run all twelve migrated classes**

Run:

```powershell
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ActorMaterializationValidationTests|FullyQualifiedName~AfterlifeActiveThreatValidationTests|FullyQualifiedName~AfterlifeArchiveActionStateTests|FullyQualifiedName~AfterlifeChronicleValidationTests|FullyQualifiedName~AfterlifeEntityProfileValidationTests|FullyQualifiedName~AfterlifeGlobalFlagValidationTests|FullyQualifiedName~AfterlifeRealmSegregationValidationTests|FullyQualifiedName~AfterlifeSpiritualConflictBalanceTests|FullyQualifiedName~AfterlifeStoryOutlineValidationTests|FullyQualifiedName~FactionIdentityValidationTests|FullyQualifiedName~RealmSemanticsValidationTests|FullyQualifiedName~SoulIdentityValidationTests" --verbosity minimal
```

Execute this command with a 300,000 ms owning-process deadline. Expected: all discovered cases pass before five minutes. A missing diagnostic is corrected by adding its owning phase to the named profile, never by removing or weakening the assertion.

- [ ] **Step 4: Run the scoped-source guard GREEN**

Run the guard command from Step 1. Expected: PASS.

- [ ] **Step 5: Commit this migration**

```powershell
git add BookOfEternityClient.IntegrationTests
git commit -m "perf: scope actor and afterlife integration validation (#1505)"
```

---

### Task 10: Migrate Guardian, NPC, and Canonical Full-Pipeline Repetition

**Files:**

- Modify:
  - `GuardianArchiveAndTradeRequestValidationTests.cs`
  - `GuardianPolicyKernelTests.cs`
  - `GuardianTradeServiceTests.cs`
  - `ChaosSeaGuardianPoliticsStateTests.cs`
  - `ChaosSeaPendingRequestHygieneTests.cs`
  - `PlayerGuardianFoundationValidationTests.cs`
  - `QuestRewardAuthorityValidationTests.cs`
  - `NpcCoreChangesTests.cs`
  - `NpcStateFileValidationTests.cs`
  - `NpcTradeRequestValidationTests.cs`
  - `CanonicalStateNormalizerTests.AfterlifeChronicles.cs`
  - `CanonicalStateNormalizerTests.GuardianProjects.cs`
  - `CanonicalStateNormalizerTests.Inventory.cs`
  - `CanonicalStateNormalizerTests.Npcs.cs`
- Modify: `IntegrationTestBoundaryTests.cs`

**Interfaces:**

| Source | Profile |
|---|---|
| `GuardianArchiveAndTradeRequestValidationTests.cs` | `GuardianArchiveTrade` |
| `GuardianPolicyKernelTests.cs` | `GuardianPolicy` |
| `GuardianTradeServiceTests.cs` | `GuardianArchiveTrade` |
| `ChaosSeaGuardianPoliticsStateTests.cs` | `GuardianPolicy` |
| `ChaosSeaPendingRequestHygieneTests.cs` | `AfterlifeRealm` |
| `PlayerGuardianFoundationValidationTests.cs` | `PlayerGuardian` |
| `QuestRewardAuthorityValidationTests.cs` | `QuestReward` |
| `NpcCoreChangesTests.cs` | `NpcState` |
| `NpcStateFileValidationTests.cs` | `NpcState` |
| `NpcTradeRequestValidationTests.cs` | `NpcState` |
| `CanonicalStateNormalizerTests.AfterlifeChronicles.cs` | `AfterlifeChronicle` |
| `CanonicalStateNormalizerTests.GuardianProjects.cs` | `CanonicalGuardian` |
| `CanonicalStateNormalizerTests.Inventory.cs` | `CanonicalInventory` |
| `CanonicalStateNormalizerTests.Npcs.cs` | `CanonicalNpc` |

- [ ] **Step 1: Add a RED guard for these fourteen mapped sources**

Use the same no-argument regex and exact profile-token assertion as Task 9.

- [ ] **Step 2: Replace all parameterless calls using the table**

Preserve the existing GuardianSystemRegression profiles; this task changes only the listed non-regression and canonical-normalizer sources.

- [ ] **Step 3: Run the migrated classes in two bounded focused batches**

Run:

```powershell
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~GuardianArchiveAndTradeRequestValidationTests|FullyQualifiedName~GuardianPolicyKernelTests|FullyQualifiedName~GuardianTradeServiceTests|FullyQualifiedName~ChaosSeaGuardianPoliticsStateTests|FullyQualifiedName~ChaosSeaPendingRequestHygieneTests|FullyQualifiedName~PlayerGuardianFoundationValidationTests|FullyQualifiedName~QuestRewardAuthorityValidationTests" --verbosity minimal
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~NpcCoreChangesTests|FullyQualifiedName~NpcStateFileValidationTests|FullyQualifiedName~NpcTradeRequestValidationTests|FullyQualifiedName~CanonicalStateNormalizerTests" --verbosity minimal
```

Execute each command with a separate 300,000 ms owning-process deadline. Expected: both batches pass within their bounds.

- [ ] **Step 4: Run the new scoped-source guard GREEN**

Expected: all fourteen sources contain their reviewed profile and no direct broad call.

- [ ] **Step 5: Commit this migration**

```powershell
git add BookOfEternityClient.IntegrationTests
git commit -m "perf: scope guardian and npc integration validation (#1505)"
```

---

### Task 11: Migrate Mortal, Shining, Story, and Miscellaneous Full-Pipeline Repetition

**Files:**

- Modify:
  - `ChaosSeaCommandDisplaySaveTests.cs`
  - `MathAssistantContractValidationTests.cs`
  - `MechanicalBonusAuthorityValidationTests.cs`
  - `MortalBootstrapValidationTests.cs`
  - `MortalCommandDisplaySaveTests.cs`
  - `ReadableDocumentAuthorityValidationTests.cs`
  - `SarefMainStoryStateValidationTests.cs`
  - `ShiningAbodeCommandDisplaySaveTests.cs`
  - `ShiningPoliticalResolutionValidationTests.cs`
  - `ShiningStateValidationTests.cs`
  - `SourceOfLightCapstoneValidationTests.cs`
  - `SystemGuardianLibraryServiceTests.cs`
  - `TrainingValidationTests.cs`
  - `ValidationServiceQteTests.cs`
  - `WeatherValidationTests.cs`
- Modify: `IntegrationTestBoundaryTests.cs`

**Interfaces:**

| Source | Profile |
|---|---|
| `ChaosSeaCommandDisplaySaveTests.cs` | `CommandDisplaySave` |
| `MathAssistantContractValidationTests.cs` | `ReadableDocument` |
| `MechanicalBonusAuthorityValidationTests.cs` | `MechanicalBonus` |
| `MortalBootstrapValidationTests.cs` | `MortalBootstrap` |
| `MortalCommandDisplaySaveTests.cs` | `CommandDisplaySave` |
| `ReadableDocumentAuthorityValidationTests.cs` | `ReadableDocument` |
| `SarefMainStoryStateValidationTests.cs` | `SarefStory` |
| `ShiningAbodeCommandDisplaySaveTests.cs` | `CommandDisplaySave` |
| `ShiningPoliticalResolutionValidationTests.cs` | `ShiningState` |
| `ShiningStateValidationTests.cs` | `ShiningState` |
| `SourceOfLightCapstoneValidationTests.cs` | `SourceOfLight` |
| `SystemGuardianLibraryServiceTests.cs` | `SystemGuardianLibrary` |
| `TrainingValidationTests.cs` | `Training` |
| `ValidationServiceQteTests.cs` | `Qte` |
| `WeatherValidationTests.cs` | `Weather` |

- [ ] **Step 1: Add a RED guard for these fifteen mapped sources**

Run the focused guard and confirm it reports the remaining parameterless calls.

- [ ] **Step 2: Replace all broad calls using the table**

Keep these seven reviewed direct broad call sites unchanged:

```text
BookOfEternityClient.TestSupport/ValidatorFixtureHarness.cs: 2
BookOfEternityClient.IntegrationTests/BookOfEternityClientGameSessionIntegrityTests.cs: 1
BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs: 1
BookOfEternityClient.IntegrationTests/FileSystemExampleFixtureIntegrityTests.cs: 2
BookOfEternityClient.IntegrationTests/FullValidationEquivalenceTests.cs: 1
```

- [ ] **Step 3: Run the migrated classes in three bounded batches**

Run:

```powershell
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~MechanicalBonusAuthorityValidationTests|FullyQualifiedName~MortalBootstrapValidationTests|FullyQualifiedName~TrainingValidationTests" --verbosity minimal
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~SarefMainStoryStateValidationTests|FullyQualifiedName~ShiningPoliticalResolutionValidationTests|FullyQualifiedName~ShiningStateValidationTests|FullyQualifiedName~SourceOfLightCapstoneValidationTests" --verbosity minimal
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~ChaosSeaCommandDisplaySaveTests|FullyQualifiedName~MathAssistantContractValidationTests|FullyQualifiedName~MortalCommandDisplaySaveTests|FullyQualifiedName~ReadableDocumentAuthorityValidationTests|FullyQualifiedName~ShiningAbodeCommandDisplaySaveTests|FullyQualifiedName~SystemGuardianLibraryServiceTests|FullyQualifiedName~ValidationServiceQteTests|FullyQualifiedName~WeatherValidationTests" --verbosity minimal
```

Execute each command with a separate 300,000 ms owning-process deadline. Expected: all three batches pass within five minutes each.

- [ ] **Step 4: Enforce the global seven-call sentinel manifest**

Add a guard that scans both `BookOfEternityClient.IntegrationTests` and `BookOfEternityClient.TestSupport`, records every parameterless call as `relative-path:line`, and compares exact per-file counts with the five-entry manifest above.

The failure message must include:

```text
Broad-validation sentinel budget is 8
Expected reviewed call sites: 7
Replace repeated calls with IntegrationValidationProfiles or a narrower state-file selection.
```

Run:

```powershell
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~BroadValidationCalls_MatchReviewedSevenCallManifest" --verbosity minimal
```

Expected: PASS with exactly seven source call sites.

- [ ] **Step 5: Run retained broad sentinels once**

Run:

```powershell
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~BookOfEternityClientGameSessionIntegrityTests|FullyQualifiedName~ExampleDocumentationValidationTests|FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~FullValidationEquivalenceTests|FullyQualifiedName~ValidatorFixtureTests" --verbosity minimal
```

Execute this command with a 900,000 ms owning-process deadline. Expected: all retained sentinel tests pass within the one fifteen-minute bound.

- [ ] **Step 6: Commit the final broad-call migration**

```powershell
git add BookOfEternityClient.IntegrationTests
git commit -m "perf: remove repeated full validation from integration tests (#1505)"
```

---

### Task 12: Replace the Measured GameEngine Polling Flake with an Explicit Checkpoint

**Files:**

- Modify: `BookOfEternityClient/Core/GameEngine.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- Modify: `BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`

**Interfaces:**

- Adds internal test checkpoint `LifeEvaluationRequestDispatchedBeforeWait`.
- Does not add a delay, timeout, public API, or production behavior branch.

- [ ] **Step 1: Add a RED source guard for the known polling loop**

Read only the body of `CheckLifeTransitions_LoadAfterRawAcceptedValidation_AbortsWithoutMutatingReplacement` and assert it does not contain:

```csharp
while (DateTime.UtcNow < deadline)
```

or:

```csharp
await Task.Delay(50);
```

Run the guard. Expected: FAIL against the current polling responder.

- [ ] **Step 2: Add the request-dispatched checkpoint**

Add to `SessionFinalizationCheckpoint`:

```csharp
LifeEvaluationRequestDispatchedBeforeWait,
```

Immediately after setting `requestDispatched = true` in `CheckLifeTransitions`, invoke:

```csharp
await InvokeSessionFinalizationCheckpointAsync(
    SessionFinalizationCheckpoint.LifeEvaluationRequestDispatchedBeforeWait);
```

- [ ] **Step 3: Respond from the explicit hook and delete the polling task**

Replace the test hook with:

```csharp
AtCheckpointAsync = async checkpoint =>
{
    if (checkpoint == SessionFinalizationCheckpoint.LifeEvaluationRequestDispatchedBeforeWait)
    {
        var requestJson = await _fs.ReadFileAsync("input/turn_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var requestDoc = JsonDocument.Parse(requestJson);
        var requestRoot = requestDoc.RootElement;
        Assert.Contains(
            "Оценка Жизни",
            requestRoot.GetProperty("playerAction").GetString(),
            StringComparison.Ordinal);
        await WriteAcceptedLifeEvaluationResponseAsync(
            requestRoot.GetProperty("sessionId").GetString(),
            requestRoot.GetProperty("requestId").GetString(),
            requestRoot.GetProperty("turnNumber").GetInt32());
        return;
    }

    if (checkpoint !=
        SessionFinalizationCheckpoint.RawAcceptedOutcomeValidatedBeforeLifeEvaluationFinalWrites)
    {
        return;
    }

    rawValidationReached.TrySetResult();
    await releaseOldFinalizer.Task;
}
```

Delete the `Task.Run` responder, its deadline loop, its `Task.Delay(50)`, and `await responder.WaitAsync(...)`.

- [ ] **Step 4: Run the formerly flaky test five consecutive times**

Run:

```powershell
dotnet build BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --verbosity minimal
1..5 | ForEach-Object {
    dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName=BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.CheckLifeTransitions_LoadAfterRawAcceptedValidation_AbortsWithoutMutatingReplacement" --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "GameEngine checkpoint repetition failed on iteration $_."
    }
}
```

Expected: 5/5 invocations pass without changing the existing failure-guard timeouts.

- [ ] **Step 5: Run the source guard GREEN and commit**

```powershell
git add BookOfEternityClient/Core/GameEngine.cs
git add BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs
git add BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs
git add BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs
git commit -m "test: replace life-transition polling with checkpoint (#1505)"
```

---

### Task 13: Route Lanes by Project and Add One Global PreMerge Deadline

**Files:**

- Modify: `scripts/test-csharp.ps1`
- Modify: `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`

**Interfaces:**

- Default and `Fast`: fast project, no category exclusion filter, five-minute maximum.
- `Focused`: fast project, caller filter, five-minute maximum.
- Diagnostic lanes: integration project, category filter, fifteen-minute maximum.
- `PreMerge`: frontend verify, both builds, complete fast assembly, complete integration assembly through non-overlapping phases, one global fifteen-minute deadline.
- `Complete`: compatibility alias for `PreMerge`.

- [ ] **Step 1: Extend runner source guards and capture RED**

Assert the runner contains:

```text
$fastTestProject
$integrationTestProject
PreMerge
summary.json
DuplicateTests
Category=ProcessIntegration&Category!=E2E
Category=E2E
```

Assert it no longer contains the old Fast exclusion filter:

```text
Category!=FullValidation&Category!=ProcessIntegration&Category!=E2E&Category!=RegressionIntegration
```

Run both boundary classes. Expected: FAIL before runner changes.

- [ ] **Step 2: Define lane routing and hard limits**

Use these definitions:

```powershell
$laneDefinitions = @{
    Fast = @{
        Project = "Fast"
        Filter = $null
        TimeoutMinutes = 5
    }
    Focused = @{
        Project = "Fast"
        Filter = $null
        TimeoutMinutes = 5
    }
    FullValidation = @{
        Project = "Integration"
        Filter = "Category=FullValidation"
        TimeoutMinutes = 15
    }
    RegressionIntegration = @{
        Project = "Integration"
        Filter = "Category=RegressionIntegration"
        TimeoutMinutes = 15
    }
    ProcessIntegration = @{
        Project = "Integration"
        Filter = "Category=ProcessIntegration"
        TimeoutMinutes = 15
    }
    E2E = @{
        Project = "Integration"
        Filter = "Category=E2E"
        TimeoutMinutes = 15
    }
    PreMerge = @{
        Project = "Both"
        Filter = $null
        TimeoutMinutes = 15
    }
}
```

Normalize the compatibility alias before lookup:

```powershell
$effectiveLane = if ($Lane -eq "Complete") { "PreMerge" } else { $Lane }
```

Add `"PreMerge"` to the parameter `ValidateSet` while retaining `"Complete"`.

Reject a requested timeout above the selected lane maximum:

```powershell
if ($TimeoutMinutes -gt [int]$laneDefinition.TimeoutMinutes) {
    throw "Lane '$Lane' has a hard limit of $($laneDefinition.TimeoutMinutes) minute(s)."
}
```

- [ ] **Step 3: Generalize owned process startup**

Change `Start-OwnedProcess` to accept:

```powershell
[string]$FileName = "dotnet"
```

Set:

```powershell
$startInfo.FileName = $FileName
```

Keep `UseShellExecute = $false`, `CreateNoWindow = $true`, `ArgumentList.Add`, redirected output, `Kill($true)`, and the shared `$deadlineUtc`.

- [ ] **Step 4: Route build, discovery, and test arguments through an explicit project**

Define:

```powershell
$fastTestProject = Join-Path $repoRoot "BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj"
$integrationTestProject = Join-Path $repoRoot "BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj"
```

Add a mandatory `$ProjectPath` argument to `New-TestArguments` and `Get-DiscoveredTestCases`; replace their use of the single `$testProject` variable.

Make Guardian sharding project-aware:

```powershell
$guardianCases = @(
    $testCases |
        Where-Object ClassName -eq "BookOfEternityClient.Tests.GuardianSystemRegressionTests"
)
if ($guardianCases.Count -gt 0) {
    $guardianSourceRoot = Split-Path -Parent $ProjectPath
}
```

Run the Guardian source-file parser and special Guardian descriptors only when `$guardianCases.Count -gt 0`. Fast discovery therefore uses only general balanced bins; integration discovery resolves Guardian partial files from `BookOfEternityClient.IntegrationTests`.

- [ ] **Step 5: Build a non-overlapping PreMerge schedule**

Use these mutually exclusive selections:

```powershell
$preMergeParallelSelections = @(
    [pscustomobject]@{
        Name = "Fast"
        ProjectPath = $fastTestProject
        Filter = $null
    },
    [pscustomobject]@{
        Name = "Integration"
        ProjectPath = $integrationTestProject
        Filter = "Category!=ProcessIntegration&Category!=E2E"
    }
)
$preMergeExclusiveSelections = @(
    [pscustomobject]@{
        Name = "ProcessIntegration"
        ProjectPath = $integrationTestProject
        Filter = "Category=ProcessIntegration&Category!=E2E"
    },
    [pscustomobject]@{
        Name = "E2E"
        ProjectPath = $integrationTestProject
        Filter = "Category=E2E"
    }
)
```

Run Fast plus non-process Integration through balanced descriptors with total external parallelism four. Then run ProcessIntegration and E2E one at a time. All phases check the original `$deadlineUtc`; no phase creates a new deadline.

- [ ] **Step 6: Run the frontend prerequisite before PreMerge/E2E builds**

Invoke:

```powershell
$frontendRun = Start-OwnedProcess `
    -Name "Frontend-verify" `
    -FileName "npm.cmd" `
    -Arguments @("run", "verify", "--prefix", "BookOfEternityClient.WebFrontend")
```

Wait through `Wait-ForOwnedProcess`; a timeout or nonzero result fails the lane before test builds.

Wrap frontend execution and all builds in `if (-not $PlanOnly)` so `-PlanOnly -NoBuild` performs discovery and schedule validation without mutating frontend artifacts or launching a build.

- [ ] **Step 7: Detect duplicate TRX test IDs and write a JSON summary**

Aggregate all `UnitTestResult` `testId` attributes. Set:

```powershell
$duplicateTests = @(
    $testIds |
        Group-Object |
        Where-Object Count -gt 1 |
        Select-Object -ExpandProperty Name
)
```

Fail PreMerge if `$duplicateTests.Count -ne 0` or total tests are below 6,560.

Write:

```powershell
$summary = [ordered]@{
    RequestedLane = $Lane
    EffectiveLane = $effectiveLane
    TimeoutMinutes = $effectiveTimeoutMinutes
    WallTime = $stopwatch.Elapsed.ToString()
    ExitCode = $exitCode
    TimedOut = $timedOut
    OwnedTreeCleanupSucceeded = $cleanupSucceeded
    Tests = [ordered]@{
        Total = $trxCounters.Total
        Executed = $trxCounters.Executed
        Passed = $trxCounters.Passed
        Failed = $trxCounters.Failed
    }
    DuplicateTests = $duplicateTests
}
$summary | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $resultDirectory "summary.json")
```

- [ ] **Step 8: Inspect plans without executing tests**

Run:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane Fast -NoBuild -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane FullValidation -NoBuild -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane PreMerge -NoBuild -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane Complete -NoBuild -PlanOnly
```

Expected:

- Fast descriptors reference only `BookOfEternityClient.Tests.csproj`;
- FullValidation descriptors reference only `BookOfEternityClient.IntegrationTests.csproj`;
- PreMerge and Complete show the same effective schedule;
- ProcessIntegration excludes E2E;
- E2E appears once.

- [ ] **Step 9: Run source guards GREEN and commit**

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~FastTestBoundaryTests" --verbosity minimal
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~IntegrationTestBoundaryTests" --verbosity minimal
git add scripts/test-csharp.ps1
git add BookOfEternityClient.Tests/FastTestBoundaryTests.cs
git add BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs
git commit -m "test: add project-routed pre-merge lane (#1505)"
```

---

### Task 14: Reconcile Documentation, Verify Budgets, Review, and Merge

**Files:**

- Modify: `docs/testing.md`
- Modify: all `specs/1505-test-suite-performance/*.md` files listed in File Structure.
- Verify: all production/test/runner changes.

**Interfaces:**

- Documents the default fast command and explicit slow commands.
- Records final measured wall times and exact test counts.
- Updates issue #1505 and merges only after all acceptance gates pass.

- [ ] **Step 1: Update documentation to the final commands**

Document exactly:

```powershell
.\scripts\test-csharp.ps1
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"
.\scripts\test-csharp.ps1 -Lane FullValidation
.\scripts\test-csharp.ps1 -Lane RegressionIntegration
.\scripts\test-csharp.ps1 -Lane ProcessIntegration
.\scripts\test-csharp.ps1 -Lane E2E
.\scripts\test-csharp.ps1 -Lane PreMerge
```

State that `Complete` is a temporary alias, Fast does not depend on category exclusion, diagnostic lanes are not ordinary post-edit controls, and only one PreMerge run is required before merge.

- [ ] **Step 2: Reconcile Spec Kit tasks with the approved architecture**

Mark completed selector, Guardian, category, and runner work accurately. Replace the old instruction to run every slow lane plus Complete with:

```text
Run focused controls during implementation, two consecutive Fast controls at final verification, and one PreMerge control. Do not serially run all diagnostic lanes before PreMerge unless a focused failure requires diagnosis.
```

- [ ] **Step 3: Build the production solution and both test projects**

Run:

```powershell
dotnet build BookOfEternityClient/BookOfEternityClient.sln --no-restore --verbosity minimal
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity minimal
dotnet build BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore --verbosity minimal
```

Expected: all three builds exit 0 with zero warnings and errors.

- [ ] **Step 4: Run Fast twice**

Run:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane Fast
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane Fast
```

Expected for each run: exit 0, wall time below five minutes, no slow test discovered, JSON/TRX/log artifacts present, cleanup complete.

- [ ] **Step 5: Run one final PreMerge control**

Run:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 -Lane PreMerge
```

Expected: exit 0; wall time below the one global fifteen-minute hard limit and preferably below ten minutes; at least 6,560 tests; zero failed tests; zero duplicate test IDs; owned-tree cleanup complete.

Do not follow this with serial runs of all four diagnostic lanes when PreMerge is green.

- [ ] **Step 6: Re-index Serena after the physical source moves**

Run from the worktree root:

```powershell
serena project index
serena project health-check
```

Expected: indexing succeeds and health-check is green. Keep `.serena/` untracked.

- [ ] **Step 7: Run final diff checks**

Run:

```powershell
git diff --check
git status --short
git diff --stat
git diff -- BookOfEternityClient/BookOfEternityClient.sln
```

Expected: no whitespace errors; `.serena/` is the only intentionally untracked local directory; the production solution diff is empty.

- [ ] **Step 8: Update measured evidence and commit documentation**

Record exact counts and wall times from the two Fast summaries and the PreMerge `summary.json`.

```powershell
git add docs/testing.md
git add specs/1505-test-suite-performance/data-model.md
git add specs/1505-test-suite-performance/plan.md
git add specs/1505-test-suite-performance/quickstart.md
git add specs/1505-test-suite-performance/research.md
git add specs/1505-test-suite-performance/spec.md
git add specs/1505-test-suite-performance/tasks.md
git commit -m "docs: record bounded test workflow (#1505)"
```

- [ ] **Step 9: Review the complete branch before integration**

Inspect:

```powershell
git diff e9099980..HEAD --stat
git diff e9099980..HEAD -- BookOfEternityClient
git diff e9099980..HEAD -- BookOfEternityClient.Tests BookOfEternityClient.IntegrationTests BookOfEternityClient.TestSupport
git log --oneline e9099980..HEAD
```

Reject any review finding that changes gameplay semantics, increases a production timeout, weakens an assertion, creates a reverse test-project reference, or permits overlapping PreMerge filters.

- [ ] **Step 10: Push, create the issue-linked PR, and merge**

Run:

```powershell
git push -u origin work/1505-test-suite-performance
$prUrl = gh pr create --base main --head work/1505-test-suite-performance --title "test: isolate and optimize C# verification lanes" --body "Closes #1505`n`n- keeps the default C# suite under five minutes`n- moves slow tests into an explicit integration assembly`n- adds a globally bounded PreMerge control"
gh pr merge $prUrl --merge --delete-branch=false
gh pr view $prUrl --json state,mergeCommit,url
```

Expected: PR state is `MERGED` and issue #1505 is closed by the merge.

- [ ] **Step 11: Verify local main and remote main agree**

Run:

```powershell
git -C "E:\Games\The Book of Eternity Reborn" pull --ff-only
$localMain = git -C "E:\Games\The Book of Eternity Reborn" rev-parse main
$remoteMain = git -C "E:\Games\The Book of Eternity Reborn" rev-parse origin/main
if ($localMain -ne $remoteMain) {
    throw "Local main does not match origin/main after merge."
}
gh issue view 1505 --json state,url
```

Expected: hashes match and issue state is `CLOSED`.

---

## Acceptance Checklist

- [ ] `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj` cannot discover integration tests.
- [ ] TestSupport contains no `Microsoft.NET.Test.Sdk` or xUnit dependency.
- [ ] IntegrationTests does not reference Tests.
- [ ] Every partial test class belongs to exactly one project.
- [ ] Fast source contains no slow category and no direct parameterless full validation.
- [ ] The reviewed broad-validation manifest has exactly seven direct call sites and stays below the budget of eight.
- [ ] Two consecutive Fast runs pass below five minutes each.
- [ ] One PreMerge run passes below fifteen minutes with zero duplicate test IDs.
- [ ] PreMerge uses one deadline across frontend verification, builds, tests, and cleanup.
- [ ] Production solution builds with zero warnings and errors.
- [ ] Serena health-check is green after re-indexing.
- [ ] `.serena/` is not committed.
- [ ] GitHub issue #1505 closes through the merged PR.
