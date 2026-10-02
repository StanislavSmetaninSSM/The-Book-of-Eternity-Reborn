namespace BookOfEternityClient.Core;

/// <summary>Internal exact image transport; file-backed images do not grant source mutation authority.</summary>
internal sealed record TrustedLocalFileImage
{
    public required bool Exists { get; init; }
    public required byte[]? Bytes { get; init; }
    public required string? Sha256 { get; init; }

    internal long Length => throw new NotImplementedException("T032-A1 image scaffold");
    internal bool IsFileBacked => throw new NotImplementedException("T032-A1 image scaffold");
    internal static TrustedLocalFileImage FromBytes(byte[]? bytes) =>
        throw new NotImplementedException("T032-A1 image scaffold");
    internal static TrustedLocalFileImage CaptureFile(TrustedLocalFileScope scope, string path) =>
        throw new NotImplementedException("T032-A1 image scaffold");
    internal void CopyTo(Stream destination) => throw new NotImplementedException("T032-A1 image scaffold");
}
