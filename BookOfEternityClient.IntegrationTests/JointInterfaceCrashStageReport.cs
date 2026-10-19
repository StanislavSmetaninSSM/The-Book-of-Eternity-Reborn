using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Writes bounded scalar ordinary-test stages without changing game or native verdicts.
/// </summary>
internal sealed class JointInterfaceCrashStageReport
{
    private readonly TextWriter _writer;
    private readonly string _runId, _nonce;
    private readonly JointInterfaceCrashIdentity _identity;
    private readonly int _cut;
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly object _gate = new();
    private readonly HashSet<SessionFinalizationCheckpoint> _sourceCheckpoints = new();
    private int _records;
    private bool _outputClosed;

    /// <summary>
    /// Creates an independent stage writer for one child.
    /// </summary>
    /// <param name="writer">
    /// The existing child output stream; the reporter neither owns nor flushes it.
    /// </param>
    /// <param name="runId">
    /// The parent-created GUID N run identity, separate from native owner identity.
    /// </param>
    /// <param name="nonce">
    /// The independent GUID N child scenario nonce.
    /// </param>
    /// <param name="identity">
    /// The child's independently read platform identity.
    /// </param>
    /// <param name="cut">
    /// The original ordinary publication cut from zero through four.
    /// </param>
    internal JointInterfaceCrashStageReport(TextWriter writer, string runId, string nonce, JointInterfaceCrashIdentity identity, int cut)
    {
        _writer = writer;
        _runId = runId;
        _nonce = nonce;
        _identity = identity;
        _cut = cut;
    }

    /// <summary>
    /// Creates stages only for an ordinary child after its mode guard has accepted parent metadata.
    /// </summary>
    /// <param name="parent">
    /// Validated parent metadata with independently generated GUID N RunId and Nonce.
    /// </param>
    /// <param name="cut">
    /// The validated publication cut from zero through four.
    /// </param>
    /// <returns>
    /// An ordinary reporter, or <see langword="null"/> for diagnostic or integrity execution.
    /// </returns>
    internal static JointInterfaceCrashStageReport? CreateForOrdinary(JsonObject parent, int cut) =>
        parent["Diagnostic"]?.GetValue<bool>() == true || parent["Fault"] != null ? null :
        new(Console.Out, parent["RunId"]!.GetValue<string>(), parent["Nonce"]!.GetValue<string>(),
            JointInterfaceCrashProcessSupport.ReadCurrentIdentity(), cut);

    /// <summary>
    /// Gets the first bounded observer error type, or null when writing has not failed.
    /// </summary>
    internal string? OutputFailure { get; private set; }

    /// <summary>
    /// Records the first observation of each existing source finalization boundary.
    /// </summary>
    /// <param name="checkpoint">
    /// An existing engine checkpoint; unrelated checkpoints do not produce records.
    /// </param>
    internal void ObserveSourceCheckpoint(SessionFinalizationCheckpoint checkpoint)
    {
        var stage = checkpoint switch
        {
            SessionFinalizationCheckpoint.TerminalSignalInspectionLeaseAcquired => "source-inspection",
            SessionFinalizationCheckpoint.TerminalSignalSnapshotCapturedBeforeResolution => "source-capture",
            SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization => "source-validated",
            _ => null
        };
        if (stage is null) return;
        lock (_gate)
        {
            if (_sourceCheckpoints.Add(checkpoint)) Observe(stage, "enter");
        }
    }

    /// <summary>
    /// Emits one bounded stage observation without an external observer or flush wait.
    /// </summary>
    /// <param name="stage">
    /// A fixed ordinary-test stage name.
    /// </param>
    /// <param name="transition">
    /// The enter, return or error transition; the writer reserves limit for its terminal marker.
    /// </param>
    /// <param name="errorType">
    /// An optional exception type name, truncated to at most 256 characters; no exception messages or game payloads.
    /// </param>
    internal void Observe(string stage, string transition, string? errorType = null)
    {
        lock (_gate)
        {
            if (_outputClosed || _records >= 64) return;
            _records++;
            if (stage is not ("fixture" or "file-system" or "engine" or "preflight-lease" or "snapshot-refresh" or
                "late-first" or "finalization-before" or "finalization-after" or "late-repeat" or "exit" or "error" or
                "source-author" or "source-inspection" or "source-capture" or "source-validated" or "source-qte" or "source-turn") ||
                transition is not ("enter" or "return" or "error"))
            {
                OutputFailure ??= "InvalidStage";
                stage = "error"; transition = "error"; errorType = "InvalidStage";
            }
            if (_records == 64) transition = "limit";
            errorType = errorType is { Length: > 256 } ? errorType[..256] : errorType;
            try
            {
                var line = Serialize(stage, transition, errorType);
                if (Encoding.UTF8.GetByteCount(line) > 2048)
                {
                    OutputFailure ??= "RecordTooLarge";
                    line = Serialize("error", _records == 64 ? "limit" : "error", "RecordTooLarge");
                }
                if (Encoding.UTF8.GetByteCount(line) > 2048) { _outputClosed = true; return; }
                _writer.Write(line); // Fixed newline; no Flush, observer task or external wait.
            }
            catch (Exception error)
            {
                var name = error.GetType().Name;
                OutputFailure ??= name.Length > 256 ? name[..256] : name;
                _outputClosed = true;
            }
        }
    }

    /// <summary>
    /// Serializes only bounded scalar identity and stage data with one fixed newline.
    /// </summary>
    /// <param name="stage">
    /// An accepted fixed stage name.
    /// </param>
    /// <param name="transition">
    /// The accepted transition or reserved final limit marker.
    /// </param>
    /// <param name="errorType">
    /// A bounded error type, or <see langword="null"/> when no error is being reported.
    /// </param>
    /// <returns>
    /// A single JSONL record for the subsequent byte-bound check.
    /// </returns>
    private string Serialize(string stage, string transition, string? errorType) => JsonSerializer.Serialize(new
    {
        Boundary = "crash5_stage_v1", RunId = _runId, Nonce = _nonce, _identity.Pid, _identity.BirthKind,
        Cut = _cut, Stage = stage, Transition = transition,
        ElapsedMilliseconds = Stopwatch.GetElapsedTime(_started).TotalMilliseconds, ErrorType = errorType
    }) + "\n";
}
