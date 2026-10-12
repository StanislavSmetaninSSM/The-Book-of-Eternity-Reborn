using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Only bytes actually issued by this engine may be revoked during its repair loop.
    // A foreign command is always preserved and refused by the command-only distributor.
    private byte[]? _mortalQteOwnedCommand;

    private async Task<IReadOnlyList<ValidationIssue>> ValidateResourceAndMortalQteDecisionAsync(bool ordinaryUpstreamAccepted = true)
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            if (!ordinaryUpstreamAccepted) await RevokeOwnedMortalQteCommandAsync(lease);
            var issues = await _validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease,
                ordinaryUpstreamAccepted ? StageMortalQteDecisionAsync : null);
            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
                await RevokeOwnedMortalQteCommandAsync(lease);
            return issues;
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException)
        {
            await RevokeOwnedMortalQteCommandAsync(lease);
            throw;
        }
    }

    private async Task<IReadOnlyList<ValidationIssue>> StageMortalQteDecisionAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        await RevokeOwnedMortalQteCommandAsync(lease);
        try
        {
            var rawBytes = await _fs.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath);
            var raw = rawBytes is null ? null : new UTF8Encoding(false, true).GetString(rawBytes).TrimStart('\uFEFF');
            var narrative = await _fs.ReadFileAsync(lease, WoundNarrativeOutputPath);
            var sceneRoot = narrative is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(
                narrative.TrimStart('\uFEFF'), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "Mortal wound ordinary scene");
            var scene = sceneRoot?["response"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            var composed = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(_fs, lease, raw, scene);
            if (composed is null) return Array.Empty<ValidationIssue>();
            if (!composed.Success) return composed.Issues;
            var decision = StrictJsonAuthority.Deserialize<JsonElement>(raw!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "accepted Mortal wound decision");
            _mortalQteOwnedCommand = await StateDistributor.StageAcceptedWoundCommandAsync(_fs, lease,
                new GameResponse { Response = scene ?? string.Empty, WoundDecisions = [decision] }, composed);
            return Array.Empty<ValidationIssue>();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or DecoderFallbackException)
        {
            return new[] { new ValidationIssue(MortalWoundOpportunityAdapter.DecisionPath, IssueSeverity.Error,
                "Mortal wound decision staging refused: " + error.Message, "mortal_wound_decision_staging_invalid") };
        }
    }

    private async Task RevokeOwnedMortalQteCommandAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, lease);
        EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, lease);
        if (_mortalQteOwnedCommand is not { } issued) return;
        var current = await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        if (current is not null && current.AsSpan().SequenceEqual(issued))
        {
            var removed = await CoordinatedStateWriteHelper.TryCommitAsync(_fs, lease,
                new CoordinatedStateWriteHelper.PlannedWrite(AcceptedMechanicsPlan.WoundCommandPath, null, null,
                    RequireCurrentBaseline: true, ExactPrevious: new CanonicalBeforeImage(true, issued)));
            if (!removed) throw new InvalidDataException("mortal_wound_decision_owned_command_changed: revocation failed closed.");
        }
        _mortalQteOwnedCommand = null;
    }
}
