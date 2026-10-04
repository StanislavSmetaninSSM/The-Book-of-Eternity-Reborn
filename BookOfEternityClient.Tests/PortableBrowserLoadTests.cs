using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Exercises the actual browser load caller with independently owned state and real publication cuts.</summary>
public sealed class PortableBrowserLoadTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-browser-load-" + Guid.NewGuid().ToString("N"));

    /// <summary>Preserves decisions, identity and late admission through the real browser service.</summary>
    /// <param name="scenario">Selects the actual publication, admission or required menu boundary.</param>
    [Theory]
    [InlineData("committed")]
    [InlineData("rollback")]
    [InlineData("uncertain")]
    [InlineData("service-refresh")]
    [InlineData("menu-refresh")]
    [InlineData("newer-generation")]
    [InlineData("late-pending")]
    [InlineData("late-token")]
    [InlineData("missing-source")]
    public async Task BrowserLoadRetainsTypedDecisionAndAdmission(string scenario)
    {
        var armed = false; var published = false; var cuts = 0; var unsafeReads = 0;
        FileSystemManager? files = null;
        const string marker = "lore/browser-load.bin";
        files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (!armed) return;
                    if (phase == TrustedLocalPublicationPhase.CommitStaged && scenario is "rollback" or "uncertain")
                    {
                        cuts++;
                        if (scenario == "uncertain") File.WriteAllBytes(files!.ResolvePath(marker), [90, 91]);
                        throw new InvalidOperationException("browser load publication cut");
                    }
                    if (phase == TrustedLocalPublicationPhase.Committed) published = true;
                },
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (cuts > 0 && scenario == "uncertain") unsafeReads++;
                    if (published && scenario == "menu-refresh" && path.EndsWith(".zip", StringComparison.Ordinal))
                    { cuts++; throw new IOException("private path must not leak"); }
                    return Task.CompletedTask;
                }
            });
        var state = PortableSaveFixture.Seed(files);
        await files.WriteFileAtomicBytesAsync(marker, [1, 2]);
        var producer = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance);
        Assert.True(await producer.SaveGameAsync("browser-load", "typed browser test"));
        var path = Assert.Single(Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip"));
        var archiveBytes = File.ReadAllBytes(path);
        await files.WriteFileAtomicBytesAsync(marker, [3, 4]);
        var generationBytes = File.ReadAllBytes(files.SessionGenerationPath);
        var oldGeneration = JsonNode.Parse(generationBytes)!["generationId"]!.GetValue<string>();
        var newer = Guid.NewGuid().ToString("N");
        byte[]? replacementLockBytes = null;
        var save = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance, new SaveLoadServiceHooks
        {
            BeforeLoadLeaseAcquisitionAsync = async () =>
            {
                if (scenario == "late-pending") await files.WriteFileAtomicAsync("input/turn_request.json", "{}");
                if (scenario == "late-token")
                {
                    var node = JsonNode.Parse(File.ReadAllBytes(files.ResolvePath(LocalUiSessionLockService.LockPath)))!.AsObject();
                    node["leaseToken"] = Guid.NewGuid().ToString("N");
                    replacementLockBytes = JsonSerializer.SerializeToUtf8Bytes(node);
                    File.WriteAllBytes(files.ResolvePath(LocalUiSessionLockService.LockPath), replacementLockBytes);
                }
            },
            AfterLoadPublicationValidatedAsync = () =>
            {
                if (scenario == "service-refresh") { cuts++; throw new IOException("private refresh detail"); }
                if (scenario == "newer-generation")
                    File.WriteAllBytes(files.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = newer }));
                return Task.CompletedTask;
            }
        });
        var coordinator = new BrowserLocalWriteCoordinator(files, new LocalUiSessionLockService(files));
        var session = new LocalWebUiSessionStatusService(files, coordinator);
        var lifecycle = new BrowserLifecycleDashboardService(files, session, new ValidationService(files, NullLogger<ValidationService>.Instance));
        var menu = new LocalWebUiMainMenuService(files, lifecycle, save, state, coordinator);
        var saveId = "manual:" + Path.GetFileName(path);
        armed = true;
        var result = await menu.LoadSaveAsync(new BrowserLoadSaveRequest(scenario == "missing-source" ? "missing" : saveId));
        var json = JsonSerializer.SerializeToNode(result, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var expected = scenario is "late-pending" or "late-token" or "missing-source" ? "NotLoaded"
            : scenario == "rollback" ? "RolledBack" : scenario == "uncertain" ? "Uncertain" : "Committed";
        Assert.Equal(expected, json["disposition"]?.GetValue<string>());
        Assert.Equal(expected == "Committed", result.Success);
        Assert.Equal(expected == "Committed" ? saveId : string.Empty, result.LoadedSaveId);
        var blocked = scenario is "uncertain" or "service-refresh" or "menu-refresh" or "newer-generation";
        Assert.Equal(blocked, json["continuationBlocked"]!.GetValue<bool>());
        Assert.DoesNotContain("private", result.Error);
        Assert.Equal(archiveBytes, File.ReadAllBytes(path));
        if (expected == "Committed")
        {
            Assert.NotNull(json["establishedGeneration"]);
            Assert.NotEqual(oldGeneration, json["establishedGeneration"]!.GetValue<string>());
            Assert.NotEqual(newer, json["establishedGeneration"]!.GetValue<string>());
            Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(files.ResolvePath(marker)));
        }
        else if (expected == "Uncertain")
        {
            Assert.Null(json["establishedGeneration"]); Assert.True(json["needsFollowUp"]!.GetValue<bool>());
            Assert.Equal(0, unsafeReads);
            Assert.Equal(new byte[] { 90, 91 }, File.ReadAllBytes(files.ResolvePath(marker)));
        }
        else
        {
            Assert.Equal(generationBytes, File.ReadAllBytes(files.SessionGenerationPath));
            Assert.Equal(new byte[] { 3, 4 }, File.ReadAllBytes(files.ResolvePath(marker)));
            if (expected == "RolledBack") Assert.Equal(oldGeneration, json["establishedGeneration"]!.GetValue<string>());
        }
        if (blocked) Assert.Null(result.Menu);
        if (scenario == "late-token") Assert.Equal(replacementLockBytes, File.ReadAllBytes(files.ResolvePath(LocalUiSessionLockService.LockPath)));
        if (scenario is "rollback" or "uncertain" or "service-refresh" or "menu-refresh") Assert.True(cuts > 0);
    }

    /// <summary>Removes only this case's owned temporary root.</summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
