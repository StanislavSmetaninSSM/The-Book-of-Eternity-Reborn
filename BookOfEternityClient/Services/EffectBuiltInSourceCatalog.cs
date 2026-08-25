using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class EffectBuiltInSourceCatalog
{
    internal const string FateShieldSourceKind = "fate_card";
    internal const string FateShieldSourceId = "builtin_ink_feather_fate_shield";
    internal const string FateShieldDefinitionKey = "fate-shield-next-critical-failure";
    internal const string FateShieldApplicationAuthority = "ink_feather_action:FATE_SHIELD";

    private const string FateShieldActionMarker = "[INK_FEATHER_ACTION: FATE_SHIELD]";

    private static readonly FrozenSet<string> RegisteredApplicationAuthorities = new[]
    {
        FateShieldApplicationAuthority
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static IReadOnlyList<EffectSourceExport> CreateCanonicalExports() =>
        new[]
        {
            new EffectSourceExport(
                "mortal_world",
                FateShieldSourceKind,
                FateShieldSourceId,
                new JsonArray(CreateFateShieldDefinition()),
                Materializable: true,
                Active: true,
                SameTurn: false,
                SourceRef: null,
                SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal)
                {
                    "active",
                    "unlocked"
                },
                RequiredApplicationAuthority: FateShieldApplicationAuthority)
        };

    internal static IReadOnlySet<string> ResolveAcceptedTurnApplicationAuthorities(
        string? playerAction,
        string realm)
    {
        if (string.Equals(realm, "mortal_world", StringComparison.Ordinal) &&
            playerAction?.Contains(FateShieldActionMarker, StringComparison.Ordinal) == true)
        {
            return new HashSet<string>(StringComparer.Ordinal)
            {
                FateShieldApplicationAuthority
            };
        }

        return new HashSet<string>(StringComparer.Ordinal);
    }

    internal static bool IsRegisteredApplicationAuthority(string authority) =>
        RegisteredApplicationAuthorities.Contains(authority);

    internal static IReadOnlyList<string> FindNewFateShieldEffectIds(
        string? currentPlayerEffectsJson,
        string? previousPlayerEffectsJson)
    {
        var current = FindFateShieldEffectIds(currentPlayerEffectsJson);
        current.ExceptWith(FindFateShieldEffectIds(previousPlayerEffectsJson));
        return current.OrderBy(static effectId => effectId, StringComparer.Ordinal).ToArray();
    }

    internal static HashSet<string> FindFateShieldEffectIds(string? playerEffectsJson)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(playerEffectsJson))
            return result;

        try
        {
            if (JsonNode.Parse(playerEffectsJson) is not JsonObject root ||
                root["activeEffects"] is not JsonArray effects)
            {
                return result;
            }

            foreach (var effect in effects.OfType<JsonObject>())
            {
                if (effect["source"] is not JsonObject source ||
                    !HasExactString(source, "kind", FateShieldSourceKind) ||
                    !HasExactString(source, "sourceId", FateShieldSourceId) ||
                    !HasExactString(source, "definitionKey", FateShieldDefinitionKey) ||
                    effect["effectId"] is not JsonValue effectIdValue ||
                    !effectIdValue.TryGetValue<string>(out var effectId) ||
                    string.IsNullOrWhiteSpace(effectId) ||
                    !string.Equals(effectId, effectId.Trim(), StringComparison.Ordinal))
                {
                    continue;
                }

                result.Add(effectId);
            }
        }
        catch (JsonException)
        {
            // The canonical carrier validator reports malformed JSON separately.
        }
        catch (InvalidOperationException)
        {
            // Wrong-shaped JSON cannot provide built-in outcome evidence.
        }

        return result;
    }

    private static bool HasExactString(
        JsonObject value,
        string propertyName,
        string expected) =>
        value[propertyName] is JsonValue property &&
        property.TryGetValue<string>(out var actual) &&
        string.Equals(actual, expected, StringComparison.Ordinal);

    private static JsonObject CreateFateShieldDefinition() => new()
    {
        ["schemaVersion"] = 1,
        ["definitionKey"] = FateShieldDefinitionKey,
        ["display"] = new JsonObject
        {
            ["name"] = "Щит Судьбы",
            ["description"] = "Чернила Судьбы смягчают следующий критический провал до обычного провала.",
            ["category"] = "buff",
            ["visibility"] = "visible"
        },
        ["allowedRealms"] = new JsonArray("mortal_world"),
        ["allowedTargetKinds"] = new JsonArray("player"),
        ["components"] = new JsonArray(new JsonObject
        {
            ["componentId"] = "fate_shield_reaction",
            ["profile"] = "event_reaction",
            ["priority"] = -100,
            ["payload"] = new JsonObject
            {
                ["eventType"] = "owner_critical_failure",
                ["resultKind"] = "event_outcome",
                ["originalOutcome"] = "critical_failure",
                ["resolvedOutcome"] = "failure",
                ["dependency"] = "before_current_event",
                ["maxExpansion"] = 1
            }
        }),
        ["parameterBounds"] = new JsonObject(),
        ["stacking"] = new JsonObject
        {
            ["stackKey"] = "ink-feather-fate-shield",
            ["policy"] = "independent",
            ["maxStacks"] = 10,
            ["atMaximum"] = "no_change",
            ["refreshMode"] = null,
            ["mergeRule"] = null
        },
        ["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = 1,
            ["consumingEventTypes"] = new JsonArray("owner_critical_failure")
        },
        ["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "fate_shield_on_critical_failure",
            ["eventType"] = "owner_critical_failure",
            ["priority"] = -100,
            ["componentIds"] = new JsonArray("fate_shield_reaction"),
            ["consumeUses"] = true,
            ["resolutionMode"] = "deterministic"
        }),
        ["removal"] = new JsonObject
        {
            ["dispelCategories"] = new JsonArray("fate"),
            ["cureKinds"] = new JsonArray(),
            ["onSourceLoss"] = "no_change",
            ["onConditionLoss"] = null,
            ["manualAuthorities"] = new JsonArray()
        },
        ["links"] = new JsonArray()
    };
}
