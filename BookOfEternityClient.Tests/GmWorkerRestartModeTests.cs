using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartModeTests
{
    [Fact]
    public async Task LegacyMode_RetainsCommonLockAndNeverUpgradesToDurable()
    {
        using var fixture = new GmWorkerRunLedgerTests.LedgerFixture();
        byte[] binding;
        using (var legacy = await GmWorkerRunLedger.OpenLegacyFixtureOwnerAsync(fixture.Target))
        {
            Assert.NotNull(legacy); legacy.Verify();
            binding = File.ReadAllBytes(Path.Combine(fixture.Target.DirectoryPath, "mode.json"));
            Assert.False(File.Exists(fixture.StatePath));
            using var duplicate = await GmWorkerRunLedger.OpenLegacyFixtureOwnerAsync(fixture.Target);
            Assert.Null(duplicate);
            await using var durable = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target);
            Assert.Null(durable);
        }
        var before = Snapshot(fixture.Target);
        await using (var durable = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target)) Assert.Null(durable);
        Assert.Equal(before, Snapshot(fixture.Target));
        using (var reopened = await GmWorkerRunLedger.OpenLegacyFixtureOwnerAsync(fixture.Target))
        { Assert.NotNull(reopened); reopened.Verify(); }
        Assert.Equal(binding, File.ReadAllBytes(Path.Combine(fixture.Target.DirectoryPath, "mode.json")));
        Assert.Equal(WorkerRunObservationKind.Blocked, (await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Kind);
        fixture.AssertSentinel();
    }

    [Fact]
    public async Task DurableMode_BindsRootBeforeInitializationAndAlwaysRejectsLegacy()
    {
        using var fixture = new GmWorkerRunLedgerTests.LedgerFixture();
        await using (var durable = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target))
        {
            Assert.NotNull(durable);
            var mode = Path.Combine(fixture.Target.DirectoryPath, "mode.json");
            Assert.True(File.Exists(mode), "Mode must be installed before first state/Start, not inferred after release.");
            var json = JsonNode.Parse(File.ReadAllBytes(mode))!;
            Assert.Equal("DurableSynthetic", (string?)json["Mode"]);
            Assert.Equal(fixture.Target.RootPath, (string?)json["RootKey"]);
            using var legacy = await GmWorkerRunLedger.OpenLegacyFixtureOwnerAsync(fixture.Target);
            Assert.Null(legacy);
            Assert.Equal(WorkerLedgerMutationKind.Applied, await durable.InitializeAsync());
        }
        var before = Snapshot(fixture.Target);
        using (var legacy = await GmWorkerRunLedger.OpenLegacyFixtureOwnerAsync(fixture.Target)) Assert.Null(legacy);
        Assert.Equal(before, Snapshot(fixture.Target));
        await using var reopened = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target);
        Assert.NotNull(reopened);
        Assert.Equal(WorkerLedgerMutationKind.AlreadyExact, await reopened.InitializeAsync());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("unknown-mode")]
    [InlineData("wrong-root")]
    [InlineData("unknown-field")]
    [InlineData("duplicate-field")]
    public async Task DamagedMode_ClosesLiveAndColdAuthorityWithoutRepair(string mutation)
    {
        using var fixture = new GmWorkerRunLedgerTests.LedgerFixture();
        await using var owner = await fixture.OpenInitialized();
        var mode = Path.Combine(fixture.Target.DirectoryPath, "mode.json");
        Assert.True(File.Exists(mode), "An initialized current namespace must have an immutable mode binding.");
        var json = JsonNode.Parse(File.ReadAllBytes(mode))!.AsObject();
        switch (mutation)
        {
            case "missing": File.Delete(mode); break;
            case "empty": File.WriteAllBytes(mode, []); break;
            case "unknown-mode": json["Mode"] = "unknown"; File.WriteAllText(mode, json.ToJsonString()); break;
            case "wrong-root": json["RootKey"] = Path.GetTempPath(); File.WriteAllText(mode, json.ToJsonString()); break;
            case "unknown-field": json["Permit"] = true; File.WriteAllText(mode, json.ToJsonString()); break;
            case "duplicate-field": File.WriteAllText(mode, json.ToJsonString().Replace("\"Mode\":", "\"Mode\":\"DurableSynthetic\",\"Mode\":")); break;
        }
        var stateBefore = File.ReadAllBytes(fixture.StatePath);
        var modeBefore = File.Exists(mode) ? File.ReadAllBytes(mode) : null;
        Assert.Equal(WorkerLedgerMutationKind.Blocked, await owner.InitializeAsync());
        Assert.Equal(stateBefore, File.ReadAllBytes(fixture.StatePath));
        Assert.Equal(modeBefore, File.Exists(mode) ? File.ReadAllBytes(mode) : null);
        await owner.DisposeAsync();
        // The full byte snapshot includes lock files only after releasing our
        // original exclusive owner. Do not ask any reader to bypass that lock.
        var before = Snapshot(fixture.Target);
        Assert.Equal(WorkerRunObservationKind.Blocked, (await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Kind);
        await using (var cold = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target)) Assert.Null(cold);
        using (var legacy = await GmWorkerRunLedger.OpenLegacyFixtureOwnerAsync(fixture.Target)) Assert.Null(legacy);
        Assert.Equal(before, Snapshot(fixture.Target));
        fixture.AssertSentinel();
    }

    private static string[] Snapshot(WorkerLedgerTarget target) => Directory.EnumerateFiles(target.RootPath, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(target.RootPath, path) + ":" +
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToArray();
}
