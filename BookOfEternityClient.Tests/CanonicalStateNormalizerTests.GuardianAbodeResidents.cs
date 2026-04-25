using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MergesTransferReceiptUpdatesIntoCanonicalReceipts()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(
            GuardianAbodeResidentState.StatePath,
            new JsonObject
            {
                [GuardianAbodeResidentState.EntriesProperty] = new JsonArray(),
                [GuardianAbodeResidentState.UpdateTransferReceiptsProperty] = new JsonArray
                {
                    new JsonObject
                    {
                        ["requestId"] = "resident_transfer_req_accepted",
                        ["residentId"] = "resident_alpha_1",
                        ["residentName"] = "Лиора",
                        ["sourceGuardianId"] = "guardian_alpha",
                        ["sourceGuardianName"] = "Азалия",
                        ["sourceAbodeId"] = "abode_alpha",
                        ["sourceAbodeName"] = "Лазурная Обитель",
                        ["targetGuardianId"] = "guardian_beta",
                        ["targetGuardianName"] = "Мириэль",
                        ["targetAbodeId"] = "abode_beta",
                        ["targetAbodeName"] = "Сад Перекрёстков",
                        ["status"] = GuardianAbodeResidentState.TransferStatusAccepted,
                        ["transferMode"] = GuardianAbodeResidentState.TransferModeAcceptedTransfer,
                        ["departureHistoryEntryId"] = "hist_departure_1",
                        ["arrivalHistoryEntryId"] = "hist_arrival_1",
                        ["resolvedAtTurn"] = 12,
                        ["resolvedAtUtc"] = "2026-04-16T04:15:00Z"
                    }
                }
            }.ToJsonString());

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse(await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath))!.AsObject();
        Assert.False(root.ContainsKey(GuardianAbodeResidentState.UpdateTransferReceiptsProperty));

        var receipts = Assert.IsType<JsonArray>(root[GuardianAbodeResidentState.TransferReceiptsProperty]);
        var receipt = Assert.Single(receipts.OfType<JsonObject>());
        Assert.Equal("resident_transfer_req_accepted", receipt["requestId"]?.GetValue<string>());
        Assert.Equal(GuardianAbodeResidentState.TransferStatusAccepted, receipt["status"]?.GetValue<string>());
    }
}
