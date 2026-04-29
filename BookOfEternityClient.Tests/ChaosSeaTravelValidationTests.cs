using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ChaosSeaTravelValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ChaosSeaTravelValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-chaos-travel-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ChaosSeaTravelWrongActiveGuardian_Fails()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("input/turn_request.json", CreateTravelTurnRequest());
        await WriteNodeAsync("game_state/meta/guardians.json", CreateGuardiansRoot(
            activeGuardianId: "guardian_other_001",
            currentAbodeId: "abode_other_001",
            discoveredAbodes: new[] { "abode_current_001", "abode_target_001", "abode_other_001" },
            targetAbodeDiscovered: true));
        await WritePendingTurnSnapshotAsync(CreateSoulRoot(), CreatePreTurnGuardiansRoot());

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "chaos_sea_travel_active_guardian_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ChaosSeaTravelTargetMissingFromDiscoveredAbodes_Fails()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("input/turn_request.json", CreateTravelTurnRequest());
        await WriteNodeAsync("game_state/meta/guardians.json", CreateGuardiansRoot(
            activeGuardianId: "guardian_target_001",
            currentAbodeId: "abode_target_001",
            discoveredAbodes: new[] { "abode_current_001" },
            targetAbodeDiscovered: true));
        await WritePendingTurnSnapshotAsync(CreateSoulRoot(), CreatePreTurnGuardiansRoot());

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "chaos_sea_travel_target_not_discovered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ChaosSeaTravelOutsideExactChaosSea_Fails()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot(currentRealm: "Shining Abode"));
        await WriteNodeAsync("input/turn_request.json", CreateTravelTurnRequest());
        await WriteNodeAsync("game_state/meta/guardians.json", CreateGuardiansRoot(
            activeGuardianId: "guardian_target_001",
            currentAbodeId: "abode_target_001",
            discoveredAbodes: new[] { "abode_current_001", "abode_target_001" },
            targetAbodeDiscovered: true));
        await WritePendingTurnSnapshotAsync(CreateSoulRoot(currentRealm: "Shining Abode"), CreatePreTurnGuardiansRoot());

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "chaos_sea_travel_invalid_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ChaosSeaTravelTargetAddedOnlyAfterTurn_Fails()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("input/turn_request.json", CreateTravelTurnRequest());
        await WriteNodeAsync("game_state/meta/guardians.json", CreateGuardiansRoot(
            activeGuardianId: "guardian_target_001",
            currentAbodeId: "abode_target_001",
            discoveredAbodes: new[] { "abode_current_001", "abode_target_001" },
            targetAbodeDiscovered: true));
        await WritePendingTurnSnapshotAsync(CreateSoulRoot(), CreatePreTurnGuardiansRoot(targetPreviouslyDiscovered: false));

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "chaos_sea_travel_target_not_discovered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ChaosSeaTravelExactTargetWithCaseDifferences_Passes()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("input/turn_request.json", CreateTravelTurnRequest());
        await WriteNodeAsync("game_state/meta/guardians.json", CreateGuardiansRoot(
            activeGuardianId: "GUARDIAN_TARGET_001",
            currentAbodeId: "ABODE_TARGET_001",
            discoveredAbodes: new[] { "abode_current_001", "ABODE_TARGET_001" },
            targetAbodeDiscovered: true));
        await WritePendingTurnSnapshotAsync(CreateSoulRoot(), CreatePreTurnGuardiansRoot());

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("chaos_sea_travel_", StringComparison.OrdinalIgnoreCase) == true);
    }

    private async Task WritePendingTurnSnapshotAsync(JsonObject preTurnSoulRoot, JsonObject preTurnGuardiansRoot)
    {
        const string soulSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";
        const string guardiansSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json";
        await WriteNodeAsync(soulSnapshotPath, preTurnSoulRoot);
        await WriteNodeAsync(guardiansSnapshotPath, preTurnGuardiansRoot);

        var soulSnapshotJson = await _fs.ReadFileAsync(soulSnapshotPath) ?? string.Empty;
        var guardiansSnapshotJson = await _fs.ReadFileAsync(guardiansSnapshotPath) ?? string.Empty;
        var manifest = new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7,
            ["requestTimestamp"] = "2026-04-24T00:00:00Z",
            ["playerAction"] = TravelPlayerAction,
            ["files"] = new JsonObject
            {
                ["game_state/meta/soul_state.json"] = soulSnapshotPath,
                ["game_state/meta/guardians.json"] = guardiansSnapshotPath
            },
            ["snapshotFileHashes"] = new JsonObject
            {
                ["game_state/meta/soul_state.json"] = PendingTurnSnapshotAuthority.ComputeSha256(soulSnapshotJson),
                ["game_state/meta/guardians.json"] = PendingTurnSnapshotAuthority.ComputeSha256(guardiansSnapshotJson)
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

    private const string TravelPlayerAction =
        "[CHAOS_SEA_TRAVEL] Душа выбирает перемещение в обитель 'Сад Переходов' " +
        "(targetAbodeId=abode_target_001, targetGuardianId=guardian_target_001, " +
        "previousAbodeId=abode_current_001, previousActiveGuardianId=guardian_current_001).";

    private static JsonObject CreateTravelTurnRequest() => new()
    {
        ["sessionId"] = "session",
        ["requestId"] = "request",
        ["turnNumber"] = 7,
        ["playerAction"] = TravelPlayerAction
    };

    private static JsonObject CreateSoulRoot(string currentRealm = "Chaos Sea") => new()
    {
        ["currentRealm"] = currentRealm,
        ["currentIncarnation"] = 2,
        ["inkFeathers"] = new JsonObject
        {
            ["current"] = 10,
            ["total"] = 10
        }
    };

    private static JsonObject CreatePreTurnGuardiansRoot(bool targetPreviouslyDiscovered = true) => CreateGuardiansRoot(
        activeGuardianId: "guardian_current_001",
        currentAbodeId: "abode_current_001",
        discoveredAbodes: targetPreviouslyDiscovered
            ? new[] { "abode_current_001", "abode_target_001" }
            : new[] { "abode_current_001" },
        targetAbodeDiscovered: targetPreviouslyDiscovered);

    private static JsonObject CreateGuardiansRoot(
        string activeGuardianId,
        string currentAbodeId,
        IEnumerable<string> discoveredAbodes,
        bool targetAbodeDiscovered) => new()
    {
        ["guardians"] = new JsonArray
        {
            new JsonObject
            {
                ["guardianId"] = "guardian_current_001",
                ["canonicalName"] = "Азалия",
                ["domain"] = "Social",
                ["abode"] = new JsonObject
                {
                    ["abodeId"] = "abode_current_001",
                    ["name"] = "Шелковая Обитель",
                    ["isDiscovered"] = true
                }
            },
            new JsonObject
            {
                ["guardianId"] = "guardian_target_001",
                ["canonicalName"] = "Мириэль",
                ["domain"] = "Magic",
                ["abode"] = new JsonObject
                {
                    ["abodeId"] = "abode_target_001",
                    ["name"] = "Сад Переходов",
                    ["isDiscovered"] = targetAbodeDiscovered
                }
            },
            new JsonObject
            {
                ["guardianId"] = "guardian_other_001",
                ["canonicalName"] = "Орион",
                ["domain"] = "Knowledge",
                ["abode"] = new JsonObject
                {
                    ["abodeId"] = "abode_other_001",
                    ["name"] = "Башня Ориона",
                    ["isDiscovered"] = true
                }
            }
        },
        ["activeGuardian"] = new JsonObject
        {
            ["guardianId"] = activeGuardianId,
            ["canonicalName"] = activeGuardianId.Contains("target", StringComparison.OrdinalIgnoreCase) ? "Мириэль" : "Орион"
        },
        ["chaosSeaNavigation"] = new JsonObject
        {
            ["currentAbodeId"] = currentAbodeId,
            ["discoveredAbodes"] = new JsonArray(discoveredAbodes.Select(id => JsonValue.Create(id)).ToArray())
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
