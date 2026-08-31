using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectSourceAuthorityTests
{
    [Theory]
    [InlineData("skill")]
    [InlineData("spiritual_art")]
    [InlineData("item")]
    [InlineData("wound")]
    [InlineData("quest")]
    [InlineData("location")]
    [InlineData("hazard")]
    [InlineData("faction")]
    [InlineData("world_event")]
    [InlineData("fate_card")]
    [InlineData("combat_action")]
    public void Resolve_EachSupportedPreTurnSourceKindUsesExactDefinition(string kind)
    {
        var authority = Build(Source(kind, kind + "_test"));

        var result = authority.Resolve(
            new EffectSourceKey("mortal_world", kind, kind + "_test", EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject { ["amount"] = 3 });

        Assert.Empty(authority.Issues);
        Assert.True(result.Success);
        Assert.Equal(kind, result.Source!.Key.Kind);
        Assert.Equal(EffectMaterializationTestFixture.DefinitionKey, result.Source.Key.DefinitionKey);
    }

    [Fact]
    public void Resolve_AcceptsOnlyExplicitValidatedSameTurnExport()
    {
        var sameTurn = Source("wound", "wound_same_turn");
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            Array.Empty<EffectSourceExport>(),
            new[] { sameTurn },
            EmptySet()));

        var result = authority.Resolve(
            new EffectSourceKey("mortal_world", "wound", "wound_same_turn", EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject { ["amount"] = 3 });

        Assert.True(result.Success);
        Assert.True(result.Source!.SameTurn);
    }

    [Fact]
    public void Resolve_SameTurnSourceRefReturnsPermanentEffectiveIdentity()
    {
        var sameTurn = Source("location", "loc_permanent_same_turn") with
        {
            SameTurn = true,
            SourceRef = "locref_same_turn"
        };
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            Array.Empty<EffectSourceExport>(),
            new[] { sameTurn },
            EmptySet()));
        var selector = new JsonObject
        {
            ["kind"] = "location",
            ["sourceRef"] = "locref_same_turn",
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };

        var result = authority.Resolve(
            selector,
            "mortal_world",
            "player",
            new JsonObject { ["amount"] = 3 });

        Assert.True(result.Success);
        Assert.Equal("loc_permanent_same_turn", result.Source!.Key.SourceId);
        Assert.True(result.Source.SameTurn);
    }

    [Theory]
    [InlineData("LOCREF_SAME_TURN", "effect_source_selector_confusable")]
    [InlineData("locref_unknown", "effect_source_selector_unresolved")]
    public void Resolve_SameTurnSourceRefRejectsAliasAndUnknown(
        string sourceRef,
        string expectedCode)
    {
        var sameTurn = Source("location", "loc_permanent_same_turn") with
        {
            SameTurn = true,
            SourceRef = "locref_same_turn"
        };
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            Array.Empty<EffectSourceExport>(),
            new[] { sameTurn },
            EmptySet()));

        var result = authority.Resolve(
            new JsonObject
            {
                ["kind"] = "location",
                ["sourceRef"] = sourceRef,
                ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
            },
            "mortal_world",
            "player",
            new JsonObject { ["amount"] = 3 });

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void Resolve_ClientAssignedSameTurnSourceIdIsNotGmSelectable()
    {
        var sameTurn = Source("item", "itm_client_assigned") with
        {
            SameTurn = true,
            SourceRef = "new_item_same_turn"
        };
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            Array.Empty<EffectSourceExport>(),
            new[] { sameTurn },
            EmptySet()));

        var result = authority.Resolve(
            new EffectSourceKey(
                "mortal_world",
                "item",
                "itm_client_assigned",
                EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject { ["amount"] = 3 });

        Assert.Contains(result.Issues, issue =>
            issue.Code == "effect_source_same_turn_id_forbidden");
    }

    [Theory]
    [InlineData(false, true, "effect_source_not_materializable")]
    [InlineData(true, false, "effect_source_inactive")]
    public void Resolve_PassiveInstantaneousOrInactiveSourceDoesNotPromote(
        bool materializable,
        bool active,
        string expectedCode)
    {
        var authority = Build(Source("skill", "skill_passive", materializable, active));

        var result = authority.Resolve(
            new EffectSourceKey("mortal_world", "skill", "skill_passive", EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject { ["amount"] = 3 });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void Resolve_SourceBoundItemRequiresItsExactSatisfiedPredicate()
    {
        const string itemId = "itm_predicate_authority";
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "equipped",
            ["onSourceLoss"] = "expire"
        };
        var unequipped = Build(new EffectSourceExport(
            "mortal_world",
            "item",
            itemId,
            new JsonArray(definition.DeepClone()),
            Materializable: true,
            Active: true,
            SameTurn: false,
            SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal) { "carried" }));
        var equipped = Build(new EffectSourceExport(
            "mortal_world",
            "item",
            itemId,
            new JsonArray(definition.DeepClone()),
            Materializable: true,
            Active: true,
            SameTurn: false,
            SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal) { "carried", "equipped" }));
        var key = new EffectSourceKey(
            "mortal_world",
            "item",
            itemId,
            EffectMaterializationTestFixture.DefinitionKey);

        var rejected = unequipped.Resolve(
            key,
            "player",
            new JsonObject { ["amount"] = 3 });
        var accepted = equipped.Resolve(
            key,
            "player",
            new JsonObject { ["amount"] = 3 });

        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "effect_source_predicate_unsatisfied");
        Assert.True(accepted.Success);
    }

    [Fact]
    public void Resolve_SourceBoundPredicateMustMatchSourceKind()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "equipped",
            ["onSourceLoss"] = "expire"
        };
        var authority = Build(new EffectSourceExport(
            "mortal_world",
            "wound",
            "wound_predicate_authority",
            new JsonArray(definition),
            Materializable: true,
            Active: true,
            SameTurn: false,
            SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal) { "equipped" }));

        var result = authority.Resolve(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                "wound_predicate_authority",
                EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject { ["amount"] = 3 });

        Assert.Contains(authority.Issues, issue =>
            issue.Code == "effect_source_predicate_incompatible");
        Assert.False(result.Success);
    }

    [Fact]
    public void Resolve_ParameterMustExistAndRemainInsideSourceBound()
    {
        var authority = Build(Source("wound", "wound_test"));
        var key = new EffectSourceKey(
            "mortal_world", "wound", "wound_test", EffectMaterializationTestFixture.DefinitionKey);

        Assert.False(authority.Resolve(key, "player", new JsonObject { ["amount"] = 99 }).Success);
        Assert.False(authority.Resolve(key, "player", new JsonObject { ["future"] = 3 }).Success);
        Assert.True(authority.Resolve(key, "player", new JsonObject { ["amount"] = 3 }).Success);
    }

    [Fact]
    public void Resolve_TargetKindMustBeAuthorizedByDefinition()
    {
        var authority = Build(Source("wound", "wound_test"));

        var result = authority.Resolve(
            new EffectSourceKey("mortal_world", "wound", "wound_test", EffectMaterializationTestFixture.DefinitionKey),
            "guardian",
            new JsonObject { ["amount"] = 3 });

        Assert.Contains(result.Issues, issue => issue.Code == "effect_source_target_kind_forbidden");
    }

    [Fact]
    public void Resolve_ReturnsDetachedDefinitionThatCannotMutateCatalogAuthority()
    {
        var authority = Build(Source("wound", "wound_test"));
        var key = new EffectSourceKey(
            "mortal_world", "wound", "wound_test", EffectMaterializationTestFixture.DefinitionKey);
        var first = authority.Resolve(key, "player", new JsonObject { ["amount"] = 3 });
        first.Source!.Definition["allowedTargetKinds"] = new JsonArray();

        var second = authority.Resolve(key, "player", new JsonObject { ["amount"] = 3 });

        Assert.True(second.Success);
    }

    [Theory]
    [InlineData("WOUND_TEST", "effect_source_selector_confusable")]
    [InlineData("wound_teѕt", "effect_source_selector_confusable")]
    [InlineData("wound_retired", "effect_source_selector_historical")]
    [InlineData("wound_unknown", "effect_source_selector_unresolved")]
    public void Resolve_AliasHistoricalAndUnknownSourceSelectorsFailClosed(
        string sourceId,
        string expectedCode)
    {
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            new[] { Source("wound", "wound_test") },
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal) { "wound_retired" }));

        var result = authority.Resolve(
            new EffectSourceKey("mortal_world", "wound", sourceId, EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject { ["amount"] = 3 });

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void Resolve_CrossRealmSelectorFailsClosed()
    {
        var authority = Build(Source("spiritual_art", "art_test"));

        var result = authority.Resolve(
            new EffectSourceKey("chaos_sea", "spiritual_art", "art_test", EffectMaterializationTestFixture.DefinitionKey),
            "afterlife_actor",
            new JsonObject { ["amount"] = 3 });

        Assert.Contains(result.Issues, issue => issue.Code == "effect_source_realm_mismatch");
    }

    [Fact]
    public void Build_DuplicateAndConfusableDefinitionsOrSourcesAreRejected()
    {
        var firstDefinition = EffectMaterializationTestFixture.CreateDefinition();
        var secondDefinition = EffectMaterializationTestFixture.CreateDefinition();
        secondDefinition["definitionKey"] = "BLEEDING_CONSEQUENCE";
        var duplicateDefinitions = new EffectSourceExport(
            "mortal_world",
            "wound",
            "wound_test",
            new JsonArray(firstDefinition, secondDefinition),
            Materializable: true,
            Active: true,
            SameTurn: false);
        var aliasSource = Source("wound", "WOUND_TEST");

        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            new[] { duplicateDefinitions, aliasSource },
            Array.Empty<EffectSourceExport>(),
            EmptySet()));

        Assert.Contains(authority.Issues, issue =>
            issue.Code is "effect_source_definition_confusable_key" or "effect_source_authority_confusable_source");
    }

    [Fact]
    public void Build_RejectsUnresolvedDefinitionLinkTarget()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["links"] = new JsonArray(new JsonObject
        {
            ["kind"] = "wound",
            ["targetId"] = "wound_missing_link_target",
            ["role"] = "context"
        });
        var authority = Build(new EffectSourceExport(
            "mortal_world",
            "wound",
            "wound_current_source",
            new JsonArray(definition),
            Materializable: true,
            Active: true,
            SameTurn: false));

        Assert.Contains(authority.Issues, issue =>
            issue.Code == "effect_source_link_target_unresolved");
    }

    [Fact]
    public void Build_AcceptsExactDefinitionLinkToComposedOwnerWithoutDefinitions()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["links"] = new JsonArray(new JsonObject
        {
            ["kind"] = "quest",
            ["targetId"] = "quest_exact_context",
            ["role"] = "context"
        });
        var source = new EffectSourceExport(
            "mortal_world",
            "wound",
            "wound_current_source",
            new JsonArray(definition),
            Materializable: true,
            Active: true,
            SameTurn: false);
        var linkedOwner = new EffectSourceExport(
            "mortal_world",
            "quest",
            "quest_exact_context",
            new JsonArray(),
            Materializable: false,
            Active: true,
            SameTurn: false);

        var authority = Build(source, linkedOwner);

        Assert.DoesNotContain(authority.Issues, issue =>
            issue.Code?.StartsWith("effect_source_link_target_", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void CanonicalPublicationSubset_AllowsAdditionalUnusedCanonicalSources()
    {
        var staged = Build(Source("skill", "skill_used_by_sealed_plan"));
        var canonical = Build(
            Source("skill", "skill_used_by_sealed_plan"),
            Source("item", "item_unrelated_canonical_source"));

        Assert.True(staged.IsCanonicalPublicationSubsetOf(canonical));
        Assert.False(canonical.IsCanonicalPublicationSubsetOf(staged));
    }

    [Fact]
    public void CanonicalPublicationSubset_RejectsChangedCanonicalDefinition()
    {
        var staged = Build(Source("skill", "skill_used_by_sealed_plan"));
        var changed = Source("skill", "skill_used_by_sealed_plan");
        changed.Definitions[0]!["display"]!["name"] = "Changed after sealing";
        var canonical = Build(
            changed,
            Source("item", "item_unrelated_canonical_source"));

        Assert.False(staged.IsCanonicalPublicationSubsetOf(canonical));
    }

    private static EffectSourceAuthority Build(params EffectSourceExport[] exports) =>
        EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            exports,
            Array.Empty<EffectSourceExport>(),
            EmptySet()));

    private static EffectSourceExport Source(
        string kind,
        string sourceId,
        bool materializable = true,
        bool active = true) =>
        new(
            "mortal_world",
            kind,
            sourceId,
            new JsonArray(EffectMaterializationTestFixture.CreateDefinition()),
            materializable,
            active,
            SameTurn: false);

    private static IReadOnlySet<string> EmptySet() => new HashSet<string>(StringComparer.Ordinal);
}
