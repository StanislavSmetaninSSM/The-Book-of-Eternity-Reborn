using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record SpiritualServiceTreatmentSelection(AfterlifeHealingServiceQuote Quote, bool PlayerConfirmed = false);

internal sealed partial record SpiritualSelfTreatmentSelection
{
    private JsonObject ServiceJson()
    {
        if (Aid is not null || Combat is not null || Service is not { } service)
            throw new InvalidDataException("Public service cannot mix treatment families.");
        var quote = AfterlifeHealingServiceContract.ParseQuote(service.Quote.ToJson());
        if (quote.WoundId != WoundId || quote.WoundFingerprint != ExpectedWoundFingerprint)
            throw new InvalidDataException("Service quote belongs to another wound.");
        return new()
        {
            ["schemaVersion"] = 7, ["mode"] = "service", ["woundId"] = WoundId,
            ["expectedWoundFingerprint"] = ExpectedWoundFingerprint, ["quote"] = quote.ToJson(),
            ["confirmation"] = service.PlayerConfirmed ? "player_service_confirmed" : "required"
        };
    }

    private static SpiritualSelfTreatmentSelection ParseService(SpiritualSelfTreatmentSelection selection, JsonObject root)
    {
        if (!AfterlifeHealingServiceContract.Closed(root, "schemaVersion", "mode", "woundId",
                "expectedWoundFingerprint", "quote", "confirmation") ||
            AfterlifeHealingServiceContract.Text(root["mode"]) != "service" ||
            AfterlifeHealingServiceContract.Text(root["confirmation"]) is not ("required" or "player_service_confirmed"))
            throw new InvalidDataException("Invalid closed public healing selection.");
        var result = selection with { Service = new(AfterlifeHealingServiceContract.ParseQuote(root["quote"]),
            root["confirmation"]!.GetValue<string>() == "player_service_confirmed") };
        if (!JsonNode.DeepEquals(result.ToJson(), root))
            throw new InvalidDataException("Public service selection differs from its exact quote.");
        return result;
    }
}
