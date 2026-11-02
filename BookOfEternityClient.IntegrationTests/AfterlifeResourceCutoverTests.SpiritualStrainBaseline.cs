using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Proves the ordinary signed baseline and rank-III player source before any new strain burden is selected.</summary>
    [Fact]
    public async Task SpiritualStrainBurden_PressureSetbackBaselineHasRealSourceAndHeadroom()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15, 4, 12]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!.AsObject();
        var first = active["exchangeLog"]![0]!.AsObject();
        first["outcome"] = "setback";
        first["after"]!["playerSideStrain"] = "overwhelmed";
        first["after"]!["oppositionSideStrain"] = "clear";
        SetDice(first, 5, 15);
        first["incomingAction"] = IncomingPressure();
        active["playerSideStrain"] = "overwhelmed";
        active["oppositionSideStrain"] = "clear";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());

        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        active = candidate["activeConflict"]!.AsObject();
        var second = active["exchangeLog"]![1]!.AsObject();
        Assert.Equal("contested", second["before"]!["conflictPosition"]!.GetValue<string>());
        second["outcome"] = "setback";
        second["after"] = second["before"]!.DeepClone();
        second["after"]!["conflictPosition"] = "opposition_advantaged";
        second["incomingAction"] = IncomingPressure();
        SetDice(second, 4, 12);
        active["playerSideStrain"] = "overwhelmed";
        active["oppositionSideStrain"] = "clear";
        active["conflictPosition"] = "opposition_advantaged";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var paths = new[] { AfterlifeSpiritualConflictState.StatePath, AfterlifeEntityProfileState.StatePath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath };
        var images = new List<byte[]?>();
        foreach (var path in paths) images.Add(await context.FileSystem.ReadFileBytesAsync(lease, path));
        var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(prepared.Issues);
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
        Assert.Empty(session.PendingRequirements);
        Assert.Equal(2, session.CheckedExchanges.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, session.ClaimedDice);
        var source = Assert.Single(session.Sources);
        Assert.True(session.Owns(source));
        Assert.Equal("player", source.AffectedSide);
        Assert.Equal(10L, source.Calculation.Input.HarmfulMargin);
        Assert.Equal(0, source.Calculation.Input.AppliedArtTier);
        Assert.Equal(0, source.Calculation.Input.TargetResilienceTier);
        Assert.Equal(20L, source.Calculation.TraumaPressure); // 10 + 0 + 4 + 6, destination cap III.
        Assert.Equal(3, source.Calculation.MaximumSeverityRank);
        var checkedSecond = JsonNode.Parse(session.CheckedExchanges[1])!;
        Assert.Equal("pressure", checkedSecond["operationType"]!.GetValue<string>());
        Assert.Equal("setback", checkedSecond["outcome"]!.GetValue<string>());
        Assert.Equal("overwhelmed", checkedSecond["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("overwhelmed", checkedSecond["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("opposition_advantaged", checkedSecond["after"]!["conflictPosition"]!.GetValue<string>());
        for (var i = 0; i < paths.Length; i++)
            Assert.Equal(images[i], await context.FileSystem.ReadFileBytesAsync(lease, paths[i]));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));

        static JsonObject IncomingPressure() => new()
        {
            ["operationType"] = "pressure", ["actorType"] = "guardian", ["actorId"] = "guardian_frame",
            ["summary"] = "Хранитель сохраняет настоящее встречное духовное давление."
        };

        static void SetDice(JsonObject exchange, int player, int opposition)
        {
            var audit = exchange["diceAudit"]!;
            audit["diceUsed"]![0]!["value"] = player;
            audit["diceUsed"]![1]!["value"] = opposition;
            audit["playerTotal"] = player;
            audit["oppositionTotal"] = opposition;
            audit["margin"] = player - opposition;
            audit["outcomeBand"] = "decisive_opposition_success";
        }
    }
}
