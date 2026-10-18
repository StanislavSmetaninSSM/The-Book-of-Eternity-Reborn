#nullable enable
using System;

namespace BookOfEternityClient.Tests;

// Test-parent-only admission. It neither starts a process nor changes game state.
internal sealed class JointInterfaceManagedCapturePolicy
{
    internal readonly record struct Decision(string State, string? Reason, bool Launch);
    internal Decision Step(JointInterfaceDiagnosticDelivery.Event? item,
        JointInterfaceDiagnosticDelivery.Health health, long now, double remainingSeconds,
        bool phaseAdmitted = true) => throw new NotImplementedException("G01 admission not implemented");
}
