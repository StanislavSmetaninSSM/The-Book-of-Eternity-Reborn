# Afterlife Special Art Progression Defects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close issue #468 by making afterlife action-cost tiers authoritative, making learned special spiritual arts upgradeable, and extending afterlife entity progression to special arts and soul dissipation.

**Architecture:** Keep the existing afterlife split: runtime state projection in `AfterlifeEntityProfileState`, validation in `ValidationService.*`, and local player UI in `ExplorerMode.Afterlife.SpiritualConflict`. The fix must preserve GM-authored response surfaces but reject audits that claim cheaper costs or progression not backed by pre-turn authority. Entity strategy progression remains client-normalized and deterministic unless GM supplies explicit override deltas.

**Tech Stack:** C#/.NET 8, `System.Text.Json.Nodes`, xUnit, Spectre.Console, GitHub issue #468.

---

## File Structure

- Modify `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`
  - Add action-cost authority context from validated pre-turn `soul_state.json` and `afterlife_entity_profiles.json`.
  - Validate standard art `artTier` against `afterlifeCombatProfile.artTiers`.
  - Validate special-art usage against player-learned `specialArts[]` and cost multiplier.
- Modify `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs`
  - Include player-learned special arts in `/spiritual_arts`.
  - Upgrade learned special art tiers inside `afterlife_entity_profiles.json`.
  - Keep standard art upgrades in `soul_state.afterlifeCombatProfile.artTiers`.
- Modify `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`
  - Add deterministic strategy upgrades for special arts and `soulDissipationTier`.
  - Add GM override application for `specialArtTierDeltas` and `soulDissipationTierDelta`.
- Modify `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
  - Validate new override fields.
  - Validate `progressionStrategy.priorityOrder` values against known standard art ids, progression tracks, `soul_dissipation`, or profile `specialArts[].artId`.
- Modify docs and examples:
  - `OtherGuides/Afterlife_Contract_Matrix.md`
  - `CLI_Agent_Daemon_Specification.md`
  - `TaskGuides/CLI_Step_Main.txt`
  - `Examples/E_CLI_Afterlife_Turns.txt`
  - `Examples/example_validation_manifest.json` only if new manifest coverage is needed.
  - `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
- Modify tests:
  - `BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs`
  - `BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs`
  - `BookOfEternityClient.Tests/CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs`
  - `BookOfEternityClient.Tests/AfterlifeEntityProfileValidationTests.cs`

---

### Task 1: Action Cost Authority Validation

