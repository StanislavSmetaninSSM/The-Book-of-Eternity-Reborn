using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    // Actual signed preparation and common publication without an Engine/repair
    // loop. The same response writer used by the genuine journey is the subject.
    [Fact]
    public async Task ShiningResidentConsentResponsePublishesAllRequiredActorMemory()
    {
        await using var fixture = await CreateShiningPatientFixtureAsync("resident", engine: false);
        var files = fixture.Files;
        var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var control = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
            .BuildControlForNextTurnAsync();
        JsonObject projection;
        await using (var sourceLease = await files.AcquireCanonicalWriteLeaseAsync())
            projection = (await AfterlifeWoundTreatmentAuthority.ProjectNaturalRecoveryAsync(files, sourceLease,
                new("session_patient_response", "request_patient_response_43", 43, control)))!;
        Assert.NotNull(projection); Assert.Equal("natural", projection["mode"]!.GetValue<string>());
        await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "session_patient_response", RequestId = "request_patient_response_43", TurnNumber = 43,
            CurrentRealm = "Shining Abode", PlayerAction = "Предложить находящемуся рядом обитателю помощь с его известной духовной раной; попытку лечения пока не начинать.",
            ProgressionControl = control
        });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease,
                LiveTurnPreparationService.TurnRequestPath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
            var patient = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p =>
                p["actorType"]?.GetValue<string>() == "resident" && p["actorId"]?.GetValue<string>() == ShiningPatientId);
            var shining = JsonNode.Parse((await files.ReadFileAsync(lease, ShiningAbodeState.StatePath))!)!;
            patient[SpiritualTreatmentParticipation.ChangeProperty] = new JsonObject
            {
                ["schemaVersion"] = 5, ["kind"] = "shining_target_consent", ["operation"] = "grant", ["healerId"] = "player_soul",
                ["woundId"] = wound.WoundId, ["locality"] = SpiritualTreatmentParticipation.LocalityJson(
                    SpiritualAidLocality.ForFaction(shining["factions"]![0]!["factionId"]!.GetValue<string>(), ShiningPatientHall)),
                ["conscious"] = true, ["capable"] = true, ["readableEvidence"] = "Я осознанно разрешаю отдельную попытку лечения моей известной раны."
            };
            await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
            await WriteNativeCompletionAsync(files, lease, turn,
                AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(wound, projection["destinationGraphs"]!.AsArray()));
            await WriteShiningPatientReasoningAsync(files, lease, turn, "resident");
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease), i => i.Severity == IssueSeverity.Error);
            var preparation = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.DoesNotContain(preparation, i => i.Severity == IssueSeverity.Error);
            var manifest = await fixture.ReadManifestAsync(lease);
            Assert.NotNull(await new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAccumulatedStateWithPlanAsync(manifest.Files));
        }
        var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single((await fixture.ReadHistoryAsync()).Transitions,
            t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
        Assert.True(result.IsNatural); Assert.Equal(8, result.SchemaVersion);
        Assert.Equal(5, result.ParticipationOutcomes![0]!["record"]!["schemaVersion"]!.GetValue<int>());
        var residentJournal = JsonNode.Parse((await files.ReadFileAsync(GuardianAbodeResidentState.StatePath))!)!["thoughtJournal"]!.AsArray();
        Assert.Contains(residentJournal, e => e!["residentId"]?.GetValue<string>() == ShiningPatientId && e["turn"]!.GetValue<int>() == 43);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync();
        Assert.True(issues.All(i => i.Severity != IssueSeverity.Error),
            "Shining patient response memory failed Full reasoning: " + string.Join("; ", issues.Select(i => i.Code + ":" + i.Actor + ":" + i.Actual)));
    }
}
