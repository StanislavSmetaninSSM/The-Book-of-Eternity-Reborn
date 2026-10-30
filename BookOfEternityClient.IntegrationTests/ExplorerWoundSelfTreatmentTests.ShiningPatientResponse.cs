using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    // Four independent worlds. Active44 rows first publish a real consent43;
    // protocol absence here is direct fixture setup, never claimed Engine cleanup.
    [Fact]
    public Task ShiningResidentConsentResponsePublishesAllRequiredActorMemory() => RunShiningPatientResponseAsync("resident", 43);
    [Fact]
    public Task RadiantPatientConsentResponsePublishesAllRequiredActorMemory() => RunShiningPatientResponseAsync("radiant_actor", 43);
    [Fact]
    public Task ShiningResidentActiveResponsePublishesAllRequiredActorMemory() => RunShiningPatientResponseAsync("resident", 44);
    [Fact]
    public Task RadiantPatientActiveResponsePublishesAllRequiredActorMemory() => RunShiningPatientResponseAsync("radiant_actor", 44);

    private static async Task RunShiningPatientResponseAsync(string kind, int lastTurn)
    {
        await using var fixture = await CreateShiningPatientFixtureAsync(kind, engine: false);
        var files = fixture.Files;
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        var scheduler = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
        JsonArray? previousLedger = null;
        JsonArray? previousResidents = null;
        JsonArray? previousGuardians = null;
        var wallet = SpiritualTreatmentWalletInvariant.Capture(JsonNode.Parse((await files.ReadFileAsync(
            AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject());
        for (var number = 43; number <= lastTurn; number++)
        {
            var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            ProgressionControl control;
            JsonObject projection;
            var requestId = "request_patient_response_" + number;
            if (number == 43)
            {
                control = await scheduler.BuildControlForNextTurnAsync();
                await using var sourceLease = await files.AcquireCanonicalWriteLeaseAsync();
                projection = (await AfterlifeWoundTreatmentAuthority.ProjectNaturalRecoveryAsync(files, sourceLease,
                    new("session_patient_response", requestId, number, control)))!;
                Assert.NotNull(projection); Assert.Equal("natural", projection["mode"]!.GetValue<string>());
            }
            else
            {
                var state = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
                await using var stageLease = await files.AcquireCanonicalWriteLeaseAsync();
                var target = Assert.Single(state.Targets, t => t.Owner.OwnerKind == kind && t.Owner.OwnerId == ShiningPatientId);
                var detail = (await WoundApplicationService.SelectTargetAsync(files, stageLease, state, target.Value, "/лечить")).WoundCommandState!;
                var choice = Assert.Single(detail.Choices, c => c.Available && c.Selection.Aid?.ShiningPatient?.Target == target.Owner);
                var selected = await WoundApplicationService.ConfirmAsync(files, stageLease, detail, choice.Value);
                Assert.True(selected.Aid!.PlayerConfirmed);
                var draws = 0;
                var staged = await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, stageLease, scheduler,
                    "session_patient_response", requestId, number, selected, count => { draws++; return Enumerable.Repeat(1, count).ToArray(); });
                Assert.True(staged.Stage.IsStaged, staged.Stage.IssueCode); Assert.Equal(1, draws);
                control = Assert.IsType<ProgressionControl>(staged.Control);
                projection = Assert.IsType<JsonObject>(staged.Stage.GmContext);
                Assert.Equal("player_to_shining_entity", projection["direction"]!.GetValue<string>());
                Assert.Equal(0, projection["targetNaturalHealingTier"]!.GetValue<int>());
            }
            await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = "session_patient_response", RequestId = requestId, TurnNumber = number,
                CurrentRealm = "Shining Abode", PlayerAction = number == 43
                    ? "Предложить находящемуся рядом обитателю помощь с его известной духовной раной; попытку лечения пока не начинать."
                    : WoundApplicationService.ShiningPatientAidActionText,
                ProgressionControl = control
            });
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                var turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease,
                    LiveTurnPreparationService.TurnRequestPath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                if (number == 43)
                {
                    var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
                    var patient = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p =>
                        p["actorType"]?.GetValue<string>() == kind && p["actorId"]?.GetValue<string>() == ShiningPatientId);
                    var shining = JsonNode.Parse((await files.ReadFileAsync(lease, ShiningAbodeState.StatePath))!)!;
                    patient[SpiritualTreatmentParticipation.ChangeProperty] = new JsonObject
                    {
                        ["schemaVersion"] = 5, ["kind"] = "shining_target_consent", ["operation"] = "grant", ["healerId"] = "player_soul",
                        ["woundId"] = wound.WoundId, ["locality"] = SpiritualTreatmentParticipation.LocalityJson(
                            SpiritualAidLocality.ForFaction(shining["factions"]![0]!["factionId"]!.GetValue<string>(), ShiningPatientHall)),
                        ["conscious"] = true, ["capable"] = true, ["readableEvidence"] = "Я осознанно разрешаю отдельную попытку лечения моей известной раны."
                    };
                    await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
                }
                await WriteNativeCompletionAsync(files, lease, turn,
                    AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(wound, projection["destinationGraphs"]!.AsArray()));
                await WriteShiningPatientReasoningAsync(files, lease, turn, kind);
                // Exercise the real conditional API transport as well as the
                // file-response writer. Distribution may change only output,
                // never the signed roster before its actual publisher owns it.
                var rosterBeforeTransport = await files.ReadFileBytesAsync(lease, GuardianAbodeResidentState.StatePath);
                var transported = JsonSerializer.Deserialize<GameResponse>((await files.ReadFileAsync(lease, "output/interface_updates.json"))!,
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                await new StateDistributor(files, NullLogger<StateDistributor>.Instance).DistributeAsync(lease, transported);
                Assert.Equal(rosterBeforeTransport, await files.ReadFileBytesAsync(lease, GuardianAbodeResidentState.StatePath));
                Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
                Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease), i => i.Severity == IssueSeverity.Error);
                Assert.DoesNotContain(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease), i => i.Severity == IssueSeverity.Error);
                var manifest = await fixture.ReadManifestAsync(lease);
                var originals = manifest.Files.Values.ToDictionary(path => path, path => files.ReadFileBytesSync(lease, path));
                Assert.NotNull(await new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance)
                    .BindTo(lease).NormalizeAccumulatedStateWithPlanAsync(manifest.Files));
                foreach (var pair in originals) Assert.Equal(pair.Value, await files.ReadFileBytesAsync(lease, pair.Key));
            }
            var result = Assert.Single((await fixture.ReadHistoryAsync()).Transitions.Select(t => t.TransitionResult)
                .OfType<SpiritualHealingPersistedResult>(), r => r.MatchesTurn("session_patient_response", number));
            Assert.Equal(number == 43, result.IsNatural); Assert.Equal(8, result.SchemaVersion);
            var memory = await AssertShiningPatientResponseMemoryAsync(files, kind, number, previousLedger, previousResidents, previousGuardians);
            previousLedger = memory.Ledger; previousResidents = memory.Residents; previousGuardians = memory.Guardians;
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnInterfacePayloadAsync(), i => i.Severity == IssueSeverity.Error);
            var issues = await validator.ValidateAcceptedTurnReasoningAsync();
            Assert.True(issues.All(i => i.Severity != IssueSeverity.Error),
                "Shining patient response memory failed Full reasoning: " + string.Join("; ", issues.Select(i => i.Code + ":" + i.Actor + ":" + i.Actual)));
            wallet.RequirePreserved(JsonNode.Parse((await files.ReadFileAsync(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject());
            fixture.AssertResourcesUnchanged();
            await using var cleanup = await files.AcquireCanonicalWriteLeaseAsync();
            files.DeleteFile(cleanup, LiveTurnPreparationService.TurnRequestPath); // Direct native protocol absence only.
        }
    }
}
