using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class InkFeatherRealmValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public InkFeatherRealmValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-ink-feather-realm-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ShiningMemoryGatesUsesAfterlifeWhitelist()
    {
        const string playerAction = "[INK_FEATHER_ACTION: MEMORY_GATES] 10 Чернильных Перьев";
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot("Shining Abode"));
        await WriteTurnRequestAsync(playerAction);
        await WritePendingTurnSnapshotAsync(CreateSoulRoot("Shining Abode"), playerAction);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_wrong_realm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_result_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ShiningRejectsMortalWorldWhitelistActions()
    {
        const string playerAction = "[INK_FEATHER_ACTION: LEARN_SKILL] 10 Чернильных Перьев";
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot("Shining Abode"));
        await WriteTurnRequestAsync(playerAction);
        await WritePendingTurnSnapshotAsync(CreateSoulRoot("Shining Abode"), playerAction);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_wrong_realm", StringComparison.OrdinalIgnoreCase) &&
            (issue.RepairHint ?? string.Empty).Contains("Shining Abode", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteTurnRequestAsync(string playerAction)
    {
        await WriteNodeAsync("input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7,
            ["playerAction"] = playerAction
        });
    }

    private async Task WritePendingTurnSnapshotAsync(JsonObject preTurnSoulRoot, string playerAction)
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
            ["playerAction"] = playerAction,
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

    private static JsonObject CreateSoulRoot(string currentRealm) => new()
    {
        ["currentRealm"] = currentRealm,
        ["currentIncarnation"] = 2,
        ["inkFeathers"] = new JsonObject
        {
            ["current"] = 10,
            ["total"] = 10
        },
        ["soulRelics"] = new JsonObject
        {
            ["equipped"] = new JsonArray(),
            ["stored"] = new JsonArray()
        }
    };

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
