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
    /// Authors only the current real binding request and keeps the saved decision exact across both replies.
    /// </summary>
    /// <param name="fs">
    /// Separate filesystem over the same physical session, without engine interruption hooks.
    /// </param>
    /// <param name="input">
    /// Engine input used to release a failed observer.
    /// </param>
    /// <param name="original">
    /// Signed original turn identity and dice.
    /// </param>
    /// <param name="originalDraft">
    /// Frozen successful two-exchange draft used to author bounded corrections.
    /// </param>
    /// <param name="observed">
    /// Actual request identities and saved-choice evidence retained across a cold restart.
    /// </param>
    /// <param name="strong">
    /// Selects force-binding arithmetic and its exact wound operation.
    /// </param>
    /// <param name="withholdFinal">
    /// Observes B without answering during the warm interruption phase.
    /// </param>
    /// <param name="token">
    /// Bounded phase cancellation controlled by the caller.
    /// </param>
    /// <returns>
    /// A task completing on cancellation or after reporting a callback failure to the engine.
    /// </returns>
    private static Task ObserveBindingLifecycleAsync(FileSystemManager fs, QueuedConsoleInputSource input,
        TurnRequest original, JsonObject originalDraft, SpiritualStagedObservation observed, bool strong,
        bool withholdFinal, CancellationToken token) => Task.Run(async () =>
    {
        var handled = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            while (!token.IsCancellationRequested)
            {
                var bytes = await ReadSpiritualStagedRequestBytesAsync(fs, token);
                if (bytes is null) { await Task.Delay(25, token); continue; }
                var report = ParseDependentSpiritualBytes(bytes);
                observed.Phase.LastRepair = report.ToJsonString();
                var request = SpiritualWoundContinuationProtocol.ReadRequest(
                    JsonSerializer.SerializeToElement(report[SpiritualWoundContinuationProtocol.EnvelopeName]));
                Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(request));
                Assert.Equal(original.SessionId, report["sessionId"]!.GetValue<string>());
                Assert.Equal(original.RequestId, report["requestId"]!.GetValue<string>());
                Assert.Equal(original.TurnNumber, report["turnNumber"]!.GetValue<int>());
                observed.Phase.Requests.TryAdd(request.ContinuationId, request);
                if (!handled.Add(request.ContinuationId)) { await Task.Delay(25, token); continue; }
                var isFinal = request.Phase == "dependent_draft" && request.DependentDraftFields.Count == 1 &&
                    request.DependentDraftFields[0].JsonPointer == "/activeConflict/controlState";
                if (request.Phase == "dependent_draft")
                {
                    Assert.Null(request.Offer);
                    if (isFinal)
                    {
                        observed.B ??= request;
                        Assert.Equal(JsonSerializer.Serialize(observed.B), JsonSerializer.Serialize(request));
                        Assert.NotEqual(observed.A!.ContinuationId, request.ContinuationId);
                    }
                    else
                    {
                        observed.A ??= request;
                        Assert.Equal(JsonSerializer.Serialize(observed.A), JsonSerializer.Serialize(request));
                        Assert.Equal(5, request.DependentDraftFields.Count);
                        Assert.All(request.DependentDraftFields, field => Assert.StartsWith("/activeConflict/exchangeLog/1/", field.JsonPointer));
                    }
                    var command = await File.ReadAllBytesAsync(fs.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath), token);
                    observed.Command ??= command;
                    Assert.Equal(observed.Command, command);
                    var saved = ParseDependentSpiritualBytes(await File.ReadAllBytesAsync(
                        fs.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath), token))["checkpoint"]!["pendingSubmission"]!;
                    var fingerprint = saved["stagedDecision"]!["decisionFingerprint"]!.GetValue<string>();
                    observed.DecisionFingerprint ??= fingerprint;
                    Assert.Equal(observed.DecisionFingerprint, fingerprint);
                    Assert.False(File.Exists(fs.ResolvePath(SpiritualWoundOpportunityReceiptState.StatePath)));
                    if (isFinal)
                    {
                        var progress = Assert.Single(saved["dependentDraftProgress"]!.AsArray())!;
                        Assert.Equal(observed.A!.ContinuationId, progress["acceptedContinuationId"]!.GetValue<string>());
                        if (withholdFinal) continue;
                    }
                }
                Assert.False(File.Exists(fs.ResolvePath("game_state/control/validation_repair_ready.json")));
                JsonElement[] decisions = [];
                if (request.Phase == "decision")
                {
                    decisions = [AfterlifeResourceCutoverTests.CreateBindingLifecycleDecision(request.Offer!.OpportunityRef, strong)];
                }
                else
                {
                    var corrected = AfterlifeResourceCutoverTests.CreateBindingLifecycleCorrectionA(originalDraft, strong);
                    if (isFinal)
                        corrected["activeConflict"]!["controlState"] = corrected["activeConflict"]!["exchangeLog"]![1]!["after"]!["controlState"]!.DeepClone();
                    await fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, corrected.ToJsonString());
                }
                await WriteSpiritualStagedReadyAsync(fs, original, request, decisions);
                observed.Phase.FileResponses++;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            observed.Phase.Failure = error;
            input.Enqueue(Key(ConsoleKey.Escape));
        }
    });

    /// <summary>
    /// Writes complete ordinary engine output with reasoning matching the two binding exchanges.
    /// </summary>
    /// <param name="context">
    /// Full isolated session after its original snapshot was signed.
    /// </param>
    /// <param name="request">
    /// Original turn identity and progression schedule.
    /// </param>
    /// <param name="strong">
    /// Selects the guardian's tier-one cost and force-binding operation.
    /// </param>
    /// <returns>
    /// A task completing after standard output, terminal signal and accurate reasoning are present.
    /// </returns>
    private static async Task WriteBindingLifecycleOutputsAsync(ResourceMaterializationTestContext context, TurnRequest request, bool strong)
    {
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var narrative = Assert.IsType<JsonObject>(await context.ReadJsonAsync("output/narrative_response.json"));
        narrative["response"] = "Чужое давление надломило волю души. Попытка наложить оковы не создаёт нового контроля.";
        await context.WriteExactJsonAsync("output/narrative_response.json", narrative.ToJsonString());
        var debug = Assert.IsType<JsonObject>(await context.ReadJsonAsync("output/debug_logs.json"));
        debug["gm_thoughts_markdown"] = string.Join("\n",
            "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: guardian_frame",
            "- Why relevant: Хранитель отвечает давлением в двух духовных обменах.",
            "- Actors outside scope: нет", "- Why outside scope: Другие акторы не участвуют.",
            "", "## Reasoning", "### guardian_frame", "- Current location: Море Хаоса; перемещения нет.",
            "- Situation: После первого pressure душа пытается наложить оковы, хранитель сохраняет встречное давление.",
            $"- Profile inputs: guardian_frame — guardian; pressure имеет ранг {(strong ? 1 : 0)}, особых искусств нет.",
            "- Thoughts: Я сохраню выбранную последовательность действий.",
            "- Motivation: Удержать устойчивость и не подчиниться оковам.",
            $"- Constraints: Два pressure стоят по {(strong ? 2 : 3)}; исходные операции и кубики не меняются.",
            "- Strategy options:", "1. Сохранить pressure. Benefit: выполнить выбранное действие. Risk: потратить силы.",
            "2. Перейти к guard. Benefit: усилить защиту. Risk: изменить исходный обмен.",
            "- Chosen strategy: Сохранить два pressure.", "- Rejected alternatives: Guard не выбран в исходном ходе.",
            $"- Actions: Кубики 5/15 и 13/{(strong ? 11 : 10)} сохранены. Рана души снижает эффективную позицию последних оков, но не каноническую позицию.",
            $"- State changes: {AfterlifeSpiritualConflictState.StatePath} содержит два обмена; душа strained, хранитель clear. Неуспешные оковы сохраняют контроль none. {ResourceMaterializationContract.StatePath} отражает player 6→3→1, guardian {(strong ? "6→4→2" : "6→3→0")}. Цели, отношения и память не изменяются.");
        await context.WriteExactJsonAsync("output/debug_logs.json", debug.ToJsonString());
    }
}
