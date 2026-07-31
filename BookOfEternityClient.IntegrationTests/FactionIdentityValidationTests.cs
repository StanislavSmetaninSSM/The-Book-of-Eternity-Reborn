using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FactionIdentityValidationTests : IDisposable
{
    private const string FactionInitialId = "temp-faction-merchant-guild-eternia";
    private const string FactionName = "Купеческая гильдия";

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public FactionIdentityValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-faction-identity-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(_rootPath, "game_session"));
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_SameTurnTemporaryFactionInSnapshot_DoesNotRequirePermanentFactionId()
    {
        var factionCoreJson = CreateTemporaryFactionCoreJson();
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", factionCoreJson);
        await WriteValidatedSnapshotManifestAsync(("game_state/factions/faction_core.json", factionCoreJson));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "faction_full_object_existing_requires_faction_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("game_state/factions/faction_core.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FactionSidecarWithoutPermanentId_ReportsRepairableFactionIdentityCode()
    {
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", CreateTemporaryFactionCoreJson());
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_custom.json", """
        {
          "entries": [
            {
              "factionId": null,
              "initialId": "temp-faction-merchant-guild-eternia",
              "isNewFaction": true,
              "name": "Купеческая гильдия",
              "summary": "Гильдия ждёт подтверждения ночных событий."
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "canonical_faction_sidecar_requires_permanent_faction_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("game_state/factions/faction_custom.json.entries[0]", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CanonicalPermanentFactionCore_DoesNotReportUnknownFullObjectFactionId()
    {
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", CreatePermanentFactionCoreJson());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "faction_full_object_unknown_faction_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("game_state/factions/faction_core.json.factions[0].factionId", StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateTemporaryFactionCoreJson()
    {
        var root = new JsonObject
        {
            ["factions"] = new JsonArray(CreateTemporaryFactionObject())
        };

        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static string CreatePermanentFactionCoreJson()
    {
        var faction = CreateTemporaryFactionObject();
        faction["factionId"] = "faction_life_001_initial_context";
        faction.Remove("initialId");
        faction.Remove("isNewFaction");
        faction["resources"] = new JsonObject
        {
            ["wealth"] = 1,
            ["manpower"] = 1,
            ["information"] = 2,
            ["magic"] = 0,
            ["metaResources"] = new JsonArray(),
            ["strategicGoods"] = new JsonArray()
        };
        faction["ranks"] = new JsonObject
        {
            ["entries"] = new JsonArray(),
            ["branches"] = new JsonArray(),
            ["hierarchySummary"] = "Во главе стоит мелкий посредник; ниже - писцы, слуги и зависимые семьи."
        };
        faction["rankBranches"] = new JsonArray();
        faction["controlledTerritories"] = new JsonArray("loc_life_001_start");
        faction["projects"] = new JsonArray();
        faction["chronicle"] = new JsonArray();
        faction["customStates"] = new JsonArray();

        var root = new JsonObject
        {
            ["factions"] = new JsonArray(faction)
        };

        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static JsonObject CreateTemporaryFactionObject()
    {
        return new JsonObject
        {
            ["factionId"] = null,
            ["initialId"] = FactionInitialId,
            ["isNewFaction"] = true,
            ["name"] = FactionName,
            ["description"] = "Торговая сила столицы Этернии, втянутая в ночную историю поместья.",
            ["factionColor"] = "#b8893d",
            ["image_prompt"] = "merchant guild banner with scales and sealed letters, dark fantasy",
            ["powerProfile"] = new JsonObject
            {
                ["military"] = 1,
                ["economic"] = 4,
                ["social"] = 3,
                ["covert"] = 2,
                ["logistics"] = 4,
                ["stability"] = 3,
                ["arcane_tech"] = 1,
                ["exploration"] = 1
            },
            ["resources"] = new JsonObject(),
            ["ranks"] = new JsonArray(),
            ["relations"] = new JsonArray(),
            ["isPlayerFaction"] = false,
            ["isPlayerMember"] = false,
            ["level"] = 1,
            ["experience"] = 0,
            ["experienceForNextLevel"] = 100,
            ["developmentArchetype"] = "merchant_guild",
            ["reputation"] = 0,
            ["reputationDescription"] = "Осторожный интерес"
        };
    }

    private async Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_faction_identity_validation_tests";
        const string requestId = "request_faction_identity_validation_tests";
        const int turnNumber = 4;
        const string playerAction = "Проверка ремонта временных фракций.";

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": "{{playerAction}}"
        }
        """);

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();

        foreach (var (path, json) in snapshotFiles)
        {
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await _fs.WriteFileAtomicAsync(snapshotPath, json);
            files[path] = snapshotPath;
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-06-26T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "mortal turn",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destinationDir, Path.GetRelativePath(sourceDir, dir)));

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destinationDir, Path.GetRelativePath(sourceDir, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
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
