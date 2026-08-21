using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceAfterlifeOwnerTests
{
    [Fact]
    public void Compose_NewAfterlifeActorMaterializesSettingDefinedResource()
    {
        var definitions = WithAfterlifeIntegrity(RequireDefinitions());
        const string actorId = "resident_soul_integrity_keeper";
        var acceptedProfile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(
            "resident",
            actorId,
            "Shining Abode",
            materializedAtTurn: 42);
        acceptedProfile["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject
            {
                ["resourceKey"] = "soul_integrity",
                ["maximum"] = 10
            })
        };
        var emptyProfiles = Profiles();

        var result = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                new AfterlifeResourceOwnerRoots(
                    emptyProfiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(2)),
                new AfterlifeResourceOwnerRoots(
                    Profiles(acceptedProfile),
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(2))));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var owner = Assert.Single(
            result.Authority!.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeActor);
        Assert.True(owner.SameTurn);
        Assert.Equal(
            acceptedProfile[ActorMaterializationContract.PropertyName]!["materializationId"]!
                .GetValue<string>(),
            owner.SameTurnRef);
        Assert.Equal(actorId, owner.Key.ResourceOwnerId);
        Assert.Contains("soul_integrity", owner.ResourceCapabilities);
        var draft = Assert.Single(result.CapacityDrafts);
        Assert.Equal(owner.Key.Realm, draft.Coordinate.Realm);
        Assert.Equal(owner.Key.OwnerKind, draft.Coordinate.OwnerKind);
        Assert.Equal(owner.Key.ResourceOwnerId, draft.Coordinate.ResourceOwnerId);
        Assert.Equal("soul_integrity", draft.Coordinate.ResourceKey);
        Assert.Equal(10m, draft.AcceptedMaximum);

        var afterImage = result.OwnerCompanionAfterImages[
            AfterlifeEntityProfileState.StatePath];
        var canonicalProfile = Assert.IsType<JsonObject>(
            Assert.Single(afterImage[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray()));
        Assert.Equal(actorId, canonicalProfile["actorId"]!.GetValue<string>());
        Assert.False(canonicalProfile.ContainsKey("actorRef"));
        Assert.False(canonicalProfile.ContainsKey("resourceMaterialization"));
    }

    [Fact]
    public async Task AcceptedTurn_ConflictStartUsesTheCommonOwnerPlanAndPublication()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            bootstrap.Definitions!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.StatePath,
            bootstrap.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.HistoryPath,
            bootstrap.History!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            AfterlifeEntityProfileState.StatePath,
            Profiles(PlayerSoulProfile("Chaos Sea")).ToJsonString());
        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath,
            AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["currentRealm"] = "Chaos Sea",
                [AfterlifeSpiritualConflictState.SoulStateProfileProperty] = new JsonObject
                {
                    [AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 2
                }
            }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42,
            currentRealm: "Chaos Sea");

        var rawConflict = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        rawConflict[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeStart,
            ["conflictState"] = ActiveConflict(
                "conflict_resource_common_publication",
                "Chaos Sea")
        };
        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath,
            rawConflict.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out _,
            out var planning));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planning.Plan);
        Assert.Contains(
            plan.OwnerAuthority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     entry.Key.ResourceOwnerId == "player_soul");
        var opposition = Assert.Single(
            plan.OwnerAuthority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        Assert.True(opposition.SameTurn);
        var state = ResourceStateContract.ParseCanonical(
            plan.StateAfterImage.ToJsonString(),
            bootstrap.Definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var playerActionPoints = Assert.Single(
            state.Ledger!.Entries,
            entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor);
        Assert.Equal(8m, playerActionPoints.Current);
        Assert.Equal(8m, playerActionPoints.Maximum);
        var actionPoints = Assert.Single(
            state.Ledger.Entries,
            entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        Assert.Equal(ResourceOwnerKind.AfterlifeConflictSide, actionPoints.Coordinate.OwnerKind);
        Assert.Equal(opposition.Key.ResourceOwnerId, actionPoints.Coordinate.ResourceOwnerId);
        Assert.Equal("spiritual_action_points", actionPoints.Coordinate.ResourceKey);
        Assert.Equal(6m, actionPoints.Current);
        Assert.Equal(6m, actionPoints.Maximum);
        Assert.Contains(
            AfterlifeSpiritualConflictState.StatePath,
            plan.OwnerCompanionAfterImages.Keys);

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var published = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);

        Assert.Same(plan, published);
        var canonicalConflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath));
        Assert.False(canonicalConflict.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        var canonicalActive = Assert.IsType<JsonObject>(canonicalConflict["activeConflict"]);
        Assert.False(canonicalActive.ContainsKey("actionEconomy"));
        Assert.False(Assert.IsType<JsonObject>(canonicalActive["oppositionSide"])
            .ContainsKey("resourceMaterialization"));
        Assert.Equal(
            opposition.Key.ResourceOwnerId,
            canonicalActive["resourceOwnerBindings"]!["opposition"]!["resourceOwnerId"]!
                .GetValue<string>());

        var canonicalIssues = await context.Validator
            .ValidateAcceptedTurnCanonicalResourceMaterializationAsync();
        Assert.DoesNotContain(
            canonicalIssues,
            issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task AcceptedTurns_SpiritFocusChangeReconfiguresPersistentActionPoints()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            bootstrap.Definitions!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.StatePath,
            bootstrap.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.HistoryPath,
            bootstrap.History!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            AfterlifeEntityProfileState.StatePath,
            Profiles(PlayerSoulProfile("Chaos Sea")).ToJsonString());
        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath,
            AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/meta/soul_state.json",
            SoulState(spiritFocusTier: 2).ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42,
            currentRealm: "Chaos Sea");

        var initialIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(initialIssues, issue => issue.Severity == IssueSeverity.Error);
        await using (var firstLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.NotNull(await context.Normalizer.BindTo(firstLease)
                .NormalizeAcceptedMechanicsAsync(backups: null));
        }

        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            currentRealm: "Chaos Sea");
        await context.WriteExactJsonAsync(
            "game_state/meta/soul_state.json",
            SoulState(spiritFocusTier: 4).ToJsonString());

        var reconfigureIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(reconfigureIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out _,
            out var planning));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planning.Plan);
        var state = ResourceStateContract.ParseCanonical(
            plan.StateAfterImage.ToJsonString(),
            bootstrap.Definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var actionPoints = Assert.Single(state.Ledger!.Entries);
        Assert.Equal(8m, actionPoints.Current);
        Assert.Equal(12m, actionPoints.Maximum);
        var history = ResourceHistoryState.ParseCanonical(
            plan.HistoryAfterImage.ToJsonString(),
            bootstrap.Definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var reconfigure = Assert.Single(
            history.History!.Transitions,
            transition => transition.Turn == 43 &&
                          transition.Operation == ResourceTransitionOperation.Reconfigure);
        Assert.Equal(8m, reconfigure.BeforeState!.Maximum);
        Assert.Equal(12m, reconfigure.AfterState!.Maximum);
        Assert.Equal(ResourceCapacityDisposition.ClampToNewMaximum, reconfigure.CapacityDisposition);
    }

    [Fact]
    public void Compose_ConflictStartKeepsPersistentActorAndAllocatesScopedOppositionOwner()
    {
        var definitions = RequireDefinitions();
        var profiles = Profiles(PlayerSoulProfile("Chaos Sea"));
        var preTurn = new AfterlifeResourceOwnerRoots(
            profiles,
            AfterlifeSpiritualConflictState.CreateDefaultRoot(),
            SoulState(2));
        var acceptedConflict = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        acceptedConflict["activeConflict"] = ActiveConflict(
            "conflict_resource_owner_start",
            "Chaos Sea");

        var result = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                preTurn,
                new AfterlifeResourceOwnerRoots(profiles, acceptedConflict, SoulState(2))));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        var playerSoul = Assert.Single(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeActor);
        Assert.Equal("chaos_sea", playerSoul.Key.Realm);
        Assert.Equal("player_soul", playerSoul.Key.ResourceOwnerId);
        Assert.False(playerSoul.SameTurn);
        Assert.Contains("spiritual_action_points", playerSoul.ResourceCapabilities);

        var opposition = Assert.Single(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        Assert.Equal("chaos_sea", opposition.Key.Realm);
        Assert.StartsWith("afterlife_conflict_side_", opposition.Key.ResourceOwnerId);
        Assert.True(opposition.SameTurn);
        Assert.StartsWith("afterlife_conflict_side_ref_", opposition.SameTurnRef);
        Assert.Contains("spiritual_action_points", opposition.ResourceCapabilities);
        var capacity = Assert.Single(
            result.CapacityDrafts,
            draft => draft.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        Assert.Equal(opposition.Key.ResourceOwnerId, capacity.Coordinate.ResourceOwnerId);
        Assert.Equal(ResourceOwnerKind.AfterlifeConflictSide, capacity.Coordinate.OwnerKind);
        Assert.Equal("spiritual_action_points", capacity.Coordinate.ResourceKey);
        Assert.Equal(6m, capacity.AcceptedMaximum);

        var conflictAfterImage = Assert.IsType<JsonObject>(
            result.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]);
        var active = Assert.IsType<JsonObject>(conflictAfterImage["activeConflict"]);
        var bindings = Assert.IsType<JsonObject>(active["resourceOwnerBindings"]);
        var oppositionBinding = Assert.IsType<JsonObject>(bindings["opposition"]);
        Assert.Equal(
            opposition.Key.ResourceOwnerId,
            oppositionBinding["resourceOwnerId"]!.GetValue<string>());
        Assert.False(active.ContainsKey("actionEconomy"));
    }

    [Fact]
    public void Compose_ConflictStartRejectsSubmittedLegacyActionEconomy()
    {
        var definitions = RequireDefinitions();
        var profiles = Profiles(PlayerSoulProfile("Chaos Sea"));
        var acceptedConflict = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        var active = ActiveConflict(
            "conflict_resource_owner_legacy_pool",
            "Chaos Sea");
        active["actionEconomy"] = new JsonObject
        {
            ["player"] = new JsonObject { ["current"] = 6, ["max"] = 6 },
            ["opposition"] = new JsonObject { ["current"] = 6, ["max"] = 6 }
        };
        acceptedConflict["activeConflict"] = active;

        var result = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(2)),
                new AfterlifeResourceOwnerRoots(profiles, acceptedConflict, SoulState(2))));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     "resource_owner_afterlife_conflict_legacy_action_economy_forbidden");
        Assert.Null(result.Authority);
    }

    [Fact]
    public void Compose_ConflictCloseRetiresOnlyScopedOwner()
    {
        var definitions = RequireDefinitions();
        var profiles = Profiles(PlayerSoulProfile("Shining Abode"));
        var preTurnConflict = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        var active = ActiveConflict(
            "conflict_resource_owner_close",
            "Shining Abode");
        Assert.IsType<JsonObject>(active["oppositionSide"])
            .Remove("resourceMaterialization");
        active["resourceOwnerBindings"] = new JsonObject
        {
            ["opposition"] = new JsonObject
            {
                ["resourceOwnerId"] = "afterlife_conflict_side_existing"
            }
        };
        preTurnConflict["activeConflict"] = active;

        var result = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                new AfterlifeResourceOwnerRoots(profiles, preTurnConflict, SoulState(2)),
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(2))));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        Assert.Contains(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     entry.Key.ResourceOwnerId == "player_soul");
        Assert.DoesNotContain(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        var terminal = Assert.Single(result.TerminalOwners);
        Assert.Equal("shining_abode", terminal.Realm);
        Assert.Equal(ResourceOwnerKind.AfterlifeConflictSide, terminal.OwnerKind);
        Assert.Equal("afterlife_conflict_side_existing", terminal.ResourceOwnerId);
        Assert.DoesNotContain(
            result.TerminalOwners,
            owner => owner.OwnerKind == ResourceOwnerKind.AfterlifeActor);
    }

    [Fact]
    public void Compose_PersistentGuardianExportsOnlyGuardianGachaCapability()
    {
        var definitions = RequireDefinitions();
        var profiles = Profiles(
            PlayerSoulProfile("Chaos Sea"),
            AfterlifeProfile(
                actorType: "guardian",
                actorId: "guardian_vesna",
                realm: "Chaos Sea"));

        var result = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(2)),
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(2))));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var guardian = Assert.Single(
            result.Authority!.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     entry.Key.ResourceOwnerId == "guardian_vesna");
        Assert.Equal(new[] { "gacha_attempts" }, guardian.ResourceCapabilities);
        var playerSoul = Assert.Single(
            result.Authority.Entries.Values,
            entry => entry.Key.ResourceOwnerId == "player_soul");
        Assert.Equal(
            new[] { "blessing_rerolls", "spiritual_action_points" },
            playerSoul.ResourceCapabilities.OrderBy(static key => key, StringComparer.Ordinal));
    }

    [Fact]
    public void Compose_PersistentGuardianCreatesReturnCapacityFromGuardianState()
    {
        var definitions = RequireDefinitions();
        var profiles = Profiles(
            PlayerSoulProfile("Chaos Sea"),
            AfterlifeProfile(
                actorType: "guardian",
                actorId: "guardian_vesna",
                realm: "Chaos Sea"));
        var guardians = GuardianReturnState(
            "guardian_vesna",
            "chaos_return_9",
            reputation: 50,
            abodePower: 0);

        var result = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(2),
                    guardians: guardians),
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(2),
                    guardians: guardians)));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var capacity = Assert.Single(
            result.CapacityDrafts,
            draft => draft.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     draft.Coordinate.ResourceOwnerId == "guardian_vesna" &&
                     draft.Coordinate.ResourceKey == "gacha_attempts");
        Assert.Equal("chaos_sea", capacity.Coordinate.Realm);
        Assert.Equal(2m, capacity.AcceptedMaximum);
        Assert.Equal(
            "afterlife_return_gacha_attempts_v1",
            capacity.ResolvedCapacity.Capacity!.Binding.AuthorityKey);
        Assert.Equal(
            2m,
            capacity.ResolvedCapacity.Capacity.Initialization!.Current);
        Assert.Equal("owner_capacity_cycle", capacity.SourceEvidence.SourceKind);
        Assert.Equal("chaos_return_9", capacity.SourceEvidence.SourceId);
    }

    [Fact]
    public void Compose_RealmChangeSuspendsPriorActorBindingAndCreatesCurrentBinding()
    {
        var definitions = RequireDefinitions();
        var preTurnProfile = PlayerSoulProfile("Chaos Sea");
        var acceptedProfile = preTurnProfile.DeepClone().AsObject();
        acceptedProfile["realm"] = "Shining Abode";
        var soulState = SoulState(spiritFocusTier: 3);

        var result = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                new AfterlifeResourceOwnerRoots(
                    Profiles(preTurnProfile),
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    soulState),
                new AfterlifeResourceOwnerRoots(
                    Profiles(acceptedProfile),
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    soulState)));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        var chaos = Assert.Single(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     entry.Key.Realm == "chaos_sea");
        Assert.Equal(ResourceOwnerLifecycle.Suspended, chaos.Lifecycle);
        Assert.False(chaos.SameTurn);
        var shining = Assert.Single(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     entry.Key.Realm == "shining_abode");
        Assert.Equal(ResourceOwnerLifecycle.Active, shining.Lifecycle);
        Assert.True(shining.SameTurn);
        Assert.StartsWith("afterlife_actor_realm_ref_", shining.SameTurnRef);

        var profileAfterImage = Assert.IsType<JsonObject>(
            result.OwnerCompanionAfterImages[AfterlifeEntityProfileState.StatePath]);
        var profile = Assert.IsType<JsonObject>(
            Assert.Single(profileAfterImage[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray()));
        var bindings = profile["resourceOwnerBindings"]!.AsArray()
            .OfType<JsonObject>()
            .OrderBy(binding => binding["realm"]!.GetValue<string>(), StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, bindings.Length);
        Assert.Equal("chaos_sea", bindings[0]["realm"]!.GetValue<string>());
        Assert.Equal("suspended", bindings[0]["state"]!.GetValue<string>());
        Assert.Equal("shining_abode", bindings[1]["realm"]!.GetValue<string>());
        Assert.Equal("active", bindings[1]["state"]!.GetValue<string>());

        var capacities = result.CapacityDrafts
            .Where(draft => draft.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                            draft.Coordinate.ResourceOwnerId == "player_soul" &&
                            draft.Coordinate.ResourceKey == "spiritual_action_points")
            .OrderBy(draft => draft.Coordinate.Realm, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, capacities.Length);
        Assert.Equal("chaos_sea", capacities[0].Coordinate.Realm);
        Assert.Equal("shining_abode", capacities[1].Coordinate.Realm);
        Assert.All(capacities, capacity => Assert.Equal(10m, capacity.AcceptedMaximum));
    }

    [Fact]
    public void Compose_ExistingPlayerSoulEmitsCurrentFormulaCapacityDraft()
    {
        var definitions = RequireDefinitions();
        var profile = PlayerSoulProfile("Chaos Sea");
        var preTurnSoul = SoulState(spiritFocusTier: 2);
        var acceptedSoul = SoulState(spiritFocusTier: 4);

        var result = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                new AfterlifeResourceOwnerRoots(
                    Profiles(profile),
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    preTurnSoul),
                new AfterlifeResourceOwnerRoots(
                    Profiles(profile.DeepClone().AsObject()),
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    acceptedSoul)));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var capacity = Assert.Single(
            result.CapacityDrafts,
            draft => draft.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     draft.Coordinate.ResourceOwnerId == "player_soul" &&
                     draft.Coordinate.ResourceKey == "spiritual_action_points");
        Assert.Equal("chaos_sea", capacity.Coordinate.Realm);
        Assert.Equal(12m, capacity.AcceptedMaximum);
        Assert.Equal("owner_capacity_state", capacity.SourceEvidence.SourceKind);
        Assert.Equal("player_soul", capacity.SourceEvidence.SourceId);
    }

    [Fact]
    public void Compose_SameShiningReturnEmitsCurrentFormulaCapacityDraft()
    {
        var definitions = RequireDefinitions();
        var profiles = Profiles(PlayerSoulProfile("Shining Abode"));
        var preTurnShining = ShiningReturnState(
            "shining_return_8",
            radianceTier: 1,
            resourceOwnerId: "afterlife_scope_existing");
        var acceptedShining = preTurnShining.DeepClone().AsObject();
        Assert.IsType<JsonObject>(acceptedShining["radiance"])["tier"] = 3;

        var result = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(3),
                    preTurnShining),
                new AfterlifeResourceOwnerRoots(
                    profiles.DeepClone().AsObject(),
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(3),
                    acceptedShining)));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var capacity = Assert.Single(
            result.CapacityDrafts,
            draft => draft.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeScope &&
                     draft.Coordinate.ResourceOwnerId == "afterlife_scope_existing" &&
                     draft.Coordinate.ResourceKey == "gacha_attempts");
        Assert.Equal(4m, capacity.AcceptedMaximum);
        Assert.Equal("owner_capacity_cycle", capacity.SourceEvidence.SourceKind);
        Assert.Equal("shining_return_8", capacity.SourceEvidence.SourceId);
    }

    [Fact]
    public void Compose_ShiningReturnCreatesScopedGachaOwnerWithoutLegacyCounters()
    {
        var definitions = RequireDefinitions();
        var profiles = Profiles(PlayerSoulProfile("Shining Abode"));
        var preTurnShining = ShiningReturnState(returnCycleId: null, radianceTier: 2);
        var acceptedShining = ShiningReturnState("shining_return_8", radianceTier: 2);

        var result = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(3),
                    preTurnShining),
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(3),
                    acceptedShining)));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var scope = Assert.Single(
            result.Authority!.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeScope);
        Assert.Equal("shining_abode", scope.Key.Realm);
        Assert.True(scope.SameTurn);
        Assert.StartsWith("afterlife_scope_ref_", scope.SameTurnRef);
        Assert.Equal(new[] { "gacha_attempts" }, scope.ResourceCapabilities);
        var capacity = Assert.Single(
            result.CapacityDrafts,
            draft => draft.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeScope);
        Assert.Equal(scope.Key.ResourceOwnerId, capacity.Coordinate.ResourceOwnerId);
        Assert.Equal("gacha_attempts", capacity.Coordinate.ResourceKey);
        Assert.Equal(3m, capacity.AcceptedMaximum);

        var afterImage = Assert.IsType<JsonObject>(
            result.OwnerCompanionAfterImages[ShiningAbodeState.StatePath]);
        var gacha = Assert.IsType<JsonObject>(afterImage["gachaSystem"]);
        Assert.False(gacha.ContainsKey("chargesPerReturn"));
        Assert.False(gacha.ContainsKey("chargesUsedThisReturn"));
        var binding = Assert.IsType<JsonObject>(
            afterImage["resourceOwnerBindings"]?["gachaReturn"]);
        Assert.Equal("shining_return_8", binding["returnCycleId"]!.GetValue<string>());
        Assert.Equal(scope.Key.ResourceOwnerId, binding["resourceOwnerId"]!.GetValue<string>());
    }

    [Fact]
    public void Compose_ShiningReturnReplacementRetiresOnlyPriorScope()
    {
        var definitions = RequireDefinitions();
        var profiles = Profiles(PlayerSoulProfile("Shining Abode"));
        var preTurnShining = ShiningReturnState(
            "shining_return_7",
            radianceTier: 1,
            resourceOwnerId: "afterlife_scope_existing");
        var acceptedShining = preTurnShining.DeepClone().AsObject();
        var acceptedGacha = Assert.IsType<JsonObject>(acceptedShining["gachaSystem"]);
        acceptedGacha["currentReturnCycleId"] = "shining_return_8";

        var result = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(3),
                    preTurnShining),
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                    SoulState(3),
                    acceptedShining)));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Contains(
            result.TerminalOwners,
            owner => owner.OwnerKind == ResourceOwnerKind.AfterlifeScope &&
                     owner.ResourceOwnerId == "afterlife_scope_existing");
        Assert.DoesNotContain(
            result.TerminalOwners,
            owner => owner.OwnerKind == ResourceOwnerKind.AfterlifeActor);
        var currentScope = Assert.Single(
            result.Authority!.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeScope);
        Assert.NotEqual("afterlife_scope_existing", currentScope.Key.ResourceOwnerId);
        Assert.True(currentScope.SameTurn);
        Assert.Equal(
            2m,
            Assert.Single(
                result.CapacityDrafts,
                draft => draft.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeScope)
                .AcceptedMaximum);
    }

    private static ResourceDefinitionCatalog RequireDefinitions()
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        return Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
    }

    private static ResourceDefinitionCatalog WithAfterlifeIntegrity(
        ResourceDefinitionCatalog definitions)
    {
        var proposal = new JsonObject
        {
            ["resourceKey"] = "soul_integrity",
            ["definitionVersion"] = 1,
            ["displayName"] = "Целостность души",
            ["numericKind"] = "integer",
            ["unit"] = "point",
            ["quantum"] = 1,
            ["minimumPolicy"] = new JsonObject
            {
                ["kind"] = "definition_fixed",
                ["value"] = 0
            },
            ["capacityPolicy"] = new JsonObject
            {
                ["kind"] = "instance_fixed"
            },
            ["initializationPolicy"] = new JsonObject
            {
                ["kind"] = "maximum"
            },
            ["allowedOwnerKinds"] = new JsonArray("afterlife_actor"),
            ["allowedOperations"] = new JsonArray("damage", "restore"),
            ["defaultFloorPolicy"] = "clamp_to_minimum",
            ["defaultCapPolicy"] = "clamp_to_maximum",
            ["visibility"] = "owner_visible"
        };
        using var document = JsonDocument.Parse(proposal.ToJsonString());
        var materialized = ResourceDefinitionCatalog.MaterializeProposal(
            document.RootElement,
            definitions,
            createdAtTurn: 1,
            createdEventRef: "turn_1:resource_definition:1",
            static () => new ResourceDefinitionIdentity(
                "resource_definition_soul_integrity",
                "resource_definition_seal_soul_integrity"));
        Assert.True(
            materialized.IsValid,
            string.Join(Environment.NewLine, materialized.Issues));
        return definitions.With(materialized.Definition!);
    }

    private static JsonObject Profiles(params JsonObject[] profiles) =>
        new()
        {
            [AfterlifeEntityProfileState.ProfilesProperty] =
                new JsonArray(profiles.Select(static profile => (JsonNode)profile).ToArray())
        };

    private static JsonObject PlayerSoulProfile(string realm) =>
        new()
        {
            ["actorType"] = "player_soul",
            ["actorId"] = "player_soul",
            ["displayName"] = "Душа игрока",
            ["realm"] = realm,
            ["resourceOwnerBindings"] = new JsonArray
            {
                new JsonObject
                {
                    ["realm"] = realm == "Chaos Sea" ? "chaos_sea" : "shining_abode",
                    ["resourceOwnerId"] = "player_soul",
                    ["state"] = "active"
                }
            }
        };

    private static JsonObject AfterlifeProfile(
        string actorType,
        string actorId,
        string realm) =>
        new()
        {
            ["actorType"] = actorType,
            ["actorId"] = actorId,
            ["displayName"] = actorId,
            ["realm"] = realm,
            ["resourceOwnerBindings"] = new JsonArray
            {
                new JsonObject
                {
                    ["realm"] = realm == "Chaos Sea" ? "chaos_sea" : "shining_abode",
                    ["resourceOwnerId"] = actorId,
                    ["state"] = "active"
                }
            }
        };

    private static JsonObject SoulState(int spiritFocusTier) =>
        new()
        {
            [AfterlifeSpiritualConflictState.SoulStateProfileProperty] = new JsonObject
            {
                [AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = spiritFocusTier
            }
        };

    private static JsonObject ShiningReturnState(
        string? returnCycleId,
        int radianceTier,
        string? resourceOwnerId = null)
    {
        var root = ShiningAbodeState.CreateDefaultState();
        Assert.IsType<JsonObject>(root["radiance"])["tier"] = radianceTier;
        var gacha = Assert.IsType<JsonObject>(root["gachaSystem"]);
        gacha["currentReturnCycleId"] = returnCycleId ?? string.Empty;
        gacha["chargesPerReturn"] = ShiningAbodeState
            .GetShiningGachaChargesPerReturn(radianceTier);
        gacha["chargesUsedThisReturn"] = 0;
        if (returnCycleId != null && resourceOwnerId != null)
        {
            root["resourceOwnerBindings"] = new JsonObject
            {
                ["gachaReturn"] = new JsonObject
                {
                    ["returnCycleId"] = returnCycleId,
                    ["resourceOwnerId"] = resourceOwnerId
                }
            };
        }
        return root;
    }

    private static JsonObject GuardianReturnState(
        string guardianId,
        string returnCycleId,
        int reputation,
        int abodePower)
    {
        var guardian = new JsonObject
        {
            ["guardianId"] = guardianId,
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = reputation
            },
            ["abodePower"] = new JsonObject
            {
                ["currentPower"] = abodePower
            },
            ["gachaSystem"] = new JsonObject
            {
                ["currentReturnCycleId"] = returnCycleId,
                ["gachaHistory"] = new JsonArray()
            }
        };
        return new JsonObject
        {
            ["guardians"] = new JsonArray(guardian),
            ["activeGuardian"] = guardian.DeepClone()
        };
    }

    private static JsonObject ActiveConflict(string conflictId, string realm) =>
        new()
        {
            ["conflictId"] = conflictId,
            ["realm"] = realm,
            ["status"] = "active",
            ["resolutionState"] = "active",
            ["playerSide"] = new JsonObject(),
            ["oppositionSide"] = new JsonObject
            {
                ["resourceMaterialization"] = new JsonObject
                {
                    ["resources"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["resourceKey"] = "spiritual_action_points",
                            ["maximum"] = 6
                        }
                    }
                }
            }
        };
}
