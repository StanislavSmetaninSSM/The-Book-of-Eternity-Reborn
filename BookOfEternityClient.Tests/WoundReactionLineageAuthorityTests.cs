using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundEffectLineagePlannerTests
{
    [Fact]
    public void ReactionExecutor_WoundApplyDefinitionRequiresTypedLineageAuthority()
    {
        var wound = CreateLineageWound();
        var sourceAuthority = CreateReactionSourceAuthority(wound);
        var root = CreateEffectFromDefinition(wound, RootEffectId, RootDefinitionKey);
        var carriers = CreatePlayerCarriers(root);
        var catalog = EffectCarrierCatalog.Build(carriers);
        var lineageAuthority = WoundReactionLineageAuthority.Build(
            sourceAuthority,
            CreateLineageIdentities(rootState: "active", childState: "removed"),
            catalog,
            Array.Empty<WoundApplicationRootEffectBinding>());
        var eventInput = CreateReactionEventInput("owner_damaged");

        var missingAuthority = EffectReactionExecutor.Plan(
            eventInput,
            sourceAuthority,
            carriers);
        var accepted = EffectReactionExecutor.Plan(
            eventInput,
            sourceAuthority,
            carriers,
            lineageAuthority);

        Assert.False(missingAuthority.Success);
        Assert.Contains(missingAuthority.Issues, static issue =>
            issue.Code == "effect_reaction_wound_lineage_authority_missing");
        Assert.True(
            accepted.Success,
            string.Join(Environment.NewLine, accepted.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        var execution = Assert.Single(accepted.Executions);
        Assert.Equal(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                WoundId,
                ChildDefinitionKey),
            execution.DownstreamSourceKey);
    }

    [Fact]
    public void ReactionExecutor_ResourceEventUsesSameTypedWoundLineageAuthority()
    {
        const string eventType = "resource_depleted";
        const string triggerId = "on_resource_depleted";
        var wound = CreateLineageWound(eventType, triggerId);
        var sourceAuthority = CreateReactionSourceAuthority(wound);
        var root = CreateEffectFromDefinition(wound, RootEffectId, RootDefinitionKey);
        var carriers = CreatePlayerCarriers(root);
        var catalog = EffectCarrierCatalog.Build(carriers);
        var lineageAuthority = WoundReactionLineageAuthority.Build(
            sourceAuthority,
            CreateLineageIdentities(rootState: "active", childState: "removed"),
            catalog,
            Array.Empty<WoundApplicationRootEffectBinding>());
        Assert.True(catalog.TryResolveOne(RootEffectId, out var occurrence));
        var producer = new ResourceAppliedEvent(
            eventType,
            "resource_operation_wound_lineage",
            "turn_43:resource:wound_lineage",
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health"),
            Before: 1m,
            After: 0m,
            AppliedAmount: 1m,
            Turn: 43,
            ExecutionSequence: 1,
            SourceFingerprint: "source_fingerprint_wound_lineage");

        var accepted = EffectReactionExecutor.PlanResourceEvent(
            occurrence,
            triggerId,
            producer,
            sourceAuthority,
            lineageAuthority);

        Assert.True(
            accepted.Success,
            string.Join(Environment.NewLine, accepted.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        Assert.Equal(
            ChildDefinitionKey,
            Assert.Single(accepted.Executions).DownstreamSourceKey?.DefinitionKey);
    }

    [Fact]
    public void ReactionAuthority_ResolvesExactPersistedRootEdgeAndOwnershipDomain()
    {
        var wound = CreateLineageWound();
        var sourceAuthority = CreateReactionSourceAuthority(wound);
        var root = CreateEffectFromDefinition(wound, RootEffectId, RootDefinitionKey);
        var catalog = EffectCarrierCatalog.Build(CreatePlayerCarriers(root));
        var authority = WoundReactionLineageAuthority.Build(
            sourceAuthority,
            CreateLineageIdentities(rootState: "active", childState: "removed"),
            catalog,
            Array.Empty<WoundApplicationRootEffectBinding>());

        Assert.True(
            authority.Success,
            string.Join(Environment.NewLine, authority.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        Assert.True(catalog.TryResolveOne(RootEffectId, out var occurrence));
        var component = Assert.Single(root["components"]!.AsArray().OfType<JsonObject>());

        var resolution = authority.ResolveApplyDefinition(
            occurrence,
            component,
            new EffectTargetKey("mortal_world", "player", "player_current"),
            ChildDefinitionKey);

        Assert.True(
            resolution.Success,
            string.Join(Environment.NewLine, resolution.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        Assert.Equal(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                WoundId,
                ChildDefinitionKey),
            resolution.Source!.Key);
        Assert.Equal(
            WoundRootOwnershipDomain.ForComplication(ComplicationId),
            resolution.OwnershipDomain);
    }

    [Fact]
    public void ReactionAuthority_RequiresExactSameTurnApplicationRootResult()
    {
        const string applicationRef = "application_wound_lineage_root";
        var wound = CreateLineageWound();
        var sourceAuthority = CreateReactionSourceAuthority(
            wound,
            sameTurn: true,
            applicationRef);
        var root = CreateEffectFromDefinition(wound, RootEffectId, RootDefinitionKey);
        var catalog = EffectCarrierCatalog.Build(CreatePlayerCarriers(root));
        var identities = CreateLineageIdentities(
            rootState: "active",
            childState: "removed");

        var missing = WoundReactionLineageAuthority.Build(
            sourceAuthority,
            identities,
            catalog,
            Array.Empty<WoundApplicationRootEffectBinding>());
        var exact = WoundReactionLineageAuthority.Build(
            sourceAuthority,
            identities,
            catalog,
            new[]
            {
                new WoundApplicationRootEffectBinding(applicationRef, RootEffectId)
            });

        Assert.False(missing.Success);
        Assert.Contains(missing.Issues, static issue =>
            issue.Code == "effect_reaction_wound_root_binding_invalid");
        Assert.True(
            exact.Success,
            string.Join(Environment.NewLine, exact.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
    }

    [Fact]
    public void ReactionAuthority_RejectsProducerPayloadOutsidePersistedExactEdge()
    {
        var wound = CreateLineageWound();
        var root = CreateEffectFromDefinition(wound, RootEffectId, RootDefinitionKey);
        var component = Assert.Single(root["components"]!.AsArray().OfType<JsonObject>());
        component["payload"]!["definitionKey"] = RootDefinitionKey;
        var catalog = EffectCarrierCatalog.Build(CreatePlayerCarriers(root));
        var authority = WoundReactionLineageAuthority.Build(
            CreateReactionSourceAuthority(wound),
            CreateLineageIdentities(rootState: "active", childState: "removed"),
            catalog,
            Array.Empty<WoundApplicationRootEffectBinding>());
        Assert.True(authority.Success);
        Assert.True(catalog.TryResolveOne(RootEffectId, out var occurrence));

        var resolution = authority.ResolveApplyDefinition(
            occurrence,
            component,
            new EffectTargetKey("mortal_world", "player", "player_current"),
            RootDefinitionKey);

        Assert.False(resolution.Success);
        Assert.Contains(resolution.Issues, static issue =>
            issue.Code == "effect_reaction_wound_lineage_edge_invalid");
    }

    [Fact]
    public void ReactionAuthority_RejectsMultipleFirstCreateParentsBeforeReaction()
    {
        var wound = CreateLineageWound();
        var root = CreateEffectFromDefinition(wound, RootEffectId, RootDefinitionKey);
        var child = CreateEffectFromDefinition(wound, ChildEffectId, ChildDefinitionKey);
        var authority = WoundReactionLineageAuthority.Build(
            CreateReactionSourceAuthority(wound),
            CreateLineageIdentities(
                rootState: "active",
                childState: "active",
                configure: static (_, childIdentity) =>
                    childIdentity["transitions"]![0]!["sourceEffectIds"] =
                        new JsonArray(RootEffectId, "effect_foreign_parent")),
            EffectCarrierCatalog.Build(CreatePlayerCarriers(root, child)),
            Array.Empty<WoundApplicationRootEffectBinding>());

        Assert.False(authority.Success);
        Assert.Contains(authority.Issues, static issue =>
            issue.Code == "effect_reaction_wound_lineage_create_invalid");
    }

    [Fact]
    public void ReactionAuthority_RejectsDefinitionClaimedByDifferentRootDomains()
    {
        const string secondRootEffectId = "effect_wound_lineage_second_root";
        var wound = CreateSharedDefinitionDomainWound(
            secondRootEffectId,
            ChildDefinitionKey);
        var first = CreateEffectFromDefinition(wound, RootEffectId, RootDefinitionKey);
        var second = CreateEffectFromDefinition(
            wound,
            secondRootEffectId,
            ChildDefinitionKey);
        var identityRoot = EffectMaterializationTestFixture.CreateIdentityIndex(
            first,
            second);
        var entries = identityRoot["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        ConfigureCreate(entries[0], 0, Array.Empty<string>());
        ConfigureCreate(entries[1], 1, Array.Empty<string>());

        var authority = WoundReactionLineageAuthority.Build(
            CreateReactionSourceAuthority(wound),
            ParseIdentityState(identityRoot),
            EffectCarrierCatalog.Build(CreatePlayerCarriers(first, second)),
            Array.Empty<WoundApplicationRootEffectBinding>());

        Assert.False(authority.Success);
        Assert.Contains(authority.Issues, static issue =>
            issue.Code == "effect_reaction_wound_lineage_cross_domain");
    }

    private static EffectSourceAuthority CreateReactionSourceAuthority(
        WoundMaterializationEnvelope wound,
        bool sameTurn = false,
        string? applicationRef = null)
    {
        Assert.True(WoundEffectCarrierAdapter.TryCreateTargetKey(
            wound.Owner,
            out var target));
        var definitions = wound.Consequences.OwnedEffectSources.Definitions
            .Select(static definition =>
                JsonNode.Parse(definition.GetRawText())!.AsObject())
            .ToArray();
        var exportedDefinitions = new JsonArray(definitions
            .Select(static definition => (JsonNode)definition.DeepClone())
            .ToArray());
        var sourceRef = sameTurn ? "wound_ref_lineage_same_turn" : null;
        var sourceExportFingerprint = sameTurn
            ? "sha256:" + new string('a', 64)
            : null;
        var export = new EffectSourceExport(
            wound.Owner.Realm,
            "wound",
            wound.WoundId,
            exportedDefinitions,
            Materializable: false,
            Active: true,
            SameTurn: sameTurn,
            SourceRef: sourceRef);
        var complicationRoots = wound.Complications
            .SelectMany(complication => complication.OwnedEffectIds.Select(effectId =>
                (EffectId: effectId, complication.ComplicationId)))
            .ToDictionary(
                static value => value.EffectId,
                static value => value.ComplicationId,
                StringComparer.Ordinal);
        var rootRows = wound.Consequences.OwnedEffectSources.RootBindings
            .Select((binding, index) => new WoundRootLineageAuthorityRow(
                sameTurn
                    ? index == 0
                        ? applicationRef
                        : applicationRef + "_" + index
                    : null,
                sameTurn ? null : binding.EffectId,
                binding.DefinitionKey,
                complicationRoots.TryGetValue(binding.EffectId, out var complicationId)
                    ? WoundRootOwnershipDomain.ForComplication(complicationId)
                    : WoundRootOwnershipDomain.BaseWound))
            .ToArray();
        var group = new WoundSourceGroupAuthority(
            new EffectIdentitySourceGroup(
                wound.Owner.Realm,
                "wound",
                wound.WoundId),
            wound.Owner,
            target,
            sameTurn,
            sourceRef,
            sourceExportFingerprint,
            definitions.Select(definition => new WoundEffectSourceDefinition(
                    definition["definitionKey"]!.GetValue<string>(),
                    definition))
                .ToArray(),
            sameTurn ? rootRows : Array.Empty<WoundRootLineageAuthorityRow>(),
            sameTurn ? Array.Empty<WoundRootLineageAuthorityRow>() : rootRows);
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            sameTurn ? Array.Empty<EffectSourceExport>() : new[] { export },
            sameTurn ? new[] { export } : Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: new[] { group }));
        Assert.Empty(authority.Issues);
        return authority;
    }

    private static JsonObject CreateEffectFromDefinition(
        WoundMaterializationEnvelope wound,
        string effectId,
        string definitionKey)
    {
        var effect = CreateEffect(effectId, definitionKey);
        var definition = wound.Consequences.OwnedEffectSources.Definitions
            .Select(static value => JsonNode.Parse(value.GetRawText())!.AsObject())
            .Single(value => string.Equals(
                value["definitionKey"]!.GetValue<string>(),
                definitionKey,
                StringComparison.Ordinal));
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        return effect;
    }

    private static JsonObject CreateReactionEventInput(string phase) => new()
    {
        ["lifecycleEvents"] = new JsonArray(new JsonObject
        {
            ["eventRef"] = "turn_43:wound_lineage:reaction",
            ["turn"] = 43,
            ["phase"] = phase,
            ["realm"] = "mortal_world",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["effectId"] = RootEffectId
        })
    };
}
