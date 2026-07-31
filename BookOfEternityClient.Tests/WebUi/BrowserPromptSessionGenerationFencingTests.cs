using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed class BrowserPromptSessionGenerationFencingTests : IDisposable
{
    private readonly string _rootPath =
        Path.Combine(
            Path.GetTempPath(),
            "boe-browser-prompt-generation-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SubmitAsync_PromptFromReplacedSession_DoesNotMutateReplacementSession()
    {
        var fs = CreateFileSystem();
        var service = CreatePromptService(fs);
        await SeedStatStateAsync(fs, strength: 1, unspent: 1, session: "A");
        var attached = await AttachStatPromptAsync(service, ownerId: "same-browser-owner");

        await fs.ClearGameStateAsync();
        await SeedStatStateAsync(fs, strength: 9, unspent: 7, session: "B");
        var expectedCharacteristics =
            await fs.ReadFileBytesAsync("game_state/misc/characteristics.json");
        var expectedPoints =
            await fs.ReadFileBytesAsync("game_state/player/stat_points.json");

        var result = await service.SubmitAsync(
            new ExplorerPromptSessionSubmitRequest(
                attached.InteractiveSession!.SessionId,
                new Dictionary<string, JsonNode?>
                {
                    ["stat_strength"] = JsonValue.Create("1")
                },
                OwnerId: "same-browser-owner"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        Assert.Contains(
            "Сессия заменена",
            UiTestTextCollector.CollectResultAndPromptText(result),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            expectedCharacteristics,
            await fs.ReadFileBytesAsync("game_state/misc/characteristics.json"));
        Assert.Equal(
            expectedPoints,
            await fs.ReadFileBytesAsync("game_state/player/stat_points.json"));
    }

    [Fact]
    public async Task CancelAsync_PromptFromReplacedSession_DoesNotReleaseReplacementLock()
    {
        var fs = CreateFileSystem();
        var service = CreatePromptService(fs);
        await SeedStatStateAsync(fs, strength: 1, unspent: 1, session: "A");
        var attached = await AttachStatPromptAsync(service, ownerId: "same-browser-owner");

        await fs.ClearGameStateAsync();
        await SeedStatStateAsync(fs, strength: 9, unspent: 7, session: "B");
        var replacementLockService = new LocalUiSessionLockService(fs);
        var replacementOwner = Owner("same-browser-owner", "Replacement browser owner");
        var acquired = await replacementLockService.AcquireOrRefreshAsync(
            replacementOwner,
            "Replacement session form");
        Assert.True(acquired.Acquired, acquired.BlockerMessage);
        var expectedLock = await fs.ReadFileBytesAsync(
            LocalUiSessionLockService.LockPath);

        var result = await service.CancelAsync(
            new ExplorerPromptSessionCancelRequest(
                attached.InteractiveSession!.SessionId,
                OwnerId: "same-browser-owner"));

        Assert.Equal(CommandExecutionState.Failed, result.State);
        Assert.Equal(
            expectedLock,
            await fs.ReadFileBytesAsync(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task AttachSession_ResultBuiltForReplacedGenerationDoesNotAdoptCurrentGeneration()
    {
        var fs = CreateFileSystem();
        var service = CreatePromptService(fs);
        await SeedStatStateAsync(fs, strength: 1, unspent: 1, session: "A");
        var capturedGeneration = await CaptureGenerationAsync(fs);
        var result = BuildStatPromptResult();

        await fs.ClearGameStateAsync();
        await SeedStatStateAsync(fs, strength: 9, unspent: 7, session: "B");

        var attached = await AttachWithCapturedGenerationAsync(
            service,
            result,
            new ExplorerWebCommandRequest(
                "/distribute",
                OwnerId: "same-browser-owner",
                OwnerLabel: "Browser prompt construction test"),
            capturedGeneration);

        Assert.Equal(CommandExecutionState.Failed, attached.State);
        Assert.Null(attached.InteractiveSession);
        Assert.Contains(
            "Сессия заменена",
            UiTestTextCollector.CollectResultAndPromptText(attached),
            StringComparison.OrdinalIgnoreCase);
        Assert.False(fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task GetSession_StaleSnapshotIsRemovedBeforeReturningReplacement()
    {
        var fs = CreateFileSystem();
        var service = CreatePromptService(fs);
        await SeedStatStateAsync(fs, strength: 1, unspent: 1, session: "A");
        var attached = await AttachStatPromptAsync(
            service,
            ownerId: "same-browser-owner");
        var sessionId = attached.InteractiveSession!.SessionId;

        await fs.ClearGameStateAsync();
        await SeedStatStateAsync(fs, strength: 9, unspent: 7, session: "B");

        var stale = await GetSessionAsync(service, sessionId);
        var removed = await GetSessionAsync(service, sessionId);

        Assert.Equal(CommandExecutionState.Failed, stale.State);
        Assert.Contains(
            "Сессия заменена",
            UiTestTextCollector.CollectResultAndPromptText(stale),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "не найдена",
            UiTestTextCollector.CollectResultAndPromptText(removed),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitAsync_InvalidStaleAnswersRemoveSnapshotBeforeValidation()
    {
        var fs = CreateFileSystem();
        var service = CreatePromptService(fs);
        await SeedStatStateAsync(fs, strength: 1, unspent: 1, session: "A");
        var attached = await AttachStatPromptAsync(
            service,
            ownerId: "same-browser-owner");
        var sessionId = attached.InteractiveSession!.SessionId;

        await fs.ClearGameStateAsync();
        await SeedStatStateAsync(fs, strength: 9, unspent: 7, session: "B");

        var stale = await service.SubmitAsync(
            new ExplorerPromptSessionSubmitRequest(
                sessionId,
                new Dictionary<string, JsonNode?>(),
                OwnerId: "same-browser-owner"));
        var removed = await GetSessionAsync(service, sessionId);

        Assert.Equal(CommandExecutionState.Failed, stale.State);
        Assert.Contains(
            "Сессия заменена",
            UiTestTextCollector.CollectResultAndPromptText(stale),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "не найдена",
            UiTestTextCollector.CollectResultAndPromptText(removed),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitAsync_StaleOwnerMismatchRemovesSnapshotBeforeOwnerValidation()
    {
        var fs = CreateFileSystem();
        var service = CreatePromptService(fs);
        await SeedStatStateAsync(fs, strength: 1, unspent: 1, session: "A");
        var attached = await AttachStatPromptAsync(
            service,
            ownerId: "same-browser-owner");
        var sessionId = attached.InteractiveSession!.SessionId;

        await fs.ClearGameStateAsync();
        await SeedStatStateAsync(fs, strength: 9, unspent: 7, session: "B");

        var stale = await service.SubmitAsync(
            new ExplorerPromptSessionSubmitRequest(
                sessionId,
                new Dictionary<string, JsonNode?>
                {
                    ["stat_strength"] = JsonValue.Create("1")
                },
                OwnerId: "different-browser-owner"));
        var removed = await GetSessionAsync(service, sessionId);

        Assert.Equal(CommandExecutionState.Failed, stale.State);
        Assert.Contains(
            "Сессия заменена",
            UiTestTextCollector.CollectResultAndPromptText(stale),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "другой вкладке",
            UiTestTextCollector.CollectResultAndPromptText(stale),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "не найдена",
            UiTestTextCollector.CollectResultAndPromptText(removed),
            StringComparison.OrdinalIgnoreCase);
    }

    private FileSystemManager CreateFileSystem()
    {
        Directory.CreateDirectory(_rootPath);
        var fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        return fs;
    }

    private static ExplorerWebPromptSessionService CreatePromptService(
        FileSystemManager fs)
    {
        var stateManager = new StateManager(
            fs,
            new GameSettings(),
            NullLogger<StateManager>.Instance);
        var lockService = new LocalUiSessionLockService(fs);
        var coordinator = new BrowserLocalWriteCoordinator(
            fs,
            lockService,
            TimeProvider.System);
        return new ExplorerWebPromptSessionService(
            fs,
            stateManager,
            lockService,
            TimeProvider.System,
            new BrowserMortalWorldWriteService(
                fs,
                coordinator,
                new ScenarioCoreService(
                    fs,
                    NullLogger<ScenarioCoreService>.Instance),
                TimeProvider.System),
            new BrowserAfterlifeWriteService(
                fs,
                stateManager,
                coordinator));
    }

    private static async Task<ExplorerCommandResult> AttachStatPromptAsync(
        ExplorerWebPromptSessionService service,
        string ownerId)
    {
        var result = BuildStatPromptResult();

        var attached = await service.AttachSessionIfNeededAsync(
            result,
            new ExplorerWebCommandRequest(
                "/distribute",
                OwnerId: ownerId,
                OwnerLabel: "Browser prompt generation test"));
        Assert.NotNull(attached.InteractiveSession);
        return attached;
    }

    private static ExplorerCommandResult BuildStatPromptResult() =>
        new()
        {
            Command = "/distribute",
            State = CommandExecutionState.RequiresInput,
            Prompts =
            [
                new UiTextInputPrompt
                {
                    Id = "stat_strength",
                    Prompt = "Сила: добавить очков",
                    DefaultValue = "0"
                }
            ]
        };

    private static async Task<string> CaptureGenerationAsync(
        FileSystemManager fs)
    {
        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        return fs.GetOrCreateSessionGeneration(writeLease);
    }

    private static async Task<ExplorerCommandResult> AttachWithCapturedGenerationAsync(
        ExplorerWebPromptSessionService service,
        ExplorerCommandResult result,
        ExplorerWebCommandRequest request,
        string expectedGeneration)
    {
        var method = typeof(ExplorerWebPromptSessionService)
            .GetMethods()
            .Where(candidate =>
                string.Equals(
                    candidate.Name,
                    nameof(ExplorerWebPromptSessionService.AttachSessionIfNeededAsync),
                    StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();
        var arguments = method.GetParameters().Length == 3
            ? new object?[] { result, request, expectedGeneration }
            : [result, request];
        return await ((Task<ExplorerCommandResult>)method.Invoke(
            service,
            arguments)!).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task<ExplorerCommandResult> GetSessionAsync(
        ExplorerWebPromptSessionService service,
        string sessionId)
    {
        var asyncMethod = typeof(ExplorerWebPromptSessionService)
            .GetMethod("GetSessionAsync");
        if (asyncMethod != null)
        {
            return await ((Task<ExplorerCommandResult>)asyncMethod.Invoke(
                service,
                [sessionId])!).WaitAsync(TimeSpan.FromSeconds(5));
        }

        return service.GetSession(sessionId);
    }

    private static async Task SeedStatStateAsync(
        FileSystemManager fs,
        int strength,
        int unspent,
        string session)
    {
        await fs.WriteFileAtomicAsync(
            "game_state/misc/characteristics.json",
            $$"""{ "strength": {{strength}}, "session": "{{session}}" }""");
        await fs.WriteFileAtomicAsync(
            "game_state/player/stat_points.json",
            $$"""{ "unspentStatPoints": {{unspent}}, "session": "{{session}}" }""");
    }

    private static LocalUiSessionLockOwner Owner(
        string ownerId,
        string ownerLabel) =>
        new(
            ownerId,
            "browser",
            ownerLabel,
            TimeSpan.FromSeconds(120));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for test-owned files.
        }
    }
}
