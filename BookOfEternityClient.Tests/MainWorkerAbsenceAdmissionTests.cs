using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

// Exercises the real negative namespace/context predicate on Linux. It does not
// claim that a Linux host has opened a Windows main or a Windows durable ledger.
public sealed class MainWorkerAbsenceAdmissionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "main-worker-absence-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    public MainWorkerAbsenceAdmissionTests()
    {
        Assert.True(OperatingSystem.IsLinux());
        _files = new(_root, NullLogger<FileSystemManager>.Instance);
    }
    private string Inventory => new WorkerLedgerTarget(_root).DirectoryPath;
    private void AssertNoMainPreparation()
    {
        Assert.False(Directory.Exists(Path.Combine(_root, ".boe_runtime", "gm-runs")));
        Assert.False(File.Exists(_files.SessionGenerationPath));
        Assert.Null(_files.CanonicalRootAuthorityIdentity.MainCoordinator);
    }
    [Fact]
    public void VerifiedAbsenceDoesNotInitializeInventoryOrMainMetadata()
    {
        var hadRuntime = Directory.Exists(Path.GetDirectoryName(Inventory));
        _files.RequireAbsentMainWorkerInventory();
        _files.RequireAbsentMainWorkerInventory();
        Assert.Equal(hadRuntime, Directory.Exists(Path.GetDirectoryName(Inventory)));
        Assert.False(Directory.Exists(Inventory));
        Assert.Null(_files.CanonicalRootAuthorityIdentity.WorkerContext);
        AssertNoMainPreparation();
    }
    [Theory]
    [InlineData("directory")] [InlineData("file")] [InlineData("parent-file")] [InlineData("link")]
    public void ExistingOrBlockedNamespaceRetainsEveryEvidenceByte(string kind)
    {
        var runtime = Path.GetDirectoryName(Inventory)!;
        var payload = new byte[] { 19, 27, 255, 0 };
        string? evidence;
        if (kind == "parent-file") { File.WriteAllBytes(runtime, payload); evidence = runtime; }
        else
        {
            Directory.CreateDirectory(runtime);
            if (kind == "file") { File.WriteAllBytes(Inventory, payload); evidence = Inventory; }
            else if (kind == "directory")
            {
                Directory.CreateDirectory(Inventory);
                evidence = Path.Combine(Inventory, "unknown.bin");
                File.WriteAllBytes(evidence, payload);
            }
            else
            {
                var peer = Path.Combine(_root, "owned-peer");
                Directory.CreateDirectory(peer);
                evidence = Path.Combine(peer, "unknown.bin");
                File.WriteAllBytes(evidence, payload);
                Directory.CreateSymbolicLink(Inventory, peer);
            }
        }
        var refusal = Record.Exception(_files.RequireAbsentMainWorkerInventory);
        Assert.True(refusal is IOException or InvalidDataException, refusal?.ToString());
        Assert.Equal(payload, File.ReadAllBytes(evidence));
        Assert.Null(_files.CanonicalRootAuthorityIdentity.WorkerContext);
        AssertNoMainPreparation();
    }
    [Fact]
    public void LaterEmptyInventoryWithdrawsPreviouslyObservedAbsence()
    {
        _files.RequireAbsentMainWorkerInventory();
        Directory.CreateDirectory(Inventory);
        Assert.Throws<IOException>(_files.RequireAbsentMainWorkerInventory);
        Assert.Empty(Directory.GetFileSystemEntries(Inventory));
        AssertNoMainPreparation();
    }
    [Fact]
    public void RetainedOriginalContextCannotBeReinterpretedAsAbsentInventory()
    {
        var context = GmWorkerRootContext.Attach(_files, true, null);
        var held = Inventory + ".fixture-held";
        try
        {
            context.RequireMainQuiescence();
            Directory.Move(Inventory, held); // controlled name loss; restore before settling original context
            Assert.False(Directory.Exists(Inventory));
            Assert.Throws<IOException>(_files.RequireAbsentMainWorkerInventory);
            Assert.Same(context, _files.CanonicalRootAuthorityIdentity.WorkerContext);
            Assert.True(Directory.Exists(held));
            AssertNoMainPreparation();
        }
        finally
        {
            if (Directory.Exists(held)) Directory.Move(held, Inventory);
            context.ReleaseClient();
        }
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
