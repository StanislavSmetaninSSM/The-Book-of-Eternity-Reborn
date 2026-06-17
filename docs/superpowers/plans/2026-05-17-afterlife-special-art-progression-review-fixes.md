# Afterlife Special Art Progression Review Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close post-review defects in issue #468 so afterlife special arts, soul dissipation, and progression overrides cannot bypass authority or become unusable through valid contract shapes.

**Architecture:** Keep fixes inside the existing afterlife contract layers: projection in `AfterlifeEntityProfileState`, validation in `ValidationService.*`, and player-local upgrades in `ExplorerMode.Afterlife.SpiritualConflict`. Authority-sensitive checks must use validated pre-turn snapshots when an accepted-turn manifest exists, while UI-only upgrades must remain local client-owned writes. GM-facing docs must be updated whenever runtime contract shape or allowed special-art usage changes.

**Tech Stack:** C#/.NET 8, `System.Text.Json.Nodes`, xUnit, Spectre.Console, existing afterlife validation/normalization patterns, GitHub issue #468.

---

## File Structure

- Modify `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`
  - Split current soul state from pre-turn authority profiles for `soulDissipationProof`.
  - Validate player-owned and non-player-owned `specialArtAudit` against pre-turn `afterlife_entity_profiles.json`.
  - Keep player action-cost multiplier checks only for player-owned special arts.
- Modify `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
  - Reject invalid progression override markers from projection.
  - Reject `specialArtTierDeltas` that target an unknown special art when authority can be resolved from the same root.
- Modify `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`
  - Make progression override projection fail closed for unknown target profiles or unknown `specialArtTierDeltas`.
  - Preserve an invalid marker that validation reports instead of silently ignoring no-op deltas.
- Modify `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs`
  - Allow learned special arts whose `upgradeCost` is Spark-only to be upgraded in Shining Abode.
  - Do not offer zero-cost Ink Feather upgrades for Spark-only arts.
- Modify docs/tests:
  - `BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs`
  - `BookOfEternityClient.Tests/AfterlifeEntityProfileValidationTests.cs`
  - `BookOfEternityClient.Tests/CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs`
  - `BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs`
  - `OtherGuides/Afterlife_Contract_Matrix.md`
  - `CLI_API_Specification.md`
  - `CLI_Agent_Daemon_Specification.md`
  - `TaskGuides/CLI_Step_Main.txt`
  - `Examples/E_CLI_Afterlife_Turns.txt`
  - `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`

---

### Task 1: Soul Dissipation Uses Pre-Turn Profile Authority

**Files:**
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`
- Test: `BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs`

- [ ] **Step 1: Write failing test for same-turn dissipation tier forgery**

Add a test where the validated pre-turn `afterlife_entity_profiles.json` has `guardian_liora.soulDissipationTier = 0`, but the current accepted state raises it to `5` and writes `recentConflicts[].soulDissipationProof.dissipationTier = 5` against the player.

Expected issue code: `afterlife_conflict_soul_dissipation_tier_mismatch`.

