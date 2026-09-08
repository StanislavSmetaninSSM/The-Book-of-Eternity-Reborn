using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class PendingTurnSnapshotPresenceIntegrationTests
{
    private const string SoulPath = "game_state/meta/soul_state.json";
    private const string SettingsPath = "game_state/core/game_settings.json";

    private static readonly string[] RequiredPaths =
    [
        SoulPath,
        ResourceMaterializationTestContext.DefinitionsPath,
        ResourceMaterializationTestContext.StatePath,
        ResourceMaterializationTestContext.HistoryPath,
        ResourceMaterializationTestContext.AuthorityPath
    ];
    private static readonly string[] OptionalPaths =
        PendingTurnSnapshotPathPresenceV1.LogicalPaths
            .Skip(RequiredPaths.Length)
            .ToArray();

    [Fact]
    public async Task LiveProducer_SignedAbsenceSurvivesLaterCurrentFileAndReaderUsesRealLease()
    {
        await using var context = await CreateSeededContextAsync();
        await PrepareLiveAsync(context);

        var captured = await ReadManifestAsync(context);
        Assert.False(captured["originalPathPresenceV1"]![SettingsPath]!.GetValue<bool>());
        Assert.Null(captured["files"]![SettingsPath]);

        await context.WriteExactJsonAsync(SettingsPath, """{"difficulty":"later-current"}""");
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = PendingTurnSnapshotReader.ReadCurrent(
            context.FileSystem,
            lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                RequiredPaths,
                OptionalPaths));

        Assert.True(result.Success);
        Assert.NotNull(result.Snapshot);
        Assert.Contains(SettingsPath, result.Snapshot!.AbsentLogicalPaths);
        Assert.DoesNotContain(SettingsPath, result.Snapshot.CoveredLogicalPaths);
        Assert.Throws<NotSupportedException>(
            () => ((ICollection<string>)result.Snapshot.AbsentLogicalPaths)
                .Add("game_state/forged.json"));
        Assert.Equal(
            PendingTurnSnapshotPathPresenceV1.LogicalPaths.Order(StringComparer.Ordinal),
            result.Snapshot.CoveredLogicalPaths
                .Concat(result.Snapshot.AbsentLogicalPaths)
                .Order(StringComparer.Ordinal));
        Assert.Throws<KeyNotFoundException>(
            () => result.Snapshot.ReadRequiredBytes(SettingsPath));
    }

    [Fact]
    public async Task BrowserProducer_SignedPresenceReturnsCapturedBytesWithoutChangingRollbackMembership()
    {
        await using var context = await CreateSeededContextAsync();
        var settingsBytes = Encoding.UTF8.GetBytes("""{"difficulty":"captured"}""");
        await context.WriteExactBytesAsync(SettingsPath, settingsBytes);
        const string rollbackPath =
            "game_state/control/pending_turn_snapshot/soul.browser.rollback.json";
        await context.WriteExactBytesAsync(
            rollbackPath,
            (await context.FileSystem.ReadFileBytesAsync(SoulPath))!);

        var stateManager = new StateManager(
            context.FileSystem,
            new GameSettings(),
            NullLogger<StateManager>.Instance);
        var queue = new BrowserAfterlifeTurnRequestQueue(context.FileSystem, stateManager);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            await queue.QueueDirectChaosSeaGachaAsync(
                lease,
                new LocalUiSessionLockOwner(
                    "presence-browser",
                    "test",
                    "presence-browser",
                    TimeSpan.FromMinutes(1)),
                "Observe original source presence.",
                new PendingTurnState { PreGeneratedDices1d20 = [11] },
                rollbackPath,
                "Chaos Sea");
        }

        var manifest = await ReadManifestAsync(context);
        Assert.True(manifest["originalPathPresenceV1"]![SettingsPath]!.GetValue<bool>());
        Assert.DoesNotContain(
            "game_state/control/spiritual_source_snapshot_coverage.json",
            manifest["rollbackBaselineFiles"]!.AsArray()
                .Select(static node => node!.GetValue<string>()));

        await context.WriteExactJsonAsync(SettingsPath, """{"difficulty":"later"}""");
        await using var readLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var read = PendingTurnSnapshotReader.ReadCurrent(
            context.FileSystem,
            readLease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                [SoulPath],
                [SettingsPath]));
        Assert.True(read.Success);
        Assert.Equal(settingsBytes, read.Snapshot!.ReadRequiredBytes(SettingsPath));
        Assert.DoesNotContain(SettingsPath, read.Snapshot.AbsentLogicalPaths);
    }

    [Fact]
    public async Task OldCurrentTurnManifest_UncoveredObservedOptionalFailsButLegacyOptionalStillSkips()
    {
        await using var context = await CreateSeededContextAsync();
        await PrepareLiveAsync(context);
        var manifest = await ReadManifestAsync(context);
        manifest.Remove("originalPathPresenceV1");
        await ResignAsync(context, manifest);

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var observed = PendingTurnSnapshotReader.ReadCurrent(
            context.FileSystem,
            lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                RequiredPaths,
                [SettingsPath]));
        Assert.False(observed.Success);
        Assert.Null(observed.Snapshot);
        Assert.Contains(
            observed.Issues,
            static issue => issue.Code == "pending_turn_snapshot_reader_absence_unproven");

        var legacy = PendingTurnSnapshotReader.ReadCurrent(
            context.FileSystem,
            lease,
            new PendingTurnSnapshotPathSelection(RequiredPaths, [SettingsPath]));
        Assert.True(legacy.Success);
        Assert.Empty(legacy.Snapshot!.AbsentLogicalPaths);
        Assert.DoesNotContain(SettingsPath, legacy.Snapshot.CoveredLogicalPaths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedPresenceWithOriginalDetachedAuthorityFailsClosed(bool rehashManifest)
    {
        await using var context = await CreateSeededContextAsync();
        await PrepareLiveAsync(context);
        var manifest = await ReadManifestAsync(context);
        manifest["originalPathPresenceV1"]![SettingsPath] = true;
        if (rehashManifest)
        {
            manifest["manifestPayloadHash"] = string.Empty;
            manifest["manifestPayloadHash"] =
                PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        }
        await context.WriteExactJsonAsync(
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            manifest.ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = ReadObserved(context, lease);

        Assert.False(result.Success);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code == "pending_turn_snapshot_reader_authority_invalid");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("unicode-confusable")]
    [InlineData("contradiction")]
    [InlineData("missing-coverage-contradiction")]
    public async Task ResignedPresenceShapeOrCoverageContradictionFailsClosed(string mutation)
    {
        await using var context = await CreateSeededContextAsync();
        await PrepareLiveAsync(context);
        var manifest = await ReadManifestAsync(context);
        var presence = manifest["originalPathPresenceV1"]!.AsObject();
        switch (mutation)
        {
            case "missing":
                presence.Remove(SettingsPath);
                break;
            case "extra":
                presence["game_state/meta/not_in_v1.json"] = false;
                break;
            case "unicode-confusable":
                presence["game_ѕtate/core/game_settings.json"] = false;
                break;
            case "contradiction":
                presence[SoulPath] = false;
                break;
            case "missing-coverage-contradiction":
                presence[SettingsPath] = true;
                break;
        }
        await ResignAsync(context, manifest);

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = ReadObserved(context, lease);

        Assert.False(result.Success);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code == "pending_turn_snapshot_reader_presence_invalid");
    }

    [Theory]
    [InlineData("files")]
    [InlineData("hashes")]
    public async Task ResignedFalseObservationWithOneSidedCoverageFailsClosed(
        string oneSidedCoverage)
    {
        await using var context = await CreateSeededContextAsync();
        await PrepareLiveAsync(context);
        var manifest = await ReadManifestAsync(context);
        if (oneSidedCoverage == "files")
        {
            manifest["files"]!.AsObject()[SettingsPath] =
                "game_state/control/pending_turn_snapshot/game_state/core/game_settings.json";
        }
        else
        {
            manifest["snapshotFileHashes"]!.AsObject()[SettingsPath] =
                new string('D', 64);
        }
        await ResignAsync(context, manifest);

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = ReadObserved(context, lease);

        Assert.False(result.Success);
        Assert.Contains(
            result.Issues,
            static issue =>
                issue.Code == "pending_turn_snapshot_reader_coverage_case_mismatch");
    }

    [Fact]
    public async Task ResignedNonBooleanPresenceFailsTypedManifestShape()
    {
        await using var context = await CreateSeededContextAsync();
        await PrepareLiveAsync(context);
        var manifest = await ReadManifestAsync(context);
        manifest["originalPathPresenceV1"]![SettingsPath] = "false";
        await ResignAsync(context, manifest);

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = ReadObserved(context, lease);

        Assert.False(result.Success);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code == "pending_turn_snapshot_reader_manifest_invalid");
    }

    [Fact]
    public async Task RawCaseConfusablePresenceKeyFailsBeforeAnyAbsenceAuthority()
    {
        await using var context = await CreateSeededContextAsync();
        await PrepareLiveAsync(context);
        var manifest = await ReadManifestAsync(context);
        manifest["originalPathPresenceV1"]!.AsObject()[SettingsPath.ToUpperInvariant()] = false;
        await context.WriteExactJsonAsync(
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            manifest.ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = ReadObserved(context, lease);

        Assert.False(result.Success);
        Assert.Null(result.Snapshot);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code == "pending_turn_snapshot_reader_manifest_invalid");
    }

    [Fact]
    public async Task RawDuplicatePresenceKeyFailsBeforeAnyAbsenceAuthority()
    {
        await using var context = await CreateSeededContextAsync();
        await PrepareLiveAsync(context);
        var manifest = await ReadManifestAsync(context);
        var json = manifest.ToJsonString();
        var row = $"\"{SettingsPath}\":false";
        Assert.Contains(row, json, StringComparison.Ordinal);
        json = json.Replace(row, $"{row},{row}", StringComparison.Ordinal);
        await context.WriteExactJsonAsync(
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            json);

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = ReadObserved(context, lease);

        Assert.False(result.Success);
        Assert.Null(result.Snapshot);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code == "pending_turn_snapshot_reader_manifest_invalid");
    }

    [Fact]
    public async Task ObservedSelection_RealSnapshotAcceptsSixtyFourAndRejectsSixtyFive()
    {
        await using var context = await CreateSeededContextAsync();
        var extraRequired = Enumerable.Range(0, 48)
            .Select(static index => $"game_state/test/presence_{index:D2}.json")
            .ToArray();
        foreach (var path in extraRequired)
            await context.WriteExactJsonAsync(path, """{}""");
        await PrepareLiveAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var sixtyFour = PendingTurnSnapshotReader.ReadCurrent(
            context.FileSystem,
            lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                RequiredPaths.Concat(extraRequired),
                OptionalPaths));
        Assert.True(sixtyFour.Success);
        Assert.Equal(
            64,
            sixtyFour.Snapshot!.CoveredLogicalPaths.Count +
            sixtyFour.Snapshot.AbsentLogicalPaths.Count);
        Assert.All(
            extraRequired,
            path => Assert.Contains(path, sixtyFour.Snapshot.CoveredLogicalPaths));

        var sixtyFive = PendingTurnSnapshotReader.ReadCurrent(
            context.FileSystem,
            lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                RequiredPaths.Concat(extraRequired).Append(
                    "game_state/test/presence_over_limit.json"),
                OptionalPaths));
        Assert.False(sixtyFive.Success);
        Assert.Contains(
            sixtyFive.Issues,
            static issue => issue.Code == "pending_turn_snapshot_reader_required_paths_invalid");
    }

    [Fact]
    public async Task RealmAutoRollback_PresenceMetadataRestoresExactForbiddenBytesWithoutMetadataArtifact()
    {
        const string forbiddenPath = "game_state/factions/faction_core.json";
        const string syntheticPath =
            "game_state/control/spiritual_source_snapshot_coverage.json";
        await using var context = await CreateSeededContextAsync();
        var before = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("""{"factions":[{"factionId":"before"}]}"""))
            .ToArray();
        await context.WriteExactBytesAsync(forbiddenPath, before);
        await context.CaptureValidatedPendingSnapshotAsync(
            currentRealm: "Chaos Sea",
            additionalTrackedPaths: [forbiddenPath]);
        var manifest = await ReadManifestAsync(context);
        Assert.NotNull(manifest["originalPathPresenceV1"]);
        Assert.Null(manifest["files"]![syntheticPath]);

        await context.WriteExactJsonAsync(
            forbiddenPath,
            """{"factions":[{"factionId":"wrong-realm"}]}""");
        var service = new RealmSegregationAutoRollbackService(
            context.FileSystem,
            NullLogger<RealmSegregationAutoRollbackService>.Instance);
        var rollback = await service.TryRollbackForbiddenRealmMutationsAsync(
            "Chaos Sea",
            [forbiddenPath],
            "presence metadata restore boundary");

        Assert.True(rollback.RolledBack);
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(forbiddenPath));
        Assert.False(context.FileSystem.FileExists(syntheticPath));
    }

    private static PendingTurnSnapshotReadResult ReadObserved(
        ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease) =>
        PendingTurnSnapshotReader.ReadCurrent(
            context.FileSystem,
            lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                RequiredPaths,
                [SettingsPath]));

    private static async Task<ResourceMaterializationTestContext> CreateSeededContextAsync()
    {
        var context = await ResourceMaterializationTestContext.CreateAsync();
        foreach (var path in RequiredPaths)
            await context.WriteExactJsonAsync(path, """{}""");
        return context;
    }

    private static Task PrepareLiveAsync(ResourceMaterializationTestContext context) =>
        new LiveTurnPreparationService(context.FileSystem).PrepareAsync(
            new LiveTurnPreparationOptions
            {
                SessionId = "presence-live",
                RequestId = "presence-live-request",
                TurnNumber = 42,
                CurrentRealm = "Mortal World",
                PlayerAction = "Observe original source presence.",
                PreGeneratedDices1d20 = [7]
            });

    private static async Task<JsonObject> ReadManifestAsync(
        ResourceMaterializationTestContext context) =>
        Assert.IsType<JsonObject>(
            await context.ReadJsonAsync(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath));

    private static async Task ResignAsync(
        ResourceMaterializationTestContext context,
        JsonObject manifest)
    {
        manifest["manifestPayloadHash"] = string.Empty;
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await context.WriteExactJsonAsync(
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(
            context.FileSystem);
    }
}
