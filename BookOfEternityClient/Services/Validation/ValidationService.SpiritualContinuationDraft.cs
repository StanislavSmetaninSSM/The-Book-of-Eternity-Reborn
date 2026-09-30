using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Validates proposed continuation drafts against the freshly reconstructed private boundary without writes.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease covering current C2 authentication and physical preservation checks.
    /// </param>
    /// <param name="expectedRequest">
    /// Previously issued comparison envelope; it never grants execution or write authority.
    /// </param>
    /// <param name="response">
    /// Closed response matching the current phase and opportunity.
    /// </param>
    /// <param name="proposedImages">
    /// Exact proposed bytes by canonical relative path; <see langword="null"/> means deletion and omitted paths remain unchanged.
    /// </param>
    /// <param name="requireResolvedDraft">
    /// When <see langword="true"/>, requires no remaining current dependency errors and an empty proposal set.
    /// A completed earlier frontier with unresolved successors still fails this strict check.
    /// </param>
    /// <returns>
    /// Empty issues only for a current response and permitted draft differences; no decision is consumed.
    /// </returns>
    internal async Task<IReadOnlyList<ValidationIssue>> ValidateSpiritualWoundContinuationDraftAsync(
        FileSystemManager.CanonicalWriteLease lease, SpiritualWoundContinuationRequest expectedRequest,
        SpiritualWoundContinuationResponse response, IReadOnlyDictionary<string, byte[]?> proposedImages,
        bool requireResolvedDraft = false)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        if (requireResolvedDraft && proposedImages.Count != 0 ||
            SpiritualWoundContinuationProtocol.ValidateResponse(expectedRequest, response).Count != 0 ||
            proposedImages.Any(pair => pair.Value is null ||
                pair.Key != "output/narrative_response.json" &&
                (expectedRequest.Phase != "dependent_draft" || pair.Key != AfterlifeSpiritualConflictState.StatePath)))
            return AdapterFailure("spiritual_continuation_proposal_not_permitted").Issues;
        var expectedJson = JsonSerializer.Serialize(expectedRequest);
        if (!await MatchesIssuedSpiritualContinuationAsync(lease, expectedRequest))
            return AdapterFailure("spiritual_continuation_request_changed").Issues;
        var captured = proposedImages.ToDictionary(pair => pair.Key,
            pair => pair.Value!.ToArray(), StringComparer.Ordinal);
        var classified = await ClassifySpiritualPendingAsync(lease);
        using var initialCapture = classified.Capture;
        if (classified.Disposition != "match" || initialCapture is null)
            return classified.Issues.Count != 0 ? classified.Issues :
                AdapterFailure("spiritual_continuation_pair_unmatched").Issues;
        try
        {
            var opened = await OpenC2ClassifiedSessionAsync(lease, initialCapture, allowAutomaticTransport: false);
            using var session = opened.Session;
            if (session is null)
                return opened.Issues.Count != 0 ? opened.Issues :
                    AdapterFailure("spiritual_continuation_owner_unavailable").Issues;
            var current = await session.ReadContinuationProjectionAsync(lease, opened.Disposition, expectedRequest);
            if (current.Request is null || JsonSerializer.Serialize(current.Request) != expectedJson ||
                SpiritualWoundContinuationProtocol.ValidateResponse(current.Request, response).Count != 0)
                return AdapterFailure("spiritual_continuation_request_changed").Issues;
            if (requireResolvedDraft && current.Issues.Count != 0)
                return current.Issues;
            return await session.ValidateContinuationImagesAsync(lease, current.Request.Phase, response, captured);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidOperationException or FormatException or JsonException or OverflowException or DecoderFallbackException)
        {
            return AdapterFailure("spiritual_continuation_proposal_invalid").Issues;
        }
    }

    /// <summary>
    /// Distinguishes refusal, complete resolution and validated progress to a separate dependent request.
    /// </summary>
    internal enum SpiritualWoundContinuationDisposition
    {
        /// <summary>
        /// The issued response or actual candidate failed authentication or validation.
        /// </summary>
        Rejected,
        /// <summary>
        /// The complete actual candidate has no remaining dependent diagnostics.
        /// </summary>
        Resolved,
        /// <summary>
        /// The exact issued frontier is complete and a different supported frontier remains.
        /// </summary>
        Advanced
    }

    /// <summary>
    /// Carries an internal physical validation result without granting execution or write authority.
    /// </summary>
    /// <param name="Disposition">
    /// Rejected, fully resolved, or advanced to the separate request.
    /// </param>
    /// <param name="Issues">
    /// Refusal diagnostics, or outstanding successor diagnostics for an advanced result.
    /// </param>
    /// <param name="NextRequest">
    /// Reserved for an activated successor; read-only evaluation always returns <see langword="null"/>.
    /// </param>
    internal sealed record SpiritualWoundContinuationEvaluation(SpiritualWoundContinuationDisposition Disposition,
        IReadOnlyList<ValidationIssue> Issues, SpiritualWoundContinuationRequest? NextRequest = null);

    /// <summary>
    /// Authenticates a physical response against its exact reconstructed issued frontier before proving a successor.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease retained across private replay and complete actual-image checks.
    /// </param>
    /// <param name="expectedRequest">
    /// Exact issued comparison envelope; transport content alone never supplies permission.
    /// </param>
    /// <param name="response">
    /// Closed correlated response; dependent responses must contain no wound decisions.
    /// </param>
    /// <returns>
    /// Typed refusal, full resolution, or completion of the issued request with a fresh supported successor.
    /// </returns>
    internal async Task<SpiritualWoundContinuationEvaluation> EvaluateSpiritualWoundContinuationDraftAsync(
        FileSystemManager.CanonicalWriteLease lease, SpiritualWoundContinuationRequest expectedRequest,
        SpiritualWoundContinuationResponse response)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        SpiritualWoundContinuationEvaluation Rejected(IReadOnlyList<ValidationIssue> issues) =>
            new(SpiritualWoundContinuationDisposition.Rejected, issues);
        if (SpiritualWoundContinuationProtocol.ValidateResponse(expectedRequest, response).Count != 0 ||
            !await MatchesIssuedSpiritualContinuationAsync(lease, expectedRequest))
            return Rejected(AdapterFailure("spiritual_continuation_request_changed").Issues);
        var classified = await ClassifySpiritualPendingAsync(lease);
        using var initialCapture = classified.Capture;
        if (classified.Disposition != "match" || initialCapture is null)
            return Rejected(classified.Issues.Count != 0 ? classified.Issues :
                AdapterFailure("spiritual_continuation_pair_unmatched").Issues);
        try
        {
            var opened = await OpenC2ClassifiedSessionAsync(lease, initialCapture, allowAutomaticTransport: false);
            using var session = opened.Session;
            if (session is null)
                return Rejected(opened.Issues.Count != 0 ? opened.Issues :
                    AdapterFailure("spiritual_continuation_owner_unavailable").Issues);
            var current = await session.ReadContinuationProjectionAsync(lease, opened.Disposition, expectedRequest);
            if (current.Request is null || JsonSerializer.Serialize(current.Request) != JsonSerializer.Serialize(expectedRequest))
                return Rejected(AdapterFailure("spiritual_continuation_request_changed").Issues);
            var preservation = await session.ValidateContinuationImagesAsync(lease, expectedRequest.Phase, response,
                new Dictionary<string, byte[]>());
            if (preservation.Count != 0) return Rejected(preservation);
            if (current.Issues.Count == 0)
                return new(SpiritualWoundContinuationDisposition.Resolved, []);
            // The whole physical candidate has passed the issued A policy. Only now may
            // an unrestricted diagnostic walk reconstruct a later supported frontier B.
            var successor = await session.ReadContinuationProjectionAsync(lease, opened.Disposition, previewNext: true);
            if (successor.Request is { } next && successor.Issues.Count != 0 &&
                JsonSerializer.Serialize(next) != JsonSerializer.Serialize(expectedRequest))
                return new(SpiritualWoundContinuationDisposition.Advanced, successor.Issues);
            return Rejected(current.Issues);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidOperationException or FormatException or JsonException or OverflowException or DecoderFallbackException)
        {
            return Rejected(AdapterFailure("spiritual_continuation_proposal_invalid").Issues);
        }
    }

    /// <summary>
    /// Rejects an older envelope when an outstanding physical request names a different frontier.
    /// </summary>
    /// <param name="lease">
    /// Active lease covering the advisory transport read.
    /// </param>
    /// <param name="expectedRequest">
    /// Issued request independently authenticated by owner replay after this comparison.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for absent transport or the exact envelope; otherwise <see langword="false"/>.
    /// </returns>
    private async Task<bool> MatchesIssuedSpiritualContinuationAsync(FileSystemManager.CanonicalWriteLease lease,
        SpiritualWoundContinuationRequest expectedRequest)
    {
        try
        {
            var bytes = await _fs.ReadFileBytesAsync(lease, "game_state/control/validation_repair_request.json");
            if (bytes is null) return true;
            var root = SpiritualWoundDependentDraftPolicy.ReadStrictRoot(SpiritualWoundStateJson.DecodeUtf8JsonText(bytes));
            if (root[SpiritualWoundContinuationProtocol.EnvelopeName] is not { } envelope) return false;
            var issued = SpiritualWoundContinuationProtocol.ReadRequest(JsonSerializer.SerializeToElement(envelope));
            return JsonSerializer.Serialize(issued) == JsonSerializer.Serialize(expectedRequest);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or
            InvalidOperationException or FormatException or JsonException or DecoderFallbackException)
        {
            return false;
        }
    }

    internal sealed partial class SpiritualC2PrivateSession
    {
        private SpiritualWoundDependentDraftPolicy? _continuationDraftPolicy;

        /// <summary>
        /// Checks detached replacements and decision semantics using this session's genuine projection and private authority.
        /// </summary>
        /// <param name="lease">
        /// Active lease retained across candidate checking and physical postflight.
        /// </param>
        /// <param name="phase">
        /// Phase just rederived from this session's exact private pair.
        /// </param>
        /// <param name="response">
        /// Correlated decisions to compose against the retained genuine opportunity without consuming it.
        /// </param>
        /// <param name="proposedImages">
        /// Detached exact replacements already restricted to narrative and dependent conflict paths.
        /// </param>
        /// <returns>
        /// Preservation diagnostics, with no source advancement or physical writes.
        /// </returns>
        internal async Task<IReadOnlyList<ValidationIssue>> ValidateContinuationImagesAsync(
            FileSystemManager.CanonicalWriteLease lease, string phase,
            SpiritualWoundContinuationResponse response, IReadOnlyDictionary<string, byte[]> proposedImages)
        {
            await _submitGate.WaitAsync();
            try
            {
                if (_disposed || _used || phase == "dependent_draft" && _continuationDraftPolicy is null)
                    throw new InvalidOperationException("No current continuation comparison policy is retained.");
                var before = await _capture.ReadC2TransportWitnessAsync(lease);
                var valid = await _capture.ValidateC2TransportImagesAsync(lease, phase,
                    _continuationDraftPolicy, proposedImages, before);
                IReadOnlyList<ValidationIssue> issues = valid
                    ? [] : AdapterFailure("spiritual_continuation_proposal_not_preserved").Issues;
                if (valid && phase == "decision")
                {
                    if (_binding is null || _opportunity is null)
                        issues = AdapterFailure("spiritual_continuation_decision_authority_missing").Issues;
                    else
                    {
                        const string narrativePath = "output/narrative_response.json";
                        var bytes = proposedImages.TryGetValue(narrativePath, out var proposed)
                            ? proposed : before[narrativePath].Bytes!;
                        var raw = Encoding.UTF8.GetString(bytes);
                        if (raw.Length > 0 && raw[0] == '\ufeff') raw = raw[1..];
                        using var scene = JsonDocument.Parse(AcceptedTurnOutputProjector.ProjectNarrative(raw).Json);
                        var composed = WoundResponseInputComposer.Compose(_binding, [_opportunity],
                            response.WoundDecisions, scene.RootElement.GetProperty("response").GetString(), []);
                        issues = composed.Success ? [] : composed.Issues;
                        if (!composed.Success && issues.Count == 0)
                            issues = AdapterFailure("spiritual_continuation_decision_invalid").Issues;
                    }
                }
                if (!SameWitness(before, await _capture.ReadC2TransportWitnessAsync(lease)))
                    throw new InvalidOperationException("The continuation changed during proposal validation.");
                return issues;
            }
            finally { _submitGate.Release(); }
        }
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Compares the complete proposed draft with the genuine committed layer and saved decision.
        /// </summary>
        /// <param name="lease">
        /// Active lease for the current matched owner and exact private inputs.
        /// </param>
        /// <param name="phase">
        /// Authenticated decision or dependent_draft phase.
        /// </param>
        /// <param name="policy">
        /// Genuine raw dependent policy, or null for a decision without conflict edits.
        /// </param>
        /// <param name="proposedImages">
        /// Exact detached replacements; paths not supplied retain their physical images.
        /// </param>
        /// <param name="physicalImages">
        /// Current exact images read by this retained session immediately before validation.
        /// </param>
        /// <returns>
        /// True only for preserved inputs and permitted narrative or dependent changes;
        /// full mechanics validation of proposed costs remains a separate post-apply check.
        /// </returns>
        internal async Task<bool> ValidateC2TransportImagesAsync(FileSystemManager.CanonicalWriteLease lease,
            string phase, SpiritualWoundDependentDraftPolicy? policy,
            IReadOnlyDictionary<string, byte[]> proposedImages,
            IReadOnlyDictionary<string, CanonicalBeforeImage> physicalImages)
        {
            await _gate.WaitAsync();
            try
            {
                EnsureCurrent(lease);
                if (_matchedC2Pair is not { } pair || (await CheckRetainedInputsAsync(lease)).Count != 0 ||
                    (phase == "dependent_draft") != _c2PendingSubmission)
                    return false;
                var parsed = SpiritualWoundCaptureCheckpointState.Parse(DecodePhysicalRoot(pair.CheckpointBytes),
                    SpiritualWoundCaptureCheckpointState.StatePath, _draftInputs.PathInventory);
                if (!parsed.IsValid || parsed.State is not { } checkpoint) return false;
                var committed = ReadC2CommittedInputLayer(checkpoint);
                if (checkpoint.ReadPendingSubmission()?["command"] is JsonObject command)
                    committed[AcceptedMechanicsPlan.WoundCommandPath] = new(true,
                        Convert.FromBase64String(command["contentBase64"]!.GetValue<string>()));
                foreach (var path in _draftInputs.PathInventory)
                {
                    var image = proposedImages.TryGetValue(path, out var proposed)
                        ? new CanonicalBeforeImage(true, proposed) : physicalImages[path];
                    if (path == FixedOriginalOutputPaths[0])
                    {
                        if (image.Bytes is null) return false;
                        var raw = DecodePhysicalRoot(image.Bytes);
                        SpiritualWoundDependentDraftPolicy.ReadStrictRoot(raw);
                        if (!ValidC2DependentNarrative(pair.Pending, AcceptedTurnOutputProjector.ProjectNarrative(raw).Json))
                            return false;
                    }
                    else if (path == AfterlifeSpiritualConflictState.StatePath && phase == "dependent_draft")
                    {
                        if (policy is null || image.Bytes is null ||
                            !policy.Allows(SpiritualWoundDependentDraftPolicy.ReadStrictRoot(SpiritualWoundStateJson.DecodeUtf8JsonText(image.Bytes))))
                            return false;
                    }
                    else if (!SameExactImage(committed[path], image)) return false;
                }
                return true;
            }
            finally { _gate.Release(); }
        }
    }
}
