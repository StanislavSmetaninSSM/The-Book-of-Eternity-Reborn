using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Foreign cards reuse known typed facts without borrowing Self or granting an action.</summary>
public sealed class WoundForeignDetailProjectionTests
{
    /// <summary>The complete known card includes readable facts and exact owned effects for each supported nearby kind.</summary>
    [Theory]
    [InlineData("guardian")]
    [InlineData("resident")]
    public void KnownCardIncludesFactsRecoveryMethodsAndOnlyItsOwnedEffect(string kind)
    {
        var data = WoundForeignDetailTestData.Create(kind);
        var input = WoundForeignDetailTestData.WithEffect(data.Input, kind, "visible").Input;
        var context = Context(input); var target = Target(context, data.Guardians, data.Residents, kind);
        var result = WoundPlayerProjection.ProjectKnownTarget(context, target);
        Assert.True(result.IsAvailable); var wound = Assert.Single(result.Active);
        Assert.Equal("Известная рана обитателя", wound.Name); Assert.Equal("II", wound.Severity);
        Assert.Contains(wound.Facts, f => f.Label == "Признаки" && f.Value == WoundForeignDetailTestData.VisibleSymptom);
        Assert.Contains(wound.Facts, f => f.Label == "Место" && f.Value == "воля и духовное равновесие");
        Assert.Contains(wound.Facts, f => f.Label == "Уход"); Assert.Contains(wound.Facts, f => f.Label == "Прогресс");
        Assert.Contains(wound.Facts, f => f.Label == "Прогноз");
        Assert.Contains(wound.Facts, f => f.Label == "Последствие" && f.Value == "Видимая помеха обитателю");
        Assert.Equal("Укрепить духовную ткань", Assert.Single(wound.Methods).Name);
        Assert.Contains("не подтверждают наличие", wound.Methods[0].Policy);
        Assert.Contains("рекомендация", wound.Methods[0].Policy); Assert.Contains("пока недоступно", wound.Methods[0].Policy);
        Assert.Empty(result.History);
        foreach (var secret in new[] { "ЛИЧНАЯ_РАНА_ИГРОКА", "ЛИЧНАЯ_ИСТОРИЯ", "wound_", "effect_", "definition_", "guardian_detail", "resident_detail", "sha256:", "Selector", "Actions", "CanonicalEffect" })
            Assert.DoesNotContain(secret, Text(result));
    }

    /// <summary>A matching effect ID never authorizes hidden, foreign or differently bound facts.</summary>
    [Theory]
    [InlineData("hidden_effect")]
    [InlineData("other_source")]
    [InlineData("other_definition")]
    [InlineData("other_owner")]
    [InlineData("hidden_complication")]
    public void PrivateOrForeignEffectsDoNotBecomeKnownConsequences(string scenario)
    {
        var data = WoundForeignDetailTestData.Create(adjust: scenario == "hidden_complication" ? wound =>
            wound["complications"] = new JsonArray(new JsonObject
            { ["complicationId"] = "secret_complication", ["kind"] = "pain", ["state"] = "active", ["displayName"] = "СЕКРЕТНОЕ_ОСЛОЖНЕНИЕ",
                ["treatmentDifficultyModifier"] = 1, ["ownedEffectIds"] = new JsonArray("foreign_effect_spiritual_roll"), ["visibility"] = "hidden" }) : null);
        var context = Context(WoundForeignDetailTestData.WithEffect(data.Input, "guardian", scenario).Input);
        var result = WoundPlayerProjection.ProjectKnownTarget(context, Target(context, data.Guardians, data.Residents, "guardian"));
        Assert.True(result.IsAvailable); Assert.Single(result.Active);
        Assert.DoesNotContain("Видимая помеха обитателю", Text(result)); Assert.DoesNotContain("СЕКРЕТНОЕ_ОСЛОЖНЕНИЕ", Text(result));
    }

