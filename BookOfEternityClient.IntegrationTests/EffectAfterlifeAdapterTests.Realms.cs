using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAfterlifeAdapterTests
{
    [Fact]
    public void CrossRealmSourceAndTargetSelectors_FailClosedBeforeApplication()
    {
        const string guardianId = "guardian_shining_only_target";
        const string artId = "art_shining_only_source";
        var definition = CreateAfterlifeProfileDefinition("guardian");
        var sourceAuthority = EffectSourceAuthority.Build(
            new EffectSourceAuthorityInput(
                new[]
                {
                    new EffectSourceExport(
                        "shining_abode",
                        "spiritual_art",
                        artId,
                        new JsonArray(definition),
                        Materializable: true,
                        Active: true,
                        SameTurn: false)
                },
                Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal)));
        var targetAuthority = EffectTargetAuthority.Build(
            new EffectTargetAuthorityInput(
                new[]
                {
                    new EffectTargetExport(
                        "shining_abode",
                        "guardian",
                        guardianId,
                        SameTurn: false)
                },
                Array.Empty<EffectTargetExport>(),
                new HashSet<string>(StringComparer.Ordinal),
                null));
        var command = EffectMaterializationTestFixture.CreateApplyCommand("guardian");
        command["target"] = new JsonObject
        {
            ["kind"] = "guardian",
            ["targetId"] = guardianId
        };
        command["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = artId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        command["parameters"] = new JsonObject { ["value"] = -2 };
        var input = new EffectAcceptedTurnInput(
            "session_cross_realm",
            "snapshot_cross_realm",
            EffectMaterializationTestFixture.CreateCommandRoot(command),
            sourceAuthority,
            targetAuthority,
            new JsonObject
            {
                ["turn"] = 42,
                ["events"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "accepted_turn",
                    ["authorityId"] = "turn_42",
                    ["eventRef"] = "turn_42:accepted_effect"
                })
            },
            Realm: "chaos_sea");

        var result = new EffectAcceptedTurnPlanCache().GetOrBuild(input);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => string.Equals(
            issue.Code,
            "effect_source_realm_mismatch",
            StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => string.Equals(
            issue.Code,
            "effect_target_realm_mismatch",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task RealmExit_RejectsCarryWithoutSuspendOrExpirePolicy()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string artId = "art_unauthorized_realm_carry";
        var definition = CreateAfterlifeProfileDefinition("player");
        definition["allowedRealms"] = new JsonArray("chaos_sea", "shining_abode");
        definition["removal"]!["onSourceLoss"] = "no_change";
        await context.MaterializeAfterlifeActorAsync(
            "player_soul",
            "player_soul",
            "Shining Abode",
            artId,
            definition);
        await ApplyPersistentProfileEffectAsync(
            context,
            turn: 43,
            actorId: "player_soul",
            artId,
            targetKind: "player");
        var acceptedRoot = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath))!.AsObject();
        var profile = Assert.Single(
            acceptedRoot[AfterlifeEntityProfileState.ProfilesProperty]!
                .AsArray()
                .OfType<JsonObject>());
        profile["realm"] = "Chaos Sea";
        profile[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty] =
            new JsonArray(
                new JsonObject
                {
                    ["realm"] = "shining_abode",
                    ["resourceOwnerId"] = "player_soul",
                    ["state"] = "suspended"
                },
                new JsonObject
                {
                    ["realm"] = "chaos_sea",
                    ["resourceOwnerId"] = "player_soul",
                    ["state"] = "active"
                });
        var carriers = new EffectCarrierCatalogInput(
            null,
            null,
            null,
            null,
            acceptedRoot,
            null);
        var input = EffectAcceptedTurnInputComposer.Compose(
            "session_unauthorized_realm_carry",
            "snapshot_unauthorized_realm_carry",
            turn: 44,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(),
            carriers,
            carriers,
            (await context.ReadJsonAsync(
                EffectMaterializationTestContext.IdentityIndexPath))!.AsObject(),
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            {
                [EffectCarrierCatalog.AfterlifeProfilesPath] = acceptedRoot
            },
            realm: "chaos_sea");
        var initial = new EffectAcceptedTurnPlanCache().GetOrBuild(input);
        Assert.True(initial.Success, DescribeIssues(initial.Issues));
        var effectPlan = Assert.IsType<EffectAcceptedTurnPlan>(initial.Plan);
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, DescribeIssues(bootstrap.Issues));
        var sources = ResourceMutationSourceCatalog.Create(
            Array.Empty<ResourceMutationSourceExport>());
        Assert.True(sources.IsValid, DescribeIssues(sources.Issues));
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 44,
                Definitions: bootstrap.Definitions!,
                State: bootstrap.State!,
                History: bootstrap.History!,
                Sources: sources.Catalog!,
                Mutations: Array.Empty<ResourceMutationIntent>(),
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(effectPlan)),
            new AcceptedMechanicsIdentityFactory());
        Assert.True(resources.IsValid, DescribeIssues(resources.Issues));
        var result = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            effectPlan,
            resources.EffectBoundaryTranscript!,
            new EffectIdentityFactory());

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => string.Equals(
            issue.Code,
            "effect_lifecycle_realm_transition_unauthorized",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("suspend", "suspended", 1)]
    [InlineData("expire", null, 0)]
    public async Task RealmExit_AppliesExactSourceLossPolicyThroughAcceptedTurnPlan(
        string lossPolicy,
        string? expectedState,
        int expectedCount)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string artId = "art_realm_exit_policy";
        var definition = CreateAfterlifeProfileDefinition("player");
        definition["allowedRealms"] = new JsonArray("chaos_sea", "shining_abode");
        definition["removal"]!["onSourceLoss"] = lossPolicy;
        await context.MaterializeAfterlifeActorAsync(
            "player_soul",
            "player_soul",
            "Shining Abode",
            artId,
            definition);
        await ApplyPersistentProfileEffectAsync(
            context,
            turn: 43,
            actorId: "player_soul",
            artId,
            targetKind: "player");

        var acceptedRoot = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath))!.AsObject();
        var profile = Assert.Single(
            acceptedRoot[AfterlifeEntityProfileState.ProfilesProperty]!
                .AsArray()
                .OfType<JsonObject>());
        profile["realm"] = "Chaos Sea";
        profile[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty] =
            new JsonArray(
                new JsonObject
                {
                    ["realm"] = "shining_abode",
                    ["resourceOwnerId"] = "player_soul",
                    ["state"] = "suspended"
                },
                new JsonObject
                {
                    ["realm"] = "chaos_sea",
                    ["resourceOwnerId"] = "player_soul",
                    ["state"] = "active"
                });
        var carriers = new EffectCarrierCatalogInput(
            null,
            null,
            null,
            null,
            acceptedRoot,
            null);
        var sourceRoots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            [EffectCarrierCatalog.AfterlifeProfilesPath] = acceptedRoot
        };
        var identity = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var input = EffectAcceptedTurnInputComposer.Compose(
            "session_realm_exit",
            "snapshot_realm_exit",
            turn: 44,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(),
            carriers,
            carriers,
            identity,
            sourceRoots,
            realm: "chaos_sea");

        var initial = new EffectAcceptedTurnPlanCache().GetOrBuild(input);
        Assert.True(initial.Success, DescribeIssues(initial.Issues));
        var effectPlan = Assert.IsType<EffectAcceptedTurnPlan>(initial.Plan);
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, DescribeIssues(bootstrap.Issues));
        var sources = ResourceMutationSourceCatalog.Create(
            Array.Empty<ResourceMutationSourceExport>());
        Assert.True(sources.IsValid, DescribeIssues(sources.Issues));
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 44,
                Definitions: bootstrap.Definitions!,
                State: bootstrap.State!,
                History: bootstrap.History!,
                Sources: sources.Catalog!,
                Mutations: Array.Empty<ResourceMutationIntent>(),
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(effectPlan)),
            new AcceptedMechanicsIdentityFactory());
        Assert.True(resources.IsValid, DescribeIssues(resources.Issues));
        var result = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            effectPlan,
            resources.EffectBoundaryTranscript!,
            new EffectIdentityFactory());

        Assert.True(result.Success, DescribeIssues(result.Issues));
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(result.Plan);
        var after = plan.CarrierAfterImages[EffectCarrierCatalog.AfterlifeProfilesPath];
        var afterProfile = Assert.Single(
            after[AfterlifeEntityProfileState.ProfilesProperty]!
                .AsArray()
                .OfType<JsonObject>());
        Assert.Equal(expectedCount, afterProfile["activeEffects"]!.AsArray().Count);
        if (expectedState != null)
        {
            Assert.Equal(
                expectedState,
                afterProfile["activeEffects"]![0]!["state"]!.GetValue<string>());
        }
        var indexEntry = Assert.Single(plan.IdentityIndexAfterImage["entries"]!.AsArray());
        Assert.Equal(
            expectedState ?? "expired",
            indexEntry!["state"]!.GetValue<string>());
    }

    [Fact]
    public void RealmTransitionEvent_KeepsEffectRealmAndUsesAcceptedProfileRealm()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("player");
        effect["realm"] = "shining_abode";
        effect["target"] = new JsonObject
        {
            ["kind"] = "player",
            ["targetId"] = "player_soul"
        };
        var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(
            "player_soul",
            "player_soul",
            "Chaos Sea",
            materializedAtTurn: 41);
        profile.Remove(ActorMaterializationContract.PropertyName);
        profile["activeEffects"] = new JsonArray(effect);
        profile[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty] =
            new JsonArray(
                new JsonObject
                {
                    ["realm"] = "shining_abode",
                    ["resourceOwnerId"] = "player_soul",
                    ["state"] = "suspended"
                },
                new JsonObject
                {
                    ["realm"] = "chaos_sea",
                    ["resourceOwnerId"] = "player_soul",
                    ["state"] = "active"
                });
        var accepted = new EffectCarrierCatalogInput(
            null,
            null,
            null,
            null,
            new JsonObject
            {
                ["schemaVersion"] = AfterlifeEntityProfileState.SchemaVersion,
                [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(profile)
            },
            null);

        var eventInput = EffectAcceptedTurnInputComposer.BuildAcceptedEventInput(
            turn: 43,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(),
            acceptedCarriers: accepted,
            commandRealm: "chaos_sea");

        var lifecycle = Assert.Single(
            eventInput["lifecycleEvents"]!.AsArray().OfType<JsonObject>(),
            node => string.Equals(
                node["target"]?["targetId"]?.GetValue<string>(),
                "player_soul",
                StringComparison.Ordinal));
        Assert.Equal("shining_abode", lifecycle["realm"]!.GetValue<string>());
        Assert.Equal("chaos_sea", lifecycle["currentRealm"]!.GetValue<string>());
    }

    [Fact]
    public void CanonicalAfterlifeBindings_KeepSuspendedRealmTargetAndSourceResolvable()
    {
        const string artId = "art_cross_realm_binding";
        var definition = CreateAfterlifeProfileDefinition("player");
        definition["allowedRealms"] = new JsonArray("chaos_sea", "shining_abode");
        var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(
            "player_soul",
            "player_soul",
            "Chaos Sea",
            materializedAtTurn: 41,
            specialArts: new JsonArray(new JsonObject
            {
                ["artId"] = artId,
                ["displayName"] = "Двухмирный след",
                ["effectDescription"] = "Сохраняет точную историю эффекта при смене мира.",
                ["owner"] = "player_soul",
                ["baseOperation"] = "guard",
                ["activeEffectDefinitions"] = new JsonArray(definition)
            }));
        profile.Remove(ActorMaterializationContract.PropertyName);
        profile["activeEffects"] = new JsonArray();
        profile[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty] =
            new JsonArray(
                new JsonObject
                {
                    ["realm"] = "shining_abode",
                    ["resourceOwnerId"] = "player_soul",
                    ["state"] = "suspended"
                },
                new JsonObject
                {
                    ["realm"] = "chaos_sea",
                    ["resourceOwnerId"] = "player_soul",
                    ["state"] = "active"
                });
        var root = new JsonObject
        {
            ["schemaVersion"] = AfterlifeEntityProfileState.SchemaVersion,
            [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(profile)
        };
        var carriers = new EffectCarrierCatalogInput(
            null,
            null,
            null,
            null,
            root,
            null);
        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            {
                [EffectCarrierCatalog.AfterlifeProfilesPath] = root
            });
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(
            carriers,
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal));

        Assert.True(targets.Resolve(
            new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_soul"
            },
            "shining_abode").Success);
        Assert.True(targets.Resolve(
            new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_soul"
            },
            "chaos_sea").Success);

        var shiningSource = new EffectSourceKey(
            "shining_abode",
            "spiritual_art",
            artId,
            EffectMaterializationTestFixture.DefinitionKey);
        var chaosSource = shiningSource with { Realm = "chaos_sea" };
        Assert.True(sources.ResolveCanonicalBinding(shiningSource, "player").Success);
        Assert.False(sources.Resolve(shiningSource, "player", new JsonObject
        {
            ["value"] = -2
        }).Success);
        Assert.True(sources.Resolve(chaosSource, "player", new JsonObject
        {
            ["value"] = -2
        }).Success);
    }

    [Fact]
    public async Task SameTurnAfterlifeActor_UsesMaterializationRefAndPublishesPermanentTargetId()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string sourceArtId = "art_same_turn_afterlife_target";
        const string guardianId = "guardian_same_turn_effect_target";
        var definition = CreateAfterlifeProfileDefinition("guardian");
        await context.MaterializeAfterlifeActorAsync(
            "player_soul",
            "player_soul",
            "Shining Abode",
            sourceArtId,
            definition);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            currentRealm: "Shining Abode");

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath))!.AsObject();
        var guardian = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(
            "guardian",
            guardianId,
            "Shining Abode",
            materializedAtTurn: 43);
        guardian["activeEffects"] = new JsonArray();
        guardian["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject
            {
                ["resourceKey"] = "soul_integrity",
                ["maximum"] = 10
            })
        };
        var targetRef = guardian[ActorMaterializationContract.PropertyName]!
            ["materializationId"]!.GetValue<string>();
        root[AfterlifeEntityProfileState.ResponseProfilesProperty] =
            new JsonArray(guardian);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath,
            root);

        var command = EffectMaterializationTestFixture.CreateApplyCommand("guardian");
        command["target"] = new JsonObject
        {
            ["kind"] = "guardian",
            ["targetRef"] = targetRef
        };
        command["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = sourceArtId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        command["parameters"] = new JsonObject { ["value"] = -2 };
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        Assert.NotNull(
            await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(backups));

        var canonical = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath))!.AsObject();
        var publishedGuardian = canonical[AfterlifeEntityProfileState.ProfilesProperty]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(profile => string.Equals(
                profile["actorId"]?.GetValue<string>(),
                guardianId,
                StringComparison.Ordinal));
        var effect = Assert.IsType<JsonObject>(
            Assert.Single(publishedGuardian["activeEffects"]!.AsArray()));
        Assert.Equal(
            guardianId,
            effect["target"]!["targetId"]!.GetValue<string>());
        Assert.False(effect["target"]!.AsObject().ContainsKey("targetRef"));
    }
}
