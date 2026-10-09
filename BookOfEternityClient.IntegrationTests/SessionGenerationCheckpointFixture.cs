using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

// Per-test probe of the original admission and real replacement publication.
// No external competing Clear, raw generation write, or new-generation binding.
internal sealed class SessionGenerationCheckpointFixture
{
    private readonly Action<string> _output;
    private readonly List<string> _laterReads = [];
    private readonly List<string> _laterMutations = [];
    private readonly Dictionary<string, byte[]> _sentinels = new(StringComparer.Ordinal);
    private byte[]? _rotationJournal;
    private byte[]? _committedGeneration;
    private string? _replacementGeneration;
    private string? _checkpoint;
    private int _checkpoints;
    private int _committedRotations;
    internal FileSystemManager Files { get; }
    internal string OriginalGeneration { get; private set; } = "";
    internal bool Rotated { get; private set; }
    internal int LaterInputReads { get; private set; }
    internal Action<string>? BeforeMutation { get; set; }

    private SessionGenerationCheckpointFixture(string root, Action<string> output)
    {
        _output = output;
        Files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = path =>
                { if (Rotated) _laterReads.Add("initial:" + path); return Task.CompletedTask; },
                AfterCanonicalReadAttemptAsync = path =>
                { if (Rotated) _laterReads.Add("attempt:" + path); return Task.CompletedTask; },
                BeforeCanonicalMutationBoundaryAsync = path =>
                { BeforeMutation?.Invoke(path); if (Rotated) _laterMutations.Add(path); return Task.CompletedTask; },
                LocalPublicationObserver = (phase, _) =>
                {
                    if (phase != TrustedLocalPublicationPhase.Committed) return;
                    var bytes = File.ReadAllBytes(Path.Combine(Files.RuntimeRootPath, "trusted-local-publication-v1", "active.json"));
                    using var metadata = CleanupPublicationCut.Metadata(bytes);
                    var before = metadata.RootElement.GetProperty("GenerationBefore").GetProperty("Id").GetString();
                    var after = metadata.RootElement.GetProperty("GenerationAfter").GetProperty("Id").GetString();
                    if (before == after) return;
                    Assert.Equal(OriginalGeneration, before);
                    Assert.False(string.IsNullOrWhiteSpace(after));
                    _committedRotations++;
                    _rotationJournal = bytes;
                    _committedGeneration = File.ReadAllBytes(Files.SessionGenerationPath);
                }
            });
    }

    internal static async Task<SessionGenerationCheckpointFixture> CreateAsync(string root, Action<string> output)
    {
        var probe = new SessionGenerationCheckpointFixture(root, output);
        // Read/create with an unobserved seed manager before arming generation observations.
        var seed = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await using var lease = await seed.AcquireCanonicalWriteLeaseAsync();
        probe.OriginalGeneration = seed.GetOrCreateSessionGeneration(lease);
        return probe;
    }

    internal async Task RotateAsync(string checkpoint, IReadOnlyDictionary<string, byte[]> sentinels)
    {
        Assert.False(Rotated);
        _checkpoint = checkpoint;
        _checkpoints++;
        await using var lifecycle = await Files.AcquireSessionLifecycleLeaseAsync();
        await using var lease = await Files.AcquireSessionReplacementWriteLeaseAsync(lifecycle);
        foreach (var (path, bytes) in sentinels)
        {
            await Files.WriteFileAtomicBytesAsync(lease, path, bytes);
            _sentinels.Add(path, File.ReadAllBytes(Files.ResolvePath(path)));
        }
        _replacementGeneration = Files.RotateSessionGeneration(lease);
        Rotated = true;
    }

    internal void Verify(Exception? failure, object? details = null)
    {
        var after = _sentinels.Keys.ToDictionary(path => path, path => CleanupPublicationCut.ReadOptional(Files.ResolvePath(path)), StringComparer.Ordinal);
        var generationAfter = File.ReadAllBytes(Files.SessionGenerationPath);
        _output(JsonSerializer.Serialize(new
        {
            kind = "f18-generation-checkpoint", root = Files.BasePath, _checkpoint, _checkpoints,
            OriginalGeneration, _replacementGeneration, _committedRotations, _rotationJournal,
            _committedGeneration, generationAfter, before = _sentinels, after,
            _laterReads, _laterMutations, LaterInputReads, OriginalTaskJoined = true,
            Failure = failure?.ToString(), details
        }));
        Assert.Equal(1, _checkpoints);
        Assert.Equal(1, _committedRotations);
        Assert.NotNull(_rotationJournal);
        Assert.True(Rotated);
        Assert.NotEqual(OriginalGeneration, _replacementGeneration);
        Assert.Equal(_committedGeneration, generationAfter);
        var replaced = Assert.IsType<SessionReplacedException>(failure);
        Assert.Equal(OriginalGeneration, replaced.ExpectedGeneration);
        Assert.Equal(_replacementGeneration, replaced.ActualGeneration);
        Assert.Empty(_laterReads);
        Assert.Empty(_laterMutations);
        Assert.Equal(0, LaterInputReads);
        foreach (var path in _sentinels.Keys) Assert.Equal(_sentinels[path], after[path]);
    }

    internal IConsoleInputSource ObserveInput(IConsoleInputSource source) => new ObservedInput(this, source);
    private sealed class ObservedInput(SessionGenerationCheckpointFixture probe, IConsoleInputSource source) : IConsoleInputSource
    {
        public bool IsScripted => source.IsScripted;
        public bool KeyAvailable => source.KeyAvailable;
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        { if (probe.Rotated) probe.LaterInputReads++; return source.ReadKey(intercept); }
        public string? ReadLine()
        { if (probe.Rotated) probe.LaterInputReads++; return source.ReadLine(); }
        public void AssertCompleted() => source.AssertCompleted();
    }

    internal static Dictionary<string, byte[]> ReplacementTerminalSentinels() => new(StringComparer.Ordinal)
    {
        ["input/turn_request.json"] = Encoding.UTF8.GetBytes("""{"sessionId":"replacement-session","requestId":"replacement-request","turnNumber":1}"""),
        ["ready/turn_complete.json"] = Encoding.UTF8.GetBytes("""{"sessionId":"replacement-session","requestId":"replacement-request","turnNumber":1,"status":"success"}""")
    };
}
