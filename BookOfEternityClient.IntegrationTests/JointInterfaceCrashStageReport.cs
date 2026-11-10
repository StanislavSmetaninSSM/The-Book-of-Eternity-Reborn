using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Writes every scalar ordinary-test stage without changing game or native verdicts.
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
    private JointSourceReadCounter? _sourceReads;
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
    /// Gets the first observer error type, or null when writing has not failed.
    /// </summary>
    internal string? OutputFailure { get; private set; }

    /// <summary>
    /// Enables scalar counters once during the isolated source probe's fixture setup.
    /// </summary>
    /// <returns>
    /// The probe-local counter wired only to existing read and mutation callbacks.
    /// </returns>
    internal JointSourceReadCounter EnableSourceReadCounters() => _sourceReads ??=
        new(snapshot => ObserveCore("source-reads", "enter", null, snapshot));

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
        bool first;
        lock (_gate)
        {
            first = _sourceCheckpoints.Add(checkpoint);
            if (first) Observe(stage, "enter");
        }
        // Never acquire the counter gate while holding the writer gate: read callbacks emit in the opposite order.
        if (first && checkpoint == SessionFinalizationCheckpoint.TerminalSignalSnapshotCapturedBeforeResolution)
            _sourceReads?.Advance("capture-to-validated");
        if (first && checkpoint == SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization)
            _sourceReads?.Advance("post-validated");
    }

    /// <summary>
    /// Emits one stage observation without a record quota or flush wait.
    /// </summary>
    /// <param name="stage">
    /// A fixed ordinary-test stage name.
    /// </param>
    /// <param name="transition">
    /// The enter, return or error transition.
    /// </param>
    /// <param name="errorType">
    /// An optional complete exception type name; no exception messages or game payloads.
    /// </param>
    internal void Observe(string stage, string transition, string? errorType = null)
    {
        if (stage == "source-turn" && transition is "return" or "error") _sourceReads?.Complete();
        ObserveCore(stage, transition, errorType, null);
    }

    /// <summary>
    /// Emits the actual enum at the existing finalization callback boundary.
    /// </summary>
    /// <param name="checkpoint">
    /// The observed enum; an undefined value is reported only as Unknown.
    /// </param>
    /// <param name="before">
    /// <see langword="true"/> records entry; <see langword="false"/> records callback return.
    /// </param>
    internal void ObserveFinalization(SessionFinalizationCheckpoint checkpoint, bool before) =>
        ObserveCore(before ? "finalization-before" : "finalization-after", before ? "enter" : "return", null, null,
            checkpoint: Enum.IsDefined(checkpoint) ? checkpoint.ToString() : "Unknown");

    /// <summary>
    /// Emits an activity snapshot without reserving or exhausting a record quota.
    /// </summary>
    /// <param name="snapshot">
    /// Scalar hook observations; counts are neither I/O completions nor success claims.
    /// </param>
    internal void ObserveResumeActivity(JointResumeActivitySnapshot snapshot) =>
        ObserveCore("resume-activity", "enter", null, null, resume: snapshot);

    /// <summary>
    /// Writes an ordinary scalar stage or a source-probe aggregate without record or byte truncation.
    /// </summary>
    /// <param name="stage">
    /// The fixed stage name, including the source-probe aggregate stage.
    /// </param>
    /// <param name="transition">
    /// The observed enter, return or error transition.
    /// </param>
    /// <param name="errorType">
    /// An optional complete exception type without message or state data.
    /// </param>
    /// <param name="reads">
    /// Optional scalar read counters; null leaves the previous stage schema unchanged.
    /// </param>
    /// <param name="resume">
    /// Optional ordinary Windows resumer observations.
    /// </param>
    /// <param name="checkpoint">
    /// An optional enum name from the existing finalization callback.
    /// </param>
    private void ObserveCore(string stage, string transition, string? errorType, JointSourceReadSnapshot? reads,
        JointResumeActivitySnapshot? resume = null, string? checkpoint = null)
    {
        lock (_gate)
        {
            if (_outputClosed) return;
            if (stage is not ("fixture" or "file-system" or "engine" or "preflight-lease" or "snapshot-refresh" or
                "late-first" or "finalization-before" or "finalization-after" or "late-repeat" or "exit" or "error" or
                "source-author" or "source-inspection" or "source-capture" or "source-validated" or "source-qte" or "source-turn" or "source-reads" or "resume-activity") ||
                transition is not ("enter" or "return" or "error"))
            {
                OutputFailure ??= "InvalidStage";
                stage = "error"; transition = "error"; errorType = "InvalidStage";
            }
            try
            {
                var line = Serialize(stage, transition, errorType, reads, resume, checkpoint);
                _writer.Write(line); // Fixed newline; no Flush, observer task or external wait.
            }
            catch (Exception error)
            {
                var name = error.GetType().Name;
                OutputFailure ??= name;
                _outputClosed = true;
            }
        }
    }

    /// <summary>
    /// Serializes scalar identity and stage data with one fixed newline.
    /// </summary>
    /// <param name="stage">
    /// An accepted fixed stage name.
    /// </param>
    /// <param name="transition">
    /// The accepted transition.
    /// </param>
    /// <param name="errorType">
    /// An error type, or <see langword="null"/> when no error is being reported.
    /// </param>
    /// <param name="reads">
    /// Optional scalar source-probe totals; no paths or file contents are serialized.
    /// </param>
    /// <param name="resume">
    /// Optional scalar resumer counters without paths or file contents.
    /// </param>
    /// <param name="checkpoint">
    /// Optional fixed enum name; absent for previous stage consumers.
    /// </param>
    /// <returns>
    /// A complete JSONL record without character or byte truncation.
    /// </returns>
    private string Serialize(string stage, string transition, string? errorType, JointSourceReadSnapshot? reads = null,
        JointResumeActivitySnapshot? resume = null, string? checkpoint = null)
    {
        var record = JsonSerializer.SerializeToNode(new
        {
            Boundary = "crash5_stage_v1", RunId = _runId, Nonce = _nonce, _identity.Pid, _identity.BirthKind,
            Cut = _cut, Stage = stage, Transition = transition,
            ElapsedMilliseconds = Stopwatch.GetElapsedTime(_started).TotalMilliseconds, ErrorType = errorType
        })!.AsObject();
        if (reads != null) record["Reads"] = JsonSerializer.SerializeToNode(reads);
        if (resume != null) record["ResumeActivity"] = JsonSerializer.SerializeToNode(resume);
        if (checkpoint != null) record["Checkpoint"] = checkpoint;
        return record.ToJsonString() + "\n";
    }
}
