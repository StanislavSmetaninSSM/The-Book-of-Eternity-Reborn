using System.IO.Compression;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

#if DEBUG
public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>Separates actual Save/Load commitment from safe console continuation after a genuine accepted birth.</summary>
    [Fact]
    public Task CombinedMortalBirth_CleanSaveLoadPreservesCommittedContinuation()
        => RunCombinedMortalBirthAsync(false, null, validateLoadedContinuation: true);

    private async Task AssertCombinedBirthLoadedContinuationAsync(GameEngineTestRuntime runtime, string artifact, Func<int> gmCalls)
    {
        Assert.Equal(42, runtime.GameLoop.TurnNumber);
        Assert.Equal(1, gmCalls());
        Assert.False(await runtime.Engine.ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync());
        Assert.True(runtime.Engine.HasRetiredOrdinaryOriginal);
        var absent = new[] { LiveTurnPreparationService.TurnRequestPath,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
            "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
            "ready/turn_complete.json", "ready/turn_error.json" };
        foreach (var path in absent) Assert.False(_fs.FileExists(path), path);
        var retained = new Dictionary<string, byte[]>();
        foreach (var path in new[] { MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationIdentityState.StatePath, "game_state/world/current_location.json",
            "game_state/npcs/npc_core.json", StorageTransportMoveService.VehiclesPath,
            "stories/mortal_life_2.jsonl" })
            retained.Add(path, (await _fs.ReadFileBytesAsync(path))!);
        string oldGeneration;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            oldGeneration = _fs.ReadExistingSessionGeneration(lease)!;
        Assert.False(string.IsNullOrWhiteSpace(oldGeneration));
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(_fs.BasePath, out _));

        // Use the same local-settings bootstrap as the console fixture, before archiving the old generation.
        Assert.Equal(oldGeneration, await runtime.StateManager.BootstrapLocalStorageAsync());
        byte[] savedConfig;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Equal(oldGeneration, _fs.ReadExistingSessionGeneration(lease));
            savedConfig = (await runtime.StateManager.ReadLocalSettingsAsync(lease)).Bytes;
        }
        Assert.NotEmpty(savedConfig);
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(_fs.BasePath, out _));

        var saved = await runtime.SaveLoad.CreateSaveAsync("combined-mortal-clean", "Accepted canonical birth", turnNumber: 42);
        Assert.Equal(SaveCreationDisposition.Committed, saved.Disposition);
        Assert.False(saved.NeedsFollowUp, saved.Failure?.ToString());
        Assert.False(saved.ContinuationBlocked, saved.Failure?.ToString());
        Assert.NotNull(saved.DestinationRelativePath);
        var archivePath = _fs.ResolvePath(saved.DestinationRelativePath!);
        using (var archive = ZipFile.OpenRead(archivePath))
        {
            foreach (var pair in retained)
            {
                var entry = Assert.Single(archive.Entries, item => item.FullName == pair.Key);
                await using var stream = entry.Open();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                Assert.Equal(pair.Value, memory.ToArray());
                Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
            }
            var configEntry = Assert.Single(archive.Entries, item => item.FullName == "config.json");
            await using (var stream = configEntry.Open())
            {
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                Assert.Equal(savedConfig, memory.ToArray());
            }
            Assert.DoesNotContain(archive.Entries, entry => absent.Contains(entry.FullName) ||
                entry.FullName.StartsWith(LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/", StringComparison.Ordinal));
        }
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(_fs.BasePath, out _));
        LoadReplacementResult? committed = null, continued = null;
        try
        {
            using (var admission = _fs.BeginMainAdmission())
            {
                await admission.AcquireAsync(quiescentOnly: true);
                committed = await runtime.Engine.LoadSelectedSaveAndRebindRuntimeAsync(archivePath);
                Assert.Equal(LoadReplacementDisposition.Committed, committed.Disposition);
                Assert.False(committed.NeedsFollowUp, committed.Failure?.ToString());
                Assert.False(committed.ContinuationBlocked, committed.Failure?.ToString());
                Assert.Equal(archivePath, committed.SelectedSourcePath);
                Assert.False(string.IsNullOrWhiteSpace(committed.EstablishedGeneration));
                Assert.NotEqual(oldGeneration, committed.EstablishedGeneration);
                await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
                    Assert.Equal(committed.EstablishedGeneration, _fs.ReadExistingSessionGeneration(lease));
                Assert.Equal(42, runtime.GameLoop.TurnNumber);
                foreach (var pair in retained) Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
                foreach (var path in absent) Assert.False(_fs.FileExists(path), path);
                Assert.Equal(savedConfig, await _fs.ReadFileBytesAsync("config.json"));
                continued = await runtime.Engine.PrepareLoadedConsoleContinuationAsync(committed);
                // A refused continuation intentionally clears the game loop. Preserve the committed DATA decision instead.
                Assert.Equal(committed.Disposition, continued.Disposition);
                Assert.Equal(committed.SelectedSourcePath, continued.SelectedSourcePath);
                Assert.Equal(committed.EstablishedGeneration, continued.EstablishedGeneration);
                await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
                    Assert.Equal(committed.EstablishedGeneration, _fs.ReadExistingSessionGeneration(lease));
                foreach (var pair in retained) Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
                foreach (var path in absent) Assert.False(_fs.FileExists(path), path);
                Assert.Equal(1, gmCalls());
            }
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(artifact, "load-continuation-logger.txt"), _journeyLogger.Describe());
            object? Describe(LoadReplacementResult? result) => result is null ? null : new
            {
                result.Disposition, result.SelectedSourcePath, result.EstablishedGeneration,
                result.NeedsFollowUp, result.ContinuationBlocked, Failure = result.Failure?.ToString()
            };
            await File.WriteAllTextAsync(Path.Combine(artifact, "load-continuation-results.json"),
                JsonSerializer.Serialize(new { OldGeneration = oldGeneration, Archive = archivePath,
                    Saved = new { saved.Disposition, saved.NeedsFollowUp, saved.ContinuationBlocked, saved.DestinationRelativePath },
                    Committed = Describe(committed), Continued = Describe(continued), GmCalls = gmCalls() },
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        // This is the sole desired RED boundary, after actual commit and quiescent admission closure.
        Assert.True(continued is { NeedsFollowUp: false, ContinuationBlocked: false },
            "Committed canonical birth cannot continue after Load: " + continued?.Failure + "\n" +
            _journeyLogger.Describe() + "; evidence: " + artifact);
    }
}
#endif
