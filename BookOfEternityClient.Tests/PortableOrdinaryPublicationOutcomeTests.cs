using System.Text.Json;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Xunit.Abstractions;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableOrdinaryPublicationOutcomeTests : IDisposable
{
    private const string Member = "game_state/core/ordinary_outcome.bin";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-ordinary-outcome-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private readonly WarningLogger _logger = new();
    private Action<TrustedLocalPublicationPhase, int>? _observer;
    private readonly ITestOutputHelper _output;
    private string Active => Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");

    public PortableOrdinaryPublicationOutcomeTests(ITestOutputHelper output)
    {
        _output = output;
        _files = new FileSystemManager(_root, _logger, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { LocalPublicationObserver = (phase, index) => _observer?.Invoke(phase, index) });
        _files.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllBytes(_files.SessionGenerationPath,
            JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, GenerationId = Guid.NewGuid().ToString("N") }));
        File.WriteAllBytes(_files.ResolvePath(Member), [0x41]);
    }

    [Fact]
    public async Task ActualConditionalAppendUncertaintyRetainsTheOriginalDecision()
    {
        string generation;
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
            generation = _files.GetOrCreateSessionGeneration(lease);
        var forward = new InjectedFailure();
        CoordinatedStatePublicationUncertainException? original = null;
        var reached = 0;
        byte[]? journalAtCut = null;
        byte[]? published = null;
        byte[] foreign = [0x83, 0x97, 0xA1];
        bool Contains(Exception error) => ReferenceEquals(error, forward) ||
            error is AggregateException aggregate && aggregate.InnerExceptions.Any(Contains) ||
            error.InnerException is { } inner && Contains(inner);
        EventHandler<FirstChanceExceptionEventArgs> firstChance = (_, e) =>
        {
            if (e.Exception is CoordinatedStatePublicationUncertainException actual && Contains(actual))
                original ??= actual;
        };
        _observer = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
            Assert.Equal(0, index);
            AssertActualMember();
            reached++;
            journalAtCut = File.ReadAllBytes(Active);
            published = File.ReadAllBytes(_files.ResolvePath(Member));
            File.WriteAllBytes(_files.ResolvePath(Member), foreign);
            throw forward;
        };
        Exception? failure;
        AppDomain.CurrentDomain.FirstChanceException += firstChance;
        try
        {
            failure = await Record.ExceptionAsync(() =>
                _files.AppendFileAtomicIfCurrentSessionAsync(Member, "B", generation));
        }
        finally
        {
            _observer = null;
            AppDomain.CurrentDomain.FirstChanceException -= firstChance;
        }
        var retainedJournal = File.Exists(Active) ? File.ReadAllBytes(Active) : null;
        var retainedMember = File.ReadAllBytes(_files.ResolvePath(Member));
        _output.WriteLine(JsonSerializer.Serialize(new
        {
            generation, reached, published, journalAtCut, retainedJournal, retainedMember,
            JournalSha256 = journalAtCut == null ? null : Convert.ToHexString(SHA256.HashData(journalAtCut)),
            OriginalUncertainty = original?.ToString(), Failure = failure?.ToString(),
            SameOriginal = ReferenceEquals(original, failure)
        }));
        Assert.Equal(1, reached);
        Assert.Equal(new byte[] { 0x41, 0x42 }, published);
        Assert.NotNull(original);
        Assert.Same(original, failure);
        Assert.Equal(journalAtCut, retainedJournal);
        Assert.Equal(foreign, retainedMember);
    }

    [Theory]
    [InlineData("write")]
    [InlineData("append")]
    [InlineData("delete")]
    [InlineData("compare-exchange")]
    public async Task ActualOrdinaryUncertaintyIsTypedAndRetainsTheOriginalDecision(string operation)
    {
        var reached = 0;
        byte[] unknown = [0xFF, 0x00];
        _observer = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
            Assert.Equal(0, index);
            AssertActualMember();
            reached++;
            File.WriteAllBytes(_files.ResolvePath(Member), unknown);
            throw new InjectedFailure();
        };
        var failure = await Record.ExceptionAsync(() => Mutate(operation));
        _observer = null;
        Assert.Equal(1, reached);
        Assert.Equal(unknown, File.ReadAllBytes(_files.ResolvePath(Member)));
        var retained = File.ReadAllBytes(Active);
        var fresh = new FileSystemManager(_root, _logger);
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await using var refused = await fresh.AcquireCanonicalWriteLeaseAsync();
        });
        Assert.Equal(retained, File.ReadAllBytes(Active));
        Assert.Equal(unknown, File.ReadAllBytes(_files.ResolvePath(Member)));
        Assert.IsType<CoordinatedStatePublicationUncertainException>(failure);
    }

    [Theory]
    [InlineData("write")]
    [InlineData("append")]
    [InlineData("delete")]
    [InlineData("compare-exchange")]
    public async Task ActualOrdinaryRolledBackFailureRetainsItsCauseAndCanRetry(string operation)
    {
        var reached = 0;
        _observer = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
            AssertActualMember();
            reached++;
            throw new InjectedFailure();
        };
        await Assert.ThrowsAsync<InjectedFailure>(() => Mutate(operation));
        _observer = null;
        Assert.Equal(1, reached);
        Assert.Equal(new byte[] { 0x41 }, File.ReadAllBytes(_files.ResolvePath(Member)));
        Assert.False(File.Exists(Active));
        await Mutate(operation);
        if (operation == "delete") Assert.False(File.Exists(_files.ResolvePath(Member)));
        else Assert.Equal(operation == "append" ? new byte[] { 0x41, 0x42 } : new byte[] { 0x42 },
            File.ReadAllBytes(_files.ResolvePath(Member)));
        Assert.False(File.Exists(Active));
    }

    [Fact]
    public async Task ActualCommittedDebtSurvivesWarningLoggerFailureWithoutFalseWriteFailure()
    {
        var reached = 0;
        _logger.ThrowWarning = true;
        _observer = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.Committed) return;
            reached++;
            throw new InjectedFailure();
        };
        var failure = await Record.ExceptionAsync(() => Mutate("write"));
        _observer = null;
        Assert.Equal(1, reached);
        Assert.Equal(1, _logger.WarningCalls);
        Assert.Equal(new byte[] { 0x42 }, File.ReadAllBytes(_files.ResolvePath(Member)));
        using (var active = JsonDocument.Parse(File.ReadAllBytes(Active)))
            Assert.True(active.RootElement.GetProperty("Committed").GetBoolean());
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
            Assert.Equal(new byte[] { 0x42 }, File.ReadAllBytes(_files.ResolvePath(Member)));
        Assert.False(File.Exists(Active));
        Assert.Null(failure);
    }

    private async Task Mutate(string operation)
    {
        switch (operation)
        {
            case "write": await _files.WriteFileAtomicBytesAsync(Member, [0x42]); break;
            case "append": await _files.AppendFileAtomicAsync(Member, "B"); break;
            case "delete": _files.DeleteFile(Member); break;
            case "compare-exchange":
                Assert.Equal(CanonicalFileMutationResult.Applied, await _files.CompareExchangeFileBytesAsync(Member, [0x41], [0x42]));
                break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
    private void AssertActualMember()
    {
        using var active = JsonDocument.Parse(File.ReadAllBytes(Active));
        Assert.Equal(_files.ResolvePath(Member), Assert.Single(active.RootElement.GetProperty("Members").EnumerateArray()).GetProperty("Path").GetString());
    }
    private sealed class InjectedFailure : Exception { }
    private sealed class WarningLogger : ILogger<FileSystemManager>
    {
        internal bool ThrowWarning;
        internal int WarningCalls;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Warning) return;
            WarningCalls++;
            if (ThrowWarning) throw new IOException("Controlled warning logger failure after committed publication.");
        }
    }
    public void Dispose()
    {
        _observer = null;
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        _output.WriteLine(JsonSerializer.Serialize(new { OwnedFixtureRoot = _root, OwnedFixtureRemoved = !Directory.Exists(_root) }));
        Assert.False(Directory.Exists(_root));
    }
}
