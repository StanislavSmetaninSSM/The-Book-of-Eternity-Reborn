using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AcceptedTurnStorageOutcomeTests
{
    [Theory]
    [InlineData("uncertain-carrier")]
    [InlineData("uncertain-later-mutations")]
    [InlineData("known-rollback")]
    public async Task ActualAcceptedNormalizationPublicationRetainsStorageDecision(string observation)
    {
        var unknownCut = observation != "known-rollback";
        var armed = false;
        var cutHits = 0;
        var beforeCutMutations = 0;
        var laterMutations = 0;
        byte[] unknown = [0xFF, 0x51];
        byte[]? retainedJournal = null;
        ResourceMaterializationTestContext? active = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = _ =>
            {
                if (armed)
                {
                    if (cutHits > 0) laterMutations++;
                    else beforeCutMutations++;
                }
                return Task.CompletedTask;
            },
            LocalPublicationObserver = (phase, index) =>
            {
                if (!armed || cutHits > 0 || phase != TrustedLocalPublicationPhase.MemberPublished) return;
                var files = active!.FileSystem;
                var journalPath = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
                using var document = JsonDocument.Parse(File.ReadAllBytes(journalPath));
                var member = document.RootElement.GetProperty("Members")[index];
                if (member.GetProperty("Path").GetString() != files.ResolvePath(ResourceMaterializationTestContext.HistoryPath)) return;
                Assert.True(member.GetProperty("After").GetProperty("Exists").GetBoolean());
                Assert.Equal(member.GetProperty("After").GetProperty("Bytes").GetBytesFromBase64(),
                    File.ReadAllBytes(files.ResolvePath(ResourceMaterializationTestContext.HistoryPath)));
                cutHits++;
                if (unknownCut)
                {
                    File.WriteAllBytes(files.ResolvePath(ResourceMaterializationTestContext.HistoryPath), unknown);
                    retainedJournal = File.ReadAllBytes(journalPath);
                }
                throw new AcceptedPublicationCutFailure();
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        active = context;
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.WriteExactJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionAndInitializationCommand().ToJsonString());
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var before = await context.CaptureAsync(ResourceMaterializationTestContext.AllResourcePaths);
        armed = true;
        var failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem, context.Normalizer, context.Validator,
            new Dictionary<string, string>(StringComparer.Ordinal)));
        armed = false;
        Assert.True(cutHits == 1, $"Accepted publication cut hits={cutHits}; failure={failure}");
        Assert.True(beforeCutMutations > 0);
        Assert.NotNull(failure);
        if (!unknownCut)
        {
            Assert.Contains("accepted storage publication fixture", failure.ToString(), StringComparison.Ordinal);
            await context.AssertUnchangedAsync(before);
            Assert.False(File.Exists(Path.Combine(context.FileSystem.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
            return;
        }
        Assert.Equal(unknown, File.ReadAllBytes(context.FileSystem.ResolvePath(ResourceMaterializationTestContext.HistoryPath)));
        Assert.Equal(retainedJournal, File.ReadAllBytes(Path.Combine(context.FileSystem.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        if (observation == "uncertain-carrier") Assert.IsType<CoordinatedStatePublicationUncertainException>(failure);
        else Assert.Equal(0, laterMutations);
    }

    [Theory]
    [InlineData("carrier")]
    [InlineData("later-mutations")]
    public async Task ActualAcceptedCompensationUncertaintyRetainsPrimaryFailure(string observation)
    {
        var armed = false;
        var forwardCutHits = 0;
        var restoreCutHits = 0;
        var laterMutations = 0;
        byte[]? originalDefinitions = null;
        byte[]? retainedJournal = null;
        byte[] unknown = [0xFF, 0x71];
        var originalFailure = new AcceptedPublicationCutFailure();
        ResourceMaterializationTestContext? active = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = _ =>
            {
                if (restoreCutHits > 0) laterMutations++;
                return Task.CompletedTask;
            },
            LocalPublicationObserver = (phase, index) =>
            {
                if (!armed || restoreCutHits > 0 || phase != TrustedLocalPublicationPhase.MemberPublished) return;
                var files = active!.FileSystem;
                var journalPath = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
                using var document = JsonDocument.Parse(File.ReadAllBytes(journalPath));
                var path = document.RootElement.GetProperty("Members")[index].GetProperty("Path").GetString();
                if (forwardCutHits == 0 && path == files.ResolvePath(ResourceMaterializationTestContext.HistoryPath))
                {
                    if (originalDefinitions!.SequenceEqual(File.ReadAllBytes(files.ResolvePath(ResourceMaterializationTestContext.DefinitionsPath)))) return;
                    forwardCutHits++;
                    throw originalFailure;
                }
                if (forwardCutHits != 1 || path != files.ResolvePath(ResourceMaterializationTestContext.DefinitionsPath)) return;
                Assert.Equal(originalDefinitions, File.ReadAllBytes(path));
                restoreCutHits++;
                File.WriteAllBytes(path, unknown);
                retainedJournal = File.ReadAllBytes(journalPath);
                throw new InvalidOperationException("accepted before-image restoration cut");
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        active = context;
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.WriteExactJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionAndInitializationCommand().ToJsonString());
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        originalDefinitions = File.ReadAllBytes(context.FileSystem.ResolvePath(ResourceMaterializationTestContext.DefinitionsPath));
        armed = true;
        var failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem, context.Normalizer, context.Validator,
            new Dictionary<string, string>(StringComparer.Ordinal)));
        armed = false;
        Assert.True(forwardCutHits == 1 && restoreCutHits == 1,
            $"Forward cut={forwardCutHits}; restore cut={restoreCutHits}; failure={failure}");
        Assert.Equal(unknown, File.ReadAllBytes(context.FileSystem.ResolvePath(ResourceMaterializationTestContext.DefinitionsPath)));
        Assert.Equal(retainedJournal, File.ReadAllBytes(Path.Combine(context.FileSystem.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        if (observation == "carrier")
        {
            var uncertain = Assert.IsType<CoordinatedStatePublicationUncertainException>(failure);
            var primary = Assert.IsType<CanonicalStateWriteException>(uncertain.Data["AcceptedTurnNormalizationFailure"]);
            Assert.Same(originalFailure, primary.InnerException);
        }
        else Assert.Equal(0, laterMutations);
    }

    private sealed class AcceptedPublicationCutFailure() : Exception("accepted storage publication fixture");
}
