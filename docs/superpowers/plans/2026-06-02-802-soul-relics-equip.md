# 2026-06-02 — #802 Soul Relics Browser Equip/Unequip Implementation Plan

> Companion to `docs/superpowers/specs/2026-06-02-802-soul-relics-equip-design.md`.
> Executes inline (no subagents); follows test-driven-development per task.

**Goal:** Add browser-side equip/unequip/inspect for soul relics (parity with console
`ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs`).

**Architecture:** New `SoulRelicEquipmentService` mirroring `InventoryEquipmentService` shape.
New write handlers in `BrowserAfterlifeWriteService`. New prompt sessions in
`ExplorerLifecycleLocalTurnCommandResultBuilder`. Catalog/help/menu updated. Tests added to
`ExplorerWebCommandServiceTests`.

**Tech Stack:** C# .NET 8, xUnit, System.Text.Json, existing `BrowserLocalWriteCoordinator`
rollback path.

**File structure:**

| File | Action | Purpose |
|---|---|---|
| `BookOfEternityClient/Services/SoulRelicEquipmentService.cs` | CREATE | Read/move relics between stored[] and equipped[] in `soul_state.json` |
| `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs` | MODIFY | Add `ApplySoulRelicEquipAsync` / `ApplySoulRelicUnequipAsync` |
| `BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs` | MODIFY | Add new command tokens to `RequiresLocalUiLock` |
| `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs` | MODIFY | Change `soul_relics` to LocalTurn; add `soul_relic_equip` / `soul_relic_unequip` |
| `BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs` | MODIFY | Add help rows for new commands |
| `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs` | MODIFY | Add menu entries |
| `BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs` | MODIFY | Update audit override |
| `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs` | MODIFY | Add `BuildSoulRelicEquipAsync` / `BuildSoulRelicUnequipAsync` |
| `BookOfEternityClient/UI/ExplorerUniversalMetaCommandResultBuilder.cs` | MODIFY | Wire `BuildSoulRelicActions` into the read-only section |
| `BookOfEternityClient/UI/ExplorerMode.cs` | MODIFY | Route `soul_relic_equip` / `soul_relic_unequip` to console interactive flow (no-op path) |
| `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs` | MODIFY | Add 8 new tests |
| `OtherGuides/Afterlife_Contract_Matrix.md` | MODIFY | Document new browser actions |
| `Examples/E_CLI_Afterlife_Turns.txt` | MODIFY | Add example browser turns |
| `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs` | VERIFY | Must pass |
| `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs` | VERIFY | Must pass |

---

## Task 1: Add `SoulRelicEquipmentService` (TDD)

**Files:**
- Create: `BookOfEternityClient/Services/SoulRelicEquipmentService.cs`
- Test: `BookOfEternityClient.Tests/Services/SoulRelicEquipmentServiceTests.cs`

### Step 1.1 — RED test: read context from soul_state.json
```csharp
[Fact]
public async Task ReadContextAsync_LoadsStoredAndEquippedRelics()
{
    var fs = new InMemoryFileSystem();
    await fs.WriteFileAsync("game_state/meta/soul_state.json", JsonSerializer.Serialize(new
    {
        soulRelics = new
        {
            stored = new[] { new { relicId = "r1", name = "Огненный Шар" } },
            equipped = new[] { new { relicId = "r2", name = "Ледяной Щит", gameplayStatus = new { currentSlot = "body" } } }
        }
    }));

    var ctx = await SoulRelicEquipmentService.ReadContextAsync(fs);

    Assert.NotNull(ctx);
    Assert.Single(ctx.Stored);
    Assert.Single(ctx.Equipped);
    Assert.Equal("r1", ctx.Stored[0].RelicId);
    Assert.Equal("body", ctx.Equipped[0].CurrentSlot);
}
```

