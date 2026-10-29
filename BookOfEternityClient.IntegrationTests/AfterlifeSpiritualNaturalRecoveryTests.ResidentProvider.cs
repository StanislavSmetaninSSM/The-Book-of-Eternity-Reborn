using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    /// <summary>A real accepted withdrawal refuses treatment during its otherwise eligible next cycle.</summary>
    [Fact]
    public async Task ResidentProviderRevocationPublishesThenRefusesItsEligibleAidWithoutDraw()
    {
        await using var fixture = await HealingFixture.CreateAsync(3, 0, 14, stageRequest: false,
            beforeResourceBootstrap: ExplorerWoundSelfTreatmentTests.SeedEngineReceivingResidentAsync);
        var files = fixture.Files;
        _ = await CommitAidGrantAsync(fixture, receiveAid: true, resident: true);
        var prior = Assert.Single((await fixture.ReadHistoryAsync()).Transitions.Select(t => t.TransitionResult)
            .OfType<SpiritualHealingPersistedResult>()).ToCanonicalJson().ToJsonString();
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var scheduler = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
        var control = await scheduler.BuildControlForNextTurnAsync();
        JsonObject projection;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            projection = (await AfterlifeWoundTreatmentAuthority.ProjectNaturalRecoveryAsync(files, lease,
                new("session_spiritual_healing", "request_withdraw_offer", 44, control)))!;
        Assert.NotNull(projection);
        await new LiveTurnPreparationService(files).PrepareAsync(new()
        {
            SessionId = "session_spiritual_healing", RequestId = "request_withdraw_offer", TurnNumber = 44,
            CurrentRealm = "Chaos Sea", PlayerAction = "Обсудить отказ жителя от добровольной помощи; не начинать лечение.", ProgressionControl = control
        });
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
            var withdrawal = ResidentOfferProposal(); withdrawal["operation"] = "revoke"; withdrawal["providerCapable"] = false;
            profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorType"]!.GetValue<string>() == "resident" &&
                SpiritualTreatmentParticipation.ProfileId(p) == AidResidentId)[SpiritualTreatmentParticipation.ChangeProperty] = withdrawal;
            await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
            await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", new JsonObject
            {
                ["timestamp"] = DateTime.UtcNow.ToString("o"), ["dialogueOptions"] = new JsonArray(),
                ["spiritualHealingGraphs"] = AuthorNaturalGraphs(original, projection["destinationGraphs"]!.AsArray())
            }.ToJsonString());
            await WriteResidentAidMemoryAsync(files, lease, "request_withdraw_offer", 44, residentProvides: true);
            await WriteAidReportAsync(files, lease, control, "request_withdraw_offer", 44);
            var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease), i => i.Severity == IssueSeverity.Error);
            var issues = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.True(!issues.Any(i => i.Severity == IssueSeverity.Error), string.Join(";", issues.Select(i => i.Code + ":" + i.Actual)));
            var manifest = await fixture.ReadManifestAsync(lease);
            Assert.NotNull(await new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAccumulatedStateWithPlanAsync(manifest.Files));
            files.DeleteFile(lease, LiveTurnPreparationService.TurnRequestPath); // Direct fixture protocol cleanup, not claimed Engine cleanup.
        }
        var history = await fixture.ReadHistoryAsync();
        var results = history.Transitions.Select(t => t.TransitionResult).OfType<SpiritualHealingPersistedResult>().ToArray();
        Assert.Equal(2, results.Length); Assert.All(results, r => Assert.Equal(6, r.SchemaVersion));
        Assert.Equal(prior, results.Single(r => r.MatchesTurn("session_spiritual_healing", 43)).ToCanonicalJson().ToJsonString());
        var record = Assert.Single(results.Single(r => r.MatchesTurn("session_spiritual_healing", 44)).ParticipationOutcomes!)!["record"]!.AsObject();
        Assert.Equal("revoked", record["status"]!.GetValue<string>()); Assert.False(record["providerCapable"]!.GetValue<bool>());
        Assert.Equal(44, record["binding"]!["turn"]!.GetValue<int>());
        var schedule = JsonNode.Parse((await files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
        Assert.Equal(schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>() + 1, record["eligibleCycleOrdinal"]!.GetValue<int>());
        var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var selected = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
            { Aid = SpiritualTreatmentParticipation.Selection(record) with { PlayerConfirmed = true } };
        Assert.Equal("player_soul", wound.Owner.OwnerKind);
        var draws = 0;
        await using var refusal = await files.AcquireCanonicalWriteLeaseAsync();
        var before = fixture.CaptureMutationRoots(AfterlifeWoundTreatmentAuthority.AidLocalityPaths);
        var refused = await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, refusal, scheduler,
            "session_spiritual_healing", "request_after_withdrawal", 45, selected, count => { draws++; return [14]; });
        Assert.False(refused.Stage.IsStaged); Assert.Null(refused.Control); Assert.Equal(0, draws);
        fixture.AssertMutationRootsUnchanged(before); fixture.AssertResourcesUnchanged();
    }
}
