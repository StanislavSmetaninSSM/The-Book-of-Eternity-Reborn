#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Tests;

internal static class PolicyProbe
{
    static readonly long T = 100 * Stopwatch.Frequency;
    static readonly JointInterfaceDiagnosticDelivery.Event Custody = new(1, 1, 0, T,
        DateTimeOffset.UnixEpoch, 123, 456, "/tmp/disposable", "0123456789abcdef0123456789abcdef",
        1, 1, DateTimeOffset.UnixEpoch, "finalization-return",
        "MortalJointCustodyCaptureCompletedBeforePreparation", "hash", "IDENTITY_ORDERED", "CUSTODY_RETURN");
    static readonly JointInterfaceDiagnosticDelivery.Health Good = new(Candidates: 1, Published: 1,
        Dropped: 0, Malformed: 0, Oversize: 0, IncompleteTail: 0, IdentityRejected: 0,
        Duplicates: 0, ElapsedRegressions: 0, TrackingExhausted: 0, Uncertain: false,
        Eof: false, RawReadFault: false, CustodySeen: true, AcceptedSeen: false,
        LateReturnSeen: false, ObserverFailure: null);
    static long After(double seconds) => T + (long)(seconds * Stopwatch.Frequency);
    static void Require(bool value) { if (!value) throw new Exception("Required admission/refusal behavior absent"); }
    public static int Main()
    {
        var rows = new List<object>(); int failed = 0;
        void Check(string name, Action body)
        {
            try { body(); rows.Add(new { name, passed = true }); }
            catch (Exception e) { failed++; rows.Add(new { name, passed = false, error = e.GetType().Name + ": " + e.Message }); }
        }
        Check("wait_for_exact_control", () => Require(new JointInterfaceManagedCapturePolicy().Step(null, Good, T, 220).State == "WAITING"));
        Check("arm_once_wait_until_receipt_plus60", () => {
            var g = new JointInterfaceManagedCapturePolicy(); Require(g.Step(Custody, Good, T, 220).State == "ARMED");
            Require(!g.Step(null, Good, After(59.9), 160.1).Launch);
            Require(g.Step(null, Good, After(60), 160).Launch);
            Require(!g.Step(Custody, Good, After(60.1), 159.9).Launch);
        });
        Check("timer_anchored_to_receipt_not_dequeue", () => {
            var g = new JointInterfaceManagedCapturePolicy(); Require(g.Step(Custody, Good, After(.9), 219.1).State == "ARMED");
            Require(g.Step(null, Good, After(60), 160).Launch);
        });
        Check("stale_queue_cancels", () => Require(new JointInterfaceManagedCapturePolicy().Step(Custody, Good, After(1.01), 219).State == "CANCELLED"));
        Check("future_timestamp_cancels", () => Require(new JointInterfaceManagedCapturePolicy().Step(Custody, Good, T - 1, 220).State == "CANCELLED"));
        Check("unknown_admission_phase_cancels", () => Require(new JointInterfaceManagedCapturePolicy().Step(Custody, Good, T, 220, false).State == "CANCELLED"));
        Check("late_arm_insufficient_window_cancels", () => Require(new JointInterfaceManagedCapturePolicy().Step(Custody, Good, T, 84).State == "CANCELLED"));
        Check("expiry_requires25_seconds", () => {
            var g = new JointInterfaceManagedCapturePolicy(); g.Step(Custody, Good, T, 220);
            Require(g.Step(null, Good, After(60), 24.99).State == "CANCELLED");
        });
        var bad = new (string, JointInterfaceDiagnosticDelivery.Health)[] {
            ("uncertain", Good with { Uncertain = true }), ("accepted", Good with { AcceptedSeen = true }),
            ("late", Good with { LateReturnSeen = true }), ("eof", Good with { Eof = true }),
            ("raw_fault", Good with { RawReadFault = true }), ("observer_fault", Good with { ObserverFailure = "fault" }),
            ("dropped", Good with { Dropped = 1 }), ("malformed", Good with { Malformed = 1 }),
            ("oversize", Good with { Oversize = 1 }), ("tail", Good with { IncompleteTail = 1 }),
            ("identity", Good with { IdentityRejected = 1 }), ("duplicate", Good with { Duplicates = 1 }),
            ("regression", Good with { ElapsedRegressions = 1 }), ("tracking", Good with { TrackingExhausted = 1 }) };
        foreach (var (name, health) in bad) Check(name + "_cancellation_is_sticky", () => {
            var g = new JointInterfaceManagedCapturePolicy(); g.Step(Custody, Good, T, 220);
            Require(g.Step(null, health, After(1), 219).State == "CANCELLED");
            Require(!g.Step(Custody, Good, After(60), 160).Launch);
        });
        Check("noncontrol_does_not_arm", () => Require(new JointInterfaceManagedCapturePolicy().Step(Custody with { Control = "NONE" }, Good, T, 220).State == "WAITING"));
        Console.WriteLine(JsonSerializer.Serialize(new { kind = "G01_PURE_POLICY", passed = rows.Count - failed, failed,
            cases = rows, gameProcesses = 0, requests = 0 }));
        return failed == 0 ? 0 : 1;
    }
}
