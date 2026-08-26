using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualWoundEffectProfileContractTests
{
    private static readonly string[] Profiles =
    {
        "spiritual_roll_hindrance",
        "spiritual_action_cost_burden",
        "spiritual_position_burden",
        "spiritual_control_burden",
        "spiritual_strain_burden",
        "spiritual_tempo_burden",
        "spiritual_counter_burden",
        "spiritual_art_restriction"
    };

    private static readonly string[] PersistentTargets =
    {
        "player", "guardian", "resident", "radiant_actor", "afterlife_actor"
    };

    private static readonly string[] AfterlifeRealms =
    {
        "chaos_sea", "shining_abode"
    };

    public static IEnumerable<object[]> ProfileTargetRealmCases =>
        from profile in Profiles
        from target in PersistentTargets
        from realm in AfterlifeRealms
        select new object[] { profile, target, realm };

    public static IEnumerable<object[]> InvalidMagnitudeCases
    {
        get
        {
            yield return Case("spiritual_roll_hindrance", "\"advantage\"");
            yield return Case("spiritual_roll_hindrance", "1");
            yield return Case("spiritual_roll_hindrance", "null");

            yield return Case("spiritual_action_cost_burden", "\"1\"");
            yield return Case("spiritual_action_cost_burden", "1.5");
            yield return Case("spiritual_action_cost_burden", "null");
            yield return Case("spiritual_action_cost_burden", "{}");
            yield return Case("spiritual_action_cost_burden", "[]");
            yield return Case("spiritual_action_cost_burden", "0");
            yield return Case("spiritual_action_cost_burden", "4");

            yield return Case("spiritual_position_burden", "0");
            yield return Case("spiritual_position_burden", "3");
            yield return Case("spiritual_position_burden", "1.5");
            yield return Case("spiritual_control_burden", "0");
            yield return Case("spiritual_control_burden", "2");
            yield return Case("spiritual_control_burden", "1.0");
            yield return Case("spiritual_strain_burden", "0");
            yield return Case("spiritual_strain_burden", "2");
            yield return Case("spiritual_strain_burden", "1.0");

            yield return Case("spiritual_tempo_burden", "\"deny_two_gains\"");
            yield return Case("spiritual_tempo_burden", "1");
            yield return Case("spiritual_counter_burden", "\"deny_one_gain\"");
            yield return Case("spiritual_counter_burden", "1");
            yield return Case("spiritual_art_restriction", "\"disadvantage\"");
            yield return Case("spiritual_art_restriction", "1");
        }
    }

    [Fact]
    public void Registry_ContainsExactlyEveryClosedSpiritualWoundProfile()
    {
        Assert.Equal(
            Profiles.OrderBy(static value => value, StringComparer.Ordinal),
            WoundConsequenceEnvelopeCatalog.SpiritualProfiles
                .OrderBy(static value => value, StringComparer.Ordinal));

        foreach (var profile in Profiles)
        {
            Assert.Contains(profile, EffectComponentProfiles.RegisteredProfiles);
            Assert.True(EffectComponentProfiles.TryGetDescriptor(profile, out var descriptor));
            Assert.Equal(profile, descriptor.Profile);
            Assert.Equal(EffectComponentResolutionMode.Deterministic, descriptor.ResolutionMode);
            Assert.Equal(
                new[] { "profile_specific" },
                descriptor.LegalMergeReducers.OrderBy(static value => value, StringComparer.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.ProjectionDescriptor));
        }
    }

    [Theory]
    [MemberData(nameof(ProfileTargetRealmCases))]
    public void CompleteDefinition_EachProfileTargetAndAfterlifeRealm_IsAccepted(
        string profile,
        string target,
        string realm)
    {
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            profile,
            target,
            realm);

        var issues = ValidateDefinitions(realm, definition);

        Assert.Empty(issues);
        Assert.Equal("source_bound", definition["lifetime"]!["mode"]!.GetValue<string>());
        Assert.Equal("wound", definition["links"]![0]!["kind"]!.GetValue<string>());
        Assert.Equal("source", definition["links"]![0]!["role"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void CompleteDefinition_AllowsBothAfterlifeRealmsTogether(string currentRealm)
    {
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            "spiritual_roll_hindrance",
            realm: currentRealm);
        definition["allowedRealms"] = new JsonArray("chaos_sea", "shining_abode");

        Assert.Empty(ValidateDefinitions(currentRealm, definition));
    }

    [Theory]
    [InlineData("current_mortal_realm", "definitions[0].allowedRealms", "effect_source_definition_spiritual_wound_realm_invalid")]
    [InlineData("allowed_mortal_realm", "definitions[0].allowedRealms", "effect_source_definition_spiritual_wound_realm_invalid")]
    [InlineData("spiritual_conflict_side", "definitions[0].allowedTargetKinds", "effect_source_definition_spiritual_wound_target_invalid")]
    [InlineData("npc_target", "definitions[0].allowedTargetKinds", "effect_source_definition_spiritual_wound_target_invalid")]
    [InlineData("missing_link", "definitions[0].links", "effect_source_definition_spiritual_wound_link_invalid")]
    [InlineData("wrong_link_kind", "definitions[0].links[0].kind", "effect_source_definition_spiritual_wound_link_invalid")]
    [InlineData("wrong_link_role", "definitions[0].links[0].role", "effect_source_definition_spiritual_wound_link_invalid")]
    [InlineData("duplicate_link", "definitions[0].links[1]", "effect_source_definition_spiritual_wound_link_invalid")]
    [InlineData("confusable_link", "definitions[0].links[0].targetId", "effect_source_definition_spiritual_wound_link_invalid")]
    public void CompleteDefinition_SpiritualWoundScopeIsClosed(
        string mutation,
        string expectedPath,
        string expectedCode)
    {
        const string woundId = "wound_spiritual_test";
        var realm = "chaos_sea";
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            "spiritual_roll_hindrance",
            woundId: woundId);
        switch (mutation)
        {
            case "current_mortal_realm":
                realm = "mortal_world";
                definition["allowedRealms"] = new JsonArray("mortal_world");
                break;
            case "allowed_mortal_realm":
                definition["allowedRealms"] = new JsonArray("chaos_sea", "mortal_world");
                break;
            case "spiritual_conflict_side":
                definition["allowedTargetKinds"] = new JsonArray("spiritual_conflict_side");
                break;
            case "npc_target":
                definition["allowedTargetKinds"] = new JsonArray("npc");
                break;
            case "missing_link":
                definition["links"] = new JsonArray();
                break;
            case "wrong_link_kind":
                definition["links"]![0]!["kind"] = "quest";
                break;
            case "wrong_link_role":
                definition["links"]![0]!["role"] = "context";
                break;
            case "duplicate_link":
                definition["links"]!.AsArray().Add(definition["links"]![0]!.DeepClone());
                break;
            case "confusable_link":
                definition["links"]![0]!["targetId"] = "wound_sp\u0456ritual_test";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var issues = ValidateDefinitions(realm, definition);

        AssertIssue(issues, expectedPath, expectedCode);
    }

    [Theory]
    [MemberData(nameof(InvalidMagnitudeCases))]
    public void ComponentMagnitude_RejectsWrongTypeOrExactDomain(
        string profile,
        string invalidMagnitudeJson)
    {
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(profile);
        effect["components"]![0]!["payload"]!["magnitude"] =
            JsonNode.Parse(invalidMagnitudeJson);
        var effectIssues = ValidateEffect(effect);

        AssertIssue(
            effectIssues,
            "effect.components[0].payload.magnitude",
            "effect_materialization_invalid_component");

        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(profile);
        definition["components"]![0]!["payload"]!["magnitude"] =
            JsonNode.Parse(invalidMagnitudeJson);
        AssertIssue(
            ValidateDefinitions("chaos_sea", definition),
            "definitions[0].components[0].payload.magnitude",
            "effect_source_definition_invalid_components");
    }

    [Theory]
    [InlineData("missing_operation", "effect.components[0].payload.operation")]
    [InlineData("extra_payload", "effect.components[0].payload.unregistered")]
    [InlineData("wrong_axis", "effect.components[0].payload.axis")]
    [InlineData("art_force_incarnation", "effect.components[0].payload.operation")]
    [InlineData("confusable_profile", "effect.components[0].profile")]
    public void ComponentPayload_IsClosedProfileExactAndArtSafe(
        string mutation,
        string expectedPath)
    {
        var profile = mutation == "art_force_incarnation"
            ? "spiritual_art_restriction"
            : "spiritual_roll_hindrance";
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(profile);
        switch (mutation)
        {
            case "missing_operation":
                effect["components"]![0]!["payload"]!.AsObject().Remove("operation");
                break;
            case "extra_payload":
                effect["components"]![0]!["payload"]!["unregistered"] = true;
                break;
            case "wrong_axis":
                effect["components"]![0]!["payload"]!["axis"] = "sideStrain";
                break;
            case "art_force_incarnation":
                effect["components"]![0]!["payload"]!["operation"] = "force_incarnation";
                break;
            case "confusable_profile":
                effect["components"]![0]!["profile"] = "spiritual_roll_h\u0456ndrance";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var issues = ValidateEffect(effect);

        AssertIssue(
            issues,
            expectedPath,
            mutation == "confusable_profile"
                ? "effect_materialization_unknown_profile"
                : "effect_materialization_invalid_component");
    }

    [Fact]
    public void SourceBoundSpiritualWound_IsNotAfterlifeCombatConditionAdapter()
    {
        var spiritual = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            "spiritual_roll_hindrance");
        Assert.Empty(ValidateDefinitions("chaos_sea", spiritual));

        var finiteCondition = EffectMaterializationTestFixture.CreateDefinition(
            "afterlife_combat_condition");
        Assert.Empty(ValidateDefinitions("chaos_sea", finiteCondition));

        var condition = finiteCondition.DeepClone().AsObject();
        condition["allowedTargetKinds"] = new JsonArray("guardian");
        condition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "expire"
        };
        condition["links"] = new JsonArray(new JsonObject
        {
            ["kind"] = "wound",
            ["targetId"] = "wound_spiritual_test",
            ["role"] = "source"
        });

        AssertIssue(
            ValidateDefinitions("chaos_sea", condition),
            "definitions[0].lifetime",
            "effect_source_definition_afterlife_condition_lifetime_invalid");
    }

    [Theory]
    [MemberData(nameof(ProjectionProfiles))]
    public void VisibleCanonicalSpiritualWoundEffect_HasSafePlayerProjection(string profile)
    {
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(profile);
        var profileRoot = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["profiles"] = new JsonArray(new JsonObject
            {
                ["actorType"] = "guardian",
                ["actorId"] = "afterlife_actor_test",
                ["realm"] = "Chaos Sea",
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            })
        };
        var snapshot = EffectMechanicsSnapshot.Build(new EffectMechanicsInput(
            new EffectCarrierCatalogInput(null, null, null, null, profileRoot, null),
            EffectMaterializationTestFixture.CreateIdentityIndex(effect)));
        Assert.True(snapshot.IsAccepted, Describe(snapshot.Issues));

        var projection = EffectPlayerProjection.Build(new EffectPlayerProjectionInput(snapshot));

        Assert.True(projection.IsAvailable);
        var entry = Assert.Single(projection.Entries);
        Assert.Contains(entry.Facts, fact =>
            string.Equals(fact.Kind, profile, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(fact.Value));
        var visible = JsonSerializer.Serialize(projection);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, visible, StringComparison.Ordinal);
        Assert.DoesNotContain("wound_spiritual_test", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("definition_spiritual_wound_test", visible, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> ProjectionProfiles =>
        Profiles.Select(static profile => new object[] { profile });

    [Fact]
    public void SpiritualWoundOwnedSourceGraph_PreservesTwoComponentsOnOneRoot()
    {
        var wound = WoundContractTestData.CreateSpiritualActiveWound();
        var sources = wound["consequences"]!["ownedEffectSources"]!.AsObject();
        var definitions = sources["definitions"]!.AsArray();
        var root = definitions[0]!.DeepClone().AsObject();
        var secondComponent = definitions[1]!["components"]![0]!.DeepClone();
        root["components"]!.AsArray().Add(secondComponent);
        root["triggers"]![0]!["componentIds"]!.AsArray().Add("component_spiritual_cost");
        sources["definitions"] = new JsonArray(root);
        sources["rootBindings"]!.AsArray().RemoveAt(1);
        wound["consequences"]!["entries"]![1]!["effectId"] = "effect_spiritual_roll";

        var parsed = WoundMaterializationContract.Parse(wound.ToJsonString(), "wound");

        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var canonical = WoundMaterializationContract.SerializeCanonical(parsed.Wound!);
        var canonicalRoot = JsonNode.Parse(canonical)!.AsObject();
        var canonicalSources = canonicalRoot["consequences"]!["ownedEffectSources"]!;
        Assert.Single(canonicalSources["definitions"]!.AsArray());
        Assert.Single(canonicalSources["rootBindings"]!.AsArray());
        Assert.Equal(2, canonicalSources["definitions"]![0]!["components"]!.AsArray().Count);
        Assert.Contains("spiritual_roll_hindrance", canonical, StringComparison.Ordinal);
        Assert.Contains("spiritual_action_cost_burden", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("afterlife_combat_condition", canonical, StringComparison.Ordinal);

        var reparsed = WoundMaterializationContract.Parse(canonical, "wound");
        Assert.True(reparsed.IsValid, Describe(reparsed.Issues));
        Assert.Equal(canonical, WoundMaterializationContract.SerializeCanonical(reparsed.Wound!));
        Assert.Equal(
            WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound!),
            WoundIdentityState.ComputeSemanticFingerprint(reparsed.Wound!));
    }

    private static object[] Case(string profile, string invalidJson) =>
        new object[] { profile, invalidJson };

    private static IReadOnlyList<ValidationIssue> ValidateDefinitions(
        string realm,
        params JsonObject[] definitions)
    {
        using var document = JsonDocument.Parse(
            new JsonArray(definitions.Select(static definition => definition.DeepClone()).ToArray())
                .ToJsonString());
        return EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "definitions",
            realm);
    }

    private static IReadOnlyList<ValidationIssue> ValidateEffect(JsonObject effect)
    {
        using var document = JsonDocument.Parse(effect.ToJsonString());
        return EffectMaterializationContract.Validate(
            document.RootElement,
            "effect",
            EffectMaterializationPhase.CanonicalActive);
    }

    private static void AssertIssue(
        IReadOnlyList<ValidationIssue> issues,
        string path,
        string code) =>
        Assert.Contains(issues, issue =>
            string.Equals(issue.FilePath, path, StringComparison.Ordinal) &&
            string.Equals(issue.Code, code, StringComparison.Ordinal));

    private static string Describe(IReadOnlyList<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(static issue =>
            $"{issue.Code} {issue.FilePath}: {issue.Expected} / {issue.Actual}"));
}
