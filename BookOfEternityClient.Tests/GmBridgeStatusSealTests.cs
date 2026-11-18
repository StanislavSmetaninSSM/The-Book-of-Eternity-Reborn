using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmBridgeStatusSealTests
{
    [Theory]
    [InlineData("terminal-main-status-initial-contention")]
    [InlineData("terminal-main-status-seal-contention")]
    public async Task ActualOriginalPublisherContentionRespectsInitialAndSealBoundaries(string mode)
    {
        string? folder = null;
        await GmOwnedTerminalLinuxTests.RunAsync(mode, value => folder = value);
        using var scenario = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder!, "scenario.json")));
        var data = scenario.RootElement;
        var nonce = data.GetProperty("Nonce").GetString();
        var owner = data.GetProperty("Original").GetRawText();
        var records = new List<JsonElement>();
        foreach (var line in await File.ReadAllLinesAsync(Path.Combine(folder!, "guardian.log")))
        {
            const string prefix = "BOE_STATUS_PUBLICATION_DATA ";
            var offset = line.IndexOf(prefix, StringComparison.Ordinal);
            if (offset < 0) continue;
            using var doc = JsonDocument.Parse(line[(offset + prefix.Length)..]);
            var record = doc.RootElement;
            if (record.GetProperty("Nonce").GetString() != nonce) continue;
            Assert.Equal(owner, record.GetProperty("OriginalOwnerIdentity").GetRawText());
            records.Add(record.Clone());
        }
        Assert.True(data.GetProperty("HostCompleted").GetBoolean());
        if (mode == "terminal-main-status-initial-contention")
        {
            Assert.Empty(records);
            Assert.True(data.GetProperty("ExpectedInitialLogicalUncertainty").GetBoolean());
            Assert.Contains("CanonicalLockContention", data.GetProperty("OriginalFirstPublicationFailure").GetString());
        }
        else
        {
            var initial = records.FindIndex(item => item.GetProperty("Stage").GetString() == "Published");
            var discarded = records.FindIndex(item => item.GetProperty("Stage").GetString() == "Deferred" &&
                item.GetProperty("Decision").GetString() == "DiscardSealed");
            Assert.True(initial >= 0 && discarded > initial);
            Assert.True(records[discarded].GetProperty("PriorActualPublication").GetBoolean());
            Assert.DoesNotContain(records.Skip(discarded + 1), item => item.GetProperty("Stage").GetString() == "Published");
            Assert.True(data.GetProperty("SealedSnapshotUnchanged").GetBoolean());
        }
    }
}
