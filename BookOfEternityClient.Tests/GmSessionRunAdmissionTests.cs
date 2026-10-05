using BookOfEternityClient.Services.GmRuntime;
using Xunit;
using static BookOfEternityClient.Tests.GmSessionRunRecordTests;

namespace BookOfEternityClient.Tests;

public sealed class GmSessionRunAdmissionTests
{
    private static GmSessionRunTarget Target => new(Identity.RootKey, Identity.Backend, Identity.GenerationId);
    private static bool Allows(GmSessionRunObservation? observation, GmSessionRunOperation operation,
        GmSessionRunIdentity? caller = null, GmSessionRunTarget? target = null) =>
        GmSessionRunAdmission.Evaluate(observation, target ?? Target, operation, caller) ==
            GmSessionRunAdmissionDecision.SlotConditionSatisfied;

    [Fact]
    public void Observation_OnlyExplicitAbsenceIsMissing()
    {
        Assert.Equal(GmSessionRunObservationKind.Missing, GmSessionRunObservation.Missing.Kind);
        Assert.Equal(GmSessionRunObservationKind.Unreadable, GmSessionRunRecordCodec.Observe(Array.Empty<byte>()).Kind);
        Assert.False(Allows(null, GmSessionRunOperation.StartRun, Identity));
        Assert.False(Allows(null, GmSessionRunOperation.QuiescentMutation));
        Assert.True(Allows(GmSessionRunObservation.Missing, GmSessionRunOperation.QuiescentMutation));
    }

    [Theory]
    [InlineData("Prepared", false, false)]
    [InlineData("Running", true, false)]
    [InlineData("Stopping", false, false)]
    [InlineData("Uncertain", false, false)]
    [InlineData("Stopped", false, true)]
    public void Admission_StateMatrixIsSlotLocal(string state, bool active, bool quiescent)
    {
        var observation = GmSessionRunObservation.FromRecord(state == "Stopped" ? Stopped : Record(state));
        Assert.True(Allows(observation, GmSessionRunOperation.DiagnosticRead));
        Assert.Equal(active, Allows(observation, GmSessionRunOperation.ActiveRunMutation, Identity));
        Assert.Equal(quiescent, Allows(observation, GmSessionRunOperation.QuiescentMutation));
        Assert.Equal(quiescent, Allows(observation, GmSessionRunOperation.StartRun,
            Identity with { Epoch = 2, RunId = new string('4', 32) }));
        Assert.False(Allows(observation, (GmSessionRunOperation)999, Identity));
    }

    [Fact]
    public void Admission_UnreadableBlocksMutationsButDiagnosticReadRemainsAvailable()
    {
        var unreadable = GmSessionRunRecordCodec.Observe(new byte[] { (byte)'{' });
        foreach (var operation in new[] { GmSessionRunOperation.StartRun, GmSessionRunOperation.ActiveRunMutation, GmSessionRunOperation.QuiescentMutation })
            Assert.False(Allows(unreadable, operation, Identity));
        Assert.True(Allows(unreadable, GmSessionRunOperation.DiagnosticRead));
        Assert.True(Allows(null, GmSessionRunOperation.DiagnosticRead));
    }

    [Theory]
    [InlineData("RootKey")]
    [InlineData("RunId")]
    [InlineData("GenerationId")]
    [InlineData("Epoch")]
    [InlineData("Backend")]
    [InlineData("HostInstanceId")]
    [InlineData("BootId")]
    public void Admission_ActiveMutationRejectsEveryStaleIdentityField(string field) =>
        Assert.False(Allows(GmSessionRunObservation.FromRecord(Record("Running")),
            GmSessionRunOperation.ActiveRunMutation, Mismatch(Identity, field)));

    [Fact]
    public void Admission_RequiresCurrentGenerationAndExistingOwner()
    {
        var running = GmSessionRunObservation.FromRecord(Record("Running"));
        Assert.False(Allows(running, GmSessionRunOperation.ActiveRunMutation, Identity,
            Target with { GenerationId = new string('7', 32) }));
        Assert.False(Allows(running, GmSessionRunOperation.ActiveRunMutation));
        Assert.False(Allows(GmSessionRunObservation.Missing, GmSessionRunOperation.ActiveRunMutation, Identity));
        Assert.False(Allows(GmSessionRunObservation.Missing, GmSessionRunOperation.StartRun, Identity, Target with { GenerationId = null }));
        Assert.True(Allows(GmSessionRunObservation.Missing, GmSessionRunOperation.QuiescentMutation, target: Target with { GenerationId = null }));
    }

