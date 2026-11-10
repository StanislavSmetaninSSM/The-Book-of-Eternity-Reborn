using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    private sealed partial class Fixture
    {
        /// <summary>
        /// Checks ordinary component refusals against a genuine original and restores each corrupted file exactly.
        /// A foreign namespace remains inside this fixture's owned container. The original lease is closed only
        /// to test its disposed state, then a new participating lease is acquired for the existing positive check.
        /// </summary>
        /// <param name="original">
        /// The actual current ordinary original whose files and data coordinates are used by these refusal probes.
        /// </param>
        /// <returns>
        /// A task completing after the expected refusals, exact restorations and canonical before-image checks.
        /// Coordinated uncertainty retains the owned container and prevents further probe restoration writes.
        /// </returns>
        internal async Task AssertOrdinaryOriginalRefusalsAsync(GameEngine.JointComponentOriginal original)
        {
            using var timing = Files.MeasureOperation("JointBundleFixture.OrdinaryOriginalRefusals");
            try
            {
                ArgumentNullException.ThrowIfNull(original);
                var protectedPaths = new[]
                {
                    LiveTurnPreparationService.TurnRequestPath,
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                    PendingTurnSnapshotAuthority.AuthorityPath,
                    ProgressionScheduleService.SchedulePath,
                    "game_state/meta/soul_state.json"
                };
                var before = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var path in protectedPaths)
                    before.Add(path, await Files.ReadFileBytesAsync(Lease, path) ??
                        throw new InvalidDataException("A genuine refusal baseline is missing: " + path));
                Assert.Null(await Files.ReadFileBytesAsync(Lease, "game_state/control/validation_repair_request.json"));
                Assert.Null(await Files.ReadFileBytesAsync(Lease, "ready/turn_complete.json"));

                var differentRequest = await AssertComponentRefusalAsync<InvalidDataException>("foreign_request_coordinates",
                    () => Runtime.Engine.CaptureJointProgressionForComponentAsync(Lease,
                        original with { RequestId = original.RequestId + "_foreign" }));
                Assert.Contains("component coordinates", differentRequest.Message, StringComparison.Ordinal);

                var foreignPath = Path.GetFullPath(Path.Combine(_owned, "foreign-refusals-" + Guid.NewGuid().ToString("N")));
                Assert.Equal(_owned, Path.GetDirectoryName(foreignPath));
                Assert.False(Directory.Exists(foreignPath));
                Assert.False(File.Exists(foreignPath));
                Directory.CreateDirectory(foreignPath);
                var foreignFiles = new FileSystemManager(foreignPath, NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance,
                    new FileSystemManagerHooks { OperationTimingObserver = Observe });
                foreignFiles.EnsureDirectoryStructure();
                var foreignLease = await foreignFiles.AcquireCanonicalWriteLeaseAsync();
                try
                {
                    var foreignGeneration = foreignFiles.GetOrCreateSessionGeneration(foreignLease);
                    Assert.NotEqual(original.Generation, foreignGeneration);
                    var differentGeneration = await AssertComponentRefusalAsync<InvalidDataException>("foreign_generation_coordinates",
                        () => Runtime.Engine.CaptureJointProgressionForComponentAsync(Lease,
                            original with { Generation = foreignGeneration }));
                    Assert.Contains("component coordinates", differentGeneration.Message, StringComparison.Ordinal);
                    var foreignProgression = await AssertComponentRefusalAsync<InvalidOperationException>("foreign_lease_progression",
                        () => Runtime.Engine.CaptureJointProgressionForComponentAsync(foreignLease, original));
                    Assert.Contains("Canonical write lease is not active", foreignProgression.Message, StringComparison.Ordinal);
                    var foreignPreparation = await AssertComponentRefusalAsync<InvalidOperationException>("foreign_lease_snapshot",
                        () => Runtime.Engine.PrepareOrdinaryOriginalForComponentAsync(foreignLease, new TurnRequest
                        {
                            SessionId = original.SessionId, RequestId = original.RequestId, TurnNumber = original.Turn,
                            PlayerAction = "Foreign lease refusal", ProgressionControl = _progression
                        }));
                    Assert.Contains("Canonical write lease is not active", foreignPreparation.Message, StringComparison.Ordinal);
                }
                finally
                {
                    try { await foreignLease.DisposeAsync(); }
                    catch { _retained = true; throw; }
                    Assert.False(foreignLease.IsActive);
                    _output.WriteLine(JsonSerializer.Serialize(new
                    {
                        Name = "JointBundleFixture.ForeignRefusalLeaseClosed", Path = foreignPath, Closed = !foreignLease.IsActive
                    }));
                }

                var changedRequest = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(
                    before[LiveTurnPreparationService.TurnRequestPath]))!.AsObject();
                changedRequest["requestId"] = original.RequestId + "_changed";
                await AssertCorruptedOriginalRefusedAndRestoredAsync(original, "malformed_current_request",
                    LiveTurnPreparationService.TurnRequestPath,
                    before[LiveTurnPreparationService.TurnRequestPath], FileSystemManager.EncodeUtf8WithPreamble("{"));
                await AssertCorruptedOriginalRefusedAndRestoredAsync(original, "changed_current_request",
                    LiveTurnPreparationService.TurnRequestPath,
                    before[LiveTurnPreparationService.TurnRequestPath],
                    FileSystemManager.EncodeUtf8WithPreamble(changedRequest.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)));
                await AssertCorruptedOriginalRefusedAndRestoredAsync(original, "incomplete_signed_authority",
                    PendingTurnSnapshotAuthority.AuthorityPath,
                    before[PendingTurnSnapshotAuthority.AuthorityPath], FileSystemManager.EncodeUtf8WithPreamble("{}"));

                var disposed = Lease;
                try { await disposed.DisposeAsync(); }
                catch { _retained = true; throw; }
                _lease = null;
                try
                {
                    Assert.False(disposed.IsActive);
                    var disposedProgression = await AssertComponentRefusalAsync<InvalidOperationException>("disposed_same_owner_lease_progression",
                        () => Runtime.Engine.CaptureJointProgressionForComponentAsync(disposed, original));
                    Assert.Contains("Canonical write lease is not active", disposedProgression.Message, StringComparison.Ordinal);
                    var disposedPreparation = await AssertComponentRefusalAsync<InvalidOperationException>("disposed_same_owner_lease_snapshot",
                        () => Runtime.Engine.PrepareOrdinaryOriginalForComponentAsync(disposed, new TurnRequest
                        {
                            SessionId = original.SessionId, RequestId = original.RequestId, TurnNumber = original.Turn,
                            PlayerAction = "Disposed lease refusal", ProgressionControl = _progression
                        }));
                    Assert.Contains("Canonical write lease is not active", disposedPreparation.Message, StringComparison.Ordinal);
                }
                finally
                {
                    if (!_retained)
                    {
                        try { _lease = await Files.AcquireCanonicalWriteLeaseAsync(); }
                        catch { _retained = true; throw; }
                    }
                }

                Assert.True(Files.IsCurrentSessionGeneration(Lease, original.Generation));
                foreach (var row in before)
                    Assert.Equal(row.Value, await Files.ReadFileBytesAsync(Lease, row.Key));
                _output.WriteLine(JsonSerializer.Serialize(new
                {
                    Name = "JointBundleFixture.OrdinaryRefusalsRestored", Original = original,
                    Refusals = 9, CanonicalPaths = protectedPaths, SameGeneration = original.Generation
                }));
            }
            catch (CoordinatedStatePublicationUncertainException)
            {
                _retained = true;
                throw;
            }
        }

        /// <summary>
        /// Requires a real typed operation to refuse with the expected exception and preserves its full diagnostic.
        /// </summary>
        /// <typeparam name="TException">
        /// The exact exception type expected from the actual production refusal.
        /// </typeparam>
        /// <param name="name">
        /// The complete diagnostic name of this refusal probe.
        /// </param>
        /// <param name="operation">
        /// The actual typed component call; this callback supplies no guard result or authority.
        /// </param>
        /// <returns>
        /// The exception actually thrown by the operation after its type has been checked.
        /// </returns>
        private async Task<TException> AssertComponentRefusalAsync<TException>(string name, Func<Task> operation)
            where TException : Exception
        {
            using var timing = Files.MeasureOperation("JointBundleFixture.Refusal." + name);
            var failure = await Assert.ThrowsAsync<TException>(async () =>
            {
                try { await operation(); }
                catch (CoordinatedStatePublicationUncertainException)
                {
                    _retained = true;
                    throw;
                }
            });
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Name = "JointBundleFixture.ComponentRefusal", Case = name, Failure = failure.ToString()
            }));
            return failure;
        }

        /// <summary>
        /// Publishes invalid probe bytes with an exact baseline, verifies refusal and restores the original bytes.
        /// Restoration also requires the exact invalid bytes and is skipped after coordinated uncertainty.
        /// </summary>
        /// <param name="original">
        /// The genuine original whose current authentication must reject the corrupted file.
        /// </param>
        /// <param name="name">
        /// The complete diagnostic name for this corruption probe.
        /// </param>
        /// <param name="path">
        /// The current request or signed authority path inside this fixture's canonical namespace.
        /// </param>
        /// <param name="before">
        /// The exact original bytes retained before any probe mutation.
        /// </param>
        /// <param name="corrupted">
        /// The invalid probe bytes to publish and subsequently require as the restoration baseline.
        /// </param>
        /// <returns>
        /// A task completing after refusal and byte-exact guarded restoration, or failing with retained uncertainty.
        /// </returns>
        private async Task AssertCorruptedOriginalRefusedAndRestoredAsync(GameEngine.JointComponentOriginal original,
            string name, string path, byte[] before, byte[] corrupted)
        {
            await PublishRefusalProbeBytesAsync(path, before, corrupted);
            try
            {
                await AssertComponentRefusalAsync<InvalidDataException>(name,
                    () => Runtime.Engine.CaptureJointProgressionForComponentAsync(Lease, original));
            }
            finally
            {
                if (!_retained)
                {
                    try { await PublishRefusalProbeBytesAsync(path, corrupted, before); }
                    catch { _retained = true; throw; }
                    Assert.Equal(before, await Files.ReadFileBytesAsync(Lease, path));
                    _output.WriteLine(JsonSerializer.Serialize(new
                    {
                        Name = "JointBundleFixture.RefusalProbeRestored", Case = name, Path = path, Bytes = before.Length
                    }));
                }
            }
        }

        /// <summary>
        /// Uses the normal guarded byte-publication journal for one request or authority probe transition.
        /// It never re-encodes the retained original bytes and preserves the writer's actual failure disposition.
        /// </summary>
        /// <param name="path">
        /// The current request or signed authority path; every other path is rejected.
        /// </param>
        /// <param name="before">
        /// The exact bytes that the normal publisher must still observe before changing the file.
        /// </param>
        /// <param name="after">
        /// The exact bytes to publish, including their original encoding or deliberately invalid probe content.
        /// </param>
        /// <returns>
        /// A task completing only after the existing publisher reports commitment and the bytes are observed.
        /// A failed publication retains this fixture's container.
        /// </returns>
        private async Task PublishRefusalProbeBytesAsync(string path, byte[] before, byte[] after)
        {
            if (path != LiveTurnPreparationService.TurnRequestPath && path != PendingTurnSnapshotAuthority.AuthorityPath)
                throw new ArgumentException("Only the current request and signed authority may be probed.", nameof(path));
            var outcome = await Files.PublishLocalFilesAsync(Lease, [new CanonicalLocalFileChange(path, before, after)]);
            if (outcome.Disposition != TrustedLocalPublicationDisposition.Committed)
                _retained = true;
            Files.RequireCommittedLocalPublication(outcome);
            Assert.Equal(after, await Files.ReadFileBytesAsync(Lease, path));
        }
    }
}
