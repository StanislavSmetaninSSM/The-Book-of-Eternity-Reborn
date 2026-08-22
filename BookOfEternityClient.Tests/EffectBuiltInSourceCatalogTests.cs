using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectBuiltInSourceCatalogTests
{
    [Fact]
    public void CanonicalSourceAuthority_ContainsClosedFateShieldDefinition()
    {
        var sourceRoots = EffectAcceptedTurnInputComposer.SourceAuthorityPaths
            .ToDictionary(
                static path => path,
                static _ => (JsonNode?)null,
                StringComparer.Ordinal);

        var authority = EffectAcceptedTurnInputComposer
            .BuildCanonicalSourceAuthority(sourceRoots);
        var resolution = authority.ResolveCanonicalBinding(
            new EffectSourceKey(
                "mortal_world",
                "fate_card",
                "builtin_ink_feather_fate_shield",
                "fate-shield-next-critical-failure"),
            "player");

        Assert.Empty(authority.Issues);
        Assert.True(
            resolution.Success,
            string.Join(Environment.NewLine, resolution.Issues.Select(static issue => issue.ToString())));
        var definition = Assert.IsType<JsonObject>(resolution.Source?.Definition);
        Assert.Equal("Щит Судьбы", definition["display"]?["name"]?.GetValue<string>());
        Assert.Equal("uses", definition["lifetime"]?["mode"]?.GetValue<string>());
        Assert.Equal(1, definition["lifetime"]?["initialUses"]?.GetValue<int>());
        var consumingEventTypes = Assert.IsType<JsonArray>(
            definition["lifetime"]?["consumingEventTypes"]);
        Assert.Contains(
            "owner_critical_failure",
            consumingEventTypes
                .Select(static node => node?.GetValue<string>()));
        var reaction = Assert.IsType<JsonObject>(
            Assert.Single(definition["components"]!.AsArray()));
        Assert.Equal(
            "event_outcome",
            reaction["payload"]?["resultKind"]?.GetValue<string>());
        Assert.Equal(
            "critical_failure",
            reaction["payload"]?["originalOutcome"]?.GetValue<string>());
        Assert.Equal(
            "failure",
            reaction["payload"]?["resolvedOutcome"]?.GetValue<string>());
    }

    [Fact]
    public void FateShieldApply_RequiresExactAcceptedTurnGrant()
    {
        var denied = BuildFateShieldPlan(new HashSet<string>(StringComparer.Ordinal));
        Assert.False(denied.Success);
        Assert.Contains(
            denied.Issues,
            static issue => issue.Code == "effect_source_application_authority_missing");

        var granted = BuildFateShieldPlan(new HashSet<string>(StringComparer.Ordinal)
        {
            EffectBuiltInSourceCatalog.FateShieldApplicationAuthority
        });
        Assert.True(
            granted.Success,
            string.Join(Environment.NewLine, granted.Issues.Select(static issue => issue.ToString())));
        var effect = Assert.Single(granted.Plan!.ActiveEffects);
        Assert.Equal("Щит Судьбы", effect["display"]?["name"]?.GetValue<string>());
        Assert.Equal("uses", effect["lifetime"]?["mode"]?.GetValue<string>());
        Assert.Equal(1, effect["lifetime"]?["remainingUses"]?.GetValue<int>());
    }

    [Fact]
    public void SourceAuthority_RejectsUnregisteredRequiredApplicationAuthority()
    {
        var source = Assert.Single(EffectBuiltInSourceCatalog.CreateCanonicalExports());
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            new[]
            {
                source with
                {
                    RequiredApplicationAuthority = "unregistered:forged_authority"
                }
            },
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal)));

        Assert.Contains(
            authority.Issues,
            static issue => issue.Code == "effect_source_application_authority_unknown");
    }

    [Fact]
    public void FindNewFateShieldEffectIds_UsesExactSourceAndSupportsIndependentStacks()
    {
        var before = CreatePlayerEffects(
            CreateEffect("effect_fate_shield_old", builtInSource: true));
        var current = CreatePlayerEffects(
            CreateEffect("effect_fate_shield_old", builtInSource: true),
            CreateEffect("effect_same_name_wrong_source", builtInSource: false),
            CreateEffect("effect_fate_shield_new", builtInSource: true));

        var added = EffectBuiltInSourceCatalog.FindNewFateShieldEffectIds(
            current.ToJsonString(),
            before.ToJsonString());

        Assert.Equal(new[] { "effect_fate_shield_new" }, added);
    }

    [Theory]
    [InlineData("[INK_FEATHER_ACTION: FATE_SHIELD] exact", "mortal_world", true)]
    [InlineData("[INK_FEATHER_ACTION: fate_shield] alias", "mortal_world", false)]
    [InlineData("[INK_FEATHER_ACTION: FATE_SHIELD] wrong realm", "chaos_sea", false)]
    [InlineData("ordinary action", "mortal_world", false)]
    public void AcceptedTurnGrant_IsExactAndMortalOnly(
        string playerAction,
        string realm,
        bool expected)
    {
        var authorities = EffectBuiltInSourceCatalog
            .ResolveAcceptedTurnApplicationAuthorities(playerAction, realm);

        Assert.Equal(
            expected,
            authorities.Contains(EffectBuiltInSourceCatalog.FateShieldApplicationAuthority));
    }

    [Fact]
    public void CriticalFailureReport_BindsSealedDiceAndSelectsOneOldestShield()
    {
        var first = CreateMaterializedFateShield("effect_fate_shield_first", createdAtTurn: 40);
        var second = CreateMaterializedFateShield("effect_fate_shield_second", createdAtTurn: 41);
        var carriers = CreateFateShieldCarriers(first, second);

        var result = EffectAcceptedEventReportCatalog.Compose(
            new JsonArray(CreateCriticalFailureReport()),
            turn: 42,
            realm: "mortal_world",
            authoritativeDice: new[] { 1, 17, 8 },
            carriers);

        Assert.Empty(result.Issues);
        var acceptedEvent = Assert.Single(result.LifecycleEvents);
        Assert.Equal(
            "owner_critical_failure",
            acceptedEvent["phase"]?.GetValue<string>());
        Assert.Equal(
            "effect_fate_shield_first",
            acceptedEvent["effectId"]?.GetValue<string>());
        Assert.Equal(
            "fate_shield_on_critical_failure",
            acceptedEvent["triggerId"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("normal", 1, "failure", false)]
    [InlineData("normal", 2, "failure", true)]
    [InlineData("advantage", 1, "failure", true)]
    [InlineData("normal", 1, "critical_failure", true)]
    public void CriticalFailureReport_RejectsUnprovedOrUnmitigatedOutcome(
        string rollMode,
        int selectedValue,
        string resolvedOutcome,
        bool expectError)
    {
        var report = CreateCriticalFailureReport();
        report["evidence"]!["rollMode"] = rollMode;
        report["evidence"]!["selectedValue"] = selectedValue;
        report["evidence"]!["resolvedOutcome"] = resolvedOutcome;
        if (string.Equals(rollMode, "advantage", StringComparison.Ordinal))
            report["evidence"]!["diceIndexes"] = new JsonArray(0, 1);

        var result = EffectAcceptedEventReportCatalog.Compose(
            new JsonArray(report),
            turn: 42,
            realm: "mortal_world",
            authoritativeDice: new[] { 1, 17, 8 },
            CreateFateShieldCarriers(
                CreateMaterializedFateShield("effect_fate_shield", createdAtTurn: 40)));

        Assert.Equal(expectError, result.Issues.Count != 0);
        Assert.Equal(expectError ? 0 : 1, result.LifecycleEvents.Count);
    }

    [Fact]
    public void CriticalFailureReport_ConsumesExactlyOneIndependentShield()
    {
        var first = CreateMaterializedFateShield("effect_fate_shield_first", createdAtTurn: 40);
        var second = CreateMaterializedFateShield("effect_fate_shield_second", createdAtTurn: 41);
        var carriers = CreateFateShieldCarriers(first, second);
        var reports = new JsonArray(CreateCriticalFailureReport());
        var accepted = EffectAcceptedEventReportCatalog.Compose(
            reports,
            turn: 42,
            realm: "mortal_world",
            authoritativeDice: new[] { 1, 17, 8 },
            carriers);
        Assert.Empty(accepted.Issues);

        var commands = EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot();
        commands["effectEventReports"] = reports.DeepClone();
        var input = new EffectAcceptedTurnInput(
            "session_fate_shield",
            "snapshot_fate_shield",
            commands,
            BuildBuiltInSourceAuthority(),
            BuildPlayerTargetAuthority(),
            EffectAcceptedTurnInputComposer.BuildAcceptedEventInput(
                42,
                commands,
                preTurnCarriers: carriers,
                acceptedCarriers: carriers,
                acceptedReportedLifecycleEvents: accepted.LifecycleEvents),
            PreTurnCarriers: carriers,
            PreTurnIdentityIndex: CreateFateShieldIdentityIndex(
                first,
                second));

        var planned = EffectAcceptedTurnPlanner.Build(
            input,
            "fate-shield-consume-test-fingerprint",
            new EffectIdentityFactory());
        Assert.True(
            planned.Success,
            string.Join(Environment.NewLine, planned.Issues.Select(static issue => issue.ToString())));

        var finalized = EffectAcceptedTurnPlanner.FinalizeAfterResourceGraph(
            planned.Plan!,
            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerExecution>(),
            new EffectIdentityFactory());

        Assert.True(
            finalized.Success,
            string.Join(Environment.NewLine, finalized.Issues.Select(static issue => issue.ToString())));
        var remaining = finalized.Plan!.CarrierAfterImages[
            EffectCarrierCatalog.PlayerPath]["activeEffects"]!.AsArray();
        var survivor = Assert.IsType<JsonObject>(Assert.Single(remaining));
        Assert.Equal(
            "effect_fate_shield_second",
            survivor["effectId"]?.GetValue<string>());
    }

    private static EffectAcceptedTurnPlanningResult BuildFateShieldPlan(
        IReadOnlySet<string> grantedAuthorities)
    {
        var sourceAuthority = BuildBuiltInSourceAuthority(grantedAuthorities);
        var targetAuthority = BuildPlayerTargetAuthority();
        var commands = new JsonObject
        {
            ["effectChanges"] = new JsonArray(new JsonObject
            {
                ["operation"] = "apply",
                ["target"] = new JsonObject
                {
                    ["kind"] = "player",
                    ["targetId"] = "player_current"
                },
                ["source"] = new JsonObject
                {
                    ["kind"] = EffectBuiltInSourceCatalog.FateShieldSourceKind,
                    ["sourceId"] = EffectBuiltInSourceCatalog.FateShieldSourceId,
                    ["definitionKey"] = EffectBuiltInSourceCatalog.FateShieldDefinitionKey
                },
                ["parameters"] = new JsonObject(),
                ["eventRef"] = new JsonObject
                {
                    ["kind"] = "accepted_turn",
                    ["authorityId"] = "turn_42"
                },
                ["reason"] = "Игрок оплатил Щит Судьбы Чернильными Перьями."
            }),
            ["effectResolutionReceipts"] = new JsonArray()
        };
        var input = new EffectAcceptedTurnInput(
            "session_fate_shield",
            "snapshot_fate_shield",
            commands,
            sourceAuthority,
            targetAuthority,
            new JsonObject
            {
                ["turn"] = 42,
                ["events"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "accepted_turn",
                    ["authorityId"] = "turn_42",
                    ["eventRef"] = "turn_42:accepted_effect"
                }),
                ["lifecycleEvents"] = new JsonArray()
            },
            PreTurnCarriers: new EffectCarrierCatalogInput(
                null,
                null,
                null,
                null,
                null,
                null));

        return EffectAcceptedTurnPlanner.Build(
            input,
            "fate-shield-test-fingerprint",
            new EffectIdentityFactory());
    }

    private static EffectSourceAuthority BuildBuiltInSourceAuthority(
        IReadOnlySet<string>? grantedAuthorities = null) =>
        EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            EffectBuiltInSourceCatalog.CreateCanonicalExports(),
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            grantedAuthorities ?? new HashSet<string>(StringComparer.Ordinal)));

    private static EffectTargetAuthority BuildPlayerTargetAuthority() =>
        EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            new[]
            {
                new EffectTargetExport(
                    "mortal_world",
                    "player",
                    "player_current",
                    SameTurn: false)
            },
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            null));

    private static JsonObject CreateMaterializedFateShield(
        string effectId,
        int createdAtTurn)
    {
        var plan = BuildFateShieldPlan(new HashSet<string>(StringComparer.Ordinal)
        {
            EffectBuiltInSourceCatalog.FateShieldApplicationAuthority
        });
        Assert.True(
            plan.Success,
            string.Join(Environment.NewLine, plan.Issues.Select(static issue => issue.ToString())));
        var effect = Assert.Single(plan.Plan!.ActiveEffects).DeepClone().AsObject();
        effect["effectId"] = effectId;
        effect["chronology"]!["createdAtTurn"] = createdAtTurn;
        effect["chronology"]!["lastTransitionTurn"] = createdAtTurn;
        return effect;
    }

    private static EffectCarrierCatalogInput CreateFateShieldCarriers(
        params JsonObject[] effects) =>
        new(
            CreatePlayerEffects(effects),
            null,
            null,
            null,
            null,
            null);

    private static JsonObject CreateFateShieldIdentityIndex(
        params JsonObject[] effects)
    {
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects);
        var entries = index["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        for (var ordinal = 0; ordinal < entries.Length; ordinal++)
        {
            var effect = effects[ordinal];
            var createdAtTurn = effect["chronology"]!["createdAtTurn"]!.GetValue<int>();
            var eventRef = $"turn_{createdAtTurn}:fate_shield_created:{ordinal + 1}";
            var transitionId = $"effect_transition_fate_shield_{ordinal + 1}";
            entries[ordinal]["createdAtTurn"] = createdAtTurn;
            var transition = entries[ordinal]["transitions"]![0]!.AsObject();
            transition["transitionId"] = transitionId;
            transition["turn"] = createdAtTurn;
            transition["eventRef"] = eventRef;
            effect["chronology"]!["createdEventRef"] = eventRef;
            effect["chronology"]!["lastTransitionId"] = transitionId;
        }
        return index;
    }

    private static JsonObject CreateCriticalFailureReport() => new()
    {
        ["eventType"] = "owner_critical_failure",
        ["target"] = new JsonObject
        {
            ["kind"] = "player",
            ["targetId"] = "player_current"
        },
        ["evidence"] = new JsonObject
        {
            ["kind"] = "mortal_action_roll",
            ["rollMode"] = "normal",
            ["diceIndexes"] = new JsonArray(0),
            ["selectedIndex"] = 0,
            ["selectedValue"] = 1,
            ["originalOutcome"] = "critical_failure",
            ["resolvedOutcome"] = "failure"
        },
        ["reason"] = "Щит Судьбы смягчил натуральную единицу до обычного провала."
    };

    private static JsonObject CreatePlayerEffects(params JsonObject[] effects) => new()
    {
        ["schemaVersion"] = 1,
        ["activeEffects"] = new JsonArray(
            effects.Select(static effect => (JsonNode)effect).ToArray())
    };

    private static JsonObject CreateEffect(string effectId, bool builtInSource) => new()
    {
        ["effectId"] = effectId,
        ["display"] = new JsonObject { ["name"] = "Щит Судьбы" },
        ["source"] = builtInSource
            ? new JsonObject
            {
                ["kind"] = EffectBuiltInSourceCatalog.FateShieldSourceKind,
                ["sourceId"] = EffectBuiltInSourceCatalog.FateShieldSourceId,
                ["definitionKey"] = EffectBuiltInSourceCatalog.FateShieldDefinitionKey
            }
            : new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = "wound_forged_same_name",
                ["definitionKey"] = "forged-shield"
            }
    };
}
