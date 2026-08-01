using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed class BrowserMortalWorldGenerationFencingTests : IDisposable
{
    private readonly string _rootPath =
        Path.Combine(Path.GetTempPath(), "boe-browser-mortal-generation-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CompanionDirective_WaitsForCanonicalLeaseBeforeReadingAndPreservesConcurrentNpcFields()
    {
        var contended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fs = CreateFileSystem(contended);
        await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_companion",
              "name": "Спутник",
              "progressionType": "Companion",
              "journalRevision": 1
            }
          ]
        }
        """);
        var generation = await GetGenerationAsync(fs);
        var service = CreateService(fs);

        FileSystemManager.CanonicalWriteLease? heldLease = await fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            var action = SessionOperationContext.RunBoundAsync(
                fs,
                generation,
                () => service.TryApplyAsync(
                    "/companion_directive",
                    Answers(
                        ("companion_id", "npc_companion"),
                        ("companion_directive", "держаться рядом")),
                    Owner("companion")));

            await contended.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var concurrent = JsonNode.Parse(
                (await fs.ReadFileAsync(heldLease, "game_state/npcs/npc_core.json"))!)!.AsObject();
            concurrent["UpdateNPCs"]![0]!["journalRevision"] = 2;
            concurrent["UpdateNPCs"]![0]!["concurrentThought"] = "Новая мысль";
            await fs.WriteFileAtomicAsync(
                heldLease,
                "game_state/npcs/npc_core.json",
                concurrent.ToJsonString());

            await heldLease.DisposeAsync();
            heldLease = null;

            var result = await action.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(result.Success, result.Message);
            var final = JsonNode.Parse((await fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
            var npc = final["UpdateNPCs"]![0]!.AsObject();
            Assert.Equal(2, npc["journalRevision"]!.GetValue<int>());
            Assert.Equal("Новая мысль", npc["concurrentThought"]!.GetValue<string>());
            Assert.Equal("держаться рядом", npc["playerCompanionDirective"]!.GetValue<string>());
        }
        finally
        {
            if (heldLease != null)
                await heldLease.DisposeAsync();
        }
    }

    [Fact]
    public async Task StatDistribution_WaitsForCanonicalLeaseBeforeReadingAndPreservesConcurrentCharacteristics()
    {
        var contended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fs = CreateFileSystem(contended);
        await fs.WriteFileAtomicAsync("game_state/player/stat_points.json", """
        {
          "unspentStatPoints": 3
        }
        """);
        await fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", """
        {
          "strength": 1,
          "concurrentRevision": 1
        }
        """);
        var generation = await GetGenerationAsync(fs);
        var service = CreateService(fs);

        FileSystemManager.CanonicalWriteLease? heldLease = await fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            var action = SessionOperationContext.RunBoundAsync(
                fs,
                generation,
                () => service.TryApplyAsync(
                    "/distribute",
                    Answers(("stat_strength", "1")),
                    Owner("stats")));

            await contended.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var concurrent = JsonNode.Parse(
                (await fs.ReadFileAsync(heldLease, "game_state/misc/characteristics.json"))!)!.AsObject();
            concurrent["concurrentRevision"] = 2;
            concurrent["worldGrantedBonus"] = 7;
            await fs.WriteFileAtomicAsync(
                heldLease,
                "game_state/misc/characteristics.json",
                concurrent.ToJsonString());

            await heldLease.DisposeAsync();
            heldLease = null;

            var result = await action.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(result.Success, result.Message);
            var final = JsonNode.Parse((await fs.ReadFileAsync("game_state/misc/characteristics.json"))!)!.AsObject();
            Assert.Equal(2, final["strength"]!.GetValue<int>());
            Assert.Equal(2, final["concurrentRevision"]!.GetValue<int>());
            Assert.Equal(7, final["worldGrantedBonus"]!.GetValue<int>());
        }
        finally
        {
            if (heldLease != null)
                await heldLease.DisposeAsync();
        }
    }

    [Fact]
    public async Task StatDistribution_WithHeldCanonicalLease_DoesNotReacquireTheLease()
    {
        var canonicalWriteLockContended = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fs = CreateFileSystem(canonicalWriteLockContended);
        await fs.WriteFileAtomicAsync(
            "game_state/player/stat_points.json",
            """{ "unspentStatPoints": 1 }""");
        await fs.WriteFileAtomicAsync(
            "game_state/misc/characteristics.json",
            """{ "strength": 1 }""");
        var service = CreateService(fs);

        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        var generation = fs.GetOrCreateSessionGeneration(writeLease);
        var result = await SessionOperationContext.RunBoundAsync(
            fs,
            generation,
            writeLease,
            () => service.TryApplyAsync(
                    writeLease,
                    "/distribute",
                    Answers(("stat_strength", "1")),
                    Owner("held-lease"))
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.False(
            canonicalWriteLockContended.Task.IsCompleted,
            "Held-lease stat distribution must not contend for or reacquire the canonical write lease.");
        Assert.True(result.Success, result.Message);
        var final = JsonNode.Parse(
            (await fs.ReadFileAsync(writeLease, "game_state/misc/characteristics.json"))!)!
            .AsObject();
        Assert.Equal(2, final["strength"]!.GetValue<int>());
    }

    [Fact]
    public async Task StatDistribution_NewGameWinsBeforeMortalLease_DoesNotWriteIntoReplacementSession()
    {
        var fs = CreateFileSystem(
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        await fs.WriteFileAtomicAsync(
            "game_state/player/stat_points.json",
            """{ "unspentStatPoints": 1, "session": "A" }""");
        await fs.WriteFileAtomicAsync(
            "game_state/misc/characteristics.json",
            """{ "strength": 1, "session": "A" }""");
        var service = CreateService(fs);
        var generation = await GetGenerationAsync(fs);
        var operationBound = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowOldOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var oldOperation = SessionOperationContext.RunBoundAsync(
            fs,
            generation,
            async () =>
            {
                operationBound.TrySetResult();
                await allowOldOperation.Task;
                return await service.TryApplyAsync(
                    "/distribute",
                    Answers(("stat_strength", "1")),
                    Owner("replacement-first"));
            });

        await operationBound.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fs.ClearGameStateAsync();
        byte[] replacementCharacteristics =
        [
            0xEF, 0xBB, 0xBF,
            (byte)'{', (byte)'"', (byte)'s', (byte)'t', (byte)'r', (byte)'e',
            (byte)'n', (byte)'g', (byte)'t', (byte)'h', (byte)'"', (byte)':',
            (byte)'9', (byte)',', (byte)'"', (byte)'s', (byte)'e', (byte)'s',
            (byte)'s', (byte)'i', (byte)'o', (byte)'n', (byte)'"', (byte)':',
            (byte)'"', (byte)'B', (byte)'"', (byte)'}'
        ];
        await fs.WriteFileAtomicBytesAsync(
            "game_state/misc/characteristics.json",
            replacementCharacteristics);
        await fs.WriteFileAtomicAsync(
            "game_state/player/stat_points.json",
            """{ "unspentStatPoints": 7, "session": "B" }""");

        allowOldOperation.TrySetResult();
        await Assert.ThrowsAsync<SessionReplacedException>(
            () => oldOperation.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(
            replacementCharacteristics,
            await fs.ReadFileBytesAsync("game_state/misc/characteristics.json"));
        Assert.Contains(
            "\"session\": \"B\"",
            await fs.ReadFileAsync("game_state/player/stat_points.json"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatDistribution_SecondWriteFailure_RestoresFirstFileByteForByte()
    {
        const string statPointsPath = "game_state/player/stat_points.json";
        const string characteristicsPath = "game_state/misc/characteristics.json";
        var fs = CreateFileSystem(
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        await fs.WriteFileAtomicAsync(
            statPointsPath,
            """
            {
              "unspentStatPoints": 1
            }
            """);
        await fs.WriteFileAtomicAsync(
            characteristicsPath,
            """
            {
              "strength": 1
            }
            """);
        var statPointsBefore = await fs.ReadFileBytesAsync(statPointsPath);
        var characteristicsBefore = await fs.ReadFileBytesAsync(characteristicsPath);
        var service = CreateService(fs);

        BrowserPromptWriteResult result;
        await using (var blocker = new FileStream(
                         fs.ResolvePath(statPointsPath),
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read))
        {
            result = await service.TryApplyAsync(
                "/distribute",
                Answers(("stat_strength", "1")),
                Owner("partial-write"));
        }

        Assert.False(result.Success);
        Assert.Equal(
            statPointsBefore,
            await fs.ReadFileBytesAsync(statPointsPath));
        Assert.Equal(
            characteristicsBefore,
            await fs.ReadFileBytesAsync(characteristicsPath));
    }

    private FileSystemManager CreateFileSystem(TaskCompletionSource contended)
    {
        Directory.CreateDirectory(_rootPath);
        var fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                CanonicalWriteLockContendedAsync = () =>
                {
                    contended.TrySetResult();
                    return Task.CompletedTask;
                }
            });
        fs.EnsureDirectoryStructure();
        return fs;
    }

    private static BrowserMortalWorldWriteService CreateService(FileSystemManager fs)
    {
        var coordinator = new BrowserLocalWriteCoordinator(
            fs,
            new LocalUiSessionLockService(fs),
            TimeProvider.System);
        return new BrowserMortalWorldWriteService(
            fs,
            coordinator,
            new ScenarioCoreService(fs, NullLogger<ScenarioCoreService>.Instance),
            TimeProvider.System);
    }

    private static async Task<string> GetGenerationAsync(FileSystemManager fs)
    {
        await using var lease = await fs.AcquireCanonicalWriteLeaseAsync();
        return fs.GetOrCreateSessionGeneration(lease);
    }

    private static Dictionary<string, JsonNode?> Answers(params (string Key, object Value)[] pairs)
    {
        var answers = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
            answers[key] = JsonValue.Create(value.ToString());
        return answers;
    }

    private static LocalUiSessionLockOwner Owner(string suffix) =>
        new($"browser-mortal-generation-{suffix}", "browser", "Browser mortal generation test", TimeSpan.FromSeconds(120));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Ignore test cleanup failures.
        }
    }
}
