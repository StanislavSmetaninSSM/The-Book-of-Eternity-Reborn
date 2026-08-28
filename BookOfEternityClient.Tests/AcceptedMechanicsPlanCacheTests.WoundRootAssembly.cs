using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedMechanicsPlanCacheTests
{
    [Fact]
    public void WoundPendingPlan_ProductionPlannerSealsDerivedPendingState()
    {
        var woundInput = CreateWoundCommonInput(
            "root_assembly_pending_production");
        var acceptedEvents = woundInput.Binding.AcceptedEvents.ToArray();
        acceptedEvents[0] = acceptedEvents[0] with
        {
            Kind = "owner_turn_end"
        };
        woundInput = woundInput with
        {
            Binding = woundInput.Binding with
            {
                AcceptedEvents = acceptedEvents,
                AcceptedEventsFingerprint =
                    WoundAcceptedEventSetFingerprint.Compute(acceptedEvents)
            },
            Opportunities = woundInput.Opportunities.Select(value =>
                ResealWoundOpportunity(value with
                {
                    EventKind = acceptedEvents.Single(eventValue =>
                        string.Equals(
                            eventValue.EventRef,
                            value.EventRef,
                            StringComparison.Ordinal)).Kind
                })).ToArray()
        };
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(woundInput));
        var boundedDefinition =
            EffectMaterializationTestFixture.CreateDefinition();
        boundedDefinition["definitionKey"] =
            "pending_production_quest_definition";
        boundedDefinition["triggers"]![0]!["resolutionMode"] =
            "bounded_receipt";
        var boundedEffect =
            EffectMaterializationTestFixture.CreateCanonicalEffect();
        boundedEffect["source"] = new JsonObject
        {
            ["kind"] = "quest",
            ["sourceId"] = "quest_pending_production",
            ["definitionKey"] = "pending_production_quest_definition"
        };
        boundedEffect["triggers"] = boundedDefinition["triggers"]!.DeepClone();
        var effectInput = WoundEffectBatchPlannerTests
            .CreateEffectInputForAcceptedCache(
                prepared,
                new EffectSourceExport(
                    "mortal_world",
                    "quest",
                    "quest_pending_production",
                    new JsonArray(boundedDefinition.DeepClone()),
                    Materializable: true,
                    Active: true,
                    SameTurn: false),
                new EffectCarrierCatalogInput(
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["activeEffects"] = new JsonArray(
                            boundedEffect.DeepClone())
                    },
                    null,
                    null,
                    null,
                    null,
                    null),
                EffectMaterializationTestFixture.CreateIdentityIndex(
                    boundedEffect),
                eventInput => eventInput["lifecycleEvents"] =
                    new JsonArray(new JsonObject
                    {
                        ["eventRef"] =
                            "turn_42:wound_pending:owner_turn_end",
                        ["causalEventRef"] = acceptedEvents[0].EventRef,
                        ["turn"] = 42,
                        ["phase"] = "owner_turn_end",
                        ["realm"] = "mortal_world",
                        ["target"] = new JsonObject
                        {
                            ["kind"] = "player",
                            ["targetId"] = "player_current"
                        },
                        ["effectId"] =
                            boundedEffect["effectId"]!.GetValue<string>(),
                        ["triggerId"] = "on_owner_turn_end"
                    }));
        var effectResult = WoundEffectBatchPlanner.Build(
            prepared,
            effectInput,
            new WoundCommonEffectIdentityFactory(
                "root_assembly_pending_production"));
        var effect = AssertWoundCommonEffect(effectResult);
        var final = AssertWoundCommonFinal(
            WoundAcceptedTurnPlanner.Finalize(prepared, effectResult));
        var bundle = new AcceptedMechanicsWoundStageBundle(
            woundInput,
            prepared,
            effect,
            final);
        var stages = new WoundCommonStageFixture(
            woundInput,
            prepared,
            effect,
            final,
            bundle);
        var baselineInput = CreateWoundCommonAcceptedInput(
            stages.Bundle,
            pendingAfterImages: new Dictionary<string, JsonObject?>(
                StringComparer.Ordinal)
            {
                [ResourcePendingResolutionState.PendingPath] = null
            });
        var baselineContext = baselineInput.PlanningContext!;
        var owners = ResourceOwnerAuthority.Build(
            new ResourceOwnerAuthorityInput(
                new[]
                {
                    new ResourceOwnerExport(
                        new ResourceOwnerKey(
                            "mortal_world",
                            ResourceOwnerKind.Player,
                            "player_current"),
                        ResourceOwnerLifecycle.Active,
                        SameTurn: false,
                        OwnerRef: null,
                        BoundNpcId: null,
                        new HashSet<string>(StringComparer.Ordinal)
                        {
                            "health"
                        },
                        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
                },
                Array.Empty<ResourceOwnerExport>(),
                Array.Empty<ResourceOwnerKey>()));
        Assert.Empty(owners.Issues);
        var context = new AcceptedMechanicsPlanningContext(
            baselineContext.DefinitionRoot,
            baselineContext.Definitions,
            baselineContext.State,
            baselineContext.History,
            owners,
            baselineContext.Sources,
            baselineContext.Commands,
            baselineContext.EffectIdentityRoot,
            baselineContext.EffectPlan,
            baselineContext.CapacityTransitions,
            baselineContext.OwnerCapacityDrafts,
            baselineContext.TerminalOwners,
            baselineContext.OwnerCompanionAfterImages,
            baselineContext.OwnerTransitions,
            baselineContext.RegisteredSystemOutcomes,
            baselineContext.PendingResolutionState,
            baselineContext.ResourceIdentityFactory,
            baselineContext.EffectIdentityFactory,
            baselineContext.ExecutionSequenceOffset,
            stages.Bundle);
        var input = new AcceptedMechanicsInput(
            baselineInput.SessionId,
            baselineInput.RequestId,
            baselineInput.SnapshotToken,
            baselineInput.Realm,
            baselineInput.Turn,
            baselineInput.AcceptedEvents,
            baselineInput.ResourceCommands,
            baselineInput.EffectCommands,
            baselineInput.PendingInput,
            baselineInput.InternalInputs,
            baselineInput.AuthorityFingerprints with { Owners = owners.Fingerprint },
            baselineInput.BeforeImages,
            baselineInput.ValidationIssues,
            context,
            baselineInput.WoundCommands,
            baselineInput.WoundInput);
        var cache = new AcceptedMechanicsPlanCache(
            AcceptedMechanicsPlanner.BuildAcceptedPlan);

        var result = cache.GetOrBuildValidated(input);

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
        Assert.True(plan.AwaitsPendingResolution);
        Assert.NotNull(plan.PendingPublicationAuthority);
        Assert.NotNull(plan.WoundStageBundle);
        Assert.Empty(plan.WoundCarrierAfterImages);
        Assert.DoesNotContain(
            AcceptedMechanicsPlan.WoundCommandPath,
            plan.TouchedPaths);
        Assert.DoesNotContain(
            AcceptedMechanicsPlan.WoundCommandPath,
            plan.ConsumedPaths);

        var publicationAuthority = Assert.IsType<
            AcceptedMechanicsPendingPublicationAuthority>(
                plan.PendingPublicationAuthority);
        var forgedPending = CreateWoundPending(
            woundInput,
            baselineContext.Definitions,
            publicationAuthority.FullTurnFingerprint,
            publicationAuthority.SemanticTurnFingerprint);
        Assert.True(
            forgedPending.IsValid,
            string.Join(Environment.NewLine, forgedPending.Issues.Select(
                static issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        var forgedPendingAfterImages = new Dictionary<string, JsonObject?>(
            StringComparer.Ordinal)
        {
            [ResourcePendingResolutionState.PendingPath] =
                forgedPending.State!.ToCanonicalRoot()
        };

        var forgedException = Record.Exception(() => new AcceptedMechanicsPlan(
            plan.InputFingerprint,
            plan.DefinitionAfterImage,
            plan.StateAfterImage,
            plan.HistoryAfterImage,
            plan.EffectCarrierAfterImages,
            plan.EffectIdentityAfterImage,
            forgedPendingAfterImages,
            plan.OwnerCompanionAfterImages,
            plan.BeforeImages,
            plan.TouchedPaths,
            plan.ConsumedPaths,
            plan.AuthorityFingerprints,
            plan.ResourceEvents,
            plan.ProjectionInput,
            plan.OwnerAuthority,
            plan.EffectPlan,
            plan.OwnerTransitions,
            forgedPending.SafeGmPacket,
            publicationAuthority,
            plan.WoundStageBundle));

        Assert.IsType<ArgumentException>(forgedException);
    }

    [Fact]
    public void WoundPendingFullFingerprint_BindsExactRetainedWoundStages()
    {
        var woundInput = CreateWoundCommonInput(
            "pending_fingerprint_stage_binding");
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(woundInput));
        var firstStages = CreateWoundCommonStages(
            woundInput,
            prepared,
            "pending_fingerprint_first");
        var secondStages = CreateWoundCommonStages(
            woundInput,
            prepared,
            "pending_fingerprint_second");
        var firstInput = CreateWoundCommonAcceptedInput(firstStages.Bundle);
        var secondInput = CreateWoundCommonAcceptedInput(secondStages.Bundle);

        Assert.NotEqual(
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(firstInput),
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(secondInput));
        Assert.Equal(
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(firstInput),
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(secondInput));
    }

    [Fact]
    public void WoundPendingFingerprint_BindsExactWoundCommands()
    {
        var stages = CreateWoundCommonStages(
            "pending_fingerprint_commands",
            "pending_fingerprint_commands");
        var firstInput = CreateWoundCommonAcceptedInput(stages.Bundle);
        var changedCommands = firstInput.WoundCommands!;
        changedCommands["testMutation"] = "changed";
        var secondInput = new AcceptedMechanicsInput(
            firstInput.SessionId,
            firstInput.RequestId,
            firstInput.SnapshotToken,
            firstInput.Realm,
            firstInput.Turn,
            firstInput.AcceptedEvents,
            firstInput.ResourceCommands,
            firstInput.EffectCommands,
            firstInput.PendingInput,
            firstInput.InternalInputs,
            firstInput.AuthorityFingerprints,
            firstInput.BeforeImages,
            firstInput.ValidationIssues,
            firstInput.PlanningContext,
            changedCommands,
            firstInput.WoundInput);

        Assert.NotEqual(
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(firstInput),
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(secondInput));
        Assert.NotEqual(
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(firstInput),
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(secondInput));
    }

    [Fact]
    public void WoundPendingPlan_RejectsInventedCurrentTurnStateWithoutPlannerProof()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_pending",
            "root_assembly_pending");
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var authorityInput = CreateWoundCommonAcceptedInput(stages.Bundle);
        var pending = CreateWoundPending(
            stages.Input,
            definitions,
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(
                authorityInput),
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(
                authorityInput));
        Assert.True(
            pending.IsValid,
            string.Join(Environment.NewLine, pending.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        var pendingAfterImages = new Dictionary<string, JsonObject?>(
            StringComparer.Ordinal)
        {
            [ResourcePendingResolutionState.PendingPath] =
                pending.State!.ToCanonicalRoot()
        };
        var input = CreateWoundCommonAcceptedInput(
            stages.Bundle,
            pendingAfterImages: pendingAfterImages);
        var inputFingerprint =
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input);
        var basePlan = Assert.IsType<AcceptedMechanicsPlan>(
            new CountingPlanner().Build(input, inputFingerprint).Plan);
        var exception = Record.Exception(() => new AcceptedMechanicsPlan(
            inputFingerprint,
            definitions.ToCanonicalRoot(),
            basePlan.StateAfterImage,
            basePlan.HistoryAfterImage,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            basePlan.EffectIdentityAfterImage,
            pendingAfterImages,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            input.BeforeImages,
            new[]
            {
                AcceptedMechanicsPlan.DefinitionPath,
                AcceptedMechanicsPlan.StatePath,
                AcceptedMechanicsPlan.HistoryPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                EffectAcceptedTurnPlan.IdentityIndexPath,
                ResourcePendingResolutionState.PendingPath
            },
            Array.Empty<string>(),
            input.AuthorityFingerprints,
            Array.Empty<ResourceAppliedEvent>(),
            new ResourceProjectionInput(
                definitions.ToCanonicalRoot(),
                basePlan.StateAfterImage,
                basePlan.HistoryAfterImage,
                input.AuthorityFingerprints.Owners),
            basePlan.OwnerAuthority,
            stages.Effect.EffectPlan,
            pendingGmPacket: pending.SafeGmPacket,
            pendingPublicationAuthority: null,
            woundStageBundle: stages.Bundle));

        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void WoundPendingPlan_RejectsCanonicalPendingFromDifferentAcceptedTurn()
    {
        var foreignStages = CreateWoundCommonStages(
            "root_assembly_pending_foreign",
            "root_assembly_pending_foreign");
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var foreignInput = CreateWoundCommonAcceptedInput(foreignStages.Bundle);
        var foreignPending = CreateWoundPending(
            foreignStages.Input,
            definitions,
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(
                foreignInput),
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(
                foreignInput));
        Assert.True(foreignPending.IsValid);
        var pendingAfterImages = new Dictionary<string, JsonObject?>(
            StringComparer.Ordinal)
        {
            [ResourcePendingResolutionState.PendingPath] =
                foreignPending.State!.ToCanonicalRoot()
        };
        var localStages = CreateWoundCommonStages(
            "root_assembly_pending_local",
            "root_assembly_pending_local");
        var localInput = CreateWoundCommonAcceptedInput(
            localStages.Bundle,
            pendingAfterImages: pendingAfterImages);
        var inputFingerprint =
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(localInput);
        var basePlan = Assert.IsType<AcceptedMechanicsPlan>(
            new CountingPlanner().Build(localInput, inputFingerprint).Plan);

        var exception = Record.Exception(() => new AcceptedMechanicsPlan(
            inputFingerprint,
            definitions.ToCanonicalRoot(),
            basePlan.StateAfterImage,
            basePlan.HistoryAfterImage,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            basePlan.EffectIdentityAfterImage,
            pendingAfterImages,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            localInput.BeforeImages,
            new[]
            {
                AcceptedMechanicsPlan.DefinitionPath,
                AcceptedMechanicsPlan.StatePath,
                AcceptedMechanicsPlan.HistoryPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                EffectAcceptedTurnPlan.IdentityIndexPath,
                ResourcePendingResolutionState.PendingPath
            },
            Array.Empty<string>(),
            localInput.AuthorityFingerprints,
            Array.Empty<ResourceAppliedEvent>(),
            new ResourceProjectionInput(
                definitions.ToCanonicalRoot(),
                basePlan.StateAfterImage,
                basePlan.HistoryAfterImage,
                localInput.AuthorityFingerprints.Owners),
            basePlan.OwnerAuthority,
            localStages.Effect.EffectPlan,
            pendingGmPacket: foreignPending.SafeGmPacket,
            pendingPublicationAuthority: null,
            woundStageBundle: localStages.Bundle));

        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void WoundPendingPlan_RejectsArbitraryPacketWithoutCanonicalPendingState()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_fake_pending",
            "root_assembly_fake_pending");
        var pendingAfterImages = new Dictionary<string, JsonObject?>(
            StringComparer.Ordinal)
        {
            [ResourcePendingResolutionState.PendingPath] = new JsonObject
            {
                ["schemaVersion"] = 1
            }
        };
        var input = CreateWoundCommonAcceptedInput(
            stages.Bundle,
            pendingAfterImages: pendingAfterImages);
        var inputFingerprint =
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input);
        var basePlan = Assert.IsType<AcceptedMechanicsPlan>(
            new CountingPlanner().Build(input, inputFingerprint).Plan);

        var exception = Record.Exception(() => new AcceptedMechanicsPlan(
            inputFingerprint,
            basePlan.DefinitionAfterImage,
            basePlan.StateAfterImage,
            basePlan.HistoryAfterImage,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            basePlan.EffectIdentityAfterImage,
            pendingAfterImages,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            input.BeforeImages,
            basePlan.TouchedPaths
                .Where(static path =>
                    path != AcceptedMechanicsPlan.WoundCommandPath)
                .Append(ResourcePendingResolutionState.PendingPath)
                .ToArray(),
            Array.Empty<string>(),
            input.AuthorityFingerprints,
            Array.Empty<ResourceAppliedEvent>(),
            basePlan.ProjectionInput,
            basePlan.OwnerAuthority,
            stages.Effect.EffectPlan,
            pendingGmPacket: new JsonObject { ["kind"] = "pending" },
            pendingPublicationAuthority: null,
            woundStageBundle: stages.Bundle));

        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void AcceptedMechanicsPlanner_WoundStagesProduceTypedCarrierPublication()
    {
        var woundInput = WoundEffectBatchPlannerTests
            .CreateNoMechanicsInputForAcceptedCache();
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(woundInput));
        var stages = CreateWoundCommonStages(
            woundInput,
            prepared,
            "root_assembly_common_plan");
        var baselineInput = CreateWoundCommonAcceptedInput(stages.Bundle);
        var baselineContext = baselineInput.PlanningContext!;
        var context = new AcceptedMechanicsPlanningContext(
            baselineContext.DefinitionRoot,
            baselineContext.Definitions,
            baselineContext.State,
            baselineContext.History,
            baselineContext.Owners,
            baselineContext.Sources,
            baselineContext.Commands,
            baselineContext.EffectIdentityRoot,
            stages.Effect.EffectPlan,
            baselineContext.CapacityTransitions,
            baselineContext.OwnerCapacityDrafts,
            baselineContext.TerminalOwners,
            baselineContext.OwnerCompanionAfterImages,
            baselineContext.OwnerTransitions,
            baselineContext.RegisteredSystemOutcomes,
            baselineContext.PendingResolutionState,
            baselineContext.ResourceIdentityFactory,
            baselineContext.EffectIdentityFactory,
            baselineContext.ExecutionSequenceOffset,
            stages.Bundle);
        var input = new AcceptedMechanicsInput(
            baselineInput.SessionId,
            baselineInput.RequestId,
            baselineInput.SnapshotToken,
            baselineInput.Realm,
            baselineInput.Turn,
            baselineInput.AcceptedEvents,
            baselineInput.ResourceCommands,
            baselineInput.EffectCommands,
            baselineInput.PendingInput,
            baselineInput.InternalInputs,
            baselineInput.AuthorityFingerprints,
            baselineInput.BeforeImages,
            baselineInput.ValidationIssues,
            context,
            baselineInput.WoundCommands,
            baselineInput.WoundInput);

        var result = AcceptedMechanicsPlanner.BuildAcceptedPlan(
            input,
            AcceptedMechanicsPlanFingerprints.ComputeInput(
                input.CreateBinding()));

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
        var root = plan.WoundCarrierAfterImages[WoundCarrierCatalog.PlayerPath];
        Assert.Single(root["activeWounds"]!.AsArray());
        Assert.Equal(
            stages.Final.IdentityIndexAfterImage.ToJsonString(),
            plan.WoundIdentityAfterImage!.ToJsonString());
        Assert.Equal(
            stages.Final.HistoryAfterImage.ToJsonString(),
            plan.WoundHistoryAfterImage!.ToJsonString());
    }

    [Fact]
    public void WoundRootAssembly_SharedAfterlifeProfilePreservesEffectArtAndService()
    {
        var input = WoundEffectBatchPlannerTests.CreateInputForAcceptedCache(
            WoundEffectBatchPlannerTests.OwnerFlavor.AfterlifeGuardian);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var effectInput = WoundEffectBatchPlannerTests
            .CreateEffectInputForAcceptedCache(prepared);
        var ownerAfterImage = effectInput.PreTurnCarriers!.AfterlifeProfiles!
            .DeepClone().AsObject();
        var profile = ownerAfterImage["profiles"]![0]!.AsObject();
        profile["services"]!["revision"] = 2;
        profile["arts"] = new JsonObject
        {
            ["spiritualHealing"] = new JsonObject
            {
                ["rank"] = 3
            }
        };
        var acceptedCarrierBaselines = effectInput.AcceptedCarrierBaselines ??
            effectInput.PreTurnCarriers;
        effectInput = effectInput with
        {
            PreTurnCarriers = effectInput.PreTurnCarriers! with
            {
                AfterlifeProfiles = ownerAfterImage
            },
            AcceptedCarrierBaselines = acceptedCarrierBaselines! with
            {
                AfterlifeProfiles = ownerAfterImage
            }
        };
        var effectResult = WoundEffectBatchPlanner.Build(
            prepared,
            effectInput,
            new WoundCommonEffectIdentityFactory("root_assembly_afterlife"));
        var effect = AssertWoundCommonEffect(effectResult);
        var final = AssertWoundCommonFinal(
            WoundAcceptedTurnPlanner.Finalize(prepared, effectResult));
        var stages = new AcceptedMechanicsWoundStageBundle(
            input,
            prepared,
            effect,
            final);

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            CompleteEffectPlanForAssembly(effect.EffectPlan),
            new Dictionary<string, JsonObject>(StringComparer.Ordinal)
            {
                [WoundCarrierCatalog.AfterlifeProfilesPath] = ownerAfterImage
            },
            stages);

        Assert.True(
            composition.Success,
            string.Join(Environment.NewLine, composition.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        Assert.Empty(composition.EffectCarrierAfterImages);
        Assert.Empty(composition.OwnerCompanionAfterImages);
        var root = composition.WoundPublication!.CarrierAfterImages[
            WoundCarrierCatalog.AfterlifeProfilesPath];
        var expectedRoot = effect.EffectPlan.CarrierAfterImages[
            WoundCarrierCatalog.AfterlifeProfilesPath].DeepClone().AsObject();
        var expectedWound = Assert.IsType<WoundMaterializationEnvelope>(
            Assert.Single(Assert.Single(final.CarrierContributions).Mutations)
                .AfterWound);
        expectedRoot["profiles"]![0]!["activeWounds"]!.AsArray().Add(
            JsonNode.Parse(
                WoundMaterializationContract.SerializeCanonical(expectedWound)));
        Assert.True(JsonNode.DeepEquals(expectedRoot, root));
        var assembledProfile = root["profiles"]![0]!.AsObject();
        Assert.Single(assembledProfile["activeWounds"]!.AsArray());
        Assert.Equal(2, assembledProfile["activeEffects"]!.AsArray().Count);
        Assert.Equal(2, assembledProfile["services"]!["revision"]!.GetValue<int>());
        Assert.Equal(
            3,
            assembledProfile["arts"]!["spiritualHealing"]!["rank"]!
                .GetValue<int>());
        Assert.Equal(
            input.Binding.Turn,
            root["profileAudit"]!["lastTurn"]!.GetValue<int>());
    }

    [Fact]
    public void WoundRootAssembly_RejectsEffectRootThatDropsOwnerFields()
    {
        var input = WoundEffectBatchPlannerTests.CreateInputForAcceptedCache(
            WoundEffectBatchPlannerTests.OwnerFlavor.AfterlifeGuardian);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stages = CreateWoundCommonStages(
            input,
            prepared,
            "root_assembly_owner_field_loss");
        var initial = stages.Effect.EffectPlan;
        var ownerRoot = initial.AcceptedCarrierBaselines.AfterlifeProfiles!;
        var forgedRoot = initial.CarrierAfterImages[
            EffectCarrierCatalog.AfterlifeProfilesPath].DeepClone().AsObject();
        forgedRoot["profiles"]![0]!["services"]!["revision"] = 99;
        var carrierAfterImages = initial.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        carrierAfterImages[EffectCarrierCatalog.AfterlifeProfilesPath] =
            forgedRoot;
        var runtimeCarriers = initial.ResourceTriggerCarriers with
        {
            AfterlifeProfiles = forgedRoot
        };
        var forgedFinal = CopyEffectPlan(
            initial,
            resourceTriggerCarriers: runtimeCarriers,
            carrierAfterImages: carrierAfterImages);

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            forgedFinal,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal)
            {
                [EffectCarrierCatalog.AfterlifeProfilesPath] = ownerRoot
            },
            stages.Bundle);

        Assert.False(composition.Success);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_effect_owner_delta_invalid");
    }

    [Fact]
    public void WoundRootAssembly_StaleSelectedWoundCollectionFailsWithoutAfterImage()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_stale",
            "root_assembly_stale");
        var staleRoot = stages.Input.PreTurnCarriers.PlayerWounds!
            .DeepClone().AsObject();
        staleRoot["activeWounds"]!.AsArray().Add(
            WoundContractTestData.CreateActiveWound(
                "wound_stale_root_assembly",
                "mortal_world",
                "player",
                "player_current",
                WoundCarrierCatalog.PlayerPath));

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            CompleteEffectPlanForAssembly(stages.Effect.EffectPlan),
            new Dictionary<string, JsonObject>(StringComparer.Ordinal)
            {
                [WoundCarrierCatalog.PlayerPath] = staleRoot
            },
            stages.Bundle);

        Assert.False(composition.Success);
        Assert.Null(composition.WoundPublication);
        Assert.Empty(composition.EffectCarrierAfterImages);
        Assert.Empty(composition.OwnerCompanionAfterImages);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_collection_stale");
    }

    [Fact]
    public void WoundRootAssembly_ReturnedRootsAreDeeplyDetached()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_detached",
            "root_assembly_detached");

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            CompleteEffectPlanForAssembly(stages.Effect.EffectPlan),
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        Assert.True(composition.Success);
        var returned = composition.WoundPublication!.CarrierAfterImages[
            WoundCarrierCatalog.PlayerPath];
        returned["forged"] = true;
        Assert.False(composition.WoundPublication.CarrierAfterImages[
            WoundCarrierCatalog.PlayerPath].ContainsKey("forged"));
    }

    [Fact]
    public void WoundRootAssembly_SharedCombatantRootPreservesExactEffectRoot()
    {
        var input = WoundEffectBatchPlannerTests.CreateInputForAcceptedCache(
            WoundEffectBatchPlannerTests.OwnerFlavor.Combatant);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stages = CreateWoundCommonStages(
            input,
            prepared,
            "root_assembly_combatant");

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            CompleteEffectPlanForAssembly(stages.Effect.EffectPlan),
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        Assert.True(composition.Success);
        Assert.DoesNotContain(
            WoundCarrierCatalog.EnemiesPath,
            composition.EffectCarrierAfterImages.Keys);
        var actual = composition.WoundPublication!.CarrierAfterImages[
            WoundCarrierCatalog.EnemiesPath];
        var expected = stages.Effect.EffectPlan.CarrierAfterImages[
            WoundCarrierCatalog.EnemiesPath].DeepClone().AsObject();
        var expectedWound = Assert.IsType<WoundMaterializationEnvelope>(
            Assert.Single(Assert.Single(stages.Final.CarrierContributions).Mutations)
                .AfterWound);
        expected["enemiesData"]![0]!["activeWounds"]!.AsArray().Add(
            JsonNode.Parse(
                WoundMaterializationContract.SerializeCanonical(expectedWound)));
        Assert.True(JsonNode.DeepEquals(expected, actual));
    }

    [Fact]
    public void WoundRootAssembly_RejectsCrossPlanInitialEffectStage()
    {
        var first = CreateWoundCommonStages("root_stage_a", "root_stage_a");
        var second = CreateWoundCommonStages("root_stage_b", "root_stage_b");

        var issues = AcceptedMechanicsCarrierAssembler.ValidateInitialEffectPlan(
            first.Effect.EffectPlan,
            second.Bundle);

        Assert.Contains(
            issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_effect_plan_mismatch");
    }

    [Fact]
    public void WoundRootAssembly_RejectsUnexplainedSiblingWoundMutation()
    {
        var input = WoundEffectBatchPlannerTests.CreateInputForAcceptedCache(
            WoundEffectBatchPlannerTests.OwnerFlavor.AfterlifeGuardian);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var effectInput = WoundEffectBatchPlannerTests
            .CreateEffectInputForAcceptedCache(prepared);
        var ownerAfterImage = effectInput.PreTurnCarriers!.AfterlifeProfiles!
            .DeepClone().AsObject();
        ownerAfterImage["profiles"]!.AsArray().Add(new JsonObject
        {
            ["actorType"] = "guardian",
            ["actorId"] = "guardian_unexplained_wound",
            ["realm"] = "Chaos Sea",
            ["displayName"] = "Unexplained sibling",
            ["activeWounds"] = new JsonArray(
                WoundContractTestData.CreateActiveWound(
                    "wound_unexplained_sibling",
                    "chaos_sea",
                    "guardian",
                    "guardian_unexplained_wound",
                    WoundCarrierCatalog.AfterlifeProfilesPath,
                    domain: "spiritual")),
            ["activeEffects"] = new JsonArray(),
            ["services"] = new JsonObject { ["revision"] = 1 }
        });
        var acceptedCarrierBaselines = effectInput.AcceptedCarrierBaselines ??
            effectInput.PreTurnCarriers;
        effectInput = effectInput with
        {
            PreTurnCarriers = effectInput.PreTurnCarriers! with
            {
                AfterlifeProfiles = ownerAfterImage
            },
            AcceptedCarrierBaselines = acceptedCarrierBaselines! with
            {
                AfterlifeProfiles = ownerAfterImage
            }
        };
        var effectResult = WoundEffectBatchPlanner.Build(
            prepared,
            effectInput,
            new WoundCommonEffectIdentityFactory("root_assembly_sibling"));
        var effect = AssertWoundCommonEffect(effectResult);
        var final = AssertWoundCommonFinal(
            WoundAcceptedTurnPlanner.Finalize(prepared, effectResult));
        var stages = new AcceptedMechanicsWoundStageBundle(
            input,
            prepared,
            effect,
            final);

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            CompleteEffectPlanForAssembly(effect.EffectPlan),
            new Dictionary<string, JsonObject>(StringComparer.Ordinal)
            {
                [WoundCarrierCatalog.AfterlifeProfilesPath] = ownerAfterImage
            },
            stages);

        Assert.False(composition.Success);
        Assert.Null(composition.WoundPublication);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_publication_agreement_invalid");
    }

    [Fact]
    public void WoundRootAssembly_RejectsFinalEffectPlanMissingNewWoundEffect()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_missing_effect",
            "root_assembly_missing_effect");
        var initial = stages.Effect.EffectPlan;
        var emptyCarriers = new EffectCarrierCatalogInput(
            null,
            null,
            null,
            null,
            null,
            null);
        var finalWithoutCreatedEffect = CopyEffectPlan(
            initial,
            activeEffects: Array.Empty<JsonObject>(),
            resourceTriggerCarriers: emptyCarriers,
            carrierAfterImages:
                new Dictionary<string, JsonObject>(StringComparer.Ordinal));

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            finalWithoutCreatedEffect,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        Assert.False(composition.Success);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_final_effect_membership_invalid");
    }

    [Fact]
    public void WoundRootAssembly_RejectsFinalPublicationCarrierMissingNewEffect()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_missing_publication_effect",
            "root_assembly_missing_publication_effect");
        var initial = stages.Effect.EffectPlan;
        var carrierAfterImages = initial.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        carrierAfterImages[EffectCarrierCatalog.PlayerPath]["activeEffects"] =
            new JsonArray();
        var finalWithoutPublicationEffect = CopyEffectPlan(
            initial,
            carrierAfterImages: carrierAfterImages);

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            finalWithoutPublicationEffect,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        Assert.False(composition.Success);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_final_effect_membership_invalid");
    }

    [Fact]
    public void WoundRootAssembly_RejectsFinalActiveEffectSetMissingNewEffect()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_missing_active_effect",
            "root_assembly_missing_active_effect");
        var finalWithoutActiveEffect = CopyEffectPlan(
            stages.Effect.EffectPlan,
            activeEffects: Array.Empty<JsonObject>());

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            finalWithoutActiveEffect,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        Assert.False(composition.Success);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_final_effect_membership_invalid");
    }

    [Fact]
    public void WoundRootAssembly_RejectsFinalIdentityMissingCreateEvidence()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_missing_identity",
            "root_assembly_missing_identity");
        var identity = stages.Effect.EffectPlan.IdentityIndexAfterImage;
        identity["entries"] = new JsonArray();
        var finalWithoutIdentity = CopyEffectPlan(
            stages.Effect.EffectPlan,
            identityIndexAfterImage: identity);

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            finalWithoutIdentity,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        Assert.False(composition.Success);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_final_effect_membership_invalid");
    }

    [Fact]
    public void WoundRootAssembly_RejectsForeignFinalEffectAuthority()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_foreign_final_authority",
            "root_assembly_foreign_final_authority");
        var foreignFinal = CopyEffectPlan(
            stages.Effect.EffectPlan,
            inputFingerprint:
                "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            foreignFinal,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        Assert.False(composition.Success);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_final_effect_authority_mismatch");
    }

    [Fact]
    public void WoundRootAssembly_RejectsChangedImmutableEffectContract()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_changed_effect_contract",
            "root_assembly_changed_effect_contract");
        var initial = stages.Effect.EffectPlan;
        var effectId = Assert.Single(stages.Effect.ApplicationResults).EffectId;
        var forgedEffect = initial.ActiveEffects.Single(effect =>
                string.Equals(
                    effect["effectId"]!.GetValue<string>(),
                    effectId,
                    StringComparison.Ordinal))
            .DeepClone().AsObject();
        forgedEffect["display"]!["name"] = "Подменённая механика раны";
        var runtimeRoot = initial.ResourceTriggerCarriers.PlayerEffects!
            .DeepClone().AsObject();
        ReplaceEffectInArray(
            runtimeRoot["activeEffects"]!.AsArray(),
            effectId,
            forgedEffect);
        var publicationRoot = initial.CarrierAfterImages[
            EffectCarrierCatalog.PlayerPath].DeepClone().AsObject();
        ReplaceEffectInArray(
            publicationRoot["activeEffects"]!.AsArray(),
            effectId,
            forgedEffect);
        var carrierAfterImages = initial.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        carrierAfterImages[EffectCarrierCatalog.PlayerPath] = publicationRoot;
        var final = CopyEffectPlan(
            initial,
            activeEffects: initial.ActiveEffects.Select(effect =>
                    string.Equals(
                        effect["effectId"]!.GetValue<string>(),
                        effectId,
                        StringComparison.Ordinal)
                        ? forgedEffect
                        : effect)
                .ToArray(),
            resourceTriggerCarriers: initial.ResourceTriggerCarriers with
            {
                PlayerEffects = runtimeRoot
            },
            carrierAfterImages: carrierAfterImages);

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            final,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        Assert.False(composition.Success);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_final_effect_membership_invalid");
    }

    [Fact]
    public void WoundRootAssembly_RejectsUnprovenLifetimeBudgetIncrease()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_lifetime_increase",
            "root_assembly_lifetime_increase");
        var initial = stages.Effect.EffectPlan;
        var effectId = Assert.Single(stages.Effect.ApplicationResults).EffectId;
        var forgedEffect = initial.ActiveEffects.Single(effect =>
                string.Equals(
                    effect["effectId"]!.GetValue<string>(),
                    effectId,
                    StringComparison.Ordinal))
            .DeepClone().AsObject();
        var lifetime = forgedEffect["lifetime"]!.AsObject();
        if (lifetime["remainingUses"] is JsonValue remainingUses &&
            remainingUses.TryGetValue<int>(out var uses))
        {
            lifetime["remainingUses"] = checked(uses + 999);
        }
        else if (lifetime["remainingTurns"] is JsonValue remainingTurns &&
                 remainingTurns.TryGetValue<int>(out var turns))
        {
            lifetime["remainingTurns"] = checked(turns + 999);
        }
        else
        {
            lifetime["remainingUses"] = 999;
        }
        var final = ReplaceEffectEverywhere(initial, effectId, forgedEffect);

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            final,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        Assert.False(composition.Success);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_final_effect_membership_invalid");
    }

    [Fact]
    public void WoundRootAssembly_RejectsHiddenFullSourceAuthorityDrift()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_source_authority_drift",
            "root_assembly_source_authority_drift");
        var initial = stages.Effect.EffectPlan;
        var entries = initial.SourceAuthority.SnapshotSameTurnWoundEntries();
        var first = Assert.Single(entries.Select(static entry => new
            {
                entry.Key.Realm,
                entry.Key.Kind,
                entry.Key.SourceId,
                entry.Materializable,
                entry.Active,
                entry.SameTurn,
                entry.SourceRef,
                entry.RequiredApplicationAuthority
            }).Distinct());
        var driftedAuthority = EffectSourceAuthority.Build(
            new EffectSourceAuthorityInput(
                Array.Empty<EffectSourceExport>(),
                new[]
                {
                    new EffectSourceExport(
                        first.Realm,
                        first.Kind,
                        first.SourceId,
                        new JsonArray(entries.Select(static entry =>
                            (JsonNode)entry.Definition.DeepClone()).ToArray()),
                        first.Materializable,
                        first.Active,
                        first.SameTurn,
                        first.SourceRef,
                        entries.SelectMany(static entry =>
                                entry.SatisfiedPredicates)
                            .ToHashSet(StringComparer.Ordinal),
                        first.RequiredApplicationAuthority)
                },
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal)
                {
                    EffectBuiltInSourceCatalog.FateShieldApplicationAuthority
                },
                WoundGroups:
                    initial.SourceAuthority.SnapshotWoundGroupAuthorities()));
        Assert.Empty(driftedAuthority.Issues);
        Assert.Equal(
            initial.SourceAuthority.CanonicalFingerprint,
            driftedAuthority.CanonicalFingerprint);
        Assert.NotEqual(
            initial.SourceAuthority.Fingerprint,
            driftedAuthority.Fingerprint);
        var forgedFinal = CopyEffectPlan(
            initial,
            sourceAuthority: driftedAuthority);

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            forgedFinal,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        Assert.False(composition.Success);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_final_effect_authority_mismatch");
    }

    [Fact]
    public void WoundCommonPlan_RejectsCarrierMapOutsideValidatedComposition()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_plan_laundering",
            "root_assembly_plan_laundering");
        var plan = CreateWoundCommonPlan(stages.Bundle, afterImageMarker: 1);
        var completedEffectPlan = CompleteEffectPlanForAssembly(
            stages.Effect.EffectPlan);
        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            completedEffectPlan,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        var exception = Record.Exception(() => new AcceptedMechanicsPlan(
            plan.InputFingerprint,
            plan.DefinitionAfterImage,
            plan.StateAfterImage,
            plan.HistoryAfterImage,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            plan.EffectIdentityAfterImage,
            plan.PendingAfterImages,
            plan.OwnerCompanionAfterImages,
            plan.BeforeImages,
            plan.TouchedPaths,
            plan.ConsumedPaths,
            plan.AuthorityFingerprints,
            plan.ResourceEvents,
            plan.ProjectionInput,
            plan.OwnerAuthority,
            completedEffectPlan,
            plan.OwnerTransitions,
            plan.PendingGmPacket,
            woundStageBundle: stages.Bundle,
            carrierComposition: composition));

        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void AfterlifeProfileUpsert_PreservesCanonicalActiveWounds()
    {
        var input = WoundEffectBatchPlannerTests.CreateInputForAcceptedCache(
            WoundEffectBatchPlannerTests.OwnerFlavor.AfterlifeGuardian);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stages = CreateWoundCommonStages(
            input,
            prepared,
            "root_assembly_profile_upsert");
        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            CompleteEffectPlanForAssembly(stages.Effect.EffectPlan),
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);
        var root = composition.WoundPublication!.CarrierAfterImages[
            WoundCarrierCatalog.AfterlifeProfilesPath];
        var existing = root["profiles"]![0]!.DeepClone().AsObject();
        var replacement = existing.DeepClone().AsObject();
        replacement.Remove("activeWounds");
        replacement["services"]!["revision"] = 7;
        var profiles = new JsonArray(existing);

        AfterlifeEntityProfileState.UpsertProfile(profiles, replacement);

        var updated = profiles[0]!.AsObject();
        Assert.Single(updated["activeWounds"]!.AsArray());
        Assert.Equal(7, updated["services"]!["revision"]!.GetValue<int>());
    }

    [Fact]
    public void WoundRootAssembly_RejectsRawPlanWithoutCompletionProof()
    {
        var stages = CreateWoundCommonStages(
            "root_assembly_raw_final",
            "root_assembly_raw_final");
        Assert.False(stages.Effect.EffectPlan.IsAcceptedBoundaryComplete);

        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            stages.Effect.EffectPlan,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            stages.Bundle);

        Assert.False(composition.Success);
        Assert.Contains(
            composition.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_effect_completion_required");
    }

    private static EffectAcceptedTurnPlan CopyEffectPlan(
        EffectAcceptedTurnPlan source,
        IReadOnlyList<JsonObject>? activeEffects = null,
        EffectCarrierCatalogInput? resourceTriggerCarriers = null,
        IReadOnlyDictionary<string, JsonObject>? carrierAfterImages = null,
        JsonObject? identityIndexAfterImage = null,
        string? inputFingerprint = null,
        EffectSourceAuthority? sourceAuthority = null) =>
        new(
            inputFingerprint ?? source.InputFingerprint,
            source.CarrierAuthorityFingerprint,
            source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint,
            source.AllocatedCombatantIds,
            source.AllocatedEffectIds,
            source.AllocatedTransitionIds,
            source.Sources,
            source.Targets,
            source.SourceBindings,
            source.DeferredReactions,
            source.ReactionExpansionCount,
            source.ReactionExpansionUsage,
            activeEffects ?? source.ActiveEffects,
            resourceTriggerCarriers ?? source.ResourceTriggerCarriers,
            sourceAuthority ?? source.SourceAuthority,
            source.TargetAuthority,
            source.EventInput,
            source.CarrierBeforeImages,
            carrierAfterImages ?? source.CarrierAfterImages,
            source.IdentityIndexBeforeImage,
            identityIndexAfterImage ?? source.IdentityIndexAfterImage,
            source.TouchedPaths,
            source.DeletedPaths,
            source.AcceptedCarrierBaselines,
            acceptedBoundaryCompletionProof:
                ReadAcceptedBoundaryCompletionProof(source),
            acceptedBoundaryBasePlanFingerprint:
                source.AcceptedBoundaryBasePlanFingerprint,
            woundApplicationRootEffectBindings:
                source.WoundApplicationRootEffectBindings);

    private static EffectAcceptedTurnPlan ReplaceEffectEverywhere(
        EffectAcceptedTurnPlan source,
        string effectId,
        JsonObject replacement)
    {
        var runtimeRoot = source.ResourceTriggerCarriers.PlayerEffects!
            .DeepClone().AsObject();
        ReplaceEffectInArray(
            runtimeRoot["activeEffects"]!.AsArray(),
            effectId,
            replacement);
        var carrierAfterImages = source.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        var publicationRoot = carrierAfterImages[EffectCarrierCatalog.PlayerPath];
        ReplaceEffectInArray(
            publicationRoot["activeEffects"]!.AsArray(),
            effectId,
            replacement);
        return CopyEffectPlan(
            source,
            activeEffects: source.ActiveEffects.Select(effect =>
                    string.Equals(
                        effect["effectId"]!.GetValue<string>(),
                        effectId,
                        StringComparison.Ordinal)
                        ? replacement
                        : effect)
                .ToArray(),
            resourceTriggerCarriers: source.ResourceTriggerCarriers with
            {
                PlayerEffects = runtimeRoot
            },
            carrierAfterImages: carrierAfterImages);
    }

    private static ResourcePendingResolutionCreationResult CreateWoundPending(
        WoundAcceptedTurnInput input,
        ResourceDefinitionCatalog definitions,
        string fullTurnFingerprint,
        string semanticTurnFingerprint)
    {
        const string fingerprint =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var binding = input.Binding;
        var effectId = "effect_wound_pending_guard";
        var eventRef = "event_wound_pending_guard";
        var triggerId = "trigger_wound_pending_guard";
        var draft = new ResourcePendingResolutionDraft(
            "bounded_receipt",
            binding.SessionId,
            binding.RequestId,
            binding.Turn,
            eventRef,
            effectId,
            new ResourcePendingAuthorityBinding("permanent", effectId),
            new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = "wound_pending_guard",
                ["definitionKey"] = "pending_guard"
            },
            new ResourcePendingAuthorityBinding(
                "permanent",
                "wound_pending_guard"),
            new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            new ResourcePendingAuthorityBinding("permanent", "player_current"),
            triggerId,
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health"),
            new ResourcePendingAuthorityBinding("permanent", "player_current"),
            ResourceOperation.Damage,
            0,
            5,
            fingerprint,
            fingerprint,
            fullTurnFingerprint,
            semanticTurnFingerprint,
            "Рана",
            "герой",
            "Здоровье",
            "урон",
            new ResourcePendingCausalAuthority(
                effectId,
                triggerId,
                eventRef,
                "event_wound_pending_trigger",
                "resource_operation_wound_pending",
                1,
                1,
                true,
                1,
                "component_wound_pending",
                null,
                fingerprint,
                fingerprint,
                0));
        return ResourcePendingResolutionState.CreatePending(
            canonicalJson: null,
            new[] { draft },
            definitions,
            () => "resource_resolution_wound_pending",
            new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));
    }

    private static void ReplaceEffectInArray(
        JsonArray effects,
        string effectId,
        JsonObject replacement)
    {
        var index = effects
            .Select((node, candidate) => (node, candidate))
            .Single(value => string.Equals(
                value.node!["effectId"]!.GetValue<string>(),
                effectId,
                StringComparison.Ordinal))
            .candidate;
        effects[index] = replacement.DeepClone();
    }
}
