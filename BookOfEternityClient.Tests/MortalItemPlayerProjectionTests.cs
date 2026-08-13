using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalItemPlayerProjectionTests
{
    [Fact]
    public void ItemProjection_SuppressesAuthorityDtoShapesAndPreservesAdjacentSemantics()
    {
        var source = new JsonObject
        {
            ["serviceInstructions"] = CreateRepairPacket(),
            ["movementRecord"] = CreateTransition(),
            ["placementCoordinate"] = CreateCarrier(),
            ["annotatedPlacementCoordinate"] = CreateAnnotatedCarrier(),
            ["embeddedEnvelope"] = MortalItemTestFixture.CreateCanonicalRoot()["materialization"]!.DeepClone(),
            ["embeddedIdentityIndex"] = CreateIdentityIndex(),
            ["embeddedSourceAuthority"] = new JsonObject
            {
                ["kind"] = "npc_trade_receipt",
                ["authorityId"] = "trade_cycle_private"
            },
            ["annotatedSourceAuthority"] = new JsonObject
            {
                ["kind"] = "npc_trade_receipt",
                ["authorityId"] = "trade_cycle_private",
                ["note"] = "PRIVATE_ANNOTATED_AUTHORITY_NOTE"
            },
            ["legitimateMechanic"] = new JsonObject
            {
                ["kind"] = "ritual",
                ["title"] = "Памятка кузнеца",
                ["turn"] = 3
            },
            ["mixedSemantic"] = new JsonObject
            {
                ["title"] = "Обычная игровая подсказка",
                ["steps"] = new JsonArray("Ударить по наковальне трижды"),
                ["expectedAuthority"] = "PRIVATE_SINGLE_ACCIDENTAL_FIELD"
            },
            ["legitimateWorldState"] = new JsonObject
            {
                ["realm"] = "Мир смертных",
                ["state"] = "ожидание",
                ["sections"] = new JsonArray("кузница", "ворота")
            }
        };

        var projected = Assert.IsType<JsonObject>(MortalItemPlayerProjection.CloneItemSemanticValue(source));

        Assert.Null(projected["serviceInstructions"]);
        Assert.Null(projected["movementRecord"]);
        Assert.Null(projected["placementCoordinate"]);
        Assert.Null(projected["annotatedPlacementCoordinate"]);
        Assert.Null(projected["embeddedEnvelope"]);
        Assert.Null(projected["embeddedIdentityIndex"]);
        Assert.Null(projected["embeddedSourceAuthority"]);
        Assert.Null(projected["annotatedSourceAuthority"]);
        Assert.Equal("ritual", projected["legitimateMechanic"]?["kind"]?.GetValue<string>());
        Assert.Equal("Памятка кузнеца", projected["legitimateMechanic"]?["title"]?.GetValue<string>());
        Assert.Equal(3, projected["legitimateMechanic"]?["turn"]?.GetValue<int>());
        Assert.Equal("Обычная игровая подсказка", projected["mixedSemantic"]?["title"]?.GetValue<string>());
        Assert.Equal(
            "Ударить по наковальне трижды",
            projected["mixedSemantic"]?["steps"]?[0]?.GetValue<string>());
        Assert.Null(projected["mixedSemantic"]?["expectedAuthority"]);
        Assert.Equal("Мир смертных", projected["legitimateWorldState"]?["realm"]?.GetValue<string>());
        Assert.Equal("ожидание", projected["legitimateWorldState"]?["state"]?.GetValue<string>());

        using var document = JsonDocument.Parse(source.ToJsonString());
        var formatted = MortalItemPlayerProjection.FormatSemanticValue(document.RootElement);
        Assert.Contains("Памятка кузнеца", formatted, StringComparison.Ordinal);
        Assert.Contains("Обычная игровая подсказка", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("Служебное задание ремонта предмета", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("transfer", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("player_inventory", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("ownershipAndPlacement", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("active", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalMaterializationProjection_SuppressesRepairWrappersAndPreservesAdjacentSemantics()
    {
        var source = new JsonObject
        {
            ["repairRequest"] = CreateValidationRepairRequest(),
            ["diagnosticReport"] = CreateValidationDiagnosticFailureReport(),
            ["offscreenStorageAuthority"] =
                MortalLocationStorageContentsState.BuildCanonicalRoot(
                    new Dictionary<MortalLocationStorageKey, JsonArray>
                    {
                        [new MortalLocationStorageKey(
                            "loc_private_offscreen",
                            "storage_private_offscreen")] = new JsonArray(
                            CreatePrivateOffscreenItem())
                    }),
            ["legitimateGuidance"] = new JsonObject
            {
                ["title"] = "Следопытская памятка",
                ["steps"] = new JsonArray("Идти вдоль реки"),
                ["reason"] = "Тропа безопаснее ночью"
            }
        };

        var projected = Assert.IsType<JsonObject>(
            MortalItemPlayerProjection.CloneMortalMaterializationSemanticValue(source));

        Assert.Null(projected["repairRequest"]);
        Assert.Null(projected["diagnosticReport"]);
        Assert.Null(projected["offscreenStorageAuthority"]);
        Assert.Equal(
            "Следопытская памятка",
            projected["legitimateGuidance"]?["title"]?.GetValue<string>());
        Assert.Equal(
            "Идти вдоль реки",
            projected["legitimateGuidance"]?["steps"]?[0]?.GetValue<string>());
        Assert.Equal(
            "Тропа безопаснее ночью",
            projected["legitimateGuidance"]?["reason"]?.GetValue<string>());

        using var document = JsonDocument.Parse(source.ToJsonString());
        var formatted = MortalItemPlayerProjection.FormatMortalMaterializationSemanticValue(
            document.RootElement);
        Assert.Contains("Следопытская памятка", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE GM INSTRUCTIONS", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE DIAGNOSTIC REASON", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE OFFSCREEN STORAGE ITEM", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("mortal_location_materialization_governed_field_missing", formatted, StringComparison.Ordinal);
    }

    private static JsonObject CreatePrivateOffscreenItem()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot(
            "itm_private_offscreen_projection");
        item["name"] = "PRIVATE OFFSCREEN STORAGE ITEM";
        MortalItemTestFixture.ResealCanonical(item);
        return item;
    }

    private static JsonObject CreateRepairPacket() =>
        new()
        {
            ["kind"] = "mortal_item_materialization_repair",
            ["priority"] = "critical",
            ["title"] = "Служебное задание ремонта предмета",
            ["targetFiles"] = new JsonArray("game_state/inventory/items.json"),
            ["expectedAuthority"] = new JsonArray("receipt"),
            ["actualEvidence"] = new JsonArray("creationRef"),
            ["steps"] = new JsonArray("Открыть validation_repair_request.json"),
            ["doNotDo"] = new JsonArray("Не изменять item_identity_index.json")
        };

    private static JsonObject CreateTransition() =>
        new()
        {
            ["transitionId"] = "mitrn_private",
            ["kind"] = "transfer",
            ["turn"] = 12,
            ["sourceItemIds"] = new JsonArray("itm_private"),
            ["sourceCarrier"] = CreateCarrier(),
            ["destinationCarrier"] = CreateCarrier(),
            ["quantityBefore"] = 1,
            ["quantityAfter"] = 1,
            ["authorityKind"] = "trade_receipt",
            ["authorityId"] = "receipt_private"
        };

    private static JsonObject CreateCarrier() =>
        new()
        {
            ["kind"] = "player_inventory",
            ["ownerId"] = "player",
            ["containerId"] = null,
            ["containerPath"] = new JsonArray()
        };

    private static JsonObject CreateAnnotatedCarrier()
    {
        var carrier = CreateCarrier();
        carrier["note"] = "PRIVATE_ANNOTATED_CARRIER_NOTE";
        return carrier;
    }

    private static JsonObject CreateIdentityIndex() =>
        new()
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray(
                new JsonObject
                {
                    ["itemId"] = "itm_private",
                    ["receiptId"] = "mirec_private",
                    ["state"] = "active",
                    ["currentCarrier"] = CreateCarrier(),
                    ["originMaterializationIds"] = new JsonArray("mat_private"),
                    ["originCreationRefs"] = new JsonArray("new_item_private"),
                    ["parentItemIds"] = new JsonArray(),
                    ["mergedIntoItemId"] = null,
                    ["transitions"] = new JsonArray(CreateTransition())
                })
        };

    private static JsonObject CreateValidationRepairRequest() =>
        new()
        {
            ["sessionId"] = "session_private",
            ["requestId"] = "request_private",
            ["turnNumber"] = 42,
            ["metadataDiagnosticOnly"] = false,
            ["source"] = "private validation source",
            ["detectedAtUtc"] = "2026-08-12T00:00:00Z",
            ["revalidationAttempt"] = 2,
            ["fullTurnResubmissionRequired"] = true,
            ["gmInstructions"] = "PRIVATE GM INSTRUCTIONS",
            ["summaryGroups"] = new JsonArray("PRIVATE SUMMARY GROUP"),
            ["harnessRepairPackets"] = new JsonArray(CreateRepairPacket()),
            ["resubmissionObligations"] = new JsonArray(
                new JsonObject
                {
                    ["actor"] = "mortal_location:new:locref_private",
                    ["route"] = "current_scene_creation",
                    ["rawCarrier"] = "currentLocationData"
                }),
            ["requiredResubmissionPaths"] = new JsonArray("output/narrative_response.json"),
            ["errors"] = new JsonArray(
                new JsonObject
                {
                    ["code"] = "mortal_location_materialization_governed_field_missing",
                    ["message"] = "PRIVATE VALIDATION MESSAGE",
                    ["actor"] = "mortal_location:new:locref_private",
                    ["expected"] = "complete description",
                    ["actual"] = "missing"
                })
        };

    private static JsonObject CreateValidationDiagnosticFailureReport() =>
        new()
        {
            ["source"] = "private diagnostic source",
            ["detectedAtUtc"] = "2026-08-12T00:00:00Z",
            ["reason"] = "PRIVATE DIAGNOSTIC REASON",
            ["rollbackAvailable"] = true,
            ["summaryGroups"] = new JsonArray("PRIVATE DIAGNOSTIC SUMMARY"),
            ["errors"] = new JsonArray(
                new JsonObject
                {
                    ["code"] = "accepted_turn_invalid_snapshot_baseline",
                    ["message"] = "PRIVATE DIAGNOSTIC MESSAGE",
                    ["actor"] = "mortal_location:index",
                    ["expected"] = "validated baseline",
                    ["actual"] = "missing"
                })
        };
}
