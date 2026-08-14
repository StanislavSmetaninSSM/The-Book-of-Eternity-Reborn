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
            var result = EffectAcceptedTurnPlanBuilder.Build(input, fingerprint, _identityFactory);
            _fingerprint = fingerprint;
            _result = result;
            return result;
        }
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
            ["eventInput"] = input.EventInput.DeepClone()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }
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
}
