# Standard Wound Arts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Register Spiritual Resilience and Spiritual Healing as ordinary tier-0..V standard arts, including current profiles, ordinary progression/training and Russian client presentation.

**Architecture:** Extend the existing standard-art registries and scalar authorities; do not introduce an art experience subsystem. Fresh builders produce both zero entries, complete current profiles validate them, and the ordinary player training/direct-upgrade and persistent-entity strategy paths consume the same definitions. Player persistent profiles remain derived mirrors of soul state.

**Tech Stack:** C#/.NET 8, System.Text.Json, xUnit, existing console/browser read models, PowerShell 7 bounded C# lanes.

**Source issue:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536).
**Feature:** `specs/1536-complete-wound-materialization/{spec,plan,tasks,data-model}.md`, FR-040..042, T082/T093/T100.
**Approved clarification:** On 2026-09-07 the user explicitly confirmed that both arts develop like all other arts. The earlier art-schema question is resolved, not a reason to pause.

## Global Constraints

- Work in `E:/Games/worktrees/boe-1536-wound-materialization` on `1536-complete-wound-materialization`; do not create another branch/worktree.
- Source issue is #1536. Preserve unrelated `.serena/`; no remote writes, push, PR, merge, issue closure, or session cleanup.
- Both arts use scalar integer tiers 0..5. No per-art `experience`, object-tier schema, new progression pipeline, migration, legacy reader, mixed-schema fallback, or automatic missing-tier repair.
- `spiritual_resilience` is `Духовная стойкость`; `spiritual_healing` is `Духовное исцеление`. Both use ordinary baseline-art `MinUnlockTier = 1` and are visible at tier 0. Existing rank gates and currency costs are unchanged.
- Player authority is `game_state/meta/soul_state.json.afterlifeCombatProfile.artTiers`; persistent `player_soul.standardArts` is a mirror, excluded from automatic entity progression.
- Complete current `artTiers` and profile `standardArts` objects explicitly contain both new entries. Missing, null, non-integer, object-shaped, negative or >5 entries fail validation. Do not make old optional art keys mandatory in this slice.
- Resilience is passive and consumes no OD. Do not add either new ID to `OperationTypes` or `SpecialArtBaseOperations` in this registration slice; the explicit healing operation is T102.
- Registration is not the spiritual wound producer, active healing resolver, safe-cycle scheduler, `/раны`/`/лечить`, Elyara invariant, or faction healing-role implementation. Do not claim those mechanics are implemented here. T093's actual diagnosis/insufficiency workflow remains tracked with the resolver/command work if not exercised here.
- The unrelated T070 legacy-preparation authority question is not answered by this task and must remain untouched.
- Preserve existing capability/materialization semantics, source ownership, accepted history, receipts, player wallet authority and transaction behavior. A populated tier-zero art object is not an empty object and must have a matching materialization section disposition; zero tiers alone do not grant combat/teaching capability.
- New tests use the existing lane taxonomy: detached deterministic tests in Fast; filesystem/training/normalizer/command/validation integration tests in Integration. Do not add file-backed tests to Fast just because an older owner still lives there.
- PS7 `scripts/test-csharp.ps1` is the sole C# test entrypoint. One lane owner, no overlapping C# runs and no C# edits while a run is active. Use exact new/changed Focused owners first; one integrated Fast and one conditional FullValidation checkpoint. No PreMerge in this task.
- GM-facing afterlife guidance, worked examples, manifest and source guards change together with the new supported profile/art contract. Mortal wound/treatment rules do not change in this slice.
- Preserve the existing UI layout; only new-art labels/descriptions and the generic standard-art heading/copy change. Player text is Russian and must distinguish passive resilience from a selectable attack. No new buttons, frontend dependencies or design-system changes.

---

### Task 1: Complete the ordinary standard-art cutover

**Files — production owners:**

