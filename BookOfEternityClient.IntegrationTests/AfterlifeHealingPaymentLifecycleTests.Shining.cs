using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeHealingPaymentLifecycleTests
{
    [Theory]
    [InlineData(1, "NoImprovement")] [InlineData(2, "RecoveryPoint")] [InlineData(20, "ReduceTwo")]
    public Task ShiningSameElyaraChargesActualOutcomeOnceAndReplays(int die, string band) =>
        RunAcceptedServiceAsync(die, band, "Shining Abode");

    [Fact]
    public async Task ShiningPaidOwnerNormalizesGuardiansUsingItsAlreadyParsedCurrentHall()
    {
        await using var fixture = await CreateServiceFixtureAsync(1, 20, realm: "Shining Abode");
        var files = fixture.Files;
        await StageAsync(fixture, 20, realm: "Shining Abode");
        await files.WriteFileAtomicAsync("output/interface_updates.json", new JsonObject { ["spiritualHealingGraphs"] = new JsonArray() }.ToJsonString());
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await fixture.WriteReportAsync(lease);
        Assert.DoesNotContain(await new ValidationService(files, NullLogger<ValidationService>.Instance)
            .PrepareSpiritualSelfTreatmentPublicationAsync(lease), issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var ready));
        var owner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(ready.Plan!.SpiritualHealingContinuation);
        try
        {
            var path = AfterlifeWoundTreatmentAuthority.ServiceLocalityPaths[0];
            var guardians = JsonNode.Parse((await files.ReadFileAsync(lease, path))!)!.AsObject();
            foreach (var guardian in guardians["guardians"]!.AsArray().OfType<JsonObject>())
                GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(guardian);
            guardians["activeGuardian"] = guardians["guardians"]![0]!.DeepClone();
            var content = guardians.ToJsonString(); var publishes = 0;
            Task Publish(CanonicalBeforeImage before) { publishes++; return files.WriteFileAtomicAsync(lease, path, content); }
            var shiningPath = ShiningAbodeState.StatePath;
            var shiningBefore = (await files.ReadFileBytesAsync(lease, shiningPath))!;
            var moved = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(shiningBefore))!;
            moved["currentHallId"] = "foreign_hall";
            await files.WriteFileAtomicAsync(lease, shiningPath, moved.ToJsonString());
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.PublishOwnedLocalityNormalizationAsync(files, lease, path, content, Publish));
            Assert.Equal(0, publishes);
            await files.WriteFileAtomicBytesAsync(lease, shiningPath, shiningBefore);
            await owner.PublishOwnedLocalityNormalizationAsync(files, lease, path, content, Publish);
            Assert.Equal(1, publishes); owner.RequireCurrent(files, lease);
            Assert.Equal(500, JsonNode.Parse((await files.ReadFileAsync(lease, AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!["inkFeathers"]!["current"]!.GetValue<int>());
            Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
            fixture.AssertResourcesUnchanged();
        }
        finally { owner.Close(); }
    }
}
