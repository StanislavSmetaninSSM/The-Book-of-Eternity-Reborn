using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services;

// A detached value, never a command, publication receipt or continuation capability.
// Only the private common owner captures it from its already sealed continuation.
internal sealed class MortalJointAcceptedOutputBasis
{
    private static readonly string[] Fields = ["version", "localWoundRef", "woundId", "transitionId", "narrativeBase64", "narrativeHash"];
    private readonly byte[] _narrative;
    private MortalJointAcceptedOutputBasis(string localRef, string woundId, string transitionId, byte[] narrative)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(localRef) || string.IsNullOrWhiteSpace(woundId) || string.IsNullOrWhiteSpace(transitionId))
            throw new InvalidDataException("joint_output_binding_coordinates");
        RequireNormalizedNarrative(narrative);
        LocalWoundRef = localRef; WoundId = woundId; TransitionId = transitionId; _narrative = narrative.ToArray();
    }
    internal string LocalWoundRef { get; }
    internal string WoundId { get; }
    internal string TransitionId { get; }
    internal byte[] NarrativeBytes => _narrative.ToArray();
    internal string Fingerprint => WoundAcceptedTurnFingerprintWriter.Compute(new[] {
        "book_of_eternity.mortal_joint_repair.output_basis.v1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(ToJson()) });
    internal JsonObject ToJson() => new() {
        ["version"] = 1, ["localWoundRef"] = LocalWoundRef, ["woundId"] = WoundId, ["transitionId"] = TransitionId,
        ["narrativeBase64"] = Convert.ToBase64String(_narrative), ["narrativeHash"] = Convert.ToHexString(SHA256.HashData(_narrative)) };
    internal static MortalJointAcceptedOutputBasis Capture(string localRef, string woundId, string transitionId, byte[] narrative) =>
        new(localRef, woundId, transitionId, narrative);
    internal static MortalJointAcceptedOutputBasis Read(JsonNode? node)
    {
        try
        {
            if (node is not JsonObject root || root.Count != Fields.Length || root.Any(row => !Fields.Contains(row.Key, StringComparer.Ordinal)) ||
                root["version"] is not JsonValue version || !version.TryGetValue<int>(out var number) || number != 1)
                throw new InvalidDataException("joint_output_binding_shape");
            string Text(string key) => root[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
                ? text : throw new InvalidDataException("joint_output_binding_" + key);
            var encoded = Text("narrativeBase64");
            var bytes = Convert.FromBase64String(encoded);
            if (Convert.ToBase64String(bytes) != encoded || Text("narrativeHash") != Convert.ToHexString(SHA256.HashData(bytes)))
                throw new InvalidDataException("joint_output_binding_digest");
            return new(Text("localWoundRef"), Text("woundId"), Text("transitionId"), bytes);
        }
        catch (Exception error) when (error is FormatException or InvalidOperationException or System.Text.Json.JsonException or System.Text.DecoderFallbackException)
        { throw new InvalidDataException("joint_output_binding_invalid", error); }
    }
    internal static void RequireNormalizedNarrative(byte[] bytes)
    {
        var text = CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
        var root = StrictJsonAuthority.Deserialize<JsonObject>(text, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed,
            "joint exact accepted narrative") ?? throw new InvalidDataException("joint_output_scene_null");
        if (root["response"] is not JsonValue response || !response.TryGetValue<string>(out var scene) || string.IsNullOrWhiteSpace(scene) ||
            root["timestamp"] is not JsonValue timestamp || !timestamp.TryGetValue<string>(out var stamp) ||
            string.IsNullOrWhiteSpace(stamp) || !DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            root.Any(row => row.Key is not ("response" or "timestamp") && !row.Key.StartsWith('_')))
            throw new InvalidDataException("joint_output_scene_shape");
        if (AcceptedTurnOutputProjector.ProjectNarrative(text).Changed)
            throw new InvalidDataException("joint_output_scene_requires_normalization_before_checkpoint");
    }
}