- `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs`: registry, stable IDs, shared fresh tier-zero object, default combat profile.
- `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`: recognized standard art IDs; reuse current mirror, override, strategy and currency pipelines.
- `BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs`: attach the default combat profile to freshly created soul state.
- `BookOfEternityClient/Services/SystemGuardianLibraryService.cs`: explicit zero entries in both system/freeform fresh Guardian profiles through their shared builder.
- `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs` and `ValidationService.AfterlifeEntityProfiles.cs`: require both current scalar entries using existing invalid-tier validation.
- `BookOfEternityClient/Services/TrainingService.cs`: Russian fallback names for authored mentor offers; keep existing generic offers, purchases and costs.
- `BookOfEternityClient/UI/ExplorerAfterlifeCombatCommandResultBuilder.cs` and `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs`: correct new-art names, passive/healing copy and help without changing the command/action protocol.

**Files — test/bootstrap/example owners:**

- Create `BookOfEternityClient.Tests/SpiritualHealingArtTests.cs` for detached registry/default/mirror/progression/cost tests, and `BookOfEternityClient.IntegrationTests/SpiritualHealingArtValidationTests.cs` if separate player/profile validation setup is clearer than adding to existing owners.
- Existing Integration owners: `AfterlifeEntityProfileValidationTests.cs`, `AfterlifeSpiritualConflictValidationTests.cs`, `CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs`, `SystemGuardianLibraryServiceTests.cs`, `TrainingServiceTests.cs`, `TrainingValidationTests.cs`, `ExplorerModeCommandTests.Afterlife.cs`, `ExplorerWebCommandServiceTestsSpiritualConflictArtDrilldowns.cs`, `ExplorerWebCommandServiceTests.cs`.
- `BookOfEternityClient.TestSupport/AfterlifeActorMaterializationTestFixture.cs` and Integration `WoundMaterializationTestFixtures.cs` / `WoundMaterializationTestFixturesTests.cs`: current scalar arts replace the obsolete future object proposal.
- Other existing positive profile fixture owners, only where actually needed for this cutover: Integration `ActorMaterializationValidationTests.cs`, `FactionMaterializationValidationTests.cs`, `ExplorerWebCommandServiceTestsAfterlifeProfileInboxDrilldowns.cs`, `SaveLoadServiceTests.cs`, `GmTurnHelperContractTests.cs`; Fast `ActorMaterializationContractTests.cs`, `LiveTurnPreparationServiceTests.cs`, `StateManagerTests.cs`, `TrainingWebCommandServiceTests.cs`, `WebUi/BrowserAfterlifeWriteServiceTests.cs`.
- `FileSystemExample/game_session/game_state/afterlife/entity_profiles/guardian_mirror.json` and `shining_senator_mirel.json`.
- Relevant current profile/soul entries in the two checked-in afterlife save archives if the diagnostic lane consumes them. Preserve unrelated entries/content; document every archive entry changed.
- `OtherGuides/Afterlife_Contract_Matrix.md`, `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`, `TaskGuides/CLI_Step_Main.txt`, `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/E_CLI_Training_Showcases.txt`, `Examples/example_validation_manifest.json`, Fast `AfterlifeDocumentationCoverageTests.cs`, Integration `ExampleDocumentationValidationTests.cs`.

**Interfaces:**

- Consumes current `SpiritualArtDefinition(string ArtId, string DisplayName, string MechanicalUse, int MinUnlockTier)`, `CreateDefaultCombatProfile()`, `ProjectCanonicalRoot(JsonObject?, JsonObject?, JsonObject?)`, `ApplyPlayerSoulProfileClientAuthority(JsonObject, JsonObject?, JsonObject?)`, and `AfterlifeTrainingCostPolicy` without changing their signatures.
- Produces two recognized scalar standard arts and one shared fresh-object helper, not a new writer or operation type.
- Existing soul state, complete profile update, `standardArtTierDeltas`, strategy `priorityOrder`, training receipts and `/spiritual_arts` remain the only relevant authority paths.

- [ ] **Step 1: Add semantic RED registry/default/cost tests before production edits.**

Create this detached core in `SpiritualHealingArtTests.cs` (extend it with the pure mirror/strategy cases in Step 2):

