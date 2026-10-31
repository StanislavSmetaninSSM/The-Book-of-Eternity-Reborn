using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentCompletion
{
    /// <summary>
    /// Authenticates a current physical settlement against the independent browser
    /// original and complete fresh canonical DATA. Neither parsed receipts nor
    /// this comparison can reserve, resolve, publish or reactivate closed owners.
    /// </summary>
    internal static async Task<MortalWoundTreatmentPersistedResult> RequirePublishedAsync(TurnRequest original,
        string snapshotToken, string generation, SpiritualSelfTreatmentSelection selection, JsonObject input, string signedOriginalScheduleHash,
        Func<string, Task<byte[]?>> read)
    {
        var control = original.ProgressionControl ?? throw Invalid("Missing physical original control.");
        var frame = new SpiritualSelfTreatmentStageContext(original.SessionId, original.RequestId, original.TurnNumber, control);
        var staged = MortalWoundGuidedTreatmentInputCodec.Read(input);
        if (selection.MortalTreatment is null || staged.Selection != selection ||
            !MortalWoundGuidedTreatmentInputCodec.IsBound(input, frame) ||
            !JsonNode.DeepEquals(original.MortalTreatment, MortalWoundGuidedTreatmentInputCodec.CreateGmContext(input)))
            throw Invalid("Physical selection or full independent original context changed.");
        var (bytes, nodes) = await ReadCurrentAsync(read);
        string? Text(string path) => bytes[path] is { } value ? CanonicalJsonUtf8.DecodeOneOptionalBom(value) : null;
        JsonObject? Root(string path) => nodes.GetValueOrDefault(path) as JsonObject;
        var parsed = WoundHistoryState.Parse(Text(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        if (!parsed.IsValid || parsed.State is not { } history) throw Invalid("Current wound History is invalid.");
        var matching = history.Transitions.Where(row => row.TreatmentResult is { } result &&
            result.Request.Coordinates.SessionId == original.SessionId && result.Request.Coordinates.RequestId == original.RequestId &&
            result.Request.Coordinates.Turn == original.TurnNumber).ToArray();
        if (matching.Length != 1 || matching[0].TreatmentResult is not { HasGuidedPublicationWitness: true } retained)
            throw Invalid("Expected exactly one actual guided publication result; historical receipts are insufficient.");
        var request = retained.Request;
        var coordinates = request.Coordinates;
        MortalWoundGuidedTreatmentInputCodec.RequireRequestMatches(input, request);
        var accepted = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(original.SessionId,
            original.RequestId, snapshotToken, original.TurnNumber);
        if (!accepted.Success) throw Invalid("The independent original accepted-event binding is invalid.");
        var binding = new WoundAcceptedTurnBinding(original.SessionId, original.RequestId, snapshotToken, "mortal_world",
            original.TurnNumber, accepted.Events, accepted.EventsFingerprint);
        var comparison = retained.ReconstructGuidedPublication();
        if (coordinates.SnapshotToken != snapshotToken || coordinates.SessionGeneration != generation ||
            comparison.BindingFingerprint != MortalWoundTreatmentAcceptedStateAuthority.HashBinding(binding) ||
            !WoundResponseInputComposer.MatchesAcceptedBinding(binding, coordinates) ||
            comparison.CurrentMinute != control.CurrentWorldTimeInMinutes ||
            coordinates.OperationKey != "mortal_treatment_operation_" + MortalWoundGuidedTreatmentInputCodec.Fingerprint(input)["sha256:".Length..])
            throw Invalid("Guided settlement belongs to a foreign original, generation, clock or operation.");
        if (request.ModeAuthority is MortalWoundProcedureCheckAuthority check &&
            (check.SourceIndices.Count != check.SourceRolls.Count || check.SourceIndices.Where((index, offset) =>
                index < 0 || index >= original.PreGeneratedDices1d20.Length || original.PreGeneratedDices1d20[index] != check.SourceRolls[offset]).Any()))
            throw Invalid("The procedure draw differs from the actual signed original pool.");
        if (bytes[AcceptedMechanicsPlan.WoundCommandPath] is not null ||
            bytes[WoundAcceptedTurnSnapshotContract.PendingResolutionPath] is not null ||
            bytes[ResourceMaterializationContract.CommandPath] is not null || bytes[EffectAcceptedTurnPlan.CommandPath] is not null)
            throw Invalid("Consumed guided work reappeared.");
        var carriers = WoundCarrierCatalog.Build(new(Root(WoundCarrierCatalog.PlayerPath), Root(WoundCarrierCatalog.NpcPath),
            Root(WoundCarrierCatalog.EnemiesPath), Root(WoundCarrierCatalog.AlliesPath), Root(WoundCarrierCatalog.AfterlifeProfilesPath)));
        var identity = WoundIdentityState.Parse(Text(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        if (carriers.Issues.Count != 0 || !identity.IsValid || history.ValidateAgreement(identity.State!, carriers).Count != 0)
            throw Invalid("Complete current wound carrier/identity/History agreement changed.");
        var current = await RequireCanonicalSettlementAsync(bytes, nodes, request, comparison.Resolution.ResultFingerprint,
            coordinates.Turn, comparison.Finalization, comparison.SemanticFingerprint);
        var expected = ReconstructFinalWound(comparison, current);
        var occurrences = carriers.Occurrences.Where(value => value.WoundId == coordinates.WoundId).ToArray();
        var terminal = matching[0];
        if (occurrences.Length != 1 || WoundMaterializationContract.SerializeCanonical(occurrences[0].Wound) !=
                WoundMaterializationContract.SerializeCanonical(expected) ||
            history.Transitions.Last(row => row.WoundId == coordinates.WoundId) != terminal ||
            terminal.TransitionId != expected.LastTransition.TransitionId ||
            terminal.AfterFingerprint != WoundIdentityState.ComputeSemanticFingerprint(expected) ||
            terminal.BeforeFingerprint != coordinates.ExpectedBeforeFingerprint || terminal.SourceFingerprint != request.RequestFingerprint)
            throw Invalid("Complete current wound differs from the independently reconstructed physical result.");
        var originalSchedule = retained.ReadGuidedOriginalScheduleBytes();
        if (PendingTurnSnapshotAuthority.ComputeSnapshotFileHash(PendingTurnSnapshotAuthority.ExactSnapshotHashMode,
                originalSchedule) != signedOriginalScheduleHash)
            throw Invalid("Retained original schedule differs from the independently authenticated original hash.");
        ProgressionScheduleService.RequirePublishedMortalTreatmentOutcome(control, original.SessionId, original.RequestId,
            original.TurnNumber, originalSchedule, bytes[EffectAcceptedTurnInputComposer.WorldTimePath], bytes[ProgressionScheduleService.SchedulePath],
            bytes[ProgressionScheduleService.ReportPath]);
        return retained;
    }
}
