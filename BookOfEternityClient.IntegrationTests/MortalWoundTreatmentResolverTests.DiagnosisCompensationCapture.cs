using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task DiagnosisPublicationCapturePreservesStaticRowsAndProvesEveryDynamicOriginalBeforeTake()
    {
        var observe = false;
        var opens = new Dictionary<string, int>(StringComparer.Ordinal);
        var hooks = new FileSystemManagerHooks { CanonicalReadActivityObserver = path =>
        {
            if (observe) opens[path] = opens.GetValueOrDefault(path) + 1;
        } };
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 30, hooks: hooks);
        var composed = await ComposeDiagnosisCommonContextAsync(fixture);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(composed.Plan);
        var authority = Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(plan.TreatmentResourcePublicationAuthority);
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(authority.RequestAuthority.ModeAuthority);
        var heldBytes = fixture.CaptureJson();
        var original = await AcceptedTurnCanonicalStateRefresh.CaptureBeforeImagesAsync(fixture.Files, fixture.Lease);
        var before = plan.BeforeImages;
        const string clockPath = EffectAcceptedTurnInputComposer.WorldTimePath;
        const string absentPath = EffectAcceptedTurnPlan.CommandPath;
        Assert.Contains(clockPath, plan.TouchedPaths);
        Assert.DoesNotContain(original, row => row.Path == clockPath);
        Assert.True(before[clockPath].Existed);
        Assert.Contains(absentPath, plan.TouchedPaths); // Genuine deletion in this real effect plan.
        Assert.False(before[absentPath].Existed);
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, absentPath));
        var missing = Assert.Throws<InvalidDataException>(() =>
            MortalWoundTreatmentResourcePublicationTransaction.RequireCompleteBeforeImageCoverage(plan, original));
        Assert.Contains(clockPath, missing.Message);

        // Exercise a genuinely absent touched path as an additional missing
        // capture row; its plan/absence was issued by the actual producer.
        var partial = original.Where(row => row.Path != absentPath).ToArray();
        var keys = partial.Select(row => row.Path).ToHashSet(StringComparer.Ordinal);
        var additions = plan.TouchedPaths.Where(path => !keys.Contains(path)).ToArray();
        var fingerprints = partial.ToDictionary(row => row.Path,
            row => new CanonicalBeforeImage(row.Bytes is not null, row.Bytes).Fingerprint, StringComparer.Ordinal);
        observe = true;
        var complete = await AcceptedTurnCanonicalStateRefresh.CaptureTreatmentBeforeImagesAsync(
            fixture.Files, fixture.Lease, plan, partial);
        observe = false;
        Assert.Equal(partial.Length + additions.Length, complete.Count);
        foreach (var row in partial)
        {
            Assert.Same(row, Assert.Single(complete, value => value.Path == row.Path));
            Assert.Equal(0, opens.GetValueOrDefault(row.Path));
        }
        foreach (var path in additions)
        {
            var row = Assert.Single(complete, value => value.Path == path);
            Assert.Equal(before[path].Bytes, row.Bytes);
            // Missing files are freshly observed as absent, without an opened
            // stream. Each present added source is physically opened once.
            Assert.Equal(before[path].Existed ? 1 : 0, opens.GetValueOrDefault(path));
        }
        var capturedClock = Assert.Single(complete, row => row.Path == clockPath);
        var escaped = Assert.IsType<byte[]>(capturedClock.Bytes);
        escaped[0] ^= 1;
        Assert.Equal(before[clockPath].Bytes, capturedClock.Bytes);
        Assert.Null(Assert.Single(complete, row => row.Path == absentPath).Bytes);
        MortalWoundTreatmentResourcePublicationTransaction.RequireCompleteBeforeImageCoverage(plan, complete);
        fixture.AssertJson(heldBytes);

        var normalizer = new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance).BindTo(fixture.Lease);
        var preflight = Assert.IsType<CanonicalStateNormalizer.TreatmentResourcePublicationPreflight>(
            await normalizer.PrevalidateTreatmentResourcePublicationTransactionAsync());
        fixture.AssertJson(heldBytes); // Actual preflight remains canonical read-only.
        Assert.Same(plan, preflight.Plan);

        foreach (var fault in new[] { "changed_clock", "missing_clock", "unexpected_effect_command" })
        {
            var originalClock = Assert.IsType<byte[]>(before[clockPath].Bytes);
            try
            {
                if (fault == "missing_clock") fixture.Files.DeleteFile(fixture.Lease, clockPath);
                else if (fault == "changed_clock")
                    await fixture.Files.WriteFileAtomicBytesAsync(fixture.Lease, clockPath, originalClock.Concat(new byte[] { 32 }).ToArray());
                else await fixture.Files.WriteFileAtomicAsync(fixture.Lease, absentPath, "{}");
                var faultImage = fixture.CaptureJson();
                var failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
                    AcceptedTurnCanonicalStateRefresh.CaptureTreatmentBeforeImagesAsync(fixture.Files, fixture.Lease, plan, partial));
                Assert.Contains(fault == "unexpected_effect_command" ? absentPath : clockPath, failure.Message);
                fixture.AssertJson(faultImage); // No partial capture publishes or restores anything.
            }
            finally
            {
                await fixture.Files.WriteFileAtomicBytesAsync(fixture.Lease, clockPath, originalClock);
                fixture.Files.DeleteFile(fixture.Lease, absentPath);
            }
        }
        foreach (var row in partial)
            Assert.Equal(fingerprints[row.Path], new CanonicalBeforeImage(row.Bytes is not null, row.Bytes).Fingerprint);
        fixture.AssertJson(heldBytes);
        Assert.True(authority.AcceptedStateAuthority.HasLiveProcedureReservationAgreement(check));
        var hold = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(fixture.Files,
            fixture.Lease, authority.RequestAuthority, authority.Finalization);
        Assert.True(hold.IsValid, JsonSerializer.Serialize(hold.Issues));
        Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, hold.State);

        // The same full-coverage gate is called by the actual nonterminal
        // transaction factory; it is a DATA refusal, not a forged take receipt.
        Assert.Throws<InvalidDataException>(() =>
            MortalWoundTreatmentResourcePublicationTransaction.RequireCompleteBeforeImageCoverage(plan, original));
        fixture.AssertJson(heldBytes);
    }

    [Fact]
    public async Task DiagnosisTerminalOnlyCleanupPreservesUnrelatedClockDriftWithoutPublicationPermission()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 30);
        var composed = await ComposeDiagnosisCommonContextAsync(fixture);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(composed.Plan);
        var authority = Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(plan.TreatmentResourcePublicationAuthority);
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(authority.RequestAuthority.ModeAuthority);
        var normalizer = new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance).BindTo(fixture.Lease);
        var preflight = Assert.IsType<CanonicalStateNormalizer.TreatmentResourcePublicationPreflight>(
            await normalizer.PrevalidateTreatmentResourcePublicationTransactionAsync());
        Assert.True(authority.AcceptedStateAuthority.HasLiveProcedureReservationAgreement(check));
        const string clockPath = EffectAcceptedTurnInputComposer.WorldTimePath;
        var clock = JsonNode.Parse(FileSystemManager.DecodeFileBytes(Assert.IsType<byte[]>(
            await fixture.Files.ReadFileBytesAsync(fixture.Lease, clockPath))))!.AsObject();
        clock["currentTimeInMinutes"] = clock["currentTimeInMinutes"]!.GetValue<long>() + 1;
        await fixture.Files.WriteFileAtomicAsync(fixture.Lease, clockPath, clock.ToJsonString());
        var drifted = fixture.CaptureJson();
        var expectedPending = JsonNode.Parse(FileSystemManager.DecodeFileBytes(
            drifted[fixture.Files.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath)]))!.AsObject();
        var heldRow = Assert.IsType<JsonObject>(Assert.Single(expectedPending["submittedTreatmentRequests"]!.AsArray()));
        Assert.Equal(authority.RequestFingerprint, heldRow["requestFingerprint"]!.GetValue<string>());
        expectedPending.Remove("submittedTreatmentRequests");
        var clockBytes = Assert.IsType<byte[]>(await fixture.Files.ReadFileBytesAsync(fixture.Lease, clockPath));
        var original = await AcceptedTurnCanonicalStateRefresh.CaptureBeforeImagesAsync(fixture.Files, fixture.Lease);
        Assert.DoesNotContain(original, row => row.Path == clockPath);
        Assert.True(AcceptedMechanicsPlanAuthority.TryTakeCurrentValidatedTreatmentPublicationForTerminalRelease(
            fixture.Files, fixture.Lease, preflight.Binding, preflight.MortalItemSnapshot, out var taken, out var receipt));
        Assert.Same(plan, taken.Plan);
        Assert.True(receipt.IsTerminalReleaseOnly);
        var transaction = MortalWoundTreatmentResourcePublicationTransaction.Create(
            fixture.Files, plan, preflight.Binding, receipt, original);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CapturePublishedAgreementAsync(fixture.Files, fixture.Lease));
            var refusedRearm = AcceptedTurnAuthorityRegistry.RearmTakenMortalWoundTreatmentPublication(fixture.Files, fixture.Lease, receipt);
            Assert.False(refusedRearm.IsValid);
            Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Stale, refusedRearm.Outcome);
            fixture.AssertJson(drifted);
            var released = await transaction.ReleaseTerminalUnderLeaseAsync(fixture.Files, fixture.Lease, "validation_failed");
            Assert.True(released.IsValid, JsonSerializer.Serialize(released.Issues));
            Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Released, released.Outcome);
            Assert.Equal(clockBytes, await fixture.Files.ReadFileBytesAsync(fixture.Lease, clockPath));
            Assert.False(authority.AcceptedStateAuthority.HasLiveProcedureReservationAgreement(check));
            var releasedClaim = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
                fixture.Files, fixture.Lease, authority.RequestAuthority, authority.Finalization);
            Assert.False(releasedClaim.IsValid);
            Assert.Null(releasedClaim.State);
            var command = JsonNode.Parse(Assert.IsType<string>(await fixture.Files.ReadFileAsync(fixture.Lease,
                AcceptedMechanicsPlan.WoundCommandPath)))!.AsObject();
            var pending = JsonNode.Parse(Assert.IsType<string>(await fixture.Files.ReadFileAsync(fixture.Lease,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath)))!.AsObject();
            Assert.True(JsonNode.DeepEquals(expectedPending, pending));
            Assert.False(MortalWoundTreatmentDurableSurfaceQuarantine.ContainsExactRequestRows(command, pending,
                authority.RequestAuthority.Coordinates.OperationKey, authority.RequestAuthority.Coordinates.AttemptId,
                authority.RequestFingerprint));
            var after = fixture.CaptureJson();
            var allowed = new[] { fixture.Files.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath) }
                .ToHashSet(StringComparer.Ordinal);
            Assert.Equal(drifted.Keys.Where(path => !allowed.Contains(path)).Order(StringComparer.Ordinal),
                after.Keys.Where(path => !allowed.Contains(path)).Order(StringComparer.Ordinal));
            foreach (var row in drifted.Where(row => !allowed.Contains(row.Key)))
                Assert.True(row.Value.AsSpan().SequenceEqual(after[row.Key]), "Terminal cleanup changed unrelated path: " + row.Key);
        }
        finally
        {
            await fixture.ReleaseLeaseAsync();
            await ((IAsyncDisposable)transaction).DisposeAsync();
        }
    }
}
