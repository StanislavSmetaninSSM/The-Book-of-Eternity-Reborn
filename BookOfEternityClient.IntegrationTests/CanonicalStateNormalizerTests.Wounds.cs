using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class CanonicalStateNormalizerWoundTests
{
    public static TheoryData<string> RetainedPublicationAgreementPaths => new()
    {
        ProgressionScheduleService.SchedulePath,
        GuardianProjectState.JournalPath,
        "game_state/quests/regular_quests.json",
        InventoryEquipmentService.ItemsPath,
        "game_state/misc/characteristics.json",
        "output/narrative_response.json",
        WoundAcceptedTurnSnapshotContract.PendingResolutionPath
    };

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

    [Theory]
    [MemberData(nameof(RetainedPublicationAgreementPaths))]
    public async Task CompleteWoundPlan_PostPublicationRetainedRootDrift_RestoresWholeLocalTransaction(
        string protectedPath)
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
                var fullPath = Path.Combine(
                    sessionPath!,
                    protectedPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(
                    fullPath,
                    "{\"driftedAfterNormalization\":true}",
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
        var protectedBefore = await context.FileSystem.ReadFileBytesAsync(protectedPath);
        var manifest = JsonNode.Parse((await context.FileSystem.ReadFileAsync(
            "game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
        var backups = manifest["files"]!.AsObject().ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value!.GetValue<string>(),
            StringComparer.OrdinalIgnoreCase);

        armed = true;
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem,
                context.Normalizer,
                context.Validator,
                backups));

        Assert.True(driftInjected);
        Assert.Contains(protectedPath, exception.Message);
        var protectedAfter = await context.FileSystem.ReadFileBytesAsync(protectedPath);
        Assert.Equal(protectedBefore == null, protectedAfter == null);
        if (protectedBefore != null)
            Assert.Equal(protectedBefore, protectedAfter);
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
