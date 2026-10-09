using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserDirectGachaLinuxBoundaryTests
{
    [Theory]
    [InlineData("case_alias")]
    [InlineData("output_alias")]
    [InlineData("literal_backslash")]
    [InlineData("outer_trim")]
    [InlineData("story_alias")]
    [InlineData("cleanup_alias")]
    [InlineData("unicode")]
    public async Task OriginalBrowserGachaAdmitsRawSnapshotNamesBeforeStageOrSpend(string mode)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        var names = PutBrowserExactNames(fixture, mode);
        var before = CaptureBrowserExactFiles(fixture); var generation = File.ReadAllBytes(fixture.Files.SessionGenerationPath);
        var mutations = new List<string>(); fixture.Mutation = path => { mutations.Add(path); return Task.CompletedTask; };
        BrowserPromptWriteResult? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await fixture.PullAsync());
        var after = CaptureBrowserExactFiles(fixture);
        output.WriteLine(JsonSerializer.Serialize(new { root = fixture.Root, mode, names, failure = failure?.ToString(),
            before, after, mutations, generationBefore = generation, generationAfter = File.ReadAllBytes(fixture.Files.SessionGenerationPath), result }));
        Assert.Equal(generation, File.ReadAllBytes(fixture.Files.SessionGenerationPath));
        if (mode == "unicode")
        {
            Assert.Null(failure); Assert.True(result!.Success, result.Message); Assert.Equal(11, fixture.Feathers());
            var snapshotPath = fixture.Manifest()["files"]![names[0]]!.GetValue<string>();
            Assert.Equal(before[names[0]], File.ReadAllBytes(fixture.Files.ResolvePath(snapshotPath)));
            Assert.Equal(fixture.BeforeSoul, File.ReadAllBytes(fixture.BackupPath()));
        }
        else
        {
            Assert.IsType<InvalidDataException>(failure);
            Assert.Null(result); Assert.Empty(mutations);
            AssertBrowserExactFilesUnchanged(before, after); Assert.Equal(18, fixture.Feathers());
        }
    }

    [Theory]
    [InlineData("case_alias")]
    [InlineData("output_alias")]
    [InlineData("literal_backslash")]
    [InlineData("outer_trim")]
    [InlineData("story_alias")]
    [InlineData("cleanup_alias")]
    [InlineData("rollback_value")]
    [InlineData("unicode")]
    public async Task OriginalBrowserQueueAdmitsRawInventoryAndRollbackBeforeCopiesOrFailureCleanup(string mode)
    {
        using var fixture = new BrowserDirectGachaLinuxFixture(output);
        await fixture.InitializeAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var actualBackup = await ExplorerLocalTurnRollbackArtifacts.StageFileAsync(fixture.Files, lease,
            BrowserDirectGachaLinuxFixture.Soul, "browser_direct_gacha");
        Assert.Equal(fixture.BeforeSoul, await fixture.Files.ReadFileBytesAsync(lease, actualBackup));
        var pending = await new PendingTurnStateService(fixture.Files, NullLogger<PendingTurnStateService>.Instance).GetOrCreateAsync(lease);
        var names = PutBrowserExactNames(fixture, mode);
        var retained = new[] { BrowserPendingTurnInspector.TurnRequestPath,
            BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
            BrowserPendingTurnInspector.PendingTurnSnapshotDirectory + "/old.bin" };
        foreach (var path in retained) PutBrowserExactFile(fixture, path, Encoding.UTF8.GetBytes("retained browser evidence:" + path));
        var supplied = mode == "rollback_value" ? "lore/odd\\leaf.json.rollback.supplied" : actualBackup;
        var before = CaptureBrowserExactFiles(fixture); var generation = File.ReadAllBytes(fixture.Files.SessionGenerationPath);
        var mutations = new List<string>(); fixture.Mutation = path => { mutations.Add(path); return Task.CompletedTask; };
        BrowserQueuedTurnRequest? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await new BrowserAfterlifeTurnRequestQueue(fixture.Files, fixture.State)
            .QueueDirectChaosSeaGachaAsync(lease, new("exact-browser-queue", "browser", "Fixture", TimeSpan.FromMinutes(2)),
                "actual exact queue", pending, supplied, "Chaos Sea"));
        var after = CaptureBrowserExactFiles(fixture);
        output.WriteLine(JsonSerializer.Serialize(new { root = fixture.Root, mode, names, actualBackup, supplied,
            failure = failure?.ToString(), before, after, mutations, generationBefore = generation,
            generationAfter = File.ReadAllBytes(fixture.Files.SessionGenerationPath), resultReturned = result != null }));
        Assert.Equal(generation, File.ReadAllBytes(fixture.Files.SessionGenerationPath));
        if (mode == "unicode")
        {
            Assert.Null(failure); Assert.NotNull(result);
            var snapshotPath = fixture.Manifest()["files"]![names[0]]!.GetValue<string>();
            Assert.Equal(before[names[0]], File.ReadAllBytes(fixture.Files.ResolvePath(snapshotPath)));
            Assert.Equal(fixture.BeforeSoul, await fixture.Files.ReadFileBytesAsync(lease, actualBackup));
        }
        else
        {
            Assert.IsType<InvalidDataException>(failure); Assert.Null(result); Assert.Empty(mutations);
            AssertBrowserExactFilesUnchanged(before, after);
        }
    }

    private static string[] PutBrowserExactNames(BrowserDirectGachaLinuxFixture fixture, string mode)
    {
        var names = mode switch
        {
            "case_alias" => new[] { "lore/Entry.json", "lore/entry.json" },
            "output_alias" => new[] { "output/" + Path.GetFileName(QteSceneService.QteOfferPath).ToUpperInvariant() },
            "literal_backslash" => new[] { "lore/odd\\leaf.json" },
            "outer_trim" => new[] { "lore/trailing.json " },
            "story_alias" => new[] { "stories/Chapter.jsonl", "stories/chapter.jsonl" },
            "cleanup_alias" => new[] { "input/Turn_Request.json" },
            "rollback_value" => Array.Empty<string>(),
            _ => new[] { "lore/ История.json" }
        };
        foreach (var path in names) PutBrowserExactFile(fixture, path,
            Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("{} ")).ToArray());
        return names;
    }

    private static void PutBrowserExactFile(BrowserDirectGachaLinuxFixture fixture, string path, byte[] bytes)
    {
        var full = Path.Combine(fixture.Files.GameSessionPath, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllBytes(full, bytes);
    }

    private static Dictionary<string, byte[]> CaptureBrowserExactFiles(BrowserDirectGachaLinuxFixture fixture) =>
        Directory.EnumerateFiles(fixture.Files.GameSessionPath, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(fixture.Files.GameSessionPath, path), File.ReadAllBytes, StringComparer.Ordinal);

    private static void AssertBrowserExactFilesUnchanged(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
    {
        Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal), after.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
    }
}
