using System.Net.Sockets;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class TrustedLocalFileScopeTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "boe-scope-" + Guid.NewGuid().ToString("N"));
    private readonly string _root;
    private readonly string _outside;

    public TrustedLocalFileScopeTests()
    {
        _root = Path.Combine(_sandbox, "root");
        _outside = Path.Combine(_sandbox, "outside");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_outside);
    }

    private TrustedLocalFileScope Scope(params string[] exactFiles) => new([_root], exactFiles);

    [Fact]
    public void ValidateFile_AllowsMissingOrRegularFileInsideRoot()
    {
        var path = Path.Combine(_root, "state.json");
        var scope = Scope();
        Assert.Equal(path, scope.ValidateFile(path));
        File.WriteAllBytes(path, [0, 255, 17]);
        Assert.Equal(path, scope.ValidateFile(path, allowMissing: false));
        Assert.Equal(new byte[] { 0, 255, 17 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void ValidateFile_ExactExternalGrantDoesNotGrantSiblingsOrDirectory()
    {
        var granted = Path.Combine(_outside, "allowed.json");
        var scope = Scope(granted);
        Assert.Equal(granted, scope.ValidateFile(granted));
        Assert.Throws<InvalidDataException>(() => scope.ValidateFile(Path.Combine(_outside, "other.json")));
        Assert.Throws<InvalidDataException>(() => scope.EnsureDirectory(_outside));
    }

    [Fact]
    public void ValidateFile_RejectsTraversalAndRelativePaths()
    {
        var scope = Scope();
        Assert.Throws<InvalidDataException>(() => scope.ValidateFile(Path.Combine(_root, "..", "outside", "state.json")));
        Assert.Throws<InvalidDataException>(() => scope.ValidateFile("state.json"));
        Assert.Throws<InvalidDataException>(() => scope.ValidateFile(Path.Combine(_root + "-sibling", "state.json")));
    }

    [Fact]
    public void ValidateFile_RejectsDirectoryLeafAndFileAncestor()
    {
        var scope = Scope();
        Assert.Throws<InvalidDataException>(() => scope.ValidateFile(_root));
        var file = Path.Combine(_root, "regular");
        File.WriteAllText(file, "unchanged");
        Assert.Throws<InvalidDataException>(() => scope.ValidateFile(Path.Combine(file, "state.json")));
        Assert.Equal("unchanged", File.ReadAllText(file));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidateFile_RejectsExistingAndDanglingSymbolicLinks(bool dangling)
    {
        var target = Path.Combine(_outside, "target.json");
        if (!dangling) File.WriteAllText(target, "outside");
        var link = Path.Combine(_root, "link.json");
        File.CreateSymbolicLink(link, target);
        Assert.Throws<InvalidDataException>(() => Scope().ValidateFile(link));
        if (!dangling) Assert.Equal("outside", File.ReadAllText(target));
    }

    [Fact]
    public void ValidateFile_RejectsLinkedAncestor()
    {
        var link = Path.Combine(_root, "linked");
        Directory.CreateSymbolicLink(link, _outside);
        Assert.Throws<InvalidDataException>(() => Scope().ValidateFile(Path.Combine(link, "state.json")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_outside));
    }

    [Fact]
    public void ValidateFile_RevalidatesRootAfterConstruction()
    {
        var scope = Scope();
        Directory.Delete(_root);
        Directory.CreateSymbolicLink(_root, _outside);
        Assert.Throws<InvalidDataException>(() => scope.ValidateFile(Path.Combine(_root, "state.json")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_outside));
    }

    [Fact]
    public void ValidateFile_RejectsNonRegularSocketWithoutOpeningIt()
    {
        var socketPath = Path.Combine(_root, "s.sock");
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(socketPath));
        Assert.Throws<InvalidDataException>(() => Scope().ValidateFile(socketPath));
        Assert.True(socket.IsBound);
    }

    [Fact]
    public void EnsureDirectory_CreatesOnlyInsideGrantedRoot()
    {
        var path = Path.Combine(_root, "a", "b");
        Assert.Equal(path, Scope().EnsureDirectory(path));
        Assert.True(Directory.Exists(path));
        Assert.Throws<InvalidDataException>(() => Scope().EnsureDirectory(Path.Combine(_outside, "new")));
        Assert.False(Directory.Exists(Path.Combine(_outside, "new")));
    }

    [Fact]
    public void Constructor_AllowsNewRootOnlyWithExistingSafeParent()
    {
        var newRoot = Path.Combine(_sandbox, "new-root");
        var scope = new TrustedLocalFileScope([newRoot]);
        Assert.Equal(newRoot, scope.EnsureDirectory(newRoot));
        Assert.True(Directory.Exists(newRoot));
        Assert.Throws<InvalidDataException>(() => new TrustedLocalFileScope([Path.Combine(_sandbox, "missing", "nested-root")]));
        Assert.False(Directory.Exists(Path.Combine(_sandbox, "missing")));
    }

    [Fact]
    public void ValidateFile_UsesHostPathCaseRulesWithoutBroadeningScope()
    {
        var changedCase = Path.Combine(_sandbox, "ROOT", "state.json");
        var scope = Scope();
        if (OperatingSystem.IsWindows())
            Assert.Equal(changedCase, scope.ValidateFile(changedCase));
        else
            Assert.Throws<InvalidDataException>(() => scope.ValidateFile(changedCase));
    }

    [Fact]
    public void ValidateFile_RequiredAbsenceIsNotSilentlyAccepted()
    {
        var path = Path.Combine(_root, "missing.json");
        Assert.Throws<FileNotFoundException>(() => Scope().ValidateFile(path, allowMissing: false));
    }

    [Fact]
    public void DeleteOwnedFile_DeletesOnlyValidatedFileAndAllowsAbsence()
    {
        var file = Path.Combine(_root, "state.json");
        var outside = Path.Combine(_outside, "sentinel.json");
        File.WriteAllText(file, "owned");
        File.WriteAllText(outside, "outside");
        var scope = Scope();
        scope.DeleteOwnedFile(file);
        scope.DeleteOwnedFile(file);
        Assert.False(File.Exists(file));
        Assert.Throws<InvalidDataException>(() => scope.DeleteOwnedFile(outside));
        Assert.Equal("outside", File.ReadAllText(outside));
    }

    [Fact]
    public void DeleteOwnedTree_PreflightsLinksBeforeDeletingAnyEntry()
    {
        var tree = Path.Combine(_root, "transaction");
        Directory.CreateDirectory(tree);
        var owned = Path.Combine(tree, "intent.json");
        var sentinel = Path.Combine(_outside, "sentinel.json");
        File.WriteAllText(owned, "evidence");
        File.WriteAllText(sentinel, "outside");
        Directory.CreateSymbolicLink(Path.Combine(tree, "link"), _outside);
        Assert.Throws<InvalidDataException>(() => Scope().DeleteOwnedTree(tree));
        Assert.Equal("evidence", File.ReadAllText(owned));
        Assert.Equal("outside", File.ReadAllText(sentinel));
    }

    [Fact]
    public void DeleteOwnedTree_DeletesValidatedContentsOnly()
    {
        var tree = Path.Combine(_root, "transaction");
        Directory.CreateDirectory(Path.Combine(tree, "nested"));
        File.WriteAllText(Path.Combine(tree, "nested", "file"), "owned");
        var scope = Scope();
        scope.DeleteOwnedTree(tree);
        scope.DeleteOwnedTree(tree);
        Assert.False(Directory.Exists(tree));
        Assert.True(Directory.Exists(_root));
        Assert.True(Directory.Exists(_outside));
    }

    public void Dispose()
    {
        if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, recursive: true);
    }
}
