using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAfterlifeAdapterTests
{
    private static readonly JsonSerializerOptions ProjectionJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [Fact]
    public async Task AfterlifeProfileProjection_UsesAcceptedVisibleEffectInBrowserAndConsole()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("guardian");
        effect["realm"] = "shining_abode";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["profiles"] = new JsonArray(new JsonObject
                {
                    ["actorType"] = "guardian",
                    ["actorId"] = "afterlife_actor_test",
                    ["displayName"] = "Хранитель Пепельной Печати",
                    ["realm"] = "Shining Abode",
                    ["activeEffects"] = new JsonArray(effect.DeepClone())
                })
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            CreateAfterlifeIdentityIndex(effect));
        await SeedShiningSoulAsync(context);
        var acceptedSnapshot = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
        Assert.True(
            acceptedSnapshot.IsAccepted,
            string.Join(Environment.NewLine, acceptedSnapshot.Issues.Select(static issue =>
                $"{issue.Code} {issue.FilePath}: {issue.Actual}")));
        var directProjection = EffectPlayerProjection.Build(
            new EffectPlayerProjectionInput(acceptedSnapshot));
        Assert.Contains(directProjection.Entries, static entry =>
            string.Equals(entry.Name, "Кровотечение", StringComparison.Ordinal));

        var stateManager = new StateManager(
            context.FileSystem,
            new GameSettings(),
            NullLogger<StateManager>.Instance);
        var result = await ExplorerAfterlifeCombatCommandResultBuilder.TryBuildAsync(
            "/afterlife_profiles",
            stateManager,
            context.FileSystem);

        var completed = Assert.IsType<ExplorerCommandResult>(result);
        var dossier = Assert.IsType<UiEntityDossierBlock>(
            Assert.Single(completed.Blocks.OfType<UiEntityDossierBlock>()));
        var profileCard = Assert.Single(
            Assert.Single(dossier.Sections, static section =>
                string.Equals(section.Id, "visible-afterlife-profiles", StringComparison.Ordinal))
                .Cards);
        Assert.Contains(profileCard.Cards, static card =>
            string.Equals(card.Title, "Кровотечение", StringComparison.Ordinal));
        var payload = JsonSerializer.Serialize(completed, ProjectionJsonOptions);
        Assert.Contains("Кровотечение", payload, StringComparison.Ordinal);
        Assert.Contains("Периодический урон", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("activeEffects", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("wound_test_torn_side", payload, StringComparison.Ordinal);

        var console = new TestExplorerConsole();
        ExplorerCommandResultConsoleRenderer.Render(console, completed);
        Assert.NotEmpty(console.Rendered);
    }

    [Fact]
    public async Task SpiritualConditionProjection_IsSharedAndFailsClosedWhenAuthorityBreaks()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string conflictId = "afterlife_conflict_duration_scene";
        var effect = CreateCanonicalSpiritualConditionEffect(
            "burden",
            "rollMode",
            new JsonObject
            {
                ["mode"] = "uses",
                ["remainingUses"] = 2,
                ["consumingTriggerIds"] = new JsonArray("condition_exchange_consumed")
            });
        effect["display"]!["name"] = "Печать тяжести";
        effect["display"]!["description"] =
            "Печать мешает стороне противника удерживать давление.";
        Assert.True(
            AfterlifeSpiritualConflictState.TryProjectCombatCondition(
                effect,
                out var condition,
                out var projectionReason),
            projectionReason);
        var conflict = CreateCanonicalSpiritualConflict(conflictId);
        conflict["activeConflict"]!["combatConditions"] =
            new JsonArray(condition.DeepClone());
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath,
            conflict);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            CreateAfterlifeIdentityIndex(effect));
        await SeedShiningSoulAsync(context);
        var acceptedSnapshot = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
        Assert.True(
            acceptedSnapshot.IsAccepted,
            string.Join(Environment.NewLine, acceptedSnapshot.Issues.Select(static issue =>
                $"{issue.Code} {issue.FilePath}: {issue.Actual}")));
        var directProjection = EffectPlayerProjection.Build(
            new EffectPlayerProjectionInput(acceptedSnapshot));
        Assert.Contains(directProjection.Entries, static entry =>
            string.Equals(entry.Name, "Печать тяжести", StringComparison.Ordinal));

        var stateManager = new StateManager(
            context.FileSystem,
            new GameSettings(),
            NullLogger<StateManager>.Instance);
        var overview = Assert.IsType<ExplorerCommandResult>(
            await ExplorerAfterlifeCombatCommandResultBuilder.TryBuildAsync(
                "/spiritual_conflict",
                stateManager,
                context.FileSystem,
                includeAdvancedDiagnostics: true));
        var log = Assert.IsType<ExplorerCommandResult>(
            await ExplorerAfterlifeCombatCommandResultBuilder.TryBuildAsync(
                "/spiritual_combat_log",
                stateManager,
                context.FileSystem));
        var conflictDossier = Assert.IsType<UiEntityDossierBlock>(
            Assert.Single(overview.Blocks.OfType<UiEntityDossierBlock>()));
        var conditionSection = Assert.Single(
            conflictDossier.Sections,
            static section => string.Equals(
                section.Id,
                "spiritual-conflict-conditions",
                StringComparison.Ordinal));
        Assert.Contains(conditionSection.Cards, static card =>
            string.Equals(card.Title, "Печать тяжести", StringComparison.Ordinal));
        var acceptedPayload = JsonSerializer.Serialize(
            new[] { overview, log },
            ProjectionJsonOptions);

        Assert.Contains("Печать тяжести", acceptedPayload, StringComparison.Ordinal);
        Assert.Contains("Условие духовного конфликта", acceptedPayload, StringComparison.Ordinal);
        Assert.Contains("rollMode", acceptedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, acceptedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("conditionId", acceptedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("activeEffect", acceptedPayload, StringComparison.OrdinalIgnoreCase);

        var console = new TestExplorerConsole();
        ExplorerCommandResultConsoleRenderer.Render(console, overview);
        Assert.NotEmpty(console.Rendered);

        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            });
        var rejected = Assert.IsType<ExplorerCommandResult>(
            await ExplorerAfterlifeCombatCommandResultBuilder.TryBuildAsync(
                "/spiritual_conflict",
                stateManager,
                context.FileSystem));
        var rejectedPayload = JsonSerializer.Serialize(rejected, ProjectionJsonOptions);

        Assert.Contains(
            EffectPlayerProjection.UnavailableMessage,
            rejectedPayload,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Печать тяжести", rejectedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, rejectedPayload, StringComparison.Ordinal);
    }

    private static JsonObject CreateAfterlifeIdentityIndex(JsonObject effect)
    {
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        var entry = Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()));
        var targetId = effect["target"]!["targetId"]!.GetValue<string>();
        entry["owner"]!["ownerId"] = targetId;
        entry["transitions"]![0]!["transitionId"] =
            effect["chronology"]!["lastTransitionId"]!.GetValue<string>();
        entry["transitions"]![0]!["eventRef"] =
            effect["chronology"]!["createdEventRef"]!.GetValue<string>();
        return index;
    }

    private static Task SeedShiningSoulAsync(EffectMaterializationTestContext context) =>
        context.WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["soulName"] = "Тестовая душа",
                ["currentRealm"] = "Shining Abode",
                ["currentIncarnation"] = 1
            });
}