```csharp
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualHealingArtTests
{
    [Theory]
    [InlineData("spiritual_resilience", "Духовная стойкость")]
    [InlineData("spiritual_healing", "Духовное исцеление")]
    public void StandardWoundArt_IsVisibleAtZeroWithOrdinaryDefinition(string id, string name)
    {
        var art = Assert.Single(AfterlifeSpiritualConflictState.SpiritualArts,
            item => item.ArtId == id);
        Assert.Equal(name, art.DisplayName);
        Assert.Equal(1, art.MinUnlockTier);
        Assert.Contains(id, AfterlifeEntityProfileState.StandardArtIds);
        Assert.DoesNotContain(id, AfterlifeSpiritualConflictState.OperationTypes);
        Assert.DoesNotContain(id, AfterlifeEntityProfileState.SpecialArtBaseOperations);
        var profile = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
        var tiers = Assert.IsType<JsonObject>(profile["artTiers"]);
        Assert.Equal(0, Assert.IsAssignableFrom<JsonValue>(tiers[id]).GetValue<int>());
    }

    [Theory]
    [InlineData("spiritual_resilience", 1, 125, 8)]
    [InlineData("spiritual_resilience", 2, 175, 11)]
    [InlineData("spiritual_resilience", 3, 225, 14)]
    [InlineData("spiritual_resilience", 4, 275, 17)]
    [InlineData("spiritual_resilience", 5, 325, 20)]
    [InlineData("spiritual_healing", 1, 125, 8)]
    [InlineData("spiritual_healing", 2, 175, 11)]
    [InlineData("spiritual_healing", 3, 225, 14)]
    [InlineData("spiritual_healing", 4, 275, 17)]
    [InlineData("spiritual_healing", 5, 325, 20)]
    public void StandardWoundArt_UsesOrdinaryCosts(string id, int tier, int ink, int sparks)
    {
        var art = Assert.Single(AfterlifeSpiritualConflictState.SpiritualArts,
            item => item.ArtId == id);
        Assert.Equal(ink, AfterlifeTrainingCostPolicy.ComputeStandardArtBaseInkFeatherCost(art, tier));
        Assert.Equal(sparks, AfterlifeTrainingCostPolicy.ComputeStandardArtBaseLightSparkCost(art, tier));
        Assert.Equal(ink * 4, AfterlifeTrainingCostPolicy.ComputeSelfStandardArtInkFeatherCost(art, tier));
        Assert.Equal(sparks * 4, AfterlifeTrainingCostPolicy.ComputeSelfStandardArtLightSparkCost(art, tier));
        Assert.Equal(ink, AfterlifeTrainingCostPolicy.ComputeMentorCost(ink, 0));
        Assert.Equal(ink * 80 / 100, AfterlifeTrainingCostPolicy.ComputeMentorCost(ink, 30));
        Assert.Equal(ink * 60 / 100, AfterlifeTrainingCostPolicy.ComputeMentorCost(ink, 60));
    }

    [Fact]
    public void StandardWoundArts_DefaultProfilesAreDetached()
    {
        var first = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
        var second = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
        Assert.Equal(0, first["artTiers"]!["spiritual_healing"]?.GetValue<int>());
        first["artTiers"]!["spiritual_healing"] = 4;
        Assert.Equal(0, second["artTiers"]!["spiritual_healing"]?.GetValue<int>());
    }
}
```

Run `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~SpiritualHealingArtTests"`.
Expected semantic RED: missing registry definitions/default zero keys, not compile errors. Retain command, artifacts and individual failures in the task report.

- [ ] **Step 2: Add and observe RED at the real bootstrap, validation, progression and training boundaries.**

Use existing owner setup/helpers; give new methods the `StandardWoundArt` token so exact project-scoped filters select them. Complete deterministic JSON profiles use both zero entries; negative cases alter exactly one target after otherwise valid setup. Add these concrete rows:

