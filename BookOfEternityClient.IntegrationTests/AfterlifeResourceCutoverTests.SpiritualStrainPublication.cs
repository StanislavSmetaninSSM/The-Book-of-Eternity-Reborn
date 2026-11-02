using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Publishes the actual completed III strain result through existing common C4 exactly once.</summary>
    [Fact]
    public async Task SpiritualStrainBurden_CompletedChoicePublishesThroughCurrentCommonOwner()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15, 4, 12]);
        await CompleteSpiritualStrainC2Async(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var expected = await PeekPlanAsync(context);
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority authority;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out authority));
            AssertNoConflictFrameErrors(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
        }
        await using (var foreign = await ResourceMaterializationTestContext.CreateAsync())
        await using (var lease = await foreign.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(foreign.FileSystem, lease, out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(foreign.FileSystem, lease, authority, out _));
        }
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.Same(expected, published.MechanicsPlan);
        Assert.NotNull(published.SpiritualConflictValidation);
        var output = Assert.IsType<SpiritualWoundPublishedOutput>(published.SpiritualWoundOutput);
        Assert.Single(output.Notifications);
        await output.RequireCurrentAsync(context.FileSystem);
        foreach (var pair in expected.WoundCarrierAfterImages.Concat(expected.EffectCarrierAfterImages)
                     .Concat(expected.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        foreach (var (path, value) in new[]
        {
            (ResourceMaterializationContract.StatePath, expected.StateAfterImage),
            (ResourceMaterializationContract.HistoryPath, expected.HistoryAfterImage),
            (WoundIdentityState.StatePath, expected.WoundIdentityAfterImage),
            (WoundHistoryState.HistoryPath, expected.WoundHistoryAfterImage),
            (EffectAcceptedTurnPlan.IdentityIndexPath, expected.EffectIdentityAfterImage)
        }) Assert.True(JsonNode.DeepEquals(value, await context.ReadJsonAsync(path)), path);
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var carriers = WoundCarrierCatalog.Build(new(null, null, null, null, profiles));
        AssertNoConflictFrameErrors(carriers.Issues);
        var wound = Assert.Single(carriers.Occurrences);
        Assert.Equal("player_soul", wound.Coordinate.OwnerKind);
        Assert.Equal("player_soul", wound.Coordinate.OwnerId);
        Assert.Equal(3, wound.Wound.Severity.Rank);
        var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!;
        Assert.Equal("broken", conflict["playerSideStrain"]!.GetValue<string>());
        var log = conflict["exchangeLog"]!.AsArray();
        Assert.Equal(2, log.Count);
        Assert.Equal("overwhelmed", log[1]!["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("broken", log[1]!["after"]!["playerSideStrain"]!.GetValue<string>());
        var receipts = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
        Assert.Equal(2, receipts["sources"]!.AsArray().Count);
        var decisions = receipts["decisions"]!.AsArray();
        Assert.Equal(2, decisions.Count);
        Assert.Equal("materialize", decisions[0]!["decision"]!.GetValue<string>());
        Assert.Equal(3, decisions[0]!["selectedSeverityRank"]!.GetValue<int>());
        Assert.Equal("none", decisions[1]!["decision"]!.GetValue<string>());
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
        {
            Assert.Contains(path, expected.ConsumedPaths);
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
        }
        var retainedBytes = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in expected.TouchedPaths.Concat(expected.ConsumedPaths).Distinct(StringComparer.Ordinal))
            retainedBytes[path] = await context.FileSystem.ReadFileBytesAsync(path);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, authority, out _));
        foreach (var (path, bytes) in retainedBytes)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(path));
    }

    /// <summary>Restores signed before-images after a committed strain publication fails on receipt read-back.</summary>
    [Fact]
    public async Task SpiritualStrainBurden_CommonPublicationReadbackFailureRestoresSignedWoundState()
    {
        var armed = false;
        var cuts = 0;
        CurrentCommittedPublicationWitness? witness = null;
        var injected = new IOException("Strain C4 receipt read-back failure.");
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) => { if (armed) witness!.Observe(phase, index); },
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && path == SpiritualWoundOpportunityReceiptState.StatePath &&
                    witness!.Has(ResourceMaterializationContract.StatePath) &&
                    witness.Has(SpiritualWoundOpportunityReceiptState.StatePath))
                {
                    witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
                    witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
                    cuts++;
                    armed = false;
                    throw injected;
                }
                return Task.CompletedTask;
            }
        };
        var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15, 4, 12]);
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), _storageOutput.WriteLine);
        var originals = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[]
        {
            SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
            SpiritualWoundOpportunityReceiptState.StatePath, ProjectionNarrativePath
        }).Distinct(StringComparer.Ordinal))
            originals[path] = await context.FileSystem.ReadFileBytesAsync(path);
        await CompleteSpiritualStrainC2Async(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var backups = await ReadSpiritualC4BackupsAsync(context);
        witness = new(context.FileSystem, ResourceMaterializationContract.StatePath,
            SpiritualWoundOpportunityReceiptState.StatePath);
        armed = true;
        var failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups));
        armed = false;
        witness.AssertRestored("spiritual-strain-common-publication-rollback",
            originals.ToDictionary(pair => context.FileSystem.ResolvePath(pair.Key), pair => pair.Value,
                StringComparer.Ordinal), failure, _storageOutput.WriteLine);
        Assert.Equal(1, cuts);
        Assert.Same(injected, failure);
        Assert.True(witness.Has(ResourceMaterializationContract.StatePath));
        Assert.True(witness.Has(SpiritualWoundOpportunityReceiptState.StatePath));
        foreach (var (path, bytes) in originals)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(path));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
    }
}
