using BookOfEternityClient.Models;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    /// <summary>Runs the existing complete accepted owner with its actual original rollback and progression control.</summary>
    /// <param name="source">Diagnostic source label.</param>
    /// <param name="rollback">The engine-issued original rollback snapshot.</param>
    /// <param name="expectedTurn">Current held turn number.</param>
    /// <param name="progression">Original progression control for the held turn.</param>
    /// <returns>The real accepted validation and repair loop disposition.</returns>
    internal async Task<AcceptedTurnValidationDisposition> ValidatePreparedLivingWorldOutcomeAsync(
        string source, RollbackSnapshot rollback, int expectedTurn, ProgressionControl progression)
    {
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        var snapshot = await LoadValidatedPendingTurnSnapshotContextAsync(manifest, requireCurrentContext: true);
        return await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(source, snapshot, rollback, expectedTurn, progression);
    }
}
