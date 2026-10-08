using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

// Native admission bodies with a labelled fixture observation, not a WMI probe,
// live terminal, or native worker-ledger qualification.
public sealed class GmMainOwnerWindowsAdmissionTests : IDisposable
{
    private readonly string _root;
    public GmMainOwnerWindowsAdmissionTests()
    {
        Assert.True(OperatingSystem.IsWindows(), "Native Windows bodies are required; platform returns are not PASS.");
        _root = Path.Combine(Path.GetTempPath(), "main-owner-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }
    private string Root(string spelling) => spelling switch {
        "extended" => PhysicalFileAuthority.ToWindowsExtendedPath(_root),
        "drive-case" => (char.IsUpper(_root[0]) ? char.ToLowerInvariant(_root[0]) : char.ToUpperInvariant(_root[0])) + _root[1..],
        _ => _root
    };
    private static WindowsStartupObservation Observation() => WindowsStartupObservation.Parse(
        Encoding.ASCII.GetBytes("wmi-lastboot-v1:2026-10-08T10:00:00.0000000Z"));
    private string RecordPath => Path.Combine(_root, ".boe_runtime", "gm-runs", "main.json");
    private byte[] Seed(GmSessionRunDisposition state)
    {
        var id = new GmSessionRunIdentity(_root, Guid.NewGuid().ToString("N"), Guid.Empty.ToString("N"), 1,
            GmSessionRunBackend.WindowsJob, Guid.NewGuid().ToString("N"), "wmi-lastboot-v1:2025-01-01T00:00:00.0000000Z");
        var record = new GmSessionRunRecord(1, id, state, state == GmSessionRunDisposition.Stopped
            ? new(id, GmSessionRunStopKind.OwnedScopeEmpty, id.BootId) : null);
        var bytes = GmSessionRunRecordCodec.Encode(record);
        Directory.CreateDirectory(Path.GetDirectoryName(RecordPath)!);
        File.WriteAllBytes(RecordPath, bytes);
        return bytes;
    }
    public static IEnumerable<object[]> QuiescentCases()
    {
        foreach (var spelling in new[] { "ordinary", "extended", "drive-case" })
        foreach (var stopped in new[] { false, true }) yield return [spelling, stopped];
    }
    [Theory] [MemberData(nameof(QuiescentCases))]
    public async Task OriginalAdmissionAndCancelledPreparationCreateNoWorkerInventory(string spelling, bool stopped)
    {
        var prior = stopped ? Seed(GmSessionRunDisposition.Stopped) : null;
        var files = new FileSystemManager(Root(spelling), NullLogger<FileSystemManager>.Instance);
        var owner = await GmSessionRunCoordinator.OpenWindowsProductionAsync(files, Observation());
        Assert.True(owner.RetainsAuthority);
        Assert.False(Directory.Exists(Path.Combine(_root, ".boe_runtime", "worker-runs")));
        Assert.Null(files.CanonicalRootAuthorityIdentity.WorkerContext);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var prepareCalls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.LaunchWindowsPreparedAsync(_ => {
            prepareCalls++;
            throw new InvalidOperationException("Cancelled preparation must not create a terminal.");
        }, cancelled.Token));
        Assert.Equal(0, prepareCalls);
        Assert.False(owner.RetainsAuthority);
        Assert.Null(files.CanonicalRootAuthorityIdentity.MainCoordinator);
        Assert.False(File.Exists(files.SessionGenerationPath));
        Assert.False(Directory.Exists(Path.Combine(_root, ".boe_runtime", "worker-runs")));
        if (prior == null) Assert.False(File.Exists(RecordPath));
        else Assert.Equal(prior, File.ReadAllBytes(RecordPath));
        using var nextGuard = await GmMainOwnerGuard.AcquireAsync(_root, attempts: 1);
    }
    public static IEnumerable<object[]> InventoryCases()
    {
        foreach (var spelling in new[] { "ordinary", "extended", "drive-case" })
        foreach (var kind in new[] { "empty", "unknown", "file" }) yield return [spelling, kind];
    }
    [Theory] [MemberData(nameof(InventoryCases))]
    public async Task ExistingInventoryRefusesWithoutAdoptionOrPreparation(string spelling, string kind)
    {
        var files = new FileSystemManager(Root(spelling), NullLogger<FileSystemManager>.Instance);
        var inventory = Path.Combine(_root, ".boe_runtime", "worker-runs");
        Directory.CreateDirectory(Path.GetDirectoryName(inventory)!);
        string? evidence = null;
        var bytes = new byte[] { 9, 4, 2, 0, 255 };
        if (kind == "file") { evidence = inventory; File.WriteAllBytes(evidence, bytes); }
        else {
            Directory.CreateDirectory(inventory);
            if (kind == "unknown") { evidence = Path.Combine(inventory, "unknown.bin"); File.WriteAllBytes(evidence, bytes); }
        }
        await Assert.ThrowsAsync<IOException>(() => GmSessionRunCoordinator.OpenWindowsProductionAsync(files, Observation()));
        Assert.Null(files.CanonicalRootAuthorityIdentity.WorkerContext);
        Assert.Null(files.CanonicalRootAuthorityIdentity.MainCoordinator);
        Assert.False(File.Exists(files.SessionGenerationPath));
        Assert.False(File.Exists(RecordPath));
        if (evidence != null) Assert.Equal(bytes, File.ReadAllBytes(evidence));
        else Assert.Empty(Directory.GetFileSystemEntries(inventory));
        using var guard = await GmMainOwnerGuard.AcquireAsync(_root, attempts: 1);
    }
    public static IEnumerable<object[]> ColdCases()
    {
        foreach (var spelling in new[] { "ordinary", "extended", "drive-case" })
        foreach (var state in new[] { GmSessionRunDisposition.Prepared, GmSessionRunDisposition.Running,
                     GmSessionRunDisposition.Stopping, GmSessionRunDisposition.Uncertain }) yield return [spelling, (int)state];
    }
    [Theory] [MemberData(nameof(ColdCases))]
    public async Task ChangedStartupObservationCannotAdmitColdNonterminal(string spelling, int state)
    {
        var before = Seed((GmSessionRunDisposition)state);
        var files = new FileSystemManager(Root(spelling), NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<IOException>(() => GmSessionRunCoordinator.OpenWindowsProductionAsync(files, Observation()));
        Assert.Equal(before, File.ReadAllBytes(RecordPath));
        Assert.False(File.Exists(files.SessionGenerationPath));
        Assert.False(Directory.Exists(Path.Combine(_root, ".boe_runtime", "worker-runs")));
        Assert.Null(files.CanonicalRootAuthorityIdentity.MainCoordinator);
        using var guard = await GmMainOwnerGuard.AcquireAsync(_root, attempts: 1);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
