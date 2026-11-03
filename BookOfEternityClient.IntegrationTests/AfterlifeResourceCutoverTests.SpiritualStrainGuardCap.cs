using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Proves the ordinary failed guard and its actual second harm source before selecting a burden.</summary>
    [Fact]
    public async Task SpiritualStrainBurden_FailedGuardOrdinaryCapHasTwoRealSources()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15, 4, 12]);
        await WriteSpiritualStrainGuardCapDraftAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var paths = new[] { AfterlifeSpiritualConflictState.StatePath, AfterlifeEntityProfileState.StatePath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
            WoundIdentityState.StatePath, WoundHistoryState.HistoryPath };
        var before = new List<byte[]?>();
        foreach (var path in paths) before.Add(await context.FileSystem.ReadFileBytesAsync(lease, path));
        var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(prepared.Issues);
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
        Assert.Empty(session.PendingRequirements);
        Assert.Equal(new[] { 0, 1, 2, 3 }, session.ClaimedDice);
        Assert.Equal(2, session.CheckedExchanges.Count);
        Assert.Equal(2, session.Sources.Count);
        Assert.All(session.Sources, source => { Assert.True(session.Owns(source)); Assert.Equal("player", source.AffectedSide); });
        Assert.Equal(3, session.Sources[0].Calculation.MaximumSeverityRank);
        Assert.Equal(2, session.Sources[1].Calculation.MaximumSeverityRank);
        Assert.Equal(8L, session.Sources[1].Calculation.Input.HarmfulMargin);
        AssertGuardCapExchange(JsonNode.Parse(session.CheckedExchanges[1])!);
        for (var i = 0; i < paths.Length; i++)
            Assert.Equal(before[i], await context.FileSystem.ReadFileBytesAsync(lease, paths[i]));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>Requires a capped burden to preserve the independently proved guard source through cold C2 and common C4.</summary>
    [Fact]
    public async Task SpiritualStrainBurden_CappedGuardKeepsOrdinarySourceAndPublishesCold()
    {
        const string scene = "Чужое давление надломило волю души. Последующая защита не выдержала, и напряжение достигло предела.";
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15, 4, 12]);
        await WriteSpiritualStrainGuardCapDraftAsync(context);
        var ordinary = (await ReadProjectedSourceContinuationCandidateAsync(context))["activeConflict"]!["exchangeLog"]![1]!.DeepClone();
        await context.WriteExactJsonAsync(ProjectionNarrativePath,
            new JsonObject { ["response"] = scene, ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var preserved = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            foreach (var path in new[] { ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                         WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
                         SpiritualWoundOpportunityReceiptState.StatePath, AfterlifeEntityProfileState.StatePath })
                preserved[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
            await CommitSpiritualContinuationDraftFirstPairAsync(context, lease);
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            Assert.Equal("offer", opened.Disposition);
            using var first = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            Assert.Equal(3, first.Offer!.MaximumSeverityRank);
            var decision = CreateSpiritualStrainThirdDegreeDecision(first.Offer.OpportunityRef);
            var consequence = decision["proposal"]!["consequenceDefinitions"]![0]!;
            consequence["definition"]!["components"]![0]!["payload"]!["operation"] = "guard";
            consequence["definition"]!["components"]![2]!["payload"]!["operation"] = "pressure";
            consequence["root"]!["slots"]![0]!["readableSummary"] = "Неудачная защита сильнее напрягает душу, не превышая надлома.";
            consequence["root"]!["slots"]![2]!["readableSummary"] = "Давление требует больше духовных сил.";
            var selected = await first.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision), scene);
            using var selectedSession = selected.Session;
            foreach (var (path, bytes) in preserved)
                Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            AssertNoConflictFrameErrors(selected.Issues);
            Assert.Equal("offer", selected.Disposition); // No editable correction exists above the existing broken cap.
            selectedSession?.Dispose();
            var cold = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
            var restored = await cold.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(restored.Issues);
            Assert.Equal("offer", restored.Disposition);
            using var second = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(restored.Session);
            Assert.Equal(2, second.Offer!.MaximumSeverityRank);
            var declined = await second.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
                { opportunityRef = second.Offer.OpportunityRef, decision = "none" }), null);
            using var completed = declined.Session;
            AssertNoConflictFrameErrors(declined.Issues);
            Assert.Equal("completed_unpublished", declined.Disposition);
            foreach (var (path, bytes) in preserved)
                Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority authority;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out authority));
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        var output = Assert.IsType<SpiritualWoundPublishedOutput>(published.SpiritualWoundOutput);
        Assert.Single(output.Notifications);
        await output.RequireCurrentAsync(context.FileSystem);
        var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!;
        Assert.Equal(2, conflict["exchangeLog"]!.AsArray().Count);
        Assert.True(JsonNode.DeepEquals(ordinary, conflict["exchangeLog"]![1]));
        AssertGuardCapExchange(conflict["exchangeLog"]![1]!);
        Assert.Equal("broken", conflict["playerSideStrain"]!.GetValue<string>());
        var receiptRoot = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
        Assert.Equal(2, receiptRoot["sources"]!.AsArray().Count);
        Assert.Equal(2, receiptRoot["decisions"]!.AsArray().Count);
        Assert.Equal("materialize", receiptRoot["decisions"]![0]!["decision"]!.GetValue<string>());
        Assert.Equal(3, receiptRoot["decisions"]![0]!["selectedSeverityRank"]!.GetValue<int>());
        Assert.Equal("none", receiptRoot["decisions"]![1]!["decision"]!.GetValue<string>());
        var receipts = SpiritualWoundOpportunityReceiptState.Parse(receiptRoot.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(receipts.IsValid, string.Join("\n", receipts.Issues));
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.True(receipts.State!.HasExactStrainHistory(history.State!));
        var planned = Assert.IsType<AcceptedMechanicsPlan>(published.MechanicsPlan);
        var retained = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in planned.TouchedPaths.Concat(planned.ConsumedPaths).Distinct(StringComparer.Ordinal))
            retained[path] = await context.FileSystem.ReadFileBytesAsync(path);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, authority, out _));
        foreach (var (path, bytes) in retained) Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(path));
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8], requestId: "request_strain_guard_history_43");
        AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
    }

    private static async Task WriteSpiritualStrainGuardCapDraftAsync(ResourceMaterializationTestContext context)
    {
        await WriteSpiritualStrainBaselineDraftAsync(context);
        var raw = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = raw["activeConflict"]!;
        var second = active["exchangeLog"]![1]!;
        second["exchangeAtTurn"] = 42;
        second["operationType"] = "guard";
        second["after"] = second["before"]!.DeepClone();
        second["after"]!["playerSideStrain"] = "broken";
        second["matchupAudit"]!["playerOperation"] = "guard";
        second["matchupAudit"]!["primaryResolutionLane"] = "guard";
        second["matchupAudit"]!["riskProfile"] = "safe_defense";
        second["matchupAudit"]!["matchupRationale"] = "Защита не выдерживает встречного давления, и обычное напряжение достигает предела.";
        var cost = second["actionCostAudit"]!["player"]!;
        cost["operationType"] = "guard";
        cost["baseCost"] = 2;
        cost["effectiveCost"] = 2;
        cost["after"] = 1;
        active["playerSideStrain"] = "broken";
        active["conflictPosition"] = second["after"]!["conflictPosition"]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, raw.ToJsonString());
    }

    private static void AssertGuardCapExchange(JsonNode exchange)
    {
        Assert.Equal("guard", exchange["operationType"]!.GetValue<string>());
        Assert.Equal("setback", exchange["outcome"]!.GetValue<string>());
        Assert.Equal("overwhelmed", exchange["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("broken", exchange["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(2, exchange["actionCostAudit"]!["player"]!["effectiveCost"]!.GetValue<int>());
        Assert.Equal(3, exchange["actionCostAudit"]!["player"]!["before"]!.GetValue<int>());
        Assert.Equal(1, exchange["actionCostAudit"]!["player"]!["after"]!.GetValue<int>());
        Assert.Equal(3, exchange["actionCostAudit"]!["opposition"]!["effectiveCost"]!.GetValue<int>());
        Assert.Equal(0, exchange["actionCostAudit"]!["opposition"]!["after"]!.GetValue<int>());
    }
}
