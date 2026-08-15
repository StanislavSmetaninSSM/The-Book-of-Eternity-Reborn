using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class EffectAcceptedTurnPlanCache
{
    private readonly object _gate = new();
    private readonly EffectIdentityFactory _identityFactory;
    private string? _fingerprint;
    private EffectAcceptedTurnPlanningResult? _result;
    private string? _validatedBindingFingerprint;
    private EffectAcceptedTurnPlanningResult? _validatedResult;

    internal EffectAcceptedTurnPlanCache()
        : this(new EffectIdentityFactory())
    {
    }

    internal EffectAcceptedTurnPlanCache(EffectIdentityFactory identityFactory)
    {
        _identityFactory = identityFactory ?? throw new ArgumentNullException(nameof(identityFactory));
    }

    internal EffectAcceptedTurnPlanningResult GetOrBuild(EffectAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fingerprint = CreateFingerprint(input);
        lock (_gate)
        {
            if (string.Equals(_fingerprint, fingerprint, StringComparison.Ordinal) && _result != null)
                return _result;
            var result = EffectAcceptedTurnPlanner.Build(input, fingerprint, _identityFactory);
            _fingerprint = fingerprint;
            _result = result;
            return result;
        }
    }

    internal EffectAcceptedTurnPlanningResult GetOrBuildValidated(
        EffectAcceptedTurnInput input)
    {
        var result = GetOrBuild(input);
        if (!result.Success)
        {
            lock (_gate)
            {
                _validatedBindingFingerprint = null;
                _validatedResult = null;
            }
            return result;
        }
        lock (_gate)
        {
            _validatedBindingFingerprint = CreateValidatedBindingFingerprint(
                input.SessionId,
                input.SnapshotToken,
                input.RawCommands,
                input.EventInput);
            _validatedResult = result;
        }
        return result;
    }

    internal bool TryGetValidated(
        string sessionId,
        string snapshotToken,
        JsonObject rawCommands,
        JsonObject eventInput,
        out EffectAcceptedTurnPlanningResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        ArgumentNullException.ThrowIfNull(rawCommands);
        ArgumentNullException.ThrowIfNull(eventInput);
        var fingerprint = CreateValidatedBindingFingerprint(
            sessionId,
            snapshotToken,
            rawCommands,
            eventInput);
        lock (_gate)
        {
            if (_validatedResult != null &&
                string.Equals(
                    _validatedBindingFingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
            {
                result = _validatedResult;
                return true;
            }
        }
        result = null!;
        return false;
    }

    internal void InvalidateValidated()
    {
        lock (_gate)
        {
            _validatedBindingFingerprint = null;
            _validatedResult = null;
        }
    }

    internal bool HasValidated()
    {
        lock (_gate)
            return _validatedResult != null;
    }

    internal bool TryPeekValidated(out EffectAcceptedTurnPlanningResult result)
    {
        lock (_gate)
        {
            if (_validatedResult != null)
            {
                result = _validatedResult;
                return true;
            }
        }
        result = null!;
        return false;
    }

    private static string CreateFingerprint(EffectAcceptedTurnInput input)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["sessionId"] = input.SessionId,
            ["snapshotToken"] = input.SnapshotToken,
            ["realm"] = input.Realm,
            ["rawCommands"] = input.RawCommands.DeepClone(),
            ["sourceAuthorityFingerprint"] = input.SourceAuthority.Fingerprint,
            ["targetAuthorityFingerprint"] = input.TargetAuthority.Fingerprint,
            ["eventInput"] = input.EventInput.DeepClone(),
            ["preTurnCarriers"] = CloneCarriers(input.PreTurnCarriers),
            ["preTurnIdentityIndex"] = input.PreTurnIdentityIndex?.DeepClone()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    private static string CreateValidatedBindingFingerprint(
        string sessionId,
        string snapshotToken,
        JsonObject rawCommands,
        JsonObject eventInput)
    {
        var root = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["snapshotToken"] = snapshotToken,
            ["rawCommands"] = rawCommands.DeepClone(),
            ["eventInput"] = eventInput.DeepClone()
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
    private static readonly ConditionalWeakTable<FileSystemManager, EffectAcceptedTurnPlanCache> Caches = new();

    internal static EffectAcceptedTurnPlanningResult GetOrBuild(
        FileSystemManager fileSystem,
        EffectAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return Caches.GetValue(fileSystem, static _ => new EffectAcceptedTurnPlanCache()).GetOrBuild(input);
    }

    internal static EffectAcceptedTurnPlanningResult GetOrBuildValidated(
        FileSystemManager fileSystem,
        EffectAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return Caches.GetValue(fileSystem, static _ => new EffectAcceptedTurnPlanCache())
            .GetOrBuildValidated(input);
    }

    internal static bool TryGetValidated(
        FileSystemManager fileSystem,
        string sessionId,
        string snapshotToken,
        JsonObject rawCommands,
        JsonObject eventInput,
        out EffectAcceptedTurnPlanningResult result)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return Caches.GetValue(fileSystem, static _ => new EffectAcceptedTurnPlanCache())
            .TryGetValidated(sessionId, snapshotToken, rawCommands, eventInput, out result);
    }

    internal static void InvalidateValidated(FileSystemManager fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        Caches.GetValue(fileSystem, static _ => new EffectAcceptedTurnPlanCache())
            .InvalidateValidated();
    }

    internal static bool HasValidated(FileSystemManager fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return Caches.GetValue(fileSystem, static _ => new EffectAcceptedTurnPlanCache())
            .HasValidated();
    }

    internal static bool TryPeekValidated(
        FileSystemManager fileSystem,
        out EffectAcceptedTurnPlanningResult result)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return Caches.GetValue(fileSystem, static _ => new EffectAcceptedTurnPlanCache())
            .TryPeekValidated(out result);
    }
}
