using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

/// <summary>
/// Proves current complete settlement from an independently authenticated browser
/// original and fresh canonical DATA. It never restores an owner or a reservation.
/// </summary>
internal static class MortalWoundDiagnosisCompletion
{
    /// <summary>
    /// Reads each current dependency once under the caller's existing read/lease
    /// boundary and requires exact original, durable result and current settlement.
    /// </summary>
    internal static async Task<MortalWoundDiagnosisPersistedResult> RequirePublishedAsync(TurnRequest original,
        string snapshotToken, string generation, SpiritualSelfTreatmentSelection selection, JsonObject input,
        Func<string, Task<byte[]?>> read)
    {
        var control = original.ProgressionControl ?? throw Invalid("Missing original control.");
        var context = new SpiritualSelfTreatmentStageContext(original.SessionId, original.RequestId, original.TurnNumber, control);
        var selected = MortalWoundDiagnosisInputCodec.Read(input);
        if (selection.MortalDiagnosis is null || selected.Selection != selection || !MortalWoundDiagnosisInputCodec.IsBound(input, context))
            throw Invalid("The independent original selection/control changed.");
        var (bytes, nodes) = await MortalWoundTreatmentCompletion.ReadCurrentAsync(read);
        string? Text(string path) => bytes[path] is { } value ? CanonicalJsonUtf8.DecodeOneOptionalBom(value) : null;
        JsonObject? Root(string path) => nodes.GetValueOrDefault(path) as JsonObject;
        var parsed = WoundHistoryState.Parse(Text(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        if (!parsed.IsValid) throw Invalid("Current wound History is invalid.");
        var history = parsed.State!;
        var matching = history.Transitions.Select(row => row.TransitionResult).OfType<MortalWoundDiagnosisPersistedResult>()
            .Where(value => value.Binding.SessionId == original.SessionId && value.Binding.RequestId == original.RequestId &&
                value.Binding.Turn == original.TurnNumber).ToArray();
        if (matching.Length != 1) throw Invalid("Expected exactly one complete diagnosis result for this original.");
        var result = matching[0]; var resultRoot = result.ToCanonicalJson();
        if (result.Binding.SnapshotToken != snapshotToken || result.Source.SessionGeneration != generation ||
            !JsonNode.DeepEquals(resultRoot["input"], input) || original.PreGeneratedDices1d20 is null ||
            !result.Source.AcceptedD20EventValues.SequenceEqual(original.PreGeneratedDices1d20))
            throw Invalid("The current result belongs to another original, token, generation or dice pool.");
        MortalWoundDiagnosisInputCodec.RequireRequestMatches(input, result.Request);
        var semantic = result.ReconstructPublicationSemantic(); // v1 is historical, never an implicit empty publication.
        var finalized = MortalWoundTreatmentResourceComposer.Finalize(result.Resolution);
        if (!finalized.IsValid || finalized.Finalization is null) throw Invalid("Invalid retained finalization.");
        var finalization = finalized.Finalization;


        var carriers = WoundCarrierCatalog.Build(new(Root(WoundCarrierCatalog.PlayerPath), Root(WoundCarrierCatalog.NpcPath),
            Root(WoundCarrierCatalog.EnemiesPath), Root(WoundCarrierCatalog.AlliesPath), Root(WoundCarrierCatalog.AfterlifeProfilesPath)));
        var identities = WoundIdentityState.Parse(Text(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        if (carriers.Issues.Count != 0 || !identities.IsValid || history.ValidateAgreement(identities.State!, carriers).Count != 0)
            throw Invalid("Current wound carrier/identity/History agreement changed.");
        var cohort = result.Recoveries.Select(member => member.Original.WoundId).ToHashSet(StringComparer.Ordinal);
        foreach (var identity in identities.State!.Entries.Where(value => value.Realm == "mortal_world" && value.Domain == "physical" && value.Status == "active"))
            if (!cohort.Contains(identity.WoundId)) throw Invalid("Current diagnosis cohort acquired an unrelated active member.");
        foreach (var member in result.Recoveries)
        {
            var final = member.Stages.Length == 0 ? member.Working : member.Stages[^1].AfterWound;
            var occurrences = carriers.Occurrences.Where(value => value.WoundId == final.WoundId).ToArray();
            if (history.Transitions.Last(value => value.WoundId == final.WoundId).TransitionId != final.LastTransition.TransitionId ||
                (final.Lifecycle == "healed" ? occurrences.Length != 0 : occurrences.Length != 1 ||
                 WoundMaterializationContract.SerializeCanonical(occurrences[0].Wound) != WoundMaterializationContract.SerializeCanonical(final)))
                throw Invalid("The complete current cohort no longer equals the published diagnosis result.");
        }
        var clock = resultRoot["clock"]!.AsObject();
        if (!JsonNode.DeepEquals(nodes[EffectAcceptedTurnInputComposer.WorldTimePath], clock["after"]) ||
            !Equal(bytes[ProgressionScheduleService.SchedulePath], Decode(clock["scheduleAfterBase64"])) ||
            !Equal(bytes[ProgressionScheduleService.ReportPath], Decode(clock["reportAfterBase64"])) ||
            bytes[AcceptedMechanicsPlan.WoundCommandPath] is not null || bytes[WoundAcceptedTurnSnapshotContract.PendingResolutionPath] is not null ||
            bytes[ResourceMaterializationContract.CommandPath] is not null || bytes[EffectAcceptedTurnPlan.CommandPath] is not null)
            throw Invalid("Clock/schedule/report changed or consumed diagnosis work reappeared.");

        await MortalWoundTreatmentCompletion.RequireCanonicalSettlementAsync(bytes, nodes, result.Request,
            result.Resolution.ResultFingerprint, result.Binding.Turn, finalization, semantic);
        return result;
    }

    private static byte[]? Decode(JsonNode? node) => node is null ? null : Convert.FromBase64String(node.GetValue<string>());
    private static bool Equal(byte[]? left, byte[]? right) => left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
    private static InvalidDataException Invalid(string reason) => new("Published Mortal diagnosis is incomplete: " + reason);
}
