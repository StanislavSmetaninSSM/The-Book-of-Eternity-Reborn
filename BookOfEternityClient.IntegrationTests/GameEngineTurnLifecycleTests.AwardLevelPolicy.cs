using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private static readonly string[] AwardProbePaths =
    ["game_state/player/experience.json", "game_state/core/player_status.json", "game_state/player/stat_points.json",
        "game_state/player/computed_characteristics.json", "game_state/misc/characteristics.json"];

    [Theory]
    [InlineData("ReadAwardExperienceLevel", """{"level":2}""", """{"level":7}""", "e,p")]
    [InlineData("ReadAwardStatusLevel", """{"level":1}""", """{"level":2}""", "e,s,p")]
    public async Task AwardContextAndStrictBytes_PrecedeMissingParsedPolicy(string method, string experience,
        string status, string expectedReads)
    {
        // The genuine owner, all byte/read/state assertions, raw witness and strict cleanup
        // finish before the missing seam can fail. No postcondition is hidden behind RED.
        await AssertAwardOwnerAsync(method, experience, status, 2, expectedReads, null);
        using var document = JsonDocument.Parse("""{"level":2}""");
        var policy = typeof(CharacteristicsService).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic,
            binder: null, types: [typeof(JsonElement)], modifiers: null);
        Assert.NotNull(policy);
        Assert.Equal(typeof(int?), policy.ReturnType);
        Assert.Equal(2, (int?)policy.Invoke(null, [document.RootElement]));
    }

    [Theory]
    [InlineData("""{"level":3,"playerLevel":9}""", """{"level":4}""", 3, "e,p", null)]
    [InlineData("""{"level":"3","playerLevel":7}""", """{"level":4}""", 7, "e,p", null)]
    [InlineData("""{"level":1}""", """{"level":4}""", 4, "e,s,p", null)]
    [InlineData("""{"level":0}""", """{"level":4}""", 4, "e,s,p", null)]
    [InlineData("""{"level":-2}""", """{"level":4}""", 4, "e,s,p", null)]
    [InlineData("{}", """{"level":4}""", 4, "e,s,p", null)]
    [InlineData(null, null, 1, "e,s,p", null)]
    [InlineData(null, """{"playerLevel":7}""", 1, "e,s,p", null)]
    [InlineData("""{"level":0}""", "{}", 0, "e,s,p", null)]
    [InlineData("""{"level":-2}""", """{"level":"7"}""", -2, "e,s,p", null)]
    [InlineData("", """{"level":4}""", 1, "e", "json")]
    [InlineData("{", """{"level":4}""", 1, "e", "json")]
    [InlineData("[]", """{"level":4}""", 1, "e", "object")]
    [InlineData("""{"level":2.5,"playerLevel":7}""", """{"level":4}""", 1, "e", "format")]
    [InlineData("""{"level":2147483648,"playerLevel":7}""", """{"level":4}""", 1, "e", "format")]
    [InlineData("{}", """{"level":3.5}""", 1, "e,s", "format")]
    [InlineData("{}", "not json", 1, "e,s", "json")]
    [InlineData("{}", "null", 1, "e,s", "object")]
    public Task AwardOwner_PreservesFallbackAbortOrderAndBytes(string? experience, string? status,
        int expectedCurrent, string expectedReads, string? expectedError) =>
        AssertAwardOwnerAsync(nameof(AwardOwner_PreservesFallbackAbortOrderAndBytes), experience, status,
            expectedCurrent, expectedReads, expectedError);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public Task AwardOwner_RealReadFaultIsLoggedAndStops(int faultSource) =>
        AssertAwardOwnerAsync(nameof(AwardOwner_RealReadFaultIsLoggedAndStops),
            faultSource == 0 ? """{"level":2}""" : """{"level":1}""", """{"level":2}""",
            1, faultSource == 0 ? "e" : "e,s", "io", faultSource);

    [Fact]
    public async Task AwardOwner_ExistingRestartGuardHasStrictCleanup()
    {
        using var strict = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        await CheckLevelUpAsync_DoesNotAwardAlreadyProcessedLevelAfterEngineRestart();
    }

    [Fact]
    public Task AwardOwner_ExistingMarkPublicationUncertaintyEscapes() =>
        // The original exact mode already owns one strict cleanup receipt.
        ProgressionUnknown_OriginalConsumerPreservesDecisionAndStops("level_mark");

    private async Task AssertAwardOwnerAsync(string context, string? experience, string? status,
        int expectedCurrent, string expectedReads, string? expectedError, int? faultSource = null)
    {
        using var strict = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var observing = false;
        var reads = new List<string>();
        var mutations = new List<string>();
        var fault = faultSource is null ? null : new IOException("A2 real ordinary canonical read fault");
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                AfterCanonicalReadAttemptAsync = path =>
                {
                    if (observing)
                    {
                        reads.Add(path);
                        if (faultSource is { } index && path == AwardProbePaths[index]) throw fault!;
                    }
                    return Task.CompletedTask;
                },
                BeforeCanonicalMutationAsync = path =>
                {
                    if (observing) mutations.Add(path);
                    return Task.CompletedTask;
                }
            });
        var input = new AwardNoInput();
        var logger = new AwardObservingLogger();
        var engine = CreateGameEngine(input,
            settings => { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; },
            fileSystem: files, logger: logger);
        var supplied = new[] { experience, status,
            """{"unspentStatPoints":0,"levelUpStatPointsAwardedThroughLevel":10}""",
            """{"playerLevel":1,"unspentStatPoints":0,"characteristics":{"strength":1}}""",
            JsonSerializer.Serialize(Characteristics.All.ToDictionary(name => name, _ => 1)) };
        for (var i = 0; i < AwardProbePaths.Length; i++)
            if (supplied[i] is { } json) await files.WriteFileAtomicAsync(AwardProbePaths[i], json);
        byte[]? ReadPhysical(string path)
        {
            var physical = files.ResolvePath(path);
            return System.IO.File.Exists(physical) ? System.IO.File.ReadAllBytes(physical) : null;
        }
        var before = AwardProbePaths.Select(ReadPhysical).ToArray();
        for (var i = 0; i < supplied.Length; i++)
        {
            Assert.Equal(supplied[i] is not null, before[i] is not null);
            if (supplied[i] is { } json) Assert.Equal(FileSystemManager.EncodeUtf8WithPreamble(json), before[i]);
        }
        Assert.Equal(1, GetPrivateFieldValue<int>(engine, "_lastKnownLevel"));
        observing = true;
        await InvokePrivateTaskAsync(engine, "CheckLevelUpAsync");
        observing = false;
        var expectedPaths = expectedReads.Split(',').Select(token => AwardProbePaths[token switch
            { "e" => 0, "s" => 1, "p" => 2, _ => throw new InvalidOperationException(token) }]);
        Assert.Equal(expectedPaths, reads);
        var lastKnown = GetPrivateFieldValue<int>(engine, "_lastKnownLevel");
        Assert.Equal(expectedError is null ? Math.Max(1, expectedCurrent) : 1, lastKnown);
        if (expectedError is null) Assert.Empty(logger.Warnings);
        else
        {
            var actual = Assert.Single(logger.Warnings);
            switch (expectedError)
            {
                case "json": Assert.IsAssignableFrom<JsonException>(actual); break;
                case "format": Assert.IsType<FormatException>(actual); break;
                case "object": Assert.IsType<InvalidOperationException>(actual); break;
                case "io": Assert.Same(fault, actual); break;
                default: throw new InvalidOperationException(expectedError);
            }
        }
        Assert.Equal(0, input.Reads);
        Assert.Empty(mutations);
        for (var i = 0; i < AwardProbePaths.Length; i++) Assert.Equal(before[i], ReadPhysical(AwardProbePaths[i]));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
        {
            Boundary = "ordinary_award_level_owner", Context = context, Root = _rootPath,
            Supplied = supplied, Reads = reads, Mutations = mutations, InputReads = input.Reads,
            BeforeLastKnown = 1, AfterLastKnown = lastKnown, ExpectedError = expectedError,
            WarningTypes = logger.Warnings.Select(error => error?.GetType().FullName), SameReadFault = fault is not null && logger.Warnings.SingleOrDefault() == fault,
            Images = AwardProbePaths.Select((path, i) => new { Path = path, Present = before[i] is not null,
                Sha256 = before[i] is { } bytes ? Convert.ToHexString(SHA256.HashData(bytes)) : null })
        }));
    }

    private sealed class AwardNoInput : IConsoleInputSource
    {
        public int Reads { get; private set; }
        public bool IsScripted => true;
        public bool KeyAvailable => false;
        public ConsoleKeyInfo ReadKey(bool intercept = true) { Reads++; throw new InvalidOperationException("A2 owner must not enter distribution UI."); }
        public string? ReadLine() { Reads++; throw new InvalidOperationException("A2 owner must not read input."); }
    }

    private sealed class AwardObservingLogger : ILogger<GameEngine>
    {
        public List<Exception?> Warnings { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(exception);
        }
    }
}
