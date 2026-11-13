using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Immutable expected DATA only. No owner, filesystem, lease, or admission capability.
    internal sealed class NpcProgressionSettlementWitness
    {
        private readonly JsonObject _originalCore;
        private readonly JsonObject? _originalSidecar;
        internal NpcCoreChangesContract.ProgressionSourceContext Context { get; }
        internal bool MatchesOriginalCore(JsonObject? actual) => JsonNode.DeepEquals(_originalCore, actual);
        internal bool MatchesOriginalSidecar(JsonObject? actual) => JsonNode.DeepEquals(_originalSidecar, actual);
        internal string PolicyFingerprint { get; }
        internal string? AuthorityFingerprint { get; }
        internal bool OriginalSidecarExists { get; }
        internal string? OriginalTradePending { get; }
        internal string? OriginalTrainingPending { get; }

        internal NpcProgressionSettlementWitness(NpcCoreChangesContract.ProgressionSourceContext context,
            JsonObject originalCore, string policyFingerprint, string? authorityFingerprint,
            JsonObject? originalSidecar, bool originalSidecarExists, string? originalTradePending,
            string? originalTrainingPending)
        {
            Context = context;
            _originalCore = originalCore.DeepClone().AsObject();
            _originalSidecar = originalSidecar?.DeepClone().AsObject();
            PolicyFingerprint = policyFingerprint;
            AuthorityFingerprint = authorityFingerprint;
            OriginalSidecarExists = originalSidecarExists;
            OriginalTradePending = originalTradePending;
            OriginalTrainingPending = originalTrainingPending;
        }
    }

    internal async Task RequireNpcProgressionWitnessAsync(FileSystemManager.CanonicalWriteLease lease,
        NpcProgressionSettlementWitness witness)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync(lease, requireFreshPhysicalSource: true);
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest is null ||
            CreateNpcProgressionSource(lookup.Manifest) != witness.Context)
            throw new InvalidDataException("NPC progression authenticated source changed before publication completed.");
        using var fresh = FileSystemManager.SuppressValidationReuse();
        var original = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest, NpcCoreChangesContract.NpcCorePath, lease);
        if (!TryReadNpcProgressionOriginal(original, out var originalRoot))
            throw new InvalidDataException("NPC progression authenticated original NPC core is missing or changed.");
        if (!witness.MatchesOriginalCore(originalRoot))
            throw new InvalidDataException("Authenticated original NPC core changed.");
        var currentAuthority = witness.AuthorityFingerprint is null
            ? await NpcCoreChangesContract.ReadProgressionPolicyAuthorityAsync(_fs)
            : await NpcCoreChangesContract.ReadAuthorityAsync(_fs);
        currentAuthority = currentAuthority with { ProgressionSource = witness.Context };
        if (!currentAuthority.NpcCharacteristicPolicy.IsValid ||
            !string.Equals(witness.PolicyFingerprint, MortalNpcWorldProgressionPolicy.Fingerprint(currentAuthority), StringComparison.Ordinal))
            throw new InvalidDataException("NPC progression current-world policy changed before publication completed.");
        if (witness.AuthorityFingerprint is not null)
        {
            if (!string.Equals(witness.AuthorityFingerprint,
                MortalNpcAcceptedSkillInput.ComputeAuthorityFingerprint(currentAuthority), StringComparison.Ordinal))
                throw new InvalidDataException("NPC skill current-world authority changed before publication completed.");
            var sidecar = await ReadAuthenticatedNpcSkillOriginalAsync(lookup.Manifest, lease);
            if (sidecar is null || sidecar.Exists != witness.OriginalSidecarExists ||
                !witness.MatchesOriginalSidecar(sidecar.Root))
                throw new InvalidDataException("Authenticated original NPC skill sidecar presence or content changed.");
            var trade = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, NpcTradeRequestState.PendingRequestPath, lease);
            var training = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, TrainingRequestState.PendingRequestPath, lease);
            if (trade != witness.OriginalTradePending || training != witness.OriginalTrainingPending)
                throw new InvalidDataException("Authenticated NPC skill dedicated pending authority changed.");
        }
    }
}
