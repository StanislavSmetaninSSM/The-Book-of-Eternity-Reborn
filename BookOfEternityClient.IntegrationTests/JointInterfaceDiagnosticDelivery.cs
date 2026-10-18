#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BookOfEternityClient.Tests;

// Test-parent observation only. It never controls a child, game state or native outcome.
internal sealed class JointInterfaceDiagnosticDelivery
{
    internal const int Capacity = 64, LineLimit = 8192, FingerprintLimit = 256;
    internal const string Boundary = "iobs_current_resume_diagnostic";
    internal const string Custody = "MortalJointCustodyCaptureCompletedBeforePreparation";
    internal const string Accepted = "AcceptedOutcomeValidatedBeforeMaterialization";
    internal sealed record Expected(int Pid, ulong BirthTick, string Root, string Nonce, int Cut);
    internal sealed record Event(long Ordinal, long LineOrdinal, long CharacterOffset, long ReceivedTimestamp,
        DateTimeOffset ReceivedUtc, int Pid, ulong BirthTick, string Root, string Nonce, int Cut,
        long SourceElapsedMilliseconds, DateTimeOffset SourceUtc, string Stage, string? ControlDetail,
        string RawLineSha256, string Disposition, string Control);
    internal readonly record struct Health(long Candidates, long Published, long Dropped, long Malformed,
        long Oversize, long IncompleteTail, long IdentityRejected, long Duplicates, long ElapsedRegressions,
        long TrackingExhausted, bool Uncertain, bool Eof, bool RawReadFault, bool CustodySeen,
        bool AcceptedSeen, bool LateReturnSeen, string? ObserverFailure)
    {
        public string SourceEmissionCompleteness => "UNKNOWN";
        public string CapturePhase => "UNKNOWN";
        public string ExpectedCustody => CustodySeen ? "OBSERVED_DISCRETE_MARKER" : Eof ? "UNOBSERVED" : "NOT_YET_OBSERVED";
    }
    internal readonly record struct QueueTiming(double ResidenceMilliseconds, bool Delayed,
        string SourceToReceiveLatency, string CapturePhase);

    private readonly Expected _expected;
    private readonly Channel<Event> _events;
    private readonly StringBuilder _line = new();
    private readonly HashSet<string> _fingerprints = new(StringComparer.Ordinal);
    private long _offset, _lineStart, _lineOrdinal, _lastElapsed = -1;
    private long _candidates, _published, _dropped, _malformed, _oversize, _incomplete;
    private long _identityRejected, _duplicates, _regressions, _trackingExhausted;
    private int _started, _uncertain, _eof, _rawReadFault, _custodySeen, _acceptedSeen, _lateReturnSeen;
    private bool _longLine, _observerDisabled;
    private string? _observerFailure;

    internal ChannelReader<Event> Reader => _events.Reader;

