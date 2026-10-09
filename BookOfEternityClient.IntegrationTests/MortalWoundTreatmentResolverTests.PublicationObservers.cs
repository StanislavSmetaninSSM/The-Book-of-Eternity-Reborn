using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    private sealed record TreatmentCommittedImage(string Path, byte[]? Bytes,
        byte[] Journal, string TransactionId);

    private static string TreatmentJournalPath(FileSystemManager files) =>
        Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");

    private static IEnumerable<TreatmentCommittedImage> ReadTreatmentCommittedImages(FileSystemManager files)
    {
        var journal = File.ReadAllBytes(TreatmentJournalPath(files));
        using var parsed = CleanupPublicationCut.Metadata(journal);
        var root = parsed.RootElement;
        Assert.True(root.GetProperty("Committed").GetBoolean());
        foreach (var member in root.GetProperty("Members").EnumerateArray())
        {
            var path = member.GetProperty("Path").GetString()!;
            var relative = new[] { AcceptedMechanicsPlan.WoundCommandPath,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath }.SingleOrDefault(candidate =>
                string.Equals(files.ResolvePath(candidate), path,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (relative == null) continue;
            var bytes = CleanupPublicationCut.ReadOptional(path);
            var after = member.GetProperty("After");
            Assert.Equal(after.GetProperty("Exists").GetBoolean(), bytes != null);
            Assert.Equal(after.GetProperty("Sha256").GetString(),
                bytes == null ? null : Convert.ToHexString(SHA256.HashData(bytes)));
            yield return new(relative, bytes, journal, root.GetProperty("TransactionId").GetString()!);
        }
    }

    private static bool ContainsTreatmentRequest(byte[]? bytes, bool command,
        MortalWoundTreatmentAttemptRequest request)
    {
        if (bytes == null) return false;
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        var root = JsonNode.Parse(text)!.AsObject();
        if (root[command ? "commands" : "submittedTreatmentRequests"] is not JsonArray rows) return false;
        return rows.OfType<JsonObject>().Any(row =>
        {
            var authority = command ? row["authority"]?["request"] : row["request"];
            return row["operationKey"]?.GetValue<string>() == request.Coordinates.OperationKey &&
                authority?["requestFingerprint"]?.GetValue<string>() == request.RequestFingerprint &&
                authority?["coordinates"]?["attemptId"]?.GetValue<string>() == request.Coordinates.AttemptId;
        });
    }

    private sealed class TerminalDurableSurfaceRemovalObserver
    {
        private FileSystemManager? _files;
        private MortalWoundTreatmentAttemptRequest? _request;
        private byte[]? _generation;
        private readonly List<object> _events = [];
        private readonly HashSet<string> _removed = new(StringComparer.Ordinal);
        private readonly HashSet<string> _restored = new(StringComparer.Ordinal);
        internal bool ObservedCommandRemoval => _removed.Contains(AcceptedMechanicsPlan.WoundCommandPath);
        internal bool ObservedPendingRemoval => _removed.Contains(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        internal bool PendingContainedRequest { get; private set; }

        internal void Arm(FileSystemManager files, MortalWoundTreatmentAttemptRequest request)
        {
            _files = files; _request = request;
            _generation = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
            Assert.True(ContainsTreatmentRequest(CleanupPublicationCut.ReadOptional(
                files.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath)), true, request));
            PendingContainedRequest = ContainsTreatmentRequest(CleanupPublicationCut.ReadOptional(
                files.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath)), false, request);
        }

        internal void Observe(TrustedLocalPublicationPhase phase, int index)
        {
            if (_files == null || phase != TrustedLocalPublicationPhase.Committed) return;
            foreach (var image in ReadTreatmentCommittedImages(_files))
            {
                var contains = ContainsTreatmentRequest(image.Bytes,
                    image.Path == AcceptedMechanicsPlan.WoundCommandPath, _request!);
                _events.Add(new { sequence = _events.Count + 1, image, contains });
                if (!contains) _removed.Add(image.Path);
                else if (_removed.Contains(image.Path)) _restored.Add(image.Path);
            }
        }

        internal void WriteEvidence(Action<string> output) => output(JsonSerializer.Serialize(new
        {
            kind = "treatment-terminal-committed-removal", _events, _removed, _restored,
            PendingContainedRequest, _generation,
            generationAfter = CleanupPublicationCut.ReadOptional(_files!.SessionGenerationPath),
            journalAfter = CleanupPublicationCut.ReadOptional(TreatmentJournalPath(_files))
        }));

        internal void AssertReached()
        {
            Assert.True(ObservedCommandRemoval);
            Assert.Equal(PendingContainedRequest, ObservedPendingRemoval);
            Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, _restored);
            if (PendingContainedRequest) Assert.Contains(WoundAcceptedTurnSnapshotContract.PendingResolutionPath, _restored);
            Assert.Equal(_generation, CleanupPublicationCut.ReadOptional(_files!.SessionGenerationPath));
            Assert.Null(CleanupPublicationCut.ReadOptional(TreatmentJournalPath(_files)));
        }
    }

    private sealed class DurableCommandProofFailureInjection
    {
        private FileSystemManager? _files;
        private TreatmentCommittedImage? _command;
        private byte[]? _generation;
        private byte[]? _beforeCorruption;
        private byte[]? _afterCorruption;
        private string? _nextMutation;
        private int _corruptions;

        internal void Arm(FileSystemManager files)
        {
            _files = files;
            _generation = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        }

        internal void Observe(TrustedLocalPublicationPhase phase, int index)
        {
            if (_files == null || _command != null || phase != TrustedLocalPublicationPhase.Committed) return;
            _command = ReadTreatmentCommittedImages(_files).SingleOrDefault(
                image => image.Path == AcceptedMechanicsPlan.WoundCommandPath);
        }

        internal Task BeforeCanonicalMutationAsync(string path)
        {
            if (_command == null || _corruptions != 0) return Task.CompletedTask;
            _nextMutation = path;
            Assert.Null(CleanupPublicationCut.ReadOptional(TreatmentJournalPath(_files!)));
            var physical = _files!.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath);
            _beforeCorruption = File.ReadAllBytes(physical);
            Assert.Equal(_command.Bytes, _beforeCorruption);
            File.AppendAllText(physical, " ");
            _afterCorruption = File.ReadAllBytes(physical);
            _corruptions++;
            return Task.CompletedTask;
        }

        internal void WriteEvidence(Action<string> output) => output(JsonSerializer.Serialize(new
        {
            kind = "treatment-durable-command-proof", _command, _nextMutation, _corruptions,
            _beforeCorruption, _afterCorruption, _generation,
            generationAfter = CleanupPublicationCut.ReadOptional(_files!.SessionGenerationPath),
            journalAfter = CleanupPublicationCut.ReadOptional(TreatmentJournalPath(_files))
        }));

        internal void AssertReached()
        {
            Assert.NotNull(_command);
            Assert.Equal(1, _corruptions);
            Assert.NotNull(_nextMutation);
            Assert.Equal(_command.Bytes, _beforeCorruption);
            Assert.Equal(_beforeCorruption!.Concat(new byte[] { 32 }).ToArray(), _afterCorruption);
            Assert.Equal(_generation, CleanupPublicationCut.ReadOptional(_files!.SessionGenerationPath));
            Assert.Null(CleanupPublicationCut.ReadOptional(TreatmentJournalPath(_files)));
        }
    }

    private sealed class TreatmentFixtureCleanup(AcceptedStateFixture fixture, Action<string> output) : IDisposable
    {
        public void Dispose()
        {
            Exception? failure = null;
            try { fixture.Dispose(); }
            catch (Exception error) { failure = error; }
            // The original fixture releases its actual held lease before deleting its root.
            // On a failed release do not attempt a second deletion under the active lease.
            output(JsonSerializer.Serialize(new { CleanupOwnedRoot = fixture.Root,
                OwnedFixtureRemoved = !Directory.Exists(fixture.Root),
                LeaseInactive = !fixture.Lease.IsActive, CleanupFailure = failure?.ToString() }));
            Assert.Null(failure);
            Assert.False(fixture.Lease.IsActive);
            Assert.False(Directory.Exists(fixture.Root));
        }
    }
}
