using System.Security.Cryptography;
using System.Text;
using BookOfEternityClient.Configuration;

namespace BookOfEternityGMBridge;

// This is local live-owner retention. Neither operation IDs nor binding IDs are durable run authority.
internal sealed partial class BridgeHost
{
    private readonly SemaphoreSlim _promptGate = new(1, 1);
    private readonly Dictionary<string, PromptOperation> _promptOperations = new(StringComparer.Ordinal);
    private Func<string> _promptScreenReader = ReadVisibleConsoleText;
    private long PromptObservationVersion => _terminalScreen != null ? CaptureTerminalView().Revision : _outputVersion;
    private bool _automaticInputPaused;
    private int _admittedPrompts;
    private const int PromptRetentionLimit = 256;

    private sealed record PromptSnapshot(string Id, string Kind, string Revision, string BindingId,
        string Text, bool Submit, GmCliInputProfile Profile);
    private sealed class PromptOperation(PromptSnapshot snapshot, InputLifetime input)
    {
        public readonly PromptSnapshot Snapshot = snapshot;
        public readonly InputLifetime Input = input;
        public CancellationTokenSource Cancellation = new();
        public PromptDeliveryPhase Phase = PromptDeliveryPhase.Queued;
        public PromptDeliveryResult? Result;
        public Task<PromptDeliveryResult> Task = null!;
        public readonly TaskCompletionSource<DraftFileProof> DraftProof=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool DraftArmed,DraftClaimed;
        public string[] ExistingDrafts=[];
        public string FirstEdge="",LastEdge="";
    }

    private static PromptDeliveryResult PromptResult(BridgeRequest request, PromptDeliveryDisposition disposition, string reason) =>
        new(request.OperationId ?? "", request.OperationKind ?? "", request.OperationRevision ?? "",
            request.InputBindingId ?? "", ContentHash(request.Text ?? "", request.AppendEnter), disposition, PromptDeliveryPhase.Terminal, reason);

