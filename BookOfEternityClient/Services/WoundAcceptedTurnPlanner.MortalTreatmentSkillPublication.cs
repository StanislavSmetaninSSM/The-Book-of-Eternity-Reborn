using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    private const string PlayerActiveSkillPath = "game_state/player/skills_active.json";
    private const string PlayerPassiveSkillPath = "game_state/player/skills_passive.json";
    private const string NpcSkillPath = "game_state/npcs/npc_core.json";

    private static readonly HashSet<string> TreatmentSkillResponseProperties = new(
        new[]
        {
            nameof(GameResponse.ActiveSkillChanges),
            nameof(GameResponse.RemoveActiveSkills),
            nameof(GameResponse.PassiveSkillChanges),
            nameof(GameResponse.RemovePassiveSkills),
            nameof(GameResponse.NPCActiveSkillChanges),
            nameof(GameResponse.NPCPassiveSkillChanges)
        },
        StringComparer.Ordinal);

    private sealed record FrozenNpcSkillCommand(
        string NpcId,
        JsonArray? SkillChanges,
        JsonArray? SkillsToRemove);

    private sealed class TreatmentSkillCommandEnvelope
    {
        internal TreatmentSkillCommandEnvelope(
            JsonObject commandRoot,
            JsonArray? activeChanges,
            JsonArray? activeRemovals,
            JsonArray? passiveChanges,
            JsonArray? passiveRemovals,
            IReadOnlyList<FrozenNpcSkillCommand> npcActive,
            IReadOnlyList<FrozenNpcSkillCommand> npcPassive,
            string fingerprint)
        {
            CommandRoot = commandRoot.DeepClone().AsObject();
            ActiveChanges = Clone(activeChanges);
            ActiveRemovals = Clone(activeRemovals);
            PassiveChanges = Clone(passiveChanges);
            PassiveRemovals = Clone(passiveRemovals);
            NpcActive = npcActive.Select(Clone).ToArray();
            NpcPassive = npcPassive.Select(Clone).ToArray();
            Fingerprint = fingerprint;
        }

        internal JsonObject CommandRoot { get; }
        internal JsonArray? ActiveChanges { get; }
        internal JsonArray? ActiveRemovals { get; }
        internal JsonArray? PassiveChanges { get; }
        internal JsonArray? PassiveRemovals { get; }
        internal IReadOnlyList<FrozenNpcSkillCommand> NpcActive { get; }
        internal IReadOnlyList<FrozenNpcSkillCommand> NpcPassive { get; }
        internal string Fingerprint { get; }
        internal bool IsEmpty => CommandRoot.Count == 0;
        internal bool TouchesPlayerActive => ActiveChanges is not null || ActiveRemovals is not null;
        internal bool TouchesPlayerPassive => PassiveChanges is not null || PassiveRemovals is not null;
        internal bool TouchesNpc => CommandRoot.ContainsKey("NPCActiveSkillChanges") ||
                                    CommandRoot.ContainsKey("NPCPassiveSkillChanges");

        private static FrozenNpcSkillCommand Clone(FrozenNpcSkillCommand value) =>
            new(value.NpcId, Clone(value.SkillChanges), Clone(value.SkillsToRemove));

        private static JsonArray? Clone(JsonArray? value) =>
            value?.DeepClone().AsArray();
    }

    private sealed record TreatmentSkillCommandEnvelopeResult(
        TreatmentSkillCommandEnvelope? Envelope,
        IReadOnlyList<ValidationIssue> Issues);

    private sealed class TreatmentSkillProjectionAuthority
    {
        private readonly Dictionary<string, CanonicalBeforeImage> _baselines;
        private readonly Dictionary<string, JsonObject> _afterImages;
        private readonly Dictionary<string, JsonObject> _actorBeforeRoots;
        private readonly Dictionary<string, JsonObject> _actorAfterRoots;
        private readonly string[] _touchedActorIds;

        internal TreatmentSkillProjectionAuthority(
            object reservationAuthority,
            string commandEnvelopeFingerprint,
            string semanticFingerprint,
            IReadOnlyDictionary<string, CanonicalBeforeImage> baselines,
            IReadOnlyDictionary<string, JsonObject> afterImages,
            IReadOnlyDictionary<string, JsonObject> actorBeforeRoots,
            IReadOnlyDictionary<string, JsonObject> actorAfterRoots,
            IReadOnlyList<string> touchedActorIds,
            string fingerprint)
        {
            ReservationAuthority = reservationAuthority;
            CommandEnvelopeFingerprint = commandEnvelopeFingerprint;
            SemanticFingerprint = semanticFingerprint;
            _baselines = baselines.ToDictionary(
                static pair => pair.Key,
                static pair => new CanonicalBeforeImage(
                    pair.Value.Existed,
                    pair.Value.Bytes?.ToArray()),
                StringComparer.Ordinal);
            _afterImages = CloneObjects(afterImages);
            _actorBeforeRoots = CloneObjects(actorBeforeRoots);
            _actorAfterRoots = CloneObjects(actorAfterRoots);
            _touchedActorIds = touchedActorIds.OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();
            Fingerprint = fingerprint;
        }

        internal object ReservationAuthority { get; }
        internal string CommandEnvelopeFingerprint { get; }
        internal string SemanticFingerprint { get; }
        internal string Fingerprint { get; }
        internal IReadOnlyDictionary<string, CanonicalBeforeImage> Baselines =>
            new ReadOnlyDictionary<string, CanonicalBeforeImage>(_baselines.ToDictionary(
                static pair => pair.Key,
                static pair => new CanonicalBeforeImage(
                    pair.Value.Existed,
                    pair.Value.Bytes?.ToArray()),
                StringComparer.Ordinal));
        internal IReadOnlyDictionary<string, JsonObject> AfterImages =>
            new ReadOnlyDictionary<string, JsonObject>(CloneObjects(_afterImages));
        internal IReadOnlyDictionary<string, JsonObject> ActorBeforeRoots =>
            new ReadOnlyDictionary<string, JsonObject>(CloneObjects(_actorBeforeRoots));
        internal IReadOnlyDictionary<string, JsonObject> ActorAfterRoots =>
            new ReadOnlyDictionary<string, JsonObject>(CloneObjects(_actorAfterRoots));
        internal IReadOnlyList<string> TouchedActorIds => Array.AsReadOnly(_touchedActorIds.ToArray());

        private static Dictionary<string, JsonObject> CloneObjects(
            IReadOnlyDictionary<string, JsonObject> values) => values.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal);
    }

    private sealed record TreatmentSkillProjectionResult(
        object? Authority,
        IReadOnlyList<ValidationIssue> Issues);

    private static TreatmentSkillCommandEnvelopeResult FreezeTreatmentSkillCommands(
        GameResponse response)
    {
        var issues = new List<ValidationIssue>();
        foreach (var property in response.GetType().GetProperties(
                     System.Reflection.BindingFlags.Instance |
                     System.Reflection.BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length != 0 || !property.CanRead)
                continue;
            if (!TreatmentSkillResponseProperties.Contains(property.Name) &&
                property.GetValue(response) is not null)
            {
                issues.Add(PublicationIssue(
                    "treatmentPublication.response." + property.Name,
                    "mortal_wound_treatment_publication_response_unsupported",
                    "only the six ordinary Mortal skill-operation fields",
                    property.Name));
            }
        }

        var root = new JsonObject();
        var active = FreezeSkillRows(
            response.ActiveSkillChanges,
            "activeSkillChanges",
            root,
            issues);
        var activeRemovals = FreezeStringRows(
            response.RemoveActiveSkills,
            "removeActiveSkills",
            root,
            issues);
        var passive = FreezeSkillRows(
            response.PassiveSkillChanges,
            "passiveSkillChanges",
            root,
            issues);
        var passiveRemovals = FreezeStringRows(
            response.RemovePassiveSkills,
            "removePassiveSkills",
            root,
            issues);
        var npcActive = FreezeNpcCommands(
            response.NPCActiveSkillChanges,
            "NPCActiveSkillChanges",
            root,
            issues);
        var npcPassive = FreezeNpcCommands(
            response.NPCPassiveSkillChanges,
            "NPCPassiveSkillChanges",
            root,
            issues);
        if (issues.Count != 0)
            return new(null, issues);

        var canonical = WoundAcceptedTurnFingerprintWriter.CanonicalJson(root)!;
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.skill_command_envelope",
            "1",
            canonical
        });
        return new(
            new TreatmentSkillCommandEnvelope(
                root,
                active,
                activeRemovals,
                passive,
                passiveRemovals,
                npcActive,
                npcPassive,
                fingerprint),
            Array.Empty<ValidationIssue>());
    }

    private static JsonArray? FreezeSkillRows(
        JsonElement[]? rows,
        string propertyName,
        JsonObject root,
        ICollection<ValidationIssue> issues)
    {
        if (rows is null)
            return null;
        var frozen = new JsonArray();
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index].ValueKind != JsonValueKind.Object ||
                HasDuplicateObjectProperties(rows[index]))
            {
                issues.Add(PublicationIssue(
                    $"treatmentPublication.response.{propertyName}[{index}]",
                    "mortal_wound_treatment_publication_skill_command_invalid",
                    "one closed unique-property skill object",
                    rows[index].ValueKind.ToString()));
                continue;
            }
            frozen.Add(JsonNode.Parse(rows[index].GetRawText()));
        }
        root[propertyName] = frozen.DeepClone();
        return frozen;
    }

    private static JsonArray? FreezeStringRows(
        string[]? rows,
        string propertyName,
        JsonObject root,
        ICollection<ValidationIssue> issues)
    {
        if (rows is null)
            return null;
        var frozen = new JsonArray();
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < rows.Length; index++)
        {
            var value = rows[index];
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                !exact.Add(value) ||
                !confusable.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
            {
                issues.Add(PublicationIssue(
                    $"treatmentPublication.response.{propertyName}[{index}]",
                    "mortal_wound_treatment_publication_skill_command_invalid",
                    "one exact/confusable-unique skill selector",
                    value ?? "null"));
                continue;
            }
            frozen.Add(value);
        }
        root[propertyName] = frozen.DeepClone();
        return frozen;
    }

    private static IReadOnlyList<FrozenNpcSkillCommand> FreezeNpcCommands(
        JsonElement[]? rows,
        string propertyName,
        JsonObject root,
        ICollection<ValidationIssue> issues)
    {
        if (rows is null)
            return Array.Empty<FrozenNpcSkillCommand>();
        var frozenRows = new JsonArray();
        var result = new List<FrozenNpcSkillCommand>();
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < rows.Length; index++)
        {
            var path = $"treatmentPublication.response.{propertyName}[{index}]";
            if (rows[index].ValueKind != JsonValueKind.Object ||
                HasDuplicateObjectProperties(rows[index]))
            {
                issues.Add(PublicationIssue(
                    path,
                    "mortal_wound_treatment_publication_npc_skill_command_invalid",
                    "one closed NPC skill-operation object",
                    rows[index].ValueKind.ToString()));
                continue;
            }
            var properties = rows[index].EnumerateObject().ToArray();
            if (properties.Any(static property => property.Name is not
                    ("npcId" or "skillChanges" or "skillsToRemove")) ||
                !rows[index].TryGetProperty("npcId", out var npcIdElement) ||
                npcIdElement.ValueKind != JsonValueKind.String ||
                !ResourceMaterializationContract.IsExactIdentifier(npcIdElement.GetString()) ||
                (!rows[index].TryGetProperty("skillChanges", out _) &&
                 !rows[index].TryGetProperty("skillsToRemove", out _)))
            {
                issues.Add(PublicationIssue(
                    path,
                    "mortal_wound_treatment_publication_npc_skill_command_invalid",
                    "only npcId plus at least one skillChanges/skillsToRemove array",
                    rows[index].GetRawText()));
                continue;
            }
            var npcId = npcIdElement.GetString()!;
            if (!exact.Add(npcId) ||
                !confusable.Add(MortalLocationIdentityState.BuildConfusableKey(npcId)))
            {
                issues.Add(PublicationIssue(
                    path + ".npcId",
                    "mortal_wound_treatment_publication_npc_selector_ambiguous",
                    "one exact/confusable-unique NPC selector per skill-kind field",
                    npcId));
                continue;
            }

            JsonArray? changes = null;
            JsonArray? removals = null;
            if (rows[index].TryGetProperty("skillChanges", out var changeElement))
            {
                if (changeElement.ValueKind != JsonValueKind.Array ||
                    changeElement.EnumerateArray().Any(static item =>
                        item.ValueKind != JsonValueKind.Object ||
                        HasDuplicateObjectProperties(item)))
                {
                    issues.Add(PublicationIssue(
                        path + ".skillChanges",
                        "mortal_wound_treatment_publication_npc_skill_command_invalid",
                        "an array of unique-property skill objects",
                        changeElement.ValueKind.ToString()));
                    continue;
                }
                changes = new JsonArray(changeElement.EnumerateArray()
                    .Select(static item => JsonNode.Parse(item.GetRawText()))
                    .ToArray());
            }
            if (rows[index].TryGetProperty("skillsToRemove", out var removalElement))
            {
                if (removalElement.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(PublicationIssue(
                        path + ".skillsToRemove",
                        "mortal_wound_treatment_publication_npc_skill_command_invalid",
                        "an array of exact skill selectors",
                        removalElement.ValueKind.ToString()));
                    continue;
                }
                var removalValues = removalElement.EnumerateArray().ToArray();
                var strings = removalValues
                    .Where(static item => item.ValueKind == JsonValueKind.String)
                    .Select(static item => item.GetString()!)
                    .ToArray();
                if (strings.Length != removalValues.Length ||
                    strings.Any(static value => string.IsNullOrWhiteSpace(value) ||
                                                !string.Equals(value, value.Trim(), StringComparison.Ordinal)) ||
                    strings.Distinct(StringComparer.Ordinal).Count() != strings.Length ||
                    strings.Select(MortalLocationIdentityState.BuildConfusableKey)
                        .Distinct(StringComparer.Ordinal).Count() != strings.Length)
                {
                    issues.Add(PublicationIssue(
                        path + ".skillsToRemove",
                        "mortal_wound_treatment_publication_npc_skill_command_invalid",
                        "exact/confusable-unique skill selectors",
                        removalElement.GetRawText()));
                    continue;
                }
                removals = new JsonArray(strings.Select(static value => (JsonNode?)value).ToArray());
            }
            var frozen = new JsonObject { ["npcId"] = npcId };
            if (changes is not null)
                frozen["skillChanges"] = changes.DeepClone();
            if (removals is not null)
                frozen["skillsToRemove"] = removals.DeepClone();
            frozenRows.Add(frozen);
            result.Add(new FrozenNpcSkillCommand(npcId, changes, removals));
        }
        root[propertyName] = frozenRows;
        return result;
    }

    private static bool HasDuplicateObjectProperties(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name) || HasDuplicateProperties(property.Value))
                return true;
        }
        return false;
    }

    private static bool HasDuplicateProperties(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => HasDuplicateObjectProperties(element),
        JsonValueKind.Array => element.EnumerateArray().Any(HasDuplicateProperties),
        _ => false
    };

    private static string ComputeTreatmentPublicationEnvelopeFingerprint(
        string baseFingerprint,
        TreatmentSkillCommandEnvelope envelope) => envelope.IsEmpty
        ? baseFingerprint
        : WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.guaranteed_stabilization_skill_publication",
            "1",
            baseFingerprint,
            envelope.Fingerprint
        });

    private static TreatmentSkillProjectionResult CreateTreatmentSkillProjection(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        TreatmentSkillCommandEnvelope envelope,
        string semanticFingerprint,
        object reservationAuthority)
    {
        var issues = new List<ValidationIssue>();
        var baselines = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        var afterImages = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var actorBefore = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var actorAfter = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var touchedActors = new HashSet<string>(StringComparer.Ordinal);

        JsonObject? ReadRoot(string path)
        {
            var bytes = fileSystem.ReadFileBytesAsync(writeLease, path).GetAwaiter().GetResult();
            baselines[path] = new CanonicalBeforeImage(bytes is not null, bytes?.ToArray());
            if (bytes is null)
            {
                issues.Add(PublicationIssue(
                    path,
                    "mortal_wound_treatment_publication_skill_baseline_missing",
                    "one canonical skill root",
                    "missing"));
                return null;
            }
            try
            {
                return JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes))?.AsObject() ??
                       throw new InvalidDataException("Expected a JSON object.");
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                issues.Add(PublicationIssue(
                    path,
                    "mortal_wound_treatment_publication_skill_baseline_invalid",
                    "one canonical skill root",
                    exception.GetType().Name));
                return null;
            }
        }

        if (envelope.TouchesPlayerActive || envelope.TouchesPlayerPassive)
        {
            var activeBefore = ReadRoot(PlayerActiveSkillPath);
            var passiveBefore = ReadRoot(PlayerPassiveSkillPath);
            if (activeBefore is not null && passiveBefore is not null)
            {
                var activeAfter = envelope.TouchesPlayerActive
                    ? ComposePlayerSkillRoot(
                        activeBefore,
                        envelope.ActiveChanges,
                        envelope.ActiveRemovals,
                        "activeSkillChanges",
                        "removeActiveSkills",
                        PlayerActiveSkillPath,
                        issues)
                    : activeBefore.DeepClone().AsObject();
                var passiveAfter = envelope.TouchesPlayerPassive
                    ? ComposePlayerSkillRoot(
                        passiveBefore,
                        envelope.PassiveChanges,
                        envelope.PassiveRemovals,
                        "passiveSkillChanges",
                        "removePassiveSkills",
                        PlayerPassiveSkillPath,
                        issues)
                    : passiveBefore.DeepClone().AsObject();
                if (activeAfter is not null && passiveAfter is not null)
                {
                    actorBefore["player:player_current:active"] = activeBefore;
                    actorBefore["player:player_current:passive"] = passiveBefore;
                    actorAfter["player:player_current:active"] = activeAfter;
                    actorAfter["player:player_current:passive"] = passiveAfter;
                    touchedActors.Add("player:player_current");
                    if (envelope.TouchesPlayerActive)
                        afterImages[PlayerActiveSkillPath] = activeAfter;
                    if (envelope.TouchesPlayerPassive)
                        afterImages[PlayerPassiveSkillPath] = passiveAfter;
                }
            }
        }

        if (envelope.TouchesNpc)
        {
            var npcBefore = ReadRoot(NpcSkillPath);
            if (npcBefore is not null)
            {
                var npcAfter = ComposeNpcSkillRoot(
                    npcBefore,
                    envelope.NpcActive,
                    envelope.NpcPassive,
                    actorBefore,
                    actorAfter,
                    touchedActors,
                    issues);
                if (npcAfter is not null)
                    afterImages[NpcSkillPath] = npcAfter;
            }
        }
        if (issues.Count != 0)
            return new(null, issues);

        var fingerprint = ComputeTreatmentSkillProjectionFingerprint(
            envelope.Fingerprint,
            semanticFingerprint,
            baselines,
            afterImages,
            touchedActors);
        return new(
            new TreatmentSkillProjectionAuthority(
                reservationAuthority,
                envelope.Fingerprint,
                semanticFingerprint,
                baselines,
                afterImages,
                actorBefore,
                actorAfter,
                touchedActors.ToArray(),
                fingerprint),
            Array.Empty<ValidationIssue>());
    }

    private static JsonObject? ComposePlayerSkillRoot(
        JsonObject before,
        JsonArray? changes,
        JsonArray? removals,
        string changeProperty,
        string removalProperty,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var commands = new JsonObject();
        if (changes is not null)
            commands[changeProperty] = changes.DeepClone();
        if (removals is not null)
            commands[removalProperty] = removals.DeepClone();
        try
        {
            return EffectAcceptedTurnInputComposer.ComposeSkillAcceptedRoot(
                before,
                commands,
                changeProperty,
                removalProperty,
                path);
        }
        catch (InvalidDataException exception)
        {
            issues.Add(PublicationIssue(
                path,
                "mortal_wound_treatment_publication_skill_command_invalid",
                "one unambiguous ordinary skill operation",
                exception.Message));
            return null;
        }
    }

    private sealed record NpcActorCopy(string Section, int Index, JsonObject Actor);

    private static JsonObject? ComposeNpcSkillRoot(
        JsonObject before,
        IReadOnlyList<FrozenNpcSkillCommand> activeCommands,
        IReadOnlyList<FrozenNpcSkillCommand> passiveCommands,
        IDictionary<string, JsonObject> actorBefore,
        IDictionary<string, JsonObject> actorAfter,
        ISet<string> touchedActors,
        ICollection<ValidationIssue> issues)
    {
        var result = before.DeepClone().AsObject();
        var actors = new Dictionary<string, List<NpcActorCopy>>(StringComparer.Ordinal);
        var confusableIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (result[section] is null)
                continue;
            if (result[section] is not JsonArray rows)
            {
                issues.Add(PublicationIssue(
                    NpcSkillPath + "." + section,
                    "mortal_wound_treatment_publication_npc_skill_command_invalid",
                    "one canonical NPC array",
                    result[section]!.GetType().Name));
                continue;
            }
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index] is not JsonObject actor ||
                    !GuardianPolicyContracts.TryResolveStrictPermanentNpcId(actor, out var npcId))
                {
                    issues.Add(PublicationIssue(
                        NpcSkillPath + $".{section}[{index}]",
                        "mortal_wound_treatment_publication_npc_skill_command_invalid",
                        "one canonical permanent NPC actor",
                        rows[index]?.ToJsonString() ?? "null"));
                    continue;
                }
                var confusable = MortalLocationIdentityState.BuildConfusableKey(npcId);
                if (confusableIds.TryGetValue(confusable, out var previous) &&
                    !string.Equals(previous, npcId, StringComparison.Ordinal))
                {
                    issues.Add(PublicationIssue(
                        NpcSkillPath + $".{section}[{index}]",
                        "mortal_wound_treatment_publication_npc_selector_ambiguous",
                        "confusable-unique permanent NPC identities",
                        npcId));
                    continue;
                }
                confusableIds[confusable] = npcId;
                if (!actors.TryGetValue(npcId, out var copies))
                    actors[npcId] = copies = new List<NpcActorCopy>();
                copies.Add(new NpcActorCopy(section, index, actor));
            }
        }
        foreach (var (npcId, copies) in actors)
        {
            if (copies.GroupBy(static copy => copy.Section, StringComparer.Ordinal)
                    .Any(static group => group.Count() != 1) ||
                copies.Skip(1).Any(copy => !JsonNode.DeepEquals(copies[0].Actor, copy.Actor)))
            {
                issues.Add(PublicationIssue(
                    NpcSkillPath,
                    "mortal_wound_treatment_publication_npc_selector_ambiguous",
                    "at most one identical NPC copy per section and identical cross-section mirrors",
                    npcId));
            }
        }
        if (issues.Count != 0)
            return null;

        foreach (var (commands, active) in new[]
                 {
                     (activeCommands, true),
                     (passiveCommands, false)
                 })
        {
            foreach (var command in commands)
            {
                if (!actors.TryGetValue(command.NpcId, out var copies))
                {
                    issues.Add(PublicationIssue(
                        NpcSkillPath,
                        "mortal_wound_treatment_publication_npc_source_missing",
                        "one exact current NPC actor",
                        command.NpcId));
                    continue;
                }
                var current = copies[0].Actor;
                if (!touchedActors.Contains("npc:" + command.NpcId))
                {
                    actorBefore["npc:" + command.NpcId + ":active"] =
                        new JsonObject { ["activeSkills"] = current["activeSkills"]?.DeepClone() ?? new JsonArray() };
                    actorBefore["npc:" + command.NpcId + ":passive"] =
                        new JsonObject { ["passiveSkills"] = current["passiveSkills"]?.DeepClone() ?? new JsonArray() };
                }
                var changeProperty = active ? "activeSkillChanges" : "passiveSkillChanges";
                var removalProperty = active ? "removeActiveSkills" : "removePassiveSkills";
                var beforeRoot = new JsonObject
                {
                    [changeProperty] = current[active ? "activeSkills" : "passiveSkills"]?.DeepClone() ??
                                       new JsonArray()
                };
                var commandsRoot = new JsonObject();
                if (command.SkillChanges is not null)
                    commandsRoot[changeProperty] = command.SkillChanges.DeepClone();
                if (command.SkillsToRemove is not null)
                    commandsRoot[removalProperty] = command.SkillsToRemove.DeepClone();
                JsonObject composed;
                try
                {
                    composed = EffectAcceptedTurnInputComposer.ComposeSkillAcceptedRoot(
                        beforeRoot,
                        commandsRoot,
                        changeProperty,
                        removalProperty,
                        NpcSkillPath + $".npc[{command.NpcId}].{(active ? "activeSkills" : "passiveSkills")}");
                }
                catch (InvalidDataException exception)
                {
                    issues.Add(PublicationIssue(
                        NpcSkillPath,
                        "mortal_wound_treatment_publication_npc_skill_command_invalid",
                        "one unambiguous ordinary NPC skill operation",
                        exception.Message));
                    continue;
                }
                foreach (var copy in copies)
                {
                    copy.Actor[active ? "activeSkills" : "passiveSkills"] =
                        composed[changeProperty]!.DeepClone();
                }
                touchedActors.Add("npc:" + command.NpcId);
                actorAfter["npc:" + command.NpcId + ":active"] =
                    new JsonObject { ["activeSkills"] = copies[0].Actor["activeSkills"]?.DeepClone() ?? new JsonArray() };
                actorAfter["npc:" + command.NpcId + ":passive"] =
                    new JsonObject { ["passiveSkills"] = copies[0].Actor["passiveSkills"]?.DeepClone() ?? new JsonArray() };
            }
        }
        return issues.Count == 0 ? result : null;
    }

    private static string ComputeTreatmentSkillProjectionFingerprint(
        string commandEnvelopeFingerprint,
        string semanticFingerprint,
        IReadOnlyDictionary<string, CanonicalBeforeImage> baselines,
        IReadOnlyDictionary<string, JsonObject> afterImages,
        IEnumerable<string> touchedActorIds)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.skill_projection_authority",
            "1",
            commandEnvelopeFingerprint,
            semanticFingerprint
        };
        foreach (var path in baselines.Keys.OrderBy(static value => value, StringComparer.Ordinal))
        {
            var before = baselines[path];
            fields.Add(path);
            fields.Add(before.Existed ? "present" : "missing");
            fields.Add(before.Bytes is null
                ? "missing"
                : "sha256:" + Convert.ToHexString(SHA256.HashData(before.Bytes)).ToLowerInvariant());
        }
        foreach (var path in afterImages.Keys.OrderBy(static value => value, StringComparer.Ordinal))
        {
            fields.Add(path);
            fields.Add(WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.skill_projection_after_image",
                "1",
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(afterImages[path])
            }));
        }
        fields.AddRange(touchedActorIds.OrderBy(static value => value, StringComparer.Ordinal));
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static bool TryReadTreatmentSkillProjection(
        object? authority,
        object reservationAuthority,
        string semanticFingerprint,
        out TreatmentSkillProjectionAuthority projection)
    {
        projection = null!;
        if (authority is not TreatmentSkillProjectionAuthority candidate ||
            !ReferenceEquals(candidate.ReservationAuthority, reservationAuthority) ||
            !string.Equals(candidate.SemanticFingerprint, semanticFingerprint, StringComparison.Ordinal))
        {
            return false;
        }
        var fingerprint = ComputeTreatmentSkillProjectionFingerprint(
            candidate.CommandEnvelopeFingerprint,
            candidate.SemanticFingerprint,
            candidate.Baselines,
            candidate.AfterImages,
            candidate.TouchedActorIds);
        if (!string.Equals(candidate.Fingerprint, fingerprint, StringComparison.Ordinal))
            return false;
        projection = candidate;
        return true;
    }

    internal static bool TryReadTreatmentSkillProjection(
        object continuationAuthority,
        object reservationAuthority,
        out IReadOnlyDictionary<string, CanonicalBeforeImage> baselines,
        out IReadOnlyDictionary<string, JsonObject> afterImages,
        out string fingerprint)
    {
        baselines = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        afterImages = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        fingerprint = string.Empty;
        if (!TryReadTreatmentContinuation(continuationAuthority, out var continuation) ||
            !TryReadTreatmentSkillProjection(
                continuation.SkillProjectionAuthority,
                reservationAuthority,
                continuation.SemanticFingerprint,
                out var projection))
        {
            return false;
        }
        baselines = projection.Baselines;
        afterImages = projection.AfterImages;
        fingerprint = projection.Fingerprint;
        return true;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateTreatmentSkillProjectionCandidate(
        object continuationAuthority,
        object reservationAuthority,
        AcceptedMechanicsPlan candidate)
    {
        if (!TryReadTreatmentContinuation(continuationAuthority, out var continuation) ||
            !TryReadTreatmentSkillProjection(
                continuation.SkillProjectionAuthority,
                reservationAuthority,
                continuation.SemanticFingerprint,
                out var projection))
        {
            return new[]
            {
                PublicationIssue(
                    "treatmentPublication.skillProjection",
                    "mortal_wound_treatment_publication_projection_mismatch",
                    "one exact private-minted sealed skill projection",
                    "missing, foreign, or changed projection")
            };
        }
        var skillPaths = new HashSet<string>(
            new[] { PlayerActiveSkillPath, PlayerPassiveSkillPath, NpcSkillPath },
            StringComparer.Ordinal);
        var actualSkillAfterImagePaths = candidate.OwnerCompanionAfterImages.Keys
            .Where(skillPaths.Contains)
            .ToHashSet(StringComparer.Ordinal);
        var expectedSkillAfterImagePaths = projection.AfterImages.Keys
            .ToHashSet(StringComparer.Ordinal);
        if (!actualSkillAfterImagePaths.SetEquals(expectedSkillAfterImagePaths))
        {
            return new[]
            {
                PublicationIssue(
                    "treatmentPublication.skillProjection.afterImages",
                    "mortal_wound_treatment_publication_projection_mismatch",
                    "exactly the skill-root after-images sealed by the projection",
                    string.Join(",", actualSkillAfterImagePaths.OrderBy(
                        static value => value,
                        StringComparer.Ordinal)))
            };
        }
        foreach (var (path, expected) in projection.AfterImages)
        {
            if (!candidate.OwnerCompanionAfterImages.TryGetValue(path, out var actual) ||
                !JsonNode.DeepEquals(expected, actual) ||
                !candidate.TouchedPaths.Contains(path, StringComparer.Ordinal))
            {
                return new[]
                {
                    PublicationIssue(
                        path,
                        "mortal_wound_treatment_publication_projection_mismatch",
                        "the exact sealed final skill-root after-image",
                        "missing or changed candidate after-image")
                };
            }
        }
        foreach (var (path, expected) in projection.Baselines)
        {
            if (!candidate.BeforeImages.TryGetValue(path, out var actual) ||
                expected.Existed != actual.Existed ||
                !(expected.Bytes ?? Array.Empty<byte>()).AsSpan().SequenceEqual(
                    actual.Bytes ?? Array.Empty<byte>()))
            {
                return new[]
                {
                    PublicationIssue(
                        path,
                        "mortal_wound_treatment_publication_projection_mismatch",
                        "the exact sealed canonical skill-root before-image",
                        "missing or changed candidate before-image")
                };
            }
        }
        return ValidateTreatmentSkillActorPreservation(projection);
    }

    private static IReadOnlyList<ValidationIssue> ValidateTreatmentSkillActorPreservation(
        TreatmentSkillProjectionAuthority projection)
    {
        var issues = new List<ValidationIssue>();
        foreach (var actor in projection.TouchedActorIds)
        {
            var parts = actor.Split(':', 2);
            var ownerKind = parts[0];
            var ownerId = parts[1];
            var prefix = actor + ":";
            var beforeActive = projection.ActorBeforeRoots[prefix + "active"];
            var beforePassive = projection.ActorBeforeRoots[prefix + "passive"];
            var afterActive = projection.ActorAfterRoots[prefix + "active"];
            var afterPassive = projection.ActorAfterRoots[prefix + "passive"];
            issues.AddRange(MortalWoundTreatmentCapabilityContract.ValidateComposedActorCatalog(
                ownerKind,
                ownerId,
                beforeActive,
                ownerKind == "player" ? PlayerActiveSkillPath : NpcSkillPath + $".npc[{ownerId}].activeSkills",
                beforePassive,
                ownerKind == "player" ? PlayerPassiveSkillPath : NpcSkillPath + $".npc[{ownerId}].passiveSkills",
                afterActive,
                afterPassive));
            ValidateProductionSkillRows(afterActive, ownerKind == "player" ? "activeSkillChanges" : "activeSkills", true, issues);
            ValidateProductionSkillRows(afterPassive, ownerKind == "player" ? "passiveSkillChanges" : "passiveSkills", false, issues);
        }
        return issues;
    }

    private static void ValidateProductionSkillRows(
        JsonObject root,
        string property,
        bool active,
        ICollection<ValidationIssue> issues)
    {
        if (root[property] is not JsonArray rows)
        {
            issues.Add(PublicationIssue(
                property,
                "mortal_wound_treatment_publication_skill_root_invalid",
                "one canonical skill array",
                "missing"));
            return;
        }
        for (var index = 0; index < rows.Count; index++)
        {
            using var document = JsonDocument.Parse(rows[index]?.ToJsonString() ?? "null");
            var valid = active
                ? ValidationService.IsProductionValidMortalActiveSkill(document.RootElement)
                : ValidationService.IsProductionValidMortalPassiveSkill(document.RootElement);
            if (!valid)
            {
                issues.Add(PublicationIssue(
                    $"{property}[{index}]",
                    "mortal_wound_treatment_publication_skill_root_invalid",
                    "one production-valid Mortal skill row",
                    rows[index]?.ToJsonString() ?? "null"));
            }
        }
    }
}
