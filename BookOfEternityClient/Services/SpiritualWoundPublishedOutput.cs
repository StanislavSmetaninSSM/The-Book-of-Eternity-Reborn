using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;
using SpiritualPublicationReceipt = BookOfEternityClient.Services.ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationReceipt;

namespace BookOfEternityClient.Services;

/// <summary>
/// Retains detached presentation and exact output witnesses from one completed spiritual publication.
/// These values permit comparison and delivery only; they grant no execution or publication authority.
/// </summary>
internal sealed class SpiritualWoundPublishedOutput
{
    private readonly FileSystemManager _fileSystem;
    private readonly IReadOnlyDictionary<string, CanonicalBeforeImage> _images;
    private readonly WoundAcceptedTurnOutputBindingResult _binding;

    /// <summary>
    /// Retains a transaction-checked presentation with detached output witnesses.
    /// </summary>
    /// <param name="fileSystem">
    /// Exact filesystem instance that owns this publication.
    /// </param>
    /// <param name="images">
    /// Exact committed output images, including absence.
    /// </param>
    /// <param name="binding">
    /// Escaped presentation and narration diagnostics from the ordered insertions.
    /// </param>
    private SpiritualWoundPublishedOutput(FileSystemManager fileSystem,
        IReadOnlyDictionary<string, CanonicalBeforeImage> images, WoundAcceptedTurnOutputBindingResult binding)
    {
        _fileSystem = fileSystem;
        _images = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(images);
        _binding = binding;
    }

    /// <summary>
    /// Gets detached notifications; an unsuccessful narration binding supplies none.
    /// </summary>
    internal IReadOnlyList<WoundPlayerNotification> Notifications => _binding.Notifications;

    /// <summary>
    /// Gets diagnostics that prevent transaction acceptance.
    /// </summary>
    internal IReadOnlyList<ValidationIssue> Issues => _binding.Issues;

    /// <summary>
    /// Binds live insertion presentation while the exact common publication receipt is still taken.
    /// </summary>
    /// <param name="fileSystem">
    /// Owning filesystem used for exact read-back under <paramref name="lease"/>.
    /// </param>
    /// <param name="lease">
    /// Active canonical publication lease.
    /// </param>
    /// <param name="receipt">
    /// Genuine taken receipt retaining the completed ordered insertion evidence.
    /// </param>
    /// <returns>
    /// Detached binding with an empty notification list when this publication has no wound insertion.
    /// </returns>
    internal static async Task<SpiritualWoundPublishedOutput> BindAsync(FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease, SpiritualPublicationReceipt receipt)
    {
        if (!AcceptedMechanicsPlanAuthority.IsTakenSpiritualPublicationCurrent(fileSystem, lease, receipt))
            throw new InvalidDataException("Spiritual output requires the current publication receipt.");
        var completion = receipt.Authority.LiveWoundCompletion;
        var inputs = receipt.Authority.PublicationInputs;
        var images = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        foreach (var path in WoundAcceptedTurnSnapshotContract.OutputPaths)
        {
            if (!inputs.TryGetValue(path, out var image))
                throw new InvalidDataException($"Spiritual output has no committed witness for '{path}'.");
            images.Add(path, image);
        }
        await RequireExactImagesAsync(fileSystem, lease, images);
        var scene = string.Empty;
        var narrative = images["output/narrative_response.json"].Bytes;
        if (narrative is not null)
        {
            try
            {
                var root = StrictJsonAuthority.Deserialize<JsonObject>(
                    SpiritualWoundStateJson.DecodeUtf8JsonText(narrative),
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "published spiritual wound narrative");
                if (root?["response"] is JsonValue value && value.TryGetValue<string>(out var text))
                    scene = text ?? string.Empty;
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or
                InvalidOperationException or NotSupportedException or System.Text.DecoderFallbackException)
            {
                scene = string.Empty;
            }
        }
        return new(fileSystem, images, completion is null
            ? new WoundAcceptedTurnOutputBindingResult(Array.Empty<WoundPlayerNotification>(), Array.Empty<ValidationIssue>())
            : WoundPlayerNotification.ComposeSpiritualAcceptedTurn(completion, scene));
    }

    /// <summary>
    /// Rejects changed output before the accepted caller transfers these notifications to the player response.
    /// </summary>
    /// <param name="fileSystem">
    /// Original filesystem instance; another instance cannot inherit this presentation handoff.
    /// </param>
    /// <returns>
    /// A task completing only when every retained output still has its exact accepted bytes or absence.
    /// </returns>
    internal async Task RequireCurrentAsync(FileSystemManager fileSystem)
    {
        if (!ReferenceEquals(fileSystem, _fileSystem))
            throw new InvalidDataException("Spiritual output belongs to another filesystem instance.");
        await using var lease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        await RequireExactImagesAsync(fileSystem, lease, _images);
    }

    /// <summary>
    /// Reads the exact retained texts after checking their physical witnesses under one canonical lease.
    /// Response construction uses these immutable strings instead of reopening mutable output files.
    /// </summary>
    /// <param name="fileSystem">
    /// Original filesystem instance receiving the accepted response.
    /// </param>
    /// <returns>
    /// Detached output texts keyed by canonical path; an originally absent file has a <see langword="null"/> value.
    /// </returns>
    internal async Task<IReadOnlyDictionary<string, string?>> ReadCurrentTextsAsync(FileSystemManager fileSystem)
    {
        await RequireCurrentAsync(fileSystem);
        return _images.ToDictionary(pair => pair.Key,
            pair => pair.Value.Bytes is { } bytes ? SpiritualWoundStateJson.DecodeUtf8JsonText(bytes) : null,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Compares each retained output under the caller's canonical lease.
    /// </summary>
    /// <param name="fileSystem">
    /// Filesystem to read.
    /// </param>
    /// <param name="lease">
    /// Active canonical lease protecting the comparison.
    /// </param>
    /// <param name="images">
    /// Exact expected images, including absent files.
    /// </param>
    /// <returns>
    /// A task completing after all images agree, throwing when any output has changed.
    /// </returns>
    private static async Task RequireExactImagesAsync(FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease, IReadOnlyDictionary<string, CanonicalBeforeImage> images)
    {
        foreach (var pair in images)
        {
            var actual = await fileSystem.ReadFileBytesAsync(lease, pair.Key);
            var expected = pair.Value.Bytes;
            if (pair.Value.Existed != (actual is not null) ||
                (expected is null ? actual is not null : actual is null || !expected.AsSpan().SequenceEqual(actual)))
                throw new InvalidDataException($"Spiritual published output '{pair.Key}' changed before delivery.");
        }
    }
}