    private async Task<BridgeResponse> DispatchPromptAsync(BridgeRequest request)
    {
        Task<PromptDeliveryResult> task;
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(request.OperationId) || string.IsNullOrWhiteSpace(request.OperationKind) ||
                string.IsNullOrWhiteSpace(request.OperationRevision) || string.IsNullOrWhiteSpace(request.InputBindingId) ||
                !request.AppendEnter || string.IsNullOrEmpty(request.Text) || request.Text.Any(c => c < ' ' && c != '\n' && c != '\r' && c != '\t'))
                return PromptResponse(PromptResult(request, PromptDeliveryDisposition.NotWritten, "invalid-operation"));
            if (_promptOperations.TryGetValue(request.OperationId, out var retained))
            {
                var s = retained.Snapshot;
                if (s.Kind != request.OperationKind || s.Revision != request.OperationRevision || s.BindingId != request.InputBindingId ||
                    s.Text != request.Text || s.Submit != request.AppendEnter)
                    return PromptResponse(PromptResult(request, PromptDeliveryDisposition.NotWritten, "identity-conflict"));
                // Only a proven zero-write availability rejection can be attempted again with the same frozen operation.
                var readmitted = false;
                if (retained.Result?.Disposition == PromptDeliveryDisposition.NotWritten &&
                    retained.Result.Reason == "not-ready" && ReferenceEquals(_inputLifetime, retained.Input) &&
                    !retained.Input.Revoked && !_automaticInputPaused && _admittedPrompts < 2)
                {
                    retained.Cancellation.Dispose();
                    retained.Cancellation = new();
                    retained.Result = null;
                    retained.Phase = PromptDeliveryPhase.Queued;
                    AdmitPrompt(retained);
                    readmitted = true;
                }
                if (!readmitted && !retained.Task.IsCompleted)
                    return PromptResponse(retained.Result ?? OperationResult(retained, null, "in-progress"));
                task = retained.Task;
            }
            else
            {
                var input = _inputLifetime;
                if (_inputClosed || input == null || input.Revoked || input.Token.IsCancellationRequested || input.Id != request.InputBindingId)
                    return PromptResponse(PromptResult(request, PromptDeliveryDisposition.NotWritten, "stale-binding"));
                if (_automaticInputPaused || input.ManualTakeover)
                    return PromptResponse(PromptResult(request, PromptDeliveryDisposition.NotWritten, "operator-paused"));
                if (_admittedPrompts >= 2 || _promptOperations.Count >= PromptRetentionLimit)
                    return PromptResponse(PromptResult(request, PromptDeliveryDisposition.NotWritten, "busy"));
                var profile = LoadBridgeConfig().GmCliInputProfile.Snapshot();
                if (!profile.IsSupported)
                    return PromptResponse(PromptResult(request, PromptDeliveryDisposition.NotWritten, "unsupported-profile"));
                var operation = new PromptOperation(new(request.OperationId, request.OperationKind, request.OperationRevision,
                    input.Id, request.Text, request.AppendEnter, profile), input);
                _promptOperations.Add(request.OperationId, operation);
                AdmitPrompt(operation);
                task = operation.Task;
            }
        }
        return PromptResponse(await task);
    }

    private void AdmitPrompt(PromptOperation operation)
    {
        _admittedPrompts++;
        operation.Task = Task.Run(() => RunPromptOperationAsync(operation));
        operation.Input.PromptTasks.Add(operation.Task);
    }

    private BridgeResponse PromptResponse(PromptDeliveryResult result) => new()
    {
        Ok = true, Status = SnapshotStatus(), PromptDelivery = result
    };

    private BridgeResponse QueryPrompt(BridgeRequest request, bool cancel)
    {
        lock (_sync)
        {
            if (!_promptOperations.TryGetValue(request.OperationId ?? "", out var operation) ||
                operation.Snapshot.BindingId != request.InputBindingId || operation.Snapshot.Kind != request.OperationKind ||
                operation.Snapshot.Revision != request.OperationRevision || operation.Snapshot.Text != request.Text ||
                operation.Snapshot.Submit != request.AppendEnter)
                return PromptResponse(PromptResult(request, PromptDeliveryDisposition.UnknownOutcome, "operation-not-retained"));
            if (cancel && operation.Result == null)
                _ = operation.Cancellation.CancelAsync();
            return PromptResponse(operation.Result ?? OperationResult(operation, null, cancel ? "cancel-requested" : "in-progress"));
        }
    }

    private static PromptDeliveryResult OperationResult(PromptOperation operation, PromptDeliveryDisposition? disposition, string reason) =>
        new(operation.Snapshot.Id, operation.Snapshot.Kind, operation.Snapshot.Revision, operation.Snapshot.BindingId,
            ContentHash(operation.Snapshot.Text, operation.Snapshot.Submit), disposition, operation.Phase, reason);

    private static string ContentHash(string text, bool submit) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes((submit ? "1\n" : "0\n") + text)));

    private bool PromptStillOwned(PromptOperation op) => !_inputClosed && ReferenceEquals(_inputLifetime, op.Input) &&
        !op.Input.Revoked && !op.Input.Token.IsCancellationRequested && !op.Input.ManualTakeover &&
        !op.Cancellation.IsCancellationRequested && !_cts.IsCancellationRequested && _pty?.AuthorityLost.IsCompleted != true && _pty?.RootExited.IsCompleted != true;

    private async Task<PromptDeliveryResult> RunPromptOperationAsync(PromptOperation operation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, operation.Input.Token, operation.Cancellation.Token);
        var token = linked.Token;
        var gateHeld = false;
        var reason = "observation-timeout";
        PromptDeliveryDisposition disposition;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await _promptGate.WaitAsync(token);
            gateHeld = true;
            lock (_sync)
            {
                if (!PromptStillOwned(operation) || _automaticInputPaused)
                    throw new OperationCanceledException(token);
                if (!_status.Ready || !IsEmptyIdleView(operation.Snapshot.Profile, _promptScreenReader()))
                {
                    reason = "not-ready";
                    return FinishPrompt(operation, PromptDeliveryDisposition.NotWritten, reason, watch.ElapsedMilliseconds);
                }
                if(operation.Snapshot.Profile.IsMini && !PrepareMiniOperation(operation))
                    return FinishPrompt(operation,PromptDeliveryDisposition.NotWritten,"unsupported-draft-shape",watch.ElapsedMilliseconds);
                _status.Ready = false;
                _status.State = "Busy";
                _status.LastPromptDispatchState = "Dispatching";
                _status.LastPromptDispatchStartedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                operation.Phase = PromptDeliveryPhase.PasteStarted;
                if(operation.Snapshot.Profile.IsMini)operation.Input.MiniPasteAttempted=true;
                TryWriteInputStatus();
            }
            var profile = operation.Snapshot.Profile;
            long version;
            lock (_sync) version = PromptObservationVersion;
            var text = operation.Snapshot.Text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", profile.NewlineSequence);
            if(profile.IsMini)await WriteMiniGestureAsync(operation,profile.PasteStart+text+profile.PasteEnd,()=>{if(!IsMiniInitialFrame(profile,CaptureTerminalView()))return false;version=PromptObservationVersion;return true;},token);
            else await WriteToPtyAsync(operation.Input, profile.PasteStart + text + profile.PasteEnd, false, token);
            lock (_sync) operation.Phase = PromptDeliveryPhase.AwaitingPaste;
            if (!await ObservePromptAsync(operation, version, screen => profile.IsMini ? IsMiniEdge(operation,false) : IsPastedView(profile, screen, text), token))
                return FinishPrompt(operation, PromptDeliveryDisposition.DraftUncertain, reason, watch.ElapsedMilliseconds);
            if(profile.IsMini && !await ObserveMiniDraftAsync(operation,token))
                return FinishPrompt(operation,PromptDeliveryDisposition.DraftUncertain,"draft-proof-or-restoration-refused",watch.ElapsedMilliseconds);
            lock (_sync)
            {
                // Linearization precedes the first submit byte. Manual input/cancel shares this lock.
                if (!PromptStillOwned(operation)) throw new OperationCanceledException(token);
                if (!(profile.IsMini ? IsMiniEdge(operation,false) : IsPastedView(profile, _promptScreenReader(), text)))
                    return FinishPrompt(operation, PromptDeliveryDisposition.DraftUncertain, "paste-view-changed", watch.ElapsedMilliseconds);
                if(!profile.IsMini)operation.Phase = PromptDeliveryPhase.SubmitStarted;
                version = PromptObservationVersion;
            }
            if(profile.IsMini)await WriteMiniGestureAsync(operation,profile.SubmitSequence,()=>{
                if(!IsMiniEdge(operation,false) || !operation.DraftProof.Task.IsCompletedSuccessfully ||
                    !DraftObservation.IsAbsent(profile.DraftDirectory,operation.DraftProof.Task.Result.Path))return false;
                operation.Phase=PromptDeliveryPhase.SubmitStarted;version=PromptObservationVersion;return true;
            },token);
            else await WriteToPtyAsync(operation.Input, profile.SubmitSequence, false, token);
            lock (_sync) operation.Phase = PromptDeliveryPhase.AwaitingSubmission;
            if (!await ObservePromptAsync(operation, version, screen => profile.IsMini ? IsMiniWorking(profile) : !IsBlockedView(profile, screen) &&
                    screen.Contains(profile.WorkingMarker, StringComparison.Ordinal) && !IsPastedView(profile, screen, text), token))
                return FinishPrompt(operation, PromptDeliveryDisposition.UnknownOutcome, reason, watch.ElapsedMilliseconds);
            lock (_sync)
            {
                if (!PromptStillOwned(operation)) throw new OperationCanceledException(token);
                return FinishPrompt(operation, PromptDeliveryDisposition.SubmissionObserved, "fresh-submission-observed", watch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or InputLifetimeUnavailableException)
        {
            reason = ex is OperationCanceledException ? "cancelled-or-revoked" : "input-failed";
            lock (_sync)
                disposition = operation.Phase >= PromptDeliveryPhase.SubmitStarted ? PromptDeliveryDisposition.UnknownOutcome :
                    operation.Phase >= PromptDeliveryPhase.PasteStarted ? PromptDeliveryDisposition.DraftUncertain : PromptDeliveryDisposition.QueuedCancelled;
            return FinishPrompt(operation, disposition, reason, watch.ElapsedMilliseconds);
        }
        catch
        {
            lock (_sync)
                disposition = operation.Phase >= PromptDeliveryPhase.SubmitStarted ? PromptDeliveryDisposition.UnknownOutcome :
                    operation.Phase >= PromptDeliveryPhase.PasteStarted ? PromptDeliveryDisposition.DraftUncertain : PromptDeliveryDisposition.NotWritten;
            return FinishPrompt(operation, disposition, "observation-failed", watch.ElapsedMilliseconds);
        }
        finally
        {
            lock (_sync) _admittedPrompts--;
            if (gateHeld) _promptGate.Release();
        }
    }

    private PromptDeliveryResult FinishPrompt(PromptOperation operation, PromptDeliveryDisposition disposition, string reason, long elapsed)
    {
        lock (_sync)
        {
            operation.Phase = PromptDeliveryPhase.Terminal;
            operation.Result = OperationResult(operation, disposition, reason);
            if (ReferenceEquals(_inputLifetime, operation.Input))
            {
                if (disposition is PromptDeliveryDisposition.DraftUncertain or PromptDeliveryDisposition.UnknownOutcome)
                    _automaticInputPaused = true;
                _status.LastPromptDispatchState = disposition.ToString();
                _status.LastPromptDispatchCompletedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                _status.LastPromptDispatchElapsedMs = elapsed;
                _status.State = _automaticInputPaused ? "InputUncertain" : "OperatorNotReady";
                _status.PromptDelivery = operation.Result;
                if (disposition != PromptDeliveryDisposition.NotWritten) _status.Ready = false;
                TryWriteInputStatus();
            }
            return operation.Result;
        }
    }

    private async Task<bool> ObservePromptAsync(PromptOperation operation, long afterVersion, Func<string, bool> predicate, CancellationToken token)
    {
        var deadline = Environment.TickCount64 + operation.Snapshot.Profile.ObservationTimeoutMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!PromptStillOwned(operation)) throw new OperationCanceledException(token);
                if (PromptObservationVersion > afterVersion && predicate(_promptScreenReader())) return true;
            }
            await Task.Delay(10, token);
        }
        return false;
    }

    private static bool IsBlockedView(GmCliInputProfile p, string screen) => string.IsNullOrWhiteSpace(screen) ||
        p.BlockedMarkers.Any(marker => screen.Contains(marker, StringComparison.OrdinalIgnoreCase));
    private static string? Composer(GmCliInputProfile p, string screen)
    {
        var start = screen.LastIndexOf(p.PromptPrefix, StringComparison.Ordinal);
        return start < 0 ? null : screen[(start + p.PromptPrefix.Length)..].Replace("\r\n", "\n").TrimEnd('\r', '\n');
    }
    private bool IsEmptyIdleView(GmCliInputProfile p, string screen) => p.IsMini ? IsMiniEmptyIdle(p) :
        p.IsSupported && !IsBlockedView(p, screen) && screen.Contains(p.IdleMarker, StringComparison.Ordinal) &&
        !screen.Contains(p.WorkingMarker, StringComparison.Ordinal) && Composer(p, screen) == "";
    private static bool IsPastedView(GmCliInputProfile p, string screen, string text) => !IsBlockedView(p, screen) &&
        screen.Contains(p.IdleMarker, StringComparison.Ordinal) && !screen.Contains(p.WorkingMarker, StringComparison.Ordinal) &&
        Composer(p, screen) == text.Replace("\r\n", "\n");

    private void TakeManualInput(InputLifetime input)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(_inputLifetime, input) || input.Revoked) throw new InputLifetimeUnavailableException();
            input.ManualTakeover = true;
            if (input.BootstrapCancellation != null) _ = input.BootstrapCancellation.CancelAsync();
            _status.Ready = false;
            foreach (var operation in _promptOperations.Values.Where(o => ReferenceEquals(o.Input, input) && o.Result == null))
                _ = operation.Cancellation.CancelAsync();
            TryWriteInputStatus();
        }
    }

    // Shell launch is a frozen local automatic frame, not a GM prompt or submission receipt.
    // Manual takeover before its byte boundary aborts it; original writer retains any started-I/O uncertainty.
    private Task WriteShellBootstrapAsync(InputLifetime input, string bootstrap, CancellationToken token)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(_inputLifetime, input) || input.Revoked || input.ManualTakeover || _inputClosed)
                return Task.CompletedTask;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, input.Token, _cts.Token);
            input.BootstrapCancellation = cancellation;
            var task = Task.Run(async () =>
            {
                var held = false;
                try
                {
                    await _promptGate.WaitAsync(cancellation.Token);
                    held = true;
                    lock (_sync)
                        if (!ReferenceEquals(_inputLifetime, input) || input.Revoked || input.ManualTakeover || _inputClosed)
                            throw new OperationCanceledException(cancellation.Token);
                    await WriteToPtyAsync(input, bootstrap, true, cancellation.Token);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                finally
                {
                    if (held) _promptGate.Release();
                    lock (_sync)
                        if (ReferenceEquals(input.BootstrapCancellation, cancellation)) input.BootstrapCancellation = null;
                    cancellation.Dispose();
                }
            });
            input.PromptTasks.Add(task); // Original Stop joins this task before replacement.
            return task;
        }
    }

    private async Task WriteManualInputAsync(InputLifetime input, string text, CancellationToken token)
    {
        TakeManualInput(input); // Latch before waiting for the whole automatic frame/operation.
        await WriteExclusiveInputAsync(input, text, false, token);
    }
    private async Task WriteExclusiveInputAsync(InputLifetime input, string text, bool appendEnter, CancellationToken token)
    {
        await _promptGate.WaitAsync(token);
        try { lock(_sync) { if(input.ManualTakeover && _terminalScreen!=null)input.ManualObservationAfter=PromptObservationVersion; } await WriteToPtyAsync(input, text, appendEnter, token); }
        finally { _promptGate.Release(); }
    }
}

internal enum PromptDeliveryDisposition { NotWritten, QueuedCancelled, DraftUncertain, SubmissionObserved, UnknownOutcome }
internal enum PromptDeliveryPhase { Queued, PasteStarted, AwaitingPaste, SubmitStarted, AwaitingSubmission, Terminal }
internal sealed record PromptDeliveryResult(string OperationId, string OperationKind, string OperationRevision,
    string InputBindingId, string ContentHash, PromptDeliveryDisposition? Disposition, PromptDeliveryPhase Phase, string Reason);
