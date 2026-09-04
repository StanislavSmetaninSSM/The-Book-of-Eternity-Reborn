using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal enum EffectComponentResolutionMode
{
    Deterministic,
    BoundedReceipt,
    Declared
}

internal sealed record EffectComponentProfileDescriptor(
    string Profile,
    EffectComponentResolutionMode ResolutionMode,
    IReadOnlySet<string> LegalMergeReducers,
    string ProjectionDescriptor);

internal enum SpiritualWoundMagnitudeKind
{
    Integer,
    String
}

internal enum SpiritualWoundProjectionKind
{
    RollHindrance,
    ActionCostBurden,
    PositionBurden,
    ControlBurden,
    StrainBurden,
    TempoBurden,
    CounterBurden,
    ArtRestriction
}

internal sealed record SpiritualWoundOperationDescriptor(
    string Operation,
    string PlayerLabel);

internal sealed record SpiritualWoundProfileDescriptor(
    string Profile,
    string Axis,
    ImmutableHashSet<string> LegalOperations,
    SpiritualWoundMagnitudeKind MagnitudeKind,
    ImmutableArray<int> IntegerMagnitudes,
    ImmutableHashSet<string> StringMagnitudes,
    SpiritualWoundProjectionKind ProjectionKind,
    string PlayerLabel)
{
    internal bool IsMagnitudeValid(JsonElement magnitude) => MagnitudeKind switch
    {
        SpiritualWoundMagnitudeKind.Integer =>
            TryReadExactInteger(magnitude, out var value) &&
            IntegerMagnitudes.Contains(value),
        SpiritualWoundMagnitudeKind.String =>
            magnitude.ValueKind == JsonValueKind.String &&
            magnitude.GetString() is string value &&
            StringMagnitudes.Contains(value),
        _ => false
    };

    internal bool TryReadMagnitudeInteger(JsonElement magnitude, out int value)
    {
        value = 0;
        return MagnitudeKind == SpiritualWoundMagnitudeKind.Integer &&
               TryReadExactInteger(magnitude, out value) &&
               IntegerMagnitudes.Contains(value);
    }

    internal string ExpectedMagnitude => MagnitudeKind switch
    {
        SpiritualWoundMagnitudeKind.Integer => string.Join(
            " | ",
            IntegerMagnitudes.Select(static value =>
                value.ToString(CultureInfo.InvariantCulture))),
        SpiritualWoundMagnitudeKind.String => string.Join(
            " | ",
            StringMagnitudes.OrderBy(static value => value, StringComparer.Ordinal)),
        _ => "registered spiritual wound magnitude"
    };

    private static bool TryReadExactInteger(JsonElement value, out int result)
    {
        result = 0;
        return value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out result) &&
               string.Equals(
                   value.GetRawText(),
                   result.ToString(CultureInfo.InvariantCulture),
                   StringComparison.Ordinal);
    }
}