| Boundary | Cases and exact assertion |
| --- | --- |
| Current profile validation | Both IDs at each 0..5 pass; missing, null, `"1"`, `true`, `1.5`, `-1`, `6`, `{ "tier": 1, "experience": 0 }` fail on the exact new-art field. Missing/non-object `artTiers` fails on that object; no normalization silently supplies the missing tier. |
| Fresh bootstrap | System and freeform Guardian builders contain both scalar zeros and retain prior guard/maneuver tiers/materialization. Fresh soul builder uses `CreateDefaultCombatProfile`; use an existing fresh-New-Game owner or a narrow existing source guard for that call site rather than driving an unrelated interactive process. |
| Player mirror | For both IDs at 0..5, `ApplyPlayerSoulProfileClientAuthority` copies the scalar from soul state, leaves no independent player art XP, disables automatic strategy and removes stale automatic ledger entries. The player's tiers/currency never grow from an NPC cycle. |
| Entity ordinary strategy | For each art with exact `priorityOrder=[id]`, sufficient currency and a single accepted Chaos/Guardian/Resident or Shining cycle, tier 0..4 grows by one and spends `10*(oldTier+1)` Ink Feathers; tier 5 stays capped. Existing reserve/allowedSpends/forbiddenSpends and cycle replay gates still work. Verify the exact strategy ledger entry and no art XP field. Use detached `ProjectCanonicalRoot` for pure policy, the existing normalizer Integration owner for correlation/persistence. |
| Self training | Both zero-tier offers have Russian names, next tier I, cost 500 Ink Feathers and the unchanged rank eligibility; buying once leaves 2000 of 2500 Ink Feathers, scalar tier 1 and the ordinary purchase receipt. Rank 0 cannot buy I; rank gates are E1/R1 for I, E3/R3 for II, E5/R5 for III, R7 for IV, R9 for V. Max V cannot be upgraded. |
| Mentor training | For each new art, authored/generated mentor offers require positive teacher tier, cannot exceed teacher/player caps, use 100/80/60% ordinary prices, and use the Russian fallback when `targetName` is absent. Buying writes the same scalar player tier and ordinary paid receipt; a zero-tier mentor cannot teach it. |
| Direct upgrades/parity | Both new IDs and their Russian selectors appear in existing console/browser selection and can be upgraded through `/spiritual_arts` at the same ordinary price/rank gate. No new operation choice for resilience. Browser player cards must not expose raw IDs as titles or call passive resilience an attack. |

The tier-I R1 gate corrects the earlier R2 plan typo: the existing `RadianceRanks`
`spark` row already unlocks tier I, including retained R1. This preserves the
user-approved ordinary-art progression; the runtime rank table does not change.

Example complete new Integration theory inside the current `TrainingServiceTests` owner:

```csharp
[Theory]
[InlineData("spiritual_resilience", "Духовная стойкость")]
[InlineData("spiritual_healing", "Духовное исцеление")]
public async Task StandardWoundArt_SelfTrainingUsesOrdinaryScalarPurchase(string id, string name)
{
    await SeedAfterlifeSoulStateAsync(inkFeathers: 2500);
    var service = CreateService();
    var view = await service.EnsureTrainingAsync(currentTurn: 21);
    var offerId = $"self_art_{id}_tier_1";
    var offer = Assert.Single(view.SelfTrainingOffers, item => item.OfferId == offerId);
    Assert.Equal(name, offer.TargetName);
    Assert.Equal(500, offer.Cost.InkFeathers);
    Assert.True(offer.Available);
    var result = await service.BuyTrainingAsync("self", offerId, currentTurn: 22);
    Assert.True(result.Success, result.Message);
    using var document = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
    var soul = document.RootElement;
    Assert.Equal(2000, soul.GetProperty("inkFeathers").GetProperty("current").GetInt32());
    Assert.Equal(1, soul.GetProperty("afterlifeCombatProfile").GetProperty("artTiers").GetProperty(id).GetInt32());
    var receipt = soul.GetProperty("afterlifeTrainingPurchaseReceipts")[0];
    Assert.Equal(offerId, receipt.GetProperty("offerId").GetString());
    Assert.Equal(500, receipt.GetProperty("inkFeathersSpent").GetInt32());
    Assert.Equal("self_fallback", receipt.GetProperty("sourceActorKind").GetString());
}
```

