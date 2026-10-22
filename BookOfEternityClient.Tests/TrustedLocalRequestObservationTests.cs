using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises request-local metadata work through real main-admission consumers on tiny owned Windows roots.
/// </summary>
public sealed class TrustedLocalRequestObservationTests : IDisposable
{
    private readonly string _sandbox;
    private readonly string _root;
    private readonly FileSystemManager _files;
    private int _attributeCalls;

    /// <summary>
    /// Creates a fresh five-component root and a passive counter without constructing a game or worker.
    /// </summary>
    public TrustedLocalRequestObservationTests()
    {
        Assert.True(OperatingSystem.IsWindows(), "This category requires actual Windows guard bodies.");
        _sandbox = Path.Combine("C:/Temp", "boe-request-observation-" + Guid.NewGuid().ToString("N"));
        _root = Path.GetFullPath(Path.Combine(_sandbox, "owned", "root"));
        Directory.CreateDirectory(_root);
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                OperationTimingObserver = (name, boundary, _) =>
                {
                    if (name == "TrustedScope.Probe.GetAttributes" && boundary == "begin") _attributeCalls++;
                }
            });
    }

    /// <summary>
    /// Requires one root-plus-lock traversal rather than repeated root observations on each genuine guard check.
    /// </summary>
    [Fact]
    public async Task GuardValidationObservesEachPathOncePerRequest()
    {
        using var guard = await GmMainOwnerGuard.AcquireAsync(_root, attempts: 1);
        using var context = _files.EnterOperationTimingContext();
        guard.Validate();
        Assert.Equal(8, _attributeCalls);
        _attributeCalls = 0;
        guard.Validate();
        Assert.Equal(8, _attributeCalls);
    }

    /// <summary>
    /// Requires a fresh single traversal for absent worker inventory under the actual worker gate.
    /// </summary>
    [Fact]
    public async Task AbsentWorkerInventoryObservesEachPathOncePerRequest()
    {
        using var guard = await GmMainOwnerGuard.AcquireAsync(_root, attempts: 1);
        using var context = _files.EnterOperationTimingContext();
        _files.RequireAbsentMainWorkerInventory();
        Assert.Equal(7, _attributeCalls);
        _attributeCalls = 0;
        _files.RequireAbsentMainWorkerInventory();
        Assert.Equal(7, _attributeCalls);
    }

    /// <summary>
    /// Requires a fresh single traversal for the missing run namespace without fabricating a run record.
    /// </summary>
    [Fact]
    public async Task AbsentRunMetadataObservesEachPathOncePerRequest()
    {
        using var guard = await GmMainOwnerGuard.AcquireAsync(_root, attempts: 1);
        using var context = _files.EnterOperationTimingContext();
        Assert.Null(GmSessionRunPersistence.Read(_root));
        Assert.Equal(7, _attributeCalls);
        _attributeCalls = 0;
        Assert.Null(GmSessionRunPersistence.Read(_root));
        Assert.Equal(7, _attributeCalls);
    }

    /// <summary>
    /// Removes only this fixture's fresh GUID container after all acquired guards have been disposed.
    /// </summary>
    public void Dispose()
    {
        var parent = Path.GetFullPath("C:/Temp").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var owned = Path.GetFullPath(_sandbox);
        if (!owned.StartsWith(parent, StringComparison.Ordinal) ||
            !Path.GetFileName(owned).StartsWith("boe-request-observation-", StringComparison.Ordinal))
            throw new InvalidOperationException("The fixture cleanup target is outside its owned namespace.");
        Directory.Delete(owned, recursive: true);
    }
}
