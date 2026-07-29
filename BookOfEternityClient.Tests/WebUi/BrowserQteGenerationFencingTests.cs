using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed class BrowserQteGenerationFencingTests : IDisposable
{
    private readonly string _rootPath;

    public BrowserQteGenerationFencingTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-browser-qte-fencing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task AcceptOffer_ConcurrentSessionReplacementWaitsForWholeQteTransaction()
    {
        var runtimeWriteStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRuntimeWrite = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementContended = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fs = CreateFileSystem(
            () =>
            {
                replacementContended.TrySetResult();
                return Task.CompletedTask;
            });
        var hooks = new QteSceneServiceHooks
        {
            BeforeRuntimeWriteAsync = async () =>
            {
                runtimeWriteStarted.TrySetResult();
                await allowRuntimeWrite.Task;
            }
        };
        var web = CreateWebService(fs, hooks);
        await WriteOfferAsync(fs, BuildTerminalOffer());
        var interactionToken = RequiredInteractionToken(
            await web.BuildReadOnlyStateAsync());

        var accept = web.ResolveOfferDecisionAsync(
            new QteWebOfferDecisionRequest("accept", interactionToken));
        await runtimeWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var replacement = fs.ClearGameStateAsync();
        await replacementContended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(replacement.IsCompleted);

        allowRuntimeWrite.TrySetResult();
        var result = await accept.WaitAsync(TimeSpan.FromSeconds(5));
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Active", result.State);
        Assert.False(fs.FileExists(QteSceneService.QteOfferPath));
        Assert.False(fs.FileExists(QteSceneService.QteRuntimePath));
    }

    [Fact]
    public async Task PracticeState_ConcurrentSessionReplacementWaitsForCharacteristicProjection()
    {
        var characteristicReadStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCharacteristicRead = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementContended = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fs = CreateFileSystem(
            () =>
            {
                replacementContended.TrySetResult();
                return Task.CompletedTask;
            });
        var web = CreateWebService(
            fs,
            new QteSceneServiceHooks
            {
                BeforeQteCharacteristicReadAsync = async () =>
                {
                    characteristicReadStarted.TrySetResult();
                    await allowCharacteristicRead.Task;
                }
            });

        var catalog = await web.BuildPracticeStateAsync();
        var start = web.StartPracticeAttemptAsync(
            new QtePracticeStartRequest(
                "MashInput",
                "normal",
                RequiredInteractionToken(catalog)));
        await characteristicReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var replacement = fs.ClearGameStateAsync();
        await replacementContended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(replacement.IsCompleted);

        allowCharacteristicRead.TrySetResult();
        var result = await start.WaitAsync(TimeSpan.FromSeconds(5));
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Active", result.State);
    }

    [Fact]
    public async Task PracticeAttempt_CannotContinueAfterSessionGenerationChanges()
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        var started = await StartPracticeAsync(web, "MashInput", "normal");
        var action = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        await fs.ClearGameStateAsync();

        var result = await web.ResolvePracticeActionAsync(
            new QtePracticeActionRequest(
                action.ActionId,
                "success",
                RequiredInteractionToken(started)));

        Assert.Equal("Failed", result.State);
        Assert.Contains("сесс", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedPracticeStart_AfterSessionReplacementDoesNotExposePreviousAttempt()
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        var started = await StartPracticeAsync(web, "MashInput", "normal");
        Assert.Equal("Active", started.State);
        await fs.ClearGameStateAsync();

        var replacementCatalog = await web.BuildPracticeStateAsync();
        var result = await web.StartPracticeAttemptAsync(
            new QtePracticeStartRequest(
                "unknown-qte",
                "normal",
                RequiredInteractionToken(replacementCatalog)));

        Assert.Equal("Failed", result.State);
        Assert.Null(result.ActiveScene);
        Assert.Equal("Catalog", (await web.BuildPracticeStateAsync()).State);
    }

    [Fact]
    public async Task DarenAttempt_CannotContinueAfterSessionGenerationChanges()
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        var started = await StartDarenAsync(web);
        var action = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        await fs.ClearGameStateAsync();

        var result = await web.ResolveDarenShowcaseActionAsync(
            new DarenShowcaseActionRequest(
                action.ActionId,
                "success",
                RequiredInteractionToken(started)));

        Assert.Equal("Failed", result.State);
        Assert.Contains("сесс", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("decline")]
    public async Task OfferDecision_StaleInteractionTokenCannotAdoptReplacementOffer(
        string decision)
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        await WriteOfferAsync(fs, BuildTerminalOffer());
        var staleToken = RequiredInteractionToken(
            await web.BuildReadOnlyStateAsync());

        await fs.ClearGameStateAsync();
        await WriteOfferAsync(fs, BuildTerminalOffer());
        var replacementState = await web.BuildReadOnlyStateAsync();
        Assert.NotEqual(staleToken, RequiredInteractionToken(replacementState));
        var replacementBytes =
            await fs.ReadFileBytesAsync(QteSceneService.QteOfferPath);

        var result = await web.ResolveOfferDecisionAsync(
            DeserializeRequest<QteWebOfferDecisionRequest>(
                new JsonObject
                {
                    ["decision"] = decision,
                    ["interactionToken"] = staleToken
                }));

        Assert.Equal("Failed", result.State);
        AssertErrorCode(result, "SessionReplaced");
        Assert.Equal(
            replacementBytes,
            await fs.ReadFileBytesAsync(QteSceneService.QteOfferPath));
        Assert.False(fs.FileExists(QteSceneService.QteRuntimePath));
    }

    [Fact]
    public async Task ActiveAction_StaleInteractionTokenCannotAdoptReplacementAttempt()
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        await WriteActiveRuntimeAsync(fs, BuildTerminalOffer());
        var staleToken = RequiredInteractionToken(
            await web.BuildReadOnlyStateAsync());

        await fs.ClearGameStateAsync();
        await WriteActiveRuntimeAsync(fs, BuildTerminalOffer());
        var replacementState = await web.BuildReadOnlyStateAsync();
        Assert.NotEqual(staleToken, RequiredInteractionToken(replacementState));
        var replacementBytes =
            await fs.ReadFileBytesAsync(QteSceneService.QteRuntimePath);

        var result = await web.ResolveActionAsync(
            DeserializeRequest<QteWebActionRequest>(
                new JsonObject
                {
                    ["actionId"] = "finish",
                    ["grade"] = "success",
                    ["interactionToken"] = staleToken
                }));

        Assert.Equal("Failed", result.State);
        AssertErrorCode(result, "SessionReplaced");
        Assert.Equal(
            replacementBytes,
            await fs.ReadFileBytesAsync(QteSceneService.QteRuntimePath));
        Assert.False(fs.FileExists(QteSceneService.QteHistoryPath));
    }

    [Fact]
    public async Task PracticeMutations_StaleInteractionTokenCannotAdoptReplacementAttempt()
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        var catalogAToken = RequiredInteractionToken(
            await web.BuildPracticeStateAsync());
        var sessionA = await web.StartPracticeAttemptAsync(
            DeserializeRequest<QtePracticeStartRequest>(
                new JsonObject
                {
                    ["typeId"] = "MashInput",
                    ["difficultyId"] = "normal",
                    ["interactionToken"] = catalogAToken
                }));
        var staleToken = RequiredInteractionToken(sessionA);
        var staleAction = Assert.Single(
            sessionA.ActiveScene!.CurrentChapter!.Actions).ActionId;

        await fs.ClearGameStateAsync();
        var catalogBToken = RequiredInteractionToken(
            await web.BuildPracticeStateAsync());
        var sessionB = await web.StartPracticeAttemptAsync(
            DeserializeRequest<QtePracticeStartRequest>(
                new JsonObject
                {
                    ["typeId"] = "MashInput",
                    ["difficultyId"] = "normal",
                    ["interactionToken"] = catalogBToken
                }));
        var replacementToken = RequiredInteractionToken(sessionB);

        var actionResult = await web.ResolvePracticeActionAsync(
            DeserializeRequest<QtePracticeActionRequest>(
                new JsonObject
                {
                    ["actionId"] = staleAction,
                    ["grade"] = "success",
                    ["interactionToken"] = staleToken
                }));
        Assert.Equal("Failed", actionResult.State);
        AssertErrorCode(actionResult, "SessionReplaced");
        Assert.Equal(
            replacementToken,
            RequiredInteractionToken(await web.BuildPracticeStateAsync()));

        var retryResult = await InvokeTokenMutationAsync<QtePracticeWebStateDto>(
            web,
            nameof(QteWebInteractionService.RetryPracticeAttemptAsync),
            staleToken);
        Assert.Equal("Failed", retryResult.State);
        AssertErrorCode(retryResult, "SessionReplaced");
        Assert.Equal(
            replacementToken,
            RequiredInteractionToken(await web.BuildPracticeStateAsync()));

        var exitResult = await InvokeTokenMutationAsync<QtePracticeWebStateDto>(
            web,
            nameof(QteWebInteractionService.ExitPracticeAttemptAsync),
            staleToken);
        Assert.Equal("Failed", exitResult.State);
        AssertErrorCode(exitResult, "SessionReplaced");
        Assert.Equal(
            replacementToken,
            RequiredInteractionToken(await web.BuildPracticeStateAsync()));
    }

    [Fact]
    public async Task PracticeMutation_OldRevisionTokenCannotMutateCompletedAttempt()
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        var catalogToken = RequiredInteractionToken(
            await web.BuildPracticeStateAsync());
        var active = await web.StartPracticeAttemptAsync(
            DeserializeRequest<QtePracticeStartRequest>(
                new JsonObject
                {
                    ["typeId"] = "MashInput",
                    ["difficultyId"] = "normal",
                    ["interactionToken"] = catalogToken
                }));
        var activeToken = RequiredInteractionToken(active);
        var action = Assert.Single(active.ActiveScene!.CurrentChapter!.Actions);
        var completed = await web.ResolvePracticeActionAsync(
            DeserializeRequest<QtePracticeActionRequest>(
                new JsonObject
                {
                    ["actionId"] = action.ActionId,
                    ["grade"] = "success",
                    ["interactionToken"] = activeToken
                }));
        var completedToken = RequiredInteractionToken(completed);
        Assert.NotEqual(activeToken, completedToken);

        var result = await InvokeTokenMutationAsync<QtePracticeWebStateDto>(
            web,
            nameof(QteWebInteractionService.RetryPracticeAttemptAsync),
            activeToken);

        Assert.Equal("Failed", result.State);
        AssertErrorCode(result, "StaleInteraction");
        Assert.Equal(
            completedToken,
            RequiredInteractionToken(await web.BuildPracticeStateAsync()));
    }

    [Fact]
    public async Task DarenMutations_StaleInteractionTokenCannotAdoptReplacementAttempt()
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        var introAToken = RequiredInteractionToken(
            await web.BuildDarenShowcaseStateAsync());
        var sessionA = await InvokeTokenMutationAsync<DarenShowcaseWebStateDto>(
            web,
            nameof(QteWebInteractionService.StartDarenShowcaseAsync),
            introAToken);
        var staleToken = RequiredInteractionToken(sessionA);
        var staleAction = Assert.Single(
            sessionA.ActiveScene!.CurrentChapter!.Actions).ActionId;

        await fs.ClearGameStateAsync();
        var introBToken = RequiredInteractionToken(
            await web.BuildDarenShowcaseStateAsync());
        var sessionB = await InvokeTokenMutationAsync<DarenShowcaseWebStateDto>(
            web,
            nameof(QteWebInteractionService.StartDarenShowcaseAsync),
            introBToken);
        var replacementToken = RequiredInteractionToken(sessionB);

        var actionResult = await web.ResolveDarenShowcaseActionAsync(
            DeserializeRequest<DarenShowcaseActionRequest>(
                new JsonObject
                {
                    ["actionId"] = staleAction,
                    ["grade"] = "success",
                    ["interactionToken"] = staleToken
                }));
        Assert.Equal("Failed", actionResult.State);
        AssertErrorCode(actionResult, "SessionReplaced");
        Assert.Equal(
            replacementToken,
            RequiredInteractionToken(await web.BuildDarenShowcaseStateAsync()));

        var retryResult = await InvokeTokenMutationAsync<DarenShowcaseWebStateDto>(
            web,
            nameof(QteWebInteractionService.RetryDarenShowcaseAsync),
            staleToken);
        Assert.Equal("Failed", retryResult.State);
        AssertErrorCode(retryResult, "SessionReplaced");
        Assert.Equal(
            replacementToken,
            RequiredInteractionToken(await web.BuildDarenShowcaseStateAsync()));

        var exitResult = await InvokeTokenMutationAsync<DarenShowcaseWebStateDto>(
            web,
            nameof(QteWebInteractionService.ExitDarenShowcaseAsync),
            staleToken);
        Assert.Equal("Failed", exitResult.State);
        AssertErrorCode(exitResult, "SessionReplaced");
        Assert.Equal(
            replacementToken,
            RequiredInteractionToken(await web.BuildDarenShowcaseStateAsync()));

        var startResult = await InvokeTokenMutationAsync<DarenShowcaseWebStateDto>(
            web,
            nameof(QteWebInteractionService.StartDarenShowcaseAsync),
            introAToken);
        Assert.Equal("Failed", startResult.State);
        AssertErrorCode(startResult, "SessionReplaced");
        Assert.Equal(
            replacementToken,
            RequiredInteractionToken(await web.BuildDarenShowcaseStateAsync()));
    }

    [Fact]
    public async Task ResolveAction_NewGameWinsBeforeQteLease_DoesNotRestoreOldBytesIntoReplacementSession()
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        await WriteActiveRuntimeAsync(fs, BuildTerminalOffer());
        var interactionToken = RequiredInteractionToken(
            await web.BuildReadOnlyStateAsync());
        string originalGeneration;
        await using (var generationLease = await fs.AcquireCanonicalWriteLeaseAsync())
            originalGeneration = fs.GetOrCreateSessionGeneration(generationLease);

        var operationBound = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowOldOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var oldOperation = SessionOperationContext.RunBoundAsync(
            fs,
            originalGeneration,
            async () =>
            {
                operationBound.TrySetResult();
                await allowOldOperation.Task;
                return await web.ResolveActionAsync(
                    new QteWebActionRequest(
                        "finish",
                        "success",
                        interactionToken));
            });

        await operationBound.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fs.ClearGameStateAsync();
        byte[] replacementBytes =
        [
            0xEF, 0xBB, 0xBF,
            (byte)'{', (byte)'"', (byte)'s', (byte)'e', (byte)'s', (byte)'s',
            (byte)'i', (byte)'o', (byte)'n', (byte)'"', (byte)':', (byte)'"',
            (byte)'B', (byte)'"', (byte)'}'
        ];
        await fs.WriteFileAtomicBytesAsync(QteSceneService.QteRuntimePath, replacementBytes);

        allowOldOperation.TrySetResult();
        await Assert.ThrowsAsync<SessionReplacedException>(
            () => oldOperation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(
            replacementBytes,
            await fs.ReadFileBytesAsync(QteSceneService.QteRuntimePath));
    }

    [Fact]
    public async Task PracticeRetry_ProjectionFailureRestoresCompletedAttempt()
    {
        var failProjection = false;
        var fs = CreateFileSystem();
        var web = CreateWebService(
            fs,
            new QteSceneServiceHooks
            {
                BeforeQteCharacteristicReadAsync = () =>
                {
                    if (!failProjection)
                        return Task.CompletedTask;

                    failProjection = false;
                    return Task.FromException(
                        new IOException("Injected practice projection failure."));
                }
            });
        var started = await StartPracticeAsync(web, "MashInput", "normal");
        var action = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        var completed = await web.ResolvePracticeActionAsync(
            new QtePracticeActionRequest(
                action.ActionId,
                "success",
                RequiredInteractionToken(started)));
        Assert.Equal("Completed", completed.State);
        failProjection = true;

        await Assert.ThrowsAsync<IOException>(() =>
            web.RetryPracticeAttemptAsync(
                RequiredInteractionToken(completed)));
        var restored = await web.BuildPracticeStateAsync();

        Assert.Equal("Completed", restored.State);
        Assert.NotNull(restored.Completion);
    }

    [Fact]
    public async Task DarenExit_ConcurrentFailedResolveDoesNotResurrectAttempt()
    {
        var projectionStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowProjectionFailure = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failProjection = false;
        var fs = CreateFileSystem();
        var web = CreateWebService(
            fs,
            new QteSceneServiceHooks
            {
                BeforeQteCharacteristicReadAsync = async () =>
                {
                    if (!failProjection)
                        return;

                    failProjection = false;
                    projectionStarted.TrySetResult();
                    await allowProjectionFailure.Task;
                    throw new IOException("Injected concurrent Daren projection failure.");
                }
            });
        var started = await StartDarenAsync(web);
        var action = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        failProjection = true;

        var resolve = web.ResolveDarenShowcaseActionAsync(
            new DarenShowcaseActionRequest(
                action.ActionId,
                "success",
                RequiredInteractionToken(started)));
        await projectionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var exit = web.ExitDarenShowcaseAsync(
            RequiredInteractionToken(started));
        allowProjectionFailure.TrySetResult();

        var failedResolve = await resolve.WaitAsync(TimeSpan.FromSeconds(5));
        var exited = await exit.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Failed", failedResolve.State);
        Assert.Equal("Intro", exited.State);
        Assert.Equal("Intro", (await web.BuildDarenShowcaseStateAsync()).State);
    }

    [Fact]
    public async Task BuildState_ProjectionFailureRollsBackRuntimeNormalization()
    {
        var failProjection = true;
        var fs = CreateFileSystem();
        var web = CreateWebService(
            fs,
            new QteSceneServiceHooks
            {
                BeforeQteCharacteristicReadAsync = () =>
                {
                    if (!failProjection)
                        return Task.CompletedTask;

                    failProjection = false;
                    return Task.FromException(
                        new IOException("Injected normalized runtime projection failure."));
                }
            });
        var offer = BuildTerminalOffer();
        await WriteActiveRuntimeAsync(fs, offer);
        var original = JsonNode.Parse(
            (await fs.ReadFileAsync(QteSceneService.QteRuntimePath))!)!.AsObject();
        original["lastDeclinedAtTurn"] = "invalid";
        var originalJson = original.ToJsonString(
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        await fs.WriteFileAtomicAsync(QteSceneService.QteRuntimePath, originalJson);

        var result = await web.BuildStateAsync();

        Assert.Equal("Failed", result.State);
        Assert.Contains("projection failure", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            originalJson,
            await fs.ReadFileAsync(QteSceneService.QteRuntimePath));
    }

    [Fact]
    public async Task LeaseBoundNormalizer_ReadsRelativeQteBackupFromCanonicalSession()
    {
        const string backupPath =
            "game_state/control/qte_normalizer_backups/test/soul_quests.json";
        const string expected = """{ "quests": [{ "questId": "qte-test" }] }""";
        var fs = CreateFileSystem();
        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        await fs.WriteFileAtomicAsync(writeLease, backupPath, expected);
        var normalizer = new CanonicalStateNormalizer(
                fs,
                NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(writeLease);

        var actual = await normalizer.ReadBackupTextAsync(backupPath);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("decline")]
    public async Task ResolveOfferDecision_LateRuntimeFailureRollsBackOfferAndRuntime(
        string decision)
    {
        var failure = new IOException($"Injected late {decision} failure.");
        var fs = CreateFileSystem();
        var web = CreateWebService(
            fs,
            new QteSceneServiceHooks
            {
                AfterRuntimeWrittenAsync = _ => Task.FromException(failure)
            });
        await WriteOfferAsync(fs, BuildTerminalOffer());
        var interactionToken = RequiredInteractionToken(
            await web.BuildReadOnlyStateAsync());

        var result = await web.ResolveOfferDecisionAsync(
            new QteWebOfferDecisionRequest(
                decision,
                interactionToken));

        Assert.Equal("Failed", result.State);
        Assert.Contains(failure.Message, result.Error, StringComparison.Ordinal);
        Assert.True(fs.FileExists(QteSceneService.QteOfferPath));
        Assert.False(fs.FileExists(QteSceneService.QteRuntimePath));
    }

    [Theory]
    [InlineData("history")]
    [InlineData("runtime")]
    public async Task CompleteAction_LatePersistenceFailureRollsBackOutcomeHistoryAndRuntime(
        string failurePhase)
    {
        var fs = CreateFileSystem();
        var failure = new IOException($"Injected late {failurePhase} failure.");
        var hooks = new QteSceneServiceHooks
        {
            AfterHistoryWrittenAsync = failurePhase == "history"
                ? () => Task.FromException(failure)
                : null,
            AfterRuntimeWrittenAsync = failurePhase == "runtime"
                ? state => state.ActiveScene == null
                    ? Task.FromException(failure)
                    : Task.CompletedTask
                : null
        };
        var web = CreateWebService(fs, hooks, out var stateManager);
        var offer = BuildTerminalOffer();
        await WriteActiveRuntimeAsync(fs, offer);
        await fs.WriteFileAtomicAsync(
            "game_state/player/experience.json",
            """{ "totalExperience": 10 }""");

        var runtimeBefore = await fs.ReadFileAsync(QteSceneService.QteRuntimePath);
        var runtimeBytesBefore = await fs.ReadFileBytesAsync(QteSceneService.QteRuntimePath);
        var experienceBytesBefore = await fs.ReadFileBytesAsync(
            "game_state/player/experience.json");
        await stateManager.RefreshGameStateAsync();
        var stateBefore = stateManager.CurrentState;
        var interactionToken = RequiredInteractionToken(
            await web.BuildReadOnlyStateAsync());

        var result = await web.ResolveActionAsync(
            new QteWebActionRequest(
                "finish",
                "success",
                interactionToken));

        Assert.Equal("Failed", result.State);
        Assert.Contains(failure.Message, result.Error, StringComparison.Ordinal);
        Assert.Equal(runtimeBefore, await fs.ReadFileAsync(QteSceneService.QteRuntimePath));
        Assert.Equal(
            runtimeBytesBefore,
            await fs.ReadFileBytesAsync(QteSceneService.QteRuntimePath));
        Assert.False(fs.FileExists(QteSceneService.QteHistoryPath));
        Assert.False(fs.FileExists("output/narrative_response.json"));
        Assert.Equal(
            experienceBytesBefore,
            await fs.ReadFileBytesAsync("game_state/player/experience.json"));

        var experience = JsonNode.Parse(
            (await fs.ReadFileAsync("game_state/player/experience.json"))!)!.AsObject();
        Assert.Equal(10, experience["totalExperience"]!.GetValue<int>());
        Assert.Same(stateBefore, stateManager.CurrentState);
    }

    [Fact]
    public async Task DarenCompletion_ConcurrentSessionReplacementWaitsForProfileCommit()
    {
        var profileWriteStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowProfileWrite = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementContended = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fs = CreateFileSystem(
            () =>
            {
                replacementContended.TrySetResult();
                return Task.CompletedTask;
            });
        var web = CreateWebService(
            fs,
            new QteSceneServiceHooks
            {
                BeforeDarenProfileWriteAsync = async () =>
                {
                    profileWriteStarted.TrySetResult();
                    await allowProfileWrite.Task;
                }
            });
        var state = await StartDarenAsync(web);
        Task<DarenShowcaseWebStateDto>? terminalAction = null;

        while (string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var action = Assert.Single(state.ActiveScene!.CurrentChapter!.Actions);
            var actionTask = web.ResolveDarenShowcaseActionAsync(
                new DarenShowcaseActionRequest(
                    action.ActionId,
                    "success",
                    RequiredInteractionToken(state)));
            var completed = await Task.WhenAny(
                actionTask,
                profileWriteStarted.Task).WaitAsync(TimeSpan.FromSeconds(5));
            if (ReferenceEquals(completed, profileWriteStarted.Task))
            {
                terminalAction = actionTask;
                break;
            }

            state = await actionTask;
        }

        Assert.NotNull(terminalAction);
        var replacement = fs.ClearGameStateAsync();
        await replacementContended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(replacement.IsCompleted);

        allowProfileWrite.TrySetResult();
        var completedState = await terminalAction!.WaitAsync(TimeSpan.FromSeconds(10));
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Completed", completedState.State);
        var profilePath = Path.Combine(
            _rootPath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        Assert.True(File.Exists(profilePath));
        Assert.Contains(
            "darenShowcase",
            await File.ReadAllTextAsync(profilePath),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DarenCompletion_StagesExternalProfileRollbackBeforeProfileWriteCompletes()
    {
        var observedDurableExternalRollback = false;
        var observedDurablePhysicalAuthority = false;
        var fs = CreateFileSystem();
        var web = CreateWebService(
            fs,
            new QteSceneServiceHooks
            {
                AfterDarenProfileWrittenAsync = async () =>
                {
                    var rollbackRoot = fs.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root);
                    var manifestPath = Directory
                        .GetFiles(
                            rollbackRoot,
                            "browser_write_manifest.json",
                            SearchOption.AllDirectories)
                        .Single();
                    var manifest = await File.ReadAllTextAsync(manifestPath);
                    observedDurableExternalRollback = manifest.Contains(
                        ExplorerLocalTurnRollbackArtifacts.DarenRewardProfileExternalFileId,
                        StringComparison.Ordinal);
                    observedDurablePhysicalAuthority =
                        manifest.Contains("\"parentIdentity\"", StringComparison.Ordinal) &&
                        manifest.Contains("\"publishedIdentity\"", StringComparison.Ordinal) &&
                        manifest.Contains("\"publishedSha256\"", StringComparison.Ordinal) &&
                        manifest.Contains("\"publicationTransactionId\"", StringComparison.Ordinal);
                }
            });
        var state = await StartDarenAsync(web);

        while (string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var action = Assert.Single(state.ActiveScene!.CurrentChapter!.Actions);
            state = await web.ResolveDarenShowcaseActionAsync(
                new DarenShowcaseActionRequest(
                    action.ActionId,
                    "success",
                    RequiredInteractionToken(state)));
        }

        Assert.Equal("Completed", state.State);
        Assert.True(observedDurableExternalRollback);
        Assert.True(observedDurablePhysicalAuthority);
    }

    [Fact]
    public async Task DarenCompletion_LateProfileFailureRollsBackProfileAndAttempt()
    {
        var failProfileWrite = true;
        var failure = new IOException("Injected late Daren profile failure.");
        var fs = CreateFileSystem();
        var web = CreateWebService(
            fs,
            new QteSceneServiceHooks
            {
                AfterDarenProfileWrittenAsync = () =>
                {
                    if (!failProfileWrite)
                        return Task.CompletedTask;

                    failProfileWrite = false;
                    return Task.FromException(failure);
                }
            });
        var state = await StartDarenAsync(web);

        while (string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var action = Assert.Single(state.ActiveScene!.CurrentChapter!.Actions);
            state = await web.ResolveDarenShowcaseActionAsync(
                new DarenShowcaseActionRequest(
                    action.ActionId,
                    "success",
                    RequiredInteractionToken(state)));
            if (string.Equals(state.State, "Failed", StringComparison.OrdinalIgnoreCase))
                break;
        }

        var profilePath = Path.Combine(
            _rootPath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        Assert.Equal("Failed", state.State);
        Assert.Contains(failure.Message, state.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(profilePath));

        var retryAction = Assert.Single(state.ActiveScene!.CurrentChapter!.Actions);
        var completed = await web.ResolveDarenShowcaseActionAsync(
            new DarenShowcaseActionRequest(
                retryAction.ActionId,
                "success",
                RequiredInteractionToken(state)));

        Assert.Equal("Completed", completed.State);
        Assert.True(File.Exists(profilePath));
    }

    [Fact]
    public async Task DarenCompletion_LateFailureRetainsPostImageAndRestoresExactBaselineIdentity()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var profilePath = Path.Combine(
            _rootPath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        byte[] baseline =
            """{ "schemaVersion": 1, "darenShowcase": null }"""u8.ToArray();
        await File.WriteAllBytesAsync(profilePath, baseline);
        var baselineIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(profilePath);
        var displacedPostImage = Path.Combine(
            _rootPath,
            "displaced-daren-post-image.json");
        var replacementBlocked = false;
        var failure = new IOException(
            "Injected Daren post-image ownership failure.");
        var fs = CreateFileSystem();
        var web = CreateWebService(
            fs,
            new QteSceneServiceHooks
            {
                AfterDarenProfileWrittenAsync = () =>
                {
                    try
                    {
                        File.Move(profilePath, displacedPostImage);
                        File.WriteAllBytes(
                            profilePath,
                            """{ "unrelated": true }"""u8.ToArray());
                    }
                    catch (Exception ex) when (
                        ex is IOException or UnauthorizedAccessException)
                    {
                        replacementBlocked = true;
                    }

                    return Task.FromException(failure);
                }
            });
        var state = await StartDarenAsync(web);

        while (string.Equals(
                   state.State,
                   "Active",
                   StringComparison.OrdinalIgnoreCase))
        {
            var action = Assert.Single(
                state.ActiveScene!.CurrentChapter!.Actions);
            state = await web.ResolveDarenShowcaseActionAsync(
                new DarenShowcaseActionRequest(
                    action.ActionId,
                    "success",
                    RequiredInteractionToken(state)));
        }

        Assert.Equal("Failed", state.State);
        Assert.True(replacementBlocked);
        Assert.False(File.Exists(displacedPostImage));
        Assert.Equal(baseline, await File.ReadAllBytesAsync(profilePath));
        Assert.Equal(
            baselineIdentity,
            WindowsHardLinkTestHelper.CaptureIdentity(profilePath));
    }

    [Fact]
    public async Task DarenCompletion_PostImageHardLinkRestoresExactBaselineAndRetainsEvidence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var profilePath = Path.Combine(
            _rootPath,
            DarenQteRewardProfileService.ProfileRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        byte[] baseline =
            """{ "schemaVersion": 1, "darenShowcase": null }"""u8.ToArray();
        await File.WriteAllBytesAsync(profilePath, baseline);
        var baselineIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(profilePath);
        var linkedPostImage = Path.Combine(
            _rootPath,
            "linked-daren-post-image.json");
        var failure = new IOException(
            "Injected Daren post-image hard-link failure.");
        var fs = CreateFileSystem();
        var web = CreateWebService(
            fs,
            new QteSceneServiceHooks
            {
                AfterDarenProfileWrittenAsync = () =>
                {
                    WindowsHardLinkTestHelper.Create(
                        linkedPostImage,
                        profilePath);
                    return Task.FromException(failure);
                }
            });
        var state = await StartDarenAsync(web);

        while (string.Equals(
                   state.State,
                   "Active",
                   StringComparison.OrdinalIgnoreCase))
        {
            var action = Assert.Single(
                state.ActiveScene!.CurrentChapter!.Actions);
            state = await web.ResolveDarenShowcaseActionAsync(
                new DarenShowcaseActionRequest(
                    action.ActionId,
                    "success",
                    RequiredInteractionToken(state)));
        }

        Assert.Equal("Failed", state.State);
        Assert.Equal(baseline, await File.ReadAllBytesAsync(profilePath));
        Assert.Equal(
            baselineIdentity,
            WindowsHardLinkTestHelper.CaptureIdentity(profilePath));
        Assert.True(File.Exists(linkedPostImage));
        Assert.NotEqual(
            baseline,
            await File.ReadAllBytesAsync(linkedPostImage));
        Assert.NotEmpty(Directory.GetFiles(
            fs.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root),
            "browser_write_manifest.json",
            SearchOption.AllDirectories));
        Assert.NotEmpty(Directory.GetDirectories(
            fs.PhysicalPublicationTransactionsRootPath));
    }

    [Fact]
    public async Task DarenCompletion_ProfileParentReplacedWithJunction_DoesNotWriteOutsideAuthority()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var profileDirectory = Path.Combine(_rootPath, "client_profile");
        var displacedProfileDirectory = Path.Combine(
            Path.GetTempPath(),
            "boe-daren-profile-original-" + Guid.NewGuid().ToString("N"));
        var profilePath = Path.Combine(
            profileDirectory,
            "qte_showcase_rewards.json");
        var displacedProfilePath = Path.Combine(
            displacedProfileDirectory,
            "qte_showcase_rewards.json");
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-daren-profile-outside-" + Guid.NewGuid().ToString("N"));
        var outsideSentinel = Path.Combine(outsideRoot, "sentinel.txt");
        var originalProfile = """{ "schemaVersion": 1, "darenShowcase": null }""";
        Directory.CreateDirectory(profileDirectory);
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllTextAsync(profilePath, originalProfile);
        await File.WriteAllTextAsync(outsideSentinel, "outside-must-remain-unchanged");

        var fs = CreateFileSystem();
        var hookInvoked = false;
        var replacementBlocked = false;
        var web = CreateWebService(
            fs,
            new QteSceneServiceHooks
            {
                BeforeDarenProfileWriteAsync = () =>
                {
                    hookInvoked = true;
                    try
                    {
                        Directory.Move(
                            profileDirectory,
                            displacedProfileDirectory);
                        CreateDirectoryJunction(
                            profileDirectory,
                            outsideRoot);
                    }
                    catch (Exception ex) when (
                        ex is IOException or UnauthorizedAccessException)
                    {
                        replacementBlocked = true;
                    }

                    return replacementBlocked
                        ? Task.FromException(
                            new IOException(
                                "Injected failure after blocked Daren parent replacement."))
                        : Task.CompletedTask;
                }
            });

        try
        {
            var state = await StartDarenAsync(web);
            while (string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase))
            {
                var action = Assert.Single(state.ActiveScene!.CurrentChapter!.Actions);
                state = await web.ResolveDarenShowcaseActionAsync(
                    new DarenShowcaseActionRequest(
                        action.ActionId,
                        "success",
                        RequiredInteractionToken(state)));
            }

            Assert.Equal("Failed", state.State);
            Assert.True(hookInvoked, state.Error);
            Assert.True(replacementBlocked, state.Error);
            Assert.False(File.Exists(Path.Combine(
                outsideRoot,
                "qte_showcase_rewards.json")));
            Assert.Equal(
                "outside-must-remain-unchanged",
                await File.ReadAllTextAsync(outsideSentinel));
            Assert.Equal(
                originalProfile,
                await File.ReadAllTextAsync(profilePath));
        }
        finally
        {
            PhysicalFileAuthority.TryDeleteTree(
                profileDirectory,
                "Daren profile junction test cleanup");
            if (Directory.Exists(displacedProfileDirectory))
                Directory.Delete(displacedProfileDirectory, recursive: true);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RestoreDarenProfileRollbackBytes_ProfileParentIsJunction_RejectsOutsideWrite()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var profileDirectory = Path.Combine(_rootPath, "client_profile");
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-daren-rollback-outside-" + Guid.NewGuid().ToString("N"));
        var outsideSentinel = Path.Combine(outsideRoot, "sentinel.txt");
        Directory.CreateDirectory(outsideRoot);
        File.WriteAllText(outsideSentinel, "rollback-must-not-touch-outside");
        CreateDirectoryJunction(profileDirectory, outsideRoot);
        var fs = CreateFileSystem();

        try
        {
            await using var writeLease =
                await fs.AcquireCanonicalWriteLeaseAsync();
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                QteSceneService.RestoreDarenProfileRollbackBytesAsync(
                    fs,
                    writeLease,
                    """{ "schemaVersion": 1 }"""u8.ToArray()));
            Assert.False(File.Exists(Path.Combine(
                outsideRoot,
                "qte_showcase_rewards.json")));
            Assert.Equal(
                "rollback-must-not-touch-outside",
                File.ReadAllText(outsideSentinel));
        }
        finally
        {
            PhysicalFileAuthority.TryDeleteTree(
                profileDirectory,
                "Daren rollback junction test cleanup");
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DarenProfileRollback_HardLinkedProfileIsRejectedWithoutChangingExternalBytes()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var profileDirectory = Path.Combine(_rootPath, "client_profile");
        var profilePath = Path.Combine(
            profileDirectory,
            "qte_showcase_rewards.json");
        var externalPath = Path.Combine(
            _rootPath,
            "external-daren-profile.json");
        var expected = """{ "outside": "must-remain-exact" }"""u8.ToArray();
        Directory.CreateDirectory(profileDirectory);
        await File.WriteAllBytesAsync(externalPath, expected);
        CreateHardLink(profilePath, externalPath);
        var fs = CreateFileSystem();

        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            QteSceneService.ReadDarenProfileRollbackBytesAsync(
                fs,
                writeLease));
        Assert.Equal(expected, await File.ReadAllBytesAsync(externalPath));
    }

    [Fact]
    public async Task DarenProfileRollback_RestoresAndReadsExactBytesThenDeletes()
    {
        byte[] expected =
        [
            0xEF, 0xBB, 0xBF,
            (byte)'{', (byte)'\r', (byte)'\n',
            (byte)' ', (byte)' ', (byte)'"', (byte)'x', (byte)'"',
            (byte)':', (byte)' ', (byte)'1',
            (byte)'\r', (byte)'\n', (byte)'}'
        ];
        var fs = CreateFileSystem();
        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();

        await QteSceneService.RestoreDarenProfileRollbackBytesAsync(
            fs,
            writeLease,
            expected);

        Assert.Equal(
            expected,
            await QteSceneService.ReadDarenProfileRollbackBytesAsync(
                fs,
                writeLease));

        await QteSceneService.RestoreDarenProfileRollbackBytesAsync(
            fs,
            writeLease,
            content: null);
        Assert.Null(await QteSceneService.ReadDarenProfileRollbackBytesAsync(
            fs,
            writeLease));
    }

    private FileSystemManager CreateFileSystem(Func<Task>? onCanonicalWriteContention = null)
    {
        var fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                CanonicalWriteLockContendedAsync = onCanonicalWriteContention
            });
        fs.EnsureDirectoryStructure();
        return fs;
    }

    private static QteWebInteractionService CreateWebService(
        FileSystemManager fs,
        QteSceneServiceHooks hooks) =>
        CreateWebService(fs, hooks, out _);

    private static QteWebInteractionService CreateWebService(
        FileSystemManager fs,
        QteSceneServiceHooks hooks,
        out StateManager stateManager)
    {
        var settings = new GameSettings();
        stateManager = new StateManager(
            fs,
            settings,
            NullLogger<StateManager>.Instance);
        var characteristics = new CharacteristicsService(
            fs,
            stateManager,
            NullLogger<CharacteristicsService>.Instance);
        var qte = new QteSceneService(
            fs,
            settings,
            characteristics,
            null!,
            null!,
            new StateDistributor(fs, NullLogger<StateDistributor>.Instance),
            new ValidationService(fs, NullLogger<ValidationService>.Instance),
            new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance),
            stateManager,
            NullLogger<QteSceneService>.Instance,
            inputSource: null,
            hooks);
        return new QteWebInteractionService(
            fs,
            qte,
            new BrowserLocalWriteCoordinator(
                fs,
                new LocalUiSessionLockService(fs)));
    }

    private static QteSceneService.QteOffer BuildTerminalOffer() =>
        new()
        {
            QteId = "browser_generation_fencing",
            Title = "Проверка транзакции QTE",
            StartChapterId = "start",
            Chapters =
            [
                new QteSceneService.QteChapter
                {
                    ChapterId = "start",
                    Title = "Финальный выбор",
                    Actions =
                    [
                        new QteSceneService.QteAction
                        {
                            ActionId = "finish",
                            Label = "Завершить",
                            Check = new QteSceneService.QteCheck
                            {
                                Type = "BranchChoice",
                                BaseDifficulty = 1,
                                Config = JsonNode.Parse("""{ "choiceGrade": "success" }""")!.AsObject()
                            },
                            Routing = new QteSceneService.QteRouting
                            {
                                Success = new QteSceneService.QteBranchTarget
                                {
                                    TerminalOutcomeId = "finished"
                                },
                                Partial = new QteSceneService.QteBranchTarget
                                {
                                    TerminalOutcomeId = "finished"
                                },
                                Fail = new QteSceneService.QteBranchTarget
                                {
                                    TerminalOutcomeId = "finished"
                                }
                            }
                        }
                    ]
                }
            ],
            TerminalOutcomes =
            [
                new QteSceneService.QteTerminalOutcome
                {
                    OutcomeId = "finished",
                    Title = "Завершено",
                    FinalNarrative = "Испытание завершено.",
                    GmSummary = "Проверка завершена.",
                    ResponseFragment = JsonNode.Parse(
                        """
                        {
                          "response": "Испытание завершено.",
                          "experienceGained": 5
                        }
                        """)!.AsObject()
                }
            ]
        };

    private static Task WriteOfferAsync(
        FileSystemManager fs,
        QteSceneService.QteOffer offer) =>
        fs.WriteFileAtomicAsync(
            QteSceneService.QteOfferPath,
            JsonSerializer.Serialize(
                offer,
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

    private static Task WriteActiveRuntimeAsync(
        FileSystemManager fs,
        QteSceneService.QteOffer offer)
    {
        var state = new QteSceneService.QteRuntimeState
        {
            PendingOffer = offer,
            ActiveScene = new QteSceneService.ActiveQteSceneState
            {
                Offer = offer,
                CurrentChapterId = offer.StartChapterId,
                AcceptedAtTurn = 12
            }
        };
        return fs.WriteFileAtomicAsync(
            QteSceneService.QteRuntimePath,
            JsonSerializer.Serialize(
                state,
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static async Task<QtePracticeWebStateDto> StartPracticeAsync(
        QteWebInteractionService service,
        string typeId,
        string difficultyId)
    {
        var catalog = await service.BuildPracticeStateAsync();
        return await service.StartPracticeAttemptAsync(
            new QtePracticeStartRequest(
                typeId,
                difficultyId,
                RequiredInteractionToken(catalog)));
    }

    private static async Task<DarenShowcaseWebStateDto> StartDarenAsync(
        QteWebInteractionService service)
    {
        var intro = await service.BuildDarenShowcaseStateAsync();
        return await service.StartDarenShowcaseAsync(
            RequiredInteractionToken(intro));
    }

    private static T DeserializeRequest<T>(JsonObject request) =>
        JsonSerializer.Deserialize<T>(
            request.ToJsonString(),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
        ?? throw new InvalidOperationException(
            $"Could not deserialize {typeof(T).Name} test request.");

    private static string RequiredInteractionToken<T>(T state)
    {
        var root = JsonSerializer.SerializeToNode(
            state,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!.AsObject();
        var token = root["interactionToken"]?.GetValue<string>();
        Assert.False(
            string.IsNullOrWhiteSpace(token),
            $"{typeof(T).Name} must publish a mutable interaction token.");
        return token!;
    }

    private static void AssertErrorCode<T>(T state, string expected)
    {
        var root = JsonSerializer.SerializeToNode(
            state,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!.AsObject();
        Assert.Equal(expected, root["errorCode"]?.GetValue<string>());
    }

    private static async Task<T> InvokeTokenMutationAsync<T>(
        QteWebInteractionService service,
        string methodName,
        string interactionToken)
    {
        var method = typeof(QteWebInteractionService).GetMethod(methodName)
            ?? throw new InvalidOperationException(
                $"Could not find {methodName}.");
        var arguments = method.GetParameters().Length == 0
            ? Array.Empty<object?>()
            : [interactionToken];
        return await ((Task<T>)method.Invoke(service, arguments)!)
            .WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static void CreateDirectoryJunction(string junctionPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(junctionPath)!);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c mklink /J \"{junctionPath}\" \"{targetPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start junction helper.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Failed to create test junction: exit code {process.ExitCode}.");
    }

    private static void CreateHardLink(string linkPath, string targetPath)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c mklink /H \"{linkPath}\" \"{targetPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start hard-link helper.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Failed to create test hard link: exit code {process.ExitCode}.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
