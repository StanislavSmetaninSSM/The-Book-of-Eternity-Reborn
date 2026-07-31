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
        => ValidateAcceptedTurnReasoningInternalAsync();

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
