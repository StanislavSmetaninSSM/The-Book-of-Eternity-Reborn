using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record WoundCarrierCoordinate(
    string Realm,
    string OwnerKind,
    string OwnerId,
    string CarrierPath);

internal sealed record WoundCarrierOccurrence(
    string WoundId,
    string FilePath,
    string JsonPath,
    WoundCarrierCoordinate Coordinate,
    WoundMaterializationEnvelope Wound);

internal sealed record WoundCarrierCatalogInput(
    JsonObject? PlayerWounds,
    JsonObject? NpcWounds,
    JsonObject? EnemyCombatants,
    JsonObject? AllyCombatants,
    JsonObject? AfterlifeProfiles);

internal sealed class WoundCarrierCatalog
{
    internal const string PlayerPath = "game_state/player/wounds.json";
    internal const string NpcPath = "game_state/npcs/npc_wounds.json";
    internal const string EnemiesPath = "game_state/combat/enemies.json";
    internal const string AlliesPath = "game_state/combat/allies.json";
    internal const string AfterlifeProfilesPath = "game_state/meta/afterlife_entity_profiles.json";

    private readonly Dictionary<string, List<WoundCarrierOccurrence>> _byWoundId;
    private readonly HashSet<string> _invalidWoundIds;

    private WoundCarrierCatalog(Builder builder)
    {
        Occurrences = builder.Occurrences.ToArray();
        Issues = builder.Issues.ToArray();
        _invalidWoundIds = new HashSet<string>(builder.InvalidWoundIds, StringComparer.Ordinal);
        _byWoundId = builder.ByWoundId.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToList(),
            StringComparer.Ordinal);
    }

    internal IReadOnlyList<WoundCarrierOccurrence> Occurrences { get; }

    internal IReadOnlyList<ValidationIssue> Issues { get; }

    internal bool TryResolveOne(string woundId, out WoundCarrierOccurrence occurrence)
    {
        occurrence = null!;
        if (string.IsNullOrEmpty(woundId) ||
            _invalidWoundIds.Contains(woundId) ||
            !_byWoundId.TryGetValue(woundId, out var candidates) ||
            candidates.Count != 1)
        {
            return false;
        }

        occurrence = candidates[0];
        return true;
    }

    internal static WoundCarrierCatalog Build(WoundCarrierCatalogInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var builder = new Builder();
        builder.ScanPlayer(input.PlayerWounds);
        builder.ScanNpcs(input.NpcWounds);
        builder.ScanCombatants(input.EnemyCombatants, EnemiesPath, "enemiesData");
        builder.ScanCombatants(input.AllyCombatants, AlliesPath, "alliesData");
        builder.ScanAfterlifeProfiles(input.AfterlifeProfiles);
        builder.FinalizeOwnerCarrierUniqueness();
        builder.FinalizeIdentityUniqueness();
        return new WoundCarrierCatalog(builder);
    }

    private sealed class Builder
    {
        private static readonly IReadOnlySet<string> PlayerRootFields = Set(
            "schemaVersion", "owner", "activeWounds");
        private static readonly IReadOnlySet<string> PlayerOwnerFields = Set(
            "realm", "ownerKind", "ownerId");
        private static readonly IReadOnlySet<string> NpcRootFields = Set(
            "schemaVersion", "entries");
        private static readonly IReadOnlySet<string> NpcEntryFields = Set(
            "npcId", "activeWounds");

        private sealed record LogicalOwnerKey(
            string Realm,
            string OwnerKind,
            string OwnerId);

        private sealed record OwnerCarrierSighting(
            string JsonPath,
            IReadOnlyList<string> WoundIds);

        private readonly Dictionary<LogicalOwnerKey, List<OwnerCarrierSighting>>
            _ownerCarrierSightings = new();

        internal List<WoundCarrierOccurrence> Occurrences { get; } = new();
        internal List<ValidationIssue> Issues { get; } = new();
        internal Dictionary<string, List<WoundCarrierOccurrence>> ByWoundId { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, List<string>> IdentitySightings { get; } =
            new(StringComparer.Ordinal);
        internal HashSet<string> InvalidWoundIds { get; } = new(StringComparer.Ordinal);

        internal void ScanPlayer(JsonObject? root)
        {
            if (root is null)
                return;
            var rootValid = ValidatePlayerRoot(root);
            if (root["activeWounds"] is not JsonArray wounds)
                return;

            ScanWounds(
                wounds,
                PlayerPath,
                PlayerPath + ".activeWounds",
                new WoundCarrierCoordinate(
                    "mortal_world",
                    "player",
                    "player_current",
                    PlayerPath),
                carrierInvalid: !rootValid);
        }

        internal void ScanNpcs(JsonObject? root)
        {
            if (root is null)
                return;
            var rootValid = ValidateNpcRoot(root);
            if (root["entries"] is not JsonArray entries)
                return;

            for (var index = 0; index < entries.Count; index++)
            {
                var entryPath = $"{NpcPath}.entries[{index}]";
                if (entries[index] is not JsonObject entry)
                {
                    Add(
                        entryPath,
                        "wound_carrier_invalid_field",
                        "strict named-NPC wound carrier entry object",
                        Describe(entries[index]));
                    continue;
                }

                var entryShapeValid = ValidateClosedObject(
                    entry,
                    entryPath,
                    NpcEntryFields,
                    "strict fields npcId and activeWounds",
                    "wound_carrier_invalid_field");
                var hasNpcId = TryReadExactIdentifier(entry["npcId"], out var npcId);
                if (!hasNpcId)
                {
                    Add(
                        entryPath + ".npcId",
                        "wound_carrier_invalid_field",
                        "exact permanent named-NPC identity",
                        Describe(entry["npcId"]));
                }

                var coordinate = hasNpcId
                    ? new WoundCarrierCoordinate("mortal_world", "npc", npcId, NpcPath)
                    : null;
                if (coordinate is not null)
                    RegisterOwnerCarrier(coordinate, entryPath, entry["activeWounds"]);

                if (entry["activeWounds"] is not JsonArray wounds)
                {
                    Add(
                        entryPath + ".activeWounds",
                        "wound_carrier_invalid_field",
                        "canonical activeWounds array",
                        Describe(entry["activeWounds"]));
                    continue;
                }

                ScanWounds(
                    wounds,
                    NpcPath,
                    entryPath + ".activeWounds",
                    coordinate,
                    carrierInvalid: !rootValid || !entryShapeValid || !hasNpcId);
            }
        }

        internal void ScanCombatants(JsonObject? root, string filePath, string collection)
        {
            if (root is null)
                return;
            if (root[collection] is not JsonArray combatants)
            {
                Add(
                    filePath + "." + collection,
                    "wound_carrier_invalid_root",
                    $"existing canonical {collection} combatant array",
                    Describe(root[collection]));
                return;
            }

            for (var index = 0; index < combatants.Count; index++)
            {
                var combatantPath = $"{filePath}.{collection}[{index}]";
                if (combatants[index] is not JsonObject combatant)
                {
                    Add(
                        combatantPath,
                        "wound_carrier_invalid_field",
                        "combatant object",
                        Describe(combatants[index]));
                    continue;
                }

                ScanCombatOwner(
                    combatant,
                    filePath,
                    combatantPath,
                    isMember: false,
                    inheritedCarrierInvalid: false);
                if (!combatant.ContainsKey("members"))
                    continue;
                if (combatant["members"] is not JsonArray members)
                {
                    Add(
                        combatantPath + ".members",
                        "wound_carrier_invalid_field",
                        "nested group-member array",
                        Describe(combatant["members"]));
                    continue;
                }

                var isExactGroup = IsExactTrue(combatant["isGroup"]);
                var hasNonEmptyMemberWoundState = members.Any(static member =>
                    member is JsonObject memberObject &&
                    HasPotentialWoundState(memberObject["activeWounds"]));
                if (!isExactGroup && hasNonEmptyMemberWoundState)
                {
                    Add(
                        combatantPath + ".isGroup",
                        "wound_carrier_group_identity_invalid",
                        "exact boolean true before nested member wound carriers",
                        Describe(combatant["isGroup"]));
                }

                for (var memberIndex = 0; memberIndex < members.Count; memberIndex++)
                {
                    var memberPath = $"{combatantPath}.members[{memberIndex}]";
                    if (members[memberIndex] is not JsonObject member)
                    {
                        Add(
                            memberPath,
                            "wound_carrier_invalid_field",
                            "nested group-member object",
                            Describe(members[memberIndex]));
                        continue;
                    }

                    if (isExactGroup || HasPotentialWoundState(member["activeWounds"]))
                    {
                        ScanCombatOwner(
                            member,
                            filePath,
                            memberPath,
                            isMember: true,
                            inheritedCarrierInvalid: !isExactGroup);
                    }
                }
            }
        }

        internal void ScanAfterlifeProfiles(JsonObject? root)
        {
            if (root is null)
                return;
            if (root["profiles"] is not JsonArray profiles)
            {
                Add(
                    AfterlifeProfilesPath + ".profiles",
                    "wound_carrier_invalid_root",
                    "existing canonical afterlife profiles array; spiritual conflict is not a wound carrier",
                    Describe(root["profiles"]));
                return;
            }

            for (var index = 0; index < profiles.Count; index++)
            {
                var profilePath = $"{AfterlifeProfilesPath}.profiles[{index}]";
                if (profiles[index] is not JsonObject profile)
                {
                    Add(
                        profilePath,
                        "wound_carrier_invalid_field",
                        "afterlife profile object",
                        Describe(profiles[index]));
                    continue;
                }
                var identityValid = TryResolveAfterlifeCoordinate(profile, out var coordinate);
                if (identityValid)
                {
                    RegisterOwnerCarrier(
                        coordinate,
                        profilePath,
                        profile["activeWounds"]);
                }

                if (!profile.ContainsKey("activeWounds"))
                    continue;
                if (profile["activeWounds"] is not JsonArray wounds)
                {
                    Add(
                        profilePath + ".activeWounds",
                        "wound_carrier_invalid_field",
                        "canonical activeWounds array",
                        Describe(profile["activeWounds"]));
                    continue;
                }
                if (wounds.Count == 0)
                    continue;

                if (!identityValid)
                {
                    Add(
                        profilePath,
                        "wound_carrier_afterlife_identity_invalid",
                        "exact supported actorType and actorId in an accepted normalized afterlife realm",
                        profile.ToJsonString());
                }

                ScanWounds(
                    wounds,
                    AfterlifeProfilesPath,
                    profilePath + ".activeWounds",
                    identityValid ? coordinate : null,
                    carrierInvalid: !identityValid);
            }
        }

        internal void FinalizeIdentityUniqueness()
        {
            var confusableIdentities =
                new Dictionary<string, (string WoundId, string JsonPath)>(StringComparer.Ordinal);
            foreach (var pair in IdentitySightings)
            {
                if (pair.Value.Count > 1)
                {
                    InvalidWoundIds.Add(pair.Key);
                    Add(
                        pair.Value[1] + ".woundId",
                        "wound_carrier_duplicate_occurrence",
                        "exactly one active carrier occurrence for each woundId",
                        $"{pair.Value.Count} occurrences of {pair.Key}");
                }

                var confusableKey = MortalLocationIdentityState.BuildConfusableKey(pair.Key);
                if (confusableIdentities.TryGetValue(confusableKey, out var first) &&
                    !string.Equals(first.WoundId, pair.Key, StringComparison.Ordinal))
                {
                    InvalidWoundIds.Add(first.WoundId);
                    InvalidWoundIds.Add(pair.Key);
                    Add(
                        pair.Value[0] + ".woundId",
                        "wound_carrier_confusable_wound_id",
                        "globally unique exact and confusable woundId",
                        $"{first.WoundId} conflicts with {pair.Key}");
                }
                else
                {
                    confusableIdentities[confusableKey] = (pair.Key, pair.Value[0]);
                }
            }
        }

        internal void FinalizeOwnerCarrierUniqueness()
        {
            foreach (var pair in _ownerCarrierSightings)
            {
                if (pair.Value.Count < 2)
                    continue;

                foreach (var sighting in pair.Value)
                    InvalidWoundIds.UnionWith(sighting.WoundIds);

                Add(
                    pair.Value[1].JsonPath,
                    "wound_carrier_duplicate_owner_carrier",
                    "exactly one physical carrier object for each logical owner coordinate",
                    $"{pair.Value.Count} carrier objects for " +
                    $"{pair.Key.Realm}/{pair.Key.OwnerKind}/{pair.Key.OwnerId}");
            }
        }

        private bool ValidatePlayerRoot(JsonObject root)
        {
            var valid = true;
            if (root.Count > 0 && root.All(property => !PlayerRootFields.Contains(property.Key)))
            {
                Add(
                    PlayerPath,
                    "wound_carrier_legacy_unsupported",
                    "direct version-1 player wound carrier root",
                    root.ToJsonString());
                valid = false;
            }
            if (!ValidateClosedObject(
                    root,
                    PlayerPath,
                    PlayerRootFields,
                    "strict fields schemaVersion, owner, and activeWounds"))
            {
                valid = false;
            }
            if (!IsSchemaVersionOne(root["schemaVersion"]))
            {
                Add(
                    PlayerPath + ".schemaVersion",
                    "wound_carrier_invalid_field",
                    "exact integer schemaVersion 1",
                    Describe(root["schemaVersion"]));
                valid = false;
            }
            if (root["owner"] is not JsonObject owner)
            {
                Add(
                    PlayerPath + ".owner",
                    "wound_carrier_invalid_field",
                    "strict player owner object",
                    Describe(root["owner"]));
                valid = false;
            }
            else
            {
                if (!ValidateClosedObject(
                        owner,
                        PlayerPath + ".owner",
                        PlayerOwnerFields,
                        "strict fields realm, ownerKind, and ownerId",
                        "wound_carrier_invalid_field"))
                {
                    valid = false;
                }
                valid &= ValidateExactValue(
                    owner,
                    "realm",
                    "mortal_world",
                    PlayerPath + ".owner.realm");
                valid &= ValidateExactValue(
                    owner,
                    "ownerKind",
                    "player",
                    PlayerPath + ".owner.ownerKind");
                valid &= ValidateExactValue(
                    owner,
                    "ownerId",
                    "player_current",
                    PlayerPath + ".owner.ownerId");
            }
            if (root["activeWounds"] is not JsonArray)
            {
                Add(
                    PlayerPath + ".activeWounds",
                    "wound_carrier_invalid_field",
                    "canonical activeWounds array",
                    Describe(root["activeWounds"]));
                valid = false;
            }

            return valid;
        }

        private bool ValidateNpcRoot(JsonObject root)
        {
            var valid = true;
            if (root.Count > 0 && root.All(property => !NpcRootFields.Contains(property.Key)))
            {
                Add(
                    NpcPath,
                    "wound_carrier_legacy_unsupported",
                    "direct version-1 named-NPC wound carrier root",
                    root.ToJsonString());
                valid = false;
            }
            if (!ValidateClosedObject(
                    root,
                    NpcPath,
                    NpcRootFields,
                    "strict fields schemaVersion and entries"))
            {
                valid = false;
            }
            if (!IsSchemaVersionOne(root["schemaVersion"]))
            {
                Add(
                    NpcPath + ".schemaVersion",
                    "wound_carrier_invalid_field",
                    "exact integer schemaVersion 1",
                    Describe(root["schemaVersion"]));
                valid = false;
            }
            if (root["entries"] is not JsonArray)
            {
                Add(
                    NpcPath + ".entries",
                    "wound_carrier_invalid_field",
                    "strict named-NPC entry array",
                    Describe(root["entries"]));
                valid = false;
            }

            return valid;
        }

        private void ScanCombatOwner(
            JsonObject owner,
            string filePath,
            string ownerPath,
            bool isMember,
            bool inheritedCarrierInvalid)
        {
            var expectedIdentityField = isMember ? "memberId" : "combatantId";
            var conflictingIdentityField = isMember ? "combatantId" : "memberId";
            var hasExpectedIdentity = TryReadExactIdentifier(
                owner[expectedIdentityField],
                out var ownerId);
            var hasConflictingIdentity = owner.ContainsKey(conflictingIdentityField) &&
                                         owner[conflictingIdentityField] is not null;
            var claimedCoordinate = hasExpectedIdentity
                ? new WoundCarrierCoordinate(
                    "mortal_world",
                    isMember ? "combatant_member" : "combatant",
                    ownerId,
                    filePath)
                : null;
            if (claimedCoordinate is not null)
            {
                RegisterOwnerCarrier(
                    claimedCoordinate,
                    ownerPath,
                    owner["activeWounds"]);
            }
            var coordinate = hasConflictingIdentity ? null : claimedCoordinate;

            if (!owner.ContainsKey("activeWounds"))
                return;
            if (owner["activeWounds"] is not JsonArray wounds)
            {
                Add(
                    ownerPath + ".activeWounds",
                    "wound_carrier_invalid_field",
                    "canonical activeWounds array",
                    Describe(owner["activeWounds"]));
                return;
            }
            if (wounds.Count == 0)
                return;

            var carrierInvalid = inheritedCarrierInvalid;
            if (owner.ContainsKey("combatantRef"))
            {
                Add(
                    ownerPath + ".combatantRef",
                    "wound_carrier_temporary_identity",
                    "field absent after client allocation of a permanent combat identity",
                    Describe(owner["combatantRef"]));
                carrierInvalid = true;
            }
            if (owner.ContainsKey("memberRef"))
            {
                Add(
                    ownerPath + ".memberRef",
                    "wound_carrier_temporary_identity",
                    "field absent after client allocation of a permanent combat identity",
                    Describe(owner["memberRef"]));
                carrierInvalid = true;
            }
            if (owner.ContainsKey("NPCId") && owner["NPCId"] is not null)
            {
                Add(
                    ownerPath + ".NPCId",
                    "wound_carrier_npc_bound_combatant",
                    "named/NPC-bound combat representation with an empty activeWounds array",
                    Describe(owner["NPCId"]));
                carrierInvalid = true;
            }

            if (!hasExpectedIdentity || hasConflictingIdentity)
            {
                Add(
                    ownerPath,
                    "wound_carrier_combat_identity_invalid",
                    isMember
                        ? "exactly one applicable permanent memberId on a nested group member"
                        : "exactly one applicable permanent combatantId on a top-level combatant",
                    owner.ToJsonString());
                carrierInvalid = true;
            }

            ScanWounds(
                wounds,
                filePath,
                ownerPath + ".activeWounds",
                coordinate,
                carrierInvalid);
        }

        private void ScanWounds(
            JsonArray wounds,
            string filePath,
            string arrayPath,
            WoundCarrierCoordinate? coordinate,
            bool carrierInvalid)
        {
            for (var index = 0; index < wounds.Count; index++)
            {
                var jsonPath = $"{arrayPath}[{index}]";
                string? sightedWoundId = null;
                if (wounds[index] is JsonObject item &&
                    TryReadExactIdentifier(item["woundId"], out var exactWoundId))
                {
                    sightedWoundId = exactWoundId;
                    RegisterIdentitySighting(exactWoundId, jsonPath);
                }

                var result = WoundMaterializationContract.Parse(
                    wounds[index]?.ToJsonString() ?? "null",
                    jsonPath);
                Issues.AddRange(result.Issues);
                if (sightedWoundId is not null && result.Issues.Count > 0)
                    InvalidWoundIds.Add(sightedWoundId);
                if (result.Wound is not { } wound)
                    continue;

                if (sightedWoundId is null)
                    RegisterIdentitySighting(wound.WoundId, jsonPath);
                var occurrenceInvalid = carrierInvalid || coordinate is null;
                if (!string.Equals(wound.Lifecycle, "active", StringComparison.Ordinal))
                {
                    AddInvalid(
                        wound.WoundId,
                        jsonPath + ".lifecycle",
                        "wound_carrier_non_active_lifecycle",
                        "active",
                        wound.Lifecycle);
                    occurrenceInvalid = true;
                }

                if (coordinate is null)
                {
                    InvalidWoundIds.Add(wound.WoundId);
                    continue;
                }

                occurrenceInvalid |= AddAgreementIssueIfDifferent(
                    wound.WoundId,
                    jsonPath + ".owner.realm",
                    coordinate.Realm,
                    wound.Owner.Realm);
                occurrenceInvalid |= AddAgreementIssueIfDifferent(
                    wound.WoundId,
                    jsonPath + ".owner.ownerKind",
                    coordinate.OwnerKind,
                    wound.Owner.OwnerKind);
                occurrenceInvalid |= AddAgreementIssueIfDifferent(
                    wound.WoundId,
                    jsonPath + ".owner.ownerId",
                    coordinate.OwnerId,
                    wound.Owner.OwnerId);
                occurrenceInvalid |= AddAgreementIssueIfDifferent(
                    wound.WoundId,
                    jsonPath + ".owner.carrierPath",
                    coordinate.CarrierPath,
                    wound.Owner.CarrierPath);

                AddOccurrence(
                    new WoundCarrierOccurrence(
                        wound.WoundId,
                        filePath,
                        jsonPath,
                        coordinate,
                        wound));
                if (occurrenceInvalid)
                    InvalidWoundIds.Add(wound.WoundId);
            }
        }

        private bool AddAgreementIssueIfDifferent(
            string woundId,
            string path,
            string expected,
            string actual)
        {
            if (string.Equals(expected, actual, StringComparison.Ordinal))
                return false;
            AddInvalid(
                woundId,
                path,
                "wound_carrier_agreement_mismatch",
                expected,
                actual);
            return true;
        }

        private void AddOccurrence(WoundCarrierOccurrence occurrence)
        {
            Occurrences.Add(occurrence);
            if (!ByWoundId.TryGetValue(occurrence.WoundId, out var occurrences))
            {
                occurrences = new List<WoundCarrierOccurrence>();
                ByWoundId.Add(occurrence.WoundId, occurrences);
            }
            occurrences.Add(occurrence);
        }

        private void RegisterOwnerCarrier(
            WoundCarrierCoordinate coordinate,
            string jsonPath,
            JsonNode? activeWounds)
        {
            var key = new LogicalOwnerKey(
                coordinate.Realm,
                coordinate.OwnerKind,
                coordinate.OwnerId);
            if (!_ownerCarrierSightings.TryGetValue(key, out var sightings))
            {
                sightings = new List<OwnerCarrierSighting>();
                _ownerCarrierSightings.Add(key, sightings);
            }

            sightings.Add(new OwnerCarrierSighting(
                jsonPath,
                CollectExactWoundIds(activeWounds)));
        }

        private void RegisterIdentitySighting(string woundId, string jsonPath)
        {
            if (!IdentitySightings.TryGetValue(woundId, out var paths))
            {
                paths = new List<string>();
                IdentitySightings.Add(woundId, paths);
            }
            paths.Add(jsonPath);
        }

        private static IReadOnlyList<string> CollectExactWoundIds(JsonNode? node)
        {
            if (node is not JsonArray wounds)
                return Array.Empty<string>();

            var woundIds = new List<string>();
            foreach (var wound in wounds)
            {
                if (wound is JsonObject item &&
                    TryReadExactIdentifier(item["woundId"], out var woundId))
                {
                    woundIds.Add(woundId);
                }
            }

            return woundIds.ToArray();
        }

        private bool ValidateClosedObject(
            JsonObject value,
            string path,
            IReadOnlySet<string> allowedFields,
            string expected,
            string issueCode = "wound_carrier_invalid_root")
        {
            var valid = true;
            foreach (var field in allowedFields)
            {
                if (value.ContainsKey(field))
                    continue;
                Add(
                    path + "." + field,
                    issueCode,
                    expected,
                    "missing required field");
                valid = false;
            }
            foreach (var property in value)
            {
                if (allowedFields.Contains(property.Key))
                    continue;
                Add(
                    path + "." + property.Key,
                    issueCode,
                    expected,
                    "unknown field");
                valid = false;
            }
            return valid;
        }

        private bool ValidateExactValue(
            JsonObject owner,
            string field,
            string expected,
            string path)
        {
            if (TryReadExactIdentifier(owner[field], out var actual) &&
                string.Equals(actual, expected, StringComparison.Ordinal))
            {
                return true;
            }

            Add(
                path,
                "wound_carrier_invalid_field",
                expected,
                Describe(owner[field]));
            return false;
        }

        private static bool TryResolveAfterlifeCoordinate(
            JsonObject profile,
            out WoundCarrierCoordinate coordinate)
        {
            coordinate = null!;
            if (!TryReadExactIdentifier(profile["actorType"], out var actorType) ||
                !TryReadExactIdentifier(profile["actorId"], out var actorId) ||
                !TryReadExactIdentifier(profile["realm"], out var realm) ||
                !AfterlifeEntityProfileState.TryNormalizeEffectRealm(realm, out var normalizedRealm))
            {
                return false;
            }

            var ownerKind = actorType switch
            {
                "player_soul" => "player_soul",
                "guardian" => "guardian",
                "resident" or "shining_resident" => "resident",
                "radiant_actor" => "radiant_actor",
                "shining_faction_head" or "saref_agent" or "system_actor" or
                    "custom_afterlife_actor" => "afterlife_actor",
                _ => null
            };
            if (ownerKind is null)
                return false;

            coordinate = new WoundCarrierCoordinate(
                normalizedRealm,
                ownerKind,
                actorId,
                AfterlifeProfilesPath);
            return true;
        }

        private void AddInvalid(
            string woundId,
            string path,
            string code,
            string expected,
            string actual)
        {
            InvalidWoundIds.Add(woundId);
            Add(path, code, expected, actual);
        }

        private void Add(string path, string code, string expected, string actual) =>
            Issues.Add(new ValidationIssue(
                path,
                IssueSeverity.Error,
                "Active wound carrier violates exact physical owner-coordinate authority.",
                code: code,
                actor: "Client",
                section: "wound_materialization",
                expected: expected,
                actual: actual,
                repairHint: "Restore one strict active wound in its exact owner carrier; allocate permanent combat identities first and never infer, copy, retarget, or migrate occurrences during scanning."));

        private static bool IsSchemaVersionOne(JsonNode? node) =>
            node is JsonValue value &&
            value.TryGetValue<int>(out var version) &&
            version == WoundMaterializationContract.SchemaVersion;

        private static bool TryReadExactIdentifier(JsonNode? node, out string value)
        {
            value = node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
                ? text
                : string.Empty;
            return ResourceMaterializationContract.IsExactIdentifier(value);
        }

        private static bool IsExactTrue(JsonNode? node) =>
            node is JsonValue value &&
            value.TryGetValue<bool>(out var isTrue) &&
            isTrue;

        private static bool HasPotentialWoundState(JsonNode? node) =>
            node is JsonArray { Count: > 0 } ||
            node is not null and not JsonArray;

        private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "missing";

        private static IReadOnlySet<string> Set(params string[] values) =>
            new HashSet<string>(values, StringComparer.Ordinal);
    }
}