    /// <summary>Only retained current route knowledge is printed; future diagnosis does not reveal its outputs.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HiddenRouteNeedsCurrentKnowledgeAndFutureDiagnosisCannotGrantIt(bool known)
    {
        var data = WoundForeignDetailTestData.Create(adjust: wound =>
        {
            wound["treatment"]!["routes"]![0]!["visibility"] = "hidden";
            if (!known) wound["treatment"]!["knownRouteIds"] = new JsonArray();
            wound["treatment"]!["diagnosisPaths"] = new JsonArray(Diagnosis("public", "Явный осмотр", []),
                Diagnosis("hidden", "Будущий закрытый осмотр", ["route:route_detail_known"], "later"),
                Diagnosis("gm_only", "ПРИВАТНАЯ_ДИАГНОСТИКА", [], "private"));
        });
        var context = Context(data.Input);
        var result = WoundPlayerProjection.ProjectKnownTarget(context, Target(context, data.Guardians, data.Residents, "guardian"));
        var wound = Assert.Single(result.Active);
        Assert.Equal(known ? 1 : 0, wound.Methods.Count);
        Assert.Contains(wound.Diagnosis, path => path.Name == "Явный осмотр");
        Assert.Equal(known, wound.Diagnosis.Any(path => path.Name == "Будущий закрытый осмотр"));
        Assert.DoesNotContain("ПРИВАТНАЯ_ДИАГНОСТИКА", Text(result));
        if (!known) Assert.DoesNotContain("Укрепить духовную ткань", Text(result));
        Assert.DoesNotContain("route_detail_known", Text(result));
    }

    /// <summary>Unsupported physical requirements and stale/hidden/mistyped bindings cannot enter a foreign card.</summary>
    [Theory]
    [InlineData("physical")]
    [InlineData("hidden_profile")]
    [InlineData("wrong_kind")]
    [InlineData("stale_wound")]
    [InlineData("unaccepted_effects")]
    public void InvalidForeignDetailFailsClosedWithoutBorrowingPlayerKnowledge(string scenario)
    {
        var data = WoundForeignDetailTestData.Create(); var context = Context(data.Input);
        var target = Target(context, data.Guardians, data.Residents, "guardian");
        if (scenario == "physical")
        {
            data = WoundForeignDetailTestData.Create(adjust: wound =>
            {
                wound["classification"]!["domain"] = "physical";
                wound["consequences"]!["slotsUsed"] = 0; wound["consequences"]!["entries"] = new JsonArray();
                wound["consequences"]!["ownedEffectSources"]!["definitions"] = new JsonArray();
                wound["consequences"]!["ownedEffectSources"]!["rootBindings"] = new JsonArray();
            });
            context = Context(data.Input with { KnownItemNames = new Dictionary<string, string> { ["sterile_thread"] = "ЛИЧНЫЕ_МАТЕРИАЛЫ" } });
            target = Target(context, data.Guardians, data.Residents, "guardian");
        }
        if (scenario == "hidden_profile") context.Input.Carriers.AfterlifeProfiles!["profiles"]![1]!["visibility"] = "gm_only";
        if (scenario == "wrong_kind") target = target with { Owner = target.Owner with { OwnerKind = "resident" } };
        if (scenario == "stale_wound") context = Context(WoundForeignDetailTestData.Create(adjust: wound => wound["display"]!["description"] = "Изменённая рана").Input);
        if (scenario == "unaccepted_effects") context = context with { Input = context.Input with { Effects = new(false, [], [], []) } };
        var result = WoundPlayerProjection.ProjectKnownTarget(context, target);
        Assert.False(result.IsAvailable); Assert.Empty(result.Active); Assert.Empty(result.History);
        Assert.DoesNotContain("ЛИЧНЫЕ_МАТЕРИАЛЫ", Text(result));
    }

    /// <summary>Returned detail is detached and never changes the canonical mutable input.</summary>
    [Fact]
    public void DetailExportDoesNotRetainMutableDisplayNodes()
    {
        var data = WoundForeignDetailTestData.Create(); var context = Context(data.Input);
        var target = Target(context, data.Guardians, data.Residents, "guardian");
        var before = data.Input.Carriers.AfterlifeProfiles!.ToJsonString();
        var result = WoundPlayerProjection.ProjectKnownTarget(context, target);
        Assert.Equal(before, data.Input.Carriers.AfterlifeProfiles.ToJsonString());
        data.Input.Carriers.AfterlifeProfiles["profiles"]![1]!["activeWounds"]![0]!["display"]!["visibleSymptoms"]![0] = "Изменили вход";
        Assert.Contains(Assert.Single(result.Active).Facts, f => f.Value == WoundForeignDetailTestData.VisibleSymptom);
    }