internal static class SpiritualWoundEffectProfileCatalog
{
    private static readonly ImmutableDictionary<string, SpiritualWoundOperationDescriptor>
        OperationDescriptors = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pressure"] = "Давление",
            ["counter"] = "Контрприём",
            ["guard"] = "Защита",
            ["maneuver"] = "Манёвр",
            ["binding"] = "Оковы",
            ["break_binding"] = "Разрыв оков",
            ["force_binding"] = "Принуждение оковами",
            ["force_incarnation"] = "Принуждение к воплощению",
            ["incarnation_resistance"] = "Сопротивление воплощению",
            ["champion_coordination"] = "Координация чемпиона",
            ["recover_spiritual_power"] = "Восстановление духовной силы"
        }.ToImmutableDictionary(
            static pair => pair.Key,
            static pair => new SpiritualWoundOperationDescriptor(pair.Key, pair.Value),
            StringComparer.Ordinal);

    private static readonly ImmutableHashSet<string> OperationSet =
        OperationDescriptors.Keys.ToImmutableHashSet(StringComparer.Ordinal);

    private static readonly ImmutableHashSet<string> ArtOperationSet =
        OperationSet.Remove("force_incarnation");

    private static readonly ImmutableDictionary<string, SpiritualWoundProfileDescriptor>
        ProfileDescriptors = new[]
        {
            StringProfile(
                "spiritual_roll_hindrance",
                "rollMode",
                OperationSet,
                SpiritualWoundProjectionKind.RollHindrance,
                "Духовная проверка",
                "disadvantage"),
            IntegerProfile(
                "spiritual_action_cost_burden",
                "actionCostAudit",
                OperationSet,
                SpiritualWoundProjectionKind.ActionCostBurden,
                "Стоимость духовного действия",
                1, 2, 3),
            IntegerProfile(
                "spiritual_position_burden",
                "conflictPosition",
                OperationSet,
                SpiritualWoundProjectionKind.PositionBurden,
                "Позиция в духовном конфликте",
                1, 2),
            IntegerProfile(
                "spiritual_control_burden",
                "controlState",
                OperationSet,
                SpiritualWoundProjectionKind.ControlBurden,
                "Духовный контроль",
                1),
            IntegerProfile(
                "spiritual_strain_burden",
                "sideStrain",
                OperationSet,
                SpiritualWoundProjectionKind.StrainBurden,
                "Духовное напряжение",
                1),
            StringProfile(
                "spiritual_tempo_burden",
                "tempoAdvantage",
                OperationSet,
                SpiritualWoundProjectionKind.TempoBurden,
                "Темп духовного конфликта",
                "deny_one_gain"),
            StringProfile(
                "spiritual_counter_burden",
                "counterPayoff",
                OperationSet,
                SpiritualWoundProjectionKind.CounterBurden,
                "Результат контрдействия",
                "reduce_one_step"),
            StringProfile(
                "spiritual_art_restriction",
                "artAvailability",
                ArtOperationSet,
                SpiritualWoundProjectionKind.ArtRestriction,
                "Доступность духовного искусства",
                "restrict", "forbid")
        }.ToImmutableDictionary(
            static descriptor => descriptor.Profile,
            StringComparer.Ordinal);

    private static readonly ImmutableHashSet<string> ProfileSet =
        ProfileDescriptors.Keys.ToImmutableHashSet(StringComparer.Ordinal);

    internal static IReadOnlyDictionary<string, SpiritualWoundProfileDescriptor> Profiles =>
        ProfileDescriptors;

    internal static IReadOnlySet<string> RegisteredProfiles => ProfileSet;

    internal static IReadOnlySet<string> Operations => OperationSet;

    internal static IReadOnlySet<string> ArtOperations => ArtOperationSet;

    internal static bool TryGetProfile(
        string profile,
        out SpiritualWoundProfileDescriptor descriptor) =>
        ProfileDescriptors.TryGetValue(profile, out descriptor!);

    internal static bool TryGetOperation(
        string operation,
        out SpiritualWoundOperationDescriptor descriptor) =>
        OperationDescriptors.TryGetValue(operation, out descriptor!);

    private static SpiritualWoundProfileDescriptor IntegerProfile(
        string profile,
        string axis,
        ImmutableHashSet<string> operations,
        SpiritualWoundProjectionKind projectionKind,
        string playerLabel,
        params int[] magnitudes) =>
        new(
            profile,
            axis,
            operations,
            SpiritualWoundMagnitudeKind.Integer,
            magnitudes.ToImmutableArray(),
            ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal),
            projectionKind,
            playerLabel);

    private static SpiritualWoundProfileDescriptor StringProfile(
        string profile,
        string axis,
        ImmutableHashSet<string> operations,
        SpiritualWoundProjectionKind projectionKind,
        string playerLabel,
        params string[] magnitudes) =>
        new(
            profile,
            axis,
            operations,
            SpiritualWoundMagnitudeKind.String,
            ImmutableArray<int>.Empty,
            magnitudes.ToImmutableHashSet(StringComparer.Ordinal),
            projectionKind,
            playerLabel);
}

internal sealed record EffectPeriodicResourceComponent(
    string ComponentId,
    string Profile,
    int Priority,
    string ResourceKey,
    decimal Amount,
    ResourceOperation Operation,
    string BoundPolicy);

internal sealed record EffectPeriodicResourceComponentResult(
    EffectPeriodicResourceComponent? Component,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Component != null && Issues.Count == 0;
}