    [Fact]
    public void Admission_StartRequiresFreshExactSuccessorAndCannotWrap()
    {
        Assert.True(Allows(GmSessionRunObservation.Missing, GmSessionRunOperation.StartRun, Identity));
        Assert.False(Allows(GmSessionRunObservation.Missing, GmSessionRunOperation.StartRun, Identity with { Epoch = 2 }));
        var stopped = GmSessionRunObservation.FromRecord(Stopped);
        Assert.False(Allows(stopped, GmSessionRunOperation.StartRun, Identity with { Epoch = 2 }));
        Assert.False(Allows(stopped, GmSessionRunOperation.StartRun, Identity with { Epoch = 3, RunId = new string('4', 32) }));
        var replacement = Identity with { Epoch = 2, RunId = new string('4', 32), GenerationId = new string('7', 32) };
        Assert.True(Allows(stopped, GmSessionRunOperation.StartRun, replacement, Target with { GenerationId = replacement.GenerationId }));
        Assert.True(Allows(stopped, GmSessionRunOperation.QuiescentMutation, target: Target with { GenerationId = replacement.GenerationId }));
        var exhaustedIdentity = Identity with { Epoch = long.MaxValue };
        var exhausted = Stopped with { Identity = exhaustedIdentity, StopEvidence = Stop(exhaustedIdentity) };
        Assert.False(Allows(GmSessionRunObservation.FromRecord(exhausted), GmSessionRunOperation.StartRun, Identity with { RunId = new string('4', 32) }));
    }

    [Fact]
    public void Admission_UsesTrustedTargetBackendForRootIdentity()
    {
        var windowsIdentity = Identity with { RootKey = "C:\\Game", Backend = GmSessionRunBackend.WindowsJob };
        var windows = Stopped with { Identity = windowsIdentity, StopEvidence = Stop(windowsIdentity) };
        Assert.True(Allows(GmSessionRunObservation.FromRecord(windows), GmSessionRunOperation.QuiescentMutation,
            target: new("c:\\game", GmSessionRunBackend.WindowsJob, Identity.GenerationId)));
        Assert.False(Allows(GmSessionRunObservation.FromRecord(Stopped), GmSessionRunOperation.QuiescentMutation,
            target: Target with { RootKey = "/GAME/世界" }));
        var foreignIdentity = Identity with { RootKey = "/GAME/世界", Backend = GmSessionRunBackend.WindowsJob };
        var foreign = Stopped with { Identity = foreignIdentity, StopEvidence = Stop(foreignIdentity) };
        Assert.False(Allows(GmSessionRunObservation.FromRecord(foreign), GmSessionRunOperation.QuiescentMutation));
        Assert.False(Allows(GmSessionRunObservation.FromRecord(Stopped), GmSessionRunOperation.QuiescentMutation,
            target: Target with { RootKey = "/other" }));
        // An unrelated root obtains its own absence observation, not this root's record.
        Assert.True(Allows(GmSessionRunObservation.Missing, GmSessionRunOperation.QuiescentMutation,
            target: Target with { RootKey = "/other" }));
    }

    [Fact]
    public void Admission_InvalidTargetOrRecordFailsClosed()
    {
        Assert.False(Allows(GmSessionRunObservation.Missing, GmSessionRunOperation.QuiescentMutation,
            target: Target with { Backend = (GmSessionRunBackend)999 }));
        Assert.False(Allows(GmSessionRunObservation.Missing, GmSessionRunOperation.StartRun,
            Identity with { RunId = new string('0', 32) }));
        Assert.False(Allows(GmSessionRunObservation.Missing, GmSessionRunOperation.QuiescentMutation,
            target: Target with { RootKey = "" }));
        Assert.False(Allows(GmSessionRunObservation.FromRecord(Record("Stopped")), GmSessionRunOperation.QuiescentMutation));
        Assert.False(Allows(GmSessionRunObservation.Missing, GmSessionRunOperation.Unspecified));
    }
}
