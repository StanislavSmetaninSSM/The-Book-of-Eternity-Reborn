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
    private IReadOnlyDictionary<string, CanonicalBeforeImage>? _mortalQteDecisionImages;
    private MortalWoundOpportunityReceiptConsumePlanResult? _mortalQteExactReplay;

    private async Task<IReadOnlyList<ValidationIssue>> ValidateResourceAndMortalQteDecisionAsync(bool ordinaryUpstreamAccepted = true)
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            if (!ordinaryUpstreamAccepted) await RevokeOwnedMortalQteCommandAsync(lease);
            var issues = await _validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease,
                ordinaryUpstreamAccepted ? StageMortalQteDecisionAsync : null,
                ordinaryUpstreamAccepted ? SealMortalQteDecisionAsync : null,
                ordinaryUpstreamAccepted ? () => _mortalQteExactReplay : null);
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
            var narrativeBytes = await _fs.ReadFileBytesAsync(lease, WoundNarrativeOutputPath);
            var narrative = narrativeBytes is null ? null : new UTF8Encoding(false, true).GetString(narrativeBytes).TrimStart('\uFEFF');
            var sceneRoot = narrative is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(
                narrative.TrimStart('\uFEFF'), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "Mortal wound ordinary scene");
            var scene = sceneRoot?["response"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            var composed = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(_fs, lease, raw, scene, out var replay);
            if (composed is null) return Array.Empty<ValidationIssue>();
            if (!composed.Success) return composed.Issues.Select(issue =>
                issue.FilePath == "woundDecisions[0]" || issue.FilePath.StartsWith("woundDecisions[0].", StringComparison.Ordinal)
                    ? new ValidationIssue(MortalWoundOpportunityAdapter.DecisionPath, issue.Severity,
                        issue.FilePath + ": " + issue.Message, issue.Code, issue.Actor, issue.Section,
                        issue.Expected, issue.Actual, issue.RepairHint, issue.Category, issue.RepairTargetFiles)
                    {
                        FactionRepairClassification = issue.FactionRepairClassification,
                        MortalItemRepairContext = issue.MortalItemRepairContext,
                        MortalLocationRepairContext = issue.MortalLocationRepairContext,
                        EffectRepairContext = issue.EffectRepairContext,
                        WoundRepairContext = issue.WoundRepairContext?.Clone()
                    }
                    : issue).ToArray();
            var decision = StrictJsonAuthority.Deserialize<JsonElement>(raw!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "accepted Mortal wound decision");
            _mortalQteOwnedCommand = await StateDistributor.StageAcceptedWoundCommandAsync(_fs, lease,
                new GameResponse { Response = scene ?? string.Empty, WoundDecisions = [decision] }, composed);
            _mortalQteExactReplay = replay;
            _mortalQteDecisionImages = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
            {
                [MortalWoundOpportunityAdapter.DecisionPath] = new(rawBytes is not null, rawBytes),
                [WoundNarrativeOutputPath] = new(narrativeBytes is not null, narrativeBytes),
                [AcceptedMechanicsPlan.WoundCommandPath] = new(_mortalQteOwnedCommand is not null, _mortalQteOwnedCommand)
            };
            return Array.Empty<ValidationIssue>();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or DecoderFallbackException)
        {
            return new[] { new ValidationIssue(MortalWoundOpportunityAdapter.DecisionPath, IssueSeverity.Error,
                "Mortal wound decision staging refused: " + error.Message, "mortal_wound_decision_staging_invalid") };
        }
    }

    private async Task<IReadOnlyList<ValidationIssue>> SealMortalQteDecisionAsync(
        FileSystemManager.CanonicalWriteLease lease, IReadOnlyDictionary<string, CanonicalBeforeImage> commonBeforeImages)
    {
        if (_mortalQteDecisionImages is null) return Array.Empty<ValidationIssue>();
        foreach (var pair in _mortalQteDecisionImages)
        {
            var current = await _fs.ReadFileBytesAsync(lease, pair.Key);
            if (!commonBeforeImages.TryGetValue(pair.Key, out var common) || common.Existed != pair.Value.Existed ||
                !Same(common.Bytes, pair.Value.Bytes) || !Same(current, pair.Value.Bytes))
                return new[] { new ValidationIssue(pair.Key, IssueSeverity.Error,
                    "The raw decision, ordinary scene or issued wound command changed before the common seal.",
                    "mortal_wound_decision_staged_input_changed") };
        }
        return Array.Empty<ValidationIssue>();

        static bool Same(byte[]? left, byte[]? right) => left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
    }

    private async Task RevokeOwnedMortalQteCommandAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, lease);
        EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, lease);
        _mortalQteDecisionImages = null;
        _mortalQteExactReplay = null;
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
