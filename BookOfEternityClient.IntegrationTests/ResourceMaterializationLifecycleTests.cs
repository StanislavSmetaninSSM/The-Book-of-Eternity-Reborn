using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceMaterializationLifecycleTests
{
    private static readonly string[] PlayerOutputPaths =
    {
        "output/narrative_response.json",
        "output/interface_updates.json",
        "output/debug_logs.json"
    };

    [Theory]
    [InlineData(EffectAcceptedTurnPlan.IdentityIndexPath)]
    [InlineData(ResourceMaterializationContract.DefinitionsPath)]
    [InlineData(ResourceMaterializationContract.HistoryPath)]
    [InlineData(ResourceMaterializationContract.StatePath)]
    public async Task CommonPublicationFailureAfterPhysicalWrite_RestoresExactTurnSnapshot(
        string failurePath)
    {
        var armed = false;
        var injected = false;
        var observedMutationPaths = new List<string>();
        string? resolvedFailurePath = null;
        var hooks = new FileSystemManagerHooks
        {
            AfterPhysicalFilePublishedAsync = path =>
            {
                if (armed)
                    observedMutationPaths.Add(path);
                if (armed &&
                    !injected &&
                    string.Equals(path, resolvedFailurePath, StringComparison.OrdinalIgnoreCase))
                {
                    injected = true;
                    throw new InvalidDataException($"Injected publication failure at '{path}'.");
                }

                return Task.CompletedTask;
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        resolvedFailurePath = context.FileSystem.ResolvePath(failurePath);
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await SeedPlayerOutputsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceMaterializationValidationTests.DefinitionAndInitializationCommand()
                .ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var trackedPaths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(PlayerOutputPaths)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var before = await context.CaptureAsync(trackedPaths);

        armed = true;
        var exception = await Record.ExceptionAsync(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
                context.FileSystem,
                context.Normalizer,
                context.Validator,
                new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.True(
            exception != null,
            $"Expected an injected failure at '{failurePath}'. Observed: {string.Join(", ", observedMutationPaths)}");
        Assert.True(injected, $"The failure hook for '{failurePath}' was not reached.");
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task CanonicalPostValidationFailure_RestoresResourcesCommandAndPlayerOutputs()
    {
        var armed = false;
        var commandConsumptionReached = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = path =>
            {
                if (armed && string.Equals(
                        path,
                        ResourceMaterializationContract.CommandPath,
                        StringComparison.Ordinal))
                {
                    commandConsumptionReached = true;
                }
                return Task.CompletedTask;
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await SeedPlayerOutputsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceMaterializationValidationTests.DefinitionAndInitializationCommand()
                .ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        var state = Assert.IsType<JsonObject>(
            AcceptedMechanicsPlanAuthority.TryPeekValidated(
                context.FileSystem,
                out _,
                out var planning) &&
            planning.Plan != null
                ? planning.Plan.StateAfterImage
                : null);
        state["schemaVersion"] = 999;
        var trackedPaths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(PlayerOutputPaths)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var before = await context.CaptureAsync(trackedPaths);

        armed = true;
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem,
            context.Normalizer,
            context.Validator,
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Contains(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(commandConsumptionReached);
        await context.AssertUnchangedAsync(before);
    }

    private static async Task SeedPlayerOutputsAsync(
        ResourceMaterializationTestContext context)
    {
        await context.WriteExactJsonAsync(
            PlayerOutputPaths[0],
            new JsonObject { ["response"] = "Старый проверенный рассказ." }.ToJsonString());
        await context.WriteExactJsonAsync(
            PlayerOutputPaths[1],
            new JsonObject { ["dialogueOptions"] = new JsonArray() }.ToJsonString());
        await context.WriteExactJsonAsync(
            PlayerOutputPaths[2],
            new JsonObject { ["gm_thoughts_markdown"] = "Технический журнал." }.ToJsonString());
    }
}

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task ResourceRepairLifecycle_ReadyOrPartialRetryCannotDiscardCompleteTurn()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        _fs.DeleteFile("output/narrative_response.json");
        _fs.DeleteFile("output/interface_updates.json");

        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "resource_repair_complete_replay");
        var request = new TurnRequest
        {
            SessionId = "session_resource_repair_complete_replay",
            RequestId = "request_resource_repair_complete_replay",
            TurnNumber = 42,
            PlayerAction = "Потратить немного маны на защитный знак.",
            Timestamp = "2026-08-22T01:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };
        await WriteJsonAsync("input/turn_request.json", request);
        await InvokePrivateTaskResultAsync(
            engine,
            "CreateCanonicalBaselineSnapshotAsync",
            request,
            rollbackSnapshot,
            "resource repair complete replay test");
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);

        await WriteResourceRepairCommandAsync(includeReason: false);
        await WriteAcceptedResourceOutputsAsync(
            "Отвергнутый неполный ответ",
            "Отвергнутый выбор");
        var initialRawIssues = ((IEnumerable)await InvokePrivateTaskResultAsync(
                engine,
                "CollectAcceptedTurnRawStateIssuesAsync"))
            .Cast<ValidationIssue>()
            .Where(issue => issue.Severity == IssueSeverity.Error)
            .ToArray();
        Assert.True(
            initialRawIssues.Length > 0 &&
            initialRawIssues.All(issue =>
                string.Equals(
                    issue.FilePath,
                    "resourceChanges[0].reason",
                    StringComparison.Ordinal) &&
                string.Equals(
                    issue.Code,
                    "resource_command_invalid_field",
                    StringComparison.Ordinal) &&
                string.Equals(issue.Actual, "missing", StringComparison.Ordinal)),
            "Expected only the bounded resource omission, but got: " +
            string.Join(
                " | ",
                initialRawIssues.Select(issue =>
                    $"{issue.Code}:{issue.FilePath}:{issue.Actual}")));

        var readyOnlyRejected = false;
        var changedResourceSemanticRejected = false;
        var partialResourceOnlyRejected = false;
        Exception? gmFailure = null;
        var gmRepair = Task.Run(async () =>
        {
            try
            {
                var firstRequest = await WaitForValidationRepairRequestContainingAsync(
                    "resource_command_invalid_field",
                    TimeSpan.FromSeconds(8));
                Assert.False(_fs.FileExists(ResourceMaterializationContract.CommandPath));
                Assert.False(_fs.FileExists("output/narrative_response.json"));
                Assert.False(_fs.FileExists("output/interface_updates.json"));

                await WriteResourceRepairReadyAsync(
                    request,
                    "Ready alone must not clear the complete-turn obligation.");
                var secondRequest = await WaitForNewRepairAttemptAsync(
                    firstRequest,
                    minimumAttempt: 2,
                    TimeSpan.FromSeconds(8));
                Assert.Contains(
                    "resource_command_invalid_field",
                    secondRequest,
                    StringComparison.Ordinal);
                readyOnlyRejected = true;

                await WriteResourceRepairCommandAsync(includeReason: true, amount: 4);
                await WriteAcceptedResourceOutputsAsync(
                    "Подменённый расход пытается изменить исходный ход.",
                    "Продолжить путь");
                await WriteResourceRepairReadyAsync(
                    request,
                    "A changed resource semantic must not satisfy exact replay.");
                var thirdRequest = await WaitForNewRepairAttemptAsync(
                    secondRequest,
                    minimumAttempt: 3,
                    TimeSpan.FromSeconds(8));
                Assert.Contains(
                    "resource_command_invalid_field",
                    thirdRequest,
                    StringComparison.Ordinal);
                changedResourceSemanticRejected = true;

                await WriteResourceRepairCommandAsync(includeReason: true);
                await WriteResourceRepairReadyAsync(
                    request,
                    "Resource command alone remains an incomplete turn replay.");
                var fourthRequest = await WaitForNewRepairAttemptAsync(
                    thirdRequest,
                    minimumAttempt: 4,
                    TimeSpan.FromSeconds(8));
                Assert.Contains(
                    "resource_command_invalid_field",
                    fourthRequest,
                    StringComparison.Ordinal);
                partialResourceOnlyRejected = true;

                await WriteResourceRepairCommandAsync(includeReason: true);
                await WriteAcceptedResourceOutputsAsync(
                    "Знак вспыхнул, забрав три единицы маны.",
                    "Продолжить путь");
                await WriteResourceRepairReadyAsync(
                    request,
                    "Complete coherent resource turn resubmitted.");

                var laterRequest = await TryWaitForNewRepairAttemptAsync(
                    fourthRequest,
                    minimumAttempt: 5,
                    TimeSpan.FromSeconds(4));
                if (laterRequest != null)
                {
                    Assert.Contains(
                        "accepted_turn_stale_player_facing_output_after_canonical_repair",
                        laterRequest,
                        StringComparison.Ordinal);
                    await WriteAcceptedResourceOutputsAsync(
                        "Знак закрепился; расход маны подтверждён миром.",
                        "Продолжить путь");
                    await WriteResourceRepairReadyAsync(
                        request,
                        "Player-facing output regenerated after canonical repair.");
                }
            }
            catch (Exception exception)
            {
                gmFailure = exception;
                await WriteResourceRepairReadyAsync(request, "Abort test worker wait.");
            }
        });

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "resource materialization repair",
            snapshotContext,
            rollbackSnapshot,
            request.TurnNumber,
            request.ProgressionControl);
        await gmRepair;

        Assert.Null(gmFailure);
        Assert.True(readyOnlyRejected);
        Assert.True(changedResourceSemanticRejected);
        Assert.True(partialResourceOnlyRejected);
        Assert.True(accepted);
        Assert.False(_fs.FileExists(ResourceMaterializationContract.CommandPath));
        Assert.True(_fs.FileExists("output/narrative_response.json"));
        Assert.True(_fs.FileExists("output/interface_updates.json"));
        var stateJson = await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath);
        Assert.NotNull(stateJson);
        var state = JsonNode.Parse(stateJson)!.AsObject();
        var mana = Assert.Single(state["entries"]!.AsArray(), entry =>
            string.Equals(
                entry!["resourceKey"]!.GetValue<string>(),
                "mana",
                StringComparison.Ordinal));
        Assert.Equal(37m, mana!["current"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task ResourceRepairLifecycle_OneMissingReasonRestoresBaselineBeforeFullTurnDispatch()
    {
        const string sessionId = "session_resource_repair_baseline";
        const string requestId = "request_resource_repair_baseline";
        const int turnNumber = 42;
        const string trackedPath = "game_state/world/weather.json";
        const string baselineJson = "{\"description\":\"До хода\"}";
        const string rejectedJson = "{\"description\":\"Непринятая перемена\"}";
        await _fs.WriteFileAtomicAsync(trackedPath, baselineJson);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "actionable_resource_repair_baseline");
        await _fs.WriteFileAtomicAsync(
            $"game_state/control/pending_turn_snapshot/{trackedPath}",
            baselineJson);
        await WritePendingTurnSnapshotManifestAsync(
            sessionId,
            requestId,
            turnNumber,
            trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });
        await _fs.WriteFileAtomicAsync(trackedPath, rejectedJson);

        var issue = CreateResourceRepairIssue(
            "resourceChanges[0].reason",
            "resource_command_invalid_field",
            expected: "non-empty trimmed readable reason",
            actual: "missing");
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();
        string? observedAtDispatch = null;
        JsonElement? observedPacket = null;
        Exception? gmAssertionFailure = null;
        var gmRepair = Task.Run(async () =>
        {
            try
            {
                var requestJson = await WaitForValidationRepairRequestContainingAsync(
                    issue.Code!,
                    TimeSpan.FromSeconds(5));
                observedAtDispatch = await _fs.ReadFileAsync(trackedPath);
                using var document = JsonDocument.Parse(requestJson);
                Assert.True(document.RootElement
                    .GetProperty("fullTurnResubmissionRequired")
                    .GetBoolean());
                observedPacket = document.RootElement
                    .GetProperty("harnessRepairPackets")
                    .EnumerateArray()
                    .Single(packet => packet.GetProperty("kind").GetString() ==
                        "resource_semantic_omission_repair")
                    .Clone();
            }
            catch (Exception exception)
            {
                gmAssertionFailure = exception;
            }
            finally
            {
                await WriteJsonAsync(
                    "game_state/control/validation_repair_ready.json",
                    new
                    {
                        sessionId,
                        requestId,
                        turnNumber,
                        updatedAtUtc = "2026-08-22T00:00:00Z",
                        note = "Complete resource turn resubmission acknowledged."
                    });
            }
        });

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "actionable resource repair",
            new List<ValidationIssue> { issue },
            1,
            rollbackSnapshot,
            repairSessionGeneration);
        await gmRepair;

        Assert.Null(gmAssertionFailure);
        Assert.True(accepted);
        Assert.Equal(baselineJson, observedAtDispatch);
        Assert.Equal(baselineJson, await _fs.ReadFileAsync(trackedPath));
        var packet = Assert.IsType<JsonElement>(observedPacket);
        Assert.Equal("resourceChanges", packet.GetProperty("route").GetString());
        Assert.Equal(
            "ordinary resource change #1",
            packet.GetProperty("transitionClass").GetString());
        Assert.Empty(packet.GetProperty("targetFiles").EnumerateArray());
        Assert.Equal(
            new[] { "narrative reason" },
            packet.GetProperty("missingFields")
                .EnumerateArray()
                .Select(value => value.GetString()));
        var packetJson = packet.GetRawText();
        Assert.DoesNotContain("resourceChanges[0]", packetJson, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state", packetJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("targetId", packetJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceId", packetJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eventRef", packetJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResourceRepairLifecycle_ProtectedAuthorityFailsClosedBeforeGmDispatch()
    {
        const string sessionId = "session_resource_repair_protected";
        const string requestId = "request_resource_repair_protected";
        const int turnNumber = 43;
        const string trackedPath = "game_state/world/weather.json";
        const string baselineJson = "{\"description\":\"До хода\"}";
        await _fs.WriteFileAtomicAsync(trackedPath, baselineJson);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "protected_resource_repair");
        await _fs.WriteFileAtomicAsync(
            $"game_state/control/pending_turn_snapshot/{trackedPath}",
            baselineJson);
        await WritePendingTurnSnapshotManifestAsync(
            sessionId,
            requestId,
            turnNumber,
            trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });

        var issue = CreateResourceRepairIssue(
            ResourceMaterializationContract.StatePath,
            "resource_materialization_direct_state_mutation",
            expected: "exact pre-turn client-owned state",
            actual: "changed");
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();
        var repairDispatched = false;
        var observer = Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
            while (DateTime.UtcNow < deadline)
            {
                if (_fs.FileExists("game_state/control/validation_repair_request.json"))
                {
                    repairDispatched = true;
                    await WriteJsonAsync(
                        "game_state/control/validation_repair_ready.json",
                        new
                        {
                            sessionId,
                            requestId,
                            turnNumber,
                            updatedAtUtc = "2026-08-22T00:00:00Z",
                            note = "Release unexpected broad resource repair dispatch."
                        });
                    return;
                }

                await Task.Delay(20);
            }
        });

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "protected resource authority",
            new List<ValidationIssue> { issue },
            1,
            rollbackSnapshot,
            repairSessionGeneration);
        await observer;

        Assert.False(accepted);
        Assert.False(repairDispatched);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.Equal(baselineJson, await _fs.ReadFileAsync(trackedPath));
    }

    [Fact]
    public async Task ResourceRepairLifecycle_ActionableOmissionWithoutRollbackFailsClosed()
    {
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();
        var issue = CreateResourceRepairIssue(
            "resourceChanges[0].reason",
            "resource_command_invalid_field",
            expected: "non-empty trimmed readable reason",
            actual: "missing");

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "resource repair without rollback",
            new List<ValidationIssue> { issue },
            1,
            null,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
    }

    private static ValidationIssue CreateResourceRepairIssue(
        string path,
        string code,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            $"Unified resource contract rejected '{path}'.",
            code: code,
            actor: "Client",
            section: "UnifiedResourceAuthority",
            expected: expected,
            actual: actual,
            category: IssueCategory.StateConsistency);

    private async Task WriteResourceRepairCommandAsync(
        bool includeReason,
        int amount = 3)
    {
        var command = ResourceMaterializationValidationTests
            .DefinitionAndInitializationCommand();
        var change = new JsonObject
        {
            ["operation"] = "spend",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["resourceKey"] = "mana",
            ["amount"] = amount,
            ["source"] = new JsonObject
            {
                ["kind"] = "narrative_outcome"
            },
            ["eventRef"] = "turn_42:resource:3"
        };
        if (includeReason)
            change["reason"] = "Защитный знак требует маны";
        command["resourceChanges"] = new JsonArray(change);
        await _fs.WriteFileAtomicAsync(
            ResourceMaterializationContract.CommandPath,
            command.ToJsonString());
    }

    private async Task WriteAcceptedResourceOutputsAsync(
        string narrative,
        string option)
    {
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = narrative,
            timestamp = "2026-08-22T01:00:01Z"
        });
        await WriteJsonAsync("output/interface_updates.json", new
        {
            dialogueOptions = new[] { option },
            timestamp = "2026-08-22T01:00:01Z"
        });
    }

    private Task WriteResourceRepairReadyAsync(TurnRequest request, string note) =>
        WriteJsonAsync(
            "game_state/control/validation_repair_ready.json",
            new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                updatedAtUtc = "2026-08-22T01:00:02Z",
                note
            });
}
