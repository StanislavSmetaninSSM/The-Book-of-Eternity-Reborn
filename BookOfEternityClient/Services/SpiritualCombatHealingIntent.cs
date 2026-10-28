using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

// Closed comparison DATA. Only the actual staging/original owners can bind it
// to a current lease, original die pool or C2/C4 publication.
internal sealed class SpiritualCombatHealingIntent
{
    internal const string Kind = "spiritual_combat_treatment_intent";
    private readonly JsonObject _root;
    private SpiritualCombatHealingIntent(JsonObject root) => _root = root.DeepClone().AsObject();
    internal JsonObject Root => _root.DeepClone().AsObject();
    internal string Fingerprint => Text(_root, "intentFingerprint");
    internal string WoundId => Text(_root, "woundId");
    internal string WoundFingerprint => Text(_root, "woundFingerprint");
    internal string ConflictId => Text(_root, "conflictId");
    internal int NextExchangeOrdinal => _root["nextExchangeOrdinal"]!.GetValue<int>();
    internal int HealingTier => _root["healingTier"]!.GetValue<int>();

    internal static SpiritualCombatHealingIntent Create(string session, string request, int turn,
        SpiritualSelfTreatmentSelection selection, int healingTier, JsonObject control,
        IReadOnlyList<AfterlifeWoundTreatmentAuthority.SourceFile> sources)
    {
        var combat = selection.Combat ?? throw new InvalidDataException("Combat intent requires a combat selection.");
        if (selection.Aid is not null) throw new InvalidDataException("Combat intent cannot contain safe aid.");
        var root = new JsonObject
        {
            ["schemaVersion"] = 1, ["kind"] = Kind, ["sessionId"] = session, ["requestId"] = request,
            ["turn"] = turn, ["realm"] = "chaos_sea", ["healerId"] = "player_soul", ["targetId"] = "player_soul",
            ["operationType"] = AfterlifeActionCostRules.SpiritualHealingOperation,
            ["woundId"] = selection.WoundId, ["woundFingerprint"] = selection.ExpectedWoundFingerprint,
            ["conflictId"] = combat.ConflictId, ["conflictPrefixFingerprint"] = combat.ExpectedConflictFingerprint,
            ["nextExchangeOrdinal"] = combat.NextExchangeOrdinal, ["healingTier"] = healingTier,
            ["progressionControl"] = control.DeepClone(),
            ["sourceImages"] = new JsonArray(sources.Select(s => (JsonNode)new JsonObject
            { ["path"] = s.Path, ["fingerprint"] = s.Image.Fingerprint }).ToArray())
        };
        root["intentFingerprint"] = Compute(root);
        return Parse(root);
    }

