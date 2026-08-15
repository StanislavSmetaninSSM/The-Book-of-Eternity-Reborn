using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceOwnerAuthorityTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

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

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
