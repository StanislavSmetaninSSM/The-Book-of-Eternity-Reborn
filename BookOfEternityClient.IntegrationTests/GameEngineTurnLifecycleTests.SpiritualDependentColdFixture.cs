using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Authors the established later-guard dependency in a full signed engine fixture without admitting private mechanics.
    /// </summary>
    /// <param name="context">
    /// Original six-point-per-side fixture receiving pressure followed by a zero-offer guard exchange.
    /// </param>
    /// <returns>
    /// A task completing after the ordinary draft retains the original guard cost until a real wound changes it.
    /// </returns>
    internal static async Task WriteSpiritualGameEngineDependentExchangeAsync(ResourceMaterializationTestContext context)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!;
        var second = active["exchangeLog"]![1]!;
        second["outcome"] = "no_effect";
        second["after"] = second["before"]!.DeepClone();
        second.AsObject().Remove("diceAudit");
        second["matchupAudit"]!["oppositionOperation"] = "guard";
        second["actionCostAudit"]!["opposition"] = CostAudit("guard", 2, 3, 1);
        active["oppositionSideStrain"] = second["before"]!["oppositionSideStrain"]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
    }

    /// <summary>
    /// Reuses the complete existing guardian wound proposal whose consequence increases the later guard cost by one.
    /// </summary>
    /// <param name="opportunityRef">
    /// Exact opportunity reference from the real current decision request.
    /// </param>
    /// <returns>
    /// A detached rank I materialize decision with the established guard-specific spiritual action cost burden.
    /// </returns>
    internal static JsonElement CreateSpiritualDependentGuardDecision(string opportunityRef) =>
        OriginalSpiritualWoundDecision(opportunityRef, "spiritual_action_cost_burden", "guard");
}

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Supplies complete ordinary outputs describing both original exchanges and the conditional later guard cost.
    /// </summary>
    /// <param name="context">
    /// Signed original session with the two-exchange draft.
    /// </param>
    /// <param name="request">
    /// Original turn metadata used by the existing output and terminal-signal writer.
    /// </param>
    /// <returns>
    /// A task completing after accurate actor reasoning replaces the single-exchange fixture explanation.
    /// </returns>
    private static async Task WriteDependentSpiritualLifecycleOutputsAsync(ResourceMaterializationTestContext context,
        TurnRequest request)
    {
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var debug = Assert.IsType<JsonObject>(await context.ReadJsonAsync("output/debug_logs.json"));
        debug["gm_thoughts_markdown"] = string.Join("\n",
            "## NPC Scope",
            "- Mode: Scene-local",
            "- Relevant actors: guardian_frame",
            "- Why relevant: Хранитель участвует в двух духовных обменах; меняются его запас действий и духовное состояние.",
            "- Actors outside scope: нет",
            "- Why outside scope: Другие самостоятельные акторы не участвуют.",
            "", "## Reasoning", "### guardian_frame",
            "- Current location: Море Хаоса; перемещения нет.",
            "- Situation: После встречного давления хранитель защищается во втором обмене с player_soul.",
            "- Profile inputs: guardian_frame — guardian из afterlife_entity_profiles; базовые искусства имеют ранг 0, особые искусства отсутствуют.",
            "- Thoughts: Я выдержу первый натиск, затем сосредоточусь на защите, хотя удерживать её становится труднее.",
            "- Motivation: Сохранить собственную устойчивость в духовном противостоянии.",
            "- Constraints: Первое pressure стоит 3; последующее guard имеет базовую цену 2. Возможная новая рана может увеличить только эффективную цену guard на 1.",
            "- Strategy options:",
            "1. Удержать исходное pressure, затем guard. Benefit: сохранить выбранную защиту. Risk: потратить оставшиеся силы, если появится бремя раны.",
            "2. Заменить второй обмен новым pressure. Benefit: вновь бороться за инициативу. Risk: изменить уже выбранное действие и оставить защиту.",
            "- Chosen strategy: Сохранить исходную последовательность pressure и guard.",
            "- Rejected alternatives: Новое pressure не выбрано: действия исходного хода уже определены.",
            "- Actions: Первый обмен использует исходные кубики 15 и 5. Во втором player_soul выполняет pressure, guardian_frame — guard; no_effect не требует нового броска, напряжение остаётся прежним.",
            $"- State changes: {AfterlifeSpiritualConflictState.StatePath} содержит оба исходных обмена; {AfterlifeEntityProfileState.StatePath} отражает состояние guardian_frame. В {ResourceMaterializationContract.StatePath} оба запаса spiritual_action_points сначала уменьшаются с 6 до 3. Второе pressure души стоит 3. Второе guard хранителя стоит 2 без раны или 3 после подтверждения бремени; исправление ограничено effectiveCost и after. Личные цели, отношения и память не изменяются.");
        await context.WriteExactJsonAsync("output/debug_logs.json", debug.ToJsonString());
    }

    /// <summary>
    /// Creates an actual worker producer for initial materialization and the later allowlisted dependent correction.
    /// </summary>
    /// <param name="context">
    /// Fixture root receiving the worker script outside canonical session state.
    /// </param>
    /// <returns>
    /// A normal validation-repair bridge profile using per-task proposal identities and reserved workspace changes only.
    /// </returns>
    private static WorkerBridgeProfile CreateDependentSpiritualWorkerProfile(ResourceMaterializationTestContext context)
    {
        var template = AfterlifeResourceCutoverTests.CreateSpiritualDependentGuardDecision("actual_offer_required").GetRawText();
        var scriptPath = Path.Combine(context.RootPath, "spiritual-dependent-cold-worker.ps1");
        File.WriteAllText(scriptPath, $$"""
            $ErrorActionPreference = 'Stop'
            $task = Get-Content -Raw -LiteralPath $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
            $continuation = $task.spiritualWoundContinuation
            if ($null -eq $continuation) { throw 'Expected an actual continuation task.' }
            if ($task.sourceTurn.sessionId -ne 'session_engine_spiritual' -or $task.sourceTurn.requestId -ne 'request_engine_spiritual_42' -or $task.sourceTurn.turnNumber -ne 42) { throw 'Original turn identity changed.' }
            $proposalId = 'proposal_' + $task.taskId
            $decisions = @()
            if ($continuation.phase -eq 'decision') {
                $decision = @'
            {{template}}
            '@ | ConvertFrom-Json
                $decision.opportunityRef = $continuation.offer.opportunityRef
                $decisions = @($decision)
                $path = 'output/narrative_response.json'
                $content = Get-Content -Raw -LiteralPath (Join-Path $env:BOE_WORKER_SESSION_PATH $path) | ConvertFrom-Json
                $content.response = 'Душа удерживает встречное давление. ' + $decision.proposal.display.acquisitionNarration + ' Хранитель сохраняет исходное guard во втором обмене.'
                $content.timestamp = [DateTime]::UtcNow.ToString('O')
            } elseif ($continuation.phase -eq 'dependent_draft') {
                if ($null -ne $continuation.offer) { throw 'A saved choice cannot receive another offer.' }
                $expected = @('/activeConflict/exchangeLog/1/actionCostAudit/opposition/after', '/activeConflict/exchangeLog/1/actionCostAudit/opposition/effectiveCost')
                $fields = @($continuation.dependentDraftFields)
                if ($fields.Count -ne 2 -or @($fields | Where-Object { $_.path -ne '{{AfterlifeSpiritualConflictState.StatePath}}' }).Count -ne 0) { throw 'Unexpected dependent paths.' }
                if (@(Compare-Object $expected @($fields.jsonPointer)).Count -ne 0) { throw 'Unexpected dependent pointers.' }
                $path = $fields[0].path
                $content = Get-Content -Raw -LiteralPath (Join-Path $env:BOE_WORKER_SESSION_PATH $path) | ConvertFrom-Json
                $content.activeConflict.exchangeLog[1].actionCostAudit.opposition.effectiveCost = 3
                $content.activeConflict.exchangeLog[1].actionCostAudit.opposition.after = 0
            } else { throw 'Unexpected continuation phase.' }
            $contentRef = 'worker_proposals/' + $proposalId + '/' + $path
            $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
            New-Item -ItemType Directory -Path (Split-Path $contentPath) -Force | Out-Null
            $content | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $contentPath -Encoding UTF8
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try { $afterHash = ([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($contentPath)))).Replace('-', '').ToLowerInvariant() }
            finally { $sha.Dispose() }
            $before = @($task.contextFiles | Where-Object { $_.path -eq $path })[0]
            $proposal = [ordered]@{
                schemaVersion = 1; proposalId = $proposalId; taskId = $task.taskId; workerId = $task.workerId
                status = 'completed'; summary = 'Resolved the current spiritual phase without replacing a saved choice.'
                changedFiles = @([ordered]@{ path = $path; changeKind = 'replace'; beforeSha256 = $before.sha256; afterSha256 = $afterHash; contentRef = $contentRef })
                findings = @()
                spiritualWoundContinuation = [ordered]@{ schemaVersion = 1; continuationId = $continuation.continuationId; woundDecisions = $decisions }
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
}
