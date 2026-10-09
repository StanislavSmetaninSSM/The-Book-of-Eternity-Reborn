using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

// Fixture-only original publisher observations; no lease is opened from an observer.
internal sealed class PreparationPublicationProbe : IDisposable
{
    internal CleanupPublicationCut Cut { get; } = new();
    internal FileSystemManager Files { get; private set; } = null!;
    internal Dictionary<string, byte[]?> CommittedImages { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, byte[]?> ImagesAtCut { get; private set; } = new(StringComparer.Ordinal);
    internal Func<string, JsonElement, bool>? Select { get; set; }
    internal Action? BeforeCut { get; set; }
    internal Func<string, Task>? BeforeMutation { get; set; }
    internal string? SelectedPath { get; private set; }
    internal FileSystemManagerHooks Hooks { get; }
    internal PreparationPublicationProbe()
    {
        Cut.Select = (path, member) =>
        {
            if (Select?.Invoke(path, member) != true) return false;
            SelectedPath = path;
            return true;
        };
        Cut.ObserveBeforeCut = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.Committed) return;
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Cut.JournalPath));
            Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
            foreach (var member in journal.RootElement.GetProperty("Members").EnumerateArray())
            {
                var path = member.GetProperty("Path").GetString()!;
                CommittedImages[path] = CleanupPublicationCut.ReadOptional(path);
            }
        };
        Cut.BeforeCut = () =>
        {
            BeforeCut?.Invoke();
            ImagesAtCut = CommittedImages.Keys.Where(path => path != SelectedPath)
                .ToDictionary(path => path, CleanupPublicationCut.ReadOptional, StringComparer.Ordinal);
        };
        Hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path => BeforeMutation?.Invoke(path) ?? Task.CompletedTask,
            BeforeCanonicalMutationBoundaryAsync = Cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
            AfterCanonicalReadInitialValidationAsync = Cut.Hooks.AfterCanonicalReadInitialValidationAsync,
            BeforeCanonicalWriteLockOpenAsync = Cut.Hooks.BeforeCanonicalWriteLockOpenAsync,
            SessionOperationClosingAsync = Cut.Hooks.SessionOperationClosingAsync,
            LocalPublicationObserver = Cut.Hooks.LocalPublicationObserver,
            LocalPublicationRecoveryObserver = Cut.Hooks.LocalPublicationRecoveryObserver
        };
    }
    internal void Attach(FileSystemManager files) { Files = files; Cut.Attach(files); }
    internal void Arm() => Cut.Armed = true;
    internal string[] CommittedBackups(string suffix) => CommittedImages.Keys.Where(p => p.EndsWith(suffix, StringComparison.Ordinal)).ToArray();
    internal object Evidence() => new
    {
        Cut = Cut.Evidence(), CommittedPaths = CommittedImages.Keys.ToArray(), ImagesAtCut,
        AfterImages = ImagesAtCut.Keys.ToDictionary(path => path, CleanupPublicationCut.ReadOptional, StringComparer.Ordinal)
    };
    internal void AssertStopped(Exception? failure, bool explicitOutcome = false)
    {
        Cut.AssertReachedAndStopped();
        if (!explicitOutcome) Assert.Same(Cut.OriginalUncertainty, failure);
        foreach (var (path, bytes) in ImagesAtCut) Assert.Equal(bytes, CleanupPublicationCut.ReadOptional(path));
    }
    internal JsonObject ValidateManifest(bool requireAuthority)
    {
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        Assert.True(CommittedImages.ContainsKey(Files.ResolvePath(manifestPath)));
        var manifest = JsonNode.Parse(File.ReadAllText(Files.ResolvePath(manifestPath)))!.AsObject();
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var hashes = Assert.IsType<JsonObject>(manifest["snapshotFileHashes"]);
        Assert.NotEmpty(files);
        foreach (var (logical, snapshot) in files)
        {
            var path = snapshot!.GetValue<string>();
            Assert.True(PendingTurnSnapshotAuthority.IsSafeRelativePath(path));
            Assert.Equal(hashes[logical]!.GetValue<string>(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Files.ResolvePath(path)))), ignoreCase: true);
        }
        if (requireAuthority)
        {
            Assert.True(CommittedImages.ContainsKey(Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
            Assert.True(PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(
                File.ReadAllText(Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)), out var payload));
            Assert.NotNull(payload);
            Assert.Equal(manifest["sessionId"]!.GetValue<string>(), payload.SessionId);
            Assert.Equal(manifest["requestId"]!.GetValue<string>(), payload.RequestId);
            Assert.Equal(manifest["turnNumber"]!.GetValue<int>(), payload.TurnNumber);
            Assert.Equal(manifest["manifestPayloadHash"]!.GetValue<string>(), payload.ManifestPayloadHash);
            Assert.Equal(files.Count, payload.Files.Count);
            foreach (var (logical, snapshot) in files)
            {
                Assert.Equal(snapshot!.GetValue<string>(), payload.Files[logical]);
                Assert.Equal(hashes[logical]!.GetValue<string>(), payload.SnapshotFileHashes[logical]);
            }
        }
        return manifest;
    }
    public void Dispose() => Cut.Dispose();
}
