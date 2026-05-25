# Issue #683 Browser Contextual Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a player-facing, realm-aware Browser Client action menu so normal players use Russian game sections and guided forms instead of a slash-command palette.

**Architecture:** C# remains the command/gameplay authority. A new C# action-menu projection builds `BrowserPlayerCommandMenuDto` from `ExplorerCommandCatalog`, `AggregatedGameState`, lifecycle, and QTE state; React only renders the typed DTO and keeps raw slash commands behind advanced mode.

**Tech Stack:** .NET 8, xUnit, React 19, TypeScript strict mode, Vite.

---

## File structure

- Create `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs`: C# projection from Explorer command descriptors into player-facing menu sections/actions.
- Modify `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`: include `ActionMenu` in `BrowserGameScreenDto`.
- Modify `BookOfEternityClient.WebFrontend/src/api/contracts.ts`: add TypeScript interfaces for the action menu DTO and `actionMenu` field.
- Modify `BookOfEternityClient.Tests/BrowserApiContractTests.cs`: fixture and guard assertions for action menu contracts.
- Modify `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`: source guard assertions for player UI/advanced separation.
- Modify `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`: smoke assertions that built source includes player action menu labels/forms.
- Modify `BookOfEternityClient.WebFrontend/src/App.tsx`: render action sections/cards/forms in `WorldRoute`; hide advanced-only actions unless advanced mode is explicit.
- Modify `BookOfEternityClient.WebFrontend/src/styles.css`: styles for action sections, availability badges, and guided forms.
- Modify `BookOfEternityClient.WebFrontend/README.md` and `docs/web-ui/local-web-host.md`: document #683 player action menu boundary.

---

### Task 1: Add failing contract tests for the C# action menu DTO

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`

- [ ] **Step 1: Write failing tests**

Add these tests after `FrontendShell_ConsumesTypedApiContractSummaryInsteadOfHardCodedEndpointList`:

```csharp
[Fact]
public void BrowserGameScreenContract_IncludesPlayerCommandActionMenu()
{
    var contracts = File.ReadAllText(Path.Combine(ApiRoot, "contracts.ts"));
    var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));

    Assert.Contains("actionMenu: BrowserPlayerCommandMenuDto", contracts, StringComparison.Ordinal);
    Assert.Contains("export interface BrowserPlayerCommandMenuDto", contracts, StringComparison.Ordinal);
    Assert.Contains("export interface BrowserPlayerCommandSectionDto", contracts, StringComparison.Ordinal);
    Assert.Contains("export interface BrowserPlayerCommandActionDto", contracts, StringComparison.Ordinal);
    Assert.Contains("realmAvailability", contracts, StringComparison.Ordinal);
    Assert.Contains("mutationWarning", contracts, StringComparison.Ordinal);
    Assert.Contains("formPrompt", contracts, StringComparison.Ordinal);

    Assert.Contains("ActionMenu", app, StringComparison.Ordinal);
    Assert.Contains("Персонаж / Душа", app, StringComparison.Ordinal);
    Assert.Contains("Подготовить форму", app, StringComparison.Ordinal);
}

[Fact]
public void EveryBrowserExecutableCommand_HasPlayerFacingActionMetadata()
{
    var menu = BrowserPlayerCommandMenuBuilder.Build(
        BuildRepresentativeState(isChaosSea: false, isShiningAbode: false, isAfterlife: false),
        BuildLifecycleDashboard(),
        BuildQteState());

    var actions = menu.Sections.SelectMany(static section => section.Actions).ToArray();
    var actionIds = actions.Select(static action => action.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var descriptor in ExplorerCommandCatalog.Descriptors.Where(static descriptor => ExplorerCommandMigrationRegistry.IsBrowserExecutable(descriptor.BrowserStatus)))
    {
        Assert.Contains(descriptor.Id, actionIds);
    }

    Assert.All(actions, action =>
    {
        Assert.False(string.IsNullOrWhiteSpace(action.Label));
        Assert.False(string.IsNullOrWhiteSpace(action.Description));
        Assert.False(string.IsNullOrWhiteSpace(action.RealmAvailability));
        Assert.False(string.IsNullOrWhiteSpace(action.MutationWarning));
        Assert.DoesNotContain('/', action.Label);
        Assert.DoesNotContain("endpoint", action.Label, StringComparison.OrdinalIgnoreCase);
    });
}

