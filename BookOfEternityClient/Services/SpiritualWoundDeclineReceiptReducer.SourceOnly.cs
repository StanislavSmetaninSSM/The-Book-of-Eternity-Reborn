using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class SpiritualWoundDeclineReceiptReducer
{
    /// <summary>
    /// Retains or admits an instance and its actual terminal closure for an exhausted capture with no decisions.
    /// </summary>
    /// <param name="owner">
    /// Capture-issued proof of the completed original stream; ordinary receipt data cannot replace it.
    /// </param>
    /// <returns>
    /// An unchanged or append-only receipt and its required terminal join proof, or rejection diagnostics.
    /// </returns>
    internal static Result ReduceSourceOnly(
        ValidationService.SpiritualOriginalTurnCapture.AuthenticatedC3SourceOnly owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!owner.IsAuthentic)
            return Fail("spiritual_c3_source_only_owner_missing");
        try
        {
            var signed = owner.Signed;
            var parsed = SpiritualWoundOpportunityReceiptState.Parse(signed.Receipt.Existed
                ? new UTF8Encoding(false, true).GetString(signed.Receipt.Bytes!) : EmptyReceipt,
                SpiritualWoundOpportunityReceiptState.StatePath);
            if (!parsed.IsValid || parsed.State is not { } before)
                return new(null, parsed.Issues);
            var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(signed.Receipt, signed.Conflict,
                owner.Realm, owner.Input.SessionId, owner.Input.RequestId, owner.Input.SnapshotToken, owner.Input.Turn);
            if (resolved.Issues.Count != 0 || resolved.InstanceId is null)
                return new(null, resolved.Issues);
            var root = SpiritualWoundStateJson.Parse(SpiritualWoundOpportunityReceiptState.SerializeCanonical(before));
            if (resolved.ProposedRow is not null)
            {
                root["instances"]!.AsArray().Add(resolved.ProposedRow.DeepClone());
                root["nextInstanceOrdinal"] = checked(
                    (int)SpiritualWoundStateJson.Integer(root["nextInstanceOrdinal"]) + 1);
            }
            if (owner.Terminal is { } terminal)
            {
                var ordinal = (int)SpiritualWoundStateJson.Integer(root["nextClosureOrdinal"]);
                root["closures"]!.AsArray().Add(terminal.CreateClosure(resolved.InstanceId, ordinal));
                root["nextClosureOrdinal"] = checked(ordinal + 1);
            }
            var candidate = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(),
                SpiritualWoundOpportunityReceiptState.StatePath);
            if (!candidate.IsValid || candidate.State is null)
                return new(null, candidate.Issues);
            var append = SpiritualWoundOpportunityReceiptState.PlanAppend(before, candidate.State);
            return append.Disposition is "appended" or "exact_replay" && append.After is not null
                ? Complete(signed, null, owner.ReceiptIdentity, append.After)
                : Fail("spiritual_c3_source_only_receipt_conflict");
        }
        catch (Exception error) when (error is JsonException or FormatException or
            InvalidOperationException or OverflowException or ArgumentException or DecoderFallbackException)
        {
            return Fail("spiritual_c3_source_only_receipt_invalid");
        }
    }
}
