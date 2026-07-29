using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeRealmAutoRollbackTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public AfterlifeRealmAutoRollbackTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-realm-auto-rollback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task LoadValidatedManifestAsync_HardLinkedSnapshotFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string rollbackPath =
            "game_state/meta/soul_state.json.explorer.rollback.realm-authority";
        await WriteAfterlifeSnapshotAsync(
            ("game_state/meta/soul_state.json",
                """{ "currentRealm": "Chaos Sea", "soulName": "Пепельная Искра" }"""));
        await _fs.WriteFileAtomicAsync(
            rollbackPath,
            """{ "currentRealm": "Mortal World", "soulName": "Before" }""");
        var manifest = JsonNode.Parse(
            await _fs.ReadFileAsync(
                "game_state/control/pending_turn_snapshot.json")
            ?? throw new InvalidDataException(
                "Expected pending-turn manifest."))!.AsObject();
        manifest["rollbackBackups"] = new JsonObject
        {
            ["game_state/meta/soul_state.json"] = rollbackPath
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
        WindowsHardLinkTestHelper.Create(
            Path.Combine(_rootPath, "linked-realm-rollback.json"),
            _fs.ResolvePath(rollbackPath));

        var service = new RealmSegregationAutoRollbackService(
            _fs,
            NullLogger<RealmSegregationAutoRollbackService>.Instance);
        var method = typeof(RealmSegregationAutoRollbackService).GetMethod(
            "LoadValidatedManifestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Realm pending-turn manifest reader was not found.");

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
            {
                var task = Assert.IsAssignableFrom<Task>(
                    method.Invoke(service, null));
                await task;
            });
    }

    [Fact]
    public async Task TryRollbackForbiddenRealmMutationsAsync_ManifestLinkAddedAfterInitialValidationFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await WriteAfterlifeSnapshotAsync(
            ("game_state/meta/soul_state.json",
                """{ "currentRealm": "Chaos Sea", "soulName": "Пепельная Искра" }"""));
        var manifestPath = _fs.ResolvePath(
            "game_state/control/pending_turn_snapshot.json");
        var aliasPath = Path.Combine(
            _rootPath,
            "linked-realm-manifest-after-open.json");
        var linked = false;
        var hooks = FileSystemManagerHookTestHelper.WithPathHook(
            "AfterCanonicalReadInitialValidationAsync",
            path =>
            {
                if (!linked &&
                    path.Equals(
                        "game_state/control/pending_turn_snapshot.json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    WindowsHardLinkTestHelper.Create(aliasPath, manifestPath);
                    linked = true;
                }

                return Task.CompletedTask;
            });
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        var service = new RealmSegregationAutoRollbackService(
            raceFs,
            NullLogger<RealmSegregationAutoRollbackService>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.TryRollbackForbiddenRealmMutationsAsync(
                "Chaos Sea",
                ["game_state/factions/faction_core.json"],
                "completion validation test"));
        Assert.True(linked);
    }

    [Fact]
    public async Task TryRollbackForbiddenRealmMutationsAsync_ChaosSeaRestoresChangedMortalFileFromSnapshot()
    {
        const string forbiddenPath = "game_state/factions/faction_core.json";
        const string beforeJson = """{ "factions": [{ "factionId": "faction_valmont", "name": "Дом Вальмонтов" }] }""";
        const string afterJson = """{ "factions": [{ "factionId": "temp-faction-merchant-guild-eternia", "name": "Wrong realm" }] }""";

        await WriteAfterlifeSnapshotAsync(
            ("game_state/meta/soul_state.json", """{ "currentRealm": "Chaos Sea", "soulName": "Пепельная Искра" }"""),
            (forbiddenPath, beforeJson));
        await _fs.WriteFileAtomicAsync(forbiddenPath, afterJson);

        var service = new RealmSegregationAutoRollbackService(_fs, NullLogger<RealmSegregationAutoRollbackService>.Instance);

        var result = await service.TryRollbackForbiddenRealmMutationsAsync(
            "Chaos Sea",
            new[] { forbiddenPath },
            "test validation");

        Assert.True(result.RolledBack);
        Assert.Equal(beforeJson, await _fs.ReadFileAsync(forbiddenPath));

        var reportJson = await _fs.ReadFileAsync(RealmSegregationAutoRollbackService.ReportPath);
        Assert.NotNull(reportJson);
        using var report = JsonDocument.Parse(reportJson!);
        var root = report.RootElement;
        Assert.Equal("Chaos Sea", root.GetProperty("sourceRealm").GetString());
        Assert.Equal("request_afterlife_auto_rollback_tests", root.GetProperty("requestId").GetString());
        Assert.Equal(12, root.GetProperty("turnNumber").GetInt32());
        var action = Assert.Single(root.GetProperty("actions").EnumerateArray());
        Assert.Equal("restore", action.GetProperty("action").GetString());
        Assert.Equal(forbiddenPath, action.GetProperty("path").GetString());
    }

    [Fact]
    public async Task TryRollbackForbiddenRealmMutationsAsync_ChaosSeaDeletesNewMortalFileWithoutSnapshotBaseline()
    {
        const string forbiddenPath = "game_state/factions/faction_projects.json";
        await WriteAfterlifeSnapshotAsync(
            ("game_state/meta/soul_state.json", """{ "currentRealm": "Chaos Sea", "soulName": "Пепельная Искра" }"""));
        await _fs.WriteFileAtomicAsync(forbiddenPath, """{ "projects": [{ "projectId": "wrong_realm_project" }] }""");

        var service = new RealmSegregationAutoRollbackService(_fs, NullLogger<RealmSegregationAutoRollbackService>.Instance);

        var result = await service.TryRollbackForbiddenRealmMutationsAsync(
            "Chaos Sea",
            new[] { forbiddenPath },
            "test validation");

        Assert.True(result.RolledBack);
        Assert.False(_fs.FileExists(forbiddenPath));

        var reportJson = await _fs.ReadFileAsync(RealmSegregationAutoRollbackService.ReportPath);
        Assert.NotNull(reportJson);
        using var report = JsonDocument.Parse(reportJson!);
        var action = Assert.Single(report.RootElement.GetProperty("actions").EnumerateArray());
        Assert.Equal("delete", action.GetProperty("action").GetString());
        Assert.Equal(forbiddenPath, action.GetProperty("path").GetString());
    }

    [Fact]
    public async Task FilterRestoredForbiddenBaselineIssuesAsync_ChaosSeaSuppressesSnapshotMatchedMortalBaselineIssues()
    {
        const string forbiddenPath = "game_state/factions/faction_core.json";
        const string baselineJson = """{ "factions": [{ "initialId": "temp-faction-merchant-guild-eternia", "name": "Wrong pre-existing baseline" }] }""";
        await WriteAfterlifeSnapshotAsync(
            ("game_state/meta/soul_state.json", """{ "currentRealm": "Chaos Sea", "soulName": "Пепельная Искра" }"""),
            (forbiddenPath, baselineJson));
        await _fs.WriteFileAtomicAsync(forbiddenPath, baselineJson);

        var service = new RealmSegregationAutoRollbackService(_fs, NullLogger<RealmSegregationAutoRollbackService>.Instance);
        var issues = new[]
        {
            new ValidationIssue(
                $"{forbiddenPath}.factions[0].initialId",
                IssueSeverity.Error,
                "Pre-existing mortal baseline faction error.",
                code: "faction_full_object_existing_requires_faction_id"),
            new ValidationIssue(
                "game_state/meta/soul_state.json.currentRealm",
                IssueSeverity.Error,
                "Current realm is still an active afterlife validation target.",
                code: "test_afterlife_current_realm_error")
        };

        var result = await service.FilterRestoredForbiddenBaselineIssuesAsync("Chaos Sea", issues);

        var remaining = Assert.Single(result.RemainingIssues);
        Assert.Equal("test_afterlife_current_realm_error", remaining.Code);
        var suppressed = Assert.Single(result.SuppressedIssues);
        Assert.Equal("faction_full_object_existing_requires_faction_id", suppressed.Code);
    }

    [Fact]
    public async Task FilterRestoredForbiddenBaselineIssuesAsync_DoesNotSuppressChangedForbiddenFile()
    {
        const string forbiddenPath = "game_state/factions/faction_core.json";
        const string baselineJson = """{ "factions": [{ "factionId": "faction_valmont", "name": "Дом Вальмонтов" }] }""";
        const string changedJson = """{ "factions": [{ "initialId": "temp-faction-merchant-guild-eternia", "name": "Wrong changed state" }] }""";
        await WriteAfterlifeSnapshotAsync(
            ("game_state/meta/soul_state.json", """{ "currentRealm": "Chaos Sea", "soulName": "Пепельная Искра" }"""),
            (forbiddenPath, baselineJson));
        await _fs.WriteFileAtomicAsync(forbiddenPath, changedJson);

        var service = new RealmSegregationAutoRollbackService(_fs, NullLogger<RealmSegregationAutoRollbackService>.Instance);
        var issues = new[]
        {
            new ValidationIssue(
                $"{forbiddenPath}.factions[0].initialId",
                IssueSeverity.Error,
                "Changed forbidden file must still be repaired or rolled back.",
                code: "faction_full_object_existing_requires_faction_id")
        };

        var result = await service.FilterRestoredForbiddenBaselineIssuesAsync("Chaos Sea", issues);

        Assert.Empty(result.SuppressedIssues);
        var remaining = Assert.Single(result.RemainingIssues);
        Assert.Equal("faction_full_object_existing_requires_faction_id", remaining.Code);
    }

    private async Task WriteAfterlifeSnapshotAsync(params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_afterlife_auto_rollback_tests";
        const string requestId = "request_afterlife_auto_rollback_tests";
        const int turnNumber = 12;
        const string playerAction = "Ask Azalia about the Chaos Sea.";

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
            await _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{path}", json);
            files[path] = $"game_state/control/pending_turn_snapshot/{path}";
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-06-25T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "ordinary player turn",
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