```csharp
[Fact]
public async Task ValidateGameStateAsync_RejectsSoulDissipationTierForgedBySameTurnProfileEdit()
{
    await WriteSoulDissipationProfileStateAsync(playerDissipationTier: 0, targetEnlightenmentTier: 1, oppositionDissipationTier: 5);
    await WriteSoulStateWithTerminalGameOverAsync(AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage);
    await WriteResolvedConflictWithSoulDissipationAsync("""
    {
      "proofId": "soul_dissipation_proof_player_death_002",
      "actorType": "guardian",
      "actorId": "guardian_liora",
      "targetActorType": "player_soul",
      "targetActorId": "player_soul",
      "dissipationTier": 5,
      "targetStabilityCoefficient": 1,
      "resolvedAtTurn": 7,
      "outcome": "soul_dispersed",
      "gmMotivation": "Лиора решила окончательно уничтожить душу после победы."
    }
    """, playerOutcome: "lost", resolutionKind: "player_loss");
    await WriteValidatedSnapshotManifestAsync(
        "pre-turn soul dissipation authority",
        "Душа проигрывает конфликт.",
        (AfterlifeEntityProfileState.StatePath, BuildSoulDissipationProfilesJson(oppositionDissipationTier: 0, targetEnlightenmentTier: 1)));

    var issues = await _validator.ValidateGameStateAsync();

    Assert.Contains(issues, issue =>
        string.Equals(issue.Code, "afterlife_conflict_soul_dissipation_tier_mismatch", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Write failing test for same-turn target coefficient lowering**

Add a test where pre-turn player profile has `targetStabilityCoefficient = 4`, current profile lowers progression to `0`, and proof claims `targetStabilityCoefficient = 0`.

Expected issue code: `afterlife_conflict_soul_dissipation_target_coefficient_mismatch`.

```csharp
[Fact]
public async Task ValidateGameStateAsync_RejectsSoulDissipationTargetCoefficientLoweredBySameTurnProfileEdit()
{
    await WriteSoulDissipationProfileStateAsync(playerDissipationTier: 0, targetEnlightenmentTier: 0, oppositionDissipationTier: 5);
    await WriteSoulStateWithTerminalGameOverAsync(AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage);
    await WriteResolvedConflictWithSoulDissipationAsync("""
    {
      "proofId": "soul_dissipation_proof_player_death_002",
      "actorType": "guardian",
      "actorId": "guardian_liora",
      "targetActorType": "player_soul",
      "targetActorId": "player_soul",
      "dissipationTier": 5,
      "targetStabilityCoefficient": 0,
      "resolvedAtTurn": 7,
      "outcome": "soul_dispersed",
      "gmMotivation": "Лиора решила окончательно уничтожить душу после победы."
    }
    """, playerOutcome: "lost", resolutionKind: "player_loss");
    await WriteValidatedSnapshotManifestAsync(
        "pre-turn soul stability authority",
        "Душа проигрывает конфликт.",
        (AfterlifeEntityProfileState.StatePath, BuildSoulDissipationProfilesJson(oppositionDissipationTier: 5, targetEnlightenmentTier: 4)));

    var issues = await _validator.ValidateGameStateAsync();

    Assert.Contains(issues, issue =>
        string.Equals(issue.Code, "afterlife_conflict_soul_dissipation_target_coefficient_mismatch", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 3: Run tests and verify red**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "RejectsSoulDissipationTierForgedBySameTurnProfileEdit|RejectsSoulDissipationTargetCoefficientLoweredBySameTurnProfileEdit"
```

Expected: both tests fail before the fix because `ResolveAfterlifeSoulDissipationContextAsync()` reads current profiles.

- [ ] **Step 4: Split current soul root from authority profiles**

Change `AfterlifeSoulDissipationContext` so `SoulRoot` remains current for `terminalGameOver`, but profile authority comes from pre-turn snapshot when present.

```csharp
private sealed record AfterlifeSoulDissipationContext(
    JsonObject? CurrentSoulRoot,
    IReadOnlyDictionary<string, JsonObject> AuthorityProfiles,
    bool UsesValidatedSnapshot);
```

Update references:

```csharp
if (soulDissipationContext.CurrentSoulRoot?[AfterlifeSpiritualConflictState.TerminalGameOverProperty] is not JsonObject gameOver)
```

and:

```csharp
return key != null && context.AuthorityProfiles.TryGetValue(key, out var profile)
    ? profile
    : null;
```

- [ ] **Step 5: Resolve profile authority from snapshot**

Update resolver:

```csharp
private async Task<AfterlifeSoulDissipationContext> ResolveAfterlifeSoulDissipationContextAsync(
    ValidationPendingTurnSnapshotManifest? manifest)
{
    var currentSoulRoot = await ReadJsonObjectAsync("game_state/meta/soul_state.json");
    var profileJson = manifest == null
        ? await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath)
        : await ReadValidatedPendingTurnSnapshotFileAsync(manifest, AfterlifeEntityProfileState.StatePath);
    var profiles = ReadAfterlifeEntityProfilesByIdentity(TryParseJsonObject(profileJson));
    return new AfterlifeSoulDissipationContext(currentSoulRoot, profiles, manifest != null);
}
```

Pass `gateContext.Manifest` from `ValidateAfterlifeSpiritualConflictStateAsync`.

- [ ] **Step 6: Run targeted tests and commit**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "SoulDissipation"
git add BookOfEternityClient\Services\Validation\ValidationService.AfterlifeSpiritualConflict.cs BookOfEternityClient.Tests\AfterlifeSpiritualConflictValidationTests.cs
git commit -m "fix: bind soul dissipation proof to pre-turn profiles"
```

---

### Task 2: Validate Non-Player Special Arts Without Breaking Player Cost Authority

**Files:**
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`
- Modify: GM-facing docs listed in File Structure
- Test: `BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs`
- Test: `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`

- [ ] **Step 1: Write failing test for valid Guardian special art in incoming action**

Use a `guard` exchange where the player guards against `incomingAction.operationType = "pressure"`, and the Guardian's incoming pressure uses `specialArtAudit` owned by `guardian_liora`.

Expected: no `afterlife_conflict_special_art_not_learned`, no `afterlife_conflict_special_art_base_operation_mismatch`, and no `afterlife_conflict_special_art_authority_mismatch`.

```csharp
[Fact]
public async Task ValidateGameStateAsync_AcceptsNonPlayerSpecialArtFromAuthorityProfile()
{
    await WriteSoulStateWithAfterlifeCombatProfileAsync("Chaos Sea", """
    {
      "schemaVersion": 1,
      "enlightenmentRank": 1,
      "radianceRank": 0,
      "retainedRadianceRank": 0,
      "spiritFocusTier": 0,
      "lastRecoveryTurn": 0,
      "artTiers": { "guard": 1 }
    }
    """);
    await WriteAfterlifeEntityProfilesWithGuardianSpecialArtAsync(
        actorId: "guardian_liora",
        artId: "mirror_pressure",
        baseOperation: "pressure",
        tier: 3,
        multiplier: 150);
    await WriteConflictStateWithRawExchangeAsync("""
    {
      "exchangeId": "exchange_guardian_special_art_001",
      "operationType": "guard",
      "outcome": "blocked",
      "incomingAction": { "operationType": "pressure", "actorType": "guardian", "actorId": "guardian_liora" },
      "before": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
      "after": { "playerSideStrain": "clear", "oppositionSideStrain": "clear", "conflictPosition": "contested" },
      "specialArtAudit": {
        "artId": "mirror_pressure",
        "displayName": "Зеркальное Давление",
        "ownerActorType": "guardian",
        "ownerActorId": "guardian_liora",
        "baseOperation": "pressure",
        "costMultiplierPercent": 150,
        "effectNote": "Давление Лиоры ударило зеркальной волной, но защита игрока удержала границу."
      },
      "actionCostAudit": {
        "player": {
          "operationType": "guard",
          "baseCost": 2,
          "minCost": 1,
          "artTier": 1,
          "effectiveCost": 1,
          "before": 6,
          "after": 5
        }
      }
    }
    """);
    await WritePreTurnActiveConflictSnapshotWithAuthorityAsync();

    var issues = await _validator.ValidateGameStateAsync();

    Assert.DoesNotContain(issues, issue =>
        issue.Code?.StartsWith("afterlife_conflict_special_art", StringComparison.OrdinalIgnoreCase) == true);
}
```

- [ ] **Step 2: Write failing tests for forged non-player special art**

Add one test where `ownerActorId` exists but profile lacks `artId`, expecting `afterlife_conflict_special_art_not_in_owner_profile`.

Add one test where `costMultiplierPercent` differs from the owner's profile, expecting `afterlife_conflict_special_art_authority_mismatch`.

```csharp
Assert.Contains(issues, issue =>
    string.Equals(issue.Code, "afterlife_conflict_special_art_not_in_owner_profile", StringComparison.OrdinalIgnoreCase));
```

- [ ] **Step 3: Run tests and verify red**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "NonPlayerSpecialArt|ForgedNonPlayerSpecialArt"
```

Expected: tests fail because current logic only looks up player learned arts and requires `specialArtAudit.baseOperation == exchange.operationType`.

- [ ] **Step 4: Replace player-only special art context with owner-aware context**

Extend authority context:

```csharp
private sealed record AfterlifeActionCostAuthorityContext(
    IReadOnlyDictionary<string, int> StandardArtTiers,
    IReadOnlyDictionary<string, JsonObject> ProfilesByIdentity);
```

Build keys with `AfterlifeEntityProfileState.BuildIdentityKey(profile)`.

Add helper:

```csharp
private static JsonObject? ResolveSpecialArtOwnerProfile(
    AfterlifeActionCostAuthorityContext authority,
    string? ownerActorType,
    string? ownerActorId)
{
    var key = BuildAfterlifeProfileIdentityKey(ownerActorType, ownerActorId);
    return key != null && authority.ProfilesByIdentity.TryGetValue(key, out var profile)
        ? profile
        : null;
}
```

- [ ] **Step 5: Distinguish player-owned and non-player-owned special arts**

Player-owned special arts:

```csharp
private static bool IsPlayerOwnedSpecialArt(JsonObject audit)
{
    var ownerActorType = AfterlifeSpiritualConflictState.GetNodeString(audit["ownerActorType"]);
    var ownerActorId = AfterlifeSpiritualConflictState.GetNodeString(audit["ownerActorId"]);
    return IsPlayerSoulActor(ownerActorType, ownerActorId);
}
```

For player-owned audits:
- require the art in the `player_soul` profile,
- use its tier for `actionCostAudit.player.artTier`,
- require `specialCostMultiplierPercent`, `specialArtId`, and `standardEffectiveCost`.

For non-player-owned audits:
- require matching owner profile,
- require matching `specialArts[].artId`,
- require matching `baseOperation` and `costMultiplierPercent`,
- require non-empty `effectNote`,
- do not multiply the player's `actionCostAudit.player.effectiveCost`.

- [ ] **Step 6: Allow base operation to match incoming action for non-player owners**

Replace the unconditional base-operation comparison with:

```csharp
var allowedOperations = ResolveAllowedSpecialArtBaseOperations(exchange, operationType, audit);
if (!allowedOperations.Any(value => ConflictTokenEquals(value, baseOperation)))
{
    AddSpecialArtIssue(... "afterlife_conflict_special_art_base_operation_mismatch" ...);
}
```

For player-owned audits, allowed operation is `exchange.operationType`.

For non-player-owned audits, allowed operations include:
- `incomingAction.operationType`,
- `incomingAction.finalOperationType`,
- `exchange.operationType` only when no `incomingAction` is present and the non-player is the acting side of that exchange.

- [ ] **Step 7: Update docs**

Document exact distinction:

- `specialArtAudit` may describe player-owned or non-player-owned special art.
- Player-owned special art affects `actionCostAudit.player` and must be learned pre-turn.
- Non-player special art must exist in the owner's pre-turn entity profile and usually maps to `incomingAction.operationType` / `finalOperationType`; it does not make player action cheaper or more expensive.
- All special art usage requires `effectNote`.

- [ ] **Step 8: Run targeted tests and commit**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeSpiritualConflictValidationTests|AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests"
git add BookOfEternityClient\Services\Validation\ValidationService.AfterlifeSpiritualConflict.cs BookOfEternityClient.Tests\AfterlifeSpiritualConflictValidationTests.cs OtherGuides\Afterlife_Contract_Matrix.md CLI_API_Specification.md CLI_Agent_Daemon_Specification.md TaskGuides\CLI_Step_Main.txt Examples\E_CLI_Afterlife_Turns.txt BookOfEternityClient.Tests\AfterlifeDocumentationCoverageTests.cs
git commit -m "fix: validate non-player afterlife special arts"
```

---

### Task 3: Support Spark-Only Learned Special Art Upgrades

**Files:**
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs`
- Test: `BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs`

- [ ] **Step 1: Write failing Shining UI test**

Seed Shining state with `lightSparks = 5`, player profile special art with `upgradeCost = { "inkFeathers": 0, "lightSparks": 2 }`, and choose `/spiritual_arts` upgrade.

Expected: special art tier increases and `lightSparks` decreases by 2.

```csharp
[Fact]
public async Task TryProcessCommand_SpiritualArts_UpgradesSparkOnlySpecialArtInShiningAbode()
{
    await SeedShiningSpiritualArtsStateAsync(lightSparks: 5, inkFeathers: 0);
    await SeedPlayerSpecialArtProfileAsync(
        artId: "radiant_guard",
        displayName: "Сияющая Защита",
        baseOperation: "guard",
        tier: 1,
        inkCost: 0,
        lightSparkCost: 2);
    await _stateManager.RefreshGameStateAsync();
    _console.QueueAnySelection("⬆ Прокачать духовное искусство");
    _console.QueueSelection("Выберите духовное искусство", "Сияющая Защита — уровень 1->2, 0 🪶 / 2 ✨");
    _console.QueueSelection("Выберите валюту", "Искры Света — 2 ✨");
    _console.QueueAnyConfirmResponse(true);

    var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_arts"));

    Assert.Null(ex);
    var shiningRoot = JsonNode.Parse(await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!.AsObject();
    Assert.Equal(3, shiningRoot["lightSparks"]?.GetValue<int>());
    var specialArt = ReadPlayerSpecialArt("radiant_guard");
    Assert.Equal(2, specialArt["tier"]?.GetValue<int>());
}
```

- [ ] **Step 2: Write failing Chaos blocker test**

Seed Chaos Sea with the same Spark-only art. `/spiritual_arts` should show the art as blocked with a clear Russian reason: `доступно только в Сияющей Обители, потому что цена указана только в Искрах Света`.

- [ ] **Step 3: Run tests and verify red**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "SparkOnlySpecialArt"
```

Expected: Shining test fails because `inkCost <= 0` blocks the quote.

- [ ] **Step 4: Change quote blocker logic**

Replace:

```csharp
else if (inkCost <= 0)
    blockReason = "у особого искусства должна быть положительная цена прокачки в Чернильных Перьях";
```

with:

```csharp
else if (inkCost <= 0 && sparkCost <= 0)
    blockReason = "у особого искусства должна быть положительная цена прокачки в Чернильных Перьях или Искрах Света";
else if (inkCost <= 0 && sparkCost > 0 && !_stateManager.CurrentState.IsInShiningAbode)
    blockReason = "цена указана только в Искрах Света; такая прокачка доступна только в обычной активной Сияющей Обители";
```

- [ ] **Step 5: Do not offer zero-cost currency choices**

Update special-art currency prompt:

```csharp
var choices = new List<string>();
if (quote.InkFeatherCost > 0)
    choices.Add($"Чернильные Перья — {quote.InkFeatherCost} 🪶");
if (_stateManager.CurrentState.IsInShiningAbode && shiningRoot != null && quote.LightSparkCost > 0)
    choices.Add($"Искры Света — {quote.LightSparkCost} ✨");
if (choices.Count == 0)
    return null;
choices.Add("← Назад");
```

Do not change standard art / spirit focus behavior unless the same zero-cost issue is present there.

- [ ] **Step 6: Run UI tests and commit**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TryProcessCommand_SpiritualArts"
git add BookOfEternityClient\UI\ExplorerMode\ExplorerMode.Afterlife.SpiritualConflict.cs BookOfEternityClient.Tests\ExplorerModeCommandTests.Afterlife.cs
git commit -m "fix: allow spark-only special art upgrades"
```

---

### Task 4: Fail Closed on Unknown Special Art Progression Overrides

**Files:**
- Modify: `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
- Test: `BookOfEternityClient.Tests/AfterlifeEntityProfileValidationTests.cs`
- Test: `BookOfEternityClient.Tests/CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs`

- [ ] **Step 1: Write failing validation test for full-root unknown special art delta**

```csharp
[Fact]
public async Task ValidateGameStateAsync_RejectsProgressionOverrideForUnknownSpecialArt()
{
    await WriteProfileStateAsync("""
    {
      "schemaVersion": 1,
      "profiles": [
        {
          "actorType": "guardian",
          "actorId": "guardian_mirror",
          "displayName": "Хранитель Зеркал",
          "realm": "Chaos Sea",
          "currencies": { "inkFeathers": 100, "lightSparks": 0 },
          "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
          "standardArts": { "guard": 0 },
          "specialArts": [],
          "customStates": [],
          "soulDissipationTier": 0,
          "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
          "ledger": []
        }
      ],
      "afterlifeEntityProgressionOverrides": [
        {
          "actorType": "guardian",
          "actorId": "guardian_mirror",
          "cycleKey": "chaos:9",
          "reason": "GM override.",
          "summary": "Опечатка в artId.",
          "specialArtTierDeltas": { "miror_guard": 1 }
        }
      ]
    }
    """);

    var issues = await _validator.ValidateGameStateAsync();

    Assert.Contains(issues, issue =>
        string.Equals(issue.Code, "afterlife_entity_profile_progression_override_unknown_special_art", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Write failing normalizer test for command-only unknown special art delta**

Seed previous/root backup with a Guardian profile that has `specialArts = []`, current file with only `afterlifeEntityProgressionOverrides[]` for `miror_guard`.

Expected after normalization: root contains invalid marker and validation reports it instead of silently removing the command.

```csharp
[Fact]
public async Task NormalizeAccumulatedStateAsync_PreservesInvalidOverrideMarkerForUnknownSpecialArtDelta()
{
    var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
    await SeedPreviousAfterlifeEntityProfileBackupAsync(profileWithNoSpecialArts: true);
    await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, """
    {
      "schemaVersion": 1,
      "afterlifeEntityProgressionOverrides": [
        {
          "actorType": "guardian",
          "actorId": "guardian_mirror",
          "cycleKey": "chaos:9",
          "reason": "GM override.",
          "summary": "Опечатка в artId.",
          "specialArtTierDeltas": { "miror_guard": 1 }
        }
      ]
    }
    """);

    await normalizer.NormalizeAccumulatedStateAsync();

    var root = JsonNode.Parse(await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
    Assert.True(root.ContainsKey("lastInvalidProgressionOverride"));
    Assert.Equal("unknown_special_art", root["lastInvalidProgressionOverrideReason"]?.GetValue<string>());
}
```

- [ ] **Step 3: Run tests and verify red**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "UnknownSpecialArt"
```

Expected: tests fail because validation accepts shape-only deltas and projector ignores unknown art ids.

- [ ] **Step 4: Build target profile lookup for validation**

In `ValidateAfterlifeEntityProfileStateFile`, build a lookup from any profile arrays present in the same root:

```csharp
var profileAuthority = BuildProfileAuthorityLookup(profiles, responseProfiles, updates);
ValidateAfterlifeEntityProgressionOverridesIfPresent(
    progressionOverrides,
    hasProgressionOverrides,
    $"{contextPrefix}.{AfterlifeEntityProfileState.ProgressionOverridesProperty}",
    profileAuthority,
    issues);
```

Then validate `specialArtTierDeltas` against the target profile if present:

```csharp
if (profileAuthority.TryGetValue(BuildIdentityKey(item), out var targetProfile))
    ValidateSpecialArtTierDeltaObject(specialArtDeltas, context, issues, ReadProfileSpecialArtIds(targetProfile));
```

Emit:

```csharp
"afterlife_entity_profile_progression_override_unknown_special_art"
```

- [ ] **Step 5: Add projection invalid marker**

In `AfterlifeEntityProfileState`, when applying overrides:

```csharp
if (profile == null)
{
    MarkInvalidProgressionOverride(result, overrideNode, "unknown_target_profile");
    continue;
}
```

For unknown special art deltas:

```csharp
if (specialArt == null)
{
    MarkInvalidProgressionOverride(result, overrideNode, "unknown_special_art");
    return false;
}
```

Marker shape:

```csharp
result["lastInvalidProgressionOverride"] = CloneObject(overrideNode);
result["lastInvalidProgressionOverrideReason"] = reason;
```

Do not apply partial deltas from the invalid override after the first unknown special art is found.

- [ ] **Step 6: Validate invalid marker**

At the start of `ValidateAfterlifeEntityProfileStateFile`, reject:

```csharp
if (root.TryGetProperty("lastInvalidProgressionOverride", out _))
{
    issues.Add(new ValidationIssue(
        $"{contextPrefix}.lastInvalidProgressionOverride",
        IssueSeverity.Error,
        "afterlifeEntityProgressionOverrides не был применён из-за некорректной authority-цели.",
        code: "afterlife_entity_profile_progression_override_invalid_authority",
        section: "AfterlifeEntityProfiles",
        expected: "valid target profile and known specialArtTierDeltas keys",
        actual: root.TryGetProperty("lastInvalidProgressionOverrideReason", out var reason) ? reason.ToString() : "invalid override"));
}
```

- [ ] **Step 7: Update docs**

Document that `specialArtTierDeltas` may only reference an existing `specialArts[].artId` on the target profile and unknown ids are repair-blocking, not no-op.

- [ ] **Step 8: Run tests and commit**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeEntityProfileValidationTests|CanonicalStateNormalizerTests.AfterlifeEntityProfiles|AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests"
git add BookOfEternityClient\Services\AfterlifeEntityProfileState.cs BookOfEternityClient\Services\Validation\ValidationService.AfterlifeEntityProfiles.cs BookOfEternityClient.Tests\AfterlifeEntityProfileValidationTests.cs BookOfEternityClient.Tests\CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs OtherGuides\Afterlife_Contract_Matrix.md CLI_API_Specification.md CLI_Agent_Daemon_Specification.md TaskGuides\CLI_Step_Main.txt Examples\E_CLI_Afterlife_Turns.txt BookOfEternityClient.Tests\AfterlifeDocumentationCoverageTests.cs
git commit -m "fix: reject unknown special art progression overrides"
```

---

### Task 5: Final Verification and Review

**Files:**
- No additional file changes expected.

- [ ] **Step 1: Run focused afterlife tests**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeSpiritualConflictValidationTests|AfterlifeEntityProfileValidationTests|CanonicalStateNormalizerTests.AfterlifeEntityProfiles|TryProcessCommand_SpiritualArts|AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests"
```

Expected: all selected tests pass.

- [ ] **Step 2: Run documentation-sensitive minimum required by AGENTS.md**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

Expected: all selected tests pass.

- [ ] **Step 3: Run full test suite**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

Expected: full suite passes.

- [ ] **Step 4: Review checklist**

Verify manually:

- `soulDissipationProof` uses pre-turn profile authority for tiers and target coefficients.
- Current `soul_state.terminalGameOver` is still read from current state, not pre-turn state.
- Player-owned special arts still require learned pre-turn player profile and scaled `actionCostAudit.player`.
- Non-player special arts validate owner profile and `effectNote`, but do not mutate player action cost.
- Spark-only special arts are upgradeable only in Shining Abode.
- Unknown `specialArtTierDeltas` cannot be silently ignored.
- GM-facing docs mention every changed afterlife contract rule.

- [ ] **Step 5: Request review**

Request branch review with these explicit prompts:

- Check same-turn authority bypasses for `soulDissipationProof`.
- Check player-vs-non-player `specialArtAudit` semantics.
- Check Spark-only upgrade UI and no zero-cost currency choice.
- Check invalid override marker cannot trap valid old saves.

---

## Self-Review

- Spec coverage: all four review findings are covered by separate tasks with failing tests before implementation.
- Placeholder scan: no `TBD`, `TODO`, or unspecified edge-case steps remain.
- Type consistency: new marker names are `lastInvalidProgressionOverride` and `lastInvalidProgressionOverrideReason`; issue codes use the `afterlife_entity_profile_*` and `afterlife_conflict_*` prefixes already used by the codebase.
- Contract guardrail: Tasks 2 and 4 explicitly include GM-facing documentation updates because they change afterlife runtime contracts.

---

Plan linked to GitHub issue #468.
