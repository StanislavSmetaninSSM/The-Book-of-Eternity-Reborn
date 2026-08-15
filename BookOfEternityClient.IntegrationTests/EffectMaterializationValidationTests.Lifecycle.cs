using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    [Fact]
    public async Task Lifecycle_ReapplyStacksExistingIdentityAndConsumesCommand()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);

        var effect = await ReadSinglePlayerEffectAsync(context);
        Assert.Equal(
            EffectMaterializationTestFixture.EffectId,
            effect["effectId"]!.GetValue<string>());
        Assert.Equal(2, effect["stacking"]!["currentStacks"]!.GetValue<int>());
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()));
        var transitions = entry["transitions"]!.AsArray();
        Assert.Equal(3, transitions.Count);
        Assert.Equal("stack", transitions[1]!["kind"]!.GetValue<string>());
        Assert.Equal(
            EffectMaterializationTestFixture.EffectId,
            Assert.Single(transitions[1]!["resultEffectIds"]!.AsArray())!.GetValue<string>());
        Assert.Equal("consume", transitions[2]!["kind"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(
            EffectMaterializationTestContext.CommandPath));
    }

    [Fact]
    public async Task Lifecycle_UntilTimeApplyDerivesDeadlineFromCanonicalWorldTime()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "until_time",
            ["duration"] = 30,
            ["timeAuthority"] = "world_time.currentTimeInMinutes"
        };
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.WriteJsonAsync(
            "game_state/world/world_time.json",
            new JsonObject
            {
                ["year"] = 1,
                ["monthName"] = "Первый месяц",
                ["dayOfMonth"] = 1,
                ["timeOfDay"] = "02:00",
                ["currentTimeInMinutes"] = 120L
            });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);
        var effect = await ReadSinglePlayerEffectAsync(context);
        Assert.Equal("until_time", effect["lifetime"]!["mode"]!.GetValue<string>());
        Assert.Equal(150L, effect["lifetime"]!["deadline"]!.GetValue<long>());
    }

    [Fact]
    public async Task Lifecycle_UntilTimeApplyUsesExactSameTurnWorldTimeOverride()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "until_time",
            ["duration"] = 30,
            ["timeAuthority"] = "world_time.currentTimeInMinutes"
        };
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.WriteJsonAsync(
            "game_state/world/world_time.json",
            new JsonObject
            {
                ["year"] = 1,
                ["monthName"] = "Первый месяц",
                ["dayOfMonth"] = 1,
                ["timeOfDay"] = "02:00",
                ["currentTimeInMinutes"] = 120L
            });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var worldTime = (await context.ReadJsonAsync(
            "game_state/world/world_time.json"))!.AsObject();
        worldTime["setWorldTime"] = new JsonObject
        {
            ["year"] = 1,
            ["monthName"] = "Первый месяц",
            ["dayOfMonth"] = 1,
            ["timeOfDay"] = "05:00",
            ["currentTimeInMinutes"] = 300L
        };
        await context.WriteJsonAsync("game_state/world/world_time.json", worldTime);
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);
        var effect = await ReadSinglePlayerEffectAsync(context);
        Assert.Equal(330L, effect["lifetime"]!["deadline"]!.GetValue<long>());
    }

    [Fact]
    public async Task Lifecycle_UntilTimeApplyRejectsUnresolvedSameTurnWorldTimeOverride()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "until_time",
            ["duration"] = 30,
            ["timeAuthority"] = "world_time.currentTimeInMinutes"
        };
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.WriteJsonAsync(
            "game_state/world/world_time.json",
            new JsonObject
            {
                ["year"] = 1,
                ["monthName"] = "Первый месяц",
                ["dayOfMonth"] = 1,
                ["timeOfDay"] = "02:00",
                ["currentTimeInMinutes"] = 120L
            });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var worldTime = (await context.ReadJsonAsync(
            "game_state/world/world_time.json"))!.AsObject();
        worldTime["setWorldTime"] = new JsonObject
        {
            ["year"] = 1,
            ["monthName"] = "Первый месяц",
            ["dayOfMonth"] = 1,
            ["timeOfDay"] = "05:00"
        };
        await context.WriteJsonAsync("game_state/world/world_time.json", worldTime);
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_plan_lifetime_context_missing");
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Lifecycle_UntilTimeExpiresAtAcceptedCanonicalWorldTimeEquality()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "until_time",
            ["duration"] = 30,
            ["timeAuthority"] = "world_time.currentTimeInMinutes"
        };
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        existing["lifetime"] = new JsonObject
        {
            ["mode"] = "until_time",
            ["deadline"] = 120L
        };
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.WriteJsonAsync(
            "game_state/world/world_time.json",
            new JsonObject
            {
                ["year"] = 1,
                ["monthName"] = "Первый месяц",
                ["dayOfMonth"] = 1,
                ["timeOfDay"] = "02:00",
                ["currentTimeInMinutes"] = 120L
            });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);
        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Empty(player["activeEffects"]!.AsArray());
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        Assert.Equal(
            "expired",
            index["entries"]![0]!["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task Lifecycle_IndependentSourceCreatesOnlyItsBoundedCanonicalSet()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreatePolicyDefinition("independent", maxStacks: 2);
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var first = EffectMaterializationTestFixture.CreateApplyCommand();
        first["eventRef"]!["authorityId"] = "turn_43";
        var second = EffectMaterializationTestFixture.CreateApplyCommand();
        second["eventRef"]!["authorityId"] = "turn_43_effect_2";
        second["reason"] = "Независимый второй экземпляр.";
        var atMaximum = EffectMaterializationTestFixture.CreateApplyCommand();
        atMaximum["eventRef"]!["authorityId"] = "turn_43_effect_3";
        atMaximum["reason"] = "Попытка превысить независимый предел.";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                first,
                second,
                atMaximum));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);
        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        var effects = player["activeEffects"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Equal(2, effects.Length);
        Assert.All(effects, effect =>
        {
            Assert.Equal("independent", effect["stacking"]!["policy"]!.GetValue<string>());
            Assert.Equal(1, effect["stacking"]!["maxStacks"]!.GetValue<int>());
            Assert.Equal(1, effect["stacking"]!["currentStacks"]!.GetValue<int>());
        });
    }

    [Fact]
    public async Task Lifecycle_MergePublishesRegisteredNumericReducerResult()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreatePolicyDefinition(
            "merge",
            maxStacks: 3,
            mergeRule: "sum");
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);
        var effect = await ReadSinglePlayerEffectAsync(context);
        Assert.Equal(2, effect["stacking"]!["currentStacks"]!.GetValue<int>());
        Assert.Equal(
            6d,
            effect["components"]![0]!["payload"]!["amount"]!.GetValue<double>());
    }

    [Fact]
    public async Task Lifecycle_RemoveThenApplySameCoordinateTerminatesOldAndCreatesNewIdentity()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(definition, currentStacks: 2);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var remove = CreateTerminalCommand("remove", "stop_bleeding", "turn_43");
        var apply = EffectMaterializationTestFixture.CreateApplyCommand();
        apply["eventRef"]!["authorityId"] = "turn_43_effect_2";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(remove, apply));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);

        var effect = await ReadSinglePlayerEffectAsync(context);
        var newEffectId = effect["effectId"]!.GetValue<string>();
        Assert.NotEqual(EffectMaterializationTestFixture.EffectId, newEffectId);
        Assert.Equal(1, effect["stacking"]!["currentStacks"]!.GetValue<int>());

        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entries = index["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Equal(2, entries.Length);
        var terminal = Assert.Single(entries, entry =>
            entry["effectId"]!.GetValue<string>() == EffectMaterializationTestFixture.EffectId);
        Assert.Equal("removed", terminal["state"]!.GetValue<string>());
        var terminalTransition = terminal["transitions"]!.AsArray()[^1]!.AsObject();
        Assert.Equal("remove", terminalTransition["kind"]!.GetValue<string>());
        Assert.Empty(terminalTransition["resultEffectIds"]!.AsArray());
        var active = Assert.Single(entries, entry =>
            entry["effectId"]!.GetValue<string>() == newEffectId);
        Assert.Equal("active", active["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task Lifecycle_ReplaceClosesOldIdentityAndCreatesOneReplacement()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreatePolicyDefinition("replace", maxStacks: 1);
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);

        var replacement = await ReadSinglePlayerEffectAsync(context);
        var replacementId = replacement["effectId"]!.GetValue<string>();
        Assert.NotEqual(EffectMaterializationTestFixture.EffectId, replacementId);
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entries = index["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Equal(2, entries.Length);
        var oldEntry = Assert.Single(entries, entry =>
            entry["effectId"]!.GetValue<string>() == EffectMaterializationTestFixture.EffectId);
        Assert.Equal("replaced", oldEntry["state"]!.GetValue<string>());
        var replaceTransition = oldEntry["transitions"]!.AsArray()[^1]!.AsObject();
        Assert.Equal("replace", replaceTransition["kind"]!.GetValue<string>());
        Assert.Equal(
            replacementId,
            Assert.Single(replaceTransition["resultEffectIds"]!.AsArray())!.GetValue<string>());
    }

    [Fact]
    public async Task Lifecycle_RefreshResetsThenAdvancesAtTheAcceptedOwnerTurnPhase()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreatePolicyDefinition(
            "refresh",
            maxStacks: 1,
            refreshMode: "reset");
        var existing = CreateLifecycleEffect(
            definition,
            currentStacks: 1,
            remainingTurns: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);
        var effect = await ReadSinglePlayerEffectAsync(context);
        Assert.Equal(
            EffectMaterializationTestFixture.EffectId,
            effect["effectId"]!.GetValue<string>());
        Assert.Equal(2, effect["lifetime"]!["remainingTurns"]!.GetValue<int>());
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var transitions = index["entries"]![0]!["transitions"]!.AsArray();
        Assert.Equal(
            new[] { "create", "refresh", "consume" },
            transitions.Select(static transition =>
                transition!["kind"]!.GetValue<string>()).ToArray());
    }

    [Fact]
    public async Task Lifecycle_DispelRequiresExactDeclaredCategoryAndLeavesWoundUntouched()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var woundBefore = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerWoundsPath);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateTerminalCommand("dispel", "physical_treatment", "turn_43")));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);
        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Empty(player["activeEffects"]!.AsArray());
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = index["entries"]![0]!.AsObject();
        Assert.Equal("dispelled", entry["state"]!.GetValue<string>());
        var transitions = entry["transitions"]!.AsArray();
        Assert.Equal(
            "dispel",
            transitions[transitions.Count - 1]!["kind"]!.GetValue<string>());
        Assert.Equal(
            woundBefore,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerWoundsPath));
    }

    [Fact]
    public async Task Lifecycle_ManualLifetimeUsesItsExactDeclaredRemovalAuthority()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "manual",
            ["authorities"] = new JsonArray("quest_cleanup")
        };
        definition["removal"]!["manualAuthorities"] = new JsonArray();
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        existing["lifetime"] = definition["lifetime"]!.DeepClone();
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateTerminalCommand("remove", "quest_cleanup", "turn_43")));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);
        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Empty(player["activeEffects"]!.AsArray());
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        Assert.Equal("removed", index["entries"]![0]!["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task Lifecycle_UndeclaredDispelAuthorityFailsWithoutCarrierOrIndexMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateTerminalCommand("dispel", "forged_arcane_word", "turn_43")));
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_lifecycle_terminal_authority_forbidden");
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerEffectsPath,
                EffectMaterializationTestContext.IdentityIndexPath));
    }

    [Fact]
    public async Task Lifecycle_StaleTerminalEffectIdFailsWithoutCarrierOrIndexMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var command = CreateTerminalCommand("remove", "stop_bleeding", "turn_43");
        command["effectId"] = "effect_stale_missing";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_lifecycle_terminal_effect_unresolved");
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerEffectsPath,
                EffectMaterializationTestContext.IdentityIndexPath));
    }

    [Fact]
    public async Task Lifecycle_RetargetedTerminalCommandFailsWithoutCarrierOrIndexMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var command = CreateTerminalCommand("remove", "stop_bleeding", "turn_43");
        command["target"]!["targetId"] = "player_other";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_target_selector_unresolved");
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerEffectsPath,
                EffectMaterializationTestContext.IdentityIndexPath));
    }

    [Fact]
    public async Task Lifecycle_SourceLossExpiresOldBeforeDifferentAcceptedSourceReapplies()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "expire"
        };
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        existing["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["linkKind"] = "wound",
            ["targetId"] = "wound_test_torn_side",
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "expire"
        };
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        const string replacementSourceId = "wound_test_new_source";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            new JsonArray(new JsonObject
            {
                ["woundId"] = replacementSourceId,
                ["woundName"] = "Новая рана",
                ["severity"] = "moderate",
                ["description"] = "Новый независимый источник эффекта.",
                ["activeEffectDefinitions"] = new JsonArray(definition.DeepClone())
            }));
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["source"]!["sourceId"] = replacementSourceId;
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        await context.Normalizer.NormalizeEffectsAsync(backups);
        var effect = await ReadSinglePlayerEffectAsync(context);
        Assert.Equal(
            replacementSourceId,
            effect["source"]!["sourceId"]!.GetValue<string>());
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entries = index["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Equal(2, entries.Length);
        var old = Assert.Single(entries, entry =>
            entry["effectId"]!.GetValue<string>() == EffectMaterializationTestFixture.EffectId);
        Assert.Equal("expired", old["state"]!.GetValue<string>());
        var oldTransitions = old["transitions"]!.AsArray();
        Assert.Equal(
            "expire",
            oldTransitions[oldTransitions.Count - 1]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public async Task Lifecycle_DueOwnerTurnExpiryNeedsNoGmCommand()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(
            definition,
            currentStacks: 1,
            remainingTurns: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        AssertNoEffectErrors(issues);
        var plan = await context.Normalizer.NormalizeEffectsAsync(backups);
        Assert.NotNull(plan);
        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Empty(player["activeEffects"]!.AsArray());
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()));
        Assert.Equal("expired", entry["state"]!.GetValue<string>());
        var transition = entry["transitions"]!.AsArray()[^1]!.AsObject();
        Assert.Equal("expire", transition["kind"]!.GetValue<string>());
        Assert.Equal(
            "turn_43:lifecycle:owner_turn_end:player_current:" +
            EffectMaterializationTestFixture.EffectId,
            transition["eventRef"]!.GetValue<string>());
    }

    [Fact]
    public async Task Lifecycle_ValidatedPlanCannotCrossSessionReplacement()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(
            definition,
            currentStacks: 1,
            remainingTurns: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();
        AssertNoEffectErrors(issues);

        var request = (await context.ReadJsonAsync(
            "input/turn_request.json"))!.AsObject();
        request["sessionId"] = "session_effect_materialization_replaced";
        await context.WriteJsonAsync("input/turn_request.json", request);
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var plan = await context.Normalizer.NormalizeEffectsAsync(backups);

        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Null(plan);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Lifecycle_HistoricalEventReplayFailsBeforePublication()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        existing["chronology"]!["createdEventRef"] = "turn_43:accepted_effect";
        await SeedLifecycleStateAsync(context, definition, existing);
        var identity = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        identity["entries"]![0]!["transitions"]![0]!["eventRef"] =
            "turn_43:accepted_effect";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            identity);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_lifecycle_event_replay");
    }

    private static async Task SeedLifecycleStateAsync(
        EffectMaterializationTestContext context,
        JsonObject definition,
        JsonObject effect)
    {
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
    }

    private static JsonObject CreateLifecycleEffect(
        JsonObject definition,
        int currentStacks,
        int remainingTurns = 3)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["display"] = definition["display"]!.DeepClone();
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        effect["removal"] = definition["removal"]!.DeepClone();
        effect["links"] = definition["links"]!.DeepClone();
        var stacking = definition["stacking"]!.DeepClone().AsObject();
        stacking.Remove("atMaximum");
        stacking["currentStacks"] = currentStacks;
        if (stacking["policy"]!.GetValue<string>() == "independent")
            stacking["maxStacks"] = 1;
        effect["stacking"] = stacking;
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "turns",
            ["remainingTurns"] = remainingTurns,
            ["advancePhase"] = "owner_turn_end"
        };
        return effect;
    }

    private static JsonObject CreatePolicyDefinition(
        string policy,
        int maxStacks,
        string? refreshMode = null,
        string? mergeRule = null)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["stacking"] = new JsonObject
        {
            ["stackKey"] = "bleeding",
            ["policy"] = policy,
            ["maxStacks"] = maxStacks,
            ["atMaximum"] = "no_change",
            ["refreshMode"] = refreshMode,
            ["mergeRule"] = mergeRule
        };
        return definition;
    }

    private static JsonObject CreateTerminalCommand(
        string operation,
        string authorityKind,
        string eventAuthorityId) =>
        new()
        {
            ["operation"] = operation,
            ["effectId"] = EffectMaterializationTestFixture.EffectId,
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["authority"] = new JsonObject
            {
                ["kind"] = authorityKind,
                ["authorityId"] = $"authority_{eventAuthorityId}"
            },
            ["eventRef"] = new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = eventAuthorityId
            },
            ["reason"] = "Эффект завершён точным разрешённым действием."
        };

    private static async Task<JsonObject> ReadSinglePlayerEffectAsync(
        EffectMaterializationTestContext context)
    {
        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        return Assert.IsType<JsonObject>(Assert.Single(
            player["activeEffects"]!.AsArray()));
    }

    private static void AssertNoEffectErrors(
        IReadOnlyList<ValidationIssue> issues) =>
        Assert.True(
            !issues.Any(issue => issue.Severity == IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
}
