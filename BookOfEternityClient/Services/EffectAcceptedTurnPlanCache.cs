using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal readonly record struct EffectAcceptedTurnPlanBinding(
    string SessionId,
    string SnapshotToken);

internal delegate EffectAcceptedTurnPlanningResult EffectAcceptedTurnPlanFactory(
    EffectAcceptedTurnInput input,
    string fingerprint,
    EffectIdentityFactory identityFactory);

internal delegate EffectAcceptedTurnPlanningResult
    WoundEffectAcceptedTurnPlanFactory(
        EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared,
        EffectIdentityFactory identityFactory);

internal sealed class EffectAcceptedTurnPlanCache
{
    private readonly object _gate = new();
    private readonly EffectIdentityFactory _identityFactory;
    private readonly EffectAcceptedTurnPlanFactory _planner;
    private readonly WoundEffectAcceptedTurnPlanFactory _woundPlanner;
    private string? _fingerprint;
    private EffectIdentityFactory? _cachedIdentityFactory;
    private EffectAcceptedTurnPlanningResult? _result;
    private EffectAcceptedTurnPlanBinding? _validatedBinding;
    private EffectAcceptedTurnPlanningResult? _validatedResult;

    internal EffectAcceptedTurnPlanCache()
        : this(new EffectIdentityFactory())
    {
    }

    internal EffectAcceptedTurnPlanCache(EffectIdentityFactory identityFactory)
        : this(
            identityFactory,
            EffectAcceptedTurnPlanner.Build,
            EffectAcceptedTurnPlanner.BuildWoundBatch)
    {
    }

    internal EffectAcceptedTurnPlanCache(
        EffectIdentityFactory identityFactory,
        EffectAcceptedTurnPlanFactory planner,
        WoundEffectAcceptedTurnPlanFactory woundPlanner)
    {
        _identityFactory = identityFactory ?? throw new ArgumentNullException(nameof(identityFactory));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _woundPlanner = woundPlanner ??
            throw new ArgumentNullException(nameof(woundPlanner));
    }

    internal EffectAcceptedTurnPlanningResult GetOrBuild(
        EffectAcceptedTurnInput input) =>
        GetOrBuild(input, out _);

    /// <summary>
    /// Builds an ordinary effect plan or reuses the exact input and allocation scope.
    /// </summary>
    /// <param name="input">
    /// Complete non-null input checked by the ordinary planner.
    /// </param>
    /// <param name="reused">
    /// Receives <see langword="true"/> only for a cached result from the same input and factory instance.
    /// </param>
    /// <param name="identityFactory">
    /// Capture-owned allocation policy; <see langword="null"/> selects the constructor policy.
    /// </param>
    /// <returns>
    /// Actual planner result without adding factory identity to serialized fingerprints.
    /// </returns>
    internal EffectAcceptedTurnPlanningResult GetOrBuild(
        EffectAcceptedTurnInput input,
        out bool reused,
        EffectIdentityFactory? identityFactory = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var selectedFactory = identityFactory ?? _identityFactory;
        var fingerprint = CreateFingerprint(input);
        return GetOrBuildCore(
            fingerprint,
            selectedFactory,
            () => _planner(
                input,
                fingerprint,
                selectedFactory),
            out reused);
    }

