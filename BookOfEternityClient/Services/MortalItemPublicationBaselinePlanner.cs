using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class MortalTreatmentItemCommandEnvelope
{
    private readonly JsonArray? _updateInventory;
    private readonly JsonArray? _moveInventoryItems;
    private readonly JsonArray? _removeInventoryItems;
    private readonly JsonArray? _npcInventoryAdds;
    private readonly JsonArray? _npcInventoryUpdates;
    private readonly JsonArray? _npcInventoryRemovals;
    private readonly JsonArray? _npcEquipmentChanges;

    internal MortalTreatmentItemCommandEnvelope(
        JsonArray? updateInventory,
        JsonArray? moveInventoryItems,
        JsonArray? removeInventoryItems,
        JsonArray? npcInventoryAdds,
        JsonArray? npcInventoryUpdates,
        JsonArray? npcInventoryRemovals,
        JsonArray? npcEquipmentChanges)
    {
        _updateInventory = Clone(updateInventory);
        _moveInventoryItems = Clone(moveInventoryItems);
        _removeInventoryItems = Clone(removeInventoryItems);
        _npcInventoryAdds = Clone(npcInventoryAdds);
        _npcInventoryUpdates = Clone(npcInventoryUpdates);
        _npcInventoryRemovals = Clone(npcInventoryRemovals);
        _npcEquipmentChanges = Clone(npcEquipmentChanges);
        Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.item_command_envelope",
            "1",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(ToRoot())
        });
    }

    internal JsonArray? UpdateInventory => Clone(_updateInventory);
    internal JsonArray? MoveInventoryItems => Clone(_moveInventoryItems);
    internal JsonArray? RemoveInventoryItems => Clone(_removeInventoryItems);
    internal JsonArray? NPCInventoryAdds => Clone(_npcInventoryAdds);
    internal JsonArray? NPCInventoryUpdates => Clone(_npcInventoryUpdates);
    internal JsonArray? NPCInventoryRemovals => Clone(_npcInventoryRemovals);
    internal JsonArray? NPCEquipmentChanges => Clone(_npcEquipmentChanges);
    internal string Fingerprint { get; }
    internal bool IsEmpty =>
        _updateInventory is null &&
        _moveInventoryItems is null &&
        _removeInventoryItems is null &&
        _npcInventoryAdds is null &&
        _npcInventoryUpdates is null &&
        _npcInventoryRemovals is null &&
        _npcEquipmentChanges is null;

    internal MortalTreatmentItemCommandEnvelope Clone() => new(
        _updateInventory,
        _moveInventoryItems,
        _removeInventoryItems,
        _npcInventoryAdds,
        _npcInventoryUpdates,
        _npcInventoryRemovals,
        _npcEquipmentChanges);

    private JsonObject ToRoot()
    {
        var root = new JsonObject();
        Add(root, "UpdateInventory", _updateInventory);
        Add(root, "moveInventoryItems", _moveInventoryItems);
        Add(root, "removeInventoryItems", _removeInventoryItems);
        Add(root, "NPCInventoryAdds", _npcInventoryAdds);
        Add(root, "NPCInventoryUpdates", _npcInventoryUpdates);
        Add(root, "NPCInventoryRemovals", _npcInventoryRemovals);
        Add(root, "NPCEquipmentChanges", _npcEquipmentChanges);
        return root;
    }

    private static void Add(JsonObject root, string property, JsonArray? value)
    {
        if (value != null)
            root[property] = value.DeepClone();
    }

    private static JsonArray? Clone(JsonArray? value) => value?.DeepClone().AsArray();
}

internal enum MortalItemNpcTradeTailDisposition
{
    Apply,
    SkipUntouchedTreatmentContinuation
}

internal sealed record MortalItemPublicationBaselineInput(
    MortalItemCanonicalProjectionResult ItemPhase,
    MortalTreatmentItemCommandEnvelope Envelope,
    NpcCoreChangesContract.Authority NpcCoreAuthority,
    CanonicalBeforeImage NpcTradePending,
    CanonicalBeforeImage TrainingPending,
    MortalItemNpcTradeTailDisposition NpcTradeDisposition,
    IReadOnlyDictionary<string, JsonNode?> BackupRoots);

internal sealed record MortalItemPublicationBaselineResult(
    IReadOnlyDictionary<string, JsonNode?> FinalCarrierRoots,
    JsonObject IdentityIndexAfterImage,
    IReadOnlyList<string> AppliedTransformIds,
    IReadOnlyList<ValidationIssue> Issues,
    string Fingerprint);

