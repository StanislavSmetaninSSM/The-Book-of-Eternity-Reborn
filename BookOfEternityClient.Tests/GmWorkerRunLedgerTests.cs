using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRunLedgerTests
{
    [Fact]
    public async Task Initialize_PersistsBoundRootStateAndStableLocks()
    {
        using var fixture = new LedgerFixture();
        await using var owner = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target);
        Assert.NotNull(owner);
        Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.InitializeAsync());
        var state = Path.Combine(fixture.Target.DirectoryPath, "state.json");
        Assert.True(File.Exists(state), "Initialization must persist an actual bounded ledger before acknowledging.");
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(state));
        Assert.Equal(1, json.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal(fixture.Target.RootPath, json.RootElement.GetProperty("RootKey").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("Sequence").GetInt64());
        Assert.Equal(0, json.RootElement.GetProperty("EpochHighWater").GetInt64());
        Assert.Empty(json.RootElement.GetProperty("Entries").EnumerateArray());
        Assert.Empty(json.RootElement.GetProperty("Retired").EnumerateArray());
        Assert.True(File.Exists(Path.Combine(fixture.Target.DirectoryPath, "owner.lock")));
        Assert.True(File.Exists(Path.Combine(fixture.Target.DirectoryPath, "journal.lock")));
        Assert.True(Directory.Exists(Path.Combine(fixture.Target.DirectoryPath, "retired")));
        var observed = await GmWorkerRunLedger.ObserveAsync(fixture.Target);
        Assert.Equal(WorkerRunObservationKind.Quiescent, observed.Kind);
        Assert.Equal(1, observed.Sequence);
        fixture.AssertSentinel();
    }

    [Fact]
    public async Task ColdMissing_IsReadOnlyAndDoesNotCreateNamespace()
    {
        using var fixture = new LedgerFixture();
        Assert.Equal(WorkerRunObservationKind.Missing, (await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Kind);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Target.RootPath));
        fixture.AssertSentinel();
    }

    [Fact]
    public async Task ExistingEmptyNamespace_IsBlockedWithoutBootstrapRepair()
    {
        using var fixture = new LedgerFixture();
        Directory.CreateDirectory(fixture.Target.DirectoryPath);
        Assert.Equal(WorkerRunObservationKind.Blocked, (await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Kind);
        await using var owner = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target);
        Assert.Null(owner);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Target.DirectoryPath));
        fixture.AssertSentinel();
    }

    private sealed class LedgerFixture : IDisposable
    {
        private readonly string _container = Path.Combine(Path.GetTempPath(), "boe-ledger-" + Guid.NewGuid().ToString("N"));
        internal WorkerLedgerTarget Target { get; }
        internal LedgerFixture()
        {
            Assert.True(OperatingSystem.IsLinux(), "Select this native persistence category only on Linux.");
            Target = new(Path.Combine(_container, "root"));
            Directory.CreateDirectory(Target.RootPath);
            File.WriteAllText(Path.Combine(_container, "outside.txt"), "outside-sentinel");
        }
        internal void AssertSentinel() => Assert.Equal("outside-sentinel", File.ReadAllText(Path.Combine(_container, "outside.txt")));
        public void Dispose() => Directory.Delete(_container, recursive: true);
    }
}
