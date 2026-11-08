using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Full retained player IV, v3 ordinary13/II→actual18/III, real first C2/cold/none/C3/common C4 and signed47.</summary>
    [Fact]
    public Task SpiritualSingletonIv_PlayerActualColdNonePublishesOnceAndValidatesSignedNext() =>
        WithSingletonIvJourneyFailureAsync("singleton_iv_player_publication", () =>
            WithSingletonIvPlayerJourneyAsync("singleton_iv_player_publication", async setup =>
            {
                var completed = await CompleteGuardianAttributionColdC3Async(setup, setup.Copy.Context.Validator);
                await PublishGuardianAttributionAndReadNextAsync(setup, setup.Copy.Context.Validator, completed);
            }));

    /// <summary>Full retained guardian IV, v5 ordinary10/I→actual15/II, real first C2/cold/none/C3/common C4 and signed47.</summary>
    [Fact]
    public Task SpiritualSingletonIv_GuardianActualColdNonePublishesOnceAndValidatesSignedNext() =>
        WithSingletonIvJourneyFailureAsync("singleton_iv_guardian_publication", () =>
            WithGuardianAttributionJourneyAsync("singleton_iv_guardian_publication", async setup =>
            {
                var completed = await CompleteGuardianAttributionColdC3Async(setup, setup.Copy.Context.Validator);
                await PublishGuardianAttributionAndReadNextAsync(setup, setup.Copy.Context.Validator, completed);
            }, retainedRank: 4));

    /// <summary>Witnesses committed v3 writes before the late cut, exact signed-origin rollback and a fresh same-byte IV player retry.</summary>
    [Fact]
    public Task SpiritualSingletonIv_PlayerLatePublicationCutRestoresSignedOriginAndFreshRetryPublishesOnce() =>
        WithSingletonIvJourneyFailureAsync("singleton_iv_player_rollback", () =>
            AssertGuardianAttributionLatePublicationCutAsync("singleton_iv_player_rollback", retainedRank: 4,
                role: PressureSingletonJourneyRole.Player));

    /// <summary>Witnesses committed v5 writes before the late cut, exact signed-origin rollback and a fresh same-byte IV guardian retry.</summary>
    [Fact]
    public Task SpiritualSingletonIv_GuardianLatePublicationCutRestoresSignedOriginAndFreshRetryPublishesOnce() =>
        WithSingletonIvJourneyFailureAsync("singleton_iv_guardian_rollback", () =>
            AssertGuardianAttributionLatePublicationCutAsync("singleton_iv_guardian_rollback", retainedRank: 4));

    /// <summary>Emits all retained primary/cleanup failures after the existing fixed ownership stages have completed.</summary>
    /// <param name="name">The exact independent journey identity.</param>
    /// <param name="work">The original journey including its complete cleanup.</param>
    private async Task WithSingletonIvJourneyFailureAsync(string name, Func<Task> work)
    {
        try { await work(); }
        catch (Exception primary)
        {
            WriteSingletonIvOwnerFailure(name, primary);
            throw;
        }
    }

    /// <summary>
    /// Reuses the lawful group factory with exactly one complete III/IV player parent and a closed contour fixed before signing.
    /// Only ordinary/output bytes are authored afterward; retained synthetic creation is not a native birth.
    /// </summary>
    /// <param name="name">Independent per-Fact identity for the signature and diagnostics.</param>
    /// <param name="work">Work using only detached setup DATA after the setup lease closes.</param>
    /// <param name="hooks">The existing typed hooks for the witnessed late publication cut.</param>
    /// <param name="retainedRank">Independent expected parentIII/IV; old IV default is unchanged.</param>
    /// <param name="contour">Closed old below-broken or new reaching-broken player DATA; old default is unchanged.</param>
    private async Task WithSingletonIvPlayerJourneyAsync(string name, Func<GuardianAttributionJourney, Task> work,
        FileSystemManagerHooks? hooks = null, int retainedRank = 4,
        PlayerSingletonPressureContour contour = PlayerSingletonPressureContour.BelowBroken)
    {
        if (retainedRank is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(retainedRank));
        var spec = new PressureGroupCase(name, Parents: 1, After: SpiritualSingletonIvPlayerDataTests.OrdinaryStrain(contour),
            Ranks: new(retainedRank, 3), AdvanceExpected: true);
        var data = SpiritualPressureGroupOwnerCaseData.Create(1, ranks: new(retainedRank, 3));
        ResourceMaterializationTestContext? context = null;
        await ExecutePressureGroupFixtureStagesAsync(new(
            Work: async () =>
            {
                var started = Stopwatch.GetTimestamp();
                context = await CreatePressureGroupSignedContextAsync(spec, data, allocated => context = allocated, hooks);
                WritePressureOwnerTiming("singleton_iv_player_journey_signed_preparation", name, started);
                GuardianAttributionJourney? setup = null;
                await ExecuteGuardianLeaseStagesAsync(context, async lease =>
                {
                    var signed = await AssertPressureGroupSignedInputsAsync(context, lease, spec, data);
                    // Capture the physical origin before ordinary authoring, including snapshot/signature and all absences.
                    var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                    var tracked = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
                    foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[]
                             { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                                 AcceptedMechanicsPlan.WoundCommandPath, SpiritualWoundOpportunityReceiptState.StatePath,
                                 ProjectionNarrativePath, ProjectionInterfacePath }).Distinct(StringComparer.Ordinal))
                        tracked[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
                    var receipt = ReadGuardianAttributionJson(tracked[SpiritualWoundOpportunityReceiptState.StatePath]!);
                    foreach (var field in new[] { "instances", "closures", "sources", "decisions" }) Assert.Single(receipt[field]!.AsArray());
                    var origin = new PressurePairSignedOrigin(files, tracked,
                        File.ReadAllBytes(context.FileSystem.SessionGenerationPath), receipt.ToJsonString(),
                        ReadGuardianAttributionJson(tracked[EffectIdentityState.StatePath]!).ToJsonString());
                    AssertPressureNoPrivatePair(context, lease);
                    var active = ReadGuardianAttributionJson(signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath))["activeConflict"]!.AsObject();
                    Assert.Empty(active["exchangeLog"]!.AsArray());
                    Assert.Equal(("clear", "clear"), (active["playerSideStrain"]!.GetValue<string>(), active["oppositionSideStrain"]!.GetValue<string>()));
                    var balance = AfterlifeConflictActionPointProjectionService.Resolve(
                        SpiritualWoundStateJson.DecodeUtf8JsonText(signed.ReadRequiredBytes(ResourceMaterializationContract.DefinitionsPath)),
                        SpiritualWoundStateJson.DecodeUtf8JsonText(signed.ReadRequiredBytes(ResourceMaterializationContract.StatePath)), active);
                    RequireGuardianJourneyIssues(name, "player_signed_ap", balance.Issues);
                    Assert.Equal((3m, 10m, 3m, 9m), (balance.Projection!.Player.Current, balance.Projection.Player.Maximum,
                        balance.Projection.Opposition.Current, balance.Projection.Opposition.Maximum));
                    var copy = await AuthorPressureGroupOrdinaryAsync(context, lease, spec, signed);
                    // Keep byte identity deliberately different from canonical JSON, exercising every replay and rollback join.
                    var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + copy.Conflict.ToJsonString() + "\n")).ToArray();
                    await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, raw);
                    Assert.True(JsonNode.DeepEquals(copy.Conflict, ReadGuardianAttributionJson(raw)));
                    Assert.NotEqual(new CanonicalBeforeImage(true, raw).Fingerprint,
                        new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(copy.Conflict.ToJsonString())).Fingerprint);
                    var authored = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
                    foreach (var path in PressureAuthoredDraftPaths)
                        authored[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
                    Assert.Equal(raw, authored[AfterlifeSpiritualConflictState.StatePath]);
                    setup = new(name, copy, origin, data, authored, ExpectedRank: retainedRank, Role: PressureSingletonJourneyRole.Player, PlayerContour: contour);
                });
                await work(Assert.IsType<GuardianAttributionJourney>(setup));
            },
            HasWindow: () => false, HasCapture: () => false, HasLease: () => false, HasContext: () => context is not null,
            AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
            AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => { }, AssertNoPrivatePair: () => { },
            DisposeLease: () => ValueTask.CompletedTask,
            DisposeContext: async () =>
            {
                var started = Stopwatch.GetTimestamp();
                await context!.DisposeAsync();
                WritePressureOwnerTiming("singleton_iv_player_journey_owned_cleanup", name, started);
            },
            AssertRootRemoved: () => Assert.False(Directory.Exists(context!.RootPath))));
    }
}
