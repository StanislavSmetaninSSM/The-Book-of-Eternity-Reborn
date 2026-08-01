using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class AfterlifeGlobalFlagValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AfterlifeGlobalFlagValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-global-flags-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidAfterlifeGlobalFlag_PassesValidation()
    {
        await WriteFlagStateAsync(BuildValidFlagStateJson());

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.AfterlifeGlobalFlag);

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_global_flag_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ObsoleteFlagWithoutReason_ReportsIssue()
    {
        await WriteFlagStateAsync(BuildValidFlagStateJson()
            .Replace("\"state\": \"active\"", "\"state\": \"obsolete\"", StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.AfterlifeGlobalFlag);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_global_flag_obsolete_reason_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_UpdateWithoutGmThoughtsSummary_ReportsIssue()
    {
        await WriteFlagStateAsync("""
        {
          "schemaVersion": 1,
          "afterlifeGlobalFlagUpdates": [
            {
              "flagId": "saref_name_revealed",
              "category": "saref",
              "state": "active",
              "visibility": "visible",
              "createdAtTurn": 21,
              "updatedAtTurn": 21,
              "reason": "Игрок узнал имя Сарефа.",
              "evidence": "azalia_saref_q4"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.AfterlifeGlobalFlag);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_global_flag_update_missing_gm_thoughts", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DuplicateFlagIds_ReportIssue()
    {
        await WriteFlagStateAsync("""
        {
          "schemaVersion": 1,
          "flags": [
            {
              "flagId": "saref_name_revealed",
              "category": "saref",
              "state": "active",
              "visibility": "visible",
              "createdAtTurn": 21,
              "updatedAtTurn": 21,
              "reason": "Игрок узнал имя Сарефа.",
              "evidence": "azalia_saref_q4",
              "linkedActors": [],
              "linkedChronicles": []
            },
            {
              "flagId": "saref_name_revealed",
              "category": "saref",
              "state": "active",
              "visibility": "visible",
              "createdAtTurn": 22,
              "updatedAtTurn": 22,
              "reason": "Дубликат.",
              "evidence": "duplicate",
              "linkedActors": [],
              "linkedChronicles": []
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.AfterlifeGlobalFlag);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_global_flag_duplicate_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnFlagRemovedByReplacement_ReportsIssue()
    {
        var preTurn = BuildValidFlagStateJson();
        await WriteSnapshotFileAsync(AfterlifeGlobalFlagState.StatePath, preTurn);
        await WriteValidatedSnapshotManifestAsync((AfterlifeGlobalFlagState.StatePath, preTurn));
        await WriteFlagStateAsync("""
        {
          "schemaVersion": 1,
          "flags": []
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.AfterlifeGlobalFlag);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_global_flag_removed_without_obsolete_marker", StringComparison.OrdinalIgnoreCase));
    }

    private Task WriteFlagStateAsync(string json) =>
        _fs.WriteFileAtomicAsync(AfterlifeGlobalFlagState.StatePath, json);

    private Task WriteSnapshotFileAsync(string logicalPath, string json) =>
        _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{logicalPath}", json);

    private async Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_afterlife_global_flags_tests";
        const string requestId = "request_afterlife_global_flags_tests";
        const int turnNumber = 42;
        const string playerAction = "Afterlife global flags validation test.";

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
            ["requestTimestamp"] = "2026-05-22T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "afterlife global flags test",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static string BuildValidFlagStateJson() =>
        """
        {
          "schemaVersion": 1,
          "flags": [
            {
              "flagId": "saref_name_revealed",
              "category": "saref",
              "state": "active",
              "visibility": "visible",
              "createdAtTurn": 21,
              "updatedAtTurn": 21,
              "reason": "Игрок узнал имя Сарефа из четвертого квеста Хранителя.",
              "evidence": "azalia_saref_q4",
              "linkedActors": [ "guardian:azalia" ],
              "linkedChronicles": [ "saref_main_thread" ]
            }
          ]
        }
        """;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Ignore temp cleanup failures.
        }
    }
}
