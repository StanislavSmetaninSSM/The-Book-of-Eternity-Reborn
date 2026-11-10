#nullable enable
using System;
using System.Diagnostics;
using System.Threading;

namespace BookOfEternityClient.Tests;

// Test-parent-only admission. It neither starts a process nor changes game state.
internal sealed class JointInterfaceManagedCapturePolicy
{
    internal readonly record struct Decision(string State, string? Reason, bool Launch);
    private string _state = "WAITING";
    private string? _reason;
    private long _receipt;
    private int _admitted;

    internal static bool Healthy(JointInterfaceDiagnosticDelivery.Health h) =>
        !h.Uncertain && !h.Eof && !h.RawReadFault && !h.AcceptedSeen && !h.LateReturnSeen &&
        h.ObserverFailure is null && h.Dropped == 0 && h.Malformed == 0 && h.Oversize == 0 &&
        h.IncompleteTail == 0 && h.IdentityRejected == 0 && h.Duplicates == 0 &&
        h.ElapsedRegressions == 0 && h.TrackingExhausted == 0;

    internal Decision Step(JointInterfaceDiagnosticDelivery.Event? item,
        JointInterfaceDiagnosticDelivery.Health health, long now, double remainingSeconds,
        bool phaseAdmitted = true)
    {
        Decision Result(bool launch = false) => new(_state, _reason, launch);
        Decision Cancel(string reason) { _state = "CANCELLED"; _reason ??= reason; return Result(); }
        if (_state == "CANCELLED") return Result();
        if (!Healthy(health)) return Cancel("STICKY_HEALTH_OR_TERMINAL");
        if (item is not null)
        {
            if (item.ReceivedTimestamp > now) return Cancel("STALE_OR_UNKNOWN_LOCAL_RESIDENCE");
            var residence = Stopwatch.GetElapsedTime(item.ReceivedTimestamp, now).TotalSeconds;
            if (residence < 0 || residence > 1) return Cancel("STALE_OR_UNKNOWN_LOCAL_RESIDENCE");
        }
        if (_state == "WAITING" && item?.Control == "CUSTODY_RETURN")
        {
            if (!health.CustodySeen || item.Disposition != "IDENTITY_ORDERED" ||
                item.Stage != "finalization-return" || item.ControlDetail != JointInterfaceDiagnosticDelivery.Custody)
                return Cancel("CUSTODY_ADMISSION_UNKNOWN");
            if (!phaseAdmitted) return Cancel("ADMISSION_PHASE_UNKNOWN");
            if (remainingSeconds < 85) return Cancel("INSUFFICIENT_WINDOW");
            _receipt = item.ReceivedTimestamp;
            _state = "ARMED";
        }
        if (_state == "ARMED")
        {
            if (!phaseAdmitted || !health.CustodySeen) return Cancel("ADMISSION_PHASE_UNKNOWN");
            if (Stopwatch.GetElapsedTime(_receipt, now).TotalSeconds >= 60)
            {
                if (remainingSeconds < 25) return Cancel("INSUFFICIENT_WINDOW");
                if (Interlocked.CompareExchange(ref _admitted, 1, 0) == 0)
                { _state = "ADMITTED"; return Result(true); }
            }
        }
        return Result();
    }
}
