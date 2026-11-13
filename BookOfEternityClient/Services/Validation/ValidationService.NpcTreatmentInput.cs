using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Intake-only candidate: never captures the ordinary item snapshot or publishes roots.
    internal async Task<(GameResponse? Response, NpcProgressionPublicationSource Source)>
        ReadTreatmentNpcProgressionInputAsync(FileSystemManager.CanonicalWriteLease lease,
            byte[]? coreBytes, byte[]? sidecarBytes)
    {
        var source = await PrepareNpcProgressionPublicationCoreAsync(lease, coreBytes, sidecarBytes, treatmentIntake: true);
        if (source.Issues.Count != 0 || source.SkillInput is not { } input)
            return (null, source);
        var originalCore = input.OriginalCore;
        var rawCore = input.RawCore;
        originalCore.Remove(NpcCoreChangesContract.PropertyName);
        rawCore.Remove(NpcCoreChangesContract.PropertyName);
        if (!MortalNpcAcceptedSkillCandidate.HasOnlyDistributorTimestampDifference(originalCore, rawCore))
            throw new InvalidDataException("Treatment NPC intake forbids direct actor resends or unrelated core mutations.");
        var originalMetadata = input.OriginalSidecar;
        var rawMetadata = input.RawSidecar;
        foreach (var key in new[] { "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges" })
            if (rawMetadata[key] is JsonArray { Count: > 0 })
                throw new InvalidDataException("Treatment NPC intake does not support mastery command arrays.");
        foreach (var key in new[] { "NPCActiveSkillChanges", "NPCPassiveSkillChanges", "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges" })
        {
            originalMetadata.Remove(key);
            rawMetadata.Remove(key);
        }
        if (!MortalNpcAcceptedSkillCandidate.HasOnlyDistributorTimestampDifference(originalMetadata, rawMetadata))
            throw new InvalidDataException("Treatment NPC intake changes unrelated sidecar metadata.");
        var raw = input.RawSidecar;
        var response = new GameResponse
        {
            NPCActiveSkillChanges = Rows("NPCActiveSkillChanges"),
            NPCPassiveSkillChanges = Rows("NPCPassiveSkillChanges")
        };
        return (response, source);

        JsonElement[]? Rows(string key) => raw[key] is JsonArray rows
            ? rows.Select(row => JsonSerializer.SerializeToElement(row)).ToArray() : null;
    }
}
