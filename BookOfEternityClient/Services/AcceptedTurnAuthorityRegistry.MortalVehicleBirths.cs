using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedTurnAuthorityRegistry
{
    /// <summary>Reuses ordinary allocation DATA only under a fresh lease/generation and exact source input.</summary>
    internal static MortalVehicleBirthPreparationResult PrepareMortalVehicleBirths(
        FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        string sessionId, string snapshotToken, JsonObject before, JsonObject raw) =>
        GetState(fs, lease).PrepareMortalVehicleBirths(sessionId, snapshotToken, before, raw);

    private sealed partial class AcceptedTurnAuthorityState
    {
        private string? _vehicleBirthInputFingerprint;
        private MortalVehicleBirthPreparationResult? _vehicleBirthPreparation;
        private readonly VehicleIdentityFactory _ordinaryVehicleFactory = new();

        internal MortalVehicleBirthPreparationResult PrepareMortalVehicleBirths(
            string sessionId, string snapshotToken, JsonObject before, JsonObject raw)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
            var input = new JsonArray(sessionId, snapshotToken, before.DeepClone(), raw.DeepClone());
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.ToJsonString())));
            lock (_gate)
            {
                if (_vehicleBirthPreparation is not null &&
                    string.Equals(_vehicleBirthInputFingerprint, fingerprint, StringComparison.Ordinal))
                    return _vehicleBirthPreparation;
                _vehicleBirthPreparation = null;
                _vehicleBirthInputFingerprint = null;
                var result = MortalVehicleBirthPreparation.Prepare(before, raw, _ordinaryVehicleFactory);
                if (result.IsValid)
                {
                    _vehicleBirthInputFingerprint = fingerprint;
                    _vehicleBirthPreparation = result;
                }
                return result;
            }
        }
    }
}