    /// <summary>
    /// Builds the prepared wound effect batch within one allocation scope and retains its validated handoff.
    /// </summary>
    /// <param name="prepared">
    /// Non-null preparation whose existing fingerprint binds the wound batch.
    /// </param>
    /// <param name="input">
    /// Complete non-null effect input for that preparation.
    /// </param>
    /// <param name="reused">
    /// Receives <see langword="true"/> only when both input fingerprint and factory instance match.
    /// </param>
    /// <param name="identityFactory">
    /// Capture-owned allocation policy; <see langword="null"/> selects the constructor policy.
    /// </param>
    /// <returns>
    /// The real wound planner result, also retained as the validated result on success.
    /// </returns>
    internal EffectAcceptedTurnPlanningResult GetOrBuildWoundValidated(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput input,
        out bool reused,
        EffectIdentityFactory? identityFactory = null)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(input);
        var selectedFactory = identityFactory ?? _identityFactory;
        var fingerprint = WoundAcceptedTurnFingerprints.ComputeEffectInput(
            prepared,
            input);
        var result = GetOrBuildCore(
            fingerprint,
            selectedFactory,
            () => _woundPlanner(
                input,
                prepared,
                selectedFactory),
            out reused);
        return SetValidated(input, result);
    }

    /// <summary>
    /// Serializes cache access and binds a retained result to its actual allocation factory.
    /// </summary>
    /// <param name="fingerprint">
    /// Existing complete planner-input fingerprint.
    /// </param>
    /// <param name="identityFactory">
    /// Non-null selected allocation factory, compared by reference only.
    /// </param>
    /// <param name="planner">
    /// Actual construction callback invoked only on a cache miss.
    /// </param>
    /// <param name="reused">
    /// Receives whether the retained input and allocation scope matched.
    /// </param>
    /// <returns>
    /// Cached or newly constructed result, including planner validation failures.
    /// </returns>
    private EffectAcceptedTurnPlanningResult GetOrBuildCore(
        string fingerprint,
        EffectIdentityFactory identityFactory,
        Func<EffectAcceptedTurnPlanningResult> planner,
        out bool reused)
    {
        reused = false;
        lock (_gate)
        {
            if (string.Equals(_fingerprint, fingerprint, StringComparison.Ordinal) && _result != null &&
                ReferenceEquals(_cachedIdentityFactory, identityFactory))
            {
                reused = true;
                return _result;
            }
            var result = planner() ?? throw new InvalidOperationException(
                "Effect accepted-turn planner returned null.");
            _fingerprint = fingerprint;
            _cachedIdentityFactory = identityFactory;
            _result = result;
            return result;
        }
    }

    internal EffectAcceptedTurnPlanningResult GetOrBuildValidated(
        EffectAcceptedTurnInput input) =>
        GetOrBuildValidated(input, out _);

    /// <summary>
    /// Builds an ordinary effect plan within the selected scope and retains its validated handoff.
    /// </summary>
    /// <param name="input">
    /// Complete non-null effect input.
    /// </param>
    /// <param name="reused">
    /// Receives whether the exact input and allocation factory reused the retained result.
    /// </param>
    /// <param name="identityFactory">
    /// Capture-owned allocation policy; <see langword="null"/> selects the constructor policy.
    /// </param>
    /// <returns>
    /// The ordinary planner result; failure clears the validated handoff.
    /// </returns>
    internal EffectAcceptedTurnPlanningResult GetOrBuildValidated(
        EffectAcceptedTurnInput input,
        out bool reused,
        EffectIdentityFactory? identityFactory = null)
    {
        var result = GetOrBuild(input, out reused, identityFactory);
        return SetValidated(input, result);
    }

    private EffectAcceptedTurnPlanningResult SetValidated(
        EffectAcceptedTurnInput input,
        EffectAcceptedTurnPlanningResult result)
    {
        if (!result.Success)
        {
            lock (_gate)
            {
                _validatedBinding = null;
                _validatedResult = null;
            }
            return result;
        }
        lock (_gate)
        {
            _validatedBinding = new EffectAcceptedTurnPlanBinding(
                input.SessionId,
                input.SnapshotToken);
            _validatedResult = result;
        }
        return result;
    }

    internal void InvalidateValidated()
    {
        lock (_gate)
        {
            _validatedBinding = null;
            _validatedResult = null;
        }
    }

    /// <summary>
    /// Discards the cached input, allocation scope, result and validated handoff.
    /// </summary>
    internal void InvalidateAll()
    {
        lock (_gate)
        {
            _fingerprint = null;
            _cachedIdentityFactory = null;
            _result = null;
            _validatedBinding = null;
            _validatedResult = null;
        }
    }

    internal bool TryPeekValidated(
        out EffectAcceptedTurnPlanBinding binding,
        out EffectAcceptedTurnPlanningResult result)
    {
        lock (_gate)
        {
            if (_validatedBinding is { } validatedBinding &&
                _validatedResult != null)
            {
                binding = validatedBinding;
                result = _validatedResult;
                return true;
            }
        }
        binding = default;
        result = null!;
        return false;
    }

    internal bool TryPeekValidated(out EffectAcceptedTurnPlanningResult result)
    {
        return TryPeekValidated(out _, out result);
    }

    private static string CreateFingerprint(EffectAcceptedTurnInput input)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 4,
            ["sessionId"] = input.SessionId,
            ["snapshotToken"] = input.SnapshotToken,
            ["realm"] = input.Realm,
            ["rawCommands"] = input.RawCommands.DeepClone(),
            ["sourceAuthorityFingerprint"] = input.SourceAuthority.Fingerprint,
            ["targetAuthorityFingerprint"] = input.TargetAuthority.Fingerprint,
            ["skillScopeAuthorityFingerprint"] = input.SkillScopeAuthority?.Fingerprint ?? "none",
            ["woundApplicationLocationsFingerprint"] = input.WoundApplicationLocations?.Fingerprint ?? "none",
            ["eventInput"] = input.EventInput.DeepClone(),
            ["preTurnCarriers"] = CloneCarriers(input.PreTurnCarriers),
            ["acceptedCarrierBaselines"] = CloneCarriers(
                input.AcceptedCarrierBaselines),
            ["publicationCarrierBaselines"] = CloneCarriers(
                input.PublicationCarrierBaselines),
            ["preTurnIdentityIndex"] = input.PreTurnIdentityIndex?.DeepClone()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    private static JsonObject? CloneCarriers(EffectCarrierCatalogInput? carriers) =>
        carriers == null
            ? null
            : new JsonObject
            {
                ["playerEffects"] = carriers.PlayerEffects?.DeepClone(),
                ["npcEffects"] = carriers.NpcEffects?.DeepClone(),
                ["enemyCombatants"] = carriers.EnemyCombatants?.DeepClone(),
                ["allyCombatants"] = carriers.AllyCombatants?.DeepClone(),
                ["afterlifeProfiles"] = carriers.AfterlifeProfiles?.DeepClone(),
                ["spiritualConflict"] = carriers.SpiritualConflict?.DeepClone()
            };
}

