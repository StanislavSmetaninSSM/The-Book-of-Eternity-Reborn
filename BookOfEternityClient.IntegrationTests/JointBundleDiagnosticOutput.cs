using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Persists this component case's complete diagnostics independently of its owned game root.
/// Direct writer completion does not establish process settlement or pipe EOF.
/// </summary>
internal sealed class JointBundleDiagnosticOutput : ITestOutputHelper, IDisposable
{
    private readonly ITestOutputHelper _referenceOutput;
    private readonly StreamWriter _stream;
    private readonly TextWriter _writer;
    private readonly object _gate = new();
    private readonly List<Exception> _finishFailures = new();
    private long _attempted;
    private long _stored;
    private TimeSpan _writeElapsed;
    private bool _finished;
    private bool _writerClosed;
    private long? _bytes;
    private string? _sha256;
    private object? _fixtureClosing;

    /// <summary>
    /// Creates exclusive diagnostic files and registers identity before any game fixture is allocated.
    /// </summary>
    /// <param name="caseName">
    /// The nonempty actual test case name recorded in the initial identity.
    /// </param>
    /// <param name="referenceOutput">
    /// The actual xUnit receiver for compact paths, completion status and the closing receipt.
    /// </param>
    /// <param name="disposeWriterForControl">
    /// When <see langword="true"/>, disposes the actual records writer after registration to exercise deferred storage failure.
    /// Normal component cases leave this <see langword="false"/>.
    /// </param>
    /// <param name="repositoryRootForControl">
    /// An absolute per-call repository root for isolated path controls, or <see langword="null"/>
    /// for the actual repository. This does not change the process-wide repository identity.
    /// </param>
    internal JointBundleDiagnosticOutput(string caseName, ITestOutputHelper referenceOutput,
        bool disposeWriterForControl = false, string? repositoryRootForControl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseName);
        ArgumentNullException.ThrowIfNull(referenceOutput);
        var repositoryRoot = repositoryRootForControl ?? TestRepoPaths.RepoRoot;
        if (!Path.IsPathFullyQualified(repositoryRoot))
            throw new InvalidDataException("A component control repository root must be absolute.");
        _referenceOutput = referenceOutput;
        FixtureNonce = Guid.NewGuid().ToString("N");
        DiagnosticDirectory = Path.GetFullPath(Path.Combine(TestDiagnosticPaths.ResolveRoot(
            Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT"), repositoryRoot),
            "joint-bundle-component-diagnostics", FixtureNonce));
        ExpectedOwnedRoot = Path.GetFullPath(Path.Combine(repositoryRoot, "TestResults",
            "joint-bundle-component", FixtureNonce));
        if (Directory.Exists(DiagnosticDirectory) || File.Exists(DiagnosticDirectory))
            throw new IOException("Diagnostic destination already exists.");
        Directory.CreateDirectory(DiagnosticDirectory);
        _stream = new StreamWriter(new FileStream(RecordsPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false)) { AutoFlush = true };
        _writer = TextWriter.Synchronized(_stream);
        Timings = new JointCleanupProfileDiagnosticCapture(_writer);
        try
        {
            using var process = Process.GetCurrentProcess();
            var initial = new
            {
                Kind = "joint_bundle_component_initial_v1", Case = caseName, FixtureNonce,
                PID = Environment.ProcessId, ProcessStartUtc = process.StartTime.ToUniversalTime(),
                RegisteredUtc = DateTime.UtcNow, ExpectedOwnedRoot, RecordsPath, ReportPath,
                Assemblies = new[] { Pin(typeof(JointBundleDiagnosticOutput).Assembly), Pin(typeof(GameEngine).Assembly), Pin(typeof(TestRepoPaths).Assembly) }
            };
            using (var initialFile = new FileStream(InitialPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                JsonSerializer.Serialize(initialFile, initial, new JsonSerializerOptions { WriteIndented = true });
            _referenceOutput.WriteLine(JsonSerializer.Serialize(new
            {
                Kind = "JointBundleDiagnosticReference", FixtureNonce, DiagnosticDirectory, InitialPath,
                RecordsPath, ReportPath, ExpectedOwnedRoot
            }));
            if (disposeWriterForControl) _stream.Dispose();
        }
        catch
        {
            _writer.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the fresh nonce shared with the expected ordinary fixture namespace.
    /// </summary>
    internal string FixtureNonce { get; }

    /// <summary>
    /// Gets the independent directory retained after fixture cleanup.
    /// </summary>
    internal string DiagnosticDirectory { get; }

    /// <summary>
    /// Gets the game container expected for this nonce; construction does not create it.
    /// </summary>
    internal string ExpectedOwnedRoot { get; }

    /// <summary>
    /// Gets the exclusive initial identity file.
    /// </summary>
    internal string InitialPath => Path.Combine(DiagnosticDirectory, "initial.json");

    /// <summary>
    /// Gets the uncapped UTF-8 JSONL destination, readable while its writer remains open.
    /// </summary>
    internal string RecordsPath => Path.Combine(DiagnosticDirectory, "records.jsonl");

    /// <summary>
    /// Gets the completion report updated independently with the final fixture closing receipt.
    /// </summary>
    internal string ReportPath => Path.Combine(DiagnosticDirectory, "report.json");

    /// <summary>
    /// Gets the existing uncapped observer with deferred storage failures and per-boundary write costs.
    /// </summary>
    internal JointCleanupProfileDiagnosticCapture Timings { get; }

    /// <summary>
    /// Persists one complete non-timing message without copying its payload into xUnit.
    /// JSON messages retain their root data; other text is stored in a Message field.
    /// </summary>
    /// <param name="message">
    /// The full message, including embedded newlines, Unicode and escape characters.
    /// </param>
    public void WriteLine(string message)
    {
        lock (_gate)
        {
            _attempted++;
            var started = Stopwatch.GetTimestamp();
            try
            {
                if (_finished) throw new InvalidOperationException("Diagnostic records writer has finished.");
                string record;
                try
                {
                    using var document = JsonDocument.Parse(message);
                    record = JsonSerializer.Serialize(document.RootElement);
                }
                catch (JsonException)
                {
                    record = JsonSerializer.Serialize(new { Kind = "component_output", Message = message });
                }
                _writer.WriteLine(record);
                _stored++;
            }
            catch (Exception error)
            {
                Timings.RecordDiagnosticFailure(error, "component-output");
                throw;
            }
            finally { _writeElapsed += Stopwatch.GetElapsedTime(started); }
        }
    }

    /// <summary>
    /// Formats and persists the full non-timing message using xUnit's formatting contract.
    /// </summary>
    /// <param name="format">
    /// The composite message format.
    /// </param>
    /// <param name="args">
    /// All format arguments; no values are removed or shortened.
    /// </param>
    public void WriteLine(string format, params object[] args) =>
        WriteLine(string.Format(CultureInfo.CurrentCulture, format, args));

    /// <summary>
    /// Flushes and closes the actual writer, hashes its exact bytes and refuses any diagnostic failure.
    /// Repeated calls preserve the first completion outcome rather than erasing earlier errors.
    /// </summary>
    internal void Finish()
    {
        lock (_gate)
        {
            if (!_finished)
            {
                _finished = true;
                Attempt(() => _writer.Flush(), "writer-flush");
                Attempt(() => { _writer.Dispose(); _writerClosed = true; }, "writer-close");
                if (_writerClosed)
                    Attempt(() =>
                    {
                        using var input = File.OpenRead(RecordsPath);
                        _bytes = input.Length;
                        _sha256 = Convert.ToHexString(SHA256.HashData(input));
                    }, "records-hash");
                if (!Timings.Complete || _attempted != _stored)
                    Attempt(() => throw new InvalidDataException("Not every diagnostic attempt was stored."), "capture-incomplete");
                PersistReport();
                Attempt(() => _referenceOutput.WriteLine(JsonSerializer.Serialize(new
                {
                    Kind = "JointBundleDiagnosticFinished", RecordsPath, ReportPath,
                    WriterClosed = _writerClosed, StoredBytes = _bytes, Sha256 = _sha256,
                    DiagnosticComplete = IsComplete
                })), "completion-reference");
                // A compact-output failure must also remain in the durable report.
                if (_finishFailures.Count != 0) PersistReport();
            }
            ThrowIfFailed();
        }
    }

    /// <summary>
    /// Records the actual final fixture receipt after owner closing and the cleanup decision,
    /// even though the records writer has already closed.
    /// </summary>
    /// <param name="ownedRoot">
    /// The exact ordinary container owned by this fixture.
    /// </param>
    /// <param name="retained">
    /// Whether cleanup was withheld or failed.
    /// </param>
    /// <param name="removed">
    /// Whether the container is absent after the actual cleanup attempt.
    /// </param>
    /// <param name="failures">
    /// Every fixture closing failure in full exception text; an empty array means none was observed.
    /// </param>
    internal void RecordFixtureClosing(string ownedRoot, bool retained, bool removed, string[] failures)
    {
        lock (_gate)
        {
            if (!_finished) throw new InvalidOperationException("Finish diagnostics before fixture cleanup.");
            _fixtureClosing = new { Owned = ownedRoot, Retained = retained, Closing = removed, Failures = failures };
            Attempt(() => _referenceOutput.WriteLine(JsonSerializer.Serialize(new
            {
                Kind = "JointBundleFixtureClosing", Owned = ownedRoot, Retained = retained, Closing = removed,
                Failures = failures, ReportPath
            })), "fixture-closing-reference");
            PersistReport();
            ThrowIfFailed();
        }
    }

    /// <summary>
    /// Finishes diagnostics idempotently; a previous completion failure remains a failure.
    /// </summary>
    public void Dispose() => Finish();

    /// <summary>
    /// Gets whether every attempted record was stored and writer closing and hashing succeeded.
    /// This says nothing about test acceptance or process settlement.
    /// </summary>
    private bool IsComplete => _finished && _writerClosed && _sha256 is not null &&
        _attempted == _stored && Timings.Complete && _finishFailures.Count == 0;

    /// <summary>
    /// Pins an actually loaded assembly by identity, module and exact file bytes.
    /// </summary>
    /// <param name="assembly">
    /// The loaded test or production assembly.
    /// </param>
    /// <returns>
    /// Data identifying the loaded assembly and the bytes currently at its location.
    /// </returns>
    private static object Pin(Assembly assembly)
    {
        using var input = File.OpenRead(assembly.Location);
        return new { assembly.FullName, assembly.Location, ModuleVersionId = assembly.ManifestModule.ModuleVersionId,
            Sha256 = Convert.ToHexString(SHA256.HashData(input)) };
    }

    /// <summary>
    /// Attempts an independent closing step while retaining its original exception and stage.
    /// </summary>
    /// <param name="action">
    /// The diagnostic closing action.
    /// </param>
    /// <param name="stage">
    /// The exact stage associated with a failure.
    /// </param>
    private void Attempt(Action action, string stage)
    {
        try { action(); }
        catch (Exception error)
        {
            _finishFailures.Add(error);
            Timings.RecordDiagnosticFailure(error, stage);
        }
    }

    /// <summary>
    /// Writes a detached full ledger, including prior completion failures and the independent closing receipt.
    /// A failed report write is retained and retried once to include that original failure.
    /// </summary>
    private void PersistReport()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var pending = ReportPath + ".pending";
                File.WriteAllText(pending, JsonSerializer.Serialize(new
                {
                    Kind = "joint_bundle_component_report_v1", FixtureNonce, RecordsPath, InitialPath,
                    WriterClosed = _writerClosed, StoredBytes = _bytes, Sha256 = _sha256,
                    DiagnosticComplete = IsComplete, OutputAttempted = _attempted, OutputStored = _stored,
                    OutputSerializationAndWriteElapsed = _writeElapsed,
                    TimingAttempted = Timings.Attempted, TimingStored = Timings.Stored,
                    TimingComplete = Timings.Complete, TimingWriteCosts = Timings.GetWriteCosts(),
                    Failures = Timings.GetFailures().Select(row => new { row.Stage, Error = row.Error.ToString() }).ToArray(),
                    FixtureClosing = _fixtureClosing
                }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                File.Move(pending, ReportPath, overwrite: true);
                return;
            }
            catch (Exception error)
            {
                _finishFailures.Add(error);
                Timings.RecordDiagnosticFailure(error, "report-write");
            }
        }
    }

    /// <summary>
    /// Refuses false diagnostic completeness without dropping original exception objects.
    /// </summary>
    private void ThrowIfFailed()
    {
        var failures = Timings.GetFailures();
        if (failures.Length != 0 || !IsComplete)
            throw new AggregateException("Joint bundle diagnostics incomplete; retain the owned game root.",
                failures.Select(row => row.Error));
    }
}
