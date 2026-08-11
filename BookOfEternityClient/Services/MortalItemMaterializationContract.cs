using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum MortalItemMaterializationPhase
{
    RawPreSeal,
    CanonicalPostSeal
}

internal static class MortalItemMaterializationContract
{
    internal const int SchemaVersion = 1;
    internal const string EnvelopeProperty = "materialization";
    internal const string ReceiptProperty = "materializationReceipt";

    internal static readonly string[] SectionNames =
    {
        "presentation",
        "physical",
        "mechanics",
        "equipment",
        "container",
        "consumption",
        "readableOrSentient",
        "craftingAndDisassembly",
        "bondsAndFateCards",
        "questRole",
        "provenance",
        "ownershipAndPlacement"
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
        "realm",
        "route",
        "sourceTurn",
        "sourceAuthority",
        "creationRef",
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
        "state",
        "reason"
    };

    private static readonly HashSet<string> ReceiptFields = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "receiptId",
        "itemId",
        "materializationId",
        "acceptedAtTurn",
        "creationRef",
        "instanceKind",
        "parentItemIds",
        "seal"
    };

    private static readonly HashSet<string> CanonicalQualities = new(StringComparer.Ordinal)
    {
        "Trash",
        "Common",
        "Uncommon",
        "Good",
        "Rare",
        "Epic",
        "Legendary",
        "Unique"
    };

    private static readonly IReadOnlyDictionary<string, string> RouteAuthorityKinds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["player_acquisition"] = "turn_outcome",
            ["npc_acquisition"] = "npc_inventory_add",
            ["new_npc_inventory"] = "new_npc",
            ["loot_acquisition"] = "loot_template",
            ["craft_output"] = "craft_request",
            ["trade_output"] = "npc_trade_receipt",
            ["quest_reward"] = "quest_reward",
            ["storage_placement"] = "location_storage"
        };

    private static readonly IReadOnlyDictionary<string, string[]> GovernedFields =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["presentation"] = new[]
            {
                "name", "description", "image_prompt", "type", "group", "quality", "rarity"
            },
            ["physical"] = new[]
            {
                "price", "count", "weight", "volume", "durability", "maxDurability"
            },
            ["mechanics"] = new[]
            {
                "bonuses", "effects", "structuredBonuses", "combatEffect", "customProperties",
                "mechanicalSummaryAuthority", "mechanicalSummaryUnresolvedReason"
            },
            ["equipment"] = new[]
            {
                "equipmentSlot", "accessoryForSlot", "requiresTwoHands"
            },
            ["container"] = new[]
            {
                "isContainer", "capacity", "containerWeight", "weightReduction", "contentsPath"
            },
            ["consumption"] = new[] { "isConsumption" },
            ["readableOrSentient"] = new[]
            {
                "textContent", "journalEntries", "isSentient", "unreadableReason", "sealedReason", "lockedReason"
            },
            ["craftingAndDisassembly"] = new[] { "disassembleTo" },
            ["bondsAndFateCards"] = new[]
            {
                "ownerBondLevelCurrent", "ownerBondLevelMax", "fateCards"
            },
            ["questRole"] = new[] { "questLinks" },
            ["provenance"] = Array.Empty<string>(),
            ["ownershipAndPlacement"] = new[] { "contentsPath" }
        };

    internal static IReadOnlyList<ValidationIssue> Validate(
        JsonElement item,
        string context,
        MortalItemMaterializationPhase phase)
    {
        var issues = new List<ValidationIssue>();
        if (item.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                context,
                "mortal_item_materialization_invalid_envelope",
                "A durable Mortal item must be a JSON object.",
                "object",
                item.ValueKind.ToString(),
                null));
            return BindActor(issues, "mortal_item:unknown");
        }

        ValidateDuplicateProperties(item, context, issues);
        ValidateIdentity(item, context, phase, issues);
        ValidateEnvelope(item, context, issues);
        ValidateGovernedFields(item, context, issues);

        if (phase == MortalItemMaterializationPhase.CanonicalPostSeal)
            ValidateReceipt(item, context, issues);

        return BindActor(issues, ResolveActor(item, phase));
    }

    internal static bool HasCompleteEnvelope(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return false;

        var issues = new List<ValidationIssue>();
        ValidateDuplicateProperties(item, "mortal-item-reader", issues);
        ValidateEnvelope(item, "mortal-item-reader", issues);
        ValidateGovernedFields(item, "mortal-item-reader", issues);
        return issues.Count == 0;
    }

    internal static string ComputeSeal(JsonObject item, JsonObject receiptWithoutSeal)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(receiptWithoutSeal);

        if (item[EnvelopeProperty] is not JsonObject envelope)
            throw new InvalidOperationException("Cannot seal an item without a materialization envelope.");

        var input = new JsonObject
        {
            ["schemaVersion"] = RequiredClone(receiptWithoutSeal, "schemaVersion"),
            ["receiptId"] = RequiredClone(receiptWithoutSeal, "receiptId"),
            ["itemId"] = RequiredClone(receiptWithoutSeal, "itemId"),
            ["materializationId"] = RequiredClone(receiptWithoutSeal, "materializationId"),
            ["acceptedAtTurn"] = RequiredClone(receiptWithoutSeal, "acceptedAtTurn"),
            ["creationRef"] = RequiredClone(receiptWithoutSeal, "creationRef"),
            ["instanceKind"] = RequiredClone(receiptWithoutSeal, "instanceKind"),
            ["parentItemIds"] = RequiredClone(receiptWithoutSeal, "parentItemIds"),
            [EnvelopeProperty] = Canonicalize(envelope)
        };

        var bytes = Encoding.UTF8.GetBytes(input.ToJsonString(CompactJson));
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

    private static void ValidateIdentity(
        JsonElement item,
        string context,
        MortalItemMaterializationPhase phase,
        List<ValidationIssue> issues)
    {
        if (phase == MortalItemMaterializationPhase.RawPreSeal)
        {
            if (!item.TryGetProperty("existedId", out var rawExistedId) ||
                rawExistedId.ValueKind != JsonValueKind.Null)
            {
                issues.Add(Issue(
                    $"{context}.existedId",
                    "mortal_item_materialization_identity_conflict",
                    "A raw new item must explicitly declare existedId as null.",
                    "null",
                    Describe(item, "existedId"),
                    null));
            }

            foreach (var clientField in new[] { "itemId", "id", "initialId", ReceiptProperty })
            {
                if (!item.TryGetProperty(clientField, out _))
                    continue;

                issues.Add(Issue(
                    $"{context}.{clientField}",
                    "mortal_item_materialization_gm_authored_client_field",
                    "The GM cannot author permanent Mortal item identity evidence.",
                    "field absent before client sealing",
                    "present",
                    null));
            }

            var creationRef = ReadExactNonEmptyString(item, "creationRef");
            if (creationRef == null)
            {
                issues.Add(Issue(
                    $"{context}.creationRef",
                    "mortal_item_materialization_identity_conflict",
                    "A raw new item requires one exact temporary creationRef.",
                    "non-empty untrimmed creationRef",
                    Describe(item, "creationRef"),
                    null));
            }

            return;
        }

        var itemId = ReadExactNonEmptyString(item, "itemId");
        var existedId = ReadExactNonEmptyString(item, "existedId");
        if (itemId == null || existedId == null ||
            !string.Equals(itemId, existedId, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                context,
                "mortal_item_materialization_identity_conflict",
                "Canonical Mortal itemId and existedId must both exist and be ordinal-equal.",
                "equal non-empty itemId and existedId",
                $"itemId={itemId ?? "missing"}; existedId={existedId ?? "missing"}",
                null));
        }

        foreach (var forbiddenAlias in new[] { "id", "initialId", "creationRef" })
        {
            if (!item.TryGetProperty(forbiddenAlias, out _))
                continue;

            issues.Add(Issue(
                $"{context}.{forbiddenAlias}",
                "mortal_item_materialization_identity_conflict",
                "Canonical Mortal items cannot retain temporary or legacy identity aliases.",
                "field absent",
                "present",
                null));
        }

        if (!item.TryGetProperty(ReceiptProperty, out _))
        {
            issues.Add(Issue(
                $"{context}.{ReceiptProperty}",
                "mortal_item_materialization_receiptless_current_item",
                "Every canonical Mortal item requires a client-sealed receipt.",
                "receipt object",
                "missing",
                null));
        }
    }

    private static void ValidateEnvelope(
        JsonElement item,
        string context,
        List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty(EnvelopeProperty, out var envelope))
        {
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}",
                "mortal_item_materialization_missing_envelope",
                "Every durable Mortal item requires a complete materialization envelope.",
                "materialization object",
                "missing",
                null));
            return;
        }

        if (envelope.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                $"{context}.{EnvelopeProperty}",
                "mortal_item_materialization_invalid_envelope",
                "Mortal item materialization must be an object.",
                "object",
                envelope.ValueKind.ToString(),
                null));
            return;
        }

        var envelopeContext = $"{context}.{EnvelopeProperty}";
        ValidateExactFields(envelope, EnvelopeFields, envelopeContext, issues);

        if (!envelope.TryGetProperty("schemaVersion", out var versionNode) ||
            versionNode.ValueKind != JsonValueKind.Number ||
            !versionNode.TryGetInt32(out var version) ||
            version != SchemaVersion)
        {
            issues.Add(InvalidEnvelope(
                $"{envelopeContext}.schemaVersion",
                SchemaVersion.ToString(),
                Describe(envelope, "schemaVersion")));
        }

        RequireExactString(envelope, "materializationId", envelopeContext, issues);
        if (!string.Equals(ReadExactNonEmptyString(envelope, "realm"), "Mortal", StringComparison.Ordinal))
        {
            issues.Add(Issue(
                $"{envelopeContext}.realm",
                "mortal_item_materialization_wrong_realm",
                "Ordinary item materialization belongs to the exact Mortal realm.",
                "Mortal",
                Describe(envelope, "realm"),
                null));
        }

        var route = ReadExactNonEmptyString(envelope, "route");
        if (route == null || !RouteAuthorityKinds.TryGetValue(route, out var expectedAuthorityKind))
        {
            issues.Add(InvalidEnvelope(
                $"{envelopeContext}.route",
                string.Join(" | ", RouteAuthorityKinds.Keys),
                route ?? Describe(envelope, "route")));
            expectedAuthorityKind = null;
        }

        if (!envelope.TryGetProperty("sourceTurn", out var turnNode) ||
            turnNode.ValueKind != JsonValueKind.Number ||
            !turnNode.TryGetInt32(out var sourceTurn) ||
            sourceTurn < 1)
        {
            issues.Add(InvalidEnvelope(
                $"{envelopeContext}.sourceTurn",
                "integer >= 1",
                Describe(envelope, "sourceTurn")));
        }

        ValidateSourceAuthority(
            envelope,
            envelopeContext,
            expectedAuthorityKind,
            issues);

        var envelopeCreationRef = ReadExactNonEmptyString(envelope, "creationRef");
        if (envelopeCreationRef == null)
        {
            issues.Add(InvalidEnvelope(
                $"{envelopeContext}.creationRef",
                "non-empty untrimmed creationRef",
                Describe(envelope, "creationRef")));
        }

        if (item.TryGetProperty("creationRef", out _))
        {
            var rootCreationRef = ReadExactNonEmptyString(item, "creationRef");
            if (!string.Equals(rootCreationRef, envelopeCreationRef, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    $"{envelopeContext}.creationRef",
                    "mortal_item_materialization_identity_conflict",
                    "Top-level and embedded creationRef values must be ordinal-equal.",
                    rootCreationRef ?? "valid top-level creationRef",
                    envelopeCreationRef ?? "missing",
                    null));
            }
        }

        if (!string.Equals(ReadExactNonEmptyString(envelope, "state"), "complete", StringComparison.Ordinal))
        {
            issues.Add(InvalidEnvelope(
                $"{envelopeContext}.state",
                "complete",
                Describe(envelope, "state")));
        }

        ValidateSections(envelope, envelopeContext, issues);
    }

    private static void ValidateSourceAuthority(
        JsonElement envelope,
        string envelopeContext,
        string? expectedAuthorityKind,
        List<ValidationIssue> issues)
    {
        var context = $"{envelopeContext}.sourceAuthority";
        if (!envelope.TryGetProperty("sourceAuthority", out var authority) ||
            authority.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                context,
                "mortal_item_materialization_route_authority_missing",
                "Mortal item materialization requires structured route authority.",
                "object with kind and authorityId",
                Describe(envelope, "sourceAuthority"),
                "provenance"));
            return;
        }

        ValidateExactFields(authority, SourceAuthorityFields, context, issues);
        var kind = ReadExactNonEmptyString(authority, "kind");
        var authorityId = ReadExactNonEmptyString(authority, "authorityId");
        if (kind == null || authorityId == null)
        {
            issues.Add(Issue(
                context,
                "mortal_item_materialization_route_authority_missing",
                "Mortal item materialization route authority requires exact non-empty fields.",
                "non-empty kind and authorityId",
                authority.ToString(),
                "provenance"));
        }

        if (expectedAuthorityKind != null &&
            !string.Equals(kind, expectedAuthorityKind, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                $"{context}.kind",
                "mortal_item_materialization_route_authority_mismatch",
                "Mortal item route and authority kind do not agree.",
                expectedAuthorityKind,
                kind ?? "missing",
                "provenance"));
        }
    }

    private static void ValidateSections(
        JsonElement envelope,
        string envelopeContext,
        List<ValidationIssue> issues)
    {
        var sectionsContext = $"{envelopeContext}.sections";
        if (!envelope.TryGetProperty("sections", out var sections) ||
            sections.ValueKind != JsonValueKind.Object)
        {
            issues.Add(InvalidEnvelope(
                sectionsContext,
                "object with twelve exact section dispositions",
                Describe(envelope, "sections")));
            return;
        }

        ValidateExactFields(
            sections,
            new HashSet<string>(SectionNames, StringComparer.Ordinal),
            sectionsContext,
            issues);

        foreach (var section in SectionNames)
        {
            if (!sections.TryGetProperty(section, out var disposition))
            {
                issues.Add(Issue(
                    $"{sectionsContext}.{section}",
                    "mortal_item_materialization_section_missing",
                    "Every governed Mortal item section requires a disposition.",
                    "populated or empty_by_design disposition",
                    "missing",
                    section));
                continue;
            }

            ValidateDisposition(disposition, sectionsContext, section, issues);
        }
    }

    private static void ValidateDisposition(
        JsonElement disposition,
        string sectionsContext,
        string section,
        List<ValidationIssue> issues)
    {
        var context = $"{sectionsContext}.{section}";
        if (disposition.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                context,
                "mortal_item_materialization_section_state_mismatch",
                "Mortal item section disposition must be an object.",
                "disposition object",
                disposition.ValueKind.ToString(),
                section));
            return;
        }

        ValidateExactFields(disposition, DispositionFields, context, issues);
        var state = ReadExactNonEmptyString(disposition, "state");
        if (state is not ("populated" or "empty_by_design"))
        {
            issues.Add(Issue(
                $"{context}.state",
                "mortal_item_materialization_section_state_mismatch",
                "Section disposition state must use the exact contract vocabulary.",
                "populated or empty_by_design",
                state ?? Describe(disposition, "state"),
                section));
            return;
        }

        var reasonExists = disposition.TryGetProperty("reason", out var reason);
        if (state == "populated")
        {
            if (!reasonExists || reason.ValueKind != JsonValueKind.Null)
            {
                issues.Add(Issue(
                    $"{context}.reason",
                    "mortal_item_materialization_invalid_envelope",
                    "A populated section must carry an explicit null reason.",
                    "null",
                    reasonExists ? reason.ToString() : "missing",
                    section));
            }

            return;
        }

        if (!reasonExists || reason.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(reason.GetString()))
        {
            issues.Add(Issue(
                $"{context}.reason",
                "mortal_item_materialization_section_empty_reason_missing",
                "An empty-by-design section requires a non-empty in-world reason.",
                "non-empty in-world string",
                reasonExists ? reason.ToString() : "missing",
                section));
        }
    }

    private static void ValidateGovernedFields(
        JsonElement item,
        string context,
        List<ValidationIssue> issues)
    {
        var checkedFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (section, fields) in GovernedFields)
        {
            foreach (var field in fields)
            {
                if (!checkedFields.Add(field))
                    continue;
                if (item.TryGetProperty(field, out _))
                    continue;

                issues.Add(Issue(
                    $"{context}.{field}",
                    "mortal_item_materialization_complete_field_missing",
                    "A complete Mortal item must physically carry every governed field.",
                    "field present with exact value or empty shape",
                    "missing",
                    section));
            }
        }

        ValidatePresentation(item, context, issues);
        ValidatePhysical(item, context, issues);

        if (!item.TryGetProperty(EnvelopeProperty, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("sections", out var sections) ||
            sections.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var section in SectionNames)
        {
            if (!sections.TryGetProperty(section, out var disposition) ||
                disposition.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var state = ReadExactNonEmptyString(disposition, "state");
            if (state == "empty_by_design")
                ValidateCanonicalEmptySurface(item, context, section, issues);
            else if (state == "populated" &&
                     section is not ("presentation" or "physical" or "provenance" or "ownershipAndPlacement") &&
                     !HasPopulatedEvidence(item, section))
            {
                issues.Add(Issue(
                    $"{context}.{EnvelopeProperty}.sections.{section}",
                    "mortal_item_materialization_section_state_mismatch",
                    "A populated section requires matching structured item evidence.",
                    "non-empty governed evidence",
                    "canonical empty surface",
                    section));
            }

            if (state == "empty_by_design" &&
                section is "presentation" or "physical" or "provenance" or "ownershipAndPlacement")
            {
                issues.Add(Issue(
                    $"{context}.{EnvelopeProperty}.sections.{section}",
                    "mortal_item_materialization_section_state_mismatch",
                    "Durable item presentation, physical, provenance, and placement sections are always populated.",
                    "populated",
                    "empty_by_design",
                    section));
            }
        }
    }

    private static void ValidatePresentation(
        JsonElement item,
        string context,
        List<ValidationIssue> issues)
    {
        foreach (var field in GovernedFields["presentation"])
        {
            if (ReadNonEmptyString(item, field) != null)
                continue;

            issues.Add(Issue(
                $"{context}.{field}",
                "mortal_item_materialization_invalid_envelope",
                "Mortal item presentation fields must be non-empty strings.",
                "non-empty string",
                Describe(item, field),
                "presentation"));
        }

        var imagePrompt = ReadNonEmptyString(item, "image_prompt");
        if (imagePrompt == null || imagePrompt.Length > 150 ||
            imagePrompt.Any(character => character is >= '\u0400' and <= '\u052f'))
        {
            issues.Add(Issue(
                $"{context}.image_prompt",
                "mortal_item_materialization_invalid_image_prompt",
                "A materialized Mortal item requires an English-only image_prompt no longer than 150 characters.",
                "non-empty English-only string <= 150 characters",
                imagePrompt ?? Describe(item, "image_prompt"),
                "presentation"));
        }

        var quality = ReadNonEmptyString(item, "quality");
        var rarity = ReadNonEmptyString(item, "rarity");
        if (quality == null || !CanonicalQualities.Contains(quality) ||
            rarity == null || !CanonicalQualities.Contains(rarity) ||
            !string.Equals(quality, rarity, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                $"{context}.quality",
                "mortal_item_materialization_invalid_envelope",
                "Item quality and rarity must use one equal canonical ordinal value.",
                string.Join(" | ", CanonicalQualities),
                $"quality={quality ?? "missing"}; rarity={rarity ?? "missing"}",
                "presentation"));
        }
    }

    private static void ValidatePhysical(
        JsonElement item,
        string context,
        List<ValidationIssue> issues)
    {
        ValidateNonNegativeNumber(item, context, "price", issues);
        ValidateNonNegativeNumber(item, context, "weight", issues);
        ValidateNonNegativeNumber(item, context, "volume", issues);

        if (!item.TryGetProperty("count", out var countNode) ||
            countNode.ValueKind != JsonValueKind.Number ||
            !countNode.TryGetInt32(out var count) ||
            count < 1)
        {
            issues.Add(PhysicalIssue(
                $"{context}.count",
                "positive integer",
                Describe(item, "count")));
        }

        var durability = ReadPercentage(item, "durability");
        var maximum = ReadPercentage(item, "maxDurability");
        if (durability == null || maximum == null || durability > maximum)
        {
            issues.Add(PhysicalIssue(
                $"{context}.durability",
                "percentage strings with durability <= maxDurability",
                $"durability={Describe(item, "durability")}; maxDurability={Describe(item, "maxDurability")}"));
        }
    }

    private static void ValidateNonNegativeNumber(
        JsonElement item,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (item.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out var number) &&
            double.IsFinite(number) &&
            number >= 0)
        {
            return;
        }

        issues.Add(PhysicalIssue(
            $"{context}.{field}",
            "finite number >= 0",
            Describe(item, field)));
    }

    private static int? ReadPercentage(JsonElement item, string field)
    {
        var value = ReadNonEmptyString(item, field);
        if (value == null || !value.EndsWith('%') ||
            !int.TryParse(value.AsSpan(0, value.Length - 1), out var percent) ||
            percent is < 0 or > 100)
        {
            return null;
        }

        return percent;
    }

    private static void ValidateCanonicalEmptySurface(
        JsonElement item,
        string context,
        string section,
        List<ValidationIssue> issues)
    {
        var valid = section switch
        {
            "mechanics" =>
                IsEmptyArray(item, "bonuses") &&
                IsEmptyArray(item, "effects") &&
                IsEmptyArray(item, "structuredBonuses") &&
                IsEmptyArray(item, "combatEffect") &&
                IsEmptyArray(item, "customProperties") &&
                IsNull(item, "mechanicalSummaryAuthority") &&
                IsNull(item, "mechanicalSummaryUnresolvedReason"),
            "equipment" =>
                IsNull(item, "equipmentSlot") &&
                IsNull(item, "accessoryForSlot") &&
                IsFalse(item, "requiresTwoHands"),
            "container" =>
                IsFalse(item, "isContainer") &&
                IsNull(item, "capacity") &&
                IsNull(item, "containerWeight") &&
                IsNull(item, "weightReduction"),
            "consumption" => IsFalse(item, "isConsumption"),
            "readableOrSentient" =>
                IsNull(item, "textContent") &&
                IsEmptyArray(item, "journalEntries") &&
                IsFalse(item, "isSentient") &&
                IsNull(item, "unreadableReason") &&
                IsNull(item, "sealedReason") &&
                IsNull(item, "lockedReason"),
            "craftingAndDisassembly" => IsNull(item, "disassembleTo"),
            "bondsAndFateCards" =>
                IsNull(item, "ownerBondLevelCurrent") &&
                IsNull(item, "ownerBondLevelMax") &&
                IsEmptyArray(item, "fateCards"),
            "questRole" => IsEmptyArray(item, "questLinks"),
            "presentation" or "physical" or "provenance" or "ownershipAndPlacement" => false,
            _ => true
        };

        if (valid)
            return;

        issues.Add(Issue(
            $"{context}.{EnvelopeProperty}.sections.{section}",
            "mortal_item_materialization_canonical_empty_surface_missing",
            "An empty-by-design section must retain its exact physical empty fields.",
            "exact null, false, and empty-array surface",
            "missing or contradictory empty evidence",
            section));
    }

    private static bool HasPopulatedEvidence(JsonElement item, string section) =>
        section switch
        {
            "mechanics" =>
                HasNonEmptyArray(item, "bonuses") ||
                HasNonEmptyArray(item, "effects") ||
                HasNonEmptyArray(item, "structuredBonuses") ||
                HasNonEmptyArray(item, "combatEffect") ||
                HasNonEmptyArray(item, "customProperties") ||
                ReadNonEmptyString(item, "mechanicalSummaryAuthority") != null,
            "equipment" =>
                HasNonEmptyStringOrArray(item, "equipmentSlot") ||
                HasNonEmptyStringOrArray(item, "accessoryForSlot"),
            "container" =>
                IsTrue(item, "isContainer") &&
                HasPositiveInteger(item, "capacity"),
            "consumption" => IsTrue(item, "isConsumption"),
            "readableOrSentient" =>
                HasNonEmptyStringOrArray(item, "textContent") ||
                HasNonEmptyArray(item, "journalEntries") ||
                IsTrue(item, "isSentient") ||
                ReadNonEmptyString(item, "unreadableReason") != null ||
                ReadNonEmptyString(item, "sealedReason") != null ||
                ReadNonEmptyString(item, "lockedReason") != null,
            "craftingAndDisassembly" => HasNonEmptyArray(item, "disassembleTo"),
            "bondsAndFateCards" =>
                !IsNull(item, "ownerBondLevelCurrent") ||
                !IsNull(item, "ownerBondLevelMax") ||
                HasNonEmptyArray(item, "fateCards"),
            "questRole" => HasNonEmptyArray(item, "questLinks"),
            _ => true
        };

    private static void ValidateReceipt(
        JsonElement item,
        string context,
        List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty(ReceiptProperty, out var receipt))
            return;

        var receiptContext = $"{context}.{ReceiptProperty}";
        if (receipt.ValueKind != JsonValueKind.Object)
        {
            issues.Add(InvalidReceipt(
                receiptContext,
                "object",
                receipt.ValueKind.ToString()));
            return;
        }

        ValidateExactFields(receipt, ReceiptFields, receiptContext, issues);
        if (!receipt.TryGetProperty("schemaVersion", out var versionNode) ||
            versionNode.ValueKind != JsonValueKind.Number ||
            !versionNode.TryGetInt32(out var version) ||
            version != SchemaVersion)
        {
            issues.Add(InvalidReceipt(
                $"{receiptContext}.schemaVersion",
                SchemaVersion.ToString(),
                Describe(receipt, "schemaVersion")));
        }

        var receiptId = ReadExactNonEmptyString(receipt, "receiptId");
        var receiptItemId = ReadExactNonEmptyString(receipt, "itemId");
        var receiptMaterializationId = ReadExactNonEmptyString(receipt, "materializationId");
        var receiptCreationRef = ReadExactNonEmptyString(receipt, "creationRef");
        var instanceKind = ReadExactNonEmptyString(receipt, "instanceKind");
        foreach (var (field, value) in new[]
                 {
                     ("receiptId", receiptId),
                     ("itemId", receiptItemId),
                     ("materializationId", receiptMaterializationId),
                     ("creationRef", receiptCreationRef)
                 })
        {
            if (value == null)
            {
                issues.Add(InvalidReceipt(
                    $"{receiptContext}.{field}",
                    "non-empty untrimmed string",
                    Describe(receipt, field)));
            }
        }

        if (!receipt.TryGetProperty("acceptedAtTurn", out var turnNode) ||
            turnNode.ValueKind != JsonValueKind.Number ||
            !turnNode.TryGetInt32(out var acceptedAtTurn) ||
            acceptedAtTurn < 1)
        {
            issues.Add(InvalidReceipt(
                $"{receiptContext}.acceptedAtTurn",
                "integer >= 1",
                Describe(receipt, "acceptedAtTurn")));
            acceptedAtTurn = -1;
        }

        var parentIdsValid = TryReadParentItemIds(receipt, out var parentItemIds);
        if (!parentIdsValid ||
            instanceKind is not ("root" or "split_derived") ||
            (instanceKind == "root" && parentItemIds.Count != 0) ||
            (instanceKind == "split_derived" && parentItemIds.Count != 1))
        {
            issues.Add(InvalidReceipt(
                receiptContext,
                "root with no parents or split_derived with exactly one parent",
                $"instanceKind={instanceKind ?? "missing"}; parents={parentItemIds.Count}"));
        }

        var itemId = ReadExactNonEmptyString(item, "itemId");
        var envelope = item.TryGetProperty(EnvelopeProperty, out var envelopeNode) &&
                       envelopeNode.ValueKind == JsonValueKind.Object
            ? envelopeNode
            : default;
        var materializationId = envelope.ValueKind == JsonValueKind.Object
            ? ReadExactNonEmptyString(envelope, "materializationId")
            : null;
        var creationRef = envelope.ValueKind == JsonValueKind.Object
            ? ReadExactNonEmptyString(envelope, "creationRef")
            : null;
        var sourceTurn = envelope.ValueKind == JsonValueKind.Object &&
                         envelope.TryGetProperty("sourceTurn", out var sourceTurnNode) &&
                         sourceTurnNode.TryGetInt32(out var parsedSourceTurn)
            ? parsedSourceTurn
            : -1;

        var acceptedTurnMatchesEnvelope = instanceKind == "split_derived"
            ? acceptedAtTurn >= sourceTurn
            : acceptedAtTurn == sourceTurn;
        if (!string.Equals(itemId, receiptItemId, StringComparison.Ordinal) ||
            !string.Equals(materializationId, receiptMaterializationId, StringComparison.Ordinal) ||
            !string.Equals(creationRef, receiptCreationRef, StringComparison.Ordinal) ||
            !acceptedTurnMatchesEnvelope)
        {
            issues.Add(Issue(
                receiptContext,
                "mortal_item_materialization_identity_conflict",
                "The embedded receipt must bind the exact canonical item and envelope identity.",
                $"{itemId}:{materializationId}:{creationRef}:{sourceTurn}",
                $"{receiptItemId}:{receiptMaterializationId}:{receiptCreationRef}:{acceptedAtTurn}",
                null));
        }

        var seal = ReadExactNonEmptyString(receipt, "seal");
        if (seal == null || !IsLowerHexSeal(seal))
        {
            issues.Add(InvalidReceipt(
                $"{receiptContext}.seal",
                "sha256 followed by 64 lowercase hexadecimal characters",
                seal ?? Describe(receipt, "seal")));
            return;
        }

        try
        {
            var itemNode = JsonNode.Parse(item.GetRawText())?.AsObject();
            var receiptNode = JsonNode.Parse(receipt.GetRawText())?.AsObject();
            if (itemNode == null || receiptNode == null)
                return;
            receiptNode.Remove("seal");
            var expectedSeal = ComputeSeal(itemNode, receiptNode);
            if (!string.Equals(seal, expectedSeal, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    $"{receiptContext}.seal",
                    "mortal_item_materialization_receipt_seal_mismatch",
                    "The embedded Mortal item receipt seal does not match immutable evidence.",
                    expectedSeal,
                    seal,
                    null));
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            issues.Add(InvalidReceipt(
                receiptContext,
                "sealable exact receipt and materialization evidence",
                exception.Message));
        }
    }

    private static bool TryReadParentItemIds(
        JsonElement receipt,
        out List<string> parentItemIds)
    {
        parentItemIds = new List<string>();
        if (!receipt.TryGetProperty("parentItemIds", out var parents) ||
            parents.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parent in parents.EnumerateArray())
        {
            if (parent.ValueKind != JsonValueKind.String)
                return false;
            var value = parent.GetString();
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                !seen.Add(value))
            {
                return false;
            }

            parentItemIds.Add(value);
        }

        return true;
    }

    private static void ValidateExactFields(
        JsonElement value,
        HashSet<string> allowedFields,
        string context,
        List<ValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                issues.Add(Issue(
                    $"{context}.{property.Name}",
                    "mortal_item_materialization_duplicate_property",
                    "Closed Mortal item materialization objects forbid duplicate JSON properties.",
                    "each property exactly once",
                    property.Name,
                    null));
            }

            if (!allowedFields.Contains(property.Name))
            {
                issues.Add(Issue(
                    $"{context}.{property.Name}",
                    "mortal_item_materialization_unknown_field",
                    "Closed Mortal item materialization objects forbid unknown properties.",
                    string.Join(" | ", allowedFields.OrderBy(field => field, StringComparer.Ordinal)),
                    property.Name,
                    null));
            }
        }
    }

    private static void ValidateDuplicateProperties(
        JsonElement item,
        string context,
        List<ValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in item.EnumerateObject())
        {
            if (seen.Add(property.Name))
                continue;

            issues.Add(Issue(
                $"{context}.{property.Name}",
                "mortal_item_materialization_duplicate_property",
                "A durable Mortal item cannot contain duplicate JSON properties.",
                "each property exactly once",
                property.Name,
                null));
        }
    }

    private static void RequireExactString(
        JsonElement value,
        string property,
        string context,
        List<ValidationIssue> issues)
    {
        if (ReadExactNonEmptyString(value, property) != null)
            return;

        issues.Add(InvalidEnvelope(
            $"{context}.{property}",
            "non-empty untrimmed string",
            Describe(value, property)));
    }

    private static string? ReadNonEmptyString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var node) ||
            node.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = node.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? ReadExactNonEmptyString(JsonElement value, string property)
    {
        var text = ReadNonEmptyString(value, property);
        return MortalItemIdentityRules.IsExactIdentity(text)
            ? text
            : null;
    }

    private static string Describe(JsonElement value, string property) =>
        value.TryGetProperty(property, out var node) ? node.ToString() : "missing";

    private static bool IsNull(JsonElement value, string property) =>
        value.TryGetProperty(property, out var node) && node.ValueKind == JsonValueKind.Null;

    private static bool IsFalse(JsonElement value, string property) =>
        value.TryGetProperty(property, out var node) && node.ValueKind == JsonValueKind.False;

    private static bool IsTrue(JsonElement value, string property) =>
        value.TryGetProperty(property, out var node) && node.ValueKind == JsonValueKind.True;

    private static bool IsEmptyArray(JsonElement value, string property) =>
        value.TryGetProperty(property, out var node) &&
        node.ValueKind == JsonValueKind.Array &&
        node.GetArrayLength() == 0;

    private static bool HasNonEmptyArray(JsonElement value, string property) =>
        value.TryGetProperty(property, out var node) &&
        node.ValueKind == JsonValueKind.Array &&
        node.GetArrayLength() > 0;

    private static bool HasNonEmptyStringOrArray(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var node))
            return false;
        if (node.ValueKind == JsonValueKind.String)
            return !string.IsNullOrWhiteSpace(node.GetString());
        return node.ValueKind == JsonValueKind.Array && node.GetArrayLength() > 0;
    }

    private static bool HasPositiveInteger(JsonElement value, string property) =>
        value.TryGetProperty(property, out var node) &&
        node.ValueKind == JsonValueKind.Number &&
        node.TryGetInt32(out var number) &&
        number > 0;

    private static bool IsLowerHexSeal(string seal)
    {
        const string prefix = "sha256:";
        if (!seal.StartsWith(prefix, StringComparison.Ordinal) ||
            seal.Length != prefix.Length + 64)
        {
            return false;
        }

        return seal.AsSpan(prefix.Length).IndexOfAnyExcept("0123456789abcdef") < 0;
    }

    private static JsonNode RequiredClone(JsonObject value, string property) =>
        value[property]?.DeepClone() ??
        throw new InvalidOperationException($"Cannot seal a receipt without '{property}'.");

    private static JsonNode Canonicalize(JsonNode value) =>
        value switch
        {
            JsonObject obj => CanonicalizeObject(obj),
            JsonArray array => new JsonArray(
                array.Select(element => element == null ? null : Canonicalize(element)).ToArray()),
            _ => value.DeepClone()
        };

    private static JsonObject CanonicalizeObject(JsonObject value)
    {
        var result = new JsonObject();
        foreach (var pair in value.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            result[pair.Key] = pair.Value == null ? null : Canonicalize(pair.Value);
        return result;
    }

    private static ValidationIssue InvalidEnvelope(
        string path,
        string expected,
        string actual) =>
        Issue(
            path,
            "mortal_item_materialization_invalid_envelope",
            "Mortal item materialization envelope is invalid.",
            expected,
            actual,
            null);

    private static ValidationIssue InvalidReceipt(
        string path,
        string expected,
        string actual) =>
        Issue(
            path,
            "mortal_item_materialization_invalid_receipt",
            "Client-sealed Mortal item receipt is invalid.",
            expected,
            actual,
            null);

    private static ValidationIssue PhysicalIssue(
        string path,
        string expected,
        string actual) =>
        Issue(
            path,
            "mortal_item_materialization_invalid_envelope",
            "Mortal item physical evidence is invalid.",
            expected,
            actual,
            "physical");

    private static ValidationIssue Issue(
        string path,
        string code,
        string message,
        string? expected,
        string? actual,
        string? section)
    {
        return new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: null,
            section: section ?? "MortalItemMaterialization",
            expected: expected,
            actual: actual,
            repairHint: "Repair only the named GM-authored item field; never author permanent IDs, receipts, seals, or the identity index.");
    }

    private static IReadOnlyList<ValidationIssue> BindActor(
        IEnumerable<ValidationIssue> issues,
        string actor) =>
        issues.Select(issue => new ValidationIssue(
                issue.FilePath,
                issue.Severity,
                issue.Message,
                code: issue.Code,
                actor: actor,
                section: issue.Section,
                expected: issue.Expected,
                actual: issue.Actual,
                repairHint: issue.RepairHint,
                category: issue.Category,
                repairTargetFiles: issue.RepairTargetFiles))
            .ToArray();

    private static string ResolveActor(
        JsonElement item,
        MortalItemMaterializationPhase phase)
    {
        var identity = phase == MortalItemMaterializationPhase.RawPreSeal
            ? ReadExactNonEmptyString(item, "creationRef")
            : ReadExactNonEmptyString(item, "itemId");
        var kind = phase == MortalItemMaterializationPhase.RawPreSeal
            ? "new"
            : "existing";
        return identity == null
            ? "mortal_item:unknown"
            : $"mortal_item:{kind}:{identity}";
    }
}
