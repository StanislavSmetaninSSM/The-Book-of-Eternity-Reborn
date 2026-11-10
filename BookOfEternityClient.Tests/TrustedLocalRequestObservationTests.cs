using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EntryKind = BookOfEternityClient.Core.TrustedLocalFileScope.EntryKind;

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
    /// Requires the saved observations to end with the request, including on the returned ordinary scope.
    /// </summary>
    [Fact]
    public void ReturnedScopeAndNextRequestObserveCurrentFileAndDirectoryKinds()
    {
        var path = Path.Combine(_root, "changing");
        var scope = TrustedLocalFileScope.CreateAndObserveNamespace(_root, path, out var first);
        Assert.Equal(TrustedLocalNamespaceKind.Missing, first.Kind);
        File.WriteAllText(path, "owned bytes");
        Assert.Equal(TrustedLocalNamespaceKind.File, scope.ObserveNamespace(path).Kind);
        Assert.Equal(path, scope.ValidateFile(path, allowMissing: false));
        File.Delete(path);
        Directory.CreateDirectory(path);
        Assert.Throws<InvalidDataException>(() => scope.ValidateFile(path, allowMissing: false));
        TrustedLocalFileScope.CreateAndObserveNamespace(_root, path, out var second);
        Assert.Equal(TrustedLocalNamespaceKind.Directory, second.Kind);
    }

    /// <summary>
    /// Keeps root-file refusal distinct from a below-root file blocker and required-file ancestor refusal.
    /// </summary>
    [Fact]
    public void RootAndNamespaceFileBlockersRetainTheirDifferentRoles()
    {
        var blocker = Path.Combine(_root, "blocker");
        var target = Path.Combine(blocker, "child");
        EntryKind ObserveBlocker(string path) => path == blocker ? EntryKind.RegularFile : EntryKind.Directory;
        TrustedLocalFileScope.CreateAndObserveNamespace(_root, target, out var blocked, ObserveBlocker);
        Assert.Equal(TrustedLocalNamespaceKind.Missing, blocked.Kind);
        Assert.Equal(blocker, blocked.BlockingFileAncestor);
        Assert.Throws<InvalidDataException>(() =>
            TrustedLocalFileScope.ValidateRootAndRequiredFile(_root, target, ObserveBlocker));
        Assert.Throws<InvalidDataException>(() => TrustedLocalFileScope.CreateAndObserveNamespace(
            _root, target, out _, path => path == _root ? EntryKind.RegularFile : EntryKind.Directory));
    }

    /// <summary>
    /// Requires missing parents to refuse construction while a missing root remains an ordinary absent namespace.
    /// </summary>
    [Fact]
    public void RequiredParentAndOptionalRootAbsenceRemainDifferent()
    {
        var target = Path.Combine(_root, "child");
        var parent = Path.GetDirectoryName(_root)!;
        Assert.Throws<InvalidDataException>(() => TrustedLocalFileScope.CreateAndObserveNamespace(
            _root, target, out _, path => path == parent ? EntryKind.Missing : EntryKind.Directory));
        EntryKind ObserveMissingRoot(string path) => path == _root || path == target
            ? EntryKind.Missing : EntryKind.Directory;
        TrustedLocalFileScope.CreateAndObserveNamespace(_root, target, out var absent, ObserveMissingRoot);
        Assert.Equal(TrustedLocalNamespaceKind.Missing, absent.Kind);
        Assert.Null(absent.BlockingFileAncestor);
        var failure = Assert.Throws<FileNotFoundException>(() =>
            TrustedLocalFileScope.ValidateRootAndRequiredFile(_root, target, ObserveMissingRoot));
        Assert.Equal(target, failure.FileName);
    }

    /// <summary>
    /// Refuses every linked, special or directory required-file leaf while retaining optional namespace kinds.
    /// </summary>
    /// <param name="entryKind">
    /// The non-file kind returned for the final requested name.
    /// </param>
    [Theory]
    [InlineData((int)EntryKind.Link)]
    [InlineData((int)EntryKind.Other)]
    [InlineData((int)EntryKind.Directory)]
    public void RequiredLeafRetainsKindRefusals(int entryKind)
    {
        var kind = (EntryKind)entryKind;
        var target = Path.Combine(_root, "leaf");
        EntryKind Observe(string path) => path == target ? kind : EntryKind.Directory;
        Assert.Throws<InvalidDataException>(() =>
            TrustedLocalFileScope.ValidateRootAndRequiredFile(_root, target, Observe));
        if (kind == EntryKind.Directory)
        {
            TrustedLocalFileScope.CreateAndObserveNamespace(_root, target, out var directory, Observe);
            Assert.Equal(TrustedLocalNamespaceKind.Directory, directory.Kind);
        }
        else Assert.Throws<InvalidDataException>(() =>
            TrustedLocalFileScope.CreateAndObserveNamespace(_root, target, out _, Observe));
    }

    /// <summary>
    /// Rejects path-grant and ambiguous spelling errors before any target kind can be admitted.
    /// </summary>
    /// <param name="spelling">
    /// The independently selected invalid target shape.
    /// </param>
    [Theory]
    [InlineData("outside")]
    [InlineData("traversal")]
    [InlineData("ambiguous")]
    [InlineData("root-as-file")]
    public void RequiredFileRetainsLexicalAndGrantRefusals(string spelling)
    {
        var target = spelling switch
        {
            "outside" => Path.Combine(_sandbox, "outside"),
            "traversal" => Path.Combine(_root, "..", "outside"),
            "ambiguous" => Path.Combine(_root, "leaf."),
            _ => _root
        };
        Assert.Throws<InvalidDataException>(() =>
            TrustedLocalFileScope.ValidateRootAndRequiredFile(_root, target, _ => EntryKind.Directory));
    }

    /// <summary>
    /// Propagates original access and I/O failures and starts a new observation set on the next request.
    /// </summary>
    /// <param name="accessDenied">
    /// Whether the injected leaf failure is access denial rather than an ordinary I/O failure.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObservationFailuresPropagateAndDoNotSurviveTheRequest(bool accessDenied)
    {
        var target = Path.Combine(_root, "leaf");
        Exception failure = accessDenied ? new UnauthorizedAccessException("owned access failure")
            : new IOException("owned sharing or I/O failure");
        EntryKind Fail(string path) => path == target ? throw failure : EntryKind.Directory;
        Assert.Same(failure, Record.Exception(() =>
            TrustedLocalFileScope.ValidateRootAndRequiredFile(_root, target, Fail)));
        Assert.Same(failure, Record.Exception(() =>
            TrustedLocalFileScope.CreateAndObserveNamespace(_root, target, out _, Fail)));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        EntryKind Observe(string path)
        {
            Assert.True(seen.Add(path), "The same request reread a completed path.");
            return path == target ? EntryKind.RegularFile : EntryKind.Directory;
        }
        TrustedLocalFileScope.ValidateRootAndRequiredFile(_root, target, Observe);
        Assert.Equal(6, seen.Count);
        Assert.Throws<InvalidDataException>(() => TrustedLocalFileScope.ValidateRootAndRequiredFile(
            _root, target, path => path == _root ? EntryKind.Link : EntryKind.Directory));
    }

    /// <summary>
    /// Requires the work reduction to follow root depth without retaining observations between repeated calls.
    /// </summary>
    /// <param name="additionalDepth">
    /// The number of ordinary directories added below the fixed five-component root.
    /// </param>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void NamespaceRequestWorkScalesWithDepthAndRemainsFresh(int additionalDepth)
    {
        var root = _root;
        for (var index = 0; index < additionalDepth; index++) root = Path.Combine(root, "child" + index);
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "missing");
        using var context = _files.EnterOperationTimingContext();
        for (var request = 0; request < 2; request++)
        {
            _attributeCalls = 0;
            TrustedLocalFileScope.CreateAndObserveNamespace(root, target, out var observed);
            Assert.Equal(TrustedLocalNamespaceKind.Missing, observed.Kind);
            Assert.Equal(6 + additionalDepth, _attributeCalls);
        }
    }

    /// <summary>
    /// Keeps real metadata admission intact when a passive timing observer throws at every boundary.
    /// </summary>
    [Fact]
    public void ThrowingObserverDoesNotAlterNamespaceResultOrFreshWork()
    {
        var calls = 0;
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                OperationTimingObserver = (name, boundary, _) =>
                {
                    if (name == "TrustedScope.Probe.GetAttributes" && boundary == "begin") calls++;
                    throw new IOException("passive observer failure");
                }
            });
        using var context = files.EnterOperationTimingContext();
        TrustedLocalFileScope.CreateAndObserveNamespace(_root, Path.Combine(_root, "missing"), out var observed);
        Assert.Equal(TrustedLocalNamespaceKind.Missing, observed.Kind);
        Assert.Equal(6, calls);
    }

    /// <summary>
    /// Requires a nested read-only request to observe its own paths even after the outer root has been checked.
    /// </summary>
    [Fact]
    public void ReentrantReadUsesItsOwnObservations()
    {
        var target = Path.Combine(_root, "missing");
        var nested = false;
        var calls = 0;
        TrustedLocalNamespaceObservation? nestedObservation = null;
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                OperationTimingObserver = (name, boundary, _) =>
                {
                    if (name == "TrustedScope.Probe.GetAttributes" && boundary == "begin") calls++;
                    if (name != "TrustedScope.ObserveNamespace" || boundary != "begin" || nested) return;
                    nested = true;
                    TrustedLocalFileScope.CreateAndObserveNamespace(_root, target, out var result);
                    nestedObservation = result;
                }
            });
        using var context = files.EnterOperationTimingContext();
        TrustedLocalFileScope.CreateAndObserveNamespace(_root, target, out var outer);
        Assert.Equal(TrustedLocalNamespaceKind.Missing, outer.Kind);
        Assert.Equal(TrustedLocalNamespaceKind.Missing, nestedObservation?.Kind);
        Assert.Equal(12, calls);
    }

    /// <summary>
    /// Preserves the worker phase timers outside their manager's temporary inner diagnostic context.
    /// </summary>
    [Fact]
    public async Task WorkerPhasesRestoreAmbientContextBeforeObserverCallbacks()
    {
        using var guard = await GmMainOwnerGuard.AcquireAsync(_root, attempts: 1);
        var required = Path.Combine(_root, "required.json");
        File.WriteAllText(required, "owned");
        var ownCalls = 0;
        var phaseEnds = new List<string>();
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                OperationTimingObserver = (name, boundary, _) =>
                {
                    if (name == "TrustedScope.Probe.GetAttributes" && boundary == "begin") ownCalls++;
                    if (boundary != "end" || name is not ("MainAdmission.WorkerInventory.ScopeConstruction" or
                        "MainAdmission.WorkerInventory.ObserveNamespace")) return;
                    phaseEnds.Add(name);
                    TrustedLocalFileScope.ValidateRootAndRequiredFile(_root, required);
                }
            });
        using var outerContext = _files.EnterOperationTimingContext();
        files.RequireAbsentMainWorkerInventory();
        Assert.Equal(new[] { "MainAdmission.WorkerInventory.ScopeConstruction",
            "MainAdmission.WorkerInventory.ObserveNamespace" }, phaseEnds);
        Assert.Equal(7, ownCalls);
        Assert.Equal(12, _attributeCalls);
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
