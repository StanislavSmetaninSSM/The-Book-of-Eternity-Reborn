using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public async Task GuidedPhysicalMenuOffersExecutableKnownRouteAfterCanonicalPremises(string mode)
    {
        var scenario = mode switch
        {
            "procedure" => CreateScenario("procedure_normal_uses_lowest_free_die", mode),
            "course" => CreateScalarCoursePublicationScenario(),
            "guaranteed" => CreateScenario("guaranteed_current_capability_proof_stabilizes", mode),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        scenario = scenario with
        {
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };
        var root = Path.Combine(Path.GetTempPath(), "boe-physical-guided-menu-" + Guid.NewGuid().ToString("N"));
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
                    foreach (var (path, value) in new (string, JsonObject)[]
                    {
                        (WoundCarrierCatalog.NpcPath, new() { ["schemaVersion"] = 1, ["entries"] = new JsonArray() }),
                        (WoundCarrierCatalog.EnemiesPath, new() { ["enemiesData"] = new JsonArray() }),
                        (WoundCarrierCatalog.AlliesPath, new() { ["alliesData"] = new JsonArray() }),
                        (WoundCarrierCatalog.AfterlifeProfilesPath, AfterlifeEntityProfileState.CreateDefaultRoot())
                    })
                        if (!File.Exists(initial.ResolvePath(path)))
                            File.WriteAllText(initial.ResolvePath(path), value.ToJsonString());
                });

            WoundMaterializationEnvelope wound;
            MortalWoundTreatmentRouteDefinition route;
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                _ = files.GetOrCreateSessionGeneration(lease);
                var current = await WoundPlayerReadService.LoadContextAsync(files, lease, retainMenuInputs: true);
                Assert.NotNull(current);
                Assert.True(current.Projection.IsAvailable);
                Assert.True(current.Input.Effects.IsAccepted);
                Assert.True(current.Catalog.TryResolveOne(scenario.Before["woundId"]!.GetValue<string>(), out var occurrence));
                wound = occurrence.Wound;
                Assert.Equal("physical", wound.Classification.Domain);
                Assert.Equal("active", wound.Lifecycle);
                Assert.True(WoundPlayerProjection.Visible(wound.Display.Visibility));
                var typed = MortalWoundTreatmentContract.ParseProjection(wound.Treatment, "physicalMenu.treatment",
                    wound.Owner.Realm, wound.Owner.OwnerKind, wound.Severity.Rank, wound.Complications, wound.Recovery.DeteriorationPolicy);
                Assert.True(typed.IsValid, DescribeIssues(typed.Issues));
                Assert.NotNull(typed.Treatment);
                route = Assert.Single(typed.Treatment.Routes);
                Assert.Equal(mode, route.Mode);
                Assert.Contains(route.RouteId, typed.Treatment.KnownRouteIds);
                Assert.NotEqual("gm_only", route.Visibility);
                Assert.DoesNotContain(typed.Treatment.DiagnosisPaths,
                    method => string.Equals(method.DisplayName, route.DisplayName, StringComparison.Ordinal));

                var parsedContext = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
                {
                    ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player",
                    ["targetId"] = "player_current", ["providerKind"] = "npc", ["providerId"] = "field_medic_01",
                    ["currentLocationId"] = "loc_field_clinic_001"
                }.ToJsonString(), "physicalMenu.context");
                Assert.True(parsedContext.IsValid, DescribeIssues(parsedContext.Issues));
                var context = Assert.IsType<MortalWoundTreatmentAuthority.Context>(parsedContext.Context);
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
                var playerSkills = MortalWoundTreatmentAcceptedStateAuthority.ParsePlayerCapabilities(roots, issues);
                var npcSkills = MortalWoundTreatmentAcceptedStateAuthority.ParseNpcCapabilities(roots[NpcCoreChangesContract.NpcCorePath], issues);
                Assert.Empty(issues);
                var facts = MortalWoundTreatmentAcceptedCanonicalProjection.ComposeMenuData(context, roots,
                    current.Input.Resources, playerSkills, npcSkills);
                Assert.Empty(facts.Issues);
                foreach (var (kind, id) in new[] { ("player", "player_current"), ("npc", "field_medic_01") })
                {
                    var actor = Assert.Single(facts.Actors, actor => actor.ActorKind == kind && actor.ActorId == id);
                    Assert.True(actor.Active && actor.Reachable);
                    Assert.Equal("active", actor.Lifecycle);
                    Assert.Equal(context.CurrentLocationId, actor.CurrentLocationId);
                }
                var snapshot = new MortalWoundTreatmentAuthority.Snapshot(1, "physical_menu_data_only", facts.Items,
                    facts.Resources, facts.Actors, facts.Facilities, facts.Locations, facts.Quests,
                    MortalWoundTreatmentAcceptedStateAuthority.ComposeRequirementEffects(current.Input.Effects), facts.Environments);
                var rawRoute = Assert.Single(wound.Treatment.Routes);
                var requirements = MortalWoundTreatmentAuthority.ResolveRequirements(rawRoute, context, snapshot);
                Assert.True(requirements.Success, DescribeIssues(requirements.Issues));
                if (mode == "course")
                {
                    var course = Assert.IsType<MortalWoundCourseRouteDefinition>(route);
                    Assert.Null(wound.Care.ActiveCourseId);
                    var minute = roots[EffectAcceptedTurnInputComposer.WorldTimePath]["currentTimeInMinutes"]!.GetValue<long>();
                    Assert.Equal(0, minute);
                    Assert.Equal(0, course.Milestones[0].AfterMinutes);
                    Assert.True(minute <= checked(course.Milestones[0].AfterMinutes + course.Resolution.MaximumGapMinutes));
                    var dose = JsonSerializer.SerializeToElement(scenario.Before["treatment"]!["routes"]![0]!["outcomes"]![0]!["requirements"])
                        .EnumerateArray().Select(value => value.Clone()).ToArray();
                    Assert.True(MortalWoundTreatmentAuthority.ResolveRequirementMembers(dose,
                        rawRoute.SourcePath + ".outcomes[0]", context, snapshot).Success);
                }
                if (mode == "guaranteed")
                {
                    var guaranteed = Assert.IsType<MortalWoundGuaranteedRouteDefinition>(route);
                    var source = Assert.Single(npcSkills, skill => skill.OwnerId == "field_medic_01" &&
                        skill.Capabilities.Any(capability => capability.CapabilityRef == guaranteed.Resolution.CapabilityRef));
                    Assert.True(source.Active);
                    Assert.Equal("active", source.Lifecycle);
                    var capability = Assert.Single(source.Capabilities,
                        capability => capability.CapabilityRef == guaranteed.Resolution.CapabilityRef);
                    Assert.Equal(1, capability.SchemaVersion);
                    Assert.Equal("physical", capability.WoundDomain);
                    Assert.InRange(wound.Severity.Rank, capability.MinimumSeverityRank, capability.MaximumSeverityRank);
                    Assert.True(capability.OperationLimits.MayStabilize);
                }
                Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                Assert.Null(await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
            }

            var before = CaptureDiagnosisMenuJson(files);
            var paths = new[] { WoundCarrierCatalog.PlayerPath, NpcCoreChangesContract.NpcCorePath,
                EffectAcceptedTurnInputComposer.WorldTimePath, MortalLocationMaterializationContract.CurrentLocationPath };
            var counts = paths.Select(reads.ReadCount).ToArray();
            var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
            var menu = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
            AssertDiagnosisMenuJson(before, files); // Reading and abandoning this form is inert.
            var menuReads = paths.Select((path, index) => new { path, count = reads.ReadCount(path) - counts[index] }).ToArray();
            output.WriteLine(JsonSerializer.Serialize(new { mode, MenuSourceReads = menuReads }));
            Assert.All(menuReads, source => Assert.True(source.count > 0, source.path));
            var self = Assert.Single(menu.Targets, target => target.Owner == wound.Owner);
            Assert.Equal(wound.Owner, menu.Targets[0].Owner);
            Assert.True(self.Available);
            var provider = Assert.Single(menu.Targets, target => target.Owner.OwnerKind == "npc" &&
                target.Owner.OwnerId == "field_medic_01" && target.Owner.CarrierPath == WoundCarrierCatalog.NpcPath);
            Assert.True(provider.Available);
            menu = menu.ForTarget(self);

            // Intended causal RED: all current premises above hold, but the
            // existing Mortal menu has diagnosis choices only. No new selection
            // codec or production execution is introduced by this test scaffold.
            Assert.True(menu.Choices.Any(choice => choice.Available && choice.Selection.WoundId == wound.WoundId &&
                choice.Selection.MortalDiagnosis is null && choice.Name.Contains(route.DisplayName, StringComparison.Ordinal)),
                "missing executable physical route after validated mode premises: " + mode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Assert.False(Directory.Exists(root));
        }
    }
}
