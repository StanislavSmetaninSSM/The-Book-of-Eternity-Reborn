using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableLoadReplacementTests
{
    private const string DaemonArchiveStatus = "game_state/control/gm_daemon_status.json";
    private const string ArchiveForeignSession = "/former-independent-test-root/game_session";
    private static bool LinuxDaemonArchiveContract => OperatingSystem.IsLinux()
        && RuntimeInformation.ProcessArchitecture == Architecture.X64;
    private string DaemonGuardPath => Path.Combine(_root, ".boe_runtime", "locks", "gm-daemon-instance.lock");

    [Fact]
    public async Task DaemonArchiveStatus_LinuxSaveOmitsLocalStatusWithoutChangingIt()
    {
        if (!LinuxDaemonArchiveContract) return;
        using var guard = LinuxDaemonInstanceGuard.Acquire(_root, null);
        var local = DaemonStatusBytes(_files.GameSessionPath, guard.OwnerId, "running", false);
        Put(DaemonArchiveStatus, local);
        Assert.True(await _service.SaveGameAsync("portable-daemon-status", "host metadata is not portable"));
        var save = Assert.Single(Directory.GetFiles(_files.ResolvePath("saves/manual_saves"), "*.zip"));
        using var archive = ZipFile.OpenRead(save);
        Assert.Null(archive.GetEntry(DaemonArchiveStatus));
        using var manifest = JsonDocument.Parse(archive.GetEntry("save_manifest.json")!.Open());
        Assert.DoesNotContain(manifest.RootElement.GetProperty("entries").EnumerateArray(),
            entry => entry.GetProperty("path").GetString() == DaemonArchiveStatus);
        Assert.Equal(local, File.ReadAllBytes(_files.ResolvePath(DaemonArchiveStatus)));
        guard.Validate();
    }

    [Fact]
    public async Task DaemonArchiveStatus_ManifestedForeignStatusVerifiedButNotMaterializedForFreshRoot()
    {
        if (!LinuxDaemonArchiveContract) return;
        var source = await PrepareDaemonArchiveAsync();
        var protectedFiles = SnapshotLibraryAndSource(source);
        Assert.False(File.Exists(DaemonGuardPath));
        Assert.False(_files.FileExists(DaemonArchiveStatus));

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.False(result.NeedsFollowUp);
        Assert.False(result.ContinuationBlocked);
        Assert.Equal(_loadedMarker, File.ReadAllBytes(_files.ResolvePath(MarkerPath)));
        AssertPreserved(protectedFiles);
        Assert.False(_files.FileExists(DaemonArchiveStatus));
        Assert.False(File.Exists(DaemonGuardPath));
        AssertOwnedScratchEmpty();
        using var fresh = LinuxDaemonInstanceGuard.AcquireWithStatusReader(_root, () =>
        {
            var path = _files.ResolvePath(DaemonArchiveStatus);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        });
        fresh.Validate();
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("length")]
    [InlineData("omitted-manifest")]
    [InlineData("missing-member")]
    [InlineData("descendant")]
    [InlineData("reserved-directory")]
    public async Task DaemonArchiveStatus_InvalidLegacyEvidenceRejectedBeforePublication(string corruption)
    {
        if (!LinuxDaemonArchiveContract) return;
        var source = await PrepareDaemonArchiveAsync(corruption);
        var before = Snapshot(_files.GameSessionPath);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        var failure = Assert.IsType<InvalidDataException>(result.Failure);
        Assert.Contains(corruption switch
        {
            "hash" => "manifested SHA-256 digest",
            "length" or "missing-member" => corruption == "length" ? "manifested length" : "every archive payload",
            "omitted-manifest" => "every archive payload",
            _ => DaemonArchiveStatus
        }, failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, _lifecycleOpen);
        Assert.Empty(_phases);
        Assert.Equal(before.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
        AssertPreserved(before);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.False(File.Exists(DaemonGuardPath));
        Assert.False(_files.FileExists(DaemonArchiveStatus));
        AssertOwnedScratchEmpty();
    }

    [Theory]
    [InlineData("running", false)]
    [InlineData("processing", false)]
    [InlineData("stopped", false)]
    [InlineData("running", true)]
    public async Task DaemonArchiveStatus_SameRootLoadPreservesLiveGuardPairAndRollback(string state, bool rollback)
    {
        if (!LinuxDaemonArchiveContract) return;
        var source = await PrepareDaemonArchiveAsync();
        var archived = SnapshotLibraryAndSource(source);
        var generationBefore = File.ReadAllBytes(_files.SessionGenerationPath);
        var statusPath = _files.ResolvePath(DaemonArchiveStatus);
        string stopped;
        string initialOwner;
        using (var original = LinuxDaemonInstanceGuard.Acquire(_root, null))
        {
            initialOwner = original.OwnerId;
            var settled = state == "stopped";
            var local = DaemonStatusBytes(_files.GameSessionPath, original.OwnerId, state, settled);
            Put(DaemonArchiveStatus, local);
            if (settled) original.MarkSettled(Encoding.UTF8.GetString(local));
            var fullBefore = Snapshot(_files.GameSessionPath);
            var priorWriteTime = File.GetLastWriteTimeUtc(statusPath);
            using var statusBefore = File.OpenHandle(statusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var statusIdentity = LinuxBridgeFifoNative.Stat(statusBefore);
            var writes = 0;
            if (rollback) _fault = (phase, _) =>
            {
                if (phase == TrustedLocalPublicationPhase.MemberPublished && ++writes == 2)
                    throw new InvalidOperationException("controlled cut after ordinary member publication");
            };

            var result = await _service.LoadGameWithOutcomeAsync(source);

            Report(result);
            Assert.Equal(rollback ? LoadReplacementDisposition.RolledBack : LoadReplacementDisposition.Committed, result.Disposition);
            _fault = null;
            Assert.Equal(rollback, generationBefore.SequenceEqual(File.ReadAllBytes(_files.SessionGenerationPath)));
            if (rollback)
            {
                Assert.Equal(fullBefore.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
                AssertPreserved(fullBefore);
            }
            else
            {
                Assert.False(result.NeedsFollowUp);
                Assert.False(result.ContinuationBlocked);
            }
            Assert.Equal(rollback ? new byte[] { 88, 89 } : _loadedMarker, File.ReadAllBytes(_files.ResolvePath(MarkerPath)));
            AssertPreserved(archived);
            Assert.Equal(local, File.ReadAllBytes(statusPath));
            Assert.Equal(priorWriteTime, File.GetLastWriteTimeUtc(statusPath));
            using var statusAfter = File.OpenHandle(statusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Assert.Equal(statusIdentity, LinuxBridgeFifoNative.Stat(statusAfter));
            original.Validate();
            var contenderReads = 0;
            Assert.ThrowsAny<IOException>(() => LinuxDaemonInstanceGuard.AcquireWithStatusReader(_root, () =>
            {
                contenderReads++;
                return File.ReadAllText(statusPath);
            }));
            Assert.Equal(0, contenderReads);
            stopped = Encoding.UTF8.GetString(DaemonStatusBytes(_files.GameSessionPath, original.OwnerId, "stopped", true));
            if (!settled)
            {
                Put(DaemonArchiveStatus, Encoding.UTF8.GetBytes(stopped));
                original.MarkSettled(stopped);
            }
        }
        using var next = LinuxDaemonInstanceGuard.Acquire(_root, stopped);
        Assert.NotEqual(initialOwner, next.OwnerId);
        next.Validate();
        AssertOwnedScratchEmpty();
    }

    [Theory]
    [InlineData("status-only")]
    [InlineData("guard-only")]
    [InlineData("owner-mismatch")]
    [InlineData("foreign-session")]
    [InlineData("settlement-mismatch")]
    [InlineData("linked-guard")]
    [InlineData("public-guard")]
    [InlineData("malformed-guard")]
    [InlineData("malformed-status")]
    [InlineData("duplicate-owner")]
    [InlineData("bad-version")]
    [InlineData("bad-boolean")]
    [InlineData("failed-status")]
    public async Task DaemonArchiveStatus_InvalidLocalPairRefusesBeforeReplacementAndRemainsUntouched(string damage)
    {
        if (!LinuxDaemonArchiveContract) return;
        var source = await PrepareDaemonArchiveAsync();
        var status = _files.ResolvePath(DaemonArchiveStatus);
        string owner = Guid.NewGuid().ToString("N");
        if (damage != "status-only")
        {
            using var guard = LinuxDaemonInstanceGuard.Acquire(_root, null);
            if (damage != "owner-mismatch") owner = guard.OwnerId;
        }
        if (damage != "guard-only") Put(DaemonArchiveStatus, DaemonStatusBytes(
            damage == "foreign-session" ? ArchiveForeignSession : _files.GameSessionPath, owner,
            damage == "settlement-mismatch" ? "stopped" : "running", damage == "settlement-mismatch"));
        if (damage == "linked-guard")
        {
            var linkedTarget = DaemonGuardPath + ".test-owned-target";
            File.Move(DaemonGuardPath, linkedTarget);
            File.CreateSymbolicLink(DaemonGuardPath, linkedTarget);
        }
        if (damage == "malformed-guard")
            File.WriteAllText(DaemonGuardPath, "not_json");
        if (damage is "malformed-status" or "duplicate-owner" or "bad-version" or "bad-boolean" or "failed-status")
        {
            var body = File.ReadAllText(status);
            var malformed = damage switch
            {
                "duplicate-owner" => body.Insert(1, "\"daemonGuardOwnerId\":\"duplicate\","),
                "bad-version" => body.Replace("\"daemonGuardVersion\":1", "\"daemonGuardVersion\":2", StringComparison.Ordinal),
                "bad-boolean" => body.Replace("\"terminalSettled\":false", "\"terminalSettled\":\"false\"", StringComparison.Ordinal),
                "failed-status" => body.Replace("\"status\":\"running\"", "\"status\":\"failed\"", StringComparison.Ordinal),
                _ => "not_json"
            };
            Assert.NotEqual(body, malformed);
            Put(DaemonArchiveStatus, Encoding.UTF8.GetBytes(malformed));
        }
        if (damage == "public-guard")
            File.SetUnixFileMode(DaemonGuardPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        var guardBytes = File.Exists(DaemonGuardPath) ? File.ReadAllBytes(DaemonGuardPath) : null;
        var guardLink = new FileInfo(DaemonGuardPath).LinkTarget;
        var statusBytes = File.Exists(status) ? File.ReadAllBytes(status) : null;
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var before = Snapshot(_files.GameSessionPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        Assert.Empty(_phases);
        Assert.Equal(before.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
        AssertPreserved(before);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(guardBytes, File.Exists(DaemonGuardPath) ? File.ReadAllBytes(DaemonGuardPath) : null);
        Assert.Equal(guardLink, new FileInfo(DaemonGuardPath).LinkTarget);
        Assert.Equal(statusBytes, File.Exists(status) ? File.ReadAllBytes(status) : null);
        AssertOwnedScratchEmpty();
    }

    [Fact]
    public async Task DaemonArchiveStatus_UnsettledPairRetentionNeverAuthorizesAdoption()
    {
        if (!LinuxDaemonArchiveContract) return;
        var source = await PrepareDaemonArchiveAsync();
        byte[] local;
        using (var original = LinuxDaemonInstanceGuard.Acquire(_root, null))
        {
            local = DaemonStatusBytes(_files.GameSessionPath, original.OwnerId, "running", false);
            Put(DaemonArchiveStatus, local);
        }
        var guardBytes = File.ReadAllBytes(DaemonGuardPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.Equal(local, File.ReadAllBytes(_files.ResolvePath(DaemonArchiveStatus)));
        Assert.Equal(guardBytes, File.ReadAllBytes(DaemonGuardPath));
        Assert.ThrowsAny<IOException>(() =>
            LinuxDaemonInstanceGuard.Acquire(_root, Encoding.UTF8.GetString(local)));
        AssertOwnedScratchEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DaemonArchiveStatus_RevalidatesGuardAfterCaptureWithoutRejectingAnUnrelatedSibling(bool changeGuard)
    {
        if (!LinuxDaemonArchiveContract) return;
        var source = await PrepareDaemonArchiveAsync();
        var statusPath = _files.ResolvePath(DaemonArchiveStatus);
        byte[] local;
        using (var daemon = LinuxDaemonInstanceGuard.Acquire(_root, null))
        {
            local = DaemonStatusBytes(_files.GameSessionPath, daemon.OwnerId, "stopped", true);
            Put(DaemonArchiveStatus, local);
            daemon.MarkSettled(Encoding.UTF8.GetString(local));
        }
        var drifted = false;
        var armDrift = false;
        var faultGuard = Encoding.UTF8.GetBytes("""{"changedByTest":true}""");
        var observerPhases = new List<TrustedLocalPublicationPhase>();
        var observed = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = _ =>
                {
                    if (armDrift && !drifted)
                    {
                        if (changeGuard) File.WriteAllBytes(DaemonGuardPath, faultGuard);
                        else Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(DaemonGuardPath)!, "unrelated-runtime-sibling"));
                        drifted = true;
                    }
                    return Task.CompletedTask;
                },
                LocalPublicationObserver = (phase, _) => observerPhases.Add(phase)
            });
        var state = new StateManager(observed, new(), NullLogger<StateManager>.Instance);
        await state.RefreshGameStateAsync();
        observerPhases.Clear();
        Assert.False(drifted);
        var service = new SaveLoadService(observed, state, _logger);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var protectedFiles = Snapshot(_files.GameSessionPath);
        armDrift = true;

        var result = await service.LoadGameWithOutcomeAsync(source);

        Assert.True(drifted);
        if (!changeGuard)
        {
            Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
            Assert.False(result.NeedsFollowUp);
            Assert.Equal(_loadedMarker, File.ReadAllBytes(_files.ResolvePath(MarkerPath)));
            Assert.Equal(local, File.ReadAllBytes(statusPath));
            AssertOwnedScratchEmpty();
            return;
        }
        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        Assert.Empty(observerPhases);
        Assert.Equal(protectedFiles.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
        AssertPreserved(protectedFiles);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(faultGuard, File.ReadAllBytes(DaemonGuardPath));
        Assert.Equal(local, File.ReadAllBytes(statusPath));
        AssertOwnedScratchEmpty();
    }

    private static byte[] DaemonStatusBytes(string session, string owner, string state, bool settled) =>
        JsonSerializer.SerializeToUtf8Bytes(new { status = state, sessionPath = session, daemonGuardVersion = 1,
            daemonGuardOwnerId = owner, terminalSettled = settled });

    private async Task<string> PrepareDaemonArchiveAsync(string? corruption = null)
    {
        var source = await PrepareCurrentArchiveAsync();
        var payload = DaemonStatusBytes(ArchiveForeignSession, "0123456789abcdef0123456789abcdef", "running", false);
        using var archive = ZipFile.Open(source, ZipArchiveMode.Update);
        var oldManifest = archive.GetEntry("save_manifest.json")!;
        JsonObject manifest;
        using (var read = oldManifest.Open()) manifest = JsonNode.Parse(read)!.AsObject();
        oldManifest.Delete();
        var path = corruption == "descendant" ? DaemonArchiveStatus + "/child.bin" : DaemonArchiveStatus;
        if (corruption == "reserved-directory") archive.CreateEntry(DaemonArchiveStatus + "/");
        else if (corruption != "missing-member")
        {
            using var entry = archive.CreateEntry(path).Open();
            entry.Write(payload);
        }
        if (corruption is not ("omitted-manifest" or "reserved-directory"))
        {
            var digest = corruption == "hash" ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(payload));
            manifest["entries"]!.AsArray().Add(new JsonObject
            {
                ["path"] = path, ["length"] = payload.LongLength + (corruption == "length" ? 1 : 0), ["sha256"] = digest
            });
        }
        using (var replacement = archive.CreateEntry("save_manifest.json").Open())
            JsonSerializer.Serialize(replacement, manifest);
        return source;
    }
}
