# Browser Command Coverage Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close #687 by adding a read-only browser command coverage matrix that audits every Explorer command/subcommand and renders it in advanced browser UI.

**Architecture:** C# remains the source of command/runtime truth. A new `BrowserCommandCoverageService` builds a deterministic DTO from `ExplorerCommandCatalog` plus shared browser action metadata exposed by `BrowserPlayerCommandMenuBuilder`; `LocalWebUiHost` serves it at `GET /api/explorer/command-coverage`. React/TypeScript adds typed contracts/client support and renders the matrix only inside `AdvancedDiagnosticsPanel` after explicit advanced-mode opt-in.

**Tech Stack:** .NET 8 Minimal API, xUnit, React + TypeScript + Vite, handwritten browser API contract fixtures.

---

### Task 1: Add C# coverage contract tests

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`

- [ ] **Step 1: Write failing contract tests**

Add tests that call the not-yet-existing `BrowserCommandCoverageService.Build()` and assert:

```csharp
[Fact]
public void BrowserCommandCoverageContract_ListsEveryCommandAndUxDecision()
{
    var coverage = BrowserCommandCoverageService.Build();

    Assert.Equal(1, coverage.SchemaVersion);
    Assert.Equal(ExplorerCommandCatalog.Descriptors.Count, coverage.Commands.Count);
    Assert.Equal(ExplorerCommandCatalog.Descriptors.SelectMany(static descriptor => descriptor.Aliases).Count(), coverage.Summary.AliasCount);
    Assert.All(coverage.Commands, command =>
    {
        Assert.False(string.IsNullOrWhiteSpace(command.Id));
        Assert.NotEmpty(command.Aliases);
        Assert.False(string.IsNullOrWhiteSpace(command.Group));
        Assert.False(string.IsNullOrWhiteSpace(command.MutationMode));
        Assert.False(string.IsNullOrWhiteSpace(command.BrowserStatus));
        Assert.False(string.IsNullOrWhiteSpace(command.HandlerKind));
        Assert.False(string.IsNullOrWhiteSpace(command.UxDecision));
        Assert.False(string.IsNullOrWhiteSpace(command.Surface));
        Assert.False(string.IsNullOrWhiteSpace(command.FormMode));
        Assert.StartsWith("/", command.PrimaryCommand, StringComparison.Ordinal);
    });

    var saref = Assert.Single(coverage.Commands, command => command.Id == "saref_story");
    Assert.Equal("player-default", saref.Surface);
    Assert.Contains(saref.Subcommands, subcommand =>
        subcommand.Id == "find_wings" &&
        subcommand.BrowserStatus == nameof(ExplorerCommandMigrationStatus.MutatingParity) &&
        subcommand.UxDecision == "guided-form");

    foreach (var advancedId in new[] { "debug", "gm", "validate", "mods", "system_guardians", "math", "help" })
        Assert.Contains(coverage.Commands, command => command.Id == advancedId && command.Surface == "advanced-only");
}
```

Add host smoke test:

```csharp
[Fact]
[Trait("Category", "BrowserWebUiParity")]
public async Task CommandCoverageEndpoint_ReturnsMachineReadableExplorerParityMatrix()
{
    var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
    await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
    await app.StartAsync();

    using var client = new HttpClient { BaseAddress = new Uri(url) };
    var root = JsonNode.Parse(await client.GetStringAsync("/api/explorer/command-coverage"))!.AsObject();

    Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
    Assert.True(root["summary"]!["descriptorCount"]!.GetValue<int>() >= 1);
    var commands = root["commands"]!.AsArray();
    Assert.Contains(commands, node => node?["id"]?.GetValue<string>() == "saref_story");
    Assert.Contains(commands, node => node?["id"]?.GetValue<string>() == "validate" && node?["surface"]?.GetValue<string>() == "advanced-only");
}
```

- [ ] **Step 2: Run RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserCommandCoverageContract|FullyQualifiedName~CommandCoverageEndpoint" --logger "console;verbosity=minimal"
```

Expected: FAIL because `BrowserCommandCoverageService` and `/api/explorer/command-coverage` do not exist.

### Task 2: Implement C# coverage service and endpoint

**Files:**
- Create: `BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs`
- Modify: `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs`
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [ ] **Step 1: Expose shared coverage metadata**

In `BrowserPlayerCommandMenuBuilder`, add public helper `GetCoverageMetadata(ExplorerCommandDescriptor descriptor)` returning section id, label, player default flag, form mode, and UX decision. Use the existing private `Metadata` and `AdvancedOnlyIds` collections so the coverage matrix cannot drift from the action menu.

- [ ] **Step 2: Add deterministic DTO service**

