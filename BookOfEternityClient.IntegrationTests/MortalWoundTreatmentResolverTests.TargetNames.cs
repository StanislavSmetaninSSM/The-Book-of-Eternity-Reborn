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
    /// <summary>Current reachable NPCs need public distinguishing text, never guessed opaque target tokens.</summary>
    [Theory]
    [InlineData("ambiguous")]
    [InlineData("distinct_names")]
    [InlineData("public_roles")]
    public async Task MortalTargetMenuRefusesIndistinguishableNamesWithoutChangingCanonicalData(string scenarioName)
    {
        var sameName = scenarioName != "distinct_names";
        var publicRoles = scenarioName == "public_roles";
        const string secondId = "field_visitor_02";
        const string firstName = "Алексей";
        var secondName = sameName ? firstName : "Борис";
        var scenario = CreateScenario("guaranteed_current_capability_proof_stabilizes", "guaranteed");
        scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before), SeedCanonicalWoundEffects = true };
        var root = Path.Combine(Path.GetTempPath(), "boe-mortal-target-names-" + Guid.NewGuid().ToString("N"));
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
                    first["name"] = firstName; first["displayName"] = firstName;
                    first["plans"] = "PRIVATE_FIRST_PLAN";
                    var second = MortalActorTestFixtures.CreateActor(secondId, "loc_field_clinic_001", "Field clinic");
                    second["name"] = secondName; second["displayName"] = secondName;
                    second["plans"] = "PRIVATE_SECOND_PLAN";
                    if (publicRoles) { first["role"] = "Лекарь"; second["role"] = "Страж"; }
                    npcs["NPCsInScene"]!.AsArray().Add(second);
                    File.WriteAllText(path, npcs.ToJsonString());
                });

            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                _ = files.GetOrCreateSessionGeneration(lease);
                var current = await WoundPlayerReadService.LoadContextAsync(files, lease, retainMenuInputs: true);
                Assert.NotNull(current);
                Assert.True(current.Projection.IsAvailable);
                Assert.True(current.Input.Effects.IsAccepted);
                var parsed = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
                {
                    ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player",
                    ["targetId"] = "player_current", ["providerKind"] = "player", ["providerId"] = "player_current",
                    ["currentLocationId"] = "loc_field_clinic_001"
                }.ToJsonString(), "mortalTargets.context");
                Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
                var context = Assert.IsType<MortalWoundTreatmentAuthority.Context>(parsed.Context);
                var issues = new List<ValidationIssue>();
                var roots = current.CurrentMenuRoots.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                foreach (var path in MortalWoundTreatmentAcceptedStateAuthority.BuildPathSelection(context, issues))
                {
                    if (roots.ContainsKey(path) || path == ResourceMaterializationContract.DefinitionsPath) continue;
                    var bytes = await files.ReadFileBytesAsync(lease, path);
                    if (bytes is not null)
                        roots.Add(path, StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                            new JsonSerializerOptions(), path) ?? throw new InvalidDataException(path));
                }
                issues.AddRange(new ValidationService(files, NullLogger<ValidationService>.Instance)
                    .ValidateMortalWoundTreatmentDetachedSources(roots));
                var player = MortalWoundTreatmentAcceptedStateAuthority.ParsePlayerCapabilities(roots, issues);
                var npcs = MortalWoundTreatmentAcceptedStateAuthority.ParseNpcCapabilities(roots[NpcCoreChangesContract.NpcCorePath], issues);
                Assert.Empty(issues);
                var facts = MortalWoundTreatmentAcceptedCanonicalProjection.ComposeMenuData(context, roots,
                    current.Input.Resources, player, npcs);
                Assert.Empty(facts.Issues);
                foreach (var (id, name) in new[] { ("field_medic_01", firstName), (secondId, secondName) })
                {
                    var actor = Assert.Single(facts.Actors, actor => actor.ActorKind == "npc" && actor.ActorId == id);
                    Assert.Equal(name, actor.DisplayName);
                    Assert.True(actor.Active && actor.Reachable);
                    Assert.Equal("active", actor.Lifecycle);
                    Assert.Equal(context.CurrentLocationId, actor.CurrentLocationId);
                }
                Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                Assert.Null(await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
            }

            var before = CaptureDiagnosisMenuJson(files);
            var priorReads = reads.ReadCount(NpcCoreChangesContract.NpcCorePath);
            var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
            var menu = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
            AssertDiagnosisMenuJson(before, files);
            Assert.Equal(1, reads.ReadCount(NpcCoreChangesContract.NpcCorePath) - priorReads);
            Assert.True(menu.IsTargetSelection);
            Assert.Equal("Себя", menu.Targets[0].Label);
            Assert.Equal("player_current", menu.Targets[0].Owner.OwnerId);
            Assert.True(menu.Targets[0].Available);
            var targets = menu.Targets.Where(target => target.Owner.OwnerKind == "npc").ToArray();
            Assert.Equal(2, targets.Length);
            Assert.Equal(new[] { "field_medic_01", secondId }, targets.Select(target => target.Owner.OwnerId));
            Assert.NotEqual(targets[0].Value, targets[1].Value);
            var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(opened.Prompts));
            Assert.Equal("wound_target", prompt.Id);
            var serialized = JsonSerializer.Serialize(new { opened.Blocks, opened.Prompts, Targets = menu.Targets });
            foreach (var hidden in new[] { "field_medic_01", secondId, "PRIVATE_FIRST_PLAN", "PRIVATE_SECOND_PLAN", "sha256:", "carrierPath" })
                Assert.DoesNotContain(hidden, serialized, StringComparison.OrdinalIgnoreCase);

            // Only this oracle may establish the causal RED: both actors and the real read above are valid and inert.
            if (sameName && !publicRoles)
            {
                Assert.All(targets, target => Assert.False(target.Available));
                Assert.All(targets, target => Assert.Contains("различить", target.Reason));
                Assert.All(targets, target => Assert.Throws<InvalidDataException>(() => menu.ResolveTarget(target.Value)));
                Assert.All(prompt.Options.Skip(1), option => Assert.True(option.Disabled));
            }
            else
            {
                Assert.All(targets, target => Assert.True(target.Available));
                Assert.NotEqual(targets[0].Label, targets[1].Label);
                Assert.All(targets, target => Assert.Equal(target.Owner, menu.ResolveTarget(target.Value).Owner));
                Assert.All(prompt.Options, option => Assert.False(option.Disabled));
            }
            AssertDiagnosisMenuJson(before, files);
            if (publicRoles)
            {
                Assert.Contains("Лекарь", targets[0].Context);
                Assert.Contains("Страж", targets[1].Context);
                var path = NpcCoreChangesContract.NpcCorePath;
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                {
                    var npcs = JsonNode.Parse((await files.ReadFileAsync(lease, path))!)!.AsObject();
                    npcs["NPCsInScene"]![0]!["role"] = "Проводник";
                    await files.WriteFileAtomicAsync(lease, path, npcs.ToJsonString());
                }
                var changed = CaptureDiagnosisMenuJson(files);
                var reopened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
                var fresh = Assert.IsType<WoundCommandState>(reopened.WoundCommandState);
                var changedTarget = Assert.Single(fresh.Targets, target => target.Owner == targets[0].Owner);
                Assert.True(changedTarget.Available);
                Assert.Contains("Проводник", changedTarget.Context);
                Assert.NotEqual(targets[0].BindingFingerprint, changedTarget.BindingFingerprint);
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    await Assert.ThrowsAsync<InvalidDataException>(() => WoundApplicationService.SelectTargetAsync(
                        files, lease, menu, targets[0].Value, "/лечить"));
                AssertDiagnosisMenuJson(changed, files);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Assert.False(Directory.Exists(root));
        }
    }
}
