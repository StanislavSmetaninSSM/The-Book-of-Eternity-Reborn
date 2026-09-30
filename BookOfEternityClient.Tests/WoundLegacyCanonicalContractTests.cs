using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundLegacyCanonicalContractTests
{
    private const string LegacyKind = "wound_legacy";
    private const string LegacyId = "wound_legacy_contract_001";
    private const string DefinitionKey = "legacy_contract_definition";
    private const string SourceRef = "legacy_ref_contract";

    [Theory]
    [InlineData("mortal_world", false, "active")]
    [InlineData("mortal_world", false, "suspended")]
    [InlineData("mortal_world", true, "active")]
    [InlineData("mortal_world", true, "suspended")]
    [InlineData("chaos_sea", false, "active")]
    [InlineData("chaos_sea", false, "suspended")]
    [InlineData("chaos_sea", true, "active")]
    [InlineData("chaos_sea", true, "suspended")]
    [InlineData("shining_abode", false, "active")]
    [InlineData("shining_abode", false, "suspended")]
    [InlineData("shining_abode", true, "active")]
    [InlineData("shining_abode", true, "suspended")]
    public void CanonicalContract_AcceptsIndependentLegacyCoordinates(string realm, bool sourceBound, string state)
    {
        var effect = Effect(realm, LegacyKind, sourceBound, state);
        var original = effect.ToJsonString();
        using var document = JsonDocument.Parse(original);
        Assert.Empty(EffectMaterializationContract.Validate(document.RootElement, "effects[0]", EffectMaterializationPhase.CanonicalActive));
        Assert.Equal(original, effect.ToJsonString());
        Assert.Equal(LegacyKind, document.RootElement.GetProperty("source").GetProperty("kind").GetString());
        Assert.Equal(LegacyId, document.RootElement.GetProperty("source").GetProperty("sourceId").GetString());
        Assert.Empty(document.RootElement.GetProperty("links").EnumerateArray());
        if (sourceBound)
        {
            Assert.Equal(LegacyKind, document.RootElement.GetProperty("lifetime").GetProperty("linkKind").GetString());
            Assert.Equal(LegacyId, document.RootElement.GetProperty("lifetime").GetProperty("targetId").GetString());
        }
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
    public void EmptyLinkDefinition_ResolvesOnlyThroughNonPublicCatalogBinding(string realm, bool sourceBound, bool sameTurn)
    {
        var definitions = new JsonArray(Definition(realm, LegacyKind, sourceBound));
        using var document = JsonDocument.Parse(definitions.ToJsonString());
        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(document.RootElement, "source.activeEffectDefinitions", realm));
        var source = new EffectSourceExport(realm, LegacyKind, LegacyId, definitions, Materializable: false, Active: true, SameTurn: sameTurn, SourceRef: sameTurn ? SourceRef : null);
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(sameTurn ? Array.Empty<EffectSourceExport>() : new[] { source }, sameTurn ? new[] { source } : Array.Empty<EffectSourceExport>(), new HashSet<string>(StringComparer.Ordinal)));
        Assert.Empty(authority.Issues);
        var binding = authority.ResolveCanonicalBinding(new EffectSourceKey(realm, LegacyKind, LegacyId, DefinitionKey), "player");
        Assert.True(binding.Success);
        Assert.NotNull(binding.Source);
        Assert.False(binding.Source.Materializable);
        Assert.True(JsonNode.DeepEquals(definitions[0], binding.Source.Definition));
        var selector = new JsonObject { ["kind"] = LegacyKind, [sameTurn ? "sourceRef" : "sourceId"] = sameTurn ? SourceRef : LegacyId, ["definitionKey"] = DefinitionKey };
        var apply = authority.Resolve(selector, realm, "player", new JsonObject());
        Assert.False(apply.Success);
        Assert.Contains(apply.Issues, static issue => issue.Code == "effect_source_not_materializable");
        var repair = authority.ResolveRepairCandidate(selector, realm, "player");
        Assert.False(repair.Success);
        Assert.Contains(repair.Issues, static issue => issue.Code == "effect_source_not_materializable");
        Assert.Empty(authority.SnapshotSameTurnWoundEntries());
        Assert.Empty(authority.SnapshotSameTurnWoundGroups());
        Assert.Empty(authority.SnapshotWoundGroupAuthorities());
    }

    public static IEnumerable<object[]> InvalidCanonicalKinds()
    {
        foreach (var field in new[] { "source", "lifetime", "links" })
        foreach (var kind in new[] { "Wound_Legacy", "wound_legacy ", "wоund_legacy", "wound_legacy_extra" })
            yield return new object[] { field, kind };
    }

    [Theory]
    [MemberData(nameof(InvalidCanonicalKinds))]
    public void CanonicalContract_RejectsEveryNonExactKind(string field, string kind)
    {
        var effect = Effect("mortal_world", "wound", true, "active");
        var expectedPath = field switch { "source" => "effects[0].source.kind", "lifetime" => "effects[0].lifetime.linkKind", "links" => "effects[0].links[0].kind", _ => throw new ArgumentOutOfRangeException(nameof(field)) };
        if (field == "links") effect["links"]![0]!["kind"] = kind;
        else effect[field]![field == "source" ? "kind" : "linkKind"] = kind;
        using var document = JsonDocument.Parse(effect.ToJsonString());
        var issue = Assert.Single(EffectMaterializationContract.Validate(document.RootElement, "effects[0]", EffectMaterializationPhase.CanonicalActive));
        Assert.Equal("effect_materialization_invalid_field", issue.Code);
        Assert.Equal(expectedPath, issue.FilePath);
    }

    [Theory]
    [InlineData("Wound_Legacy")]
    [InlineData("wound_legacy ")]
    [InlineData("wоund_legacy")]
    [InlineData("wound_legacy_extra")]
    public void DefinitionContract_RejectsEveryNonExactLinkKind(string kind)
    {
        var definition = Definition("mortal_world", kind, true);
        using var document = JsonDocument.Parse(new JsonArray(definition).ToJsonString());
        var issue = Assert.Single(EffectSourceDefinitionContract.ValidateArray(document.RootElement, "source.activeEffectDefinitions", "mortal_world"));
        Assert.Equal("effect_source_definition_invalid_field", issue.Code);
        Assert.Equal("source.activeEffectDefinitions[0].links[0].kind", issue.FilePath);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("condition")]
    [InlineData("context")]
    [InlineData("cleanup_companion")]
    public void DefinitionContract_DoesNotOpenAuthoredLegacyLinks(string role)
    {
        var definition = Definition("mortal_world", LegacyKind, true);
        definition["links"] = Links(LegacyKind);
        definition["links"]![0]!["role"] = role;
        using var document = JsonDocument.Parse(new JsonArray(definition).ToJsonString());
        var issue = Assert.Single(EffectSourceDefinitionContract.ValidateArray(document.RootElement, "source.activeEffectDefinitions", "mortal_world"));
        Assert.Equal("effect_source_definition_invalid_field", issue.Code);
        Assert.Equal("source.activeEffectDefinitions[0].links[0].kind", issue.FilePath);
    }

    [Theory]
    [InlineData("mortal_world")]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void ExistingWoundKind_RemainsStructurallyValid(string realm)
    {
        using var effect = JsonDocument.Parse(Effect(realm, "wound", true, "active").ToJsonString());
        using var definitions = JsonDocument.Parse(new JsonArray(Definition(realm, "wound", true)).ToJsonString());
        Assert.Empty(EffectMaterializationContract.Validate(effect.RootElement, "effects[0]", EffectMaterializationPhase.CanonicalActive));
        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(definitions.RootElement, "source.activeEffectDefinitions", realm));
    }

    private static JsonObject Effect(string realm, string kind, bool sourceBound, string state)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "action_control");
        effect["realm"] = realm; effect["state"] = state;
        effect["source"] = new JsonObject { ["kind"] = kind, ["sourceId"] = LegacyId, ["definitionKey"] = DefinitionKey };
        effect["links"] = kind == LegacyKind ? new JsonArray() : Links(kind);
        effect["lifetime"] = sourceBound ? new JsonObject { ["mode"] = "source_bound", ["linkKind"] = kind, ["targetId"] = LegacyId, ["activePredicate"] = "active", ["onSourceLoss"] = "expire" } : new JsonObject { ["mode"] = "permanent" };
        return effect;
    }

    private static JsonObject Definition(string realm, string kind, bool sourceBound)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("action_control");
        definition["definitionKey"] = DefinitionKey; definition["allowedRealms"] = new JsonArray(realm); definition["allowedTargetKinds"] = new JsonArray("player"); definition["links"] = kind == LegacyKind ? new JsonArray() : Links(kind);
        definition["lifetime"] = sourceBound ? new JsonObject { ["mode"] = "source_bound", ["activePredicate"] = "active", ["onSourceLoss"] = "expire" } : new JsonObject { ["mode"] = "permanent" };
        return definition;
    }

    private static JsonArray Links(string kind) => new(new JsonObject { ["kind"] = kind, ["targetId"] = LegacyId, ["role"] = "source" });
}
