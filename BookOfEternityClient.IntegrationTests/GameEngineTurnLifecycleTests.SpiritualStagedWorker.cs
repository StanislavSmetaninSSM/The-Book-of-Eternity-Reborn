using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Creates the real worker producer for one initial decision and the two separately issued dependent groups.
    /// </summary>
    /// <param name="context">
    /// Actual fixture root receiving the external test worker script.
    /// </param>
    /// <returns>
    /// The normal validation-repair bridge profile, preserving standard reservation, apply and Ready behavior.
    /// </returns>
    private static WorkerBridgeProfile CreateSpiritualStagedWorkerProfile(ResourceMaterializationTestContext context)
    {
        var template = AfterlifeResourceCutoverTests.CreateSpiritualStagedPlayerDecision("actual_offer_required").GetRawText();
        var scriptPath = Path.Combine(context.RootPath, "spiritual-staged-worker.ps1");
        File.WriteAllText(scriptPath, $$"""
            $ErrorActionPreference = 'Stop'
            $task = Get-Content -Raw -LiteralPath $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
            $request = $task.spiritualWoundContinuation
            if ($null -eq $request) { throw 'Expected actual spiritual continuation.' }
            if ($task.sourceTurn.sessionId -ne 'session_engine_spiritual' -or $task.sourceTurn.requestId -ne 'request_engine_spiritual_42' -or $task.sourceTurn.turnNumber -ne 42) { throw 'Original turn changed.' }
            $proposalId = 'proposal_' + $task.taskId
            $decisions = @()
            if ($request.phase -eq 'decision') {
                $decision = @'
            {{template}}
            '@ | ConvertFrom-Json
                $decision.opportunityRef = $request.offer.opportunityRef
                $decisions = @($decision)
                $path = 'output/narrative_response.json'
                $content = Get-Content -Raw -LiteralPath (Join-Path $env:BOE_WORKER_SESSION_PATH $path) | ConvertFrom-Json
                $content.response = 'Чужое давление надломило волю души. Во втором и третьем обменах оба сохраняют давление; нового вреда нет.'
                $content.timestamp = [DateTime]::UtcNow.ToString('O')
            } elseif ($request.phase -eq 'dependent_draft') {
                if ($null -ne $request.offer) { throw 'Saved choice must not be offered again.' }
                $path = '{{AfterlifeSpiritualConflictState.StatePath}}'
                $content = Get-Content -Raw -LiteralPath (Join-Path $env:BOE_WORKER_SESSION_PATH $path) | ConvertFrom-Json
                $fields = @($request.dependentDraftFields)
                if (@($fields | Where-Object { $_.path -ne $path }).Count -ne 0) { throw 'Foreign dependency path.' }
                $isB = @($fields | Where-Object { $_.jsonPointer -like '/activeConflict/exchangeLog/2/diceAudit/*' }).Count -gt 0
                if ($isB) {
                    $expected = @('/activeConflict/exchangeLog/2/diceAudit/margin', '/activeConflict/exchangeLog/2/diceAudit/modifierBreakdown', '/activeConflict/exchangeLog/2/diceAudit/oppositionTotal')
                    if ($fields.Count -ne 3 -or @(Compare-Object $expected @($fields.jsonPointer)).Count -ne 0) { throw 'B permissions changed.' }
                    if ($null -eq $content.activeConflict.exchangeLog[1].diceAudit.criticalResult) { throw 'B did not retain actual A narration.' }
                    $dice = $content.activeConflict.exchangeLog[2].diceAudit
                    $dice.oppositionTotal = 10
                    $dice.margin = -1
                } else {
                    $expected = @('/activeConflict/exchangeLog/1/diceAudit/criticalResult', '/activeConflict/exchangeLog/1/diceAudit/margin', '/activeConflict/exchangeLog/1/diceAudit/modifierBreakdown', '/activeConflict/exchangeLog/1/diceAudit/oppositionTotal')
                    if ($fields.Count -ne 4 -or @(Compare-Object $expected @($fields.jsonPointer)).Count -ne 0) { throw 'A permissions changed.' }
                    $dice = $content.activeConflict.exchangeLog[1].diceAudit
                    $dice.oppositionTotal = 20
                    $dice.margin = 1
                    $dice | Add-Member -NotePropertyName criticalResult -NotePropertyValue ([ordered]@{
                        playerNaturalRoll = 20; oppositionNaturalRoll = 18
                        marginOutcomeBand = 'mixed_or_no_effect'; normalizedOutcomeBand = 'player_success'
                        scaleLimit = 'Удачный бросок ограничен текущим духовным обменом и не создаёт нового вреда.'
                        narrativeConstraint = 'Обе стороны сохраняют напряжение, позицию и исходный no_effect.'
                    })
                }
                if (@($dice.modifierBreakdown.opposition).Count -ne 0) { throw 'Unexpected preexisting position row.' }
                $dice.modifierBreakdown.opposition = @([ordered]@{ modifierType = 'conflict_position'; source = 'conflictPosition'; position = 'opposition_advantaged'; value = 2 })
            } else { throw 'Unknown phase.' }
            $contentRef = 'worker_proposals/' + $proposalId + '/' + $path
            $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
            New-Item -ItemType Directory -Path (Split-Path $contentPath) -Force | Out-Null
            $content | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $contentPath -Encoding UTF8
            $afterHash = (Get-FileHash -LiteralPath $contentPath -Algorithm SHA256).Hash.ToLowerInvariant()
            $before = @($task.contextFiles | Where-Object { $_.path -eq $path })[0]
            $proposal = [ordered]@{
                schemaVersion = 1; proposalId = $proposalId; taskId = $task.taskId; workerId = $task.workerId
                status = 'completed'; summary = 'Completed only the actual issued spiritual frontier.'
                changedFiles = @([ordered]@{ path = $path; changeKind = 'replace'; beforeSha256 = $before.sha256; afterSha256 = $afterHash; contentRef = $contentRef })
                findings = @()
                spiritualWoundContinuation = [ordered]@{ schemaVersion = 1; continuationId = $request.continuationId; woundDecisions = $decisions }
                selfCheck = [ordered]@{ scopeReviewed = $true; validationExpectedToPass = $true; notes = @() }
                createdAtUtc = [DateTime]::UtcNow.ToString('O')
            }
            $proposal | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
            """);
        return GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
        {
            LaunchCommand = $"pwsh.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            TimeoutSeconds = 10
        };
    }

    /// <summary>
    /// Requires three actual reserved tasks to traverse worker apply and Ready once each with their exact issued request.
    /// </summary>
    /// <param name="context">
    /// Accepted session retaining real worker task/proposal/audit evidence.
    /// </param>
    /// <param name="observed">
    /// Actual request identities seen by the independent lifecycle observer.
    /// </param>
    /// <returns>
    /// A task completing after one decision task, separate A/B tasks and their accepted proposals are proved.
    /// </returns>
    private static async Task AssertSpiritualStagedWorkersAsync(ResourceMaterializationTestContext context, SpiritualStagedObservation observed)
    {
        var events = await new GmWorkerAuditLog(context.FileSystem).ReadEventsAsync();
        var dispatched = events.Where(row => row.EventType == "task-dispatched").ToArray();
        Assert.Equal(3, dispatched.Length);
        Assert.Equal(3, dispatched.Select(row => row.TaskId).Distinct(StringComparer.Ordinal).Count());
        foreach (var eventType in new[] { "proposal-applied", "validation-repair-ready-created" })
            Assert.Equal(3, events.Count(row => row.EventType == eventType));
        var continuationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dispatch in dispatched)
        {
            var task = GmWorkerJson.Deserialize<WorkerTaskPacket>((await context.FileSystem.ReadFileAsync(GmWorkerBridgePool.GetTaskPacketPath(dispatch.TaskId!)))!);
            Assert.NotNull(task);
            Assert.Equal(dispatch.TaskId, task.TaskId);
            Assert.Equal("session_engine_spiritual", task.SourceTurn.SessionId);
            Assert.Equal("request_engine_spiritual_42", task.SourceTurn.RequestId);
            Assert.Equal(42, task.SourceTurn.TurnNumber);
            var request = Assert.IsType<SpiritualWoundContinuationRequest>(task!.SpiritualWoundContinuation);
            Assert.True(continuationIds.Add(request.ContinuationId));
            Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(request));
            Assert.Equal(JsonSerializer.Serialize(observed.Phase.Requests[request.ContinuationId]), JsonSerializer.Serialize(request));
            var proposal = GmWorkerJson.Deserialize<WorkerProposal>((await context.FileSystem.ReadFileAsync(GmWorkerBridgePool.GetProposalInboxPath(dispatch.TaskId!)))!);
            Assert.NotNull(proposal);
            Assert.Equal("proposal_" + task.TaskId, proposal.ProposalId);
            Assert.Equal(task.TaskId, proposal.TaskId);
            var response = Assert.IsType<SpiritualWoundContinuationResponse>(proposal!.SpiritualWoundContinuation);
            Assert.Empty(SpiritualWoundContinuationProtocol.ValidateResponse(request, response));
            var changed = Assert.Single(proposal.ChangedFiles);
            if (request.Phase == "decision")
            {
                Assert.Empty(task.ValidationIssues);
                Assert.Equal("output/narrative_response.json", Assert.Single(task.AllowedProposalPaths));
                Assert.Contains(task.ContextFiles, file => file.Path == "output/narrative_response.json");
                Assert.All(task.ContextFiles, file => Assert.Contains(file.Path,
                    new[] { "output/narrative_response.json", AfterlifeRealmAuthorityContract.StatePath }));
                Assert.Equal("materialize", Assert.Single(response.WoundDecisions).GetProperty("decision").GetString());
                Assert.Equal("output/narrative_response.json", changed.Path);
            }
            else
            {
                Assert.Equal("dependent_draft", request.Phase);
                Assert.NotEmpty(task.ValidationIssues);
                Assert.Empty(response.WoundDecisions);
                Assert.Null(request.Offer);
                Assert.Equal(AfterlifeSpiritualConflictState.StatePath, changed.Path);
            }
            foreach (var eventType in new[] { "proposal-applied", "validation-repair-ready-created" })
                Assert.Single(events, row => row.EventType == eventType && row.TaskId == dispatch.TaskId);
        }
        Assert.Equal(observed.Phase.Requests.Keys.Order(StringComparer.Ordinal), continuationIds.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Writes normal turn outputs with complete guardian reasoning for the three original pressure exchanges.
    /// </summary>
    /// <param name="context">
    /// Full signed original fixture receiving standard output artifacts.
    /// </param>
    /// <param name="request">
    /// Immutable original turn identity and actions.
    /// </param>
    /// <returns>
    /// A task completing after the standard terminal signal and accurate NPC reasoning are present.
    /// </returns>
    private static async Task WriteSpiritualStagedLifecycleOutputsAsync(ResourceMaterializationTestContext context, TurnRequest request)
    {
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var debug = Assert.IsType<JsonObject>(await context.ReadJsonAsync("output/debug_logs.json"));
        debug["gm_thoughts_markdown"] = string.Join("\n",
            "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: guardian_frame",
            "- Why relevant: Хранитель участвует в трёх духовных обменах и тратит запас действий.",
            "- Actors outside scope: нет", "- Why outside scope: Другие самостоятельные акторы не участвуют.",
            "", "## Reasoning", "### guardian_frame", "- Current location: Море Хаоса; перемещения нет.",
            "- Situation: Хранитель сохраняет встречное pressure во всех трёх обменах с player_soul.",
            "- Profile inputs: guardian_frame — guardian из afterlife_entity_profiles; pressure имеет ранг 2, особые искусства отсутствуют.",
            "- Thoughts: Продолжу давление после первого успеха, не меняя выбранную последовательность.",
            "- Motivation: Сохранить устойчивость в духовном противостоянии.",
            "- Constraints: Каждое pressure стоит 1 после скидки искусства; запас 6→5→4→3. Возможная рана души даёт штраф стартовой позиции pressure 1.",
            "- Strategy options:", "1. Продолжить pressure. Benefit: сохранить выбранное действие. Risk: потратить силы без нового вреда.",
            "2. Перейти к защите. Benefit: сосредоточиться на устойчивости. Risk: отказаться от уже выбранного давления.",
            "- Chosen strategy: Сохранить три исходных pressure.", "- Rejected alternatives: Guard не выбран; исходные действия уже определены.",
            "- Actions: Исходные кубики 5/15,20/18,9/8 сохраняются. В среднем обмене независимый player+1 даёт21/18, бремя души даёт opposition+2 и21/20. Natural20 требует только ограниченного критического описания; исходный no_effect сохраняется. Третий9/8 становится9/10, тоже no_effect.",
            $"- State changes: {AfterlifeSpiritualConflictState.StatePath} содержит три исходных обмена; после первого душа strained, хранитель clear. Два следующих before/after совпадают; новые раны и последствия не сочиняются. {ResourceMaterializationContract.StatePath} отражает шесть расходов по1. Личные цели, отношения и память не изменяются.");
        await context.WriteExactJsonAsync("output/debug_logs.json", debug.ToJsonString());
    }
}
