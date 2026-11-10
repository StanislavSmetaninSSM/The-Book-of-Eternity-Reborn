using System.Text.Json;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    /// <summary>Stages a real queued browser original through the existing producers for component validation.</summary>
    /// <param name="binding">The actual queued action and existing session generation.</param>
    /// <param name="request">Complete typed request matching this engine session and queued action.</param>
    /// <param name="withRollback">Whether the existing backup producer must capture pre-turn rollback DATA.</param>
    /// <returns>The genuinely published and sealed staged original; no execution or cleanup is implied.</returns>
    internal Task<PendingPlayerActionService.Staged> StageBrowserOriginalForValidationAsync(
        PendingPlayerActionService.Binding binding, TurnRequest request, bool withRollback)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(request);
        if (request.SessionId != _gameLoop.SessionId || request.TurnNumber != _gameLoop.TurnNumber + 1 ||
            request.RequestId != binding.ActionId || request.PlayerAction != binding.Action ||
            request.ProgressionControl == null || string.IsNullOrWhiteSpace(request.Timestamp))
            throw new InvalidDataException("Browser validation preparation requires the current engine and exact queued request.");
        // Detach caller DATA before admission/await; authority remains with the actual participating owner.
        var json = JsonSerializer.Serialize(request, JsonOpts);
        var detached = JsonSerializer.Deserialize<TurnRequest>(json, JsonOpts)!;
        return SessionOperationContext.RunParticipatingExpectedSessionAsync(_fs, binding.Generation, async () =>
        {
            using var original = _fs.BeginBrowserOriginalOperation(binding);
            await ClaimBrowserPreparationAsync(binding);
            var backup = withRollback ? await CreatePreTurnBackup(binding.ActionId) : null;
            await CreateCanonicalBaselineSnapshotAsync(detached, backup, OrdinaryPlayerTurnSourceLabel, binding);
            return await PublishBrowserStagingAsync(binding, json);
        });
    }
}
