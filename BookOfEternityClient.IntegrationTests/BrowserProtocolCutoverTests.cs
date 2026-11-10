using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserProtocolCutoverTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-browser-protocol-" + Guid.NewGuid().ToString("N"));
    private const string Member = "game_state/meta/ProtocolMember.bin";
    private static readonly byte[] Before = [0xEF, 0xBB, 0xBF, 0, 0xFF, 41];
    private static readonly byte[] After = [0xD0, 0x94, 0, 0xFE, 42];
    private FileSystemManager _files = null!;
    private Func<string, Task>? _mutation;
    private Action<TrustedLocalPublicationPhase, int>? _publication;
    private string ConfiguredRoot = null!;
    private string ProfilePath => Path.Combine(_root, DarenQteRewardProfileService.ProfileRelativePath.Replace('/', Path.DirectorySeparatorChar));
    private string JournalPath => Path.Combine(_root, ".boe_runtime/trusted-local-publication-v1/active.json");

    private async Task InitializeAsync(bool extended = false)
    {
        Directory.CreateDirectory(_root);
        ConfiguredRoot = extended ? @"\\?\" + Path.GetFullPath(_root) : _root;
        if (extended) { Assert.True(OperatingSystem.IsWindows()); Assert.NotEqual(_root, ConfiguredRoot); }
        _files = new(ConfiguredRoot, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks {
                BeforeCanonicalMutationBoundaryAsync = path => _mutation?.Invoke(path) ?? Task.CompletedTask,
                LocalPublicationObserver = (phase, index) => _publication?.Invoke(phase, index)
            });
        await new StateManager(_files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
    }

    private string CurrentMember(int index)
    {
        using var journal = JsonDocument.Parse(File.ReadAllBytes(JournalPath));
        Assert.False(journal.RootElement.GetProperty("Committed").GetBoolean());
        var members = journal.RootElement.GetProperty("Members");
        Assert.InRange(index, 0, members.GetArrayLength() - 1);
        return members[index].GetProperty("Path").GetString()!;
    }
    private bool SamePath(string left, string right) => string.Equals(
        TrustedLocalFilePublication.NormalizeAuthorityPath(left, OperatingSystem.IsWindows()),
        TrustedLocalFilePublication.NormalizeAuthorityPath(right, OperatingSystem.IsWindows()),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private FileSystemManager Fresh(FileSystemManagerHooks? hooks = null) =>
        new(ConfiguredRoot, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
    private Dictionary<string, byte[]> Snapshot() => Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
        .Where(path => !path.EndsWith(".lock", StringComparison.Ordinal))
        .ToDictionary(path => Path.GetRelativePath(_root, path), File.ReadAllBytes, StringComparer.Ordinal);
    private void AssertSnapshot(Dictionary<string, byte[]> expected)
    {
        var actual = Snapshot(); Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
    private void AssertCleanBrowser()
    {
        var root = _files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any());
        Assert.False(File.Exists(JournalPath));
    }
    private string AddNeutralBackup()
    {
        var root = _files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root + "/browser_direct_gacha/" +
            DateTime.UtcNow.Ticks + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "game_state_meta_soul_state.json.rollback." + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(path, Before); return path;
    }

    [Theory]
    [InlineData("mixed-manifest")]
    [InlineData("empty-old-intent")]
    [InlineData("malformed-current-intent")]
    [InlineData("neutral")]
    public async Task LinuxProtocolAdmission_PreservesPendingDecisionBeforeRecovery(string mode)
    {
        Assert.True(OperatingSystem.IsLinux(), "Actual Linux body required.");
        await InitializeAsync(); await CheckProtocolAdmissionAsync(mode);
    }

    private async Task CheckProtocolAdmissionAsync(string mode)
    {
        await _files.WriteFileAtomicBytesAsync(Member, Before);
        var cuts = 0; string? neutral = null;
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
        {
            var transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(_files, lease, [Member], "protocol");
            Assert.Equal(7, transaction.SchemaVersion);
            _publication = (phase, index) => {
                if (phase != TrustedLocalPublicationPhase.MemberPublished || !SamePath(CurrentMember(index), _files.ResolvePath(Member))) return;
                Assert.Equal(0, index); Assert.Equal(After, File.ReadAllBytes(_files.ResolvePath(Member)));
                cuts++; File.WriteAllBytes(_files.ResolvePath(Member), [99]);
                throw new InvalidOperationException("Actual protocol pending member cut.");
            };
            await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(
                () => _files.WriteFileAtomicBytesAsync(lease, Member, After));
            Assert.Equal(1, cuts); Assert.True(File.Exists(JournalPath));
            // Controlled repair to the journal's exact after-image lets a later
            // original recovery actually settle. This does not fabricate a decision.
            File.WriteAllBytes(_files.ResolvePath(Member), After);
            _publication = null;
            if (mode == "neutral") neutral = AddNeutralBackup();
            else
            {
                var root = _files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root + "/foreign/" + DateTime.UtcNow.Ticks + "_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                if (mode == "mixed-manifest")
                {
                    var json = JsonNode.Parse(File.ReadAllBytes(_files.ResolvePath(transaction.ManifestPath)))!;
                    json["schemaVersion"] = 6; json["scope"] = "foreign";
                    // Deliberately mixed metadata discriminator only; NOT a claim
                    // that editing7 creates authentic original6 publication receipts.
                    File.WriteAllText(Path.Combine(root, "browser_write_manifest.json"), json.ToJsonString());
                }
                else File.WriteAllBytes(Path.Combine(root, "browser_write_cleanup_committed.intent"),
                    mode == "empty-old-intent" ? [] : "{broken"u8.ToArray());
            }
        }
        var beforeAdmission = Snapshot(); var recoveryEvents = 0; var mutations = 0;
        var fresh = Fresh(new() {
            LocalPublicationRecoveryObserver = (_, _) => recoveryEvents++,
            BeforeCanonicalMutationBoundaryAsync = _ => { mutations++; return Task.CompletedTask; }
        });
        if (mode != "neutral")
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await fresh.AcquireCanonicalWriteLeaseAsync(); });
            Assert.Equal(0, recoveryEvents); Assert.Equal(0, mutations); AssertSnapshot(beforeAdmission);
            Assert.Equal(After, File.ReadAllBytes(_files.ResolvePath(Member)));
        }
        else
        {
            await using (var lease = await fresh.AcquireCanonicalWriteLeaseAsync()) { }
            Assert.True(recoveryEvents > 0); Assert.True(mutations > 0);
            Assert.Equal(Before, File.ReadAllBytes(_files.ResolvePath(Member)));
            Assert.Equal(Before, File.ReadAllBytes(neutral!)); Assert.False(File.Exists(JournalPath));
            var manifests = Directory.GetFiles(_files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "browser_write_manifest.json", SearchOption.AllDirectories);
            Assert.Empty(manifests);
        }
        output.WriteLine($"Protocol={mode}; cut={cuts}; recoveryEvents={recoveryEvents}; mutations={mutations}");
    }

    [Fact]
    public async Task LinuxNeutralPendingBackup_RecoversWithoutBrowserManifest()
    {
        Assert.True(OperatingSystem.IsLinux(), "Actual Linux pending-backup body required.");
        await InitializeAsync(); await CheckNeutralPendingBackupAsync();
    }

    private async Task CheckNeutralPendingBackupAsync()
    {
        var backup = AddNeutralBackup();
        var relative = FileSystemManager.GetLocalRelativePath(_files.GameSessionPath, backup, OperatingSystem.IsWindows());
        var initial = Snapshot(); var cuts = 0;
        _publication = (phase, index) => {
            if (phase != TrustedLocalPublicationPhase.MemberStaged || !SamePath(CurrentMember(index), backup)) return;
            Assert.Equal(0, index); Assert.Equal(Before, File.ReadAllBytes(backup));
            var stage = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(backup)!, ".boe-local-*.stage"));
            Assert.Equal(After, File.ReadAllBytes(stage));
            cuts++; File.WriteAllBytes(backup, [99]);
            throw new InvalidOperationException("Actual neutral backup staging cut.");
        };
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Null(lease.BrowserLocalAccess); Assert.Null(lease.ExternalPublicationContext);
            await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(
                () => _files.WriteFileAtomicBytesAsync(lease, relative, After));
            Assert.Equal(1, cuts); Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(backup));
            Assert.True(File.Exists(JournalPath));
            var journal = File.ReadAllBytes(JournalPath);
            var stage = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(backup)!, ".boe-local-*.stage"));
            Assert.Equal(After, File.ReadAllBytes(stage));
            // Controlled restoration of the exact recorded before-image only;
            // retain the actual journal/stage for the original cold recovery.
            File.WriteAllBytes(backup, Before);
            Assert.Equal(journal, File.ReadAllBytes(JournalPath)); Assert.Equal(After, File.ReadAllBytes(stage));
            Assert.Empty(Directory.GetFiles(_files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "browser_write_manifest.json", SearchOption.AllDirectories));
            Assert.Equal(ExplorerLocalTurnRollbackArtifacts.BrowserStorageProtocol.None,
                ExplorerLocalTurnRollbackArtifacts.ClassifyBrowserStorageEvidence(_files, lease));
        }
        _publication = null;
        var recovery = 0;
        var fresh = Fresh(new() { LocalPublicationRecoveryObserver = (_, _) => recovery++ });
        await using (var lease = await fresh.AcquireCanonicalWriteLeaseAsync()) { }
        Assert.True(recovery > 0); Assert.Equal(Before, File.ReadAllBytes(backup));
        Assert.False(File.Exists(JournalPath));
        Assert.Empty(Directory.GetFiles(_root, ".boe-local-*", SearchOption.AllDirectories));
        AssertSnapshot(initial); // No adoption/authority metadata was minted.
        output.WriteLine($"NeutralOnly=True; cut={cuts}; recoveryEvents={recovery}; exactSnapshot=True");
    }

    public void Dispose()
    {
        _mutation = null; _publication = null;
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        output.WriteLine("OwnedFixtureRemoved=" + !Directory.Exists(_root));
    }
}
