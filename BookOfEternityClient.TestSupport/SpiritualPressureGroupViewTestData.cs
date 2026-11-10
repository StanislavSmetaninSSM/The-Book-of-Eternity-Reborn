using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Fixes the two canonical wound-ID positions before any graph, ledger or signature is authored.</summary>
internal sealed record SpiritualPressureParentRanks
{
    /// <summary>The severity of the lexically first parent, independent of its severity order.</summary>
    internal int First { get; }
    /// <summary>The severity of the lexically second parent, independent of its severity order.</summary>
    internal int Second { get; }

    /// <summary>Accepts only complete rank-III/IV fixture configurations; malformed-domain tests mutate detached DATA explicitly.</summary>
    /// <param name="first">Severity authored for the first exact wound ID.</param>
    /// <param name="second">Severity authored for the second exact wound ID.</param>
    internal SpiritualPressureParentRanks(int first = 3, int second = 3)
    {
        if (first is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(first));
        if (second is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(second));
        First = first;
        Second = second;
    }
}

/// <summary>Builds minimal intrinsic pair DATA without world setup, receipts, historical execution or live authority.</summary>
internal static class SpiritualPressureGroupViewTestData
{
    internal const string FirstWoundId = "wound_pressure_view_A";
    internal const string SecondWoundId = "wound_pressure_view_B";

    /// <summary>Authors two independent III/IV graphs with legal shared-root multi-profile siblings and complete ledger joins.</summary>
    /// <param name="secondOwnerId">Intrinsic owner of the second graph, authored consistently before any reader is called.</param>
    /// <param name="ranks">Complete severities for the two fixed wound-ID positions; omission preserves the original III+III bytes.</param>
    /// <returns>Fresh mutable DATA owned only by this invocation.</returns>
    internal static SpiritualPressureGroupViewData CreatePair(string secondOwnerId = "player_soul",
        SpiritualPressureParentRanks? ranks = null)
    {
        ranks ??= new();
        var profiles = new JsonArray();
        var identities = new List<JsonObject>();
        var transitions = new List<JsonObject>();
        for (var index = 0; index < 2; index++)
        {
            var label = index == 0 ? "A" : "B";
            var woundId = index == 0 ? FirstWoundId : SecondWoundId;
            var ownerId = index == 0 ? "player_soul" : secondOwnerId;
            var effectId = "effect_pressure_view_" + label;
            var definitionKey = "definition_pressure_view_" + label;
            var turn = 42 + 2 * index;
            var eventRef = $"turn_{turn}:pressure_view_{label}";
            var transitionId = "wound_transition_pressure_view_" + label;
            var rank = index == 0 ? ranks.First : ranks.Second;
            var rankName = rank == 4 ? "IV" : "III";
            var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
                "spiritual_strain_burden", "player", "chaos_sea", woundId, definitionKey, "pressure");
            definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                "spiritual_roll_hindrance", "component_002", "counter"));
            definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                "spiritual_action_cost_burden", "component_003", "guard", JsonValue.Create(rank == 4 ? 3 : 2)));
            definition["triggers"]![0]!["componentIds"] = new JsonArray("component_001", "component_002", "component_003");
            var parent = WoundContractTestData.CreateSpiritualActiveWound(woundId, ownerId: ownerId,
                carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
            parent["origin"]!["eventRef"] = eventRef;
            parent["origin"]!["sourceKind"] = "spiritual_standard_art";
            parent["origin"]!["sourceId"] = "guardian:guardian_view:pressure";
            parent["origin"]!["createdAtTurn"] = turn;
            parent["origin"]!["opportunityId"] = "opportunity_pressure_view_" + label;
            parent["severity"] = new JsonObject
            {
                ["value"] = rankName, ["rank"] = rank, ["maximumAtCreation"] = rankName, ["lastChangeEventRef"] = eventRef
            };
            parent["recovery"]!["currentStepThreshold"] = rank == 4 ? 8 : 6;
            parent["recovery"]!["recoveryAnchor"] = null;
            parent["recovery"]!["deteriorationAnchor"] = null;
            parent["lastTransition"] = new JsonObject
            {
                ["transitionId"] = transitionId, ["ordinal"] = 1, ["turn"] = turn, ["kind"] = "create"
            };
            parent["consequences"] = new JsonObject
            {
                ["slotBudget"] = rank, ["slotsUsed"] = rank,
                ["ownedEffectSources"] = new JsonObject
                {
                    ["definitions"] = new JsonArray(definition),
                    ["rootBindings"] = new JsonArray(WoundContractTestData.CreateRootBinding(effectId, definitionKey))
                },
                ["entries"] = new JsonArray(
                    Entry(1, "spiritual_strain_burden", effectId, "Провал давления усиливает надлом."),
                    Entry(2, "spiritual_roll_hindrance", effectId, "Контрприём душе даётся труднее."),
                    Entry(3, "spiritual_action_cost_burden", effectId, "Защита требует больше духовных сил."))
            };
            if (rank == 4)
            {
                definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                    "spiritual_action_cost_burden", "component_004", "counter", JsonValue.Create(3)));
                definition["triggers"]![0]!["componentIds"]!.AsArray().Add("component_004");
                parent["consequences"]!["entries"]!.AsArray().Add(
                    Entry(4, "spiritual_action_cost_burden", effectId, "Контрприём требует больше духовных сил."));
            }
            var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "fixture.pressureGroupView.parent");
            if (!parsed.IsValid)
                throw new InvalidDataException("Pressure group view DATA preparation failed:\n" + string.Join("\n", parsed.Issues));
            var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound!);
            identities.Add(WoundContractTestData.CreateIdentityEntry(woundId, "chaos_sea", "player_soul", ownerId,
                WoundCarrierCatalog.AfterlifeProfilesPath, "spiritual", createdAtTurn: turn,
                createdEventRef: eventRef, semanticFingerprint: fingerprint));
            var transition = WoundContractTestData.CreateTransition(ordinal: index + 1);
            transition["transitionId"] = transitionId;
            transition["woundId"] = woundId;
            transition["turn"] = turn;
            transition["eventRef"] = eventRef;
            transition["operationKey"] = "operation_pressure_view_" + label;
            transition["attemptId"] = null;
            transition["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(woundId);
            transition["afterFingerprint"] = fingerprint;
            transition["sourceFingerprint"] = "sha256:" + new string('b', 64);
            transitions.Add(transition);
            var profile = profiles.OfType<JsonObject>().SingleOrDefault(row => row["actorId"]!.GetValue<string>() == ownerId);
            if (profile is null)
            {
                profile = new JsonObject
                {
                    ["actorType"] = "player_soul", ["actorId"] = ownerId, ["realm"] = "Chaos Sea",
                    ["displayName"] = "Духовный участник", ["activeWounds"] = new JsonArray()
                };
                profiles.Add(profile);
            }
            profile["activeWounds"]!.AsArray().Add(parent);
        }
        return new(new JsonObject { ["schemaVersion"] = 1, ["profiles"] = profiles },
            WoundContractTestData.CreateIdentityIndex(identities.ToArray()),
            WoundContractTestData.CreateHistory(transitions.ToArray()));
    }

    /// <summary>Authors one readable sibling slot tied to the exact shared root.</summary>
    private static JsonObject Entry(int slot, string profile, string effectId, string summary) => new()
    {
        ["slot"] = slot, ["profileKey"] = profile, ["effectId"] = effectId, ["readableSummary"] = summary
    };
}

