using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Holds detached original narrative and interface projections; absent files remain absent.
    /// </summary>
    /// <param name="Narrative">
    /// Narrative projection, or <see langword="null"/> when the original file was absent.
    /// </param>
    /// <param name="Interface">
    /// Interface projection, or <see langword="null"/> when the original file was absent.
    /// </param>
    internal sealed record SpiritualOriginalOutputProjection(
        AcceptedOutputProjection? Narrative, AcceptedOutputProjection? Interface);

    /// <summary>
    /// Reports a retained original output projection or freshness issues.
    /// </summary>
    /// <param name="Projection">
    /// Retained projection, or <see langword="null"/> when freshness checking failed.
    /// </param>
    /// <param name="Issues">
    /// Validation issues preventing use of the retained projection.
    /// </param>
    internal sealed record SpiritualOriginalOutputProjectionResult(
        SpiritualOriginalOutputProjection? Projection, IReadOnlyList<ValidationIssue> Issues);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private static readonly string[] FixedOriginalOutputPaths =
        [
            "output/narrative_response.json",
            "output/interface_updates.json"
        ];

        /// <summary>
        /// Builds both projected images from retained original bytes in the initial allocation scope.
        /// </summary>
        /// <param name="inputs">
        /// Detached original draft inventory bound to the signed session, request, snapshot and turn.
        /// </param>
        /// <param name="clock">
        /// Attempt-owned clock used only when the original interface needs fallback time.
        /// </param>
        /// <returns>
        /// Immutable projections preserving absence separately from present empty content.
        /// </returns>
        private static SpiritualOriginalOutputProjection BuildOriginalOutputProjection(
            SpiritualOriginalDraftInputs inputs, SpiritualWoundProjectionClock clock)
        {
            var narrativeImage = inputs.ReadImage(FixedOriginalOutputPaths[0]);
            var interfaceImage = inputs.ReadImage(FixedOriginalOutputPaths[1]);
            var narrative = narrativeImage.Existed
                ? AcceptedTurnOutputProjector.ProjectNarrative(DecodeOriginalOutput(narrativeImage))
                : null;
            AcceptedOutputProjection? interfaceProjection = null;
            if (interfaceImage.Existed)
            {
                var json = DecodeOriginalOutput(interfaceImage);
                DateTimeOffset? fallback = null;
                if (AcceptedTurnOutputProjector.RequiresInterfaceTimestampFallback(json))
                {
                    var evidence = new JsonObject
                    {
                        ["sessionId"] = inputs.SessionId,
                        ["requestId"] = inputs.RequestId,
                        ["snapshotToken"] = inputs.SnapshotToken,
                        ["turn"] = inputs.Turn,
                        ["path"] = FixedOriginalOutputPaths[1],
                        ["originalImageFingerprint"] = interfaceImage.Fingerprint
                    };
                    fallback = clock.GetUtcNow(AcceptedTurnProjectionTimeKind.InterfaceOutputTimestamp, evidence);
                }
                interfaceProjection = AcceptedTurnOutputProjector.ProjectInterface(json, fallback);
            }
            return new(narrative, interfaceProjection);
        }

        /// <summary>
        /// Decodes an exact present original image using the ordinary filesystem read policy.
        /// </summary>
        /// <param name="image">
        /// Present retained image; absent images are rejected.
        /// </param>
        /// <returns>
        /// Decoded text with UTF-8 and byte-order-mark detection matching the ordinary reader.
        /// </returns>
        private static string DecodeOriginalOutput(CanonicalBeforeImage image)
        {
            if (!image.Existed)
                throw new ArgumentException("An absent original output cannot be decoded.", nameof(image));
            using var stream = new MemoryStream(image.Bytes!, writable: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Reads the named original output projection after validating capture ownership and freshness.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for the current capture.
        /// </param>
        /// <returns>
        /// The retained projection or input-change issues, without allocating another timestamp.
        /// </returns>
        internal async Task<SpiritualOriginalOutputProjectionResult> ReadOriginalOutputProjectionAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                EnsureCurrent(lease);
                if (_originalOutputProjection is null)
                    throw new InvalidOperationException("Legacy capture has no original output projection.");
                var issues = await CheckRetainedInputsAsync(lease);
                if (issues.Count != 0)
                    return new(null, issues);
                EnsureCurrent(lease);
                return new(_originalOutputProjection, Array.Empty<ValidationIssue>());
            }
            finally { _gate.Release(); }
        }
    }
}