[Fact]
public void PlayerCommandActionMenu_SeparatesAdvancedAndContextualRealmActions()
{
    var mortalMenu = BrowserPlayerCommandMenuBuilder.Build(
        BuildRepresentativeState(isChaosSea: false, isShiningAbode: false, isAfterlife: false),
        BuildLifecycleDashboard(),
        BuildQteState());
    var chaosMenu = BrowserPlayerCommandMenuBuilder.Build(
        BuildRepresentativeState(isChaosSea: true, isShiningAbode: false, isAfterlife: true),
        BuildLifecycleDashboard(),
        BuildQteState());
    var shiningMenu = BrowserPlayerCommandMenuBuilder.Build(
        BuildRepresentativeState(isChaosSea: false, isShiningAbode: true, isAfterlife: true),
        BuildLifecycleDashboard(),
        BuildQteState());

    Assert.Contains(mortalMenu.Sections, section => section.Label == "Мир");
    Assert.Contains(mortalMenu.Sections, section => section.Label == "Расширенный режим" && !section.PlayerDefault);
    Assert.Contains(FindAction(mortalMenu, "debug").SectionId, value => value == "advanced");
    Assert.False(FindAction(mortalMenu, "chaos_sea").Enabled);
    Assert.True(FindAction(chaosMenu, "chaos_sea").Enabled);
    Assert.True(FindAction(shiningMenu, "source_of_light").Enabled);
    Assert.Contains("активный духовный конфликт", FindAction(shiningMenu, "spiritual_action").DisabledReason, StringComparison.OrdinalIgnoreCase);
}
```

Add helper methods near the bottom of the test class:

```csharp
private static BrowserPlayerCommandActionDto FindAction(BrowserPlayerCommandMenuDto menu, string id) =>
    menu.Sections.SelectMany(static section => section.Actions).Single(action => string.Equals(action.Id, id, StringComparison.OrdinalIgnoreCase));

private static AggregatedGameState BuildRepresentativeState(bool isChaosSea, bool isShiningAbode, bool isAfterlife) =>
    new()
    {
        SoulName = "Арион",
        CharacterName = "Арион",
        CharacterClass = "Следопыт",
        CharacterRace = "Человек",
        CurrentRealm = isShiningAbode ? "Shining Abode" : isChaosSea ? "Chaos Sea" : "Mortal World",
        CurrentLocation = isShiningAbode ? "Зал Света" : isChaosSea ? "Море Хаоса" : "Пепельная дорога",
        WorldTime = "Сумерки",
        SessionId = "contract",
        IsInChaosSea = isChaosSea,
        IsInShiningAbode = isShiningAbode,
        IsInAnyShiningAbodeState = isShiningAbode,
        IsInAfterlifeRealm = isAfterlife,
        CanReenterShiningAbode = isChaosSea
    };
```

- [ ] **Step 2: Run RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserGameScreenContract_IncludesPlayerCommandActionMenu|FullyQualifiedName~EveryBrowserExecutableCommand_HasPlayerFacingActionMetadata|FullyQualifiedName~PlayerCommandActionMenu_SeparatesAdvancedAndContextualRealmActions" --logger "console;verbosity=minimal"
```

Expected: FAIL because `BrowserPlayerCommandMenuBuilder` and DTOs do not exist yet.

- [ ] **Step 3: Commit not yet**

Do not commit RED-only tests; implement Task 2 first.

---

### Task 2: Implement C# player command action menu projection

**Files:**
- Create: `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs`
- Modify: `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
- Modify: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`

- [ ] **Step 1: Create builder and DTO records**

Create a builder that:

- Emits required sections: `soul`, `world`, `quests`, `map`, `factions`, `guardians`, `afterlife`, `combat`, `archive`, `settings`, `advanced`.
- Adds every browser-executable descriptor exactly once by descriptor ID.
- Gives every action a Russian label, description, realm availability, mutation warning, form label, form prompt, and internal advanced command.
- Marks advanced-only commands (`debug`, `gm`, `validate`, `mods`, `system_guardians`, `math`, `help`) as `PlayerDefault = false`.
- Disables mutating actions when lifecycle/QTE prevents local writes.

- [ ] **Step 2: Add `ActionMenu` to `BrowserGameScreenDto`**

Change the record signature to include `BrowserPlayerCommandMenuDto ActionMenu` before `Flags`, and pass:

```csharp
ActionMenu: BrowserPlayerCommandMenuBuilder.Build(state, lifecycle, qte),
```

- [ ] **Step 3: Update representative fixture object**

In `BuildGameScreen()` in `BrowserApiContractTests.cs`, pass a representative action menu before `Flags`:

```csharp
ActionMenu: BrowserPlayerCommandMenuBuilder.Build(
    BuildRepresentativeState(isChaosSea: false, isShiningAbode: false, isAfterlife: false),
    BuildLifecycleDashboard(),
    BuildQteState()),
```

- [ ] **Step 4: Run GREEN for C# focused tests**

Run the Task 1 test command again.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs BookOfEternityClient/WebUi/BrowserGameScreenService.cs BookOfEternityClient.Tests/BrowserApiContractTests.cs
git commit -m "feat(web-ui): expose browser player action menu metadata"
```

---

### Task 3: Update TypeScript contract and React player action menu

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles.css`
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Modify: `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`

- [ ] **Step 1: Write RED source guard tests**

Extend `ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn()` with assertions:

