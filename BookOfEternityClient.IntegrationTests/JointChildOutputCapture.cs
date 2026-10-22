using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Stores complete decoded child text independently of disposable game fixture roots.
/// </summary>
internal sealed class JointChildOutputCapture
{
    /// <summary>
    /// Identifies decoded text re-encoded continuously as UTF8 without an added BOM.
    /// </summary>
    internal const string Representation = "decoded-text-reencoded-utf8-no-bom";
    private readonly object _context;
    private object? _identity, _outcome;
    private readonly string _fixtureRoot;
    private readonly Func<string, (long Bytes, string Hash)> _inspect;
    private readonly Action<string, string> _writeManifest;
    private readonly ConcurrentQueue<(string Stage, Exception Error)> _errors = new();
    private readonly Output _stdout, _stderr;
    private volatile bool _finished;
    private readonly object _manifestGate = new();
    private int _appliedFailures;
    private bool _finalizationAttempted;
    private Task? _deferredFinalization;

    /// <summary>
    /// Prepares exclusive output artifacts before a child can start.
    /// </summary>
    /// <param name="fixtureRoot">
    /// The disposable fixture directory; artifacts must be outside it.
    /// </param>
    /// <param name="context">
    /// Parent-known source, run and role metadata; never child-derived authority.
    /// </param>
    /// <param name="artifactParent">
    /// Optional independent test artifact directory, taking priority over environment configuration.
    /// A <see langword="null"/> value selects the joint-child-output directory under
    /// BOE_TEST_DIAGNOSTIC_ROOT when provided, or under the repository TestResults directory otherwise.
    /// </param>
    /// <param name="openWriter">
    /// Optional storage fault seam; null creates UTF8-noBOM AutoFlush writers.
    /// </param>
    /// <param name="inspect">
    /// Optional bounded file-inspection seam; null reads actual length and SHA256.
    /// </param>
    /// <param name="writeManifest">
    /// Optional manifest fault seam; null atomically replaces a small owned manifest.
    /// </param>
    internal JointChildOutputCapture(string fixtureRoot, object context, string? artifactParent = null,
        Func<string, TextWriter>? openWriter = null, Func<string, (long Bytes, string Hash)>? inspect = null,
        Action<string, string>? writeManifest = null)
    {
        _context = context;
        _fixtureRoot = Path.GetFullPath(fixtureRoot);
        _inspect = inspect ?? Inspect;
        _writeManifest = writeManifest ?? WriteManifest;
        var parent = Path.GetFullPath(artifactParent ?? ResolveDefaultArtifactParent());
        var fixture = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fixtureRoot));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (parent.Equals(fixture, comparison) || parent.StartsWith(fixture + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Child output artifacts must be outside the disposable fixture.");
        DirectoryPath = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        openWriter ??= path => new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false), 4096) { AutoFlush = true };
        TextWriter? first = null, second = null;
        try
        {
            _stdout = new(Path.Combine(DirectoryPath, "stdout.utf8"), first = openWriter(Path.Combine(DirectoryPath, "stdout.utf8")));
            _stderr = new(Path.Combine(DirectoryPath, "stderr.utf8"), second = openWriter(Path.Combine(DirectoryPath, "stderr.utf8")));
            _writeManifest(Path.Combine(DirectoryPath, "initial.json"), Serialize());
        }
        catch (Exception primary)
        {
            foreach (var writer in new[] { first, second })
                try { writer?.Dispose(); }
                catch (Exception error) when (FinitePipeOutput.Recoverable(error))
                { JointInterfaceCrashProcessSupport.PreserveFailure(primary, error, "capture-preparation-close"); }
            throw;
        }
    }

    /// <summary>
    /// Resolves the default child artifact parent without creating directories or opening output files.
    /// </summary>
    /// <returns>
    /// The absolute joint-child-output directory under BOE_TEST_DIAGNOSTIC_ROOT when provided,
    /// or under the repository TestResults directory otherwise. A provided empty, whitespace
    /// or nonabsolute root throws <see cref="InvalidDataException"/> before artifact creation.
    /// </returns>
    internal static string ResolveDefaultArtifactParent()
    {
        var requestedRoot = Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT");
        if (requestedRoot is not null &&
            (string.IsNullOrWhiteSpace(requestedRoot) || !Path.IsPathFullyQualified(requestedRoot)))
            throw new InvalidDataException("BOE_TEST_DIAGNOSTIC_ROOT must be a nonempty absolute path when provided.");
        return Path.GetFullPath(requestedRoot is null
            ? Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-child-output")
            : Path.Combine(requestedRoot, "joint-child-output"));
    }

    /// <summary>
    /// Gets the exclusive external artifact directory, including incomplete evidence.
    /// </summary>
    internal string DirectoryPath { get; }
    /// <summary>
    /// Gets the continuous standard-output destination supplied before native drains start.
    /// </summary>
    internal TextWriter StandardOutput => _stdout.Writer;
    /// <summary>
    /// Gets the independent standard-error destination.
    /// </summary>
    internal TextWriter StandardError => _stderr.Writer;
    /// <summary>
    /// Gets the current complete or explicitly incomplete standard-output reference.
    /// </summary>
    internal JointChildOutputReference OutputReference => _stdout.Reference;
    /// <summary>
    /// Gets the current complete or explicitly incomplete standard-error reference.
    /// </summary>
    internal JointChildOutputReference ErrorReference => _stderr.Reference;
    /// <summary>
    /// Reports whether evidence needs retention because either artifact or metadata is incomplete.
    /// </summary>
    internal bool EvidenceRetentionRequired => !_finished || !_errors.IsEmpty ||
        !OutputReference.PersistenceComplete || !ErrorReference.PersistenceComplete;

    /// <summary>
    /// Saves independently observed identity before the final test report exists.
    /// </summary>
    /// <param name="identity">
    /// Parent-observed original child or native owner identity.
    /// </param>
    internal void BindIdentity(object identity)
    {
        _identity = identity;
        SaveManifest();
    }

    /// <summary>
    /// Saves original process outcome independently of the potentially failing test report.
    /// </summary>
    /// <param name="outcome">
    /// Actual wait, exit, retirement and primary-error observations; never raw output.
    /// </param>
    internal void RecordOutcome(object outcome)
    {
        _outcome = outcome; SaveManifest();
    }

    /// <summary>
    /// Retains a reporting or finalization error in the independent artifact manifest.
    /// </summary>
    /// <param name="stage">
    /// The failed operation's name.
    /// </param>
    /// <param name="error">
    /// The original exception to retain without changing process observations.
    /// </param>
    internal void RecordFailure(string stage, Exception error)
    {
        _errors.Enqueue((stage, error)); SaveManifest();
    }

    /// <summary>
    /// Finalizes each settled stream independently; active drains retain their open destinations.
    /// </summary>
    /// <param name="stdout">
    /// The actual output drain result, or null while that drain remains unresolved.
    /// </param>
    /// <param name="stderr">
    /// The actual error drain result, or null while that drain remains unresolved.
    /// </param>
    /// <returns>
    /// Completion of flush, close, bounded hashing and small-manifest persistence attempts.
    /// </returns>
    internal async Task FinishAsync(FinitePipeDrainResult? stdout, FinitePipeDrainResult? stderr)
    {
        if (_finished) throw new InvalidOperationException("Capture already finalized.");
        await FinishOneAsync(_stdout, stdout, "stdout");
        await FinishOneAsync(_stderr, stderr, "stderr");
        _finalizationAttempted = true;
        _finished = _stdout.Finalized && _stderr.Finalized;
        SaveManifest();
    }

    /// <summary>
    /// Closes prepared destinations after a refusal that started no child or pipe reader.
    /// </summary>
    /// <param name="primary">
    /// The exact original configuration, budget or launch error.
    /// </param>
    /// <returns>
    /// Completion of incomplete-artifact finalization, with secondary failures attached to the original error.
    /// </returns>
    internal async Task FailBeforeLaunchAsync(Exception primary)
    {
        RecordOutcome(new { ChildStarted = false, PrimaryFailure = primary.ToString() });
        RecordFailure("prelaunch", primary);
        await FinishAsync(new(false, null, null), new(false, null, null));
        PreserveFailure(primary);
    }

    /// <summary>
    /// Retains pending destinations until the original drain tasks eventually finish.
    /// </summary>
    /// <param name="stdout">
    /// The original exclusive output drain task.
    /// </param>
    /// <param name="stderr">
    /// The independent original error drain task.
    /// </param>
    internal void ObserveDeferredCompletion(Task<FinitePipeDrainResult> stdout, Task<FinitePipeDrainResult> stderr)
    {
        if (_finished || _deferredFinalization != null) return;
        _deferredFinalization = CompleteDeferredAsync(stdout, stderr);
    }

    /// <summary>
    /// Finalizes late original drain results without creating native retirement or additional deadlines.
    /// </summary>
    /// <param name="stdout">
    /// The original output task, which may remain pending indefinitely.
    /// </param>
    /// <param name="stderr">
    /// The original error task, which may remain pending indefinitely.
    /// </param>
    /// <returns>
    /// Completion of late artifact finalization; ordinary failures remain in manifest evidence.
    /// </returns>
    private async Task CompleteDeferredAsync(Task<FinitePipeDrainResult> stdout, Task<FinitePipeDrainResult> stderr)
    {
        try
        {
            async Task<FinitePipeDrainResult> ObserveAsync(Task<FinitePipeDrainResult> task)
            {
                try { return await task; }
                catch (Exception error) when (FinitePipeOutput.Recoverable(error)) { return new(false, error, null); }
            }
            var results = await Task.WhenAll(ObserveAsync(stdout), ObserveAsync(stderr));
            await FinishAsync(results[0], results[1]);
        }
        catch (Exception error) when (FinitePipeOutput.Recoverable(error))
        {
            _errors.Enqueue(("deferred-finalization", error)); SaveManifest();
        }
    }

    /// <summary>
    /// Attaches previously unreported capture failures without replacing the first process or wait failure.
    /// </summary>
    /// <param name="primary">
    /// The existing original failure, or null if capture supplies the first failure.
    /// </param>
    /// <returns>
    /// The same original exception with additional errors, a new capture failure, or null on success.
    /// </returns>
    internal Exception? PreserveFailure(Exception? primary)
    {
        var snapshot = _errors.ToArray();
        foreach (var (stage, error) in snapshot.Skip(_appliedFailures))
            primary = JointInterfaceCrashProcessSupport.PreserveFailure(primary, error, stage);
        _appliedFailures = snapshot.Length;
        return primary;
    }

    /// <summary>
    /// Finalizes only a positively settled drain without altering its EOF observation.
    /// </summary>
    /// <param name="output">
    /// The independent stream destination.
    /// </param>
    /// <param name="drain">
    /// The completed drain observation, or null while active.
    /// </param>
    /// <param name="stage">
    /// The stream name used in failure evidence.
    /// </param>
    /// <returns>
    /// Completion of independent flush, close and file inspection attempts.
    /// </returns>
    private async Task FinishOneAsync(Output output, FinitePipeDrainResult? drain, string stage)
    {
        if (drain == null || output.Finalized) return;
        var failures = new List<string>();
        void Record(Exception? error, string operation)
        {
            if (error == null) return;
            _errors.Enqueue((stage + "-" + operation, error)); failures.Add(operation + ": " + error);
        }
        Record(drain.ReadFailure, "read"); Record(drain.WriteFailure, "write");
        if (drain.ObserverFailure != null) _errors.Enqueue((stage + "-observe", drain.ObserverFailure));
        try { await output.Writer.FlushAsync(); }
        catch (Exception error) when (FinitePipeOutput.Recoverable(error)) { Record(error, "flush"); }
        try { await output.Writer.DisposeAsync(); }
        catch (Exception error) when (FinitePipeOutput.Recoverable(error)) { Record(error, "close"); }
        long? bytes = null; string? hash = null;
        try { var inspected = _inspect(output.Path); bytes = inspected.Bytes; hash = inspected.Hash; }
        catch (Exception error) when (FinitePipeOutput.Recoverable(error)) { Record(error, "hash"); }
        output.Reference = new(output.Path, Representation, drain.Eof, bytes, hash,
            drain.Eof && failures.Count == 0, drain.ReadFailure?.ToString(), failures.ToArray());
        output.Finalized = true;
    }

    /// <summary>
    /// Writes compact status independently of the test-framework callback.
    /// </summary>
    private void SaveManifest()
    {
        lock (_manifestGate)
        {
            try { _writeManifest(Path.Combine(DirectoryPath, "capture.json"), Serialize()); }
            catch (Exception error) when (FinitePipeOutput.Recoverable(error)) { _errors.Enqueue(("capture-manifest", error)); }
        }
    }

    /// <summary>
    /// Serializes references and parent metadata without embedding raw streams.
    /// </summary>
    /// <returns>
    /// Small structured evidence with explicit representation and completion status.
    /// </returns>
    private string Serialize() => JsonSerializer.Serialize(new
    {
        Kind = "joint_child_output_v1", FixtureRoot = _fixtureRoot, Context = _context, Identity = _identity, Outcome = _outcome,
        Stdout = _stdout.Reference, Stderr = _stderr.Reference,
        FinalizationAttempted = _finalizationAttempted, Errors = _errors.Select(x => new { x.Stage, Error = x.Error.ToString() }).ToArray()
    });

    /// <summary>
    /// Inspects finalized bytes without reading the whole file into memory.
    /// </summary>
    /// <param name="path">
    /// The owned output file.
    /// </param>
    /// <returns>
    /// Actual byte length and uppercase SHA256.
    /// </returns>
    private static (long Bytes, string Hash) Inspect(string path)
    {
        using var stream = File.OpenRead(path);
        return (stream.Length, Convert.ToHexString(SHA256.HashData(stream)));
    }

    /// <summary>
    /// Replaces only one small manifest in this capture's exclusive directory.
    /// </summary>
    /// <param name="path">
    /// The owned manifest path.
    /// </param>
    /// <param name="text">
    /// Compact reference metadata without child output content.
    /// </param>
    private static void WriteManifest(string path, string text)
    {
        var pending = path + ".pending";
        File.WriteAllText(pending, text, new UTF8Encoding(false));
        File.Move(pending, path, overwrite: true);
    }

    /// <summary>
    /// Keeps one original writer and its current data-only reference.
    /// </summary>
    /// <param name="path">
    /// The exclusive artifact filename.
    /// </param>
    /// <param name="writer">
    /// The continuous decoded-text writer.
    /// </param>
    private sealed class Output(string path, TextWriter writer)
    {
        /// <summary>
        /// Records whether this original writer was finalized after its actual drain settled.
        /// </summary>
        internal bool Finalized;
        /// <summary>
        /// Stores the exclusive artifact filename.
        /// </summary>
        internal readonly string Path = path;
        /// <summary>
        /// Stores the continuous original destination.
        /// </summary>
        internal readonly TextWriter Writer = writer;
        /// <summary>
        /// Stores explicit complete or incomplete file observations.
        /// </summary>
        internal JointChildOutputReference Reference = new(path, Representation, false, null, null, false, null, []);
    }
}

