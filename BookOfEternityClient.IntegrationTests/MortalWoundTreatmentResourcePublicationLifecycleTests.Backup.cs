using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackupUncertaintyRetainsRealTreatmentResourceAndItemClaims(bool committedDebt)
    {
        var armed = false; var reached = 0; var laterBoundaries = 0;
        FileSystemManager? files = null; string? unknownPath = null; byte[]? recognizedBytes = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = _ => { if (reached > 0) laterBoundaries++; return Task.CompletedTask; },
            LocalPublicationObserver = (phase, _) =>
            {
                if (!armed || reached > 0 || phase != (committedDebt ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.MemberPublished)) return;
                using var journal = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
                var members = journal.RootElement.GetProperty("Members");
                if (members.GetArrayLength() != 1) return;
                var member = members[0]; var path = member.GetProperty("Path").GetString()!;
                if (!path.Contains(".backup.", StringComparison.Ordinal)) return;
                recognizedBytes = member.GetProperty("After").GetProperty("Bytes").GetBytesFromBase64();
                unknownPath = path; reached++; File.WriteAllBytes(path, [99]);
                throw new InvalidOperationException("Nontransient actual treatment backup publication cut.");
            }
        };
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null, itemScenario: HeldTreatmentItemScenario.SelectedStack(), hooks: hooks);
        files = context.FileSystem;
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        var paths = new[] { ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
            AcceptedMechanicsPlan.WoundCommandPath, "game_state/npcs/items.json" };
        var before = paths.ToDictionary(path => path, path => File.Exists(files.ResolvePath(path)) ? File.ReadAllBytes(files.ResolvePath(path)) : null);
        var root = WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(
            context.AcceptedState.Binding, context.Resolution, HeldTreatmentPipelineContext.FinalSceneText);
        var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(root));
        Assert.True(parsed.Success, DescribeValidationIssues(parsed.Issues));
        var composition = WoundResponseInputComposer.RecomposeCommandRoot(context.AcceptedState.Binding,
            parsed, Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composition.Success, DescribeValidationIssues(composition.Issues));
        var response = context.CreateMechanicsOnlyTreatmentProposal(); response.Response = HeldTreatmentPipelineContext.FinalSceneText;
        await context.ReleaseLeaseAsync(); armed = true;
        await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() =>
            new StateDistributor(files, NullLogger<StateDistributor>.Instance).DistributeAsync(response, composition));
        Assert.Equal(1, reached); Assert.Equal(0, laterBoundaries);
        foreach (var (path, bytes) in before)
        {
            Assert.Equal(bytes != null, File.Exists(files.ResolvePath(path)));
            if (bytes != null) Assert.Equal(bytes, File.ReadAllBytes(files.ResolvePath(path)));
        }
        var journalPath = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        var evidence = File.ReadAllBytes(journalPath);
        await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await files.AcquireCanonicalWriteLeaseAsync(); });
        Assert.Equal(evidence, File.ReadAllBytes(journalPath));
        Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(unknownPath!));
        // Restore this test's injected unknown bytes to the recorded after image,
        // then use normal acquisition/recovery to inspect the retained live claims.
        armed = false; File.WriteAllBytes(unknownPath!, recognizedBytes!);
        await context.AcquireLeaseAsync();
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
    }
}
