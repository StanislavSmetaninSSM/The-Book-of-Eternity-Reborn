using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class MortalLocationAcceptedTurnPlanCache
{
    private readonly object _gate = new();
    private string? _fingerprint;
    private MortalLocationAcceptedTurnPlanningResult? _result;

    internal MortalLocationAcceptedTurnPlanningResult GetOrBuild(
        MortalLocationAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fingerprint = CreateFingerprint(input);
        lock (_gate)
        {
            if (string.Equals(_fingerprint, fingerprint, StringComparison.Ordinal) &&
                _result != null)
            {
                return _result;
            }

            var result = MortalLocationAcceptedTurnPlanner.Build(
                input,
                new MortalLocationIdentityFactory());
            _fingerprint = fingerprint;
            _result = result;
            return result;
        }
    }

    private static string CreateFingerprint(MortalLocationAcceptedTurnInput input)
    {
        var scope = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["turn"] = input.Turn,
            ["preTurnWorldMap"] = input.PreTurnWorldMap.DeepClone(),
            ["preTurnCurrentLocation"] = input.PreTurnCurrentLocation?.DeepClone(),
            ["preTurnIdentityIndex"] = input.PreTurnIdentityIndex.DeepClone(),
            ["rawCurrentLocationData"] = input.RawCurrentLocationData?.DeepClone(),
            ["rawWorldMapUpdates"] = input.RawWorldMapUpdates?.DeepClone(),
            ["bootstrapScaffold"] = input.BootstrapScaffold?.DeepClone(),
            ["rawNpcCore"] = input.RawNpcCore?.DeepClone(),
            ["rawFactionCore"] = input.RawFactionCore?.DeepClone(),
            ["preTurnStorageContents"] = input.PreTurnStorageContents?.DeepClone(),
            ["rawCurrentItemCarrier"] = input.RawCurrentItemCarrier?.DeepClone()
        };
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(scope.ToJsonString())));
    }
}

internal static class MortalLocationAcceptedTurnPlanAuthority
{
    private static readonly ConditionalWeakTable<
        FileSystemManager,
        MortalLocationAcceptedTurnPlanCache> Caches = new();

    internal static MortalLocationAcceptedTurnPlanningResult GetOrBuild(
        FileSystemManager fileSystem,
        MortalLocationAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return Caches.GetValue(
            fileSystem,
            static _ => new MortalLocationAcceptedTurnPlanCache()).GetOrBuild(input);
    }
}
