using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentResourcePublicationTransaction
{
    private static readonly object CompletedItemValidationIssuer = new();
    private CompletedItemValidation? _completedItemValidation;

    /// <summary>
    /// Reads immutable comparison DATA exported only after finalized settlement and observed successful lease closure.
    /// This value carries no reservation, receipt or execution authority.
    /// </summary>
    /// <returns>The completed comparison, or null for every incomplete or unproved closure.</returns>
    internal CompletedItemValidation? ReadCompletedItemValidation() => Volatile.Read(ref _completedItemValidation);

    private CompletedItemValidation? CaptureCompletedItemValidation(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        try
        {
            files.EnsureCanonicalWriteLeaseActive(lease);
            var publication = _plan.TreatmentResourcePublicationAuthority;
            var item = publication?.ItemPublicationAuthority;
            if (!ReferenceEquals(files, _fileSystem) || _receipt.IsTerminalReleaseOnly ||
                !ReferenceEquals(_plan, _receipt.Plan) || !ReferenceEquals(publication, _receipt.PublicationAuthority) ||
                publication is null || !publication.HasValidSeal() || item is null || !item.HasValidSeal() ||
                _bindingFingerprint != AcceptedMechanicsPlanFingerprints.ComputeInput(_receipt.Binding) ||
                files.CanonicalRootAuthorityIdentity.SessionGenerationRevision != _receipt.SessionGenerationRevision)
                return null;
            var image = Volatile.Read(ref _publishedAgreement)?.SingleOrDefault(static value =>
                value.Path == NpcCoreChangesContract.NpcCorePath);
            if (image?.Bytes is not { } bytes || JsonNode.Parse(DecodeUtf8(bytes)) is not JsonObject root)
                return null;
            var comparison = item.CaptureExactPublishedNpcInventoryComparison(root);
            return comparison is null ? null : new CompletedItemValidation(CompletedItemValidationIssuer,
                files, _receipt.SessionGenerationRevision, _receipt.SessionGeneration, _receipt.Binding, bytes, root, comparison);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or
            JsonException or DecoderFallbackException or IOException)
        {
            // Settlement has already finalized. Failure to export DATA cannot compensate or spend again.
            return null;
        }
    }

    /// <summary>
    /// Non-deserializable, copied comparison DATA for the exact completed item publication and original frame.
    /// It cannot re-open an owner or authorize a mutation.
    /// </summary>
    internal sealed class CompletedItemValidation
    {
        private readonly FileSystemManager _files;
        private readonly long _generationRevision;
        private readonly string _generation;
        private readonly (string Session, string Request, int Turn, string Token) _frame;
        private readonly byte[] _npcBytes;
        private readonly JsonObject _npcRoot;
        private readonly MortalWoundTreatmentItemPublicationAuthority.NpcInventoryContinuityComparison _comparison;

        /// <summary>
        /// Restricts construction to the actual transaction's successful finalized path.
        /// </summary>
        /// <param name="issuer">Private transaction issuance identity.</param>
        /// <param name="files">Actual completed session owner.</param>
        /// <param name="generationRevision">Original session replacement generation revision.</param>
        /// <param name="generation">Exact session generation of the real finalized receipt.</param>
        /// <param name="binding">Immutable full input frame sealed by the actual publication.</param>
        /// <param name="npcBytes">Exact whole published physical NPC image.</param>
        /// <param name="npcRoot">Its parsed complete root.</param>
        /// <param name="comparison">The existing recomposed item before/after comparison.</param>
        internal CompletedItemValidation(object issuer, FileSystemManager files, long generationRevision, string generation,
            AcceptedMechanicsPlanBinding binding, byte[] npcBytes, JsonObject npcRoot,
            MortalWoundTreatmentItemPublicationAuthority.NpcInventoryContinuityComparison comparison)
        {
            if (!ReferenceEquals(issuer, CompletedItemValidationIssuer))
                throw new InvalidOperationException("Completed item comparison has no actual transaction issuer.");
            _files = files; _generationRevision = generationRevision; _generation = generation;
            _frame = (binding.SessionId, binding.RequestId, binding.Turn, binding.SnapshotToken);
            _npcBytes = npcBytes.ToArray(); _npcRoot = npcRoot.DeepClone().AsObject(); _comparison = comparison;
        }

        /// <summary>
        /// Binds one invocation's genuine original frame, parsed root and fresh whole physical bytes.
        /// </summary>
        /// <param name="files">Current session owner.</param>
        /// <param name="generation">Generation observed by the actual invocation read owner.</param>
        /// <param name="sessionId">Authenticated original session.</param>
        /// <param name="requestId">Authenticated original request.</param>
        /// <param name="turn">Authenticated original turn.</param>
        /// <param name="snapshotToken">Authenticated full original manifest/control seal.</param>
        /// <param name="parsedRoot">Actual root parsed in this validation invocation.</param>
        /// <param name="physicalBytes">Whole NPC bytes read under this invocation's existing lease.</param>
        /// <returns>True only for the exact completed image in the same original frame and generation.</returns>
        internal bool Matches(FileSystemManager files, string generation, string sessionId, string requestId, int turn,
            string snapshotToken, JsonObject parsedRoot, byte[]? physicalBytes) =>
            ReferenceEquals(_files, files) && files.CanonicalRootAuthorityIdentity.SessionGenerationRevision == _generationRevision &&
            _generation == generation && _frame.Session == sessionId && _frame.Request == requestId && _frame.Turn == turn &&
            _frame.Token == snapshotToken && physicalBytes is not null &&
            _npcBytes.AsSpan().SequenceEqual(physicalBytes) && JsonNode.DeepEquals(_npcRoot, parsedRoot);

        /// <summary>
        /// Finds exactly one ordinary inventory issue proved by the completed comparison; duplicates refuse.
        /// </summary>
        /// <param name="issues">Ordered issues from the same validation invocation.</param>
        /// <returns>The unique index, or -1 when no unique exact comparison exists.</returns>
        internal int FindUniqueIssue(IReadOnlyList<ValidationIssue> issues)
        {
            var match = -1;
            for (var index = 0; index < issues.Count; index++)
            {
                if (!_comparison.Proves(issues[index])) continue;
                if (match >= 0) return -1;
                match = index;
            }
            return match;
        }
    }
}
