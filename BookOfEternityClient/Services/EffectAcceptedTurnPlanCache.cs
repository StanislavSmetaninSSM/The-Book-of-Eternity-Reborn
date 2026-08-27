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

    internal EffectAcceptedTurnPlanningResult GetOrBuild(
        EffectAcceptedTurnInput input,
        out bool reused)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fingerprint = CreateFingerprint(input);
        return GetOrBuildCore(
            fingerprint,
            () => _planner(
                input,
                fingerprint,
                _identityFactory),
            out reused);
    }

    internal EffectAcceptedTurnPlanningResult GetOrBuildWoundValidated(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput input,
        out bool reused)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(input);
        var fingerprint = WoundAcceptedTurnFingerprints.ComputeEffectInput(
            prepared,
            input);
        var result = GetOrBuildCore(
            fingerprint,
            () => _woundPlanner(
                input,
                prepared,
                _identityFactory),
            out reused);
        return SetValidated(input, result);
    }

    private EffectAcceptedTurnPlanningResult GetOrBuildCore(
        string fingerprint,
        Func<EffectAcceptedTurnPlanningResult> planner,
        out bool reused)
    {
        reused = false;
        lock (_gate)
        {
            if (string.Equals(_fingerprint, fingerprint, StringComparison.Ordinal) && _result != null)
            {
                reused = true;
                return _result;
            }
            var result = planner() ?? throw new InvalidOperationException(
                "Effect accepted-turn planner returned null.");
            _fingerprint = fingerprint;
            _result = result;
            return result;
        }
    }

    internal EffectAcceptedTurnPlanningResult GetOrBuildValidated(
        EffectAcceptedTurnInput input) =>
        GetOrBuildValidated(input, out _);

    internal EffectAcceptedTurnPlanningResult GetOrBuildValidated(
        EffectAcceptedTurnInput input,
        out bool reused)
    {
        var result = GetOrBuild(input, out reused);
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

    internal void InvalidateAll()
    {
        lock (_gate)
        {
            _fingerprint = null;
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
            ["schemaVersion"] = 3,
            ["sessionId"] = input.SessionId,
            ["snapshotToken"] = input.SnapshotToken,
            ["realm"] = input.Realm,
            ["rawCommands"] = input.RawCommands.DeepClone(),
            ["sourceAuthorityFingerprint"] = input.SourceAuthority.Fingerprint,
            ["targetAuthorityFingerprint"] = input.TargetAuthority.Fingerprint,
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
    internal static EffectAcceptedTurnPlanningResult GetOrBuildValidated(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        EffectAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        return AcceptedTurnAuthorityRegistry.GetOrBuildEffectValidated(
            fileSystem,
            writeLease,
            input);
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
