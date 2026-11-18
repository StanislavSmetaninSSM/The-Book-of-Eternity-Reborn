using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmHeldReadyDiagnosticTests
{
    [Fact]
    public async Task OriginalCanonicalHoldReleasedByActualHelperStdinClose()
    {
        // Historical method/category identity retained; the acceptance contract now
        // requires cooperative cancellation, never the old expected Unresolved.
        string? folder=null;
        await GmOwnedTerminalLinuxTests.RunAsync("terminal-main-held-ready",value=>folder=value);
        Assert.NotNull(folder);
        using var evidence=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder!,"scenario.json")));
        var root=evidence.RootElement;
        Assert.Equal("CancelledClosedObservedAfterTimeout",root.GetProperty("HeldReadyClassification").GetString());
        var nonce=root.GetProperty("StopDiagnosticNonce").GetString();
        var original=JsonSerializer.Deserialize<GmSessionRunIdentity>(root.GetProperty("HeldReadyActualTerminalClose").GetProperty("Identity"))!;
        var markers=new List<JsonElement>();
        foreach(var line in await File.ReadAllLinesAsync(Path.Combine(folder!,"guardian.log")))
        {
            const string prefix="BOE_STATUS_PUBLICATION_DATA ";
            if(!line.StartsWith(prefix,StringComparison.Ordinal))continue;
            using var parsed=JsonDocument.Parse(line[prefix.Length..]);
            var value=parsed.RootElement;
            if(value.GetProperty("Nonce").GetString()!=nonce)continue;
            Assert.Equal(1,value.GetProperty("SchemaVersion").GetInt32());
            Assert.Equal(original,JsonSerializer.Deserialize<GmSessionRunIdentity>(value.GetProperty("OriginalOwnerIdentity")));
            markers.Add(value.Clone());
        }
        var initialHash=root.GetProperty("HeldReadyInitialStatusSHA256").GetString();
        var latestHash=root.GetProperty("HeldReadyLatestStatusSHA256").GetString();
        Assert.NotEqual(initialHash,latestHash);
        var initial=markers.FindIndex(m=>m.GetProperty("Stage").GetString()=="Published"&&m.GetProperty("SnapshotSHA256").GetString()==initialHash);
        var firstQueuedAt=root.GetProperty("HeldReadyQueuedStatusUpdatedAt").GetString();
        var deferred=markers.FindIndex(m=>m.GetProperty("Stage").GetString()=="Deferred"&&m.GetProperty("PriorActualPublication").GetBoolean()&&
            m.GetProperty("SnapshotUpdatedAtUtc").GetString()==firstQueuedAt&&m.GetProperty("Decision").GetString()=="KeepNewer");
        var latest=markers.FindIndex(m=>m.GetProperty("Stage").GetString()=="Published"&&m.GetProperty("SnapshotSHA256").GetString()==latestHash);
        Assert.True(initial>=0&&deferred>initial&&latest>deferred,"Missing actual initial-publication→deferral→fresh latest-publication chain.");
        Assert.Equal("KeepNewer",markers[deferred].GetProperty("Decision").GetString());
        var deferredHash=markers[deferred].GetProperty("SnapshotSHA256").GetString();
        Assert.Matches("^[0-9a-f]{64}$",deferredHash!);
        Assert.NotEqual(initialHash,deferredHash);Assert.NotEqual(latestHash,deferredHash);
        var secondEntry=root.GetProperty("HeldReadySecondStatusEntryUtc").GetDateTimeOffset();
        Assert.True(markers[deferred].GetProperty("ObservedAtUtc").GetDateTimeOffset()<=secondEntry);
        Assert.True(root.GetProperty("HeldReadyReleaseUtc").GetDateTimeOffset()>=secondEntry);
        Assert.True(root.GetProperty("HeldReadyReleaseUtc").GetDateTimeOffset()>=root.GetProperty("HeldReadyCancellationSentUtc").GetDateTimeOffset());
        await File.WriteAllTextAsync(Path.Combine(folder!,"held-ready-status-provenance.json"),JsonSerializer.Serialize(new{
            Initial=markers[initial],Deferred=markers[deferred],Latest=markers[latest]}));
        Assert.True(root.GetProperty("OriginalStoppedAndEndpointRemoved").GetBoolean());
        // Only the reached decision is qualified. Separate tests own other races.
    }
}
