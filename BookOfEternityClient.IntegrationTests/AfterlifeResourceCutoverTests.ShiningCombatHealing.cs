using System.Security.Cryptography;
using System.Text;
using BookOfEternityClient.Models;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData("healed")]
    [InlineData("blocked_worsen")]
    public Task ShiningCombatHealingStagesAndPublishesThroughItsActualPair(string mode) =>
        RunCombatHealingPairAsync(mode, failPublication: false, realm: "shining_abode");

    [Fact]
    public async Task ShiningPublishedOrdinaryDuelOffersItsOriginalKnownWoundForCombatHealing()
    {
        // Missing behavior: the real treatment menu excludes a lawful Shining
        // duel, although the existing ordinary publisher owns its realm and OD.
        ResourceMaterializationTestContext? context = null;
        string? root = null;
        try
        {
            context = await CreateCompleteConflictFrameContextAsync(
                initializeContext: owned =>
                {
                    context = owned;
                    root = owned.RootPath;
                    return Task.CompletedTask;
                },
                captureOriginalSnapshot: PrepareShiningHealingOriginalAsync);
            var originalWound = await ReadCombatHealingWoundAsync(context);
            Assert.Equal("shining_abode", originalWound.Owner.Realm);
            var originalHistory = await context.FileSystem.ReadFileBytesAsync(WoundHistoryState.HistoryPath);
            var originalSchedule = await context.FileSystem.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath);
            await WriteCompleteConflictFrameExchangeAsync(context);
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var plan = await PeekPlanAsync(context);
            foreach (var owner in new[] { ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeConflictSide })
            {
                var points = ResolvePlannedActionPoints(plan, owner);
                Assert.Equal("shining_abode", points.Coordinate.Realm);
                Assert.Equal(3m, points.Current);
            }
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                Assert.Same(plan, await context.Normalizer.BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
            Assert.True(JsonNode.DeepEquals(plan.StateAfterImage,
                await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
            Assert.True(JsonNode.DeepEquals(plan.HistoryAfterImage,
                await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
            var published = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            Assert.False(published.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
            var active = Assert.IsType<JsonObject>(published["activeConflict"]);
            Assert.Equal("Shining Abode", active["realm"]!.GetValue<string>());
            Assert.Equal("strained", active["oppositionSideStrain"]!.GetValue<string>());
            var exchange = Assert.IsType<JsonObject>(Assert.Single(active["exchangeLog"]!.AsArray()));
            Assert.Equal("exchange_conflict_frame_42", exchange["exchangeId"]!.GetValue<string>());
            Assert.Equal(10, exchange["diceAudit"]!["margin"]!.GetValue<int>());
            AssertNoConflictFrameErrors(await context.Validator.ValidateGameStateAsync(
                new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState)));
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(originalWound),
                WoundMaterializationContract.SerializeCanonical(await ReadCombatHealingWoundAsync(context)));
            Assert.Equal(originalHistory, await context.FileSystem.ReadFileBytesAsync(WoundHistoryState.HistoryPath));
            Assert.Equal(originalSchedule, await context.FileSystem.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath));

            // A readable known wound and actual sufficient source must precede
            // the choice oracle, so an unrelated reader refusal cannot be RED.
            var stable = CaptureShiningMenuTree(context.FileSystem.GameSessionPath);
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                var read = Assert.IsType<WoundPlayerReadContext>(
                    await WoundPlayerReadService.LoadContextAsync(context.FileSystem, lease, retainMenuInputs: true));
                Assert.True(read.Projection.IsAvailable);
                Assert.True(read.Input.SelfVisible);
                Assert.Equal("shining_abode", read.Input.Realm);
                Assert.Single(read.Projection.Active);
                var player = read.Input.Carriers.AfterlifeProfiles!["profiles"]!.AsArray().OfType<JsonObject>()
                    .Single(p => p["actorId"]!.GetValue<string>() == "player_soul");
                var prepared = SpiritualHealingResolver.PrepareSource(new(originalWound, originalWound.Owner,
                    player, read.Soul, read.Input.Effects));
                AssertNoConflictFrameErrors(prepared.Issues);
                Assert.Equal(3, Assert.IsType<SpiritualHealingPreparedSource>(prepared.Source).HealingTier);
            }
            var menu = await WoundApplicationService.OpenAsync(context.FileSystem, "/лечить", treatment: true);
            Assert.Equal(stable, CaptureShiningMenuTree(context.FileSystem.GameSessionPath));
            var state = Assert.IsType<WoundCommandState>(menu.WoundCommandState);
            var choice = Assert.Single(state.Choices, c => c.Available && c.Selection.Combat is not null);
            Assert.Equal(originalWound.WoundId, choice.Selection.WoundId);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(originalWound), choice.Selection.ExpectedWoundFingerprint);
            Assert.Equal(2, choice.Selection.Combat!.NextExchangeOrdinal);
            await AssertShiningCombatMenuModeRefusalsAsync(context);
        }
        finally
        {
            if (context is not null) await context.DisposeAsync();
            if (root is not null) Assert.False(Directory.Exists(root));
        }
    }

    private static async Task PrepareShiningHealingOriginalAsync(ResourceMaterializationTestContext context)
    {
        await PrepareShiningCombatBaselineAsync(context, 3, 1, false, ordinaryPressure: true);
        await context.WriteExactBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath,
            await File.ReadAllBytesAsync(Path.Combine(TestRepoPaths.BaseSessionRoot, EffectAcceptedTurnInputComposer.WorldTimePath)));
        await new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance).EnsureInitializedAsync();
        var schedule = Assert.IsType<JsonObject>(await context.ReadJsonAsync(ProgressionScheduleService.SchedulePath));
        Assert.Equal("Shining Abode", schedule["currentRealm"]!.GetValue<string>());
        foreach (var field in new[] { "pendingWorldCycles", "pendingFactionCycles", "pendingChaosSeaCycles",
            "pendingGuardianProjectCycles", "pendingResidentAgencyCycles", "pendingShiningAbodeCycles",
            "pendingShiningFactionCycles", "pendingShiningTradeCycles" }) Assert.Equal(0, schedule[field]!.GetValue<int>());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Shining Abode",
            preGeneratedDices1d20: [15, 5, 12, 8]);
    }

    private static async Task PrepareShiningCombatBaselineAsync(ResourceMaterializationTestContext context,
        int tier, int rank, bool guaranteedOpponent, bool ordinaryPressure)
    {
        // This callback runs before the fixture's only signed snapshot and after
        // its provisional Chaos setup; no signed or published bytes are rewritten.
        var shining = ShiningAbodeState.CreateDefaultState();
        Assert.Equal(ShiningAbodeState.AvailabilityActive, shining["availability"]!.GetValue<string>());
        Assert.Null(shining["preparedIncarnationPackage"]);
        await context.WriteExactJsonAsync(ShiningAbodeState.StatePath, shining.ToJsonString());
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        foreach (var profile in profiles["profiles"]!.AsArray().OfType<JsonObject>())
        {
            profile["realm"] = "Shining Abode";
            foreach (var binding in profile["resourceOwnerBindings"]!.AsArray().OfType<JsonObject>())
                binding["realm"] = "shining_abode";
        }
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/meta/soul_state.json"));
        soul["currentRealm"] = "Shining Abode";
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        conflict["activeConflict"]!["realm"] = "Shining Abode";
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
        await SeedCombatHealingOriginalAsync(context, tier, rank, guaranteedOpponent, realm: "shining_abode");

        profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/meta/soul_state.json"));
        conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        if (ordinaryPressure)
        {
            foreach (var profile in profiles["profiles"]!.AsArray().OfType<JsonObject>()) profile["standardArts"]!["pressure"] = 0;
            soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["pressure"] = 0;
            conflict["activeConflict"]!["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]!["pressure"] = 0;
        }
        AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority(profiles, soul, shining);
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(ResourceBootstrapStateBuilder.BuildPristine().Definitions);
        var (resources, history) = BuildActionPointState(definitions, profiles, conflict["activeConflict"]!.AsObject(), soul, 6m, 6m);
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
        await context.WriteExactJsonAsync(ResourceMaterializationContract.StatePath, resources.ToCanonicalJson());
        await context.WriteExactJsonAsync(ResourceMaterializationContract.HistoryPath, history.ToCanonicalJson());

        var empty = WoundPlayerProjectionTestData.InputInRealm("shining_abode").Carriers;
        foreach (var (path, carrier) in new[]
        {
            (WoundCarrierCatalog.PlayerPath, empty.PlayerWounds), (WoundCarrierCatalog.NpcPath, empty.NpcWounds),
            (WoundCarrierCatalog.EnemiesPath, empty.EnemyCombatants), (WoundCarrierCatalog.AlliesPath, empty.AllyCombatants)
        }) await context.WriteExactJsonAsync(path, carrier!.ToJsonString());
        await WriteComposedAuthorityAsync(context, definitions, resources, history);
    }

    private static JsonObject ChangeShiningCombatMode(JsonObject original, string mode)
    {
        var changed = original.DeepClone().AsObject();
        if (mode == "sealed") changed["availability"] = ShiningAbodeState.AvailabilitySealedUntilNextAscension;
        else if (mode == "malformed") changed["preparedIncarnationPackage"] = "broken package";
        else if (mode == "handoff") changed["preparedIncarnationPackage"] = new JsonObject
        {
            ["selectedCardIds"] = new JsonArray("card_memory"),
            ["selectedCards"] = new JsonArray(new JsonObject
            { ["cardId"] = "card_memory", ["dedupeKey"] = "memory:card_memory", ["sourceType"] = "project",
                ["sourceFactionId"] = "faction_old", ["sourceActorId"] = "project_old", ["effectFamily"] = "memory",
                ["rarity"] = "common", ["displayName"] = "Память Света", ["displaySummary"] = "Сохраняет эхо.", ["effectPayload"] = new JsonObject() }),
            ["generatedFromDraftVersion"] = 1, ["preparedAtTurn"] = 10, ["preparedAtUtc"] = "2026-04-17T00:10:00Z"
        };
        else throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == "handoff") Assert.Equal(ShiningAbodeState.PreparedIncarnationPackageMode.ValidHandoff,
            ShiningAbodeState.GetPreparedIncarnationPackageMode(changed));
        if (mode == "malformed") Assert.Equal(ShiningAbodeState.PreparedIncarnationPackageMode.InvalidFault,
            ShiningAbodeState.GetPreparedIncarnationPackageMode(changed));
        return changed;
    }

    private static async Task AssertShiningCombatStagingRefusalsAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, ProgressionScheduleService scheduler, SpiritualSelfTreatmentSelection selected)
    {
        var initial = CaptureShiningMenuTree(context.FileSystem.GameSessionPath);
        foreach (var mode in new[] { "missing", "sealed", "handoff", "malformed", "soul", "conflict", "schedule" })
        {
            var path = mode switch
            {
                "soul" => "game_state/meta/soul_state.json",
                "conflict" => AfterlifeSpiritualConflictState.StatePath,
                "schedule" => ProgressionScheduleService.SchedulePath,
                _ => ShiningAbodeState.StatePath
            };
            var original = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(lease, path));
            var changed = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(original))!.AsObject();
            var selection = selected;
            try
            {
                if (mode == "missing") context.FileSystem.DeleteFile(lease, path);
                else
                {
                    if (mode is "soul" or "schedule") changed["currentRealm"] = "Chaos Sea";
                    else if (mode == "conflict")
                    {
                        changed["activeConflict"]!["realm"] = "Chaos Sea";
                        selection = selected with { Combat = new(selected.Combat!.ConflictId,
                            SpiritualCombatHealingIntent.ConflictFingerprint(changed["activeConflict"]!.AsObject()), selected.Combat.NextExchangeOrdinal) };
                    }
                    else changed = ChangeShiningCombatMode(changed, mode);
                    await context.FileSystem.WriteFileAtomicAsync(lease, path, changed.ToJsonString());
                }
                var before = CaptureShiningMenuTree(context.FileSystem.GameSessionPath);
                var refused = await AfterlifeWoundTreatmentAuthority.StagePlayerCombatTurnAsync(context.FileSystem, lease, scheduler,
                    "session_resource_materialization", "request_resource_materialization", 42, selection);
                Assert.False(refused.Stage.IsStaged);
                Assert.Equal("spiritual_combat_treatment_source_invalid", refused.Stage.IssueCode);
                Assert.Null(refused.Control);
                Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                Assert.Equal(before, CaptureShiningMenuTree(context.FileSystem.GameSessionPath));
            }
            finally { await context.FileSystem.WriteFileAtomicBytesAsync(lease, path, original); }
            Assert.Equal(initial, CaptureShiningMenuTree(context.FileSystem.GameSessionPath));
        }
    }

    private static async Task AssertShiningCombatReplayRefusalsAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, ProgressionControl control, string snapshotToken, JsonObject intent)
    {
        var initial = CaptureShiningMenuTree(context.FileSystem.GameSessionPath);
        var original = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(
            (await context.FileSystem.ReadFileBytesAsync(lease, ShiningAbodeState.StatePath))!))!.AsObject();
        foreach (var mode in new[] { "missing", "sealed", "handoff", "malformed" })
        {
            var changed = mode == "missing" ? null : Encoding.UTF8.GetBytes(ChangeShiningCombatMode(original, mode).ToJsonString());
            var modeReads = 0;
            var refused = await Assert.ThrowsAsync<InvalidDataException>(() => ProgressionScheduleService.IsPublishedSpiritualCombatCoreAsync(control,
                "session_resource_materialization", "request_resource_materialization", 42, path =>
                {
                    if (path != ShiningAbodeState.StatePath) return context.FileSystem.ReadFileBytesAsync(lease, path);
                    modeReads++;
                    return Task.FromResult<byte[]?>(changed);
                }, snapshotToken, intent));
            Assert.Contains("current realm mode", refused.Message);
            Assert.Equal(1, modeReads);
            Assert.Equal(initial, CaptureShiningMenuTree(context.FileSystem.GameSessionPath));
        }
    }

    private static async Task AssertShiningCombatMenuModeRefusalsAsync(ResourceMaterializationTestContext context)
    {
        var path = ShiningAbodeState.StatePath;
        var bytes = (await context.FileSystem.ReadFileBytesAsync(path))!;
        var original = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes))!.AsObject();
        var initial = CaptureShiningMenuTree(context.FileSystem.GameSessionPath);
        foreach (var mode in new[] { "missing", "sealed", "handoff", "malformed" })
        {
            try
            {
                await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                {
                    if (mode == "missing") context.FileSystem.DeleteFile(lease, path);
                    else await context.FileSystem.WriteFileAtomicAsync(lease, path, ChangeShiningCombatMode(original, mode).ToJsonString());
                }
                var before = CaptureShiningMenuTree(context.FileSystem.GameSessionPath);
                var menu = await WoundApplicationService.OpenAsync(context.FileSystem, "/лечить", treatment: true);
                var state = Assert.IsType<WoundCommandState>(menu.WoundCommandState);
                Assert.DoesNotContain(state.Choices, choice => choice.Available && choice.Selection.Combat is not null);
                Assert.Equal(before, CaptureShiningMenuTree(context.FileSystem.GameSessionPath));
            }
            finally { await context.FileSystem.WriteFileAtomicBytesAsync(path, bytes); }
            Assert.Equal(initial, CaptureShiningMenuTree(context.FileSystem.GameSessionPath));
        }
    }

    private static string CaptureShiningMenuTree(string root) => string.Join("\n",
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(path => Path.GetRelativePath(root, path) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))));
}
