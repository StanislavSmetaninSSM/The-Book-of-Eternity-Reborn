using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeRealmSegregationValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AfterlifeRealmSegregationValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-realm-segregation-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    public static IEnumerable<object[]> MortalForbiddenAfterlifeStateFiles()
    {
        yield return new object[]
        {
            GuardianAbodeResidentState.StatePath,
            """{ "schemaVersion": 1, "entries": [] }""",
            """{ "schemaVersion": 1, "entries": [{ "residentId": "resident_echo", "guardianId": "guardian_mirror", "abodeId": "abode_mirror" }] }"""
        };
        yield return new object[]
        {
            GuardianThoughtJournalState.StatePath,
            """{ "schemaVersion": 1, "entries": [] }""",
            """{ "schemaVersion": 1, "entries": [{ "entryId": "thought_1", "guardianId": "guardian_mirror", "summary": "Mortal turn tried to write guardian thought." }] }"""
        };
        yield return new object[]
        {
            GuardianSocialJournalState.StatePath,
            """{ "schemaVersion": 1, "entries": [] }""",
            """{ "schemaVersion": 1, "entries": [{ "entryId": "social_1", "guardianId": "guardian_mirror", "summary": "Mortal turn tried to write guardian social event." }] }"""
        };
        yield return new object[]
        {
            "game_state/meta/guardian_projects.json",
            """{ "schemaVersion": 1, "projects": [] }""",
            """{ "schemaVersion": 1, "projects": [{ "projectId": "project_mirror", "guardianId": "guardian_mirror", "status": "active" }] }"""
        };
        yield return new object[]
        {
            "game_state/meta/guardian_project_journal.json",
            """{ "schemaVersion": 1, "entries": [] }""",
            """{ "schemaVersion": 1, "entries": [{ "entryId": "project_journal_1", "projectId": "project_mirror", "summary": "Mortal turn tried to write project journal." }] }"""
        };
        yield return new object[]
        {
            "game_state/meta/abode_power_journal.json",
            """{ "schemaVersion": 1, "events": [] }""",
            """{ "schemaVersion": 1, "events": [{ "eventId": "power_1", "guardianId": "guardian_mirror", "reasonType": "test" }] }"""
        };
        yield return new object[]
        {
            ShiningAbodeState.StatePath,
            CreateShiningStateJson(availability: "active"),
            CreateShiningStateJson(availability: "sealed_until_next_ascension")
        };
    }

    [Fact]
    public async Task ValidateGameStateAsync_ChaosSeaTurnChangingShiningState_FailsRealmSegregation()
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        var preTurnShining = CreateShiningStateJson(availability: "active");
        var currentShining = CreateShiningStateJson(availability: "sealed_until_next_ascension");

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", preTurnSoul);
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, currentShining);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteSnapshotFileAsync(ShiningAbodeState.StatePath, preTurnShining);
        await WriteValidatedSnapshotManifestAsync(
            ("game_state/meta/soul_state.json", preTurnSoul),
            (ShiningAbodeState.StatePath, preTurnShining));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "realm_segregation_violation", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains(ShiningAbodeState.StatePath, StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ShiningAbodeTurnChangingShiningState_DoesNotFailRealmSegregation()
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Shining Abode"
        }
        """;
        var preTurnShining = CreateShiningStateJson(availability: "active");
        var currentShining = CreateShiningStateJson(availability: "sealed_until_next_ascension");

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", preTurnSoul);
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, currentShining);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteSnapshotFileAsync(ShiningAbodeState.StatePath, preTurnShining);
        await WriteValidatedSnapshotManifestAsync(
            ("game_state/meta/soul_state.json", preTurnSoul),
            (ShiningAbodeState.StatePath, preTurnShining));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "realm_segregation_violation", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains(ShiningAbodeState.StatePath, StringComparison.OrdinalIgnoreCase) == true);
    }

    [Theory]
    [MemberData(nameof(MortalForbiddenAfterlifeStateFiles))]
    public async Task ValidateGameStateAsync_MortalWorldTurnChangingAfterlifeAuthorityFile_FailsRealmSegregation(
        string relativePath,
        string preTurnJson,
        string currentJson)
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "MortalWorldProfile"
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", preTurnSoul);
        await _fs.WriteFileAtomicAsync(relativePath, currentJson);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteSnapshotFileAsync(relativePath, preTurnJson);
        await WriteValidatedSnapshotManifestAsync(
            ("game_state/meta/soul_state.json", preTurnSoul),
            (relativePath, preTurnJson));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "realm_segregation_violation", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains(relativePath, StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MortalWorldTurnCreatingOutputFile_DoesNotRequireValidatedBaseline()
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "MortalWorldProfile"
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", preTurnSoul);
        await _fs.WriteFileAtomicAsync("output/interface_updates.json", """
        {
          "statusChanges": [],
          "availableActions": []
        }
        """);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteValidatedSnapshotManifestAsync(("game_state/meta/soul_state.json", preTurnSoul));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "realm_segregation_missing_validated_tracked_baseline", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "output/interface_updates.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MortalWorldTurnWithoutGuardianPowerEvents_DoesNotRequireAbodePowerJournal()
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Mortal World"
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", preTurnSoul);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "activeGuardian": null,
          "chaosSeaNavigation": {
            "currentAbodeId": null
          }
        }
        """);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteSnapshotFileAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "activeGuardian": null,
          "chaosSeaNavigation": {
            "currentAbodeId": null
          }
        }
        """);
        await WriteValidatedSnapshotManifestAsync(
            ("game_state/meta/soul_state.json", preTurnSoul),
            ("game_state/meta/guardians.json", """
            {
              "guardians": [],
              "activeGuardian": null,
              "chaosSeaNavigation": {
                "currentAbodeId": null
              }
            }
            """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_resonance_invalid_current_journal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ItemJournalReferencingCanonicalItemWithNullContentsPath_IsAccepted()
    {
        const string preTurnSoul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Mortal World"
        }
        """;
        const string inventory = """
        {
          "items": [
            {
              "existedId": "item_ancient_sword",
              "itemId": "item_ancient_sword",
              "id": "item_ancient_sword",
              "name": "Древний меч",
              "itemName": "Древний меч",
              "description": "Старинный клинок с потемневшими рунами у гарды.",
              "image_prompt": "ancient rune etched sword",
              "quality": "Rare",
              "durability": "95%",
              "price": 120,
              "count": 1,
              "weight": 1.8,
              "volume": 1.0,
              "contentsPath": null,
              "isContainer": false,
              "isConsumption": false,
              "requiresTwoHands": false
            }
          ]
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", preTurnSoul);
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", inventory);
        await _fs.WriteFileAtomicAsync("game_state/npcs/item_journals.json", """
        {
          "entries": [
            {
              "itemId": "item_ancient_sword",
              "itemName": "Древний меч",
              "journalEntries": [
                {
                  "entryId": "sword_001",
                  "timestamp": "2023-05-20T16:45:00Z",
                  "event": "first_touch",
                  "description": "Меч отозвался на прикосновение."
                }
              ]
            }
          ]
        }
        """);
        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteSnapshotFileAsync("game_state/inventory/items.json", inventory);
        await WriteValidatedSnapshotManifestAsync(
            ("game_state/meta/soul_state.json", preTurnSoul),
            ("game_state/inventory/items.json", inventory));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "item_journal_unknown_item_reference", StringComparison.OrdinalIgnoreCase));
    }

    private Task WriteSnapshotFileAsync(string logicalPath, string json)
    {
        return _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{logicalPath}", json);
    }

    private static string CreateShiningStateJson(string availability)
    {
        var root = new JsonObject
        {
            ["availability"] = availability,
            ["radiance"] = new JsonObject
            {
                ["experience"] = 0,
                ["tier"] = 0
            },
            ["lightSparks"] = 0,
            ["halls"] = new JsonArray(),
            ["factions"] = new JsonArray(),
            ["shiningPoliticalActors"] = new JsonArray(),
            ["pendingNativeFactionDiscovery"] = null,
            ["factionFoundingReceipts"] = new JsonArray(),
            ["factionRealignmentReceipts"] = new JsonArray(),
            ["coreActionReceipts"] = new JsonArray(),
            ["gates"] = new JsonObject
            {
                ["draftVersion"] = 0,
                ["hasOpenDraft"] = false,
                ["isStale"] = false,
                ["allCandidateBlessingCards"] = new JsonArray(),
                ["availableBlessingCards"] = new JsonArray(),
                ["shownBlessingCardIds"] = new JsonArray(),
                ["selectedBlessingCardIds"] = new JsonArray(),
                ["nextCandidateCursor"] = 0,
                ["rerollsRemaining"] = 0
            },
            ["gachaSystem"] = new JsonObject
            {
                ["chargesPerReturn"] = 0,
                ["chargesUsedThisReturn"] = 0,
                ["currentReturnCycleId"] = "shining_return_1",
                ["gachaHistory"] = new JsonArray()
            }
        };

        return root.ToJsonString();
    }

    private async Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_afterlife_realm_segregation_tests";
        const string requestId = "request_afterlife_realm_segregation_tests";
        const int turnNumber = 11;
        const string playerAction = "Mortal turn must not mutate afterlife authority files.";

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
            files[path] = $"game_state/control/pending_turn_snapshot/{path}";
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-05-19T00:00:00Z",
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
