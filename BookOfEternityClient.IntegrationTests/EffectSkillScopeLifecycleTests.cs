using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class EffectSkillScopeLifecycleTests : IAsyncDisposable
{
    private const string ActiveSkillsPath = "game_state/player/skills_active.json";
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string SourceSkillId = "skill_effect_source_001";
    private const string FocusedSkillId = "skill_lockpicking";
    private const string AlternateSkillId = "skill_trap_disarm";
    private readonly string _rootPath;
    private readonly FileSystemManager _fileSystem;

    public EffectSkillScopeLifecycleTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-effect-skill-scope-" + Guid.NewGuid().ToString("N"));
        _fileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance);
        _fileSystem.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task TurnRequestCatalog_LiveHelperPublishesExactPlayerAndNpcUsableCatalog()
    {
        await SeedScopedSkillsAsync(_fileSystem);

        await new LiveTurnPreparationService(_fileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "scope-live",
            RequestId = "scope-live-request",
            TurnNumber = 42,
            CurrentRealm = "Mortal World",
            PlayerAction = "Осмотреть рану.",
            PreGeneratedDices1d20 = new[] { 8 }
        });

        var request = await ReadTurnRequestAsync();
        var catalog = Assert.IsType<JsonObject>(request["effectSkillScopeCatalog"]);

        Assert.True(JsonNode.DeepEquals(CreateExpectedCatalog(), catalog));
    }

    [Fact]
    public async Task TurnRequestCatalog_UnrelatedMalformedEffectCarrierDoesNotSuppressValidCatalog()
    {
        await SeedScopedSkillsAsync(_fileSystem);
        await _fileSystem.WriteFileAtomicAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonArray(new JsonObject
            {
                ["legacyEffect"] = "unrelated malformed carrier"
            }).ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        await new LiveTurnPreparationService(_fileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "scope-live-malformed-carrier",
            RequestId = "scope-live-malformed-carrier-request",
            TurnNumber = 44,
            CurrentRealm = "Mortal World",
            PlayerAction = "Осмотреть рану при повреждённом носителе эффекта.",
            PreGeneratedDices1d20 = new[] { 10 }
        });

        var request = await ReadTurnRequestAsync();
        var catalog = Assert.IsType<JsonObject>(request["effectSkillScopeCatalog"]);

        Assert.True(JsonNode.DeepEquals(CreateExpectedCatalog(), catalog));
    }

    [Fact]
    public async Task TurnRequestCatalog_MalformedSkillRootFailsClosedToExplicitEmptyCatalog()
    {
        await SeedScopedSkillsAsync(_fileSystem);
        await _fileSystem.WriteFileAtomicAsync(ActiveSkillsPath, "{ malformed skill root");

        await new LiveTurnPreparationService(_fileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "scope-live-malformed-skills",
            RequestId = "scope-live-malformed-skills-request",
            TurnNumber = 45,
            CurrentRealm = "Mortal World",
            PlayerAction = "Осмотреть недоступный каталог навыков.",
            PreGeneratedDices1d20 = new[] { 11 }
        });

        var request = await ReadTurnRequestAsync();
        var catalog = Assert.IsType<JsonObject>(request["effectSkillScopeCatalog"]);

        Assert.True(JsonNode.DeepEquals(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["targets"] = new JsonArray()
        }, catalog));
    }

    [Fact]
    public void TurnRequestCatalog_AfterlifeOnlyDirectRequestUsesExplicitEmptyCatalog()
    {
        var request = JsonSerializer.SerializeToNode(
            new TurnRequest { PlayerAction = "Продолжить духовный путь." },
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!.AsObject();

        var catalog = Assert.IsType<JsonObject>(request["effectSkillScopeCatalog"]);

        Assert.True(JsonNode.DeepEquals(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["targets"] = new JsonArray()
        }, catalog));
    }

    [Fact]
    public async Task TurnRequestCatalog_EditedRequestCatalogDoesNotChangeCanonicalScopeAuthority()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition(profile: "roll_modifier");
        definition["components"]![0]!["payload"]!["operations"] = new JsonArray("skill_check");
        definition["components"]![0]!["payload"]!["scope"] = new JsonObject
        {
            ["kind"] = "skill",
            ["skillId"] = "skill_forged"
        };
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.CaptureValidatedPendingSnapshotAsync();

        var request = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            LiveTurnPreparationService.TurnRequestPath));
        request["effectSkillScopeCatalog"] = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["targets"] = new JsonArray(new JsonObject
            {
                ["realm"] = "mortal_world",
                ["kind"] = "player",
                ["targetId"] = "player_current",
                ["skills"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_forged",
                    ["displayName"] = "Подмена"
                })
            })
        };
        await context.WriteJsonAsync(
            LiveTurnPreparationService.TurnRequestPath,
            request);
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["source"] = new JsonObject
        {
            ["kind"] = "skill",
            ["sourceId"] = EffectMaterializationTestContext.MaterializableSkillId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        command["parameters"] = new JsonObject();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        Assert.Contains(
            issues,
            issue => issue.Code == "effect_roll_skill_scope_unavailable");
        Assert.Equal(before, after);
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(context.FileSystem));
    }

    [Fact]
    public async Task FocusedApply_OrdinarySkillPublishesExactScopeAndCarrierIndexAgreement()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedFocusedSkillAuthorityAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateFocusedSkillApplyCommand()));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        AssertNoErrors(issues);
        var handoff = Assert.IsType<AcceptedMechanicsAuthorityTestProbe.CommonHandoff>(
            await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem));
        var prepared = Assert.IsType<AcceptedMechanicsPlan>(handoff.Result.Plan);
        var preparedEffect = Assert.Single(
            Assert.IsType<EffectAcceptedTurnPlan>(prepared.EffectPlan).ActiveEffects);
        AssertFocusedScope(preparedEffect, FocusedSkillId);

        var publishedPlan = Assert.IsType<EffectAcceptedTurnPlan>(
            await context.NormalizeAcceptedEffectsAsync(backups));
        var publishedRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.PlayerPath));
        var publishedEffect = Assert.IsType<JsonObject>(Assert.Single(
            publishedRoot["activeEffects"]!.AsArray()));
        var effectId = publishedEffect["effectId"]!.GetValue<string>();
        AssertFocusedScope(publishedEffect, FocusedSkillId);
        Assert.Equal("skill", publishedEffect["source"]!["kind"]!.GetValue<string>());
        Assert.Equal(SourceSkillId, publishedEffect["source"]!["sourceId"]!.GetValue<string>());
        Assert.Equal(
            effectId,
            Assert.Single(publishedPlan.ActiveEffects)["effectId"]!.GetValue<string>());

        var identityRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectIdentityState.StatePath));
        var identity = Assert.IsType<JsonObject>(Assert.Single(
            identityRoot["entries"]!.AsArray()));
        Assert.Equal(effectId, identity["effectId"]!.GetValue<string>());
        Assert.Equal("player", identity["owner"]!["kind"]!.GetValue<string>());
        Assert.Equal("player_current", identity["owner"]!["ownerId"]!.GetValue<string>());
        Assert.Equal(EffectCarrierCatalog.PlayerPath,
            identity["owner"]!["carrierPath"]!.GetValue<string>());

        var snapshot = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
        Assert.True(snapshot.IsAccepted, Describe(snapshot.Issues));
        AssertFocusedResolution(snapshot, expectedActive: true);
        Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));
    }

    [Theory]
    [InlineData("wrong_owner")]
    [InlineData("final_disable")]
    public async Task FocusedApply_FinalAuthorityRejectsWithoutPartialPublication(
        string scenario)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedFocusedSkillAuthorityAsync(context);
        if (scenario == "wrong_owner")
        {
            await context.WriteJsonAsync(NpcCorePath, new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer",
                    ["activeSkills"] = new JsonArray()
                })
            });
        }
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateFocusedSkillApplyCommand(
                    scenario == "wrong_owner" ? "npc" : "player")));
        if (scenario == "final_disable")
            await SetFocusedSkillAvailabilityAsync(context, active: false);
        var before = await context.CaptureBytesAsync(
            EffectCarrierCatalog.PlayerPath,
            EffectCarrierCatalog.NpcPath,
            EffectIdentityState.StatePath,
            EffectMaterializationTestContext.CommandPath,
            WoundCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            "output/narrative_response.json",
            "output/interface_updates.json");

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, static issue =>
            issue.Code == "effect_roll_skill_scope_unavailable");
        Assert.Equal(before, await context.CaptureBytesAsync(before.Keys.ToArray()));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            context.FileSystem));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(
            context.FileSystem));
    }

    [Fact]
    public async Task FocusedApply_ChangedSelectorAfterValidationRejectsWithoutPublication()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedFocusedSkillAuthorityAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateFocusedSkillApplyCommand()));
        AssertNoErrors(await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync());

        await ChangeFocusedDefinitionSelectorAsync(context, AlternateSkillId);
        var before = await context.CaptureBytesAsync(
            EffectCarrierCatalog.PlayerPath,
            EffectIdentityState.StatePath,
            EffectMaterializationTestContext.CommandPath);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Contains("changed after", exception.Message, StringComparison.Ordinal);
        Assert.Equal(before, await context.CaptureBytesAsync(before.Keys.ToArray()));
    }

    [Fact]
    public async Task FocusedApply_SkillAuthorityChangeInvalidatesFileBackedPlanCache()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedFocusedSkillAuthorityAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateFocusedSkillApplyCommand()));
        var before = await context.CaptureBytesAsync(
            EffectCarrierCatalog.PlayerPath,
            EffectIdentityState.StatePath,
            EffectMaterializationTestContext.CommandPath);

        AssertNoErrors(await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync());
        var first = Assert.IsType<AcceptedMechanicsAuthorityTestProbe.CommonHandoff>(
            await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem));
        var firstPlan = Assert.IsType<AcceptedMechanicsPlan>(first.Result.Plan);
        var firstEffectPlan = Assert.IsType<EffectAcceptedTurnPlan>(firstPlan.EffectPlan);

        AssertNoErrors(await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync());
        var repeated = Assert.IsType<AcceptedMechanicsAuthorityTestProbe.CommonHandoff>(
            await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem));
        var repeatedPlan = Assert.IsType<AcceptedMechanicsPlan>(repeated.Result.Plan);
        var repeatedEffectPlan = Assert.IsType<EffectAcceptedTurnPlan>(repeatedPlan.EffectPlan);
        Assert.Equal(firstPlan.InputFingerprint, repeatedPlan.InputFingerprint);
        Assert.Equal(firstEffectPlan.InputFingerprint, repeatedEffectPlan.InputFingerprint);
        Assert.Equal(
            firstEffectPlan.AllocatedEffectIds,
            repeatedEffectPlan.AllocatedEffectIds);

        await AddUnrelatedSkillAsync(context);
        AssertNoErrors(await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync());
        var changed = Assert.IsType<AcceptedMechanicsAuthorityTestProbe.CommonHandoff>(
            await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem));
        var changedPlan = Assert.IsType<AcceptedMechanicsPlan>(changed.Result.Plan);
        var changedEffectPlan = Assert.IsType<EffectAcceptedTurnPlan>(changedPlan.EffectPlan);
        Assert.NotEqual(firstPlan.InputFingerprint, changedPlan.InputFingerprint);
        Assert.NotEqual(firstEffectPlan.InputFingerprint, changedEffectPlan.InputFingerprint);
        Assert.Equal(before, await context.CaptureBytesAsync(before.Keys.ToArray()));
    }

    [Theory]
    [InlineData("wrong_owner")]
    [InlineData("final_disable")]
    public async Task WoundFocusedApply_FinalAuthorityRejectsWithoutPartialPublication(
        string scenario)
    {
        var prepared = await WoundMaterializationLifecycleTests
            .PrepareFocusedSkillScopedWoundAsync(
                FocusedSkillId,
                includeWrongOwnerSkill: string.Equals(
                    scenario,
                    "wrong_owner",
                    StringComparison.Ordinal));
        await using var context = prepared.Context;
        if (!string.Equals(scenario, "wrong_owner", StringComparison.Ordinal))
        {
            await SetFocusedSkillAvailabilityAsync(context, active: false);
        }
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            prepared.CommandRoot.ToJsonString());
        var before = await context.CaptureAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            WoundCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            EffectCarrierCatalog.PlayerPath,
            EffectIdentityState.StatePath,
            "output/narrative_response.json",
            "output/interface_updates.json");

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        var scopeIssue = Assert.Single(issues, static issue =>
            issue.Code == "wound_materialization_effect_binding_invalid");
        Assert.Equal(
            "woundDecisions[0].proposal.consequenceDefinitions[0].definition." +
            "components[0].payload.scope.skillId",
            scopeIssue.FilePath);
        Assert.NotNull(scopeIssue.WoundRepairContext);
        Assert.NotEmpty(WoundRepairPacketBuilder.Build(issues));
        await context.AssertUnchangedAsync(before);
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            context.FileSystem));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(
            context.FileSystem));
    }

    [Fact]
    public async Task WoundFocusedApply_ChangedSelectorAfterValidationRejectsWithoutPublication()
    {
        var prepared = await WoundMaterializationLifecycleTests
            .PrepareFocusedSkillScopedWoundAsync(FocusedSkillId);
        await using var context = prepared.Context;
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            prepared.CommandRoot.ToJsonString());
        AssertNoErrors(await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync());
        var protectedBefore = await context.CaptureAsync(
            WoundCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            EffectCarrierCatalog.PlayerPath,
            EffectIdentityState.StatePath,
            "output/narrative_response.json",
            "output/interface_updates.json");
        var changed = prepared.CommandRoot.DeepClone().AsObject();
        changed["commands"]![0]!["decision"]!["proposal"]!["consequenceDefinitions"]![0]!
            ["definition"]!["components"]![0]!["payload"]!["scope"]!["skillId"] =
            AlternateSkillId;
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            changed.ToJsonString());

        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            await Assert.ThrowsAnyAsync<Exception>(() => context.Normalizer
                .BindTo(lease)
                .NormalizeAcceptedMechanicsAsync(backups: null));
        }

        await context.AssertUnchangedAsync(protectedBefore);
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            context.FileSystem));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(
            context.FileSystem));
    }

    [Fact]
    public async Task WoundFocusedEffect_RestartAndAvailabilityDeriveActiveDormantDormantActive()
    {
        await using var context = await WoundMaterializationLifecycleTests
            .PublishFocusedSkillScopedWoundAsync(FocusedSkillId);
        var immutablePaths = new[]
        {
            WoundCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            EffectCarrierCatalog.PlayerPath,
            EffectIdentityState.StatePath
        };
        var publishedBytes = await CaptureBytesAsync(context.FileSystem, immutablePaths);
        var woundRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath));
        var wound = Assert.IsType<JsonObject>(Assert.Single(
            woundRoot["activeWounds"]!.AsArray()));
        var woundId = wound["woundId"]!.GetValue<string>();
        var rootBinding = Assert.IsType<JsonObject>(Assert.Single(
            wound["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray()));
        var effectId = rootBinding["effectId"]!.GetValue<string>();
        var effectRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.PlayerPath));
        var effect = effectRoot["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .Single(value => string.Equals(
                value["effectId"]?.GetValue<string>(),
                effectId,
                StringComparison.Ordinal));
        Assert.Equal("wound", effect["source"]!["kind"]!.GetValue<string>());
        Assert.Equal(woundId, effect["source"]!["sourceId"]!.GetValue<string>());
        AssertFocusedScope(effect, FocusedSkillId);

        var restarted = new FileSystemManager(
            context.RootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance);
        AssertFocusedResolution(
            await LoadAcceptedSnapshotAsync(restarted),
            expectedActive: true);

        await WriteCurrentSkillRowsAsync(context, Array.Empty<string>());
        AssertFocusedResolution(
            await LoadAcceptedSnapshotAsync(restarted),
            expectedActive: false);
        Assert.Equal(publishedBytes, await CaptureBytesAsync(restarted, immutablePaths));

        await WriteCurrentSkillRowsAsync(context, new[] { FocusedSkillId.ToUpperInvariant() });
        AssertFocusedResolution(
            await LoadAcceptedSnapshotAsync(restarted),
            expectedActive: false);
        Assert.Equal(publishedBytes, await CaptureBytesAsync(restarted, immutablePaths));

        await WriteCurrentSkillRowsAsync(context, new[] { FocusedSkillId });
        AssertFocusedResolution(
            await LoadAcceptedSnapshotAsync(restarted),
            expectedActive: true);
        Assert.Equal(publishedBytes, await CaptureBytesAsync(restarted, immutablePaths));
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
        return ValueTask.CompletedTask;
    }

    internal static JsonObject CreateFocusedDefinition(string skillId)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("roll_modifier");
        definition["components"]![0]!["payload"] =
            EffectMaterializationTestFixture.CreateFocusedRollModifierPayload(skillId);
        return definition;
    }

    internal static async Task SeedFocusedSkillAuthorityAsync(
        EffectMaterializationTestContext context)
    {
        await context.SeedPlayerSkillSourceAsync(
            CreateFocusedDefinition(FocusedSkillId),
            SourceSkillId);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(ActiveSkillsPath));
        var source = Assert.IsType<JsonObject>(Assert.Single(
            root["activeSkillChanges"]!.AsArray()));
        root["activeSkillChanges"]!.AsArray().Add(
            CreateSelectableSkill(source, FocusedSkillId, "Взлом"));
        root["activeSkillChanges"]!.AsArray().Add(
            CreateSelectableSkill(source, AlternateSkillId, "Обезвреживание ловушек"));
        await context.WriteJsonAsync(ActiveSkillsPath, root);
    }

    private static JsonObject CreateSelectableSkill(
        JsonObject source,
        string skillId,
        string name)
    {
        var skill = source.DeepClone().AsObject();
        skill["skillId"] = skillId;
        skill["skillName"] = name;
        skill["active"] = true;
        skill["lifecycle"] = "active";
        skill["activeEffectDefinitions"] = new JsonArray();
        return skill;
    }

    internal static JsonObject CreateFocusedSkillApplyCommand(
        string targetKind = "player")
    {
        var command = EffectMaterializationTestFixture.CreateApplyCommand(targetKind);
        command["source"] = new JsonObject
        {
            ["kind"] = "skill",
            ["sourceId"] = SourceSkillId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        command["parameters"] = new JsonObject();
        return command;
    }

    private static async Task SetFocusedSkillAvailabilityAsync(
        EffectMaterializationTestContext context,
        bool active)
    {
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(ActiveSkillsPath));
        var selected = root["activeSkillChanges"]!.AsArray()
            .OfType<JsonObject>()
            .Single(value => string.Equals(
                value["skillId"]?.GetValue<string>(),
                FocusedSkillId,
                StringComparison.Ordinal));
        selected["active"] = active;
        selected["lifecycle"] = active ? "active" : "inactive";
        await context.WriteJsonAsync(ActiveSkillsPath, root);
    }

    private static async Task SetFocusedSkillAvailabilityAsync(
        ResourceMaterializationTestContext context,
        bool active)
    {
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(ActiveSkillsPath));
        var selected = root["activeSkillChanges"]!.AsArray()
            .OfType<JsonObject>()
            .Single(value => string.Equals(
                value["skillId"]?.GetValue<string>(),
                FocusedSkillId,
                StringComparison.Ordinal));
        selected["active"] = active;
        selected["lifecycle"] = active ? "active" : "inactive";
        await context.WriteExactJsonAsync(
            ActiveSkillsPath,
            root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static async Task ChangeFocusedDefinitionSelectorAsync(
        EffectMaterializationTestContext context,
        string skillId)
    {
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(ActiveSkillsPath));
        var source = root["activeSkillChanges"]!.AsArray()
            .OfType<JsonObject>()
            .Single(value => string.Equals(
                value["skillId"]?.GetValue<string>(),
                SourceSkillId,
                StringComparison.Ordinal));
        source["activeEffectDefinitions"]![0]!["components"]![0]!["payload"]!["scope"]!["skillId"] =
            skillId;
        await context.WriteJsonAsync(ActiveSkillsPath, root);
    }

    private static async Task AddUnrelatedSkillAsync(
        EffectMaterializationTestContext context)
    {
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(ActiveSkillsPath));
        var template = root["activeSkillChanges"]!.AsArray()
            .OfType<JsonObject>()
            .First();
        root["activeSkillChanges"]!.AsArray().Add(
            CreateSelectableSkill(template, "skill_unrelated_001", "Наблюдательность"));
        await context.WriteJsonAsync(ActiveSkillsPath, root);
    }

    private static void AssertFocusedScope(JsonObject effect, string skillId)
    {
        var component = Assert.IsType<JsonObject>(Assert.Single(
            effect["components"]!.AsArray()));
        Assert.Equal("roll_modifier", component["profile"]!.GetValue<string>());
        var scope = Assert.IsType<JsonObject>(component["payload"]!["scope"]);
        Assert.Equal("skill", scope["kind"]!.GetValue<string>());
        Assert.Equal(skillId, scope["skillId"]!.GetValue<string>());
    }

    private static void AssertFocusedResolution(
        EffectMechanicsSnapshot snapshot,
        bool expectedActive)
    {
        var resolution = EffectRollContributionResolver.Resolve(
            snapshot,
            new EffectRollContext(
                "mortal_world",
                "player",
                "player_current",
                "skill_check",
                FocusedSkillId));
        Assert.True(resolution.IsValid, Describe(resolution.Issues));
        Assert.Equal(expectedActive ? "disadvantage" : "normal", resolution.RollMode);
        Assert.Equal(expectedActive ? 1 : 0, resolution.Contributions.Count);
    }

    private static void AssertNoErrors(IEnumerable<ValidationIssue> issues) =>
        Assert.DoesNotContain(issues, static issue =>
            issue.Severity == IssueSeverity.Error);

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(static issue => issue.ToString()));

    private static async Task<EffectMechanicsSnapshot> LoadAcceptedSnapshotAsync(
        FileSystemManager fileSystem)
    {
        var snapshot = await EffectMechanicsSnapshot.LoadAsync(fileSystem);
        Assert.True(snapshot.IsAccepted, Describe(snapshot.Issues));
        return snapshot;
    }

    private static Task WriteCurrentSkillRowsAsync(
        ResourceMaterializationTestContext context,
        IReadOnlyList<string> skillIds) =>
        context.WriteExactJsonAsync(
            ActiveSkillsPath,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(skillIds.Select(skillId =>
                    (JsonNode)new JsonObject
                    {
                        ["skillId"] = skillId,
                        ["skillName"] = "Проверяемый навык",
                        ["active"] = true,
                        ["lifecycle"] = "active",
                        ["activeEffectDefinitions"] = new JsonArray()
                    }).ToArray()),
                ["removeActiveSkills"] = new JsonArray()
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

    private static async Task<IReadOnlyDictionary<string, string?>> CaptureBytesAsync(
        FileSystemManager fileSystem,
        IEnumerable<string> paths)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            var bytes = await fileSystem.ReadFileBytesAsync(path);
            result[path] = bytes is null ? null : Convert.ToBase64String(bytes);
        }
        return result;
    }

    internal static async Task SeedScopedSkillsAsync(FileSystemManager fileSystem)
    {
        await fileSystem.WriteFileAtomicAsync(ActiveSkillsPath, new JsonObject
        {
            ["activeSkillChanges"] = new JsonArray(
                Skill("skill_lockpicking", "Взлом", active: true),
                Skill("skill_inactive", "Спящий", active: false),
                new JsonObject { ["skillName"] = "Без идентификатора" },
                Skill("skill_duplicate", "Первый дубликат", active: true),
                Skill("skill_duplicate", "Второй дубликат", active: true),
                Skill("skill_confusable", "Первый похожий", active: true),
                Skill("SKILL_CONFUSABLE", "Второй похожий", active: true))
        }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await fileSystem.WriteFileAtomicAsync(NpcCorePath, new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_healer",
                ["activeSkills"] = new JsonArray(
                    Skill("skill_medicine", "Медицина", active: true),
                    Skill("skill_npc_inactive", "Недоступный", active: false))
            })
        }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task<JsonObject> ReadTurnRequestAsync() =>
        (JsonNode.Parse(await _fileSystem.ReadFileAsync(LiveTurnPreparationService.TurnRequestPath)
            ?? throw new InvalidOperationException("turn_request.json is missing.")) as JsonObject)
        ?? throw new InvalidOperationException("turn_request.json is not an object.");

    private static JsonObject Skill(string skillId, string skillName, bool active) => new()
    {
        ["skillId"] = skillId,
        ["skillName"] = skillName,
        ["lifecycle"] = active ? "active" : "inactive",
        ["active"] = active
    };

    internal static JsonObject CreateExpectedCatalog() => new()
    {
        ["schemaVersion"] = 1,
        ["targets"] = new JsonArray(
            new JsonObject
            {
                ["realm"] = "mortal_world",
                ["kind"] = "npc",
                ["targetId"] = "npc_healer",
                ["skills"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_medicine",
                    ["displayName"] = "Медицина"
                })
            },
            new JsonObject
            {
                ["realm"] = "mortal_world",
                ["kind"] = "player",
                ["targetId"] = "player_current",
                ["skills"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_lockpicking",
                    ["displayName"] = "Взлом"
                })
            })
    };

}

