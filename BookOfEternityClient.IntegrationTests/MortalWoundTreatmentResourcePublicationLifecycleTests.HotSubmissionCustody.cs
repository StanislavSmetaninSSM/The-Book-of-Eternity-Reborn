using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    // T179-Pd.3b HS01: the missing production custody owner must fail only
    // AFTER the genuine Stage/repair/held-refusal and no-consequence oracles.
    [Fact]
    public Task GuaranteedTreatment_HotCustodyBindsActualPostOriginalSubmissionAndConsumedReady() =>
        RunJointCompletionControlAsync(nonzeroProgression: false, useLiveCaller: true,
            postOriginalAutomaticSubmission: true);

    private static readonly string[] HotJointInputPaths =
    [
        "game_state/player/status_changes.json", "game_state/player/experience.json",
        "game_state/core/player_status.json", "game_state/meta/soul_state.json",
        "game_state/misc/characteristics.json", "game_state/player/stat_points.json"
    ];

    // Detached fixture expectations only. No private issuer, accepted flag,
    // source frame, acknowledgment or publication capability is authored here.
    private sealed record HotJointSubmissionFixture(
        string Generation,
        Dictionary<string, byte[]?> OriginalInputs,
        HashSet<string> UncoveredOriginalPaths,
        Dictionary<string, byte[]?> CurrentInputs,
        Dictionary<string, byte[]> AuthoredPublicInputs);

    private async Task<HotJointSubmissionFixture> AuthorHotJointSubmissionAfterOriginalAsync(
        FileSystemManager.CanonicalWriteLease lease, PendingTurnSnapshotReadAuthority actualOriginal)
    {
        var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
            new PendingTurnSnapshotPathSelection(
                ["game_state/meta/soul_state.json"],
                HotJointInputPaths.Where(path => path != "game_state/meta/soul_state.json")));
        Assert.True(read.Success, DescribeValidationIssues(read.Issues));
        var original = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
        Assert.Equal(actualOriginal.SessionId, original.SessionId);
        Assert.Equal(actualOriginal.RequestId, original.RequestId);
        Assert.Equal(actualOriginal.SnapshotToken, original.SnapshotToken);
        Assert.Equal(43, original.TurnNumber);
        var originalInputs = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        var uncoveredOriginalPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in HotJointInputPaths)
        {
            if (original.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal))
                originalInputs.Add(path, original.ReadRequiredBytes(path).ToArray());
            else
            {
                // This genuine producer did not sign an absence row for this
                // path. Uncovered inventory is not pristine/absence authority.
                Assert.DoesNotContain(path, original.AbsentLogicalPaths);
                Assert.DoesNotContain(path, original.DeclaredOriginalLogicalPaths);
                originalInputs.Add(path, null);
                uncoveredOriginalPaths.Add(path);
            }
            Assert.Equal(originalInputs[path], await _fs.ReadFileBytesAsync(lease, path));
        }
        Assert.Equal(new[] { "game_state/player/status_changes.json" },
            uncoveredOriginalPaths.Order(StringComparer.Ordinal));
        Assert.Null(originalInputs["game_state/player/status_changes.json"]);
        var originalExperience = ParseJsonObjectBytes(Assert.IsType<byte[]>(originalInputs[HotJointInputPaths[1]]));
        Assert.Equal(1, originalExperience["level"]!.GetValue<int>());
        var points = ParseJsonObjectBytes(Assert.IsType<byte[]>(originalInputs[HotJointInputPaths[5]]));
        Assert.Equal(1, points["levelUpStatPointsAwardedThroughLevel"]!.GetValue<int>());
        if (originalInputs[HotJointInputPaths[0]] is { } originalChanges &&
            ParseJsonObjectBytes(originalChanges)["statsIncreased"] is { } originalStats)
            Assert.Empty(Assert.IsType<JsonArray>(originalStats));

        var changes = originalInputs[HotJointInputPaths[0]] is { } changesBytes
            ? ParseJsonObjectBytes(changesBytes) : new JsonObject();
        changes["statsIncreased"] = new JsonArray("strength");
        var experience = originalExperience.DeepClone().AsObject();
        experience["level"] = 2; experience["playerLevel"] = 2;
        experience["experience"] = 49; experience["currentExperience"] = 49;
        experience["totalExperience"] = 149; experience["experienceForNextLevel"] = 150;
        var player = ParseJsonObjectBytes(Assert.IsType<byte[]>(originalInputs[HotJointInputPaths[2]]));
        player["level"] = 2;
        var authored = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, value) in new[]
        {
            (HotJointInputPaths[0], changes), (HotJointInputPaths[1], experience), (HotJointInputPaths[2], player)
        })
        {
            await _fs.WriteFileAtomicAsync(lease, path, value.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            var bytes = Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, path));
            Assert.False(originalInputs[path] is { } old && old.AsSpan().SequenceEqual(bytes));
            authored.Add(path, bytes.ToArray());
        }
        var currentInputs = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in HotJointInputPaths)
        {
            currentInputs.Add(path, await _fs.ReadFileBytesAsync(lease, path));
            Assert.Equal(authored.TryGetValue(path, out var submitted) ? submitted : originalInputs[path], currentInputs[path]);
            if (originalInputs[path] is { } old)
                Assert.Equal(old, original.ReadRequiredBytes(path));
        }
        // Initial public completion follows these public writes. The existing
        // repair observer resubmits these same three inputs and writes Ready last.
        var complete = ParseJsonObjectBytes(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, "ready/turn_complete.json")));
        var modified = Assert.IsType<JsonArray>(complete["filesModified"]);
        foreach (var path in authored.Keys)
        {
            Assert.DoesNotContain(modified, value => value!.GetValue<string>() == path);
            modified.Add(path);
        }
        await _fs.WriteFileAtomicAsync(lease, "ready/turn_complete.json",
            complete.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var generation = Assert.IsType<string>(_fs.ReadLocalGenerationSnapshot(lease).Binding.Id);
        WritePhysicalInjuryDiagnostic("fr063_hot_custody_genuine_post_original_submission", new
        {
            ActualCaller = "ProcessPlayerTurn", original.SessionId, original.RequestId,
            original.SnapshotToken, original.TurnNumber, Generation = generation,
            OriginalInputs = originalInputs, UncoveredOriginalPaths = uncoveredOriginalPaths,
            OriginalAbsenceAuthorityClaimed = false, CurrentInputs = currentInputs,
            AuthoredPublicPaths = authored.Keys, StatAcknowledgmentAuthored = false,
            PrivateOriginOrAdmissionAuthored = false
        });
        return new(generation, originalInputs, uncoveredOriginalPaths, currentInputs, authored);
    }

    private async Task AssertHotJointPostOriginalCustodyAsync(GameEngine engine,
        PendingTurnSnapshotReadAuthority original, MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourceFinalization finalization, IReadOnlyDictionary<string, byte[]?> before,
        byte[] signedOriginalSchedule, byte[] signedOriginalQteRuntime, JsonObject heldCommand,
        IReadOnlyList<(string Path, string Raw, Dictionary<string, byte[]?> Images)> milestones,
        int repairReplies, bool entered, bool handled, Exception? failure, Exception? observerFailure,
        HotJointSubmissionFixture fixture)
    {
        var firstChanged = AcceptedMechanicsWoundCommonInputComposer.TreatmentOrdinaryGuardPaths
            .Concat(GameEngine.MortalJointInactiveTailPaths).Distinct(StringComparer.Ordinal)
            .First(fixture.AuthoredPublicInputs.ContainsKey);
        Assert.Equal("game_state/player/status_changes.json", firstChanged);
        Assert.Equal(new[] { firstChanged }, fixture.UncoveredOriginalPaths.Order(StringComparer.Ordinal));
        Assert.Null(fixture.OriginalInputs[firstChanged]);
        Assert.NotNull(fixture.CurrentInputs[firstChanged]);
        await AssertJointActiveTailRefusalAsync("post_original_stats_level", engine, original, request, finalization,
            before, signedOriginalSchedule, signedOriginalQteRuntime, heldCommand, milestones,
            repairReplies, entered, handled, failure, observerFailure,
            useLiveCaller: true, mechanicalChangedPath: firstChanged);

        // The existing owner must first have retained the exact submitted inputs,
        // original/request/G, full held union, turn42 and one correlated repair.
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Equal(fixture.Generation, _fs.ReadLocalGenerationSnapshot(lease).Binding.Id);
            foreach (var image in fixture.CurrentInputs)
                Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(lease, image.Key));
            Assert.Null(await _fs.ReadFileBytesAsync(lease, "game_state/control/validation_repair_ready.json"));
            Assert.Equal(before["input/turn_request.json"], await _fs.ReadFileBytesAsync(lease, "input/turn_request.json"));
            Assert.Equal(before[LiveTurnPreparationService.PendingTurnSnapshotManifestPath],
                await _fs.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            Assert.Equal(before[PendingTurnSnapshotAuthority.AuthorityPath],
                await _fs.ReadFileBytesAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath));
        }
        WritePhysicalInjuryDiagnostic("fr063_hot_custody_existing_owner_oracles_before_missing_result", new
        { ActualOriginalTurn = original.TurnNumber, ActualCurrentTurn = 42, fixture.Generation,
            HeldRequestFingerprint = request.RequestFingerprint, ExistingRefusalPath = firstChanged,
            RepairReplies = repairReplies, NewCommonPublication = false, ChangedInputsRetained = true });

        // Intended absent-feature RED: inspection only, after the actual owner
        // and physical facts above. There is no fallback that could qualify GREEN.
        var export = typeof(GameEngine).GetMethod("ExportMortalJointPublicationAttemptDiagnostic",
            BindingFlags.Instance | BindingFlags.NonPublic, binder: null, Type.EmptyTypes, modifiers: null);
        Assert.NotNull(export);
        var diagnostic = Assert.IsType<JsonObject>(export!.Invoke(engine, null));
        WritePhysicalInjuryDiagnostic("fr063_hot_custody_actual_attempt_diagnostic", diagnostic);
        Assert.Equal("ClosedNoNativeAttempt", diagnostic["closeDisposition"]!.GetValue<string>());
        Assert.False(diagnostic["delegated"]!.GetValue<bool>());
        Assert.False(diagnostic["captureCanOpen"]!.GetValue<bool>());
        Assert.True(diagnostic["liveCapabilitiesExpired"]!.GetValue<bool>());
        Assert.False(diagnostic["fullSemanticAdmission"]!.GetValue<bool>());
        Assert.False(diagnostic["hasS"]!.GetValue<bool>());
        Assert.False(diagnostic["hasB"]!.GetValue<bool>());
        Assert.False(diagnostic["hasM3"]!.GetValue<bool>());
        var binding = Assert.IsType<JsonObject>(diagnostic["binding"]);
        Assert.Equal(fixture.Generation, binding["generation"]!.GetValue<string>());
        Assert.Equal(original.SessionId, binding["sessionId"]!.GetValue<string>());
        Assert.Equal(original.RequestId, binding["requestId"]!.GetValue<string>());
        Assert.Equal(original.SnapshotToken, binding["snapshotToken"]!.GetValue<string>());
        Assert.Equal(43, binding["turn"]!.GetValue<int>());
        Assert.True(binding["actualStageCompleted"]!.GetValue<bool>());
        var custody = Assert.IsType<JsonObject>(diagnostic["custody"]);
        Assert.Equal("ConsumedReadyMatched", custody["status"]!.GetValue<string>());
        Assert.True(custody["readyObservedBeforeDelete"]!.GetValue<bool>());
        Assert.True(custody["readyDeleteReturned"]!.GetValue<bool>());
        Assert.True(custody["readyReleaseReturned"]!.GetValue<bool>());
        Assert.True(custody["currentSourceRecheckSucceeded"]!.GetValue<bool>());
        AssertHotCustodyImageRows(custody["originalInputs"], fixture.OriginalInputs, fixture.UncoveredOriginalPaths);
        AssertHotCustodyImageRows(custody["currentInputs"], fixture.CurrentInputs);
        AssertHotCustodyImageRows(custody["outputs"], new[]
            { "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" }
            .ToDictionary(path => path, path => before[path], StringComparer.Ordinal));

        var actualAttempt = GetPrivateField<GameEngine.MortalJointPublicationAttempt>(engine, "_mortalJointPublicationAttempt");
        var expiredLease = GetPrivateField<FileSystemManager.CanonicalWriteLease>(actualAttempt, "_lease");
        Assert.False(expiredLease.IsActive);
        Assert.Throws<InvalidDataException>(() => actualAttempt.RequireActualOwner(_fs, expiredLease));
        var expiredClose = AcceptedTurnAuthorityRegistry.CloseOwnedJointAttemptUnattempted(_fs, expiredLease, actualAttempt);
        Assert.Equal("UnavailableSourceFault", expiredClose.Disposition);
        Assert.Null(GetPrivateFieldValue<object?>(expiredClose, "_receipt"));
        Assert.Equal("ClosedNoNativeAttempt", Assert.IsType<JsonObject>(export.Invoke(engine, null))["closeDisposition"]!.GetValue<string>());

        // Diagnostics are detached DATA. Mutating an export cannot mutate the
        // retained source or turn it into a live lease/issuer/capture capability.
        var frozen = diagnostic.DeepClone();
        custody["currentInputs"]![0]!["base64"] = "AA==";
        diagnostic["hasS"] = true;
        Assert.True(JsonNode.DeepEquals(frozen, Assert.IsType<JsonObject>(export.Invoke(engine, null))));
    }

    private static void AssertHotCustodyImageRows(JsonNode? value, IReadOnlyDictionary<string, byte[]?> expected,
        IReadOnlySet<string>? uncoveredOriginalPaths = null)
    {
        var rows = Assert.IsType<JsonArray>(value);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), rows.Select(row => row!["path"]!.GetValue<string>()).Order(StringComparer.Ordinal));
        foreach (var row in rows)
        {
            var path = row!["path"]!.GetValue<string>();
            var bytes = expected[path];
            if (uncoveredOriginalPaths?.Contains(path) == true)
            {
                Assert.Equal("UncoveredInSignedInventory", row["sourceStatus"]!.GetValue<string>());
                Assert.Null(row["present"]);
                Assert.Null(row["base64"]);
                continue;
            }
            if (uncoveredOriginalPaths is not null)
                Assert.Equal("Present", row["sourceStatus"]!.GetValue<string>());
            Assert.Equal(bytes is not null, row["present"]!.GetValue<bool>());
            if (bytes is null) Assert.Null(row["base64"]);
            else Assert.Equal(bytes, Convert.FromBase64String(row["base64"]!.GetValue<string>()));
        }
    }
}