    internal static SpiritualCombatHealingIntent Parse(JsonNode? node)
    {
        if (node is not JsonObject root) throw new InvalidDataException("Combat intent must be an object.");
        var fields = "schemaVersion kind sessionId requestId turn realm healerId targetId operationType woundId woundFingerprint conflictId conflictPrefixFingerprint nextExchangeOrdinal healingTier progressionControl sourceImages intentFingerprint".Split(' ');
        if (root.Count != fields.Length || fields.Any(f => !root.ContainsKey(f)) ||
            !Int(root, "schemaVersion", 1, 1) || !Int(root, "turn", 1, int.MaxValue) ||
            !Int(root, "nextExchangeOrdinal", 1, int.MaxValue) || !Int(root, "healingTier", 1, 5) ||
            Text(root, "kind") != Kind || Text(root, "realm") != "chaos_sea" ||
            Text(root, "healerId") != "player_soul" || Text(root, "targetId") != "player_soul" ||
            Text(root, "operationType") != AfterlifeActionCostRules.SpiritualHealingOperation ||
            new[] { "sessionId", "requestId", "woundId", "conflictId" }.Any(f => !ResourceMaterializationContract.IsExactIdentifier(Text(root, f))) ||
            new[] { "woundFingerprint", "conflictPrefixFingerprint", "intentFingerprint" }.Any(f => !ResourceMaterializationContract.IsAuthorityFingerprint(Text(root, f))) ||
            root["progressionControl"] is not JsonObject control ||
            JsonSerializer.SerializeToNode(new BookOfEternityClient.Models.ProgressionControl())!.AsObject().Any(p => !control.ContainsKey(p.Key)) ||
            root["sourceImages"] is not JsonArray images || images.Count == 0)
            throw new InvalidDataException("Invalid closed combat intent.");
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in images)
            if (row is not JsonObject image || image.Count != 2 ||
                !AfterlifeWoundTreatmentAuthority.CombatSourcePaths.Contains(Text(image, "path"), StringComparer.Ordinal) ||
                !paths.Add(Text(image, "path")) || !ResourceMaterializationContract.IsAuthorityFingerprint(Text(image, "fingerprint")))
                throw new InvalidDataException("Combat intent source images are incomplete or ambiguous.");
        if (!AfterlifeWoundTreatmentAuthority.CombatSourcePaths.SequenceEqual(
                images.Select(image => Text(image!.AsObject(), "path")), StringComparer.Ordinal) ||
            Text(root, "intentFingerprint") != Compute(root))
            throw new InvalidDataException("Combat intent fingerprint or exact source set differs.");
        return new(root);
    }

    internal bool Matches(string session, string request, int turn) =>
        Text(_root, "sessionId") == session && Text(_root, "requestId") == request && _root["turn"]!.GetValue<int>() == turn;

    internal SpiritualSelfTreatmentSelection Selection => new(WoundId, WoundFingerprint)
    {
        Combat = new(ConflictId, Text(_root, "conflictPrefixFingerprint"), NextExchangeOrdinal)
    };

    internal bool IsBound(SpiritualSelfTreatmentStageContext context, SpiritualSelfTreatmentSelection selection) =>
        Matches(context.SessionId, context.RequestId, context.Turn) && selection.Combat is not null &&
        selection.Aid is null && Selection == selection && JsonNode.DeepEquals(_root["progressionControl"],
            JsonSerializer.SerializeToNode(context.Progression));

    internal static bool IsKind(JsonObject root) => Text(root, "kind") == Kind;

    internal JsonObject CreateGmProjection(AfterlifeWoundTreatmentAuthority.SelectedWoundSourceData data) => new()
    {
        ["schemaVersion"] = 1, ["mode"] = "combat_self", ["intentFingerprint"] = Fingerprint,
        ["operationType"] = AfterlifeActionCostRules.SpiritualHealingOperation, ["conflictId"] = ConflictId,
        ["nextExchangeOrdinal"] = NextExchangeOrdinal, ["woundId"] = WoundId, ["woundName"] = data.Wound.Display.Name,
        ["severityRank"] = data.Wound.Severity.Rank, ["healingSource"] = JsonSerializer.SerializeToNode(data.Source),
        ["possibleDestinationGraphs"] = new JsonArray(Enumerable.Range(1, data.Wound.Severity.Rank - 1)
            .Select(rank => (JsonNode)new JsonObject
            {
                ["stageRef"] = SpiritualHealingRequestProjection.GraphRef(Fingerprint, WoundId, rank),
                ["severityRank"] = rank, ["complications"] = SpiritualHealingRequestProjection.Complications(Fingerprint, data.Wound)
            }).ToArray())
    };

    internal static string ConflictFingerprint(JsonObject activeConflict) => WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "book_of_eternity.spiritual_combat_healing.conflict_prefix", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(activeConflict) });

    private static string Compute(JsonObject root)
    {
        var body = root.DeepClone().AsObject(); body.Remove("intentFingerprint");
        return WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { "book_of_eternity.spiritual_combat_healing.intent", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(body) });
    }
    private static bool Int(JsonObject root, string field, int min, int max) =>
        root[field] is JsonValue v && v.TryGetValue<int>(out var n) && n >= min && n <= max;
    private static string Text(JsonObject root, string field) =>
        root[field] is JsonValue v && v.TryGetValue<string>(out var s) ? s ?? "" : "";
}
