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
