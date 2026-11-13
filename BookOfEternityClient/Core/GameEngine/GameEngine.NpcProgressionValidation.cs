namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    /// <summary>
    /// Runs the existing accepted-turn owner against its actual current authenticated snapshot.
    /// Keeps private snapshot authority internal; adds no alternate validation or publication path.
    /// </summary>
    internal async Task<AcceptedTurnValidationDisposition> ValidatePreparedNpcProgressionOutcomeAsync(
        string source, int expectedTurn)
    {
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        var snapshot = await LoadValidatedPendingTurnSnapshotContextAsync(manifest, requireCurrentContext: true);
        return await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(source, snapshot,
            rollbackSnapshot: null, expectedTurn, progressionControl: null);
    }
}
