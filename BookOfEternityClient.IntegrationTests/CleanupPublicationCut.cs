using System.Buffers.Binary;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

// Owned fixture observer only. Captures the actual publisher's decision; never fabricates CSP.
internal sealed class CleanupPublicationCut : IDisposable
{
    internal static readonly byte[] Foreign = "foreign-cleanup-publication-image"u8.ToArray();
    private readonly InvalidOperationException _forward = new("actual cleanup MemberPublished cut");
    private readonly EventHandler<FirstChanceExceptionEventArgs> _firstChance;
    private FileSystemManager? _files;
    private bool _closingLease;
    internal CleanupPublicationCut()
    {
        Hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = Published,
            BeforeCanonicalMutationBoundaryAsync = path => { if (Cuts != 0) LaterMutations.Add(path); return Task.CompletedTask; },
            AfterCanonicalReadInitialValidationAsync = path => { if (Cuts != 0) LaterReads.Add(path); return Task.CompletedTask; },
            LocalPublicationRecoveryObserver = (_, _) => { if (Cuts != 0) LaterRecovery++; },
            SessionOperationClosingAsync = () => { if (Cuts != 0) { Assert.False(_closingLease); _closingLease = true; } return Task.CompletedTask; },
            BeforeCanonicalWriteLockOpenAsync = () =>
            {
                if (Cuts != 0) { if (_closingLease) { ClosingLeases++; _closingLease = false; } else LaterLeases++; }
                return Task.CompletedTask;
            }
        };
        _firstChance = (_, e) =>
        {
            if (e.Exception is CoordinatedStatePublicationUncertainException actual && Contains(actual, _forward))
                OriginalUncertainty ??= actual;
        };
        AppDomain.CurrentDomain.FirstChanceException += _firstChance;
    }
    internal FileSystemManagerHooks Hooks { get; }
    internal bool Armed { get; set; }
    internal Func<string, JsonElement, bool>? Select { get; set; }
    internal Action? BeforeCut { get; set; }
    internal int Cuts { get; private set; }
    internal int Index { get; private set; } = -1;
    internal string? Target { get; private set; }
    internal byte[]? JournalAtCut { get; private set; }
    internal byte[]? PublishedBytes { get; private set; }
    internal CoordinatedStatePublicationUncertainException? OriginalUncertainty { get; private set; }
    internal List<string> LaterMutations { get; } = [];
    internal List<string> LaterReads { get; } = [];
    internal int LaterLeases { get; private set; }
    internal int ClosingLeases { get; private set; }
    internal int LaterRecovery { get; private set; }
    internal int LaterPublications { get; private set; }
    internal string JournalPath => Path.Combine(_files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
    internal void Attach(FileSystemManager files) => _files = files;
    internal static byte[]? ReadOptional(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
    internal static JsonDocument Metadata(byte[] bytes) => bytes.AsSpan().StartsWith("BOELP2\r\n"u8)
        ? JsonDocument.Parse(bytes.AsMemory(16, checked((int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(8, 8)))))
        : JsonDocument.Parse(bytes);
    private void Published(TrustedLocalPublicationPhase phase, int index)
    {
        if (!Armed || phase != TrustedLocalPublicationPhase.MemberPublished) return;
        if (Cuts != 0) { LaterPublications++; return; }
        var journal = File.ReadAllBytes(JournalPath);
        using var parsed = Metadata(journal);
        var member = parsed.RootElement.GetProperty("Members")[index];
        var target = member.GetProperty("Path").GetString()!;
        if (Select?.Invoke(target, member) != true) return;
        BeforeCut?.Invoke();
        Assert.False(parsed.RootElement.GetProperty("Committed").GetBoolean());
        Target = target; Index = index; JournalAtCut = journal; PublishedBytes = ReadOptional(target); Cuts++;
        File.WriteAllBytes(target, Foreign);
        throw _forward;
    }
    internal object Evidence() => new
    {
        Cuts, Index, Target, JournalAtCut, PublishedBytes,
        JournalSha256 = JournalAtCut == null ? null : Convert.ToHexString(SHA256.HashData(JournalAtCut)),
        RetainedJournal = ReadOptional(JournalPath), RetainedTarget = Target == null ? null : ReadOptional(Target),
        ActualUncertainty = OriginalUncertainty?.ToString(), LaterMutations, LaterReads, LaterLeases,
        ClosingLeases, PendingClosingLease = _closingLease, LaterRecovery, LaterPublications
    };
    internal void AssertReachedAndStopped()
    {
        Assert.Equal(1, Cuts); Assert.Equal(0, Index); Assert.NotNull(OriginalUncertainty);
        Assert.Equal(JournalAtCut, ReadOptional(JournalPath)); Assert.Equal(Foreign, ReadOptional(Target!));
        Assert.Empty(LaterMutations); Assert.Equal(0, LaterPublications); Assert.Equal(0, LaterRecovery); Assert.Equal(0, LaterLeases);
        Assert.False(_closingLease);
    }
    private static bool Contains(Exception error, Exception wanted) => ReferenceEquals(error, wanted) ||
        error is AggregateException aggregate && aggregate.InnerExceptions.Any(e => Contains(e, wanted)) ||
        error.InnerException is { } inner && Contains(inner, wanted);
    public void Dispose() => AppDomain.CurrentDomain.FirstChanceException -= _firstChance;
}

internal sealed class CleanupThrowingLogger<T>(CleanupPublicationCut cut) : Microsoft.Extensions.Logging.ILogger<T>
{
    internal int Throws { get; private set; }
    internal InvalidOperationException DiagnosticFailure { get; } = new("secondary cleanup diagnostic failed");
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId id,
        TState state, Exception? error, Func<TState, Exception?, string> formatter)
    {
        if (cut.Cuts == 0) return;
        Throws++;
        throw DiagnosticFailure;
    }
}
