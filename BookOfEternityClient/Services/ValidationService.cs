using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

/// <summary>
/// Public validation API and top-level orchestration entrypoints.
/// </summary>
public partial class ValidationService
{
    internal FileSystemManager CanonicalFileSystem => _fs;

    /// <summary>
    /// Run all validations on the current game state. Returns list of issues found.
    /// </summary>
    public Task<List<ValidationIssue>> ValidateGameStateAsync()
        => ValidateGameStateInternalAsync(GameStateValidationSelection.All);

    internal Task<List<ValidationIssue>> ValidateGameStateAsync(
        GameStateValidationPhase phases)
        => ValidateGameStateInternalAsync(new GameStateValidationSelection(phases));

    internal Task<List<ValidationIssue>> ValidateGameStateAsync(
        GameStateValidationSelection selection)
        => ValidateGameStateInternalAsync(selection);

    /// <summary>
    /// Validate a single GM response before distributing to files.
    /// </summary>
    public List<ValidationIssue> ValidateResponse(JsonElement response)
        => ValidateResponseInternal(response);

    public Task<List<ValidationIssue>> ValidateAcceptedTurnNarrativePayloadAsync()
        => ValidateAcceptedTurnNarrativePayloadInternalAsync();

    public Task<List<ValidationIssue>> ValidateAcceptedTurnInterfacePayloadAsync()
        => ValidateAcceptedTurnInterfacePayloadInternalAsync();

    public Task<List<ValidationIssue>> ValidateAcceptedTurnReasoningAsync()
        => ValidateAcceptedTurnReasoningInternalAsync(AcceptedTurnReasoningValidationSelection.Full);

    internal Task<List<ValidationIssue>> ValidateAcceptedTurnReasoningAsync(
        AcceptedTurnReasoningValidationSelection selection)
        => ValidateAcceptedTurnReasoningInternalAsync(selection);

    public Task<List<ValidationIssue>> ValidateAcceptedTurnSpecialActionOutcomesAsync()
        => ValidateAcceptedTurnSpecialActionOutcomesInternalAsync();

    public Task<List<ValidationIssue>> ValidatePendingMemoryLegacyApplicationAsync()
        => ValidatePendingMemoryLegacyApplicationInternalAsync();

    public Task<List<ValidationIssue>> ValidateAcceptedTurnQteOfferAsync()
        => ValidateAcceptedTurnQteOfferInternalAsync();

    public Task<List<ValidationIssue>> ValidateAcceptedTurnMortalCombatMaterializationAsync()
        => ValidateAcceptedTurnMortalCombatMaterializationInternalAsync();

    public Task<List<ValidationIssue>> ValidateAcceptedTurnMortalLevelUpMaterializationAsync()
        => ValidateAcceptedTurnMortalLevelUpMaterializationInternalAsync();
}

[Flags]
internal enum AcceptedTurnReasoningValidationScope : byte
{
    None = 0,
    Core = 1 << 0,
    GuardianMusing = 1 << 1,
    MortalNpcJournal = 1 << 2,
    AfterlifeResidentJournal = 1 << 3,
    AfterlifeEntityLedger = 1 << 4,
    ShiningFactionMemory = 1 << 5,
    AfterlifeMemoryOwner = 1 << 6,
    Full = Core | GuardianMusing | MortalNpcJournal | AfterlifeResidentJournal |
           AfterlifeEntityLedger | ShiningFactionMemory | AfterlifeMemoryOwner
}

internal sealed class AcceptedTurnReasoningValidationSelection
{
    public static AcceptedTurnReasoningValidationSelection Full { get; } =
        new(AcceptedTurnReasoningValidationScope.Full);

    public AcceptedTurnReasoningValidationSelection(AcceptedTurnReasoningValidationScope scopes)
    {
        if ((scopes & ~AcceptedTurnReasoningValidationScope.Full) != 0 ||
            (scopes & AcceptedTurnReasoningValidationScope.Core) == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scopes),
                scopes,
                "Reasoning validation selections require Core and only defined scopes.");
        }

        Scopes = scopes;
    }

    public AcceptedTurnReasoningValidationScope Scopes { get; }

    public bool Includes(AcceptedTurnReasoningValidationScope scope) =>
        (Scopes & scope) != 0;
}
