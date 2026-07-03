using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Models;

public class TurnRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("turnNumber")]
    public int TurnNumber { get; set; }

    [JsonPropertyName("playerAction")]
    public string PlayerAction { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("gameMode")]
    public string GameMode { get; set; } = "normal";

    [JsonPropertyName("preGeneratedDices1d20")]
    public int[] PreGeneratedDices1d20 { get; set; } = Array.Empty<int>();

    [JsonPropertyName("gachaBaseResult")]
    public GachaResult? GachaBaseResult { get; set; }

    [JsonPropertyName("additionalContext")]
    public AdditionalContext? AdditionalContext { get; set; }

    [JsonPropertyName("systemReminder")]
    public string? SystemReminder { get; set; }

    [JsonPropertyName("computedCharacteristics")]
    public object? ComputedCharacteristics { get; set; }

    [JsonPropertyName("progressionControl")]
    public ProgressionControl? ProgressionControl { get; set; }

    [JsonPropertyName("afterlifeSpiritualConflictPreview")]
    public JsonObject? AfterlifeSpiritualConflictPreview { get; set; }
}

public class AdditionalContext
{
    [JsonPropertyName("urgency")]
    public string Urgency { get; set; } = "medium";

    [JsonPropertyName("expectedResponse")]
    public string ExpectedResponse { get; set; } = "narrative";
}

/// <summary>
/// Client-computed gacha base result. The GM MUST use this base rarity
/// and may only upgrade it via documented modifiers (guardian reputation, hard/impossible mode).
/// It is separate from the GM-facing preGeneratedDices1d20 pool.
/// </summary>
public class GachaResult
{
    [JsonPropertyName("diceUsed")]
    public int[] DiceUsed { get; set; } = Array.Empty<int>();

    [JsonPropertyName("baseScore")]
    public int BaseScore { get; set; }

    [JsonPropertyName("baseRarity")]
    public string BaseRarity { get; set; } = "Common";

    [JsonPropertyName("formula")]
    public string Formula { get; set; } = "client-computed gacha base (range 4-80)";
}

public class ProgressionControl
{
    [JsonPropertyName("currentRealm")]
    public string CurrentRealm { get; set; } = "Chaos Sea";

    [JsonPropertyName("currentWorldTimeInMinutes")]
    public int CurrentWorldTimeInMinutes { get; set; }

    [JsonPropertyName("lastWorldSimulationTimeInMinutes")]
    public int LastWorldSimulationTimeInMinutes { get; set; }

    [JsonPropertyName("lastFactionSimulationTimeInMinutes")]
    public int LastFactionSimulationTimeInMinutes { get; set; }

    [JsonPropertyName("worldCycleMinutes")]
    public int WorldCycleMinutes { get; set; } = 240;

    [JsonPropertyName("factionCycleMinutes")]
    public int FactionCycleMinutes { get; set; } = 1440;

    [JsonPropertyName("worldCyclesAlreadyPendingBeforeTurn")]
    public int WorldCyclesAlreadyPendingBeforeTurn { get; set; }

    [JsonPropertyName("factionCyclesAlreadyPendingBeforeTurn")]
    public int FactionCyclesAlreadyPendingBeforeTurn { get; set; }

    [JsonPropertyName("mustEvaluateWorldProgression")]
    public bool MustEvaluateWorldProgression { get; set; }

    [JsonPropertyName("mustEvaluateFactionProgression")]
    public bool MustEvaluateFactionProgression { get; set; }

    [JsonPropertyName("currentChaosSeaTurnOrdinal")]
    public int CurrentChaosSeaTurnOrdinal { get; set; }

    [JsonPropertyName("nextChaosSeaTurnOrdinal")]
    public int NextChaosSeaTurnOrdinal { get; set; }

    [JsonPropertyName("lastChaosSeaSimulationOrdinal")]
    public int LastChaosSeaSimulationOrdinal { get; set; }

    [JsonPropertyName("lastGuardianProjectCycleOrdinal")]
    public int LastGuardianProjectCycleOrdinal { get; set; }

    [JsonPropertyName("nextGuardianProjectCycleOrdinal")]
    public int NextGuardianProjectCycleOrdinal { get; set; }

    [JsonPropertyName("lastResidentAgencyCycleOrdinal")]
    public int LastResidentAgencyCycleOrdinal { get; set; }

    [JsonPropertyName("lastShiningAbodeCycleOrdinal")]
    public int LastShiningAbodeCycleOrdinal { get; set; }

    [JsonPropertyName("lastShiningFactionCycleOrdinal")]
    public int LastShiningFactionCycleOrdinal { get; set; }

    [JsonPropertyName("lastShiningTradeCycleOrdinal")]
    public int LastShiningTradeCycleOrdinal { get; set; }

    [JsonPropertyName("chaosSeaCycleEquivalentHours")]
    public int ChaosSeaCycleEquivalentHours { get; set; } = 24;

    [JsonPropertyName("nextResidentAgencyCycleOrdinal")]
    public int NextResidentAgencyCycleOrdinal { get; set; }

    [JsonPropertyName("nextShiningAbodeCycleOrdinal")]
    public int NextShiningAbodeCycleOrdinal { get; set; }

    [JsonPropertyName("nextShiningFactionCycleOrdinal")]
    public int NextShiningFactionCycleOrdinal { get; set; }

