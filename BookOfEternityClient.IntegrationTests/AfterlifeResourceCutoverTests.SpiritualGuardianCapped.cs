using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Proves the genuine signed zero-exchange original, post-sign capped ordinary candidate and unstaged source prefix without querying v5 eligibility.</summary>
    /// <param name="rank">Independent complete retained guardianIII/IV graph, fixed before signing.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public async Task SpiritualGuardianCapped_SignedPrerequisitesAreLawful(int rank)
    {
        var name = "guardian_capped_signed_prerequisite_" + rank;
        try
        {
            await WithGuardianAttributionJourneyAsync(name, async setup =>
            {
                var context = setup.Copy.Context;
                FileSystemManager.CanonicalWriteLease? lease = null;
                ValidationService.SpiritualOriginalTurnCapture? capture = null;
                IReadOnlyDictionary<string, byte[]>? files = null;
                IReadOnlyDictionary<string, byte[]>? disposalFiles = null;
                byte[]? generation = null;
                await ExecutePressureGroupFixtureStagesAsync(new(
                    Work: async () =>
                    {
                        lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                        var originalRaw = Assert.IsType<byte[]>(setup.Origin.Tracked[AfterlifeSpiritualConflictState.StatePath]);
                        var originalRoot = ReadGuardianAttributionJson(originalRaw);
                        var originalActive = originalRoot["activeConflict"]!.AsObject();
                        Assert.Empty(originalActive["exchangeLog"]!.AsArray());
                        Assert.Equal(("clear", "strained"), (originalActive["playerSideStrain"]!.GetValue<string>(),
                            originalActive["oppositionSideStrain"]!.GetValue<string>()));
                        var originalProfiles = ReadGuardianAttributionJson(Assert.IsType<byte[]>(setup.Origin.Tracked[AfterlifeEntityProfileState.StatePath]));
                        SpiritualGuardianAttributionDataCheck.Require(setup.Retained, originalProfiles, originalRoot, rank);
                        var ordinaryRaw = Assert.IsType<byte[]>(setup.Authored[AfterlifeSpiritualConflictState.StatePath]);
                        Assert.Equal(ordinaryRaw, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
                        Assert.True(ordinaryRaw.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
                        Assert.True(JsonNode.DeepEquals(setup.Copy.Conflict, ReadGuardianAttributionJson(ordinaryRaw)));
                        Assert.True(JsonNode.DeepEquals(setup.Ordinary, setup.Actual));
                        Assert.True(JsonNode.DeepEquals(setup.Copy.Conflict, setup.ActualRoot));
                        Assert.NotEqual(new CanonicalBeforeImage(true, originalRaw).Fingerprint,
                            new CanonicalBeforeImage(true, ordinaryRaw).Fingerprint);
                        Assert.Equal((20L, 3, 20L, 3), (setup.OrdinaryCalculation.TraumaPressure, setup.OrdinaryCalculation.MaximumSeverityRank,
                            setup.ActualCalculation.TraumaPressure, setup.ActualCalculation.MaximumSeverityRank));

                        var recorded = await MeasurePressureOwnerPhaseAsync("guardian_capped_prerequisite_intake", name,
                            () => context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease));
                        capture = recorded.Capture; // Capture cleanup ownership before any assertion can fail.
                        RequireGuardianJourneyIssues(name, "capped_prerequisite_intake", recorded.Issues);
                        Assert.NotNull(capture);
                        RequireGuardianJourneyIssues(name, "capped_prerequisite_resource_begin",
                            await MeasurePressureOwnerPhaseAsync("guardian_capped_prerequisite_resource_begin", name,
                                () => capture.BeginResourceExecutionAsync(lease)));
                        var prepared = await MeasurePressureOwnerPhaseAsync("guardian_capped_prerequisite_original_prefix", name,
                            () => capture.PrepareOriginalResourcePrefixAsync(lease));
                        RequireGuardianJourneyIssues(name, "capped_prerequisite_original_prefix", prepared.Issues);
                        Assert.True(prepared.Ready); Assert.False(prepared.PrefixBlocked);
                        files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                        generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                        var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, name + "_prepared");
                        Assert.Equal(0, frontier.NextOrdinal); Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
                        Assert.True(capture.IsCurrentOwner);
                        var counts = capture.ReadExistingStrainReuseCounts();
                        Assert.Equal((0, 0, 0, 0), (counts.Queries, counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations));
                        var sourceCounts = capture.ReadExistingPressureGroupComputationCounts();
                        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(0, 0, 0, 0, 0), sourceCounts.CurrentBatch);
                        Assert.Equal(sourceCounts.CurrentBatch, sourceCounts.SignedBatch); Assert.Equal(0, sourceCounts.ResourceBatchComputations);
                        AssertExistingPressureInputInert(context, lease, files, generation);
                        AssertPressureNoPrivatePair(context, lease);
                        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                        _storageOutput.WriteLine(JsonSerializer.Serialize(new
                        {
                            phase = "guardian_capped_genuine_signed_prerequisites", name, rank,
                            chronology = "signed zero-exchange original, then ordinary candidate; no ordinary query or actual admission",
                            originalRawBase64 = Convert.ToBase64String(originalRaw), ordinaryRawBase64 = Convert.ToBase64String(ordinaryRaw),
                            originalRoot, originalProfiles, ordinaryRoot = setup.Copy.Conflict,
                            setup.OrdinaryCalculation, setup.ActualCalculation, setup.Retained, frontier, counts, sourceCounts
                        }));
                    },
                    HasWindow: () => files is not null, HasCapture: () => capture is not null,
                    HasLease: () => lease is not null, HasContext: () => false,
                    AssertWindowInert: () => { AssertExistingPressureInputInert(context, lease!, files!, generation!); return Task.CompletedTask; },
                    CaptureDisposalFiles: () => disposalFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath),
                    DisposeCaptureExplicitly: () => capture!.Dispose(),
                    AssertDisposedCaptureInert: () =>
                    {
                        Assert.False(capture!.IsCurrentOwner);
                        if (disposalFiles is not null && generation is not null)
                            AssertExistingPressureInputInert(context, lease!, disposalFiles, generation);
                    },
                    DisposeCaptureImplicitly: () => capture!.Dispose(), AssertNoPrivatePair: () => AssertPressureNoPrivatePair(context, lease!),
                    DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
            }, retainedRank: rank, contour: GuardianSingletonPressureContour.CappedBroken);
        }
        catch (Exception primary)
        {
            WriteSingletonIvOwnerFailure(name, primary); // Includes all retained eligible cleanup failures; always rethrow.
            throw;
        }
    }

    /// <summary>Desired genuine ordinary-IV/actual-IV and C1 success after lawful signed/current preflight; refusal remains a causal failure.</summary>
    /// <param name="rank">Independent complete retained guardianIII/IV parent.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public Task SpiritualGuardianCapped_RealOwnerAdvancesAndComposesC1(int rank) =>
        AssertGuardianSingletonOwnerAsync("guardian_capped_owner_" + rank, rank, GuardianSingletonPressureContour.CappedBroken);
}
