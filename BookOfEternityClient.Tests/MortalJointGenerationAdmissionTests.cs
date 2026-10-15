using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

// These raw candidates exercise only a negative admission check. They are not
// signed originals and cannot qualify any joint completion or phase capability.
public sealed class MortalJointGenerationAdmissionTests : IDisposable
{
    private const string Input = "input/turn_request.json";
    private const string Sentinel = "game_state/world/admission-sentinel.json";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-joint-generation-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private readonly ITestOutputHelper _output;

    public MortalJointGenerationAdmissionTests(ITestOutputHelper output)
    {
        _output = output;
        _files = CreateFiles();
        _files.EnsureDirectoryStructure();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThrowingVetoPreservesGenerationAndClosesAdmission(bool existing)
    {
        var generation = existing ? await InitializeGenerationAsync() : null;
        await WarmLeaseAsync();
        var before = RawImages();
        var expected = new InvalidDataException("injected generation veto");
        var entered = false;
        var vetoCalls = 0;
        var failure = await Record.ExceptionAsync(() => SessionOperationContext.RunParticipatingCurrentSessionAsync(
            _files, () => { entered = true; return Task.FromResult(17); },
            beforeCurrentGeneration: _ => { vetoCalls++; throw expected; }));
        Assert.Same(expected, failure);
        Assert.Equal(1, vetoCalls);
        Assert.False(entered);
        AssertRawEqual(before, RawImages());

        // A separate intentional ordinary operation proves all refusal scopes
        // closed. Only this later operation may bootstrap an absent generation.
        var next = await SessionOperationContext.RunParticipatingCurrentSessionAsync(_files, () =>
        {
            Assert.True(SessionOperationContext.TryGetExpectedGeneration(_files.BasePath, out var bound));
            return Task.FromResult(bound);
        });
        Assert.Equal(existing ? generation : next, ReadGeneration());
        Assert.True(Guid.TryParseExact(next, "N", out _));
    }

    [Fact]
    public async Task DefaultBootstrapAndSuccessfulVetoRetainPhysicalGeneration()
    {
        Assert.False(File.Exists(_files.SessionGenerationPath));
        var generation = await SessionOperationContext.RunParticipatingCurrentSessionAsync(_files, async () =>
        {
            Assert.True(SessionOperationContext.TryGetExpectedGeneration(_files.BasePath, out var bound));
            await _files.WriteFileAtomicAsync(Sentinel, "first");
            return bound;
        });
        var bytes = File.ReadAllBytes(_files.SessionGenerationPath);
        var vetoCalls = 0;
        var result = await SessionOperationContext.RunParticipatingCurrentSessionAsync(_files, async () =>
        {
            await _files.WriteFileAtomicAsync(Sentinel, "second");
            return 17;
        }, beforeCurrentGeneration: lease =>
        {
            vetoCalls++;
            Assert.Equal(generation, _files.ReadExistingSessionGeneration(lease));
            return Task.CompletedTask;
        });
        Assert.Equal(17, result);
        Assert.Equal(1, vetoCalls);
        Assert.Equal(bytes, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal("second", File.ReadAllText(_files.ResolvePath(Sentinel)));
    }

    [Theory]
    [InlineData("no_marker", false)]
    [InlineData("v1", false)]
    [InlineData("invalid_family", false)]
    [InlineData("invalid_tuple", false)]
    [InlineData("no_headers", false)]
    [InlineData("foreign_headers", false)]
    [InlineData("manifest_only", true)]
    [InlineData("input_only", true)]
    [InlineData("manifest_match_input_foreign", true)]
    [InlineData("manifest_foreign_input_match", true)]
    public async Task ApplicabilityDeterminesWhetherMissingGenerationCanBootstrap(string scenario, bool applicable)
    {
        var generation = await InitializeGenerationAsync();
        var marker = Marker(generation);
        if (scenario == "v1") marker["schemaVersion"] = 1;
        if (scenario == "invalid_family") marker["jointTreatment"] = "invalid";
        if (scenario == "invalid_tuple") marker["turn"] = "43";
        if (scenario != "no_marker") await WriteJsonAsync(MortalQteDecisionPublication.StatePath, marker);
        if (scenario is not ("no_headers" or "input_only"))
            await WriteJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                Header(scenario is "foreign_headers" or "manifest_foreign_input_match"));
        if (scenario is not ("no_headers" or "manifest_only"))
            await WriteJsonAsync(Input, Header(scenario is "foreign_headers" or "manifest_match_input_foreign"));
        File.Delete(_files.SessionGenerationPath); // Deliberate owned fault after setup leases close.
        var before = RawImages();
        var entered = false;
        var failure = await Record.ExceptionAsync(() => SessionOperationContext.RunParticipatingCurrentSessionAsync(
            _files, () => { entered = true; return Task.FromResult(17); },
            beforeCurrentGeneration: lease => InvokeVetoAsync(_files, lease)));
        if (applicable)
        {
            Assert.Equal("Active joint continuation generation is missing; evidence retained.",
                Assert.IsType<InvalidDataException>(failure).Message);
            Assert.False(entered);
            AssertRawEqual(before, RawImages());
        }
        else
        {
            Assert.Null(failure);
            Assert.True(entered);
            Assert.NotNull(ReadGeneration());
        }
    }

    [Theory]
    [InlineData("physical_missing")]
    [InlineData("physical_malformed")]
    [InlineData("marker_absent")]
    [InlineData("marker_object")]
    [InlineData("marker_invalid")]
    [InlineData("marker_noncanonical")]
    [InlineData("marker_foreign")]
    [InlineData("equal")]
    public async Task ApplicableCandidateRefusesDamagedGenerationImages(string scenario)
    {
        var generation = await InitializeGenerationAsync();
        var marker = Marker(generation);
        switch (scenario)
        {
            case "marker_absent": marker.Remove("generation"); break;
            case "marker_object": marker["generation"] = new JsonObject(); break;
            case "marker_invalid": marker["generation"] = "not-a-generation"; break;
            case "marker_noncanonical": marker["generation"] = "ABCDEF0123456789ABCDEF0123456789AB"; break;
            case "marker_foreign": marker["generation"] = Guid.NewGuid().ToString("N"); break;
        }
        await WriteJsonAsync(MortalQteDecisionPublication.StatePath, marker);
        await WriteJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath, Header());
        await WriteJsonAsync(Input, Header());
        if (scenario == "physical_missing") File.Delete(_files.SessionGenerationPath);
        if (scenario == "physical_malformed") File.WriteAllText(_files.SessionGenerationPath, "{malformed");
        var before = RawImages();
        var entered = false;
        var failure = await Record.ExceptionAsync(() => SessionOperationContext.RunParticipatingCurrentSessionAsync(
            _files, () => { entered = true; return Task.FromResult(17); },
            beforeCurrentGeneration: lease => InvokeVetoAsync(_files, lease)));
        AssertRawEqual(before, RawImages());
        if (scenario == "equal")
        {
            Assert.Null(failure);
            Assert.True(entered);
            Assert.Equal(generation, ReadGeneration());
            // The callback is an ordinary test operation, not a completion owner.
            // Raw equality has issued no original/phase/receipt/story capability.
        }
        else
        {
            var refused = Assert.IsType<InvalidDataException>(failure);
            Assert.False(entered);
            if (scenario.StartsWith("marker_", StringComparison.Ordinal))
                Assert.Equal("Active joint continuation generation is invalid or differs; evidence retained.", refused.Message);
        }
    }

