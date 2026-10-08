using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Core;

internal sealed record LocalSettingsSnapshot(byte[] Bytes, GameSettings Settings);

public partial class StateManager
{
    internal async Task<LocalSettingsSnapshot> ReadLocalSettingsAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        var bytes = await _fs.ReadLocalFileBytesAsync(lease, "config.json")
            ?? throw new InvalidDataException("config.json is absent; local bootstrap must complete before settings are read.");
        var loaded = DecodeLocalSettings(bytes);
        var candidate = CaptureRuntimeSnapshot().Settings;
        candidate.ApplyLoadedValues(loaded);
        return new(bytes, candidate);
    }

    internal byte[] EncodeLocalSettings(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Encoding.UTF8.GetPreamble().Concat(JsonSerializer.SerializeToUtf8Bytes(settings, JsonOpts)).ToArray();
    }

    // Load authority is independent of a menu preview or the injected mutable receiver.
    internal static GameSettings PrepareLocalLoadSettings(byte[]? bytes)
    {
        var candidate = new GameSettings();
        if (bytes != null) candidate.ApplyLoadedValues(DecodeLocalSettings(bytes));
        return candidate;
    }

    private static GameSettings DecodeLocalSettings(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return StrictJsonAuthority.Deserialize<GameSettings>(reader.ReadToEnd(), JsonOpts, "config.json")
                ?? throw new InvalidDataException("config.json must contain a settings object.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("config.json is invalid; its existing bytes have been preserved.", ex);
        }
    }
}
