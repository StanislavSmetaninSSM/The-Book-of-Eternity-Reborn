using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Replays an earlier response at the current offer and observes the engine's actual rejection before checking exact state preservation.
    /// </summary>
    /// <param name="context">
    /// Real signed session paused at its second spiritual offer.
    /// </param>
    /// <param name="current">
    /// Current issued request whose identity must survive the rejected response.
    /// </param>
    /// <param name="firstReady">
    /// Exact response text previously consumed at the first offer.
    /// </param>
    /// <param name="originalImages">
    /// Original signed and canonical images that must remain unchanged before publication.
    /// </param>
    /// <param name="cancellationToken">
    /// Stops the observer when the owning bounded lifecycle operation ends.
    /// </param>
    /// <returns>
    /// A task completing only after a new rejection report appears and all original and private images match.
    /// </returns>
    private static async Task AssertStaleSpiritualReadyRejectedAsync(ResourceMaterializationTestContext context,
        SpiritualWoundContinuationRequest current, string firstReady,
        IReadOnlyDictionary<string, byte[]> originalImages, CancellationToken cancellationToken)
    {
        const string requestPath = "game_state/control/validation_repair_request.json";
        const string readyPath = "game_state/control/validation_repair_ready.json";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        var images = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token))
        {
            foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                         SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath,
                         ResourceMaterializationContract.HistoryPath })
                images.Add(path, await context.FileSystem.ReadFileBytesAsync(lease, path));
            var checkpoint = ParseDependentSpiritualBytes(Assert.IsType<byte[]>(images[SpiritualWoundCaptureCheckpointState.StatePath]));
            Assert.Equal(1, checkpoint["checkpoint"]!["committedAdvance"]!.GetValue<int>());
            Assert.Null(checkpoint["checkpoint"]!["pendingSubmission"]);
            Assert.False(context.FileSystem.FileExists(lease, readyPath));
        }
        await context.WriteExactJsonAsync(readyPath, firstReady);
        JsonObject? rejection = null;
        while (rejection is null)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var physical = context.FileSystem.ResolvePath(requestPath);
            if (File.Exists(physical))
            {
                try
                {
                    using var stream = new FileStream(physical, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    var report = JsonNode.Parse(await reader.ReadToEndAsync(deadline.Token))!.AsObject();
                    if (report["revalidationAttempt"]!.GetValue<int>() > 1 && report["errors"]!.AsArray().Count > 0)
                        rejection = report;
                }
                catch (FileNotFoundException) { }
            }
            if (rejection is null)
                await Task.Delay(25, deadline.Token);
        }
        Assert.Equal(current.ContinuationId, rejection[SpiritualWoundContinuationProtocol.EnvelopeName]!["continuationId"]!.GetValue<string>());
        Assert.Equal("decision", rejection[SpiritualWoundContinuationProtocol.EnvelopeName]!["phase"]!.GetValue<string>());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token))
        {
            Assert.False(context.FileSystem.FileExists(lease, readyPath));
            Assert.False(context.FileSystem.FileExists(lease, SpiritualWoundOpportunityReceiptState.StatePath));
            foreach (var pair in originalImages)
                Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
            foreach (var pair in images)
                Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
        }
    }
}
