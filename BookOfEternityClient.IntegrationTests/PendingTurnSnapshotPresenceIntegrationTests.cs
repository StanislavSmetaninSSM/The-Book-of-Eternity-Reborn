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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResourceFixture_ObservationPreservesRollbackMembership(
        bool explicitlyTrackSettings)
    {
        await using var context = await CreateSeededContextAsync();
        var settingsBytes = Encoding.UTF8.GetBytes("""{"difficulty":"captured"}""");
        await context.WriteExactBytesAsync(SettingsPath, settingsBytes);
        var baselinePaths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(ResourceMaterializationTestContext.AllResourcePaths)
            .Append(ResourceMaterializationTestContext.FullPartyInteractionsPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(SettingsPath, baselinePaths);
        if (explicitlyTrackSettings)
            baselinePaths.Add(SettingsPath);
        var expectedBaseline = baselinePaths
            .Where(path => context.FileSystem.FileExists(path))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: explicitlyTrackSettings ? [SettingsPath] : null);

        var manifest = await ReadManifestAsync(context);
        Assert.True(manifest["originalPathPresenceV1"]![SettingsPath]!.GetValue<bool>());
        Assert.NotNull(manifest["files"]![SettingsPath]);
        Assert.NotNull(manifest["snapshotFileHashes"]![SettingsPath]);
        Assert.Equal(expectedBaseline, manifest["rollbackBaselineFiles"]!.AsArray()
            .Select(static node => node!.GetValue<string>()).ToArray());
        Assert.Empty(manifest["rollbackBackups"]!.AsObject());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = ReadObserved(context, lease);
        Assert.True(result.Success);
        Assert.Equal(settingsBytes, result.Snapshot!.ReadRequiredBytes(SettingsPath));
    }

    /// <summary>
    /// Preserves the original presence of each private spiritual service root after physical edits.
    /// </summary>
    /// <param name="path">
    /// Exact client-owned service path observed by the signed snapshot.
    /// </param>
    /// <param name="originallyPresent">
    /// Whether the path existed when the original snapshot was created.
    /// </param>
    [Theory]
    [InlineData("game_state/control/spiritual_wound_capture_checkpoint.json", false)]
    [InlineData("game_state/control/spiritual_wound_capture_checkpoint.json", true)]
    [InlineData("game_state/control/pending_spiritual_wound_decisions.json", false)]
    [InlineData("game_state/control/pending_spiritual_wound_decisions.json", true)]
    [InlineData("game_state/wounds/spiritual_wound_opportunity_receipts.json", false)]
    [InlineData("game_state/wounds/spiritual_wound_opportunity_receipts.json", true)]
    public async Task SpiritualServiceRoot_ObservedOptionalSelectionPreservesOriginalImage(
        string path, bool originallyPresent)
    {
        await using var context = await CreateSeededContextAsync();
        var originalBytes = Encoding.UTF8.GetBytes("{\"original\":true}");
        if (originallyPresent)
            await context.WriteExactBytesAsync(path, originalBytes);
        await PrepareLiveAsync(context);

        var manifest = await ReadManifestAsync(context);
        Assert.Equal(originallyPresent,
            manifest["originalPathPresenceV1"]![path]!.GetValue<bool>());
        await context.WriteExactBytesAsync(path, Encoding.UTF8.GetBytes("{\"later\":true}"));

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(RequiredPaths, [path]));

        Assert.True(result.Success);
        Assert.NotNull(result.Snapshot);
        if (originallyPresent)
        {
            Assert.Equal(originalBytes, result.Snapshot.ReadRequiredBytes(path));
            Assert.DoesNotContain(path, result.Snapshot.AbsentLogicalPaths);
        }
        else
        {
            Assert.Contains(path, result.Snapshot.AbsentLogicalPaths);
            Assert.DoesNotContain(path, result.Snapshot.CoveredLogicalPaths);
        }
    }

    /// <summary>
    /// Keeps an earlier presence map usable for ordinary reads while rejecting an unobserved
    /// spiritual service root required by named original capture.
    /// </summary>
    [Fact]
    public async Task LegacyPresenceMap_OrdinaryReadSucceedsButMissingSpiritualObservationFails()
    {
        await using var context = await CreateSeededContextAsync();
        await PrepareLiveAsync(context);
        var manifest = await ReadManifestAsync(context);
        var observations = manifest["originalPathPresenceV1"]!.AsObject();
        Assert.True(observations.Remove(SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.True(observations.Remove(SpiritualWoundDecisionPendingState.StatePath));
        Assert.True(observations.Remove(SpiritualWoundOpportunityReceiptState.StatePath));
        await ResignAsync(context, manifest);

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var ordinary = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease, [SoulPath]);
        Assert.True(ordinary.Success);

        var spiritual = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                [SoulPath],
                [SpiritualWoundCaptureCheckpointState.StatePath,
                 SpiritualWoundDecisionPendingState.StatePath]));
        Assert.False(spiritual.Success);
        Assert.Contains(spiritual.Issues, issue =>
            issue.Code == "pending_turn_snapshot_reader_absence_unproven" &&
            issue.FilePath == SpiritualWoundCaptureCheckpointState.StatePath);

        var namedCapture = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        Assert.Null(namedCapture.Capture);
        Assert.Contains(namedCapture.Issues, issue =>
            issue.Code == "pending_turn_snapshot_reader_absence_unproven" &&
            issue.FilePath == SpiritualWoundCaptureCheckpointState.StatePath);
    }

    /// <summary>
    /// Keeps an eighteen-path signed manifest readable while requiring proof of receipt absence.
    /// </summary>
    /// <param name="originallyPresent">
    /// Whether the older manifest contains exact signed receipt bytes despite lacking its presence key.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousPresenceMap_ReceiptRequiresCoverageOrSignedObservation(
        bool originallyPresent)
    {
        await using var context = await CreateSeededContextAsync();
        const string receiptPath = SpiritualWoundOpportunityReceiptState.StatePath;
        var originalBytes = Encoding.UTF8.GetBytes("{\"schemaVersion\":1}");
        if (originallyPresent)
            await context.WriteExactBytesAsync(receiptPath, originalBytes);
        await PrepareLiveAsync(context);
        var manifest = await ReadManifestAsync(context);
        Assert.True(manifest["originalPathPresenceV1"]!.AsObject().Remove(receiptPath));
        await ResignAsync(context, manifest);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var ordinary = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            [SoulPath]);
        Assert.True(ordinary.Success);
        var receipt = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                [SoulPath], [receiptPath]));
        if (originallyPresent)
        {
            Assert.True(receipt.Success);
            Assert.Equal(originalBytes, receipt.Snapshot!.ReadRequiredBytes(receiptPath));
        }
        else
        {
            Assert.False(receipt.Success);
            Assert.Contains(receipt.Issues, issue =>
                issue.Code == "pending_turn_snapshot_reader_absence_unproven" &&
                issue.FilePath == receiptPath);
        }
    }

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
        var extraRequired = Enumerable.Range(0,
                64 - PendingTurnSnapshotPathPresenceV1.LogicalPaths.Count)
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