/// <summary>
/// References full or explicitly incomplete output; grants no native ownership evidence.
/// </summary>
/// <param name="Path">
/// The external artifact filename.
/// </param>
/// <param name="Representation">
/// The decoded-text UTF8 representation; never a raw-pipe-byte claim.
/// </param>
/// <param name="Eof">
/// Whether the exclusive reader positively observed EOF.
/// </param>
/// <param name="StoredBytes">
/// Actual inspected byte length, or null before successful inspection.
/// </param>
/// <param name="Sha256">
/// Actual inspected SHA256, or null when unavailable.
/// </param>
/// <param name="PersistenceComplete">
/// Whether EOF, all writes, flush, close and inspection succeeded.
/// </param>
/// <param name="ReadFailure">
/// The original read failure text, or null.
/// </param>
/// <param name="Failures">
/// All captured stream finalization and observation failures.
/// </param>
internal sealed record JointChildOutputReference(string Path, string Representation, bool Eof, long? StoredBytes, string? Sha256, bool PersistenceComplete, string? ReadFailure, string[] Failures);

/// <summary>
/// Keeps crash-path cleanup failures secondary to the original exception.
/// </summary>
internal sealed class JointChildFailureLedger
{
    private Exception? _primary;

    /// <summary>
    /// Records the first error or attaches a distinct secondary error to it.
    /// </summary>
    /// <param name="error">
    /// The exact process, assertion, reporting or cleanup exception.
    /// </param>
    /// <param name="stage">
    /// The operation name retained in secondary evidence.
    /// </param>
    internal void Record(Exception error, string stage)
    {
        if (!ReferenceEquals(_primary, error))
            _primary = JointInterfaceCrashProcessSupport.PreserveFailure(_primary, error, stage);
    }

    /// <summary>
    /// Runs owned cleanup without replacing an already propagating primary exception.
    /// </summary>
    /// <param name="cleanup">
    /// Cleanup or report action for this original owned container only.
    /// </param>
    internal void Cleanup(Action cleanup)
    {
        try { cleanup(); }
        catch (Exception error) when (FinitePipeOutput.Recoverable(error))
        {
            var originalExists = _primary != null;
            Record(error, "owned-cleanup");
            if (!originalExists) throw;
        }
    }
}
