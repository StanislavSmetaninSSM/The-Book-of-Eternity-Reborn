using System.Text.Json.Nodes;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalBootstrapValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public MortalBootstrapValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-mortal-bootstrap-validation-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(_rootPath, "game_session"));

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public void MortalBootstrapStateBuilder_BuildsCanonicalBaselineWithoutKnownRepairLoopShapes()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Мирон, молодой архивариус-изгнанник.",
            worldDescription: "Город-государство у болот и старых руин.",
            startingCircumstances: "Мирон приходит в себя ночью в архивной башне после кражи запретной описи.",
            createdAtUtc: DateTimeOffset.Parse("2026-06-29T07:00:00Z"));

        Assert.Contains("game_state/world/current_location.json", files.Keys);
        Assert.Contains("game_state/world/world_map.json", files.Keys);
        Assert.Contains("game_state/factions/faction_core.json", files.Keys);
        Assert.Contains("game_state/quests/regular_quests.json", files.Keys);
        Assert.Contains("game_state/inventory/items.json", files.Keys);
        Assert.Contains("game_state/player/experience.json", files.Keys);
        Assert.Contains("lore/codex_entries.json", files.Keys);

        var currentLocation = files["game_state/world/current_location.json"];
        Assert.Equal("loc_life_001_start", currentLocation["locationId"]!.GetValue<string>());
        var lastEventsDescription = currentLocation["lastEventsDescription"]!.GetValue<string>();
        Assert.StartsWith("#[3]. Начало смертной жизни:", lastEventsDescription, StringComparison.Ordinal);
        Assert.DoesNotContain("#3 -", lastEventsDescription, StringComparison.Ordinal);

        var faction = files["game_state/factions/faction_core.json"]!["factions"]!.AsArray()[0]!.AsObject();
        Assert.Equal("faction_life_001_initial_context", faction["factionId"]!.GetValue<string>());
        Assert.False(faction.ContainsKey("initialId"));
        Assert.False(faction.ContainsKey("isNewFaction"));

        var quest = files["game_state/quests/regular_quests.json"]!["quests"]!.AsArray()[0]!.AsObject();
        var detailsLog = quest["detailsLog"]!.AsArray();
        Assert.Equal("#[3]. Первая цель новой жизни связала выбранные обстоятельства стартовой сцены.", detailsLog[0]!.GetValue<string>());
        AssertPlayerFacingBootstrapTextIsClean(quest, "fresh mortal bootstrap quest");

        var inventory = files["game_state/inventory/items.json"];
        var item = inventory["items"]!.AsArray().Single()!.AsObject();
        Assert.Equal("item_life_001_opening_anchor", item["itemId"]!.GetValue<string>());
        Assert.Equal("item_life_001_opening_anchor", item["existedId"]!.GetValue<string>());
        Assert.Equal("Common", item["quality"]!.GetValue<string>());
        Assert.Equal("100%", item["durability"]!.GetValue<string>());
        Assert.False(item["isContainer"]!.GetValue<bool>());
        Assert.False(item["isConsumption"]!.GetValue<bool>());
        Assert.False(item["requiresTwoHands"]!.GetValue<bool>());
        Assert.True(item.ContainsKey("equipmentSlot"));
        Assert.True(item.ContainsKey("accessoryForSlot"));
        Assert.True(item.ContainsKey("contentsPath"));
        Assert.Null(item["equipmentSlot"]);
        Assert.Null(item["accessoryForSlot"]);
        Assert.Null(item["contentsPath"]);
        Assert.NotEmpty(item["textContent"]!.AsArray());

        var experience = files["game_state/player/experience.json"];
        Assert.Equal(1, experience["playerLevel"]!.GetValue<int>());
        Assert.Equal(1, experience["level"]!.GetValue<int>());
        Assert.Equal(0, experience["currentExperience"]!.GetValue<int>());
        Assert.Equal(0, experience["experience"]!.GetValue<int>());
        Assert.Equal(0, experience["totalExperience"]!.GetValue<int>());
        Assert.Equal(100, experience["experienceForNextLevel"]!.GetValue<int>());
        Assert.Equal(0, experience["experienceGained"]!.GetValue<int>());

        var codexEntries = files["lore/codex_entries.json"]!["entries"]!.AsArray();
        var currentWorldEntry = codexEntries
            .OfType<JsonObject>()
            .Single(entry => string.Equals(
                entry["entryId"]?.GetValue<string>(),
                "codex_life_001_world",
                StringComparison.Ordinal));
        Assert.StartsWith(
            "current_world/",
            currentWorldEntry["sourceFile"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateGameStateAsync_CanonicalFactionWithOnlyTemporaryIdentity_ReportsBlockingIssue()
    {
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", new JsonObject
        {
            ["factions"] = new JsonArray
            {
                new JsonObject
                {
                    ["factionId"] = null,
                    ["initialId"] = "temp-faction-merchant-guild",
                    ["isNewFaction"] = true,
                    ["name"] = "Купеческая гильдия порта",
                    ["description"] = "Гильдия долговых книг и архивных печатей.",
                    ["image_prompt"] = "merchant guild archive seal, gothic port fantasy",
                    ["level"] = 1,
                    ["experience"] = 0,
                    ["experienceForNextLevel"] = 100,
                    ["developmentArchetype"] = "mercantile_archive_guild",
                    ["isPlayerFaction"] = false,
                    ["isPlayerMember"] = false,
                    ["reputation"] = 0,
                    ["powerProfile"] = new JsonObject
                    {
                        ["military"] = 1,
                        ["economic"] = 10,
                        ["social"] = 4,
                        ["covert"] = 2,
                        ["logistics"] = 5,
                        ["stability"] = 7,
                        ["arcane_tech"] = 1,
                        ["exploration"] = 1
                    },
                    ["resources"] = new JsonObject
                    {
                        ["wealth"] = 5,
                        ["metaResources"] = new JsonArray(),
                        ["strategicGoods"] = new JsonArray()
                    },
                    ["ranks"] = new JsonObject
                    {
                        ["branches"] = new JsonArray()
                    }
                }
            }
        }.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.Code, "canonical_faction_core_requires_permanent_faction_id", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertPlayerFacingBootstrapTextIsClean(JsonNode? node, string context)
    {
        var forbiddenTerms = new[] { "bootstrap", "baseline", "client", "клиент", "scaffold" };
        foreach (var text in EnumerateJsonStrings(node))
        {
            foreach (var term in forbiddenTerms)
            {
                Assert.DoesNotContain(term, text, StringComparison.OrdinalIgnoreCase);
            }
        }

        static IEnumerable<string> EnumerateJsonStrings(JsonNode? current)
        {
            switch (current)
            {
                case JsonValue value when value.TryGetValue<string>(out var text):
                    yield return text;
                    break;
                case JsonArray array:
                    foreach (var item in array)
                    foreach (var text in EnumerateJsonStrings(item))
                        yield return text;
                    break;
                case JsonObject obj:
                    foreach (var property in obj)
                    foreach (var text in EnumerateJsonStrings(property.Value))
                        yield return text;
                    break;
            }
        }
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentCodexStoredEntriesCanReferenceOtherCurrentEntries()
    {
        var preTurnCodex = """
        {
          "entries": [
            {
              "entryId": "codex_life_001_world",
              "title": "Текущий смертный мир",
              "category": "geography",
              "content": "Базовая запись текущего мира.",
              "summary": "Базовая запись текущего мира.",
              "sourceFile": "current_world/world_setting.json",
              "relatedEntries": []
            }
          ]
        }
        """;

        var currentCodex = """
        {
          "entries": [
            {
              "entryId": "codex_life_001_world",
              "title": "Текущий смертный мир",
              "category": "geography",
              "content": "Базовая запись текущего мира.",
              "summary": "Базовая запись текущего мира.",
              "sourceFile": "current_world/world_setting.json",
              "relatedEntries": []
            },
            {
              "entryId": "codex_life_001_ethernia",
              "title": "Этерния",
              "category": "geography",
              "content": "Город-государство дворянских домов и магических гильдий.",
              "summary": "Город-государство дворянских домов и магических гильдий.",
              "sourceFile": "current_world/geography.json",
              "relatedEntries": [ "codex_life_001_world" ]
            },
            {
              "entryId": "codex_life_001_house_valmont",
              "title": "Дом Вальмонт",
              "category": "factions",
              "content": "Дворянский дом, связанный с архивами и руническими тайнами.",
              "summary": "Дворянский дом, связанный с архивами и руническими тайнами.",
              "sourceFile": "current_world/cultures.json",
              "relatedEntries": [ "codex_life_001_ethernia" ]
            }
          ]
        }
        """;

        await WriteValidatedSnapshotManifestAsync(("lore/codex_entries.json", preTurnCodex));
        await _fs.WriteFileAtomicAsync("lore/codex_entries.json", currentCodex);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "codex_related_entry_unknown_target", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "codex_life_001_ethernia", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ClientOwnedMortalBootstrapBaselineLoreDoesNotRequireGmRewrite()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Лира Вальмонт, молодая дворянка-маг.",
            worldDescription: "Поместье Вальмонт в столице Этернии.",
            startingCircumstances: "Лира просыпается в своих покоях после тревожных снов.",
            createdAtUtc: DateTimeOffset.Parse("2026-06-29T07:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        var bootstrapLoreFiles = new[]
        {
            "lore/current_world/world_setting.json",
            "lore/current_world/geography.json",
            "lore/current_world/history.json",
            "lore/current_world/cultures.json",
            "lore/current_world/threats.json",
            "lore/codex_entries.json"
        };

        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "GM-инициированного воплощения",
            includeSnapshotFilesAsRollbackBaseline: false,
            bootstrapLoreFiles.Select(path => (Path: path, Json: files[path].ToJsonString())).ToArray());
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "sessionId": "session_mortal_bootstrap_validation_tests",
          "requestId": "request_mortal_bootstrap_validation_tests",
          "turnNumber": 3
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_reused_previous_world_lore", StringComparison.OrdinalIgnoreCase));
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
            // best-effort cleanup
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles) =>
        WriteValidatedSnapshotManifestAsync(
            sourceLabel: "mortal bootstrap validation tests",
            includeSnapshotFilesAsRollbackBaseline: true,
            snapshotFiles);

    private async Task WriteValidatedSnapshotManifestAsync(
        string sourceLabel,
        bool includeSnapshotFilesAsRollbackBaseline,
        params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_mortal_bootstrap_validation_tests";
        const string requestId = "request_mortal_bootstrap_validation_tests";
        const int turnNumber = 3;
        const string playerAction = "Mortal bootstrap validation test.";

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": {{JsonSerializer.Serialize(playerAction)}}
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
            if (includeSnapshotFilesAsRollbackBaseline)
                rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-06-29T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = sourceLabel,
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }
}
