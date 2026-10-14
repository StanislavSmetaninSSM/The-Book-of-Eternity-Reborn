using System.Text;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedTurnAuthorityRegistry
{
    private static readonly object JointRepairReleaseIssuer = new();
    internal sealed record JointRepairReleasePreparation(MortalJointRepairReleaseToken? Token,
        IReadOnlyList<ValidationIssue> Issues);

    internal static async Task<JointRepairReleasePreparation> PrepareMortalJointRepairReleaseAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalJointRepairCheckpoint.RejectedCapture capture, string reason)
    {
        capture.EnsureCurrent(files, lease);
        if (!capture.Accepted.IsLeaseBoundTo(files, lease))
            return new(null, JointReleaseIssue("Terminal preparation requires its actual current participating accepted-state lease."));
        if (reason is not ("cancelled" or "validation_failed"))
            return new(null, JointReleaseIssue("Only cancelled or validation_failed can settle this confirmed pre-plan wave."));
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(capture.Resolution);
        if (!finalization.IsValid || finalization.Finalization is null)
            return new(null, finalization.Issues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue).ToArray());
        var guards = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in MortalJointRepairReleaseToken.GuardPaths)
            guards.Add(path, await files.ReadFileBytesAsync(lease, path));
        var checkpoint = capture.Checkpoint.ToJsonObject();
        if (!JointReleaseSame(guards[AcceptedMechanicsPlan.WoundCommandPath], capture.CommandBytes) ||
            guards[WoundAcceptedTurnSnapshotContract.PendingResolutionPath] is not { } pending ||
            !JsonNode.DeepEquals(JointReleaseRoot(pending), capture.Pending) ||
            // The actual baseline owner may have removed these public outputs
            // before dispatch. Their current absence grants no acquisition or
            // treatment authority: exact installed command/wave and retained
            // rejected-source provenance are still mandatory above.
            guards[MortalWoundOpportunityAdapter.DecisionPath] is { } draft &&
                !(capture.Packet.MatchesRejectedDecision(JointReleaseRoot(draft)) || capture.Packet.MatchesCorrectedDecision(JointReleaseRoot(draft),
                    JointReleaseRoot(Convert.FromBase64String(checkpoint["narrativeBase64"]!.GetValue<string>()))["response"]?.GetValue<string>())) ||
            guards["output/narrative_response.json"] is { } scene &&
                !JointReleaseSameScene(scene, Convert.FromBase64String(checkpoint["narrativeBase64"]!.GetValue<string>())) ||
            JointReleaseHash(guards[LiveTurnPreparationService.PendingTurnSnapshotManifestPath]) != checkpoint["originalManifestHash"]!.GetValue<string>() ||
            JointReleaseHash(guards[PendingTurnSnapshotAuthority.AuthorityPath]) != checkpoint["originalAuthorityHash"]!.GetValue<string>())
            return new(null, JointReleaseIssue("The entire current owned wave/command/draft/scene/original must agree before terminal preparation."));
        capture.EnsureCurrent(files, lease);
        return GetState(files, lease).PrepareMortalJointRepairRelease(files, lease, capture, finalization.Finalization, reason, guards);
    }

    internal static bool IsCurrentMortalJointRepairRelease(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, MortalJointRepairReleaseToken token) =>
        GetState(files, lease).IsCurrentMortalJointRepairRelease(files, lease, token);

    internal static MortalJointRepairPublicationSettlement CompleteMortalJointRepairRelease(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, MortalJointRepairReleaseToken token,
        MortalJointRepairReleaseToken.RemovalReadbackProof proof) =>
        GetState(files, lease).CompleteMortalJointRepairRelease(files, lease, token, proof);

    // This token owns terminal removal only. It is neither a common take receipt
    // nor accepted contribution/publication authority. No parsed checkpoint mints it.
    internal sealed class MortalJointRepairReleaseToken
    {
        internal static readonly string[] GuardPaths =
        [
            AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
            MortalWoundOpportunityAdapter.DecisionPath, "output/narrative_response.json",
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath
        ];
        private readonly FileSystemManager _files;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly Dictionary<string, byte[]?> _guards;
        private CanonicalLocalFileChange[]? _members;
        private RemovalReadbackProof? _readback;
        private int _batchCommitted;
        private int _consumed;
        internal MortalJointRepairReleaseToken(object issuer, FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            object stateToken, string generation, long revision, MortalJointRepairCheckpoint.RejectedCapture capture,
            MortalWoundTreatmentResourceFinalization finalization, MortalWoundTreatmentResourcePreparedRelease prepared,
            IReadOnlyDictionary<string, byte[]?> guards)
        {
            if (!ReferenceEquals(issuer, JointRepairReleaseIssuer)) throw new InvalidOperationException("Joint release requires its private registry issuer.");
            _files = files; _lease = lease; StateToken = stateToken; Generation = generation; Revision = revision;
            Capture = capture; Finalization = finalization; Prepared = prepared;
            _guards = guards.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);
        }
        internal object StateToken { get; }
        internal string Generation { get; }
        internal long Revision { get; }
        internal MortalJointRepairCheckpoint.RejectedCapture Capture { get; }
        internal MortalWoundTreatmentResourceFinalization Finalization { get; }
        internal MortalWoundTreatmentResourcePreparedRelease Prepared { get; }
        internal bool IsConsumed => Volatile.Read(ref _consumed) != 0;
        internal bool Owns(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
            ReferenceEquals(files, _files) && ReferenceEquals(lease, _lease) && !IsConsumed;
        internal bool Consume(object issuer) => ReferenceEquals(issuer, JointRepairReleaseIssuer) &&
            Interlocked.CompareExchange(ref _consumed, 1, 0) == 0;
        internal async Task ValidateBeforeRemovalAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            EnsureOwner(files, lease);
            foreach (var (path, expected) in _guards)
                if (!JointReleaseSame(await files.ReadFileBytesAsync(lease, path), expected))
                    throw new InvalidDataException("Joint terminal owned bytes changed: " + path);
            RequireCurrentClaim(files, lease);
        }
        internal void BindMembers(object issuer, IReadOnlyList<CanonicalLocalFileChange> members)
        {
            if (!GameEngine.IsMortalJointRepairTerminalBatchIssuer(issuer) || IsConsumed)
                throw new InvalidOperationException("Only the exact owning terminal rollback may bind removal members.");
            var byPath = members.ToDictionary(row => row.RelativePath, StringComparer.Ordinal);
            foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath, MortalWoundOpportunityAdapter.DecisionPath })
                if (!byPath.TryGetValue(path, out var member) || member.After is not null || !JointReleaseSame(member.Before, _guards[path]))
                    throw new InvalidDataException("Joint terminal batch must consume the entire exact owned graph: " + path);
            foreach (var (path, expected) in _guards)
                if (byPath.TryGetValue(path, out var member) &&
                    (!JointReleaseSame(member.Before, expected) || (path is LiveTurnPreparationService.PendingTurnSnapshotManifestPath or PendingTurnSnapshotAuthority.AuthorityPath) &&
                        !JointReleaseSame(member.After, expected)))
                    throw new InvalidDataException("Joint terminal before-image/original guard changed: " + path);
            var frozen = members.Select(row => new CanonicalLocalFileChange(row.RelativePath, row.Before?.ToArray(), row.After?.ToArray())).ToArray();
            if (Interlocked.CompareExchange(ref _members, frozen, null) is not null)
                throw new InvalidOperationException("Joint terminal member cohort can be bound only once.");
        }
        internal void ObserveCommitted(object issuer)
        {
            if (!GameEngine.IsMortalJointRepairTerminalBatchIssuer(issuer) || Volatile.Read(ref _members) is null)
                throw new InvalidOperationException("Joint terminal commit must come from its bound rollback publisher.");
            Volatile.Write(ref _batchCommitted, 1);
        }
        internal sealed class RemovalReadbackProof
        {
            private readonly MortalJointRepairReleaseToken _token;
            internal RemovalReadbackProof(object issuer, MortalJointRepairReleaseToken token)
            {
                if (!ReferenceEquals(issuer, JointRepairReleaseIssuer)) throw new InvalidOperationException("Joint terminal readback requires its owning token.");
                _token = token;
            }
            internal bool Agrees(MortalJointRepairReleaseToken token) => ReferenceEquals(_token, token) &&
                ReferenceEquals(token._readback, this) && Volatile.Read(ref token._batchCommitted) == 1 && !token.IsConsumed;
        }
        internal async Task<RemovalReadbackProof> ValidateRemovalReadbackAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            EnsureOwner(files, lease);
            var members = Volatile.Read(ref _members) ?? throw new InvalidOperationException("Joint terminal cohort is not bound.");
            if (Volatile.Read(ref _batchCommitted) != 1) throw new InvalidDataException("Joint terminal removal is not known committed.");
            foreach (var member in members)
                if (!JointReleaseSame(await files.ReadFileBytesAsync(lease, member.RelativePath), member.After))
                    throw new InvalidDataException("Joint terminal readback differs: " + member.RelativePath);
            await ValidateRetainedGuardsAsync(files, lease, members);
            RequireInventory(files, lease, members, after: true);
            RequireCurrentClaim(files, lease);
            return _readback = new RemovalReadbackProof(JointRepairReleaseIssuer, this);
        }
        internal async Task CompensateRemovalAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            EnsureOwner(files, lease); RequireCurrentClaim(files, lease);
            _readback = null;
            var members = Volatile.Read(ref _members) ?? throw new InvalidOperationException("Joint terminal compensation has no bound cohort.");
            await ValidateRetainedGuardsAsync(files, lease, members);
            var restore = new List<CanonicalLocalFileChange>();
            foreach (var member in members)
            {
                var actual = await files.ReadFileBytesAsync(lease, member.RelativePath);
                if (!JointReleaseSame(actual, member.Before) && !JointReleaseSame(actual, member.After))
                    throw new InvalidDataException("Joint terminal compensation refuses unknown bytes: " + member.RelativePath);
                if (!JointReleaseSame(actual, member.Before)) restore.Add(new(member.RelativePath, actual, member.Before));
            }
            RequireNoUnlistedRollbackPaths(files, lease, members);
            RequireCurrentClaim(files, lease);
            if (restore.Count != 0)
                files.RequireCommittedLocalPublication(await files.PublishLocalFilesAsync(lease, restore,
                    validatePreparedNamespace: () =>
                    {
                        EnsureOwner(files, lease); RequireNoUnlistedRollbackPaths(files, lease, members);
                        RequireCurrentClaim(files, lease); return Task.CompletedTask;
                    }));
            foreach (var member in members)
                if (!JointReleaseSame(await files.ReadFileBytesAsync(lease, member.RelativePath), member.Before))
                    throw new InvalidDataException("Joint terminal compensation readback differs: " + member.RelativePath);
            RequireInventory(files, lease, members, after: false);
            await ValidateBeforeRemovalAsync(files, lease);
        }
        private static void RequireNoUnlistedRollbackPaths(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            IReadOnlyList<CanonicalLocalFileChange> members)
        {
            var paths = members.Select(row => row.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (GameEngine.EnumerateOriginalBrowserRollbackTrackedFiles(files.EnumerateFiles(lease, "*").ToArray()).Any(path => !paths.Contains(path)))
                throw new InvalidDataException("Joint terminal compensation refuses an undeclared rollback member.");
        }
        private static void RequireInventory(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            IReadOnlyList<CanonicalLocalFileChange> members, bool after)
        {
            var paths = members.Where(row => (after ? row.After : row.Before) is not null)
                .Select(row => row.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!paths.SetEquals(GameEngine.EnumerateOriginalBrowserRollbackTrackedFiles(files.EnumerateFiles(lease, "*").ToArray())))
                throw new InvalidDataException("Joint terminal complete rollback inventory differs.");
        }
        private async Task ValidateRetainedGuardsAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            IReadOnlyList<CanonicalLocalFileChange> members)
        {
            var produced = members.Select(row => row.RelativePath).ToHashSet(StringComparer.Ordinal);
            foreach (var (path, expected) in _guards)
                if (!produced.Contains(path) && !JointReleaseSame(await files.ReadFileBytesAsync(lease, path), expected))
                    throw new InvalidDataException("Joint terminal retained original guard changed: " + path);
        }
        private void EnsureOwner(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            files.EnsureCanonicalWriteLeaseActive(lease); Capture.EnsureCurrent(files, lease);
            if (!Owns(files, lease) || !files.IsCurrentSessionGeneration(lease, Generation))
                throw new InvalidDataException("Joint terminal token/issuing lease/G changed.");
        }
        private void RequireCurrentClaim(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            if (!IsCurrentMortalJointRepairRelease(files, lease, this))
                throw new InvalidDataException("Joint terminal token no longer owns the exact still-confirmed agreement.");
        }
    }

    private sealed partial class AcceptedTurnAuthorityState
    {
        private MortalJointRepairReleaseToken? _openJointRepairReleaseToken;
        internal JointRepairReleasePreparation PrepareMortalJointRepairRelease(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, MortalJointRepairCheckpoint.RejectedCapture capture,
            MortalWoundTreatmentResourceFinalization finalization, string reason, IReadOnlyDictionary<string, byte[]?> guards)
        {
            lock (_gate)
            {
                if (!JointReleasePrePlan() || _openJointRepairReleaseToken is not null ||
                    !ReferenceEquals(_mortalWoundTreatmentAcceptedState, capture.Accepted))
                    return new(null, JointReleaseIssue("Terminal release requires the current pre-plan joint owner."));
                var hold = _treatmentResources.ProbeConfirmed(TreatmentResourceRegistryCapability, capture.Request, finalization);
                if (!hold.IsValid) return new(null, JointHoldIssues(hold));
                var prepared = _treatmentResources.PrepareActiveRelease(TreatmentResourceRegistryCapability, capture.Request, reason);
                if (!prepared.IsValid || prepared.PreparedRelease is null)
                    return new(null, prepared.Issues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue).ToArray());
                var token = new MortalJointRepairReleaseToken(JointRepairReleaseIssuer, files, lease, _authorityStateToken,
                    _sessionGeneration, _sessionGenerationRevision, capture, finalization, prepared.PreparedRelease, guards);
                _openJointRepairReleaseToken = token;
                return new(token, Array.Empty<ValidationIssue>());
            }
        }
        internal bool IsCurrentMortalJointRepairRelease(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, MortalJointRepairReleaseToken token)
        { lock (_gate) { return JointReleaseCurrent(files, lease, token); } }
        internal MortalJointRepairPublicationSettlement CompleteMortalJointRepairRelease(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, MortalJointRepairReleaseToken token,
            MortalJointRepairReleaseToken.RemovalReadbackProof proof)
        {
            lock (_gate)
            {
                if (!JointReleaseCurrent(files, lease, token) || !proof.Agrees(token))
                    return new(MortalJointRepairPublicationDisposition.PublicationUncertain, JointReleaseIssue("Terminal removal/claim/token agreement changed."));
                if (!_treatmentResources.ReleasePreparedUnchecked(TreatmentResourceRegistryCapability, token.Prepared))
                    return new(MortalJointRepairPublicationDisposition.PublicationUncertain, JointReleaseIssue("The exact prepared agreement could not be released."));
                if (!token.Consume(JointRepairReleaseIssuer)) throw new InvalidOperationException("Joint terminal token was consumed twice.");
                _openJointRepairReleaseToken = null;
                _commonPlan.InvalidateAll(); _mortalItems.InvalidateValidated();
                ClearMortalWoundTreatmentPublicationReservationCore();
                return new(MortalJointRepairPublicationDisposition.TerminalReleased, Array.Empty<ValidationIssue>());
            }
        }
        private bool JointReleasePrePlan() => _mortalWoundTreatmentPublicationReservation is null &&
            _openTreatmentPublicationReceipt is null && _openJointRepairPublicationReceipt is null &&
            _validatedJointRepairCommonAuthority is null && _validatedJointRepairCommonPlan is null &&
            !_commonPlan.HasPlanOrPublicationForJointRelease;
        private bool JointReleaseCurrent(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalJointRepairReleaseToken token)
        {
            if (!JointReleasePrePlan() || !ReferenceEquals(_openJointRepairReleaseToken, token) || !token.Owns(files, lease) ||
                !ReferenceEquals(token.StateToken, _authorityStateToken) || token.Generation != _sessionGeneration || token.Revision != _sessionGenerationRevision ||
                !ReferenceEquals(_mortalWoundTreatmentAcceptedState, token.Capture.Accepted)) return false;
            var hold = _treatmentResources.ProbeConfirmed(TreatmentResourceRegistryCapability, token.Capture.Request, token.Finalization);
            if (!hold.IsValid) return false;
            var prepared = _treatmentResources.PrepareActiveRelease(TreatmentResourceRegistryCapability, token.Capture.Request, token.Prepared.Reason);
            return prepared.IsValid && prepared.PreparedRelease is { } current && ReferenceEquals(current.Agreement, token.Prepared.Agreement);
        }
    }
    private static JsonObject JointReleaseRoot(byte[] bytes) => StrictJsonAuthority.Deserialize<JsonObject>(
        new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "joint terminal owned input") ??
        throw new InvalidDataException("Joint terminal owned input must be an object.");
    private static bool JointReleaseSameScene(byte[] current, byte[] rejected)
    {
        var actual = JointReleaseRoot(current); var expected = JointReleaseRoot(rejected);
        actual.Remove("timestamp"); expected.Remove("timestamp");
        return JsonNode.DeepEquals(actual, expected);
    }
    private static string? JointReleaseHash(byte[]? bytes) => bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
    private static bool JointReleaseSame(byte[]? left, byte[]? right) => left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
    private static IReadOnlyList<ValidationIssue> JointReleaseIssue(string reason) =>
        [MortalWoundOpportunityAdapter.PrivateSourceIssue(WoundAcceptedTurnPlannerCore.NewIssue(
            "mortal_joint_repair_terminal_release_refused", reason, "one exact pre-plan source-bound owned wave and confirmed claim", "blocked"))];
}
