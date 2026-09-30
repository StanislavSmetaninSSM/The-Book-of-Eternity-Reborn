using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private static int _spiritualGameEnginePreparationCount;

    /// <summary>
    /// Gets the number of complete unsigned spiritual fixture preparations in this test process.
    /// </summary>
    internal static int SpiritualGameEnginePreparationCount =>
        Volatile.Read(ref _spiritualGameEnginePreparationCount);

    /// <summary>
    /// Materializes the cached unsigned spiritual baseline before the actual GameEngine captures this fixture's own original.
    /// </summary>
    /// <param name="captureOriginalSnapshot">
    /// Lifecycle callback invoked once per fresh context after base files, spiritual overrides and composed authority are ready.
    /// </param>
    /// <param name="training">
    /// Whether the original conflict has a zero wound ceiling; defaults to a hostile optional wound source.
    /// </param>
    /// <param name="hooks">
    /// Optional hooks on the fresh canonical filesystem used by the callback and subsequent lifecycle; <see langword="null"/> omits hooks.
    /// </param>
    /// <returns>
    /// A disposable context signed by the callback, without an authored exchange or any C2 checkpoint.
    /// </returns>
    internal static async Task<ResourceMaterializationTestContext> CreateSpiritualGameEngineOriginalAsync(
        Func<ResourceMaterializationTestContext, Task> captureOriginalSnapshot, bool training = false,
        FileSystemManagerHooks? hooks = null)
    {
        ArgumentNullException.ThrowIfNull(captureOriginalSnapshot);
        var prepared = await SpiritualGameEngineTemplate.Value;
        var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        try
        {
            prepared.Materialize(context.FileSystem.GameSessionPath);
            if (training)
            {
                var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
                conflict["activeConflict"]!["dangerMode"] = "training";
                await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
            }
            await captureOriginalSnapshot(context);
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Prepares the hostile Chaos Sea scaffold once without invoking a signing callback or retaining its mutable context.
    /// </summary>
    /// <returns>
    /// Detached relative directories and file bytes from the complete unsigned game session.
    /// </returns>
    private static async Task<PreparedFixtureTree> PrepareSpiritualGameEngineTemplateAsync()
    {
        Interlocked.Increment(ref _spiritualGameEnginePreparationCount);
        JsonObject? originalSoul = null;
        ResourceMaterializationTestContext? preparation = null;
        try
        {
            var prepared = await CreateCompleteConflictFrameContextAsync(
                initializeContext: async context =>
                {
                    preparation = context;
                    CopySpiritualGameEngineChaosSeaBaseline(context.FileSystem.GameSessionPath);
                    originalSoul = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/meta/soul_state.json"));
                },
                seedOriginalInputs: async context =>
                {
                    await SeedOriginalIntakeBaselinesAsync(context);
                    var spiritualSoul = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/meta/soul_state.json"));
                    foreach (var pair in spiritualSoul)
                        originalSoul![pair.Key] = pair.Value?.DeepClone();
                    var soulArts = originalSoul![AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!.AsObject();
                    soulArts.Remove("force_binding");
                    soulArts.Remove("recover_spiritual_power");
                    await context.WriteExactJsonAsync("game_state/meta/soul_state.json", originalSoul!.ToJsonString());
                    await CompleteSpiritualGameEngineOriginalScaffoldAsync(context);
                },
                captureOriginalSnapshot: static _ => Task.CompletedTask);
            return CaptureUnsignedSpiritualGameEngineTemplate(prepared.FileSystem);
        }
        finally
        {
            if (preparation != null) await preparation.DisposeAsync();
        }
    }

    /// <summary>
    /// Completes the unchanged original afterlife scaffold required by full lifecycle validation before signing.
    /// </summary>
    /// <param name="context">
    /// Original fixture whose conflict, resource balances and exchange inputs are already established.
    /// </param>
    /// <returns>
    /// A task completing after empty wound authority, realm lore and complete actor profile scaffolds are written.
    /// </returns>
    private static async Task CompleteSpiritualGameEngineOriginalScaffoldAsync(ResourceMaterializationTestContext context)
    {
        var history = new JsonObject
        {
            ["schemaVersion"] = 1, ["nextOrdinal"] = 1, ["transitions"] = new JsonArray()
        };
        var identity = new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray() };
        Assert.True(WoundHistoryState.Parse(history.ToJsonString(), WoundHistoryState.HistoryPath).IsValid);
        Assert.True(WoundIdentityState.Parse(identity.ToJsonString(), WoundIdentityState.StatePath).IsValid);
        await context.WriteExactJsonAsync(WoundHistoryState.HistoryPath, history.ToJsonString());
        await context.WriteExactJsonAsync(WoundIdentityState.StatePath, identity.ToJsonString());
        await context.WriteExactJsonAsync("lore/chaos_sea/guardians_lore.json",
            new JsonObject { ["entries"] = new JsonArray() }.ToJsonString());
        await context.WriteExactJsonAsync("lore/chaos_sea/player_chronicle.json",
            new JsonObject { ["entries"] = new JsonArray() }.ToJsonString());
        await context.WriteExactJsonAsync("lore/chaos_sea/soul_system_lore.json",
            new JsonObject { ["summary"] = "Душа сохраняет опыт, Чернильные Перья и Реликвии Души между жизнями." }.ToJsonString());

        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        foreach (var profile in profiles["profiles"]!.AsArray().OfType<JsonObject>())
        {
            profile["currencies"] = new JsonObject { ["inkFeathers"] = 0, ["lightSparks"] = 0 };
            profile["progression"] = new JsonObject
            {
                ["enlightenment"] = new JsonObject { ["experience"] = 0, ["tier"] = 0 },
                ["radiance"] = new JsonObject { ["experience"] = 0, ["tier"] = 0 }
            };
            profile["soulDissipationTier"] = 0;
            profile["ledger"] = new JsonArray();
            profile["progressionLedger"] = new JsonArray();
            profile["progressionStrategy"] = new JsonObject
            {
                ["strategyId"] = "strategy_" + profile["actorId"]!.GetValue<string>(),
                ["summary"] = "Сохранять текущие духовные силы во время обмена.",
                ["priorityOrder"] = new JsonArray("spiritual_resilience"),
                ["resourceReserve"] = new JsonObject { ["inkFeathers"] = 0, ["lightSparks"] = 0 },
                ["allowedSpends"] = new JsonArray(),
                ["forbiddenSpends"] = new JsonArray("standardArts", "specialArts")
            };
        }
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
    }

    /// <summary>
    /// Authors the existing pressure exchange after the GameEngine has signed the original state and dice.
    /// </summary>
    /// <param name="context">
    /// Signed original direct-duel fixture with both sides starting at six action points.
    /// </param>
    /// <returns>
    /// A task completing after the raw exchange response is written, without applying its resource or wound effects.
    /// </returns>
    internal static Task WriteSpiritualGameEngineExchangeAsync(ResourceMaterializationTestContext context) =>
        WriteCompleteConflictFrameExchangeAsync(context);
}
