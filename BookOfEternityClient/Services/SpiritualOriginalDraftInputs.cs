using System.Collections.ObjectModel;
using System.Text;

namespace BookOfEternityClient.Services;

/// <summary>
/// Retains exact current distributed draft inputs for comparison with one original spiritual turn.
/// This detached view does not grant signed snapshot or publication authority.
/// </summary>
internal sealed class SpiritualOriginalDraftInputs
{
    private readonly IReadOnlyDictionary<string, CanonicalBeforeImage> _images;
    private readonly ReadOnlyCollection<string> _pathInventory;

    private SpiritualOriginalDraftInputs(string sessionId, string requestId, string snapshotToken,
        int turn, IReadOnlyList<string> pathInventory,
        IReadOnlyDictionary<string, CanonicalBeforeImage> images)
    {
        SessionId = sessionId;
        RequestId = requestId;
        SnapshotToken = snapshotToken;
        Turn = turn;
        _pathInventory = Array.AsReadOnly(pathInventory.ToArray());
        _images = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(images);
    }

    /// <summary>
    /// Gets the original session identifier.
    /// </summary>
    internal string SessionId { get; }

    /// <summary>
    /// Gets the original request identifier.
    /// </summary>
    internal string RequestId { get; }

    /// <summary>
    /// Gets the validated original snapshot token.
    /// </summary>
    internal string SnapshotToken { get; }

    /// <summary>
    /// Gets the original turn number.
    /// </summary>
    internal int Turn { get; }

    /// <summary>
    /// Gets the detached, ordinally ordered draft path inventory.
    /// </summary>
    internal IReadOnlyList<string> PathInventory => _pathInventory;

