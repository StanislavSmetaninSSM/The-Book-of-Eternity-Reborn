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

        var accept = web.ResolveOfferDecisionAsync(new QteWebOfferDecisionRequest("accept"));
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

        var start = web.StartPracticeAttemptAsync(
            new QtePracticeStartRequest("MashInput", "normal"));
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
        var started = await web.StartPracticeAttemptAsync(
            new QtePracticeStartRequest("MashInput", "normal"));
        var action = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        await fs.ClearGameStateAsync();

        var result = await web.ResolvePracticeActionAsync(
            new QtePracticeActionRequest(action.ActionId, "success"));

        Assert.Equal("Failed", result.State);
        Assert.Contains("сесс", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedPracticeStart_AfterSessionReplacementDoesNotExposePreviousAttempt()
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        var started = await web.StartPracticeAttemptAsync(
            new QtePracticeStartRequest("MashInput", "normal"));
        Assert.Equal("Active", started.State);
        await fs.ClearGameStateAsync();

        var result = await web.StartPracticeAttemptAsync(
            new QtePracticeStartRequest("unknown-qte", "normal"));

        Assert.Equal("Failed", result.State);
        Assert.Null(result.ActiveScene);
        Assert.Equal("Catalog", (await web.BuildPracticeStateAsync()).State);
    }

    [Fact]
    public async Task DarenAttempt_CannotContinueAfterSessionGenerationChanges()
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        var started = await web.StartDarenShowcaseAsync();
        var action = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        await fs.ClearGameStateAsync();

        var result = await web.ResolveDarenShowcaseActionAsync(
            new DarenShowcaseActionRequest(action.ActionId, "success"));

        Assert.Equal("Failed", result.State);
        Assert.Contains("сесс", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAction_NewGameWinsBeforeQteLease_DoesNotRestoreOldBytesIntoReplacementSession()
    {
        var fs = CreateFileSystem();
        var web = CreateWebService(fs, new QteSceneServiceHooks());
        await WriteActiveRuntimeAsync(fs, BuildTerminalOffer());
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
                    new QteWebActionRequest("finish", "success"));
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
        var started = await web.StartPracticeAttemptAsync(
            new QtePracticeStartRequest("MashInput", "normal"));
        var action = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        var completed = await web.ResolvePracticeActionAsync(
            new QtePracticeActionRequest(action.ActionId, "success"));
        Assert.Equal("Completed", completed.State);
        failProjection = true;

        await Assert.ThrowsAsync<IOException>(() => web.RetryPracticeAttemptAsync());
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
        var started = await web.StartDarenShowcaseAsync();
        var action = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        failProjection = true;

        var resolve = web.ResolveDarenShowcaseActionAsync(
            new DarenShowcaseActionRequest(action.ActionId, "success"));
        await projectionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var exit = web.ExitDarenShowcaseAsync();
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

        var result = await web.ResolveOfferDecisionAsync(
            new QteWebOfferDecisionRequest(decision));

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

        var result = await web.ResolveActionAsync(
            new QteWebActionRequest("finish", "success"));

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
        var state = await web.StartDarenShowcaseAsync();
        Task<DarenShowcaseWebStateDto>? terminalAction = null;

        while (string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var action = Assert.Single(state.ActiveScene!.CurrentChapter!.Actions);
            var actionTask = web.ResolveDarenShowcaseActionAsync(
                new DarenShowcaseActionRequest(action.ActionId, "success"));
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
        var state = await web.StartDarenShowcaseAsync();

        while (string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var action = Assert.Single(state.ActiveScene!.CurrentChapter!.Actions);
            state = await web.ResolveDarenShowcaseActionAsync(
                new DarenShowcaseActionRequest(action.ActionId, "success"));
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
            new DarenShowcaseActionRequest(retryAction.ActionId, "success"));

        Assert.Equal("Completed", completed.State);
        Assert.True(File.Exists(profilePath));
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

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
