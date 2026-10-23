using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the actual QTE DATA boundary with tiny source files and replaceable calculation inputs.
/// </summary>
/// <param name="output">
/// Receiver for uncapped timing rows and owned fixture cleanup evidence.
/// </param>
public sealed class JointQteContributionDataTests(ITestOutputHelper output)
{
    /// <summary>
    /// Reuses a stable calculation while preserving every fresh entry, exit and source proof.
    /// </summary>
    /// <param name="eligible">
    /// Whether fresh live registry eligibility permits skipping the source.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RepeatedCalculationReusesOnlyData(bool eligible)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var scope = files.BeginRecoveryValidation(lease);
        fixture.Eligible = eligible;
        for (var i = 0; i < 8; i++)
        {
            var result = await fixture.ReadAsync(lease);
            Assert.Equal(new[] { "source-v1", "history-v1" }, result.Fields);
            Assert.Equal("source-v1", result.AfterImages["successor"]["source"]!.GetValue<string>());
            result.Fields[0] = "export damaged";
            result.AfterImages["successor"]["source"] = "export damaged";
        }
        Assert.Equal(16, fixture.BaselineChecks);
        Assert.Equal(eligible ? 1 : 8, fixture.Bodies);
        Assert.Equal(8, fixture.CompletedProofs);
    }

    /// <summary>
    /// Observes all physical source changes between read phases even when the root epoch is unchanged.
    /// </summary>
    /// <param name="change">
    /// Source bytes, absence or timestamp changed directly between phases.
    /// </param>
    [Theory]
    [InlineData("bytes")]
    [InlineData("same-time-bytes")]
    [InlineData("appearance")]
    [InlineData("timestamp")]
    public async Task PhysicalSourceDriftRunsOriginalCalculation(string change)
    {
        using var fixture = new Fixture(output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        using var scope = fixture.Files.BeginRecoveryValidation(lease);
        await fixture.ReadAsync(lease);
        await fixture.ReadAsync(lease);
        Assert.Equal(1, fixture.Bodies);
        var path = fixture.Files.ResolvePath("treatment-source.json");
        var time = File.GetLastWriteTimeUtc(path);
        if (change is "bytes" or "same-time-bytes")
        {
            File.WriteAllText(path, "source-v2");
            if (change == "same-time-bytes") File.SetLastWriteTimeUtc(path, time);
        }
        else if (change == "appearance") File.WriteAllText(fixture.Files.ResolvePath("optional.txt"), "appeared");
        else File.SetLastWriteTimeUtc(path, time.AddMinutes(1));
        var result = await fixture.ReadAsync(lease);
        Assert.Equal(change is "bytes" or "same-time-bytes" ? "source-v2" : "source-v1", result.Fields[0]);
        Assert.Equal(2, fixture.Bodies);
    }

    /// <summary>
    /// Tracks nested mutable inputs instead of trusting an unchanged object or retained seal.
    /// </summary>
    [Fact]
    public async Task StructuralHistoryChangeAndRestorationDoNotResurrectEntry()
    {
        using var fixture = new Fixture(output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        using var scope = fixture.Files.BeginRecoveryValidation(lease);
        await fixture.ReadAsync(lease);
        await fixture.ReadAsync(lease);
        Assert.Equal(1, fixture.Bodies);
        fixture.History["value"] = "history-v2";
        Assert.Equal("history-v2", (await fixture.ReadAsync(lease)).Fields[1]);
        fixture.History["value"] = "history-v1";
        Assert.Equal("history-v1", (await fixture.ReadAsync(lease)).Fields[1]);
        Assert.Equal(3, fixture.Bodies);
    }

    /// <summary>
    /// Live eligibility loss selects the original source after a successful cache warmup.
    /// </summary>
    [Fact]
    public async Task LiveEligibilityIsNeverCached()
    {
        using var fixture = new Fixture(output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        using var scope = fixture.Files.BeginRecoveryValidation(lease);
        await fixture.ReadAsync(lease);
        await fixture.ReadAsync(lease);
        Assert.Equal(1, fixture.Bodies);
        fixture.Eligible = false;
        await fixture.ReadAsync(lease);
        await fixture.ReadAsync(lease);
        Assert.Equal(3, fixture.Bodies);
    }

    /// <summary>
    /// Rejects a successful calculation if its structural DATA changes during an asynchronous source.
    /// </summary>
    [Fact]
    public async Task StructuralMutationDuringAwaitCannotEscape()
    {
        using var fixture = new Fixture(output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        using var scope = fixture.Files.BeginRecoveryValidation(lease);
        fixture.AfterSource = async () => { await Task.Yield(); fixture.History["value"] = "changed during await"; };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ReadAsync(lease));
        Assert.Equal(0, fixture.CompletedProofs);
        Assert.Equal(0, scope.CaptureState().Entries);
    }

    /// <summary>
    /// Preserves original failure identity and retires successful DATA computed before a failed exit.
    /// </summary>
    /// <param name="stage">
    /// Original entry, source or exit boundary that refuses.
    /// </param>
    [Theory]
    [InlineData("entry")]
    [InlineData("source")]
    [InlineData("exit")]
    public async Task FailedBoundaryRetiresDataAndKeepsFirstException(string stage)
    {
        using var fixture = new Fixture(output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        using var scope = fixture.Files.BeginRecoveryValidation(lease);
        var error = new InvalidDataException("original " + stage);
        fixture.BaselineError = stage == "source" ? null : error;
        fixture.FailBaselineAt = stage == "entry" ? 1 : 2;
        fixture.SourceError = stage == "source" ? error : null;
        Assert.Same(error, await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReadAsync(lease)));
        Assert.Equal(stage == "entry" ? 0 : 1, fixture.Bodies);
        Assert.Equal(stage == "exit" ? 2 : 1, fixture.BaselineChecks);
        Assert.Equal(0, fixture.CompletedProofs);
        Assert.Equal(0, scope.CaptureState().Entries);
        fixture.BaselineError = fixture.SourceError = null;
        var before = fixture.Bodies;
        await fixture.ReadAsync(lease);
        await fixture.ReadAsync(lease);
        Assert.Equal(before + 2, fixture.Bodies);
    }

    /// <summary>
    /// Explicit fresh work retires retained QTE DATA and a later operation starts independently.
    /// </summary>
    [Fact]
    public async Task FreshAndIndependentOperationsExecuteTheirOwnSources()
    {
        using var fixture = new Fixture(output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        using (fixture.Files.BeginRecoveryValidation(lease))
        {
            await fixture.ReadAsync(lease);
            await fixture.ReadAsync(lease);
            Assert.Equal(1, fixture.Bodies);
            using (fixture.Files.ForceFreshRecoveryValidation()) await fixture.ReadAsync(lease);
            await fixture.ReadAsync(lease);
            await fixture.ReadAsync(lease);
            Assert.Equal(3, fixture.Bodies);
        }
        using (fixture.Files.BeginRecoveryValidation(lease))
        {
            await fixture.ReadAsync(lease);
            await fixture.ReadAsync(lease);
            Assert.Equal(4, fixture.Bodies);
        }
    }

    /// <summary>
    /// Owns only a marked ordinary file fixture and independent mutable calculation DATA.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly string _parent = Path.GetFullPath("C:/Temp");
        private readonly string _root;
        private readonly List<object> _timings = [];
        private readonly object _owner = new();
        private readonly Func<OperationStructuralSnapshot?> _capture;

        /// <summary>
        /// Creates independent files and a stable structural capture policy without game objects.
        /// </summary>
        /// <param name="output">
        /// Receiver for every timing and exact cleanup result.
        /// </param>
        internal Fixture(ITestOutputHelper output)
        {
            _output = output;
            _root = Path.Combine(_parent, "boe-qte-data-" + _nonce);
            Directory.CreateDirectory(Path.Combine(_root, "game_session"));
            File.WriteAllText(Path.Combine(_root, ".fixture-owner"), _nonce);
            File.WriteAllText(Path.Combine(_root, "game_session", "treatment-source.json"), "source-v1");
            Files = new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks { OperationTimingObserver = (name, boundary, elapsed) =>
                    _timings.Add(new { name, boundary, Seconds = elapsed.TotalSeconds }) });
            _capture = () => new([History.ToJsonString()], [_owner]);
        }

        /// <summary>
        /// Gets this fixture's real file manager.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets the actual mutable calculation input inspected by the structural policy.
        /// </summary>
        internal JsonObject History { get; } = new() { ["value"] = "history-v1" };

        /// <summary>
        /// Gets or sets current live eligibility, independently of cached calculation DATA.
        /// </summary>
        internal bool Eligible { get; set; } = true;

        /// <summary>
        /// Gets the actual number of original calculation body executions.
        /// </summary>
        internal int Bodies { get; private set; }

        /// <summary>
        /// Gets the actual number of fresh baseline checks.
        /// </summary>
        internal int BaselineChecks { get; private set; }

        /// <summary>
        /// Gets the number of completed calculation/source-proof operations.
        /// </summary>
        internal int CompletedProofs { get; private set; }

        /// <summary>
        /// Gets or sets an optional action after source DATA is calculated, before its asynchronous return.
        /// </summary>
        internal Func<Task>? AfterSource { get; set; }

        /// <summary>
        /// Gets or sets the original source refusal, or null for successful calculation.
        /// </summary>
        internal Exception? SourceError { get; set; }

        /// <summary>
        /// Gets or sets the original baseline refusal, or null for successful checks.
        /// </summary>
        internal Exception? BaselineError { get; set; }

        /// <summary>
        /// Gets or sets the one-based baseline check at which the optional refusal occurs.
        /// </summary>
        internal int FailBaselineAt { get; set; }

        /// <summary>
        /// Executes the production QTE boundary with one small replaceable calculation source.
        /// </summary>
        /// <param name="lease">
        /// Exact original physical lease used for every read and proof.
        /// </param>
        /// <returns>
        /// Detached successful fields/images after all current proof boundaries.
        /// </returns>
        internal async Task<JointQtePreparedData> ReadAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            var result = await JointQteContributionData.RecomputeAsync(Files, lease, _owner, _capture,
                () => Eligible, () =>
                {
                    BaselineChecks++;
                    if (BaselineError != null && BaselineChecks == FailBaselineAt) throw BaselineError;
                    return Task.CompletedTask;
                }, async () =>
                {
                    Bodies++;
                    if (SourceError != null) throw SourceError;
                    var source = await Files.ReadFileAsync(lease, "treatment-source.json");
                    var optional = Files.ReadFileSync(lease, "optional.txt");
                    var data = new JointQtePreparedData([source, History["value"]!.GetValue<string>()],
                        new Dictionary<string, JsonObject> { ["successor"] = new() { ["source"] = source, ["optional"] = optional } });
                    if (AfterSource != null) await AfterSource();
                    return data;
                });
            CompletedProofs++;
            return result;
        }

        /// <summary>
        /// Emits every diagnostic row and deletes only the exact independently owned fixture.
        /// </summary>
        public void Dispose()
        {
            _output.WriteLine(JsonSerializer.Serialize(new { Bodies, BaselineChecks, CompletedProofs, Timings = _timings }));
            var scope = new TrustedLocalFileScope([_parent]);
            if (Path.GetDirectoryName(_root) != _parent || Path.GetFileName(_root) != "boe-qte-data-" + _nonce ||
                File.ReadAllText(scope.ValidateFile(Path.Combine(_root, ".fixture-owner"), false)) != _nonce)
                throw new InvalidDataException("QTE fixture ownership changed; root retained.");
            scope.DeleteOwnedTree(_root);
            Assert.False(Directory.Exists(_root));
        }
    }
}
