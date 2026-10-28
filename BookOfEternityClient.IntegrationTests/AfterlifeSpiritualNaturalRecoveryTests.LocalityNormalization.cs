using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    [Fact]
    public async Task FullNormalizationPublishesParticipationWhileForeignLocalityWritesStayRefused()
    {
        await using (var fixture = await HealingFixture.CreateAsync(3, 3, 14, stageRequest: false,
            beforeResourceBootstrap: ExplorerWoundSelfTreatmentTests.SeedEngineAidGuardianAsync))
        {
            var files = fixture.Files;
            await PrepareGrantForFullNormalizationAsync(fixture);
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease), i => i.Severity == IssueSeverity.Error);
            Assert.DoesNotContain(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease), i => i.Severity == IssueSeverity.Error);
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var ready));
            var owner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(ready.Plan!.SpiritualHealingContinuation);
            var originalFingerprint = owner.SourceFingerprint;
            var path = AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0];
            var before = (await files.ReadFileBytesAsync(lease, path))!;
            var beforeRoot = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(before))!;
            Assert.Null(beforeRoot["guardians"]![0]![GuardianTradeRequestState.ReceiptsProperty]);
            var manifest = await fixture.ReadManifestAsync(lease);
            var originalLocality = (await files.ReadFileBytesAsync(lease, manifest.Files[path]))!;
            var published = await new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAccumulatedStateWithPlanAsync(manifest.Files);
            Assert.Same(ready.Plan, published);
            Assert.True(owner.IsClosed);
            Assert.Equal(originalFingerprint, owner.SourceFingerprint);
            Assert.Equal(originalLocality, await files.ReadFileBytesAsync(lease, manifest.Files[path]));
            var after = (await files.ReadFileBytesAsync(lease, path))!;
            Assert.False(before.AsSpan().SequenceEqual(after)); // Actual full-normalizer writer was required, not direct common publication.
            var afterRoot = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(after))!;
            Assert.Empty(afterRoot["guardians"]![0]![GuardianTradeRequestState.ReceiptsProperty]!.AsArray());
            Assert.Empty(afterRoot["activeGuardian"]![GuardianTradeRequestState.ReceiptsProperty]!.AsArray());
            var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single((await fixture.ReadHistoryAsync()).Transitions,
                t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
            Assert.True(result.IsNatural);
            Assert.Equal(3, result.SchemaVersion);
            Assert.Single(result.ParticipationOutcomes!);
            Assert.Equal(1, Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound.Recovery.CurrentStepProgress);
            fixture.AssertResourcesUnchanged();
        }

        // Independent mutable fixture: before/after guards are tested on the
        // genuine prepared issuer, without claiming another accepted grant.
        await using (var fixture = await HealingFixture.CreateAsync(3, 3, 14, stageRequest: false,
            beforeResourceBootstrap: ExplorerWoundSelfTreatmentTests.SeedEngineAidGuardianAsync))
        {
            var files = fixture.Files;
            await PrepareGrantForFullNormalizationAsync(fixture);
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            Assert.DoesNotContain(await new ValidationService(files, NullLogger<ValidationService>.Instance)
                .PrepareSpiritualSelfTreatmentPublicationAsync(lease), i => i.Severity == IssueSeverity.Error);
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var ready));
            var owner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(ready.Plan!.SpiritualHealingContinuation);
            try
            {
                var path = AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0];
                var before = (await files.ReadFileBytesAsync(lease, path))!;
                var normalized = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(before))!.AsObject();
                foreach (var guardian in normalized["guardians"]!.AsArray().OfType<JsonObject>())
                    GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(guardian);
                normalized["activeGuardian"] = normalized["guardians"]![0]!.DeepClone();
                var content = normalized.ToJsonString();
                var foreign = normalized.DeepClone().AsObject(); foreign["foreignUnownedChange"] = true;
                var callbackWrites = 0;
                Task Write(CanonicalBeforeImage _) { callbackWrites++; return files.WriteFileAtomicAsync(lease, path, content); }
                await files.WriteFileAtomicAsync(lease, path, foreign.ToJsonString());
                var altered = fixture.CaptureMutationRoots([path]);
                await Assert.ThrowsAsync<InvalidDataException>(() => owner.PublishOwnedLocalityNormalizationAsync(files, lease, path, content, Write));
                Assert.Equal(0, callbackWrites);
                fixture.AssertMutationRootsUnchanged(altered);
                await files.WriteFileAtomicBytesAsync(lease, path, before);
                var changedAuthority = normalized.DeepClone().AsObject();
                changedAuthority["chaosSeaNavigation"]!["currentAbodeId"] = "foreign_abode";
                await Assert.ThrowsAsync<InvalidDataException>(() => owner.PublishOwnedLocalityNormalizationAsync(files, lease, path, changedAuthority.ToJsonString(), Write));
                Assert.Equal(0, callbackWrites);
                await Assert.ThrowsAsync<InvalidDataException>(() => owner.PublishOwnedLocalityNormalizationAsync(files, lease, path, content, async beforeImage =>
                {
                    await Write(beforeImage);
                    await files.WriteFileAtomicAsync(lease, path, foreign.ToJsonString());
                }));
                await files.WriteFileAtomicBytesAsync(lease, path, before);
                owner.RequireCurrent(files, lease); // Failed exact readback did not advance the expected closing image.
                await owner.PublishOwnedLocalityNormalizationAsync(files, lease, path, content, Write);
                owner.RequireCurrent(files, lease);
                var expected = (await files.ReadFileBytesAsync(lease, path))!;
                await files.WriteFileAtomicAsync(lease, path, foreign.ToJsonString());
                Assert.Throws<InvalidDataException>(() => owner.RequireCurrent(files, lease));
                await files.WriteFileAtomicBytesAsync(lease, path, expected);
                owner.RequireCurrent(files, lease);
                Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.TransitionResult is SpiritualHealingPersistedResult);
                fixture.AssertResourcesUnchanged();
            }
            finally { owner.Close(); }
        }
    }

    private static async Task PrepareGrantForFullNormalizationAsync(HealingFixture fixture)
    {
        await StageAsync(fixture);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
        profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => SpiritualTreatmentParticipation.ProfileId(p) == AidGuardianId)
            [SpiritualTreatmentParticipation.ChangeProperty] = AidGrantProposal();
        await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await fixture.WriteReportAsync(lease);
    }
}
