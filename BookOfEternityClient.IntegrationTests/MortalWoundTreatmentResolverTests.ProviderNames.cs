using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData(false, "ambiguous")]
    [InlineData(false, "public_roles")]
    [InlineData(false, "distinct_names")]
    [InlineData(false, "stale_name")]
    [InlineData(true, "ambiguous")]
    [InlineData(true, "public_roles")]
    [InlineData(true, "distinct_names")]
    [InlineData(true, "stale_name")]
    public async Task MortalSelfMenuDistinguishesCurrentProvidersWithoutPrivateIdentity(bool diagnosis, string scenarioName)
    {
        const string secondId = "field_medic_02";
        var scenario = CreateScenario("guaranteed_current_capability_proof_stabilizes", "guaranteed");
        var hidden = scenario.Before["treatment"]!["routes"]![0]!.DeepClone().AsObject();
        hidden["routeId"] = "provider_hidden_route"; hidden["visibility"] = "hidden";
        scenario.Before["treatment"]!["routes"]!.AsArray().Add(hidden);
        var method = WoundContractTestData.CreateMortalDiagnosisPath(false, 30);
        method["reveals"] = new JsonArray("route:provider_hidden_route");
        scenario.Before["treatment"]!["diagnosisPaths"] = new JsonArray(method);
        scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before), SeedCanonicalWoundEffects = true };
        var root = Path.Combine(Path.GetTempPath(), "boe-mortal-provider-names-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var reads = new CanonicalReadActivityAccumulator();
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks { PassiveCanonicalReads = reads });
        try
        {
            files.EnsureDirectoryStructure();
            _ = AcceptedStateFixture.SeedCanonicalComponentData(files, scenario, "player", "player_current",
                WoundCarrierCatalog.PlayerPath, initial =>
                {
                    SeedMortalMenuDisplayCarriers(initial);
                    var path = initial.ResolvePath(NpcCoreChangesContract.NpcCorePath);
                    var npcs = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                    var first = npcs["NPCsInScene"]![0]!.AsObject();
                    first["name"] = "Алексей"; first["displayName"] = "Алексей";
                    first["role"] = "Лекарь"; first["plans"] = "PRIVATE_PROVIDER_FIRST";
                    var second = MortalActorTestFixtures.CreateActor(secondId, "loc_field_clinic_001", "Field clinic");
                    second["name"] = scenarioName is "distinct_names" or "stale_name" ? "Борис" : "Алексей";
                    second["displayName"] = second["name"]!.DeepClone();
                    second["role"] = scenarioName == "public_roles" ? "Страж" : "Лекарь";
                    second["plans"] = "PRIVATE_PROVIDER_SECOND";
                    // Independent genuine skill identities, not copied owner-bound capability proofs.
                    second["activeSkills"] = new JsonArray(
                        CreateTreatmentSkill("skill_second_guaranteed_01", "exact_materialized_healing_source"),
                        CreateTreatmentSkill("skill_second_medicine_01", "field_medicine"));
                    npcs["NPCsInScene"]!.AsArray().Add(second);
                    File.WriteAllText(path, npcs.ToJsonString());
                });
            await AssertMortalProviderPremisesAsync(files, new[] { "field_medic_01", secondId });
            var before = CaptureDiagnosisMenuJson(files);
            var priorReads = reads.ReadCount(NpcCoreChangesContract.NpcCorePath);
            var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
            Assert.Equal(1, reads.ReadCount(NpcCoreChangesContract.NpcCorePath) - priorReads);
            var navigation = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
            Assert.True(navigation.IsTargetSelection);
            var self = Assert.Single(navigation.Targets, row => row.Owner.OwnerKind == "player");
            Assert.True(self.Available); Assert.Equal("Себя", self.Label);
            ExplorerCommandResult selected;
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                selected = await WoundApplicationService.SelectTargetAsync(files, lease, navigation, self.Value, "/лечить");
            var menu = Assert.IsType<WoundCommandState>(selected.WoundCommandState);
            Assert.False(menu.IsTargetSelection); Assert.Equal(self.Owner, menu.SelectedTarget!.Owner);
            var providers = ProviderChoices(menu);
            Assert.Equal(2, providers.Length);
            Assert.Equal(new[] { "field_medic_01", secondId }, providers.Select(row => Provider(row).OwnerId));
            Assert.NotEqual(providers[0].Value, providers[1].Value);
            Assert.All(providers, row => Assert.Equal("player_current", diagnosis
                ? row.Selection.MortalDiagnosis!.Target.OwnerId : row.Selection.MortalTreatment!.Target.OwnerId));
            AssertDiagnosisMenuJson(before, files);
            AssertPublicOnly(selected);
            var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(selected.Prompts, row => row is UiSelectionPrompt));
            var options = prompt.Options.Where(row => providers.Any(provider => provider.Value == row.Value)).ToArray();
            Assert.Equal(2, options.Length);

            // Causal output oracles follow independent current requirements/capabilities and real Self navigation.
            if (scenarioName == "ambiguous")
            {
                Assert.All(providers, row => Assert.False(row.Available));
                Assert.All(providers, row => Assert.Contains("различить", row.Reason));
                Assert.All(providers, row => Assert.Throws<InvalidDataException>(() => menu.Resolve(row.Value)));
                Assert.All(options, row => Assert.True(row.Disabled));
            }
            else
            {
                Assert.All(providers, row => Assert.True(row.Available));
                Assert.NotEqual(providers[0].Name, providers[1].Name);
                Assert.NotEqual(options[0].Label, options[1].Label);
                Assert.All(options, row => Assert.False(row.Disabled));
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    foreach (var provider in providers)
                        Assert.Equal(provider.Selection, await WoundApplicationService.ConfirmAsync(files, lease, menu, provider.Value));
            }
            AssertDiagnosisMenuJson(before, files);
            if (scenarioName == "public_roles")
            {
                Assert.Contains("Лекарь", providers[0].Name); Assert.Contains("Страж", providers[1].Name);
                await ChangeRole("Проводник");
                var changed = CaptureDiagnosisMenuJson(files);
                var fresh = await OpenSelf();
                var freshProviders = ProviderChoices(Assert.IsType<WoundCommandState>(fresh.WoundCommandState));
                Assert.All(freshProviders, row => Assert.True(row.Available));
                Assert.Contains("Проводник", freshProviders[0].Name);
                Assert.NotEqual(providers[0].Name, freshProviders[0].Name);
                Assert.Equal(providers[0].Selection, freshProviders[0].Selection);
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    await Assert.ThrowsAsync<InvalidDataException>(() => WoundApplicationService.ConfirmAsync(files, lease, menu, providers[0].Value));
                AssertDiagnosisMenuJson(changed, files); AssertPublicOnly(fresh);
                await ChangeRole("Страж");
                var ambiguous = CaptureDiagnosisMenuJson(files);
                var ambiguousResult = await OpenSelf();
                Assert.All(ProviderChoices(Assert.IsType<WoundCommandState>(ambiguousResult.WoundCommandState)), row => Assert.False(row.Available));
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    await Assert.ThrowsAsync<InvalidDataException>(() => WoundApplicationService.ConfirmAsync(files, lease, menu, providers[1].Value));
                AssertDiagnosisMenuJson(ambiguous, files); AssertPublicOnly(ambiguousResult);
            }

            if (scenarioName == "stale_name")
            {
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                {
                    var npcs = JsonNode.Parse((await files.ReadFileAsync(lease, NpcCoreChangesContract.NpcCorePath))!)!.AsObject();
                    npcs["NPCsInScene"]![0]!["name"] = "Кирилл";
                    npcs["NPCsInScene"]![0]!["displayName"] = "Кирилл";
                    await files.WriteFileAtomicAsync(lease, NpcCoreChangesContract.NpcCorePath, npcs.ToJsonString());
                }
                var renamed = CaptureDiagnosisMenuJson(files);
                var fresh = await OpenSelf();
                var currentProvider = ProviderChoices(Assert.IsType<WoundCommandState>(fresh.WoundCommandState))[0];
                Assert.True(currentProvider.Available); Assert.Contains("Кирилл", currentProvider.Name);
                Assert.NotEqual(providers[0].Name, currentProvider.Name);
                Assert.Equal(providers[0].Selection, currentProvider.Selection);
                // This independently reaches the stale visible-name oracle on the unmodified producer.
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    await Assert.ThrowsAsync<InvalidDataException>(() => WoundApplicationService.ConfirmAsync(files, lease, menu, providers[0].Value));
                AssertDiagnosisMenuJson(renamed, files); AssertPublicOnly(fresh);
            }

            WoundOwnerCoordinate Provider(WoundTreatmentChoice row) => diagnosis ? row.Selection.MortalDiagnosis!.Provider : row.Selection.MortalTreatment!.Provider;
            WoundTreatmentChoice[] ProviderChoices(WoundCommandState state) => state.Choices.Where(row => diagnosis
                ? row.Selection.MortalDiagnosis?.Provider.OwnerKind == "npc" : row.Selection.MortalTreatment?.Provider.OwnerKind == "npc").ToArray();
            void AssertPublicOnly(ExplorerCommandResult result)
            {
                var output = JsonSerializer.Serialize(new { result.Blocks, result.Prompts });
                foreach (var hiddenText in new[] { "field_medic_01", secondId, "PRIVATE_PROVIDER_FIRST", "PRIVATE_PROVIDER_SECOND", "provider_hidden_route", "sha256:", "carrierPath" })
                    Assert.DoesNotContain(hiddenText, output, StringComparison.OrdinalIgnoreCase);
            }
            async Task ChangeRole(string role)
            {
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                var npcs = JsonNode.Parse((await files.ReadFileAsync(lease, NpcCoreChangesContract.NpcCorePath))!)!.AsObject();
                npcs["NPCsInScene"]![0]!["role"] = role;
                await files.WriteFileAtomicAsync(lease, NpcCoreChangesContract.NpcCorePath, npcs.ToJsonString());
            }
            async Task<ExplorerCommandResult> OpenSelf()
            {
                var result = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
                var state = Assert.IsType<WoundCommandState>(result.WoundCommandState);
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                return await WoundApplicationService.SelectTargetAsync(files, lease, state,
                    Assert.Single(state.Targets, row => row.Owner.OwnerKind == "player").Value, "/лечить");
            }
        }
        finally { Directory.Delete(root, recursive: true); Assert.False(Directory.Exists(root)); }
    }

    private static async Task AssertMortalProviderPremisesAsync(FileSystemManager files, string[] providerIds)
    {
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        _ = files.GetOrCreateSessionGeneration(lease);
        var current = await WoundPlayerReadService.LoadContextAsync(files, lease, retainMenuInputs: true);
        Assert.NotNull(current); Assert.True(current.Projection.IsAvailable); Assert.True(current.Input.Effects.IsAccepted);
        var context = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
        {
            ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player", ["targetId"] = "player_current",
            ["providerKind"] = "npc", ["providerId"] = providerIds[0], ["currentLocationId"] = "loc_field_clinic_001"
        }.ToJsonString(), "providerNames.context").Context!;
        Assert.NotNull(context);
        var issues = new List<ValidationIssue>();
        var roots = current.CurrentMenuRoots.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var path in MortalWoundTreatmentAcceptedStateAuthority.BuildPathSelection(context, issues))
        {
            if (roots.ContainsKey(path) || path == ResourceMaterializationContract.DefinitionsPath) continue;
            var bytes = await files.ReadFileBytesAsync(lease, path);
            if (bytes is not null) roots.Add(path, StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), path)!);
        }
        issues.AddRange(new ValidationService(files, NullLogger<ValidationService>.Instance).ValidateMortalWoundTreatmentDetachedSources(roots));
        var player = MortalWoundTreatmentAcceptedStateAuthority.ParsePlayerCapabilities(roots, issues);
        var npcs = MortalWoundTreatmentAcceptedStateAuthority.ParseNpcCapabilities(roots[NpcCoreChangesContract.NpcCorePath], issues);
        Assert.Empty(issues);
        var facts = MortalWoundTreatmentAcceptedCanonicalProjection.ComposeMenuData(context, roots, current.Input.Resources, player, npcs);
        Assert.Empty(facts.Issues);
        var snapshot = new MortalWoundTreatmentAuthority.Snapshot(1, "provider_names_current", facts.Items, facts.Resources,
            facts.Actors, facts.Facilities, facts.Locations, facts.Quests,
            MortalWoundTreatmentAcceptedStateAuthority.ComposeRequirementEffects(current.Input.Effects), facts.Environments);
        var wound = Assert.Single(current.Catalog.Occurrences).Wound;
        var parsed = MortalWoundTreatmentContract.ParseProjection(wound.Treatment, "providerNames.treatment", wound.Owner.Realm,
            wound.Owner.OwnerKind, wound.Severity.Rank, wound.Complications, wound.Recovery.DeteriorationPolicy);
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var guaranteed = Assert.IsType<MortalWoundGuaranteedRouteDefinition>(parsed.Treatment!.Routes[0]);
        foreach (var id in providerIds)
        {
            var actor = Assert.Single(facts.Actors, row => row.ActorKind == "npc" && row.ActorId == id);
            Assert.True(actor.Active && actor.Reachable); Assert.Equal("active", actor.Lifecycle);
            Assert.Equal(context.CurrentLocationId, actor.CurrentLocationId);
            var providerContext = context with { ProviderId = id };
            Assert.True(MortalWoundTreatmentAuthority.ResolveRequirements(wound.Treatment.Routes[0], providerContext, snapshot).Success);
            var capability = Assert.Single(npcs.Where(row => row.OwnerKind == "npc" && row.OwnerId == id && row.Active && row.Lifecycle == "active")
                .SelectMany(row => row.Capabilities), row => row.CapabilityRef == guaranteed.Resolution.CapabilityRef);
            Assert.True(MortalWoundTreatmentCapabilityAuthority.CoversGuaranteedOutcome(wound, guaranteed.Outcome.DeclaredResult, capability));
            var method = Assert.Single(wound.Treatment.DiagnosisPaths);
            Assert.True(MortalWoundTreatmentAuthority.ResolveRequirementMembers(method.Requirements, method.SourcePath, providerContext, snapshot).Success);
        }
        Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Null(await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
    }
}
