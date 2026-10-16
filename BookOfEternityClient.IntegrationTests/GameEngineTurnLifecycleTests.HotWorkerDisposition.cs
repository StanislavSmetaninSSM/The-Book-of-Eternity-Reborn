using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HotSubmission_ActualWorkerApplyCannotIssueOrdinaryReadyCustody(bool failReadyPublication)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        const string weatherPath = "game_state/world/weather.json";
        const string readyPath = "game_state/control/validation_repair_ready.json";
        var readyCuts = 0; var armed = false;
        string? appliedTaskAtCut = null;
        FileSystemManager? files = null;
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationAsync = path =>
                {
                    if (!armed || !failReadyPublication || readyCuts != 0 || path != readyPath)
                        return Task.CompletedTask;
                    var auditPath = files!.ResolvePath(GmWorkerAuditLog.AuditLogPath);
                    if (!File.Exists(auditPath)) return Task.CompletedTask;
                    var applied = File.ReadAllLines(auditPath).Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => GmWorkerJson.Deserialize<WorkerAuditEvent>(line))
                        .Where(row => row?.EventType == "proposal-applied").ToArray();
                    if (applied.Length == 0) return Task.CompletedTask;
                    appliedTaskAtCut = Assert.Single(applied)!.TaskId;
                    readyCuts++;
                    // Real accepted apply has returned. This existing physical
                    // mutation hook is before Ready publisher/temp creation.
                    throw new IOException("Actual accepted worker apply; Ready publication did not begin.");
                }
            });
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
        await files.WriteFileAtomicAsync(weatherPath, "{\"tendency\":\"NO_CHANGE\",\"description\":\"Погода не меняется.\"}");
        var scriptPath = Path.Combine(_rootPath, "hot-custody-worker.ps1");
        await File.WriteAllTextAsync(scriptPath, """
            $ErrorActionPreference = 'Stop'
            $task = Get-Content -Raw -LiteralPath $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
            $weather = @($task.contextFiles | Where-Object { $_.path -eq 'game_state/world/weather.json' })[0]
            if ($null -eq $weather) { throw 'Missing actual weather context.' }
            $proposalId = 'worker_proposal_hot_custody'
            $contentRef = 'worker_proposals/' + $proposalId + '/game_state/world/weather.json'
            $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
            New-Item -ItemType Directory -Path (Split-Path $contentPath) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $env:BOE_WORKER_SESSION_PATH 'game_state/world/weather.json') -Destination $contentPath
            $proposal = [ordered]@{
                schemaVersion = 1; proposalId = $proposalId; taskId = $task.taskId; workerId = $task.workerId
                status = 'completed'; summary = 'Actual no-op weather repair for HOT route control.'
                changedFiles = @([ordered]@{
                    path = 'game_state/world/weather.json'; changeKind = 'replace'; beforeSha256 = $weather.sha256
                    afterSha256 = (Get-FileHash -LiteralPath $contentPath -Algorithm SHA256).Hash.ToLowerInvariant()
                    contentRef = $contentRef
                })
                findings = @(); selfCheck = [ordered]@{ scopeReviewed = $true; validationExpectedToPass = $true; notes = @() }
                createdAtUtc = [DateTime]::UtcNow.ToString('O')
            }
            $proposal | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
            """, Encoding.UTF8);
        var executable = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";
        var shell = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Select(path => Path.GetFullPath(Path.Combine(path, executable))).FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("PowerShell 7 fixture executable is unavailable.");
        var input = new QueuedConsoleInputSource([]);
        var engine = CreateGameEngine(input, settings =>
        {
            InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0;
            settings.GmWorkerBridgeProfiles.Add(GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
                { Enabled = true, LaunchCommand = $"\"{shell}\" -NoLogo -NoProfile -File \"{scriptPath}\"", TimeoutSeconds = 15 });
        }, fileSystem: files);
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession("session_hot_actual_worker", 42);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(files, async () =>
        {
            var preparation = Assert.IsAssignableFrom<object>(await InvokePrivateTaskNullableResultAsync(
                engine, "StagePlayerTurnAsync", "Спокойно осмотреться.", null, null));
            var request = Assert.IsType<TurnRequest>(preparation.GetType().GetProperty("Request")!.GetValue(preparation));
            var backup = preparation.GetType().GetProperty("Backup")!.GetValue(preparation)!;
            var generation = Assert.IsType<string>(preparation.GetType().GetProperty("Generation")!.GetValue(preparation));
            var before = await files.ReadFileBytesAsync(weatherPath);
            armed = true;
            using var deadlineStop = new CancellationTokenSource();
            var deadline = Task.Delay(TimeSpan.FromSeconds(40), deadlineStop.Token);
            Task<bool>? repairWait = null;
            var failures = new List<Exception>();
            var accepted = false;
            var guardFired = false;
            try
            {
                repairWait = InvokePrivateAsync<bool>(engine, "WaitForContractRepairAsync", "hot actual worker control",
                    new List<ValidationIssue> { new(weatherPath, IssueSeverity.Error, "Synthetic weather repair owner control.",
                        code: "normalized_weather_missing_description") }, 1, backup, generation);
                if (await Task.WhenAny(repairWait, deadline) != repairWait)
                {
                    guardFired = true;
                    throw new TimeoutException("Actual worker repair Wait exceeded the fixture's protective 40s bound.");
                }
                accepted = await repairWait;
            }
            catch (Exception failure) { failures.Add(failure); }
            finally
            {
                await deadlineStop.CancelAsync();
                if (repairWait is { IsCompleted: false }) input.Enqueue(Key(ConsoleKey.Escape));
                // A worker fallback enters the real ordinary Ready/Escape loop.
                // Join that owner and the guard before participation/root disposal.
                try { if (repairWait is not null) await repairWait; }
                catch (Exception failure)
                {
                    if (!failures.Contains(failure)) failures.Add(failure);
                }
                try { await deadline; }
                catch (OperationCanceledException failure) when (failure.CancellationToken == deadlineStop.Token) { }
                catch (Exception failure)
                {
                    if (!failures.Contains(failure)) failures.Add(failure);
                }
                armed = false;
            }
            // Retain the actual worker result even when fallback/launch/apply fails;
            // a diagnostic failure must not replace the primary owner failure.
            try
            {
                WritePhysicalInjuryDiagnostic("fr063_hot_actual_worker_settled_before_oracles", new
                {
                    failReadyPublication, guardFired, accepted, readyCuts, appliedTaskAtCut,
                    WaitSettled = repairWait?.IsCompleted, GuardSettled = deadline.IsCompleted,
                    Audit = await new GmWorkerAuditLog(files).ReadEventsAsync(),
                    LatestTask = await files.ReadFileAsync(GmWorkerValidationRepairDelegator.LatestValidationRepairTaskPath),
                    Failures = failures.Select(failure => failure.ToString()).ToArray(), request.RequestId, generation
                });
            }
            catch (Exception failure) { failures.Add(failure); }
            if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException("Actual worker fixture and settled diagnostics failed.", failures);
            Assert.True(accepted); Assert.Equal(failReadyPublication ? 1 : 0, readyCuts);
            var audit = await new GmWorkerAuditLog(files).ReadEventsAsync();
            var applied = Assert.Single(audit, row => row.EventType == "proposal-applied");
            var task = GmWorkerJson.Deserialize<WorkerTaskPacket>((await files.ReadFileAsync(
                GmWorkerValidationRepairDelegator.LatestValidationRepairTaskPath))!)!;
            Assert.Equal(task.TaskId, applied.TaskId); Assert.Equal(request.SessionId, task.SourceTurn.SessionId);
            Assert.Equal(request.RequestId, task.SourceTurn.RequestId); Assert.Equal(43, task.SourceTurn.TurnNumber);
            Assert.Equal(generation, task.SessionGeneration);
            if (failReadyPublication) Assert.Equal(task.TaskId, appliedTaskAtCut);
            Assert.Equal(before, await files.ReadFileBytesAsync(weatherPath));
            Assert.Null(await files.ReadFileBytesAsync(readyPath));
            Assert.Null(await files.ReadFileBytesAsync("game_state/control/validation_repair_request.json"));
            var ledger = (await files.ReadFileAsync("game_state/control/gm_trajectory_ledger.jsonl"))!
                .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonNode.Parse(line)!.AsObject()).ToArray();
            var acceptedRow = Assert.Single(ledger, row => row["requestId"]?.GetValue<string>() == request.RequestId &&
                row["sessionId"]?.GetValue<string>() == request.SessionId && row["turnNumber"]?.GetValue<int>() == 43 &&
                row["validation"]?["acceptanceScope"]?.GetValue<string>() ==
                    (failReadyPublication ? "worker_apply_gate" : "correlated_repair_ready"));
            Assert.Equal(failReadyPublication ? "worker_apply_gate_accepted" : "validation_repair_ready",
                acceptedRow["terminal"]!["kind"]!.GetValue<string>());
            Assert.False(acceptedRow["validation"]!["fullCanonicalStateAccepted"]!.GetValue<bool>());
            var diagnostic = InvokePrivateValue<JsonObject>(engine, "ExportHotOrdinarySubmissionDiagnostic");
            Assert.True(diagnostic["binding"]!["actualStageCompleted"]!.GetValue<bool>());
            Assert.Equal("Unavailable", diagnostic["custody"]!["status"]!.GetValue<string>());
            Assert.Equal("WorkerDispositionNotIntegrated", diagnostic["custody"]!["reason"]!.GetValue<string>());
            Assert.Equal(!failReadyPublication, diagnostic["custody"]!["readyDeleteReturned"]!.GetValue<bool>());
            Assert.Equal(!failReadyPublication, diagnostic["custody"]!["readyReleaseReturned"]!.GetValue<bool>());
            foreach (var key in new[] { "hasS", "hasB", "hasM3" }) Assert.False(diagnostic[key]!.GetValue<bool>());
            WritePhysicalInjuryDiagnostic("fr063_hot_actual_worker_disposition", new
                { failReadyPublication, readyCuts, appliedTaskAtCut, applied, task, diagnostic, request.RequestId, generation });
            return true;
        });
    }
}
