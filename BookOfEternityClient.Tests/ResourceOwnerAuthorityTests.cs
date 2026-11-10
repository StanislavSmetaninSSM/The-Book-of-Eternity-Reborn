using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceOwnerAuthorityTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task AcceptedCompositionCarriesVerifiedHistoryWithoutImportingCurrentOwnersOrSpendPermission()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var retired = new ResourceOwnerKey("chaos_sea", ResourceOwnerKind.AfterlifeConflictSide, "closed_conflict_side");
        var coordinate = new ResourceCoordinate(retired.Realm, retired.OwnerKind, retired.ResourceOwnerId, "spiritual_action_points");
        var snapshot = new ResourceStateSnapshot(6, 6,
            new(ResourceCapacityKind.RegisteredFormula, ResourceCapacityFormulaCatalog.AfterlifeSpiritualActionPointsV1, FingerprintA),
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition("initialize_closed_side", "initialize_closed_side", "turn_1:closed_side",
            "owner_lifecycle", "closed_conflict", ResourceMutationPhase.RegisteredSystemOutcome, 100, 0,
            coordinate, ResourceTransitionOperation.Initialize, 0, 0, ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.InitializeFromDefinition, null, snapshot,
            new("owner_lifecycle", "closed_conflict", FingerprintA), FingerprintA, null, 1);
        var retire = initialize with
        {
            TransitionId = "retire_closed_side", OperationId = "retire_closed_side", EventRef = "turn_2:closed_side",
            Operation = ResourceTransitionOperation.Retire, CapacityDisposition = null,
            BeforeState = snapshot, AfterState = null, Turn = 2
        };
        var history = ResourceHistoryState.CreateValidated(new[] { initialize, retire }, definitions);
        Assert.True(history.IsValid, string.Join(";", history.Issues));
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        var inventory = new JsonObject { ["items"] = new JsonArray(item), ["equippedItems"] = new JsonObject() }.ToJsonString();
        var verified = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions,
            path => Task.FromResult<string?>(path == InventoryEquipmentService.ItemsPath ? inventory :
                path == "game_state/meta/soul_state.json" ? "{\"currentRealm\":\"Mortal World\"}" : null),
            state: null, history: history.History, CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(verified.IsValid, string.Join(";", verified.Issues));
        Assert.Contains(retired, verified.Authority!.ExportHistoricalOwners());
        Assert.Contains(verified.Authority.Entries.Keys, key => key.OwnerKind == ResourceOwnerKind.Item &&
            key.ResourceOwnerId == MortalItemTestFixture.ItemId);
        var emptyCurrent = Build(Array.Empty<ResourceOwnerExport>());
        var current = new ResourceOwnerCompositionResult(emptyCurrent, new Dictionary<string, JsonObject>(),
            Array.Empty<AcceptedMechanicsOwnerTransition>(), null, Array.Empty<ResourceOwnerCapacityDraft>(),
            Array.Empty<ResourceOwnerKey>(), Array.Empty<ValidationIssue>());
        var combined = ResourceOwnerComposition.Combine(verified.Authority.ExportHistoricalOwners(), current);
        Assert.True(combined.IsValid, string.Join(";", combined.Issues));
        Assert.Empty(combined.TerminalOwners);
        Assert.Empty(combined.Authority!.Entries);
        Assert.Empty(combined.Authority.ValidateCanonicalAgreement(new ResourceStateLedger(Array.Empty<ResourceStateEntry>()), history.History!));
        Assert.Contains(ResolveId(combined.Authority, retired.Realm, retired.OwnerKind, retired.ResourceOwnerId,
            coordinate.ResourceKey).Issues, issue => issue.Code == "resource_owner_historical");
        Assert.False(ResolveId(combined.Authority, "mortal_world", ResourceOwnerKind.Item, MortalItemTestFixture.ItemId, "durability").Success);
        var live = new ResourceStateLedger(new[] { new ResourceStateEntry(coordinate, 6, 6, snapshot.CapacityBinding,
            ResourceLifecycleState.Active, new(1, initialize.EventRef, initialize.TransitionId, initialize.EventRef, 1)) });
        Assert.Contains(combined.Authority.ValidateCanonicalAgreement(live, history.History!),
            issue => issue.FilePath.StartsWith("resourceState.entries", StringComparison.Ordinal));
        var overlap = current with { Authority = Build(Array.Empty<ResourceOwnerExport>(), historical: new[] { retired }) };
        Assert.True(ResourceOwnerComposition.Combine(verified.Authority.ExportHistoricalOwners(), overlap).IsValid);
        Assert.Contains(ResourceOwnerComposition.Combine(new[] { retired, retired }, current).Issues,
            issue => issue.Code == "resource_owner_identity_duplicate");
        Assert.Contains(ResourceOwnerComposition.Combine(new[] { retired, retired }, overlap).Issues,
            issue => issue.Code == "resource_owner_identity_duplicate");
        Assert.Contains(ResourceOwnerComposition.Combine(Array.Empty<ResourceOwnerKey>(), overlap, overlap).Issues,
            issue => issue.Code == "resource_owner_identity_duplicate");
        Assert.Contains(ResourceOwnerComposition.Combine(new[] { retired with { ResourceOwnerId = "CLOSED_CONFLICT_SIDE" } }, overlap).Issues,
            issue => issue.Code == "resource_owner_identity_confusable");
        var reused = current with { Authority = Build(new[] { Owner(retired.Realm, retired.OwnerKind, retired.ResourceOwnerId, coordinate.ResourceKey) }) };
        Assert.Contains(ResourceOwnerComposition.Combine(verified.Authority.ExportHistoricalOwners(), reused).Issues,
            issue => issue.Code == "resource_owner_historical_conflict");
    }

    [Theory]
    [InlineData("identical_cross_carrier", true)]
    [InlineData("divergent_cross_carrier", false)]
    [InlineData("same_section_duplicate", false)]
    public void MortalComposition_GroupsOnlyIdenticalPermanentNpcCrossCarrierMirrors(
        string mutation,
        bool expectedValid)
    {
        var npc = new JsonObject
        {
            ["NPCId"] = "npc_field_medic",
            ["displayName"] = "Field medic"
        };
        var root = new JsonObject
        {
            ["NPCsInScene"] = new JsonArray(npc.DeepClone()),
            ["UpdateNPCs"] = mutation == "same_section_duplicate"
                ? new JsonArray()
                : new JsonArray(npc.DeepClone())
        };
        if (mutation == "same_section_duplicate")
            root["NPCsInScene"]!.AsArray().Add(npc.DeepClone());
        if (mutation == "divergent_cross_carrier")
            root["UpdateNPCs"]![0]!["displayName"] = "Divergent medic";
        var items = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            null, null, null, null, null,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal)));

        var result = MortalResourceOwnerComposer.ComposeCanonical(
            ResourceDefinitionCatalog.CreateBuiltIn(),
            new MortalResourceOwnerRoots(
                root,
                new JsonObject { ["enemiesData"] = new JsonArray() },
                new JsonObject { ["alliesData"] = new JsonArray() },
                new JsonObject { ["vehicles"] = new JsonArray() }),
            items);

        Assert.Equal(expectedValid, result.IsValid);
        if (expectedValid)
        {
            Assert.Single(result.Authority!.Entries, pair =>
                pair.Key.OwnerKind == ResourceOwnerKind.Npc &&
                pair.Key.ResourceOwnerId == "npc_field_medic");
        }
        else
        {
            Assert.Contains(result.Issues, issue =>
                issue.Code == "resource_owner_npc_identity_ambiguous");
        }
    }

    [Fact]
    public void MortalComposition_RejectsThirdIdenticalNpcCopyInSamePreTurnCarrier()
    {
        var root = NpcMirrorRoot();
        root["NPCsInScene"]!.AsArray().Add(
            root["NPCsInScene"]![0]!.DeepClone());

        var result = MortalResourceOwnerComposer.ComposeCanonical(
            ResourceDefinitionCatalog.CreateBuiltIn(),
            new MortalResourceOwnerRoots(
                root,
                new JsonObject { ["enemiesData"] = new JsonArray() },
                new JsonObject { ["alliesData"] = new JsonArray() },
                new JsonObject { ["vehicles"] = new JsonArray() }),
            EmptyItemCatalog());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_owner_npc_identity_ambiguous");
    }

    [Fact]
    public void MortalComposition_RejectsThirdIdenticalNpcCopyInSameAcceptedCarrier()
    {
        var preTurn = NpcMirrorRoot();
        var accepted = NpcMirrorRoot();
        accepted["NPCsInScene"]!.AsArray().Add(
            accepted["NPCsInScene"]![0]!.DeepClone());

        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                new MortalResourceOwnerRoots(
                    preTurn,
                    new JsonObject { ["enemiesData"] = new JsonArray() },
                    new JsonObject { ["alliesData"] = new JsonArray() },
                    new JsonObject { ["vehicles"] = new JsonArray() }),
                new MortalResourceOwnerRoots(
                    accepted,
                    new JsonObject { ["enemiesData"] = new JsonArray() },
                    new JsonObject { ["alliesData"] = new JsonArray() },
                    new JsonObject { ["vehicles"] = new JsonArray() })));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_owner_npc_identity_ambiguous");
    }

    [Theory]
    [InlineData("mortal_world", "Player", "player_current", "health")]
    [InlineData("mortal_world", "Npc", "npc_alpha", "health")]
    [InlineData("mortal_world", "Combatant", "combatant_alpha", "poise")]
    [InlineData("mortal_world", "CombatGroupMember", "member_alpha", "health")]
    [InlineData("mortal_world", "Vehicle", "vehicle_alpha", "health")]
    [InlineData("mortal_world", "Item", "item_alpha", "charges")]
    [InlineData("chaos_sea", "AfterlifeActor", "actor_alpha", "action_points")]
    [InlineData("chaos_sea", "AfterlifeConflictSide", "side_alpha", "action_points")]
    [InlineData("shining_abode", "AfterlifeScope", "scope_alpha", "gacha_attempts")]
    public void Resolve_EveryOwnerFamilyUsesExactActiveCapabilityAuthority(
        string realm,
        string kindToken,
        string ownerId,
        string resourceKey)
    {
        var kind = Enum.Parse<ResourceOwnerKind>(kindToken);
        var authority = Build(new[] { Owner(realm, kind, ownerId, resourceKey) });

        var result = authority.Resolve(new ResourceOwnerRequest(
            realm,
            kind,
            resourceKey,
            ResourceOwnerId: ownerId,
            OwnerRef: null));

        Assert.Empty(authority.Issues);
        Assert.True(result.Success);
        Assert.Equal(new ResourceOwnerKey(realm, kind, ownerId), result.Entry!.Key);
    }

    [Fact]
    public void Resolve_SameTurnOwnerRequiresExactRefAndNeverAcceptsAllocatedId()
    {
        var sameTurn = Owner(
            "mortal_world",
            ResourceOwnerKind.Item,
            "item_allocated",
            "charges",
            sameTurn: true,
            ownerRef: "item_ref_alpha");
        var authority = Build(Array.Empty<ResourceOwnerExport>(), new[] { sameTurn });

        var byRef = authority.Resolve(new ResourceOwnerRequest(
            "mortal_world",
            ResourceOwnerKind.Item,
            "charges",
            ResourceOwnerId: null,
            OwnerRef: "item_ref_alpha"));
        var byAllocatedId = authority.Resolve(new ResourceOwnerRequest(
            "mortal_world",
            ResourceOwnerKind.Item,
            "charges",
            ResourceOwnerId: "item_allocated",
            OwnerRef: null));

        Assert.True(byRef.Success);
        Assert.Equal("item_allocated", byRef.Entry!.Key.ResourceOwnerId);
        Assert.Contains(byAllocatedId.Issues, issue =>
            issue.Code == "resource_owner_same_turn_id_forbidden");
    }

    [Fact]
    public void Build_RejectsDuplicateConfusableOwnerAndCrossKindRefs()
    {
        var authority = Build(
            new[]
            {
                Owner("mortal_world", ResourceOwnerKind.Npc, "npc_alpha", "health"),
                Owner("mortal_world", ResourceOwnerKind.Npc, "NPC_ALPHA", "health")
            },
            new[]
            {
                Owner(
                    "mortal_world",
                    ResourceOwnerKind.Combatant,
                    "combatant_alpha",
                    "health",
                    sameTurn: true,
                    ownerRef: "owner_ref_alpha"),
                Owner(
                    "mortal_world",
                    ResourceOwnerKind.Item,
                    "item_alpha",
                    "charges",
                    sameTurn: true,
                    ownerRef: "OWNER_REF_ALPHA")
            });

        Assert.Contains(authority.Issues, issue =>
            issue.Code == "resource_owner_identity_confusable");
        Assert.Contains(authority.Issues, issue =>
            issue.Code == "resource_owner_ref_confusable");
    }

    [Theory]
    [InlineData("npc_retired", "npc_retired", "resource_owner_identity_duplicate")]
    [InlineData("npc_retired", "NPC_RETIRED", "resource_owner_identity_confusable")]
    [InlineData("npc_healer", "npc_heаler", "resource_owner_identity_confusable")]
    public void Build_RejectsDuplicateAndConfusableHistoricalOwnerIdentities(
        string firstOwnerId,
        string secondOwnerId,
        string expectedCode)
    {
        var authority = Build(
            Array.Empty<ResourceOwnerExport>(),
            historical: new[]
            {
                new ResourceOwnerKey(
                    "mortal_world",
                    ResourceOwnerKind.Npc,
                    firstOwnerId),
                new ResourceOwnerKey(
                    "mortal_world",
                    ResourceOwnerKind.Npc,
                    secondOwnerId)
            });

        Assert.Contains(authority.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void Resolve_RejectsCapabilityLifecycleRealmHistoricalNameAndIndexInference()
    {
        var suspended = Owner(
            "mortal_world",
            ResourceOwnerKind.Npc,
            "npc_suspended",
            "health") with
        {
            Lifecycle = ResourceOwnerLifecycle.Suspended
        };
        var authority = Build(
            new[]
            {
                Owner("mortal_world", ResourceOwnerKind.Npc, "npc_alpha", "health"),
                suspended,
                Owner("chaos_sea", ResourceOwnerKind.AfterlifeActor, "actor_alpha", "action_points")
            },
            historical: new[]
            {
                new ResourceOwnerKey("mortal_world", ResourceOwnerKind.Npc, "npc_retired")
            });

        Assert.Contains(ResolveId(authority, "mortal_world", ResourceOwnerKind.Npc, "npc_alpha", "energy").Issues,
            issue => issue.Code == "resource_owner_capability_missing");
        Assert.Contains(ResolveId(authority, "mortal_world", ResourceOwnerKind.Npc, "npc_suspended", "health").Issues,
            issue => issue.Code == "resource_owner_inactive");
        Assert.Contains(ResolveId(authority, "shining_abode", ResourceOwnerKind.AfterlifeActor, "actor_alpha", "action_points").Issues,
            issue => issue.Code == "resource_owner_realm_mismatch");
        Assert.Contains(ResolveId(authority, "mortal_world", ResourceOwnerKind.Npc, "NPC_RETIRED", "health").Issues,
            issue => issue.Code == "resource_owner_historical");
        Assert.Contains(ResolveId(authority, "mortal_world", ResourceOwnerKind.Npc, "Лекарь", "health").Issues,
            issue => issue.Code == "resource_owner_unresolved");
        Assert.Contains(ResolveId(authority, "mortal_world", ResourceOwnerKind.Npc, "0", "health").Issues,
            issue => issue.Code == "resource_owner_unresolved");
    }

    [Fact]
    public void SuspendedOwner_ResolvesOnlySealedRealmIndependentCapabilityAndAgreesWithState()
    {
        var owner = Owner(
            "shining_abode",
            ResourceOwnerKind.AfterlifeActor,
            "player_soul",
            "spiritual_action_points") with
        {
            Lifecycle = ResourceOwnerLifecycle.Suspended,
            ResourceCapabilities = Set("spiritual_action_points", "blessing_rerolls"),
            RealmIndependentResourceCapabilities = Set("blessing_rerolls")
        };
        var authority = Build(new[] { owner });

        var persistent = ResolveId(
            authority,
            "shining_abode",
            ResourceOwnerKind.AfterlifeActor,
            "player_soul",
            "blessing_rerolls");
        var realmBound = ResolveId(
            authority,
            "shining_abode",
            ResourceOwnerKind.AfterlifeActor,
            "player_soul",
            "spiritual_action_points");

        Assert.True(persistent.Success);
        Assert.Contains(realmBound.Issues, issue => issue.Code == "resource_owner_inactive");

        var state = new ResourceStateLedger(new[]
        {
            Entry("blessing_rerolls", ResourceLifecycleState.Active, "rerolls"),
            Entry("spiritual_action_points", ResourceLifecycleState.Suspended, "action_points")
        });
        var history = ResourceHistoryState.ParseCanonical(
            "{\"schemaVersion\":1,\"entries\":[]}",
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.True(history.IsValid);
        Assert.Empty(authority.ValidateCanonicalAgreement(
            state,
            Assert.IsType<ResourceHistoryState>(history.History)));

        var realmBoundOnly = Build(new[]
        {
            owner with { RealmIndependentResourceCapabilities = Set() }
        });
        Assert.NotEqual(authority.Fingerprint, realmBoundOnly.Fingerprint);
        Assert.True(Assert.IsAssignableFrom<ISet<string>>(
            authority.Entries[owner.Key].RealmIndependentResourceCapabilities).IsReadOnly);
    }

    [Fact]
    public void Build_RejectsRealmIndependentCapabilityThatOwnerDidNotExport()
    {
        var authority = Build(new[]
        {
            Owner(
                "shining_abode",
                ResourceOwnerKind.AfterlifeActor,
                "player_soul",
                "spiritual_action_points") with
            {
                RealmIndependentResourceCapabilities = Set("blessing_rerolls")
            }
        });

        Assert.Contains(authority.Issues, issue =>
            issue.Code == "resource_owner_realm_independent_capability_unbound");
    }

    [Fact]
    public void Build_RejectsInvalidPlayerRealmFingerprintCapabilityAndNpcBinding()
    {
        var invalidCapability = Owner(
            "mortal_world",
            ResourceOwnerKind.Item,
            "item_alpha",
            " bad_capability") with
        {
            AuthorityFingerprint = "not-a-fingerprint"
        };
        var mismatchedNpcBinding = Owner(
            "mortal_world",
            ResourceOwnerKind.Npc,
            "npc_alpha",
            "health") with
        {
            BoundNpcId = "npc_other"
        };
        var authority = Build(new[]
        {
            Owner("mortal_world", ResourceOwnerKind.Player, "player_other", "health"),
            Owner("mortal_world", ResourceOwnerKind.AfterlifeActor, "actor_alpha", "action_points"),
            invalidCapability,
            mismatchedNpcBinding
        });

        Assert.Contains(authority.Issues, issue => issue.Code == "resource_owner_player_identity_invalid");
        Assert.Contains(authority.Issues, issue => issue.Code == "resource_owner_realm_invalid");
        Assert.Contains(authority.Issues, issue => issue.Code == "resource_owner_capability_invalid");
        Assert.Contains(authority.Issues, issue => issue.Code == "resource_owner_fingerprint_invalid");
        Assert.Contains(authority.Issues, issue => issue.Code == "resource_owner_npc_binding_invalid");
    }

    [Fact]
    public void Fingerprint_IsOrderIndependentSemanticAndCollectionsAreDefensive()
    {
        var first = Owner("mortal_world", ResourceOwnerKind.Player, "player_current", "health");
        var second = Owner("mortal_world", ResourceOwnerKind.Item, "item_alpha", "charges");
        var ordered = Build(new[] { first, second });
        var reversed = Build(new[] { second, first });
        var changed = Build(new[]
        {
            first,
            second with
            {
                ResourceCapabilities = Set("charges", "durability")
            }
        });

        Assert.Equal(ordered.Fingerprint, reversed.Fingerprint);
        Assert.NotEqual(ordered.Fingerprint, changed.Fingerprint);
        Assert.True(Assert.IsAssignableFrom<IDictionary<ResourceOwnerKey, ResourceOwnerAuthorityEntry>>(
            ordered.Entries).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<ISet<string>>(
            ordered.Entries[second.Key].ResourceCapabilities).IsReadOnly);
    }

    [Fact]
    public void CanonicalAgreement_RejectsOwnerAndResourceLifecycleMismatch()
    {
        var suspended = Owner(
            "mortal_world",
            ResourceOwnerKind.Item,
            "item_alpha",
            "charges") with
        {
            Lifecycle = ResourceOwnerLifecycle.Suspended
        };
        var authority = Build(new[] { suspended });
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Item,
            "item_alpha",
            "charges");
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                coordinate,
                Current: 1m,
                Maximum: 1m,
                new ResourceCapacityBinding(
                    ResourceCapacityKind.InstanceFixed,
                    "capacity_item_alpha_charges",
                    FingerprintA),
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    CreatedAtTurn: 1,
                    CreatedEventRef: "turn_1:resource:1",
                    LastTransitionId: "transition_initialize_item_alpha",
                    LastEventRef: "turn_1:resource:1",
                    LastTransitionTurn: 1))
        });
        var historyResult = ResourceHistoryState.ParseCanonical(
            "{\"schemaVersion\":1,\"entries\":[]}",
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        Assert.True(historyResult.IsValid);

        var issues = authority.ValidateCanonicalAgreement(
            state,
            Assert.IsType<ResourceHistoryState>(historyResult.History));

        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_lifecycle_state_mismatch");
    }

    private static ResourceOwnerAuthorityResolution ResolveId(
        ResourceOwnerAuthority authority,
        string realm,
        ResourceOwnerKind kind,
        string ownerId,
        string resourceKey) =>
        authority.Resolve(new ResourceOwnerRequest(
            realm,
            kind,
            resourceKey,
            ResourceOwnerId: ownerId,
            OwnerRef: null));

    private static JsonObject NpcMirrorRoot()
    {
        var npc = new JsonObject
        {
            ["NPCId"] = "npc_field_medic",
            ["displayName"] = "Field medic"
        };
        return new JsonObject
        {
            ["NPCsInScene"] = new JsonArray(npc.DeepClone()),
            ["UpdateNPCs"] = new JsonArray(npc.DeepClone())
        };
    }

    private static MortalItemCarrierCatalog EmptyItemCatalog() =>
        MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            null,
            null,
            null,
            null,
            null,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal)));

    private static ResourceOwnerAuthority Build(
        IReadOnlyList<ResourceOwnerExport> preTurn,
        IReadOnlyList<ResourceOwnerExport>? sameTurn = null,
        IReadOnlyList<ResourceOwnerKey>? historical = null) =>
        ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            preTurn,
            sameTurn ?? Array.Empty<ResourceOwnerExport>(),
            historical ?? Array.Empty<ResourceOwnerKey>()));

    private static ResourceOwnerExport Owner(
        string realm,
        ResourceOwnerKind kind,
        string ownerId,
        string capability,
        bool sameTurn = false,
        string? ownerRef = null) =>
        new(
            new ResourceOwnerKey(realm, kind, ownerId),
            ResourceOwnerLifecycle.Active,
            sameTurn,
            ownerRef,
            BoundNpcId: kind == ResourceOwnerKind.Npc ? ownerId : null,
            Set(capability),
            FingerprintA);

    private static ResourceStateEntry Entry(
        string resourceKey,
        ResourceLifecycleState lifecycle,
        string identity) =>
        new(
            new ResourceCoordinate(
                "shining_abode",
                ResourceOwnerKind.AfterlifeActor,
                "player_soul",
                resourceKey),
            Current: 1m,
            Maximum: 1m,
            new ResourceCapacityBinding(
                ResourceCapacityKind.InstanceFixed,
                $"capacity_{identity}",
                FingerprintA),
            lifecycle,
            new ResourceChronology(
                CreatedAtTurn: 1,
                CreatedEventRef: $"turn_1:resource:{identity}",
                LastTransitionId: $"transition_initialize_{identity}",
                LastEventRef: $"turn_1:resource:{identity}",
                LastTransitionTurn: 1));

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
