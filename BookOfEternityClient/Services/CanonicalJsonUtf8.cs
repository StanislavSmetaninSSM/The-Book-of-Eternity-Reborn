using System.Text;

namespace BookOfEternityClient.Services;

internal static class CanonicalJsonUtf8
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static string DecodeOneOptionalBom(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var preamble = Encoding.UTF8.GetPreamble();
        var payload = bytes.AsSpan();
        if (payload.StartsWith(preamble))
            payload = payload[preamble.Length..];
        if (payload.IndexOf(preamble) >= 0)
        {
            throw new InvalidDataException(
                "Canonical JSON permits at most one leading UTF-8 BOM.");
        }

        try
        {
            return StrictUtf8.GetString(payload);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                "Canonical JSON bytes are not valid UTF-8.",
                exception);
        }
    }
}
