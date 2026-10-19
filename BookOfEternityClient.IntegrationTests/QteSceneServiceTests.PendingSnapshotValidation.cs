using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class QteSceneServiceTests
{
    /// <summary>
    /// Preserves the genuine story writer's exact afterimage and ordered history issues through borrowed validation.
    /// </summary>
    /// <param name="tampered">
    /// Whether to change the accepted story bytes after the actual writer has issued its evidence.
    /// </param>
    /// <returns>
    /// Completion after scalar parity, the expected history verdict and one bounded proof lifetime are checked.
    /// </returns>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task QtePendingSnapshot_AcceptedStoryProof(bool tampered)
    {
        ValidationService? validator = null;
        var invocations = new HashSet<object>();
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = _ =>
                {
                    if (validator != null && PendingQteInvocation(validator) is { } invocation) invocations.Add(invocation);
                    return Task.CompletedTask;
                }
            });
        const string realm = "Mortal World";
        const string action = "Осмотреть рану.";
        await files.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            "{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
        await files.WriteFileAtomicAsync("game_state/history/chat_log.json", "[]");
        var story = new StoryService(files, NullLogger<StoryService>.Instance);
        await story.AppendTurnAsync(20, realm, 1, "Подойти ближе.", "Герой подходит.");
        await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "borrowed-story-session", RequestId = "borrowed-story-request", TurnNumber = 21,
            CurrentRealm = realm, PlayerAction = action, PreGeneratedDices1d20 = [8]
        });
        await BindOrdinaryQteSnapshotSourceAsync(files);
        await files.WriteFileAtomicAsync(QteSceneService.QteOfferPath, "{}");
        var accepted = await story.AppendAcceptedQteSourceTurnAsync(21, realm, 1, action, "Осмотр завершён.", null, null);
        Assert.NotNull(accepted);
        var acceptedBytes = accepted.AfterBytes;
        if (tampered) await files.AppendFileAtomicAsync(accepted.StoryPath, " ");
        validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var scalar = await validator.ValidateQteTerminalGameStateAsync(accepted);
        Assert.Empty(invocations);
        var reserved = await validator.ValidateQteTerminalGameStateAsync(lease, accepted);
        Assert.Equal(scalar.Select(issue => JsonSerializer.Serialize(issue)), reserved.Select(issue => JsonSerializer.Serialize(issue)));
        Assert.Equal(tampered, reserved.Any(issue => issue.Code == "client_owned_story_append_afterimage_mismatch"));
        Assert.DoesNotContain(reserved, issue => issue.Code == "client_owned_history_surface_modified");
        Assert.Equal(acceptedBytes, accepted.AfterBytes);
        Assert.True(lease.IsActive);
        Assert.Single(invocations);
        Assert.Null(PendingQteInvocation(validator));
    }

    /// <summary>
    /// Signs the fixture's prepared physical snapshot as the ordinary player-turn source required by the QTE writer.
    /// </summary>
    /// <param name="files">
    /// Independent fixture containing the actual preparation service's snapshot files and request.
    /// </param>
    /// <returns>
    /// Completion after publishing the matching manifest and detached authority through the ordinary writer.
    /// </returns>
    private static async Task BindOrdinaryQteSnapshotSourceAsync(FileSystemManager files)
    {
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
            (await files.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!,
            LiveTurnPreparationService.ManifestJsonOptions)!;
        // The live-test helper's source label deliberately cannot authorize QTE offers.
        // Retain all its physical inputs and issue the ordinary fixture's corresponding signed source pair.
        manifest.SourceLabel = QteSceneService.OrdinaryPlayerTurnSourceLabel;
        var authority = PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(manifest,
            LiveTurnPreparationService.ManifestHashJsonOptions,
            static value => value.ManifestPayloadHash, static (value, hash) => value.ManifestPayloadHash = hash,
            static value => value.SessionId, static value => value.RequestId, static value => value.TurnNumber,
            static value => value.Files, static value => value.SnapshotFileHashes,
            static value => value.ClientOwnedValidationHashes, static value => value.RollbackBaselineFiles,
            static value => value.SourceLabel, static value => value.RollbackBackups,
            path => files.ReadFileBytesSync(lease, path));
        await files.WriteFileAtomicAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            JsonSerializer.Serialize(manifest, LiveTurnPreparationService.ManifestJsonOptions));
        await files.WriteFileAtomicAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath, authority);
    }

    /// <summary>
    /// Keeps retained manifest overrides on the existing scalar QTE route without installing a borrowed proof.
    /// </summary>
    /// <returns>
    /// Completion after exact ordered issue parity and preservation of the caller's original lease.
    /// </returns>
    [Fact]
    public async Task QtePendingSnapshot_OverrideKeepsScalarRoute()
    {
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        using var manifestScope = validator.UsePrevalidatedPendingTurnSnapshotScope(new LiveTurnPendingSnapshotManifest
        {
            SessionId = "retained-session", RequestId = "retained-request", TurnNumber = 21,
            SourceLabel = QteSceneService.OrdinaryPlayerTurnSourceLabel
        });
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var scalar = await validator.ValidateQteTerminalGameStateAsync(null);
        var retained = await validator.ValidateQteTerminalGameStateAsync(lease, null);
        Assert.Equal(scalar.Select(issue => JsonSerializer.Serialize(issue)), retained.Select(issue => JsonSerializer.Serialize(issue)));
        Assert.True(lease.IsActive);
        Assert.Null(PendingQteInvocation(validator));
    }

    /// <summary>
    /// Preserves the scalar route's result or missing-draft failure for a retained same-turn owner view.
    /// </summary>
    /// <returns>
    /// Completion after checking the original route's exact outcome and unchanged external ownership.
    /// </returns>
    [Fact]
    public async Task QtePendingSnapshot_SameTurnKeepsScalarRoute()
    {
        var inputs = SpiritualOriginalDraftInputs.Create("same-turn-session", "same-turn-request", "same-turn-token", 21,
            [], new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal));
        var constructor = typeof(ValidationService).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 3);
        var validator = (ValidationService)constructor.Invoke([_fs, NullLogger<ValidationService>.Instance, inputs]);
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        List<ValidationIssue>? scalar = null, retained = null;
        var originalError = await Record.ExceptionAsync(async () => scalar = await validator.ValidateQteTerminalGameStateAsync(null));
        var retainedError = await Record.ExceptionAsync(async () => retained = await validator.ValidateQteTerminalGameStateAsync(lease, null));
        Assert.Equal(originalError?.GetType(), retainedError?.GetType());
        Assert.Equal(originalError?.Message, retainedError?.Message);
        if (originalError == null)
            Assert.Equal(scalar!.Select(issue => JsonSerializer.Serialize(issue)), retained!.Select(issue => JsonSerializer.Serialize(issue)));
        Assert.True(lease.IsActive);
        Assert.Null(PendingQteInvocation(validator));
    }

    /// <summary>
    /// The actual terminal caller gives each validation a fresh proof under its one original lease.
    /// </summary>
    /// <param name="allowPreexisting">
    /// True exercises both baseline and final validation; false exercises only final validation.
    /// </param>
    /// <returns>
    /// Completion after checking proof identities, write ordering and the owner's eventual disposal.
    /// </returns>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task QtePendingSnapshot_CallerSeparatesInvocations(bool allowPreexisting)
    {
        ValidationService? validator = null;
        var invocations = new HashSet<object>();
        var leases = new HashSet<FileSystemManager.CanonicalWriteLease>();
        var mutationInsideProof = false;
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = _ =>
                {
                    if (validator != null && PendingQteInvocation(validator) is { } invocation)
                    {
                        invocations.Add(invocation);
                        if (PendingQteLease(invocation) is { } lease) leases.Add(lease);
                    }
                    return Task.CompletedTask;
                },
                BeforeCanonicalMutationBoundaryAsync = _ =>
                {
                    mutationInsideProof |= validator != null && PendingQteInvocation(validator) != null;
                    return Task.CompletedTask;
                }
            });
        await files.WriteFileAtomicAsync("game_state/player/experience.json", "{\"totalExperience\":10}");
        var service = CreateRuntimeCapableService(files);
        validator = (ValidationService)typeof(QteSceneService).GetField("_validator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        var error = await Record.ExceptionAsync(() => service.ApplyTerminalOutcomeValidatedStateChangesAsync(
            PendingQteOutcome(invalidName: false), allowPreexisting));

        // The minimal physical fixture deliberately has preexisting required-file issues.
        // The permissive caller filters those unchanged issues; the strict caller restores on them.
        if (allowPreexisting) Assert.Null(error); else Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(allowPreexisting ? 2 : 1, invocations.Count);
        Assert.Single(leases);
        Assert.All(invocations, invocation => Assert.True((bool)invocation.GetType()
            .GetField("Closed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(invocation)!));
        Assert.False(mutationInsideProof);
        Assert.False(leases.Single().IsActive);
        Assert.Null(PendingQteInvocation(validator));
        AssertNoQteBackupArtifacts();
    }

    /// <summary>
    /// Actual validation failure restores prior bytes and cleans backups only after proof has closed.
    /// </summary>
    /// <returns>
    /// Completion after the real QTE compensation path restores the original experience value.
    /// </returns>
    [Fact]
    public async Task QtePendingSnapshot_ValidationFailureRestoresAfterClose()
    {
        ValidationService? validator = null;
        var observedProof = false;
        var writesAfterProof = 0;
        var mutationInsideProof = false;
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = _ =>
                {
                    observedProof |= validator != null && PendingQteInvocation(validator) != null;
                    return Task.CompletedTask;
                },
                BeforeCanonicalMutationBoundaryAsync = _ =>
                {
                    if (validator != null && PendingQteInvocation(validator) != null) mutationInsideProof = true;
                    else if (observedProof) writesAfterProof++;
                    return Task.CompletedTask;
                }
            });
        await files.WriteFileAtomicAsync("game_state/player/experience.json", "{\"totalExperience\":10}");
        var service = CreateRuntimeCapableService(files);
        validator = (ValidationService)typeof(QteSceneService).GetField("_validator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyTerminalOutcomeValidatedStateChangesAsync(PendingQteOutcome(invalidName: true)));
        Assert.True(observedProof);
        Assert.False(mutationInsideProof);
        Assert.True(writesAfterProof > 0);
        using var restored = JsonDocument.Parse((await files.ReadFileAsync("game_state/player/experience.json"))!);
        Assert.Equal(10, restored.RootElement.GetProperty("totalExperience").GetInt32());
        Assert.False(files.FileExists("game_state/player/transformation.json"));
        Assert.Null(PendingQteInvocation(validator));
        AssertNoQteBackupArtifacts();
    }

    /// <summary>
    /// Builds a real terminal response with optional invalid current-state materialization.
    /// </summary>
    /// <param name="invalidName">
    /// True includes the existing invalid-name regression; false keeps only ordinary experience change.
    /// </param>
    /// <returns>
    /// An outcome consumed by the actual terminal state-change service.
    /// </returns>
    private static QteSceneService.QteTerminalOutcome PendingQteOutcome(bool invalidName) => new()
    {
        OutcomeId = "borrowed_validation", Title = "Validation boundary", FinalNarrative = "Исход применён.",
        GmSummary = "Validation boundary regression.",
        ResponseFragment = JsonNode.Parse(invalidName
            ? "{\"response\":\"Исход применён.\",\"experienceGained\":5,\"playerCharacterNameChange\":\"Новая личность\"}"
            : "{\"response\":\"Исход применён.\",\"experienceGained\":5}")!.AsObject()
    };

    /// <summary>
    /// Observes the actual validator reservation without constructing or granting source authority.
    /// </summary>
    /// <param name="validator">
    /// Validator used by the real QTE service.
    /// </param>
    /// <returns>
    /// The active invocation identity, or null outside validation.
    /// </returns>
    private static object? PendingQteInvocation(ValidationService validator) => typeof(ValidationService)
        .GetField("_pendingSnapshotReservation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(validator);

    /// <summary>
    /// Observes the physical scope's retained original lease during an actual validation read.
    /// </summary>
    /// <param name="invocation">
    /// Actual invocation observed from the validator.
    /// </param>
    /// <returns>
    /// Its lease after opening, or null while the scope is not yet installed.
    /// </returns>
    private static FileSystemManager.CanonicalWriteLease? PendingQteLease(object invocation)
    {
        var scope = invocation.GetType().GetField("Physical", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(invocation);
        return scope == null ? null : (FileSystemManager.CanonicalWriteLease?)scope.GetType()
            .GetProperty("Lease", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scope);
    }
}