Run the owning Fast and Integration filters separately before implementing their behavior. A passing regression on existing generic policy is not new-feature RED; report it honestly alongside the new failing registration/boundary rows. Do not skip the real Integration RED run.

- [ ] **Step 3: Implement the registry, fresh bootstrap and strict current scalar profile contract.**

In `AfterlifeSpiritualConflictState`, keep existing public signatures and add:

```csharp
public const string SpiritualResilienceArtId = "spiritual_resilience";
public const string SpiritualHealingArtId = "spiritual_healing";

public static readonly IReadOnlyList<string> RequiredWoundArtIds =
    Array.AsReadOnly(new[] { SpiritualResilienceArtId, SpiritualHealingArtId });

public static JsonObject CreateDefaultArtTiers() => new()
{
    [SpiritualResilienceArtId] = 0,
    [SpiritualHealingArtId] = 0
};
```

Append these definitions to `SpiritualArts`:

```csharp
new(SpiritualResilienceArtId, "Духовная стойкость",
    "Пассивная стойкость души к духовным ранам; не требует отдельного действия и не расходует ОД.", 1),
new(SpiritualHealingArtId, "Духовное исцеление",
    "Искусство диагностики и лечения духовных ран; на нулевой ступени доступна только диагностика.", 1)
```

Use `["artTiers"] = CreateDefaultArtTiers()` in `CreateDefaultCombatProfile`.
Add both constant IDs to `AfterlifeEntityProfileState.StandardArtIds` only; preserve the operation registries.
Add `[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile()` to fresh New Game soul construction.
In `BuildInitialMentorStandardArts`, start from `CreateDefaultArtTiers()` and then set the existing guard, maneuver and domain tiers exactly as before.

Require missing new entries at the two validation boundaries, then reuse their existing scalar/range loops. The missing-entry issue shape is:

```csharp
foreach (var artId in AfterlifeSpiritualConflictState.RequiredWoundArtIds)
{
    if (standardArts.TryGetProperty(artId, out _))
        continue;
    issues.Add(new ValidationIssue(
        $"{context}.standardArts.{artId}",
        IssueSeverity.Error,
        "Профиль должен явно содержать тир духовной стойкости и духовного исцеления.",
        code: "afterlife_entity_profile_missing_standard_art_tier",
        section: "AfterlifeEntityProfiles",
        expected: "integer 0..5",
        actual: "missing"));
}
```

The player equivalent uses `artTiers`, path `.artTiers.{artId}`, code `afterlife_combat_profile_missing_art_tier`, section `AfterlifeSpiritualConflict`. Replace the existing silent missing-artTiers return with an issue on `.artTiers`, code `afterlife_combat_profile_missing_art_tiers`, expected `object with explicit spiritual_resilience and spiritual_healing tiers`; a non-object still uses existing `RequireObject`. Do not make a missing entire optional player combat profile a new unrelated soul-state validation rule here; the fresh supported bootstrap now creates it explicitly. Never backfill a present malformed/incomplete current object in a normalizer.

Do not rewrite the ordinary cost, purchase, mirror, strategy or override algorithms if registration is enough. If a new test exposes an actual additional authority gap, report its source and proposed bounded correction before broadening that code.

- [ ] **Step 4: Reconcile current data and Russian presentation.**

For positive complete profile seeds, retain every existing tier and insert only:

```json
"spiritual_resilience": 0,
"spiritual_healing": 0
```

Seed through the shared complete-profile fixture first. Audit hand-authored positive fixtures individually, especially materialization `sections.standardArts`: an object containing baseline zeros uses `{ "state": "populated" }`, while `canFight`/`canTeach` remain false if they have no positive usable content. Keep deliberate absent/malformed/empty negative tests and their rejection assertions meaningful. Do not modify a materialization section named `standardArts` as though it were a tier map.

The wound scenario fixture must carry requested resilience/healing scalar tiers in current profiles and player soul authority, not duplicate object-shaped future art authority. Retain genuinely future danger escalation, healing-service and faction-role proposals separately; the completed registration does not implement those features. Update fixture tests accordingly.

