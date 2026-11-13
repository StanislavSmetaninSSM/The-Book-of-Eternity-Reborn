using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    // No new handoff: every use rechecks the existing transaction's live take receipt.
    private JsonObject RequireTreatmentNpcSkillOwner(AcceptedMechanicsPlan plan,
        MortalWoundTreatmentPublicationTakeReceipt receipt) =>
        RequireTreatmentNpcSkillResult(_fs, _writeLease!, _npcProgressionSource!, plan, receipt);

    internal static JsonObject RequireTreatmentNpcSkillResult(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease,
        ValidationService.NpcProgressionPublicationSource source, AcceptedMechanicsPlan plan,
        MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        var input = source.SkillInput ?? throw new InvalidDataException("Treatment NPC skill input is missing.");
        var context = source.Context;
        var binding = receipt.Binding;
        if (context is null || context.Realm != "mortal_world" ||
            context.SessionId != binding.SessionId || context.RequestId != binding.RequestId ||
            context.SnapshotToken != binding.SnapshotToken || context.TurnNumber != binding.Turn ||
            context.Realm != binding.Realm || !ReferenceEquals(receipt.Plan, plan) ||
            !AcceptedMechanicsPlanAuthority.IsTakenTreatmentPublicationCurrent(files, lease, binding, plan, receipt) ||
            plan.TreatmentResourcePublicationAuthority is not { RequiresCoordinatedSettlement: true } authority ||
            !authority.HasValidSeal() || !ReferenceEquals(receipt.PublicationAuthority, authority) ||
            input.ItemProjectionProof is not null ||
            !plan.OwnerCompanionAfterImages.TryGetValue(NpcCoreChangesContract.NpcCorePath, out var finalRoot))
            throw new InvalidDataException("Treatment NPC skills lost their exact sealed final root or live transaction owner.");
        source.RequirePolicyAuthority(authority.NpcCoreAuthority);
        source.RequireBaseline(authority.NpcCoreBackupRoot);
        RequireCandidateFields(input.CandidateCore, finalRoot);
        if (finalRoot.ContainsKey(NpcCoreChangesContract.PropertyName))
            throw new InvalidDataException("Treatment final NPC root retains core commands.");
        return finalRoot; // OwnerCompanionAfterImages already exports detached roots.
    }

    private async Task ValidateTreatmentNpcSkillOpeningAsync(AcceptedMechanicsPlan plan,
        MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        RequireTreatmentNpcSkillOwner(plan, receipt);
        await _npcProgressionSource!.RequireRawSkillInputsAsync(_fs, _writeLease);
        var input = _npcProgressionSource.SkillInput!;
        var originalMetadata = input.OriginalSidecar;
        var currentMetadata = input.RawSidecar;
        foreach (var key in new[] { "NPCActiveSkillChanges", "NPCPassiveSkillChanges", "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges" })
        {
            originalMetadata.Remove(key);
            currentMetadata.Remove(key);
        }
        if (!MortalNpcAcceptedSkillCandidate.HasOnlyDistributorTimestampDifference(originalMetadata, currentMetadata))
            throw new InvalidDataException("Treatment NPC sidecar changes unrelated metadata.");
        var beforeImages = plan.BeforeImages; // One detached export for both raw witnesses.
        foreach (var (path, raw) in new[]
                 { (NpcCoreChangesContract.NpcCorePath, input.RawCore), (MortalNpcAcceptedSkillInput.SidecarPath, input.RawSidecar) })
            if (!beforeImages.TryGetValue(path, out var before) || !before.Existed || before.Bytes is null ||
                !JsonNode.DeepEquals(JsonNode.Parse(DecodePendingSnapshotText(before.Bytes)), raw))
                throw new InvalidDataException("Treatment NPC raw witness differs from its sealed plan: " + path);
    }

    private async Task ConsumeTreatmentNpcSkillsAsync(MortalWoundTreatmentPublicationTakeReceipt receipt)
    {
        var expected = RequireTreatmentNpcSkillOwner(receipt.Plan, receipt);
        await _npcProgressionSource!.RequireCurrentAsync(_fs, _writeLease);
        var input = _npcProgressionSource.SkillInput!;
        if (!JsonNode.DeepEquals(await ReadNodeAsync(NpcCoreChangesContract.NpcCorePath), expected) ||
            !JsonNode.DeepEquals(await ReadNodeAsync(MortalNpcAcceptedSkillInput.SidecarPath), input.RawSidecar))
            throw new InvalidDataException("Treatment NPC publication diverged before pending command consumption.");
        // Existing writer, covered by the coordinator's sidecar before-image and rollback.
        await WriteIfChangedAsync(MortalNpcAcceptedSkillInput.SidecarPath, input.RawSidecar, input.ConsumedSidecar);
        await _npcProgressionSource.RequirePublishedSkillsAsync(_fs, _writeLease, receipt);
    }

    private static void RequireCandidateFields(JsonObject candidate, JsonObject finalRoot)
    {
        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (candidate.ContainsKey(section) != finalRoot.ContainsKey(section))
                throw new InvalidDataException("Treatment NPC candidate topology changed.");
            if (candidate[section] is not JsonArray expected) continue;
            if (finalRoot[section] is not JsonArray actual || expected.Count != actual.Count)
                throw new InvalidDataException("Treatment NPC candidate actor count changed.");
            for (var i = 0; i < expected.Count; i++)
            {
                if (expected[i] is not JsonObject before || actual[i] is not JsonObject after ||
                    !GuardianPolicyContracts.TryResolveStrictPermanentNpcId(before, out var id) ||
                    !GuardianPolicyContracts.TryResolveStrictPermanentNpcId(after, out var actualId) || id != actualId)
                    throw new InvalidDataException("Treatment NPC candidate identity changed.");
                // Item consumption/equipment is owned by the exact sealed final root, never by this candidate.
                foreach (var field in new[] { "activeSkills", "passiveSkills", "level", "experience", "experienceForNextLevel",
                             "progressionProfile", "progressionType", "progressionTrackers", "characteristics" })
                    if (before.ContainsKey(field) != after.ContainsKey(field) || !JsonNode.DeepEquals(before[field], after[field]))
                        throw new InvalidDataException("Treatment final NPC skill/progression differs from the admitted candidate: " + field);
            }
        }
    }
}
