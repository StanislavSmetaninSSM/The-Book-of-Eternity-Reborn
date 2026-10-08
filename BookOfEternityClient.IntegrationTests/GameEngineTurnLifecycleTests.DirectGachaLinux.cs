using System.Collections;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task DirectGachaLinux_TurnLifetime_OriginalCaptureValidatedRollbackAndCleanup()
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(_directGachaOutput!, _rootPath);
        await fixture.InitializeAsync();
        Assert.True((await fixture.PullAsync()).Success);
        var path = fixture.BackupPath();
        var relative = Path.GetRelativePath(fixture.Files.GameSessionPath, path).Replace('\\', '/');
        var engine = CreateGameEngine(fileSystem: fixture.Files);
        var captured = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "linux_actual_gacha");
        var mapping = (IDictionary)captured.GetType().GetProperty("BackupFiles")!.GetValue(captured)!;
        Assert.Equal(relative, mapping[BrowserDirectGachaLinuxFixture.Soul]);
        var manifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
        var validated = await InvokePrivateTaskResultAsync(engine, "GetValidatedRollbackSnapshotAsync", manifest);
        await InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", validated);
        Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(fixture.Files.ResolvePath(BrowserDirectGachaLinuxFixture.Soul)));
        Assert.Equal(fixture.BeforeHistory, File.ReadAllBytes(fixture.Files.ResolvePath("game_state/history/chat_log.json")));
        InvokePrivate(engine, "CleanupBackup", captured);
        await InvokePrivateTaskAsync(engine, "CleanupPendingTurnSnapshotAsync");
        Assert.False(File.Exists(path));
        Assert.False(File.Exists(fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
        Assert.False(File.Exists(fixture.Files.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath)));
    }

    [Theory]
    [InlineData("consumed-deletion-debt")]
    [InlineData("unmapped")]
    [InlineData("changed-bytes")]
    [InlineData("changed-request")]
    [InlineData("changed-action")]
    public async Task DirectGachaLinux_AdoptionRefuses_OriginalCaptureRetainsUnboundEvidence(string cut)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(_directGachaOutput!, _rootPath);
        await fixture.InitializeAsync();
        Assert.True((await fixture.PullAsync()).Success);
        var path = fixture.BackupPath();
        var relative = Path.GetRelativePath(fixture.Files.GameSessionPath, path).Replace('\\', '/');
        var engine = CreateGameEngine(fileSystem: fixture.Files);
        if (cut == "consumed-deletion-debt")
        {
            var captured = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "consumed_gacha");
            var reached = false;
            fixture.Mutation = member =>
            {
                if (member == relative) { reached = true; throw new InvalidOperationException("consumed backup deletion cut"); }
                return Task.CompletedTask;
            };
            InvokePrivate(engine, "CleanupBackup", captured); // Original consumer swallows/logs this deletion debt.
            Assert.True(reached); Assert.True(File.Exists(path));
            fixture.Mutation = null;
            await InvokePrivateTaskAsync(engine, "CleanupAcceptedTurnTerminalArtifactsAsync");
            Assert.False(File.Exists(fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
            Assert.False(File.Exists(fixture.Files.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath)));
            Assert.False(File.Exists(fixture.Files.ResolvePath(BrowserPendingTurnInspector.TurnRequestPath)));
        }
        else if (cut == "unmapped")
        {
            path = fixture.Files.ResolvePath(BrowserDirectGachaLinuxFixture.DirectRoot + "/" + DateTime.UtcNow.Ticks + "_" + Guid.NewGuid().ToString("N") +
                "/game_state_meta_soul_state.json.rollback." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, fixture.BeforeSoul); // Controlled residue, no invented authority.
        }
        else if (cut == "changed-bytes") File.WriteAllBytes(path, [0, 255, 41]);
        else
        {
            var requestPath = fixture.Files.ResolvePath(BrowserPendingTurnInspector.TurnRequestPath);
            var request = JsonNode.Parse(File.ReadAllText(requestPath))!.AsObject();
            request[cut == "changed-request" ? "requestId" : "playerAction"] = "different-current-request";
            File.WriteAllText(requestPath, request.ToJsonString());
        }
        var retained = File.ReadAllBytes(path);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "fresh_capture"));
        Assert.Contains("direct-gacha", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(retained, File.ReadAllBytes(path));
        Assert.Equal(11, fixture.Feathers());
        Assert.Equal(fixture.BeforeHistory, File.ReadAllBytes(fixture.Files.ResolvePath("game_state/history/chat_log.json")));
        Assert.Empty(Directory.GetFiles(fixture.Files.GameSessionPath, "*.rollback.fresh_capture", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("sessionId")]
    [InlineData("requestId")]
    [InlineData("turnNumber")]
    [InlineData("timestamp")]
    [InlineData("changed-session")]
    [InlineData("changed-turn")]
    [InlineData("changed-timestamp")]
    public async Task DirectGachaLinux_RawRequestRefusal_OriginalCaptureDoesNotMintIdentity(string cut)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(_directGachaOutput!, _rootPath);
        await fixture.InitializeAsync(); Assert.True((await fixture.PullAsync()).Success);
        var path = fixture.BackupPath(); var bytes = File.ReadAllBytes(path);
        var manifestPath = fixture.Files.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath);
        var authorityPath = fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath);
        var manifest = File.ReadAllBytes(manifestPath); var authority = File.ReadAllBytes(authorityPath);
        var requestPath = fixture.Files.ResolvePath(BrowserPendingTurnInspector.TurnRequestPath);
        if (cut == "absent") File.Delete(requestPath);
        else
        {
            var request = JsonNode.Parse(File.ReadAllText(requestPath))!.AsObject();
            if (cut.StartsWith("changed-", StringComparison.Ordinal))
            {
                var member = cut == "changed-session" ? "sessionId" : cut == "changed-turn" ? "turnNumber" : "timestamp";
                request[member] = member == "turnNumber" ? JsonValue.Create(88) : JsonValue.Create("different-current-context");
            }
            else request.Remove(cut);
            File.WriteAllText(requestPath, request.ToJsonString());
        }
        var engine = CreateGameEngine(fileSystem: fixture.Files);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "raw_identity_refusal"));
        Assert.Contains("direct-gacha", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Equal(manifest, File.ReadAllBytes(manifestPath)); Assert.Equal(authority, File.ReadAllBytes(authorityPath));
        Assert.Equal(11, fixture.Feathers());
        Assert.Empty(Directory.GetFiles(fixture.Files.GameSessionPath, "*.rollback.raw_identity_refusal", SearchOption.AllDirectories));
    }
}