    internal JointInterfaceDiagnosticDelivery(Expected expected, int capacity = Capacity)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (expected.Pid <= 0 || expected.BirthTick == 0 || !Path.IsPathFullyQualified(expected.Root) ||
            !Guid.TryParseExact(expected.Nonce, "N", out _) || expected.Cut is < 0 or > 4)
            throw new ArgumentException("Invalid independent owned-child expectation.", nameof(expected));
        if (capacity is < 1 or > Capacity) throw new ArgumentOutOfRangeException(nameof(capacity));
        _expected = expected;
        _events = Channel.CreateBounded<Event>(new BoundedChannelOptions(capacity)
        {
            SingleWriter = true, SingleReader = true, FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    }

    // Fallback is possible only before any read. No callback, replacement reader or I/O observer.
    internal static Task<string> Start(TextReader reader, Expected expected,
        out JointInterfaceDiagnosticDelivery? delivery, out string? observerError, int capacity = Capacity)
    {
        try { delivery = new(expected, capacity); }
        catch (Exception error) when (Recoverable(error))
        {
            delivery = null; observerError = error.GetType().Name;
            return reader.ReadToEndAsync();
        }
        observerError = null;
        return delivery.DrainAsync(reader);
    }

    private static bool Recoverable(Exception error) => error is not OutOfMemoryException and not StackOverflowException;
    private void Uncertain() => Volatile.Write(ref _uncertain, 1);
    private void ObserverFault(Exception error)
    {
        _observerFailure ??= error.GetType().Name;
        _observerDisabled = true;
        Uncertain();
    }

    internal async Task<string> DrainAsync(TextReader reader)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) throw new InvalidOperationException("Only one raw drain is permitted.");
        var raw = new StringBuilder();
        var buffer = new char[4096];
        try
        {
            int count;
            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
            {
                raw.Append(buffer, 0, count); // Same decoded text as ReadToEndAsync, before any observer operation.
                if (_observerDisabled) continue;
                try { ObserveChunk(buffer, count); }
                catch (Exception error) when (Recoverable(error)) { ObserverFault(error); }
            }
            Volatile.Write(ref _eof, 1);
            if (_line.Length != 0 || _longLine) { Interlocked.Increment(ref _incomplete); Uncertain(); }
            return raw.ToString();
        }
        catch
        {
            Volatile.Write(ref _rawReadFault, 1); Uncertain();
            throw; // Original reader I/O remains the raw task's failure; never pretend EOF.
        }
        finally
        {
            try { _events.Writer.TryComplete(); }
            catch (Exception error) when (Recoverable(error)) { ObserverFault(error); }
        }
    }

    private void ObserveChunk(char[] chars, int count)
    {
        for (var i = 0; i < count; i++, _offset++)
        {
            if (chars[i] == '\n')
            {
                _lineOrdinal++;
                if (_longLine) { Interlocked.Increment(ref _oversize); Uncertain(); }
                else ObserveLine(_line.ToString());
                _line.Clear(); _longLine = false; _lineStart = _offset + 1;
            }
            else if (!_longLine)
            {
                if (_line.Length == LineLimit) { _longLine = true; _line.Clear(); }
                else _line.Append(chars[i]);
            }
        }
    }

    private void ObserveLine(string line)
    {
        if (!line.Contains(Boundary, StringComparison.Ordinal)) return;
        var ordinal = Interlocked.Increment(ref _candidates);
        Event item;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Diagnostic object required.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!names.Add(property.Name)) throw new JsonException("Duplicate diagnostic property.");
            if (root.GetProperty("Boundary").GetString() != Boundary) return; // Ordinary/other Boundary is never control.
            var pid = root.GetProperty("Pid").GetInt32();
            var tick = root.GetProperty("BirthTick").GetUInt64();
            var sourceRoot = root.GetProperty("Root").GetString();
            var nonce = root.GetProperty("Nonce").GetString();
            var cut = root.GetProperty("Cut").GetInt32();
            var elapsed = root.GetProperty("ElapsedMilliseconds").GetInt64();
            var utc = root.GetProperty("Utc").GetDateTimeOffset();
            var stage = root.GetProperty("Stage").GetString();
            var detailElement = root.GetProperty("Detail");
            if (detailElement.ValueKind is not (JsonValueKind.Null or JsonValueKind.String)) throw new JsonException("Typed detail required.");
            var detail = detailElement.GetString();
            if (elapsed < 0 || string.IsNullOrWhiteSpace(stage)) throw new JsonException("Typed stage/time required.");
            if (pid != _expected.Pid || tick != _expected.BirthTick || sourceRoot != _expected.Root ||
                nonce != _expected.Nonce || cut != _expected.Cut)
            {
                Interlocked.Increment(ref _identityRejected); Uncertain(); return;
            }
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line)));
            var duplicate = _fingerprints.Contains(fingerprint);
            var regression = elapsed < _lastElapsed;
            var trackingUncertain = false;
            if (duplicate) { Interlocked.Increment(ref _duplicates); Uncertain(); }
            else if (_fingerprints.Count < FingerprintLimit) _fingerprints.Add(fingerprint);
            else { Interlocked.Increment(ref _trackingExhausted); Uncertain(); trackingUncertain = true; }
            if (regression) { Interlocked.Increment(ref _regressions); Uncertain(); }
            else _lastElapsed = elapsed; // Equal milliseconds across different events are legitimate.
            var disposition = regression ? "STALE_ORDER" : duplicate ? "DUPLICATE" : trackingUncertain ? "TRACKING_UNKNOWN" : "IDENTITY_ORDERED";
            var control = "NONE";
            if (disposition == "IDENTITY_ORDERED")
            {
                if (stage == "finalization-return" && detail == Custody) { control = "CUSTODY_RETURN"; Volatile.Write(ref _custodySeen, 1); }
                else if (stage is "finalization-enter" or "finalization-return" && detail == Accepted)
                { control = "ACCEPTED_MARKER"; Volatile.Write(ref _acceptedSeen, 1); }
                else if (stage == "late-first-return") { control = "LATE_RETURN"; Volatile.Write(ref _lateReturnSeen, 1); }
            }
            var controlDetail = stage is "finalization-enter" or "finalization-return" && detail is Custody or Accepted ? detail : null;
            item = new Event(ordinal, _lineOrdinal, _lineStart, Stopwatch.GetTimestamp(), DateTimeOffset.UtcNow,
                pid, tick, sourceRoot!, nonce!, cut, elapsed, utc, stage!, controlDetail, fingerprint, disposition, control);
        }
        catch (Exception error) when (Recoverable(error))
        {
            Interlocked.Increment(ref _malformed); Uncertain(); return;
        }
        // Publication faults are observer faults, distinct from malformed input.
        // FullMode=Wait: TryWrite false means known loss. Never await/backpressure.
        if (_events.Writer.TryWrite(item)) Interlocked.Increment(ref _published);
        else { Interlocked.Increment(ref _dropped); Uncertain(); }
    }

    // Health/terminal markers never depend on a queued event being delivered.
    internal Health ReadHealth() => new(Interlocked.Read(ref _candidates), Interlocked.Read(ref _published),
        Interlocked.Read(ref _dropped), Interlocked.Read(ref _malformed), Interlocked.Read(ref _oversize),
        Interlocked.Read(ref _incomplete), Interlocked.Read(ref _identityRejected), Interlocked.Read(ref _duplicates),
        Interlocked.Read(ref _regressions), Interlocked.Read(ref _trackingExhausted), Volatile.Read(ref _uncertain) != 0,
        Volatile.Read(ref _eof) != 0, Volatile.Read(ref _rawReadFault) != 0, Volatile.Read(ref _custodySeen) != 0,
        Volatile.Read(ref _acceptedSeen) != 0, Volatile.Read(ref _lateReturnSeen) != 0, Volatile.Read(ref _observerFailure));

    internal static QueueTiming QueueResidence(Event item, TimeSpan delayedAfter)
    {
        if (delayedAfter < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delayedAfter));
        var elapsed = Stopwatch.GetElapsedTime(item.ReceivedTimestamp);
        return new(elapsed.TotalMilliseconds, elapsed > delayedAfter, "UNKNOWN", "UNKNOWN");
    }
}
