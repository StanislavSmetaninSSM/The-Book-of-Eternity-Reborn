using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundDetachedConsequenceShapeTests
{
    [Fact]
    public void Shape_DefersRealSeverityButPreservesMechanicalCoordinate()
    {
        var effects = Effects(Scalar("-4"));
        var shape = Shape(effects);
        Assert.True(shape.IsValid, Describe(shape.Issues));
        Assert.Equal("characteristic_modifier:characteristic:strength", Assert.Single(shape.Slots).Coordinate);
        Issue(Full(effects).Issues, "wound_consequence_magnitude_exceeded", "draft.definition.components[0].payload.value");
    }

    [Theory]
    [InlineData("0", "null", "value", "effect_materialization_invalid_component")]
    [InlineData("1e100", "null", "value", "effect_materialization_invalid_component")]
    [InlineData("1e-100", "null", "value", "wound_consequence_magnitude_exceeded")]
    [InlineData("-4", "{\"minimum\":0,\"maximum\":1}", "cap", "wound_consequence_magnitude_exceeded")]
    [InlineData("-4", "{\"minimum\":1e100,\"maximum\":1e100}", "cap.minimum", "wound_consequence_magnitude_exceeded")]
    public void Shape_RejectsInexactOrEffectiveZeroScalar(string value, string cap, string field, string code)
    {
        Issue(Shape(Effects(Scalar(value, cap))).Issues,
            code, "draft.definition.components[0].payload." + field);
    }

    [Fact]
    public void Shape_SafeCapUsesExactEffectiveValue()
    {
        var effects = Effects(Scalar("-40", "{\"minimum\":-1,\"maximum\":1}"));
        Assert.True(Shape(effects).IsValid);
        Assert.True(Full(effects).IsValid);
        var amplified = Effects(Scalar("-1", "{\"minimum\":1.0000000000000000000000000001,\"maximum\":2}"));
        Assert.True(Shape(amplified).IsValid);
        Issue(Full(amplified).Issues, "wound_consequence_magnitude_exceeded", "draft.definition.components[0].payload.cap");
    }

    [Fact]
    public void Shape_RejectsDuplicateMechanicalCoordinate()
    {
        var result = Shape(Effects(Scalar("-1"), Scalar("-2", id: "second")));
        Issue(result.Issues, "wound_consequence_duplicate_coordinate", "draft.definition.components[1].payload.characteristic");
        Assert.Single(result.Slots);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("1e100")]
    public void Shape_RejectsInvalidActionCostSyntax(string modifier)
    {
        var result = Shape(Effects(Action("movement", "cost_modifier", modifier)));
        Assert.Contains(result.Issues, issue => issue.FilePath == "draft.definition.components[0].payload.modifier" &&
            issue.Code is "wound_consequence_magnitude_exceeded" or "effect_materialization_invalid_component");
    }

    [Fact]
    public void Shape_DefersActionCostMagnitudeAndRequiresNullOutsideCostMode()
    {
        var cost = Effects(Action("movement", "cost_modifier", "-40"));
        Assert.True(Shape(cost).IsValid);
        Issue(Full(cost).Issues, "wound_consequence_magnitude_exceeded", "draft.definition.components[0].payload.modifier");
        Assert.True(Shape(Effects(Action("movement", "restrict"))).IsValid);
        Issue(Shape(Effects(Action("movement", "restrict", "1"))).Issues,
            "wound_consequence_magnitude_exceeded", "draft.definition.components[0].payload.modifier");
    }

    [Theory]
    [InlineData("communication")]
    [InlineData("exit")]
    [InlineData("help")]
    [InlineData("inspection")]
    [InlineData("treatment")]
    [InlineData("all")]
    [InlineData("escape")]
    [InlineData("defend")]
    [InlineData("use_item")]
    [InlineData("interact")]
    public void Shape_ForbidCannotTargetProtectedOrDisallowedActions(string action)
    {
        var result = Shape(Effects(Action(action, "forbid")));
        Assert.Contains(result.Issues, issue => issue.FilePath == "draft.definition.components[0].payload.action" &&
            issue.Code is "wound_consequence_action_forbid_invalid" or "effect_materialization_invalid_component");
    }

    [Theory]
    [InlineData("attack")]
    [InlineData("cast")]
    [InlineData("movement")]
    public void Shape_AllowedForbidStillRequiresRealHeavyRank(string action)
    {
        var effects = Effects(Action(action, "forbid"));
        Assert.Single(Shape(effects).Slots);
        Assert.True(Shape(effects).IsValid);
        Issue(Full(effects).Issues, "wound_consequence_action_forbid_invalid", "draft.definition.components[0].payload.operation");
    }

    [Fact]
    public void Shape_ExpandsLocalApplyDefinitionWithoutGrantingRankAuthority()
    {
        var effects = ReactionEffects(new[] { Expansion(Scalar("-4")) });
        var result = Shape(effects);
        Assert.True(result.IsValid, Describe(result.Issues));
        Assert.Equal(2, result.Slots.Length);
        Issue(Full(effects).Issues, "wound_consequence_reaction_expansion_invalid", "draft.definition.components[0].payload.definitionKey");
    }

    [Theory]
    [InlineData("missing", "draft.definition.components[0].payload.definitionKey")]
    [InlineData("duplicate", "draft.definition.components[0].payload.definitionKey")]
    [InlineData("unused", "draft.child")]
    [InlineData("maximum", "draft.definition.components[0].payload.maxExpansion")]
    [InlineData("nested", "draft.child.components[0].profile")]
    public void Shape_RejectsInvalidLocalExpansion(string scenario, string path)
    {
        var expansions = scenario switch
        {
            "missing" => Array.Empty<WoundDetachedMortalReactionExpansionRef>(),
            "duplicate" => new[] { Expansion(Scalar("-1")), Expansion(Scalar("-1")) },
            "unused" => new[] { new WoundDetachedMortalReactionExpansionRef("unbound", "draft.child", new[] { Scalar("-1") }) },
            "nested" => new[] { Expansion(Reaction()) },
            _ => new[] { Expansion(Scalar("-1")) }
        };
        Issue(Shape(ReactionEffects(expansions, scenario == "maximum" ? 1 : 2)).Issues,
            "wound_consequence_reaction_expansion_invalid", path);
    }

    [Fact]
    public void Shape_BindsActualEdgeParametersBeforeChildValidation()
    {
        var child = Scalar("0");
        var valid = new WoundDetachedMortalReactionExpansionRef("reaction", "draft.child", new[] { child }, Raw("{\"value\":-40}"));
        Assert.True(Shape(ReactionEffects(new[] { valid })).IsValid);
        var invalid = new WoundDetachedMortalReactionExpansionRef("reaction", "draft.child", new[] { Scalar("-1") }, Raw("{\"value\":0}"));
        Issue(Shape(ReactionEffects(new[] { invalid })).Issues,
            "effect_materialization_invalid_component", "draft.child.components[0].payload.value");
    }

    [Fact]
    public void Shape_PeriodicSlotsRetainDeferredAuthority()
    {
        var result = Shape(Effects(Component("bleeding", "periodic_damage",
            "{\"resource\":\"health\",\"amount\":100,\"damageType\":\"bleeding\",\"floorPolicy\":\"registered_resource_floor\"}")));
        Assert.True(result.IsValid, Describe(result.Issues));
        Assert.Single(result.Slots);
        Assert.True(result.HasDeferredPeriodicAuthority);
    }

    [Theory]
    [InlineData(false, 64)]
    [InlineData(true, 64)]
    [InlineData(false, 65)]
    [InlineData(true, 65)]
    public void DetachedExpansion_PreservesFlatteningBoundBeforeSlotBudgetValidation(bool shapeOnly, int childCount)
    {
        // Resource names are local selectors only; this API defers resource authority
        // and the adapter separately owns the aggregate wound slot budget.
        var children = Enumerable.Range(0, childCount).Select(index => Component(
            "periodic_" + index, "periodic_damage",
            $$"""{"resource":"resource_{{index}}","amount":1,"damageType":"bleeding","floorPolicy":"registered_resource_floor"} """)).ToArray();
        var effects = ReactionEffects(new[] { new WoundDetachedMortalReactionExpansionRef("reaction", "draft.child", children) });
        var result = shapeOnly ? Shape(effects) : WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(3, "draft", effects));

        if (childCount == 64)
        {
            Assert.True(result.IsValid, Describe(result.Issues));
            Assert.Equal(65, result.Slots.Length);
            Assert.True(result.HasDeferredPeriodicAuthority);
        }
        else
        {
            Issue(result.Issues, "wound_consequence_limit_exceeded", "draft.child.components");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdapterShape_SeparatesRealBudgetFromReciprocalSlots(bool global)
    {
        var (definitions, roots) = Graph(Scalar("-1"), Action("movement", "restrict"));
        Assert.True(Adapter(definitions, roots, global).IsValid);
        Issue(WoundPersistedConsequenceEnvelopeAdapter.ValidatePrevalidatedDetached(
            1, "draft", definitions, roots, global).Issues,
            "wound_materialization_consequence_slot_invalid", "draft");
    }

    [Theory]
    [InlineData(false, "missing", "draft.root.slots")]
    [InlineData(false, "extra", "draft.root.slots[1].profileKey")]
    [InlineData(false, "wrong", "draft.root.slots[0].profileKey")]
    [InlineData(true, "missing", "draft.entries")]
    [InlineData(true, "extra", "draft.entries")]
    [InlineData(true, "wrong", "draft.entries[0]")]
    public void AdapterShape_AlwaysRequiresSlotReciprocity(bool global, string mutation, string path)
    {
        var (definitions, roots) = Graph(Scalar("-1"));
        roots[0] = roots[0] with { ExpectedSlots = mutation switch
        {
            "missing" => Array.Empty<WoundEffectSlotAgreement>(),
            "extra" => new[] { new WoundEffectSlotAgreement(1, "characteristic_modifier", "penalty"), new WoundEffectSlotAgreement(2, "action_control", "restriction") },
            _ => new[] { new WoundEffectSlotAgreement(1, "action_control", "restriction") }
        } };
        Issue(Adapter(definitions, roots, global).Issues, "wound_materialization_consequence_slot_invalid", path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdapterShape_RejectsPersistedCountMismatch(bool global)
    {
        var (definitions, roots) = Graph(Scalar("-1"));
        Issue(Adapter(definitions, roots, global, 2).Issues, "wound_materialization_consequence_slot_invalid", "draft.slotsUsed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdapterShape_EnforcesAbsoluteStructuralMaximum(bool global)
    {
        var (definitions, roots) = Graph(Scalar("-1"), Action("attack", "restrict"), Action("cast", "restrict"),
            Action("movement", "restrict"), Component("resistance", "resistance_modifier",
                "{\"resistance\":\"fire\",\"operation\":\"flat\",\"value\":-1,\"cap\":null}"));
        Issue(Adapter(definitions, roots, global).Issues, "wound_materialization_consequence_slot_invalid", "draft");
    }

    [Fact]
    public void AdapterShape_DirectRootTargetIsNotAlsoAnUnboundChild()
    {
        var definitions = new[] { Definition("root", Reaction()), Definition("child", Scalar("-1")) };
        var roots = new[]
        {
            new WoundPersistedConsequenceRoot("root_effect", "root", "draft.root", new[] { new WoundEffectSlotAgreement(1, "event_reaction", "reaction") }),
            new WoundPersistedConsequenceRoot("child_effect", "child", "draft.child_root", new[] { new WoundEffectSlotAgreement(2, "characteristic_modifier", "penalty") })
        };
        var result = Adapter(definitions, roots, false);
        Assert.True(result.IsValid, Describe(result.Issues));
        Assert.Equal(2, result.DerivedSlots.Length);
        Assert.Single(result.DerivedSlots, slot => slot.EffectRef == "child_effect");
    }

    private static WoundDetachedMortalEnvelopeValidationResult Shape(WoundDetachedMortalEffectRef[] effects) =>
        WoundConsequenceEnvelopeCatalog.ValidateDetachedMortalShape("draft", effects);

    private static WoundDetachedMortalEnvelopeValidationResult Full(WoundDetachedMortalEffectRef[] effects) =>
        WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(new WoundDetachedMortalEnvelopeRequest(1, "draft", effects));

    private static WoundDetachedMortalEffectRef[] Effects(params JsonElement[] components) =>
        new[] { new WoundDetachedMortalEffectRef("root_effect", "draft.definition", components) };

    private static WoundDetachedMortalEffectRef[] ReactionEffects(WoundDetachedMortalReactionExpansionRef[] expansions, int maximum = 2) =>
        new[] { new WoundDetachedMortalEffectRef("root_effect", "draft.definition", new[] { Reaction(maximum) }, expansions) };

    private static WoundDetachedMortalReactionExpansionRef Expansion(JsonElement component) =>
        new("reaction", "draft.child", new[] { component });

    private static JsonElement Scalar(string value, string cap = "null", string id = "strength") =>
        Component(id, "characteristic_modifier", $$"""{"characteristic":"strength","operation":"flat","value":{{value}},"cap":{{cap}}} """);

    private static JsonElement Action(string action, string operation, string modifier = "null") =>
        Component(action, "action_control", $$"""{"action":"{{action}}","operation":"{{operation}}","modifier":{{modifier}}} """);

    private static JsonElement Reaction(int maximum = 2) => Component("reaction", "event_reaction",
        $$$"""{"eventType":"owner_damaged","resultKind":"apply_definition","dependency":"after_current_event","maxExpansion":{{{maximum}}},"definitionKey":"child","parameters":{}} """);

    private static JsonElement Component(string id, string profile, string payload) =>
        Raw($$"""{"componentId":"{{id}}","profile":"{{profile}}","priority":0,"payload":{{payload}}} """);

    private static JsonElement Raw(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static WoundPersistedConsequenceDefinition Definition(string reference, params JsonElement[] components) =>
        new(reference, "draft." + reference + ".definition", JsonSerializer.SerializeToElement(new { definitionKey = reference, components }));

    private static (WoundPersistedConsequenceDefinition[], WoundPersistedConsequenceRoot[]) Graph(params JsonElement[] components) =>
        (new[] { Definition("root", components) }, new[] { new WoundPersistedConsequenceRoot("root_effect", "root", "draft.root",
            components.OrderBy(component => component.GetProperty("componentId").GetString(), StringComparer.Ordinal)
                .Select((component, index) => new WoundEffectSlotAgreement(index + 1, component.GetProperty("profile").GetString()!, "consequence")).ToArray()) });

    private static WoundPersistedConsequenceEnvelopeValidationResult Adapter(WoundPersistedConsequenceDefinition[] definitions,
        WoundPersistedConsequenceRoot[] roots, bool global, int? used = null) =>
        WoundPersistedConsequenceEnvelopeAdapter.ValidatePrevalidatedDetachedShape("draft", definitions, roots, global, used);

    private static void Issue(IEnumerable<ValidationIssue> issues, string code, string path) =>
        Assert.Contains(issues, issue => issue.Code == code && issue.FilePath == path);

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join("; ", issues.Select(issue => issue.Code + ":" + issue.FilePath));
}
