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

    [Fact]
    public async Task CompleteWoundPlan_PostPublicationOutputDrift_RestoresWholeLocalTransaction()
    {
        const string outputPath = "output/narrative_response.json";
        const string acceptedOutput = "{\"response\":\"Sealed wound narration.\"}";
        var armed = false;
        var driftInjected = false;
        string? sessionPath = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (!armed ||
                    driftInjected ||
                    !string.Equals(
                        path,
                        ResourceMaterializationContract.DefinitionsPath,
                        StringComparison.Ordinal))
                {
                    return Task.CompletedTask;
                }

                driftInjected = true;
                File.WriteAllText(
                    Path.Combine(
                        sessionPath!,
                        outputPath.Replace('/', Path.DirectorySeparatorChar)),
                    "{\"response\":\"Drifted after preflight.\"}",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return Task.CompletedTask;
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        sessionPath = context.FileSystem.GameSessionPath;
        await context.FileSystem.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
        await SeedValidatedEmptyWoundCommandAsync(context);
        await context.FileSystem.WriteFileAtomicAsync(outputPath, acceptedOutput);
        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(
            itemIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var manifest = JsonNode.Parse((await context.FileSystem.ReadFileAsync(
            "game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
        var backups = manifest["files"]!.AsObject().ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value!.GetValue<string>(),
            StringComparer.OrdinalIgnoreCase);

        armed = true;
        var result = await AcceptedTurnCanonicalStateRefresh
            .NormalizeAndValidateWithPlanAsync(
                context.FileSystem,
                context.Normalizer,
                context.Validator,
                backups);

        Assert.True(driftInjected);
        Assert.Null(result.MechanicsPlan);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code ==
                "wound_materialization_output_authority_mismatch");
        Assert.Equal(
            acceptedOutput,
            await context.FileSystem.ReadFileAsync(outputPath));
        Assert.NotNull(await context.ReadJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath));
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
