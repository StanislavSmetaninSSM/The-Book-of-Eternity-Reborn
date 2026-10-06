using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRunRecordTests
{
    [Theory]
    [InlineData("Prepared")]
    [InlineData("LaunchIntent")]
    [InlineData("ReleaseIntent")]
    [InlineData("Released")]
    [InlineData("StopValidated")]
    [InlineData("PublicationIntent")]
    [InlineData("Published")]
    [InlineData("CleanupPending")]
    [InlineData("Uncertain")]
    public void ColdNonterminalRecord_RetainsExactIdentityAndRemainsUncertain(string phase)
    {
        var record = new WorkerRunRecord(1, Identity(), Enum.Parse<WorkerRunPhase>(phase));
        var observation = GmWorkerRunRecordCodec.Observe(Bytes(record));
        Assert.Equal(WorkerRunObservationKind.Uncertain, observation.Kind);
        Assert.Equal(record, observation.Record);
    }

    internal static WorkerRunIdentity Identity() => new(
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ledger-record-root")), 1,
        "11111111111111111111111111111111", "22222222222222222222222222222222",
        "worker", "task", new string('a', 64), WorkerRunBackend.LinuxNativeLineage,
        WorkerRunScope.OrdinarySamePidNamespace, "33333333333333333333333333333333",
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ledger-record-workspace")));

    internal static byte[] Bytes(WorkerRunRecord record) => JsonSerializer.SerializeToUtf8Bytes(record,
        new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } });
}