internal static class EffectAcceptedTurnPlanAuthority
{
    /// <summary>
    /// Builds a registry-owned effect stage under the active write lease.
    /// </summary>
    /// <param name="fileSystem">
    /// Filesystem whose private registry owns the plan.
    /// </param>
    /// <param name="writeLease">
    /// Active canonical write lease for registry access.
    /// </param>
    /// <param name="input">
    /// Complete input passed through the existing owner validation.
    /// </param>
    /// <param name="identityFactory">
    /// Allocation policy for this attempt, or <see langword="null"/> to retain the ordinary cache policy.
    /// </param>
    /// <returns>
    /// The validated result or owner diagnostics.
    /// </returns>
    internal static EffectAcceptedTurnPlanningResult GetOrBuildValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        EffectAcceptedTurnInput input, EffectIdentityFactory? identityFactory = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.GetOrBuildEffectValidated(
            fileSystem,
            writeLease,
            input, identityFactory);
    }

    internal static void InvalidateValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        AcceptedTurnAuthorityRegistry.InvalidateEffectValidated(
            fileSystem,
            writeLease);
    }

    internal static bool TryPeekValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        out EffectAcceptedTurnPlanBinding binding,
        out EffectAcceptedTurnPlanningResult result)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.TryPeekEffectValidated(
            fileSystem,
            writeLease,
            out binding,
            out result);
    }

    internal static bool TryPeekValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        out EffectAcceptedTurnPlanningResult result)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.TryPeekEffectValidated(
            fileSystem,
            writeLease,
            out result);
    }

}
