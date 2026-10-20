using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises production cleanup leaves with real leases and independent ordinary DATA.
/// No case constructs an engine or issues deletion authority.
/// </summary>
public sealed class JointCleanupValidatorTests
{
    /// <summary>
    /// Requires the exact terminal tuple, valid signal contract and absent error bytes.
    /// </summary>
    /// <param name="fault">
    /// The deliberately invalid terminal field, or none for the valid synthetic contract.
    /// </param>
    /// <returns>
    /// Completion after the real leaf accepts or returns its original refusal without game writes.
    /// </returns>
    [Theory]
    [InlineData("none")]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("session")]
    [InlineData("request")]
    [InlineData("turn")]
    [InlineData("typed-turn")]
    [InlineData("status")]
    [InlineData("timestamp")]
    [InlineData("missing-files")]
    [InlineData("unsafe-files")]
    [InlineData("error")]
    [InlineData("invalid-first")]
    public async Task TerminalRequiresExactOwnedContract(string fault)
    {
        var root = NewOwnedRoot();
        Directory.CreateDirectory(Path.Combine(root, "game_session", "ready"));
        using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        const string session = "synthetic-session", request = "synthetic-request";
        var signal = new JsonObject { ["sessionId"] = session, ["requestId"] = request,
            ["turnNumber"] = 43, ["status"] = "success", ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["filesModified"] = new JsonArray() };
        switch (fault)
        {
            case "session": signal["sessionId"] = "different"; break;
            case "request": signal["requestId"] = "different"; break;
            case "turn": signal["turnNumber"] = 44; break;
            case "typed-turn": signal["turnNumber"] = "43"; break;
            case "status": signal["status"] = "failed"; break;
            case "timestamp": signal["timestamp"] = "invalid"; break;
            case "missing-files": signal.Remove("filesModified"); break;
            case "unsafe-files": signal["filesModified"] = new JsonArray("../outside"); break;
            case "invalid-first": signal["requestId"] = "different";
                Directory.CreateDirectory(Path.Combine(root, "game_session", "ready", "turn_error.json")); break;
        }
        if (fault != "missing") File.WriteAllText(Path.Combine(root, "game_session", "ready", "turn_complete.json"),
            fault == "malformed" ? "{" : signal.ToJsonString());
        if (fault == "error") File.WriteAllText(Path.Combine(root, "game_session", "ready", "turn_error.json"), "");
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var generation = files.GetOrCreateSessionGeneration(lease); // Synthetic contract only.
        var before = JointCleanupValidatorTestData.CaptureImages(root);
        var dirs = JointCleanupValidatorTestData.CaptureDirectories(root);
        await SessionOperationContext.RunBoundAsync(files, generation, lease, async () =>
        {
            Task Verify() => GameEngine.VerifyMortalJointTerminalSignalAsync(files, lease, session, request, 43,
                NullLogger<GameEngine>.Instance);
            if (fault == "none") await Verify();
            else Assert.Equal("joint_cleanup_terminal_not_owned", (await Assert.ThrowsAsync<InvalidDataException>(Verify)).Message);
            return true;
        });
        Assert.Equal(before.OrderBy(p => p.Key), JointCleanupValidatorTestData.CaptureImages(root).OrderBy(p => p.Key));
        Assert.Equal(dirs, JointCleanupValidatorTestData.CaptureDirectories(root));
    }

    /// <summary>
    /// Refuses a foreign manager or closed lease before accepting DATA.
    /// </summary>
    /// <param name="fault">
    /// The owned manager or lease fault injected into this isolated synthetic fixture.
    /// </param>
    /// <returns>
    /// Completion after both leaves preserve the actual physical lease refusal.
    /// </returns>
    [Theory]
    [InlineData("foreign")]
    [InlineData("disposed")]
    public async Task LeavesRefuseLostParticipation(string fault)
    {
        var root = NewOwnedRoot();
        Directory.CreateDirectory(Path.Combine(root, "game_session"));
        using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var generation = files.GetOrCreateSessionGeneration(lease);
        var bothLeavesChecked = false;
        var closing = await Record.ExceptionAsync(() => SessionOperationContext.RunBoundAsync(files, generation, lease, async () =>
        {
            var reader = fault == "foreign" ? new FileSystemManager(Path.Combine(root, "foreign"), NullLogger<FileSystemManager>.Instance) : files;
            if (fault == "disposed") await lease.DisposeAsync();
            Task Terminal() => GameEngine.VerifyMortalJointTerminalSignalAsync(reader, lease, "session", "request", 43, NullLogger<GameEngine>.Instance);
            Task Original() => GameEngine.VerifyMortalJointDestructiveOriginalAsync(reader, lease, new(), "unused");
            await Assert.ThrowsAsync<InvalidOperationException>(Terminal);
            await Assert.ThrowsAsync<InvalidOperationException>(Original);
            bothLeavesChecked = true;
            return true;
        }));
        Assert.True(bothLeavesChecked, closing?.ToString());
        if (fault == "foreign") Assert.Null(closing);
        else Assert.IsType<InvalidOperationException>(closing);
    }

    /// <summary>
    /// Uses the existing operation fence to refuse replacement before any cleanup leaf is admitted.
    /// Ordinary read leaves retain their physical-lease contract and do not acquire a new generation policy.
    /// </summary>
    /// <returns>
    /// Completion after the real generation barrier refuses and both leaf calls remain unentered.
    /// </returns>
    [Fact]
    public async Task GenerationReplacementRefusesBeforeLeafAdmission()
    {
        var root = NewOwnedRoot();
        Directory.CreateDirectory(Path.Combine(root, "game_session"));
        using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var generation = files.GetOrCreateSessionGeneration(lease);
        var injected = false;
        var leafEntered = false;
        await Assert.ThrowsAsync<SessionReplacedException>(() => SessionOperationContext.RunBoundAsync(files, generation, lease, async () =>
        {
            File.WriteAllText(files.SessionGenerationPath,
                JsonSerializer.Serialize(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") }));
            injected = true;
            files.VerifyCurrentSessionOperation(lease);
            leafEntered = true;
            await GameEngine.VerifyMortalJointTerminalSignalAsync(files, lease, "session", "request", 43, NullLogger<GameEngine>.Instance);
            await GameEngine.VerifyMortalJointDestructiveOriginalAsync(files, lease, new(), "unused");
            return true;
        }));
        Assert.True(injected);
        Assert.False(leafEntered);
    }

    /// <summary>
    /// Runs the real destructive-original validator on genuine copied manifest and backup DATA.
    /// </summary>
    /// <param name="fault">
    /// The independently injected authority, manifest, expected-hash or backup fault, or none.
    /// </param>
    /// <returns>
    /// Completion after genuine validation or its unchanged refusal with exact game images retained.
    /// </returns>
    [Theory]
    [InlineData("none")]
    [InlineData("authority-missing")]
    [InlineData("authority-integrity")]
    [InlineData("manifest")]
    [InlineData("expected-hash")]
    [InlineData("backup-missing")]
    [InlineData("backup-changed")]
    public async Task DestructiveOriginalRequiresGenuineAuthorityAndBackups(string fault)
    {
        var owned = NewOwnedRoot();
        Directory.CreateDirectory(owned);
        using var cleanup = new CleanupOwnedFixture(owned, Console.WriteLine);
        var root = Path.Combine(owned, "copy");
        JointCleanupDataTemplate.Materialize(JointCleanupValidatorTestData.ArchivePath, JointCleanupValidatorTestData.ArchiveSha256, root);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var generation = files.ReadExistingSessionGeneration(lease)
            ?? throw new InvalidDataException("Observed DATA has no existing generation.");
        await SessionOperationContext.RunBoundAsync(files, generation, lease, async () =>
        {
            var manifest = await JointCleanupValidatorTestData.ReadManifestAsync(files, lease);
            var expected = manifest.ManifestPayloadHash;
            var authority = files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath);
            if (fault == "authority-missing") File.Delete(authority);
            if (fault == "authority-integrity")
            {
                var json = JsonNode.Parse(File.ReadAllText(authority))!.AsObject();
                json["payloadSha256"] = new string('0', 64); File.WriteAllText(authority, json.ToJsonString());
            }
            if (fault == "manifest") manifest.PlayerAction += "deliberate drift";
            if (fault == "expected-hash") expected = new string('0', 64);
            Assert.NotEmpty(manifest.RollbackBackups);
            var backup = files.ResolvePath(manifest.RollbackBackups.OrderBy(p => p.Key, StringComparer.Ordinal).First().Value);
            if (fault == "backup-missing") File.Delete(backup);
            if (fault == "backup-changed") File.AppendAllText(backup, "deliberate drift");
            var before = JointCleanupValidatorTestData.CaptureImages(root);
            var generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
            if (fault == "none")
            {
                var payload = await GameEngine.VerifyMortalJointDestructiveOriginalAsync(files, lease, manifest, expected);
                Assert.Equal(expected, payload.ManifestPayloadHash);
                Assert.Equal(manifest.RollbackBackups.Count, payload.RollbackBackups.Count);
            }
            else
            {
                var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                    GameEngine.VerifyMortalJointDestructiveOriginalAsync(files, lease, manifest, expected));
                Assert.StartsWith("joint_cleanup_original_backup_authority: ", error.Message);
            }
            Assert.Equal(before.OrderBy(p => p.Key), JointCleanupValidatorTestData.CaptureImages(root).OrderBy(p => p.Key));
            Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
            return true;
        });
    }

    /// <summary>
    /// Selects a new isolated root under the repository's ordinary diagnostic directory.
    /// </summary>
    /// <returns>
    /// A unique nonexistent path owned only by the current test.
    /// </returns>
    private static string NewOwnedRoot() => Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-cleanup-controls", Guid.NewGuid().ToString("N"));
}