internal static class EffectComponentProfiles
{
    private static readonly HashSet<string> ComponentFields = new(StringComparer.Ordinal)
    {
        "componentId", "profile", "priority", "payload"
    };

    private static readonly HashSet<string> Characteristics = new(
        BookOfEternityClient.Configuration.Characteristics.All,
        StringComparer.Ordinal);

    private static readonly HashSet<string> ModifierOperations = new(StringComparer.Ordinal)
    {
        "flat", "percent"
    };

    private static readonly HashSet<string> RollOperations = new(StringComparer.Ordinal)
    {
        "attack_roll", "defense_roll", "skill_check", "saving_throw", "damage_roll",
        "initiative_roll"
    };

    private static readonly HashSet<string> Contributions = new(StringComparer.Ordinal)
    {
        "advantage", "disadvantage"
    };

    private static readonly HashSet<string> ResistanceKinds = new(StringComparer.Ordinal)
    {
        "physical", "magical", "fire", "frost", "poison", "bleeding", "radiant",
        "necrotic", "spiritual"
    };

    private static readonly HashSet<string> DamageTypes = new(StringComparer.Ordinal)
    {
        "physical", "magical", "fire", "frost", "poison", "bleeding", "radiant",
        "necrotic", "spiritual"
    };

    private static readonly HashSet<string> FloorPolicies = new(StringComparer.Ordinal)
    {
        "registered_resource_floor", "may_reach_zero", "cannot_reduce_below_one"
    };

