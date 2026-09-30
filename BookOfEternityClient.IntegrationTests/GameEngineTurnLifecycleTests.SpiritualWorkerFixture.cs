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
    /// Reuses the complete tested GM proposal while leaving opportunity correlation to the genuine current offer.
    /// </summary>
    /// <param name="opportunityRef">
    /// Exact public opportunity reference read from the owner-issued transport request.
    /// </param>
    /// <returns>
    /// A detached rank I guardian wound decision with the existing complete consequence and recovery fields.
    /// </returns>
    internal static JsonElement CreateSpiritualLifecycleMaterializeDecision(string opportunityRef) =>
        OriginalSpiritualWoundDecision(opportunityRef);
}

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Creates a deterministic GM process that authors its response and optional narrative only in the reserved worker workspace.
    /// </summary>
    /// <param name="context">
    /// Fixture root used to store the temporary worker script outside canonical session state.
    /// </param>
    /// <param name="materialize">
    /// Whether the process copies the complete proposal template into a materialize response and proposes its acquisition narration.
    /// </param>
    /// <returns>
    /// The existing validation-repair profile running PowerShell 7 with unchanged default permissions.
    /// </returns>
    private static WorkerBridgeProfile CreateSpiritualLifecycleWorkerProfile(ResourceMaterializationTestContext context, bool materialize)
    {
        var template = AfterlifeResourceCutoverTests.CreateSpiritualLifecycleMaterializeDecision("actual_offer_required").GetRawText();
        var scriptPath = Path.Combine(context.RootPath, "spiritual-lifecycle-worker.ps1");
        File.WriteAllText(scriptPath, $$"""
            $ErrorActionPreference = 'Stop'
            $task = Get-Content -Raw -LiteralPath $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
            $continuation = $task.spiritualWoundContinuation
            if ($null -eq $continuation -or $continuation.phase -ne 'decision') { throw 'Expected an actual decision task.' }
            if ($task.sourceTurn.sessionId -ne 'session_engine_spiritual' -or $task.sourceTurn.requestId -ne 'request_engine_spiritual_42' -or $task.sourceTurn.turnNumber -ne 42) { throw 'Original turn identity changed.' }
            $proposalId = 'worker_proposal_spiritual_lifecycle'
            $changes = @()
            $decision = [ordered]@{ opportunityRef = $continuation.offer.opportunityRef; decision = 'none' }
            if ({{(materialize ? "$true" : "$false")}}) {
                $decision = @'
            {{template}}
            '@ | ConvertFrom-Json
                $decision.opportunityRef = $continuation.offer.opportunityRef
                $path = 'output/narrative_response.json'
                $narrative = Get-Content -Raw -LiteralPath (Join-Path $env:BOE_WORKER_SESSION_PATH $path) | ConvertFrom-Json
                $narrative.response = $decision.proposal.display.acquisitionNarration
                $contentRef = 'worker_proposals/' + $proposalId + '/' + $path
                $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
                New-Item -ItemType Directory -Path (Split-Path $contentPath) -Force | Out-Null
                $narrative | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $contentPath -Encoding UTF8
                $sha = [System.Security.Cryptography.SHA256]::Create()
                try { $afterHash = ([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($contentPath)))).Replace('-', '').ToLowerInvariant() }
                finally { $sha.Dispose() }
                $before = @($task.contextFiles | Where-Object { $_.path -eq $path })[0]
                $changes = @([ordered]@{ path = $path; changeKind = 'replace'; beforeSha256 = $before.sha256; afterSha256 = $afterHash; contentRef = $contentRef })
            }
            $proposal = [ordered]@{
                schemaVersion = 1; proposalId = $proposalId; taskId = $task.taskId; workerId = $task.workerId
                status = 'completed'; summary = 'Responded to the original spiritual exchange.'
                changedFiles = $changes; findings = @()
                spiritualWoundContinuation = [ordered]@{ schemaVersion = 1; continuationId = $continuation.continuationId; woundDecisions = @($decision) }
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
    /// Proves the real worker executed, passed apply and published its response before GameEngine consumed Ready.
    /// </summary>
    /// <param name="context">
    /// Accepted session retaining worker task, proposal and audit evidence.
    /// </param>
    /// <param name="original">
    /// Signed original turn identity that the worker task must retain.
    /// </param>
    /// <param name="materialize">
    /// Whether the worker must have proposed one narrative replacement and a materialize decision.
    /// </param>
    /// <returns>
    /// A task completing after exactly one dispatch, apply and Ready event and their genuine envelope are checked.
    /// </returns>
    private static async Task AssertSpiritualLifecycleWorkerCompletedAsync(ResourceMaterializationTestContext context,
        TurnRequest original, bool materialize)
    {
        var task = GmWorkerJson.Deserialize<WorkerTaskPacket>((await context.FileSystem.ReadFileAsync(
            GmWorkerValidationRepairDelegator.LatestValidationRepairTaskPath))!);
        Assert.NotNull(task);
        Assert.Equal(original.SessionId, task.SourceTurn.SessionId);
        Assert.Equal(original.RequestId, task.SourceTurn.RequestId);
        Assert.Equal(original.TurnNumber, task.SourceTurn.TurnNumber);
        Assert.Empty(task.ValidationIssues);
        var request = Assert.IsType<SpiritualWoundContinuationRequest>(task.SpiritualWoundContinuation);
        Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(request));
        Assert.Equal("decision", request.Phase);
        Assert.Equal("output/narrative_response.json", Assert.Single(task.AllowedProposalPaths));
        Assert.Contains(task.ContextFiles, file => file.Path == "output/narrative_response.json");
        Assert.All(task.ContextFiles, file => Assert.Contains(file.Path,
            new[] { "output/narrative_response.json", AfterlifeRealmAuthorityContract.StatePath }));
        var proposal = GmWorkerJson.Deserialize<WorkerProposal>((await context.FileSystem.ReadFileAsync(
            GmWorkerBridgePool.GetProposalInboxPath(task.TaskId)))!);
        Assert.NotNull(proposal);
        Assert.Equal(task.TaskId, proposal.TaskId);
        var response = Assert.IsType<SpiritualWoundContinuationResponse>(proposal.SpiritualWoundContinuation);
        Assert.Empty(SpiritualWoundContinuationProtocol.ValidateResponse(request, response));
        Assert.Equal(materialize ? "materialize" : "none", Assert.Single(response.WoundDecisions).GetProperty("decision").GetString());
        if (materialize)
            Assert.Equal("output/narrative_response.json", Assert.Single(proposal.ChangedFiles).Path);
        else
            Assert.Empty(proposal.ChangedFiles);
        var events = await new GmWorkerAuditLog(context.FileSystem).ReadEventsAsync();
        foreach (var eventType in new[] { "task-dispatched", "proposal-applied", "validation-repair-ready-created" })
            Assert.Equal(task.TaskId, Assert.Single(events, entry => entry.EventType == eventType).TaskId);
    }

    /// <summary>
    /// Checks the single materialized guardian wound and its exact creation links across canonical authority and receipt.
    /// </summary>
    /// <param name="context">
    /// Accepted session containing the published wound and chronology.
    /// </param>
    /// <param name="decisionReceipt">
    /// The single materialize receipt generated by the client-owned publisher.
    /// </param>
    /// <returns>
    /// A task completing after identity, carrier, creation transition and acquisition narration agree.
    /// </returns>
    private static async Task AssertSpiritualLifecycleMaterializedWoundAsync(ResourceMaterializationTestContext context,
        JsonNode decisionReceipt)
    {
        var identity = WoundIdentityState.Parse(await context.FileSystem.ReadFileAsync(WoundIdentityState.StatePath),
            WoundIdentityState.StatePath);
        Assert.True(identity.IsValid, string.Join(Environment.NewLine, identity.Issues));
        var entry = Assert.Single(identity.State!.Entries);
        Assert.Equal("guardian", entry.OwnerKind);
        Assert.Equal("guardian_frame", entry.OwnerId);
        Assert.Equal(42, entry.CreatedAtTurn);
        Assert.Equal(entry.WoundId, decisionReceipt["woundId"]!.GetValue<string>());
        var history = WoundHistoryState.Parse(await context.FileSystem.ReadFileAsync(WoundHistoryState.HistoryPath),
            WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var creation = Assert.Single(history.State!.Transitions);
        Assert.Equal("create", creation.Kind);
        Assert.Equal(42, creation.Turn);
        Assert.Equal(entry.WoundId, creation.WoundId);
        Assert.Equal(creation.TransitionId, decisionReceipt["transitionId"]!.GetValue<string>());
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!["profiles"]!.AsArray();
        var guardian = Assert.Single(profiles.OfType<JsonObject>(), profile => profile["actorId"]!.GetValue<string>() == "guardian_frame");
        var wound = Assert.Single(guardian["activeWounds"]!.AsArray())!;
        Assert.Equal(entry.WoundId, wound["woundId"]!.GetValue<string>());
        var parsedWound = WoundMaterializationContract.Parse(wound.ToJsonString(),
            AfterlifeEntityProfileState.StatePath + ".profiles[1].activeWounds[0]");
        Assert.True(parsedWound.IsValid, string.Join(Environment.NewLine, parsedWound.Issues));
        Assert.Equal("I", parsedWound.Wound!.Severity.Value);
        Assert.Equal(1, parsedWound.Wound.Severity.Rank);
        Assert.Contains("Чужое давление надломило волю хранителя.",
            (await context.ReadJsonAsync("output/narrative_response.json"))!["response"]!.GetValue<string>(), StringComparison.Ordinal);
    }
}
