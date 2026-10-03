using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableLoadReplacementTests : IDisposable
{
    private const string SoulPath = "game_state/meta/soul_state.json";
    private const string MarkerPath = "lore/load-marker.bin";
    private const string OldOnlyPath = "game_state/world/old-only.bin";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-portable-load-" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;
    private readonly FileSystemManager _files;
    private readonly StateManager _state;
    private readonly SaveLoadService _service;
    private readonly PortableSaveFixture.CaptureLogger _logger = new();
    private readonly List<(TrustedLocalPublicationPhase Phase, int Index)> _phases = [];
    private readonly byte[] _loadedSoul = Encoding.UTF8.GetBytes("""{"soulName":"Loaded soul","currentRealm":"Mortal World","currentIncarnation":1}""");
    private readonly byte[] _loadedMarker = [0xEF, 0xBB, 0xBF, 0, 0xFF, 7];
    private readonly byte[] _config = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("""{"language":"en","consoleFontSize":0,"musicVolume":31}""")];
    private Action<string>? _afterExtraction;
    private Action<string>? _beforePreparationCleanup;
    private Action? _afterPublication;
    private bool _observeLoad;
    private int _prepared;
    private int _lifecycleOpen;
    private Action<TrustedLocalPublicationPhase, int>? _fault;

    public PortableLoadReplacementTests(ITestOutputHelper output)
    {
        _output = output;
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (!_observeLoad) return;
                    _phases.Add((phase, index));
                    _fault?.Invoke(phase, index);
                },
                BeforeSessionLifecycleLockOpenAsync = () =>
                {
                    if (_observeLoad) _lifecycleOpen++;
                    return Task.CompletedTask;
                }
            });
        _state = PortableSaveFixture.Seed(_files);
        _service = new SaveLoadService(_files, _state, _logger, new SaveLoadServiceHooks
        {
            BeforeLoadLeaseAcquisitionAsync = () => { _prepared++; return Task.CompletedTask; },
            AfterLoadArchiveExtractedAsync = path => { _afterExtraction?.Invoke(path); return Task.CompletedTask; },
            BeforeLoadPreparationCleanupAsync = path => { _beforePreparationCleanup?.Invoke(path); return Task.CompletedTask; },
            AfterLoadPublicationValidatedAsync = () => { _afterPublication?.Invoke(); return Task.CompletedTask; }
        });
    }

    [Fact]
    public async Task CurrentProducerLoadPublishesOneReplacementAndPreservesSelectedRootArchive()
    {
        var source = await PrepareCurrentArchiveAsync();
        var protectedFiles = SnapshotLibraryAndSource(source);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var externalProfile = Path.Combine(_root, "client_profile", "qte_showcase_rewards.json");
        Directory.CreateDirectory(Path.GetDirectoryName(externalProfile)!);
        File.WriteAllBytes(externalProfile, [0xFF, 0, 41]);
        var history = File.ReadAllBytes(_files.ResolvePath(ResourceMaterializationContract.HistoryPath));

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(1, _prepared);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.False(result.ContinuationBlocked);
        Assert.False(result.NeedsFollowUp);
        Assert.Equal(source, result.SelectedSourcePath);
        Assert.False(generation.SequenceEqual(File.ReadAllBytes(_files.SessionGenerationPath)));
        Assert.False(string.IsNullOrWhiteSpace(result.EstablishedGeneration));
        Assert.Single(_phases.Where(value => value.Phase == TrustedLocalPublicationPhase.Committed));
        Assert.Equal(_loadedSoul, File.ReadAllBytes(_files.ResolvePath(SoulPath)));
        Assert.Equal(_loadedMarker, File.ReadAllBytes(_files.ResolvePath(MarkerPath)));
        Assert.Equal(history, File.ReadAllBytes(_files.ResolvePath(ResourceMaterializationContract.HistoryPath)));
        Assert.False(File.Exists(_files.ResolvePath(OldOnlyPath)));
        Assert.False(File.Exists(_files.ResolvePath("save_manifest.json")));
        Assert.Equal("Loaded soul", _state.CurrentState.SoulName);
        AssertPreserved(protectedFiles);
        Assert.Equal(new byte[] { 0xFF, 0, 41 }, File.ReadAllBytes(externalProfile));
        AssertOwnedScratchEmpty();
    }

    [Fact]
    public async Task LaterMemberFailureRestoresCompleteSessionSourceLibraryAndGeneration()
    {
        var source = await PrepareCurrentArchiveAsync();
        var before = Snapshot(_files.GameSessionPath);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var cuts = 0;
        _fault = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished || index != 1) return;
            cuts++;
            throw new InvalidOperationException("load later-member cut");
        };

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(1, _prepared);
        Assert.Equal(1, cuts);
        Assert.Equal(LoadReplacementDisposition.RolledBack, result.Disposition);
        Assert.Equal(before.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
        AssertPreserved(before);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal("Old live soul", _state.CurrentState.SoulName);
        Assert.Equal(28, _state.Settings.ConsoleFontSize);
        Assert.DoesNotContain(_phases, value => value.Phase == TrustedLocalPublicationPhase.Committed);
        AssertOwnedScratchEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PresentSettingsNormalizeOnFreshReceiverAndPreserveExactSourceBytes(bool archiveHasConfig)
    {
        var source = await PrepareCurrentArchiveAsync(archiveHasConfig);
        Put("config.json", archiveHasConfig
            ? Encoding.UTF8.GetBytes("""{"language":"ru","consoleFontSize":26}""")
            : _config);
        _state.Settings.ConsoleFontSize = 28;
        _state.Settings.Language = "ru";

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(1, _prepared);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.Equal(_config, File.ReadAllBytes(_files.ResolvePath("config.json")));
        Assert.Equal(20, _state.Settings.ConsoleFontSize);
        Assert.Equal("en", _state.Settings.Language);
        Assert.Equal(31, _state.Settings.MusicVolume);
        AssertOwnedScratchEmpty();
    }

    [Fact]
    public async Task BothConfigsAbsentUsesFreshDefaultsWithoutPersistingConfiguration()
    {
        var source = await PrepareCurrentArchiveAsync();
        Assert.False(File.Exists(_files.ResolvePath("config.json")));
        _state.Settings.Language = "en";
        _state.Settings.MusicVolume = 2;

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(1, _prepared);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.False(File.Exists(_files.ResolvePath("config.json")));
        var defaults = new BookOfEternityClient.Configuration.GameSettings();
        Assert.Equal(defaults.ConsoleFontSize, _state.Settings.ConsoleFontSize);
        Assert.Equal(defaults.Language, _state.Settings.Language);
        Assert.Equal(defaults.MusicVolume, _state.Settings.MusicVolume);
        AssertOwnedScratchEmpty();
    }

    [Fact]
    public async Task BoundInvocationRejectsBeforePreparationAndUnboundLoadRemainsAvailable()
    {
        var source = await PrepareCurrentArchiveAsync();
        var before = Snapshot(_files.GameSessionPath);
        var generationBytes = File.ReadAllBytes(_files.SessionGenerationPath);
        string generation;
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
            generation = _files.ReadLocalGenerationSnapshot(lease).Binding.Id!;

        var rejected = await SessionOperationContext.RunBoundAsync(_files, generation,
            () => _service.LoadGameWithOutcomeAsync(source));

        Report(rejected);
        Assert.Equal(LoadReplacementDisposition.NotLoaded, rejected.Disposition);
        var failure = Assert.IsType<InvalidOperationException>(rejected.Failure);
        Assert.Contains("generation-bound", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, _prepared);
        Assert.Equal(0, _lifecycleOpen);
        Assert.Empty(_phases);
        Assert.False(Directory.Exists(Path.Combine(_files.RuntimeRootPath, "load-staging")));
        Assert.Equal(before.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
        AssertPreserved(before);
        Assert.Equal(generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal("Old live soul", _state.CurrentState.SoulName);
        Assert.Equal(28, _state.Settings.ConsoleFontSize);

        var allowed = await _service.LoadGameWithOutcomeAsync(source);
        Report(allowed);
        Assert.Equal(LoadReplacementDisposition.Committed, allowed.Disposition);
        Assert.Equal(1, _prepared);
        Assert.Single(_phases.Where(value => value.Phase == TrustedLocalPublicationPhase.Committed));
        AssertOwnedScratchEmpty();
    }

    [Theory]
    [InlineData("imports/source.zip")]
    [InlineData("imports/source.zip/child.bin")]
    [InlineData("imports")]
    public async Task IncomingPayloadCannotOverwriteSelectedArchiveOrItsTopology(string incomingPath)
    {
        var source = await PrepareCurrentArchiveAsync(selectedRelativePath: "imports/source.zip", collision: incomingPath);
        var before = Snapshot(_files.GameSessionPath);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        var failure = Assert.IsType<InvalidDataException>(result.Failure);
        Assert.Contains("selected archive", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _prepared);
        Assert.Equal(0, _lifecycleOpen);
        Assert.Empty(_phases);
        Assert.Equal(before.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
        AssertPreserved(before);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal("Old live soul", _state.CurrentState.SoulName);
        AssertOwnedScratchEmpty();
    }

    [Theory]
    [InlineData(" config.json")]
    [InlineData("config.json ")]
    [InlineData(" saves/manual_saves/new.zip")]
    [InlineData(" game_state/control/pending_turn_snapshot.json")]
    public async Task PayloadAliasesRejectBeforeSettingsLibraryOrEphemeralMutation(string incomingPath)
    {
        var source = await PrepareCurrentArchiveAsync(collision: incomingPath);
        var before = Snapshot(_files.GameSessionPath);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        var failure = Assert.IsType<InvalidDataException>(result.Failure);
        Assert.Contains("canonical spelling", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _prepared);
        Assert.Equal(0, _lifecycleOpen);
        Assert.Empty(_phases);
        Assert.Equal(before.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
        AssertPreserved(before);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal("Old live soul", _state.CurrentState.SoulName);
        Assert.Equal(28, _state.Settings.ConsoleFontSize);
        AssertOwnedScratchEmpty();
    }

    private async Task<string> PrepareCurrentArchiveAsync(bool archiveHasConfig = false,
        string selectedRelativePath = "import.zip", string? collision = null)
    {
        Put(SoulPath, _loadedSoul);
        Put(MarkerPath, _loadedMarker);
        if (archiveHasConfig) Put("config.json", _config);
        Assert.True(await _service.SaveGameAsync("load-source", "current public producer for load"));
        var produced = Assert.Single(Directory.GetFiles(_files.ResolvePath("saves/manual_saves"), "*.zip"));
        var source = _files.ResolvePath(selectedRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.Copy(produced, source);
        if (collision != null)
        {
            // Optional-manifest admission is an existing archive contract. Keep every
            // real producer payload while adding one bounded, otherwise valid collision.
            using var archive = ZipFile.Open(source, ZipArchiveMode.Update);
            archive.GetEntry("save_manifest.json")!.Delete();
            using var payload = archive.CreateEntry(collision, CompressionLevel.NoCompression).Open();
            payload.WriteByte(42);
        }
        using (var archive = ZipFile.OpenRead(source))
        {
            Assert.NotNull(archive.GetEntry(SoulPath));
            Assert.NotNull(archive.GetEntry(ResourceMaterializationContract.HistoryPath));
        }
        foreach (var scope in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            Put($"saves/{scope}/existing.zip", [0xFF, 0, 19]);
        Put(SoulPath, Encoding.UTF8.GetBytes("""{"soulName":"Old live soul","currentRealm":"Mortal World","currentIncarnation":1}"""));
        Put(MarkerPath, [88, 89]);
        Put(OldOnlyPath, [0xFF, 0, 0xFE]);
        await _state.RefreshGameStateAsync();
        Assert.Equal("Old live soul", _state.CurrentState.SoulName);
        _state.Settings.ConsoleFontSize = 28;
        _observeLoad = true;
        return source;
    }

    private void Put(string relative, byte[] bytes)
    {
        var path = _files.ResolvePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private Dictionary<string, string> SnapshotLibraryAndSource(string source)
    {
        var result = Snapshot(_files.ResolvePath("saves"));
        result[source] = Hash(source);
        return result;
    }

    private static Dictionary<string, string> Snapshot(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => path, Hash, StringComparer.Ordinal);

    private static string Hash(string path)
    {
        using var source = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(source));
    }

    private static void AssertPreserved(Dictionary<string, string> before)
    {
        foreach (var (path, hash) in before) Assert.Equal(hash, Hash(path));
    }

    private void AssertOwnedScratchEmpty()
    {
        foreach (var name in new[] { "load-staging", "load-transactions", "trusted-local-publication-v1" })
        {
            var path = Path.Combine(_files.RuntimeRootPath, name);
            if (Directory.Exists(path)) Assert.Empty(Directory.EnumerateFileSystemEntries(path));
        }
    }

    private void Report(LoadReplacementResult result)
    {
        _output.WriteLine("Disposition={0}; preparation={1}; lifecycle opens={2}; B1 phases={3}",
            result.Disposition, _prepared, _lifecycleOpen, string.Join(",", _phases));
        if (result.Failure != null) _output.WriteLine(result.Failure.ToString());
        foreach (var error in _logger.Errors) _output.WriteLine(error.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
