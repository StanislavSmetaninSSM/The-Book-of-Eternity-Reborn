using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class StoryService
{
    // Named detached data, never an original/G/output/publication capability.
    internal sealed class JointStoryRowInputs
    {
        internal const string SoulPath = "game_state/meta/soul_state.json";
        internal const string NarrativePath = "output/narrative_response.json";
        internal const string LocationPath = "game_state/world/current_location.json";
        internal const string GuardiansPath = "game_state/meta/guardians.json";
        internal const string ResidentsPath = "game_state/meta/guardian_abode_residents.json";
        internal const string NpcPath = "game_state/npcs/npc_core.json";
        private readonly byte[] _soul, _narrative, _location;
        private readonly byte[]? _guardians, _residents, _npc;
        internal JointStoryRowInputs(string generation, string requestId, int turn, string originalRealm,
            int originalIncarnation, string playerAction, string preparedAtUtc, byte[] soul, byte[] narrative,
            byte[] location, byte[]? guardians = null, byte[]? residents = null, byte[]? npc = null)
        {
            Generation = generation; RequestId = requestId; Turn = turn; OriginalRealm = originalRealm;
            OriginalIncarnation = originalIncarnation; PlayerAction = playerAction; PreparedAtUtc = preparedAtUtc;
            _soul = soul.ToArray(); _narrative = narrative.ToArray(); _location = location.ToArray();
            _guardians = guardians?.ToArray(); _residents = residents?.ToArray(); _npc = npc?.ToArray();
        }
        internal string Generation { get; }
        internal string RequestId { get; }
        internal int Turn { get; }
        internal string OriginalRealm { get; }
        internal int OriginalIncarnation { get; }
        internal string PlayerAction { get; }
        internal string PreparedAtUtc { get; }
        internal byte[]? ReadSourceBytes(string path) => (path switch
        {
            SoulPath => _soul, NarrativePath => _narrative, LocationPath => _location,
            GuardiansPath => _guardians, ResidentsPath => _residents, NpcPath => _npc,
            _ => throw new ArgumentOutOfRangeException(nameof(path))
        })?.ToArray();
    }

    internal sealed class JointStoryRowCalculation(string path, string rowJson, IEnumerable<string> derivedReadPaths)
    {
        internal string Path { get; } = path;
        internal string RowJson { get; } = rowJson;
        internal IReadOnlyList<string> DerivedReadPaths { get; } = Array.AsReadOnly(derivedReadPaths.ToArray());
    }

    internal static JointStoryRowCalculation CalculateMortalJointStoryRow(JointStoryRowInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var reads = new List<string>();
        JsonObject? Read(string path)
        {
            reads.Add(path);
            var bytes = inputs.ReadSourceBytes(path);
            return bytes is null ? null : ReadJointStoryObject(bytes, path);
        }

        var soul = Read(JointStoryRowInputs.SoulPath) ?? throw new InvalidDataException("joint_story_soul_missing");
        if (ReadJointStoryString(soul, "currentRealm") != inputs.OriginalRealm ||
            ReadJointStoryIncarnation(soul) != inputs.OriginalIncarnation || string.IsNullOrWhiteSpace(inputs.PlayerAction))
            throw new InvalidDataException("joint_story_current_realm_action_changed");
        var narrativeRoot = Read(JointStoryRowInputs.NarrativePath) ?? throw new InvalidDataException("joint_story_narrative_missing");
        var narrative = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(ReadJointStoryString(narrativeRoot, "response"));
        if (string.IsNullOrWhiteSpace(narrative)) throw new InvalidDataException("joint_story_narrative_empty");
        var locationRoot = Read(JointStoryRowInputs.LocationPath) ?? throw new InvalidDataException("joint_story_location_missing");
        var location = ReadJointStoryString(locationRoot["currentLocationData"] as JsonObject ?? locationRoot, "name") ?? "Неизвестно";
        var refs = ReadMortalJointStoryRefs(inputs.PlayerAction, Read);
        var row = JsonSerializer.Serialize(new StoryEntry
        {
            RequestId = inputs.RequestId, SessionGeneration = inputs.Generation, Turn = inputs.Turn,
            Timestamp = inputs.PreparedAtUtc, Realm = inputs.OriginalRealm, Player = inputs.PlayerAction,
            Narrative = narrative, Location = location, EntityRefs = refs
        }, JsonOpts);
        // Existing publisher owns signed prefix recognition and BOM/newline framing.
        return new(GetStoryPath(inputs.OriginalRealm, inputs.OriginalIncarnation), row, reads);
    }
}