/// <summary>Independent current or signed-shaped intrinsic DATA; no member authenticates a historical or current owner.</summary>
internal sealed record SpiritualPressureGroupViewData(JsonObject Profiles, JsonObject Identity, JsonObject History)
{
    /// <summary>Supplies the existing typed carrier reader with this invocation's one profile snapshot.</summary>
    internal WoundCarrierCatalogInput Carriers => new(null, null, null, null, Profiles);

    /// <summary>Returns a fresh exact requested order, without changing which parents a live consumer may select.</summary>
    internal IReadOnlyList<string> WoundIds => new[]
    {
        SpiritualPressureGroupViewTestData.FirstWoundId, SpiritualPressureGroupViewTestData.SecondWoundId
    };

    /// <summary>Finds an authored parent by exact identity rather than relying on carrier order.</summary>
    internal JsonObject Parent(string woundId) => Profiles["profiles"]!.AsArray().OfType<JsonObject>()
        .SelectMany(profile => profile["activeWounds"]!.AsArray().OfType<JsonObject>())
        .Single(parent => parent["woundId"]!.GetValue<string>() == woundId);

    /// <summary>Separates all mutable inputs for another test basis without issuing signed provenance.</summary>
    internal SpiritualPressureGroupViewData Copy() => new(Profiles.DeepClone().AsObject(),
        Identity.DeepClone().AsObject(), History.DeepClone().AsObject());
}
