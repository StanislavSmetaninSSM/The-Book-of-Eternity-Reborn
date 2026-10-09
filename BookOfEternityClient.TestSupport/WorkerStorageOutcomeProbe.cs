using System.Buffers.Binary;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

// Test-support observation of the real common publisher, also usable by the
// stopped durable-worker fixture. It never constructs a storage outcome.
internal sealed class WorkerStorageOutcomeProbe : IDisposable
{
    internal static readonly byte[] Foreign = "foreign-worker-publication-image"u8.ToArray();
    private readonly Exception _cutFailure = new InvalidOperationException("original worker MemberPublished cut");
    private readonly EventHandler<FirstChanceExceptionEventArgs> _firstChance;
    private FileSystemManager? _files;
    private bool _closing;
    internal WorkerStorageOutcomeProbe()
    {
        Hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = Observe,
            LocalPublicationRecoveryObserver = (_, _) => { if (Cuts != 0) LaterRecovery++; },
            AfterCanonicalReadInitialValidationAsync = path =>
            { if (Cuts != 0) LaterReads.Add(path); return Task.CompletedTask; },
            BeforeCanonicalMutationBoundaryAsync = path =>
            {
                if (Cuts != 0)
                {
                    LaterMutations.Add(path);
                    throw new InvalidOperationException("fixture refuses later worker canonical mutation");
                }
                return Task.CompletedTask;
            },
            SessionOperationClosingAsync = () =>
            { if (Cuts != 0) { Require(!_closing, "nested closing observation"); _closing = true; } return Task.CompletedTask; },
            BeforeCanonicalWriteLockOpenAsync = () =>
            {
                if (Cuts == 0) return Task.CompletedTask;
                if (_closing) { ClosingLeases++; _closing = false; return Task.CompletedTask; }
                LaterLeases++;
                throw new InvalidOperationException("fixture refuses later worker canonical admission");
            }
        };
        _firstChance = (_, args) =>
        {
            if (args.Exception is CoordinatedStatePublicationUncertainException actual && Contains(actual, _cutFailure))
                OriginalUncertainty ??= actual;
        };
        AppDomain.CurrentDomain.FirstChanceException += _firstChance;
    }
    internal FileSystemManagerHooks Hooks { get; }
    internal Func<string, bool>? Select { get; set; }
    internal Action? BeforeCut { get; set; }
    internal FileSystemManager Files => _files ?? throw new InvalidOperationException("original worker FS not attached");
    internal Dictionary<string, byte[]?> Committed { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, byte[]?>? PriorAtCut { get; private set; }
    internal int Cuts { get; private set; }
    internal int Index { get; private set; } = -1;
    internal string? Target { get; private set; }
    internal byte[]? JournalAtCut { get; private set; }
    internal byte[]? PublishedBytes { get; private set; }
    internal CoordinatedStatePublicationUncertainException? OriginalUncertainty { get; private set; }
    internal List<string> LaterReads { get; } = [];
    internal List<string> LaterMutations { get; } = [];
    internal int LaterLeases { get; private set; }
    internal int ClosingLeases { get; private set; }
    internal int LaterRecovery { get; private set; }
    internal int LaterPublications { get; private set; }
    internal string JournalPath => Path.Combine(Files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
    internal void Attach(FileSystemManager files)
    {
        Require(_files == null || ReferenceEquals(_files, files), "probe changed original worker FS");
        _files = files;
    }
    internal static byte[]? Bytes(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
    internal static JsonDocument Metadata(byte[] bytes) => bytes.AsSpan().StartsWith("BOELP2\r\n"u8)
        ? JsonDocument.Parse(bytes.AsMemory(16, checked((int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(8, 8)))))
        : JsonDocument.Parse(bytes);
    private void Observe(TrustedLocalPublicationPhase phase, int index)
    {
        if (Cuts != 0) { if (phase == TrustedLocalPublicationPhase.MemberPublished) LaterPublications++; return; }
        if (phase is not (TrustedLocalPublicationPhase.Committed or TrustedLocalPublicationPhase.MemberPublished)) return;
        var journal = File.ReadAllBytes(JournalPath);
        using var parsed = Metadata(journal);
        if (phase == TrustedLocalPublicationPhase.Committed)
        {
            foreach (var member in parsed.RootElement.GetProperty("Members").EnumerateArray())
            {
                var path = member.GetProperty("Path").GetString()!;
                Committed[path] = Bytes(path);
            }
            return;
        }
        var target = parsed.RootElement.GetProperty("Members")[index].GetProperty("Path").GetString()!;
        if (Select?.Invoke(target) != true) return;
        Require(index == 0 && !parsed.RootElement.GetProperty("Committed").GetBoolean(), "cut is not original nonCommitted member0");
        BeforeCut?.Invoke();
        PriorAtCut = Committed.Where(pair => pair.Key != target).ToDictionary(pair => pair.Key, pair => Bytes(pair.Key), StringComparer.Ordinal);
        foreach (var pair in PriorAtCut) Require(Equal(pair.Value, Committed[pair.Key]), "prior commit changed before cut");
        Target = target; Index = index; JournalAtCut = journal; PublishedBytes = Bytes(target); Cuts++;
        File.WriteAllBytes(target, Foreign);
        throw _cutFailure;
    }
    internal object Evidence() => new
    {
        Cuts, Index, Target, JournalAtCut, PublishedBytes,
        JournalSha256 = JournalAtCut == null ? null : Convert.ToHexString(SHA256.HashData(JournalAtCut)),
        RetainedJournal = Bytes(JournalPath), RetainedTarget = Target == null ? null : Bytes(Target),
        ActualUncertainty = OriginalUncertainty?.ToString(), LaterReads, LaterMutations, LaterLeases,
        ClosingLeases, PendingClosingLease = _closing, LaterRecovery, LaterPublications,
        PriorAtCut, AfterImages = PriorAtCut?.ToDictionary(pair => pair.Key, pair => Bytes(pair.Key), StringComparer.Ordinal)
    };
    internal void RequireStopped()
    {
        Require(Cuts == 1 && Index == 0 && OriginalUncertainty != null, "original publication uncertainty was not reached");
        Require(Equal(JournalAtCut, Bytes(JournalPath)) && Equal(Foreign, Bytes(Target!)), "original uncertain journal/foreign bytes changed");
        Require(LaterReads.Count == 0 && LaterMutations.Count == 0 && LaterLeases == 0 && LaterRecovery == 0 && LaterPublications == 0 && !_closing,
            "canonical work continued after worker publication uncertainty");
        foreach (var pair in PriorAtCut!) Require(Equal(pair.Value, Bytes(pair.Key)), "earlier committed image changed after uncertainty");
    }
    internal static bool Equal(byte[]? first, byte[]? second) => first == null ? second == null : second != null && first.AsSpan().SequenceEqual(second);
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static bool Contains(Exception error, Exception wanted) => ReferenceEquals(error, wanted) ||
        error is AggregateException aggregate && aggregate.InnerExceptions.Any(e => Contains(e, wanted)) ||
        error.InnerException is { } inner && Contains(inner, wanted);
    public void Dispose() => AppDomain.CurrentDomain.FirstChanceException -= _firstChance;
}