### Step 1.2 — RED test: equip moves from stored to equipped
```csharp
[Fact]
public async Task EquipAsync_MovesRelicFromStoredToEquipped()
{
    var fs = await SeedSoulStateAsync(storedIds: new[] { "r1" }, equippedIds: Array.Empty<string>());
    var outcome = await SoulRelicEquipmentService.EquipAsync(fs, "r1", "mainHand");

    Assert.True(outcome.Success);
    var ctx = await SoulRelicEquipmentService.ReadContextAsync(fs);
    Assert.Empty(ctx.Stored);
    Assert.Single(ctx.Equipped);
    Assert.Equal("r1", ctx.Equipped[0].RelicId);
    Assert.True(ctx.Equipped[0].IsEquipped);
    Assert.Equal("mainHand", ctx.Equipped[0].CurrentSlot);
}
```

### Step 1.3 — RED test: equip fails when relic not in stored
```csharp
[Fact]
public async Task EquipAsync_WhenRelicNotInStored_ReturnsFailure()
{
    var fs = await SeedSoulStateAsync(storedIds: Array.Empty<string>(), equippedIds: new[] { "r1" });
    var outcome = await SoulRelicEquipmentService.EquipAsync(fs, "missing", "mainHand");

    Assert.False(outcome.Success);
    Assert.Contains("не найдена", outcome.Message);
}
```

### Step 1.4 — RED test: equip fails when already equipped
```csharp
[Fact]
public async Task EquipAsync_WhenAlreadyEquipped_ReturnsFailure()
{
    var fs = await SeedSoulStateAsync(storedIds: Array.Empty<string>(), equippedIds: new[] { "r1" });
    var outcome = await SoulRelicEquipmentService.EquipAsync(fs, "r1", "mainHand");

    Assert.False(outcome.Success);
    Assert.Contains("уже экипирована", outcome.Message);
}
```

### Step 1.5 — RED test: unequip moves back to stored
```csharp
[Fact]
public async Task UnequipAsync_MovesRelicFromEquippedToStored()
{
    var fs = await SeedSoulStateAsync(storedIds: Array.Empty<string>(), equippedIds: new[] { "r1" });
    var outcome = await SoulRelicEquipmentService.UnequipAsync(fs, "r1");

    Assert.True(outcome.Success);
    var ctx = await SoulRelicEquipmentService.ReadContextAsync(fs);
    Assert.Empty(ctx.Equipped);
    Assert.Single(ctx.Stored);
    Assert.Equal("r1", ctx.Stored[0].RelicId);
    Assert.False(ctx.Stored[0].IsEquipped);
}
```

### Step 1.6 — RED test: normalize legacy flat array
```csharp
[Fact]
public async Task ReadContextAsync_NormalizesLegacyFlatArray()
{
    var fs = new InMemoryFileSystem();
    await fs.WriteFileAsync("game_state/meta/soul_state.json", JsonSerializer.Serialize(new
    {
        soulRelics = new object[]
        {
            new { relicId = "r1", name = "А", gameplayStatus = new { equipped = false } },
            new { relicId = "r2", name = "Б", gameplayStatus = new { equipped = true, currentSlot = "head" } }
        }
    }));

    var ctx = await SoulRelicEquipmentService.ReadContextAsync(fs);

    Assert.Single(ctx.Stored);
    Assert.Single(ctx.Equipped);
    Assert.Equal("r1", ctx.Stored[0].RelicId);
    Assert.Equal("r2", ctx.Equipped[0].RelicId);
    Assert.Equal("head", ctx.Equipped[0].CurrentSlot);
}
```

### Step 1.7 — RED test: missing file returns null context
```csharp
[Fact]
public async Task ReadContextAsync_MissingFile_ReturnsNull()
{
    var fs = new InMemoryFileSystem();
    var ctx = await SoulRelicEquipmentService.ReadContextAsync(fs);
    Assert.Null(ctx);
}
```

### Step 1.8 — Implement `SoulRelicEquipmentService`

Mirror `InventoryEquipmentService` style:
- Constants: `SoulStatePath = "game_state/meta/soul_state.json"`.
- Records: `SoulRelicItem(RelicId, Name, IsEquipped, CurrentSlot)`, `SoulRelicEquipmentContext(Root, Stored, Equipped)`, `SoulRelicWriteOutcome(Success, Message, RelicId, RelicName)`.
- Static methods: `ReadContextAsync`, `EquipAsync(fs, relicIdOrName, slotKey)`, `UnequipAsync(fs, relicIdOrName)`, `ValidateEquipAsync`, `ValidateUnequipAsync`, `BuildActionId`, `FormatCommandArgument`, `ReadFirstCommandArgument`, `NormalizeLegacyFlatSoulRelics` (copied from console), `IsLegacyFlatRelicEquipped`, `RelicNodeMatches`.
- Implementation walks `stored[]` / `equipped[]` JsonArrays, mutates the in-memory `JsonNode`, sets `gameplayStatus.equipped` / `currentSlot`, then writes atomically.

