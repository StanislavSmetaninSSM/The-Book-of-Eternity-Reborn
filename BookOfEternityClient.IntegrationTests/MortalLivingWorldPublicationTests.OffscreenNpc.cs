using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLivingWorldPublicationTests
{
    private static readonly GameStateValidationSelection OffscreenNpcFinalSelection = new(
        GameStateValidationPhase.AcceptedTurnActorMaterializationCompleteness |
        GameStateValidationPhase.NpcStateFiles);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OrdinaryPublication_ReducedOffscreenNpcLocationAndActivityRetainHistoricalCarriers(bool withActivity)
    {
        await using var context = await PrepareOffscreenNpcOrdinaryAsync();
        var backups = await BackupsAsync(context);
        var original = (await context.ReadJsonAsync(CompletionCore))!.AsObject();
        var raw = original.DeepClone().AsObject();
        raw["NPCCoreChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_journey_courier",
            ["reason"] = "The courier followed the already accepted footpath.",
            ["location"] = new JsonObject { ["currentLocationId"] = "loc_journey_forest", ["initialLocationId"] = null }
        });
        await context.WriteJsonAsync(CompletionCore, raw);
        var sidecar = new JsonObject
        {
            ["NPCActivityUpdates"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_journey_captain",
                ["NPCName"] = "Captain Irina",
                ["activityUpdate"] = new JsonObject { ["timeSpentMinutes"] = 60, ["currentStepNumber"] = 1 }
            })
        };
        if (withActivity) await context.WriteJsonAsync(CompletionCommands, sidecar);
        var clock = (await context.ReadJsonAsync(MortalLivingWorldSource.WorldTimePath))!.AsObject();
        clock["currentTimeInMinutes"] = 540;
        clock["timeOfDay"] = "09:00";
        await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, clock);
        Assert.DoesNotContain(await context.Validator.ValidateNpcCoreChangesBeforeNormalizationAsync(),
            issue => issue.Severity == IssueSeverity.Error);

        var result = await PublishAsync(context, backups);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        var settled = (await context.ReadJsonAsync(CompletionCore))!.AsObject();
        Assert.Empty(settled["NPCsInScene"]!.AsArray());
        Assert.Null(settled["NPCCoreChanges"]);
        var expected = original.DeepClone();
        if (withActivity)
        {
            expected["UpdateNPCs"]![0]!["currentActivity"]!["timeSpentMinutes"] = 60;
            expected["UpdateNPCs"]![0]!["currentActivity"]!["currentStepNumber"] = 1;
        }
        expected["UpdateNPCs"]![1]!["currentLocationId"] = "loc_journey_forest";
        Assert.True(JsonNode.DeepEquals(expected, settled), "Both reducers must reach the exact canonical image; no fixture after-image is installed.");
        Assert.Null((await context.ReadJsonAsync(CompletionCommands))?["NPCActivityUpdates"]);

        var issues = await context.Validator.ValidateCompletedLocationStateAsync(result, OffscreenNpcFinalSelection);
        Assert.DoesNotContain(issues, issue => IsHistoricalOffscreenResendIssue(issue.Code));
        Assert.Contains(await context.Validator.ValidateGameStateAsync(OffscreenNpcFinalSelection),
            issue => IsHistoricalOffscreenResendIssue(issue.Code));
        var settledSidecar = await context.ReadJsonAsync(CompletionCommands);
        await context.WriteJsonAsync(CompletionCommands, sidecar);
        var activityDrift = await Assert.ThrowsAsync<ValidationService.CompletedLocationComparisonRefusedException>(
            () => context.Validator.ValidateCompletedLocationStateAsync(result, OffscreenNpcFinalSelection));
        Assert.Contains("off-screen NPC activity", activityDrift.Message);
        if (settledSidecar is null) context.FileSystem.DeleteFile(CompletionCommands);
        else await context.WriteJsonAsync(CompletionCommands, settledSidecar);
        var changed = settled.DeepClone();
        changed["UpdateNPCs"]![0]!["currentActivity"]!["timeSpentMinutes"] = 61;
        await context.WriteJsonAsync(CompletionCore, changed);
        var error = await Assert.ThrowsAsync<ValidationService.CompletedLocationComparisonRefusedException>(
            () => context.Validator.ValidateCompletedLocationStateAsync(result, OffscreenNpcFinalSelection));
        Assert.Contains("off-screen NPC published image changed", error.Message);
        context.FileSystem.DeleteFile(CompletionCore);
        await Assert.ThrowsAsync<ValidationService.CompletedLocationComparisonRefusedException>(
            () => context.Validator.ValidateCompletedLocationStateAsync(result, OffscreenNpcFinalSelection));
    }

    [Fact]
    public async Task OrdinaryPublication_ReducedOffscreenNpcTerminalOutcomeRetainsHistoricalCarrier()
    {
        await using var context = await PrepareOffscreenNpcOrdinaryAsync();
        var backups = await BackupsAsync(context);
        var original = (await context.ReadJsonAsync(CompletionCore))!.AsObject();
        var actor = original["UpdateNPCs"]![0]!;
        var activityName = actor["currentActivity"]!["activityName"]!.GetValue<string>();
        const string summary = "Confirmed the time, but could not identify the suspect.";
        var sidecar = new JsonObject
        {
            ["completeNPCActivities"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_journey_captain", ["NPCName"] = "Captain Irina",
                ["activityName"] = activityName, ["finalState"] = "Completed",
                ["finalOutcome"] = "SuccessWithComplication", ["narrativeSummary"] = summary
            })
        };
        await context.WriteJsonAsync(CompletionCommands, sidecar);

        var result = await PublishAsync(context, backups);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        var settled = (await context.ReadJsonAsync(CompletionCore))!.AsObject();
        var expected = original.DeepClone();
        var expectedActor = expected["UpdateNPCs"]![0]!;
        expectedActor["currentActivity"] = null;
        expectedActor["completedActivities"]!.AsArray().Add(new JsonObject
        {
            ["activityName"] = activityName, ["completionTurn"] = 42,
            ["finalOutcome"] = "SuccessWithComplication", ["narrativeSummary"] = summary
        });
        Assert.True(JsonNode.DeepEquals(expected, settled));
        Assert.Equal(0, (await context.ReadJsonAsync(CompletionCommands))!.AsObject().Count);

        var final = await context.Validator.ValidateCompletedLocationStateAsync(result, OffscreenNpcFinalSelection);
        Assert.DoesNotContain(final, issue => IsHistoricalOffscreenResendIssue(issue.Code));
        Assert.Contains(await context.Validator.ValidateGameStateAsync(OffscreenNpcFinalSelection),
            issue => IsHistoricalOffscreenResendIssue(issue.Code));
        await context.WriteJsonAsync(CompletionCommands, sidecar);
        await Assert.ThrowsAsync<ValidationService.CompletedLocationComparisonRefusedException>(
            () => context.Validator.ValidateCompletedLocationStateAsync(result, OffscreenNpcFinalSelection));
        await context.WriteJsonAsync(CompletionCommands, new JsonObject());
        settled["UpdateNPCs"]![0]!["completedActivities"]![0]!["completionTurn"] = 43;
        await context.WriteJsonAsync(CompletionCore, settled);
        await Assert.ThrowsAsync<ValidationService.CompletedLocationComparisonRefusedException>(
            () => context.Validator.ValidateCompletedLocationStateAsync(result, OffscreenNpcFinalSelection));
    }

    [Theory]
    [InlineData("raw-location")]
    [InlineData("raw-envelope")]
    [InlineData("raw-inventory")]
    public async Task OrdinaryPublication_AuthoredOffscreenNpcRowsAreNotCertifiedAsSettled(string mutation)
    {
        await using var context = await PrepareOffscreenNpcOrdinaryAsync();
        var raw = (await context.ReadJsonAsync(CompletionCore))!.AsObject();
        var actor = raw["UpdateNPCs"]![0]!;
        switch (mutation)
        {
            case "raw-location": actor["currentLocationId"] = "loc_journey_forest"; break;
            case "raw-envelope": actor["materialization"]!["materializationId"] = "mat_replacement"; break;
            case "raw-inventory": actor["inventory"]!.AsArray().Add(new JsonObject { ["itemId"] = "unauthorized_item" }); break;
        }
        await context.WriteJsonAsync(CompletionCore, raw);
        var issues = await context.Validator.ValidateGameStateAsync(OffscreenNpcFinalSelection);
        Assert.Contains(issues, issue => IsHistoricalOffscreenResendIssue(issue.Code));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task OrdinaryPublication_AuthorizedOffscreenNpcAcquisitionKeepsHistoricalCarrier(
        bool withMovement, bool withAssessment, bool wrongClaim)
    {
        const string npcId = "npc_journey_captain";
        const string creationRef = "new_offscreen_captain_lantern";
        const string authorityId = "npc_inventory_add:42:0:npc_journey_captain";
        await using var context = await PrepareOffscreenNpcOrdinaryAsync();
        var backups = await BackupsAsync(context);
        var original = (await context.ReadJsonAsync(CompletionCore))!.AsObject();
        var originalActor = Assert.Single(original["UpdateNPCs"]!.AsArray()
            .OfType<JsonObject>(), row => row["NPCId"]!.GetValue<string>() == npcId);
        Assert.Empty(originalActor["inventory"]!.AsArray());
        if (withMovement)
        {
            var raw = original.DeepClone().AsObject();
            raw["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = npcId,
                ["reason"] = "The captain followed the accepted route before receiving the equipment.",
                ["location"] = new JsonObject { ["currentLocationId"] = "loc_journey_forest", ["initialLocationId"] = null }
            });
            await context.WriteJsonAsync(CompletionCore, raw);
            var clock = (await context.ReadJsonAsync(MortalLivingWorldSource.WorldTimePath))!.AsObject();
            clock["currentTimeInMinutes"] = withAssessment ? 720 : 540;
            clock["timeOfDay"] = withAssessment ? "12:00" : "09:00";
            await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, clock);
            Assert.DoesNotContain(await context.Validator.ValidateNpcCoreChangesBeforeNormalizationAsync(),
                issue => issue.Severity == IssueSeverity.Error);
        }
        if (withAssessment)
        {
            const int from = 480, to = 720;
            var report = Report(changes: true);
            report["fromMinute"] = from;
            report["toMinute"] = to;
            report["events"] = new JsonArray(new JsonObject
            {
                ["eventId"] = "captain-obtains-lantern", ["fromMinute"] = from, ["toMinute"] = to,
                ["actors"] = new JsonArray(Npc()), ["targets"] = new JsonArray(Npc()),
                ["cause"] = new JsonObject
                {
                    ["summary"] = "The captain reaches the waystation and receives gear for the investigation.",
                    ["references"] = new JsonArray(Npc())
                },
                ["effects"] = new JsonArray(
                    new JsonObject
                    {
                        ["effectId"] = "arrive", ["target"] = Npc(), ["operation"] = "update",
                        ["field"] = "currentLocationId",
                        ["before"] = new JsonObject { ["present"] = true, ["value"] = originalActor["currentLocationId"]!.DeepClone() },
                        ["after"] = new JsonObject { ["present"] = true, ["value"] = "loc_journey_forest" }
                    },
                    new JsonObject
                    {
                        ["effectId"] = "acquire", ["target"] = Npc(), ["operation"] = "acquire",
                        ["field"] = "inventory",
                        ["before"] = new JsonObject { ["present"] = false },
                        ["after"] = new JsonObject { ["present"] = true, ["value"] = wrongClaim ? "other_lantern" : creationRef }
                    }),
                ["consequences"] = new JsonArray(new JsonObject
                {
                    ["summary"] = "The captain is at the waystation and now carries the acquired lantern.",
                    ["effectIds"] = new JsonArray("arrive", "acquire")
                }),
                ["knowledge"] = new JsonObject { ["player"] = "hidden", ["newsReferences"] = new JsonArray() }
            });
            await context.WriteJsonAsync(MortalLivingWorldSource.ReportPath, report);
            JsonObject Npc() => new() { ["kind"] = "npc", ["id"] = npcId };
        }
        const string commands = MortalItemAcceptedTransferCatalog.NpcCommandsPath;
        await context.WriteJsonAsync(commands, new JsonObject
        {
            ["NPCInventoryAdds"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = npcId,
                ["NPCName"] = originalActor["name"]!.GetValue<string>(),
                ["destinationContainerId"] = null,
                ["item"] = MortalItemTestFixture.CreateRawRoot(route: "npc_acquisition",
                    authorityKind: "npc_inventory_add", authorityId: authorityId,
                    creationRef: creationRef, materializationId: "mat_offscreen_captain_lantern")
            })
        });
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var rawIssues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(rawIssues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, rawIssues.Select(issue => $"{issue.Code}: {issue.FilePath}: {issue.Actual}")));
        var common = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem);
        Assert.NotNull(common);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(common!.Result.Plan);
        string allocatedId;
        string rootReceiptId;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(context.FileSystem, lease,
                common.Binding.SessionId, common.Binding.SnapshotToken, common.Binding.Turn, out var snapshot));
            Assert.True(snapshot.MatchesAcceptedOwnerAuthority(plan.OwnerAuthority));
            Assert.True(snapshot.TryGetAllocatedItemId(creationRef, out allocatedId!));
            Assert.True(snapshot.TryGetRootReceiptId(creationRef, out rootReceiptId!));
        }
        var beforePublication = wrongClaim ? await CaptureAsync(context) : null;
        var result = await PublishAsync(context, backups);
        if (beforePublication is not null)
        {
            Assert.Contains(result.Issues, issue => issue.Code == "mortal_living_world_npc_acquisition_mismatch");
            await AssertBytesAsync(context, beforePublication);
            return;
        }
        Assert.True(result.Issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.Code}: {issue.Section}: {issue.Message}")));
        if (withAssessment)
        {
            var history = await context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath);
            var entry = Assert.Single(Assert.IsType<JsonArray>(history?["entries"]));
            Assert.Equal(creationRef, entry!["report"]!["events"]![0]!["effects"]![1]!["after"]!["value"]!.GetValue<string>());
            Assert.Null(await context.ReadJsonAsync(MortalLivingWorldSource.ReportPath));
        }
        var settled = (await context.ReadJsonAsync(CompletionCore))!.AsObject();
        Assert.Empty(settled["NPCsInScene"]!.AsArray());
        var actor = Assert.Single(settled["UpdateNPCs"]!.AsArray()
            .OfType<JsonObject>(), row => row["NPCId"]!.GetValue<string>() == npcId);
        var item = Assert.Single(actor["inventory"]!.AsArray())!.AsObject();
        var itemId = item["itemId"]!.GetValue<string>();
        Assert.Equal(allocatedId, itemId);
        var receipt = item["materializationReceipt"]!;
        Assert.Equal(itemId, receipt["itemId"]!.GetValue<string>());
        Assert.Equal(rootReceiptId, receipt["receiptId"]!.GetValue<string>());
        Assert.Equal(creationRef, receipt["creationRef"]!.GetValue<string>());
        Assert.Equal(42, receipt["acceptedAtTurn"]!.GetValue<int>());
        Assert.Equal(authorityId, item["materialization"]!["sourceAuthority"]!["authorityId"]!.GetValue<string>());
        var index = MortalItemIdentityState.Parse((await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!);
        Assert.Empty(index.Issues);
        var carrier = index.EntriesByItemId[itemId]["currentCarrier"]!;
        Assert.Equal(npcId, carrier["ownerId"]!.GetValue<string>());
        Assert.Equal("npc_inventory", carrier["kind"]!.GetValue<string>());
        Assert.Null((await context.ReadJsonAsync(commands))?["NPCInventoryAdds"]);
        var withoutInventoryChange = settled.DeepClone();
        var checkedActor = Assert.Single(withoutInventoryChange["UpdateNPCs"]!.AsArray().OfType<JsonObject>(),
            row => row["NPCId"]!.GetValue<string>() == npcId);
        checkedActor["inventory"] = new JsonArray();
        var expected = original.DeepClone();
        if (withMovement)
        {
            var expectedActor = Assert.Single(expected["UpdateNPCs"]!.AsArray().OfType<JsonObject>(),
                row => row["NPCId"]!.GetValue<string>() == npcId);
            expectedActor["currentLocationId"] = "loc_journey_forest";
            expectedActor["initialLocationId"] = null;
        }
        Assert.True(JsonNode.DeepEquals(expected, withoutInventoryChange),
            "Only the exact historical actor's supported item and location deltas may change.");
        var issues = await context.Validator.ValidateCompletedLocationStateAsync(result, OffscreenNpcFinalSelection);
        Assert.DoesNotContain(issues, issue => IsHistoricalOffscreenResendIssue(issue.Code));
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        // The completed turn comparison cannot bless a different item index.
        // Whitespace preserves JSON semantics but changes the bound publication bytes.
        var indexText = await context.FileSystem.ReadFileAsync(MortalItemIdentityState.StatePath);
        Assert.NotNull(indexText);
        await context.FileSystem.WriteFileAtomicAsync(MortalItemIdentityState.StatePath, indexText + "\n");
        await Assert.ThrowsAsync<ValidationService.CompletedLocationComparisonRefusedException>(
            () => context.Validator.ValidateCompletedLocationStateAsync(result, OffscreenNpcFinalSelection));
    }

    private static bool IsHistoricalOffscreenResendIssue(string? code) => code is
        "actor_materialization_historical_envelope_changed" or
        "actor_materialization_existing_resend_forbidden" or
        "npc_existing_inventory_resend_forbidden";

    private static async Task<EffectMaterializationTestContext> PrepareOffscreenNpcOrdinaryAsync()
    {
        var context = await EffectMaterializationTestContext.CreateAsync();
        try
        {
            await MortalLiveSeedBuilder.PrepareAsync(context.FileSystem, TestRepoPaths.BaseSessionRoot,
                MortalLiveSeedBuilder.SeedOffscreenActorsAsync, stateOnly: true);
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                context.FileSystem.GetOrCreateSessionGeneration(lease);
            var scheduler = new ProgressionScheduleService(context.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance);
            var control = await scheduler.BuildControlForNextTurnAsync("Mortal World");
            await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = Session, RequestId = Request, TurnNumber = 42, CurrentRealm = "Mortal World",
                PlayerAction = "Wait in the private chambers while other people act.",
                ProgressionControl = control, PreGeneratedDices1d20 = [7]
            });
            return context;
        }
        catch { await context.DisposeAsync(); throw; }
    }
}
