namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Validates a selected canonical after-image following an actual ordinary refresh.
    /// This RED-stage entry preserves strict validation; a result is not publication authority.
    /// No current IDs are inferred as authenticated merely from this argument.
    /// </summary>
    internal Task<List<ValidationIssue>> ValidateCompletedLocationStateAsync(
        AcceptedTurnCanonicalStateRefresh.Result published, GameStateValidationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(published);
        ArgumentNullException.ThrowIfNull(selection);
        return ValidateGameStateAsync(selection);
    }
}
