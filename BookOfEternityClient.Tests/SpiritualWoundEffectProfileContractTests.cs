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

    private static readonly string[] Operations =
    {
        "pressure", "counter", "guard", "maneuver", "binding", "break_binding",
        "force_binding", "force_incarnation", "incarnation_resistance",
        "champion_coordination", "recover_spiritual_power"
    };

    private static readonly string[] Axes =
    {
        "rollMode", "actionCostAudit", "conflictPosition", "controlState",
        "sideStrain", "tempoAdvantage", "counterPayoff", "artAvailability"
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

    public static IEnumerable<object[]> ProfileCases =>
        Profiles.Select(static profile => new object[] { profile });

    public static IEnumerable<object[]> ValidOperationCases
    {
        get
        {
            foreach (var profile in Profiles)
            foreach (var operation in Operations)
            {
                if (string.Equals(profile, "spiritual_art_restriction", StringComparison.Ordinal) &&
                    string.Equals(operation, "force_incarnation", StringComparison.Ordinal))
                {
                    continue;
                }
                yield return Case(profile, operation);
            }
        }
    }

    public static IEnumerable<object[]> InvalidOperationCases
    {
        get
        {
            foreach (var profile in Profiles)
            {
                foreach (var operation in new[]
                         {
                             "spiritual_healing", "unknown_operation", "Pressure", "pressur\u0435"
                         })
                {
                    yield return Case(profile, operation);
                }
            }
            yield return Case("spiritual_art_restriction", "force_incarnation");
        }
    }

    public static IEnumerable<object[]> InvalidAxisCases
    {
        get
        {
            foreach (var profile in Profiles)
            {
                var expectedAxis = AxisFor(profile);
                foreach (var axis in Axes.Where(axis =>
                             !string.Equals(axis, expectedAxis, StringComparison.Ordinal)))
                {
                    yield return Case(profile, axis);
                }
                yield return Case(profile, ToggleFirstCharacterCase(expectedAxis));
            }
        }
    }

    public static IEnumerable<object[]> ValidMagnitudeCases
    {
        get
        {
            yield return Case("spiritual_roll_hindrance", "\"disadvantage\"");
            yield return Case("spiritual_action_cost_burden", "1");
            yield return Case("spiritual_action_cost_burden", "2");
            yield return Case("spiritual_action_cost_burden", "3");
            yield return Case("spiritual_position_burden", "1");
            yield return Case("spiritual_position_burden", "2");
            yield return Case("spiritual_control_burden", "1");
            yield return Case("spiritual_strain_burden", "1");
            yield return Case("spiritual_tempo_burden", "\"deny_one_gain\"");
            yield return Case("spiritual_counter_burden", "\"reduce_one_step\"");
            yield return Case("spiritual_art_restriction", "\"restrict\"");
            yield return Case("spiritual_art_restriction", "\"forbid\"");
        }
    }

    public static IEnumerable<object[]> InvalidPayloadCases
    {
        get
        {
            foreach (var profile in Profiles)
            {
                foreach (var member in new[] { "operation", "axis", "magnitude" })
                {
                    yield return PayloadCase(profile, member, "missing", null);
                    yield return PayloadCase(profile, member, "value", "null");
                }

                yield return PayloadCase(profile, "Operation", "case", "\"pressure\"");
                yield return PayloadCase(profile, "Axis", "case", $"\"{AxisFor(profile)}\"");
                yield return PayloadCase(profile, "Magnitude", "case", DefaultMagnitudeJson(profile));

                foreach (var member in new[] { "operation", "axis" })
                foreach (var invalidJson in new[] { "1", "true", "{}", "[]" })
                    yield return PayloadCase(profile, member, "value", invalidJson);

                foreach (var invalidJson in InvalidMagnitudeJson(profile))
                    yield return PayloadCase(profile, "magnitude", "value", invalidJson);

                yield return PayloadCase(profile, "unregistered", "extra", "true");
            }
        }
    }

    public static IEnumerable<object[]> InvalidScopeCases
    {
        get
        {
            var cases = new (string Mutation, string Path, string Code)[]
            {
                ("current_mortal_realm", "definitions[0].allowedRealms", "effect_source_definition_spiritual_wound_realm_invalid"),
                ("allowed_mortal_realm", "definitions[0].allowedRealms", "effect_source_definition_spiritual_wound_realm_invalid"),
                ("spiritual_conflict_side", "definitions[0].allowedTargetKinds", "effect_source_definition_spiritual_wound_target_invalid"),
                ("npc_target", "definitions[0].allowedTargetKinds", "effect_source_definition_spiritual_wound_target_invalid"),
                ("combatant_target", "definitions[0].allowedTargetKinds", "effect_source_definition_spiritual_wound_target_invalid"),
                ("mixed_target", "definitions[0].allowedTargetKinds", "effect_source_definition_spiritual_wound_target_invalid"),
                ("missing_link", "definitions[0].links", "effect_source_definition_spiritual_wound_link_invalid"),
                ("wrong_link_kind", "definitions[0].links[0].kind", "effect_source_definition_spiritual_wound_link_invalid"),
                ("wrong_link_role", "definitions[0].links[0].role", "effect_source_definition_spiritual_wound_link_invalid"),
                ("duplicate_link", "definitions[0].links[1]", "effect_source_definition_spiritual_wound_link_invalid"),
                ("confusable_duplicate_link", "definitions[0].links[1]", "effect_source_definition_spiritual_wound_link_invalid")
            };
            foreach (var profile in Profiles)
            foreach (var (mutation, path, code) in cases)
                yield return new object[] { profile, mutation, path, code };
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
    [MemberData(nameof(ValidOperationCases))]
    public void CommonRegistry_AcceptsEveryExactProfileOperationAndAxisThroughBothBoundaries(
        string profile,
        string operation)
    {
        AssertAcceptedByBothBoundaries(
            profile,
            operation,
            ParseRequiredJson(DefaultMagnitudeJson(profile)));
    }

    [Theory]
    [MemberData(nameof(InvalidOperationCases))]
    public void CommonRegistry_RejectsSafetyUnknownCaseConfusableAndIneligibleOperationsThroughBothBoundaries(
        string profile,
        string operation)
    {
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(profile);
        EffectPayload(effect)["operation"] = operation;
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(profile);
        DefinitionPayload(definition)["operation"] = operation;

        AssertRejectedByBothBoundaries(
            effect,
            definition,
            "operation");
    }

    [Theory]
    [MemberData(nameof(InvalidAxisCases))]
    public void CommonRegistry_RejectsEveryOtherRegisteredAndOrdinalCaseAxisThroughBothBoundaries(
        string profile,
        string axis)
    {
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(profile);
        EffectPayload(effect)["axis"] = axis;
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(profile);
        DefinitionPayload(definition)["axis"] = axis;

        AssertRejectedByBothBoundaries(effect, definition, "axis");
    }

    [Theory]
    [MemberData(nameof(ValidMagnitudeCases))]
    public void CommonRegistry_AcceptsEveryExactMagnitudeEndpointThroughBothBoundaries(
        string profile,
        string magnitudeJson)
    {
        AssertAcceptedByBothBoundaries(
            profile,
            "counter",
            ParseRequiredJson(magnitudeJson));
    }

    [Theory]
    [MemberData(nameof(InvalidPayloadCases))]
    public void CommonRegistry_RejectsMissingNullWrongTypeLexemeAndClosedPayloadThroughBothBoundaries(
        string profile,
        string member,
        string mutation,
        string? valueJson)
    {
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(profile);
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(profile);
        MutatePayload(EffectPayload(effect), member, mutation, valueJson);
        MutatePayload(DefinitionPayload(definition), member, mutation, valueJson);

        AssertRejectedByBothBoundaries(effect, definition, member);
    }

    [Fact]
    public void CommonRegistry_RejectsConfusableProfileThroughBothBoundaries()
    {
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(
            "spiritual_roll_hindrance");
        effect["components"]![0]!["profile"] = "spiritual_roll_h\u0456ndrance";
        var effectIssues = ValidateEffect(effect);
        Assert.NotEmpty(effectIssues);
        AssertIssue(
            effectIssues,
            "effect.components[0].profile",
            "effect_materialization_unknown_profile");

        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            "spiritual_roll_hindrance");
        definition["components"]![0]!["profile"] = "spiritual_roll_h\u0456ndrance";
        var definitionIssues = ValidateDefinitions("chaos_sea", definition);
        Assert.NotEmpty(definitionIssues);
        AssertIssue(
            definitionIssues,
            "definitions[0].components[0].profile",
            "effect_source_definition_invalid_components");
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
    [MemberData(nameof(ProfileCases))]
    public void CompleteDefinition_AllowsAllFivePersistentTargetsAsOneNonEmptySubset(
        string profile)
    {
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            profile);
        definition["allowedTargetKinds"] = new JsonArray(
            PersistentTargets.Select(static target => (JsonNode?)target).ToArray());

        Assert.Empty(ValidateDefinitions("chaos_sea", definition));
    }

    [Theory]
    [MemberData(nameof(ProfileCases))]
    public void CompleteDefinition_AllowsOneWoundSourceLinkWithIndependentContextSibling(
        string profile)
    {
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            profile);
        definition["links"]!.AsArray().Add(new JsonObject
        {
            ["kind"] = "combat",
            ["targetId"] = "afterlife_conflict_context_001",
            ["role"] = "context"
        });

        Assert.Empty(ValidateDefinitions("chaos_sea", definition));
    }

    [Theory]
    [MemberData(nameof(ProfileCases))]
    public void CompleteDefinition_AllowsIndependentWoundContextSibling(
        string profile)
    {
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            profile);
        definition["links"]!.AsArray().Add(new JsonObject
        {
            ["kind"] = "wound",
            ["targetId"] = "wound_related_context_001",
            ["role"] = "context"
        });

        Assert.Empty(ValidateDefinitions("chaos_sea", definition));
    }

    [Theory]
    [MemberData(nameof(ProfileCases))]
    public void CompleteDefinition_AllowsOrdinaryAfterlifeTurnsLifetime(string profile)
    {
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            profile);
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "turns",
            ["initialTurns"] = 2,
            ["advancePhase"] = "afterlife_exchange_end"
        };

        Assert.Empty(ValidateDefinitions("chaos_sea", definition));
    }

    [Theory]
    [MemberData(nameof(InvalidScopeCases))]
    public void CompleteDefinition_SpiritualWoundScopeIsClosed(
        string profile,
        string mutation,
        string expectedPath,
        string expectedCode)
    {
        const string woundId = "wound_spiritual_test";
        var realm = "chaos_sea";
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            profile,
            woundId: woundId);
        switch (mutation)
        {
            case "current_mortal_realm":
                realm = "mortal_world";
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
            case "combatant_target":
                definition["allowedTargetKinds"] = new JsonArray("combatant");
                break;
            case "mixed_target":
                definition["allowedTargetKinds"] = new JsonArray("guardian", "combatant");
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
            case "confusable_duplicate_link":
                definition["links"]!.AsArray().Add(new JsonObject
                {
                    ["kind"] = "wound",
                    ["targetId"] = "wound_sp\u0456ritual_test",
                    ["role"] = "source"
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var issues = ValidateDefinitions(realm, definition);

        AssertIssue(issues, expectedPath, expectedCode);
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
    [MemberData(nameof(ProjectionCases))]
    public void VisibleCanonicalSpiritualWoundEffect_ProjectsExactMechanicsWithoutTechnicalIdentity(
        string profile,
        string operation,
        string magnitudeJson,
        string expectedLabel,
        string expectedValue)
    {
        const string effectId = "effect_projection_private_001";
        const string componentId = "component_projection_private_001";
        const string stackKey = "stack_projection_private_001";
        const string targetId = "actor_projection_private_001";
        const string woundId = "wound_projection_private_001";
        const string definitionKey = "definition_projection_private_001";
        const string contextId = "conflict_projection_private_001";
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(
            profile,
            targetKind: "guardian",
            woundId: woundId,
            operation: operation,
            magnitude: ParseRequiredJson(magnitudeJson));
        effect["effectId"] = effectId;
        effect["target"]!["targetId"] = targetId;
        effect["source"]!["sourceId"] = woundId;
        effect["source"]!["definitionKey"] = definitionKey;
        effect["components"]![0]!["componentId"] = componentId;
        effect["triggers"]![0]!["componentIds"] = new JsonArray(componentId);
        effect["stacking"]!["stackKey"] = stackKey;
        effect["lifetime"]!["targetId"] = woundId;
        effect["links"]![0]!["targetId"] = woundId;
        effect["links"]!.AsArray().Add(new JsonObject
        {
            ["kind"] = "combat",
            ["targetId"] = contextId,
            ["role"] = "context"
        });
        var profileRoot = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["profiles"] = new JsonArray(new JsonObject
            {
                ["actorType"] = "guardian",
                ["actorId"] = targetId,
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
        var fact = Assert.Single(entry.Facts, fact =>
            string.Equals(fact.Kind, profile, StringComparison.Ordinal));
        Assert.Equal(expectedLabel, fact.Label);
        Assert.Equal(expectedValue, fact.Value);
        foreach (var rawPropertyName in new[] { "operation", "axis", "magnitude" })
            Assert.DoesNotContain(rawPropertyName, fact.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(operation, fact.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AxisFor(profile), fact.Value, StringComparison.OrdinalIgnoreCase);
        var magnitudeNode = ParseRequiredJson(magnitudeJson);
        if (magnitudeNode is JsonValue magnitudeValue &&
            magnitudeValue.TryGetValue<string>(out var rawMagnitude))
        {
            Assert.DoesNotContain(rawMagnitude, fact.Value, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("_", fact.Value, StringComparison.Ordinal);
        var visible = JsonSerializer.Serialize(projection);
        foreach (var privateValue in new[]
                 {
                     effectId, componentId, stackKey, targetId, woundId, definitionKey, contextId,
                     EffectMaterializationTestFixture.TransitionId, "turn_42:wound_opened"
                 })
        {
            Assert.DoesNotContain(privateValue, visible, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("\"links\"", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("\"sourceId\"", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("\"definitionKey\"", visible, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> ProjectionCases
    {
        get
        {
            yield return new object[]
            {
                "spiritual_roll_hindrance", "pressure", "\"disadvantage\"",
                "Духовная проверка", "Давление: бросок совершается с помехой."
            };
            yield return new object[]
            {
                "spiritual_roll_hindrance", "counter", "\"disadvantage\"",
                "Духовная проверка", "Контрприём: бросок совершается с помехой."
            };
            yield return new object[]
            {
                "spiritual_roll_hindrance", "force_incarnation", "\"disadvantage\"",
                "Духовная проверка",
                "Принуждение к воплощению: бросок совершается с помехой."
            };
            yield return new object[]
            {
                "spiritual_roll_hindrance", "champion_coordination", "\"disadvantage\"",
                "Духовная проверка",
                "Координация чемпиона: бросок совершается с помехой."
            };
            yield return new object[]
            {
                "spiritual_roll_hindrance", "recover_spiritual_power", "\"disadvantage\"",
                "Духовная проверка",
                "Восстановление духовной силы: бросок совершается с помехой."
            };
            yield return new object[]
            {
                "spiritual_action_cost_burden", "pressure", "1",
                "Стоимость духовного действия",
                "Давление: стоимость духовного действия увеличена на 1."
            };
            yield return new object[]
            {
                "spiritual_action_cost_burden", "counter", "3",
                "Стоимость духовного действия",
                "Контрприём: стоимость духовного действия увеличена на 3."
            };
            yield return new object[]
            {
                "spiritual_position_burden", "maneuver", "1",
                "Позиция в духовном конфликте", "Манёвр: позиция ухудшена на 1 ступень."
            };
            yield return new object[]
            {
                "spiritual_control_burden", "binding", "1",
                "Духовный контроль", "Оковы: контроль ухудшен на 1 ступень."
            };
            yield return new object[]
            {
                "spiritual_strain_burden", "break_binding", "1",
                "Духовное напряжение",
                "Разрыв оков: напряжение стороны увеличено на 1 ступень."
            };
            yield return new object[]
            {
                "spiritual_tempo_burden", "force_binding", "\"deny_one_gain\"",
                "Темп духовного конфликта",
                "Принуждение оковами: получение одного преимущества темпа запрещено."
            };
            yield return new object[]
            {
                "spiritual_counter_burden", "incarnation_resistance", "\"reduce_one_step\"",
                "Результат контрдействия",
                "Сопротивление воплощению: результат контрдействия снижен на 1 ступень."
            };
            yield return new object[]
            {
                "spiritual_art_restriction", "counter", "\"restrict\"",
                "Доступность духовного искусства",
                "Контрприём: духовное искусство ограничено."
            };
            yield return new object[]
            {
                "spiritual_art_restriction", "guard", "\"forbid\"",
                "Доступность духовного искусства",
                "Защита: духовное искусство запрещено."
            };
        }
    }

    [Fact]
    public void ProjectionCases_CoverEveryRegisteredSpiritualOperation()
    {
        Assert.Equal(
            Operations.OrderBy(static operation => operation, StringComparer.Ordinal),
            ProjectionCases
                .Select(static row => Assert.IsType<string>(row[1]))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static operation => operation, StringComparer.Ordinal));
    }

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

    private static object[] PayloadCase(
        string profile,
        string member,
        string mutation,
        string? valueJson) =>
        new object[] { profile, member, mutation, valueJson! };

    private static IEnumerable<string> InvalidMagnitudeJson(string profile) => profile switch
    {
        "spiritual_action_cost_burden" =>
            new[] { "\"1\"", "true", "1.5", "1.0", "1e0", "{}", "[]", "0", "4" },
        "spiritual_position_burden" =>
            new[] { "\"1\"", "true", "1.5", "1.0", "1e0", "{}", "[]", "0", "3" },
        "spiritual_control_burden" or "spiritual_strain_burden" =>
            new[] { "\"1\"", "true", "1.5", "1.0", "1e0", "{}", "[]", "0", "2" },
        "spiritual_roll_hindrance" =>
            new[] { "\"advantage\"", "\"Disadvantage\"", "1", "true", "{}", "[]" },
        "spiritual_tempo_burden" =>
            new[] { "\"deny_two_gains\"", "\"Deny_one_gain\"", "1", "true", "{}", "[]" },
        "spiritual_counter_burden" =>
            new[] { "\"deny_one_gain\"", "\"Reduce_one_step\"", "1", "true", "{}", "[]" },
        "spiritual_art_restriction" =>
            new[]
            {
                "\"disadvantage\"", "\"Restrict\"", "\"Forbid\"", "1", "true", "{}", "[]"
            },
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
    };

    private static string DefaultMagnitudeJson(string profile) => profile switch
    {
        "spiritual_roll_hindrance" => "\"disadvantage\"",
        "spiritual_action_cost_burden" => "3",
        "spiritual_position_burden" => "2",
        "spiritual_control_burden" or "spiritual_strain_burden" => "1",
        "spiritual_tempo_burden" => "\"deny_one_gain\"",
        "spiritual_counter_burden" => "\"reduce_one_step\"",
        "spiritual_art_restriction" => "\"forbid\"",
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
    };

    private static string AxisFor(string profile) => profile switch
    {
        "spiritual_roll_hindrance" => "rollMode",
        "spiritual_action_cost_burden" => "actionCostAudit",
        "spiritual_position_burden" => "conflictPosition",
        "spiritual_control_burden" => "controlState",
        "spiritual_strain_burden" => "sideStrain",
        "spiritual_tempo_burden" => "tempoAdvantage",
        "spiritual_counter_burden" => "counterPayoff",
        "spiritual_art_restriction" => "artAvailability",
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
    };

    private static string ToggleFirstCharacterCase(string value) =>
        char.ToUpperInvariant(value[0]) + value[1..];

    private static JsonNode ParseRequiredJson(string json) =>
        JsonNode.Parse(json) ?? throw new ArgumentException("Expected non-null JSON test value.", nameof(json));

    private static JsonObject EffectPayload(JsonObject effect) =>
        effect["components"]![0]!["payload"]!.AsObject();

    private static JsonObject DefinitionPayload(JsonObject definition) =>
        definition["components"]![0]!["payload"]!.AsObject();

    private static void MutatePayload(
        JsonObject payload,
        string member,
        string mutation,
        string? valueJson)
    {
        switch (mutation)
        {
            case "missing":
                payload.Remove(member);
                break;
            case "case":
                payload.Remove(char.ToLowerInvariant(member[0]) + member[1..]);
                payload[member] = valueJson == null ? null : JsonNode.Parse(valueJson);
                break;
            case "extra":
            case "value":
                payload[member] = valueJson == null ? null : JsonNode.Parse(valueJson);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
    }

    private static void AssertAcceptedByBothBoundaries(
        string profile,
        string operation,
        JsonNode magnitude)
    {
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(
            profile,
            operation: operation,
            magnitude: magnitude);
        Assert.Empty(ValidateEffect(effect));

        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            profile,
            operation: operation,
            magnitude: magnitude);
        Assert.Empty(ValidateDefinitions("chaos_sea", definition));
    }

    private static void AssertRejectedByBothBoundaries(
        JsonObject effect,
        JsonObject definition,
        string member)
    {
        var effectIssues = ValidateEffect(effect);
        Assert.NotEmpty(effectIssues);
        AssertIssue(
            effectIssues,
            $"effect.components[0].payload.{member}",
            "effect_materialization_invalid_component");

        var definitionIssues = ValidateDefinitions("chaos_sea", definition);
        Assert.NotEmpty(definitionIssues);
        AssertIssue(
            definitionIssues,
            $"definitions[0].components[0].payload.{member}",
            "effect_source_definition_invalid_components");
    }

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
