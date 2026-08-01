using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ProjectsAfterlifeGlobalFlagUpdates()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeGlobalFlagState.StatePath,
            """
            {
              "schemaVersion": 1,
              "flags": [
                {
                  "flagId": "saref_name_revealed",
                  "category": "saref",
                  "state": "active",
                  "visibility": "visible",
                  "createdAtTurn": 21,
                  "updatedAtTurn": 21,
                  "reason": "Игрок узнал имя Сарефа из четвертого квеста Хранителя.",
                  "evidence": "azalia_saref_q4",
                  "linkedActors": [ "guardian:azalia" ],
                  "linkedChronicles": [ "saref_main_thread" ]
                }
              ],
              "afterlifeGlobalFlagUpdates": [
                {
                  "flagId": "saref_name_revealed",
                  "category": "saref",
                  "state": "active",
                  "visibility": "visible",
                  "updatedAtTurn": 22,
                  "reason": "Игрок сопоставил имя с Крыльями Ангелов.",
                  "evidence": "memory_scene_white_lodge",
                  "linkedActors": [ "guardian:azalia", "faction:wings_of_angels" ],
                  "linkedChronicles": [ "saref_main_thread", "memory_scene_azalia_q4" ],
                  "gmThoughtsSummary": "ГМ фиксирует новый глобальный факт после сцены памяти."
                },
                {
                  "flagId": "wings_route_hidden",
                  "category": "saref",
                  "state": "active",
                  "visibility": "hidden",
                  "createdAtTurn": 22,
                  "updatedAtTurn": 22,
                  "reason": "Маршрут к внешнему кругу Крыльев Ангелов найден, но игрок еще не понимает, что искать.",
                  "evidence": "route_fragment_black_gate",
                  "linkedActors": [ "faction:wings_of_angels" ],
                  "linkedChronicles": [ "saref_main_thread" ],
                  "gmThoughtsSummary": "ГМ сохраняет скрытый флаг без вывода в обычный статус игрока."
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeGlobalFlagState.StatePath))!)!.AsObject();
        Assert.False(root.ContainsKey(AfterlifeGlobalFlagState.UpdateProperty));
        Assert.Equal(1, root["schemaVersion"]?.GetValue<int>());

        var flags = root[AfterlifeGlobalFlagState.FlagsProperty]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Equal(2, flags.Length);

        var sarefFlag = Assert.Single(flags, flag => flag["flagId"]?.GetValue<string>() == "saref_name_revealed");
        Assert.Equal(21, sarefFlag["createdAtTurn"]?.GetValue<int>());
        Assert.Equal(22, sarefFlag["updatedAtTurn"]?.GetValue<int>());
        Assert.Equal("Игрок сопоставил имя с Крыльями Ангелов.", sarefFlag["reason"]?.GetValue<string>());
        Assert.Equal("memory_scene_white_lodge", sarefFlag["evidence"]?.GetValue<string>());

        var hiddenFlag = Assert.Single(flags, flag => flag["flagId"]?.GetValue<string>() == "wings_route_hidden");
        Assert.Equal("hidden", hiddenFlag["visibility"]?.GetValue<string>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ObsoleteUpdateKeepsFlagWithReason()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            AfterlifeGlobalFlagState.StatePath,
            """
            {
              "schemaVersion": 1,
              "flags": [
                {
                  "flagId": "source_of_light_pending_clue",
                  "category": "source_of_light",
                  "state": "active",
                  "visibility": "hidden",
                  "createdAtTurn": 30,
                  "updatedAtTurn": 30,
                  "reason": "Игрок услышал зов Источника Света.",
                  "evidence": "radiance_capstone_hint",
                  "linkedActors": [],
                  "linkedChronicles": [ "source_of_light" ]
                }
              ],
              "afterlifeGlobalFlagUpdates": [
                {
                  "flagId": "source_of_light_pending_clue",
                  "category": "source_of_light",
                  "state": "obsolete",
                  "visibility": "hidden",
                  "updatedAtTurn": 35,
                  "reason": "Источник Света открыт полноценным capstone-контрактом.",
                  "evidence": "pending_source_of_light_capstone:closed",
                  "obsoleteReason": "Флаг заменен canonical Source of Light closure tuple.",
                  "gmThoughtsSummary": "ГМ помечает старую зацепку obsolete вместо удаления."
                }
              ]
            }
            """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync(AfterlifeGlobalFlagState.StatePath))!)!.AsObject();
        var flag = Assert.Single(root[AfterlifeGlobalFlagState.FlagsProperty]!.AsArray().OfType<JsonObject>());
        Assert.Equal("obsolete", flag["state"]?.GetValue<string>());
        Assert.Equal("Флаг заменен canonical Source of Light closure tuple.", flag["obsoleteReason"]?.GetValue<string>());
        Assert.Equal(30, flag["createdAtTurn"]?.GetValue<int>());
        Assert.Equal(35, flag["updatedAtTurn"]?.GetValue<int>());
    }
}