Add both explicit Russian fallback names in `TrainingService.FormatStandardSpiritualArtName` and browser `DescribeArt`. Console already uses the registry name fallback; give new arts their correct Russian use/rule/example/matchup descriptions instead of inherited attack text. Keep existing arts' copy unchanged.

Browser overview copy becomes `Боевые, защитные и целительные искусства души. Развитие каждого искусства открывает его собственные возможности.`; section title `Искусства`. Resilience subtitle is `Пассивное духовное искусство`, healing subtitle is `Целительное духовное искусство`, existing subtitles stay unchanged. Resilience cost text is `Пассивное действие, без затрат ОД`, its use text explains resistance to spiritual wounds, and its counter text is `Не является отдельным боевым приёмом`. Healing use text includes the tier-0 diagnosis limitation and treatment of wounds no heavier than the mastered tier; it must not claim to restore OD. The detail must not offer either ID as an executable conflict operation during this slice. Avoid broad UI refactoring or new attack/counter buttons.

- [ ] **Step 5: Synchronize GM guidance, worked example, manifest and guard.**

Extend the current afterlife profile/standard-art guidance with this contract, using existing sections rather than duplicating a giant profile guide:

```text
spiritual_resilience — Духовная стойкость; spiritual_healing — Духовное исцеление.
Оба стандартных искусства явно присутствуют в текущем профиле с integer-тиром
0..5, в том числе на нулевой ступени. Это не объекты {tier, experience}.
Игрок развивает их обычным обучением или /spiritual_arts; player_soul.standardArts
только отражает soul_state.afterlifeCombatProfile.artTiers. Сущности развивают
их через обычную progressionStrategy и standardArtTierDeltas, без отдельного
опыта искусства. Стойкость пассивна и не является operationType. Регистрация
исцеления не разрешает новый operationType: его отдельный боевой контракт
добавляется вместе с обработчиком лечения.
```

Update a complete existing `afterlifeEntityProfileUpdates` example to include both scalar entries and a valid strategy priority for one new art; show one ordinary explicit `standardArtTierDeltas` example for the other where the example already demonstrates a supported override. Keep player tier writes out of GM-authored override examples. Update that section's validation manifest assertions to prove both scalar fields and the ordinary progression path; add a focused documentation guard asserting both IDs, scalar-zero example, no independent XP, and passive-operation distinction in the guide/matrix/example. Update profile training examples and filesystem examples to the same required shape.

Check daemon/launcher guidance entrypoints: update only if the existing mandatory guide chain does not reach the amended guide. Record that check and the Mortal World no-update rationale. If FullValidation finds an affected archived save profile, perform the exact current-art cutover in those entries, preserving unrelated archive content and timestamps as the existing archive tests require.

- [ ] **Step 6: Verify the integrated change, self-review and commit.**

Run the exact new/changed owner filters with a fresh build and inspect summary/log/TRX membership. Use `-FocusedProject Integration` for real filesystem owners and keep separate Fast filters. Expected all new tests GREEN, including current negative validators rejecting the intended field and existing owner tests retaining their semantics.

At the integrated checkpoint run once each:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
```

If a lane fails, inspect exact failures and use only coherent owning reruns; record official lane failure and later exact reconciliation separately. A full successful lane may only be claimed from its own summary. Do not rerun broad controls ritualistically. Preserve all case coverage; a measured legitimate Focused overrun may use bounded `-TimeoutMinutes` up to 15 with written evidence.

Do not change frontend files, so `npm run verify`/rendered layout checks are not required for unchanged layout. Validate the C# generated player presentation through the real console/browser command owner tests. Run the scoped copy detector once if it supports these changed C# read-model targets; report unsupported coverage rather than inventing a visual QA result.

Inspect `git diff --check`, scope, JSON examples and both baseline/current profile validation paths. Commit only task-owned source/tests/docs/examples, never unrelated `.serena/` or SDD metadata. Record the full BASE..HEAD range and every verification artifact in the task report. Parent independent diff/artifact inspection and fresh task review precede any T082/T093/T100 checkbox closure. #1536 and the wound/healing workflows remain open.