Create `BrowserCommandCoverageService.Build()` that returns:

```csharp
public sealed record BrowserCommandCoverageDto(int SchemaVersion, BrowserCommandCoverageSummaryDto Summary, IReadOnlyList<BrowserCommandCoverageEntryDto> Commands);
public sealed record BrowserCommandCoverageSummaryDto(int DescriptorCount, int AliasCount, int SubcommandCount, int BrowserExecutableCount, int PlayerDefaultActionCount, int AdvancedOnlyActionCount, int MutatingCommandCount, int CommandsNeedingFollowUpCount);
public sealed record BrowserCommandCoverageEntryDto(string Id, IReadOnlyList<string> Aliases, string Group, string MutationMode, string BrowserStatus, string HandlerKind, string UxDecision, string Surface, string FormMode, string PrimaryActionLabel, string PrimaryCommand, IReadOnlyList<BrowserCommandSubcommandCoverageDto> Subcommands, string FollowUpIssue, string Reason);
public sealed record BrowserCommandSubcommandCoverageDto(string Id, IReadOnlyList<string> Aliases, string CanonicalCommand, string BrowserStatus, string UxDecision, string FollowUpIssue, string Reason);
```

- [ ] **Step 3: Register endpoint**

Register `BrowserCommandCoverageService` in DI and map:

```csharp
app.MapGet("/api/explorer/command-coverage", (BrowserCommandCoverageService coverage) => coverage.Build());
```

- [ ] **Step 4: Run GREEN for C#**

Run the RED command again. Expected: PASS.

### Task 3: Add TypeScript contracts, client, fixture, and advanced rendering

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
- Modify: `BookOfEternityClient.WebFrontend/src/api/client.ts`
- Modify: `BookOfEternityClient.WebFrontend/src/api/contract-fixture-checks.ts`
- Create: `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/command-coverage.json`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`

- [ ] **Step 1: Add failing frontend/source contract guards**

Extend existing tests in `BrowserApiContractTests` and `BrowserFrontendWorkspaceTests` so they require `BrowserCommandCoverageDto`, `getCommandCoverage`, `command-coverage`, lazy advanced-only fetching, and `CommandCoverageMatrix` rendering in `AdvancedDiagnosticsPanel`.

- [ ] **Step 2: Run RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~FrontendApiContractFiles|FullyQualifiedName~ReactAppShell" --logger "console;verbosity=minimal"
```

Expected: FAIL until TypeScript contract/client/rendering changes exist.

- [ ] **Step 3: Implement TypeScript DTOs/client**

Add the `BrowserCommandCoverage*` interfaces, `getCommandCoverage()` client method, endpoint descriptor, fixture import, and `satisfies BrowserCommandCoverageDto` guard.

- [ ] **Step 4: Render only in advanced mode**

Extend `BrowserShellState` with `commandCoverage: BrowserApiResult<BrowserCommandCoverageDto> | null`; fetch it only when `advancedEnabled` is true; pass it to `AdvancedDiagnosticsPanel`; render a compact matrix with id, UX decision, browser status, surface, form mode, and aliases.

- [ ] **Step 5: Update contract fixture**

Use the C# representative DTO from `BrowserApiContractTests` to write `command-coverage.json`, then run fixture and TypeScript checks.

### Task 4: Update docs and verify

**Files:**
- Modify: `docs/web-ui/local-web-host.md`
- Modify: `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs` if docs guard needs new endpoint assertion.

- [ ] **Step 1: Document endpoint and advanced-only behavior**

Add `GET /api/explorer/command-coverage` to the local web host endpoint list and explain that it is an advanced-only read-only audit matrix for issue #687.

- [ ] **Step 2: Run focused verification**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiParity|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests|FullyQualifiedName~CommandCoverageEndpoint" --logger "console;verbosity=minimal"
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
git diff --check
```

Expected: all commands exit 0.

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/2026-05-26-browser-command-coverage-parity-design.md docs/superpowers/plans/2026-05-26-browser-command-coverage-parity.md BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs BookOfEternityClient/WebUi/LocalWebUiHost.cs BookOfEternityClient.Tests/BrowserApiContractTests.cs BookOfEternityClient.Tests/LocalWebUiHostTests.cs BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.WebFrontend/src/api/contracts.ts BookOfEternityClient.WebFrontend/src/api/client.ts BookOfEternityClient.WebFrontend/src/api/contract-fixture-checks.ts BookOfEternityClient.WebFrontend/src/api/contract-fixtures/command-coverage.json BookOfEternityClient.WebFrontend/src/App.tsx docs/web-ui/local-web-host.md
git commit -m "feat(web-ui): add browser command coverage matrix"
```
