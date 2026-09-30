using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Collects genuine observed envelopes and saved selection evidence across warm and cold callers.
    /// </summary>
    private sealed class SpiritualStagedObservation
    {
        /// <summary>
        /// Gets the shared bounded phase diagnostics and observed request identities.
        /// </summary>
        internal DependentSpiritualGmObservation Phase { get; } = new();
        /// <summary>
        /// Gets or sets the actual initial dependent request.
        /// </summary>
        internal SpiritualWoundContinuationRequest? A { get; set; }
        /// <summary>
        /// Gets or sets the actual successor request after A acceptance.
        /// </summary>
        internal SpiritualWoundContinuationRequest? B { get; set; }
        /// <summary>
        /// Gets or sets the exact saved wound command observed at A.
        /// </summary>
        internal byte[]? Command { get; set; }
        /// <summary>
        /// Gets or sets the immutable staged decision fingerprint observed at A.
        /// </summary>
        internal string? DecisionFingerprint { get; set; }
        /// <summary>
        /// Signals that the file responder finished writing A and deliberately withheld Ready.
        /// </summary>
        internal int AppliedAWithoutReady;
        /// <summary>
        /// Gets or sets the count of observed stale A Ready deletions at B.
        /// </summary>
        internal int StaleReadyRejections { get; set; }
    }

    /// <summary>
    /// Observes real engine requests and uses a separate filesystem to keep interruption hooks exclusive to the engine.
    /// </summary>
    /// <param name="fs">
    /// Same physical session through a filesystem without interruption hooks.
    /// </param>
    /// <param name="input">
    /// Phase input used only to release a failed callback.
    /// </param>
    /// <param name="original">
    /// Actual signed turn request.
    /// </param>
    /// <param name="originalDraft">
    /// Frozen original conflict used to author exact A and B candidates.
    /// </param>
    /// <param name="shared">
    /// Cross-phase observations and saved selection evidence.
    /// </param>
    /// <param name="worker">
    /// Whether responses belong to real worker processes and this callback only observes.
    /// </param>
    /// <param name="cut">
    /// Warm interruption contour, or none during normal execution and recovery.
    /// </param>
    /// <param name="staleA">
    /// Whether to submit one stale A Ready at B before authoring the valid B correction.
    /// </param>
    /// <param name="cancellationToken">
    /// Stops polling after the bounded caller ends.
    /// </param>
    /// <returns>
    /// A task completing after polling stops or a retained callback failure releases the engine.
    /// </returns>
    private static Task ObserveSpiritualStagedRequestsAsync(FileSystemManager fs, QueuedConsoleInputSource input,
        TurnRequest original, JsonObject originalDraft, SpiritualStagedObservation shared, bool worker,
        string cut, bool staleA, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        const string repairPath = "game_state/control/validation_repair_request.json";
        const string readyPath = "game_state/control/validation_repair_ready.json";
        var handled = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytes = await ReadSpiritualStagedRequestBytesAsync(fs, cancellationToken);
                if (bytes is null) { await Task.Delay(25, cancellationToken); continue; }
                var report = ParseDependentSpiritualBytes(bytes);
                shared.Phase.LastRepair = report.ToJsonString();
                var request = SpiritualWoundContinuationProtocol.ReadRequest(JsonSerializer.SerializeToElement(report["spiritualWoundContinuation"]));
                Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(request));
                Assert.Equal(original.SessionId, report["sessionId"]!.GetValue<string>());
                Assert.Equal(original.RequestId, report["requestId"]!.GetValue<string>());
                Assert.Equal(42, report["turnNumber"]!.GetValue<int>());
                shared.Phase.Requests.TryAdd(request.ContinuationId, request);
                if (!handled.Add(request.ContinuationId)) { await Task.Delay(25, cancellationToken); continue; }
                var stage = request.Phase == "decision" ? "decision" : request.DependentDraftFields.Any(
                    field => field.JsonPointer.Contains("/exchangeLog/2/", StringComparison.Ordinal)) ? "b" : "a";
                if (stage == "a")
                {
                    shared.A ??= request;
                    Assert.Equal(JsonSerializer.Serialize(shared.A), JsonSerializer.Serialize(request));
                    Assert.All(request.DependentDraftFields, field => Assert.StartsWith("/activeConflict/exchangeLog/1/diceAudit/", field.JsonPointer));
                }
                if (stage == "b")
                {
                    shared.B ??= request;
                    Assert.Equal(JsonSerializer.Serialize(shared.B), JsonSerializer.Serialize(request));
                    Assert.All(request.DependentDraftFields, field => Assert.StartsWith("/activeConflict/exchangeLog/2/diceAudit/", field.JsonPointer));
                }
                if (stage != "decision")
                {
                    Assert.Null(request.Offer);
                    var command = await File.ReadAllBytesAsync(fs.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath), cancellationToken);
                    shared.Command ??= command;
                    Assert.Equal(shared.Command, command);
                    var saved = ParseDependentSpiritualBytes(await File.ReadAllBytesAsync(
                        fs.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath), cancellationToken))["checkpoint"]!["pendingSubmission"]!["stagedDecision"]!;
                    shared.DecisionFingerprint ??= saved["decisionFingerprint"]!.GetValue<string>();
                    Assert.Equal(shared.DecisionFingerprint, saved["decisionFingerprint"]!.GetValue<string>());
                    Assert.Equal("materialize", saved["decision"]!.GetValue<string>());
                    Assert.False(File.Exists(fs.ResolvePath(SpiritualWoundOpportunityReceiptState.StatePath)));
                }
                if (worker || cut == "b_publish" && stage == "b")
                { await Task.Delay(25, cancellationToken); continue; }
                // An already-written warm Ready belongs to the cold engine; do not replace it.
                if (File.Exists(fs.ResolvePath(readyPath))) { await Task.Delay(25, cancellationToken); continue; }
                if (staleA && stage == "b")
                {
                    var held = new Dictionary<string, byte[]>();
                    foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                        SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath, AfterlifeSpiritualConflictState.StatePath })
                        held[path] = (await fs.ReadFileBytesAsync(path))!;
                    await WriteSpiritualStagedReadyAsync(fs, original, shared.A!, []);
                    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
                    while (File.Exists(fs.ResolvePath(readyPath)) && DateTime.UtcNow < deadline)
                        await Task.Delay(25, cancellationToken);
                    Assert.False(File.Exists(fs.ResolvePath(readyPath)), "Stale A Ready was not rejected at B.");
                    foreach (var pair in held) Assert.Equal(pair.Value, await fs.ReadFileBytesAsync(pair.Key));
                    var retainedB = ParseDependentSpiritualBytes((await fs.ReadFileBytesAsync(repairPath))!);
                    Assert.Equal(request.ContinuationId, retainedB["spiritualWoundContinuation"]!["continuationId"]!.GetValue<string>());
                    // Ready deletion precedes the engine's fresh B replay. Wait for its actual
                    // retry publication rather than competing with that replay's write lease.
                    JsonObject? rejection = null;
                    while (rejection is null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var retryBytes = await ReadSpiritualStagedRequestBytesAsync(fs, cancellationToken);
                        if (retryBytes is not null && !retryBytes.AsSpan().SequenceEqual(bytes))
                        {
                            var retry = ParseDependentSpiritualBytes(retryBytes);
                            shared.Phase.LastRepair = retry.ToJsonString();
                            if (retry["revalidationAttempt"]!.GetValue<int>() > report["revalidationAttempt"]!.GetValue<int>() &&
                                retry["errors"]!.AsArray().Any(error =>
                                    error?["code"]?.GetValue<string>() == "spiritual_continuation_request_changed"))
                                rejection = retry;
                        }
                        if (rejection is null) await Task.Delay(25, cancellationToken);
                    }
                    Assert.Equal(original.SessionId, rejection["sessionId"]!.GetValue<string>());
                    Assert.Equal(original.RequestId, rejection["requestId"]!.GetValue<string>());
                    Assert.Equal(original.TurnNumber, rejection["turnNumber"]!.GetValue<int>());
                    var retriedRequest = SpiritualWoundContinuationProtocol.ReadRequest(
                        JsonSerializer.SerializeToElement(rejection[SpiritualWoundContinuationProtocol.EnvelopeName]));
                    Assert.Equal(JsonSerializer.Serialize(request), JsonSerializer.Serialize(retriedRequest));
                    await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync(cancellationToken: cancellationToken))
                    {
                        Assert.False(fs.FileExists(lease, readyPath));
                        foreach (var pair in held) Assert.Equal(pair.Value, await fs.ReadFileBytesAsync(lease, pair.Key));
                    }
                    shared.StaleReadyRejections++;
                }
                JsonElement[] decisions = [];
                if (stage == "decision")
                {
                    var narrative = ParseDependentSpiritualBytes((await fs.ReadFileBytesAsync("output/narrative_response.json"))!);
                    narrative["response"] = "Чужое давление надломило волю души. Во втором и третьем обменах оба сохраняют давление; нового вреда нет.";
                    narrative["timestamp"] = DateTime.UtcNow.ToString("O");
                    await fs.WriteFileAtomicAsync("output/narrative_response.json", narrative.ToJsonString());
                    decisions = [AfterlifeResourceCutoverTests.CreateSpiritualStagedPlayerDecision(request.Offer!.OpportunityRef)];
                }
                else
                {
                    var candidate = AfterlifeResourceCutoverTests.CreateSpiritualStagedCorrectionA(originalDraft);
                    if (stage == "b") candidate = AfterlifeResourceCutoverTests.CreateSpiritualStagedCorrectionB(candidate);
                    await fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
                    if (cut == "a_apply" && stage == "a")
                    {
                        Volatile.Write(ref shared.AppliedAWithoutReady, 1);
                        continue;
                    }
                }
                await WriteSpiritualStagedReadyAsync(fs, original, request, decisions);
                shared.Phase.FileResponses++;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            shared.Phase.Failure = error;
            input.Enqueue(Key(ConsoleKey.Escape));
        }
    });

    /// <summary>
    /// Reads a published repair request cooperatively without acquiring the engine's canonical lease.
    /// </summary>
    /// <param name="fs">
    /// Filesystem for the observed physical session.
    /// </param>
    /// <param name="cancellationToken">
    /// Existing caller deadline controlling the bounded observer.
    /// </param>
    /// <returns>
    /// Exact request bytes, or <see langword="null"/> while the file is absent or temporarily shared exclusively.
    /// </returns>
    private static async Task<byte[]?> ReadSpiritualStagedRequestBytesAsync(FileSystemManager fs,
        CancellationToken cancellationToken)
    {
        try
        {
            var path = fs.ResolvePath("game_state/control/validation_repair_request.json");
            if (!File.Exists(path)) return null;
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            using var content = new MemoryStream();
            await stream.CopyToAsync(content, cancellationToken);
            return content.ToArray();
        }
        catch (FileNotFoundException) { return null; }
        catch (IOException error) when ((error.HResult & 0xFFFF) is 32 or 33) { return null; }
    }

    /// <summary>
    /// Writes the existing closed Ready shape using only the actual observed request identity.
    /// </summary>
    /// <param name="fs">
    /// Responder filesystem for the original physical session.
    /// </param>
    /// <param name="original">
    /// Signed original turn metadata.
    /// </param>
    /// <param name="request">
    /// Actual request answered by this Ready.
    /// </param>
    /// <param name="decisions">
    /// One initial materialization or an empty dependent response.
    /// </param>
    /// <returns>
    /// The atomic Ready write task.
    /// </returns>
    private static Task WriteSpiritualStagedReadyAsync(FileSystemManager fs, TurnRequest original,
        SpiritualWoundContinuationRequest request, JsonElement[] decisions) => fs.WriteFileAtomicAsync(
        "game_state/control/validation_repair_ready.json", JsonSerializer.Serialize(new
        {
            sessionId = original.SessionId, requestId = original.RequestId, turnNumber = original.TurnNumber,
            timestamp = DateTime.UtcNow.ToString("O"), status = "success",
            spiritualWoundContinuation = new { schemaVersion = 1, continuationId = request.ContinuationId, woundDecisions = decisions }
        }));

    /// <summary>
    /// Reads exact durable controls, immutable inputs and canonical publication surfaces without nested leases or hooks.
    /// </summary>
    /// <param name="context">
    /// The actual physical fixture; callers arrange a stable boundary or hold the engine's existing lease.
    /// </param>
    /// <returns>
    /// Exact bytes by canonical path, including explicit absence.
    /// </returns>
    private static async Task<Dictionary<string, byte[]?>> ReadSpiritualStagedPhysicalImagesAsync(ResourceMaterializationTestContext context)
    {
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json",
            "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json",
            "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
            SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
            AcceptedMechanicsPlan.WoundCommandPath, AfterlifeSpiritualConflictState.StatePath,
            AfterlifeEntityProfileState.StatePath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
            EffectIdentityState.StatePath, SpiritualWoundOpportunityReceiptState.StatePath, "stories/chaos_sea.jsonl" })
        {
            var physical = context.FileSystem.ResolvePath(path);
            result[path] = File.Exists(physical) ? await File.ReadAllBytesAsync(physical) : null;
        }
        return result;
    }
}
