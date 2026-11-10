using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Session identity shared only by the successive turns in this independently owned Fact.</summary>
    private const string NativeGuardianSession = "guardian_native_birth";

    /// <summary>Executes one guardian III birth, safe new-conflict start, signed current v5 use and common publication.</summary>
    /// <returns>Completion after the full journey and all owned cleanup assertions.</returns>
    [Fact]
    public Task SpiritualGuardianNativeBirth_PublishedParentSurvivesCleanStartAndSignedCurrentUse()
        => ExecuteNativeGuardianJourneyAsync(3);

    /// <summary>Executes one genuine guardian IV birth with four consequences, recovery and signed current-parent use.</summary>
    /// <returns>Completion after independent IV publication, ancestry and owned cleanup assertions.</returns>
    [Fact]
    public Task SpiritualGuardianNativeIVBirth_PublishedParentSurvivesCleanStartAndSignedCurrentUse()
        => ExecuteNativeGuardianJourneyAsync(4);

    /// <summary>Shares journey logic, never mutable state, between the two closed native birth cases.</summary>
    /// <param name="birthRank">Independent expected rank fixed before any proposal, source or signature: exactly III or IV.</param>
    /// <returns>Completion of this independently owned fixture.</returns>
    private async Task ExecuteNativeGuardianJourneyAsync(int birthRank)
    {
        Assert.True(birthRank is 3 or 4);
        var proposal = CreateNativeGuardianProposal(birthRank); // Authored before any snapshot or issued wound identity.
        ResourceMaterializationTestContext? context = null;
        await ExecutePressureGroupFixtureStagesAsync(new(
            Work: async () =>
            {
                var started = Stopwatch.GetTimestamp();
                context = await CreateCompleteConflictFrameContextAsync(
                    seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, playerCurrent: 6m, spiritFocusTier: 3,
                    initializeContext: allocated => { context = allocated; return Task.CompletedTask; },
                    captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(turn: 44,
                        currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5],
                        sessionId: NativeGuardianSession, requestId: "guardian_native_birth_44"));
                WritePressureOwnerTiming("guardian_native_preparation", NativeGuardianSession, started);
                var definitions = ResourceDefinitionCatalog.ParseCanonical(
                    await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false);
                RequireGuardianJourneyIssues(NativeGuardianSession, "definitions", definitions.Issues);
                var catalog = Assert.IsType<ResourceDefinitionCatalog>(definitions.Catalog);
                var seed = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!.DeepClone().AsObject();
                var retiredOwner = seed["resourceOwnerBindings"]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();
                var initialResources = ReadPressurePairResourceData(catalog,
                    await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
                    await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath));
                var birthExchange = SpiritualGuardianAttributionTestData.CreateExchange("guardian_native_birth_exchange_44", 44,
                    "clear", birthRank == 4 ? "broken" : "overwhelmed", 15, 5);
                SetNativeGuardianCosts(birthExchange, 6m, 6m, 6m);
                await AuthorNativeGuardianExchangeAsync(context, birthExchange, terminal: true);
                var birth = await CompleteNativeGuardianExchangeAsync(context, birthExchange, proposal, parent: null, birthRank);
                var born = birth.PublishedParent;
                Assert.Equal(birth.InsertedWoundId, born.Wound.WoundId);
                Assert.Equal((44, birthRank, 0L), (born.Wound.Origin.CreatedAtTurn, born.Wound.Severity.Rank,
                    born.Wound.Recovery.CurrentStepProgress));
                Assert.Single(born.History.Transitions);
                Assert.Single(born.Receipt["instances"]!.AsArray()); Assert.Single(born.Receipt["closures"]!.AsArray());
                Assert.Single(born.Receipt["sources"]!.AsArray()); Assert.Single(born.Receipt["decisions"]!.AsArray());
                Assert.True(JsonNode.DeepEquals(birth.Witness, born.Receipt["sources"]![0]!["witness"]));
                await ExecuteGuardianLeaseStagesAsync(context, async lease =>
                {
                    var resources = await ReadPressurePairResourcesAsync(context, lease, catalog);
                    Assert.Equal((3m, 10m), PressurePairPlayerBalance(resources.State));
                    AssertNativeGuardianResourceSpends(initialResources, resources, 44, 6m, 6m, retiredOwner, terminal: true);
                    AssertPressurePairRetiredOwners(resources, [retiredOwner]);
                    Assert.Null((await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]);
                });
                var currentOwner = await PublishNativeGuardianStartAsync(context, catalog, seed, born, retiredOwner, birthRank);
                var recovered = await ReadNativeGuardianParentAsync(context, birthRank);
                await SignNativeGuardianStateAsync(context, 46, "guardian_native_current_46", [12, 4]);
                var beforeUse = ReadPressurePairResourceData(catalog,
                    await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
                    await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath));
                var ordinary = SpiritualGuardianAttributionTestData.CreateExchange("guardian_native_current_exchange_46", 46,
                    "strained", "fractured", declaration: true);
                SetNativeGuardianCosts(ordinary, 3m, 6m, 6m);
                await AuthorNativeGuardianExchangeAsync(context, ordinary, terminal: false);
                var used = await CompleteNativeGuardianExchangeAsync(context, ordinary, proposal: null, recovered, birthRank);
                Assert.Null(used.InsertedWoundId);
                var final = used.PublishedParent;
                Assert.Equal(WoundMaterializationContract.SerializeCanonical(recovered.Wound),
                    WoundMaterializationContract.SerializeCanonical(final.Wound));
                Assert.True(JsonNode.DeepEquals(recovered.HistoryRoot, final.HistoryRoot));
                Assert.True(JsonNode.DeepEquals(recovered.Identity, final.Identity));
                AssertNativeGuardianReceiptAppend(recovered.Receipt, final.Receipt, used.Witness);
                await ExecuteGuardianLeaseStagesAsync(context, async lease =>
                {
                    var resources = await ReadPressurePairResourcesAsync(context, lease, catalog);
                    Assert.Equal((0m, 10m), PressurePairPlayerBalance(resources.State));
                    AssertNativeGuardianResourceSpends(beforeUse, resources, 46, 3m, 6m, currentOwner, terminal: false);
                    AssertPressurePairRetiredOwners(resources, [retiredOwner]);
                    Assert.True(resources.State.TryResolveExact(new("chaos_sea", ResourceOwnerKind.AfterlifeConflictSide,
                        currentOwner, "spiritual_action_points"), out var side));
                    Assert.Equal((3m, 6m), (side!.Current, side.Maximum));
                    Assert.False(resources.History.IsTerminal(side.Coordinate));
                });
                await SignNativeGuardianStateAsync(context, 47, "guardian_native_published_47", [15, 5],
                    Assert.IsType<JsonObject>(used.ExpectedConflict));
                Assert.Single(final.History.Transitions, row => row.Kind == "create");
                Assert.Equal(2, final.History.Transitions.Count);
                WritePressureOwnerTiming("guardian_native_complete_journey", NativeGuardianSession, started);
            },
            HasWindow: () => false, HasCapture: () => false, HasLease: () => false, HasContext: () => context is not null,
            AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
            AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => { }, AssertNoPrivatePair: () => { },
            DisposeLease: () => ValueTask.CompletedTask, DisposeContext: async () => await context!.DisposeAsync(),
            AssertRootRemoved: () => Assert.False(Directory.Exists(context!.RootPath))));
    }

    /// <summary>Full guardian proposal only; no canonical wound/effect identity or historical receipt is authored.</summary>
    /// <param name="birthRank">Independent closed III/IV case, fixed before signatures.</param>
    /// <returns>Independent mutable proposal fixed before the first signature.</returns>
    private static JsonObject CreateNativeGuardianProposal(int birthRank)
    {
        var proposal = JsonNode.Parse(OriginalSpiritualWoundDecision("unissued_proposal",
            "spiritual_strain_burden", "pressure", "guardian").GetRawText())!["proposal"]!.DeepClone().AsObject();
        proposal["severity"] = birthRank == 4 ? "IV" : "III";
        // This rank-I proposal template must be raised to the independently fixed birth threshold.
        proposal["recovery"]!["currentStepThreshold"] = birthRank == 4 ? 8 : 6;
        var consequence = proposal["consequenceDefinitions"]![0]!;
        var definition = consequence["definition"]!;
        definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
            "spiritual_roll_hindrance", "component_002", "counter"));
        definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
            "spiritual_action_cost_burden", "component_003", "guard", JsonValue.Create(birthRank == 4 ? 3 : 2)));
        definition["triggers"]![0]!["componentIds"]!.AsArray().Add("component_002");
        definition["triggers"]![0]!["componentIds"]!.AsArray().Add("component_003");
        consequence["root"]!["slots"]!.AsArray().Add(new JsonObject
            { ["profileKey"] = "spiritual_roll_hindrance", ["readableSummary"] = "Контрприём Хранителю даётся труднее." });
        consequence["root"]!["slots"]!.AsArray().Add(new JsonObject
            { ["profileKey"] = "spiritual_action_cost_burden", ["readableSummary"] = "Защита требует больше духовных сил." });
        if (birthRank == 4)
        {
            definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                "spiritual_action_cost_burden", "component_004", "counter", JsonValue.Create(3)));
            definition["triggers"]![0]!["componentIds"]!.AsArray().Add("component_004");
            consequence["root"]!["slots"]!.AsArray().Add(new JsonObject
                { ["profileKey"] = "spiritual_action_cost_burden", ["readableSummary"] = "Контрприём требует больше духовных сил." });
        }
        return proposal;
    }

    /// <summary>Authors the standard pressure cost with independently chosen original balances and maxima.</summary>
    /// <param name="exchange">This turn's new exchange, before intake.</param>
    /// <param name="playerBefore">Exact signed original player balance.</param>
    /// <param name="oppositionBefore">Exact signed original side balance.</param>
    /// <param name="maximum">Exact side maximum; the persistent player maximum is ten.</param>
    private static void SetNativeGuardianCosts(JsonObject exchange, decimal playerBefore, decimal oppositionBefore, decimal maximum)
    {
        exchange["actionCostAudit"]!["player"] = CostAudit("pressure", 3m, playerBefore, playerBefore - 3m);
        exchange["actionCostAudit"]!["player"]!["max"] = 10m;
        exchange["actionCostAudit"]!["opposition"] = CostAudit("pressure", 3m, oppositionBefore, oppositionBefore - 3m);
        exchange["actionCostAudit"]!["opposition"]!["max"] = maximum;
    }

    /// <summary>Authors only the genuine exchange/terminal response, never wound or lineage state.</summary>
    /// <param name="context">This Fact's current signed filesystem and validator.</param>
    /// <param name="exchange">Complete ordinary new exchange fixed before capture.</param>
    /// <param name="terminal">Whether birth44 also resolves and retires its conflict side.</param>
    /// <returns>Completion after authoring and ordinary location/item preflight.</returns>
    private static async Task AuthorNativeGuardianExchangeAsync(ResourceMaterializationTestContext context, JsonObject exchange, bool terminal)
    {
        var root = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
        var conflictId = root["activeConflict"]!["conflictId"]!.GetValue<string>();
        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root,
            new JsonObject { ["mode"] = "exchange", ["exchange"] = exchange.DeepClone() });
        Assert.Null(projected["lastInvalidUpdate"]);
        if (terminal)
        {
            projected = AfterlifeSpiritualConflictState.ApplyUpdate(projected, new JsonObject
            {
                ["mode"] = "resolve", ["resolution"] = new JsonObject
                {
                    ["conflictId"] = conflictId, ["operationType"] = "pressure", ["resolvedAtTurn"] = 44,
                    ["resolvedAtUtc"] = "2026-11-08T00:00:00Z", ["guardianId"] = "guardian_frame",
                    ["playerOutcome"] = "won", ["diceAudit"] = exchange["diceAudit"]!.DeepClone(),
                    ["terminalExchange"] = exchange.DeepClone(), ["summary"] = "Душа завершила духовное противостояние."
                }
            });
            Assert.Null(projected["activeConflict"]);
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, projected.ToJsonString());
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
            { ["response"] = "Чужое давление надломило волю хранителя.", ["timestamp"] = "2026-11-08T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
    }

    /// <summary>Detached actual published parent and complete lineage DATA; no permission or live handle.</summary>
    /// <param name="Wound">The current materialized guardian envelope.</param>
    /// <param name="History">Parsed complete current wound History.</param>
    /// <param name="Identity">Current wound identity root.</param>
    /// <param name="HistoryRoot">Exact parsed current History root.</param>
    /// <param name="Receipt">Current source and decision receipt root.</param>
    /// <param name="Profiles">Current carrier profiles, including the actual parent.</param>
    /// <param name="Effects">Current effect identity root.</param>
    private sealed record NativeGuardianParent(WoundMaterializationEnvelope Wound, WoundHistoryState History,
        JsonObject Identity, JsonObject HistoryRoot, JsonObject Receipt, JsonObject Profiles, JsonObject Effects);

    /// <summary>Reads only actual current publication and requires the complete guardian graph and exact creation receipt.</summary>
    /// <param name="context">This Fact's filesystem after a real completed publication.</param>
    /// <param name="birthRank">Independent expected rank selected before the original birth.</param>
    /// <returns>Detached checked parent and lineage DATA.</returns>
    private static async Task<NativeGuardianParent> ReadNativeGuardianParentAsync(ResourceMaterializationTestContext context, int birthRank)
    {
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        var guardian = ReadGuardianAttributionProfile(profiles);
        var woundJson = Assert.Single(guardian["activeWounds"]!.AsArray())!;
        var parsed = WoundMaterializationContract.Parse(woundJson.ToJsonString(), "native.guardian");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        var wound = parsed.Wound!;
        Assert.Equal(("chaos_sea", "guardian", "guardian_frame", WoundCarrierCatalog.AfterlifeProfilesPath),
            (wound.Owner.Realm, wound.Owner.OwnerKind, wound.Owner.OwnerId, wound.Owner.CarrierPath));
        Assert.Equal(("active", "spiritual", birthRank), (wound.Lifecycle, wound.Classification.Domain, wound.Severity.Rank));
        Assert.Equal(birthRank == 4 ? "IV" : "III", woundJson["severity"]!["maximumAtCreation"]!.GetValue<string>());
        Assert.Equal(birthRank == 4 ? 8L : 6L, wound.Recovery.CurrentStepThreshold);
        Assert.Equal((birthRank, birthRank, birthRank), (woundJson["consequences"]!["slotBudget"]!.GetValue<int>(),
            woundJson["consequences"]!["slotsUsed"]!.GetValue<int>(), woundJson["consequences"]!["entries"]!.AsArray().Count));
        var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
        var definition = Assert.Single(wound.Consequences.OwnedEffectSources.Definitions);
        var components = definition.GetProperty("components").EnumerateArray().ToArray();
        Assert.Equal(birthRank == 4
                ? new[] { "spiritual_strain_burden", "spiritual_roll_hindrance", "spiritual_action_cost_burden", "spiritual_action_cost_burden" }
                : new[] { "spiritual_strain_burden", "spiritual_roll_hindrance", "spiritual_action_cost_burden" },
            components.Select(row => row.GetProperty("profile").GetString()));
        Assert.Equal(birthRank == 4 ? new[] { "pressure", "counter", "guard", "counter" } : new[] { "pressure", "counter", "guard" },
            components.Select(row => row.GetProperty("payload").GetProperty("operation").GetString()));
        Assert.Equal(1, components[0].GetProperty("payload").GetProperty("magnitude").GetInt32());
        Assert.Equal(birthRank == 4 ? 3 : 2, components[2].GetProperty("payload").GetProperty("magnitude").GetInt32());
        if (birthRank == 4) Assert.Equal(3, components[3].GetProperty("payload").GetProperty("magnitude").GetInt32());
        Assert.Equal(birthRank == 4 ? new[] { "component_001", "component_002", "component_003", "component_004" }
                : new[] { "component_001", "component_002", "component_003" },
            Assert.Single(definition.GetProperty("triggers").EnumerateArray()).GetProperty("componentIds")
                .EnumerateArray().Select(row => row.GetString()));
        var effect = Assert.Single(guardian["activeEffects"]!.AsArray())!;
        Assert.Equal((root.EffectId, root.DefinitionKey, wound.WoundId, "guardian", "guardian_frame"),
            (effect["effectId"]!.GetValue<string>(), effect["source"]!["definitionKey"]!.GetValue<string>(),
                effect["source"]!["sourceId"]!.GetValue<string>(), effect["target"]!["kind"]!.GetValue<string>(),
                effect["target"]!["targetId"]!.GetValue<string>()));
        foreach (var field in new[] { "components", "triggers", "links" })
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(definition.GetProperty(field).GetRawText()), effect[field]));
        var identity = (await context.ReadJsonAsync(WoundIdentityState.StatePath))!.AsObject();
        var historyRoot = (await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.AsObject();
        var identities = WoundIdentityState.Parse(identity.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(historyRoot.ToJsonString(), WoundHistoryState.HistoryPath);
        var carrier = WoundCarrierCatalog.Build(new(null, null, null, null, profiles));
        Assert.Empty(carrier.Issues); Assert.Single(carrier.Occurrences);
        Assert.True(identities.IsValid, string.Join("\n", identities.Issues));
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.Empty(history.State!.ValidateAgreement(identities.State!, carrier));
        var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.AsObject();
        var receipts = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(receipts.IsValid, string.Join("\n", receipts.Issues));
        Assert.True(receipts.State!.HasExactStrainHistory(history.State));
        var creation = Assert.Single(history.State.ResolveWoundTransitions(wound.WoundId), row => row.Kind == "create");
        Assert.Equal((44, 1), (creation.Turn, creation.WoundTransitionOrdinal));
        Assert.True(receipts.State.CoversOriginalCreation(wound.WoundId, wound.Origin.OpportunityId, creation));
        var effects = (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.AsObject();
        var effectIdentities = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(effects), EffectIdentityState.StatePath);
        Assert.Empty(effectIdentities.Issues);
        Assert.True(effectIdentities.State!.TryGetEntry(root.EffectId, out var entry)); Assert.Equal("active", entry.State);
        return new(wound, history.State, identity, historyRoot, receipt, profiles, effects);
    }

    /// <summary>Signs existing published bytes and checks physical/snapshot equality before actual frame validation.</summary>
    /// <param name="context">This Fact's published canonical state.</param>
    /// <param name="turn">The new signed observation turn.</param>
    /// <param name="request">Distinct request identity for this signature.</param>
    /// <param name="dice">Authoritative visible dice for the new turn.</param>
    /// <param name="expectedConflict">Independent full actual conflict expectation for signed47, absent for signed46.</param>
    /// <returns>Completion after signature, byte equality, independent conflict and frame assertions.</returns>
    private async Task SignNativeGuardianStateAsync(ResourceMaterializationTestContext context, int turn, string request,
        int[] dice, JsonObject? expectedConflict = null)
    {
        var before = await CaptureExistingStrainCanonicalAsync(context);
        before[AfterlifeSpiritualConflictState.StatePath] = await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath);
        await context.CaptureValidatedPendingSnapshotAsync(turn: turn, currentRealm: "Chaos Sea", preGeneratedDices1d20: dice,
            sessionId: NativeGuardianSession, requestId: request);
        await ExecuteGuardianLeaseStagesAsync(context, async lease =>
        {
            var result = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease, PressureOrdinarySignedPaths);
            RequireGuardianJourneyIssues(NativeGuardianSession, "signed_" + turn, result.Issues);
            var signed = Assert.IsType<PendingTurnSnapshotReadAuthority>(result.Snapshot);
            Assert.Equal((NativeGuardianSession, request, turn), (signed.SessionId, signed.RequestId, signed.TurnNumber));
            Assert.Equal(dice, signed.AcceptedD20EventValues);
            foreach (var path in PressureOrdinarySignedPaths)
                Assert.Equal(await context.FileSystem.ReadFileBytesAsync(lease, path), signed.ReadRequiredBytes(path));
            if (expectedConflict is not null)
                Assert.True(JsonNode.DeepEquals(expectedConflict,
                    ReadGuardianAttributionJson(signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath))));
            foreach (var pair in before) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
            AssertPressureNoPrivatePair(context, lease);
        });
        RequireGuardianJourneyIssues(NativeGuardianSession, "signed_frame_" + turn, await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState)));
    }
}
