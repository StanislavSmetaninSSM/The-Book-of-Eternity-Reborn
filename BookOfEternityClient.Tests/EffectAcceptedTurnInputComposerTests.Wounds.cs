using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectAcceptedTurnInputComposerWoundTests
{
    [Fact]
    public void BuildCanonicalSourceAuthority_ExportsPersistedCanonicalWoundGraphAsNonMaterializable()
    {
        const string woundId = "wound_composer_persisted";
        var wound = WoundContractTestData.CreateActiveWound(woundId);
        var carrier = WoundContractTestData.CreatePlayerCarrier(wound);
        var ownedSources = wound["consequences"]!["ownedEffectSources"]!.AsObject();
        var definitions = ownedSources["definitions"]!.AsArray();
        var expectedDefinition = Assert.IsType<JsonObject>(definitions[0]);
        var definitionKey = expectedDefinition["definitionKey"]!.GetValue<string>();
        var roots = new Dictionary<string, JsonNode?>
        {
            [WoundCarrierCatalog.PlayerPath] = carrier
        };

        var authority = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
        var key = new EffectSourceKey(
            "mortal_world",
            "wound",
            woundId,
            definitionKey);
        var canonical = authority.ResolveCanonicalBinding(key, "player");

        Assert.True(
            canonical.Success,
            string.Join(
                Environment.NewLine,
                canonical.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.Message} " +
                    $"(expected={issue.Expected}; actual={issue.Actual})")));
        var source = Assert.IsType<EffectSourceAuthorityEntry>(canonical.Source);
        Assert.False(source.Materializable);
        Assert.True(source.Active);
        Assert.False(source.SameTurn);
        Assert.Null(source.SourceRef);
        Assert.True(JsonNode.DeepEquals(expectedDefinition, source.Definition));
        Assert.True(authority.TryResolveWoundGroup(
            new EffectIdentitySourceGroup("mortal_world", "wound", woundId),
            out var group));
        Assert.Equal(
            new WoundOwnerCoordinate(
                "mortal_world",
                "player",
                "player_current",
                WoundCarrierCatalog.PlayerPath),
            group.Owner);
        Assert.Equal(
            new EffectTargetKey("mortal_world", "player", "player_current"),
            group.Target);
        Assert.False(group.SameTurn);
        Assert.Null(group.SourceRef);
        Assert.Null(group.PreparedSourceExportFingerprint);
        Assert.Empty(group.ApplicationRootLineage);
        Assert.Equal(2, group.ExistingRootLineage.Count);
        Assert.All(
            group.ExistingRootLineage,
            static row =>
            {
                Assert.Null(row.ApplicationRef);
                Assert.NotNull(row.EffectId);
                Assert.Equal(WoundRootOwnershipDomain.BaseWound, row.OwnershipDomain);
            });
        Assert.Equal(
            group.GraphAuthorityFingerprint,
            group.RecomputeGraphAuthorityFingerprint());

        var ordinaryApply = authority.Resolve(key, "player", new JsonObject());
        Assert.False(ordinaryApply.Success);
        Assert.Contains(
            ordinaryApply.Issues,
            static issue => issue.Code == "effect_source_not_materializable");

        expectedDefinition["display"]!["name"] = "mutated after composition";
        var detached = authority.ResolveCanonicalBinding(key, "player");
        Assert.True(detached.Success);
        Assert.NotEqual(
            "mutated after composition",
            detached.Source!.Definition["display"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void BuildCanonicalSourceAuthority_FingerprintsPersistedEmptyWoundSourceGroup()
    {
        var emptyWound = WoundContractTestData.CreateActiveWound(
            "wound_composer_empty_graph");
        emptyWound["consequences"] = new JsonObject
        {
            ["slotBudget"] = 2,
            ["slotsUsed"] = 0,
            ["entries"] = new JsonArray(),
            ["ownedEffectSources"] = new JsonObject
            {
                ["definitions"] = new JsonArray(),
                ["rootBindings"] = new JsonArray()
            }
        };
        var withEmptyGroup = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
            new Dictionary<string, JsonNode?>
            {
                [WoundCarrierCatalog.PlayerPath] =
                    WoundContractTestData.CreatePlayerCarrier(emptyWound)
            });
        var withoutWound = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
            new Dictionary<string, JsonNode?>
            {
                [WoundCarrierCatalog.PlayerPath] =
                    WoundContractTestData.CreatePlayerCarrier()
            });

        Assert.Empty(withEmptyGroup.Issues);
        Assert.Empty(withoutWound.Issues);
        Assert.NotEqual(withoutWound.Fingerprint, withEmptyGroup.Fingerprint);
        Assert.NotEqual(
            withoutWound.CanonicalFingerprint,
            withEmptyGroup.CanonicalFingerprint);
    }

    [Fact]
    public void BuildCanonicalSourceAuthority_DoesNotScanLegacyLooseWoundDefinitions()
    {
        const string woundId = "wound_legacy_loose_scan";
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var definitionKey = definition["definitionKey"]!.GetValue<string>();
        var legacyRoot = new JsonObject
        {
            ["playerWoundChanges"] = new JsonArray(new JsonObject
            {
                ["woundId"] = woundId,
                ["activeEffectDefinitions"] = new JsonArray(definition)
            })
        };

        var authority = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
            new Dictionary<string, JsonNode?>
            {
                [WoundCarrierCatalog.PlayerPath] = legacyRoot
            });
        var resolved = authority.ResolveCanonicalBinding(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                woundId,
                definitionKey),
            "player");

        Assert.False(resolved.Success);
        Assert.Contains(
            resolved.Issues,
            static issue => issue.Code == "effect_source_selector_unresolved");
        Assert.Empty(authority.SnapshotWoundGroupAuthorities());
    }

    [Fact]
    public void Compose_DerivesSealedSameTurnWoundGroupFromPreparedPlan()
    {
        var preparation = WoundAcceptedTurnPlanner.Prepare(
            WoundEffectBatchPlannerTests.CreateInputForAcceptedCache());
        Assert.True(
            preparation.Success,
            string.Join(
                Environment.NewLine,
                preparation.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.Message}")));
        var prepared = Assert.IsType<WoundPreparedAcceptedTurnPlan>(preparation.Plan);
        var binding = prepared.Binding;
        var batch = Assert.Single(prepared.EffectOperationBatches);
        var emptyEffectCarriers = new EffectCarrierCatalogInput(
            null,
            null,
            null,
            null,
            null,
            null);

        var composed = EffectAcceptedTurnInputComposer.Compose(
            binding.SessionId,
            binding.SnapshotToken,
            binding.Turn,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(),
            emptyEffectCarriers,
            emptyEffectCarriers,
            prepared.BaselineAuthority.PreTurnIdentityIndex,
            CreateSourceRoots(prepared.BaselineAuthority.PreTurnCarriers),
            realm: binding.Realm,
            preparedWoundPlan: prepared);

        Assert.Empty(composed.SourceAuthority.Issues);
        var group = Assert.Single(
            composed.SourceAuthority.SnapshotWoundGroupAuthorities());
        Assert.Equal(
            new EffectIdentitySourceGroup(
                batch.SourceExport.Realm,
                batch.SourceExport.Kind,
                batch.SourceExport.SourceId),
            group.Key);
        Assert.Equal(batch.SourceExport.Owner, group.Owner);
        Assert.True(group.SameTurn);
        Assert.Equal(batch.SourceExport.SourceRef, group.SourceRef);
        Assert.Equal(
            batch.SourceExportFingerprint,
            group.PreparedSourceExportFingerprint);
        Assert.Equal(
            batch.RootLineageAuthority
                .Where(static row => row.ApplicationRef is not null),
            group.ApplicationRootLineage);
        Assert.Equal(
            batch.RootLineageAuthority
                .Where(static row => row.EffectId is not null),
            group.ExistingRootLineage);
        Assert.Equal(
            group.GraphAuthorityFingerprint,
            group.RecomputeGraphAuthorityFingerprint());

        var export = Assert.Single(
            composed.SourceAuthority.SnapshotSameTurnWoundGroups());
        Assert.False(export.Materializable);
        Assert.True(export.Active);
        Assert.True(export.SameTurn);
        Assert.Equal(batch.SourceExport.SourceRef, export.SourceRef);
        Assert.Equal(batch.SourceExport.Definitions.Count, export.Definitions.Count);

        var definition = Assert.Single(batch.SourceExport.Definitions);
        var key = new EffectSourceKey(
            batch.SourceExport.Realm,
            batch.SourceExport.Kind,
            batch.SourceExport.SourceId,
            definition.DefinitionKey);
        var canonical = composed.SourceAuthority.ResolveCanonicalBinding(
            key,
            group.Target.Kind);
        Assert.True(canonical.Success);
        Assert.False(canonical.Source!.Materializable);
        var ordinary = composed.SourceAuthority.Resolve(
            new JsonObject
            {
                ["kind"] = batch.SourceExport.Kind,
                ["sourceRef"] = batch.SourceExport.SourceRef,
                ["definitionKey"] = definition.DefinitionKey
            },
            batch.SourceExport.Realm,
            group.Target.Kind,
            new JsonObject());
        Assert.False(ordinary.Success);
        Assert.Contains(
            ordinary.Issues,
            static issue => issue.Code == "effect_source_not_materializable");

        var events = composed.EventInput["events"]!.AsArray();
        Assert.Equal(binding.AcceptedEvents.Count, events.Count);
        Assert.Equal(
            binding.AcceptedEvents[0].EventRef,
            events[0]!["eventRef"]!.GetValue<string>());
    }

    [Fact]
    public void Compose_FullWorsenRematerializationExportsOnlyNewCurrentHeads()
    {
        var input = WoundEffectBatchPlannerTests
            .CreateWorsenInputWithRetainedAndNewRoot();
        var preparation = WoundAcceptedTurnPlanner.Prepare(input);
        Assert.True(preparation.Success, string.Join(Environment.NewLine,
            preparation.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        var prepared = Assert.IsType<WoundPreparedAcceptedTurnPlan>(
            preparation.Plan);
        var batch = Assert.Single(prepared.EffectOperationBatches);

        var composed = EffectAcceptedTurnInputComposer.Compose(
            prepared.Binding.SessionId,
            prepared.Binding.SnapshotToken,
            prepared.Binding.Turn,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(),
            input.PreTurnEffectCarriers!,
            input.PreTurnEffectCarriers!,
            input.PreTurnEffectIdentityIndex,
            CreateSourceRoots(input.PreTurnCarriers),
            realm: prepared.Binding.Realm,
            preparedWoundPlan: prepared);

        Assert.Empty(composed.SourceAuthority.Issues);
        var group = Assert.Single(
            composed.SourceAuthority.SnapshotWoundGroupAuthorities());
        Assert.Equal(batch.RootLineageAuthority, group.ApplicationRootLineage);
        Assert.Empty(group.ExistingRootLineage);
        Assert.Equal(1, batch.RootApplications.Count(static root =>
            root.PriorRootEffectId is not null));
        Assert.Equal(1, batch.RootApplications.Count(static root =>
            root.PriorRootEffectId is null));
    }

    [Fact]
    public void Compose_RejectsCallerSuppliedGenericWoundExport()
    {
        var prepared = PrepareStandardPlan();
        var binding = prepared.Binding;
        var batch = Assert.Single(prepared.EffectOperationBatches);
        var injected = new EffectSourceExport(
            batch.SourceExport.Realm,
            batch.SourceExport.Kind,
            batch.SourceExport.SourceId,
            new JsonArray(batch.SourceExport.Definitions.Select(static value =>
                (JsonNode)value.Definition).ToArray()),
            Materializable: false,
            Active: true,
            SameTurn: true,
            SourceRef: batch.SourceExport.SourceRef);

        var composed = Compose(prepared, new[] { injected });

        Assert.Contains(
            composed.SourceAuthority.Issues,
            static issue =>
                issue.Code == "effect_source_wound_external_export_forbidden");
        Assert.Empty(composed.SourceAuthority.SnapshotWoundGroupAuthorities());
        Assert.Empty(composed.SourceAuthority.SnapshotSameTurnWoundEntries());
    }

    [Fact]
    public void Compose_RejectsPreparedBatchChangedUnderOldSeals()
    {
        var prepared = PrepareStandardPlan();
        var originalBatch = Assert.Single(prepared.EffectOperationBatches);
        var source = originalBatch.SourceExport;
        var definitions = source.Definitions.ToArray();
        var changedDefinition = definitions[0].Definition;
        changedDefinition["forged"] = true;
        definitions[0] = new WoundEffectSourceDefinition(
            definitions[0].DefinitionKey,
            changedDefinition);
        var changedSource = new WoundEffectSourceExport(
            source.SchemaVersion,
            source.Kind,
            source.SourceId,
            source.SourceRef,
            source.State,
            source.Materializable,
            source.Realm,
            source.Owner,
            source.CausalEventRef,
            source.EventSemanticFingerprint,
            source.OpportunityId,
            source.OpportunityAuthorityFingerprint,
            definitions);
        var changedBatch = new WoundEffectOperationBatch(
            originalBatch.LocalWoundRef,
            originalBatch.PreparedWoundId,
            changedSource,
            originalBatch.RootApplications,
            originalBatch.TerminalOperations,
            originalBatch.RootLineageAuthority,
            originalBatch.SourceExportFingerprint,
            originalBatch.TransitionAuthority);
        var tampered = new WoundPreparedAcceptedTurnPlan(
            prepared.Binding,
            prepared.BindingFingerprint,
            prepared.InputFingerprint,
            prepared.WoundPreparationFingerprint,
            prepared.AllocatedWoundIds,
            prepared.AllocatedTransitionIds,
            prepared.PreparedWounds,
            new[] { changedBatch },
            prepared.BaselineAuthority);

        var composed = Compose(tampered);

        Assert.Contains(
            composed.SourceAuthority.Issues,
            static issue => issue.Code == "wound_plan_prepared_seal_mismatch");
        Assert.Empty(composed.SourceAuthority.SnapshotWoundGroupAuthorities());
        Assert.Empty(composed.SourceAuthority.SnapshotSameTurnWoundEntries());
    }

    [Theory]
    [InlineData(WoundEffectBatchPlannerTests.OwnerFlavor.Player, "player")]
    [InlineData(WoundEffectBatchPlannerTests.OwnerFlavor.Npc, "npc")]
    [InlineData(WoundEffectBatchPlannerTests.OwnerFlavor.Combatant, "combatant")]
    [InlineData(WoundEffectBatchPlannerTests.OwnerFlavor.AfterlifeGuardian, "guardian")]
    public void Compose_MapsPreparedWoundOwnerThroughClosedTargetAdapter(
        WoundEffectBatchPlannerTests.OwnerFlavor ownerFlavor,
        string expectedTargetKind)
    {
        var result = WoundAcceptedTurnPlanner.Prepare(
            WoundEffectBatchPlannerTests.CreateInputForAcceptedCache(ownerFlavor));
        Assert.True(
            result.Success,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.Message}")));
        var prepared = Assert.IsType<WoundPreparedAcceptedTurnPlan>(result.Plan);

        var composed = Compose(prepared);

        Assert.Empty(composed.SourceAuthority.Issues);
        var group = Assert.Single(
            composed.SourceAuthority.SnapshotWoundGroupAuthorities());
        Assert.Equal(prepared.Binding.Realm, group.Target.Realm);
        Assert.Equal(expectedTargetKind, group.Target.Kind);
        Assert.Equal(group.Owner.OwnerId, group.Target.TargetId);
    }

    [Fact]
    public void Compose_FeedsPreparedWoundDirectlyIntoTypedEffectStage()
    {
        var prepared = PrepareStandardPlan();
        var batch = Assert.Single(prepared.EffectOperationBatches);
        var definition = Assert.Single(batch.SourceExport.Definitions);
        var localKey = new EffectSourceKey(
            batch.SourceExport.Realm,
            batch.SourceExport.Kind,
            batch.LocalWoundRef,
            definition.DefinitionKey);
        var locations = new EffectApplicationDiagnosticLocations(new[]
        {
            new KeyValuePair<EffectSourceKey, EffectApplicationDiagnosticLocation>(
                localKey,
                new(
                    "woundDecisions[0].proposal.consequenceDefinitions[0].definition.components",
                    "wound_materialization"))
        }).BindPreparedSources(prepared);
        var composed = Compose(prepared) with { WoundApplicationLocations = locations };
        var root = Assert.Single(batch.RootApplications);
        Assert.NotNull(composed.SkillScopeAuthority);
        Assert.True(locations.TryResolve(root.ExpectedSourceKey, out var location));
        Assert.Equal(
            "woundDecisions[0].proposal.consequenceDefinitions[0].definition.components",
            location.Path);
        Assert.Equal("wound_materialization", location.Section);

        var result = WoundEffectBatchPlanner.Build(
            prepared,
            composed,
            new EffectIdentityFactory());

        Assert.True(
            result.Success,
            string.Join(" | ", result.Issues.Select(static issue =>
                issue.Code + ":" + issue.Message)));
        var plan = Assert.IsType<WoundEffectBatchAcceptedPlan>(result.Plan);
        Assert.Single(plan.ApplicationResults);
        Assert.Empty(plan.TerminationResults);
        Assert.Empty(composed.RawCommands["effectChanges"]!.AsArray());
    }

    private static WoundPreparedAcceptedTurnPlan PrepareStandardPlan()
    {
        var result = WoundAcceptedTurnPlanner.Prepare(
            WoundEffectBatchPlannerTests.CreateInputForAcceptedCache());
        Assert.True(
            result.Success,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.Message}")));
        return Assert.IsType<WoundPreparedAcceptedTurnPlan>(result.Plan);
    }

    private static EffectAcceptedTurnInput Compose(
        WoundPreparedAcceptedTurnPlan prepared,
        IReadOnlyList<EffectSourceExport>? acceptedPlanSourceExports = null)
    {
        var binding = prepared.Binding;
        var emptyEffectCarriers = new EffectCarrierCatalogInput(
            null,
            null,
            null,
            null,
            null,
            null);
        return EffectAcceptedTurnInputComposer.Compose(
            binding.SessionId,
            binding.SnapshotToken,
            binding.Turn,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(),
            emptyEffectCarriers,
            emptyEffectCarriers,
            prepared.BaselineAuthority.PreTurnIdentityIndex,
            CreateSourceRoots(prepared.BaselineAuthority.PreTurnCarriers),
            acceptedPlanSourceExports: acceptedPlanSourceExports,
            realm: binding.Realm,
            preparedWoundPlan: prepared);
    }

    private static IReadOnlyDictionary<string, JsonNode?> CreateSourceRoots(
        WoundCarrierCatalogInput carriers)
    {
        var roots = new Dictionary<string, JsonNode?>();
        Add(WoundCarrierCatalog.PlayerPath, carriers.PlayerWounds);
        Add(WoundCarrierCatalog.NpcPath, carriers.NpcWounds);
        Add(WoundCarrierCatalog.EnemiesPath, carriers.EnemyCombatants);
        Add(WoundCarrierCatalog.AlliesPath, carriers.AllyCombatants);
        Add(WoundCarrierCatalog.AfterlifeProfilesPath, carriers.AfterlifeProfiles);
        return roots;

        void Add(string path, JsonObject? root)
        {
            if (root is not null)
                roots[path] = root;
        }
    }
}
