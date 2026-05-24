using BookOfEternityClient.Core;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserGameScreenService
{
    private readonly StateManager _stateManager;

    public BrowserGameScreenService(StateManager stateManager)
    {
        _stateManager = stateManager;
    }

    public async Task<BrowserGameScreenDto> BuildAsync()
    {
        await _stateManager.RefreshGameStateAsync();
        var state = _stateManager.CurrentState;

        return new BrowserGameScreenDto(
            SchemaVersion: 1,
            Soul: new BrowserGameScreenSoulDto(
                Name: state.SoulName,
                Realm: state.CurrentRealm,
                Incarnation: state.Incarnation,
                InkFeathers: state.InkFeathers,
                EnlightenmentTier: state.EnlightenmentTier,
                ActiveGuardianName: state.ActiveGuardianName),
            Player: new BrowserGameScreenPlayerDto(
                Name: state.CharacterName,
                Class: state.CharacterClass,
                Race: state.CharacterRace,
                CurrentCondition: state.PlayerStatus.CurrentCondition,
                HealthPercentage: state.PlayerStatus.HealthPercentage,
                EnergyPercentage: state.PlayerStatus.EnergyPercentage,
                PoisePercentage: state.PlayerStatus.PoisePercentage,
                ActiveConditions: state.PlayerStatus.ActiveConditions),
            World: new BrowserGameScreenWorldDto(
                Location: state.CurrentLocation,
                WorldTime: state.WorldTime,
                TurnNumber: state.TurnNumber,
                SessionId: state.SessionId),
            Narrative: new BrowserGameScreenNarrativeDto(
                Text: state.Narrative),
            Flags: new BrowserGameScreenFlagsDto(
                IsInChaosSea: state.IsInChaosSea,
                IsInAnyShiningAbodeState: state.IsInAnyShiningAbodeState,
                IsInShiningAbode: state.IsInShiningAbode,
                IsInShiningAbodePendingBootstrap: state.IsInShiningAbodePendingBootstrap,
                IsInAfterlifeRealm: state.IsInAfterlifeRealm,
                CanReenterShiningAbode: state.CanReenterShiningAbode));
    }
}

public sealed record BrowserGameScreenDto(
    int SchemaVersion,
    BrowserGameScreenSoulDto Soul,
    BrowserGameScreenPlayerDto Player,
    BrowserGameScreenWorldDto World,
    BrowserGameScreenNarrativeDto Narrative,
    BrowserGameScreenFlagsDto Flags);

public sealed record BrowserGameScreenSoulDto(
    string Name,
    string Realm,
    int Incarnation,
    int InkFeathers,
    string EnlightenmentTier,
    string ActiveGuardianName);

public sealed record BrowserGameScreenPlayerDto(
    string Name,
    string Class,
    string Race,
    string CurrentCondition,
    string HealthPercentage,
    string EnergyPercentage,
    string PoisePercentage,
    IReadOnlyList<string> ActiveConditions);

public sealed record BrowserGameScreenWorldDto(
    string Location,
    string WorldTime,
    int TurnNumber,
    string SessionId);

public sealed record BrowserGameScreenNarrativeDto(string Text);

public sealed record BrowserGameScreenFlagsDto(
    bool IsInChaosSea,
    bool IsInAnyShiningAbodeState,
    bool IsInShiningAbode,
    bool IsInShiningAbodePendingBootstrap,
    bool IsInAfterlifeRealm,
    bool CanReenterShiningAbode);
