using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public Task GuaranteedTreatment_AppliedBeforeStoryFreshProcessCompletesOnceAndRepeatsInertly() =>
        RunJointCompletionControlAsync(nonzeroProgression: false, pauseAppliedForCold: true);

    [Fact]
    public Task GuaranteedTreatment_UnappliedCommonFreshProcessAppliesPhaseAndCompletesExactlyOnce() =>
        RunJointCompletionControlAsync(nonzeroProgression: false, pauseUnappliedForCold: true);

    [Fact]
    public Task GuaranteedTreatment_NonzeroReportConsumedBeforeAppliedColdCompletesWithoutRecreation() =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true);

    private async Task RunJointAppliedBeforeStoryColdAsync(GameEngine parent, PendingTurnSnapshotReadAuthority original,
        string requestFingerprint, IReadOnlyDictionary<string, byte[]?> originalBefore, string scene, string expectedPhase = "applied",
        string? refusalScenario = null)
    {
        var originalRequest = StrictJsonAuthority.Deserialize<TurnRequest>(CanonicalJsonUtf8.DecodeOneOptionalBom(originalBefore["input/turn_request.json"]!),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual cold parent original control");
        Assert.NotNull(originalRequest); Assert.NotNull(originalRequest!.ProgressionControl);
        var expectedWorldCycles = originalRequest.ProgressionControl!.WorldCyclesAlreadyPendingBeforeTurn;
        Dictionary<string, byte[]?> paused;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            paused = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
            Assert.Equal(originalBefore["$sessionGeneration"], paused["$sessionGeneration"]);
            var marker = ParseJsonObjectBytes(Assert.IsType<byte[]>(paused[MortalQteDecisionPublication.StatePath]));
            Assert.Equal(2, marker["schemaVersion"]!.GetValue<int>());
            Assert.Equal(expectedPhase, marker["progression"]!["phase"]!.GetValue<string>());
            if (expectedPhase == "applied")
            {
                Assert.Null(await _fs.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath));
                if (expectedWorldCycles > 0)
                {
                    var realReport = Assert.IsType<byte[]>(originalBefore[ProgressionScheduleService.ReportPath]);
                    Assert.Equal(realReport, Convert.FromBase64String(marker["progression"]!["reportBefore"]!["bytes"]!.GetValue<string>()));
                    Assert.False(marker["progression"]!["reportAfter"]!["exists"]!.GetValue<bool>());
                }
            }
            Assert.Equal(original.RequestId, marker["requestId"]!.GetValue<string>());
            Assert.Equal(originalBefore[QteStoryPath], paused[QteStoryPath]);
            foreach (var path in new[] { "input/turn_request.json", LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                         PendingTurnSnapshotAuthority.AuthorityPath, "ready/turn_complete.json" })
                Assert.Equal(originalBefore[path], paused[path]);
            Assert.Equal(42, GetPrivateField<GameLoop>(parent, "_gameLoop").TurnNumber);
            Assert.False(GetPrivateFieldValue<bool>(parent, "_inGame"));
            lease.EnsureNoPendingLocalDecision();
        } // No parent input/read/write lease or participating caller survives launch.
        var context = new JsonObject
        {
            ["parentProcessId"] = Environment.ProcessId, ["sessionId"] = original.SessionId,
            ["requestId"] = original.RequestId, ["snapshotToken"] = original.SnapshotToken,
            ["requestFingerprint"] = requestFingerprint, ["scene"] = scene, ["expectedPhase"] = expectedPhase, ["expectedWorldCycles"] = expectedWorldCycles,
            ["refusalScenario"] = refusalScenario,
            ["generationSha256"] = MortalArchiveHash(paused["$sessionGeneration"]!),
            ["binaryPins"] = JsonSerializer.SerializeToNode(MortalArchiveBinaryPins()),
            ["pausedImages"] = JsonSerializer.SerializeToNode(paused.ToDictionary(pair => pair.Key,
                pair => pair.Value is null ? null : MortalArchiveHash(pair.Value), StringComparer.Ordinal))
        };
        // Context contains only assertion scalars/digests, never private authority,
        // complete request graphs, original bytes, cached plans or phase capability.
        var contextPath = Path.Combine(_rootPath, "joint-applied-cold-context.json");
        var output = Path.Combine(_rootPath, "joint-applied-cold-result.json");
        await File.WriteAllTextAsync(contextPath, context.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        Exception? failure = null;
        try
        {
            failure = await Record.ExceptionAsync(() => RunMortalArchiveChildAsync(
                "engine-mortal-joint-applied-cold", _fs.BasePath, output, refusalScenario is not null ? 2 : expectedPhase == "applied" ? 0 : 1, contextPath));
        }
        finally
        {
            // Retain all available managed output/full drained log before the
            // parent SOURCE root's ordinary owned cleanup, including timeout.
            WritePhysicalInjuryDiagnostic("fr063_joint_applied_cold_parent_outcome", new
            {
                ParentProcessId = Environment.ProcessId, Context = context, Failure = failure?.ToString(),
                Child = File.Exists(output) ? await File.ReadAllTextAsync(output) : null,
                FullChildLog = File.Exists(output + ".log") ? await File.ReadAllTextAsync(output + ".log") : null,
                PhysicalAfter = MortalArchiveDigests(await CaptureMortalArchiveImagesAsync(_fs,
                    paused.Keys.Where(path => path != "$sessionGeneration")))
            });
        }
        Assert.Null(failure);
        var child = ParseJsonObjectBytes(await File.ReadAllBytesAsync(output));
        Assert.NotEqual(Environment.ProcessId, child["processId"]!.GetValue<int>());
        Assert.True(child["actualCallerEntered"]!.GetValue<bool>());
        Assert.Equal(refusalScenario is null, child["completed"]!.GetValue<bool>());
        Assert.Equal(refusalScenario is not null, child["refusalVerified"]!.GetValue<bool>());
        Assert.Null(child["failure"]);
    }

    public static async Task WriteMortalJointAppliedColdProbeAsync(string root, string output, int scenario, string contextPath)
    {
        Assert.InRange(scenario, 0, 2);
        var context = JsonNode.Parse(await File.ReadAllTextAsync(contextPath))!.AsObject();
        using var factory = new GameEngineTurnLifecycleTests();
        using var factoryOwned = new CleanupOwnedFixture(factory._rootPath, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var input = new QueuedConsoleInputSource([]);
        var actualCallerEntered = false;
        var engine = factory.CreateGameEngine(input, settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            fileSystem: files, finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = checkpoint =>
                {
                    if (checkpoint == SessionFinalizationCheckpoint.LateTerminalAndIdleOperationBound) actualCallerEntered = true;
                    return Task.CompletedTask;
                }
            });
        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_joint_applied_child_entry", Observation = new
        { ProcessId = Environment.ProcessId, ParentProcessId = context["parentProcessId"]!.GetValue<int>(),
            BorrowedRoot = root, AuxiliaryFactoryRoot = factory._rootPath, Arguments = Environment.GetCommandLineArgs(),
            DependencyContext = MortalArchiveDependencyContext(), LoadedDependencies = MortalArchiveLoadedDependencyBindings() } }));
        Exception? failure = null;
        bool? handled = null, secondIdle = null;
        var completed = false;
        var refusalVerified = false;
        var refusalScenario = context["refusalScenario"]?.GetValue<string>();
        Dictionary<string, byte[]?>? intactBeforeTamper = null;
        Dictionary<string, byte[]?>? before = null, after = null, repeated = null;
        PendingTurnSnapshotReadAuthority? original = null;
        byte[]? storyBefore = null;
        try
        {
            Assert.NotEqual(Environment.ProcessId, context["parentProcessId"]!.GetValue<int>());
            AssertMortalArchiveChildBinaries(context);
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                before = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                foreach (var pair in context["pausedImages"]!.AsObject())
                    Assert.Equal(pair.Value?.GetValue<string>(), before.TryGetValue(pair.Key, out var image) && image is not null ? MortalArchiveHash(image) : null);
                Assert.Equal(context["generationSha256"]!.GetValue<string>(), MortalArchiveHash(files.ReadLocalGenerationSnapshot(lease).Bytes));
                var read = PendingTurnSnapshotReader.ReadCurrent(files, lease,
                    ["game_state/meta/soul_state.json", WoundHistoryState.HistoryPath, QteStoryPath]);
                Assert.True(read.Success, DescribeValidationIssues(read.Issues));
                original = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
                Assert.Equal(context["sessionId"]!.GetValue<string>(), original.SessionId);
                Assert.Equal(context["requestId"]!.GetValue<string>(), original.RequestId);
                Assert.Equal(context["snapshotToken"]!.GetValue<string>(), original.SnapshotToken);
                Assert.Equal(43, original.TurnNumber);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(files, lease));
                var observedMarker = ParseJsonObjectBytes(Assert.IsType<byte[]>(before[MortalQteDecisionPublication.StatePath]));
                Assert.Equal(scenario == 1 ? "unapplied" : "applied", observedMarker["progression"]!["phase"]!.GetValue<string>());
                Assert.Equal(context["expectedPhase"]!.GetValue<string>(), observedMarker["progression"]!["phase"]!.GetValue<string>());
                var actualRequest = StrictJsonAuthority.Deserialize<TurnRequest>(CanonicalJsonUtf8.DecodeOneOptionalBom(before["input/turn_request.json"]!),
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "fresh child actual original progression control");
                Assert.NotNull(actualRequest); Assert.NotNull(actualRequest!.ProgressionControl);
                var cycles = context["expectedWorldCycles"]!.GetValue<int>();
                Assert.Equal(cycles, actualRequest.ProgressionControl!.WorldCyclesAlreadyPendingBeforeTurn);
                Assert.Equal(0, actualRequest.ProgressionControl.FactionCyclesAlreadyPendingBeforeTurn);
                if (scenario != 1)
                {
                    Assert.Null(await files.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath));
                    if (cycles > 0)
                    {
                        Assert.True(observedMarker["progression"]!["reportBefore"]!["exists"]!.GetValue<bool>());
                        var frozenReport = ParseJsonObjectBytes(Convert.FromBase64String(observedMarker["progression"]!["reportBefore"]!["bytes"]!.GetValue<string>()))["progressionProcessingReport"]!;
                        Assert.Equal(original.SessionId, frozenReport["sessionId"]!.GetValue<string>());
                        Assert.Equal(original.RequestId, frozenReport["requestId"]!.GetValue<string>());
                        Assert.Equal(original.TurnNumber, frozenReport["turnNumber"]!.GetValue<int>());
                        Assert.Equal(cycles, frozenReport["worldCyclesProcessed"]!.GetValue<int>());
                        Assert.Equal(checked(actualRequest.ProgressionControl.LastWorldSimulationTimeInMinutes + cycles * actualRequest.ProgressionControl.WorldCycleMinutes),
                            frozenReport["newLastWorldSimulationTimeInMinutes"]!.GetValue<int>());
                        Assert.False(observedMarker["progression"]!["reportAfter"]!["exists"]!.GetValue<bool>());
                    }
                }
                storyBefore = await files.ReadFileBytesAsync(lease, QteStoryPath);
                Assert.Equal(original.ReadRequiredBytes(QteStoryPath), storyBefore);
                await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateReadOnlyAsync(lease);
                Assert.Equal(42, GetPrivateField<StateManager>(engine, "_stateManager").CurrentState.TurnNumber);
                GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(original.SessionId, 42);
                await AssertConfirmedClaimReadImagesAsync(files, lease, before);
                if (scenario == 2)
                {
                    // All official original/control/phase and fresh-cache prerequisites
                    // above precede the corruption. No authority helper runs afterward.
                    Assert.NotNull(refusalScenario); Assert.Equal(1, cycles);
                    intactBeforeTamper = before;
                    await ApplyJointCompletionRefusalTamperAsync(files, lease, refusalScenario!);
                    before = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                    Assert.Equal(intactBeforeTamper["$sessionGeneration"], before["$sessionGeneration"]);
                }
                lease.EnsureNoPendingLocalDecision();
            }
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_joint_applied_child_before_actual_owner", Observation = new
            { Environment.ProcessId, original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber,
                FreshCommonCacheEmpty = true, OriginalFromOfficialReader = true, RefusalScenario = refusalScenario,
                IntactBeforeTamper = intactBeforeTamper is null ? null : MortalArchiveDigests(intactBeforeTamper),
                Before = MortalArchiveDigests(before) } }));
            // The first owning call is the real late lifecycle. No Restore,
            // Export, correction, reducer or Compose has run in this process.
            handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            if (scenario == 2)
            {
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                {
                    after = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                    await AssertConfirmedClaimReadImagesAsync(files, lease, before!);
                    Assert.Equal(storyBefore, after[QteStoryPath]);
                    Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
                    foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json",
                                 LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
                    {
                        var expectedRetained = refusalScenario == "input_control" && path == "input/turn_request.json"
                            ? before![path] : intactBeforeTamper![path];
                        Assert.Equal(expectedRetained, after[path]);
                    }
                }
                // The boolean means a blocked attempt, after complete preservation.
                Assert.True(actualCallerEntered); Assert.True(handled);
                Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
                refusalVerified = true;
                return;
            }
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                after = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                var history = WoundHistoryState.Parse(await files.ReadFileAsync(lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
                Assert.True(history.IsValid, DescribeValidationIssues(history.Issues));
                var prior = WoundHistoryState.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(original.ReadRequiredBytes(WoundHistoryState.HistoryPath)), WoundHistoryState.HistoryPath);
                Assert.True(prior.IsValid, DescribeValidationIssues(prior.Issues));
                var rows = history.State!.Transitions.Skip(prior.State!.Transitions.Count).ToArray();
                Assert.Equal(2, rows.Length); Assert.Equal("create", rows[0].Kind); Assert.Equal("treat", rows[1].Kind);
                Assert.Equal(prior.State.NextOrdinal, rows[0].Ordinal); Assert.Equal(prior.State.NextOrdinal + 1, rows[1].Ordinal);
                Assert.Equal(context["requestFingerprint"]!.GetValue<string>(), rows[1].TreatmentResult!.Request.RequestFingerprint);
                var definitions = ResourceDefinitionCatalog.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath), false);
                Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
                var state = ResourceStateContract.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
                var resources = ResourceHistoryState.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
                Assert.True(state.IsValid, DescribeValidationIssues(state.Issues)); Assert.True(resources.IsValid, DescribeValidationIssues(resources.Issues));
                Assert.Empty(resources.History!.ValidateStateAgreement(state.Ledger!));
                var spend = Assert.Single(resources.History.Transitions, row => row.Turn == 43 && row.Coordinate.ResourceKey == "energy" && row.Operation == ResourceTransitionOperation.Spend);
                Assert.Equal(2m, spend.AppliedAmount); Assert.Equal(2m, spend.BeforeState!.Current); Assert.Equal(0m, spend.AfterState!.Current);
                var damage = Assert.Single(resources.History.Transitions, row => row.Turn == 43 && row.Coordinate.ResourceKey == "health");
                Assert.Equal(3m, damage.AppliedAmount); Assert.Equal(5m, damage.BeforeState!.Current); Assert.Equal(2m, damage.AfterState!.Current);
                Assert.Equal(0m, Assert.Single(state.Ledger!.Entries, entry => entry.Coordinate.ResourceKey == "energy").Current);
                Assert.Equal(2m, Assert.Single(state.Ledger.Entries, entry => entry.Coordinate.ResourceKey == "health").Current);
                var marker = ParseJsonObjectBytes(Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, MortalQteDecisionPublication.StatePath)));
                Assert.Equal("applied", marker["progression"]!["phase"]!.GetValue<string>());
                foreach (var path in new[] { ResourceMaterializationContract.StatePath,
                             ResourceMaterializationContract.HistoryPath, WoundHistoryState.HistoryPath, WoundIdentityState.StatePath,
                             WoundCarrierCatalog.PlayerPath, EffectIdentityState.StatePath, EffectCarrierCatalog.PlayerPath,
                             MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath })
                    Assert.Equal(before[path], after[path]); // Neither cold contour redoes finalized mechanics.
                var previousMarker = ParseJsonObjectBytes(Assert.IsType<byte[]>(before[MortalQteDecisionPublication.StatePath]));
                var previousPhase = Assert.IsType<JsonObject>(previousMarker["progression"]);
                var appliedPhase = Assert.IsType<JsonObject>(marker["progression"]);
                Assert.True(JsonNode.DeepEquals(previousPhase["sourceGuards"], appliedPhase["sourceGuards"]));
                foreach (var image in new[] { "scheduleBefore", "reportBefore", "scheduleAfter", "reportAfter" })
                    Assert.True(JsonNode.DeepEquals(previousPhase[image], appliedPhase[image]));
                Assert.Equal(previousPhase["mutationFingerprint"]!.GetValue<string>(), appliedPhase["mutationFingerprint"]!.GetValue<string>());
                if (scenario == 0)
                {
                    foreach (var path in new[] { MortalQteDecisionPublication.StatePath, ProgressionScheduleService.SchedulePath,
                                 CharacteristicsService.JointDerivedProjectionPath }) Assert.Equal(before[path], after[path]);
                }
                else
                {
                    var originalRequest = StrictJsonAuthority.Deserialize<BookOfEternityClient.Models.TurnRequest>(
                        CanonicalJsonUtf8.DecodeOneOptionalBom(before["input/turn_request.json"]!),
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual unapplied cold original control");
                    Assert.NotNull(originalRequest); Assert.NotNull(originalRequest!.ProgressionControl);
                    var control = originalRequest.ProgressionControl!;
                    Assert.Equal(0, control.WorldCyclesAlreadyPendingBeforeTurn); Assert.Equal(0, control.FactionCyclesAlreadyPendingBeforeTurn);
                    var scheduleBefore = Convert.FromBase64String(previousPhase["scheduleBefore"]!["bytes"]!.GetValue<string>());
                    Assert.Equal(scheduleBefore, before[ProgressionScheduleService.SchedulePath]);
                    var scheduleAfter = Convert.FromBase64String(appliedPhase["scheduleAfter"]!["bytes"]!.GetValue<string>());
                    Assert.Equal(scheduleAfter, after[ProgressionScheduleService.SchedulePath]);
                    var expected = ParseJsonObjectBytes(scheduleBefore);
                    expected["currentWorldTimeInMinutes"] = control.CurrentWorldTimeInMinutes;
                    expected["lastWorldSimulationTimeInMinutes"] = control.LastWorldSimulationTimeInMinutes;
                    expected["lastFactionSimulationTimeInMinutes"] = control.LastFactionSimulationTimeInMinutes;
                    expected["pendingWorldCycles"] = 0; expected["pendingFactionCycles"] = 0;
                    expected["lastUpdatedUtc"] = appliedPhase["preparedAtUtc"]!.GetValue<string>();
                    Assert.True(JsonNode.DeepEquals(expected, ParseJsonObjectBytes(scheduleAfter)));
                    Assert.NotNull(await files.ReadFileBytesAsync(lease, CharacteristicsService.JointDerivedProjectionPath));
                }
                Assert.Null(await files.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath));
                var story = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, QteStoryPath));
                Assert.True(story.AsSpan().StartsWith(storyBefore!));
                var suffix = Encoding.UTF8.GetString(story.AsSpan(storyBefore!.Length)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var row = StrictJsonAuthority.Deserialize<StoryEntry>(Assert.Single(suffix), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "cold exact joint story");
                Assert.NotNull(row); Assert.Equal(43, row!.Turn); Assert.Equal("Осмотреть ушиб.", row.Player);
                Assert.Equal(context["scene"]!.GetValue<string>(), row.Narrative);
                Assert.Equal(43, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
                Assert.True(GetPrivateFieldValue<bool>(engine, "_inGame"));
                Assert.Equal(row.Narrative, GetPrivateField<GameResponse>(engine, "_lastResponse").Response);
                Assert.Equal(before["$sessionGeneration"], after["$sessionGeneration"]);
                foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                             PendingTurnSnapshotAuthority.AuthorityPath, AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                             MortalWoundOpportunityAdapter.DecisionPath, "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json" })
                    Assert.Null(await files.ReadFileBytesAsync(lease, path));
            }
            Assert.True(actualCallerEntered); Assert.False(handled);
            secondIdle = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                repeated = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                await AssertConfirmedClaimReadImagesAsync(files, lease, after);
            }
            Assert.False(secondIdle); Assert.Equal(43, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            completed = true;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
            {
                processId = Environment.ProcessId, actualCallerEntered, handled, secondIdle, completed, refusalVerified, refusalScenario,
                AuxiliaryFactoryRoot = factory._rootPath, Root = root, BinaryPins = MortalArchiveBinaryPins(),
                DependencyContext = MortalArchiveDependencyContext(), LoadedDependencies = MortalArchiveLoadedDependencyBindings(),
                Original = original is null ? null : new { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber },
                actualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber, failure = failure?.ToString(),
                IntactBeforeTamper = intactBeforeTamper is null ? null : MortalArchiveDigests(intactBeforeTamper),
                Before = before is null ? null : MortalArchiveDigests(before), After = after is null ? null : MortalArchiveDigests(after),
                Repeated = repeated is null ? null : MortalArchiveDigests(repeated)
            }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
    }
}