public sealed partial class ExplorerModeCommandTests
{
    [Fact]
    public async Task TryProcessCommand_SpiritualConflictUsesAcceptedEffectProjection()
    {
        await SeedAcceptedSpiritualConditionProjectionAsync(validIdentity: true);
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() =>
            _explorer.TryProcessCommand("/spiritual_conflict"));
        var rendered = ExtractRenderedText();

        Assert.Null(exception);
        Assert.Contains("Печать тяжести", rendered, StringComparison.Ordinal);
        Assert.Contains("Условие духовного конфликта", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("wound_test_torn_side", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("conditionId", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SpiritualConflictBrokenAuthorityFailsClosedWithoutRawFallback()
    {
        await SeedAcceptedSpiritualConditionProjectionAsync(validIdentity: false);
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() =>
            _explorer.TryProcessCommand("/spiritual_conflict"));
        var rendered = ExtractRenderedText();

        Assert.Null(exception);
        Assert.Contains(EffectPlayerProjection.UnavailableMessage, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Печать тяжести", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_SpiritualActionUsesAcceptedConditionProjection()
    {
        await SeedAcceptedSpiritualConditionProjectionAsync(validIdentity: true);
        await _stateManager.RefreshGameStateAsync();

        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var exception = await Record.ExceptionAsync(async () =>
        {
            var result = await ExplorerLifecycleLocalTurnCommandResultBuilder.TryBuildAsync(
                "/spiritual_action",
                _stateManager,
                _fs,
                validation);
            ExplorerCommandResultConsoleRenderer.Render(_console, Assert.IsType<ExplorerCommandResult>(result));
        });
        var rendered = ExtractRenderedText();

        Assert.Null(exception);
        Assert.Contains("Печать тяжести", rendered, StringComparison.Ordinal);
        Assert.Contains("Условие духовного конфликта", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("conditionId", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("activeEffect", rendered, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedAcceptedSpiritualConditionProjectionAsync(bool validIdentity)
    {
        const string conflictId = "afterlife_conflict_console_projection";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "guardian",
            "afterlife_combat_condition");
        effect["realm"] = "shining_abode";
        effect["target"] = new JsonObject
        {
            ["kind"] = "spiritual_conflict_side",
            ["targetId"] = conflictId + ":opposition"
        };
        effect["display"]!["name"] = "Печать тяжести";
        effect["display"]!["description"] =
            "Печать мешает стороне противника удерживать давление.";
        effect["display"]!["category"] = "condition";
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "turns",
            ["remainingTurns"] = 2,
            ["advancePhase"] = "afterlife_exchange_end"
        };
        var payload = effect["components"]![0]!["payload"]!.AsObject();
        payload["actorId"] = "afterlife_actor_test";
        Assert.True(
            AfterlifeSpiritualConflictState.TryProjectCombatCondition(
                effect,
                out var condition,
                out var projectionReason),
            projectionReason);

        await WriteRawJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["soulName"] = "Тестовая душа",
                ["currentRealm"] = "Shining Abode",
                ["currentIncarnation"] = 1
            }.ToJsonString());
        await WriteRawJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeConflict"] = new JsonObject
                {
                    ["conflictId"] = conflictId,
                    ["realm"] = "Shining Abode",
                    ["sideModel"] = "direct_duel",
                    ["status"] = "active",
                    ["resolutionState"] = "active",
                    ["conflictPosition"] = "contested",
                    ["playerSideStrain"] = "clear",
                    ["oppositionSideStrain"] = "clear",
                    ["playerSide"] = new JsonObject
                    {
                        ["leadContestant"] = new JsonObject
                        {
                            ["actorType"] = "player",
                            ["actorId"] = "player_soul",
                            ["displayName"] = "Тестовая душа"
                        },
                        ["supporters"] = new JsonArray()
                    },
                    ["oppositionSide"] = new JsonObject
                    {
                        ["leadContestant"] = new JsonObject
                        {
                            ["actorType"] = "guardian",
                            ["actorId"] = "afterlife_actor_test",
                            ["displayName"] = "Хранитель печати"
                        },
                        ["supporters"] = new JsonArray()
                    },
                    ["combatConditions"] = new JsonArray(condition.DeepClone()),
                    ["exchangeLog"] = new JsonArray()
                },
                ["recentConflicts"] = new JsonArray()
            }.ToJsonString());
        await WriteRawJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            (validIdentity
                ? CreateDistinctIdentityIndex([effect])
                : new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["entries"] = new JsonArray()
                }).ToJsonString());
    }
}