Run: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~SoulRelicEquipmentServiceTests"`
Expected: 7 tests pass.

### Step 1.9 — Commit
```bash
git add BookOfEternityClient/Services/SoulRelicEquipmentService.cs BookOfEternityClient.Tests/Services/SoulRelicEquipmentServiceTests.cs
git commit -m "feat(services): add SoulRelicEquipmentService for stored/equipped moves"
```

---

## Task 2: Wire browser write handlers (TDD)

**Files:**
- Modify: `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs`
- Test: `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`

### Step 2.1 — RED test: submit prompt with equip answers moves relic
```csharp
[Fact]
public async Task SubmitPromptSessionAsync_SoulRelicEquip_MovesRelicToEquippedAndReleasesLock()
{
    // Seed soul_state.json with one stored relic
    // Build prompt session via ExecuteAsync_SoulRelics_AddsEquipAction
    // Submit { "confirm_soul_relic_write": true, "relic_id": "r1", "slot_key": "mainHand" }
    // Assert relic moved to equipped
}
```

### Step 2.2 — RED test: submit prompt with unequip answers moves back
```csharp
[Fact]
public async Task SubmitPromptSessionAsync_SoulRelicUnequip_MovesRelicToStoredAndReleasesLock()
{
    // Seed with one equipped relic
    // Submit { "confirm_soul_relic_write": true, "relic_id": "r1" }
    // Assert relic moved to stored
}
```

### Step 2.3 — RED test: confirm false → no mutation, session stays open
```csharp
[Fact]
public async Task SubmitPromptSessionAsync_SoulRelicEquip_WithoutConfirm_KeepsSessionOpen()
{
    // Submit { "confirm_soul_relic_write": false, ... }
    // Assert RequiresInput, no mutation
}
```

### Step 2.4 — RED test: GM turn blocks
```csharp
[Fact]
public async Task ExecuteAsync_SoulRelicEquip_WithActiveGmTurn_BlocksPromptSession()
{
    // Seed pending GM turn
    // Assert result.State == Pending
}
```

### Step 2.5 — Implement
- Add `ApplySoulRelicEquipAsync(command, answers, owner)` and `ApplySoulRelicUnequipAsync` in
  `BrowserAfterlifeWriteService.TryApplyAsync` switch.
- `Equip`: read `confirm_soul_relic_write`, `relic_id`, `slot_key`; call
  `SoulRelicEquipmentService.EquipAsync` through `ExecuteAsync` with `[SoulStatePath]`.
- `Unequip`: same with `relic_id` only; `SoulRelicEquipmentService.UnequipAsync`.
- Mirror the `confirm_inventory_write` shape from `BrowserMortalWorldWriteService`.

### Step 2.6 — Commit
```bash
git commit -am "feat(web): wire soul relic equip/unequip browser write handlers"
```

---

## Task 3: Add prompt sessions (TDD)

**Files:**
- Modify: `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs`
- Test: same

### Step 3.1 — RED test: equip action opens prompt with relic/slot/confirm
```csharp
[Fact]
public async Task ExecuteAsync_SoulRelicEquipAction_OpensPromptSessionWithRelicSlotAndConfirmation()
{
    // Assert result.Prompts has 3: relic selection, slot selection, confirmation
}
```

### Step 3.2 — RED test: list view shows equip/unequip actions
```csharp
[Fact]
public async Task ExecuteAsync_SoulRelics_AddsEquipAndUnequipActions()
{
    // Assert result.Actions has both labels: "Экипировать «X»" and "Снять «Y»"
}
```

