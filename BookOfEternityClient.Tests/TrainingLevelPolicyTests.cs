using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class TrainingLevelPolicyTests(ITestOutputHelper output)
{
    private static readonly string[] Sources =
    ["game_state/player/experience.json", "game_state/core/player_status.json", "game_state/meta/soul_state.json"];

    [Theory]
    [InlineData("ReadTrainingExperienceLevel")]
    [InlineData("ReadTrainingSingleLevel")]
    public async Task OrdinaryContext_PrecedesMissingParsedPolicy(string method)
    {
        await WithOwner("{\"level\":2}", "{\"level\":7}", "{\"level\":9}", async (owner, _, _) =>
        {
            var actual = await owner.ComputeAsync();
            Assert.Equal(2, actual.PlayerLevel);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                Boundary = "ordinary_context_before_training_policy", Method = method,
                ActualPlayerLevel = actual.PlayerLevel, ExpectedPlayerLevel = 2
            }));
            Assert.Equal(2, ReadPolicy(method, "{\"level\":2}"));
        });
    }

    [Theory]
    [InlineData("{}", null)]
    [InlineData("{\"level\":2,\"playerLevel\":9}", 2)]
    [InlineData("{\"level\":\"2\",\"playerLevel\":9}", 9)]
    [InlineData("{\"level\":null,\"playerLevel\":9}", 9)]
    [InlineData("{\"level\":true,\"playerLevel\":9}", 9)]
    [InlineData("{\"LEVEL\":2,\"PlayerLevel\":9}", null)]
    [InlineData("{\"level\":0,\"playerLevel\":9}", 0)]
    [InlineData("{\"level\":-3,\"playerLevel\":9}", -3)]
    [InlineData("{\"level\":-2147483648}", int.MinValue)]
    [InlineData("{\"level\":2147483647}", int.MaxValue)]
    [InlineData("{\"level\":2,\"level\":5}", 5)]
    [InlineData("{\"level\":2,\"level\":\"2\",\"playerLevel\":4}", 4)]
    [InlineData("{\"playerLevel\":7}", 7)]
    public void ExperiencePolicy_ReturnsExactNumericPriority(string json, int? expected) =>
        Assert.Equal(expected, ReadPolicy("ReadTrainingExperienceLevel", json));

    [Theory]
    [InlineData("{}", null)]
    [InlineData("{\"level\":2,\"playerLevel\":9}", 2)]
    [InlineData("{\"playerLevel\":7}", null)]
    [InlineData("{\"level\":\"7\"}", null)]
    [InlineData("{\"level\":0}", 0)]
    [InlineData("{\"level\":-2}", -2)]
    [InlineData("{\"level\":-2147483648}", int.MinValue)]
    [InlineData("{\"level\":2147483647}", int.MaxValue)]
    [InlineData("{\"Level\":7}", null)]
    [InlineData("{\"level\":2,\"level\":false}", null)]
    public void SinglePolicy_ReturnsOnlyExactNumericLevel(string json, int? expected) =>
        Assert.Equal(expected, ReadPolicy("ReadTrainingSingleLevel", json));

    [Theory]
    [InlineData("ReadTrainingExperienceLevel", "{\"level\":2.5,\"playerLevel\":7}")]
    [InlineData("ReadTrainingExperienceLevel", "{\"level\":2147483648,\"playerLevel\":7}")]
    [InlineData("ReadTrainingSingleLevel", "{\"level\":2.5}")]
    [InlineData("ReadTrainingSingleLevel", "{\"level\":2147483648}")]
    public void ParsedPolicy_PreservesConversionFailure(string method, string json) =>
        Assert.Throws<FormatException>(() => ReadPolicy(method, json));

    [Theory]
    [InlineData("ReadTrainingExperienceLevel", "[]")]
    [InlineData("ReadTrainingExperienceLevel", "null")]
    [InlineData("ReadTrainingExperienceLevel", "2")]
    [InlineData("ReadTrainingSingleLevel", "[]")]
    [InlineData("ReadTrainingSingleLevel", "null")]
    [InlineData("ReadTrainingSingleLevel", "2")]
    public void ParsedPolicy_PreservesNonObjectFailure(string method, string json) =>
        Assert.Throws<InvalidOperationException>(() => ReadPolicy(method, json));

    [Theory]
    [InlineData("{\"level\":3,\"playerLevel\":9}", "{\"level\":4}", "{\"level\":5}", 3, "0")]
    [InlineData("{\"level\":\"3\",\"playerLevel\":7}", "{\"level\":4}", null, 7, "0")]
    [InlineData("{\"level\":0}", "{\"level\":4}", null, 0, "0")]
    [InlineData("{\"level\":-2}", "{\"level\":4}", null, -2, "0")]
    [InlineData("{}", "{\"level\":4}", "{\"level\":5}", 4, "0,1")]
    [InlineData("{", "{\"level\":4}", null, 4, "0,1")]
    [InlineData("{\"level\":2.5,\"playerLevel\":99}", "{\"level\":4}", null, 4, "0,1")]
    [InlineData("{\"level\":2147483648,\"playerLevel\":99}", "{\"level\":4}", null, 4, "0,1")]
    [InlineData("[]", "{\"level\":4}", null, 4, "0,1")]
    [InlineData(null, "{}", "{\"level\":5}", 5, "0,1,2")]
    [InlineData("", "not json", "{\"level\":5}", 5, "0,1,2")]
    [InlineData(null, null, null, 1, "0,1,2")]
    [InlineData(null, "{\"playerLevel\":4}", "{\"level\":7}", 7, "0,1,2")]
    [InlineData(null, "{\"level\":3.5}", "{\"level\":5}", 5, "0,1,2")]
    public async Task OrdinaryOwner_PreservesSourceOrderAndBytes(string? experience, string? status, string? soul,
        int expected, string sourceIndices)
    {
        await WithOwner(experience, status, soul, async (owner, reads, _) =>
        {
            Assert.Equal(expected, await ReadActualOwner(owner));
            Assert.Equal(sourceIndices.Split(',').Select(i => Sources[int.Parse(i)]), reads);
        });
    }

    [Fact]
    public async Task OrdinaryOwner_ReadFaultEscapesBeforeParseCatch()
    {
        var fault = new IOException("training-level-a1 actual ordinary read fault");
        await WithOwner("{\"level\":2}", "{\"level\":7}", "{\"level\":9}", async (owner, reads, armFault) =>
        {
            armFault(fault);
            Assert.Same(fault, await Assert.ThrowsAsync<IOException>(() => ReadActualOwner(owner)));
            Assert.Equal([Sources[0]], reads);
        });
    }

    private static int? ReadPolicy(string methodName, string json)
    {
        using var document = JsonDocument.Parse(json);
        var method = typeof(CharacteristicsService).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic,
            binder: null, types: [typeof(JsonElement)], modifiers: null);
        Assert.NotNull(method);
        Assert.Equal(typeof(int?), method.ReturnType);
        try { return (int?)method.Invoke(null, [document.RootElement]); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static async Task<int> ReadActualOwner(CharacteristicsService owner)
    {
        var method = typeof(CharacteristicsService).GetMethod("GetPlayerLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return await (Task<int>)method.Invoke(owner, null)!;
    }

    private async Task WithOwner(string? experience, string? status, string? soul,
        Func<CharacteristicsService, List<string>, Action<IOException>, Task> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-training-level-a1-" + Guid.NewGuid().ToString("N"));
        var reads = new List<string>();
        IOException? fault = null;
        var observing = false;
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    AfterCanonicalReadAttemptAsync = path =>
                    {
                        if (observing && Sources.Contains(path, StringComparer.Ordinal))
                        {
                            reads.Add(path);
                            if (fault is not null && path == Sources[0]) throw fault;
                        }
                        return Task.CompletedTask;
                    }
                });
            files.EnsureDirectoryStructure();
            var stats = Characteristics.All.ToDictionary(name => name, _ => 1);
            await files.WriteFileAtomicAsync("game_state/misc/characteristics.json", JsonSerializer.Serialize(stats));
            var sources = new[] { experience, status, soul };
            for (var i = 0; i < Sources.Length; i++)
                if (sources[i] is { } json) await files.WriteFileAtomicAsync(Sources[i], json);
            var before = Sources.Select(path => ReadPhysical(root, path)).ToArray();
            var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
            var owner = new CharacteristicsService(files, state, NullLogger<CharacteristicsService>.Instance);
            observing = true;
            await check(owner, reads, error => fault = error);
            for (var i = 0; i < Sources.Length; i++) Assert.Equal(before[i], ReadPhysical(root, Sources[i]));
            output.WriteLine(JsonSerializer.Serialize(new
            {
                Boundary = "ordinary_training_level_sources", Root = root, Reads = reads,
                Images = Sources.Select((path, i) => new { Path = path, Present = before[i] is not null,
                    Sha256 = before[i] is { } bytes ? Convert.ToHexString(SHA256.HashData(bytes)) : null })
            }));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                Boundary = "training_level_a1_cleanup", Root = root, Removed = !Directory.Exists(root)
            }));
            Assert.False(Directory.Exists(root));
        }
    }

    private static byte[]? ReadPhysical(string root, string path)
    {
        var physical = Path.Combine(root, path);
        return File.Exists(physical) ? File.ReadAllBytes(physical) : null;
    }
}
