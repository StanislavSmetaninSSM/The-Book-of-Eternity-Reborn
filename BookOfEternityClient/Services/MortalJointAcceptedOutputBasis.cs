using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

// A detached value, never a command, publication receipt or continuation capability.
// Only the private common owner captures it from its already sealed continuation.
internal sealed class MortalJointAcceptedOutputBasis
{
    private static readonly string[] Fields = ["version", "localWoundRef", "woundId", "transitionId", "narrativeBase64", "narrativeHash"];
    private readonly byte[] _narrative;
    private readonly MortalJointInterfacePublication.Record? _interface;
    private MortalJointAcceptedOutputBasis(string localRef, string woundId, string transitionId, byte[] narrative,
        MortalJointInterfacePublication.Record? publication = null)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(localRef) || string.IsNullOrWhiteSpace(woundId) || string.IsNullOrWhiteSpace(transitionId))
            throw new InvalidDataException("joint_output_binding_coordinates");
        RequireNormalizedNarrative(narrative);
        if (publication is { } result && (result.State != "Published" || result.HasErrors))
            throw new InvalidDataException("joint_output_binding_requires_published_no_error_data");
        _interface = publication is null ? null : MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(publication.ToJson()));
        LocalWoundRef = localRef; WoundId = woundId; TransitionId = transitionId; _narrative = narrative.ToArray();
    }
    internal string LocalWoundRef { get; }
    internal string WoundId { get; }
    internal string TransitionId { get; }
    internal byte[] NarrativeBytes => _narrative.ToArray();
    internal MortalJointInterfacePublication.Record? InterfacePublication => _interface is null ? null :
        MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(_interface.ToJson()));
    internal string Fingerprint => WoundAcceptedTurnFingerprintWriter.Compute(new[] {
        _interface is null ? "book_of_eternity.mortal_joint_repair.output_basis.v1" : "book_of_eternity.mortal_joint_repair.output_basis.v3",
        WoundAcceptedTurnFingerprintWriter.CanonicalJson(ToJson()) });
    internal JsonObject ToJson()
    {
        var root = new JsonObject { ["version"] = _interface is null ? 1 : 3, ["localWoundRef"] = LocalWoundRef,
            ["woundId"] = WoundId, ["transitionId"] = TransitionId,
            ["narrativeBase64"] = Convert.ToBase64String(_narrative), ["narrativeHash"] = Convert.ToHexString(SHA256.HashData(_narrative)) };
        if (_interface is not null) root["publishedInterfaceResult"] = new JsonObject
        { ["version"] = 1, ["origin"] = "PreparedResultPublicationV1", ["record"] = _interface.ToJson() };
        return root;
    }
    internal static MortalJointAcceptedOutputBasis Capture(string localRef, string woundId, string transitionId, byte[] narrative,
        MortalJointInterfacePublication.Record? publication = null) => new(localRef, woundId, transitionId, narrative, publication);
    internal static MortalJointAcceptedOutputBasis Read(JsonNode? node)
    {
        try
        {
            if (node is not JsonObject root || root["version"] is not JsonValue version ||
                !version.TryGetValue<int>(out var number) || number is not (1 or 3))
                throw new InvalidDataException("joint_output_binding_shape");
            var fields = number == 1 ? Fields : Fields.Append("publishedInterfaceResult").ToArray();
            if (root.Count != fields.Length || root.Any(row => !fields.Contains(row.Key, StringComparer.Ordinal)))
                throw new InvalidDataException("joint_output_binding_shape");
            MortalJointInterfacePublication.Record? publication = null;
            if (number == 3)
            {
                if (root["publishedInterfaceResult"] is not JsonObject result || result.Count != 3 ||
                    result.Any(row => row.Key is not ("version" or "origin" or "record")) ||
                    result["version"]?.GetValue<int>() != 1 || result["origin"]?.GetValue<string>() != "PreparedResultPublicationV1")
                    throw new InvalidDataException("joint_output_binding_result_origin");
                publication = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(result["record"]));
            }
            string Text(string key) => root[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
                ? text : throw new InvalidDataException("joint_output_binding_" + key);
            var encoded = Text("narrativeBase64");
            var bytes = Convert.FromBase64String(encoded);
            if (Convert.ToBase64String(bytes) != encoded || Text("narrativeHash") != Convert.ToHexString(SHA256.HashData(bytes)))
                throw new InvalidDataException("joint_output_binding_digest");
            return new(Text("localWoundRef"), Text("woundId"), Text("transitionId"), bytes, publication);
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
