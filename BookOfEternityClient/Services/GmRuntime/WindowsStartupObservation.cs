using System.Globalization;
using System.Text;

namespace BookOfEternityClient.Services.GmRuntime;

// A documented WMI restart timestamp, not a unique boot GUID or reboot proof.
// Equality/difference never admits an old nonterminal run or authorizes its stop.
internal sealed class WindowsStartupObservation
{
    internal const string Prefix = "wmi-lastboot-v1:";
    private WindowsStartupObservation(string value) => Value = value;
    internal string Value { get; }

    internal static WindowsStartupObservation Parse(byte[] bytes)
    {
        if (bytes.Length != Prefix.Length + 28 || bytes.Any(value => value > 127))
            throw new InvalidDataException("Windows startup observation is unavailable or malformed.");
        var value = Encoding.ASCII.GetString(bytes);
        if (!value.StartsWith(Prefix, StringComparison.Ordinal) ||
            !DateTimeOffset.TryParseExact(value[Prefix.Length..], "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
            throw new InvalidDataException("Windows startup observation is unavailable or malformed.");
        return new(value);
    }
}
