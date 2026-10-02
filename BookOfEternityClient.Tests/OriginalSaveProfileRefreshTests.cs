using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies retained physical publication receipts when profile refresh participates in an original writer.
/// </summary>
public sealed class OriginalSaveProfileRefreshTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "boe-original-save-profile-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Exercises the Windows original profile repair under a real lease and observation-only recorder.
    /// </summary>
    /// <returns>
    /// Completion after the exact physical receipt, repaired projection and untouched session evidence are verified.
    /// </returns>
    [Fact]
    public async Task WindowsOriginalRecorderRefreshRetainsPhysicalPublicationReceipt()
    {
        Assert.True(OperatingSystem.IsWindows(), "This control requires actual Windows physical receipt execution.");
        var ordinaryPublications = 0;
        var physicalPublications = new List<string>();
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (_, _) => ordinaryPublications++,
                AfterPhysicalFilePublishedAsync = path =>
                {
                    physicalPublications.Add(path);
                    return Task.CompletedTask;
                }
            });
        var state = PortableSaveFixture.Seed(files);
        var soulPath = files.ResolvePath("game_state/meta/soul_state.json");
        File.WriteAllText(soulPath,
            """{"soulName":"Пепельная Искра","currentRealm":"Chaos Sea","currentIncarnation":7,"inkFeathers":{"current":0,"total":0},"enlightenment":{"experience":0,"level":0},"afterlifeCombatProfile":{"spiritFocusTier":0,"artTiers":{"guard":0,"recover_spiritual_power":0}}}""",
            new UTF8Encoding(false));
        var profilePath = files.ResolvePath(AfterlifeEntityProfileState.StatePath);
        File.WriteAllText(profilePath,
            """{"schemaVersion":1,"profiles":[{"actorType":"player_soul","actorId":"player_soul","displayName":"Пепельная Искра","realm":"Chaos Sea","currencies":{"inkFeathers":4,"lightSparks":0},"progression":{"enlightenment":{"experience":0,"tier":0},"radiance":{"experience":0,"tier":0}},"standardArts":{"guard":1,"recover_spiritual_power":1},"progressionStrategy":{"strategyId":"strategy_player","priorityOrder":["guard"],"lastAutoProgressionCycleKey":"chaos:14"},"progressionLedger":[]}]}""",
            new UTF8Encoding(false));
        var beforeProfile = File.ReadAllBytes(profilePath);
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var sentinels = new[]
        {
            "game_state/meta/soul_state.json",
            "game_state/world/test_fixture_state.json",
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        }.ToDictionary(path => path, path => File.ReadAllBytes(files.ResolvePath(path)));
        var outsidePath = Path.Combine(_root, "outside-sentinel.bin");
        File.WriteAllBytes(outsidePath, [77, 0, 255]);
        var recorder = new PublicationRecorder();

        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            lease.MutationIntentRecorder = recorder;
            try
            {
                await state.RefreshGameStateAsync(lease);
            }
            finally
            {
                lease.MutationIntentRecorder = null;
            }
            Assert.Null(lease.MutationIntentRecorder);
        }

        var publication = Assert.Single(recorder.Publications);
        Assert.Equal(AfterlifeEntityProfileState.StatePath, publication.Path);
        Assert.Equal(CanonicalMutationOperation.Write, publication.Receipt.Operation);
        Assert.Equal(profilePath, Assert.Single(physicalPublications));
        Assert.Empty(recorder.NonPublications);
        var afterProfile = File.ReadAllBytes(profilePath);
        Assert.False(beforeProfile.AsSpan().SequenceEqual(afterProfile));
        var intent = Assert.Single(recorder.Intents);
        Assert.Equal(AfterlifeEntityProfileState.StatePath, intent.Path);
        Assert.Equal(afterProfile, intent.Desired);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(afterProfile)), publication.Receipt.Sha256);
        using (var handle = File.OpenHandle(profilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Equal(PhysicalFileAuthority.CaptureFileIdentity(handle, "original profile test result"),
                publication.Receipt.PhysicalIdentity);
        using var profile = JsonDocument.Parse(afterProfile);
        var player = Assert.Single(profile.RootElement.GetProperty("profiles").EnumerateArray());
        Assert.Equal(0, player.GetProperty("currencies").GetProperty("inkFeathers").GetInt32());
        Assert.Equal(0, player.GetProperty("standardArts").GetProperty("guard").GetInt32());
        Assert.Equal(0, player.GetProperty("standardArts").GetProperty("recover_spiritual_power").GetInt32());
        Assert.False(player.GetProperty("progressionStrategy").GetProperty("autoProgressionEnabled").GetBoolean());
        Assert.False(player.GetProperty("progressionStrategy").TryGetProperty("lastAutoProgressionCycleKey", out _));
        Assert.Equal("Chaos Sea", state.CurrentState.CurrentRealm);
        Assert.Equal(7, state.CurrentState.Incarnation);
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        foreach (var (path, bytes) in sentinels)
            Assert.Equal(bytes, File.ReadAllBytes(files.ResolvePath(path)));
        Assert.Equal(new byte[] { 77, 0, 255 }, File.ReadAllBytes(outsidePath));
        Assert.Equal(0, ordinaryPublications);
        Assert.False(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json")));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>
    /// Observes original writer callbacks without changing admission, mutation or receipt behavior.
    /// </summary>
    private sealed class PublicationRecorder : ICanonicalMutationIntentRecorder
    {
        /// <summary>
        /// Retains the requested destination bytes from actual mutation callbacks.
        /// </summary>
        internal List<(string Path, byte[]? Desired)> Intents { get; } = [];

        /// <summary>
        /// Retains actual publication receipts delivered by the original writer.
        /// </summary>
        internal List<(string Path, CanonicalMutationPublication Receipt)> Publications { get; } = [];

        /// <summary>
        /// Retains destinations reported as not published.
        /// </summary>
        internal List<string> NonPublications { get; } = [];

        /// <summary>
        /// Records the destination and detached bytes requested by the writer.
        /// </summary>
        /// <param name="relativePath">
        /// The canonical session-relative destination reported by the writer.
        /// </param>
        /// <param name="desiredContent">
        /// The requested after-image, or null for deletion.
        /// </param>
        /// <returns>
        /// Immediate completion after the observation is retained.
        /// </returns>
        public Task RecordMutationIntentAsync(string relativePath, byte[]? desiredContent)
        {
            Intents.Add((relativePath, desiredContent?.ToArray()));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Records a destination that the writer reports as not published.
        /// </summary>
        /// <param name="relativePath">
        /// The canonical session-relative destination whose publication did not complete.
        /// </param>
        /// <returns>
        /// Immediate completion after the observation is retained.
        /// </returns>
        public Task RecordMutationNonPublicationAsync(string relativePath)
        {
            NonPublications.Add(relativePath);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Retains the real physical publication receipt delivered by the writer.
        /// </summary>
        /// <param name="relativePath">
        /// The canonical session-relative destination published by the writer.
        /// </param>
        /// <param name="publication">
        /// The writer's confirmed physical identity and exact after-image hash.
        /// </param>
        /// <returns>
        /// Immediate completion after the receipt is retained.
        /// </returns>
        public Task RecordMutationPublicationAsync(string relativePath, CanonicalMutationPublication publication)
        {
            Publications.Add((relativePath, publication));
            return Task.CompletedTask;
        }
    }
}
