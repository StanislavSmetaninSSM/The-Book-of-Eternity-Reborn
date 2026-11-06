using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Authenticates both pressure DATA inputs once and rejects physical signed-artifact corruption without re-signing it.</summary>
    [Theory]
    [InlineData("receipt", "missing")]
    [InlineData("receipt", "hash")]
    [InlineData("history", "missing")]
    [InlineData("history", "hash")]
    public async Task ConflictFrame_PressureHistoryRequiresReadableDeclaredSignedArtifacts(string artifact, string corruption)
    {
        var probe = new ConflictFrameReadProbe();
        await using var context = await CreateCompleteConflictFrameContextAsync(probe.Hooks,
            seedOriginalInputs: SeedPressureHistoryFrameAsync,
            captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(
                turn: 45, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]));
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(FrameManifestPath));
        var receiptPath = manifest["files"]![SpiritualWoundOpportunityReceiptState.StatePath]!.GetValue<string>();
        var historyPath = manifest["files"]![WoundHistoryState.HistoryPath]!.GetValue<string>();
        probe.Start();
        ValidationService.SpiritualConflictValidationFrame control;
        try { control = await context.Validator.CaptureSpiritualConflictValidationFrameAsync(); }
        finally { probe.Stop(); }
        Assert.True(control.HasValidatedSnapshot);
        Assert.Equal(45, control.SnapshotTurnNumber);
        Assert.False(control.HistoricalStrainCaptureInvalid);
        Assert.True(Assert.IsType<SpiritualWoundOpportunityReceiptState>(control.HistoricalStrainReceipt).HasRetainedPressureComparisons);
        Assert.Equal(1, probe.Attempts(receiptPath));
        Assert.Equal(1, probe.Attempts(historyPath));
        var path = artifact == "receipt" ? receiptPath : historyPath;
        if (corruption == "missing") await context.DeleteAsync(path);
        else await context.WriteExactJsonAsync(path, "{}");
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot); // The detached manifest remains genuine and unchanged.
        Assert.True(frame.HistoricalStrainCaptureInvalid);
        Assert.Null(frame.HistoricalStrainReceipt);
        Assert.Contains(context.Validator.EvaluateSpiritualConflictValidationFrame(frame), issue =>
            issue.Code == "spiritual_wound_strain_parent_history_mismatch" && issue.Severity == IssueSeverity.Error);
        Assert.Equal(manifest.ToJsonString(), (await context.ReadJsonAsync(FrameManifestPath))!.ToJsonString());
    }

    /// <summary>Rejects malformed receipt or History bytes authenticated by the genuine signer, independently of hash corruption.</summary>
    [Theory]
    [InlineData("receipt")]
    [InlineData("history")]
    public async Task ConflictFrame_PressureHistoryRejectsSignedMalformedArtifacts(string artifact)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(seedOriginalInputs: async fixture =>
        {
            await SeedPressureHistoryFrameAsync(fixture);
            await fixture.WriteExactJsonAsync(artifact == "receipt" ?
                SpiritualWoundOpportunityReceiptState.StatePath : WoundHistoryState.HistoryPath,
                "{ malformed signed DATA");
        }, captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(
            turn: 45, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]));
        var manifest = (await context.ReadJsonAsync(FrameManifestPath))!;
        var path = artifact == "receipt" ? SpiritualWoundOpportunityReceiptState.StatePath : WoundHistoryState.HistoryPath;
        Assert.Equal(await context.FileSystem.ReadFileBytesAsync(path),
            await context.FileSystem.ReadFileBytesAsync(manifest["files"]![path]!.GetValue<string>()));
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        Assert.True(frame.HistoricalStrainCaptureInvalid);
        Assert.Null(frame.HistoricalStrainReceipt);
        Assert.Contains(context.Validator.EvaluateSpiritualConflictValidationFrame(frame), issue =>
            issue.Code == "spiritual_wound_strain_parent_history_mismatch");
    }

    /// <summary>Rejects authenticated but contradictory log or parent History joins, independently of signed-artifact integrity.</summary>
    [Theory]
    [InlineData("exchange-log")]
    [InlineData("parent-history")]
    public async Task ConflictFrame_PressureHistoryRejectsAuthenticatedJoinMismatch(string mismatch)
    {
        var seeded = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        await using var context = await CreateCompleteConflictFrameContextAsync(seedOriginalInputs: async fixture =>
        {
            await SeedPressureHistoryFrameAsync(fixture);
            var receiptRoot = (await fixture.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.AsObject();
            var source = receiptRoot["sources"]![0]!["witness"]!.AsObject();
            Assert.Equal(2, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var comparison).MaximumSeverityRank);
            Assert.Equal(3, Assert.IsType<SpiritualStrainPreimageData>(comparison).Version);
            var receipt = SpiritualWoundOpportunityReceiptState.Parse(receiptRoot.ToJsonString(), "fixture.receipt");
            Assert.True(receipt.IsValid, string.Join("\n", receipt.Issues));
            var state = Assert.IsType<SpiritualWoundOpportunityReceiptState>(receipt.State);
            var historyRoot = (await fixture.ReadJsonAsync(WoundHistoryState.HistoryPath))!.AsObject();
            var history = WoundHistoryState.Parse(historyRoot.ToJsonString(), WoundHistoryState.HistoryPath);
            Assert.True(history.IsValid, string.Join("\n", history.Issues));
            Assert.True(state.HasExactStrainHistory(Assert.IsType<WoundHistoryState>(history.State)));
            var conflictRoot = (await fixture.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
            var active = conflictRoot["activeConflict"]!.AsObject();
            Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
                state.MatchRetainedPressureHistory("chaos_sea", active, 45));

            // Change only one semantic join before signing. Every signed artifact remains readable and hash-valid.
            if (mismatch == "exchange-log")
            {
                active["exchangeLog"]!.AsArray().Clear();
                await fixture.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflictRoot.ToJsonString());
            }
            else
            {
                historyRoot["transitions"]![0]!["afterFingerprint"] = "sha256:" + new string('d', 64);
                Assert.True(WoundHistoryState.Parse(historyRoot.ToJsonString(), WoundHistoryState.HistoryPath).IsValid);
                await fixture.WriteExactJsonAsync(WoundHistoryState.HistoryPath, historyRoot.ToJsonString());
            }
            foreach (var path in new[] { AfterlifeSpiritualConflictState.StatePath,
                         SpiritualWoundOpportunityReceiptState.StatePath, WoundHistoryState.HistoryPath })
                seeded.Add(path, Assert.IsType<byte[]>(await fixture.FileSystem.ReadFileBytesAsync(path)));
        }, captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(
            turn: 45, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]));

        var manifest = (await context.ReadJsonAsync(FrameManifestPath))!;
        foreach (var (path, bytes) in seeded)
        {
            var snapshotPath = manifest["files"]![path]!.GetValue<string>();
            Assert.Equal(bytes, Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(snapshotPath)));
            Assert.Equal(bytes, Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(path)));
        }
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        Assert.True(frame.HistoricalStrainCaptureInvalid);
        Assert.Null(frame.HistoricalStrainReceipt);
        var issue = Assert.Single(context.Validator.EvaluateSpiritualConflictValidationFrame(frame)
            .Where(issue => issue.Code == "spiritual_wound_strain_parent_history_mismatch"));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.StatePath, issue.FilePath);
    }

    /// <summary>A declared receipt must be read even when the signed pressure log has no strain change or retained v3 comparison.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("hash")]
    [InlineData("malformed")]
    public async Task ConflictFrame_DeclaredOrdinaryReceiptCannotHideUnreadableArtifact(string corruption)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(seedOriginalInputs: fixture =>
            fixture.WriteExactJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath,
                corruption == "malformed" ? "{ invalid receipt" : EmptyPressureFrameReceipt().ToJsonString()));
        var manifest = (await context.ReadJsonAsync(FrameManifestPath))!;
        var path = manifest["files"]![SpiritualWoundOpportunityReceiptState.StatePath]!.GetValue<string>();
        if (corruption != "malformed")
        {
            var control = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
            Assert.True(control.HasValidatedSnapshot);
            Assert.False(control.HistoricalStrainCaptureInvalid);
            if (corruption == "missing") await context.DeleteAsync(path);
            else await context.WriteExactJsonAsync(path, "{}");
        }
        var frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync();
        Assert.True(frame.HasValidatedSnapshot);
        Assert.True(frame.HistoricalStrainCaptureInvalid);
        Assert.Null(frame.HistoricalStrainReceipt);
        Assert.Contains(context.Validator.EvaluateSpiritualConflictValidationFrame(frame), issue =>
            issue.Code == "spiritual_wound_strain_parent_history_mismatch");
    }

    /// <summary>Ordinary pressure with no declared receipt does not invent a comparison or read History.</summary>
    [Fact]
    public async Task ConflictFrame_OrdinaryAbsentReceiptNeedsNoHistory()
    {
        var probe = new ConflictFrameReadProbe();
        await using var context = await CreateCompleteConflictFrameContextAsync(probe.Hooks,
            playerCurrent: 3m, oppositionCurrent: 3m, seedOriginalInputs: async fixture =>
            {
                // Historical ordinary DATA is the subject here, not a new accepted resource publication.
                await WriteCompleteConflictFrameExchangeAsync(fixture);
                var root = (await fixture.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
                var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root,
                    root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject());
                projected.Remove(AfterlifeSpiritualConflictState.ResponseField);
                await fixture.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, projected.ToJsonString());
            }, captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(
                turn: 43, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]));
        var manifest = (await context.ReadJsonAsync(FrameManifestPath))!;
        Assert.Null(manifest["files"]![SpiritualWoundOpportunityReceiptState.StatePath]);
        Assert.False(manifest["originalPathPresenceV1"]![SpiritualWoundOpportunityReceiptState.StatePath]!.GetValue<bool>());
        var signedConflict = (await context.ReadJsonAsync(
            manifest["files"]![AfterlifeSpiritualConflictState.StatePath]!.GetValue<string>()))!;
        Assert.False(signedConflict.AsObject().ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        var exchange = Assert.IsType<JsonObject>(Assert.Single(signedConflict["activeConflict"]!["exchangeLog"]!.AsArray()));
        Assert.Equal("pressure", exchange["operationType"]!.GetValue<string>());
        Assert.Equal(42, exchange["turnNumber"]!.GetValue<int>());
        probe.Start();
        ValidationService.SpiritualConflictValidationFrame frame;
        try { frame = await context.Validator.CaptureSpiritualConflictValidationFrameAsync(); }
        finally { probe.Stop(); }
        Assert.True(frame.HasValidatedSnapshot);
        Assert.Equal(43, frame.SnapshotTurnNumber);
        Assert.False(frame.HistoricalStrainCaptureInvalid);
        Assert.Null(frame.HistoricalStrainReceipt);
        Assert.DoesNotContain(probe.Events, row => row.Path.EndsWith(WoundHistoryState.HistoryPath, StringComparison.Ordinal));
        AssertNoConflictFrameErrors(context.Validator.EvaluateSpiritualConflictValidationFrame(frame));
    }

    private static async Task SeedPressureHistoryFrameAsync(ResourceMaterializationTestContext context)
    {
        // These are immutable fixture DATA signed by the real existing fixture signer below.
        // They never represent a current wound owner or authenticate a live harm source.
        var source = SpiritualExistingPressureTestData.CreateData();
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        var (receipt, parent) = SpiritualExistingPressureTestData.CreateLedger(source);
        var history = SpiritualExistingPressureTestData.CreateHistory(parent);
        Assert.True(SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), "fixture.receipt").IsValid);
        Assert.True(WoundHistoryState.Parse(history.ToJsonString(), "fixture.history").IsValid);
        var root = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
        var active = root["activeConflict"]!.AsObject();
        active["conflictId"] = source["conflictId"]!.DeepClone();
        var actual = SpiritualExistingPressureTestData.ReadActual(source);
        active["exchangeLog"] = new JsonArray(actual);
        foreach (var field in new[] { "playerSideStrain", "oppositionSideStrain", "conflictPosition" })
            active[field] = actual["after"]![field]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await context.WriteExactJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath, receipt.ToJsonString());
        await context.WriteExactJsonAsync(WoundHistoryState.HistoryPath, history.ToJsonString());
    }

    private static JsonObject EmptyPressureFrameReceipt() => new()
    {
        ["schemaVersion"] = 1, ["nextInstanceOrdinal"] = 1, ["nextClosureOrdinal"] = 1,
        ["nextSourceOrdinal"] = 1, ["nextDecisionOrdinal"] = 1,
        ["instances"] = new JsonArray(), ["closures"] = new JsonArray(),
        ["sources"] = new JsonArray(), ["decisions"] = new JsonArray()
    };
}