    [JsonPropertyName("nextShiningTradeCycleOrdinal")]
    public int NextShiningTradeCycleOrdinal { get; set; }

    [JsonPropertyName("chaosSeaCyclesExpectedThisTurn")]
    public int ChaosSeaCyclesExpectedThisTurn { get; set; }

    [JsonPropertyName("guardianProjectCyclesExpectedThisTurn")]
    public int GuardianProjectCyclesExpectedThisTurn { get; set; }

    [JsonPropertyName("residentAgencyCyclesExpectedThisTurn")]
    public int ResidentAgencyCyclesExpectedThisTurn { get; set; }

    [JsonPropertyName("shiningAbodeCyclesExpectedThisTurn")]
    public int ShiningAbodeCyclesExpectedThisTurn { get; set; }

    [JsonPropertyName("shiningFactionCyclesExpectedThisTurn")]
    public int ShiningFactionCyclesExpectedThisTurn { get; set; }

    [JsonPropertyName("shiningTradeCyclesExpectedThisTurn")]
    public int ShiningTradeCyclesExpectedThisTurn { get; set; }

    [JsonPropertyName("mustEvaluateChaosSeaProgression")]
    public bool MustEvaluateChaosSeaProgression { get; set; }

    [JsonPropertyName("mustEvaluateGuardianProjectProgression")]
    public bool MustEvaluateGuardianProjectProgression { get; set; }

    [JsonPropertyName("mustEvaluateResidentAgencyProgression")]
    public bool MustEvaluateResidentAgencyProgression { get; set; }

    [JsonPropertyName("mustEvaluateShiningAbodeProgression")]
    public bool MustEvaluateShiningAbodeProgression { get; set; }

    [JsonPropertyName("mustEvaluateShiningFactionProgression")]
    public bool MustEvaluateShiningFactionProgression { get; set; }

    [JsonPropertyName("mustEvaluateShiningTradeProgression")]
    public bool MustEvaluateShiningTradeProgression { get; set; }

    [JsonPropertyName("afterlifeCatchupRequired")]
    public bool AfterlifeCatchupRequired { get; set; }

    [JsonPropertyName("afterlifeCatchupElapsedCycles")]
    public int AfterlifeCatchupElapsedCycles { get; set; }

    [JsonPropertyName("afterlifeCatchupPressureTier")]
    public string AfterlifeCatchupPressureTier { get; set; } = "none";

    [JsonPropertyName("afterlifeCatchupSummaryEventsRequired")]
    public int AfterlifeCatchupSummaryEventsRequired { get; set; }

    [JsonPropertyName("afterlifeCatchupContours")]
    public string[] AfterlifeCatchupContours { get; set; } = Array.Empty<string>();
}

public class ProgressionProcessingReport
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("turnNumber")]
    public int TurnNumber { get; set; }

    [JsonPropertyName("worldCyclesProcessed")]
    public int? WorldCyclesProcessed { get; set; }

    [JsonPropertyName("factionCyclesProcessed")]
    public int? FactionCyclesProcessed { get; set; }

    [JsonPropertyName("chaosSeaCyclesProcessed")]
    public int? ChaosSeaCyclesProcessed { get; set; }

    [JsonPropertyName("guardianProjectCyclesProcessed")]
    public int? GuardianProjectCyclesProcessed { get; set; }

    [JsonPropertyName("residentAgencyCyclesProcessed")]
    public int? ResidentAgencyCyclesProcessed { get; set; }

    [JsonPropertyName("shiningAbodeCyclesProcessed")]
    public int? ShiningAbodeCyclesProcessed { get; set; }

    [JsonPropertyName("shiningFactionCyclesProcessed")]
    public int? ShiningFactionCyclesProcessed { get; set; }

    [JsonPropertyName("shiningTradeCyclesProcessed")]
    public int? ShiningTradeCyclesProcessed { get; set; }

    [JsonPropertyName("newLastWorldSimulationTimeInMinutes")]
    public int? NewLastWorldSimulationTimeInMinutes { get; set; }

    [JsonPropertyName("newLastFactionSimulationTimeInMinutes")]
    public int? NewLastFactionSimulationTimeInMinutes { get; set; }

    [JsonPropertyName("newLastChaosSeaSimulationOrdinal")]
    public int? NewLastChaosSeaSimulationOrdinal { get; set; }

    [JsonPropertyName("newLastGuardianProjectCycleOrdinal")]
    public int? NewLastGuardianProjectCycleOrdinal { get; set; }

    [JsonPropertyName("newLastResidentAgencyCycleOrdinal")]
    public int? NewLastResidentAgencyCycleOrdinal { get; set; }

    [JsonPropertyName("newLastShiningAbodeCycleOrdinal")]
    public int? NewLastShiningAbodeCycleOrdinal { get; set; }

    [JsonPropertyName("newLastShiningFactionCycleOrdinal")]
    public int? NewLastShiningFactionCycleOrdinal { get; set; }

    [JsonPropertyName("newLastShiningTradeCycleOrdinal")]
    public int? NewLastShiningTradeCycleOrdinal { get; set; }

    [JsonPropertyName("afterlifeCatchupProcessed")]
    public bool? AfterlifeCatchupProcessed { get; set; }

    [JsonPropertyName("afterlifeCatchupSummaryEventsProcessed")]
    public int? AfterlifeCatchupSummaryEventsProcessed { get; set; }
}
