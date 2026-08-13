using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class MortalLocationMaterializationContract
{
    internal const string WorldMapPath = "game_state/world/world_map.json";
    internal const string CurrentLocationPath = "game_state/world/current_location.json";
    internal const string EnvelopeProperty = "materialization";
    internal const string ReceiptProperty = "materializationReceipt";

    internal static readonly string[] LocationSectionNames =
    {
        "presentation",
        "physical",
        "placement",
        "discovery",
        "difficulty",
        "chronicle",
        "factionControl",
        "actorBindings",
        "storageMetadata",
        "activeThreats",
        "loreBindings",
        "customStates",
        "topology"
    };

    internal static readonly string[] LinkSectionNames =
    {
        "endpoints",
        "presentation",
        "traversal",
        "access",
        "discovery",
        "customStates"
    };

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly HashSet<string> EnvelopeFields = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "materializationId",
        "entityKind",
        "realm",
        "route",
        "sourceTurn",
        "sourceAuthority",
        "initialId",
        "state",
        "sections"
    };

    private static readonly HashSet<string> SourceAuthorityFields = new(StringComparer.Ordinal)
    {
        "kind",
        "authorityId"
    };

    private static readonly HashSet<string> DispositionFields = new(StringComparer.Ordinal)
    {
        "disposition",
        "reason"
    };

    private static readonly HashSet<string> LocationReceiptFields = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "receiptId",
        "locationId",
        "initialId",
        "materializationId",
        "realm",
        "route",
        "sourceTurn",
        "sourceAuthorityKind",
        "sourceAuthorityId",
        "seal"
    };

    private static readonly HashSet<string> LinkReceiptFields = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "receiptId",
        "linkId",
        "initialId",
        "materializationId",
        "realm",
        "route",
        "sourceTurn",
        "sourceAuthorityKind",
        "sourceAuthorityId",
        "sourceLocationId",
        "targetLocationId",
        "seal"
    };

    private static readonly HashSet<string> DiscoveryFields = new(StringComparer.Ordinal)
    {
        "tier",
        "audience",
        "rumorSummary"
    };

    private static readonly HashSet<string> DifficultyFields = new(StringComparer.Ordinal)
    {
        "danger",
        "recommendedLevel",
        "description"
    };

    private static readonly HashSet<string> CoordinatesFields = new(StringComparer.Ordinal)
    {
        "x",
        "y",
        "z"
    };

    private static readonly HashSet<string> LinkAccessFields = new(StringComparer.Ordinal)
    {
        "state",
        "reason",
        "requirements"
    };

    private static readonly HashSet<string> LocationRoutes = new(StringComparer.Ordinal)
    {
        "current_scene_creation",
        "world_map_creation"
    };

    private static readonly HashSet<string> LinkTypes = new(StringComparer.Ordinal)
    {
        "road",
        "path",
        "passage",
        "portal",
        "one_way",
        "hidden_path",
        "sealed_passage",
        "other"
    };

    private static readonly HashSet<string> ActorBindingRoles = new(StringComparer.Ordinal)
    {
        "resident",
        "owner",
        "staff",
        "prisoner",
        "other"
    };

    private static readonly string[] ClientOwnedRootFields =
    {
        ReceiptProperty,
        "receiptId",
        "seal",
        "locationIdentityIndex",
        "linkIdentityIndex",
        "requestId",
        "sessionId",
        "reservationId",
        "transitionId"
    };

    private static readonly IReadOnlyDictionary<string, string> LocationArraySections =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["factionControl"] = "factionControl",
            ["actorBindings"] = "actorBindings",
            ["storageMetadata"] = "locationStorages",
            ["activeThreats"] = "activeThreats",
            ["loreBindings"] = "loreBindings",
            ["customStates"] = "customStates"
        };

    internal static IReadOnlyList<ValidationIssue> ValidateRawLocation(
        JsonElement value,
        string context,
        string expectedRoute)
    {
        var issues = new List<ValidationIssue>();
        if (!RequireObject(value, context, issues))
            return issues;

        ValidateDuplicateProperties(value, context, issues);
        ValidateRawRootIdentity(value, context, "locationId", issues);
        ValidateLocationFields(value, context, raw: true, issues);
        ValidateRawEmbeddedThreatAuthority(value, context, issues);
        ValidateRawLocationStorageContents(value, context, expectedRoute, issues);
        ValidateEnvelope(
            value,
            context,
            entityKind: "mortal_location",
            expectedRoute,
            LocationRoutes,
            LocationSectionNames,
            issues);
        ValidateLocationSectionEvidence(value, context, requireCurrentArrayAgreement: true, issues);
        return issues;
    }

    private static void ValidateRawEmbeddedThreatAuthority(
        JsonElement location,
        string context,
        List<ValidationIssue> issues)
    {
        if (!location.TryGetProperty("activeThreats", out var threats) ||
            threats.ValueKind != JsonValueKind.Array ||
            threats.GetArrayLength() == 0)
        {
            return;
        }

        issues.Add(new ValidationIssue(
            context + ".activeThreats",
            IssueSeverity.Error,
            "New Mortal locations cannot claim permanent threat identity inside the location payload.",
            code: "mortal_location_embedded_threat_authority_forbidden",
            section: "mortal_location_materialization",
            expected: "empty activeThreats plus same-turn threatsToAdd with client-assigned threatId",
            actual: $"{threats.GetArrayLength()} embedded threat(s)",
            repairHint: "Keep activeThreats empty on creation and author each threat through worldMapUpdates.threatsToAdd."));
    }

    internal static IReadOnlyList<ValidationIssue> ValidateCanonicalLocation(
        JsonElement value,
        string context) =>
        ValidateCanonicalLocation(value, context, allowStorageContents: false);

    internal static IReadOnlyList<ValidationIssue> ValidateCanonicalCurrentLocation(
        JsonElement value,
        string context) =>
        ValidateCanonicalLocation(value, context, allowStorageContents: true);

    private static IReadOnlyList<ValidationIssue> ValidateCanonicalLocation(
        JsonElement value,
        string context,
        bool allowStorageContents)
    {
        var issues = new List<ValidationIssue>();
        if (!RequireObject(value, context, issues))
            return issues;

        ValidateDuplicateProperties(value, context, issues);
        ValidateCanonicalRootIdentity(value, context, "locationId", new[] { "initialId", "parentInitialId" }, issues);
        ValidateLocationFields(value, context, raw: false, issues);
        if (!allowStorageContents)
            ValidateCanonicalMapStorageContents(value, context, issues);
        ValidateEnvelope(
            value,
            context,
            entityKind: "mortal_location",
            expectedRoute: null,
            LocationRoutes,
            LocationSectionNames,
            issues);
        ValidateLocationSectionEvidence(value, context, requireCurrentArrayAgreement: false, issues);
        ValidateReceipt(value, context, isLink: false, issues);
        return issues;
    }

    private static void ValidateCanonicalMapStorageContents(
        JsonElement location,
        string context,
        List<ValidationIssue> issues)
    {
        if (!location.TryGetProperty("locationStorages", out var storages) ||
            storages.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var storage in storages.EnumerateArray())
        {
            if (storage.ValueKind == JsonValueKind.Object &&
                storage.TryGetProperty("contents", out var contents))
            {
                issues.Add(Issue(
                    $"{context}.locationStorages[{index}].contents",
                    "mortal_location_map_storage_contents_forbidden",
                    "Canonical world-map storage members carry metadata only; item contents live in current/offscreen item authority.",
                    "field absent from world-map storage metadata",
                    contents.GetRawText()));
            }
            index++;
        }
    }

    internal static bool SharedCurrentProjectionValueEquals(
        string fieldName,
        JsonNode? canonical,
        JsonNode? current)
    {
        if (!string.Equals(fieldName, "locationStorages", StringComparison.Ordinal))
            return JsonNode.DeepEquals(canonical, current);
        if (canonical is not JsonArray canonicalStorages ||
            current is not JsonArray currentStorages ||
            canonicalStorages.Count != currentStorages.Count)
        {
            return false;
        }

        for (var index = 0; index < canonicalStorages.Count; index++)
        {
            if (canonicalStorages[index] is not JsonObject canonicalStorage ||
                currentStorages[index] is not JsonObject currentStorage)
            {
                return false;
            }

            var currentMetadata = currentStorage.DeepClone().AsObject();
            currentMetadata.Remove("contents");
            currentMetadata.Remove("itemIds");
            if (!JsonNode.DeepEquals(canonicalStorage, currentMetadata))
                return false;
        }

        return true;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateRawLink(
        JsonElement value,
        string context,
        string expectedRoute)
    {
        var issues = new List<ValidationIssue>();
        if (!RequireObject(value, context, issues))
            return issues;

        ValidateDuplicateProperties(value, context, issues);
        ValidateRawRootIdentity(value, context, "linkId", issues);
        ValidateLinkFields(value, context, raw: true, issues);
        ValidateEnvelope(
            value,
            context,
            entityKind: "mortal_location_link",
            expectedRoute,
            new HashSet<string>(StringComparer.Ordinal) { "world_map_link_creation" },
            LinkSectionNames,
            issues);
        ValidateLinkSectionEvidence(value, context, issues);
        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateCanonicalLink(
        JsonElement value,
        string context)
    {
        var issues = new List<ValidationIssue>();
        if (!RequireObject(value, context, issues))
            return issues;

        ValidateDuplicateProperties(value, context, issues);
        ValidateCanonicalRootIdentity(
            value,
            context,
            "linkId",
            new[] { "initialId", "sourceInitialId", "targetInitialId" },
            issues);
        ValidateLinkFields(value, context, raw: false, issues);
        ValidateEnvelope(
            value,
            context,
            entityKind: "mortal_location_link",
            expectedRoute: null,
            new HashSet<string>(StringComparer.Ordinal) { "world_map_link_creation" },
            LinkSectionNames,
            issues);
        ValidateLinkSectionEvidence(value, context, issues);
        ValidateReceipt(value, context, isLink: true, issues);
        return issues;
    }

    internal static string ComputeSeal(JsonObject materialization, JsonObject receipt)
    {
        ArgumentNullException.ThrowIfNull(materialization);
        ArgumentNullException.ThrowIfNull(receipt);

        var input = new JsonObject();
        foreach (var property in receipt
                     .Where(static pair => !string.Equals(pair.Key, "seal", StringComparison.Ordinal))
                     .OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            input[property.Key] = property.Value?.DeepClone();
        }
        input[EnvelopeProperty] = Canonicalize(materialization);

        var bytes = Encoding.UTF8.GetBytes(CanonicalizeObject(input).ToJsonString(CompactJson));
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    internal static bool ImmutableEvidenceEquals(JsonNode previous, JsonNode current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        return string.Equals(
            Canonicalize(previous).ToJsonString(CompactJson),
            Canonicalize(current).ToJsonString(CompactJson),
            StringComparison.Ordinal);
    }

    private static bool RequireObject(
        JsonElement value,
        string context,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
            return true;

        issues.Add(Issue(
            context,
            "mortal_location_materialization_invalid_root",
            "A Mortal location materialization carrier must be a JSON object.",
            "object",
            value.ValueKind.ToString()));
        return false;
    }

    private static void ValidateRawRootIdentity(
        JsonElement value,
        string context,
        string permanentIdField,
        List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty(permanentIdField, out var permanentId) ||
            permanentId.ValueKind != JsonValueKind.Null)
        {
            issues.Add(Issue(
                $"{context}.{permanentIdField}",
                "mortal_location_materialization_identity_conflict",
                "A raw creation must explicitly carry a null permanent identity.",
                "null",
                Describe(value, permanentIdField)));
        }

        if (ReadExactNonEmptyString(value, "initialId") == null)
        {
            issues.Add(Issue(
                $"{context}.initialId",
                "mortal_location_materialization_identity_conflict",
                "A raw creation requires one exact non-empty initialId.",
                "exact non-empty string",
                Describe(value, "initialId")));
        }

        foreach (var field in ClientOwnedRootFields)
        {
            if (!value.TryGetProperty(field, out _))
                continue;

            issues.Add(Issue(
                $"{context}.{field}",
                "mortal_location_materialization_gm_authored_client_field",
                "The GM cannot author client-owned location identity or protocol evidence.",
                "field absent before client sealing",
                "present"));
        }
    }

    private static void ValidateCanonicalRootIdentity(
        JsonElement value,
        string context,
        string permanentIdField,
        IEnumerable<string> forbiddenTemporaryFields,
        List<ValidationIssue> issues)
    {
        if (ReadExactNonEmptyString(value, permanentIdField) == null)
        {
            issues.Add(Issue(
                $"{context}.{permanentIdField}",
                "mortal_location_materialization_identity_conflict",
                "Canonical location state requires one exact permanent identity.",
                "exact non-empty string",
                Describe(value, permanentIdField)));
        }

        foreach (var field in forbiddenTemporaryFields)
        {
            if (!value.TryGetProperty(field, out _))
                continue;

            issues.Add(Issue(
                $"{context}.{field}",
                string.Equals(permanentIdField, "linkId", StringComparison.Ordinal) &&
                field is "sourceInitialId" or "targetInitialId"
                    ? "mortal_location_link_endpoint_selector_invalid"
                    : "mortal_location_materialization_identity_conflict",
                "Canonical location state cannot retain temporary selectors.",
                "field absent",
                "present"));
        }
    }

    private static void ValidateLocationFields(
        JsonElement value,
        string context,
        bool raw,
        List<ValidationIssue> issues)
    {
        RequireExactString(value, "realm", "mortal_world", context, issues, "mortal_location_materialization_wrong_realm");
        foreach (var field in new[] { "name", "displayName", "purpose", "description", "image_prompt", "region", "lastEventsDescription" })
            RequireNonEmptyString(value, field, context, issues);

        foreach (var field in new[]
                 {
                     "features",
                     "eventDescriptions",
                     "factionControl",
                     "actorBindings",
                     "locationStorages",
                     "activeThreats",
                     "loreBindings",
                     "customStates"
                 })
        {
            RequireArray(value, field, context, issues);
        }

        ValidateLocationFactionControlSelectors(value, context, raw, issues);
        ValidateLocationActorBindingSelectors(value, context, raw, issues);
        ValidateLocationStorageIdentities(value, context, issues);
        ValidateLocationThreatIdentities(value, context, issues);
        ValidateLocationThreatSemantics(value, context, issues);
        ValidateLocationLoreBindings(value, context, issues);
        if (value.TryGetProperty("customStates", out var customStates))
        {
            issues.AddRange(MortalLocationCustomStateContract.Validate(
                customStates,
                context + ".customStates"));
        }

        if (raw)
        {
            RequireProperty(value, "parentLocationId", context, issues);
            RequireProperty(value, "parentInitialId", context, issues);
            ValidateSelectorXorOrNeither(value, "parentLocationId", "parentInitialId", context, issues);
        }
        else
        {
            RequireProperty(value, "parentLocationId", context, issues);
        }

        ValidatePhysicalShape(value, context, issues);
        ValidateCoordinates(value, context, issues);
        ValidateDiscovery(value, context, issues);
        ValidateDifficulty(value, "internalDifficulty", context, issues);
        ValidateDifficulty(value, "externalDifficulty", context, issues);
    }

    private static void ValidateLocationThreatIdentities(
        JsonElement location,
        string context,
        List<ValidationIssue> issues)
    {
        if (!location.TryGetProperty("activeThreats", out var threats) ||
            threats.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var exactThreatIds = new HashSet<string>(StringComparer.Ordinal);
        var confusableThreatIds = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var threat in threats.EnumerateArray())
        {
            var threatContext = $"{context}.activeThreats[{index++}]";
            var threatId = threat.ValueKind == JsonValueKind.Object
                ? ReadExactNonEmptyString(threat, "threatId")
                : null;
            if (threatId == null)
            {
                issues.Add(Issue(
                    threatContext,
                    "mortal_location_threat_identity_invalid",
                    "Materialized active threat requires one exact permanent identity.",
                    "object with exact non-empty threatId",
                    threat.ValueKind == JsonValueKind.Object
                        ? Describe(threat, "threatId")
                        : threat.GetRawText()));
                continue;
            }

            if (!exactThreatIds.Add(threatId))
            {
                issues.Add(Issue(
                    threatContext + ".threatId",
                    "mortal_location_threat_identity_duplicate",
                    "A location cannot contain the same exact active threat identity more than once.",
                    "unique exact threatId within the location",
                    threatId));
                continue;
            }

            if (!confusableThreatIds.Add(
                    MortalLocationIdentityState.BuildConfusableKey(threatId)))
            {
                issues.Add(Issue(
                    threatContext + ".threatId",
                    "mortal_location_threat_identity_confusable",
                    "Active threat identities must remain unique under case and Unicode-confusable normalization.",
                    "unique non-confusable threatId within the location",
                    threatId));
            }
        }
    }

    private static void ValidateLocationThreatSemantics(
        JsonElement location,
        string context,
        List<ValidationIssue> issues)
    {
        if (!location.TryGetProperty("activeThreats", out var threats) ||
            threats.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var threat in threats.EnumerateArray())
        {
            issues.AddRange(MortalLocationActiveThreatContract.Validate(
                threat,
                $"{context}.activeThreats[{index++}]"));
        }
    }

    private static void ValidateLocationLoreBindings(
        JsonElement location,
        string context,
        List<ValidationIssue> issues)
    {
        if (!location.TryGetProperty("loreBindings", out var bindings) ||
            bindings.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var identityFields = new[] { "codexEntryId", "questId", "worldEventId" };
        var index = 0;
        foreach (var binding in bindings.EnumerateArray())
        {
            var bindingContext = $"{context}.loreBindings[{index++}]";
            if (binding.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue(
                    bindingContext,
                    "mortal_location_lore_binding_selector_invalid",
                    "Lore binding must be an object with one closed kind and matching identity.",
                    "codex/codexEntryId, quest/questId, or world_event/worldEventId",
                    binding.GetRawText()));
                continue;
            }

            var kind = ReadExactNonEmptyString(binding, "kind");
            var expectedIdentityField = kind switch
            {
                "codex" => "codexEntryId",
                "quest" => "questId",
                "world_event" => "worldEventId",
                _ => null
            };
            var presentIdentityFields = identityFields
                .Where(field => binding.TryGetProperty(field, out _))
                .ToArray();
            if (expectedIdentityField != null &&
                presentIdentityFields.Length == 1 &&
                string.Equals(
                    presentIdentityFields[0],
                    expectedIdentityField,
                    StringComparison.Ordinal) &&
                ReadExactNonEmptyString(binding, expectedIdentityField) != null)
            {
                continue;
            }

            issues.Add(Issue(
                bindingContext,
                "mortal_location_lore_binding_selector_invalid",
                "Lore binding kind must select exactly one matching permanent identity field.",
                "codex/codexEntryId, quest/questId, or world_event/worldEventId",
                $"kind={kind ?? "missing"}; identities={string.Join(',', presentIdentityFields)}"));
        }
    }

    private static void ValidateLocationActorBindingSelectors(
        JsonElement location,
        string context,
        bool raw,
        List<ValidationIssue> issues)
    {
        if (!location.TryGetProperty("actorBindings", out var bindings) ||
            bindings.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var binding in bindings.EnumerateArray())
        {
            var bindingContext = $"{context}.actorBindings[{index++}]";
            if (binding.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue(
                    bindingContext,
                    "mortal_location_actor_binding_selector_invalid",
                    "Actor binding must be an object with one exact actor selector.",
                    raw
                        ? "exactly one exact actorId or initialActorId"
                        : "one exact actorId and no initialActorId",
                    binding.GetRawText()));
                continue;
            }

            var hasActorId = binding.TryGetProperty("actorId", out var actorIdNode);
            var hasInitialActorId = binding.TryGetProperty("initialActorId", out var initialActorIdNode);
            var actorId = ReadExactNonEmptyString(binding, "actorId");
            var initialActorId = ReadExactNonEmptyString(binding, "initialActorId");
            var actorIdMalformed = hasActorId &&
                                   actorIdNode.ValueKind is not (JsonValueKind.Null or JsonValueKind.String) ||
                                   hasActorId && actorIdNode.ValueKind == JsonValueKind.String && actorId == null;
            var initialActorIdMalformed = hasInitialActorId &&
                                          initialActorIdNode.ValueKind is not (JsonValueKind.Null or JsonValueKind.String) ||
                                          hasInitialActorId && initialActorIdNode.ValueKind == JsonValueKind.String && initialActorId == null;
            var valid = raw
                ? !actorIdMalformed && !initialActorIdMalformed &&
                  (actorId != null) != (initialActorId != null)
                : hasActorId && !hasInitialActorId && actorId != null;
            if (!valid)
            {
                issues.Add(Issue(
                    bindingContext,
                    "mortal_location_actor_binding_selector_invalid",
                    "Actor binding must resolve through one exact authority selector.",
                    raw
                        ? "exactly one exact actorId or initialActorId"
                        : "one exact actorId and no initialActorId",
                    $"actorId={Describe(binding, "actorId")}; initialActorId={Describe(binding, "initialActorId")}"));
            }

            var role = ReadExactNonEmptyString(binding, "role");
            if (role == null || !ActorBindingRoles.Contains(role))
            {
                issues.Add(Issue(
                    bindingContext + ".role",
                    "mortal_location_actor_binding_role_invalid",
                    "Actor binding role must be one closed physical relationship.",
                    string.Join(" | ", ActorBindingRoles.OrderBy(static value => value, StringComparer.Ordinal)),
                    Describe(binding, "role")));
            }
        }
    }

    private static void ValidateLocationFactionControlSelectors(
        JsonElement location,
        string context,
        bool raw,
        List<ValidationIssue> issues)
    {
        if (!location.TryGetProperty("factionControl", out var controls) ||
            controls.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var control in controls.EnumerateArray())
        {
            var controlContext = $"{context}.factionControl[{index++}]";
            if (control.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue(
                    controlContext,
                    "mortal_location_faction_control_selector_invalid",
                    "Faction control must be an object with one exact faction selector.",
                    raw
                        ? "exactly one exact factionId or initialFactionId"
                        : "one exact factionId and no initialFactionId",
                    control.GetRawText()));
                continue;
            }

            var hasFactionId = control.TryGetProperty("factionId", out var factionIdNode);
            var hasInitialFactionId = control.TryGetProperty("initialFactionId", out var initialFactionIdNode);
            var factionId = ReadExactNonEmptyString(control, "factionId");
            var initialFactionId = ReadExactNonEmptyString(control, "initialFactionId");
            var factionIdMalformed = hasFactionId &&
                                     factionIdNode.ValueKind is not (JsonValueKind.Null or JsonValueKind.String) ||
                                     hasFactionId && factionIdNode.ValueKind == JsonValueKind.String && factionId == null;
            var initialFactionIdMalformed = hasInitialFactionId &&
                                            initialFactionIdNode.ValueKind is not (JsonValueKind.Null or JsonValueKind.String) ||
                                            hasInitialFactionId && initialFactionIdNode.ValueKind == JsonValueKind.String && initialFactionId == null;
            var valid = raw
                ? !factionIdMalformed && !initialFactionIdMalformed &&
                  (factionId != null) != (initialFactionId != null)
                : hasFactionId && !hasInitialFactionId && factionId != null;
            if (valid)
                continue;

            issues.Add(Issue(
                controlContext,
                "mortal_location_faction_control_selector_invalid",
                "Faction control must resolve through one exact authority selector.",
                raw
                    ? "exactly one exact factionId or initialFactionId"
                    : "one exact factionId and no initialFactionId",
                $"factionId={Describe(control, "factionId")}; initialFactionId={Describe(control, "initialFactionId")}"));
        }
    }

    private static void ValidateLocationStorageIdentities(
        JsonElement location,
        string context,
        List<ValidationIssue> issues)
    {
        if (!location.TryGetProperty("locationStorages", out var storages) ||
            storages.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var storageIds = new HashSet<string>(StringComparer.Ordinal);
        var storageAliasKeys = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var storage in storages.EnumerateArray())
        {
            var storageContext = $"{context}.locationStorages[{index++}]";
            if (storage.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue(
                    storageContext,
                    "mortal_location_storage_identity_invalid",
                    "Location storage must be an object with one exact storage identity.",
                    "object with exact non-empty storageId",
                    storage.GetRawText()));
                continue;
            }

            issues.AddRange(MortalLocationStorageMetadataContract.Validate(
                storage,
                storageContext));

            var storageId = ReadExactNonEmptyString(storage, "storageId");
            if (storageId == null)
            {
                issues.Add(Issue(
                    $"{storageContext}.storageId",
                    "mortal_location_storage_identity_invalid",
                    "Location storage requires one exact non-empty storageId.",
                    "exact non-empty storageId",
                    Describe(storage, "storageId")));
                continue;
            }

            var exactUnique = storageIds.Add(storageId);
            var aliasUnique = storageAliasKeys.Add(
                MortalLocationIdentityState.BuildConfusableKey(storageId));
            if (exactUnique && aliasUnique)
                continue;

            issues.Add(Issue(
                storageContext + ".storageId",
                exactUnique
                    ? "mortal_location_storage_identity_confusable"
                    : "mortal_location_storage_identity_duplicate",
                exactUnique
                    ? "Location storage identities cannot use case or Unicode-confusable aliases."
                    : "Location storage identities must be unique inside one location.",
                "one unique exact/confusable storageId per location",
                storageId));
        }
    }

    private static void ValidateRawLocationStorageContents(
        JsonElement location,
        string context,
        string expectedRoute,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(expectedRoute, "world_map_creation", StringComparison.Ordinal) ||
            !location.TryGetProperty("locationStorages", out var storages) ||
            storages.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var storage in storages.EnumerateArray())
        {
            var storageIndex = index++;
            if (storage.ValueKind != JsonValueKind.Object ||
                !storage.TryGetProperty("contents", out _))
            {
                continue;
            }

            issues.Add(Issue(
                $"{context}.locationStorages[{storageIndex}].contents",
                "mortal_location_remote_storage_contents_forbidden",
                "Remote location creation may declare storage metadata but cannot carry item contents.",
                "contents absent; item carriers are allowed only in the current-scene projection",
                "present"));
        }
    }

    private static void ValidatePhysicalShape(
        JsonElement value,
        string context,
        List<ValidationIssue> issues)
    {
        var locationType = ReadExactNonEmptyString(value, "locationType");
        var biome = ReadExactNonEmptyString(value, "biome");
        var biomeDescription = ReadExactNonEmptyString(value, "biomeDescription");
        var indoorType = ReadExactNonEmptyString(value, "indoorType");

        if (string.Equals(locationType, "outdoor", StringComparison.Ordinal))
        {
            if (biome == null)
            {
                AddPhysicalShapeIssue(
                    value,
                    context,
                    "biome",
                    "exact non-empty outdoor biome",
                    issues);
            }
            if (biomeDescription == null)
            {
                AddPhysicalShapeIssue(
                    value,
                    context,
                    "biomeDescription",
                    "exact non-empty outdoor biome description",
                    issues);
            }
            if (!IsNull(value, "indoorType"))
            {
                AddPhysicalShapeIssue(
                    value,
                    context,
                    "indoorType",
                    "null for an outdoor location",
                    issues);
            }
            return;
        }

        if (string.Equals(locationType, "indoor", StringComparison.Ordinal))
        {
            if (indoorType == null)
            {
                AddPhysicalShapeIssue(
                    value,
                    context,
                    "indoorType",
                    "exact non-empty indoor type",
                    issues);
            }
            if (!IsNull(value, "biome"))
            {
                AddPhysicalShapeIssue(
                    value,
                    context,
                    "biome",
                    "null for an indoor location",
                    issues);
            }
            if (!IsNull(value, "biomeDescription"))
            {
                AddPhysicalShapeIssue(
                    value,
                    context,
                    "biomeDescription",
                    "null for an indoor location",
                    issues);
            }
            return;
        }

        var coherentOutdoorShape = biome != null &&
            biomeDescription != null &&
            IsNull(value, "indoorType");
        var coherentIndoorShape = indoorType != null &&
            IsNull(value, "biome") &&
            IsNull(value, "biomeDescription");
        if (coherentOutdoorShape || coherentIndoorShape)
        {
            issues.Add(Issue(
                $"{context}.locationType",
                "mortal_location_materialization_physical_shape_invalid",
                "The location type must exactly match its complete physical fields.",
                coherentOutdoorShape ? "outdoor" : "indoor",
                Describe(value, "locationType")));
            return;
        }

        issues.Add(Issue(
            $"{context}.locationType",
            "mortal_location_materialization_physical_shape_ambiguous",
            "The intended indoor or outdoor shape is ambiguous and cannot be repaired by one bounded field set.",
            "one coherent indoor or outdoor shape",
            Describe(value, "locationType")));
    }

    private static void AddPhysicalShapeIssue(
        JsonElement value,
        string context,
        string field,
        string expected,
        List<ValidationIssue> issues)
    {
        issues.Add(Issue(
            $"{context}.{field}",
            "mortal_location_materialization_physical_shape_invalid",
            "Indoor and outdoor location fields must be mutually exclusive and complete.",
            expected,
            Describe(value, field)));
    }

    private static void ValidateCoordinates(
        JsonElement value,
        string context,
        List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty("coordinates", out var coordinates) ||
            coordinates.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                $"{context}.coordinates",
                "mortal_location_materialization_governed_field_missing",
                "Location placement requires an exact coordinate object.",
                "object with x/y/z",
                Describe(value, "coordinates")));
            return;
        }

        ValidateClosedObject(coordinates, CoordinatesFields, $"{context}.coordinates", issues);
        foreach (var field in CoordinatesFields)
        {
            if (!coordinates.TryGetProperty(field, out var coordinate) ||
                coordinate.ValueKind != JsonValueKind.Number ||
                !coordinate.TryGetInt32(out _))
            {
                issues.Add(Issue(
                    $"{context}.coordinates.{field}",
                    "mortal_location_materialization_governed_field_missing",
                    "Each location coordinate must be an exact integer.",
                    "integer",
                    Describe(coordinates, field)));
            }
        }
    }

    private static void ValidateDiscovery(
        JsonElement value,
        string context,
        List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty("discovery", out var discovery) ||
            discovery.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                $"{context}.discovery",
                "mortal_location_materialization_discovery_invalid",
                "Location discovery must be a complete object.",
                "tier/audience/rumorSummary",
                Describe(value, "discovery")));
            return;
        }

        ValidateClosedObject(discovery, DiscoveryFields, $"{context}.discovery", issues);
        var tier = ReadExactNonEmptyString(discovery, "tier");
        var audience = ReadExactNonEmptyString(discovery, "audience");
        var rumor = ReadExactNonEmptyString(discovery, "rumorSummary");
        var valid = tier switch
        {
            "hidden" => audience == "gm_only" && IsNull(discovery, "rumorSummary"),
            "rumored" => audience == "player_known" && rumor != null,
            "discovered" or "visited" => audience == "player_known" && IsNull(discovery, "rumorSummary"),
            _ => false
        };
        if (valid)
            return;

        issues.Add(Issue(
            $"{context}.discovery",
            "mortal_location_materialization_discovery_invalid",
            "Discovery tier, audience, and rumor summary must form an allowed exact pair.",
            "hidden/gm_only, rumored/player_known with summary, or discovered|visited/player_known",
            discovery.GetRawText()));
    }

    private static void ValidateDifficulty(
        JsonElement value,
        string field,
        string context,
        List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty(field, out var difficulty) ||
            difficulty.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                $"{context}.{field}",
                "mortal_location_materialization_governed_field_missing",
                "Each location requires a complete difficulty profile.",
                "danger/recommendedLevel/description object",
                Describe(value, field)));
            return;
        }

        ValidateClosedObject(difficulty, DifficultyFields, $"{context}.{field}", issues);
        if (ReadExactNonEmptyString(difficulty, "danger") == null ||
            ReadExactNonEmptyString(difficulty, "description") == null ||
            !difficulty.TryGetProperty("recommendedLevel", out var level) ||
            level.ValueKind != JsonValueKind.Number ||
            !level.TryGetInt32(out var recommendedLevel) ||
            recommendedLevel < 1)
        {
            issues.Add(Issue(
                $"{context}.{field}",
                "mortal_location_materialization_governed_field_missing",
                "Difficulty profiles require meaningful danger, level, and description.",
                "non-empty danger/description and level >= 1",
                difficulty.GetRawText()));
        }
    }

    private static void ValidateLocationSectionEvidence(
        JsonElement value,
        string context,
        bool requireCurrentArrayAgreement,
        List<ValidationIssue> issues)
    {
        if (!TryGetSections(value, out var sections))
            return;

        foreach (var section in new[] { "presentation", "physical", "placement", "discovery", "difficulty", "chronicle" })
        {
            if (ReadDisposition(sections, section) == "populated")
                continue;
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}.sections.{section}",
                "mortal_location_materialization_section_disposition_mismatch",
                "Required location sections must be populated.",
                "populated",
                ReadDisposition(sections, section) ?? "missing"));
        }

        if (!requireCurrentArrayAgreement)
            return;

        foreach (var pair in LocationArraySections)
        {
            if (!value.TryGetProperty(pair.Value, out var array) || array.ValueKind != JsonValueKind.Array)
                continue;
            ValidateArrayDisposition(sections, pair.Key, array.GetArrayLength(), context, issues);
        }
    }

    private static void ValidateLinkFields(
        JsonElement value,
        string context,
        bool raw,
        List<ValidationIssue> issues)
    {
        foreach (var field in new[] { "name", "description", "directionLabel", "travelMode" })
            RequireNonEmptyString(value, field, context, issues);

        var linkType = ReadExactNonEmptyString(value, "linkType");
        if (linkType == null || !LinkTypes.Contains(linkType))
        {
            issues.Add(Issue(
                $"{context}.linkType",
                "mortal_location_materialization_governed_field_missing",
                "Location links require a closed link type.",
                string.Join('|', LinkTypes),
                linkType ?? "missing"));
        }

        if (raw)
        {
            ValidateSelectorXor(value, "sourceLocationId", "sourceInitialId", context, issues);
            ValidateSelectorXor(value, "targetLocationId", "targetInitialId", context, issues);
        }
        else
        {
            foreach (var field in new[] { "sourceLocationId", "targetLocationId" })
            {
                if (ReadExactNonEmptyString(value, field) == null)
                {
                    issues.Add(Issue(
                        $"{context}.{field}",
                        "mortal_location_link_endpoint_selector_invalid",
                        "Canonical links require exact permanent endpoint IDs.",
                        "exact non-empty permanent ID",
                        Describe(value, field)));
                }
            }
        }

        var source = ReadExactNonEmptyString(value, "sourceLocationId") ?? ReadExactNonEmptyString(value, "sourceInitialId");
        var target = ReadExactNonEmptyString(value, "targetLocationId") ?? ReadExactNonEmptyString(value, "targetInitialId");
        if (source != null && target != null && string.Equals(source, target, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                context,
                "mortal_location_link_endpoint_selector_invalid",
                "Self-links are not supported by the Mortal location contract.",
                "different source and target",
                source));
        }

        ValidateLinkAccess(value, context, issues);
        ValidateDiscovery(value, context, issues);
        RequireArray(value, "customStates", context, issues);
        if (value.TryGetProperty("customStates", out var customStates))
        {
            issues.AddRange(MortalLocationCustomStateContract.Validate(
                customStates,
                context + ".customStates"));
        }
    }

    private static void ValidateLinkAccess(
        JsonElement value,
        string context,
        List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty("access", out var access) || access.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                $"{context}.access",
                "mortal_location_materialization_governed_field_missing",
                "Location links require access state.",
                "access object",
                Describe(value, "access")));
            return;
        }

        ValidateClosedObject(access, LinkAccessFields, $"{context}.access", issues);
        var state = ReadExactNonEmptyString(access, "state");
        var reason = ReadExactNonEmptyString(access, "reason");
        var hasRequirements = access.TryGetProperty("requirements", out var requirements) &&
                              requirements.ValueKind == JsonValueKind.Array;
        var valid = state switch
        {
            "open" => hasRequirements,
            "conditional" or "sealed" => hasRequirements && reason != null,
            _ => false
        };
        if (valid)
            return;

        issues.Add(Issue(
            $"{context}.access",
            "mortal_location_materialization_governed_field_missing",
            "Link access must use a complete open, conditional, or sealed shape.",
            "closed access state with requirements and required reason",
            access.GetRawText()));
    }

    private static void ValidateLinkSectionEvidence(
        JsonElement value,
        string context,
        List<ValidationIssue> issues)
    {
        if (!TryGetSections(value, out var sections))
            return;

        foreach (var section in new[] { "endpoints", "presentation", "traversal", "access", "discovery" })
        {
            if (ReadDisposition(sections, section) == "populated")
                continue;
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}.sections.{section}",
                "mortal_location_materialization_section_disposition_mismatch",
                "Required link sections must be populated.",
                "populated",
                ReadDisposition(sections, section) ?? "missing"));
        }

        if (value.TryGetProperty("customStates", out var customStates) &&
            customStates.ValueKind == JsonValueKind.Array)
        {
            ValidateArrayDisposition(
                sections,
                "customStates",
                customStates.GetArrayLength(),
                context,
                issues);
        }
    }

    private static void ValidateEnvelope(
        JsonElement root,
        string context,
        string entityKind,
        string? expectedRoute,
        IReadOnlySet<string> allowedRoutes,
        IReadOnlyCollection<string> expectedSections,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(EnvelopeProperty, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}",
                "mortal_location_materialization_missing_envelope",
                "Every Mortal location or link requires a complete materialization envelope.",
                "object",
                Describe(root, EnvelopeProperty)));
            return;
        }

        ValidateClosedObject(envelope, EnvelopeFields, $"{context}.{EnvelopeProperty}", issues);
        if (!ReadExactInt(envelope, "schemaVersion", out var schemaVersion) || schemaVersion != 1 ||
            !string.Equals(ReadExactNonEmptyString(envelope, "entityKind"), entityKind, StringComparison.Ordinal) ||
            !string.Equals(ReadExactNonEmptyString(envelope, "state"), "complete", StringComparison.Ordinal))
        {
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}",
                "mortal_location_materialization_invalid_envelope",
                "Materialization envelope closed values are invalid.",
                $"schemaVersion=1; entityKind={entityKind}; state=complete",
                envelope.GetRawText()));
        }

        if (!string.Equals(ReadExactNonEmptyString(envelope, "realm"), "mortal_world", StringComparison.Ordinal))
        {
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}.realm",
                "mortal_location_materialization_wrong_realm",
                "Mortal location materialization requires realm=mortal_world.",
                "mortal_world",
                Describe(envelope, "realm")));
        }

        var route = ReadExactNonEmptyString(envelope, "route");
        if (route == null || !allowedRoutes.Contains(route) ||
            expectedRoute != null && !string.Equals(route, expectedRoute, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}.route",
                "mortal_location_materialization_route_mismatch",
                "The materialization route must match the exact raw carrier.",
                expectedRoute ?? string.Join('|', allowedRoutes),
                route ?? "missing"));
        }

        if (!ReadExactInt(envelope, "sourceTurn", out var sourceTurn) || sourceTurn < 1 ||
            ReadExactNonEmptyString(envelope, "materializationId") == null ||
            ReadExactNonEmptyString(envelope, "initialId") == null)
        {
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}",
                "mortal_location_materialization_invalid_envelope",
                "Envelope identity and source turn must be exact and non-empty.",
                "materializationId, initialId, sourceTurn>=1",
                envelope.GetRawText()));
        }

        var rootInitialId = ReadExactNonEmptyString(root, "initialId");
        if (rootInitialId != null &&
            !string.Equals(rootInitialId, ReadExactNonEmptyString(envelope, "initialId"), StringComparison.Ordinal))
        {
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}.initialId",
                "mortal_location_materialization_identity_conflict",
                "Envelope initialId must match the raw root exactly.",
                rootInitialId,
                Describe(envelope, "initialId")));
        }

        ValidateSourceAuthority(envelope, context, issues);
        ValidateSections(envelope, context, expectedSections, issues);
    }

    private static void ValidateSourceAuthority(
        JsonElement envelope,
        string context,
        List<ValidationIssue> issues)
    {
        if (!envelope.TryGetProperty("sourceAuthority", out var authority) ||
            authority.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}.sourceAuthority",
                "mortal_location_materialization_source_authority_mismatch",
                "Materialization requires exact source authority.",
                "source authority object",
                Describe(envelope, "sourceAuthority")));
            return;
        }

        ValidateClosedObject(authority, SourceAuthorityFields, $"{context}.{EnvelopeProperty}.sourceAuthority", issues);
        var kind = ReadExactNonEmptyString(authority, "kind");
        var authorityId = ReadExactNonEmptyString(authority, "authorityId");
        if (authorityId != null && kind is "turn_outcome" or "mortal_bootstrap_scaffold")
            return;

        issues.Add(Issue(
            $"{context}.{EnvelopeProperty}.sourceAuthority",
            "mortal_location_materialization_source_authority_mismatch",
            "Materialization source authority must be an exact turn or open bootstrap scaffold.",
            "turn_outcome or mortal_bootstrap_scaffold with authorityId",
            authority.GetRawText()));
    }

    private static void ValidateSections(
        JsonElement envelope,
        string context,
        IReadOnlyCollection<string> expectedSections,
        List<ValidationIssue> issues)
    {
        if (!envelope.TryGetProperty("sections", out var sections) ||
            sections.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}.sections",
                "mortal_location_materialization_invalid_envelope",
                "Materialization requires a complete sections object.",
                "closed sections object",
                Describe(envelope, "sections")));
            return;
        }

        ValidateClosedObject(
            sections,
            new HashSet<string>(expectedSections, StringComparer.Ordinal),
            $"{context}.{EnvelopeProperty}.sections",
            issues);
        foreach (var sectionName in expectedSections)
        {
            if (!sections.TryGetProperty(sectionName, out var disposition) ||
                disposition.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue(
                    $"{context}.{EnvelopeProperty}.sections.{sectionName}",
                    "mortal_location_materialization_section_disposition_mismatch",
                    "Every governed section requires a disposition object.",
                    "disposition/reason object",
                    Describe(sections, sectionName)));
                continue;
            }

            ValidateClosedObject(
                disposition,
                DispositionFields,
                $"{context}.{EnvelopeProperty}.sections.{sectionName}",
                issues);
            var state = ReadExactNonEmptyString(disposition, "disposition");
            if (state is not ("populated" or "empty_by_design"))
            {
                issues.Add(Issue(
                    $"{context}.{EnvelopeProperty}.sections.{sectionName}.disposition",
                    "mortal_location_materialization_section_disposition_mismatch",
                    "Section disposition must use a closed value.",
                    "populated or empty_by_design",
                    state ?? "missing"));
            }

            if (state == "empty_by_design" && ReadExactNonEmptyString(disposition, "reason") == null)
            {
                issues.Add(Issue(
                    $"{context}.{EnvelopeProperty}.sections.{sectionName}.reason",
                    "mortal_location_materialization_section_empty_reason_missing",
                    "An empty-by-design section requires a non-empty in-world reason.",
                    "non-empty reason",
                    Describe(disposition, "reason")));
            }
            else if (state == "populated" && !IsNull(disposition, "reason"))
            {
                issues.Add(Issue(
                    $"{context}.{EnvelopeProperty}.sections.{sectionName}.reason",
                    "mortal_location_materialization_section_disposition_mismatch",
                    "A populated section carries a null reason.",
                    "null",
                    Describe(disposition, "reason")));
            }
        }
    }

    private static void ValidateReceipt(
        JsonElement root,
        string context,
        bool isLink,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(ReceiptProperty, out var receipt) ||
            receipt.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                $"{context}.{ReceiptProperty}",
                "mortal_location_materialization_receipt_required",
                "Every canonical Mortal location or link requires a client-sealed receipt.",
                "receipt object",
                Describe(root, ReceiptProperty)));
            return;
        }

        var fields = isLink ? LinkReceiptFields : LocationReceiptFields;
        ValidateClosedObject(receipt, fields, $"{context}.{ReceiptProperty}", issues);
        if (!root.TryGetProperty(EnvelopeProperty, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object)
            return;

        var identityField = isLink ? "linkId" : "locationId";
        var mismatched =
            !Matches(root, identityField, receipt, identityField) ||
            !Matches(envelope, "initialId", receipt, "initialId") ||
            !Matches(envelope, "materializationId", receipt, "materializationId") ||
            !Matches(envelope, "realm", receipt, "realm") ||
            !Matches(envelope, "route", receipt, "route") ||
            !Matches(envelope, "sourceTurn", receipt, "sourceTurn") ||
            !MatchesNested(envelope, "sourceAuthority", "kind", receipt, "sourceAuthorityKind") ||
            !MatchesNested(envelope, "sourceAuthority", "authorityId", receipt, "sourceAuthorityId");
        if (isLink)
        {
            mismatched |= !Matches(root, "sourceLocationId", receipt, "sourceLocationId") ||
                          !Matches(root, "targetLocationId", receipt, "targetLocationId");
        }

        if (mismatched || !ReadExactInt(receipt, "schemaVersion", out var schemaVersion) || schemaVersion != 1 ||
            ReadExactNonEmptyString(receipt, "receiptId") == null)
        {
            issues.Add(Issue(
                $"{context}.{ReceiptProperty}",
                "mortal_location_materialization_receipt_mismatch",
                "The client receipt must exactly bind root identity, envelope, route, source, and endpoints.",
                "exact receipt/root/envelope agreement",
                receipt.GetRawText()));
        }

        var seal = ReadExactNonEmptyString(receipt, "seal");
        var envelopeNode = JsonNode.Parse(envelope.GetRawText())?.AsObject();
        var receiptNode = JsonNode.Parse(receipt.GetRawText())?.AsObject();
        if (envelopeNode == null || receiptNode == null || seal == null ||
            !string.Equals(seal, ComputeSeal(envelopeNode, receiptNode), StringComparison.Ordinal))
        {
            issues.Add(Issue(
                $"{context}.{ReceiptProperty}.seal",
                "mortal_location_materialization_seal_invalid",
                "The materialization receipt seal is invalid.",
                "client-computed stable seal",
                seal ?? "missing"));
        }
    }

    private static void ValidateArrayDisposition(
        JsonElement sections,
        string sectionName,
        int count,
        string context,
        List<ValidationIssue> issues)
    {
        var disposition = ReadDisposition(sections, sectionName);
        if (disposition == "populated" && count > 0 ||
            disposition == "empty_by_design" && count == 0)
        {
            return;
        }

        issues.Add(Issue(
            $"{context}.{EnvelopeProperty}.sections.{sectionName}",
            "mortal_location_materialization_section_disposition_mismatch",
            "Section disposition must agree with its physical collection.",
            count == 0 ? "empty_by_design" : "populated",
            disposition ?? "missing"));
    }

    private static bool TryGetSections(JsonElement root, out JsonElement sections)
    {
        sections = default;
        return root.TryGetProperty(EnvelopeProperty, out var envelope) &&
               envelope.ValueKind == JsonValueKind.Object &&
               envelope.TryGetProperty("sections", out sections) &&
               sections.ValueKind == JsonValueKind.Object;
    }

    private static string? ReadDisposition(JsonElement sections, string sectionName)
    {
        return sections.TryGetProperty(sectionName, out var section) &&
               section.ValueKind == JsonValueKind.Object
            ? ReadExactNonEmptyString(section, "disposition")
            : null;
    }

    private static void ValidateSelectorXor(
        JsonElement root,
        string permanentField,
        string temporaryField,
        string context,
        List<ValidationIssue> issues)
    {
        var permanentValid = TryReadNullableExactSelector(root, permanentField, out var permanentSelected);
        var temporaryValid = TryReadNullableExactSelector(root, temporaryField, out var temporarySelected);
        if (permanentValid && temporaryValid && permanentSelected ^ temporarySelected)
            return;

        var issueField = permanentValid && !temporaryValid
            ? temporaryField
            : permanentField;
        issues.Add(Issue(
            $"{context}.{issueField}",
            "mortal_location_link_endpoint_selector_invalid",
            "Each raw link endpoint requires exactly one nullable exact permanent or temporary selector.",
            $"{permanentField}=exact string and {temporaryField}=null, or the inverse",
            $"{permanentField}={Describe(root, permanentField)}; {temporaryField}={Describe(root, temporaryField)}"));
    }

    private static void ValidateSelectorXorOrNeither(
        JsonElement root,
        string permanentField,
        string temporaryField,
        string context,
        List<ValidationIssue> issues)
    {
        var permanentValid = TryReadNullableExactSelector(root, permanentField, out var permanentSelected);
        var temporaryValid = TryReadNullableExactSelector(root, temporaryField, out var temporarySelected);
        if (permanentValid && temporaryValid && !(permanentSelected && temporarySelected))
            return;

        var issueField = permanentValid && !temporaryValid
            ? temporaryField
            : permanentField;
        issues.Add(Issue(
            $"{context}.{issueField}",
            "mortal_location_materialization_identity_conflict",
            "A parent location requires two nullable exact selector fields and may select at most one.",
            $"{permanentField}=exact string/null; {temporaryField}=exact string/null; at most one string",
            $"{permanentField}={Describe(root, permanentField)}; {temporaryField}={Describe(root, temporaryField)}"));
    }

    private static bool TryReadNullableExactSelector(
        JsonElement root,
        string field,
        out bool selected)
    {
        selected = false;
        if (!root.TryGetProperty(field, out var value))
            return false;
        if (value.ValueKind == JsonValueKind.Null)
            return true;
        if (ReadExactNonEmptyString(root, field) == null)
            return false;

        selected = true;
        return true;
    }

    private static void ValidateClosedObject(
        JsonElement value,
        IReadOnlySet<string> fields,
        string context,
        List<ValidationIssue> issues)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (fields.Contains(property.Name))
                continue;
            issues.Add(Issue(
                $"{context}.{property.Name}",
                "mortal_location_materialization_unknown_field",
                "Closed materialization objects cannot contain unknown fields.",
                string.Join(',', fields.OrderBy(static field => field, StringComparer.Ordinal)),
                property.Name));
        }

        foreach (var field in fields)
        {
            if (value.TryGetProperty(field, out _))
                continue;
            issues.Add(Issue(
                $"{context}.{field}",
                "mortal_location_materialization_invalid_envelope",
                "Closed materialization objects require every declared field.",
                "field present",
                "missing"));
        }
    }

    private static void RequireNonEmptyString(
        JsonElement root,
        string field,
        string context,
        List<ValidationIssue> issues)
    {
        if (ReadExactNonEmptyString(root, field) != null)
            return;
        issues.Add(Issue(
            $"{context}.{field}",
            "mortal_location_materialization_governed_field_missing",
            "A complete location or link requires this non-empty field.",
            "exact non-empty string",
            Describe(root, field)));
    }

    private static void RequireExactString(
        JsonElement root,
        string field,
        string expected,
        string context,
        List<ValidationIssue> issues,
        string code)
    {
        var actual = ReadExactNonEmptyString(root, field);
        if (string.Equals(actual, expected, StringComparison.Ordinal))
            return;
        issues.Add(Issue(
            $"{context}.{field}",
            code,
            "A closed location field has the wrong exact value.",
            expected,
            actual ?? "missing"));
    }

    private static void RequireArray(
        JsonElement root,
        string field,
        string context,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Array)
            return;
        issues.Add(Issue(
            $"{context}.{field}",
            "mortal_location_materialization_governed_field_missing",
            "A complete location or link must physically carry this array.",
            "array",
            Describe(root, field)));
    }

    private static void RequireProperty(
        JsonElement root,
        string field,
        string context,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out _))
            return;
        issues.Add(Issue(
            $"{context}.{field}",
            "mortal_location_materialization_governed_field_missing",
            "A complete location must physically carry this selector field.",
            "field present",
            "missing"));
    }

    private static void ValidateDuplicateProperties(
        JsonElement value,
        string context,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var propertyPath = $"{context}.{property.Name}";
                if (!names.Add(property.Name))
                {
                    issues.Add(Issue(
                        propertyPath,
                        "mortal_location_materialization_duplicate_property",
                        "Duplicate JSON properties are forbidden in location materialization state.",
                        "unique exact property names",
                        property.Name));
                }
                ValidateDuplicateProperties(property.Value, propertyPath, issues);
            }
            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
            return;
        var index = 0;
        foreach (var element in value.EnumerateArray())
        {
            ValidateDuplicateProperties(element, $"{context}[{index}]", issues);
            index++;
        }
    }

    private static bool Matches(
        JsonElement left,
        string leftField,
        JsonElement right,
        string rightField)
    {
        if (!left.TryGetProperty(leftField, out var leftValue) ||
            !right.TryGetProperty(rightField, out var rightValue))
            return false;
        return string.Equals(
            leftValue.GetRawText(),
            rightValue.GetRawText(),
            StringComparison.Ordinal);
    }

    private static bool MatchesNested(
        JsonElement left,
        string objectField,
        string nestedField,
        JsonElement right,
        string rightField)
    {
        return left.TryGetProperty(objectField, out var nested) &&
               nested.ValueKind == JsonValueKind.Object &&
               Matches(nested, nestedField, right, rightField);
    }

    private static bool IsNull(JsonElement root, string field)
    {
        return root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Null;
    }

    private static string? ReadExactNonEmptyString(JsonElement root, string field)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString();
        return !string.IsNullOrEmpty(text) && string.Equals(text, text.Trim(), StringComparison.Ordinal)
            ? text
            : null;
    }

    private static bool ReadExactInt(JsonElement root, string field, out int result)
    {
        result = default;
        return root.TryGetProperty(field, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out result);
    }

    private static string Describe(JsonElement root, string field)
    {
        return !root.TryGetProperty(field, out var value)
            ? "missing"
            : value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? "null"
                : value.GetRawText();
    }

    private static ValidationIssue Issue(
        string path,
        string code,
        string message,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "mortal_location_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Исправьте только GM-owned semantic field named by this issue.");

    private static JsonNode Canonicalize(JsonNode value) =>
        value switch
        {
            JsonObject obj => CanonicalizeObject(obj),
            JsonArray array => new JsonArray(
                array.Select(static element => element == null ? null : Canonicalize(element)).ToArray()),
            _ => value.DeepClone()
        };

    private static JsonObject CanonicalizeObject(JsonObject value)
    {
        var result = new JsonObject();
        foreach (var property in value.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            result[property.Key] = property.Value == null ? null : Canonicalize(property.Value);
        return result;
    }
}
