using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OriginalSpiritualPrefix_RealActionPointEffectPrecedesExchange(bool gain, bool bounded)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await SeedOriginalPrefixActionPointEffectAsync(context, gain, bounded);
        await WriteCompleteConflictFrameExchangeAsync(context);
        var candidate = await ReadSourceMissingCandidateAsync(context);
        var prefixValue = gain ? 5 : 4;
        candidate["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!["before"] = prefixValue;
        candidate["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!["after"] = prefixValue - 3;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath,
            OriginalCaptureDefinitionAndActionPointCommand(gain ? 2m : 1m, 0m).ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var step = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(step.Issues);
        if (bounded)
        {
            Assert.Empty(source.Sources);
            var wait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualResourceExchange>(step.Step!.PendingResource);
            var packet = capture.ReadPendingResourceRequest(lease, wait);
            var request = Assert.Single(packet["requests"]!.AsArray())!;
            var accepted = await capture.ResumePendingResourceAsync(lease, wait, new JsonArray(new JsonObject
            {
                ["requestId"] = request["requestId"]!.GetValue<string>(),
                ["resultKind"] = "resource_delta", ["amount"] = 1,
                ["reason"] = "Exact source-owned AP effect"
            }));
            AssertNoConflictFrameErrors(accepted.Issues);
            Assert.NotNull(accepted.Step!.OriginalPrefix);
            step = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(step.Issues);
        }
        Assert.NotNull(step.Step!.Interval);
        Assert.True(source.Owns(Assert.Single(source.Sources)));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        var final = resources.Drain();
        Assert.True(final.IsValid, string.Join(Environment.NewLine, final.Issues));
        var transitions = final.AppliedTransitions.Where(value => value.Coordinate.ResourceOwnerId == "player_soul" &&
            value.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(gain ? new[] { 6m, 4m, 5m } : new[] { 6m, 5m, 4m }, transitions.Select(value => value.BeforeState!.Current));
        Assert.Equal(gain ? new[] { 4m, 5m, 2m } : new[] { 5m, 4m, 1m }, transitions.Select(value => value.AfterState!.Current));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Seeds a source-owned afterlife effect that changes action points once after a resource spend.
    /// </summary>
    /// <param name="context">
    /// Signed-turn fixture receiving the profile, exact carrier identity and replacement baseline snapshot.
    /// </param>
    /// <param name="gain">
    /// True creates a periodic gain; false creates a periodic spend.
    /// </param>
    /// <param name="bounded">
    /// True requires a resource receipt; false resolves the component deterministically.
    /// </param>
    /// <returns>
    /// A task completing after all fixture images and the validated snapshot are written.
    /// </returns>
    private static async Task SeedOriginalPrefixActionPointEffectAsync(
        ResourceMaterializationTestContext context, bool gain, bool bounded)
    {
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var player = profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>()
            .Single(value => value["actorId"]!.GetValue<string>() == "player_soul");
        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_restore");
        definition["allowedRealms"] = new JsonArray("chaos_sea");
        definition["allowedTargetKinds"] = new JsonArray("player");
        var component = definition["components"]![0]!;
        component["profile"] = gain ? "periodic_gain" : "periodic_spend";
        component["payload"] = new JsonObject
        {
            ["resource"] = "spiritual_action_points", ["amount"] = 1,
            [gain ? "capPolicy" : "floorPolicy"] = gain ? "registered_resource_cap" : "registered_resource_floor"
        };
        definition["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "on_ap_spent", ["eventType"] = "resource_spent", ["priority"] = 100,
            ["componentIds"] = new JsonArray("component_001"), ["consumeUses"] = true,
            ["resolutionMode"] = bounded ? "bounded_receipt" : "deterministic"
        });
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses", ["initialUses"] = 1, ["consumingEventTypes"] = new JsonArray("resource_spent")
        };
        player["specialArts"] = new JsonArray(new JsonObject
        {
            ["artId"] = "art_prefix_ap", ["displayName"] = "Отзвук действия", ["effectDescription"] = "Меняет очки действий один раз.",
            ["owner"] = "player_soul", ["baseOperation"] = "guard", ["activeEffectDefinitions"] = new JsonArray(definition.DeepClone())
        });
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "periodic_restore");
        effect["realm"] = "chaos_sea";
        effect["target"]!["targetId"] = "player_soul";
        effect["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art", ["sourceId"] = "art_prefix_ap", ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses", ["remainingUses"] = 1, ["consumingTriggerIds"] = new JsonArray("on_ap_spent")
        };
        player["activeEffects"] = new JsonArray(effect);
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        var identityIndex = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        identityIndex["entries"]![0]!["owner"]!["carrierPath"] = AfterlifeEntityProfileState.StatePath;
        await context.WriteExactJsonAsync(EffectIdentityState.StatePath, identityIndex.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
    }
}