internal static class MortalItemPublicationBaselinePlanner
{
    internal static IReadOnlyList<string> TransformRegistry { get; } =
        Array.AsReadOnly(new[]
        {
            "quest_history:v1",
            "npc_core:v1",
            "npc_trade:v1",
            "inventory_items_journal:v1",
            "item_bonds:v1",
            "item_text_updates:v1",
            "npc_item_journals:v1"
        });

    internal static MortalItemPublicationBaselineResult Project(
        MortalItemPublicationBaselineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var issues = Validate(input);
        if (issues.Count > 0)
            return Invalid(input, issues);

        var roots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
            input.ItemPhase.ItemPhaseAfterImages);
        var appliedTransformIds = new List<string>();
        foreach (var registration in TransformRegistry)
        {
            var applied = ApplyRegisteredTransform(registration, roots, input);
            roots = applied.Roots;
            appliedTransformIds.Add(applied.AppliedTransformId);
        }

        var index = roots[MortalItemIdentityState.StatePath]!.DeepClone().AsObject();
        var finalRoots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(roots);
        return new MortalItemPublicationBaselineResult(
            finalRoots,
            index,
            appliedTransformIds.ToArray(),
            Array.Empty<ValidationIssue>(),
            Fingerprint(input, finalRoots, appliedTransformIds, Array.Empty<ValidationIssue>()));
    }

    private static AppliedTransform ApplyRegisteredTransform(
        string registration,
        IReadOnlyDictionary<string, JsonNode?> source,
        MortalItemPublicationBaselineInput input)
    {
        var roots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(source);
        string appliedId;
        switch (registration)
        {
            case "quest_history:v1":
                Apply(roots, input, "game_state/quests/quest_history.json",
                    MortalItemPublicationTailTransforms.QuestHistory);
                appliedId = registration;
                break;
            case "npc_core:v1":
            {
                const string path = NpcCoreChangesContract.NpcCorePath;
                roots[path] = MortalItemPublicationTailTransforms.NpcCore(
                    roots[path],
                    input.BackupRoots[path],
                    input.NpcCoreAuthority,
                    input.NpcTradePending,
                    input.TrainingPending);
                appliedId = registration;
                break;
            }
            case "npc_trade:v1":
            {
                const string path = NpcCoreChangesContract.NpcCorePath;
                roots[path] = MortalItemPublicationTailTransforms.NpcTrade(
                    roots[path],
                    input.BackupRoots[path],
                    input.NpcTradeDisposition);
                appliedId = input.NpcTradeDisposition ==
                            MortalItemNpcTradeTailDisposition.Apply
                    ? "npc_trade:apply:v1"
                    : "npc_trade:skip_untouched_treatment_continuation:v1";
                break;
            }
            case "inventory_items_journal:v1":
            {
                const string path = InventoryEquipmentService.ItemsPath;
                roots[path] = MortalItemPublicationTailTransforms.InventoryItemsJournal(
                    roots[path]);
                appliedId = registration;
                break;
            }
            case "item_bonds:v1":
                Apply(roots, input, "game_state/inventory/item_bonds.json",
                    MortalItemPublicationTailTransforms.ItemBonds);
                appliedId = registration;
                break;
            case "item_text_updates:v1":
                Apply(roots, input, "game_state/inventory/item_text_updates.json",
                    MortalItemPublicationTailTransforms.ItemTexts);
                appliedId = registration;
                break;
            case "npc_item_journals:v1":
                Apply(roots, input, "game_state/npcs/item_journals.json",
                    MortalItemPublicationTailTransforms.NpcItemJournals);
                appliedId = registration;
                break;
            default:
                throw new InvalidOperationException(
                    $"Unregistered Mortal item publication transform '{registration}'.");
        }
        return new AppliedTransform(roots, appliedId);
    }

    private static void Apply(
        IDictionary<string, JsonNode?> roots,
        MortalItemPublicationBaselineInput input,
        string path,
        Func<JsonNode?, JsonNode?, JsonNode?> transform) =>
        roots[path] = transform(roots[path], input.BackupRoots[path]);

    private static List<ValidationIssue> Validate(MortalItemPublicationBaselineInput input)
    {
        var issues = new List<ValidationIssue>();
        if (input.ItemPhase.Issues.Count > 0)
            issues.AddRange(input.ItemPhase.Issues);
        if (!ExactPaths(input.ItemPhase.ItemPhaseAfterImages) ||
            !ExactPaths(input.BackupRoots))
        {
            issues.Add(Issue(
                "mortal_item_publication_baseline_root_set_mismatch",
                "the exact 13-root projection and backup maps",
                "missing or extra root path"));
        }
        if (input.ItemPhase.ItemPhaseAfterImages.GetValueOrDefault(
                MortalItemIdentityState.StatePath) is not JsonObject index ||
            !JsonNode.DeepEquals(index, input.ItemPhase.IdentityIndexAfterImage))
        {
            issues.Add(Issue(
                "mortal_item_publication_baseline_identity_mismatch",
                "the exact item-phase identity after-image",
                "changed"));
        }
        return issues;
    }

    private static bool ExactPaths(IReadOnlyDictionary<string, JsonNode?> roots) =>
        roots.Count == MortalItemCanonicalProjectionPlanner.ProjectionRootPaths.Count &&
        roots.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(
            MortalItemCanonicalProjectionPlanner.ProjectionRootPaths);

    private static MortalItemPublicationBaselineResult Invalid(
        MortalItemPublicationBaselineInput input,
        IReadOnlyList<ValidationIssue> issues) => new(
        new Dictionary<string, JsonNode?>(StringComparer.Ordinal),
        MortalItemIdentityState.CreateEmptyRoot(),
        Array.Empty<string>(),
        issues.ToArray(),
        Fingerprint(input, null, Array.Empty<string>(), issues));

    private static ValidationIssue Issue(string code, string expected, string actual) => new(
        MortalItemIdentityState.StatePath,
        IssueSeverity.Error,
        "Mortal item final pre-publication baseline projection failed.",
        code: code,
        actor: "mortal_item:publication_baseline",
        section: "MortalItemMaterialization",
        expected: expected,
        actual: actual,
        repairHint: "Reject publication and rebuild the accepted projection authority.",
        repairTargetFiles: MortalItemCanonicalProjectionPlanner.ProjectionRootPaths.ToArray());

    private static string Fingerprint(
        MortalItemPublicationBaselineInput input,
        IReadOnlyDictionary<string, JsonNode?>? roots,
        IReadOnlyList<string> applied,
        IReadOnlyList<ValidationIssue> issues)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_item.publication_baseline",
            "1",
            input.ItemPhase.Fingerprint,
            input.Envelope.Fingerprint,
            input.NpcTradePending.Fingerprint,
            input.TrainingPending.Fingerprint,
            input.NpcTradeDisposition.ToString(),
            AuthorityFingerprint(input.NpcCoreAuthority)
        };
        foreach (var pair in input.BackupRoots.OrderBy(static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add("backup");
            fields.Add(pair.Key);
            fields.Add(pair.Value == null ? "missing" :
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
        foreach (var id in applied)
            fields.Add(id);
        foreach (var pair in roots?.OrderBy(static pair => pair.Key, StringComparer.Ordinal) ??
                             Enumerable.Empty<KeyValuePair<string, JsonNode?>>())
        {
            fields.Add("final");
            fields.Add(pair.Key);
            fields.Add(pair.Value == null ? "missing" :
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
        foreach (var issue in issues)
            fields.Add(issue.Code);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static string AuthorityFingerprint(NpcCoreChangesContract.Authority authority)
    {
        var root = new JsonObject
        {
            ["knownPermanentLocationIds"] = new JsonArray(authority.KnownPermanentLocationIds
                .OrderBy(static value => value, StringComparer.Ordinal)
                .Select(static value => (JsonNode?)value).ToArray()),
            ["sameTurnLocationInitialIds"] = new JsonArray(authority.SameTurnLocationInitialIds
                .OrderBy(static value => value, StringComparer.Ordinal)
                .Select(static value => (JsonNode?)value).ToArray()),
            ["factionNamesById"] = new JsonObject(authority.FactionNamesById
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => new KeyValuePair<string, JsonNode?>(
                    pair.Key, pair.Value))),
            ["worldCharacteristicKeys"] = new JsonArray(authority.WorldCharacteristicKeys
                .OrderBy(static value => value, StringComparer.Ordinal)
                .Select(static value => (JsonNode?)value).ToArray())
        };
        return WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_item.npc_core_authority",
            "1",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(root)
        });
    }

    private sealed record AppliedTransform(
        Dictionary<string, JsonNode?> Roots,
        string AppliedTransformId);
}
