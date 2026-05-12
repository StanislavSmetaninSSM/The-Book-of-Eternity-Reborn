using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeSpiritualConflictBalanceTests : IDisposable
{
    private static readonly int[] AuthoritativeConflictDice =
    {
        5, 18, 14, 9, 11, 7, 20, 1, 13, 6, 16, 8, 12, 4, 10, 15, 3, 17, 2, 19
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AfterlifeSpiritualConflictBalanceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-spiritual-conflict-balance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    public static IEnumerable<object[]> BalanceMatrix()
    {
        yield return [new BalanceCase(
            "equal_sides_average_roll",
            PlayerDieIndex: 4,
            OppositionDieIndex: 3,
            PlayerModifier: 0,
            OppositionModifier: 0,
            ExpectedMargin: 2,
            ExpectedBand: "mixed_or_no_effect",
            ExpectedOutcome: "no_effect",
            BeforePosition: "contested",
            AfterPosition: "contested",
            Reading: "Equal sides do not auto-win from a small roll edge.")];

        yield return [new BalanceCase(
            "weak_player_vs_average_guardian",
            PlayerDieIndex: 2,
            OppositionDieIndex: 3,
            PlayerModifier: 0,
            OppositionModifier: 4,
            ExpectedMargin: 1,
            ExpectedBand: "mixed_or_no_effect",
            ExpectedOutcome: "no_effect",
            BeforePosition: "contested",
            AfterPosition: "contested",
            Reading: "A good player roll can avoid collapse but not beat an average Guardian outright.")];

        yield return [new BalanceCase(
            "upgraded_chaos_player_vs_average_guardian",
            PlayerDieIndex: 2,
            OppositionDieIndex: 3,
            PlayerModifier: 3,
            OppositionModifier: 2,
            ExpectedMargin: 6,
            ExpectedBand: "player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_advantaged",
            Reading: "A one-to-two art-tier investment matters without becoming decisive.")];

        yield return [new BalanceCase(
            "same_upgrade_bad_roll",
            PlayerDieIndex: 0,
            OppositionDieIndex: 1,
            PlayerModifier: 3,
            OppositionModifier: 2,
            ExpectedMargin: -12,
            ExpectedBand: "decisive_opposition_success",
            ExpectedOutcome: "setback",
            BeforePosition: "contested",
            AfterPosition: "opposition_dominant",
            Reading: "Dice can still create a dramatic loss.")];

        yield return [new BalanceCase(
            "returned_shining_soul_retained_radiance",
            PlayerDieIndex: 12,
            OppositionDieIndex: 11,
            PlayerModifier: 4,
            OppositionModifier: 3,
            ExpectedMargin: 5,
            ExpectedBand: "player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_advantaged",
            Reading: "Retained Radiance remains relevant after return to Chaos Sea.")];

        yield return [new BalanceCase(
            "weak_player_aided_by_strong_champion",
            PlayerDieIndex: 8,
            OppositionDieIndex: 9,
            PlayerModifier: 6,
            OppositionModifier: 4,
            ExpectedMargin: 9,
            ExpectedBand: "decisive_player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_dominant",
            Reading: "Champion support solves the weak-player-plus-strong-ally case without mass combat.")];

        yield return [new BalanceCase(
            "strong_guardian_vs_novice",
            PlayerDieIndex: 3,
            OppositionDieIndex: 4,
            PlayerModifier: 0,
            OppositionModifier: 6,
            ExpectedMargin: -8,
            ExpectedBand: "decisive_opposition_success",
            ExpectedOutcome: "setback",
            BeforePosition: "contested",
            AfterPosition: "opposition_dominant",
            Reading: "A novice should not trade evenly with a high-authority Guardian on average rolls.")];

        yield return [new BalanceCase(
            "four_tier_advantage_average_roll",
            PlayerDieIndex: 2,
            OppositionDieIndex: 3,
            PlayerModifier: 6,
            OppositionModifier: 1,
            ExpectedMargin: 10,
            ExpectedBand: "decisive_player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_dominant",
            Reading: "Large progression advantage is decisive on normal dice.")];

        yield return [new BalanceCase(
            "four_tier_advantage_extreme_bad_roll",
            PlayerDieIndex: 0,
            OppositionDieIndex: 1,
            PlayerModifier: 6,
            OppositionModifier: 1,
            ExpectedMargin: -8,
            ExpectedBand: "decisive_opposition_success",
            ExpectedOutcome: "setback",
            BeforePosition: "contested",
            AfterPosition: "opposition_dominant",
            Reading: "Even a large advantage does not erase rare dramatic reversals.")];
    }

    [Theory]
    [MemberData(nameof(BalanceMatrix))]
    public async Task ValidateGameStateAsync_AfterlifeConflictBalanceMatrix_AcceptsExpectedDiceBands(BalanceCase scenario)
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildDiceAudit(scenario);
        await WriteConflictStateWithExchangeAsync(scenario, diceAudit);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Equal(scenario.ExpectedMargin, diceAudit["margin"]?.GetValue<int>());
        Assert.Equal(scenario.ExpectedBand, diceAudit["outcomeBand"]?.GetValue<string>());
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_conflict_dice_", StringComparison.OrdinalIgnoreCase) == true ||
            string.Equals(issue.Code, "afterlife_conflict_exchange_missing_dice_audit", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject BuildDiceAudit(BalanceCase scenario)
    {
        var playerDie = AuthoritativeConflictDice[scenario.PlayerDieIndex];
        var oppositionDie = AuthoritativeConflictDice[scenario.OppositionDieIndex];
        var playerTotal = playerDie + scenario.PlayerModifier;
        var oppositionTotal = oppositionDie + scenario.OppositionModifier;
        var margin = playerTotal - oppositionTotal;
        Assert.Equal(scenario.ExpectedMargin, margin);
        Assert.Equal(scenario.ExpectedBand, ExpectedBand(margin));

        return new JsonObject
        {
            ["formulaVersion"] = "afterlife_spiritual_conflict_v1",
            ["diceSource"] = "input/turn_request.json.preGeneratedDices1d20",
            ["diceUsed"] = new JsonArray(
                new JsonObject
                {
                    ["side"] = "player",
                    ["sourceIndex"] = scenario.PlayerDieIndex,
                    ["sides"] = 20,
                    ["value"] = playerDie
                },
                new JsonObject
                {
                    ["side"] = "opposition",
                    ["sourceIndex"] = scenario.OppositionDieIndex,
                    ["sides"] = 20,
                    ["value"] = oppositionDie
                }),
            ["playerTotal"] = playerTotal,
            ["oppositionTotal"] = oppositionTotal,
            ["margin"] = margin,
            ["outcomeBand"] = scenario.ExpectedBand,
            ["modifierBreakdown"] = new JsonObject
            {
                ["player"] = new JsonArray(
                    new JsonObject
                    {
                        ["source"] = "balance audit player progression/support modifier",
                        ["value"] = scenario.PlayerModifier
                    }),
                ["opposition"] = new JsonArray(
                    new JsonObject
                    {
                        ["source"] = "balance audit opposition progression/support modifier",
                        ["value"] = scenario.OppositionModifier
                    })
            }
        };
    }

    private static string ExpectedBand(int margin) =>
        margin >= 8 ? "decisive_player_success" :
        margin >= 3 ? "player_success" :
        margin >= -2 ? "mixed_or_no_effect" :
        margin >= -7 ? "opposition_success" :
        "decisive_opposition_success";

    private Task WriteSoulStateAsync()
    {
        return _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);
    }

    private Task WriteConflictStateWithExchangeAsync(BalanceCase scenario, JsonObject diceAudit)
    {
        return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_balance_001",
            "realm": "Chaos Sea",
            "sideModel": {{JsonSerializer.Serialize(scenario.Name == "weak_player_aided_by_strong_champion" ? "champion_duel" : "direct_duel")}},
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": {
                  "pressure": 2,
                  "guard": 1
                },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": {{JsonSerializer.Serialize(scenario.AfterPosition)}},
            "resolutionState": "active",
            "exchangeLog": [
              {
                "exchangeId": {{JsonSerializer.Serialize("exchange_balance_" + scenario.Name)}},
                "operationType": "pressure",
                "outcome": {{JsonSerializer.Serialize(scenario.ExpectedOutcome)}},
                "before": {
                  "playerSideStrain": "clear",
                  "oppositionSideStrain": "clear",
                  "conflictPosition": {{JsonSerializer.Serialize(scenario.BeforePosition)}}
                },
                "after": {
                  "playerSideStrain": "clear",
                  "oppositionSideStrain": "clear",
                  "conflictPosition": {{JsonSerializer.Serialize(scenario.AfterPosition)}}
                },
                "diceAudit": {{diceAudit.ToJsonString()}}
              }
            ]
          },
          "recentConflicts": []
        }
        """);
    }

    private async Task WritePreTurnActiveConflictSnapshotAsync()
    {
        const string soul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string conflict = """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_balance_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": {
                  "pressure": 2,
                  "guard": 1
                },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "active",
            "exchangeLog": []
          },
          "recentConflicts": []
        }
        """;

        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, conflict);
        await WriteValidatedSnapshotManifestAsync(
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, conflict));
    }

    private Task WriteSnapshotFileAsync(string logicalPath, string json)
    {
        return _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{logicalPath}", json);
    }

    private async Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_spiritual_conflict_balance_tests";
        const string requestId = "request_spiritual_conflict_balance_tests";
        const int turnNumber = 7;
        const string playerAction = "[AFTERLIFE_SPIRITUAL_ACTION: afterlife_conflict_balance_001] Проверяю баланс духовного поединка.";

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": {{JsonSerializer.Serialize(playerAction)}},
          "preGeneratedDices1d20": {{JsonSerializer.Serialize(AuthoritativeConflictDice)}}
        }
        """);

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();

        foreach (var (path, json) in snapshotFiles)
        {
            files[path] = $"game_state/control/pending_turn_snapshot/{path}";
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-05-06T00:00:00Z",
            ["playerAction"] = playerAction,
            ["preGeneratedDices1d20"] = JsonSerializer.SerializeToNode(AuthoritativeConflictDice),
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "afterlife-spiritual-conflict-balance-tests",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignored
        }
    }

    public sealed record BalanceCase(
        string Name,
        int PlayerDieIndex,
        int OppositionDieIndex,
        int PlayerModifier,
        int OppositionModifier,
        int ExpectedMargin,
        string ExpectedBand,
        string ExpectedOutcome,
        string BeforePosition,
        string AfterPosition,
        string Reading);
}