    [Fact]
    public async Task CandidateReadFailureCannotBecomeBootstrap()
    {
        var generation = await InitializeGenerationAsync();
        await WriteJsonAsync(MortalQteDecisionPublication.StatePath, Marker(generation));
        await WriteJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath, Header());
        await WriteJsonAsync(Input, Header());
        File.Delete(_files.SessionGenerationPath);
        var expected = new IOException("injected active header read failure");
        var readingVeto = false;
        var failures = 0;
        var hooked = CreateFiles(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (readingVeto && path == Input) { failures++; throw expected; }
                return Task.CompletedTask;
            }
        });
        var before = RawImages();
        var entered = false;
        var failure = await Record.ExceptionAsync(() => SessionOperationContext.RunParticipatingCurrentSessionAsync(
            hooked, () => { entered = true; return Task.FromResult(17); },
            beforeCurrentGeneration: lease => { readingVeto = true; return InvokeVetoAsync(hooked, lease); }));
        Assert.Same(expected, failure);
        Assert.Equal(1, failures);
        Assert.False(entered);
        AssertRawEqual(before, RawImages());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundCurrentPathSkipsVetoAndFencesReplacedGeneration(bool foreign)
    {
        var generation = await InitializeGenerationAsync();
        Dictionary<string, byte[]>? before = null;
        var vetoCalls = 0;
        var failure = await Record.ExceptionAsync(() => SessionOperationContext.RunBoundAsync(_files, generation, async () =>
        {
            DamageGeneration(foreign);
            before = RawImages();
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(_files, async () =>
            {
                await _files.WriteFileAtomicAsync(Sentinel, "forbidden");
                return 17;
            }, beforeCurrentGeneration: _ => { vetoCalls++; return Task.CompletedTask; });
        }));
        Assert.IsType<SessionReplacedException>(failure);
        Assert.Equal(0, vetoCalls);
        Assert.NotNull(before);
        AssertRawEqual(before!, RawImages());
        Assert.False(File.Exists(_files.ResolvePath(Sentinel)));
    }

    [Theory]
    [InlineData("same")]
    [InlineData("missing")]
    [InlineData("foreign")]
    public async Task ExpectedPathNeverBootstrapsMissingOrReplacedGeneration(string scenario)
    {
        var generation = await InitializeGenerationAsync();
        if (scenario != "same") DamageGeneration(scenario == "foreign");
        var before = RawImages();
        var failure = await Record.ExceptionAsync(() => SessionOperationContext.RunParticipatingExpectedSessionAsync(_files, generation, async () =>
        {
            await _files.WriteFileAtomicAsync(Sentinel, "expected");
            return 17;
        }));
        if (scenario == "same")
        {
            Assert.Null(failure);
            Assert.Equal(generation, ReadGeneration());
            Assert.Equal("expected", File.ReadAllText(_files.ResolvePath(Sentinel)));
        }
        else
        {
            Assert.IsType<SessionReplacedException>(failure);
            AssertRawEqual(before, RawImages());
        }
    }

    [Fact]
    public async Task ClosingReplacementRetainsEstablishedResultWithoutReplay()
    {
        await InitializeGenerationAsync();
        var hooked = CreateFiles(new FileSystemManagerHooks
        {
            SessionOperationClosingAsync = () => { File.Delete(_files.SessionGenerationPath); return Task.CompletedTask; }
        });
        var operations = 0;
        var vetoCalls = 0;
        var failure = await Record.ExceptionAsync(() => SessionOperationContext.RunParticipatingCurrentSessionAsync(
            hooked, () => { operations++; return Task.FromResult(17); },
            beforeCurrentGeneration: _ => { vetoCalls++; return Task.CompletedTask; }));
        var replaced = Assert.IsType<SessionReplacedException>(failure);
        Assert.Equal(17, Assert.IsType<int>(replaced.Data["EstablishedOperationResult"]));
        Assert.Equal(1, operations);
        Assert.Equal(1, vetoCalls);
        Assert.False(File.Exists(_files.SessionGenerationPath));
    }

    [Fact]
    public async Task ClosingScopeRejectsEscapedWriter()
    {
        await InitializeGenerationAsync();
        var generationBytes = File.ReadAllBytes(_files.SessionGenerationPath);
        var closingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseClosing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEscaped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hooked = CreateFiles(new FileSystemManagerHooks
        {
            SessionOperationClosingAsync = async () => { closingStarted.TrySetResult(); await releaseClosing.Task; }
        });
        Task? escaped = null;
        Exception? escapedFailure = null;
        var owner = SessionOperationContext.RunParticipatingCurrentSessionAsync(hooked, () =>
        {
            escaped = Task.Run(async () => { await releaseEscaped.Task; await hooked.WriteFileAtomicAsync(Sentinel, "escaped"); });
            return Task.FromResult(17);
        }, beforeCurrentGeneration: _ => Task.CompletedTask);
        try
        {
            await closingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(escaped);
            releaseEscaped.TrySetResult();
            escapedFailure = await Record.ExceptionAsync(() => escaped!.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            releaseEscaped.TrySetResult();
            releaseClosing.TrySetResult();
            await owner.WaitAsync(TimeSpan.FromSeconds(5));
            if (escaped is not null) await Record.ExceptionAsync(() => escaped.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        Assert.IsType<SessionReplacedException>(escapedFailure);
        Assert.Equal(generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.False(File.Exists(_files.ResolvePath(Sentinel)));
    }

    private FileSystemManager CreateFiles(FileSystemManagerHooks? hooks = null) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);

    private async Task<string> InitializeGenerationAsync()
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        return _files.GetOrCreateSessionGeneration(lease);
    }

    private async Task WarmLeaseAsync()
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        lease.EnsureNoPendingLocalDecision();
    }

    private string? ReadGeneration() => File.Exists(_files.SessionGenerationPath)
        ? FileSystemManager.ParseSessionGenerationText(File.ReadAllText(_files.SessionGenerationPath)) : null;

    private void DamageGeneration(bool foreign)
    {
        if (foreign) File.WriteAllText(_files.SessionGenerationPath,
            JsonSerializer.Serialize(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") }));
        else File.Delete(_files.SessionGenerationPath);
    }

    private Task WriteJsonAsync(string path, JsonObject value) => _files.WriteFileAtomicAsync(path, value.ToJsonString());

    private static JsonObject Marker(string generation) => new()
    {
        ["schemaVersion"] = 2, ["jointTreatment"] = new JsonObject(), ["progression"] = new JsonObject(),
        ["sessionId"] = "admission-session", ["requestId"] = "admission-request", ["turn"] = 43, ["generation"] = generation
    };

    private static JsonObject Header(bool foreign = false) => new()
    {
        ["sessionId"] = foreign ? "foreign-session" : "admission-session", ["requestId"] = "admission-request", ["turnNumber"] = 43
    };

    private static Task InvokeVetoAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        var method = typeof(GameEngine).GetMethod("ValidateMortalJointGenerationBeforeBootstrapAsync", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method!.Invoke(null, [files, lease]));
    }

    private Dictionary<string, byte[]> RawImages()
    {
        var lockStreams = new HashSet<string>(StringComparer.Ordinal)
        {
            Path.Combine(_files.RuntimeRootPath, "locks", "canonical-write.lock"),
            Path.Combine(_files.RuntimeRootPath, "locks", "session-lifecycle.lock"),
            Path.Combine(_files.RuntimeRootPath, "locks", "gm-main-owner.lock")
        };
        var paths = Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var result = paths.Where(path => !lockStreams.Contains(path)).ToDictionary(
            path => Path.GetRelativePath(_root, path), File.ReadAllBytes, StringComparer.Ordinal);
        result.Add("$files", JsonSerializer.SerializeToUtf8Bytes(paths.Select(path => Path.GetRelativePath(_root, path))));
        result.Add("$directories", JsonSerializer.SerializeToUtf8Bytes(Directory.GetDirectories(_root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_root, path)).Order(StringComparer.Ordinal)));
        return result;
    }

    private static void AssertRawEqual(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
    {
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
    }

    public void Dispose()
    {
        Exception? failure = null;
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (Exception error) { failure = error; }
        _output.WriteLine(JsonSerializer.Serialize(new { CleanupOwnedRoot = _root, OwnedFixtureRemoved = !Directory.Exists(_root), CleanupFailure = failure?.ToString() }));
        Assert.Null(failure);
        Assert.False(Directory.Exists(_root));
    }
}
