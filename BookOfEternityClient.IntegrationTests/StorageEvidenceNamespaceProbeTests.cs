using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises real storage namespace traversal using independent ordinary filesystem fixtures.
/// </summary>
public sealed class StorageEvidenceNamespaceProbeTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Selects the destination for complete native-attempt diagnostics from this test.
    /// </summary>
    /// <param name="output">
    /// The current case's diagnostic receiver.
    /// </param>
    public StorageEvidenceNamespaceProbeTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Classifies the same real leaf while eliminating duplicate successful ancestor opens.
    /// </summary>
    /// <param name="depth">
    /// Number of relative components, including the leaf.
    /// </param>
    /// <param name="leafKind">
    /// Whether the final name is missing, a regular file or an ordinary directory.
    /// </param>
    [Theory]
    [InlineData(1, "missing")]
    [InlineData(1, "file")]
    [InlineData(1, "directory")]
    [InlineData(2, "missing")]
    [InlineData(2, "file")]
    [InlineData(2, "directory")]
    [InlineData(4, "missing")]
    [InlineData(4, "file")]
    [InlineData(4, "directory")]
    public void FreshTraversal_ClassifiesLeafWithOneOpenPerDirectory(int depth, string leafKind)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var path = fixture.PrepareLeaf(depth, leafKind);
        var before = fixture.CaptureFiles();
        var originalAttempts = new List<PhysicalNamespaceOpenAttempt>();
        var storageAttempts = new List<PhysicalNamespaceOpenAttempt>();
        try
        {
            var expected = leafKind switch
            {
                "missing" => PhysicalFileAuthority.NamespaceEntryKind.Missing,
                "file" => PhysicalFileAuthority.NamespaceEntryKind.RegularFile,
                _ => PhysicalFileAuthority.NamespaceEntryKind.Directory
            };
            Assert.Equal(expected, Probe(fixture.Source, path, false, originalAttempts));
            Assert.Equal(expected, Probe(fixture.Source, path, true, storageAttempts));
            Assert.Equal(before, fixture.CaptureFiles());
            Assert.Equal(2 * depth + 2, originalAttempts.Count);
            Assert.Equal(depth + 2, storageAttempts.Count);
            var combined = storageAttempts.Where(item => item.Kind == PhysicalNamespaceOpenKind.CombinedDirectory).ToArray();
            Assert.Equal(depth, combined.Length);
            Assert.All(combined, item =>
            {
                Assert.Equal(0x00100081u, item.DesiredAccess);
                Assert.Equal(3u, item.ShareMode);
                Assert.Equal(0x02200000u, item.Flags);
            });
        }
        finally { WriteAttempts(originalAttempts, storageAttempts); }
    }

    /// <summary>
    /// Retains original absence and refusal distinctions without changing any fixture bytes.
    /// </summary>
    /// <param name="situation">
    /// A missing root or parent, a file at either boundary, or an invalid target relationship.
    /// </param>
    [Theory]
    [InlineData("missing-root")]
    [InlineData("missing-parent")]
    [InlineData("file-root")]
    [InlineData("file-parent")]
    [InlineData("outside")]
    [InlineData("same-root")]
    public void FreshTraversal_PreservesOriginalMissingAndBadParentOutcomes(string situation)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var target = Path.Combine(fixture.Source, "parent", "leaf");
        if (situation.EndsWith("root", StringComparison.Ordinal) && situation != "same-root")
        {
            Directory.Delete(fixture.Source);
            if (situation == "file-root") File.WriteAllText(fixture.Source, "root-file");
        }
        else if (situation == "file-parent") File.WriteAllText(Path.GetDirectoryName(target)!, "parent-file");
        else if (situation == "outside") target = Path.Combine(fixture.Root, "outside-name");
        else if (situation == "same-root") target = fixture.Source;
        var before = fixture.CaptureFiles();
        CompareOutcome(fixture.Source, target, situation.StartsWith("missing", StringComparison.Ordinal)
            ? PhysicalFileAuthority.NamespaceEntryKind.Missing : null);
        Assert.Equal(before, fixture.CaptureFiles());
    }

    /// <summary>
    /// Retains ordinary Unicode and extended Windows spelling behavior.
    /// </summary>
    /// <param name="extended">
    /// Whether both root and leaf use the extended drive path spelling.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshTraversal_PreservesUnicodeAndExtendedSpelling(bool extended)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var leaf = fixture.PrepareLeaf(2, "file");
        var root = fixture.Source;
        if (extended) { root = @"\\?\" + root; leaf = @"\\?\" + leaf; }
        var attempts = new List<PhysicalNamespaceOpenAttempt>();
        Assert.Equal(PhysicalFileAuthority.NamespaceEntryKind.RegularFile, Probe(root, leaf, true, attempts));
        WriteAttempts([], attempts);
    }

    /// <summary>
    /// Preserves original refusal classes when combined access fails before metadata classification.
    /// </summary>
    /// <param name="denial">
    /// A regular file denying data access, or a directory denying listing access.
    /// </param>
    [Theory]
    [InlineData("file-data")]
    [InlineData("directory-list")]
    public void FailedCombinedOpen_UsesOriginalAccessAndTypeRefusal(string denial)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var parent = Path.Combine(fixture.Source, "restricted");
        var file = denial == "file-data";
        if (file) File.WriteAllText(parent, "retained-data");
        else Directory.CreateDirectory(parent);
        var target = Path.Combine(parent, "leaf");
        var rights = FileSystemRights.ReadData;
        var originalAttempts = new List<PhysicalNamespaceOpenAttempt>();
        var attempts = new List<PhysicalNamespaceOpenAttempt>();
        try
        {
            fixture.WithDeniedAccess(parent, file, rights, () =>
            {
                var original = Record.Exception(() => Probe(fixture.Source, target, false, originalAttempts));
                var storage = Record.Exception(() => Probe(fixture.Source, target, true, attempts));
                Assert.NotNull(original);
                Assert.NotNull(storage);
                Assert.Equal(original.GetType(), storage.GetType());
                if (file) Assert.IsType<InvalidDataException>(storage);
                else Assert.IsType<UnauthorizedAccessException>(storage);
                Assert.Contains(attempts, item => item.Path == parent && item.Kind == PhysicalNamespaceOpenKind.NamespaceEntry);
            });
            if (file) Assert.Equal("retained-data", File.ReadAllText(parent));
        }
        finally { WriteAttempts(originalAttempts, attempts); }
    }

    /// <summary>
    /// Exercises actual attribute denial after parent admission, including NTFS's parent-list permission rule.
    /// </summary>
    [Fact]
    public void AttributeDenialAfterParentAdmission_RetainsOriginalRefusalAndReleasesHandles()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var leaf = fixture.PrepareLeaf(1, "directory");
        var originalAttempts = new List<PhysicalNamespaceOpenAttempt>();
        var storageAttempts = new List<PhysicalNamespaceOpenAttempt>();
        try
        {
            foreach (var storage in new[] { false, true })
            {
                using var childDenial = fixture.DenyAccess(leaf, false, FileSystemRights.ReadAttributes);
                IDisposable? parentDenial = null;
                try
                {
                    var hooks = new PhysicalNamespaceProbeHooks
                    {
                        ObserveNativeOpen = (storage ? storageAttempts : originalAttempts).Add,
                        // Existing parent handles retain their granted access. Denying
                        // later listing prevents NTFS from granting child attributes
                        // through the parent's listing permission.
                        BeforeLeafProbe = _ => parentDenial = fixture.DenyAccess(fixture.Source, false, FileSystemRights.ListDirectory)
                    };
                    var error = Record.Exception(() =>
                    {
                        if (storage) PhysicalFileAuthority.ProbeStorageEvidenceNamespaceEntryFromRoot(fixture.Source, leaf, "Attribute fixture", hooks);
                        else PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(fixture.Source, leaf, "Attribute fixture", hooks);
                    });
                    Assert.IsType<UnauthorizedAccessException>(error);
                }
                finally { parentDenial?.Dispose(); }
            }
            Assert.Empty(Directory.EnumerateFileSystemEntries(leaf));
        }
        finally { WriteAttempts(originalAttempts, storageAttempts); }
    }

    /// <summary>
    /// Does not replace a bad-parent refusal with a data-sharing failure from combined access.
    /// </summary>
    [Fact]
    public void FailedCombinedOpen_PreservesBadParentUnderExclusiveFileSharing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var parent = Path.Combine(fixture.Source, "exclusive-file");
        File.WriteAllText(parent, "exclusive");
        using (new FileStream(parent, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            CompareOutcome(fixture.Source, Path.Combine(parent, "leaf"), requireInvalidData: false);
        Assert.Equal("exclusive", File.ReadAllText(parent));
    }

    /// <summary>
    /// Retains junction refusal for ancestors and final-leaf reparse classification.
    /// </summary>
    /// <param name="boundary">
    /// Root, intermediate parent or final leaf replaced by a junction.
    /// </param>
    [Theory]
    [InlineData("root")]
    [InlineData("parent")]
    [InlineData("leaf")]
    public void FreshTraversal_DoesNotFollowJunctions(string boundary)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var outside = Path.Combine(fixture.Root, "independent-target");
        Directory.CreateDirectory(outside);
        var retained = Path.Combine(outside, "retained.txt");
        File.WriteAllText(retained, "outside-bytes");
        var parent = Path.Combine(fixture.Source, "parent");
        var target = Path.Combine(parent, "leaf");
        string link;
        if (boundary == "root") { Directory.Delete(fixture.Source); link = fixture.Source; }
        else if (boundary == "parent") link = parent;
        else { Directory.CreateDirectory(parent); link = target; }
        fixture.CreateJunction(link, outside);
        CompareOutcome(fixture.Source, target, boundary == "leaf" ? PhysicalFileAuthority.NamespaceEntryKind.ReparsePoint : null);
        Assert.Equal("outside-bytes", File.ReadAllText(retained));
        Assert.Single(Directory.EnumerateFileSystemEntries(outside));
    }

    /// <summary>
    /// Keeps the original single-link refusal for file leaves and file ancestors.
    /// </summary>
    /// <param name="ancestor">
    /// Whether the multiply linked file blocks a descendant rather than being the requested leaf.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshTraversal_RetainsHardLinkRefusal(bool ancestor)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var original = Path.Combine(fixture.Root, "outside-original");
        File.WriteAllText(original, "immutable-linked-bytes");
        var linked = Path.Combine(fixture.Source, "linked");
        WindowsHardLinkTestHelper.Create(linked, original);
        var target = ancestor ? Path.Combine(linked, "leaf") : linked;
        CompareOutcome(fixture.Source, target);
        Assert.Equal("immutable-linked-bytes", File.ReadAllText(original));
        Assert.Equal("immutable-linked-bytes", File.ReadAllText(linked));
    }

    /// <summary>
    /// Isolates passive observation failures while retaining every attempted native open.
    /// </summary>
    [Fact]
    public void NativeObserverFailure_DoesNotReplaceActualOutcome()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var leaf = fixture.PrepareLeaf(2, "file");
        var attempts = new List<PhysicalNamespaceOpenAttempt>();
        var result = PhysicalFileAuthority.ProbeStorageEvidenceNamespaceEntryFromRoot(fixture.Source, leaf, "Observer fixture",
            new PhysicalNamespaceProbeHooks
            {
                ObserveNativeOpen = item => { attempts.Add(item); throw new IOException("passive observer failure"); }
            });
        Assert.Equal(PhysicalFileAuthority.NamespaceEntryKind.RegularFile, result);
        Assert.NotEmpty(attempts);
        Assert.Equal("leaf-bytes", File.ReadAllText(leaf));
        WriteAttempts([], attempts);
    }

    /// <summary>
    /// Propagates the original post-open failure and releases all authorities without retrying validation.
    /// </summary>
    [Fact]
    public void SuccessfulOpenValidationFailure_PropagatesAndReleasesHandles()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var target = fixture.PrepareLeaf(2, "file");
        var attempts = new List<PhysicalNamespaceOpenAttempt>();
        var failure = new InvalidDataException("controlled post-open authority failure");
        var validations = 0;
        var actual = Record.Exception(() => PhysicalFileAuthority.ProbeStorageEvidenceNamespaceEntryFromRoot(
            fixture.Source, target, "Validation fixture", new PhysicalNamespaceProbeHooks
            {
                ObserveNativeOpen = attempts.Add,
                BeforeDirectoryHandleValidation = path =>
                {
                    if (!path.Equals(fixture.Source, StringComparison.Ordinal)) return;
                    validations++;
                    throw failure;
                }
            }));
        Assert.Same(failure, actual);
        Assert.Equal(1, validations);
        Assert.DoesNotContain(attempts, item => item.Path == target);
        var moved = fixture.Source + ".released";
        Directory.Move(fixture.Source, moved);
        Directory.Move(moved, fixture.Source);
        WriteAttempts([], attempts);
    }

    /// <summary>
    /// Retains every ancestor until leaf classification and releases them after the traversal returns.
    /// </summary>
    [Fact]
    public void RetainedParent_BlocksAncestorRenameUntilLeafProbeCompletes()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var target = fixture.PrepareLeaf(4, "file");
        var blocked = new List<bool>();
        var attempts = new List<PhysicalNamespaceOpenAttempt>();
        var result = PhysicalFileAuthority.ProbeStorageEvidenceNamespaceEntryFromRoot(fixture.Source, target, "Retention fixture",
            new PhysicalNamespaceProbeHooks
            {
                ObserveNativeOpen = attempts.Add,
                BeforeLeafProbe = _ =>
                {
                    var current = Path.GetDirectoryName(target)!;
                    while (true)
                    {
                        var displaced = current + ".attempt";
                        var error = Record.Exception(() => Directory.Move(current, displaced));
                        blocked.Add(error is IOException or UnauthorizedAccessException);
                        if (error == null) Directory.Move(displaced, current);
                        if (current == fixture.Source) break;
                        current = Path.GetDirectoryName(current)!;
                    }
                }
            });
        Assert.Equal(PhysicalFileAuthority.NamespaceEntryKind.RegularFile, result);
        Assert.Equal(4, blocked.Count);
        Assert.All(blocked, item => Assert.True(item));
        var moved = fixture.Source + ".released";
        Directory.Move(fixture.Source, moved);
        Directory.Move(moved, fixture.Source);
        WriteAttempts([], attempts);
    }

    /// <summary>
    /// Uses a fresh final observation after controlled leaf replacement, without following a reparse target.
    /// </summary>
    [Fact]
    public void BeforeLeafBoundary_ObservesReplacementByDirectory()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var target = fixture.PrepareLeaf(2, "file");
        var result = PhysicalFileAuthority.ProbeStorageEvidenceNamespaceEntryFromRoot(fixture.Source, target, "Replacement fixture",
            new PhysicalNamespaceProbeHooks
            {
                BeforeLeafProbe = path => { File.Delete(path); Directory.CreateDirectory(path); }
            });
        Assert.Equal(PhysicalFileAuthority.NamespaceEntryKind.Directory, result);
    }

    /// <summary>
    /// Rejects an intermediate junction inserted after root opening and before child admission.
    /// </summary>
    [Fact]
    public void AfterRootOpen_RejectsNewIntermediateJunctionWithoutFollowingTarget()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var parent = Path.Combine(fixture.Source, "pending-parent");
        Directory.CreateDirectory(parent);
        var outside = Path.Combine(fixture.Root, "independent-target");
        Directory.CreateDirectory(outside);
        var retained = Path.Combine(outside, "retained.txt");
        File.WriteAllText(retained, "unchanged-outside");
        var attempts = new List<PhysicalNamespaceOpenAttempt>();
        var injected = 0;
        try
        {
            Assert.Throws<InvalidDataException>(() => PhysicalFileAuthority.ProbeStorageEvidenceNamespaceEntryFromRoot(
                fixture.Source, Path.Combine(parent, "leaf"), "Replacement fixture", new PhysicalNamespaceProbeHooks
                {
                    ObserveNativeOpen = attempts.Add,
                    BeforeDirectoryHandleValidation = path =>
                    {
                        if (path != fixture.Source) return;
                        injected++;
                        Directory.Delete(parent);
                        fixture.CreateJunction(parent, outside);
                    }
                }));
            Assert.Equal(1, injected);
            Assert.Equal("unchanged-outside", File.ReadAllText(retained));
            Assert.Single(Directory.EnumerateFileSystemEntries(outside));
        }
        finally { WriteAttempts([], attempts); }
    }

    /// <summary>
    /// Observes later creation and deletion with independent fresh native attempts on every call.
    /// </summary>
    [Fact]
    public void RepeatedCalls_DoNotCacheAbsenceOrPresence()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var target = fixture.PrepareLeaf(2, "missing");
        var attempts = new List<PhysicalNamespaceOpenAttempt>();
        Assert.Equal(PhysicalFileAuthority.NamespaceEntryKind.Missing, Probe(fixture.Source, target, true, attempts));
        var firstCount = attempts.Count;
        File.WriteAllText(target, "fresh-bytes");
        Assert.Equal(PhysicalFileAuthority.NamespaceEntryKind.RegularFile, Probe(fixture.Source, target, true, attempts));
        Assert.Equal(firstCount * 2, attempts.Count);
        File.Delete(target);
        Assert.Equal(PhysicalFileAuthority.NamespaceEntryKind.Missing, Probe(fixture.Source, target, true, attempts));
        Assert.Equal(firstCount * 3, attempts.Count);
        WriteAttempts([], attempts);
    }

    /// <summary>
    /// Preserves actual missing-text admission with empty or unresolved supported storage evidence.
    /// </summary>
    /// <param name="unknownLoadEvidence">
    /// Whether an independent unresolved load journal must retain its bytes and refuse admission.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualReadAdmission_PreservesEmptyRootsOrUnresolvedEvidence(bool unknownLoadEvidence)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ProbeFixture();
        var files = new FileSystemManager(fixture.Root, NullLogger<FileSystemManager>.Instance);
        Directory.CreateDirectory(Path.Combine(files.GameSessionPath, "output"));
        foreach (var area in new[] { "file-publication-transactions", "load-transactions", "worker-apply-transactions" })
            Directory.CreateDirectory(Path.Combine(files.RuntimeRootPath, area));
        var evidence = Path.Combine(files.RuntimeRootPath, "load-transactions", "unknown-journal");
        if (unknownLoadEvidence) File.WriteAllBytes(evidence, [0xFE, 0]);
        if (unknownLoadEvidence)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => files.ReadFileAsync("output/missing.txt"));
            Assert.Equal(new byte[] { 0xFE, 0 }, File.ReadAllBytes(evidence));
        }
        else Assert.Null(await files.ReadFileAsync("output/missing.txt"));
        Assert.False(File.Exists(files.SessionGenerationPath));
        Assert.False(File.Exists(files.ResolvePath("config.json")));
    }

    /// <summary>
    /// Invokes the real selected traversal with complete scoped native observations.
    /// </summary>
    /// <param name="root">
    /// The physical root to anchor.
    /// </param>
    /// <param name="target">
    /// The absolute requested descendant.
    /// </param>
    /// <param name="storage">
    /// True selects the storage-specific entry; false selects the unchanged general entry.
    /// </param>
    /// <param name="attempts">
    /// Receives every actual native open without a quota.
    /// </param>
    /// <returns>
    /// The original native traversal classification.
    /// </returns>
    private static PhysicalFileAuthority.NamespaceEntryKind Probe(string root, string target, bool storage,
        List<PhysicalNamespaceOpenAttempt> attempts)
    {
        var hooks = new PhysicalNamespaceProbeHooks { ObserveNativeOpen = attempts.Add };
        return storage
            ? PhysicalFileAuthority.ProbeStorageEvidenceNamespaceEntryFromRoot(root, target, "Storage fixture", hooks)
            : PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(root, target, "Storage fixture", hooks);
    }

    /// <summary>
    /// Compares actual classification or refusal from both real traversal entries.
    /// </summary>
    /// <param name="root">
    /// The physical root to anchor.
    /// </param>
    /// <param name="target">
    /// Requested descendant or deliberately invalid relationship.
    /// </param>
    /// <param name="expectedKind">
    /// Expected successful classification, or <see langword="null"/> when the original entry must refuse.
    /// </param>
    /// <param name="requireInvalidData">
    /// Whether refusal must be <see cref="InvalidDataException"/>; false permits the original platform sharing failure class.
    /// </param>
    private void CompareOutcome(string root, string target, PhysicalFileAuthority.NamespaceEntryKind? expectedKind = null,
        bool requireInvalidData = true)
    {
        var originalAttempts = new List<PhysicalNamespaceOpenAttempt>();
        var storageAttempts = new List<PhysicalNamespaceOpenAttempt>();
        PhysicalFileAuthority.NamespaceEntryKind? original = null, storage = null;
        var originalError = Record.Exception(() => { original = Probe(root, target, false, originalAttempts); });
        var storageError = Record.Exception(() => { storage = Probe(root, target, true, storageAttempts); });
        if (expectedKind.HasValue)
        {
            Assert.Null(originalError);
            Assert.Null(storageError);
            Assert.Equal(expectedKind, original);
        }
        else
        {
            Assert.NotNull(originalError);
            Assert.NotNull(storageError);
            if (requireInvalidData) Assert.IsType<InvalidDataException>(originalError);
        }
        Assert.Equal(original, storage);
        Assert.Equal(originalError?.GetType(), storageError?.GetType());
        WriteAttempts(originalAttempts, storageAttempts);
    }

    /// <summary>
    /// Writes all attempted native opens for both compared entries.
    /// </summary>
    /// <param name="original">
    /// Complete general-entry attempts, or an empty list when not compared.
    /// </param>
    /// <param name="storage">
    /// Complete storage-entry attempts.
    /// </param>
    private void WriteAttempts(IReadOnlyList<PhysicalNamespaceOpenAttempt> original, IReadOnlyList<PhysicalNamespaceOpenAttempt> storage) =>
        _output.WriteLine(JsonSerializer.Serialize(new { Original = original, Storage = storage }));

    /// <summary>
    /// Owns one small ordinary directory tree, including explicitly tracked link entries and temporary ACL changes.
    /// </summary>
    private sealed class ProbeFixture : IDisposable
    {
        private readonly List<string> _junctions = [];
        private readonly string _parent;
        private int _activeDenials;
        private bool _retainRoot;

        /// <summary>
        /// Creates independent ordinary directories without game bootstrap or generation.
        /// </summary>
        internal ProbeFixture()
        {
            _parent = Path.GetFullPath(Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "storage-namespace-probe"));
            Directory.CreateDirectory(_parent);
            Root = Path.Combine(_parent, Guid.NewGuid().ToString("N"));
            Source = Path.Combine(Root, "физический корень");
            Directory.CreateDirectory(Source);
        }

        /// <summary>
        /// Gets this case's owned absolute root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the independent physical traversal root.
        /// </summary>
        internal string Source { get; }

        /// <summary>
        /// Creates only the ordinary ancestors and requested leaf kind for one probe.
        /// </summary>
        /// <param name="depth">
        /// Positive relative component count including the leaf.
        /// </param>
        /// <param name="kind">
        /// Missing, file or directory leaf representation.
        /// </param>
        /// <returns>
        /// Absolute final leaf path.
        /// </returns>
        internal string PrepareLeaf(int depth, string kind)
        {
            var path = Source;
            for (var index = 1; index < depth; index++) path = Path.Combine(path, "уровень-" + index);
            Directory.CreateDirectory(path);
            path = Path.Combine(path, "запись.txt");
            if (kind == "file") File.WriteAllText(path, "leaf-bytes");
            else if (kind == "directory") Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// Captures exact ordinary file contents before any link fixtures are introduced.
        /// </summary>
        /// <returns>
        /// Ordered relative file names and content hashes.
        /// </returns>
        internal KeyValuePair<string, string>[] CaptureFiles() => Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Select(path => new KeyValuePair<string, string>(Path.GetRelativePath(Root, path),
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))))
            .OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();

        /// <summary>
        /// Applies a temporary denial only to an owned entry and restores its original descriptor.
        /// </summary>
        /// <param name="path">
        /// Owned existing entry whose descriptor is restored in the finally block.
        /// </param>
        /// <param name="file">
        /// Whether the entry is an ordinary file rather than a directory.
        /// </param>
        /// <param name="rights">
        /// Access bits denied to this process's current user for the test interval.
        /// </param>
        /// <param name="action">
        /// Real probe assertions to execute under the temporary denial.
        /// </param>
        [SupportedOSPlatform("windows")]
        internal void WithDeniedAccess(string path, bool file, FileSystemRights rights, Action action)
        {
            using var denial = DenyAccess(path, file, rights);
            action();
        }

        /// <summary>
        /// Installs one owned denial whose disposal explicitly persists and verifies the original DACL.
        /// </summary>
        /// <param name="path">
        /// Strict descendant of this independent fixture root.
        /// </param>
        /// <param name="file">
        /// Whether the existing entry is a file rather than a directory.
        /// </param>
        /// <param name="rights">
        /// Access bits denied only to the current user during the returned scope.
        /// </param>
        /// <returns>
        /// A single-use restoration scope; a failed restoration retains the root instead of attempting cleanup.
        /// </returns>
        [SupportedOSPlatform("windows")]
        internal IDisposable DenyAccess(string path, bool file, FileSystemRights rights)
        {
            RequireOwned(path);
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User ?? throw new InvalidOperationException("Current user SID is absent.");
            var original = ReadSecurity(path, file);
            var descriptor = original.GetSecurityDescriptorBinaryForm();
            var originalDacl = original.GetSecurityDescriptorSddlForm(AccessControlSections.Access);
            FileSystemSecurity changed = file ? new FileSecurity() : new DirectorySecurity();
            changed.SetSecurityDescriptorBinaryForm(descriptor, AccessControlSections.Access);
            changed.AddAccessRule(new(sid, rights, AccessControlType.Deny));
            _activeDenials++;
            try { PersistSecurity(path, file, changed); }
            catch { _retainRoot = true; throw; }
            return new RestorationScope(() =>
            {
                try
                {
                    FileSystemSecurity restored = file ? new FileSecurity() : new DirectorySecurity();
                    restored.SetSecurityDescriptorBinaryForm(descriptor, AccessControlSections.Access);
                    PersistSecurity(path, file, restored);
                    Assert.Equal(originalDacl, ReadSecurity(path, file).GetSecurityDescriptorSddlForm(AccessControlSections.Access));
                    _activeDenials--;
                }
                catch { _retainRoot = true; throw; }
            });
        }

        /// <summary>
        /// Reads the descriptor of one already checked owned entry.
        /// </summary>
        /// <param name="path">
        /// Existing owned absolute path.
        /// </param>
        /// <param name="file">
        /// True for an ordinary file; false for a directory.
        /// </param>
        /// <returns>
        /// The original descriptor used only by this fixture's temporary denial scope.
        /// </returns>
        [SupportedOSPlatform("windows")]
        private static FileSystemSecurity ReadSecurity(string path, bool file) =>
            file ? new FileInfo(path).GetAccessControl() : new DirectoryInfo(path).GetAccessControl();

        /// <summary>
        /// Persists a descriptor whose access section has explicitly been marked modified.
        /// </summary>
        /// <param name="path">
        /// Existing owned absolute path.
        /// </param>
        /// <param name="file">
        /// True for an ordinary file; false for a directory.
        /// </param>
        /// <param name="security">
        /// Descriptor of the matching file or directory type.
        /// </param>
        [SupportedOSPlatform("windows")]
        private static void PersistSecurity(string path, bool file, FileSystemSecurity security)
        {
            if (file) new FileInfo(path).SetAccessControl((FileSecurity)security);
            else new DirectoryInfo(path).SetAccessControl((DirectorySecurity)security);
        }

        /// <summary>
        /// Creates and tracks a junction between two explicitly owned fixture paths.
        /// </summary>
        /// <param name="path">
        /// New owned junction name, removed before recursive fixture cleanup.
        /// </param>
        /// <param name="target">
        /// Existing ordinary target inside the same independent fixture.
        /// </param>
        internal void CreateJunction(string path, string target)
        {
            RequireOwned(path); RequireOwned(target);
            _junctions.Add(path);
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{path}\" \"{target}\"")
            { UseShellExecute = false, CreateNoWindow = true }) ?? throw new InvalidOperationException("Junction helper did not start.");
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }

        /// <summary>
        /// Validates an absolute target before any fixture mutation or removal.
        /// </summary>
        /// <param name="path">
        /// Target that must be a strict descendant of this case's root.
        /// </param>
        private void RequireOwned(string path)
        {
            if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Fixture target escaped its owned root.");
        }

        /// <summary>
        /// Removes tracked link entries before deleting the verified ordinary fixture root.
        /// </summary>
        public void Dispose()
        {
            var resolved = Path.GetFullPath(Root);
            if (!resolved.StartsWith(_parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Fixture cleanup escaped its declared parent.");
            if (_retainRoot || _activeDenials != 0)
                throw new InvalidOperationException("Fixture ACL restoration is unresolved; root retained: " + resolved);
            foreach (var junction in _junctions)
            {
                RequireOwned(junction);
                if (Directory.Exists(junction)) Directory.Delete(junction, recursive: false);
            }
            Directory.Delete(resolved, recursive: true);
        }

        /// <summary>
        /// Invokes one owned restoration at most once, including when restoration fails.
        /// </summary>
        private sealed class RestorationScope : IDisposable
        {
            private Action? _restore;

            /// <summary>
            /// Captures the original-descriptor restoration operation.
            /// </summary>
            /// <param name="restore">
            /// Exact restoration for one newly created owned entry.
            /// </param>
            internal RestorationScope(Action restore) => _restore = restore;

            /// <summary>
            /// Persists and verifies the original descriptor without retrying a failed restoration.
            /// </summary>
            public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
        }
    }
}
