using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameStateDataReuseTests
{
    /// <summary>
    /// Runs the actual control or realm phase against a tiny genuine snapshot and fresh fallback.
    /// </summary>
    /// <param name="realm">
    /// Whether to select RealmSegregation instead of ClientOwnedControlFiles.
    /// </param>
    /// <param name="invalidAuthority">
    /// Whether to replace the detached authority with malformed bytes before validation.
    /// </param>
    /// <returns>
    /// Completion after ordered issue parity, actual DATA reuse and changed-source detection.
    /// </returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SelectedControlPhasePreservesSourcesAndOrderedIssues(
        bool realm, bool invalidAuthority)
    {
        var phase = realm ? GameStateValidationPhase.RealmSegregation : GameStateValidationPhase.ClientOwnedControlFiles;
        using var cached = new Fixture(output);
        using var fresh = new Fixture(output, mutableReadHook: true);
        foreach (var fixture in new[] { cached, fresh })
        {
            await fixture.InitializeGenerationAsync();
            await PrepareControlSnapshotAsync(fixture);
            if (invalidAuthority) fixture.WriteRaw(PendingTurnSnapshotAuthority.AuthorityPath, "{broken");
            fixture.Recording = true;
        }
        var actual = await ValidateControlAsync(cached, phase);
        var expected = await ValidateControlAsync(fresh, phase);
        Assert.Equal(IssueImages(expected), IssueImages(actual));
        Assert.Equal(1, cached.TimingBegins("GameStateData.Phase"));
        Assert.Equal(0, fresh.TimingBegins("GameStateData.Phase"));
        if (!invalidAuthority)
        {
            Assert.DoesNotContain(actual, issue => issue.Code?.Contains("missing_validated") == true ||
                issue.Code == "client_owned_pending_snapshot_manifest_modified");
            Assert.True(cached.Reads("game_state/control/pending_turn_snapshot.json") <
                fresh.Reads("game_state/control/pending_turn_snapshot.json"));
        }
        else Assert.NotEmpty(actual);

        cached.WriteRaw(PendingTurnSnapshotAuthority.AuthorityPath, "{changed");
        var changed = await ValidateControlAsync(cached, phase);
        Assert.NotEmpty(changed);
        Assert.Equal(2, cached.TimingBegins("GameStateData.Phase"));
    }

    /// <summary>
    /// Executes the ordinary participating phase without constructing a game or granting a verdict.
    /// </summary>
    /// <param name="fixture">
    /// Independent real files and their passive source observations.
    /// </param>
    /// <param name="phase">
    /// The exact production phase to execute.
    /// </param>
    /// <returns>
    /// Original ordered issues after the actual validation entrypoint completes.
    /// </returns>
    private static Task<List<ValidationIssue>> ValidateControlAsync(Fixture fixture, GameStateValidationPhase phase) =>
        SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files,
            () => new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance).ValidateGameStateAsync(phase));

    /// <summary>
    /// Creates a two-source snapshot with genuine hashes, current request and detached authority.
    /// </summary>
    /// <param name="fixture">
    /// The exclusively owned file fixture receiving the immutable before-images.
    /// </param>
    /// <returns>
    /// Completion after the existing production authority builder publishes the fixture envelope.
    /// </returns>
    private static async Task PrepareControlSnapshotAsync(Fixture fixture)
    {
        var files = new JsonObject();
        var hashes = new JsonObject();
        var backups = new JsonObject();
        var baseline = new JsonArray();
        var cohort = ConsoleLocalTurnRollbackArtifacts.CreateRoot();
        var marker = cohort + "/" + ConsoleLocalTurnRollbackArtifacts.MarkerName;
        string generation;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            generation = fixture.Files.GetOrCreateSessionGeneration(lease);
        fixture.WriteRaw(marker, Encoding.UTF8.GetString(ConsoleLocalTurnRollbackArtifacts.CreateMarker(cohort, generation)));
        files[marker] = marker;
        hashes[marker] = PendingTurnSnapshotAuthority.ComputeSha256(File.ReadAllBytes(fixture.RawPath(marker)));
        foreach (var (path, content) in new[]
        {
            ("game_state/meta/soul_state.json", "{\"currentRealm\":\"Mortal World\"}"),
            ("game_state/guardians/guardian_state.json", "{\"guardians\":[]}")
        })
        {
            var snapshot = "game_state/control/pending_turn_snapshot/" + Path.GetFileName(path);
            var backup = cohort + "/" + Path.GetFileName(path) + ".rollback.json";
            fixture.WriteRaw(path, content);
            fixture.WriteRaw(snapshot, content);
            fixture.WriteRaw(backup, content);
            files[path] = snapshot;
            hashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(Encoding.UTF8.GetBytes(content));
            backups[path] = backup;
            baseline.Add(path);
        }
        var manifest = new JsonObject
        {
            ["sessionId"] = "session", ["requestId"] = "request", ["turnNumber"] = 42,
            ["requestTimestamp"] = "time", ["playerAction"] = "action",
            ["files"] = files, ["snapshotFileHashes"] = hashes,
            ["clientOwnedValidationHashes"] = new JsonObject(), ["rollbackBackups"] = backups,
            ["rollbackBaselineFiles"] = baseline, ["sourceLabel"] = "control source fixture", ["manifestPayloadHash"] = ""
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        fixture.WriteRaw("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        fixture.WriteRaw("input/turn_request.json",
            "{\"sessionId\":\"session\",\"requestId\":\"request\",\"turnNumber\":42,\"timestamp\":\"time\",\"playerAction\":\"action\"}");
        await PendingTurnSnapshotTestAuthority.WriteAuthorityForManifestAsync(fixture.Files, manifest);
    }
}
