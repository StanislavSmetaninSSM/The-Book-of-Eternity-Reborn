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
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!); File.WriteAllBytes(ProfilePath, Before);
        }
        var memberCuts = 0; var profileCuts = 0; var profileBoundaries = 0; var ran = false;
        _mutation = path => { if (path == "@daren_reward_profile") profileBoundaries++; return Task.CompletedTask; };
        _publication = (phase, index) => {
            if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
            var member = CurrentMember(index);
            if (SamePath(member, _files.ResolvePath(Member))) memberCuts++;
            if (SamePath(member, ProfilePath)) profileCuts++;
        };
        var coordinator = new BrowserLocalWriteCoordinator(_files, new LocalUiSessionLockService(_files));
        var result = await coordinator.ExecuteAtomicAsync(new("native-protocol", "Fixture", "exact"), [Member], async lease =>
        {
            ran = true; Assert.Null(lease.MutationIntentRecorder);
            var local = Assert.IsType<ExplorerLocalTurnRollbackArtifacts.LocalBrowserTransaction>(lease.ExternalPublicationContext);
            Assert.Equal(7, local.Document.SchemaVersion);
            var store = new DarenRewardProfileFileStore(_files);
            Assert.Equal(existed ? Before : null, await store.ReadExactBytesAsync(lease));
            await store.WriteExactBytesAtomicAsync(lease, After);
            await _files.WriteFileAtomicBytesAsync(lease, Member, After);
            Assert.Equal(1, profileCuts); Assert.Equal(1, memberCuts);
            if (rollback) throw new InvalidOperationException("Original callback after both current members published.");
        }, rollbackExternalFileIds: [ExplorerLocalTurnRollbackArtifacts.DarenRewardProfileExternalFileId]);
        Assert.True(ran, result.Message); Assert.True(profileBoundaries > 0);
        Assert.Equal(rollback ? BrowserPreparedWriteDisposition.RolledBack : BrowserPreparedWriteDisposition.Committed, result.Disposition);
        Assert.Equal(!rollback, result.Success);
        var expected = rollback ? existed ? Before : null : After;
        Assert.Equal(expected, File.Exists(_files.ResolvePath(Member)) ? File.ReadAllBytes(_files.ResolvePath(Member)) : null);
        Assert.Equal(expected, File.Exists(ProfilePath) ? File.ReadAllBytes(ProfilePath) : null);
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
    public async Task WindowsPendingProfile_RecoversStandaloneOrDeclaredBytesOrAbsence(bool existed, bool extended)
    {
        Assert.True(OperatingSystem.IsWindows(), "Native Windows external pending recovery body required.");
        await InitializeAsync(extended);
        if (existed) { Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!); File.WriteAllBytes(ProfilePath, Before); }
        await _files.WriteFileAtomicBytesAsync(Member, Before);
        var cuts = 0; var boundaries = 0;
        _mutation = path => { if (path == "@daren_reward_profile") boundaries++; return Task.CompletedTask; };
        _publication = (phase, index) => {
            if (phase != TrustedLocalPublicationPhase.MemberPublished || !SamePath(CurrentMember(index), ProfilePath)) return;
            Assert.Equal(0, index); Assert.Equal(After, File.ReadAllBytes(ProfilePath));
            cuts++; File.WriteAllBytes(ProfilePath, [99]); throw new InvalidOperationException("Actual external member cut.");
        };
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
        {
            // Extended-root rows include a declared browser transaction so its
            // preflight must filter external profile scratch BEFORE deriving
            // GameSession-relative browser scratch. Ordinary rows are standalone.
            if (extended)
            {
                var transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(
                    _files, lease, [Member], "external_pending",
                    rollbackExternalFileIds: [ExplorerLocalTurnRollbackArtifacts.DarenRewardProfileExternalFileId]);
                Assert.Equal(7, transaction.SchemaVersion); Assert.NotNull(transaction.LocalTransaction);
            }
            var store = new DarenRewardProfileFileStore(_files); store.EnsureWriteSupported(lease);
            Assert.Equal(existed ? Before : null, await store.ReadExactBytesAsync(lease));
            await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => store.WriteExactBytesAtomicAsync(lease, After));
            Assert.Equal(1, cuts); Assert.Equal(1, boundaries); Assert.True(File.Exists(JournalPath));
            File.WriteAllBytes(ProfilePath, After); // Exact known image, controlled recovery continuation.
        }
        _publication = null; _mutation = null;
        var recovery = 0;
        var fresh = Fresh(new() { LocalPublicationRecoveryObserver = (_, _) => recovery++ });
        await using (var lease = await fresh.AcquireCanonicalWriteLeaseAsync())
            Assert.Equal(existed ? Before : null, await new DarenRewardProfileFileStore(fresh).ReadExactBytesAsync(lease));
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
        await InitializeAsync(extended); await _files.WriteFileAtomicBytesAsync(Member, Before);
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!); File.WriteAllBytes(ProfilePath, Before);
        var commonMembers = 0;
        _publication = (phase, index) => {
            if (phase == TrustedLocalPublicationPhase.MemberPublished &&
                (SamePath(CurrentMember(index), ProfilePath) || SamePath(CurrentMember(index), _files.ResolvePath(Member)))) commonMembers++;
        };
        string neutral;
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
        {
            var transaction = await ExplorerLocalTurnRollbackArtifacts.StageOriginalBrowserWriteTransactionAsync(_files, lease,
                [Member], "authentic_original", rollbackExternalFileIds: [ExplorerLocalTurnRollbackArtifacts.DarenRewardProfileExternalFileId]);
            Assert.Equal(6, transaction.SchemaVersion); Assert.Null(transaction.LocalTransaction); Assert.NotNull(lease.MutationIntentRecorder);
            Assert.IsType<DarenRewardProfileRollbackTransaction>(lease.ExternalPublicationContext);
            var store = new DarenRewardProfileFileStore(_files);
            Assert.Equal(Before, await store.ReadExactBytesAsync(lease));
            await store.WriteExactBytesAtomicAsync(lease, After);
            await _files.WriteFileAtomicBytesAsync(lease, Member, After);
            Assert.Equal(0, commonMembers); Assert.Equal(After, File.ReadAllBytes(ProfilePath));
            var manifest = JsonNode.Parse(File.ReadAllBytes(_files.ResolvePath(transaction.ManifestPath)))!;
            Assert.Equal(6, manifest["schemaVersion"]!.GetValue<int>());
            Assert.NotNull(manifest["entries"]![0]!["publicationReceipt"]);
            Assert.NotNull(manifest["externalEntries"]![0]!["publishedIdentity"]);
            neutral = AddNeutralBackup();
        }
        await using (var lease = await Fresh().AcquireCanonicalWriteLeaseAsync()) { }
        Assert.Equal(Before, File.ReadAllBytes(_files.ResolvePath(Member))); Assert.Equal(Before, File.ReadAllBytes(ProfilePath));
        Assert.Equal(Before, File.ReadAllBytes(neutral)); Assert.False(File.Exists(JournalPath));
        Assert.Empty(Directory.GetFiles(_files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "browser_write_manifest.json", SearchOption.AllDirectories));
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
}
