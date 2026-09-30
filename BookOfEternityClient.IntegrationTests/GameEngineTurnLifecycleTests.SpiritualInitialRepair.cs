using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Repairs one ordinary resource reason before entering spiritual continuation and publishing the original turn once.
    /// </summary>
    /// <returns>
    /// A task completing after the same signed turn repairs its original command and consumes one actual wound offer.
    /// </returns>
    [Fact]
    public async Task AcceptedTurnSpiritualContinuation_InitialMissingResourceReasonUsesOrdinaryRepair()
    {
        const string repairPath = "game_state/control/validation_repair_request.json";
        const string readyPath = "game_state/control/validation_repair_ready.json";
        var input = new QueuedConsoleInputSource([]);
        GameEngine? engine = null;
        object? rollback = null;
        var request = new TurnRequest
        {
            SessionId = "session_spiritual_initial_repair", RequestId = "request_spiritual_initial_repair_42",
            TurnNumber = 42, PlayerAction = "Сосредоточиться и удержать встречное духовное давление.",
            Timestamp = DateTime.UtcNow.ToString("O"), PreGeneratedDices1d20 = [15, 5, 12, 8]
        };
        await using var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
        {
            engine = CreateGameEngine(input, fileSystem: original.FileSystem);
            await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
            await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
            request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
            rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "spiritual-initial-repair");
            await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync",
                request, rollback, "spiritual-initial-repair");
        });
        await AfterlifeResourceCutoverTests.WriteSpiritualGameEngineExchangeAsync(context);
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var command = new JsonObject
        {
            ["resourceChanges"] = new JsonArray(new JsonObject
            {
                ["operation"] = "spend",
                ["target"] = new JsonObject { ["kind"] = "afterlife_actor", ["targetId"] = "player_soul" },
                ["resourceKey"] = "spiritual_action_points", ["amount"] = 1,
                ["source"] = new JsonObject { ["kind"] = "action_cost" },
                ["eventRef"] = "turn_42:resource:1"
            }, new JsonObject
            {
                ["operation"] = "gain",
                ["target"] = new JsonObject { ["kind"] = "afterlife_actor", ["targetId"] = "player_soul" },
                ["resourceKey"] = "spiritual_action_points", ["amount"] = 1,
                ["source"] = new JsonObject { ["kind"] = "narrative_outcome" },
                ["eventRef"] = "turn_42:resource:2", ["reason"] = "Собранная воля возвращает потраченную духовную силу."
            })
        };
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath, command.ToJsonString());
        var initialErrors = ((IEnumerable)await InvokePrivateTaskResultAsync(engine!, "CollectAcceptedTurnRawStateIssuesAsync"))
            .Cast<ValidationIssue>().Where(issue => issue.Severity == IssueSeverity.Error).ToArray();
        Assert.NotEmpty(initialErrors);
        Assert.All(initialErrors, issue =>
        {
            Assert.Equal("resource_command_invalid_field", issue.Code);
            Assert.Equal("resourceChanges[0].reason", issue.FilePath);
            Assert.Equal("missing", issue.Actual);
        });
        Assert.Single(ResourceRepairPacketBuilder.Build(initialErrors));
        var resolution = await InvokePrivateTaskResultAsync(engine!, "ResolveActivePendingTurnSnapshotContextAsync");
        Assert.Equal("Usable", resolution.GetType().GetProperty("Status")!.GetValue(resolution)!.ToString());
        var snapshot = resolution.GetType().GetProperty("Context")!.GetValue(resolution);
        Assert.NotNull(snapshot);
        var preserved = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                     "game_state/control/pending_turn_snapshot.authority.json" })
            preserved.Add(path, (await context.FileSystem.ReadFileBytesAsync(path))!);
        var originalDraft = (await context.FileSystem.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath))!;
        var originalResources = await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath);
        var repairCount = 0;
        var decisionCount = 0;
        var freshnessCount = 0;
        Exception? callbackFailure = null;
        string? lastRepair = null;
        using var stop = new CancellationTokenSource();
        var physicalRepairPath = context.FileSystem.ResolvePath(repairPath);
        var callback = Task.Run(async () =>
        {
            string? handled = null;
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    string? raw = null;
                    if (File.Exists(physicalRepairPath))
                    {
                        try
                        {
                            using var stream = new FileStream(physicalRepairPath, FileMode.Open,
                                FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            using var reader = new StreamReader(stream);
                            raw = await reader.ReadToEndAsync(stop.Token);
                        }
                        catch (FileNotFoundException) { }
                    }
                    if (!string.IsNullOrWhiteSpace(raw) && raw != handled)
                    {
                        handled = lastRepair = raw;
                        using var document = JsonDocument.Parse(raw);
                        var repair = document.RootElement;
                        Assert.Equal(request.SessionId, repair.GetProperty("sessionId").GetString());
                        Assert.Equal(request.RequestId, repair.GetProperty("requestId").GetString());
                        Assert.Equal(42, repair.GetProperty("turnNumber").GetInt32());
                        foreach (var pair in preserved)
                            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
                        if (repair.TryGetProperty("spiritualWoundContinuation", out var envelope) &&
                            envelope.ValueKind != JsonValueKind.Null)
                        {
                            Assert.Equal(1, repairCount);
                            Assert.Equal(0, decisionCount);
                            var continuation = SpiritualWoundContinuationProtocol.ReadRequest(envelope);
                            Assert.Equal("decision", continuation.Phase);
                            Assert.Empty(repair.GetProperty("errors").EnumerateArray());
                            Assert.False(repair.GetProperty("fullTurnResubmissionRequired").GetBoolean());
                            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                            {
                                var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
                                var current = await fresh.ReadSpiritualWoundContinuationAsync(lease);
                                Assert.Equal("decision", current.Disposition);
                                Assert.Empty(current.Issues);
                                Assert.Equal(continuation.ContinuationId, current.Request!.ContinuationId);
                                Assert.Equal(originalResources, await context.FileSystem.ReadFileBytesAsync(lease,
                                    ResourceMaterializationContract.StatePath));
                            }
                            await context.WriteExactJsonAsync(readyPath, JsonSerializer.Serialize(new
                            {
                                sessionId = request.SessionId, requestId = request.RequestId, turnNumber = 42,
                                timestamp = DateTime.UtcNow.ToString("O"), status = "success",
                                spiritualWoundContinuation = new
                                {
                                    schemaVersion = 1, continuationId = continuation.ContinuationId,
                                    woundDecisions = new[] { new { opportunityRef = continuation.Offer!.OpportunityRef, decision = "none" } }
                                }
                            }));
                            decisionCount++;
                        }
                        else
                        {
                            var codes = repair.GetProperty("errors").EnumerateArray()
                                .Select(issue => issue.GetProperty("code").GetString()).ToArray();
                            if (codes.Contains("resource_command_invalid_field"))
                            {
                                Assert.Equal(0, repairCount);
                                Assert.Equal(0, decisionCount);
                                Assert.All(codes, code => Assert.Equal("resource_command_invalid_field", code));
                                Assert.True(repair.GetProperty("fullTurnResubmissionRequired").GetBoolean());
                                Assert.Contains(repair.GetProperty("harnessRepairPackets").EnumerateArray(),
                                    packet => packet.GetProperty("kind").GetString() == "resource_semantic_omission_repair");
                                Assert.False(context.FileSystem.FileExists(SpiritualWoundCaptureCheckpointState.StatePath));
                                Assert.False(context.FileSystem.FileExists(SpiritualWoundDecisionPendingState.StatePath));
                                Assert.False(context.FileSystem.FileExists(ResourceMaterializationContract.CommandPath));
                                Assert.Equal(originalResources, await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath));
                                command["resourceChanges"]![0]!["reason"] = "Сосредоточение перед обменом требует духовной силы.";
                                await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath, command.ToJsonString());
                                await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, originalDraft);
                                repairCount++;
                            }
                            else
                            {
                                Assert.Equal(1, repairCount);
                                Assert.Equal(1, decisionCount);
                                Assert.Equal(0, freshnessCount);
                                Assert.NotEmpty(codes);
                                Assert.All(codes, code => Assert.Equal("accepted_turn_stale_player_facing_output_after_canonical_repair", code));
                                freshnessCount++;
                            }
                            await WriteSpiritualLifecycleOutputsAsync(context, request);
                            await context.WriteExactJsonAsync(readyPath, JsonSerializer.Serialize(new
                            {
                                sessionId = request.SessionId, requestId = request.RequestId, turnNumber = 42,
                                updatedAtUtc = DateTime.UtcNow.ToString("O"), note = "Исходный ход повторно представлен с полным объяснением расхода."
                            }));
                        }
                    }
                    await Task.Delay(25, stop.Token);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (Exception error)
            {
                callbackFailure = error;
                input.Enqueue(Key(ConsoleKey.Escape));
            }
        });
        var validation = InvokePrivateTaskResultAsync(engine!, "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "ответа GM", snapshot, rollback, 42, request.ProgressionControl);
        object? disposition = null;
        try
        {
            disposition = await validation.WaitAsync(TimeSpan.FromSeconds(240));
        }
        finally
        {
            stop.Cancel();
            input.Enqueue(Key(ConsoleKey.Escape));
            await callback.WaitAsync(TimeSpan.FromSeconds(5));
            if (!validation.IsCompleted)
                await validation.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(callbackFailure is null, callbackFailure + "\nLast repair: " + lastRepair);
        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, Assert.IsType<AcceptedTurnValidationDisposition>(disposition));
        Assert.Equal(1, repairCount);
        Assert.Equal(1, decisionCount);
        foreach (var pair in preserved)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
        foreach (var path in new[] { repairPath, readyPath, SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, ResourceMaterializationContract.CommandPath,
                     AcceptedMechanicsPlan.WoundCommandPath })
            Assert.False(context.FileSystem.FileExists(path), path);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
        Assert.True(definitions.IsValid, string.Join(Environment.NewLine, definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        Assert.Equal(new[] { 3m, 3m }, state.Ledger!.Entries.Where(entry => entry.Coordinate.ResourceKey == "spiritual_action_points")
            .Select(entry => entry.Current).Order().ToArray());
        var history = ResourceHistoryState.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var spends = history.History!.Transitions.Where(transition => transition.Turn == 42 &&
            transition.Operation == ResourceTransitionOperation.Spend).ToArray();
        Assert.Equal(3, spends.Length);
        Assert.Equal(new[] { 1m, 3m, 3m }, spends.Select(transition => transition.AppliedAmount).Order().ToArray());
        Assert.Equal(2, spends.Count(transition => transition.OriginId == "exchange_conflict_frame_42"));
        var gain = Assert.Single(history.History.Transitions, transition => transition.Turn == 42 &&
            transition.Operation == ResourceTransitionOperation.Gain);
        Assert.Equal(1m, gain.AppliedAmount);
        var receipts = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        Assert.Single(receipts["instances"]!.AsArray());
        Assert.Single(receipts["sources"]!.AsArray());
        Assert.Equal("none", Assert.Single(receipts["decisions"]!.AsArray())!["decision"]!.GetValue<string>());
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        Assert.False(conflict.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        var exchange = Assert.Single(conflict["activeConflict"]!["exchangeLog"]!.AsArray())!;
        Assert.Equal("exchange_conflict_frame_42", exchange["exchangeId"]!.GetValue<string>());
        Assert.Equal(new[] { 15, 5 }, exchange["diceAudit"]!["diceUsed"]!.AsArray().Select(die => die!["value"]!.GetValue<int>()));
    }
}