    /// <summary>A resident shares an ID with a Guardian, but only its own typed effects may enter its card.</summary>
    [Fact]
    public void SameIdGuardianEffectCannotBorrowTheResidentsWoundBinding()
    {
        var data = WoundForeignDetailTestData.Create("resident", sameId: true);
        var profiles = data.Input.Carriers.AfterlifeProfiles!;
        JsonObject Effect(string kind, string effectId, string definition, string name, string profile)
        {
            var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(profile, kind, woundId: "wound_foreign_detail");
            effect["effectId"] = effectId; effect["target"]!["targetId"] = WoundForeignDetailTestData.GuardianId;
            effect["source"]!["definitionKey"] = definition; effect["display"]!["name"] = name; return effect;
        }
        // All read-schema/identity coordinates are valid. The Guardian row deliberately
        // claims the resident wound's first root: matching IDs/source alone are insufficient.
        var guardianEffect = Effect("guardian", "foreign_effect_spiritual_roll", "definition_spiritual_roll", "ЧУЖОЕ_ВЛИЯНИЕ_ХРАНИТЕЛЯ", "spiritual_roll_hindrance");
        var residentEffect = Effect("resident", "foreign_effect_spiritual_cost", "definition_spiritual_cost", "Видимое влияние жителя", "spiritual_action_cost_burden");
        const string residentTransition = "effect_transition_resident_detail";
        const string residentEvent = "turn_42:resident_detail";
        residentEffect["chronology"]!["lastTransitionId"] = residentTransition;
        residentEffect["chronology"]!["createdEventRef"] = residentEvent;
        profiles["profiles"]![1]!["activeEffects"] = new JsonArray(guardianEffect.DeepClone());
        profiles["profiles"]![2]!["activeEffects"] = new JsonArray(residentEffect.DeepClone());
        var identities = EffectMaterializationTestFixture.CreateIdentityIndex(guardianEffect, residentEffect);
        // The minimal factory defaults to one event; two independent effects need
        // distinct immutable transitions matching their canonical chronology.
        identities["entries"]![1]!["transitions"]![0]!["transitionId"] = residentTransition;
        identities["entries"]![1]!["transitions"]![0]!["eventRef"] = residentEvent;
        var snapshot = EffectMechanicsSnapshot.Build(new(new EffectCarrierCatalogInput(null, null, null, null, profiles, null),
            identities));
        Assert.True(snapshot.IsAccepted, string.Join(";", snapshot.Issues));
        var context = Context(data.Input with { Effects = snapshot });
        var target = Target(context, data.Guardians, data.Residents, "resident");
        Assert.Equal(WoundForeignDetailTestData.GuardianId, target.Owner.OwnerId);
        var card = WoundPlayerProjection.ProjectKnownTarget(context, target);
        Assert.True(card.IsAvailable); Assert.Single(card.Active);
        Assert.Contains("Видимое влияние жителя", Text(card));
        Assert.DoesNotContain("ЧУЖОЕ_ВЛИЯНИЕ_ХРАНИТЕЛЯ", Text(card));
    }

    private static WoundPlayerReadContext Context(WoundPlayerProjectionInput input)
    {
        var catalog = WoundCarrierCatalog.Build(input.Carriers); Assert.Empty(catalog.Issues);
        Assert.Empty(input.History.ValidateAgreement(input.Identities, catalog));
        var projection = WoundPlayerProjection.Build(input, catalog); Assert.True(projection.IsAvailable);
        return new(input, catalog, new JsonObject { ["currentRealm"] = "Chaos Sea" }, null,
            projection with { History = [new("ЛИЧНАЯ_ИСТОРИЯ", "II", "Закрытый личный итог")] });
    }

    private static WoundTargetChoice Target(WoundPlayerReadContext context, JsonObject guardians, JsonObject residents, string kind) =>
        Assert.Single(WoundTargetSelectionService.Build(context, guardians, residents), t => t.Owner.OwnerKind == kind);

    private static JsonObject Diagnosis(string visibility, string name, string[] facts, string id = "public") => new()
    {
        ["diagnosisPathId"] = id, ["displayName"] = name, ["visibility"] = visibility,
        ["requiresKnownFacts"] = new JsonArray(facts.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()),
        ["requirements"] = new JsonArray(), ["check"] = new JsonObject(),
        ["reveals"] = new JsonArray("route:route_detail_known"), ["failurePolicy"] = "no_reveal"
    };

    private static string Text(object value) => JsonSerializer.Serialize(value,
        new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