    private static readonly HashSet<string> CapPolicies = new(StringComparer.Ordinal)
    {
        "registered_resource_cap", "may_exceed_soft_cap", "cannot_exceed_maximum"
    };

    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal)
    {
        "movement", "attack", "defend", "use_item", "cast", "interact", "escape"
    };

    private static readonly HashSet<string> ActionOperations = new(StringComparer.Ordinal)
    {
        "grant", "restrict", "forbid", "cost_modifier"
    };

    private static readonly IReadOnlySet<string> EventTypes =
        EffectEventTypeCatalog.Registered;

    private static readonly IReadOnlySet<string> ReactionResultKinds =
        EffectReactionResultCatalog.RegisteredKinds;

    private static readonly HashSet<string> Dependencies = new(StringComparer.Ordinal)
    {
        "before_current_event", "after_current_event", "after_component"
    };

    private static readonly HashSet<string> WoundSymptoms = new(StringComparer.Ordinal)
    {
        "bleeding", "pain", "weakness", "restricted_movement", "infection_risk"
    };

    private static readonly HashSet<string> WoundConsequences = new(StringComparer.Ordinal)
    {
        "periodic_damage", "characteristic_modifier", "roll_modifier", "action_control"
    };

    private static readonly HashSet<string> AfterlifeConditionKinds = new(
        AfterlifeSpiritualConflictState.CombatConditionKinds,
        StringComparer.Ordinal);

    private static readonly HashSet<string> AfterlifeSides = new(
        AfterlifeSpiritualConflictState.CombatConditionTargetSides,
        StringComparer.Ordinal);

    private static readonly HashSet<string> AfterlifeOperations = new(
        AfterlifeSpiritualConflictState.OperationTypes,
        StringComparer.Ordinal);

    private static readonly HashSet<string> AfterlifeAxes = new(
        AfterlifeSpiritualConflictState.CombatConditionMechanicalAxes,
        StringComparer.Ordinal);

    private static readonly ImmutableDictionary<string, EffectComponentProfileDescriptor> Descriptors =
        new Dictionary<string, EffectComponentProfileDescriptor>(StringComparer.Ordinal)
        {
            ["characteristic_modifier"] = Descriptor(
                "characteristic_modifier", "characteristic modifier", "sum", "minimum", "maximum"),
            ["roll_modifier"] = Descriptor(
                "roll_modifier", "roll contribution", "minimum", "maximum"),
            ["resistance_modifier"] = Descriptor(
                "resistance_modifier", "resistance modifier", "sum", "minimum", "maximum"),
            ["periodic_damage"] = Descriptor(
                "periodic_damage", "periodic damage", "sum", "minimum", "maximum"),
            ["periodic_restore"] = Descriptor(
                "periodic_restore", "periodic restoration", "sum", "minimum", "maximum"),
            ["action_control"] = Descriptor(
                "action_control", "action control", "minimum", "maximum"),
            ["event_reaction"] = new(
                "event_reaction",
                EffectComponentResolutionMode.Declared,
                ImmutableHashSet.Create(StringComparer.Ordinal, "profile_specific"),
                "source-declared deterministic or bounded event reaction"),
            ["wound_consequence"] = Descriptor(
                "wound_consequence", "wound consequence", "profile_specific"),
            ["afterlife_combat_condition"] = Descriptor(
                "afterlife_combat_condition", "afterlife combat condition", "profile_specific")
        }
        .Concat(SpiritualWoundEffectProfileCatalog.Profiles.Values.Select(static profile =>
            new KeyValuePair<string, EffectComponentProfileDescriptor>(
                profile.Profile,
                Descriptor(
                    profile.Profile,
                    profile.PlayerLabel,
                    "profile_specific"))))
        .ToImmutableDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);

    private static readonly ImmutableArray<string> RegisteredProfileKeys =
        Descriptors.Keys.OrderBy(static profile => profile, StringComparer.Ordinal).ToImmutableArray();

    internal static IReadOnlyCollection<string> RegisteredProfiles => RegisteredProfileKeys;

    internal static bool TryGetDescriptor(
        string profile,
        out EffectComponentProfileDescriptor descriptor) =>
        Descriptors.TryGetValue(profile, out descriptor!);

    internal static EffectPeriodicResourceComponentResult ParsePeriodicResourceComponent(
        JsonElement component,
        string path)
    {
        var issues = new List<ValidationIssue>();
        ValidateComponent(component, path, issues);
        if (issues.Count != 0)
            return new EffectPeriodicResourceComponentResult(null, issues.ToArray());

        var profile = component.GetProperty("profile").GetString()!;
        if (profile is not ("periodic_damage" or "periodic_restore"))
        {
            Add(
                issues,
                path + ".profile",
                "effect_resource_profile_unsupported",
                "periodic_damage or periodic_restore",
                profile);
            return new EffectPeriodicResourceComponentResult(null, issues.ToArray());
        }

        var payload = component.GetProperty("payload");
        if (!ResourceMaterializationContract.TryReadExactDecimal(
                payload.GetProperty("amount"),
                out var amount) ||
            amount <= 0m)
        {
            Add(
                issues,
                path + ".payload.amount",
                "effect_resource_amount_unrepresentable",
                "positive exactly representable decimal amount",
                payload.GetProperty("amount").GetRawText());
            return new EffectPeriodicResourceComponentResult(null, issues.ToArray());
        }

        var policyField = string.Equals(
            profile,
            "periodic_damage",
            StringComparison.Ordinal)
            ? "floorPolicy"
            : "capPolicy";
        return new EffectPeriodicResourceComponentResult(
            new EffectPeriodicResourceComponent(
                component.GetProperty("componentId").GetString()!,
                profile,
                component.GetProperty("priority").GetInt32(),
                payload.GetProperty("resource").GetString()!,
                amount,
                string.Equals(profile, "periodic_damage", StringComparison.Ordinal)
                    ? ResourceOperation.Damage
                    : ResourceOperation.Restore,
                payload.GetProperty(policyField).GetString()!),
            Array.Empty<ValidationIssue>());
    }

    internal static void ValidateComponent(
        JsonElement component,
        string path,
        List<ValidationIssue> issues)
    {
        if (component.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path, "effect_materialization_invalid_component", "component object", component.ValueKind.ToString());
            return;
        }

        ValidateClosedObject(component, path, ComponentFields, issues);
        RequireExactIdentifier(component, path, "componentId", issues);
        RequireBoundedInt(component, path, "priority", -10_000, 10_000, issues);

        var profile = RequireExactIdentifier(component, path, "profile", issues);
        if (profile == null)
            return;
        if (!Descriptors.ContainsKey(profile))
        {
            Add(
                issues,
                path + ".profile",
                "effect_materialization_unknown_profile",
                string.Join(" | ", Descriptors.Keys.OrderBy(static value => value, StringComparer.Ordinal)),
                profile);
            return;
        }

        if (!component.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path + ".payload", "effect_materialization_invalid_component", "closed profile payload object", Describe(component, "payload"));
            return;
        }

        switch (profile)
        {
            case "characteristic_modifier":
                ValidateCharacteristicModifier(payload, path + ".payload", issues);
                break;
            case "roll_modifier":
                ValidateRollModifier(payload, path + ".payload", issues);
                break;
            case "resistance_modifier":
                ValidateResistanceModifier(payload, path + ".payload", issues);
                break;
            case "periodic_damage":
                ValidatePeriodicDamage(payload, path + ".payload", issues);
                break;
            case "periodic_restore":
                ValidatePeriodicRestore(payload, path + ".payload", issues);
                break;
            case "action_control":
                ValidateActionControl(payload, path + ".payload", issues);
                break;
            case "event_reaction":
                ValidateEventReaction(payload, path + ".payload", issues);
                break;
            case "wound_consequence":
                ValidateWoundConsequence(payload, path + ".payload", issues);
                break;
            case "afterlife_combat_condition":
                ValidateAfterlifeCombatCondition(payload, path + ".payload", issues);
                break;
            default:
                if (SpiritualWoundEffectProfileCatalog.TryGetProfile(
                        profile,
                        out var spiritualProfile))
                {
                    ValidateSpiritualWound(
                        payload,
                        path + ".payload",
                        spiritualProfile,
                        issues);
                }
                break;
        }
    }

    private static EffectComponentProfileDescriptor Descriptor(
        string profile,
        string projection,
        params string[] reducers) =>
        new(
            profile,
            EffectComponentResolutionMode.Deterministic,
            reducers.ToImmutableHashSet(StringComparer.Ordinal),
            projection);

    private static void ValidateCharacteristicModifier(
        JsonElement payload,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateClosedObject(payload, path, Set("characteristic", "operation", "value", "cap"), issues);
        RequireClosedString(payload, path, "characteristic", Characteristics, issues);
        RequireClosedString(payload, path, "operation", ModifierOperations, issues);
        RequireFiniteNonZero(payload, path, "value", issues);
        ValidateOptionalCap(payload, path, issues);
    }

    private static void ValidateRollModifier(
        JsonElement payload,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateClosedObject(
            payload,
            path,
            Set("operations", "contribution", "scope"),
            issues);
        var operations = TryReadClosedStringArray(
            payload,
            path,
            "operations",
            RollOperations,
            issues);
        RequireClosedString(payload, path, "contribution", Contributions, issues);

        if (!payload.TryGetProperty("scope", out var scope) ||
            scope.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path + ".scope", "effect_materialization_invalid_component",
                "closed roll scope object", Describe(payload, "scope"));
            return;
        }

        var scopePath = path + ".scope";
        var kind = RequireClosedString(
            scope,
            scopePath,
            "kind",
            Set("all", "skill"),
            issues);
        switch (kind)
        {
            case "all":
                ValidateClosedObject(scope, scopePath, Set("kind"), issues);
                break;
            case "skill":
                ValidateClosedObject(scope, scopePath, Set("kind", "skillId"), issues);
                RequireExactIdentifier(scope, scopePath, "skillId", issues);
                if (operations is not null && operations is not ["skill_check"])
                {
                    Add(issues, path + ".operations",
                        "effect_materialization_invalid_component",
                        "exactly [\"skill_check\"] for scope.kind=skill",
                        Describe(payload, "operations"));
                }
                break;
        }
    }

    private static void ValidateResistanceModifier(
        JsonElement payload,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateClosedObject(payload, path, Set("resistance", "operation", "value", "cap"), issues);
        RequireClosedString(payload, path, "resistance", ResistanceKinds, issues);
        RequireClosedString(payload, path, "operation", ModifierOperations, issues);
        RequireFiniteNonZero(payload, path, "value", issues);
        ValidateOptionalCap(payload, path, issues);
    }

    private static void ValidatePeriodicDamage(
        JsonElement payload,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateClosedObject(payload, path, Set("resource", "amount", "damageType", "floorPolicy"), issues);
        RequireExactIdentifier(payload, path, "resource", issues);
        RequireFinitePositive(payload, path, "amount", issues);
        RequireClosedString(payload, path, "damageType", DamageTypes, issues);
        RequireClosedString(payload, path, "floorPolicy", FloorPolicies, issues);
    }

    private static void ValidatePeriodicRestore(
        JsonElement payload,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateClosedObject(payload, path, Set("resource", "amount", "capPolicy"), issues);
        RequireExactIdentifier(payload, path, "resource", issues);
        RequireFinitePositive(payload, path, "amount", issues);
        RequireClosedString(payload, path, "capPolicy", CapPolicies, issues);
    }

    private static void ValidateActionControl(
        JsonElement payload,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateClosedObject(payload, path, Set("action", "operation", "modifier"), issues);
        RequireClosedString(payload, path, "action", Actions, issues);
        var operation = RequireClosedString(payload, path, "operation", ActionOperations, issues);
        if (string.Equals(operation, "cost_modifier", StringComparison.Ordinal))
            RequireFiniteNonZero(payload, path, "modifier", issues);
        else if (payload.TryGetProperty("modifier", out var modifier) && modifier.ValueKind != JsonValueKind.Null)
            ValidateFiniteBoundedNumber(modifier, path + ".modifier", -1_000, 1_000, issues);
    }

    private static void ValidateEventReaction(
        JsonElement payload,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateClosedObject(
            payload,
            path,
            Set(
                "eventType",
                "resultKind",
                "definitionKey",
                "componentId",
                "parameters",
                "originalOutcome",
                "resolvedOutcome",
                "dependency",
                "afterComponentId",
                "maxExpansion"),
            issues);
        var eventType = RequireClosedString(
            payload,
            path,
            "eventType",
            EventTypes,
            issues);
        var resultKind = RequireClosedString(payload, path, "resultKind", ReactionResultKinds, issues);
        var dependency = RequireClosedString(payload, path, "dependency", Dependencies, issues);
        RequireBoundedInt(payload, path, "maxExpansion", 1, 64, issues);
        _ = EffectReactionResultCatalog.TryResolve(resultKind, out var descriptor);
        if (descriptor?.Behavior == EffectReactionResultBehavior.ApplyDefinition)
        {
            RequireExactIdentifier(payload, path, "definitionKey", issues);
            RequireObject(payload, path, "parameters", issues);
        }
        else
        {
            ForbidIfPresent(payload, path, "definitionKey", resultKind, issues);
            ForbidIfPresent(payload, path, "parameters", resultKind, issues);
        }
        if (descriptor?.Behavior == EffectReactionResultBehavior.PeriodicComponent)
            RequireExactIdentifier(payload, path, "componentId", issues);
        else
            ForbidIfPresent(payload, path, "componentId", resultKind, issues);
        if (descriptor?.Behavior == EffectReactionResultBehavior.EventOutcome)
        {
            var originalOutcome = RequireExactIdentifier(
                payload,
                path,
                "originalOutcome",
                issues);
            var resolvedOutcome = RequireExactIdentifier(
                payload,
                path,
                "resolvedOutcome",
                issues);
            if (originalOutcome != null &&
                resolvedOutcome != null &&
                eventType != null &&
                !EffectEventOutcomeCatalog.Contains(
                    eventType,
                    originalOutcome,
                    resolvedOutcome))
            {
                Add(
                    issues,
                    path,
                    "effect_materialization_invalid_event_outcome",
                    "one registered source-owned event outcome transition",
                    $"{originalOutcome}->{resolvedOutcome}");
            }
        }
        else
        {
            ForbidIfPresent(payload, path, "originalOutcome", resultKind, issues);
            ForbidIfPresent(payload, path, "resolvedOutcome", resultKind, issues);
        }
        if (string.Equals(dependency, "after_component", StringComparison.Ordinal))
            RequireExactIdentifier(payload, path, "afterComponentId", issues);
        else
            ForbidIfPresent(payload, path, "afterComponentId", dependency, issues);
    }

    private static void ForbidIfPresent(
        JsonElement root,
        string path,
        string field,
        string? discriminator,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value))
        {
            Add(
                issues,
                path + "." + field,
                "effect_materialization_invalid_component",
                $"field absent for '{discriminator ?? "unknown"}' reaction semantics",
                value.GetRawText());
        }
    }

    private static void ValidateWoundConsequence(
        JsonElement payload,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateClosedObject(payload, path, Set("woundId", "symptom", "consequence"), issues);
        RequireExactIdentifier(payload, path, "woundId", issues);
        RequireClosedString(payload, path, "symptom", WoundSymptoms, issues);
        RequireClosedString(payload, path, "consequence", WoundConsequences, issues);
    }

    private static void ValidateAfterlifeCombatCondition(
        JsonElement payload,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateClosedObject(
            payload,
            path,
            Set("conditionKind", "targetSide", "actorId", "operations", "axes", "counterplay", "payoff"),
            issues);
        RequireClosedString(payload, path, "conditionKind", AfterlifeConditionKinds, issues);
        RequireClosedString(payload, path, "targetSide", AfterlifeSides, issues);
        RequireExactIdentifier(payload, path, "actorId", issues);
        RequireClosedStringArray(payload, path, "operations", AfterlifeOperations, issues);
        RequireClosedStringArray(payload, path, "axes", AfterlifeAxes, issues);
        RequireExactStringArray(payload, path, "counterplay", issues);
        RequireExactIdentifier(payload, path, "payoff", issues);
    }

    private static void ValidateSpiritualWound(
        JsonElement payload,
        string path,
        SpiritualWoundProfileDescriptor descriptor,
        List<ValidationIssue> issues)
    {
        ValidateClosedObject(payload, path, Set("operation", "axis", "magnitude"), issues);
        RequireClosedString(
            payload,
            path,
            "operation",
            descriptor.LegalOperations,
            issues);
        RequireClosedString(
            payload,
            path,
            "axis",
            new HashSet<string>(StringComparer.Ordinal) { descriptor.Axis },
            issues);

        if (!payload.TryGetProperty("magnitude", out var magnitude) ||
            !descriptor.IsMagnitudeValid(magnitude))
        {
            Add(
                issues,
                path + ".magnitude",
                "effect_materialization_invalid_component",
                descriptor.ExpectedMagnitude,
                Describe(payload, "magnitude"));
        }
    }

    private static void ValidateOptionalCap(
        JsonElement payload,
        string path,
        List<ValidationIssue> issues)
    {
        if (!payload.TryGetProperty("cap", out var cap) || cap.ValueKind == JsonValueKind.Null)
            return;
        if (cap.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path + ".cap", "effect_materialization_invalid_component", "closed numeric cap object or null", cap.GetRawText());
            return;
        }

        ValidateClosedObject(cap, path + ".cap", Set("minimum", "maximum"), issues);
        var hasMinimum = TryReadFinite(cap, "minimum", out var minimum);
        var hasMaximum = TryReadFinite(cap, "maximum", out var maximum);
        if (!hasMinimum)
            Add(issues, path + ".cap.minimum", "effect_materialization_invalid_component", "finite number", Describe(cap, "minimum"));
        if (!hasMaximum)
            Add(issues, path + ".cap.maximum", "effect_materialization_invalid_component", "finite number", Describe(cap, "maximum"));
        if (hasMinimum && hasMaximum && minimum > maximum)
            Add(issues, path + ".cap", "effect_materialization_invalid_component", "minimum <= maximum", cap.GetRawText());
    }

    private static string? RequireClosedString(
        JsonElement root,
        string path,
        string field,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues)
    {
        var value = RequireExactIdentifier(root, path, field, issues);
        if (value != null && !allowed.Contains(value))
        {
            Add(
                issues,
                path + "." + field,
                "effect_materialization_invalid_component",
                string.Join(" | ", allowed.OrderBy(static item => item, StringComparer.Ordinal)),
                value);
            return null;
        }
        return value;
    }

    private static string? RequireExactIdentifier(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            value.GetString() is string text &&
            text.Length > 0 &&
            string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return text;
        }

        Add(
            issues,
            path + "." + field,
            "effect_materialization_invalid_component",
            "exact non-empty string without surrounding whitespace",
            Describe(root, field));
        return null;
    }

    private static void RequireClosedStringArray(
        JsonElement root,
        string path,
        string field,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            Add(issues, path + "." + field, "effect_materialization_invalid_component", "non-empty closed string array", Describe(root, field));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemPath = $"{path}.{field}[{index++}]";
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not string text ||
                text.Length == 0 || !string.Equals(text, text.Trim(), StringComparison.Ordinal) ||
                !allowed.Contains(text) || !seen.Add(text))
            {
                Add(issues, itemPath, "effect_materialization_invalid_component", "one unique registered value", item.GetRawText());
            }
        }
    }

    private static string[]? TryReadClosedStringArray(
        JsonElement root,
        string path,
        string field,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            Add(issues, path + "." + field, "effect_materialization_invalid_component", "non-empty closed string array", Describe(root, field));
            return null;
        }

        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var hasInvalidItem = false;
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemPath = $"{path}.{field}[{index++}]";
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not string text ||
                text.Length == 0 || !string.Equals(text, text.Trim(), StringComparison.Ordinal) ||
                !allowed.Contains(text) || !seen.Add(text))
            {
                Add(issues, itemPath, "effect_materialization_invalid_component", "one unique registered value", item.GetRawText());
                hasInvalidItem = true;
                continue;
            }

            values.Add(text);
        }

        return hasInvalidItem ? null : values.ToArray();
    }

    private static void RequireExactStringArray(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            Add(issues, path + "." + field, "effect_materialization_invalid_component", "non-empty exact string array", Describe(root, field));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemPath = $"{path}.{field}[{index++}]";
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not string text ||
                text.Length == 0 || !string.Equals(text, text.Trim(), StringComparison.Ordinal) || !seen.Add(text))
            {
                Add(issues, itemPath, "effect_materialization_invalid_component", "one unique exact non-empty string", item.GetRawText());
            }
        }
    }

    private static void RequireFinitePositive(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!TryReadFinite(root, field, out var value) || value <= 0)
            Add(issues, path + "." + field, "effect_materialization_invalid_component", "finite number > 0", Describe(root, field));
    }

    private static void RequireFiniteNonZero(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!TryReadFinite(root, field, out var value) || value == 0 || Math.Abs(value) > 1_000_000)
            Add(issues, path + "." + field, "effect_materialization_invalid_component", "bounded finite non-zero number", Describe(root, field));
    }

    private static void RequireBoundedInt(
        JsonElement root,
        string path,
        string field,
        int minimum,
        int maximum,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var number) ||
            number < minimum || number > maximum)
        {
            Add(issues, path + "." + field, "effect_materialization_invalid_component", $"integer from {minimum} through {maximum}", Describe(root, field));
        }
    }

    private static void RequireObject(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path + "." + field,
                "effect_materialization_invalid_component",
                "closed object",
                Describe(root, field));
        }
    }

    private static void ValidateFiniteBoundedNumber(
        JsonElement value,
        string path,
        double minimum,
        double maximum,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out var number) ||
            !double.IsFinite(number) ||
            number < minimum ||
            number > maximum)
        {
            Add(issues, path, "effect_materialization_invalid_component", $"finite number from {minimum} through {maximum}", value.GetRawText());
        }
    }

    private static bool TryReadFinite(JsonElement root, string field, out double number)
    {
        number = 0;
        return root.TryGetProperty(field, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetDouble(out number) &&
               double.IsFinite(number);
    }

    private static void ValidateClosedObject(
        JsonElement value,
        string path,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                Add(
                    issues,
                    path + "." + property.Name,
                    "effect_materialization_invalid_component",
                    "registered profile payload field",
                    property.Name);
            }
        }
    }

    private static HashSet<string> Set(params string[] values) =>
        new(values, StringComparer.Ordinal);

    private static string Describe(JsonElement root, string field) =>
        root.TryGetProperty(field, out var value) ? value.GetRawText() : "missing";

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Active effect component violates its registered mechanical profile.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Use one registered component profile and submit its complete closed mechanical payload."));
}