### Step 3.3 — Implement
- Add `BuildSoulRelicEquipAsync(command, fs)`:
  - Read context, build selection prompts.
  - Relic selection from `ctx.Stored`.
  - Slot selection: hardcoded "mainHand" / "offHand" / "head" / "body" (or read
    `currentSlot` if any).
  - Confirmation `confirm_soul_relic_write`.
- Add `BuildSoulRelicUnequipAsync`: relic selection from `ctx.Equipped` + confirmation.
- Hook into `TryBuildAsync` switch.
- Add `RequiresLocalUiLock` entries in `ExplorerWebPromptSessionService`.

### Step 3.4 — Commit
```bash
git commit -am "feat(web): add soul relic equip/unequip prompt sessions"
```

---

## Task 4: Catalog / help / menu / coverage (TDD-light)

**Files:** all five

### Step 4.1 — Update `ExplorerCommandCatalog`
- `D("soul_relics", ... LocalTurn, UniversalMeta, ["/soul_relics", "/реликвии"], acceptsArguments: true)`
- Add `D("soul_relic_equip", LocalTurn, ..., ["/экипировать_реликвию", "/equip_relic"])`
- Add `D("soul_relic_unequip", LocalTurn, ..., ["/снять_реликвию", "/unequip_relic"])`

### Step 4.2 — Update help and menu rows
- Help: add 2 rows for the new commands.
- Menu: add 2 menu entries under `soul_relic_equip` / `soul_relic_unequip`.

### Step 4.3 — Update coverage audit
- Change `soul_relics` row to drop `#802` from `Tracked(...)`.

### Step 4.4 — Console route
- `ExplorerMode.cs`: route `("soul_relic_equip", ShowSoulRelics)`,
  `("soul_relic_unequip", ShowSoulRelics)` (same interactive flow as the parent command).

### Step 4.5 — Commit
```bash
git commit -am "feat(web): register soul_relic_equip/unequip in catalog, help, menu, coverage"
```

---

## Task 5: Afterlife documentation guardrail (AGENTS.md)

### Step 5.1 — Update `OtherGuides/Afterlife_Contract_Matrix.md`
- In the browser-write section, add:
  - `soul_relic_equip` — moves stored → equipped with slot
  - `soul_relic_unequip` — moves equipped → stored
- In the GM prompt section, add the corresponding entries.

### Step 5.2 — Update `Examples/E_CLI_Afterlife_Turns.txt`
- Add 2 example browser turns.

### Step 5.3 — Update `Examples/example_validation_manifest.json`
- Add the two new actionType entries if the manifest enumerates them.

### Step 5.4 — Run validation gate
```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```
Expected: all pass.

### Step 5.5 — Commit
```bash
git commit -am "docs(afterlife): document soul_relic_equip/unequip browser actions"
```

---

## Task 6: Final verification

### Step 6.1 — Full test run
```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiSmoke|Category=BrowserWebUiParity|Inventory|SoulRelic|Equip|Unequip|BrowserMortalWorldWrite|BrowserWebUi|SoulRelicEquipmentServiceTests"
```
Expected: all relevant pass; pre-existing 4 environment failures stay (not related).

### Step 6.2 — Frontend verify
```bash
cd BookOfEternityClient.WebFrontend && npm run verify
```
Expected: typecheck + 23+ vitest + vite build all pass.

### Step 6.3 — Secret-pattern scan
```bash
git diff --check origin/main...HEAD
```
Expected: clean (exit 0). Plus: `grep -E "(api[_-]?key|token|secret).*=" -r BookOfEternityClient/` should be clean in changed lines.

### Step 6.4 — Self-review
- Re-read the spec coverage checklist in the design doc.
- Re-scan for placeholders in this plan.
- Run `git log --oneline origin/main..HEAD` and ensure the commit story is clean.

### Step 6.5 — Push & open PR
```bash
git push -u origin fix/802-web-soul-relics-equip
gh pr create --title "feat(web): soul relic equip/unequip prompts (#802)" --body "Closes #802. ..."
```

### Step 6.6 — Merge
```bash
gh pr merge <N> --squash --delete-branch
```

---

## After #802 lands
Move to #806 (inventory drop/split/stack) and #807 (NPC start talk) using the same template.
