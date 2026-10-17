using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserProtocolCutoverTests
{
    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, false)]
    [InlineData(false, true, false)] [InlineData(true, true, false)]
    [InlineData(false, false, true)] [InlineData(true, false, true)]
    [InlineData(false, true, true)] [InlineData(true, true, true)]
    public async Task WindowsFreshBrowser_UsesCurrentDeclaredMembers(bool existed, bool rollback, bool extended)
    {
        Assert.True(OperatingSystem.IsWindows(), "Native Windows body required; no skip/early-return qualification.");
        await InitializeAsync(extended);
        if (existed)
        {
            await _files.WriteFileAtomicBytesAsync(Member, Before);
            Directory.CreateDirectory(Path.GetDirectoryName(CompanionPath)!); File.WriteAllBytes(CompanionPath, Before);
        }
        var memberCuts = 0; var companionCuts = 0; var companionBoundaries = 0; var ran = false;
        _mutation = path => { if (path == Companion) companionBoundaries++; return Task.CompletedTask; };
        _publication = (phase, index) => {
            if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
            var member = CurrentMember(index);
            if (SamePath(member, _files.ResolvePath(Member))) memberCuts++;
            if (SamePath(member, CompanionPath)) companionCuts++;
        };
        var coordinator = new BrowserLocalWriteCoordinator(_files, new LocalUiSessionLockService(_files));
        var result = await coordinator.ExecuteAtomicAsync(new("native-protocol", "Fixture", "exact"), [Member, Companion], async lease =>
        {
            ran = true; Assert.Null(lease.MutationIntentRecorder);
            var local = Assert.IsType<ExplorerLocalTurnRollbackArtifacts.LocalBrowserTransaction>(lease.ExternalPublicationContext);
            Assert.Equal(7, local.Document.SchemaVersion);
            Assert.Equal(existed ? Before : null, await _files.ReadLocalFileBytesAsync(lease, Companion));
            await _files.WriteFileAtomicBytesAsync(lease, Companion, After);
            await _files.WriteFileAtomicBytesAsync(lease, Member, After);
            Assert.Equal(1, companionCuts); Assert.Equal(1, memberCuts);
            if (rollback) throw new InvalidOperationException("Original callback after both current members published.");
        });
        Assert.True(ran, result.Message); Assert.True(companionBoundaries > 0);
        Assert.Equal(rollback ? BrowserPreparedWriteDisposition.RolledBack : BrowserPreparedWriteDisposition.Committed, result.Disposition);
        Assert.Equal(!rollback, result.Success);
        var expected = rollback ? existed ? Before : null : After;
        Assert.Equal(expected, File.Exists(_files.ResolvePath(Member)) ? File.ReadAllBytes(_files.ResolvePath(Member)) : null);
        Assert.Equal(expected, File.Exists(CompanionPath) ? File.ReadAllBytes(CompanionPath) : null);
        AssertCleanBrowser();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WindowsAliasMember_RecordsOneBaselineAndRestoresExactBytes(bool extended)
    {
        Assert.True(OperatingSystem.IsWindows(), "Native Windows destination alias body required.");
        await InitializeAsync(extended); await _files.WriteFileAtomicBytesAsync(Member, Before);
        var alias = Member.Replace("ProtocolMember.bin", "protocolmember.bin", StringComparison.Ordinal);
        Assert.NotEqual(Member, alias); Assert.Equal(Before, File.ReadAllBytes(_files.ResolvePath(alias)));
        var publications = 0; var ran = false;
        _publication = (phase, index) => {
            if (phase == TrustedLocalPublicationPhase.MemberPublished && SamePath(CurrentMember(index), _files.ResolvePath(Member))) publications++;
        };
        var coordinator = new BrowserLocalWriteCoordinator(_files, new LocalUiSessionLockService(_files));
        var result = await coordinator.ExecuteAtomicAsync(new("native-alias", "Fixture", "alias"), [Member, alias], async lease =>
        {
            ran = true;
            var local = Assert.IsType<ExplorerLocalTurnRollbackArtifacts.LocalBrowserTransaction>(lease.ExternalPublicationContext);
            Assert.Single(local.Document.Entries); Assert.Null(lease.MutationIntentRecorder);
            await _files.WriteFileAtomicBytesAsync(lease, alias, After);
            Assert.Equal(1, publications); Assert.Equal(After, File.ReadAllBytes(_files.ResolvePath(Member)));
            throw new InvalidOperationException("Alias write requires its original rollback intent.");
        });
        Assert.True(ran, result.Message); Assert.Equal(2, publications);
        Assert.Equal(BrowserPreparedWriteDisposition.RolledBack, result.Disposition);
        Assert.Equal(Before, File.ReadAllBytes(_files.ResolvePath(Member))); AssertCleanBrowser();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task WindowsPendingMember_RecoversStandaloneOrDeclaredBytesOrAbsence(bool existed, bool extended)
    {
        Assert.True(OperatingSystem.IsWindows(), "Native Windows ordinary member pending recovery body required.");
        await InitializeAsync(extended);
        if (existed) { Directory.CreateDirectory(Path.GetDirectoryName(CompanionPath)!); File.WriteAllBytes(CompanionPath, Before); }
        await _files.WriteFileAtomicBytesAsync(Member, Before);
        var cuts = 0; var boundaries = 0;
        _mutation = path => { if (path == Companion) boundaries++; return Task.CompletedTask; };
        _publication = (phase, index) => {
            if (phase != TrustedLocalPublicationPhase.MemberPublished || !SamePath(CurrentMember(index), CompanionPath)) return;
            Assert.Equal(0, index); Assert.Equal(After, File.ReadAllBytes(CompanionPath));
            cuts++; File.WriteAllBytes(CompanionPath, [99]); throw new InvalidOperationException("Actual ordinary member cut.");
        };
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
        {
            // Extended-root rows include a declared browser transaction so its
            // preflight must validate ordinary ordinary companion scratch BEFORE deriving
            // GameSession-relative browser scratch. Ordinary rows are standalone.
            if (extended)
            {
                var transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                    _files, lease, [Member, Companion], "ordinary_pending");
                Assert.Equal(7, transaction.SchemaVersion); Assert.NotNull(transaction.LocalTransaction);
                lease.ExternalPublicationContext = transaction.LocalTransaction;
                Assert.Same(transaction.LocalTransaction, lease.ExternalPublicationContext);
            }
            Assert.Equal(existed ? Before : null, await _files.ReadLocalFileBytesAsync(lease, Companion));
            await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => _files.WriteFileAtomicBytesAsync(lease, Companion, After));
            Assert.Equal(1, cuts); Assert.Equal(1, boundaries); Assert.True(File.Exists(JournalPath));
            File.WriteAllBytes(CompanionPath, After); // Exact known image, controlled recovery continuation.
        }
        _publication = null; _mutation = null;
        var recovery = 0;
        var fresh = Fresh(new() { LocalPublicationRecoveryObserver = (_, _) => recovery++ });
        await using (var lease = await fresh.AcquireCanonicalWriteLeaseAsync())
            Assert.Equal(existed ? Before : null, await fresh.ReadLocalFileBytesAsync(lease, Companion));
        Assert.True(recovery > 0); Assert.Equal(Before, File.ReadAllBytes(_files.ResolvePath(Member)));
        AssertCleanBrowser();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task WindowsCleanupOnlyProtocol_UsesItsOriginalHandler(bool legacy, bool extended)
    {
        Assert.True(OperatingSystem.IsWindows(), "Native Windows cleanup protocol body required.");
        await InitializeAsync(extended); await _files.WriteFileAtomicBytesAsync(Member, Before);
        string intent;
        if (legacy)
        {
            var root = _files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root + "/legacy/" + DateTime.UtcNow.Ticks + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); intent = Path.Combine(root, "browser_write_cleanup_committed.intent");
            File.WriteAllBytes(intent, []); // Exact historical cleanup-only wire format, no fabricated version7 manifest.
        }
        else
        {
            var cuts = 0;
            var coordinator = new BrowserLocalWriteCoordinator(_files, new LocalUiSessionLockService(_files));
            var result = await coordinator.ExecuteAtomicAsync(new("native-cleanup", "Fixture", "cleanup"), [Member], async lease =>
            {
                Assert.IsType<ExplorerLocalTurnRollbackArtifacts.LocalBrowserTransaction>(lease.ExternalPublicationContext);
                await _files.WriteFileAtomicBytesAsync(lease, Member, After);
                _mutation = path => {
                    if (path.EndsWith("browser_write_cleanup_committed.intent", StringComparison.Ordinal) && ++cuts == 2)
                        throw new InvalidOperationException("Original cleanup after manifest removal.");
                    return Task.CompletedTask;
                };
            });
            Assert.True(result.Success, result.Message); Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
            Assert.Equal(2, cuts);
            intent = Assert.Single(Directory.GetFiles(_files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "*.intent", SearchOption.AllDirectories));
            var document = JsonNode.Parse(File.ReadAllBytes(intent))!; Assert.Equal(7, document["schemaVersion"]!.GetValue<int>());
            Assert.Empty(Directory.GetFiles(_files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "browser_write_manifest.json", SearchOption.AllDirectories));
        }
        _mutation = null;
        await using (var lease = await Fresh().AcquireCanonicalWriteLeaseAsync()) { }
        Assert.False(File.Exists(intent)); Assert.Equal(legacy ? Before : After, File.ReadAllBytes(_files.ResolvePath(Member)));
        AssertCleanBrowser();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WindowsAuthenticOriginalSix_RecoversReceiptAndRetainsNeutralBackup(bool extended)
    {
        Assert.True(OperatingSystem.IsWindows(), "Native original schema6 body required.");
        await InitializeAsync(extended);
        AssertPhysicalManifestParserControls();
        AssertNativePublicationPathEquivalenceControls();
        await _files.WriteFileAtomicBytesAsync(Member, Before);
        Directory.CreateDirectory(Path.GetDirectoryName(CompanionPath)!); File.WriteAllBytes(CompanionPath, Before);
        var commonMembers = 0;
        _publication = (phase, index) => {
            if (phase == TrustedLocalPublicationPhase.MemberPublished &&
                (SamePath(CurrentMember(index), CompanionPath) || SamePath(CurrentMember(index), _files.ResolvePath(Member)))) commonMembers++;
        };
        string neutral;
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
        {
            var transaction = await ExplorerLocalTurnRollbackArtifacts.StageOriginalBrowserWriteTransactionAsync(_files, lease,
                [Member, Companion], "authentic_original");
            Assert.Equal(6, transaction.SchemaVersion); Assert.Null(transaction.LocalTransaction); Assert.NotNull(lease.MutationIntentRecorder);
            Assert.Null(lease.ExternalPublicationContext);
            Assert.Equal(Before, await _files.ReadOriginalFileBytesAsync(lease, Companion));
            await _files.WriteFileAtomicBytesAsync(lease, Companion, After);
            await _files.WriteFileAtomicBytesAsync(lease, Member, After);
            Assert.Equal(0, commonMembers);
            using (var reader = new FileStream(CompanionPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var image = new MemoryStream())
            {
                reader.CopyTo(image);
                Assert.Equal(After, image.ToArray());
            }
            var manifest = ParsePhysicalManifestForTest(_files.ResolvePath(transaction.ManifestPath));
            Assert.Equal(6, manifest["schemaVersion"]!.GetValue<int>());
            Assert.NotNull(manifest["entries"]![0]!["publicationReceipt"]);
            Assert.NotNull(manifest["entries"]![1]!["publicationReceipt"]);
            Assert.Empty(manifest["externalEntries"]!.AsArray());
            neutral = AddNeutralBackup();
        }
        await using (var lease = await Fresh().AcquireCanonicalWriteLeaseAsync()) { }
        Assert.Equal(Before, File.ReadAllBytes(_files.ResolvePath(Member))); Assert.Equal(Before, File.ReadAllBytes(CompanionPath));
        Assert.Equal(Before, File.ReadAllBytes(neutral)); Assert.False(File.Exists(JournalPath));
        Assert.Empty(Directory.GetFiles(_files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "browser_write_manifest.json", SearchOption.AllDirectories));
    }

    private void AssertNativePublicationPathEquivalenceControls()
    {
        var equality = typeof(ReversibleFilePublication).GetMethod("PathsEqual",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        bool Same(string left, string right)
        {
            try { return (bool)equality.Invoke(null, [left, right])!; }
            catch (System.Reflection.TargetInvocationException failure) when (failure.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
        }
        var ordinary = Path.Combine(_root, "policy", "State.bin");
        var extended = @"\\?\" + ordinary;
        var differentDrive = char.ToUpperInvariant(ordinary[0]) == 'C' ? "D:" : "C:";
        var ordinaryParent = Path.GetDirectoryName(ordinary)!;
        var extendedParent = @"\\?\" + ordinaryParent;
        const string unc = @"\\server\share\policy\State.bin";
        const string extendedUnc = @"\\?\UNC\server\share\policy\State.bin";
        (string Left, string Right, bool Expected)[] comparisons =
        [
            (ordinary, ordinary, true), (ordinary, extended, true), (extended, ordinary, true),
            (extended, extended, true), (ordinary, extended.ToUpperInvariant(), true),
            (ordinary.Replace('\\', '/'), extended, true),
            (unc, extendedUnc, true), (extendedUnc, unc, true), (unc, extendedUnc.ToUpperInvariant(), true),
            (ordinary, ordinary + "other", false), (extended, extended + "other", false),
            (ordinary, differentDrive + ordinary[2..], false),
            (unc, @"\\server\other-share\policy\State.bin", false),
            (unc, @"\\other-server\share\policy\State.bin", false),
            (ordinary, Path.Combine(Path.GetDirectoryName(_root)!, "outside", "policy", "State.bin"), false),
            (ordinary, ordinary + "\\", false), (extended, extended + "\\", false),
            (ordinaryParent, extendedParent, true),
            (ordinaryParent + "\\", extendedParent + "\\", true),
            (ordinaryParent, extendedParent + "\\", false)
        ];
        foreach (var (left, right, expected) in comparisons)
            Assert.Equal(expected, Same(left, right));
        string[] refused =
        [
            @"\\.\C:\policy\State.bin", @"\\.\PhysicalDrive0",
            @"\\?\GLOBALROOT\Device\HarddiskVolume1\State.bin",
            @"\\?\Volume{00000000-0000-0000-0000-000000000000}\State.bin",
            @"\??\C:\policy\State.bin", @"\\?\pipe\boe-policy",
            ordinary + ".", extended + ".", ordinary + " ", extended + " ", ordinary + ":stream",
            Path.Combine(_root, "policy", "NUL.json"), Path.Combine(_root, "policy", "COM1"),
            Path.Combine(_root, ".", "policy", "State.bin"),
            @"\\?\" + Path.Combine(_root, ".", "policy", "State.bin"),
            Path.Combine(_root, "policy", "..", "policy", "State.bin"),
            @"\\?\" + Path.Combine(_root, "policy", "..", "policy", "State.bin"),
            @"\\?\UNC\server\share\policy\..\State.bin"
        ];
        foreach (var path in refused)
        {
            Assert.Throws<InvalidDataException>(() => Same(path, ordinary));
            Assert.Throws<InvalidDataException>(() => Same(ordinary, path));
        }
        output.WriteLine($"NativePathPolicyControls=True; Comparisons={comparisons.Length}; RefusedSpellings={refused.Length}; BothSides=True; UNC=StringOnly; NoFilesystemOrGrant=True");
    }

    private static JsonNode ParsePhysicalManifestForTest(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var preamble = Encoding.UTF8.GetPreamble();
        var offset = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        var json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(bytes, offset, bytes.Length - offset);
        return JsonNode.Parse(json)!;
    }

    private void AssertPhysicalManifestParserControls()
    {
        var path = Path.Combine(ConfiguredRoot, "manifest-parser-control.json");
        var body = Encoding.UTF8.GetBytes("{\"schemaVersion\":6,\"value\":\"Д\"}");
        var preamble = Encoding.UTF8.GetPreamble();
        try
        {
            foreach (var bytes in new byte[][] { body, [.. preamble, .. body] })
            {
                File.WriteAllBytes(path, bytes);
                var parsed = ParsePhysicalManifestForTest(path);
                Assert.Equal(6, parsed["schemaVersion"]!.GetValue<int>());
                Assert.Equal("Д", parsed["value"]!.GetValue<string>());
                Assert.Equal(bytes, File.ReadAllBytes(path));
            }
            byte[] malformed = [.. Encoding.UTF8.GetBytes("{\"value\":\""), 0xFF, .. Encoding.UTF8.GetBytes("\"}")];
            foreach (var bytes in new byte[][] { malformed, [.. preamble, .. malformed] })
            {
                File.WriteAllBytes(path, bytes);
                Assert.Throws<DecoderFallbackException>(() => ParsePhysicalManifestForTest(path));
                Assert.Equal(bytes, File.ReadAllBytes(path));
            }
            byte[] doublePreamble = [.. preamble, .. preamble, .. body];
            File.WriteAllBytes(path, doublePreamble);
            Assert.ThrowsAny<JsonException>(() => ParsePhysicalManifestForTest(path));
            Assert.Equal(doublePreamble, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
        Assert.False(File.Exists(path));
        output.WriteLine("ManifestParserControls=True; BOM=True; NoBOM=True; MalformedUtf8Rejected=True; DoubleBOMRejected=True; PhysicalBytesUnchanged=True");
    }

    [Theory]
    [InlineData("mixed-manifest", false)] [InlineData("empty-old-intent", false)]
    [InlineData("malformed-current-intent", false)] [InlineData("neutral", false)]
    [InlineData("mixed-manifest", true)] [InlineData("empty-old-intent", true)]
    [InlineData("malformed-current-intent", true)] [InlineData("neutral", true)]
    public async Task WindowsProtocolAdmission_PreservesPendingDecisionBeforeRecovery(string mode, bool extended)
    {
        Assert.True(OperatingSystem.IsWindows(), "Native Windows protocol-order body required.");
        await InitializeAsync(extended); await CheckProtocolAdmissionAsync(mode);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WindowsNeutralPendingBackup_RecoversWithoutBrowserManifest(bool extended)
    {
        Assert.True(OperatingSystem.IsWindows(), "Native Windows neutral pending-backup body required.");
        await InitializeAsync(extended); await CheckNeutralPendingBackupAsync();
    }

}
