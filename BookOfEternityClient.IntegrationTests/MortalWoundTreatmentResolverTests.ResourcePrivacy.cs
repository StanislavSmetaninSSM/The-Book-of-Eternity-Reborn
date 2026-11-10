using System.Globalization;
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
    [InlineData(false, "target", "owner_visible")]
    [InlineData(true, "target", "owner_visible")]
    [InlineData(false, "provider", "owner_visible")]
    [InlineData(true, "provider", "owner_visible")]
    [InlineData(false, "target", "player_visible")]
    [InlineData(false, "target", "hidden")]
    [InlineData(false, "target", "gm_only")]
    [InlineData(false, "target", "missing")]
    public async Task MortalSelectedTargetResourceRequirementsRespectActualPlayerOwnership(bool self, string role, string visibility)
    {
        const string resourceKey = "private_wound_supply";
        const string resourceName = "Особый личный запас";
        var scenario = CreateScalarCoursePublicationScenario();
        if (!self)
        {
            var before = WoundContractTestData.CreateActiveWound(ownerKind: "npc", ownerId: "field_medic_01", carrierPath: WoundCarrierCatalog.NpcPath);
            before["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().RemoveAt(1);
            before["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray().RemoveAt(1);
            before["consequences"]!["entries"]!.AsArray().RemoveAt(1);
            before["consequences"]!["slotsUsed"] = 1;
            before["treatment"] = scenario.Before["treatment"]!.DeepClone();
            scenario = scenario with { Before = before };
        }
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["requirements"]!.AsArray().Add(ResourceRequirement(701));
        route["resourcePolicy"]!["mutations"]!.AsArray().Add(new JsonObject
        { ["kind"] = "consume_requirement", ["scope"] = "common", ["milestoneOrdinal"] = null, ["requirementIndex"] = 1 });
        route["outcomes"]![0]!["requirements"] = new JsonArray(ResourceRequirement(702));
        var hidden = route.DeepClone().AsObject(); hidden["routeId"] = "privacy_hidden_route"; hidden["visibility"] = "hidden";
        scenario.Before["treatment"]!["routes"]!.AsArray().Add(hidden);
        var diagnosis = WoundContractTestData.CreateMortalDiagnosisPath(false, 30);
        diagnosis["requirements"]!.AsArray().Add(ResourceRequirement(703));
        diagnosis["check"]!["resourcePolicy"]!["mutations"]!.AsArray().Add(new JsonObject
        { ["kind"] = "consume_requirement", ["scope"] = "common", ["milestoneOrdinal"] = null, ["requirementIndex"] = 1 });
        diagnosis["reveals"] = new JsonArray("route:privacy_hidden_route");
        scenario.Before["treatment"]!["diagnosisPaths"] = new JsonArray(diagnosis);
        scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before), SeedCanonicalWoundEffects = true };
        var root = Path.Combine(Path.GetTempPath(), "boe-mortal-resource-privacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var reads = new CanonicalReadActivityAccumulator();
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks { PassiveCanonicalReads = reads });
        try
        {
            files.EnsureDirectoryStructure();
            var targetKind = self ? "player" : "npc";
            var targetId = self ? "player_current" : "field_medic_01";
            var carrierPath = self ? WoundCarrierCatalog.PlayerPath : WoundCarrierCatalog.NpcPath;
            _ = AcceptedStateFixture.SeedCanonicalComponentData(files, scenario, targetKind, targetId, carrierPath, SeedMortalMenuDisplayCarriers);
            if (!self)
            {
                // Initial-world carrier placement only, before any menu/original or retained authority exists.
                var playerEffects = JsonNode.Parse(File.ReadAllText(files.ResolvePath(EffectCarrierCatalog.PlayerPath)))!.AsObject();
                var npcEffects = playerEffects["activeEffects"]!.AsArray().Select(row => row!.DeepClone()).ToArray();
                Assert.NotEmpty(npcEffects);
                Assert.All(npcEffects, row => Assert.Equal("npc", row["target"]!["kind"]!.GetValue<string>()));
                playerEffects["activeEffects"] = new JsonArray();
                File.WriteAllText(files.ResolvePath(EffectCarrierCatalog.PlayerPath), playerEffects.ToJsonString());
                File.WriteAllText(files.ResolvePath(EffectCarrierCatalog.NpcPath), new JsonObject
                {
                    ["schemaVersion"] = 1, ["entries"] = new JsonArray(new JsonObject
                    { ["NPCId"] = targetId, ["activeEffects"] = new JsonArray(npcEffects) })
                }.ToJsonString());
            }
            var parsedDefinitions = ResourceDefinitionCatalog.ParseCanonical(File.ReadAllText(files.ResolvePath(ResourceMaterializationContract.DefinitionsPath)), false);
            Assert.True(parsedDefinitions.IsValid);
            var definitions = parsedDefinitions.Catalog!;
            if (visibility != "missing")
            {
                var raw = definitions.ToCanonicalRoot()["definitions"]!.AsArray().OfType<JsonObject>()
                    .Single(row => row["resourceKey"]!.GetValue<string>() == "energy").DeepClone().AsObject();
                raw.Remove("materialization"); raw["resourceKey"] = resourceKey; raw["displayName"] = resourceName;
                raw["visibility"] = visibility; raw["capacityPolicy"] = new JsonObject { ["kind"] = "instance_fixed" };
                var proposal = ResourceDefinitionCatalog.MaterializeProposal(JsonSerializer.SerializeToElement(raw), definitions,
                    42, "turn_42:privacy_resource", () => new("definition_privacy_supply", "seal_privacy_supply"));
                Assert.True(proposal.IsValid, string.Join(";", proposal.Issues));
                definitions = definitions.With(proposal.Definition!);
                File.WriteAllText(files.ResolvePath(ResourceMaterializationContract.DefinitionsPath), definitions.ToCanonicalJson());
            }
            var state = ResourceStateContract.ParseCanonical(File.ReadAllText(files.ResolvePath(ResourceMaterializationContract.StatePath)), definitions, false);
            var history = ResourceHistoryState.ParseCanonical(File.ReadAllText(files.ResolvePath(ResourceMaterializationContract.HistoryPath)), definitions, false);
            Assert.True(state.IsValid); Assert.True(history.IsValid); Assert.Empty(history.History!.ValidateStateAgreement(state.Ledger!));
            var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions,
                path => Task.FromResult<string?>(File.Exists(files.ResolvePath(path)) ? File.ReadAllText(files.ResolvePath(path)) : null),
                state.Ledger!, history.History!, CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
            Assert.True(authority.IsValid, DescribeIssues(authority.Issues));
            File.WriteAllText(files.ResolvePath(CanonicalResourceOwnerAuthorityComposer.AuthorityPath), authority.CanonicalAuthorityJson);
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                _ = files.GetOrCreateSessionGeneration(lease);
                var context = await WoundPlayerReadService.LoadContextAsync(files, lease, retainMenuInputs: true);
                Assert.NotNull(context); Assert.True(context.Projection.IsAvailable); Assert.True(context.Input.Effects.IsAccepted);
                var occurrence = Assert.Single(context.Catalog.Occurrences);
                Assert.Equal(targetId, occurrence.Wound.Owner.OwnerId);
                Assert.Equal(targetKind, occurrence.Wound.Owner.OwnerKind);
                Assert.True(WoundPlayerProjection.Visible(occurrence.Wound.Display.Visibility));
                Assert.True(context.Input.Identities.TryGetEntry(occurrence.WoundId, out var identity));
                Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(occurrence.Wound), identity.SemanticFingerprint);
                Assert.Empty(context.Input.History.ValidateAgreement(context.Input.Identities, context.Catalog));
                Assert.Contains(context.Input.Effects.Effects, effect => effect.TargetId == targetId && effect.TargetKind == targetKind);
                Assert.Equal(visibility != "missing", context.Input.Resources.TryResolveExact(resourceKey, out _));
                Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                Assert.Null(await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
            }
            var before = CaptureDiagnosisMenuJson(files);
            var priorReads = reads.ReadCount(NpcCoreChangesContract.NpcCorePath);
            var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
            Assert.Equal(1, reads.ReadCount(NpcCoreChangesContract.NpcCorePath) - priorReads);
            var navigation = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
            var target = Assert.Single(navigation.Targets, row => row.Owner.OwnerKind == targetKind && row.Owner.OwnerId == targetId);
            Assert.True(target.Available); Assert.Single(target.Wounds);
            ExplorerCommandResult selected;
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                selected = await WoundApplicationService.SelectTargetAsync(files, lease, navigation, target.Value, "/лечить");
            var menu = Assert.IsType<WoundCommandState>(selected.WoundCommandState);
            Assert.Equal(target.Owner, menu.SelectedTarget!.Owner);
            var card = Assert.Single(selected.Blocks.OfType<UiPanelBlock>(), panel => panel.Title == target.Wounds[0].Name + " — " + target.Wounds[0].Severity);
            var cardLines = card.Blocks.OfType<UiTextBlock>().Select(block => block.Text).ToArray();
            var methodLine = Assert.Single(cardLines, line => line.StartsWith("Известный способ", StringComparison.Ordinal));
            var diagnosisLine = Assert.Single(cardLines, line => line.StartsWith("Диагностика", StringComparison.Ordinal));
            var treatments = menu.Choices.Where(row => row.Selection.MortalTreatment is not null).ToArray();
            var diagnoses = menu.Choices.Where(row => row.Selection.MortalDiagnosis is not null).ToArray();
            Assert.Equal(2, treatments.Length); Assert.Equal(2, diagnoses.Length);
            Assert.All(treatments, row => { Assert.Equal("course", row.Selection.MortalTreatment!.Mode); Assert.Equal(1, row.Selection.MortalTreatment.MilestoneOrdinal); });
            // This is declared-requirement display, not a balance or executable attempt: the added resource has no allocated pool.
            Assert.All(treatments.Concat(diagnoses), row => Assert.False(row.Available));
            AssertDiagnosisMenuJson(before, files);
            var output = JsonSerializer.Serialize(new { selected.Blocks, selected.Prompts });
            foreach (var hiddenText in new[] { resourceKey, "privacy_hidden_route", "field_medic_01", "sha256:", "carrierPath" })
                Assert.DoesNotContain(hiddenText, output, StringComparison.OrdinalIgnoreCase);
            var violations = new List<string>();
            Check("card method", methodLine, 701, role == "target" && self);
            Check("card diagnosis", diagnosisLine, 703, role == "target" && self);
            foreach (var choice in treatments)
            {
                var playerOwner = role == "target" ? self : choice.Selection.MortalTreatment!.Provider.OwnerKind == "player";
                Check("common " + choice.Value, Between(choice.Reason, "Общие условия: ", ". Условия этого этапа:"), 701, playerOwner);
                Check("milestone " + choice.Value, Between(choice.Reason, "Условия этого этапа: ", ". Просмотр"), 702, playerOwner);
                var costs = Between(choice.Reason, "Заявленные расходы на эту попытку: ", ". Исходы списания:");
                Check("common cost " + choice.Value, costs, 701, playerOwner);
                Check("milestone cost " + choice.Value, costs, 702, playerOwner);
            }
            foreach (var choice in diagnoses)
            {
                var playerOwner = role == "target" ? self : choice.Selection.MortalDiagnosis!.Provider.OwnerKind == "player";
                Check("diagnosis requirements " + choice.Value, Between(choice.Reason, "Условия: ", ". Осмотр"), 703, playerOwner);
                Check("diagnosis cost " + choice.Value, Between(choice.Reason, "Расход по условиям: ", ". Списание при"), 703, playerOwner);
            }
            // One aggregate privacy oracle records every related surface after all genuine current-menu premises.
            Assert.Empty(violations);
            AssertDiagnosisMenuJson(before, files);

            void Check(string surface, string text, int quantity, bool owningPlayer)
            {
                var readable = visibility == "player_visible" || visibility == "owner_visible" && owningPlayer;
                if (text.Contains(resourceName, StringComparison.Ordinal) != readable)
                    violations.Add(surface + ": resource name visibility differs");
                if (text.Contains(quantity.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) != readable)
                    violations.Add(surface + ": declared quantity visibility differs");
            }
        }
        finally { Directory.Delete(root, recursive: true); Assert.False(Directory.Exists(root)); }

        JsonObject ResourceRequirement(int quantity) => new()
        { ["kind"] = "resource_quantity", ["resourceRef"] = resourceKey, ["quantity"] = quantity, ["ownerRole"] = role };
        static string Between(string text, string start, string end)
        {
            var from = text.IndexOf(start, StringComparison.Ordinal); Assert.True(from >= 0, start);
            from += start.Length; var to = text.IndexOf(end, from, StringComparison.Ordinal); Assert.True(to >= from, end);
            return text[from..to];
        }
    }
}
