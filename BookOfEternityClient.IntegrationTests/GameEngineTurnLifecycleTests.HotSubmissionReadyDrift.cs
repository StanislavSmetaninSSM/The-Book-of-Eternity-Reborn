using System.Reflection;
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
    public Task HotSubmission_LegacyUnchangedReadyHasGenuineOrdinaryStage() =>
        RunHotSubmissionReadyDriftAsync(replaceReady: false, requireNewDiagnostic: false);

    [Fact]
    public Task HotSubmission_LegacyAcceptedReadyDoesNotProveUnchangedSource() =>
        RunHotSubmissionReadyDriftAsync(replaceReady: true, requireNewDiagnostic: false);

    [Fact]
    public Task HotSubmission_ReadyReplacedAfterReadCannotIssueConsumedCustody() =>
        RunHotSubmissionReadyDriftAsync(replaceReady: true, requireNewDiagnostic: true);

    private async Task RunHotSubmissionReadyDriftAsync(bool replaceReady, bool requireNewDiagnostic)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        const string readyPath = "game_state/control/validation_repair_ready.json";
        const string repairPath = "game_state/control/validation_repair_request.json";
        const string manifestPath = LiveTurnPreparationService.PendingTurnSnapshotManifestPath;
        const string issueCode = "hot_submission_ready_fixture";
        var armed = false;
        var readyOpened = false;
        var replacements = 0;
        var gmReadyPublication = new AsyncLocal<bool>();
        var ownerReadOrder = new List<string>();
        byte[]? observedReady = null, replacementReady = null, readyBeforeDelete = null;
        FileSystemManager? actualFiles = null;
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = path =>
                {
                    // The GM writer's own publication readback is not the
                    // later real Wait owner's Ready read. This is test scheduling
                    // state only, never an engine flag or source authority.
                    if (!armed || gmReadyPublication.Value) return Task.CompletedTask;
                    if (path == readyPath && !readyOpened)
                    {
                        readyOpened = true;
                        observedReady = File.ReadAllBytes(actualFiles!.ResolvePath(readyPath));
                    }
                    if (readyOpened && (path == readyPath || path == manifestPath)) ownerReadOrder.Add(path);
                    // This hook is BEFORE the opened file's bytes are copied.
                    // Mutate the EARLIER Ready only when the LATER manifest opens.
                    if (replaceReady && readyOpened && path == manifestPath && replacements == 0)
                    {
                        var replacement = ParseJsonObjectBytes(Assert.IsType<byte[]>(observedReady));
                        replacement["requestId"] = "replacement_after_actual_ready_read";
                        replacement["note"] = "Different public Ready after the real initial read.";
                        replacementReady = System.Text.Encoding.UTF8.GetBytes(replacement.ToJsonString());
                        File.WriteAllBytes(actualFiles!.ResolvePath(readyPath), replacementReady);
                        replacements++;
                    }
                    return Task.CompletedTask;
                },
                BeforeCanonicalMutationAsync = path =>
                {
                    if (armed && !gmReadyPublication.Value && readyOpened && path == readyPath && File.Exists(actualFiles!.ResolvePath(readyPath)))
                        readyBeforeDelete = File.ReadAllBytes(actualFiles.ResolvePath(readyPath));
                    return Task.CompletedTask;
                }
            });
        actualFiles = files;
        CopyDirectory(TestRepoPaths.BaseSessionRoot, files.GameSessionPath);
        await files.WriteFileAtomicAsync("game_state/misc/characteristics.json", JsonSerializer.Serialize(
            Characteristics.All.ToDictionary(name => name, _ => 20), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await files.WriteFileAtomicAsync("game_state/player/experience.json",
            "{\"level\":1,\"experience\":0,\"experienceForNextLevel\":100}");
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(2, 42, 20, 20, 20, 20, 20);
        Assert.True(resources.IsValid, JsonSerializer.Serialize(resources.Issues));
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(resources.Definitions,
            path => files.ReadFileAsync(path), resources.State, resources.History, CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(authority.IsValid, JsonSerializer.Serialize(authority.Issues));
        await files.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, authority.CanonicalAuthorityJson!);
        var input = new QueuedConsoleInputSource([]);
        var engine = CreateGameEngine(input, settings =>
        { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; }, fileSystem: files);
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession("session_hot_ready_custody", 42);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(files, async () =>
        {
            var preparation = Assert.IsAssignableFrom<object>(await InvokePrivateTaskNullableResultAsync(
                engine, "StagePlayerTurnAsync", "Спокойно осмотреться.", null, null));
            // Read only the actual returned preparation. No issuer/context field is seeded.
            var request = Assert.IsType<TurnRequest>(preparation.GetType().GetProperty("Request")!.GetValue(preparation));
            var backup = Assert.IsAssignableFrom<object>(preparation.GetType().GetProperty("Backup")!.GetValue(preparation));
            var generation = Assert.IsType<string>(preparation.GetType().GetProperty("Generation")!.GetValue(preparation));
            PendingTurnSnapshotReadAuthority original;
            var sourceImages = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            var sourcePaths = HotJointInputPaths.Concat(new[]
            {
                manifestPath, PendingTurnSnapshotAuthority.AuthorityPath, LiveTurnPreparationService.TurnRequestPath,
                "ready/turn_complete.json", "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json",
                ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                ProgressionScheduleService.SchedulePath, "game_state/wounds/wound_history.json"
            }).Distinct(StringComparer.Ordinal).ToArray();
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                var read = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease, ["game_state/meta/soul_state.json"]);
                Assert.True(read.Success, JsonSerializer.Serialize(read.Issues));
                original = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
                read.Evidence!.RequireCurrent(files, lease, original);
                Assert.Equal(request.SessionId, original.SessionId); Assert.Equal(request.RequestId, original.RequestId);
                Assert.Equal(43, original.TurnNumber); Assert.Equal("mortal_world", original.Realm);
                Assert.Equal(generation, files.ReadLocalGenerationSnapshot(lease).Binding.Id);
                Assert.Null(await files.ReadFileBytesAsync(lease, readyPath));
                foreach (var path in sourcePaths) sourceImages.Add(path, await files.ReadFileBytesAsync(lease, path));
            }
            using var publisherStop = new CancellationTokenSource();
            using var deadlineStop = new CancellationTokenSource();
            Task? gmRepair = null;
            Task<bool>? repairWait = null;
            var failures = new List<Exception>();
            var accepted = false;
            try
            {
                var deadline = Task.Delay(TimeSpan.FromSeconds(30), deadlineStop.Token);
                gmRepair = Task.Run(async () =>
                {
                    var requestDeadline = DateTime.UtcNow.AddSeconds(10);
                    while (true)
                    {
                        publisherStop.Token.ThrowIfCancellationRequested();
                        var text = await files.ReadFileAsync(repairPath);
                        if (text?.Contains(issueCode, StringComparison.Ordinal) == true) break;
                        Assert.True(DateTime.UtcNow < requestDeadline, "The real repair request was not published.");
                        await Task.Delay(25, publisherStop.Token);
                    }
                    publisherStop.Token.ThrowIfCancellationRequested();
                    gmReadyPublication.Value = true;
                    armed = true;
                    try
                    {
                        await files.WriteFileAtomicAsync(readyPath, JsonSerializer.Serialize(new
                        {
                            sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
                            updatedAtUtc = "2026-10-16T00:00:00Z", note = "Initial public Ready."
                        }));
                    }
                    finally { gmReadyPublication.Value = false; }
                });
                repairWait = InvokePrivateAsync<bool>(engine, "WaitForContractRepairAsync", "обработки хода",
                    new List<ValidationIssue> { new("output/interface_updates.json", IssueSeverity.Error,
                        "Source-custody fixture repair; no semantic acceptance is claimed.", code: issueCode) },
                    1, backup, generation);
                var first = await Task.WhenAny(gmRepair, repairWait, deadline);
                // Surface a publisher fault immediately; finally requests actual
                // Wait cancellation through its existing console input owner.
                if (first == gmRepair) await gmRepair;
                if (first == deadline || await Task.WhenAny(repairWait, deadline) != repairWait)
                    throw new TimeoutException("The real repair Wait exceeded the fixture's protective 30s bound.");
                accepted = await repairWait;
            }
            catch (Exception failure) { failures.Add(failure); }
            finally
            {
                await publisherStop.CancelAsync();
                await deadlineStop.CancelAsync();
                if (repairWait is { IsCompleted: false }) input.Enqueue(Key(ConsoleKey.Escape));
                // Never dispose the participation/root while either task can
                // still read or publish. Cancellation cannot abandon an in-flight write.
                try { if (gmRepair is not null) await gmRepair; }
                catch (OperationCanceledException failure) when (failure.CancellationToken == publisherStop.Token) { }
                catch (Exception failure)
                {
                    if (!failures.Contains(failure)) failures.Add(failure);
                }
                try { if (repairWait is not null) await repairWait; }
                catch (Exception failure)
                {
                    if (!failures.Contains(failure)) failures.Add(failure);
                }
                armed = false;
            }
            if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException("Real repair fixture and task cleanup both failed.", failures);
            Assert.True(accepted); Assert.True(readyOpened);
            Assert.Equal(replaceReady ? 1 : 0, replacements);
            Assert.NotNull(observedReady); Assert.NotNull(readyBeforeDelete);
            Assert.Equal(request.RequestId, ParseJsonObjectBytes(observedReady!)["requestId"]!.GetValue<string>());
            Assert.Equal(readyPath, ownerReadOrder[0]);
            Assert.True(ownerReadOrder.IndexOf(manifestPath) > 0);
            Assert.Equal(replaceReady ? Assert.IsType<byte[]>(replacementReady) : observedReady, readyBeforeDelete);
            if (replaceReady)
            {
                Assert.False(observedReady!.AsSpan().SequenceEqual(replacementReady));
                Assert.Equal("replacement_after_actual_ready_read", ParseJsonObjectBytes(replacementReady!)["requestId"]!.GetValue<string>());
            }
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.Null(await files.ReadFileBytesAsync(lease, readyPath));
                Assert.Equal(generation, files.ReadLocalGenerationSnapshot(lease).Binding.Id);
                foreach (var image in sourceImages) Assert.Equal(image.Value, await files.ReadFileBytesAsync(lease, image.Key));
            }
            Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            var ledger = JsonNode.Parse((await files.ReadFileAsync("game_state/control/gm_trajectory_ledger.jsonl"))!.Trim())!.AsObject();
            Assert.Equal(request.RequestId, ledger["requestId"]!.GetValue<string>());
            Assert.Equal("correlated_repair_ready", ledger["validation"]!["acceptanceScope"]!.GetValue<string>());
            Assert.False(ledger["validation"]!["fullCanonicalStateAccepted"]!.GetValue<bool>());
            WritePhysicalInjuryDiagnostic("fr063_hot_ready_real_legacy_control_before_new_diagnostic", new
            {
                replaceReady, requireNewDiagnostic, accepted, replacements, Original = new
                { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber },
                Generation = generation, ObservedReady = observedReady, ReplacementReady = replacementReady,
                ReadyBeforeActualDelete = readyBeforeDelete, ReadyAbsentAfterOwnerReturned = true,
                CurrentTurn = 42, OwnerReadOrder = ownerReadOrder, SourceImages = sourceImages, NoOriginalOrPrivateFieldAuthored = true,
                SemanticAcceptanceClaimed = false
            });
            if (!requireNewDiagnostic) return true;
            var export = typeof(GameEngine).GetMethod("ExportHotOrdinarySubmissionDiagnostic",
                BindingFlags.Instance | BindingFlags.NonPublic, binder: null, Type.EmptyTypes, modifiers: null);
            Assert.NotNull(export); // Intended missing-owner RED, after the genuine drift/control cut.
            var diagnostic = Assert.IsType<JsonObject>(export!.Invoke(engine, null));
            var binding = Assert.IsType<JsonObject>(diagnostic["binding"]);
            Assert.True(binding["actualStageCompleted"]!.GetValue<bool>());
            Assert.Equal(generation, binding["generation"]!.GetValue<string>());
            Assert.Equal(original.RequestId, binding["requestId"]!.GetValue<string>());
            Assert.Equal(original.SnapshotToken, binding["snapshotToken"]!.GetValue<string>());
            var custody = Assert.IsType<JsonObject>(diagnostic["custody"]);
            Assert.Equal("Unavailable", custody["status"]!.GetValue<string>());
            Assert.Equal("SourceChanged", custody["reason"]!.GetValue<string>());
            Assert.True(custody["readyObservedBeforeDelete"]!.GetValue<bool>());
            Assert.True(custody["readyDeleteReturned"]!.GetValue<bool>());
            Assert.True(custody["readyReleaseReturned"]!.GetValue<bool>());
            Assert.False(diagnostic["hasS"]!.GetValue<bool>());
            Assert.False(diagnostic["hasB"]!.GetValue<bool>());
            Assert.False(diagnostic["hasM3"]!.GetValue<bool>());
            var frozen = diagnostic.DeepClone();
            custody["status"] = "ConsumedReadyMatched";
            Assert.True(JsonNode.DeepEquals(frozen, Assert.IsType<JsonObject>(export.Invoke(engine, null))));
            return true;
        });
    }
}
