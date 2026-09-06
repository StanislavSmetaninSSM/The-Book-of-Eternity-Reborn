using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundLegacySourceAuthorityTests
{
    private const string LegacyId = "wound_legacy_healed_catalog";
    private const string DefinitionKey = "legacy_residual_tremor";
    private const string LegacyRef = "legacyref_catalog";

    private static EffectSourceExport Source(
        string realm = "mortal_world", bool sameTurn = false)
    {
        var definition = WoundContractTestData.CreateOwnedEffectDefinition(
            "already_healed_wound", realm, DefinitionKey, "action_control");
        definition["links"] = new JsonArray();
        return new EffectSourceExport(
            realm, "wound_legacy", LegacyId, new JsonArray(definition),
            Materializable: false, Active: true, SameTurn: sameTurn,
            SourceRef: sameTurn ? LegacyRef : null);
    }

    private static EffectSourceKey Key(string realm) =>
        new(realm, "wound_legacy", LegacyId, DefinitionKey);

    private static JsonObject Selector(EffectSourceExport source) => new()
    {
        ["kind"] = source.Kind,
        [source.SameTurn ? "sourceRef" : "sourceId"] =
            source.SameTurn ? source.SourceRef : source.SourceId,
        ["definitionKey"] = DefinitionKey
    };

    private static EffectSourceAuthority Build(
        bool sameTurn, params EffectSourceExport[] sources) =>
        EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            sameTurn ? Array.Empty<EffectSourceExport>() : sources,
            sameTurn ? sources : Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));

    private static EffectAcceptedTurnInput Compose(
        string realm, IReadOnlyList<EffectSourceExport> exports)
    {
        var empty = new EffectCarrierCatalogInput(
            null, null, null, null, null, null);
        return EffectAcceptedTurnInputComposer.Compose(
            "session_legacy_catalog", "snapshot_legacy_catalog", 1,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(),
            empty, empty, null, new Dictionary<string, JsonNode?>(),
            acceptedPlanSourceExports: exports, realm: realm);
    }

    [Theory]
    [InlineData("mortal_world", false)]
    [InlineData("mortal_world", true)]
    [InlineData("chaos_sea", false)]
    [InlineData("chaos_sea", true)]
    [InlineData("shining_abode", false)]
    [InlineData("shining_abode", true)]
    public void ExplicitNonPublicCatalogSource_ResolvesWithoutActiveWound(
        string realm, bool sameTurn)
    {
        var source = Source(realm, sameTurn);
        var authority = Build(sameTurn, source);

        Assert.Empty(authority.Issues);
        var canonical = authority.ResolveCanonicalBinding(Key(realm), "player");
        Assert.True(canonical.Success);
        Assert.NotNull(canonical.Source);
        Assert.Equal(Key(realm), canonical.Source.Key);
        Assert.False(canonical.Source.Materializable);
        Assert.Equal(sameTurn, canonical.Source.SameTurn);
        Assert.True(JsonNode.DeepEquals(
            source.Definitions[0], canonical.Source.Definition));

        var selector = Selector(source);
        var publicAttempt = authority.Resolve(
            selector, realm, "player", new JsonObject());
        Assert.False(publicAttempt.Success);
        Assert.Contains(publicAttempt.Issues, static issue =>
            issue.Code == "effect_source_not_materializable");
        var repairAttempt = authority.ResolveRepairCandidate(
            selector, realm, "player");
        Assert.False(repairAttempt.Success);
        Assert.Contains(repairAttempt.Issues, static issue =>
            issue.Code == "effect_source_not_materializable");

        Assert.Empty(authority.SnapshotSameTurnWoundEntries());
        Assert.Empty(authority.SnapshotSameTurnWoundGroups());
        Assert.Empty(authority.SnapshotWoundGroupAuthorities());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ForgedPublicExport_IsRejectedBeforeItCanPoisonValidSibling(
        bool sameTurn, bool forgedFirst)
    {
        var valid = Source(sameTurn: sameTurn);
        var forged = valid with { Materializable = true };
        var sources = forgedFirst
            ? new[] { forged, valid }
            : new[] { valid, forged };

        var authority = Build(sameTurn, sources);

        var issue = Assert.Single(authority.Issues);
        Assert.Equal(
            "effect_source_wound_legacy_public_materialization_forbidden",
            issue.Code);
        var canonical = authority.ResolveCanonicalBinding(Key("mortal_world"), "player");
        Assert.True(canonical.Success);
        Assert.NotNull(canonical.Source);
        Assert.False(canonical.Source.Materializable);
        var attempt = authority.Resolve(
            Selector(valid), "mortal_world", "player", new JsonObject());
        Assert.False(attempt.Success);
        Assert.Contains(attempt.Issues, static value =>
            value.Code == "effect_source_not_materializable");
    }

    [Theory]
    [InlineData("WOUND_LEGACY")]
    [InlineData("wound_legacy ")]
    [InlineData("wound_legacY")]
    public void LookalikeKind_IsNotRegistered(string kind)
    {
        var source = Source() with { Kind = kind };
        var authority = Build(false, source);

        Assert.Contains(authority.Issues, static issue =>
            issue.Code == "effect_source_authority_invalid_export");
        Assert.False(authority.ResolveCanonicalBinding(
            new EffectSourceKey("mortal_world", kind, LegacyId, DefinitionKey),
            "player").Success);
    }

    [Theory]
    [InlineData("mortal_world", false, false)]
    [InlineData("mortal_world", false, true)]
    [InlineData("mortal_world", true, false)]
    [InlineData("mortal_world", true, true)]
    [InlineData("chaos_sea", false, false)]
    [InlineData("chaos_sea", false, true)]
    [InlineData("chaos_sea", true, false)]
    [InlineData("chaos_sea", true, true)]
    [InlineData("shining_abode", false, false)]
    [InlineData("shining_abode", false, true)]
    [InlineData("shining_abode", true, false)]
    [InlineData("shining_abode", true, true)]
    public void Composer_RejectsAndExcludesGenericLegacyExports(
        string realm, bool sameTurn, bool materializable)
    {
        var source = Source(realm, sameTurn) with
        {
            Materializable = materializable
        };

        var input = Compose(realm, new[] { source });

        Assert.Contains(input.SourceAuthority.Issues, static issue =>
            issue.Code == "effect_source_wound_legacy_external_export_forbidden");
        Assert.False(input.SourceAuthority.ResolveCanonicalBinding(
            Key(realm), "player").Success);
        Assert.DoesNotContain(input.SourceAuthority.Issues, static issue =>
            issue.Code is "effect_source_authority_invalid_export"
                or "effect_source_wound_legacy_public_materialization_forbidden");
        Assert.Empty(input.SourceAuthority.SnapshotSameTurnWoundEntries());
        Assert.Empty(input.SourceAuthority.SnapshotSameTurnWoundGroups());
    }

    [Theory]
    [InlineData("mortal_world")]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void Composer_WithoutLegacyInjection_PreservesEmptyControl(string realm)
    {
        var input = Compose(realm, Array.Empty<EffectSourceExport>());

        Assert.Empty(input.SourceAuthority.Issues);
        Assert.False(input.SourceAuthority.ResolveCanonicalBinding(
            Key(realm), "player").Success);
    }
}