public sealed partial class WoundMaterializationLifecycleTests
{
    internal static async Task<ResourceMaterializationTestContext>
        PublishFocusedSkillScopedWoundAsync(string focusedSkillId)
    {
        var scenario = await PrepareFocusedSkillScopedWoundAsync(focusedSkillId);
        var context = scenario.Context;
        try
        {
            await PublishAsync(context, scenario.CommandRoot);
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    internal static async Task<FocusedWoundScopeScenario>
        PrepareFocusedSkillScopedWoundAsync(
            string focusedSkillId,
            FileSystemManagerHooks? hooks = null,
            bool includeWrongOwnerSkill = false)
    {
        var context = await CreatePlayerContextAsync(hooks);
        try
        {
            var focusedSkill = CreateCompleteActiveSkill(focusedSkillId, "Взлом");
            focusedSkill["activeEffectDefinitions"] = new JsonArray();
            await context.WriteExactJsonAsync(
                "game_state/player/skills_active.json",
                new JsonObject
                {
                    ["activeSkillChanges"] = includeWrongOwnerSkill
                        ? new JsonArray()
                        : new JsonArray(focusedSkill),
                    ["removeActiveSkills"] = new JsonArray()
                }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            await context.WriteExactJsonAsync(
                EffectIdentityState.StatePath,
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["entries"] = new JsonArray()
                }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            if (includeWrongOwnerSkill)
            {
                var npcRoot = MortalActorTestFixtures.CreateNpcCoreRoot(
                    actorId: "npc_wrong_owner");
                var npc = Assert.IsType<JsonObject>(Assert.Single(
                    npcRoot["NPCsInScene"]!.AsArray()));
                npc["activeSkills"] = new JsonArray(
                    CreateCompleteActiveSkill(
                        focusedSkillId,
                        "Взлом другого владельца"));
                await context.WriteExactJsonAsync(
                    NpcCoreChangesContract.NpcCorePath,
                    npcRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                await RefreshResourceOwnerAuthorityAsync(context);
            }
            var authority = await CreateSignedAuthorityAsync(
                context,
                maximumSeverityRank: 2);
            var proposal = CreateRepairRoundtripProposal("treatment");
            var consequence = Assert.IsType<JsonObject>(Assert.Single(
                proposal["consequenceDefinitions"]!.AsArray()));
            var definition = EffectMaterializationTestFixture.CreateDefinition("roll_modifier");
            definition["definitionKey"] = "wound_skill_hindrance_root";
            definition["display"]!["name"] = "Боль мешает точным движениям";
            definition["display"]!["description"] =
                "Рана создаёт помеху при использовании выбранного навыка.";
            definition["allowedRealms"] = new JsonArray("mortal_world");
            definition["allowedTargetKinds"] = new JsonArray("player");
            definition["components"]![0]!["payload"] =
                EffectMaterializationTestFixture.CreateFocusedRollModifierPayload(
                    focusedSkillId);
            definition["parameterBounds"] = new JsonObject();
            definition["stacking"]!["stackKey"] = "stack_wound_skill_hindrance_root";
            definition["stacking"]!["policy"] = "independent";
            definition["stacking"]!["maxStacks"] = 1;
            definition["stacking"]!["atMaximum"] = "no_change";
            definition["stacking"]!["refreshMode"] = null;
            definition["stacking"]!["mergeRule"] = null;
            definition["links"] = new JsonArray();
            consequence["definitionRef"] = "skill_hindrance_root";
            consequence["definition"] = definition;
            consequence["root"]!["slots"]![0]!["profileKey"] = "roll_modifier";
            consequence["root"]!["slots"]![0]!["readableSummary"] =
                "Боль мешает применять навык «Взлом».";
            var decision = Decision("materialize", proposal);
            decision["opportunityRef"] = authority.Opportunity.PublicRef;
            var response = Response(decision);
            var composed = WoundResponseInputComposer.Compose(
                authority.Binding,
                new[] { authority.Opportunity },
                response.WoundDecisions,
                response.Response,
                Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.True(composed.Success, Describe(composed.Issues));
            return new FocusedWoundScopeScenario(
                context,
                Assert.IsType<JsonObject>(composed.CommandRoot));
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    private static JsonObject CreateCompleteActiveSkill(string skillId, string skillName) =>
        new()
        {
            ["skillId"] = skillId,
            ["skillName"] = skillName,
            ["skillDescription"] = "Открывает сложные механизмы.",
            ["rarity"] = "Common",
            ["combatEffect"] = new JsonObject
            {
                ["actionName"] = "Взлом",
                ["actionCost"] = "Main",
                ["effects"] = new JsonArray(new JsonObject
                {
                    ["effectType"] = "Damage",
                    ["targetType"] = "Enemy",
                    ["effectDescription"] = "Преодолевает сопротивление механизма.",
                    ["value"] = "45%",
                    ["poiseDamage"] = "0%"
                }),
                ["isActivatedEffect"] = true
            },
            ["scalingCharacteristic"] = "Intelligence",
            ["active"] = true,
            ["lifecycle"] = "active"
        };

    private static async Task RefreshResourceOwnerAuthorityAsync(
        ResourceMaterializationTestContext context)
    {
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: 41,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        Assert.True(resources.IsValid, Describe(resources.Issues));
        var resourceOwners = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            resources.Definitions!,
            context.FileSystem.ReadFileAsync,
            resources.State!,
            resources.History!,
            CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(resourceOwners.IsValid, Describe(resourceOwners.Issues));
        await context.WriteExactJsonAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            resourceOwners.CanonicalAuthorityJson!);
    }
}

internal sealed record FocusedWoundScopeScenario(
    ResourceMaterializationTestContext Context,
    JsonObject CommandRoot);

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task EffectSkillScopeLifecycleTests_FocusedPublicationFailureRollsBackEveryTrackedSurface()
    {
        using var ownedClass = new CleanupOwnedFixture(_rootPath, WriteEffectCutEvidence);
        var probe = new EffectPublicationFailureProbe();
        var prepared = await WoundMaterializationLifecycleTests
            .PrepareFocusedSkillScopedWoundAsync(
                "skill_lockpicking",
                new FileSystemManagerHooks
                {
                    LocalPublicationObserver = probe.Observe
                });
        await using var context = prepared.Context;
        using var ownedContext = new CleanupOwnedFixture(context.RootPath, WriteEffectCutEvidence);
        var engine = CreateGameEngine(fileSystem: context.FileSystem);
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "wound_skill_scope_publication");
        var baseline = await CaptureRollbackTrackedSetAsync(engine, context.FileSystem);
        var woundBefore = await context.CaptureAsync(
            WoundCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            EffectCarrierCatalog.PlayerPath,
            EffectIdentityState.StatePath);
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            prepared.CommandRoot.ToJsonString());
        await context.WriteExactJsonAsync(
            "output/narrative_response.json",
            new JsonObject
            {
                ["response"] = "Непринятое описание раны с точной помехой навыку.",
                ["timestamp"] = "2026-09-05T18:30:00Z"
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            "output/interface_updates.json",
            new JsonObject
            {
                ["dialogueOptions"] = new JsonArray(new JsonObject
                {
                    ["text"] = "Продолжить",
                    ["category"] = "continue"
                }),
                ["timestamp"] = "2026-09-05T18:30:00Z"
            }.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, static issue =>
            issue.Severity == IssueSeverity.Error);
        probe.ArmAfterPublication(
            context.FileSystem,
            WoundHistoryState.HistoryPath);

        Exception? failure;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            failure = await Record.ExceptionAsync(() => context.Normalizer
                .BindTo(lease)
                .NormalizeAcceptedMechanicsAsync(backups: null));
        }
        probe.AssertNormalizationFailure(failure, WriteEffectCutEvidence);
        await context.FileSystem.WriteFileAtomicAsync(
            "game_state/control/validation_diagnostic_failure_report.json",
            "{\"scope\":\"skill_lockpicking\"}");

        var diagnosticBeforeRollback = File.ReadAllBytes(context.FileSystem.ResolvePath(
            "game_state/control/validation_diagnostic_failure_report.json"));

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);

        AssertEffectRollbackRaw(context.FileSystem, baseline, probe.GenerationBefore,
            diagnosticBeforeRollback, WriteEffectCutEvidence);
        Assert.Equal(
            baseline,
            await CaptureRollbackTrackedSetAsync(engine, context.FileSystem));
        await context.AssertUnchangedAsync(woundBefore);
        Assert.False(context.FileSystem.FileExists("output/narrative_response.json"));
        Assert.False(context.FileSystem.FileExists("output/interface_updates.json"));
        Assert.Equal(
            "{\"scope\":\"skill_lockpicking\"}",
            await context.FileSystem.ReadFileAsync(
                "game_state/control/validation_diagnostic_failure_report.json"));

        var effectRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.PlayerPath));
        Assert.Empty(effectRoot["activeEffects"]!.AsArray());
        var identityRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectIdentityState.StatePath));
        Assert.Empty(identityRoot["entries"]!.AsArray());
        var woundRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath));
        Assert.Empty(woundRoot["activeWounds"]!.AsArray());
        var woundIdentityRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundIdentityState.StatePath));
        Assert.Empty(woundIdentityRoot["entries"]!.AsArray());
        var woundHistoryRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundHistoryState.HistoryPath));
        Assert.Empty(woundHistoryRoot["transitions"]!.AsArray());
    }

    [Fact]
    public async Task EffectSkillScopeLifecycleTests_TurnRequestCatalog_OrdinaryGameEnginePublishesCanonicalCatalog()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        await WriteOrdinaryTurnPlayerSkillAsync();
        var preflightIssues = await new ValidationService(
            _fs,
            NullLogger<ValidationService>.Instance).ValidateGameStateAsync(
                IntegrationValidationProfiles.EffectSkillScopeCatalog);
        Assert.True(
            preflightIssues.Count == 0,
            string.Join(Environment.NewLine, preflightIssues.Select(static issue => issue.ToString())));
        var engine = CreateGameEngine(new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]));
        var capturedCatalog = new TaskCompletionSource<JsonObject?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalError = Task.Run(async () =>
        {
            var request = await WaitForTurnRequestAsync();
            var root = JsonNode.Parse(await _fs.ReadFileAsync("input/turn_request.json")
                ?? throw new InvalidOperationException("turn_request.json is missing."))!.AsObject();
            capturedCatalog.TrySetResult(
                root["effectSkillScopeCatalog"] is JsonObject requestedCatalog
                    ? requestedCatalog.DeepClone().AsObject()
                    : null);
            await _fs.WriteFileAtomicAsync("ready/turn_error.json", JsonSerializer.Serialize(new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                timestamp = DateTime.UtcNow.ToString("o"),
                status = "error",
                error = "Test terminal cleanup."
            }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        });

        var processTurn = InvokePrivateTaskAsync(
            engine,
            "ProcessPlayerTurn",
            "Осмотреть рану.",
            null);
        var catalog = await capturedCatalog.Task.WaitAsync(TimeSpan.FromSeconds(35));

        await processTurn;
        await terminalError.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(catalog);
        Assert.True(JsonNode.DeepEquals(CreatePlayerOnlyCatalog(), catalog));
    }

    private async Task WriteOrdinaryTurnPlayerSkillAsync()
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/player/skills_active.json",
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_lockpicking",
                    ["skillName"] = "Взлом",
                    ["skillDescription"] = "Открывает сложные механизмы.",
                    ["rarity"] = "Common",
                    ["combatEffect"] = new JsonObject
                    {
                        ["actionName"] = "Взлом",
                        ["actionCost"] = "Main",
                        ["effects"] = new JsonArray(new JsonObject
                        {
                            ["effectType"] = "Damage",
                            ["targetType"] = "Enemy",
                            ["effectDescription"] = "Преодолевает сопротивление механизма.",
                            ["value"] = "45%",
                            ["poiseDamage"] = "0%"
                        }),
                        ["isActivatedEffect"] = true
                    },
                    ["scalingCharacteristic"] = "Intelligence"
                })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/player/skill_mastery.json",
            new JsonObject
            {
                ["skillMasteryChanges"] = new JsonArray(new JsonObject
                {
                    ["skillName"] = "Взлом",
                    ["newMasteryLevel"] = 1,
                    ["newCurrentMasteryProgress"] = 0,
                    ["newMasteryProgressNeeded"] = 100,
                    ["masteryLeveledUp"] = false
                })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static JsonObject CreatePlayerOnlyCatalog() => new()
    {
        ["schemaVersion"] = 1,
        ["targets"] = new JsonArray(new JsonObject
        {
            ["realm"] = "mortal_world",
            ["kind"] = "player",
            ["targetId"] = "player_current",
            ["skills"] = new JsonArray(new JsonObject
            {
                ["skillId"] = "skill_lockpicking",
                ["displayName"] = "Взлом"
            })
        })
    };
}
