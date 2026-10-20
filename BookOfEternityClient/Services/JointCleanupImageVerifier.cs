using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Verifies a fixed cleanup cohort and its retained byte and absence images without issuing authority.
/// </summary>
internal static class JointCleanupImageVerifier
{
    /// <summary>
    /// Reads members followed by retained entries, refusing the first exact-image mismatch.
    /// </summary>
    /// <param name="members">
    /// The fixed cleanup cohort containing each member's before and after images.
    /// </param>
    /// <param name="retained">
    /// Retained bytes, with a <see langword="null"/> value requiring physical absence rather than an empty file.
    /// </param>
    /// <param name="after">
    /// <see langword="true"/> compares committed member after-images; <see langword="false"/> compares member before-images.
    /// </param>
    /// <param name="read">
    /// The reader for each relative path; <see langword="null"/> means absence, and read failures propagate unchanged.
    /// </param>
    /// <returns>
    /// Completion after every image matches; no later entry is read after the first refusal or read failure.
    /// </returns>
    internal static async Task RequireImagesAsync(IReadOnlyList<CanonicalLocalFileChange> members,
        IReadOnlyDictionary<string, byte[]?> retained, bool after, Func<string, Task<byte[]?>> read)
    {
        foreach (var member in members)
            if (!BytesEqual(await read(member.RelativePath), after ? member.After : member.Before))
                throw new InvalidDataException("joint_cleanup_member_mismatch: " + member.RelativePath);
        foreach (var row in retained)
            if (!BytesEqual(await read(row.Key), row.Value))
                throw new InvalidDataException("joint_cleanup_retained_union_changed: " + row.Key);
    }

    /// <summary>
    /// Compares exact bytes while keeping physical absence distinct from an empty image.
    /// </summary>
    /// <param name="left">
    /// The read bytes, or <see langword="null"/> for absence.
    /// </param>
    /// <param name="right">
    /// The required bytes, or <see langword="null"/> for required absence.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if both are absent or have identical bytes; otherwise <see langword="false"/>.
    /// </returns>
    private static bool BytesEqual(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
}
