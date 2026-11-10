using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class CanonicalStateNormalizerResourceTests(ITestOutputHelper output)
{
    /// <summary>
    /// Verifies both leased authority wrappers forward fresh allocation scopes to their real planners.
    /// </summary>
    /// <param name="woundStage">
    /// <see langword="true"/> selects a prepared wound; <see langword="false"/> selects ordinary effects.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpiritualScope_LeasedAuthorityWrappersReplayAllocations(bool woundStage)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        WoundPreparedAcceptedTurnPlan? prepared = null;
        if (woundStage)
        {
            var preparation = WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
                context.FileSystem, lease, WoundAcceptedTurnTestFixture.CreateDefaultInput());
            Assert.True(preparation.Success, string.Join(Environment.NewLine, preparation.Issues));
            prepared = preparation.Plan!;
        }
        var input = prepared is null ? CreateIndependentEffectInput()
            : WoundAcceptedTurnTestFixture.CreateEffectInput(prepared);
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var first = Build(new SpiritualWoundEffectIdentityFactory(journal, new EffectIdentityFactory()));
        var rows = journal.Export().ToJsonString();
        Assert.NotEqual("[]", rows);
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        var second = Build(new SpiritualWoundEffectIdentityFactory(replay, new EffectIdentityFactory()));
        Assert.Equal(rows, replay.Export().ToJsonString());
        Assert.Equal(first.InputFingerprint, second.InputFingerprint);
        Assert.Equal(first.IdentityIndexAfterImage.ToJsonString(), second.IdentityIndexAfterImage.ToJsonString());
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));

        EffectAcceptedTurnPlan Build(EffectIdentityFactory factory)
        {
            if (prepared is null)
            {
                var result = EffectAcceptedTurnPlanAuthority.GetOrBuildValidated(context.FileSystem, lease, input, factory);
                Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues));
                return result.Plan!;
            }
            var wound = WoundAcceptedTurnPlanAuthority.GetOrBuildEffectValidated(context.FileSystem, lease, prepared, input, factory);
            Assert.True(wound.Success, string.Join(Environment.NewLine, wound.Issues));
            Assert.True(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, lease, out var retained));
            return retained.Plan!;
        }
    }

    [Fact]
    public void ResourceAuthorityPathsAreTrackedForSnapshotPublicationAndRollback()
    {
        foreach (var path in ResourceMaterializationTestContext.AllResourcePaths)
        {
            if (!string.Equals(
                    path,
                    ResourceMaterializationTestContext.CommandsPath,
                    StringComparison.Ordinal))
            {
                Assert.Contains(path, CanonicalStateNormalizer.CanonicalAccumulatedFiles);
                Assert.Contains(path, CanonicalStateNormalizer.NormalizerBackupInputFiles);
            }
            Assert.Contains(path, CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        }
    }

    [Fact]
    public async Task DefinitionCreation_PublishesCanonicalCatalogAndConsumesCommand()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);

        Assert.NotNull(plan);
        var definitions = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath));
        var definition = Assert.IsType<JsonObject>(
            Assert.Single(definitions["definitions"]!.AsArray()));
        Assert.Equal("mana", definition["resourceKey"]!.GetValue<string>());
        Assert.NotNull(definition["materialization"]);
        Assert.Null(await context.ReadJsonAsync(ResourceMaterializationTestContext.CommandsPath));
    }

    [Theory]
    [InlineData(ResourceMaterializationTestContext.DefinitionsPath)]
    [InlineData(ResourceMaterializationTestContext.StatePath)]
    [InlineData(ResourceMaterializationTestContext.HistoryPath)]
    [InlineData(ResourceMaterializationTestContext.CommandsPath)]
    public async Task DefinitionCreation_LateLiteralNullAuthorityFailsBeforeEveryWrite(
        string changedPath)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await context.WriteExactJsonAsync(changedPath, "null");
        var before = await context.CaptureAsync(ResourceMaterializationTestContext.AllResourcePaths);
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        await Assert.ThrowsAsync<InvalidDataException>(() => context.Normalizer
            .BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null));

        await context.AssertUnchangedAsync(before);
    }

    /// <summary>
    /// Rejects each changed current authority before publishing an ordinary skill-sourced effect.
    /// </summary>
    /// <param name="authorityKind">
    /// Named publication authority whose validated baseline is changed.
    /// </param>
    /// <param name="changedPath">
    /// Exact current canonical path corresponding to that authority.
    /// </param>
    /// <returns>
    /// A task completing after rejection, byte preservation and common-handoff invalidation are confirmed.
    /// </returns>
    [Theory]
    [InlineData("definition", ResourceMaterializationContract.DefinitionsPath)]
    [InlineData("state", ResourceMaterializationContract.StatePath)]
    [InlineData("history", ResourceMaterializationContract.HistoryPath)]
    [InlineData("source", EffectMaterializationTestContext.MaterializableSkillPath)]
    [InlineData("owner", "game_state/npcs/npc_core.json")]
    [InlineData("target", "game_state/npcs/npc_core.json")]
    [InlineData("carrier", EffectMaterializationTestContext.PlayerEffectsPath)]
    [InlineData("index", EffectMaterializationTestContext.IdentityIndexPath)]
    [InlineData("event", "input/turn_request.json")]
    [InlineData("command", EffectMaterializationTestContext.CommandPath)]
    [InlineData("pending", ResourcePendingResolutionState.PendingPath)]
    [InlineData("internal_adapter", "game_state/control/pending_turn_snapshot.json")]
    public async Task CommonPlan_LateAuthorityMutationFailsBeforeEveryWrite(
        string authorityKind,
        string changedPath)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedEmptyMortalItemIdentityAsync(context.FileSystem);
        await context.SeedPlayerSkillSourceAsync();
        var usesNpcAuthority = authorityKind is "owner" or "target";
        if (usesNpcAuthority)
            await MaterializeNpcHealthAsync(context, "npc_resource_toctou_target", 60m);
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var command = EffectMaterializationTestContext.CreateSkillApplyCommand();
        if (usesNpcAuthority)
        {
            command["target"] = new JsonObject
            {
                ["kind"] = "npc",
                ["targetId"] = "npc_resource_toctou_target"
            };
        }
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(
            itemIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(
                Environment.NewLine,
                issues.Select(issue =>
                    $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        var validatedHandoff = await AcceptedMechanicsAuthorityTestProbe
            .PeekCommonAsync(context.FileSystem);
        Assert.NotNull(validatedHandoff);
        var planning = validatedHandoff.Result;
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planning.Plan);

        await ApplyLateMutationAsync(context, changedPath, authorityKind);
        var protectedPaths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(plan.BeforeImages.Keys)
            .Append(changedPath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var before = await context.CaptureBytesAsync(protectedPaths);
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        await Assert.ThrowsAsync<InvalidDataException>(() => context.Normalizer
            .BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups));

        Assert.Equal(before, await context.CaptureBytesAsync(protectedPaths));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            writeLease));
    }

    /// <summary>
    /// Rejects an authenticated snapshot swap at the publication authority gate before any write.
    /// </summary>
    /// <returns>
    /// A task completing after the swap is rejected, resources remain unchanged and the handoff is invalidated.
    /// </returns>
    [Fact]
    public async Task CommonPlan_SnapshotAuthoritySwapAfterPreflight_FailsBeforeEveryPublicationWrite()
    {
        const string snapshotManifestPath =
            "game_state/control/pending_turn_snapshot.json";
        FileSystemManager? fileSystem = null;
        byte[]? replacementManifestBytes = null;
        byte[]? replacementAuthorityBytes = null;
        var armed = false;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed ||
                    swapped ||
                    !string.Equals(
                        path,
                        "input/turn_request.json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                File.WriteAllBytes(
                    fileSystem!.ResolvePath(snapshotManifestPath),
                    replacementManifestBytes!);
                File.WriteAllBytes(
                    fileSystem.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath),
                    replacementAuthorityBytes!);
                swapped = true;
                return Task.CompletedTask;
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        fileSystem = context.FileSystem;
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();

        var manifestA = Assert.IsType<JsonObject>(
            await context.ReadJsonAsync(snapshotManifestPath));
        var manifestABytes = (await fileSystem.ReadFileBytesAsync(snapshotManifestPath))!;
        var authorityABytes = (await fileSystem.ReadFileBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath))!;
        var manifestB = manifestA.DeepClone().AsObject();
        manifestB["sourceLabel"] = "Accepted publisher swapped snapshot B";
        manifestB["manifestPayloadHash"] = string.Empty;
        manifestB["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifestB);
        await context.WriteExactJsonAsync(snapshotManifestPath, manifestB.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fileSystem);
        replacementManifestBytes = (await fileSystem.ReadFileBytesAsync(snapshotManifestPath))!;
        replacementAuthorityBytes = (await fileSystem.ReadFileBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath))!;
        await fileSystem.WriteFileAtomicBytesAsync(snapshotManifestPath, manifestABytes);
        await fileSystem.WriteFileAtomicBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
            authorityABytes);

        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var before = await context.CaptureAsync(
            ResourceMaterializationTestContext.AllResourcePaths);
        armed = true;
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => context.Normalizer
            .BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null));

        Assert.True(swapped);
        Assert.Contains(
            "publication authority changed after preflight",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        await context.AssertUnchangedAsync(before);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fileSystem,
            writeLease));
    }

    [Fact]
    public async Task CommonPlan_IndependentEffectCacheWithoutCommonPlan_FailsPreflight()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await using (var publicationLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            SeedIndependentValidatedEffectCache(
                context.FileSystem,
                publicationLease,
                CreateIndependentEffectInput());
        }
        var before = await context.CaptureBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Normalizer
            .BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null));

        Assert.Equal(
            before,
            await context.CaptureBytesAsync(
                CanonicalStateNormalizer.NormalizerRollbackTrackedFiles));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            writeLease));
        Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
            context.FileSystem,
            writeLease,
            out _));
    }

    [Fact]
    public async Task CommonPlan_EffectCacheAppearsAfterNoPlanPreflight_FailsBeforeEveryWrite()
    {
        EffectMaterializationTestContext? hookedContext = null;
        var effectInput = CreateIndependentEffectInput();
        FileSystemManager.CanonicalWriteLease? activeWriteLease = null;
        var armed = false;
        var appeared = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed ||
                    appeared ||
                    !string.Equals(
                        path,
                        "input/turn_request.json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                appeared = true;
                SeedIndependentValidatedEffectCache(
                    hookedContext!.FileSystem,
                    activeWriteLease!,
                    effectInput);
                return Task.CompletedTask;
            }
        };
        await using var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        hookedContext = context;
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap());
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject
            {
                ["currentLocationData"] = MortalLocationTestFixture.CreateRawLocation(
                    "current_scene_creation")
            });
        var before = await context.CaptureBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(
            context.FileSystem));
        armed = true;

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        activeWriteLease = writeLease;
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Normalizer
            .BindTo(writeLease)
            .NormalizeAccumulatedStateWithPlanAsync(backups));

        Assert.True(appeared);
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(
                CanonicalStateNormalizer.NormalizerRollbackTrackedFiles));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            writeLease));
        Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
            context.FileSystem,
            writeLease,
            out _));
    }

    [Fact]
    public async Task CommonPlan_EffectCachePublicationAtCanonicalMutationBoundary_WaitsForCanonicalContour()
    {
        var effectInput = CreateIndependentEffectInput();
        var mainContentions = 0;
        var canonicalContentions = 0;
        var armed = false;
        var paused = 0;
        var boundaryReached = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBoundary = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var publicationContended = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = async _ =>
            {
                if (!armed || Interlocked.Exchange(ref paused, 1) != 0)
                    return;

                boundaryReached.TrySetResult(true);
                await releaseBoundary.Task;
            },
            MainOwnerLockContendedAsync = () =>
            {
                Interlocked.Increment(ref mainContentions);
                publicationContended.TrySetResult(true);
                return Task.CompletedTask;
            },
            CanonicalWriteLockContendedAsync = () =>
            {
                Interlocked.Increment(ref canonicalContentions);
                return Task.CompletedTask;
            }
        };
        await using var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap());
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject
            {
                ["currentLocationData"] = MortalLocationTestFixture.CreateRawLocation(
                    "current_scene_creation")
            });
        armed = true;

        var normalizationTask = Task.Run(async () =>
        {
            await using var normalizationLease =
                await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            return await context.Normalizer
                .BindTo(normalizationLease)
                .NormalizeAccumulatedStateWithPlanAsync(backups);
        });
        Task<FileSystemManager.CanonicalWriteLease>? publicationLeaseTask = null;
        var publicationLeaseReleased = false;
        try
        {
            await Task.WhenAny(normalizationTask, boundaryReached.Task).WaitAsync(TimeSpan.FromSeconds(30));
            if (!boundaryReached.Task.IsCompleted) await normalizationTask;
            Assert.True(boundaryReached.Task.IsCompletedSuccessfully);
            publicationLeaseTask = Task.Run(() => context.FileSystem.AcquireCanonicalWriteLeaseAsync());
            await Task.WhenAny(publicationLeaseTask, publicationContended.Task).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(publicationContended.Task.IsCompletedSuccessfully);
            Assert.False(publicationLeaseTask.IsCompleted);
            Assert.True(mainContentions > 0);
            Assert.Equal(0, canonicalContentions);

            releaseBoundary.TrySetResult(true);
            Assert.Null(await normalizationTask.WaitAsync(TimeSpan.FromSeconds(30)));
            var publicationLease = await publicationLeaseTask.WaitAsync(TimeSpan.FromSeconds(30));
            SeedIndependentValidatedEffectCache(context.FileSystem, publicationLease, effectInput);
            Assert.True(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, publicationLease, out _));
            EffectAcceptedTurnPlanAuthority.InvalidateValidated(context.FileSystem, publicationLease);
        }
        finally
        {
            releaseBoundary.TrySetResult(true);
            await Record.ExceptionAsync(() => Task.WhenAll(normalizationTask, publicationLeaseTask ?? Task.CompletedTask));
            if (publicationLeaseTask?.IsCompletedSuccessfully == true)
            {
                await publicationLeaseTask.Result.DisposeAsync();
                publicationLeaseReleased = true;
            }
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                kind = "f18-admission-contention", method = nameof(CommonPlan_EffectCachePublicationAtCanonicalMutationBoundary_WaitsForCanonicalContour), root = context.RootPath,
                paused, mainContentions, canonicalContentions, firstSettled = normalizationTask.IsCompleted,
                secondSettled = publicationLeaseTask?.IsCompleted, publicationLeaseReleased
            }));
        }
    }

    [Fact]
    public async Task CommonPlan_PublicValidationPublishesBeforeWaitingNormalizerContinues()
    {
        var mainContentions = 0;
        var canonicalContentions = 0;
        var armed = false;
        var paused = 0;
        var validationReadEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseValidation = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var normalizationContended = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = async path =>
            {
                if (!armed ||
                    !string.Equals(
                        path,
                        ResourceMaterializationContract.CommandPath,
                        StringComparison.OrdinalIgnoreCase) ||
                    Interlocked.Exchange(ref paused, 1) != 0)
                {
                    return;
                }

                validationReadEntered.TrySetResult(true);
                await releaseValidation.Task;
            },
            MainOwnerLockContendedAsync = () =>
            {
                Interlocked.Increment(ref mainContentions);
                normalizationContended.TrySetResult(true);
                return Task.CompletedTask;
            },
            CanonicalWriteLockContendedAsync = () =>
            {
                Interlocked.Increment(ref canonicalContentions);
                return Task.CompletedTask;
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());
        armed = true;

        var validationTask = context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Task<AcceptedMechanicsPlan?>? normalizationTask = null;
        try
        {
            await Task.WhenAny(validationTask, validationReadEntered.Task).WaitAsync(TimeSpan.FromSeconds(30));
            if (!validationReadEntered.Task.IsCompleted) await validationTask;
            Assert.True(validationReadEntered.Task.IsCompletedSuccessfully);
            normalizationTask = context.Normalizer
                .NormalizeAcceptedMechanicsAsync(backups: null);
            await Task.WhenAny(normalizationTask, normalizationContended.Task).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(normalizationContended.Task.IsCompletedSuccessfully);
            Assert.True(mainContentions > 0);
            Assert.Equal(0, canonicalContentions);
            Assert.False(normalizationTask.IsCompleted);

            releaseValidation.TrySetResult(true);
            var issues = await validationTask.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
            Assert.NotNull(normalizationTask);
            var plan = await normalizationTask.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.NotNull(plan);
            Assert.Null(await context.ReadJsonAsync(ResourceMaterializationContract.CommandPath));
            Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
                context.FileSystem));
            Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(
                context.FileSystem));
        }
        finally
        {
            releaseValidation.TrySetResult(true);
            await Record.ExceptionAsync(() => Task.WhenAll(validationTask, normalizationTask ?? Task.CompletedTask));
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                kind = "f18-admission-contention", method = nameof(CommonPlan_PublicValidationPublishesBeforeWaitingNormalizerContinues), root = context.RootPath,
                paused, mainContentions, canonicalContentions, firstSettled = validationTask.IsCompleted,
                secondSettled = normalizationTask?.IsCompleted
            }));
        }
    }

    [Fact]
    public async Task CommonPlan_PublicValidationPublishesForNormalizerUsingSameCanonicalRoot()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        var secondFileSystem = new FileSystemManager(
            context.RootPath,
            NullLogger<FileSystemManager>.Instance);
        var secondNormalizer = new CanonicalStateNormalizer(
            secondFileSystem,
            NullLogger<CanonicalStateNormalizer>.Instance);
        await using var writeLease =
            await secondFileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await secondNormalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);

        Assert.NotNull(plan);
        Assert.False(secondFileSystem.FileExists(ResourceMaterializationContract.CommandPath));
    }

    [Fact]
    public async Task CommonPlan_PublicationSurvivesPublisherCollectionWhileConsumerRootLives()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());

        var (publisherReference, expectedPlan) = PublishCommonPlanFromEphemeralManager(
            context.RootPath);
        ForceFullCollection(publisherReference);
        Assert.False(publisherReference.TryGetTarget(out _));

        var published = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            context.FileSystem);
        Assert.NotNull(published);
        Assert.Same(expectedPlan, published!.Result.Plan);

        await using var writeLease =
            await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var normalizedPlan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);

        Assert.Same(expectedPlan, normalizedPlan);
        Assert.False(context.FileSystem.FileExists(
            ResourceMaterializationContract.CommandPath));
        GC.KeepAlive(context.FileSystem);
    }

    [Fact]
    public async Task CommonPlan_SessionGenerationRotationDropsAllRootScopedHandoffs()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));

        await using (var effectLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            SeedIndependentValidatedEffectCache(
                context.FileSystem,
                effectLease,
                CreateIndependentEffectInput());
        }
        var emptyItemCatalog = MortalItemCarrierCatalog.Build(
            new MortalItemCarrierCatalogInput(
                PlayerInventory: null,
                NpcCore: null,
                NpcInventoryCommands: null,
                CurrentLocation: null,
                Vehicles: null,
                CompanionRoots:
                    new Dictionary<string, JsonObject>(StringComparer.Ordinal)));
        Assert.Empty(emptyItemCatalog.Issues);
        await AcceptedMechanicsAuthorityTestProbe.RegisterItemsAsync(
            context.FileSystem,
            "session_root_authority_rotation",
            "snapshot_root_authority_rotation",
            emptyItemCatalog,
            Array.Empty<string>());
        Assert.NotNull(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(
            context.FileSystem));
        Assert.True(await AcceptedMechanicsAuthorityTestProbe.HasItemsAsync(
            context.FileSystem));

        _ = await SessionReplacementTestHarness.RotateGenerationAsync(
            context.FileSystem);
        var secondFileSystem = new FileSystemManager(
            context.RootPath,
            NullLogger<FileSystemManager>.Instance);

        Assert.Same(
            context.FileSystem.CanonicalRootAuthorityIdentity,
            secondFileSystem.CanonicalRootAuthorityIdentity);
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            secondFileSystem));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(
            secondFileSystem));
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasItemsAsync(
            secondFileSystem));
    }

    [Fact]
    public async Task CommonPlan_FailedLoadGenerationAbaDropsPublishedHandoffs()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));

        string initialGeneration;
        await using (var readLease =
                     await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            initialGeneration = context.FileSystem.GetOrCreateSessionGeneration(
                readLease);
        }

        await using (var lifecycleLease =
                     await context.FileSystem.AcquireSessionLifecycleLeaseAsync())
        await using (var replacementLease =
                     await context.FileSystem.AcquireSessionReplacementWriteLeaseAsync(
                         lifecycleLease))
        {
            var transactionId = Guid.NewGuid().ToString("N");
            var replacementGeneration = context.FileSystem.BeginLoadTransaction(
                replacementLease,
                transactionId);
            context.FileSystem.ActivateLoadTransactionSession(
                replacementLease,
                transactionId);
            Assert.True(context.FileSystem.IsCurrentSessionGeneration(
                replacementLease,
                replacementGeneration));

            context.FileSystem.RecoverInterruptedLoadTransaction(replacementLease);
            Assert.True(context.FileSystem.IsCurrentSessionGeneration(
                replacementLease,
                initialGeneration));
        }

        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
    }

    [Fact]
    public async Task CommonPlan_DifferentCanonicalRootCannotObservePublishedHandoff()
    {
        await using var publisher = await ResourceMaterializationTestContext.CreateAsync();
        await using var isolatedConsumer = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(publisher);
        await publisher.CaptureValidatedPendingSnapshotAsync();
        await publisher.WriteExactJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());

        var issues = await publisher.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.NotSame(
            publisher.FileSystem.CanonicalRootAuthorityIdentity,
            isolatedConsumer.FileSystem.CanonicalRootAuthorityIdentity);
        Assert.True(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            publisher.FileSystem));
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            isolatedConsumer.FileSystem));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(
            isolatedConsumer.FileSystem));
    }

    [Fact]
    public async Task AcceptedTurnAuthority_CaseVariantRootsRemainIsolatedOnCaseSensitiveFileSystem()
    {
        if (OperatingSystem.IsWindows())
            return;

        var parentRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-authority-root-case-" + Guid.NewGuid().ToString("N"));
        var firstRoot = Path.Combine(parentRoot, "AuthorityRoot");
        var secondRoot = Path.Combine(parentRoot, "authorityroot");
        Directory.CreateDirectory(firstRoot);
        try
        {
            if (Directory.Exists(secondRoot))
                return;

            Directory.CreateDirectory(secondRoot);
            var firstFileSystem = new FileSystemManager(
                firstRoot,
                NullLogger<FileSystemManager>.Instance);
            var secondFileSystem = new FileSystemManager(
                secondRoot,
                NullLogger<FileSystemManager>.Instance);
            firstFileSystem.EnsureDirectoryStructure();
            secondFileSystem.EnsureDirectoryStructure();

            string sharedGeneration;
            await using (var generationLease =
                         await firstFileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                sharedGeneration = firstFileSystem.GetOrCreateSessionGeneration(
                    generationLease);
            }
            Directory.CreateDirectory(
                Path.GetDirectoryName(secondFileSystem.SessionGenerationPath)!);
            await File.WriteAllTextAsync(
                secondFileSystem.SessionGenerationPath,
                $$"""{"SchemaVersion":1,"GenerationId":"{{sharedGeneration}}"}""");

            await using (var publicationLease =
                         await firstFileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                SeedIndependentValidatedEffectCache(
                    firstFileSystem,
                    publicationLease,
                    CreateIndependentEffectInput());
            }

            Assert.NotSame(
                firstFileSystem.CanonicalRootAuthorityIdentity,
                secondFileSystem.CanonicalRootAuthorityIdentity);
            Assert.NotNull(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(
                firstFileSystem));
            Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(
                secondFileSystem));
        }
        finally
        {
            if (Directory.Exists(parentRoot))
                Directory.Delete(parentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CommonPlan_PublicValidationWaitsForNormalizerAndRevalidatesFreshState()
    {
        var mainContentions = 0;
        var canonicalContentions = 0;
        var armed = false;
        var paused = 0;
        var normalizationMutationEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseNormalization = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var validationContended = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = async _ =>
            {
                if (!armed || Interlocked.Exchange(ref paused, 1) != 0)
                    return;

                normalizationMutationEntered.TrySetResult(true);
                await releaseNormalization.Task;
            },
            MainOwnerLockContendedAsync = () =>
            {
                Interlocked.Increment(ref mainContentions);
                validationContended.TrySetResult(true);
                return Task.CompletedTask;
            },
            CanonicalWriteLockContendedAsync = () =>
            {
                Interlocked.Increment(ref canonicalContentions);
                return Task.CompletedTask;
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());
        var initialIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(
            initialIssues,
            issue => issue.Severity == IssueSeverity.Error);
        armed = true;

        var normalizationTask = context.Normalizer
            .NormalizeAcceptedMechanicsAsync(backups: null);
        Task<IReadOnlyList<ValidationIssue>>? validationTask = null;
        try
        {
            await Task.WhenAny(normalizationTask, normalizationMutationEntered.Task).WaitAsync(TimeSpan.FromSeconds(30));
            if (!normalizationMutationEntered.Task.IsCompleted) await normalizationTask;
            Assert.True(normalizationMutationEntered.Task.IsCompletedSuccessfully);
            validationTask = context.Validator
                .ValidateAcceptedTurnRawResourceMaterializationAsync();
            await Task.WhenAny(validationTask, validationContended.Task).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(validationContended.Task.IsCompletedSuccessfully);
            Assert.True(mainContentions > 0);
            Assert.Equal(0, canonicalContentions);
            Assert.False(validationTask.IsCompleted);

            releaseNormalization.TrySetResult(true);
            var plan = await normalizationTask.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.NotNull(validationTask);
            _ = await validationTask.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.NotNull(plan);
            Assert.Null(await context.ReadJsonAsync(ResourceMaterializationContract.CommandPath));
            Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
                context.FileSystem));
            Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(
                context.FileSystem));
        }
        finally
        {
            releaseNormalization.TrySetResult(true);
            await Record.ExceptionAsync(() => Task.WhenAll(normalizationTask, validationTask ?? Task.CompletedTask));
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                kind = "f18-admission-contention", method = nameof(CommonPlan_PublicValidationWaitsForNormalizerAndRevalidatesFreshState), root = context.RootPath,
                paused, mainContentions, canonicalContentions, firstSettled = normalizationTask.IsCompleted,
                secondSettled = validationTask?.IsCompleted
            }));
        }
    }

    [Fact]
    public async Task SameTurnDefinitionInitialization_PublishesExactStateAndHistoryAtomically()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionAndInitializationCommand()
                .ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);

        Assert.NotNull(plan);
        var state = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ResourceMaterializationTestContext.StatePath));
        var stateEntry = Assert.IsType<JsonObject>(Assert.Single(state["entries"]!.AsArray()));
        Assert.Equal("mana", stateEntry["resourceKey"]!.GetValue<string>());
        Assert.Equal("player_current", stateEntry["resourceOwnerId"]!.GetValue<string>());
        Assert.Equal(40m, stateEntry["current"]!.GetValue<decimal>());
        Assert.Equal(40m, stateEntry["maximum"]!.GetValue<decimal>());

        var history = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ResourceMaterializationTestContext.HistoryPath));
        var transition = Assert.IsType<JsonObject>(
            Assert.Single(history["entries"]!.AsArray()));
        Assert.Equal("initialize", transition["operation"]!.GetValue<string>());
        Assert.Equal("turn_42:resource:2", transition["eventRef"]!.GetValue<string>());
        Assert.Equal("initialize_from_definition",
            transition["capacityDisposition"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(ResourceMaterializationTestContext.CommandsPath));
    }

    [Fact]
    public async Task MidPublicationFailure_RestoresEveryResourceByteAndCommand()
    {
        var armed = false;
        var injected = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (armed &&
                    !injected &&
                    string.Equals(
                        path,
                        ResourceMaterializationContract.HistoryPath,
                        StringComparison.Ordinal))
                {
                    injected = true;
                    throw new IOException("Injected resource history publication failure.");
                }
                return Task.CompletedTask;
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await SeedEmptyMortalItemIdentityAsync(context.FileSystem);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionAndInitializationCommand()
                .ToJsonString());
        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(
            itemIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var before = await context.CaptureAsync(
            ResourceMaterializationTestContext.AllResourcePaths);

        armed = true;
        await Assert.ThrowsAsync<CanonicalStateWriteException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
                context.FileSystem,
                context.Normalizer,
                context.Validator,
                new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.True(injected);
        await context.AssertUnchangedAsync(before);
    }

    /// <summary>
    /// Publishes one ordinary skill-sourced effect through the common accepted-mechanics plan.
    /// </summary>
    /// <returns>
    /// A task completing after one effect is published and its command and common handoff are consumed.
    /// </returns>
    [Fact]
    public async Task EffectOnlyTurn_PublishesThroughOneAcceptedMechanicsPlan()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedEmptyMortalItemIdentityAsync(context.FileSystem);
        await context.SeedPlayerSkillSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestContext.CreateSkillApplyCommand()));

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(
            itemIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);

        Assert.NotNull(plan);
        Assert.NotNull(plan.EffectPlan);
        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Single(player["activeEffects"]!.AsArray());
        Assert.Null(await context.ReadJsonAsync(
            EffectMaterializationTestContext.CommandPath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            writeLease));
    }

    /// <summary>
    /// Invalidates the common handoff when its registered skill definition changes before publication.
    /// </summary>
    /// <returns>
    /// A task completing after publication rejection and common-handoff invalidation are confirmed.
    /// </returns>
    [Fact]
    public async Task EffectOnlyTurn_EffectPreflightFailureInvalidatesCommonHandoff()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedEmptyMortalItemIdentityAsync(context.FileSystem);
        await context.SeedPlayerSkillSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestContext.CreateSkillApplyCommand()));

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(
            itemIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));

        var changedDefinition = EffectMaterializationTestFixture.CreateDefinition();
        changedDefinition["display"]!["name"] = "Подменённый источник";
        await context.SeedPlayerSkillSourceAsync(changedDefinition);
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        await Assert.ThrowsAsync<InvalidDataException>(() => context.Normalizer
            .BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups));

        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            writeLease));
    }

    /// <summary>
    /// Mutates the selected current authority after its common plan has been validated.
    /// </summary>
    /// <param name="context">
    /// Isolated effect fixture whose physical authority is changed.
    /// </param>
    /// <param name="path">
    /// Exact canonical path selected by the mutation row.
    /// </param>
    /// <param name="authorityKind">
    /// Named source, owner, target or other authority boundary to mutate.
    /// </param>
    /// <returns>
    /// A task completing after the selected changed authority has been written.
    /// </returns>
    private static async Task ApplyLateMutationAsync(
        EffectMaterializationTestContext context,
        string path,
        string authorityKind)
    {
        var root = await context.ReadJsonAsync(path);
        if (authorityKind == "source" && root is JsonObject skillRoot)
        {
            var skill = FindFirstObjectWithProperty(skillRoot, "activeEffectDefinitions")
                ?? throw new InvalidOperationException(
                    "Materializable skill source is missing from its registered source root.");
            skill["activeEffectDefinitions"]![0]!["display"]!["name"] =
                "Поздно изменённый источник";
            await context.WriteJsonAsync(path, skillRoot);
            return;
        }
        if (authorityKind is "owner" or "target" && root is JsonObject npcRoot)
        {
            var npc = FindFirstObjectWithProperty(npcRoot, "NPCId")
                ?? throw new InvalidOperationException(
                    "Materialized NPC target is missing from its canonical owner root.");
            npc["NPCId"] = authorityKind == "owner"
                ? "npc_resource_toctou_owner_changed"
                : "npc_resource_toctou_target_changed";
            await context.WriteJsonAsync(path, npcRoot);
            return;
        }
        if (root is JsonObject objectRoot)
        {
            objectRoot["_lateMutation"] = authorityKind;
            await context.WriteJsonAsync(path, objectRoot);
            return;
        }
        if (root is JsonArray arrayRoot)
        {
            arrayRoot.Add(new JsonObject { ["_lateMutation"] = authorityKind });
            await context.WriteJsonAsync(path, arrayRoot);
            return;
        }

        await context.WriteJsonAsync(
            path,
            new JsonObject { ["_lateMutation"] = authorityKind });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<FileSystemManager> Publisher, AcceptedMechanicsPlan Plan)
        PublishCommonPlanFromEphemeralManager(string rootPath)
    {
        var publisher = new FileSystemManager(
            rootPath,
            NullLogger<FileSystemManager>.Instance);
        var validator = new ValidationService(
            publisher,
            NullLogger<ValidationService>.Instance);

        var issues = validator.ValidateAcceptedTurnRawResourceMaterializationAsync()
            .GetAwaiter()
            .GetResult();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var handoff = AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(publisher)
            .GetAwaiter()
            .GetResult();
        var plan = Assert.IsType<AcceptedMechanicsPlan>(
            Assert.IsType<AcceptedMechanicsAuthorityTestProbe.CommonHandoff>(handoff)
                .Result.Plan);
        return (new WeakReference<FileSystemManager>(publisher), plan);
    }

    private static void ForceFullCollection(WeakReference<FileSystemManager> reference)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            if (!reference.TryGetTarget(out _))
                return;
        }
    }

    /// <summary>
    /// Creates an independent ordinary skill-effect input without a common publication handoff.
    /// </summary>
    /// <returns>
    /// A complete deterministic source, target and command input for independent effect-cache checks.
    /// </returns>
    private static EffectAcceptedTurnInput CreateIndependentEffectInput()
    {
        var source = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            new[]
            {
                new EffectSourceExport(
                    "mortal_world",
                    "skill",
                    EffectMaterializationTestContext.MaterializableSkillId,
                    new JsonArray(EffectMaterializationTestFixture.CreateDefinition()),
                    Materializable: true,
                    Active: true,
                    SameTurn: false)
            },
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));
        var target = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            new[]
            {
                new EffectTargetExport(
                    "mortal_world",
                    "player",
                    "player_current",
                    SameTurn: false)
            },
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            null));
        return new EffectAcceptedTurnInput(
            "session_independent_effect_cache",
            "snapshot_independent_effect_cache",
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestContext.CreateSkillApplyCommand()),
            source,
            target,
            new JsonObject
            {
                ["turn"] = 42,
                ["events"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "accepted_turn",
                    ["authorityId"] = "turn_42",
                    ["eventRef"] = "turn_42:wound_opened"
                })
            });
    }

    private static void SeedIndependentValidatedEffectCache(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        EffectAcceptedTurnInput input)
    {
        var validated = EffectAcceptedTurnPlanAuthority.GetOrBuildValidated(
            fileSystem,
            writeLease,
            input);
        Assert.True(
            validated.Success,
            string.Join(
                Environment.NewLine,
                validated.Issues.Select(issue =>
                    $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        Assert.IsType<EffectAcceptedTurnPlan>(validated.Plan);
        Assert.True(EffectAcceptedTurnPlanAuthority.TryPeekValidated(
            fileSystem,
            writeLease,
            out _));
    }

    private static JsonObject? FindFirstObjectWithProperty(JsonNode? node, string propertyName)
    {
        if (node is JsonObject objectNode)
        {
            if (objectNode.ContainsKey(propertyName))
                return objectNode;
            foreach (var child in objectNode)
            {
                var match = FindFirstObjectWithProperty(child.Value, propertyName);
                if (match != null)
                    return match;
            }
        }
        else if (node is JsonArray arrayNode)
        {
            foreach (var child in arrayNode)
            {
                var match = FindFirstObjectWithProperty(child, propertyName);
                if (match != null)
                    return match;
            }
        }
        return null;
    }

    private static async Task MaterializeNpcHealthAsync(
        EffectMaterializationTestContext context,
        string npcId,
        decimal maximum)
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        await context.WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(bootstrap.Definitions!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(bootstrap.State!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(bootstrap.History!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject { ["NPCsInScene"] = new JsonArray() });

        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            bootstrap.Definitions,
            context.FileSystem.ReadFileAsync,
            bootstrap.State,
            bootstrap.History,
            CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(
            authority.IsValid &&
            !string.IsNullOrWhiteSpace(authority.CanonicalAuthorityJson),
            string.Join(Environment.NewLine, authority.Issues));
        await context.WriteJsonAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            JsonNode.Parse(authority.CanonicalAuthorityJson!)!);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 41);

        var npc = EffectMaterializationTestFixture.CreateSameTurnMortalActor(npcId);
        npc["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject
            {
                ["resourceKey"] = "health",
                ["maximum"] = maximum
            })
        };
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject { ["UpdateNPCs"] = new JsonArray(npc) });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(
                Environment.NewLine,
                issues.Select(issue =>
                    $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.NotNull(await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null));
    }

    private static Task SeedEmptyMortalItemIdentityAsync(FileSystemManager fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return fileSystem.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
    }
}
