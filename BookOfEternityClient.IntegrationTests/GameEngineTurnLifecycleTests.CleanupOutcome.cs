using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupUnknown_OriginalRollbackAndUndispatchedPrepStopNextConsumer(bool undispatched)
    {
        using var ownedFixture = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var cut = new CleanupPublicationCut();
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, cut.Hooks);
        cut.Attach(files);
        var logger = new CleanupThrowingLogger<GameEngine>(cut);
        var engine = CreateGameEngine(fileSystem: files, logger: undispatched ? null : logger, configureSettings: s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; });
        await GetPrivateField<StateManager>(engine, "_stateManager").BootstrapLocalStorageAsync();
        const string tracked = "lore/current_world/world_setting.json";
        await files.WriteFileAtomicAsync(tracked, """{"worldName":"exact cleanup baseline"}""");
        var baseline = File.ReadAllBytes(files.ResolvePath(tracked));
        string generation;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync()) generation = files.GetOrCreateSessionGeneration(lease);
        var snapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "cleanup_outcome");
        var backups = Assert.IsAssignableFrom<Dictionary<string, string>>(snapshot.GetType().GetProperty("BackupFiles")!.GetValue(snapshot));
        Assert.NotEmpty(backups);
        var backupTargets = backups.Values.Select(files.ResolvePath).ToHashSet(StringComparer.Ordinal);
        var manifestPath = files.ResolvePath("game_state/control/pending_turn_snapshot.json");
        var authorityPath = files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath);
        byte[]? manifest = null, authority = null;
        if (undispatched)
        {
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", CreateSnapshotByteContractRequest("cleanup_unknown"), snapshot, "cleanup outcome fixture");
            manifest = File.ReadAllBytes(manifestPath); authority = File.ReadAllBytes(authorityPath);
            Assert.True(PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(await files.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath), out _));
        }
        else await files.WriteFileAtomicAsync(tracked, """{"worldName":"changed before exact restore"}""");
        byte[]? trackedAtCut = null;
        cut.Select = (path, member) => backupTargets.Contains(path) && !member.GetProperty("After").GetProperty("Exists").GetBoolean();
        cut.BeforeCut = () => trackedAtCut = File.ReadAllBytes(files.ResolvePath(tracked));
        cut.Armed = true;
        var failure = await Record.ExceptionAsync(() => undispatched
            ? InvokePrivateTaskAsync(engine, "CleanupUndispatchedTransitionPrepAsync", snapshot, false, true)
            : InvokePrivateTaskAsync(engine, "RestorePreTurnBackupForSessionAsync", snapshot, generation));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { undispatched, Failure = failure?.ToString(), DiagnosticThrows = logger.Throws, baseline, trackedAtCut,
            TrackedAfter = CleanupPublicationCut.ReadOptional(files.ResolvePath(tracked)), manifest, authority,
            ManifestAfter = CleanupPublicationCut.ReadOptional(manifestPath), AuthorityAfter = CleanupPublicationCut.ReadOptional(authorityPath), Cut = cut.Evidence() }));
        cut.AssertReachedAndStopped(); Assert.Same(cut.OriginalUncertainty, failure);
        Assert.Equal(baseline, trackedAtCut); Assert.Empty(cut.LaterReads);
        if (undispatched) { Assert.Equal(manifest, File.ReadAllBytes(manifestPath)); Assert.Equal(authority, File.ReadAllBytes(authorityPath)); }
    }

    [Fact]
    public async Task CleanupUnknown_OriginalTurnStagingDoesNotCompensateUncertainRequest()
    {
        using var ownedFixture = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var cut = new CleanupPublicationCut();
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, cut.Hooks);
        cut.Attach(files);
        var input = new NewGameCancelInput();
        var engine = CreateGameEngine(input, s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; }, fileSystem: files);
        await GetPrivateField<StateManager>(engine, "_stateManager").BootstrapLocalStorageAsync();
        var guardian = new SystemGuardianLibraryService(files, NullLogger<SystemGuardianLibraryService>.Instance)
            .BuildFreeformPendingGuardianCreationNode("Спокойный Хранитель маяка, который встречает душу на берегу Моря Хаоса.", "Пробная Душа");
        await InvokePrivateAsync<string>(engine, "InitializeChaosSea", "Пробная Душа", "Человеческий силуэт мягкого синего света.", guardian, null);
        Assert.False(await InvokePrivateAsync<bool>(engine, "WaitForGmResponse").WaitAsync(TimeSpan.FromSeconds(8)));
        var manifestPath = files.ResolvePath("game_state/control/pending_turn_snapshot.json");
        var authorityPath = files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath);
        byte[]? manifestAtCut = null, authorityAtCut = null;
        input.Arm(() => { }); // Safe original cancellation if the required publication cut is missed.
        cut.Select = (path, member) => path == files.ResolvePath("input/turn_request.json") && member.GetProperty("After").GetProperty("Exists").GetBoolean();
        cut.BeforeCut = () => { manifestAtCut = File.ReadAllBytes(manifestPath); authorityAtCut = File.ReadAllBytes(authorityPath); };
        cut.Armed = true;
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Я осматриваю берег Моря Хаоса."));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { Failure = failure?.ToString(), input.EscapeReads, manifestAtCut, authorityAtCut,
            ManifestAfter = CleanupPublicationCut.ReadOptional(manifestPath), AuthorityAfter = CleanupPublicationCut.ReadOptional(authorityPath), Cut = cut.Evidence() }));
        cut.AssertReachedAndStopped(); Assert.Same(cut.OriginalUncertainty, failure);
        Assert.Equal(0, input.EscapeReads); Assert.NotNull(manifestAtCut); Assert.NotNull(authorityAtCut);
        Assert.Equal(manifestAtCut, File.ReadAllBytes(manifestPath)); Assert.Equal(authorityAtCut, File.ReadAllBytes(authorityPath));
    }
}
