using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Independent receipt/history DATA, never source authentication or publication permission.</summary>
internal sealed record MortalLivingWorldReceiptData(string HistoryJson, byte[] HistoryBytes, JsonObject Receipt);

/// <summary>Deterministic private receipt declarations shared by ordinary and pre-seal owners.</summary>
internal static class MortalLivingWorldReceipt
{
    /// <summary>Validates and independently copies current revision history; it grants no replay authority.</summary>
    internal static JsonObject ValidateHistory(JsonObject? history) =>
        throw new NotImplementedException("#1516 deterministic receipt history DATA has not been implemented.");

    /// <summary>Appends exact source/report/window declarations without validating actual domain effects or retaining caller DATA.</summary>
    internal static MortalLivingWorldReceiptData Build(string snapshotToken, MortalLivingWorldAssessmentContext context,
        JsonObject report, JsonObject? priorHistory) =>
        throw new NotImplementedException("#1516 deterministic receipt declaration DATA has not been implemented.");
}
