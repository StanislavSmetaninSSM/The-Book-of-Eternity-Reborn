using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Reads owned source inputs without admitting mechanics or changing canonical conflict state.
    /// The source reader covers its fixed sixteen inputs rather than the broader signed snapshot registry.
    /// </summary>
    [Fact]
    public async Task SourceOwner_RealOriginalReaderProducesOwnedSourceWithoutAdmissionOrWrites()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var before = await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        AssertNoConflictFrameErrors(prepared.Issues);
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
        var source = Assert.Single(session.Sources);
        Assert.True(session.Owns(source));
        Assert.False(session.Owns(source with { }));
        Assert.Empty(session.PendingRequirements);
        Assert.Equal("guardian:guardian_frame", source.AffectedActor);
        Assert.Equal("player_soul:player_soul", source.ActingActor);
        Assert.Equal("opposition", source.AffectedSide);
        Assert.Equal(4, source.Calculation.Input.SourceSeverityCap);
        Assert.Equal(10L, source.Calculation.Input.HarmfulMargin);
        Assert.Equal(1, source.Calculation.MaximumSeverityRank);
        Assert.Equal(new[] { 0, 1 }, session.ClaimedDice);
        Assert.Equal(16, session.SelectedPaths.Count);
        string[] expectedSourcePaths =
        [
            "game_state/meta/soul_state.json",
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            AfterlifeSpiritualConflictState.StatePath,
            AfterlifeEntityProfileState.StatePath,
            ShiningAbodeState.StatePath,
            AfterlifeSpiritualConflictState.DifficultySettingsPath,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            WoundCarrierCatalog.PlayerPath,
            WoundCarrierCatalog.NpcPath,
            WoundCarrierCatalog.EnemiesPath,
            WoundCarrierCatalog.AlliesPath
        ];
        Assert.Equal(expectedSourcePaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            session.SelectedPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(SpiritualWoundDecisionPendingState.StatePath, session.SelectedPaths);
        Assert.DoesNotContain(SpiritualWoundCaptureCheckpointState.StatePath, session.SelectedPaths);
        Assert.DoesNotContain(SpiritualWoundOpportunityReceiptState.StatePath, session.SelectedPaths);
        foreach (var path in session.SelectedPaths)
        {
            Assert.NotEqual(session.HasOriginalBytes(path), session.IsOriginalAbsent(path));
            if (session.IsOriginalAbsent(path))
                Assert.Null(session.ReadOriginal(path));
            else
                Assert.NotNull(session.ReadOriginal(path));
        }
        Assert.Equal("chaos_sea", session.Realm);
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        var detached = JsonNode.Parse(session.ReadOriginal(AfterlifeSpiritualConflictState.StatePath)!)!.AsObject();
        detached["activeConflict"] = null;
        Assert.NotNull(JsonNode.Parse(session.ReadOriginal(AfterlifeSpiritualConflictState.StatePath)!)!["activeConflict"]);
    }

    [Fact]
    public async Task SourceOwner_ShiningAbodeUsesSignedRealmAndRecomposedOriginalOwners()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        foreach (var profile in profiles["profiles"]!.AsArray().OfType<JsonObject>())
        {
            profile["realm"] = "Shining Abode";
            foreach (var binding in profile["resourceOwnerBindings"]!.AsArray().OfType<JsonObject>())
                binding["realm"] = "shining_abode";
        }
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/meta/soul_state.json"));
        soul["currentRealm"] = "Shining Abode";
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var active = root["activeConflict"]!.AsObject();
        active["realm"] = "Shining Abode";
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(ResourceBootstrapStateBuilder.BuildPristine().Definitions);
        var (state, history) = BuildActionPointState(definitions, profiles, active, soul, 6m, 6m);
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await context.WriteExactJsonAsync(ResourceMaterializationTestContext.StatePath, state.ToCanonicalJson());
        await context.WriteExactJsonAsync(ResourceMaterializationTestContext.HistoryPath, history.ToCanonicalJson());
        await WriteComposedAuthorityAsync(context, definitions, state, history);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Shining Abode", preGeneratedDices1d20: [15, 5, 12, 8]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        AssertNoConflictFrameErrors(result.Issues);
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
        Assert.Equal("shining_abode", session.Realm);
        Assert.Equal("Shining Abode", JsonNode.Parse(session.ReadOriginal("game_state/meta/soul_state.json")!)!
            ["currentRealm"]!.GetValue<string>());
        var source = Assert.Single(session.Sources);
        Assert.True(session.Owns(source));
        Assert.Equal(10L, source.Calculation.Input.HarmfulMargin);
        Assert.Equal("guardian:guardian_frame", source.AffectedActor);
        Assert.Empty(session.PendingRequirements);
        Assert.Equal(new[] { 0, 1 }, session.ClaimedDice);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceOwner_DiceFreeVoluntaryIncomingHarmRemainsPendingActualSource(bool special)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        if (special)
        {
            var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
            profiles["profiles"]![1]!["specialArts"]!.AsArray()
                .Add(SourceOwnerSpecialArt("guardian", "guardian_frame"));
            await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
            await context.CaptureValidatedPendingSnapshotAsync(
                turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        }
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
        var exchange = update["exchange"]!.AsObject();
        exchange["operationType"] = "negotiate";
        exchange["voluntary"] = true;
        exchange["outcome"] = "setback";
        exchange.Remove("diceAudit");
        exchange.Remove("matchupAudit");
        exchange["incomingAction"] = new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian_frame", ["operationType"] = "pressure"
        };
        exchange["after"]!["playerSideStrain"] = "strained";
        exchange["after"]!["oppositionSideStrain"] = "clear";
        exchange["actionCostAudit"]!.AsObject().Remove("player");
        if (special)
        {
            exchange["specialArtAudit"] = new JsonObject
            {
                ["artId"] = "art_source_owner", ["ownerActorType"] = "guardian",
                ["ownerActorId"] = "guardian_frame", ["baseOperation"] = "pressure",
                ["costMultiplierPercent"] = 200, ["effectNote"] = "Входящее искусство сохраняется для доказательства источника."
            };
            var cost = exchange["actionCostAudit"]!["opposition"]!.AsObject();
            cost["effectiveCost"] = 6;
            cost["after"] = 0;
            cost["specialArtId"] = "art_source_owner";
            cost["specialCostMultiplierPercent"] = 200;
            cost["standardEffectiveCost"] = 3;
        }
        update["activeConflictAfter"]!["playerSideStrain"] = "strained";
        update["activeConflictAfter"]!["oppositionSideStrain"] = "clear";
        update["activeConflictAfter"]!["exchangeLog"]!.AsArray()[0] = exchange.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        AssertNoConflictFrameErrors(result.Issues);
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
        Assert.Empty(session.Sources);
        Assert.Empty(session.ClaimedDice);
        var pending = Assert.Single(session.PendingRequirements);
        Assert.Equal(ValidationService.SpiritualSourceRequirement.AppliedSourceBinding, pending.Kind);
        Assert.Single(session.CheckedExchanges);
        Assert.Null(JsonNode.Parse(Assert.Single(session.CheckedExchanges))!["diceAudit"]);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Theory]
    [InlineData("request")]
    [InlineData("signed_bytes")]
    [InlineData("coverage")]
    [InlineData("dice_value")]
    [InlineData("tier_negative")]
    [InlineData("tier_fraction")]
    [InlineData("member_duplicate")]
    [InlineData("foreign_actor")]
    [InlineData("strain")]
    [InlineData("resource_projection")]
    public async Task SourceOwner_OneOriginalOrCandidateMutationCannotProduceSource(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        if (mutation == "resource_projection")
        {
            var original = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            original["activeConflict"]!.AsObject().Remove(AfterlifeEntityProfileState.ResourceOwnerBindingsProperty);
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
            await context.CaptureValidatedPendingSnapshotAsync(
                turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        }
        if (mutation is "tier_negative" or "tier_fraction")
        {
            var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
            profiles["profiles"]![1]!["standardArts"]!["spiritual_resilience"] =
                mutation == "tier_negative" ? JsonValue.Create(-1) : JsonValue.Create(1.5);
            await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
            await context.CaptureValidatedPendingSnapshotAsync(
                turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        }
        await WriteCompleteConflictFrameExchangeAsync(context);
        if (mutation == "request")
        {
            var request = Assert.IsType<JsonObject>(await context.ReadJsonAsync("input/turn_request.json"));
            request["requestId"] = "another_request";
            await context.WriteExactJsonAsync("input/turn_request.json", request.ToJsonString());
        }
        else if (mutation == "signed_bytes")
        {
            await context.WriteExactJsonAsync(
                "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", "{}");
        }
        else if (mutation == "coverage")
        {
            var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/control/pending_turn_snapshot.json"));
            manifest["files"]!.AsObject().Remove(AfterlifeEntityProfileState.StatePath);
            // The unresealed manifest is deliberately not original authority.
            await context.WriteExactJsonAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        }
        else if (mutation is "dice_value" or "member_duplicate" or "foreign_actor" or "strain")
        {
            var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
            var exchange = update["exchange"]!.AsObject();
            if (mutation == "dice_value")
                exchange["diceAudit"]!["diceUsed"]![0]!["value"] = 14;
            if (mutation == "strain")
                exchange["before"]!["oppositionSideStrain"] = "fractured";
            if (mutation == "member_duplicate")
                update["activeConflictAfter"]!["playerSide"]!["supporters"]!.AsArray()
                    .Add(update["activeConflictAfter"]!["oppositionSide"]!["leadContestant"]!.DeepClone());
            if (mutation == "foreign_actor")
                exchange["incomingAction"] = new JsonObject
                {
                    ["actorType"] = "guardian", ["actorId"] = "guardian_missing", ["operationType"] = "pressure"
                };
            update["activeConflictAfter"]!["exchangeLog"]!.AsArray()[0] = exchange.DeepClone();
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        }

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        Assert.Null(result.Session);
        Assert.Contains(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceOwner_WholeTurnDiceRegistryPreservesTwoExchangeOrder(bool duplicate)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
        var active = update["activeConflictAfter"]!.AsObject();
        var first = update["exchange"]!.AsObject();
        var second = first.DeepClone().AsObject();
        second["exchangeId"] = "exchange_source_second";
        second["before"] = first["after"]!.DeepClone();
        second["after"] = second["before"]!.DeepClone();
        second["after"]!["oppositionSideStrain"] = "fractured";
        second["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = duplicate ? 0 : 2;
        second["diceAudit"]!["diceUsed"]![0]!["value"] = duplicate ? 15 : 12;
        second["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = duplicate ? 1 : 3;
        second["diceAudit"]!["diceUsed"]![1]!["value"] = duplicate ? 5 : 8;
        second["diceAudit"]!["playerTotal"] = duplicate ? 15 : 12;
        second["diceAudit"]!["oppositionTotal"] = duplicate ? 5 : 8;
        second["diceAudit"]!["margin"] = duplicate ? 10 : 4;
        second["diceAudit"]!["outcomeBand"] = duplicate ? "decisive_player_success" : "player_success";
        foreach (var side in new[] { "player", "opposition" })
        {
            second["actionCostAudit"]![side]!["before"] = 3;
            second["actionCostAudit"]![side]!["after"] = 0;
        }
        active["exchangeLog"]!.AsArray().Add(second);
        active["oppositionSideStrain"] = "fractured";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        if (duplicate)
        {
            Assert.Null(result.Session);
            Assert.Contains(result.Issues, issue => issue.Code == "spiritual_source_input_invalid");
        }
        else
        {
            AssertNoConflictFrameErrors(result.Issues);
            var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
            Assert.Equal(2, session.CheckedExchanges.Count);
            Assert.Equal(new[] { 0, 1, 2, 3 }, session.ClaimedDice);
            Assert.Equal(new[] { "exchange_conflict_frame_42", "exchange_source_second" },
                session.Sources.Select(source => source.ExchangeId));
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, null)]
    [InlineData(1, null)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public async Task SourceOwner_SpecialSourceUsesOriginalEnvelopeAndHarderCaps(int? cap, int? guarantee)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var art = SourceOwnerSpecialArt("player_soul", "player_soul");
        if (cap.HasValue)
            art["spiritualWoundEnvelope"] = new JsonObject
            {
                ["schemaVersion"] = 1, ["maximumSeverityRank"] = cap.Value,
                ["guaranteedSeverityRank"] = guarantee
            };
        profiles["profiles"]![0]!["specialArts"]!.AsArray().Add(art);
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
        var exchange = update["exchange"]!.AsObject();
        exchange["specialArtAudit"] = new JsonObject
        {
            ["artId"] = "art_source_owner", ["ownerActorType"] = "player_soul",
            ["ownerActorId"] = "player_soul", ["baseOperation"] = "pressure",
            ["costMultiplierPercent"] = 200, ["effectNote"] = "Искусство оставляет точный след в духовном давлении."
        };
        var cost = exchange["actionCostAudit"]!["player"]!.AsObject();
        cost["effectiveCost"] = 6;
        cost["after"] = 0;
        cost["specialArtId"] = "art_source_owner";
        cost["specialCostMultiplierPercent"] = 200;
        cost["standardEffectiveCost"] = 3;
        update["activeConflictAfter"]!["exchangeLog"]!.AsArray()[0] = exchange.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        // A current profile change cannot grant a stronger guarantee to this source.
        profiles["profiles"]![0]!["specialArts"]![0]!["spiritualWoundEnvelope"] = new JsonObject
        {
            ["schemaVersion"] = 1, ["maximumSeverityRank"] = 4, ["guaranteedSeverityRank"] = 4
        };
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        if (guarantee == 2)
        {
            AssertNoConflictFrameErrors(result.Issues);
            var retained = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
            var laterGuarantee = Assert.Single(retained.Sources);
            Assert.Equal(1, laterGuarantee.Calculation.MaximumSeverityRank);
            Assert.Equal(2, laterGuarantee.GuaranteedSeverityRank);
            var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            using var owner = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
            AssertNoConflictFrameErrors(await owner.BeginResourceExecutionAsync(lease));
            var step = await owner.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(step.Issues);
            var ownerSource = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
                OriginalCaptureField(owner, "_source"));
            var admitted = await owner.AdmitWoundSourceAsync(lease,
                step.Step!.Interval!, Assert.Single(ownerSource.Sources));
            AssertNoConflictFrameErrors(admitted.Issues);
            var impossible = await owner.ReadWoundOpportunityAsync(lease, admitted.Admission!);
            Assert.Null(impossible.Opportunity);
            Assert.Contains(impossible.Issues,
                issue => issue.Code == "spiritual_wound_guarantee_above_maximum");
            return;
        }
        AssertNoConflictFrameErrors(result.Issues);
        var source = Assert.Single(Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session).Sources);
        Assert.Equal(cap ?? 4, source.Calculation.Input.SourceSeverityCap);
        Assert.Equal(guarantee, source.GuaranteedSeverityRank);
        Assert.Equal(Math.Min(1, cap ?? 4), source.Calculation.MaximumSeverityRank);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("escalation")]
    [InlineData("terminal_missing")]
    [InlineData("terminal_complete")]
    public async Task SourceOwner_PendingContoursAreRetainedNotAdmission(string contour)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        if (contour == "start")
        {
            var original = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            await WriteCompleteConflictFrameExchangeAsync(context);
            var proposed = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            var seed = AfterlifeSpiritualConflictState.ApplyUpdate(original,
                proposed[AfterlifeSpiritualConflictState.ResponseField]!.AsObject())["activeConflict"]!.DeepClone();
            original["activeConflict"] = null;
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
            await context.CaptureValidatedPendingSnapshotAsync(
                turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
            original[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
            {
                ["mode"] = "start", ["conflictSeed"] = seed
            };
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
        }
        else
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
            if (contour == "escalation")
            {
                root = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);
                root.Remove(AfterlifeSpiritualConflictState.ResponseField);
                root["activeConflict"]!["dangerMode"] = "annihilation";
            }
            else
            {
                var exchange = update["exchange"]!.DeepClone();
                root.Remove(AfterlifeSpiritualConflictState.ResponseField);
                root["activeConflict"] = null;
                var resolution = new JsonObject
                {
                    ["conflictId"] = "conflict_resource_cost", ["realm"] = "Chaos Sea",
                    ["dangerMode"] = "hostile", ["operationType"] = "pressure",
                    ["resolutionState"] = "resolved", ["resolvedAtTurn"] = 42,
                    ["diceAudit"] = exchange["diceAudit"]!.DeepClone()
                };
                if (contour == "terminal_complete")
                    resolution["terminalExchange"] = exchange;
                root["recentConflicts"]!.AsArray().Add(resolution);
            }
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        }
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        AssertNoConflictFrameErrors(result.Issues);
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
        if (contour is "start" or "terminal_missing")
            Assert.False(session.TryReadInitialActiveExchangeInventory(out _, out _));
        else if (contour == "terminal_complete")
        {
            Assert.True(session.TryReadInitialActiveExchangeInventory(out var conflictId, out var exchangeIds));
            Assert.Equal("conflict_resource_cost", conflictId);
            Assert.Equal("exchange_conflict_frame_42", Assert.Single(exchangeIds));
        }
        var pending = Assert.Single(session.PendingRequirements);
        Assert.Equal(contour switch
        {
            "start" => ValidationService.SpiritualSourceRequirement.PriorStartDeclaration,
            "escalation" => ValidationService.SpiritualSourceRequirement.PriorEscalationDeclaration,
            "terminal_missing" => ValidationService.SpiritualSourceRequirement.TerminalExchangeAudit,
            _ => ValidationService.SpiritualSourceRequirement.TerminalClosure
        }, pending.Kind);
        Assert.NotEmpty(pending.CandidateJson);
        Assert.True(session.HasWork);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.Equal(contour == "terminal_complete" ? 1 : 0, session.Sources.Count);
    }

    [Fact]
    public async Task SourceOwner_DisposedLeaseCannotAcquire()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        await lease.DisposeAsync();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Validator.BeginSpiritualWoundSourceSessionAsync(lease));
    }


    [Fact]
    public async Task SourceOwner_BothSidesUseSignedDirectionAndPlayerSoulNotMirror()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        profiles["profiles"]![0]!["standardArts"]!["spiritual_resilience"] = 5;
        profiles["profiles"]![0]!["standardArts"]!["pressure"] = 5;
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
        update["exchange"]!["after"]!["playerSideStrain"] = "strained";
        update["activeConflictAfter"]!["playerSideStrain"] = "strained";
        update["activeConflictAfter"]!["exchangeLog"]!.AsArray()[0] = update["exchange"]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        AssertNoConflictFrameErrors(result.Issues);
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
        Assert.Equal(2, session.Sources.Count);
        var player = session.Sources.Single(source => source.AffectedSide == "player");
        var opposition = session.Sources.Single(source => source.AffectedSide == "opposition");
        Assert.Equal(-10L, player.Calculation.Input.HarmfulMargin);
        Assert.Equal(10L, opposition.Calculation.Input.HarmfulMargin);
        Assert.Equal(0, player.Calculation.Input.TargetResilienceTier);
        Assert.Equal(0, opposition.Calculation.Input.AppliedArtTier);
        Assert.Equal(new[] { 0, 1 }, session.ClaimedDice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceOwner_PassiveSideHasNoFabricatedSpendOrArtTier(bool passiveHarm)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
        var exchange = update["exchange"]!.AsObject();
        exchange["matchupAudit"]!["oppositionOperation"] = "passive";
        exchange["actionCostAudit"]!.AsObject().Remove("opposition");
        if (passiveHarm)
        {
            exchange["after"]!["playerSideStrain"] = "strained";
            update["activeConflictAfter"]!["playerSideStrain"] = "strained";
        }
        update["activeConflictAfter"]!["exchangeLog"]!.AsArray()[0] = exchange.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        AssertNoConflictFrameErrors(result.Issues);
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
        Assert.Single(session.Sources);
        Assert.Null(JsonNode.Parse(Assert.Single(session.CheckedExchanges))!["actionCostAudit"]!["opposition"]);
        if (passiveHarm)
            Assert.Equal(ValidationService.SpiritualSourceRequirement.AppliedSourceBinding,
                Assert.Single(session.PendingRequirements).Kind);
        else
            Assert.Empty(session.PendingRequirements);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task SourceOwner_EveryRawResilienceTierRemainsLegal(int tier)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        profiles["profiles"]![1]!["standardArts"]!["spiritual_resilience"] = tier;
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(result.Issues);
        var source = Assert.Single(Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session).Sources);
        Assert.Equal(tier, source.Calculation.Input.TargetResilienceTier);
        Assert.Equal(10L - 2L * tier, source.Calculation.TraumaPressure);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("exchangeAtTurn")]
    [InlineData("resolvedAtTurn")]
    [InlineData("wrong")]
    public async Task SourceOwner_CurrentnessComesFromOriginalComparisonNotMandatoryNewMarker(string marker)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
        var exchange = update["exchange"]!.AsObject();
        exchange.Remove("turnNumber");
        if (marker != "absent")
            exchange[marker == "wrong" ? "turnNumber" : marker] = marker == "wrong" ? 41 : 42;
        update["activeConflictAfter"]!["exchangeLog"]!.AsArray()[0] = exchange.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        if (marker == "wrong")
        {
            Assert.Null(result.Session);
            Assert.Contains(result.Issues, issue => issue.Code == "spiritual_source_input_invalid");
        }
        else
        {
            AssertNoConflictFrameErrors(result.Issues);
            Assert.Single(Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session).Sources);
        }
    }

    [Theory]
    [InlineData("exact")]
    [InlineData("missing_reference")]
    [InlineData("history")]
    [InlineData("current_removal")]
    public async Task SourceOwner_RetraumaResolvesExactOriginalCarrierIdentityAndHistory(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var wound = WoundContractTestData.CreateActiveWound(
            realm: "chaos_sea", ownerKind: "guardian", ownerId: "guardian_frame",
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath, domain: "spiritual");
        profiles["profiles"]![1]!["activeWounds"] = new JsonArray(wound);
        var originalCarriers = WoundCarrierCatalog.Build(new(null, null, null, null, profiles));
        Assert.Empty(originalCarriers.Issues);
        var originalOccurrence = Assert.Single(originalCarriers.Occurrences);
        Assert.Equal("wound_test_torn_side", originalOccurrence.WoundId);
        Assert.Equal("guardian_frame", originalOccurrence.Coordinate.OwnerId);
        var parsed = WoundMaterializationContract.Parse(wound.ToJsonString(), "wound");
        Assert.True(parsed.IsValid, string.Join("; ", parsed.Issues.Select(issue => issue.Code)));
        var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound!);
        var identities = WoundContractTestData.CreateIdentityIndex(
            WoundContractTestData.CreateIdentityEntry(
                realm: "chaos_sea", ownerKind: "guardian", ownerId: "guardian_frame",
                carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath, domain: "spiritual",
                semanticFingerprint: fingerprint));
        var transition = WoundContractTestData.CreateTransition();
        transition["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint("wound_test_torn_side");
        transition["afterFingerprint"] = mutation == "history" ? "sha256:" + new string('a', 64) : fingerprint;
        transition["sourceFingerprint"] = "sha256:" + new string('e', 64);
        transition["attemptId"] = null;
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.WriteExactJsonAsync(WoundIdentityState.StatePath, identities.ToJsonString());
        await context.WriteExactJsonAsync(WoundHistoryState.HistoryPath,
            WoundContractTestData.CreateHistory(transition).ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
        update["exchange"]!["spiritualWoundTarget"] = new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian_frame",
            ["retraumaWoundRef"] = mutation == "missing_reference" ? "wound_missing" : "wound_test_torn_side"
        };
        update["activeConflictAfter"]!["exchangeLog"]!.AsArray()[0] = update["exchange"]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        if (mutation == "current_removal")
        {
            profiles["profiles"]![1]!["activeWounds"] = new JsonArray();
            await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        }
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        if (mutation is "history" or "missing_reference")
        {
            Assert.Null(result.Session);
            var sourceIssue = Assert.Single(result.Issues,
                issue => issue.Code == "spiritual_source_input_invalid");
            Assert.Equal(mutation == "history"
                ? "original re-trauma history/identity/carrier agreement"
                : "exact active re-trauma wound in the authoritative view", sourceIssue.Actual);
        }
        else
        {
            Assert.True(!result.Issues.Any(issue => issue.Severity == IssueSeverity.Error),
                string.Join(Environment.NewLine, result.Issues.Select(
                    issue => $"{issue.Code}: {issue.Actual}")));
            var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
            Assert.Equal("wound_test_torn_side", Assert.Single(session.Sources).RetraumaWoundId);
            // Current removal remains a mandatory C/E candidate-agreement check.
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        }
    }

    [Theory]
    [InlineData("duplicate_retained")]
    [InlineData("duplicate_resolution")]
    [InlineData("prefix_required")]
    [InlineData("dice_mismatch")]
    public async Task SourceOwner_TerminalCannotReuseHistoryOrPretendMissingPrefixIsProved(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var original = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        await WriteCompleteConflictFrameExchangeAsync(context);
        var proposed = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var exchange = proposed[AfterlifeSpiritualConflictState.ResponseField]!["exchange"]!.DeepClone().AsObject();
        if (mutation == "duplicate_retained")
        {
            var retained = exchange.DeepClone().AsObject();
            retained["turnNumber"] = 41;
            original["activeConflict"]!["exchangeLog"]!.AsArray().Add(retained);
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
            await context.CaptureValidatedPendingSnapshotAsync(
                turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        }
        if (mutation == "prefix_required")
            exchange["before"]!["playerSideStrain"] = "strained";
        var terminal = new JsonObject
        {
            ["conflictId"] = "conflict_resource_cost", ["realm"] = "Chaos Sea",
            ["dangerMode"] = "hostile", ["operationType"] = "pressure",
            ["resolutionState"] = "resolved", ["resolvedAtTurn"] = 42,
            ["terminalExchange"] = exchange, ["diceAudit"] = exchange["diceAudit"]!.DeepClone()
        };
        if (mutation == "dice_mismatch")
            terminal["diceAudit"]!["margin"] = 9;
        var candidate = original.DeepClone().AsObject();
        candidate["activeConflict"] = null;
        candidate["recentConflicts"] = new JsonArray(terminal);
        if (mutation == "duplicate_resolution")
            candidate["recentConflicts"]!.AsArray().Add(terminal.DeepClone());
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        if (mutation == "prefix_required")
        {
            AssertNoConflictFrameErrors(result.Issues);
            var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
            Assert.Empty(session.Sources);
            Assert.Empty(session.ClaimedDice);
            Assert.Equal(ValidationService.SpiritualSourceRequirement.TerminalExchangePrefix,
                Assert.Single(session.PendingRequirements).Kind);
        }
        else
        {
            Assert.Null(result.Session);
            Assert.Contains(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        }
    }


    [Fact]
    public async Task SourceOwner_HistoricalReadableSummaryDriftUsesSameOneUseMatcher()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var original = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        await WriteCompleteConflictFrameExchangeAsync(context);
        var proposal = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var historical = proposal[AfterlifeSpiritualConflictState.ResponseField]!["exchange"]!.DeepClone().AsObject();
        historical["exchangeId"] = "exchange_source_historical";
        historical["exchangeAtTurn"] = 41;
        historical["turnNumber"] = 41;
        historical["summary"] = "Прежний обмен.";
        original["activeConflict"]!["exchangeLog"]!.AsArray().Add(historical);
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        var current = proposal[AfterlifeSpiritualConflictState.ResponseField]!["activeConflictAfter"]!.DeepClone().AsObject();
        var retained = historical.DeepClone().AsObject();
        retained["summary"] = "Уточнённый рассказ о прежнем обмене.";
        current["exchangeLog"]!.AsArray().Insert(0, retained);
        original["activeConflict"] = current;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(result.Issues);
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
        Assert.Single(session.Sources);
        Assert.Single(session.CheckedExchanges);
        Assert.Equal(new[] { 0, 1 }, session.ClaimedDice);
    }


    [Fact]
    public async Task SourceOwner_FreshValidatorReconstructsOriginalSourceAfterCurrentInputsChange()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        ValidationService.SpiritualWoundSourceSession originalSession;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            originalSession = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
        }

        // Leave only the authored exchange and real current request/signature untouched.
        // Current profile/resource/effect/wound inputs cannot replace signed originals.
        foreach (var path in originalSession.SelectedPaths.Where(path =>
            path != AfterlifeSpiritualConflictState.StatePath))
            await context.WriteExactJsonAsync(path, "{}");
        var freshValidator = new ValidationService(context.FileSystem,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidationService>.Instance);
        await using var freshLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var replayed = await freshValidator.BeginSpiritualWoundSourceSessionAsync(freshLease);

        AssertNoConflictFrameErrors(replayed.Issues);
        var cold = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(replayed.Session);
        var previousSource = Assert.Single(originalSession.Sources);
        var coldSource = Assert.Single(cold.Sources);
        Assert.Equal(previousSource, coldSource);
        Assert.False(cold.Owns(previousSource));
        Assert.True(cold.Owns(coldSource));
        Assert.Equal(originalSession.SessionId, cold.SessionId);
        Assert.Equal(originalSession.RequestId, cold.RequestId);
        Assert.Equal(originalSession.SnapshotToken, cold.SnapshotToken);
        Assert.Equal(originalSession.ClaimedDice.ToArray(), cold.ClaimedDice.ToArray());
        foreach (var path in originalSession.SelectedPaths)
            Assert.Equal(originalSession.ReadOriginal(path), cold.ReadOriginal(path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, freshLease));
        // C/E must still reject invalid current candidates. Cold B preparation alone
        // never authorizes those current writes or admits a wound.
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceOwner_SignedAbsentSettingsUseOnlyExistingNoSettingsContour(bool forgedDifficultyAudit)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        Assert.Null(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath));
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"));
        Assert.False(manifest["originalPathPresenceV1"]![AfterlifeSpiritualConflictState.DifficultySettingsPath]!.GetValue<bool>());
        await WriteCompleteConflictFrameExchangeAsync(context);
        // A current file cannot retroactively become an original difficulty declaration.
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath,
            """{"difficulty":"impossible"}""");
        if (forgedDifficultyAudit)
        {
            var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
            update["exchange"]!["diceAudit"]!["difficultyAudit"] = new JsonObject
            {
                ["difficulty"] = "normal",
                ["source"] = "game_state/core/game_settings.json.difficulty",
                ["oppositionModifier"] = 0, ["rewardMultiplierPercent"] = 100
            };
            update["activeConflictAfter"]!["exchangeLog"]!.AsArray()[0] = update["exchange"]!.DeepClone();
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        }
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        if (forgedDifficultyAudit)
        {
            Assert.Null(result.Session);
            Assert.Contains(result.Issues, issue =>
                issue.Code == "afterlife_conflict_dice_difficulty_without_settings");
        }
        else
        {
            AssertNoConflictFrameErrors(result.Issues);
            var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
            Assert.True(session.IsOriginalAbsent(AfterlifeSpiritualConflictState.DifficultySettingsPath));
            Assert.False(session.HasOriginalBytes(AfterlifeSpiritualConflictState.DifficultySettingsPath));
            Assert.Null(session.ReadOriginal(AfterlifeSpiritualConflictState.DifficultySettingsPath));
            Assert.Equal(10L, Assert.Single(session.Sources).Calculation.Input.HarmfulMargin);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ReadOriginal("unselected/path.json"));
        }
    }

    [Fact]
    public async Task SourceOwner_AuthenticOldManifestWithUncoveredSettingsDoesNotMeanAbsent()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"));
        Assert.False(manifest["files"]!.AsObject().ContainsKey(AfterlifeSpiritualConflictState.DifficultySettingsPath));
        Assert.True(manifest.Remove("originalPathPresenceV1"));
        manifest["manifestPayloadHash"] = string.Empty;
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await context.WriteExactJsonAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        // Correctly re-sign an old-shaped fixture: the failure must be absent proof,
        // not an unresealed-manifest or detached-authority mismatch.
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(context.FileSystem);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        Assert.Null(result.Session);
        Assert.Contains(result.Issues, issue => issue.Code == "pending_turn_snapshot_reader_absence_unproven");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "pending_turn_snapshot_reader_authority_invalid");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Fact]
    public async Task SourceOwner_SignedAbsentConflictRetainsStartRequirementWithoutUsingStaleSnapshotBytes()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var original = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        await WriteCompleteConflictFrameExchangeAsync(context);
        var proposal = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var seed = AfterlifeSpiritualConflictState.ApplyUpdate(original,
            proposal[AfterlifeSpiritualConflictState.ResponseField]!.AsObject())["activeConflict"]!.DeepClone();
        await context.DeleteAsync(AfterlifeSpiritualConflictState.StatePath);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            new JsonObject { ["schemaVersion"] = 1, ["activeConflict"] = seed,
                ["recentConflicts"] = new JsonArray() }.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(result.Issues);
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
        Assert.True(session.IsOriginalAbsent(AfterlifeSpiritualConflictState.StatePath));
        Assert.False(session.HasOriginalBytes(AfterlifeSpiritualConflictState.StatePath));
        Assert.Null(session.ReadOriginal(AfterlifeSpiritualConflictState.StatePath));
        Assert.Empty(session.Sources);
        Assert.Empty(session.ClaimedDice);
        Assert.Equal(ValidationService.SpiritualSourceRequirement.PriorStartDeclaration,
            Assert.Single(session.PendingRequirements).Kind);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Fact]
    public async Task SourceOwner_SignedAbsentWoundIdentityIsNotAnExistingRetraumaTarget()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var original = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(original.Issues);
            var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(original.Session);
            Assert.True(session.IsOriginalAbsent(WoundIdentityState.StatePath));
            Assert.Null(session.ReadOriginal(WoundIdentityState.StatePath));
            Assert.Null(Assert.Single(session.Sources).RetraumaWoundId);
        }
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
        update["exchange"]!["spiritualWoundTarget"] = new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian_frame",
            ["retraumaWoundRef"] = "wound_not_original"
        };
        update["activeConflictAfter"]!["exchangeLog"]!.AsArray()[0] = update["exchange"]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var finalLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(finalLease);
        Assert.Null(result.Session);
        Assert.Contains(result.Issues, issue => issue.Code == "spiritual_source_input_invalid");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "pending_turn_snapshot_reader_absence_unproven");
    }


    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{ broken")]
    [InlineData("\"normal\"")]
    public async Task SourceOwner_CoveredSettingsCannotMasqueradeAsSignedAbsence(string settingsJson)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath, settingsJson);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"));
        Assert.True(manifest["originalPathPresenceV1"]![AfterlifeSpiritualConflictState.DifficultySettingsPath]!.GetValue<bool>());
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        Assert.Null(result.Session);
        Assert.Contains(result.Issues, issue => issue.Code == (settingsJson == "{}"
            ? "afterlife_conflict_dice_difficulty_audit_missing"
            : "spiritual_source_input_invalid"));
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "pending_turn_snapshot_reader_absence_unproven");
    }

    private static JsonObject SourceOwnerSpecialArt(string actorType, string actorId) => new()
    {
        ["artId"] = "art_source_owner",
        ["displayName"] = "Нить надлома",
        ["effectSummary"] = "Давление оставляет след в духовном узоре.",
        ["ownerActorType"] = actorType, ["ownerActorId"] = actorId,
        ["baseOperation"] = "pressure", ["tier"] = 0,
        ["costMultiplierPercent"] = 200,
        ["upgradeCost"] = new JsonObject { ["inkFeathers"] = 1 },
        ["canTeachPlayer"] = false, ["trainingConditions"] = new JsonArray()
    };
}