**Files:**
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`
- Test: `BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs`

- [ ] **Step 1: Write failing test for inflated standard art tier**

Add a test that creates a current exchange with `operationType="pressure"`, pre-turn `soul_state.afterlifeCombatProfile.artTiers.pressure = 0`, and `actionCostAudit.player.artTier = 5`. The test must expect `afterlife_conflict_action_cost_art_tier_authority_mismatch`.

```csharp
[Fact]
public async Task ValidateGameStateAsync_RejectsActionCostArtTierAboveAuthorityProfile()
{
    await WriteSoulStateAsync("Chaos Sea", inkFeathers: 0, afterlifeCombatProfileJson: """
    {
      "schemaVersion": 1,
      "enlightenmentRank": 1,
      "radianceRank": 0,
      "retainedRadianceRank": 0,
      "spiritFocusTier": 0,
      "lastRecoveryTurn": 0,
      "artTiers": { "pressure": 0 }
    }
    """);
    await WriteCurrentPressureExchangeWithActionCostAsync(artTier: 5, effectiveCost: 1);
    await WritePreTurnActiveConflictSnapshotAsync();

    var issues = await _validator.ValidateGameStateAsync();

    Assert.Contains(issues, issue =>
        string.Equals(issue.Code, "afterlife_conflict_action_cost_art_tier_authority_mismatch", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Write failing tests for special art authority**

Add one test where `specialArtAudit.artId="mirror_pressure"` is used without a player profile special art, expecting `afterlife_conflict_special_art_not_learned`. Add a second test where the player profile has that special art with tier `2` and multiplier `150`, and the exchange is accepted only when `actionCostAudit.player.artTier=2`, `specialCostMultiplierPercent=150`, and `standardEffectiveCost` matches the standard cost after tier reduction.

```csharp
[Fact]
public async Task ValidateGameStateAsync_RejectsSpecialArtCostWhenPlayerHasNotLearnedArt()
{
    await WriteSoulStateAsync();
    await WriteAfterlifeEntityProfilesAsync(playerSpecialArtsJson: "[]");
    await WriteCurrentSpecialPressureExchangeAsync(artId: "mirror_pressure", artTier: 2, multiplier: 150);
    await WritePreTurnActiveConflictSnapshotAsync();

    var issues = await _validator.ValidateGameStateAsync();

    Assert.Contains(issues, issue =>
        string.Equals(issue.Code, "afterlife_conflict_special_art_not_learned", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public async Task ValidateGameStateAsync_AcceptsSpecialArtCostFromLearnedPlayerProfile()
{
    await WriteSoulStateAsync();
    await WriteAfterlifeEntityProfilesAsync(playerSpecialArtsJson: """
    [
      {
        "artId": "mirror_pressure",
        "displayName": "Зеркальное Давление",
        "ownerActorType": "player_soul",
        "ownerActorId": "player_soul",
        "baseOperation": "pressure",
        "tier": 2,
        "costMultiplierPercent": 150,
        "upgradeCost": { "inkFeathers": 40, "lightSparks": 0 },
        "effectSummary": "Давление отражает часть обета."
      }
    ]
    """);
    await WriteCurrentSpecialPressureExchangeAsync(artId: "mirror_pressure", artTier: 2, multiplier: 150);
    await WritePreTurnActiveConflictSnapshotAsync();

    var issues = await _validator.ValidateGameStateAsync();

    Assert.DoesNotContain(issues, issue =>
        issue.Code?.StartsWith("afterlife_conflict_special_art", StringComparison.OrdinalIgnoreCase) == true ||
        string.Equals(issue.Code, "afterlife_conflict_action_cost_art_tier_authority_mismatch", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 3: Run tests and confirm failure**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "RejectsActionCostArtTierAboveAuthorityProfile|RejectsSpecialArtCostWhenPlayerHasNotLearnedArt|AcceptsSpecialArtCostFromLearnedPlayerProfile"
```

Expected: new tests fail because current validation trusts `actionCostAudit.player.artTier`.

- [ ] **Step 4: Implement authority context**

In `ValidationService.AfterlifeSpiritualConflict.cs`, add a small context record and resolver:

```csharp
private sealed record AfterlifeActionCostAuthorityContext(
    IReadOnlyDictionary<string, int> StandardArtTiers,
    IReadOnlyDictionary<string, JsonObject> PlayerSpecialArts);

private async Task<AfterlifeActionCostAuthorityContext> ResolveAfterlifeActionCostAuthorityContextAsync(
    ValidationPendingTurnSnapshotManifest? manifest)
{
    var soulJson = manifest == null
        ? await _fs.ReadFileAsync("game_state/meta/soul_state.json")
        : await ReadValidatedPendingTurnSnapshotFileAsync(manifest, "game_state/meta/soul_state.json");
    var standardTiers = ReadAfterlifeCombatProfileArtTiers(TryParseJsonObject(soulJson));

    var profileJson = manifest == null
        ? await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath)
        : await ReadValidatedPendingTurnSnapshotFileAsync(manifest, AfterlifeEntityProfileState.StatePath);
    var playerSpecialArts = ReadPlayerSpecialArts(TryParseJsonObject(profileJson));

    return new AfterlifeActionCostAuthorityContext(standardTiers, playerSpecialArts);
}
```

Use `manifest` so accepted-turn validation cannot be bypassed by same-turn edits.

- [ ] **Step 5: Validate standard art tiers**

Change `ValidateActionCostAudit(...)` to receive `AfterlifeActionCostAuthorityContext actionCostAuthority`. For non-special standard actions, compare `playerAudit.artTier` to `actionCostAuthority.StandardArtTiers[operationType]` defaulting to 0.

If mismatched, add:

```csharp
AddActionCostIssue(
    issues,
    $"{context}.actionCostAudit.player.artTier",
    "actionCostAudit.player.artTier должен совпадать с pre-turn afterlifeCombatProfile.artTiers для этого действия.",
    "afterlife_conflict_action_cost_art_tier_authority_mismatch",
    expectedAuthorityTier.ToString(),
    artTier.ToString());
```

- [ ] **Step 6: Validate special art authority**

When `specialArtAudit` exists:

```csharp
if (!actionCostAuthority.PlayerSpecialArts.TryGetValue(specialArtId, out var learnedArt))
{
    AddSpecialArtIssue(
        issues,
        $"{context}.specialArtAudit.artId",
        "Особое духовное искусство можно использовать только если оно есть в pre-turn профиле души игрока.",
        "afterlife_conflict_special_art_not_learned",
        "player_soul specialArts[] contains artId",
        specialArtId ?? "missing");
}
```

Then require `learnedArt.baseOperation == operationType`, `learnedArt.tier == actionCostAudit.player.artTier`, and `learnedArt.costMultiplierPercent == specialArtAudit.costMultiplierPercent`.

- [ ] **Step 7: Run targeted tests**

Run the command from Step 3. Expected: all three tests pass.

- [ ] **Step 8: Commit**

```powershell
git add BookOfEternityClient\Services\Validation\ValidationService.AfterlifeSpiritualConflict.cs BookOfEternityClient.Tests\AfterlifeSpiritualConflictValidationTests.cs
git commit -m "fix: validate afterlife action cost authority"
```

---

### Task 2: Learned Special Art Upgrade UI

**Files:**
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs`
- Test: `BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs`

- [ ] **Step 1: Write failing UI test**

Add a test that seeds `afterlife_entity_profiles.json` with a `player_soul` special art `mirror_guard`, opens `/spiritual_arts`, chooses that special art, confirms upgrade, and asserts the player profile special art tier increments and currency is spent.

```csharp
[Fact]
public async Task TryProcessCommand_SpiritualArts_UpgradesLearnedSpecialArtAndSpendsCurrency()
{
    await SeedAfterlifeStateAsync(inkFeathers: 120);
    await SeedPlayerSpecialArtProfileAsync(
        artId: "mirror_guard",
        displayName: "Зеркальная Защита",
        baseOperation: "guard",
        tier: 1,
        inkCost: 30,
        lightSparkCost: 0);

    _console.QueueSelection("Действие духовных искусств", "⬆ Прокачать духовное искусство");
    _console.QueueSelection("Выберите духовное искусство", "Зеркальная Защита — уровень 1->2, 30 🪶");
    _console.QueueSelection("Выберите валюту", "Чернильные Перья");
    _console.QueueConfirmation(true);
    _console.QueueKey(ConsoleKey.Enter);

    var result = await _explorer.TryProcessCommand("/spiritual_arts");

    Assert.Equal(string.Empty, result);
    var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
    var player = root["profiles"]!.AsArray().OfType<JsonObject>()
        .Single(profile => profile["actorType"]?.GetValue<string>() == "player_soul");
    var art = player["specialArts"]!.AsArray().OfType<JsonObject>()
        .Single(item => item["artId"]?.GetValue<string>() == "mirror_guard");
    Assert.Equal(2, art["tier"]?.GetValue<int>());
}
```

- [ ] **Step 2: Run test and confirm failure**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TryProcessCommand_SpiritualArts_UpgradesLearnedSpecialArtAndSpendsCurrency"
```

Expected: fails because `/spiritual_arts` does not list learned special arts.

- [ ] **Step 3: Add quote model support for special arts**

Extend `SpiritualArtUpgradeQuote` with:

```csharp
string? SpecialArtId,
JsonObject? SpecialArtProfile,
bool IsSpecialArt
```

For standard arts, set these to `null`, `null`, `false`.

- [ ] **Step 4: Load player special arts**

In `ShowSpiritualArtsAsync`, after `profile` is built, read `AfterlifeEntityProfileState.StatePath` and find `actorType=player_soul`. Merge learned special arts into quotes:

```csharp
var entityProfilesRoot = await ReadJsonObjectForAfterlifeStatusAsync(AfterlifeEntityProfileState.StatePath);
var playerSpecialArts = ReadPlayerLearnedSpecialArts(entityProfilesRoot);
var quotes = BuildSpiritualArtUpgradeQuotes(profile, playerSpecialArts);
```

- [ ] **Step 5: Build special art quote**

For each learned special art:

```csharp
var artId = AfterlifeEntityProfileState.GetNodeString(art["artId"]);
var baseOperation = AfterlifeEntityProfileState.GetNodeString(art["baseOperation"]);
var currentTier = Math.Clamp(AfterlifeEntityProfileState.GetNodeInt(art["tier"]), 0, SpiritualArtMaxTier);
var upgradeCost = art["upgradeCost"] as JsonObject;
var inkCost = Math.Max(0, AfterlifeEntityProfileState.GetNodeInt(upgradeCost?["inkFeathers"]));
var sparkCost = Math.Max(0, AfterlifeEntityProfileState.GetNodeInt(upgradeCost?["lightSparks"]));
```

Use display label `"{displayName} — уровень {currentTier}->{nextTier}, {inkCost} 🪶"` and show that it is based on the Russian label of `baseOperation`.

- [ ] **Step 6: Apply special art upgrade**

When `quote.IsSpecialArt`, update `afterlife_entity_profiles.json` instead of `soul_state.afterlifeCombatProfile.artTiers`. Locate `player_soul.specialArts[].artId`, increment `tier`, append a `ledger[]` entry on the player profile with:

```json
{
  "reason": "special_art_local_upgrade",
  "summary": "Игрок локально прокачал особое духовное искусство.",
  "sourceSurface": "spiritual_arts_local_upgrade"
}
```

Spend the chosen currency through the existing soul/shining currency helpers.

- [ ] **Step 7: Block malformed profile state**

If `afterlife_entity_profiles.json` is malformed or missing the player profile while a special-art upgrade is selected, show a blocker and do not mutate currency.

- [ ] **Step 8: Run targeted UI tests**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TryProcessCommand_SpiritualArts"
```

Expected: all `/spiritual_arts` tests pass.

- [ ] **Step 9: Commit**

```powershell
git add BookOfEternityClient\UI\ExplorerMode\ExplorerMode.Afterlife.SpiritualConflict.cs BookOfEternityClient.Tests\ExplorerModeCommandTests.Afterlife.cs
git commit -m "feat: upgrade learned afterlife special arts"
```

---

### Task 3: Entity Auto-Progression for Special Arts and Soul Dissipation

**Files:**
- Modify: `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
- Test: `BookOfEternityClient.Tests/CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs`
- Test: `BookOfEternityClient.Tests/AfterlifeEntityProfileValidationTests.cs`

- [ ] **Step 1: Write failing normalizer tests**

Add tests:

```csharp
[Fact]
public async Task NormalizeAccumulatedStateAsync_AutoProgressionUpgradesSpecialArtByStrategy()
```

Seed a profile with `currencies.inkFeathers=30`, `specialArts[0].artId="mirror_guard"`, `tier=1`, `upgradeCost.inkFeathers=30`, and `progressionStrategy.priorityOrder=["mirror_guard"]`. After normalization, assert special art tier is `2`, `inkFeathers=0`, and `progressionLedger[].upgrades` contains `specialArt:mirror_guard:1->2`.

```csharp
[Fact]
public async Task NormalizeAccumulatedStateAsync_AutoProgressionUpgradesSoulDissipationByStrategy()
```

Seed `currencies.inkFeathers=100`, `soulDissipationTier=0`, `progressionStrategy.priorityOrder=["soul_dissipation"]`. After normalization, assert `soulDissipationTier=1`, `inkFeathers=50`, and ledger contains `soulDissipation:0->1`.

- [ ] **Step 2: Define soul dissipation upgrade cost**

In `AfterlifeEntityProfileState.cs`, add:

```csharp
private static CurrencyDelta ResolveSoulDissipationUpgradeCost(JsonObject profile, int nextTier)
{
    var isShining = IsShiningRealm(profile);
    return isShining
        ? new CurrencyDelta(30 * nextTier, 2 * nextTier)
        : new CurrencyDelta(50 * nextTier, 0);
}
```

This keeps soul dissipation expensive, allows Chaos-only Feather progression, and adds Spark pressure in Shining.

- [ ] **Step 3: Implement special art strategy upgrade**

In `ApplyStrategyUpgrade`, after standard arts and before progression tracks, add:

```csharp
if (TryUpgradeSpecialArt(profile, currencies, priority, ref spending, upgrades))
    return;
```

`TryUpgradeSpecialArt` finds `specialArts[].artId`, checks `tier < 5`, reads `upgradeCost.inkFeathers/lightSparks`, verifies affordability, spends both currencies, increments `tier`, and appends `specialArt:{artId}:{old}->{new}`.

- [ ] **Step 4: Implement soul dissipation strategy upgrade**

In `ApplyStrategyUpgrade`, recognize both `"soul_dissipation"` and `"soulDissipation"`:

```csharp
if (ConflictTokenEquals(priority, "soul_dissipation", "soulDissipation") &&
    TryUpgradeSoulDissipation(profile, currencies, ref spending, upgrades))
{
    return;
}
```

Use a local string comparison helper if `ConflictTokenEquals` is not available in this class.

- [ ] **Step 5: Extend GM overrides**

In `ApplyProgressionOverrides`, support:

```json
"specialArtTierDeltas": { "mirror_guard": 1 },
"soulDissipationTierDelta": 1
```

Clamp resulting tiers to `0..5`, append spending/summary to `progressionLedger`, and do not silently create a special art that does not exist.

- [ ] **Step 6: Validate new override fields**

In `ValidateAfterlifeEntityProgressionOverride`, accept and validate:

```csharp
var hasSpecialArtDeltas = item.TryGetProperty("specialArtTierDeltas", out var specialArtDeltas);
var hasSoulDissipationDelta = item.TryGetProperty("soulDissipationTierDelta", out var soulDissipationDelta);
```

Require at least one of the five delta groups. `specialArtTierDeltas` must be object with integer deltas. `soulDissipationTierDelta` must be integer in `-5..5`.

- [ ] **Step 7: Validate strategy priorities**

In `ValidateAfterlifeProfileProgressionStrategy`, reject a `priorityOrder` item unless it is:

- a standard art id,
- `enlightenment`,
- `radiance`,
- `soul_dissipation` or `soulDissipation`,
- an existing `specialArts[].artId` for the same profile.

Use issue code `afterlife_entity_profile_strategy_unknown_priority`.

- [ ] **Step 8: Run normalizer and validation tests**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "CanonicalStateNormalizerTests.AfterlifeEntityProfiles|AfterlifeEntityProfileValidationTests"
```

Expected: all pass.

- [ ] **Step 9: Commit**

```powershell
git add BookOfEternityClient\Services\AfterlifeEntityProfileState.cs BookOfEternityClient\Services\Validation\ValidationService.AfterlifeEntityProfiles.cs BookOfEternityClient.Tests\CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs BookOfEternityClient.Tests\AfterlifeEntityProfileValidationTests.cs
git commit -m "feat: progress special arts and soul dissipation"
```

---

### Task 4: Documentation and Examples

**Files:**
- Modify: `OtherGuides/Afterlife_Contract_Matrix.md`
- Modify: `CLI_Agent_Daemon_Specification.md`
- Modify: `TaskGuides/CLI_Step_Main.txt`
- Modify: `Examples/E_CLI_Afterlife_Turns.txt`
- Modify: `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`

- [ ] **Step 1: Update contract wording**

Document:

- `actionCostAudit.player.artTier` is not trusted from GM text; it must equal pre-turn authority.
- Special art use requires learned/player-owned `specialArts[]`.
- `/spiritual_arts` upgrades learned special arts locally.
- `progressionStrategy.priorityOrder` may target special art ids and `soul_dissipation`.
- `afterlifeEntityProgressionOverrides[]` may include `specialArtTierDeltas` and `soulDissipationTierDelta`.

- [ ] **Step 2: Update example 26**

Add or amend an example profile where:

```json
"progressionStrategy": {
  "strategyId": "strategy_guardian_mirror",
  "summary": "Сначала усиливает особую защиту, затем развеивание души.",
  "priorityOrder": ["mirror_guard", "soul_dissipation", "guard"]
}
```

Add an override example:

```json
"afterlifeEntityProgressionOverrides": [
  {
    "actorType": "guardian",
    "actorId": "guardian_mirror",
    "cycleKey": "chaos:7",
    "reason": "Хранитель изменил приоритеты после дуэли.",
    "summary": "GM forced progression upgraded special art and soul dissipation.",
    "specialArtTierDeltas": { "mirror_guard": 1 },
    "soulDissipationTierDelta": 1,
    "currencyDeltas": { "inkFeathers": -80 }
  }
]
```

- [ ] **Step 3: Update docs coverage test**

Add assertions in `AfterlifeEntityProfilesAreDocumentedForGm` for:

```csharp
Assert.Contains("specialArtTierDeltas", text, StringComparison.Ordinal);
Assert.Contains("soulDissipationTierDelta", text, StringComparison.Ordinal);
Assert.Contains("soul_dissipation", text, StringComparison.Ordinal);
Assert.Contains("actionCostAudit.player.artTier", matrix + examples + daemonSpec + taskGuide, StringComparison.Ordinal);
```

- [ ] **Step 4: Run documentation-sensitive tests**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

Expected: all pass.

- [ ] **Step 5: Commit**

```powershell
git add OtherGuides\Afterlife_Contract_Matrix.md CLI_Agent_Daemon_Specification.md TaskGuides\CLI_Step_Main.txt Examples\E_CLI_Afterlife_Turns.txt BookOfEternityClient.Tests\AfterlifeDocumentationCoverageTests.cs
git commit -m "docs: document afterlife special art progression authority"
```

---

### Task 5: Final Verification

**Files:**
- No code files unless previous task requires small corrections.

- [ ] **Step 1: Run focused afterlife tests**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeSpiritualConflictValidationTests|AfterlifeEntityProfileValidationTests|CanonicalStateNormalizerTests.AfterlifeEntityProfiles|TryProcessCommand_SpiritualArts|AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests"
```

Expected: all selected tests pass.

- [ ] **Step 2: Run full suite**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

Expected: full suite passes.

- [ ] **Step 3: Check task state and worktree**

```powershell
git status --short --branch
gh issue view 468 --json number,title,state
```

Expected: branch contains only intended tracked changes/commits; issue #468 is open until review/merge.

- [ ] **Step 4: Request review**

Ask for review over the branch. Review must explicitly cover:

- art-tier authority cannot be forged through `actionCostAudit`,
- learned special arts can be upgraded by the player,
- entity progression can upgrade special arts and soul dissipation,
- docs and examples match validation.

---

## Self-Review

- Spec coverage: The plan covers all three audit findings and includes tests for validation authority, UI upgradeability, entity auto-progression, GM overrides, and docs.
- Placeholder scan: No `TBD`, `TODO`, or unspecified "add tests" steps remain; every task includes concrete files and commands.
- Type consistency: New properties are consistently named `specialArtTierDeltas`, `soulDissipationTierDelta`, `soul_dissipation`, and `actionCostAudit.player.artTier`.

---

Plan linked to GitHub issue #468.
