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
    public string ShiningAbodeAvailability { get; set; } = string.Empty;
    public bool HasPendingShiningAbodeBootstrapPackage { get; set; }
    public bool HasBlockingAfterlifeReturnGuard { get; set; }
    
    // Timestamps
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// True when the soul is specifically in the Chaos Sea hub.
    /// </summary>
    public bool IsInChaosSea => string.Equals(CurrentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(CurrentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase)
                                || string.IsNullOrEmpty(CurrentRealm);

    private bool IsShiningAbodeRealmBucket => string.Equals(CurrentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(CurrentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True for any Shining Abode realm bucket state, including the pending-bootstrap handoff.
    /// </summary>
    public bool IsInAnyShiningAbodeState => IsShiningAbodeRealmBucket;

    /// <summary>
    /// True only for ordinary active Shining Abode mode, not the pending-bootstrap handoff state.
    /// </summary>
    public bool IsInShiningAbode => IsShiningAbodeRealmBucket && !HasPendingShiningAbodeBootstrapPackage;

    /// <summary>
    /// True when the soul is still in the Shining Abode realm bucket, but control has already been handed to mortal bootstrap.
    /// </summary>
    public bool IsInShiningAbodePendingBootstrap => IsShiningAbodeRealmBucket && HasPendingShiningAbodeBootstrapPackage;

    public bool IsInAfterlifeRealm => IsInChaosSea || IsInAnyShiningAbodeState;

    public bool HasActiveStoredShiningAbode =>
        string.Equals(ShiningAbodeAvailability, "active", StringComparison.OrdinalIgnoreCase);

    public bool CanReenterShiningAbode =>
        IsInChaosSea &&
        HasActiveStoredShiningAbode &&
        !HasPendingShiningAbodeBootstrapPackage &&
        !HasBlockingAfterlifeReturnGuard;
}

public class PlayerStatusState
{
    public string HealthPercentage { get; set; } = "100%";
    public string EnergyPercentage { get; set; } = "100%";
    public string PoisePercentage { get; set; } = "100%";
    public string CurrentCondition { get; set; } = "Здоров";
    public string[] ActiveConditions { get; set; } = Array.Empty<string>();
}
