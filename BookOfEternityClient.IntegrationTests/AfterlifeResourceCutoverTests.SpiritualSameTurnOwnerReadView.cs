using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Validates a changed same-turn owner from A despite an invalid physical B.
    /// </summary>
    /// <param name="changedPath">
    /// Changed owner whose physical image is corrupted after the original image is frozen.
    /// </param>
    /// <param name="validA">
    /// Valid current owner image in the signed original attempt.
    /// </param>
    [Theory]
    [InlineData("game_state/world/world_events.json", "{\"worldEventsLog\":[]}")]
    [InlineData("game_state/player/skills_active.json", "{\"activeSkillChanges\":[]}")]
    [InlineData("game_state/npcs/npc_core.json", "{\"NPCsInScene\":[],\"UpdateNPCs\":[]}")]
    [InlineData("game_state/factions/faction_core.json", "{\"factionCoreChanges\":[]}")]
    public async Task SpiritualSameTurnOwnerReadView_RejectsPhysicalOwnerChangeAtFinalGuard(
        string changedPath, string validA)
    {
        const string changedPhysical = "{malformed-owner-B";
        ResourceMaterializationTestContext? context = null;
        var armed = false;
        var frozenTarget = false;
        var changed = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed)
                    return Task.CompletedTask;
                var logicalPath = path.Replace('\\', '/');
                if (logicalPath == changedPath)
                    frozenTarget = true;
                if (frozenTarget && !changed &&
                    logicalPath == LiveTurnPreparationService.PendingTurnSnapshotManifestPath)
                {
                    File.WriteAllText(context!.FileSystem.ResolvePath(changedPath), changedPhysical);
                    changed = true;
                }
                return Task.CompletedTask;
            }
        };
        await using (context = await CreateCompleteConflictFrameContextAsync(
                         hooks, async fixture =>
                         {
                             await SeedOriginalIntakeBaselinesAsync(fixture);
                             if (changedPath == NpcCoreChangesContract.NpcCorePath)
                                 await fixture.WriteExactJsonAsync(changedPath,
                                     "{\"NPCsInScene\":[]}");
                         }))
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            await WriteOriginalIntakeDraftAsync(context);
            await context.WriteExactJsonAsync(changedPath, validA);
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            armed = true;

            var rejected = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);

            Assert.True(frozenTarget);
            Assert.True(changed);
            Assert.Null(rejected.Capture);
            Assert.Contains(rejected.Issues, issue =>
                issue.Code == "spiritual_original_input_changed" && issue.FilePath == changedPath);
            Assert.Equal(changedPhysical, await context.FileSystem.ReadFileAsync(lease, changedPath));
            Assert.Contains(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease),
                issue => issue.FilePath == changedPath &&
                         issue.Code != "spiritual_original_input_changed");
        }
    }

    /// <summary>
    /// Rejects an invalid retained owner even when the physical owner becomes valid before validation.
    /// </summary>
    /// <param name="changedPath">
    /// Exact changed same-turn owner path.
    /// </param>
    /// <param name="invalidA">
    /// Retained owner payload that the applicable validator must reject.
    /// </param>
    /// <param name="validB">
    /// Later physical owner payload that would hide the original error if read.
    /// </param>
    /// <param name="expectedCode">
    /// Diagnostic proving the invalid retained owner was validated.
    /// </param>
    [Theory]
    [InlineData("game_state/player/skills_active.json", "{\"wrong\":[]}",
        "{\"activeSkillChanges\":[]}", "player_contract_missing_allowed_top_level_key")]
    [InlineData("game_state/npcs/npc_core.json", "{\"NPCsInScene\":[],\"wrong\":[]}",
        "{\"NPCsInScene\":[],\"UpdateNPCs\":[]}", "npc_contract_unknown_top_level_key")]
    [InlineData("game_state/factions/faction_core.json", "{\"factions\":\"wrong\"}",
        "{\"factionCoreChanges\":[]}", "faction_materialization_current_authority_unusable")]
    public async Task SpiritualSameTurnOwnerReadView_RejectsInvalidAAfterPhysicalRepair(
        string changedPath, string invalidA, string validB, string expectedCode)
    {
        ResourceMaterializationTestContext? context = null;
        var armed = false;
        var frozenTarget = false;
        var changed = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed)
                    return Task.CompletedTask;
                var logicalPath = path.Replace('\\', '/');
                if (logicalPath == changedPath)
                    frozenTarget = true;
                if (frozenTarget && !changed &&
                    logicalPath == LiveTurnPreparationService.PendingTurnSnapshotManifestPath)
                {
                    File.WriteAllText(context!.FileSystem.ResolvePath(changedPath), validB);
                    changed = true;
                }
                return Task.CompletedTask;
            }
        };
        await using (context = await CreateCompleteConflictFrameContextAsync(
                         hooks, async fixture =>
                         {
                             await SeedOriginalIntakeBaselinesAsync(fixture);
                             if (changedPath == NpcCoreChangesContract.NpcCorePath)
                                 await fixture.WriteExactJsonAsync(changedPath,
                                     "{\"NPCsInScene\":[]}");
                         }))
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            await WriteOriginalIntakeDraftAsync(context);
            await context.WriteExactJsonAsync(changedPath, invalidA);
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            armed = true;

            var rejected = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);

            Assert.True(frozenTarget);
            Assert.True(changed);
            Assert.Null(rejected.Capture);
            Assert.Contains(rejected.Issues, issue => issue.Code == expectedCode &&
                issue.FilePath.StartsWith(changedPath, StringComparison.Ordinal));
            Assert.DoesNotContain(rejected.Issues,
                issue => issue.Code == "spiritual_original_input_changed");
            Assert.Equal(validB, await context.FileSystem.ReadFileAsync(lease, changedPath));
        }
    }

    /// <summary>
    /// Keeps absent and present owner images distinct through the final original freshness guard.
    /// </summary>
    /// <param name="retainedPresent">
    /// Whether the original changed world-event owner exists before the later physical mutation.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpiritualSameTurnOwnerReadView_PreservesWorldEventPresence(bool retainedPresent)
    {
        const string changedPath = "game_state/world/world_events.json";
        const string validOwner = "{\"worldEventsLog\":[]}";
        ResourceMaterializationTestContext? context = null;
        var armed = false;
        var frozenTarget = false;
        var changed = false;
        var authorityReads = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed)
                    return Task.CompletedTask;
                var logicalPath = path.Replace('\\', '/');
                if (retainedPresent && logicalPath == changedPath)
                    frozenTarget = true;
                if (!retainedPresent && logicalPath ==
                    CanonicalResourceOwnerAuthorityComposer.AuthorityPath &&
                    ++authorityReads == 2)
                    frozenTarget = true;
                if (frozenTarget && !changed &&
                    logicalPath == LiveTurnPreparationService.PendingTurnSnapshotManifestPath)
                {
                    var physicalPath = context!.FileSystem.ResolvePath(changedPath);
                    if (retainedPresent)
                        File.Delete(physicalPath);
                    else
                        File.WriteAllText(physicalPath, "{malformed-late-owner");
                    changed = true;
                }
                return Task.CompletedTask;
            }
        };
        await using (context = await CreateCompleteConflictFrameContextAsync(
                         hooks, async fixture =>
                         {
                             await SeedOriginalIntakeBaselinesAsync(fixture);
                             if (!retainedPresent)
                                 await fixture.WriteExactJsonAsync(changedPath, validOwner);
                         }))
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            await WriteOriginalIntakeDraftAsync(context);
            if (retainedPresent)
                await context.WriteExactJsonAsync(changedPath, validOwner);
            else
                File.Delete(context.FileSystem.ResolvePath(changedPath));
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            armed = true;

            var rejected = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);

            Assert.True(frozenTarget);
            Assert.True(changed);
            Assert.Null(rejected.Capture);
            Assert.Contains(rejected.Issues, issue =>
                issue.Code == "spiritual_original_input_changed" && issue.FilePath == changedPath);
            Assert.Equal(retainedPresent ? null : "{malformed-late-owner",
                await context.FileSystem.ReadFileAsync(lease, changedPath));
        }
    }

    /// <summary>
    /// Keeps Mortal and Shining faction companion reads on the retained draft during owner validation.
    /// </summary>
    /// <param name="companionPath">
    /// Current companion path read by one of the two faction materialization families.
    /// </param>
    /// <param name="validA">
    /// Valid companion payload retained by the original attempt.
    /// </param>
    [Theory]
    [InlineData("game_state/factions/faction_chronicles.json", "{\"entries\":[]}")]
    [InlineData("game_state/meta/main_story_saref_state.json", "{}")]
    public async Task SpiritualSameTurnOwnerReadView_DoesNotReopenFactionCompanionB(
        string companionPath, string validA)
    {
        const string factionPath = "game_state/factions/faction_core.json";
        const string physicalB = "{malformed-companion-B";
        ResourceMaterializationTestContext? context = null;
        var armed = false;
        var frozen = false;
        var changed = false;
        var companionReads = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed)
                    return Task.CompletedTask;
                var logicalPath = path.Replace('\\', '/');
                if (logicalPath == companionPath)
                {
                    companionReads++;
                    frozen = true;
                }
                if (frozen && !changed &&
                    logicalPath == LiveTurnPreparationService.PendingTurnSnapshotManifestPath)
                {
                    File.WriteAllText(context!.FileSystem.ResolvePath(companionPath), physicalB);
                    changed = true;
                }
                return Task.CompletedTask;
            }
        };
        await using (context = await CreateCompleteConflictFrameContextAsync(
                         hooks, SeedOriginalIntakeBaselinesAsync))
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            await WriteOriginalIntakeDraftAsync(context);
            await context.WriteExactJsonAsync(factionPath, "{\"factionCoreChanges\":[]}");
            await context.WriteExactJsonAsync(companionPath, validA);
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            armed = true;

            var rejected = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);

            Assert.True(frozen);
            Assert.True(changed);
            Assert.Null(rejected.Capture);
            Assert.Contains(rejected.Issues, issue => issue.Code ==
                "spiritual_original_input_changed" && issue.FilePath == companionPath);
            Assert.Equal(2, companionReads);
            Assert.Equal(physicalB, await context.FileSystem.ReadFileAsync(lease, companionPath));
        }
    }

    /// <summary>
    /// Fails closed when a transitive faction companion is missing from the named draft inventory.
    /// </summary>
    [Fact]
    public async Task SpiritualSameTurnOwnerReadView_RejectsUnregisteredFactionCompanion()
    {
        const string factionPath = "game_state/factions/faction_core.json";
        const string missingPath = "game_state/factions/faction_chronicles.json";
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        var draft = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(capture, "_draftInputs"));
        Assert.Contains(missingPath, draft.PathInventory);
        var keptPaths = draft.PathInventory.Where(path => path != missingPath).ToArray();
        var incomplete = SpiritualOriginalDraftInputs.Create(draft.SessionId, draft.RequestId,
            draft.SnapshotToken, draft.Turn, keptPaths,
            keptPaths.ToDictionary(path => path, draft.ReadImage, StringComparer.Ordinal));
        var method = Assert.IsAssignableFrom<MethodInfo>(typeof(ValidationService).GetMethod(
            "ValidateAndCollectAcceptedEffectOwnerExportsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic));
        var preTurnSources = new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            [factionPath] = null
        };
        var acceptedSources = new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            [factionPath] = JsonNode.Parse("{\"factionCoreChanges\":[]}")
        };
        var issues = new List<ValidationIssue>();

        var task = Assert.IsAssignableFrom<Task>(method.Invoke(context.Validator,
            [preTurnSources, acceptedSources, issues, incomplete]));
        await task;

        Assert.Contains(issues, issue => issue.Code ==
            "spiritual_original_effect_input_unregistered");
    }

    /// <summary>
    /// Keeps NPC scene-link validation on the retained current location after physical B changes its anchor.
    /// </summary>
    [Fact]
    public async Task SpiritualSameTurnOwnerReadView_NpcSceneUsesRetainedLocationAnchor()
    {
        const string npcPath = "game_state/npcs/npc_core.json";
        const string changedPath = "game_state/world/current_location.json";
        const string physicalB = "{\"currentLocationData\":{\"locationId\":null}}";
        ResourceMaterializationTestContext? context = null;
        var armed = false;
        var frozen = false;
        var changed = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed)
                    return Task.CompletedTask;
                var logicalPath = path.Replace('\\', '/');
                if (logicalPath == changedPath)
                    frozen = true;
                if (frozen && !changed &&
                    logicalPath == LiveTurnPreparationService.PendingTurnSnapshotManifestPath)
                {
                    File.WriteAllText(context!.FileSystem.ResolvePath(changedPath), physicalB);
                    changed = true;
                }
                return Task.CompletedTask;
            }
        };
        await using (context = await CreateCompleteConflictFrameContextAsync(
                         hooks, async fixture =>
                         {
                             await SeedOriginalIntakeBaselinesAsync(fixture);
                             var preTurnNpc = MortalActorTestFixtures.CreateNpcCoreRoot(
                                 "npc_original_scene_anchor", IntakeBaselineLocationId, "Исходный брод");
                             await fixture.WriteExactJsonAsync(npcPath, preTurnNpc.ToJsonString());
                         }))
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            await WriteOriginalIntakeDraftAsync(context);
            var npcRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(npcPath));
            npcRoot["UpdateNPCs"] = new JsonArray();
            await context.WriteExactJsonAsync(npcPath, npcRoot.ToJsonString());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            armed = true;

            var rejected = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);

            Assert.True(frozen);
            Assert.True(changed);
            Assert.Null(rejected.Capture);
            Assert.Contains(rejected.Issues, issue => issue.Code ==
                "spiritual_original_input_changed" && issue.FilePath == changedPath);
            Assert.DoesNotContain(rejected.Issues, issue => issue.Code ==
                "current_location_new_scene_missing_initial_id_for_npc_scene");
            Assert.Equal(physicalB, await context.FileSystem.ReadFileAsync(lease, changedPath));
        }
    }
}