```csharp
Assert.Contains("ActionMenu", app, StringComparison.Ordinal);
Assert.Contains("action-menu", styles, StringComparison.Ordinal);
Assert.Contains("Персонаж / Душа", app, StringComparison.Ordinal);
Assert.Contains("Подготовить форму", app, StringComparison.Ordinal);
Assert.Contains("mutationWarning", app, StringComparison.Ordinal);
Assert.DoesNotContain("action.advancedCommand}", app, StringComparison.Ordinal);
```

Extend the built frontend smoke source assertions with:

```csharp
Assert.Contains("ActionMenu", appSource, StringComparison.Ordinal);
Assert.Contains("Персонаж / Душа", appSource, StringComparison.Ordinal);
Assert.Contains("Подготовить форму", appSource, StringComparison.Ordinal);
Assert.DoesNotContain("action.advancedCommand}", appSource, StringComparison.Ordinal);
```

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn|FullyQualifiedName~BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics" --logger "console;verbosity=minimal"
```

Expected: FAIL before React rendering exists. The built smoke may also require a fresh frontend build before it can fully run; the source assertions should drive the failure.

- [ ] **Step 2: Add TypeScript interfaces**

Add `BrowserPlayerCommandMenuDto`, `BrowserPlayerCommandSectionDto`, and `BrowserPlayerCommandActionDto` to `contracts.ts`. Add `actionMenu: BrowserPlayerCommandMenuDto` to `BrowserGameScreenDto`.

- [ ] **Step 3: Render `ActionMenu`**

Update `WorldRoute` to render:

```tsx
<ActionMenu menu={game.actionMenu} />
```

Implement `ActionMenu`, `ActionSection`, and `ActionCard` components that:

- Render only `section.playerDefault === true` in the default UI.
- Show labels/descriptions/realm availability/mutation warnings.
- For actions with `formMode !== 'none'`, render a guided `<form>` with a textarea/select-style placeholder and a `Подготовить форму` button.
- Do not render `advancedCommand` in player-default UI.

- [ ] **Step 4: Add styles**

Add `.action-menu`, `.action-section-grid`, `.action-card`, `.availability-pill`, `.guided-form`, and responsive styling to `styles.css`.

- [ ] **Step 5: Verify GREEN**

Run:

```bash
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn|FullyQualifiedName~BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/api/contracts.ts BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/styles.css BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs
git commit -m "feat(web-ui): render browser contextual action menu"
```

---

### Task 4: Update browser docs and run full verification

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`
- Modify: `docs/superpowers/specs/2026-05-25-issue-683-browser-contextual-actions-design.md`
- Modify: `docs/superpowers/plans/2026-05-25-issue-683-browser-contextual-actions.md`

- [ ] **Step 1: Document #683 boundary**

Add short notes to README and local web host docs:

- #683 adds a player-facing contextual action menu from C# DTO metadata.
- Default UI hides slash commands/advanced command IDs.
- Mutating actions show guided forms and warnings; actual write execution remains governed by C# local-write/lifecycle services.

- [ ] **Step 2: Verify docs and whitespace**

Run:

```bash
git diff --check
```

Expected: no output, exit 0.

- [ ] **Step 3: Run relevant verification**

Run:

```bash
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "WebUi|LocalWebUi|ExplorerWeb|CommandMigration|BrowserApiContract|BrowserFrontendWorkspace" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 4: Security scan added lines**

Use the repository's standard added-line security scan from the `requesting-code-review` skill against the staged diff. For this issue the expected result is no code or documentation findings; if a documentation-only example is ever reported, rewrite the example so the scan remains clean.

Run:

```bash
git diff --cached > .hermes/issue-683-staged.diff
# Then run the standard added-line secret/injection/deserialization scan from requesting-code-review against .hermes/issue-683-staged.diff.
```

Expected: no findings.

- [ ] **Step 5: Commit docs/final verification metadata**

```bash
git add BookOfEternityClient.WebFrontend/README.md docs/web-ui/local-web-host.md docs/superpowers/specs/2026-05-25-issue-683-browser-contextual-actions-design.md docs/superpowers/plans/2026-05-25-issue-683-browser-contextual-actions.md
git commit -m "docs(web-ui): document browser contextual actions"
```

---

### Task 5: Review, PR, CI, merge, and close issue

**Files:**
- No direct file edits expected unless review/CI finds issues.

- [ ] **Step 1: Independent review**

Dispatch an independent reviewer with the final diff. Required verdict: no Critical or Important issues before PR/merge.

- [ ] **Step 2: Create PR**

```bash
git push -u origin task/683-browser-contextual-actions
gh pr create --title "feat(web-ui): add browser contextual action menu" --body-file .hermes/pr-683.md
```

PR body must include `Closes #683` only after all acceptance criteria are satisfied.

- [ ] **Step 3: Wait for CI**

```bash
gh pr checks --watch --interval 10
```

Expected: all checks PASS.

- [ ] **Step 4: Merge**

```bash
gh pr merge --squash --delete-branch
git checkout main
git pull --ff-only origin main
```

- [ ] **Step 5: Verify closure**

```bash
gh issue view 683 --json state,closedAt,url,title
```

Expected: `state` is `CLOSED`.
