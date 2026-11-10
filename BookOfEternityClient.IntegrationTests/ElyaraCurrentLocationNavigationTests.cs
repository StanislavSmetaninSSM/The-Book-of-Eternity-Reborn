using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Current canonical provider navigation; no live GM or frontend DOM.</summary>
public sealed partial class ElyaraCurrentLocationNavigationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task TreatmentEntryOffersCanonicalElyaraNavigationWithoutMutatingState()
    {
        // Reuse the accepted small public-service fixture, without constructing
        // GameEngine, drawing treatment dice or staging an ordinary turn.
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20);
        var files = fixture.Files;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            _ = files.GetOrCreateSessionGeneration(lease);
        var before = SnapshotCanonical(files);
        var result = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
        var state = Assert.IsType<WoundCommandState>(result.WoundCommandState);
        var paid = Assert.Single(state.Choices, choice => choice.Available && choice.Selection.Service is not null);
        Assert.Equal(25, paid.Selection.Service!.Quote.Amount); // Valid existing locality/profile is the control.
        AssertCanonicalUnchanged(files, before);
        output.WriteLine("Existing exact local Elyara quote is available at25; no canonical bytes changed before navigation assertion.");
        Assert.Contains(result.Actions, action => action.Label == "Пойти к Элиаре за лечением");
        AssertCanonicalUnchanged(files, before);
    }

    private static Dictionary<string, byte[]> SnapshotCanonical(FileSystemManager files)
    {
        var basePath = Path.GetDirectoryName(files.ResolvePath("game_state"))!;
        return new[] { "game_state", "input", "output" }
            .Select(name => Path.Combine(basePath, name)).Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(path => Path.GetRelativePath(basePath, path), File.ReadAllBytes, StringComparer.Ordinal);
    }

    private static void AssertCanonicalUnchanged(FileSystemManager files, Dictionary<string, byte[]> before)
    {
        var after = SnapshotCanonical(files);
        Assert.Equal(before.Keys, after.Keys);
        foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
    }
}
