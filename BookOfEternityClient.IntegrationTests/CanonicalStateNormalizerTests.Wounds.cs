using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class CanonicalStateNormalizerWoundTests
{
    [Fact]
    public async Task CompleteWoundPlan_PublishesIndexAndHistoryAndConsumesCommand()
    {
        var mutationPaths = new List<string>();
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                mutationPaths.Add(path);
                return Task.CompletedTask;
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        await SeedValidatedEmptyWoundCommandAsync(context);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        mutationPaths.Clear();
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);

        var published = Assert.IsType<AcceptedMechanicsPlan>(plan);
        Assert.NotNull(published.WoundStageBundle);
        Assert.Contains(WoundIdentityState.StatePath, mutationPaths);
        Assert.Contains(WoundHistoryState.HistoryPath, mutationPaths);
        Assert.Null(await context.ReadJsonAsync(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.True(JsonNode.DeepEquals(
            published.WoundIdentityAfterImage,
            await context.ReadJsonAsync(WoundIdentityState.StatePath)));
        Assert.True(JsonNode.DeepEquals(
            published.WoundHistoryAfterImage,
            await context.ReadJsonAsync(WoundHistoryState.HistoryPath)));
    }

    [Fact]
    public async Task CompleteWoundPlan_ReadBackRejectsIdentityDriftAfterPublication()
    {
        var armed = false;
        var woundIdentityReads = 0;
        string? sessionPath = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed ||
                    !string.Equals(
                        path,
                        WoundIdentityState.StatePath,
                        StringComparison.Ordinal))
                {
                    return Task.CompletedTask;
                }

                woundIdentityReads++;
                if (woundIdentityReads == 2)
                {
                    var fullPath = Path.Combine(
                        sessionPath!,
                        path.Replace('/', Path.DirectorySeparatorChar));
                    File.WriteAllText(
                        fullPath,
                        "{}",
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                }
                return Task.CompletedTask;
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        sessionPath = context.FileSystem.GameSessionPath;
        await SeedValidatedEmptyWoundCommandAsync(context);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        armed = true;
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null));

        Assert.Equal(2, woundIdentityReads);
        Assert.Contains(WoundIdentityState.StatePath, exception.Message);
    }

    [Fact]
    public async Task CompleteWoundPlan_LateSharedCarrierMutationFailsBeforePublication()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedValidatedEmptyWoundCommandAsync(context);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        await context.WriteExactJsonAsync(
            WoundCarrierCatalog.AfterlifeProfilesPath,
            new JsonObject().ToJsonString());

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null));

        Assert.Contains(WoundCarrierCatalog.AfterlifeProfilesPath, exception.Message);
    }

    private static async Task SeedValidatedEmptyWoundCommandAsync(
        ResourceMaterializationTestContext context)
    {
        await WoundMaterializationValidationTests.SeedEmptyFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths:
                WoundMaterializationValidationTests.SnapshotWoundPaths);
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            WoundMaterializationValidationTests.EmptyCommands(
                sessionId: "session_resource_materialization",
                requestId: "request_resource_materialization",
                snapshotToken: await WoundMaterializationValidationTests
                    .ReadSnapshotTokenAsync(context))
                .ToJsonString());
    }
}
