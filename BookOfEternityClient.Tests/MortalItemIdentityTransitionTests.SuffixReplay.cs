using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalItemIdentityTransitionTests
{
    [Fact]
    public void ValidateAgainst_SuffixReplay_AllowsTransferThenPartialConsumeFromPreviousCursor()
    {
        var (before, current, entry, npcCarrier) = ArrangeSuffixReplay(quantity: 2);
        var playerCarrier = Carrier("player_inventory", "player");
        entry["currentCarrier"] = playerCarrier.DeepClone();
        AppendSuffixTransition(entry, "transfer", npcCarrier, playerCarrier, 2, 2);
        AppendSuffixTransition(entry, "consume", playerCarrier, playerCarrier, 2, 1);

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_AllowsTransferThenFullConsumeToTerminalState()
    {
        var (before, current, entry, npcCarrier) = ArrangeSuffixReplay(quantity: 2);
        var playerCarrier = Carrier("player_inventory", "player");
        entry["state"] = "consumed";
        entry["currentCarrier"] = null;
        AppendSuffixTransition(entry, "transfer", npcCarrier, playerCarrier, 2, 2);
        AppendSuffixTransition(entry, "consume", playerCarrier, null, 2, 0);

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("wrong_carrier")]
    [InlineData("missing_carrier")]
    [InlineData("wrong_identity")]
    [InlineData("missing_identity")]
    public void ValidateAgainst_SuffixReplay_RejectsInvalidTransferWithinConsumedSuffix(string mutation)
    {
        var (before, current, entry, npcCarrier) = ArrangeSuffixReplay(quantity: 2);
        var playerCarrier = Carrier("player_inventory", "player");
        entry["currentCarrier"] = playerCarrier.DeepClone();
        var transfer = CreateSuffixTransition(
            "transfer",
            npcCarrier,
            playerCarrier,
            2,
            2);
        switch (mutation)
        {
            case "wrong_carrier":
                transfer["sourceCarrier"] = Carrier(
                    "location_storage",
                    "loc_other",
                    "storage_other");
                break;
            case "missing_carrier":
                transfer["sourceCarrier"] = null;
                break;
            case "wrong_identity":
                transfer["sourceItemIds"] = new JsonArray("itm_other");
                break;
            case "missing_identity":
                transfer["sourceItemIds"] = new JsonArray();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        MortalItemIdentityState.AppendTransition(entry, transfer);
        AppendSuffixTransition(entry, "consume", playerCarrier, playerCarrier, 2, 1);

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_transfer_transition_mismatch");
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_RejectsTransitionsThatCannotReplayInStoredOrder()
    {
        var (before, current, entry, npcCarrier) = ArrangeSuffixReplay(quantity: 2);
        var playerCarrier = Carrier("player_inventory", "player");
        entry["currentCarrier"] = playerCarrier.DeepClone();
        AppendSuffixTransition(entry, "consume", playerCarrier, playerCarrier, 2, 1);
        AppendSuffixTransition(entry, "transfer", npcCarrier, playerCarrier, 1, 1);

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_unrecorded_state_change");
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_AllowsMultipleTransfersThenPartialConsume()
    {
        var (before, current, entry, npcCarrier) = ArrangeSuffixReplay(quantity: 2);
        var storageCarrier = Carrier(
            "location_storage",
            "loc_test",
            "storage_test");
        var playerCarrier = Carrier("player_inventory", "player");
        entry["currentCarrier"] = playerCarrier.DeepClone();
        AppendSuffixTransition(entry, "transfer", npcCarrier, storageCarrier, 2, 2);
        AppendSuffixTransition(entry, "transfer", storageCarrier, playerCarrier, 2, 2);
        AppendSuffixTransition(entry, "consume", playerCarrier, playerCarrier, 2, 1);

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_AllowsSameCarrierSemanticQuantityUpdate()
    {
        var (before, current, entry, npcCarrier) = ArrangeSuffixReplay(quantity: 2);
        AppendSuffixTransition(entry, "semantic_update", npcCarrier, npcCarrier, 2, 3);

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_AllowsMergeSurvivorThenTransfer()
    {
        var scenario = ArrangeOriginMergeSuffix();
        var destination = Carrier("npc_inventory", "npc_healer");
        scenario.SurvivorEntry["currentCarrier"] = destination.DeepClone();
        AppendOriginTransition(
            scenario.SurvivorEntry,
            "transfer",
            new[] { scenario.SurvivorId },
            scenario.Carrier,
            destination,
            5,
            5);

        var issues = ValidateSuffixScenario(scenario.Before, scenario.Current);

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_AllowsMergeSurvivorThenPartialConsume()
    {
        var scenario = ArrangeOriginMergeSuffix();
        AppendOriginTransition(
            scenario.SurvivorEntry,
            "consume",
            new[] { scenario.SurvivorId },
            scenario.Carrier,
            scenario.Carrier,
            5,
            4);

        var issues = ValidateSuffixScenario(scenario.Before, scenario.Current);

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_AllowsMergeSurvivorThenFullConsume()
    {
        var scenario = ArrangeOriginMergeSuffix();
        scenario.SurvivorEntry["state"] = "consumed";
        scenario.SurvivorEntry["currentCarrier"] = null;
        AppendOriginTransition(
            scenario.SurvivorEntry,
            "consume",
            new[] { scenario.SurvivorId },
            scenario.Carrier,
            null,
            5,
            0);

        var issues = ValidateSuffixScenario(scenario.Before, scenario.Current);

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_RejectsForgedOriginOutsideMergeSources()
    {
        var scenario = ArrangeOriginMergeSuffix();
        scenario.SurvivorEntry["originMaterializationIds"] = UnionIdentityArrays(
            scenario.SurvivorEntry["originMaterializationIds"]!.AsArray(),
            new JsonArray("mat_forged"));
        scenario.SurvivorEntry["originCreationRefs"] = UnionIdentityArrays(
            scenario.SurvivorEntry["originCreationRefs"]!.AsArray(),
            new JsonArray("new_item_forged"));

        var issues = ValidateSuffixScenario(scenario.Before, scenario.Current);

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_origin_history_rewrite");
        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_origin_creation_history_rewrite");
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_RejectsMergeWithMissingSourceIdentity()
    {
        var scenario = ArrangeOriginMergeSuffix();
        var merge = scenario.SurvivorEntry["transitions"]!.AsArray()[^1]!.AsObject();
        merge["sourceItemIds"] = new JsonArray(scenario.SurvivorId, "itm_missing");

        var issues = ValidateSuffixScenario(scenario.Before, scenario.Current);

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_unrecorded_state_change");
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_RejectsMergeWhoseQuantityIsNotExactSourceSum()
    {
        var scenario = ArrangeOriginMergeSuffix();
        var merge = scenario.SurvivorEntry["transitions"]!.AsArray()[^1]!.AsObject();
        merge["quantityAfter"] = 6;

        var issues = ValidateSuffixScenario(scenario.Before, scenario.Current);

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_unrecorded_state_change");
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_RejectsMergeWithPreviouslyRetiredSource()
    {
        const string survivorId = "itm_suffix_survivor";
        const string contributorId = "itm_suffix_contributor";
        var survivor = CreateCountedItem(survivorId, 2);
        var contributor = CreateCountedItem(contributorId, 3);
        var before = MortalItemTestFixture.CreateIndex(survivor, contributor);
        var beforeContributor = FindEntry(before, contributorId);
        var carrier = beforeContributor["currentCarrier"]!.DeepClone().AsObject();
        beforeContributor["state"] = "destroyed";
        beforeContributor["currentCarrier"] = null;
        AppendOriginTransition(
            beforeContributor,
            "destroy",
            new[] { contributorId },
            carrier,
            null,
            3,
            0);

        var current = before.DeepClone().AsObject();
        var currentSurvivor = FindEntry(current, survivorId);
        currentSurvivor["originMaterializationIds"] = UnionIdentityArrays(
            currentSurvivor["originMaterializationIds"]!.AsArray(),
            beforeContributor["originMaterializationIds"]!.AsArray());
        currentSurvivor["originCreationRefs"] = UnionIdentityArrays(
            currentSurvivor["originCreationRefs"]!.AsArray(),
            beforeContributor["originCreationRefs"]!.AsArray());
        AppendOriginTransition(
            currentSurvivor,
            "merge",
            new[] { survivorId, contributorId },
            carrier,
            carrier,
            2,
            5);

        var issues = ValidateSuffixScenario(before, current);

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_unrecorded_state_change");
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_RejectsContributorMergeWithoutSurvivorMerge()
    {
        const string survivorId = "itm_suffix_survivor";
        const string contributorId = "itm_suffix_contributor";
        var survivor = CreateCountedItem(survivorId, 2);
        var contributor = CreateCountedItem(contributorId, 3);
        var before = MortalItemTestFixture.CreateIndex(survivor, contributor);
        var current = before.DeepClone().AsObject();
        var contributorEntry = FindEntry(current, contributorId);
        var carrier = contributorEntry["currentCarrier"]!.DeepClone().AsObject();
        contributorEntry["state"] = "merged";
        contributorEntry["currentCarrier"] = null;
        contributorEntry["mergedIntoItemId"] = survivorId;
        AppendOriginTransition(
            contributorEntry,
            "merge",
            new[] { survivorId, contributorId },
            carrier,
            null,
            3,
            0);

        var issues = ValidateSuffixScenario(before, current);

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_unrecorded_state_change");
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_ReportsOriginDiagnosticsWithoutAppendedTransition()
    {
        var (before, current, entry, _) = ArrangeSuffixReplay(quantity: 2);
        entry["originMaterializationIds"] = UnionIdentityArrays(
            entry["originMaterializationIds"]!.AsArray(),
            new JsonArray("mat_forged"));
        entry["originCreationRefs"] = UnionIdentityArrays(
            entry["originCreationRefs"]!.AsArray(),
            new JsonArray("new_item_forged"));

        var issues = ValidateSuffixScenario(before, current);

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_origin_history_rewrite");
        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_origin_creation_history_rewrite");
    }

    [Theory]
    [InlineData("semantic_update")]
    [InlineData("partial_consume")]
    [InlineData("split")]
    [InlineData("transfer")]
    public void ValidateAgainst_SuffixReplay_AllowsContributorPrefixBeforePairedMerge(
        string prefixKind)
    {
        var scenario = ArrangeOriginMergeSuffix(prefixKind);

        var issues = ValidateSuffixScenario(scenario.Before, scenario.Current);

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_AllowsNestedMergeSurvivorThenContributorReplay()
    {
        const string finalSurvivorId = "itm_suffix_final_survivor";
        const string nestedSurvivorId = "itm_suffix_nested_survivor";
        const string nestedContributorId = "itm_suffix_nested_contributor";
        var finalSurvivor = CreateCountedItem(finalSurvivorId, 2);
        var nestedSurvivor = CreateCountedItem(nestedSurvivorId, 3);
        var nestedContributor = CreateCountedItem(nestedContributorId, 4);
        var before = MortalItemTestFixture.CreateIndex(
            finalSurvivor,
            nestedSurvivor,
            nestedContributor);
        var current = before.DeepClone().AsObject();
        var finalEntry = FindEntry(current, finalSurvivorId);
        var nestedEntry = FindEntry(current, nestedSurvivorId);
        var contributorEntry = FindEntry(current, nestedContributorId);
        var carrier = finalEntry["currentCarrier"]!.DeepClone().AsObject();

        nestedEntry["originMaterializationIds"] = UnionIdentityArrays(
            nestedEntry["originMaterializationIds"]!.AsArray(),
            contributorEntry["originMaterializationIds"]!.AsArray());
        nestedEntry["originCreationRefs"] = UnionIdentityArrays(
            nestedEntry["originCreationRefs"]!.AsArray(),
            contributorEntry["originCreationRefs"]!.AsArray());
        AppendOriginTransition(
            nestedEntry,
            "merge",
            new[] { nestedSurvivorId, nestedContributorId },
            carrier,
            carrier,
            3,
            7);
        contributorEntry["state"] = "merged";
        contributorEntry["currentCarrier"] = null;
        contributorEntry["mergedIntoItemId"] = nestedSurvivorId;
        AppendOriginTransition(
            contributorEntry,
            "merge",
            new[] { nestedSurvivorId, nestedContributorId },
            carrier,
            null,
            4,
            0);

        finalEntry["originMaterializationIds"] = UnionIdentityArrays(
            finalEntry["originMaterializationIds"]!.AsArray(),
            nestedEntry["originMaterializationIds"]!.AsArray());
        finalEntry["originCreationRefs"] = UnionIdentityArrays(
            finalEntry["originCreationRefs"]!.AsArray(),
            nestedEntry["originCreationRefs"]!.AsArray());
        AppendOriginTransition(
            finalEntry,
            "merge",
            new[] { finalSurvivorId, nestedSurvivorId },
            carrier,
            carrier,
            2,
            9);
        nestedEntry["state"] = "merged";
        nestedEntry["currentCarrier"] = null;
        nestedEntry["mergedIntoItemId"] = finalSurvivorId;
        AppendOriginTransition(
            nestedEntry,
            "merge",
            new[] { finalSurvivorId, nestedSurvivorId },
            carrier,
            null,
            7,
            0);

        var issues = ValidateSuffixScenario(before, current);

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateAgainst_SuffixReplay_RejectsCyclicMergeGraphWithoutRecursionFailure()
    {
        var items = new[]
        {
            CreateCountedItem("itm_suffix_cycle_a", 1),
            CreateCountedItem("itm_suffix_cycle_b", 1),
            CreateCountedItem("itm_suffix_cycle_c", 1)
        };
        var before = MortalItemTestFixture.CreateIndex(items);
        var current = before.DeepClone().AsObject();
        var entryA = FindEntry(current, "itm_suffix_cycle_a");
        var entryB = FindEntry(current, "itm_suffix_cycle_b");
        var entryC = FindEntry(current, "itm_suffix_cycle_c");
        var carrier = entryA["currentCarrier"]!.DeepClone().AsObject();

        AppendCyclicMergePair(
            entryA,
            "itm_suffix_cycle_a",
            "itm_suffix_cycle_c",
            carrier,
            survivor: true);
        AppendCyclicMergePair(
            entryA,
            "itm_suffix_cycle_a",
            "itm_suffix_cycle_b",
            carrier,
            survivor: false);
        entryA["state"] = "merged";
        entryA["currentCarrier"] = null;
        entryA["mergedIntoItemId"] = "itm_suffix_cycle_b";

        AppendCyclicMergePair(
            entryB,
            "itm_suffix_cycle_a",
            "itm_suffix_cycle_b",
            carrier,
            survivor: true);
        AppendCyclicMergePair(
            entryB,
            "itm_suffix_cycle_b",
            "itm_suffix_cycle_c",
            carrier,
            survivor: false);
        entryB["state"] = "merged";
        entryB["currentCarrier"] = null;
        entryB["mergedIntoItemId"] = "itm_suffix_cycle_c";

        AppendCyclicMergePair(
            entryC,
            "itm_suffix_cycle_b",
            "itm_suffix_cycle_c",
            carrier,
            survivor: true);
        AppendCyclicMergePair(
            entryC,
            "itm_suffix_cycle_a",
            "itm_suffix_cycle_c",
            carrier,
            survivor: false);
        entryC["state"] = "merged";
        entryC["currentCarrier"] = null;
        entryC["mergedIntoItemId"] = "itm_suffix_cycle_a";

        var issues = ValidateSuffixScenario(before, current);

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_unrecorded_state_change");
    }

    private static (
        JsonObject Before,
        JsonObject Current,
        JsonObject Entry,
        JsonObject InitialCarrier) ArrangeSuffixReplay(int quantity)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        item["count"] = quantity;
        var before = MortalItemTestFixture.CreateIndexForCarrier(
            item,
            "npc_inventory",
            "npc_healer");
        var current = before.DeepClone().AsObject();
        var entry = current["entries"]![0]!.AsObject();
        var initialCarrier = entry["currentCarrier"]!.DeepClone().AsObject();
        return (before, current, entry, initialCarrier);
    }

    private static void AppendSuffixTransition(
        JsonObject entry,
        string kind,
        JsonObject? sourceCarrier,
        JsonObject? destinationCarrier,
        int quantityBefore,
        int quantityAfter) =>
        MortalItemIdentityState.AppendTransition(
            entry,
            CreateSuffixTransition(
                kind,
                sourceCarrier,
                destinationCarrier,
                quantityBefore,
                quantityAfter));

    private static JsonObject CreateSuffixTransition(
        string kind,
        JsonObject? sourceCarrier,
        JsonObject? destinationCarrier,
        int quantityBefore,
        int quantityAfter) =>
        MortalItemIdentityState.CreateTransition(
            kind,
            turn: 43,
            new[] { MortalItemTestFixture.ItemId },
            sourceCarrier,
            destinationCarrier,
            quantityBefore,
            quantityAfter,
            authorityKind: "wound_treatment",
            authorityId: "treatment_43");

    private static OriginMergeSuffixScenario ArrangeOriginMergeSuffix(
        string? contributorPrefixKind = null)
    {
        const string survivorId = "itm_suffix_survivor";
        const string contributorId = "itm_suffix_contributor";
        var survivor = CreateCountedItem(survivorId, 2);
        var contributor = CreateCountedItem(contributorId, 3);
        var before = MortalItemTestFixture.CreateIndex(survivor, contributor);
        var current = before.DeepClone().AsObject();
        var survivorEntry = FindEntry(current, survivorId);
        var contributorEntry = FindEntry(current, contributorId);
        var carrier = survivorEntry["currentCarrier"]!.DeepClone().AsObject();
        var contributorQuantity = 3;
        switch (contributorPrefixKind)
        {
            case null:
                break;
            case "semantic_update":
                AppendOriginTransition(
                    contributorEntry,
                    "semantic_update",
                    new[] { contributorId },
                    carrier,
                    carrier,
                    contributorQuantity,
                    4);
                contributorQuantity = 4;
                break;
            case "partial_consume":
                AppendOriginTransition(
                    contributorEntry,
                    "consume",
                    new[] { contributorId },
                    carrier,
                    carrier,
                    contributorQuantity,
                    2);
                contributorQuantity = 2;
                break;
            case "split":
                AppendOriginTransition(
                    contributorEntry,
                    "split",
                    new[] { contributorId },
                    carrier,
                    carrier,
                    contributorQuantity,
                    2);
                contributorQuantity = 2;
                break;
            case "transfer":
                var destination = Carrier(
                    "location_storage",
                    "loc_suffix_merge",
                    "storage_suffix_merge");
                survivorEntry["currentCarrier"] = destination.DeepClone();
                contributorEntry["currentCarrier"] = destination.DeepClone();
                AppendOriginTransition(
                    survivorEntry,
                    "transfer",
                    new[] { survivorId },
                    carrier,
                    destination,
                    2,
                    2);
                AppendOriginTransition(
                    contributorEntry,
                    "transfer",
                    new[] { contributorId },
                    carrier,
                    destination,
                    contributorQuantity,
                    contributorQuantity);
                carrier = destination;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(contributorPrefixKind),
                    contributorPrefixKind,
                    null);
        }
        var sourceItemIds = new[] { survivorId, contributorId };

        survivorEntry["originMaterializationIds"] = UnionIdentityArrays(
            survivorEntry["originMaterializationIds"]!.AsArray(),
            contributorEntry["originMaterializationIds"]!.AsArray());
        survivorEntry["originCreationRefs"] = UnionIdentityArrays(
            survivorEntry["originCreationRefs"]!.AsArray(),
            contributorEntry["originCreationRefs"]!.AsArray());
        AppendOriginTransition(
            survivorEntry,
            "merge",
            sourceItemIds,
            carrier,
            carrier,
            2,
            2 + contributorQuantity);

        contributorEntry["state"] = "merged";
        contributorEntry["currentCarrier"] = null;
        contributorEntry["mergedIntoItemId"] = survivorId;
        AppendOriginTransition(
            contributorEntry,
            "merge",
            sourceItemIds,
            carrier,
            null,
            contributorQuantity,
            0);

        return new OriginMergeSuffixScenario(
            before,
            current,
            survivorEntry,
            carrier,
            survivorId);
    }

    private static JsonObject CreateCountedItem(string itemId, int quantity)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot(itemId);
        item["count"] = quantity;
        return item;
    }

    private static JsonObject FindEntry(JsonObject root, string itemId) =>
        root["entries"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(entry => string.Equals(
                entry["itemId"]!.GetValue<string>(),
                itemId,
                StringComparison.Ordinal));

    private static JsonArray UnionIdentityArrays(params JsonArray[] sources) =>
        new(sources
            .SelectMany(source => source)
            .Select(node => node!.GetValue<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => (JsonNode?)JsonValue.Create(value))
            .ToArray());

    private static void AppendOriginTransition(
        JsonObject entry,
        string kind,
        IReadOnlyList<string> sourceItemIds,
        JsonObject sourceCarrier,
        JsonObject? destinationCarrier,
        int quantityBefore,
        int quantityAfter) =>
        MortalItemIdentityState.AppendTransition(
            entry,
            MortalItemIdentityState.CreateTransition(
                kind,
                turn: 43,
                sourceItemIds,
                sourceCarrier,
                destinationCarrier,
                quantityBefore,
                quantityAfter,
                authorityKind: "wound_treatment",
                authorityId: "treatment_43"));

    private static void AppendCyclicMergePair(
        JsonObject entry,
        string firstItemId,
        string secondItemId,
        JsonObject carrier,
        bool survivor) =>
        AppendOriginTransition(
            entry,
            "merge",
            new[] { firstItemId, secondItemId },
            carrier,
            survivor ? carrier : null,
            survivor ? 1 : 2,
            survivor ? 2 : 0);

    private static IReadOnlyList<ValidationIssue> ValidateSuffixScenario(
        JsonObject before,
        JsonObject current) =>
        MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

    private sealed record OriginMergeSuffixScenario(
        JsonObject Before,
        JsonObject Current,
        JsonObject SurvivorEntry,
        JsonObject Carrier,
        string SurvivorId);
}
