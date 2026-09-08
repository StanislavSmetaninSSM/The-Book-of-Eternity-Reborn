using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlannerTests
{
    [Fact]
    public void IdentityHistoryOwner_ContractRedStartsWithRealConsumingReplacement()
    {
        var production = IdentityOwnerReplacement();
        Assert.True(production.Result.Success, IdentityOwnerIssues(production.Result));
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(production.Result.Plan);
        var old = plan.IdentityIndexAfterImage["entries"]!.AsArray().OfType<JsonObject>()
            .Single(value => value["effectId"]!.GetValue<string>() == production.OldId);
        Assert.Equal(new[] { "consume", "replace" },
            old["transitions"]!.AsArray().TakeLast(2).Select(value => value!["kind"]!.GetValue<string>()));
        Assert.Equal(new[] { "effect", "transition", "transition", "transition" }, production.Factory.Kinds);

        var type = typeof(EffectAcceptedTurnPlanner).Assembly.GetType(
            "BookOfEternityClient.Services.EffectIdentityHistoryOwner");
        Assert.NotNull(type); // First semantic RED only after old real production succeeds.
        var constructor = type!.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(JsonObject), typeof(EffectIdentityFactory) }, null);
        Assert.NotNull(constructor);
        using var owner = Assert.IsAssignableFrom<IDisposable>(
            constructor!.Invoke(new object[] { plan.IdentityIndexAfterImage, new IdentityOwnerFactory() }));
        var publish = type.GetMethod("Publish", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(publish);
        var image = Assert.IsType<JsonObject>(publish!.Invoke(owner, null));
        Assert.True(JsonNode.DeepEquals(plan.IdentityIndexAfterImage, image));
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void IdentityHistoryOwner_ProductionReplacementKeepsAllocationAndCanonicalOrder(
        bool consumes, int uses)
    {
        var first = IdentityOwnerReplacement(consumes, uses);
        var second = IdentityOwnerReplacement(consumes, uses);
        Assert.True(first.Result.Success, IdentityOwnerIssues(first.Result));
        Assert.True(second.Result.Success, IdentityOwnerIssues(second.Result));
        var plan = first.Result.Plan!;
        var other = second.Result.Plan!;
        Assert.Equal(plan.IdentityIndexAfterImage.ToJsonString(), other.IdentityIndexAfterImage.ToJsonString());
        Assert.Equal(plan.ResourceTriggerCarriers.PlayerEffects!.ToJsonString(),
            other.ResourceTriggerCarriers.PlayerEffects!.ToJsonString());
        Assert.Equal(plan.AllocatedEffectIds, other.AllocatedEffectIds);
        Assert.Equal(plan.AllocatedTransitionIds, other.AllocatedTransitionIds);
        Assert.Equal(first.Factory.Ids, second.Factory.Ids);
        Assert.Equal(consumes
            ? new[] { "effect", "transition", "transition", "transition" }
            : new[] { "transition", "effect", "transition", "transition" }, first.Factory.Kinds);
        var old = plan.IdentityIndexAfterImage["entries"]!.AsArray().OfType<JsonObject>()
            .Single(value => value["effectId"]!.GetValue<string>() == first.OldId);
        Assert.Equal(consumes ? new[] { "consume", "replace" } : new[] { "trigger", "replace" },
            old["transitions"]!.AsArray().TakeLast(2).Select(value => value!["kind"]!.GetValue<string>()));
        Assert.Equal(consumes ? first.Factory.Ids[3] : first.Factory.Ids[0],
            old["transitions"]!.AsArray()[^2]!["transitionId"]!.GetValue<string>());
        Assert.Equal(consumes ? first.Factory.Ids[1] : first.Factory.Ids[2],
            old["transitions"]!.AsArray()[^1]!["transitionId"]!.GetValue<string>());
    }

    [Fact]
    public void IdentityHistoryOwner_WritesExactAnchorsAndRetainsAgreementAcrossLaterExpiry()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var baseline = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        var oldId = effect["effectId"]!.GetValue<string>();
        var factory = new IdentityOwnerFactory();
        using var owner = new EffectIdentityHistoryOwner(baseline, factory);
        var childId = owner.Factory.CreateEffectId();
        var replace = IdentityOwnerTransition(owner.Factory.CreateTransitionId(), "replace",
            "turn_42:identity_owner:replace", oldId, childId);
        var createRef = EffectAcceptedTurnPlanner.CreateApplicationTransitionEventRef(
            "turn_42:identity_owner:replace", "replacement_result_create");
        var create = IdentityOwnerTransition(owner.Factory.CreateTransitionId(), "create", createRef, oldId, childId);
        Assert.True(owner.TryAppendTransition(oldId, "replaced", replace));
        var childEntry = baseline["entries"]![0]!.DeepClone().AsObject();
        childEntry["effectId"] = childId;
        childEntry["state"] = "active";
        childEntry["createdAtTurn"] = 42;
        childEntry["transitions"] = new JsonArray(create.DeepClone());
        owner.CreateEntry(childEntry);
        var child = new EffectReplayIdentity(childId,
            new ResourcePendingAuthorityBinding("accepted_application", createRef));
        var old = new EffectReplayIdentity(oldId, new ResourcePendingAuthorityBinding("permanent", oldId));
        owner.RetainReplacementAgreement("turn_42:identity_owner:replace", child, old, createRef, oldId);
        var point = Assert.Single(owner.ReplacementAgreements);
        Assert.Equal(2, point.WriteCount);
        Assert.Equal(2, point.Anchors.Count);

        var before = Assert.Single(owner.FindEntries(oldId));
        var consume = IdentityOwnerTransition(owner.Factory.CreateTransitionId(), "consume",
            "turn_42:identity_owner:consume", oldId, oldId);
        var anchorIndex = before["transitions"]!.AsArray().Count - 1;
        owner.InsertBeforeTransition(oldId, anchorIndex, replace, consume);
        var expire = IdentityOwnerTransition(owner.Factory.CreateTransitionId(), "expire",
            "turn_42:identity_owner:expiry", childId);
        Assert.True(owner.TryAppendTransition(childId, "expired", expire));
        var insertion = owner.Writes.Single(value => value.Kind == EffectIdentityWriteKind.InsertBeforeTransition);
        Assert.Equal(replace["transitionId"]!.GetValue<string>(), insertion.AnchorTransitionId);
        Assert.Equal(anchorIndex, insertion.AnchorIndex);
        Assert.True(JsonNode.DeepEquals(replace, JsonNode.Parse(insertion.AnchorJson!)));
        Assert.True(JsonNode.DeepEquals(consume, JsonNode.Parse(insertion.PayloadJson)));
        Assert.Equal("replaced", insertion.BeforeState);
        Assert.Equal(insertion.BeforeState, insertion.AfterState);
        var published = owner.Publish();
        var entries = published["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        var oldAfter = entries.Single(value => value["effectId"]!.GetValue<string>() == oldId);
        Assert.Equal(new[] { "consume", "replace" },
            oldAfter["transitions"]!.AsArray().TakeLast(2).Select(value => value!["kind"]!.GetValue<string>()));
        Assert.Equal("expired", entries.Single(value => value["effectId"]!.GetValue<string>() == childId)["state"]!.GetValue<string>());
        Assert.Equal(factory.Ids, owner.Allocations.Select(value => value.Identity));
        Assert.Equal(new long[] { 0, 1, 2, 3 }, owner.Writes.Select(value => value.Ordinal));
        Assert.Equal(2, point.WriteCount); // the point-in-time assertion was not silently moved to final state
    }

    [Fact]
    public void IdentityHistoryOwner_CloneIsolationAndOnePublication()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var baseline = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        var expected = baseline.ToJsonString();
        using var owner = new EffectIdentityHistoryOwner(baseline, new IdentityOwnerFactory());
        baseline["entries"]!.AsArray().Clear();
        owner.ReadSnapshot()["entries"]!.AsArray().Clear();
        Assert.Single(owner.ReadEntries())["state"] = "changed_outside_owner";
        Assert.Single(owner.FindEntries(effect["effectId"]!.GetValue<string>()))["state"] = "changed_again";
        Assert.Equal(expected, owner.ReadSnapshot().ToJsonString());
        var published = owner.Publish();
        Assert.Equal(expected, published.ToJsonString());
        published["entries"]!.AsArray().Clear();
        Assert.Equal(expected, owner.ReadSnapshot().ToJsonString());
        Assert.Throws<InvalidOperationException>(() => owner.Publish());
        Assert.Throws<InvalidOperationException>(() => owner.Factory.CreateEffectId());
        owner.Dispose();
        owner.Dispose();
        Assert.Throws<ObjectDisposedException>(() => owner.ReadSnapshot());
        Assert.Throws<ObjectDisposedException>(() => owner.Factory.CreateTransitionId());
    }

    [Fact]
    public void IdentityHistoryOwner_RejectsChangedAnchorWithoutWriting()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var baseline = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        var id = effect["effectId"]!.GetValue<string>();
        using var owner = new EffectIdentityHistoryOwner(baseline, new IdentityOwnerFactory());
        var before = owner.ReadSnapshot().ToJsonString();
        var anchor = baseline["entries"]![0]!["transitions"]![0]!.DeepClone().AsObject();
        anchor["eventRef"] = "turn_42:wrong_anchor";
        Assert.Throws<InvalidOperationException>(() => owner.InsertBeforeTransition(id, 0, anchor,
            IdentityOwnerTransition("effect_transition_rejected", "consume", "turn_42:consume", id, id)));
        Assert.Empty(owner.Writes);
        Assert.Equal(before, owner.ReadSnapshot().ToJsonString());
    }

    [Fact]
    public void IdentityHistoryOwner_MissingAppendTargetPreservesOwnerAndExistingDiagnosticRoute()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var baseline = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        using var owner = new EffectIdentityHistoryOwner(baseline, new IdentityOwnerFactory());
        Assert.False(owner.TryAppendTransition("effect_missing", "active",
            IdentityOwnerTransition("effect_transition_missing", "trigger", "turn_42:missing",
                "effect_missing", "effect_missing")));
        Assert.Empty(owner.Writes);
        Assert.True(JsonNode.DeepEquals(baseline, owner.Publish()));
    }

    [Fact]
    public void IdentityHistoryOwner_AllocatorFailureCannotPublishOrAllocateAgain()
    {
        using var owner = new EffectIdentityHistoryOwner(
            EffectMaterializationTestFixture.CreateIdentityIndex(),
            new IdentityOwnerThrowingFactory());
        Assert.Throws<IOException>(() => owner.Factory.CreateTransitionId());
        Assert.Empty(owner.Allocations);
        Assert.Empty(owner.Writes);
        Assert.Throws<InvalidOperationException>(() => owner.Publish());
        Assert.Throws<InvalidOperationException>(() => owner.Factory.CreateEffectId());
    }

    [Fact]
    public void IdentityHistoryOwner_RealAllocatorCollisionRemainsValidationFailure()
    {
        var result = IdentityOwnerReplacement(collideTransitions: true);
        Assert.False(result.Result.Success);
        Assert.Null(result.Result.Plan);
        Assert.Contains(result.Result.Issues, value => value.Code == "effect_identity_duplicate_transition");
        Assert.Equal(new[] { "effect", "transition", "transition", "transition" }, result.Factory.Kinds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IdentityHistoryOwner_MalformedReplacementTransitionKeepsFinalValidation(string? transitionId)
    {
        var result = IdentityOwnerReplacement(
            malformedReplacementTransition: true, replacementTransitionId: transitionId);
        Assert.False(result.Result.Success);
        Assert.Null(result.Result.Plan);
        Assert.Contains(result.Result.Issues, value => value.Code == "effect_identity_invalid_field");
        Assert.DoesNotContain(result.Result.Issues,
            value => value.Code == "effect_reaction_replacement_authority_invalid");
        Assert.Equal(new[] { "effect", "transition", "transition", "transition" }, result.Factory.Kinds);
    }

    private static (EffectAcceptedTurnPlanningResult Result, IdentityOwnerFactory Factory, string OldId)
        IdentityOwnerReplacement(bool consumes = true, int uses = 2, bool collideTransitions = false,
            bool malformedReplacementTransition = false, string? replacementTransitionId = null)
    {
        const string rootKey = "identity_owner_reaction_root";
        const string childKey = "identity_owner_replacement";
        const string stackKey = "identity-owner-replacement";
        var root = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        root["definitionKey"] = rootKey;
        root["stacking"]!["stackKey"] = stackKey;
        root["components"]![0]!["payload"]!["resultKind"] = "apply_definition";
        root["components"]![0]!["payload"]!["definitionKey"] = childKey;
        root["components"]![0]!["payload"]!["parameters"] = new JsonObject { ["amount"] = 3 };
        root["components"]![0]!["payload"]!["maxExpansion"] = 2;
        var child = EffectMaterializationTestFixture.CreateDefinition();
        child["definitionKey"] = childKey;
        child["stacking"]!["stackKey"] = stackKey;
        child["stacking"]!["policy"] = "replace";
        child["stacking"]!["maxStacks"] = 1;
        child["stacking"]!["atMaximum"] = "no_change";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "event_reaction");
        effect["source"]!["definitionKey"] = rootKey;
        effect["stacking"]!["stackKey"] = stackKey;
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
        return (EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            plan, resources.EffectBoundaryTranscript, factory), factory, effect["effectId"]!.GetValue<string>());
    }

    private static JsonObject IdentityOwnerTransition(
        string id, string kind, string eventRef, string source, params string[] results) =>
        new()
        {
            ["transitionId"] = id, ["kind"] = kind, ["turn"] = 42, ["eventRef"] = eventRef,
            ["sourceEffectIds"] = new JsonArray(JsonValue.Create(source)),
            ["resultEffectIds"] = new JsonArray(results.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray()),
            ["receiptId"] = null
        };

    private static string IdentityOwnerIssues(EffectAcceptedTurnPlanningResult result) =>
        string.Join(Environment.NewLine, result.Issues.Select(value => value.Code + ": " + value.Actual));

    private sealed class IdentityOwnerFactory(bool collideTransitions = false,
        bool malformedReplacementTransition = false, string? replacementTransitionId = null) : EffectIdentityFactory
    {
        internal List<string> Ids { get; } = new();
        internal List<string> Kinds { get; } = new();
        private int _effects;
        private int _transitions;
        internal override string CreateEffectId()
        {
            var id = "effect_identity_owner_" + ++_effects;
            Kinds.Add("effect"); Ids.Add(id); return id;
        }
        internal override string CreateTransitionId()
        {
            _transitions++;
            var id = "effect_transition_identity_owner_" + (collideTransitions ? 1 : _transitions);
            if (malformedReplacementTransition && _transitions == 1)
                id = replacementTransitionId!;
            Kinds.Add("transition"); Ids.Add(id); return id;
        }
    }

    private sealed class IdentityOwnerThrowingFactory : EffectIdentityFactory
    {
        internal override string CreateTransitionId() => throw new IOException("scripted allocator failure");
    }
}
