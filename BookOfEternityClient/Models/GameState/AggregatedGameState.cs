namespace BookOfEternityClient.Models.GameState;

/// <summary>
/// Aggregated game state loaded from all game_state/ files.
/// Used for UI rendering and state exploration.
/// </summary>
public class AggregatedGameState
{
    // Core
    public string Narrative { get; set; } = string.Empty;
    public string GmDebug { get; set; } = string.Empty;
    public PlayerStatusState PlayerStatus { get; set; } = new();
    
    // Session
    public string SessionId { get; set; } = string.Empty;
    public int TurnNumber { get; set; }
    public string CurrentLocation { get; set; } = string.Empty;
    public string WorldTime { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string CharacterClass { get; set; } = string.Empty;
    public string CharacterRace { get; set; } = string.Empty;
    
    // Soul meta
    public string CurrentRealm { get; set; } = "Chaos Sea"; // "Chaos Sea", "Shining Abode", or mortal world name
    public string SoulName { get; set; } = string.Empty;
    public int Incarnation { get; set; } = 1;
    public int InkFeathers { get; set; }
    public string EnlightenmentTier { get; set; } = "Новичок";
    public string ActiveGuardianName { get; set; } = string.Empty;
    
    // Timestamps
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// True when the soul is specifically in the Chaos Sea hub.
    /// </summary>
    public bool IsInChaosSea => string.Equals(CurrentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(CurrentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase)
                                || string.IsNullOrEmpty(CurrentRealm);

    public bool IsInShiningAbode => string.Equals(CurrentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(CurrentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    public bool IsInAfterlifeRealm => IsInChaosSea || IsInShiningAbode;
}

public class PlayerStatusState
{
    public string HealthPercentage { get; set; } = "100%";
    public string EnergyPercentage { get; set; } = "100%";
    public string PoisePercentage { get; set; } = "100%";
    public string CurrentCondition { get; set; } = "Здоров";
    public string[] ActiveConditions { get; set; } = Array.Empty<string>();
}
