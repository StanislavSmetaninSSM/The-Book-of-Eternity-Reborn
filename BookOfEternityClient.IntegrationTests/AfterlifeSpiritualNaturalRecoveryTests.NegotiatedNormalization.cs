using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NegotiatedFactionOwnerAdvancesBothShiningGuardsAndKeepsSignedOriginalAndMemory(bool omitUnrelatedGuardian)
    {
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateFactionServiceFixtureAsync(3, 14,
            additionalBeforeResourceBootstrap: async (files, seedLease, profiles) =>
            {
                if (!omitUnrelatedGuardian) return;
                var actors = profiles["profiles"]!.AsArray();
                foreach (var actor in actors.OfType<JsonObject>().Where(p => p["actorId"]?.GetValue<string>() == AfterlifeWoundTreatmentAuthority.ElyaraId).ToArray())
                    actors.Remove(actor);
                files.DeleteFile(seedLease, AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0]);
                await files.WriteFileAtomicAsync(seedLease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
            });
        var files = fixture.Files;
        await StageAsync(fixture, "Shining Abode");
        byte[] originalShining = []; string originalPath = "";
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
            var roster = JsonNode.Parse((await files.ReadFileAsync(lease, GuardianAbodeResidentState.StatePath))!)!.AsObject();
            var shiningPath = ShiningAbodeState.StatePath;
            var shining = JsonNode.Parse((await files.ReadFileAsync(lease, shiningPath))!)!.AsObject();
            var healer = AfterlifeHealingPaymentLifecycleTests.FactionHealerId;
            var member = roster["entries"]!.AsArray().OfType<JsonObject>().Single(r => r["residentId"]!.GetValue<string>() == healer);
            var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            var proposal = ResidentOfferProposal(); proposal["woundId"] = wound.WoundId;
            proposal["locality"] = SpiritualTreatmentParticipation.LocalityJson(SpiritualAidLocality.ForFaction(
                member["shiningFactionId"]!.GetValue<string>(), shining["currentHallId"]!.GetValue<string>()));
            proposal["compensation"] = new JsonObject { ["kind"] = "favor", ["agreementRef"] = "agreement_native_normalization",
                ["readableTerms"] = "Перенести архив после отдельно подтверждённой попытки." };
            profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]!.GetValue<string>() == healer)
                [SpiritualTreatmentParticipation.ChangeProperty] = proposal;
            await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
            await fixture.WriteReportAsync(lease);
            await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", new JsonObject
            {
                ["timestamp"] = DateTime.UtcNow.ToString("o"), ["dialogueOptions"] = new JsonArray(),
                [SpiritualTreatmentParticipation.ResidentThoughtResponseProperty] = new JsonArray(new JsonObject
                {
                    ["entryId"] = "negotiated_normalization_thought", ["residentId"] = healer, ["turn"] = 43,
                    ["timestamp"] = DateTime.UtcNow.ToString("o"), ["title"] = "Предложение помощи",
                    ["summary"] = "Я предлагаю явно оговорённую помощь; душа ещё не подтвердила лечение.",
                    ["eventType"] = "conversation", ["consequence"] = "Предложение само не создаёт обязательства души.",
                    ["attitude"] = "attentive", ["intent"] = "Сохранить самостоятельный выбор души.", ["tags"] = new JsonArray()
                })
            }.ToJsonString());
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease), i => i.Severity == IssueSeverity.Error);
            Assert.DoesNotContain(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease), i => i.Severity == IssueSeverity.Error);
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var ready));
            var owner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(ready.Plan!.SpiritualHealingContinuation);
            try
            {
                var sourceFingerprint = owner.SourceFingerprint;
                var record = Assert.Single(owner.ParticipationOutcomes!.OfType<JsonObject>())["record"]!.AsObject();
                var selected = AfterlifeWoundHealingPlanner.ReadOriginalAidLocalitySource(files, lease, SpiritualTreatmentParticipation.Selection(record));
                Assert.True(selected.Success); Assert.NotNull(selected.Snapshot); Assert.NotNull(selected.Evidence);
                Assert.Equal(new[] { GuardianAbodeResidentState.StatePath }, selected.Snapshot.CoveredLogicalPaths);
                selected.Evidence.RequireCurrent(files, lease, selected.Snapshot);
                var manifest = await fixture.ReadManifestAsync(lease);
                originalPath = manifest.Files[shiningPath];
                originalShining = (await files.ReadFileBytesAsync(lease, originalPath))!;
                if (omitUnrelatedGuardian)
                {
                    Assert.False(manifest.Files.ContainsKey(AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0]));
                    Assert.False(AfterlifeWoundHealingPlanner.ReadOriginalAidLocalitySource(files, lease).Success);
                }
                var before = (await files.ReadFileBytesAsync(lease, shiningPath))!;
                // Only JSON formatting changes. The real owned writer must advance
                // both live Shining guards while preserving the signed source bytes.
                ShiningAbodeState.NormalizeStateRoot(shining, roster);
                var content = shining.ToJsonString(new() { WriteIndented = true }) + "\n";
                if (before.AsSpan().SequenceEqual(FileSystemManager.EncodeUtf8WithPreamble(content))) content += "\n";
                Assert.False(before.AsSpan().SequenceEqual(FileSystemManager.EncodeUtf8WithPreamble(content)));
                var writes = 0;
                Task Publish(CanonicalBeforeImage expected)
                {
                    Assert.Equal(new CanonicalBeforeImage(true, before).Fingerprint, expected.Fingerprint);
                    writes++; return files.WriteFileAtomicAsync(lease, shiningPath, content);
                }
                var foreign = shining.DeepClone().AsObject(); foreign["currentHallId"] = "hall_foreign";
                await Assert.ThrowsAsync<InvalidDataException>(() => owner.PublishOwnedLocalityNormalizationAsync(files, lease, shiningPath, foreign.ToJsonString(), Publish));
                Assert.Equal(0, writes);
                await owner.PublishOwnedLocalityNormalizationAsync(files, lease, shiningPath, content, Publish);
                Assert.Equal(1, writes); owner.RequireCurrent(files, lease);
                Assert.Equal(sourceFingerprint, owner.SourceFingerprint);
                Assert.Equal(originalShining, await files.ReadFileBytesAsync(lease, manifest.Files[shiningPath]));
                var published = await new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance)
                    .BindTo(lease).NormalizeAccumulatedStateWithPlanAsync(manifest.Files);
                Assert.Same(ready.Plan, published); Assert.True(owner.IsClosed);
                var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single((await fixture.ReadHistoryAsync()).Transitions,
                    t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
                Assert.True(result.IsNatural); Assert.Equal(6, result.SchemaVersion);
                Assert.True(JsonNode.DeepEquals(proposal["compensation"], result.ParticipationOutcomes![0]!["record"]!["compensation"]));
                Assert.Equal(originalShining, await files.ReadFileBytesAsync(lease, manifest.Files[shiningPath]));
                Assert.Single(JsonNode.Parse((await files.ReadFileAsync(lease, GuardianAbodeResidentState.StatePath))!)!["thoughtJournal"]!.AsArray());
                Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
                fixture.AssertResourcesUnchanged();
            }
            finally { owner.Close(); }
        }
        Assert.DoesNotContain(await validator.ValidateAcceptedTurnInterfacePayloadAsync(), i => i.Severity == IssueSeverity.Error);
        Assert.Equal(originalShining, await files.ReadFileBytesAsync(originalPath));
        fixture.AssertResourcesUnchanged();
    }
}
