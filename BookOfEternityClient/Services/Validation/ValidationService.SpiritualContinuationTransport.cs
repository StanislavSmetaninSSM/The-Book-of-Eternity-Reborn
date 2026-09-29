using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Reports a read-only continuation boundary without retaining source or execution authority.
    /// </summary>
    /// <param name="Disposition">
    /// Decision, dependent_draft, completed_unpublished, no_checkpoint, automatic_continuation or blocked.
    /// </param>
    /// <param name="Request">
    /// Safe current request, or null when no GM response may be requested at this boundary.
    /// </param>
    /// <param name="Issues">
    /// Current dependent-draft diagnostics or the reason the private boundary cannot be authenticated.
    /// </param>
    internal sealed record SpiritualWoundContinuationReadResult(string Disposition,
        SpiritualWoundContinuationRequest? Request, IReadOnlyList<ValidationIssue> Issues)
    {
        /// <summary>
        /// Gets the last completed request proved by private journal replay, solely for obsolete transport cleanup.
        /// </summary>
        internal SpiritualWoundContinuationRequest? AcceptedRequest { get; init; }
    }

    /// <summary>
    /// Reads the current owner-derived continuation without repairing, advancing or publishing it.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease retained for genuine owner replay and exact physical checks.
    /// </param>
    /// <param name="expectedRequest">
    /// Optional issued envelope that must equal the independently derived current request.
    /// </param>
    /// <returns>
    /// A safe comparison request or an authenticated boundary without a request.
    /// </returns>
    internal async Task<SpiritualWoundContinuationReadResult> ReadSpiritualWoundContinuationAsync(
        FileSystemManager.CanonicalWriteLease lease, SpiritualWoundContinuationRequest? expectedRequest = null)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        var classified = await ClassifySpiritualPendingAsync(lease);
        using var initialCapture = classified.Capture;
        if (classified.Disposition == "no_checkpoint")
            return new("no_checkpoint", null, []);
        if (classified.Disposition != "match" || initialCapture is null)
            return new("blocked", null, classified.Issues.Count != 0 ? classified.Issues :
                AdapterFailure("spiritual_continuation_pair_unmatched").Issues);
        try
        {
            var opened = await OpenC2ClassifiedSessionAsync(lease, initialCapture,
                allowAutomaticTransport: false);
            using var session = opened.Session;
            if (session is null)
                return new("blocked", null, opened.Issues.Count != 0 ? opened.Issues :
                    AdapterFailure("spiritual_continuation_owner_unavailable").Issues);
            return await session.ReadContinuationProjectionAsync(lease, opened.Disposition, expectedRequest);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidOperationException or FormatException or JsonException or OverflowException or DecoderFallbackException)
        {
            return new("blocked", null, AdapterFailure("spiritual_continuation_read_failed").Issues);
        }
    }

    internal sealed partial class SpiritualC2PrivateSession
    {
        /// <summary>
        /// Projects a matched boundary while preserving exact private and draft images across diagnostics.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease retained until the final owner and physical checks complete.
        /// </param>
        /// <param name="disposition">
        /// Boundary returned by genuine source admission with automatic transport disabled.
        /// </param>
        /// <param name="expectedRequest">
        /// Optional issued envelope compared in full with the independently derived current request.
        /// </param>
        /// <param name="previewNext">
        /// Whether to prove an uncommitted successor for evaluation only; no public active request is changed.
        /// </param>
        /// <returns>
        /// Detached comparison data; no execution owner escapes this projection.
        /// </returns>
        internal async Task<SpiritualWoundContinuationReadResult> ReadContinuationProjectionAsync(
            FileSystemManager.CanonicalWriteLease lease, string disposition,
            SpiritualWoundContinuationRequest? expectedRequest = null, bool previewNext = false)
        {
            await _submitGate.WaitAsync();
            try
            {
                if (_disposed || _used)
                    throw new InvalidOperationException("The continuation session is no longer current.");
                _continuationDraftPolicy = null;
                var before = await _capture.ReadC2TransportWitnessAsync(lease);
                SpiritualWoundContinuationRequest? request = null;
                IReadOnlyList<ValidationIssue> issues = [];
                if (disposition == "offer" && Offer is { } offer)
                {
                    var safeOffer = new SpiritualWoundContinuationOffer
                    {
                        OpportunityRef = offer.OpportunityRef,
                        MinimumSeverityRank = offer.MinimumSeverityRank,
                        RequiredSeverityRank = offer.RequiredSeverityRank,
                        MaximumSeverityRank = offer.MaximumSeverityRank,
                        Target = offer.Target,
                        Cause = offer.Cause,
                        AllowedLocationKinds = Array.AsReadOnly(offer.AllowedLocationKinds.ToArray()),
                        AllowedDecisions = Array.AsReadOnly(offer.AllowedDecisions.ToArray())
                    };
                    var checkpoint = before[SpiritualWoundCaptureCheckpointState.StatePath].Bytes!;
                    var pending = before[SpiritualWoundDecisionPendingState.StatePath].Bytes!;
                    var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                        "spiritual_continuation_decision_v1:" + Convert.ToHexString(SHA256.HashData(checkpoint)) +
                        ":" + Convert.ToHexString(SHA256.HashData(pending)) + ":" + JsonSerializer.Serialize(safeOffer))));
                    request = CreateContinuationRequest(id, "decision", safeOffer, []);
                    disposition = "decision";
                }
                else if (disposition == "dependent_continuation" && _capture.HasC2PendingSubmission)
                {
                    var context = _capture.NeedsSequentialDependentContext
                        ? await ReadSequentialDependentContextAsync(lease, previewNext)
                        : await _capture.ReadC2DependentContextAsync(lease);
                    if (context is null)
                        throw new InvalidOperationException("The saved choice has no proven correction context.");
                    _continuationDraftPolicy = context.DraftPolicy;
                    request = CreateContinuationRequest(context.ContinuationId, "dependent_draft", null,
                        context.DependentDraftFields);
                    issues = context.CurrentIssues;
                    disposition = "dependent_draft";
                }
                else if (disposition is not ("completed_unpublished" or "automatic_continuation"))
                    throw new InvalidOperationException("The continuation boundary is unsupported.");
                if (!SameWitness(before, await _capture.ReadC2TransportWitnessAsync(lease)))
                    throw new InvalidOperationException("The continuation inputs changed during projection.");
                if (request is not null && SpiritualWoundContinuationProtocol.ValidateRequest(request).Count != 0)
                    throw new InvalidOperationException("The owner projection violates the transport contract.");
                if (expectedRequest is not null && JsonSerializer.Serialize(request) != JsonSerializer.Serialize(expectedRequest))
                    throw new InvalidOperationException("The issued request is not the current private frontier.");
                SpiritualWoundContinuationRequest? acceptedRequest = null;
                var checkpointState = SpiritualWoundCaptureCheckpointState.Parse(
                    SpiritualWoundStateJson.DecodeUtf8JsonText(before[SpiritualWoundCaptureCheckpointState.StatePath].Bytes!),
                    SpiritualWoundCaptureCheckpointState.StatePath, before.Keys.Where(SpiritualOriginalDraftInputs.IsDraftPath)).State;
                if (checkpointState?.ReadPendingSubmission()?["dependentDraftProgress"] is System.Text.Json.Nodes.JsonArray rows && rows.Count > 0)
                {
                    var last = rows[rows.Count - 1]!;
                    var fields = last["dependentDraftFields"]!.AsArray().Select(field => new SpiritualWoundContinuationField
                    {
                        Path = field!["path"]!.GetValue<string>(), JsonPointer = field["jsonPointer"]!.GetValue<string>()
                    }).ToArray();
                    acceptedRequest = CreateContinuationRequest(last["acceptedContinuationId"]!.GetValue<string>(), "dependent_draft", null, fields);
                }
                return new(disposition, request, issues) { AcceptedRequest = acceptedRequest };
            }
            finally { _submitGate.Release(); }
        }

        /// <summary>
        /// Builds the closed transport envelope using only detached owner-derived fields.
        /// </summary>
        /// <param name="id">
        /// Exact private-boundary comparison token, never an execution capability.
        /// </param>
        /// <param name="phase">
        /// Decision or dependent_draft according to the authenticated owner state.
        /// </param>
        /// <param name="offer">
        /// Safe current offer for a decision, otherwise null.
        /// </param>
        /// <param name="fields">
        /// Stable exact correction fields; empty for decision requests.
        /// </param>
        /// <returns>
        /// A detached request whose scene source remains the existing narrative response.
        /// </returns>
        internal static SpiritualWoundContinuationRequest CreateContinuationRequest(string id, string phase,
            SpiritualWoundContinuationOffer? offer, IReadOnlyList<SpiritualWoundContinuationField> fields) => new()
        {
            ContinuationId = id,
            Phase = phase,
            Offer = offer,
            DependentDraftFields = Array.AsReadOnly(fields.ToArray()),
            SceneTextSource = new() { Path = "output/narrative_response.json", Field = "response" }
        };
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Captures exact physical transport inputs while rechecking the current replayed owner and private pair.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease covering source witnesses, current drafts and private controls.
        /// </param>
        /// <returns>
        /// Detached exact images; stale ownership or an unexplained saved command throws.
        /// </returns>
        internal async Task<IReadOnlyDictionary<string, CanonicalBeforeImage>> ReadC2TransportWitnessAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                EnsureCurrent(lease);
                if (_matchedC2Pair is not { } pair || (await CheckRetainedInputsAsync(lease)).Count != 0)
                    throw new InvalidOperationException("The matched transport owner is unavailable.");
                if (_c2PendingSubmission)
                {
                    var checkpoint = await RequireC2PendingSubmissionCoreAsync(lease);
                    await ReadC2SubmissionChangesCoreAsync(lease, checkpoint);
                }
                var result = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
                foreach (var path in _draftInputs.PathInventory.Concat(new[]
                { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath }).Distinct())
                {
                    var bytes = await _validator._fs.ReadFileBytesAsync(lease, path);
                    result[path] = new(bytes is not null, bytes);
                }
                if (!ExactBytes(result[SpiritualWoundCaptureCheckpointState.StatePath].Bytes, pair.CheckpointBytes) ||
                    !ExactBytes(result[SpiritualWoundDecisionPendingState.StatePath].Bytes, pair.PendingBytes) ||
                    (await CheckRetainedInputsAsync(lease)).Count != 0)
                    throw new InvalidOperationException("The transport pair changed during its witness read.");
                EnsureCurrent(lease);
                return result;
            }
            finally { _gate.Release(); }
        }
    }
}
