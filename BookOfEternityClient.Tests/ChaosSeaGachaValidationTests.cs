using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ChaosSeaGachaValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ChaosSeaGachaValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-chaos-gacha-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaWithoutNewRelic_Fails()
    {
        var preTurnSoul = CreateSoulRoot();
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7,
            ["playerAction"] = "[CHAOS_SEA_DIRECT_GACHA] Игрок напрямую тянет Реликвию Души из Моря Хаоса и тратит 5 Чернильных Перьев."
        });
        await WritePendingTurnSnapshotAsync(preTurnSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_missing_new_relic_materialization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaOutsideExactChaosSea_Fails()
    {
        var preTurnSoul = CreateSoulRoot(currentRealm: "Shining Abode", inkFeathers: 10);
        var currentSoul = CreateSoulRoot(currentRealm: "Shining Abode", inkFeathers: 5);
        AddStoredSoulRelic(currentSoul, "relic_new");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7,
            ["playerAction"] = "[CHAOS_SEA_DIRECT_GACHA] Игрок напрямую тянет Реликвию Души из Моря Хаоса и тратит 5 Чернильных Перьев."
        });
        await WritePendingTurnSnapshotAsync(preTurnSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_invalid_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaWithoutFeatherDeduction_Fails()
    {
        var preTurnSoul = CreateSoulRoot(inkFeathers: 10);
        var currentSoul = CreateSoulRoot(inkFeathers: 10);
        AddStoredSoulRelic(currentSoul, "relic_new");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7,
            ["playerAction"] = "[CHAOS_SEA_DIRECT_GACHA] Игрок напрямую тянет Реликвию Души из Моря Хаоса и тратит 5 Чернильных Перьев."
        });
        await WritePendingTurnSnapshotAsync(preTurnSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_feather_balance_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WritePendingTurnSnapshotAsync(JsonObject preTurnSoulRoot)
    {
        const string soulSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";
        await WriteNodeAsync(soulSnapshotPath, preTurnSoulRoot);

        var soulSnapshotJson = await _fs.ReadFileAsync(soulSnapshotPath) ?? string.Empty;
        var manifest = new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7,
            ["requestTimestamp"] = "2026-04-24T00:00:00Z",
            ["playerAction"] = "[CHAOS_SEA_DIRECT_GACHA] Игрок напрямую тянет Реликвию Души из Моря Хаоса и тратит 5 Чернильных Перьев.",
            ["files"] = new JsonObject
            {
                ["game_state/meta/soul_state.json"] = soulSnapshotPath
            },
            ["snapshotFileHashes"] = new JsonObject
            {
                ["game_state/meta/soul_state.json"] = PendingTurnSnapshotAuthority.ComputeSha256(soulSnapshotJson)
            },
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = new JsonArray(),
            ["sourceLabel"] = "обычный ход игрока"
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await WriteNodeAsync("game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task WriteNodeAsync(string path, JsonNode node)
    {
        await _fs.WriteFileAtomicAsync(path, node.ToJsonString());
    }

    private static JsonObject CreateSoulRoot(string currentRealm = "Chaos Sea", int inkFeathers = 10) => new()
    {
        ["currentRealm"] = currentRealm,
        ["currentIncarnation"] = 2,
        ["inkFeathers"] = new JsonObject
        {
            ["current"] = inkFeathers,
            ["total"] = 10
        },
        ["soulRelics"] = new JsonObject
        {
            ["equipped"] = new JsonArray(),
            ["stored"] = new JsonArray
            {
                new JsonObject
                {
                    ["relicId"] = "relic_existing",
                    ["name"] = "Старая реликвия",
                    ["rarity"] = "Common",
                    ["quality"] = "Common"
                }
            }
        }
    };

    private static void AddStoredSoulRelic(JsonObject soulRoot, string relicId)
    {
        var stored = soulRoot["soulRelics"]?["stored"]?.AsArray()
            ?? throw new InvalidOperationException("Expected soulRelics.stored test array.");
        stored.Add(new JsonObject
        {
            ["relicId"] = relicId,
            ["name"] = "Новая реликвия",
            ["rarity"] = "Common",
            ["quality"] = "Common"
        });
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
}
