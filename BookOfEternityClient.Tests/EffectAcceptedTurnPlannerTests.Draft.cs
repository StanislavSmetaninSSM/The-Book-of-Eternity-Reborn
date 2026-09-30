using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlannerTests
{
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void DraftOwner_OrdinaryReplacementGolden(bool consumes, int uses)
    {
        var fixture = CreateDraftOwnerFixture(consumes: consumes, uses: uses);
        var result = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            fixture.Plan, fixture.Transcript, fixture.Factory);
        Assert.True(result.Success, IdentityOwnerIssues(result));
        var plan = result.Plan!;
        var old = DraftOwnerEntry(plan.IdentityIndexAfterImage, fixture.OldId);
        var child = Assert.Single(plan.ActiveEffects);
        var childId = child["effectId"]!.GetValue<string>();
        Assert.NotEqual(fixture.OldId, childId);
        Assert.Equal(new[] { "effect_identity_owner_1" }, plan.AllocatedEffectIds);
        Assert.Equal(consumes
            ? new[] { "effect", "transition", "transition", "transition" }
            : new[] { "transition", "effect", "transition", "transition" }, fixture.Factory.Kinds);
        Assert.Equal(consumes ? new[] { "consume", "replace" } : new[] { "trigger", "replace" },
            old["transitions"]!.AsArray().TakeLast(2)
                .Select(value => value!["kind"]!.GetValue<string>()));
        Assert.Equal("replaced", old["state"]!.GetValue<string>());
        Assert.Equal("active", DraftOwnerEntry(plan.IdentityIndexAfterImage, childId)["state"]!.GetValue<string>());
        Assert.True(plan.IsAcceptedBoundaryComplete);
        Assert.Empty(plan.DeferredReactions);
        PrintDraftOwnerGolden("replace/" + consumes + "/" + uses, plan, fixture.Factory);
    }

    [Theory]
    [InlineData("stack", 1, "stack", 2, 3)]
    [InlineData("stack", 3, "no_change", 3, 3)]
    [InlineData("refresh", 1, "refresh", 1, 3)]
    [InlineData("merge", 1, "merge", 2, 6)]
    public void DraftOwner_OrdinaryNonCreateGolden(
        string policy, int stacks, string disposition, int expectedStacks, int expectedAmount)
    {
        var fixture = CreateDraftOwnerFixture(nonCreatePolicy: policy, incumbentStacks: stacks);
        var result = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            fixture.Plan, fixture.Transcript, fixture.Factory);
        Assert.True(result.Success, IdentityOwnerIssues(result));
        var plan = result.Plan!;
        Assert.Empty(plan.AllocatedEffectIds);
        Assert.Equal(new[] { "transition", "transition" }, fixture.Factory.Kinds);
        Assert.Equal(2, plan.ResourceTriggerCarriers.PlayerEffects!["activeEffects"]!.AsArray().Count);
        var incumbent = plan.ResourceTriggerCarriers.PlayerEffects!["activeEffects"]!.AsArray()
            .OfType<JsonObject>().Single(value => value["effectId"]!.GetValue<string>() == fixture.IncumbentId);
        Assert.Equal(expectedStacks, incumbent["stacking"]!["currentStacks"]!.GetValue<int>());
        Assert.Equal((double)expectedAmount, System.Text.Json.JsonSerializer.Deserialize<double>(
            incumbent["components"]![0]!["payload"]!["amount"]!.ToJsonString()));
        Assert.Equal(3, incumbent["lifetime"]!["remainingTurns"]!.GetValue<int>());
        var entry = DraftOwnerEntry(plan.IdentityIndexAfterImage, fixture.IncumbentId!);
        Assert.Equal(disposition == "no_change" ? "stack" : disposition,
            entry["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
        Assert.Equal(fixture.Factory.Ids[1],
            entry["transitions"]!.AsArray()[^1]!["transitionId"]!.GetValue<string>());
        Assert.Equal("active", entry["state"]!.GetValue<string>());
        Assert.Equal(new[] { "create", "trigger" },
            DraftOwnerEntry(plan.IdentityIndexAfterImage, fixture.OldId)["transitions"]!.AsArray()
                .Select(value => value!["kind"]!.GetValue<string>()));
        PrintDraftOwnerGolden(policy + "/" + stacks, plan, fixture.Factory);
    }

    [Fact]
    public void DraftOwner_ContractRedStartsWithRealReplacement()
    {
        var fixture = CreateDraftOwnerFixture(consumes: true);
        var ordinary = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            fixture.Plan, fixture.Transcript, fixture.Factory);
        Assert.True(ordinary.Success, IdentityOwnerIssues(ordinary));
        Assert.Equal(new[] { "effect", "transition", "transition", "transition" }, fixture.Factory.Kinds);
        var adapter = typeof(EffectAcceptedTurnPlanner).GetMethod(
            "CompleteAcceptedBoundaryTranscript", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Contains(DraftOwnerCalledMethods(adapter), method =>
            method.DeclaringType?.Name == "EffectAcceptedDraft" && method.Name == "Complete");
        var nested = typeof(EffectAcceptedTurnPlanner).GetNestedType(
            "EffectAcceptedDraft", BindingFlags.NonPublic);
        Assert.NotNull(nested);
        var begin = nested!.GetMethod("Begin", BindingFlags.NonPublic | BindingFlags.Static);
        var complete = nested.GetMethod("Complete", BindingFlags.NonPublic | BindingFlags.Instance);
        var read = nested.GetMethod("ReadIdentityIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        var phases = nested.GetProperty("Phases", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(begin);
        Assert.NotNull(complete);
        Assert.NotNull(read);
        Assert.NotNull(phases);
        using var draft = Assert.IsAssignableFrom<IDisposable>(
            begin!.Invoke(null, new object[] { fixture.Plan, new IdentityOwnerFactory() }));
        var result = Assert.IsType<EffectAcceptedTurnPlanningResult>(
            complete!.Invoke(draft, new object[] { fixture.Transcript }));
        Assert.True(result.Success, IdentityOwnerIssues(result));
        Assert.True(JsonNode.DeepEquals(ordinary.Plan!.IdentityIndexAfterImage,
            Assert.IsType<JsonObject>(read!.Invoke(draft, null))));
        Assert.Equal(6, Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            phases!.GetValue(draft)).Cast<object>().Count());
    }

    private sealed record DraftOwnerFixture(
        EffectAcceptedTurnPlan Plan,
        AcceptedEffectBoundaryTranscript Transcript,
        IdentityOwnerFactory Factory,
        string OldId,
        string? IncumbentId);

    private static DraftOwnerFixture CreateDraftOwnerFixture(
        bool consumes = false, int uses = 2,
        string? nonCreatePolicy = null, int incumbentStacks = 1,
        bool collideTransitions = false,
        bool malformedReplacementTransition = false, string? replacementTransitionId = null)
    {
        const string rootKey = "draft_owner_root";
        const string childKey = "draft_owner_child";
        const string childStackKey = "draft-owner-child";
        const string rootStackKey = "draft-owner-root";
        var root = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        root["definitionKey"] = rootKey;
        root["stacking"]!["stackKey"] = nonCreatePolicy == null ? childStackKey : rootStackKey;
        root["components"]![0]!["payload"]!["resultKind"] = "apply_definition";
        root["components"]![0]!["payload"]!["definitionKey"] = childKey;
        root["components"]![0]!["payload"]!["parameters"] = new JsonObject { ["amount"] = 3 };
        root["components"]![0]!["payload"]!["maxExpansion"] = 2;
        var child = EffectMaterializationTestFixture.CreateDefinition();
        child["definitionKey"] = childKey;
        child["stacking"]!["stackKey"] = childStackKey;
        child["stacking"]!["policy"] = nonCreatePolicy ?? "replace";
        child["stacking"]!["maxStacks"] = nonCreatePolicy is "stack" or "merge" ? 3 : 1;
        child["stacking"]!["atMaximum"] = "no_change";
        child["stacking"]!["refreshMode"] = nonCreatePolicy == "refresh" ? "reset" : null;
        child["stacking"]!["mergeRule"] = nonCreatePolicy == "merge" ? "sum" : null;
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "event_reaction");
        effect["source"]!["definitionKey"] = rootKey;
        effect["stacking"]!["stackKey"] = nonCreatePolicy == null ? childStackKey : rootStackKey;
        if (consumes)
        {
            root["triggers"]![0]!["consumeUses"] = true;
            root["lifetime"] = new JsonObject
            {
                ["mode"] = "uses", ["initialUses"] = uses,
                ["consumingEventTypes"] = new JsonArray("owner_damaged")
            };
            effect["lifetime"] = new JsonObject
            {
                ["mode"] = "uses", ["remainingUses"] = uses,
                ["consumingTriggerIds"] = new JsonArray("on_owner_damaged"),
                ["displayText"] = "Accepted uses remain"
            };
        }
        effect["components"] = root["components"]!.DeepClone();
        effect["triggers"] = root["triggers"]!.DeepClone();
        var input = CreateReactionInput(effect, root, child);
        string? incumbentId = null;
        if (nonCreatePolicy != null)
        {
            incumbentId = "effect_draft_owner_incumbent";
            var incumbent = EffectMaterializationTestFixture.CreateCanonicalEffect();
            incumbent["effectId"] = incumbentId;
            incumbent["source"]!["kind"] = "quest";
            incumbent["source"]!["sourceId"] = "quest_effect_reaction_test";
            incumbent["source"]!["definitionKey"] = childKey;
            incumbent["stacking"] = child["stacking"]!.DeepClone();
            incumbent["stacking"]!.AsObject().Remove("atMaximum");
            incumbent["stacking"]!["currentStacks"] = incumbentStacks;
            incumbent["chronology"]!["lastTransitionId"] = "effect_transition_draft_owner_incumbent";
            incumbent["chronology"]!["createdEventRef"] = "turn_42:draft_owner_incumbent_created";
            var identity = EffectMaterializationTestFixture.CreateIdentityIndex(effect, incumbent);
            identity["entries"]![1]!["transitions"]![0]!["transitionId"] =
                "effect_transition_draft_owner_incumbent";
            identity["entries"]![1]!["transitions"]![0]!["eventRef"] =
                "turn_42:draft_owner_incumbent_created";
            input = input with
            {
                PreTurnCarriers = new EffectCarrierCatalogInput(
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["activeEffects"] = new JsonArray(effect.DeepClone(), incumbent.DeepClone())
                    }, null, null, null, null, null),
                PreTurnIdentityIndex = identity
            };
        }
        var factory = new IdentityOwnerFactory(
            collideTransitions, malformedReplacementTransition, replacementTransitionId);
        var built = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(input);
        Assert.True(built.Success, IdentityOwnerIssues(built));
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(built.Plan);
        Assert.Empty(factory.Ids);
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            plan, ResourceOwnerAuthority.CreateCurrentPlayerAuthority(definitions), definitions);
        Assert.Empty(due.Issues);
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid);
        var sources = ResourceMutationSourceCatalog.Create(due.SourceExports);
        Assert.True(sources.IsValid);
        var resources = AcceptedMechanicsPlanner.BuildResources(new AcceptedMechanicsResourceInput(
            Turn: 42, Definitions: bootstrap.Definitions!, State: bootstrap.State!, History: bootstrap.History!,
            Sources: sources.Catalog!, Mutations: due.Mutations,
            InitialTriggerCandidates: due.TriggerCandidates, InitialEffectResolutionWork: due.Work,
            EffectPlanAuthority: AcceptedMechanicsPlanner.CreateEffectPlanAuthority(plan)),
            new AcceptedMechanicsIdentityFactory());
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        Assert.Single(resources.EffectBoundaryTranscript.AcceptedActivations);
        Assert.Single(resources.EffectBoundaryTranscript.ReleasedReactions);
        return new DraftOwnerFixture(plan, resources.EffectBoundaryTranscript, factory,
            effect["effectId"]!.GetValue<string>(), incumbentId);
    }

    private static JsonObject DraftOwnerEntry(JsonObject identity, string effectId) =>
        identity["entries"]!.AsArray().OfType<JsonObject>()
            .Single(value => value["effectId"]!.GetValue<string>() == effectId);

    private static void PrintDraftOwnerGolden(
        string scenario, EffectAcceptedTurnPlan plan, IdentityOwnerFactory factory) =>
        Console.WriteLine("DRAFT_OWNER_GOLDEN " + scenario + " " +
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan) + " " +
            string.Join(",", factory.Kinds.Zip(factory.Ids, (kind, id) => kind + ":" + id)));

    // Decode operands rather than searching raw token bytes or source text.
    private static IEnumerable<MethodBase> DraftOwnerCalledMethods(MethodInfo method)
    {
        var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .GroupBy(code => unchecked((ushort)code.Value))
            .ToDictionary(group => group.Key, group => group.First());
        var bytes = method.GetMethodBody()!.GetILAsByteArray()!;
        for (var offset = 0; offset < bytes.Length;)
        {
            ushort key = bytes[offset++];
            if (key == 0xfe)
                key = (ushort)(0xfe00 | bytes[offset++]);
            var code = codes[key];
            if (code.OperandType == OperandType.InlineMethod)
                yield return method.Module.ResolveMethod(BitConverter.ToInt32(bytes, offset))!;
            offset += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField or
                    OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or
                    OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => checked(4 + 4 * BitConverter.ToInt32(bytes, offset)),
                _ => throw new InvalidOperationException("Unexpected IL operand " + code.OperandType)
            };
        }
    }
}