    /// <summary>
    /// Freezes exact images for the complete owner-selected draft inventory.
    /// </summary>
    /// <param name="sessionId">
    /// Original session identifier; it must be nonblank.
    /// </param>
    /// <param name="requestId">
    /// Original request identifier; it must be nonblank.
    /// </param>
    /// <param name="snapshotToken">
    /// Exact validated original snapshot token; it must be nonblank.
    /// </param>
    /// <param name="turn">
    /// Positive original turn number.
    /// </param>
    /// <param name="pathInventory">
    /// Complete relative draft paths, including paths currently absent.
    /// </param>
    /// <param name="images">
    /// Exact current images for every inventory path; neither missing nor extra keys are allowed.
    /// </param>
    /// <returns>
    /// A detached, ordinally ordered view with defensive image copies.
    /// </returns>
    internal static SpiritualOriginalDraftInputs Create(string sessionId, string requestId,
        string snapshotToken, int turn, IReadOnlyList<string> pathInventory,
        IReadOnlyDictionary<string, CanonicalBeforeImage> images)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(requestId) ||
            string.IsNullOrWhiteSpace(snapshotToken) || turn <= 0)
            throw new ArgumentException("An exact positive original turn identity is required.");
        ArgumentNullException.ThrowIfNull(pathInventory);
        ArgumentNullException.ThrowIfNull(images);
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in pathInventory)
        {
            if (!IsDraftPath(path) || !exact.Add(path) || !aliases.Add(path))
                throw new ArgumentException($"Invalid or case-confusable draft path '{path}'.",
                    nameof(pathInventory));
        }
        if (images.Count != exact.Count || images.Keys.Any(path => !exact.Contains(path)) ||
            images.Values.Any(static image => image is null))
            throw new ArgumentException("Draft images must match the exact inventory.", nameof(images));
        return new(sessionId, requestId, snapshotToken, turn,
            exact.OrderBy(static path => path, StringComparer.Ordinal).ToArray(), images);
    }

    /// <summary>
    /// Determines whether a relative path can be retained as ordinary draft data.
    /// </summary>
    /// <param name="path">
    /// Candidate relative path; blank, unsafe and excluded physical control paths are rejected.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a draft path; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsDraftPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path != path.Trim() ||
            path.Contains('\\') || path.Contains(':') || path.Contains("//", StringComparison.Ordinal) ||
            !PendingTurnSnapshotAuthority.IsSafeRelativePath(path))
            return false;
        var inRoot = path.StartsWith("game_state/", StringComparison.Ordinal) ||
                     path.StartsWith("lore/", StringComparison.Ordinal) ||
                     path.StartsWith("world_profiles/", StringComparison.Ordinal) ||
                     path.StartsWith("output/", StringComparison.Ordinal);
        if (!inRoot || path.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWith(ExplorerLocalTurnRollbackArtifacts.Root + "/",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, ExplorerLocalTurnRollbackArtifacts.Root,
                StringComparison.OrdinalIgnoreCase))
            return false;
        if (!path.StartsWith("game_state/control/", StringComparison.OrdinalIgnoreCase))
            return true;
        var control = path["game_state/control/".Length..];
        return !control.Equals("pending_turn_snapshot.json", StringComparison.OrdinalIgnoreCase) &&
               !control.Equals("pending_turn_snapshot.authority.json", StringComparison.OrdinalIgnoreCase) &&
               !control.StartsWith("pending_turn_snapshot/", StringComparison.OrdinalIgnoreCase) &&
               !path.Equals(SpiritualWoundCaptureCheckpointState.StatePath, StringComparison.OrdinalIgnoreCase) &&
               !control.Equals("pending_spiritual_wound_decisions.json", StringComparison.OrdinalIgnoreCase) &&
               !control.Equals("pending_wound_resolutions.json", StringComparison.OrdinalIgnoreCase) &&
               !control.Equals("pending_dice_state.json", StringComparison.OrdinalIgnoreCase) &&
               !control.Equals("pending_mortal_wound_occurrences.json", StringComparison.OrdinalIgnoreCase) &&
               !control.Equals("validation_repair_request.json", StringComparison.OrdinalIgnoreCase) &&
               !control.Equals("validation_repair_ready.json", StringComparison.OrdinalIgnoreCase) &&
               !control.Equals("validation_auto_rollback_report.json", StringComparison.OrdinalIgnoreCase) &&
               !control.StartsWith("gm_context_pack/", StringComparison.OrdinalIgnoreCase) &&
               !control.StartsWith("gm_bridge_status", StringComparison.OrdinalIgnoreCase) &&
               !control.StartsWith("gm_daemon_status", StringComparison.OrdinalIgnoreCase) &&
               !control.StartsWith("local_ui_session_lock", StringComparison.OrdinalIgnoreCase) &&
               !control.StartsWith("terminal_protocol_failure", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Detects a draft root whose spelling differs only by case from its canonical path.
    /// </summary>
    /// <param name="path">
    /// Enumerated or declared relative path to inspect; unrelated paths return <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a known draft root uses noncanonical casing; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool HasCaseAliasedDraftRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        var separator = path.IndexOf('/');
        if (separator <= 0)
            return false;
        var root = path[..separator];
        return new[] { "game_state", "lore", "world_profiles", "output" }.Any(canonical =>
            string.Equals(root, canonical, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(root, canonical, StringComparison.Ordinal));
    }

    /// <summary>
    /// Compares all four original identity coordinates without transferring authority.
    /// </summary>
    /// <param name="sessionId">
    /// Session identifier to compare.
    /// </param>
    /// <param name="requestId">
    /// Request identifier to compare.
    /// </param>
    /// <param name="snapshotToken">
    /// Snapshot token to compare.
    /// </param>
    /// <param name="turn">
    /// Turn number to compare.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only when every coordinate equals the retained original.
    /// </returns>
    internal bool MatchesIdentity(string sessionId, string requestId, string snapshotToken, int turn) =>
        SessionId == sessionId && RequestId == requestId && SnapshotToken == snapshotToken && Turn == turn;

    /// <summary>
    /// Reads a detached exact image only for a registered draft path.
    /// </summary>
    /// <param name="path">
    /// Exact case-sensitive inventory path.
    /// </param>
    /// <returns>
    /// A fresh image copy preserving present-empty and absent distinctions.
    /// </returns>
    internal CanonicalBeforeImage ReadImage(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!_images.TryGetValue(path, out var image))
            throw new KeyNotFoundException($"Original draft path '{path}' was not retained.");
        return new CanonicalBeforeImage(image.Existed, image.Bytes);
    }

    /// <summary>
    /// Creates a detached candidate layer over the same registered draft inventory.
    /// This operation does not authorize any changed path or continuation.
    /// </summary>
    /// <param name="changes">
    /// Exact path/image replacements; unknown paths and null images are rejected.
    /// </param>
    /// <returns>
    /// A new defensive draft view retaining the original identity and every unchanged image.
    /// </returns>
    internal SpiritualOriginalDraftInputs WithImageChanges(
        IReadOnlyDictionary<string, CanonicalBeforeImage> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Keys.Any(path => !_images.ContainsKey(path)) ||
            changes.Values.Any(static image => image is null))
            throw new ArgumentException("Changes must name registered draft images.", nameof(changes));
        var images = _images.ToDictionary(pair => pair.Key,
            pair => new CanonicalBeforeImage(pair.Value.Existed, pair.Value.Bytes),
            StringComparer.Ordinal);
        foreach (var pair in changes)
            images[pair.Key] = new CanonicalBeforeImage(pair.Value.Existed, pair.Value.Bytes);
        return Create(SessionId, RequestId, SnapshotToken, Turn, _pathInventory, images);
    }

    /// <summary>
    /// Decodes a retained current draft image using the ordinary filesystem text policy.
    /// </summary>
    /// <param name="path">
    /// Exact case-sensitive inventory path; an unknown path is rejected.
    /// </param>
    /// <returns>
    /// Decoded text, including an empty string for a present empty image, or <see langword="null"/>
    /// when the registered image was absent.
    /// </returns>
    internal string? ReadText(string path)
    {
        var image = ReadImage(path);
        if (!image.Existed)
            return null;
        using var stream = new MemoryStream(image.Bytes!, writable: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
