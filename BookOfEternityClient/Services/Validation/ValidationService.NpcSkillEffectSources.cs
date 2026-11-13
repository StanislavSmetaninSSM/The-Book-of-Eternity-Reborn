using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Admits ordinary NPC skill commands from the existing selected raw view.
    /// Original offered skills remain unchanged; only accepted current source DATA
    /// is projected. This method grants neither publication nor new focus authority.
    /// </summary>
    private async Task<MortalNpcAcceptedSkillInput?> ComposeOrdinaryNpcSkillEffectInputAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        IReadOnlyDictionary<string, JsonNode?> originals,
        IReadOnlyDictionary<string, JsonNode?> current,
        List<ValidationIssue> issues,
        SpiritualOriginalDraftInputs? currentInputs,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var rawText = await ReadEffectFileAsync(MortalNpcAcceptedSkillInput.SidecarPath,
            writeLease, currentInputs);
        if (rawText is null) return null;
        if (!TryReadNpcProgressionOriginal(rawText, out var rawSidecar))
        {
            issues.Add(NewEffectIssue(MortalNpcAcceptedSkillInput.SidecarPath,
                "npc_skill_candidate_invalid_root", "one exact object with unique properties", "malformed sidecar"));
            return null;
        }
        if (!MortalNpcAcceptedSkillInput.HasPending(rawSidecar!)) return null;
        if (!originals.TryGetValue(NpcCoreChangesContract.NpcCorePath, out var before) || before is not JsonObject originalCore ||
            !current.TryGetValue(NpcCoreChangesContract.NpcCorePath, out var now) || now is not JsonObject rawCore)
        {
            issues.Add(NewEffectIssue(NpcCoreChangesContract.NpcCorePath,
                "npc_skill_candidate_core_unavailable", "authenticated original and raw current NPC core", "missing or non-object"));
            return null;
        }
        var originalText = await ReadValidatedPendingTurnSnapshotFileAsync(manifest,
            MortalNpcAcceptedSkillInput.SidecarPath, writeLease);
        if (!TryReadNpcProgressionOriginal(originalText, out var originalSidecar))
        {
            issues.Add(NewEffectIssue(MortalNpcAcceptedSkillInput.SidecarPath,
                "npc_skill_candidate_original_unavailable", "present authenticated clean original sidecar", "missing or invalid"));
            return null;
        }
        string? Root(string path) => current.TryGetValue(path, out var root) ? root?.ToJsonString() : null;
        var authority = NpcCoreChangesContract.CreateAuthorityFromCanonicalJson(
            Root(MortalLocationMaterializationContract.WorldMapPath),
            Root(MortalLocationMaterializationContract.CurrentLocationPath),
            Root("game_state/factions/faction_core.json"),
            await ReadEffectFileAsync("game_state/misc/characteristics.json", writeLease, currentInputs))
            with { ProgressionSource = CreateNpcProgressionSource(manifest) };
        var trade = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, NpcTradeRequestState.PendingRequestPath, writeLease);
        var training = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, TrainingRequestState.PendingRequestPath, writeLease);
        var input = MortalNpcAcceptedSkillInput.Create(originalCore, originalSidecar!, rawCore, rawSidecar!,
            authority, trade, training, out var candidateIssues);
        issues.AddRange(candidateIssues);
        return input;
    }
}
